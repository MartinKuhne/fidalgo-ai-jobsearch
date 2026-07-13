using System.Diagnostics;
using Fidalgo.Host.Services;
using Fidalgo.Shared;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Add services
var dbPath = Constants.GetDefaultDatabasePath();
builder.Services.AddDbContext<JobDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<JobRepository>();
builder.Services.AddScoped<IJobsService, JobsService>();
builder.Services.AddScoped<ITenantService, TenantService>();

var llmEndpoint = builder.Configuration["LLM:Endpoint"] ?? "";
var llmModel = builder.Configuration["LLM:Model"] ?? "";
var llmApiKey = builder.Configuration["LLM:ApiKey"] ?? "";
builder.Services.AddSingleton<ChatClient>(sp => new ChatClient(llmModel, new ApiKeyCredential(llmApiKey), new OpenAIClientOptions { Endpoint = new Uri(llmEndpoint) }));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Jobs API
app.MapGet("/api/jobs", async (IJobsService jobsService, [FromQuery] string email, [FromQuery] DateTime? dateFrom, [FromQuery] int page, [FromQuery] int pageSize, [FromQuery] string? search, [FromQuery] string? sortBy, [FromQuery] string? sortDir, CancellationToken cancellationToken) =>
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

// Resume API
app.MapPost("/api/jobs/{id:guid}/resume", async (Guid id, [FromBody] ResumeRequest request, IJobsService jobsService, ChatClient chatClient, CancellationToken cancellationToken) =>
{
    var job = await jobsService.GetJobByIdAsync(id, cancellationToken);
    if (job == null) return Results.NotFound();

    var prompt = $@"
You are an expert resume writer. The user wants to apply for a job.
Here is the job description:
{job.Description}

Here is the user's current base resume:
{request.BaseResume}

Please generate an updated, tailored version of the resume in Markdown format that emphasizes the user's skills and experiences most relevant to the job description. Do not invent fake experience, just highlight and rephrase what is already there. Return ONLY the markdown resume.
";

    var messages = new List<OpenAI.Chat.ChatMessage> { new UserChatMessage(prompt) };
    var completion = await chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
    return Results.Ok(new { UpdatedResume = completion.Value.Content[0].Text });
});

// Ensure any SPA routes not mapped to API fall back to index.html
app.MapFallbackToFile("index.html");

// Auto-launch browser
app.Lifetime.ApplicationStarted.Register(() =>
{
    var url = app.Urls.FirstOrDefault(u => u.StartsWith("http://"));
    if (url != null)
    {
        try
        {
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
        }
        catch { /* Ignore */ }
    }
});

app.Run();

public partial class Program { }

public record ResumeRequest(string BaseResume);
