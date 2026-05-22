using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Hosting;
using Fidalgo.Agent.Configuration;
using Fidalgo.Agent.DependencyInjection;
using Fidalgo.Agent.Tools;
using Fidalgo.Agent.Agents;
using Fidalgo.Shared.Storage;
using Fidalgo.Agent.Logging;
using Fidalgo.Agent.Logging.Configuration;
using Fidalgo.Agent.Tracing;
using Fidalgo.Agent.Tracing.Configuration;
using Fidalgo.Agent.ErrorHandling;
using Fidalgo.Agent.Retry;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);

string baseDir = AppContext.BaseDirectory;
builder.Configuration.AddJsonFile(Path.Combine(baseDir, "appsettings.json"), optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile(Path.Combine(baseDir, "appsettings.Development.json"), optional: true, reloadOnChange: true);
builder.Configuration.AddCommandLine(args);

builder.Services.AddSerilog(Log.Logger);
builder.Services.AddCliOptions(builder.Configuration);
builder.Services.AddAgentServices(builder.Configuration.GetSection("DatabasePath")?.Get<string>());
builder.Services.AddLlmConfiguration(builder.Configuration);

builder.Services.AddSingleton<ILoggingConfiguration, LoggingConfiguration>();
builder.Services.AddSingleton<ITracingConfiguration, TracingConfiguration>();
builder.Services.AddSingleton<ITraceContextProvider, TraceContextProvider>();
builder.Services.AddSingleton<ILogEntryWriter, LogEntryWriter>();
builder.Services.AddSingleton<ISpanFactory, SpanFactory>();
builder.Services.AddSingleton<IExceptionMapper, ExceptionMapper>();
builder.Services.AddSingleton<IRetryPolicy, RetryPolicy>();
builder.Services.AddSingleton<ITraceContextPropagator, TraceContextPropagator>();
builder.Services.AddSingleton<IOtlpExporter, OtlpExporter>();

var host = builder.Build();

using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
await db.Database.MigrateAsync();

var cliOptions = host.Services.GetRequiredService<IOptions<CliOptions>>().Value;

if (string.IsNullOrEmpty(cliOptions.Resume) && !cliOptions.QueryJobs && cliOptions.DiscardJobId is null && !cliOptions.ListDiscarded)
{
    Console.WriteLine("Error: --resume is required");
    return 1;
}

if (!string.IsNullOrEmpty(cliOptions.Resume) && !cliOptions.QueryJobs && cliOptions.DiscardJobId is null && !cliOptions.ListDiscarded)
{
    if (!File.Exists(cliOptions.Resume))
    {
        Console.WriteLine($"Error: Resume file not found: {cliOptions.Resume}");
        return 1;
    }
}

if (cliOptions.QueryJobs)
{
    return RunQueryJobsMode(host, cliOptions);
}

if (cliOptions.DiscardJobId is not null)
{
    return RunDiscardJobMode(host, cliOptions.DiscardJobId.Value);
}

if (cliOptions.ListDiscarded)
{
    return RunListDiscardedMode(host, cliOptions.Email);
}

return RunSearchMode(host, cliOptions);

static int RunQueryJobsMode(IHost host, CliOptions options)
{
    var repository = ServiceProviderServiceExtensions.GetRequiredService<JobRepository>(host.Services);
    var tool = ServiceProviderServiceExtensions.GetRequiredService<GetJobsTool>(host.Services);
    
    var jobs = tool.GetAsync(options.Email, options.DateFrom, options.DateTo, options.EmployerFilter, null, options.SourceWebsiteFilter).Result;
    
    Console.WriteLine($"Found {jobs.Count} jobs:");
    foreach (var job in jobs)
    {
        Console.WriteLine($"- [{job.Employer}] {job.EmployerJobId} (Score: {job.Score}, {job.Recommendation})");
    }
    
    return 0;
}

static int RunDiscardJobMode(IHost host, Guid internalId)
{
    var repository = ServiceProviderServiceExtensions.GetRequiredService<JobRepository>(host.Services);
    var success = repository.SoftDeleteAsync(internalId).Result;
    
    if (success)
    {
        Console.WriteLine($"Job {internalId} marked as discarded");
        return 0;
    }
    else
    {
        Console.WriteLine($"Job {internalId} not found");
        return 1;
    }
}

static int RunListDiscardedMode(IHost host, string email)
{
    var repository = ServiceProviderServiceExtensions.GetRequiredService<JobRepository>(host.Services);
    var jobs = repository.GetDiscardedAsync(email).Result;
    
    Console.WriteLine($"Found {jobs.Count} discarded jobs:");
    foreach (var job in jobs)
    {
        Console.WriteLine($"- [{job.Employer}] {job.EmployerJobId} (Score: {job.Score}, {job.Recommendation})");
    }
    
    return 0;
}

static int RunSearchMode(IHost host, CliOptions options)
{
    var llmConfig = ServiceProviderServiceExtensions.GetRequiredService<IOptions<LlmConfiguration>>(host.Services).Value;
    var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FidalgoAgent/1.0");
    
    var chatClient = new ChatClient(llmConfig.Model, new ApiKeyCredential(llmConfig.ApiKey), new OpenAIClientOptions { Endpoint = new Uri(llmConfig.Endpoint) });

    var fetchTool = ServiceProviderServiceExtensions.GetRequiredService<IBrowserFetchTool>(host.Services);
    var saveJobTool = ServiceProviderServiceExtensions.GetRequiredService<SaveJobTool>(host.Services);
    var getJobsTool = ServiceProviderServiceExtensions.GetRequiredService<GetJobsTool>(host.Services);
    var jobInDbTool = ServiceProviderServiceExtensions.GetRequiredService<JobInDbTool>(host.Services);

    var resumeContent = File.ReadAllText(options.Resume);

    var loggerFactory = ServiceProviderServiceExtensions.GetRequiredService<ILoggerFactory>(host.Services);
    var logger = loggerFactory.CreateLogger<JobSearchAgent>();

    var agent = new JobSearchAgent(chatClient, fetchTool, saveJobTool, getJobsTool, jobInDbTool, options.Email, resumeContent, options.ZipCode, logger);

    var result = agent.RunAsync(options.Keywords).Result;
    
    Console.WriteLine($"Search complete. Processed {result} messages.");
    return 0;
}
