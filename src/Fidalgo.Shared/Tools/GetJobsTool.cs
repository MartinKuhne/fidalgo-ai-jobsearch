using Fidalgo.Shared.Storage;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Shared.Tools;

/// <summary>
/// Agent tool wrapper around JobRepository.QueryAsync for querying saved jobs.
/// Delegates filtering to the repository and provides logging for tool execution.
/// </summary>
public class GetJobsTool
{
    private readonly JobRepository _repository;
    private readonly ILogger<GetJobsTool> _logger;

    /// <summary>Initializes a new instance of the GetJobsTool.</summary>
    /// <param name="repository">The job repository for data access.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public GetJobsTool(JobRepository repository, ILogger<GetJobsTool> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>Queries saved jobs by email with optional filters.</summary>
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