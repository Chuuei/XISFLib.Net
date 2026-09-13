namespace XISFLib.Tests;

public class XisfUtilityTests
{
    [Fact]
    public void ReadsRegionsAndChannelsWithoutLayoutCopies()
    {
        ushort[] samples =
        [
            1, 10, 100, 2, 20, 200, 3, 30, 300,
            4, 40, 400, 5, 50, 500, 6, 60, 600
        ];
        XisfImageData image = XisfImageData.FromSamples(samples, [3, 2], 3, XisfPixelLayout.Interleaved);

        Assert.Equal([2, 20, 200, 3, 30, 300, 5, 50, 500, 6, 60, 600], image.ReadRegion<int>(1, 0, 2, 2));
        Assert.Equal([10, 20, 30, 40, 50, 60], image.ReadChannel<int>(1));
        Assert.Equal([2, 3, 5, 6, 20, 30, 50, 60, 200, 300, 500, 600],
            image.ReadRegion<int>(1, 0, 2, 2, XisfPixelLayout.Planar));
    }

    [Fact]
    public void PreservesUInt64PrecisionWhenReadingRegionsAndChannels()
    {
        ulong[] samples = [9_007_199_254_740_993, ulong.MaxValue];
        XisfImageData image = XisfImageData.FromSamples(samples, [2, 1], 1);

        Assert.Equal(samples, image.ReadRegion<ulong>(0, 0, 2, 1));
        Assert.Equal(samples, image.ReadChannel<ulong>(0));
    }

    [Fact]
    public void CalculatesRobustStatisticsAndHistogramPerChannel()
    {
        XisfImageData image = XisfImageData.FromSamples(new float[] { 0, 1, 2, 3, float.NaN }, [5, 1], 1);

        XisfChannelStatistics statistics = Assert.Single(image.CalculateStatistics());
        XisfHistogram histogram = image.CalculateHistogram(0, 3, 0, 3);

        Assert.Equal(4, statistics.FiniteCount);
        Assert.Equal(1, statistics.NonFiniteCount);
        Assert.Equal(1.5, statistics.Mean, 12);
        Assert.Equal(1.5, statistics.Median, 12);
        Assert.Equal(1, statistics.MedianAbsoluteDeviation, 12);
        Assert.Equal([1, 1, 2], histogram.Bins);
        Assert.Equal(1, histogram.NonFiniteCount);
    }

    [Fact]
    public void ExtractsUnifiedMetadataAndTransformsTanWcsRoundTrip()
    {
        XisfImageData image = XisfImageData.FromSamples(new byte[] { 0 }, [1, 1], 1);
        image.FitsKeywords.Add(new("OBJECT", "M31", null));
        image.FitsKeywords.Add(new("EXPTIME", "120.5", null));
        image.FitsKeywords.Add(new("CRPIX1", "100", null));
        image.FitsKeywords.Add(new("CRPIX2", "200", null));
        image.FitsKeywords.Add(new("CRVAL1", "10", null));
        image.FitsKeywords.Add(new("CRVAL2", "20", null));
        image.FitsKeywords.Add(new("CD1_1", "-0.001", null));
        image.FitsKeywords.Add(new("CD1_2", "0", null));
        image.FitsKeywords.Add(new("CD2_1", "0", null));
        image.FitsKeywords.Add(new("CD2_2", "0.001", null));
        image.FitsKeywords.Add(new("CTYPE1", "RA---TAN", null));
        image.FitsKeywords.Add(new("CTYPE2", "DEC--TAN", null));

        AstronomyImageMetadata metadata = AstronomyMetadata.From(image);
        Assert.Equal("M31", metadata.ObjectName);
        Assert.Equal(120.5, metadata.ExposureSeconds);
        Assert.True(CelestialWorldCoordinateSystem.TryCreate(image.FitsKeywords, out CelestialWorldCoordinateSystem? wcs));
        (double ra, double dec) = wcs!.PixelToSky(120, 220);
        (double x, double y) = wcs.SkyToPixel(ra, dec);
        Assert.Equal(120, x, 8);
        Assert.Equal(220, y, 8);
        Assert.Equal(3.6, wcs.PixelScaleArcseconds, 8);
    }
}