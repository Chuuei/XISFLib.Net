namespace XISFLib.Viewer;

public static partial class ViewerFinishService
{
    private readonly record struct RestorationStar(int X, int Y, double Background, double Noise, double Peak, double Sigma, bool Saturated);

    private static List<RestorationStar> FindRestorationStars(SampleReader reader, int sourceWidth, int colors,
        int left, int top, int width, int height, CancellationToken token, StageProgress progress)
    {
        const int tile = 128, limit = 16_384;
        int channel = colors == 3 ? 1 : 0;
        var strongest = new PriorityQueue<RestorationStar, double>();
        for (int ty = 0; ty < height; ty += tile)
        for (int tx = 0; tx < width; tx += tile)
        {
            token.ThrowIfCancellationRequested(); progress.Report(100d * (ty * width + tx) / (width * height));
            int endX = Math.Min(width, tx + tile), endY = Math.Min(height, ty + tile);
            var samples = new List<double>(256); var differences = new List<double>(256);
            for (int y = ty; y < endY; y += 8)
            for (int x = tx; x < endX; x += 8)
            {
                int pixel = (top + y) * sourceWidth + left + x;
                if (!reader.IsVisible(pixel, colors)) continue;
                double value = reader.Read(pixel, channel);
                if (!double.IsFinite(value)) continue;
                samples.Add(value);
                if (x + 1 < endX)
                {
                    double delta = reader.Read(pixel + 1, channel) - value;
                    if (double.IsFinite(delta)) differences.Add(delta);
                }
            }
            if (samples.Count < 16 || differences.Count < 16) continue;
            double background = Median(samples), center = Median(differences);
            double noise = Math.Max(1e-9, Median(differences.Select(v => Math.Abs(v - center)).ToList()) * 1.4826 / Math.Sqrt(2));
            double threshold = background + Math.Max(8 * noise, 1e-6);
            for (int y = Math.Max(12, ty); y < Math.Min(height - 12, endY); y++)
            {
                token.ThrowIfCancellationRequested();
                for (int x = Math.Max(12, tx); x < Math.Min(width - 12, endX); x++)
                {
                    int pixel = (top + y) * sourceWidth + left + x;
                    double peak = reader.Read(pixel, channel);
                    if (!double.IsFinite(peak) || peak <= threshold || !reader.IsVisible(pixel, colors)) continue;
                    bool maximum = true;
                    for (int dy = -1; dy <= 1 && maximum; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int neighbor = pixel + dy * sourceWidth + dx;
                        double value = reader.Read(neighbor, channel);
                        if (!double.IsFinite(value) || value > peak || (value == peak && neighbor < pixel)) { maximum = false; break; }
                    }
                    if (!maximum) continue;
                    var ring = new List<double>(32);
                    for (int angle = 0; angle < 32; angle++)
                    {
                        double a = angle * Math.PI / 16;
                        int index = pixel + (int)Math.Round(9 * Math.Sin(a)) * sourceWidth + (int)Math.Round(9 * Math.Cos(a));
                        double value = reader.Read(index, channel);
                        if (double.IsFinite(value)) ring.Add(value);
                    }
                    if (ring.Count != 32) continue;
                    double localBackground = Median(ring), signal = peak - localBackground;
                    if (signal <= Math.Max(8 * noise, 1e-6)) continue;
                    double weight = 0, xx = 0, yy = 0, xy = 0; int support = 0, clipped = 0;
                    for (int dy = -4; dy <= 4; dy++)
                    for (int dx = -4; dx <= 4; dx++)
                    {
                        double value = reader.Read(pixel + dy * sourceWidth + dx, channel);
                        if (!double.IsFinite(value)) { support = -1000; continue; }
                        if (value >= 0.98) clipped++;
                        double w = Math.Max(0, value - localBackground - 2 * noise);
                        if (w > 0) support++;
                        weight += w; xx += w * dx * dx; yy += w * dy * dy; xy += w * dx * dy;
                    }
                    if (support < 5 || weight <= 0) continue;
                    xx /= weight; yy /= weight; xy /= weight;
                    double major = (xx + yy + Math.Sqrt((xx - yy) * (xx - yy) + 4 * xy * xy)) / 2;
                    double minor = xx + yy - major, sigma = Math.Sqrt((xx + yy) / 2);
                    if (minor < major * 0.55 || sigma < 0.85 || sigma > 2.8) continue;
                    // Truncated central moments underestimate broad cores. Use the outer wing level to keep halos out of deconvolution.
                    double wingRatio = (localBackground - background) / (peak - background);
                    if (wingRatio >= 0.95) continue; // Small fluctuations on an extended bright plateau are not separate stars.
                    if (wingRatio is > 0.05 and < 0.95) sigma = Math.Max(sigma, 9 / Math.Sqrt(-2 * Math.Log(wingRatio)));
                    var star = new RestorationStar(x, y, localBackground, noise, signal, sigma, clipped != 0);
                    if (strongest.Count < limit) strongest.Enqueue(star, signal);
                    else strongest.EnqueueDequeue(star, signal);
                }
            }
        }
        return strongest.UnorderedItems.Select(item => item.Element).OrderBy(s => s.Y).ThenBy(s => s.X).ToList();
    }

    private static int RestoreStars(SampleReader reader, int sourceWidth, int colors, int left, int top, int width, int height,
        List<RestorationStar> stars, double[] offsets, BackgroundPlane[] gradients, XisfDisplayFunction? function,
        byte[] bgra, CancellationToken token, StageProgress progress)
    {
        const int radius = 10, side = radius * 2 + 1, iterations = 5;
        int count = 0;
        // Local workspaces only: no full-frame floating-point restoration copies.
        var observed = new float[side * side]; var estimate = new float[side * side];
        var scratch = new float[side * side]; var blurred = new float[side * side]; var ratio = new float[side * side];
        for (int index = 0; index < stars.Count; index++)
        {
            token.ThrowIfCancellationRequested(); progress.Report(index * 100d / Math.Max(1, stars.Count));
            var star = stars[index];
            if (star.Saturated || star.Peak < 15 * star.Noise || star.Sigma < 1.05 || star.Sigma > 2.8) continue;
            // Reject blends; overlapping patches must never create double corrections or rectangular seams.
            if (HasCloseNeighbor(stars, index, radius * 2)) continue;
            bool valid = true;
            for (int dy = -radius; dy <= radius && valid; dy++)
            for (int dx = -radius; dx <= radius && valid; dx++)
            {
                int pixel = (top + star.Y + dy) * sourceWidth + left + star.X + dx;
                if (!reader.IsVisible(pixel, colors)) { valid = false; break; }
                for (int c = 0; c < colors; c++)
                {
                    double value = reader.Read(pixel, c);
                    if (!double.IsFinite(value) || value >= 0.98) { valid = false; break; }
                }
            }
            if (!valid) continue;
            double sigma = Math.Clamp(star.Sigma * 0.7, 0.6, 1.5);
            int kernelRadius = (int)Math.Ceiling(3 * sigma);
            var kernel = new float[kernelRadius * 2 + 1];
            double sum = 0;
            for (int i = -kernelRadius; i <= kernelRadius; i++) sum += kernel[i + kernelRadius] = (float)Math.Exp(-i * i / (2 * sigma * sigma));
            for (int i = 0; i < kernel.Length; i++) kernel[i] /= (float)sum;
            for (int c = 0; c < colors; c++)
            {
                var ring = new List<double>(32);
                for (int angle = 0; angle < 32; angle++)
                {
                    double a = angle * Math.PI / 16;
                    int pixel = (top + star.Y + (int)Math.Round(9 * Math.Sin(a))) * sourceWidth + left + star.X + (int)Math.Round(9 * Math.Cos(a));
                    double value = reader.Read(pixel, c);
                    if (double.IsFinite(value)) ring.Add(value);
                }
                if (ring.Count != 32) { valid = false; break; }
                double background = Median(ring);
                for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    int pixel = (top + star.Y + y - radius) * sourceWidth + left + star.X + x - radius;
                    double value = reader.Read(pixel, c);
                    if (!double.IsFinite(value) || !reader.IsVisible(pixel, colors)) valid = false;
                    observed[y * side + x] = estimate[y * side + x] = (float)Math.Max(0, value - background);
                }
                if (!valid) break;
                double channelPeak = observed.Max();
                double floor = Math.Max(star.Noise, channelPeak * 0.005);
                for (int iteration = 0; iteration < iterations; iteration++)
                {
                    token.ThrowIfCancellationRequested();
                    ConvolveStar(estimate, scratch, blurred, side, kernel, kernelRadius);
                    for (int i = 0; i < ratio.Length; i++)
                    {
                        double damping = Math.Clamp((observed[i] - 2 * star.Noise) / (6 * star.Noise), 0, 1);
                        ratio[i] = (float)(1 + damping * (Math.Clamp((observed[i] + floor) / (blurred[i] + floor), 0.5, 2) - 1));
                    }
                    ConvolveStar(ratio, scratch, blurred, side, kernel, kernelRadius);
                    for (int i = 0; i < estimate.Length; i++) estimate[i] = Math.Min(estimate[i] * blurred[i], (float)(channelPeak * 2));
                }
                for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    int dx = x - radius, dy = y - radius;
                    double r = Math.Sqrt(dx * dx + dy * dy);
                    double mask = 0.75 * Math.Clamp((9 - r) / 3, 0, 1)
                        * Math.Clamp((observed[y * side + x] - 3 * star.Noise) / Math.Max(channelPeak * 0.08, star.Noise), 0, 1);
                    if (mask <= 0) continue;
                    int sx = star.X + dx, sy = star.Y + dy, pixel = (top + sy) * sourceWidth + left + sx;
                    double value = reader.Read(pixel, c) + mask * (estimate[y * side + x] - observed[y * side + x]);
                    WriteRestoredPixel(reader, pixel, colors, c, sx, sy, width, height, value, offsets, gradients, function, bgra);
                }
            }
            if (valid) count++;
        }
        return count;
    }

    private static bool HasCloseNeighbor(List<RestorationStar> stars, int index, int distance)
    {
        var star = stars[index];
        // Stars are ordered by row; bound the search vertically before comparing distances.
        for (int i = index - 1; i >= 0 && star.Y - stars[i].Y < distance; i--)
            if (Math.Abs(star.X - stars[i].X) < distance && SquaredDistance(star.X, star.Y, stars[i].X, stars[i].Y) < distance * distance) return true;
        for (int i = index + 1; i < stars.Count && stars[i].Y - star.Y < distance; i++)
            if (Math.Abs(star.X - stars[i].X) < distance && SquaredDistance(star.X, star.Y, stars[i].X, stars[i].Y) < distance * distance) return true;
        return false;
    }

    private static long SquaredDistance(int x, int y, int sx, int sy) => (long)(x - sx) * (x - sx) + (long)(y - sy) * (y - sy);

    private static void ConvolveStar(float[] input, float[] scratch, float[] output, int side, float[] kernel, int radius)
    {
        for (int y = 0; y < side; y++)
        for (int x = 0; x < side; x++)
        {
            float sum = 0;
            for (int k = -radius; k <= radius; k++) sum += input[y * side + Math.Clamp(x + k, 0, side - 1)] * kernel[k + radius];
            scratch[y * side + x] = sum;
        }
        for (int y = 0; y < side; y++)
        for (int x = 0; x < side; x++)
        {
            float sum = 0;
            for (int k = -radius; k <= radius; k++) sum += scratch[Math.Clamp(y + k, 0, side - 1) * side + x] * kernel[k + radius];
            output[y * side + x] = sum;
        }
    }

    private static void WriteRestoredPixel(SampleReader reader, int pixel, int colors, int channel, int x, int y, int width, int height,
        double value, double[] offsets, BackgroundPlane[] gradients, XisfDisplayFunction? function, byte[] bgra)
    {
        double alpha = reader.Alpha(pixel, colors);
        byte display = ToByte(XisfDisplayFunctions.Evaluate(value - offsets[channel] - gradients[channel].At(x, y, width, height), function, channel) * alpha);
        byte original = ToByte(XisfDisplayFunctions.Evaluate(reader.Read(pixel, channel) - offsets[channel]
            - gradients[channel].At(x, y, width, height), function, channel) * alpha);
        int destination = (y * width + x) * 4;
        int target = destination + (colors == 1 ? 0 : 2 - channel);
        byte corrected = (byte)Math.Clamp(bgra[target] + display - original, 0, 255);
        if (colors == 1) bgra[destination] = bgra[destination + 1] = bgra[destination + 2] = corrected;
        else bgra[target] = corrected;
    }

    private static int ReduceSpikes(SampleReader reader, int sourceWidth, int colors, int left, int top, int width, int height,
        List<RestorationStar> stars, double[] offsets, BackgroundPlane[] gradients, XisfDisplayFunction? function,
        byte[] bgra, CancellationToken token, StageProgress progress)
    {
        int channel = colors == 3 ? 1 : 0, count = 0;
        var distinct = new List<RestorationStar>();
        foreach (var candidate in stars.Where(s => s.Peak > 80 * s.Noise && s.Sigma >= 1.3).OrderByDescending(s => s.Peak + s.Background))
        {
            // A structured bright core can contain several local maxima; fit that star once, anchored by its brightest peak.
            if (distinct.Any(s => SquaredDistance(s.X, s.Y, candidate.X, candidate.Y) < Math.Pow(Math.Max(24, 6 * s.Sigma), 2))) continue;
            distinct.Add(candidate); if (distinct.Count == 32) break;
        }
        var bright = distinct.ToArray();
        for (int index = 0; index < bright.Length; index++)
        {
            token.ThrowIfCancellationRequested(); progress.Report(index * 100d / Math.Max(1, bright.Length));
            var star = bright[index];
            int inner = Math.Max(12, (int)Math.Ceiling(4 * star.Sigma));
            int outer = Math.Min(128, Math.Min(Math.Min(star.X, width - 1 - star.X), Math.Min(star.Y, height - 1 - star.Y)) - 8);
            if (outer < inner + 20) continue;
            double threshold = Math.Max(4 * star.Noise, star.Peak * 0.001);
            var rays = new List<(double Angle, double Score, int End)>();
            var angularScores = new double[90];
            for (int degrees = 0; degrees < 180; degrees += 2)
            {
                double angle = degrees * Math.PI / 180, cos = Math.Cos(angle), sin = Math.Sin(angle), score = 0;
                int support = 0, end = inner;
                for (int r = inner; r <= outer; r += 4)
                {
                    double forward = RayExcess(reader, sourceWidth, colors, channel, left, top, star.X, star.Y, r * cos, r * sin, cos, sin);
                    double reverse = RayExcess(reader, sourceWidth, colors, channel, left, top, star.X, star.Y, -r * cos, -r * sin, cos, sin);
                    double paired = Math.Min(forward, reverse);
                    if (paired > threshold) { support++; end = r; score += paired; }
                }
                angularScores[degrees / 2] = score;
                if (support >= 5 && support >= (outer - inner) / 16) rays.Add((angle, score, Math.Min(outer, end + 4)));
            }
            double ordinaryScore = Median(angularScores.ToList());
            var selected = new List<(double Angle, double Score, int End)>();
            foreach (var ray in rays.OrderByDescending(r => r.Score))
            {
                int bin = (int)Math.Round(ray.Angle * 90 / Math.PI) % 90;
                // A round halo produces similar transverse excess in every direction; only narrow angular peaks qualify.
                if (ray.Score < ordinaryScore * 2.5 || (angularScores[(bin + 3) % 90] + angularScores[(bin + 87) % 90]) / 2 > ray.Score * 0.8) continue;
                if (selected.Any(r => Math.Min(Math.Abs(r.Angle - ray.Angle), Math.PI - Math.Abs(r.Angle - ray.Angle)) < Math.PI / 12)) continue;
                selected.Add(ray); if (selected.Count == 3) break;
            }
            if (selected.Count == 0) continue;
            var neighbors = stars.Where(s => s != star && (s.Sigma <= 2.8 || s.Saturated)
                && s.Background < star.Background + star.Peak * 0.3
                && SquaredDistance(s.X, s.Y, star.X, star.Y) < (outer + 12) * (outer + 12)).ToArray();
            bool changed = false;
            foreach (var ray in selected)
            {
                double cos = Math.Cos(ray.Angle), sin = Math.Sin(ray.Angle);
                for (int dy = -ray.End; dy <= ray.End; dy++)
                {
                    token.ThrowIfCancellationRequested();
                    for (int dx = -ray.End; dx <= ray.End; dx++)
                    {
                        double along = dx * cos + dy * sin, across = -dx * sin + dy * cos;
                        double r = Math.Abs(along);
                        if (r < inner || r > ray.End || Math.Abs(across) > 3) continue;
                        int x = star.X + dx, y = star.Y + dy;
                        if (neighbors.Any(s => SquaredDistance(x, y, s.X, s.Y) < Math.Pow(3 * s.Sigma + 2, 2))) continue;
                        double excess = RayExcess(reader, sourceWidth, colors, channel, left, top, star.X, star.Y, dx, dy, cos, sin);
                        if (excess <= threshold) continue;
                        double strength = (star.Sigma > 3 ? 0.25 : 0.4) * Math.Clamp((r - inner) / 6, 0, 1) * Math.Clamp((ray.End - r) / 6, 0, 1)
                            * Math.Clamp((3.5 - Math.Abs(across)) / 2, 0, 1);
                        int pixel = (top + y) * sourceWidth + left + x;
                        for (int c = 0; c < colors; c++)
                        {
                            double value = reader.Read(pixel, c);
                            double reduction = RayExcess(reader, sourceWidth, colors, c, left, top, star.X, star.Y, dx, dy, cos, sin);
                            if (!double.IsFinite(value) || reduction <= 0) continue;
                            WriteRestoredPixel(reader, pixel, colors, c, x, y, width, height, value - strength * reduction, offsets, gradients, function, bgra);
                        }
                        changed |= strength > 0;
                    }
                }
            }
            if (changed) count++;
        }
        return count;
    }

    private static double RayExcess(SampleReader reader, int sourceWidth, int colors, int channel, int left, int top,
        int x, int y, double dx, double dy, double cos, double sin)
    {
        int cx = x + (int)Math.Round(dx), cy = y + (int)Math.Round(dy);
        int pixel = (top + cy) * sourceWidth + left + cx;
        if (!reader.IsVisible(pixel, colors)) return 0;
        double distance = Math.Clamp(Math.Sqrt(dx * dx + dy * dy) * 0.3, 6, 16);
        int across = (int)Math.Round(distance * cos) * sourceWidth - (int)Math.Round(distance * sin);
        if (!reader.IsVisible(pixel + across, colors) || !reader.IsVisible(pixel - across, colors)) return 0;
        double value = reader.Read(pixel, channel), a = reader.Read(pixel + across, channel), b = reader.Read(pixel - across, channel);
        return double.IsFinite(value) && double.IsFinite(a) && double.IsFinite(b) ? Math.Max(0, value - Math.Max(a, b)) : 0;
    }
}
