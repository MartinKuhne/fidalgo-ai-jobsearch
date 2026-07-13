namespace Fidalgo.Shared.Storage;

/// <summary>
/// EF Core entity mapping the Jobs database table to a .NET class.
/// Represents a single job posting with all searchable and displayable fields.
/// Mutable properties required by EF Core for change tracking.
/// </summary>
public class JobEntity
{
    /// <summary>Unique database identifier for this job record.</summary>
    public Guid InternalId { get; set; } = Guid.NewGuid();

    /// <summary>Email address of the tenant who saved this job.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Name of the employer or company.</summary>
    public string Employer { get; set; } = string.Empty;

    /// <summary>Job title (nullable for jobs without a formal title).</summary>
    public string? Title { get; set; }

    /// <summary>Date the job was originally posted.</summary>
    public DateTime? PostedDate { get; set; }

    /// <summary>Job identifier from the employer's source system.</summary>
    public string? EmployerJobId { get; set; }

    /// <summary>Lower bound of the salary range.</summary>
    public decimal? SalaryRangeLow { get; set; }

    /// <summary>Upper bound of the salary range.</summary>
    public decimal? SalaryRangeHigh { get; set; }

    /// <summary>Full job description text.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Pros or advantages of this job position.</summary>
    public string Pros { get; set; } = string.Empty;

    /// <summary>Cons or disadvantages of this job position.</summary>
    public string Cons { get; set; } = string.Empty;

    /// <summary>Resume tailoring hints specific to this job.</summary>
    public string ResumeHints { get; set; } = string.Empty;

    /// <summary>AI-assigned relevance score from 0 to 100.</summary>
    public int Score { get; set; }

    /// <summary>AI-generated recommendation: Apply, Maybe, or Do not apply.</summary>
    public string Recommendation { get; set; } = string.Empty;

    /// <summary>Soft-delete flag. True when the job has been discarded.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Date when a notification was sent about this job.</summary>
    public DateTime? DateNotified { get; set; }

    /// <summary>Source website where the job was found (e.g., indeed.com).</summary>
    public string SourceWebsite { get; set; } = string.Empty;
}