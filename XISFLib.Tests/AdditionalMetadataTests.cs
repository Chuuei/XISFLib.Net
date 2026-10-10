using System.Reflection;

namespace XISFLib.Tests;

public class AdditionalMetadataTests
{
    [Fact]
    public void FitsTypedMetadataRoundTripsWithCommentaryAndWcs()
    {
        var hdu = FitsHdu.CreateImage(FitsImage.FromPhysicalSamples(new byte[] { 7 }, [1, 1]), true);
        hdu.Header.Set(FitsKnownKeywords.Standard.OBJECT, "Barnard's Star");
        hdu.Header.Set(FitsKnownKeywords.Common.EXPTIME, 30.5, "seconds");
        hdu.Header.Set(FitsKnownKeywords.Standard.DATE_OBS, "2026-10-10");
        hdu.Header.Set(FitsKnownKeywords.Astrophotography.XBINNING, 2L);
        hdu.Header.Set(FitsKnownKeywords.Wcs.CD(1, 2, 'A'), 0.25);
        hdu.Header.Set(FitsKnownKeywords.Wcs.PV(1, 0), 1.5);
        hdu.Header.Add(FitsKnownKeywords.Standard.HISTORY, "First step");
        hdu.Header.Add(FitsKnownKeywords.Standard.HISTORY, "Second step");
        Assert.Throws<InvalidOperationException>(() => hdu.Header.Set(FitsKnownKeywords.Standard.HISTORY, "Overwrite"));
        Assert.Throws<InvalidOperationException>(() => hdu.Header.Set(FitsKnownKeywords.Standard.BITPIX, 16L));
        using var stream = new MemoryStream();
        FitsWriter.Write(stream, new FitsDocument([hdu]));
        stream.Position = 0;
        var header = FitsReader.Load(stream).Hdus[0].Header;
        Assert.Equal("Barnard's Star", header.GetRequiredValue(FitsKnownKeywords.Standard.OBJECT));
        Assert.Equal(30.5, header.GetRequiredValue(FitsKnownKeywords.Common.EXPTIME));
        Assert.Equal("2026-10-10", header.GetRequiredValue(FitsKnownKeywords.Standard.DATE_OBS));
        Assert.Equal(2L, header.GetRequiredValue(FitsKnownKeywords.Astrophotography.XBINNING));
        Assert.Equal(0.25, header.GetRequiredValue(FitsKnownKeywords.Wcs.CD(1, 2, 'A')));
        Assert.Equal(1.5, header.GetRequiredValue(FitsKnownKeywords.Wcs.PV(1, 0)));
        Assert.Equal(new[] { "First step", "Second step" }, header.Find(FitsKnownKeywords.Standard.HISTORY).Select(c => c.Value));
        Assert.True(header.Remove(FitsKnownKeywords.Standard.HISTORY));
        Assert.Empty(header.Find(FitsKnownKeywords.Standard.HISTORY));
    }

    [Fact]
    public void FitsConventionsAndIncompatibleValuesAreNotGuessed()
    {
        var header = new FitsHeader();
        Assert.False(header.TryGetValue(FitsKnownKeywords.Common.EXPTIME, out double _));
        Assert.Equal(-1, header.GetValueOrDefault(FitsKnownKeywords.Common.EXPTIME, -1));
        Assert.Throws<KeyNotFoundException>(() => header.GetRequiredValue(FitsKnownKeywords.Common.EXPTIME));
        header.Set("EXPTIME", "3.05D1");
        Assert.Equal(30.5, header.GetRequiredValue(FitsKnownKeywords.Common.EXPTIME));
        header.Set("EXPTIME", "unavailable");
        Assert.False(header.TryGetValue(FitsKnownKeywords.Common.EXPTIME, out double _));
        Assert.Throws<FormatException>(() => header.GetValueOrDefault(FitsKnownKeywords.Common.EXPTIME, -1));
        header.Set("XBINNING", Math.BitIncrement(1d));
        Assert.False(header.TryGetValue(FitsKnownKeywords.Astrophotography.XBINNING, out long _));
        header.Set("XBINNING", double.PositiveInfinity);
        Assert.False(header.TryGetValue(FitsKnownKeywords.Astrophotography.XBINNING, out long _));
        header.Set("XBINNING", 2d);
        Assert.Equal(2L, header.GetRequiredValue(FitsKnownKeywords.Astrophotography.XBINNING));
        header.Set("XBINNING", (double)(1L << 60));
        Assert.Equal(1L << 60, header.GetRequiredValue(FitsKnownKeywords.Astrophotography.XBINNING));
        header.Set("XBINNING", (double)long.MaxValue);
        Assert.False(header.TryGetValue(FitsKnownKeywords.Astrophotography.XBINNING, out long _));
        header.Set("APERTURE", 80d);
        Assert.False(header.TryGetValue(FitsKnownKeywords.Common.APERTURE, out string _));
        Assert.Equal(80d, header.GetRequiredValue(FitsKnownKeywords.Examples.APERTURE));
        header.Set("RA", "12:30:00");
        Assert.False(header.TryGetValue(FitsKnownKeywords.Common.RA, out double _));
        Assert.Equal("12:30:00", header.GetRequiredValue(FitsKnownKeywords.Common.RAText));
        header.Set("EXPTIME", true);
        Assert.False(header.TryGetValue(FitsKnownKeywords.Common.EXPTIME, out double _));
    }

    [Fact]
    public void IndexedFactoriesRejectInvalidNames()
    {
        Assert.Equal("NAXIS2", FitsKnownKeywords.Standard.NAXISn(2).Keyword);
        Assert.Equal("TFORM12", FitsKnownKeywords.Standard.TFORM(12).Keyword);
        Assert.Equal("CUNIT2A", FitsKnownKeywords.Wcs.CUNIT(2, 'A').Keyword);
        Assert.Equal("PC99_99Z", FitsKnownKeywords.Wcs.PC(99, 99, 'Z').Keyword);
        Assert.Equal("PS1_0", FitsKnownKeywords.Wcs.PS(1, 0).Keyword);
        Assert.Throws<ArgumentOutOfRangeException>(() => FitsKnownKeywords.Standard.NAXISn(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => FitsKnownKeywords.Standard.TFORM(10000));
        Assert.Throws<ArgumentOutOfRangeException>(() => FitsKnownKeywords.Wcs.PC(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => FitsKnownKeywords.Wcs.PV(1, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => FitsKnownKeywords.Wcs.CUNIT(100));
        Assert.Throws<ArgumentOutOfRangeException>(() => FitsKnownKeywords.Wcs.CUNIT(1, 'a'));
    }

    [Fact]
    public void AdditionalXisfPropertiesRetainObservedTypesAndArrayOwnership()
    {
        var image = XisfImageData.FromSamples(new byte[] { 7 }, [1, 1], 1);
        double[] sigmas = [0.1, 0.2, 0.3];
        image.Properties.Set(XisfAdditionalProperties.Pcl.Noise.Sigmas, sigmas);
        image.Properties.Set(XisfAdditionalProperties.Pcl.Signal.PSFCounts, new uint[] { 1, 2, 3 });
        image.Properties.Set(XisfAdditionalProperties.Instrument.FrameExposureTime, 30.5);
        image.Properties.Set(XisfAdditionalProperties.Observer.Name, "Observer");
        sigmas[0] = 99;
        image.FitsKeywords.Add(XisfFitsKeyword.FromValue(FitsKnownKeywords.Standard.OBJECT, "Barnard's Star"));
        image.FitsKeywords.Add(XisfFitsKeyword.FromValue(FitsKnownKeywords.Standard.HISTORY, "Calibration"));
        using var stream = new MemoryStream();
        XisfWriter.Write(stream, [image]);
        stream.Position = 0;
        var loaded = XisfReader.Load(stream).Images[0];
        Assert.Equal(new double[] { 0.1, 0.2, 0.3 }, loaded.Properties.GetRequiredValue(XisfAdditionalProperties.Pcl.Noise.Sigmas));
        Assert.Equal(new uint[] { 1, 2, 3 }, loaded.Properties.GetRequiredValue(XisfAdditionalProperties.Pcl.Signal.PSFCounts));
        Assert.Equal("Float64", loaded.Properties[XisfAdditionalProperties.Instrument.FrameExposureTime.Id].Type);
        Assert.Equal(30.5, loaded.Properties.GetRequiredValue(XisfAdditionalProperties.Instrument.FrameExposureTime));
        Assert.Equal("Observer", loaded.Properties.GetRequiredValue(XisfAdditionalProperties.Observer.Name));
        Assert.Equal("Observer", AstronomyMetadata.From(loaded).Observer);
        Assert.Equal("'Barnard''s Star'", loaded.FitsKeywords.Single(c => c.Name == "OBJECT").Value);
        Assert.Equal("Calibration", loaded.FitsKeywords.Single(c => c.Name == "HISTORY").Comment);
    }

    [Fact]
    public void CatalogsExposeEvidenceAndAllFixedDefinitions()
    {
        Assert.Equal(91, XisfAdditionalProperties.All.Count);
        Assert.Equal(91, XisfAdditionalProperties.All.Select(k => k.Id).Distinct().Count());
        Assert.DoesNotContain(XisfAdditionalProperties.All, key => XisfKnownProperties.All.Any(k => k.Id == key.Id));
        Assert.Equal(MetadataDefinitionSource.ObservedExamples, XisfAdditionalProperties.Pcl.Noise.Sigmas.Source);
        Assert.Equal(MetadataDefinitionSource.LibraryConvention, XisfAdditionalProperties.FitsStorage.BLANK.Source);
        Assert.Equal(MetadataDefinitionSource.Specification, XisfKnownProperties.Instrument.ExposureTime.Source);
        Assert.Equal(MetadataDefinitionSource.PublishedConvention, FitsKnownKeywords.Common.EXPTIME.Source);
        Assert.Equal("s", FitsKnownKeywords.Common.EXPTIME.Unit);
        Assert.All(FitsKnownKeywords.All, key => Assert.False(string.IsNullOrWhiteSpace(key.SourceReference)));
        Assert.Equal(Keys(typeof(FitsKnownKeywords)).OrderBy(k => k.Keyword).ThenBy(k => k.ValueType.Name),
            FitsKnownKeywords.All.OrderBy(k => k.Keyword).ThenBy(k => k.ValueType.Name));
        Assert.Throws<NotSupportedException>(() => ((IList<FitsKeywordKey>)FitsKnownKeywords.All).Clear());
    }

    private static IEnumerable<FitsKeywordKey> Keys(Type type) => type.GetProperties(BindingFlags.Static | BindingFlags.Public)
        .Where(p => typeof(FitsKeywordKey).IsAssignableFrom(p.PropertyType)).Select(p => (FitsKeywordKey)p.GetValue(null)!)
        .Concat(type.GetNestedTypes(BindingFlags.Public).SelectMany(Keys));
}
