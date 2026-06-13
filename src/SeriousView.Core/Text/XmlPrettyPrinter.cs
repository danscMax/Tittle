using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SeriousView.Core.Text;

/// <summary>Display-only XML re-formatting for the source view (the XML twin of
/// <see cref="JsonPrettyPrinter"/>). Pure: parse → indented serialize; anything unparseable
/// (malformed markup) returns null and the raw text is shown as-is — the stored document text is
/// never touched.</summary>
public static class XmlPrettyPrinter
{
    public static string? TryFormat(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            // PreserveWhitespace off → XmlWriter re-indents from scratch; keep significant content.
            var doc = XDocument.Parse(text, LoadOptions.None);

            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                // Keep the original declaration's presence: emit one only when the source had it.
                OmitXmlDeclaration = doc.Declaration is null,
                Encoding = new UTF8Encoding(false),
            };

            var sb = new StringBuilder();
            using (var writer = XmlWriter.Create(sb, settings))
                doc.Save(writer);
            return sb.ToString();
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
