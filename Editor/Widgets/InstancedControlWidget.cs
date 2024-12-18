using System;
using System.Linq;
using DataTables;
using Editor;
using Sandbox;

namespace DataTablesEditor;

[CustomEditor( typeof(object), WithAllAttributes = new[] { typeof(InstancedAttribute) } )]
public class InstancedControlWidget : ControlWidget
{
	public InstancedControlWidget( SerializedProperty property ) : base( property )
	{
		Dropdown dropdown = new(property.PropertyType.Name);
		ControlSheet sheet = new();

		dropdown.PopulatePopup = widget =>
		{
			var type = property.PropertyType;

			var structTypes = TypeLibrary.GetTypes().Where( x => x.TargetType == type || x.TargetType.IsSubclassOf( type ) );
			foreach ( var structType in structTypes )
			{
				var btn = new DropdownButton( dropdown, structType.Name );
				btn.Value = structType.TargetType;
				btn.Icon = "account_tree";
				btn.Clicked = () =>
				{
					var instance = Activator.CreateInstance( structType.TargetType );
					property.SetValue( instance );

					sheet.Clear( true );
					sheet.AddObject( instance.GetSerialized() );
				};
				widget.Layout.Add( btn );
			}
		};

		var value = property.GetValue<object>();
		Log.Info( value );

		if ( value is not null )
		{
			dropdown.Text = value.GetType().Name;
			sheet.Clear( true );
			sheet.AddObject( value.GetSerialized() );
		}

		Layout = Layout.Column();
		Layout.Add( dropdown );
		Layout.Add( sheet );
	}

	protected override void PaintUnder()
	{
	}
}
