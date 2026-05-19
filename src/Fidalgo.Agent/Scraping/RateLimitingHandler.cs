namespace Fidalgo.Agent.Scraping;

/// <summary>
/// DelegatingHandler that enforces rate limiting per website.
/// </summary>
public class RateLimitingHandler : DelegatingHandler
{
    private readonly Dictionary<string, DateTime> _lastRequestTime = new();
    private readonly TimeSpan _minimumInterval;
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new instance with a default 3-second interval.
    /// </summary>
    public RateLimitingHandler()
        : this(TimeSpan.FromSeconds(3))
    {
    }

    /// <summary>
    /// Creates a new instance with the specified minimum interval between requests.
    /// </summary>
    /// <param name="minimumInterval">Minimum time between requests to the same website.</param>
    public RateLimitingHandler(TimeSpan minimumInterval)
    {
        _minimumInterval = minimumInterval;
    }

    /// <summary>
    /// Sends an HTTP request after enforcing rate limits.
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var host = request.RequestUri?.Host ?? string.Empty;

        lock (_lock)
        {
            if (_lastRequestTime.TryGetValue(host, out var lastTime))
            {
                var elapsed = DateTime.UtcNow - lastTime;
                if (elapsed < _minimumInterval)
                {
                    var waitTime = _minimumInterval - elapsed;
                    lock (_lock)
                    {
                        Thread.Sleep(waitTime);
                    }
                }
            }
            _lastRequestTime[host] = DateTime.UtcNow;
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
