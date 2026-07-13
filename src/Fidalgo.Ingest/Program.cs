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
using Fidalgo.Ingest;
using Fidalgo.Ingest.Configuration;
using Fidalgo.Ingest.DependencyInjection;
using Fidalgo.Ingest.Tools;
using Fidalgo.Shared.Tools;
using Fidalgo.Ingest.Agents;
using Fidalgo.Shared.Storage;
using Fidalgo.Shared.Logging;
using Fidalgo.Shared.Logging.Configuration;
using Fidalgo.Shared.Tracing;
using Fidalgo.Shared.Tracing.Configuration;
using Fidalgo.Shared.ErrorHandling;
using Fidalgo.Shared.Retry;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .CreateLogger();

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine("Fidalgo JobSearch AI Agent");
    Console.WriteLine("Usage: Fidalgo.Ingest [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --Email <email>              [Required] The user's email address");
    Console.WriteLine("  --Keywords <keywords>        [Required] Search keywords (e.g. 'software engineer')");
    Console.WriteLine("  --ZipCode <zip>              [Required] Target zip code");
    Console.WriteLine("  --QueryJobs true             Query mode: List saved jobs");
    Console.WriteLine("  --EmployerFilter <name>      Query mode: Filter jobs by employer");
    Console.WriteLine("  --DateFrom <date>            Query mode: Filter jobs from this date");
    Console.WriteLine("  --DateTo <date>              Query mode: Filter jobs to this date");
    Console.WriteLine("  --SourceWebsiteFilter <site> Query mode: Filter by job board source");
    Console.WriteLine("  --Api                        Mode: Read from Adzuna API without LLM (default)");
    Console.WriteLine("  --Scrape                     Mode: Use LLM and playwright to scrape jobs");
    Console.WriteLine("  --help, -h                   Show this help message");
    return 0;
}

var builder = Host.CreateApplicationBuilder(args);

string baseDir = AppContext.BaseDirectory;
builder.Configuration.AddJsonFile(Path.Combine(baseDir, "appsettings.json"), optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile(Path.Combine(baseDir, "appsettings.Development.json"), optional: true, reloadOnChange: true);
builder.Configuration.AddCommandLine(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

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
await db.Database.EnsureCreatedAsync();

var cliOptions = host.Services.GetRequiredService<IOptions<CliOptions>>().Value;

if (!cliOptions.QueryJobs)
{
    // Search mode doesn't require extra validation for ingest other than standard arguments.
}

if (cliOptions.QueryJobs)
{
    return RunQueryJobsMode(host, cliOptions);
}

return RunSearchMode(host, cliOptions);

static int RunQueryJobsMode(IHost host, CliOptions options)
{
    using var scope = host.Services.CreateScope();
    var repository = ServiceProviderServiceExtensions.GetRequiredService<JobRepository>(scope.ServiceProvider);
    var tool = ServiceProviderServiceExtensions.GetRequiredService<GetJobsTool>(scope.ServiceProvider);
    
    var jobs = tool.GetAsync(options.Email, options.DateFrom, options.DateTo, options.EmployerFilter, null, options.SourceWebsiteFilter).Result;
    
    Console.WriteLine($"Found {jobs.Count} jobs:");
    foreach (var job in jobs)
    {
        Console.WriteLine($"- [{job.Employer}] {job.EmployerJobId} (Score: {job.Score}, {job.Recommendation})");
    }
    
    return 0;
}


static int RunSearchMode(IHost host, CliOptions options)
{
    using var scope = host.Services.CreateScope();
    var serviceProvider = scope.ServiceProvider;

    var loggerFactory = ServiceProviderServiceExtensions.GetRequiredService<ILoggerFactory>(serviceProvider);
    var logger = loggerFactory.CreateLogger<JobSearchAgent>();

    var fetchTool = ServiceProviderServiceExtensions.GetRequiredService<IBrowserFetchTool>(serviceProvider);
    var saveJobTool = ServiceProviderServiceExtensions.GetRequiredService<SaveJobTool>(serviceProvider);
    var getJobsTool = ServiceProviderServiceExtensions.GetRequiredService<GetJobsTool>(serviceProvider);
    var jobInDbTool = ServiceProviderServiceExtensions.GetRequiredService<JobInDbTool>(serviceProvider);
    var webSearchTool = ServiceProviderServiceExtensions.GetRequiredService<WebSearchTool>(serviceProvider);
    var markdownFetchTool = ServiceProviderServiceExtensions.GetRequiredService<MarkdownFetchTool>(serviceProvider);
    
    if (options.Scrape)
    {
        var chatClient = ServiceProviderServiceExtensions.GetRequiredService<ChatClient>(serviceProvider);
        var delegateTool = ServiceProviderServiceExtensions.GetRequiredService<DelegateTool>(serviceProvider);

        var agent = new JobSearchAgent(chatClient, fetchTool, saveJobTool, getJobsTool, jobInDbTool, webSearchTool, markdownFetchTool, delegateTool, options.Email, options.ZipCode, logger);

        var result = agent.RunAsync(options.Keywords).Result;
        
        Console.WriteLine($"Scrape search complete. Processed {result} messages.");
    }
    else
    {
        var runner = ServiceProviderServiceExtensions.GetRequiredService<ApiModeRunner>(serviceProvider);
        runner.RunAsync(options).Wait();
    }
    return 0;
}