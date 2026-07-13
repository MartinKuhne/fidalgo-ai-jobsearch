namespace Fidalgo.Shared.Models;

/// <summary>
/// Represents a single fetch operation request.
/// </summary>
/// <param name="Url">The URL to fetch (required).</param>
/// <param name="WaitForSelector">CSS selector to wait for before capturing content (optional).</param>
/// <param name="TimeoutMilliseconds">Override for default timeout in milliseconds (optional).</param>
/// <param name="BrowserConfiguration">Custom browser settings for this request (optional).</param>
public record FetchRequest(
    string Url,
    string? WaitForSelector = null,
    int? TimeoutMilliseconds = null,
    BrowserConfiguration? BrowserConfiguration = null);