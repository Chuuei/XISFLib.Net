using System.Globalization;
using System.Numerics;
using System.Text;

namespace XISFLib;

/// <summary>Generates property and table-cell display text according to XISF format tokens.</summary>
public static class XisfPropertyFormatter
{
    /// <summary>Formats a decoded property, applying its format to numeric collection components.</summary>
    /// <param name="property">The property to represent.</param>
    /// <returns>The readable representation; unit tokens remain available in the original format string.</returns>
    public static string Format(XisfProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);
        if (!property.IsAvailable) throw new XisfUnsupportedFeatureException(property.UnavailableReason ?? "Property unavailable.");
        return FormatValue(property.Value, property.Format);
    }

    /// <summary>Formats a scalar, complex value, vector, matrix, or table-cell value.</summary>
    /// <param name="value">The decoded value.</param>
    /// <param name="format">The optional semicolon-separated XISF format tokens.</param>
    /// <returns>A readable representation. Time points ignore format tokens.</returns>
    public static string FormatValue(object? value, string? format = null)
    {
        if (value is DateTimeOffset time) return time.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        if (value is DateTime date) return date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        Dictionary<string, string> tokens = new(StringComparer.Ordinal);
        foreach (string part in (format ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string token = string.Concat(part.Where(c => !char.IsWhiteSpace(c)));
            int separator = token.IndexOf(':');
            if (separator > 0) tokens[token[..separator]] = token[(separator + 1)..];
        }
        return Render(value, tokens);
    }
    private static string Render(object? value, Dictionary<string, string> tokens)
    {
        if (value is Complex complex) return $"({Render(complex.Real, tokens)}, {Render(complex.Imaginary, tokens)})";
        if (value is XisfComplex32 single) return $"({Render(single.Real, tokens)}, {Render(single.Imaginary, tokens)})";
        if (value is XisfComplex128 quad) return $"({Render(quad.Real, tokens)}, {Render(quad.Imaginary, tokens)})";
        if (value is Array array)
        {
            if (array.Rank == 1) return "[" + string.Join(", ", array.Cast<object>().Select(v => Render(v, tokens))) + "]";
            if (array.Rank != 2) throw new ArgumentException("XISF collections have rank one or two.", nameof(value));
            return "[" + string.Join("; ", Enumerable.Range(0, array.GetLength(0)).Select(row =>
                string.Join(", ", Enumerable.Range(0, array.GetLength(1)).Select(column => Render(array.GetValue(row, column), tokens))))) + "]";
        }
        string text;
        bool numeric = false;
        if (value is bool boolean) text = tokens.GetValueOrDefault("bool") == "numeric" ? boolean ? "1" : "0" : boolean ? "true" : "false";
        else if (value is sbyte or byte or short or ushort or int or uint or long or ulong or Int128 or UInt128 or BigInteger)
        {
            numeric = true;
            BigInteger integer = BigInteger.Parse(((IFormattable)value).ToString(null, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            int radix = tokens.GetValueOrDefault("base") switch { "bin" => 2, "oct" => 8, "hex" => 16, _ => 10 };
            BigInteger magnitude = BigInteger.Abs(integer);
            StringBuilder digits = new();
            do { magnitude = BigInteger.DivRem(magnitude, radix, out BigInteger remainder); digits.Insert(0, "0123456789abcdef"[(int)remainder]); } while (magnitude > 0);
            text = (integer.Sign < 0 ? "-" : "") + digits;
        }
        else if (value is float or double or decimal or XisfFloat128)
        {
            numeric = true;
            int precision = ReadCount(tokens, "precision", 6, 10000);
            string mode = tokens.GetValueOrDefault("float", "auto");
            if (value is XisfFloat128 quadValue) text = FormatExactDecimal(quadValue.ToString(), mode, precision);
            else if (value is decimal decimalValue) text = FormatExactDecimal(decimalValue.ToString(CultureInfo.InvariantCulture), mode, precision);
            else
            {
                double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                text = !double.IsFinite(number) ? double.IsNaN(number) ? "NaN" : number < 0 ? "-Inf" : "+Inf" :
                    number.ToString((mode == "fixed" ? "F" : mode == "scientific" ? "E" : "G") + (mode == "auto" ? Math.Max(1, precision) : precision), CultureInfo.InvariantCulture);
            }
            if (RepresentedZero(text)) text = text.TrimStart('-', '+');
        }
        else text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        if (numeric && tokens.GetValueOrDefault("sign") == "force" && !text.StartsWith('-') && !text.StartsWith('+') && text != "NaN" && !RepresentedZero(text)) text = "+" + text;
        int width = ReadCount(tokens, "width", 0, 1_000_000);
        int padding = width - text.EnumerateRunes().Count();
        if (padding <= 0) return text;
        string fillText = tokens.GetValueOrDefault("fill", " ");
        char fill = fillText.Length == 1 && fillText[0] <= 127 && fillText[0] != ';' ? fillText[0] : ' ';
        string alignment = tokens.GetValueOrDefault("align", "right");
        return alignment == "left" ? text + new string(fill, padding) : alignment == "center" ?
            new string(fill, (padding + 1) / 2) + text + new string(fill, padding / 2) : new string(fill, padding) + text;
    }
    private static int ReadCount(Dictionary<string, string> tokens, string name, int fallback, int maximum)
    {
        if (!tokens.TryGetValue(name, out string? text)) return fallback;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count > maximum)
            throw new ArgumentException($"Invalid or excessive {name} format count.");
        return count;
    }
    private static bool RepresentedZero(string text) => text.Split(['e', 'E'])[0].All(c => c is '0' or '.' or '-' or '+');
    private static string FormatExactDecimal(string text, string mode, int precision)
    {
        if (text is "NaN" or "+Inf" or "-Inf") return text;
        bool negative = text.StartsWith('-'); text = text.TrimStart('-', '+');
        int point = text.IndexOf('.');
        int fraction = point < 0 ? 0 : text.Length - point - 1;
        string digits = text.Replace(".", "", StringComparison.Ordinal).TrimStart('0');
        if (digits.Length == 0) digits = "0";
        BigInteger numerator = BigInteger.Parse(digits, CultureInfo.InvariantCulture), denominator = BigInteger.Pow(10, fraction);
        int exponent = numerator.IsZero ? 0 : digits.Length - fraction - 1;
        BigInteger Round(int decimals)
        {
            BigInteger n = decimals >= 0 ? numerator * BigInteger.Pow(10, decimals) : numerator;
            BigInteger d = decimals >= 0 ? denominator : denominator * BigInteger.Pow(10, -decimals);
            BigInteger q = BigInteger.DivRem(n, d, out BigInteger r);
            int comparison = (r * 2).CompareTo(d);
            return comparison > 0 || comparison == 0 && !q.IsEven ? q + 1 : q;
        }
        bool auto = mode != "fixed" && mode != "scientific";
        int significant = auto ? Math.Max(1, precision) : precision + 1;
        if (mode != "fixed")
        {
            BigInteger rounded = Round(significant - 1 - exponent);
            if (rounded.ToString(CultureInfo.InvariantCulture).Length > significant) exponent++;
        }
        bool scientific = mode == "scientific" || auto && (exponent < -4 || exponent >= significant);
        int decimals = scientific ? significant - 1 : auto ? Math.Max(0, significant - 1 - exponent) : precision;
        string result;
        if (scientific)
        {
            string significand = Round(significant - 1 - exponent).ToString(CultureInfo.InvariantCulture).PadLeft(significant, '0');
            result = decimals == 0 ? significand : significand.Insert(1, ".");
            if (auto) result = result.TrimEnd('0').TrimEnd('.');
            result += "e" + (exponent < 0 ? "-" : "+") + Math.Abs(exponent).ToString("D2", CultureInfo.InvariantCulture);
        }
        else
        {
            string fixedDigits = Round(decimals).ToString(CultureInfo.InvariantCulture).PadLeft(decimals + 1, '0');
            result = decimals == 0 ? fixedDigits : fixedDigits.Insert(fixedDigits.Length - decimals, ".");
            if (auto && decimals > 0) result = result.TrimEnd('0').TrimEnd('.');
        }
        return negative && !RepresentedZero(result) ? "-" + result : result;
    }
}
