using XISFLib.Viewer;

namespace XISFLib.Viewer.Core.Tests;

public class ImageAttributeEditorTests
{
    [Fact]
    public void AttributesAndAncillaryFieldsSurviveSerializationWithoutChangingSamples()
    {
        var image = XisfImageData.FromSamples(new byte[] { 10, 20 }, [2, 1], 1);
        var uuid = Guid.NewGuid();
        var rgb = XisfRgbWorkingSpace.Srgb;
        image.RgbWorkingSpace = rgb with { Name = "Retained working space" };
        var fields = ImageEditorFields.FromImage(image);
        fields.Id = "edited"; fields.ImageType = "ScienceFrame"; fields.ColorSpace = "Gray"; fields.Orientation = "90;flip";
        fields.Offset = "12.5"; fields.Uuid = uuid.ToString(); fields.ResolutionX = "100"; fields.ResolutionY = "150";
        fields.CfaPattern = "RGGB"; fields.CfaWidth = "2"; fields.CfaHeight = "2"; fields.CfaName = "Bayer";
        fields.DisplayFunction = "0.5,0.5,0.5,0.5; 0,0,0,0; 1,1,1,1; 0,0,0,0; 1,1,1,1";
        fields.Parse(image).Apply(image);
        Assert.Equal(new byte[] { 10, 20 }, image.GetSamples<byte>());
        using var stream = new MemoryStream(); XisfWriter.Write(stream, [image]); stream.Position = 0;
        var loaded = XisfReader.Load(stream).Images[0];
        Assert.Equal("edited", loaded.Id); Assert.Equal("Light", loaded.ImageType); Assert.Equal(uuid, loaded.Uuid);
        Assert.Equal("90;flip", loaded.Orientation); Assert.Equal(12.5, loaded.Offset);
        Assert.Equal(new XisfResolution(100, 150, "inch"), loaded.Resolution);
        Assert.Equal("RGGB", loaded.ColorFilterArray!.Pattern); Assert.Equal("Bayer", loaded.ColorFilterArray.Name);
        Assert.Equal("Retained working space", loaded.RgbWorkingSpace!.Name);
        Assert.Equal(XisfColorSpace.DeriveLuminanceCoefficients(rgb), loaded.RgbWorkingSpace.LuminanceCoefficients);
        Assert.Equal(new[] { .5, .5, .5, .5 }, loaded.DisplayFunction!.Midtones);
        Assert.Equal(new byte[] { 10, 20 }, loaded.GetSamples<byte>());
    }

    [Theory]
    [InlineData("uuid")]
    [InlineData("resolution")]
    [InlineData("cfa")]
    [InlineData("display")]
    [InlineData("color")]
    [InlineData("offset")]
    public void InvalidAttributeSnapshotsCannotMutateTheImage(string field)
    {
        var image = XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1); image.Id = "original";
        var edit = ImageEditorFields.FromImage(image); edit.Id = "changed";
        switch (field)
        {
            case "uuid": edit.Uuid = "00000000-0000-1000-8000-000000000000"; break;
            case "resolution": edit.ResolutionX = "100"; break;
            case "cfa": edit.CfaPattern = "RGGB"; edit.CfaWidth = "1"; edit.CfaHeight = "1"; break;
            case "display": edit.DisplayFunction = "NaN,0,0,0;0,0,0,0;1,1,1,1;0,0,0,0;1,1,1,1"; break;
            case "color": edit.ColorSpace = "RGB"; break;
            case "offset": edit.Offset = "-1"; break;
        }
        Assert.Throws<FormatException>(() => edit.Parse(image));
        Assert.Equal("original", image.Id); Assert.Null(image.ColorFilterArray);
        Assert.Equal(new byte[] { 42 }, image.GetSamples<byte>());
    }

    [Fact]
    public void BlanksRemoveOptionalMetadataAndKeepPixelGeometry()
    {
        var image = XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1);
        image.Resolution = new(72, 72, "inch"); image.ColorFilterArray = new("R", 1, 1, null);
        image.RgbWorkingSpace = XisfRgbWorkingSpace.Srgb;
        var edit = ImageEditorFields.FromImage(image);
        edit.ResolutionX = edit.ResolutionY = edit.CfaPattern = edit.Gamma = edit.DisplayFunction = "";
        edit.Parse(image).Apply(image);
        Assert.Null(image.Resolution); Assert.Null(image.ColorFilterArray); Assert.Null(image.RgbWorkingSpace); Assert.Null(image.DisplayFunction);
        Assert.Equal(new[] { 1, 1 }, image.Dimensions);
    }

    [Fact]
    public async Task ApplyingOrientationUpdatesPreviewAndInvalidPropertiesKeepAttributesUnchanged()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf");
        try
        {
            XisfWriter.Write(path, [XisfImageData.FromSamples(new byte[] { 10, 20 }, [2, 1], 1)]);
            XisfBgra32Image? frame = null;
            var vm = new WorkbenchViewModel(value => { frame = value; return Task.CompletedTask; }) { FilePath = path, AutoStretch = false };
            await vm.LoadCommand.ExecuteAsync(null);
            vm.ImageEditor.Orientation = "90";
            await vm.ApplyMetadataCommand.ExecuteAsync(null);
            Assert.Equal(1, frame!.Width); Assert.Equal(2, frame.Height);
            vm.ImageEditor.Id = "changed";
            vm.ImageProperties.Add(new() { Id = "Invalid", Type = "Int32", Value = "bad" });
            await vm.ApplyMetadataCommand.ExecuteAsync(null);
            Assert.Null(vm.SelectedImage!.Id);
            Assert.Empty(vm.SelectedImage.Properties);
        }
        finally { File.Delete(path); }
    }
}
