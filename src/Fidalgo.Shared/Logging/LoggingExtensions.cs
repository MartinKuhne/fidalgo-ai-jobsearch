using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Shared.Logging;

/// <summary>
/// Extension methods for configuring logging infrastructure.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Adds logging services to the service collection with default agent settings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgentLogging(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        return services;
    }
}