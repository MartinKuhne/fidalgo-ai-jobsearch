using Fidalgo.Shared.Models;

namespace Fidalgo.Shared.Pdf;

/// <summary>
/// Renders Markdown documents into ATS-friendly PDF byte arrays.
/// </summary>
public interface IPdfRenderer
{
    /// <summary>
    /// Converts <paramref name="markdown"/> into a PDF document.
    /// </summary>
    /// <param name="markdown">Source Markdown document.</param>
    /// <param name="options">Rendering options (document kind, title, page size).</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>Raw PDF file bytes.</returns>
    Task<byte[]> RenderMarkdownToPdfAsync(
        string markdown,
        PdfRenderOptions options,
        CancellationToken cancellationToken = default);
}
