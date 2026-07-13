using Microsoft.Extensions.Logging;

namespace Fidalgo.Shared.Tools;

public class MarkdownFetchTool
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MarkdownFetchTool> _logger;

    public MarkdownFetchTool(HttpClient httpClient, ILogger<MarkdownFetchTool> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>Fetches the content of a URL and converts it to markdown.</summary>
    public virtual async Task<string> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Markdown fetch: {Url}", url);
        var requestUrl = $"http://localhost:3333/api/fetch?url={Uri.EscapeDataString(url)}&browser=true";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Markdown fetch returned {StatusCode}. Body: {Body}", response.StatusCode, errorBody);
            }
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Markdown fetch failed for URL: {Url}", url);
            return $"Fetch failed: {ex.Message}";
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("markdown", out var mdElement))
            {
                return mdElement.GetString() ?? "";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON from markdown fetcher. Returning raw response.");
        }
        
        return json;
    }
}