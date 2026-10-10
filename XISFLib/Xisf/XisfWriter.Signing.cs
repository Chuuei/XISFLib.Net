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
    private static void SignXml(XDocument document, RSA key)
    {
        XmlDocument xml = new() { PreserveWhitespace = true };
        using (XmlReader reader = document.CreateReader()) xml.Load(reader);
        foreach (XmlNode signature in xml.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl).Cast<XmlNode>().ToArray())
            signature.ParentNode?.RemoveChild(signature);
        string id = xml.DocumentElement!.GetAttribute("id");
        if (string.IsNullOrEmpty(id)) { id = "XISFRootElement_" + Guid.NewGuid().ToString("N"); xml.DocumentElement.SetAttribute("id", id); }
        foreach (XElement block in document.Descendants().Where(IsDataBlockOwner))
        {
            string location = (string?)block.Attribute("location") ?? "embedded";
            if (!location.StartsWith("inline:", StringComparison.Ordinal) && location != "embedded" && block.Attribute("checksum") is null)
                throw new InvalidOperationException("Signing requires a checksum for every external or attached data block.");
        }
        SignedXml signedXml = new(xml) { SigningKey = key };
        XisfSignatureAlgorithms.Configure(signedXml);
        signedXml.SignedInfo!.CanonicalizationMethod = XisfSignatureAlgorithms.Canonical11;
        Reference reference = new("#" + id);
        reference.AddTransform(new XisfCanonicalXml11Transform());
        signedXml.AddReference(reference);
        KeyInfo keyInfo = new();
        keyInfo.AddClause(new RSAKeyValue(key));
        signedXml.KeyInfo = keyInfo;
        signedXml.ComputeSignature();
        document.Root!.SetAttributeValue("id", id);
        document.Descendants(XName.Get("Signature", SignedXml.XmlDsigNamespaceUrl)).Remove();
        document.RemoveAnnotations<XisfDetachedSignature>();
        document.AddAnnotation(new XisfDetachedSignature(XElement.Parse(signedXml.GetXml().OuterXml, LoadOptions.PreserveWhitespace)));
    }

}
