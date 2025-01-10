using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Internal;
using Sandbox.UI;

namespace DataTables;

internal static class TypeLibraryHelperExtensions
{
	public static List<MemberDescription> GetFieldsAndProperties( this TypeLibrary typeLibrary, TypeDescription type )
	{
		var props = type.Properties.Where( x => x.IsPublic && !x.IsStatic && x.CanRead && x.CanWrite &&
		                                        !x.HasAttribute( typeof(JsonIgnoreAttribute) ) &&
		                                        !x.HasAttribute( typeof(HideAttribute) ) );

		var fields = type.Fields.Where( x => x.IsPublic && !x.IsStatic &&
		                                     !x.HasAttribute( typeof(JsonIgnoreAttribute) ) &&
		                                     !x.HasAttribute( typeof(HideAttribute) ) );

		return props.Concat<MemberDescription>( fields ).OrderBy( x => x.Order )
			.ThenBy( x => x.SourceFile )
			.ThenBy( x => x.SourceLine )
			.ToList();
	}

	public static T Clone<T>( this TypeLibrary typeLibrary, T target )
	{
		return (T)CloneInternal( typeLibrary, target );
	}

	public static object CloneInternal( this TypeLibrary typeLibrary, object target )
	{
		if ( target is null )
			return null;

		var targetType = target.GetType();
		TypeDescription type = typeLibrary.GetType( targetType );

		if ( type.IsValueType || targetType.IsAssignableTo( typeof(Resource) ) )
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

		if ( type.IsValueType || targetType.IsAssignableTo( typeof(string) ) || targetType.IsAssignableTo( typeof(Resource) ) )
			return target;

		object instance = typeLibrary.Create<object>( targetType );

		var members = typeLibrary.GetFieldsAndProperties( type );
		foreach ( var member in members )
		{
			object value = null;
			if ( member.IsField )
			{
				FieldDescription field = (FieldDescription)member;
				value = field.GetValue( target );
				if ( value is null )
					continue;

				field.SetValue( instance, CloneInternal( typeLibrary, value ) );
				continue;
			}

			PropertyDescription property = (PropertyDescription)member;
			value = property.GetValue( target );
			if ( value is null )
				continue;

			property.SetValue( instance, CloneInternal( typeLibrary, value ) );
		}

		return instance;
	}

	public static T Merge<T>( this TypeLibrary typeLibrary, T target, T merger ) where T : class
	{
		Assert.True( target is not null );
		Assert.True( merger is not null );

		var targetType = target.GetType();
		TypeDescription type = typeLibrary.GetType( targetType );

		var mergerType = merger.GetType();
		TypeDescription mergerTypeDesc = typeLibrary.GetType( targetType );

		if ( mergerTypeDesc.IsValueType || mergerType.IsAssignableTo( typeof(string) ) ||
		     mergerType.IsAssignableTo( typeof(Resource) ) )
		{
			return merger;
		}

		var members = typeLibrary.GetFieldsAndProperties( type );
		foreach ( var member in members )
		{
			if ( member.IsField )
			{
				MergeField( typeLibrary, (FieldDescription)member, target, merger );
				continue;
			}

			MergeProperty( typeLibrary, (PropertyDescription)member, target, merger );
		}

		return target;
	}

	private static void MergeField( TypeLibrary typeLibrary, FieldDescription field, object target, object merger )
	{
		Assert.True( target is not null && merger is not null );

		var hasIgnore = field.HasAttribute<JsonIgnoreAttribute>();
		var hasHide = field.HasAttribute<HideAttribute>();

		if ( hasIgnore || hasHide )
			return;

		var mergerType = typeLibrary.GetType( merger.GetType() );
		if ( mergerType.IsGenericType )
			return;

		var mergerValue = field.GetValue( merger );
		var targetValue = field.GetValue( target );

		if ( mergerValue is null )
		{
			field.SetValue( target, null );
			return;
		}

		Type mergerValueType = mergerValue?.GetType();
		Type targetValueType = targetValue?.GetType();

		if ( mergerValueType != targetValueType )
		{
			object cloneObj = typeLibrary.CloneInternal( mergerValue );
			if ( cloneObj.GetType().IsAssignableTo( field.FieldType ) )
				field.SetValue( target, cloneObj );
			return;
		}

		var fieldType = typeLibrary.GetType( mergerValueType );

		if ( fieldType.IsValueType || mergerValueType.IsAssignableTo( typeof(string) ) ||
		     mergerValueType.IsAssignableTo( typeof(Resource) ) )
		{
			field.SetValue( target, mergerValue );
			return;
		}

		if ( targetValue is null )
		{
			field.SetValue( target, typeLibrary.CloneInternal( mergerValue ) );
			return;
		}

		if ( fieldType.TargetType.IsAssignableTo( typeof(IList) ) )
		{
			IList targetList = (IList)targetValue;
			IList mergerList = (IList)mergerValue;

			MergeList( typeLibrary, targetList, mergerList );
			return;
		}

		if ( fieldType.TargetType.IsAssignableTo( typeof(IDictionary) ) )
		{
			IDictionary targetDict = (IDictionary)targetValue;
			IDictionary mergerDict = (IDictionary)mergerValue;

			MergeDictionary( typeLibrary, targetDict, mergerDict );
			return;
		}

		var members = typeLibrary.GetFieldsAndProperties( fieldType );
		foreach ( var member in members )
		{
			if ( member.IsField )
			{
				MergeField( typeLibrary, (FieldDescription)member, field.GetValue( target ), mergerValue );
				continue;
			}

			MergeProperty( typeLibrary, (PropertyDescription)member, field.GetValue( target ), mergerValue );
		}
	}

	private static void MergeProperty( this TypeLibrary typeLibrary, PropertyDescription property, object target, object merger )
	{
		Assert.True( target is not null && merger is not null );

		var hasIgnore = property.HasAttribute<JsonIgnoreAttribute>();
		var hasHide = property.HasAttribute<HideAttribute>();

		if ( hasIgnore || hasHide || !property.CanRead || !property.CanWrite )
			return;

		var mergerType = typeLibrary.GetType( merger.GetType() );
		if ( mergerType.IsGenericType )
			return;

		var mergerValue = property.GetValue( merger );
		var targetValue = property.GetValue( target );

		if ( mergerValue is null )
		{
			property.SetValue( target, null );
			return;
		}

		Type mergerValueType = mergerValue?.GetType();
		Type targetValueType = targetValue?.GetType();

		if ( mergerValueType != targetValueType )
		{
			object cloneObj = typeLibrary.CloneInternal( mergerValue );
			if ( cloneObj.GetType().IsAssignableTo( property.PropertyType ) )
				property.SetValue( target, cloneObj );
			return;
		}

		var propertyType = typeLibrary.GetType( mergerValueType );

		if ( propertyType.IsValueType || mergerValueType.IsAssignableTo( typeof(string) ) ||
		     mergerValueType.IsAssignableTo( typeof(Resource) ) )
		{
			property.SetValue( target, mergerValue );
			return;
		}

		if ( targetValue is null )
		{
			property.SetValue( target, typeLibrary.CloneInternal( mergerValue ) );
			return;
		}

		if ( propertyType.TargetType.IsAssignableTo( typeof(IList) ) )
		{
			IList targetList = (IList)targetValue;
			IList mergerList = (IList)mergerValue;

			MergeList( typeLibrary, targetList, mergerList );
			return;
		}

		if ( propertyType.TargetType.IsAssignableTo( typeof(IDictionary) ) )
		{
			IDictionary targetDict = (IDictionary)targetValue;
			IDictionary mergerDict = (IDictionary)mergerValue;

			MergeDictionary( typeLibrary, targetDict, mergerDict );
			return;
		}

		var members = typeLibrary.GetFieldsAndProperties( propertyType );
		foreach ( var member in members )
		{
			if ( member.IsField )
			{
				MergeField( typeLibrary, (FieldDescription)member, property.GetValue( target ), mergerValue );
				continue;
			}

			MergeProperty( typeLibrary, (PropertyDescription)member, property.GetValue( target ), mergerValue );
		}
	}

	private static void MergeDictionary( TypeLibrary typeLibrary, IDictionary targetDict, IDictionary mergerDict )
	{
		if ( mergerDict.Count == 0 )
			targetDict.Clear();
		else
		{
			Type targetKeyArg = typeLibrary.GetGenericArguments( targetDict.GetType() )[0];
			Type targetValueArg = typeLibrary.GetGenericArguments( targetDict.GetType() )[1];
			Type mergerKeyArg = typeLibrary.GetGenericArguments( mergerDict.GetType() )[0];
			Type mergerValueArg = typeLibrary.GetGenericArguments( mergerDict.GetType() )[1];

			if ( mergerKeyArg != targetKeyArg || mergerValueArg != targetValueArg )
				targetDict.Clear();

			foreach ( var key in mergerDict.Keys )
			{
				var value = mergerDict[key];

				bool isCorrectKeyType = key.GetType().IsAssignableTo( targetKeyArg );
				bool isCorrectValueType = value.GetType().IsAssignableTo( targetValueArg );

				if ( !isCorrectKeyType || !isCorrectValueType )
					continue;

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
						if ( value is not null )
							targetDict[key] = value;
					}
					else
					{
						var mergerValue = mergerDict[key];
						var targetValue = targetDict[key];

						if ( mergerValue.GetType() == targetValue.GetType() )
						{
							targetDict[key] = Merge( typeLibrary, targetValue, mergerValue );
							continue;
						}

						if ( mergerValue.GetType().IsAssignableTo( targetValueArg ) )
							targetDict[key] = typeLibrary.CloneInternal( mergerValue );
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
			Type targetArg = typeLibrary.GetGenericArguments( targetList.GetType() ).First();
			Type mergerArg = typeLibrary.GetGenericArguments( mergerList.GetType() ).First();

			if ( mergerArg != targetArg )
				targetList.Clear();

			int i;
			for ( i = 0; i < mergerList.Count; i++ )
			{
				if ( i > targetList.Count - 1 )
				{
					var value = mergerList[i];
					if ( value.GetType().IsAssignableTo( targetArg ) )
						targetList.Add( typeLibrary.CloneInternal( value ) );
				}
				else
				{
					var elemTargetType = targetList[i].GetType();
					var elemType = typeLibrary.GetType( elemTargetType );
					if ( elemType.IsValueType )
					{
						var value = mergerList[i];
						if ( value.GetType().IsAssignableTo( targetArg ) )
							targetList[i] = mergerList[i];
					}
					else
					{
						var mergerValue = mergerList[i];
						var targetValue = targetList[i];

						if ( mergerValue.GetType() == targetValue.GetType() )
						{
							targetList[i] = Merge( typeLibrary, targetValue, mergerValue );
							continue;
						}

						if ( mergerValue.GetType().IsAssignableTo( targetArg ) )
							targetList[i] = typeLibrary.CloneInternal( mergerList[i] );
					}
				}
			}

			for ( int j = targetList.Count - 1; j >= i; j-- )
			{
				targetList.RemoveAt( j );
			}
		}
	}
}
