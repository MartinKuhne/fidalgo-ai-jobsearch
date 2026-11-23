namespace Fidalgo.Shared.Models;

/// <summary>
/// Result returned after generating or skipping an application packet for a job.
/// </summary>
public sealed record ApplicationPacketResult(
    PacketGenerationOutcome Outcome,
    Guid JobId,
    string FolderName,
    string DirectoryPath,
    DateTime GeneratedAtUtc,
    string ResumeMarkdown,
    string CoverLetterMarkdown);
