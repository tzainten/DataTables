using System.Collections.Generic;
using Sandbox;

namespace DataTables;

public class RowStruct
{
	public string RowName { get; set; } = "Hello";
}

[GameResource( "Data Table", "dt", "Description", Icon = "equalizer", IconBgColor = "lime" )]
public class DataTable : GameResource
{
	[Hide] public string StructType { get; set; }

	[Hide] public List<RowStruct> StructEntries { get; set; } = new();

	public T Get<T>( string rowName ) where T : RowStruct
	{
		return (T)StructEntries.Find( x => x.RowName == rowName );
	}

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
