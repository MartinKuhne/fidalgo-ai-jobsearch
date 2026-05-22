# Research: Job Browse Web Site

**Feature**: 006-job-browse-web
**Date**: 2026-05-22

## Research Findings

### Decision 1: Blazor WebAssembly vs Blazor Server

**Chosen**: Blazor WebAssembly (standalone)

**Rationale**:
- WebAssembly runs client-side in the browser, reducing server load
- The application is a simple read/list interface with no real-time requirements
- Users access the site from various devices/browsers; WASM has broad support
- SQLite access requires a database, which cannot be accessed directly from the browser. The web app will call into a lightweight API layer or use a server-side Blazor approach.

**Alternatives considered**:
- **Blazor Server**: Requires persistent SignalR connection; adds server complexity for a simple read-only interface. Rejected because the feature scope does not justify the infrastructure overhead.
- **Server-rendered Razor Pages**: Simpler but requires full page reloads for filtering/pagination. Rejected because the spec requires smooth modal interactions and client-side state management.

### Decision 2: Shared Library Architecture

**Chosen**: Extract `JobDbContext`, `JobEntity`, and `JobRepository` into a new `Fidalgo.Shared` library referenced by both `Fidalgo.Agent` and `Fidalgo.Web`.

**Rationale**:
- Satisfies requirement WEB-101 (shared library containing the DB context)
- Eliminates code duplication between agent and web projects
- Single source of truth for the data model
- Both projects can update the shared library independently

**Alternatives considered**:
- **Copy-paste models to web project**: Creates maintenance burden and risk of divergence. Rejected because it violates DRY principle.
- **REST API layer**: Would require building an API server just to expose the database. Rejected because the web app and database are likely co-located; direct shared library access is simpler and sufficient for the scope.

### Decision 3: UI Component Library

**Chosen**: MudBlazor

**Rationale**:
- Provides all required components out of the box: drop-downs, data tables with pagination, modals, icons (trashcan)
- Actively maintained, MIT licensed
- Blazor-native with strong community support
- Reduces custom CSS/JS development time significantly

**Alternatives considered**:
- **Bootstrap + custom JavaScript**: More familiar but requires more boilerplate and custom component implementation. Rejected because MudBlazor provides pre-built Blazor components.
- **Fluent UI Blazor**: Microsoft's official Blazor library but less mature than MudBlazor for data table features. Rejected because pagination and filtering would require more custom work.

### Decision 4: Pagination Approach

**Chosen**: Client-side pagination with server-side data fetch

**Rationale**:
- The job list is scoped per tenant (email), so the dataset per user is manageable
- Client-side pagination provides instant page switching without round-trips
- Initial data fetch retrieves all non-deleted jobs for the selected tenant (with date filter applied server-side)
- Pagination state managed in `PaginationState` class

**Alternatives considered**:
- **Full server-side pagination**: Fetch 20 jobs per page from the database. Rejected because it adds database query complexity and network latency for each page change.
- **Load all jobs client-side then paginate**: Simplest approach but may be slow for tenants with many jobs. The chosen approach balances initial load time with page-switching speed.

### Decision 5: Date Filter - "Date Found" Field

**Chosen**: Use `PostedDate` field from `JobEntity` as the date filter basis, with the understanding that it represents when the job was discovered/saved.

**Rationale**:
- The spec says "filter by the date the job was found" but `JobEntity` has `PostedDate` (when job was posted) and no explicit "date found" field
- `PostedDate` is the closest available field and is already nullable in the entity
- The date filter will use `PostedDate` for filtering; if a dedicated "date found" field is needed later, it can be added

**Alternatives considered**:
- **Add a new `DateFound` field to JobEntity**: Would require a database migration. Rejected because it adds scope; `PostedDate` serves the filtering purpose for v1.
- **Use entity creation timestamp**: SQLite does not expose creation timestamps natively without additional infrastructure. Rejected because it adds unnecessary complexity.

### Decision 6: Soft Delete Implementation

**Chosen**: Reuse existing `JobRepository.SoftDeleteAsync()` method

**Rationale**:
- `JobRepository` already has `SoftDeleteAsync(Guid)` method that sets `IsDeleted = true`
- `JobRepository.QueryAsync()` already excludes deleted jobs by default (`excludeDeleted: true`)
- No new repository methods needed; the existing infrastructure satisfies WEB-007, WEB-008, WEB-011

**Alternatives considered**:
- **Hard delete**: Permanent data loss. Rejected because soft-delete preserves audit trail and allows recovery.
- **New `DeleteJobAsync` method**: Unnecessary duplication; existing `SoftDeleteAsync` already handles the requirement.

### Decision 7: Database Connection Sharing

**Chosen**: Both `Fidalgo.Agent` and `Fidalgo.Web` use the same SQLite database file path, configured via environment variable or CLI option.

**Rationale**:
- SQLite supports concurrent reads; the web app only reads and soft-deletes
- The agent writes new jobs; the web app reads and updates `IsDeleted`
- SQLite's file-level locking handles basic concurrency for this read-heavy workload
- Configuration via `DatabasePath` CLI option (agent) and appsettings.json (web) pointing to the same path

**Alternatives considered**:
- **Separate databases**: Would require sync mechanism. Rejected because it adds complexity with no benefit.
- **SQLite connection pooling**: Not needed for the expected scale (single user per session).
