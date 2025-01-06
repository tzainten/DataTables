using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sandbox;

namespace DataTables;

public class RowStruct
{
	public int Integer { get; set; }
}

[GameResource( "Data Table", "dt", "Description", Icon = "equalizer", IconBgColor = "#b0e24d" )]
public class DataTable : GameResource
{
	[Hide] public string StructType { get; set; }

	public Dictionary<string, RowStruct> StructEntries = new();

	[Hide] public int EntryCount { get; set; } = 0;

	[Title( "Get Row - {T|RowStruct}" )]
	public T Get<T>( string rowName ) where T : RowStruct
	{
		return (T)StructEntries[rowName];
	}

	private void Fix()
	{
		Utf8JsonReader reader = new(Encoding.UTF8.GetBytes( FileSystem.Mounted.ReadAllText( ResourcePath ) ),
			new()
			{
				AllowTrailingCommas = false,
				CommentHandling = JsonCommentHandling.Skip
			});

		JsonObject jobj = Sandbox.Json.ParseToJsonObject( ref reader );

		if ( jobj.ContainsKey( "StructEntries" ) )
		{
			Json._currentProperty = null;
			Dictionary<string, RowStruct> structEntries =
				(Dictionary<string, RowStruct>)Json.DeserializeDictionary( jobj["StructEntries"].AsObject(),
					typeof(Dictionary<string, RowStruct>) );
			Json._currentProperty = null;

			if ( StructEntries.Count == 0 )
				StructEntries = structEntries;
			else
			{
				foreach ( var pair in structEntries )
				{
					if ( StructEntries.ContainsKey( pair.Key ) )
					{
						var entry = StructEntries[pair.Key];
						TypeLibrary.Merge( ref entry, pair.Value );
					}
					else
					{
						StructEntries.Add( pair.Key, pair.Value );
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
			JsonObject jobj = new();
			Json._currentProperty = null;
			Json.SerializeDictionary( jobj, StructEntries );
			Json._currentProperty = null;

			node["StructEntries"] = jobj;
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
