using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Web.Services;

public class TenantService : ITenantService
{
    private readonly JobDbContext _context;
    private readonly ILogger<TenantService> _logger;

    public TenantService(JobDbContext context, ILogger<TenantService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<TenantEmailInfo>> GetTenantEmailsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching tenant emails");

            var jobs = await _context.Jobs
                .Where(j => !j.IsDeleted && !string.IsNullOrEmpty(j.Email))
                .ToListAsync(cancellationToken);

            var tenantInfo = jobs
                .GroupBy(j => j.Email)
                .Select(g => new TenantEmailInfo(
                    g.Key!,
                    g.Count()))
                .OrderBy(t => t.Email)
                .ToList();

            _logger.LogInformation("Found {TenantCount} tenants with jobs", tenantInfo.Count);

            return tenantInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tenant emails");
            throw new InvalidOperationException("Failed to load tenant list. Please try again later.", ex);
        }
    }
}
