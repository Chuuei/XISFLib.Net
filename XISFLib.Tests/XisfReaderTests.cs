using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using K4os.Compression.LZ4;

namespace XISFLib.Tests;

public class XisfReaderTests
{
    [Fact]
    public void LoadsAttachedPlanarImageAndMetadata()
    {
        byte[] pixels = [1, 0, 2, 0, 10, 0, 20, 0];
        string checksum = Convert.ToHexString(SHA256.HashData(pixels)).ToLowerInvariant();
        string body = $$"""
            <Metadata>
              <Property id="XISF:CreationTime" type="TimePoint" value="2026-09-13T10:00:00Z" />
              <Property id="XISF:CreatorApplication" type="String">Unit tests</Property>
            </Metadata>
            <Image id="light" geometry="2:1:2" sampleFormat="UInt16" colorSpace="Gray" pixelStorage="Planar"
                   location="attachment:{POSITION}:8" checksum="sha-256:{{checksum}}">
              <Property id="Observation:ExposureTime" type="Float64" value="30.5" />
              <FITSKeyword name="OBJECT" value="M31" comment="Target" />
              <ColorFilterArray pattern="RGGB" width="2" height="2" name="Bayer" />
            </Image>
            """;

        using MemoryStream stream = new(TestFile(body, pixels));
        XisfDocument document = XisfReader.Load(stream);
        XisfImage image = Assert.Single(document.Images);

        Assert.Equal("1.0", document.Version);
        Assert.Equal("Unit tests", document.Metadata["XISF:CreatorApplication"].Value);
        Assert.Equal([2, 1], image.Dimensions);
        Assert.Equal(2, image.ChannelCount);
        Assert.Equal(XisfSampleFormat.UInt16, image.SampleFormat);
        Assert.Equal([1d, 2d, 10d, 20d], image.GetSamples<double>(XisfPixelLayout.Planar));
        Assert.Equal([1d, 10d, 2d, 20d], image.GetSamples<double>(XisfPixelLayout.Interleaved));
        Assert.Equal("M31", Assert.Single(image.FitsKeywords).Value);
        Assert.Equal(30.5, image.Properties["Observation:ExposureTime"].Value);
        Assert.Equal("RGGB", image.ColorFilterArray?.Pattern);
        Assert.NotNull(document.Xml.Root);
    }

    [Fact]
    public void ReadsEmbeddedBigEndianZlibShuffledFloatImage()
    {
        byte[] raw = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(raw, BitConverter.SingleToInt32Bits(0.25f));
        BinaryPrimitives.WriteInt32BigEndian(raw.AsSpan(4), BitConverter.SingleToInt32Bits(0.75f));
        byte[] shuffled = Shuffle(raw, 4);
        byte[] compressed;
        using (MemoryStream target = new())
        {
            using (ZLibStream zlib = new(target, CompressionLevel.SmallestSize, true))
                zlib.Write(shuffled);
            compressed = target.ToArray();
        }

        string body = $$"""
            <Metadata><Property id="XISF:CreationTime" type="TimePoint" value="2026-09-13T10:00:00Z" /><Property id="XISF:CreatorApplication" type="String">Tests</Property></Metadata>
            <Image geometry="2:1:1" sampleFormat="Float32" bounds="0:1" byteOrder="big" location="embedded">
              <Data encoding="base64" compression="zlib+sh:8:4">{{Convert.ToBase64String(compressed)}}</Data>
            </Image>
            """;

        using MemoryStream stream = new(TestFile(body));
        XisfImage image = Assert.Single(XisfReader.Load(stream).Images);

        Assert.Equal([0.25, 0.75], image.GetSamples<double>());
        Assert.Equal([0.25, 0.75], image.GetNormalizedSamples());
    }

    [Fact]
    public void ParsesPropertyCollectionsAndTables()
    {
        string body = """
            <Metadata><Property id="XISF:CreationTime" type="TimePoint" value="2026-09-13T10:00:00Z" /><Property id="XISF:CreatorApplication" type="String">Tests</Property></Metadata>
            <Property id="Enabled" type="Boolean" value="true" />
            <Property id="Offsets" type="I32Vector" length="3">1 2 3</Property>
            <Property id="Payload" type="ByteArray" length="3" location="inline:hex">0102ff</Property>
            <Table id="stars" caption="Stars"><Structure><Field id="name" type="String" /><Field id="magnitude" type="Float64" /></Structure><Row><Cell value="Vega" /><Cell value="0.03" /></Row></Table>
            """;

        using MemoryStream stream = new(TestFile(body));
        XisfDocument document = XisfReader.Load(stream);

        Assert.Equal(true, document.Properties["Enabled"].Value);
        Assert.Equal([1, 2, 3], Assert.IsType<int[]>(document.Properties["Offsets"].Value));
        Assert.Equal([1, 2, 255], Assert.IsType<byte[]>(document.Properties["Payload"].Value));
        XisfTable table = Assert.Single(document.Tables);
        Assert.Equal("Vega", table.Rows[0].Cells[0].Value);
        Assert.Equal(0.03, table.Rows[0].Cells[1].Value);
    }

    [Fact]
    public void ResolvesSharedCoreElementsAndStandaloneStructures()
    {
        string body = """
            <Metadata><Property id="XISF:CreationTime" type="TimePoint" value="2026-09-13T10:00:00Z" /><Property id="XISF:CreatorApplication" type="String">Tests</Property></Metadata>
            <Property uid="SharedExposure" id="Observation:ExposureTime" type="Float64" value="30" />
            <RGBWorkingSpace uid="SharedRgb" gamma="2.2" sRGB="false" x="0.64:0.30:0.15" Y="0.21:0.72:0.07" />
            <Structure uid="StarFields"><Field id="name" type="String" /><Field id="magnitude" type="Float64" /></Structure>
            <Table id="stars"><Reference ref="StarFields" /><Row><Cell value="Vega" /><Cell value="0.03" /></Row></Table>
            <Image geometry="1:1" sampleFormat="UInt8" location="inline:hex">2a<Reference ref="SharedExposure" /><Reference ref="SharedRgb" /></Image>
            """;

        using MemoryStream stream = new(TestFile(body));
        XisfDocument document = XisfReader.Load(stream);
        XisfImage image = Assert.Single(document.Images);

        Assert.Equal(30d, image.Properties["Observation:ExposureTime"].Value);
        Assert.Equal(2.2, image.RgbWorkingSpace?.Gamma);
        Assert.Equal("Vega", Assert.Single(document.Tables).Rows[0].Cells[0].Value);
    }

    [Theory]
    [InlineData("<Property uid=\"Duplicate\" id=\"A\" type=\"String\">a</Property><Property uid=\"Duplicate\" id=\"B\" type=\"String\">b</Property>")]
    [InlineData("<Image geometry=\"1:1\" sampleFormat=\"UInt8\" location=\"inline:hex\">2a<Reference ref=\"Missing\" /></Image>")]
    public void RejectsInvalidCoreElementReferences(string body)
    {
        using MemoryStream stream = new(TestFile(body));
        Assert.Throws<XisfFormatException>(() => XisfReader.Load(stream));
    }

    [Fact]
    public void RejectsInvalidSignatureAndChecksumBeforeDecoding()
    {
        Assert.Throws<XisfFormatException>(() => XisfReader.Load(new MemoryStream(new byte[16])));

        string body = """
            <Metadata><Property id="XISF:CreationTime" type="TimePoint" value="2026-09-13T10:00:00Z" /><Property id="XISF:CreatorApplication" type="String">Tests</Property></Metadata>
            <Image geometry="1:1:1" sampleFormat="UInt8" location="attachment:{POSITION}:1" checksum="sha1:0000000000000000000000000000000000000000" />
            """;
        using MemoryStream stream = new(TestFile(body, [42]));
        XisfImage image = Assert.Single(XisfReader.Load(stream).Images);

        Assert.Throws<XisfChecksumException>(() => image.ReadRawData());
    }

    [Fact]
    public void LoadsDistributedHeaderAndIndexedDataBlockFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xisf-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            byte[] xisb = new byte[74];
            "XISB0100"u8.CopyTo(xisb);
            BinaryPrimitives.WriteUInt32LittleEndian(xisb.AsSpan(16), 1);
            BinaryPrimitives.WriteUInt64LittleEndian(xisb.AsSpan(32), 42);
            BinaryPrimitives.WriteUInt64LittleEndian(xisb.AsSpan(40), 72);
            BinaryPrimitives.WriteUInt64LittleEndian(xisb.AsSpan(48), 2);
            xisb[72] = 7;
            xisb[73] = 9;
            File.WriteAllBytes(Path.Combine(directory, "pixels.xisb"), xisb);
            string header = """
                <xisf version="1.0" xmlns="http://www.pixinsight.com/xisf">
                  <Metadata><Property id="XISF:CreationTime" type="TimePoint" value="2026-09-13T10:00:00Z" /><Property id="XISF:CreatorApplication" type="String">Tests</Property></Metadata>
                  <Image geometry="2:1:1" sampleFormat="UInt8" location="path(@header_dir/pixels.xisb):0x2a" />
                </xisf>
                """;
            string headerPath = Path.Combine(directory, "distributed.xish");
            File.WriteAllText(headerPath, header, new UTF8Encoding(false));

            XisfImage image = Assert.Single(XisfReader.Load(headerPath).Images);

            Assert.Equal([7d, 9d], image.GetSamples<double>());
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void DecompressesLz4ImageBlock()
    {
        byte[] raw = [3, 1, 4, 1];
        byte[] buffer = new byte[LZ4Codec.MaximumOutputSize(raw.Length)];
        int length = LZ4Codec.Encode(raw, buffer);
        byte[] compressed = buffer[..length];
        string body = $$"""
            <Metadata><Property id="XISF:CreationTime" type="TimePoint" value="2026-09-13T10:00:00Z" /><Property id="XISF:CreatorApplication" type="String">Tests</Property></Metadata>
            <Image geometry="4:1:1" sampleFormat="UInt8" location="embedded"><Data encoding="base64" compression="lz4:4">{{Convert.ToBase64String(compressed)}}</Data></Image>
            """;

        using MemoryStream stream = new(TestFile(body));

        Assert.Equal([3, 1, 4, 1], Assert.Single(XisfReader.Load(stream).Images).GetSamples<int>());
    }

    [Theory]
    [InlineData("slow.xisf")]
    public void LoadsExampleImage(string fileName)
    {
        string examplesDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Examples"));
        XisfDocument document = XisfReader.Load(Path.Combine(examplesDirectory, fileName));

        Assert.NotEmpty(document.Images);
        foreach (XisfImage image in document.Images)
        {
            byte[] data = image.ReadRawData();
            Assert.NotEmpty(data);
        }
    }

    private static byte[] TestFile(string body, byte[]? attachment = null)
    {
        const string rootStart = "<xisf version=\"1.0\" xmlns=\"http://www.pixinsight.com/xisf\">";
        string xml = rootStart + body + "</xisf>";
        if (attachment is not null)
        {
            int position = 16 + Encoding.UTF8.GetByteCount(xml);
            xml = xml.Replace("{POSITION}", position.ToString("D10"), StringComparison.Ordinal);
        }

        byte[] header = Encoding.UTF8.GetBytes(xml);
        byte[] file = new byte[16 + header.Length + (attachment?.Length ?? 0)];
        "XISF0100"u8.CopyTo(file);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(8), (uint)header.Length);
        header.CopyTo(file, 16);
        attachment?.CopyTo(file, 16 + header.Length);
        return file;
    }

    private static byte[] Shuffle(byte[] input, int itemSize)
    {
        byte[] output = new byte[input.Length];
        int count = input.Length / itemSize;
        for (int j = 0; j < itemSize; j++)
        for (int i = 0; i < count; i++)
            output[j * count + i] = input[i * itemSize + j];
        return output;
    }
}