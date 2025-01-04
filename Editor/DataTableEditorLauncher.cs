using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using DataTables;
using Editor;
using Sandbox;
using FileSystem = Sandbox.FileSystem;

namespace DataTablesEditor;

[EditorForAssetType( "dt" )]
public class DataTableEditorLauncher : BaseWindow, IAssetEditor
{
	public bool CanOpenMultipleAssets => false;

	private Asset _asset;

	private DataTable _dataTable;

	public DataTableEditorLauncher()
	{
	}

	private void FillLayout()
	{
		DeleteOnClose = true;

		Size = new Vector2( 300, 75 );
		WindowTitle = "Select a RowStruct type";
		SetWindowIcon( "equalizer" );

		Layout = Layout.Column();
		Layout.Margin = 8;
		Layout.Spacing = 8;

		var dropdown = new Dropdown();
		dropdown.Icon = "error";
		dropdown.Text = "None";
		dropdown.PopulatePopup = widget =>
		{
			var structTypes = TypeLibrary.GetTypes().Where( x => x.TargetType.IsSubclassOf( typeof(RowStruct) ) );

			foreach ( var structType in structTypes )
			{
				bool passValidationCheck = true;
				foreach ( var property in structType.Properties.Where( x => x.IsPublic && !x.IsStatic ) )
				{
					var type = property.PropertyType;
					bool isList = type.IsAssignableTo( typeof(IList) );
					bool isDictionary = type.IsAssignableTo( typeof(IDictionary) );

					if ( type.IsGenericType && (!isList && !isDictionary) )
						passValidationCheck = false;
				}

				var btn = new DropdownButton( dropdown, structType.Name );
				btn.Value = structType;
				btn.Icon = "account_tree";

				if ( !passValidationCheck )
				{
					btn.Enabled = false;
					btn.Icon = "error";
					btn.ToolTip = "\u26a0\ufe0f This RowStruct type contains generic properties that are not supported.";
					btn.Label.Color = Color.White;
					btn.IconButton.Foreground = Color.White;
					btn.IconButton.TranslucentBackground = false;
				}

				widget.Layout.Add( btn );
			}
		};

		var lbl = new Label( "Please assign a RowStruct type to this Data Table:" );

		Layout.AddStretchCell();
		Layout.Add( lbl );
		Layout.Add( dropdown );

		var row = Layout.AddRow();
		row.AddStretchCell();

		var confirmBtn = new Button.Primary( "Confirm", icon: "done" );
		confirmBtn.Enabled = false;
		confirmBtn.Clicked = () =>
		{
			var type = dropdown.Value as TypeDescription;
			_dataTable.StructType = type.FullName;
			_asset.SaveToDisk( _dataTable );
			OpenEditor();
		};

		dropdown.OnValueChanged = () =>
		{
			confirmBtn.Enabled = dropdown.Value is not null;
		};

		row.Add( confirmBtn );

		Layout.AddStretchCell();

		Show();
	}

	private void OpenEditor()
	{
		DataTableEditor editor = new(_asset, _dataTable);
		Close();
	}

	public void AssetOpen( Asset asset )
	{
		_asset = asset;
		_dataTable = _asset.LoadResource<DataTable>();

		var structType = TypeLibrary.GetType( _dataTable.StructType );
		if ( structType is null || !TypeLibrary.GetTypes().Any( x => x.TargetType.IsSubclassOf( typeof(RowStruct) ) ) )
		{
			FillLayout();
		}
		else
		{
			foreach ( var property in structType.Properties.Where( x => x.IsPublic && !x.IsStatic ) )
			{
				var type = property.PropertyType;
				bool isList = type.IsAssignableTo( typeof(IList) );
				bool isDictionary = type.IsAssignableTo( typeof(IDictionary) );

				if ( type.IsGenericType && (!isList && !isDictionary) )
				{
					StructDialog popup = new StructDialog( $"Data Table Editor - {asset.Path}" );
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
			OpenEditor();
		}
	}

	public void SelectMember( string memberName )
	{
		throw new System.NotImplementedException();
	}

	protected override void OnPaint()
	{
		Paint.ClearPen();
		Paint.SetBrush( Theme.WidgetBackground );
		Paint.DrawRect( LocalRect );
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
