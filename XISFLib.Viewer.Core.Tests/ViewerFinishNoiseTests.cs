using XISFLib.Viewer;

namespace XISFLib.Viewer.Core.Tests;

public class ViewerFinishNoiseTests
{
    [Theory]
    [InlineData(1, XisfPixelLayout.Planar)]
    [InlineData(3, XisfPixelLayout.Interleaved)]
    public void NoiseReductionRetainsBackgroundDustAndStarsAndDoesNotModifySource(int colors, XisfPixelLayout layout)
    {
        const int width = 240, height = 240;
        var random = new Random(721); var samples = new float[width * height * colors];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        for (int c = 0; c < colors; c++)
        {
            float value = (float)(0.03 + (random.NextDouble() - 0.5) * 0.008
                + (x >= 90 && x < 120 && y >= 70 && y < 150 ? -0.008 : 0)
                + 0.10 * Math.Exp(-((x - 160d) * (x - 160) + (y - 130d) * (y - 130)) / 8));
            samples[layout == XisfPixelLayout.Planar ? c * width * height + y * width + x : (y * width + x) * colors + c] = value;
        }
        foreach (var (x, y) in new[] { (40, 40), (39, 40), (41, 40), (40, 39), (40, 41) })
        for (int c = 0; c < colors; c++)
            samples[layout == XisfPixelLayout.Planar ? c * width * height + y * width + x : (y * width + x) * colors + c]
                = x == 40 && y == 40 ? 0.015f : 0.03f;
        var source = XisfImageData.FromSamples(samples, [width, height], colors, layout, true);
        byte[] original = source.RawData.ToArray();
        var before = ViewerFinishService.Create(source, false, default, null, new(false, false, false));
        var after = ViewerFinishService.Create(source, false, default, null, new(false, false, true));
        Assert.Contains("multiscale", after.Summary);
        var a = Measure(before, 15, 15, 50, 50); var b = Measure(after, 15, 15, 50, 50);
        Assert.True(b.Scatter < a.Scatter * 0.8, $"Noise {a.Scatter} → {b.Scatter}");
        Assert.InRange(Math.Abs(a.Mean - b.Mean), 0, 3);
        Assert.True(after.Pixels[(40 * width + 40) * 4] > before.Pixels[(40 * width + 40) * 4] + 5);
        Assert.True(b.Mean - Measure(after, 95, 80, 20, 60).Mean > 0.8 * (a.Mean - Measure(before, 95, 80, 20, 60).Mean));
        Assert.InRange((int)after.Pixels[(130 * width + 160) * 4], before.Pixels[(130 * width + 160) * 4] - 15, 255);
        // The tile boundary crosses flat sky: it must not introduce a brightness band.
        Assert.InRange(Math.Abs(Measure(after, 182, 15, 8, 60).Mean - Measure(after, 194, 15, 8, 60).Mean), 0, 3);
        Assert.Equal(original, source.RawData.ToArray());
        Assert.All(Enumerable.Range(0, width * height), p => Assert.Equal(255, after.Pixels[p * 4 + 3]));
        if (colors == 1)
            Assert.All(Enumerable.Range(0, width * height), p => Assert.Equal(after.Pixels[p * 4], after.Pixels[p * 4 + 2]));
        Assert.DoesNotContain("multiscale", ViewerFinishService.Create(source, true).Summary);
    }

    [Fact]
    public void CornerCoverageCropRetainsMostOfTheFieldAndLeavesTheSourceUntouched()
    {
        const int side = 512;
        var samples = Enumerable.Repeat(0.03f, side * side).ToArray();
        for (int y = 0; y < side; y++)
        for (int x = 0; x < side; x++)
            if ((x < 64 && y > 350) || (x >= 448 && y < 150)) samples[y * side + x] = 0;
        var source = XisfImageData.FromSamples(samples, [side, side], 1);
        byte[] original = source.RawData.ToArray();
        var result = ViewerFinishService.Create(source);
        Assert.InRange(result.Width, 360, 400); Assert.InRange(result.Height, 490, 512);
        Assert.All(Enumerable.Range(0, result.Width * result.Height), p => Assert.True(result.Pixels[p * 4] > 0));
        Assert.Equal(original, source.RawData.ToArray());
        var raster = ViewerFinishService.Create(source, true);
        Assert.Equal(502, raster.Width); Assert.Equal(502, raster.Height);
    }

    [Fact]
    public void CancellingNoiseStageStopsBeforePublishingAndPreservesSource()
    {
        var random = new Random(2);
        var source = XisfImageData.FromSamples(Enumerable.Range(0, 4096).Select(_ => (float)(0.03 + random.NextDouble() * 0.01)).ToArray(), [64, 64], 1);
        var original = source.RawData.ToArray();
        using var cancellation = new CancellationTokenSource();
        var progress = new Receiver(p => { if (p.Stage == "Reducing grain and color noise") cancellation.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => ViewerFinishService.Create(source, false, cancellation.Token, progress));
        Assert.Equal(original, source.RawData.ToArray());
    }

    private sealed class Receiver(Action<FinishProgress> action) : IProgress<FinishProgress>
    { public void Report(FinishProgress value) => action(value); }

    private static (double Mean, double Scatter) Measure(FinishedPicture image, int x, int y, int w, int h)
    {
        var values = new List<double>();
        for (int yy = y; yy < y + h; yy++)
        for (int xx = x; xx < x + w; xx++) values.Add(image.Pixels[(yy * image.Width + xx) * 4]);
        double mean = values.Average();
        return (mean, Math.Sqrt(values.Average(v => (v - mean) * (v - mean))));
    }
}
