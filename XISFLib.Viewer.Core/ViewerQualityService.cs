using System.Buffers.Binary;
using System.Globalization;

namespace XISFLib.Viewer;

/// <summary>Bounded-memory quality estimates from unstretched, two-dimensional real samples.</summary>
public static class ViewerQualityService
{
    private const int SampleLimit = 65_536, TileSize = 128, Radius = 6;

    /// <summary>Scans original pixels for counts and moments and estimates noise and resolved star quality with bounded samples and tiles.</summary>
    /// <param name="image">The owned image; samples are read without modification. Only two-dimensional real 8–64 bit formats are supported.</param>
    /// <param name="channel">Zero-based channel to analyze; callers should avoid alpha for star detection.</param>
    /// <param name="threshold">Detection threshold above local background, from 3 to 50 estimated noise standard deviations.</param>
    /// <param name="cancellationToken">Cancellation checked at scan rows, tiles and between analysis stages.</param>
    /// <returns>Display-ready metrics in stored sample units, sampled channel statistics and method limitations. SNR is an uncalibrated peak estimate.</returns>
    public static QualityReport Analyze(XisfImageData image, int channel, double threshold, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Dimensions.Count != 2 || image.SampleFormat is not (XisfSampleFormat.UInt8 or XisfSampleFormat.UInt16
            or XisfSampleFormat.UInt32 or XisfSampleFormat.UInt64 or XisfSampleFormat.Float32 or XisfSampleFormat.Float64))
            throw new NotSupportedException("Quality analysis requires a two-dimensional real image with 8–64 bit samples.");
        if ((uint)channel >= (uint)image.ChannelCount) throw new ArgumentOutOfRangeException(nameof(channel));
        if (!double.IsFinite(threshold) || threshold is < 3 or > 50)
            throw new ArgumentOutOfRangeException(nameof(threshold), "Detection threshold must be from 3 through 50 sigma.");
        int width = image.Dimensions[0], height = image.Dimensions[1], pixels = checked(width * height);
        int step = Math.Max(1, (int)Math.Ceiling(pixels / (double)SampleLimit));
        var samples = new List<double>(SampleLimit);
        var differences = new List<double>(SampleLimit * 2);
        double lower = image.LowerBound ?? 0, upper = image.UpperBound ?? Maximum(image.SampleFormat);
        long finite = 0, low = 0, high = 0;
        double minimum = double.PositiveInfinity, maximum = double.NegativeInfinity, mean = 0, m2 = 0;
        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int x = 0; x < width; x++)
            {
                int pixel = y * width + x;
                double value = Read(image, pixel, channel, pixels);
                if (!double.IsFinite(value)) continue;
                finite++;
                minimum = Math.Min(minimum, value); maximum = Math.Max(maximum, value);
                double delta = value - mean; mean += delta / finite; m2 += delta * (value - mean);
                if (value <= lower) low++;
                if (value >= upper) high++;
                if (pixel % step != 0) continue;
                samples.Add(value);
                if (x + 1 < width) AddDifference(Read(image, pixel + 1, channel, pixels) - value);
                if (y + 1 < height) AddDifference(Read(image, pixel + width, channel, pixels) - value);
            }
        }
        void AddDifference(double value) { if (double.IsFinite(value)) differences.Add(value); }
        double background = Median(samples), noise = Mad(differences) * 1.482602218505602 / Math.Sqrt(2);
        double p01 = Percentile(samples, 0.01), p99 = Percentile(samples, 0.99);
        long stars = 0, saturated = 0;
        var widths = new List<double>(SampleLimit);
        var eccentricities = new List<double>(SampleLimit);
        var snrs = new List<double>(SampleLimit);
        var tileBackgrounds = new List<double>();
        bool usableNoise = double.IsFinite(noise) && noise > 0;
        // Overlapping tiles retain complete apertures; only peaks in the owned core are counted.
        for (int top = 0; top < height; top += TileSize)
        for (int left = 0; left < width; left += TileSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int x0 = Math.Max(0, left - Radius), y0 = Math.Max(0, top - Radius);
            int x1 = Math.Min(width, left + TileSize + Radius), y1 = Math.Min(height, top + TileSize + Radius);
            int tw = x1 - x0, th = y1 - y0;
            var tile = new double[tw * th];
            var local = new List<double>(TileSize * TileSize);
            for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                double value = Read(image, y * width + x, channel, pixels);
                tile[(y - y0) * tw + x - x0] = value;
                if (x >= left && x < left + TileSize && y >= top && y < top + TileSize && double.IsFinite(value)) local.Add(value);
            }
            double sky = Median(local);
            // Retain a bounded, spatially distributed sample of tile backgrounds.
            long tileIndex = (long)(top / TileSize) * ((width + TileSize - 1) / TileSize) + left / TileSize;
            long tileCount = (long)((width + TileSize - 1) / TileSize) * ((height + TileSize - 1) / TileSize);
            if (tileIndex % Math.Max(1, (tileCount + SampleLimit - 1) / SampleLimit) == 0 && double.IsFinite(sky)) tileBackgrounds.Add(sky);
            if (!usableNoise || !double.IsFinite(sky)) continue;
            for (int y = Math.Max(top, Radius); y < Math.Min(top + TileSize, height - Radius); y++)
            for (int x = Math.Max(left, Radius); x < Math.Min(left + TileSize, width - Radius); x++)
            {
                int tx = x - x0, ty = y - y0;
                double peak = tile[ty * tw + tx];
                if (!double.IsFinite(peak) || peak <= sky + threshold * noise || !IsMaximum(tile, tw, tx, ty)) continue;
                if (peak >= upper) { saturated++; continue; }
                var shape = Shape(tile, tw, tx, ty, sky, noise, upper);
                if (shape is null) continue;
                stars++;
                // Count all candidates, retain the first bounded set for shape summaries.
                if (widths.Count < SampleLimit)
                {
                    widths.Add(shape.Value.Width); eccentricities.Add(shape.Value.Eccentricity); snrs.Add((peak - sky) / noise);
                }
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        var channels = image.CalculateStatistics(new() { MaximumSamplesPerChannel = SampleLimit });
        cancellationToken.ThrowIfCancellationRequested();
        tileBackgrounds.Sort();
        var entries = new List<QualityEntry>
        {
            new("Analyzed channel", channel.ToString(CultureInfo.CurrentCulture), "Zero-based channel; alpha is never selected automatically."),
            new("Image size", $"{width:N0} × {height:N0} ({pixels:N0} pixels)", "Scientific pixel coordinates; display rotation and stretch do not affect analysis."),
            new("Detected star candidates", usableNoise ? stars.ToString("N0") : "Unavailable", $"Peaks above local background + {threshold:G4}σ; unsaturated, resolved candidates only. Six-pixel border excluded."),
            new("Star density", usableNoise ? F(stars * 1_000_000d / pixels) + " / megapixel" : "Unavailable", "Detected candidates divided by the entire image area."),
            new("Saturated peak candidates", usableNoise ? saturated.ToString("N0") : "Unavailable", "Threshold-crossing local maxima at/above the declared upper bound; omitted from star shapes."),
            new("Median star FWHM estimate", F(Median(widths), " px"), "Gaussian-equivalent second-moment width in a six-pixel radius aperture; no PSF fit."),
            new("Median eccentricity", F(Median(eccentricities)), "sqrt(1 − minor variance / major variance). 0 is round; approaching 1 is elongated."),
            new("Median star peak SNR estimate", F(Median(snrs)), "(Peak − local background) / estimated noise. Excludes source shot noise; not photometric SNR."),
            new("Background estimate", F(background), $"Median of {samples.Count:N0} systematic finite samples in stored sample units."),
            new("Noise σ estimate", usableNoise ? F(noise) : "Unavailable", "1.4826 × MAD of sampled horizontal/vertical neighbor differences / sqrt(2); assumes independent Gaussian noise."),
            new("Background variation", F(Percentile(tileBackgrounds, 0.9) - Percentile(tileBackgrounds, 0.1)), "90th − 10th percentile of 128-pixel tile medians; includes real extended structures."),
            new("Signal span / noise estimate", usableNoise ? F((p99 - p01) / noise) : "Unavailable", "(99th − 1st sampled percentile) / noise; contrast diagnostic, not object SNR."),
            new("Finite / non-finite pixels", $"{finite:N0} / {pixels - finite:N0}", "Full selected-channel scan; NaN and infinities excluded from estimates."),
            new("Minimum / maximum", finite > 0 ? $"{F(minimum)} / {F(maximum)}" : "Unavailable", "Full selected-channel scan, in stored sample units."),
            new("Mean / standard deviation", finite > 0 ? $"{F(mean)} / {F(finite > 1 ? Math.Sqrt(m2 / (finite - 1)) : 0)}" : "Unavailable", "Full finite selected-channel scan; standard deviation includes astronomical structure."),
            new("1st / 99th percentile", $"{F(p01)} / {F(p99)}", "Systematic samples; useful for judging usable signal range."),
            new("At/below lower bound", Rate(low, finite), $"Bound {F(lower)}; full scan. A diagnostic of possible black clipping."),
            new("At/above upper bound", Rate(high, finite), $"Bound {F(upper)}; full scan. Declared bounds do not establish the sensor saturation level."),
            new("Stars in shape summary", widths.Count.ToString("N0"), "First 65,536 accepted candidates at most; count and density cover the full image.")
        };
        string warning = "Estimates depend on the selected channel, threshold and image processing. Crowding, nebulosity, CFA mosaics, correlated noise and broad stars can bias results. No gain/read-noise calibration or photometry is inferred.";
        if (!usableNoise) warning += " Noise is zero or unavailable: star detection and SNR are unavailable.";
        if (image.ColorFilterArray is not null) warning += " CFA mosaic detected: debayer before comparing star shapes.";
        if (image.SampleFormat == XisfSampleFormat.UInt64) warning += " UInt64 analysis uses double estimates and may round values above 2^53; stored pixels are unchanged.";
        return new(entries, channels, warning);
    }

    private static bool IsMaximum(double[] tile, int width, int x, int y)
    {
        int center = y * width + x;
        for (int dy = -2; dy <= 2; dy++)
        for (int dx = -2; dx <= 2; dx++)
        {
            int index = (y + dy) * width + x + dx;
            if (!double.IsFinite(tile[index]) || tile[index] > tile[center] || (tile[index] == tile[center] && index < center)) return false;
        }
        return true;
    }

    private static (double Width, double Eccentricity)? Shape(double[] tile, int width, int x, int y, double sky, double noise, double upper)
    {
        double sum = 0, sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
        int support = 0;
        for (int dy = -Radius; dy <= Radius; dy++)
        for (int dx = -Radius; dx <= Radius; dx++)
        {
            if (dx * dx + dy * dy > Radius * Radius) continue;
            double value = tile[(y + dy) * width + x + dx];
            if (!double.IsFinite(value) || value >= upper) return null;
            double weight = Math.Max(0, value - sky);
            if (Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1 && weight > 2 * noise) support++;
            sum += weight; sx += weight * dx; sy += weight * dy;
            sxx += weight * dx * dx; syy += weight * dy * dy; sxy += weight * dx * dy;
        }
        if (support < 3 || sum <= 0 || !double.IsFinite(sum)) return null;
        double vx = sxx / sum - Math.Pow(sx / sum, 2), vy = syy / sum - Math.Pow(sy / sum, 2), cov = sxy / sum - sx * sy / (sum * sum);
        double disc = Math.Sqrt(Math.Pow(vx - vy, 2) + 4 * cov * cov);
        double major = (vx + vy + disc) / 2, minor = (vx + vy - disc) / 2;
        double fwhm = 2.354820045 * Math.Sqrt((vx + vy) / 2);
        if (!double.IsFinite(fwhm) || minor <= 0 || fwhm < 1 || fwhm > 8) return null;
        return (fwhm, Math.Sqrt(Math.Clamp(1 - minor / major, 0, 1)));
    }

    private static double Read(XisfImageData image, int pixel, int channel, int pixels)
    {
        int size = image.SampleFormat switch { XisfSampleFormat.UInt8 => 1, XisfSampleFormat.UInt16 => 2, XisfSampleFormat.UInt32 or XisfSampleFormat.Float32 => 4, _ => 8 };
        long index = image.PixelStorage == XisfPixelLayout.Planar ? (long)channel * pixels + pixel : (long)pixel * image.ChannelCount + channel;
        var bytes = image.RawData.Slice(checked((int)(index * size)), size);
        bool big = image.IsBigEndian;
        return image.SampleFormat switch
        {
            XisfSampleFormat.UInt8 => bytes[0],
            XisfSampleFormat.UInt16 => big ? BinaryPrimitives.ReadUInt16BigEndian(bytes) : BinaryPrimitives.ReadUInt16LittleEndian(bytes),
            XisfSampleFormat.UInt32 => big ? BinaryPrimitives.ReadUInt32BigEndian(bytes) : BinaryPrimitives.ReadUInt32LittleEndian(bytes),
            XisfSampleFormat.UInt64 => big ? BinaryPrimitives.ReadUInt64BigEndian(bytes) : BinaryPrimitives.ReadUInt64LittleEndian(bytes),
            XisfSampleFormat.Float32 => big ? BinaryPrimitives.ReadSingleBigEndian(bytes) : BinaryPrimitives.ReadSingleLittleEndian(bytes),
            _ => big ? BinaryPrimitives.ReadDoubleBigEndian(bytes) : BinaryPrimitives.ReadDoubleLittleEndian(bytes)
        };
    }
    private static double Maximum(XisfSampleFormat format) => format switch
    { XisfSampleFormat.UInt8 => byte.MaxValue, XisfSampleFormat.UInt16 => ushort.MaxValue, XisfSampleFormat.UInt32 => uint.MaxValue, XisfSampleFormat.UInt64 => ulong.MaxValue, _ => 1 };
    private static double Median(List<double> values) { values.Sort(); return Percentile(values, 0.5); }
    private static double Mad(List<double> values)
    {
        double median = Median(values);
        for (int i = 0; i < values.Count; i++) values[i] = Math.Abs(values[i] - median);
        return Median(values);
    }
    private static double Percentile(List<double> sorted, double fraction)
    {
        if (sorted.Count == 0) return double.NaN;
        double index = (sorted.Count - 1) * fraction;
        int lo = (int)index, hi = Math.Min(lo + 1, sorted.Count - 1);
        return sorted[lo] * (1 - (index - lo)) + sorted[hi] * (index - lo);
    }
    private static string F(double value, string suffix = "") => double.IsFinite(value) ? value.ToString("G6", CultureInfo.CurrentCulture) + suffix : "Unavailable";
    private static string Rate(long count, long finite) => finite == 0 ? "Unavailable" : $"{count:N0} ({100d * count / finite:F3}%)";
}

public sealed record QualityEntry(string Name, string Value, string Explanation);
public sealed record QualityReport(IReadOnlyList<QualityEntry> Entries, IReadOnlyList<XisfChannelStatistics> Channels, string Notes);
