using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using XISFLib.Viewer;

namespace XISFLib.WpfViewer;

public partial class MainWindow
{
    private readonly ObservableCollection<PropertyEditorRow> _imageProperties = [];
    private readonly ObservableCollection<PropertyEditorRow> _documentMetadata = [];
    private readonly ObservableCollection<PropertyEditorRow> _documentProperties = [];
    private readonly ObservableCollection<FitsKeywordEditorRow> _fitsKeywords = [];
    private XisfImageData? _editableImage;
    private byte[]? _iccProfile;
    private XisfImageData? _thumbnail;
    private int _editorImageIndex = -1;
    private string? _sourceXisfPath;
    private TableEditorFields _tableEditor = new();

    private void InitializeWorkbench()
    {
        ImageTypeComboBox.ItemsSource = new[] { "", "Bias", "Dark", "Flat", "Light", "MasterBias", "MasterDark", "MasterFlat", "MasterLight", "DefectMap", "RejectionMapHigh", "RejectionMapLow", "BinaryRejectionMapHigh", "BinaryRejectionMapLow", "SlopeMap", "WeightMap" };
        ColorSpaceComboBox.ItemsSource = new[] { "Gray", "RGB", "CIELab" };
        OrientationComboBox.ItemsSource = new[] { "0", "flip", "90", "90;flip", "-90", "-90;flip", "180", "180;flip" };
        ResolutionUnitComboBox.ItemsSource = new[] { "inch", "cm" };
        CompressionComboBox.ItemsSource = Enum.GetValues<XisfCompression>();
        ChecksumComboBox.ItemsSource = Enum.GetValues<XisfChecksum>();
        CompressionComboBox.SelectedItem = XisfCompression.Zlib;
        ChecksumComboBox.SelectedItem = XisfChecksum.Sha256;
        OrientationComboBox.SelectedItem = "0";
        ResolutionUnitComboBox.SelectedItem = "inch";
        ImagePropertiesGrid.ItemsSource = _imageProperties;
        DocumentMetadataGrid.ItemsSource = _documentMetadata;
        DocumentPropertiesGrid.ItemsSource = _documentProperties;
        FitsKeywordsGrid.ItemsSource = _fitsKeywords;
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
    {
        if (sender is FrameworkElement { DataContext: PropertyEditorRow row }) await OpenPropertyValueAsync(row);
    }

    private async Task OpenPropertyValueAsync(PropertyEditorRow row)
    {
        if (_isBusy) return;
        try
        {
            CommitEditorGrids();
            await PropertyValueWindow.OpenAsync(this, row);
        }
        catch (Exception error) { ShowError(error.Message); }
    }

    private void UnloadButton_Click(object sender, RoutedEventArgs e)
    {
        _document = null;
        _fitsDocument = null;
        _editableImages = [];
        _editableImage = null;
        _editorImageIndex = -1;
        _sourceXisfPath = null;
        _iccProfile = null;
        _thumbnail = null;

        FilePathTextBox.Clear();
        ImageSelector.ItemsSource = null;
        ImageSelector.SelectedIndex = -1;
        ImageSelector.IsEnabled = false;
        ImageView.Source = null;
        ResetImageViewport();
        EmptyViewText.Visibility = Visibility.Visible;
        ImageDetailsText.Text = string.Empty;

        _imageProperties.Clear();
        _fitsKeywords.Clear();
        ClearDocumentEditors();
        ClearImageEditor();

        MetadataTree.ItemsSource = null;
        MetadataTree.Visibility = Visibility.Collapsed;
        EmptyMetadataText.Text = "Load an XISF or FITS file to view its metadata.";
        EmptyMetadataText.Visibility = Visibility.Visible;
        DiagnosticsGrid.ItemsSource = null;
        DiagnosticsGrid.Visibility = Visibility.Collapsed;
        EmptyDiagnosticsText.Visibility = Visibility.Visible;
        PreserveOriginalCheckBox.IsChecked = false;
        PreserveOriginalCheckBox.IsEnabled = false;
        ClearUtilityResults();
        StatusText.Text = "No image loaded.";
    }

    private void ClearImageEditor()
    {
        ImageIdTextBox.Clear();
        ImageTypeComboBox.Text = string.Empty;
        ColorSpaceComboBox.Text = string.Empty;
        OrientationComboBox.SelectedItem = "0";
        OffsetTextBox.Text = "0";
        UuidTextBox.Clear();
        ResolutionXTextBox.Clear();
        ResolutionYTextBox.Clear();
        ResolutionUnitComboBox.SelectedItem = "inch";
        CfaPatternTextBox.Clear();
        CfaNameTextBox.Clear();
        CfaWidthTextBox.Clear();
        CfaHeightTextBox.Clear();
        RgbGammaTextBox.Clear();
        RgbSrgbCheckBox.IsChecked = false;
        RgbChromaticitiesTextBox.Clear();
        RgbYChromaticitiesTextBox.Clear();
        RgbLuminanceTextBox.Clear();
        DisplayFunctionTextBox.Clear();
        IccProfileText.Text = "None";
        ThumbnailText.Text = "None";
        PopulateTable(null);
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Import a standard 2D image",
            Filter = "Supported images|*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp;*.gif|PNG (*.png)|*.png|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|TIFF (*.tif;*.tiff)|*.tif;*.tiff|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        await RunBusyAsync("Importing raster image…", async () =>
        {
            (XisfImageData image, BitmapSource preview) = await Task.Run(() => ImportRasterImage(dialog.FileName, false));
            _document = null;
            _fitsDocument = null;
            _editableImages = [image];
            _sourceXisfPath = null;
            PreserveOriginalCheckBox.IsChecked = false;
            PreserveOriginalCheckBox.IsEnabled = false;
            _editableImage = image;
            _editorImageIndex = -1;
            FilePathTextBox.Text = dialog.FileName;
            ImageSelector.ItemsSource = new[] { $"1: {Path.GetFileNameWithoutExtension(dialog.FileName)}" };
            ImageSelector.SelectedIndex = 0;
            ImageSelector.IsEnabled = false;
            ImageView.Source = preview;
            ResetImageViewport();
            EmptyViewText.Visibility = Visibility.Collapsed;
            ImageDetailsText.Text = $"{preview.PixelWidth} × {preview.PixelHeight}, {image.ChannelCount} channel(s), UInt8, imported raster";
            ClearDocumentEditors();
            PopulateImageEditor(image);
            RefreshUtilityOverview();
            MetadataTree.Visibility = Visibility.Collapsed;
            EmptyMetadataText.Visibility = Visibility.Visible;
            EmptyMetadataText.Text = "This image has not been saved as XISF yet.";
            DiagnosticsGrid.ItemsSource = null;
            DiagnosticsGrid.Visibility = Visibility.Collapsed;
            EmptyDiagnosticsText.Visibility = Visibility.Visible;
            StatusText.Text = $"Imported {Path.GetFileName(dialog.FileName)}. Configure metadata and save it as XISF.";
        });
    }

    private static (XisfImageData Image, BitmapSource Preview) ImportRasterImage(string path, bool thumbnail)
    {
        var image = ViewerRasterDecoder.Load(path, thumbnail);
        return (image, image.ToBitmapSource());
    }

    private async Task LoadImageEditorAsync(int index)
    {
        if (_editableImages[index] is null)
        {
            XisfImage source = _document?.Images[index] ?? throw new InvalidOperationException("The selected image is unavailable.");
            _editableImages[index] = await Task.Run(() => XisfImageData.FromImage(source));
        }
        _editorImageIndex = index;
        XisfImageData editable = _editableImages[index]!;
        _editableImage = editable;
        PopulateImageEditor(editable);
    }

    private static XisfTableData CopyTable(XisfTable table)
    {
        XisfTableData result = new() { Id = table.Id, Caption = table.Caption };
        foreach (XisfTableField field in table.Fields) result.Fields.Add(new XisfTableFieldData(field.Id, field.Type, field.Header, field.Format));
        foreach (XisfTableRow row in table.Rows) result.Rows.Add(row.Cells.Select(cell => cell.Value).ToArray());
        return result;
    }

    private void PopulateImageEditor(XisfImageData image)
    {
        ImageIdTextBox.Text = image.Id ?? "";
        ImageTypeComboBox.Text = image.ImageType ?? "";
        ColorSpaceComboBox.Text = image.ColorSpace ?? "";
        OrientationComboBox.SelectedItem = image.Orientation ?? "0";
        OffsetTextBox.Text = image.Offset.ToString("R", CultureInfo.InvariantCulture);
        UuidTextBox.Text = image.Uuid?.ToString("D") ?? "";
        ResolutionXTextBox.Text = image.Resolution?.Horizontal.ToString("R", CultureInfo.InvariantCulture) ?? "";
        ResolutionYTextBox.Text = image.Resolution?.Vertical.ToString("R", CultureInfo.InvariantCulture) ?? "";
        ResolutionUnitComboBox.SelectedItem = image.Resolution?.Unit ?? "inch";
        Replace(_imageProperties, image.Properties.Select(pair => ViewerEditorCodec.ToEditorRow(pair.Key, pair.Value)));
        Replace(_fitsKeywords, image.FitsKeywords.Select(keyword => new FitsKeywordEditorRow { Name = keyword.Name, Value = keyword.Value, Comment = keyword.Comment ?? "" }));
        CfaPatternTextBox.Text = image.ColorFilterArray?.Pattern ?? "";
        CfaNameTextBox.Text = image.ColorFilterArray?.Name ?? "";
        CfaWidthTextBox.Text = image.ColorFilterArray?.Width.ToString(CultureInfo.InvariantCulture) ?? "";
        CfaHeightTextBox.Text = image.ColorFilterArray?.Height.ToString(CultureInfo.InvariantCulture) ?? "";
        RgbGammaTextBox.Text = image.RgbWorkingSpace?.Gamma.ToString("R", CultureInfo.InvariantCulture) ?? "";
        RgbSrgbCheckBox.IsChecked = image.RgbWorkingSpace?.IsSrgb == true;
        RgbChromaticitiesTextBox.Text = FormatNumbers(image.RgbWorkingSpace?.ChromaticityCoordinates);
        RgbYChromaticitiesTextBox.Text = FormatNumbers(image.RgbWorkingSpace?.YChromaticityCoordinates);
        RgbLuminanceTextBox.Text = FormatNumbers(image.RgbWorkingSpace?.LuminanceCoefficients);
        DisplayFunctionTextBox.Text = image.DisplayFunction is null ? "" : string.Join("; ", new[] { FormatNumbers(image.DisplayFunction.Midtones), FormatNumbers(image.DisplayFunction.Shadows), FormatNumbers(image.DisplayFunction.Highlights), FormatNumbers(image.DisplayFunction.LowRange), FormatNumbers(image.DisplayFunction.HighRange) });
        _iccProfile = image.IccProfile;
        _thumbnail = image.Thumbnail;
        IccProfileText.Text = _iccProfile is null ? "None" : $"{_iccProfile.Length:N0} bytes";
        ThumbnailText.Text = _thumbnail is null ? "None" : $"{_thumbnail.Dimensions[0]} × {_thumbnail.Dimensions[1]}";
        PopulateTable(image.Tables.FirstOrDefault());
    }

    private void PopulateTable(XisfTableData? table)
    {
        _tableEditor = TableEditorFields.FromTable(table);
        TableIdTextBox.Text = _tableEditor.Id; TableCaptionTextBox.Text = _tableEditor.Caption;
        TableFieldsTextBox.Text = _tableEditor.Fields; TableRowsTextBox.Text = _tableEditor.Rows;
        foreach (var box in new[] { TableIdTextBox, TableCaptionTextBox, TableFieldsTextBox, TableRowsTextBox })
        { box.IsReadOnly = _tableEditor.IsReadOnly; box.ToolTip = _tableEditor.Notice; }
    }

    private void PopulateLoadedDocumentEditors(XisfDocument document)
    {
        Replace(_documentMetadata, document.Metadata.Where(pair => pair.Key is not ("XISF:CreationTime" or "XISF:CreatorApplication")).Select(ToEditorRow));
        Replace(_documentProperties, document.Properties.Select(ToEditorRow));
    }

    private static PropertyEditorRow ToEditorRow(KeyValuePair<string, XisfProperty> pair) => ViewerEditorCodec.ToEditorRow(pair);
    private static PropertyEditorRow ToEditorRow(string id, XisfPropertyData property) => ViewerEditorCodec.ToEditorRow(id, property);

    private void ClearDocumentEditors()
    {
        _documentMetadata.Clear();
        _documentProperties.Clear();
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (T value in values) target.Add(value);
    }

    private async void IccProfileButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new() { Title = "Select an ICC profile", Filter = "ICC profiles (*.icc;*.icm)|*.icc;*.icm|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        await RunBusyAsync("Reading ICC profile…", async () =>
        {
            _iccProfile = await Task.Run(() => File.ReadAllBytes(dialog.FileName));
            IccProfileText.Text = $"{Path.GetFileName(dialog.FileName)} ({_iccProfile.Length:N0} bytes)";
        });
    }

    private async void ThumbnailButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new() { Title = "Select a thumbnail image", Filter = "Supported images|*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp;*.gif|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        await RunBusyAsync("Reading thumbnail…", async () =>
        {
            _thumbnail = await Task.Run(() => ViewerRasterDecoder.Load(dialog.FileName, true));
            ThumbnailText.Text = $"{Path.GetFileName(dialog.FileName)} ({_thumbnail.Dimensions[0]} × {_thumbnail.Dimensions[1]})";
        });
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editableImage is null) { ShowError("Load or import an image first."); return; }
        SaveFileDialog dialog = new()
        {
            Title = "Save astronomy image",
            Filter = DistributedCheckBox.IsChecked == true ? "Distributed XISF header (*.xish)|*.xish" : "XISF image (*.xisf)|*.xisf|FITS image (*.fits)|*.fits",
            DefaultExt = DistributedCheckBox.IsChecked == true ? ".xish" : ".xisf",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) != true) return;
        bool fitsOutput = IsFitsPath(dialog.FileName);
        bool distributed = !fitsOutput && DistributedCheckBox.IsChecked == true;
        bool preserveOriginal = !fitsOutput && !distributed && PreserveOriginalCheckBox.IsChecked == true && _sourceXisfPath is not null;
        await RunBusyAsync("Writing and validating astronomy image…", async () =>
        {
            string path = dialog.FileName;
            IReadOnlyList<ConversionFinding> conversionFindings = [];
            if (preserveOriginal)
            {
                await Task.Run(() => CopyFileUnlessSame(_sourceXisfPath!, path));
            }
            else
            {
                XisfWriterOptions options = CreateWriterOptions();
                CommitEditorGrids();
                var document = DocumentEditorState.Parse(_documentMetadata, _documentProperties, options.CreatorApplication);
                ApplyImageEditor(_editableImage);
                conversionFindings = await Task.Run(() => ViewerFileService.Save(path, _editableImage, document, options, distributed, null));
            }
            LoadedFile loaded = await Task.Run(() => LoadFile(path));
            _document = loaded.Xisf;
            _fitsDocument = loaded.Fits;
            _editableImages = loaded.Images;
            _sourceXisfPath = distributed || fitsOutput ? null : path;
            PreserveOriginalCheckBox.IsEnabled = !distributed && !fitsOutput;
            PreserveOriginalCheckBox.IsChecked = !distributed && !fitsOutput;
            FilePathTextBox.Text = path;
            MetadataTree.ItemsSource = _document is not null ? BuildMetadata(_document, path) : BuildFitsMetadata(_fitsDocument!, path);
            MetadataTree.Visibility = Visibility.Visible;
            EmptyMetadataText.Visibility = Visibility.Collapsed;
            ImageSelector.ItemsSource = Enumerable.Range(0, _editableImages.Length).Select(GetImageLabel).ToArray();
            ImageSelector.SelectedIndex = 0;
            await LoadImageEditorAsync(0);
            if (_document is not null)
                ShowDiagnostics(_document.Validate());
            else
                ShowDiagnostics(_fitsDocument!.Diagnostics);
            if (conversionFindings.Any(f => f.Effect == ConversionEffect.Omitted))
                DiagnosticsGrid.ItemsSource = _fitsDocument!.Diagnostics.Select(d => new { Severity = d.Severity.ToString(), d.Code, d.Message })
                    .Concat(conversionFindings.Where(f => f.Effect == ConversionEffect.Omitted).Select(f => new { Severity = "Conversion", f.Code, f.Message })).ToArray();
            StatusText.Text = preserveOriginal
                ? $"Saved a byte-identical copy and reloaded {Path.GetFileName(path)}. Editor changes were not applied."
                : $"Saved, reloaded and validated {Path.GetFileName(path)}{(fitsOutput ? " as FITS" : "")}.";
            await RenderSelectedImageAsync();
        });
    }

    private static void CopyFileUnlessSame(string sourcePath, string destinationPath)
    {
        ViewerFileService.CopyOriginal(sourcePath, destinationPath);
    }

    private void ApplyImageEditor(XisfImageData image)
    {
        CommitEditorGrids();
        // Parse all fields before mutating the live image.
        var properties = ViewerEditorCodec.ParseProperties(_imageProperties);
        var keywords = ViewerEditorCodec.ParseFitsKeywords(_fitsKeywords);
        var attributes = new ImageEditorFields
        {
            Id = ImageIdTextBox.Text, ImageType = ImageTypeComboBox.Text, ColorSpace = ColorSpaceComboBox.Text,
            Orientation = OrientationComboBox.SelectedItem as string ?? "0", Offset = OffsetTextBox.Text, Uuid = UuidTextBox.Text,
            ResolutionX = ResolutionXTextBox.Text, ResolutionY = ResolutionYTextBox.Text, ResolutionUnit = ResolutionUnitComboBox.SelectedItem as string ?? "inch",
            CfaPattern = CfaPatternTextBox.Text, CfaWidth = CfaWidthTextBox.Text, CfaHeight = CfaHeightTextBox.Text, CfaName = CfaNameTextBox.Text,
            Gamma = RgbGammaTextBox.Text, IsSrgb = RgbSrgbCheckBox.IsChecked == true, ChromaticityX = RgbChromaticitiesTextBox.Text,
            ChromaticityY = RgbYChromaticitiesTextBox.Text, Luminance = RgbLuminanceTextBox.Text, DisplayFunction = DisplayFunctionTextBox.Text
        }.Parse(image);
        var table = CreateTable();
        attributes.Apply(image);
        image.Properties.Clear();
        foreach (var pair in properties) image.Properties.Add(pair.Key, pair.Value);
        image.FitsKeywords.Clear();
        foreach (var keyword in keywords) image.FitsKeywords.Add(keyword);
        if (attributes.WorkingSpace is { } workingSpace) RgbLuminanceTextBox.Text = FormatNumbers(workingSpace.LuminanceCoefficients);
        image.IccProfile = _iccProfile;
        image.Thumbnail = _thumbnail;
        if (table is not null)
        {
            if (image.Tables.Count == 0) image.Tables.Add(table);
            else image.Tables[0] = table;
        }
    }

    private XisfWriterOptions CreateWriterOptions()
    {
        XisfWriterOptions options = new()
        {
            Compression = (XisfCompression)(CompressionComboBox.SelectedItem ?? XisfCompression.None),
            Checksum = (XisfChecksum)(ChecksumComboBox.SelectedItem ?? XisfChecksum.None),
            Shuffle = ShuffleCheckBox.IsChecked == true,
            CreatorApplication = CreatorTextBox.Text.Trim()
        };
        return options;
    }

    private static void AddProperties(IDictionary<string, object?> destination, IEnumerable<PropertyEditorRow> rows)
    {
        foreach (var pair in ViewerEditorCodec.ParseProperties(rows)) destination[pair.Key] = pair.Value;
    }

    private void CommitEditorGrids()
    {
        foreach (var grid in new[] { ImagePropertiesGrid, DocumentMetadataGrid, DocumentPropertiesGrid, FitsKeywordsGrid })
            if (!grid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true) || !grid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true))
                throw new FormatException("Complete or correct the active grid edit first.");
    }

    private XisfTableData? CreateTable()
    {
        _tableEditor.Id = TableIdTextBox.Text; _tableEditor.Caption = TableCaptionTextBox.Text;
        _tableEditor.Fields = TableFieldsTextBox.Text; _tableEditor.Rows = TableRowsTextBox.Text;
        return _tableEditor.Parse();
    }

    private void ValidateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_document is not null)
        {
            IReadOnlyList<XisfDiagnostic> diagnostics = _document.Validate();
            ShowDiagnostics(diagnostics);
            StatusText.Text = $"XISF validation completed with {diagnostics.Count} diagnostic(s).";
            return;
        }
        if (_fitsDocument is not null)
        {
            ShowDiagnostics(_fitsDocument.Diagnostics);
            StatusText.Text = $"FITS validation completed with {_fitsDocument.Diagnostics.Count} diagnostic(s).";
            return;
        }
        ShowError("Load or save an XISF or FITS document first.");
    }
    private void NoticesButton_Click(object sender, RoutedEventArgs e)
    {
        new Window { Owner = this, Title = "Third-party notices", Width = 850, Height = 600,
            Content = new System.Windows.Controls.TextBox { Text = ViewerNotices.Read(), IsReadOnly = true, AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto, Margin = new Thickness(12) } }.ShowDialog();
    }

    private void ShowDiagnostics(IReadOnlyList<XisfDiagnostic> diagnostics)
    {
        DiagnosticsGrid.ItemsSource = diagnostics;
        DiagnosticsGrid.Visibility = Visibility.Visible;
        EmptyDiagnosticsText.Visibility = Visibility.Collapsed;
    }

    private void ShowDiagnostics(IReadOnlyList<FitsValidationDiagnostic> diagnostics)
    {
        DiagnosticsGrid.ItemsSource = diagnostics;
        DiagnosticsGrid.Visibility = Visibility.Visible;
        EmptyDiagnosticsText.Visibility = Visibility.Collapsed;
    }

    private static object? ParseProperty(string? type, string value, int? rows = null, int? columns = null) => ViewerEditorCodec.ParseProperty(type, value, rows, columns);

    private static string CreateIdentifier(string value)
    {
        string identifier = new(value.Select(character => char.IsAsciiLetterOrDigit(character) || character == '_' ? character : '_').ToArray());
        if (identifier.Length == 0) return "ImportedImage";
        return char.IsAsciiLetter(identifier[0]) || identifier[0] == '_' ? identifier : $"_{identifier}";
    }
    private static IEnumerable<string> Lines(string text) => text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string FormatNumbers(IEnumerable<double>? values) => values is null ? "" : string.Join(", ", values.Select(value => value.ToString("R", CultureInfo.InvariantCulture)));
    private static string FormatEditorValue(object? value) => ViewerEditorCodec.FormatEditorValue(value);
    private static string PropertyTypeName(object? value) => ViewerEditorCodec.PropertyTypeName(value);
}
