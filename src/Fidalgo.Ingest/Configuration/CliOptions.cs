using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

using Fidalgo.Shared.Tools;
using Fidalgo.Shared.UserProfile;
namespace Fidalgo.Ingest.Configuration;

public record CliOptions
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Keywords { get; set; } = string.Empty;

    [Required]
    public string ZipCode { get; set; } = string.Empty;
    public string[]? JobSearchKeywords { get; set; }
    public bool QueryJobs { get; set; }
    public string? EmployerFilter { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? SourceWebsiteFilter { get; set; }
    public bool Api { get; set; } = true;
    public bool Scrape { get; set; }
}

public static class CliOptionsExtensions
{
    public static IServiceCollection AddCliOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CliOptions>()
            .Bind(configuration)
            .Configure(options =>
            {
                // Post-bind step: if --Keywords was not supplied, fold the
                // JobSearchKeywords array from the user config file into the
                // Keywords string. Runs after Bind and before the [Required]
                // validation, so Keywords is non-empty when validation runs.
                var resolved = UserProfileConfig.ResolveKeywords(options.Keywords, options.JobSearchKeywords);
                if (resolved is not null)
                {
                    options.Keywords = resolved;
                }
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}