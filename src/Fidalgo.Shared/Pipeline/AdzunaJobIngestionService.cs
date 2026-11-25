using System.Globalization;
using Fidalgo.Shared.Storage;
using Fidalgo.Shared.Tools;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Shared.Pipeline;

/// <summary>
/// Ingests job postings from the Adzuna API, enriches descriptions via <see cref="MarkdownFetchTool"/>,
/// and persists deduplicated untriaged jobs via <see cref="JobRepository"/>.
/// </summary>
public class AdzunaJobIngestionService : IJobIngestionService
{
    private const int DefaultPageSize = 20;

    private readonly JobSearchTool _jobSearchTool;
    private readonly JobInDbTool _jobInDbTool;
    private readonly JobRepository _repository;
    private readonly MarkdownFetchTool _markdownFetchTool;
    private readonly ILogger<AdzunaJobIngestionService> _logger;

    /// <summary>Initializes a new instance of <see cref="AdzunaJobIngestionService"/>.</summary>
    public AdzunaJobIngestionService(
        JobSearchTool jobSearchTool,
        JobInDbTool jobInDbTool,
        JobRepository repository,
        MarkdownFetchTool markdownFetchTool,
        ILogger<AdzunaJobIngestionService> logger)
    {
        ArgumentNullException.ThrowIfNull(jobSearchTool);
        ArgumentNullException.ThrowIfNull(jobInDbTool);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(markdownFetchTool);
        ArgumentNullException.ThrowIfNull(logger);

        _jobSearchTool = jobSearchTool;
        _jobInDbTool = jobInDbTool;
        _repository = repository;
        _markdownFetchTool = markdownFetchTool;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> IngestAsync(
        string email,
        string keywords,
        string zipCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(keywords);
        ArgumentException.ThrowIfNullOrWhiteSpace(zipCode);

        var savedCount = 0;
        var page = 1;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (page > 1)
            {
                await Task.Delay(1000, cancellationToken);
            }

            var jobs = await _jobSearchTool.SearchRawAsync(
                keywords,
                zipCode,
                page: page,
                cancellationToken: cancellationToken);

            if (jobs is null || jobs.Results.Count == 0)
            {
                break;
            }

            _logger.LogInformation(
                "API search found {ResultCount} jobs on page {Page} (Total available: {Count}).",
                jobs.Results.Count,
                page,
                jobs.Count);

            foreach (var job in jobs.Results)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var employerJobId = job.Id ?? string.Empty;
                var exists = await _jobInDbTool.ContainsAsync(email, "adzuna", employerJobId, cancellationToken);
                if (exists)
                {
                    _logger.LogInformation("Skipping job {JobId} because it already exists in the database.", job.Id);
                    continue;
                }

                if (string.IsNullOrEmpty(job.RedirectUrl))
                {
                    continue;
                }

                _logger.LogInformation("Saving job {JobId} from {Url}", job.Id, job.RedirectUrl);

                DateTime? postedDate = DateTime.TryParse(
                    job.Created,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsedDate)
                    ? parsedDate
                    : null;

                var description = await _markdownFetchTool.FetchAsync(job.RedirectUrl, cancellationToken);
                var entity = new JobEntity
                {
                    Email = email,
                    Employer = job.Company?.DisplayName ?? "Unknown",
                    Title = job.Title,
                    EmployerJobId = job.Id,
                    PostedDate = postedDate,
                    SalaryRangeLow = job.SalaryMin,
                    SalaryRangeHigh = job.SalaryMax,
                    Description = description ?? string.Empty,
                    Pros = string.Empty,
                    Cons = string.Empty,
                    ResumeHints = string.Empty,
                    Score = 0,
                    Recommendation = "Maybe",
                    IsDeleted = false,
                    SourceWebsite = "adzuna"
                };

                await _repository.SaveAsync(entity, cancellationToken);
                savedCount++;

                await Task.Delay(1000, cancellationToken);
            }

            if (jobs.Results.Count < DefaultPageSize)
            {
                break;
            }

            page++;
        }

        return savedCount;
    }
}
