using System.Buffers.Binary;
using System.Reflection;

namespace XISFLib.Tests;

public class FitsPerformanceRegressionTests
{
    private delegate uint Checksum(ReadOnlySpan<byte> bytes);
    private delegate uint SplitChecksum(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second);

    [Fact]
    public void SeparatePaddingWritesPreserveAsciiSpacesChecksumsAndFixedBuffers()
    {
        var table = new FitsHdu(FitsHduType.AsciiTable, new FitsHeader([
            new("NAXIS1", 5L), new("NAXIS2", 1L), new("TFIELDS", 1L),
            new("TTYPE1", "name"), new("TFORM1", "A5"), new("TBCOL1", 1L)
        ]), "hello"u8.ToArray());
        var document = new FitsDocument([FitsHdu.CreatePrimary(), table]);
        using var output = new MemoryStream();
        FitsWriter.Write(output, document, new() { WriteChecksums = true });
        byte[] serialized = output.ToArray();
        Assert.Equal(8640, serialized.Length);
        Assert.All(serialized.Skip(5765), value => Assert.Equal((byte)' ', value));
        var actual = FitsReader.Load(new MemoryStream(serialized), new() { ValidationMode = FitsValidationMode.Strict });
        Assert.All(actual.Hdus, hdu => { Assert.True(hdu.ChecksumValid); Assert.True(hdu.DataChecksumValid); });
        Assert.Equal("hello", Assert.IsType<FitsTable>(actual.Hdus[1].Content).Rows[0][0]);
        byte[] fixedBuffer = new byte[serialized.Length + 23];
        using var fixedStream = new MemoryStream(fixedBuffer, 23, serialized.Length, true, true);
        FitsWriter.Write(fixedStream, document, new() { WriteChecksums = true });
        Assert.Equal(serialized, fixedBuffer.AsSpan(23).ToArray());
        Assert.Equal(serialized.Length, fixedStream.Position);
    }

    [Fact]
    public void VectorChecksumMatchesIndependentReferenceAcrossCarriesTailsAndSplitWords()
    {
        if (Environment.GetEnvironmentVariable("DOTNET_EnableHWIntrinsic") == "0")
        {
            Assert.False(System.Runtime.Intrinsics.Vector256.IsHardwareAccelerated);
            Assert.False(System.Numerics.Vector.IsHardwareAccelerated);
        }
        Type type = typeof(FitsReader).Assembly.GetType("XISFLib.FitsChecksum")!;
        var sum = type.GetMethod("Sum", BindingFlags.Static | BindingFlags.NonPublic, null, [typeof(ReadOnlySpan<byte>)], null)!.CreateDelegate<Checksum>();
        var splitSum = type.GetMethod("Sum", BindingFlags.Static | BindingFlags.NonPublic, null, [typeof(ReadOnlySpan<byte>), typeof(ReadOnlySpan<byte>)], null)!.CreateDelegate<SplitChecksum>();
        var random = new Random(751);
        foreach (int length in Enumerable.Range(0, 160).Concat([1023, 1024, 1025, 32769, 1048579]))
        foreach (bool allOnes in new[] { false, true })
        {
            var bytes = new byte[length];
            if (allOnes) Array.Fill(bytes, byte.MaxValue);
            else random.NextBytes(bytes);
            uint expected = ReferenceSum(bytes);
            Assert.Equal(expected, sum(bytes));
            foreach (int split in Enumerable.Range(0, Math.Min(length, 12) + 1).Append(length))
                Assert.Equal(expected, splitSum(bytes.AsSpan(0, split), bytes.AsSpan(split)));
        }
    }

    [Fact]
    public void BulkEncodingMatchesIndependentBigEndianStorageForAllTypes()
    {
        Check(new byte[] { 0, 1, 128, 255 }, 8, 0, "000180ff");
        Check(new short[] { short.MinValue, -1, 0, short.MaxValue }, 16, 0, "8000ffff00007fff");
        Check(new ushort[] { 0, 1, 32768, ushort.MaxValue }, 16, 32768, "8000800100007fff");
        Check(new int[] { int.MinValue, -1, 0, int.MaxValue }, 32, 0, "80000000ffffffff000000007fffffff");
        Check(new uint[] { 0, 1, 2147483648, uint.MaxValue }, 32, 2147483648, "8000000080000001000000007fffffff");
        Check(new long[] { long.MinValue, -1, 0, long.MaxValue }, 64, 0, "8000000000000000ffffffffffffffff00000000000000007fffffffffffffff");
        Check(new ulong[] { 0, 1, 9223372036854775808, ulong.MaxValue }, 64, 9223372036854775808d, "8000000000000000800000000000000100000000000000007fffffffffffffff");
        Check(new float[] { -0f, float.Epsilon, BitConverter.Int32BitsToSingle(0x7fc12345), float.PositiveInfinity }, -32, 0, "80000000000000017fc123457f800000");
        Check(new double[] { -0d, double.Epsilon, BitConverter.Int64BitsToDouble(0x7ff8123456789abc), double.NegativeInfinity }, -64, 0, "800000000000000000000000000000017ff8123456789abcfff0000000000000");

        static void Check<T>(T[] values, int bitPix, double zero, string hex) where T : unmanaged
        {
            var image = FitsImage.FromPhysicalSamples(values, [values.Length]);
            Assert.Equal(bitPix, image.BitPix);
            Assert.Equal(zero, image.BZero);
            Assert.Equal(Convert.FromHexString(hex), image.ReadRawData());
        }
    }

    [Fact]
    public void VectorUnsignedOffsetsPreserveLargeValuesAndNonVectorTails()
    {
        ulong[] values = Enumerable.Range(0, 257).Select(i => ulong.MaxValue - (ulong)i).ToArray();
        var fits = FitsImage.FromPhysicalSamples(values, [257, 1]);
        byte[] raw = fits.ReadRawData();
        for (int i = 0; i < values.Length; i++)
            Assert.Equal(values[i] ^ (1UL << 63), BinaryPrimitives.ReadUInt64BigEndian(raw.AsSpan(i * 8)));
        var converted = FitsConverter.ToXisf(fits);
        for (int i = 0; i < values.Length; i++)
            Assert.Equal(values[i], BinaryPrimitives.ReadUInt64BigEndian(converted.RawData.Slice(i * 8)));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(-32)]
    [InlineData(-64)]
    public void FusedScalingRetainsPhysicalValuesBlankSamplesAndFiniteBounds(int bitPix)
    {
        long[] values = bitPix == 8 ? [0, 1, 2, 255] : [-3, 0, 2, 7];
        int size = Math.Abs(bitPix) / 8;
        byte[] raw = new byte[size * values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            Span<byte> sample = raw.AsSpan(i * size, size);
            switch (bitPix)
            {
                case 8: sample[0] = (byte)values[i]; break;
                case 16: BinaryPrimitives.WriteInt16BigEndian(sample, (short)values[i]); break;
                case 32: BinaryPrimitives.WriteInt32BigEndian(sample, (int)values[i]); break;
                case 64: BinaryPrimitives.WriteInt64BigEndian(sample, values[i]); break;
                case -32: BinaryPrimitives.WriteSingleBigEndian(sample, values[i]); break;
                case -64: BinaryPrimitives.WriteDoubleBigEndian(sample, values[i]); break;
            }
        }
        var image = FitsImage.FromRaw(raw, bitPix, [4, 1], -1.75, 0.25, bitPix > 0 ? 2 : null);
        var converted = FitsConverter.ToXisf(image);
        double[] expected = values.Select(v => bitPix > 0 && v == 2 ? double.NaN : v * -1.75 + 0.25).ToArray();
        for (int i = 0; i < values.Length; i++)
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]), BinaryPrimitives.ReadInt64BigEndian(converted.RawData.Slice(i * 8)));
        Assert.Equal(expected.Where(double.IsFinite).Min(), converted.LowerBound);
        Assert.Equal(expected.Where(double.IsFinite).Max(), converted.UpperBound);
        Assert.Equal(image.ReadRawData(), raw);
    }

    [Fact]
    public void FusedScalingRetainsEmptyFiniteRangeAndSigned64PrecisionGuards()
    {
        var blank = FitsImage.FromRaw(new byte[8], 16, [4, 1], 1, 0, 0);
        var converted = FitsConverter.ToXisf(blank);
        Assert.Equal(0d, converted.LowerBound);
        Assert.Equal(1d, converted.UpperBound);
        Assert.All(Enumerable.Range(0, 4), i => Assert.True(double.IsNaN(BinaryPrimitives.ReadDoubleBigEndian(converted.RawData.Slice(i * 8)))));
        Assert.Throws<NotSupportedException>(() => FitsConverter.ToXisf(FitsImage.FromPhysicalSamples(new long[] { (1L << 53) + 1 }, [1, 1])));
        var constant = FitsConverter.ToXisf(FitsImage.FromRaw(new byte[8], 16, [4, 1], 0, 42));
        Assert.Equal(42d, constant.LowerBound);
        Assert.Equal(43d, constant.UpperBound);
    }

    [Fact]
    public void SharedChecksumCalculationValidatesSingleCardsAndRejectsCorruption()
    {
        var document = new FitsDocument([FitsHdu.CreateImage(FitsImage.FromPhysicalSamples(new byte[] { 1, 2, 3, 4, 5 }, [5, 1]), true)]);
        using var stream = new MemoryStream();
        FitsWriter.Write(stream, document, new() { WriteChecksums = true });
        byte[] original = stream.ToArray();
        var both = FitsReader.Load(new MemoryStream(original));
        Assert.True(both.Hdus[0].ChecksumValid);
        Assert.True(both.Hdus[0].DataChecksumValid);
        byte[] corrupt = original.ToArray(); corrupt[2880] ^= 1;
        var actual = FitsReader.Load(new MemoryStream(corrupt));
        Assert.False(actual.Hdus[0].ChecksumValid);
        Assert.False(actual.Hdus[0].DataChecksumValid);
        Assert.Throws<FitsFormatException>(() => FitsReader.Load(new MemoryStream(corrupt), new() { ValidationMode = FitsValidationMode.Strict }));
        byte[] dataOnly = original.ToArray();
        for (int i = 0; i < 2880; i += 80)
            if (System.Text.Encoding.ASCII.GetString(dataOnly, i, 8) == "CHECKSUM") dataOnly.AsSpan(i, 80).Fill((byte)' ');
        var single = FitsReader.Load(new MemoryStream(dataOnly));
        Assert.Null(single.Hdus[0].ChecksumValid);
        Assert.True(single.Hdus[0].DataChecksumValid);
        byte[] checksumOnly = original.ToArray();
        for (int i = 0; i < 2880; i += 80)
            if (System.Text.Encoding.ASCII.GetString(checksumOnly, i, 8) == "DATASUM ") checksumOnly.AsSpan(i, 80).Fill((byte)' ');
        var checksumSingle = FitsReader.Load(new MemoryStream(checksumOnly));
        Assert.Null(checksumSingle.Hdus[0].DataChecksumValid);
        // Removing a header card also changes the HDU checksum.
        Assert.False(checksumSingle.Hdus[0].ChecksumValid);
    }

    private static uint ReferenceSum(ReadOnlySpan<byte> bytes)
    {
        ulong result = 0;
        for (int i = 0; i < bytes.Length; i += 4)
        {
            uint word = 0;
            for (int lane = 0; lane < 4; lane++) word = (word << 8) | (i + lane < bytes.Length ? bytes[i + lane] : 0u);
            result += word;
            result = (result & uint.MaxValue) + (result >> 32);
        }
        while (result >> 32 != 0) result = (result & uint.MaxValue) + (result >> 32);
        return (uint)result;
    }
}
