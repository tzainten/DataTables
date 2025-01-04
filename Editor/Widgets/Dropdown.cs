using System;
using Editor;
using Sandbox;

namespace DataTablesEditor;

public class Dropdown : Widget
{
	public Action<Widget> PopulatePopup;
	public Action OnValueChanged;

	private Label _label;

	private IconButton _labelIcon;
	private IconButton _arrowIcon;

	public object Value;

	private string _icon;
	public string Icon
	{
		set
		{
			_icon = value;
			Rebuild();
		}
		get
		{
			return _icon;
		}
	}

	private string _text;
	public string Text
	{
		set
		{
			_text = value;
			Rebuild();
		}
		get
		{
			return _text;
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

		_text = title;
		Rebuild();
	}

	public void Rebuild()
	{
		Layout.Clear( true );

		_labelIcon = new IconButton( _icon );
		_labelIcon.Background = Color.Transparent;
		_labelIcon.Foreground = Theme.ControlText;
		_labelIcon.IconSize = 15;

		_label = new Label( _text ?? "" );

		_arrowIcon = new IconButton( "Arrow_Drop_Down" );
		_arrowIcon.Background = Color.Transparent;
		_arrowIcon.Foreground = Theme.ControlText;
		_arrowIcon.IconSize = 18;

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

	public IconButton IconButton;

	public Label Label;

	public string Icon
	{
		set
		{
			if ( IconButton.IsValid() )
				IconButton.Icon = value;
		}
		get
		{
			return IconButton?.Icon;
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

		IconButton = new IconButton( Icon );
		IconButton.Background = Color.Transparent;
		IconButton.Foreground = Theme.ControlText;
		IconButton.IconSize = 20;
		IconButton.OnPaintOverride = () =>
		{
			Paint.Antialiasing = true;
			Paint.SetPen( IconButton.Foreground.WithAlpha( 0.7f ), 3.0f );
			Paint.ClearBrush();

			Paint.DrawIcon( IconButton.LocalRect, Icon, 24 );
			Log.Info( Icon );

			return true;
		};

		Layout.Add( IconButton );

		Text = title;

		Label = new Label( title );
		Layout.Add( Label );
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
		Dropdown.Rebuild();

		if ( Dropdown.OnValueChanged is not null )
			Dropdown.OnValueChanged();
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		Rect rect = LocalRect;
		rect = rect.Shrink(2f);

		Paint.SetBrushAndPen( Theme.ControlBackground );
		Paint.DrawRect( LocalRect );

		if ( !Enabled )
		{
			Paint.SetBrushAndPen( Theme.Red.WithAlpha( 0.1f ) );
			Paint.DrawRect( LocalRect );
		}

		if ( Paint.HasMouseOver )
		{
			Paint.SetBrushAndPen( Enabled ? Theme.WidgetBackground : Theme.Red.WithAlpha( 0.2f ) );
			Paint.DrawRect( rect );
		}

		if ( Value == Dropdown.Value )
		{
			Paint.SetBrushAndPen( Theme.Blue.WithAlpha( 0.3f ) );
			Paint.DrawRect( rect );
		}
	}
}
