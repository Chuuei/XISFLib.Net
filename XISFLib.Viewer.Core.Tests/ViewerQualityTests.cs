using System.Globalization;
using XISFLib.Viewer;

namespace XISFLib.Viewer.Core.Tests;

public class ViewerQualityTests
{
    [Fact]
    public void KnownNoiseAndGaussianStarsIncludeTileBoundariesAndRejectHotPixels()
    {
        const int width = 280, height = 180;
        var values = Noise(width * height, 0.1, 0.001);
        foreach (var (x, y) in new[] { (40, 40), (127, 90), (128, 140), (230, 70) })
            AddStar(values, width, height, x, y, 0.3, 1.5, 1.5);
        values[30 * width + 80] = 0.8; // An isolated hot pixel is not a resolved star.
        var image = XisfImageData.FromSamples(values, [width, height], 1);
        var original = image.RawData.ToArray();
        var report = ViewerQualityService.Analyze(image, 0, 5);
        Assert.Equal("4", Value(report, "Detected star candidates"));
        Assert.InRange(Number(report, "Noise σ estimate"), 0.00095, 0.00105);
        Assert.InRange(Number(report, "Background estimate"), 0.0999, 0.1002);
        Assert.InRange(Number(report, "Median star FWHM estimate", " px"), 3.4, 3.9);
        Assert.InRange(Number(report, "Median eccentricity"), 0, 0.2);
        Assert.InRange(Number(report, "Median star peak SNR estimate"), 270, 330);
        Assert.Equal(original, image.RawData.ToArray());
    }

    [Fact]
    public void ElongatedStarsHaveHigherEccentricityAndThresholdControlsDetection()
    {
        var values = Noise(128 * 128, 0.1, 0.001);
        AddStar(values, 128, 128, 60, 60, 0.2, 2, 1);
        AddStar(values, 128, 128, 95, 95, 0.015, 1.5, 1.5);
        var image = XisfImageData.FromSamples(values, [128, 128], 1);
        Assert.Equal("2", Value(ViewerQualityService.Analyze(image, 0, 5), "Detected star candidates"));
        var report = ViewerQualityService.Analyze(image, 0, 30);
        Assert.Equal("1", Value(report, "Detected star candidates"));
        Assert.InRange(Number(report, "Median eccentricity"), 0.8, 0.9);
    }

    [Fact]
    public void NonFiniteAndClippedPixelsAreCountedAndFlatImagesHaveNoInventedSnr()
    {
        var image = XisfImageData.FromSamples(new double[] { 0, 0.1, 0.1, 1, 1.1, double.NaN, double.PositiveInfinity, -0.1 }, [8, 1], 1);
        var report = ViewerQualityService.Analyze(image, 0, 5);
        Assert.Equal("6 / 2", Value(report, "Finite / non-finite pixels"));
        Assert.StartsWith("2 (", Value(report, "At/below lower bound"));
        Assert.StartsWith("2 (", Value(report, "At/above upper bound"));
        report = ViewerQualityService.Analyze(XisfImageData.FromSamples(Enumerable.Repeat(0.2f, 1024).ToArray(), [32, 32], 1), 0, 5);
        Assert.Equal("Unavailable", Value(report, "Noise σ estimate"));
        Assert.Equal("Unavailable", Value(report, "Detected star candidates"));
        Assert.Equal("Unavailable", Value(report, "Median star peak SNR estimate"));
    }

    [Theory]
    [InlineData(XisfPixelLayout.Planar, false)]
    [InlineData(XisfPixelLayout.Planar, true)]
    [InlineData(XisfPixelLayout.Interleaved, false)]
    [InlineData(XisfPixelLayout.Interleaved, true)]
    public void SelectedChannelRespectsLayoutAndByteOrder(XisfPixelLayout layout, bool bigEndian)
    {
        // FromSamples input layout and endian describe serialization; input values stay CLR values.
        ushort[] samples = layout == XisfPixelLayout.Planar ? [1, 2, 3, 4, 100, 200, 300, 400] : [1, 100, 2, 200, 3, 300, 4, 400];
        var image = XisfImageData.FromSamples(samples, [2, 2], 2, layout, bigEndian);
        var report = ViewerQualityService.Analyze(image, 1, 5);
        Assert.Equal("100 / 400", Value(report, "Minimum / maximum"));
        Assert.Equal(250, Number(report, "Background estimate"));
    }

    [Fact]
    public void SaturatedAndBorderStarsAreExcludedFromShapeMeasurements()
    {
        var values = Noise(128 * 128, 0.1, 0.001);
        AddStar(values, 128, 128, 3, 50, 0.3, 1.5, 1.5);
        AddStar(values, 128, 128, 60, 60, 1.1, 1.5, 1.5);
        var report = ViewerQualityService.Analyze(XisfImageData.FromSamples(values, [128, 128], 1), 0, 5);
        Assert.Equal("0", Value(report, "Detected star candidates"));
        Assert.Equal("1", Value(report, "Saturated peak candidates"));
    }

    [Fact]
    public void InvalidInputsAndCancellationAreExplicit()
    {
        var image = XisfImageData.FromSamples(new byte[] { 1 }, [1, 1], 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => ViewerQualityService.Analyze(image, 1, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => ViewerQualityService.Analyze(image, 0, double.NaN));
        Assert.Throws<NotSupportedException>(() => ViewerQualityService.Analyze(XisfImageData.FromSamples(new byte[] { 1 }, [1, 1, 1], 1), 0, 5));
        Assert.Throws<NotSupportedException>(() => ViewerQualityService.Analyze(XisfImageData.FromSamples(new System.Numerics.Complex[] { new(1, 2) }, [1, 1], 1), 0, 5));
        Assert.Throws<OperationCanceledException>(() => ViewerQualityService.Analyze(image, 0, 5, new CancellationToken(true)));
    }

    [Fact]
    public async Task WorkbenchClearsResultsOnSelectionUnloadAndEditsButKeepsThemForDisplayStretch()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf");
        try
        {
            XisfWriter.Write(path, [XisfImageData.FromSamples(Noise(32 * 32, 0.1, 0.001), [32, 32], 1), XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1)]);
            var vm = new WorkbenchViewModel(_ => Task.CompletedTask) { FilePath = path };
            Assert.False(vm.Quality.AnalyzeCommand.CanExecute(null));
            await vm.LoadCommand.ExecuteAsync(null);
            await vm.Quality.AnalyzeCommand.ExecuteAsync(null);
            Assert.NotEmpty(vm.Quality.Entries);
            var entries = vm.Quality.Entries;
            vm.AutoStretch = false; await vm.ApplyCommand.ExecuteAsync(null);
            Assert.Same(entries, vm.Quality.Entries);
            await vm.ApplyMetadataCommand.ExecuteAsync(null); Assert.Empty(vm.Quality.Entries);
            await vm.Quality.AnalyzeCommand.ExecuteAsync(null);
            vm.SelectedImageIndex = 1; await vm.SelectImageCommand.ExecuteAsync(null);
            Assert.Empty(vm.Quality.Entries);
            await vm.Quality.AnalyzeCommand.ExecuteAsync(null);
            vm.Quality.DetectionThreshold = "NaN"; await vm.Quality.AnalyzeCommand.ExecuteAsync(null);
            Assert.Empty(vm.Quality.Entries); Assert.Contains("threshold", vm.Status); Assert.False(vm.IsBusy);
            await vm.UnloadCommand.ExecuteAsync(null);
            Assert.Empty(vm.Quality.Channels); Assert.False(vm.Quality.AnalyzeCommand.CanExecute(null));
        }
        finally { File.Delete(path); }
    }

    private static double[] Noise(int count, double background, double sigma)
    {
        var random = new Random(42);
        return Enumerable.Range(0, count).Select(_ => background + sigma * Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble())).ToArray();
    }
    private static void AddStar(double[] samples, int width, int height, int x, int y, double amplitude, double sx, double sy)
    {
        for (int row = 0; row < height; row++)
        for (int col = 0; col < width; col++)
            samples[row * width + col] += amplitude * Math.Exp(-0.5 * (Math.Pow((col - x) / sx, 2) + Math.Pow((row - y) / sy, 2)));
    }
    private static string Value(QualityReport report, string name) => report.Entries.Single(e => e.Name == name).Value;
    private static double Number(QualityReport report, string name, string suffix = "")
    {
        string value = Value(report, name);
        return double.Parse(suffix.Length == 0 ? value : value[..^suffix.Length], CultureInfo.CurrentCulture);
    }
}
