using Microsoft.EntityFrameworkCore;

namespace Fidalgo.Agent.Storage;

/// <summary>
/// Provides database initialization and migration support.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Ensures the database is created and migrated to the latest version.
    /// </summary>
    /// <param name="context">The DbContext to use.</param>
    public static async Task EnsureDatabaseAsync(JobDbContext context)
    {
        if (context.Database.GetPendingMigrations().Any())
        {
            await context.Database.MigrateAsync();
        }
        else if (!await context.Database.CanConnectAsync())
        {
            await context.Database.MigrateAsync();
        }
    }
}
