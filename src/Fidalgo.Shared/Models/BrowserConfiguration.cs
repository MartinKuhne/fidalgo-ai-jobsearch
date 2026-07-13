namespace Fidalgo.Shared.Models;

/// <summary>
/// Represents configurable browser settings for the fetch tool.
/// </summary>
/// <param name="ViewportWidth">Width of browser viewport in pixels (default: 1920).</param>
/// <param name="ViewportHeight">Height of browser viewport in pixels (default: 1080).</param>
/// <param name="UserAgent">Custom user agent string for HTTP requests (optional).</param>
/// <param name="Headless">Whether to run browser in headless mode (default: true).</param>
/// <param name="TimeoutMilliseconds">Maximum time for page operations in milliseconds (default: 30000).</param>
/// <param name="SignInPauseSeconds">Seconds to pause when a sign-in page is detected (default: 120).</param>
/// <param name="MaxWaitForInterceptSeconds">Seconds to wait for an intercept page (Cloudflare/sign-in) to resolve by reloading (default: 0 = disabled).</param>
public record BrowserConfiguration(
    int ViewportWidth = 1920,
    int ViewportHeight = 1080,
    string? UserAgent = null,
    bool Headless = true,
    int TimeoutMilliseconds = 30000,
    int SignInPauseSeconds = 120,
    int MaxWaitForInterceptSeconds = 0);