using System.Diagnostics;
using Fidalgo.Host.ErrorHandling;
using Fidalgo.Host.Models;
using Fidalgo.Host.Services;
using Fidalgo.Shared;
using Fidalgo.Shared.ApplicationPackets;
using Fidalgo.Shared.ApplicationPackets.Prompts;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.ErrorHandling;
using Fidalgo.Shared.Llm;
using Fidalgo.Shared.Logging;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;
using Fidalgo.Shared.UserProfile;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

var disableUserProfileConfig =
    !builder.Configuration.GetValue("UseUserProfileConfig", true) ||
    string.Equals(
        Environment.GetEnvironmentVariable("FIDALGO_DISABLE_USER_PROFILE_CONFIG"),
        "true",
        StringComparison.OrdinalIgnoreCase);

if (!disableUserProfileConfig)
{
    builder.Configuration.AddUserProfileConfig();

    // Place user profile config.json before environment variables and command-line args
    // so it provides server defaults that env vars and CLI flags can still override.
    var addedSource = builder.Configuration.Sources[^1];
    var envOrCliIndex = -1;
    for (var i = 0; i < builder.Configuration.Sources.Count - 1; i++)
    {
        if (builder.Configuration.Sources[i] is EnvironmentVariablesConfigurationSource
            or CommandLineConfigurationSource)
        {
            envOrCliIndex = i;
            break;
        }
    }

    if (envOrCliIndex >= 0)
    {
        builder.Configuration.Sources.RemoveAt(builder.Configuration.Sources.Count - 1);
        builder.Configuration.Sources.Insert(envOrCliIndex, addedSource);
    }
}

var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var logDirectory = string.IsNullOrWhiteSpace(appDataRoot)
    ? Path.Combine(AppContext.BaseDirectory, "fidalgo", "logs")
    : Path.Combine(appDataRoot, "fidalgo", "logs");
Directory.CreateDirectory(logDirectory);
var logFilePath = Path.Combine(logDirectory, "log-.json");

builder.Services.AddSerilog(
    (services, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
        .WriteTo.Console()
        .WriteTo.File(
            new CompactJsonFormatter(),
            logFilePath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            shared: true),
    preserveStaticLogger: true);

var configuredDbPath = builder.Configuration["DatabasePath"];
var dbPath = string.IsNullOrWhiteSpace(configuredDbPath)
    ? Constants.GetDefaultDatabasePath()
    : configuredDbPath;

builder.Services.AddDbContext<JobDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<JobRepository>();
builder.Services.AddScoped<IJobsService, JobsService>();
builder.Services.AddScoped<ITenantService, TenantService>();

builder.Services.AddFidalgoLlmClient(builder.Configuration);
builder.Services.AddApplicationPacketServices(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.Use(async (httpContext, next) =>
{
    Activity? startedActivity = null;
    var activity = Activity.Current;
    if (activity is null)
    {
        startedActivity = new Activity("Fidalgo.Host.Request");
        startedActivity.SetIdFormat(ActivityIdFormat.W3C);
        startedActivity.Start();
        activity = startedActivity;
    }

    var traceId = activity.TraceId != default
        ? activity.TraceId.ToHexString()
        : Guid.NewGuid().ToString("N");
    var spanId = activity.SpanId != default
        ? activity.SpanId.ToHexString()
        : Guid.NewGuid().ToString("N")[..16];
    var parentSpanId = activity.ParentSpanId != default
        ? activity.ParentSpanId.ToHexString()
        : null;

    var traceContextProvider = httpContext.RequestServices.GetRequiredService<ITraceContextProvider>();
    traceContextProvider.SetCurrentContext(new TraceContext(
        TraceId: traceId,
        SpanId: spanId,
        ParentSpanId: parentSpanId,
        CorrelationId: traceId,
        Timestamp: DateTime.UtcNow,
        IsRoot: parentSpanId is null));

    using var correlationScope = Serilog.Context.LogContext.PushProperty("CorrelationId", traceId);
    try
    {
        await next(httpContext);
    }
    finally
    {
        traceContextProvider.SetCurrentContext(null!);
        startedActivity?.Dispose();
    }
});

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();

// Jobs API
app.MapGet("/api/jobs", async (
    IJobsService jobsService,
    [FromQuery] string email,
    [FromQuery] DateTime? dateFrom,
    [FromQuery] int page,
    [FromQuery] int pageSize,
    [FromQuery] string? search,
    [FromQuery] string? sortBy,
    [FromQuery] string? sortDir,
    CancellationToken cancellationToken) =>
{
    var result = await jobsService.GetJobsAsync(email, dateFrom, page, pageSize, search, sortBy, sortDir, cancellationToken);
    return Results.Ok(result);
});

app.MapGet("/api/jobs/{id:guid}", async (IJobsService jobsService, Guid id, CancellationToken cancellationToken) =>
{
    var job = await jobsService.GetJobByIdAsync(id, cancellationToken);
    return job is not null ? Results.Ok(job) : Results.NotFound();
});

app.MapDelete("/api/jobs/{id:guid}", async (IJobsService jobsService, Guid id, CancellationToken cancellationToken) =>
{
    var result = await jobsService.SoftDeleteJobAsync(id, cancellationToken);
    return result ? Results.Ok() : Results.NotFound();
});

// Tenants API
app.MapGet("/api/tenants", async (ITenantService tenantService, CancellationToken cancellationToken) =>
{
    var tenants = await tenantService.GetTenantEmailsAsync(cancellationToken);
    return Results.Ok(tenants);
});

// Profile defaults API
app.MapGet("/api/profile/defaults", async (
    UserProfileResolver resolver,
    IOptions<ApplicationPacketOptions> packetOptions,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var resumePath = resolver.ResolveResumePath(null);
    var storyPath = resolver.ResolveCareerStoryPath(null);
    var goalsPath = resolver.ResolveCareerGoalsPath(null);

    var resumeMarkdown = await ReadOptionalProfileFileAsync(resumePath, cancellationToken);
    var storyMarkdown = await ReadOptionalProfileFileAsync(storyPath, cancellationToken);
    var goalsMarkdown = await ReadOptionalProfileFileAsync(goalsPath, cancellationToken);

    var defaultEmail = configuration["Email"];
    if (string.IsNullOrWhiteSpace(defaultEmail))
    {
        defaultEmail = null;
    }

    var outputDirectory = ApplicationPacketPaths.ResolveBaseOutputDirectory(null, packetOptions.Value);

    return Results.Ok(new ProfileDefaultsDto(
        DefaultEmail: defaultEmail,
        HasResume: resumeMarkdown is not null,
        HasCareerStory: storyMarkdown is not null,
        CareerStoryLength: storyMarkdown?.Length ?? 0,
        HasCareerGoals: goalsMarkdown is not null,
        CareerGoalsLength: goalsMarkdown?.Length ?? 0,
        BaseResumeMarkdown: resumeMarkdown,
        CareerStoryMarkdown: storyMarkdown,
        CareerGoalsMarkdown: goalsMarkdown,
        OutputDirectory: outputDirectory));
});

// Application Packet APIs
app.MapGet("/api/jobs/{id:guid}/packet", async (
    Guid id,
    IJobsService jobsService,
    IApplicationPacketStore packetStore,
    IOptions<ApplicationPacketOptions> packetOptions,
    CancellationToken cancellationToken) =>
{
    if (id == Guid.Empty)
    {
        return Results.NotFound();
    }

    var job = await jobsService.GetJobByIdAsync(id, cancellationToken);
    if (job is null || job.IsDeleted)
    {
        return Results.NotFound();
    }

    var baseOutputDir = ApplicationPacketPaths.ResolveBaseOutputDirectory(null, packetOptions.Value);
    var status = await packetStore.GetStatusAsync(baseOutputDir, job, cancellationToken);

    return Results.Ok(new ApplicationPacketStatusDto(
        Exists: status.Exists,
        FolderName: status.FolderName,
        DirectoryPath: status.DirectoryPath,
        GeneratedAtUtc: status.GeneratedAtUtc,
        Files: BuildPacketFilesDto(id),
        ResumeMarkdown: status.ResumeMarkdown,
        CoverLetterMarkdown: status.CoverLetterMarkdown));
});

app.MapPost("/api/jobs/{id:guid}/packet", async (
    Guid id,
    [FromBody] GeneratePacketRequest? request,
    IApplicationPacketService packetService,
    CancellationToken cancellationToken) =>
{
    var overrides = new UserProfileLoadOverrides(
        ResumeContentOverride: string.IsNullOrWhiteSpace(request?.BaseResumeOverride) ? null : request.BaseResumeOverride,
        CareerStoryContentOverride: string.IsNullOrWhiteSpace(request?.CareerStoryOverride) ? null : request.CareerStoryOverride,
        CareerGoalsContentOverride: string.IsNullOrWhiteSpace(request?.CareerGoalsOverride) ? null : request.CareerGoalsOverride);

    var force = request?.Force ?? false;
    var generationRequest = new ApplicationPacketGenerationRequest(
        ProfileOverrides: overrides,
        ForceRegenerate: force,
        SkipExisting: !force);

    var result = await packetService.GenerateForJobAsync(id, generationRequest, cancellationToken);

    return Results.Ok(new ApplicationPacketResultDto(
        Outcome: result.Outcome.ToString(),
        JobId: result.JobId,
        FolderName: result.FolderName,
        DirectoryPath: result.DirectoryPath,
        GeneratedAtUtc: result.GeneratedAtUtc,
        Files: BuildPacketFilesDto(result.JobId),
        ResumeMarkdown: result.ResumeMarkdown,
        CoverLetterMarkdown: result.CoverLetterMarkdown));
});

app.MapGet("/api/jobs/{id:guid}/packet/files/{fileName}", async (
    Guid id,
    string fileName,
    [FromQuery] bool? download,
    IJobsService jobsService,
    IApplicationPacketStore packetStore,
    IOptions<ApplicationPacketOptions> packetOptions,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (!ApplicationPacketPaths.IsAllowedPacketFileName(fileName))
    {
        throw new ValidationException(
            "Invalid packet file name.",
            new Dictionary<string, string[]>
            {
                ["fileName"] = [$"File '{fileName}' is not an allowed application packet file."]
            });
    }

    if (id == Guid.Empty)
    {
        return Results.NotFound();
    }

    var job = await jobsService.GetJobByIdAsync(id, cancellationToken);
    if (job is null || job.IsDeleted)
    {
        return Results.NotFound();
    }

    var baseOutputDir = ApplicationPacketPaths.ResolveBaseOutputDirectory(null, packetOptions.Value);
    var stream = await packetStore.OpenPacketFileAsync(baseOutputDir, job, fileName, cancellationToken);
    if (stream is null)
    {
        return Results.NotFound();
    }

    var canonicalFileName = fileName.ToLowerInvariant();
    var contentType = canonicalFileName.EndsWith(".pdf", StringComparison.Ordinal)
        ? "application/pdf"
        : "text/markdown; charset=utf-8";

    httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";

    if (download ?? false)
    {
        return Results.File(stream, contentType, fileDownloadName: canonicalFileName);
    }

    httpContext.Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
    {
        FileName = canonicalFileName
    }.ToString();
    return Results.File(stream, contentType);
});

// Legacy Resume API (delegates to ITextGenerator + IUserProfileLoader with server resume fallback)
app.MapPost("/api/jobs/{id:guid}/resume", async (
    Guid id,
    [FromBody] ResumeRequest? request,
    IJobsService jobsService,
    IUserProfileLoader profileLoader,
    ITextGenerator textGenerator,
    IOptions<ApplicationPacketOptions> packetOptions,
    CancellationToken cancellationToken) =>
{
    if (id == Guid.Empty)
    {
        return Results.NotFound();
    }

    var job = await jobsService.GetJobByIdAsync(id, cancellationToken);
    if (job is null || job.IsDeleted)
    {
        return Results.NotFound();
    }

    var baseResumeOverride = string.IsNullOrWhiteSpace(request?.BaseResume)
        ? null
        : request.BaseResume;

    var profile = await profileLoader.LoadAsync(
        new UserProfileLoadOverrides(ResumeContentOverride: baseResumeOverride),
        cancellationToken);

    var llmTimeout = TimeSpan.FromSeconds(Math.Max(1, packetOptions.Value.LlmTimeoutSeconds));
    var rawResume = await textGenerator.GenerateAsync(
        ResumeTailoringPromptBuilder.BuildSystemPrompt(),
        ResumeTailoringPromptBuilder.BuildUserPrompt(profile, job),
        new TextGenerationOptions(Timeout: llmTimeout),
        cancellationToken);

    var updatedResume = MarkdownResponseNormalizer.Normalize(rawResume);
    return Results.Ok(new { UpdatedResume = updatedResume });
});

// Ensure any SPA routes not mapped to API fall back to index.html
app.MapFallbackToFile("index.html");

// Startup/shutdown lifecycle logs (NFR-005) and browser auto-launch
app.Lifetime.ApplicationStarted.Register(() =>
{
    app.Logger.LogInformation("Fidalgo.Host started.");

    if (app.Environment.IsEnvironment("Testing"))
    {
        return;
    }

    var url = app.Urls.FirstOrDefault(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase));
    if (url is not null)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Failed to auto-launch browser for {Url}", url);
        }
    }
});

app.Lifetime.ApplicationStopping.Register(() =>
{
    app.Logger.LogInformation("Fidalgo.Host stopping.");
});

app.Run();

static async Task<string?> ReadOptionalProfileFileAsync(string? path, CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
    {
        return null;
    }

    var content = await File.ReadAllTextAsync(path, cancellationToken);
    return string.IsNullOrWhiteSpace(content) ? null : content;
}

static PacketFilesDto BuildPacketFilesDto(Guid jobId) => new(
    ResumePdfUrl: $"/api/jobs/{jobId}/packet/files/{ApplicationPacketPaths.ResumePdfFileName}",
    CoverLetterPdfUrl: $"/api/jobs/{jobId}/packet/files/{ApplicationPacketPaths.CoverLetterPdfFileName}",
    JobPostingUrl: $"/api/jobs/{jobId}/packet/files/{ApplicationPacketPaths.JobPostingFileName}");

public partial class Program { }
