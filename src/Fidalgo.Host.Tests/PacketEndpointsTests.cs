using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Fidalgo.Host.Models;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.ErrorHandling;
using Fidalgo.Shared.Llm;
using Fidalgo.Shared.Logging;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.Pdf;
using Fidalgo.Shared.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace Fidalgo.Host.Tests;

public class PacketEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly byte[] FakeResumePdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n%Resume");
    private static readonly byte[] FakeCoverLetterPdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n%CoverLetter");

    [Fact]
    public async Task GetProfileDefaults_ReflectsIsolatedProfileDirectory()
    {
        using var fixture = new HostTestFixture();
        fixture.WriteProfileFile("resume.md", "# Jane Doe\nStaff Engineer");
        fixture.WriteProfileFile("career-story.md", "Scaled distributed systems.");
        fixture.WriteProfileFile("career-goals.md", "Lead architecture teams.");

        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/profile/defaults");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ProfileDefaultsDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal("candidate@example.com", dto!.DefaultEmail);
        Assert.True(dto.HasResume);
        Assert.True(dto.HasCareerStory);
        Assert.Equal("Scaled distributed systems.".Length, dto.CareerStoryLength);
        Assert.True(dto.HasCareerGoals);
        Assert.Equal("Lead architecture teams.".Length, dto.CareerGoalsLength);
        Assert.Equal("# Jane Doe\nStaff Engineer", dto.BaseResumeMarkdown);
        Assert.Equal("Scaled distributed systems.", dto.CareerStoryMarkdown);
        Assert.Equal("Lead architecture teams.", dto.CareerGoalsMarkdown);
        Assert.Equal(fixture.OutputDirectory, dto.OutputDirectory);
    }

    [Fact]
    public async Task GetProfileDefaults_WhenResumeMissing_Returns200WithHasResumeFalse()
    {
        using var fixture = new HostTestFixture();
        fixture.WriteProfileFile("career-goals.md", "Principal Engineer");

        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/profile/defaults");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ProfileDefaultsDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.False(dto!.HasResume);
        Assert.Null(dto.BaseResumeMarkdown);
        Assert.False(dto.HasCareerStory);
        Assert.True(dto.HasCareerGoals);
    }

    [Fact]
    public async Task GetJobPacketStatus_MissingOrSoftDeletedJob_Returns404()
    {
        using var fixture = new HostTestFixture();
        var deletedJob = await fixture.SeedJobAsync(isDeleted: true);

        using var client = fixture.CreateClient();
        var missingResponse = await client.GetAsync($"/api/jobs/{Guid.NewGuid()}/packet");
        var deletedResponse = await client.GetAsync($"/api/jobs/{deletedJob.InternalId}/packet");

        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deletedResponse.StatusCode);
    }

    [Fact]
    public async Task PostGeneratePacket_AndGetStatus_AndServeFiles_EndToEnd()
    {
        using var fixture = new HostTestFixture();
        fixture.WriteProfileFile("resume.md", "# Base Resume\n- Built cloud systems");
        fixture.WriteProfileFile("career-story.md", "Led 10-person platform team.");
        fixture.WriteProfileFile("career-goals.md", "Architect resilient systems.");
        var job = await fixture.SeedJobAsync();

        fixture.TextGenerator
            .GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TextGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns("# Tailored Resume\n- Cloud systems", "# Tailored Cover Letter\nDear Hiring Manager");
        fixture.PdfRenderer
            .RenderMarkdownToPdfAsync(Arg.Any<string>(), Arg.Is<PdfRenderOptions>(o => o.Kind == PdfDocumentKind.Resume), Arg.Any<CancellationToken>())
            .Returns(FakeResumePdfBytes);
        fixture.PdfRenderer
            .RenderMarkdownToPdfAsync(Arg.Any<string>(), Arg.Is<PdfRenderOptions>(o => o.Kind == PdfDocumentKind.CoverLetter), Arg.Any<CancellationToken>())
            .Returns(FakeCoverLetterPdfBytes);

        using var client = fixture.CreateClient();

        // Status before generation
        var initialStatusResponse = await client.GetAsync($"/api/jobs/{job.InternalId}/packet");
        Assert.Equal(HttpStatusCode.OK, initialStatusResponse.StatusCode);
        var initialStatus = await initialStatusResponse.Content.ReadFromJsonAsync<ApplicationPacketStatusDto>(JsonOptions);
        Assert.NotNull(initialStatus);
        Assert.False(initialStatus!.Exists);

        // Generate packet using server defaults
        var generateResponse = await client.PostAsJsonAsync($"/api/jobs/{job.InternalId}/packet", new GeneratePacketRequest());
        Assert.Equal(HttpStatusCode.OK, generateResponse.StatusCode);
        var result = await generateResponse.Content.ReadFromJsonAsync<ApplicationPacketResultDto>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("Generated", result!.Outcome);
        Assert.Equal(job.InternalId, result.JobId);
        Assert.Equal("# Tailored Resume\n- Cloud systems", result.ResumeMarkdown);
        Assert.Equal("# Tailored Cover Letter\nDear Hiring Manager", result.CoverLetterMarkdown);
        Assert.StartsWith(fixture.OutputDirectory, result.DirectoryPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal($"/api/jobs/{job.InternalId}/packet/files/resume.pdf", result.Files.ResumePdfUrl);
        Assert.Equal($"/api/jobs/{job.InternalId}/packet/files/cover-letter.pdf", result.Files.CoverLetterPdfUrl);
        Assert.Equal($"/api/jobs/{job.InternalId}/packet/files/job-posting.md", result.Files.JobPostingUrl);

        // Idempotent second call without force skips LLM/PDF
        var secondResponse = await client.PostAsJsonAsync($"/api/jobs/{job.InternalId}/packet", new GeneratePacketRequest(Force: false));
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondResult = await secondResponse.Content.ReadFromJsonAsync<ApplicationPacketResultDto>(JsonOptions);
        Assert.Equal("SkippedExisting", secondResult!.Outcome);
        await fixture.TextGenerator.Received(2).GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TextGenerationOptions?>(), Arg.Any<CancellationToken>());

        // Status after generation
        var readyStatusResponse = await client.GetAsync($"/api/jobs/{job.InternalId}/packet");
        Assert.Equal(HttpStatusCode.OK, readyStatusResponse.StatusCode);
        var readyStatus = await readyStatusResponse.Content.ReadFromJsonAsync<ApplicationPacketStatusDto>(JsonOptions);
        Assert.True(readyStatus!.Exists);
        Assert.NotNull(readyStatus.GeneratedAtUtc);
        Assert.Equal("# Tailored Resume\n- Cloud systems", readyStatus.ResumeMarkdown);

        // Serve inline resume.pdf
        var resumePdfResponse = await client.GetAsync(result.Files.ResumePdfUrl);
        Assert.Equal(HttpStatusCode.OK, resumePdfResponse.StatusCode);
        Assert.Equal("application/pdf", resumePdfResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", resumePdfResponse.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(FakeResumePdfBytes, await resumePdfResponse.Content.ReadAsByteArrayAsync());

        // Serve attachment cover-letter.pdf with ?download=true
        var coverLetterDownloadResponse = await client.GetAsync($"{result.Files.CoverLetterPdfUrl}?download=true");
        Assert.Equal(HttpStatusCode.OK, coverLetterDownloadResponse.StatusCode);
        Assert.Equal("application/pdf", coverLetterDownloadResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", coverLetterDownloadResponse.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("cover-letter.pdf", coverLetterDownloadResponse.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal(FakeCoverLetterPdfBytes, await coverLetterDownloadResponse.Content.ReadAsByteArrayAsync());

        // Serve job-posting.md
        var postingResponse = await client.GetAsync(result.Files.JobPostingUrl);
        Assert.Equal(HttpStatusCode.OK, postingResponse.StatusCode);
        Assert.Equal("text/markdown", postingResponse.Content.Headers.ContentType?.MediaType);
        var postingContent = await postingResponse.Content.ReadAsStringAsync();
        Assert.Contains("Acme Corp", postingContent);
        Assert.Contains("Staff Architect", postingContent);
    }

    [Fact]
    public async Task PostGeneratePacket_MissingResumeWithoutOverride_Returns400ValidationProblem()
    {
        using var fixture = new HostTestFixture();
        var job = await fixture.SeedJobAsync();

        using var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/jobs/{job.InternalId}/packet", new GeneratePacketRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem!.Errors.ContainsKey("baseResume"));
    }

    [Fact]
    public async Task PostGeneratePacket_MissingOrSoftDeletedJob_Returns404()
    {
        using var fixture = new HostTestFixture();
        fixture.WriteProfileFile("resume.md", "# Resume");
        var deletedJob = await fixture.SeedJobAsync(isDeleted: true);

        using var client = fixture.CreateClient();
        var missingResponse = await client.PostAsJsonAsync($"/api/jobs/{Guid.NewGuid()}/packet", new GeneratePacketRequest());
        var deletedResponse = await client.PostAsJsonAsync($"/api/jobs/{deletedJob.InternalId}/packet", new GeneratePacketRequest());

        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deletedResponse.StatusCode);
    }

    [Fact]
    public async Task PostGeneratePacket_UpstreamFailure_Returns502WithoutStackTrace()
    {
        using var fixture = new HostTestFixture();
        fixture.WriteProfileFile("resume.md", "# Resume");
        var job = await fixture.SeedJobAsync();

        fixture.TextGenerator
            .GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TextGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new AgentExecutionException("Simulated LLM upstream failure"));

        using var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/jobs/{job.InternalId}/packet", new GeneratePacketRequest());

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var rawBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("StackTrace", rawBody);
        Assert.DoesNotContain("Simulated LLM upstream failure", rawBody);
        var problem = JsonSerializer.Deserialize<ProblemDetails>(rawBody, JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(502, problem!.Status);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    [Theory]
    [InlineData("packet.json")]
    [InlineData("..%2Fappsettings.json")]
    [InlineData("..%5Cappsettings.json")]
    [InlineData("secrets.txt")]
    public async Task GetPacketFile_DisallowedFileName_Returns400ValidationProblem(string disallowedFile)
    {
        using var fixture = new HostTestFixture();
        var job = await fixture.SeedJobAsync();

        using var client = fixture.CreateClient();
        var response = await client.GetAsync($"/api/jobs/{job.InternalId}/packet/files/{disallowedFile}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem!.Errors.ContainsKey("fileName"));
    }

    [Fact]
    public async Task GetPacketFile_MissingFileOrSoftDeletedJob_Returns404()
    {
        using var fixture = new HostTestFixture();
        var job = await fixture.SeedJobAsync();
        var deletedJob = await fixture.SeedJobAsync(isDeleted: true);

        using var client = fixture.CreateClient();
        var missingFileResponse = await client.GetAsync($"/api/jobs/{job.InternalId}/packet/files/resume.pdf");
        var deletedJobResponse = await client.GetAsync($"/api/jobs/{deletedJob.InternalId}/packet/files/resume.pdf");

        Assert.Equal(HttpStatusCode.NotFound, missingFileResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deletedJobResponse.StatusCode);
    }

    [Fact]
    public async Task PostLegacyResume_FallsBackToServerResumeAndNormalizesResponse()
    {
        using var fixture = new HostTestFixture();
        fixture.WriteProfileFile("resume.md", "# Server Base Resume\n- C# / .NET");
        var job = await fixture.SeedJobAsync();

        fixture.TextGenerator
            .GenerateAsync(Arg.Any<string>(), Arg.Is<string>(p => p.Contains("Server Base Resume")), Arg.Any<TextGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns("```markdown\n# Tailored Legacy Resume\n```");

        using var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/jobs/{job.InternalId}/resume", new ResumeRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("# Tailored Legacy Resume", doc.RootElement.GetProperty("updatedResume").GetString());
    }

    [Fact]
    public async Task GetJobs_WithMissingEmail_PreservesBadHttpRequest400()
    {
        using var fixture = new HostTestFixture();
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/jobs?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetJobs_WithInvalidPage_Returns400ValidationProblem()
    {
        using var fixture = new HostTestFixture();
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/jobs?email=candidate@example.com&page=0&pageSize=10");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem!.Errors.ContainsKey("page"));
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task PacketAndResumeEndpoints_WithEmptyGuid_Return404()
    {
        using var fixture = new HostTestFixture();
        using var client = fixture.CreateClient();

        var packetStatusResponse = await client.GetAsync($"/api/jobs/{Guid.Empty}/packet");
        var packetFileResponse = await client.GetAsync($"/api/jobs/{Guid.Empty}/packet/files/resume.pdf");
        var legacyResumeResponse = await client.PostAsJsonAsync($"/api/jobs/{Guid.Empty}/resume", new ResumeRequest());

        Assert.Equal(HttpStatusCode.NotFound, packetStatusResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, packetFileResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, legacyResumeResponse.StatusCode);
    }

    [Fact]
    public async Task PostGeneratePacket_WithOverridesAndForce_RegeneratesExistingPacket()
    {
        using var fixture = new HostTestFixture();
        fixture.WriteProfileFile("resume.md", "# Server Base Resume");
        var job = await fixture.SeedJobAsync();

        fixture.TextGenerator
            .GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TextGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns("# Initial Resume", "# Initial Cover Letter", "# Forced Override Resume", "# Forced Cover Letter");
        fixture.PdfRenderer
            .RenderMarkdownToPdfAsync(Arg.Any<string>(), Arg.Is<PdfRenderOptions>(o => o.Kind == PdfDocumentKind.Resume), Arg.Any<CancellationToken>())
            .Returns(FakeResumePdfBytes);
        fixture.PdfRenderer
            .RenderMarkdownToPdfAsync(Arg.Any<string>(), Arg.Is<PdfRenderOptions>(o => o.Kind == PdfDocumentKind.CoverLetter), Arg.Any<CancellationToken>())
            .Returns(FakeCoverLetterPdfBytes);

        using var client = fixture.CreateClient();

        var firstResponse = await client.PostAsJsonAsync($"/api/jobs/{job.InternalId}/packet", new GeneratePacketRequest());
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var overrideRequest = new GeneratePacketRequest(
            BaseResumeOverride: "# Custom Override Resume",
            CareerStoryOverride: "Custom story override",
            CareerGoalsOverride: "Custom goals override",
            Force: true);
        var forcedResponse = await client.PostAsJsonAsync($"/api/jobs/{job.InternalId}/packet", overrideRequest);

        Assert.Equal(HttpStatusCode.OK, forcedResponse.StatusCode);
        var result = await forcedResponse.Content.ReadFromJsonAsync<ApplicationPacketResultDto>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("Generated", result!.Outcome);
        Assert.Equal("# Forced Override Resume", result.ResumeMarkdown);
        Assert.Equal("# Forced Cover Letter", result.CoverLetterMarkdown);

        await fixture.TextGenerator.Received(4).GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TextGenerationOptions?>(), Arg.Any<CancellationToken>());
        var prompts = fixture.TextGenerator.ReceivedCalls()
            .Select(c => (string)c.GetArguments()[1]!)
            .ToList();
        Assert.Contains(prompts, p => p.Contains("Custom Override Resume", StringComparison.Ordinal));
        Assert.Contains(prompts, p => p.Contains("Custom story override", StringComparison.Ordinal));
        Assert.Contains(prompts, p => p.Contains("Custom goals override", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetPacketFile_SetsCacheControlNoStoreHeader()
    {
        using var fixture = new HostTestFixture();
        fixture.WriteProfileFile("resume.md", "# Base Resume");
        var job = await fixture.SeedJobAsync();

        fixture.TextGenerator
            .GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TextGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns("# Tailored Resume", "# Tailored Cover Letter");
        fixture.PdfRenderer
            .RenderMarkdownToPdfAsync(Arg.Any<string>(), Arg.Is<PdfRenderOptions>(o => o.Kind == PdfDocumentKind.Resume), Arg.Any<CancellationToken>())
            .Returns(FakeResumePdfBytes);
        fixture.PdfRenderer
            .RenderMarkdownToPdfAsync(Arg.Any<string>(), Arg.Is<PdfRenderOptions>(o => o.Kind == PdfDocumentKind.CoverLetter), Arg.Any<CancellationToken>())
            .Returns(FakeCoverLetterPdfBytes);

        using var client = fixture.CreateClient();
        var generateResponse = await client.PostAsJsonAsync($"/api/jobs/{job.InternalId}/packet", new GeneratePacketRequest());
        Assert.Equal(HttpStatusCode.OK, generateResponse.StatusCode);

        var fileResponse = await client.GetAsync($"/api/jobs/{job.InternalId}/packet/files/resume.pdf");
        Assert.Equal(HttpStatusCode.OK, fileResponse.StatusCode);
        Assert.NotNull(fileResponse.Headers.CacheControl);
        Assert.True(fileResponse.Headers.CacheControl!.NoStore);
        Assert.True(fileResponse.Headers.CacheControl.NoCache);
        Assert.True(fileResponse.Headers.CacheControl.MustRevalidate);
    }

    [Fact]
    public async Task RequestMiddleware_PopulatesTraceContextDuringRequestAndClearsAfterward()
    {
        using var fixture = new HostTestFixture();
        fixture.WriteProfileFile("resume.md", "# Base Resume");
        var job = await fixture.SeedJobAsync();
        var traceContextProvider = fixture.Services.GetRequiredService<ITraceContextProvider>();

        TraceContext? capturedDuringRequest = null;
        fixture.TextGenerator
            .GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TextGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                capturedDuringRequest = traceContextProvider.GetCurrentContext();
                return "# Tailored Resume";
            }, _ => "# Tailored Cover Letter");
        fixture.PdfRenderer
            .RenderMarkdownToPdfAsync(Arg.Any<string>(), Arg.Is<PdfRenderOptions>(o => o.Kind == PdfDocumentKind.Resume), Arg.Any<CancellationToken>())
            .Returns(FakeResumePdfBytes);
        fixture.PdfRenderer
            .RenderMarkdownToPdfAsync(Arg.Any<string>(), Arg.Is<PdfRenderOptions>(o => o.Kind == PdfDocumentKind.CoverLetter), Arg.Any<CancellationToken>())
            .Returns(FakeCoverLetterPdfBytes);

        using var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/jobs/{job.InternalId}/packet", new GeneratePacketRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(capturedDuringRequest);
        Assert.False(string.IsNullOrWhiteSpace(capturedDuringRequest!.TraceId));
        Assert.False(string.IsNullOrWhiteSpace(capturedDuringRequest.SpanId));
        Assert.Equal(capturedDuringRequest.TraceId, capturedDuringRequest.CorrelationId);
        Assert.Null(traceContextProvider.GetCurrentContext());
    }

    private sealed class HostTestFixture : WebApplicationFactory<Program>
    {
        private readonly string _rootDir;
        private readonly string _dbPath;

        public string ProfileDirectory { get; }
        public string OutputDirectory { get; }
        public ITextGenerator TextGenerator { get; } = Substitute.For<ITextGenerator>();
        public IPdfRenderer PdfRenderer { get; } = Substitute.For<IPdfRenderer>();

        public HostTestFixture()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), $"fidalgo-host-tests-{Guid.NewGuid():N}");
            ProfileDirectory = Path.Combine(_rootDir, "profile");
            OutputDirectory = Path.Combine(_rootDir, "output");
            _dbPath = Path.Combine(_rootDir, "jobs.db");

            Directory.CreateDirectory(ProfileDirectory);
            Directory.CreateDirectory(OutputDirectory);
        }

        public void WriteProfileFile(string fileName, string content)
        {
            File.WriteAllText(Path.Combine(ProfileDirectory, fileName), content);
        }

        public async Task<JobEntity> SeedJobAsync(bool isDeleted = false)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
            var job = new JobEntity
            {
                InternalId = Guid.NewGuid(),
                Email = "candidate@example.com",
                Employer = "Acme Corp",
                Title = "Staff Architect",
                EmployerJobId = $"emp-{Guid.NewGuid():N}"[..12],
                SourceWebsite = "adzuna",
                PostedDate = DateTime.UtcNow,
                SalaryRangeLow = 170000,
                SalaryRangeHigh = 210000,
                Description = "Design distributed cloud services.",
                Pros = "Strong architecture alignment",
                Cons = "None",
                ResumeHints = "Highlight .NET and distributed tracing",
                Score = 92,
                Recommendation = "Apply",
                IsDeleted = isDeleted
            };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();
            return job;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("UseUserProfileConfig", "false");
            builder.UseSetting("DatabasePath", _dbPath);
            builder.UseSetting("Email", "candidate@example.com");
            builder.UseSetting($"{ApplicationPacketOptions.ConfigurationSectionName}:ProfileDirectory", ProfileDirectory);
            builder.UseSetting($"{ApplicationPacketOptions.ConfigurationSectionName}:OutputDirectory", OutputDirectory);

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UseUserProfileConfig"] = "false",
                    ["DatabasePath"] = _dbPath,
                    ["Email"] = "candidate@example.com",
                    [$"{ApplicationPacketOptions.ConfigurationSectionName}:ProfileDirectory"] = ProfileDirectory,
                    [$"{ApplicationPacketOptions.ConfigurationSectionName}:OutputDirectory"] = OutputDirectory
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITextGenerator>();
                services.AddSingleton(TextGenerator);

                services.RemoveAll<IPdfRenderer>();
                services.AddSingleton(PdfRenderer);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(_rootDir))
            {
                try
                {
                    Directory.Delete(_rootDir, recursive: true);
                }
                catch
                {
                    // Best-effort temp directory cleanup on Windows.
                }
            }
        }
    }
}
