using System;
using System.Text;
using System.Text.RegularExpressions;
using SeriousView.Core.Export;
using Xunit;

namespace SeriousView.Tests.Core;

public class ClipboardHtmlTests
{
    private static (int StartHtml, int EndHtml, int StartFragment, int EndFragment, byte[] Payload)
        Parse(string html)
    {
        var payload = ClipboardHtml.BuildCfHtml(html);
        var text = Encoding.UTF8.GetString(payload);
        int Of(string key) => int.Parse(Regex.Match(text, key + @":(\d{10})").Groups[1].Value);
        return (Of("StartHTML"), Of("EndHTML"), Of("StartFragment"), Of("EndFragment"), payload);
    }

    [Fact]
    public void Offsets_SliceTheDocumentAndFragment_ByBytes()
    {
        const string html = "<html><body><p>Привет, мир</p></body></html>"; // multibyte content
        var (startHtml, endHtml, startFrag, endFrag, payload) = Parse(html);

        var doc = Encoding.UTF8.GetString(payload, startHtml, endHtml - startHtml);
        Assert.StartsWith("<html>", doc);
        Assert.EndsWith("</html>", doc);

        var fragment = Encoding.UTF8.GetString(payload, startFrag, endFrag - startFrag);
        Assert.Equal("<p>Привет, мир</p>", fragment);
    }

    [Fact]
    public void EndHtml_IsTheExactPayloadLength()
    {
        var (_, endHtml, _, _, payload) = Parse("<html><body>x</body></html>");

        Assert.Equal(payload.Length, endHtml);
    }

    [Fact]
    public void NoBodyTag_WrapsTheWholeDocument()
    {
        var marked = ClipboardHtml.InsertFragmentMarkers("<p>bare</p>");

        Assert.StartsWith("<!--StartFragment-->", marked);
        Assert.EndsWith("<!--EndFragment-->", marked);
    }

    [Fact]
    public void BodyWithAttributes_GetsMarkersInside()
    {
        var marked = ClipboardHtml.InsertFragmentMarkers("<html><body class=\"dark\"><p>x</p></body></html>");

        Assert.Contains("<body class=\"dark\"><!--StartFragment--><p>x</p><!--EndFragment--></body>", marked);
    }

    [Fact]
    public void BodyContainingMarkerTokens_OffsetsStillPointAtTheInsertedMarkers()
    {
        // S6: the body literally contains the EndFragment token. Offsets must come from the markers
        // WE inserted (around the whole body), not the user's earlier occurrence — else the fragment
        // would be truncated.
        const string html = "<html><body><p>text <!--EndFragment--> more</p></body></html>";
        var (_, _, startFrag, endFrag, payload) = Parse(html);

        var fragment = Encoding.UTF8.GetString(payload, startFrag, endFrag - startFrag);
        Assert.Equal("<p>text <!--EndFragment--> more</p>", fragment);
    }

    [Fact]
    public void Header_IsAsciiWithCrLf()
    {
        var payload = ClipboardHtml.BuildCfHtml("<html><body>x</body></html>");
        var head = Encoding.ASCII.GetString(payload, 0, 13);

        Assert.Equal("Version:0.9\r\n", head);
    }
}
