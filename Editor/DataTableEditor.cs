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

	private ToolBar _toolBar;

	private List<RowStruct> _internalEntries;

	private TableView _tableView;

	public DataTableEditor( Asset asset, DataTable dataTable )
	{
		_asset = asset;
		_dataTable = dataTable;
		_internalEntries = _dataTable.StructEntries;

		DeleteOnClose = true;

		Size = new Vector2( 1000, 800 );
		Title = "Data Table Editor";
		SetWindowIcon( "equalizer" );

		AddToolbar();

		Canvas = new();
		Canvas.Layout = Layout.Column();
		var layout = Canvas.Layout;

		_tableView = new TableView( Canvas );
		_tableView.MinimumHeight = 200;

		var structType = TypeLibrary.GetType( _dataTable.StructType );

		var rowNameCol = _tableView.AddColumn();
		rowNameCol.Name = "RowName";
		rowNameCol.Value = o =>
		{
			return structType.GetProperty( "RowName" ).GetValue( o )?.ToString() ?? "";
		};

		int i = 0;
		foreach ( var property in structType.Properties.Where( x => x.IsPublic && !x.IsStatic ) )
		{
			if ( property.Name == "RowName" )
				continue;

			int idx = i;
			var col = _tableView.AddColumn();
			col.Name = property.Name;
			col.Value = o =>
			{
				return property.GetValue( o )?.ToString() ?? "";
			};
			i++;
		}

		_tableView.SetItems( _internalEntries.ToList() );
		_tableView.FillHeader();

		Splitter splitter = new(this);
		splitter.IsVertical = true;

		var sheetCanvas = new Widget();
		sheetCanvas.Layout = Layout.Column();
		sheetCanvas.MinimumHeight = 300;

		var sheet = new ControlSheet();
		sheetCanvas.Layout.Add( sheet );
		sheetCanvas.Layout.AddStretchCell();
		sheetCanvas.OnPaintOverride = () =>
		{
			Paint.ClearPen();
			Paint.SetBrush( Theme.WidgetBackground );
			Paint.DrawRect( sheetCanvas.LocalRect );

			return false;
		};

		_tableView.ItemClicked = o =>
		{
			sheet.Clear( true );
			sheet.AddObject( o.GetSerialized() );
		};

		splitter.AddWidget( _tableView );
		splitter.AddWidget( sheetCanvas );
		splitter.SetCollapsible( 0, false );
		splitter.SetCollapsible( 1, false );

		layout.Add( splitter );

		Show();
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
		_toolBar.AddOption( "Delete", "common/remove.png" ).StatusTip = "Delete the currently selected entry";

		var stretch = new Widget();
		stretch.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
		_toolBar.AddWidget( stretch );

		var dropdown = new Dropdown( _dataTable.StructType );
		dropdown.FixedWidth = 200;
		dropdown.Icon = "account_tree";
		_toolBar.AddWidget( dropdown );
	}

	private void AddEntry()
	{
		var o = TypeLibrary.Create<RowStruct>( _dataTable.StructType );
		_internalEntries.Add( o );
		_tableView.AddItem( o );
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
