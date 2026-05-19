# Data Model: Job Scraper Agent

## JobPosting

Represents a discovered job posting extracted from a website.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | Guid | Primary Key, Required | Unique identifier for this record |
| SourceUrl | string | Required, Unique | The URL where this job was found |
| Title | string | Required | Job title or position name |
| Company | string | Required | Employer or company name |
| Description | string | Required | Full job description text |
| PostedDate | DateTime | Nullable | When the job was posted (if available) |
| SalaryLow | decimal | Nullable | Minimum salary range value |
| SalaryHigh | decimal | Nullable | Maximum salary range value |
| Currency | string | Nullable, default "USD" | Salary currency code |
| MatchedKeywords | string | Required | JSON array of search keywords that matched this job |
| SourceWebsite | string | Required | Name of the website where found |
| ScrapedAt | DateTime | Required, default now | When this job was scraped |
| IsDiscarded | bool | Required, default false | Whether the user has discarded this job |
| AppliedDate | DateTime | Nullable | Date user marked as applied |

**Validation Rules**:
- SourceUrl must be a valid URI
- Title and Company must not be empty
- MatchedKeywords must contain at least one keyword
- SalaryLow must be less than or equal to SalaryHigh if both are provided

**Duplicate Detection**:
- Primary key: SourceUrl (unique constraint)
- Fallback key (when no URL): Hash of (Title + Company + Description)

## SearchConfiguration

Defines which websites to monitor and what keywords to search for.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | Guid | Primary Key, Required | Unique identifier |
| UserEmail | string | Required, Indexed | Email address identifying the user |
| Websites | string | Required | JSON array of website names to monitor |
| Keywords | string | Required | JSON array of search keywords |
| IsActive | bool | Required, default true | Whether this configuration is active |
| CreatedAt | DateTime | Required, default now | When this config was created |
| UpdatedAt | DateTime | Required, default now | When this config was last updated |

**Validation Rules**:
- UserEmail must be a valid email format
- Websites must contain at least one entry from the supported list
- Keywords must contain at least one entry

**Supported Websites**:
- governmentjobs.com
- google
- glassdoor.com
- monster.com
- indeed.com
- linkedin.com

## ScrapeResult

Records the outcome of each scraping attempt.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | Guid | Primary Key, Required | Unique identifier |
| ConfigurationId | Guid | Required, Indexed | Foreign key to SearchConfiguration |
| Website | string | Required | Name of the website scraped |
| Status | string | Required | Success, Partial, or Failed |
| JobsFound | int | Required, default 0 | Number of new jobs discovered |
| JobsSkipped | int | Required, default 0 | Number of duplicates skipped |
| ErrorMessage | string | Nullable | Error details if status is Failed |
| StartedAt | DateTime | Required | When scraping started |
| CompletedAt | DateTime | Nullable | When scraping finished |
| DurationSeconds | decimal | Nullable | Elapsed time in seconds |

**Validation Rules**:
- Status must be one of: Success, Partial, Failed
- ConfigurationId must reference an existing SearchConfiguration

## Relationships

```
SearchConfiguration (1) ----< (N) JobPosting
SearchConfiguration (1) ----< (N) ScrapeResult
```

- Each SearchConfiguration can have many JobPostings and ScrapeResults
- Each JobPosting references the SearchConfiguration that triggered its discovery
- Each ScrapeResult references the SearchConfiguration that was active during the scrape
