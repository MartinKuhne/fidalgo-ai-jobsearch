namespace Fidalgo.Host.Models;

/// <summary>
/// Response returned by <c>POST /api/jobs/{id}/packet</c> after generating or returning an existing packet.
/// </summary>
public sealed record ApplicationPacketResultDto(
    string Outcome,
    Guid JobId,
    string FolderName,
    string DirectoryPath,
    DateTime GeneratedAtUtc,
    PacketFilesDto Files,
    string ResumeMarkdown,
    string CoverLetterMarkdown);
