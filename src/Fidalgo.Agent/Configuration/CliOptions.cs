using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace Fidalgo.Agent.Configuration;

public record CliOptions
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Keywords { get; set; } = string.Empty;

    public string ResumePath { get; set; } = string.Empty;
    public string? NarrativePath { get; set; }
    public string Location { get; set; } = "United States";
    [Required]
    public string ZipCode { get; set; } = string.Empty;
    public bool QueryJobs { get; set; }
    public string? EmployerFilter { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? SourceWebsiteFilter { get; set; }
    public Guid? DiscardJobId { get; set; }
    public bool ListDiscarded { get; set; }
}

public static class CliOptionsExtensions
{
    public static IServiceCollection AddCliOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CliOptions>()
            .Bind(configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
