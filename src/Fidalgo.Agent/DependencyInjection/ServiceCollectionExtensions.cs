using Fidalgo.Agent.Configuration;
using Fidalgo.Agent.DependencyInjection;
using Fidalgo.Agent.Logging;
using Fidalgo.Agent.Models;
using Fidalgo.Agent.Prompts;
using Fidalgo.Agent.Sanitization;
using Fidalgo.Agent.Storage;
using Fidalgo.Agent.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

        // Browser Fetch Tool
        services.AddOptions<BrowserFetchOptions>()
            .BindConfiguration("BrowserFetch")
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IBrowserFetchTool, BrowserFetchTool>();

        // Tools
        services.AddScoped<SaveJobTool>();
        services.AddScoped<GetJobsTool>();

        // Configuration
        services.AddSingleton<CliOptions>();

        return services;
    }
}

public class BrowserFetchOptions
{
    public BrowserConfiguration Configuration { get; set; } = new();
}
