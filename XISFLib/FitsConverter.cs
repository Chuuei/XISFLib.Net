using System.Buffers.Binary;
using System.Globalization;

namespace XISFLib;

/// <summary>Controls axis interpretation and preservation of structural keywords.</summary>
public sealed class FitsConversionOptions
{
    /// <summary>Whether a third axis of length three or four is interpreted as color channels.</summary>
    public bool TreatThirdAxisAsColorChannels { get; set; } = true;
    /// <summary>Whether structural FITS cards are also preserved as XISF FITS keywords.</summary>
    public bool IncludeStructuralKeywords { get; set; }
}

/// <summary>Converts supported images and metadata between FITS and XISF.</summary>
public static class FitsConverter
{
    private static readonly HashSet<string> StructuralKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SIMPLE", "XTENSION", "BITPIX", "NAXIS", "PCOUNT", "GCOUNT", "EXTEND", "BSCALE", "BZERO", "BLANK", "CHECKSUM", "DATASUM", "END"
    };

    /// <summary>Converts FITS images to editable XISF images and retains supported storage metadata.</summary>
    /// <param name="document">The document to read, validate, sign, or serialize.</param>
    /// <param name="options">The operation settings, or null to use defaults.</param>
    /// <returns>The converted image or images with known metadata.</returns>
    /// <exception cref="NotSupportedException">Signed 64-bit FITS storage cannot be represented exactly by the floating-point conversion path.</exception>
    public static IReadOnlyList<XisfImageData> ToXisf(FitsDocument document, FitsConversionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= new FitsConversionOptions();
        List<XisfImageData> result = [];
        FitsHeader? primaryMetadata = document.Hdus[0].Content is null ? document.Hdus[0].Header : null;
        foreach (FitsHdu hdu in document.Hdus)
        {
            if (hdu.Content is not FitsImage image) continue;
            XisfImageData converted = ToXisf(image, options);
            AddKeywords(converted, hdu.Header, options);
            if (result.Count == 0 && primaryMetadata is not null && !ReferenceEquals(primaryMetadata, hdu.Header))
                AddKeywords(converted, primaryMetadata, options);
            result.Add(converted);
        }
        if (result.Count > 0)
            foreach (FitsHdu hdu in document.Hdus.Where(hdu => hdu.Content is FitsTable))
                result[0].Tables.Add(ToXisfTable((FitsTable)hdu.Content!, hdu.Header));
        return result;
    }

    /// <summary>Converts FITS images to editable XISF images and retains supported storage metadata.</summary>
    /// <param name="image">The source image.</param>
    /// <param name="options">The operation settings, or null to use defaults.</param>
    /// <returns>The converted image or images with known metadata.</returns>
    /// <exception cref="NotSupportedException">Signed 64-bit FITS storage cannot be represented exactly by the floating-point conversion path.</exception>
    public static XisfImageData ToXisf(FitsImage image, FitsConversionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        options ??= new FitsConversionOptions();
        int[] dimensions = image.Dimensions.ToArray();
        int channels = 1;
        if (options.TreatThirdAxisAsColorChannels && dimensions.Length == 3 && dimensions[2] is 3 or 4)
        {
            channels = dimensions[2];
            dimensions = dimensions[..2];
        }

        XisfImageData result;
        if (image.BScale == 1 && image.Blank is null && image.BitPix == 8 && image.BZero == 0)
            result = XisfImageData.FromOwnedRaw(image.RawData, dimensions, channels, XisfSampleFormat.UInt8, XisfPixelLayout.Planar, true);
        else if (image.BScale == 1 && image.Blank is null && image.BitPix == 16 && image.BZero == 32768d)
            result = XisfImageData.FromOwnedRaw(ConvertUnsigned(image.RawData, 2), dimensions, channels, XisfSampleFormat.UInt16, XisfPixelLayout.Planar, true);
        else if (image.BScale == 1 && image.Blank is null && image.BitPix == 32 && image.BZero == 2147483648d)
            result = XisfImageData.FromOwnedRaw(ConvertUnsigned(image.RawData, 4), dimensions, channels, XisfSampleFormat.UInt32, XisfPixelLayout.Planar, true);
        else if (image.BScale == 1 && image.Blank is null && image.BitPix == 64 && image.BZero == 9223372036854775808d)
            result = XisfImageData.FromOwnedRaw(ConvertUnsigned(image.RawData, 8), dimensions, channels, XisfSampleFormat.UInt64, XisfPixelLayout.Planar, true);
        else if (image.BScale == 1 && image.BZero == 0 && image.Blank is null && image.BitPix is -32 or -64)
            result = XisfImageData.FromOwnedRaw(image.RawData, dimensions, channels, image.BitPix == -32 ? XisfSampleFormat.Float32 : XisfSampleFormat.Float64, XisfPixelLayout.Planar, true);
        else
        {
            if (image.BitPix == 64)
                for (int offset = 0; offset < image.RawData.Length; offset += 8)
                {
                    long stored = BinaryPrimitives.ReadInt64BigEndian(image.RawData.AsSpan(offset, 8));
                    if ((Int128)(double)stored != stored)
                        throw new NotSupportedException("This signed FITS sample cannot be represented exactly by the floating-point conversion path.");
                }
            double[] physical = image.GetPhysicalSamples();
            result = XisfImageData.FromSamples(physical, dimensions, channels, XisfPixelLayout.Planar, true);
            double[] finite = physical.Where(double.IsFinite).ToArray();
            double lower = finite.Length == 0 ? 0 : finite.Min();
            double upper = finite.Length == 0 ? 1 : finite.Max();
            if (!(lower < upper)) upper = lower + 1;
            result.LowerBound = lower;
            result.UpperBound = upper;
        }
        result.ColorSpace = channels is 3 or 4 ? "RGB" : channels == 1 ? "Gray" : null;
        result.Properties["FITS:BITPIX"] = image.BitPix;
        result.Properties["FITS:BSCALE"] = image.BScale;
        result.Properties["FITS:BZERO"] = image.BZero;
        if (image.Blank.HasValue) result.Properties["FITS:BLANK"] = image.Blank.Value;
        return result;
    }

    /// <summary>Converts XISF images to FITS with the first image as the primary HDU.</summary>
    /// <param name="images">The images in output order; at least one image is required.</param>
    /// <returns>A FITS document containing an HDU per image.</returns>
    public static FitsDocument FromXisf(IEnumerable<XisfImageData> images)
    {
        ArgumentNullException.ThrowIfNull(images);
        XisfImageData[] list = images.ToArray();
        if (list.Length == 0) throw new ArgumentException("At least one XISF image is required.", nameof(images));
        List<FitsHdu> hdus = [];
        for (int index = 0; index < list.Length; index++) hdus.Add(CreateHdu(list[index], index == 0));
        return new FitsDocument(hdus);
    }

    /// <summary>Converts XISF images to FITS with the first image as the primary HDU.</summary>
    /// <param name="images">The images in output order; at least one image is required.</param>
    /// <returns>A FITS document containing an HDU per image.</returns>
    public static FitsDocument FromXisf(IEnumerable<XisfImage> images)
    {
        ArgumentNullException.ThrowIfNull(images);
        XisfImage[] list = images.ToArray();
        if (list.Length == 0) throw new ArgumentException("At least one XISF image is required.", nameof(images));
        List<FitsHdu> hdus = [];
        for (int index = 0; index < list.Length; index++) hdus.Add(CreateHdu(list[index], index == 0));
        return new FitsDocument(hdus);
    }

    /// <summary>Loads FITS and writes its supported images and metadata as XISF.</summary>
    /// <param name="fitsPath">The FITS input or output path.</param>
    /// <param name="xisfPath">The XISF input or output path.</param>
    /// <param name="writerOptions">The output serialization settings, or null for defaults.</param>
    /// <param name="conversionOptions">The conversion settings, or null for defaults.</param>
    public static void ConvertFitsToXisf(string fitsPath, string xisfPath, XisfWriterOptions? writerOptions = null, FitsConversionOptions? conversionOptions = null) =>
        XisfWriter.Write(xisfPath, ToXisf(FitsReader.Load(fitsPath), conversionOptions), writerOptions);

    /// <summary>Loads XISF and writes its images as FITS HDUs.</summary>
    /// <param name="xisfPath">The XISF input or output path.</param>
    /// <param name="fitsPath">The FITS input or output path.</param>
    /// <param name="writerOptions">The output serialization settings, or null for defaults.</param>
    public static void ConvertXisfToFits(string xisfPath, string fitsPath, FitsWriterOptions? writerOptions = null) =>
        FitsWriter.Write(fitsPath, FromXisf(XisfReader.Load(xisfPath).Images), writerOptions);

    private static FitsHdu CreateHdu(XisfImageData image, bool primary)
    {
        FitsImage fits = TryCreateExactFitsImage(image) ?? (TryGetNumber(image.Properties, "FITS:BITPIX") is { } bitPix
            ? FromPreservedFitsStorage(ReadPhysicalSamples(image), FitsDimensions(image), checked((int)bitPix),
                TryGetNumber(image.Properties, "FITS:BSCALE") ?? 1, TryGetNumber(image.Properties, "FITS:BZERO") ?? 0,
                TryGetNumber(image.Properties, "FITS:BLANK") is { } blank ? checked((long)blank) : null)
            : image.SampleFormat switch
        {
            XisfSampleFormat.UInt8 => FitsImage.FromPhysicalSamples(ReadPlanarSamples<byte>(image), FitsDimensions(image)),
            XisfSampleFormat.UInt16 => FitsImage.FromPhysicalSamples(ReadPlanarSamples<ushort>(image), FitsDimensions(image)),
            XisfSampleFormat.UInt32 => FitsImage.FromPhysicalSamples(ReadPlanarSamples<uint>(image), FitsDimensions(image)),
            XisfSampleFormat.UInt64 => FitsImage.FromPhysicalSamples(ReadPlanarSamples<ulong>(image), FitsDimensions(image)),
            XisfSampleFormat.Float32 => FitsImage.FromPhysicalSamples(ReadPlanarSamples<float>(image), FitsDimensions(image)),
            XisfSampleFormat.Float64 => FitsImage.FromPhysicalSamples(ReadPlanarSamples<double>(image), FitsDimensions(image)),
            _ => throw new NotSupportedException("Complex XISF images cannot be represented by a standard FITS image HDU.")
        });
        FitsHdu hdu = FitsHdu.CreateImage(fits, primary);
        AddXisfKeywords(hdu.Header, image.FitsKeywords);
        if (!string.IsNullOrWhiteSpace(image.Id)) hdu.Header.Add(new FitsHeaderCard("EXTNAME", image.Id));
        return hdu;
    }

    private static FitsHdu CreateHdu(XisfImage image, bool primary)
    {
        IReadOnlyList<int> dimensions = FitsDimensions(image.Dimensions, image.ChannelCount);
        FitsImage fits = TryCreateExactFitsImage(image) ?? (TryGetNumber(image.Properties, "FITS:BITPIX") is { } bitPix
            ? FromPreservedFitsStorage(image.GetSamples<double>(), dimensions, checked((int)bitPix),
                TryGetNumber(image.Properties, "FITS:BSCALE") ?? 1, TryGetNumber(image.Properties, "FITS:BZERO") ?? 0,
                TryGetNumber(image.Properties, "FITS:BLANK") is { } blank ? checked((long)blank) : null)
            : image.SampleFormat switch
        {
            XisfSampleFormat.UInt8 => FitsImage.FromPhysicalSamples(image.GetSamples<byte>(), dimensions),
            XisfSampleFormat.UInt16 => FitsImage.FromPhysicalSamples(image.GetSamples<ushort>(), dimensions),
            XisfSampleFormat.UInt32 => FitsImage.FromPhysicalSamples(image.GetSamples<uint>(), dimensions),
            XisfSampleFormat.UInt64 => FitsImage.FromPhysicalSamples(image.GetSamples<ulong>(), dimensions),
            XisfSampleFormat.Float32 => FitsImage.FromPhysicalSamples(image.GetSamples<float>(), dimensions),
            XisfSampleFormat.Float64 => FitsImage.FromPhysicalSamples(image.GetSamples<double>(), dimensions),
            _ => throw new NotSupportedException("Complex XISF images cannot be represented by a standard FITS image HDU.")
        });
        FitsHdu hdu = FitsHdu.CreateImage(fits, primary);
        AddXisfKeywords(hdu.Header, image.FitsKeywords);
        if (!string.IsNullOrWhiteSpace(image.Id)) hdu.Header.Add(new FitsHeaderCard("EXTNAME", image.Id));
        return hdu;
    }

    private static bool HasStandardUnsignedStorage(XisfSampleFormat format, double? bitPix, double scale, double zero, bool hasBlank) =>
        scale == 1 && !hasBlank && (format, bitPix, zero) is
            (XisfSampleFormat.UInt8, 8, 0) or
            (XisfSampleFormat.UInt16, 16, 32768) or
            (XisfSampleFormat.UInt32, 32, 2147483648) or
            (XisfSampleFormat.UInt64, 64, 9223372036854775808d);

    private static FitsImage? TryCreateExactFitsImage(XisfImage image)
    {
        if (!HasStandardUnsignedStorage(image.SampleFormat, TryGetNumber(image.Properties, "FITS:BITPIX"),
                TryGetNumber(image.Properties, "FITS:BSCALE") ?? 1, TryGetNumber(image.Properties, "FITS:BZERO") ?? 0,
                image.Properties.ContainsKey("FITS:BLANK"))) return null;
        IReadOnlyList<int> dimensions = FitsDimensions(image.Dimensions, image.ChannelCount);
        return image.SampleFormat switch
        {
            XisfSampleFormat.UInt8 => FitsImage.FromPhysicalSamples(image.GetSamples<byte>(), dimensions),
            XisfSampleFormat.UInt16 => FitsImage.FromPhysicalSamples(image.GetSamples<ushort>(), dimensions),
            XisfSampleFormat.UInt32 => FitsImage.FromPhysicalSamples(image.GetSamples<uint>(), dimensions),
            XisfSampleFormat.UInt64 => FitsImage.FromPhysicalSamples(image.GetSamples<ulong>(), dimensions),
            _ => null
        };
    }

    private static FitsImage? TryCreateExactFitsImage(XisfImageData image)
    {
        if (!HasStandardUnsignedStorage(image.SampleFormat, TryGetNumber(image.Properties, "FITS:BITPIX"),
                TryGetNumber(image.Properties, "FITS:BSCALE") ?? 1, TryGetNumber(image.Properties, "FITS:BZERO") ?? 0,
                image.Properties.ContainsKey("FITS:BLANK"))) return null;
        IReadOnlyList<int> dimensions = FitsDimensions(image);
        return image.SampleFormat switch
        {
            XisfSampleFormat.UInt8 => FitsImage.FromPhysicalSamples(ReadPlanarSamples<byte>(image), dimensions),
            XisfSampleFormat.UInt16 => FitsImage.FromPhysicalSamples(ReadPlanarSamples<ushort>(image), dimensions),
            XisfSampleFormat.UInt32 => FitsImage.FromPhysicalSamples(ReadPlanarSamples<uint>(image), dimensions),
            XisfSampleFormat.UInt64 => FitsImage.FromPhysicalSamples(ReadPlanarSamples<ulong>(image), dimensions),
            _ => null
        };
    }

    private static IReadOnlyList<int> FitsDimensions(XisfImageData image) => FitsDimensions(image.Dimensions, image.ChannelCount);

    private static IReadOnlyList<int> FitsDimensions(IReadOnlyList<int> dimensions, int channels) =>
        channels == 1 ? dimensions : dimensions.Concat([channels]).ToArray();

    private static byte[] ConvertUnsigned(byte[] source, int size)
    {
        byte[] result = source.ToArray();
        for (int offset = 0; offset < result.Length; offset += size) result[offset] ^= 0x80;
        return result;
    }

    private static T[] ReadSamples<T>(byte[] data, bool bigEndian) where T : unmanaged
    {
        int size = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        T[] result = new T[data.Length / size];
        for (int index = 0; index < result.Length; index++)
        {
            ReadOnlySpan<byte> value = data.AsSpan(index * size, size);
            object sample;
            if (typeof(T) == typeof(byte)) sample = value[0];
            else if (typeof(T) == typeof(ushort)) sample = bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(value) : BinaryPrimitives.ReadUInt16LittleEndian(value);
            else if (typeof(T) == typeof(uint)) sample = bigEndian ? BinaryPrimitives.ReadUInt32BigEndian(value) : BinaryPrimitives.ReadUInt32LittleEndian(value);
            else if (typeof(T) == typeof(ulong)) sample = bigEndian ? BinaryPrimitives.ReadUInt64BigEndian(value) : BinaryPrimitives.ReadUInt64LittleEndian(value);
            else if (typeof(T) == typeof(float)) sample = BitConverter.Int32BitsToSingle(bigEndian ? BinaryPrimitives.ReadInt32BigEndian(value) : BinaryPrimitives.ReadInt32LittleEndian(value));
            else if (typeof(T) == typeof(double)) sample = BitConverter.Int64BitsToDouble(bigEndian ? BinaryPrimitives.ReadInt64BigEndian(value) : BinaryPrimitives.ReadInt64LittleEndian(value));
            else throw new NotSupportedException();
            result[index] = (T)sample;
        }
        return result;
    }

    private static double[] ReadPhysicalSamples(XisfImageData image) => image.SampleFormat switch
    {
        XisfSampleFormat.UInt8 => ReadPlanarSamples<byte>(image).Select(value => (double)value).ToArray(),
        XisfSampleFormat.UInt16 => ReadPlanarSamples<ushort>(image).Select(value => (double)value).ToArray(),
        XisfSampleFormat.UInt32 => ReadPlanarSamples<uint>(image).Select(value => (double)value).ToArray(),
        XisfSampleFormat.UInt64 => ReadPlanarSamples<ulong>(image).Select(value => (double)value).ToArray(),
        XisfSampleFormat.Float32 => ReadPlanarSamples<float>(image).Select(value => (double)value).ToArray(),
        XisfSampleFormat.Float64 => ReadPlanarSamples<double>(image),
        _ => throw new NotSupportedException("Complex XISF images cannot be represented by a standard FITS image HDU.")
    };

    private static T[] ReadPlanarSamples<T>(XisfImageData image) where T : unmanaged
    {
        T[] samples = ReadSamples<T>(image.Data, image.IsBigEndian);
        if (image.PixelStorage == XisfPixelLayout.Planar || image.ChannelCount == 1) return samples;
        int pixels = image.Dimensions.Aggregate(1, (total, dimension) => checked(total * dimension));
        T[] planar = new T[samples.Length];
        for (int pixel = 0; pixel < pixels; pixel++)
        for (int channel = 0; channel < image.ChannelCount; channel++)
            planar[channel * pixels + pixel] = samples[pixel * image.ChannelCount + channel];
        return planar;
    }

    private static FitsImage FromPreservedFitsStorage(double[] physical, IReadOnlyList<int> dimensions, int bitPix, double scale, double zero, long? blank)
    {
        if (bitPix is not (8 or 16 or 32 or 64 or -32 or -64) || scale == 0 || !double.IsFinite(scale) || !double.IsFinite(zero))
            throw new FitsFormatException("Invalid preserved FITS image storage metadata.");
        int size = Math.Abs(bitPix) / 8;
        byte[] raw = new byte[checked(physical.Length * size)];
        for (int index = 0; index < physical.Length; index++)
        {
            double stored = double.IsNaN(physical[index]) && blank.HasValue ? blank.Value : (physical[index] - zero) / scale;
            Span<byte> target = raw.AsSpan(index * size, size);
            switch (bitPix)
            {
                case 8: target[0] = checked((byte)Math.Round(stored)); break;
                case 16: BinaryPrimitives.WriteInt16BigEndian(target, checked((short)Math.Round(stored))); break;
                case 32: BinaryPrimitives.WriteInt32BigEndian(target, checked((int)Math.Round(stored))); break;
                case 64: BinaryPrimitives.WriteInt64BigEndian(target, checked((long)Math.Round(stored))); break;
                case -32: BinaryPrimitives.WriteInt32BigEndian(target, BitConverter.SingleToInt32Bits((float)stored)); break;
                case -64: BinaryPrimitives.WriteInt64BigEndian(target, BitConverter.DoubleToInt64Bits(stored)); break;
            }
        }
        return new FitsImage(bitPix, dimensions.ToArray(), raw, scale, zero, blank);
    }

    private static double? TryGetNumber(IDictionary<string, object?> properties, string id)
    {
        if (!properties.TryGetValue(id, out object? value)) return null;
        value = value is XisfPropertyData declared ? declared.Value : value;
        return value is null ? null : Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    private static double? TryGetNumber(IReadOnlyDictionary<string, XisfProperty> properties, string id) =>
        properties.TryGetValue(id, out XisfProperty? property) && property.Value is not null
            ? Convert.ToDouble(property.Value, CultureInfo.InvariantCulture) : null;

    private static XisfTableData ToXisfTable(FitsTable table, FitsHeader header)
    {
        string id = header.GetValue<string>("EXTNAME", "FITSTable")!;
        XisfTableData result = new() { Id = SanitizeIdentifier(id), Caption = id };
        for (int index = 0; index < table.Columns.Count; index++)
        {
            FitsTableColumn column = table.Columns[index];
            object? sample = table.Rows.Select(row => row[index]).FirstOrDefault(value => value is not null);
            result.Fields.Add(new XisfTableFieldData(SanitizeIdentifier(column.Name), XisfType(sample), column.Name, column.Format));
        }
        foreach (IReadOnlyList<object?> row in table.Rows)
            result.Rows.Add(row.Select(ToXisfCell).ToArray());
        return result;
    }

    private static object? ToXisfCell(object? value) => value is Array array
        ? string.Join(',', array.Cast<object?>().Select(item => Convert.ToString(item, CultureInfo.InvariantCulture))) : value;

    private static string XisfType(object? value) => value switch
    {
        bool => "Boolean", byte => "UInt8", short => "Int16", int => "Int32", long => "Int64",
        float => "Float32", double => "Float64", System.Numerics.Complex => "Complex64", string => "String", Array => "String", _ => "String"
    };

    private static string SanitizeIdentifier(string value)
    {
        string result = new(value.Select(character => char.IsLetterOrDigit(character) || character is '_' or '-' or ':' ? character : '_').ToArray());
        return string.IsNullOrWhiteSpace(result) ? "FITSTable" : result;
    }

    private static void AddKeywords(XisfImageData image, FitsHeader header, FitsConversionOptions options)
    {
        foreach (FitsHeaderCard card in header)
        {
            if (!options.IncludeStructuralKeywords && IsStructural(card.Keyword)) continue;
            image.FitsKeywords.Add(card.Keyword is "HISTORY" or "COMMENT"
                ? new XisfFitsKeyword(card.Keyword, "", Convert.ToString(card.Value, CultureInfo.InvariantCulture) ?? card.Comment ?? "")
                : new XisfFitsKeyword(card.Keyword, card.Value is string text ? "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'" : FormatValue(card.Value), card.Comment));
        }
    }

    private static bool IsStructural(string keyword) => StructuralKeywords.Contains(keyword) || keyword.StartsWith("NAXIS", StringComparison.OrdinalIgnoreCase);

    private static string FormatValue(object? value) => value switch
    {
        null => "",
        bool logical => logical ? "T" : "F",
        string text => text,
        double number => number.ToString("G17", CultureInfo.InvariantCulture),
        float number => number.ToString("G9", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
    };

    private static void AddXisfKeywords(FitsHeader header, IEnumerable<XisfFitsKeyword> keywords)
    {
        foreach (XisfFitsKeyword keyword in keywords)
        {
            if (IsStructural(keyword.Name)) continue;
            header.Add(keyword.Name is "HISTORY" or "COMMENT"
                ? new FitsHeaderCard(keyword.Name, keyword.Comment ?? keyword.Value)
                : new FitsHeaderCard(keyword.Name, ParseValue(keyword.Name, keyword.Value), keyword.Comment));
        }
    }

    private static object? ParseValue(string name, string value)
    {
        if (name is "COMMENT" or "HISTORY") return value;
        string text = value.Trim();
        if (text.Length >= 2 && text[0] == '\'' && text[^1] == '\'') return text[1..^1].Replace("''", "'", StringComparison.Ordinal);
        if (text == "T") return true;
        if (text == "F") return false;
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer)) return integer;
        if (double.TryParse(text.Replace('D', 'E'), NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) return number;
        return value;
    }
}
