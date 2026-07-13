# Entity Relationship Diagram

```mermaid
erDiagram
    USER ||--o{ JOB : "finds"
    USER ||--|| USER_PREFERENCE : "has"
    USER_PREFERENCE }o--|| USER : "belongs to"
    JOB ||--o{ JOB_TECHNICAL_SKILL : "has"
    JOB ||--o{ JOB_INTERPERSONAL_SKILL : "has"

    USER {
        string email PK
        string smtp_host
        int smtp_port
        string smtp_username
        string smtp_password
        bool smtp_use_ssl
    }

    USER_PREFERENCE {
        string email FK PK
        string search_keywords
        string resume_file_path
        string? career_narrative_path
    }

    JOB {
        string id PK
        string email FK
        string employer_name
        datetime posted_date
        string? external_job_id
        decimal? salary_low
        decimal? salary_high
        string job_description
        decimal suitability_rating
        datetime? applied_date
        bool discarded
        datetime created_at
        datetime updated_at
    }

    JOB_TECHNICAL_SKILL {
        int id PK
        string job_id FK
        string skill_name
        int relevance_rank
    }

    JOB_INTERPERSONAL_SKILL {
        int id PK
        string job_id FK
        string skill_name
        int relevance_rank
    }
```

## Entity Descriptions

### `USER`
Represents a system user, keyed by e-mail address (REQ-005). Stores SMTP configuration for outbound email delivery (REQ-012).

### `USER_PREFERENCE`
Per-user JSON-derived configuration: search keywords, resume file path, and optional career narrative file path (REQ-006). One-to-one with `USER`.

### `JOB`
A job posting discovered during scraping. Contains employer name, posted date, external job ID, salary range, full description, suitability rating, applied/discarded flags (REQ-011). Linked to the user who owns it.

### `JOB_TECHNICAL_SKILL`
One row per technical skill extracted from a job description (REQ-008). Ordered by relevance rank (1–10).

### `JOB_INTERPERSONAL_SKILL`
One row per interpersonal skill extracted from a job description (REQ-009). Ordered by relevance rank (1–10).
