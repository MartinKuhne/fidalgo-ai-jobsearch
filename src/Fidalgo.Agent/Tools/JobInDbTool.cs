using Fidalgo.Agent.Storage;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Agent.Tools;

public class JobInDbTool
{
    private readonly JobRepository _repository;
    private readonly ILogger<JobInDbTool> _logger;

    public JobInDbTool(JobRepository repository, ILogger<JobInDbTool> logger)
    {
        _repository = repository;
        _logger = logger;
    }

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
