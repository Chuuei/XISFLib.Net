using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Numerics;

namespace XISFLib;

internal static class XisfSampleBuffer
{
    public static void ToggleSignBits(Span<byte> bytes, int componentSize, bool bigEndian)
    {
        if (componentSize is not (2 or 4 or 8)) throw new ArgumentOutOfRangeException(nameof(componentSize));
        int signByte = bigEndian ? 0 : componentSize - 1;
        int offset = 0;
        if (Vector.IsHardwareAccelerated && bytes.Length >= Vector<byte>.Count)
        {
            Span<byte> maskBytes = stackalloc byte[Vector<byte>.Count];
            maskBytes.Clear();
            for (int i = signByte; i < maskBytes.Length; i += componentSize) maskBytes[i] = 0x80;
            var mask = new Vector<byte>(maskBytes);
            for (; offset <= bytes.Length - Vector<byte>.Count; offset += Vector<byte>.Count)
                (new Vector<byte>(bytes[offset..]) ^ mask).CopyTo(bytes[offset..]);
        }
        for (int i = offset + signByte; i < bytes.Length; i += componentSize) bytes[i] ^= 0x80;
    }

    // Every caller first restricts element types to the unmanaged XISF numeric representations.
    public static byte[] CopyArray(Array values, int byteLength, int componentSize)
    {
        var bytes = new byte[byteLength];
        MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetArrayDataReference(values), byteLength).CopyTo(bytes);
        if (!BitConverter.IsLittleEndian) ReverseComponents(bytes, componentSize);
        return bytes;
    }

    public static bool Matches<T>(XisfSampleFormat format) => format switch
    {
        XisfSampleFormat.UInt8 => typeof(T) == typeof(byte),
        XisfSampleFormat.UInt16 => typeof(T) == typeof(ushort),
        XisfSampleFormat.UInt32 => typeof(T) == typeof(uint),
        XisfSampleFormat.UInt64 => typeof(T) == typeof(ulong),
        XisfSampleFormat.Float32 => typeof(T) == typeof(float),
        XisfSampleFormat.Float64 => typeof(T) == typeof(double),
        _ => false
    };

    public static void CopyToArray<T>(ReadOnlySpan<byte> bytes, T[] destination, int componentSize, bool bigEndian)
    {
        bytes.CopyTo(MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference((Array)destination), bytes.Length));
        if (bigEndian == BitConverter.IsLittleEndian)
            ReverseComponents(MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference((Array)destination), bytes.Length), componentSize);
    }

    public static void ReverseComponents(Span<byte> bytes, int componentSize)
    {
        switch (componentSize)
        {
            case 1: break;
            case 2:
                var words = MemoryMarshal.Cast<byte, ushort>(bytes);
                BinaryPrimitives.ReverseEndianness(words, words);
                break;
            case 4:
                var dwords = MemoryMarshal.Cast<byte, uint>(bytes);
                BinaryPrimitives.ReverseEndianness(dwords, dwords);
                break;
            case 8:
                var qwords = MemoryMarshal.Cast<byte, ulong>(bytes);
                BinaryPrimitives.ReverseEndianness(qwords, qwords);
                break;
            case 16:
                var quads = MemoryMarshal.Cast<byte, UInt128>(bytes);
                BinaryPrimitives.ReverseEndianness(quads, quads);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(componentSize));
        }
    }
}
