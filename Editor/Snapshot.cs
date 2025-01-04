using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DataTables;

namespace DataTablesEditor;

internal class Snapshot
{
	private DataTableEditor _editor;

	private List<RowStruct> _entries = new();
	private int _entryCount;

	public Snapshot( DataTableEditor editor )
	{
		_editor = editor;
		_entries = editor.InternalEntries.ToList();
		_entryCount = editor.EntryCount;
	}

	public void Restore()
	{
		_editor.InternalEntries = _entries.ToList();
		_editor.EntryCount = _entryCount;
	}
}
