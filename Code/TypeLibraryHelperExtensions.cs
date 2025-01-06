using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;
using Sandbox.Diagnostics;
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

	public static void Merge<T>( this TypeLibrary typeLibrary, T target, T merger ) where T : class
	{
		var targetType = target.GetType();
		TypeDescription type = typeLibrary.GetType( targetType );

		foreach ( var field in type.Fields.Where( x => x.IsPublic && !x.IsStatic ) )
		{
			MergeField( typeLibrary, field, target, merger );
		}

		foreach ( var property in type.Properties.Where( x => x.IsPublic && !x.IsStatic ) )
		{
			MergeProperty( typeLibrary, property, target, merger );
		}
	}

	private static void MergeField( TypeLibrary typeLibrary, FieldDescription field, object target, object merger )
	{
		Assert.True( target is not null );

		var mergerType = typeLibrary.GetType( merger.GetType() );
		if ( mergerType.IsGenericType )
			return;

		var value = field.GetValue( merger );
		if ( value is null )
		{
			field.SetValue( target, null );
			return;
		}

		var targetValue = field.GetValue( target );
		if ( targetValue is null )
		{
			field.SetValue( target, typeLibrary.CloneInternal( value ) );
			return;
		}

		var fieldType = typeLibrary.GetType( value.GetType() ); // @TODO: field.TypeDescription.IsValueType doesn't work here. Not sure if it should?

		if ( fieldType.IsValueType || fieldType.TargetType.IsAssignableTo( typeof(string) ) )
		{
			field.SetValue( target, field.GetValue( merger ) );
			return;
		}

		if ( fieldType.TargetType.IsAssignableTo( typeof(IList) ) )
		{
			IList targetList = (IList)targetValue;
			IList mergerList = (IList)value;

			MergeList( typeLibrary, targetList, mergerList );
			return;
		}

		if ( fieldType.TargetType.IsAssignableTo( typeof(IDictionary) ) )
		{
			IDictionary targetDict = (IDictionary)targetValue;
			IDictionary mergerDict = (IDictionary)value;

			MergeDictionary( typeLibrary, targetDict, mergerDict );
			return;
		}

		foreach ( var innerField in fieldType.Fields.Where( x => x.IsPublic && !x.IsStatic ) )
		{
			MergeField( typeLibrary, innerField, field.GetValue( target ), value );
		}
	}

	private static void MergeDictionary( TypeLibrary typeLibrary, IDictionary targetDict, IDictionary mergerDict )
	{
		if ( mergerDict.Count == 0 )
			targetDict.Clear();
		else
		{
			foreach ( var key in mergerDict.Keys )
			{
				var value = mergerDict[key];

				if ( !targetDict.Contains( key ) )
				{
					if ( value is not null )
						targetDict.Add( key, typeLibrary.CloneInternal( value ) );
				}
				else
				{
					var elemTargetType = value.GetType();
					var elemType = typeLibrary.GetType( elemTargetType );
					if ( elemType.IsValueType )
					{
						targetDict[key] = value;
					}
					else
					{
						typeLibrary.Merge( targetDict[key], value );
					}
				}
			}

			List<object> nullKeys = new();
			foreach ( var key in targetDict.Keys )
			{
				if ( !mergerDict.Contains( key ) )
					nullKeys.Add( key );
			}

			foreach ( var key in nullKeys )
			{
				targetDict.Remove( key );
			}
		}
	}

	private static void MergeList( this TypeLibrary typeLibrary, IList targetList, IList mergerList )
	{
		if ( mergerList.Count == 0 )
			targetList.Clear();
		else
		{
			int i;
			for ( i = 0; i < mergerList.Count; i++ )
			{
				if ( i > targetList.Count - 1 )
				{
					targetList.Add( typeLibrary.CloneInternal( mergerList[i] ) );
				}
				else
				{
					var elemTargetType = targetList[i].GetType();
					var elemType = typeLibrary.GetType( elemTargetType );
					if ( elemType.IsValueType )
					{
						targetList[i] = mergerList[i];
					}
					else
					{
						Merge( typeLibrary, targetList[i], mergerList[i] );
					}
				}
			}

			for ( int j = targetList.Count - 1; j >= i; j-- )
			{
				targetList.RemoveAt( j );
			}
		}
	}

	private static void MergeProperty( this TypeLibrary typeLibrary, PropertyDescription property, object target, object merger )
	{
		Assert.True( target is not null );

		var mergerType = typeLibrary.GetType( merger.GetType() );
		if ( mergerType.IsGenericType )
			return;

		var value = property.GetValue( merger );
		if ( value is null )
		{
			property.SetValue( target, null );
			return;
		}

		var targetValue = property.GetValue( target );
		if ( targetValue is null )
		{
			property.SetValue( target, typeLibrary.CloneInternal( value ) );
			return;
		}

		var propertyType = typeLibrary.GetType( value.GetType() );

		if ( propertyType.IsValueType || propertyType.TargetType.IsAssignableTo( typeof(string) ) )
		{
			property.SetValue( target, property.GetValue( merger ) );
			return;
		}

		foreach ( var innerProperty in propertyType.Properties.Where( x => x.IsPublic && !x.IsStatic ) )
		{
			MergeProperty( typeLibrary, innerProperty, property.GetValue( target ), value );
		}
	}

	public static PropertyDescription _currentProperty = null;
	public static void __Old_Merge<T>( this TypeLibrary typeLibrary, ref T target, T merger )
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
