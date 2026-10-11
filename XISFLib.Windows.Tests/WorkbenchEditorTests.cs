using System.Reflection;
using System.Runtime.ExceptionServices;
using TextBox = System.Windows.Controls.TextBox;
using XISFLib.WpfViewer;

namespace XISFLib.Windows.Tests;

public class WorkbenchEditorTests
{
    [Fact]
    public void WpfLargeMetadataGridHasBoundedRowsAndPreservesFullValue()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new MainWindow();
                try
                {
                    Invoke(window, "InitializeWorkbench");
                    var image = XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1);
                    string value = new('x', 200000);
                    image.Properties["Test:Large"] = value;
                    for (int i = 0; i < 1000; i++) image.Properties[$"Test:Value{i}"] = i;
                    Invoke(window, "PopulateImageEditor", image);
                    var tab = (System.Windows.Controls.TabItem)window.FindName("ImageEditorTab"); tab.IsSelected = true;
                    window.Measure(new System.Windows.Size(1450, 900)); window.Arrange(new System.Windows.Rect(0, 0, 1450, 900)); window.UpdateLayout();
                    var grid = (System.Windows.Controls.DataGrid)window.FindName("ImagePropertiesGrid");
                    Assert.Equal(260, grid.Height); Assert.Equal(28, grid.RowHeight);
                    Assert.True(grid.EnableRowVirtualization);
                    Assert.Equal("InlineValue", ((System.Windows.Data.Binding)((System.Windows.Controls.DataGridTextColumn)grid.Columns[2]).Binding).Path.Path);
                    var row = Assert.IsType<XISFLib.Viewer.PropertyEditorRow>(grid.Items[0]);
                    Assert.True(row.InlineValue.Length < 350); Assert.Equal(value, row.Value);
                    Assert.Null(grid.ItemContainerGenerator.ContainerFromIndex(900));
                    Invoke(window, "ApplyImageEditor", image);
                    Assert.Equal(value, Assert.IsType<XisfPropertyData>(image.Properties[row.Id]).Value);
                }
                finally { window.Close(); }
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    [Theory]
    [InlineData("UuidTextBox", "not-a-guid")]
    [InlineData("CfaWidthTextBox", "not-an-integer")]
    [InlineData("TableFieldsTextBox", "missing-field-type")]
    public void WpfParsesAllEditorFieldsBeforeChangingTheImage(string control, string invalid)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new MainWindow();
                try
                {
                    Invoke(window, "InitializeWorkbench");
                    var image = XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1);
                    image.Id = "original"; image.Properties["Test:Original"] = "retained";
                    Invoke(window, "PopulateImageEditor", image);
                    Text(window, "ImageIdTextBox", "changed");
                    Text(window, "CfaPatternTextBox", "RGGB");
                    Text(window, "CfaWidthTextBox", "2"); Text(window, "CfaHeightTextBox", "2");
                    Text(window, "TableIdTextBox", "stars"); Text(window, "TableFieldsTextBox", "name|String");
                    Text(window, control, invalid);
                    var exception = Assert.Throws<TargetInvocationException>(() => Invoke(window, "ApplyImageEditor", image));
                    Assert.IsType<FormatException>(exception.InnerException);
                    Assert.Equal("original", image.Id);
                    Assert.Equal("retained", image.Properties["Test:Original"]);
                    Assert.Null(image.ColorFilterArray); Assert.Empty(image.Tables);
                    Assert.Equal(new byte[] { 42 }, image.GetSamples<byte>());
                }
                finally { window.Close(); }
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void Text(MainWindow window, string name, string value) => ((TextBox)window.FindName(name)).Text = value;
    private static void Invoke(MainWindow window, string method, params object[] args) =>
        typeof(MainWindow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, args);
}
