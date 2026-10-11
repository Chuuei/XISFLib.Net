using XISFLib.Viewer;

namespace XISFLib.Viewer.Core.Tests;

public class DocumentAndTableEditorTests
{
    [Fact]
    public void DocumentRowsKeepTypesAndWriterCreationPolicy()
    {
        var editor = new DocumentEditorState { CreatorApplication = " Test creator " };
        var instant = DateTimeOffset.Parse("2026-10-11T12:34:56.1234567+02:00");
        editor.Metadata.Add(new() { Id = "XISF:Title", Value = "Session" });
        editor.Properties.Add(ViewerEditorCodec.ToEditorRow("Test:Time", new XisfPropertyData("TimePoint", instant)));
        editor.Properties.Add(ViewerEditorCodec.ToEditorRow("Test:Matrix", new XisfPropertyData("UI64Matrix", new ulong[,] { { ulong.MaxValue } }, "%d", "Exact")));
        editor.Apply();
        var content = editor.Applied.CreateContent(XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1));
        Assert.Single(content.Images);
        using var stream = new MemoryStream();
        XisfWriter.Write(stream, content, new() { CreatorApplication = editor.Applied.CreatorApplication }); stream.Position = 0;
        var loaded = XisfReader.Load(stream);
        Assert.Equal("Test creator", loaded.Metadata["XISF:CreatorApplication"].Value);
        Assert.True(loaded.Metadata.ContainsKey("XISF:CreationTime"));
        Assert.Equal("Session", loaded.Metadata["XISF:Title"].Value);
        Assert.Equal(instant, loaded.Properties["Test:Time"].Value);
        Assert.Equal(new ulong[,] { { ulong.MaxValue } }, loaded.Properties["Test:Matrix"].Value);
        Assert.Equal("Exact", loaded.Properties["Test:Matrix"].Comment);
        editor.Load(loaded);
        Assert.DoesNotContain(editor.Metadata, row => row.Id is "XISF:CreationTime" or "XISF:CreatorApplication");
    }

    [Fact]
    public void InvalidDocumentRowsRetainTheAppliedSnapshot()
    {
        var editor = new DocumentEditorState();
        editor.Metadata.Add(new() { Id = "Title", Value = "Original" }); editor.Apply();
        var applied = editor.Applied;
        editor.Metadata[0].Value = "Changed";
        editor.Properties.Add(new() { Id = "Invalid", Type = "Int32", Value = "bad" });
        Assert.Throws<FormatException>(() => editor.Apply());
        Assert.Same(applied, editor.Applied);
        Assert.Equal("Original", Assert.IsType<XisfPropertyData>(applied.Metadata["Title"]).Value);
    }

    [Fact]
    public void FirstTableEditsPreserveCommentsAndOtherTables()
    {
        var table = new XisfTableData { Id = "stars", Caption = "Old", Comment = "Retain table comment" };
        table.Fields.Add(new("name", "String", "Star", "%s")); table.Fields.Add(new("flux", "Float32", "Flux", "%.2f"));
        table.Rows.Add(["Vega", .25f]);
        var editor = TableEditorFields.FromTable(table); editor.Caption = "New"; editor.Rows = "Sirius|0.5";
        var edited = editor.Parse()!;
        Assert.Equal("Retain table comment", edited.Comment); Assert.Equal("New", edited.Caption);
        Assert.Equal("%.2f", edited.Fields[1].Format); Assert.IsType<float>(edited.Rows[0][1]);
        var image = XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1); image.Tables.Add(edited);
        var other = new XisfTableData { Id = "other" }; other.Fields.Add(new("value", "String")); other.Rows.Add(["retained"]); image.Tables.Add(other);
        using var stream = new MemoryStream(); XisfWriter.Write(stream, [image]); stream.Position = 0;
        var loaded = XisfReader.Load(stream).Images[0];
        Assert.Equal(2, loaded.Tables.Count); Assert.Equal("Sirius", loaded.Tables[0].Rows[0].Cells[0].Value);
        Assert.Equal(.5f, loaded.Tables[0].Rows[0].Cells[1].Value); Assert.Equal("retained", loaded.Tables[1].Rows[0].Cells[0].Value);
    }

    [Theory]
    [InlineData("pipe|value")]
    [InlineData("line\nvalue")]
    [InlineData(" leading")]
    [InlineData("")]
    public void UnrepresentableTableTextIsPreservedReadOnly(string value)
    {
        var table = new XisfTableData { Id = "preserved" }; table.Fields.Add(new("text", "String")); table.Rows.Add([value]);
        var editor = TableEditorFields.FromTable(table);
        Assert.True(editor.IsReadOnly); Assert.Same(table, editor.Parse());
    }

    [Fact]
    public void ArrayValuedTableCellsArePreservedReadOnly()
    {
        var table = new XisfTableData { Id = "arrays" }; table.Fields.Add(new("values", "F64Vector")); table.Rows.Add([new double[] { 1, 2 }]);
        var editor = TableEditorFields.FromTable(table);
        Assert.True(editor.IsReadOnly); Assert.Same(table, editor.Parse());
    }

    [Fact]
    public async Task InvalidTableRowsCannotPartiallyApplyImageAttributes()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf");
        try
        {
            XisfWriter.Write(path, [XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1)]);
            var vm = new WorkbenchViewModel(_ => Task.CompletedTask) { FilePath = path };
            await vm.LoadCommand.ExecuteAsync(null);
            vm.ImageEditor.Id = "changed"; vm.TableEditor.Id = "stars"; vm.TableEditor.Fields = "name|String\nflux|Float32"; vm.TableEditor.Rows = "missing";
            await vm.ApplyMetadataCommand.ExecuteAsync(null);
            Assert.Null(vm.SelectedImage!.Id); Assert.Empty(vm.SelectedImage.Tables);
            vm.DocumentEditor.Metadata.Add(new() { Id = "Title", Value = "Session" }); await vm.ApplyDocumentCommand.ExecuteAsync(null);
            await vm.UnloadCommand.ExecuteAsync(null);
            Assert.Empty(vm.DocumentEditor.Metadata); Assert.Empty(vm.DocumentEditor.Applied.Metadata); Assert.False(vm.ApplyDocumentCommand.CanExecute(null));
        }
        finally { File.Delete(path); }
    }
}
