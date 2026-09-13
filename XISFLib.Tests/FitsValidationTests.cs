using System.Text;

namespace XISFLib.Tests;

public class FitsValidationTests
{
    [Fact]
    public void RejectsHeaderCardLimitBeforeReadingUnboundedHeader()
    {
        using MemoryStream stream = CreateFits(
            Card("SIMPLE", "T"), Card("BITPIX", "8"), Card("NAXIS", "0"), Card("COMMENT", "one"), Card("END"));

        FitsFormatException exception = Assert.Throws<FitsFormatException>(() =>
            FitsReader.Load(stream, new FitsReaderOptions { MaximumHeaderCards = 4 }));

        Assert.Contains("header card", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsAxisAndTableCellResourceLimits()
    {
        using MemoryStream image = CreateFits(
            Card("SIMPLE", "T"), Card("BITPIX", "8"), Card("NAXIS", "2"), Card("NAXIS1", "1"), Card("NAXIS2", "1"), Card("END"),
            [1]);
        Assert.Throws<FitsFormatException>(() => FitsReader.Load(image, new FitsReaderOptions { MaximumAxisCount = 1 }));

        using MemoryStream table = CreateFits(
            Card("SIMPLE", "T"), Card("BITPIX", "8"), Card("NAXIS", "0"), Card("EXTEND", "T"), Card("END"),
            [],
            [Card("XTENSION", "'BINTABLE'"), Card("BITPIX", "8"), Card("NAXIS", "2"), Card("NAXIS1", "2"),
             Card("NAXIS2", "2"), Card("PCOUNT", "0"), Card("GCOUNT", "1"), Card("TFIELDS", "2"),
             Card("TFORM1", "'1B'"), Card("TFORM2", "'1B'"), Card("END")],
            [1, 2, 3, 4]);
        Assert.Throws<FitsFormatException>(() => FitsReader.Load(table, new FitsReaderOptions { MaximumTableCells = 3 }));
    }

    [Fact]
    public void EnforcesCumulativeDecodedByteLimit()
    {
        using MemoryStream stream = CreateFits(
            Card("SIMPLE", "T"), Card("BITPIX", "8"), Card("NAXIS", "1"), Card("NAXIS1", "2"), Card("EXTEND", "T"), Card("END"),
            [1, 2],
            [Card("XTENSION", "'IMAGE'"), Card("BITPIX", "8"), Card("NAXIS", "1"), Card("NAXIS1", "2"),
             Card("PCOUNT", "0"), Card("GCOUNT", "1"), Card("END")],
            [3, 4]);

        FitsFormatException exception = Assert.Throws<FitsFormatException>(() =>
            FitsReader.Load(stream, new FitsReaderOptions { MaximumTotalDecodedBytes = 3 }));

        Assert.Contains("total", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TolerantModeReturnsStructuredConformanceDiagnostics()
    {
        using MemoryStream stream = CreateFits(
            Card("SIMPLE", "T"), Card("NAXIS", "0"), Card("BITPIX", "8"), Card("NAXIS", "0"),
            Card("bad-key", "1"), Card("END"));

        FitsDocument document = FitsReader.Load(stream);

        Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Code == "FITS.MandatoryOrder" && diagnostic.HduIndex == 0);
        Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Code == "FITS.DuplicateKeyword" && diagnostic.Severity == FitsValidationSeverity.Error);
        Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Code == "FITS.KeywordSyntax");
    }

    [Fact]
    public void StrictModeRejectsConformanceErrors()
    {
        using MemoryStream stream = CreateFits(
            Card("SIMPLE", "T"), Card("NAXIS", "0"), Card("BITPIX", "8"), Card("END"));

        FitsFormatException exception = Assert.Throws<FitsFormatException>(() => FitsReader.Load(stream,
            new FitsReaderOptions { ValidationMode = FitsValidationMode.Strict }));

        Assert.Contains("mandatory", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReportsNonZeroPaddingAndInvalidDataChecksum()
    {
        using MemoryStream stream = CreateFits(
            Card("SIMPLE", "T"), Card("BITPIX", "8"), Card("NAXIS", "1"), Card("NAXIS1", "1"),
            Card("DATASUM", "'0'"), Card("END"), [42], paddingByte: 7);

        FitsDocument document = FitsReader.Load(stream);

        Assert.False(document.Hdus[0].DataChecksumValid);
        Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Code == "FITS.Padding");
        Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Code == "FITS.DataChecksum");
    }

    private static string Card(string keyword, string? value = null)
    {
        string text = keyword == "END" ? "END" : keyword is "COMMENT" or "HISTORY"
            ? $"{keyword,-8}{value}" : $"{keyword,-8}= {value}";
        return text.PadRight(80);
    }

    private static MemoryStream CreateFits(string first, string second, string third, string fourth, string fifth, string sixth,
        byte[]? data = null, string[]? extension = null, byte[]? extensionData = null, byte paddingByte = 0) =>
        CreateFits([first, second, third, fourth, fifth, sixth], data, extension, extensionData, paddingByte);

    private static MemoryStream CreateFits(string first, string second, string third, string fourth, string fifth,
        byte[]? data = null, string[]? extension = null, byte[]? extensionData = null, byte paddingByte = 0) =>
        CreateFits([first, second, third, fourth, fifth], data, extension, extensionData, paddingByte);

    private static MemoryStream CreateFits(params string[] cards) => CreateFits(cards, null, null, null, 0);

    private static MemoryStream CreateFits(string[] cards, byte[]? data, string[]? extension, byte[]? extensionData, byte paddingByte)
    {
        MemoryStream stream = new();
        WriteHdu(stream, cards, data ?? [], paddingByte);
        if (extension is not null) WriteHdu(stream, extension, extensionData ?? [], paddingByte);
        stream.Position = 0;
        return stream;
    }

    private static void WriteHdu(Stream stream, string[] cards, byte[] data, byte paddingByte)
    {
        byte[] header = Encoding.ASCII.GetBytes(string.Concat(cards).PadRight(((cards.Length * 80 + 2879) / 2880) * 2880));
        stream.Write(header);
        stream.Write(data);
        int padding = (2880 - data.Length % 2880) % 2880;
        if (padding > 0) stream.Write(Enumerable.Repeat(paddingByte, padding).Select(value => (byte)value).ToArray());
    }
}