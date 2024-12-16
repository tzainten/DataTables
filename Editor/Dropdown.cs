using System;
using Editor;
using Sandbox;

namespace DataTablesEditor;

public class Dropdown : Widget
{
	public Action Clicked;

	private Label _label;

	private IconButton _labelIcon;
	private IconButton _arrowIcon;

	public object Value;

	public string Icon
	{
		set
		{
			if ( _labelIcon.IsValid() )
			{
				_labelIcon.Icon = value;
				Rebuild();
			}
		}
		get
		{
			return _labelIcon?.Icon;
		}
	}

	public string Text
	{
		set
		{
			if ( _label.IsValid() )
				_label.Text = value;
		}
		get
		{
			return _label?.Text;
		}
	}

	public Dropdown( string title = null )
	{
		Cursor = CursorShape.Finger;

		Layout = Layout.Row();
		Layout.Alignment = TextFlag.LeftCenter;
		Layout.Spacing = 4;
		Layout.Margin = 4;

		_labelIcon = new IconButton( "" );
		_labelIcon.Background = Color.Transparent;
		_labelIcon.Foreground = Theme.ControlText;
		_labelIcon.IconSize = 15;

		_label = new Label( title ?? "" );

		_arrowIcon = new IconButton( "Arrow_Drop_Down" );
		_arrowIcon.Background = Color.Transparent;
		_arrowIcon.Foreground = Theme.ControlText;
		_arrowIcon.IconSize = 18;

		Rebuild();
	}

	private void Rebuild()
	{
		Layout.Clear( false );

		if ( _labelIcon.Icon != "" )
			Layout.Add( _labelIcon );

		Layout.Add( _label );
		Layout.AddStretchCell();
		Layout.Add( _arrowIcon );
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		base.OnMouseClick( e );

		if ( Clicked is not null )
			Clicked();
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		Paint.SetBrushAndPen( Theme.ButtonDefault.WithAlpha( 0.2f ) );
		Paint.DrawRect( LocalRect );

		if ( Paint.HasMouseOver )
		{
			Paint.SetBrushAndPen( Theme.WidgetBackground );
			Paint.DrawRect( LocalRect );
		}
	}
}
