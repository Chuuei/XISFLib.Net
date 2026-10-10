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
    /// <summary>Tables associated with the unit as a whole.</summary>
    public IList<XisfTableData> Tables { get; } = [];
    /// <summary>Extension elements in other namespaces, serialized as children of the root.</summary>
    public IList<XElement> Extensions { get; } = [];
    /// <summary>The application name written to XISF creation metadata.</summary>
    public string CreatorApplication { get; set; } = "XISFLib.Net " + typeof(XisfWriter).Assembly.GetName().Version;
    /// <summary>The timestamp written to XISF creation metadata.</summary>
    public DateTimeOffset CreationTime { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Document-level metadata indexed by property identifiers.</summary>
    public IDictionary<string, object?> Metadata { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);
    /// <summary>Typed properties indexed by case-sensitive identifiers.</summary>
    public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);
}

/// <summary>Serializes XISF images and documents, including distributed output and XML signatures.</summary>
public static class XisfWriter
{
    private static readonly Regex PropertyIdentifier = new("^[_a-zA-Z][_a-zA-Z0-9]*(:[_a-zA-Z][_a-zA-Z0-9]*)*$", RegexOptions.CultureInvariant);
    private static readonly XNamespace Namespace = "http://www.pixinsight.com/xisf";

    /// <summary>Replaces existing XML signatures with a detached RSA signature on the current header.</summary>
    /// <param name="document">The document to read, validate, sign, or serialize.</param>
    /// <param name="key">The RSA key used to sign the XML header.</param>
    public static void Sign(XisfDocument document, RSA key)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(key);
        foreach (XElement block in document.Xml.Descendants().Where(IsDataBlockOwner))
        {
            string location = (string?)block.Attribute("location") ?? "embedded";
            if (location != "embedded" && !location.StartsWith("inline:", StringComparison.Ordinal)) new XisfDataBlock(block, document.Source).VerifyIntegrity();
        }
        SignXml(document.Xml, key);
    }

    private static void SignXml(XDocument document, RSA key)
    {
        XmlDocument xml = new() { PreserveWhitespace = true };
        using (XmlReader reader = document.CreateReader()) xml.Load(reader);
        foreach (XmlNode signature in xml.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl).Cast<XmlNode>().ToArray())
            signature.ParentNode?.RemoveChild(signature);
        string id = xml.DocumentElement!.GetAttribute("id");
        if (string.IsNullOrEmpty(id)) { id = "XISFRootElement_" + Guid.NewGuid().ToString("N"); xml.DocumentElement.SetAttribute("id", id); }
        foreach (XElement block in document.Descendants().Where(IsDataBlockOwner))
        {
            string location = (string?)block.Attribute("location") ?? "embedded";
            if (!location.StartsWith("inline:", StringComparison.Ordinal) && location != "embedded" && block.Attribute("checksum") is null)
                throw new InvalidOperationException("Signing requires a checksum for every external or attached data block.");
        }
        SignedXml signedXml = new(xml) { SigningKey = key };
        XisfSignatureAlgorithms.Configure(signedXml);
        signedXml.SignedInfo!.CanonicalizationMethod = XisfSignatureAlgorithms.Canonical11;
        Reference reference = new("#" + id);
        reference.AddTransform(new XisfCanonicalXml11Transform());
        signedXml.AddReference(reference);
        KeyInfo keyInfo = new();
        keyInfo.AddClause(new RSAKeyValue(key));
        signedXml.KeyInfo = keyInfo;
        signedXml.ComputeSignature();
        document.Root!.SetAttributeValue("id", id);
        document.Descendants(XName.Get("Signature", SignedXml.XmlDsigNamespaceUrl)).Remove();
        document.RemoveAnnotations<XisfDetachedSignature>();
        document.AddAnnotation(new XisfDetachedSignature(XElement.Parse(signedXml.GetXml().OuterXml, LoadOptions.PreserveWhitespace)));
    }

    /// <summary>Serializes a document or images to the supplied file or stream.</summary>
    /// <param name="path">The destination file path.</param>
    /// <param name="images">The images in output order; at least one image is required.</param>
    /// <param name="options">The operation settings, or null to use defaults.</param>
    /// <remarks>File output is staged beside the destination and committed only after successful serialization.</remarks>
    public static void Write(string path, IEnumerable<XisfImageData> images, XisfWriterOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        AtomicFile.Write(path, stream => Write(stream, images, options));
    }

    /// <summary>Serializes a document or images to the supplied file or stream.</summary>
    /// <param name="path">The destination file path.</param>
    /// <param name="document">The document to read, validate, sign, or serialize.</param>
    /// <remarks>File output is staged beside the destination and committed only after successful serialization.</remarks>
    public static void Write(string path, XisfDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        AtomicFile.Write(path, stream => Write(stream, document));
    }

    /// <summary>Serializes a document or images to the supplied file or stream.</summary>
    /// <param name="stream">The caller-owned stream, positioned at the file origin; it remains open.</param>
    /// <param name="document">The document to read, validate, sign, or serialize.</param>
    /// <remarks>The caller owns the stream. An error can leave partial output.</remarks>
    public static void Write(Stream stream, XisfDocument document)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(document);
        if (!stream.CanWrite) throw new ArgumentException("The output stream must be writable.", nameof(stream));
        if (document.HasXmlSignature && !XisfReader.VerifyXmlSignature(document)) throw new InvalidOperationException("The current signature or data integrity is invalid; use WriteSigned with a signing key after correcting the data.");
        (XDocument copy, List<Attachment> attachments) = PrepareLoadedDocument(document);
        WriteMonolithic(stream, copy, attachments);
    }

    /// <summary>Rebuilds attachment locations and writes a fresh detached RSA XML signature.</summary>
    /// <param name="stream">The caller-owned stream, positioned at the file origin; it remains open.</param>
    /// <param name="document">The document to read, validate, sign, or serialize.</param>
    /// <param name="key">The RSA key used to sign the XML header.</param>
    public static void WriteSigned(Stream stream, XisfDocument document, RSA key)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(key);
        if (!stream.CanWrite) throw new ArgumentException("The output stream must be writable.", nameof(stream));
        XisfDocument unsigned = new(document.Version, new XDocument(document.Xml), document.Metadata, document.Properties, document.Tables, document.Images, document.Source);
        (XDocument copy, List<Attachment> attachments) = PrepareLoadedDocument(unsigned);
        foreach (Attachment attachment in attachments) attachment.Element.SetAttributeValue("checksum", Checksum(attachment.Data, XisfChecksum.Sha256));
        copy.RemoveAnnotations<XisfDetachedSignature>();
        SignXml(copy, key);
        XisfDetachedSignature initialSignature = copy.Annotation<XisfDetachedSignature>()!;
        copy.RemoveAnnotations<XisfDetachedSignature>();
        // Reserve the fixed-size signature while stabilizing offsets, then sign the final locations.
        copy.AddAnnotation(new XisfSignatureReservation(initialSignature.Element.ToString(SaveOptions.DisableFormatting)));
        SetAttachmentLocations(copy, attachments);
        copy.RemoveAnnotations<XisfSignatureReservation>();
        SignXml(copy, key);
        WriteMonolithic(stream, copy, attachments);
    }

    private static (XDocument Document, List<Attachment> Attachments) PrepareLoadedDocument(XisfDocument document)
    {
        XDocument copy = new(document.Xml);
        if (document.DetachedSignature is { } signature) copy.AddAnnotation(new XisfDetachedSignature(new XElement(signature)));
        copy.Descendants(XName.Get("Signature", SignedXml.XmlDsigNamespaceUrl)).Remove();
        XElement[] originals = document.Xml.Descendants().Where(IsDataBlockOwner).ToArray();
        XElement[] targets = copy.Descendants().Where(IsDataBlockOwner).ToArray();
        List<Attachment> attachments = [];
        for (int i = 0; i < originals.Length; i++)
        {
            XElement original = originals[i], target = targets[i];
            string location = (string?)original.Attribute("location") ?? "embedded";
            if (location.StartsWith("inline:", StringComparison.OrdinalIgnoreCase) || location.Equals("embedded", StringComparison.OrdinalIgnoreCase)) continue;
            byte[] data = new XisfDataBlock(original, document.Source).ReadSerialized();
            if (target.Attribute("checksum") is null) target.SetAttributeValue("checksum", Checksum(data, XisfChecksum.Sha256));
            if (copy.Annotation<XisfDetachedSignature>() is null)
                target.SetAttributeValue("location", "attachment:00000000000000000000:" + data.Length);
            long uncompressed = target.Attribute("compression") is { } compression ? long.Parse(compression.Value.Split(':')[1], CultureInfo.InvariantCulture) : data.LongLength;
            attachments.Add(new Attachment(target, data, uncompressed));
        }
        if (copy.Annotation<XisfDetachedSignature>() is not null)
        {
            if (attachments.Any(a => !((string?)a.Element.Attribute("location"))!.StartsWith("attachment:", StringComparison.Ordinal)))
                throw new InvalidOperationException("Converting a signed distributed unit requires a new signature. Use WriteSigned.");
            attachments = attachments.GroupBy(a => (string?)a.Element.Attribute("location")).Select(g => g.First())
                .OrderBy(a => long.Parse(((string)a.Element.Attribute("location")!).Split(':')[1], CultureInfo.InvariantCulture)).ToList();
        }
        return (copy, attachments);
    }

    /// <summary>Serializes a document or images to the supplied file or stream.</summary>
    /// <param name="stream">The caller-owned stream, positioned at the file origin; it remains open.</param>
    /// <param name="images">The images in output order; at least one image is required.</param>
    /// <param name="options">The operation settings, or null to use defaults.</param>
    /// <remarks>The caller owns the stream. An error can leave partial output.</remarks>
    public static void Write(Stream stream, IEnumerable<XisfImageData> images, XisfWriterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(images);
        if (!stream.CanWrite) throw new ArgumentException("The output stream must be writable.", nameof(stream));
        (XDocument document, List<Attachment> attachments) = BuildDocument(images, options);
        WriteMonolithic(stream, document, attachments);
    }

    private static void WriteMonolithic(Stream stream, XDocument document, IReadOnlyList<Attachment> attachments)
    {
        SetAttachmentLocations(document, attachments);
        byte[] xml = SerializeXml(document);

        Span<byte> header = stackalloc byte[16];
        "XISF0100"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], checked((uint)xml.Length));
        header[12..].Clear();
        stream.Write(header);
        stream.Write(xml);
        long position = 16L + xml.Length;
        foreach (Attachment attachment in attachments)
        {
            long target = long.Parse(((string)attachment.Element.Attribute("location")!).Split(':')[1], CultureInfo.InvariantCulture);
            byte[] padding = new byte[8192];
            while (position < target) { int count = (int)Math.Min(padding.Length, target - position); stream.Write(padding.AsSpan(0, count)); position += count; }
            stream.Write(attachment.Data); position += attachment.Data.LongLength;
        }
    }

    private static void SetAttachmentLocations(XDocument document, IReadOnlyList<Attachment> attachments)
    {
        if (document.Annotation<XisfDetachedSignature>() is not null)
        {
            long previousEnd = 16L + SerializeXml(document).Length;
            foreach (Attachment attachment in attachments)
            {
                long position = long.Parse(((string)attachment.Element.Attribute("location")!).Split(':')[1], CultureInfo.InvariantCulture);
                if (position < previousEnd) throw new InvalidOperationException("The signed header layout cannot be preserved. Use WriteSigned to regenerate the signature.");
                previousEnd = checked(position + attachment.Data.LongLength);
            }
            return;
        }
        int alignment = 1;
        XElement? alignmentProperty = document.Root!.Elements(Namespace + "Metadata").Elements(Namespace + "Property").FirstOrDefault(p => (string?)p.Attribute("id") == "XISF:BlockAlignmentSize");
        if (alignmentProperty is not null && int.TryParse((string?)alignmentProperty.Attribute("value"), out int declaredAlignment) && declaredAlignment > 0) alignment = declaredAlignment;
        int xmlLength = SerializeXml(document).Length;
        for (int iteration = 0; iteration < 32; iteration++)
        {
            long position = 16L + xmlLength;
            foreach (Attachment attachment in attachments)
            {
                position = checked(position + (alignment - position % alignment) % alignment);
                attachment.Element.SetAttributeValue("location",
                    $"attachment:{position.ToString(CultureInfo.InvariantCulture)}:{attachment.Data.Length.ToString(CultureInfo.InvariantCulture)}");
                position += attachment.Data.Length;
            }

            int updatedXmlLength = SerializeXml(document).Length;
            if (updatedXmlLength == xmlLength)
                return;
            xmlLength = updatedXmlLength;
        }

        throw new InvalidOperationException("Unable to stabilize XISF attachment locations.");
    }

    private static bool IsDataBlockOwner(XElement element)
    {
        string? location = (string?)element.Attribute("location");
        return location is not null || element.Elements().Any(e => e.Name.LocalName == "Data");
    }

    /// <summary>Writes an XML header and indexed data-block file in the same directory.</summary>
    /// <param name="headerPath">The destination path for the distributed XML header.</param>
    /// <param name="dataBlockPath">The destination path for indexed data blocks, in the same directory as the header.</param>
    /// <param name="images">The images in output order; at least one image is required.</param>
    /// <param name="options">The operation settings, or null to use defaults.</param>
    public static void WriteDistributed(string headerPath, string dataBlockPath, IEnumerable<XisfImageData> images, XisfWriterOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataBlockPath);
        string fullHeaderPath = Path.GetFullPath(headerPath);
        string fullDataPath = Path.GetFullPath(dataBlockPath);
        if (!string.Equals(Path.GetDirectoryName(fullHeaderPath), Path.GetDirectoryName(fullDataPath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The header and data-block files must be in the same directory.", nameof(dataBlockPath));
        if (string.Equals(fullHeaderPath, fullDataPath, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("The header and data block paths must differ.");
        (XDocument document, List<Attachment> attachments) = BuildDocument(images, options);
        string fileName = Path.GetFileName(fullDataPath).Replace('\\', '/');
        for (int i = 0; i < attachments.Count; i++)
            attachments[i].Element.SetAttributeValue("location", $"path(@header_dir/{fileName}):{i + 1}");
        File.WriteAllBytes(fullHeaderPath, SerializeXml(document));
        WriteDataBlockFile(fullDataPath, attachments);
    }

    private static (XDocument Document, List<Attachment> Attachments) BuildDocument(IEnumerable<XisfImageData> images, XisfWriterOptions? options)
    {
        ArgumentNullException.ThrowIfNull(images);
        XisfImageData[] imageList = images.ToArray();
        if (imageList.Any(image => image is null)) throw new ArgumentException("The image collection cannot contain null elements.", nameof(images));
        options ??= new XisfWriterOptions();
        if (options.CompressionSubblockSize < 0 || options.BlockAlignmentSize < 1 || options.BlockAlignmentSize > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(options));
        List<Attachment> attachments = [];
        XElement root = new(Namespace + "xisf", new XAttribute("version", "1.0"));
        XElement metadata = new(Namespace + "Metadata");
        AddProperty(metadata, "XISF:CreationTime", options.CreationTime, options, attachments);
        AddProperty(metadata, "XISF:CreatorApplication", options.CreatorApplication, options, attachments);
        foreach ((string id, object? value) in options.Metadata)
        {
            if (id is "XISF:CreationTime" or "XISF:CreatorApplication" || id == "XISF:BlockAlignmentSize" && options.BlockAlignmentSize > 1) continue;
            if (!id.StartsWith("XISF:", StringComparison.Ordinal)) throw new ArgumentException("Metadata properties must use the XISF namespace.");
            AddProperty(metadata, id, value, options, attachments);
        }
        if (options.BlockAlignmentSize > 1) AddProperty(metadata, "XISF:BlockAlignmentSize", checked((ushort)options.BlockAlignmentSize), options, attachments);
        root.Add(metadata);
        foreach ((string id, object? value) in options.Properties) AddProperty(root, id, value, options, attachments);
        foreach (XisfTableData table in options.Tables) root.Add(CreateTable(table, options, attachments));
        foreach (XElement extension in options.Extensions)
        {
            if (extension.Name.Namespace == Namespace || extension.Name.Namespace == XNamespace.None) throw new ArgumentException("Extension elements require their own XML namespace.");
            root.Add(new XElement(extension));
        }
        if (imageList.Where(i => !string.IsNullOrEmpty(i.Id)).Select(i => i.Id).Distinct(StringComparer.Ordinal).Count() != imageList.Count(i => !string.IsNullOrEmpty(i.Id)))
            throw new ArgumentException("Image identifiers must be unique within a unit.", nameof(images));
        foreach (XisfImageData image in imageList) root.Add(CreateImage(image, options, attachments, false));
        foreach (var pair in new[] { (Attribute: "compression", Id: "XISF:CompressionCodecs"), (Attribute: "checksum", Id: "XISF:ChecksumAlgorithms") })
        {
            string[] algorithms = root.Descendants().Attributes(pair.Attribute).Select(a => a.Value.Split(':')[0]).Distinct(StringComparer.Ordinal).ToArray();
            if (algorithms.Length > 0 && !options.Metadata.ContainsKey(pair.Id)) AddProperty(metadata, pair.Id, string.Join(',', algorithms), options, attachments);
        }
        if (options.DataBlockStorage != XisfDataBlockStorage.Attachment && !options.Metadata.ContainsKey("XISF:MaxInlineBlockSize"))
            AddProperty(metadata, "XISF:MaxInlineBlockSize", options.MaximumInlineBlockSize, options, attachments);
        return (new XDocument(new XDeclaration("1.0", "UTF-8", null), root), attachments);
    }

    private static void WriteDataBlockFile(string path, IReadOnlyList<Attachment> attachments)
    {
        long dataPosition = checked(32L + attachments.Count * 40L);
        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Span<byte> header = stackalloc byte[32];
        header.Clear();
        "XISB0100"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], checked((uint)attachments.Count));
        stream.Write(header);
        long position = dataPosition;
        byte[] entryBuffer = new byte[40];
        for (int i = 0; i < attachments.Count; i++)
        {
            Span<byte> entry = entryBuffer;
            entry.Clear();
            BinaryPrimitives.WriteUInt64LittleEndian(entry, checked((ulong)i + 1));
            BinaryPrimitives.WriteUInt64LittleEndian(entry[8..], checked((ulong)position));
            BinaryPrimitives.WriteUInt64LittleEndian(entry[16..], checked((ulong)attachments[i].Data.LongLength));
            BinaryPrimitives.WriteUInt64LittleEndian(entry[24..], attachments[i].Element.Attribute("compression") is null ? 0 : checked((ulong)attachments[i].UncompressedLength));
            stream.Write(entry);
            position += attachments[i].Data.LongLength;
        }
        foreach (Attachment attachment in attachments) stream.Write(attachment.Data);
    }

    private static byte[] SerializeXml(XDocument document)
    {
        using MemoryStream stream = new();
        using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            OmitXmlDeclaration = false
        }))
        {
            document.Save(writer);
            if (document.Annotation<XisfSignatureReservation>() is { } reservation)
            {
                writer.Flush(); stream.Write(Encoding.UTF8.GetBytes(reservation.Xml));
            }
            if (document.Annotation<XisfDetachedSignature>() is { } signature)
            {
                writer.Flush();
                byte[] detached = Encoding.UTF8.GetBytes(signature.Element.ToString(SaveOptions.DisableFormatting));
                stream.Write(detached);
            }
        }
        return stream.ToArray();
    }

    private static XElement CreateImage(XisfImageData image, XisfWriterOptions options, List<Attachment> attachments, bool thumbnail)
    {
        string colorSpace = image.ColorSpace ?? "Gray";
        if (colorSpace is not ("Gray" or "RGB" or "CIELab") || colorSpace != "Gray" && image.ChannelCount < 3) throw new ArgumentException("Invalid image color space or channel count.", nameof(image));
        if (!string.IsNullOrEmpty(image.Id) && (!PropertyIdentifier.IsMatch(image.Id) || image.Id.Contains(':'))) throw new ArgumentException("Invalid image identifier.", nameof(image));
        if (image.Orientation is { Length: > 0 } orientation && orientation is not ("0" or "flip" or "90" or "90;flip" or "-90" or "-90;flip" or "180" or "180;flip")) throw new ArgumentException("Invalid image orientation.", nameof(image));
        if (image.Uuid is { } identifier && (identifier.ToString("D")[14] != '4' || !"89ab".Contains(identifier.ToString("D")[19]))) throw new ArgumentException("Image UUIDs must be RFC 9562 version 4.", nameof(image));
        if (thumbnail && (image.Dimensions.Count != 2 || image.SampleFormat is not (XisfSampleFormat.UInt8 or XisfSampleFormat.UInt16) || colorSpace == "CIELab" || image.ChannelCount > (colorSpace == "Gray" ? 2 : 4) || image.ColorFilterArray is not null || image.Thumbnail is not null))
            throw new ArgumentException("The thumbnail does not satisfy XISF restrictions.", nameof(image));
        if (image.Offset < 0 || !double.IsFinite(image.Offset)) throw new ArgumentException("The image offset must be a finite nonnegative value.", nameof(image));
        XElement element = new(Namespace + (thumbnail ? "Thumbnail" : "Image"),
            new XAttribute("geometry", string.Join(':', image.Dimensions.Append(image.ChannelCount))),
            new XAttribute("sampleFormat", image.SampleFormat),
            new XAttribute("pixelStorage", image.PixelStorage == XisfPixelLayout.Interleaved ? "Normal" : "Planar"));
        if (!string.IsNullOrWhiteSpace(image.Id)) element.SetAttributeValue("id", image.Id);
        if (!string.IsNullOrWhiteSpace(image.ColorSpace)) element.SetAttributeValue("colorSpace", image.ColorSpace);
        if (!string.IsNullOrWhiteSpace(image.ImageType)) element.SetAttributeValue("imageType", CanonicalImageType(image.ImageType));
        if (image.Offset != 0) element.SetAttributeValue("offset", Format(image.Offset));
        if (!string.IsNullOrWhiteSpace(image.Orientation)) element.SetAttributeValue("orientation", image.Orientation);
        if (image.Uuid is { } uuid) element.SetAttributeValue("uuid", uuid.ToString("D"));
        if (image.IsBigEndian) element.SetAttributeValue("byteOrder", "big");
        if (!thumbnail && (image.SampleFormat is XisfSampleFormat.Float32 or XisfSampleFormat.Float64 || image.LowerBound.HasValue || image.UpperBound.HasValue))
        {
            double lower = image.LowerBound ?? throw new ArgumentException("Floating-point images require a lower bound.", nameof(image));
            double upper = image.UpperBound ?? throw new ArgumentException("Floating-point images require an upper bound.", nameof(image));
            if (!double.IsFinite(lower) || !double.IsFinite(upper) || !(lower < upper)) throw new ArgumentException("The lower image bound must be less than the upper bound.", nameof(image));
            element.SetAttributeValue("bounds", $"{Format(lower)}:{Format(upper)}");
        }

        AddAttachment(element, image.Data, XisfImageData.BytesPerSample(image.SampleFormat), options, attachments);
        foreach ((string id, object? value) in image.Properties) AddProperty(element, id, value, options, attachments);
        foreach (XisfFitsKeyword keyword in image.FitsKeywords)
        {
            XElement keywordElement = new(Namespace + "FITSKeyword", new XAttribute("name", keyword.Name), new XAttribute("value", keyword.Name is "HISTORY" or "COMMENT" ? "" : keyword.Value),
                new XAttribute("comment", keyword.Name is "HISTORY" or "COMMENT" ? keyword.Comment ?? keyword.Value : keyword.Comment ?? ""));
            element.Add(keywordElement);
        }
        foreach (XisfTableData table in image.Tables)
            element.Add(CreateTable(table, options, attachments));
        if (image.IccProfile is not null)
        {
            XElement profile = new(Namespace + "ICCProfile");
            AddAttachment(profile, PrepareIccProfile(image.IccProfile), 1, options, attachments);
            element.Add(profile);
        }
        if (image.Thumbnail is not null) element.Add(CreateImage(image.Thumbnail, options, attachments, true));
        AddImageMetadata(element, image);
        return element;
    }

    private static string CanonicalImageType(string imageType) => imageType switch
    {
        "BiasFrame" => "Bias",
        "DarkFrame" => "Dark",
        "FlatField" => "Flat",
        "LightFrame" or "ScienceFrame" => "Light",
        "Bias" or "Dark" or "Flat" or "Light" or "MasterBias" or "MasterDark" or "MasterFlat" or "MasterLight" or
        "DefectMap" or "RejectionMapHigh" or "RejectionMapLow" or "BinaryRejectionMapHigh" or "BinaryRejectionMapLow" or
        "SlopeMap" or "WeightMap" => imageType,
        _ => throw new ArgumentException($"'{imageType}' is not a valid XISF image type.", nameof(imageType))
    };

    private static byte[] PrepareIccProfile(byte[] profile)
    {
        if (profile.Length < 128 || !profile.AsSpan(36, 4).SequenceEqual("acsp"u8))
            throw new ArgumentException("The ICC profile has an invalid header.", nameof(profile));
        byte[] embedded = profile.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(embedded, checked((uint)embedded.Length));
        uint flags = BinaryPrimitives.ReadUInt32BigEndian(embedded.AsSpan(44, 4));
        BinaryPrimitives.WriteUInt32BigEndian(embedded.AsSpan(44, 4), flags | 1);
        return embedded;
    }

    private static XElement CreateTable(XisfTableData table, XisfWriterOptions options, List<Attachment> attachments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table.Id);
        if (!PropertyIdentifier.IsMatch(table.Id) || table.Fields.Any(f => !PropertyIdentifier.IsMatch(f.Id)) || table.Fields.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count() != table.Fields.Count)
            throw new ArgumentException("Invalid table or field identifier, or duplicate fields.");
        if (table.Fields.Count == 0) throw new ArgumentException($"Table '{table.Id}' must have at least one field.");
        XElement element = new(Namespace + "Table", new XAttribute("id", table.Id));
        if (table.Caption is not null) element.SetAttributeValue("caption", table.Caption);
        if (table.Comment is not null) element.SetAttributeValue("comment", table.Comment);
        element.SetAttributeValue("rows", table.Rows.Count);
        element.SetAttributeValue("columns", table.Fields.Count);
        XElement structure = new(Namespace + "Structure");
        foreach (XisfTableFieldData field in table.Fields)
        {
            XElement child = new(Namespace + "Field", new XAttribute("id", field.Id), new XAttribute("type", field.Type));
            if (field.Header is not null) child.SetAttributeValue("header", field.Header);
            if (field.Format is not null) child.SetAttributeValue("format", field.Format);
            structure.Add(child);
        }
        element.Add(structure);
        foreach (IReadOnlyList<object?> row in table.Rows)
        {
            if (row.Count != table.Fields.Count) throw new ArgumentException($"A row in table '{table.Id}' has an invalid cell count.");
            XElement rowElement = new(Namespace + "Row");
            for (int index = 0; index < row.Count; index++)
            {
                XElement holder = new(Namespace + "Temporary");
                object? value = row[index];
                if (value is XisfTableCell preserved)
                    value = new XisfProperty("cell", table.Fields[index].Type, preserved.Value, null, preserved.Xml) { Source = preserved.Source };
                else value = new XisfPropertyData(table.Fields[index].Type, value);
                AddProperty(holder, "cell", value, options, attachments);
                XElement cell = holder.Elements().Single();
                cell.Remove();
                cell.Name = Namespace + "Cell";
                cell.Attributes("id").Remove();
                cell.Attributes("type").Remove();
                rowElement.Add(cell);
            }
            element.Add(rowElement);
        }
        return element;
    }

    private static void AddImageMetadata(XElement element, XisfImageData image)
    {
        if (image.ColorFilterArray is { } cfa)
        {
            if (cfa.Width <= 0 || cfa.Height <= 0 || cfa.Pattern.Length != (long)cfa.Width * cfa.Height || cfa.Pattern.Any(c => !"0RGBWCMY".Contains(c))) throw new ArgumentException("Invalid color filter array.", nameof(image));
            XElement child = new(Namespace + "ColorFilterArray", new XAttribute("pattern", cfa.Pattern), new XAttribute("width", cfa.Width), new XAttribute("height", cfa.Height));
            if (cfa.Name is not null) child.SetAttributeValue("name", cfa.Name);
            element.Add(child);
        }
        if (image.RgbWorkingSpace is { } rgb)
            element.Add(new XElement(Namespace + "RGBWorkingSpace", new XAttribute("gamma", rgb.IsSrgb ? "sRGB" : Format(rgb.Gamma)), new XAttribute("x", Join(rgb.ChromaticityCoordinates)), new XAttribute("y", Join(rgb.YChromaticityCoordinates)), new XAttribute("Y", Join(XisfColorSpace.DeriveLuminanceCoefficients(rgb))), rgb.Name is null ? null : new XAttribute("name", rgb.Name)));
        if (image.DisplayFunction is { } display)
        {
            if (new[] { display.Midtones, display.Shadows, display.Highlights, display.LowRange, display.HighRange }.Any(v => v.Count != 4 || v.Any(n => !double.IsFinite(n)))) throw new ArgumentException("Display function vectors require four finite components.", nameof(image));
            element.Add(new XElement(Namespace + "DisplayFunction", new XAttribute("m", Join(display.Midtones)), new XAttribute("s", Join(display.Shadows)), new XAttribute("h", Join(display.Highlights)), new XAttribute("l", Join(display.LowRange)), new XAttribute("r", Join(display.HighRange)), display.Name is null ? null : new XAttribute("name", display.Name)));
        }
        if (image.Resolution is { } resolution)
        {
            if (!double.IsFinite(resolution.Horizontal) || !double.IsFinite(resolution.Vertical) || resolution.Horizontal <= 0 || resolution.Vertical <= 0 || resolution.Unit is not ("inch" or "cm")) throw new ArgumentException("Invalid image resolution.", nameof(image));
            element.Add(new XElement(Namespace + "Resolution", new XAttribute("horizontal", Format(resolution.Horizontal)), new XAttribute("vertical", Format(resolution.Vertical)), new XAttribute("unit", resolution.Unit)));
        }
    }

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

    private static void AddProperty(XElement parent, string id, object? value, XisfWriterOptions options, List<Attachment> attachments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!PropertyIdentifier.IsMatch(id)) throw new ArgumentException("Invalid XISF property identifier.", nameof(id));
        if (value is XisfProperty preserved)
        {
            XElement original = preserved.Xml;
            XElement copy = new(original);
            if (original.Name.Namespace == XNamespace.None)
                foreach (XElement core in copy.DescendantsAndSelf().Where(e => e.Name.Namespace == XNamespace.None))
                    core.Name = Namespace + core.Name.LocalName;
            copy.SetAttributeValue("id", id);
            string? location = (string?)copy.Attribute("location");
            if (location is not null && location != "embedded" && !location.StartsWith("inline:", StringComparison.Ordinal))
            {
                if (preserved.Source is null) throw new ArgumentException("A preserved block requires its original data source.");
                byte[] serialized = new XisfDataBlock(original, preserved.Source).ReadSerialized();
                copy.SetAttributeValue("location", "attachment:00000000000000000000:" + serialized.Length);
                long uncompressed = copy.Attribute("compression") is { } compression ? long.Parse(compression.Value.Split(':')[1], CultureInfo.InvariantCulture) : serialized.LongLength;
                attachments.Add(new Attachment(copy, serialized, uncompressed));
            }
            parent.Add(copy);
            return;
        }
        XisfPropertyData? declared = value as XisfPropertyData;
        PropertySerialization property = PropertyValue(declared is null ? value : declared.Value);
        if (declared is not null)
            property = ApplyDeclaredPropertyType(property, declared.Type);
        XElement element = new(Namespace + "Property", new XAttribute("id", id), new XAttribute("type", property.Type));
        if (declared?.Format is not null) element.SetAttributeValue("format", declared.Format);
        if (declared?.Comment is not null) element.SetAttributeValue("comment", declared.Comment);
        if (property.Binary is not null)
        {
            if (property.Rows is { } rows)
            {
                element.SetAttributeValue("rows", rows);
                element.SetAttributeValue("columns", property.Columns);
            }
            else
                element.SetAttributeValue("length", property.Length);
            if (property.Binary.Length == 0)
                element.SetAttributeValue("location", "inline:base64");
            else
                AddAttachment(element, property.Binary, property.ItemSize, options, attachments);
        }
        else if (property.Type == "String")
        {
            string text = property.Text ?? string.Empty;
            if (text.Contains('\0')) throw new ArgumentException("XISF strings cannot contain null code points.");
            byte[] utf8 = new UTF8Encoding(false, true).GetBytes(text);
            bool xmlCompatible = true;
            try { XmlConvert.VerifyXmlChars(text); } catch (XmlException) { xmlCompatible = false; }
            if (utf8.Length > 0 && (options.StringsAsDataBlocks || !xmlCompatible)) AddAttachment(element, utf8, 1, options, attachments);
            else element.Value = text;
        }
        else
            element.SetAttributeValue("value", property.Text ?? string.Empty);
        parent.Add(element);
    }

    private static PropertySerialization ApplyDeclaredPropertyType(PropertySerialization property, string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        string canonical = type.ToLowerInvariant() switch
        {
            "byte" => "UInt8", "short" => "Int16", "ushort" => "UInt16", "int" => "Int32", "uint" => "UInt32",
            "float" => "Float32", "double" => "Float64", "quad" => "Float128", "complex" => "Complex64",
            "bytearray" => "UI8Vector", "ivector" => "I32Vector", "uivector" => "UI32Vector", "vector" => "F64Vector",
            "bytematrix" => "UI8Matrix", "imatrix" => "I32Matrix", "uimatrix" => "UI32Matrix", "matrix" => "F64Matrix",
            _ => type
        };
        if (canonical.StartsWith("C32", StringComparison.OrdinalIgnoreCase) && property.Type.StartsWith("C64", StringComparison.Ordinal) && property.Binary is not null)
        {
            byte[] single = new byte[property.Binary.Length / 2];
            for (int offset = 0; offset < property.Binary.Length; offset += 8)
                BinaryPrimitives.WriteSingleLittleEndian(single.AsSpan(offset / 2), (float)BinaryPrimitives.ReadDoubleLittleEndian(property.Binary.AsSpan(offset)));
            property = property with { Type = property.Type.Replace("C64", "C32", StringComparison.Ordinal), Binary = single, ItemSize = 8 };
        }
        else if (canonical == "Complex32" && property.Type == "Complex64")
        {
            string[] components = property.Text!.Trim('(', ')').Split(',');
            property = property with { Type = "Complex32", Text = $"({Format((float)XisfReader.ParseFloatingPoint(components[0]))},{Format((float)XisfReader.ParseFloatingPoint(components[1]))})" };
        }
        string actualCanonical = property.Type;
        if (!string.Equals(canonical, actualCanonical, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"The value serialized as '{property.Type}' is incompatible with the declared XISF property type '{type}'.");
        return property with { Type = type };
    }

    private static PropertySerialization PropertyValue(object? value) => value switch
    {
        null => new("String", ""),
        string text => new("String", text),
        bool boolean => new("Boolean", boolean.ToString().ToLowerInvariant()),
        sbyte number => new("Int8", number.ToString(CultureInfo.InvariantCulture)),
        short number => new("Int16", number.ToString(CultureInfo.InvariantCulture)),
        int number => new("Int32", number.ToString(CultureInfo.InvariantCulture)),
        long number => new("Int64", number.ToString(CultureInfo.InvariantCulture)),
        byte number => new("UInt8", number.ToString(CultureInfo.InvariantCulture)),
        ushort number => new("UInt16", number.ToString(CultureInfo.InvariantCulture)),
        uint number => new("UInt32", number.ToString(CultureInfo.InvariantCulture)),
        ulong number => new("UInt64", number.ToString(CultureInfo.InvariantCulture)),
        Int128 number => new("Int128", number.ToString(CultureInfo.InvariantCulture)),
        UInt128 number => new("UInt128", number.ToString(CultureInfo.InvariantCulture)),
        float number => new("Float32", Format(number)),
        double number => new("Float64", Format(number)),
        decimal number => new("Float128", number.ToString(CultureInfo.InvariantCulture)),
        XisfFloat128 number => new("Float128", number.ToString()),
        XisfComplex128 number => new("Complex128", number.ToString()),
        XisfComplex32 number => new("Complex32", $"({Format(number.Real)},{Format(number.Imaginary)})"),
        DateTimeOffset time => new("TimePoint", time.ToString("O", CultureInfo.InvariantCulture)),
        DateTime time => new("TimePoint", time.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
        Complex complex => new("Complex64", $"({Format(complex.Real)},{Format(complex.Imaginary)})"),
        Array array when array.Rank is 1 or 2 => SerializeArray(array),
        _ => throw new NotSupportedException($"The property type '{value.GetType().Name}' is not supported.")
    };

    private static PropertySerialization SerializeArray(Array values)
    {
        Type elementType = values.GetType().GetElementType()!;
        (string prefix, int itemSize) = elementType == typeof(sbyte) ? ("I8", 1) :
            elementType == typeof(byte) ? ("UI8", 1) :
            elementType == typeof(short) ? ("I16", 2) :
            elementType == typeof(ushort) ? ("UI16", 2) :
            elementType == typeof(int) ? ("I32", 4) :
            elementType == typeof(uint) ? ("UI32", 4) :
            elementType == typeof(long) ? ("I64", 8) :
            elementType == typeof(ulong) ? ("UI64", 8) :
            elementType == typeof(Int128) ? ("I128", 16) :
            elementType == typeof(UInt128) ? ("UI128", 16) :
            elementType == typeof(float) ? ("F32", 4) :
            elementType == typeof(double) ? ("F64", 8) :
            elementType == typeof(Complex) ? ("C64", 16) :
            elementType == typeof(XisfFloat128) ? ("F128", 16) :
            elementType == typeof(XisfComplex128) ? ("C128", 32) :
            elementType == typeof(XisfComplex32) ? ("C32", 8) :
            throw new NotSupportedException($"Arrays of '{elementType.Name}' cannot be serialized as XISF properties.");
        int componentSize = prefix.StartsWith('C') ? itemSize / 2 : itemSize;
        byte[] data = XisfSampleBuffer.CopyArray(values, checked(values.Length * itemSize), componentSize);
        return values.Rank == 1
            ? new PropertySerialization(prefix + "Vector", Binary: data, Length: values.Length, ItemSize: itemSize)
            : new PropertySerialization(prefix + "Matrix", Binary: data, Rows: values.GetLength(0), Columns: values.GetLength(1), ItemSize: itemSize);
    }

    private static string Join<T>(IEnumerable<T> values) where T : IFormattable => string.Join(' ', values.Select(value => value.ToString(null, CultureInfo.InvariantCulture)));
    private static string Join(IEnumerable<double> values) => string.Join(':', values.Select(Format));
    private static string Format(double value) => double.IsPositiveInfinity(value) ? "+Inf" : double.IsNegativeInfinity(value) ? "-Inf" : value.ToString("R", CultureInfo.InvariantCulture);

    private sealed record Attachment(XElement Element, byte[] Data, long UncompressedLength);
    private sealed record PropertySerialization(string Type, string? Text = null, byte[]? Binary = null,
        int? Length = null, int? Rows = null, int? Columns = null, int ItemSize = 1);
}
