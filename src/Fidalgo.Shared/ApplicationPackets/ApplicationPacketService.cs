using System.Diagnostics;
using Fidalgo.Shared.ApplicationPackets.Prompts;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.ErrorHandling;
using Fidalgo.Shared.Llm;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.Pdf;
using Fidalgo.Shared.Storage;
using Fidalgo.Shared.UserProfile;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fidalgo.Shared.ApplicationPackets;

/// <summary>
/// Orchestrates profile loading, two-step LLM tailoring (resume + cover letter), PDF rendering,
/// and atomic packet persistence for a job posting.
/// </summary>
public class ApplicationPacketService : IApplicationPacketService
{
    private static readonly ActivitySource ActivitySource = new("Fidalgo.ApplicationPacket");

    private readonly JobRepository _jobRepository;
    private readonly IUserProfileLoader _profileLoader;
    private readonly ITextGenerator _textGenerator;
    private readonly IPdfRenderer _pdfRenderer;
    private readonly IApplicationPacketStore _packetStore;
    private readonly IOptions<ApplicationPacketOptions> _packetOptions;
    private readonly IOptions<LlmConfiguration> _llmOptions;
    private readonly ILogger<ApplicationPacketService> _logger;

    /// <summary>Initializes a new instance of <see cref="ApplicationPacketService"/>.</summary>
    public ApplicationPacketService(
        JobRepository jobRepository,
        IUserProfileLoader profileLoader,
        ITextGenerator textGenerator,
        IPdfRenderer pdfRenderer,
        IApplicationPacketStore packetStore,
        IOptions<ApplicationPacketOptions> packetOptions,
        IOptions<LlmConfiguration> llmOptions,
        ILogger<ApplicationPacketService> logger)
    {
        ArgumentNullException.ThrowIfNull(jobRepository);
        ArgumentNullException.ThrowIfNull(profileLoader);
        ArgumentNullException.ThrowIfNull(textGenerator);
        ArgumentNullException.ThrowIfNull(pdfRenderer);
        ArgumentNullException.ThrowIfNull(packetStore);
        ArgumentNullException.ThrowIfNull(packetOptions);
        ArgumentNullException.ThrowIfNull(llmOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _jobRepository = jobRepository;
        _profileLoader = profileLoader;
        _textGenerator = textGenerator;
        _pdfRenderer = pdfRenderer;
        _packetStore = packetStore;
        _packetOptions = packetOptions;
        _llmOptions = llmOptions;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ApplicationPacketResult> GenerateForJobAsync(
        Guid jobId,
        ApplicationPacketGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken);
        if (job is null || job.IsDeleted)
        {
            throw new NotFoundException($"Job '{jobId}' was not found.");
        }

        return await GenerateForJobAsync(job, request, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ApplicationPacketResult> GenerateForJobAsync(
        JobEntity job,
        ApplicationPacketGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(request);

        if (job.IsDeleted)
        {
            throw new NotFoundException($"Job '{job.InternalId}' was not found.");
        }

        using var rootActivity = ActivitySource.StartActivity("packet.generate", ActivityKind.Internal);
        rootActivity?.SetTag("job.id", job.InternalId);

        var effectiveForce = request.ForceRegenerate || !request.SkipExisting;
        var baseOutputDir = ApplicationPacketPaths.ResolveBaseOutputDirectory(
            request.ProfileOverrides?.OutputDirectoryOverride,
            _packetOptions.Value);

        var status = await _packetStore.GetStatusAsync(baseOutputDir, job, cancellationToken);
        if (status.Exists && !effectiveForce)
        {
            _logger.LogInformation(
                "Skipping existing application packet for job {JobId} in {FolderName}.",
                job.InternalId,
                status.FolderName);

            return new ApplicationPacketResult(
                Outcome: PacketGenerationOutcome.SkippedExisting,
                JobId: job.InternalId,
                FolderName: status.FolderName,
                DirectoryPath: status.DirectoryPath,
                GeneratedAtUtc: status.GeneratedAtUtc ?? DateTime.UtcNow,
                ResumeMarkdown: status.ResumeMarkdown ?? string.Empty,
                CoverLetterMarkdown: status.CoverLetterMarkdown ?? string.Empty);
        }

        UserProfileContent profile;
        using (ActivitySource.StartActivity("profile.load", ActivityKind.Internal))
        {
            profile = await _profileLoader.LoadAsync(request.ProfileOverrides, cancellationToken);
        }

        var llmTimeout = TimeSpan.FromSeconds(Math.Max(1, _packetOptions.Value.LlmTimeoutSeconds));
        var textOptions = new TextGenerationOptions(Timeout: llmTimeout);

        string resumeMarkdown;
        using (ActivitySource.StartActivity("llm.tailor_resume", ActivityKind.Internal))
        {
            var rawResume = await _textGenerator.GenerateAsync(
                ResumeTailoringPromptBuilder.BuildSystemPrompt(),
                ResumeTailoringPromptBuilder.BuildUserPrompt(profile, job),
                textOptions,
                cancellationToken);
            resumeMarkdown = MarkdownResponseNormalizer.Normalize(rawResume);
        }

        string coverLetterMarkdown;
        using (ActivitySource.StartActivity("llm.cover_letter", ActivityKind.Internal))
        {
            var rawCoverLetter = await _textGenerator.GenerateAsync(
                CoverLetterPromptBuilder.BuildSystemPrompt(),
                CoverLetterPromptBuilder.BuildUserPrompt(profile, job),
                textOptions,
                cancellationToken);
            coverLetterMarkdown = MarkdownResponseNormalizer.Normalize(rawCoverLetter);
        }

        var pageSize = string.IsNullOrWhiteSpace(_packetOptions.Value.ResumePageSize)
            ? "Letter"
            : _packetOptions.Value.ResumePageSize;
        var roleLabel = string.IsNullOrWhiteSpace(job.Title) ? "Role" : job.Title;
        var employerLabel = string.IsNullOrWhiteSpace(job.Employer) ? "Employer" : job.Employer;

        byte[] resumePdfBytes;
        byte[] coverLetterPdfBytes;
        using (ActivitySource.StartActivity("pdf.render.batch", ActivityKind.Internal))
        {
            resumePdfBytes = await _pdfRenderer.RenderMarkdownToPdfAsync(
                resumeMarkdown,
                new PdfRenderOptions(PdfDocumentKind.Resume, $"{employerLabel} - {roleLabel} Resume", pageSize),
                cancellationToken);

            coverLetterPdfBytes = await _pdfRenderer.RenderMarkdownToPdfAsync(
                coverLetterMarkdown,
                new PdfRenderOptions(PdfDocumentKind.CoverLetter, $"{employerLabel} - {roleLabel} Cover Letter", pageSize),
                cancellationToken);
        }

        var jobPostingMarkdown = JobPostingSnapshotBuilder.Build(job);
        var modelName = _llmOptions.Value.Model ?? string.Empty;

        ApplicationPacketFiles savedFiles;
        using (ActivitySource.StartActivity("packet.store", ActivityKind.Internal))
        {
            savedFiles = await _packetStore.SavePacketAsync(
                baseOutputDir,
                job,
                new ApplicationPacketWriteRequest(
                    ResumePdfBytes: resumePdfBytes,
                    CoverLetterPdfBytes: coverLetterPdfBytes,
                    JobPostingMarkdown: jobPostingMarkdown,
                    ResumeMarkdown: resumeMarkdown,
                    CoverLetterMarkdown: coverLetterMarkdown,
                    ModelName: modelName,
                    ForceRegenerated: effectiveForce),
                cancellationToken);
        }

        return new ApplicationPacketResult(
            Outcome: PacketGenerationOutcome.Generated,
            JobId: job.InternalId,
            FolderName: savedFiles.FolderName,
            DirectoryPath: savedFiles.DirectoryPath,
            GeneratedAtUtc: DateTime.UtcNow,
            ResumeMarkdown: resumeMarkdown,
            CoverLetterMarkdown: coverLetterMarkdown);
    }
}
