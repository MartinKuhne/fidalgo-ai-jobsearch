using System.ComponentModel.DataAnnotations;

namespace Fidalgo.Shared.Configuration;

/// <summary>
/// Configuration options for the Adzuna job search API.
/// Loaded from user secrets or environment variables.
/// </summary>
public class AdzunaConfiguration
{
    /// <summary>
    /// The Adzuna application ID.
    /// Set via user secrets key "Adzuna:AppId" or environment variable "Adzuna__AppId".
    /// </summary>
    [Required]
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// The Adzuna application key.
    /// Set via user secrets key "Adzuna:AppKey" or environment variable "Adzuna__AppKey".
    /// </summary>
    [Required]
    public string AppKey { get; set; } = string.Empty;
}