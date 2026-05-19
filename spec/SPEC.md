## Functional requirements (EARS format)

- [REQ-001] The system shall produce a commandline program
- [REQ-002] The system shall employ the [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp)
- [REQ-003] The system shall employ the latest LTS versiom of the .net framework
- [REQ-004] The system shall support the job web sites listed in the [Job search engines] section of this document
- [REQ-005] The system shall support multiple users, keyed by their e-mail address
- [REQ-006] The system shall support the following per-user options as command line arguments
  - E-mail address
  - A list of job search keywords
  - A resume file
  - An optional career narrative file
- [REQ-007] The system shall maintain a sqlite database to manage jobs that have been found and analyzed
- [REQ-008] The system shall provide a [fetch] tool for the agent to fetch a web site or retrieve a web search
- [REQ-008b] When the fetch tool is invoked it shall remove html elements purely used for rendering such as fonts and font styles
- [REQ-009] The system shall provide a [save_job] tool for the agent to store a job
- [REQ-010] The system shall provide a [get_jobs] tool for the agent to search stored jobs by date, employer, job ID and web site the job was found at 
- [REQ-011] The [save_job] tool shall store
  - email: The user's email address
  - employer: The employer name
  - posted_date: The posted date
  - internal_id: A unique identifier (automatically generated, not an input argument)
  - employer_job_id The job ID (if available) (text)
  - salary_range_low: The low end of the salary range (if available)
  - salary_range_high: The low end of the salary range (if available)
  - desciption: The job description as posted (text)
  - pros: A field to incidate why it fits (text)
  - cons: A field to incidate potentical concerns (text)
  - resume_hints: Resume hints (text)
  - score: A match score as a percentage (number)
  - recommendation: An apply recommendation: Apply, Maybe, or Do not apply.
  - is_deleted: A boolean, defaulting to false, that allows the user to discard a job when they do not plan to apply
  - date_notified A date (nullable) to indicate when a notification was sent to the user for the job posting

- [REQ-100] The system shall use a local OpenAI comatible LLM at http://192.168.1.21:8080/v1
- [REQ-101] When the system is invoked, it shall periodically invoke the AI agent with the tools configured and the prompt listed in the [Agent instructions] section of this document
- [REQ-102] When the system invokes the AI agent, it shall substitute the {{email}} string with the user's email
- [REQ-103] When the system invokes the AI agent, it shall substitute the {{query}} string with the job search query

## Job search engines

| Site           | Query template                                       |
| -------------- | ---------------------------------------------------- |
| indeed.com     | https://www.indeed.com/jobs?l=Seattle&q=(keywords)   |

## Agent instructions

```
You are Fidalgo JobSearch AI, a focused job-search agent.

Tool plan:
- Call the fetch tool with the {{query}} argument
- Make subsequent fetch calls to read individual job descriptions
- When a job posting is encountered, use the get_jobs tool to identify if the job is already in the database
- When a job is not already in the database, compare the job description to the user's resume and career narrative
- When a job is not already in the database, identify a match score percentage.
- When a job is not already in the database, use the save_job tool to create a permanent local record of the job posting
- When using the save_job tool, all text files must be in markdown format
- When using the save_job tool, create compact but actionable recommendations how the user can reshape their resume to match this job posting in the resume_hints field
- After reading up to 100 pages, stop using fetch tool
- Search again only if the first search returns zero usable jobs.
- Avoid broad search pages, expired jobs, and LinkedIn unless no better source exists.

