using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Editor;
using Sandbox;
using Sandbox.UI;
using Sandbox.VR;
using Label = Editor.Label;

namespace DataTablesEditor.Widgets;

/// <summary>
/// I am absolutely not proud of this at all.
/// </summary>
internal class TableView : Widget
{
	public class Column
	{
		private string _name;
		public string Name
		{
			get
			{
				return _name;
			}
			set
			{
				if ( value is null )
					_name = null;
				else
				{
					StringBuilder result = new StringBuilder();
					result.Append( value[0] );

					for (int i = 1; i < value.Length; i++)
					{
						if ( char.IsUpper( value[i] ) && !char.IsUpper( value[i - 1] ) )
							result.Append( ' ' );

						result.Append( value[i] );
					}

					_name = result.ToString();
				}
			}
		}
		public int Width;
		public Func<object, string> Value;
		public TextFlag TextFlag = TextFlag.Left;
		public Label Label;
	}

	public ListView ListView { get; set; }

	public List<Column> Columns { get; set; } = new();

	TableHeader Header;

	public Action<object> ItemClicked;

	public Dictionary<VirtualWidget, int> WidgetIndexMap = new();

	public TableView( Widget parent ) : base( parent )
	{
		ListView = new( this );

		ListView.ItemPaint = widget => PaintRow( widget );
		ListView.ItemSize = new Vector2( 0, 24 );
		ListView.ItemSpacing = 0;
		ListView.ItemClicked = o =>
		{
			if ( ItemClicked is not null )
				ItemClicked( o );
		};
		ListView.Margin = new Margin( 0, 4, 0, 0 );

		Layout = Layout.Column();
		Layout.Add( Header = new TableHeader( this ) );
		Layout.Add( ListView );
	}

	public Column AddColumn( string name = null, int? width = null, Func<object, string> value = null )
	{
		var col = new Column();
		col.Name = name;
		col.Width = width ?? 50;
		col.Value = value;

		Columns.Add( col );

		return col;
	}

	protected override void DoLayout()
	{
		base.DoLayout();

		int i = 0;
		foreach ( var lbl in Header.Labels )
		{
			Columns[i].Width = (int)lbl.Width;
			i++;
		}
	}

	public void FillHeader()
	{
		foreach ( var column in Columns )
		{
			Header.AddColumn( column );
		}
	}

	public void SetItems<T>( IEnumerable<T> items )
	{
		foreach ( var elem in items.ToList().Cast<object>() )
		{
			ListView.AddItem( elem );
		}
	}

	public void AddItem( object item )
	{
		ListView.AddItem( item );
	}

	private void PaintRow( VirtualWidget widget )
	{
		/*if ( widget.Object is not T t )
			return;*/

		var isAlt = widget.Row % 2 == 0;
		var backgroundColor = widget.Selected ? Theme.Selection : ( widget.Hovered ? Theme.ButtonDefault.WithAlpha( 0.7f ) : Theme.ButtonDefault.WithAlpha( isAlt ? 0.3f : 0.2f ) );

		Paint.ClearPen();
		Paint.SetBrush( backgroundColor );
		Paint.DrawRect( widget.Rect );

		Rect rect = widget.Rect;

		Paint.SetDefaultFont();
		Paint.SetPen( widget.Selected ? Color.Orange : Theme.ControlText );

		foreach ( var column in Columns )
		{
			var width = column.Width + 4;
			rect.Width = width;
			Paint.DrawText( rect.Shrink( 8, 0 ), column.Value(widget.Object), column.TextFlag | TextFlag.CenterVertically | TextFlag.SingleLine );
			rect.Left += width;
		}
	}

	class TableHeader : Widget
	{
		readonly TableView Table;

		public List<Label> Labels = new();

		public HeaderSplitter _splitter;

		public static TableHeader Header = null;

		[EditorEvent.Frame]
		public static void Frame()
		{
			if ( Header is null || !Header.IsValid )
				return;

			var list = Header._splitter.Labels;
			for ( int i = 0; i < list.Count; i++ )
			{
				var lbl = list[i];
				Header.Table.Columns[i].Width = (int)lbl.Width;

				var _ = new Widget();
				Header.Table.ListView.AddItem( _ );
				Header.Table.ListView.RemoveItem( _ );
			}
		}

		public TableHeader( TableView parent ) : base( parent )
		{
			Table = parent;
			MinimumHeight = 25;

			Layout = Layout.Row();

			_splitter = new(this);
			_splitter.IsHorizontal = true;

			Header = this;

			Layout.Add( _splitter );
		}

		public override void Close()
		{
			base.Close();

			Header = null;
		}

		public void AddColumn( Column column )
		{
			Labels.Add( _splitter.AddColumn( column ) );
		}
	}

	public class HeaderSplitter : Splitter
	{
		public List<Label> Labels = new();

		public HeaderSplitter(Widget parent) : base(parent)
		{
		}

		private int splitterCount = 0;
		public Label AddColumn( Column column )
		{
			var lbl = new Label( column.Name );
			lbl.ContentMargins = new Margin( 8, 0, 0, 0 );
			lbl.OnPaintOverride = () =>
			{
				Paint.SetBrush( ControlWidget.ControlColor );
				Paint.SetPen( Color.Transparent );
				Paint.DrawRect( lbl.LocalRect );

				return false;
			};
			column.Label = lbl;
			Labels.Add( lbl );

			AddWidget( lbl );
			SetCollapsible( splitterCount++, false );

			return lbl;
		}
	}
}