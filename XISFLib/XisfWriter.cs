using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
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
    Lz4Hc
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
    /// <summary>The application name written to XISF creation metadata.</summary>
    public string CreatorApplication { get; set; } = "XISFLib.Net";
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
    private static readonly XNamespace Namespace = "http://www.pixinsight.com/xisf";

    /// <summary>Replaces existing XML signatures with an enveloped RSA signature on the current header.</summary>
    /// <param name="document">The document to read, validate, sign, or serialize.</param>
    /// <param name="key">The RSA key used to sign the XML header.</param>
    public static void Sign(XisfDocument document, RSA key)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(key);
        SignXml(document.Xml, key);
    }

    private static void SignXml(XDocument document, RSA key)
    {
        XmlDocument xml = new() { PreserveWhitespace = true };
        using (XmlReader reader = document.CreateReader()) xml.Load(reader);
        foreach (XmlNode signature in xml.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl).Cast<XmlNode>().ToArray())
            signature.ParentNode?.RemoveChild(signature);
        SignedXml signedXml = new(xml) { SigningKey = key };
        Reference reference = new("");
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigC14NTransform());
        signedXml.AddReference(reference);
        KeyInfo keyInfo = new();
        keyInfo.AddClause(new RSAKeyValue(key));
        signedXml.KeyInfo = keyInfo;
        signedXml.ComputeSignature();
        xml.DocumentElement!.AppendChild(xml.ImportNode(signedXml.GetXml(), true));
        document.Root!.ReplaceWith(XElement.Parse(xml.DocumentElement.OuterXml, LoadOptions.PreserveWhitespace));
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
        (XDocument copy, List<Attachment> attachments) = PrepareLoadedDocument(document);
        WriteMonolithic(stream, copy, attachments);
    }

    /// <summary>Rebuilds attachment locations and writes a fresh enveloped RSA XML signature.</summary>
    /// <param name="stream">The caller-owned stream, positioned at the file origin; it remains open.</param>
    /// <param name="document">The document to read, validate, sign, or serialize.</param>
    /// <param name="key">The RSA key used to sign the XML header.</param>
    public static void WriteSigned(Stream stream, XisfDocument document, RSA key)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(key);
        if (!stream.CanWrite) throw new ArgumentException("The output stream must be writable.", nameof(stream));
        (XDocument copy, List<Attachment> attachments) = PrepareLoadedDocument(document);
        SignXml(copy, key);
        SetAttachmentLocations(copy, attachments);
        SignXml(copy, key);
        WriteMonolithic(stream, copy, attachments);
    }

    private static (XDocument Document, List<Attachment> Attachments) PrepareLoadedDocument(XisfDocument document)
    {
        XDocument copy = new(document.Xml);
        XElement[] originals = document.Xml.Descendants().Where(IsDataBlockOwner).ToArray();
        XElement[] targets = copy.Descendants().Where(IsDataBlockOwner).ToArray();
        List<Attachment> attachments = [];
        for (int i = 0; i < originals.Length; i++)
        {
            byte[] data = new XisfDataBlock(originals[i], document.Source).Read();
            XElement target = targets[i];
            target.Elements().Where(e => e.Name.LocalName == "Data").Remove();
            target.Attributes("compression").Remove();
            target.Attributes("checksum").Remove();
            target.Attributes("subblocks").Remove();
            target.SetAttributeValue("location", "attachment:00000000000000000000:" + data.Length);
            attachments.Add(new Attachment(target, data, data.LongLength));
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
        stream.Write(header);
        stream.Write(xml);
        foreach (Attachment attachment in attachments) stream.Write(attachment.Data);
    }

    private static void SetAttachmentLocations(XDocument document, IReadOnlyList<Attachment> attachments)
    {
        int xmlLength = SerializeXml(document).Length;
        for (int iteration = 0; iteration < 32; iteration++)
        {
            long position = 16L + xmlLength;
            foreach (Attachment attachment in attachments)
            {
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
        if (imageList.Length == 0) throw new ArgumentException("At least one image is required.", nameof(images));
        if (imageList.Any(image => image is null)) throw new ArgumentException("The image collection cannot contain null elements.", nameof(images));
        options ??= new XisfWriterOptions();
        List<Attachment> attachments = [];
        XElement root = new(Namespace + "xisf", new XAttribute("version", "1.0"));
        XElement metadata = new(Namespace + "Metadata");
        AddProperty(metadata, "XISF:CreationTime", options.CreationTime, options, attachments);
        AddProperty(metadata, "XISF:CreatorApplication", options.CreatorApplication, options, attachments);
        foreach ((string id, object? value) in options.Metadata)
        {
            if (id is "XISF:CreationTime" or "XISF:CreatorApplication") continue;
            AddProperty(metadata, id, value, options, attachments);
        }
        root.Add(metadata);
        foreach ((string id, object? value) in options.Properties) AddProperty(root, id, value, options, attachments);
        foreach (XisfImageData image in imageList) root.Add(CreateImage(image, options, attachments, false));
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
            BinaryPrimitives.WriteUInt64LittleEndian(entry[24..], checked((ulong)attachments[i].UncompressedLength));
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
            document.Save(writer);
        return stream.ToArray();
    }

    private static XElement CreateImage(XisfImageData image, XisfWriterOptions options, List<Attachment> attachments, bool thumbnail)
    {
        if (image.Offset < 0 || !double.IsFinite(image.Offset)) throw new ArgumentException("The image offset must be a finite nonnegative value.", nameof(image));
        XElement element = new(Namespace + (thumbnail ? "Thumbnail" : "Image"),
            new XAttribute("geometry", string.Join(':', image.Dimensions.Append(image.ChannelCount))),
            new XAttribute("sampleFormat", image.SampleFormat),
            new XAttribute("pixelStorage", image.PixelStorage == XisfPixelLayout.Interleaved ? "Normal" : "Planar"));
        if (!string.IsNullOrWhiteSpace(image.Id) && !thumbnail) element.SetAttributeValue("id", image.Id);
        if (!string.IsNullOrWhiteSpace(image.ColorSpace)) element.SetAttributeValue("colorSpace", image.ColorSpace);
        if (!string.IsNullOrWhiteSpace(image.ImageType) && !thumbnail) element.SetAttributeValue("imageType", CanonicalImageType(image.ImageType));
        if (image.Offset != 0) element.SetAttributeValue("offset", Format(image.Offset));
        if (!string.IsNullOrWhiteSpace(image.Orientation)) element.SetAttributeValue("orientation", image.Orientation);
        if (image.Uuid is { } uuid && !thumbnail) element.SetAttributeValue("uuid", uuid.ToString("D"));
        if (image.IsBigEndian) element.SetAttributeValue("byteOrder", "big");
        if (image.SampleFormat is XisfSampleFormat.Float32 or XisfSampleFormat.Float64)
        {
            double lower = image.LowerBound ?? throw new ArgumentException("Floating-point images require a lower bound.", nameof(image));
            double upper = image.UpperBound ?? throw new ArgumentException("Floating-point images require an upper bound.", nameof(image));
            if (!(lower < upper)) throw new ArgumentException("The lower image bound must be less than the upper bound.", nameof(image));
            element.SetAttributeValue("bounds", $"{Format(lower)}:{Format(upper)}");
        }

        AddAttachment(element, image.Data, XisfImageData.BytesPerSample(image.SampleFormat), options, attachments);
        foreach ((string id, object? value) in image.Properties) AddProperty(element, id, value, options, attachments);
        foreach (XisfFitsKeyword keyword in image.FitsKeywords)
        {
            XElement keywordElement = new(Namespace + "FITSKeyword", new XAttribute("name", keyword.Name), new XAttribute("value", keyword.Value));
            if (keyword.Comment is not null) keywordElement.SetAttributeValue("comment", keyword.Comment);
            element.Add(keywordElement);
        }
        foreach (XisfTableData table in image.Tables)
            element.Add(CreateTable(table));
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

    private static XElement CreateTable(XisfTableData table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table.Id);
        if (table.Fields.Count == 0) throw new ArgumentException($"Table '{table.Id}' must have at least one field.");
        XElement element = new(Namespace + "Table", new XAttribute("id", table.Id));
        if (table.Caption is not null) element.SetAttributeValue("caption", table.Caption);
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
            foreach (object? value in row)
            {
                PropertySerialization serialized = PropertyValue(value);
                if (serialized.Binary is not null) throw new NotSupportedException("Table cells cannot contain vector or matrix values.");
                XElement cell = new(Namespace + "Cell");
                if (serialized.Type == "String") cell.Value = serialized.Text ?? string.Empty;
                else cell.SetAttributeValue("value", serialized.Text ?? string.Empty);
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
            XElement child = new(Namespace + "ColorFilterArray", new XAttribute("pattern", cfa.Pattern), new XAttribute("width", cfa.Width), new XAttribute("height", cfa.Height));
            if (cfa.Name is not null) child.SetAttributeValue("name", cfa.Name);
            element.Add(child);
        }
        if (image.RgbWorkingSpace is { } rgb)
            element.Add(new XElement(Namespace + "RGBWorkingSpace", new XAttribute("gamma", Format(rgb.Gamma)), new XAttribute("sRGB", rgb.IsSrgb.ToString().ToLowerInvariant()), new XAttribute("x", Join(rgb.ChromaticityCoordinates)), new XAttribute("Y", Join(rgb.LuminanceCoefficients))));
        if (image.DisplayFunction is { } display)
            element.Add(new XElement(Namespace + "DisplayFunction", new XAttribute("m", Join(display.Midtones)), new XAttribute("s", Join(display.Shadows)), new XAttribute("h", Join(display.Highlights)), new XAttribute("l", Join(display.LowRange)), new XAttribute("r", Join(display.HighRange))));
        if (image.Resolution is { } resolution)
            element.Add(new XElement(Namespace + "Resolution", new XAttribute("horizontal", Format(resolution.Horizontal)), new XAttribute("vertical", Format(resolution.Vertical)), new XAttribute("unit", resolution.Unit)));
    }

    private static void AddAttachment(XElement element, byte[] raw, int itemSize, XisfWriterOptions options, List<Attachment> attachments)
    {
        byte[] serialized = options.Shuffle && options.Compression != XisfCompression.None && itemSize > 1 ? Shuffle(raw, itemSize) : raw;
        serialized = Compress(serialized, options.Compression);
        if (options.Compression != XisfCompression.None)
        {
            string codec = options.Compression switch
            {
                XisfCompression.Zlib => "zlib",
                XisfCompression.Lz4 => "lz4",
                XisfCompression.Lz4Hc => "lz4hc",
                _ => throw new ArgumentOutOfRangeException()
            };
            if (options.Shuffle && itemSize > 1) codec += "+sh";
            element.SetAttributeValue("compression", $"{codec}:{raw.Length}" + (codec.EndsWith("+sh", StringComparison.Ordinal) ? $":{itemSize}" : ""));
        }
        string? checksum = Checksum(serialized, options.Checksum);
        if (checksum is not null) element.SetAttributeValue("checksum", checksum);
        element.SetAttributeValue("location", "attachment:00000000000000000000:" + serialized.Length);
        attachments.Add(new Attachment(element, serialized, raw.LongLength));
    }

    private static byte[] Compress(byte[] data, XisfCompression compression)
    {
        if (compression == XisfCompression.None) return data;
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
            XisfChecksum.Sha3_256 or XisfChecksum.Sha3_512 => throw new PlatformNotSupportedException("SHA-3 is not supported on this platform."),
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
        XisfPropertyData? declared = value as XisfPropertyData;
        PropertySerialization property = PropertyValue(declared?.Value ?? value);
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
            AddAttachment(element, property.Binary, property.ItemSize, options, attachments);
        }
        else if (property.Type == "String")
            element.Value = property.Text ?? string.Empty;
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
        float number => new("Float32", number.ToString("R", CultureInfo.InvariantCulture)),
        double number => new("Float64", number.ToString("R", CultureInfo.InvariantCulture)),
        decimal number => new("Float128", number.ToString(CultureInfo.InvariantCulture)),
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
            throw new NotSupportedException($"Arrays of '{elementType.Name}' cannot be serialized as XISF properties.");
        byte[] data = new byte[checked(values.Length * itemSize)];
        int offset = 0;
        foreach (object value in values)
        {
            WritePropertyItem(data.AsSpan(offset, itemSize), value, elementType);
            offset += itemSize;
        }
        return values.Rank == 1
            ? new PropertySerialization(prefix + "Vector", Binary: data, Length: values.Length, ItemSize: itemSize)
            : new PropertySerialization(prefix + "Matrix", Binary: data, Rows: values.GetLength(0), Columns: values.GetLength(1), ItemSize: itemSize);
    }

    private static void WritePropertyItem(Span<byte> destination, object value, Type type)
    {
        if (type == typeof(byte)) destination[0] = (byte)value;
        else if (type == typeof(sbyte)) destination[0] = unchecked((byte)(sbyte)value);
        else if (type == typeof(short)) BinaryPrimitives.WriteInt16LittleEndian(destination, (short)value);
        else if (type == typeof(ushort)) BinaryPrimitives.WriteUInt16LittleEndian(destination, (ushort)value);
        else if (type == typeof(int)) BinaryPrimitives.WriteInt32LittleEndian(destination, (int)value);
        else if (type == typeof(uint)) BinaryPrimitives.WriteUInt32LittleEndian(destination, (uint)value);
        else if (type == typeof(long)) BinaryPrimitives.WriteInt64LittleEndian(destination, (long)value);
        else if (type == typeof(ulong)) BinaryPrimitives.WriteUInt64LittleEndian(destination, (ulong)value);
        else if (type == typeof(Int128)) BinaryPrimitives.WriteInt128LittleEndian(destination, (Int128)value);
        else if (type == typeof(UInt128)) BinaryPrimitives.WriteUInt128LittleEndian(destination, (UInt128)value);
        else if (type == typeof(float)) BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits((float)value));
        else if (type == typeof(double)) BinaryPrimitives.WriteInt64LittleEndian(destination, BitConverter.DoubleToInt64Bits((double)value));
        else if (type == typeof(Complex))
        {
            Complex complex = (Complex)value;
            BinaryPrimitives.WriteInt64LittleEndian(destination, BitConverter.DoubleToInt64Bits(complex.Real));
            BinaryPrimitives.WriteInt64LittleEndian(destination[8..], BitConverter.DoubleToInt64Bits(complex.Imaginary));
        }
    }

    private static string Join<T>(IEnumerable<T> values) where T : IFormattable => string.Join(' ', values.Select(value => value.ToString(null, CultureInfo.InvariantCulture)));
    private static string Join(IEnumerable<double> values) => string.Join(':', values.Select(Format));
    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private sealed record Attachment(XElement Element, byte[] Data, long UncompressedLength);
    private sealed record PropertySerialization(string Type, string? Text = null, byte[]? Binary = null,
        int? Length = null, int? Rows = null, int? Columns = null, int ItemSize = 1);
}
