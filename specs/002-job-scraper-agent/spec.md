# Feature Specification: AI-Powered Job Scraper Agent

**Feature Branch**: `002-job-scraper-agent`

**Created**: 2026-05-18

**Status**: Draft

**Input**: User description: "AI-powered job scraper agent using Microsoft Agent Framework that searches Indeed.com for jobs matching user keywords, analyzes them against a resume using a local LLM, and stores results in a database."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run Automated Job Search (Priority: P1)

A user configures their job search preferences (email, keywords, resume) and runs the command-line tool. The tool automatically fetches job listings from Indeed.com, analyzes each against their resume using AI, and stores the results with match scores and recommendations.

**Why this priority**: This is the core value proposition - automated job discovery and analysis. Without this, the system provides no value.

**Independent Test**: Can be fully tested by providing a resume file and keywords, running the tool, and confirming job listings are retrieved, analyzed, and persisted with scores and recommendations.

**Acceptance Scenarios**:

1. **Given** a user has provided a valid email, keywords, and resume file, **When** the tool is invoked, **Then** the tool fetches job listings from Indeed.com matching those keywords
2. **Given** job listings are fetched, **When** the tool analyzes each listing against the user's resume and career narrative, **Then** each job is stored with a match score, pros/cons, and an apply recommendation (Apply, Maybe, or Do not apply)
3. **Given** a duplicate job posting is encountered, **When** the tool checks the database, **Then** it skips saving the duplicate
4. **Given** the agent has processed up to 100 job pages, **When** the page limit is reached, **Then** the agent stops fetching and completes its analysis cycle

---

### User Story 2 - Review Stored Job Results (Priority: P2)

A user wants to browse previously discovered jobs, filter by criteria (date, employer, job ID, source website), and review the AI's match analysis to decide which positions to pursue.

**Why this priority**: After jobs are collected, users need to act on the insights. This surfaces the stored data for decision-making.

**Independent Test**: Can be tested by querying stored jobs after a search run and verifying results are returned filtered by each supported criterion.

**Acceptance Scenarios**:

1. **Given** jobs have been saved from previous searches, **When** a user queries stored jobs by employer name, **Then** only jobs from that employer are returned
2. **Given** jobs have been saved from previous searches, **When** a user queries stored jobs by date range, **Then** only jobs within that range are returned
3. **Given** jobs have been saved from previous searches, **When** a user queries stored jobs by source website, **Then** only jobs from that website are returned
4. **Given** a job has been marked as deleted, **When** querying for active jobs, **Then** deleted jobs are excluded from results

---

### User Story 3 - Discard Irrelevant Jobs (Priority: P3)

A user reviews their saved jobs and decides a particular posting is not worth pursuing. They mark it as discarded so it no longer appears in active searches.

**Why this priority**: Enables users to curate their job list and focus on promising opportunities.

**Independent Test**: Can be tested by marking a job as deleted and verifying it no longer appears in active job queries.

**Acceptance Scenarios**:

1. **Given** a saved job is no longer of interest, **When** the user discards it, **Then** the job is marked with is_deleted=true
2. **Given** a job is marked as deleted, **When** querying for active job listings, **Then** the deleted job is not included in results

---

### Edge Cases

- What happens when the LLM endpoint at http://192.168.1.21:8080/v1 is unreachable or returns errors? The tool should report the failure gracefully and exit with a meaningful error message.
- How does the system handle Indeed.com returning no results for a keyword search? The tool should log the empty result and allow the agent to attempt a second search.
- What happens when the resume file cannot be read (missing, corrupted, wrong format)? The tool should validate the file exists and is readable before invoking the agent.
- How does the system handle duplicate email registration (same email used across concurrent runs)? Jobs should be keyed by email + employer_job_id to prevent duplicates.
- What happens when the SQLite database file is locked or corrupted? The tool should report the error and exit without losing previously saved data.
- How does the tool handle invalid command-line arguments (malformed email, missing required files)? It should validate all inputs before starting the agent.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The application MUST accept command-line arguments including email address, job search keywords, resume file path, and an optional career narrative file path
- **FR-002**: The application MUST validate that the resume file exists and is readable before proceeding
- **FR-003**: The application MUST validate that the email address is in a valid email format
- **FR-004**: The application MUST support multiple users, uniquely identified by their email address
- **FR-005**: The application MUST store job data in a local database, with jobs keyed to the user's email address
- **FR-006**: The application MUST provide a fetch tool that retrieves web page content from Indeed.com job search URLs
- **FR-007**: The fetch tool MUST strip HTML rendering elements (fonts, font styles, and similar presentational markup) from retrieved content
- **FR-008**: The application MUST provide a save_job tool that stores job records with all specified fields (employer, posted_date, description, pros, cons, resume_hints, score, recommendation, salary range, employer_job_id, is_deleted, date_notified)
- **FR-009**: The save_job tool MUST auto-generate a unique internal_id for each saved job record
- **FR-010**: The save_job tool MUST store text fields (description, pros, cons, resume_hints) in markdown format
- **FR-011**: The application MUST provide a get_jobs tool that retrieves stored jobs filterable by date, employer, employer_job_id, and source website
- **FR-012**: The application MUST connect to a local AI language model endpoint for AI agent operations
- **FR-013**: When invoked, the application MUST substitute {{email}} with the user's email address in the agent prompt
- **FR-014**: When invoked, the application MUST substitute {{query}} with the user's job search query in the agent prompt and search URL
- **FR-015**: The agent MUST limit its fetch operations to a maximum of 100 pages per invocation
- **FR-016**: The agent MUST search again only if the first search returns zero usable job listings
- **FR-017**: The save_job tool MUST store a score field as a percentage (0-100) representing the match quality
- **FR-018**: The save_job tool MUST store a recommendation field with one of three values: Apply, Maybe, or Do not apply
- **FR-019**: The save_job tool MUST default the is_deleted field to false when creating a new job record
- **FR-020**: The save_job tool MUST support a nullable date_notified field to track when a notification was sent for a job posting

### Key Entities

- **User**: A job seeker identified by their email address. Has associated configuration: search keywords, resume file, and optional career narrative file.
- **Job**: A job posting discovered by the AI agent. Contains employer info, posting details, salary data, the AI's match analysis (score, pros, cons, resume hints), an apply recommendation, and lifecycle flags (is_deleted, date_notified).
- **JobSearchConfiguration**: The per-user search parameters including keywords, resume path, career narrative path, and email address. Provided via command-line arguments.
- **AgentContext**: The runtime state of the AI agent including the configured tools (fetch, save_job, get_jobs), the system prompt, and the template-substituted values ({{email}}, {{query}}).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can discover and analyze job listings automatically by providing keywords and a resume file, with zero manual browsing required
- **SC-002**: Each discovered job is analyzed and persisted within 30 seconds of being fetched
- **SC-003**: The system correctly identifies and skips duplicate job postings (same employer + job ID) across multiple search runs
- **SC-004**: Saved jobs can be retrieved by date, employer, job ID, and source website within 2 seconds
- **SC-005**: The agent completes a full search cycle (fetch listings, analyze, save) without exceeding 100 page fetches
- **SC-006**: Incomplete or corrupt AI responses do not corrupt the job database - partial results are rejected
- **SC-007**: Invalid command-line arguments produce clear, actionable error messages within 1 second
- **SC-008**: The system successfully processes searches for at least 3 different users with distinct keywords and resumes in separate invocations

## Assumptions

- The target AI language model endpoint is available and responsive when the tool is invoked (expected at a configurable local network address)
- The local database system is available and supports concurrent read/write operations
- The user has network connectivity to reach both job search websites and the local AI endpoint
- Resume and career narrative files are in plain text or markdown format that the AI agent can process
- The Indeed.com URL format (template with location and keywords) remains stable during the tool's operation
- Job search location is assumed to be Seattle (as configured in the Indeed URL template) - this is not configurable via command-line arguments in v1
- The tool is intended for single-user-per-invocation usage; concurrent invocations by different users are handled via separate runs
- Data retention follows typical practice for personal job search tools - data is kept indefinitely until explicitly deleted by the user
- The "periodically invoke" in the agent instructions is interpreted as a single invocation cycle per program run (fetch up to 100 pages once), not continuous re-invocation