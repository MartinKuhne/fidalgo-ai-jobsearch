# Implementation Plan: System Logging and Tracing Infrastructure

**Branch**: `004-system-logging-tracing` | **Date**: 2026-05-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-system-logging-tracing/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

This feature implements system-wide logging, tracing, and error handling infrastructure for the Fidalgo application. The primary requirements are:
- Structured logging to console and daily file output with 7-day retention
- Distributed tracing with W3C Trace Context compliance and OpenTelemetry export
- Global exception handling with HTTP status code mapping and user-friendly error messages
- Automatic retry with exponential backoff for transient failures

The technical approach involves configuring .NET's built-in logging framework, integrating OpenTelemetry SDK, and implementing middleware for request tracing and exception handling.

## Technical Context

**Language/Version**: .NET 10.0 (LTS)

**Primary Dependencies**: 
- Microsoft.Extensions.Logging (built-in)
- OpenTelemetry SDK (.NET)
- OpenTelemetry Exporter OTLP

**Storage**: 
- Console output for development
- File-based JSON logs (one file per day) for production
- SQLite for application data (existing)

**Testing**: 
- xUnit for unit tests
- Integration tests for external dependencies
- Minimum 80% code coverage required

**Target Platform**: Windows/Linux/macOS (cross-platform .NET application)

**Project Type**: Web service/API (Fidalgo Agent)

**Performance Goals**: 
- Log writes must not block request processing (asynchronous)
- Trace context propagation overhead < 1ms per request
- Retry mechanism must not cause unacceptable delays (max 3 retries, exponential backoff)

**Constraints**: 
- No secrets stored in code (use environment variables)
- Log files must be rotated and cleaned up automatically
- OpenTelemetry export must be configurable (enabled/disabled, endpoint configurable)

**Scale/Scope**: 
- Support 1000+ concurrent requests
- Handle burst logging without degradation
- 7-day log retention with automatic cleanup

## Constitution Check

*GATES: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Compliance Verification

✅ **Open Source (I)**: All dependencies must have open source licenses compatible with project distribution. OpenTelemetry and .NET are both open source.

✅ **Testing (II)**: Tests MUST be written before implementation. This is a cross-cutting concern, so tests should cover:
- Log entry format and content
- Trace context propagation
- Exception handling and HTTP status code mapping
- Retry behavior with exponential backoff
- Log file rotation and cleanup

✅ **Observability (III)**: This feature DIRECTLY implements the observability requirements from the constitution:
- Structured logs with correlation IDs ✅
- Metrics export (OpenTelemetry) ✅
- Tracing with OpenTelemetry standard ✅
- Log levels: ERROR, WARN, INFO, DEBUG ✅

✅ **Infrastructure as Code (IV)**: Logging configuration must be in code (not hardcoded). Environment variables will configure:
- Log file path
- OpenTelemetry endpoint
- Log level thresholds
- Retention settings

✅ **Self Explaining Code (V)**: Public logging/tracing APIs must include XML documentation. Code must use clear naming for logging categories and trace context properties.

✅ **Immutability (VI)**: Log entries and trace context structures must use records/immutability where feasible.

### Summary

**All constitutional gates PASS**. This feature aligns with all constitutional principles and requires no violations or justifications.

## Project Structure

### Documentation (this feature)

```text
specs/004-system-logging-tracing/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Fidalgo.Agent/
│   ├── Logging/              # NEW: Logging infrastructure
│   │   ├── LogEntry.cs
│   │   ├── TraceContext.cs
│   │   ├── Span.cs
│   │   └── LoggingExtensions.cs
│   ├── Tracing/              # NEW: Tracing infrastructure
│   │   ├── TraceMiddleware.cs
│   │   ├── SpanFactory.cs
│   │   └── OpenTelemetryExporter.cs
│   ├── ErrorHandling/        # NEW: Error handling
│   │   ├── GlobalExceptionHandler.cs
│   │   ├── HttpErrorMapper.cs
│   │   └── ValidationProblemDetails.cs
│   ├── Retry/                # NEW: Retry logic
│   │   ├── RetryPolicy.cs
│   │   ├── ExponentialBackoff.cs
│   │   └── TransientFaultDetector.cs
│   ├── Models/               # EXISTING: Trace context entities
│   │   ├── TraceContext.cs   # NEW: Move from Logging
│   │   └── LogEntry.cs       # NEW: Move from Logging
│   └── Services/             # EXISTING: Use logging/tracing
│       ├── JobScraperService.cs
│       └── LlmService.cs
└── Fidalgo.Agent.Tests/
    ├── Logging/              # NEW: Logging tests
    ├── Tracing/              # NEW: Tracing tests
    ├── ErrorHandling/        # NEW: Error handling tests
    └── Retry/                # NEW: Retry tests

tests/
├── contract/                 # EXISTING: Contract tests
├── integration/              # EXISTING: Integration tests
└── unit/                     # EXISTING: Unit tests
```

**Structure Decision**: Single project structure selected. This is a cross-cutting concern that adds new functionality to the existing Fidalgo.Agent project. No new projects or directories are needed. The feature adds new subdirectories under `src/Fidalgo.Agent/` for each concern area.

## Complexity Tracking

No violations identified. This feature aligns with all constitutional principles.
