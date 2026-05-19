using Fidalgo.Agent.Models;

namespace Fidalgo.Agent.Tools;

/// <summary>
/// Interface for browser fetch operations using Playwright browser automation.
/// </summary>
public interface IBrowserFetchTool
{
    /// <summary>
    /// Fetches the HTML content of a web page using Playwright browser automation.
    /// </summary>
    /// <param name="request">The fetch request containing URL and optional wait conditions.</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>FetchResult with HTML content or error information.</returns>
    /// <exception cref="InvalidOperationException">Thrown when fetch fails after all retries.</exception>
    Task<FetchResult> FetchAsync(FetchRequest request, CancellationToken cancellationToken = default);
}
