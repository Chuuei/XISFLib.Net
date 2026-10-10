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

/// <summary>Serializes XISF images and documents, including distributed output and XML signatures.</summary>
public static partial class XisfWriter
{
    /// <summary>Writes editable content as a distributed header and indexed block file.</summary>
    /// <param name="headerPath">The header path.</param>
    /// <param name="dataBlockPath">A distinct block path in the same directory.</param>
    /// <param name="document">All editable document content.</param>
    /// <param name="options">Storage settings.</param>
    public static void WriteDistributed(string headerPath, string dataBlockPath, XisfDocumentData document, XisfWriterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(headerPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataBlockPath);
        string header = Path.GetFullPath(headerPath), data = Path.GetFullPath(dataBlockPath);
        if (!string.Equals(Path.GetDirectoryName(header), Path.GetDirectoryName(data), StringComparison.OrdinalIgnoreCase) || string.Equals(header, data, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Distributed paths must differ and share a directory.");
        var built = BuildDocument(document.Images, options, document);
        string fileName = Path.GetFileName(data).Replace('\\', '/');
        for (int i = 0; i < built.Attachments.Count; i++) built.Attachments[i].Element.SetAttributeValue("location", $"path(@header_dir/{fileName}):{i + 1}");
        File.WriteAllBytes(header, SerializeXml(built.Document));
        WriteDataBlockFile(data, built.Attachments);
    }
    /// <summary>Writes all content of an editable document with the requested storage settings.</summary>
    /// <param name="path">The destination path, replaced atomically.</param>
    /// <param name="document">Editable document content.</param>
    /// <param name="options">Serialization settings, or defaults.</param>
    public static void Write(string path, XisfDocumentData document, XisfWriterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        AtomicFile.Write(path, stream => Write(stream, document, options));
    }

    /// <summary>Writes an editable document to a caller-owned stream.</summary>
    /// <param name="stream">The writable stream; it remains open.</param>
    /// <param name="document">Editable document content.</param>
    /// <param name="options">Serialization settings, or defaults.</param>
    public static void Write(Stream stream, XisfDocumentData document, XisfWriterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(document);
        if (!stream.CanWrite) throw new ArgumentException("The output stream must be writable.", nameof(stream));
        var built = BuildDocument(document.Images, options, document);
        WriteMonolithic(stream, built.Document, built.Attachments);
    }
    private static readonly Regex PropertyIdentifier = new("^[_a-zA-Z][_a-zA-Z0-9]*(:[_a-zA-Z][_a-zA-Z0-9]*)*$", RegexOptions.CultureInvariant);
    private static readonly XNamespace Namespace = "http://www.pixinsight.com/xisf";

    /// <summary>Creates a signed copy of the current header without changing the parsed source document.</summary>
    /// <param name="document">The document to read, validate, sign, or serialize.</param>
    /// <param name="key">The RSA key used to sign the XML header.</param>
    /// <returns>A parsed document with a detached RSA signature.</returns>
    public static XisfDocument Sign(XisfDocument document, RSA key)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(key);
        foreach (XElement block in document.XmlCore.Descendants().Where(IsDataBlockOwner))
        {
            string location = (string?)block.Attribute("location") ?? "embedded";
            if (location != "embedded" && !location.StartsWith("inline:", StringComparison.Ordinal)) new XisfDataBlock(block, document.Source).VerifyIntegrity();
        }
        var signed = XisfReader.ParseHeader(new XDocument(document.XmlCore), document.Source);
        SignXml(signed.XmlCore, key);
        return signed;
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
        XisfDocument unsigned = new(document.Version, new XDocument(document.XmlCore), document.Metadata, document.Properties, document.Tables, document.Images, document.Source);
        (XDocument copy, List<Attachment> attachments) = PrepareLoadedDocument(unsigned);
        WriteSignedCore(stream, copy, attachments, key);
    }

    /// <summary>Writes a signed parsed document to an atomically replaced file.</summary>
    /// <param name="path">The destination path.</param>
    /// <param name="document">The parsed document.</param>
    /// <param name="key">The RSA private signing key.</param>
    public static void WriteSigned(string path, XisfDocument document, RSA key) => AtomicFile.Write(path, stream => WriteSigned(stream, document, key));

    /// <summary>Writes editable content with a detached signature over the finalized layout.</summary>
    /// <param name="stream">The caller-owned writable stream.</param>
    /// <param name="document">Editable content.</param>
    /// <param name="key">The RSA private signing key.</param>
    /// <param name="options">Storage settings; attached blocks are signed with SHA-256 checksums.</param>
    public static void WriteSigned(Stream stream, XisfDocumentData document, RSA key, XisfWriterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(key);
        if (!stream.CanWrite) throw new ArgumentException("The output stream must be writable.", nameof(stream));
        var built = BuildDocument(document.Images, options, document);
        WriteSignedCore(stream, built.Document, built.Attachments, key);
    }

    /// <summary>Writes signed editable content to an atomically replaced file.</summary>
    /// <param name="path">The destination path.</param>
    /// <param name="document">Editable content.</param>
    /// <param name="key">The RSA private signing key.</param>
    /// <param name="options">Storage settings.</param>
    public static void WriteSigned(string path, XisfDocumentData document, RSA key, XisfWriterOptions? options = null) => AtomicFile.Write(path, stream => WriteSigned(stream, document, key, options));

    private static void WriteSignedCore(Stream stream, XDocument copy, List<Attachment> attachments, RSA key)
    {
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
        XDocument copy = new(document.XmlCore);
        if (document.DetachedSignature is { } signature) copy.AddAnnotation(new XisfDetachedSignature(new XElement(signature)));
        copy.Descendants(XName.Get("Signature", SignedXml.XmlDsigNamespaceUrl)).Remove();
        XElement[] originals = document.XmlCore.Descendants().Where(IsDataBlockOwner).ToArray();
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
        (XDocument document, List<Attachment> attachments) = BuildDocument(images, options, null);
        WriteMonolithic(stream, document, attachments);
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
        (XDocument document, List<Attachment> attachments) = BuildDocument(images, options, null);
        string fileName = Path.GetFileName(fullDataPath).Replace('\\', '/');
        for (int i = 0; i < attachments.Count; i++)
            attachments[i].Element.SetAttributeValue("location", $"path(@header_dir/{fileName}):{i + 1}");
        File.WriteAllBytes(fullHeaderPath, SerializeXml(document));
        WriteDataBlockFile(fullDataPath, attachments);
    }

    private static (XDocument Document, List<Attachment> Attachments) BuildDocument(IEnumerable<XisfImageData> images,
        XisfWriterOptions? options, XisfDocumentData? content)
    {
        ArgumentNullException.ThrowIfNull(images);
        XisfImageData[] imageList = images.ToArray();
        if (imageList.Any(image => image is null)) throw new ArgumentException("The image collection cannot contain null elements.", nameof(images));
        options = options?.Snapshot() ?? new XisfWriterOptions();
        content ??= new XisfDocumentData();
        ApiContracts.Defined(options.Compression, nameof(options.Compression));
        ApiContracts.Defined(options.Checksum, nameof(options.Checksum));
        ApiContracts.Defined(options.DataBlockStorage, nameof(options.DataBlockStorage));
        if (options.CompressionSubblockSize < 0 || options.BlockAlignmentSize < 1 || options.BlockAlignmentSize > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(options));
        List<Attachment> attachments = [];
        XElement root = new(Namespace + "xisf", new XAttribute("version", "1.0"));
        XElement metadata = new(Namespace + "Metadata");
        AddProperty(metadata, "XISF:CreationTime", options.CreationTime, options, attachments);
        AddProperty(metadata, "XISF:CreatorApplication", options.CreatorApplication, options, attachments);
        foreach ((string id, object? value) in content.Metadata)
        {
            if (id is "XISF:CreationTime" or "XISF:CreatorApplication" || id == "XISF:BlockAlignmentSize" && options.BlockAlignmentSize > 1) continue;
            if (!id.StartsWith("XISF:", StringComparison.Ordinal)) throw new ArgumentException("Metadata properties must use the XISF namespace.");
            AddProperty(metadata, id, value, options, attachments);
        }
        if (options.BlockAlignmentSize > 1) AddProperty(metadata, "XISF:BlockAlignmentSize", checked((ushort)options.BlockAlignmentSize), options, attachments);
        root.Add(metadata);
        foreach ((string id, object? value) in content.Properties) AddProperty(root, id, value, options, attachments);
        foreach (XisfTableData table in content.Tables) root.Add(CreateTable(table, options, attachments));
        foreach (XElement extension in content.Extensions)
        {
            if (extension.Name.Namespace == Namespace || extension.Name.Namespace == XNamespace.None) throw new ArgumentException("Extension elements require their own XML namespace.");
            root.Add(new XElement(extension));
        }
        if (imageList.Where(i => !string.IsNullOrEmpty(i.Id)).Select(i => i.Id).Distinct(StringComparer.Ordinal).Count() != imageList.Count(i => !string.IsNullOrEmpty(i.Id)))
            throw new ArgumentException("Image identifiers must be unique within a unit.", nameof(images));
        foreach (XisfImageData image in imageList) root.Add(CreateImage(image, options, attachments, false));
        foreach (var preserved in content.PreservedElements) root.Add(PreserveElement(preserved, attachments));
        foreach (var pair in new[] { (Attribute: "compression", Id: "XISF:CompressionCodecs"), (Attribute: "checksum", Id: "XISF:ChecksumAlgorithms") })
        {
            string[] algorithms = root.Descendants().Attributes(pair.Attribute).Select(a => a.Value.Split(':')[0]).Distinct(StringComparer.Ordinal).ToArray();
            if (algorithms.Length > 0 && !content.Metadata.ContainsKey(pair.Id)) AddProperty(metadata, pair.Id, string.Join(',', algorithms), options, attachments);
        }
        if (options.DataBlockStorage != XisfDataBlockStorage.Attachment && !content.Metadata.ContainsKey("XISF:MaxInlineBlockSize"))
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
        foreach (var preserved in image.PreservedElements) element.Add(PreserveElement(preserved, attachments));
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
                    value = new XisfProperty("cell", table.Fields[index].Type, preserved.Value, null, preserved.XmlCore) { Source = preserved.Source };
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

    private static string Join<T>(IEnumerable<T> values) where T : IFormattable => string.Join(' ', values.Select(value => value.ToString(null, CultureInfo.InvariantCulture)));
    private static string Join(IEnumerable<double> values) => string.Join(':', values.Select(Format));
    private static string Format(double value) => double.IsPositiveInfinity(value) ? "+Inf" : double.IsNegativeInfinity(value) ? "-Inf" : value.ToString("R", CultureInfo.InvariantCulture);

    private sealed record Attachment(XElement Element, byte[] Data, long UncompressedLength);
    private sealed record PropertySerialization(string Type, string? Text = null, byte[]? Binary = null,
        int? Length = null, int? Rows = null, int? Columns = null, int ItemSize = 1);
}
