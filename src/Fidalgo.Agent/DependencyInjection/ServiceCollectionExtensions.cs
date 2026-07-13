using Fidalgo.Agent.Configuration;
using Fidalgo.Agent.DependencyInjection;
using Fidalgo.Shared.Logging;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.Sanitization;
using Fidalgo.Shared;
using Fidalgo.Shared.Storage;
using Fidalgo.Agent.Tools;
using Fidalgo.Shared.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Fidalgo.Shared.Configuration;
namespace Fidalgo.Agent.DependencyInjection;

/// <summary>
/// Central dependency injection registration for all agent services.
/// Configures database context, repositories, sanitizers, browser fetch tool, agent tools, and CLI configuration.
/// Provides a single entry point for bootstrapping the agent's service container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers all agent services with the service collection.</summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="databasePath">Optional custom path for the SQLite database file.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgentServices(this IServiceCollection services, string? databasePath = null)
    {
        services.AddAgentLogging();

        // Database
        var path = databasePath ?? Constants.GetDefaultDatabasePath();
        services.AddDbContext<JobDbContext>(options =>
            options.UseSqlite($"Data Source={path}"));

        // Repositories
        services.AddScoped<JobRepository>();

        // Services
        services.AddScoped<HtmlSanitizer>();
        services.AddScoped<HtmlStripper>();

        // Browser Fetch Tool
        services.AddOptions<BrowserFetchOptions>()
            .BindConfiguration("BrowserFetch")
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IBrowserFetchTool, BrowserFetchTool>();

        // HTTP client for external services
        services.AddSingleton<HttpClient>(sp =>
        {
            var handler = new TracingHttpMessageHandler(sp.GetRequiredService<ITraceContextProvider>())
            {
                InnerHandler = new SocketsHttpHandler()
            };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FidalgoAgent/1.0");
            return client;
        });

        // Adzuna Configuration
        services.AddOptions<AdzunaConfiguration>()
            .BindConfiguration("Adzuna")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Tools
        services.AddScoped<SaveJobTool>();
        services.AddScoped<GetJobsTool>();
        services.AddScoped<JobInDbTool>();
        services.AddScoped<WebSearchTool>();
        services.AddScoped<MarkdownFetchTool>();
        services.AddScoped<DelegateTool>();
        services.AddScoped<JobSearchTool>();

        // Configuration
        services.AddSingleton<CliOptions>();

        return services;
    }
}

/// <summary>Options class for browser fetch configuration.</summary>
public class BrowserFetchOptions
{
    /// <summary>Browser configuration settings.</summary>
    public BrowserConfiguration Configuration { get; set; } = new();
}