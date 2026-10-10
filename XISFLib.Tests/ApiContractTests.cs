using System.Numerics;
using System.Xml.Linq;
using Xunit;

namespace XISFLib.Tests;

public class ApiContractTests
{
    [Fact]
    public void MissingAndIncompatibleMetadataCannotMasqueradeAsZero()
    {
        var header = new FitsHeader();
        Assert.False(header.TryGetValue<int>("GAIN", out _));
        Assert.Throws<KeyNotFoundException>(() => header.GetRequiredValue<int>("GAIN"));
        header.Set("GAIN", 0);
        Assert.True(header.TryGetValue<int>("GAIN", out var gain));
        Assert.Equal(0, gain);
        header.Set("GAIN", "invalid");
        Assert.False(header.TryGetValue<int>("GAIN", out _));
        Assert.Contains("GAIN", Assert.Throws<FormatException>(() => header.GetRequiredValue<int>("GAIN")).Message);
        header.Set(" GAIN ", 2);
        Assert.Single(header.Find("GAIN"));
        Assert.Equal(2, header.GetRequiredValue<int>("GAIN"));
        header.Add(new("HISTORY", "first")); header.Add(new("HISTORY", "second"));
        Assert.Equal(2, header.Find("HISTORY").Count());
    }

    [Fact]
    public void FitsImageReplacementUsesOnePayloadAndRejectsStructuralHeaderEdits()
    {
        var hdu = FitsHdu.CreateImage(FitsImage.FromPhysicalSamples(new byte[] { 42 }, [1]), true);
        Assert.Throws<InvalidOperationException>(() => hdu.Header.Set("BZERO", 100));
        Assert.Throws<InvalidOperationException>(() => hdu.Header.Add(new("NAXIS1", 2)));
        hdu.Header.Set("OBJECT", "M31");
        hdu.ReplaceImage(FitsImage.FromPhysicalSamples(new ushort[] { 500, 600 }, [2]));
        using var stream = new MemoryStream();
        FitsWriter.Write(stream, new FitsDocument([hdu]), new() { WriteChecksums = true });
        stream.Position = 0;
        var loaded = FitsReader.Load(stream);
        Assert.Equal(new double[] { 500, 600 }, loaded.Images.Single().GetPhysicalSamples());
        Assert.Equal(loaded.Images.Single().ReadRawData(), loaded.Hdus[0].Data.ToArray());
        Assert.Equal("M31", loaded.Hdus[0].Header.GetRequiredValue<string>("OBJECT"));
        Assert.True(loaded.Hdus[0].ChecksumValid);
    }

    [Fact]
    public void ReplacingOpaquePayloadInvalidatesDecodedContentAndChecksums()
    {
        var hdu = new FitsHdu(FitsHduType.Unknown, new FitsHeader(), new byte[] { 1 });
        byte[] replacement = [2, 3];
        hdu.ReplaceRawData(replacement); replacement[0] = 9;
        Assert.Equal(new byte[] { 2, 3 }, hdu.Data.ToArray());
        Assert.Null(hdu.Content); Assert.Null(hdu.ChecksumValid);
    }

    [Fact]
    public void ParsedXmlAndPropertyArraysAreDetachedAndAdvancedEditsReparseViews()
    {
        var image = XisfImageData.FromSamples(new byte[] { 1, 2 }, [2, 1], 1);
        image.Properties.Set("Test:Numbers", new double[] { 1, 2 });
        var document = Load(image);
        document.Xml.Root!.Elements().First(e => e.Name.LocalName == "Image").SetAttributeValue("id", "ignored");
        var values = (double[])document.Images[0].Properties["Test:Numbers"].Value!; values[0] = 99;
        Assert.Null(document.Images[0].Id);
        Assert.Equal(1, document.Images[0].Properties.GetRequiredValue<double[]>("Test:Numbers")[0]);
        var edited = document.EditXml(xml => xml.Root!.Elements().First(e => e.Name.LocalName == "Image").SetAttributeValue("id", "new_id"));
        Assert.Equal("new_id", edited.Images[0].Id);
        Assert.Null(document.Images[0].Id);
        Assert.Equal(new byte[] { 1, 2 }, edited.Images[0].GetSamples<byte>());
        Assert.Throws<XisfFormatException>(() => document.EditXml(xml => xml.Descendants().First(e => e.Name.LocalName == "Image").SetAttributeValue("geometry", "bad")));
    }

    [Fact]
    public void ReaderPolicyIsCapturedAndStreamRemainsCallerOwned()
    {
        var image = XisfImageData.FromSamples(new byte[] { 1, 2 }, [2], 1);
        using var source = new MemoryStream(); XisfWriter.Write(source, [image]); source.Position = 0;
        var options = new XisfReaderOptions(); var doc = XisfReader.Load(source, options: options);
        options.MaximumDataBlockSize = 1; options.AllowHeaderRelativeDataBlocks = false;
        Assert.Equal(new byte[] { 1, 2 }, doc.Images[0].ReadRawData());
        Assert.True(source.CanRead);
    }

    [Fact]
    public void LayoutDefaultsAgreeAcrossParsedEditableAndRegionReads()
    {
        var editable = XisfImageData.FromSamples(new ushort[] { 1, 2, 3, 4, 5, 6 }, [2, 1], 3, XisfPixelLayout.Interleaved, true);
        var parsed = Load(editable).Images[0];
        ushort[] planar = [1, 4, 2, 5, 3, 6];
        Assert.Equal(planar, parsed.GetSamples<ushort>());
        Assert.Equal(planar, editable.GetSamples<ushort>());
        Assert.Equal(planar, parsed.ReadRegion<ushort>(0, 0, 2, 1));
        Assert.Equal(planar, editable.ReadRegion<ushort>(0, 0, 2, 1));
        Assert.Equal(new ushort[] { 1, 2, 3, 4, 5, 6 }, editable.GetInterleavedSamples<ushort>());
        Assert.Throws<ArgumentOutOfRangeException>(() => parsed.GetSamples<ushort>((XisfPixelLayout)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => editable.GetSamples<ushort>((XisfPixelLayout)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => editable.ReadRegion<ushort>(0, 0, 2, 1, (XisfPixelLayout)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => XisfImageData.FromSamples(new byte[1], [1], 1, (XisfPixelLayout)999));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EditableComplexAndNormalizedSamplesMatchParsedReads(bool bigEndian)
    {
        var image = XisfImageData.FromSamples(new XisfComplex32[] { new(1, -2), new(3, -4) }, [1, 1], 2, XisfPixelLayout.Interleaved, bigEndian);
        Assert.Equal(Load(image).Images[0].GetComplexSamples(), image.GetComplexSamples());
        var real = XisfImageData.FromSamples(new float[] { 10, 20 }, [2, 1], 1, isBigEndian: bigEndian);
        real.LowerBound = 10; real.UpperBound = 20;
        Assert.Equal(Load(real).Images[0].GetNormalizedSamples(), real.GetNormalizedSamples());
    }

    [Fact]
    public void ScientificCubeIsPreservedUnlessColorInterpretationIsExplicit()
    {
        var image = FitsImage.FromPhysicalSamples(new byte[12], [2, 2, 3]);
        var doc = new FitsDocument([FitsHdu.CreateImage(image, true)]);
        var scientific = FitsConverter.ConvertToXisf(doc);
        Assert.Equal(new[] { 2, 2, 3 }, scientific.Value[0].Dimensions); Assert.Equal(1, scientific.Value[0].ChannelCount);
        var color = FitsConverter.ConvertToXisf(doc, new() { AxisInterpretation = FitsAxisInterpretation.RgbOrRgba });
        Assert.Equal(new[] { 2, 2 }, color.Value[0].Dimensions); Assert.Equal(3, color.Value[0].ChannelCount);
        Assert.Contains(color.Findings, f => f.Code == "FITS_COLOR_AXIS");
    }

    [Fact]
    public void ConversionRejectsUnapprovedLossAndReportsAllowedLoss()
    {
        var image = XisfImageData.FromSamples(new byte[] { 1 }, [1, 1], 1);
        image.Properties.Set("Observation:Object:Name", "M31");
        var rejection = Assert.Throws<ConversionLossException>(() => FitsConverter.FromXisf([image]));
        Assert.Contains(rejection.Findings, f => f.Code == "XISF_PROPERTIES");
        var allowed = FitsConverter.ConvertToFits([image], ConversionLossPolicy.Allow);
        Assert.True(allowed.HasLosses); Assert.Single(allowed.Value.Images);
        var opaque = new FitsHdu(FitsHduType.Unknown, new FitsHeader(), new byte[] { 9 });
        var fits = new FitsDocument([FitsHdu.CreateImage(FitsImage.FromPhysicalSamples(new byte[] { 1 }, [1]), true), opaque]);
        Assert.Throws<ConversionLossException>(() => FitsConverter.ToXisf(fits));
        Assert.True(FitsConverter.ConvertToXisf(fits, new() { LossPolicy = ConversionLossPolicy.Allow }).HasLosses);
    }

    [Fact]
    public void EditableDocumentRetainsGlobalContentMultipleImagesTablesAndExtensions()
    {
        var original = Load(XisfImageData.FromSamples(new byte[] { 7 }, [1], 1));
        original = original.EditXml(xml => xml.Root!.Add(new XElement(XName.Get("Custom", "urn:test"), new XAttribute("answer", 42))));
        var content = original.ToEditable();
        content.Properties.Set("Test:Global", 42);
        content.Images.Add(XisfImageData.FromSamples(new byte[] { 8 }, [1], 1));
        content.Images[0].Id = "edited";
        var table = new XisfTableData { Id = "table" };
        table.Fields.Add(new("n", "Int32")); table.Rows.Add(new object[] { 3 }); content.Tables.Add(table);
        using var stream = new MemoryStream(); XisfWriter.Write(stream, content); stream.Position = 0;
        var saved = XisfReader.Load(stream);
        Assert.Equal(2, saved.Images.Count); Assert.Single(saved.Tables);
        Assert.Equal(42, saved.Properties.GetRequiredValue<int>("Test:Global"));
        Assert.Equal("42", saved.Xml.Root!.Element(XName.Get("Custom", "urn:test"))!.Attribute("answer")!.Value);
        Assert.Equal("edited", saved.Images[0].Id);
    }

    [Fact]
    public void TypedFactoriesInferCollectionsAndQuoteFitsStrings()
    {
        var properties = new XisfPropertyCollection();
        properties.Set("Test:Matrix", new double[,] { { 1, 2 } });
        Assert.Equal("F64Matrix", Assert.IsType<XisfPropertyData>(properties["Test:Matrix"]).Type);
        Assert.Throws<NotSupportedException>(() => properties.Set("Test:Bad", new object()));
        Assert.Throws<ArgumentException>(() => properties.Set("bad id", 1));
        Assert.Equal("'O''Brien'", XisfFitsKeyword.FromValue("OBJECT", "O'Brien").Value);
        var history = XisfFitsKeyword.FromValue("HISTORY", "Processed");
        Assert.Equal("", history.Value); Assert.Equal("Processed", history.Comment);
    }

    [Fact]
    public async Task AsyncWritesRespectCancellationAndDoNotCloseStreams()
    {
        var content = new XisfDocumentData(); content.Images.Add(XisfImageData.FromSamples(new byte[] { 1 }, [1], 1));
        using var stream = new MemoryStream();
        await XisfWriter.WriteAsync(stream, content); Assert.True(stream.CanWrite);
        using var cancelled = new MemoryStream();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => XisfWriter.WriteAsync(cancelled, content, cancellationToken: new CancellationToken(true)));
        Assert.Equal(0, cancelled.Length); Assert.True(cancelled.CanWrite);
    }

    private static XisfDocument Load(XisfImageData image)
    {
        using var stream = new MemoryStream(); XisfWriter.Write(stream, [image]); stream.Position = 0;
        return XisfReader.Load(stream);
    }

    [Fact]
    public void EditableDocumentPreservesUnavailableAncillaryAndUnsupportedImages()
    {
        var image = XisfImageData.FromSamples(new byte[] { 7 }, [1], 1);
        image.IccProfile = new byte[128];
        "acsp"u8.CopyTo(image.IccProfile.AsSpan(36));
        var source = Load(image).EditXml(xml =>
        {
            xml.Descendants().Single(e => e.Name.LocalName == "ICCProfile").SetAttributeValue("compression", "future:128");
            XNamespace ns = xml.Root!.Name.Namespace;
            xml.Root.Add(new XElement(ns + "Image", new XAttribute("geometry", "1:1"), new XAttribute("sampleFormat", "Future"),
                new XAttribute("location", "embedded"), new XElement(ns + "Data", new XAttribute("encoding", "hex"), "AA")));
        });
        Assert.Single(source.UnavailableObjects); Assert.Single(source.Images[0].UnavailableObjects);
        using var stream = new MemoryStream(); XisfWriter.Write(stream, source.ToEditable()); stream.Position = 0;
        var saved = XisfReader.Load(stream);
        Assert.Single(saved.UnavailableObjects); Assert.Single(saved.Images[0].UnavailableObjects);
        Assert.Equal(new byte[] { 7 }, saved.Images[0].ReadRawData());
        Assert.Equal("future:128", saved.Images[0].Xml.Elements().Single(e => e.Name.LocalName == "ICCProfile").Attribute("compression")!.Value);
    }

    [Fact]
    public void SigningReturnsAnIndependentDocumentAndSignedEditableOutputVerifies()
    {
        var source = Load(XisfImageData.FromSamples(new byte[] { 7 }, [1], 1));
        using var key = System.Security.Cryptography.RSA.Create(2048);
        var signed = XisfWriter.Sign(source, key);
        Assert.False(source.HasXmlSignature); Assert.True(XisfReader.VerifyXmlSignature(signed));
        using var stream = new MemoryStream(); XisfWriter.WriteSigned(stream, source.ToEditable(), key, new() { Compression = XisfCompression.Zstd, BlockAlignmentSize = 1024 });
        stream.Position = 0; var saved = XisfReader.Load(stream);
        Assert.True(XisfReader.VerifyXmlSignature(saved)); Assert.Equal(new byte[] { 7 }, saved.Images[0].ReadRawData());
    }

    [Fact]
    public async Task ReaderSnapshotIsTakenBeforeAsyncWorkStarts()
    {
        using var stream = new MemoryStream(); XisfWriter.Write(stream, [XisfImageData.FromSamples(new byte[] { 1, 2 }, [2], 1)]); stream.Position = 0;
        var options = new XisfReaderOptions();
        var operation = XisfReader.LoadAsync(stream, options: options);
        options.MaximumDataBlockSize = 1;
        var source = await operation;
        Assert.Equal(new byte[] { 1, 2 }, source.Images[0].ReadRawData());
    }

    [Fact]
    public void UndefinedSettingsAndStructuralRemovalAreRejected()
    {
        using var stream = new MemoryStream();
        var image = XisfImageData.FromSamples(new byte[] { 1 }, [1], 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => XisfWriter.Write(stream, [image], new() { Compression = (XisfCompression)999 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => XisfWriter.Write(stream, [image], new() { Checksum = (XisfChecksum)999 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => FitsConverter.ToXisf(FitsImage.FromPhysicalSamples(new byte[] { 1 }, [1]), new() { AxisInterpretation = (FitsAxisInterpretation)999 }));
        var hdu = FitsHdu.CreateImage(FitsImage.FromPhysicalSamples(new byte[] { 1 }, [1]), true);
        hdu.Header.Add(new("BITPIX", 8));
        Assert.Throws<InvalidOperationException>(() => hdu.Header.RemoveAt(0));
        Assert.Throws<InvalidOperationException>(() => hdu.Header.Clear());
        Assert.Throws<InvalidOperationException>(() => hdu.Header[0] = new("OBJECT", "M31"));
        Assert.Equal("BITPIX", hdu.Header[0].Keyword);
    }

    [Fact]
    public void RawImageHduConstructionRetainsGeometryAndPixels()
    {
        var header = new FitsHeader([new("BITPIX", 8), new("NAXIS", 1), new("NAXIS1", 2)]);
        byte[] raw = [42, 43];
        var hdu = new FitsHdu(FitsHduType.Primary, header, raw);
        raw[0] = 99;
        Assert.NotNull(hdu.Image);
        Assert.Throws<InvalidOperationException>(() => hdu.ReplaceRawData(new byte[] { 1 }));
        using var stream = new MemoryStream();
        FitsWriter.Write(stream, new FitsDocument([hdu]));
        stream.Position = 0;
        Assert.Equal(new double[] { 42, 43 }, FitsReader.Load(stream).Images.Single().GetPhysicalSamples());
        Assert.Throws<KeyNotFoundException>(() => new FitsHdu(FitsHduType.Image, new FitsHeader(), new byte[] { 1 }));
    }

    [Fact]
    public void EditableDocumentConversionReportsGlobalContentLoss()
    {
        var document = new XisfDocumentData();
        document.Images.Add(XisfImageData.FromSamples(new byte[] { 1 }, [1], 1));
        document.Properties.Set("Observation:Object", "M31");
        Assert.Throws<ConversionLossException>(() => FitsConverter.ConvertToFits(document));
        var converted = FitsConverter.ConvertToFits(document, ConversionLossPolicy.Allow);
        Assert.Contains(converted.Findings, f => f.Code == "XISF_DOCUMENT" && f.Effect == ConversionEffect.Omitted);
        Assert.Equal(new double[] { 1 }, converted.Value.Images.Single().GetPhysicalSamples());
    }

    [Fact]
    public void AdvancedXmlEditsRespectCapturedHeaderSizeLimits()
    {
        using var stream = new MemoryStream();
        XisfWriter.Write(stream, [XisfImageData.FromSamples(new byte[] { 1 }, [1], 1)]);
        stream.Position = 0;
        var source = XisfReader.Load(stream, options: new() { MaximumXmlCharacters = 4096, MaximumXmlHeaderSize = 4096 });
        Assert.Throws<XisfFormatException>(() => source.EditXml(xml => xml.Root!.Add(new XComment(new string('x', 4096)))));
        Assert.Equal(new byte[] { 1 }, source.Images[0].ReadRawData());
    }

    [Fact]
    public void ParsedConversionRejectsMetadataLossBeforeReadingPixels()
    {
        var image = XisfImageData.FromSamples(new byte[] { 1 }, [1], 1);
        image.Properties.Set("FITS:OBJECT", "M31");
        var source = Load(image).EditXml(xml => xml.Root!.Element(XName.Get("Image", "http://www.pixinsight.com/xisf"))!.SetAttributeValue("location", "attachment:999999999:1"));
        var error = Assert.Throws<ConversionLossException>(() => FitsConverter.FromXisf(source.Images));
        Assert.Contains(error.Findings, f => f.Code == "XISF_PROPERTIES");
        Assert.Throws<ConversionLossException>(() => FitsConverter.ConvertToFits(source));
    }
}
