namespace XISFLib;

/// <summary>Controls whether FITS conformance errors are recorded or rejected.</summary>
public enum FitsValidationMode
{
    /// <summary>Records conformance findings while accepting supported input.</summary>
    Tolerant,
    /// <summary>Rejects error-level conformance findings.</summary>
    Strict
}

/// <summary>Controls FITS validation and limits for input, headers, and decoded data.</summary>
public sealed class FitsReaderOptions
{
    internal FitsReaderOptions Snapshot() => (FitsReaderOptions)MemberwiseClone();
    /// <summary>The maximum number of HDUs per FITS document.</summary>
    public int MaximumHduCount { get; set; } = 1024;
    /// <summary>The maximum raw payload bytes per FITS HDU.</summary>
    public long MaximumDataSize { get; set; } = int.MaxValue;
    /// <summary>The maximum parsed header-card count per HDU.</summary>
    public int MaximumHeaderCards { get; set; } = 100_000;
    /// <summary>The maximum padded FITS header size in bytes per HDU.</summary>
    public long MaximumHeaderBytes { get; set; } = 8L * 1024 * 1024;
    /// <summary>The maximum image axis count.</summary>
    public int MaximumAxisCount { get; set; } = 32;
    /// <summary>The maximum cumulative raw FITS HDU payload bytes retained by the reader.</summary>
    public long MaximumTotalDecodedBytes { get; set; } = int.MaxValue;
    /// <summary>The maximum decoded cells per FITS table.</summary>
    public long MaximumTableCells { get; set; } = 10_000_000;
    /// <summary>The maximum input bytes buffered when the source must be retained in memory.</summary>
    public long MaximumBufferedInputBytes { get; set; } = 1024L * 1024 * 1024;
    /// <summary>Whether error-level conformance findings are retained or cause the reader to throw.</summary>
    public FitsValidationMode ValidationMode { get; set; }
}

