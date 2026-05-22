# Contracts: Job Browse Web Site API

**Feature**: 006-job-browse-web
**Branch**: 006-job-browse-web
**Date**: 2026-05-22

## JobsService Contract

### `GetJobsAsync`

Retrieves a paginated list of non-deleted jobs for a specific tenant.

**Method Signature**:
```csharp
Task<PaginatedResult<JobViewModel>> GetJobsAsync(
    string email,
    DateTime? dateFrom = null,
    int page = 1,
    int pageSize = 20,
    CancellationToken cancellationToken = default);
```

**Parameters**:
| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `email` | string | Yes | - | The tenant's email address (scopes the query) |
| `dateFrom` | DateTime? | No | null | Filter: only include jobs with `PostedDate >= dateFrom` |
| `page` | int | No | 1 | Page number (1-based) |
| `pageSize` | int | No | 20 | Number of items per page (max: 100) |
| `cancellationToken` | CancellationToken | No | default | Cancellation token |

**Returns**: `PaginatedResult<JobViewModel>` containing:
- `Items`: List of `JobViewModel` (ordered by `Score` descending)
- `TotalItems`: Total count of matching jobs (ignoring pagination)
- `Page`: Current page number
- `PageSize`: Items per page
- `TotalPages`: Calculated total pages

**Errors**:
- Throws `ArgumentNullException` if `email` is null or empty
- Throws `ArgumentOutOfRangeException` if `page < 1` or `pageSize < 1` or `pageSize > 100`

**Notes**:
- Deleted jobs (`IsDeleted = true`) are always excluded
- Results are always ordered by `Score` descending (suitability rating)
- The `dateFrom` filter uses `PostedDate` as the date basis

---

### `SoftDeleteJobAsync`

Marks a job as deleted (soft delete).

**Method Signature**:
```csharp
Task<bool> SoftDeleteJobAsync(
    Guid internalId,
    CancellationToken cancellationToken = default);
```

**Parameters**:
| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `internalId` | Guid | Yes | - | The job's internal ID |
| `cancellationToken` | CancellationToken | No | default | Cancellation token |

**Returns**: `true` if the job was found and marked as deleted; `false` if not found

**Errors**:
- Throws `ArgumentNullException` if `internalId` is default (Guid.Empty)

**Notes**:
- Sets `IsDeleted = true` on the job entity
- The job remains in the database but is excluded from all list queries
- Idempotent: calling multiple times with the same ID is safe

---

### `GetJobByIdAsync`

Retrieves the full job entity for modal display.

**Method Signature**:
```csharp
Task<JobEntity?> GetJobByIdAsync(
    Guid internalId,
    CancellationToken cancellationToken = default);
```

**Parameters**:
| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `internalId` | Guid | Yes | - | The job's internal ID |
| `cancellationToken` | CancellationToken | No | default | Cancellation token |

**Returns**: The full `JobEntity` if found, `null` if not found

**Errors**: None (returns null for missing jobs rather than throwing)

**Notes**:
- Returns the complete job entity including all fields (description, pros, cons, resume hints, salary range, etc.)
- Does not check `IsDeleted` - allows viewing details of deleted jobs if needed

---

## TenantService Contract

### `GetTenantEmailsAsync`

Gets all unique email addresses that have non-deleted jobs.

**Method Signature**:
```csharp
Task<List<TenantEmailInfo>> GetTenantEmailsAsync(
    CancellationToken cancellationToken = default);
```

**Parameters**: None (beyond cancellation token)

**Returns**: `List<TenantEmailInfo>` ordered alphabetically by email, where each entry contains:
- `Email`: The unique email address
- `JobCount`: Number of non-deleted jobs for that email

**Errors**: None

**Notes**:
- Uses the `Email` index on the Jobs table for efficient querying
- Only includes emails that have at least one non-deleted job
- Job count reflects non-deleted jobs only
