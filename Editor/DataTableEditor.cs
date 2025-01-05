using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using DataTables;
using Editor;
using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Helpers;
using Application = Editor.Application;
using Json = DataTables.Json;

namespace DataTablesEditor;

public class DataTableEditor : DockWindow
{
	public bool CanOpenMultipleAssets => false;

	private Asset _asset;
	private DataTable _dataTable;
	private TypeDescription _structType;

	private ToolBar _toolBar;

	public List<RowStruct> InternalEntries = new();
	public int EntryCount = 0;

	private TableView _tableView;

	private ControlSheet _sheet;

	private Splitter _splitter;

	private bool _isUnsaved = false;

	private string _previousJson;

	private PropertyDescription[] _previousProperties;

	private string _defaultDockState;

	private UndoStack _undoStack = new();

	public DataTableEditor( Asset asset, DataTable dataTable )
	{
		_asset = asset;
		_dataTable = dataTable;
		_structType = TypeLibrary.GetType( dataTable.StructType );

		_previousProperties = _structType.Properties.ToArray();

		EntryCount = _dataTable.EntryCount;

		foreach ( var entry in _dataTable.StructEntries )
			InternalEntries.Add( entry );

		JsonArray array = new();
		Json.SerializeArray( array, InternalEntries );
		string json = array.ToJsonString();
		_previousJson = json;

		DeleteOnClose = true;

		Size = new Vector2( 1000, 800 );
		Title = $"Data Table Editor - {asset.Path}";
		SetWindowIcon( "equalizer" );

		AddToolbar();
		BuildMenuBar();

		Show();
		PopulateEditor();
		RestoreFromStateCookie();
	}

	private string SerializeEntries()
	{
		JsonArray array = new();
		Json.SerializeArray( array, InternalEntries );
		return array.ToJsonString();
	}

	[Shortcut( "editor.undo", "CTRL+Z", ShortcutType.Window )]
	private void Undo()
	{
		if ( _undoStack.Undo() is UndoOp op )
		{
			Log.Info( $"Undo ({op.name})" );

			Json._currentProperty = null;
			InternalEntries = (List<RowStruct>)Json.DeserializeArray( JsonNode.Parse( op.undoBuffer )?.AsArray(), typeof(List<RowStruct>) );
			Json._currentProperty = null;
			_previousJson = SerializeEntries();
			MarkUnsaved();
			PopulateEditor();

			EditorUtility.PlayRawSound( "sounds/editor/success.wav" );
		}
	}

	[Shortcut( "editor.redo", "CTRL+Y", ShortcutType.Window )]
	private void Redo()
	{
		if ( _undoStack.Redo() is UndoOp op )
		{
			Log.Info( $"Redo ({op.name})" );

			Json._currentProperty = null; // @TODO: This is dumb. DO BETTER!
			InternalEntries = (List<RowStruct>)Json.DeserializeArray( JsonNode.Parse( op.redoBuffer )?.AsArray(), typeof(List<RowStruct>) );
			Json._currentProperty = null;
			_previousJson = SerializeEntries();
			MarkUnsaved();
			PopulateEditor();

			EditorUtility.PlayRawSound( "sounds/editor/success.wav" );
		}
	}

	private void MarkUnsaved()
	{
		_isUnsaved = true;
		//Title = $"Data Table Editor - {_asset.Path}*";
	}

	private void MarkSaved()
	{
		_isUnsaved = false;
		//Title = $"Data Table Editor - {_asset.Path}";
	}

	private bool SheetFilter( SerializedProperty property )
	{
		var obj = property.Parent;
		obj.OnPropertyChanged = serializedProperty =>
		{
			MarkUnsaved();
		};
		return true;
	}

	private string EvaluateTitle()
	{
		return $"Data Table Editor - {_asset.Path}{(_isUnsaved ? "*" : "")} {(Game.IsPlaying ? "(Restricted)" : "")}";
	}

	private int _mouseUpFrames;

	private RealTimeSince _timeSinceChange;

	[EditorEvent.Frame]
	private void Tick()
	{
		Title = EvaluateTitle();

		_addOption.Enabled = !Game.IsPlaying;
		_duplicateOption.Enabled = !Game.IsPlaying;
		_deleteOption.Enabled = !Game.IsPlaying;

		_mouseUpFrames++;
		if ( Application.MouseButtons != 0 )
		{
			_mouseUpFrames = 0;
		}

		if ( _mouseUpFrames > 2 && _timeSinceChange > 0.5f )
		{
			if ( InternalEntries is null )
				return;

			string json = SerializeEntries();
			if ( json != _previousJson )
			{
				_undoStack.PushUndo("Modified a RowStruct", _previousJson  );
				_undoStack.PushRedo( json );
				_previousJson = json;
				MarkUnsaved();
				_timeSinceChange = 0;
			}
		}
	}

	[EditorEvent.Hotload]
	private void PopulateEditor()
	{
		if ( !Visible )
			return;

		PropertyDescription[] properties = _structType.Properties.ToArray();
		foreach ( var property in properties )
		{
			var type = property.PropertyType;
			bool isList = type.IsAssignableTo( typeof(IList) );
			bool isDictionary = type.IsAssignableTo( typeof(IDictionary) );

			if ( type.IsGenericType && (!isList && !isDictionary) )
			{
				StructDialog popup = new StructDialog( $"Data Table Editor - {_asset.Path}" );
				popup.DeleteOnClose = true;
				popup.OnWindowClosed = delegate
				{
					Close();
				};
				popup.SetModal(on: true, application: true);
				popup.Hide();
				popup.Show();
				return;
			}
		}

		DockManager.Clear();
		DockManager.RegisterDockType( "Table View", "equalizer", null, false );
		DockManager.RegisterDockType( "Row Editor", "tune", null, false );

		if ( _splitter is not null && _splitter.IsValid )
			_splitter.DestroyChildren();

		for ( int i = InternalEntries.Count - 1; i >= 0; i-- )
		{
			if ( InternalEntries[i] is null )
				InternalEntries.RemoveAt( i );
		}

		ScrollArea scroll = new ScrollArea( _splitter );
		scroll.Canvas = new Widget( scroll )
		{
			Layout = Layout.Row(),
			VerticalSizeMode = SizeMode.CanGrow | SizeMode.Expand
		};

		scroll.Canvas.Layout.AddStretchCell();
		scroll.Canvas.OnPaintOverride = () =>
		{
			Paint.ClearPen();
			Paint.SetBrush( Theme.WidgetBackground );
			Paint.DrawRect( scroll.Canvas.LocalRect );

			return false;
		};

		_sheet = new ControlSheet();

		var layout = scroll.Canvas.Layout;

		var sheetCanvas = new Widget( scroll.Canvas );
		sheetCanvas.MinimumHeight = 200;
		sheetCanvas.Layout = Layout.Column();
		sheetCanvas.Layout.Add( _sheet );
		sheetCanvas.Layout.AddStretchCell();

		layout.Add( sheetCanvas );
		layout.AddStretchCell();

		var structType = TypeLibrary.GetType( _dataTable.StructType );
		if ( structType is null )
			return;

		Widget tableView = new(this){ WindowTitle = "Table View" };
		tableView.Layout = Layout.Column();
		tableView.SetWindowIcon( "equalizer" );
		tableView.Name = "Table View";

		_tableView = new TableView( tableView );

		var rowNameCol = _tableView.AddColumn();
		rowNameCol.Name = "RowName";
		rowNameCol.TextColor = Color.Parse( "#e1913c" ).GetValueOrDefault();
		rowNameCol.Value = o =>
		{
			return structType.GetProperty( "RowName" ).GetValue( o )?.ToString() ?? "";
		};

		foreach ( var property in structType.Properties.Where( x => x.IsPublic && !x.IsStatic ) )
		{
			if ( property.Name == "RowName" )
				continue;

			var col = _tableView.AddColumn();
			col.Name = property.Name;
			col.Value = o =>
			{
				return property.GetValue( o )?.ToString() ?? "";
			};
		}

		_tableView.SetItems( InternalEntries.ToList() );
		_tableView.FillHeader();

		if ( InternalEntries.Count > 0 )
		{
			_tableView.ListView.Selection.Add( InternalEntries[0] );
			PopulateControlSheet( InternalEntries[0].GetSerialized() );
		}

		_tableView.ItemClicked = o =>
		{
			PopulateControlSheet( o.GetSerialized() );
		};

		tableView.Layout.Add( _tableView );

		Widget rowEditor = new(this){ WindowTitle = "Row Editor" };
		rowEditor.Layout = Layout.Column();
		rowEditor.SetWindowIcon( "tune" );
		rowEditor.Name = "Row Editor";

		rowEditor.Layout.Add( scroll );

		var flags = DockManager.DockProperty.HideCloseButton | DockManager.DockProperty.HideOnClose | DockManager.DockProperty.DisallowFloatWindow;

		DockManager.AddDock( null, tableView, DockArea.Top, flags );
		DockManager.AddDock( null, rowEditor, DockArea.Bottom, flags );
		DockManager.RaiseDock( "Table View" );
		DockManager.Update();

		_defaultDockState = DockManager.State;

		if ( StateCookie != "DataTableEditor" )
		{
			StateCookie = "DataTableEditor";
		}
	}

	private void PopulateControlSheet( SerializedObject so )
	{
		_sheet.Clear( true );
		_sheet.AddObject( so, SheetFilter );
	}

	public void BuildMenuBar()
	{
		var file = MenuBar.AddMenu( "File" );
		file.AddOption( "New", "common/new.png", null, "editor.new" ).StatusTip = "New Graph";
		file.AddOption( "Open", "common/open.png", null, "editor.open" ).StatusTip = "Open Graph";
		file.AddOption( "Save", "common/save.png", null, "editor.save" ).StatusTip = "Save Graph";

		var view = MenuBar.AddMenu( "View" );
		view.AboutToShow += () => OnViewMenu( view );
	}

	private void OnViewMenu( Menu view )
	{
		view.Clear();
		view.AddOption( "Restore To Default", "settings_backup_restore", RestoreDefaultDockLayout );
		view.AddSeparator();

		foreach ( var dock in DockManager.DockTypes )
		{
			var o = view.AddOption( dock.Title, dock.Icon );
			o.Checkable = true;
			o.Checked = DockManager.IsDockOpen( dock.Title );
			o.Toggled += ( b ) =>
			{
				DockManager.SetDockState( dock.Title, b );
			};
		}
	}

	protected override void RestoreDefaultDockLayout()
	{
		DockManager.State = _defaultDockState;

		SaveToStateCookie();
	}

	private Option _addOption;
	private Option _duplicateOption;
	private Option _deleteOption;
	private void AddToolbar()
	{
		_toolBar = new ToolBar( this, "DataTableToolbar" );
		AddToolBar( _toolBar, ToolbarPosition.Top );

		_toolBar.Movable = false;
		_toolBar.AddOption( "Save", "common/save.png", Save ).StatusTip = "Saves this Data Table to disk";
		_toolBar.AddOption( "Browse", "common/browse.png" ).StatusTip = "Filler";
		_toolBar.AddSeparator();
		_addOption = _toolBar.AddOption( "Add", "common/add.png", AddEntry );
		_addOption.StatusTip = "Append a new entry";
		_duplicateOption = _toolBar.AddOption( "Duplicate", "common/copy.png", DuplicateEntry );
		_duplicateOption.StatusTip = "Appends a duplicate of the currently selected entry";
		_deleteOption = _toolBar.AddOption( "Delete", "common/remove.png", RemoveEntry );
		_deleteOption.StatusTip = "Delete the currently selected entry";

		/*var stretch = new Widget();
		stretch.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
		_toolBar.AddWidget( stretch );

		var dropdown = new Dropdown( _dataTable.StructType );
		dropdown.FixedWidth = 200;
		dropdown.Icon = "account_tree";
		_toolBar.AddWidget( dropdown );*/
	}

	private void DuplicateEntry()
	{
		if ( _tableView.ListView.Selection.Count > 0 )
			MarkUnsaved();

		List<object> newSelections = new();
		foreach ( var selection in _tableView.ListView.Selection )
		{
			var row = selection as RowStruct;
			var o = TypeLibrary.Clone<RowStruct>( row );
			o.RowName = $"NewEntry_{EntryCount++}";

			InternalEntries.Add( o );
			_tableView.AddItem( o );

			PopulateControlSheet( o.GetSerialized() );

			newSelections.Add( o );
		}

		_tableView.ListView.Selection.Clear();
		foreach ( var newSelection in newSelections )
		{
			_tableView.ListView.Selection.Add( newSelection );
			_tableView.ListView.ScrollTo( newSelection );
		}
	}

	private void RemoveEntry()
	{
		_sheet.Clear( true );

		if ( _tableView.ListView.Selection.Count > 0 )
			MarkUnsaved();

		_previousJson = SerializeEntries();

		var index = -1;
		foreach ( var selection in _tableView.ListView.Selection )
		{
			var row = selection as RowStruct;

			var tuple = InternalEntries.Index().First( x => x.Item.RowName == row.RowName );
			index = tuple.Index - 1;

			_tableView.ListView.RemoveItem( selection );
			InternalEntries.Remove( row );
		}

		_tableView.ListView.Selection.Clear();

		if ( index < 0 )
			index = 0;
		if ( index < InternalEntries.Count )
		{
			_tableView.ListView.Selection.Add( InternalEntries[index] );
			PopulateControlSheet( InternalEntries[index].GetSerialized() );
		}

		var json = SerializeEntries();
		_undoStack.PushUndo( $"Remove Row(s)", _previousJson );
		_undoStack.PushRedo( json );
		_previousJson = json;
	}

	private void AddEntry()
	{
		MarkUnsaved();

		_previousJson = SerializeEntries();

		var o = TypeLibrary.Create<RowStruct>( _dataTable.StructType );
		o.RowName = $"NewEntry_{EntryCount++}";

		InternalEntries.Add( o );
		_tableView.AddItem( o );

		_tableView.ListView.Selection.Clear();
		_tableView.ListView.Selection.Add( o );

		PopulateControlSheet( o.GetSerialized() );

		_tableView.ListView.ScrollTo( o );

		var json = SerializeEntries();
		_undoStack.PushUndo( $"Add Entry {o.RowName}", _previousJson );
		_undoStack.PushRedo( json );
		_previousJson = json;
	}

	private List<object> _clipboard = new();

	[Shortcut( "editor.copy", "CTRL+C", ShortcutType.Window )]
	private void Copy()
	{
		_clipboard.Clear();
		foreach ( var selection in _tableView.ListView.Selection )
			_clipboard.Add( selection );
	}

	[Shortcut( "editor.paste", "CTRL+V", ShortcutType.Window )]
	private void Paste()
	{
		if ( _clipboard.Count == 0 )
			return;

		MarkUnsaved();

		List<object> newSelections = new();
		foreach ( var selection in _clipboard )
		{
			var row = selection as RowStruct;
			var o = TypeLibrary.Clone<RowStruct>( row );
			o.RowName = $"NewEntry_{EntryCount++}";

			InternalEntries.Add( o );
			_tableView.AddItem( o );

			PopulateControlSheet( o.GetSerialized() );

			newSelections.Add( o );
		}

		_tableView.ListView.Selection.Clear();
		foreach ( var newSelection in newSelections )
		{
			_tableView.ListView.Selection.Add( newSelection );
			_tableView.ListView.ScrollTo( newSelection );
		}
	}

	[Shortcut( "editor.delete", "DEL", ShortcutType.Window )]
	private void Delete()
	{
		RemoveEntry();
	}

	[Shortcut( "editor.save", "CTRL+S", ShortcutType.Window )]
	private void Save()
	{
		MarkSaved();

		if ( InternalEntries.Count == 0 )
			EntryCount = 0;

		_dataTable.StructEntries.Clear();
		foreach ( var entry in InternalEntries )
			_dataTable.StructEntries.Add( entry );

		_dataTable.EntryCount = EntryCount;
		_asset.SaveToDisk( _dataTable );
	}

	protected override bool OnClose()
	{
		if ( _isUnsaved )
		{
			CloseDialog dialog = new($"Data Table Editor - {_asset.Path}", () =>
			{
				Save();
				Close();
			}, () =>
			{
				_isUnsaved = false;
				if ( _dataTable.StructEntries.Count == 0 )
					_dataTable.EntryCount = 0;
				Close();
			});
		}
		else
		{
			SaveToStateCookie();
		}

		return !_isUnsaved;
	}

	public override void SetWindowIcon( string name )
	{
		Pixmap pixmap = new Pixmap( 128, 128 );
		pixmap.Clear( Color.Transparent );

		using ( Paint.ToPixmap( pixmap ) )
		{
			Rect rect = new Rect( 0.0f, 0.0f, 128f, 128f );
			Paint.ClearPen();
			Paint.SetBrush( Color.Parse( "#b0e24d" ).GetValueOrDefault() );
			Paint.DrawRect( in rect, 16f );
			Paint.SetPen( in Color.Black );
			Paint.DrawIcon( rect, name, 120f );
		}

		SetWindowIcon( pixmap );
	}
}
