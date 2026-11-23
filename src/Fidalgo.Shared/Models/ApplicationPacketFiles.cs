namespace Fidalgo.Shared.Models;

/// <summary>
/// Resolved file system paths for all artifacts inside a per-job application packet directory.
/// </summary>
public sealed record ApplicationPacketFiles(
    string DirectoryPath,
    string FolderName,
    string ResumePdfPath,
    string CoverLetterPdfPath,
    string JobPostingPath,
    string ResumeMarkdownPath,
    string CoverLetterMarkdownPath,
    string ManifestPath);
