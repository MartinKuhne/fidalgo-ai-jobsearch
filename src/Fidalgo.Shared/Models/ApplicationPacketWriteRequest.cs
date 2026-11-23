namespace Fidalgo.Shared.Models;

/// <summary>
/// Payload of rendered PDFs, Markdown sources, and manifest metadata to atomically persist for a job packet.
/// </summary>
public sealed record ApplicationPacketWriteRequest(
    byte[] ResumePdfBytes,
    byte[] CoverLetterPdfBytes,
    string JobPostingMarkdown,
    string ResumeMarkdown,
    string CoverLetterMarkdown,
    string ModelName,
    bool ForceRegenerated);
