using Fidalgo.Shared.Models;

namespace Fidalgo.Shared.Pdf;

/// <summary>
/// Provides clean, static print CSS stylesheets for <c>md-to-pdf</c> rendering
/// tailored to resumes versus cover letters.
/// </summary>
public static class PdfCssProvider
{
    private static readonly HashSet<string> AllowedPageSizes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Letter",
        "A4",
        "Legal",
        "Tabloid",
        "A3",
        "A5"
    };

    /// <summary>
    /// Returns the print CSS stylesheet for the specified <paramref name="options"/>.
    /// </summary>
    public static string GetCss(PdfRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var pageSize = NormalizePageSize(options.PageSize);

        return options.Kind switch
        {
            PdfDocumentKind.CoverLetter => BuildCoverLetterCss(pageSize),
            _ => BuildResumeCss(pageSize)
        };
    }

    internal static string NormalizePageSize(string? pageSize)
    {
        if (!string.IsNullOrWhiteSpace(pageSize) && AllowedPageSizes.TryGetValue(pageSize.Trim(), out var canonical))
        {
            return canonical;
        }

        return "Letter";
    }

    private static string BuildResumeCss(string pageSize) =>
        $$"""
        @page {
          size: {{pageSize}};
          margin: 0.55in 0.6in;
        }

        body {
          font-family: "Liberation Sans", "Segoe UI", Arial, sans-serif;
          font-size: 10pt;
          line-height: 1.35;
          color: #1a202c;
        }

        h1 {
          font-size: 18pt;
          margin: 0 0 4pt 0;
          color: #111827;
        }

        h2 {
          font-size: 11.5pt;
          text-transform: uppercase;
          letter-spacing: 0.04em;
          border-bottom: 1px solid #cbd5e1;
          padding-bottom: 2pt;
          margin: 10pt 0 5pt 0;
          color: #1e293b;
        }

        h3 {
          font-size: 10.5pt;
          margin: 7pt 0 2pt 0;
          color: #0f172a;
        }

        p {
          margin: 0 0 5pt 0;
        }

        ul, ol {
          margin: 2pt 0 6pt 0;
          padding-left: 16pt;
        }

        li {
          margin-bottom: 2pt;
        }

        h2, h3, li {
          break-inside: avoid;
        }
        """;

    private static string BuildCoverLetterCss(string pageSize) =>
        $$"""
        @page {
          size: {{pageSize}};
          margin: 0.85in 0.9in;
        }

        body {
          font-family: "Liberation Serif", Georgia, "Times New Roman", serif;
          font-size: 11pt;
          line-height: 1.5;
          color: #1a202c;
        }

        h1 {
          font-size: 16pt;
          margin: 0 0 6pt 0;
          color: #111827;
        }

        h2, h3 {
          font-size: 11.5pt;
          margin: 10pt 0 4pt 0;
          color: #1e293b;
        }

        p {
          margin: 0 0 10pt 0;
        }

        ul, ol {
          margin: 4pt 0 10pt 0;
          padding-left: 18pt;
        }

        li {
          margin-bottom: 4pt;
        }

        p, li {
          break-inside: avoid;
        }
        """;
}
