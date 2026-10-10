using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XISFLib;

/// <summary>Creates Windows Forms bitmaps, frozen WPF sources, and BGRA display buffers.</summary>
public static class XisfWindowsExtensions
{
    /// <summary>Creates a Windows Forms bitmap; the caller must dispose the returned bitmap.</summary>
    /// <param name="image">The source image.</param>
    /// <param name="stretch">The display stretch, or null for normalized linear rendering.</param>
    /// <returns>A new bitmap that the caller must dispose.</returns>
    public static Bitmap ToBitmap(this XisfImage image, XisfStretchOptions? stretch = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        (int width, int height, byte[] pixels) = CreateBgra32Pixels(image.ReadRawData(), image.Dimensions, image.ChannelCount,
            image.SampleFormat, image.PixelStorage, image.IsBigEndian, image.LowerBound, image.UpperBound, image.ColorSpace,
            image.Orientation, stretch, image.DisplayFunction, image.RgbWorkingSpace);
        return CreateBitmap(width, height, pixels, image.Resolution);
    }

    /// <summary>Creates a Windows Forms bitmap; the caller must dispose the returned bitmap.</summary>
    /// <param name="image">The source image.</param>
    /// <param name="stretch">The display stretch, or null for normalized linear rendering.</param>
    /// <returns>A new bitmap that the caller must dispose.</returns>
    public static Bitmap ToBitmap(this XisfImageData image, XisfStretchOptions? stretch = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        (int width, int height, byte[] pixels) = CreateBgra32Pixels(image.RawData, image.Dimensions, image.ChannelCount,
            image.SampleFormat, image.PixelStorage, image.IsBigEndian, image.LowerBound ?? 0, GetUpperBound(image), image.ColorSpace,
            image.Orientation ?? "0", stretch, image.DisplayFunction, image.RgbWorkingSpace);
        return CreateBitmap(width, height, pixels, image.Resolution);
    }

    private static Bitmap CreateBitmap(int width, int height, byte[] pixels, XisfResolution? resolution)
    {
        Bitmap bitmap = new(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try
            {
                int sourceStride = checked(width * 4);
                for (int row = 0; row < height; row++)
                    Marshal.Copy(pixels, row * sourceStride, bitmapData.Scan0 + row * bitmapData.Stride, sourceStride);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
            ApplyResolution(bitmap, resolution);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    /// <summary>Creates a frozen WPF bitmap source suitable for use across threads.</summary>
    /// <param name="image">The source image.</param>
    /// <param name="stretch">The display stretch, or null for normalized linear rendering.</param>
    /// <returns>A frozen WPF bitmap source.</returns>
    public static BitmapSource ToBitmapSource(this XisfImage image, XisfStretchOptions? stretch = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        (int width, int height, byte[] pixels) = CreateBgra32Pixels(image.ReadRawData(), image.Dimensions, image.ChannelCount,
            image.SampleFormat, image.PixelStorage, image.IsBigEndian, image.LowerBound, image.UpperBound, image.ColorSpace,
            image.Orientation, stretch, image.DisplayFunction, image.RgbWorkingSpace);
        return CreateBitmapSource(width, height, pixels, image.Resolution);
    }

    /// <summary>Creates a frozen WPF bitmap source suitable for use across threads.</summary>
    /// <param name="image">The source image.</param>
    /// <param name="stretch">The display stretch, or null for normalized linear rendering.</param>
    /// <returns>A frozen WPF bitmap source.</returns>
    public static BitmapSource ToBitmapSource(this XisfImageData image, XisfStretchOptions? stretch = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        (int width, int height, byte[] pixels) = CreateBgra32Pixels(image.RawData, image.Dimensions, image.ChannelCount,
            image.SampleFormat, image.PixelStorage, image.IsBigEndian, image.LowerBound ?? 0, GetUpperBound(image), image.ColorSpace,
            image.Orientation ?? "0", stretch, image.DisplayFunction, image.RgbWorkingSpace);
        return CreateBitmapSource(width, height, pixels, image.Resolution);
    }

    private static BitmapSource CreateBitmapSource(int width, int height, byte[] pixels, XisfResolution? resolution)
    {
        (double dpiX, double dpiY) = GetDpi(resolution);
        BitmapSource bitmap = BitmapSource.Create(width, height, dpiX, dpiY, PixelFormats.Bgra32, null, pixels, checked(width * 4));
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Returns a newly allocated BGRA display buffer with optional stretching.</summary>
    /// <param name="image">The source image.</param>
    /// <param name="stretch">The display stretch, or null for normalized linear rendering.</param>
    /// <returns>A newly allocated BGRA display buffer.</returns>
    public static byte[] ToBgra32Pixels(this XisfImage image, XisfStretchOptions? stretch = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        return CreateBgra32Pixels(image.ReadRawData(), image.Dimensions, image.ChannelCount, image.SampleFormat, image.PixelStorage,
            image.IsBigEndian, image.LowerBound, image.UpperBound, image.ColorSpace, image.Orientation, stretch, image.DisplayFunction, image.RgbWorkingSpace).Pixels;
    }

    /// <summary>Returns a newly allocated BGRA display buffer with optional stretching.</summary>
    /// <param name="image">The source image.</param>
    /// <param name="stretch">The display stretch, or null for normalized linear rendering.</param>
    /// <returns>A newly allocated BGRA display buffer.</returns>
    public static byte[] ToBgra32Pixels(this XisfImageData image, XisfStretchOptions? stretch = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        return CreateBgra32Pixels(image.RawData, image.Dimensions, image.ChannelCount, image.SampleFormat, image.PixelStorage,
            image.IsBigEndian, image.LowerBound ?? 0, GetUpperBound(image), image.ColorSpace, image.Orientation ?? "0", stretch,
            image.DisplayFunction, image.RgbWorkingSpace).Pixels;
    }

    private static double GetUpperBound(XisfImageData image) => image.UpperBound ?? (image.SampleFormat switch
    {
        XisfSampleFormat.UInt8 => byte.MaxValue,
        XisfSampleFormat.UInt16 => ushort.MaxValue,
        XisfSampleFormat.UInt32 => uint.MaxValue,
        XisfSampleFormat.UInt64 => ulong.MaxValue,
        _ => double.NaN
    });

    private static (int Width, int Height, byte[] Pixels) CreateBgra32Pixels(ReadOnlySpan<byte> data,
        IReadOnlyList<int> dimensions,
        int channelCount, XisfSampleFormat sampleFormat, XisfPixelLayout pixelStorage, bool isBigEndian,
        double lowerBound,
        double upperBound, string? colorSpace, string orientation, XisfStretchOptions? stretch,
        XisfDisplayFunction? displayFunction, XisfRgbWorkingSpace? workingSpace)
    {
        if (dimensions.Count != 2)
            throw new NotSupportedException("Windows Forms and WPF bitmaps require a two-dimensional XISF image.");
        if (sampleFormat is XisfSampleFormat.Complex32 or XisfSampleFormat.Complex64)
            throw new NotSupportedException("Complex XISF samples cannot be represented as a bitmap.");
        bool isLab = colorSpace?.Equals("CIELab", StringComparison.OrdinalIgnoreCase) == true;
        bool isGray = colorSpace is null || colorSpace.Equals("Gray", StringComparison.OrdinalIgnoreCase);
        if (!isLab && !isGray && !colorSpace!.Equals("RGB", StringComparison.OrdinalIgnoreCase)) throw new NotSupportedException("Unsupported image color space.");
        int nominalChannels = isGray ? 1 : 3;
        if (channelCount < nominalChannels) throw new XisfFormatException("Channel count is inconsistent with the color space.");

        int width = dimensions[0];
        int height = dimensions[1];
        int pixelCount = checked(width * height);
        double labMaximum = sampleFormat switch { XisfSampleFormat.UInt8 => byte.MaxValue, XisfSampleFormat.UInt16 => ushort.MaxValue, XisfSampleFormat.UInt32 => uint.MaxValue, XisfSampleFormat.UInt64 => ulong.MaxValue, _ => 1 };
        NormalizedSampleReader samples = new(data, pixelCount, channelCount, sampleFormat, pixelStorage, isBigEndian, isLab ? 0 : lowerBound, isLab ? labMaximum : upperBound);
        int colorChannelCount = isLab ? 1 : nominalChannels;
        StretchTransform[] transforms = CreateStretchTransforms(samples, colorChannelCount, stretch, displayFunction);
        if (isLab && stretch?.UseDisplayFunction == true && displayFunction is not null)
            transforms[0] = new StretchTransform(displayFunction.Shadows[3], displayFunction.Midtones[3], displayFunction.Highlights[3], displayFunction.LowRange[3], displayFunction.HighRange[3]);
        byte[] pixels = new byte[checked(pixelCount * 4)];
        for (int pixel = 0; pixel < pixelCount; pixel++)
        {
            byte red;
            byte green;
            byte blue;
            byte alpha;
            if (isGray)
            {
                red = green = blue = ToByte(transforms[0].Apply(samples.Read(pixel, 0)));
                alpha = channelCount > 1 ? ToByte(samples.Read(pixel, 1)) : byte.MaxValue;
            }
            else
            {
                if (isLab)
                {
                    var rgb = XisfColorSpace.LabToRgb(transforms[0].Apply(samples.Read(pixel, 0)), samples.Read(pixel, 1), samples.Read(pixel, 2), workingSpace);
                    double rgbMaximum = upperBound / labMaximum, rgbMinimum = lowerBound / labMaximum;
                    (red, green, blue) = (ToByte((rgb.Red - rgbMinimum) / (rgbMaximum - rgbMinimum)), ToByte((rgb.Green - rgbMinimum) / (rgbMaximum - rgbMinimum)), ToByte((rgb.Blue - rgbMinimum) / (rgbMaximum - rgbMinimum)));
                }
                else
                {
                    red = ToByte(transforms[0].Apply(samples.Read(pixel, 0)));
                    green = ToByte(transforms[1].Apply(samples.Read(pixel, 1)));
                    blue = ToByte(transforms[2].Apply(samples.Read(pixel, 2)));
                }
                alpha = channelCount > 3 ? ToByte(samples.Read(pixel, 3)) : byte.MaxValue;
            }

            int target = pixel * 4;
            pixels[target] = blue;
            pixels[target + 1] = green;
            pixels[target + 2] = red;
            pixels[target + 3] = alpha;
        }
        return ApplyOrientation(width, height, pixels, orientation);
    }

    private static (int Width, int Height, byte[] Pixels) ApplyOrientation(int width, int height, byte[] source, string orientation)
    {
        if (orientation == "0") return (width, height, source);
        bool quarterTurn = orientation.StartsWith("90", StringComparison.Ordinal) || orientation.StartsWith("-90", StringComparison.Ordinal);
        int targetWidth = quarterTurn ? height : width;
        int targetHeight = quarterTurn ? width : height;
        byte[] target = new byte[source.Length];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            (int tx, int ty) = orientation.Split(';')[0] switch
            {
                "90" => (y, width - 1 - x),
                "-90" => (height - 1 - y, x),
                "180" => (width - 1 - x, height - 1 - y),
                _ => (x, y)
            };
            if (orientation.EndsWith(";flip", StringComparison.Ordinal) || orientation == "flip") tx = targetWidth - 1 - tx;
            Buffer.BlockCopy(source, (y * width + x) * 4, target, (ty * targetWidth + tx) * 4, 4);
        }
        return (targetWidth, targetHeight, target);
    }

    private static StretchTransform[] CreateStretchTransforms(NormalizedSampleReader samples, int colorChannelCount,
        XisfStretchOptions? options, XisfDisplayFunction? displayFunction)
    {
        StretchTransform[] transforms = Enumerable.Repeat(StretchTransform.Identity, colorChannelCount).ToArray();
        if (options is null)
            return transforms;
        if (options.UseDisplayFunction)
        {
            if (displayFunction is null) return transforms;
            if (new[] { displayFunction.Midtones.Count, displayFunction.Shadows.Count, displayFunction.Highlights.Count,
                    displayFunction.LowRange.Count, displayFunction.HighRange.Count }.Any(count => count != 4))
                throw new XisfFormatException("Display function vectors must have four components.");
            for (int channel = 0; channel < colorChannelCount; channel++)
                transforms[channel] = new StretchTransform(displayFunction.Shadows[channel], displayFunction.Midtones[channel],
                    displayFunction.Highlights[channel], displayFunction.LowRange[channel], displayFunction.HighRange[channel]);
            return transforms;
        }
        if (!double.IsFinite(options.TargetBackground) || options.TargetBackground is <= 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(options), "The target background must be between zero and one.");
        if (!double.IsFinite(options.ShadowsClipping) || options.ShadowsClipping > 0)
            throw new ArgumentOutOfRangeException(nameof(options), "The shadows clipping value must be finite and nonpositive.");

        double[][] channels = new double[colorChannelCount][];
        int stride = Math.Max(1, (int)Math.Ceiling(samples.PixelCount / 262144.0));
        for (int channel = 0; channel < colorChannelCount; channel++)
        {
            List<double> values = [];
            for (int pixel = 0; pixel < samples.PixelCount; pixel += stride) values.Add(samples.Read(pixel, channel));
            channels[channel] = values.ToArray();
        }
        XisfDisplayFunction adaptive = XisfDisplayFunctions.CreateAdaptive(channels, options.TargetBackground, options.ShadowsClipping, options.LinkChannels);
        for (int channel = 0; channel < colorChannelCount; channel++)
            transforms[channel] = new StretchTransform(adaptive.Shadows[channel], adaptive.Midtones[channel], adaptive.Highlights[channel], 0, 1);
        return transforms;
    }

    private static byte ToByte(double value)
    {
        if (double.IsNaN(value) || value <= 0) return 0;
        if (value >= 1) return byte.MaxValue;
        return (byte)Math.Round(value * byte.MaxValue, MidpointRounding.AwayFromZero);
    }

    private static void ApplyResolution(Bitmap bitmap, XisfResolution? resolution)
    {
        (double dpiX, double dpiY) = GetDpi(resolution);
        bitmap.SetResolution((float)dpiX, (float)dpiY);
    }

    private static (double Horizontal, double Vertical) GetDpi(XisfResolution? resolution)
    {
        if (resolution is null) return (72, 72);
        double scale = resolution.Unit.Equals("cm", StringComparison.OrdinalIgnoreCase) ? 2.54 : 1;
        double horizontal = resolution.Horizontal * scale;
        double vertical = resolution.Vertical * scale;
        return horizontal > 0 && vertical > 0 && double.IsFinite(horizontal) && double.IsFinite(vertical)
            ? (horizontal, vertical)
            : (72, 72);
    }

    private readonly ref struct NormalizedSampleReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private readonly int _channelCount;
        private readonly int _bytesPerSample;
        private readonly XisfSampleFormat _sampleFormat;
        private readonly XisfPixelLayout _pixelStorage;
        private readonly bool _isBigEndian;
        private readonly double _lowerBound;
        private readonly double _range;

        public NormalizedSampleReader(ReadOnlySpan<byte> data, int pixelCount, int channelCount, XisfSampleFormat sampleFormat,
            XisfPixelLayout pixelStorage, bool isBigEndian, double lowerBound, double upperBound)
        {
            _data = data;
            PixelCount = pixelCount;
            _channelCount = channelCount;
            _bytesPerSample = sampleFormat switch
            {
                XisfSampleFormat.UInt8 => 1,
                XisfSampleFormat.UInt16 => 2,
                XisfSampleFormat.UInt32 or XisfSampleFormat.Float32 => 4,
                XisfSampleFormat.UInt64 or XisfSampleFormat.Float64 => 8,
                _ => throw new ArgumentOutOfRangeException(nameof(sampleFormat))
            };
            _sampleFormat = sampleFormat;
            _pixelStorage = pixelStorage;
            _isBigEndian = isBigEndian;
            _lowerBound = lowerBound;
            _range = upperBound - lowerBound;
        }

        public int PixelCount { get; }

        public double Read(int pixel, int channel)
        {
            if (_range == 0)
                return 0;
            int sample = _pixelStorage == XisfPixelLayout.Planar
                ? checked(channel * PixelCount + pixel)
                : checked(pixel * _channelCount + channel);
            ReadOnlySpan<byte> bytes = _data.Slice(checked(sample * _bytesPerSample), _bytesPerSample);
            double value = _sampleFormat switch
            {
                XisfSampleFormat.UInt8 => bytes[0],
                XisfSampleFormat.UInt16 => _isBigEndian ? BinaryPrimitives.ReadUInt16BigEndian(bytes) : BinaryPrimitives.ReadUInt16LittleEndian(bytes),
                XisfSampleFormat.UInt32 => _isBigEndian ? BinaryPrimitives.ReadUInt32BigEndian(bytes) : BinaryPrimitives.ReadUInt32LittleEndian(bytes),
                XisfSampleFormat.UInt64 => _isBigEndian ? BinaryPrimitives.ReadUInt64BigEndian(bytes) : BinaryPrimitives.ReadUInt64LittleEndian(bytes),
                XisfSampleFormat.Float32 => BitConverter.Int32BitsToSingle(_isBigEndian
                    ? BinaryPrimitives.ReadInt32BigEndian(bytes)
                    : BinaryPrimitives.ReadInt32LittleEndian(bytes)),
                XisfSampleFormat.Float64 => BitConverter.Int64BitsToDouble(_isBigEndian
                    ? BinaryPrimitives.ReadInt64BigEndian(bytes)
                    : BinaryPrimitives.ReadInt64LittleEndian(bytes)),
                _ => throw new InvalidOperationException()
            };
            return (value - _lowerBound) / _range;
        }
    }

    private readonly record struct StretchTransform(double Shadows, double Midtones, double Highlights, double LowRange, double HighRange)
    {
        public static StretchTransform Identity { get; } = new(0, 0.5, 1, 0, 1);

        public double Apply(double value)
        {
            if (double.IsNaN(value)) return 0;
            return XisfDisplayFunctions.Evaluate(value, Midtones, Shadows, Highlights, LowRange, HighRange);
        }
    }
}
