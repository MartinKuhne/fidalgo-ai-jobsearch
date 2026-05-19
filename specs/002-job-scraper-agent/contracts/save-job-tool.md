# Contract: Save Job Tool

**Purpose**: Store a job record with analysis results in the local database.

## Signature

```
save_job(email, employer, employer_job_id, posted_date, salary_range_low, salary_range_high, description, pros, cons, resume_hints, score, recommendation) -> job_record
```

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| email | string | Yes | User's email address |
| employer | string | Yes | Employer name |
| employer_job_id | string | No | External job ID from the employer's system |
| posted_date | DateTime | No | When the job was posted |
| salary_range_low | decimal | No | Low end of salary range |
| salary_range_high | decimal | No | High end of salary range |
| description | string (markdown) | Yes | Job description as posted |
| pros | string (markdown) | Yes | Why the job fits the user |
| cons | string (markdown) | Yes | Potential concerns |
| resume_hints | string (markdown) | Yes | Actionable recommendations for resume tailoring |
| score | int (0-100) | Yes | Match score percentage |
| recommendation | string | Yes | One of "Apply", "Maybe", "Do not apply" |

## Returns

| Field | Type | Description |
|-------|------|-------------|
| InternalId | string (GUID) | Auto-generated unique identifier |
| Success | bool | Whether the save succeeded |
| ErrorMessage | string | Error details if Success is false |

## Behavior

- Auto-generates a unique InternalId (GUID) for each saved job
- All text fields must be provided in markdown format
- Sets IsDeleted to false by default
- If a duplicate is detected (same email + employer_job_id), the job is skipped and the existing InternalId is returned
- Score must be between 0 and 100

## Example Call

```json
{
  "email": "user@example.com",
  "employer": "Microsoft",
  "posted_date": "2026-05-15",
  "description": "## Senior Software Engineer...",
  "pros": "Strong match with cloud experience",
  "cons": "Requires Azure experience listed as preferred",
  "resume_hints": "Highlight Azure certifications",
  "score": 85,
  "recommendation": "Apply"
}
```