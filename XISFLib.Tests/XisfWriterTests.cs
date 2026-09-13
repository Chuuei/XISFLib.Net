using System.Numerics;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace XISFLib.Tests;

public class XisfWriterTests
{
    [Fact]
    public void WritesTypedSamplesToStreamAndPreservesMetadata()
    {
        ushort[] samples = [1, 10, 2, 20];
        XisfImageData image = XisfImageData.FromSamples(samples, [2, 1], 2, XisfPixelLayout.Interleaved);
        image.Id = "light";
        image.ColorSpace = "RGB";
        image.ImageType = "LightFrame";
        image.Properties["Observation:ExposureTime"] = 30.5;
        image.FitsKeywords.Add(new XisfFitsKeyword("OBJECT", "M31", "Target"));
        XisfTableData table = new() { Id = "measurements", Caption = "Measurements" };
        table.Fields.Add(new("name", "String"));
        table.Fields.Add(new("value", "Float64"));
        table.Rows.Add(["FWHM", 2.4]);
        image.Tables.Add(table);

        using MemoryStream output = new();
        XisfWriter.Write(output, [image], new XisfWriterOptions
        {
            Compression = XisfCompression.Zlib,
            Shuffle = true,
            Checksum = XisfChecksum.Sha256,
            CreatorApplication = "Writer tests"
        });

        output.Position = 0;
        XisfDocument document = XisfReader.Load(output);
        XisfImage actual = Assert.Single(document.Images);
        Assert.Equal("Writer tests", document.Metadata["XISF:CreatorApplication"].Value);
        Assert.Equal("light", actual.Id);
        Assert.Equal("Light", actual.ImageType);
        Assert.Equal(XisfPixelLayout.Interleaved, actual.PixelStorage);
        Assert.Equal(samples, actual.GetSamples<ushort>(XisfPixelLayout.Interleaved));
        Assert.Equal(30.5, actual.Properties["Observation:ExposureTime"].Value);
        Assert.Equal("M31", Assert.Single(actual.FitsKeywords).Value);
        Assert.Equal(2.4, Assert.Single(Assert.Single(actual.Tables).Rows).Cells[1].Value);
        Assert.Contains("sha256:", actual.Xml.Attribute("checksum")?.Value);
        Assert.Contains("zlib+sh:", actual.Xml.Attribute("compression")?.Value);
    }

    [Fact]
    public void WritesCommonMultidimensionalArraysAndComplexSamples()
    {
        byte[,] grayscale = { { 1, 2 }, { 3, 4 } };
        float[,,] rgb =
        {
            { { 0.1f, 0.2f, 0.3f }, { 0.4f, 0.5f, 0.6f } }
        };
        Complex[] complex = [new(1, -2), new(3, -4)];

        XisfImageData grayImage = XisfImageData.FromArray(grayscale);
        XisfImageData rgbImage = XisfImageData.FromArray(rgb, 0, 1);
        XisfImageData complexImage = XisfImageData.FromSamples(complex, [2, 1], 1);

        using MemoryStream output = new();
        XisfWriter.Write(output, [grayImage, rgbImage, complexImage]);
        output.Position = 0;
        IReadOnlyList<XisfImage> images = XisfReader.Load(output).Images;

        Assert.Equal([1, 2, 3, 4], images[0].GetSamples<byte>());
        Assert.Equal([2, 2], images[0].Dimensions);
        Assert.Equal([0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f], images[1].GetSamples<float>(XisfPixelLayout.Interleaved));
        Assert.Equal([0d, 1d], [images[1].LowerBound, images[1].UpperBound]);
        Assert.Equal(complex, images[2].GetComplexSamples());
    }

    [Fact]
    public void WritesRawSamplesToAFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"xisf-writer-{Guid.NewGuid():N}.xisf");
        try
        {
            XisfImageData image = XisfImageData.FromRaw([7, 9], [2, 1], 1, XisfSampleFormat.UInt8);
            XisfWriter.Write(path, [image], new XisfWriterOptions { Compression = XisfCompression.Lz4 });

            Assert.Equal([7, 9], Assert.Single(XisfReader.Load(path).Images).GetSamples<int>());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void StreamsPathBackedZlibAttachmentsAndCreatesEditableImages()
    {
        string path = Path.Combine(Path.GetTempPath(), $"xisf-streaming-{Guid.NewGuid():N}.xisf");
        byte[] samples = new byte[1024 * 1024];
        new Random(42).NextBytes(samples);
        try
        {
            XisfImageData source = XisfImageData.FromSamples(samples, [1024, 1024], 1);
            source.Id = "streamed";
            XisfWriter.Write(path, [source], new XisfWriterOptions
            {
                Compression = XisfCompression.Zlib,
                Checksum = XisfChecksum.Sha256
            });

            XisfImage loaded = Assert.Single(XisfReader.Load(path).Images);
            XisfImageData editable = XisfImageData.FromImage(loaded);

            Assert.Equal(samples, loaded.ReadRawData());
            Assert.Equal(samples, editable.RawData.ToArray());
            Assert.Equal("streamed", editable.Id);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void WritesConformantStringComplexVectorAndMatrixProperties()
    {
        XisfImageData image = XisfImageData.FromSamples(new byte[] { 1 }, [1], 1);
        image.Properties["Text"] = "  significant text  ";
        image.Properties["Complex"] = new Complex(1.25, -2.5);
        image.Properties["Vector"] = new[] { 1, 2, 3 };
        image.Properties["Matrix"] = new int[,] { { 1, 2 }, { 3, 4 } };

        using MemoryStream output = new();
        XisfWriter.Write(output, [image], new XisfWriterOptions { Compression = XisfCompression.None });
        output.Position = 0;
        XisfDocument document = XisfReader.Load(output);
        XisfImage actual = Assert.Single(document.Images);

        Assert.Equal("  significant text  ", actual.Properties["Text"].Value);
        Assert.Null(actual.Properties["Text"].Xml.Attribute("value"));
        Assert.Equal("(1.25,-2.5)", actual.Properties["Complex"].Xml.Attribute("value")?.Value);
        Assert.Equal([1, 2, 3], Assert.IsType<int[]>(actual.Properties["Vector"].Value));
        Assert.NotNull(actual.Properties["Vector"].Xml.Attribute("location"));
        Assert.Equal("3", actual.Properties["Vector"].Xml.Attribute("length")?.Value);
        int[,] matrix = Assert.IsType<int[,]>(actual.Properties["Matrix"].Value);
        Assert.Equal(2, matrix.GetLength(0));
        Assert.Equal(2, matrix.GetLength(1));
        Assert.Equal(4, matrix[1, 1]);
        Assert.Equal("2", actual.Properties["Matrix"].Xml.Attribute("rows")?.Value);
        Assert.Equal("2", actual.Properties["Matrix"].Xml.Attribute("columns")?.Value);
        Assert.DoesNotContain(document.Validate(), diagnostic => diagnostic.Severity == XisfDiagnosticSeverity.Error);
    }

    [Fact]
    public void PreservesExplicitPropertyTypesDimensionsAndAnnotations()
    {
        XisfImageData image = XisfImageData.FromSamples(new byte[] { 1 }, [1], 1);
        image.Properties["PCL:AstrometricSolution:LinearTransformationMatrix"] = new XisfPropertyData(
            "F64Matrix", new double[,] { { 1, 2 }, { 3, 4 } }, "%.15g", "Astrometric transform");

        using MemoryStream output = new();
        XisfWriter.Write(output, [image], new XisfWriterOptions { Compression = XisfCompression.Zlib });
        output.Position = 0;
        XisfProperty property = Assert.Single(XisfReader.Load(output).Images).Properties["PCL:AstrometricSolution:LinearTransformationMatrix"];

        Assert.Equal("F64Matrix", property.Type);
        Assert.Equal("%.15g", property.Format);
        Assert.Equal("Astrometric transform", property.Comment);
        Assert.Equal("2", property.Xml.Attribute("rows")?.Value);
        Assert.Equal("2", property.Xml.Attribute("columns")?.Value);
        Assert.Equal(new double[,] { { 1, 2 }, { 3, 4 } }, Assert.IsType<double[,]>(property.Value));
    }

    [Fact]
    public void RoundTripsRepresentativeAstrometricPropertyDeclarations()
    {
        XisfImageData image = XisfImageData.FromSamples(new byte[] { 1 }, [1], 1);
        image.Properties["PCL:AstrometricSolution:ProjectionSystem"] = new XisfPropertyData("String", "Gnomonic", null, "Projection");
        image.Properties["PCL:AstrometricSolution:ReferenceCelestialCoordinates"] = new XisfPropertyData("F64Vector", new[] { 312.5, 31.7 });
        image.Properties["PCL:AstrometricSolution:CelestialPoleNativeCoordinates"] = new XisfPropertyData("F64Vector", new[] { 180.0, 45.0 });
        image.Properties["PCL:AstrometricSolution:LinearTransformationMatrix"] = new XisfPropertyData("F64Matrix", new double[,] { { -0.00065, -0.00002 }, { -0.00002, 0.00065 } }, "%.15g");
        image.Properties["PCL:AstrometricSolution:SplineWorldTransformation:LinearApproximation"] = new XisfPropertyData("F64Matrix", new double[,] { { 1, 0, 0 }, { 0, 1, 0 } });
        image.Properties["PCL:AstrometricSolution:SplineWorldTransformation:PointGridInterpolation:ImageToNative:GridX"] = new XisfPropertyData("F64Matrix", new double[,] { { 1, 2 }, { 3, 4 } });
        image.Properties["PCL:AstrometricSolution:SplineWorldTransformation:ControlPoints:Image"] = new XisfPropertyData("F64Vector", new[] { 1.0, 2.0, 3.0, 4.0 });
        image.Properties["PCL:AstrometricSolution:SplineWorldTransformation:MaxSplinePoints"] = new XisfPropertyData("UInt32", 2500u);

        using MemoryStream output = new();
        XisfWriter.Write(output, [image], new XisfWriterOptions { Compression = XisfCompression.Zlib });
        output.Position = 0;
        XisfImage actual = Assert.Single(XisfReader.Load(output).Images);

        Assert.NotEmpty(image.Properties);
        foreach ((string id, object? value) in image.Properties)
        {
            XisfPropertyData expected = Assert.IsType<XisfPropertyData>(value);
            XisfProperty property = actual.Properties[id];
            Assert.Equal(expected.Type, property.Type);
            Assert.Equal(expected.Format, property.Format);
            Assert.Equal(expected.Comment, property.Comment);
            Assert.Equal((expected.Value as Array)?.Rank, (property.Value as Array)?.Rank);
            Assert.Equivalent(expected.Value, property.Value, strict: true);
            if (expected.Value is Array expectedArray && expectedArray.Rank == 2)
            {
                Assert.Equal(expectedArray.GetLength(0).ToString(), property.Xml.Attribute("rows")?.Value);
                Assert.Equal(expectedArray.GetLength(1).ToString(), property.Xml.Attribute("columns")?.Value);
            }
        }
    }

    [Fact]
    public void WritesDistributedHeaderAndIndexedDataBlocks()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xisf-distributed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string header = Path.Combine(directory, "image.xish");
        string blocks = Path.Combine(directory, "image.xisb");
        try
        {
            XisfImageData image = XisfImageData.FromSamples(new ushort[] { 1, 2, 3 }, [3], 1);
            image.Properties["Samples"] = new[] { 4, 5 };

            XisfWriter.WriteDistributed(header, blocks, [image], new XisfWriterOptions { Compression = XisfCompression.Zlib });
            XisfDocument document = XisfReader.Load(header);

            Assert.Equal([1, 2, 3], Assert.Single(document.Images).GetSamples<int>());
            Assert.Equal([4, 5], Assert.IsType<int[]>(document.Images[0].Properties["Samples"].Value));
            Assert.StartsWith("path(@header_dir/image.xisb):", document.Images[0].Xml.Attribute("location")?.Value);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void RewritesLoadedDocumentWithoutLosingUnknownMetadata()
    {
        XisfImageData image = XisfImageData.FromSamples(new byte[] { 7, 8 }, [2], 1);
        using MemoryStream first = new();
        XisfWriter.Write(first, [image], new XisfWriterOptions { Compression = XisfCompression.Zlib });
        first.Position = 0;
        XisfDocument loaded = XisfReader.Load(first);
        loaded.Xml.Root!.Add(new XElement(XName.Get("Custom", "urn:test"), new XAttribute("answer", 42)));

        using MemoryStream second = new();
        XisfWriter.Write(second, loaded);
        second.Position = 0;
        XisfDocument rewritten = XisfReader.Load(second);

        Assert.Equal([7, 8], Assert.Single(rewritten.Images).GetSamples<int>());
        Assert.Equal("42", rewritten.Xml.Root!.Element(XName.Get("Custom", "urn:test"))?.Attribute("answer")?.Value);
    }

    [Fact]
    public void WritesAttachmentOffsetsAsPlainDecimalIntegerExpressions()
    {
        XisfImageData image = XisfImageData.FromSamples(new float[] { 0.1f, 0.2f, 0.3f }, [1, 1], 3);
        image.Properties["Samples"] = new[] { 1, 2, 3 };

        using MemoryStream output = new();
        XisfWriter.Write(output, [image], new XisfWriterOptions { Compression = XisfCompression.Zlib });
        output.Position = 0;
        XisfDocument document = XisfReader.Load(output);

        string[] locations = document.Xml.Descendants()
            .Select(element => (string?)element.Attribute("location"))
            .Where(location => location?.StartsWith("attachment:", StringComparison.Ordinal) == true)
            .Cast<string>()
            .ToArray();
        Assert.NotEmpty(locations);
        Assert.All(locations, location =>
        {
            string offset = location.Split(':')[1];
            Assert.False(offset.Length > 1 && offset[0] == '0', $"Attachment offset '{offset}' has leading zeroes.");
        });
    }

    [Fact]
    public void CreatesAndPersistsXmlDigitalSignature()
    {
        using MemoryStream source = new();
        XisfWriter.Write(source, [XisfImageData.FromSamples(new byte[] { 1 }, [1], 1)]);
        source.Position = 0;
        XisfDocument document = XisfReader.Load(source);
        using RSA key = RSA.Create(2048);

        using MemoryStream signed = new();
        XisfWriter.WriteSigned(signed, document, key);
        signed.Position = 0;
        Assert.True(XisfReader.VerifyXmlSignature(XisfReader.Load(signed)));
    }

    [Fact]
    public void RejectsInvalidSampleCountsAndNonWritableStreams()
    {
        Assert.Throws<ArgumentException>(() => XisfImageData.FromSamples(new ushort[3], [2, 1], 2));
        XisfImageData image = XisfImageData.FromSamples(new byte[1], [1, 1], 1);
        Assert.Throws<ArgumentException>(() => XisfWriter.Write(Stream.Null, Array.Empty<XisfImageData>()));
        Assert.Throws<ArgumentException>(() => XisfWriter.Write(new MemoryStream(Array.Empty<byte>(), false), [image]));
    }
}