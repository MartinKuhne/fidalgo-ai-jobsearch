using Fidalgo.Shared.Storage;
using Microsoft.Extensions.Logging;
using Fidalgo.Shared.Tools;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Fidalgo.Ingest.Tools;

/// <summary>
/// Agent tool to save a job entity with validation for score and recommendation values.
/// Validates score is between 0-100 and recommendation is one of Apply/Maybe/Do not apply.
/// </summary>
public class SaveJobTool
{
    private readonly JobRepository _repository;
    private readonly MarkdownFetchTool _markdownFetchTool;
    private readonly ILogger<SaveJobTool> _logger;

    /// <summary>Initializes a new instance of the SaveJobTool.</summary>
    /// <param name="repository">The job repository for data access.</param>
    /// <param name="markdownFetchTool">The tool to fetch full markdown.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public SaveJobTool(JobRepository repository, MarkdownFetchTool markdownFetchTool, ILogger<SaveJobTool> logger)
    {
        _repository = repository;
        _markdownFetchTool = markdownFetchTool;
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
        string jobUrl,
        string sourceWebsite,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving job for {Email}: {Employer} at {JobUrl}", email, employer, jobUrl);

        if (string.IsNullOrEmpty(description) && !string.IsNullOrEmpty(jobUrl))
        {
            description = await _markdownFetchTool.FetchAsync(jobUrl, cancellationToken);
        }

        description ??= string.Empty;

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
            Pros = string.Empty,
            Cons = string.Empty,
            ResumeHints = string.Empty,
            Score = 0,
            Recommendation = "Maybe",
            IsDeleted = false,
            SourceWebsite = sourceWebsite
        };

        var jobId = await _repository.SaveAsync(job, cancellationToken);
        
        _logger.LogInformation("Job saved successfully with ID: {JobId}", jobId);

        return jobId;
    }
}