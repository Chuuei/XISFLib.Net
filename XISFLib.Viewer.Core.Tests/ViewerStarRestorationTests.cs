using XISFLib.Viewer;

namespace XISFLib.Viewer.Core.Tests;

public class ViewerStarRestorationTests
{
    [Fact]
    public void APlainBroadHaloAndCloseBlendAreLeftAlone()
    {
        const int width = 192, height = 192;
        var random = new Random(91); var samples = new float[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            samples[y * width + x] = (float)(0.03 + (random.NextDouble() - 0.5) * 0.001
                + 0.5 * Math.Exp(-((x - 60d) * (x - 60) + (y - 70d) * (y - 70)) / 98)
                + 0.02 * Math.Exp(-((x - 130d) * (x - 130) + (y - 140d) * (y - 140)) / 6)
                + 0.02 * Math.Exp(-((x - 140d) * (x - 140) + (y - 140d) * (y - 140)) / 6));
        var source = XisfImageData.FromSamples(samples, [width, height], 1);
        var before = ViewerFinishService.Create(source, false, default, null, new(false, false));
        var after = ViewerFinishService.Create(source);
        Assert.DoesNotContain("deconvolution", after.Summary); Assert.DoesNotContain("spike attenuation", after.Summary);
        Assert.Equal(before.Pixels, after.Pixels);
    }

    [Theory]
    [InlineData(XisfPixelLayout.Planar, false)]
    [InlineData(XisfPixelLayout.Interleaved, true)]
    public void DeconvolutionNarrowsAnIsolatedStarWithoutChangingTheBackgroundOrSource(XisfPixelLayout layout, bool bigEndian)
    {
        const int width = 128, height = 128;
        var random = new Random(42); var samples = new float[width * height * 3];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        for (int c = 0; c < 3; c++)
        {
            float value = (float)(0.03 + (random.NextDouble() - 0.5) * 0.001
                + (0.006 - c * 0.0005) * Math.Exp(-((x - 64d) * (x - 64) + (y - 64d) * (y - 64)) / (2 * 1.8 * 1.8)));
            samples[layout == XisfPixelLayout.Planar ? c * width * height + y * width + x : (y * width + x) * 3 + c] = value;
        }
        var image = XisfImageData.FromSamples(samples, [width, height], 3, layout, bigEndian);
        byte[] original = image.RawData.ToArray();
        var before = ViewerFinishService.Create(image, false, default, null, new(false, false));
        var after = ViewerFinishService.Create(image, false, default, null, new(true, false));
        Assert.Contains("deconvolution of 1 star", after.Summary);
        double beforeWidth = Width(before, 64, 64), afterWidth = Width(after, 64, 64);
        Assert.True(afterWidth < beforeWidth * 0.97, $"Expected a narrower star: {beforeWidth} → {afterWidth}.");
        for (int y = 5; y < 35; y++)
        for (int x = 5; x < 35; x++)
            Assert.Equal(before.Pixels[(y * width + x) * 4], after.Pixels[(y * width + x) * 4]);
        Assert.Equal(original, image.RawData.ToArray());
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(32d)]
    public void OppositeThinSpikesAreAttenuatedWithCoreAndNearbyStarPreserved(double angleDegrees)
    {
        const int width = 240, height = 240, cx = 120, cy = 120;
        double angle = angleDegrees * Math.PI / 180, cos = Math.Cos(angle), sin = Math.Sin(angle);
        var random = new Random(5); var samples = new float[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            double dx = x - cx, dy = y - cy, along = dx * cos + dy * sin, across = -dx * sin + dy * cos;
            double spikes = Math.Abs(along) < 80 ? 0.009 * Math.Exp(-across * across / 2) * Math.Exp(-Math.Abs(along) / 60) : 0;
            samples[y * width + x] = (float)(0.03 + (random.NextDouble() - 0.5) * 0.001
                + 0.4 * Math.Exp(-(dx * dx + dy * dy) / 8) + spikes
                + 0.02 * Math.Exp(-((x - 180d) * (x - 180) + (y - 150d) * (y - 150)) / 6));
        }
        var image = XisfImageData.FromSamples(samples, [width, height], 1);
        var before = ViewerFinishService.Create(image, false, default, null, new(false, false));
        var after = ViewerFinishService.Create(image, false, default, null, new(false, true));
        Assert.Contains("spike attenuation around 1 bright star", after.Summary);
        double a = RayMean(before, cx, cy, cos, sin), b = RayMean(after, cx, cy, cos, sin);
        Assert.True(b < a - 3, $"Expected weaker rays: {a} → {b}.");
        Assert.Equal(before.Pixels[(cy * width + cx) * 4], after.Pixels[(cy * width + cx) * 4]);
        Assert.Equal(before.Pixels[(150 * width + 180) * 4], after.Pixels[(150 * width + 180) * 4]);
    }

    [Fact]
    public void SaturatedStarsHotPixelsAndBroadSmoothEmissionAreNotDeconvolvedOrTreatedAsSpikes()
    {
        const int width = 128, height = 128;
        var random = new Random(6); var samples = new float[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            samples[y * width + x] = (float)Math.Min(1, 0.03 + (random.NextDouble() - 0.5) * 0.001
                + 1.4 * Math.Exp(-((x - 40d) * (x - 40) + (y - 40d) * (y - 40)) / 6)
                + 0.01 * Math.Exp(-((x - 90d) * (x - 90) + (y - 85d) * (y - 85)) / 200));
        samples[90 * width + 30] = 0.8f;
        var image = XisfImageData.FromSamples(samples, [width, height], 1);
        var before = ViewerFinishService.Create(image, false, default, null, new(false, false));
        var after = ViewerFinishService.Create(image);
        Assert.DoesNotContain("deconvolution", after.Summary); Assert.DoesNotContain("spike attenuation", after.Summary);
        Assert.Equal(before.Pixels, after.Pixels);
    }

    private static double Width(FinishedPicture image, int cx, int cy)
    {
        double sky = Enumerable.Range(0, 900).Select(i => (double)image.Pixels[((10 + i / 30) * image.Width + 10 + i % 30) * 4]).Order().ElementAt(450);
        double cutoff = sky + (image.Pixels[(cy * image.Width + cx) * 4] - sky) * 0.15;
        double sum = 0, moment = 0;
        for (int dy = -8; dy <= 8; dy++)
        for (int dx = -8; dx <= 8; dx++)
        {
            double weight = Math.Max(0, image.Pixels[((cy + dy) * image.Width + cx + dx) * 4] - cutoff);
            sum += weight; moment += weight * (dx * dx + dy * dy);
        }
        return Math.Sqrt(moment / (2 * sum));
    }

    private static double RayMean(FinishedPicture image, int cx, int cy, double cos, double sin)
    {
        double sum = 0;
        for (int r = 25; r <= 65; r++)
            sum += image.Pixels[((cy + (int)Math.Round(r * sin)) * image.Width + cx + (int)Math.Round(r * cos)) * 4];
        return sum / 41;
    }
}
