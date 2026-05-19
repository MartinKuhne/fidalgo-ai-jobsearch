# Data Model: Browser Fetch Tool

**Feature**: Browser Fetch Tool  
**Branch**: 005-browser-fetch  
**Date**: 2026-05-19

## Entities

### BrowserConfiguration

Represents configurable browser settings for the fetch tool.

**Fields**:
- `ViewportWidth` (int): Width of browser viewport in pixels (default: 1920)
- `ViewportHeight` (int): Height of browser viewport in pixels (default: 1080)
- `UserAgent` (string): Custom user agent string for HTTP requests (default: browser default)
- `Headless` (bool): Whether to run browser in headless mode (default: true)
- `TimeoutMilliseconds` (int): Maximum time for page operations in milliseconds (default: 30000)

**Validation Rules**:
- `ViewportWidth` must be between 320 and 8192
- `ViewportHeight` must be between 240 and 4320
- `TimeoutMilliseconds` must be between 1000 and 300000

**State Transitions**: N/A (immutable value object)

---

### FetchRequest

Represents a single fetch operation request.

**Fields**:
- `Url` (string, required): The URL to fetch
- `WaitForSelector` (string, optional): CSS selector to wait for before capturing content
- `TimeoutMilliseconds` (int, optional): Override for default timeout (null uses BrowserConfiguration default)
- `BrowserConfiguration` (BrowserConfiguration, optional): Custom browser settings for this request

**Validation Rules**:
- `Url` must be a valid HTTP or HTTPS URL
- `WaitForSelector` must be a valid CSS selector if provided
- `TimeoutMilliseconds` must be positive if provided

**State Transitions**: N/A (immutable value object)

---

### FetchResult

Represents the result of a fetch operation.

**Fields**:
- `Url` (string): The URL that was fetched
- `Content` (string): The HTML content of the page
- `ContentLoadedAt` (DateTime): When the content was successfully retrieved
- `TotalDurationMilliseconds` (int): Total time taken for the fetch operation
- `HasWaited` (bool): Whether the tool waited for a selector before capturing
- `WaitDurationMilliseconds` (int?): Time spent waiting for selector (null if no wait)
- `Error` (string, optional): Error message if the operation failed
- `IsSuccess` (bool): Whether the fetch operation succeeded

**Validation Rules**:
- `IsSuccess` must be true when `Error` is null
- `IsSuccess` must be false when `Error` is non-null
- `ContentLoadedAt` must be after operation start time
- `TotalDurationMilliseconds` must be positive

**State Transitions**: N/A (immutable result object)

---

### BrowserInstance

Represents a Playwright browser instance and its lifecycle.

**Fields**:
- `BrowserId` (string): Unique identifier for this browser instance
- `LaunchedAt` (DateTime): When the browser was launched
- `ClosedAt` (DateTime?, optional): When the browser was closed (null if still running)
- `PageCount` (int): Number of pages created through this browser
- `LastPageCreatedAt` (DateTime?): When the last page was created
- `LastPageClosedAt` (DateTime?): When the last page was closed

**Validation Rules**:
- `ClosedAt` must be after `LaunchedAt` if present
- `LastPageCreatedAt` must be after `LaunchedAt` if present
- `LastPageClosedAt` must be after `LastPageCreatedAt` if present

**State Transitions**:
1. **Created** → **Launched**: Browser process started
2. **Launched** → **Active**: First page created
3. **Active** → **Active**: Additional pages created (PageCount increments)
4. **Active** → **Closing**: Last page closed
5. **Closing** → **Closed**: Browser process terminated

---

## Relationships

- **FetchRequest** → **BrowserConfiguration**: Optional embedded configuration
- **FetchResult** ← **FetchRequest**: Result corresponds to original request
- **BrowserInstance** → **FetchResult**: Multiple fetch results may use same browser instance

## Database Schema

No database schema required - all entities are in-memory value objects.

## API Contracts

### IBrowserFetchTool Interface

```csharp
public interface IBrowserFetchTool
{
    /// <summary>
    /// Fetches the HTML content of a web page using Playwright browser automation.
    /// </summary>
    /// <param name="request">The fetch request containing URL and optional wait conditions.</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>FetchResult with HTML content or error information.</returns>
    Task<FetchResult> FetchAsync(FetchRequest request, CancellationToken cancellationToken = default);
}
```
