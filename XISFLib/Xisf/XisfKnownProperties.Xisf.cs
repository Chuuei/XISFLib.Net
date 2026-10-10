namespace XISFLib;

public static partial class XisfKnownProperties
{

    /// <summary>Predefined properties in the Xisf group.</summary>
    public static class Xisf
    {
        /// <summary>XISF:Abstract (String).</summary>
        public static XisfPropertyKey<string> Abstract { get; } = new("XISF:Abstract", "String");
        /// <summary>XISF:AccessRights (String).</summary>
        public static XisfPropertyKey<string> AccessRights { get; } = new("XISF:AccessRights", "String");
        /// <summary>XISF:Authors (String).</summary>
        public static XisfPropertyKey<string> Authors { get; } = new("XISF:Authors", "String");
        /// <summary>XISF:BibliographicReferences (String).</summary>
        public static XisfPropertyKey<string> BibliographicReferences { get; } = new("XISF:BibliographicReferences", "String");
        /// <summary>XISF:BlockAlignmentSize (UInt16; units: bytes).</summary>
        public static XisfPropertyKey<ushort> BlockAlignmentSize { get; } = new("XISF:BlockAlignmentSize", "UInt16", unit: "bytes");
        /// <summary>XISF:BriefDescription (String).</summary>
        public static XisfPropertyKey<string> BriefDescription { get; } = new("XISF:BriefDescription", "String");
        /// <summary>XISF:ChecksumAlgorithms (String).</summary>
        public static XisfPropertyKey<string> ChecksumAlgorithms { get; } = new("XISF:ChecksumAlgorithms", "String");
        /// <summary>XISF:CompressionCodecs (String).</summary>
        public static XisfPropertyKey<string> CompressionCodecs { get; } = new("XISF:CompressionCodecs", "String");
        /// <summary>XISF:CompressionLevel (Int32).</summary>
        public static XisfPropertyKey<int> CompressionLevel { get; } = new("XISF:CompressionLevel", "Int32");
        /// <summary>XISF:Contributors (String).</summary>
        public static XisfPropertyKey<string> Contributors { get; } = new("XISF:Contributors", "String");
        /// <summary>XISF:Copyright (String).</summary>
        public static XisfPropertyKey<string> Copyright { get; } = new("XISF:Copyright", "String");
        /// <summary>XISF:CreationTime (TimePoint).</summary>
        public static XisfPropertyKey<DateTimeOffset> CreationTime { get; } = new("XISF:CreationTime", "TimePoint");
        /// <summary>XISF:CreatorApplication (String).</summary>
        public static XisfPropertyKey<string> CreatorApplication { get; } = new("XISF:CreatorApplication", "String");
        /// <summary>XISF:CreatorModule (String).</summary>
        public static XisfPropertyKey<string> CreatorModule { get; } = new("XISF:CreatorModule", "String");
        /// <summary>XISF:CreatorOS (String).</summary>
        public static XisfPropertyKey<string> CreatorOS { get; } = new("XISF:CreatorOS", "String");
        /// <summary>XISF:Description (String).</summary>
        public static XisfPropertyKey<string> Description { get; } = new("XISF:Description", "String");
        /// <summary>XISF:Keywords (String).</summary>
        public static XisfPropertyKey<string> Keywords { get; } = new("XISF:Keywords", "String");
        /// <summary>XISF:Languages (String).</summary>
        public static XisfPropertyKey<string> Languages { get; } = new("XISF:Languages", "String");
        /// <summary>XISF:License (String).</summary>
        public static XisfPropertyKey<string> License { get; } = new("XISF:License", "String");
        /// <summary>XISF:MaxInlineBlockSize (UInt16; units: bytes).</summary>
        public static XisfPropertyKey<ushort> MaxInlineBlockSize { get; } = new("XISF:MaxInlineBlockSize", "UInt16", unit: "bytes");
        /// <summary>XISF:OriginalCreationTime (TimePoint).</summary>
        public static XisfPropertyKey<DateTimeOffset> OriginalCreationTime { get; } = new("XISF:OriginalCreationTime", "TimePoint");
        /// <summary>XISF:OutputHints (String).</summary>
        public static XisfPropertyKey<string> OutputHints { get; } = new("XISF:OutputHints", "String");
        /// <summary>XISF:RelatedResources (String).</summary>
        public static XisfPropertyKey<string> RelatedResources { get; } = new("XISF:RelatedResources", "String");
        /// <summary>XISF:Title (String).</summary>
        public static XisfPropertyKey<string> Title { get; } = new("XISF:Title", "String");
    }
}
