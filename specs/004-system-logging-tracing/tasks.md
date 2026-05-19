# Tasks: System Logging and Tracing Infrastructure

**Input**: Design documents from `/specs/004-system-logging-tracing/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are OPTIONAL - only include them if explicitly requested in the feature specification. This feature requires tests per the constitution.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Single project**: `src/`, `tests/` at repository root
- Paths shown below assume single project structure per plan.md

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Create project structure per implementation plan in specs/004-system-logging-tracing/
- [X] T002 Add Serilog.AspNetCore NuGet package to Fidalgo.Agent project
- [X] T003 Add OpenTelemetry.Exporter.OpenTelemetryProtocol NuGet package to Fidalgo.Agent project
- [X] T004 Add Polly NuGet package to Fidalgo.Agent project
- [X] T005 [P] Configure Serilog in appsettings.json with console and file sinks
- [X] T006 [P] Configure OpenTelemetry in appsettings.json with collector endpoint
- [X] T007 [P] Add logging configuration classes in src/Fidalgo.Agent/Logging/

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T008 Create TraceContext record in src/Fidalgo.Agent/Models/TraceContext.cs
- [X] T009 Create LogEntry record in src/Fidalgo.Agent/Models/LogEntry.cs
- [X] T010 Create Span record in src/Fidalgo.Agent/Models/Span.cs
- [X] T011 Create ExceptionDetails record in src/Fidalgo.Agent/Models/ExceptionDetails.cs
- [X] T012 Create ITraceContextProvider interface in src/Fidalgo.Agent/Logging/ITraceContextProvider.cs
- [X] T013 Implement TraceContextProvider service in src/Fidalgo.Agent/Logging/TraceContextProvider.cs
- [X] T014 [P] Create ILogEntryWriter interface in src/Fidalgo.Agent/Logging/ILogEntryWriter.cs
- [X] T015 [P] Implement LogEntryWriter service in src/Fidalgo.Agent/Logging/LogEntryWriter.cs
- [X] T016 [P] Create ISpanFactory interface in src/Fidalgo.Agent/Tracing/ISpanFactory.cs
- [X] T017 [P] Implement SpanFactory service in src/Fidalgo.Agent/Tracing/SpanFactory.cs
- [X] T018 Create IExceptionMapper interface in src/Fidalgo.Agent/ErrorHandling/IExceptionMapper.cs
- [X] T019 Implement ExceptionMapper service in src/Fidalgo.Agent/ErrorHandling/ExceptionMapper.cs
- [X] T020 Create IRetryPolicy interface in src/Fidalgo.Agent/Retry/IPolicy.cs
- [X] T021 Implement RetryPolicy service in src/Fidalgo.Agent/Retry/RetryPolicy.cs
- [X] T022 Create ITraceContextPropagator interface in src/Fidalgo.Agent/Tracing/ITraceContextPropagator.cs
- [X] T023 Implement TraceContextPropagator service in src/Fidalgo.Agent/Tracing/TraceContextPropagator.cs
- [X] T024 Create IOtlpExporter interface in src/Fidalgo.Agent/Tracing/IOtlpExporter.cs
- [X] T025 Implement OtlpExporter service in src/Fidalgo.Agent/Tracing/OtlpExporter.cs
- [X] T026 Create ILoggingConfiguration interface in src/Fidalgo.Agent/Logging/Configuration/ILoggingConfiguration.cs
- [X] T027 Create ITracingConfiguration interface in src/Fidalgo.Agent/Tracing/Configuration/ITracingConfiguration.cs
- [X] T028 Implement configuration classes in src/Fidalgo.Agent/Logging/Configuration/LoggingConfiguration.cs
- [X] T029 Implement configuration classes in src/Fidalgo.Agent/Tracing/Configuration/TracingConfiguration.cs
- [X] T030 Configure Serilog in Program.cs with file sink for daily rotation
- [X] T031 Configure OpenTelemetry in Program.cs with OTLP exporter
- [X] T032 Configure exception handling middleware in Program.cs
- [X] T033 Configure retry policies in Program.cs
- [X] T034 Register all services in DI container in Program.cs

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Application Startup and Shutdown Logging (Priority: P1) 🎯 MVP

**Goal**: Implement structured logging for application startup, shutdown, and exception handling

**Independent Test**: Start and stop the application and verify INFO-level log entries appear in both console and file outputs with timestamps and trace context

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T035 [P] [US1] Unit test for startup logging in tests/unit/Logging/StartupLoggingTests.cs
- [ ] T036 [P] [US1] Unit test for shutdown logging in tests/unit/Logging/ShutdownLoggingTests.cs
- [ ] T037 [P] [US1] Unit test for exception logging in tests/unit/Logging/ExceptionLoggingTests.cs

### Implementation for User Story 1

- [ ] T038 [US1] Add startup logging to Program.cs Main method
- [ ] T039 [US1] Add shutdown logging to Program.cs application stopping event
- [ ] T040 [US1] Implement exception handling middleware in src/Fidalgo.Agent/ErrorHandling/ExceptionHandlingMiddleware.cs
- [ ] T041 [US1] Add error logging for unhandled exceptions in ExceptionHandlingMiddleware.cs
- [ ] T042 [US1] Add validation error handling in ExceptionHandlingMiddleware.cs
- [ ] T043 [US1] Add trace context to startup/shutdown log entries
- [ ] T044 [US1] Add logging extensions in src/Fidalgo.Agent/Logging/LoggingExtensions.cs

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Request-Level Tracing and Correlation (Priority: P1)

**Goal**: Implement distributed tracing with W3C Trace Context and correlation ID propagation

**Independent Test**: Send a test request and verify that all log entries during request processing include the same trace ID and correlation ID, and that downstream calls propagate these IDs

### Tests for User Story 2

- [ ] T045 [P] [US2] Unit test for trace ID generation in tests/unit/Tracing/TraceIdGenerationTests.cs
- [ ] T046 [P] [US2] Unit test for trace context propagation in tests/unit/Tracing/TraceContextPropagationTests.cs
- [ ] T047 [P] [US2] Integration test for request tracing in tests/integration/Tracing/RequestTracingTests.cs

### Implementation for User Story 2

- [ ] T048 [US2] Implement trace middleware in src/Fidalgo.Agent/Tracing/TraceMiddleware.cs
- [ ] T049 [US2] Add trace ID extraction from HTTP headers in TraceMiddleware.cs
- [ ] T050 [US2] Add trace ID generation for requests without context in TraceMiddleware.cs
- [ ] T051 [US2] Add correlation ID extraction and propagation in TraceMiddleware.cs
- [ ] T052 [US2] Implement span creation for incoming requests in TraceMiddleware.cs
- [ ] T053 [US2] Implement span creation for downstream calls in TraceContextPropagator.cs
- [ ] T054 [US2] Add span attributes for controller and action in TraceMiddleware.cs
- [ ] T055 [US2] Add logging for request start/complete with trace context in TraceMiddleware.cs

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - Structured Logging and Error Handling (Priority: P1)

**Goal**: Implement structured logging, exception mapping, and validation error handling

**Independent Test**: Trigger various error conditions and verify that errors are logged with full stack traces, mapped to appropriate HTTP status codes, and user-facing messages are clear and specific

### Tests for User Story 3

- [ ] T056 [P] [US3] Unit test for exception mapping in tests/unit/ErrorHandling/ExceptionMappingTests.cs
- [ ] T057 [P] [US3] Unit test for validation error handling in tests/unit/ErrorHandling/ValidationErrorTests.cs
- [ ] T058 [P] [US3] Integration test for error responses in tests/integration/ErrorHandling/ErrorResponseTests.cs

### Implementation for User Story 3

- [ ] T059 [US3] Implement HTTP status code mapping in ExceptionMapper.cs
- [ ] T060 [US3] Add validation exception type in src/Fidalgo.Agent/ErrorHandling/ValidationException.cs
- [ ] T061 [US3] Implement validation error extraction in ExceptionMapper.cs
- [ ] T062 [US3] Create ErrorResponse class in src/Fidalgo.Agent/ErrorHandling/ErrorResponse.cs
- [ ] T063 [US3] Create ValidationProblemDetails class in src/Fidalgo.Agent/ErrorHandling/ValidationProblemDetails.cs
- [ ] T064 [US3] Add user-friendly error message mapping in ExceptionMapper.cs
- [ ] T065 [US3] Implement stack trace filtering for production in ExceptionHandlingMiddleware.cs
- [ ] T066 [US3] Add validation error response formatting in ExceptionHandlingMiddleware.cs

**Checkpoint**: At this point, User Stories 1, 2, AND 3 should all work independently

---

## Phase 6: User Story 4 - Retry with Exponential Backoff (Priority: P2)

**Goal**: Implement automatic retry with exponential backoff for transient failures

**Independent Test**: Simulate transient failures and verify that the system retries with increasing delays following exponential backoff

### Tests for User Story 4

- [ ] T067 [P] [US4] Unit test for retry policy in tests/unit/Retry/RetryPolicyTests.cs
- [ ] T068 [P] [US4] Unit test for exponential backoff in tests/unit/Retry/ExponentialBackoffTests.cs
- [ ] T069 [P] [US4] Integration test for retry behavior in tests/integration/Retry/RetryIntegrationTests.cs

### Implementation for User Story 4

- [ ] T070 [US4] Implement TransientFaultDetector in src/Fidalgo.Agent/Retry/TransientFaultDetector.cs
- [ ] T071 [US4] Implement ExponentialBackoff policy in src/Fidalgo.Agent/Retry/ExponentialBackoff.cs
- [ ] T072 [US4] Add retry configuration to RetryPolicy.cs
- [ ] T073 [US4] Implement retry logic with jitter in RetryPolicy.cs
- [ ] T074 [US4] Add retry logging in RetryPolicy.cs
- [ ] T075 [US4] Create IRetryPolicy interface in src/Fidalgo.Agent/Retry/IRetryPolicy.cs
- [ ] T076 [US4] Implement IRetryPolicy in RetryPolicy.cs
- [ ] T077 [US4] Add retry to HTTP client calls in services that make external calls

**Checkpoint**: At this point, User Stories 1-4 should all work independently

---

## Phase 7: User Story 5 - Log Retention and OpenTelemetry Export (Priority: P2)

**Goal**: Implement 7-day log retention and OpenTelemetry export for centralized monitoring

**Independent Test**: Run the system for an extended period and verify that daily log files are created and retained for 7 days, and that tracing data is exported to a configured collector

### Tests for User Story 5

- [ ] T078 [P] [US5] Unit test for log retention in tests/unit/Logging/LogRetentionTests.cs
- [ ] T079 [P] [US5] Integration test for OpenTelemetry export in tests/integration/Tracing/OpenTelemetryExportTests.cs

### Implementation for User Story 5

- [ ] T080 [US5] Configure Serilog file sink with 7-day retention in appsettings.json
- [ ] T081 [US5] Implement log file cleanup in src/Fidalgo.Agent/Logging/LogCleanupService.cs
- [ ] T082 [US5] Add OpenTelemetry span export in OtlpExporter.cs
- [ ] T083 [US5] Implement span buffering in OtlpExporter.cs
- [ ] T084 [US5] Add OpenTelemetry configuration options in TracingConfiguration.cs
- [ ] T085 [US5] Implement graceful shutdown for OpenTelemetry exporter in OtlpExporter.cs
- [ ] T086 [US5] Add health check for OpenTelemetry connectivity in src/Fidalgo.Agent/Tracing/Health/TracingHealthCheck.cs
- [ ] T087 [US5] Add log file rotation monitoring in src/Fidalgo.Agent/Logging/LogRotationMonitor.cs

**Checkpoint**: All user stories should now be independently functional

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T088 [P] Add XML documentation to all public interfaces in src/Fidalgo.Agent/Logging/
- [ ] T089 [P] Add XML documentation to all public interfaces in src/Fidalgo.Agent/Tracing/
- [ ] T090 [P] Add XML documentation to all public interfaces in src/Fidalgo.Agent/ErrorHandling/
- [ ] T091 [P] Add XML documentation to all public interfaces in src/Fidalgo.Agent/Retry/
- [ ] T092 Code cleanup and refactoring across all logging/tracing components
- [ ] T093 Performance optimization for async logging in LogEntryWriter.cs
- [ ] T094 [P] Additional unit tests for edge cases in tests/unit/
- [ ] T095 [P] Contract tests for all interfaces in tests/contract/
- [ ] T096 Security hardening for error messages (no sensitive data leakage)
- [ ] T097 Run quickstart.md validation examples
- [ ] T098 Verify all success criteria from spec.md are met
- [ ] T099 Update AGENTS.md with new plan reference

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3)
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P1)**: Can start after Foundational (Phase 2) - May use components from US1 but independently testable
- **User Story 3 (P1)**: Can start after Foundational (Phase 2) - May use components from US1/US2 but independently testable
- **User Story 4 (P2)**: Can start after Foundational (Phase 2) - May use components from US1/US2/US3 but independently testable
- **User Story 5 (P2)**: Can start after Foundational (Phase 2) - May use components from US1/US2/US3/US4 but independently testable

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Models before services
- Services before middleware/endpoints
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- All Foundational tasks marked [P] can run in parallel (within Phase 2)
- Once Foundational phase completes, all user stories can start in parallel (if team capacity allows)
- All tests for a user story marked [P] can run in parallel
- Models within a story marked [P] can run in parallel
- Different user stories can be worked on in parallel by different team members

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Unit test for startup logging in tests/unit/Logging/StartupLoggingTests.cs"
Task: "Unit test for shutdown logging in tests/unit/Logging/ShutdownLoggingTests.cs"
Task: "Unit test for exception logging in tests/unit/Logging/ExceptionLoggingTests.cs"

# Launch all models for User Story 1 together:
Task: "Add startup logging to Program.cs Main method"
Task: "Add shutdown logging to Program.cs application stopping event"
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
5. Add User Story 4 → Test independently → Deploy/Demo
6. Add User Story 5 → Test independently → Deploy/Demo
7. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1
   - Developer B: User Story 2
   - Developer C: User Story 3
   - Developer D: User Story 4
   - Developer E: User Story 5
3. Stories complete and integrate independently

---

## Summary

- **Total Task Count**: 99 tasks
- **Setup Phase**: 7 tasks
- **Foundational Phase**: 23 tasks
- **User Story 1**: 7 tasks (3 tests + 4 implementation)
- **User Story 2**: 8 tasks (3 tests + 5 implementation)
- **User Story 3**: 8 tasks (3 tests + 5 implementation)
- **User Story 4**: 8 tasks (3 tests + 5 implementation)
- **User Story 5**: 8 tasks (2 tests + 6 implementation)
- **Polish Phase**: 12 tasks

- **Parallel Opportunities**: 47 tasks marked with [P]
- **Independent Test Criteria**: Each user story can be tested independently after implementation
- **Suggested MVP Scope**: Phase 1 + Phase 2 + User Story 1 (37 tasks total)

### Format Validation

✅ All tasks follow the strict checklist format:
- Checkbox: `- [ ]` format
- Task ID: Sequential numbers (T001-T099)
- [P] marker: Parallelizable tasks marked
- [Story] label: User story phase tasks have US1-US5 labels
- Description: Clear action with exact file path

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
