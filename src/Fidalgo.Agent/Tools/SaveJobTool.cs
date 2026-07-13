using Fidalgo.Shared.Storage;
using Microsoft.Extensions.Logging;

using Fidalgo.Shared.Tools;
namespace Fidalgo.Agent.Tools;

/// <summary>
/// Agent tool to save a job entity with validation for score and recommendation values.
/// Validates score is between 0-100 and recommendation is one of Apply/Maybe/Do not apply.
/// </summary>
public class SaveJobTool
{
    private readonly JobRepository _repository;
    private readonly ILogger<SaveJobTool> _logger;

    /// <summary>Initializes a new instance of the SaveJobTool.</summary>
    /// <param name="repository">The job repository for data access.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public SaveJobTool(JobRepository repository, ILogger<SaveJobTool> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>Saves a job entity with validation for score and recommendation values.</summary>
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