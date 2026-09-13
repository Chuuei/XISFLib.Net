using System.Buffers.Binary;

namespace XISFLib.Tests;

public class FitsTests
{
    [Fact]
    public void WritesAndReadsUnsignedImageWithLongMetadataAndChecksums()
    {
        XisfImageData source = XisfImageData.FromSamples(new ushort[] { 0, 1, 32768, 65535 }, [2, 2], 1);
        source.FitsKeywords.Add(new XisfFitsKeyword("OBJECT", "NGC 6992", "Target"));
        source.FitsKeywords.Add(new XisfFitsKeyword("HIERARCH ESO DET CHIP NAME", "A very long detector description that exceeds one standard FITS value card and must continue without losing text", null));

        FitsDocument created = FitsConverter.FromXisf([source]);
        using MemoryStream stream = new();
        FitsWriter.Write(stream, created, new FitsWriterOptions { WriteChecksums = true });

        Assert.Equal(0, stream.Length % 2880);
        stream.Position = 0;
        FitsDocument actual = FitsReader.Load(stream);
        FitsImage image = Assert.IsType<FitsImage>(Assert.Single(actual.Hdus).Content);

        Assert.Equal(16, image.BitPix);
        Assert.Equal(32768d, image.BZero);
        Assert.Equal([2, 2], image.Dimensions);
        Assert.Equal([0d, 1d, 32768d, 65535d], image.GetPhysicalSamples());
        Assert.Equal("NGC 6992", actual.Hdus[0].Header.GetValue<string>("OBJECT"));
        Assert.StartsWith("A very long detector description", actual.Hdus[0].Header.GetValue<string>("HIERARCH ESO DET CHIP NAME"));
        Assert.True(actual.Hdus[0].ChecksumValid);
        Assert.True(actual.Hdus[0].DataChecksumValid);
    }

    [Fact]
    public void ReadsImageExtensionAndPreservesUnknownHduData()
    {
        FitsHdu primary = FitsHdu.CreatePrimary();
        FitsHdu image = FitsHdu.CreateImage(FitsImage.FromPhysicalSamples(new float[] { 0.25f, 0.75f }, [2, 1]), false);
        FitsHdu unknown = new(FitsHduType.Unknown,
            [new FitsHeaderCard("XTENSION", "CUSTOM"), new FitsHeaderCard("BITPIX", 8L), new FitsHeaderCard("NAXIS", 1L),
             new FitsHeaderCard("NAXIS1", 4L), new FitsHeaderCard("PCOUNT", 0L), new FitsHeaderCard("GCOUNT", 1L)], [1, 2, 3, 4]);
        FitsDocument source = new([primary, image, unknown]);

        using MemoryStream first = new();
        FitsWriter.Write(first, source);
        first.Position = 0;
        FitsDocument loaded = FitsReader.Load(first);

        Assert.Equal(3, loaded.Hdus.Count);
        Assert.Equal([0.25d, 0.75d], Assert.IsType<FitsImage>(loaded.Hdus[1].Content).GetPhysicalSamples());
        Assert.Equal([1, 2, 3, 4], loaded.Hdus[2].Data);

        using MemoryStream second = new();
        FitsWriter.Write(second, loaded);
        second.Position = 0;
        Assert.Equal([1, 2, 3, 4], FitsReader.Load(second).Hdus[2].Data);
    }

    [Fact]
    public void ReadsBinaryTableIncludingLogicalComplexBitsAndVariableArray()
    {
        byte[] row = new byte[22];
        row[0] = (byte)'T';
        BinaryPrimitives.WriteInt32BigEndian(row.AsSpan(1), BitConverter.SingleToInt32Bits(1.5f));
        BinaryPrimitives.WriteInt32BigEndian(row.AsSpan(5), BitConverter.SingleToInt32Bits(-2.5f));
        row[9] = 0b1010_0000;
        BinaryPrimitives.WriteInt32BigEndian(row.AsSpan(10), 2);
        BinaryPrimitives.WriteInt32BigEndian(row.AsSpan(14), 0);
        byte[] heap = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(heap, 10);
        BinaryPrimitives.WriteInt32BigEndian(heap.AsSpan(4), 20);
        FitsHeader header = new([
            new FitsHeaderCard("XTENSION", "BINTABLE"), new FitsHeaderCard("NAXIS1", 18L),
            new FitsHeaderCard("NAXIS2", 1L), new FitsHeaderCard("PCOUNT", 8L), new FitsHeaderCard("TFIELDS", 4L),
            new FitsHeaderCard("TTYPE1", "VALID"), new FitsHeaderCard("TFORM1", "1L"),
            new FitsHeaderCard("TTYPE2", "VALUE"), new FitsHeaderCard("TFORM2", "1C"),
            new FitsHeaderCard("TTYPE3", "MASK"), new FitsHeaderCard("TFORM3", "8X"),
            new FitsHeaderCard("TTYPE4", "VALUES"), new FitsHeaderCard("TFORM4", "1PJ")]);
        FitsHdu hdu = new(FitsHduType.BinaryTable, header, row[..18].Concat(heap).ToArray());

        using MemoryStream stream = new();
        FitsWriter.Write(stream, new FitsDocument([FitsHdu.CreatePrimary(), hdu]));
        stream.Position = 0;
        FitsTable table = Assert.IsType<FitsTable>(FitsReader.Load(stream).Hdus[1].Content);

        Assert.Equal(true, table.Rows[0][0]);
        Assert.Equal(new System.Numerics.Complex(1.5, -2.5), table.Rows[0][1]);
        Assert.Equal(new[] { true, false, true, false, false, false, false, false }, table.Rows[0][2]);
        Assert.Equal(new[] { 10, 20 }, table.Rows[0][3]);
    }

    [Fact]
    public void ConvertsFitsToXisfAndBackWithMetadataAndPixels()
    {
        FitsImage fitsImage = FitsImage.FromPhysicalSamples(new short[] { -10, 20 }, [2, 1]);
        FitsHdu fitsHdu = FitsHdu.CreateImage(fitsImage, true);
        fitsHdu.Header.Add(new FitsHeaderCard("CRVAL1", 123.5, "WCS reference"));
        fitsHdu.Header.Add(new FitsHeaderCard("HISTORY", "Calibrated once"));

        XisfImageData xisf = Assert.Single(FitsConverter.ToXisf(new FitsDocument([fitsHdu])));
        Assert.Equal([-10d, 20d], ReadXisfValues(xisf));
        Assert.Contains(xisf.FitsKeywords, keyword => keyword.Name == "CRVAL1" && keyword.Value == "123.5");

        FitsDocument roundTrip = FitsConverter.FromXisf([xisf]);
        FitsImage actual = Assert.IsType<FitsImage>(Assert.Single(roundTrip.Hdus).Content);
        Assert.Equal([-10d, 20d], actual.GetPhysicalSamples());
        Assert.Equal(123.5, roundTrip.Hdus[0].Header.GetValue<double>("CRVAL1"));
    }

    [Fact]
    public void PreservesSignedScaledStorageThroughSerializedXisf()
    {
        byte[] raw = new byte[4];
        BinaryPrimitives.WriteInt16BigEndian(raw, -3);
        BinaryPrimitives.WriteInt16BigEndian(raw.AsSpan(2), 7);
        FitsImage source = FitsImage.FromRaw(raw, 16, [2, 1], 2, 10);
        XisfImageData converted = FitsConverter.ToXisf(source);
        using MemoryStream xisf = new();
        XisfWriter.Write(xisf, [converted]);
        xisf.Position = 0;

        FitsImage actual = Assert.IsType<FitsImage>(Assert.Single(FitsConverter.FromXisf(XisfReader.Load(xisf).Images).Hdus).Content);

        Assert.Equal(16, actual.BitPix);
        Assert.Equal(2, actual.BScale);
        Assert.Equal(10, actual.BZero);
        Assert.Equal([4d, 24d], actual.GetPhysicalSamples());
        Assert.Equal(raw, actual.ReadRawData());
    }

    [Fact]
    public void ConvertsInterleavedRgbToFitsPlanarCube()
    {
        XisfImageData source = XisfImageData.FromSamples(new byte[] { 1, 2, 3, 4, 5, 6 }, [2, 1], 3, XisfPixelLayout.Interleaved);

        FitsImage fits = Assert.IsType<FitsImage>(Assert.Single(FitsConverter.FromXisf([source]).Hdus).Content);
        XisfImageData roundTrip = FitsConverter.ToXisf(fits);

        Assert.Equal([2, 1, 3], fits.Dimensions);
        Assert.Equal([1d, 4d, 2d, 5d, 3d, 6d], fits.GetPhysicalSamples());
        Assert.Equal([1d, 4d, 2d, 5d, 3d, 6d], ReadXisfValues(roundTrip));
        Assert.Equal(3, roundTrip.ChannelCount);
    }

    [Theory]
    [InlineData("slow.fits")]
    public void LoadsAndConvertsExampleFitsImages(string fileName)
    {
        string examplesDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Examples"));

        FitsDocument document = FitsReader.Load(Path.Combine(examplesDirectory, fileName));
        FitsImage[] images = document.Hdus.Select(hdu => hdu.Content).OfType<FitsImage>().ToArray();

        Assert.NotEmpty(document.Hdus);
        Assert.NotEmpty(images);
        foreach (FitsImage image in images)
        {
            Assert.NotEmpty(image.Dimensions);
            Assert.All(image.Dimensions, dimension => Assert.True(dimension > 0));
            Assert.Equal(image.Dimensions.Aggregate(1L, (count, dimension) => checked(count * dimension)), image.GetPhysicalSamples().LongLength);
        }

        IReadOnlyList<XisfImageData> converted = FitsConverter.ToXisf(document);
        Assert.Equal(images.Length, converted.Count);
        Assert.All(converted, image =>
        {
            Assert.NotEmpty(image.Dimensions);
            Assert.True(image.ChannelCount > 0);
        });
    }

    private static double[] ReadXisfValues(XisfImageData image)
    {
        using MemoryStream stream = new();
        XisfWriter.Write(stream, [image]);
        stream.Position = 0;
        return Assert.Single(XisfReader.Load(stream).Images).GetSamples<double>();
    }
}