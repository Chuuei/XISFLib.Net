using System.Xml.Linq;

namespace XISFLib;

/// <summary>The compression codec applied to serialized data blocks.</summary>
public enum XisfCompression
{
    /// <summary>Disables this operation.</summary>
    None,
    /// <summary>Uses zlib compression.</summary>
    Zlib,
    /// <summary>Uses the fast LZ4 compression codec.</summary>
    Lz4,
    /// <summary>Uses high-compression LZ4.</summary>
    Lz4Hc,
    /// <summary>Uses Zstandard compression, introduced in XISF Revision 1.</summary>
    Zstd
}

/// <summary>The placement of serialized binary blocks in a newly written header.</summary>
public enum XisfDataBlockStorage
{
    /// <summary>Stores blocks following the monolithic header, or in the distributed block file.</summary>
    Attachment,
    /// <summary>Stores text-encoded blocks in child Data elements.</summary>
    Embedded,
    /// <summary>Stores blocks inline where permitted; image samples use embedded Data elements.</summary>
    Inline
}

/// <summary>The digest algorithm applied to serialized data blocks.</summary>
public enum XisfChecksum
{
    /// <summary>Disables this operation.</summary>
    None,
    /// <summary>Uses the SHA1 digest algorithm.</summary>
    Sha1,
    /// <summary>Uses the SHA256 digest algorithm.</summary>
    Sha256,
    /// <summary>Uses the SHA512 digest algorithm.</summary>
    Sha512,
    /// <summary>Uses the SHA3-256 digest algorithm.</summary>
    Sha3_256,
    /// <summary>Uses the SHA3-512 digest algorithm.</summary>
    Sha3_512
}

/// <summary>Controls attachment compression, checksums, and creation metadata.</summary>
public sealed class XisfWriterOptions
{
    /// <summary>The attachment codec; the default is uncompressed.</summary>
    public XisfCompression Compression { get; set; }
    /// <summary>Whether sample bytes are shuffled before compression.</summary>
    public bool Shuffle { get; set; }
    /// <summary>The attachment digest algorithm; the default is SHA-256.</summary>
    public XisfChecksum Checksum { get; set; } = XisfChecksum.Sha256;
    /// <summary>The block placement; defaults to attachments.</summary>
    public XisfDataBlockStorage DataBlockStorage { get; set; }
    /// <summary>The maximum serialized size of inline and embedded blocks; larger blocks use attachments.</summary>
    public ushort MaximumInlineBlockSize { get; set; } = ushort.MaxValue;
    /// <summary>Whether text-encoded blocks use hexadecimal rather than Base64.</summary>
    public bool HexEncoding { get; set; }
    /// <summary>The maximum uncompressed bytes per independently compressed subblock; zero disables splitting.</summary>
    public int CompressionSubblockSize { get; set; }
    /// <summary>The alignment in bytes for attached blocks; one disables padding.</summary>
    public int BlockAlignmentSize { get; set; } = 1;
    /// <summary>Whether nonempty String properties are stored as UTF-8 blocks.</summary>
    public bool StringsAsDataBlocks { get; set; }
    /// <summary>The application name written to XISF creation metadata.</summary>
    public string CreatorApplication { get; set; } = "XISFLib.Net " + typeof(XisfWriter).Assembly.GetName().Version;
    /// <summary>The timestamp written to XISF creation metadata.</summary>
    public DateTimeOffset CreationTime { get; set; } = DateTimeOffset.UtcNow;
    internal XisfWriterOptions Snapshot() => (XisfWriterOptions)MemberwiseClone();
}

