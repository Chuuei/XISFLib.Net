using System.Buffers.Binary;
using System.Numerics;

namespace XISFLib;

// FIPS 202 fallback for platforms whose native crypto provider does not offer SHA-3.
internal static class XisfSha3
{
    private static readonly ulong[] RoundConstants =
    [
        0x0000000000000001, 0x0000000000008082, 0x800000000000808a, 0x8000000080008000,
        0x000000000000808b, 0x0000000080000001, 0x8000000080008081, 0x8000000000008009,
        0x000000000000008a, 0x0000000000000088, 0x0000000080008009, 0x000000008000000a,
        0x000000008000808b, 0x800000000000008b, 0x8000000000008089, 0x8000000000008003,
        0x8000000000008002, 0x8000000000000080, 0x000000000000800a, 0x800000008000000a,
        0x8000000080008081, 0x8000000000008080, 0x0000000080000001, 0x8000000080008008
    ];
    private static readonly int[] Rotations = [0, 1, 62, 28, 27, 36, 44, 6, 55, 20, 3, 10, 43, 25, 39, 41, 45, 15, 21, 8, 18, 2, 61, 56, 14];
    public static byte[] HashData(ReadOnlySpan<byte> data, int bits)
    {
        int rate = bits == 256 ? 136 : 72;
        ulong[] state = new ulong[25];
        while (data.Length >= rate)
        {
            for (int i = 0; i < rate / 8; i++) state[i] ^= BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(i * 8, 8));
            Permute(state); data = data[rate..];
        }
        Span<byte> final = stackalloc byte[136]; final.Clear(); data.CopyTo(final);
        final[data.Length] ^= 0x06; final[rate - 1] ^= 0x80;
        for (int i = 0; i < rate / 8; i++) state[i] ^= BinaryPrimitives.ReadUInt64LittleEndian(final.Slice(i * 8, 8));
        Permute(state);
        byte[] result = new byte[bits / 8];
        for (int i = 0; i < result.Length / 8; i++) BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(i * 8, 8), state[i]);
        return result;
    }
    private static void Permute(ulong[] a)
    {
        Span<ulong> c = stackalloc ulong[5], d = stackalloc ulong[5], b = stackalloc ulong[25];
        foreach (ulong rc in RoundConstants)
        {
            for (int x = 0; x < 5; x++) c[x] = a[x] ^ a[x + 5] ^ a[x + 10] ^ a[x + 15] ^ a[x + 20];
            for (int x = 0; x < 5; x++) d[x] = c[(x + 4) % 5] ^ BitOperations.RotateLeft(c[(x + 1) % 5], 1);
            for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++)
                b[y + 5 * ((2 * x + 3 * y) % 5)] = BitOperations.RotateLeft(a[x + 5 * y] ^ d[x], Rotations[x + 5 * y]);
            for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++) a[x + 5 * y] = b[x + 5 * y] ^ (~b[(x + 1) % 5 + 5 * y] & b[(x + 2) % 5 + 5 * y]);
            a[0] ^= rc;
        }
    }
}
