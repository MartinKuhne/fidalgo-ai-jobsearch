namespace Fidalgo.Agent.Scraping;

/// <summary>
/// Represents a job posting discovered from a website.
/// </summary>
public record JobPosting
{
    /// <summary>
    /// The canonical URL of the job posting.
    /// </summary>
    public string SourceUrl { get; init; } = string.Empty;

    /// <summary>
    /// The job title or position name.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The employer or company name.
    /// </summary>
    public string Company { get; init; } = string.Empty;

    /// <summary>
    /// The full job description text.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// When the job was posted, if available.
    /// </summary>
    public DateTime? PostedDate { get; init; }

    /// <summary>
    /// The minimum salary range value.
    /// </summary>
    public decimal? SalaryLow { get; init; }

    /// <summary>
    /// The maximum salary range value.
    /// </summary>
    public decimal? SalaryHigh { get; init; }

    /// <summary>
    /// The currency code for salary values. Defaults to "USD".
    /// </summary>
    public string Currency { get; init; } = "USD";

    /// <summary>
    /// The name of the website where the job was found.
    /// </summary>
    public string SourceWebsite { get; init; } = string.Empty;

    /// <summary>
    /// Unique identifier for database storage.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// JSON array of search keywords that matched this job.
    /// </summary>
    public string MatchedKeywords { get; set; } = string.Empty;

    /// <summary>
    /// When this job was scraped and stored.
    /// </summary>
    public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the user has discarded this job.
    /// </summary>
    public bool IsDiscarded { get; init; }

    /// <summary>
    /// Date the user marked as applied, if applicable.
    /// </summary>
    public DateTime? AppliedDate { get; init; }
}
