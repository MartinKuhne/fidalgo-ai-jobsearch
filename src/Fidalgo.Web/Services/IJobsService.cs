using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;

namespace Fidalgo.Web.Services;

public interface IJobsService
{
    Task<PaginatedResult<JobViewModel>> GetJobsAsync(
        string email,
        DateTime? dateFrom = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteJobAsync(Guid internalId, CancellationToken cancellationToken = default);

    Task<JobEntity?> GetJobByIdAsync(Guid internalId, CancellationToken cancellationToken = default);
}
