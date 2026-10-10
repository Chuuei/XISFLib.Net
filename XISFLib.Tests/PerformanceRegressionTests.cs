using System.Buffers.Binary;
using System.Numerics;

namespace XISFLib.Tests;

public class PerformanceRegressionTests
{
    [Theory]
    [InlineData(XisfCompression.Zlib)]
    [InlineData(XisfCompression.Lz4)]
    [InlineData(XisfCompression.Lz4Hc)]
    [InlineData(XisfCompression.Zstd)]
    public void SubblocksDecodeIntoDisjointOutputSlicesWithShuffleTails(XisfCompression compression)
    {
        ushort[] values = Enumerable.Range(0, 1031).Select(i => (ushort)(i * 53)).ToArray();
        var source = XisfImageData.FromSamples(values, [1031, 1], 1, isBigEndian: true);
        using var stream = new MemoryStream();
        XisfWriter.Write(stream, [source], new() { Compression = compression, Shuffle = true, CompressionSubblockSize = 7, Checksum = XisfChecksum.Sha256 });
        stream.Position = 0;
        var actual = XisfReader.Load(stream).Images[0];
        Assert.Equal(source.RawData.ToArray(), actual.ReadRawData());
        Assert.Equal(values, actual.GetSamples<ushort>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BulkSamplesPreserveFloatingPointBitsAndComplexComponentOrder(bool bigEndian)
    {
        int[] bits = [0, int.MinValue, 0x7f800000, unchecked((int)0xff800000), 0x7fc12345, 1];
        float[] values = bits.Select(BitConverter.Int32BitsToSingle).ToArray();
        var image = XisfImageData.FromSamples(values, [values.Length, 1], 1, isBigEndian: bigEndian);
        byte[] expected = new byte[values.Length * 4];
        for (int i = 0; i < bits.Length; i++)
            if (bigEndian) BinaryPrimitives.WriteInt32BigEndian(expected.AsSpan(i * 4), bits[i]);
            else BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(i * 4), bits[i]);
        Assert.Equal(expected, image.RawData.ToArray());
        Assert.Equal(bits, RoundTrip(image).GetSamples<float>().Select(BitConverter.SingleToInt32Bits));

        XisfComplex32[] complex = [new(values[4], values[5]), new(values[1], values[2])];
        var complexImage = XisfImageData.FromSamples(complex, [2, 1], 1, isBigEndian: bigEndian);
        Assert.Equal(expected.AsSpan(16, 8).ToArray().Concat(expected.AsSpan(4, 8).ToArray()), complexImage.RawData.ToArray());
        Complex[] doubles = [new(BitConverter.Int64BitsToDouble(0x7ff8123456789abc), -0d)];
        var doubleImage = XisfImageData.FromSamples(doubles, [1, 1], 1, isBigEndian: bigEndian);
        var decoded = RoundTrip(doubleImage).GetComplexSamples()[0];
        Assert.Equal(BitConverter.DoubleToInt64Bits(doubles[0].Real), BitConverter.DoubleToInt64Bits(decoded.Real));
        Assert.Equal(BitConverter.DoubleToInt64Bits(doubles[0].Imaginary), BitConverter.DoubleToInt64Bits(decoded.Imaginary));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactIntegerReadsRetainFullPrecisionAndConversionsRemainChecked(bool bigEndian)
    {
        ulong[] values = [0, 1, (1UL << 53) + 1, ulong.MaxValue];
        var image = RoundTrip(XisfImageData.FromSamples(values, [4, 1], 1, isBigEndian: bigEndian));
        Assert.Equal(values, image.GetSamples<ulong>());
        Assert.Throws<OverflowException>(() => image.GetSamples<byte>());
    }

    [Fact]
    public void BulkPropertyMatricesPreserveQuadAndDoubleBitPatterns()
    {
        var image = XisfImageData.FromSamples(new byte[] { 0 }, [1, 1], 1);
        double[,] matrix = { { -0d, BitConverter.Int64BitsToDouble(0x7ff8123456789abc) }, { double.PositiveInfinity, double.Epsilon } };
        XisfFloat128[,] quads = { { new(UInt128.One), new((UInt128.One << 127) | 123) } };
        image.Properties["Test:Matrix"] = matrix;
        image.Properties["Test:Quads"] = quads;
        var actual = RoundTrip(image);
        Assert.Equal(matrix.Cast<double>().Select(BitConverter.DoubleToInt64Bits), Assert.IsType<double[,]>(actual.Properties["Test:Matrix"].Value).Cast<double>().Select(BitConverter.DoubleToInt64Bits));
        Assert.Equal(quads.Cast<XisfFloat128>(), Assert.IsType<XisfFloat128[,]>(actual.Properties["Test:Quads"].Value).Cast<XisfFloat128>());
    }

    [Fact]
    public void CachedColorMatricesTrackChangesToBothPrimaryCoordinateLists()
    {
        double[] x = XisfRgbWorkingSpace.Srgb.ChromaticityCoordinates.ToArray();
        double[] y = XisfRgbWorkingSpace.Srgb.YChromaticityCoordinates.ToArray();
        var space = new XisfRgbWorkingSpace(2.4, true, x, [0.2, 0.7, 0.1]) { YChromaticityCoordinates = y };
        var before = XisfColorSpace.RgbToXyz(0.8, 0.2, 0.4, space);
        x[0] -= 0.02;
        y[1] -= 0.03;
        var fresh = space with { ChromaticityCoordinates = x.ToArray(), YChromaticityCoordinates = y.ToArray() };
        Assert.NotEqual(before, XisfColorSpace.RgbToXyz(0.8, 0.2, 0.4, space));
        Assert.Equal(XisfColorSpace.RgbToXyz(0.8, 0.2, 0.4, fresh), XisfColorSpace.RgbToXyz(0.8, 0.2, 0.4, space));
        Assert.Equal(XisfColorSpace.XyzToRgb(0.3, 0.2, 0.1, fresh), XisfColorSpace.XyzToRgb(0.3, 0.2, 0.1, space));
    }

    [Fact]
    public void AdaptiveSelectionMatchesSortedReferenceForVariedDistributions()
    {
        var random = new Random(8192);
        for (int length = 1; length <= 512; length++)
        foreach (int distribution in Enumerable.Range(0, 5))
        {
            double[] input = Enumerable.Range(0, length).Select(i => distribution switch
            {
                0 => random.NextDouble(), 1 => i / (double)length,
                2 => 1 - i / (double)length, 3 => 0.75, _ => (i % 5) / 4d
            }).Append(double.NaN).Append(double.PositiveInfinity).ToArray();
            double[] sorted = input.Where(double.IsFinite).Order().ToArray();
            double median = Median(sorted);
            double deviation = 1.4826 * Median(sorted.Select(v => Math.Abs(v - median)).Order().ToArray());
            bool inverted = median > 0.5;
            double shadow = !inverted && deviation != 0 ? Math.Clamp(median - 2.8 * deviation, 0, 1) : 0;
            double highlight = inverted && deviation != 0 ? Math.Clamp(median + 2.8 * deviation, 0, 1) : 1;
            double midtones = inverted ? XisfDisplayFunctions.MidtonesTransfer(0.25, highlight - median) : XisfDisplayFunctions.MidtonesTransfer(median - shadow, 0.25);
            if (!double.IsFinite(midtones)) midtones = 0.5;
            var actual = XisfDisplayFunctions.CreateAdaptive([input]);
            Assert.Equal(shadow, actual.Shadows[0]);
            Assert.Equal(highlight, actual.Highlights[0]);
            Assert.Equal(midtones, actual.Midtones[0]);
        }
    }

    private static double Median(double[] sorted) => sorted.Length % 2 == 0 ? (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2 : sorted[sorted.Length / 2];

    private static XisfImage RoundTrip(XisfImageData image)
    {
        using var stream = new MemoryStream();
        XisfWriter.Write(stream, [image]);
        stream.Position = 0;
        return XisfReader.Load(stream).Images[0];
    }
}
