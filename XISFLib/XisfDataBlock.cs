using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Buffers.Binary;
using System.Xml.Linq;
using K4os.Compression.LZ4;

namespace XISFLib;

internal sealed record XisfDataSource(byte[] File, string? BaseDirectory, string? FilePath, XisfReaderOptions Options)
{
    public byte[] ReadAttachment(long position, int size)
    {
        if (position < 0 || size < 0)
            throw new XisfFormatException("Invalid attached data block location.");
        if (size > Options.MaximumDataBlockSize)
            throw new XisfFormatException("The data block exceeds the configured size limit.");
        if (FilePath is null)
        {
            if (position > File.LongLength || size > File.LongLength - position || position > int.MaxValue)
                throw new XisfFormatException("Invalid attached data block location.");
            return File.AsSpan(checked((int)position), size).ToArray();
        }
        using FileStream stream = new(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (position > stream.Length || size > stream.Length - position)
            throw new XisfFormatException("Invalid attached data block location.");
        stream.Position = position;
        byte[] data = new byte[size];
        stream.ReadExactly(data);
        return data;
    }
}

internal sealed class XisfDataBlock
{
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly XElement _owner;
    private readonly XisfDataSource _source;

    public XisfDataBlock(XElement owner, XisfDataSource source)
    {
        _owner = owner;
        _source = source;
    }

    public byte[] Read()
    {
        XElement attributes = _owner;
        string location = (string?)_owner.Attribute("location") ?? (_owner.Elements().Any(e => e.Name.LocalName == "Data") ? "embedded" : "");
        byte[] serialized;
        if (location.StartsWith("inline:", StringComparison.OrdinalIgnoreCase))
            serialized = DecodeText(_owner.Value, location[7..]);
        else if (location.Equals("embedded", StringComparison.OrdinalIgnoreCase))
        {
            XElement data = _owner.Elements().FirstOrDefault(e => e.Name.LocalName == "Data")
                ?? throw new XisfFormatException("An embedded data block requires a Data child element.");
            attributes = data;
            serialized = DecodeText(data.Value, XisfReader.RequiredAttribute(data, "encoding"));
        }
        else if (location.StartsWith("attachment:", StringComparison.OrdinalIgnoreCase) || location.StartsWith("attached:", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = location.Split(':');
            if (parts.Length != 3 || !long.TryParse(parts[1], out long position) || !int.TryParse(parts[2], out int size))
                throw new XisfFormatException("Invalid attached data block location.");
            byte[]? streamed = ReadPathBackedZlibAttachment(position, size, attributes);
            if (streamed is not null)
                return streamed;
            serialized = _source.ReadAttachment(position, size);
        }
        else
            serialized = ReadExternal(location);

        if (serialized.Length > _source.Options.MaximumDataBlockSize)
            throw new XisfFormatException("The data block exceeds the configured size limit.");
        VerifyChecksum(serialized, (string?)attributes.Attribute("checksum") ?? (string?)_owner.Attribute("checksum"));
        return Decompress(serialized, attributes);
    }

    private byte[]? ReadPathBackedZlibAttachment(long position, int size, XElement attributes)
    {
        string? path = _source.FilePath;
        string? compression = (string?)attributes.Attribute("compression");
        if (path is null || string.IsNullOrWhiteSpace(compression) || attributes.Attribute("subblocks") is not null)
            return null;

        string[] parameters = compression.Split(':');
        if (parameters.Length != 2 || !parameters[0].Equals("zlib", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(parameters[1], out int expectedSize) || expectedSize < 0)
            return null;
        if (size < 0 || size > _source.Options.MaximumDataBlockSize || expectedSize > _source.Options.MaximumDataBlockSize)
            throw new XisfFormatException("The data block exceeds the configured size limit.");
        if ((expectedSize > 0 && size == 0) || (size > 0 && expectedSize / (double)size > _source.Options.MaximumDecompressionRatio))
            throw new XisfFormatException("The compressed data block exceeds the configured decompression-ratio limit.");

        string? checksum = (string?)attributes.Attribute("checksum") ?? (string?)_owner.Attribute("checksum");
        using IncrementalHash? hash = CreateIncrementalHash(checksum);
        using AttachmentReadStream attachment = new(path, position, size, hash);
        using ZLibStream zlib = new(attachment, CompressionMode.Decompress, true);
        byte[] result = new byte[expectedSize];
        try
        {
            zlib.ReadExactly(result);
        }
        catch (EndOfStreamException exception)
        {
            throw new XisfFormatException("The decompressed zlib block has an unexpected size.", exception);
        }
        if (zlib.ReadByte() >= 0)
            throw new XisfFormatException("The decompressed zlib block has an unexpected size.");
        zlib.Dispose();
        attachment.Drain();
        if (hash is not null)
            VerifyChecksumDigest(hash.GetHashAndReset(), checksum!);
        return result;
    }

    private byte[] ReadExternal(string location)
    {
        bool isUrl = location.StartsWith("url(", StringComparison.OrdinalIgnoreCase);
        bool isPath = location.StartsWith("path(", StringComparison.OrdinalIgnoreCase);
        if (!isUrl && !isPath)
            throw new XisfFormatException($"Unsupported data block location '{location}'.");
        int close = location.LastIndexOf(')');
        if (close < (isUrl ? 4 : 5))
            throw new XisfFormatException("Invalid external data block location.");
        string resource = location[(isUrl ? 4 : 5)..close];
        string? indexText = close + 1 < location.Length && location[close + 1] == ':' ? location[(close + 2)..] : null;
        byte[] file;
        if (isUrl)
        {
            if (!_source.Options.AllowNetworkResources)
                throw new XisfFormatException("Network data blocks are disabled by reader policy.");
            using CancellationTokenSource timeout = new(_source.Options.NetworkTimeout);
            using HttpResponseMessage response = Http.GetAsync(resource, HttpCompletionOption.ResponseHeadersRead, timeout.Token).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length > _source.Options.MaximumDataBlockSize)
                throw new XisfFormatException("The network data block exceeds the configured size limit.");
            using Stream network = response.Content.ReadAsStream(timeout.Token);
            using MemoryStream output = new();
            CopyToWithLimit(network, output, _source.Options.MaximumDataBlockSize);
            file = output.ToArray();
        }
        else
        {
            if (resource.StartsWith("@header_dir/", StringComparison.Ordinal))
            {
                if (!_source.Options.AllowHeaderRelativeDataBlocks)
                    throw new XisfFormatException("Header-relative data blocks are disabled by reader policy.");
                string baseDirectory = _source.BaseDirectory ?? throw new XisfFormatException("A base directory is required for @header_dir paths.");
                resource = Path.GetFullPath(Path.Combine(baseDirectory, resource[12..].Replace('/', Path.DirectorySeparatorChar)));
                string relative = Path.GetRelativePath(baseDirectory, resource);
                if (!_source.Options.AllowHeaderDirectoryTraversal && (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
                    throw new XisfFormatException("The external data-block path escapes the header directory.");
            }
            else
            {
                if (!_source.Options.AllowExternalFilePaths)
                    throw new XisfFormatException("External file data blocks are disabled by reader policy.");
                resource = resource.Replace('/', Path.DirectorySeparatorChar);
            }
            if (new FileInfo(resource).Length > _source.Options.MaximumDataBlockSize)
                throw new XisfFormatException("The data block exceeds the configured size limit.");
            file = File.ReadAllBytes(resource);
        }
        if (file.Length > _source.Options.MaximumDataBlockSize)
            throw new XisfFormatException("The data block exceeds the configured size limit.");
        return indexText is null ? file : ReadXisbBlock(file, ParseUnsigned(indexText));
    }

    private static void CopyToWithLimit(Stream input, Stream output, int maximumBytes)
    {
        byte[] buffer = new byte[81920];
        int total = 0;
        while (true)
        {
            int read = input.Read(buffer, 0, Math.Min(buffer.Length, maximumBytes - total + 1));
            if (read == 0) break;
            total = checked(total + read);
            if (total > maximumBytes)
                throw new XisfFormatException("The network data block exceeds the configured size limit.");
            output.Write(buffer, 0, read);
        }
    }

    private static byte[] ReadXisbBlock(byte[] file, ulong id)
    {
        if (file.Length < 32 || !file.AsSpan(0, 8).SequenceEqual("XISB0100"u8) || file.AsSpan(8, 8).ContainsAnyExcept((byte)0))
            throw new XisfFormatException("Invalid XISF data blocks file header.");
        ulong nodePosition = 16;
        HashSet<ulong> visited = [];
        HashSet<ulong> identifiers = [];
        while (nodePosition != 0 && visited.Add(nodePosition))
        {
            if (nodePosition > (ulong)file.Length - 16)
                throw new XisfFormatException("Invalid XISF block-index node position.");
            int node = checked((int)nodePosition);
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(node, 4));
            if (!file.AsSpan(node + 4, 4).SequenceEqual(new byte[4]) || (ulong)count > ((ulong)file.Length - nodePosition - 16) / 40)
                throw new XisfFormatException("Invalid XISF block-index node.");
            ulong next = BinaryPrimitives.ReadUInt64LittleEndian(file.AsSpan(node + 8, 8));
            for (uint i = 0; i < count; i++)
            {
                int entry = checked(node + 16 + (int)i * 40);
                ulong entryId = BinaryPrimitives.ReadUInt64LittleEndian(file.AsSpan(entry, 8));
                ulong position = BinaryPrimitives.ReadUInt64LittleEndian(file.AsSpan(entry + 8, 8));
                ulong length = BinaryPrimitives.ReadUInt64LittleEndian(file.AsSpan(entry + 16, 8));
                if (!file.AsSpan(entry + 32, 8).SequenceEqual(new byte[8]) || entryId != 0 && !identifiers.Add(entryId))
                    throw new XisfFormatException("Invalid XISF block-index entry.");
                if (entryId == id)
                {
                    if (position == 0 || position > (ulong)file.Length || length > (ulong)file.Length - position || length > int.MaxValue)
                        throw new XisfFormatException("Invalid XISF indexed data block dimensions.");
                    return file.AsSpan(checked((int)position), checked((int)length)).ToArray();
                }
            }
            nodePosition = next;
        }
        if (nodePosition != 0)
            throw new XisfFormatException("A cycle was detected in the XISF block index.");
        throw new XisfFormatException($"XISF data block index identifier '{id}' was not found.");
    }

    private static ulong ParseUnsigned(string text) => text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        ? Convert.ToUInt64(text[2..], 16)
        : ulong.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

    private static byte[] DecodeText(string text, string encoding) => encoding.ToLowerInvariant() switch
    {
        "base64" => Convert.FromBase64String(XisfReader.RemoveWhitespace(text)),
        "hex" => Convert.FromHexString(XisfReader.RemoveWhitespace(text)),
        _ => throw new XisfFormatException($"Unsupported inline encoding '{encoding}'.")
    };

    private static void VerifyChecksum(byte[] data, string? checksum)
    {
        if (string.IsNullOrWhiteSpace(checksum)) return;
        int separator = checksum.IndexOf(':');
        if (separator <= 0) throw new XisfFormatException("Invalid checksum attribute.");
        string algorithm = checksum[..separator].Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
        byte[] digest = algorithm switch
        {
            "sha1" => SHA1.HashData(data),
            "sha256" => SHA256.HashData(data),
            "sha512" => SHA512.HashData(data),
            "sha3256" when SHA3_256.IsSupported => SHA3_256.HashData(data),
            "sha3512" when SHA3_512.IsSupported => SHA3_512.HashData(data),
            _ => throw new XisfFormatException($"Unsupported checksum algorithm '{checksum[..separator]}'.")
        };
        VerifyChecksumDigest(digest, checksum);
    }

    private static IncrementalHash? CreateIncrementalHash(string? checksum)
    {
        if (string.IsNullOrWhiteSpace(checksum)) return null;
        int separator = checksum.IndexOf(':');
        if (separator <= 0) throw new XisfFormatException("Invalid checksum attribute.");
        string algorithm = checksum[..separator].Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
        HashAlgorithmName name = algorithm switch
        {
            "sha1" => HashAlgorithmName.SHA1,
            "sha256" => HashAlgorithmName.SHA256,
            "sha512" => HashAlgorithmName.SHA512,
            "sha3256" when SHA3_256.IsSupported => new HashAlgorithmName("SHA3-256"),
            "sha3512" when SHA3_512.IsSupported => new HashAlgorithmName("SHA3-512"),
            _ => throw new XisfFormatException($"Unsupported checksum algorithm '{checksum[..separator]}'.")
        };
        return IncrementalHash.CreateHash(name);
    }

    private static void VerifyChecksumDigest(byte[] digest, string checksum)
    {
        int separator = checksum.IndexOf(':');
        string actual = Convert.ToHexString(digest).ToLowerInvariant();
        if (!actual.Equals(checksum[(separator + 1)..], StringComparison.Ordinal))
            throw new XisfChecksumException("XISF data block checksum verification failed.");
    }

    private byte[] Decompress(byte[] data, XElement attributes)
    {
        string? compression = (string?)attributes.Attribute("compression");
        if (string.IsNullOrWhiteSpace(compression)) return data;
        string[] parameters = compression.Split(':');
        if (parameters.Length < 2 || !int.TryParse(parameters[1], out int expectedSize) || expectedSize < 0)
            throw new XisfFormatException("Invalid compression attribute.");
        if (expectedSize > _source.Options.MaximumDataBlockSize)
            throw new XisfFormatException("The decompressed data block exceeds the configured size limit.");
        if ((expectedSize > 0 && data.Length == 0) || (data.Length > 0 && expectedSize / (double)data.Length > _source.Options.MaximumDecompressionRatio))
            throw new XisfFormatException("The compressed data block exceeds the configured decompression-ratio limit.");
        string codec = parameters[0].ToLowerInvariant();
        bool shuffled = codec.EndsWith("+sh", StringComparison.Ordinal);
        string? subblocks = (string?)attributes.Attribute("subblocks");
        byte[] result = subblocks is null
            ? DecompressOne(data, codec, expectedSize)
            : DecompressSubblocks(data, codec, subblocks, expectedSize);
        if (shuffled)
        {
            if (parameters.Length != 3 || !int.TryParse(parameters[2], out int itemSize) || itemSize <= 0)
                throw new XisfFormatException("A shuffled compression codec requires a positive item size.");
            result = Unshuffle(result, itemSize);
        }
        return result;
    }

    private static byte[] DecompressSubblocks(byte[] data, string codec, string definition, int expectedSize)
    {
        using MemoryStream output = new(expectedSize);
        int offset = 0;
        foreach (string item in definition.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] sizes = item.Split(',');
            if (sizes.Length != 2 || !int.TryParse(sizes[0], out int compressed) || !int.TryParse(sizes[1], out int uncompressed) || compressed < 0 || uncompressed < 0 || compressed > data.Length - offset || uncompressed > expectedSize - output.Length)
                throw new XisfFormatException("Invalid compression subblocks attribute.");
            byte[] block = DecompressOne(data.AsSpan(offset, compressed).ToArray(), codec, uncompressed);
            output.Write(block);
            offset += compressed;
        }
        if (offset != data.Length || output.Length != expectedSize)
            throw new XisfFormatException("Compressed subblock lengths do not match the data block.");
        return output.ToArray();
    }

    private static byte[] DecompressOne(byte[] data, string codec, int expectedSize)
    {
        codec = codec.Replace("+sh", "", StringComparison.Ordinal);
        if (codec == "zlib")
        {
            using MemoryStream source = new(data);
            using ZLibStream zlib = new(source, CompressionMode.Decompress);
            byte[] result = new byte[expectedSize];
            try
            {
                zlib.ReadExactly(result);
            }
            catch (EndOfStreamException exception)
            {
                throw new XisfFormatException("The decompressed zlib block has an unexpected size.", exception);
            }
            if (zlib.ReadByte() >= 0) throw new XisfFormatException("The decompressed zlib block has an unexpected size.");
            return result;
        }
        if (codec is "lz4" or "lz4hc")
        {
            byte[] result = new byte[expectedSize];
            int decoded = LZ4Codec.Decode(data, result);
            if (decoded != expectedSize) throw new XisfFormatException("The decompressed LZ4 block has an unexpected size.");
            return result;
        }
        throw new XisfFormatException($"Unsupported compression codec '{codec}'.");
    }

    private static byte[] Unshuffle(byte[] input, int itemSize)
    {
        byte[] output = new byte[input.Length];
        int count = input.Length / itemSize;
        int source = 0;
        for (int j = 0; j < itemSize; j++)
        for (int i = 0; i < count; i++)
            output[i * itemSize + j] = input[source++];
        input.AsSpan(count * itemSize).CopyTo(output.AsSpan(count * itemSize));
        return output;
    }
}

internal sealed class AttachmentReadStream : Stream
{
    private readonly FileStream _stream;
    private readonly IncrementalHash? _hash;
    private long _remaining;

    public AttachmentReadStream(string path, long position, int size, IncrementalHash? hash)
    {
        _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan);
        if (position < 0 || position > _stream.Length || size < 0 || size > _stream.Length - position)
        {
            _stream.Dispose();
            throw new XisfFormatException("Invalid attached data block location.");
        }
        _stream.Position = position;
        _remaining = size;
        _hash = hash;
    }

    public void Drain()
    {
        Span<byte> buffer = stackalloc byte[16384];
        while (Read(buffer) > 0) { }
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (_remaining == 0) return 0;
        int requested = (int)Math.Min(buffer.Length, _remaining);
        int read = _stream.Read(buffer[..requested]);
        if (read == 0) throw new EndOfStreamException();
        _hash?.AppendData(buffer[..read]);
        _remaining -= read;
        return read;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _stream.Dispose();
        base.Dispose(disposing);
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
