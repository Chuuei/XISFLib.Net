namespace XISFLib;

/// <summary>A rendered display image with caller-owned, top-down, straight-alpha BGRA pixels.</summary>
public sealed class XisfBgra32Image
{
    internal XisfBgra32Image(int width, int height, byte[] pixels, double dpiX, double dpiY)
    { Width = width; Height = height; Pixels = pixels; DpiX = dpiX; DpiY = dpiY; }
    /// <summary>The oriented pixel width.</summary>
    public int Width { get; }
    /// <summary>The oriented pixel height.</summary>
    public int Height { get; }
    /// <summary>The byte count per row, with no padding.</summary>
    public int Stride => checked(Width * 4);
    /// <summary>Independent mutable display bytes; modifications do not affect the scientific image.</summary>
    public byte[] Pixels { get; }
    /// <summary>The horizontal dots per inch, defaulting to 72 when absent or invalid.</summary>
    public double DpiX { get; }
    /// <summary>The vertical dots per inch, defaulting to 72 when absent or invalid.</summary>
    public double DpiY { get; }
}
