using Fidalgo.Agent.Configuration;
using Fidalgo.Agent.DependencyInjection;
using Fidalgo.Agent.Logging;
using Fidalgo.Agent.Prompts;
using Fidalgo.Agent.Sanitization;
using Fidalgo.Agent.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fidalgo.Agent.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentServices(this IServiceCollection services, string databasePath = "jobs.db")
    {
        services.AddAgentLogging();

        // Database
        services.AddDbContext<JobDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));

        // Repositories
        services.AddScoped<JobRepository>();

        // Services
        services.AddScoped<HtmlSanitizer>();

        // Configuration
        services.AddSingleton<CliOptions>();

        return services;
    }
}
