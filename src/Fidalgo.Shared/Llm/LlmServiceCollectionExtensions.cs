using System.ClientModel;
using Fidalgo.Shared.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace Fidalgo.Shared.Llm;

/// <summary>
/// Dependency injection extensions for registering the shared OpenAI-compatible LLM client and <see cref="ITextGenerator"/>.
/// </summary>
public static class LlmServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="LlmConfiguration"/>, <see cref="ChatClient"/>, and <see cref="ITextGenerator"/>.
    /// </summary>
    public static IServiceCollection AddFidalgoLlmClient(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<LlmConfiguration>()
            .Bind(configuration.GetSection(LlmConfiguration.ConfigurationSectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ApplicationPacketOptions>()
            .Bind(configuration.GetSection(ApplicationPacketOptions.ConfigurationSectionName));

        services.AddSingleton<ChatClient>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<LlmConfiguration>>().Value;
            return new ChatClient(
                config.Model,
                new ApiKeyCredential(config.ApiKey),
                new OpenAIClientOptions { Endpoint = new Uri(config.Endpoint) });
        });

        services.AddSingleton<ITextGenerator, OpenAiTextGenerator>();

        return services;
    }
}
