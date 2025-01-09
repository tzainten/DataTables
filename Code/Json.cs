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

[AttributeUsage(AttributeTargets.Property)]
public class JsonTypeAnnotateAttribute : Attribute
{
}

internal static class Json
{
	public static JsonSerializerOptions Options()
	{
		return new JsonSerializerOptions() { WriteIndented = true };
	}

	public static JsonNode Serialize( object target, bool typeAnnotate )
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

		var node = SerializeObject( target, true );
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

	public static JsonObject SerializeObject( object target, bool typeAnnotate )
	{
		JsonObject jobj = new();

		var type = target.GetType();
		var typeDesc = TypeLibrary.GetType( type );

		if ( typeDesc.IsValueType || type.IsAssignableTo( typeof(Resource) ) ||
		     type.IsAssignableTo( typeof(string) ) )
			return Sandbox.Json.ToNode( target ).AsObject();

		foreach ( var property in typeDesc.Properties.Where( x => x.IsPublic && !x.IsStatic ) )
		{
			var hasIgnore = property.HasAttribute<JsonIgnoreAttribute>();
			var hasInclude = property.HasAttribute<JsonIncludeAttribute>();
			var hasProperty = property.HasAttribute<PropertyAttribute>();

			if ( !hasIgnore && (property.IsGetMethodPublic || hasInclude || hasProperty) )
			{
				var value = property.GetValue( target );
				if ( value is null )
					continue;

				jobj[property.Name] = Serialize( value, property.HasAttribute( typeof(JsonTypeAnnotateAttribute) ) );
			}
		}

		return jobj;
	}

	public static T Deserialize<T>( string json )
	{
		Utf8JsonReader reader = new(Encoding.UTF8.GetBytes( json ),
			new()
			{
				AllowTrailingCommas = false,
				CommentHandling = JsonCommentHandling.Skip
			});

		JsonNode node = JsonNode.Parse( json );
		if ( node is null )
			return default;

		return (T)DeserializeInternal( node, typeof(T) );
	}

	public static object DeserializeInternal( JsonNode node, Type type )
	{
		TypeDescription typeDesc = TypeLibrary.GetType( type );
		if ( typeDesc is not null && typeDesc.IsValueType || type.IsAssignableTo( typeof(Resource) ) ||
		     type.IsAssignableTo( typeof(string) ) )
		{
			return Sandbox.Json.FromNode( node, type );
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

			var property = typeDesc.GetProperty( node.Key );
			if ( property is null )
				continue;

			var value = DeserializeInternal( node.Value, property.PropertyType );
			if ( value is null )
				continue;

			if ( value.GetType().IsAssignableTo( property.PropertyType ) )
				property.SetValue( instance, value );
		}

		return instance;
	}
}
