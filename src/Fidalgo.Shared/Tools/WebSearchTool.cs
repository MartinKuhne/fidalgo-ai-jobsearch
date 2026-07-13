using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Shared.Tools;

public class WebSearchTool
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebSearchTool> _logger;

    public WebSearchTool(HttpClient httpClient, ILogger<WebSearchTool> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Web search: {Query}", query);
        var url = $"http://localhost:8090/search?q={Uri.EscapeDataString(query)}&format=json";
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Web search failed for query: {Query}", query);
            return $"Search failed: {ex.Message}";
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("results", out var results))
            return "No results found.";

        var output = new StringBuilder();
        foreach (var result in results.EnumerateArray())
        {
            var title = result.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            var resultUrl = result.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
            var snippet = result.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
            output.AppendLine($"## {title}");
            output.AppendLine($"URL: {resultUrl}");
            output.AppendLine(snippet);
            output.AppendLine();
        }

        if (output.Length == 0)
            return "No results found.";

        return output.ToString();
    }
}