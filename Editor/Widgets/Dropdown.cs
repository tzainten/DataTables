using System;
using Editor;
using Sandbox;

namespace DataTablesEditor;

public class Dropdown : Widget
{
	public Action<Widget> PopulatePopup;

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

	private PopupWidget _popupMenu;

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

	public void Rebuild()
	{
		Layout.Clear( false );

		if ( _labelIcon.Icon != "" )
			Layout.Add( _labelIcon );

		Layout.Add( _label );
		Layout.AddStretchCell();
		Layout.Add( _arrowIcon );
	}

	public void ClosePopup()
	{
		_popupMenu.Close();
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		base.OnMouseClick( e );

		_popupMenu = new PopupWidget( null );
		_popupMenu.Layout = Layout.Column();
		_popupMenu.MinimumWidth = ScreenRect.Width;

		ScrollArea scroll = new ScrollArea( _popupMenu );
		_popupMenu.Layout.Add( scroll, 1 );

		scroll.Canvas = new Widget( scroll )
		{
			Layout = Layout.Column(),
			VerticalSizeMode = SizeMode.CanGrow | SizeMode.Expand
		};

		if ( PopulatePopup is not null )
			PopulatePopup( scroll.Canvas );

		_popupMenu.Position = ScreenRect.BottomLeft;
		_popupMenu.Visible = true;

		_popupMenu.AdjustSize();
		_popupMenu.ConstrainToScreen();
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		Paint.ClearPen();
		if ( Paint.HasMouseOver )
		{
			Paint.SetPen( Color.Lerp( ControlWidget.ControlColor, ControlWidget.ControlHighlightPrimary, 0.6f ),
				1f );
			Paint.SetBrush(
				Color.Lerp( ControlWidget.ControlColor, ControlWidget.ControlHighlightPrimary, 0.2f ) );
			Paint.DrawRect( LocalRect.Shrink( 1f ), ControlWidget.ControlRadius );
		}
		else
		{
			Paint.SetBrush( ControlWidget.ControlColor );
			Paint.DrawRect( LocalRect, ControlWidget.ControlRadius );
		}
	}
}

public class DropdownButton : Widget
{
	public Action Clicked;

	public Dropdown Dropdown;

	public object Value;

	public string Text;

	private IconButton _iconButton;

	public string Icon
	{
		set
		{
			if ( _iconButton.IsValid() )
				_iconButton.Icon = value;
		}
		get
		{
			return _iconButton?.Icon;
		}
	}

	public DropdownButton( Dropdown dropdown, string title = null, string icon = null )
	{
		Dropdown = dropdown;
		Cursor = CursorShape.Finger;

		Layout = Layout.Row();
		Layout.Alignment = TextFlag.LeftCenter;
		Layout.Margin = 8;
		Layout.Spacing = 8;

		Icon = icon ?? "layers";

		_iconButton = new IconButton( Icon );
		_iconButton.Background = Color.Transparent;
		_iconButton.Foreground = Theme.ControlText;
		_iconButton.IconSize = 20;

		Layout.Add( _iconButton );

		Text = title;
		Layout.Add( new Label( title ) );
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		base.OnMouseClick( e );

		if ( Clicked is not null )
			Clicked();

		Dropdown.Value = Value;
		Dropdown.Text = Text;
		Dropdown.Icon = Icon;
		Dropdown.ClosePopup();
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		Rect rect = LocalRect;
		rect = rect.Shrink(2f);

		Paint.SetBrushAndPen( Theme.ControlBackground );
		Paint.DrawRect( LocalRect );

		if ( Paint.HasMouseOver )
		{
			Paint.SetBrushAndPen( Theme.WidgetBackground );
			Paint.DrawRect( rect );
		}

		if ( Value == Dropdown.Value )
		{
			Paint.SetBrushAndPen( Theme.Blue.WithAlpha( 0.3f ) );
			Paint.DrawRect( rect );
		}
	}
}
