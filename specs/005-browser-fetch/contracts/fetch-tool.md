# Contracts: Browser Fetch Tool

**Feature**: Browser Fetch Tool  
**Branch**: 005-browser-fetch  
**Date**: 2026-05-19

## Overview

This document defines the public contracts for the browser_fetch tool. The tool is integrated into the Fidalgo Agent as a service that can be invoked programmatically.

## Tool Interface

### IBrowserFetchTool

The primary interface for browser fetch operations.

```csharp
namespace Fidalgo.Agent.Tools;

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
```

## Request/Response Contracts

### FetchRequest

```csharp
public record FetchRequest(
    string Url,
    string? WaitForSelector = null,
    int? TimeoutMilliseconds = null,
    BrowserConfiguration? BrowserConfiguration = null);
```

**Validation Requirements**:
- `Url`: Must be a valid HTTP or HTTPS URL (e.g., "https://example.com")
- `WaitForSelector`: Must be a valid CSS selector if provided (e.g., "#main-content", ".article")
- `TimeoutMilliseconds`: Must be between 1000 and 300000 if provided

### FetchResult

```csharp
public record FetchResult(
    string Url,
    string Content,
    DateTime ContentLoadedAt,
    int TotalDurationMilliseconds,
    bool HasWaited,
    int? WaitDurationMilliseconds,
    string? Error = null);
```

**Properties**:
- `Url`: The URL that was fetched (echoed from request)
- `Content`: Complete HTML content of the rendered page
- `ContentLoadedAt`: UTC timestamp when content was retrieved
- `TotalDurationMilliseconds`: Total operation time including any waiting
- `HasWaited`: True if waitForSelector was used and executed
- `WaitDurationMilliseconds`: Time spent waiting for selector (null if no wait)
- `Error`: Non-null only if operation failed

### BrowserConfiguration

```csharp
public record BrowserConfiguration(
    int ViewportWidth = 1920,
    int ViewportHeight = 1080,
    string? UserAgent = null,
    bool Headless = true,
    int TimeoutMilliseconds = 30000);
```

**Default Values**:
- `ViewportWidth`: 1920 pixels
- `ViewportHeight`: 1080 pixels
- `UserAgent`: Browser default (Playwright's Firefox default)
- `Headless`: true (headless mode for server environments)
- `TimeoutMilliseconds`: 30000 (30 seconds)

## Error Contracts

### Error Response Format

When fetch operations fail, the `FetchResult.Error` property contains a human-readable message:

```
"Failed to fetch {url}: {specific_error}"
```

**Common Error Scenarios**:
- Invalid URL format: "Invalid URL format: {url}"
- Network timeout: "Request to {url} timed out after {timeout}ms"
- Browser crash: "Browser crashed during navigation to {url}"
- Element not found: "Selector '{selector}' not found on {url} after {timeout}ms"
- Navigation failure: "Failed to navigate to {url}: {http_status_code}"

## Integration Contracts

### Dependency Injection Registration

```csharp
// In ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBrowserFetch(this IServiceCollection services)
    {
        services.AddOptions<BrowserFetchOptions>()
            .BindConfiguration("BrowserFetch")
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        services.AddScoped<IBrowserFetchTool, BrowserFetchTool>();
        return services;
    }
}
```

### Configuration Schema

```json
{
  "BrowserFetch": {
    "ViewportWidth": 1920,
    "ViewportHeight": 1080,
    "UserAgent": null,
    "Headless": true,
    "TimeoutMilliseconds": 30000
  }
}
```

## Tracing Contracts

### Span Attributes

The tool must emit OpenTelemetry spans with the following attributes:

| Attribute | Type | Description |
|-----------|------|-------------|
| `browser.type` | string | "firefox" |
| `fetch.url` | string | The URL being fetched |
| `fetch.duration_ms` | int | Total operation duration |
| `fetch.waited` | boolean | Whether waitForSelector was used |
| `fetch.wait_duration_ms` | int? | Time spent waiting for selector |
| `fetch.success` | boolean | Whether operation succeeded |
| `fetch.error` | string? | Error message if failed |

### Span Hierarchy

```
Root Span: "browser_fetch"
  └─ Child Span: "browser_launch"
  └─ Child Span: "page_navigation"
     └─ Child Span: "element_wait" (if waitForSelector specified)
  └─ Child Span: "browser_close"
```

## Logging Contracts

### Log Events

| Level | Event | Fields |
|-------|-------|--------|
| INFO | Tool initialized | `tool`, `viewport`, `timeout` |
| INFO | Navigation started | `url`, `trace_id`, `correlation_id` |
| INFO | Navigation completed | `url`, `duration_ms`, `trace_id` |
| INFO | Element wait started | `selector`, `timeout_ms`, `trace_id` |
| INFO | Element found | `selector`, `duration_ms`, `trace_id` |
| WARN | Retry attempt | `attempt`, `max_retries`, `error`, `trace_id` |
| ERROR | Fetch failed | `url`, `error`, `stack_trace`, `trace_id` |
| ERROR | Browser crashed | `url`, `error`, `stack_trace`, `trace_id` |

## Performance Contracts

### Success Criteria Alignment

| Criterion | Contract Requirement |
|-----------|---------------------|
| SC-001: <30s for standard pages | Default timeout: 30000ms |
| SC-002: 95% JS rendering accuracy | Use Playwright's WaitForLoadState("networkidle") |
| SC-003: Viewport 320x568 to 1920x1080+ | Configurable via BrowserConfiguration |
| SC-004: Wait for async content | waitForSelector support with configurable timeout |
| SC-005: 90% error clarity | Error messages include URL, error type, and context |

## Versioning

**Contract Version**: 1.0.0  
**Compatible Tool Versions**: >=1.0.0
