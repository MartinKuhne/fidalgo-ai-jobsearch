using Fidalgo.Shared.Storage;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Agent.Tools;

public class SaveJobTool
{
    private readonly JobRepository _repository;
    private readonly ILogger<SaveJobTool> _logger;

    public SaveJobTool(JobRepository repository, ILogger<SaveJobTool> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Guid> SaveAsync(
        string email,
        string employer,
        string? title,
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
        _logger.LogInformation("Saving job for {Email}: {Employer} (Score: {Score}, Recommendation: {Recommendation})", 
            email, employer, score, recommendation);
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
            Title = title,
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

        var jobId = await _repository.SaveAsync(job, cancellationToken);
        
        _logger.LogInformation("Job saved successfully with ID: {JobId}", jobId);

        return jobId;
    }
}
