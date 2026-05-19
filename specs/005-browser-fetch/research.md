# Research: Browser Fetch Tool with Playwright

**Feature**: Browser Fetch Tool  
**Branch**: 005-browser-fetch  
**Date**: 2026-05-19

## Decisions

### Decision: Use Playwright .NET API with Firefox Browser

**What**: Implement the browser_fetch tool using Playwright .NET library with Firefox as the target browser.

**Rationale**: 
- Playwright provides robust cross-browser automation with first-class support for Firefox
- Playwright .NET is actively maintained by Microsoft and has comprehensive API coverage
- Firefox support is production-ready with full WebDriver BiDi implementation
- Playwright handles JavaScript execution, page loading events, and browser lifecycle management automatically
- The library is open source (MIT license) and compatible with project requirements

**Alternatives considered**:
- Selenium WebDriver: More verbose API, requires manual browser driver management, less reliable for dynamic content
- PuppeteerSharp: Firefox support is experimental/limited, primarily designed for Chrome
- Custom browser automation: Would require significant development effort and maintenance burden

---

### Decision: Implement as a Tool Class Following Existing Patterns

**What**: Create a `BrowserFetchTool` class in `src/Fidalgo.Agent/Tools/` following the same pattern as `FetchTool`.

**Rationale**:
- Consistent with existing project structure (FetchTool.cs, SaveJobTool.cs, GetJobsTool.cs)
- Leverages existing dependency injection and service collection patterns
- Follows the same error handling and retry patterns used throughout the agent
- Enables easy testing with interface abstraction (IBrowserFetchTool)

**Alternatives considered**:
- Standalone console application: Would require separate project and deployment complexity
- Azure Function: Overkill for this feature, adds infrastructure complexity
- Microservice: Unnecessary decoupling for a tool that integrates tightly with the agent

---

### Decision: Use Configuration-Based Browser Settings

**What**: Implement browser configuration (viewport, user agent) via options pattern with sensible defaults.

**Rationale**:
- Aligns with existing configuration patterns in the project (LoggingConfiguration, TracingConfiguration)
- Allows runtime customization without code changes
- Supports environment-specific configuration via appsettings.json
- Enables easy testing with mock configurations

**Alternatives considered**:
- Hardcoded values: Would require code changes for any configuration adjustment
- Command-line arguments: Less flexible for different deployment scenarios
- Environment variables: Possible but less structured than options pattern

---

### Decision: Implement Wait Mechanism Using CSS Selectors

**What**: Support waiting for DOM elements using CSS selector expressions before capturing content.

**Rationale**:
- CSS selectors are the standard way to identify elements in web pages
- Playwright's `WaitForSelectorAsync` provides reliable element detection
- Supports both visible and hidden element waiting
- More flexible than waiting for specific text or attributes

**Alternatives considered**:
- XPath selectors: More powerful but less commonly used and more complex
- Text-based waiting: Too fragile for dynamic content
- Timeout-only waiting: Doesn't guarantee specific content is loaded

---

### Decision: Use Default 30-Second Timeout with Configurable Override

**What**: Implement a default 30-second timeout for page fetch operations with support for configuration.

**Rationale**:
- 30 seconds aligns with SC-001 success criterion ("within 30 seconds")
- Playwright's default timeout is 30 seconds, aligning with industry standards
- Configurable via options pattern allows adjustment for slow networks or complex pages
- Prevents indefinite hanging on unresponsive sites

**Alternatives considered**:
- Fixed timeout: Would not accommodate varying network conditions
- No timeout: Risk of infinite hanging on problematic sites
- Shorter timeout (10-15s): May be too aggressive for complex pages

---

### Decision: Implement Graceful Browser Cleanup with Using Statement

**What**: Use Playwright's `using` statement pattern to ensure browser instances are properly disposed.

**Rationale**:
- Playwright's recommended pattern for resource management
- Ensures browser process termination even on errors
- Prevents orphaned browser processes consuming system resources
- Simple and explicit code structure

**Alternatives considered**:
- Manual cleanup: Error-prone, may miss cleanup on exceptions
- Finalizer-based cleanup: Non-deterministic timing, not reliable for resource management
- Background cleanup service: Overly complex for this use case

---

## Dependencies

### Required NuGet Packages

1. **Microsoft.Playwright** (latest stable)
   - Primary library for browser automation
   - Supports Firefox, Chrome, Edge
   - MIT license, Microsoft maintained

2. **Microsoft.Extensions.Options** (already in project via Microsoft.Extensions.Hosting)
   - Configuration pattern for browser settings
   - Already available through existing dependencies

3. **Microsoft.Extensions.Logging** (already in project via Serilog.AspNetCore)
   - Structured logging with correlation IDs
   - Already available through existing dependencies

## Integration Patterns

### Integration with Existing Agent Architecture

**Pattern**: Tool Interface Implementation

The `BrowserFetchTool` will implement an `IBrowserFetchTool` interface similar to existing tools:

```csharp
public interface IBrowserFetchTool
{
    Task<FetchResult> FetchAsync(FetchRequest request, CancellationToken cancellationToken = default);
}
```

**Service Registration** (in ServiceCollectionExtensions):
```csharp
services.AddOptions<BrowserFetchOptions>()
    .BindConfiguration("BrowserFetch");
services.AddScoped<IBrowserFetchTool, BrowserFetchTool>();
```

### Tracing Integration

Following NFR-100 through NFR-108 requirements:
- Create root span for each fetch operation
- Propagate TraceID and CorrelationID to browser automation
- Add child spans for browser lifecycle events (launch, page creation, navigation, close)
- Include timing metrics in span attributes

### Error Handling Integration

Leverage existing `RetryPolicy` for transient failures:
- Retry on `TimeoutException` (page load timeout)
- Retry on `BrowserClosedException` (unexpected browser termination)
- Do not retry on `NavigationException` (permanent failures)

### Logging Integration

Follow NFR-001 through NFR-006 requirements:
- Log INFO on tool startup/shutdown
- Log ERROR with full stack trace on exceptions
- Include TraceID and CorrelationID in all log entries
- Log key events: navigation start, navigation complete, element wait start, element found

## Open Questions

None - all technical decisions resolved through research.
