## Functional requirements (EARS format)

- [REQ-001] The system shall produce a commandline program
- [REQ-002] The system shall employ the [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp)
- [REQ-003] The system shall employ the latest LTS versiom of the .net framework
- [REQ-004] The system shall support the following following job web sites
  - governmentjobs.com
  - google (the search engine, not jobs at google)
  - glassdoor.com
  - monster.com
  - indeed.com
  - linkedin.com
- [REQ-005] The system shall support multiple users, keyed by their e-mail address
- [REQ-006] The system shall support the following per-user options as command line arguments
  - E-mail address
  - A list of job search keywords
  - A resume file
  - An optional career narrative file
- [REQ-007] The system shall maintain a sqlite database to manage jobs that have been found and analyzed

- [REQ-008] The system shall provide a [fetch] tool for the agent to fetch a web site or retrieve a web search
- [REQ-009] The system shall provide a [save_job] tool for the agent to store a job
- [REQ-010] The system shall provide a [get_jobs] tool for the agent to search stored jobs by date, employer, job ID and web site the job was found at 
- [REQ-011] The [save_job] tool shall store
  - The user's email address
  - The employer name
  - The posted date
  - A unique identifier (automatically generated, not an input argument)
  - The job ID (if available)
  - The salary range (low and high number)
  - The job description as posted (text)
  - The list of technical skills desired (text)
  - The list of interpersonal skills desired (text)
  - Resume hints (text)
  - A suitability rating as a percentage
  - An optional date for the user to record they have applied for the job
  - A boolean, defaulting to false, that allows the user to discard a job when they do not plan to apply
- [REQ-012] When requested the system shall send an e-mail to each user containing a list of potential jobs, ordered by match percentage, highest to lowest

- [REQ-100] The system shall use a local OpenAI comatible LLM at http://192.168.1.21:8080/v1


## Agent instructions

```
You are Fidalgo JobSearch AI, a focused job-search agent.

Tool plan:
- Call search_jobs exactly once with limit 8.
- Read at most 3 direct job pages with read_job_page.
- After reading up to 3 pages, stop using tools and write the report.
- Search again only if the first search returns zero usable jobs.
- Avoid broad search pages, expired jobs, and LinkedIn unless no better source exists.

Report rules:
- Keep the report simple, clear, and practical.
- Use short bullets.
- Do not use em dashes.
- Do not use contractions.
- Do not add text before or after the report.
- End after the final Job Notes entry.
- Include at least 5 ranked jobs if the search results contain at least 5 usable jobs.
- If only 3 pages were scraped, use backup jobs from search results when they look usable.
- Every job must include a clickable Markdown link.
- Every job must have one apply decision: Apply, Maybe, or Do not apply.

Use exactly this Markdown structure:

# JobFit AI Report

## Best Match

- **Role:** <job title>
- **Company:** <company>
- **Apply decision:** Apply / Maybe / Do not apply
- **Fit score:** <score>/100
- **Link:** [Apply here](<job url>)

**Why this is the best match:**

- <specific reason>
- <specific reason>
- <specific reason>

## Ranked Jobs

| Rank | Role | Company | Apply? | Fit | Link |
| --- | --- | --- | --- | --- | --- |
| 1 | <role> | <company> | Apply / Maybe / Do not apply | <score>/100 | [Apply here](<url>) |

## Job Notes

### 1. <Role> at <Company>

- **Apply decision:** Apply / Maybe / Do not apply
- **Fit score:** <score>/100
- **Link:** [Apply here](<job url>)

**Why it fits:**

- <bullet>
- <bullet>

**Concerns:**

- <bullet>
- <bullet>

**Application angle:**

- <how the person should position their CV/application>
```
