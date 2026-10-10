using System.Globalization;
using System.Numerics;

namespace XISFLib;

/// <summary>An IEEE 754 binary128 value that preserves all 128 bits without converting through double.</summary>
/// <param name="Bits">The IEEE binary128 bit pattern.</param>
public readonly record struct XisfFloat128(UInt128 Bits)
{
    private static readonly UInt128 FractionMask = (UInt128.One << 112) - 1;
    /// <summary>Whether the exponent denotes an infinity or NaN.</summary>
    public bool IsFinite => ((Bits >> 112) & 0x7fff) != 0x7fff;
    /// <summary>Whether this value is NaN.</summary>
    public bool IsNaN => !IsFinite && (Bits & FractionMask) != 0;
    /// <summary>Parses decimal text using round-to-nearest, ties-to-even at binary128 precision.</summary>
    /// <param name="text">An XISF floating-point scalar.</param>
    /// <returns>The correctly rounded binary128 value.</returns>
    public static XisfFloat128 Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        text = text.Trim();
        bool negative = text.StartsWith('-');
        UInt128 sign = negative ? UInt128.One << 127 : 0;
        if (text is "NaN" or "nan" or "-nan") return new(sign | ((UInt128)0x7fff << 112) | (UInt128.One << 111));
        if (text is "Inf" or "+Inf" or "-Inf" or "inf" or "+inf" or "-inf") return new(sign | ((UInt128)0x7fff << 112));
        if (text.StartsWith('-') || text.StartsWith('+')) text = text[1..];
        string[] components = text.Split(['e', 'E']);
        if (components.Length > 2 || components[0].Length == 0) throw new FormatException("Invalid binary128 scalar.");
        int decimalExponent = components.Length == 2 ? int.Parse(components[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) : 0;
        string mantissa = components[0];
        int point = mantissa.IndexOf('.');
        if (point >= 0) { decimalExponent = checked(decimalExponent - (mantissa.Length - point - 1)); mantissa = mantissa.Remove(point, 1); }
        if (mantissa.Length == 0 || mantissa.Any(c => c is < '0' or > '9')) throw new FormatException("Invalid binary128 digits.");
        BigInteger numerator = BigInteger.Parse(mantissa, CultureInfo.InvariantCulture);
        if (numerator.IsZero) return new(sign);
        // These cutoffs are outside the binary128 range, and avoid constructing arbitrarily huge powers.
        long magnitude = (long)decimalExponent + mantissa.TrimStart('0').Length;
        if (magnitude > 5000) return new(sign | ((UInt128)0x7fff << 112));
        if (magnitude < -5000) return new(sign);
        BigInteger denominator = BigInteger.One;
        if (decimalExponent >= 0) numerator *= BigInteger.Pow(10, decimalExponent);
        else denominator = BigInteger.Pow(10, checked(-decimalExponent));
        int exponent = checked((int)(numerator.GetBitLength() - denominator.GetBitLength()));
        if (exponent >= 0 ? numerator < (denominator << exponent) : (numerator << -exponent) < denominator) exponent--;
        int shift = exponent < -16382 ? 16494 : 112 - exponent;
        BigInteger scaledNumerator = shift >= 0 ? numerator << shift : numerator;
        BigInteger scaledDenominator = shift < 0 ? denominator << -shift : denominator;
        BigInteger significand = BigInteger.DivRem(scaledNumerator, scaledDenominator, out BigInteger remainder);
        int comparison = (remainder << 1).CompareTo(scaledDenominator);
        if (comparison > 0 || comparison == 0 && !significand.IsEven) significand++;
        if (exponent < -16382)
        {
            if (significand >= (BigInteger.One << 112)) return new(sign | (UInt128.One << 112));
            return new(sign | (UInt128)significand);
        }
        if (significand >= (BigInteger.One << 113)) { significand >>= 1; exponent++; }
        if (exponent > 16383) return new(sign | ((UInt128)0x7fff << 112));
        return new(sign | ((UInt128)(exponent + 16383) << 112) | ((UInt128)significand & FractionMask));
    }

    /// <summary>Formats a finite value as exact decimal text, or an XISF non-finite token.</summary>
    /// <returns>A lossless invariant scalar representation.</returns>
    public override string ToString()
    {
        bool negative = (Bits >> 127) != 0;
        string sign = negative ? "-" : "";
        int exponent = (int)((Bits >> 112) & 0x7fff);
        UInt128 fraction = Bits & FractionMask;
        if (exponent == 0x7fff) return fraction != 0 ? "NaN" : negative ? "-Inf" : "+Inf";
        if (exponent == 0 && fraction == 0) return sign + "0";
        BigInteger significand = (BigInteger)fraction;
        int binaryExponent;
        if (exponent == 0) binaryExponent = -16494;
        else { significand |= BigInteger.One << 112; binaryExponent = exponent - 16383 - 112; }
        if (binaryExponent >= 0) return sign + (significand << binaryExponent).ToString(CultureInfo.InvariantCulture);
        int decimals = -binaryExponent;
        string digits = (significand * BigInteger.Pow(5, decimals)).ToString(CultureInfo.InvariantCulture);
        string result = digits.Length > decimals ? digits.Insert(digits.Length - decimals, ".") : "0." + new string('0', decimals - digits.Length) + digits;
        return sign + result.TrimEnd('0').TrimEnd('.');
    }
}

/// <summary>A complex value with two binary128 components.</summary>
/// <param name="Real">The real component.</param>
/// <param name="Imaginary">The imaginary component.</param>
public readonly record struct XisfComplex128(XisfFloat128 Real, XisfFloat128 Imaginary)
{
    /// <summary>Formats both components as XISF complex scalar text.</summary>
    /// <returns>A lossless scalar representation.</returns>
    public override string ToString() => $"({Real},{Imaginary})";
}

/// <summary>A complex value with two single-precision components.</summary>
/// <param name="Real">The real component.</param>
/// <param name="Imaginary">The imaginary component.</param>
public readonly record struct XisfComplex32(float Real, float Imaginary);
