namespace Fidalgo.Agent.Configuration;

/// <summary>
/// Defines which websites to monitor and what keywords to search for.
/// </summary>
public class SearchConfiguration
{
    /// <summary>
    /// Unique identifier for this configuration.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Email address identifying the user.
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of website names to monitor.
    /// </summary>
    public string Websites { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of search keywords.
    /// </summary>
    public string Keywords { get; set; } = string.Empty;

    /// <summary>
    /// Whether this configuration is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When this configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this configuration was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
