using System.Buffers.Binary;
using System.Text;

namespace XISFLib.Tests;

public class XisfReaderSecurityTests
{
    [Fact]
    public void ExternalAndNetworkResourcesAreDisabledByDefault()
    {
        XisfReaderOptions options = new();

        Assert.False(options.AllowNetworkResources);
        Assert.False(options.AllowExternalFilePaths);
        Assert.True(options.AllowHeaderRelativeDataBlocks);
        Assert.False(options.AllowHeaderDirectoryTraversal);
    }

    [Fact]
    public void RejectsOversizedAndDeepXmlBeforeBuildingDocument()
    {
        Assert.Throws<XisfFormatException>(() => XisfReader.Load(File("<Image geometry=\"1:1\" sampleFormat=\"UInt8\" location=\"inline:hex\">00</Image>"),
            options: new XisfReaderOptions { MaximumXmlHeaderSize = 32 }));
        Assert.Throws<XisfFormatException>(() => XisfReader.Load(File("<A><B><C /></B></A>"),
            options: new XisfReaderOptions { MaximumXmlDepth = 2 }));
    }

    [Fact]
    public void RejectsImageAndDecompressionBombLimits()
    {
        Assert.Throws<XisfFormatException>(() => XisfReader.Load(File("<Image geometry=\"100:100\" sampleFormat=\"UInt8\" location=\"inline:hex\"></Image>"),
            options: new XisfReaderOptions { MaximumPixelsPerImage = 100 }));

        string compressed = Convert.ToBase64String([1]);
        XisfImage image = Assert.Single(XisfReader.Load(File($"<Image geometry=\"100:1\" sampleFormat=\"UInt8\" location=\"embedded\"><Data encoding=\"base64\" compression=\"zlib:100\">{compressed}</Data></Image>"),
            options: new XisfReaderOptions { MaximumDecompressionRatio = 10 }).Images);
        Assert.Throws<XisfFormatException>(() => image.ReadRawData());
    }

    private static MemoryStream File(string body)
    {
        string xml = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><xisf xmlns=\"http://www.pixinsight.com/xisf\" version=\"1.0\">{body}</xisf>";
        byte[] header = Encoding.UTF8.GetBytes(xml);
        byte[] file = new byte[16 + header.Length];
        "XISF0100"u8.CopyTo(file);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(8), (uint)header.Length);
        header.CopyTo(file, 16);
        return new MemoryStream(file);
    }
}