using HtmlAgilityPack;

namespace Fidalgo.Agent.Sanitization;

/// <summary>
/// Strips visual elements, scripts, styles, and noise from HTML to produce clean content suitable for LLM processing.
/// </summary>
public class HtmlStripper
{
    private static readonly string[] TagsToRemove =
    [
        "head", "meta", "base", "link",
        "script", "style", "noscript",
        "img", "video", "audio", "iframe",
        "object", "embed", "canvas", "svg", "math",
        "form", "input", "textarea", "select", "button",
    ];

    private static readonly string[] StructuralTagsToRemove = ["nav", "footer", "aside", "header"];

    private static readonly string[] ClassPatternsToRemove =
    [
        "ad", "ads", "sponsored", "promo", "banner",
        "cookie", "consent", "popup", "modal",
        "share", "social", "login", "signin", "sign-in",
        "breadcrumb", "pagination", "pager",
    ];

    private static readonly string[] IdPatternsToRemove =
    [
        "ad", "ads", "sponsored", "promo", "banner",
        "cookie", "consent", "popup", "modal",
        "share", "social", "login", "signin", "sign-in",
        "breadcrumb", "pagination", "pager",
    ];

    private static readonly string[] EventAttributePrefixes =
    [
        "onclick", "ondblclick", "onmousedown", "onmouseup", "onmouseover",
        "onmousemove", "onmouseout", "onkeydown", "onkeypress", "onkeyup",
        "onload", "onerror", "onfocus", "onblur", "onsubmit", "onreset",
        "onselect", "onchange", "oninput", "oncontextmenu",
    ];

    /// <summary>
    /// Strips visual elements, scripts, styles, and noise from HTML content.
    /// </summary>
    /// <param name="html">Raw HTML content to strip.</param>
    /// <returns>Clean HTML with visual and noise elements removed.</returns>
    public string Strip(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var doc = new HtmlDocument();
        doc.OptionFixNestedTags = true;
        doc.OptionAutoCloseOnEnd = true;
        doc.LoadHtml(html);

        var body = doc.DocumentNode.SelectSingleNode("//body");
        if (body == null)
        {
            return html;
        }

        RemoveTags(body);
        RemoveStructuralTags(body);
        RemoveElementsByClassPatterns(body);
        RemoveElementsByIdPatterns(body);
        RemoveEventAttributes(body);
        RemoveStyleAttributes(body);
        RemoveEmptyElements(body);
        NormalizeBreakElements(body);

        var mainContent = body.InnerHtml.Trim();
        return string.IsNullOrEmpty(mainContent) ? string.Empty : mainContent;
    }

    private static void RemoveTags(HtmlNode parentNode)
    {
        foreach (var tag in TagsToRemove)
        {
            var nodes = parentNode.SelectNodes($"//{tag}");
            if (nodes == null) continue;
            foreach (var node in nodes.ToList())
            {
                node.Remove();
            }
        }
    }

    private static void RemoveStructuralTags(HtmlNode parentNode)
    {
        foreach (var tag in StructuralTagsToRemove)
        {
            var nodes = parentNode.SelectNodes($"//{tag}");
            if (nodes == null) continue;
            foreach (var node in nodes.ToList())
            {
                node.Remove();
            }
        }
    }

    private static void RemoveElementsByClassPatterns(HtmlNode parentNode)
    {
        var allElements = parentNode.SelectNodes(".//*");
        if (allElements == null) return;

        var elementsToRemove = new HashSet<HtmlNode>();
        foreach (HtmlNode element in allElements)
        {
            var classAttr = element.Attributes["class"];
            if (classAttr == null) continue;

            var classValue = classAttr.Value.ToLowerInvariant();
            if (MatchesAnyPattern(classValue, ClassPatternsToRemove))
            {
                elementsToRemove.Add(element);
            }
        }

        foreach (var element in elementsToRemove)
        {
            element.Remove();
        }
    }

    private static void RemoveElementsByIdPatterns(HtmlNode parentNode)
    {
        var allElements = parentNode.SelectNodes(".//*");
        if (allElements == null) return;

        var elementsToRemove = new HashSet<HtmlNode>();
        foreach (HtmlNode element in allElements)
        {
            var idAttr = element.Attributes["id"];
            if (idAttr == null) continue;

            var idValue = idAttr.Value.ToLowerInvariant();
            if (MatchesAnyPattern(idValue, IdPatternsToRemove))
            {
                elementsToRemove.Add(element);
            }
        }

        foreach (var element in elementsToRemove)
        {
            element.Remove();
        }
    }

    private static bool MatchesAnyPattern(string value, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (value.Contains(pattern))
            {
                return true;
            }
        }
        return false;
    }

    private static void RemoveEventAttributes(HtmlNode parentNode)
    {
        var allElements = parentNode.SelectNodes(".//*");
        if (allElements == null) return;

        foreach (HtmlNode element in allElements)
        {
            var attributesToRemove = new List<HtmlAttribute>();
            foreach (HtmlAttribute attr in element.Attributes)
            {
                if (IsEventAttribute(attr.Name))
                {
                    attributesToRemove.Add(attr);
                }
            }
            foreach (var attr in attributesToRemove)
            {
                element.Attributes.Remove(attr);
            }
        }
    }

    private static bool IsEventAttribute(string attrName)
    {
        var lower = attrName.ToLowerInvariant();
        return EventAttributePrefixes.Any(prefix => lower.StartsWith(prefix));
    }

    private static void RemoveStyleAttributes(HtmlNode parentNode)
    {
        var allElements = parentNode.SelectNodes(".//*");
        if (allElements == null) return;

        foreach (HtmlNode element in allElements)
        {
            var styleAttr = element.Attributes["style"];
            if (styleAttr != null)
            {
                element.Attributes.Remove(styleAttr);
            }
        }
    }

    private static void RemoveEmptyElements(HtmlNode parentNode)
    {
        var allElements = parentNode.SelectNodes(".//*");
        if (allElements == null) return;

        var emptyNodes = allElements.Cast<HtmlNode>()
            .OrderByDescending(n => GetNodeDepth(n, parentNode))
            .Where(n => string.IsNullOrWhiteSpace(n.InnerText) && string.IsNullOrWhiteSpace(n.InnerHtml))
            .ToList();

        foreach (var node in emptyNodes)
        {
            node.Remove();
        }
    }

    private static int GetNodeDepth(HtmlNode node, HtmlNode root)
    {
        int depth = 0;
        var current = node.ParentNode;
        while (current != null && current != root)
        {
            depth++;
            current = current.ParentNode;
        }
        return depth;
    }

    private static void NormalizeBreakElements(HtmlNode parentNode)
    {
        var brNodes = parentNode.SelectNodes("//br");
        if (brNodes != null)
        {
            foreach (HtmlNode br in brNodes)
            {
                var newline = br.OwnerDocument.CreateTextNode("\n");
                br.ParentNode?.ReplaceChild(newline, br);
            }
        }

        var hrNodes = parentNode.SelectNodes("//hr");
        if (hrNodes != null)
        {
            foreach (HtmlNode hr in hrNodes)
            {
                var doubleNewline = hr.OwnerDocument.CreateTextNode("\n\n");
                hr.ParentNode?.ReplaceChild(doubleNewline, hr);
            }
        }
    }
}
