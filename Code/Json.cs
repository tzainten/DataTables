using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sandbox;

namespace DataTables;

public static class Json
{
	private static PropertyDescription _currentProperty;

	public static void SerializeProperty( Utf8JsonWriter writer, object value )
	{
		switch ( value.GetObjectType() )
		{
			case ObjectType.Number:
				double.TryParse( value.ToString(), out double result );
				writer.WriteNumberValue( result );
				break;
			case ObjectType.Object:
				SerializeObject( writer, value );
				break;
			case ObjectType.String:
				writer.WriteStringValue( (string)value );
				break;
			case ObjectType.Boolean:
				writer.WriteBooleanValue( (bool)value );
				break;
			case ObjectType.Array:
				SerializeArray( writer, value as IList );
				break;
			case ObjectType.Dictionary:
				SerializeDictionary( writer, value as IDictionary );
				break;
			case ObjectType.Unknown:
				throw new Exception( "Unknown object type." );
		}
	}

	public static void SerializeObject( Utf8JsonWriter writer, object value, bool annotateType = false, Action<Utf8JsonWriter> tailWrite = null )
	{
		writer.WriteStartObject();

		if ( annotateType )
			writer.WriteString( "__type", value.GetType().FullName );

		SerializeProperties( writer, value, true );

		if ( tailWrite is not null )
			tailWrite( writer );

		writer.WriteEndObject();
	}

	public static void SerializeArray( Utf8JsonWriter writer, IEnumerable array )
	{
		writer.WriteStartArray();

		foreach ( var elem in array )
		{
			if ( elem is null )
				continue;

			TypeDescription type = TypeLibrary.GetType( elem.GetType().FullName );
			if ( type.Properties.Length > 0 )
				SerializeObject( writer, elem, true );
			else
				SerializeProperty( writer, elem );
		}

		writer.WriteEndArray();
	}

	public static void SerializeDictionary( Utf8JsonWriter writer, IDictionary dictionary )
	{
		writer.WriteStartObject();

		foreach ( var key in dictionary.Keys )
		{
			if ( key is null )
				continue;

			var value = dictionary[key];
			writer.WritePropertyName( key.ToString() );
			SerializeProperty( writer, value );
		}

		writer.WriteEndObject();
	}

	public static void SerializeProperties( Utf8JsonWriter writer, object target, bool genericCheck = false )
	{
		TypeDescription type = TypeLibrary.GetType( target.GetType().Name );
		foreach ( var property in type.Properties.Where( x => x.IsPublic && !x.IsStatic ) )
		{
			var hasIgnore = property.HasAttribute<JsonIgnoreAttribute>();
			var hasInclude = property.HasAttribute<JsonIncludeAttribute>();
			var hasProperty = property.HasAttribute<PropertyAttribute>();

			if ( !hasIgnore && (property.IsGetMethodPublic || hasInclude || hasProperty) )
			{
				var value = property.GetValue( target );
				if ( value is null )
					continue;

				_currentProperty = property;

				writer.WritePropertyName( property.Name );
				SerializeProperty( writer, value );
			}
		}
	}

	public static string Serialize( object target, Action<Utf8JsonWriter> tailWrite = null )
	{
		using MemoryStream stream = new();
		using Utf8JsonWriter writer = new(stream, new JsonWriterOptions() { Indented = true });

		if ( target.GetObjectType() == ObjectType.Object )
			SerializeObject( writer, target, tailWrite: tailWrite );
		else
			SerializeProperty( writer, target );

		_currentProperty = null;
		writer.Flush();
		return Encoding.UTF8.GetString( stream.ToArray() );
	}

	public static T Deserialize<T>( string json )
	{
		Utf8JsonReader reader = new(Encoding.UTF8.GetBytes( json ),
			new()
			{
				AllowTrailingCommas = false,
				CommentHandling = JsonCommentHandling.Skip
			});

		JsonObject obj = Sandbox.Json.ParseToJsonObject( ref reader );
		if ( obj is null )
			return default;

		_currentProperty = null;
		return (T)DeserializeObject( obj, TypeLibrary.GetType( typeof(T) ) );
	}

	public static object DeserializeObject( JsonObject target, TypeDescription targetType = null )
	{
		target.TryGetPropertyValue( "__type", out var __type );

		TypeDescription type = null;
		object instance = null;

		if ( _currentProperty is not null )
		{
			if ( __type is not null )
			{
				type = TypeLibrary.GetType( __type.GetValue<string>() );

				if ( type.IsGenericType )
				{
					var listType = TypeLibrary.GetGenericArguments( _currentProperty.PropertyType ).FirstOrDefault();
					bool isValidType = type.TargetType == listType || type.TargetType.IsSubclassOf( listType );
					if ( !isValidType )
					{
						Log.Error( "Invalid List Type!" );
						return null;
					}
				}
			}

			if ( type is null )
			{
				if ( _currentProperty.PropertyType.IsGenericType )
				{
					type = TypeLibrary.GetType( TypeLibrary.GetGenericArguments( _currentProperty.PropertyType )
						.FirstOrDefault() );
				}
				else
				{
					type = TypeLibrary.GetType( _currentProperty.PropertyType );
				}
			}
		}
		else
		{
			type = targetType;
		}

		instance = TypeLibrary.Create<object>( type.TargetType );

		using var enumerator = target.GetEnumerator();
		while ( enumerator.MoveNext() )
		{
			var pair = enumerator.Current;

			var value = pair.Value;
			if ( value is null )
				continue;

			var property = type.GetProperty( pair.Key );
			if ( property is null )
				continue;

			_currentProperty = property;

			switch ( value.GetValueKind() )
			{
				case JsonValueKind.String:
					property.SetValue( instance, value.GetValue<string>() );
					break;
				case JsonValueKind.False:
				case JsonValueKind.True:
					property.SetValue( instance, value.GetValue<bool>() );
					break;
				case JsonValueKind.Number:
					property.SetValue( instance, value.GetValue<double>() );
					break;
				case JsonValueKind.Array:
					IList list = (IList)DeserializeArray( instance, value.AsArray() );
					property.SetValue( instance, list );
					break;
			}
		}

		return instance;
	}

	public static object DeserializeArray( object target, JsonArray node )
	{
		IList list = null;

		var type = TypeLibrary.GetType( _currentProperty.PropertyType );
		if ( type.TargetType.IsAssignableTo( typeof(IList) ) )
		{
			list = (IList)TypeLibrary.Create<object>( _currentProperty.PropertyType );
		}

		if ( list is null )
			return list;

		var enumerator = node.GetEnumerator();
		while ( enumerator.MoveNext() )
		{
			var value = enumerator.Current;
			switch ( value.GetValueKind() )
			{
				case JsonValueKind.Object:
					var previousProperty = _currentProperty;
					var obj = DeserializeObject( value.AsObject() );
					_currentProperty = previousProperty;
					list.Add( obj );
					break;
				case JsonValueKind.String:
					list.Add( value.GetValue<string>() );
					break;
				case JsonValueKind.False:
				case JsonValueKind.True:
					list.Add( value.GetValue<bool>() );
					break;
				case JsonValueKind.Number:
					list.Add( value.GetValue<double>() );
					break;
			}
		}

		return list;
	}
}
