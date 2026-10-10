using System.Drawing;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingColor = System.Drawing.Color;

namespace XISFLib.Windows.Tests;

public class XisfWindowsExtensionsTests
{
    [Theory]
    [InlineData("Gray", XisfPixelLayout.Planar, false)]
    [InlineData("Gray", XisfPixelLayout.Planar, true)]
    [InlineData("Gray", XisfPixelLayout.Interleaved, false)]
    [InlineData("Gray", XisfPixelLayout.Interleaved, true)]
    [InlineData("RGB", XisfPixelLayout.Planar, false)]
    [InlineData("RGB", XisfPixelLayout.Planar, true)]
    [InlineData("RGB", XisfPixelLayout.Interleaved, false)]
    [InlineData("RGB", XisfPixelLayout.Interleaved, true)]
    [InlineData("CIELab", XisfPixelLayout.Planar, false)]
    [InlineData("CIELab", XisfPixelLayout.Planar, true)]
    [InlineData("CIELab", XisfPixelLayout.Interleaved, false)]
    [InlineData("CIELab", XisfPixelLayout.Interleaved, true)]
    public void ParallelRenderingMatchesSequentialRowsIncludingAlphaAndDisplayFunctions(string colorSpace, XisfPixelLayout layout, bool bigEndian)
    {
        const int width = 512, height = 513, pixels = width * height;
        int channels = colorSpace == "Gray" ? 2 : 4;
        float[] values = Enumerable.Range(0, pixels * channels).Select(i => (i % 67) switch
        {
            0 => float.NaN, 1 => float.PositiveInfinity, 2 => float.NegativeInfinity,
            _ => (i % 257) / 256f
        }).ToArray();
        var display = new XisfDisplayFunction([0.3, 0.4, 0.6, 0.2], [0, 0.1, 0.2, 0], [1, 0.9, 0.8, 1], [-0.1, 0, 0, 0], [1.1, 1, 1, 1]);
        var options = new XisfStretchOptions { UseDisplayFunction = true };
        var image = XisfImageData.FromSamples(values, [width, height], channels, layout, bigEndian);
        image.ColorSpace = colorSpace;
        image.DisplayFunction = display;
        byte[] expected = new byte[pixels * 4];
        float[] row = new float[width * channels];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            for (int channel = 0; channel < channels; channel++)
                row[x * channels + channel] = layout == XisfPixelLayout.Planar
                    ? values[channel * pixels + y * width + x] : values[(y * width + x) * channels + channel];
            var reference = XisfImageData.FromSamples(row, [width, 1], channels, XisfPixelLayout.Interleaved, bigEndian);
            reference.ColorSpace = colorSpace;
            reference.DisplayFunction = display;
            reference.ToBgra32Pixels(options).CopyTo(expected, y * width * 4);
        }
        Assert.Equal(expected, image.ToBgra32Pixels(options));
        BitmapSource bitmap = image.ToBitmapSource(options);
        byte[] actual = new byte[expected.Length];
        bitmap.CopyPixels(actual, width * 4, 0);
        Assert.True(bitmap.IsFrozen);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ParallelRenderingPreservesArgumentExceptionForInvalidLabWorkingSpace()
    {
        var image = XisfImageData.FromSamples(new float[512 * 512 * 3], [512, 512], 3);
        image.ColorSpace = "CIELab";
        image.RgbWorkingSpace = XisfRgbWorkingSpace.Srgb with { Gamma = -1 };
        Assert.Throws<ArgumentException>(() => image.ToBgra32Pixels());
    }

    [Fact]
    public void CreatesWindowsFormsBitmapFromNormalizedRgbSamples()
    {
        XisfImage image = LoadImage([0f, 0.5f, 1f, 1f, 0f, 0.25f], 2, 1, 3);

        using Bitmap bitmap = image.ToBitmap();

        Assert.Equal(2, bitmap.Width);
        Assert.Equal(1, bitmap.Height);
        Assert.Equal(DrawingColor.FromArgb(255, 0, 128, 255), bitmap.GetPixel(0, 0));
        Assert.Equal(DrawingColor.FromArgb(255, 255, 0, 64), bitmap.GetPixel(1, 0));
    }

    [Fact]
    public void CreatesFrozenWpfBitmapSourceFromGrayscaleAndAlphaSamples()
    {
        XisfImage image = LoadImage([0.25f, 0.5f, 1f, 0f], 2, 1, 2);

        BitmapSource bitmap = image.ToBitmapSource();
        byte[] pixels = new byte[8];
        bitmap.CopyPixels(pixels, 8, 0);

        Assert.Equal(PixelFormats.Bgra32, bitmap.Format);
        Assert.True(bitmap.IsFrozen);
        Assert.Equal([64, 64, 64, 128, 255, 255, 255, 0], pixels);
    }

    [Fact]
    public void AutoStretchMakesDarkLinearImageVisibleWithoutStretchingAlpha()
    {
        XisfImage image = LoadImage([0.01f, 0.5f, 0.02f, 1f, 0.03f, 0.25f, 0.04f, 0f], 4, 1, 2);

        byte[] linear = image.ToBgra32Pixels();
        byte[] stretched = image.ToBgra32Pixels(XisfStretchOptions.Auto);

        Assert.True(stretched[4] > linear[4] * 4);
        Assert.Equal(linear[3], stretched[3]);
        Assert.Equal(linear[7], stretched[7]);
        Assert.Equal(linear[11], stretched[11]);
        Assert.Equal(linear[15], stretched[15]);
    }

    [Fact]
    public void AutoStretchCanLinkRgbChannelsToPreserveColorBalance()
    {
        XisfImage image = LoadImage([0.01f, 0.02f, 0.03f, 0.02f, 0.04f, 0.06f, 0.03f, 0.06f, 0.09f], 3, 1, 3);

        byte[] stretched = image.ToBgra32Pixels(XisfStretchOptions.Auto);

        Assert.True(stretched[8] > stretched[9]);
        Assert.True(stretched[9] > stretched[10]);
        Assert.Equal(255, stretched[11]);
    }

    [Fact]
    public void RejectsNonTwoDimensionalAndComplexImages()
    {
        XisfImageData volume = XisfImageData.FromSamples(new byte[8], [2, 2, 2], 1);
        XisfImageData complex = XisfImageData.FromSamples([new System.Numerics.Complex(1, 2)], [1, 1], 1);

        Assert.Throws<NotSupportedException>(() => LoadImage(volume).ToBitmap());
        Assert.Throws<NotSupportedException>(() => LoadImage(complex).ToBitmapSource());
    }

    [Fact]
    public void AppliesOrientationAndXisfDefaultResolution()
    {
        XisfImageData data = XisfImageData.FromSamples(new byte[] { 10, 20 }, [2, 1], 1);
        data.Orientation = "90";
        XisfImage image = LoadImage(data);

        BitmapSource bitmap = image.ToBitmapSource();

        Assert.Equal(1, bitmap.PixelWidth);
        Assert.Equal(2, bitmap.PixelHeight);
        Assert.Equal(72, bitmap.DpiX);
        Assert.Equal(72, bitmap.DpiY);
        Assert.Equal([20, 20, 20, 255, 10, 10, 10, 255], image.ToBgra32Pixels());
    }

    [Fact]
    public void ConvertsCieLabToSrgb()
    {
        XisfImageData data = XisfImageData.FromSamples(new float[] { 1, 128f / 255, 128f / 255 }, [1, 1], 3, XisfPixelLayout.Interleaved);
        data.ColorSpace = "CIELab";

        byte[] pixels = LoadImage(data).ToBgra32Pixels();

        Assert.InRange(pixels[0], 250, 255);
        Assert.InRange(pixels[1], 250, 255);
        Assert.InRange(pixels[2], 250, 255);
    }

    [Fact]
    public void AppliesStoredDisplayFunctionOnRequest()
    {
        XisfImageData data = XisfImageData.FromSamples(new float[] { 0.25f, 0.5f, 0.75f }, [3, 1], 1);
        data.DisplayFunction = new XisfDisplayFunction(
            [0.5, 0.5, 0.5, 0.5], [0.25, 0.25, 0.25, 0.25], [0.75, 0.75, 0.75, 0.75],
            [0, 0, 0, 0], [1, 1, 1, 1]);

        byte[] pixels = LoadImage(data).ToBgra32Pixels(XisfStretchOptions.DisplayFunction);

        Assert.Equal(0, pixels[0]);
        Assert.InRange(pixels[4], 127, 128);
        Assert.Equal(255, pixels[8]);
    }

    [Fact]
    public void XisfImageDataMatchesLoadedImageForAllRealSampleFormatsAndLayouts()
    {
        XisfImageData[] images =
        [
            XisfImageData.FromSamples(new byte[] { 0, 128, 255, 255, 64, 0 }, [2, 1], 3, XisfPixelLayout.Interleaved),
            XisfImageData.FromSamples(new ushort[] { 0, ushort.MaxValue, 32768, 16384, ushort.MaxValue, 0 }, [2, 1], 3, XisfPixelLayout.Planar, true),
            XisfImageData.FromSamples(new uint[] { 0, 2147483648, uint.MaxValue, uint.MaxValue, 1073741824, 0 }, [2, 1], 3, XisfPixelLayout.Interleaved),
            XisfImageData.FromSamples(new ulong[] { 0, ulong.MaxValue, 9223372036854775808, 4611686018427387904, ulong.MaxValue, 0 }, [2, 1], 3, XisfPixelLayout.Planar, true),
            XisfImageData.FromSamples(new float[] { 0, 1, 0.5f, 0.25f, 1, 0 }, [2, 1], 3, XisfPixelLayout.Interleaved, true),
            XisfImageData.FromSamples(new double[] { 0, 1, 0.5, 0.25, 1, 0 }, [2, 1], 3, XisfPixelLayout.Planar)
        ];

        foreach (XisfImageData data in images)
            Assert.Equal(LoadImage(data).ToBgra32Pixels(), data.ToBgra32Pixels());
    }

    [Fact]
    public void XisfImageDataBitmapOverloadsMatchLoadedImageMetadataAndPixels()
    {
        XisfImageData data = XisfImageData.FromSamples(new float[] { 0.01f, 0.5f, 0.02f, 1, 0.03f, 0.25f, 0.04f, 0 }, [4, 1], 2, XisfPixelLayout.Interleaved);
        data.Orientation = "90";
        data.Resolution = new XisfResolution(40, 20, "cm");
        XisfImage image = LoadImage(data);

        using Bitmap expectedBitmap = image.ToBitmap(XisfStretchOptions.Auto);
        using Bitmap actualBitmap = data.ToBitmap(XisfStretchOptions.Auto);
        BitmapSource expectedSource = image.ToBitmapSource(XisfStretchOptions.Auto);
        BitmapSource actualSource = data.ToBitmapSource(XisfStretchOptions.Auto);

        Assert.Equal(expectedBitmap.Size, actualBitmap.Size);
        Assert.Equal(expectedBitmap.HorizontalResolution, actualBitmap.HorizontalResolution);
        Assert.Equal(expectedBitmap.VerticalResolution, actualBitmap.VerticalResolution);
        Assert.Equal(expectedSource.PixelWidth, actualSource.PixelWidth);
        Assert.Equal(expectedSource.PixelHeight, actualSource.PixelHeight);
        Assert.Equal(expectedSource.DpiX, actualSource.DpiX);
        Assert.Equal(expectedSource.DpiY, actualSource.DpiY);
        Assert.Equal(image.ToBgra32Pixels(XisfStretchOptions.Auto), data.ToBgra32Pixels(XisfStretchOptions.Auto));
    }

    [Fact]
    public void NormalizedLabUsesD50AndAppliesBoundsAfterColorConversion()
    {
        var data = XisfImageData.FromSamples(new double[] { 0.5, 0.5, 0.5 }, [1, 1], 3, XisfPixelLayout.Interleaved);
        data.ColorSpace = "CIELab";
        data.LowerBound = 0.25;
        data.UpperBound = 0.75;
        var rgb = XisfColorSpace.LabToRgb(0.5, 0.5, 0.5);
        byte expected = (byte)Math.Round(Math.Clamp((rgb.Red - 0.25) / 0.5, 0, 1) * 255);
        byte[] rendered = data.ToBgra32Pixels();
        Assert.InRange(rendered[0], expected - 1, expected + 1);
        Assert.InRange(rendered[1], expected - 1, expected + 1);
        Assert.InRange(rendered[2], expected - 1, expected + 1);
        Assert.Equal(rendered, LoadImage(data).ToBgra32Pixels());
    }

    private static XisfImage LoadImage(float[] samples, int width, int height, int channels) =>
        LoadImage(XisfImageData.FromSamples(samples, [width, height], channels, XisfPixelLayout.Interleaved));

    private static XisfImage LoadImage(XisfImageData data)
    {
        using MemoryStream stream = new();
        XisfWriter.Write(stream, [data]);
        stream.Position = 0;
        return Assert.Single(XisfReader.Load(stream).Images);
    }
}
