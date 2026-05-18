## Functional requirements (EARS format)

- [REQ-001] The system shall produce a long running containerized image as well as a commandline program
- [REQ-002] The system shall employ the [Micrsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp)
- [REQ-003] The system shall employ the latest LTS versio of the .net framework
- [REQ-004] The system shall periodically (every four hours) scrape the following web sites with a job search for 'software engineer' and 'software engineering manager'
  - governmentjobs.com
  - google (the search engine, not jobs at google)
  - glassdoor.com
  - monster.com
  - indeed.com
  - linkedin.com
- [REQ-005] The system shall support multiple users, keyed by their e-mail address
- [REQ-006] The system shall support the following per-user options in a json file
  - E-mail address
  - A list of job search keywords
  - A resume file
  - An optional career narrative file
- [REQ-007] The system shall maintain a sqlite database containing jobs found
- [REQ-008] For each job found, the system shall identify the top 10 technical skills sought by the employer
- [REQ-009] For each job found, the system shall identify the top 10 interpersonal skills sought by the employer
- [REQ-010] For each job found, the system shall identify overlap between the applicant's technical and interpersonal skills as expressed by their resume and narrative, and the skills sought by the employer
- [REQ-011] For each job found, the system shall store
  - The employer name
  - The posted date
  - A unique identifier
  - The job ID (if available)
  - The salary range (low and high number)
  - The job description as posted
  - The list of technical skills desired as identified as the previous step
  - The list of interpersonal skills desired as identified as the previous step
  - A suitability rating as a percentage
  - An optional date for the user to record they have applied for the job
  - A boolean, defaulting to false, that allows the user to discard a job when they do not plan to apply
- [REQ-012] When requested and every day at 5am, the system shall send an e-mail to each applicant containing a list of potential jobs, ordered by match percentage, highest to lowest
- [REQ-013] When sending out the job report email, each job shall contain a clickable link that allows the user to download a customized copy of their resume appropriate for the job offering

- [REQ-100] The system shall use a local OpenAI comatible LLM at http://192.168.1.21:8080/v1
- [REQ-101] The system shall select a suitable technology to search the web and retrieve web pages
- [REQ-101] The system shall select a suitable technology to create PDF pages to generate resumes

- [WEB-001] The system shall expose a simple web site allowing the user to browse the database of jobs found
- [WEB-002] The web site shall provide filtering by date
- [WEB-003] The web site shall provide filtering by suitability rating
- [WEB-004] The web site shall allow the user to mark a job as applied to with a date selector, defaulting to the current date
- [WEB-005] The web site shall allow the user to mark a job as discarded
- [WEB-006] The web site shall now show jobs which have been discarded


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
