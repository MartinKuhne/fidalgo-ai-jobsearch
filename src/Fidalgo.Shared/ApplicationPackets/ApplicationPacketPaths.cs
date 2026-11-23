using System.Globalization;
using System.Text;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;
using Fidalgo.Shared.UserProfile;

namespace Fidalgo.Shared.ApplicationPackets;

/// <summary>
/// Pure path resolution, deterministic folder naming, slugification, and security containment
/// helpers for per-job application packet directories.
/// </summary>
public static class ApplicationPacketPaths
{
    /// <summary>Primary tailored resume PDF artifact file name.</summary>
    public const string ResumePdfFileName = "resume.pdf";

    /// <summary>Primary tailored cover letter PDF artifact file name.</summary>
    public const string CoverLetterPdfFileName = "cover-letter.pdf";

    /// <summary>Primary job posting Markdown snapshot file name.</summary>
    public const string JobPostingFileName = "job-posting.md";

    /// <summary>Supporting tailored resume Markdown source file name.</summary>
    public const string ResumeMarkdownFileName = "resume.md";

    /// <summary>Supporting cover letter Markdown source file name.</summary>
    public const string CoverLetterMarkdownFileName = "cover-letter.md";

    /// <summary>Supporting packet metadata manifest file name (not exposed for file download).</summary>
    public const string ManifestFileName = "packet.json";

    private const int MaxEmployerSlugLength = 32;
    private const int MaxTitleSlugLength = 40;

    private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static readonly HashSet<string> AllowedPacketFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ResumePdfFileName,
        CoverLetterPdfFileName,
        JobPostingFileName,
        ResumeMarkdownFileName,
        CoverLetterMarkdownFileName
    };

    /// <summary>
    /// Resolves the base output directory for application packets using documented precedence:
    /// explicit override, <see cref="ApplicationPacketOptions.OutputDirectory"/>,
    /// <c>{ProfileDirectory}/applications</c>, or <see cref="UserProfilePaths.GetDefaultApplicationsDir"/>.
    /// </summary>
    public static string ResolveBaseOutputDirectory(string? outputDirectoryOverride, ApplicationPacketOptions? options)
    {
        if (!string.IsNullOrWhiteSpace(outputDirectoryOverride))
        {
            return outputDirectoryOverride;
        }

        if (options is not null)
        {
            if (!string.IsNullOrWhiteSpace(options.OutputDirectory))
            {
                return options.OutputDirectory;
            }

            if (!string.IsNullOrWhiteSpace(options.ProfileDirectory))
            {
                return Path.Combine(options.ProfileDirectory, UserProfilePaths.ApplicationsDirectoryName);
            }
        }

        return UserProfilePaths.GetDefaultApplicationsDir();
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="fileName"/> matches one of the 5 allowed packet files.
    /// </summary>
    public static bool IsAllowedPacketFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName) && AllowedPacketFileNames.Contains(fileName);

    /// <summary>
    /// Builds a deterministic, filesystem-safe folder name for a job:
    /// <c>&lt;sanitized-employer&gt;_&lt;sanitized-title&gt;_&lt;short-id&gt;</c>.
    /// </summary>
    public static string BuildJobFolderName(JobEntity job)
    {
        ArgumentNullException.ThrowIfNull(job);

        var employerSlug = Slugify(job.Employer, MaxEmployerSlugLength, "unknown-employer");
        var titleSlug = Slugify(job.Title, MaxTitleSlugLength, "untitled-role");
        var shortId = job.InternalId.ToString("N")[..8].ToLowerInvariant();

        var folderName = $"{employerSlug}_{titleSlug}_{shortId}";
        var invalidChars = Path.GetInvalidFileNameChars();
        if (folderName.IndexOfAny(invalidChars) >= 0)
        {
            var sb = new StringBuilder(folderName.Length);
            foreach (var ch in folderName)
            {
                sb.Append(Array.IndexOf(invalidChars, ch) >= 0 ? '-' : ch);
            }
            folderName = sb.ToString();
        }

        if (IsReservedDeviceName(folderName))
        {
            folderName = $"job-{folderName}";
        }

        return folderName;
    }

    /// <summary>
    /// Resolves and verifies that the target job directory is strictly contained inside <paramref name="baseOutputDir"/>.
    /// </summary>
    public static string ResolveJobDirectory(string baseOutputDir, JobEntity job)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseOutputDir);
        ArgumentNullException.ThrowIfNull(job);

        var folderName = BuildJobFolderName(job);
        return ResolveContainedDirectory(baseOutputDir, folderName);
    }

    /// <summary>
    /// Resolves a candidate child folder path inside <paramref name="baseOutputDir"/> and asserts containment.
    /// </summary>
    internal static string ResolveContainedDirectory(string baseOutputDir, string folderName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseOutputDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

        var fullBase = Path.GetFullPath(baseOutputDir);
        var baseRoot = Path.EndsInDirectorySeparator(fullBase)
            ? fullBase
            : fullBase + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(baseRoot, folderName));

        if (!candidate.StartsWith(baseRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Resolved job directory '{candidate}' escapes base output directory '{baseRoot}'.");
        }

        return candidate;
    }

    /// <summary>
    /// Returns all resolved artifact paths inside a job's packet directory.
    /// </summary>
    public static ApplicationPacketFiles GetPacketFiles(string baseOutputDir, JobEntity job)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseOutputDir);
        ArgumentNullException.ThrowIfNull(job);

        var folderName = BuildJobFolderName(job);
        var dir = ResolveContainedDirectory(baseOutputDir, folderName);

        return new ApplicationPacketFiles(
            DirectoryPath: dir,
            FolderName: folderName,
            ResumePdfPath: Path.Combine(dir, ResumePdfFileName),
            CoverLetterPdfPath: Path.Combine(dir, CoverLetterPdfFileName),
            JobPostingPath: Path.Combine(dir, JobPostingFileName),
            ResumeMarkdownPath: Path.Combine(dir, ResumeMarkdownFileName),
            CoverLetterMarkdownPath: Path.Combine(dir, CoverLetterMarkdownFileName),
            ManifestPath: Path.Combine(dir, ManifestFileName));
    }

    /// <summary>
    /// Normalizes arbitrary text into a lowercase ASCII slug capped at <paramref name="maxLength"/> characters.
    /// </summary>
    public static string Slugify(string? value, int maxLength, string fallback)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);

        if (string.IsNullOrWhiteSpace(value))
        {
            return EnsureSafeSlug(fallback, maxLength, fallback);
        }

        var normalized = value.Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder(Math.Min(normalized.Length, maxLength));
        var previousWasHyphen = false;

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(ch);
                previousWasHyphen = false;
            }
            else if (ch is >= 'A' and <= 'Z')
            {
                sb.Append(char.ToLowerInvariant(ch));
                previousWasHyphen = false;
            }
            else if (!previousWasHyphen && sb.Length > 0)
            {
                sb.Append('-');
                previousWasHyphen = true;
            }

            if (sb.Length >= maxLength)
            {
                break;
            }
        }

        var slug = sb.ToString().Trim('-');
        if (string.IsNullOrEmpty(slug))
        {
            return EnsureSafeSlug(fallback, maxLength, fallback);
        }

        if (IsReservedDeviceName(slug))
        {
            var prefixed = $"job-{slug}";
            slug = prefixed.Length > maxLength ? prefixed[..maxLength].TrimEnd('-') : prefixed;
        }

        return string.IsNullOrEmpty(slug) ? fallback : slug;
    }

    private static string EnsureSafeSlug(string value, int maxLength, string fallback)
    {
        var trimmed = value.Length > maxLength ? value[..maxLength].TrimEnd('-') : value;
        return string.IsNullOrEmpty(trimmed) ? fallback : trimmed;
    }

    private static bool IsReservedDeviceName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var dotIndex = name.IndexOf('.');
        var stem = dotIndex >= 0 ? name[..dotIndex] : name;
        return WindowsReservedNames.Contains(stem);
    }
}
