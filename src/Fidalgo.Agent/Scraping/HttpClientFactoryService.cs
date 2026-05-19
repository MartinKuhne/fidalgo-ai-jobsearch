using System.Net;
using System.Net.Http.Headers;

namespace Fidalgo.Agent.Scraping;

/// <summary>
/// Factory service for creating HttpClient instances with rate limiting.
/// </summary>
public class HttpClientFactoryService
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Creates a new instance of the HttpClientFactoryService.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    public HttpClientFactoryService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Creates a new HttpClient with rate limiting and default policies.
    /// </summary>
    /// <returns>A configured HttpClient instance.</returns>
    public HttpClient CreateClient()
    {
        var handler = new RateLimitingHandler();
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestHeaders =
            {
                UserAgent = { new ProductInfoHeaderValue("FidalgoAgent", "1.0") }
            }
        };
        return client;
    }

    /// <summary>
    /// Creates an HttpClient for a specific website with a custom user agent.
    /// </summary>
    /// <param name="websiteName">The website name for the user agent.</param>
    /// <returns>A configured HttpClient instance.</returns>
    public HttpClient CreateClient(string websiteName)
    {
        var handler = new RateLimitingHandler();
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestHeaders =
            {
                UserAgent = { new ProductInfoHeaderValue("FidalgoAgent", "1.0") },
                Accept = { new MediaTypeWithQualityHeaderValue("text/html") }
            }
        };
        return client;
    }
}
