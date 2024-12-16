using DataTables;
using Editor;
using Sandbox;
using Json = DataTables.Json;

namespace DataTablesEditor;

public class DataTableEditor : DockWindow
{
	public bool CanOpenMultipleAssets => false;

	private Asset _asset;
	private DataTable _dataTable;

	public DataTableEditor( Asset asset, DataTable dataTable )
	{
		_asset = asset;
		_dataTable = dataTable;

		DeleteOnClose = true;

		Size = new Vector2( 1000, 800 );
		Title = "Data Table Editor";
		SetWindowIcon( "equalizer" );

		Show();

		JsonDummy o = new();
		o.Test = 5;
		o.Numbers = new();

		MyClass o1 = new();
		o1.Blue = 99;
		o1.List = new();

		MyClass o2 = new();
		o2.Blue = 65536;
		o1.List.Add( o2 );

		o.Numbers.Add( o1 );

		Log.Info( Json.Serialize( o ) );
	}

	[Shortcut( "editor.save", "CTRL+S", ShortcutType.Window )]
	private void Save()
	{
		_dataTable.StructEntries.Add( new TechJamRowStruct() { RowNumber = 5 } );
		_asset.SaveToDisk( _dataTable );
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
