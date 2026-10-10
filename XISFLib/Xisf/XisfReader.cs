using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography.Xml;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace XISFLib;

/// <summary>Reads monolithic XISF files and distributed XML headers.</summary>
public static class XisfReader
{
    private static readonly byte[] Signature = "XISF0100"u8.ToArray();

    /// <summary>Reads a document from a file or readable stream using the specified resource policy.</summary>
    /// <param name="path">The source file path.</param>
    /// <param name="options">The operation settings, or null to use defaults.</param>
    /// <returns>The parsed document.</returns>
    /// <remarks>Image attachments are read lazily from the original file. Keep it available until required samples are materialized.</remarks>
    public static XisfDocument Load(string path, XisfReaderOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        using FileStream stream = File.OpenRead(fullPath);
        return LoadCore(stream, Path.GetDirectoryName(fullPath), fullPath, ValidateOptions(options));
    }

    /// <summary>Reads a document from a file or readable stream using the specified resource policy.</summary>
    /// <param name="stream">The caller-owned stream, positioned at the file origin; it remains open.</param>
    /// <param name="baseDirectory">The directory used to resolve header-relative resources, or null if unavailable.</param>
    /// <param name="options">The operation settings, or null to use defaults.</param>
    /// <returns>The parsed document.</returns>
    /// <remarks>The stream remains open. Monolithic input is buffered within MaximumBufferedInputBytes.</remarks>
    public static XisfDocument Load(Stream stream, string? baseDirectory = null, XisfReaderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return LoadCore(stream, baseDirectory is null ? null : Path.GetFullPath(baseDirectory), null, ValidateOptions(options));
    }

    /// <summary>Reads a file on a worker thread with cooperative cancellation at input-read boundaries.</summary>
    /// <param name="path">The source file path.</param>
    /// <param name="options">The operation settings, or null to use defaults.</param>
    /// <param name="cancellationToken">A token observed at the documented cooperative cancellation boundaries.</param>
    /// <returns>A task yielding the parsed document, or cancellation at an observed checkpoint.</returns>
    public static Task<XisfDocument> LoadAsync(string path, XisfReaderOptions? options = null, CancellationToken cancellationToken = default)
    {
        options = ValidateOptions(options);
        return Task.Run(() =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            string fullPath = Path.GetFullPath(path);
            using FileStream file = File.OpenRead(fullPath);
            using CancellationReadStream stream = new(file, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            XisfDocument result = LoadCore(stream, Path.GetDirectoryName(fullPath), fullPath, ValidateOptions(options), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }, cancellationToken);
    }

    /// <summary>Loads a stream on a worker thread with cooperative cancellation.</summary>
    /// <param name="stream">The caller-owned readable stream; it remains open.</param>
    /// <param name="baseDirectory">The directory for header-relative resources, or null.</param>
    /// <param name="options">The reader policy, or null to use defaults.</param>
    /// <param name="cancellationToken">A token observed during reading and XML parsing.</param>
    /// <returns>A task yielding the parsed document.</returns>
    public static Task<XisfDocument> LoadAsync(Stream stream, string? baseDirectory = null, XisfReaderOptions? options = null, CancellationToken cancellationToken = default)
    {
        options = ValidateOptions(options);
        return Task.Run(() =>
        {
            ArgumentNullException.ThrowIfNull(stream);
            using CancellationReadStream cancellable = new(stream, cancellationToken);
            XisfDocument result = LoadCore(cancellable, baseDirectory is null ? null : Path.GetFullPath(baseDirectory), null, ValidateOptions(options), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }, cancellationToken);
    }

    /// <summary>Checks the XML signature using its embedded key; this does not authenticate the signer.</summary>
    /// <param name="document">The document to read, validate, sign, or serialize.</param>
    /// <returns>True if signature verification succeeds; false if no signature is present or verification fails.</returns>
    public static bool VerifyXmlSignature(XisfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        XmlDocument xml = new() { PreserveWhitespace = true };
        using XmlReader reader = document.XmlCore.CreateReader();
        xml.Load(reader);
        XmlElement? signature;
        if (document.DetachedSignature is { } detached)
        {
            XmlDocument signatureDocument = new() { PreserveWhitespace = true };
            using XmlReader signatureReader = detached.CreateReader();
            signatureDocument.Load(signatureReader);
            signature = signatureDocument.DocumentElement;
        }
        else signature = xml.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl).OfType<XmlElement>().FirstOrDefault();
        if (signature is null)
            return false;
        SignedXml signedXml = new(xml);
        XisfSignatureAlgorithms.Configure(signedXml);
        try
        {
            signedXml.LoadXml(signature);
            if (signedXml.SignedInfo?.References.Count != 1) return false;
            Reference reference = (Reference)signedXml.SignedInfo.References[0]!;
            string? id = (string?)document.XmlCore.Root!.Attribute("id");
            if (document.DetachedSignature is not null)
            {
                foreach (Transform transform in reference.TransformChain)
                    if (transform is not (XmlDsigC14NTransform or XmlDsigExcC14NTransform)) return false;
                if (signedXml.SignedInfo.CanonicalizationMethod is XisfSignatureAlgorithms.Canonical11 or XisfSignatureAlgorithms.Canonical11Comments &&
                    document.DetachedSignature.Attributes().Any(a => a.Name.Namespace == XNamespace.Xml && a.Name.LocalName is "id" or "base")) return false;
            }
            if (document.DetachedSignature is not null && (string.IsNullOrEmpty(id) || reference.Uri != "#" + id ||
                document.XmlCore.Descendants().Count(e => (string?)e.Attribute("id") == id) != 1)) return false;
            foreach (XElement block in document.XmlCore.Descendants().Where(e => e.Attribute("location") is not null))
            {
                string location = (string)block.Attribute("location")!;
                if (location.StartsWith("inline:", StringComparison.Ordinal) || location == "embedded") continue;
                if (block.Attribute("checksum") is null) return false;
                new XisfDataBlock(block, document.Source).VerifyIntegrity();
            }
            return signedXml.CheckSignature();
        }
        catch (Exception exception) when (exception is CryptographicException or XisfFormatException or XmlException or IOException)
        { return false; }
    }

    private static XisfDocument LoadCore(Stream input, string? baseDirectory, string? sourceFilePath, XisfReaderOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            byte[] prefix = new byte[16];
            int prefixLength = 0;
            while (prefixLength < prefix.Length)
            {
                int read = input.Read(prefix, prefixLength, prefix.Length - prefixLength);
                if (read == 0) break;
                prefixLength += read;
            }
            bool monolithic = prefixLength == 16 && prefix.AsSpan(0, 8).SequenceEqual(Signature);
            string text;
            byte[] file = [];
            if (monolithic)
            {
                if (prefix[12] != 0 || prefix[13] != 0 || prefix[14] != 0 || prefix[15] != 0)
                    throw new XisfFormatException("The reserved XISF file-header field must be zero.");
                uint headerLength = BinaryPrimitives.ReadUInt32LittleEndian(prefix.AsSpan(8, 4));
                if (headerLength == 0 || headerLength > options.MaximumXmlHeaderSize || input.CanSeek && headerLength > input.Length - 16)
                    throw new XisfFormatException("The XISF XML header length is invalid.");
                if (sourceFilePath is null && 16L + headerLength > options.MaximumBufferedInputBytes)
                    throw new XisfFormatException("The buffered XISF input exceeds the configured size limit.");
                byte[] header = new byte[checked((int)headerLength)];
                input.ReadExactly(header);
                text = new UTF8Encoding(false, true).GetString(header);
                if (sourceFilePath is null)
                {
                    using MemoryStream copy = new();
                    copy.Write(prefix);
                    copy.Write(header);
                    if (copy.Length > options.MaximumBufferedInputBytes)
                        throw new XisfFormatException("The buffered XISF input exceeds the configured size limit.");
                    CopyToWithLimit(input, copy, options.MaximumBufferedInputBytes - copy.Length);
                    file = copy.ToArray();
                }
            }
            else
            {
                using MemoryStream copy = new();
                copy.Write(prefix, 0, prefixLength);
                CopyToWithLimit(input, copy, options.MaximumXmlHeaderSize - prefixLength);
                file = copy.ToArray();
                text = new UTF8Encoding(false, true).GetString(file);
                if (!text.TrimStart('\uFEFF').AsSpan().TrimStart().StartsWith("<", StringComparison.Ordinal))
                    throw new XisfFormatException("The stream is neither a monolithic XISF file nor an XML XISF header file.");
            }
            XmlReaderSettings xmlSettings = new()
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = options.MaximumXmlCharacters
            };
            XDocument xml = XisfXmlHeader.Read(text, xmlSettings, cancellationToken);
            XisfDataSource source = new(monolithic ? file : [], baseDirectory, monolithic ? sourceFilePath : null, options);
            return ParseHeader(xml, source, cancellationToken);
        }
        catch (XisfFormatException)
        {
            throw;
        }
        catch (Exception ex) when (ex is XmlException or DecoderFallbackException or OverflowException or FormatException)
        {
            throw new XisfFormatException("The XISF unit is malformed.", ex);
        }
    }

    internal static XisfDocument ParseHeader(XDocument xml, XisfDataSource source, CancellationToken cancellationToken = default)
    {
            XElement root = xml.Root ?? throw new XisfFormatException("The XISF XML header has no root element.");
            if (root.Name.LocalName != "xisf")
                throw new XisfFormatException("The XISF XML root element must be named 'xisf'.");
            string version = RequiredAttribute(root, "version");
            if (version != "1.0")
                throw new XisfFormatException($"Unsupported XISF version '{version}'.");

            EnforceXmlLimits(root, source.Options, cancellationToken);


            XisfReferenceResolver references = new(root);
            XElement? metadataElement = references.Children(root, "Metadata").FirstOrDefault();
            Dictionary<string, XisfProperty> metadata = metadataElement is null
                ? []
                : ParseProperties(metadataElement, source, references);
            Dictionary<string, XisfProperty> properties = ParseProperties(root, source, references);
            List<XisfTable> tables = references.Children(root, "Table").Select(e => ParseTable(e, source, references)).ToList();
            List<XisfImage> images = [];
            List<XisfUnavailableObject> unavailable = [];
            foreach (XElement element in references.Children(root, "Image"))
            {
                try { images.Add(XisfImage.Parse(element, source, references)); }
                catch (XisfUnsupportedFeatureException exception) { unavailable.Add(new(element, exception.Message)); }
            }
            return new XisfDocument(version, xml, metadata, properties, tables, images, source) { UnavailableObjects = unavailable.AsReadOnly() };
    }

    internal static Dictionary<string, XisfProperty> ParseProperties(XElement parent, XisfDataSource source, XisfReferenceResolver? references = null)
    {
        Dictionary<string, XisfProperty> result = new(StringComparer.Ordinal);
        IEnumerable<XElement> elements = references?.Children(parent, "Property") ?? parent.Elements().Where(e => e.Name.LocalName == "Property");
        foreach (XElement element in elements)
        {
            string id = RequiredAttribute(element, "id");
            string type = RequiredAttribute(element, "type");
            object? value = null;
            string? unavailable = null;
            try { value = ParsePropertyValue(element, type, source); }
            catch (XisfUnsupportedFeatureException exception) { unavailable = exception.Message; }
            catch (Exception exception) when (id.StartsWith("AstrometricSolution:", StringComparison.Ordinal) && exception is XisfFormatException or FormatException or OverflowException)
            { unavailable = exception.Message; }
            if (!result.TryAdd(id, new XisfProperty(id, type, value, (string?)element.Attribute("format"), element, (string?)element.Attribute("comment"))
                { IsAvailable = unavailable is null, UnavailableReason = unavailable, Source = source }))
                throw new XisfFormatException($"Duplicate property identifier '{id}'.");
        }
        return result;
    }

    internal static object? ParsePropertyValue(XElement element, string type, XisfDataSource source)
    {
        string canonicalType = CanonicalPropertyType(type);
        string text = (string?)element.Attribute("value") ?? (canonicalType == "String" ? element.Value : element.Value.Trim());
        if (element.Attribute("location") is not null || element.Elements().Any(e => e.Name.LocalName == "Data"))
        {
            byte[] data = new XisfDataBlock(element, source).Read();
            if (canonicalType == "String")
            {
                string value = new UTF8Encoding(false, true).GetString(data);
                if (value.Contains('\0')) throw new XisfFormatException("XISF strings cannot contain null code points.");
                return value;
            }
            {
                object value = ParseBinaryProperty(canonicalType, data, string.Equals((string?)element.Attribute("byteOrder"), "big", StringComparison.OrdinalIgnoreCase));
                return ValidateAndShapeCollection(element, canonicalType, value);
            }
        }

        string normalized = canonicalType.ToLowerInvariant();
        if (normalized == "string") return text;
        if (normalized == "boolean") return ParseBoolean(text);
        if (normalized == "timepoint") return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        if (normalized == "complex128")
        {
            string[] components = text.Trim().Trim('(', ')').Split(',');
            if (components.Length != 2) throw new XisfFormatException("Complex128 requires two components.");
            return new XisfComplex128(XisfFloat128.Parse(components[0]), XisfFloat128.Parse(components[1]));
        }
        if (normalized == "complex32" || normalized == "complex64") return ParseComplex(text);
        if (normalized == "float32") return (float)ParseFloatingPoint(text);
        if (normalized == "float64") return ParseFloatingPoint(text);
        if (normalized == "float128") return XisfFloat128.Parse(text);
        if (type.Equals("ByteArray", StringComparison.OrdinalIgnoreCase)) return string.IsNullOrWhiteSpace(text) ? [] : Convert.FromHexString(RemoveWhitespace(text));
        if (normalized.EndsWith("vector", StringComparison.Ordinal)) return ValidateAndShapeCollection(element, canonicalType, ParseVector(canonicalType, text));
        if (normalized.EndsWith("matrix", StringComparison.Ordinal)) return ValidateAndShapeCollection(element, canonicalType, ParseVector(canonicalType.Replace("Matrix", "Vector", StringComparison.Ordinal), text));
        return normalized switch
        {
            "int8" => (sbyte)ParseInteger(text, 8, true),
            "int16" => (short)ParseInteger(text, 16, true),
            "int32" or "int" => (int)ParseInteger(text, 32, true),
            "int64" => (long)ParseInteger(text, 64, true),
            "uint8" => (byte)ParseInteger(text, 8, false),
            "uint16" => (ushort)ParseInteger(text, 16, false),
            "uint32" => (uint)ParseInteger(text, 32, false),
            "uint64" => (ulong)ParseInteger(text, 64, false),
            "int128" => (Int128)ParseInteger(text, 128, true),
            "uint128" => (UInt128)ParseInteger(text, 128, false),
            _ => throw new XisfUnsupportedFeatureException($"Unsupported property type '{type}'.")
        };
    }

    private static string CanonicalPropertyType(string type) => type.ToLowerInvariant() switch
    {
        "byte" => "UInt8",
        "short" => "Int16",
        "ushort" => "UInt16",
        "int" => "Int32",
        "uint" => "UInt32",
        "float" => "Float32",
        "double" => "Float64",
        "quad" => "Float128",
        "complex" => "Complex64",
        "bytearray" => "UI8Vector",
        "ivector" => "I32Vector",
        "uivector" => "UI32Vector",
        "vector" => "F64Vector",
        "bytematrix" => "UI8Matrix",
        "imatrix" => "I32Matrix",
        "uimatrix" => "UI32Matrix",
        "matrix" => "F64Matrix",
        _ => type
    };

    private static object ValidateAndShapeCollection(XElement element, string type, object value)
    {
        if (value is not Array array)
            return value;
        if (type.EndsWith("Vector", StringComparison.OrdinalIgnoreCase))
        {
            int length = ParseRequiredNonNegativeInteger(element, "length");
            if (array.Length != length)
                throw new XisfFormatException($"Property '{(string?)element.Attribute("id")}' declares {length} vector elements but contains {array.Length}.");
            return value;
        }
        if (!type.EndsWith("Matrix", StringComparison.OrdinalIgnoreCase))
            return value;
        int rows = ParseRequiredNonNegativeInteger(element, "rows");
        int columns = ParseRequiredNonNegativeInteger(element, "columns");
        if (array.Length != checked(rows * columns))
            throw new XisfFormatException($"Property '{(string?)element.Attribute("id")}' matrix dimensions do not match its data.");
        Array matrix = Array.CreateInstance(array.GetType().GetElementType()!, rows, columns);
        for (int i = 0; i < array.Length; i++)
            matrix.SetValue(array.GetValue(i), i / columns, i % columns);
        return matrix;
    }

    private static int ParseRequiredNonNegativeInteger(XElement element, string name)
    {
        string text = RequiredAttribute(element, name);
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 0)
            throw new XisfFormatException($"Element '{element.Name.LocalName}' has an invalid '{name}' attribute.");
        return value;
    }

    private static object ParseVector(string type, string text)
    {
        string[] values = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        string prefix = type[..type.IndexOf(type.Contains("Matrix", StringComparison.Ordinal) ? "Matrix" : "Vector", StringComparison.Ordinal)].ToUpperInvariant();
        return prefix switch
        {
            "I8" => values.Select(v => sbyte.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
            "I16" => values.Select(v => short.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
            "I32" => values.Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
            "I64" => values.Select(v => long.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
            "UI8" => values.Select(v => byte.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
            "UI16" => values.Select(v => ushort.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
            "UI32" => values.Select(v => uint.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
            "UI64" => values.Select(v => ulong.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
            "I128" => values.Select(v => Int128.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
            "UI128" => values.Select(v => UInt128.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
            "F32" => values.Select(v => float.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray(),
            _ => values.Select(v => double.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray()
        };
    }

    private static object ParseBinaryProperty(string type, byte[] data, bool bigEndian)
    {
        string scalarType = type.Replace("Vector", "", StringComparison.Ordinal).Replace("Matrix", "", StringComparison.Ordinal);
        int size = scalarType.ToUpperInvariant() switch
        {
            "I8" or "UI8" => 1,
            "I16" or "UI16" => 2,
            "I32" or "UI32" or "F32" or "C32" => scalarType.Equals("C32", StringComparison.OrdinalIgnoreCase) ? 8 : 4,
            "I64" or "UI64" or "F64" or "C64" => scalarType.Equals("C64", StringComparison.OrdinalIgnoreCase) ? 16 : 8,
            "I128" or "UI128" or "F128" => 16,
            "C128" => 32,
            _ => throw new XisfUnsupportedFeatureException($"Unsupported binary property type '{type}'.")
        };
        if (data.Length % size != 0)
            throw new XisfFormatException($"Data size is not valid for property type '{type}'.");
        int count = data.Length / size;
        return scalarType.ToUpperInvariant() switch
        {
            "I8" => data.Select(v => unchecked((sbyte)v)).ToArray(),
            "UI8" => data,
            "I16" => Enumerable.Range(0, count).Select(i => bigEndian ? BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(i * size)) : BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(i * size))).ToArray(),
            "UI16" => Enumerable.Range(0, count).Select(i => bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(i * size)) : BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(i * size))).ToArray(),
            "I32" => Enumerable.Range(0, count).Select(i => bigEndian ? BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(i * size)) : BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(i * size))).ToArray(),
            "UI32" => Enumerable.Range(0, count).Select(i => bigEndian ? BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(i * size)) : BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i * size))).ToArray(),
            "I64" => Enumerable.Range(0, count).Select(i => bigEndian ? BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(i * size)) : BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(i * size))).ToArray(),
            "UI64" => Enumerable.Range(0, count).Select(i => bigEndian ? BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(i * size)) : BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(i * size))).ToArray(),
            "I128" => Enumerable.Range(0, count).Select(i => bigEndian ? BinaryPrimitives.ReadInt128BigEndian(data.AsSpan(i * size)) : BinaryPrimitives.ReadInt128LittleEndian(data.AsSpan(i * size))).ToArray(),
            "UI128" => Enumerable.Range(0, count).Select(i => bigEndian ? BinaryPrimitives.ReadUInt128BigEndian(data.AsSpan(i * size)) : BinaryPrimitives.ReadUInt128LittleEndian(data.AsSpan(i * size))).ToArray(),
            "F128" => Enumerable.Range(0, count).Select(i => new XisfFloat128(bigEndian ? BinaryPrimitives.ReadUInt128BigEndian(data.AsSpan(i * size)) : BinaryPrimitives.ReadUInt128LittleEndian(data.AsSpan(i * size)))).ToArray(),
            "C128" => Enumerable.Range(0, count).Select(i => new XisfComplex128(
                new(bigEndian ? BinaryPrimitives.ReadUInt128BigEndian(data.AsSpan(i * size)) : BinaryPrimitives.ReadUInt128LittleEndian(data.AsSpan(i * size))),
                new(bigEndian ? BinaryPrimitives.ReadUInt128BigEndian(data.AsSpan(i * size + 16)) : BinaryPrimitives.ReadUInt128LittleEndian(data.AsSpan(i * size + 16))))).ToArray(),
            "F32" => Enumerable.Range(0, count).Select(i => BitConverter.Int32BitsToSingle(bigEndian ? BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(i * size)) : BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(i * size)))).ToArray(),
            "F64" => Enumerable.Range(0, count).Select(i => BitConverter.Int64BitsToDouble(bigEndian ? BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(i * size)) : BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(i * size)))).ToArray(),
            "C32" => Enumerable.Range(0, count).Select(i => new Complex(
                BitConverter.Int32BitsToSingle(bigEndian ? BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(i * size, 4)) : BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(i * size, 4))),
                BitConverter.Int32BitsToSingle(bigEndian ? BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(i * size + 4, 4)) : BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(i * size + 4, 4))))).ToArray(),
            "C64" => Enumerable.Range(0, count).Select(i => new Complex(
                BitConverter.Int64BitsToDouble(bigEndian ? BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(i * size, 8)) : BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(i * size, 8))),
                BitConverter.Int64BitsToDouble(bigEndian ? BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(i * size + 8, 8)) : BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(i * size + 8, 8))))).ToArray(),
            _ => throw new XisfUnsupportedFeatureException($"Unsupported binary property type '{type}'.")
        };
    }

    internal static XisfTable ParseTable(XElement element, XisfDataSource source, XisfReferenceResolver references)
    {
        string id = RequiredAttribute(element, "id");
        XElement? structure = references.Children(element, "Structure").SingleOrDefault();
        List<XisfTableField> fields = structure?.Elements().Where(e => e.Name == structure.Name.Namespace + "Field")
            .Select(e => new XisfTableField(RequiredAttribute(e, "id"), RequiredAttribute(e, "type"), (string?)e.Attribute("header"), (string?)e.Attribute("format"))).ToList() ?? [];
        List<XisfTableRow> rows = [];
        foreach (XElement row in element.Elements(element.Name.Namespace + "Row"))
        {
            List<XisfTableCell> cells = [];
            int index = 0;
            foreach (XElement cell in row.Elements(row.Name.Namespace + "Cell"))
            {
                string type = index < fields.Count ? fields[index].Type : "String";
                try { cells.Add(new XisfTableCell(ParsePropertyValue(cell, type, source), cell) { Source = source }); }
                catch (XisfUnsupportedFeatureException exception) { cells.Add(new(null, cell) { IsAvailable = false, UnavailableReason = exception.Message, Source = source }); }
                index++;
            }
            rows.Add(new XisfTableRow(cells));
        }
        return new XisfTable(id, (string?)element.Attribute("caption"), fields, rows, element);
    }

    internal static string RequiredAttribute(XElement element, string name) =>
        (string?)element.Attribute(name) ?? throw new XisfFormatException($"Element '{element.Name.LocalName}' requires attribute '{name}'.");

    internal static double[] ParseDoubles(string? value) => string.IsNullOrWhiteSpace(value)
        ? []
        : value.Split(':', StringSplitOptions.RemoveEmptyEntries).Select(v => double.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();

    internal static bool ParseBoolean(string value) => value.Trim() switch
    {
        "true" or "1" => true,
        "false" or "0" => false,
        _ => throw new XisfFormatException("Invalid Boolean value.")
    };

    internal static double ParseFloatingPoint(string value) => value.Trim() switch
    {
        "NaN" or "nan" or "-nan" => double.NaN,
        "+Inf" or "Inf" or "inf" or "+inf" => double.PositiveInfinity,
        "-Inf" or "-inf" => double.NegativeInfinity,
        _ => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture)
    };

    private static BigInteger ParseInteger(string text, int bits, bool signed)
    {
        text = text.Trim();
        int radix = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? 16 :
            text.StartsWith("0o", StringComparison.OrdinalIgnoreCase) ? 8 : text.StartsWith("0b", StringComparison.OrdinalIgnoreCase) ? 2 : 10;
        BigInteger value;
        if (radix == 10) value = BigInteger.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        else
        {
            value = 0;
            if (text.Length == 2) throw new XisfFormatException("An integer prefix requires digits.");
            foreach (char c in text.AsSpan(2))
            {
                int digit = c is >= '0' and <= '9' ? c - '0' : c is >= 'a' and <= 'f' ? c - 'a' + 10 : c is >= 'A' and <= 'F' ? c - 'A' + 10 : -1;
                if (digit < 0 || digit >= radix) throw new XisfFormatException("Invalid integer digit.");
                value = value * radix + digit;
            }
            if (value >= (BigInteger.One << bits)) throw new XisfFormatException("Integer exceeds its declared width.");
            if (signed && value >= (BigInteger.One << (bits - 1))) value -= BigInteger.One << bits;
        }
        BigInteger minimum = signed ? -(BigInteger.One << (bits - 1)) : BigInteger.Zero;
        BigInteger maximum = (BigInteger.One << (signed ? bits - 1 : bits)) - 1;
        if (value < minimum || value > maximum) throw new XisfFormatException("Integer exceeds its declared range.");
        return value;
    }

    private static Complex ParseComplex(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && value[0] == '(' && value[^1] == ')')
            value = value[1..^1];
        string[] parts = value.Split(value.Contains(',') ? ',' : ':');
        return parts.Length == 2
            ? new Complex(ParseFloatingPoint(parts[0]), ParseFloatingPoint(parts[1]))
            : new Complex(ParseFloatingPoint(value), 0);
    }

    internal static string RemoveWhitespace(string value) => string.Concat(value.Where(c => !char.IsWhiteSpace(c)));

    private static void CopyToWithLimit(Stream input, Stream output, long maximumBytes)
    {
        if (maximumBytes < 0)
            throw new XisfFormatException("The XISF input exceeds the configured size limit.");
        byte[] buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, maximumBytes - total + 1));
            if (read == 0) break;
            total = checked(total + read);
            if (total > maximumBytes)
                throw new XisfFormatException("The XISF input exceeds the configured size limit.");
            output.Write(buffer, 0, read);
        }
    }

    private static void EnforceXmlLimits(XElement root, XisfReaderOptions options, CancellationToken cancellationToken)
    {
        int elements = 0;
        int properties = 0;
        int images = 0;
        int cells = 0;
        foreach (XElement element in root.DescendantsAndSelf())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++elements > options.MaximumXmlElements)
                throw new XisfFormatException("The XISF XML header exceeds the configured element-count limit.");
            int depth = 0;
            for (XElement? parent = element.Parent; parent is not null; parent = parent.Parent)
                if (++depth > options.MaximumXmlDepth)
                    throw new XisfFormatException("The XISF XML header exceeds the configured depth limit.");
            switch (element.Name.LocalName)
            {
                case "Property" when ++properties > options.MaximumPropertyCount:
                    throw new XisfFormatException("The XISF unit exceeds the configured property-count limit.");
                case "Image" when ++images > options.MaximumImageCount:
                    throw new XisfFormatException("The XISF unit exceeds the configured image-count limit.");
                case "Cell" when ++cells > options.MaximumTableCellCount:
                    throw new XisfFormatException("The XISF unit exceeds the configured table-cell limit.");
            }
        }
    }

    private static XisfReaderOptions ValidateOptions(XisfReaderOptions? options)
    {
        options = options?.Snapshot() ?? new XisfReaderOptions();
        if (options.MaximumBufferedInputBytes < 0 || options.MaximumXmlHeaderSize <= 0 || options.MaximumXmlCharacters <= 0 || options.MaximumXmlDepth <= 0 ||
            options.MaximumXmlElements <= 0 || options.MaximumImageCount <= 0 || options.MaximumImageDimensions <= 0 ||
            options.MaximumPixelsPerImage <= 0 || options.MaximumPropertyCount <= 0 || options.MaximumTableCellCount <= 0 ||
            options.MaximumDataBlockSize <= 0 || !double.IsFinite(options.MaximumDecompressionRatio) || options.MaximumDecompressionRatio < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "All XISF resource limits must be positive and the decompression ratio must be at least one.");
        if (options.NetworkTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options), "The network timeout must be positive.");
        return options;
    }
}

internal sealed class XisfReferenceResolver
{
    private const string XisfNamespace = "http://www.pixinsight.com/xisf";
    private static readonly Regex Identifier = new("^[_a-zA-Z][_a-zA-Z0-9]*$", RegexOptions.CultureInvariant);
    private readonly IReadOnlyDictionary<string, XElement> _elements;

    public XisfReferenceResolver(XElement root)
    {
        Dictionary<string, XElement> elements = new(StringComparer.Ordinal);
        foreach (XElement element in root.DescendantsAndSelf().Where(e => e.Name.Namespace == root.Name.Namespace))
        {
            XAttribute? attribute = element.Attribute("uid");
            if (attribute is null)
                continue;
            if (!Identifier.IsMatch(attribute.Value))
                throw new XisfFormatException($"Invalid core element uid '{attribute.Value}'.");
            if (!elements.TryAdd(attribute.Value, element))
                throw new XisfFormatException($"Duplicate core element uid '{attribute.Value}'.");
        }
        _elements = elements;
        HashSet<XElement> active = [], done = [];
        void Visit(XElement element)
        {
            if (done.Contains(element)) return;
            if (!active.Add(element)) throw new XisfFormatException("Cyclic core element references are not permitted.");
            foreach (XElement reference in element.Descendants(XName.Get("Reference", root.Name.NamespaceName))) Visit(Resolve(reference));
            active.Remove(element); done.Add(element);
        }
        foreach (XElement element in elements.Values) Visit(element);
        foreach (XElement reference in root.Descendants(XName.Get("Reference", root.Name.NamespaceName)))
            Resolve(reference);
    }

    public IEnumerable<XElement> Children(XElement parent, string name)
    {
        foreach (XElement child in parent.Elements())
        {
            XElement candidate = child.Name.LocalName == "Reference" && child.Name.Namespace == parent.Name.Namespace ? Resolve(child) : child;
            if (candidate.Name.LocalName == name && (candidate.Name.Namespace == parent.Name.Namespace || candidate.Name.NamespaceName == XisfNamespace))
                yield return candidate;
        }
    }

    private XElement Resolve(XElement reference)
    {
        if (reference.Attribute("uid") is not null || reference.Attribute("ref") is not { } attribute || !Identifier.IsMatch(attribute.Value))
            throw new XisfFormatException("A Reference element must define one valid ref attribute.");
        if (!_elements.TryGetValue(attribute.Value, out XElement? target))
            throw new XisfFormatException($"Referenced core element '{attribute.Value}' was not found.");
        if (target.Name.LocalName == "Reference") throw new XisfFormatException("Chained references are not permitted.");
        return target;
    }
}
