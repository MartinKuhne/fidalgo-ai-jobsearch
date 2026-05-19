using AngleSharp.Html.Parser;

namespace Fidalgo.Agent.Sanitization;

public class HtmlSanitizer
{
    private readonly HtmlParser _parser;

    public HtmlSanitizer()
    {
        _parser = new HtmlParser();
    }

    public string Sanitize(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var document = _parser.ParseDocument(html);
        
        RemoveRenderingElements(document);
        RemoveInlineStyles(document);
        
        return document.Body?.TextContent ?? string.Empty;
    }

    private void RemoveRenderingElements(AngleSharp.Html.Dom.IHtmlDocument document)
    {
        var elementsToRemove = new[] { "font", "style", "link", "script", "svg" };
        
        foreach (var tagName in elementsToRemove)
        {
            var elements = document.QuerySelectorAll(tagName);
            foreach (var element in elements)
            {
                element.Remove();
            }
        }
    }

    private void RemoveInlineStyles(AngleSharp.Html.Dom.IHtmlDocument document)
    {
        var allElements = document.QuerySelectorAll("*");
        foreach (var element in allElements)
        {
            if (element.HasAttribute("style"))
            {
                element.RemoveAttribute("style");
            }
        }
    }
}
