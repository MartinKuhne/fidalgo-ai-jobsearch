namespace Fidalgo.Agent.Models;

/// <summary>
/// Represents the result of a fetch operation.
/// </summary>
/// <param name="Url">The URL that was fetched.</param>
/// <param name="Content">The HTML content of the page.</param>
/// <param name="ContentLoadedAt">When the content was successfully retrieved.</param>
/// <param name="TotalDurationMilliseconds">Total time taken for the fetch operation.</param>
/// <param name="HasWaited">Whether the tool waited for a selector before capturing.</param>
/// <param name="WaitDurationMilliseconds">Time spent waiting for selector (null if no wait).</param>
/// <param name="Error">Error message if the operation failed (optional).</param>
public record FetchResult(
    string Url,
    string Content,
    DateTime ContentLoadedAt,
    int TotalDurationMilliseconds,
    bool HasWaited,
    int? WaitDurationMilliseconds,
    string? Error = null);
