using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using K4os.Compression.LZ4;

namespace XISFLib;

public static partial class XisfWriter
{
    private static void AddProperty(XElement parent, string id, object? value, XisfWriterOptions options, List<Attachment> attachments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!PropertyIdentifier.IsMatch(id)) throw new ArgumentException("Invalid XISF property identifier.", nameof(id));
        if (value is XisfProperty preserved)
        {
            XElement original = preserved.XmlCore;
            XElement copy = new(original);
            if (original.Name.Namespace == XNamespace.None)
                foreach (XElement core in copy.DescendantsAndSelf().Where(e => e.Name.Namespace == XNamespace.None))
                    core.Name = Namespace + core.Name.LocalName;
            copy.SetAttributeValue("id", id);
            string? location = (string?)copy.Attribute("location");
            if (location is not null && location != "embedded" && !location.StartsWith("inline:", StringComparison.Ordinal))
            {
                if (preserved.Source is null) throw new ArgumentException("A preserved block requires its original data source.");
                byte[] serialized = new XisfDataBlock(original, preserved.Source).ReadSerialized();
                copy.SetAttributeValue("location", "attachment:00000000000000000000:" + serialized.Length);
                long uncompressed = copy.Attribute("compression") is { } compression ? long.Parse(compression.Value.Split(':')[1], CultureInfo.InvariantCulture) : serialized.LongLength;
                attachments.Add(new Attachment(copy, serialized, uncompressed));
            }
            parent.Add(copy);
            return;
        }
        XisfPropertyData? declared = value as XisfPropertyData;
        PropertySerialization property = PropertyValue(declared is null ? value : declared.Value);
        if (declared is not null)
            property = ApplyDeclaredPropertyType(property, declared.Type);
        XElement element = new(Namespace + "Property", new XAttribute("id", id), new XAttribute("type", property.Type));
        if (declared?.Format is not null) element.SetAttributeValue("format", declared.Format);
        if (declared?.Comment is not null) element.SetAttributeValue("comment", declared.Comment);
        if (property.Binary is not null)
        {
            if (property.Rows is { } rows)
            {
                element.SetAttributeValue("rows", rows);
                element.SetAttributeValue("columns", property.Columns);
            }
            else
                element.SetAttributeValue("length", property.Length);
            if (property.Binary.Length == 0)
                element.SetAttributeValue("location", "inline:base64");
            else
                AddAttachment(element, property.Binary, property.ItemSize, options, attachments);
        }
        else if (property.Type == "String")
        {
            string text = property.Text ?? string.Empty;
            if (text.Contains('\0')) throw new ArgumentException("XISF strings cannot contain null code points.");
            byte[] utf8 = new UTF8Encoding(false, true).GetBytes(text);
            bool xmlCompatible = true;
            try { XmlConvert.VerifyXmlChars(text); } catch (XmlException) { xmlCompatible = false; }
            if (utf8.Length > 0 && (options.StringsAsDataBlocks || !xmlCompatible)) AddAttachment(element, utf8, 1, options, attachments);
            else element.Value = text;
        }
        else
            element.SetAttributeValue("value", property.Text ?? string.Empty);
        parent.Add(element);
    }

    private static PropertySerialization ApplyDeclaredPropertyType(PropertySerialization property, string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        string canonical = type.ToLowerInvariant() switch
        {
            "byte" => "UInt8", "short" => "Int16", "ushort" => "UInt16", "int" => "Int32", "uint" => "UInt32",
            "float" => "Float32", "double" => "Float64", "quad" => "Float128", "complex" => "Complex64",
            "bytearray" => "UI8Vector", "ivector" => "I32Vector", "uivector" => "UI32Vector", "vector" => "F64Vector",
            "bytematrix" => "UI8Matrix", "imatrix" => "I32Matrix", "uimatrix" => "UI32Matrix", "matrix" => "F64Matrix",
            _ => type
        };
        if (canonical.StartsWith("C32", StringComparison.OrdinalIgnoreCase) && property.Type.StartsWith("C64", StringComparison.Ordinal) && property.Binary is not null)
        {
            byte[] single = new byte[property.Binary.Length / 2];
            for (int offset = 0; offset < property.Binary.Length; offset += 8)
                BinaryPrimitives.WriteSingleLittleEndian(single.AsSpan(offset / 2), (float)BinaryPrimitives.ReadDoubleLittleEndian(property.Binary.AsSpan(offset)));
            property = property with { Type = property.Type.Replace("C64", "C32", StringComparison.Ordinal), Binary = single, ItemSize = 8 };
        }
        else if (canonical == "Complex32" && property.Type == "Complex64")
        {
            string[] components = property.Text!.Trim('(', ')').Split(',');
            property = property with { Type = "Complex32", Text = $"({Format((float)XisfReader.ParseFloatingPoint(components[0]))},{Format((float)XisfReader.ParseFloatingPoint(components[1]))})" };
        }
        string actualCanonical = property.Type;
        if (!string.Equals(canonical, actualCanonical, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"The value serialized as '{property.Type}' is incompatible with the declared XISF property type '{type}'.");
        return property with { Type = type };
    }

    private static PropertySerialization PropertyValue(object? value) => value switch
    {
        null => new("String", ""),
        string text => new("String", text),
        bool boolean => new("Boolean", boolean.ToString().ToLowerInvariant()),
        sbyte number => new("Int8", number.ToString(CultureInfo.InvariantCulture)),
        short number => new("Int16", number.ToString(CultureInfo.InvariantCulture)),
        int number => new("Int32", number.ToString(CultureInfo.InvariantCulture)),
        long number => new("Int64", number.ToString(CultureInfo.InvariantCulture)),
        byte number => new("UInt8", number.ToString(CultureInfo.InvariantCulture)),
        ushort number => new("UInt16", number.ToString(CultureInfo.InvariantCulture)),
        uint number => new("UInt32", number.ToString(CultureInfo.InvariantCulture)),
        ulong number => new("UInt64", number.ToString(CultureInfo.InvariantCulture)),
        Int128 number => new("Int128", number.ToString(CultureInfo.InvariantCulture)),
        UInt128 number => new("UInt128", number.ToString(CultureInfo.InvariantCulture)),
        float number => new("Float32", Format(number)),
        double number => new("Float64", Format(number)),
        decimal number => new("Float128", number.ToString(CultureInfo.InvariantCulture)),
        XisfFloat128 number => new("Float128", number.ToString()),
        XisfComplex128 number => new("Complex128", number.ToString()),
        XisfComplex32 number => new("Complex32", $"({Format(number.Real)},{Format(number.Imaginary)})"),
        DateTimeOffset time => new("TimePoint", time.ToString("O", CultureInfo.InvariantCulture)),
        DateTime time => new("TimePoint", time.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
        Complex complex => new("Complex64", $"({Format(complex.Real)},{Format(complex.Imaginary)})"),
        Array array when array.Rank is 1 or 2 => SerializeArray(array),
        _ => throw new NotSupportedException($"The property type '{value.GetType().Name}' is not supported.")
    };

    private static PropertySerialization SerializeArray(Array values)
    {
        Type elementType = values.GetType().GetElementType()!;
        var numeric = XisfNumericTypes.Numeric(elementType);
        string prefix = numeric.Prefix;
        int itemSize = numeric.Size;
        int componentSize = prefix.StartsWith('C') ? itemSize / 2 : itemSize;
        byte[] data = XisfSampleBuffer.CopyArray(values, checked(values.Length * itemSize), componentSize);
        return values.Rank == 1
            ? new PropertySerialization(prefix + "Vector", Binary: data, Length: values.Length, ItemSize: itemSize)
            : new PropertySerialization(prefix + "Matrix", Binary: data, Rows: values.GetLength(0), Columns: values.GetLength(1), ItemSize: itemSize);
    }

}
