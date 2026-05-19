using Microsoft.EntityFrameworkCore;
using Fidalgo.Agent.Configuration;

namespace Fidalgo.Agent.Storage;

/// <summary>
/// Repository for search configuration persistence.
/// </summary>
public class ConfigRepository
{
    private readonly JobDbContext _context;

    /// <summary>
    /// Creates a new instance of the ConfigRepository.
    /// </summary>
    /// <param name="context">The database context.</param>
    public ConfigRepository(JobDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves the active configuration for a user by email.
    /// </summary>
    /// <param name="email">The user's email address.</param>
    /// <returns>The active configuration, or null if none exists.</returns>
    public async Task<SearchConfiguration?> GetActiveConfigAsync(string email)
    {
        return await _context.SearchConfigurations
            .FirstOrDefaultAsync(c => c.UserEmail == email && c.IsActive);
    }

    /// <summary>
    /// Retrieves the configuration for a user by email (any status).
    /// </summary>
    /// <param name="email">The user's email address.</param>
    /// <returns>The configuration, or null if none exists.</returns>
    public async Task<SearchConfiguration?> GetConfigByEmailAsync(string email)
    {
        return await _context.SearchConfigurations
            .FirstOrDefaultAsync(c => c.UserEmail == email);
    }

    /// <summary>
    /// Saves or updates a search configuration.
    /// </summary>
    /// <param name="config">The configuration to save.</param>
    public async Task UpsertConfigAsync(SearchConfiguration config)
    {
        var existing = await _context.SearchConfigurations
            .FirstOrDefaultAsync(c => c.UserEmail == config.UserEmail);

        if (existing != null)
        {
            existing.Websites = config.Websites;
            existing.Keywords = config.Keywords;
            existing.IsActive = config.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            config.Id = Guid.NewGuid();
            config.CreatedAt = DateTime.UtcNow;
            config.UpdatedAt = DateTime.UtcNow;
            await _context.SearchConfigurations.AddAsync(config);
        }

        await _context.SaveChangesAsync();
    }
}
