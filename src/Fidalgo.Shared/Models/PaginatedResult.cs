namespace Fidalgo.Shared.Models;

/// <summary>
/// Immutable wrapper for paginated query results containing items and pagination metadata.
/// Provides computed TotalPages to support UI pagination controls.
/// Used by JobsService to return sliced job lists with count information.
/// </summary>
public sealed class PaginatedResult<T>
{
    /// <summary>Items on the current page.</summary>
    public List<T> Items { get; init; } = new();

    /// <summary>Total number of items across all pages.</summary>
    public int TotalItems { get; init; }

    /// <summary>Current page number (1-based).</summary>
    public int Page { get; init; }

    /// <summary>Number of items per page.</summary>
    public int PageSize { get; init; }

    /// <summary>Computed total number of pages.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalItems / (double)PageSize) : 0;
}