using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.Pipeline;
using Microsoft.Extensions.Options;

namespace Fidalgo.Host.Services;

/// <summary>
/// Hosted background service that continuously executes the job ingestion, evaluation,
/// and close-fit application packet generation pipeline on a configurable interval (default: 4 hours).
/// </summary>
public class JobPipelineBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<PipelineOptions> _options;
    private readonly PipelineStatusTracker _statusTracker;
    private readonly ILogger<JobPipelineBackgroundService> _logger;

    /// <summary>Initializes a new instance of <see cref="JobPipelineBackgroundService"/>.</summary>
    public JobPipelineBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<PipelineOptions> options,
        PipelineStatusTracker statusTracker,
        ILogger<JobPipelineBackgroundService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(statusTracker);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _options = options;
        _statusTracker = statusTracker;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.Value;
        if (!opts.Enabled)
        {
            _logger.LogInformation("Continuous job pipeline is disabled (Pipeline:Enabled=false).");
            return;
        }

        _logger.LogInformation(
            "Continuous job pipeline enabled: running every {IntervalHours} hour(s) (MinScore={MinScore}, Filter={RecommendationFilter}, RunOnStartup={RunOnStartup}).",
            opts.IntervalHours,
            opts.MinScore,
            opts.RecommendationFilter,
            opts.RunOnStartup);

        if (opts.RunOnStartup)
        {
            await RunSingleCycleSafeAsync(stoppingToken);
        }

        var interval = TimeSpan.FromHours(Math.Max(0.01, opts.IntervalHours));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSingleCycleSafeAsync(stoppingToken);
        }
    }

    /// <summary>Executes one scoped pipeline cycle, isolating failures so the timer loop stays alive.</summary>
    public async Task RunSingleCycleSafeAsync(CancellationToken cancellationToken)
    {
        _statusTracker.MarkStarted(DateTime.UtcNow);
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var runner = scope.ServiceProvider.GetRequiredService<IJobPipelineRunner>();
            var result = await runner.RunCycleAsync(cancellationToken);
            _statusTracker.MarkCompleted(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _statusTracker.MarkFailed("Pipeline cycle cancelled.", DateTime.UtcNow);
            throw;
        }
        catch (Exception ex)
        {
            _statusTracker.MarkFailed(ex.Message, DateTime.UtcNow);
            _logger.LogError(ex, "Unhandled exception during continuous job pipeline cycle.");
        }
    }
}
