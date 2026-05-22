using Fidalgo.Agent.Models;
using Fidalgo.Agent.Sanitization;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Agent.Tools;

/// <summary>
/// Tool for fetching web page content using Playwright browser automation.
/// </summary>
public class BrowserFetchTool : IBrowserFetchTool
{
    private readonly ILogger<BrowserFetchTool> _logger;
    private readonly HtmlStripper _htmlStripper;

    public BrowserFetchTool(ILogger<BrowserFetchTool> logger, HtmlStripper htmlStripper)
    {
        _logger = logger;
        _htmlStripper = htmlStripper;
    }
    /// <inheritdoc/>
    public async Task<FetchResult> FetchAsync(FetchRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching URL: {Url}", request.Url);

        var fetchId = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);

        var startTime = DateTime.UtcNow;
        var hasWaited = false;
        var waitDurationMs = 0;

        try
        {
            using var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = request.BrowserConfiguration?.Headless ?? false,
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

               var initialUrl = request.Url;
                var isIndeedJobs = initialUrl.Contains("indeed.com/jobs", StringComparison.OrdinalIgnoreCase);

                await page.GotoAsync(request.Url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = timeout,
                });

                var signInPauseSeconds = request.BrowserConfiguration?.SignInPauseSeconds ?? 0;
                var maxWaitForInterceptSeconds = request.BrowserConfiguration?.MaxWaitForInterceptSeconds ?? 0;

                if (signInPauseSeconds > 0 && isIndeedJobs)
                {
                    var isSignInPage = await DetectSignInPageAsync(page);

                    if (isSignInPage)
                    {
                        var currentPageUrl = page.Url;
                        _logger.LogWarning("Indeed sign-in page detected at {CurrentUrl} (requested {OriginalUrl}). Pausing for {Seconds} seconds to allow manual sign-in.", currentPageUrl, request.Url, signInPauseSeconds);
                        await Task.Delay(signInPauseSeconds * 1000, cancellationToken);
                    }
                }

                if (maxWaitForInterceptSeconds > 0 && isIndeedJobs)
                {
                    var interceptResolved = await WaitForInterceptResolutionAsync(page, initialUrl, maxWaitForInterceptSeconds, cancellationToken);
                    if (!interceptResolved)
                    {
                        _logger.LogWarning("Intercept page did not resolve within {Seconds} seconds. Proceeding with current page state.", maxWaitForInterceptSeconds);
                    }
                }

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
                var strippedContent = _htmlStripper.Strip(content);

                var totalDuration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

                _logger.LogInformation("Successfully fetched URL: {Url} in {Duration}ms", request.Url, totalDuration);

                await WriteFetchLogAsync(fetchId, request.Url, content, null, cancellationToken);

                return new FetchResult(
                    Url: request.Url,
                    Content: strippedContent,
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
            await WriteFetchLogAsync(fetchId, request.Url, string.Empty, ex.Message, cancellationToken);
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
            await WriteFetchLogAsync(fetchId, request.Url, string.Empty, $"HTTP {(int)ex.StatusCode} {ex.StatusCode}", cancellationToken);
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
            await WriteFetchLogAsync(fetchId, request.Url, string.Empty, ex.Message, cancellationToken);
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

    private async Task<bool> DetectSignInPageAsync(IPage page)
    {
        var title = await page.TitleAsync();
        var titleLower = title.ToLowerInvariant();
        var isSignInTitle = titleLower.Contains("sign in") && titleLower.Contains("indeed");

        var isAuthUrl = page.Url.Contains("indeed.com/auth", StringComparison.OrdinalIgnoreCase)
                     || page.Url.Contains("secure.indeed.com/auth", StringComparison.OrdinalIgnoreCase);

        var hasAuthSelector = await page.Locator("[data-tn-component=\"auth-page-email-input\"]").CountAsync();

        return isSignInTitle || isAuthUrl || hasAuthSelector > 0;
    }

    private async Task<bool> DetectCloudflareChallengeAsync(IPage page)
    {
        var title = await page.TitleAsync();
        var titleLower = title.ToLowerInvariant();
        var isChallengeTitle = titleLower.Contains("just a moment");

        var bodyText = await page.TextContentAsync("body");
        var hasVerificationHeading = bodyText is not null && bodyText.Contains("Additional Verification Required");

        var hasCloudflareBox = await page.Locator("#cf-box-container").CountAsync();

        return isChallengeTitle || hasVerificationHeading || hasCloudflareBox > 0;
    }

    private async Task<bool> WaitForInterceptResolutionAsync(
        IPage page,
        string originalUrl,
        int maxWaitSeconds,
        CancellationToken cancellationToken)
    {
        var pollInterval = TimeSpan.FromSeconds(3);
        var maxWait = TimeSpan.FromSeconds(maxWaitSeconds);
        var elapsed = TimeSpan.Zero;

        while (elapsed < maxWait && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(pollInterval, cancellationToken);
            elapsed = pollInterval;

            if (await DetectCloudflareChallengeAsync(page))
            {
                var currentPageUrl = page.Url;
                _logger.LogInformation("Cloudflare challenge detected at {CurrentUrl} (requested {OriginalUrl}). Waiting for resolution...", currentPageUrl, originalUrl);
                continue;
            }

            var isStillOnOriginalUrl = page.Url == originalUrl;
            if (!isStillOnOriginalUrl)
            {
                _logger.LogInformation("Page navigated away from intercept page to {CurrentUrl}", page.Url);
                return true;
            }

            _logger.LogDebug("Intercept page appears resolved at {CurrentUrl}", page.Url);
            return true;
        }

        return false;
    }

    private async Task WriteFetchLogAsync(string fetchId, string url, string content, string? error, CancellationToken cancellationToken)
    {
        try
        {
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory(), "fetch-logs");
            Directory.CreateDirectory(logDir);
            var logFilePath = Path.Combine(logDir, $"fetch-{fetchId}.html");
            var logContent = error is not null
                ? $"<!-- Fetch failed: {error} -->\n<!-- URL: {url} -->\n"
                : string.Empty;
            await File.WriteAllTextAsync(logFilePath, logContent + content, cancellationToken);
            _logger.LogInformation("Fetched content written to {LogPath}", logFilePath);
        }
        catch
        {
            _logger.LogWarning("Failed to write fetch log for {Url}", url);
        }
    }
}
