using Fidalgo.Shared.ApplicationPackets;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.ErrorHandling;
using Fidalgo.Shared.Llm;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.Pdf;
using Fidalgo.Shared.Storage;
using Fidalgo.Shared.UserProfile;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Fidalgo.Shared.Tests.ApplicationPackets;

public class ApplicationPacketServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _profileDir;
    private readonly string _outputDir;
    private readonly JobDbContext _dbContext;
    private readonly JobRepository _jobRepository;
    private readonly IUserProfileLoader _profileLoader;
    private readonly ITextGenerator _textGenerator;
    private readonly IPdfRenderer _pdfRenderer;
    private readonly IApplicationPacketStore _packetStore;
    private readonly ApplicationPacketOptions _packetOptions;
    private readonly ApplicationPacketService _sut;

    public ApplicationPacketServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "fidalgo-pkt-svc-" + Guid.NewGuid().ToString("N"));
        _profileDir = Path.Combine(_tempDir, "profile");
        _outputDir = Path.Combine(_tempDir, "applications");
        Directory.CreateDirectory(_profileDir);
        Directory.CreateDirectory(_outputDir);

        var dbOptions = new DbContextOptionsBuilder<JobDbContext>()
            .UseInMemoryDatabase("pkt-svc-" + Guid.NewGuid().ToString("N"))
            .Options;
        _dbContext = new JobDbContext(dbOptions);
        _jobRepository = Substitute.ForPartsOf<JobRepository>(_dbContext);

        _profileLoader = Substitute.For<IUserProfileLoader>();
        _textGenerator = Substitute.For<ITextGenerator>();
        _pdfRenderer = Substitute.For<IPdfRenderer>();
        _packetStore = Substitute.For<IApplicationPacketStore>();
        _packetOptions = new ApplicationPacketOptions
        {
            ProfileDirectory = _profileDir,
            OutputDirectory = _outputDir,
            ResumePageSize = "Letter",
            LlmTimeoutSeconds = 60
        };

        _sut = new ApplicationPacketService(
            _jobRepository,
            _profileLoader,
            _textGenerator,
            _pdfRenderer,
            _packetStore,
            Options.Create(_packetOptions),
            Options.Create(new LlmConfiguration { Model = "test-model", Endpoint = "http://localhost:8080/v1" }),
            NullLogger<ApplicationPacketService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateForJobAsync_ThrowsNotFoundException_WhenJobMissingOrSoftDeleted()
    {
        var missingId = Guid.NewGuid();
        var deletedJob = CreateSampleJob(isDeleted: true);
        await _dbContext.Jobs.AddAsync(deletedJob);
        await _dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.GenerateForJobAsync(missingId, new ApplicationPacketGenerationRequest()));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.GenerateForJobAsync(deletedJob.InternalId, new ApplicationPacketGenerationRequest()));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.GenerateForJobAsync(deletedJob, new ApplicationPacketGenerationRequest()));
    }

    [Fact]
    public async Task GenerateForJobAsync_SkipsExistingCompletePacket_WithoutCallingProfileLlmOrPdf()
    {
        var job = CreateSampleJob();
        var existingAt = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
        _packetStore
            .GetStatusAsync(_outputDir, job, Arg.Any<CancellationToken>())
            .Returns(new ApplicationPacketStatus(
                Exists: true,
                FolderName: "acme_staff-engineer_11111111",
                DirectoryPath: Path.Combine(_outputDir, "acme_staff-engineer_11111111"),
                GeneratedAtUtc: existingAt,
                ResumeMarkdown: "# Cached Resume",
                CoverLetterMarkdown: "# Cached Cover"));

        var result = await _sut.GenerateForJobAsync(
            job,
            new ApplicationPacketGenerationRequest(ForceRegenerate: false, SkipExisting: true));

        Assert.Equal(PacketGenerationOutcome.SkippedExisting, result.Outcome);
        Assert.Equal("# Cached Resume", result.ResumeMarkdown);
        Assert.Equal("# Cached Cover", result.CoverLetterMarkdown);
        Assert.Equal(existingAt, result.GeneratedAtUtc);

        await _profileLoader.DidNotReceive().LoadAsync(Arg.Any<UserProfileLoadOverrides?>(), Arg.Any<CancellationToken>());
        await _textGenerator.DidNotReceive().GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TextGenerationOptions?>(), Arg.Any<CancellationToken>());
        await _pdfRenderer.DidNotReceive().RenderMarkdownToPdfAsync(Arg.Any<string>(), Arg.Any<PdfRenderOptions>(), Arg.Any<CancellationToken>());
        await _packetStore.DidNotReceive().SavePacketAsync(Arg.Any<string>(), Arg.Any<JobEntity>(), Arg.Any<ApplicationPacketWriteRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task GenerateForJobAsync_RegeneratesWhenForceOrSkipExistingFalse(bool forceRegenerate, bool skipExisting)
    {
        var job = CreateSampleJob();
        SetupCompleteGenerationMocks(job);
        _packetStore
            .GetStatusAsync(_outputDir, job, Arg.Any<CancellationToken>())
            .Returns(new ApplicationPacketStatus(true, "folder", Path.Combine(_outputDir, "folder"), DateTime.UtcNow, "old", "old"));

        var result = await _sut.GenerateForJobAsync(
            job,
            new ApplicationPacketGenerationRequest(ForceRegenerate: forceRegenerate, SkipExisting: skipExisting));

        Assert.Equal(PacketGenerationOutcome.Generated, result.Outcome);
        await _textGenerator.Received(2).GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TextGenerationOptions?>(), Arg.Any<CancellationToken>());
        await _pdfRenderer.Received(2).RenderMarkdownToPdfAsync(Arg.Any<string>(), Arg.Any<PdfRenderOptions>(), Arg.Any<CancellationToken>());
        await _packetStore.Received(1).SavePacketAsync(
            _outputDir,
            job,
            Arg.Is<ApplicationPacketWriteRequest>(w =>
                w.ForceRegenerated &&
                w.ResumeMarkdown == "# Tailored Resume" &&
                w.CoverLetterMarkdown == "# Tailored Cover Letter" &&
                w.ModelName == "test-model"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateForJobAsync_CallsResumeThenCoverLetterThenPdfsAndNormalizesMarkdown()
    {
        var job = CreateSampleJob();
        var profile = CreateSampleProfile();
        _packetStore
            .GetStatusAsync(_outputDir, job, Arg.Any<CancellationToken>())
            .Returns(new ApplicationPacketStatus(false, "acme_staff-engineer_a1b2c3d4", Path.Combine(_outputDir, "acme_staff-engineer_a1b2c3d4"), null, null, null));
        _profileLoader.LoadAsync(Arg.Any<UserProfileLoadOverrides?>(), Arg.Any<CancellationToken>()).Returns(profile);

        _textGenerator
            .GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TextGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns(
                "```markdown\n# Tailored Resume\n```",
                "Here is your cover letter:\n\n```markdown\n# Tailored Cover Letter\n```");

        _pdfRenderer
            .RenderMarkdownToPdfAsync(Arg.Any<string>(), Arg.Any<PdfRenderOptions>(), Arg.Any<CancellationToken>())
            .Returns("%PDF-1.4\nresume"u8.ToArray(), "%PDF-1.4\ncover"u8.ToArray());

        _packetStore
            .SavePacketAsync(_outputDir, job, Arg.Any<ApplicationPacketWriteRequest>(), Arg.Any<CancellationToken>())
            .Returns(ApplicationPacketPaths.GetPacketFiles(_outputDir, job));

        var result = await _sut.GenerateForJobAsync(job, new ApplicationPacketGenerationRequest());

        Assert.Equal(PacketGenerationOutcome.Generated, result.Outcome);
        Assert.Equal("# Tailored Resume", result.ResumeMarkdown);
        Assert.Equal("# Tailored Cover Letter", result.CoverLetterMarkdown);

        Received.InOrder(() =>
        {
            _profileLoader.LoadAsync(Arg.Any<UserProfileLoadOverrides?>(), Arg.Any<CancellationToken>());
            _textGenerator.GenerateAsync(Arg.Any<string>(), Arg.Is<string>(p => p.Contains("<candidate_resume>")), Arg.Any<TextGenerationOptions?>(), Arg.Any<CancellationToken>());
            _textGenerator.GenerateAsync(Arg.Any<string>(), Arg.Is<string>(p => p.Contains("<career_goals>")), Arg.Any<TextGenerationOptions?>(), Arg.Any<CancellationToken>());
            _pdfRenderer.RenderMarkdownToPdfAsync("# Tailored Resume", Arg.Is<PdfRenderOptions>(o => o.Kind == PdfDocumentKind.Resume), Arg.Any<CancellationToken>());
            _pdfRenderer.RenderMarkdownToPdfAsync("# Tailored Cover Letter", Arg.Is<PdfRenderOptions>(o => o.Kind == PdfDocumentKind.CoverLetter), Arg.Any<CancellationToken>());
            _packetStore.SavePacketAsync(_outputDir, job, Arg.Any<ApplicationPacketWriteRequest>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task GenerateForJobAsync_DoesNotSavePartialPacket_WhenSecondStepFails()
    {
        var job = CreateSampleJob();
        var realStore = new FileSystemApplicationPacketStore(NullLogger<FileSystemApplicationPacketStore>.Instance);
        var sut = new ApplicationPacketService(
            _jobRepository,
            _profileLoader,
            _textGenerator,
            _pdfRenderer,
            realStore,
            Options.Create(_packetOptions),
            Options.Create(new LlmConfiguration { Model = "test-model" }),
            NullLogger<ApplicationPacketService>.Instance);

        _profileLoader.LoadAsync(Arg.Any<UserProfileLoadOverrides?>(), Arg.Any<CancellationToken>()).Returns(CreateSampleProfile());
        _textGenerator
            .GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TextGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns("# Tailored Resume", "# Cover Letter");
        _pdfRenderer
            .RenderMarkdownToPdfAsync(Arg.Any<string>(), Arg.Is<PdfRenderOptions>(o => o.Kind == PdfDocumentKind.Resume), Arg.Any<CancellationToken>())
            .Returns("%PDF-1.4\nresume"u8.ToArray());
        _pdfRenderer
            .RenderMarkdownToPdfAsync(Arg.Any<string>(), Arg.Is<PdfRenderOptions>(o => o.Kind == PdfDocumentKind.CoverLetter), Arg.Any<CancellationToken>())
            .Returns<byte[]>(_ => throw new AgentExecutionException("PDF service down"));

        await Assert.ThrowsAsync<AgentExecutionException>(() =>
            sut.GenerateForJobAsync(job, new ApplicationPacketGenerationRequest()));

        var status = await realStore.GetStatusAsync(_outputDir, job);
        Assert.False(status.Exists);
        Assert.False(Directory.Exists(status.DirectoryPath));
    }

    [Fact]
    public void AddApplicationPacketServices_ResolvesCoreServices()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApplicationPacket:ProfileDirectory"] = _profileDir,
                ["ApplicationPacket:OutputDirectory"] = _outputDir,
                ["LLM:Endpoint"] = "http://localhost:8080/v1",
                ["LLM:Model"] = "test-model"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<JobDbContext>(o => o.UseInMemoryDatabase("di-test"));
        services.AddScoped<JobRepository>();
        services.AddFidalgoLlmClient(config);
        services.AddApplicationPacketServices(config);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<UserProfileResolver>());
        Assert.IsType<UserProfileLoader>(scope.ServiceProvider.GetRequiredService<IUserProfileLoader>());
        Assert.IsType<MdToPdfRenderer>(scope.ServiceProvider.GetRequiredService<IPdfRenderer>());
        Assert.IsType<FileSystemApplicationPacketStore>(scope.ServiceProvider.GetRequiredService<IApplicationPacketStore>());
        Assert.IsType<ApplicationPacketService>(scope.ServiceProvider.GetRequiredService<IApplicationPacketService>());
    }

    [Fact]
    public async Task AddApplicationPacketServices_DisposesOwnedMdToPdfHttpClient_OnContainerShutdown()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApplicationPacket:ProfileDirectory"] = _profileDir,
                ["ApplicationPacket:OutputDirectory"] = _outputDir
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationPacketServices(config);

        OwnedMdToPdfHttpClient ownedClient;
        using (var provider = services.BuildServiceProvider())
        {
            ownedClient = provider.GetRequiredService<OwnedMdToPdfHttpClient>();
            Assert.NotNull(provider.GetRequiredService<IPdfRenderer>());
        }

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            ownedClient.Client.GetAsync("http://127.0.0.1:1/"));
    }

    private void SetupCompleteGenerationMocks(JobEntity job)
    {
        _profileLoader.LoadAsync(Arg.Any<UserProfileLoadOverrides?>(), Arg.Any<CancellationToken>()).Returns(CreateSampleProfile());
        _textGenerator
            .GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TextGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns("# Tailored Resume", "# Tailored Cover Letter");
        _pdfRenderer
            .RenderMarkdownToPdfAsync(Arg.Any<string>(), Arg.Any<PdfRenderOptions>(), Arg.Any<CancellationToken>())
            .Returns("%PDF-1.4\nresume"u8.ToArray(), "%PDF-1.4\ncover"u8.ToArray());
        _packetStore
            .SavePacketAsync(_outputDir, job, Arg.Any<ApplicationPacketWriteRequest>(), Arg.Any<CancellationToken>())
            .Returns(ApplicationPacketPaths.GetPacketFiles(_outputDir, job));
    }

    private UserProfileContent CreateSampleProfile() =>
        new(
            ResumeMarkdown: "# Base Resume\nC# and .NET",
            CareerStoryMarkdown: "Led cloud migration.",
            CareerGoalsMarkdown: "Architect distributed systems.",
            DefaultEmail: "candidate@example.com",
            ApplicationsOutputDir: _outputDir,
            Sources: new ProfileSourceMetadata(false, Path.Combine(_profileDir, "resume.md"), true, null, true, null));

    private static JobEntity CreateSampleJob(bool isDeleted = false) =>
        new()
        {
            InternalId = Guid.Parse("a1b2c3d4-0000-0000-0000-000000000001"),
            Email = "candidate@example.com",
            Employer = "Acme",
            Title = "Staff Engineer",
            Description = "Build cloud services.",
            ResumeHints = "Highlight .NET",
            Pros = "Remote",
            Recommendation = "Apply",
            Score = 92,
            IsDeleted = isDeleted
        };
}
