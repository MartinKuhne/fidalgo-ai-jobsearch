using Fidalgo.Shared.Models;

namespace Fidalgo.Host.Services;

/// <summary>
/// Contract for retrieving a list of all tenants with their job counts.
/// Implemented by TenantService to group non-deleted jobs by email.
/// </summary>
public interface ITenantService
{
    /// <summary>Retrieves all tenants with their associated job counts, ordered alphabetically.</summary>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>List of tenant email summaries.</returns>
    Task<List<TenantEmailInfo>> GetTenantEmailsAsync(CancellationToken cancellationToken = default);
}
