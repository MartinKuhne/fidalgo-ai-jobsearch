using System.Diagnostics;
using Fidalgo.Agent.Agents;
using Fidalgo.Agent.Configuration;
using Fidalgo.Shared.ApplicationPackets;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.ErrorHandling;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;
using Fidalgo.Shared.Tools;
using Fidalgo.Shared.UserProfile;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace Fidalgo.Agent;

/// <summary>
/// Executes <c>Fidalgo.Agent</c> CLI modes asynchronously: job querying, job discarding,
/// single-job and batch application packet generation, and untriaged job evaluation.
/// </summary>
public class AgentRunner
{
    private static readonly ActivitySource ActivitySource = new("Fidalgo.Agent");
    private readonly JobRepository _repository;
    private readonly GetJobsTool _getJobsTool;
    private readonly IApplicationPacketService _packetService;
    private readonly IUserProfileLoader _profileLoader;
    private readonly IOptions<ApplicationPacketOptions> _packetOptions;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AgentRunner> _logger;
    private readonly ChatClient? _chatClient;
    private readonly TextWriter _output;

    /// <summary>Initializes a new instance of <see cref="AgentRunner"/>.</summary>
    public AgentRunner(
        JobRepository repository,
        GetJobsTool getJobsTool,
        IApplicationPacketService packetService,
        IUserProfileLoader profileLoader,
        IOptions<ApplicationPacketOptions> packetOptions,
        ILoggerFactory loggerFactory,
        ChatClient? chatClient = null,
        TextWriter? output = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(getJobsTool);
        ArgumentNullException.ThrowIfNull(packetService);
        ArgumentNullException.ThrowIfNull(profileLoader);
        ArgumentNullException.ThrowIfNull(packetOptions);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _repository = repository;
        _getJobsTool = getJobsTool;
        _packetService = packetService;
        _profileLoader = profileLoader;
        _packetOptions = packetOptions;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<AgentRunner>();
        _chatClient = chatClient;
        _output = output ?? Console.Out;
    }

    /// <summary>
    /// Dispatches the requested agent execution mode based on <paramref name="options"/>.
    /// </summary>
    public async Task<int> RunAsync(CliOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var operationName = Activity.Current is null ? "agent.run" : "agent.dispatch";
        using var traceScope = FidalgoHostBootstrap.BeginRootTrace(ActivitySource, operationName);

        if (options.QueryJobs)
        {
            return await RunQueryJobsModeAsync(options, cancellationToken);
        }

        if (options.DiscardJobId is not null)
        {
            return await RunDiscardJobModeAsync(options.DiscardJobId.Value, cancellationToken);
        }

        if (options.ListDiscarded)
        {
            return await RunListDiscardedModeAsync(options.Email, cancellationToken);
        }

        if (options.TargetJobId is not null)
        {
            return await RunTargetJobPacketModeAsync(options, options.TargetJobId.Value, cancellationToken);
        }

        if (options.GeneratePackets)
        {
            return await RunGeneratePacketsModeAsync(options, cancellationToken);
        }

        return await RunEvaluationModeAsync(options, cancellationToken);
    }

    private async Task<int> RunQueryJobsModeAsync(CliOptions options, CancellationToken cancellationToken)
    {
        var jobs = await _getJobsTool.GetAsync(
            options.Email,
            options.DateFrom,
            options.DateTo,
            options.EmployerFilter,
            null,
            options.SourceWebsiteFilter,
            cancellationToken);

        await _output.WriteLineAsync($"Found {jobs.Count} jobs:");
        foreach (var job in jobs)
        {
            await _output.WriteLineAsync($"- [{job.Employer}] {job.EmployerJobId} (Score: {job.Score}, {job.Recommendation})");
        }

        return 0;
    }

    private async Task<int> RunDiscardJobModeAsync(Guid internalId, CancellationToken cancellationToken)
    {
        var success = await _repository.SoftDeleteAsync(internalId, cancellationToken);
        if (success)
        {
            await _output.WriteLineAsync($"Job {internalId} marked as discarded");
            return 0;
        }

        await _output.WriteLineAsync($"Job {internalId} not found");
        return 1;
    }

    private async Task<int> RunListDiscardedModeAsync(string email, CancellationToken cancellationToken)
    {
        var jobs = await _repository.GetDiscardedAsync(email, cancellationToken);
        await _output.WriteLineAsync($"Found {jobs.Count} discarded jobs:");
        foreach (var job in jobs)
        {
            await _output.WriteLineAsync($"- [{job.Employer}] {job.EmployerJobId} (Score: {job.Score}, {job.Recommendation})");
        }

        return 0;
    }

    private async Task<int> RunTargetJobPacketModeAsync(
        CliOptions options,
        Guid targetJobId,
        CancellationToken cancellationToken)
    {
        var job = await _repository.GetByIdAsync(targetJobId, cancellationToken);
        if (job is null || job.IsDeleted)
        {
            _logger.LogWarning("Target job {JobId} was not found or has been discarded.", targetJobId);
            await _output.WriteLineAsync($"Error: Job {targetJobId} was not found or has been discarded.");
            return 1;
        }

        var overrides = BuildOverrides(options);
        var profile = await TryLoadProfileAsync(overrides, cancellationToken);
        if (profile is null)
        {
            return 1;
        }

        var request = BuildPacketRequest(options, overrides);
        try
        {
            var result = await _packetService.GenerateForJobAsync(job, request, cancellationToken);
            await _output.WriteLineAsync($"Application packet {result.Outcome} for job {job.InternalId} at {result.DirectoryPath}");
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate application packet for target job {JobId}.", targetJobId);
            await _output.WriteLineAsync($"Error generating application packet for job {targetJobId}: {ex.Message}");
            return 1;
        }
    }

    private async Task<int> RunGeneratePacketsModeAsync(CliOptions options, CancellationToken cancellationToken)
    {
        var overrides = BuildOverrides(options);
        var profile = await TryLoadProfileAsync(overrides, cancellationToken);
        if (profile is null)
        {
            return 1;
        }

        await GeneratePacketsBatchAsync(options, overrides, cancellationToken);
        return 0;
    }

    private async Task<int> RunEvaluationModeAsync(CliOptions options, CancellationToken cancellationToken)
    {
        var overrides = BuildOverrides(options);
        var profile = await TryLoadProfileAsync(overrides, cancellationToken);
        if (profile is null)
        {
            return 1;
        }

        var jobs = await _repository.GetUntriagedAsync(options.Email, cancellationToken);
        await _output.WriteLineAsync($"Found {jobs.Count} untriaged jobs.");

        int totalProcessed = 0;
        if (jobs.Count > 0)
        {
            if (_chatClient is null)
            {
                throw new InvalidOperationException("ChatClient is required to evaluate untriaged jobs.");
            }

            var evalLogger = _loggerFactory.CreateLogger<JobEvaluationAgent>();
            var agent = new JobEvaluationAgent(
                _chatClient,
                _repository,
                options.Email,
                profile.ResumeMarkdown,
                evalLogger,
                profile.CareerStoryMarkdown,
                profile.CareerGoalsMarkdown);

            foreach (var job in jobs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _output.WriteLineAsync($"Evaluating job: {job.Title} at {job.Employer}...");
                try
                {
                    await agent.EvaluateAsync(job, cancellationToken);
                    totalProcessed++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    await _output.WriteLineAsync($"Failed to evaluate job {job.InternalId}: {ex.Message}");
                    evalLogger.LogError(ex, "Failed to evaluate job {JobId}", job.InternalId);
                }
            }
        }

        await _output.WriteLineAsync($"Evaluation complete. Processed {totalProcessed} jobs.");

        if (_packetOptions.Value.AutoGenerateForApply)
        {
            await GeneratePacketsBatchAsync(options, overrides, cancellationToken);
        }

        return 0;
    }

    private async Task GeneratePacketsBatchAsync(
        CliOptions options,
        UserProfileLoadOverrides overrides,
        CancellationToken cancellationToken)
    {
        var allowedTiers = CloseFitSelector.ParseTiers(options.RecommendationFilter, _packetOptions.Value.RecommendationFilter);
        var minScore = options.MinScore ?? 0;
        var allJobs = await _repository.QueryAsync(options.Email, excludeDeleted: true, cancellationToken: cancellationToken);
        var matchingJobs = allJobs
            .Where(j => CloseFitSelector.IsCloseFit(j, allowedTiers, minScore))
            .ToList();

        var request = BuildPacketRequest(options, overrides);

        int generated = 0;
        int skipped = 0;
        int failed = 0;

        foreach (var job in matchingJobs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await _packetService.GenerateForJobAsync(job, request, cancellationToken);
                if (result.Outcome == PacketGenerationOutcome.Generated)
                {
                    generated++;
                    _logger.LogInformation(
                        "Generated application packet for job {JobId} in {DirectoryPath}.",
                        job.InternalId,
                        result.DirectoryPath);
                }
                else
                {
                    skipped++;
                    _logger.LogInformation(
                        "Skipped existing application packet for job {JobId} in {DirectoryPath}.",
                        job.InternalId,
                        result.DirectoryPath);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Failed to generate application packet for job {JobId}.", job.InternalId);
                await _output.WriteLineAsync($"Failed to generate packet for job {job.InternalId}: {ex.Message}");
            }
        }

        await _output.WriteLineAsync(
            $"Application packet generation complete. Generated: {generated}, Skipped: {skipped}, Failed: {failed}");
    }

    private async Task<UserProfileContent?> TryLoadProfileAsync(
        UserProfileLoadOverrides overrides,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _profileLoader.LoadAsync(overrides, cancellationToken);
        }
        catch (ValidationException ex)
        {
            _logger.LogError(ex, "Base resume could not be loaded.");
            await _output.WriteLineAsync(
                $"Error: A base resume is required. Place resume.md in Documents\\Fidalgo\\resume.md or pass --Resume <path>. ({ex.Message})");
            return null;
        }
    }

    private static UserProfileLoadOverrides BuildOverrides(CliOptions options) =>
        new(
            ResumePath: options.Resume,
            CareerStoryPath: options.CareerStory,
            CareerGoalsPath: options.CareerGoals,
            OutputDirectoryOverride: options.OutputDir);

    private static ApplicationPacketGenerationRequest BuildPacketRequest(
        CliOptions options,
        UserProfileLoadOverrides overrides)
    {
        var effectiveForce = options.ForceRegenerate || options.SkipExisting == false;
        return new ApplicationPacketGenerationRequest(
            ProfileOverrides: overrides,
            ForceRegenerate: effectiveForce,
            SkipExisting: options.SkipExisting ?? true);
    }
}
