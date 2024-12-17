using System.Collections.Generic;
using Sandbox;

namespace DataTables;

public class RowStruct
{
}

[GameResource( "Data Table", "dt", "Description", Icon = "equalizer", IconBgColor = "lime", Category = "Ignition" )]
public class DataTable : GameResource
{
	[Hide] public string StructType { get; set; }

	public Dictionary<string, RowStruct> StructEntries { get; set; } = new();

	private void Fix()
	{
		var dataTable = Json.Deserialize<DataTable>( FileSystem.Mounted.ReadAllText( ResourcePath ) );
		StructType = dataTable.StructType;
		StructEntries = dataTable.StructEntries;
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
