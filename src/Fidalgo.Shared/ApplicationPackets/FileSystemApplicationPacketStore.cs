using System.Text;
using System.Text.Json;
using Fidalgo.Shared.ErrorHandling;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Shared.ApplicationPackets;

/// <summary>
/// File-system backed <see cref="IApplicationPacketStore"/> using temporary staging files and atomic
/// <see cref="File.Move(string, string, bool)"/> promotion.
/// </summary>
public class FileSystemApplicationPacketStore : IApplicationPacketStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ILogger<FileSystemApplicationPacketStore> _logger;

    /// <summary>Initializes a new instance of <see cref="FileSystemApplicationPacketStore"/>.</summary>
    public FileSystemApplicationPacketStore(ILogger<FileSystemApplicationPacketStore> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ApplicationPacketStatus> GetStatusAsync(
        string baseOutputDir,
        JobEntity job,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseOutputDir);
        ArgumentNullException.ThrowIfNull(job);
        cancellationToken.ThrowIfCancellationRequested();

        var files = ApplicationPacketPaths.GetPacketFiles(baseOutputDir, job);

        var isComplete =
            IsNonEmptyFile(files.ResumePdfPath) &&
            IsNonEmptyFile(files.CoverLetterPdfPath) &&
            IsNonEmptyFile(files.JobPostingPath);

        DateTime? generatedAtUtc = null;
        if (isComplete)
        {
            generatedAtUtc = await TryReadManifestTimestampAsync(files.ManifestPath, cancellationToken)
                ?? File.GetLastWriteTimeUtc(files.ResumePdfPath);
        }

        string? resumeMarkdown = File.Exists(files.ResumeMarkdownPath)
            ? await File.ReadAllTextAsync(files.ResumeMarkdownPath, cancellationToken)
            : null;

        string? coverLetterMarkdown = File.Exists(files.CoverLetterMarkdownPath)
            ? await File.ReadAllTextAsync(files.CoverLetterMarkdownPath, cancellationToken)
            : null;

        return new ApplicationPacketStatus(
            Exists: isComplete,
            FolderName: files.FolderName,
            DirectoryPath: files.DirectoryPath,
            GeneratedAtUtc: generatedAtUtc,
            ResumeMarkdown: resumeMarkdown,
            CoverLetterMarkdown: coverLetterMarkdown);
    }

    /// <inheritdoc/>
    public async Task<ApplicationPacketFiles> SavePacketAsync(
        string baseOutputDir,
        JobEntity job,
        ApplicationPacketWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseOutputDir);
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var files = ApplicationPacketPaths.GetPacketFiles(baseOutputDir, job);
        Directory.CreateDirectory(files.DirectoryPath);

        var generatedAtUtc = DateTime.UtcNow;
        var manifestJson = JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["jobId"] = job.InternalId,
                ["generatedAtUtc"] = generatedAtUtc,
                ["model"] = request.ModelName,
                ["forceRegenerated"] = request.ForceRegenerated
            },
            JsonOptions);

        var stagedPairs = new List<(string StagingPath, string TargetPath)>(6);
        try
        {
            stagedPairs.Add((await StageBytesAsync(files.ResumePdfPath, request.ResumePdfBytes, cancellationToken), files.ResumePdfPath));
            stagedPairs.Add((await StageBytesAsync(files.CoverLetterPdfPath, request.CoverLetterPdfBytes, cancellationToken), files.CoverLetterPdfPath));
            stagedPairs.Add((await StageTextAsync(files.JobPostingPath, request.JobPostingMarkdown, cancellationToken), files.JobPostingPath));
            stagedPairs.Add((await StageTextAsync(files.ResumeMarkdownPath, request.ResumeMarkdown, cancellationToken), files.ResumeMarkdownPath));
            stagedPairs.Add((await StageTextAsync(files.CoverLetterMarkdownPath, request.CoverLetterMarkdown, cancellationToken), files.CoverLetterMarkdownPath));
            stagedPairs.Add((await StageTextAsync(files.ManifestPath, manifestJson, cancellationToken), files.ManifestPath));

            foreach (var (stagingPath, targetPath) in stagedPairs)
            {
                File.Move(stagingPath, targetPath, overwrite: true);
            }

            _logger.LogInformation(
                "Saved application packet for job {JobId} to {FolderName} (ResumePdfBytes={ResumePdfBytes}, CoverLetterPdfBytes={CoverLetterPdfBytes}).",
                job.InternalId,
                files.FolderName,
                request.ResumePdfBytes.Length,
                request.CoverLetterPdfBytes.Length);

            return files;
        }
        catch
        {
            foreach (var (stagingPath, _) in stagedPairs)
            {
                TryDeleteFile(stagingPath);
            }

            throw;
        }
    }

    /// <inheritdoc/>
    public Task<Stream?> OpenPacketFileAsync(
        string baseOutputDir,
        JobEntity job,
        string allowedFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseOutputDir);
        ArgumentNullException.ThrowIfNull(job);
        cancellationToken.ThrowIfCancellationRequested();

        if (!ApplicationPacketPaths.IsAllowedPacketFileName(allowedFileName))
        {
            throw new ValidationException(
                "Invalid packet file name.",
                new Dictionary<string, string[]>
                {
                    ["fileName"] = [$"File '{allowedFileName}' is not an allowed application packet file."]
                });
        }

        var files = ApplicationPacketPaths.GetPacketFiles(baseOutputDir, job);
        var targetPath = allowedFileName.ToLowerInvariant() switch
        {
            ApplicationPacketPaths.ResumePdfFileName => files.ResumePdfPath,
            ApplicationPacketPaths.CoverLetterPdfFileName => files.CoverLetterPdfPath,
            ApplicationPacketPaths.JobPostingFileName => files.JobPostingPath,
            ApplicationPacketPaths.ResumeMarkdownFileName => files.ResumeMarkdownPath,
            ApplicationPacketPaths.CoverLetterMarkdownFileName => files.CoverLetterMarkdownPath,
            _ => throw new ValidationException(
                "Invalid packet file name.",
                new Dictionary<string, string[]>
                {
                    ["fileName"] = [$"File '{allowedFileName}' is not an allowed application packet file."]
                })
        };

        if (!File.Exists(targetPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            targetPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    private static bool IsNonEmptyFile(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        return new FileInfo(path).Length > 0;
    }

    private static async Task<DateTime?> TryReadManifestTimestampAsync(string manifestPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("generatedAtUtc", out var prop) &&
                prop.TryGetDateTime(out var timestamp))
            {
                return DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
            }
        }
        catch (JsonException)
        {
            // Ignore malformed manifest and let caller fall back to file timestamp.
        }

        return null;
    }

    private static async Task<string> StageBytesAsync(string targetPath, byte[] bytes, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(targetPath)!;
        var stagingPath = Path.Combine(dir, $".{Path.GetFileName(targetPath)}.tmp-{Guid.NewGuid():N}");
        await File.WriteAllBytesAsync(stagingPath, bytes, cancellationToken);
        return stagingPath;
    }

    private static async Task<string> StageTextAsync(string targetPath, string content, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(targetPath)!;
        var stagingPath = Path.Combine(dir, $".{Path.GetFileName(targetPath)}.tmp-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(stagingPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
        return stagingPath;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort staging cleanup.
        }
    }
}
