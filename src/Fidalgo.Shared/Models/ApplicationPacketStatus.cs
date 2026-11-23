namespace Fidalgo.Shared.Models;

/// <summary>
/// Current on-disk status and Markdown previews for a job's application packet.
/// </summary>
public sealed record ApplicationPacketStatus(
    bool Exists,
    string FolderName,
    string DirectoryPath,
    DateTime? GeneratedAtUtc,
    string? ResumeMarkdown,
    string? CoverLetterMarkdown);
