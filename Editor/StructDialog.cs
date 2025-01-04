using Editor;
using Sandbox;

namespace DataTablesEditor;

public class StructDialog : BaseWindow
{
	public StructDialog(string title)
	{
		DeleteOnClose = true;

		Size = new Vector2( 400, 100 );
		WindowTitle = title;
		SetWindowIcon( "equalizer" );

		EditorUtility.PlayRawSound( "sounds/editor/fail.wav" );

		Layout = Layout.Column();
		Layout.Margin = 8;
		Layout.Spacing = 8;

		var icon = new IconButton( "\u26a0\ufe0f" );
		icon.IconSize = 48f;
		icon.FixedHeight = 64f;
		icon.FixedWidth = 64f;
		icon.Background = Color.Transparent;
		icon.TransparentForMouseEvents = true;

		var row = Layout.AddRow();

		var lbl = new Label( "This RowStruct type contains generic properties that are not supported.\nSorry! This is lame but will hopefully be fixed in the future!" );

		row.Add( icon );
		row.Add( lbl );
		row.AddStretchCell();

		row = Layout.AddRow();
		row.AddStretchCell();

		var confirmBtn = new Button.Primary( "Okay", icon: "done" );
		confirmBtn.Clicked = () =>
		{
			Close();
		};

		row.Spacing = 8;
		row.Add( confirmBtn );
		Layout.AddStretchCell();

		Show();
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
