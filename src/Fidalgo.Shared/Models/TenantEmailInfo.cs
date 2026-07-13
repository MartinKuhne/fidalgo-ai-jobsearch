namespace Fidalgo.Shared.Models;

/// <summary>
/// Immutable DTO summarizing a tenant's email address and associated job count.
/// Used by TenantService to produce a list of all tenants with their job totals.
/// </summary>
public record TenantEmailInfo(
    string Email,
    int JobCount);