using System;

namespace DataTables;

/// <summary>
/// This doesn't actually do anything special. It's just used to tell the editor to use our custom ListControlWidget
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class InstancedAttribute : Attribute
{
}
