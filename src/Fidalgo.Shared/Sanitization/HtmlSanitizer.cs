using AngleSharp.Html.Parser;

namespace Fidalgo.Shared.Sanitization;

/// <summary>
/// Sanitizes HTML by parsing it with AngleSharp and stripping rendering elements and inline styles.
/// Returns clean text content suitable for LLM processing.
/// </summary>
public class HtmlSanitizer
{
    private readonly HtmlParser _parser;

    /// <summary>Initializes a new instance of the HtmlSanitizer.</summary>
    public HtmlSanitizer()
    {
        _parser = new HtmlParser();
    }

    /// <summary>Sanitizes HTML content by removing rendering elements and inline styles.</summary>
    /// <param name="html">Raw HTML content to sanitize.</param>
    /// <returns>Plain text content, or empty string if input is null or empty.</returns>
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