using System;
using System.Collections.Generic;
using System.Linq;
using DataTables;
using Editor;
using Sandbox;
using Json = Sandbox.Json;

namespace DataTablesEditor;

[CustomEditor( typeof(List<>), WithAllAttributes = new[] { typeof(InstancedAttribute) } )]
[CustomEditor( typeof(System.Array), WithAllAttributes = new[] { typeof(InstancedAttribute) } )]
public class InstancedListControlWidget : ControlWidget
{
	public override bool SupportsMultiEdit => false;

	internal SerializedCollection Collection;

	Layout Content;

	IconButton addButton;
	bool preventRebuild = false;

	public Type ListType;

	public InstancedListControlWidget( SerializedProperty property )
		: this( property, GetCollection( property ) )
	{
	}

	private static SerializedCollection GetCollection( SerializedProperty property )
	{
		if ( !property.TryGetAsObject( out var so ) || so is not SerializedCollection sc )
			return null;

		return sc;
	}

	public InstancedListControlWidget( SerializedProperty property, SerializedCollection sc )
		: base( property )
	{
		Layout = Layout.Column();
		Layout.Spacing = 2;

		if ( sc is null ) return;

		ListType = property.PropertyType.GetGenericArguments().First();

		Collection = sc;
		Collection.OnEntryAdded = Rebuild;
		Collection.OnEntryRemoved = Rebuild;

		Content = Layout.Column();

		Layout.Add( Content );

		Rebuild();
	}

	public void Rebuild()
	{
		if ( preventRebuild ) return;

		using var _ = SuspendUpdates.For( this );

		Content.Clear( true );
		Content.Margin = 0;

		var column = Layout.Column();

		int index = 0;
		foreach ( var entry in Collection )
		{
			column.Add( new ListEntryWidget( this, entry, index ) );
			index++;
		}

		// bottom row
		if ( !IsControlDisabled )
		{
			var buttonRow = Layout.Row();
			buttonRow.Margin = new Sandbox.UI.Margin( ControlRowHeight + 2, 0, 0, 0 );
			addButton = new IconButton( "add" )
			{
				Background = Theme.ControlBackground,
				ToolTip = "Add Element",
				FixedWidth = ControlRowHeight,
				FixedHeight = ControlRowHeight
			};
			addButton.MouseClick = AddEntry;
			buttonRow.Add( addButton );
			buttonRow.AddStretchCell( 1 );
			column.Add( buttonRow );
		}

		Content.Add( column );
	}

	void AddEntry()
	{
		Collection.Add( null );
	}

	void RemoveEntry( int index )
	{
		Collection.RemoveAt( index );
	}

	void DuplicateEntry( int index )
	{
		var sourceProperty = Collection.Skip( index ).First();
		var sourceObj = sourceProperty.GetValue<object>();
		var sourceJson = Json.ToNode( sourceObj );

		Collection.Add( index + 1, Json.FromNode( sourceJson, sourceProperty.PropertyType ) );
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = true;

		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlText.Darken( 0.6f ) );
	}

	// individual list entry
	class ListEntryWidget : Widget
	{
		Drag dragData;
		bool draggingAbove = false;
		bool draggingBelow = false;

		InstancedListControlWidget ListWidget;
		int Index = -1;

		public ControlSheet Sheet;

		public ListEntryWidget( InstancedListControlWidget parent, SerializedProperty property, int index ) : base( parent )
		{
			ListWidget = parent;
			Index = index;
			Layout = Layout.Row();
			Layout.Margin = new Sandbox.UI.Margin( 0, 2 );
			Layout.Spacing = 2;
			ReadOnly = parent.ReadOnly;
			Enabled = parent.Enabled;

			ToolTip = $"Element {Index}";

			var control = Create( property );
			control.ReadOnly = ReadOnly;
			control.Enabled = Enabled;

			if ( control.IsControlDisabled )
			{
				Layout.Add( control );
			}
			else
			{
				IsDraggable = !control.IsControlDisabled;
				var dragHandle = new DragHandle( this )
				{
					IconSize = 13,
					Foreground = Theme.ControlText,
					Background = Color.Transparent,
					FixedWidth = ControlRowHeight,
					FixedHeight = ControlRowHeight
				};
				var removeButton = new IconButton( "clear", () => parent.RemoveEntry( index ) )
				{
					ToolTip = "Remove",
					Background = Theme.ControlBackground,
					FixedWidth = ControlRowHeight,
					FixedHeight = ControlRowHeight
				};

				Layout.Add( dragHandle );
				//Layout.Add( control );
				var col = new Widget();
				col.Layout = Layout.Column();

				var dropdown = new Dropdown( "Select a class..." );

				dropdown.PopulatePopup = widget =>
				{
					var structTypes = TypeLibrary.GetTypes().Where( x => x.TargetType == ListWidget.ListType || x.TargetType.IsSubclassOf( ListWidget.ListType ) );

					foreach ( var structType in structTypes )
					{
						var btn = new DropdownButton( dropdown, structType.Name );
						btn.Value = structType.TargetType;
						btn.Icon = "account_tree";
						btn.Clicked = () =>
						{
							var instance = Activator.CreateInstance( structType.TargetType );
							property.SetValue( instance );

							Sheet.Clear( true );
							Sheet.AddObject( instance.GetSerialized() );
						};
						widget.Layout.Add( btn );
					}
				};

				Sheet = new ControlSheet();

				var value = property.GetValue<object>();
				if ( value is not null )
				{
					dropdown.Icon = "account_tree";
					dropdown.Text = value.GetType().Name;
					dropdown.Value = value.GetType();

					Sheet.AddObject( value.GetSerialized() );
				}
				else
				{
					dropdown.Icon = "error";
				}

				col.Layout.Add( dropdown );
				col.Layout.Add( Sheet );
				Layout.Add( col );
				Layout.Add( removeButton );

				dragHandle.MouseRightClick += () =>
				{
					var menu = new ContextMenu( this );

					menu.AddOption( "Remove", "clear", () => parent.RemoveEntry( index ) );
					menu.AddOption( "Duplicate", "content_copy", () => parent.DuplicateEntry( index ) );

					menu.OpenAtCursor();
				};
			}

			AcceptDrops = true;
		}

		protected override void OnPaint()
		{
			base.OnPaint();

			if ( draggingAbove )
			{
				Paint.SetPen( Theme.Selection, 2f, PenStyle.Solid );
				Paint.DrawLine( LocalRect.TopLeft, LocalRect.TopRight );
				draggingAbove = false;
			}
			else if ( draggingBelow )
			{
				Paint.SetPen( Theme.Selection, 2f, PenStyle.Solid );
				Paint.DrawLine( LocalRect.BottomLeft, LocalRect.BottomRight );
				draggingBelow = false;
			}
		}

		public override void OnDragHover( DragEvent ev )
		{
			base.OnDragHover( ev );

			if ( !TryDragOperation( ev, out var dragDelta ) )
			{
				draggingAbove = false;
				draggingBelow = false;
				return;
			}

			draggingAbove = dragDelta > 0;
			draggingBelow = dragDelta < 0;
		}

		public override void OnDragDrop( DragEvent ev )
		{
			base.OnDragDrop( ev );

			if ( !TryDragOperation( ev, out var delta ) ) return;

			ListWidget.preventRebuild = true;

			List<object> list = new();
			var movingIndex = Index + delta;
			foreach ( var item in ListWidget.Collection )
			{
				list.Add( item.GetValue<object>() );
			}

			var prop = list.ElementAt( movingIndex );
			list.RemoveAt( movingIndex );
			list.Insert( Index, prop );

			while ( ListWidget.Collection.Count() > 0 )
			{
				ListWidget.Collection.RemoveAt( 0 );
			}

			foreach ( var item in list )
			{
				ListWidget.Collection.Add( item );
			}

			ListWidget.preventRebuild = false;
			ListWidget.Rebuild();
		}

		bool TryDragOperation( DragEvent ev, out int delta )
		{
			delta = 0;
			var obj = ev.Data.OfType<SerializedProperty>().FirstOrDefault();

			if ( obj == null ) return false;

			var otherIndex = ListWidget.Collection.ToList().IndexOf( obj );

			if ( Index == -1 || otherIndex == -1 )
			{
				return false;
			}

			delta = otherIndex - Index;
			return true;
		}

		class DragHandle : IconButton
		{
			ListEntryWidget Entry;

			public DragHandle( ListEntryWidget entry ) : base( "drag_handle" )
			{
				Entry = entry;

				IsDraggable = Entry.IsDraggable;
			}

			protected override void OnDragStart()
			{
				base.OnDragStart();

				Entry.dragData = new Drag( this );
				Entry.dragData.Data.Object = Entry.ListWidget.Collection.ElementAt( Entry.Index );
				Entry.dragData.Execute();
			}
		}
	}
}
