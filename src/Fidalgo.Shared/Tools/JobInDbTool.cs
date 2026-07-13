using Fidalgo.Shared.Storage;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Shared.Tools;

/// <summary>
/// Agent tool to check if a job already exists in the local database.
/// Wraps JobRepository.ExistsAsync for use by the AI agent's tool calling system.
/// </summary>
public class JobInDbTool
{
    private readonly JobRepository _repository;
    private readonly ILogger<JobInDbTool> _logger;

    /// <summary>Initializes a new instance of the JobInDbTool.</summary>
    /// <param name="repository">The job repository for data access.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public JobInDbTool(JobRepository repository, ILogger<JobInDbTool> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>Checks if a job with the given employer ID exists for the tenant.</summary>
    public async Task<bool> ContainsAsync(
        string email,
        string siteUrl,
        string jobId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking if job {JobId} from {SiteUrl} exists for {Email}", jobId, siteUrl, email);
        return await _repository.ExistsAsync(email, siteUrl, jobId, cancellationToken);
    }
}