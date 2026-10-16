using XISFLib.Viewer;

namespace XISFLib.Viewer.Core.Tests;

public sealed class ViewerDebayerTests
{
    [Theory]
    [InlineData("RGGB", false)] [InlineData("BGGR", true)]
    [InlineData("GRBG", false)] [InlineData("GBRG", true)]
    public void ConstantColorMosaicsReconstructEveryPixelIncludingOddEdges(string pattern, bool bigEndian)
    {
        ushort[] mosaic = Enumerable.Range(0, 35).Select(i => pattern[(i / 7 % 2) * 2 + i % 7 % 2] switch
        { 'R' => (ushort)60000, 'G' => (ushort)30000, _ => (ushort)1000 }).ToArray();
        var source = XisfImageData.FromSamples(mosaic, [7, 5], 1, isBigEndian: bigEndian);
        source.ColorFilterArray = new(pattern, 2, 2, null);
        source.Orientation = "flip"; source.Resolution = new(144, 72, "inch");
        source.Properties.Set("Custom:Test", "metadata");
        source.FitsKeywords.Add(XisfFitsKeyword.FromValue("BAYERPAT", pattern));
        source.FitsKeywords.Add(XisfFitsKeyword.FromValue("HISTORY", "original history"));
        source.Thumbnail = XisfImageData.FromSamples(new byte[] { 0 }, [1, 1], 1);
        source.IccProfile = [1, 2, 3];
        var result = ViewerDebayerService.Create(source, pattern);
        Assert.Equal(Enumerable.Range(0, 105).Select(i => new ushort[] { 60000, 30000, 1000 }[i % 3]), result.GetSamples<ushort>(XisfPixelLayout.Interleaved));
        Assert.Equal(source.RawData.ToArray(), XisfImageData.FromSamples(mosaic, [7, 5], 1, isBigEndian: bigEndian).RawData.ToArray());
        Assert.Equal(source.SampleFormat, result.SampleFormat); Assert.Equal(bigEndian, result.IsBigEndian);
        Assert.Equal("RGB", result.ColorSpace); Assert.Equal(source.Dimensions, result.Dimensions);
        Assert.Equal(source.Orientation, result.Orientation); Assert.Equal(source.Resolution, result.Resolution);
        Assert.True(result.Properties.ContainsKey("Custom:Test"));
        Assert.Null(result.ColorFilterArray); Assert.Null(result.Thumbnail); Assert.Null(result.IccProfile);
        Assert.DoesNotContain(result.FitsKeywords, k => k.Name == "BAYERPAT");
        Assert.Equal(2, result.FitsKeywords.Count(k => k.Name == "HISTORY"));
    }

    [Fact]
    public void InterpolationUsesAxialAndDiagonalNeighborsAndExactUInt64Means()
    {
        // RGGB: green at center averages its four axial neighbors; blue averages diagonals.
        var source = XisfImageData.FromSamples(new ulong[]
        {
            1, 2, 3, 4, 5,
            6, ulong.MaxValue, 8, ulong.MaxValue - 2, 10,
            11, 12, 13, 14, 15,
            16, ulong.MaxValue - 4, 18, ulong.MaxValue - 6, 20,
            21, 22, 23, 24, 25
        }, [5, 5], 1, isBigEndian: true);
        var result = ViewerDebayerService.Create(source, "RGGB").GetSamples<ulong>(XisfPixelLayout.Interleaved);
        Assert.Equal(13ul, result[12 * 3]); Assert.Equal(13ul, result[12 * 3 + 1]);
        Assert.Equal(ulong.MaxValue - 3, result[12 * 3 + 2]);
        Assert.Equal(ulong.MaxValue, result[6 * 3 + 2]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FloatingSamplesRetainBoundsNonfiniteValuesAndKnownSampleBits(bool bigEndian)
    {
        var source = XisfImageData.FromSamples(new double[] { -0d, 2, 3, double.NaN, 5, 6, 7, 8, double.PositiveInfinity }, [3, 3], 1, isBigEndian: bigEndian);
        source.LowerBound = -10; source.UpperBound = 20;
        var image = ViewerDebayerService.Create(source, "RGGB");
        var values = image.GetSamples<double>(XisfPixelLayout.Interleaved);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0d), BitConverter.DoubleToInt64Bits(values[0]));
        Assert.True(double.IsNaN(values[1])); Assert.Equal(double.PositiveInfinity, values[8 * 3]);
        Assert.Equal(-10, image.LowerBound); Assert.Equal(20, image.UpperBound);
    }

    [Fact]
    public void MetadataDetectionUsesNativeCfaThenFitsWithOffsetsAndRejectsInvalidMetadata()
    {
        var image = XisfImageData.FromSamples(new byte[4], [2, 2], 1);
        Assert.Null(ViewerDebayerService.Detect(image).Pattern);
        image.FitsKeywords.Add(XisfFitsKeyword.FromValue("BAYERPAT", "RGGB"));
        image.FitsKeywords.Add(XisfFitsKeyword.FromValue("XBAYROFF", 1));
        image.FitsKeywords.Add(XisfFitsKeyword.FromValue("YBAYROFF", -1));
        Assert.Equal("BGGR", ViewerDebayerService.Detect(image).Pattern);
        image.ColorFilterArray = new("GBRG", 2, 2, null);
        Assert.Equal("GBRG", ViewerDebayerService.Detect(image).Pattern);
        image.ColorFilterArray = new("RGBRGB", 3, 2, null);
        Assert.Null(ViewerDebayerService.Detect(image).Pattern);
        image.ColorFilterArray = null;
        image.FitsKeywords[1] = new("XBAYROFF", "1.5", null);
        Assert.Null(ViewerDebayerService.Detect(image).Pattern);
    }

    [Fact]
    public void RejectsUnsupportedGeometryColorAndPatternsAndHonorsCancellation()
    {
        var source = XisfImageData.FromSamples(new float[4], [2, 2], 1);
        Assert.Throws<ArgumentException>(() => ViewerDebayerService.Create(source, "XXXX"));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => ViewerDebayerService.Create(source, "RGGB", cts.Token));
        foreach (var image in new[] { XisfImageData.FromSamples(new byte[4], [1, 4], 1),
            XisfImageData.FromSamples(new byte[8], [2, 2, 2], 1), XisfImageData.FromSamples(new byte[12], [2, 2], 3) })
            Assert.Throws<NotSupportedException>(() => ViewerDebayerService.Create(image, "RGGB"));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task WorkflowDetectsOrRequiresManualChoiceAndSavesSelectedRgbWithoutChangingOriginal(bool metadata)
    {
        string folder = Path.Combine(Path.GetTempPath(), "debayer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "source.xisf"), output = Path.Combine(folder, "rgb.xisf");
            var source = XisfImageData.FromSamples(new ushort[] { 60000, 30000, 30000, 1000 }, [2, 2], 1);
            if (metadata) source.ColorFilterArray = new("RGGB", 2, 2, null);
            XisfWriter.Write(path, [source]);
            byte[] original = File.ReadAllBytes(path);
            var vm = new WorkbenchViewModel(_ => Task.CompletedTask) { FilePath = path, AutoStretch = false };
            await vm.LoadCommand.ExecuteAsync(null);
            Assert.Equal(metadata ? "RGGB" : null, vm.BayerPattern);
            Assert.Equal(metadata, vm.DebayerCommand.CanExecute(null));
            vm.BayerPattern = "RGGB";
            await vm.DebayerCommand.ExecuteAsync(null);
            Assert.Equal(3, vm.SelectedImage!.ChannelCount); Assert.Equal(2, vm.ImageLabels.Count);
            Assert.False(vm.CanCopyOriginal); Assert.False(vm.PreserveOriginal); Assert.False(vm.CanDebayer);
            await vm.SaveAsync(output);
            Assert.Equal(3, XisfReader.Load(output).Images[0].ChannelCount);
            Assert.Null(XisfReader.Load(output).Images[0].ColorFilterArray);
            Assert.Equal(original, File.ReadAllBytes(path));
            await vm.UnloadCommand.ExecuteAsync(null);
            Assert.Null(vm.BayerPattern); Assert.False(vm.DebayerCommand.CanExecute(null));
        }
        finally { foreach (string file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); }
    }
}
