# Data Model: AI-Powered Job Scraper Agent

## Job

Represents a discovered and analyzed job posting stored by the system.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| InternalId | string (GUID) | Primary Key, Auto-generated | Unique identifier for this record |
| Email | string | Required, Indexed | Email identifier for the user |
| Employer | string | Required | Employer or company name |
| PostedDate | DateTime | Nullable | When the job was posted if available |
| EmployerJobId | string | Nullable | The job ID from the employer's system |
| SalaryRangeLow | decimal | Nullable | Low end of the salary range |
| SalaryRangeHigh | decimal | Nullable | High end of the salary range |
| Description | string (markdown) | Required | The job description as posted |
| Pros | string (markdown) | Required | Why this job fits the user's profile |
| Cons | string (markdown) | Required | Potential concerns or mismatches |
| ResumeHints | string (markdown) | Required | Actionable recommendations for resume tailoring |
| Score | int (0-100) | Required | Match score as a percentage |
| Recommendation | string | Required, one of: Apply, Maybe, Do not apply | Apply recommendation |
| IsDeleted | bool | Required, default false | Soft-delete flag for user-discarded jobs |
| DateNotified | DateTime | Nullable | When a notification was sent to the user |
| SourceWebsite | string | Required | The website where the job was found |

### Validation Rules
- Score must be between 0 and 100
- Recommendation must be one of "Apply", "Maybe", or "Do not apply"
- All text fields (Description, Pros, Cons, ResumeHints) must be in markdown format
- InternalId is auto-generated and not provided as input
- IsDeleted defaults to false when a job is first saved

## User (thin profile)

A user is identified by their email address and does not require a separate user entity in the database. The email is used as a key to scope job records. Each job record belongs to exactly one user (by email), and one user can have many job records.

## Relationships

```
User (email) (1) ----< (N) Job
```

- One user can have many saved job records
- Each job record belongs to exactly one user identified by email
- No separate user table is needed; the email field on Job serves as the user foreign key