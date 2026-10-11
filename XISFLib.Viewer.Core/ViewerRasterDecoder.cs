using ImageMagick;

namespace XISFLib.Viewer;

/// <summary>Frame-zero portable raster import into UInt8 interleaved RGB/RGBA.</summary>
public static class ViewerRasterDecoder
{
    public static XisfImageData Load(string path, bool thumbnail = false)
    {
        using var stream = File.OpenRead(path);
        Span<byte> signature = stackalloc byte[8]; int count = stream.Read(signature); stream.Position = 0;
        MagickFormat format = count >= 8 && signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? MagickFormat.Png
            : count >= 3 && signature[0] == 255 && signature[1] == 216 && signature[2] == 255 ? MagickFormat.Jpeg
            : count >= 6 && (signature[..6].SequenceEqual("GIF87a"u8) || signature[..6].SequenceEqual("GIF89a"u8)) ? MagickFormat.Gif
            : count >= 2 && signature[..2].SequenceEqual("BM"u8) ? MagickFormat.Bmp
            : count >= 4 && ((signature[0] == 73 && signature[1] == 73 && signature[2] is 42 or 43 && signature[3] == 0)
                || (signature[0] == 77 && signature[1] == 77 && signature[2] == 0 && signature[3] is 42 or 43)) ? MagickFormat.Tiff
            : throw new NotSupportedException("Raster import supports PNG, JPEG, TIFF, BMP and GIF images.");
        // Decode the first frame only, including multi-page TIFF and animated GIF.
        using var image = new MagickImage(stream, new MagickReadSettings { FrameIndex = 0, FrameCount = 1, Format = format });
        if (image.Format is not (MagickFormat.Png or MagickFormat.Png8 or MagickFormat.Png24 or MagickFormat.Png32 or MagickFormat.Png48 or MagickFormat.Png64
            or MagickFormat.Jpeg or MagickFormat.Tiff or MagickFormat.Tiff64 or MagickFormat.Bmp or MagickFormat.Bmp2 or MagickFormat.Bmp3 or MagickFormat.Gif))
            throw new NotSupportedException("Raster import supports PNG, JPEG, TIFF, BMP and GIF images.");
        if (image.ColorSpace != ColorSpace.sRGB) image.ColorSpace = ColorSpace.sRGB;
        image.Depth = 8;
        int width = checked((int)image.Width), height = checked((int)image.Height);
        int expectedBytes = checked(width * height * 4);
        using var pixels = image.GetPixels();
        var rgba = pixels.ToByteArray(PixelMapping.RGBA)!;
        if (rgba.Length != expectedBytes) throw new InvalidDataException("Unexpected raster buffer size.");
        bool alpha = false;
        if (!thumbnail) for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] != 255) { alpha = true; break; }
        byte[] samples = rgba;
        if (!alpha)
        {
            samples = new byte[checked(width * height * 3)];
            for (int i = 0, j = 0; i < rgba.Length; i += 4) { samples[j++] = rgba[i]; samples[j++] = rgba[i + 1]; samples[j++] = rgba[i + 2]; }
        }
        var result = XisfImageData.FromSamples(samples, [width, height], alpha ? 4 : 3, XisfPixelLayout.Interleaved);
        result.ColorSpace = "RGB";
        result.Id = thumbnail ? null : Identifier(Path.GetFileNameWithoutExtension(path));
        var density = image.Density;
        double factor = density.Units == DensityUnit.PixelsPerCentimeter ? 2.54 : 1;
        if (!thumbnail && double.IsFinite(density.X) && double.IsFinite(density.Y) && density.X > 0 && density.Y > 0)
            result.Resolution = new(density.X * factor, density.Y * factor, "inch");
        return result;
    }

    public static string Identifier(string value)
    {
        string identifier = new(value.Select(c => char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        if (identifier.Length == 0) return "ImportedImage";
        return char.IsAsciiLetter(identifier[0]) || identifier[0] == '_' ? identifier : $"_{identifier}";
    }
}
