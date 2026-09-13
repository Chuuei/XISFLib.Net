using System.Numerics;
using System.Text;

namespace XISFLib.Tests;

public class XisfPropertyTypeTests
{
    [Fact]
    public void ParsesRequiredScalarTypesAndAliases()
    {
        const string properties = """
            <Property id="i8" type="Int8" value="-8" />
            <Property id="u8" type="Byte" value="8" />
            <Property id="i16" type="Short" value="-16" />
            <Property id="u16" type="UShort" value="16" />
            <Property id="i32" type="Int" value="-32" />
            <Property id="u32" type="UInt" value="32" />
            <Property id="i64" type="Int64" value="-64" />
            <Property id="u64" type="UInt64" value="64" />
            <Property id="f32" type="Float" value="1.25" />
            <Property id="f64" type="Double" value="2.5" />
            <Property id="c32" type="Complex32" value="(1.5,-2.5)" />
            <Property id="c64" type="Complex" value="(3.5,-4.5)" />
            """;

        XisfDocument document = Load(properties);

        Assert.IsType<sbyte>(document.Properties["i8"].Value);
        Assert.IsType<byte>(document.Properties["u8"].Value);
        Assert.IsType<short>(document.Properties["i16"].Value);
        Assert.IsType<ushort>(document.Properties["u16"].Value);
        Assert.IsType<int>(document.Properties["i32"].Value);
        Assert.IsType<uint>(document.Properties["u32"].Value);
        Assert.IsType<long>(document.Properties["i64"].Value);
        Assert.IsType<ulong>(document.Properties["u64"].Value);
        Assert.Equal(1.25f, Assert.IsType<float>(document.Properties["f32"].Value));
        Assert.Equal(2.5, Assert.IsType<double>(document.Properties["f64"].Value));
        Assert.Equal(new Complex(1.5, -2.5), Assert.IsType<Complex>(document.Properties["c32"].Value));
        Assert.Equal(new Complex(3.5, -4.5), Assert.IsType<Complex>(document.Properties["c64"].Value));
    }

    [Fact]
    public void ParsesVectorAndMatrixAliasesAndComplexCollections()
    {
        const string properties = """
            <Property id="bytes" type="ByteArray" length="3" location="inline:hex">0102ff</Property>
            <Property id="ints" type="IVector" length="2" location="inline:hex">ffffffff02000000</Property>
            <Property id="uints" type="UIVector" length="2" location="inline:hex">01000000ffffffff</Property>
            <Property id="vector" type="Vector" length="2" location="inline:hex">000000000000f83f00000000000004c0</Property>
            <Property id="bytes2d" type="ByteMatrix" rows="2" columns="2" location="inline:hex">01020304</Property>
            <Property id="matrix" type="Matrix" rows="1" columns="2" location="inline:hex">000000000000f03f00000000000000c0</Property>
            <Property id="complex32" type="C32Vector" length="2" location="inline:hex">0000803f000000c000004040000080c0</Property>
            <Property id="complex64" type="C64Matrix" rows="1" columns="2" location="inline:hex">000000000000f03f00000000000000c0000000000000084000000000000010c0</Property>
            """;

        XisfDocument document = Load(properties);

        Assert.Equal([1, 2, 255], Assert.IsType<byte[]>(document.Properties["bytes"].Value));
        Assert.Equal([-1, 2], Assert.IsType<int[]>(document.Properties["ints"].Value));
        Assert.Equal([1u, uint.MaxValue], Assert.IsType<uint[]>(document.Properties["uints"].Value));
        Assert.Equal([1.5, -2.5], Assert.IsType<double[]>(document.Properties["vector"].Value));
        Assert.Equal(4, Assert.IsType<byte[,]>(document.Properties["bytes2d"].Value)[1, 1]);
        Assert.Equal(-2, Assert.IsType<double[,]>(document.Properties["matrix"].Value)[0, 1]);
        Assert.Equal(new Complex(3, -4), Assert.IsType<Complex[]>(document.Properties["complex32"].Value)[1]);
        Assert.Equal(new Complex(3, -4), Assert.IsType<Complex[,]>(document.Properties["complex64"].Value)[0, 1]);
    }

    [Fact]
    public void WritesAndReadsAllSupportedArrayTypesIncludingInt128()
    {
        XisfWriterOptions options = new() { Compression = XisfCompression.None };
        options.Properties["i128"] = Int128.MinValue + 1;
        options.Properties["u128"] = UInt128.MaxValue - 1;
        options.Properties["i8"] = new sbyte[] { -1, 2 };
        options.Properties["u8"] = new byte[] { 1, 255 };
        options.Properties["i16"] = new short[] { -2, 3 };
        options.Properties["u16"] = new ushort[] { 2, 65535 };
        options.Properties["i32"] = new[] { -3, 4 };
        options.Properties["u32"] = new[] { 3u, uint.MaxValue };
        options.Properties["i64"] = new[] { -4L, 5L };
        options.Properties["u64"] = new[] { 4UL, ulong.MaxValue };
        options.Properties["i128v"] = new[] { Int128.MinValue + 1, Int128.MaxValue };
        options.Properties["u128m"] = new UInt128[,] { { 1, UInt128.MaxValue } };
        options.Properties["f32"] = new[] { 1.25f, -2.5f };
        options.Properties["f64"] = new[,] { { 1.25, -2.5 } };
        options.Properties["complex"] = new[,] { { new Complex(1, -2), new Complex(3, -4) } };

        using MemoryStream output = new();
        XisfWriter.Write(output, [XisfImageData.FromSamples(new byte[] { 1 }, [1], 1)], options);
        output.Position = 0;
        XisfDocument document = XisfReader.Load(output);

        Assert.Equal(Int128.MinValue + 1, Assert.IsType<Int128>(document.Properties["i128"].Value));
        Assert.Equal(UInt128.MaxValue - 1, Assert.IsType<UInt128>(document.Properties["u128"].Value));
        Assert.Equal([Int128.MinValue + 1, Int128.MaxValue], Assert.IsType<Int128[]>(document.Properties["i128v"].Value));
        Assert.Equal(UInt128.MaxValue, Assert.IsType<UInt128[,]>(document.Properties["u128m"].Value)[0, 1]);
        Assert.Equal(new Complex(3, -4), Assert.IsType<Complex[,]>(document.Properties["complex"].Value)[0, 1]);
        Assert.Equal("I128Vector", document.Properties["i128v"].Type);
        Assert.Equal("UI128Matrix", document.Properties["u128m"].Type);
        Assert.Equal("C64Matrix", document.Properties["complex"].Type);
    }

    private static XisfDocument Load(string properties)
    {
        string xml = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <xisf xmlns="http://www.pixinsight.com/xisf" version="1.0">
              {{properties}}
            </xisf>
            """;
        return XisfReader.Load(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
    }
}