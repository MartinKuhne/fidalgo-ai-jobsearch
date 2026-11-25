using Fidalgo.Agent.Configuration;
using Fidalgo.Shared.ApplicationPackets;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.ErrorHandling;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;
using Fidalgo.Shared.Tools;
using Fidalgo.Shared.UserProfile;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Fidalgo.Agent.Tests;

public class AgentRunnerTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // Best-effort temp cleanup on Windows.
            }
        }
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"fidalgo-agent-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    private static (JobRepository Repo, GetJobsTool GetJobsTool, JobDbContext Db) CreateMockRepository()
    {
        var dbOptions = new DbContextOptionsBuilder<JobDbContext>()
            .UseInMemoryDatabase($"AgentRunnerTest-{Guid.NewGuid():N}")
            .Options;
        var db = new JobDbContext(dbOptions);
        var repo = Substitute.For<JobRepository>(db);
        var getJobsTool = new GetJobsTool(repo, NullLogger<GetJobsTool>.Instance);
        return (repo, getJobsTool, db);
    }

    private static UserProfileContent SampleProfile(string outputDir = @"C:\temp\apps") =>
        new(
            ResumeMarkdown: "# Jane Doe\nStaff Engineer",
            CareerStoryMarkdown: "Led distributed systems.",
            CareerGoalsMarkdown: "Principal Engineer.",
            DefaultEmail: "user@example.com",
            ApplicationsOutputDir: outputDir,
            Sources: new ProfileSourceMetadata(
                ResumeFromOverride: false,
                ResumePath: "resume.md",
                HasCareerStory: true,
                CareerStoryPath: "career-story.md",
                HasCareerGoals: true,
                CareerGoalsPath: "career-goals.md"));

    private static JobEntity CreateJob(string recommendation, bool isDeleted = false, string employer = "Acme", string title = "Architect") =>
        new()
        {
            InternalId = Guid.NewGuid(),
            Email = "user@example.com",
            Employer = employer,
            Title = title,
            Description = "Job description",
            Score = 90,
            Recommendation = recommendation,
            IsDeleted = isDeleted,
            SourceWebsite = "adzuna"
        };

    [Fact]
    public async Task RunAsync_GeneratePackets_FiltersToApplyByDefault_AndSkipsSoftDeletedJobs()
    {
        var (repo, getJobsTool, db) = CreateMockRepository();
        await using var _ = db;
        var packetService = Substitute.For<IApplicationPacketService>();
        var profileLoader = Substitute.For<IUserProfileLoader>();
        profileLoader.LoadAsync(Arg.Any<UserProfileLoadOverrides?>(), Arg.Any<CancellationToken>())
            .Returns(SampleProfile());

        var applyJob = CreateJob("Apply");
        var maybeJob = CreateJob("Maybe");
        var rejectJob = CreateJob("Do not apply");
        var deletedApplyJob = CreateJob("Apply", isDeleted: true);

        repo.QueryAsync("user@example.com", excludeDeleted: true, cancellationToken: Arg.Any<CancellationToken>())
            .Returns([applyJob, maybeJob, rejectJob, deletedApplyJob]);

        packetService.GenerateForJobAsync(applyJob, Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ApplicationPacketResult(
                PacketGenerationOutcome.Generated,
                applyJob.InternalId,
                "acme_architect_12345678",
                @"C:\temp\apps\acme_architect_12345678",
                DateTime.UtcNow,
                "# Resume",
                "# Cover"));

        var output = new StringWriter();
        var sut = new AgentRunner(
            repo,
            getJobsTool,
            packetService,
            profileLoader,
            Options.Create(new ApplicationPacketOptions()),
            NullLoggerFactory.Instance,
            output: output);

        var exitCode = await sut.RunAsync(new CliOptions
        {
            Email = "user@example.com",
            GeneratePackets = true
        });

        Assert.Equal(0, exitCode);
        await packetService.Received(1).GenerateForJobAsync(applyJob, Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>());
        await packetService.DidNotReceive().GenerateForJobAsync(maybeJob, Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>());
        await packetService.DidNotReceive().GenerateForJobAsync(rejectJob, Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>());
        await packetService.DidNotReceive().GenerateForJobAsync(deletedApplyJob, Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>());
        Assert.Contains("Generated: 1, Skipped: 0, Failed: 0", output.ToString());
    }

    [Fact]
    public async Task RunAsync_GeneratePackets_SupportsCommaSeparatedRecommendationFilter()
    {
        var (repo, getJobsTool, db) = CreateMockRepository();
        await using var _ = db;
        var packetService = Substitute.For<IApplicationPacketService>();
        var profileLoader = Substitute.For<IUserProfileLoader>();
        profileLoader.LoadAsync(Arg.Any<UserProfileLoadOverrides?>(), Arg.Any<CancellationToken>())
            .Returns(SampleProfile());

        var applyJob = CreateJob("Apply");
        var maybeJob = CreateJob("Maybe");
        var rejectJob = CreateJob("Do not apply");

        repo.QueryAsync("user@example.com", excludeDeleted: true, cancellationToken: Arg.Any<CancellationToken>())
            .Returns([applyJob, maybeJob, rejectJob]);

        packetService.GenerateForJobAsync(Arg.Any<JobEntity>(), Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var job = ci.Arg<JobEntity>();
                return new ApplicationPacketResult(
                    PacketGenerationOutcome.Generated,
                    job.InternalId,
                    "folder",
                    @"C:\out\folder",
                    DateTime.UtcNow,
                    "# Resume",
                    "# Cover");
            });

        var output = new StringWriter();
        var sut = new AgentRunner(
            repo,
            getJobsTool,
            packetService,
            profileLoader,
            Options.Create(new ApplicationPacketOptions { RecommendationFilter = "Apply" }),
            NullLoggerFactory.Instance,
            output: output);

        var exitCode = await sut.RunAsync(new CliOptions
        {
            Email = "user@example.com",
            GeneratePackets = true,
            RecommendationFilter = "Apply, Maybe"
        });

        Assert.Equal(0, exitCode);
        await packetService.Received(1).GenerateForJobAsync(applyJob, Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>());
        await packetService.Received(1).GenerateForJobAsync(maybeJob, Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>());
        await packetService.DidNotReceive().GenerateForJobAsync(rejectJob, Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>());
        Assert.Contains("Generated: 2, Skipped: 0, Failed: 0", output.ToString());
    }

    [Theory]
    [InlineData(true, null, true, true)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, true)]
    public async Task RunAsync_GeneratePackets_PropagatesForceAndSkipFlags(
        bool forceRegenerate,
        bool? skipExisting,
        bool expectedForce,
        bool expectedSkipExisting)
    {
        var (repo, getJobsTool, db) = CreateMockRepository();
        await using var _ = db;
        var packetService = Substitute.For<IApplicationPacketService>();
        var profileLoader = Substitute.For<IUserProfileLoader>();
        profileLoader.LoadAsync(Arg.Any<UserProfileLoadOverrides?>(), Arg.Any<CancellationToken>())
            .Returns(SampleProfile());

        var job = CreateJob("Apply");
        repo.QueryAsync("user@example.com", excludeDeleted: true, cancellationToken: Arg.Any<CancellationToken>())
            .Returns([job]);

        ApplicationPacketGenerationRequest? capturedRequest = null;
        packetService.GenerateForJobAsync(job, Arg.Do<ApplicationPacketGenerationRequest>(r => capturedRequest = r), Arg.Any<CancellationToken>())
            .Returns(new ApplicationPacketResult(
                PacketGenerationOutcome.SkippedExisting,
                job.InternalId,
                "folder",
                @"C:\out\folder",
                DateTime.UtcNow,
                "# Resume",
                "# Cover"));

        var output = new StringWriter();
        var sut = new AgentRunner(
            repo,
            getJobsTool,
            packetService,
            profileLoader,
            Options.Create(new ApplicationPacketOptions()),
            NullLoggerFactory.Instance,
            output: output);

        var exitCode = await sut.RunAsync(new CliOptions
        {
            Email = "user@example.com",
            GeneratePackets = true,
            ForceRegenerate = forceRegenerate,
            SkipExisting = skipExisting,
            OutputDir = @"C:\custom-out"
        });

        Assert.Equal(0, exitCode);
        Assert.NotNull(capturedRequest);
        Assert.Equal(expectedForce, capturedRequest.ForceRegenerate);
        Assert.Equal(expectedSkipExisting, capturedRequest.SkipExisting);
        Assert.Equal(@"C:\custom-out", capturedRequest.ProfileOverrides?.OutputDirectoryOverride);
        Assert.Contains("Generated: 0, Skipped: 1, Failed: 0", output.ToString());
    }

    [Fact]
    public async Task RunAsync_GeneratePackets_IsolatesPerJobExceptions_AndPrintsSummary()
    {
        var (repo, getJobsTool, db) = CreateMockRepository();
        await using var _ = db;
        var packetService = Substitute.For<IApplicationPacketService>();
        var profileLoader = Substitute.For<IUserProfileLoader>();
        profileLoader.LoadAsync(Arg.Any<UserProfileLoadOverrides?>(), Arg.Any<CancellationToken>())
            .Returns(SampleProfile());

        var job1 = CreateJob("Apply", employer: "FirstCo");
        var job2 = CreateJob("Apply", employer: "BrokenCo");
        var job3 = CreateJob("Apply", employer: "ThirdCo");

        repo.QueryAsync("user@example.com", excludeDeleted: true, cancellationToken: Arg.Any<CancellationToken>())
            .Returns([job1, job2, job3]);

        packetService.GenerateForJobAsync(job1, Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ApplicationPacketResult(PacketGenerationOutcome.Generated, job1.InternalId, "f1", @"C:\out\f1", DateTime.UtcNow, "", ""));
        packetService.GenerateForJobAsync(job2, Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<ApplicationPacketResult>>(_ => throw new AgentExecutionException("LLM timed out"));
        packetService.GenerateForJobAsync(job3, Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ApplicationPacketResult(PacketGenerationOutcome.SkippedExisting, job3.InternalId, "f3", @"C:\out\f3", DateTime.UtcNow, "", ""));

        var output = new StringWriter();
        var sut = new AgentRunner(
            repo,
            getJobsTool,
            packetService,
            profileLoader,
            Options.Create(new ApplicationPacketOptions()),
            NullLoggerFactory.Instance,
            output: output);

        var exitCode = await sut.RunAsync(new CliOptions
        {
            Email = "user@example.com",
            GeneratePackets = true
        });

        Assert.Equal(0, exitCode);
        await packetService.Received(1).GenerateForJobAsync(job3, Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>());
        Assert.Contains("Generated: 1, Skipped: 1, Failed: 1", output.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunAsync_TargetJobId_ReturnsExitCode1_WhenJobMissingOrSoftDeleted(bool isSoftDeleted)
    {
        var (repo, getJobsTool, db) = CreateMockRepository();
        await using var _ = db;
        var packetService = Substitute.For<IApplicationPacketService>();
        var profileLoader = Substitute.For<IUserProfileLoader>();

        var targetId = Guid.NewGuid();
        var returnedJob = isSoftDeleted ? CreateJob("Apply", isDeleted: true) : null;
        if (returnedJob is not null)
        {
            returnedJob.InternalId = targetId;
        }

        repo.GetByIdAsync(targetId, Arg.Any<CancellationToken>())
            .Returns(returnedJob);

        var output = new StringWriter();
        var sut = new AgentRunner(
            repo,
            getJobsTool,
            packetService,
            profileLoader,
            Options.Create(new ApplicationPacketOptions()),
            NullLoggerFactory.Instance,
            output: output);

        var exitCode = await sut.RunAsync(new CliOptions
        {
            Email = "user@example.com",
            TargetJobId = targetId
        });

        Assert.Equal(1, exitCode);
        await packetService.DidNotReceive().GenerateForJobAsync(Arg.Any<JobEntity>(), Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>());
        await packetService.DidNotReceive().GenerateForJobAsync(Arg.Any<Guid>(), Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_TargetJobId_GeneratesPacket_WhenJobExistsAndNotDeleted()
    {
        var (repo, getJobsTool, db) = CreateMockRepository();
        await using var _ = db;
        var packetService = Substitute.For<IApplicationPacketService>();
        var profileLoader = Substitute.For<IUserProfileLoader>();
        profileLoader.LoadAsync(Arg.Any<UserProfileLoadOverrides?>(), Arg.Any<CancellationToken>())
            .Returns(SampleProfile());

        var job = CreateJob("Maybe");
        repo.GetByIdAsync(job.InternalId, Arg.Any<CancellationToken>()).Returns(job);

        packetService.GenerateForJobAsync(job, Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ApplicationPacketResult(
                PacketGenerationOutcome.Generated,
                job.InternalId,
                "acme_architect_12345678",
                @"C:\out\acme_architect_12345678",
                DateTime.UtcNow,
                "# Resume",
                "# Cover"));

        var output = new StringWriter();
        var sut = new AgentRunner(
            repo,
            getJobsTool,
            packetService,
            profileLoader,
            Options.Create(new ApplicationPacketOptions()),
            NullLoggerFactory.Instance,
            output: output);

        var exitCode = await sut.RunAsync(new CliOptions
        {
            Email = "user@example.com",
            TargetJobId = job.InternalId,
            OutputDir = @"C:\out"
        });

        Assert.Equal(0, exitCode);
        await packetService.Received(1).GenerateForJobAsync(
            job,
            Arg.Is<ApplicationPacketGenerationRequest>(r => r.ProfileOverrides != null && r.ProfileOverrides.OutputDirectoryOverride == @"C:\out"),
            Arg.Any<CancellationToken>());
        Assert.Contains(@"C:\out\acme_architect_12345678", output.ToString());
    }

    [Fact]
    public async Task RunAsync_MissingResume_ReturnsExitCode1WithHelpfulMessage()
    {
        var (repo, getJobsTool, db) = CreateMockRepository();
        await using var _ = db;
        var packetService = Substitute.For<IApplicationPacketService>();
        var profileLoader = Substitute.For<IUserProfileLoader>();
        profileLoader.LoadAsync(Arg.Any<UserProfileLoadOverrides?>(), Arg.Any<CancellationToken>())
            .Returns<Task<UserProfileContent>>(_ => throw new ValidationException(
                "Base resume is required.",
                new Dictionary<string, string[]> { ["baseResume"] = ["Missing resume.md"] }));

        var output = new StringWriter();
        var sut = new AgentRunner(
            repo,
            getJobsTool,
            packetService,
            profileLoader,
            Options.Create(new ApplicationPacketOptions()),
            NullLoggerFactory.Instance,
            output: output);

        var exitCode = await sut.RunAsync(new CliOptions
        {
            Email = "user@example.com",
            GeneratePackets = true
        });

        Assert.Equal(1, exitCode);
        var text = output.ToString();
        Assert.Contains(@"Documents\Fidalgo\resume.md", text);
        Assert.Contains("--Resume", text);
        await packetService.DidNotReceive().GenerateForJobAsync(Arg.Any<JobEntity>(), Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_DefaultEvaluationMode_ChainsIntoAutoGenerate_WhenEnabled()
    {
        var (repo, getJobsTool, db) = CreateMockRepository();
        await using var _ = db;
        var packetService = Substitute.For<IApplicationPacketService>();
        var profileLoader = Substitute.For<IUserProfileLoader>();
        profileLoader.LoadAsync(Arg.Any<UserProfileLoadOverrides?>(), Arg.Any<CancellationToken>())
            .Returns(SampleProfile());

        repo.GetUntriagedAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns([]);

        var applyJob = CreateJob("Apply");
        repo.QueryAsync("user@example.com", excludeDeleted: true, cancellationToken: Arg.Any<CancellationToken>())
            .Returns([applyJob]);

        packetService.GenerateForJobAsync(applyJob, Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ApplicationPacketResult(
                PacketGenerationOutcome.Generated,
                applyJob.InternalId,
                "folder",
                @"C:\out\folder",
                DateTime.UtcNow,
                "# Resume",
                "# Cover"));

        var output = new StringWriter();
        var sut = new AgentRunner(
            repo,
            getJobsTool,
            packetService,
            profileLoader,
            Options.Create(new ApplicationPacketOptions { AutoGenerateForApply = true }),
            NullLoggerFactory.Instance,
            output: output);

        var exitCode = await sut.RunAsync(new CliOptions { Email = "user@example.com" });

        Assert.Equal(0, exitCode);
        await packetService.Received(1).GenerateForJobAsync(applyJob, Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>());
        Assert.Contains("Generated: 1, Skipped: 0, Failed: 0", output.ToString());
    }

    [Fact]
    public async Task RunAsync_QueryAndDiscardModes_WorkAsExpected()
    {
        var (repo, getJobsTool, db) = CreateMockRepository();
        await using var _ = db;
        var packetService = Substitute.For<IApplicationPacketService>();
        var profileLoader = Substitute.For<IUserProfileLoader>();

        var job = CreateJob("Apply", employer: "QueryCo");
        job.EmployerJobId = "EXT-42";
        repo.QueryAsync("user@example.com", null, null, null, null, null, true, Arg.Any<CancellationToken>())
            .Returns([job]);
        repo.SoftDeleteAsync(job.InternalId, Arg.Any<CancellationToken>())
            .Returns(true);
        repo.GetDiscardedAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns([job]);

        var queryOut = new StringWriter();
        var sut = new AgentRunner(
            repo,
            getJobsTool,
            packetService,
            profileLoader,
            Options.Create(new ApplicationPacketOptions()),
            NullLoggerFactory.Instance,
            output: queryOut);

        Assert.Equal(0, await sut.RunAsync(new CliOptions { Email = "user@example.com", QueryJobs = true }));
        Assert.Contains("QueryCo", queryOut.ToString());

        Assert.Equal(0, await sut.RunAsync(new CliOptions { Email = "user@example.com", DiscardJobId = job.InternalId }));
        Assert.Equal(1, await sut.RunAsync(new CliOptions { Email = "user@example.com", DiscardJobId = Guid.NewGuid() }));
        Assert.Equal(0, await sut.RunAsync(new CliOptions { Email = "user@example.com", ListDiscarded = true }));

        await profileLoader.DidNotReceive().LoadAsync(Arg.Any<UserProfileLoadOverrides?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task Program_HelpFlag_PrintsUsageWithDocumentsDefaultsAndReturnsZero(string flag)
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await Program.ExecuteAsync(
            [flag],
            output: output,
            error: error,
            useUserProfileConfig: false);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains(@"Documents\Fidalgo\", text);
        Assert.Contains(@"%APPDATA%\.config\Fidalgo\config.json", text);
        Assert.Contains("--GeneratePackets", text);
        Assert.Contains("--TargetJobId", text);
    }

    [Fact]
    public async Task Program_ZeroArgs_SucceedsFromConfigJson_WithoutPrintingHelp()
    {
        var tempDir = CreateTempDir();
        var profileDir = Path.Combine(tempDir, "profile");
        var outputDir = Path.Combine(tempDir, "applications");
        Directory.CreateDirectory(profileDir);
        await File.WriteAllTextAsync(Path.Combine(profileDir, "resume.md"), "# Jane Doe\nArchitect");

        var configPath = Path.Combine(tempDir, "config.json");
        await File.WriteAllTextAsync(
            configPath,
            $$"""
            {
              "Email": "zeroarg@example.com",
              "DatabasePath": "{{Path.Combine(tempDir, "test-jobs.db").Replace("\\", "\\\\")}}",
              "ApplicationPacket": {
                "ProfileDirectory": "{{profileDir.Replace("\\", "\\\\")}}",
                "OutputDirectory": "{{outputDir.Replace("\\", "\\\\")}}",
                "AutoGenerateForApply": false
              }
            }
            """);

        var logPath = Path.Combine(tempDir, "logs", "log-.json");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await Program.ExecuteAsync(
            [],
            output: output,
            error: error,
            useUserProfileConfig: false,
            userProfileConfigPath: configPath,
            logFilePath: logPath);

        Assert.Equal(0, exitCode);
        Assert.Contains("Found 0 untriaged jobs.", output.ToString());
        Assert.DoesNotContain("Usage: Fidalgo.Agent", output.ToString());

        var logFiles = Directory.GetFiles(Path.Combine(tempDir, "logs"), "log-*.json");
        Assert.NotEmpty(logFiles);
        var logContent = await File.ReadAllTextAsync(logFiles[0]);
        Assert.Contains("CorrelationId", logContent);
    }

    [Fact]
    public void Program_BuildHost_CliAndEnvOverrideUserProfileConfig()
    {
        var tempDir = CreateTempDir();
        var configPath = Path.Combine(tempDir, "config.json");
        File.WriteAllText(configPath, """
        {
          "Email": "from-config@example.com",
          "ApplicationPacket": {
            "RecommendationFilter": "Maybe"
          }
        }
        """);

        var previousEnv = Environment.GetEnvironmentVariable("ApplicationPacket__RecommendationFilter");
        Environment.SetEnvironmentVariable("ApplicationPacket__RecommendationFilter", "Apply");
        try
        {
            using var host = Program.BuildHost(
                ["--Email", "from-cli@example.com"],
                useUserProfileConfig: false,
                userProfileConfigPath: configPath,
                logFilePath: Path.Combine(tempDir, "logs", "log-.json"));

            var cliOptions = host.Services.GetRequiredService<IOptions<CliOptions>>().Value;
            var packetOptions = host.Services.GetRequiredService<IOptions<ApplicationPacketOptions>>().Value;

            Assert.Equal("from-cli@example.com", cliOptions.Email);
            Assert.Equal("Apply", packetOptions.RecommendationFilter);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ApplicationPacket__RecommendationFilter", previousEnv);
        }
    }

    [Fact]
    public async Task Program_ZeroArgs_WithMissingEmail_ReturnsExitCode1AndSetupGuidance()
    {
        var tempDir = CreateTempDir();
        var configPath = Path.Combine(tempDir, "empty-config.json");
        await File.WriteAllTextAsync(configPath, "{}");

        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await Program.ExecuteAsync(
            [],
            output: output,
            error: error,
            useUserProfileConfig: false,
            userProfileConfigPath: configPath,
            logFilePath: Path.Combine(tempDir, "logs", "log-.json"),
            configureConfiguration: cfg =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabasePath"] = Path.Combine(tempDir, "test-jobs.db")
                });
            });

        Assert.Equal(1, exitCode);
        var errorText = error.ToString();
        Assert.Contains("config.json", errorText);
        Assert.Contains("Usage: Fidalgo.Agent", errorText);
    }

    [Fact]
    public void Program_BareBooleanFlags_BindCleanlyWithOtherOptions()
    {
        var tempDir = CreateTempDir();
        using var host = Program.BuildHost(
            ["--Email", "cli@example.com", "--GeneratePackets", "--ForceRegenerate", "--OutputDir", tempDir],
            useUserProfileConfig: false,
            logFilePath: Path.Combine(tempDir, "logs", "log-.json"),
            configureConfiguration: cfg =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ApplicationPacket:RecommendationFilter"] = "Apply,Maybe"
                });
            });

        var cliOptions = host.Services.GetRequiredService<IOptions<CliOptions>>().Value;
        var packetOptions = host.Services.GetRequiredService<IOptions<ApplicationPacketOptions>>().Value;

        Assert.Equal("cli@example.com", cliOptions.Email);
        Assert.True(cliOptions.GeneratePackets);
        Assert.True(cliOptions.ForceRegenerate);
        Assert.Null(cliOptions.SkipExisting);
        Assert.Null(cliOptions.RecommendationFilter);
        Assert.Equal(tempDir, cliOptions.OutputDir);
        Assert.Equal("Apply,Maybe", packetOptions.RecommendationFilter);
    }
}
