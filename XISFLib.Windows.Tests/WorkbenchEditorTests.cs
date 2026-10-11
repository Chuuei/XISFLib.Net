using System.Runtime.ExceptionServices;
using System.IO;
using TextBox = System.Windows.Controls.TextBox;
using DataGrid = System.Windows.Controls.DataGrid;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
using Size = System.Windows.Size;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using XISFLib.Viewer;
using XISFLib.WpfViewer;

namespace XISFLib.Windows.Tests;

public class WorkbenchEditorTests
{
    [Fact]
    public void WpfLargeMetadataGridHasBoundedRowsAndPreservesFullValue() => RunOnSta(async window =>
    {
        var image = XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1);
        string value = new('x', 200000); image.Properties["Test:Large"] = value;
        for (int i = 0; i < 1000; i++) image.Properties[$"Test:Value{i}"] = i;
        await WithFile(window, image, async (vm, path) =>
        {
            ((TabItem)window.FindName("ImageEditorTab")).IsSelected = true;
            window.Measure(new Size(1450, 900)); window.Arrange(new Rect(0, 0, 1450, 900)); window.UpdateLayout();
            var grid = (DataGrid)window.FindName("ImagePropertiesGrid");
            Assert.Equal(260, grid.Height); Assert.Equal(28, grid.RowHeight); Assert.True(grid.EnableRowVirtualization);
            Assert.Equal("InlineValue", ((System.Windows.Data.Binding)((DataGridTextColumn)grid.Columns[2]).Binding).Path.Path);
            var row = Assert.IsType<PropertyEditorRow>(grid.Items[0]);
            Assert.True(row.InlineValue.Length < 350); Assert.Equal(value, row.Value);
            Assert.Null(grid.ItemContainerGenerator.ContainerFromIndex(900));
            vm.PreserveOriginal = false; await window.SaveToPathAsync(path + ".saved.xisf");
            Assert.Equal(value, XisfReader.Load(path + ".saved.xisf").Images[0].Properties[row.Id].Value);
        });
    });

    [Theory]
    [InlineData("UuidTextBox", "not-a-guid")]
    [InlineData("CfaWidthTextBox", "not-an-integer")]
    [InlineData("TableFieldsTextBox", "missing-field-type")]
    public void WpfParsesAllEditorFieldsBeforeChangingTheImage(string control, string invalid) => RunOnSta(async window =>
    {
        var image = XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1);
        image.Id = "original"; image.Properties["Test:Original"] = "retained";
        await WithFile(window, image, async (vm, path) =>
        {
            var working = vm.SelectedImage!;
            Text(window, "ImageIdTextBox", "changed"); Text(window, "CfaPatternTextBox", "RGGB");
            Text(window, "CfaWidthTextBox", "2"); Text(window, "CfaHeightTextBox", "2");
            Text(window, "TableIdTextBox", "stars"); Text(window, "TableFieldsTextBox", "name|String");
            Text(window, control, invalid); vm.PreserveOriginal = false;
            await window.SaveToPathAsync(path + ".saved.xisf");
            Assert.False(File.Exists(path + ".saved.xisf"));
            Assert.Equal("original", working.Id);
            Assert.Equal("retained", Assert.IsType<XisfPropertyData>(working.Properties["Test:Original"]).Value);
            Assert.Null(working.ColorFilterArray); Assert.Empty(working.Tables);
            Assert.Equal(new byte[] { 42 }, working.GetSamples<byte>());
        });
    });

    [Fact]
    public void WpfUsesSharedUtilityCommandsAndCommitsActiveDocumentCellOnSave() => RunOnSta(async window =>
    {
        var image = XisfImageData.FromSamples(new byte[] { 10, 20 }, [2, 1], 1);
        await WithFile(window, image, async (vm, path) =>
        {
            ((TabItem)window.FindName("WriterTab")).IsSelected = true;
            vm.DocumentEditor.Properties.Add(new() { Id = "Test:Saved", Value = "before" });
            window.Measure(new Size(1450, 900)); window.Arrange(new Rect(0, 0, 1450, 900)); window.UpdateLayout();
            var grid = (DataGrid)window.FindName("DocumentPropertiesGrid");
            grid.SelectedItem = vm.DocumentEditor.Properties[0]; grid.CurrentColumn = grid.Columns[2];
            grid.CurrentCell = new DataGridCellInfo(grid.SelectedItem, grid.Columns[2]); grid.Focus();
            Assert.True(grid.BeginEdit()); window.UpdateLayout();
            var cell = grid.Columns[2].GetCellContent(grid.SelectedItem);
            Assert.IsType<TextBox>(cell).Text = "saved";
            vm.PreserveOriginal = false; await window.SaveToPathAsync(path + ".saved.xisf");
            Assert.Equal("saved", XisfReader.Load(path + ".saved.xisf").Properties["Test:Saved"].Value);
            var button = (Button)window.FindName("CalculateStatisticsButton");
            Assert.Same(vm.Utilities.StatisticsCommand, button.Command);
            await vm.Utilities.StatisticsCommand.ExecuteAsync(null);
            Assert.Equal(15, Assert.Single(vm.Utilities.Statistics).Mean);
            Assert.Same(vm.Utilities.Statistics, ((DataGrid)window.FindName("StatisticsGrid")).ItemsSource);
        });
    });

    [Fact]
    public void WpfByteCopySkipsInvalidDraftsAndFailedLoadKeepsSession() => RunOnSta(async window =>
    {
        await WithFile(window, XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1), async (vm, path) =>
        {
            Text(window, "UuidTextBox", "invalid draft");
            await window.SaveToPathAsync(path + ".saved.xisf");
            Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(path + ".saved.xisf"));
            var original = vm.SelectedImage; var bitmap = ((Image)window.FindName("ImageView")).Source;
            vm.FilePath = path + ".missing"; await vm.LoadCommand.ExecuteAsync(null);
            Assert.Same(original, vm.SelectedImage); Assert.Same(bitmap, ((Image)window.FindName("ImageView")).Source);
            Assert.False(vm.IsBusy);
        });
    });

    private static async Task WithFile(MainWindow window, XisfImageData image, Func<WorkbenchViewModel, string, Task> action)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf");
        try
        {
            XisfWriter.Write(path, [image]); var vm = Assert.IsType<WorkbenchViewModel>(window.DataContext);
            vm.FilePath = path; await vm.LoadCommand.ExecuteAsync(null);
            Assert.StartsWith("Loaded ", vm.Status); await action(vm, path);
        }
        finally { File.Delete(path); File.Delete(path + ".saved.xisf"); }
    }
    private static void RunOnSta(Func<MainWindow, Task> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(async () =>
            {
                var window = new MainWindow();
                window.ShowInTaskbar = false; window.Opacity = 0; window.Show();
                try { await action(window); }
                catch (Exception error) { failure = error; }
                finally { window.Close(); frame.Continue = false; }
            });
            Dispatcher.PushFrame(frame);
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private static void Text(MainWindow window, string name, string value) => ((TextBox)window.FindName(name)).Text = value;
}
