using Fidalgo.Agent.Storage;

namespace Fidalgo.Agent.Tools;

public class GetJobsTool
{
    private readonly JobRepository _repository;

    public GetJobsTool(JobRepository repository)
    {
        _repository = repository;
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
        return await _repository.QueryAsync(
            email: email,
            dateFrom: dateFrom,
            dateTo: dateTo,
            employer: employer,
            employerJobId: employerJobId,
            sourceWebsite: sourceWebsite,
            excludeDeleted: true,
            cancellationToken: cancellationToken);
    }
}
