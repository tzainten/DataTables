using Editor;

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
}
