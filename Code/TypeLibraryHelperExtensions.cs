using System;
using System.Collections;
using System.Linq;
using System.Text.Json;
using Sandbox;
using Sandbox.Internal;

namespace DataTables;

internal static class TypeLibraryHelperExtensions
{
	public static T Clone<T>( this TypeLibrary typeLibrary, T target )
	{
		return (T)CloneInternal( typeLibrary, target );
	}

	public static object CloneInternal( this TypeLibrary typeLibrary, object target )
	{
		var targetType = target.GetType();
		TypeDescription type = typeLibrary.GetType( targetType );

		if ( type.IsValueType )
			return target;

		if ( targetType.IsAssignableTo( typeof(IList) ) )
		{
			IList listTarget = (IList)target;
			IList result = (IList)typeLibrary.Create<object>( targetType );

			foreach ( var elem in listTarget )
			{
				result.Add( CloneInternal( typeLibrary, elem ) );
			}

			return result;
		}

		if ( targetType.IsAssignableTo( typeof(IDictionary) ) )
		{
			IDictionary dictTarget = (IDictionary)target;
			IDictionary result = (IDictionary)typeLibrary.Create<object>( targetType );

			foreach ( var key in dictTarget.Keys )
			{
				if ( key is null )
					continue;

				var value = dictTarget[key];
				if ( value is null )
					continue;

				result.Add( key, CloneInternal( typeLibrary, value ) );
			}

			return result;
		}

		return CloneObject( typeLibrary, target );
	}

	public static object CloneObject( this TypeLibrary typeLibrary, object target )
	{
		var targetType = target.GetType();
		TypeDescription type = typeLibrary.GetType( targetType );

		object instance = null;
		if ( targetType.IsAssignableTo( typeof(string) ) )
			instance = new String( (string)target );
		else
		{
			instance = typeLibrary.Create<object>( targetType );
			foreach ( var field in type.Fields.Where( x => x.IsPublic && !x.IsStatic ) )
			{
				var value = field.GetValue( target );
				if ( value is null )
					continue;

				field.SetValue( instance, CloneInternal( typeLibrary, value ) );
			}

			foreach ( var property in type.Properties.Where( x => x.IsPublic && !x.IsStatic ) )
			{
				var value = property.GetValue( target );
				if ( value is null )
					continue;

				property.SetValue( instance, CloneInternal( typeLibrary, value ) );
			}
		}

		return instance;
	}

	public static PropertyDescription _currentProperty = null;
	public static void Merge<T>( this TypeLibrary typeLibrary, ref T target, T merger )
	{
		var targetType = typeof(T);
		TypeDescription type = typeLibrary.GetType( targetType );
		if ( type.IsValueType )
		{
			target = merger;
			return;
		}

		foreach ( var property in typeLibrary.GetPropertyDescriptions( merger ).Where( x => x.IsPublic && !x.IsStatic ) )
		{
			var value = property.GetValue( merger );
			if ( value is null )
				continue;

			_currentProperty = property;

			switch ( value.GetObjectType() )
			{
				case ObjectType.Array:
					//property.SetValue( target, CloneList( typeLibrary, value ) );
					break;
				case ObjectType.Object:
					property.SetValue( target, CloneInternal( typeLibrary, property.GetValue( merger ) ) );
					break;
				case ObjectType.String:
				case ObjectType.Boolean:
				case ObjectType.Number:
					property.SetValue( target, property.GetValue( merger ) );
					break;
			}
		}
	}
}
