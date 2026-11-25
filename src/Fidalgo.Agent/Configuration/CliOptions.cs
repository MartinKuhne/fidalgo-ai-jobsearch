using System.ComponentModel.DataAnnotations;

namespace Fidalgo.Agent.Configuration;

/// <summary>
/// Command-line and configuration options for <c>Fidalgo.Agent</c>.
/// </summary>
public record CliOptions
{
    /// <summary>Tenant email address used to scope saved jobs.</summary>
    [Required]
    public string Email { get; set; } = string.Empty;

    /// <summary>Optional path override to the candidate's base resume Markdown file.</summary>
    public string? Resume { get; set; }

    /// <summary>Optional path override to the candidate's career story Markdown file.</summary>
    public string? CareerStory { get; set; }

    /// <summary>Optional path override to the candidate's career goals Markdown file.</summary>
    public string? CareerGoals { get; set; }

    /// <summary>When true, lists saved jobs matching the query filters and exits.</summary>
    public bool QueryJobs { get; set; }

    /// <summary>Optional employer substring filter for query mode.</summary>
    public string? EmployerFilter { get; set; }

    /// <summary>Optional lower posted-date bound for query mode.</summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>Optional upper posted-date bound for query mode.</summary>
    public DateTime? DateTo { get; set; }

    /// <summary>Optional source website filter for query mode.</summary>
    public string? SourceWebsiteFilter { get; set; }

    /// <summary>When set, soft-deletes (discards) the specified job ID and exits.</summary>
    public Guid? DiscardJobId { get; set; }

    /// <summary>When true, lists all discarded jobs and exits.</summary>
    public bool ListDiscarded { get; set; }

    /// <summary>When true, generates application packets for matching evaluated jobs.</summary>
    public bool GeneratePackets { get; set; }

    /// <summary>Optional single job ID to generate an application packet for.</summary>
    public Guid? TargetJobId { get; set; }

    /// <summary>When true, forces regeneration even when a complete packet already exists on disk.</summary>
    public bool ForceRegenerate { get; set; }

    /// <summary>
    /// Optional override controlling whether existing complete packets are skipped.
    /// Nullable so omitted CLI flags do not shadow configuration defaults.
    /// </summary>
    public bool? SkipExisting { get; set; }

    /// <summary>Optional base directory override for generated application packets.</summary>
    public string? OutputDir { get; set; }

    /// <summary>
    /// Optional comma-separated recommendation tier filter (e.g. <c>Apply,Maybe</c>).
    /// Nullable so omitted CLI flags fall back to <c>ApplicationPacket:RecommendationFilter</c>.
    /// </summary>
    public string? RecommendationFilter { get; set; }

    /// <summary>
    /// Optional minimum match score (0-100) required when generating application packets.
    /// </summary>
    public int? MinScore { get; set; }
}
