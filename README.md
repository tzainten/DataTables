# How to use

## Define your RowStruct in C#
```csharp
// This is a valid RowStruct
public class YourRowStruct : RowStruct
{
	public int Number { get; set; }
}
```
Then create a DataTable asset in the Asset Browser and assign your struct to it

## How to get data in C#
```csharp
DataTable table = ResourceLibrary.Get<DataTable>("your_data_table.dt");
int Number = table.Get<YourRowStruct>("NewEntry_0").Number;
```

## Insert a new row in C#
:warning: This won't save to disk. Maybe this will be implemented later.
```csharp
YourRowStruct yourRowStruct = new();
yourRowStruct.Number = 69;

DataTable table = ResourceLibrary.Get<DataTable>("your_data_table.dt");
table.Add("UniqueRowName", yourRowStruct); 
```

# Instanced Attribute

This attribute lets you create subclasses of an object inside a List.

Here's how to use it.
```csharp
// Your Base Class
public class Something
{
}

// Your Subclass1
public class CoolThing1 : Something
{
	public string Text { get; set; }
}

// Your Subclass2
public class CoolThing2 : Something
{
	public int IntegerProperty { get; set; }

	public List<int> Numbers { get; set; }
}

public class YourCoolRowStruct : RowStruct
{
	[Instanced]
	public List<Something> Somethings { get; set; } // The editor will let you insert `Something`, `CoolThing1` or `CoolThing2` inside this list
}
```

# :warning: Known Issues :warning:
- Custom Generic Types are not currently supported. I haven't been able to get TypeLibrary to work with this yet, but hopefully in the future this will get fixed.
- `DataTable.Add` doesn't save to disk. This isn't currently possible because `FileSystem.Mounted` does not have write access
    - If there's a way to get type annotation to work using Sandbox.Json, then this could probably get fixed.
