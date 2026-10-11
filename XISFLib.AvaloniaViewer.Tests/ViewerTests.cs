using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using XISFLib.Viewer;

[assembly: AvaloniaTestApplication(typeof(XISFLib.AvaloniaViewer.Tests.TestAppBuilder))]

namespace XISFLib.AvaloniaViewer.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseSkia().UseHarfBuzz()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public class ViewerTests
{
    [AvaloniaFact]
    public async Task FinishTabHasASeparateReviewedPreviewAndSaveAvailabilityFollowsItsLifecycle()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf"), export = path + ".png";
        try
        {
            XisfWriter.Write(path, [XisfImageData.FromSamples(Enumerable.Range(0, 1024).Select(i => (ushort)(1000 + i * 4)).ToArray(), [32, 32], 1)]);
            var window = new MainWindow(); window.Show();
            try
            {
                var vm = Assert.IsType<WorkbenchViewModel>(window.DataContext);
                vm.FilePath = path; await vm.LoadCommand.ExecuteAsync(null);
                var source = window.FindControl<Image>("ImageView")!.Source;
                var tabs = window.FindControl<TabControl>("WorkbenchTabs")!;
                tabs.SelectedItem = window.FindControl<TabItem>("FinishTab"); window.UpdateLayout();
                var create = window.FindControl<Button>("CreateFinishedButton")!;
                var png = window.FindControl<Button>("SaveFinishedPngButton")!;
                var progressPanel = window.FindControl<StackPanel>("FinishProgressPanel")!;
                var progressBar = window.FindControl<ProgressBar>("FinishProgressBar")!;
                var progressText = window.FindControl<TextBlock>("FinishProgressText")!;
                var cancel = window.FindControl<Button>("CancelFinishedButton")!;
                var sharpen = window.FindControl<CheckBox>("SharpenStarsCheckBox")!;
                var spikes = window.FindControl<CheckBox>("ReduceStarSpikesCheckBox")!;
                Assert.True(sharpen.IsChecked); Assert.True(spikes.IsChecked);
                int visibleOperations = 0;
                vm.Finish.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName != nameof(ViewerFinishState.IsProcessing) || !vm.Finish.IsProcessing) return;
                    window.UpdateLayout();
                    Assert.True(progressPanel.IsVisible); Assert.True(progressBar.IsIndeterminate);
                    Assert.Equal(vm.Finish.ProgressText, progressText.Text);
                    visibleOperations++;
                };
                Assert.False(progressPanel.IsVisible);
                Assert.Same(vm.Finish.CreateCommand, create.Command); Assert.False(png.IsEffectivelyEnabled);
                create.Command!.Execute(null); await vm.Finish.CreateCommand.ExecutionTask!;
                window.UpdateLayout();
                Assert.True(png.IsEffectivelyEnabled);
                Assert.False(progressPanel.IsVisible); Assert.False(cancel.IsEffectivelyEnabled);
                var preview = window.FindControl<Image>("FinishedImageView")!;
                Assert.NotNull(preview.Source); Assert.Same(source, window.FindControl<Image>("ImageView")!.Source);
                Assert.NotSame(source, preview.Source);
                var bitmap = Assert.IsType<WriteableBitmap>(preview.Source); Assert.Equal(new PixelSize(32, 32), bitmap.PixelSize);
                await vm.SaveFinishedAsync(export); window.UpdateLayout();
                Assert.Equal(2, visibleOperations); Assert.False(progressPanel.IsVisible);
                sharpen.IsChecked = false; window.UpdateLayout();
                Assert.False(vm.Finish.SharpenStars); Assert.Null(preview.Source); Assert.False(png.IsEffectivelyEnabled);
                spikes.IsChecked = false;
                Assert.False(vm.Finish.ReduceStarSpikes);
                await vm.UnloadCommand.ExecuteAsync(null);
                Assert.Null(preview.Source); Assert.False(png.IsEffectivelyEnabled);
            }
            finally { window.Close(); }
        }
        finally { File.Delete(path); File.Delete(export); }
    }

    [AvaloniaFact]
    public async Task StatisticsTabBindsQualityCommandAndResultsAndHasFiniteViewports()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf");
        try
        {
            var image = XisfImageData.FromSamples(new ushort[] { 100, 200, 300, 400, 500, 600 }, [2, 1], 3, XisfPixelLayout.Interleaved);
            image.ColorSpace = "RGB";
            XisfWriter.Write(path, [image]);
            var window = new MainWindow(); window.Show();
            try
            {
                var vm = Assert.IsType<WorkbenchViewModel>(window.DataContext);
                vm.FilePath = path; await vm.LoadCommand.ExecuteAsync(null);
                Assert.Equal(1, vm.Quality.Channel);
                var tabs = window.FindControl<TabControl>("WorkbenchTabs")!;
                tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(t => Equals(t.Header, "Statistics"));
                window.UpdateLayout();
                var button = window.FindControl<Button>("AnalyzeQualityButton")!;
                Assert.Same(vm.Quality.AnalyzeCommand, button.Command);
                button.Command!.Execute(null);
                await vm.Quality.AnalyzeCommand.ExecutionTask!;
                window.UpdateLayout();
                Assert.NotEmpty(vm.Quality.Entries);
                var metrics = window.FindControl<DataGrid>("QualityMetricsGrid")!;
                Assert.Same(vm.Quality.Entries, metrics.ItemsSource);
                Assert.Equal(470, metrics.Height);
                Assert.Same(vm.Quality.Statistics, window.FindControl<DataGrid>("QualityChannelsGrid")!.ItemsSource);
                Assert.Equal(3, vm.Quality.Statistics.Count);
                Assert.Contains(metrics.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Analyzed channel");
                await vm.UnloadCommand.ExecuteAsync(null);
                Assert.Empty(vm.Quality.Entries); Assert.False(button.IsEffectivelyEnabled);
            }
            finally { window.Close(); }
        }
        finally { File.Delete(path); }
    }

    [AvaloniaFact]
    public void ClosingPagedEditorDiscardsEditsAndUnchangedAcceptPreservesOriginalArray()
    {
        var owner = new MainWindow(); owner.Show();
        try
        {
            var original = new XisfPropertyData("UI64Vector", Enumerable.Repeat(ulong.MaxValue, 5000).ToArray());
            var row = ViewerEditorCodec.ToEditorRow("Test:Large", original);
            var dialog = new PropertyValueWindow(row, row.Value); dialog.Show(owner); dialog.UpdateLayout();
            Assert.Single(dialog.GetVisualDescendants().OfType<TextBox>()).Text = "discard me";
            dialog.Close();
            Assert.Same(original, ViewerEditorCodec.ParseProperties([row])[row.Id]);
            dialog = new PropertyValueWindow(row, row.Value); dialog.Show(owner); dialog.UpdateLayout();
            Assert.Single(dialog.GetVisualDescendants().OfType<Button>(), b => b.Name == "AcceptValue")
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Same(original, ViewerEditorCodec.ParseProperties([row])[row.Id]);
            dialog.Close();
        }
        finally { owner.Close(); }
    }
    [AvaloniaFact]
    public async Task LargeMetadataUsesBoundedGridAndPagedEditorWithoutLosingText()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf");
        string value = new string('x', 200000) + "\nlast line";
        try
        {
            var image = XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1);
            image.Properties["Test:Large"] = value;
            XisfWriter.Write(path, [image]);
            var window = new MainWindow(); window.Show();
            try
            {
                var vm = Assert.IsType<WorkbenchViewModel>(window.DataContext);
                vm.FilePath = path; await vm.LoadCommand.ExecuteAsync(null);
                window.FindControl<TabControl>("WorkbenchTabs")!.SelectedIndex = 3; window.UpdateLayout();
                var grid = window.FindControl<DataGrid>("ImagePropertiesGrid")!;
                var row = Assert.Single(vm.ImageProperties);
                Assert.DoesNotContain(grid.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Length > 350);
                grid.SelectedItem = row; grid.CurrentColumn = grid.Columns[2]; grid.Focus();
                Assert.False(grid.BeginEdit());
                var dialog = new PropertyValueWindow(row, row.Value); dialog.Show(window); dialog.UpdateLayout();
                try
                {
                    var text = Assert.Single(dialog.GetVisualDescendants().OfType<TextBox>());
                    Assert.Equal(MetadataText.PageSize, text.Text!.Length);
                    text.Text = "changed first page";
                    var next = Assert.Single(dialog.GetVisualDescendants().OfType<Button>(), b => b.Name == "NextPage");
                    next.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(new string('x', MetadataText.PageSize), text.Text);
                    var accept = Assert.Single(dialog.GetVisualDescendants().OfType<Button>(), b => b.Name == "AcceptValue");
                    accept.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                    // Accept assembles the staged pages on a worker.
                    for (int attempt = 0; attempt < 200 && dialog.IsVisible; attempt++)
                    { await Task.Delay(1); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
                }
                finally { dialog.Close(); }
                Assert.Equal("changed first page" + value[MetadataText.PageSize..], row.Value);
                await vm.ApplyMetadataCommand.ExecuteAsync(null);
                Assert.Equal(row.Value, Assert.IsType<XisfPropertyData>(vm.SelectedImage!.Properties[row.Id]).Value);
            }
            finally { window.Close(); }
        }
        finally { File.Delete(path); }
    }
    [AvaloniaFact]
    public async Task SaveCommitsDocumentCellAndUtilitiesBindActualResults()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf"), output = path + ".saved.xisf";
        try
        {
            XisfWriter.Write(path, [XisfImageData.FromSamples(new byte[] { 10, 20 }, [2, 1], 1)]);
            var window = new MainWindow(); window.Show();
            try
            {
                var vm = Assert.IsType<WorkbenchViewModel>(window.DataContext); vm.FilePath = path; vm.AutoStretch = false;
                await vm.LoadCommand.ExecuteAsync(null); vm.PreserveOriginal = false;
                var row = new PropertyEditorRow { Id = "Test:Saved", Value = "old" }; vm.DocumentEditor.Properties.Add(row);
                window.FindControl<TabControl>("WorkbenchTabs")!.SelectedIndex = 4; window.UpdateLayout();
                var grid = window.FindControl<DataGrid>("DocumentPropertiesGrid")!; grid.SelectedItem = row; grid.CurrentColumn = grid.Columns[2];
                grid.Focus(); Assert.True(grid.BeginEdit()); window.UpdateLayout(); Assert.Single(grid.GetVisualDescendants().OfType<TextBox>()).Text = "saved";
                await window.SaveToPathAsync(output);
                Assert.Equal("saved", XisfReader.Load(output).Properties["Test:Saved"].Value);
                Assert.Equal(new byte[] { 10, 20 }, vm.SelectedImage!.GetSamples<byte>());
                window.FindControl<TabControl>("WorkbenchTabs")!.SelectedIndex = 2; window.UpdateLayout();
                var statisticsButton = window.FindControl<Button>("StatisticsButton")!;
                Assert.Same(vm.Utilities.StatisticsCommand, statisticsButton.Command);
                var position = statisticsButton.TranslatePoint(new Point(10, 10), window)!.Value;
                window.MouseDown(position, Avalonia.Input.MouseButton.Left); window.MouseUp(position, Avalonia.Input.MouseButton.Left);
                await vm.Utilities.StatisticsCommand.ExecutionTask!;
                Assert.Same(vm.Utilities.Statistics, window.FindControl<DataGrid>("StatisticsGrid")!.ItemsSource);
                Assert.Equal(15d, Assert.Single(vm.Utilities.Statistics).Mean);
                Assert.Same(vm.Utilities.Overview, window.FindControl<DataGrid>("AstronomyGrid")!.ItemsSource);
                Assert.True(window.FindControl<Button>("SaveButton")!.IsEffectivelyEnabled);
            }
            finally { window.Close(); }
        }
        finally { File.Delete(path); File.Delete(output); }
    }

    [AvaloniaFact]
    public async Task PointerZoomPanAndCaptureReleaseRespectViewport()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf");
        try
        {
            XisfWriter.Write(path, [XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1)]);
            var window = new MainWindow(); window.Show();
            try
            {
                var vm = Assert.IsType<WorkbenchViewModel>(window.DataContext); vm.FilePath = path; await vm.LoadCommand.ExecuteAsync(null);
                var image = window.FindControl<Image>("ImageView")!;
                var group = Assert.IsType<Avalonia.Media.TransformGroup>(image.RenderTransform);
                var scale = Assert.IsType<Avalonia.Media.ScaleTransform>(group.Children[0]);
                var translation = Assert.IsType<Avalonia.Media.TranslateTransform>(group.Children[1]);
                window.MouseWheel(new Point(600, 300), new Vector(0, 1)); Assert.Equal(1.2, scale.ScaleX, 10);
                var before = translation.X;
                window.MouseDown(new Point(600, 300), Avalonia.Input.MouseButton.Left); window.MouseMove(new Point(650, 320), Avalonia.Input.RawInputModifiers.LeftMouseButton);
                Assert.Equal(before + 50, translation.X, 8); window.MouseUp(new Point(650, 320), Avalonia.Input.MouseButton.Left);
                var stopped = translation.X; window.MouseMove(new Point(700, 400)); Assert.Equal(stopped, translation.X);
                for (int i = 0; i < 100; i++) window.MouseWheel(new Point(600, 300), new Vector(0, 1)); Assert.Equal(64, scale.ScaleX);
                for (int i = 0; i < 100; i++) window.MouseWheel(new Point(600, 300), new Vector(0, -1)); Assert.Equal(1, scale.ScaleX);
            }
            finally { window.Close(); }
        }
        finally { File.Delete(path); }
    }
    [AvaloniaFact]
    public async Task DocumentGridCommitsActiveCellAndTableControlsApplyTypedRows()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf");
        try
        {
            XisfWriter.Write(path, [XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1)]);
            var window = new MainWindow(); window.Show();
            try
            {
                var vm = Assert.IsType<WorkbenchViewModel>(window.DataContext);
                vm.FilePath = path; await vm.LoadCommand.ExecuteAsync(null);
                var row = new PropertyEditorRow { Id = "Test:Title", Value = "before" }; vm.DocumentEditor.Properties.Add(row);
                window.FindControl<TabControl>("WorkbenchTabs")!.SelectedIndex = 4; window.UpdateLayout();
                var grid = window.FindControl<DataGrid>("DocumentPropertiesGrid")!;
                grid.SelectedItem = row; grid.CurrentColumn = grid.Columns[2]; grid.Focus(); Assert.True(grid.BeginEdit()); window.UpdateLayout();
                Assert.Single(grid.GetVisualDescendants().OfType<TextBox>()).Text = "after";
                window.FindControl<Button>("ApplyDocumentButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await vm.ApplyDocumentCommand.ExecutionTask!;
                Assert.Equal("after", Assert.IsType<XisfPropertyData>(vm.DocumentEditor.Applied.Properties["Test:Title"]).Value);
                window.FindControl<TabControl>("WorkbenchTabs")!.SelectedIndex = 3;
                window.FindControl<TextBox>("TableIdEditor")!.Text = "stars";
                window.FindControl<TextBox>("TableFieldsEditor")!.Text = "name|String\nflux|Float32";
                window.FindControl<TextBox>("TableRowsEditor")!.Text = "Vega|0.25";
                window.FindControl<Button>("ApplyMetadataButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await vm.ApplyMetadataCommand.ExecutionTask!;
                var table = Assert.Single(vm.SelectedImage!.Tables);
                Assert.Equal("Vega", table.Rows[0][0]); Assert.Equal(.25f, table.Rows[0][1]);
                Assert.Equal(new byte[] { 42 }, vm.SelectedImage.GetSamples<byte>());
            }
            finally { window.Close(); }
        }
        finally { File.Delete(path); }
    }

    [AvaloniaFact]
    public async Task AttributeControlsApplyOrientationAndRejectInvalidCfaWithoutPartialChanges()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf");
        try
        {
            XisfWriter.Write(path, [XisfImageData.FromSamples(new byte[] { 10, 20 }, [2, 1], 1)]);
            var window = new MainWindow(); window.Show();
            try
            {
                var vm = Assert.IsType<WorkbenchViewModel>(window.DataContext);
                vm.AutoStretch = false; vm.FilePath = path; await vm.LoadCommand.ExecuteAsync(null);
                window.FindControl<TabControl>("WorkbenchTabs")!.SelectedIndex = 3;
                window.FindControl<TextBox>("ImageIdEditor")!.Text = "renamed";
                window.FindControl<TextBox>("OrientationEditor")!.Text = "90";
                window.FindControl<Button>("ApplyMetadataButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await vm.ApplyMetadataCommand.ExecutionTask!;
                Assert.Equal("renamed", vm.SelectedImage!.Id);
                var preview = Assert.IsType<WriteableBitmap>(window.FindControl<Image>("ImageView")!.Source);
                Assert.Equal(new PixelSize(1, 2), preview.PixelSize);
                window.FindControl<TextBox>("ImageIdEditor")!.Text = "should_not_apply";
                vm.ImageEditor.CfaPattern = "RGGB"; vm.ImageEditor.CfaHeight = "2";
                window.FindControl<TextBox>("CfaWidthEditor")!.Text = "invalid";
                window.FindControl<Button>("ApplyMetadataButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await vm.ApplyMetadataCommand.ExecutionTask!;
                Assert.Equal("renamed", vm.SelectedImage.Id); Assert.Null(vm.SelectedImage.ColorFilterArray);
                Assert.Same(preview, window.FindControl<Image>("ImageView")!.Source);
            }
            finally { window.Close(); }
        }
        finally { File.Delete(path); }
    }

    [AvaloniaFact]
    public async Task MetadataGridCommitsActiveCellAndBlocksUnsupportedRows()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf");
        try
        {
            var image = XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1);
            image.Properties["Test:Text"] = "before";
            image.Properties["Test:Complex"] = new XisfPropertyData("Complex64", new System.Numerics.Complex(1, 2));
            XisfWriter.Write(path, [image]);
            var window = new MainWindow(); window.Show();
            try
            {
                var vm = Assert.IsType<WorkbenchViewModel>(window.DataContext);
                vm.FilePath = path; await vm.LoadCommand.ExecuteAsync(null);
                window.FindControl<TabControl>("WorkbenchTabs")!.SelectedIndex = 3;
                window.UpdateLayout();
                var grid = window.FindControl<DataGrid>("ImagePropertiesGrid")!;
                grid.SelectedItem = Assert.Single(vm.ImageProperties, r => r.Id == "Test:Complex");
                grid.CurrentColumn = grid.Columns[2]; grid.Focus();
                Assert.False(grid.BeginEdit());
                grid.SelectedItem = Assert.Single(vm.ImageProperties, r => r.Id == "Test:Text");
                Assert.True(grid.BeginEdit()); window.UpdateLayout();
                var editor = Assert.Single(grid.GetVisualDescendants().OfType<TextBox>());
                editor.Text = "after";
                window.FindControl<Button>("ApplyMetadataButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                if (vm.ApplyMetadataCommand.ExecutionTask is { } operation) await operation;
                Assert.StartsWith("Metadata applied", vm.Status);
                var property = Assert.IsType<XisfPropertyData>(vm.SelectedImage!.Properties["Test:Text"]);
                Assert.Equal("after", property.Value);
                Assert.Equal(new byte[] { 42 }, vm.SelectedImage.GetSamples<byte>());
            }
            finally { window.Close(); }
        }
        finally { File.Delete(path); }
    }

    [AvaloniaFact]
    public async Task InformationTreeKeepsLargeBranchesCollapsedAndSupportsExpansion()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf");
        try
        {
            var image = XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1);
            for (int i = 0; i < 2000; i++) image.Properties[$"Test:Value{i}"] = i;
            XisfWriter.Write(path, [image]);
            var window = new MainWindow();
            window.Show();
            try
            {
                var vm = Assert.IsType<WorkbenchViewModel>(window.DataContext);
                vm.FilePath = path;
                await vm.LoadCommand.ExecuteAsync(null);
                window.FindControl<TabControl>("WorkbenchTabs")!.SelectedIndex = 1;
                window.UpdateLayout();
                var tree = window.FindControl<TreeView>("MetadataTree")!;
                var containers = tree.GetVisualDescendants().OfType<TreeViewItem>().ToArray();
                Assert.Contains(containers, c => c.DataContext is MetadataNode { Name: "File" } && c.IsExpanded);
                Assert.Contains(containers, c => c.DataContext is MetadataNode { Name: "Document" } && c.IsExpanded);
                var imagesNode = Assert.Single(vm.Metadata, n => n.Name == "Images");
                var images = Assert.Single(containers, c => ReferenceEquals(c.DataContext, imagesNode));
                Assert.False(images.IsExpanded);
                Assert.True(containers.Length < 100, $"Collapsed metadata created {containers.Length} containers.");
                images.IsExpanded = true;
                window.UpdateLayout();
                Assert.Contains(tree.GetVisualDescendants().OfType<TreeViewItem>(),
                    c => c.DataContext is MetadataNode n && n.Name.StartsWith("Image 1:"));
            }
            finally { window.Close(); }
        }
        finally { File.Delete(path); }
    }

    [AvaloniaFact]
    public void FramebufferUploadPreservesStraightAlphaBgraDpiAndOrientation()
    {
        var image = XisfImageData.FromSamples(new byte[] { 10, 20, 30, 64, 40, 50, 60, 128 }, [2, 1], 4, XisfPixelLayout.Interleaved);
        image.ColorSpace = "RGB"; image.Orientation = "90"; image.Resolution = new(100, 150, "inch");
        var rendered = XisfDisplayRenderer.Render(image);
        using var bitmap = AvaloniaBitmapAdapter.Create(rendered);
        Assert.Equal(new PixelSize(1, 2), bitmap.PixelSize);
        Assert.Equal(new Vector(100, 150), bitmap.Dpi);
        Assert.Equal(AlphaFormat.Unpremul, bitmap.AlphaFormat);
        using var framebuffer = bitmap.Lock();
        var bytes = new byte[rendered.Pixels.Length];
        for (int row = 0; row < rendered.Height; row++)
            Marshal.Copy(framebuffer.Address + row * framebuffer.RowBytes, bytes, row * rendered.Stride, rendered.Stride);
        Assert.Equal(rendered.Pixels, bytes);
    }

    [AvaloniaFact]
    public async Task WindowLoadsXisfThroughSharedCommandsAndClearsPreviewOnUnload()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf");
        try
        {
            XisfWriter.Write(path, [XisfImageData.FromSamples(new byte[] { 127 }, [1, 1], 1)]);
            var window = new MainWindow();
            window.Show();
            try
            {
                var vm = Assert.IsType<WorkbenchViewModel>(window.DataContext);
                vm.FilePath = path;
                await vm.LoadCommand.ExecuteAsync(null);
                Assert.StartsWith("Loaded ", vm.Status);
                Assert.IsType<WriteableBitmap>(window.FindControl<Image>("ImageView")!.Source);
                Assert.False(vm.IsBusy);
                var tree = window.FindControl<TreeView>("MetadataTree")!;
                Assert.Same(vm.Metadata, tree.ItemsSource);
                Assert.Equal(7, vm.Metadata.Count);
                await vm.ValidateCommand.ExecuteAsync(null);
                Assert.StartsWith("Validation complete", vm.Status);
                await vm.UnloadCommand.ExecuteAsync(null);
                Assert.Null(window.FindControl<Image>("ImageView")!.Source);
                Assert.Empty(tree.ItemsSource!);
            }
            finally { window.Close(); }
        }
        finally { File.Delete(path); }
    }
}
