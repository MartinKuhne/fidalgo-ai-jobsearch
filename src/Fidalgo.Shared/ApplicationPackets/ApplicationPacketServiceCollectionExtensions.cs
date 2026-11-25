using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.Logging;
using Fidalgo.Shared.Pdf;
using Fidalgo.Shared.Tracing;
using Fidalgo.Shared.UserProfile;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fidalgo.Shared.ApplicationPackets;

/// <summary>
/// Dependency injection extensions for registering application packet services, profile loader,
/// PDF renderer, and file-system packet store.
/// </summary>
public static class ApplicationPacketServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ApplicationPacketOptions"/>, <see cref="UserProfileResolver"/>,
    /// <see cref="IUserProfileLoader"/>, <see cref="IPdfRenderer"/>, <see cref="IApplicationPacketStore"/>,
    /// and <see cref="IApplicationPacketService"/>.
    /// </summary>
    public static IServiceCollection AddApplicationPacketServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        FidalgoTracingListener.EnsureRegistered();

        services.AddOptions<ApplicationPacketOptions>()
            .Bind(configuration.GetSection(ApplicationPacketOptions.ConfigurationSectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<LlmConfiguration>()
            .Bind(configuration.GetSection(LlmConfiguration.ConfigurationSectionName));

        services.TryAddSingleton(configuration);
        services.TryAddSingleton<ITraceContextProvider, TraceContextProvider>();

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<ApplicationPacketOptions>>().Value;
            var baseDir = string.IsNullOrWhiteSpace(opts.ProfileDirectory) ? null : opts.ProfileDirectory;
            return new UserProfileResolver(
                sp.GetRequiredService<ILogger<UserProfileResolver>>(),
                baseDir);
        });

        services.AddSingleton<IUserProfileLoader, UserProfileLoader>();

        services.TryAddSingleton<OwnedMdToPdfHttpClient>();
        services.AddSingleton<IPdfRenderer>(sp => new MdToPdfRenderer(
            sp.GetRequiredService<OwnedMdToPdfHttpClient>().Client,
            sp.GetRequiredService<IOptions<ApplicationPacketOptions>>(),
            sp.GetRequiredService<ILogger<MdToPdfRenderer>>()));

        services.AddSingleton<IApplicationPacketStore, FileSystemApplicationPacketStore>();
        services.AddScoped<IApplicationPacketService, ApplicationPacketService>();

        return services;
    }
}
