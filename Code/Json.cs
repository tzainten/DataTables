using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sandbox;
using Sandbox.Diagnostics;

namespace DataTables;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class JsonTypeAnnotateAttribute : Attribute
{
}

internal static class Json
{
	public static JsonSerializerOptions Options()
	{
		return new JsonSerializerOptions() { WriteIndented = true };
	}

	public static JsonNode Serialize( object target, bool typeAnnotate, Type typeOverride = null )
	{
		var type = target.GetType();
		var typeDesc = TypeLibrary.GetType( type );

		if ( typeDesc.IsValueType || type.IsAssignableTo( typeof(Resource) ) ||
		     type.IsAssignableTo( typeof(string) ) )
			return Sandbox.Json.ToNode( target );

		if ( type.IsAssignableTo( typeof(IList) ) )
			return SerializeList( (IList)target, typeAnnotate );

		if ( type.IsAssignableTo( typeof(IDictionary) ) )
			return SerializeDictionary( (IDictionary)target, typeAnnotate );

		var node = SerializeObject( target, true, typeOverride );
		if ( typeAnnotate )
			node["__type"] = typeDesc.FullName;
		return node;
	}

	public static JsonNode SerializeDictionary( IDictionary target, bool typeAnnotate )
	{
		JsonObject jdict = new();

		Type keyArg = TypeLibrary.GetGenericArguments( target.GetType() )[0];

		bool isInteger = keyArg == typeof(int);
		bool isString = keyArg == typeof(string);
		bool isReal = keyArg == typeof(float) || keyArg == typeof(double);

		if ( !(isInteger || isString || isReal) )
		{
			Log.Error(
				$"The type '{keyArg.FullName}' is not a supported dictionary key! If you really need this to be supported, please submit an issue @ https://github.com/tzainten/DataTables" );
			return jdict;
		}

		foreach ( var key in target.Keys )
		{
			if ( key is null )
				continue;

			var value = target[key];
			if ( value is null )
				continue;

			jdict.Add( key.ToString(), Serialize( value, typeAnnotate ) );
		}

		return jdict;
	}

	public static JsonArray SerializeList( IList target, bool typeAnnotate )
	{
		JsonArray jarray = new();

		foreach ( var elem in target )
		{
			if ( elem is null )
				continue;

			jarray.Add( Serialize( elem, typeAnnotate ) );
		}

		return jarray;
	}

	public static JsonObject SerializeObject( object target, bool typeAnnotate, Type typeOverride = null )
	{
		JsonObject jobj = new();

		var type = typeOverride ?? target.GetType();
		var typeDesc = TypeLibrary.GetType( type );

		if ( typeDesc.IsValueType || type.IsAssignableTo( typeof(Resource) ) ||
		     type.IsAssignableTo( typeof(string) ) )
			return Sandbox.Json.ToNode( target ).AsObject();

		var members = TypeLibrary.GetFieldsAndProperties( typeDesc );
		foreach ( var member in members )
		{
			object value = null;
			bool shouldAnnotate = false;

			if ( member.IsField )
			{
				FieldDescription field = (FieldDescription)member;
				value = field.GetValue( target );
				if ( value is null )
					continue;

				shouldAnnotate = field.HasAttribute( typeof(JsonTypeAnnotateAttribute) );
				jobj[field.Name] = Serialize( value, shouldAnnotate, !shouldAnnotate ? field.FieldType : null );

				continue;
			}

			PropertyDescription property = (PropertyDescription)member;
			value = property.GetValue( target );
			if ( value is null )
				continue;

			shouldAnnotate = property.HasAttribute( typeof(JsonTypeAnnotateAttribute) );
			jobj[property.Name] = Serialize( value, shouldAnnotate, !shouldAnnotate ? property.PropertyType : null );
		}

		return jobj;
	}

	public static T Deserialize<T>( string json )
	{
		JsonNode node = JsonNode.Parse( json );
		if ( node is null )
			return default;

		return (T)DeserializeInternal( node, typeof(T) );
	}

	public static object DeserializeInternal( JsonNode node, Type type )
	{
		TypeDescription typeDesc = TypeLibrary.GetType( type );
		if ( typeDesc is not null && (typeDesc.IsValueType || type.IsAssignableTo( typeof(Resource) ) ||
		                              type.IsAssignableTo( typeof(string) )) )
		{
			try
			{
				return Sandbox.Json.FromNode( node, type );
			}
			catch ( Exception e )
			{
				return null;
			}
		}

		if ( type.IsAssignableTo( typeof(IDictionary) ) )
			return DeserializeDictionary( node.AsObject(), type );

		switch ( node.GetValueKind() )
		{
			case JsonValueKind.Object:
				return DeserializeObject( node.AsObject(), type );
			case JsonValueKind.Array:
				return DeserializeList( node.AsArray(), type );
			default:
				return Sandbox.Json.FromNode( node, type );
		}
	}

	public static IList DeserializeList( JsonArray jarray, Type type )
	{
		IList list = TypeLibrary.Create<IList>( type );

		using var enumerator = jarray.GetEnumerator();
		while ( enumerator.MoveNext() )
		{
			var node = enumerator.Current;

			Type genericArg = TypeLibrary.GetGenericArguments( type ).First();

			var elem = DeserializeInternal( node, genericArg );
			if ( elem is null )
				continue;

			if ( elem.GetType().IsAssignableTo( genericArg ) )
				list.Add( elem );
		}

		return list;
	}

	public static IDictionary DeserializeDictionary( JsonObject jobj, Type type )
	{
		IDictionary dict = TypeLibrary.Create<IDictionary>( type );

		using var enumerator = jobj.GetEnumerator();
		while ( enumerator.MoveNext() )
		{
			var pair = enumerator.Current;

			Type[] genericArgs = TypeLibrary.GetGenericArguments( type );
			var keyType = genericArgs[0];

			var key = pair.Key;
			object parsedKey = key;
			if ( keyType == typeof(int) )
			{
				if ( int.TryParse( key, out int num ) )
					parsedKey = num;
			}
			else if ( keyType == typeof(double) )
			{
				if ( double.TryParse( key, out double num ) )
					parsedKey = num;
			}
			else if ( keyType == typeof(float) )
			{
				if ( float.TryParse( key, out float num ) )
					parsedKey = num;
			}

			var node = pair.Value;

			var elem = DeserializeInternal( node, genericArgs[1] );
			if ( elem is null )
				continue;

			Type keyArg = TypeLibrary.GetGenericArguments( type )[0];
			Type valueArg = TypeLibrary.GetGenericArguments( type )[1];

			bool isCorrectKeyType = parsedKey.GetType().IsAssignableTo( keyArg );
			bool isCorrectValueType = elem.GetType().IsAssignableTo( valueArg );

			if ( !isCorrectKeyType || !isCorrectValueType )
				continue;

			if ( elem.GetType().IsAssignableTo( genericArgs[1] ) )
				dict.Add( parsedKey, elem );
		}

		return dict;
	}

	public static object DeserializeObject( JsonObject jobj, Type type )
	{
		jobj.TryGetPropertyValue( "__type", out JsonNode __type );

		TypeDescription typeDesc = null;
		if ( __type is not null )
		{
			typeDesc = TypeLibrary.GetType( __type.GetValue<string>() );
		}
		else
		{
			typeDesc = TypeLibrary.GetType( type );
		}

		if ( typeDesc is null )
			return null;

		object instance = TypeLibrary.Create<object>( typeDesc.TargetType );
		using var enumerator = jobj.GetEnumerator();
		while ( enumerator.MoveNext() )
		{
			var node = enumerator.Current;

			var property = typeDesc.Properties.FirstOrDefault( x =>
				x.IsPublic && !x.IsStatic && x.IsNamed( node.Key ) && x.CanWrite && x.CanRead );
			bool isValidProperty = property is not null;

			var field = typeDesc.Fields.FirstOrDefault( x => x.IsPublic && !x.IsStatic && x.IsNamed( node.Key ) );
			bool isValidField = field is not null;

			if ( !isValidProperty && !isValidField )
				continue;

			var deserializeType = isValidProperty ? property.PropertyType : field.FieldType;
			var value = DeserializeInternal( node.Value, deserializeType );
			if ( value is null )
				continue;

			if ( value.GetType().IsAssignableTo( deserializeType ) )
			{
				if ( isValidProperty )
					property.SetValue( instance, value );
				else
					field.SetValue( instance, value );
			}
		}

		return instance;
	}
}
