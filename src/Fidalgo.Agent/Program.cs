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
using Fidalgo.Shared.Tools;
using Fidalgo.Agent.Agents;
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
    Console.WriteLine("Usage: Fidalgo.Agent [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --Email <email>              [Required] The user's email address");
    Console.WriteLine("  --Resume <path>              [Required] Path to resume text file");
    Console.WriteLine("  --QueryJobs true             Query mode: List saved jobs");
    Console.WriteLine("  --EmployerFilter <name>      Query mode: Filter jobs by employer");
    Console.WriteLine("  --DateFrom <date>            Query mode: Filter jobs from this date");
    Console.WriteLine("  --DateTo <date>              Query mode: Filter jobs to this date");
    Console.WriteLine("  --SourceWebsiteFilter <site> Query mode: Filter by job board source");
    Console.WriteLine("  --DiscardJobId <guid>        Discard mode: Mark a job as discarded");
    Console.WriteLine("  --ListDiscarded true         List mode: Show all discarded jobs");
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

static int RunDiscardJobMode(IHost host, Guid internalId)
{
    using var scope = host.Services.CreateScope();
    var repository = ServiceProviderServiceExtensions.GetRequiredService<JobRepository>(scope.ServiceProvider);
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
    using var scope = host.Services.CreateScope();
    var repository = ServiceProviderServiceExtensions.GetRequiredService<JobRepository>(scope.ServiceProvider);
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
    using var scope = host.Services.CreateScope();
    var serviceProvider = scope.ServiceProvider;

    var chatClient = ServiceProviderServiceExtensions.GetRequiredService<ChatClient>(serviceProvider);
    var repository = ServiceProviderServiceExtensions.GetRequiredService<JobRepository>(serviceProvider);
    var loggerFactory = ServiceProviderServiceExtensions.GetRequiredService<ILoggerFactory>(serviceProvider);
    var logger = loggerFactory.CreateLogger<JobEvaluationAgent>();

    var resumeContent = File.ReadAllText(options.Resume!);

    var agent = new JobEvaluationAgent(chatClient, repository, options.Email, resumeContent, logger);

    var jobs = repository.GetUntriagedAsync(options.Email).Result;
    Console.WriteLine($"Found {jobs.Count} untriaged jobs.");

    int totalProcessed = 0;
    foreach (var job in jobs)
    {
        Console.WriteLine($"Evaluating job: {job.Title} at {job.Employer}...");
        try
        {
            agent.EvaluateAsync(job).Wait();
            totalProcessed++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to evaluate job {job.InternalId}: {ex.Message}");
            logger.LogError(ex, "Failed to evaluate job {JobId}", job.InternalId);
        }
    }
    
    Console.WriteLine($"Evaluation complete. Processed {totalProcessed} jobs.");
    return 0;
}