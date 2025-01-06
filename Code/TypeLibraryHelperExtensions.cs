using System;
using System.Collections;
using System.Linq;
using System.Text.Json;
using Sandbox;
using Sandbox.Internal;

namespace DataTables;

internal static class TypeLibraryHelperExtensions
{
	private static PropertyDescription _currentProperty;

	public static T Clone<T>( this TypeLibrary typeLibrary, object target )
	{
		var targetType = typeof(T);
		TypeDescription type = typeLibrary.GetType( targetType );
		if ( type.IsValueType )
			return (T)target;

		if ( targetType.IsAssignableTo( typeof(IList) ) )
		{
			return (T)CloneList( typeLibrary, target );
		}

		return CloneObject<T>( typeLibrary, target );
	}

	public static IList CloneList( this TypeLibrary typeLibrary, object target )
	{
		IList list = null;

		var type = typeLibrary.GetType( _currentProperty.PropertyType );
		if ( type.TargetType.IsAssignableTo( typeof(IList) ) )
		{
			list = (IList)typeLibrary.Create<object>( _currentProperty.PropertyType );
		}

		if ( list is null )
			return list;

		var enumerator = (target as IList).GetEnumerator();
		while ( enumerator.MoveNext() )
		{
			var value = enumerator.Current;
			if ( value is null )
				continue;

			switch ( value.GetObjectType() )
			{
				case ObjectType.Object:
					var previousProperty = _currentProperty;
					list.Add( Clone<object>( typeLibrary, value ) );
					_currentProperty = previousProperty;
					break;
				case ObjectType.String:
					list.Add( (string)value );
					break;
				case ObjectType.Boolean:
					list.Add( (bool)value );
					break;
				case ObjectType.Number:
					if ( int.TryParse( value.ToString(), out var intValue ) )
						list.Add( intValue );
					else
						list.Add( (double)value );
					break;
			}
		}

		return list;
	}

	public static T CloneObject<T>( this TypeLibrary typeLibrary, object target )
	{
		if ( target is null )
			return default;

		var o = typeLibrary.Create<T>( target.GetType() );
		foreach ( var property in typeLibrary.GetPropertyDescriptions( o ).Where( x => x.IsPublic && !x.IsStatic ) )
		{
			var value = property.GetValue( target );
			if ( value is null )
				continue;

			_currentProperty = property;

			switch ( value.GetObjectType() )
			{
				case ObjectType.Array:
					property.SetValue( o, CloneList( typeLibrary, value ) );
					break;
				case ObjectType.Object:
					property.SetValue( o, CloneObject<T>( typeLibrary, value ) );
					break;
				case ObjectType.String:
				case ObjectType.Boolean:
				case ObjectType.Number:
					property.SetValue( o, value );
					break;
			}
		}

		return o;
	}

	public static void Merge<T>( this TypeLibrary typeLibrary, ref T target, T merger )
	{
		var targetType = typeof(T);
		TypeDescription type = typeLibrary.GetType( targetType );
		if ( type.IsValueType )
		{
			target = merger;
			return;
		}

		foreach ( var property in typeLibrary.GetPropertyDescriptions( target ).Where( x => x.IsPublic && !x.IsStatic ) )
		{
			var value = property.GetValue( target );
			if ( value is null )
				continue;

			_currentProperty = property;

			switch ( value.GetObjectType() )
			{
				case ObjectType.Array:
					//property.SetValue( target, CloneList( typeLibrary, value ) );
					break;
				case ObjectType.Object:
					//property.SetValue( o, CloneObject<T>( typeLibrary, value ) );
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
