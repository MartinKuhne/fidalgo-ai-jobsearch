using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;

namespace Fidalgo.Host.Services;

/// <summary>
/// Contract for paginated job retrieval, soft deletion, and single job lookup.
/// Implemented by JobsService to provide the Blazor UI with job data.
/// </summary>
public interface IJobsService
{
    /// <summary>Retrieves a page of non-deleted jobs for a tenant, sorted by score then date.</summary>
    /// <param name="email">Tenant email to filter by.</param>
    /// <param name="dateFrom">Optional start date filter.</param>
    /// <param name="page">Page number (1-based, default: 1).</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100).</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>Paginated result containing the requested page of jobs.</returns>
    Task<PaginatedResult<JobViewModel>> GetJobsAsync(
        string email,
        DateTime? dateFrom = null,
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? sortBy = null,
        string? sortDir = null,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a job by marking it as discarded.</summary>
    /// <param name="internalId">The job's internal database ID.</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>True if the job was found and deleted.</returns>
    Task<bool> SoftDeleteJobAsync(Guid internalId, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a single job entity by its internal database ID.</summary>
    /// <param name="internalId">The job's internal ID.</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>The job entity, or null if not found.</returns>
    Task<JobEntity?> GetJobByIdAsync(Guid internalId, CancellationToken cancellationToken = default);
}
