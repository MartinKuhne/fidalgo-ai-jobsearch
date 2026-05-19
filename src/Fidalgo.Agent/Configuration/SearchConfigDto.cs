using System.Text.Json.Serialization;

namespace Fidalgo.Agent.Configuration;

/// <summary>
/// Data transfer object for deserializing JSON configuration.
/// </summary>
public class SearchConfigDto
{
    /// <summary>
    /// The user's email address.
    /// </summary>
    [JsonPropertyName("userEmail")]
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// List of websites to monitor.
    /// </summary>
    [JsonPropertyName("websites")]
    public List<string> Websites { get; set; } = new();

    /// <summary>
    /// List of search keywords.
    /// </summary>
    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = new();
}
