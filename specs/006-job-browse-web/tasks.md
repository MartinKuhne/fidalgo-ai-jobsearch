# Tasks: Job Browse Web Site

**Input**: Design documents from `/specs/006-job-browse-web/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: The Constitution (II. Testing - NON-NEGOTABLE) requires tests for every feature. Test-first approach enforced.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Single project**: `src/`, `tests/` at repository root
- Paths shown below assume single project - adjust based on plan.md structure

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Create Fidalgo.Shared library project in src/Fidalgo.Shared/Fidalgo.Shared.csproj
- [x] T002 [P] Create Fidalgo.Web Blazor WebAssembly project in src/Fidalgo.Web/Fidalgo.Web.csproj
- [x] T003 [P] Create Fidalgo.Web.Tests test project in src/Fidalgo.Web.Tests/Fidalgo.Web.Tests.csproj
- [x] T004 Add MudBlazor package reference to Fidalgo.Web.csproj
- [x] T005 Add Microsoft.AspNetCore.Components.WebAssembly package reference to Fidalgo.Web.csproj
- [x] T006 Add Microsoft.EntityFrameworkCore.Sqlite package reference to Fidalgo.Shared.csproj
- [x] T007 Configure Fidalgo.Web to reference Fidalgo.Shared project
- [x] T008 Configure Fidalgo.Web.Tests to reference Fidalgo.Shared and Fidalgo.Web projects

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T010 [P] Move JobEntity.cs from Fidalgo.Agent/Storage to Fidalgo.Shared/Storage/JobEntity.cs
- [x] T011 [P] Move JobDbContext.cs from Fidalgo.Agent/Storage to Fidalgo.Shared/Storage/JobDbContext.cs
- [x] T012 [P] Move JobRepository.cs from Fidalgo.Agent/Storage to Fidalgo.Shared/Storage/JobRepository.cs
- [x] T013 Update Fidalgo.Agent to reference Fidalgo.Shared instead of local storage files
- [x] T014 Create JobViewModel record in Fidalgo.Shared/Models/JobViewModel.cs
- [x] T015 Create PaginationState class in Fidalgo.Web/Models/PaginationState.cs
- [x] T016 Create TenantEmailInfo record in Fidalgo.Shared/Models/TenantEmailInfo.cs
- [x] T017 Create IJobsService interface in Fidalgo.Web/Services/IJobsService.cs
- [x] T018 Create ITenantService interface in Fidalgo.Web/Services/ITenantService.cs
- [x] T019 Create JobsService implementation in Fidalgo.Web/Services/JobsService.cs
- [x] T020 Create TenantService implementation in Fidalgo.Web/Services/TenantService.cs
- [x] T021 Configure DI registration for shared services in Fidalgo.Web/Program.cs
- [x] T022 Create wwwroot/index.html SPA entry point for Fidalgo.Web

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Browse and filter jobs by tenant (Priority: P1) 🎯 MVP

**Goal**: Users can select their email from a drop-down, view a paginated list of non-deleted jobs ordered by suitability rating, and filter by date.

**Independent Test**: A user can select their email, see a filtered and sorted list of non-deleted jobs with pagination, and verify the results match expectations.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T023 [P] [US1] Create JobsServiceTests.cs in Fidalgo.Web.Tests/Unit/JobsServiceTests.cs with test for GetJobsAsync returns jobs ordered by score descending
- [x] T024 [P] [US1] Create JobsServiceTests.cs in Fidalgo.Web.Tests/Unit/JobsServiceTests.cs with test for GetJobsAsync applies date filter correctly
- [x] T025 [P] [US1] Create TenantServiceTests.cs in Fidalgo.Web.Tests/Unit/TenantServiceTests.cs with test for GetTenantEmailsAsync returns unique emails with job counts
- [x] T026 [P] [US1] Create component test in Fidalgo.Web.Tests/Unit/JobsPageTests.cs for tenant drop-down population

### Implementation for User Story 1

- [x] T027 [US1] Create MainLayout.razor in Fidalgo.Web/Components/Layout/MainLayout.razor with top navbar structure
- [x] T028 [US1] Create Index.razor in Fidalgo.Web/Components/Pages/Index.razor that redirects to Jobs page
- [x] T029 [US1] Create Jobs.razor in Fidalgo.Web/Components/Pages/Jobs.razor with tenant drop-down, date filter, and jobs table
- [x] T030 [US1] Implement tenant drop-down binding in Jobs.razor using TenantService.GetTenantEmailsAsync
- [x] T031 [US1] Implement jobs table with MudTable component displaying Score, Employer, Date, Title, Recommendation columns
- [x] T032 [US1] Implement pagination controls using PaginationState in Jobs.razor
- [x] T033 [US1] Implement date filter binding that calls JobsService.GetJobsAsync with dateFrom parameter
- [x] T034 [US1] Add empty state message when tenant has no jobs
- [x] T035 [US1] Add empty state message when date filter returns zero jobs

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Review job details in a modal (Priority: P2)

**Goal**: Users can click a job title to view all job details in a modal dialog without leaving the list view.

**Independent Test**: A user can click any job title in the list and see a modal with all job fields displayed.

### Tests for User Story 2 ⚠️

- [x] T036 [P] [US2] Create component test in Fidalgo.Web.Tests/Unit/JobDetailsModalTests.cs for modal rendering with job data
- [x] T037 [P] [US2] Create component test in Fidalgo.Web.Tests/Unit/JobDetailsModalTests.cs for modal close behavior

### Implementation for User Story 2

- [x] T038 [US2] Create JobDetailsModal.razor in Fidalgo.Web/Components/Components/JobDetailsModal.razor
- [x] T039 [US2] Add MudDialog component to JobDetailsModal.razor with all job fields displayed
- [x] T040 [US2] Add click handler on job title in Jobs.razor to open modal via JobsService.GetJobByIdAsync
- [x] T041 [US2] Implement modal close behavior that preserves filter and page state
- [x] T042 [US2] Style modal content with MudPaper for readability

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - Delete jobs (Priority: P3)

**Goal**: Users can soft-delete jobs from the list or modal, removing them from all views.

**Independent Test**: A user can delete a job from the list or from the modal, and the job disappears from all views.

### Tests for User Story 3 ⚠️

- [x] T043 [P] [US3] Create JobsServiceTests.cs in Fidalgo.Web.Tests/Unit/JobsServiceTests.cs with test for SoftDeleteJobAsync marks job as deleted
- [X] T044 [P] [US3] Create component test in Fidalgo.Web.Tests/Unit/JobsPageTests.cs for delete action removing job from list

### Implementation for User Story 3

- [x] T045 [US3] Add trashcan icon column to jobs table in Jobs.razor
- [x] T046 [US3] Implement delete handler on trashcan icon calling JobsService.SoftDeleteJobAsync
- [x] T047 [US3] Refresh jobs list after successful delete in Jobs.razor
- [x] T048 [US3] Add delete button to JobDetailsModal.razor
- [x] T049 [US3] Implement delete handler in JobDetailsModal.razor calling JobsService.SoftDeleteJobAsync
- [x] T050 [US3] Close modal and refresh list after successful delete from modal
- [x] T051 [US3] Add confirmation dialog before delete action using MudConfirmDialog

**Checkpoint**: All user stories should now be independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T052 Add structured logging to JobsService and TenantService per Constitution III. Observability
- [x] T053 Add error handling with user-friendly messages for database failures
- [x] T054 [P] Add loading indicators during async operations in Jobs.razor
- [x] T055 [P] Add responsive CSS for mobile browser support
- [x] T056 Update quickstart.md with run instructions and verify quickstart
- [x] T057 Run dotnet build and dotnet test to verify compilation and all tests pass
- [x] T058 Verify Constitution compliance: check linting, formatting, and code clarity

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-5)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3)
- **Polish (Phase 6)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Integrates with US1 components (Jobs.razor)
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - Integrates with US1/US2 components

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Models before services
- Services before UI components
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel (T002, T003, T004, T005, T006, T007, T008)
- All Foundational tasks marked [P] can run in parallel (T010, T011, T012, T014, T015, T016)
- Once Foundational phase completes, all user stories can start in parallel (if team capacity allows)
- All tests for a user story marked [P] can run in parallel
- Models within a story marked [P] can run in parallel
- Different user stories can be worked on in parallel by different team members

### Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Create JobsServiceTests.cs with GetJobsAsync ordering test"
Task: "Create JobsServiceTests.cs with date filter test"
Task: "Create TenantServiceTests.cs with GetTenantEmailsAsync test"
Task: "Create component test for tenant drop-down population"

# Launch foundational models in parallel:
Task: "Create JobViewModel record"
Task: "Create PaginationState class"
Task: "Create TenantEmailInfo record"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → Test independently → Deploy/Demo
5. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
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
