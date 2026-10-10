using System.Security.Cryptography;
using System.Text.Json;

namespace XISFLib.Tests;

public class DisplayRendererTests
{
    // Captured from independent pre-extraction Windows binaries, not the shared renderer.
    [Fact]
    public void RendererMatchesAllPreMigrationHashes()
    {
        var cases = JsonSerializer.Deserialize<Baseline[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "RenderingBaseline.json")))!;
        Assert.Equal(288, cases.Length);
        foreach (var test in cases)
        {
            int channels = test.Color == "Gray" ? 2 : 4;
            var values = Enumerable.Range(0, 12 * channels).Select(i => (i % 19) switch
            { 0 => float.NaN, 1 => float.PositiveInfinity, 2 => float.NegativeInfinity, _ => (i % 17) / 16f }).ToArray();
            var image = XisfImageData.FromSamples(values, [4, 3], channels, Enum.Parse<XisfPixelLayout>(test.Layout), test.BigEndian);
            image.ColorSpace = test.Color;
            image.Orientation = test.Orientation;
            image.DisplayFunction = new([0.3, 0.4, 0.6, 0.2], [0, 0.1, 0.2, 0], [1, 0.9, 0.8, 1], [-0.1, 0, 0, 0], [1.1, 1, 1, 1]);
            var options = test.Mode == "Linear" ? null : test.Mode == "Auto" ? XisfStretchOptions.Auto : XisfStretchOptions.DisplayFunction;
            var result = XisfDisplayRenderer.Render(image, options);
            Assert.Equal(test.Sha256, Convert.ToHexString(SHA256.HashData(result.Pixels)));
            bool rotated = test.Orientation.StartsWith("90") || test.Orientation.StartsWith("-90");
            Assert.Equal(rotated ? 3 : 4, result.Width);
            Assert.Equal(rotated ? 4 : 3, result.Height);
            Assert.Equal(result.Width * 4, result.Stride);
        }
    }

    [Fact]
    public void RenderedPixelsAreOwnedAndDpiUsesExistingRules()
    {
        var image = XisfImageData.FromSamples(new byte[] { 127 }, [1, 1], 1);
        image.Resolution = new(100, 50, "cm");
        var result = XisfDisplayRenderer.Render(image);
        Assert.Equal(254, result.DpiX);
        Assert.Equal(127, result.DpiY);
        Assert.Equal(new byte[] { 127, 127, 127, 255 }, result.Pixels);
        result.Pixels[0] = 0;
        Assert.Equal(127, XisfDisplayRenderer.Render(image).Pixels[0]);
        image.Resolution = null;
        Assert.Equal(72, XisfDisplayRenderer.Render(image).DpiX);
    }

    private sealed record Baseline(string Color, string Orientation, string Layout, bool BigEndian, string Mode, string Sha256);
}
