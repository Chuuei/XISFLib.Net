using System.Buffers.Binary;
using System.Globalization;

namespace XISFLib.Viewer;

/// <summary>Bilinear Bayer reconstruction in stored pixel coordinates, before display orientation.</summary>
public static class ViewerDebayerService
{
    public static IReadOnlyList<string> Patterns { get; } = Array.AsReadOnly(new[] { "RGGB", "BGGR", "GRBG", "GBRG" });

    public static bool IsSupported(XisfImageData? image) => image is not null
        && image.Dimensions.Count == 2 && image.Dimensions[0] >= 2 && image.Dimensions[1] >= 2
        && image.ChannelCount == 1 && (image.ColorSpace is null || image.ColorSpace.Equals("Gray", StringComparison.OrdinalIgnoreCase))
        && image.SampleFormat is XisfSampleFormat.UInt8 or XisfSampleFormat.UInt16 or XisfSampleFormat.UInt32
            or XisfSampleFormat.UInt64 or XisfSampleFormat.Float32 or XisfSampleFormat.Float64;

    /// <summary>Finds the effective tile at stored pixel (0,0). Native CFA takes precedence over FITS conventions.</summary>
    public static (string? Pattern, string Description) Detect(XisfImageData? image)
    {
        if (!IsSupported(image)) return (null, "Select a single-channel, two-dimensional real Gray image (at least 2 × 2).");
        if (image!.ColorFilterArray is { } cfa)
        {
            string native = cfa.Pattern.Trim().ToUpperInvariant();
            return cfa.Width == 2 && cfa.Height == 2 && Patterns.Contains(native)
                ? (native, $"XISF CFA metadata: {native}.")
                : (null, "Unsupported CFA metadata. Only use a manual pattern if this is a 2 × 2 Bayer mosaic.");
        }
        var cards = image.FitsKeywords;
        string? Text(string name) => cards.FirstOrDefault(k => k.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value.Trim().Trim('\'').Trim();
        string? pattern = Text("BAYERPAT")?.ToUpperInvariant();
        if (pattern is null || !Patterns.Contains(pattern)) return (null, "No supported Bayer metadata. Choose the pattern at the first two stored rows.");
        bool Offset(string name, out long value)
        {
            string? text = Text(name);
            value = 0;
            return text is null || long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
        if (!Offset("XBAYROFF", out long x) || !Offset("YBAYROFF", out long y))
            return (null, "Invalid Bayer offsets in metadata. Choose the effective pattern manually.");
        string effective = string.Concat(Enumerable.Range(0, 4).Select(i => pattern[(((i / 2) + (int)(y & 1)) & 1) * 2 + (((i % 2) + (int)(x & 1)) & 1)]));
        return (effective, $"FITS BAYERPAT: {pattern}; offsets ({x}, {y}); effective pattern {effective}.");
    }

    /// <summary>Creates RGB data with unchanged geometry, storage type, byte order and bounds. Source samples are untouched.</summary>
    /// <remarks>Missing colors average the available matching neighbors in a 3 × 3 neighborhood; edges use fewer neighbors.
    /// Integer means round down using an exact wide sum, including UInt64. Floating non-finite values propagate.
    /// This is interpolation, without white balance, calibration or color correction. Stale mosaic/display/thumbnail metadata is removed.</remarks>
    public static XisfImageData Create(XisfImageData source, string pattern, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!IsSupported(source)) throw new NotSupportedException("Debayering requires a single-channel, two-dimensional real Gray image of at least 2 × 2 pixels.");
        if (!Patterns.Contains(pattern)) throw new ArgumentException("Choose RGGB, BGGR, GRBG or GBRG.", nameof(pattern));
        cancellationToken.ThrowIfCancellationRequested();
        int width = source.Dimensions[0], height = source.Dimensions[1];
        int size = source.RawData.Length / checked(width * height);
        byte[] output = new byte[checked(checked(width * height) * checked(3 * size))];
        bool integer = source.SampleFormat is XisfSampleFormat.UInt8 or XisfSampleFormat.UInt16 or XisfSampleFormat.UInt32 or XisfSampleFormat.UInt64;
        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int x = 0; x < width; x++)
            for (int c = 0; c < 3; c++)
            {
                char color = "RGB"[c];
                var destination = output.AsSpan(((y * width + x) * 3 + c) * size, size);
                if (pattern[(y & 1) * 2 + (x & 1)] == color)
                {
                    source.RawData.Slice((y * width + x) * size, size).CopyTo(destination);
                    continue;
                }
                UInt128 sum = 0;
                double floating = 0;
                int count = 0;
                // Count first so floating averages do not overflow merely from summation.
                for (int yy = Math.Max(0, y - 1); yy <= Math.Min(height - 1, y + 1); yy++)
                for (int xx = Math.Max(0, x - 1); xx <= Math.Min(width - 1, x + 1); xx++)
                    if (pattern[(yy & 1) * 2 + (xx & 1)] == color) count++;
                for (int yy = Math.Max(0, y - 1); yy <= Math.Min(height - 1, y + 1); yy++)
                for (int xx = Math.Max(0, x - 1); xx <= Math.Min(width - 1, x + 1); xx++)
                {
                    if (pattern[(yy & 1) * 2 + (xx & 1)] != color) continue;
                    var bytes = source.RawData.Slice((yy * width + xx) * size, size);
                    ulong bits = Read(bytes, source.IsBigEndian);
                    if (integer) sum += bits;
                    else floating += (size == 4 ? BitConverter.Int32BitsToSingle((int)bits) : BitConverter.Int64BitsToDouble((long)bits)) / count;
                }
                ulong result = integer ? (ulong)(sum / (uint)count) : size == 4
                    ? (uint)BitConverter.SingleToInt32Bits((float)floating) : (ulong)BitConverter.DoubleToInt64Bits(floating);
                Write(destination, result, source.IsBigEndian);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        var image = XisfImageData.FromRaw(output, source.Dimensions, 3, source.SampleFormat, XisfPixelLayout.Interleaved, source.IsBigEndian);
        image.Id = (source.Id ?? "image") + "_debayered";
        image.ImageType = source.ImageType; image.Offset = source.Offset; image.Orientation = source.Orientation;
        image.LowerBound = source.LowerBound; image.UpperBound = source.UpperBound; image.Resolution = source.Resolution;
        image.RgbWorkingSpace = source.RgbWorkingSpace;
        foreach (var property in source.Properties)
            // Interpolated physical values must not be requantized through original FITS scaling/BLANK hints.
            if (property.Key is not ("FITS:BITPIX" or "FITS:BSCALE" or "FITS:BZERO" or "FITS:BLANK"))
                image.Properties.Add(property.Key, property.Value);
        foreach (var table in source.Tables) image.Tables.Add(table);
        foreach (var card in source.FitsKeywords)
            if (!new[] { "BAYERPAT", "XBAYROFF", "YBAYROFF" }.Contains(card.Name, StringComparer.OrdinalIgnoreCase)) image.FitsKeywords.Add(card);
        image.FitsKeywords.Add(XisfFitsKeyword.FromValue("HISTORY", $"Bilinear debayer {pattern} in stored pixel coordinates; integer means rounded down."));
        return image;
    }

    private static ulong Read(ReadOnlySpan<byte> bytes, bool big) => bytes.Length switch
    {
        1 => bytes[0],
        2 => big ? BinaryPrimitives.ReadUInt16BigEndian(bytes) : BinaryPrimitives.ReadUInt16LittleEndian(bytes),
        4 => big ? BinaryPrimitives.ReadUInt32BigEndian(bytes) : BinaryPrimitives.ReadUInt32LittleEndian(bytes),
        _ => big ? BinaryPrimitives.ReadUInt64BigEndian(bytes) : BinaryPrimitives.ReadUInt64LittleEndian(bytes)
    };

    private static void Write(Span<byte> bytes, ulong value, bool big)
    {
        switch (bytes.Length)
        {
            case 1: bytes[0] = (byte)value; break;
            case 2: if (big) BinaryPrimitives.WriteUInt16BigEndian(bytes, (ushort)value); else BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)value); break;
            case 4: if (big) BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)value); else BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)value); break;
            default: if (big) BinaryPrimitives.WriteUInt64BigEndian(bytes, value); else BinaryPrimitives.WriteUInt64LittleEndian(bytes, value); break;
        }
    }
}
