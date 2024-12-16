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

	public static void SerializeArray( Utf8JsonWriter writer, IEnumerable value )
	{
		writer.WriteStartArray();

		foreach ( var elem in value )
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

		SerializeObject( writer, target, tailWrite: tailWrite );

		writer.Flush();
		return Encoding.UTF8.GetString( stream.ToArray() );
	}
}
