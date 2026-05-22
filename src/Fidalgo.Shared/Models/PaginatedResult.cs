namespace Fidalgo.Shared.Models;

public class PaginatedResult<T>
{
    public List<T> Items { get; init; } = new();
    public int TotalItems { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalItems / (double)PageSize) : 0;
}
