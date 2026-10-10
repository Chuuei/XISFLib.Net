using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace XISFLib.Tests;

public class SpecificationRevisionTests
{
    private static XisfDocument RoundTrip(XisfImageData image, XisfWriterOptions? options = null)
    {
        using MemoryStream stream = new();
        XisfWriter.Write(stream, [image], options);
        stream.Position = 0;
        return XisfReader.Load(stream);
    }
    private static XisfImageData LinearImage(string projection = "Gnomonic", double ra = 123, double dec = 45)
    {
        var image = XisfImageData.FromSamples(new byte[100], [10, 10], 1);
        var p = image.Properties;
        p["AstrometricSolution:Version"] = "1.0";
        p["AstrometricSolution:ProjectionSystem"] = projection;
        p["AstrometricSolution:ReferenceCelestialCoordinates"] = new double[] { ra, dec };
        p["AstrometricSolution:ReferenceImageCoordinates"] = new double[] { 5, 5 };
        p["AstrometricSolution:LinearTransformationMatrix"] = new double[,] { { -0.001, 0 }, { 0, 0.001 } };
        return image;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZstandardRoundTripsImagesAndProperties(bool shuffle)
    {
        ushort[] values = Enumerable.Range(0, 4096).Select(i => (ushort)(i * 7)).ToArray();
        var image = XisfImageData.FromSamples(values, [64, 64], 1);
        image.Properties["Test:Matrix"] = new double[,] { { 1, 2 }, { 3, 4 } };
        var doc = RoundTrip(image, new() { Compression = XisfCompression.Zstd, Shuffle = shuffle });
        var actual = doc.Images[0];
        Assert.Equal(values, actual.GetSamples<ushort>());
        Assert.Equal(4.0, Assert.IsType<double[,]>(actual.Properties["Test:Matrix"].Value)[1, 1]);
        Assert.StartsWith(shuffle ? "zstd+sh:" : "zstd:", actual.Xml.Attribute("compression")!.Value);
    }

    [Fact]
    public void EmptyCollectionsUseEmptyInlineBlocksAndTableCellsSupportArrays()
    {
        var image = XisfImageData.FromSamples(new byte[] { 1 }, [1, 1], 1);
        image.Properties["Test:Empty"] = Array.Empty<double>();
        image.Properties["Test:EmptyMatrix"] = new int[0, 4];
        XisfTableData table = new() { Id = "vectors" };
        table.Fields.Add(new("samples", "F64Vector"));
        table.Fields.Add(new("matrix", "I32Matrix"));
        table.Rows.Add([new double[] { 1, 2, 3 }, new int[,] { { 4, 5 } }]);
        image.Tables.Add(table);
        var actual = RoundTrip(image).Images[0];
        Assert.Empty(Assert.IsType<double[]>(actual.Properties["Test:Empty"].Value));
        Assert.Equal("inline:base64", actual.Properties["Test:Empty"].Xml.Attribute("location")!.Value);
        Assert.Equal(4, Assert.IsType<int[,]>(actual.Properties["Test:EmptyMatrix"].Value).GetLength(1));
        var row = actual.Tables[0].Rows[0];
        Assert.Equal(new double[] { 1, 2, 3 }, Assert.IsType<double[]>(row.Cells[0].Value));
        Assert.Equal(5, Assert.IsType<int[,]>(row.Cells[1].Value)[0, 1]);
    }

    [Theory]
    [InlineData("Gnomonic")]
    [InlineData("Stereographic")]
    [InlineData("ZenithalEqualArea")]
    [InlineData("Orthographic")]
    [InlineData("PlateCarree")]
    [InlineData("Mercator")]
    [InlineData("HammerAitoff")]
    public void AllProjectionsMapTheReferenceAndRoundTripToSubMicroPixelAccuracy(string projection)
    {
        var solution = RoundTrip(LinearImage(projection)).Images[0].AstrometricSolution!;
        Assert.Equal(1, solution.AvailableLayer);
        var reference = solution.ImageToCelestial(5, 5);
        Assert.Equal(123, reference.RightAscension, 10);
        Assert.Equal(45, reference.Declination, 10);
        foreach (var point in new[] { (0.5, 0.5), (9.5, 9.5), (-1000.0, 3000.0) })
        {
            var celestial = solution.ImageToCelestial(point.Item1, point.Item2);
            var image = solution.CelestialToImage(celestial.RightAscension, celestial.Declination);
            Assert.InRange(Math.Abs(image.X - point.Item1), 0, 1e-6);
            Assert.InRange(Math.Abs(image.Y - point.Item2), 0, 1e-6);
        }
    }

    [Theory]
    [InlineData(-90)]
    [InlineData(90)]
    [InlineData(89.99999)]
    [InlineData(-89.99999)]
    public void SphericalRotationHandlesPolesAndLongitudeWrap(double declination)
    {
        var solution = RoundTrip(LinearImage(ra: 359.999, dec: declination)).Images[0].AstrometricSolution!;
        var celestial = solution.ImageToCelestial(3.5, 7.5);
        Assert.InRange(celestial.RightAscension, 0, 360);
        var point = solution.CelestialToImage(celestial.RightAscension, celestial.Declination);
        Assert.InRange(Math.Abs(point.X - 3.5), 0, 1e-6);
        Assert.InRange(Math.Abs(point.Y - 7.5), 0, 1e-6);
    }

    [Fact]
    public void StoredInverseIsUsedAndUnavailableLayersFallBack()
    {
        var image = LinearImage();
        image.Properties["AstrometricSolution:ProjectiveTransformation:ImageToProjection"] = new double[,] { { 2, 0, 1 }, { 0, 3, 2 }, { 0, 0, 1 } };
        image.Properties["AstrometricSolution:ProjectiveTransformation:ProjectionToImage"] = new double[,] { { 7, 0, 11 }, { 0, 9, 13 }, { 0, 0, 1 } };
        image.Properties["AstrometricSolution:DistortionModel:ImageToProjection:BasisFunction"] = "FutureBasis";
        image.Properties["AstrometricSolution:CelestialReferenceSystem"] = "FutureSystem";
        var solution = RoundTrip(image).Images[0].AstrometricSolution!;
        Assert.Equal(2, solution.AvailableLayer);
        Assert.Equal((11.0, 13.0), solution.ProjectionToImage(0, 0));
        Assert.NotEmpty(solution.Diagnostics);
        Assert.Equal("FutureSystem", solution.CelestialReferenceSystem);
        image.Properties["AstrometricSolution:Version"] = "2.0";
        solution = RoundTrip(image).Images[0].AstrometricSolution!;
        Assert.Equal(0, solution.AvailableLayer);
        Assert.Contains("AstrometricSolution:DistortionModel:ImageToProjection:BasisFunction", solution.Properties.Keys);
    }

    [Theory]
    [InlineData("ThinPlateSpline", 2, true, 0.0)]
    [InlineData("VariableOrder", 3, true, 0.0)]
    [InlineData("Gaussian", 1, false, 0.36787944117144233)]
    [InlineData("Multiquadric", 1, false, 1.4142135623730951)]
    [InlineData("InverseMultiquadric", 1, false, 0.7071067811865475)]
    [InlineData("InverseQuadratic", 1, false, 0.5)]
    public void EverySplineBasisMatchesItsAnalyticKernel(string basis, int order, bool polynomial, double expected)
    {
        var image = LinearImage();
        foreach (string direction in new[] { "ImageToProjection", "ProjectionToImage" })
        {
            image.Properties["AstrometricSolution:ProjectiveTransformation:" + direction] = new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
            string prefix = "AstrometricSolution:DistortionModel:" + direction + ":";
            image.Properties[prefix + "BasisFunction"] = basis;
            image.Properties[prefix + "Order"] = order;
            image.Properties[prefix + "Polynomial"] = polynomial;
            image.Properties[prefix + "Terms"] = "Global";
            image.Properties[prefix + "Global:X:Normalization"] = new double[] { 0, 0, 1 };
            image.Properties[prefix + "Global:X:Nodes"] = new double[,] { { 0, 0 } };
            double[] coefficients = new double[1 + (polynomial ? order * (order + 1) / 2 : 0)]; coefficients[0] = 1;
            image.Properties[prefix + "Global:X:Coefficients"] = coefficients;
            image.Properties[prefix + "Global:Y:Coefficients"] = coefficients;
            if (!polynomial) image.Properties[prefix + "Global:X:ShapeParameter"] = 1.0;
        }
        var solution = RoundTrip(image).Images[0].AstrometricSolution!;
        Assert.Equal(3, solution.AvailableLayer);
        Assert.Equal(1 + expected, solution.ImageToProjection(1, 0).X, 12);
        Assert.Equal(expected, solution.ImageToProjection(1, 0).Y, 12);
        Assert.Equal(1 + expected, solution.ProjectionToImage(1, 0).X, 12);
    }

    [Fact]
    public void Binary128PreservesPrecisionSubnormalsAndAllFiniteBitPatterns()
    {
        var value = XisfFloat128.Parse("1.0000000000000000000000000000000002");
        Assert.NotEqual(XisfFloat128.Parse("1").Bits, value.Bits);
        Assert.Equal(value, XisfFloat128.Parse(value.ToString()));
        var random = new Random(417);
        var values = Enumerable.Range(0, 50).Select(_ => { byte[] bytes = new byte[16]; random.NextBytes(bytes); return new XisfFloat128(System.Buffers.Binary.BinaryPrimitives.ReadUInt128LittleEndian(bytes)); }).Where(v => v.IsFinite).ToArray();
        foreach (var number in values.Append(new(UInt128.One)).Append(new((UInt128)0x7ffe << 112)))
            Assert.Equal(number, XisfFloat128.Parse(number.ToString()));
        var image = XisfImageData.FromSamples(new byte[] { 1 }, [1, 1], 1);
        image.Properties["Test:Quad"] = value;
        image.Properties["Test:Quads"] = values;
        image.Properties["Test:Complex"] = new XisfComplex128(value, new(UInt128.One));
        var actual = RoundTrip(image).Images[0];
        Assert.Equal(value, actual.Properties["Test:Quad"].Value);
        Assert.Equal(values, Assert.IsType<XisfFloat128[]>(actual.Properties["Test:Quads"].Value));
        Assert.Equal(image.Properties["Test:Complex"], actual.Properties["Test:Complex"].Value);
    }

    [Fact]
    public void NormativeColorConversionUsesD50AndNeutralHalfChrominance()
    {
        var white = XisfColorSpace.RgbToXyz(1, 1, 1);
        Assert.Equal(XisfColorSpace.WhiteX, white.X, 12);
        Assert.Equal(1, white.Y, 12);
        Assert.Equal(XisfColorSpace.WhiteZ, white.Z, 12);
        var lab = XisfColorSpace.RgbToLab(1, 1, 1);
        Assert.Equal(1, lab.L, 12); Assert.Equal(0.5, lab.A, 12); Assert.Equal(0.5, lab.B, 12);
        foreach (var rgb in new[] { (0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0), (0.1, 0.8, 0.3) })
        {
            lab = XisfColorSpace.RgbToLab(rgb.Item1, rgb.Item2, rgb.Item3);
            var actual = XisfColorSpace.LabToRgb(lab.L, lab.A, lab.B);
            Assert.Equal(rgb.Item1, actual.Red, 12); Assert.Equal(rgb.Item2, actual.Green, 12); Assert.Equal(rgb.Item3, actual.Blue, 12);
        }
        var image = XisfImageData.FromSamples(new byte[] { 1 }, [1, 1], 1);
        image.RgbWorkingSpace = XisfRgbWorkingSpace.Srgb;
        var parsed = RoundTrip(image).Images[0];
        Assert.True(parsed.RgbWorkingSpace!.IsSrgb);
        Assert.Equal("sRGB", parsed.Xml.Elements().Single(e => e.Name.LocalName == "RGBWorkingSpace").Attribute("gamma")!.Value);
        Assert.Equal(3, parsed.RgbWorkingSpace.YChromaticityCoordinates.Count);
    }

    [Fact]
    public void TextScalarsSupportTwoComplementPrefixesNonFiniteTokensAndUtcDefault()
    {
        string xml = "<xisf version='1.0'><Property id='hex' type='Int32' value='0x80E950AB'/><Property id='oct' type='UInt16' value='0o777'/><Property id='bin' type='Int8' value='0b11111111'/><Property id='inf' type='Float64' value='+Inf'/><Property id='nan' type='Float32' value='-nan'/><Property id='time' type='TimePoint' value='2026-01-02T03:04:05'/></xisf>";
        var doc = XisfReader.Load(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
        Assert.Equal(-2132193109, doc.Properties["hex"].Value);
        Assert.Equal((ushort)511, doc.Properties["oct"].Value);
        Assert.Equal((sbyte)-1, doc.Properties["bin"].Value);
        Assert.Equal(double.PositiveInfinity, doc.Properties["inf"].Value);
        Assert.True(float.IsNaN(Assert.IsType<float>(doc.Properties["nan"].Value)));
        Assert.Equal(TimeSpan.Zero, Assert.IsType<DateTimeOffset>(doc.Properties["time"].Value).Offset);
    }
    [Theory]
    [InlineData(XisfDataBlockStorage.Attachment)]
    [InlineData(XisfDataBlockStorage.Embedded)]
    [InlineData(XisfDataBlockStorage.Inline)]
    public void StorageModesSubblocksAlignmentAndUtf8BlocksInteroperate(XisfDataBlockStorage storage)
    {
        var image = XisfImageData.FromSamples(Enumerable.Range(0, 64).Select(i => (ushort)i).ToArray(), [8, 8], 1, isBigEndian: true);
        image.Properties["Test:Text"] = "Unicode \U0001f31f \u0001";
        image.Properties["Test:Vector"] = new double[] { 1, 2, 3, 4 };
        var actual = RoundTrip(image, new() { DataBlockStorage = storage, HexEncoding = true, Compression = XisfCompression.Zstd,
            Shuffle = true, CompressionSubblockSize = 7, BlockAlignmentSize = 1024 }).Images[0];
        Assert.Equal(Enumerable.Range(0, 64).Select(i => (ushort)i), actual.GetSamples<ushort>());
        Assert.Equal(image.Properties["Test:Text"], actual.Properties["Test:Text"].Value);
        Assert.Equal(new double[] { 1, 2, 3, 4 }, actual.Properties["Test:Vector"].Value);
        if (storage == XisfDataBlockStorage.Attachment)
            Assert.Equal(0, long.Parse(actual.Xml.Attribute("location")!.Value.Split(':')[1]) % 1024);
        else Assert.Equal("embedded", actual.Xml.Attribute("location")!.Value);
    }

    [Fact]
    public void UnsupportedObjectsRemainAccessibleAndPreserveOpaqueBlocksWhenSaving()
    {
        var image = LinearImage();
        image.Properties["AstrometricSolution:FutureProperty"] = new double[] { 2, 3 };
        var doc = RoundTrip(image, new() { Compression = XisfCompression.Zstd });
        var property = doc.Images[0].Properties["AstrometricSolution:FutureProperty"];
        doc = doc.EditXml(xml => xml.Descendants().Single(e => (string?)e.Attribute("id") == property.Id).SetAttributeValue("compression", "future:16"));
        using MemoryStream saved = new(); XisfWriter.Write(saved, doc); saved.Position = 0;
        var loaded = XisfReader.Load(saved);
        Assert.False(loaded.Images[0].Properties[property.Id].IsAvailable);
        Assert.Equal(1, loaded.Images[0].AstrometricSolution!.AvailableLayer);
        var editable = XisfImageData.FromImage(loaded.Images[0]);
        var preserved = RoundTrip(editable).Images[0];
        Assert.False(preserved.Properties[property.Id].IsAvailable);
        Assert.Equal("future:16", preserved.Properties[property.Id].Xml.Attribute("compression")!.Value);
        string xml = "<xisf version='1.0' xmlns:ext='urn:test'><Image geometry='1:1' sampleFormat='Future' location='embedded'><Data encoding='hex'>00</Data></Image><ext:Image/><Property id='future' type='Future' value='x'/><Property id='known' type='Int32' value='7'/></xisf>";
        loaded = XisfReader.Load(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
        Assert.Single(loaded.UnavailableObjects); Assert.Empty(loaded.Images);
        Assert.False(loaded.Properties["future"].IsAvailable); Assert.Equal(7, loaded.Properties["known"].Value);
    }

    [Fact]
    public void DetachedSignatureSealsHeaderAndPayloadAndCanBePreserved()
    {
        var doc = RoundTrip(LinearImage(), new() { BlockAlignmentSize = 4096 });
        using var key = System.Security.Cryptography.RSA.Create(2048);
        using MemoryStream output = new(); XisfWriter.WriteSigned(output, doc, key); output.Position = 0;
        var signed = XisfReader.Load(output);
        Assert.NotNull(signed.DetachedSignature);
        Assert.Empty(signed.Xml.Root!.Descendants(XName.Get("Signature", "http://www.w3.org/2000/09/xmldsig#")));
        Assert.True(XisfReader.VerifyXmlSignature(signed));
        Assert.Equal(new byte[100], signed.Images[0].ReadRawData());
        using MemoryStream preserved = new(); XisfWriter.Write(preserved, signed); preserved.Position = 0;
        Assert.True(XisfReader.VerifyXmlSignature(XisfReader.Load(preserved)));
        byte[] tampered = output.ToArray(); tampered[^1] ^= 1;
        Assert.False(XisfReader.VerifyXmlSignature(XisfReader.Load(new MemoryStream(tampered))));
        var edited = signed.EditXml(xml => xml.Root!.SetAttributeValue("changed", "true"));
        Assert.False(edited.HasXmlSignature);
        Assert.False(XisfReader.VerifyXmlSignature(edited));
        Assert.True(XisfReader.VerifyXmlSignature(signed));
    }

    [Fact]
    public void LocalFallbackAndNearestSupportRulesEvaluateAsSpecified()
    {
        var image = LinearImage();
        foreach (string direction in new[] { "ImageToProjection", "ProjectionToImage" })
        {
            image.Properties["AstrometricSolution:ProjectiveTransformation:" + direction] = new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
            string p = "AstrometricSolution:DistortionModel:" + direction + ":";
            image.Properties[p + "BasisFunction"] = "ThinPlateSpline"; image.Properties[p + "Order"] = 2;
            image.Properties[p + "Terms"] = "Local\nFallback";
            image.Properties[p + "Local:Center"] = new double[,] { { 0, 0 }, { 10, 0 } };
            image.Properties[p + "Local:Radius"] = new double[] { 2, 2 };
            image.Properties[p + "Local:X:Normalization"] = new double[,] { { 0, 0, 1 }, { 10, 0, 1 } };
            image.Properties[p + "Local:X:NodeOffsets"] = new int[] { 0, 0, 0 };
            image.Properties[p + "Local:X:Nodes"] = new double[0, 2];
            image.Properties[p + "Local:X:Coefficients"] = new double[] { 10, 0, 0, 20, 0, 0 };
            image.Properties[p + "Local:Y:Coefficients"] = new double[6];
            image.Properties[p + "Fallback:Threshold"] = 0.5;
            image.Properties[p + "Fallback:X:Normalization"] = new double[] { 0, 0, 1 };
            image.Properties[p + "Fallback:X:Nodes"] = new double[0, 2];
            image.Properties[p + "Fallback:X:Coefficients"] = new double[] { 30, 0, 0 };
            image.Properties[p + "Fallback:Y:Coefficients"] = new double[3];
        }
        var model = RoundTrip(image).Images[0].AstrometricSolution!;
        Assert.Equal(3, model.AvailableLayer); Assert.Equal(10, model.ImageToProjection(0, 0).X); Assert.Equal(35, model.ImageToProjection(5, 0).X);
        foreach (string direction in new[] { "ImageToProjection", "ProjectionToImage" })
            image.Properties["AstrometricSolution:DistortionModel:" + direction + ":Terms"] = "Local";
        model = RoundTrip(image).Images[0].AstrometricSolution!;
        Assert.Equal(3, model.AvailableLayer); Assert.Equal(26, model.ImageToProjection(6, 0).X);
    }

    [Theory]
    [InlineData(0.00001, "precision:2;float:auto", "1E-05")]
    [InlineData(-0.00001, "precision:2;float:fixed;sign:force", "0.00")]
    [InlineData(1.5, "precision:2;float:fixed;sign:force;width:8;fill:_;align:center", "__+1.50_")]
    public void FormatTokensApplyToComponentsAndSuppressSignsOnRoundedZero(double value, string format, string expected)
    { Assert.Equal(expected, XisfPropertyFormatter.FormatValue(value, format)); }

    [Fact]
    public void FormatterPreservesQuadPrecisionAndSupportsAllScalarTokens()
    {
        Assert.Equal("001f", XisfPropertyFormatter.FormatValue(31, "base:hex;width:4;fill:0"));
        Assert.Equal("1", XisfPropertyFormatter.FormatValue(true, "bool:numeric"));
        var quad = XisfFloat128.Parse("1.0000000000000000000000000000000002");
        Assert.NotEqual(XisfPropertyFormatter.FormatValue(1.0, "precision:36"), XisfPropertyFormatter.FormatValue(quad, "precision:36"));
        Assert.Equal("[1.00, 2.00]", XisfPropertyFormatter.FormatValue(new double[] { 1, 2 }, "float:fixed;precision:2"));
        Assert.Equal("(1.00, -2.00)", XisfPropertyFormatter.FormatValue(new Complex(1, -2), "float:fixed;precision:2"));
    }

    [Fact]
    public void DisplayFunctionOrderAndAdaptiveInvertedImagesMatchTheEquations()
    {
        Assert.Equal(0.5, XisfDisplayFunctions.MidtonesTransfer(0.25, 0.25));
        Assert.Equal(0.25, XisfDisplayFunctions.Evaluate(0.1, 0.5, 0.2, 0.8, -0.5, 1.5), 12);
        Assert.Equal(0.75, XisfDisplayFunctions.Evaluate(0.9, 0.5, 0.2, 0.8, -0.5, 1.5), 12);
        double[] values = [0.7, 0.75, 0.8, 0.85, 0.9];
        var adaptive = XisfDisplayFunctions.CreateAdaptive([values]);
        Assert.NotEqual(0.5, adaptive.Midtones[0]);
        Assert.InRange(XisfDisplayFunctions.Evaluate(0.8, adaptive), 0.7, 0.9);
    }

    [Fact]
    public void FileUrlsAndCustomSchemesRespectResourcePolicies()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, [42]);
            string xml = "<xisf version='1.0'><Image geometry='1:1' sampleFormat='UInt8' location='url(" + new Uri(path).AbsoluteUri + ")'/></xisf>";
            var document = XisfReader.Load(new MemoryStream(Encoding.UTF8.GetBytes(xml)), options: new() { AllowExternalFilePaths = true });
            Assert.Equal(new byte[] { 42 }, document.Images[0].ReadRawData());
            xml = "<xisf version='1.0'><Image geometry='1:1' sampleFormat='UInt8' location='url(ftp://example.invalid/a(b).dat)'/></xisf>";
            document = XisfReader.Load(new MemoryStream(Encoding.UTF8.GetBytes(xml)), options: new() { AllowNetworkResources = true,
                ExternalResourceResolver = (uri, _) => { Assert.EndsWith("a(b).dat", uri.AbsoluteUri); return new MemoryStream(new byte[] { 7 }); } });
            Assert.Equal(new byte[] { 7 }, document.Images[0].ReadRawData());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Complex32CanBeCopiedFromDecodedValuesAndIntegerBoundsSurviveWriting()
    {
        var image = XisfImageData.FromSamples(new byte[] { 8 }, [1, 1], 1); image.LowerBound = 4; image.UpperBound = 12;
        image.Properties["Test:C32"] = new XisfPropertyData("C32Vector", new Complex[] { new(1.5, -2.5) });
        var first = RoundTrip(image).Images[0];
        Assert.Equal(0.5, first.GetNormalizedSamples()[0]);
        var second = RoundTrip(XisfImageData.FromImage(first)).Images[0];
        Assert.Equal(new Complex[] { new(1.5, -2.5) }, second.Properties["Test:C32"].Value);
        Assert.Equal("C32Vector", second.Properties["Test:C32"].Type);
    }

    private delegate byte[] Sha3Hash(ReadOnlySpan<byte> data, int bits);

    [Fact]
    public void SeparateSplineComponentsUseTheirOwnNormalizationNodesAndShape()
    {
        var image = LinearImage();
        foreach (string direction in new[] { "ImageToProjection", "ProjectionToImage" })
        {
            image.Properties["AstrometricSolution:ProjectiveTransformation:" + direction] = new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
            string prefix = "AstrometricSolution:DistortionModel:" + direction + ":";
            image.Properties[prefix + "BasisFunction"] = "Gaussian";
            image.Properties[prefix + "Order"] = 1;
            image.Properties[prefix + "Polynomial"] = false;
            image.Properties[prefix + "Terms"] = "Global";
            image.Properties[prefix + "Global:X:Normalization"] = new double[] { 1, 1, 2 };
            image.Properties[prefix + "Global:X:Nodes"] = new double[,] { { 2, 4 } };
            image.Properties[prefix + "Global:X:Coefficients"] = new double[] { 3 };
            image.Properties[prefix + "Global:X:ShapeParameter"] = 1.0;
            image.Properties[prefix + "Global:Y:Normalization"] = new double[] { 0, 0, 1 };
            image.Properties[prefix + "Global:Y:Nodes"] = new double[,] { { 2, 3 } };
            image.Properties[prefix + "Global:Y:Coefficients"] = new double[] { 5 };
            image.Properties[prefix + "Global:Y:ShapeParameter"] = 2.0;
        }
        var solution = RoundTrip(image).Images[0].AstrometricSolution!;
        Assert.Equal(3, solution.AvailableLayer);
        Assert.Equal((5.0, 8.0), solution.ImageToProjection(2, 3));
        var displaced = solution.ImageToProjection(2.5, 3);
        Assert.Equal(2.5 + 3 * Math.Exp(-1), displaced.X, 12);
        Assert.Equal(3 + 5 * Math.Exp(-1), displaced.Y, 12);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AllBinary128MatrixTypesReadExactIndependentBytes(bool bigEndian)
    {
        UInt128 bits = ((UInt128)0x3fff << 112) | 1;
        byte[] bytes = new byte[32];
        if (bigEndian)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt128BigEndian(bytes, bits);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt128BigEndian(bytes.AsSpan(16), UInt128.MaxValue);
        }
        else
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt128LittleEndian(bytes, bits);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt128LittleEndian(bytes.AsSpan(16), UInt128.MaxValue);
        }
        string properties = string.Concat(new[] { "I128", "UI128", "F128", "C128" }.Select(type =>
            $"<Property id='Test:{type}' type='{type}Matrix' rows='1' columns='1' byteOrder='{(bigEndian ? "big" : "little")}' location='inline:hex'>{Convert.ToHexString(bytes.AsSpan(0, type == "C128" ? 32 : 16))}</Property>"));
        var document = XisfReader.Load(new MemoryStream(Encoding.UTF8.GetBytes("<xisf version='1.0'>" + properties + "</xisf>")));
        Assert.Equal((Int128)bits, ((Int128[,])document.Properties["Test:I128"].Value!)[0, 0]);
        Assert.Equal(bits, ((UInt128[,])document.Properties["Test:UI128"].Value!)[0, 0]);
        Assert.Equal(bits, ((XisfFloat128[,])document.Properties["Test:F128"].Value!)[0, 0].Bits);
        var complex = ((XisfComplex128[,])document.Properties["Test:C128"].Value!)[0, 0];
        Assert.Equal(bits, complex.Real.Bits);
        Assert.Equal(UInt128.MaxValue, complex.Imaginary.Bits);
    }

    [Fact]
    public void EditableCopyPreservesPropertiesFromHeaderWithoutRecommendedNamespace()
    {
        string xml = "<xisf version='1.0'><Image geometry='1:1' sampleFormat='UInt8' location='embedded'><Data encoding='base64'>Kg==</Data>"
            + "<Property id='Test:Future' type='future' location='embedded'><Data encoding='base64'>Kg==</Data></Property>"
            + "<Property id='AstrometricSolution:Version' type='String'>2.0</Property></Image></xisf>";
        var image = XisfReader.Load(new MemoryStream(Encoding.UTF8.GetBytes(xml))).Images[0];
        var copy = RoundTrip(XisfImageData.FromImage(image)).Images[0];
        Assert.False(copy.Properties["Test:Future"].IsAvailable);
        Assert.Equal("2.0", copy.Properties["AstrometricSolution:Version"].Value);
        Assert.Equal(0, copy.AstrometricSolution!.AvailableLayer);
    }

    [Fact]
    public void XisbSupportsZeroIdentifiersAndFreeEntriesAndRejectsCyclesAndLengthMismatch()
    {
        string path = Path.GetTempFileName();
        try
        {
            byte[] bytes = new byte[113];
            "XISB0100"u8.CopyTo(bytes);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 2);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(40), 112);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(48), 1);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(72), 99);
            bytes[112] = 42;
            File.WriteAllBytes(path, bytes);
            string xml = "<xisf version='1.0'><Image geometry='1:1' sampleFormat='UInt8' location='url(" + new Uri(path).AbsoluteUri + "):0'/></xisf>";
            var image = XisfReader.Load(new MemoryStream(Encoding.UTF8.GetBytes(xml)), options: new() { AllowExternalFilePaths = true }).Images[0];
            Assert.Equal(new byte[] { 42 }, image.ReadRawData());
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24), 16);
            File.WriteAllBytes(path, bytes);
            Assert.Throws<XisfFormatException>(() => image.ReadRawData());
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24), 0);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(56), 1);
            File.WriteAllBytes(path, bytes);
            Assert.Throws<XisfFormatException>(() => image.ReadRawData());
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(256, "a7ffc6f8bf1ed76651c14756a061d662f580ff4de43b49fa82d80a4b80f8434a")]
    [InlineData(512, "a69f73cca23a9ac5c8b567dc185a756e97c982164fe25859e0d1dcc1475c80a615b2123af1f5f94c11e3e9402c3ac558f500199d95b6d3e301758586281dcd26")]
    public void PortableSha3MatchesKnownAnswersAndNativeProvider(int bits, string emptyDigest)
    {
        var method = typeof(XisfReader).Assembly.GetType("XISFLib.XisfSha3")!.GetMethod("HashData")!;
        var hash = method.CreateDelegate<Sha3Hash>();
        Assert.Equal(emptyDigest, Convert.ToHexString(hash([], bits)).ToLowerInvariant());
        foreach (int length in new[] { 1, 71, 72, 135, 136, 137, 4096 })
        {
            byte[] bytes = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();
            if (bits == 256 && SHA3_256.IsSupported) Assert.Equal(SHA3_256.HashData(bytes), hash(bytes, bits));
            if (bits == 512 && SHA3_512.IsSupported) Assert.Equal(SHA3_512.HashData(bytes), hash(bytes, bits));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypedComplex32ImagesPreserveComponentsAndByteOrder(bool bigEndian)
    {
        var values = new XisfComplex32[] { new(1.5f, -2.25f), new(float.PositiveInfinity, float.NaN) };
        var image = XisfImageData.FromSamples(values, [2, 1], 1, isBigEndian: bigEndian);
        var decoded = RoundTrip(image).Images[0].GetComplexSamples();
        Assert.Equal(new Complex(1.5, -2.25), decoded[0]);
        Assert.True(double.IsPositiveInfinity(decoded[1].Real));
        Assert.True(double.IsNaN(decoded[1].Imaginary));
    }

    [Fact]
    public void UnsupportedOptionalObjectsDoNotHideImageAndPropertyIdsAreCaseSensitive()
    {
        string xml = "<xisf version='1.0'><Image geometry='1:1' sampleFormat='UInt8' location='embedded'><Data encoding='base64'>Kg==</Data>"
            + "<ICCProfile location='inline:base64' compression='future:1'>Kg==</ICCProfile>"
            + "<Thumbnail geometry='1:1' sampleFormat='future' location='embedded'><Data encoding='base64'>Kg==</Data></Thumbnail>"
            + "<Property id='Test:Value' type='Int32' value='1'/><Property id='Test:value' type='Int32' value='2'/></Image></xisf>";
        var image = XisfReader.Load(new MemoryStream(Encoding.UTF8.GetBytes(xml))).Images.Single();
        Assert.Equal(new byte[] { 42 }, image.ReadRawData());
        Assert.Equal(2, image.UnavailableObjects.Count);
        Assert.Equal("1", AstronomyMetadata.From(image).Values["Test:Value"]);
        Assert.Equal("2", AstronomyMetadata.From(image).Values["Test:value"]);
    }
}
