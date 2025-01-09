using System.Collections.Generic;
using Sandbox;

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

	public Model Model { get; set; }
}

public class ExampleRowStruct : RowStruct
{
	public int Number { get; set; }

	[ImageAssetPath]
	public string ImagePath { get; set; }

	[JsonTypeAnnotate, Instanced]
	public List<Something> Somethings { get; set; }

	public Transform Transform { get; set; }

	public List<Transform> Transforms { get; set; }

	public Model Model { get; set; }

	public DataTable Table { get; set; }

	public Material Material { get; set; }

	public Texture Texture { get; set; }

	public Clothing Clothing { get; set; }

	[JsonTypeAnnotate, Instanced]
	public Something Something { get; set; }

	public Dictionary<string, int> Dictionary { get; set; }
}
