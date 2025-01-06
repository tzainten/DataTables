using System;
using Editor;
using Sandbox;

namespace DataTablesEditor;

public class CloseDialog : BaseWindow
{
	public CloseDialog(string title, Action onConfirm, Action onCancel)
	{
		DeleteOnClose = true;

		Size = new Vector2( 400, 100 );
		WindowTitle = title;
		SetWindowIcon( "equalizer" );

		Layout = Layout.Column();
		Layout.Margin = 8;
		Layout.Spacing = 8;

		var icon = new IconButton( "\uD83D\uDCBE" );
		icon.IconSize = 48f;
		icon.FixedHeight = 64f;
		icon.FixedWidth = 64f;
		icon.Background = Color.Transparent;
		icon.TransparentForMouseEvents = true;

		var row = Layout.AddRow();

		var lbl = new Label( "You have unsaved changes!\nWould you like to save them now?" );

		row.Add( icon );
		row.Add( lbl );
		row.AddStretchCell();

		row = Layout.AddRow();
		row.AddStretchCell();

		var confirmBtn = new Button.Primary( "Save", icon: "save" );
		confirmBtn.Clicked = () =>
		{
			onConfirm();
			Close();
		};
		var cancelBtn = new Button.Danger( "Don't Save", icon: "block" );
		cancelBtn.Clicked = () =>
		{
			onCancel();
			Close();
		};

		row.Spacing = 8;
		row.Add( confirmBtn );
		row.Add( cancelBtn );
		Layout.AddStretchCell();

		SetModal( true, true );
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
