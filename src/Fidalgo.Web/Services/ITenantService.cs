using Fidalgo.Shared.Models;

namespace Fidalgo.Web.Services;

public interface ITenantService
{
    Task<List<TenantEmailInfo>> GetTenantEmailsAsync(CancellationToken cancellationToken = default);
}
