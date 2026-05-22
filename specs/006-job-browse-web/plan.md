# Implementation Plan: Job Browse Web Site

**Branch**: `006-job-browse-web` | **Date**: 2026-05-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-job-browse-web/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Implement a Blazor web application (`Fidalgo.Web`) that allows job seekers to browse, filter, and manage their discovered jobs. The web site shares the `JobDbContext` and `JobRepository` from `Fidalgo.Agent` via a shared library. Users select their email from a drop-down, view a paginated list of non-deleted jobs ordered by suitability rating, filter by date, view job details in a modal, and soft-delete jobs. The site serves as a user-friendly interface for the job discovery system.

## Technical Context

**Language/Version**: C# 13, .NET 10.0 LTS

**Primary Dependencies**: 
- Microsoft.AspNetCore.Components.WebAssembly (latest stable) - Blazor WebAssembly framework
- Microsoft.EntityFrameworkCore.Sqlite (10.0.8) - Shared SQLite database access
- MudBlazor (latest stable) - Blazor component library for UI (drop-downs, tables, modals, pagination)
- Microsoft.Extensions.Options - Configuration pattern
- Microsoft.Extensions.Logging - Structured logging

**Storage**: SQLite (shared database with Fidalgo.Agent, configured via CLI options or environment variable)

**Testing**: xUnit (existing test framework) - Unit tests for services, component tests for Blazor pages

**Target Platform**: Cross-platform web browser (Chrome, Firefox, Edge, Safari) - Blazor WebAssembly runs client-side

**Project Type**: Web application - Blazor WebAssembly standalone site

**Performance Goals**: 
- Job list loads within 2 seconds for tenants with up to 1000 jobs
- Pagination and filtering respond within 500ms
- Modal opens within 200ms

**Constraints**: 
- Must share `JobDbContext` and `JobRepository` with `Fidalgo.Agent` via a shared library
- Must use Blazor as the technology foundation (per spec requirement WEB-100)
- Must integrate with existing tracing and logging infrastructure
- No authentication system required for v1 (email-based scoping only)
- SQLite must support concurrent read access (web site reads while agent may write)

**Scale/Scope**: 
- New web project alongside existing agent project
- Single user-facing application (no multi-tenant isolation beyond email scoping)
- Designed for internal use by job seekers who have jobs in the system

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Compliance Status: ✅ PASS

| Constitution Requirement | Status | Justification |
|-------------------------|--------|---------------|
| **I. Open Source** | ✅ PASS | Blazor is part of .NET (MIT licensed); MudBlazor is MIT licensed; all dependencies have open source licenses |
| **II. Testing** | ✅ PASS | Feature includes unit tests for services and component tests for Blazor pages; test-first approach |
| **III. Observability** | ✅ PASS | Integrates with existing tracing (OpenTelemetry) and logging infrastructure via shared dependencies |
| **IV. Infrastructure as Code** | ✅ PASS | Database path configured via environment/CLI options; no hardcoded values |
| **V. Self Explaining Code** | ✅ PASS | Clear naming (JobList.razor, JobDetailsModal.razor, JobsService); XML documentation on public APIs |
| **VI. Immutability** | ✅ PASS | View models use records; configuration passed via options pattern |
| **Security: No secrets** | ✅ PASS | Database path via configuration; no secrets in code |

### Gates Determined

- **No violations**: Feature aligns with all constitutional principles
- **Re-check after Phase 1**: Design maintains compliance (see Phase 1 section)

## Project Structure

### Documentation (this feature)

```text
specs/006-job-browse-web/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── jobs-api.md      # Web API contracts
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Fidalgo.Agent/                          # Existing agent project (unchanged)
│   ├── Storage/
│   │   ├── JobDbContext.cs                  # Shared - also used by Fidalgo.Web
│   │   ├── JobEntity.cs                     # Shared - also used by Fidalgo.Web
│   │   └── JobRepository.cs                 # Shared - also used by Fidalgo.Web
│   └── ...
├── Fidalgo.Agent.Tests/                    # Existing test project (unchanged)
├── Fidalgo.Shared/                         # NEW: Shared library with DB context
│   ├── Fidalgo.Shared.csproj
│   ├── Storage/
│   │   ├── JobDbContext.cs                  # Re-exported from Agent
│   │   ├── JobEntity.cs                     # Re-exported from Agent
│   │   └── JobRepository.cs                 # Re-exported from Agent
│   └── Models/
│       └── JobViewModel.cs                  # NEW: UI-friendly DTO for web display
├── Fidalgo.Web/                            # NEW: Blazor WebAssembly project
│   ├── Fidalgo.Web.csproj
│   ├── Program.cs
│   ├── wwwroot/
│   │   └── index.html
│   ├── Components/
│   │   ├── Layout/
│   │   │   ├── MainLayout.razor             # Top navbar + page content
│   │   │   └── NavMenu.razor                # Navigation sidebar (if needed)
│   │   ├── Pages/
│   │   │   ├── Jobs.razor                   # Main job list page
│   │   │   └── Index.razor                  # Home page (redirect to Jobs)
│   │   └── Components/
│   │       └── JobDetailsModal.razor        # Modal dialog for job details
│   ├── Services/
│   │   ├── JobsService.cs                   # NEW: Business logic for job operations
│   │   └── TenantService.cs                 # NEW: Email drop-down population
│   └── Models/
│       └── PaginationState.cs               # NEW: Pagination state management
└── Fidalgo.Web.Tests/                      # NEW: Test project for web
    ├── Fidalgo.Web.Tests.csproj
    └── Unit/
        ├── JobsServiceTests.cs
        └── TenantServiceTests.cs
```

**Structure Decision**: New shared library (`Fidalgo.Shared`) and new Blazor web project (`Fidalgo.Web`). The shared library contains the `JobDbContext`, `JobEntity`, and `JobRepository` so both `Fidalgo.Agent` and `Fidalgo.Web` reference it instead of duplicating code. This satisfies requirement WEB-101 (shared library containing the DB context). The web project uses Blazor WebAssembly for client-side rendering with server-side API calls for database operations.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No violations detected. Feature design aligns with all constitutional principles.
