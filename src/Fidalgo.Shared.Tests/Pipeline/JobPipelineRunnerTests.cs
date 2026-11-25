using Fidalgo.Shared.ApplicationPackets;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.ErrorHandling;
using Fidalgo.Shared.Logging;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.Pipeline;
using Fidalgo.Shared.Storage;
using Fidalgo.Shared.UserProfile;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Fidalgo.Shared.Tests.Pipeline;

public class JobPipelineRunnerTests : IDisposable
{
    private readonly JobDbContext _dbContext;
    private readonly JobRepository _repository;
    private readonly IJobIngestionService _ingestionService = Substitute.For<IJobIngestionService>();
    private readonly IJobEvaluationService _evaluationService = Substitute.For<IJobEvaluationService>();
    private readonly IUserProfileLoader _profileLoader = Substitute.For<IUserProfileLoader>();
    private readonly IApplicationPacketService _packetService =
        Substitute.For<IApplicationPacketService>();
    private readonly ITraceContextProvider _traceContextProvider = new TraceContextProvider();
    private readonly ILogger<JobPipelineRunner> _logger = Substitute.For<ILogger<JobPipelineRunner>>();

    public JobPipelineRunnerTests()
    {
        var dbOptions = new DbContextOptionsBuilder<JobDbContext>()
            .UseInMemoryDatabase($"pipeline-test-{Guid.NewGuid():N}")
            .Options;
        _dbContext = new JobDbContext(dbOptions);
        _repository = new JobRepository(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task RunCycleAsync_ReturnsEmpty_WhenEmailMissing()
    {
        var runner = BuildRunner(new Dictionary<string, string?>());

        var result = await runner.RunCycleAsync();

        Assert.Equal(0, result.IngestedCount);
        Assert.Equal(0, result.EvaluatedCount);
        Assert.Equal(0, result.PacketsGenerated);
        await _ingestionService.DidNotReceiveWithAnyArgs().IngestAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task RunCycleAsync_IngestsEvaluatesAndGeneratesPacketsOnlyForCloseFits()
    {
        var email = "user@example.com";
        var untriagedJob = new JobEntity
        {
            InternalId = Guid.NewGuid(),
            Email = email,
            Employer = "Acme",
            Title = "Principal Engineer",
            EmployerJobId = "job-1",
            Score = 0,
            Recommendation = "Maybe"
        };
        var lowScoreApply = new JobEntity
        {
            InternalId = Guid.NewGuid(),
            Email = email,
            Employer = "Beta",
            Title = "Junior Role",
            EmployerJobId = "job-2",
            Score = 55,
            Recommendation = "Apply"
        };
        var maybeHighScore = new JobEntity
        {
            InternalId = Guid.NewGuid(),
            Email = email,
            Employer = "Gamma",
            Title = "Staff Role",
            EmployerJobId = "job-3",
            Score = 90,
            Recommendation = "Maybe"
        };
        _dbContext.Jobs.AddRange(untriagedJob, lowScoreApply, maybeHighScore);
        await _dbContext.SaveChangesAsync();

        _ingestionService.IngestAsync(email, "architect engineer", "98033", Arg.Any<CancellationToken>())
            .Returns(3);

        _profileLoader.LoadAsync(null, Arg.Any<CancellationToken>())
            .Returns(new UserProfileContent(
                "# Resume",
                "Story",
                "Goals",
                email,
                @"C:\apps",
                new ProfileSourceMetadata(false, "resume.md", true, "career-story.md", true, "career-goals.md")));

        _evaluationService.EvaluateJobAsync(
                untriagedJob,
                email,
                "# Resume",
                "Story",
                "Goals",
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                untriagedJob.Score = 88;
                untriagedJob.Recommendation = "Apply";
                return 1;
            });

        _packetService.GenerateForJobAsync(
                Arg.Any<JobEntity>(),
                Arg.Any<ApplicationPacketGenerationRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var job = callInfo.Arg<JobEntity>();
                return new ApplicationPacketResult(
                    PacketGenerationOutcome.Generated,
                    job.InternalId,
                    "acme_principal_1234",
                    @"C:\apps\acme",
                    DateTime.UtcNow,
                    "# Resume",
                    "# Cover");
            });

        var runner = BuildRunner(new Dictionary<string, string?>
        {
            ["Email"] = email,
            ["ZipCode"] = "98033",
            ["JobSearchKeywords:0"] = "architect",
            ["JobSearchKeywords:1"] = "engineer",
            ["Adzuna:AppId"] = "app-id",
            ["Adzuna:AppKey"] = "app-key"
        }, new PipelineOptions
        {
            Enabled = true,
            MinScore = 70,
            RecommendationFilter = "Apply",
            GeneratePackets = true
        });

        var result = await runner.RunCycleAsync();

        Assert.Equal(3, result.IngestedCount);
        Assert.Equal(1, result.EvaluatedCount);
        Assert.Equal(1, result.PacketsGenerated);
        Assert.Equal(0, result.PacketsSkipped);
        Assert.Equal(0, result.FailedCount);

        await _packetService.Received(1).GenerateForJobAsync(
            untriagedJob,
            Arg.Is<ApplicationPacketGenerationRequest>(r => !r.ForceRegenerate && r.SkipExisting),
            Arg.Any<CancellationToken>());
        await _packetService.DidNotReceive().GenerateForJobAsync(
            lowScoreApply,
            Arg.Any<ApplicationPacketGenerationRequest>(),
            Arg.Any<CancellationToken>());
        await _packetService.DidNotReceive().GenerateForJobAsync(
            maybeHighScore,
            Arg.Any<ApplicationPacketGenerationRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunCycleAsync_ContinuesEvaluationAndPackets_WhenIngestionFails()
    {
        var email = "user@example.com";
        var closeFit = new JobEntity
        {
            InternalId = Guid.NewGuid(),
            Email = email,
            Employer = "Acme",
            Title = "Architect",
            EmployerJobId = "job-1",
            Score = 92,
            Recommendation = "Apply"
        };
        _dbContext.Jobs.Add(closeFit);
        await _dbContext.SaveChangesAsync();

        _ingestionService.IngestAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new HttpRequestException("Adzuna down"));

        _profileLoader.LoadAsync(null, Arg.Any<CancellationToken>())
            .Returns(new UserProfileContent(
                "# Resume",
                null,
                null,
                email,
                @"C:\apps",
                new ProfileSourceMetadata(false, "resume.md", false, null, false, null)));

        _packetService.GenerateForJobAsync(closeFit, Arg.Any<ApplicationPacketGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ApplicationPacketResult(
                PacketGenerationOutcome.SkippedExisting,
                closeFit.InternalId,
                "acme_arch_1",
                @"C:\apps\acme",
                DateTime.UtcNow,
                "# R",
                "# C"));

        var runner = BuildRunner(new Dictionary<string, string?>
        {
            ["Email"] = email,
            ["ZipCode"] = "98033",
            ["Keywords"] = "architect",
            ["Adzuna:AppId"] = "id",
            ["Adzuna:AppKey"] = "key"
        });

        var result = await runner.RunCycleAsync();

        Assert.Equal(0, result.IngestedCount);
        Assert.Equal(1, result.PacketsSkipped);
        Assert.Equal(1, result.FailedCount);
    }

    [Fact]
    public async Task RunCycleAsync_SkipsEvaluationAndPackets_WhenResumeMissing()
    {
        _profileLoader.LoadAsync(null, Arg.Any<CancellationToken>())
            .Returns<UserProfileContent>(_ => throw new ValidationException(
                "Missing resume",
                new Dictionary<string, string[]> { ["baseResume"] = ["Missing"] }));

        var runner = BuildRunner(new Dictionary<string, string?>
        {
            ["Email"] = "user@example.com"
        });

        var result = await runner.RunCycleAsync();

        Assert.Equal(0, result.EvaluatedCount);
        Assert.Equal(0, result.PacketsGenerated);
        await _evaluationService.DidNotReceiveWithAnyArgs().EvaluateJobAsync(default!, default!, default!, default, default, default);
    }

    private JobPipelineRunner BuildRunner(
        IDictionary<string, string?> configValues,
        PipelineOptions? pipelineOptions = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var adzuna = new AdzunaConfiguration
        {
            AppId = config["Adzuna:AppId"] ?? string.Empty,
            AppKey = config["Adzuna:AppKey"] ?? string.Empty
        };

        return new JobPipelineRunner(
            config,
            Options.Create(pipelineOptions ?? new PipelineOptions { Enabled = true }),
            Options.Create(adzuna),
            _ingestionService,
            _evaluationService,
            _profileLoader,
            _repository,
            _packetService,
            _traceContextProvider,
            _logger);
    }
}
