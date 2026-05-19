# Fidalgo Constitution

## Core Principles

### I. Open Source

All project source code, documentation, and configuration SHALL be publicly available under permissive open source licenses. Third-party dependencies MUST have open source licenses compatible with project distribution. No proprietary or closed-source components allowed.

### II. Testing (NON-NEGOTIABLE)

Every feature MUST have corresponding tests before implementation. TDD mandatory: Tests written → User approved → Tests fail → Then implement. Red-Green-Refactor cycle strictly enforced. Minimum 80% code coverage required for new code. Integration tests required for all external interfaces and contracts.

### III. Observability

All services MUST emit structured logs with correlation IDs for request tracing. Metrics MUST be exported in Prometheus format. Tracing MUST use OpenTelemetry standard. Health endpoints MUST expose dependency status. Log levels: ERROR for failures, WARN for degraded states, INFO for key events, DEBUG for development only.

### IV. Infrastructure as Code

All infrastructure configuration MUST be version-controlled as code. Environment-specific values MUST use configuration files or environment variables, never hardcoded. Deployment automation MUST use declarative tools (e.g., Terraform, Ansible). Infrastructure changes MUST go through code review before apply.

### V. Self Explaining Code

Code MUST be self-documenting through clear naming, explicit intent, and minimal comments required. Complex business logic MUST include XML documentation on public APIs. README files REQUIRED at each package/module level explaining purpose and usage. Code reviews MUST verify clarity before approval.

### VI. Immutability

Configuration and data structures MUST be immutable where feasible. Records and value objects over mutable classes. State changes MUST produce new instances rather than modify existing. Dependency injection for testability, not runtime mutation. Mutable state limited to service-level caching with explicit invalidation.

## Additional Constraints

### Technology Stack

- .NET LTS framework required (REQ-003 from SPEC.md)
- Microsoft Agent Framework required (REQ-002 from SPEC.md)
- SQLite for local storage (REQ-007 from SPEC.md)
- Local OpenAI-compatible LLM at configurable endpoint (REQ-100 from SPEC.md)

### Security Requirements

- No secrets stored in code; use environment variables or secure vaults
- Encryption in transit for all external communications
- Input validation on all public interfaces
- Least privilege principle for service accounts

## Development Workflow

### Quality Gates

- All PRs require code review from at least one maintainer
- Tests MUST pass before merge (unit + integration)
- Linting and formatting checks MUST pass
- Breaking changes require migration plan documented

### Release Process

- Semantic versioning (MAJOR.MINOR.PATCH)
- Changelog maintained for all releases
- Release candidates tagged before production deploy

## Governance

Constitution supersedes all other practices. Amendments require:
1. Proposal documented in architecture-decision-records.md
2. Rationale clearly stated with alternatives considered
3. Migration plan if breaking changes required
4. Version bump: MAJOR for principle removal, MINOR for additions, PATCH for clarifications

All PRs/reviews must verify compliance with this constitution. Complexity must be justified with simpler alternative rejected because.

**Version**: 1.0.0 | **Ratified**: 2026-05-18 | **Last Amended**: 2026-05-18