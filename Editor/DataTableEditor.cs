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

	public Dictionary<string, RowStruct> InternalEntries = new();
	public int EntryCount = 0;

	private TableView _tableView;

	private ControlSheet _sheet;

	private bool _isUnsaved = false;

	private string _previousJson;
	private EditorState _previousEditorState;

	private PropertyDescription[] _previousProperties;

	private string _defaultDockState;

	private UndoStack _undoStack = new();
	private UndoHistory _undoHistory;

	public DataTableEditor( Asset asset, DataTable dataTable )
	{
		_asset = asset;
		_dataTable = dataTable;
		_structType = TypeLibrary.GetType( dataTable.StructType );

		_previousProperties = _structType.Properties.ToArray();

		EntryCount = _dataTable.EntryCount;

		foreach ( var pair in _dataTable.StructEntries )
			InternalEntries.Add( pair.Key, TypeLibrary.Clone<RowStruct>( pair.Value ) );

		_previousJson = SerializeEntries();

		DeleteOnClose = true;

		Size = new Vector2( 1000, 800 );
		Title = $"Data Table Editor - {asset.Path}";
		SetWindowIcon( "equalizer" );

		AddToolbar();
		BuildMenuBar();

		Show();
		PopulateEditor();

		_previousEditorState = new EditorState(GetSelectedNames(), _sheetRowName);
	}

	private string SerializeEntries()
	{
		JsonObject jobj = new();
		Json.SerializeDictionary( jobj, InternalEntries );
		return jobj.ToJsonString();
	}

	[Shortcut( "editor.undo", "CTRL+Z", ShortcutType.Window )]
	private void Undo()
	{
		if ( _undoStack.Undo() is UndoOp op )
		{
			Json._currentProperty = null;
			InternalEntries = (Dictionary<string, RowStruct>)Json.DeserializeDictionary( JsonNode.Parse( op.undoBuffer )?.AsObject(), typeof(Dictionary<string, RowStruct>) );
			Json._currentProperty = null;
			_previousJson = SerializeEntries();
			MarkUnsaved();
			//PopulateEditor();
			UpdateViewAndEditor();

			EditorState undoState = op.UndoEditorState;
			_tableView.ListView.Selection.Clear();
			foreach ( var rowName in undoState.SelectedNames )
			{
				var pair = InternalEntries.FirstOrDefault( x => x.Key == rowName );
				if ( pair.Key is not null )
					_tableView.ListView.Selection.Add( pair );
			}

			var _pair = InternalEntries.FirstOrDefault( x => x.Key == undoState.SheetRowName );
			if ( _pair.Key is not null )
				PopulateControlSheet( _pair.Value );
		}
	}

	[Shortcut( "editor.redo", "CTRL+Y", ShortcutType.Window )]
	private void Redo()
	{
		if ( _undoStack.Redo() is UndoOp op )
		{
			Json._currentProperty = null; // @TODO: This is dumb. DO BETTER!
			InternalEntries = (Dictionary<string, RowStruct>)Json.DeserializeDictionary( JsonNode.Parse( op.redoBuffer )?.AsObject(), typeof(Dictionary<string, RowStruct>) );
			Json._currentProperty = null;
			_previousJson = SerializeEntries();
			MarkUnsaved();
			//PopulateEditor();
			UpdateViewAndEditor();

			EditorState redoState = op.RedoEditorState;
			_tableView.ListView.Selection.Clear();
			foreach ( var rowName in redoState.SelectedNames )
			{
				var pair = InternalEntries.FirstOrDefault( x => x.Key == rowName );
				if ( pair.Key is not null )
					_tableView.ListView.Selection.Add( pair );
			}

			var _pair = InternalEntries.FirstOrDefault( x => x.Key == redoState.SheetRowName );
			if ( _pair.Key is not null )
				PopulateControlSheet( _pair.Value );
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
		_undoHistory.UndoLevel = _undoStack.UndoLevel;

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
				EditorState state = new(GetSelectedNames(), _sheetRowName);

				_undoStack.PushUndo("Modified a RowStruct", _previousJson, _previousEditorState );
				OnUndoPushed();
				_undoStack.PushRedo( json, state );
				_previousJson = json;
				_previousEditorState = state;
				MarkUnsaved();
				_timeSinceChange = 0;
			}
		}
	}

	private void UpdateViewAndEditor()
	{
		if ( !Visible )
			return;

		_tableEditor.Layout.Clear( true );

		_tableView = new TableView( _tableEditor );

		var structType = TypeLibrary.GetType( _structType.TargetType );

		var rowNameCol = _tableView.AddColumn();
		rowNameCol.Name = "RowName";
		rowNameCol.TextColor = Color.Parse( "#e1913c" ).GetValueOrDefault();
		rowNameCol.Value = o =>
		{
			var pair = (KeyValuePair<string, RowStruct>)o;
			return pair.Key;
		};

		foreach ( var property in structType.Properties.Where( x => x.IsPublic && !x.IsStatic ) )
		{
			if ( property.Name == "RowName" )
				continue;

			var col = _tableView.AddColumn();
			col.Name = property.Name;
			col.Value = o =>
			{
				var pair = (KeyValuePair<string, RowStruct>)o;
				return property.GetValue( pair.Value )?.ToString() ?? "";
			};
		}

		_tableView.SetItems( InternalEntries.ToList() );
		_tableView.FillHeader();

		if ( InternalEntries.Count > 0 )
		{
			_tableView.ListView.Selection.Add( InternalEntries.First() );
			PopulateControlSheet( InternalEntries.First().Value );
			_sheetRowName = InternalEntries.First().Key;
		}

		_tableView.ItemClicked = o =>
		{
			var pair = (KeyValuePair<string, RowStruct>)o;
			PopulateControlSheet( pair.Value );
			_sheetRowName = pair.Key;
		};

		_tableEditor.Layout.Add( _tableView );
	}

	private Widget _rowEditor;
	private Widget _tableEditor;

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
		DockManager.RegisterDockType( "Undo History", "history", null, false );

		List<string> nullKeys = new();
		foreach ( var pair in InternalEntries )
		{
			if ( pair.Value is null )
				nullKeys.Add( pair.Key );
		}

		foreach ( var key in nullKeys )
			InternalEntries.Remove( key );

		_rowEditor = new(this){ WindowTitle = "Row Editor" };
		_rowEditor.Layout = Layout.Column();
		_rowEditor.SetWindowIcon( "tune" );
		_rowEditor.Name = "Row Editor";

		ScrollArea scroll = new ScrollArea( _rowEditor );
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

		_tableEditor = new(this){ WindowTitle = "Table View" };
		_tableEditor.Layout = Layout.Column();
		_tableEditor.SetWindowIcon( "equalizer" );
		_tableEditor.Name = "Table View";

		_tableView = new TableView( _tableEditor );

		var rowNameCol = _tableView.AddColumn();
		rowNameCol.Name = "RowName";
		rowNameCol.TextColor = Color.Parse( "#e1913c" ).GetValueOrDefault();
		rowNameCol.Value = o =>
		{
			var pair = (KeyValuePair<string, RowStruct>)o;
			return pair.Key;
		};

		foreach ( var property in structType.Properties.Where( x => x.IsPublic && !x.IsStatic ) )
		{
			if ( property.Name == "RowName" )
				continue;

			var col = _tableView.AddColumn();
			col.Name = property.Name;
			col.Value = o =>
			{
				var pair = (KeyValuePair<string, RowStruct>)o;
				return property.GetValue( pair.Value )?.ToString() ?? "";
			};
		}

		_tableView.SetItems( InternalEntries.ToList() );
		_tableView.FillHeader();

		if ( InternalEntries.Count > 0 )
		{
			_tableView.ListView.Selection.Add( InternalEntries.First() );
			PopulateControlSheet( InternalEntries.First().Value );
			_sheetRowName = InternalEntries.First().Key;
		}

		_tableView.ItemClicked = o =>
		{
			var pair = (KeyValuePair<string, RowStruct>)o;
			PopulateControlSheet( pair.Value );
			_sheetRowName = pair.Key;
		};

		_tableEditor.Layout.Add( _tableView );

		_rowEditor.Layout.Add( scroll );

		_undoHistory = new UndoHistory( this, _undoStack );
		_undoHistory.OnUndo = Undo;
		_undoHistory.OnRedo = Redo;
		_undoHistory.OnHistorySelected = SetUndoLevel;

		var flags = DockManager.DockProperty.HideCloseButton | DockManager.DockProperty.HideOnClose | DockManager.DockProperty.DisallowFloatWindow;

		DockManager.AddDock( null, _tableEditor, DockArea.Top, flags );
		DockManager.AddDock( null, _rowEditor, DockArea.Bottom, flags );
		DockManager.AddDock( _rowEditor, _undoHistory, DockArea.Inside, flags );
		DockManager.RaiseDock( "Row Editor" );
		DockManager.Update();

		_defaultDockState = DockManager.State;

		if ( StateCookie != "DataTableEditor" )
		{
			StateCookie = "DataTableEditor";
		}
		else
		{
			RestoreFromStateCookie();
		}
	}

	public void OnUndoPushed()
	{
		_undoHistory.History = _undoStack.Names;
	}

	private void SetUndoLevel( int level )
	{
		if ( _undoStack.SetUndoLevel( level ) is UndoOp op )
		{
			Json._currentProperty = null; // @TODO: This is dumb. DO BETTER!
			InternalEntries = (Dictionary<string, RowStruct>)Json.DeserializeDictionary( JsonNode.Parse( op.redoBuffer )?.AsObject(), typeof(Dictionary<string, RowStruct>) );
			Json._currentProperty = null;
			_previousJson = SerializeEntries();
			MarkUnsaved();
			//PopulateEditor();
			UpdateViewAndEditor();
		}
	}

	private string _sheetRowName;

	private void PopulateControlSheet( object so )
	{
		_sheet.Clear( true );
		_sheet.AddObject( so.GetSerialized(), SheetFilter );
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
		foreach ( KeyValuePair<string, RowStruct> pair in _tableView.ListView.Selection )
		{
			var o = TypeLibrary.Clone<RowStruct>( pair.Value );
			//o.RowName = $"NewEntry_{EntryCount++}";

			string key = $"NewEntry_{EntryCount++}";
			KeyValuePair<string, RowStruct> newPair = new(key, o);

			InternalEntries.Add( key, o );
			_tableView.AddItem( newPair );

			PopulateControlSheet( o );
			_sheetRowName = key;

			newSelections.Add( newPair );
		}

		_tableView.ListView.Selection.Clear();
		foreach ( var newSelection in newSelections )
		{
			_tableView.ListView.Selection.Add( newSelection );
			_tableView.ListView.ScrollTo( newSelection );
		}
	}

	private List<string> GetSelectedNames()
	{
		List<string> result = new();

		foreach ( KeyValuePair<string, RowStruct> pair in _tableView.ListView.Selection )
			result.Add( pair.Key );

		return result;
	}

	private void RemoveEntry()
	{
		_sheet.Clear( true );

		if ( _tableView.ListView.Selection.Count > 0 )
			MarkUnsaved();

		_previousJson = SerializeEntries();
		_previousEditorState = new(GetSelectedNames(), _sheetRowName);

		int index = -1;
		string key = string.Empty;
		foreach ( KeyValuePair<string, RowStruct> pair in _tableView.ListView.Selection )
		{
			var tuple = InternalEntries.Index().First( x => x.Item.Key == pair.Key );
			index = tuple.Index - 1;

			var tuple2 = InternalEntries.Index().FirstOrDefault( x => x.Index == index );
			key = tuple2.Item.Key;

			_tableView.ListView.RemoveItem( pair );
			InternalEntries.Remove( pair.Key );
		}

		_tableView.ListView.Selection.Clear();

		if ( key is not null && InternalEntries.ContainsKey( key ) )
		{
			_tableView.ListView.Selection.Add( new KeyValuePair<string, RowStruct>(key, InternalEntries[key]) );

			PopulateControlSheet( InternalEntries[key] );
			_sheetRowName = key;
		}

		EditorState state = new(GetSelectedNames(), _sheetRowName);

		var json = SerializeEntries();
		_undoStack.PushUndo( $"Remove Row(s)", _previousJson, _previousEditorState );
		OnUndoPushed();
		_undoStack.PushRedo( json, state );
		_previousJson = json;
		_previousEditorState = state;
	}

	private void AddEntry()
	{
		MarkUnsaved();

		_previousJson = SerializeEntries();

		_previousEditorState = new(GetSelectedNames(), _sheetRowName);

		var o = TypeLibrary.Create<RowStruct>( _dataTable.StructType );

		string key = $"NewEntry_{EntryCount++}";
		KeyValuePair<string, RowStruct> newPair = new(key, o);

		InternalEntries.Add( key, o );
		_tableView.AddItem( newPair );

		_tableView.ListView.Selection.Clear();
		_tableView.ListView.Selection.Add( newPair );

		PopulateControlSheet( o );
		_sheetRowName = key;

		_tableView.ListView.ScrollTo( newPair );

		EditorState state = new(GetSelectedNames(), _sheetRowName);

		var json = SerializeEntries();
		_undoStack.PushUndo( $"Add Entry {key}", _previousJson, _previousEditorState );
		OnUndoPushed();
		_undoStack.PushRedo( json, state );
		_previousJson = json;
		_previousEditorState = state;
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

			string key = $"NewEntry_{EntryCount++}";
			KeyValuePair<string, RowStruct> newPair = new(key, o);

			InternalEntries.Add( key, o );
			_tableView.AddItem( newPair );

			PopulateControlSheet( o );

			newSelections.Add( newPair );
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

		/*_dataTable.StructEntries.Clear();
		foreach ( var pair in InternalEntries )
			_dataTable.StructEntries.Add( pair.Key, pair.Value );*/

		List<string> nullKeys = new();
		foreach ( var pair in _dataTable.StructEntries )
		{
			if ( !InternalEntries.ContainsKey( pair.Key ) )
				nullKeys.Add( pair.Key );
		}

		foreach ( var key in nullKeys )
			_dataTable.StructEntries.Remove( key );

		foreach ( var pair in InternalEntries )
		{
			if ( _dataTable.StructEntries.ContainsKey( pair.Key ) )
			{
				var entry = _dataTable.StructEntries[pair.Key];
				entry.Integer = pair.Value.Integer;
			}
			else
			{
				_dataTable.StructEntries.Add( pair.Key, TypeLibrary.Clone<RowStruct>( pair.Value ) );
			}
		}

		_dataTable.EntryCount = EntryCount;
		_asset.SaveToDisk( _dataTable );

		EditorUtility.PlayRawSound( "sounds/editor/success.wav" );
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
