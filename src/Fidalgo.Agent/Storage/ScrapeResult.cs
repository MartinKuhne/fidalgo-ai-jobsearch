namespace Fidalgo.Agent.Storage;

/// <summary>
/// Records the outcome of each scraping attempt.
/// </summary>
public class ScrapeResult
{
    /// <summary>
    /// Unique identifier for this record.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to the SearchConfiguration.
    /// </summary>
    public Guid ConfigurationId { get; set; }

    /// <summary>
    /// Name of the website scraped.
    /// </summary>
    public string Website { get; set; } = string.Empty;

    /// <summary>
    /// Status: Success, Partial, or Failed.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Number of new jobs discovered.
    /// </summary>
    public int JobsFound { get; set; }

    /// <summary>
    /// Number of duplicates skipped.
    /// </summary>
    public int JobsSkipped { get; set; }

    /// <summary>
    /// Error details if status is Failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When scraping started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When scraping finished.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Elapsed time in seconds.
    /// </summary>
    public decimal? DurationSeconds { get; set; }
}
