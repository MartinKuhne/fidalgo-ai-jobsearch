using System.Diagnostics;
using Fidalgo.Shared.ApplicationPackets;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.ErrorHandling;
using Fidalgo.Shared.Logging;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;
using Fidalgo.Shared.UserProfile;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fidalgo.Shared.Pipeline;

/// <summary>
/// Orchestrates one complete periodic pipeline cycle:
/// 1) Ingest new jobs for configured search criteria,
/// 2) Evaluate untriaged jobs against the candidate's resume, career story, and career goals,
/// 3) Generate resume and cover letter PDF packets for close-fit opportunities.
/// </summary>
public class JobPipelineRunner : IJobPipelineRunner
{
    private static readonly ActivitySource ActivitySource = new("Fidalgo.Pipeline");

    private readonly IConfiguration _configuration;
    private readonly IOptions<PipelineOptions> _pipelineOptions;
    private readonly IOptions<AdzunaConfiguration> _adzunaOptions;
    private readonly IJobIngestionService _ingestionService;
    private readonly IJobEvaluationService _evaluationService;
    private readonly IUserProfileLoader _profileLoader;
    private readonly JobRepository _repository;
    private readonly IApplicationPacketService _packetService;
    private readonly ITraceContextProvider _traceContextProvider;
    private readonly ILogger<JobPipelineRunner> _logger;

    /// <summary>Initializes a new instance of <see cref="JobPipelineRunner"/>.</summary>
    public JobPipelineRunner(
        IConfiguration configuration,
        IOptions<PipelineOptions> pipelineOptions,
        IOptions<AdzunaConfiguration> adzunaOptions,
        IJobIngestionService ingestionService,
        IJobEvaluationService evaluationService,
        IUserProfileLoader profileLoader,
        JobRepository repository,
        IApplicationPacketService packetService,
        ITraceContextProvider traceContextProvider,
        ILogger<JobPipelineRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(pipelineOptions);
        ArgumentNullException.ThrowIfNull(adzunaOptions);
        ArgumentNullException.ThrowIfNull(ingestionService);
        ArgumentNullException.ThrowIfNull(evaluationService);
        ArgumentNullException.ThrowIfNull(profileLoader);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(packetService);
        ArgumentNullException.ThrowIfNull(traceContextProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _configuration = configuration;
        _pipelineOptions = pipelineOptions;
        _adzunaOptions = adzunaOptions;
        _ingestionService = ingestionService;
        _evaluationService = evaluationService;
        _profileLoader = profileLoader;
        _repository = repository;
        _packetService = packetService;
        _traceContextProvider = traceContextProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PipelineCycleResult> RunCycleAsync(CancellationToken cancellationToken = default)
    {
        using var traceScope = FidalgoHostBootstrap.BeginRootTrace(
            ActivitySource,
            "pipeline.cycle",
            _traceContextProvider);

        var options = _pipelineOptions.Value;
        var email = _configuration["Email"];
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Skipping pipeline cycle: Email is not configured in config.json or environment variables.");
            return new PipelineCycleResult(0, 0, 0, 0, 0, DateTime.UtcNow);
        }

        var failedCount = 0;
        var ingestedCount = await RunIngestionStageAsync(email, () => failedCount++, cancellationToken);

        UserProfileContent? profile;
        try
        {
            profile = await _profileLoader.LoadAsync(overrides: null, cancellationToken);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(
                ex,
                "Skipping evaluation and packet generation for {Email}: base resume is missing.",
                email);
            return new PipelineCycleResult(ingestedCount, 0, 0, 0, failedCount, DateTime.UtcNow);
        }

        var evaluatedCount = await RunEvaluationStageAsync(email, profile, () => failedCount++, cancellationToken);
        var (generated, skipped) = options.GeneratePackets
            ? await RunCloseFitPacketStageAsync(email, options, () => failedCount++, cancellationToken)
            : (0, 0);

        var result = new PipelineCycleResult(
            IngestedCount: ingestedCount,
            EvaluatedCount: evaluatedCount,
            PacketsGenerated: generated,
            PacketsSkipped: skipped,
            FailedCount: failedCount,
            CompletedAtUtc: DateTime.UtcNow);

        _logger.LogInformation(
            "Pipeline cycle complete for {Email}: Ingested={Ingested}, Evaluated={Evaluated}, PacketsGenerated={Generated}, PacketsSkipped={Skipped}, Failed={Failed}.",
            email,
            result.IngestedCount,
            result.EvaluatedCount,
            result.PacketsGenerated,
            result.PacketsSkipped,
            result.FailedCount);

        return result;
    }

    private async Task<int> RunIngestionStageAsync(
        string email,
        Action incrementFailed,
        CancellationToken cancellationToken)
    {
        var zipCode = _configuration["ZipCode"];
        var keywordsFromList = _configuration.GetSection("JobSearchKeywords").Get<string[]>();
        var keywords = UserProfileConfig.ResolveKeywords(_configuration["Keywords"], keywordsFromList);
        var adzuna = _adzunaOptions.Value;

        if (string.IsNullOrWhiteSpace(keywords) ||
            string.IsNullOrWhiteSpace(zipCode) ||
            string.IsNullOrWhiteSpace(adzuna.AppId) ||
            string.IsNullOrWhiteSpace(adzuna.AppKey))
        {
            _logger.LogWarning(
                "Skipping job ingestion stage: Keywords, ZipCode, Adzuna:AppId, and Adzuna:AppKey are required.");
            return 0;
        }

        using var activity = ActivitySource.StartActivity("pipeline.ingest", ActivityKind.Internal);
        try
        {
            var saved = await _ingestionService.IngestAsync(email, keywords, zipCode, cancellationToken);
            _logger.LogInformation("Pipeline ingestion saved {SavedCount} new job(s) for {Email}.", saved, email);
            return saved;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            incrementFailed();
            _logger.LogError(ex, "Pipeline ingestion stage failed for {Email}.", email);
            return 0;
        }
    }

    private async Task<int> RunEvaluationStageAsync(
        string email,
        UserProfileContent profile,
        Action incrementFailed,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("pipeline.evaluate", ActivityKind.Internal);
        var untriagedJobs = await _repository.GetUntriagedAsync(email, cancellationToken);
        _logger.LogInformation("Found {UntriagedCount} untriaged job(s) for {Email}.", untriagedJobs.Count, email);

        var evaluated = 0;
        foreach (var job in untriagedJobs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _evaluationService.EvaluateJobAsync(
                    job,
                    email,
                    profile.ResumeMarkdown,
                    profile.CareerStoryMarkdown,
                    profile.CareerGoalsMarkdown,
                    cancellationToken);
                evaluated++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                incrementFailed();
                _logger.LogError(ex, "Failed to evaluate job {JobId} during pipeline cycle.", job.InternalId);
            }
        }

        return evaluated;
    }

    private async Task<(int Generated, int Skipped)> RunCloseFitPacketStageAsync(
        string email,
        PipelineOptions options,
        Action incrementFailed,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("pipeline.packets", ActivityKind.Internal);
        var allowedTiers = CloseFitSelector.ParseTiers(options.RecommendationFilter);
        var activeJobs = await _repository.QueryAsync(email, excludeDeleted: true, cancellationToken: cancellationToken);
        var closeFits = activeJobs
            .Where(j => CloseFitSelector.IsCloseFit(j, allowedTiers, options.MinScore))
            .ToList();

        _logger.LogInformation(
            "Found {CloseFitCount} close-fit job(s) (Filter={Filter}, MinScore={MinScore}) for {Email}.",
            closeFits.Count,
            options.RecommendationFilter,
            options.MinScore,
            email);

        var request = new ApplicationPacketGenerationRequest(ForceRegenerate: false, SkipExisting: true);
        var generated = 0;
        var skipped = 0;

        foreach (var job in closeFits)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await _packetService.GenerateForJobAsync(job, request, cancellationToken);
                if (result.Outcome == PacketGenerationOutcome.Generated)
                {
                    generated++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                incrementFailed();
                _logger.LogError(ex, "Failed to generate close-fit packet for job {JobId}.", job.InternalId);
            }
        }

        return (generated, skipped);
    }
}
