using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Text.RegularExpressions;

namespace XISFLib;

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
        options = options?.Snapshot();
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
    public static Task<FitsDocument> LoadAsync(Stream stream, FitsReaderOptions? options = null, CancellationToken cancellationToken = default)
    {
        options = options?.Snapshot();
        return Task.Run(() =>
        {
            ArgumentNullException.ThrowIfNull(stream);
            using CancellationReadStream cancellable = new(stream, cancellationToken);
            FitsDocument result = LoadCore(cancellable, options, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }, cancellationToken);
    }

    private static FitsDocument LoadCore(Stream stream, FitsReaderOptions? options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead) throw new ArgumentException("The FITS stream must be readable.", nameof(stream));
        options = options?.Snapshot() ?? new FitsReaderOptions();
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
                uint? dataSum = header.GetValueOrDefault<string>("DATASUM") is not null || header.FindFirst("CHECKSUM") is not null
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
        ApiContracts.Defined(options.ValidationMode, nameof(options.ValidationMode));
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

    private static FitsHduType GetHduType(FitsHeader header, bool first)
    {
        if (first)
        {
            if (header.Count == 0 || !header[0].Keyword.Equals("SIMPLE", StringComparison.OrdinalIgnoreCase))
                throw new FitsFormatException("SIMPLE must be the first card of the primary HDU.");
            if (header.GetValueOrDefault<bool?>("SIMPLE") != true) throw new FitsFormatException("The primary HDU must begin with SIMPLE = T.");
            return header.GetValueOrDefault<bool?>("GROUPS") == true ? FitsHduType.RandomGroups : FitsHduType.Primary;
        }
        if (header.Count == 0 || !header[0].Keyword.Equals("XTENSION", StringComparison.OrdinalIgnoreCase))
            throw new FitsFormatException("XTENSION must be the first card of an extension HDU.");
        string extension = header.GetValueOrDefault<string>("XTENSION", "")!.Trim();
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
            long rowsEnd = checked(hdu.Header.GetValueOrDefault<long>("NAXIS1", 0) * hdu.Header.GetValueOrDefault<long>("NAXIS2", 0));
            if (heapStart < rowsEnd || heapStart > hdu.DataBuffer.LongLength)
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
        int naxis = checked((int)header.GetValueOrDefault<long>("NAXIS", 0));
        if (naxis < 0 || naxis > 999 || naxis > options.MaximumAxisCount) throw new FitsFormatException("NAXIS exceeds the configured limit or is invalid.");
        long pcount = header.GetValueOrDefault<long>("PCOUNT", 0);
        long gcount = header.GetValueOrDefault<long>("GCOUNT", 1);
        if (pcount < 0 || gcount < 1) throw new FitsFormatException("Invalid PCOUNT or GCOUNT value.");
        if (type is FitsHduType.AsciiTable or FitsHduType.BinaryTable)
        {
            long rowLength = header.GetValueOrDefault<long>("NAXIS1", 0);
            long rowCount = header.GetValueOrDefault<long>("NAXIS2", 0);
            long fieldCount = header.GetValueOrDefault<long>("TFIELDS", 0);
            if (rowLength < 0 || rowCount < 0 || fieldCount < 0) throw new FitsFormatException("A FITS table has a negative size.");
            if (fieldCount > options.MaximumTableCells || checked(rowCount * fieldCount) > options.MaximumTableCells)
                throw new FitsFormatException("A FITS table exceeds the configured table cell limit.");
            return checked(rowLength * rowCount + pcount);
        }
        if (naxis == 0) return 0;
        int bitPix = checked((int)header.GetValueOrDefault<long>("BITPIX"));
        if (bitPix is not (8 or 16 or 32 or 64 or -32 or -64)) throw new FitsFormatException("Unsupported BITPIX value.");
        long elements = type == FitsHduType.RandomGroups ? 1 : 1;
        int start = type == FitsHduType.RandomGroups ? 2 : 1;
        for (int axis = start; axis <= naxis; axis++)
        {
            long length = header.GetValueOrDefault<long>($"NAXIS{axis}");
            if (length < 0) throw new FitsFormatException("FITS axis lengths cannot be negative.");
            elements = checked(elements * length);
        }
        return checked((elements + pcount) * gcount * Math.Abs(bitPix) / 8);
    }

    private static object? ParseContent(FitsHdu hdu, FitsReaderOptions options, CancellationToken cancellationToken)
    {
        if (hdu.Type is FitsHduType.Primary or FitsHduType.Image)
        {
            int naxis = checked((int)hdu.Header.GetValueOrDefault<long>("NAXIS", 0));
            if (naxis == 0) return null;
            int[] dimensions = Enumerable.Range(1, naxis).Select(axis => checked((int)hdu.Header.GetValueOrDefault<long>($"NAXIS{axis}"))).ToArray();
            return new FitsImage(checked((int)hdu.Header.GetValueOrDefault<long>("BITPIX")), dimensions, hdu.DataBuffer,
                hdu.Header.GetValueOrDefault<double>("BSCALE", 1), hdu.Header.GetValueOrDefault<double>("BZERO", 0), hdu.Header.GetValueOrDefault<long?>("BLANK"));
        }
        if (hdu.Type == FitsHduType.BinaryTable) return ParseBinaryTable(hdu.Header, hdu.DataBuffer, options, cancellationToken);
        if (hdu.Type == FitsHduType.AsciiTable) return ParseAsciiTable(hdu.Header, hdu.DataBuffer, options, cancellationToken);
        return null;
    }

    private static bool? ValidateDataChecksum(FitsHeader header, byte[] data, byte[] padding, uint? dataSum)
    {
        string? expected = header.GetValueOrDefault<string>("DATASUM");
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

