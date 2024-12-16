using System.Collections.Generic;
using Sandbox;

namespace DataTables;

public class RowStruct
{
}

public class MyClass
{
	public int Blue { get; set; }

	public List<MyClass> List { get; set; }
}

public class JsonDummy
{
	public int Test { get; set; }

	public List<MyClass> Numbers { get; set; }
}

[GameResource( "Data Table", "dt", "Description", Icon = "equalizer", IconBgColor = "lime", Category = "Ignition" )]
public class DataTable : GameResource
{
	[Hide] public string StructType { get; set; }

	[Hide] public List<RowStruct> StructEntries { get; set; } = new();
}
