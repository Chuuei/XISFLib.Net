using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace XISFLib;

internal sealed record XisfSignatureReservation(string Xml);

internal sealed record XisfDetachedSignature(XElement Element);

internal static class XisfXmlHeader
{
    public static XDocument Read(string text, XmlReaderSettings settings, CancellationToken token)
    {
        text = text.TrimStart('\uFEFF');
        if (settings.MaxCharactersInDocument > 0 && text.Length > settings.MaxCharactersInDocument) throw new XisfFormatException("The XML header exceeds the character limit.");
        XmlReaderSettings containerSettings = settings.Clone();
        if (containerSettings.MaxCharactersInDocument > 0) containerSettings.MaxCharactersInDocument = checked(containerSettings.MaxCharactersInDocument + 64);
        XDeclaration? declaration = null;
        Match match = Regex.Match(text, @"\A\s*<\?xml\s+([^?]+)\?>", RegexOptions.CultureInvariant);
        if (match.Success)
        {
            using StringReader declarationReader = new(match.Value + "<root/>");
            using XmlReader reader = XmlReader.Create(declarationReader, settings);
            declaration = XDocument.Load(reader).Declaration;
            text = text[match.Length..];
        }
        using StringReader source = new CancellationStringReader("<XisfHeaderContainer>" + text + "</XisfHeaderContainer>", token);
        using XmlReader xmlReader = XmlReader.Create(source, containerSettings);
        XElement container = XElement.Load(xmlReader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        XElement[] elements = container.Elements().ToArray();
        if (elements.Length is < 1 or > 2 || elements[0].Name.LocalName != "xisf" ||
            elements.Length == 2 && elements[1].Name != XName.Get("Signature", "http://www.w3.org/2000/09/xmldsig#") ||
            container.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value)))
            throw new XisfFormatException("An XISF header requires one root followed by at most one detached signature.");
        elements[0].Remove();
        XDocument document = new(declaration, elements[0]);
        if (elements.Length == 2) document.AddAnnotation(new XisfDetachedSignature(new XElement(elements[1])));
        return document;
    }
}
