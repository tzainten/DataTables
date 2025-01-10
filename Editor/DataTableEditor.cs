using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DataTables;
using Editor;
using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Helpers;
using Application = Editor.Application;
using FileSystem = Editor.FileSystem;
using Json = DataTables.Json;

namespace DataTablesEditor;

public class DataTableEditor : DockWindow
{
	public bool CanOpenMultipleAssets => false;

	private Dictionary<string, WeakReference> _weakTable;

	private Asset _asset;
	private DataTable _dataTable;
	private TypeDescription _structType;

	private ToolBar _toolBar;

	public List<RowStruct> InternalEntries = new();
	public int EntryCount = 0;

	private TableView _tableView;

	private ControlSheet _sheet;

	private bool _isUnsaved = false;

	private string _previousJson;
	private EditorState _previousEditorState;

	private PropertyDescription[] _previousProperties;

	private string _defaultDockState;

	private UndoStack _undoStack = new();
	private UndoHistory _undoHistory;

	public DataTableEditor( Asset asset )
	{
		_asset = asset;
		_dataTable = _asset.LoadResource<DataTable>();
		_weakTable = _dataTable.WeakTable;

		if ( _dataTable is null )
		{
			Close();
			return;
		}
		_dataTable.Fix();

		_structType = TypeLibrary.GetType( _dataTable.StructType );

		_previousProperties = _structType.Properties.ToArray();

		EntryCount = _dataTable.EntryCount;

		foreach ( var row in _dataTable.StructEntries )
		{
			InternalEntries.Add( TypeLibrary.Clone( row ) );
		}

		_previousJson = SerializeEntries();
		_lastSaveJson = _previousJson;

		DeleteOnClose = true;

		Size = new Vector2( 1000, 800 );
		Title = $"Data Table Editor - {asset.Path}";
		SetWindowIcon( "equalizer" );

		AddToolbar();
		BuildMenuBar();

		Show();
		PopulateEditor();

		_previousEditorState = new EditorState(GetSelectedNames(), _sheetRowName, EntryCount);
	}

	private string SerializeEntries()
	{
		JsonArray jarray = new();
		jarray = Json.SerializeList( InternalEntries, true );
		return jarray.ToJsonString();
	}

	[Shortcut( "editor.undo", "CTRL+Z", ShortcutType.Window )]
	private void Undo()
	{
		if ( _undoStack.Undo() is UndoOp op )
		{
			InternalEntries = (List<RowStruct>)Json.DeserializeList( JsonNode.Parse( op.undoBuffer )?.AsArray(), typeof(List<RowStruct>) );
			_previousJson = SerializeEntries();
			MarkUnsaved();
			//PopulateEditor();
			_sheet.Clear( true );

			EditorState undoState = op.UndoEditorState;
			_sheetRowName = undoState.SheetRowName;
			UpdateViewAndEditor();

			EntryCount = undoState.EntryCount;
			_tableView.ListView.Selection.Clear();
			foreach ( var rowName in undoState.SelectedNames )
			{
				var row = InternalEntries.FirstOrDefault( x => x.RowName == rowName );
				if ( row is not null )
					_tableView.ListView.Selection.Add( row );
			}

			var _row = InternalEntries.FirstOrDefault( x => x.RowName == undoState.SheetRowName );
			if ( _row is not null )
				PopulateControlSheet( _row );
		}
	}

	[Shortcut( "editor.redo", "CTRL+Y", ShortcutType.Window )]
	private void Redo()
	{
		if ( _undoStack.Redo() is UndoOp op )
		{
			InternalEntries = (List<RowStruct>)Json.DeserializeList( JsonNode.Parse( op.redoBuffer )?.AsArray(), typeof(List<RowStruct>) );
			_previousJson = SerializeEntries();
			MarkUnsaved();
			//PopulateEditor();
			_sheet.Clear( true );

			EditorState redoState = op.RedoEditorState;
			_sheetRowName = redoState.SheetRowName;
			UpdateViewAndEditor();

			EntryCount = redoState.EntryCount;
			_tableView.ListView.Selection.Clear();
			foreach ( var rowName in redoState.SelectedNames )
			{
				var row = InternalEntries.FirstOrDefault( x => x.RowName == rowName );
				if ( row is not null )
					_tableView.ListView.Selection.Add( row );
			}

			var _row = InternalEntries.FirstOrDefault( x => x.RowName == redoState.SheetRowName );
			if ( _row is not null )
				PopulateControlSheet( _row );
		}
	}

	private void MarkUnsaved()
	{
		_isUnsaved = SerializeEntries() != _lastSaveJson;
		//Title = $"Data Table Editor - {_asset.Path}*";
	}

	private void MarkSaved()
	{
		_isUnsaved = false;
		//Title = $"Data Table Editor - {_asset.Path}";
	}

	private bool SheetFilter( SerializedProperty property )
	{
		var obj = property.Parent;
		obj.OnPropertyChanged = serializedProperty =>
		{
			MarkUnsaved();
		};
		return true;
	}

	private string EvaluateTitle()
	{
		return $"Data Table Editor - {_asset.Path}{(_isUnsaved ? "*" : "")}";
	}

	private int _mouseUpFrames;

	private RealTimeSince _timeSinceChange;

	[EditorEvent.Frame]
	private void Tick()
	{
		if ( !Visible )
			return;

		_undoHistory.UndoLevel = _undoStack.UndoLevel;

		Title = EvaluateTitle();

		/*_addOption.Enabled = !Game.IsPlaying;
		_duplicateOption.Enabled = !Game.IsPlaying;
		_deleteOption.Enabled = !Game.IsPlaying;*/

		_mouseUpFrames++;
		if ( Application.MouseButtons != 0 )
		{
			_mouseUpFrames = 0;
		}

		if ( _mouseUpFrames > 2 && _timeSinceChange > 0.5f )
		{
			if ( InternalEntries is null )
				return;

			string json = SerializeEntries();
			if ( json != _previousJson )
			{
				EditorState state = new(GetSelectedNames(), _sheetRowName, EntryCount);

				_undoStack.PushUndo("Modified a RowStruct", _previousJson, _previousEditorState );
				OnUndoPushed();
				_undoStack.PushRedo( json, state );
				_previousJson = json;
				_previousEditorState = state;
				MarkUnsaved();
				_timeSinceChange = 0;
			}
		}
	}

	private void UpdateViewAndEditor()
	{
		if ( !Visible )
			return;

		_tableEditor.Layout.Clear( true );

		_tableView = new TableView( _tableEditor );
		_tableView.ListView.BodyContextMenu = () =>
		{
			var m = new ContextMenu( this );

			m.AddOption( "Add", "add", () =>
			{
				AddEntry();
			} );

			m.AddOption( "Paste", "content_paste", () =>
			{
				Paste();
			} );

			m.OpenAtCursor();
		};
		_tableView.ListView.ItemContextMenu = o =>
		{
			var m = new ContextMenu( this );

			m.AddOption( "Copy", "content_copy", () =>
			{
				Copy();
			} );

			m.AddOption( "Delete", "delete", () =>
			{
				RemoveEntry();
			} );

			m.OpenAtCursor();
		};

		var structType = TypeLibrary.GetType( _structType.TargetType );

		foreach ( var member in TypeLibrary.GetFieldsAndProperties( structType ) )
		{
			var col = _tableView.AddColumn();

			if ( member.Name == "RowName" )
				col.TextColor = Color.Parse( "#e1913c" ).GetValueOrDefault();

			col.Name = member.Name;
			col.Value = o =>
			{
				if ( member.IsField )
				{
					FieldDescription field = structType.Fields.FirstOrDefault( x => x.IsNamed( member.Name ) );
					return field?.GetValue( o )?.ToString() ?? "";
				}

				PropertyDescription property =
					structType.Properties.FirstOrDefault( x => x.IsNamed( member.Name ) );
				return property?.GetValue( o )?.ToString() ?? "";
			};
		}

		_tableView.SetItems( InternalEntries.ToList() );
		_tableView.FillHeader();

		if ( InternalEntries.Count > 0 )
		{
			_tableView.ListView.Selection.Add( InternalEntries.First() );
			_sheetRowName = InternalEntries.First().RowName;
			PopulateControlSheet( InternalEntries.First() );
		}

		_tableView.ItemClicked = o =>
		{
			RowStruct row = (RowStruct)o;
			_sheetRowName = row.RowName;
			PopulateControlSheet( o );
		};

		_tableEditor.Layout.Add( _tableView );

		RowStruct row = InternalEntries.Find( o => o.RowName == _sheetRowName );
		if ( row is not null )
		{
			PopulateControlSheet( row );
		}
	}

	private Widget _rowEditor;
	private Widget _tableEditor;

	[EditorEvent.Hotload]
	private void PopulateEditor()
	{
		if ( !Visible )
			return;

		PropertyDescription[] properties = _structType.Properties.ToArray();
		foreach ( var property in properties )
		{
			var type = property.PropertyType;
			bool isList = type.IsAssignableTo( typeof(IList) );
			bool isDictionary = type.IsAssignableTo( typeof(IDictionary) );

			if ( type.IsGenericType && (!isList && !isDictionary) )
			{
				StructDialog popup = new StructDialog( $"Data Table Editor - {_asset.Path}" );
				popup.DeleteOnClose = true;
				popup.OnWindowClosed = delegate
				{
					Close();
				};
				popup.SetModal(on: true, application: true);
				popup.Hide();
				popup.Show();
				return;
			}
		}

		DockManager.Clear();
		DockManager.RegisterDockType( "Table View", "equalizer", null, false );
		DockManager.RegisterDockType( "Row Editor", "tune", null, false );
		DockManager.RegisterDockType( "Undo History", "history", null, false );

		int i;
		for ( i = InternalEntries.Count - 1; i >= 0; i-- )
		{
			if ( InternalEntries[i] is null )
				InternalEntries.RemoveAt( i );
		}

		_rowEditor = new(this){ WindowTitle = "Row Editor" };
		_rowEditor.Layout = Layout.Column();
		_rowEditor.SetWindowIcon( "tune" );
		_rowEditor.Name = "Row Editor";

		ScrollArea scroll = new ScrollArea( _rowEditor );
		scroll.Canvas = new Widget( scroll )
		{
			Layout = Layout.Row(),
			VerticalSizeMode = SizeMode.CanGrow | SizeMode.Expand
		};

		scroll.Canvas.Layout.AddStretchCell();
		scroll.Canvas.OnPaintOverride = () =>
		{
			Paint.ClearPen();
			Paint.SetBrush( Theme.WidgetBackground );
			Paint.DrawRect( scroll.Canvas.LocalRect );

			return false;
		};

		_sheet = new ControlSheet();

		var layout = scroll.Canvas.Layout;

		var sheetCanvas = new Widget( scroll.Canvas );
		sheetCanvas.MinimumHeight = 200;
		sheetCanvas.Layout = Layout.Column();
		sheetCanvas.Layout.Add( _sheet );
		sheetCanvas.Layout.AddStretchCell();

		layout.Add( sheetCanvas );
		layout.AddStretchCell();

		var structType = TypeLibrary.GetType( _dataTable.StructType );
		if ( structType is null )
			return;

		_tableEditor = new(this){ WindowTitle = "Table View" };
		_tableEditor.Layout = Layout.Column();
		_tableEditor.SetWindowIcon( "equalizer" );
		_tableEditor.Name = "Table View";

		_tableView = new TableView( _tableEditor );
		_tableView.ListView.BodyContextMenu = () =>
		{
			var m = new ContextMenu( this );

			m.AddOption( "Add", "add", () =>
			{
				AddEntry();
			} );

			m.AddOption( "Paste", "content_paste", () =>
			{
				Paste();
			} );

			m.OpenAtCursor();
		};
		_tableView.ListView.ItemContextMenu = o =>
		{
			var m = new ContextMenu( this );

			m.AddOption( "Copy", "content_copy", () =>
			{
				Copy();
			} );

			m.AddOption( "Delete", "delete", () =>
			{
				RemoveEntry();
			} );

			m.OpenAtCursor();
		};

		foreach ( var member in TypeLibrary.GetFieldsAndProperties( structType ) )
		{
			var col = _tableView.AddColumn();

			if ( member.Name == "RowName" )
				col.TextColor = Color.Parse( "#e1913c" ).GetValueOrDefault();

			col.Name = member.Name;
			col.Value = o =>
			{
				if ( member.IsField )
				{
					FieldDescription field = structType.Fields.FirstOrDefault( x => x.IsNamed( member.Name ) );
					return field?.GetValue( o )?.ToString() ?? "";
				}

				PropertyDescription property =
					structType.Properties.FirstOrDefault( x => x.IsNamed( member.Name ) );
				return property?.GetValue( o )?.ToString() ?? "";
			};
		}

		_tableView.SetItems( InternalEntries.ToList() );
		_tableView.FillHeader();

		if ( InternalEntries.Count > 0 )
		{
			_tableView.ListView.Selection.Add( InternalEntries.First() );
			_sheetRowName = InternalEntries.First().RowName;
			PopulateControlSheet( InternalEntries.First() );
		}

		_tableView.ItemClicked = o =>
		{
			//var pair = (KeyValuePair<string, RowStruct>)o;
			RowStruct row = (RowStruct)o;
			_sheetRowName = row.RowName;
			PopulateControlSheet( o );
		};

		_tableEditor.Layout.Add( _tableView );

		_rowEditor.Layout.Add( scroll );

		_undoHistory = new UndoHistory( this, _undoStack );
		_undoHistory.OnUndo = Undo;
		_undoHistory.OnRedo = Redo;
		_undoHistory.OnHistorySelected = SetUndoLevel;

		var flags = DockManager.DockProperty.HideCloseButton | DockManager.DockProperty.HideOnClose | DockManager.DockProperty.DisallowFloatWindow;

		DockManager.AddDock( null, _tableEditor, DockArea.Top, flags );
		DockManager.AddDock( null, _rowEditor, DockArea.Bottom, flags );
		DockManager.AddDock( _rowEditor, _undoHistory, DockArea.Inside, flags );
		DockManager.RaiseDock( "Row Editor" );
		DockManager.Update();

		_defaultDockState = DockManager.State;

		if ( StateCookie != "DataTableEditor" )
		{
			StateCookie = "DataTableEditor";
		}
		else
		{
			RestoreFromStateCookie();
		}
	}

	public void OnUndoPushed()
	{
		_undoHistory.History = _undoStack.Names;
	}

	private void SetUndoLevel( int level )
	{
		if ( _undoStack.SetUndoLevel( level ) is UndoOp op )
		{
			InternalEntries = (List<RowStruct>)Json.DeserializeList( JsonNode.Parse( op.redoBuffer )?.AsArray(), typeof(List<RowStruct>) );
			_previousJson = SerializeEntries();
			MarkUnsaved();
			//PopulateEditor();
			UpdateViewAndEditor();
		}
	}

	private string _sheetRowName;

	private void PopulateControlSheet( object o )
	{
		_sheet.Clear( true );

		_sheet.Margin = new Sandbox.UI.Margin( 8 );
		_sheet.Spacing = 4f;

		SerializedObject so = o.GetSerialized();
		var properties = so.AsEnumerable();

		var props = so.AsEnumerable().Where( x =>
				x.IsPublic && x.IsEditable && !x.HasAttribute( typeof(JsonIgnoreAttribute) ) &&
				!x.HasAttribute( typeof(HideAttribute) ) )
			.OrderBy( x => x.Order )
			.ThenBy( x => x.SourceFile )
			.ThenBy( x => x.SourceLine )
			.ToList();

		foreach ( var prop in props )
		{
			_sheet.AddRow( prop );
		}
	}

	public void BuildMenuBar()
	{
		var file = MenuBar.AddMenu( "File" );
		file.AddOption( "New", "note_add", New, "editor.new" ).StatusTip = "New Data Table";
		file.AddOption( "Open", "file_open", Open, "editor.open" ).StatusTip = "Open Data Table";
		file.AddOption( "Save", "save", Save, "editor.save" ).StatusTip = "Save Data Table";
		file.AddOption( "Save As...", "save_as", SaveAs, "editor.save-as" ).StatusTip = "Save Data Table As...";
		file.AddOption( "Quit", "logout", Close, "editor.quit" ).StatusTip = "Quit";

		var view = MenuBar.AddMenu( "View" );
		view.AboutToShow += () => OnViewMenu( view );
	}

	[Shortcut( "editor.new", "CTRL+N", ShortcutType.Window )]
	private void New()
	{
		var fd = new FileDialog( null )
		{
			Title = "New Data Table",
			DefaultSuffix = $".dt",
			Directory = Path.GetDirectoryName( _asset.AbsolutePath )
		};

		fd.SetNameFilter( "Data Table (*.dt)" );
		fd.SetModeSave();

		if ( !fd.Execute() )
			return;

		//
		Action newFile = () =>
		{
			var jobj = new JsonObject();
			jobj["StructType"] = _structType.FullName;
			File.WriteAllText( fd.SelectedFile, jobj.ToJsonString() );
			_asset = AssetSystem.RegisterFile( fd.SelectedFile );
			if ( _asset == null )
			{
				Log.Warning( $"Unable to register asset {fd.SelectedFile}" );

				return;
			}

			NewAsync( _asset.AbsolutePath );
		};

		if ( _isUnsaved )
		{
			CloseDialog dialog = new($"Data Table Editor - {_asset.Path}", () =>
			{
				Save();
				newFile();
			}, () =>
			{
				_isUnsaved = false;
				if ( _dataTable.StructEntries.Count == 0 )
					_dataTable.EntryCount = 0;
				newFile();
			});
			return;
		}

		newFile();
	}

	private async void NewAsync( string path )
	{
		var asset = AssetSystem.RegisterFile( path );

		while ( !asset.IsCompiledAndUpToDate )
		{
			await Task.Yield();
		}

		MainAssetBrowser.Instance?.UpdateAssetList();
		DataTableEditor editor = new(_asset); // @TODO: When saving, don't open a new editor! Do what ShaderGraph instead
		Close();
	}

	[Shortcut( "editor.open", "CTRL+O", ShortcutType.Window )]
	private void Open()
	{
		var fd = new FileDialog( null )
		{
			Title = "Open Data Table",
			DefaultSuffix = $".dt",
			Directory = Path.GetDirectoryName( _asset.AbsolutePath )
		};

		fd.SetNameFilter( "Data Table (*.dt)" );
		fd.SetModeOpen();

		if ( !fd.Execute() )
			return;

		Action open = () =>
		{
			_asset = AssetSystem.FindByPath( fd.SelectedFile );
			if ( _asset == null )
			{
				Log.Warning( $"Failed to find asset at location: {fd.SelectedFile}" );

				return;
			}

			DataTableEditor editor = new(_asset);
			Close();
		};

		if ( _isUnsaved )
		{
			CloseDialog dialog = new($"Data Table Editor - {_asset.Path}", () =>
			{
				Save();
				open();
			}, () =>
			{
				_isUnsaved = false;
				if ( _dataTable.StructEntries.Count == 0 )
					_dataTable.EntryCount = 0;
				open();
			});
			return;
		}

		open();
	}

	private void OnViewMenu( Menu view )
	{
		view.Clear();
		view.AddOption( "Restore To Default", "settings_backup_restore", RestoreDefaultDockLayout );
		view.AddSeparator();

		foreach ( var dock in DockManager.DockTypes )
		{
			var o = view.AddOption( dock.Title, dock.Icon );
			o.Checkable = true;
			o.Checked = DockManager.IsDockOpen( dock.Title );
			o.Toggled += ( b ) =>
			{
				DockManager.SetDockState( dock.Title, b );
			};
		}
	}

	protected override void RestoreDefaultDockLayout()
	{
		DockManager.State = _defaultDockState;

		SaveToStateCookie();
	}

	private Option _addOption;
	private Option _duplicateOption;
	private Option _deleteOption;
	private void AddToolbar()
	{
		_toolBar = new ToolBar( this, "DataTableToolbar" );
		AddToolBar( _toolBar, ToolbarPosition.Top );

		_toolBar.Movable = false;
		_toolBar.AddOption( "Save", "save", Save ).StatusTip = "Save Data Table";
		_toolBar.AddOption( "Save As...", "save_as", SaveAs ).StatusTip = "Save Data Table as";
		//_toolBar.AddOption( "Browse", "common/browse.png" ).StatusTip = "Filler";d
		_toolBar.AddSeparator();
		_addOption = _toolBar.AddOption( "Add", "add", AddEntry );
		_addOption.StatusTip = "Append a new entry";
		_duplicateOption = _toolBar.AddOption( "Duplicate", "copy_all", DuplicateEntry );
		_duplicateOption.StatusTip = "Appends a duplicate of the currently selected entry";
		_deleteOption = _toolBar.AddOption( "Delete", "delete_outline", RemoveEntry );
		_deleteOption.StatusTip = "Delete the currently selected entry";

		/*var stretch = new Widget();
		stretch.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
		_toolBar.AddWidget( stretch );

		var dropdown = new Dropdown( _dataTable.StructType );
		dropdown.FixedWidth = 200;
		dropdown.Icon = "account_tree";
		_toolBar.AddWidget( dropdown );*/
	}

	private void DuplicateEntry()
	{
		List<object> newSelections = new();
		foreach ( RowStruct row in _tableView.ListView.Selection )
		{
			var o = TypeLibrary.Clone<RowStruct>( row );
			o.RowName = $"NewEntry_{EntryCount++}";

			InternalEntries.Add( o );
			_tableView.AddItem( o );

			_sheetRowName = o.RowName;
			PopulateControlSheet( o );

			newSelections.Add( o );
		}

		_tableView.ListView.Selection.Clear();
		foreach ( var newSelection in newSelections )
		{
			_tableView.ListView.Selection.Add( newSelection );
			_tableView.ListView.ScrollTo( newSelection );
		}

		MarkUnsaved();
	}

	private List<string> GetSelectedNames()
	{
		List<string> result = new();

		foreach ( RowStruct row in _tableView.ListView.Selection )
			result.Add( row.RowName );

		return result;
	}

	private void RemoveEntry()
	{
		_sheet.Clear( true );

		_previousJson = SerializeEntries();
		_previousEditorState = new(GetSelectedNames(), _sheetRowName, EntryCount);

		int index = -1;
		foreach ( RowStruct row in _tableView.ListView.Selection )
		{
			var tuple = InternalEntries.Index().First( x => x.Item.RowName == row.RowName );
			index = tuple.Index - 1;

			_tableView.ListView.RemoveItem( row );
			InternalEntries.Remove( row );
		}

		_tableView.ListView.Selection.Clear();

		if ( index < 0 )
			index = 0;
		if ( index < InternalEntries.Count )
		{
			_tableView.ListView.Selection.Add( InternalEntries[index] );

			_sheetRowName = InternalEntries[index].RowName;
			PopulateControlSheet( InternalEntries[index] );
		}

		EditorState state = new(GetSelectedNames(), _sheetRowName, EntryCount);

		var json = SerializeEntries();
		_undoStack.PushUndo( $"Remove Row(s)", _previousJson, _previousEditorState );
		OnUndoPushed();
		_undoStack.PushRedo( json, state );
		_previousJson = json;
		_previousEditorState = state;
		MarkUnsaved();
	}

	private void AddEntry()
	{
		_previousJson = SerializeEntries();

		_previousEditorState = new(GetSelectedNames(), _sheetRowName, EntryCount);

		var o = TypeLibrary.Create<RowStruct>( _dataTable.StructType );
		o.RowName = $"NewEntry_{EntryCount++}";

		InternalEntries.Add( o );
		_tableView.AddItem( o );

		_tableView.ListView.Selection.Clear();
		_tableView.ListView.Selection.Add( o );

		_sheetRowName = o.RowName;
		PopulateControlSheet( o );

		_tableView.ListView.ScrollTo( o );

		EditorState state = new(GetSelectedNames(), _sheetRowName, EntryCount);

		var json = SerializeEntries();
		_undoStack.PushUndo( $"Add Entry {o.RowName}", _previousJson, _previousEditorState );
		OnUndoPushed();
		_undoStack.PushRedo( json, state );
		_previousJson = json;
		_previousEditorState = state;
		MarkUnsaved();
	}

	private List<object> _clipboard = new();

	[Shortcut( "editor.copy", "CTRL+C", ShortcutType.Window )]
	private void Copy()
	{
		_clipboard.Clear();
		foreach ( var selection in _tableView.ListView.Selection )
			_clipboard.Add( selection );
	}

	[Shortcut( "editor.paste", "CTRL+V", ShortcutType.Window )]
	private void Paste()
	{
		if ( _clipboard.Count == 0 )
			return;

		MarkUnsaved();

		List<object> newSelections = new();
		foreach ( var selection in _clipboard )
		{
			var row = selection as RowStruct;
			var o = TypeLibrary.Clone<RowStruct>( row );
			o.RowName = $"NewEntry_{EntryCount++}";

			InternalEntries.Add( o );
			_tableView.AddItem( o );

			PopulateControlSheet( o );

			newSelections.Add( o );
		}

		_tableView.ListView.Selection.Clear();
		foreach ( var newSelection in newSelections )
		{
			_tableView.ListView.Selection.Add( newSelection );
			_tableView.ListView.ScrollTo( newSelection );
		}
	}

	[Shortcut( "editor.delete", "DEL", ShortcutType.Window )]
	private void Delete()
	{
		RemoveEntry();
	}

	private string _lastSaveJson;

	[Shortcut( "editor.save", "CTRL+S", ShortcutType.Window )]
	private void Save()
	{
		MarkSaved();

		if ( InternalEntries.Count == 0 )
			EntryCount = 0;

		for ( int i = _dataTable.StructEntries.Count - 1; i >= 0; i-- )
		{
			var row = _dataTable.StructEntries[i];
			var internalRow = InternalEntries.Find( x => x.RowName == row.RowName );
			if ( internalRow is null )
				_dataTable.StructEntries.RemoveAt( i );
		}

		for ( int i = 0; i < InternalEntries.Count; i++ )
		{
			var internalRow = InternalEntries[i];
			var row = _dataTable.StructEntries.Find( x => x.RowName == internalRow.RowName );

			if ( row is not null )
			{
				TypeLibrary.Merge( row, internalRow );
			}
			else
			{
				if ( _weakTable.TryGetValue( internalRow.RowName, out WeakReference weakReference ) )
				{
					if ( !weakReference.IsAlive )
					{
						_weakTable.Remove( internalRow.RowName );
						_dataTable.StructEntries.Insert( i, TypeLibrary.Clone( internalRow ) );
						continue;
					}

					RowStruct weakRow = (RowStruct)weakReference.Target;
					TypeLibrary.Merge( weakRow, internalRow );
					_dataTable.StructEntries.Insert( i, weakRow );
					continue;
				}

				_dataTable.StructEntries.Insert( i, TypeLibrary.Clone( internalRow ) );
			}
		}

		if (_dataTable.StructEntries.Count != InternalEntries.Count)
			Log.Error($"Data Table {_dataTable} failed to merge! {_dataTable.StructEntries.Count} != {InternalEntries.Count}"  );

		_lastSaveJson = SerializeEntries();
		_dataTable.EntryCount = EntryCount;
		if ( _asset.SaveToDisk( _dataTable ) )
			EditorUtility.PlayRawSound( "sounds/editor/success.wav" );
	}

	[Shortcut("editor.save-as", "CTRL+SHIFT+S", ShortcutType.Window)]
	private void SaveAs()
	{
		var fd = new FileDialog( null )
		{
			Title = "Save Data Table",
			DefaultSuffix = $".dt",
			Directory = Path.GetDirectoryName( _asset.AbsolutePath )
		};

		fd.SetNameFilter( "Data Table (*.dt)" );
		fd.SetModeSave();

		if ( !fd.Execute() )
			return;

		JsonObject jobj = new();
		jobj["StructType"] = _structType.FullName;
		jobj["StructEntries"] = Json.SerializeList( InternalEntries, true );
		File.WriteAllText( fd.SelectedFile, jobj.ToJsonString() );
		_asset = AssetSystem.RegisterFile( fd.SelectedFile );
		if ( _asset == null )
		{
			Log.Warning( $"Unable to register asset {fd.SelectedFile}" );

			return;
		}

		SaveAsAsync( _asset.AbsolutePath );
	}

	private async void SaveAsAsync( string path )
	{
		var asset = AssetSystem.RegisterFile( path );

		while ( !asset.IsCompiledAndUpToDate )
		{
			await Task.Yield();
		}

		MainAssetBrowser.Instance?.UpdateAssetList();
		DataTableEditor editor = new(_asset); // @TODO: When saving, don't open a new editor! Do what ShaderGraph instead

		_isUnsaved = false;
		Close();
	}

	protected override bool OnClose()
	{
		if ( _isUnsaved )
		{
			CloseDialog dialog = new($"Data Table Editor - {_asset.Path}", () =>
			{
				Save();
				Close();
			}, () =>
			{
				_isUnsaved = false;
				if ( _dataTable.StructEntries.Count == 0 )
					_dataTable.EntryCount = 0;
				Close();
			});
		}
		else
		{
			DataTableEditorLauncher.OpenAssetEditors.Remove( _asset.Path.FastHash() );
		}

		return !_isUnsaved;
	}

	public override void SetWindowIcon( string name )
	{
		Pixmap pixmap = new Pixmap( 128, 128 );
		pixmap.Clear( Color.Transparent );

		using ( Paint.ToPixmap( pixmap ) )
		{
			Rect rect = new Rect( 0.0f, 0.0f, 128f, 128f );
			Paint.ClearPen();
			Paint.SetBrush( Color.Parse( "#b0e24d" ).GetValueOrDefault() );
			Paint.DrawRect( in rect, 16f );
			Paint.SetPen( in Color.Black );
			Paint.DrawIcon( rect, name, 120f );
		}

		SetWindowIcon( pixmap );
	}
}
