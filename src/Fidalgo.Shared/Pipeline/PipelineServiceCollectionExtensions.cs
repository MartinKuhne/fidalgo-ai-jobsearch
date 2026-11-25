using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.Logging;
using Fidalgo.Shared.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fidalgo.Shared.Pipeline;

/// <summary>
/// Dependency injection registration for the continuous ingest -> evaluate -> packet pipeline.
/// </summary>
public static class PipelineServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="PipelineOptions"/>, optional <see cref="AdzunaConfiguration"/>,
    /// ingestion/evaluation tools, and <see cref="IJobPipelineRunner"/>.
    /// </summary>
    public static IServiceCollection AddFidalgoPipeline(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<PipelineOptions>()
            .Bind(configuration.GetSection(PipelineOptions.ConfigurationSectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AdzunaConfiguration>()
            .Bind(configuration.GetSection("Adzuna"));

        services.TryAddSingleton<ITraceContextProvider, TraceContextProvider>();
        services.TryAddSingleton(sp =>
        {
            var handler = new TracingHttpMessageHandler(sp.GetRequiredService<ITraceContextProvider>())
            {
                InnerHandler = new SocketsHttpHandler()
            };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FidalgoPipeline/1.0");
            return client;
        });

        services.TryAddScoped<JobSearchTool>();
        services.TryAddScoped<JobInDbTool>();
        services.TryAddScoped<MarkdownFetchTool>();

        services.AddScoped<IJobIngestionService, AdzunaJobIngestionService>();
        services.AddScoped<IJobEvaluationService, LlmJobEvaluationService>();
        services.AddScoped<IJobPipelineRunner, JobPipelineRunner>();

        return services;
    }
}
