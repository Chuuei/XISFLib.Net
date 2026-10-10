using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Text.RegularExpressions;

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

/// <summary>Reads FITS HDUs, validates headers and payloads, and decodes supported images and tables.</summary>
public static partial class FitsReader
{
    private const int BlockSize = 2880;

    /// <summary>Reads a document from a file or readable stream using the specified resource policy.</summary>
    /// <param name="path">The source file path.</param>
    /// <param name="options">The operation settings, or null to use defaults.</param>
    /// <returns>The parsed document.</returns>
    public static FitsDocument Load(string path, FitsReaderOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
        return Load(stream, options);
    }

    /// <summary>Reads a file on a worker thread with cooperative cancellation at input-read boundaries.</summary>
    /// <param name="path">The source file path.</param>
    /// <param name="options">The operation settings, or null to use defaults.</param>
    /// <param name="cancellationToken">A token observed at the documented cooperative cancellation boundaries.</param>
    /// <returns>A task yielding the parsed document, or cancellation at an observed checkpoint.</returns>
    public static async Task<FitsDocument> LoadAsync(string path, FitsReaderOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan | FileOptions.Asynchronous);
        return await Task.Run(() =>
        {
            using CancellationReadStream cancellable = new(stream, cancellationToken);
            FitsDocument result = LoadCore(cancellable, options, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads a document from a file or readable stream using the specified resource policy.</summary>
    /// <param name="stream">The caller-owned stream, positioned at the file origin; it remains open.</param>
    /// <param name="options">The operation settings, or null to use defaults.</param>
    /// <returns>The parsed document.</returns>
    /// <remarks>The stream remains open. Non-seekable input is buffered within MaximumBufferedInputBytes.</remarks>
    public static FitsDocument Load(Stream stream, FitsReaderOptions? options = null) => LoadCore(stream, options, default);

    /// <summary>Loads a FITS stream on a worker thread with cooperative cancellation.</summary>
    /// <param name="stream">The caller-owned readable stream; it remains open.</param>
    /// <param name="options">The reader policy, or null to use defaults.</param>
    /// <param name="cancellationToken">A token observed during reading and table decoding.</param>
    /// <returns>A task yielding the parsed document.</returns>
    public static Task<FitsDocument> LoadAsync(Stream stream, FitsReaderOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            ArgumentNullException.ThrowIfNull(stream);
            using CancellationReadStream cancellable = new(stream, cancellationToken);
            FitsDocument result = LoadCore(cancellable, options, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }, cancellationToken);

    private static FitsDocument LoadCore(Stream stream, FitsReaderOptions? options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead) throw new ArgumentException("The FITS stream must be readable.", nameof(stream));
        options ??= new FitsReaderOptions();
        ValidateOptions(options);
        if (!stream.CanSeek)
        {
            using MemoryStream buffered = new();
            byte[] copyBuffer = new byte[81920];
            int read;
            while ((read = stream.Read(copyBuffer, 0, copyBuffer.Length)) != 0)
            {
                if (checked(buffered.Length + read) > options.MaximumBufferedInputBytes)
                    throw new FitsFormatException("The non-seekable FITS stream exceeds the configured buffered input limit.");
                buffered.Write(copyBuffer, 0, read);
            }
            buffered.Position = 0;
            return LoadCore(buffered, options, cancellationToken);
        }
        List<FitsHdu> hdus = [];
        List<FitsValidationDiagnostic> diagnostics = [];
        long totalDecodedBytes = 0;
        try
        {
            while (!AtEnd(stream))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (hdus.Count >= options.MaximumHduCount) throw new FitsFormatException("The FITS file exceeds the configured HDU limit.");
                int hduIndex = hdus.Count;
                byte[] headerBytes = ReadHeader(stream, options, out FitsHeader header);
                FitsHduType type = GetHduType(header, hdus.Count == 0);
                ValidateHeader(header, type, hduIndex, diagnostics);
                ThrowIfStrict(options, diagnostics, hduIndex);
                long dataLength = GetDataLength(header, type, options);
                if (dataLength < 0 || dataLength > options.MaximumDataSize || dataLength > int.MaxValue)
                    throw new FitsFormatException("A FITS HDU exceeds the configured or supported data size.");
                totalDecodedBytes = checked(totalDecodedBytes + dataLength);
                if (totalDecodedBytes > options.MaximumTotalDecodedBytes)
                    throw new FitsFormatException("The FITS file exceeds the configured total decoded byte limit.");
                byte[] data = ReadExactly(stream, checked((int)dataLength));
                int padding = Padding(dataLength);
                byte[] pad = padding == 0 ? [] : ReadExactly(stream, padding);

                FitsHdu hdu = FitsHdu.FromOwnedData(type, header, data);
                uint? dataSum = header.GetValue<string>("DATASUM") is not null || header.FindFirst("CHECKSUM") is not null
                    ? FitsChecksum.Sum(data, pad) : null;
                hdu.DataChecksumValid = ValidateDataChecksum(header, data, pad, dataSum);
                hdu.ChecksumValid = ValidateChecksum(header, headerBytes, data, pad, dataSum);
                ValidatePayload(hdu, pad, hduIndex, diagnostics);
                ThrowIfStrict(options, diagnostics, hduIndex);
                hdu.Content = ParseContent(hdu, options, cancellationToken);
                hdus.Add(hdu);
            }
        }
        catch (FitsFormatException) { throw; }
        catch (Exception exception) when (exception is EndOfStreamException or OverflowException or FormatException or ArgumentException)
        {
            throw new FitsFormatException("The FITS file is malformed.", exception);
        }
        if (hdus.Count == 0) throw new FitsFormatException("The stream contains no FITS HDUs.");
        return new FitsDocument(hdus, diagnostics);
    }

    private static void ValidateOptions(FitsReaderOptions options)
    {
        if (options.MaximumHduCount <= 0 || options.MaximumHeaderCards <= 0 || options.MaximumHeaderBytes < BlockSize ||
            options.MaximumAxisCount < 0 || options.MaximumDataSize < 0 || options.MaximumTotalDecodedBytes < 0 ||
            options.MaximumTableCells < 0 || options.MaximumBufferedInputBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "FITS reader resource limits must be nonnegative, and count limits must be positive.");
    }

    private static bool AtEnd(Stream stream)
    {
        if (stream.CanSeek) return stream.Position == stream.Length;
        int value = stream.ReadByte();
        if (value < 0) return true;
        throw new FitsFormatException("A non-seekable FITS stream cannot be probed after an HDU.");
    }

    private static byte[] ReadHeader(Stream stream, FitsReaderOptions options, out FitsHeader header)
    {
        using MemoryStream bytes = new();
        header = new FitsHeader();
        bool ended = false;
        while (!ended)
        {
            if (checked(bytes.Length + BlockSize) > options.MaximumHeaderBytes)
                throw new FitsFormatException("A FITS header exceeds the configured header byte limit.");
            byte[] block = ReadExactly(stream, BlockSize);
            bytes.Write(block);
            for (int offset = 0; offset < block.Length; offset += 80)
            {
                string cardText = Encoding.ASCII.GetString(block, offset, 80);
                string first = cardText[..8].TrimEnd();
                if (first == "END")
                {
                    if (header.Count >= options.MaximumHeaderCards)
                        throw new FitsFormatException("A FITS header exceeds the configured header card limit.");
                    ended = true;
                    break;
                }
                if (string.IsNullOrWhiteSpace(cardText)) continue;
                if (header.Count >= options.MaximumHeaderCards)
                    throw new FitsFormatException("A FITS header exceeds the configured header card limit.");
                FitsHeaderCard card = ParseCard(cardText);
                if (card.Keyword.Equals("CONTINUE", StringComparison.OrdinalIgnoreCase) && card.Value is string continuation && header.Count > 0 && header[^1].Value is string previous)
                {
                    bool more = continuation.EndsWith('&');
                    string joined = (previous.EndsWith('&') ? previous[..^1] : previous) + (more ? continuation : continuation);
                    header[^1] = header[^1] with { Value = joined };
                }
                else header.Add(card);
            }
        }
        return bytes.ToArray();
    }

    internal static FitsHeaderCard ParseCard(string text)
    {
        string keyword;
        int equals;
        if (text.StartsWith("HIERARCH ", StringComparison.Ordinal))
        {
            equals = text.IndexOf("= ", StringComparison.Ordinal);
            keyword = equals >= 0 ? text[..equals].TrimEnd() : text.TrimEnd();
        }
        else
        {
            keyword = text[..8].TrimEnd();
            equals = text.Length >= 10 && text[8] == '=' ? 8 : -1;
        }
        if (keyword is "COMMENT" or "HISTORY" or "CONTINUE" && equals < 0)
        {
            string content = text[8..].TrimEnd();
            if (keyword == "CONTINUE") return new FitsHeaderCard(keyword, ParseValueAndComment(content).Value);
            return new FitsHeaderCard(keyword, content.TrimStart());
        }
        if (equals < 0) return new FitsHeaderCard(keyword, text[8..].Trim());
        (object? value, string? comment) = ParseValueAndComment(text[(equals + 2)..]);
        return new FitsHeaderCard(keyword, value, comment);
    }

    private static (object? Value, string? Comment) ParseValueAndComment(string field)
    {
        field = field.TrimStart();
        if (field.StartsWith('\''))
        {
            StringBuilder value = new();
            int index = 1;
            while (index < field.Length)
            {
                if (field[index] == '\'' && index + 1 < field.Length && field[index + 1] == '\'') { value.Append('\''); index += 2; continue; }
                if (field[index] == '\'') { index++; break; }
                value.Append(field[index++]);
            }
            string? comment = index < field.Length && field[index..].TrimStart().StartsWith('/')
                ? field[index..].TrimStart()[1..].Trim() : null;
            return (value.ToString().TrimEnd(), comment);
        }
        int slash = field.IndexOf('/');
        string token = (slash >= 0 ? field[..slash] : field).Trim();
        string? trailing = slash >= 0 ? field[(slash + 1)..].Trim() : null;
        if (token == "T") return (true, trailing);
        if (token == "F") return (false, trailing);
        string normalized = token.Replace('D', 'E');
        if (long.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer)) return (integer, trailing);
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double real)) return (real, trailing);
        return (token, trailing);
    }

    private static FitsHduType GetHduType(FitsHeader header, bool first)
    {
        if (first)
        {
            if (header.Count == 0 || !header[0].Keyword.Equals("SIMPLE", StringComparison.OrdinalIgnoreCase))
                throw new FitsFormatException("SIMPLE must be the first card of the primary HDU.");
            if (header.GetValue<bool?>("SIMPLE") != true) throw new FitsFormatException("The primary HDU must begin with SIMPLE = T.");
            return header.GetValue<bool?>("GROUPS") == true ? FitsHduType.RandomGroups : FitsHduType.Primary;
        }
        if (header.Count == 0 || !header[0].Keyword.Equals("XTENSION", StringComparison.OrdinalIgnoreCase))
            throw new FitsFormatException("XTENSION must be the first card of an extension HDU.");
        string extension = header.GetValue<string>("XTENSION", "")!.Trim();
        return extension.ToUpperInvariant() switch
        {
            "IMAGE" => FitsHduType.Image,
            "TABLE" => FitsHduType.AsciiTable,
            "BINTABLE" => FitsHduType.BinaryTable,
            _ => FitsHduType.Unknown
        };
    }

    private static void ValidateHeader(FitsHeader header, FitsHduType type, int hduIndex,
        ICollection<FitsValidationDiagnostic> diagnostics)
    {
        string[] mandatory = type == FitsHduType.Primary || type == FitsHduType.RandomGroups
            ? ["SIMPLE", "BITPIX", "NAXIS"]
            : ["XTENSION", "BITPIX", "NAXIS"];
        for (int index = 0; index < mandatory.Length; index++)
        {
            if (header.Count <= index || !header[index].Keyword.Equals(mandatory[index], StringComparison.Ordinal))
            {
                diagnostics.Add(new FitsValidationDiagnostic("FITS.MandatoryOrder", FitsValidationSeverity.Error,
                    $"The mandatory {mandatory[index]} keyword must be card {index + 1} of this HDU.", hduIndex,
                    header.Count > index ? index : null, mandatory[index]));
            }
        }

        int naxis = 0;
        bool validNaxis = TryGetInteger(header, "NAXIS", out long naxisValue) && naxisValue is >= 0 and <= 999;
        if (validNaxis)
        {
            naxis = (int)naxisValue;
            for (int axis = 1; axis <= naxis; axis++)
            {
                int expectedIndex = mandatory.Length - 1 + axis;
                string keyword = $"NAXIS{axis}";
                if (header.Count <= expectedIndex || !header[expectedIndex].Keyword.Equals(keyword, StringComparison.Ordinal))
                    diagnostics.Add(new FitsValidationDiagnostic("FITS.MandatoryOrder", FitsValidationSeverity.Error,
                        $"The mandatory {keyword} keyword is missing or is not in its required position.", hduIndex,
                        header.Count > expectedIndex ? expectedIndex : null, keyword));
                if (!TryGetInteger(header, keyword, out long axisLength) || axisLength < 0)
                    diagnostics.Add(new FitsValidationDiagnostic("FITS.Axis", FitsValidationSeverity.Error,
                        $"{keyword} must be a nonnegative integer.", hduIndex, null, keyword));
            }
        }
        else
            diagnostics.Add(new FitsValidationDiagnostic("FITS.Axis", FitsValidationSeverity.Error,
                "NAXIS must be an integer from zero through 999.", hduIndex, null, "NAXIS"));

        if (!TryGetInteger(header, "BITPIX", out long bitPix) || bitPix is not (8 or 16 or 32 or 64 or -32 or -64))
            diagnostics.Add(new FitsValidationDiagnostic("FITS.BitPix", FitsValidationSeverity.Error,
                "BITPIX is missing or unsupported.", hduIndex, null, "BITPIX"));

        HashSet<string> repeatable = new(StringComparer.OrdinalIgnoreCase) { "COMMENT", "HISTORY", "CONTINUE", string.Empty };
        foreach (IGrouping<string, (FitsHeaderCard Card, int Index)> group in header.Select((card, index) => (card, index))
                     .GroupBy(item => item.card.Keyword, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() <= 1 || repeatable.Contains(group.Key)) continue;
            foreach ((FitsHeaderCard card, int index) in group.Skip(1))
                diagnostics.Add(new FitsValidationDiagnostic("FITS.DuplicateKeyword", FitsValidationSeverity.Error,
                    $"Keyword '{card.Keyword}' occurs more than once.", hduIndex, index, card.Keyword));
        }

        for (int index = 0; index < header.Count; index++)
        {
            string keyword = header[index].Keyword;
            bool valid = keyword.StartsWith("HIERARCH ", StringComparison.Ordinal)
                ? keyword.Length > 9 && keyword[9..].All(character => character is >= ' ' and <= '~')
                : keyword.Length is > 0 and <= 8 && keyword.All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_');
            if (!valid)
                diagnostics.Add(new FitsValidationDiagnostic("FITS.KeywordSyntax", FitsValidationSeverity.Warning,
                    $"Keyword '{keyword}' does not use standard FITS keyword syntax.", hduIndex, index, keyword));
        }

        if (type is FitsHduType.AsciiTable or FitsHduType.BinaryTable)
        {
            if (naxis != 2)
                diagnostics.Add(new FitsValidationDiagnostic("FITS.TableShape", FitsValidationSeverity.Error,
                    "A FITS table must have NAXIS = 2.", hduIndex, null, "NAXIS"));
            if (bitPix != 8)
                diagnostics.Add(new FitsValidationDiagnostic("FITS.TableShape", FitsValidationSeverity.Error,
                    "A FITS table must have BITPIX = 8.", hduIndex, null, "BITPIX"));
            if (!TryGetInteger(header, "PCOUNT", out long pcount) || pcount < 0 ||
                !TryGetInteger(header, "GCOUNT", out long gcount) || gcount != 1)
                diagnostics.Add(new FitsValidationDiagnostic("FITS.TableShape", FitsValidationSeverity.Error,
                    "A FITS table requires PCOUNT >= 0 and GCOUNT = 1.", hduIndex));
            if (!TryGetInteger(header, "TFIELDS", out long fields) || fields is < 0 or > 999)
                diagnostics.Add(new FitsValidationDiagnostic("FITS.TableFields", FitsValidationSeverity.Error,
                    "TFIELDS must be an integer from zero through 999.", hduIndex, null, "TFIELDS"));
        }
    }

    private static void ValidatePayload(FitsHdu hdu, byte[] padding, int hduIndex,
        ICollection<FitsValidationDiagnostic> diagnostics)
    {
        byte expectedPadding = hdu.Type == FitsHduType.AsciiTable ? (byte)' ' : (byte)0;
        if (padding.Any(value => value != expectedPadding))
            diagnostics.Add(new FitsValidationDiagnostic("FITS.Padding", FitsValidationSeverity.Warning,
                $"The HDU data padding contains bytes other than {(expectedPadding == 0 ? "zero" : "ASCII spaces")}.", hduIndex));
        if (hdu.DataChecksumValid == false)
            diagnostics.Add(new FitsValidationDiagnostic("FITS.DataChecksum", FitsValidationSeverity.Error,
                "The DATASUM value does not match the HDU data records.", hduIndex, null, "DATASUM"));
        if (hdu.ChecksumValid == false)
            diagnostics.Add(new FitsValidationDiagnostic("FITS.Checksum", FitsValidationSeverity.Error,
                "The CHECKSUM value does not match the complete HDU.", hduIndex, null, "CHECKSUM"));

        if (hdu.Type == FitsHduType.BinaryTable && TryGetInteger(hdu.Header, "THEAP", out long heapStart))
        {
            long rowsEnd = checked(hdu.Header.GetValue<long>("NAXIS1", 0) * hdu.Header.GetValue<long>("NAXIS2", 0));
            if (heapStart < rowsEnd || heapStart > hdu.Data.LongLength)
                diagnostics.Add(new FitsValidationDiagnostic("FITS.HeapBounds", FitsValidationSeverity.Error,
                    "THEAP points outside the declared binary-table heap.", hduIndex, null, "THEAP"));
        }
    }

    private static void ThrowIfStrict(FitsReaderOptions options, IEnumerable<FitsValidationDiagnostic> diagnostics, int hduIndex)
    {
        if (options.ValidationMode != FitsValidationMode.Strict) return;
        FitsValidationDiagnostic? error = diagnostics.FirstOrDefault(item => item.HduIndex == hduIndex && item.Severity == FitsValidationSeverity.Error);
        if (error is not null)
            throw new FitsFormatException($"FITS conformance error: {error.Message}");
    }

    private static bool TryGetInteger(FitsHeader header, string keyword, out long value)
    {
        object? raw = header.FindFirst(keyword)?.Value;
        if (raw is long integer)
        {
            value = integer;
            return true;
        }
        return long.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out value);
    }

    private static long GetDataLength(FitsHeader header, FitsHduType type, FitsReaderOptions options)
    {
        int naxis = checked((int)header.GetValue<long>("NAXIS", 0));
        if (naxis < 0 || naxis > 999 || naxis > options.MaximumAxisCount) throw new FitsFormatException("NAXIS exceeds the configured limit or is invalid.");
        long pcount = header.GetValue<long>("PCOUNT", 0);
        long gcount = header.GetValue<long>("GCOUNT", 1);
        if (pcount < 0 || gcount < 1) throw new FitsFormatException("Invalid PCOUNT or GCOUNT value.");
        if (type is FitsHduType.AsciiTable or FitsHduType.BinaryTable)
        {
            long rowLength = header.GetValue<long>("NAXIS1", 0);
            long rowCount = header.GetValue<long>("NAXIS2", 0);
            long fieldCount = header.GetValue<long>("TFIELDS", 0);
            if (rowLength < 0 || rowCount < 0 || fieldCount < 0) throw new FitsFormatException("A FITS table has a negative size.");
            if (fieldCount > options.MaximumTableCells || checked(rowCount * fieldCount) > options.MaximumTableCells)
                throw new FitsFormatException("A FITS table exceeds the configured table cell limit.");
            return checked(rowLength * rowCount + pcount);
        }
        if (naxis == 0) return 0;
        int bitPix = checked((int)header.GetValue<long>("BITPIX"));
        if (bitPix is not (8 or 16 or 32 or 64 or -32 or -64)) throw new FitsFormatException("Unsupported BITPIX value.");
        long elements = type == FitsHduType.RandomGroups ? 1 : 1;
        int start = type == FitsHduType.RandomGroups ? 2 : 1;
        for (int axis = start; axis <= naxis; axis++)
        {
            long length = header.GetValue<long>($"NAXIS{axis}");
            if (length < 0) throw new FitsFormatException("FITS axis lengths cannot be negative.");
            elements = checked(elements * length);
        }
        return checked((elements + pcount) * gcount * Math.Abs(bitPix) / 8);
    }

    private static object? ParseContent(FitsHdu hdu, FitsReaderOptions options, CancellationToken cancellationToken)
    {
        if (hdu.Type is FitsHduType.Primary or FitsHduType.Image)
        {
            int naxis = checked((int)hdu.Header.GetValue<long>("NAXIS", 0));
            if (naxis == 0) return null;
            int[] dimensions = Enumerable.Range(1, naxis).Select(axis => checked((int)hdu.Header.GetValue<long>($"NAXIS{axis}"))).ToArray();
            return new FitsImage(checked((int)hdu.Header.GetValue<long>("BITPIX")), dimensions, hdu.Data,
                hdu.Header.GetValue<double>("BSCALE", 1), hdu.Header.GetValue<double>("BZERO", 0), hdu.Header.GetValue<long?>("BLANK"));
        }
        if (hdu.Type == FitsHduType.BinaryTable) return ParseBinaryTable(hdu.Header, hdu.Data, options, cancellationToken);
        if (hdu.Type == FitsHduType.AsciiTable) return ParseAsciiTable(hdu.Header, hdu.Data, options, cancellationToken);
        return null;
    }

    private static FitsTable ParseBinaryTable(FitsHeader header, byte[] data, FitsReaderOptions options, CancellationToken cancellationToken)
    {
        int fieldCount = checked((int)header.GetValue<long>("TFIELDS"));
        int rowLength = checked((int)header.GetValue<long>("NAXIS1"));
        int rowCount = checked((int)header.GetValue<long>("NAXIS2"));
        int heapStart = checked((int)header.GetValue<long>("THEAP", checked((long)rowLength * rowCount)));
        if (heapStart < checked(rowLength * rowCount) || heapStart > data.Length)
            throw new FitsFormatException("The binary table heap offset is outside the declared heap.");
        if (checked((long)rowCount * fieldCount) > options.MaximumTableCells)
            throw new FitsFormatException("A FITS table exceeds the configured table cell limit.");
        List<FitsTableColumn> columns = [];
        List<BinaryFormat> formats = [];
        for (int field = 1; field <= fieldCount; field++)
        {
            string format = header.GetValue<string>($"TFORM{field}") ?? throw new FitsFormatException($"Missing TFORM{field}.");
            formats.Add(ParseBinaryFormat(format));
            columns.Add(new FitsTableColumn(header.GetValue<string>($"TTYPE{field}", $"Column{field}")!, format,
                header.GetValue<string>($"TUNIT{field}"), header.GetValue<double>($"TSCAL{field}", 1),
                header.GetValue<double>($"TZERO{field}", 0), header.GetValue<long?>($"TNULL{field}")));
        }
        if (formats.Aggregate(0L, (width, format) => checked(width + format.Width)) > rowLength) throw new FitsFormatException("Binary table fields exceed NAXIS1.");
        List<IReadOnlyList<object?>> rows = [];
        for (int row = 0; row < rowCount; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int offset = row * rowLength;
            List<object?> cells = [];
            for (int field = 0; field < formats.Count; field++)
            {
                BinaryFormat format = formats[field];
                cells.Add(ReadBinaryValue(data.AsSpan(offset, format.Width), format, data, heapStart, columns[field]));
                offset += format.Width;
            }
            rows.Add(cells);
        }
        return new FitsTable(columns, rows);
    }

    private static BinaryFormat ParseBinaryFormat(string text)
    {
        Match match = BinaryTableFormatRegex().Match(text.Trim().ToUpperInvariant());
        if (!match.Success) throw new FitsFormatException($"Unsupported binary-table format '{text}'.");
        int repeat = match.Groups[1].Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 1;
        char code = match.Groups[2].Value[0];
        char? element = match.Groups[3].Success ? match.Groups[3].Value[0] : null;
        int scalarWidth = code switch { 'L' or 'B' or 'A' => 1, 'X' => 0, 'I' => 2, 'J' or 'E' => 4, 'K' or 'D' or 'C' or 'P' => 8, 'M' or 'Q' => 16, _ => throw new FitsFormatException($"Unsupported binary-table code '{code}'.") };
        int width = code == 'X' ? (repeat + 7) / 8 : checked(repeat * scalarWidth);
        return new BinaryFormat(repeat, code, element, width);
    }

    private static object? ReadBinaryValue(ReadOnlySpan<byte> field, BinaryFormat format, byte[] data, int heapStart, FitsTableColumn column)
    {
        if (format.Code is 'P' or 'Q')
        {
            long count = format.Code == 'P' ? BinaryPrimitives.ReadInt32BigEndian(field) : BinaryPrimitives.ReadInt64BigEndian(field);
            long offset = format.Code == 'P' ? BinaryPrimitives.ReadInt32BigEndian(field[4..]) : BinaryPrimitives.ReadInt64BigEndian(field[8..]);
            if (count < 0 || offset < 0) throw new FitsFormatException("Invalid variable-length array descriptor.");
            BinaryFormat nested = ParseBinaryFormat($"{count}{format.Element ?? 'B'}");
            long position = checked(heapStart + offset);
            if (checked(position + nested.Width) > data.LongLength) throw new FitsFormatException("Variable-length array points outside the table heap.");
            return ReadBinaryValue(data.AsSpan(checked((int)position), nested.Width), nested, data, heapStart, column);
        }
        if (format.Code == 'A') return Encoding.ASCII.GetString(field).TrimEnd();
        if (format.Code == 'L')
        {
            bool[] logical = new bool[format.Repeat];
            for (int index = 0; index < logical.Length; index++) logical[index] = field[index] == 'T';
            return logical.Length == 1 ? logical[0] : logical;
        }
        if (format.Code == 'X')
        {
            bool[] bits = new bool[format.Repeat];
            for (int index = 0; index < bits.Length; index++) bits[index] = (field[index / 8] & (1 << (7 - index % 8))) != 0;
            return bits;
        }
        List<object?> values = [];
        int size = format.Width / format.Repeat;
        for (int index = 0; index < format.Repeat; index++)
        {
            ReadOnlySpan<byte> value = field.Slice(index * size, size);
            object raw;
            if (format.Code == 'B') raw = value[0];
            else if (format.Code == 'I') raw = BinaryPrimitives.ReadInt16BigEndian(value);
            else if (format.Code == 'J') raw = BinaryPrimitives.ReadInt32BigEndian(value);
            else if (format.Code == 'K') raw = BinaryPrimitives.ReadInt64BigEndian(value);
            else if (format.Code == 'E') raw = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(value));
            else if (format.Code == 'D') raw = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(value));
            else if (format.Code == 'C') raw = new Complex(BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(value)), BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(value[4..])));
            else if (format.Code == 'M') raw = new Complex(BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(value)), BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(value[8..])));
            else throw new FitsFormatException($"Unsupported binary-table code '{format.Code}'.");
            if (raw is IConvertible && format.Code is not ('C' or 'M'))
            {
                bool integerCode = format.Code is 'B' or 'I' or 'J' or 'K';
                if (column.Null.HasValue && integerCode && Convert.ToInt64(raw, CultureInfo.InvariantCulture) == column.Null.Value)
                    values.Add(null);
                else if (column.Scale != 1 || column.Zero != 0)
                    values.Add(Convert.ToDouble(raw, CultureInfo.InvariantCulture) * column.Scale + column.Zero);
                else values.Add(raw);
            }
            else values.Add(raw);
        }
        return Collapse(values.ToArray());
    }

    private static object? Collapse(object?[] values) => values.Length == 1 ? values[0] : ToTypedArray(values);

    private static object ToTypedArray(object?[] values)
    {
        if (values.All(value => value is byte)) return values.Cast<byte>().ToArray();
        if (values.All(value => value is short)) return values.Cast<short>().ToArray();
        if (values.All(value => value is int)) return values.Cast<int>().ToArray();
        if (values.All(value => value is long)) return values.Cast<long>().ToArray();
        if (values.All(value => value is double)) return values.Cast<double>().ToArray();
        if (values.All(value => value is Complex)) return values.Cast<Complex>().ToArray();
        return values;
    }

    private static FitsTable ParseAsciiTable(FitsHeader header, byte[] data, FitsReaderOptions options, CancellationToken cancellationToken)
    {
        int fields = checked((int)header.GetValue<long>("TFIELDS"));
        int rowLength = checked((int)header.GetValue<long>("NAXIS1"));
        int rowCount = checked((int)header.GetValue<long>("NAXIS2"));
        if (checked((long)rowCount * fields) > options.MaximumTableCells)
            throw new FitsFormatException("A FITS table exceeds the configured table cell limit.");
        List<FitsTableColumn> columns = [];
        List<(int Start, int Width, char Code)> layouts = [];
        for (int field = 1; field <= fields; field++)
        {
            string format = header.GetValue<string>($"TFORM{field}") ?? throw new FitsFormatException($"Missing TFORM{field}.");
            Match match = AsciiTableFormatRegex().Match(format.Trim().ToUpperInvariant());
            if (!match.Success) throw new FitsFormatException($"Unsupported ASCII-table format '{format}'.");
            layouts.Add((checked((int)header.GetValue<long>($"TBCOL{field}")) - 1, int.Parse(match.Groups[2].Value), match.Groups[1].Value[0]));
            columns.Add(new FitsTableColumn(header.GetValue<string>($"TTYPE{field}", $"Column{field}")!, format, header.GetValue<string>($"TUNIT{field}"),
                header.GetValue<double>($"TSCAL{field}", 1), header.GetValue<double>($"TZERO{field}", 0), header.GetValue<long?>($"TNULL{field}")));
        }
        List<IReadOnlyList<object?>> rows = [];
        for (int row = 0; row < rowCount; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<object?> cells = [];
            for (int field = 0; field < fields; field++)
            {
                (int start, int width, char code) = layouts[field];
                string value = Encoding.ASCII.GetString(data, row * rowLength + start, width).Trim();
                object? parsed = value.Length == 0 ? null : code switch
                {
                    'A' => value, 'I' => long.Parse(value, CultureInfo.InvariantCulture),
                    _ => double.Parse(value.Replace('D', 'E'), NumberStyles.Float, CultureInfo.InvariantCulture)
                };
                cells.Add(parsed is IConvertible && parsed is not string ? Convert.ToDouble(parsed, CultureInfo.InvariantCulture) * columns[field].Scale + columns[field].Zero : parsed);
            }
            rows.Add(cells);
        }
        return new FitsTable(columns, rows);
    }

    private static bool? ValidateDataChecksum(FitsHeader header, byte[] data, byte[] padding, uint? dataSum)
    {
        string? expected = header.GetValue<string>("DATASUM");
        if (expected is null) return null;
        return uint.TryParse(expected, NumberStyles.None, CultureInfo.InvariantCulture, out uint sum) && (dataSum ?? FitsChecksum.Sum(data, padding)) == sum;
    }

    private static bool? ValidateChecksum(FitsHeader header, byte[] headerBytes, byte[] data, byte[] padding,
        uint? dataSum)
    {
        if (header.FindFirst("CHECKSUM") is null) return null;
        return FitsChecksum.Add(FitsChecksum.Sum(headerBytes), dataSum ?? FitsChecksum.Sum(data, padding)) == uint.MaxValue;
    }

    private static int Padding(long length) => checked((int)((BlockSize - length % BlockSize) % BlockSize));

    private static byte[] ReadExactly(Stream stream, int length)
    {
        // ReadExactly fills every byte before the buffer can escape this method.
        byte[] result = GC.AllocateUninitializedArray<byte>(length);
        stream.ReadExactly(result);
        return result;
    }

    private sealed record BinaryFormat(int Repeat, char Code, char? Element, int Width);

    [GeneratedRegex(@"^(\d*)([LXBIJKAEDCMPQ])([LXBIJKAEDCM])?(?:\(\d+\))?$")]
    private static partial Regex BinaryTableFormatRegex();

    [GeneratedRegex(@"^([AIFED])(\d+)(?:\.\d+)?(?:E\d+)?$")]
    private static partial Regex AsciiTableFormatRegex();
}

internal static class FitsChecksum
{
    internal static uint Sum(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        int alignedLength = first.Length & ~3;
        uint sum = Sum(first[..alignedLength]);
        int remainder = first.Length - alignedLength;
        if (remainder == 0)
            return Add(sum, Sum(second));

        Span<byte> crossingWord = stackalloc byte[4];
        crossingWord.Clear();
        first[alignedLength..].CopyTo(crossingWord);
        int fromSecond = Math.Min(4 - remainder, second.Length);
        second[..fromSecond].CopyTo(crossingWord[remainder..]);
        sum = Add(sum, BinaryPrimitives.ReadUInt32BigEndian(crossingWord));
        return Add(sum, Sum(second[fromSecond..]));
    }

    internal static uint Sum(ReadOnlySpan<byte> bytes)
    {
        ulong sum = 0;
        int index = 0;
        if (Vector256.IsHardwareAccelerated && BitConverter.IsLittleEndian)
        {
            // Widen before accumulating: 32-bit lanes would lose end-around carries.
            // Even the largest supported span's entire word sum fits in 64 bits.
            Vector256<ulong> accumulator = Vector256<ulong>.Zero;
            Vector256<byte> swap = Vector256.Create(0x00010203u, 0x04050607u, 0x08090a0bu, 0x0c0d0e0fu,
                0x10111213u, 0x14151617u, 0x18191a1bu, 0x1c1d1e1fu).AsByte();
            for (; index <= bytes.Length - Vector256<byte>.Count; index += Vector256<byte>.Count)
            {
                var words = Vector256.Shuffle(Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(bytes), (nuint)index), swap).AsUInt32();
                var (lower, upper) = Vector256.Widen(words);
                accumulator += lower + upper;
            }
            sum = Vector256.Sum(accumulator);
        }
        while (index <= bytes.Length - 4)
        {
            sum += BinaryPrimitives.ReadUInt32BigEndian(bytes[index..]);
            index += 4;
        }
        if (index < bytes.Length)
        {
            Span<byte> tail = stackalloc byte[4];
            tail.Clear();
            bytes[index..].CopyTo(tail);
            sum += BinaryPrimitives.ReadUInt32BigEndian(tail);
        }
        while (sum >> 32 != 0) sum = (sum & uint.MaxValue) + (sum >> 32);
        return (uint)sum;
    }

    internal static uint Add(uint left, uint right)
    {
        ulong sum = (ulong)left + right;
        while (sum >> 32 != 0) sum = (sum & uint.MaxValue) + (sum >> 32);
        return (uint)sum;
    }
}
