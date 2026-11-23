using System.ComponentModel.DataAnnotations;

namespace Fidalgo.Shared.Configuration;

/// <summary>
/// Configuration options for the OpenAI-compatible LLM endpoint.
/// Bound from the <c>LLM</c> configuration section.
/// </summary>
public class LlmConfiguration
{
    /// <summary>The configuration section name (<c>LLM</c>).</summary>
    public const string ConfigurationSectionName = "LLM";

    /// <summary>Base URI of the OpenAI-compatible API endpoint.</summary>
    [Required]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Model identifier to request from the LLM endpoint.</summary>
    [Required]
    public string Model { get; set; } = string.Empty;

    /// <summary>API key credential sent to the LLM endpoint.</summary>
    public string ApiKey { get; set; } = "u-mkuhne";
}
