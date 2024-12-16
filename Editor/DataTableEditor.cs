using System.Linq;
using DataTables;
using Editor;
using Sandbox;
using Sandbox.UI;
using Button = Editor.Button;
using Label = Editor.Label;

namespace DataTablesEditor;

[EditorForAssetType( "dt" )]
public class DataTableEditorLauncher : BaseWindow, IAssetEditor
{
	public bool CanOpenMultipleAssets => false;

	public DataTableEditorLauncher()
	{
	}

	protected override void OnPaint()
	{
		Paint.ClearPen();
		Paint.SetBrush( Theme.WindowBackground );
		Paint.DrawRect( LocalRect );
	}

	private void FillLayout()
	{
		DeleteOnClose = true;

		Size = new Vector2( 300, 75 );
		WindowTitle = "Select a RowStruct type";
		SetWindowIcon( "equalizer" );

		Layout = Layout.Column();
		Layout.Margin = 8;
		Layout.Spacing = 8;

		var dropdown = new Dropdown();
		dropdown.Icon = "layers_clear";
		dropdown.Text = "None";
		dropdown.PopulatePopup = widget =>
		{
			var structTypes = TypeLibrary.GetTypes().Where( x => x.TargetType.IsSubclassOf( typeof(RowStruct) ) );

			foreach ( var structType in structTypes )
			{
				var btn = new DropdownButton( dropdown, structType.Name );
				btn.Value = structType;
				widget.Layout.Add( btn );
			}
		};

		var lbl = new Label( "Please assign a RowStruct type to this Data Table:" );

		Layout.AddStretchCell();
		Layout.Add( lbl );
		Layout.Add( dropdown );

		var row = Layout.AddRow();
		row.AddStretchCell();

		var confirmBtn = new Button.Primary( "Confirm", icon: "done" );
		confirmBtn.Clicked = () =>
		{
			OpenEditor();
		};
		row.Add( confirmBtn );

		Layout.AddStretchCell();

		Show();
	}

	private void OpenEditor()
	{
		DataTableEditor editor = new();
		Close();
	}

	public void AssetOpen( Asset asset )
	{
		if ( true )
		{
			FillLayout();
		}
		else
		{
			OpenEditor();
		}
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
