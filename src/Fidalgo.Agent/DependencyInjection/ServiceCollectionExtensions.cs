using Fidalgo.Agent.Agents;
using Fidalgo.Agent.Configuration;
using Fidalgo.Agent.Logging;
using Fidalgo.Agent.Scraping;
using Fidalgo.Agent.Scraping.Scrapers;
using Fidalgo.Agent.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fidalgo.Agent.DependencyInjection;

/// <summary>
/// Extension methods for registering agent services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all agent services with the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="databasePath">Path to the SQLite database file.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgentServices(this IServiceCollection services, string databasePath = "jobs.db")
    {
        services.AddAgentLogging();

        // Database
        services.AddDbContext<JobDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));

        // Repositories
        services.AddScoped<ConfigRepository>();
        services.AddScoped<JobRepository>();
        services.AddScoped<DuplicateDetector>();
        services.AddScoped<JobQueryService>();

        // Services
        services.AddScoped<ConfigurationService>();
        services.AddScoped<SearchOrchestrator>();
        services.AddScoped<HttpClientFactoryService>();

        // Scraper Registry
        services.AddSingleton<WebsiteScraperRegistry>(sp =>
        {
            var factory = sp.GetRequiredService<HttpClientFactoryService>();
            var registry = new WebsiteScraperRegistry();
            registry.RegisterAll(
                () => new GovernmentJobsScraper(factory.CreateClient()),
                () => new GoogleScraper(factory.CreateClient()),
                () => new GlassdoorScraper(factory.CreateClient()),
                () => new MonsterScraper(factory.CreateClient()),
                () => new IndeedScraper(factory.CreateClient()),
                () => new LinkedInScraper(factory.CreateClient())
            );
            return registry;
        });

        // CLI Handlers
        services.AddScoped<ConfigCommandHandler>();

        return services;
    }
}
