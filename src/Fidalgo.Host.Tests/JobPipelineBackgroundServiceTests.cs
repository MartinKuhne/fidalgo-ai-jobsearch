using System.Net;
using System.Text.Json;
using Fidalgo.Host.Services;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.Pipeline;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Fidalgo.Host.Tests;

public class JobPipelineBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_DoesNotRunCycles_WhenDisabled()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var options = Options.Create(new PipelineOptions { Enabled = false });
        var tracker = new PipelineStatusTracker(options);
        var logger = Substitute.For<ILogger<JobPipelineBackgroundService>>();

        var sut = new JobPipelineBackgroundService(scopeFactory, options, tracker, logger);

        await sut.StartAsync(CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);

        scopeFactory.DidNotReceive().CreateScope();
        Assert.False(tracker.GetSnapshot().Enabled);
    }

    [Fact]
    public async Task RunSingleCycleSafeAsync_RecordsCompletedResultAndHandlesFailures()
    {
        var runner = Substitute.For<IJobPipelineRunner>();
        var expectedResult = new PipelineCycleResult(2, 1, 1, 0, 0, DateTime.UtcNow);
        runner.RunCycleAsync(Arg.Any<CancellationToken>()).Returns(expectedResult);

        var services = new ServiceCollection();
        services.AddScoped(_ => runner);
        using var provider = services.BuildServiceProvider();

        var options = Options.Create(new PipelineOptions { Enabled = true, IntervalHours = 4, MinScore = 75 });
        var tracker = new PipelineStatusTracker(options);
        var logger = Substitute.For<ILogger<JobPipelineBackgroundService>>();
        var sut = new JobPipelineBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            tracker,
            logger);

        await sut.RunSingleCycleSafeAsync(CancellationToken.None);

        var snapshot = tracker.GetSnapshot();
        Assert.True(snapshot.Enabled);
        Assert.Equal(4, snapshot.IntervalHours);
        Assert.Equal(75, snapshot.MinScore);
        Assert.False(snapshot.IsRunning);
        Assert.Equal(expectedResult, snapshot.LastResult);
        Assert.Null(snapshot.LastError);

        runner.RunCycleAsync(Arg.Any<CancellationToken>())
            .Returns<PipelineCycleResult>(_ => throw new InvalidOperationException("Transient failure"));

        await sut.RunSingleCycleSafeAsync(CancellationToken.None);

        var failedSnapshot = tracker.GetSnapshot();
        Assert.False(failedSnapshot.IsRunning);
        Assert.Equal("Transient failure", failedSnapshot.LastError);
    }

    [Fact]
    public async Task GetPipelineStatus_ReturnsOkSnapshot()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("UseUserProfileConfig", "false");
            builder.UseSetting("Pipeline:Enabled", "false");
            builder.UseSetting("Pipeline:IntervalHours", "4");
            builder.UseSetting("Pipeline:MinScore", "70");
        });

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/pipeline/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(payload);
        Assert.False(doc.RootElement.GetProperty("enabled").GetBoolean());
        Assert.Equal(4, doc.RootElement.GetProperty("intervalHours").GetDouble());
        Assert.Equal(70, doc.RootElement.GetProperty("minScore").GetInt32());
    }
}
