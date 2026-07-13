using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Host.Services;

/// <summary>
/// Service that queries jobs from the repository, applies pagination, and maps to view models.
/// Validates input parameters and provides structured error handling with logging.
/// Sorts results by relevance score descending, then by posted date descending.
/// </summary>
public class JobsService : IJobsService
{
    private readonly JobRepository _repository;
    private readonly ILogger<JobsService> _logger;

    /// <summary>Initializes a new instance of the JobsService.</summary>
    /// <param name="repository">The job repository for data access.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public JobsService(JobRepository repository, ILogger<JobsService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>Retrieves a page of non-deleted jobs for a tenant, sorted by score then date.</summary>
    public async Task<PaginatedResult<JobViewModel>> GetJobsAsync(
        string email,
        DateTime? dateFrom = null,
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? sortBy = null,
        string? sortDir = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching jobs for email={Email} page={Page} pageSize={PageSize}", email, page, pageSize);

            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning("GetJobsAsync called with empty email");
                throw new ArgumentException("Email cannot be null or empty.", nameof(email));
            }

            if (page < 1)
            {
                _logger.LogWarning("GetJobsAsync called with invalid page={Page}", page);
                throw new ArgumentOutOfRangeException(nameof(page), "Page must be >= 1.");
            }

            if (pageSize < 1 || pageSize > 100)
            {
                _logger.LogWarning("GetJobsAsync called with invalid pageSize={PageSize}", pageSize);
                throw new ArgumentOutOfRangeException(nameof(pageSize), "PageSize must be between 1 and 100.");
            }

            var allJobs = await _repository.QueryAsync(
                email,
                dateFrom: dateFrom,
                excludeDeleted: true,
                cancellationToken: cancellationToken);

            var filteredJobs = (IEnumerable<JobEntity>)allJobs;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lowerSearch = search.ToLowerInvariant();
                filteredJobs = filteredJobs.Where(j => 
                    (j.Title != null && j.Title.ToLowerInvariant().Contains(lowerSearch)) ||
                    (j.Employer != null && j.Employer.ToLowerInvariant().Contains(lowerSearch)));
            }

            var totalItems = filteredJobs.Count();
            _logger.LogInformation("Found {TotalItems} non-deleted jobs for email={Email}", totalItems, email);

            if (!string.IsNullOrEmpty(sortBy))
            {
                bool isDesc = sortDir?.ToLowerInvariant() != "asc";
                filteredJobs = sortBy.ToLowerInvariant() switch
                {
                    "title" => isDesc ? filteredJobs.OrderByDescending(j => j.Title) : filteredJobs.OrderBy(j => j.Title),
                    "company" => isDesc ? filteredJobs.OrderByDescending(j => j.Employer) : filteredJobs.OrderBy(j => j.Employer),
                    "date" => isDesc ? filteredJobs.OrderByDescending(j => j.PostedDate ?? DateTime.MinValue) : filteredJobs.OrderBy(j => j.PostedDate ?? DateTime.MinValue),
                    "score" => isDesc ? filteredJobs.OrderByDescending(j => j.Score) : filteredJobs.OrderBy(j => j.Score),
                    "recommendation" => isDesc ? filteredJobs.OrderByDescending(j => j.Recommendation) : filteredJobs.OrderBy(j => j.Recommendation),
                    _ => filteredJobs.OrderByDescending(j => j.Score).ThenByDescending(j => j.PostedDate ?? DateTime.MinValue)
                };
            }
            else
            {
                filteredJobs = filteredJobs
                    .OrderByDescending(j => j.Score)
                    .ThenByDescending(j => j.PostedDate ?? DateTime.MinValue);
            }

            var pagedJobs = filteredJobs
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(j => new JobViewModel(
                    j.InternalId,
                    j.Email,
                    j.Employer,
                    j.Title,
                    j.Score,
                    j.Recommendation,
                    j.PostedDate,
                    j.SourceWebsite,
                    FormatPay(j.SalaryRangeLow, j.SalaryRangeHigh),
                    j.Description,
                    GetUrl(j),
                    j.Pros + "\n" + j.Cons))
                .ToList();

            _logger.LogInformation("Returning {ReturnedItems} jobs for email={Email} page={Page}", pagedJobs.Count, email, page);

            return new PaginatedResult<JobViewModel>
            {
                Items = pagedJobs,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex) when (ex is not ArgumentException and not ArgumentOutOfRangeException)
        {
            _logger.LogError(ex, "Error fetching jobs for email={Email}", email);
            throw new InvalidOperationException("Failed to load jobs. Please try again later.", ex);
        }
    }

    public async Task<bool> SoftDeleteJobAsync(Guid internalId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (internalId == Guid.Empty)
            {
                _logger.LogWarning("SoftDeleteJobAsync called with empty internalId");
                throw new ArgumentException("Internal ID cannot be empty.", nameof(internalId));
            }

            _logger.LogInformation("Soft deleting job with internalId={InternalId}", internalId);
            var result = await _repository.SoftDeleteAsync(internalId, cancellationToken);
            _logger.LogInformation("Soft delete result for job {InternalId}: {Result}", internalId, result);
            return result;
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            _logger.LogError(ex, "Error soft deleting job with internalId={InternalId}", internalId);
            throw new InvalidOperationException("Failed to delete job. Please try again later.", ex);
        }
    }

    public async Task<JobEntity?> GetJobByIdAsync(Guid internalId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (internalId == Guid.Empty)
            {
                _logger.LogWarning("GetJobByIdAsync called with empty internalId");
                throw new ArgumentException("Internal ID cannot be empty.", nameof(internalId));
            }

            _logger.LogInformation("Fetching job by internalId={InternalId}", internalId);
            var result = await _repository.GetByIdAsync(internalId, cancellationToken);
            if (result == null)
            {
                _logger.LogWarning("Job not found for internalId={InternalId}", internalId);
            }
            return result;
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            _logger.LogError(ex, "Error fetching job by internalId={InternalId}", internalId);
            throw new InvalidOperationException("Failed to load job details. Please try again later.", ex);
        }
    }
    
    private static string FormatPay(decimal? low, decimal? high)
    {
        if (low.HasValue && high.HasValue && low.Value > 0 && high.Value > 0)
        {
            return $"${low.Value:N0} - ${high.Value:N0}";
        }
        if (low.HasValue && low.Value > 0)
        {
            return $"${low.Value:N0}";
        }
        return string.Empty;
    }

    private static string GetUrl(JobEntity j)
    {
        if (j.SourceWebsite == "adzuna" && !string.IsNullOrEmpty(j.EmployerJobId))
        {
            return $"https://www.adzuna.com/details/{j.EmployerJobId}";
        }
        return j.SourceWebsite.StartsWith("http") ? j.SourceWebsite : "";
    }
}
