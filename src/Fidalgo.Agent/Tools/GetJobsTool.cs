using Fidalgo.Shared.Storage;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Agent.Tools;

public class GetJobsTool
{
    private readonly JobRepository _repository;
    private readonly ILogger<GetJobsTool> _logger;

    public GetJobsTool(JobRepository repository, ILogger<GetJobsTool> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<List<JobEntity>> GetAsync(
        string email,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        string? employer = null,
        string? employerJobId = null,
        string? sourceWebsite = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Querying jobs for {Email} with filters: dateFrom={DateFrom}, dateTo={DateTo}, employer={Employer}, sourceWebsite={SourceWebsite}", 
            email, dateFrom, dateTo, employer, sourceWebsite);

        var jobs = await _repository.QueryAsync(
            email: email,
            dateFrom: dateFrom,
            dateTo: dateTo,
            employer: employer,
            employerJobId: employerJobId,
            sourceWebsite: sourceWebsite,
            excludeDeleted: true,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Found {Count} jobs for {Email}", jobs.Count, email);

        return jobs;
    }
}
