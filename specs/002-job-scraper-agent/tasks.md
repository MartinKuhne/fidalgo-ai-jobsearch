---
description: "Task list for AI-Powered Job Scraper Agent implementation"
---

# Tasks: AI-Powered Job Scraper Agent

**Input**: Design documents from `/specs/002-job-scraper-agent/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to
- Include exact file paths in descriptions

## Path Conventions

- **Single project**: `src/`, `tests/` at repository root
- All paths are relative to repository root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Update project configuration and add new dependencies

- [X] T001 Update Fidalgo.Agent.csproj from Web SDK to Console SDK, add Microsoft.Agents and Microsoft.Extensions.AI.OpenAI packages in src/Fidalgo.Agent/Fidalgo.Agent.csproj
- [X] T002 [P] Update Fidalgo.Agent.Tests.csproj with project reference in src/Fidalgo.Agent.Tests/Fidalgo.Agent.Tests.csproj
- [X] T003 Configure appsettings.json with LLM endpoint, model name, and database path in src/Fidalgo.Agent/appsettings.json

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 [P] Create Job entity matching data-model.md (InternalId, Email, Employer, EmployerJobId, PostedDate, SalaryRangeLow, SalaryRangeHigh, Description, Pros, Cons, ResumeHints, Score, Recommendation, IsDeleted, DateNotified, SourceWebsite) in src/Fidalgo.Agent/Storage/JobEntity.cs
- [X] T005 [P] Update JobDbContext to replace JobPosting/ScrapeResult/SearchConfiguration DbSets with single Job DbSet in src/Fidalgo.Agent/Storage/JobDbContext.cs
- [X] T006 [P] Update JobRepository to expose methods matching the new Job entity (save, query by filters, soft-delete) in src/Fidalgo.Agent/Storage/JobRepository.cs
- [X] T007 [P] Create CliOptions record for command-line argument parsing (--email, --keywords, --resume, --narrative, --query-jobs) in src/Fidalgo.Agent/Configuration/CliOptions.cs
- [X] T008 [P] Create LLM client configuration service wired to appsettings.json endpoint in src/Fidalgo.Agent/Configuration/LlmConfiguration.cs
- [X] T009 [P] Create AgentPrompt template with {{email}} and {{query}} placeholders in src/Fidalgo.Agent/Prompts/AgentPrompt.cs
- [X] T010 [P] Create HtmlSanitizer for stripping rendering-only HTML elements (font, style, script, SVG, inline styles) using AngleSharp in src/Fidalgo.Agent/Sanitization/HtmlSanitizer.cs
- [X] T011 Update ServiceCollectionExtensions to register new services (JobRepository, LLM client, HtmlSanitizer, CliOptions) and remove old registrations in src/Fidalgo.Agent/DependencyInjection/ServiceCollectionExtensions.cs

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Run Automated Job Search (Priority: P1) MVP

**Goal**: User provides email, keywords, and resume via CLI; the tool fetches Indeed.com listings, analyzes them via LLM, and persists results with scores and recommendations.

**Independent Test**: Provide a resume file and keywords, run the tool, and confirm job listings are retrieved, analyzed, and persisted with scores (0-100) and recommendations (Apply/Maybe/Do not apply).

### Implementation for User Story 1

- [X] T012 [P] [US1] Create FetchTool class that uses HttpClient and HtmlSanitizer to fetch and clean Indeed.com page content in src/Fidalgo.Agent/Tools/FetchTool.cs
- [X] T013 [P] [US1] Create SaveJobTool class that uses JobRepository to persist job records after AI analysis in src/Fidalgo.Agent/Tools/SaveJobTool.cs
- [X] T014 [US1] Create JobSearchAgent class using Microsoft Agent Framework AIAgent with registered fetch and save_job tools in src/Fidalgo.Agent/Agents/JobSearchAgent.cs
- [X] T015 [US1] Implement agent prompt with {{email}} and {{query}} substitution and Indeed URL template in src/Fidalgo.Agent/Prompts/AgentPrompt.cs
- [X] T016 [US1] Rewrite Program.cs as CLI entry point that parses args, validates inputs, creates IChatClient to local LLM, builds AIAgent, and runs the agent in src/Fidalgo.Agent/Program.cs
- [X] T017 [US1] Implement input validation (email format, resume file existence, keywords non-empty) in src/Fidalgo.Agent/Program.cs
- [X] T018 [US1] Implement 100-page fetch limit enforcement in the agent prompt or tool logic in src/Fidalgo.Agent/Agents/JobSearchAgent.cs
- [X] T019 [US1] Implement empty-result retry (one re-search if first search returns zero jobs) in src/Fidalgo.Agent/Agents/JobSearchAgent.cs
- [X] T020 [US1] Add logging throughout US1 flow (search start, jobs found, analysis complete, errors) in src/Fidalgo.Agent/Logging/

**Checkpoint**: User Story 1 fully functional - tool can fetch, analyze, and store jobs with scores and recommendations

---

## Phase 4: User Story 2 - Review Stored Job Results (Priority: P2)

**Goal**: User queries stored jobs by date, employer, employer_job_id, and source website; results are returned with all analysis fields.

**Independent Test**: Query stored jobs after a search run and verify filtered results are returned for each supported criterion (employer, date range, source website).

### Implementation for User Story 2

- [X] T021 [P] [US2] Create GetJobsTool class that uses JobRepository to query stored jobs with filter parameters in src/Fidalgo.Agent/Tools/GetJobsTool.cs
- [X] T022 [US2] Register GetJobsTool with the AIAgent in the agent setup in src/Fidalgo.Agent/Agents/JobSearchAgent.cs
- [X] T023 [US2] Implement --query-jobs CLI flag that runs get_jobs tool directly (bypassing search) and prints results in src/Fidalgo.Agent/Program.cs
- [X] T024 [US2] Add CLI filters (--employer, --date-from, --date-to, --source-website) for query mode in src/Fidalgo.Agent/Configuration/CliOptions.cs

**Checkpoint**: User Story 2 functional - users can retrieve and filter stored jobs

---

## Phase 5: User Story 3 - Discard Irrelevant Jobs (Priority: P3)

**Goal**: User marks a job as discarded so it no longer appears in active queries.

**Independent Test**: Mark a job as deleted via discard operation and verify it no longer appears in active job queries.

### Implementation for User Story 3

- [X] T025 [P] [US3] Add soft-delete support to JobRepository (set IsDeleted=true by InternalId) in src/Fidalgo.Agent/Storage/JobRepository.cs
- [X] T026 [P] [US3] Implement --discard-job CLI flag that takes InternalId and calls soft-delete in src/Fidalgo.Agent/Program.cs
- [X] T027 [US3] Ensure GetJobsTool filters out IsDeleted=true jobs by default in src/Fidalgo.Agent/Tools/GetJobsTool.cs
- [X] T028 [US3] Implement --list-discarded CLI flag to retrieve discarded jobs in src/Fidalgo.Agent/Program.cs

**Checkpoint**: All user stories functional

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T029 [P] Add comprehensive error handling for LLM endpoint unreachable, DB locked, network failures in src/Fidalgo.Agent/
- [ ] T030 [P] Add logging throughout the application (search lifecycle, tool invocations, errors) in src/Fidalgo.Agent/Logging/
- [ ] T031 Run quickstart.md validation to verify all CLI flags work end-to-end
- [ ] T032 [P] Clean up unused files from 001 implementation (old scrapers, config command handler, obsolete models) in src/Fidalgo.Agent/

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-5)**: All depend on Foundational phase completion
  - US1 (P1) first → US2 (P2) depends on stored data from US1 → US3 (P3) depends on US2 query infrastructure
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Needs saved jobs from US1 to test querying
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - Needs query infrastructure from US2

### Within Each User Story

- Models before services
- Tools before agent integration
- Core implementation before CLI integration
- Story complete before moving to next priority

### Parallel Opportunities

- T004-T010 in Phase 2 can all run in parallel (different files, independent)
- T012-T013 in Phase 3 can run in parallel
- T025-T026 in Phase 5 can run in parallel

---

## Parallel Example: User Story 1

```bash
# Launch all tool implementations together:
Task: "Create FetchTool in src/Fidalgo.Agent/Tools/FetchTool.cs"
Task: "Create SaveJobTool in src/Fidalgo.Agent/Tools/SaveJobTool.cs"

# Then (after tools done):
Task: "Create JobSearchAgent in src/Fidalgo.Agent/Agents/JobSearchAgent.cs"
Task: "Rewrite Program.cs in src/Fidalgo.Agent/Program.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Run end-to-end test with test resume and keywords
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test with: `--email test@test.com --keywords "software engineer" --resume ./test-resume.md` → Verify jobs stored with scores and recommendations (MVP!)
3. Add User Story 2 → Test with: `--email test@test.com --query-jobs --employer "Microsoft"` → Jobs returned filtered
4. Add User Story 3 → Test with: `--email test@test.com --discard-job <internalId>` → Verify excluded from active queries

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence