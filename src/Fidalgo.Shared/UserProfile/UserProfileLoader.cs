using Fidalgo.Shared.ApplicationPackets;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.ErrorHandling;
using Fidalgo.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fidalgo.Shared.UserProfile;

/// <summary>
/// Loads candidate profile documents via <see cref="UserProfileResolver"/> with support for
/// file-path overrides, inline Markdown overrides, and output directory resolution.
/// </summary>
public class UserProfileLoader : IUserProfileLoader
{
    private readonly UserProfileResolver _resolver;
    private readonly IOptions<ApplicationPacketOptions> _packetOptions;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserProfileLoader> _logger;

    /// <summary>Initializes a new instance of <see cref="UserProfileLoader"/>.</summary>
    public UserProfileLoader(
        UserProfileResolver resolver,
        IOptions<ApplicationPacketOptions> packetOptions,
        IConfiguration configuration,
        ILogger<UserProfileLoader> logger)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(packetOptions);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _resolver = resolver;
        _packetOptions = packetOptions;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UserProfileContent> LoadAsync(
        UserProfileLoadOverrides? overrides = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string resumeMarkdown;
        string? resumePath = null;
        bool resumeFromOverride;

        if (!string.IsNullOrWhiteSpace(overrides?.ResumeContentOverride))
        {
            resumeMarkdown = overrides.ResumeContentOverride;
            resumeFromOverride = true;
        }
        else
        {
            resumePath = _resolver.ResolveResumePath(overrides?.ResumePath);
            if (string.IsNullOrWhiteSpace(resumePath))
            {
                throw new ValidationException(
                    "Base resume is required.",
                    new Dictionary<string, string[]>
                    {
                        ["baseResume"] = ["A base resume is required. Provide a resume override or create resume.md in Documents\\Fidalgo."]
                    });
            }

            resumeMarkdown = await File.ReadAllTextAsync(resumePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(resumeMarkdown))
            {
                throw new ValidationException(
                    "Base resume cannot be empty.",
                    new Dictionary<string, string[]>
                    {
                        ["baseResume"] = ["The resolved resume file is empty. Provide non-empty Markdown content."]
                    });
            }

            resumeFromOverride = !string.IsNullOrWhiteSpace(overrides?.ResumePath);
        }

        var (careerStoryMarkdown, careerStoryPath) = await LoadOptionalDocumentAsync(
            overrides?.CareerStoryContentOverride,
            overrides?.CareerStoryPath,
            _resolver.ResolveCareerStoryPath,
            cancellationToken);

        var (careerGoalsMarkdown, careerGoalsPath) = await LoadOptionalDocumentAsync(
            overrides?.CareerGoalsContentOverride,
            overrides?.CareerGoalsPath,
            _resolver.ResolveCareerGoalsPath,
            cancellationToken);

        var defaultEmail = _configuration["Email"];
        if (string.IsNullOrWhiteSpace(defaultEmail))
        {
            defaultEmail = null;
        }

        var applicationsOutputDir = ApplicationPacketPaths.ResolveBaseOutputDirectory(
            overrides?.OutputDirectoryOverride,
            _packetOptions.Value);

        var metadata = new ProfileSourceMetadata(
            ResumeFromOverride: resumeFromOverride,
            ResumePath: resumePath,
            HasCareerStory: !string.IsNullOrWhiteSpace(careerStoryMarkdown),
            CareerStoryPath: careerStoryPath,
            HasCareerGoals: !string.IsNullOrWhiteSpace(careerGoalsMarkdown),
            CareerGoalsPath: careerGoalsPath);

        _logger.LogDebug(
            "Loaded user profile (ResumeFromOverride={ResumeFromOverride}, HasCareerStory={HasCareerStory}, HasCareerGoals={HasCareerGoals}).",
            metadata.ResumeFromOverride,
            metadata.HasCareerStory,
            metadata.HasCareerGoals);

        return new UserProfileContent(
            ResumeMarkdown: resumeMarkdown,
            CareerStoryMarkdown: careerStoryMarkdown,
            CareerGoalsMarkdown: careerGoalsMarkdown,
            DefaultEmail: defaultEmail,
            ApplicationsOutputDir: applicationsOutputDir,
            Sources: metadata);
    }

    private static async Task<(string? Content, string? Path)> LoadOptionalDocumentAsync(
        string? inlineOverride,
        string? pathOverride,
        Func<string?, string?> resolvePath,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(inlineOverride))
        {
            return (inlineOverride, null);
        }

        var resolvedPath = resolvePath(pathOverride);
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            return (null, null);
        }

        var content = await File.ReadAllTextAsync(resolvedPath, cancellationToken);
        return string.IsNullOrWhiteSpace(content)
            ? (null, resolvedPath)
            : (content, resolvedPath);
    }
}
