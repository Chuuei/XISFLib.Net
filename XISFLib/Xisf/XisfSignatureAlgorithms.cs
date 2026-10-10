using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace XISFLib;

/// <summary>Canonical XML 1.1 transform for complete XISF roots and detached SignedInfo trees.</summary>
/// <remarks>XISF uses XML 1.0 and signs the entire root. The 1.1 changes concerning omitted
/// ancestors' xml:id and xml:base attributes do not arise for these complete trees.
/// This transform rejects subsets with such omitted ancestor context.</remarks>
public sealed class XisfCanonicalXml11Transform : XmlDsigC14NTransform
{
    /// <summary>Creates the Canonical XML 1.1 transform without comments.</summary>
    public XisfCanonicalXml11Transform() { Algorithm = XisfSignatureAlgorithms.Canonical11; }
    /// <summary>Loads a complete tree or a subset without omitted xml:id/xml:base ancestor context.</summary>
    /// <param name="obj">The XML document, stream, or node set.</param>
    public override void LoadInput(object obj)
    {
        XisfSignatureAlgorithms.CheckAncestorContext(obj);
        base.LoadInput(obj);
    }
}

/// <summary>Canonical XML 1.1 with comments for complete XISF roots and detached SignedInfo trees.</summary>
public sealed class XisfCanonicalXml11WithCommentsTransform : XmlDsigC14NTransform
{
    /// <summary>Creates the Canonical XML 1.1 transform including comments.</summary>
    public XisfCanonicalXml11WithCommentsTransform() : base(true) { Algorithm = XisfSignatureAlgorithms.Canonical11Comments; }
    /// <summary>Loads a complete tree or a subset without omitted xml:id/xml:base ancestor context.</summary>
    /// <param name="obj">The XML document, stream, or node set.</param>
    public override void LoadInput(object obj)
    {
        XisfSignatureAlgorithms.CheckAncestorContext(obj);
        base.LoadInput(obj);
    }
}

internal static class XisfSignatureAlgorithms
{
    public const string Canonical11 = "http://www.w3.org/2006/12/xml-c14n11";
    public const string Canonical11Comments = Canonical11 + "#WithComments";
    private static readonly Lazy<bool> Registered = new(() =>
    {
        CryptoConfig.AddAlgorithm(typeof(XisfCanonicalXml11Transform), Canonical11);
        CryptoConfig.AddAlgorithm(typeof(XisfCanonicalXml11WithCommentsTransform), Canonical11Comments);
        return true;
    });
    public static void Configure(SignedXml xml)
    {
        _ = Registered.Value;
        xml.SafeCanonicalizationMethods.Add(Canonical11);
        xml.SafeCanonicalizationMethods.Add(Canonical11Comments);
    }
    public static void CheckAncestorContext(object input)
    {
        if (input is not XmlNodeList nodes) return;
        HashSet<XmlNode> included = nodes.Cast<XmlNode>().ToHashSet();
        foreach (XmlElement element in nodes.OfType<XmlElement>())
        {
            for (XmlNode? ancestor = element.ParentNode; ancestor is XmlElement parent && !included.Contains(parent); ancestor = ancestor.ParentNode)
                if (parent.HasAttribute("id", "http://www.w3.org/XML/1998/namespace") || parent.HasAttribute("base", "http://www.w3.org/XML/1998/namespace"))
                    throw new CryptographicException("Canonical XML 1.1 requires a complete tree without omitted xml:id/xml:base ancestors.");
        }
    }
}
