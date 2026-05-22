Scope: This file governs the entire repository.
These instructions must be followed always if you’re contributing, reviewing, or acting as an automated coding agent.

## Process

- Build command: ```dotnet build src/Fidalgo.slnx```
- Test command: ```dotnet test src/Fidalgo.slnx```
- Before starting new work, ensure the project compiles with no warnings and unit tests pass. Fix any issues and commit before starting new work
- Always _read_ the **Architecture Memory** in the ./wiki/architecture/ folder when doing research
- Always  _maintain_ **Architecture Memory** in the ./wiki/architecture/ folder when work is performed
- Keep changes small and focused; work in small iterations
- Every change must be built and tested before being commited
- Code compiles with no warnings
- Commit every change once successful
- if a semantic index is available, index the codebase after updates
- Prefer the simplest design that satisfies current requirements.
- If multiple options exist, document a brief rationale and link docs/architecture-decision-records.md
- User instructions take precedence over the central doc

## Coding principles

- Each module serves a problem domain or purpose (SRP)
- New features added via extension, not modification (OCP)
- Subclasses work anywhere base class is used (LSP)
- Interfaces are minimal and focused (ISP)
- Business logic knows nothing about concrete implementations (DIP)
- Public functions have a concise comment explaining their purpose
- Prefer libraries over custom code
- Use immutability where feasible
- All arguments are passed to the function in the signature, honest functions
- Avoid side effects other than logging
- Avoid libraries without open source license
- Use structured exception handling
- No unused fields/methods/usings

## Security

- Security by default: encryption at rest & in transit, least privilege
- Do not store passwords in code

## C#

- Follow common coding conventions
- Use NSubstitute for mocking
- Modern C#: nullable enabled; warnings as errors; primary constructors where helpful
- Async‑first; propagate CancellationToken; Async suffix for async methods
- Use newest available LTS framework
- Use options and options validation as learned from https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration

## node.js

- Use typescript
- Prefer ESM over commonjs
- Use pino for logging
- Use the opentelemetry auto instrumentation
- Commits must be eslint clean and tsc --noEmit passes without error

## Task Tooling

- Prefer PowerShell when on Windows

## Code Organization

- src/: source code and tests
- doc/: documentation
- One class, stuct, record or interface per file

# EARS (Easy Approach to Requirements Syntax) formatted universal non-functional requirements

## Logging

- [NFR-001] The system shall write log entries to the console
- [NFR-002] When the system is a binary executable, it shall write log entries to a file, one file per day, in structured JSON format.
- [NFR-003] When the system is a binary executable, it shall retain logs for 7 days
- [NFR-004] The system shall include a timestamp in UTC ISO 8601 format for every log entry.
- [NFR-005] When the system starts up or shuts down gracefully, it shall write a log entry with the level INFO.
- [NFR-006] When the system encounters an unexpected exception, it shall write a log entry with the level ERROR including the full stack trace.

## Tracing

- [NFR-100] The system shall generate a unique TraceID for every incoming request that does not already contain one.
- [NFR-101] The system shall propagate the TraceID to all downstream services and external dependencies via HTTP headers.
- [NFR-102] The system shall adhere to the W3C Trace Context standard for trace identifier headers.
- [NFR-103] The system shall export tracing data to an OpenTelemetry-compatible collector.
- [NFR-104] When the system receives a request, it shall create a "Root Span" representing the processing of that entire request.
- [NFR-105] When the system calls an external dependency, it shall create a "Child Span" linked to the active "Root Span".
- [NFR-106] When the system detects a transient failure (e.g., network timeout), it shall perform a retry operation using an exponential backoff strategy.
- [NFR-107] While the system is processing a user request, it shall include the CorrelationID in every log entry generated during that request.
- [NFR-108] While a specific trace is active, the system shall ensure that all SpanIDs are logically and hierarchically linked to the parent TraceID.

## Error handling

- [NFR-200] The system shall implement a global exception handler to catch unhandled exceptions.
- [NFR-201] The system shall map specific technical exceptions to appropriate HTTP status codes (e.g., 400 Bad Request, 401 Unauthorized, 404 Not Found, 500 Internal Server Error).
- [NFR-202] If an unhandled system exception occurs, then the system shall not display raw stack traces or technical error messages to the end-user.
- [NFR-203] If a validation error occurs, then the system shall display specific error messages indicating which fields were invalid.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan:
specs/006-job-browse-web/plan.md
<!-- SPECKIT END -->
