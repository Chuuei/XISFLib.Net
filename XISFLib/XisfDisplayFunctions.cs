namespace XISFLib;

/// <summary>Implements XISF display functions and the recommended adaptive algorithm.</summary>
public static class XisfDisplayFunctions
{
    /// <summary>Evaluates the midtones transfer function, including its boundary cases.</summary>
    /// <param name="x">The normalized input.</param>
    /// <param name="midtones">The midtones balance in [0,1].</param>
    /// <returns>The transformed value.</returns>
    public static double MidtonesTransfer(double x, double midtones) => x == 0 ? 0 : x == midtones ? 0.5 : x == 1 ? 1 :
        (midtones - 1) * x / ((2 * midtones - 1) * x - midtones);

    /// <summary>Applies clipping, midtones, and then range expansion, in the specified order.</summary>
    /// <param name="value">The normalized sample.</param>
    /// <param name="midtones">The midtones balance.</param>
    /// <param name="shadows">The shadow clipping point.</param>
    /// <param name="highlights">The highlight clipping point.</param>
    /// <param name="lowRange">The shadow range expansion parameter.</param>
    /// <param name="highRange">The highlight range expansion parameter.</param>
    /// <returns>The display sample.</returns>
    public static double Evaluate(double value, double midtones, double shadows, double highlights, double lowRange = 0, double highRange = 1)
    {
        double clipped = shadows == highlights ? shadows : value < shadows ? 0 : value > highlights ? 1 : (value - shadows) / (highlights - shadows);
        return (MidtonesTransfer(clipped, midtones) - lowRange) / (highRange - lowRange);
    }

    /// <summary>Applies a stored display function to a nominal color or lightness component.</summary>
    /// <param name="value">The normalized sample.</param>
    /// <param name="function">The display function; null selects the identity.</param>
    /// <param name="component">0 for red/gray, 1 for green, 2 for blue, or 3 for Lab lightness.</param>
    /// <returns>The display sample.</returns>
    public static double Evaluate(double value, XisfDisplayFunction? function, int component = 0)
    {
        if (function is null) return value;
        if (component is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(component));
        return Evaluate(value, function.Midtones[component], function.Shadows[component], function.Highlights[component], function.LowRange[component], function.HighRange[component]);
    }

    /// <summary>Computes adaptive display parameters from normalized gray or RGB channel samples.</summary>
    /// <param name="channels">One gray channel or three RGB channels. Non-finite samples are excluded.</param>
    /// <param name="targetBackground">The recommended default target background is 0.25.</param>
    /// <param name="clipping">Clipping in normalized median absolute deviations; defaults to -2.8.</param>
    /// <param name="linkChannels">Whether to preserve color balance with shared RGB parameters.</param>
    /// <returns>The adaptive function, including inverted-image handling.</returns>
    public static XisfDisplayFunction CreateAdaptive(IReadOnlyList<double[]> channels, double targetBackground = 0.25, double clipping = -2.8, bool linkChannels = false)
    {
        ArgumentNullException.ThrowIfNull(channels);
        if (channels.Count is not (1 or 3)) throw new ArgumentException("Supply one gray or three RGB channels.", nameof(channels));
        if (!double.IsFinite(targetBackground) || targetBackground is <= 0 or >= 1 || !double.IsFinite(clipping) || clipping > 0)
            throw new ArgumentOutOfRangeException(nameof(targetBackground));
        double[] medians = new double[channels.Count], deviations = new double[channels.Count];
        for (int i = 0; i < channels.Count; i++)
        {
            double[] values = new double[channels[i].Length];
            int count = 0;
            foreach (double value in channels[i])
                if (double.IsFinite(value)) values[count++] = Math.Clamp(value, 0, 1);
            if (count != values.Length) Array.Resize(ref values, count);
            if (values.Length == 0) { medians[i] = 0; deviations[i] = 0; continue; }
            medians[i] = Median(values);
            for (int j = 0; j < values.Length; j++) values[j] = Math.Abs(values[j] - medians[i]);
            deviations[i] = 1.4826 * Median(values);
        }
        double[] m = [0.5, 0.5, 0.5, 0.5], s = [0, 0, 0, 0], h = [1, 1, 1, 1];
        bool invertAll = medians.Min() > 0.5;
        for (int i = 0; i < channels.Count; i++)
        {
            bool inverted = linkChannels ? invertAll : medians[i] > 0.5;
            if (!inverted && deviations[i] != 0) s[i] = Math.Clamp(medians[i] + clipping * deviations[i], 0, 1);
            if (inverted && deviations[i] != 0) h[i] = Math.Clamp(medians[i] - clipping * deviations[i], 0, 1);
            m[i] = inverted ? MidtonesTransfer(targetBackground, h[i] - medians[i]) : MidtonesTransfer(medians[i] - s[i], targetBackground);
            if (!double.IsFinite(m[i])) m[i] = 0.5;
        }
        if (linkChannels && channels.Count == 3)
        {
            double shadow = s.Take(3).Average(), highlight = h.Take(3).Average(), median = medians.Average();
            double midtones = invertAll ? MidtonesTransfer(targetBackground, highlight - median) : MidtonesTransfer(median - shadow, targetBackground);
            for (int i = 0; i < 3; i++) { s[i] = shadow; h[i] = highlight; m[i] = double.IsFinite(midtones) ? midtones : 0.5; }
        }
        return new(m, s, h, new double[4], new double[] { 1, 1, 1, 1 }) { Name = "Adaptive display function" };
    }
    private static double Median(double[] values) => XisfSelection.Median(values);
}
