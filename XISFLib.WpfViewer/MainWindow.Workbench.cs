using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

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
        RgbLuminanceTextBox.Clear();
        DisplayFunctionTextBox.Clear();
        IccProfileText.Text = "None";
        ThumbnailText.Text = "None";
        PopulateTable(null);
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Import a standard 2D image",
            Filter = "Supported images|*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp;*.gif;*.wdp;*.jxr|PNG (*.png)|*.png|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|TIFF (*.tif;*.tiff)|*.tif;*.tiff|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            (XisfImageData image, BitmapSource preview) = ImportRasterImage(dialog.FileName, false);
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
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private static (XisfImageData Image, BitmapSource Preview) ImportRasterImage(string path, bool thumbnail)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapFrame frame = decoder.Frames[0];
        FormatConvertedBitmap converted = new(frame, PixelFormats.Bgra32, null, 0);
        int stride = checked(converted.PixelWidth * 4);
        byte[] bgra = new byte[checked(stride * converted.PixelHeight)];
        converted.CopyPixels(bgra, stride, 0);
        bool hasAlpha = !thumbnail && Enumerable.Range(0, converted.PixelWidth * converted.PixelHeight).Any(i => bgra[i * 4 + 3] != byte.MaxValue);
        int channels = hasAlpha ? 4 : 3;
        byte[] samples = new byte[checked(converted.PixelWidth * converted.PixelHeight * channels)];
        for (int source = 0, target = 0; source < bgra.Length; source += 4)
        {
            samples[target++] = bgra[source + 2];
            samples[target++] = bgra[source + 1];
            samples[target++] = bgra[source];
            if (hasAlpha) samples[target++] = bgra[source + 3];
        }
        XisfImageData image = XisfImageData.FromSamples(samples, [converted.PixelWidth, converted.PixelHeight], channels, XisfPixelLayout.Interleaved);
        image.ColorSpace = "RGB";
        image.Id = CreateIdentifier(Path.GetFileNameWithoutExtension(path));
        if (frame.DpiX > 0 && frame.DpiY > 0)
            image.Resolution = new XisfResolution(frame.DpiX, frame.DpiY, "inch");
        BitmapSource preview = frame;
        preview.Freeze();
        return (image, preview);
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
        Replace(_imageProperties, editable.Properties.Select(pair => pair.Value is XisfPropertyData property
            ? ToEditorRow(pair.Key, property)
            : new PropertyEditorRow { Id = pair.Key, Type = PropertyTypeName(pair.Value), Value = FormatEditorValue(pair.Value) }));
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
        Replace(_imageProperties, image.Properties.Select(pair => pair.Value is XisfPropertyData property
            ? ToEditorRow(pair.Key, property)
            : new PropertyEditorRow { Id = pair.Key, Type = PropertyTypeName(pair.Value), Value = FormatEditorValue(pair.Value) }));
        Replace(_fitsKeywords, image.FitsKeywords.Select(keyword => new FitsKeywordEditorRow { Name = keyword.Name, Value = keyword.Value, Comment = keyword.Comment ?? "" }));
        CfaPatternTextBox.Text = image.ColorFilterArray?.Pattern ?? "";
        CfaNameTextBox.Text = image.ColorFilterArray?.Name ?? "";
        CfaWidthTextBox.Text = image.ColorFilterArray?.Width.ToString(CultureInfo.InvariantCulture) ?? "";
        CfaHeightTextBox.Text = image.ColorFilterArray?.Height.ToString(CultureInfo.InvariantCulture) ?? "";
        RgbGammaTextBox.Text = image.RgbWorkingSpace?.Gamma.ToString("R", CultureInfo.InvariantCulture) ?? "";
        RgbSrgbCheckBox.IsChecked = image.RgbWorkingSpace?.IsSrgb == true;
        RgbChromaticitiesTextBox.Text = FormatNumbers(image.RgbWorkingSpace?.ChromaticityCoordinates);
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
        TableIdTextBox.Text = table?.Id ?? "";
        TableCaptionTextBox.Text = table?.Caption ?? "";
        TableFieldsTextBox.Text = table is null ? "" : string.Join(Environment.NewLine, table.Fields.Select(f => $"{f.Id}|{f.Type}|{f.Header}|{f.Format}"));
        TableRowsTextBox.Text = table is null ? "" : string.Join(Environment.NewLine, table.Rows.Select(row => string.Join('|', row.Select(FormatEditorValue))));
    }

    private void PopulateLoadedDocumentEditors(XisfDocument document)
    {
        Replace(_documentMetadata, document.Metadata.Where(pair => pair.Key is not ("XISF:CreationTime" or "XISF:CreatorApplication")).Select(ToEditorRow));
        Replace(_documentProperties, document.Properties.Select(ToEditorRow));
    }

    private static PropertyEditorRow ToEditorRow(KeyValuePair<string, XisfProperty> pair) =>
        ToEditorRow(pair.Key, new XisfPropertyData(pair.Value.Type, pair.Value.Value, pair.Value.Format, pair.Value.Comment));

    private static PropertyEditorRow ToEditorRow(string id, XisfPropertyData property)
    {
        Array? array = property.Value as Array;
        return new PropertyEditorRow
        {
            Id = id,
            Type = property.Type,
            Value = FormatEditorValue(property.Value),
            Format = property.Format,
            Comment = property.Comment,
            Rows = array?.Rank == 2 ? array.GetLength(0) : null,
            Columns = array?.Rank == 2 ? array.GetLength(1) : null
        };
    }

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

    private void IccProfileButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new() { Title = "Select an ICC profile", Filter = "ICC profiles (*.icc;*.icm)|*.icc;*.icm|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        _iccProfile = File.ReadAllBytes(dialog.FileName);
        IccProfileText.Text = $"{Path.GetFileName(dialog.FileName)} ({_iccProfile.Length:N0} bytes)";
    }

    private void ThumbnailButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new() { Title = "Select a thumbnail image", Filter = "Supported images|*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp;*.gif|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        (_thumbnail, _) = ImportRasterImage(dialog.FileName, true);
        _thumbnail.Id = null;
        _thumbnail.IccProfile = null;
        _thumbnail.Resolution = null;
        ThumbnailText.Text = $"{Path.GetFileName(dialog.FileName)} ({_thumbnail.Dimensions[0]} × {_thumbnail.Dimensions[1]})";
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
            if (preserveOriginal)
            {
                await Task.Run(() => CopyFileUnlessSame(_sourceXisfPath!, path));
            }
            else
            {
                ApplyImageEditor(_editableImage);
                XisfWriterOptions options = CreateWriterOptions();
                await Task.Run(() =>
                {
                    if (fitsOutput) FitsWriter.Write(path, FitsConverter.FromXisf([_editableImage]), new FitsWriterOptions { WriteChecksums = true });
                    else if (distributed) XisfWriter.WriteDistributed(path, Path.ChangeExtension(path, ".xisb"), [_editableImage], options);
                    else XisfWriter.Write(path, [_editableImage], options);
                });
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
            StatusText.Text = preserveOriginal
                ? $"Saved a byte-identical copy and reloaded {Path.GetFileName(path)}. Editor changes were not applied."
                : $"Saved, reloaded and validated {Path.GetFileName(path)}{(fitsOutput ? " as FITS" : "")}.";
            await RenderSelectedImageAsync();
        });
    }

    private static void CopyFileUnlessSame(string sourcePath, string destinationPath)
    {
        string source = Path.GetFullPath(sourcePath);
        string destination = Path.GetFullPath(destinationPath);
        if (!string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            File.Copy(source, destination, true);
    }

    private void ApplyImageEditor(XisfImageData image)
    {
        image.Id = EmptyToNull(ImageIdTextBox.Text);
        image.ImageType = EmptyToNull(ImageTypeComboBox.Text);
        image.ColorSpace = EmptyToNull(ColorSpaceComboBox.Text);
        image.Orientation = OrientationComboBox.SelectedItem as string ?? "0";
        image.Offset = ParseOptionalDouble(OffsetTextBox.Text) ?? 0;
        image.Uuid = string.IsNullOrWhiteSpace(UuidTextBox.Text) ? null : Guid.Parse(UuidTextBox.Text);
        double? resolutionX = ParseOptionalDouble(ResolutionXTextBox.Text);
        double? resolutionY = ParseOptionalDouble(ResolutionYTextBox.Text);
        image.Resolution = resolutionX is null && resolutionY is null ? null : new XisfResolution(resolutionX ?? throw new FormatException("Both resolution values are required."), resolutionY ?? throw new FormatException("Both resolution values are required."), ResolutionUnitComboBox.SelectedItem as string ?? "inch");
        image.Properties.Clear();
        AddProperties(image.Properties, _imageProperties);
        image.FitsKeywords.Clear();
        foreach (FitsKeywordEditorRow row in _fitsKeywords.Where(row => !string.IsNullOrWhiteSpace(row.Name))) image.FitsKeywords.Add(new XisfFitsKeyword(row.Name.Trim(), row.Value, EmptyToNull(row.Comment)));
        image.ColorFilterArray = string.IsNullOrWhiteSpace(CfaPatternTextBox.Text) ? null : new XisfColorFilterArray(CfaPatternTextBox.Text.Trim(), int.Parse(CfaWidthTextBox.Text, CultureInfo.InvariantCulture), int.Parse(CfaHeightTextBox.Text, CultureInfo.InvariantCulture), EmptyToNull(CfaNameTextBox.Text));
        double? gamma = ParseOptionalDouble(RgbGammaTextBox.Text);
        image.RgbWorkingSpace = gamma is null ? null : new XisfRgbWorkingSpace(gamma.Value, RgbSrgbCheckBox.IsChecked == true, ParseNumbers(RgbChromaticitiesTextBox.Text, 3), ParseNumbers(RgbLuminanceTextBox.Text, 3));
        image.DisplayFunction = ParseDisplayFunction(DisplayFunctionTextBox.Text);
        image.IccProfile = _iccProfile;
        image.Thumbnail = _thumbnail;
        if (CreateTable() is { } table)
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
        AddProperties(options.Metadata, _documentMetadata);
        AddProperties(options.Properties, _documentProperties);
        return options;
    }

    private static void AddProperties(IDictionary<string, object?> destination, IEnumerable<PropertyEditorRow> rows)
    {
        foreach (PropertyEditorRow row in rows.Where(row => !string.IsNullOrWhiteSpace(row.Id)))
        {
            string type = string.IsNullOrWhiteSpace(row.Type) ? "String" : row.Type.Trim();
            destination[row.Id.Trim()] = new XisfPropertyData(type, ParseProperty(type, row.Value, row.Rows, row.Columns), row.Format, row.Comment);
        }
    }

    private XisfTableData? CreateTable()
    {
        if (string.IsNullOrWhiteSpace(TableIdTextBox.Text)) return null;
        XisfTableData table = new() { Id = TableIdTextBox.Text.Trim(), Caption = EmptyToNull(TableCaptionTextBox.Text) };
        foreach (string line in Lines(TableFieldsTextBox.Text))
        {
            string[] values = line.Split('|');
            if (values.Length < 2) throw new FormatException("Each table field requires at least id|type.");
            table.Fields.Add(new XisfTableFieldData(values[0].Trim(), values[1].Trim(), values.Length > 2 ? EmptyToNull(values[2]) : null, values.Length > 3 ? EmptyToNull(values[3]) : null));
        }
        foreach (string line in Lines(TableRowsTextBox.Text))
        {
            string[] values = line.Split('|');
            if (values.Length != table.Fields.Count) throw new FormatException("Each table row must have one cell per field.");
            table.Rows.Add(values.Select((value, index) => ParseProperty(table.Fields[index].Type, value.Trim())).ToArray());
        }
        return table;
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

    private static object? ParseProperty(string? type, string value, int? rows = null, int? columns = null)
    {
        string normalized = string.IsNullOrWhiteSpace(type) ? "String" : type.Trim();
        string key = normalized.ToLowerInvariant();
        string[] values = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (key == "bytearray") return values.Select(v => byte.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        key = key switch
        {
            "ivector" => "i32vector", "uivector" => "ui32vector", "vector" => "f64vector",
            "bytematrix" => "ui8matrix", "imatrix" => "i32matrix", "uimatrix" => "ui32matrix", "matrix" => "f64matrix",
            _ => key
        };
        if (key.EndsWith("vector") || key.EndsWith("matrix"))
        {
            bool matrix = key.EndsWith("matrix");
            string scalar = key[..^6];
            Array parsed = scalar switch
            {
                "i8" => values.Select(v => sbyte.Parse(v, CultureInfo.InvariantCulture)).ToArray(), "ui8" => values.Select(v => byte.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
                "i16" => values.Select(v => short.Parse(v, CultureInfo.InvariantCulture)).ToArray(), "ui16" => values.Select(v => ushort.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
                "i32" => values.Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray(), "ui32" => values.Select(v => uint.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
                "i64" => values.Select(v => long.Parse(v, CultureInfo.InvariantCulture)).ToArray(), "ui64" => values.Select(v => ulong.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
                "i128" => values.Select(v => Int128.Parse(v, CultureInfo.InvariantCulture)).ToArray(), "ui128" => values.Select(v => UInt128.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
                "f32" => values.Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
                _ => values.Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray()
            };
            if (!matrix) return parsed;
            if (rows is null || columns is null || checked(rows.Value * columns.Value) != parsed.Length)
                throw new FormatException($"Property '{normalized}' requires valid matrix row and column dimensions.");
            Array shaped = Array.CreateInstance(parsed.GetType().GetElementType()!, rows.Value, columns.Value);
            for (int i = 0; i < parsed.Length; i++) shaped.SetValue(parsed.GetValue(i), i / columns.Value, i % columns.Value);
            return shaped;
        }
        return key switch
        {
            "string" => value, "boolean" or "bool" => bool.Parse(value), "int8" => sbyte.Parse(value, CultureInfo.InvariantCulture),
            "uint8" or "byte" => byte.Parse(value, CultureInfo.InvariantCulture), "int16" => short.Parse(value, CultureInfo.InvariantCulture),
            "uint16" => ushort.Parse(value, CultureInfo.InvariantCulture), "int32" or "int" => int.Parse(value, CultureInfo.InvariantCulture),
            "uint32" or "uint" => uint.Parse(value, CultureInfo.InvariantCulture), "int64" => long.Parse(value, CultureInfo.InvariantCulture),
            "uint64" => ulong.Parse(value, CultureInfo.InvariantCulture), "float32" or "float" => float.Parse(value, CultureInfo.InvariantCulture),
            "float64" or "double" => double.Parse(value, CultureInfo.InvariantCulture), "timepoint" => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            _ => value
        };
    }

    private static XisfDisplayFunction? ParseDisplayFunction(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string[] groups = text.Split(';', StringSplitOptions.TrimEntries);
        if (groups.Length != 5) throw new FormatException("A display function requires five semicolon-separated groups.");
        return new XisfDisplayFunction(ParseNumbers(groups[0], 4), ParseNumbers(groups[1], 4), ParseNumbers(groups[2], 4), ParseNumbers(groups[3], 4), ParseNumbers(groups[4], 4));
    }

    private static double[] ParseNumbers(string text, int count)
    {
        double[] values = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        if (values.Length != count) throw new FormatException($"Expected {count} comma-separated values.");
        return values;
    }

    private static double? ParseOptionalDouble(string text) => string.IsNullOrWhiteSpace(text) ? null : double.Parse(text, CultureInfo.InvariantCulture);
    private static string CreateIdentifier(string value)
    {
        string identifier = new(value.Select(character => char.IsAsciiLetterOrDigit(character) || character == '_' ? character : '_').ToArray());
        if (identifier.Length == 0) return "ImportedImage";
        return char.IsAsciiLetter(identifier[0]) || identifier[0] == '_' ? identifier : $"_{identifier}";
    }
    private static IEnumerable<string> Lines(string text) => text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string FormatNumbers(IEnumerable<double>? values) => values is null ? "" : string.Join(", ", values.Select(value => value.ToString("R", CultureInfo.InvariantCulture)));
    private static string FormatEditorValue(object? value) => value is null ? "" : value is string text ? text : value is System.Collections.IEnumerable sequence ? string.Join(", ", sequence.Cast<object?>().Select(FormatEditorValue)) : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    private static string PropertyTypeName(object? value) => value switch { string => "String", bool => "Boolean", DateTimeOffset => "TimePoint", sbyte => "Int8", byte => "UInt8", short => "Int16", ushort => "UInt16", int => "Int32", uint => "UInt32", long => "Int64", ulong => "UInt64", float => "Float32", double => "Float64", _ => "String" };
}

public sealed class PropertyEditorRow
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "String";
    public string Value { get; set; } = "";
    public string? Format { get; set; }
    public string? Comment { get; set; }
    public int? Rows { get; set; }
    public int? Columns { get; set; }
}

public sealed class FitsKeywordEditorRow
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string Comment { get; set; } = "";
}