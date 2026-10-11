namespace XISFLib.Viewer;

/// <summary>The portable file-loading and lazy image-materialization behavior shared by both viewers.</summary>
public sealed class ViewerSession
{
    private readonly XisfImageData?[] _images;

    private ViewerSession(string path, XisfDocument? xisf, FitsDocument? fits, XisfImageData?[] images)
    {
        SourcePath = Path.GetFullPath(path);
        Xisf = xisf;
        Fits = fits;
        _images = images;
        Images = Array.AsReadOnly(images);
    }

    public string SourcePath { get; }
    public XisfDocument? Xisf { get; }
    public FitsDocument? Fits { get; }
    public IReadOnlyList<XisfImageData?> Images { get; }
    public bool CanCopyOriginal => Xisf is not null && ViewerFileService.IsMonolithicXisf(SourcePath);
    public double ParseMilliseconds { get; private set; }
    public double MaterializeMilliseconds { get; private set; }

    // Call on the viewer's serialized worker operation; session mutation is not thread-safe.
    public XisfImageData GetImage(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _images.Length);
        return _images[index] ??= XisfImageData.FromImage(Xisf!.Images[index]);
    }

    public string GetImageLabel(int index) => $"{index + 1}: {(Xisf?.Images[index].Id ?? _images[index]?.Id) ?? "unnamed image"}";

    public static ViewerSession Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        if (IsFitsPath(path))
        {
            FitsDocument fits = FitsReader.Load(path);
            double parsed = timer.Elapsed.TotalMilliseconds;
            var converted = FitsConverter.ToXisf(fits, new()
            { AxisInterpretation = FitsAxisInterpretation.RgbOrRgba, LossPolicy = ConversionLossPolicy.Allow });
            if (converted.Count == 0) throw new FitsFormatException("The FITS document does not contain a convertible image HDU.");
            return new(path, null, fits, converted.Select(image => (XisfImageData?)image).ToArray())
            { ParseMilliseconds = parsed, MaterializeMilliseconds = timer.Elapsed.TotalMilliseconds - parsed };
        }

        var xisf = XisfReader.Load(path);
        double parseTime = timer.Elapsed.TotalMilliseconds;
        if (xisf.Images.Count == 0) throw new XisfFormatException("The XISF document does not contain an image.");
        var images = new XisfImageData?[xisf.Images.Count];
        images[0] = XisfImageData.FromImage(xisf.Images[0]);
        return new(path, xisf, null, images) { ParseMilliseconds = parseTime, MaterializeMilliseconds = timer.Elapsed.TotalMilliseconds - parseTime };
    }

    public static ViewerSession FromRaster(string path, XisfImageData image) => new(path, null, null, [image]);

    public static bool IsFitsPath(string path) => Path.GetExtension(path).ToLowerInvariant() is ".fit" or ".fits" or ".fts";
}
