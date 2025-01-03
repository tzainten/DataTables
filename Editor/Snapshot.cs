using System.Collections.Generic;
using System.Text.Json.Nodes;
using DataTables;

namespace DataTablesEditor;

public class Snapshot
{
	private JsonObject _object = new();
	private DataTable _dataTable;

	public Snapshot( DataTable dataTable )
	{
		_dataTable = dataTable;
		_object = Json.Serialize( _dataTable );
	}

	public void Restore()
	{
		var dataTable = Json.Deserialize<DataTable>( _object.ToJsonString() );
		_dataTable.StructEntries = dataTable.StructEntries;
	}
}
