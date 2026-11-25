using Fidalgo.Ingest.Configuration;
using Fidalgo.Ingest.Tools;
using Fidalgo.Shared.Pipeline;
using Fidalgo.Shared.Storage;
using Fidalgo.Shared.Tools;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Ingest;

public class ApiModeRunner
{
    private readonly AdzunaJobIngestionService _ingestionService;

    public ApiModeRunner(
        JobSearchTool jobSearchTool,
        JobInDbTool jobInDbTool,
        SaveJobTool saveJobTool,
        JobRepository repository,
        MarkdownFetchTool markdownFetchTool,
        ILogger<AdzunaJobIngestionService> logger)
    {
        _ = saveJobTool;
        _ingestionService = new AdzunaJobIngestionService(
            jobSearchTool,
            jobInDbTool,
            repository,
            markdownFetchTool,
            logger);
    }

    public Task RunAsync(CliOptions options, CancellationToken cancellationToken = default) =>
        _ingestionService.IngestAsync(options.Email, options.Keywords, options.ZipCode, cancellationToken);
}
