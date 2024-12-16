using Editor;
using Sandbox;

namespace DataTablesEditor;

[EditorForAssetType( "dt" )]
public class DataTableEditor : DockWindow, IAssetEditor
{
	public bool CanOpenMultipleAssets => false;

	public DataTableEditor()
	{
		DeleteOnClose = true;

		Size = new Vector2( 1000, 800 );
		Title = "Data Table Editor";
		SetWindowIcon( "equalizer" );

		Show();
	}

	public void AssetOpen( Asset asset )
	{
		Raise();
	}

	public void SelectMember( string memberName )
	{
		throw new System.NotImplementedException();
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
