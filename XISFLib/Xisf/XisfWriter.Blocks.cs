using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using K4os.Compression.LZ4;

namespace XISFLib;

public static partial class XisfWriter
{
    private static void AddAttachment(XElement element, byte[] raw, int itemSize, XisfWriterOptions options, List<Attachment> attachments)
    {
        byte[] serialized = options.Shuffle && options.Compression != XisfCompression.None && itemSize > 1 ? Shuffle(raw, itemSize) : raw;
        if (options.CompressionSubblockSize > 0 && options.Compression != XisfCompression.None && serialized.Length > options.CompressionSubblockSize)
        {
            using MemoryStream packed = new();
            List<string> subblocks = [];
            for (int offset = 0; offset < serialized.Length; offset += options.CompressionSubblockSize)
            {
                int count = Math.Min(options.CompressionSubblockSize, serialized.Length - offset);
                byte[] compressed = Compress(serialized.AsSpan(offset, count).ToArray(), options.Compression);
                packed.Write(compressed); subblocks.Add($"{compressed.Length},{count}");
            }
            serialized = packed.ToArray();
            element.SetAttributeValue("subblocks", string.Join(':', subblocks));
        }
        else serialized = Compress(serialized, options.Compression);
        if (options.Compression != XisfCompression.None)
        {
            string codec = options.Compression switch
            {
                XisfCompression.Zlib => "zlib",
                XisfCompression.Lz4 => "lz4",
                XisfCompression.Lz4Hc => "lz4hc",
                XisfCompression.Zstd => "zstd",
                _ => throw new ArgumentOutOfRangeException()
            };
            if (options.Shuffle && itemSize > 1) codec += "+sh";
            element.SetAttributeValue("compression", $"{codec}:{raw.Length}" + (codec.EndsWith("+sh", StringComparison.Ordinal) ? $":{itemSize}" : ""));
        }
        string? checksum = Checksum(serialized, options.Checksum);
        if (checksum is not null) element.SetAttributeValue("checksum", checksum);
        if (options.DataBlockStorage != XisfDataBlockStorage.Attachment && serialized.Length <= options.MaximumInlineBlockSize)
        {
            string encoding = options.HexEncoding ? "hex" : "base64";
            string text = options.HexEncoding ? Convert.ToHexString(serialized).ToLowerInvariant() : Convert.ToBase64String(serialized);
            bool embedded = options.DataBlockStorage == XisfDataBlockStorage.Embedded || element.Name.LocalName is "Image" or "Thumbnail";
            if (embedded)
            {
                XElement data = new(Namespace + "Data", new XAttribute("encoding", encoding), text);
                foreach (string name in new[] { "compression", "checksum", "subblocks" })
                    if (element.Attribute(name) is { } attribute) { data.SetAttributeValue(name, attribute.Value); attribute.Remove(); }
                element.SetAttributeValue("location", "embedded"); element.Add(data);
            }
            else { element.SetAttributeValue("location", "inline:" + encoding); element.Value = text; }
            return;
        }
        element.SetAttributeValue("location", "attachment:00000000000000000000:" + serialized.Length);
        attachments.Add(new Attachment(element, serialized, raw.LongLength));
    }

    private static byte[] Compress(byte[] data, XisfCompression compression)
    {
        if (compression == XisfCompression.None) return data;
        if (compression == XisfCompression.Zstd)
        {
            using ZstdSharp.Compressor compressor = new();
            return compressor.Wrap(data).ToArray();
        }
        if (compression == XisfCompression.Zlib)
        {
            using MemoryStream output = new();
            using (ZLibStream zlib = new(output, CompressionLevel.SmallestSize, true)) zlib.Write(data);
            return output.ToArray();
        }
        byte[] buffer = new byte[LZ4Codec.MaximumOutputSize(data.Length)];
        LZ4Level level = compression == XisfCompression.Lz4Hc ? LZ4Level.L12_MAX : LZ4Level.L00_FAST;
        int length = LZ4Codec.Encode(data, buffer, level);
        if (length <= 0) throw new InvalidOperationException("LZ4 compression failed.");
        return buffer[..length];
    }

    private static byte[] Shuffle(byte[] input, int itemSize)
    {
        byte[] output = new byte[input.Length];
        int count = input.Length / itemSize;
        int target = 0;
        for (int component = 0; component < itemSize; component++)
        for (int item = 0; item < count; item++)
            output[target++] = input[item * itemSize + component];
        input.AsSpan(count * itemSize).CopyTo(output.AsSpan(target));
        return output;
    }

    private static string? Checksum(byte[] data, XisfChecksum checksum)
    {
        byte[]? digest = checksum switch
        {
            XisfChecksum.None => null,
            XisfChecksum.Sha1 => SHA1.HashData(data),
            XisfChecksum.Sha256 => SHA256.HashData(data),
            XisfChecksum.Sha512 => SHA512.HashData(data),
            XisfChecksum.Sha3_256 when SHA3_256.IsSupported => SHA3_256.HashData(data),
            XisfChecksum.Sha3_512 when SHA3_512.IsSupported => SHA3_512.HashData(data),
            XisfChecksum.Sha3_256 => XisfSha3.HashData(data, 256),
            XisfChecksum.Sha3_512 => XisfSha3.HashData(data, 512),
            _ => throw new ArgumentOutOfRangeException(nameof(checksum))
        };
        if (digest is null) return null;
        string name = checksum switch
        {
            XisfChecksum.Sha1 => "sha1",
            XisfChecksum.Sha256 => "sha256",
            XisfChecksum.Sha512 => "sha512",
            XisfChecksum.Sha3_256 => "sha3-256",
            XisfChecksum.Sha3_512 => "sha3-512",
            _ => throw new ArgumentOutOfRangeException(nameof(checksum))
        };
        return $"{name}:{Convert.ToHexString(digest).ToLowerInvariant()}";
    }

}
