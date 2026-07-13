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
- [REQ-008c] When the fetch tool is invoked and it detects the "Sign in" text in the page <title>, it shall wait for 120 seconds for a second page load to allow the user to sign in
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
  - title: The job title
  - desciption: The job description as posted (text)
  - pros: A field to incidate why it fits (text)
  - cons: A field to incidate potentical concerns (text)
  - resume_hints: Resume hints (text)
  - score: A match score as a percentage (number)
  - recommendation: An apply recommendation: Apply, Maybe, or Do not apply.
  - is_deleted: A boolean, defaulting to false, that allows the user to discard a job when they do not plan to apply
  - date_notified A date (nullable) to indicate when a notification was sent to the user for the job posting

- [REQ-100] The system shall use a local OpenAI comatible LLM at http://192.168.1.21:8080/v1 with OpenAIClient
- [REQ-101] When the system is invoked, it shall periodically invoke the AI agent with the tools configured and the prompt listed in the [Agent instructions] section of this document
- [REQ-102] When the system invokes the AI agent, it shall substitute the {{email}} string with the user's email
- [REQ-103] When the system invokes the AI agent, it shall substitute the {{query}} string with the job search query
- [REQ-104] The AI agent shall use GetStreamingResponseAsync and print progress messages to the log file
- [REQ-105] The AI agent shall use 'u-mkuhne' as the Api Key as no api key is needed for the local LLM

- [WEB-001] The system shall expose a simple web site allowing the user to browse the database of jobs found
- [WEB-002] The webs site shall display a drop-down that contains all the tenant e-mail addresses.
- [WEB-003] When the user selects an e-mail from the drop-down, the system shall display a list of jobs for that tenant
- [WEB-004] The list of jobs shall allow the user to filter by the date the job was found
- [WEB-005] The list of jobs shall be ordered by suitability rating (descending) by default
- [WEB-006] The list of jobs shall show 20 entries and a time and allow paging
- [WEB-007] The list of jobs shall display a trashcan icon with every entry. When the user clicks on the icon, the system shall set the is_deleted field to true
- [WEB-008] The list of jobs shall not display jobs where the is_deleted field is true
- [WEB-010] When the user clicks on a job title in the jobs list, the system shall display a modal dialog containing all the fields of the job
- [WEB-011] The job modal shall display a delete button that functions the same as the delete button in the jobs list
- [WEB-100] The web site shall use Blazor as a technology foundation
- [WEB-101] The web site and the job search agent shall use a shared library containing the db context
- [WEB-102] The web site project shall be named Fidalgo.Web

## Web site layout

- Top Navbar
- Jobs table

| Score | Employer | Date | Title | Recommendation | Actions |
| ----- | -------- | ---- | ----- | -------------- | ------- |

## Job search engines

| Site           | Query template                                       |
| -------------- | ---------------------------------------------------- |
| indeed.com     | https://www.indeed.com/jobs?l=Seattle&q=(keywords)   |


