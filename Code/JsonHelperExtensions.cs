using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;

namespace DataTables;

public enum ObjectType
{
	Number = 0,
	Object,
	String,
	Array,
	Dictionary,
	Boolean,
	Unknown
}

public static class JsonHelperExtensions
{
	public static bool IsNumber( this object o )
	{
		switch ( Type.GetTypeCode( o.GetType() ) )
		{
			case TypeCode.Byte:
			case TypeCode.SByte:
			case TypeCode.UInt16:
			case TypeCode.UInt32:
			case TypeCode.UInt64:
			case TypeCode.Int16:
			case TypeCode.Int32:
			case TypeCode.Int64:
			case TypeCode.Decimal:
			case TypeCode.Double:
			case TypeCode.Single:
				return true;
			default:
				return false;
		}
	}

	public static bool IsObject( this object o ) => Type.GetTypeCode( o.GetType() ) == TypeCode.Object;

	public static bool IsString( this object o )
	{
		switch ( Type.GetTypeCode( o.GetType() ) )
		{
			case TypeCode.String:
			case TypeCode.Char:
				return true;
			default:
				return false;
		}
	}

	public static bool IsArray( this object o )
	{
		return o is IList;
	}

	public static bool IsDictionary( this object o )
	{
		return o is IDictionary;
	}

	public static bool IsBoolean( this object o )
	{
		return o is bool;
	}

	public static ObjectType GetObjectType( this object o )
	{
		if ( o.IsNumber() )
			return ObjectType.Number;

		if ( o.IsArray() )
			return ObjectType.Array;

		if ( o.IsDictionary() )
			return ObjectType.Dictionary;

		if ( o.IsObject() )
			return ObjectType.Object;

		if ( o.IsString() )
			return ObjectType.String;

		if ( o.IsBoolean() )
			return ObjectType.Boolean;

		return ObjectType.Unknown;
	}
}
