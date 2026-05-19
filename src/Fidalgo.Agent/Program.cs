using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Fidalgo.Agent.Configuration;
using Fidalgo.Agent.DependencyInjection;
using Fidalgo.Agent.Tools;
using Fidalgo.Agent.Agents;
using Fidalgo.Agent.Storage;
using Fidalgo.Agent.Logging;
using Fidalgo.Agent.Logging.Configuration;
using Fidalgo.Agent.Tracing;
using Fidalgo.Agent.Tracing.Configuration;
using Fidalgo.Agent.ErrorHandling;
using Fidalgo.Agent.Retry;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAgentServices(builder.Configuration.GetSection("DatabasePath")?.Get<string>() ?? "jobs.db");
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

var email = args.FirstOrDefault(a => a.StartsWith("--email="))?.Split('=')[1];
var keywords = args.FirstOrDefault(a => a.StartsWith("--keywords="))?.Split('=')[1];
var resumePath = args.FirstOrDefault(a => a.StartsWith("--resume="))?.Split('=')[1];
var narrativePath = args.FirstOrDefault(a => a.StartsWith("--narrative="))?.Split('=')[1];
var queryJobs = args.Any(a => a == "--query-jobs");
var employerFilter = args.FirstOrDefault(a => a.StartsWith("--employer="))?.Split('=')[1];
var dateFromArg = args.FirstOrDefault(a => a.StartsWith("--date-from="))?.Split('=')[1];
var dateToArg = args.FirstOrDefault(a => a.StartsWith("--date-to="))?.Split('=')[1];
var sourceWebsiteFilter = args.FirstOrDefault(a => a.StartsWith("--source-website="))?.Split('=')[1];
var discardJobIdArg = args.FirstOrDefault(a => a.StartsWith("--discard-job="))?.Split('=')[1];
var listDiscarded = args.Any(a => a == "--list-discarded");

if (string.IsNullOrEmpty(email))
{
    Console.WriteLine("Error: --email is required");
    return 1;
}

   if (string.IsNullOrEmpty(keywords))
    {
        Console.WriteLine("Error: --keywords is required");
        return 1;
    }

if (string.IsNullOrEmpty(resumePath) && !queryJobs && string.IsNullOrEmpty(discardJobIdArg) && !listDiscarded)
{
    Console.WriteLine("Error: --resume is required");
    return 1;
}

if (!File.Exists(resumePath) && !queryJobs && string.IsNullOrEmpty(discardJobIdArg) && !listDiscarded)
{
    Console.WriteLine($"Error: Resume file not found: {resumePath}");
    return 1;
}

if (queryJobs)
{
    return RunQueryJobsMode(host, email, employerFilter, dateFromArg, dateToArg, sourceWebsiteFilter);
}

if (!string.IsNullOrEmpty(discardJobIdArg) && Guid.TryParse(discardJobIdArg, out var discardId))
{
    return RunDiscardJobMode(host, email, discardId);
}

if (listDiscarded)
{
    return RunListDiscardedMode(host, email);
}

return RunSearchMode(host, email, keywords, resumePath, !string.IsNullOrEmpty(narrativePath) ? narrativePath : null);

static int RunQueryJobsMode(IHost host, string email, string? employer, string? dateFrom, string? dateTo, string? sourceWebsite)
{
    var repository = ServiceProviderServiceExtensions.GetRequiredService<JobRepository>(host.Services);
    var tool = new GetJobsTool(repository);
    
    DateTime? from = null, to = null;
    if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out var df)) from = df;
    if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out var dt)) to = dt;

    var jobs = tool.GetAsync(email, from, to, employer, null, sourceWebsite).Result;
    
    Console.WriteLine($"Found {jobs.Count} jobs:");
    foreach (var job in jobs)
    {
        Console.WriteLine($"- [{job.Employer}] {job.EmployerJobId} (Score: {job.Score}, {job.Recommendation})");
    }
    
    return 0;
}

static int RunDiscardJobMode(IHost host, string email, Guid internalId)
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

static int RunSearchMode(IHost host, string email, string keywords, string resumePath, string? narrativePath)
{
    var llmConfig = ServiceProviderServiceExtensions.GetRequiredService<IOptions<LlmConfiguration>>(host.Services).Value;
    var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FidalgoAgent/1.0");
    
    var chatClient = new ChatClient(llmConfig.Model, new ApiKeyCredential(llmConfig.ApiKey), new OpenAIClientOptions { Endpoint = new Uri(llmConfig.Endpoint) });

    var fetchTool = new FetchTool(httpClient, new Fidalgo.Agent.Sanitization.HtmlSanitizer());
    var saveJobTool = new SaveJobTool(ServiceProviderServiceExtensions.GetRequiredService<JobRepository>(host.Services));
    var getJobsTool = new GetJobsTool(ServiceProviderServiceExtensions.GetRequiredService<JobRepository>(host.Services));

    var resumeContent = File.ReadAllText(resumePath);
    var narrativeContent = !string.IsNullOrEmpty(narrativePath) ? File.ReadAllText(narrativePath) : null;

    var agent = new JobSearchAgent(chatClient, fetchTool, saveJobTool, getJobsTool, email, resumeContent, narrativeContent);

    var result = agent.RunAsync(keywords).Result;
    
    Console.WriteLine($"Search complete. Processed {result} messages.");
    return 0;
}