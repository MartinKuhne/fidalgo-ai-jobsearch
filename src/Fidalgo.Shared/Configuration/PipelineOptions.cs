using System.ComponentModel.DataAnnotations;

namespace Fidalgo.Shared.Configuration;

/// <summary>
/// Options controlling the continuous background pipeline that periodically ingests jobs,
/// evaluates untriaged postings, and generates application packets for close fits.
/// Bound from the <c>Pipeline</c> configuration section.
/// </summary>
public class PipelineOptions
{
    /// <summary>The configuration section name (<c>Pipeline</c>).</summary>
    public const string ConfigurationSectionName = "Pipeline";

    /// <summary>When true, <c>Fidalgo.Host</c> runs the periodic background pipeline automatically.</summary>
    public bool Enabled { get; set; }

    /// <summary>Interval in hours between pipeline runs (default: 4 hours).</summary>
    [Range(0.01, 168.0)]
    public double IntervalHours { get; set; } = 4.0;

    /// <summary>When true, executes an initial pipeline cycle immediately on host startup.</summary>
    public bool RunOnStartup { get; set; } = true;

    /// <summary>Minimum match score (0-100) required to treat a job as a close fit for packet generation.</summary>
    [Range(0, 100)]
    public int MinScore { get; set; } = 70;

    /// <summary>Comma-separated recommendation tiers eligible for close-fit packet generation.</summary>
    public string RecommendationFilter { get; set; } = "Apply";

    /// <summary>When true, generates application packets for close fits during each pipeline cycle.</summary>
    public bool GeneratePackets { get; set; } = true;
}
