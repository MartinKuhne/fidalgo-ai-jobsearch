# Quick Start: Browser Fetch Tool

**Feature**: Browser Fetch Tool  
**Branch**: 005-browser-fetch  
**Date**: 2026-05-19

## Prerequisites

- .NET 10.0 SDK installed
- Firefox browser installed on the system
- Playwright Firefox browser support installed (`dotnet tool install -g Microsoft.Playwright.CLI`)

## Basic Usage

### 1. Register the Tool

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddBrowserFetch(); // Add to service collection
```

### 2. Fetch a Web Page

```csharp
var fetchTool = serviceProvider.GetRequiredService<IBrowserFetchTool>();
var result = await fetchTool.FetchAsync(new FetchRequest("https://example.com"));

if (result.IsSuccess)
{
    Console.WriteLine($"Fetched {result.Url} in {result.TotalDurationMilliseconds}ms");
    Console.WriteLine($"Content length: {result.Content.Length} characters");
}
else
{
    Console.WriteLine($"Failed to fetch {result.Url}: {result.Error}");
}
```

### 3. Configure Browser Settings

```csharp
var request = new FetchRequest(
    url: "https://example.com",
    browserConfiguration: new BrowserConfiguration(
        viewportWidth: 1280,
        viewportHeight: 720,
        userAgent: "Custom-Agent/1.0",
        headless: true,
        timeoutMilliseconds: 45000
    )
);

var result = await fetchTool.FetchAsync(request);
```

### 4. Wait for Dynamic Content

```csharp
var request = new FetchRequest(
    url: "https://example.com",
    waitForSelector: "#dynamic-content"
);

var result = await fetchTool.FetchAsync(request);

if (result.HasWaited)
{
    Console.WriteLine($"Waited {result.WaitDurationMilliseconds}ms for selector");
}
```

## Advanced Scenarios

### Custom Retry Policy

The tool uses the existing `RetryPolicy` for transient failures:

```csharp
// Retry on timeout or browser crashes
var request = new FetchRequest("https://example.com");
var result = await fetchTool.FetchAsync(request);
// Automatically retries up to 3 times with exponential backoff
```

### Integration with Agent Tracing

The tool automatically creates spans and propagates trace context:

```csharp
// Trace context is automatically propagated
var traceContext = new TraceContext(
    traceId: "abc123",
    spanId: "def456",
    parentSpanId: null,
    correlationId: "corr789",
    timestamp: DateTime.UtcNow,
    isRoot: true
);

var result = await fetchTool.FetchAsync(new FetchRequest("https://example.com"));
// Spans are automatically created with trace context
```

### Error Handling

```csharp
try
{
    var result = await fetchTool.FetchAsync(new FetchRequest("https://nonexistent.example"));
    
    if (!result.IsSuccess)
    {
        // Handle fetch failure
        Console.WriteLine($"Fetch failed: {result.Error}");
    }
}
catch (InvalidOperationException ex)
{
    // Handle tool-level errors (e.g., after all retries exhausted)
    Console.WriteLine($"Tool error: {ex.Message}");
}
```

## Configuration

### appsettings.json

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

### Environment Variables

```bash
export BrowserFetch__ViewportWidth=1280
export BrowserFetch__ViewportHeight=720
export BrowserFetch__TimeoutMilliseconds=45000
```

## Testing

### Unit Test Example

```csharp
[Fact]
public async Task FetchAsync_ReturnsContent()
{
    // Arrange
    var fetchTool = new BrowserFetchTool();
    var request = new FetchRequest("https://httpbin.org/html");
    
    // Act
    var result = await fetchTool.FetchAsync(request);
    
    // Assert
    Assert.True(result.IsSuccess);
    Assert.Contains("<html", result.Content, StringComparison.OrdinalIgnoreCase);
    Assert.Equal("https://httpbin.org/html", result.Url);
}
```

### Integration Test Example

```csharp
[Fact]
public async Task FetchAsync_WithWaitForSelector_ReturnsContent()
{
    // Arrange
    var fetchTool = new BrowserFetchTool();
    var request = new FetchRequest(
        url: "https://example.com",
        waitForSelector: "h1"
    );
    
    // Act
    var result = await fetchTool.FetchAsync(request);
    
    // Assert
    Assert.True(result.IsSuccess);
    Assert.True(result.HasWaited);
    Assert.NotNull(result.WaitDurationMilliseconds);
    Assert.Contains("<h1", result.Content, StringComparison.OrdinalIgnoreCase);
}
```

## Troubleshooting

### Browser Not Found

**Error**: "Failed to launch browser: firefox"

**Solution**: Ensure Firefox is installed and accessible in PATH:
```bash
firefox --version
```

### Playwright Browser Support

**Error**: "Browser binary not found"

**Solution**: Install Playwright Firefox browser:
```bash
dotnet tool install -g Microsoft.Playwright.CLI
playwright install firefox
```

### Timeout Issues

**Error**: "Request timed out"

**Solution**: Increase timeout in configuration:
```json
{
  "BrowserFetch": {
    "TimeoutMilliseconds": 60000
  }
}
```

### Headless Mode Not Working

**Error**: "Display not found" (Linux)

**Solution**: Install Xvfb or use headless mode:
```csharp
var config = new BrowserConfiguration(headless: true);
```

## Performance Tips

1. **Reuse Browser Instances**: The tool manages browser lifecycle automatically
2. **Use Headless Mode**: Set `headless: true` for server environments
3. **Set Appropriate Timeouts**: Adjust timeout based on network conditions
4. **Wait Strategically**: Use `waitForSelector` only when necessary
5. **Monitor Logs**: Check for retry attempts indicating network issues
