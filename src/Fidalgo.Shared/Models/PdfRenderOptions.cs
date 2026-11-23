namespace Fidalgo.Shared.Models;

/// <summary>
/// Options controlling HTML templating and PDF page layout.
/// </summary>
public sealed record PdfRenderOptions(
    PdfDocumentKind Kind,
    string PageSize = "Letter");
