using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sandbox;

namespace DataTables;

public class RowStruct
{
	public string RowName { get; set; }
}

[GameResource( "Data Table", "dt", "Description", Icon = "equalizer", IconBgColor = "#b0e24d" )]
public class DataTable : GameResource
{
	[Hide] public string StructType { get; set; }

	[Hide] public List<RowStruct> StructEntries { get; set; } = new();

	[Hide] public int EntryCount { get; set; } = 0;

	[Title( "Get Row - {T|RowStruct}" )]
	public T Get<T>( string rowName ) where T : RowStruct
	{
		return (T)StructEntries.Find( x => x.RowName == rowName );
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
			List<RowStruct> structEntries = (List<RowStruct>)Json.DeserializeArray(jobj["StructEntries"].AsArray(), typeof(List<RowStruct>) );
			Json._currentProperty = null;

			StructEntries = structEntries;
		}
	}

	protected override void OnJsonSerialize( JsonObject node )
	{
		base.OnJsonSerialize( node );

		if ( StructEntries.Count > 0 )
		{
			JsonArray ja = new();
			foreach ( var entry in StructEntries )
				ja.Add( Json.Serialize( entry ) );

			node["StructEntries"] = ja;
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
