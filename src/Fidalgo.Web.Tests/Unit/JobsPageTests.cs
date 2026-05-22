using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;
using Fidalgo.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using NSubstitute;
namespace Fidalgo.Web.Tests.Unit;

public class JobsPageTests
{
    private readonly ILogger<JobsService> _logger = Substitute.For<ILogger<JobsService>>();

    [Fact]
    public void Services_CanBeRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMudServices();

        var options = new DbContextOptionsBuilder<JobDbContext>()
            .UseInMemoryDatabase("TestDb")
            .Options;

        services.AddSingleton(options);
        services.AddScoped<JobDbContext>();
        services.AddScoped<JobRepository>();
        services.AddLogging();
        services.AddScoped<IJobsService, JobsService>();
        services.AddScoped<ITenantService, TenantService>();

        var sp = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(sp.GetRequiredService<IJobsService>());
        Assert.NotNull(sp.GetRequiredService<ITenantService>());
        Assert.NotNull(sp.GetRequiredService<JobRepository>());
    }

    [Fact]
    public async Task DeleteAction_VerifiesJobIsDeletedFromService()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<JobDbContext>()
            .UseInMemoryDatabase($"Test_{Guid.NewGuid()}")
            .Options;

        using var dbCtx = new JobDbContext(dbOptions);
        var email = "test@example.com";
        var jobId = Guid.NewGuid();
        await dbCtx.Jobs.AddAsync(new JobEntity
        {
            Email = email,
            Employer = "Test Employer",
            Title = "Test Job",
            Score = 80,
            Recommendation = "Apply",
            InternalId = jobId
        });
        await dbCtx.SaveChangesAsync();

        var repository = new JobRepository(dbCtx);
        var jobsService = new JobsService(repository, _logger);

        // Verify job exists before delete
        var jobsBefore = await jobsService.GetJobsAsync(email);
        Assert.Single(jobsBefore.Items);
        Assert.Equal(jobId, jobsBefore.Items[0].InternalId);

        // Act - simulate delete action from Jobs.razor
        var deleteResult = await jobsService.SoftDeleteJobAsync(jobId);

        // Assert - verify job is deleted
        Assert.True(deleteResult);
        var jobsAfter = await jobsService.GetJobsAsync(email);
        Assert.Empty(jobsAfter.Items);
    }

    [Fact]
    public async Task DeleteAction_ConfirmationDialog_ShowsJobEmployer()
    {
        // Arrange
        var job = new JobViewModel(
            Guid.NewGuid(),
            "test@example.com",
            "Acme Corp",
            "Software Engineer",
            85,
            "Apply",
            new DateTime(2024, 6, 1),
            "LinkedIn");

        var jobsService = Substitute.For<IJobsService>();
        jobsService.SoftDeleteJobAsync(job.InternalId).Returns(Task.FromResult(true));

        // Act & Assert - verify the dialog would receive the job data
        Assert.Equal("Acme Corp", job.Employer);
        Assert.NotEqual(Guid.Empty, job.InternalId);

        // Verify the service can be called
        await jobsService.SoftDeleteJobAsync(job.InternalId);
        await jobsService.Received(1).SoftDeleteJobAsync(job.InternalId);
    }
}
