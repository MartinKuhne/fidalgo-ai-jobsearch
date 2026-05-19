# Scraper Interface Contract

## Interface: IWebsiteScraper

All website scrapers must implement this contract to be discoverable by the `WebsiteScraperRegistry`.

### Method: SearchAsync

```
SearchAsync(string keyword, CancellationToken cancellationToken)
    -> Task<List<JobPosting>>
```

**Purpose**: Search the website for job postings matching the given keyword.

**Parameters**:
- `keyword` (string): The search keyword to look for (e.g., "software engineer")
- `cancellationToken` (CancellationToken): Token for cancelling the operation

**Returns**: A list of `JobPosting` objects found during the search. The list may be empty but must not be null.

**Throws**:
- `ScraperException`: When the website is unreachable, returns an error page, or the response cannot be parsed. The exception must include the website name and a human-readable error message.
- `OperationCanceledException`: When the cancellation token is triggered.

### Class: JobPosting

```
class JobPosting
{
    string SourceUrl { get; }
    string Title { get; }
    string Company { get; }
    string Description { get; }
    DateTime? PostedDate { get; }
    decimal? SalaryLow { get; }
    decimal? SalaryHigh { get; }
    string Currency { get; }
    string SourceWebsite { get; }
}
```

**Constraints**:
- `SourceUrl` must be the canonical URL of the job posting (not a search results page)
- `Title` and `Company` must not be null or empty
- `Description` must not be null or empty
- `SourceWebsite` must match the scraper's registered website name
- `PostedDate` may be null if the website does not provide it
- `SalaryLow` and `SalaryHigh` may be null if salary is not listed
- `Currency` defaults to "USD" if not specified

### Registration Contract

Each scraper must register itself with the `WebsiteScraperRegistry` using its website name. The registry uses the website name (e.g., "indeed.com") as the lookup key.

**Registration example**:
```
registry.Register("indeed.com", new IndeedScraper());
```

**Duplicate registration**: If a scraper attempts to register a website name that is already registered, the registry must throw `InvalidOperationException` with a descriptive message.

### Error Handling Contract

All scrapers must:
1. Return an empty list (not null) when no jobs are found
2. Throw `ScraperException` (not generic exceptions) when scraping fails
3. Respect the `CancellationToken` and check it periodically during long operations
4. Implement a reasonable timeout (default 30 seconds per page)

### Rate Limiting Contract

Scrapers must not make more than one request per 3 seconds to the same website. The rate limiter is enforced by the `WebsiteScraperRegistry` layer, not individual scrapers. Scrapers must not implement their own rate limiting.
