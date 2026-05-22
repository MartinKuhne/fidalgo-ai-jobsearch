# Data Model: Job Browse Web Site

**Feature**: 006-job-browse-web
**Branch**: 006-job-browse-web
**Date**: 2026-05-22

## Shared Entities (from Fidalgo.Agent)

The following entities are defined in `Fidalgo.Agent.Storage` and shared via `Fidalgo.Shared`:

### JobEntity

Already defined in `Fidalgo.Agent.Storage.JobEntity`. Key attributes relevant to the web site:

| Field | Type | Web Site Usage |
|-------|------|----------------|
| `InternalId` | Guid | Job identification, delete target |
| `Email` | string | Tenant scoping (drop-down population) |
| `Employer` | string | Displayed in jobs list |
| `Title` | string? | Displayed in jobs list (clickable link to modal) |
| `Score` | int | Displayed in jobs list and modal; default sort field (descending) |
| `Recommendation` | string | Displayed in jobs list and modal |
| `IsDeleted` | bool | Filtered out from list views |
| `PostedDate` | DateTime? | Date filter basis |
| `Description` | string | Displayed in modal |
| `Pros` | string | Displayed in modal |
| `Cons` | string | Displayed in modal |
| `ResumeHints` | string | Displayed in modal |
| `SourceWebsite` | string | Displayed in modal |
| `SalaryRangeLow` | decimal? | Displayed in modal |
| `SalaryRangeHigh` | decimal? | Displayed in modal |
| `PostedDate` | DateTime? | Displayed in modal |

## Web-Specific Entities

### JobViewModel

A UI-friendly DTO used by the web site to display job information in the list view.

**Fields**:
- `InternalId` (Guid): Job identifier
- `Email` (string): Tenant email
- `Employer` (string): Employer name
- `Title` (string?): Job title
- `Score` (int): Suitability rating
- `Recommendation` (string): Apply/Maybe/Do not apply
- `PostedDate` (DateTime?): Date the job was posted/found
- `SourceWebsite` (string): Source website name

**Purpose**: Reduces data transferred to the client for the list view. The modal uses the full `JobEntity` when a user clicks a job title.

**State Transitions**: N/A (read-only DTO)

### PaginationState

Manages pagination state for the jobs list.

**Fields**:
- `CurrentPage` (int): Currently displayed page (1-based)
- `PageSize` (int): Number of items per page (default: 20)
- `TotalItems` (int): Total number of items matching the current filter
- `TotalPages` (int): Calculated as `ceil(TotalItems / PageSize)`
- `HasPreviousPage` (bool): `CurrentPage > 1`
- `HasNextPage` (bool): `CurrentPage < TotalPages`

**Validation Rules**:
- `CurrentPage` must be >= 1
- `PageSize` must be > 0 and <= 100
- `TotalItems` must be >= 0

**State Transitions**:
1. **Initial**: `CurrentPage = 1`, `TotalItems` fetched from service
2. **PageChanged**: `CurrentPage` updated, list re-fetched
3. **FilterChanged**: `CurrentPage = 1` (reset to first page), `TotalItems` re-fetched

**Purpose**: Enables the pagination controls in the UI. The web site fetches paginated results from the service layer.

**State Transitions**: N/A (state holder, no domain state transitions)

### TenantEmailInfo

Represents a unique email address with job count for the drop-down.

**Fields**:
- `Email` (string): The tenant's email address
- `JobCount` (int): Number of non-deleted jobs for this email

**Purpose**: Populates the tenant drop-down with email addresses and their job counts.

**State Transitions**: N/A (read-only DTO)

## Relationships

```
TenantEmailInfo (1) ----< (N) JobViewModel
TenantEmailInfo (1) ----< (N) JobEntity
```

- Each email address maps to zero or more jobs
- The drop-down uses `TenantEmailInfo` for display
- The jobs list uses `JobViewModel` for the table view
- The modal uses the full `JobEntity` for detailed display

## Database Schema

No schema changes required. The web site uses the existing `JobEntity` table and its indexes:
- `Email` index (used for tenant scoping and drop-down population)
- Composite unique index on `(Email, EmployerJobId)` (unchanged)

The web site relies on the existing `IsDeleted` column for filtering deleted jobs.

## API Contracts

### JobsService Interface

```csharp
public interface IJobsService
{
    /// <summary>
    /// Gets a paginated list of non-deleted jobs for the specified tenant.
    /// </summary>
    /// <param name="email">The tenant's email address.</param>
    /// <param name="dateFrom">Optional filter: only jobs with PostedDate >= this value.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page (default: 20).</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>Paginated list of jobs ordered by Score descending.</returns>
    Task<PaginatedResult<JobViewModel>> GetJobsAsync(
        string email,
        DateTime? dateFrom = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a job by its internal ID.
    /// </summary>
    /// <param name="internalId">The job's internal ID.</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>True if the job was found and deleted, false otherwise.</returns>
    Task<bool> SoftDeleteJobAsync(Guid internalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full job entity by ID for modal display.
    /// </summary>
    /// <param name="internalId">The job's internal ID.</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>The full JobEntity, or null if not found.</returns>
    Task<JobEntity?> GetJobByIdAsync(Guid internalId, CancellationToken cancellationToken = default);
}
```

### TenantService Interface

```csharp
public interface ITenantService
{
    /// <summary>
    /// Gets all unique email addresses with non-deleted jobs, along with job counts.
    /// </summary>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>List of TenantEmailInfo ordered by email.</returns>
    Task<List<TenantEmailInfo>> GetTenantEmailsAsync(CancellationToken cancellationToken = default);
}
```
