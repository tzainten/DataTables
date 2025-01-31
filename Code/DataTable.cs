using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sandbox;

namespace DataTables;

public class RowStruct
{
	public string RowName { get; set; }
}

[GameResource( "Data Table", "dt", "Description", Icon = "equalizer", IconBgColor = "#b0e24d" )]
public class DataTable : GameResource
{
	[Hide] internal Dictionary<string, WeakReference> WeakTable = new();

	[Hide] public string StructType { get; set; }

	[Hide] public List<RowStruct> StructEntries = new();

	[Hide] public int EntryCount { get; set; } = 0;

	[Title( "Get Row - {T|RowStruct}" )]
	public T Get<T>( string rowName ) where T : RowStruct
	{
		T result = (T)StructEntries.Find( x => x.RowName == rowName );
		if ( !WeakTable.ContainsKey( rowName ) )
			WeakTable.Add( rowName, new WeakReference( result ) );
		return result;
	}

	internal void Fix()
	{
		var json = FileSystem.Mounted.ReadAllText( ResourcePath );
		if ( json is null )
			return;

		Utf8JsonReader reader = new(Encoding.UTF8.GetBytes( json ),
			new()
			{
				AllowTrailingCommas = false,
				CommentHandling = JsonCommentHandling.Skip
			});

		JsonObject jobj = Sandbox.Json.ParseToJsonObject( ref reader );

		if ( jobj.ContainsKey( "StructEntries" ) )
		{
			List<RowStruct> structEntries =
				(List<RowStruct>)Json.DeserializeList( jobj["StructEntries"].AsArray(),
					typeof(List<RowStruct>) );

			if ( StructEntries.Count == 0 )
				StructEntries = structEntries;
			else
			{
				int i;
				for ( i = StructEntries.Count - 1; i >= 0; i-- )
				{
					var row = StructEntries[i];
					var otherRow = structEntries.Find( x => x.RowName == row.RowName );
					if ( otherRow is null )
						StructEntries.RemoveAt( i );
				}

				for ( i = 0; i < structEntries.Count; i++ )
				{
					var otherRow = structEntries[i];
					var row = StructEntries.Find( x => x.RowName == otherRow.RowName );

					if ( row is not null )
					{
						TypeLibrary.Merge( row, otherRow );
					}
					else
					{
						StructEntries.Add( otherRow );
					}
				}
			}
		}
	}

	protected override void OnJsonSerialize( JsonObject node )
	{
		base.OnJsonSerialize( node );

		if ( StructEntries.Count > 0 )
		{
			JsonArray jarray = new();
			jarray = Json.SerializeList( StructEntries, true );

			node["StructEntries"] = jarray;
		}
	}

	protected override void PostLoad()
	{
		base.PostLoad();
		Fix();
	}

	protected override void PostReload()
	{
		base.PostReload();
		Fix();
	}
}
