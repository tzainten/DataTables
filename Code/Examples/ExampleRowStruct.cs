using System.Collections.Generic;

namespace DataTables;

public class Something
{
}

public class CoolThing1 : Something
{
	public string Text { get; set; }
}

public class CoolThing2 : Something
{
	public int IntegerProperty { get; set; }

	public List<int> Numbers { get; set; }
}

public class ExampleRowStruct : RowStruct
{
	public int Number { get; set; }

	[Instanced]
	public List<Something> Somethings { get; set; }
}
