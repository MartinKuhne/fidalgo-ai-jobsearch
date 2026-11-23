namespace Fidalgo.Host.Models;

/// <summary>
/// API URLs for previewing and downloading artifacts inside a job's application packet.
/// </summary>
public sealed record PacketFilesDto(
    string ResumePdfUrl,
    string CoverLetterPdfUrl,
    string JobPostingUrl);
