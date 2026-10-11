using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using XISFLib.Viewer;

namespace XISFLib.WpfViewer;

public partial class MainWindow
{
    private void InitializeWorkbench()
    {
        ImageTypeComboBox.ItemsSource = new[] { "", "Bias", "Dark", "Flat", "Light", "MasterBias", "MasterDark", "MasterFlat", "MasterLight", "DefectMap", "RejectionMapHigh", "RejectionMapLow", "BinaryRejectionMapHigh", "BinaryRejectionMapLow", "SlopeMap", "WeightMap" };
        ColorSpaceComboBox.ItemsSource = new[] { "Gray", "RGB", "CIELab" };
        OrientationComboBox.ItemsSource = new[] { "0", "flip", "90", "90;flip", "-90", "-90;flip", "180", "180;flip" };
        ResolutionUnitComboBox.ItemsSource = new[] { "inch", "cm" };
        foreach (var grid in new[] { ImagePropertiesGrid, DocumentMetadataGrid, DocumentPropertiesGrid })
        {
            grid.BeginningEdit += (_, e) =>
            {
                if (e.Row.Item is PropertyEditorRow row && (row.IsReadOnly || (Equals(e.Column.Header, "Value") && row.RequiresValueDialog))) e.Cancel = true;
            };
            grid.MouseDoubleClick += async (_, _) =>
            {
                if (Equals(grid.CurrentColumn?.Header, "Value") && grid.SelectedItem is PropertyEditorRow { RequiresValueDialog: true } row)
                    await OpenPropertyValueAsync(row);
            };
        }
    }
    private async void OpenPropertyValue_Click(object sender, RoutedEventArgs e)
    { if (sender is FrameworkElement { DataContext: PropertyEditorRow row }) await OpenPropertyValueAsync(row); }
    private async Task OpenPropertyValueAsync(PropertyEditorRow row)
    {
        if (!_viewModel.CanEditMetadata) return;
        try { CommitEditorGrids(); await PropertyValueWindow.OpenAsync(this, row); }
        catch (Exception error) { ShowError(error.Message); }
    }
    private void UnloadButton_Click(object sender, RoutedEventArgs e)
    { if (_viewModel.UnloadCommand.CanExecute(null)) _ = _viewModel.UnloadCommand.ExecuteAsync(null); }
    private static OpenFileDialog RasterPicker(string title) => new()
    { Title = title, Filter = "Supported images|*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp;*.gif|All files|*.*", CheckFileExists = true };
    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanInteract) return;
        var picker = RasterPicker("Import a standard 2D image");
        if (picker.ShowDialog(this) == true) await _viewModel.ImportAsync(picker.FileName);
    }
    private async void IccProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanEditMetadata) return;
        var picker = new OpenFileDialog { Title = "Select an ICC profile", Filter = "ICC profiles|*.icc;*.icm|All files|*.*", CheckFileExists = true };
        if (picker.ShowDialog(this) == true) await _viewModel.AttachIccAsync(picker.FileName);
    }
    private async void ThumbnailButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanEditMetadata) return;
        var picker = RasterPicker("Select a thumbnail image");
        if (picker.ShowDialog(this) == true) await _viewModel.AttachThumbnailAsync(picker.FileName);
    }
    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanEditMetadata) return;
        var picker = new SaveFileDialog { Title = "Save astronomy image", AddExtension = true, OverwritePrompt = true,
            Filter = _viewModel.Distributed ? "Distributed XISF header|*.xish" : "XISF image|*.xisf|FITS image|*.fits",
            DefaultExt = _viewModel.Distributed ? ".xish" : ".xisf" };
        if (picker.ShowDialog(this) != true) return;
        try { await SaveToPathAsync(picker.FileName); }
        catch (Exception error) { ShowError(error.Message); }
    }
    public Task SaveToPathAsync(string path)
    {
        if (!_viewModel.CanEditMetadata) return Task.CompletedTask;
        if (!_viewModel.ShouldCopyOriginal(path)) CommitEditorGrids();
        return _viewModel.SaveAsync(path);
    }
    private void CommitEditorGrids()
    {
        foreach (var grid in new[] { ImagePropertiesGrid, DocumentMetadataGrid, DocumentPropertiesGrid, FitsKeywordsGrid })
            if (!grid.CommitEdit(DataGridEditingUnit.Cell, true) || !grid.CommitEdit(DataGridEditingUnit.Row, true))
                throw new FormatException("Complete or correct the active grid edit first.");
    }
    private void ValidateButton_Click(object sender, RoutedEventArgs e)
    { if (_viewModel.ValidateCommand.CanExecute(null)) _ = _viewModel.ValidateCommand.ExecuteAsync(null); }
    private void NoticesButton_Click(object sender, RoutedEventArgs e)
    {
        new Window { Owner = this, Title = "Third-party notices", Width = 850, Height = 600,
            Content = new TextBox { Text = ViewerNotices.Read(), IsReadOnly = true, AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(12) } }.ShowDialog();
    }
}
