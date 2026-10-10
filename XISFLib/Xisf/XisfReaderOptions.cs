namespace XISFLib;

/// <summary>Controls external-resource access and resource limits for XISF input.</summary>
public sealed class XisfReaderOptions
{
    internal XisfReaderOptions Snapshot() => (XisfReaderOptions)MemberwiseClone();
    /// <summary>Whether URL data blocks may be fetched; disabled by default.</summary>
    public bool AllowNetworkResources { get; set; }
    /// <summary>Whether general filesystem data-block paths may be read; disabled by default.</summary>
    public bool AllowExternalFilePaths { get; set; }
    /// <summary>Whether header-directory-relative blocks may be read; enabled by default.</summary>
    public bool AllowHeaderRelativeDataBlocks { get; set; } = true;
    /// <summary>Whether header-relative paths may escape the header directory; disabled by default.</summary>
    public bool AllowHeaderDirectoryTraversal { get; set; }
    /// <summary>The maximum input bytes buffered when the source must be retained in memory.</summary>
    public long MaximumBufferedInputBytes { get; set; } = 1024L * 1024 * 1024;
    /// <summary>The maximum encoded XML header size in bytes.</summary>
    public int MaximumXmlHeaderSize { get; set; } = 64 * 1024 * 1024;
    /// <summary>The maximum characters accepted by the XML parser.</summary>
    public long MaximumXmlCharacters { get; set; } = 64L * 1024 * 1024;
    /// <summary>The maximum XML nesting depth accepted after parsing.</summary>
    public int MaximumXmlDepth { get; set; } = 128;
    /// <summary>The maximum XML element count in the header.</summary>
    public int MaximumXmlElements { get; set; } = 1_000_000;
    /// <summary>The maximum number of Image elements in the header.</summary>
    public int MaximumImageCount { get; set; } = 10_000;
    /// <summary>The maximum image axis count, excluding channels.</summary>
    public int MaximumImageDimensions { get; set; } = 16;
    /// <summary>The maximum pixels per image, excluding channels.</summary>
    public long MaximumPixelsPerImage { get; set; } = 2_000_000_000;
    /// <summary>The maximum number of Property elements in the header.</summary>
    public int MaximumPropertyCount { get; set; } = 100_000;
    /// <summary>The maximum number of Cell elements in the header.</summary>
    public int MaximumTableCellCount { get; set; } = 10_000_000;
    /// <summary>The maximum serialized or decompressed block size in bytes.</summary>
    public int MaximumDataBlockSize { get; set; } = 1024 * 1024 * 1024;
    /// <summary>The maximum declared uncompressed-to-compressed size ratio.</summary>
    public double MaximumDecompressionRatio { get; set; } = 1000;
    /// <summary>Optional retrieval for non-HTTP URL schemes. The reader owns and disposes the returned stream.</summary>
    public Func<Uri, CancellationToken, Stream>? ExternalResourceResolver { get; set; }
    /// <summary>The timeout for external network requests.</summary>
    public TimeSpan NetworkTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

