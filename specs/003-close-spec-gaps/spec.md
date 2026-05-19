# Feature Specification: Close Specification Gaps

**Feature Branch**: `003-close-spec-gaps`

**Created**: 2026-05-18

**Status**: Draft

**Input**: User description: "Close any feature gaps between the product specification in ./spec/SPEC.md and the implemented code"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Implement Missing Job Search Engine Support (Priority: P1)

An administrator needs to configure the system to search multiple job websites using their specific URL templates as defined in the specification. The system supports dynamic configuration of job search engines instead of hardcoding URLs.

**Why this priority**: Without configurable job search engine templates, the system cannot support multiple job sites or adapt to URL changes. This is a critical gap between spec and implementation.

**Independent Test**: Can be fully tested by configuring job search engines via appsettings.json with different URL templates, then verifying the agent searches each site correctly.

**Acceptance Scenarios**:

1. **Given** the system has job search engine templates configured, **When** the agent starts a search, **Then** it uses the configured templates to build search URLs
2. **Given** a job search engine template is updated, **When** the agent runs, **Then** it uses the updated template without code changes
3. **Given** multiple job search engines are configured, **When** the agent searches, **Then** it searches all configured sites

---

### User Story 2 - Add Streaming Progress Reporting (Priority: P1)

A user runs the job search agent and wants to see real-time progress as the agent fetches and analyzes job listings. Progress messages are written to a log file using streaming responses.

**Why this priority**: Without streaming progress, users see no feedback during the search process, making it unclear if the system is working or hung. This is explicitly required by REQ-104.

**Independent Test**: Can be tested by running the agent with verbose logging enabled and verifying progress messages appear in the log file in real-time as jobs are fetched.

**Acceptance Scenarios**:

1. **Given** the agent is fetching job listings, **When** each page is retrieved, **Then** a progress message is logged
2. **Given** the agent is analyzing jobs, **When** each job is processed, **Then** a progress message is logged
3. **Given** the agent completes its search cycle, **When** all pages are processed, **Then** a summary message is logged

---

### User Story 3 - Implement Periodic Job Search Execution (Priority: P2)

A user wants the system to automatically search for new jobs at regular intervals without manual intervention. The system supports scheduled execution of the job search agent.

**Why this priority**: The specification requires periodic invocation (REQ-101), but the current implementation only runs once per invocation. Automatic scheduling improves usability.

**Independent Test**: Can be tested by configuring a schedule (e.g., every 30 minutes) and verifying the agent runs automatically at those intervals.

**Acceptance Scenarios**:

1. **Given** periodic execution is configured, **When** the scheduled time arrives, **Then** the agent runs automatically
2. **Given** multiple scheduled runs, **When** the system is running, **Then** each scheduled execution completes independently
3. **Given** a scheduled run fails, **When** the error occurs, **Then** the failure is logged and the next scheduled run is not affected

---

### User Story 4 - Match Agent Instructions to Specification (Priority: P1)

The AI agent's system prompt and behavior match exactly what is specified in the specification document. The agent uses the exact instructions from the spec.

**Why this priority**: If the agent behavior doesn't match the specification, results may be incorrect or inconsistent. This is a fundamental requirement for the feature to work as intended.

**Independent Test**: Can be tested by examining the agent's system prompt and verifying it matches the spec's "Agent instructions" section exactly.

**Acceptance Scenarios**:

1. **Given** the agent is invoked, **When** the agent starts, **Then** its system prompt matches the spec's agent instructions
2. **Given** the agent follows the tool plan, **When** the agent processes jobs, **Then** it follows the exact steps specified
3. **Given** the agent has processed 100 pages, **When** the limit is reached, **Then** it stops fetching as specified

---

### User Story 5 - Implement Missing Database Fields (Priority: P2)

The job database stores and tracks notification dates for job postings. The `date_notified` field is used to track when users were notified about specific job postings.

**Why this priority**: The field exists in the entity but is never used, making it useless. Implementing notification tracking enables future notification features.

**Independent Test**: Can be tested by marking a job as notified and verifying the `date_notified` field is set and queryable.

**Acceptance Scenarios**:

1. **Given** a job is marked as notified, **When** the notification is recorded, **Then** the `date_notified` field is set to the current timestamp
2. **Given** jobs have notification dates, **When** querying jobs by notification status, **Then** results are filtered correctly
3. **Given** the notification date is set, **When** the job is queried, **Then** the `date_notified` value is returned

---

### Edge Cases

- What happens when a job search engine URL template is malformed or invalid? The system should validate templates on startup and report errors before attempting searches.
- How does the system handle periodic execution when the LLM endpoint is unreachable? The system should log the error, skip the current cycle, and attempt the next scheduled execution.
- What happens when the log file cannot be written (permissions, disk full)? The system should log to console as fallback and report the error.
- How does the system handle multiple concurrent invocations (manual + scheduled)? The system should use file locking or similar mechanisms to prevent conflicts.
- What happens when the agent instructions are updated in the spec but not in the code? The system should include a version check and warn when spec and implementation are out of sync.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST support configurable job search engine templates defined in configuration (appsettings.json or similar)
- **FR-002**: Each job search engine template MUST support variable substitution for keywords and location
- **FR-003**: The system MUST validate job search engine templates on startup and report configuration errors
- **FR-004**: The system MUST use streaming responses (`GetStreamingResponseAsync`) for agent invocations
- **FR-005**: Progress messages from streaming MUST be written to a log file
- **FR-006**: The system MUST support scheduled/periodic execution of the job search agent
- **FR-007**: Scheduled execution MUST be configurable via configuration file
- **FR-008**: The system MUST support disabling periodic execution via configuration
- **FR-009**: The agent's system prompt MUST match the exact instructions from the specification document
- **FR-010**: The agent MUST use the exact tool plan specified in the specification
- **FR-011**: The agent MUST substitute `{{email}}` with the user's email address in prompts
- **FR-012**: The agent MUST substitute `{{query}}` with the job search query in prompts
- **FR-013**: The agent MUST use `GetStreamingResponseAsync` and stream progress messages to the log file
- **FR-014**: The `date_notified` field MUST be set when a job is marked as notified
- **FR-015**: The system MUST support querying jobs by notification status (notified/not notified)
- **FR-016**: The system MUST support querying jobs by `date_notified` date range
- **FR-017**: The system MUST validate email format for user email addresses
- **FR-018**: The system MUST validate that resume files exist and are readable before processing
- **FR-019**: The system MUST validate that career narrative files exist (if provided) and are readable
- **FR-020**: The system MUST configure HTTP timeouts for fetch operations to prevent indefinite hangs
- **FR-021**: The system MUST use async/await throughout to avoid blocking calls
- **FR-022**: The system MUST validate that text fields stored via `save_job` are in markdown format
- **FR-023**: The system MUST use exact recommendation values: "Apply", "Maybe", or "Do not apply"
- **FR-024**: The system MUST use file locking or similar mechanisms to prevent conflicts between manual and scheduled invocations

### Key Entities

- **JobSearchEngine**: A configurable job search engine with URL template, name, and priority. Supports variable substitution for keywords and location.
- **ScheduledExecution**: Configuration for periodic agent execution including interval, enabled status, and next scheduled time.
- **AgentInstructions**: The exact system prompt and tool plan from the specification, versioned for tracking.
- **NotificationRecord**: Tracks when a user was notified about a job posting, including the notification date and method.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All requirements from the specification (REQ-001 through REQ-104) are implemented with 100% compliance
- **SC-002**: Job search engine configuration can be updated without code changes or recompilation
- **SC-003**: Progress messages appear in the log file within 1 second of job processing
- **SC-004**: The agent completes a full search cycle with streaming progress in under 10 minutes for 100 pages
- **SC-005**: Scheduled execution runs automatically at configured intervals with no manual intervention
- **SC-006**: The agent's system prompt matches the specification exactly with zero deviations
- **SC-007**: All nullable reference warnings are resolved with zero compiler warnings
- **SC-008**: The `date_notified` field is properly used for at least 95% of job records
- **SC-009**: HTTP fetch operations timeout after 30 seconds instead of hanging indefinitely
- **SC-010**: The system can handle concurrent manual and scheduled invocations without conflicts

## Assumptions

- The configuration system (appsettings.json or similar) supports complex nested objects for job search engine templates
- The logging system supports file-based logging with configurable levels
- The scheduling system can run on the same schedule as the existing application lifecycle
- The file locking mechanism is available and performant for the expected usage patterns
- The LLM endpoint supports streaming responses via `GetStreamingResponseAsync`
- The specification document is the authoritative source for agent instructions
- Users have appropriate permissions to write to the log file location
- The database supports the `date_notified` field with proper indexing for queries
