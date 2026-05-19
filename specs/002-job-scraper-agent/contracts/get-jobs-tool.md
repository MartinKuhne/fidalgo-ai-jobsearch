# Contract: Get Jobs Tool

**Purpose**: Retrieve stored jobs using various filter criteria.

## Signature

```
get_jobs(email, [date_from], [date_to], [employer], [employer_job_id], [source_website]) -> job[]
```

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| email | string | Yes | User's email address to scope the search |
| date_from | DateTime | No | Filter jobs posted on or after this date |
| date_to | DateTime | No | Filter jobs posted on or before this date |
| employer | string | No | Filter by employer name (partial match) |
| employer_job_id | string | No | Filter by external job ID |
| source_website | string | No | Filter by source website name |

## Returns

| Field | Type | Description |
|-------|------|-------------|
| Jobs | Job[] | Array of matching job records |
| Success | bool | Whether the query succeeded |
| ErrorMessage | string | Error details if Success is false |
| TotalCount | int | Number of matching records |

## Job Record Structure

Each returned job includes all fields stored by save_job_tool.

## Filter Behavior

- All filter parameters are optional and can be combined
- When multiple filters are specified, they are combined with AND logic
- When no filters are specified (only email), all non-deleted jobs are returned
- Jobs with IsDeleted=true are excluded by default

## Example Call

```json
{
  "email": "user@example.com",
  "employer": "Microsoft",
  "source_website": "indeed.com"
}
```