using System.Buffers.Binary;
using ImageMagick;

namespace XISFLib.Viewer;

/// <summary>Creates an aesthetic, full-resolution sharing copy without modifying scientific samples.</summary>
public static partial class ViewerFinishService
{
    /// <summary>Automatically trims blank edges, reduces reliable broad gradients in linear data and stretches onto a dark sky.</summary>
    /// <param name="source">Two-dimensional real Gray/RGB data, optionally with alpha. CFA mosaics require debayering first.</param>
    /// <param name="isRasterImport">Whether the image is an already-developed raster import, whose exposure/color balance should be retained.</param>
    /// <param name="cancellationToken">Cancellation between stages and at scan/render rows; native filters are not interruptible.</param>
    /// <returns>An owned, opaque 8-bit BGRA sharing copy with orientation and valid source DPI, plus a description of automatic choices.</returns>
    public static FinishedPicture Create(XisfImageData source, bool isRasterImport = false, CancellationToken cancellationToken = default)
        => Create(source, isRasterImport, cancellationToken, null);

    /// <summary>Creates a finished sharing copy with throttled, stage-local progress reported on the worker thread.</summary>
    /// <param name="source">Two-dimensional real Gray/RGB source, optionally with alpha; source samples are unchanged.</param>
    /// <param name="isRasterImport">Whether to retain developed raster exposure.</param>
    /// <param name="cancellationToken">Cancellation between stages and scan/render rows.</param>
    /// <param name="progress">Optional progress receiver. Marshal callbacks to the UI when required.</param>
    /// <returns>An owned, opaque sharing copy with source orientation/DPI.</returns>
    public static FinishedPicture Create(XisfImageData source, bool isRasterImport, CancellationToken cancellationToken, IProgress<FinishProgress>? progress)
        => Create(source, isRasterImport, cancellationToken, progress, new());

    /// <summary>Creates a sharing copy with optional adaptive noise reduction, automatic star restoration and spike attenuation.</summary>
    /// <param name="source">Scientific Gray/RGB source; samples and metadata remain unchanged.</param>
    /// <param name="isRasterImport">Whether to retain developed raster exposure and skip linear star restoration.</param>
    /// <param name="cancellationToken">Cancellation between stages, tiles, stars and scan rows.</param>
    /// <param name="progress">Optional worker-thread stage progress.</param>
    /// <param name="options">Simple noise and star restoration switches; no manual noise or PSF parameters are needed.</param>
    /// <returns>An owned sharing preview/export artifact.</returns>
    public static FinishedPicture Create(XisfImageData source, bool isRasterImport, CancellationToken cancellationToken,
        IProgress<FinishProgress>? progress, FinishOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var reporter = new StageProgress(progress);
        reporter.Begin("Inspecting image and trimming edges");
        ArgumentNullException.ThrowIfNull(source);
        if (source.ColorFilterArray is not null) throw new NotSupportedException("This image is a CFA mosaic. Open a debayered RGB image to create a finished color picture.");
        bool gray = source.ColorSpace is null || source.ColorSpace.Equals("Gray", StringComparison.OrdinalIgnoreCase);
        bool rgb = source.ColorSpace?.Equals("RGB", StringComparison.OrdinalIgnoreCase) == true;
        int colors = gray ? 1 : 3;
        if (source.Dimensions.Count != 2 || (!gray && !rgb) || source.ChannelCount < colors || source.ChannelCount > colors + 1
            || source.SampleFormat is not (XisfSampleFormat.UInt8 or XisfSampleFormat.UInt16 or XisfSampleFormat.UInt32
                or XisfSampleFormat.UInt64 or XisfSampleFormat.Float32 or XisfSampleFormat.Float64))
            throw new NotSupportedException("Finishing supports two-dimensional Gray or RGB images with real 8–64 bit samples, optionally with alpha.");
        var reader = new SampleReader(source);
        int width = source.Dimensions[0], height = source.Dimensions[1];
        // Verify some visible signal before treating black edges as empty stacking margins.
        bool hasSignal = false;
        int pixels = checked(width * height), stride = Math.Max(1, (int)Math.Ceiling(pixels / 65_536d));
        for (int pixel = 0; pixel < pixels; pixel += stride)
        {
            if (pixel % 1024 == 0) cancellationToken.ThrowIfCancellationRequested();
            if (reader.HasSignal(pixel, colors)) { hasSignal = true; break; }
        }
        int left = 0, top = 0, right = width, bottom = height;
        if (hasSignal)
        {
            // Stop at the first populated edge; never search for or tightly crop the astronomical subject.
            while (top < height / 5 && BlankLine(reader, width, colors, 0, top, width, true, cancellationToken)) top++;
            while (height - bottom < height / 5 && BlankLine(reader, width, colors, 0, bottom - 1, width, true, cancellationToken)) bottom--;
            while (left < width / 5 && BlankLine(reader, width, colors, left, top, bottom - top, false, cancellationToken)) left++;
            while (width - right < width / 5 && BlankLine(reader, width, colors, right - 1, top, bottom - top, false, cancellationToken)) right--;
            if (!isRasterImport)
                TrimMosaicCorners(reader, width, colors, ref left, ref top, ref right, ref bottom, cancellationToken);
            // Small edge cleanup for large frames, without a composition/aspect-ratio crop.
            int tidyX = right - left >= 256 ? Math.Max(1, (right - left) / 100) : 0;
            int tidyY = bottom - top >= 256 ? Math.Max(1, (bottom - top) / 100) : 0;
            left += tidyX; right -= tidyX; top += tidyY; bottom -= tidyY;
        }
        int croppedWidth = right - left, croppedHeight = bottom - top;
        int cropPixels = checked(croppedWidth * croppedHeight);
        int sampleStride = Math.Max(1, (int)Math.Ceiling(cropPixels / 65_536d));
        reporter.Begin("Measuring background and stretch");
        var sampled = Enumerable.Range(0, colors).Select(_ => new List<double>(65_536)).ToArray();
        for (int pixel = 0; pixel < cropPixels; pixel += sampleStride)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = (top + pixel / croppedWidth) * width + left + pixel % croppedWidth;
            if (!reader.IsVisible(index, colors)) continue;
            for (int c = 0; c < colors; c++)
            {
                double value = reader.Read(index, c);
                if (double.IsFinite(value)) sampled[c].Add(Math.Clamp(value, 0, 1));
            }
        }
        double[] medians = sampled.Select(Median).ToArray();
        double[] deviations = sampled.Select((values, channel) => Median(values.Select(value => Math.Abs(value - medians[channel])).ToList()) * 1.4826).ToArray();
        double medianLevel = medians.Average(), spread = deviations.Average();
        // A high pedestal with a narrow distribution can still be linear astronomy data.
        // Constant frames and already-developed broad distributions retain their exposure.
        bool highPedestal = double.IsFinite(medianLevel) && medianLevel >= 0.18 && double.IsFinite(spread)
            && spread > 0 && spread < medianLevel * 0.1;
        bool stretch = !isRasterImport && medians.All(double.IsFinite) && (medianLevel < 0.18 || highPedestal) && hasSignal;
        var gradients = new BackgroundPlane[colors];
        if (stretch)
        {
            reporter.Begin("Reducing uneven sky glow");
            for (int c = 0; c < colors; c++)
                gradients[c] = EstimateGradient(reader, width, colors, left, top, croppedWidth, croppedHeight, c, cancellationToken);
            if (gradients.Any(g => g.IsActive))
            {
                // Recompute the stretch from corrected samples, with the same correction as the full-resolution render.
                foreach (var values in sampled) values.Clear();
                for (int pixel = 0; pixel < cropPixels; pixel += sampleStride)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int x = pixel % croppedWidth, y = pixel / croppedWidth;
                    int index = (top + y) * width + left + x;
                    if (!reader.IsVisible(index, colors)) continue;
                    for (int c = 0; c < colors; c++)
                    {
                        double value = reader.Read(index, c);
                        if (double.IsFinite(value)) sampled[c].Add(value - gradients[c].At(x, y, croppedWidth, croppedHeight));
                    }
                }
                medians = sampled.Select(Median).ToArray();
                medianLevel = medians.Average();
            }
        }
        // Keep high-offset data below the adaptive algorithm's inverted-image branch.
        double pedestal = stretch && highPedestal ? Math.Max(0, medianLevel - 0.1) : 0;
        var offsets = new double[colors];
        if (stretch && rgb)
        {
            double target = medians.Average();
            for (int c = 0; c < colors; c++) offsets[c] = medians[c] - target + pedestal;
        }
        else if (stretch) offsets[0] = pedestal;
        XisfDisplayFunction? function = stretch ? XisfDisplayFunctions.CreateAdaptive(
            sampled.Select((values, c) => values.Select(v => v - offsets[c]).ToArray()).ToArray(), 0.10, -2.8, true) : null;
        byte[] bgra = new byte[checked(cropPixels * 4)];
        reporter.Begin("Stretching image", 0);
        for (int y = 0; y < croppedHeight; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            reporter.Report(y * 100d / croppedHeight);
            for (int x = 0; x < croppedWidth; x++)
            {
                int sourcePixel = (top + y) * width + left + x, dest = (y * croppedWidth + x) * 4;
                double alpha = source.ChannelCount > colors ? reader.Read(sourcePixel, colors) : 1;
                alpha = double.IsFinite(alpha) ? Math.Clamp(alpha, 0, 1) : 0;
                for (int c = 0; c < 3; c++)
                {
                    int channel = gray ? 0 : c;
                    double value = reader.Read(sourcePixel, channel);
                    value = double.IsFinite(value) ? XisfDisplayFunctions.Evaluate(value - offsets[channel]
                        - gradients[channel].At(x, y, croppedWidth, croppedHeight), function, channel) : 0;
                    bgra[dest + 2 - c] = ToByte(value * alpha);
                }
                bgra[dest + 3] = 255; // Flatten onto black, identically for preview, PNG and JPEG.
            }
        }
        double displayNoise = EstimateDisplayNoise(bgra, croppedWidth, croppedHeight);
        bool multiscaleNoise = stretch && options.ReduceNoise && displayNoise >= 2;
        if (multiscaleNoise)
        {
            reporter.Begin("Reducing grain and color noise", 0);
            bgra = ReduceMultiscaleNoise(bgra, croppedWidth, croppedHeight, gray, cancellationToken, reporter);
        }
        reporter.Begin("Preparing image for cleanup");
        using var picture = new MagickImage();
        // Some filters contain several native passes; keep those stages indeterminate rather than showing a false completion.
        if (progress is not null) picture.Progress += (_, e) =>
        {
            if (e.Origin?.StartsWith("SelectiveBlur/", StringComparison.Ordinal) == true) reporter.Report((double)e.Progress);
        };
        picture.ReadPixels(bgra, new PixelReadSettings((uint)croppedWidth, (uint)croppedHeight, StorageType.Char, "BGRA"));
        picture.ColorSpace = ColorSpace.sRGB;
        cancellationToken.ThrowIfCancellationRequested();
        // Selective blur smooths only low-contrast neighborhoods; thresholded sharpening avoids boosting faint grain.
        if (croppedWidth >= 3 && croppedHeight >= 3)
        {
            double contrastThreshold = Math.Clamp(2.5 * displayNoise / 255 * 100, 3, 15);
            reporter.Begin("Smoothing background noise");
            picture.SelectiveBlur(2, 0.75, new Percentage(contrastThreshold), Channels.RGB);
            cancellationToken.ThrowIfCancellationRequested();
            reporter.Begin("Enhancing detail");
            picture.UnsharpMask(1, 0.7, 0.2, Math.Max(0.02, 2 * displayNoise / 255), Channels.RGB);
            reporter.Begin("Finishing colors");
            if (rgb) picture.Modulate(new Percentage(100), new Percentage(106));
        }
        cancellationToken.ThrowIfCancellationRequested();
        picture.Depth = 8;
        reporter.Begin("Preparing finished preview");
        byte[] bytes;
        using (var output = picture.GetPixels()) bytes = output.ToByteArray(PixelMapping.BGRA)!;
        cancellationToken.ThrowIfCancellationRequested();
        // Restore linear star signals, then add their display deltas to the cleaned image so smoothing does not undo restoration.
        int restoredStars = 0, reducedSpikes = 0;
        if (stretch && (options.SharpenStars || options.ReduceStarSpikes))
        {
            reporter.Begin("Finding stars for restoration", 0);
            var stars = FindRestorationStars(reader, width, colors, left, top, croppedWidth, croppedHeight, cancellationToken, reporter);
            if (options.SharpenStars)
            {
                reporter.Begin("Deconvolving isolated stars", 0);
                restoredStars = RestoreStars(reader, width, colors, left, top, croppedWidth, croppedHeight, stars,
                    offsets, gradients, function, bytes, cancellationToken, reporter);
            }
            if (options.ReduceStarSpikes)
            {
                reporter.Begin("Reducing star spikes", 0);
                reducedSpikes = ReduceSpikes(reader, width, colors, left, top, croppedWidth, croppedHeight, stars,
                    offsets, gradients, function, bytes, cancellationToken, reporter);
            }
        }
        reporter.Begin("Preparing finished preview");
        if (source.Orientation is not (null or "0"))
        {
            picture.ReadPixels(bytes, new PixelReadSettings((uint)croppedWidth, (uint)croppedHeight, StorageType.Char, "BGRA"));
            ApplyOrientation(picture, source.Orientation);
            using var oriented = picture.GetPixels();
            bytes = oriented.ToByteArray(PixelMapping.BGRA)!;
        }
        cancellationToken.ThrowIfCancellationRequested();
        var (dpiX, dpiY) = Dpi(source.Resolution);
        bool quarterTurn = source.Orientation?.StartsWith("90", StringComparison.Ordinal) == true || source.Orientation?.StartsWith("-90", StringComparison.Ordinal) == true;
        if (quarterTurn) (dpiX, dpiY) = (dpiY, dpiX);
        string crop = left == 0 && top == 0 && right == width && bottom == height ? "Full frame retained" : $"Edges trimmed: {width:N0} × {height:N0} → {croppedWidth:N0} × {croppedHeight:N0}";
        string exposure = stretch ? (rgb ? "Automatic stretch onto a dark sky and background color balance" : "Automatic stretch onto a dark sky") : "Existing exposure retained";
        string gradient = gradients.Any(g => g.IsActive) ? "; conservative gradient reduction" : "";
        if (multiscaleNoise) gradient += "; adaptive multiscale grain and color noise reduction";
        string restoration = restoredStars > 0 ? $"; gentle deconvolution of {restoredStars:N0} {(restoredStars == 1 ? "star" : "stars")}" : "";
        if (reducedSpikes > 0) restoration += $"; spike attenuation around {reducedSpikes:N0} bright {(reducedSpikes == 1 ? "star" : "stars")}";
        return new(checked((int)picture.Width), checked((int)picture.Height), bytes, dpiX, dpiY,
            $"{crop}. {exposure}{gradient}{restoration}; gentle noise reduction and detail enhancement{(rgb ? ", subtle color boost" : "")}. Ready to save as an 8-bit sharing image.");
    }

    /// <summary>Publishes the reviewed opaque picture as PNG or high-quality JPEG, staging beside the destination.</summary>
    /// <param name="path">Destination ending in .png, .jpg or .jpeg.</param>
    /// <param name="picture">The exact finished preview pixels.</param>
    /// <param name="cancellationToken">Checked before/after encoding and before publication. Existing output survives failure/cancellation.</param>
    public static void Save(string path, FinishedPicture picture, CancellationToken cancellationToken = default)
        => Save(path, picture, cancellationToken, null);

    /// <summary>Stages and publishes PNG/JPEG with throttled stage-local progress on the worker thread.</summary>
    /// <param name="path">Destination ending in .png, .jpg or .jpeg.</param>
    /// <param name="picture">The exact reviewed opaque BGRA pixels.</param>
    /// <param name="cancellationToken">Checked before/after encoding and before atomic publication.</param>
    /// <param name="progress">Optional progress receiver. Marshal callbacks to the UI when required.</param>
    public static void Save(string path, FinishedPicture picture, CancellationToken cancellationToken, IProgress<FinishProgress>? progress)
    {
        var reporter = new StageProgress(progress);
        ArgumentNullException.ThrowIfNull(picture);
        if (picture.Width <= 0 || picture.Height <= 0 || picture.Pixels.Length != checked(picture.Width * picture.Height * 4))
            throw new ArgumentException("Unexpected finished picture dimensions or buffer size.", nameof(picture));
        if (!double.IsFinite(picture.DpiX) || !double.IsFinite(picture.DpiY) || picture.DpiX <= 0 || picture.DpiY <= 0)
            throw new ArgumentException("Finished picture DPI must be finite and positive.", nameof(picture));
        MagickFormat format = Path.GetExtension(path).ToLowerInvariant() switch
        { ".png" => MagickFormat.Png24, ".jpg" or ".jpeg" => MagickFormat.Jpeg, _ => throw new NotSupportedException("Save the finished picture as .png, .jpg or .jpeg.") };
        path = Path.GetFullPath(path);
        string temporary = Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            reporter.Begin("Preparing pixels for export");
            using var image = new MagickImage();
            if (progress is not null) image.Progress += (_, e) =>
            {
                if (e.Origin?.StartsWith("Save/", StringComparison.Ordinal) == true) reporter.Report((double)e.Progress);
            };
            image.ReadPixels(picture.Pixels, new PixelReadSettings((uint)picture.Width, (uint)picture.Height, StorageType.Char, "BGRA"));
            image.ColorSpace = ColorSpace.sRGB; image.Depth = 8; image.Quality = 95;
            image.Density = new Density(picture.DpiX, picture.DpiY, DensityUnit.PixelsPerInch);
            image.Alpha(AlphaAction.Off);
            // JPEG quality and PNG compression are different settings. Quality 95 otherwise selects PNG level 9.
            if (format == MagickFormat.Png24) image.Settings.SetDefine(MagickFormat.Png, "compression-level", 3);
            reporter.Begin(format == MagickFormat.Png24 ? "Encoding PNG" : "Encoding JPEG");
            cancellationToken.ThrowIfCancellationRequested();
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) image.Write(stream, format);
            cancellationToken.ThrowIfCancellationRequested();
            reporter.Begin("Publishing saved image");
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed class StageProgress(IProgress<FinishProgress>? progress)
    {
        private readonly object _gate = new();
        private string _stage = "";
        private long _lastReport;
        private double _lastPercent;
        public void Begin(string stage, double? percentage = null)
        {
            lock (_gate)
            {
                _stage = stage; _lastReport = Environment.TickCount64; _lastPercent = percentage ?? 0;
                progress?.Report(new(stage, percentage));
            }
        }
        public void Report(double percentage)
        {
            if (progress is null) return;
            lock (_gate)
            {
                long now = Environment.TickCount64;
                percentage = Math.Clamp(percentage, 0, 100);
                if (now - _lastReport < 100 || percentage <= _lastPercent) return;
                _lastReport = now; _lastPercent = percentage;
                progress.Report(new(_stage, percentage));
            }
        }
    }

    private readonly record struct BackgroundPlane(double X, double Y)
    {
        public bool IsActive => X != 0 || Y != 0;
        // Only remove the slope, retaining the central sky level and 20% of the estimated gradient.
        public double At(int x, int y, int width, int height) => IsActive
            ? X * ((x + 0.5) / width - 0.5) + Y * ((y + 0.5) / height - 0.5) : 0;
    }

    private static BackgroundPlane EstimateGradient(SampleReader reader, int sourceWidth, int colors, int left, int top,
        int width, int height, int channel, CancellationToken token)
    {
        if (width < 96 || height < 96) return default;
        const int grid = 12, samples = 16;
        var cells = new List<(double X, double Y, double Value)>();
        for (int row = 0; row < grid; row++)
        for (int col = 0; col < grid; col++)
        {
            token.ThrowIfCancellationRequested();
            var values = new List<double>(samples * samples);
            int x0 = col * width / grid, x1 = (col + 1) * width / grid;
            int y0 = row * height / grid, y1 = (row + 1) * height / grid;
            int nx = Math.Min(samples, x1 - x0), ny = Math.Min(samples, y1 - y0);
            for (int sy = 0; sy < ny; sy++)
            for (int sx = 0; sx < nx; sx++)
            {
                int x = x0 + (2 * sx + 1) * (x1 - x0) / (2 * nx);
                int y = y0 + (2 * sy + 1) * (y1 - y0) / (2 * ny);
                int pixel = (top + y) * sourceWidth + left + x;
                if (!reader.IsVisible(pixel, colors)) continue;
                double value = reader.Read(pixel, channel);
                if (double.IsFinite(value)) values.Add(value);
            }
            // Medians reject sparse bright stars. Missing/transparent tiles cannot anchor the model.
            if (values.Count >= nx * ny * 3 / 4)
                cells.Add(((x0 + x1) / (2d * width) - 0.5, (y0 + y1) / (2d * height) - 0.5, Median(values)));
        }
        if (cells.Count < grid * grid * 3 / 4) return default;
        bool[] included = Enumerable.Repeat(true, cells.Count).ToArray();
        double[] coefficients = new double[3];
        double scatter = 0;
        for (int iteration = 0; iteration < 6; iteration++)
        {
            token.ThrowIfCancellationRequested();
            // Three-parameter least squares: constant plus one slope per axis, with no local surface curvature.
            var matrix = new double[3, 4];
            for (int i = 0; i < cells.Count; i++)
            {
                if (!included[i]) continue;
                var cell = cells[i];
                double[] basis = [1, cell.X, cell.Y];
                for (int r = 0; r < 3; r++)
                {
                    for (int c = 0; c < 3; c++) matrix[r, c] += basis[r] * basis[c];
                    matrix[r, 3] += basis[r] * cell.Value;
                }
            }
            for (int pivot = 0; pivot < 3; pivot++)
            {
                double divisor = matrix[pivot, pivot];
                if (Math.Abs(divisor) < 1e-10) return default;
                for (int c = pivot; c < 4; c++) matrix[pivot, c] /= divisor;
                for (int r = 0; r < 3; r++)
                {
                    if (r == pivot) continue;
                    double factor = matrix[r, pivot];
                    for (int c = pivot; c < 4; c++) matrix[r, c] -= factor * matrix[pivot, c];
                }
            }
            for (int c = 0; c < 3; c++) coefficients[c] = matrix[c, 3];
            double[] residuals = cells.Select(cell => cell.Value - coefficients[0] - coefficients[1] * cell.X - coefficients[2] * cell.Y).ToArray();
            double center = Median(residuals.Where((_, i) => included[i]).ToList());
            scatter = 1.4826 * Median(residuals.Where((_, i) => included[i]).Select(v => Math.Abs(v - center)).ToList());
            if (iteration == 5) break;
            // Reject both bright emission/halos and dark clouds rather than fitting their local contrast.
            double limit = Math.Max(1e-10, 2.5 * scatter);
            for (int i = 0; i < cells.Count; i++) included[i] &= Math.Abs(residuals[i] - center) <= limit;
            if (included.Count(v => v) < cells.Count * 0.6) return default;
        }
        // Require a coherent trend supported across the frame, rather than one bright corner or an isolated object.
        for (int quadrant = 0; quadrant < 4; quadrant++)
            if (cells.Where((cell, i) => included[i] && (cell.X >= 0 ? 1 : 0) + (cell.Y >= 0 ? 2 : 0) == quadrant).Count() < 8) return default;
        double span = Math.Abs(coefficients[1]) + Math.Abs(coefficients[2]);
        if (!double.IsFinite(span) || !double.IsFinite(scatter) || span < Math.Max(1e-8, 2 * scatter)) return default;
        return new(coefficients[1] * 0.8, coefficients[2] * 0.8);
    }

    private static bool BlankLine(SampleReader reader, int width, int colors, int x, int y, int length, bool horizontal, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        int populated = 0;
        // Inspect every pixel; require at least 99% empty coverage before trimming a line.
        for (int i = 0; i < length; i++)
        {
            if (i % 4096 == 0) token.ThrowIfCancellationRequested();
            if (reader.HasContent((y + (horizontal ? 0 : i)) * width + x + (horizontal ? i : 0), colors)) populated++;
            if (populated > length / 100) return false;
        }
        return true;
    }
    private static double Median(List<double> values)
    {
        if (values.Count == 0) return double.NaN;
        values.Sort(); int mid = values.Count / 2;
        return values.Count % 2 != 0 ? values[mid] : values[mid - 1] * 0.5 + values[mid] * 0.5;
    }
    private static double EstimateDisplayNoise(byte[] bgra, int width, int height)
    {
        int count = checked(width * height), step = Math.Max(1, (int)Math.Ceiling(count / 65_536d));
        var differences = new List<double>(65_536);
        for (int pixel = 0; pixel < count; pixel += step)
        {
            if (pixel % width == width - 1) continue;
            int i = pixel * 4;
            double delta = 0.2126 * (bgra[i + 6] - bgra[i + 2]) + 0.7152 * (bgra[i + 5] - bgra[i + 1]) + 0.0722 * (bgra[i + 4] - bgra[i]);
            differences.Add(delta);
        }
        double median = Median(differences);
        for (int i = 0; i < differences.Count; i++) differences[i] = Math.Abs(differences[i] - median);
        double sigma = Median(differences) * 1.4826 / Math.Sqrt(2);
        return double.IsFinite(sigma) ? sigma : 0;
    }
    private static byte ToByte(double value) => !double.IsFinite(value) || value <= 0 ? (byte)0 : value >= 1 ? (byte)255 : (byte)Math.Round(value * 255);
    private static (double X, double Y) Dpi(XisfResolution? resolution)
    {
        double factor = resolution?.Unit.Equals("cm", StringComparison.OrdinalIgnoreCase) == true ? 2.54 : 1;
        double x = (resolution?.Horizontal ?? 72) * factor, y = (resolution?.Vertical ?? 72) * factor;
        return (double.IsFinite(x) && x > 0 ? x : 72, double.IsFinite(y) && y > 0 ? y : 72);
    }
    private static void ApplyOrientation(MagickImage image, string orientation)
    {
        string rotation = orientation.Split(';')[0];
        switch (rotation)
        {
            case "90": image.Rotate(-90); break;
            case "-90": image.Rotate(90); break;
            case "180": image.Rotate(180); break;
            case "0": case "flip": break;
            default: throw new NotSupportedException("Unsupported image orientation.");
        }
        if (orientation == "flip" || orientation.EndsWith(";flip", StringComparison.Ordinal)) image.Flop();
        image.ResetPage();
    }

    private readonly ref struct SampleReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private readonly int _pixels, _channels, _size;
        private readonly bool _planar, _big;
        private readonly XisfSampleFormat _format;
        private readonly double _lower, _range;
        public SampleReader(XisfImageData image)
        {
            _data = image.RawData; _pixels = checked(image.Dimensions[0] * image.Dimensions[1]); _channels = image.ChannelCount;
            _planar = image.PixelStorage == XisfPixelLayout.Planar; _big = image.IsBigEndian; _format = image.SampleFormat;
            _size = _format switch { XisfSampleFormat.UInt8 => 1, XisfSampleFormat.UInt16 => 2, XisfSampleFormat.UInt32 or XisfSampleFormat.Float32 => 4, _ => 8 };
            _lower = image.LowerBound ?? 0;
            double upper = image.UpperBound ?? (_format switch { XisfSampleFormat.UInt8 => byte.MaxValue, XisfSampleFormat.UInt16 => ushort.MaxValue, XisfSampleFormat.UInt32 => uint.MaxValue, XisfSampleFormat.UInt64 => ulong.MaxValue, _ => 1 });
            _range = upper - _lower;
            if (!double.IsFinite(_lower) || !double.IsFinite(_range) || _range <= 0) throw new NotSupportedException("The image needs finite increasing sample bounds to create a finished picture.");
        }
        public bool HasSignal(int pixel, int colors)
        {
            if (!IsVisible(pixel, colors)) return false;
            for (int c = 0; c < colors; c++) { double value = Read(pixel, c); if (double.IsFinite(value) && value > 0) return true; }
            return false;
        }
        public double Alpha(int pixel, int colors)
        {
            double alpha = _channels > colors ? Read(pixel, colors) : 1;
            return double.IsFinite(alpha) ? Math.Clamp(alpha, 0, 1) : 0;
        }
        public bool HasContent(int pixel, int colors)
        {
            if (!IsVisible(pixel, colors)) return false;
            for (int c = 0; c < colors; c++) { double value = Read(pixel, c); if (double.IsFinite(value) && value != 0) return true; }
            return false;
        }
        public bool IsVisible(int pixel, int colors)
        {
            if (_channels > colors) { double alpha = Read(pixel, colors); if (!double.IsFinite(alpha) || alpha <= 0) return false; }
            for (int c = 0; c < colors; c++) if (double.IsFinite(Read(pixel, c))) return true;
            return false;
        }
        public double Read(int pixel, int channel)
        {
            long sample = _planar ? (long)channel * _pixels + pixel : (long)pixel * _channels + channel;
            var bytes = _data.Slice(checked((int)(sample * _size)), _size);
            double raw = _format switch
            {
                XisfSampleFormat.UInt8 => bytes[0],
                XisfSampleFormat.UInt16 => _big ? BinaryPrimitives.ReadUInt16BigEndian(bytes) : BinaryPrimitives.ReadUInt16LittleEndian(bytes),
                XisfSampleFormat.UInt32 => _big ? BinaryPrimitives.ReadUInt32BigEndian(bytes) : BinaryPrimitives.ReadUInt32LittleEndian(bytes),
                XisfSampleFormat.UInt64 => _big ? BinaryPrimitives.ReadUInt64BigEndian(bytes) : BinaryPrimitives.ReadUInt64LittleEndian(bytes),
                XisfSampleFormat.Float32 => _big ? BinaryPrimitives.ReadSingleBigEndian(bytes) : BinaryPrimitives.ReadSingleLittleEndian(bytes),
                _ => _big ? BinaryPrimitives.ReadDoubleBigEndian(bytes) : BinaryPrimitives.ReadDoubleLittleEndian(bytes)
            };
            return (raw - _lower) / _range;
        }
    }
}

/// <summary>An explicit finished preview/export artifact. Owned mutable BGRA bytes never alias scientific storage.</summary>
/// <param name="Width">Oriented width in pixels.</param>
/// <param name="Height">Oriented height in pixels.</param>
/// <param name="Pixels">Caller-owned, top-down opaque BGRA bytes with no row padding.</param>
/// <param name="DpiX">Horizontal dots per inch.</param>
/// <param name="DpiY">Vertical dots per inch.</param>
/// <param name="Summary">The automatic choices made to create the sharing picture.</param>
public sealed record FinishedPicture(int Width, int Height, byte[] Pixels, double DpiX, double DpiY, string Summary);

/// <summary>Progress for the current processing stage, rather than an estimated overall completion time.</summary>
/// <param name="Stage">User-facing stage description.</param>
/// <param name="Percentage">Completed stage percentage, or null when the stage cannot measure progress.</param>
public sealed record FinishProgress(string Stage, double? Percentage);

/// <summary>Novice sharing-image restoration switches, enabled by default. Scientific storage is never restored in place.</summary>
/// <param name="SharpenStars">Apply damped local Richardson–Lucy restoration to isolated, resolved, unsaturated stars in linear data.</param>
/// <param name="ReduceStarSpikes">Attenuate supported narrow opposite rays around bright stars, retaining cores and broad halos.</param>
/// <param name="ReduceNoise">Adaptively suppress fine grain and color noise at three scales in stretched scientific images.</param>
public sealed record FinishOptions(bool SharpenStars = true, bool ReduceStarSpikes = true, bool ReduceNoise = true);
