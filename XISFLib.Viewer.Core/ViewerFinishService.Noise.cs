namespace XISFLib.Viewer;

public static partial class ViewerFinishService
{
    // Undecimated B3-spline wavelets, evaluated on bounded tiles. Fourteen halo pixels
    // cover all three convolutions (2 + 4 + 8); only tile interiors are published.
    // Work in luminance/color differences to suppress colored grain more than detail.
    private static byte[] ReduceMultiscaleNoise(byte[] input, int width, int height, bool gray,
        CancellationToken token, StageProgress progress)
    {
        token.ThrowIfCancellationRequested();
        const int tile = 192, halo = 14, side = tile + 2 * halo;
        int size = side * side;
        var current = new float[size]; var horizontal = new float[size];
        var smooth = new float[size]; var removed = new float[size];
        var estimates = new float[3][];
        for (int c = 0; c < 3; c++) estimates[c] = new float[size];
        var noiseSamples = new List<double>(tile * tile / 16);
        byte[] output = new byte[input.Length];
        for (int ty = 0; ty < height; ty += tile)
        for (int tx = 0; tx < width; tx += tile)
        {
            token.ThrowIfCancellationRequested();
            progress.Report(100d * ((long)ty * width + tx) / ((long)width * height));
            int tw = Math.Min(tile, width - tx), th = Math.Min(tile, height - ty);
            for (int channel = 0; channel < (gray ? 1 : 3); channel++)
            {
                Array.Clear(removed);
                for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    int p = (Math.Clamp(ty + y - halo, 0, height - 1) * width
                        + Math.Clamp(tx + x - halo, 0, width - 1)) * 4;
                    float luminance = (input[p + 2] + 2f * input[p + 1] + input[p]) / 4;
                    current[y * side + x] = channel == 0 ? luminance
                        : channel == 1 ? input[p + 2] - luminance : input[p] - luminance;
                }
                for (int scale = 0; scale < 3; scale++)
                {
                    token.ThrowIfCancellationRequested();
                    int step = 1 << scale;
                    for (int y = 0; y < side; y++)
                    for (int x = 0; x < side; x++)
                    {
                        int row = y * side;
                        horizontal[row + x] = (current[row + Math.Max(0, x - 2 * step)]
                            + 4 * current[row + Math.Max(0, x - step)] + 6 * current[row + x]
                            + 4 * current[row + Math.Min(side - 1, x + step)]
                            + current[row + Math.Min(side - 1, x + 2 * step)]) / 16;
                    }
                    for (int y = 0; y < side; y++)
                    for (int x = 0; x < side; x++)
                        smooth[y * side + x] = (horizontal[Math.Max(0, y - 2 * step) * side + x]
                            + 4 * horizontal[Math.Max(0, y - step) * side + x] + 6 * horizontal[y * side + x]
                            + 4 * horizontal[Math.Min(side - 1, y + step) * side + x]
                            + horizontal[Math.Min(side - 1, y + 2 * step) * side + x]) / 16;
                    // Measure each scale directly: stacked/resampled noise need not be white.
                    noiseSamples.Clear();
                    for (int y = halo; y < halo + th; y += 4)
                    for (int x = halo; x < halo + tw; x += 4)
                    {
                        int i = y * side + x;
                        noiseSamples.Add(current[i] - smooth[i]);
                    }
                    double median = Median(noiseSamples);
                    for (int i = 0; i < noiseSamples.Count; i++) noiseSamples[i] = Math.Abs(noiseSamples[i] - median);
                    double sigma = Median(noiseSamples) * 1.4826;
                    double threshold = sigma * (channel == 0 ? 1.1 : 1.8);
                    if (threshold > 0.05)
                        for (int i = 0; i < size; i++)
                        {
                            double detail = current[i] - smooth[i];
                            // Firm shrinkage leaves significant edges/stars intact, avoiding soft-threshold bias.
                            double keep = Math.Clamp((Math.Abs(detail) / threshold - 1) / 2, 0, 1);
                            // Stretch clipping can make a cold noise outlier look significant.
                            // Suppress only an isolated luminance dip with all four neighbors
                            // brighter; coherent dark edges retain their significant coefficients.
                            int x = i % side, y = i / side;
                            if (channel == 0 && scale == 0 && detail < -3 * threshold
                                && x > 0 && x < side - 1 && y > 0 && y < side - 1
                                && current[i] + threshold < Math.Min(Math.Min(current[i - 1], current[i + 1]),
                                    Math.Min(current[i - side], current[i + side])))
                                keep = Math.Min(keep, 0.2);
                            removed[i] += (float)(detail * (1 - keep));
                        }
                    (current, smooth) = (smooth, current);
                }
                for (int y = halo; y < halo + th; y++)
                for (int x = halo; x < halo + tw; x++)
                {
                    int i = y * side + x;
                    estimates[channel][i] = removed[i];
                }
            }
            for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
            {
                int p = ((ty + y) * width + tx + x) * 4, i = (y + halo) * side + x + halo;
                float l = estimates[0][i], r = gray ? 0 : estimates[1][i], b = gray ? 0 : estimates[2][i];
                // Keep solid black padding, but allow isolated clipped noise pixels to be repaired.
                bool blank = IsBlack(input, p)
                    && IsBlack(input, ((ty + y) * width + Math.Max(0, tx + x - 1)) * 4)
                    && IsBlack(input, ((ty + y) * width + Math.Min(width - 1, tx + x + 1)) * 4)
                    && IsBlack(input, (Math.Max(0, ty + y - 1) * width + tx + x) * 4)
                    && IsBlack(input, (Math.Min(height - 1, ty + y + 1) * width + tx + x) * 4);
                output[p] = blank ? (byte)0 : ToByte((input[p] - l - b) / 255d);
                output[p + 1] = blank ? (byte)0 : ToByte((input[p + 1] - l + (r + b) / 2) / 255d);
                output[p + 2] = blank ? (byte)0 : ToByte((input[p + 2] - l - r) / 255d);
                output[p + 3] = input[p + 3];
            }
        }
        return output;
    }

    private static bool IsBlack(byte[] pixels, int index) => pixels[index] == 0 && pixels[index + 1] == 0 && pixels[index + 2] == 0;
}
