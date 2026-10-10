using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Text.RegularExpressions;

namespace XISFLib;

public static partial class FitsReader
{
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

}
