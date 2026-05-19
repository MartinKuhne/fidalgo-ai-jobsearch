using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fidalgo.Agent.Configuration;

public class LlmConfiguration
{
    public string Endpoint { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public static class LlmConfigurationExtensions
{
    public static IServiceCollection AddLlmConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmConfiguration>(configuration.GetSection("LLM"));
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IOptions<LlmConfiguration>>().Value;
            return new Uri(config.Endpoint);
        });
        return services;
    }
}
