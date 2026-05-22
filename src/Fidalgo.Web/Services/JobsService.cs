using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Web.Services;

public class JobsService : IJobsService
{
    private readonly JobRepository _repository;
    private readonly ILogger<JobsService> _logger;

    public JobsService(JobRepository repository, ILogger<JobsService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<PaginatedResult<JobViewModel>> GetJobsAsync(
        string email,
        DateTime? dateFrom = null,
        int page = 1,
        int pageSize = 20,
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

            var totalItems = allJobs.Count;
            _logger.LogInformation("Found {TotalItems} non-deleted jobs for email={Email}", totalItems, email);

            var pagedJobs = allJobs
                .OrderByDescending(j => j.Score)
                .ThenByDescending(j => j.PostedDate ?? DateTime.MinValue)
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
                    j.SourceWebsite))
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
            _logger.LogInformation("Fetching job by internalId={InternalId}", internalId);
            var result = await _repository.GetByIdAsync(internalId, cancellationToken);
            if (result == null)
            {
                _logger.LogWarning("Job not found for internalId={InternalId}", internalId);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching job by internalId={InternalId}", internalId);
            throw new InvalidOperationException("Failed to load job details. Please try again later.", ex);
        }
    }
}
