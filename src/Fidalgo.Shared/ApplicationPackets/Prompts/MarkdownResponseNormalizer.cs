using System.Text.RegularExpressions;

namespace Fidalgo.Shared.ApplicationPackets.Prompts;

/// <summary>
/// Cleans accidental LLM conversational preambles and Markdown code fences from generated documents.
/// </summary>
public static partial class MarkdownResponseNormalizer
{
    [GeneratedRegex(@"^\s*(?:```(?:markdown|md)?\s*\r?\n)(?<body>[\s\S]*?)\r?\n```\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex OuterFenceRegex();

    [GeneratedRegex(@"^(?:here(?:'s| is)\b[^\r\n]*:|sure[,!.]?[^\r\n]*:|certainly[,!.]?[^\r\n]*:|below is\b[^\r\n]*:)\s*\r?\n+", RegexOptions.IgnoreCase)]
    private static partial Regex ConversationalPreambleRegex();

    /// <summary>
    /// Strips leading conversational preamble and outer <c>```markdown</c> / <c>```</c> fences while preserving Markdown content.
    /// </summary>
    public static string Normalize(string? rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return string.Empty;
        }

        var text = rawResponse.Trim();

        var preambleMatch = ConversationalPreambleRegex().Match(text);
        if (preambleMatch.Success)
        {
            text = text[preambleMatch.Length..].Trim();
        }

        var fenceMatch = OuterFenceRegex().Match(text);
        if (fenceMatch.Success)
        {
            text = fenceMatch.Groups["body"].Value.Trim();
        }
        else if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = text.IndexOf('\n');
            if (firstNewLine >= 0)
            {
                text = text[(firstNewLine + 1)..];
                if (text.EndsWith("```", StringComparison.Ordinal))
                {
                    text = text[..^3];
                }
                text = text.Trim();
            }
        }

        return text;
    }
}
