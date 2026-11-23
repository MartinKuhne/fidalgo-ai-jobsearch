namespace Fidalgo.Host.Models;

/// <summary>
/// Current on-disk application packet status returned by <c>GET /api/jobs/{id}/packet</c>.
/// </summary>
public sealed record ApplicationPacketStatusDto(
    bool Exists,
    string FolderName,
    string DirectoryPath,
    DateTime? GeneratedAtUtc,
    PacketFilesDto Files,
    string? ResumeMarkdown,
    string? CoverLetterMarkdown);
