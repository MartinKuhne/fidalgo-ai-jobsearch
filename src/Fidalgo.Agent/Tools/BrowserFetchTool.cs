using Fidalgo.Agent.Models;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Agent.Tools;

/// <summary>
/// Tool for fetching web page content using Playwright browser automation.
/// </summary>
public class BrowserFetchTool : IBrowserFetchTool
{
    private readonly ILogger<BrowserFetchTool> _logger;

    public BrowserFetchTool(ILogger<BrowserFetchTool> logger)
    {
        _logger = logger;
    }
    /// <inheritdoc/>
    public async Task<FetchResult> FetchAsync(FetchRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching URL: {Url}", request.Url);

        var startTime = DateTime.UtcNow;
        var hasWaited = false;
        var waitDurationMs = 0;

        try
        {
            using var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = request.BrowserConfiguration?.Headless ?? true,
            });

            await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize
                {
                    Width = request.BrowserConfiguration?.ViewportWidth ?? 1920,
                    Height = request.BrowserConfiguration?.ViewportHeight ?? 1080,
                },
                UserAgent = request.BrowserConfiguration?.UserAgent,
            });

            var page = await context.NewPageAsync();

            try
            {
                var timeout = request.TimeoutMilliseconds ?? request.BrowserConfiguration?.TimeoutMilliseconds ?? 30000;

                await page.GotoAsync(request.Url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = timeout,
                });

                if (!string.IsNullOrEmpty(request.WaitForSelector))
                {
                    var waitStartTime = DateTime.UtcNow;
                    hasWaited = true;

                    await page.WaitForSelectorAsync(request.WaitForSelector, new PageWaitForSelectorOptions
                    {
                        Timeout = timeout,
                    });

                    waitDurationMs = (int)(DateTime.UtcNow - waitStartTime).TotalMilliseconds;
                }

                var content = await page.ContentAsync();

            var totalDuration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger.LogInformation("Successfully fetched URL: {Url} in {Duration}ms", request.Url, totalDuration);

            return new FetchResult(
                Url: request.Url,
                Content: content,
                ContentLoadedAt: DateTime.UtcNow,
                TotalDurationMilliseconds: totalDuration,
                HasWaited: hasWaited,
                WaitDurationMilliseconds: hasWaited ? waitDurationMs : null,
                Error: null);
            }
            finally
            {
                await page.CloseAsync();
            }
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Request to {Url} timed out after {Timeout}ms", request.Url, request.TimeoutMilliseconds ?? request.BrowserConfiguration?.TimeoutMilliseconds ?? 30000);
            var timeout = request.TimeoutMilliseconds ?? request.BrowserConfiguration?.TimeoutMilliseconds ?? 30000;
            return new FetchResult(
                Url: request.Url,
                Content: string.Empty,
                ContentLoadedAt: DateTime.UtcNow,
                TotalDurationMilliseconds: (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                HasWaited: hasWaited,
                WaitDurationMilliseconds: hasWaited ? waitDurationMs : null,
                Error: $"Request to {request.Url} timed out after {timeout}ms");
        }
        catch (HttpRequestException ex) when (ex.StatusCode != null)
        {
            _logger.LogError(ex, "Failed to navigate to {Url}: HTTP {StatusCode}", request.Url, ex.StatusCode);
            return new FetchResult(
                Url: request.Url,
                Content: string.Empty,
                ContentLoadedAt: DateTime.UtcNow,
                TotalDurationMilliseconds: (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                HasWaited: hasWaited,
                WaitDurationMilliseconds: hasWaited ? waitDurationMs : null,
                Error: $"Failed to navigate to {request.Url}: HTTP {(int)ex.StatusCode} {ex.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch {Url}: {Message}", request.Url, ex.Message);
            return new FetchResult(
                Url: request.Url,
                Content: string.Empty,
                ContentLoadedAt: DateTime.UtcNow,
                TotalDurationMilliseconds: (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                HasWaited: hasWaited,
                WaitDurationMilliseconds: hasWaited ? waitDurationMs : null,
                Error: $"Failed to fetch {request.Url}: {ex.Message}");
        }
    }
}
