using System.Collections.Generic;
using System.Linq;
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

	[Title( "Add Row - {T|RowStruct}" ), Description("WARNING: This will not save to disk!")]
	public bool Add( string rowName, RowStruct rowStruct )
	{
		if ( StructEntries.Find( x => x.RowName == rowName ) is not null )
			return false;

		rowStruct.RowName = rowName;
		StructEntries.Add( rowStruct );

		return true;
	}

	public void Fix()
	{
		var dataTable = Json.Deserialize<DataTable>( FileSystem.Mounted.ReadAllText( ResourcePath ) );
		StructType = dataTable.StructType;
		StructEntries = dataTable.StructEntries;
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
