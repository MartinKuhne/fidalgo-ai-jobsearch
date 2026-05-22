# Feature Specification: Job Browse Web Site

**Feature Branch**: `006-job-browse-web`

**Created**: 2026-05-22

**Status**: Draft

**Input**: User description: "The system shall expose a simple web site allowing the user to browse the database of jobs found. The web site shall display a drop-down that contains all the tenant e-mail addresses. When the user selects an e-mail from the drop-down, the system shall display a list of jobs for that tenant. The list of jobs shall allow the user to filter by the date the job was found. The list of jobs shall be ordered by suitability rating (descending) by default. The list of jobs shall show 20 entries and allow paging. The list of jobs shall display a trashcan icon with every entry. When the user clicks on the icon, the system shall set the is_deleted field to true. The list of jobs shall not display jobs where the is_deleted field is true. When the user clicks on a job title in the jobs list, the system shall display a modal dialog containing all the fields of the job. The job modal shall display a delete button that functions the same as the delete button in the jobs list."

## User Scenarios & Testing

### User Story 1 - Browse and filter jobs by tenant (Priority: P1)

A job seeker logs into the web site to review jobs that have been discovered and scored for their email address. They select their email from a drop-down, see a paginated list of their jobs ordered by suitability, and can filter by date.

**Why this priority**: This is the core value proposition - without being able to view and filter jobs, the system provides no user value. This story delivers the MVP.

**Independent Test**: A user can select their email, see a filtered and sorted list of non-deleted jobs with pagination, and verify the results match expectations.

**Acceptance Scenarios**:

1. **Given** the user has opened the web site, **When** they open the tenant drop-down, **Then** the drop-down displays all email addresses that have associated jobs in the system
2. **Given** the user has selected an email from the drop-down, **When** the page loads, **Then** the system displays a list of non-deleted jobs for that tenant, ordered by suitability rating descending, showing 20 entries per page
3. **Given** the user is viewing a list of jobs, **When** they apply a date filter, **Then** the system updates the list to show only jobs found on or after the selected date
4. **Given** the user is viewing a paginated list, **When** they navigate to another page, **Then** the system displays the next 20 jobs while preserving the current filter and sort order

### User Story 2 - Review job details in a modal (Priority: P2)

A job seeker finds an interesting job in the list and wants to see all the details (description, pros, cons, recommendations) without leaving the list view.

**Why this priority**: Users need to evaluate jobs quickly without excessive navigation. The modal provides a fast way to review details while keeping context.

**Independent Test**: A user can click any job title in the list and see a modal with all job fields displayed.

**Acceptance Scenarios**:

1. **Given** the user is viewing the jobs list, **When** they click on a job title, **Then** a modal dialog opens displaying all fields of that job including score, employer, title, description, pros, cons, resume hints, recommendation, and source
2. **Given** the modal is open, **When** the user clicks outside the modal or closes it, **Then** the modal closes and the user returns to the jobs list with the same filter and page state

### User Story 3 - Delete jobs (Priority: P3)

A job seeker wants to remove jobs they are not interested in. Deleted jobs should be hidden from the list but remain recoverable if needed.

**Why this priority**: Users need a way to curate their job list and focus on relevant opportunities. Soft-delete preserves data for potential future analysis.

**Independent Test**: A user can delete a job from the list or from the modal, and the job disappears from all views.

**Acceptance Scenarios**:

1. **Given** the user is viewing the jobs list, **When** they click the trashcan icon on a job entry, **Then** the system marks the job as deleted and removes it from the current list
2. **Given** the user has deleted a job, **When** they refresh or navigate away and back, **Then** the deleted job does not appear in any job list view
3. **Given** the user has a job details modal open, **When** they click the delete button in the modal, **Then** the system marks the job as deleted, closes the modal, and the jobs list updates to reflect the deletion

---

### Edge Cases

- What happens when a tenant has no jobs? The system shall display an empty state message indicating no jobs are available for the selected email.
- What happens when filtering results in zero jobs? The system shall display a message indicating no jobs match the selected date filter.
- What happens when the user selects an email that has no jobs yet? The system shall display an empty state message.
- How does the system handle rapid successive delete actions? The system shall process each delete request sequentially and prevent duplicate deletions.

## Requirements

### Functional Requirements

- **FR-001**: Users MUST be able to select their email address from a drop-down to scope their job view
- **FR-002**: The system MUST display a list of jobs for the selected tenant, ordered by suitability rating in descending order
- **FR-003**: Users MUST be able to filter the jobs list by the date the job was found
- **FR-004**: The system MUST display 20 job entries per page and provide pagination controls
- **FR-005**: Users MUST be able to delete jobs from the list, which marks them as deleted and removes them from all views
- **FR-006**: Users MUST be able to click a job title to view all job details in a modal dialog
- **FR-007**: The system MUST NOT display jobs that have been marked as deleted
- **FR-008**: Users MUST be able to delete jobs from within the job details modal
- **FR-009**: The system MUST display the following information for each job in the list: suitability score, employer name, date found, job title, and recommendation
- **FR-010**: The job details modal MUST display all job fields: score, employer, title, posted date, description, pros, cons, resume hints, recommendation, source website, and date found

### Key Entities

- **Job**: Represents a discovered and analyzed job posting. Key attributes: internal ID, associated email (tenant), employer name, job title, posted date, salary range, description, pros, cons, resume hints, suitability score (0-100), recommendation (Apply/Maybe/Do not apply), soft-delete flag, date found, and source website. A job belongs to exactly one tenant identified by email.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Users can select their email and view their job list within 2 seconds of page load
- **SC-002**: 95% of users can successfully filter and browse their jobs on the first attempt without error
- **SC-003**: Users can view details of any job from the list with no more than 2 clicks
- **SC-004**: Deleted jobs are immediately removed from all list views and do not reappear after pagination or filtering changes

## Assumptions

- Users are identified solely by their email address; no separate login/authentication flow is required for v1
- Jobs are already populated in the database by the job scraper agent; this web site only reads and soft-deletes existing records
- The suitability score is pre-computed by the job scoring agent and stored as an integer from 0 to 100
- The date a job was "found" is stored as a timestamp when the job was first saved to the database
- "Tenant" refers to a user identified by their email address
- Deleted jobs are soft-deleted (marked with a flag) rather than hard-deleted from the database
- The web site is intended for internal use by job seekers who have jobs in the system
