using System.Numerics;

namespace XISFLib;

internal static class XisfSelection
{
    // Exact order statistics for non-NaN values. Three-way partitioning handles
    // constant images; the sort fallback bounds adversarial input costs.
    internal static double Median(Span<double> values)
    {
        if (values.IsEmpty) return double.NaN;
        int middle = values.Length / 2;
        double upper = Select(values, middle);
        if ((values.Length & 1) != 0) return upper;
        double lower = values[0];
        for (int i = 1; i < middle; i++) lower = Math.Max(lower, values[i]);
        return (lower + upper) / 2;
    }

    private static double Select(Span<double> values, int index)
    {
        int budget = 2 * BitOperations.Log2((uint)values.Length);
        while (values.Length > 32 && budget-- > 0)
        {
            double a = values[0], b = values[values.Length / 2], c = values[^1];
            double pivot = a < b ? (b < c ? b : Math.Max(a, c)) : (a < c ? a : Math.Max(b, c));
            int lower = 0, current = 0, upper = values.Length - 1;
            while (current <= upper)
            {
                if (values[current] < pivot)
                {
                    (values[lower], values[current]) = (values[current], values[lower]);
                    lower++; current++;
                }
                else if (values[current] > pivot)
                {
                    (values[upper], values[current]) = (values[current], values[upper]);
                    upper--;
                }
                else current++;
            }
            if (index < lower) values = values[..lower];
            else if (index > upper)
            {
                values = values[(upper + 1)..];
                index -= upper + 1;
            }
            else return pivot;
        }
        values.Sort();
        return values[index];
    }
}
