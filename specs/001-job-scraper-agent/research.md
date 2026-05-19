# Research: Job Scraper Agent

## Decision: Use AngleSharp for HTML parsing

**Rationale**: AngleSharp is a well-maintained, pure-C# HTML5-compliant parser with a jQuery-like query API. It handles malformed HTML gracefully, supports CSS selectors for element extraction, and has no native dependencies. It is the most widely used HTML parser in the C# ecosystem for scraping scenarios.

**Alternatives considered**:
- HtmlAgilityPack: Older, less actively maintained, but still functional. Chosen against due to AngleSharp's superior API and better modern C# support.
- Regex-based parsing: Rejected due to fragility and inability to handle malformed HTML.
- Headless browser (Playwright/ Puppeteer): Overkill for static page scraping; adds significant complexity and resource overhead.

## Decision: Use Microsoft.Data.Sqlite with EF Core for database

**Rationale**: The project requires SQLite (spec/SPEC.md REQ-007). EF Core provides a clean abstraction over SQLite, enables LINQ queries for duplicate detection, and supports migrations for schema evolution. Microsoft.Data.Sqlite is the official Microsoft provider with excellent Windows support and a single DLL dependency.

**Alternatives considered**:
- Dapper: Lighter weight but requires manual SQL mapping. Chosen against because EF Core's change tracking simplifies insert-or-skip duplicate logic.
- Raw SQLite via Microsoft.Data.Sqlite: Rejected because EF Core provides better testability through DbContext mocking and migration support.

## Decision: Strategy pattern for website scrapers

**Rationale**: Each job site has a different URL structure, HTML layout, and anti-bot measure. A shared `IWebsiteScraper` interface with per-site implementations allows independent development, testing, and failure isolation. If one site changes its layout, only that scraper needs updating.

**Alternatives considered**:
- Single monolithic scraper with site-specific branches: Rejected because it violates SRP and makes each site's logic tightly coupled.
- Generic HTML template engine: Rejected because job sites vary too widely in structure; templates would be as complex as custom scrapers.

## Decision: HttpClient with custom DelegatingHandler for rate limiting

**Rationale**: HttpClient is the standard C# HTTP client. A custom DelegatingHandler can enforce per-site rate limiting (e.g., one request per 3 seconds) without polluting scraper logic. Connection pooling is built-in via HttpClient reuse.

**Alternatives considered**:
- Polly retry/resilience library: Excellent for retry and circuit-breaking, but rate limiting is better handled at the HttpClient level. Will be used alongside the DelegatingHandler for retry logic.
- Third-party scraping libraries (e.g., Flurl): Simpler HTTP API but adds dependency; HttpClient + DelegatingHandler provides the same flexibility with fewer dependencies.

## Decision: BackgroundService for periodic execution

**Rationale**: .NET's `BackgroundService` (via `IHostedService`) provides a clean pattern for long-running background tasks. It supports graceful shutdown, periodic execution via `Task.Delay` with cancellation, and integrates with .NET's dependency injection and configuration systems.

**Alternatives considered**:
- Windows Task Scheduler: Works but ties the agent to Windows; BackgroundService supports both Windows and Linux containers.
- Quartz.NET: Overkill for a simple periodic interval; BackgroundService is sufficient for the "every 4 hours" requirement.

## Decision: JSON configuration file for user settings

**Rationale**: JSON is the standard configuration format in .NET via `Microsoft.Extensions.Configuration.Json`. It supports hierarchical settings, is human-editable, and integrates with .NET's built-in options pattern (`IOptions<T>`).

**Alternatives considered**:
- YAML: More readable but requires a third-party parser in .NET.
- INI files: Simpler but lack nested structure support needed for per-site keyword lists.

## Decision: Unique key based on source URL + search keyword combination

**Rationale**: A job posting can appear on multiple sites with the same URL. The deduplication key combines the source URL and the search keyword that found it, ensuring that the same job discovered via different searches is stored only once per unique URL. For jobs with no URL, a hash of (title + company + description) serves as a fallback key.

**Alternatives considered**:
- Hash of title + company + URL: Simpler but fails when the same job appears on multiple sites with different URLs.
- External ID from job board: Not always available and inconsistent across sites.
