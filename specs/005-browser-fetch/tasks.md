# Tasks: Browser Fetch Tool

**Input**: Design documents from `/specs/005-browser-fetch/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are included as part of implementation per constitutional requirement (NFR-002: Testing mandatory).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Single project**: `src/`, `tests/` at repository root
- Paths shown below assume single project structure per plan.md

---

## Phase 1: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T001 [P] Create `src/Fidalgo.Agent/Tools/IBrowserFetchTool.cs` with interface definition
- [ ] T002 [P] Create `src/Fidalgo.Agent/Models/FetchRequest.cs` record
- [ ] T003 [P] Create `src/Fidalgo.Agent/Models/FetchResult.cs` record
- [ ] T004 [P] Create `src/Fidalgo.Agent/Models/BrowserConfiguration.cs` record
- [ ] T005 [P] Add `Microsoft.Playwright` NuGet package to `src/Fidalgo.Agent/Fidalgo.Agent.csproj`
- [ ] T006 [US1] Update `src/Fidalgo.Agent/DependencyInjection/ServiceCollectionExtensions.cs` to register browser fetch services
- [ ] T007 [US1] Create `src/Fidalgo.Agent/Tools/BrowserFetchTool.cs` with basic fetch implementation
- [ ] T008 [US1] Implement browser launch and cleanup using Playwright `using` pattern
- [ ] T009 [US1] Add error handling for invalid URLs and unreachable sites
- [ ] T010 [US1] Add tracing spans for browser lifecycle (launch, navigation, close)
- [ ] T011 [US1] Add logging with TraceID and CorrelationID propagation

**Checkpoint**: Foundational phase ready - user story implementation can now begin in parallel

---

## Phase 2: User Story 1 - Fetch Web Page Content (Priority: P1) 🎯 MVP

**Goal**: Implement basic web page fetching using Playwright Firefox browser

**Independent Test**: Provide a URL and verify the tool returns complete HTML content of the rendered page

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T012 [P] [US1] Create contract test `tests/contract/BrowserFetchToolTests.cs` for successful fetch
- [ ] T013 [P] [US1] Create contract test for invalid URL error handling
- [ ] T014 [P] [US1] Create integration test `tests/integration/FetchWebPageIntegrationTests.cs` for JavaScript rendering

### Implementation for User Story 1

- [ ] T015 [US1] Implement JavaScript execution and fully rendered HTML capture in `BrowserFetchTool.cs`
- [ ] T016 [US1] Add timeout handling with 30-second default (configurable via BrowserConfiguration)
- [ ] T017 [US1] Implement retry policy integration for transient failures (TimeoutException, BrowserClosedException)
- [ ] T018 [US1] Add validation for URL format (must be HTTP or HTTPS)
- [ ] T019 [US1] Add span attributes for fetch timing metrics
- [ ] T020 [US1] Add logging for navigation start, complete, and error events

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 3: User Story 2 - Configure Browser Settings (Priority: P2)

**Goal**: Add configurable browser settings for viewport and user agent

**Independent Test**: Provide configuration options and verify the browser uses the specified settings when fetching pages

### Tests for User Story 2 ⚠️

- [ ] T021 [P] [US2] Create contract test for custom viewport dimensions
- [ ] T022 [P] [US2] Create contract test for custom user agent string
- [ ] T023 [P] [US2] Create contract test for default configuration when none provided

### Implementation for User Story 2

- [ ] T024 [US2] Update `BrowserFetchTool.cs` to accept BrowserConfiguration in FetchRequest
- [ ] T025 [US2] Implement viewport configuration in Playwright browser context
- [ ] T026 [US2] Implement user agent configuration in Playwright page settings
- [ ] T027 [US2] Add validation for viewport dimensions (320-8192 width, 240-4320 height)
- [ ] T028 [US2] Add default values (1920x1080, browser default user agent)
- [ ] T029 [US2] Add span attributes for browser configuration (viewport, user_agent)
- [ ] T030 [US2] Add logging for configuration applied

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 4: User Story 3 - Handle Page Load Events (Priority: P3)

**Goal**: Implement wait mechanism for specific DOM elements before capturing content

**Independent Test**: Provide wait conditions and verify the tool waits for those conditions before returning content

### Tests for User Story 3 ⚠️

- [ ] T031 [P] [US3] Create contract test for waitForSelector functionality
- [ ] T032 [P] [US3] Create contract test for timeout when selector not found
- [ ] T033 [P] [US3] Create integration test for dynamic content loading

### Implementation for User Story 3

- [ ] T034 [US3] Update FetchRequest to accept optional WaitForSelector (CSS selector string)
- [ ] T035 [US3] Update FetchResult to include HasWaited and WaitDurationMilliseconds properties
- [ ] T036 [US3] Implement Playwright WaitForSelectorAsync in BrowserFetchTool.cs
- [ ] T037 [US3] Add timeout handling for selector wait (use BrowserConfiguration.TimeoutMilliseconds)
- [ ] T038 [US3] Add error handling when selector not found after timeout
- [ ] T039 [US3] Add span attributes for wait timing (waited, wait_duration_ms)
- [ ] T040 [US3] Add logging for wait start, element found, and timeout events

**Checkpoint**: All user stories should now be independently functional

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T041 [P] Update `AGENTS.md` to reference `specs/005-browser-fetch/plan.md`
- [ ] T042 [P] Add XML documentation comments to all public types
- [ ] T043 [P] Run `dotnet format` on entire project
- [ ] T044 [P] Run `dotnet build` and ensure no warnings
- [ ] T045 [P] Run all tests and verify 80%+ coverage for new code
- [ ] T046 [P] Test quickstart.md examples manually
- [ ] T047 [P] Verify tracing spans emit correctly with OpenTelemetry
- [ ] T048 [P] Verify logging includes TraceID and CorrelationID
- [ ] T049 [P] Verify retry policy applies to transient failures
- [ ] T050 [P] Verify browser instances are properly disposed (no orphaned processes)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 1)**: No dependencies - can start immediately (but blocks all user stories)
- **User Stories (Phase 2-4)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3)
- **Polish (Phase 5)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 1) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 1) - Builds on US1 but independently testable
- **User Story 3 (P3)**: Can start after Foundational (Phase 1) - Builds on US1 but independently testable

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Models before services
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- All Foundational tasks marked [P] can run in parallel
- Once Foundational phase completes, all user stories can start in parallel (if team capacity allows)
- All tests for a user story marked [P] can run in parallel
- Different user stories can be worked on in parallel by different team members

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Foundational
2. Complete Phase 2: User Story 1
3. **STOP and VALIDATE**: Test User Story 1 independently
4. Deploy/demo if ready

### Incremental Delivery

1. Complete Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → Test independently → Deploy/Demo
5. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1
   - Developer B: User Story 2
   - Developer C: User Story 3
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
