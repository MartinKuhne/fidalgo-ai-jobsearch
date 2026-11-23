using System.ComponentModel.DataAnnotations;

namespace Fidalgo.Shared.Configuration;

/// <summary>
/// Configuration options controlling profile resolution, packet output directory,
/// batch generation defaults, and PDF/LLM settings.
/// Bound from the <c>ApplicationPacket</c> configuration section.
/// </summary>
public class ApplicationPacketOptions
{
    /// <summary>The configuration section name (<c>ApplicationPacket</c>).</summary>
    public const string ConfigurationSectionName = "ApplicationPacket";

    /// <summary>Optional explicit directory for user profile documents; isolates resolution when set.</summary>
    public string? ProfileDirectory { get; set; }

    /// <summary>Optional explicit base directory for generated application packets.</summary>
    public string? OutputDirectory { get; set; }

    /// <summary>When true, unattended agent runs automatically generate packets for matching jobs after evaluation.</summary>
    public bool AutoGenerateForApply { get; set; }

    /// <summary>Comma-separated recommendation tiers targeted during batch packet generation.</summary>
    public string RecommendationFilter { get; set; } = "Apply";

    /// <summary>Default paper format for rendered PDFs (e.g. <c>Letter</c>, <c>A4</c>).</summary>
    public string ResumePageSize { get; set; } = "Letter";

    /// <summary>Timeout in seconds for each LLM completion request.</summary>
    [Range(1, 3600)]
    public int LlmTimeoutSeconds { get; set; } = 120;

    /// <summary>Endpoint URL for the <c>md-to-pdf</c> HTTP rendering service.</summary>
    public string MdToPdfEndpoint { get; set; } = "http://localhost:8000";

    /// <summary>Rendering engine name passed to <c>md-to-pdf</c> (e.g. <c>weasyprint</c>).</summary>
    public string MdToPdfEngine { get; set; } = "weasyprint";

    /// <summary>Timeout in seconds for each PDF rendering HTTP request.</summary>
    [Range(1, 600)]
    public int PdfTimeoutSeconds { get; set; } = 60;
}
