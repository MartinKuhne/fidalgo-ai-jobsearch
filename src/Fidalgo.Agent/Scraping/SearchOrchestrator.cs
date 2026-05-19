using System.Text.Json;
using Fidalgo.Agent.Configuration;
using Fidalgo.Agent.Storage;
using Microsoft.EntityFrameworkCore;

namespace Fidalgo.Agent.Scraping;

/// <summary>
/// Orchestrates the search process across configured websites and keywords.
/// </summary>
public class SearchOrchestrator
{
    private readonly WebsiteScraperRegistry _registry;
    private readonly JobDbContext _dbContext;
    private readonly ILogger<SearchOrchestrator> _logger;
    private readonly HttpClientFactoryService _httpClientFactory;

    /// <summary>
    /// Creates a new instance of the SearchOrchestrator.
    /// </summary>
    /// <param name="registry">The scraper registry.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public SearchOrchestrator(WebsiteScraperRegistry registry, JobDbContext dbContext,
        ILogger<SearchOrchestrator> logger, HttpClientFactoryService httpClientFactory)
    {
        _registry = registry;
        _dbContext = dbContext;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Executes a full search cycle across all configured websites and keywords.
    /// </summary>
    /// <param name="configurationId">The configuration ID to associate results with.</param>
    /// <param name="cancellationToken">Token for cancelling the operation.</param>
    /// <returns>The scrape result record.</returns>
    public async Task<ScrapeResult> ExecuteSearchAsync(Guid configurationId, CancellationToken cancellationToken = default)
    {
        var config = await _dbContext.SearchConfigurations.FindAsync(configurationId);
        if (config == null)
        {
            throw new ArgumentException($"Configuration not found: {configurationId}");
        }

        var websites = JsonSerializer.Deserialize<List<string>>(config.Websites) ?? new();
        var keywords = JsonSerializer.Deserialize<List<string>>(config.Keywords) ?? new();

        var result = new ScrapeResult
        {
            Id = Guid.NewGuid(),
            ConfigurationId = configurationId,
            StartedAt = DateTime.UtcNow,
            JobsFound = 0,
            JobsSkipped = 0
        };

        var allJobs = new List<JobPosting>();

        foreach (var website in websites)
        {
            try
            {
                var scraper = _registry.Get(website);
                if (scraper == null)
                {
                    _logger.LogWarning("No scraper registered for website: {Website}", website);
                    continue;
                }

                foreach (var keyword in keywords)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    _logger.LogInformation("Searching {Website} for keyword: {Keyword}", website, keyword);

                    var scraperClient = _httpClientFactory.CreateClient(website);
                    var searchResults = await scraper.SearchAsync(keyword, cancellationToken);

                    foreach (var job in searchResults)
                    {
                        job.MatchedKeywords = JsonSerializer.Serialize(keywords);
                        job.ScrapedAt = DateTime.UtcNow;
                        allJobs.Add(job);
                    }

                    result.JobsFound += searchResults.Count;
                    _logger.LogInformation("Found {Count} jobs from {Website} for keyword: {Keyword}",
                        searchResults.Count, website, keyword);
                }
            }
            catch (ScraperException ex)
            {
                result.ErrorMessage = ex.Message;
                result.Status = "Partial";
                _logger.LogError(ex, "Scraper error for website {Website}", website);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Status = "Partial";
                _logger.LogError(ex, "Error searching website {Website}", website);
            }
        }

        // Deduplicate jobs by SourceUrl
        var existingUrls = await _dbContext.JobPostings
            .Where(j => allJobs.Select(x => x.SourceUrl).Contains(j.SourceUrl))
            .Select(j => j.SourceUrl)
            .ToListAsync(cancellationToken);

        var newJobs = allJobs.Where(j => !existingUrls.Contains(j.SourceUrl)).ToList();
        result.JobsSkipped = allJobs.Count - newJobs.Count;

        // Store new jobs
        if (newJobs.Any())
        {
            await _dbContext.JobPostings.AddRangeAsync(newJobs, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Stored {Count} new jobs in database", newJobs.Count);
        }

        result.CompletedAt = DateTime.UtcNow;
        result.DurationSeconds = (decimal)(result.CompletedAt.Value - result.StartedAt).TotalSeconds;
        result.Status = result.ErrorMessage != null ? "Partial" : "Success";

        _dbContext.ScrapeResults.Add(result);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }
}
