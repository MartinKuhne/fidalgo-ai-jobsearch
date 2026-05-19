using Fidalgo.Agent.Configuration;
using Fidalgo.Agent.DependencyInjection;
using Fidalgo.Agent.Scraping;
using Fidalgo.Agent.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Agent.Agents;

/// <summary>
/// Background service that runs job searches on a schedule.
/// </summary>
public class JobSearchAgent : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobSearchAgent> _logger;
    private readonly TimeSpan _searchInterval;

    /// <summary>
    /// Creates a new instance of the JobSearchAgent.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="searchInterval">Interval between search cycles. Defaults to 4 hours.</param>
    public JobSearchAgent(IServiceProvider serviceProvider, ILogger<JobSearchAgent> logger, TimeSpan? searchInterval = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _searchInterval = searchInterval ?? TimeSpan.FromHours(4);
    }

    /// <summary>
    /// Executes the background service loop.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobSearchAgent started. Search interval: {Interval}", _searchInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSearchCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during search cycle");
            }

            await Task.Delay(_searchInterval, stoppingToken);
        }

        _logger.LogInformation("JobSearchAgent stopped");
    }

    private async Task RunSearchCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var configRepo = scope.ServiceProvider.GetRequiredService<ConfigRepository>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<SearchOrchestrator>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<JobSearchAgent>>();

        var activeConfigs = await configRepo.GetActiveConfigAsync("");

        if (activeConfigs == null)
        {
            logger.LogWarning("No active configuration found. Run with --config to set up search sources.");
            return;
        }

        logger.LogInformation("Starting search cycle for user {Email}", activeConfigs.UserEmail);

        try
        {
            var result = await orchestrator.ExecuteSearchAsync(activeConfigs.Id, cancellationToken);
            logger.LogInformation(
                "Search cycle complete: {JobsFound} jobs found, {JobsSkipped} duplicates skipped in {Duration:F1}s",
                result.JobsFound, result.JobsSkipped, result.DurationSeconds ?? 0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search cycle failed");
        }
    }
}
