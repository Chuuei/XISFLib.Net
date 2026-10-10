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
    private static void WriteMonolithic(Stream stream, XDocument document, IReadOnlyList<Attachment> attachments)
    {
        SetAttachmentLocations(document, attachments);
        byte[] xml = SerializeXml(document);

        Span<byte> header = stackalloc byte[16];
        "XISF0100"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], checked((uint)xml.Length));
        header[12..].Clear();
        stream.Write(header);
        stream.Write(xml);
        long position = 16L + xml.Length;
        foreach (Attachment attachment in attachments)
        {
            long target = long.Parse(((string)attachment.Element.Attribute("location")!).Split(':')[1], CultureInfo.InvariantCulture);
            byte[] padding = new byte[8192];
            while (position < target) { int count = (int)Math.Min(padding.Length, target - position); stream.Write(padding.AsSpan(0, count)); position += count; }
            stream.Write(attachment.Data); position += attachment.Data.LongLength;
        }
    }

    private static void SetAttachmentLocations(XDocument document, IReadOnlyList<Attachment> attachments)
    {
        if (document.Annotation<XisfDetachedSignature>() is not null)
        {
            long previousEnd = 16L + SerializeXml(document).Length;
            foreach (Attachment attachment in attachments)
            {
                long position = long.Parse(((string)attachment.Element.Attribute("location")!).Split(':')[1], CultureInfo.InvariantCulture);
                if (position < previousEnd) throw new InvalidOperationException("The signed header layout cannot be preserved. Use WriteSigned to regenerate the signature.");
                previousEnd = checked(position + attachment.Data.LongLength);
            }
            return;
        }
        int alignment = 1;
        XElement? alignmentProperty = document.Root!.Elements(Namespace + "Metadata").Elements(Namespace + "Property").FirstOrDefault(p => (string?)p.Attribute("id") == "XISF:BlockAlignmentSize");
        if (alignmentProperty is not null && int.TryParse((string?)alignmentProperty.Attribute("value"), out int declaredAlignment) && declaredAlignment > 0) alignment = declaredAlignment;
        int xmlLength = SerializeXml(document).Length;
        for (int iteration = 0; iteration < 32; iteration++)
        {
            long position = 16L + xmlLength;
            foreach (Attachment attachment in attachments)
            {
                position = checked(position + (alignment - position % alignment) % alignment);
                attachment.Element.SetAttributeValue("location",
                    $"attachment:{position.ToString(CultureInfo.InvariantCulture)}:{attachment.Data.Length.ToString(CultureInfo.InvariantCulture)}");
                position += attachment.Data.Length;
            }

            int updatedXmlLength = SerializeXml(document).Length;
            if (updatedXmlLength == xmlLength)
                return;
            xmlLength = updatedXmlLength;
        }

        throw new InvalidOperationException("Unable to stabilize XISF attachment locations.");
    }

    private static bool IsDataBlockOwner(XElement element)
    {
        string? location = (string?)element.Attribute("location");
        return location is not null || element.Elements().Any(e => e.Name.LocalName == "Data");
    }

    private static XElement PreserveElement(XisfPreservedElement preserved, List<Attachment> attachments)
    {
        XElement copy = new(preserved.Xml);
        XElement[] originals = preserved.Xml.DescendantsAndSelf().Where(IsDataBlockOwner).ToArray();
        XElement[] targets = copy.DescendantsAndSelf().Where(IsDataBlockOwner).ToArray();
        for (int i = 0; i < originals.Length; i++)
        {
            string location = (string?)originals[i].Attribute("location") ?? "embedded";
            if (location == "embedded" || location.StartsWith("inline:", StringComparison.OrdinalIgnoreCase)) continue;
            byte[] bytes = new XisfDataBlock(originals[i], preserved.Source).ReadSerialized();
            targets[i].SetAttributeValue("location", "attachment:00000000000000000000:" + bytes.Length);
            long length = targets[i].Attribute("compression") is { } compression ? long.Parse(compression.Value.Split(':')[1], CultureInfo.InvariantCulture) : bytes.LongLength;
            attachments.Add(new(targets[i], bytes, length));
        }
        return copy;
    }

}
