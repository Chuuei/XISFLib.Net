using System.Collections;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Linq;
using Microsoft.Win32;

namespace XISFLib.WpfViewer;

public partial class MainWindow : Window
{
    private const double MinimumImageZoom = 1;
    private const double MaximumImageZoom = 64;
    private const double ImageZoomStep = 1.2;
    private XisfDocument? _document;
    private FitsDocument? _fitsDocument;
    private XisfImageData?[] _editableImages = [];
    private bool _isBusy;
    private bool _isPanning;
    private Point _panStart;
    private Vector _panOrigin;
    private double _imageZoom = 1;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeWorkbench();
        UpdateStretchControls();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Select an XISF or FITS image",
            Filter = "Astronomy images (*.xisf;*.xish;*.fit;*.fits;*.fts)|*.xisf;*.xish;*.fit;*.fits;*.fts|XISF images (*.xisf;*.xish)|*.xisf;*.xish|FITS images (*.fit;*.fits;*.fts)|*.fit;*.fits;*.fts|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
            FilePathTextBox.Text = dialog.FileName;
    }

    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        string path = FilePathTextBox.Text.Trim();
        if (path.Length == 0)
        {
            ShowError("Select an XISF or FITS file first.");
            return;
        }

        await RunBusyAsync("Loading astronomy image…", async () =>
        {
            LoadedFile loaded = await Task.Run(() => LoadFile(path));
            bool fits = loaded.Fits is not null;
            _document = loaded.Xisf;
            _fitsDocument = loaded.Fits;
            _editableImages = loaded.Images;
            bool monolithic = !fits && string.Equals(Path.GetExtension(path), ".xisf", StringComparison.OrdinalIgnoreCase);
            _sourceXisfPath = monolithic ? Path.GetFullPath(path) : null;
            PreserveOriginalCheckBox.IsEnabled = monolithic;
            PreserveOriginalCheckBox.IsChecked = monolithic;
            ImageSelector.ItemsSource = Enumerable.Range(0, _editableImages.Length).Select(GetImageLabel).ToArray();
            ImageSelector.SelectedIndex = 0;
            _editableImage = _editableImages[0]!;
            _editorImageIndex = 0;
            ResetImageViewport();
            await RenderSelectedImageAsync();
            // Let WPF process its render queue before preparing the remaining tabs.
            await Dispatcher.Yield(DispatcherPriority.Background);
            StatusText.Text = "Image ready. Preparing metadata and validation…";
            var details = await Task.Run(() =>
            {
                IReadOnlyList<MetadataNode> metadata = loaded.Xisf is not null ? BuildMetadata(loaded.Xisf, path) : BuildFitsMetadata(loaded.Fits!, path);
                IReadOnlyList<XisfDiagnostic>? diagnostics = loaded.Xisf?.Validate();
                return (Metadata: metadata, Diagnostics: diagnostics);
            });
            if (_document is not null)
                PopulateLoadedDocumentEditors(_document);
            else
                ClearDocumentEditors();
            MetadataTree.ItemsSource = details.Metadata;
            MetadataTree.Visibility = Visibility.Visible;
            EmptyMetadataText.Visibility = Visibility.Collapsed;
            await LoadImageEditorAsync(0);
            RefreshUtilityOverview();
            if (details.Diagnostics is not null) ShowDiagnostics(details.Diagnostics);
            else ShowDiagnostics(loaded.Fits!.Diagnostics);
            StatusText.Text = $"Loaded {Path.GetFileName(path)} ({_editableImages.Length} image(s){(fits ? ", converted from FITS" : "")}).";
        });
    }

    private static bool IsFitsPath(string path) => Path.GetExtension(path).ToLowerInvariant() is ".fit" or ".fits" or ".fts";

    private static LoadedFile LoadFile(string path)
    {
        if (IsFitsPath(path))
        {
            FitsDocument fits = FitsReader.Load(path);
            IReadOnlyList<XisfImageData> converted = FitsConverter.ToXisf(fits, new() { AxisInterpretation = FitsAxisInterpretation.RgbOrRgba, LossPolicy = ConversionLossPolicy.Allow });
            if (converted.Count == 0) throw new FitsFormatException("The FITS document does not contain a convertible image HDU.");
            return new LoadedFile(null, fits, converted.Select(image => (XisfImageData?)image).ToArray());
        }

        XisfDocument xisf = XisfReader.Load(path);
        if (xisf.Images.Count == 0) throw new XisfFormatException("The XISF document does not contain an image.");
        XisfImageData?[] images = new XisfImageData?[xisf.Images.Count];
        images[0] = XisfImageData.FromImage(xisf.Images[0]);
        return new LoadedFile(xisf, null, images);
    }

    private string GetImageLabel(int index) => $"{index + 1}: {(_document?.Images[index].Id ?? _editableImages[index]?.Id) ?? "unnamed image"}";

    private async void ImageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_editableImages.Length > 0 && !_isBusy && ImageSelector.SelectedIndex >= 0)
        {
            int index = ImageSelector.SelectedIndex;
            await RunBusyAsync("Rendering image…", async () =>
            {
                if (index != _editorImageIndex)
                {
                    await LoadImageEditorAsync(index);
                    RefreshUtilityOverview();
                    ResetImageViewport();
                }
                await RenderSelectedImageAsync();
            });
        }
    }

    private void ImageViewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ImageView.Source is null)
            return;

        double factor = e.Delta > 0 ? ImageZoomStep : 1 / ImageZoomStep;
        double newZoom = Math.Clamp(_imageZoom * factor, MinimumImageZoom, MaximumImageZoom);
        if (Math.Abs(newZoom - _imageZoom) < double.Epsilon)
            return;

        Point pointer = e.GetPosition(ImageViewport);
        double ratio = newZoom / _imageZoom;
        ImageTranslateTransform.X = pointer.X - (pointer.X - ImageTranslateTransform.X) * ratio;
        ImageTranslateTransform.Y = pointer.Y - (pointer.Y - ImageTranslateTransform.Y) * ratio;
        _imageZoom = newZoom;
        ImageScaleTransform.ScaleX = newZoom;
        ImageScaleTransform.ScaleY = newZoom;
        e.Handled = true;
    }

    private void ImageViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ImageView.Source is null)
            return;

        _isPanning = true;
        _panStart = e.GetPosition(ImageViewport);
        _panOrigin = new Vector(ImageTranslateTransform.X, ImageTranslateTransform.Y);
        ImageViewport.Cursor = Cursors.SizeAll;
        ImageViewport.CaptureMouse();
        e.Handled = true;
    }

    private void ImageViewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning || e.LeftButton != MouseButtonState.Pressed)
            return;

        Vector movement = e.GetPosition(ImageViewport) - _panStart;
        ImageTranslateTransform.X = _panOrigin.X + movement.X;
        ImageTranslateTransform.Y = _panOrigin.Y + movement.Y;
        e.Handled = true;
    }

    private void ImageViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning)
            return;

        ImageViewport.ReleaseMouseCapture();
        EndImagePan();
        e.Handled = true;
    }

    private void ImageViewport_LostMouseCapture(object sender, MouseEventArgs e) => EndImagePan();

    private void EndImagePan()
    {
        _isPanning = false;
        ImageViewport.Cursor = Cursors.Hand;
    }

    private void ResetImageViewport()
    {
        _imageZoom = 1;
        ImageScaleTransform.ScaleX = 1;
        ImageScaleTransform.ScaleY = 1;
        ImageTranslateTransform.X = 0;
        ImageTranslateTransform.Y = 0;
        EndImagePan();
    }

    private async void StretchSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        UpdateStretchControls();
        if (_editableImage is not null && !_isBusy)
            await RunBusyAsync("Applying display settings…", RenderSelectedImageAsync);
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editableImage is not null)
            await RunBusyAsync("Applying display settings…", RenderSelectedImageAsync);
    }

    private async void StretchValue_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_editableImage is not null && AutoStretchCheckBox.IsChecked == true && !_isBusy)
            await RunBusyAsync("Applying display settings…", RenderSelectedImageAsync);
    }

    private void StretchValue_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ApplyButton.Focus();
        }
    }

    private async Task RenderSelectedImageAsync()
    {
        if (_editableImage is null || ImageSelector.SelectedIndex < 0)
            return;

        XisfStretchOptions? stretch = CreateStretchOptions();
        XisfImageData image = _editableImage;
        ImageView.Source = await Task.Run(() => image.ToBitmapSource(stretch));
        EmptyViewText.Visibility = Visibility.Collapsed;
        ImageDetailsText.Text = string.Format(CultureInfo.CurrentCulture,
            "{0} × {1}, {2} channel(s), {3}, {4}",
            _editableImage.Dimensions[0], _editableImage.Dimensions[1], _editableImage.ChannelCount, _editableImage.SampleFormat,
            _editableImage.ColorSpace ?? "unspecified color space");
    }

    private XisfStretchOptions? CreateStretchOptions()
    {
        if (AutoStretchCheckBox.IsChecked != true)
            return null;

        double target = ParseNumber(TargetBackgroundTextBox.Text, "Target background");
        double clipping = ParseNumber(ShadowsClippingTextBox.Text, "Shadows clipping");
        if (target is <= 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(target), "Target background must be between zero and one.");
        if (clipping > 0)
            throw new ArgumentOutOfRangeException(nameof(clipping), "Shadows clipping must be zero or negative.");

        return new XisfStretchOptions
        {
            TargetBackground = target,
            ShadowsClipping = clipping,
            LinkChannels = LinkChannelsCheckBox.IsChecked == true
        };
    }

    private static double ParseNumber(string text, string name)
    {
        const NumberStyles style = NumberStyles.Float;
        if (double.TryParse(text, style, CultureInfo.CurrentCulture, out double value) ||
            double.TryParse(text, style, CultureInfo.InvariantCulture, out value))
            return value;
        throw new FormatException($"{name} is not a valid number.");
    }

    private void UpdateStretchControls()
    {
        bool enabled = AutoStretchCheckBox.IsChecked == true;
        LinkChannelsCheckBox.IsEnabled = enabled;
        TargetBackgroundTextBox.IsEnabled = enabled;
        ShadowsClippingTextBox.IsEnabled = enabled;
    }

    private static IReadOnlyList<MetadataNode> BuildMetadata(XisfDocument document, string path)
    {
        FileInfo file = new(path);
        List<MetadataNode> roots =
        [
            new("File", children:
            [
                new("Name", file.Name),
                new("Folder", file.DirectoryName ?? string.Empty),
                new("Size", $"{file.Length:N0} bytes"),
                new("Last modified", file.LastWriteTime.ToString("G", CultureInfo.CurrentCulture))
            ], isExpanded: true),
            new("Document", children:
            [
                new("XISF version", document.Version),
                new("Images", document.Images.Count.ToString(CultureInfo.CurrentCulture)),
                new("Tables", document.Tables.Count.ToString(CultureInfo.CurrentCulture)),
                new("XML signature", document.HasXmlSignature ? "Present" : "Not present")
            ], isExpanded: true),
            BuildPropertiesNode("Metadata", document.Metadata),
            BuildPropertiesNode("Document properties", document.Properties),
            BuildTablesNode(document.Tables),
            new("Images", children: document.Images.Select((image, index) => BuildImageNode(image, index + 1)).ToArray()),
            new("Complete XISF header", children: [BuildXmlNode(document.Xml.Root!)])
        ];

        return roots;
    }

    private static IReadOnlyList<MetadataNode> BuildFitsMetadata(FitsDocument document, string path)
    {
        FileInfo file = new(path);
        return
        [
            new("File", children:
            [
                new("Name", file.Name),
                new("Folder", file.DirectoryName ?? string.Empty),
                new("Size", $"{file.Length:N0} bytes"),
                new("Last modified", file.LastWriteTime.ToString("G", CultureInfo.CurrentCulture))
            ], isExpanded: true),
            new("Document", children:
            [
                new("Format", "FITS"),
                new("Header/data units", document.Hdus.Count.ToString(CultureInfo.CurrentCulture)),
                new("Images", document.Hdus.Count(hdu => hdu.Content is FitsImage).ToString(CultureInfo.CurrentCulture))
            ], isExpanded: true),
            new("Header/data units", children: document.Hdus.Select((hdu, index) => new MetadataNode(
                $"HDU {index + 1}: {hdu.Type}",
                children: hdu.Header.Select(card => new MetadataNode(card.Keyword, FormatValue(card.Value),
                    card.Comment is null ? null : [new("Comment", card.Comment)])).ToArray())).ToArray())
        ];
    }

    private static MetadataNode BuildImageNode(XisfImage image, int number)
    {
        List<MetadataNode> details =
        [
            new("Identifier", image.Id ?? "Not specified"),
            new("Dimensions", string.Join(" × ", image.Dimensions)),
            new("Channels", image.ChannelCount.ToString(CultureInfo.CurrentCulture)),
            new("Pixels per channel", image.PixelCount.ToString("N0", CultureInfo.CurrentCulture)),
            new("Sample format", image.SampleFormat.ToString()),
            new("Pixel storage", image.PixelStorage.ToString()),
            new("Byte order", image.IsBigEndian ? "Big-endian" : "Little-endian"),
            new("Color space", image.ColorSpace ?? "Not specified"),
            new("Image type", image.ImageType ?? "Not specified"),
            new("Offset / pedestal", FormatValue(image.Offset)),
            new("Orientation", image.Orientation),
            new("UUID", image.Uuid?.ToString("D") ?? "Not specified"),
            new("Sample bounds", $"{FormatValue(image.LowerBound)} to {FormatValue(image.UpperBound)}"),
            BuildPropertiesNode("Properties", image.Properties),
            BuildTablesNode(image.Tables),
            new("FITS keywords", children: image.FitsKeywords.Select(keyword =>
                new MetadataNode(keyword.Name, keyword.Value,
                    keyword.Comment is null ? null : [new("Comment", keyword.Comment)])).ToArray())
        ];

        if (image.Resolution is { } resolution)
            details.Add(new("Resolution", $"{FormatValue(resolution.Horizontal)} × {FormatValue(resolution.Vertical)} {resolution.Unit}"));
        if (image.ColorFilterArray is { } cfa)
            details.Add(new("Color filter array", $"{cfa.Pattern} ({cfa.Width} × {cfa.Height})",
                cfa.Name is null ? null : [new("Name", cfa.Name)]));
        if (image.RgbWorkingSpace is { } rgb)
            details.Add(new("RGB working space", children:
            [
                new("Transfer function", rgb.IsSrgb ? "sRGB" : $"Gamma {FormatValue(rgb.Gamma)}"),
                new("Chromaticity coordinates", FormatValue(rgb.ChromaticityCoordinates)),
                new("Luminance coefficients", FormatValue(rgb.LuminanceCoefficients))
            ]));
        if (image.DisplayFunction is { } display)
            details.Add(new("Display function", children:
            [
                new("Midtones", FormatValue(display.Midtones)),
                new("Shadows", FormatValue(display.Shadows)),
                new("Highlights", FormatValue(display.Highlights)),
                new("Low range", FormatValue(display.LowRange)),
                new("High range", FormatValue(display.HighRange))
            ]));
        if (image.IccProfile is { } profile)
            details.Add(new("ICC profile", $"{profile.Length:N0} bytes"));
        if (image.Thumbnail is { } thumbnail)
            details.Add(new("Thumbnail", children: BuildImageNode(thumbnail, 0).Children));

        return new($"Image {number}: {image.Id ?? "unnamed"}", children: details);
    }

    private static MetadataNode BuildPropertiesNode(string name, IReadOnlyDictionary<string, XisfProperty> properties) =>
        new(name, properties.Count == 0 ? "None" : null,
            properties.Select(pair => new MetadataNode(pair.Key, FormatValue(pair.Value.Value),
                new MetadataNode?[]
                {
                    pair.Value.Type.Length == 0 ? null : new("Type", pair.Value.Type),
                    pair.Value.Format is null ? null : new("Format", pair.Value.Format),
                    pair.Value.Comment is null ? null : new("Comment", pair.Value.Comment)
                }.OfType<MetadataNode>().ToArray())).ToArray());

    private static MetadataNode BuildTablesNode(IReadOnlyList<XisfTable> tables) =>
        new("Tables", tables.Count == 0 ? "None" : null, tables.Select(table =>
            new MetadataNode(table.Id, table.Caption, children:
            [
                new("Fields", children: table.Fields.Select(field => new MetadataNode(field.Id, field.Type,
                    new MetadataNode?[]
                    {
                        field.Header is null ? null : new("Header", field.Header),
                        field.Format is null ? null : new("Format", field.Format)
                    }.OfType<MetadataNode>().ToArray())).ToArray()),
                new("Rows", children: table.Rows.Select((row, index) => new MetadataNode($"Row {index + 1}", children:
                    row.Cells.Select((cell, cellIndex) => new MetadataNode(
                        cellIndex < table.Fields.Count ? table.Fields[cellIndex].Id : $"Cell {cellIndex + 1}",
                        FormatValue(cell.Value))).ToArray())).ToArray())
            ])).ToArray());

    private static MetadataNode BuildXmlNode(XElement element)
    {
        List<MetadataNode> children = element.Attributes()
            .Select(attribute => new MetadataNode(FriendlyName(attribute.Name.LocalName), attribute.Value))
            .ToList();
        children.AddRange(element.Elements().Select(BuildXmlNode));

        string? value = null;
        if (!element.HasElements)
        {
            string text = element.Value.Trim();
            if (text.Length > 0)
                value = IsEmbeddedData(element, text) ? $"Embedded data ({text.Length:N0} characters)" : text;
        }

        return new(FriendlyName(element.Name.LocalName), value, children);
    }

    private static bool IsEmbeddedData(XElement element, string value) =>
        value.Length > 512 && (element.AncestorsAndSelf().Attributes("location")
            .Any(attribute => attribute.Value.StartsWith("inline:", StringComparison.OrdinalIgnoreCase)) ||
            element.Name.LocalName.Equals("Data", StringComparison.OrdinalIgnoreCase));

    private static string FriendlyName(string name)
    {
        if (name.Length == 0)
            return name;

        return string.Concat(name.Select((character, index) =>
            index > 0 && char.IsUpper(character) && !char.IsUpper(name[index - 1]) ? $" {char.ToLowerInvariant(character)}" : character.ToString()));
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
            return "Not specified";
        if (value is byte[] bytes)
            return $"{bytes.Length:N0} bytes";
        if (value is string text)
            return text;
        if (value is IEnumerable values)
            return string.Join(", ", values.Cast<object?>().Select(FormatValue));
        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.CurrentCulture) ?? string.Empty;
        return value.ToString() ?? string.Empty;
    }

    private async Task RunBusyAsync(string status, Func<Task> action)
    {
        if (_isBusy)
            return;

        _isBusy = true;
        LoadButton.IsEnabled = false;
        UnloadButton.IsEnabled = false;
        ImportButton.IsEnabled = false;
        SaveButton.IsEnabled = false;
        ApplyButton.IsEnabled = false;
        ImageSelector.IsEnabled = false;
        ImageEditorTab.IsEnabled = false;
        WriterTab.IsEnabled = false;
        UtilitiesTab.IsEnabled = false;
        StatusText.Text = status;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            LoadButton.IsEnabled = true;
            UnloadButton.IsEnabled = true;
            ImportButton.IsEnabled = true;
            SaveButton.IsEnabled = true;
            ApplyButton.IsEnabled = true;
            ImageSelector.IsEnabled = _editableImages.Length > 1;
            ImageEditorTab.IsEnabled = true;
            WriterTab.IsEnabled = true;
            UtilitiesTab.IsEnabled = true;
            _isBusy = false;
        }
    }

    private void ShowError(string message)
    {
        StatusText.Text = message;
        MessageBox.Show(this, message, "XISF Library Workbench", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

internal sealed record LoadedFile(XisfDocument? Xisf, FitsDocument? Fits, XisfImageData?[] Images);

public sealed class MetadataNode
{
    public MetadataNode(string name, string? value = null, IReadOnlyList<MetadataNode>? children = null,
        bool isExpanded = false)
    {
        Name = name;
        Value = value;
        Children = children ?? Array.Empty<MetadataNode>();
        IsExpanded = isExpanded;
    }

    public string Name { get; }
    public string? Value { get; }
    public IReadOnlyList<MetadataNode> Children { get; }
    public bool IsExpanded { get; }
}
