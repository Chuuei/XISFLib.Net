using System.Buffers.Binary;
using System.Numerics;

namespace XISFLib;

public sealed class XisfStatisticsOptions
{
    public bool Normalize { get; set; }
    public int MaximumSamplesPerChannel { get; set; } = 1_000_000;
}

public sealed record XisfChannelStatistics(
    int Channel,
    long SampleCount,
    long FiniteCount,
    long NonFiniteCount,
    double Minimum,
    double Maximum,
    double Mean,
    double StandardDeviation,
    double Median,
    double MedianAbsoluteDeviation);

public sealed record XisfHistogram(
    int Channel,
    double LowerBound,
    double UpperBound,
    IReadOnlyList<long> Bins,
    long UnderflowCount,
    long OverflowCount,
    long NonFiniteCount);

public static class XisfImageExtensions
{
    public static T[] ReadRegion<T>(this XisfImage image, int x, int y, int width, int height,
        XisfPixelLayout layout = XisfPixelLayout.Interleaved) where T : INumberBase<T>
    {
        ArgumentNullException.ThrowIfNull(image);
        return ReadRegionCore<T>(image.ReadRawData(), image.Dimensions, image.ChannelCount, image.SampleFormat,
            image.PixelStorage, image.IsBigEndian, x, y, width, height, layout);
    }

    public static T[] ReadRegion<T>(this XisfImageData image, int x, int y, int width, int height,
        XisfPixelLayout layout = XisfPixelLayout.Interleaved) where T : INumberBase<T>
    {
        ArgumentNullException.ThrowIfNull(image);
        return ReadRegionCore<T>(image.RawData, image.Dimensions, image.ChannelCount, image.SampleFormat,
            image.PixelStorage, image.IsBigEndian, x, y, width, height, layout);
    }

    public static T[] ReadChannel<T>(this XisfImage image, int channel) where T : INumberBase<T>
    {
        ArgumentNullException.ThrowIfNull(image);
        return ReadChannelCore<T>(image.ReadRawData(), image.Dimensions, image.ChannelCount, image.SampleFormat,
            image.PixelStorage, image.IsBigEndian, channel);
    }

    public static T[] ReadChannel<T>(this XisfImageData image, int channel) where T : INumberBase<T>
    {
        ArgumentNullException.ThrowIfNull(image);
        return ReadChannelCore<T>(image.RawData, image.Dimensions, image.ChannelCount, image.SampleFormat,
            image.PixelStorage, image.IsBigEndian, channel);
    }

    public static IReadOnlyList<XisfChannelStatistics> CalculateStatistics(this XisfImage image, XisfStatisticsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        return CalculateStatisticsCore(image.ReadRawData(), image.Dimensions, image.ChannelCount, image.SampleFormat,
            image.PixelStorage, image.IsBigEndian, image.LowerBound, image.UpperBound, options);
    }

    public static IReadOnlyList<XisfChannelStatistics> CalculateStatistics(this XisfImageData image, XisfStatisticsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        return CalculateStatisticsCore(image.RawData, image.Dimensions, image.ChannelCount, image.SampleFormat,
            image.PixelStorage, image.IsBigEndian, image.LowerBound ?? 0, image.UpperBound ?? IntegerMaximum(image.SampleFormat), options);
    }

    public static XisfHistogram CalculateHistogram(this XisfImage image, int channel, int binCount = 256,
        double? lowerBound = null, double? upperBound = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        return CalculateHistogramCore(image.ReadRawData(), image.Dimensions, image.ChannelCount, image.SampleFormat,
            image.PixelStorage, image.IsBigEndian, channel, binCount, lowerBound ?? image.LowerBound, upperBound ?? image.UpperBound);
    }

    public static XisfHistogram CalculateHistogram(this XisfImageData image, int channel, int binCount = 256,
        double? lowerBound = null, double? upperBound = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        return CalculateHistogramCore(image.RawData, image.Dimensions, image.ChannelCount, image.SampleFormat,
            image.PixelStorage, image.IsBigEndian, channel, binCount, lowerBound ?? image.LowerBound ?? 0,
            upperBound ?? image.UpperBound ?? IntegerMaximum(image.SampleFormat));
    }

    private static T[] ReadRegionCore<T>(ReadOnlySpan<byte> data, IReadOnlyList<int> dimensions, int channels,
        XisfSampleFormat format, XisfPixelLayout storage, bool bigEndian, int x, int y, int width, int height,
        XisfPixelLayout output) where T : INumberBase<T>
    {
        ValidateRealTwoDimensional(dimensions, channels, format);
        ValidateRegion(dimensions, x, y, width, height);
        T[] result = new T[checked(width * height * channels)];
        int sourceWidth = dimensions[0];
        long pixelCount = checked((long)sourceWidth * dimensions[1]);
        int sampleSize = XisfImageData.BytesPerSample(format);
        for (int row = 0; row < height; row++)
        for (int column = 0; column < width; column++)
        for (int channel = 0; channel < channels; channel++)
        {
            long pixel = checked((long)(y + row) * sourceWidth + x + column);
            long sourceSample = storage == XisfPixelLayout.Planar ? channel * pixelCount + pixel : pixel * channels + channel;
            int destinationPixel = row * width + column;
            int destination = output == XisfPixelLayout.Planar
                ? channel * width * height + destinationPixel
                : destinationPixel * channels + channel;
            result[destination] = T.CreateChecked(ReadReal(data.Slice(checked((int)(sourceSample * sampleSize)), sampleSize), format, bigEndian));
        }
        return result;
    }

    private static T[] ReadChannelCore<T>(ReadOnlySpan<byte> data, IReadOnlyList<int> dimensions, int channels,
        XisfSampleFormat format, XisfPixelLayout storage, bool bigEndian, int channel) where T : INumberBase<T>
    {
        ValidateRealTwoDimensional(dimensions, channels, format);
        ValidateChannel(channel, channels);
        int pixels = checked(dimensions[0] * dimensions[1]);
        T[] result = new T[pixels];
        int sampleSize = XisfImageData.BytesPerSample(format);
        for (int pixel = 0; pixel < pixels; pixel++)
        {
            long sourceSample = storage == XisfPixelLayout.Planar ? (long)channel * pixels + pixel : (long)pixel * channels + channel;
            result[pixel] = T.CreateChecked(ReadReal(data.Slice(checked((int)(sourceSample * sampleSize)), sampleSize), format, bigEndian));
        }
        return result;
    }

    private static IReadOnlyList<XisfChannelStatistics> CalculateStatisticsCore(ReadOnlySpan<byte> data,
        IReadOnlyList<int> dimensions, int channels, XisfSampleFormat format, XisfPixelLayout storage, bool bigEndian,
        double lowerBound, double upperBound, XisfStatisticsOptions? options)
    {
        ValidateRealTwoDimensional(dimensions, channels, format);
        options ??= new XisfStatisticsOptions();
        if (options.MaximumSamplesPerChannel <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "The maximum sample count must be positive.");
        int pixels = checked(dimensions[0] * dimensions[1]);
        int step = Math.Max(1, (int)Math.Ceiling(pixels / (double)options.MaximumSamplesPerChannel));
        int capacity = checked((pixels + step - 1) / step);
        int sampleSize = XisfImageData.BytesPerSample(format);
        List<XisfChannelStatistics> result = new(channels);
        for (int channel = 0; channel < channels; channel++)
        {
            List<double> finite = new(capacity);
            long nonFinite = 0;
            double mean = 0;
            double m2 = 0;
            for (int pixel = 0; pixel < pixels; pixel += step)
            {
                long sourceSample = storage == XisfPixelLayout.Planar ? (long)channel * pixels + pixel : (long)pixel * channels + channel;
                double value = ReadReal(data.Slice(checked((int)(sourceSample * sampleSize)), sampleSize), format, bigEndian);
                if (options.Normalize)
                    value = upperBound == lowerBound ? 0 : (value - lowerBound) / (upperBound - lowerBound);
                if (!double.IsFinite(value))
                {
                    nonFinite++;
                    continue;
                }
                finite.Add(value);
                double delta = value - mean;
                mean += delta / finite.Count;
                m2 += delta * (value - mean);
            }
            finite.Sort();
            double median = Median(finite);
            double mad = 0;
            if (finite.Count > 0)
            {
                double[] deviations = finite.Select(value => Math.Abs(value - median)).ToArray();
                Array.Sort(deviations);
                mad = Median(deviations);
            }
            result.Add(new XisfChannelStatistics(channel, (pixels + step - 1L) / step, finite.Count, nonFinite,
                finite.Count == 0 ? double.NaN : finite[0], finite.Count == 0 ? double.NaN : finite[^1],
                finite.Count == 0 ? double.NaN : mean, finite.Count < 2 ? 0 : Math.Sqrt(m2 / (finite.Count - 1)), median, mad));
        }
        return result;
    }

    private static XisfHistogram CalculateHistogramCore(ReadOnlySpan<byte> data, IReadOnlyList<int> dimensions,
        int channels, XisfSampleFormat format, XisfPixelLayout storage, bool bigEndian, int channel, int binCount,
        double lowerBound, double upperBound)
    {
        ValidateRealTwoDimensional(dimensions, channels, format);
        ValidateChannel(channel, channels);
        if (binCount <= 0) throw new ArgumentOutOfRangeException(nameof(binCount));
        if (!double.IsFinite(lowerBound) || !double.IsFinite(upperBound) || lowerBound >= upperBound)
            throw new ArgumentException("Histogram bounds must be finite and increasing.");
        long[] bins = new long[binCount];
        long underflow = 0, overflow = 0, nonFinite = 0;
        int pixels = checked(dimensions[0] * dimensions[1]);
        int sampleSize = XisfImageData.BytesPerSample(format);
        for (int pixel = 0; pixel < pixels; pixel++)
        {
            long sourceSample = storage == XisfPixelLayout.Planar ? (long)channel * pixels + pixel : (long)pixel * channels + channel;
            double value = ReadReal(data.Slice(checked((int)(sourceSample * sampleSize)), sampleSize), format, bigEndian);
            if (!double.IsFinite(value)) nonFinite++;
            else if (value < lowerBound) underflow++;
            else if (value > upperBound) overflow++;
            else
            {
                int index = value == upperBound ? binCount - 1 : (int)((value - lowerBound) / (upperBound - lowerBound) * binCount);
                bins[index]++;
            }
        }
        return new XisfHistogram(channel, lowerBound, upperBound, bins, underflow, overflow, nonFinite);
    }

    private static double ReadReal(ReadOnlySpan<byte> data, XisfSampleFormat format, bool bigEndian) => format switch
    {
        XisfSampleFormat.UInt8 => data[0],
        XisfSampleFormat.UInt16 => bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(data) : BinaryPrimitives.ReadUInt16LittleEndian(data),
        XisfSampleFormat.UInt32 => bigEndian ? BinaryPrimitives.ReadUInt32BigEndian(data) : BinaryPrimitives.ReadUInt32LittleEndian(data),
        XisfSampleFormat.UInt64 => bigEndian ? BinaryPrimitives.ReadUInt64BigEndian(data) : BinaryPrimitives.ReadUInt64LittleEndian(data),
        XisfSampleFormat.Float32 => BitConverter.Int32BitsToSingle(bigEndian ? BinaryPrimitives.ReadInt32BigEndian(data) : BinaryPrimitives.ReadInt32LittleEndian(data)),
        XisfSampleFormat.Float64 => BitConverter.Int64BitsToDouble(bigEndian ? BinaryPrimitives.ReadInt64BigEndian(data) : BinaryPrimitives.ReadInt64LittleEndian(data)),
        _ => throw new InvalidOperationException("Complex images require complex-specific processing.")
    };

    private static void ValidateRealTwoDimensional(IReadOnlyList<int> dimensions, int channels, XisfSampleFormat format)
    {
        if (dimensions.Count != 2) throw new NotSupportedException("Region and analysis operations currently require a two-dimensional image.");
        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
        if (format is XisfSampleFormat.Complex32 or XisfSampleFormat.Complex64)
            throw new NotSupportedException("Region and analysis operations for complex images are not supported.");
    }

    private static void ValidateRegion(IReadOnlyList<int> dimensions, int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || width <= 0 || height <= 0 || x > dimensions[0] - width || y > dimensions[1] - height)
            throw new ArgumentOutOfRangeException(nameof(x), "The requested region is outside the image bounds.");
    }

    private static void ValidateChannel(int channel, int channels)
    {
        if ((uint)channel >= (uint)channels) throw new ArgumentOutOfRangeException(nameof(channel));
    }

    private static double Median(IReadOnlyList<double> sorted) => sorted.Count switch
    {
        0 => double.NaN,
        _ when (sorted.Count & 1) != 0 => sorted[sorted.Count / 2],
        _ => (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2
    };

    private static double IntegerMaximum(XisfSampleFormat format) => format switch
    {
        XisfSampleFormat.UInt8 => byte.MaxValue,
        XisfSampleFormat.UInt16 => ushort.MaxValue,
        XisfSampleFormat.UInt32 => uint.MaxValue,
        XisfSampleFormat.UInt64 => ulong.MaxValue,
        _ => 1
    };
}