using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Host.Services;

/// <summary>
/// Service that groups non-deleted jobs by tenant email and counts them.
/// Returns an alphabetically ordered list of tenant summaries for the UI.
/// Provides structured error handling with logging.
/// </summary>
public class TenantService : ITenantService
{
    private readonly JobDbContext _context;
    private readonly ILogger<TenantService> _logger;

    /// <summary>Initializes a new instance of the TenantService.</summary>
    /// <param name="context">The EF Core DbContext for database access.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public TenantService(JobDbContext context, ILogger<TenantService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Retrieves all tenants with their associated job counts, ordered alphabetically.</summary>
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
