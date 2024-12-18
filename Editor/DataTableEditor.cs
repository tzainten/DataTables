using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DataTables;
using DataTablesEditor.Widgets;
using Editor;
using Sandbox;
using Json = DataTables.Json;

namespace DataTablesEditor;

public class DataTableEditor : DockWindow
{
	public bool CanOpenMultipleAssets => false;

	private Asset _asset;
	private DataTable _dataTable;
	private TypeDescription _structType;

	private ToolBar _toolBar;

	private List<RowStruct> _internalEntries;

	private TableView _tableView;

	private ControlSheet _sheet;

	private Splitter _splitter;

	public DataTableEditor( Asset asset, DataTable dataTable )
	{
		_asset = asset;
		_dataTable = dataTable;
		_structType = TypeLibrary.GetType( dataTable.StructType );
		_internalEntries = _dataTable.StructEntries;

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

		Show();
		PopulateEditor();
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
		sheetCanvas.Layout = Layout.Column();
		sheetCanvas.Layout.AddStretchCell();
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
			_sheet.Clear( true );
			_sheet.AddObject( _internalEntries[0].GetSerialized() );
		}

		_tableView.ItemClicked = o =>
		{
			_sheet.Clear( true );
			_sheet.AddObject( o.GetSerialized() );
		};

		_splitter.AddWidget( _tableView );
		//_splitter.AddWidget( scroll.Canvas );
		_splitter.AddWidget( scroll );
		_splitter.SetCollapsible( 0, false );
		_splitter.SetCollapsible( 1, false );

		Canvas.Layout.Add( _splitter );
	}

	private void AddToolbar()
	{
		_toolBar = new ToolBar( this, "DataTableToolbar" );
		AddToolBar( _toolBar, ToolbarPosition.Top );

		_toolBar.Movable = false;
		_toolBar.AddOption( "Save", "common/save.png" ).StatusTip = "Saves this Data Table to disk";
		_toolBar.AddOption( "Browse", "common/browse.png" ).StatusTip = "Filler";
		_toolBar.AddSeparator();
		_toolBar.AddOption( "Add", "common/add.png", AddEntry ).StatusTip = "Append a new entry";
		_toolBar.AddOption( "Duplicate", "common/copy.png" ).StatusTip = "Appends a duplicate of the currently selected entry";
		_toolBar.AddOption( "Delete", "common/remove.png", RemoveEntry ).StatusTip = "Delete the currently selected entry";

		/*var stretch = new Widget();
		stretch.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
		_toolBar.AddWidget( stretch );

		var dropdown = new Dropdown( _dataTable.StructType );
		dropdown.FixedWidth = 200;
		dropdown.Icon = "account_tree";
		_toolBar.AddWidget( dropdown );*/
	}

	private void RemoveEntry()
	{
		var selection = _tableView.ListView.Selection.First() as RowStruct;
		_tableView.ListView.RemoveItem( selection );
		_sheet.Clear( true );

		_tableView.ListView.Selection.Clear();
		var tuple = _internalEntries.Index().First( x => x.Item.RowName == selection.RowName );

		_internalEntries.Remove( selection );

		var index = tuple.Index - 1;
		if ( index < 0 )
			index = 0;

		if ( index < _internalEntries.Count )
		{
			_tableView.ListView.Selection.Add( _internalEntries[index] );
			_sheet.Clear( true );
			_sheet.AddObject( _internalEntries[index].GetSerialized() );
		}
	}

	private void AddEntry()
	{
		var o = TypeLibrary.Create<RowStruct>( _dataTable.StructType );
		o.RowName = $"NewEntry_{_internalEntries.Count + 1}";

		_internalEntries.Add( o );
		_tableView.AddItem( o );

		_tableView.ListView.Selection.Clear();
		_tableView.ListView.Selection.Add( o );

		_sheet.Clear( true );
		_sheet.AddObject( o.GetSerialized() );

		_tableView.ListView.ScrollTo( o );
	}

	[Shortcut( "editor.save", "CTRL+S", ShortcutType.Window )]
	private void Save()
	{
		_dataTable.StructEntries = _internalEntries;
		var json = Json.Serialize( _dataTable, writer =>
		{
			writer.WritePropertyName( "__references" );

			var references = _asset.GetReferences( false );
			Json.SerializeArray( writer, references.Select( x => x.Path ) );

			writer.WritePropertyName( "__version" );
			writer.WriteNumberValue( _dataTable.ResourceVersion );
		} );
		File.WriteAllText( _asset.AbsolutePath, json );
	}

	public override void SetWindowIcon( string name )
	{
		Pixmap pixmap = new Pixmap( 128, 128 );
		pixmap.Clear( Color.Transparent );

		using ( Paint.ToPixmap( pixmap ) )
		{
			Rect rect = new Rect( 0.0f, 0.0f, 128f, 128f );
			Paint.ClearPen();
			Paint.SetBrush( in Color.Green );
			Paint.DrawRect( in rect, 16f );
			Paint.SetPen( in Color.Black );
			Paint.DrawIcon( rect, name, 120f );
		}

		SetWindowIcon( pixmap );
	}
}
