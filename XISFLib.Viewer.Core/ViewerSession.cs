namespace XISFLib.Viewer;

/// <summary>Portable file loading and lazy image materialization for the desktop viewer.</summary>
public sealed class ViewerSession
{
    private readonly List<XisfImageData?> _images;

    private ViewerSession(string path, XisfDocument? xisf, FitsDocument? fits, XisfImageData?[] images)
    {
        SourcePath = Path.GetFullPath(path);
        Xisf = xisf;
        Fits = fits;
        _images = images.ToList();
        OriginalImageCount = images.Length;
        Images = _images.AsReadOnly();
    }

    public string SourcePath { get; }
    public XisfDocument? Xisf { get; }
    public FitsDocument? Fits { get; }
    public IReadOnlyList<XisfImageData?> Images { get; }
    public int OriginalImageCount { get; }
    public bool CanCopyOriginal => Xisf is not null && ViewerFileService.IsMonolithicXisf(SourcePath);
    public double ParseMilliseconds { get; private set; }
    public double MaterializeMilliseconds { get; private set; }

    // Call on the viewer's serialized worker operation; session mutation is not thread-safe.
    public XisfImageData GetImage(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _images.Count);
        return _images[index] ??= XisfImageData.FromImage(Xisf!.Images[index]);
    }

    public string GetImageLabel(int index) => $"{index + 1}: {(_images[index]?.Id ?? (index < OriginalImageCount ? Xisf?.Images[index].Id : null)) ?? "unnamed image"}";

    internal int AddDerivedImage(XisfImageData image)
    {
        _images.Add(image);
        return _images.Count - 1;
    }

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
