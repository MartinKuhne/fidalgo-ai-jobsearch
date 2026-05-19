using Fidalgo.Agent.Storage;

namespace Fidalgo.Agent.Tools;

public class SaveJobTool
{
    private readonly JobRepository _repository;

    public SaveJobTool(JobRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> SaveAsync(
        string email,
        string employer,
        string? employerJobId,
        DateTime? postedDate,
        decimal? salaryRangeLow,
        decimal? salaryRangeHigh,
        string description,
        string pros,
        string cons,
        string resumeHints,
        int score,
        string recommendation,
        string sourceWebsite,
        CancellationToken cancellationToken = default)
    {
        if (score < 0 || score > 100)
        {
            throw new ArgumentException("Score must be between 0 and 100", nameof(score));
        }

        if (string.IsNullOrEmpty(recommendation) || 
            (recommendation != "Apply" && recommendation != "Maybe" && recommendation != "Do not apply"))
        {
            throw new ArgumentException("Recommendation must be Apply, Maybe, or Do not apply", nameof(recommendation));
        }

        var job = new JobEntity
        {
            Email = email,
            Employer = employer,
            EmployerJobId = employerJobId,
            PostedDate = postedDate,
            SalaryRangeLow = salaryRangeLow,
            SalaryRangeHigh = salaryRangeHigh,
            Description = description,
            Pros = pros,
            Cons = cons,
            ResumeHints = resumeHints,
            Score = score,
            Recommendation = recommendation,
            IsDeleted = false,
            SourceWebsite = sourceWebsite
        };

        return await _repository.SaveAsync(job, cancellationToken);
    }
}
