using System.Security.Cryptography;
using ImageMagick;
using XISFLib.Viewer;

namespace XISFLib.Viewer.Core.Tests;

public sealed class ViewerWorkflowTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "xisf-viewer-" + Guid.NewGuid().ToString("N"));
    public ViewerWorkflowTests() => Directory.CreateDirectory(_folder);
    private string FilePath(string name) => Path.Combine(_folder, name);
    public void Dispose() { foreach (var file in Directory.GetFiles(_folder)) File.Delete(file); Directory.Delete(_folder); }

    [Theory]
    [InlineData(MagickFormat.Png)] [InlineData(MagickFormat.Jpeg)] [InlineData(MagickFormat.Tiff)]
    [InlineData(MagickFormat.Bmp)] [InlineData(MagickFormat.Gif)]
    public void PortableDecoderSupportsAllRequiredCodecs(MagickFormat format)
    {
        var path = FilePath("raster." + format);
        using (var image = new MagickImage(MagickColors.Red, 2, 1))
        { image.Density = new(144, 96); image.Write(path, format); }
        var decoded = ViewerRasterDecoder.Load(path);
        Assert.Equal(new[] { 2, 1 }, decoded.Dimensions); Assert.Equal(3, decoded.ChannelCount);
        var samples = decoded.GetSamples<byte>(XisfPixelLayout.Interleaved); Assert.InRange(samples[0], (byte)250, (byte)255); Assert.InRange(samples[1], (byte)0, (byte)5);
        if (format is MagickFormat.Png or MagickFormat.Tiff) { Assert.Equal(144, decoded.Resolution!.Horizontal, 1); Assert.Equal(96, decoded.Resolution.Vertical, 1); }
    }

    [Fact]
    public void AlphaFrameZeroThumbnailAndDpiArePreserved()
    {
        string path = FilePath("9 transparent.tiff");
        using (var images = new MagickImageCollection())
        {
            var first = new MagickImage(new MagickColor("#80402080"), 2, 1) { Density = new(144, 72) };
            images.Add(first); images.Add(new MagickImage(MagickColors.Blue, 4, 3)); images.Write(path, MagickFormat.Tiff);
        }
        var decoded = ViewerRasterDecoder.Load(path); Assert.Equal(4, decoded.ChannelCount);
        Assert.Equal("_9_transparent", decoded.Id); Assert.Equal(new[] { 2, 1 }, decoded.Dimensions);
        Assert.Equal(new byte[] { 128, 64, 32, 128, 128, 64, 32, 128 }, decoded.GetSamples<byte>(XisfPixelLayout.Interleaved));
        var thumbnail = ViewerRasterDecoder.Load(path, true);
        Assert.Equal(3, thumbnail.ChannelCount); Assert.Null(thumbnail.Id); Assert.Null(thumbnail.Resolution); Assert.Null(thumbnail.IccProfile);
        Assert.Equal(new byte[] { 128, 64, 32, 128, 64, 32 }, thumbnail.GetSamples<byte>(XisfPixelLayout.Interleaved));
    }

    [Fact]
    public void UnsupportedSignaturesAreRejectedBeforeGeneralDecoderUse()
    {
        var path = FilePath("unsupported.png"); File.WriteAllText(path, "<svg xmlns='http://www.w3.org/2000/svg'/>");
        Assert.Throws<NotSupportedException>(() => ViewerRasterDecoder.Load(path));
        Assert.Contains("Apache", ViewerNotices.Read());
    }

    [Fact]
    public async Task CopyIgnoresInvalidDraftsAndPreservesWholeMultiImageSource()
    {
        string source = FilePath("source.xisf"), destination = FilePath("copy.xisf");
        XisfWriter.Write(source, [Image(42), Image(84)]);
        var vm = new WorkbenchViewModel(_ => Task.CompletedTask) { FilePath = source }; await vm.LoadCommand.ExecuteAsync(null);
        Assert.True(vm.CanCopyOriginal); Assert.True(vm.PreserveOriginal);
        vm.ImageEditor.Uuid = "bad"; vm.DocumentEditor.Properties.Add(new() { Id = "Bad", Type = "Int32", Value = "bad" });
        await vm.SaveAsync(destination);
        Assert.Equal(SHA256.HashData(File.ReadAllBytes(source)), SHA256.HashData(File.ReadAllBytes(destination)));
        Assert.Equal(2, vm.ImageLabels.Count); Assert.Contains("byte-identical", vm.Status);
        var before = File.ReadAllBytes(destination); await vm.SaveAsync(destination); Assert.Equal(before, File.ReadAllBytes(destination));
    }

    [Fact]
    public async Task ReencodedAndDistributedSavesCommitSelectedImageAndDocumentAndReload()
    {
        string source = FilePath("source.xisf"); XisfWriter.Write(source, [Image(42), Image(84)]);
        var original = File.ReadAllBytes(source);
        var vm = new WorkbenchViewModel(_ => Task.CompletedTask) { FilePath = source, AutoStretch = false };
        await vm.LoadCommand.ExecuteAsync(null); vm.SelectedImageIndex = 1; await vm.SelectImageCommand.ExecuteAsync(null);
        vm.PreserveOriginal = false; vm.ImageEditor.Id = "Selected";
        vm.DocumentEditor.Properties.Add(new() { Id = "Test:Document", Value = "retained" });
        vm.ImageProperties.Add(new() { Id = "Test:Float", Type = "Float32", Value = "0.25" });
        vm.DocumentEditor.CreatorApplication = "Workflow tests";
        var profile = FilePath("profile.icc"); File.WriteAllBytes(profile, Profile()); await vm.AttachIccAsync(profile);
        var thumbnail = FilePath("thumbnail.png"); using (var png = new MagickImage(MagickColors.Red, 1, 1)) png.Write(thumbnail);
        await vm.AttachThumbnailAsync(thumbnail);
        string output = FilePath("selected.xisf"); await vm.SaveAsync(output);
        var loaded = XisfReader.Load(output); Assert.Single(loaded.Images); Assert.Equal("Selected", loaded.Images[0].Id);
        Assert.Equal(new byte[] { 84 }, loaded.Images[0].GetSamples<byte>()); Assert.Equal(.25f, loaded.Images[0].Properties["Test:Float"].Value);
        Assert.Equal("retained", loaded.Properties["Test:Document"].Value); Assert.Equal("Workflow tests", loaded.Metadata["XISF:CreatorApplication"].Value);
        Assert.Equal(Profile(), vm.SelectedImage!.IccProfile); Assert.Equal(3, vm.SelectedImage.Thumbnail!.ChannelCount);
        Assert.True(vm.CanCopyOriginal); Assert.True(vm.PreserveOriginal); Assert.NotEmpty(vm.Metadata);
        vm.Distributed = true; output = FilePath("selected.xish"); await vm.SaveAsync(output);
        Assert.True(File.Exists(FilePath("selected.xisb"))); Assert.False(vm.CanCopyOriginal);
        Assert.Equal(new byte[] { 84 }, XisfReader.Load(output).Images[0].GetSamples<byte>()); Assert.Equal(original, File.ReadAllBytes(source));
    }

    [Fact]
    public async Task FitsSaveShowsConversionOmissionsAndChecksums()
    {
        string source = FilePath("source.xisf"), output = FilePath("selected.fits"); var image = Image(42); image.IccProfile = Profile();
        XisfWriter.Write(source, [image]);
        var vm = new WorkbenchViewModel(_ => Task.CompletedTask) { FilePath = source }; await vm.LoadCommand.ExecuteAsync(null);
        await vm.SaveAsync(output);
        Assert.Equal(output, vm.FilePath); Assert.False(vm.CanCopyOriginal); Assert.Equal(new byte[] { 42 }, vm.SelectedImage!.GetSamples<byte>());
        Assert.Contains(vm.Diagnostics, d => d.Severity == "Conversion");
        var fits = FitsReader.Load(output); Assert.Contains(fits.Hdus[0].Header, c => c.Keyword == "CHECKSUM"); Assert.Contains(fits.Hdus[0].Header, c => c.Keyword == "DATASUM");
        Assert.DoesNotContain(fits.Diagnostics, d => d.Code.Contains("CHECKSUM", StringComparison.OrdinalIgnoreCase) && d.Severity == FitsValidationSeverity.Error);
    }

    [Fact]
    public async Task InvalidDraftsAndWriteFailureKeepExistingOutputAndRecover()
    {
        var source = FilePath("source.xisf"); XisfWriter.Write(source, [Image(42)]);
        var output = FilePath("existing.xisf"); File.WriteAllText(output, "existing");
        var vm = new WorkbenchViewModel(_ => Task.CompletedTask) { FilePath = source }; await vm.LoadCommand.ExecuteAsync(null);
        vm.PreserveOriginal = false; vm.ImageEditor.Id = "should_not_apply"; vm.TableEditor.Id = "table"; vm.TableEditor.Fields = "bad";
        await vm.SaveAsync(output); Assert.Equal("existing", File.ReadAllText(output)); Assert.Null(vm.SelectedImage!.Id); Assert.False(vm.IsBusy);
        vm.TableEditor.Id = ""; vm.ImageEditor.Id = "valid"; await vm.SaveAsync(FilePath("missing/child.xisf"));
        Assert.Equal(source, vm.FilePath); Assert.False(vm.IsBusy); await vm.SaveAsync(output); Assert.Equal(output, vm.FilePath);
    }

    [Fact]
    public void EveryWriterOptionRoundTripsScientificSamples()
    {
        var image = XisfImageData.FromSamples(new ushort[] { 1, 32768, 65535 }, [3, 1], 1);
        var snapshot = DocumentEditorState.Parse([], [], "Options test");
        foreach (var compression in Enum.GetValues<XisfCompression>()) foreach (var checksum in Enum.GetValues<XisfChecksum>())
        {
            string output = FilePath("options.xisf");
            ViewerFileService.Save(output, image, snapshot, new() { Compression = compression, Checksum = checksum, Shuffle = true }, false, null);
            Assert.Equal(new ushort[] { 1, 32768, 65535 }, XisfReader.Load(output).Images[0].GetSamples<ushort>());
        }
    }

    [Fact]
    public async Task ImportThenUtilitiesAndSaveHaveWorkingSessionState()
    {
        string input = FilePath("raster.bmp"); using (var raster = new MagickImage(MagickColors.Red, 2, 1)) raster.Write(input);
        var vm = new WorkbenchViewModel(_ => Task.CompletedTask); await vm.ImportAsync(input);
        Assert.True(vm.CanEditMetadata); Assert.False(vm.CanCopyOriginal); Assert.Empty(vm.Metadata); Assert.Equal(3, vm.Utilities.Channels.Count);
        await vm.Utilities.StatisticsCommand.ExecuteAsync(null); Assert.Equal(3, vm.Utilities.Statistics.Count);
        await vm.Utilities.HistogramCommand.ExecuteAsync(null); Assert.Equal(256, vm.Utilities.Histogram.Count);
        await vm.Utilities.RegionCommand.ExecuteAsync(null); Assert.Contains("6 value(s)", vm.Utilities.Samples);
        await vm.Utilities.ChannelCommand.ExecuteAsync(null); Assert.Contains("2 value(s)", vm.Utilities.Samples);
        vm.Utilities.Bins = "65537"; await vm.Utilities.HistogramCommand.ExecuteAsync(null); Assert.Contains("65,536", vm.Status); Assert.False(vm.IsBusy);
        string output = FilePath("import.xisf"); await vm.SaveAsync(output); Assert.True(vm.CanCopyOriginal); Assert.NotEmpty(vm.Metadata);
        await vm.UnloadCommand.ExecuteAsync(null); Assert.Empty(vm.Utilities.Statistics); Assert.Empty(vm.Utilities.Overview); Assert.False(vm.Utilities.RegionCommand.CanExecute(null));
    }
    [Fact]
    public async Task FitsWcsUtilitiesUseZeroBasedCoordinatesAndUpdateReciprocalFields()
    {
        var image = Image(42); image.Properties["Instrument:ExposureTime"] = 30f;
        image.FitsKeywords.Add(new("CTYPE1", "'RA---TAN'", null)); image.FitsKeywords.Add(new("CTYPE2", "'DEC--TAN'", null));
        image.FitsKeywords.Add(new("CRPIX1", "1", null)); image.FitsKeywords.Add(new("CRPIX2", "1", null));
        image.FitsKeywords.Add(new("CRVAL1", "120", null)); image.FitsKeywords.Add(new("CRVAL2", "45", null));
        image.FitsKeywords.Add(new("CDELT1", "-0.001", null)); image.FitsKeywords.Add(new("CDELT2", "0.001", null));
        var path = FilePath("wcs.xisf"); XisfWriter.Write(path, [image]);
        var vm = new WorkbenchViewModel(_ => Task.CompletedTask) { FilePath = path }; await vm.LoadCommand.ExecuteAsync(null);
        Assert.Contains(vm.Utilities.Overview, entry => entry.Name == "Exposure" && entry.Value.StartsWith("30"));
        Assert.Contains("TAN", vm.Utilities.WcsSummary); Assert.True(vm.Utilities.PixelToSkyCommand.CanExecute(null));
        vm.Utilities.PixelX = "0"; vm.Utilities.PixelY = "0"; await vm.Utilities.PixelToSkyCommand.ExecuteAsync(null);
        Assert.Equal(120d, ViewerUtilityService.Number(vm.Utilities.RightAscension), 8); Assert.Equal(45d, ViewerUtilityService.Number(vm.Utilities.Declination), 8);
        await vm.Utilities.SkyToPixelCommand.ExecuteAsync(null); Assert.Equal(0d, ViewerUtilityService.Number(vm.Utilities.PixelX), 8); Assert.Equal(0d, ViewerUtilityService.Number(vm.Utilities.PixelY), 8);
        await vm.UnloadCommand.ExecuteAsync(null); Assert.False(vm.Utilities.PixelToSkyCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(".fit")] [InlineData(".fits")] [InlineData(".fts")]
    public async Task FitsExtensionsAndFailedLoadsRetainTheExistingSession(string extension)
    {
        var path = FilePath("image" + extension);
        FitsWriter.Write(path, FitsConverter.ConvertToFits([Image(42)], ConversionLossPolicy.Allow).Value);
        var vm = new WorkbenchViewModel(_ => Task.CompletedTask) { FilePath = path }; await vm.LoadCommand.ExecuteAsync(null);
        Assert.NotNull(vm.SelectedImage); var previous = vm.SelectedImage; var metadata = vm.Metadata;
        vm.FilePath = FilePath("missing.xisf"); await vm.LoadCommand.ExecuteAsync(null); Assert.Same(previous, vm.SelectedImage); Assert.Same(metadata, vm.Metadata);
        vm.FilePath = FilePath("corrupt.xisf"); File.WriteAllText(vm.FilePath, "broken"); await vm.LoadCommand.ExecuteAsync(null);
        Assert.Same(previous, vm.SelectedImage); Assert.False(vm.IsBusy);
    }

    [Fact]
    public void RenamedDistributedHeadersNeverEnableByteCopyAndComplexFitsFailsSafely()
    {
        var path = FilePath("header.xisf"); var snapshot = DocumentEditorState.Parse([], [], "test");
        ViewerFileService.Save(path, Image(42), snapshot, new(), true, null);
        Assert.False(ViewerSession.Load(path).CanCopyOriginal);
        var output = FilePath("complex.fits"); File.WriteAllText(output, "original");
        var complex = XisfImageData.FromSamples(new System.Numerics.Complex[] { new(1, 2) }, [1, 1], 1);
        Assert.ThrowsAny<Exception>(() => ViewerFileService.Save(output, complex, snapshot, new(), false, null));
        Assert.Equal("original", File.ReadAllText(output));
    }
    private static XisfImageData Image(byte value) => XisfImageData.FromSamples(new byte[] { value }, [1, 1], 1);
    private static byte[] Profile() { var profile = new byte[128]; profile[3] = 128; "acsp"u8.CopyTo(profile.AsSpan(36)); profile[47] = 1; return profile; }
}
