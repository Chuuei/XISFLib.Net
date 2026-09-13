using System.Buffers.Binary;
using System.Text;

namespace XISFLib.Tests;

public class XisfValidationTests
{
    [Fact]
    public void ReportsInvalidImageAndDataBlockAttributes()
    {
        XisfDocument document = Load("""
            <Metadata><Property id="XISF:CreationTime" type="TimePoint" value="2026-09-13T10:00:00Z" /><Property id="XISF:CreatorApplication" type="String">Tests</Property></Metadata>
            <Image geometry="1:1" sampleFormat="UInt8" bounds="1:0" imageType="Unknown" byteOrder="middle" location="inline:hex">2a<FITSKeyword name="bad name" value="1" /></Image>
            """);

        IReadOnlyList<XisfDiagnostic> diagnostics = document.Validate();

        Assert.Contains(diagnostics, item => item.Code == "XISF010");
        Assert.Contains(diagnostics, item => item.Code == "XISF039");
        Assert.Contains(diagnostics, item => item.Code == "XISF040");
        Assert.Contains(diagnostics, item => item.Code == "XISF041");
        Assert.Contains(diagnostics, item => item.Code == "XISF042");
    }

    [Fact]
    public void ValidatesIccProfileHeaderAndEmbeddedFlag()
    {
        byte[] profile = new byte[128];
        BinaryPrimitives.WriteUInt32BigEndian(profile, 128);
        "acsp"u8.CopyTo(profile.AsSpan(36));
        string invalidProfile = Convert.ToBase64String(profile);
        string body = $"""
            <Metadata><Property id="XISF:CreationTime" type="TimePoint" value="2026-09-13T10:00:00Z" /><Property id="XISF:CreatorApplication" type="String">Tests</Property></Metadata>
            <Image geometry="1:1" sampleFormat="UInt8" location="embedded"><Data encoding="hex">2a</Data><ICCProfile location="inline:base64">{invalidProfile}</ICCProfile></Image>
            """;
        XisfDocument document = Load(body);

        Assert.Contains(document.Validate(), item => item.Code == "XISF044");

        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(44), 1);
        document = Load(body.Replace(invalidProfile, Convert.ToBase64String(profile), StringComparison.Ordinal));
        Assert.DoesNotContain(document.Validate(), item => item.Code == "XISF044");
    }

    [Fact]
    public void WriterCanonicalizesImageTypesAndEmbedsIccProfiles()
    {
        byte[] profile = new byte[128];
        "acsp"u8.CopyTo(profile.AsSpan(36));
        XisfImageData image = XisfImageData.FromSamples(new byte[] { 1 }, [1], 1);
        image.ImageType = "ScienceFrame";
        image.IccProfile = profile;
        using MemoryStream stream = new();

        XisfWriter.Write(stream, [image]);
        stream.Position = 0;
        XisfDocument document = XisfReader.Load(stream);

        Assert.Equal("Light", Assert.Single(document.Images).ImageType);
        Assert.DoesNotContain(document.Validate(), item => item.Code == "XISF044");
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32BigEndian(profile.AsSpan(44, 4)));
    }

    private static XisfDocument Load(string body)
    {
        string xml = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><xisf xmlns=\"http://www.pixinsight.com/xisf\" version=\"1.0\">{body}</xisf>";
        byte[] header = Encoding.UTF8.GetBytes(xml);
        byte[] file = new byte[16 + header.Length];
        "XISF0100"u8.CopyTo(file);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(8), (uint)header.Length);
        header.CopyTo(file, 16);
        return XisfReader.Load(new MemoryStream(file));
    }
}