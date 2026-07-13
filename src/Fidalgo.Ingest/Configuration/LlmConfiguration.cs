using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

using Fidalgo.Shared.Tools;
namespace Fidalgo.Ingest.Configuration;

public class LlmConfiguration
{
    public const string ConfigurationSectionName = "LLM";

    [Required]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public string Model { get; set; } = string.Empty;

    public string ApiKey { get; set; } = "u-mkuhne";
}

public static class LlmConfigurationExtensions
{
    public static IServiceCollection AddLlmConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LlmConfiguration>()
            .Bind(configuration.GetSection(LlmConfiguration.ConfigurationSectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IOptions<LlmConfiguration>>().Value;
            return new Uri(config.Endpoint);
        });

        services.AddSingleton<ChatClient>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<LlmConfiguration>>().Value;
            var endpoint = sp.GetRequiredService<Uri>();
            return new ChatClient(config.Model, new ApiKeyCredential(config.ApiKey), new OpenAIClientOptions { Endpoint = endpoint });
        });

        return services;
    }
}