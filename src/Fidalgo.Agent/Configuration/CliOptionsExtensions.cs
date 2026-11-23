using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fidalgo.Agent.Configuration;

/// <summary>
/// Dependency injection extensions for registering and validating <see cref="CliOptions"/>.
/// </summary>
public static class CliOptionsExtensions
{
    /// <summary>
    /// Binds <see cref="CliOptions"/> from root configuration and enables startup validation.
    /// </summary>
    public static IServiceCollection AddCliOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CliOptions>()
            .Bind(configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
