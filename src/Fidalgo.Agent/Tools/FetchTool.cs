using System.Net.Http.Headers;
using Fidalgo.Agent.Sanitization;

namespace Fidalgo.Agent.Tools;

public class FetchTool
{
    private readonly HttpClient _httpClient;
    private readonly HtmlSanitizer _sanitizer;

    public FetchTool(HttpClient httpClient, HtmlSanitizer sanitizer)
    {
        _httpClient = httpClient;
        _sanitizer = sanitizer;
    }

    public async Task<string> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return _sanitizer.Sanitize(content);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to fetch {url}: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InvalidOperationException($"Request to {url} timed out", ex);
        }
    }
}
