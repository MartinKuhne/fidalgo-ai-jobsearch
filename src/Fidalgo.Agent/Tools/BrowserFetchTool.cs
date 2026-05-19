using Fidalgo.Agent.Models;

namespace Fidalgo.Agent.Tools;

/// <summary>
/// Tool for fetching web page content using Playwright browser automation.
/// </summary>
public class BrowserFetchTool : IBrowserFetchTool
{
    /// <inheritdoc/>
    public async Task<FetchResult> FetchAsync(FetchRequest request, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var hasWaited = false;
        var waitDurationMs = 0;

        try
        {
            using var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = request.BrowserConfiguration?.Headless ?? true,
            }, cancellationToken);

            using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize
                {
                    Width = request.BrowserConfiguration?.ViewportWidth ?? 1920,
                    Height = request.BrowserConfiguration?.ViewportHeight ?? 1080,
                },
                UserAgent = request.BrowserConfiguration?.UserAgent,
            }, cancellationToken);

            using var page = await context.NewPageAsync(cancellationToken);

            var timeout = request.TimeoutMilliseconds ?? request.BrowserConfiguration?.TimeoutMilliseconds ?? 30000;

            if (!string.IsNullOrEmpty(request.WaitForSelector))
            {
                var waitStartTime = DateTime.UtcNow;
                hasWaited = true;

                await page.WaitForSelectorAsync(request.WaitForSelector, new FrameWaitForSelectorOptions
                {
                    Timeout = timeout,
                }, cancellationToken);

                waitDurationMs = (int)(DateTime.UtcNow - waitStartTime).TotalMilliseconds;
            }

            await page.GotoAsync(request.Url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = timeout,
            }, cancellationToken);

            var content = await page.ContentAsync(cancellationToken);

            var totalDuration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            return new FetchResult(
                Url: request.Url,
                Content: content,
                ContentLoadedAt: DateTime.UtcNow,
                TotalDurationMilliseconds: totalDuration,
                HasWaited: hasWaited,
                WaitDurationMilliseconds: hasWaited ? waitDurationMs : null,
                Error: null);
        }
        catch (TimeoutException ex)
        {
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
        catch (Exception ex) when (ex.Message.Contains("Browser", StringComparison.OrdinalIgnoreCase))
        {
            return new FetchResult(
                Url: request.Url,
                Content: string.Empty,
                ContentLoadedAt: DateTime.UtcNow,
                TotalDurationMilliseconds: (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                HasWaited: hasWaited,
                WaitDurationMilliseconds: hasWaited ? waitDurationMs : null,
                Error: $"Browser error during navigation to {request.Url}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode != null)
        {
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
