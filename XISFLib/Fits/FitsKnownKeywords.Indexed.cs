using System.Globalization;

namespace XISFLib;

public static partial class FitsKnownKeywords
{
    private const string WcsReference = "https://fits.gsfc.nasa.gov/fits_standard.html";

    private static string WcsAxis(string prefix, int axis, char? alternate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(axis, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(axis, 99);
        ValidateAlternate(alternate);
        return Indexed(prefix, axis) + alternate;
    }

    private static string WcsPair(string prefix, int axis, int index, char? alternate, bool parameter)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, parameter ? 0 : 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, 99);
        return WcsAxis(prefix, axis, null) + "_" + index.ToString(CultureInfo.InvariantCulture) + ValidatedAlternate(alternate);
    }

    private static string ValidatedAlternate(char? alternate)
    {
        ValidateAlternate(alternate);
        return alternate?.ToString() ?? "";
    }

    private static void ValidateAlternate(char? alternate)
    {
        if (alternate is not null && alternate is not (>= 'A' and <= 'Z'))
            throw new ArgumentOutOfRangeException(nameof(alternate), "Alternate WCS codes must be A through Z.");
    }

    private static string Indexed(string prefix, int index)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 1);
        string keyword = prefix + index.ToString(CultureInfo.InvariantCulture);
        if (keyword.Length > 8) throw new ArgumentOutOfRangeException(nameof(index), "The keyword must fit in eight characters.");
        return keyword;
    }
}
