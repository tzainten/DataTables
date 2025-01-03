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

	private List<RowStruct> _internalEntries = new();
	private int _entryCount = 0;

	private TableView _tableView;

	private ControlSheet _sheet;

	private Splitter _splitter;

	private bool _isUnsaved = false;

	private UndoSystem _undo = new();

	private string _previousJson;

	public DataTableEditor( Asset asset, DataTable dataTable )
	{
		_asset = asset;
		_dataTable = dataTable;
		_structType = TypeLibrary.GetType( dataTable.StructType );

		_entryCount = _dataTable.EntryCount;
		foreach ( var entry in _dataTable.StructEntries )
			_internalEntries.Add( entry );

		JsonArray array = new();
		Json.SerializeArray( array, _internalEntries );
		string json = array.ToJsonString();
		_previousJson = json;

		DeleteOnClose = true;

		Size = new Vector2( 1000, 800 );
		Title = $"Data Table Editor - {asset.Path}";
		SetWindowIcon( "equalizer" );

		AddToolbar();

		Canvas = new();
		Canvas.Layout = Layout.Column();
		var layout = Canvas.Layout;

		_splitter = new(this);
		_splitter.IsVertical = true;

		_undo.SetSnapshotFunction( GetSnapshot );
		_undo.Initialize();

		Show();
		PopulateEditor();
	}

	private Action GetSnapshot()
	{
		var state = new Snapshot( _dataTable );
		return () => RestoreState( state );
	}

	private void RestoreState( Snapshot oldState )
	{
		oldState.Restore();
		_entryCount = _dataTable.EntryCount;
		_internalEntries.Clear();
		foreach ( var entry in _dataTable.StructEntries )
			_internalEntries.Add( entry );
		MarkUnsaved();
		PopulateEditor();
	}

	[Shortcut( "editor.undo", "CTRL+Z", ShortcutType.Window )]
	private void Undo()
	{
		if ( _undo.Undo() )
			EditorUtility.PlayRawSound( "sounds/editor/success.wav" );
	}

	private void MarkUnsaved()
	{
		_isUnsaved = true;
		Title = $"Data Table Editor - {_asset.Path}*";
	}

	private void MarkSaved()
	{
		_isUnsaved = false;
		Title = $"Data Table Editor - {_asset.Path}";
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

	private int _mouseUpFrames;

	private RealTimeSince _timeSinceChange;

	[EditorEvent.Frame]
	private void Tick()
	{
		_mouseUpFrames++;
		if ( Application.MouseButtons != 0 )
		{
			_mouseUpFrames = 0;
		}

		if ( _mouseUpFrames > 2 && _timeSinceChange > 0.5f )
		{
			JsonArray array = new();
			Json.SerializeArray( array, _internalEntries );
			string json = array.ToJsonString();
			if ( json != _previousJson )
			{
				_previousJson = json;
				_undo.Snapshot( "Generic Undo" );
				Log.Info( "Undo!" );
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

		if ( _splitter is not null && _splitter.IsValid )
			_splitter.DestroyChildren();

		for ( int i = _internalEntries.Count - 1; i >= 0; i-- )
		{
			if ( _internalEntries[i] is null )
				_internalEntries.RemoveAt( i );
		}

		ScrollArea scroll = new ScrollArea( _splitter );
		scroll.Canvas = new Widget( scroll )
		{
			Layout = Layout.Column(),
			VerticalSizeMode = SizeMode.CanGrow | SizeMode.Expand
		};

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

		_tableView = new TableView( Canvas );
		_tableView.MinimumHeight = 200;

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

		_tableView.SetItems( _internalEntries.ToList() );
		_tableView.FillHeader();

		if ( _internalEntries.Count > 0 )
		{
			_tableView.ListView.Selection.Add( _internalEntries[0] );
			PopulateControlSheet( _internalEntries[0].GetSerialized() );
		}

		_tableView.ItemClicked = o =>
		{
			PopulateControlSheet( o.GetSerialized() );
		};

		_splitter.AddWidget( _tableView );
		//_splitter.AddWidget( scroll.Canvas );
		_splitter.AddWidget( scroll );
		_splitter.SetCollapsible( 0, false );
		_splitter.SetCollapsible( 1, false );

		Canvas.Layout.Add( _splitter );
	}

	private void PopulateControlSheet( SerializedObject so )
	{
		_sheet.Clear( true );
		_sheet.AddObject( so, SheetFilter );
	}

	private void AddToolbar()
	{
		_toolBar = new ToolBar( this, "DataTableToolbar" );
		AddToolBar( _toolBar, ToolbarPosition.Top );

		_toolBar.Movable = false;
		_toolBar.AddOption( "Save", "common/save.png", Save ).StatusTip = "Saves this Data Table to disk";
		_toolBar.AddOption( "Browse", "common/browse.png" ).StatusTip = "Filler";
		_toolBar.AddSeparator();
		_toolBar.AddOption( "Add", "common/add.png", AddEntry ).StatusTip = "Append a new entry";
		_toolBar.AddOption( "Duplicate", "common/copy.png", DuplicateEntry ).StatusTip = "Appends a duplicate of the currently selected entry";
		_toolBar.AddOption( "Delete", "common/remove.png", RemoveEntry ).StatusTip = "Delete the currently selected entry";

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
			o.RowName = $"NewEntry_{_entryCount++}";

			_internalEntries.Add( o );
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

		var index = -1;
		foreach ( var selection in _tableView.ListView.Selection )
		{
			var row = selection as RowStruct;

			var tuple = _internalEntries.Index().First( x => x.Item.RowName == row.RowName );
			index = tuple.Index - 1;

			_tableView.ListView.RemoveItem( selection );
			_internalEntries.Remove( row );
		}

		_tableView.ListView.Selection.Clear();

		if ( index < 0 )
			index = 0;
		if ( index < _internalEntries.Count )
		{
			_tableView.ListView.Selection.Add( _internalEntries[index] );
			PopulateControlSheet( _internalEntries[index].GetSerialized() );
		}
	}

	private void AddEntry()
	{
		MarkUnsaved();

		var o = TypeLibrary.Create<RowStruct>( _dataTable.StructType );
		o.RowName = $"NewEntry_{_entryCount++}";

		_internalEntries.Add( o );
		_tableView.AddItem( o );

		_tableView.ListView.Selection.Clear();
		_tableView.ListView.Selection.Add( o );

		PopulateControlSheet( o.GetSerialized() );

		_tableView.ListView.ScrollTo( o );
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
			o.RowName = $"NewEntry_{_entryCount++}";

			_internalEntries.Add( o );
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

		if ( _internalEntries.Count == 0 )
			_entryCount = 0;

		_dataTable.StructEntries.Clear();
		foreach ( var entry in _internalEntries )
			_dataTable.StructEntries.Add( entry );

		_dataTable.EntryCount = _entryCount;
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
