using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sandbox;

namespace DataTables;

internal static class Json
{
	private static PropertyDescription _currentProperty;

	public static void SerializeProperty( JsonObject jo, string name, object value )
	{
		switch ( value.GetObjectType() )
		{
			case ObjectType.Number:
				double.TryParse( value.ToString(), out double result );
				jo[name] = result;
				break;
			case ObjectType.Object:
				JsonObject new_jo = new();
				SerializeObject(new_jo, value);
				jo[name] = new_jo;
				break;
			case ObjectType.String:
				jo[name] = (string)value;
				break;
			case ObjectType.Boolean:
				jo[name] = (bool)value;
				break;
			case ObjectType.Array:
				JsonArray ja = new();
				SerializeArray(ja, value as IEnumerable);
				jo[name] = ja;
				break;
			case ObjectType.Dictionary:
				JsonObject jd = new();
				SerializeDictionary(jd, value as IDictionary);
				jo[name] = jd;
				break;
			case ObjectType.Unknown:
				throw new Exception( "Unknown object type." );
		}
	}

	public static void SerializeObject( JsonObject jo, object value )
	{
		SerializeProperties( jo, value );
		jo["__type"] = value.GetType().Name;
	}

	public static void SerializeArray( JsonArray ja, IEnumerable array )
	{
		foreach ( var elem in array )
		{
			if ( elem is null )
				continue;

			if (elem.GetObjectType() == ObjectType.Object)
			{
				JsonObject new_jo = new();
				SerializeObject( new_jo, elem );
				ja.Add(new_jo);
			}
			else
			{
				//SerializeProperty( writer, elem );
				ja.Add(elem);
			}
		}
	}

	public static void SerializeDictionary( JsonObject jo, IDictionary dictionary )
	{
		foreach ( var key in dictionary.Keys )
		{
			if ( key is null )
				continue;

			var value = dictionary[key];
			if (value is null)
				continue;

			SerializeProperty( jo, key.ToString(), value );
		}
	}

	public static void SerializeProperties( JsonObject jo, object target )
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
				SerializeProperty(jo, property.Name, value);
			}
		}
	}

	public static JsonObject Serialize( object target )
	{
		JsonObject jo = new();

		SerializeObject( jo, target );

		return jo;
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
			if ( property.PropertyType.IsAssignableTo( typeof(IDictionary) ) )
			{
				IDictionary dictionary = DeserializeDictionary( value.AsObject() );
				property.SetValue( instance, dictionary );
				continue;
			}

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
					var num = value.GetValue<double>();
					if (int.TryParse(num.ToString(), out var result))
						property.SetValue(instance, result);
					else
						property.SetValue(instance, value.GetValue<double>());
					break;
				case JsonValueKind.Array:
					IList list = (IList)DeserializeArray( value.AsArray() );
					property.SetValue( instance, list );
					break;
				case JsonValueKind.Object:
					var previousProperty = _currentProperty;
					property.SetValue( instance, DeserializeObject( value.AsObject() ) );
					_currentProperty = previousProperty;
					break;
			}
		}

		return instance;
	}

	private static IDictionary DeserializeDictionary( JsonObject node )
	{
		IDictionary dictionary = null;

		var type = TypeLibrary.GetType( _currentProperty.PropertyType );
		if ( type.TargetType.IsAssignableTo( typeof(IDictionary) ) )
		{
			dictionary = (IDictionary)TypeLibrary.Create<object>( _currentProperty.PropertyType );
		}

		if ( dictionary is null )
			return dictionary;

		using var enumerator = node.GetEnumerator();
		while ( enumerator.MoveNext() )
		{
			var pair = enumerator.Current;

			var key = pair.Key;
			var value = pair.Value;

			switch ( value.GetValueKind() )
			{
				case JsonValueKind.Object:
					var previousProperty = _currentProperty;
					var obj = DeserializeObject( value.AsObject() );
					_currentProperty = previousProperty;
					dictionary.Add( key, obj );
					break;
				case JsonValueKind.String:
					dictionary.Add( key, value.GetValue<string>() );
					break;
				case JsonValueKind.False:
				case JsonValueKind.True:
					dictionary.Add( key, value.GetValue<bool>() );
					break;
				case JsonValueKind.Number:
					dictionary.Add( key, value.GetValue<double>() );
					break;
			}
		}

		return dictionary;
	}

	public static object DeserializeArray( JsonArray node )
	{
		IList list = null;

		var type = TypeLibrary.GetType( _currentProperty.PropertyType );
		if ( type.TargetType.IsAssignableTo( typeof(IList) ) )
		{
			list = (IList)TypeLibrary.Create<object>( _currentProperty.PropertyType );
		}

		if ( list is null )
			return list;

		using var enumerator = node.GetEnumerator();
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
					var num = value.GetValue<double>();
					if ( int.TryParse( num.ToString(), out var result ) )
						list.Add( result );
					else
						list.Add( value.GetValue<double>() );
					break;
			}
		}

		return list;
	}
}
