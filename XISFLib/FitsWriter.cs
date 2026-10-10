using System.Globalization;
using System.Text;

namespace XISFLib;

/// <summary>Controls optional FITS checksum generation.</summary>
public sealed class FitsWriterOptions
{
    /// <summary>Whether DATASUM and CHECKSUM cards are generated for each HDU.</summary>
    public bool WriteChecksums { get; set; }
}

/// <summary>Serializes FITS documents with optional HDU checksums.</summary>
public static class FitsWriter
{
    private const int BlockSize = 2880;
    private static readonly HashSet<string> StructuralKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SIMPLE", "XTENSION", "BITPIX", "NAXIS", "PCOUNT", "GCOUNT", "EXTEND", "CHECKSUM", "DATASUM",
        "BSCALE", "BZERO", "BLANK"
    };

    /// <summary>Serializes a document or images to the supplied file or stream.</summary>
    /// <param name="path">The destination file path.</param>
    /// <param name="document">The document to read, validate, sign, or serialize.</param>
    /// <param name="options">The operation settings, or null to use defaults.</param>
    /// <remarks>File output is staged beside the destination and committed only after successful serialization.</remarks>
    public static void Write(string path, FitsDocument document, FitsWriterOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        AtomicFile.Write(path, stream => Write(stream, document, options));
    }

    /// <summary>Serializes FITS on a worker thread and checks cancellation before replacing the destination.</summary>
    /// <param name="path">The destination file path.</param>
    /// <param name="document">The document to read, validate, sign, or serialize.</param>
    /// <param name="options">The operation settings, or null to use defaults.</param>
    /// <param name="cancellationToken">A token observed at the documented cooperative cancellation boundaries.</param>
    /// <returns>A task completing after the staged file is written and committed.</returns>
    public static Task WriteAsync(string path, FitsDocument document, FitsWriterOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            AtomicFile.Write(path, stream =>
            {
                Write(stream, document, options);
                cancellationToken.ThrowIfCancellationRequested();
            });
        }, cancellationToken);

    /// <summary>Serializes a document or images to the supplied file or stream.</summary>
    /// <param name="stream">The caller-owned stream, positioned at the file origin; it remains open.</param>
    /// <param name="document">The document to read, validate, sign, or serialize.</param>
    /// <param name="options">The operation settings, or null to use defaults.</param>
    /// <remarks>The caller owns the stream. An error can leave partial output.</remarks>
    public static void Write(Stream stream, FitsDocument document, FitsWriterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(document);
        if (!stream.CanWrite) throw new ArgumentException("The FITS stream must be writable.", nameof(stream));
        options ??= new FitsWriterOptions();
        for (int index = 0; index < document.Hdus.Count; index++)
            WriteHdu(stream, document.Hdus[index], index == 0, index < document.Hdus.Count - 1, options);
    }

    private static void WriteHdu(Stream stream, FitsHdu hdu, bool first, bool hasExtensions, FitsWriterOptions options)
    {
        if (first && hdu.Type != FitsHduType.Primary) throw new FitsFormatException("The first HDU must be primary.");
        byte[] data = hdu.Content is FitsImage image ? image.RawData : hdu.Data;
        List<FitsHeaderCard> cards = BuildCards(hdu, first, hasExtensions, data);
        byte[] padding = new byte[(BlockSize - data.Length % BlockSize) % BlockSize];
        if (hdu.Type == FitsHduType.AsciiTable) padding.AsSpan().Fill((byte)' ');
        if (options.WriteChecksums)
        {
            uint dataSum = FitsChecksum.Sum(data, padding);
            cards.Add(new FitsHeaderCard("DATASUM", dataSum.ToString(CultureInfo.InvariantCulture), "data unit checksum"));
            cards.Add(new FitsHeaderCard("CHECKSUM", "0000000000000000", "HDU checksum"));
            byte[] placeholder = SerializeHeader(cards);
            uint sum = FitsChecksum.Add(FitsChecksum.Sum(placeholder), dataSum);
            string? encoded = null;
            for (int variant = 0; variant < 2 && encoded is null; variant++)
            for (int rotation = 0; rotation < 16 && encoded is null; rotation++)
            {
                string candidate = EncodeChecksum(~sum, variant == 0, rotation);
                cards[^1] = cards[^1] with { Value = candidate };
                if (FitsChecksum.Add(FitsChecksum.Sum(SerializeHeader(cards)), dataSum) == uint.MaxValue) encoded = candidate;
            }
            if (encoded is null) throw new FitsFormatException("Unable to encode the FITS HDU checksum.");
            cards[^1] = cards[^1] with { Value = encoded };
        }
        byte[] header = SerializeHeader(cards);
        if (stream is MemoryStream memory)
        {
            // Reserve this complete HDU together so the final padding write does
            // not force a second image-sized expansion. Retain geometric growth.
            long required = memory.Position + header.Length + (long)data.Length + padding.Length;
            if (required <= int.MaxValue && required > memory.Capacity)
                memory.Capacity = (int)Math.Max(required, Math.Min(int.MaxValue, (long)memory.Capacity * 2));
        }
        stream.Write(header);
        stream.Write(data);
        stream.Write(padding);
    }

    private static List<FitsHeaderCard> BuildCards(FitsHdu hdu, bool first, bool hasExtensions, byte[] data)
    {
        List<FitsHeaderCard> cards = [];
        FitsImage? image = hdu.Content as FitsImage;
        if (first)
        {
            cards.Add(new FitsHeaderCard("SIMPLE", true, "conforms to FITS standard"));
            AddImageStructure(cards, image);
            if (hasExtensions) cards.Add(new FitsHeaderCard("EXTEND", true));
        }
        else
        {
            string extension = hdu.Type switch
            {
                FitsHduType.Image => "IMAGE",
                FitsHduType.AsciiTable => "TABLE",
                FitsHduType.BinaryTable => "BINTABLE",
                _ => hdu.Header.GetValue<string>("XTENSION", "UNKNOWN")!
            };
            cards.Add(new FitsHeaderCard("XTENSION", extension));
            if (image is not null) AddImageStructure(cards, image, true);
            else AddExtensionStructure(cards, hdu, data);
        }

        foreach (FitsHeaderCard card in hdu.Header)
        {
            if (IsStructural(card.Keyword)) continue;
            cards.Add(card);
        }
        return cards;
    }

    private static void AddImageStructure(List<FitsHeaderCard> cards, FitsImage? image, bool extension = false)
    {
        cards.Add(new FitsHeaderCard("BITPIX", image?.BitPix ?? 8));
        cards.Add(new FitsHeaderCard("NAXIS", image?.Dimensions.Count ?? 0));
        if (image is not null)
        {
            for (int axis = 0; axis < image.Dimensions.Count; axis++) cards.Add(new FitsHeaderCard($"NAXIS{axis + 1}", image.Dimensions[axis]));
            if (image.BScale != 1) cards.Add(new FitsHeaderCard("BSCALE", image.BScale));
            if (image.BZero != 0) cards.Add(new FitsHeaderCard("BZERO", image.BZero));
            if (image.Blank.HasValue) cards.Add(new FitsHeaderCard("BLANK", image.Blank.Value));
        }
        if (extension)
        {
            cards.Add(new FitsHeaderCard("PCOUNT", 0));
            cards.Add(new FitsHeaderCard("GCOUNT", 1));
        }
    }

    private static void AddExtensionStructure(List<FitsHeaderCard> cards, FitsHdu hdu, byte[] data)
    {
        if (hdu.Type is FitsHduType.BinaryTable or FitsHduType.AsciiTable)
        {
            cards.Add(new FitsHeaderCard("BITPIX", 8));
            cards.Add(new FitsHeaderCard("NAXIS", 2));
            cards.Add(new FitsHeaderCard("NAXIS1", hdu.Header.GetValue<long>("NAXIS1")));
            cards.Add(new FitsHeaderCard("NAXIS2", hdu.Header.GetValue<long>("NAXIS2")));
            cards.Add(new FitsHeaderCard("PCOUNT", hdu.Header.GetValue<long>("PCOUNT", 0)));
            cards.Add(new FitsHeaderCard("GCOUNT", 1));
            return;
        }
        int bitPix = checked((int)hdu.Header.GetValue<long>("BITPIX", 8));
        int naxis = checked((int)hdu.Header.GetValue<long>("NAXIS", data.Length == 0 ? 0 : 1));
        cards.Add(new FitsHeaderCard("BITPIX", bitPix));
        cards.Add(new FitsHeaderCard("NAXIS", naxis));
        for (int axis = 1; axis <= naxis; axis++)
            cards.Add(new FitsHeaderCard($"NAXIS{axis}", hdu.Header.GetValue<long>($"NAXIS{axis}", axis == 1 ? data.Length : 1)));
        cards.Add(new FitsHeaderCard("PCOUNT", hdu.Header.GetValue<long>("PCOUNT", 0)));
        cards.Add(new FitsHeaderCard("GCOUNT", hdu.Header.GetValue<long>("GCOUNT", 1)));
    }

    private static bool IsStructural(string keyword) => StructuralKeywords.Contains(keyword) ||
        keyword.StartsWith("NAXIS", StringComparison.OrdinalIgnoreCase) && keyword.Length > 5;

    private static byte[] SerializeHeader(IReadOnlyList<FitsHeaderCard> cards)
    {
        List<string> records = [];
        foreach (FitsHeaderCard card in cards) records.AddRange(FormatCard(card));
        records.Add("END".PadRight(80));
        while (records.Count % 36 != 0) records.Add(new string(' ', 80));
        return Encoding.ASCII.GetBytes(string.Concat(records));
    }

    private static IEnumerable<string> FormatCard(FitsHeaderCard card)
    {
        string keyword = card.Keyword.ToUpperInvariant();
        if (keyword is "COMMENT" or "HISTORY")
        {
            foreach (string part in SplitText(Convert.ToString(card.Value, CultureInfo.InvariantCulture) ?? "", 72))
                yield return (keyword.PadRight(8) + part).PadRight(80);
            yield break;
        }
        bool hierarch = keyword.StartsWith("HIERARCH ", StringComparison.Ordinal) || keyword.Length > 8 || keyword.Any(character => character is not (>= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_'));
        string prefix = hierarch ? (keyword.StartsWith("HIERARCH ") ? keyword : "HIERARCH " + keyword) + " = " : keyword.PadRight(8) + "= ";
        if (card.Value is string text)
        {
            string escaped = text.Replace("'", "''", StringComparison.Ordinal);
            int capacity = Math.Max(8, 80 - prefix.Length - 3);
            List<string> segments = SplitText(escaped, Math.Min(60, capacity)).ToList();
            for (int i = 0; i < segments.Count; i++)
            {
                bool more = i < segments.Count - 1;
                string currentPrefix = i == 0 ? prefix : "CONTINUE  ";
                string value = $"'{segments[i]}{(more ? "&" : "")}'";
                string comment = !more && card.Comment is not null ? " / " + card.Comment : "";
                yield return Fit(currentPrefix + value + comment);
            }
            yield break;
        }
        string formatted = card.Value switch
        {
            bool logical => logical ? "T" : "F",
            null => "",
            float number => number.ToString("G9", CultureInfo.InvariantCulture),
            double number => number.ToString("G17", CultureInfo.InvariantCulture),
            IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
            _ => Convert.ToString(card.Value, CultureInfo.InvariantCulture) ?? ""
        };
        string rightAligned = formatted.Length <= 20 ? formatted.PadLeft(20) : formatted;
        yield return Fit(prefix + rightAligned + (card.Comment is null ? "" : " / " + card.Comment));
    }

    private static IEnumerable<string> SplitText(string value, int length)
    {
        if (value.Length == 0) { yield return ""; yield break; }
        for (int offset = 0; offset < value.Length; offset += length)
            yield return value.Substring(offset, Math.Min(length, value.Length - offset));
    }

    private static string Fit(string text) => text.Length <= 80 ? text.PadRight(80) : text[..80];

    private static string EncodeChecksum(uint complement, bool transposed, int rotation)
    {
        Span<byte> bytes = stackalloc byte[4];
        bytes[0] = (byte)(complement >> 24);
        bytes[1] = (byte)(complement >> 16);
        bytes[2] = (byte)(complement >> 8);
        bytes[3] = (byte)complement;
        Span<char> encoded = stackalloc char[16];
        for (int i = 0; i < 4; i++)
        {
            int quotient = bytes[i] / 4 + 48;
            int remainder = bytes[i] % 4;
            for (int j = 0; j < 4; j++)
            {
                int index = transposed ? 4 * j + i : 4 * i + j;
                encoded[index] = (char)(quotient + (j == 0 ? remainder : 0));
            }
        }
        if (transposed)
        {
            for (int byteIndex = 0; byteIndex < 4; byteIndex++)
            for (int pair = 0; pair < 4; pair += 2)
            {
                int first = 4 * pair + byteIndex;
                int second = 4 * (pair + 1) + byteIndex;
                while (IsPunctuation(encoded[first]) || IsPunctuation(encoded[second])) { encoded[first]++; encoded[second]--; }
            }
        }
        else
        {
            for (int i = 0; i < 16; i += 2)
                while (IsPunctuation(encoded[i]) || IsPunctuation(encoded[i + 1])) { encoded[i]++; encoded[i + 1]--; }
        }
        char[] result = new char[16];
        for (int i = 0; i < result.Length; i++) result[(i + rotation) % result.Length] = encoded[i];
        return new string(result);
    }

    private static bool IsPunctuation(char character) => character is >= ':' and <= '@' or >= '[' and <= '`';
}
