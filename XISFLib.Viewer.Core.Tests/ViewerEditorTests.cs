using System.Numerics;
using XISFLib.Viewer;

namespace XISFLib.Viewer.Core.Tests;

public class ViewerEditorTests
{
    [Theory]
    [InlineData("UInt64", "18446744073709551615", typeof(ulong))]
    [InlineData("Int64", "-9223372036854775808", typeof(long))]
    [InlineData("Float32", "0.25", typeof(float))]
    [InlineData("Boolean", "true", typeof(bool))]
    [InlineData("TimePoint", "2026-10-11T12:00:00+02:00", typeof(DateTimeOffset))]
    public void ScalarsKeepDeclaredTypes(string type, string value, Type expected) =>
        Assert.IsType(expected, ViewerEditorCodec.ParseProperty(type, value));

    [Fact]
    public void TypedMatrixAndRepeatedFitsCardsSurviveSaveReload()
    {
        var image = XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1);
        var matrix = new long[,] { { long.MinValue, 2 }, { 3, long.MaxValue } };
        var row = ViewerEditorCodec.ToEditorRow("Test:Matrix", new XisfPropertyData("I64Matrix", matrix, "%d", "Exact integers"));
        Assert.Equal(2, row.Rows); Assert.Equal(2, row.Columns);
        ViewerEditorCodec.ApplyMetadata(image, [row, new() { Id = "Test:Unsigned", Type = "UInt64", Value = ulong.MaxValue.ToString() }],
            [new() { Name = "HISTORY", Value = "first" }, new() { Name = "HISTORY", Value = "second" }, new() { Name = "OBJECT", Value = "'M31'", Comment = "Target" }]);
        using var stream = new MemoryStream();
        XisfWriter.Write(stream, [image]); stream.Position = 0;
        var loaded = XisfReader.Load(stream).Images[0];
        Assert.Equal(matrix, loaded.Properties["Test:Matrix"].Value);
        Assert.Equal("%d", loaded.Properties["Test:Matrix"].Format);
        Assert.Equal("Exact integers", loaded.Properties["Test:Matrix"].Comment);
        Assert.Equal(ulong.MaxValue, loaded.Properties["Test:Unsigned"].Value);
        Assert.Equal(new[] { "", "", "'M31'" }, loaded.FitsKeywords.Select(k => k.Value));
        Assert.Equal(new[] { "first", "second" }, loaded.FitsKeywords.Take(2).Select(k => k.Comment));
        Assert.Equal("Target", loaded.FitsKeywords[2].Comment);
    }

    [Fact]
    public void InvalidRowsDoNotPartiallyApplyOrReplaceScientificStorage()
    {
        var image = XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1);
        image.Properties["Original"] = "retained";
        image.FitsKeywords.Add(new("OBJECT", "'M31'", null));
        Assert.Throws<FormatException>(() => ViewerEditorCodec.ApplyMetadata(image,
            [new() { Id = "Valid", Type = "Int32", Value = "1" }, new() { Id = "Invalid", Type = "Int32", Value = "no" }], []));
        Assert.Equal("retained", image.Properties["Original"]);
        Assert.Single(image.Properties); Assert.Single(image.FitsKeywords);
        Assert.Equal(new byte[] { 42 }, image.GetSamples<byte>());
        Assert.Throws<FormatException>(() => ViewerEditorCodec.ParseProperties([new() { Id = "same" }, new() { Id = " same " }]));
        Assert.Throws<FormatException>(() => ViewerEditorCodec.ParseProperty("I32Matrix", "1,2", 2, 2));
        Assert.Throws<OverflowException>(() => ViewerEditorCodec.ParseProperty("UInt64", "18446744073709551616"));
        Assert.Throws<NotSupportedException>(() => ViewerEditorCodec.ParseProperty("Complex64", "1,2"));
    }

    [Fact]
    public void UnsupportedValuesArePreservedWithoutParsingDisplayText()
    {
        var image = XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1);
        var property = new XisfPropertyData("Complex64", new Complex(1, -2), null, "Preserve");
        var row = ViewerEditorCodec.ToEditorRow("Test:Complex", property);
        Assert.True(row.IsReadOnly);
        ViewerEditorCodec.ApplyMetadata(image, [row], []);
        Assert.Same(property, image.Properties["Test:Complex"]);
        using var stream = new MemoryStream(); XisfWriter.Write(stream, [image]); stream.Position = 0;
        Assert.Equal(new Complex(1, -2), XisfReader.Load(stream).Images[0].Properties["Test:Complex"].Value);
        row.Id = "Renamed";
        Assert.Throws<FormatException>(() => ViewerEditorCodec.ParseProperties([row]));
    }

    [Fact]
    public void TableCodecChecksCellCountAndScalarTypes()
    {
        var table = ViewerEditorCodec.ParseTable("stars", "Catalog", "name|String|Star\nflux|Float32|Flux", "Vega|0.25")!;
        Assert.Equal("Star", table.Fields[0].Header);
        Assert.Equal("Vega", table.Rows[0][0]); Assert.IsType<float>(table.Rows[0][1]);
        Assert.Throws<FormatException>(() => ViewerEditorCodec.ParseTable("stars", "", "name|String\nflux|Float32", "Vega"));
        Assert.Throws<FormatException>(() => ViewerEditorCodec.ParseTable("stars", "", "name", "Vega"));
    }

    [Fact]
    public void UnavailablePropertyRetainsItsOriginalXmlOnReencoding()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xish");
        try
        {
            File.WriteAllText(path, """
                <xisf version="1.0" xmlns="http://www.pixinsight.com/xisf">
                  <Image geometry="1:1:1" sampleFormat="UInt8" location="embedded"><Data encoding="base64">Kg==</Data>
                    <Property id="Test:Unknown" type="FutureType" custom="retained"><Extension>opaque</Extension></Property>
                  </Image>
                </xisf>
                """);
            var parsed = XisfReader.Load(path).Images[0];
            var image = XisfImageData.FromImage(parsed);
            var row = ViewerEditorCodec.ToEditorRow("Test:Unknown", image.Properties["Test:Unknown"]);
            Assert.True(row.IsReadOnly);
            ViewerEditorCodec.ApplyMetadata(image, [row], []);
            using var stream = new MemoryStream(); XisfWriter.Write(stream, [image]); stream.Position = 0;
            var retained = XisfReader.Load(stream).Images[0].Properties["Test:Unknown"];
            Assert.False(retained.IsAvailable);
            Assert.Equal("retained", retained.Xml.Attribute("custom")!.Value);
            Assert.Equal("opaque", retained.Xml.Elements().Single().Value);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task SelectionDiscardsUnappliedRowsButKeepsAppliedMetadataPerImage()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf");
        try
        {
            XisfWriter.Write(path, [XisfImageData.FromSamples(new byte[] { 1 }, [1, 1], 1), XisfImageData.FromSamples(new byte[] { 2 }, [1, 1], 1)]);
            var vm = new WorkbenchViewModel(_ => Task.CompletedTask) { FilePath = path };
            await vm.LoadCommand.ExecuteAsync(null);
            vm.ImageProperties.Add(new() { Id = "Test:Applied", Value = "first" });
            await vm.ApplyMetadataCommand.ExecuteAsync(null);
            vm.ImageProperties.Add(new() { Id = "Test:Discarded", Value = "unsaved" });
            vm.SelectedImageIndex = 1; await vm.SelectImageCommand.ExecuteAsync(null);
            Assert.DoesNotContain(vm.ImageProperties, r => r.Id == "Test:Applied");
            vm.SelectedImageIndex = 0; await vm.SelectImageCommand.ExecuteAsync(null);
            Assert.Contains(vm.ImageProperties, r => r.Id == "Test:Applied" && r.Value == "first");
            Assert.DoesNotContain(vm.ImageProperties, r => r.Id == "Test:Discarded");
            await vm.UnloadCommand.ExecuteAsync(null);
            Assert.Empty(vm.ImageProperties); Assert.False(vm.CanEditMetadata);
        }
        finally { File.Delete(path); }
    }
}
