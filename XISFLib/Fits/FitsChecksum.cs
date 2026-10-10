using System.Buffers.Binary;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;

namespace XISFLib;

internal static class FitsChecksum
{
    internal static uint Sum(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        int alignedLength = first.Length & ~3;
        uint sum = Sum(first[..alignedLength]);
        int remainder = first.Length - alignedLength;
        if (remainder == 0)
            return Add(sum, Sum(second));

        Span<byte> crossingWord = stackalloc byte[4];
        crossingWord.Clear();
        first[alignedLength..].CopyTo(crossingWord);
        int fromSecond = Math.Min(4 - remainder, second.Length);
        second[..fromSecond].CopyTo(crossingWord[remainder..]);
        sum = Add(sum, BinaryPrimitives.ReadUInt32BigEndian(crossingWord));
        return Add(sum, Sum(second[fromSecond..]));
    }

    internal static uint Sum(ReadOnlySpan<byte> bytes)
    {
        ulong sum = 0;
        int index = 0;
        if (Vector256.IsHardwareAccelerated && BitConverter.IsLittleEndian)
        {
            // Widen before accumulating: 32-bit lanes would lose end-around carries.
            // Even the largest supported span's entire word sum fits in 64 bits.
            Vector256<ulong> accumulator = Vector256<ulong>.Zero;
            Vector256<byte> swap = Vector256.Create(0x00010203u, 0x04050607u, 0x08090a0bu, 0x0c0d0e0fu,
                0x10111213u, 0x14151617u, 0x18191a1bu, 0x1c1d1e1fu).AsByte();
            for (; index <= bytes.Length - Vector256<byte>.Count; index += Vector256<byte>.Count)
            {
                var words = Vector256.Shuffle(Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(bytes), (nuint)index), swap).AsUInt32();
                var (lower, upper) = Vector256.Widen(words);
                accumulator += lower + upper;
            }
            sum = Vector256.Sum(accumulator);
        }
        while (index <= bytes.Length - 4)
        {
            sum += BinaryPrimitives.ReadUInt32BigEndian(bytes[index..]);
            index += 4;
        }
        if (index < bytes.Length)
        {
            Span<byte> tail = stackalloc byte[4];
            tail.Clear();
            bytes[index..].CopyTo(tail);
            sum += BinaryPrimitives.ReadUInt32BigEndian(tail);
        }
        while (sum >> 32 != 0) sum = (sum & uint.MaxValue) + (sum >> 32);
        return (uint)sum;
    }

    internal static uint Add(uint left, uint right)
    {
        ulong sum = (ulong)left + right;
        while (sum >> 32 != 0) sum = (sum & uint.MaxValue) + (sum >> 32);
        return (uint)sum;
    }
}
