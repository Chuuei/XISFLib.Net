using System.Text;
using System.Xml.Linq;

namespace XISFLib.Tests;

public sealed class BatchConversionTests : IDisposable
{
    private readonly string _folder = Path.Combine(AppContext.BaseDirectory, "conversion-" + Guid.NewGuid().ToString("N"));

    public BatchConversionTests() => Directory.CreateDirectory(_folder);

    [Fact]
    public void ConvertsAllImagesAndPreservesMetadataWithChecksums()
    {
        XisfImageData first = XisfImageData.FromSamples(new ushort[] { 0, 65535 }, [2, 1], 1);
        first.Properties["Observation:Object:Name"] = "Native target";
        first.Properties["Observation:ExposureTime"] = 120.5;
        first.Properties["Custom:Unicode"] = "nébuleuse 星";
        first.Properties["Custom:Vector"] = new double[] { 1.25, 2.5 };
        first.FitsKeywords.Add(new XisfFitsKeyword("OBJECT", "'M31'", "Original target"));
        first.FitsKeywords.Add(new XisfFitsKeyword("CRVAL1", "123.45", "WCS"));
        first.FitsKeywords.Add(new XisfFitsKeyword("HISTORY", "First", null));
        first.FitsKeywords.Add(new XisfFitsKeyword("HISTORY", "Second", null));
        XisfImageData second = XisfImageData.FromSamples(new float[] { 0.25f }, [1, 1], 1);
        second.Properties["Observation:Object:Name"] = "M42";
        XisfWriter.Write(Path.Combine(_folder, "capture.XISF"), [first, second]);

        Assert.Equal(0, BatchConversion.Run([_folder], new StringWriter(), new StringWriter()));

        FitsDocument result = FitsReader.Load(Path.Combine(_folder, "capture.fits"));
        Assert.Equal(3, result.Hdus.Count);
        Assert.Equal([0d, 65535d], Assert.IsType<FitsImage>(result.Hdus[0].Content).GetPhysicalSamples());
        Assert.Equal([0.25d], Assert.IsType<FitsImage>(result.Hdus[1].Content).GetPhysicalSamples());
        Assert.Equal("M31", result.Hdus[0].Header.GetValueOrDefault<string>("OBJECT"));
        Assert.Equal("Original target", result.Hdus[0].Header.FindFirst("OBJECT")!.Comment);
        Assert.Equal("M42", result.Hdus[1].Header.GetValueOrDefault<string>("OBJECT"));
        Assert.Equal(120.5, result.Hdus[0].Header.GetValueOrDefault<double>("EXPTIME"));
        Assert.Equal(123.45, result.Hdus[0].Header.GetValueOrDefault<double>("CRVAL1"));
        Assert.Equal(new[] { "First", "Second" }, result.Hdus[0].Header.Find("HISTORY").Take(2).Select(card => card.Value));
        Assert.All(result.Hdus, hdu => { Assert.True(hdu.ChecksumValid); Assert.True(hdu.DataChecksumValid); });
        Assert.Equal("XISFMETA", result.Hdus[2].Header.GetValueOrDefault<string>("EXTNAME"));
        XElement archive = XElement.Parse(Encoding.UTF8.GetString(result.Hdus[2].Data.Span));
        Assert.Contains("nébuleuse 星", archive.Value);
        XElement vector = archive.Elements("Image").First().Element("Properties")!.Elements("Property")
            .Single(element => (string?)element.Attribute("id") == "Custom:Vector").Element("Array")!;
        Assert.Equal("2", (string?)vector.Attribute("dimensions"));
        Assert.Equal(new[] { "1.25", "2.5" }, vector.Elements("Value").Select(value => value.Value));
    }

    [Fact]
    public void ContinuesAfterBadFilesAndNeverOverwritesOrRecurses()
    {
        XisfImageData image = XisfImageData.FromSamples(new byte[] { 7 }, [1, 1], 1);
        File.WriteAllText(Path.Combine(_folder, "a-broken.xisf"), "invalid");
        XisfWriter.Write(Path.Combine(_folder, "b-existing.xisf"), [image]);
        File.WriteAllText(Path.Combine(_folder, "b-existing.fits"), "keep me");
        XisfWriter.Write(Path.Combine(_folder, "c-valid.xisf"), [image]);
        string child = Directory.CreateDirectory(Path.Combine(_folder, "child")).FullName;
        XisfWriter.Write(Path.Combine(child, "nested.xisf"), [image]);
        StringWriter error = new();

        Assert.Equal(1, BatchConversion.Run([_folder], new StringWriter(), error));

        Assert.Equal("keep me", File.ReadAllText(Path.Combine(_folder, "b-existing.fits")));
        Assert.True(File.Exists(Path.Combine(_folder, "c-valid.fits")));
        Assert.False(File.Exists(Path.Combine(_folder, "a-broken.fits")));
        Assert.False(File.Exists(Path.Combine(child, "nested.fits")));
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
        Assert.Contains("a-broken.xisf", error.ToString());
        Assert.Contains("not overwritten", error.ToString());
    }

    [Fact]
    public void UsesGlobalPropertiesWithoutOverridingImageProperties()
    {
        XisfImageData image = XisfImageData.FromSamples(new byte[] { 1 }, [1, 1], 1);
        image.Properties["Observation:Object:Name"] = "Image target";
        using MemoryStream stream = new();
        XisfWriter.Write(stream, [image]);
        stream.Position = 0;
        XisfDocument document = XisfReader.Load(stream);
        XNamespace ns = document.Xml.Root!.Name.Namespace;
        document = document.EditXml(xml =>
        {
            xml.Root!.Add(new XElement(ns + "Property", new XAttribute("id", "Observation:Object:Name"),
                new XAttribute("type", "String"), "Global target"));
            xml.Root.Add(new XElement(ns + "Property", new XAttribute("id", "Instrument:Telescope:Name"),
                new XAttribute("type", "String"), "Global telescope"));
        });
        using MemoryStream updated = new();
        XisfWriter.Write(updated, document);
        updated.Position = 0;

        FitsDocument result = BatchConversion.CreateDocument(XisfReader.Load(updated));

        Assert.Equal("Image target", result.Hdus[0].Header.GetValueOrDefault<string>("OBJECT"));
        Assert.Equal("Global telescope", result.Hdus[0].Header.GetValueOrDefault<string>("TELESCOP"));
        Assert.Contains("Global target", Encoding.UTF8.GetString(result.Hdus[^1].Data.Span));
    }

    [Fact]
    public void RejectsComplexPixelsWithoutLeavingOutput()
    {
        XisfImageData image = XisfImageData.FromSamples(new System.Numerics.Complex[] { new(1, 2) }, [1, 1], 1);
        XisfWriter.Write(Path.Combine(_folder, "complex.xisf"), [image]);
        StringWriter error = new();

        Assert.Equal(1, BatchConversion.Run([_folder], new StringWriter(), error));

        Assert.Contains("Complex", error.ToString());
        Assert.False(File.Exists(Path.Combine(_folder, "complex.fits")));
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
    }

    [Fact]
    public void ValidatesArgumentsAndAcceptsEmptyFolders()
    {
        Assert.Equal(2, BatchConversion.Run([], new StringWriter(), new StringWriter()));
        Assert.Equal(2, BatchConversion.Run([Path.Combine(_folder, "missing")], new StringWriter(), new StringWriter()));
        Assert.Equal(0, BatchConversion.Run([_folder], new StringWriter(), new StringWriter()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    [InlineData("3")]
    public void ConvertsMultipleFilesWithAccurateTotals(string? parallelConversions)
    {
        for (int index = 0; index < 24; index++)
        {
            XisfImageData image = XisfImageData.FromSamples(new ushort[] { (ushort)index }, [1, 1], 1);
            image.Properties["Observation:Object:Name"] = $"Target {index}";
            XisfWriter.Write(Path.Combine(_folder, $"capture-{index}.xisf"), [image]);
        }
        for (int index = 0; index < 12; index++)
            File.WriteAllText(Path.Combine(_folder, $"broken-{index}.xisf"), "invalid");
        StringWriter output = new(), error = new();
        string[] args = parallelConversions is null ? [_folder] : [_folder, parallelConversions];

        Assert.Equal(1, BatchConversion.Run(args, output, error));

        Assert.Contains("Converted 24; failed 12; found 36 XISF file(s).", output.ToString());
        Assert.Equal(25, output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(12, error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
        for (int index = 0; index < 24; index++)
        {
            FitsDocument result = FitsReader.Load(Path.Combine(_folder, $"capture-{index}.fits"));
            Assert.Equal(new[] { (double)index }, Assert.IsType<FitsImage>(result.Hdus[0].Content).GetPhysicalSamples());
            Assert.Equal($"Target {index}", result.Hdus[0].Header.GetValueOrDefault<string>("OBJECT"));
            Assert.All(result.Hdus, hdu => { Assert.True(hdu.ChecksumValid); Assert.True(hdu.DataChecksumValid); });
        }
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    [InlineData("")]
    public void RejectsInvalidParallelConversionCounts(string count)
    {
        StringWriter error = new();

        Assert.Equal(2, BatchConversion.Run([_folder, count], new StringWriter(), error));

        Assert.Contains("Usage:", error.ToString());
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public void RejectsExtraArguments() =>
        Assert.Equal(2, BatchConversion.Run([_folder, "2", "extra"], new StringWriter(), new StringWriter()));

    public void Dispose() => Directory.Delete(_folder, true);
}
