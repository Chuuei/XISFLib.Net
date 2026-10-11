using XISFLib.Viewer;

namespace XISFLib.Viewer.Core.Tests;

public class LargeMetadataTests
{
    [Fact]
    public void LargeStringPreviewNeverReplacesSavedValue()
    {
        string value = string.Concat(Enumerable.Repeat("spline,coefficient\n", 20000));
        var image = XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1);
        var row = ViewerEditorCodec.ToEditorRow("Test:Spline", new XisfPropertyData("String", value, "%s", "full text"));
        Assert.True(row.RequiresValueDialog);
        Assert.True(row.InlineValue.Length < 350);
        Assert.DoesNotContain('\n', row.InlineValue);
        ViewerEditorCodec.ApplyMetadata(image, [row], []);
        using var stream = new MemoryStream(); XisfWriter.Write(stream, [image]); stream.Position = 0;
        Assert.Equal(value, XisfReader.Load(stream).Images[0].Properties["Test:Spline"].Value);
        Assert.Equal(value, new MetadataNode("Spline", value).Value);
        Assert.True(new MetadataNode("Spline", value).DisplayValue!.Length < 350);
    }

    [Fact]
    public void LargeArraySummaryAndUnchangedApplyAvoidFullFormattingAndRetainPrecision()
    {
        var values = Enumerable.Repeat(ulong.MaxValue, 100000).ToArray();
        var original = new XisfPropertyData("UI64Vector", values, "%u", "exact");
        var row = ViewerEditorCodec.ToEditorRow("Test:Vector", original);
        Assert.Contains("elements", row.InlineValue);
        Assert.True(row.RequiresValueDialog);
        Assert.Same(original, ViewerEditorCodec.ParseProperties([row])[row.Id]);
        Assert.Equal(ulong.MaxValue.ToString(), row.Value.Split(',')[0]);
        // Inspecting the complete text does not make it a user edit.
        Assert.Same(original, ViewerEditorCodec.ParseProperties([row])[row.Id]);
        row.Value = "1, 2";
        Assert.Equal(new ulong[] { 1, 2 }, Assert.IsType<XisfPropertyData>(ViewerEditorCodec.ParseProperties([row])[row.Id]).Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4096)]
    [InlineData(4097)]
    [InlineData(200000)]
    public void PageReconstructionIsExactIncludingWhitespaceAndSurrogateBoundaries(int length)
    {
        string value = new('x', length);
        if (length > 4096) value = value[..4095] + "😀\r\n\t" + value[4097..];
        var pages = new PagedMetadataText(value);
        Assert.Equal(value, string.Concat(Enumerable.Range(0, pages.PageCount).Select(pages.GetPage)));
        Assert.Same(value, pages.BuildText());
        pages.SetPage(0, pages.GetPage(0) + "edited");
        Assert.True(pages.HasChanges);
        Assert.Equal(string.Concat(Enumerable.Range(0, pages.PageCount).Select(pages.GetPage)), pages.BuildText());
        Assert.All(Enumerable.Range(0, pages.PageCount), p => Assert.True(pages.GetPage(p).Length <= MetadataText.PageSize + 7));
        Assert.Throws<ArgumentOutOfRangeException>(() => pages.GetPage(pages.PageCount));
    }
}
