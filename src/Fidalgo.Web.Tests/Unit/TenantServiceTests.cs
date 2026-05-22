using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;
using Fidalgo.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Fidalgo.Web.Tests.Unit;

public class TenantServiceTests
{
    private readonly ILogger<TenantService> _logger = Substitute.For<ILogger<TenantService>>();
    private DbContextOptions<JobDbContext> CreateOptions(string databaseName)
    {
        return new DbContextOptionsBuilder<JobDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    [Fact]
    public async Task GetTenantEmailsAsync_ReturnsUniqueEmailsWithJobCounts()
    {
        // Arrange
        using var context = new JobDbContext(CreateOptions($"Test_{Guid.NewGuid()}"));
        var service = new TenantService(context, _logger);

        await context.Jobs.AddRangeAsync(
            new JobEntity { Email = "alice@example.com", Employer = "Company A", Title = "Job 1", Score = 50, Recommendation = "good", InternalId = Guid.NewGuid() },
            new JobEntity { Email = "alice@example.com", Employer = "Company B", Title = "Job 2", Score = 70, Recommendation = "good", InternalId = Guid.NewGuid() },
            new JobEntity { Email = "bob@example.com", Employer = "Company C", Title = "Job 3", Score = 90, Recommendation = "excellent", InternalId = Guid.NewGuid() }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetTenantEmailsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        var alice = result.First(t => t.Email == "alice@example.com");
        Assert.Equal(2, alice.JobCount);
        var bob = result.First(t => t.Email == "bob@example.com");
        Assert.Equal(1, bob.JobCount);
    }

    [Fact]
    public async Task GetTenantEmailsAsync_ExcludesDeletedJobs()
    {
        // Arrange
        using var context = new JobDbContext(CreateOptions($"Test_{Guid.NewGuid()}"));
        var service = new TenantService(context, _logger);

        await context.Jobs.AddRangeAsync(
            new JobEntity { Email = "alice@example.com", Employer = "Company A", Title = "Active Job", Score = 50, Recommendation = "good", IsDeleted = false, InternalId = Guid.NewGuid() },
            new JobEntity { Email = "alice@example.com", Employer = "Company B", Title = "Deleted Job", Score = 70, Recommendation = "good", IsDeleted = true, InternalId = Guid.NewGuid() }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetTenantEmailsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(1, result[0].JobCount);
    }

    [Fact]
    public async Task GetTenantEmailsAsync_ReturnsEmptyListWhenNoJobsExist()
    {
        // Arrange
        using var context = new JobDbContext(CreateOptions($"Test_{Guid.NewGuid()}"));
        var service = new TenantService(context, _logger);

        // Act
        var result = await service.GetTenantEmailsAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTenantEmailsAsync_ReturnsEmailsSortedAlphabetically()
    {
        // Arrange
        using var context = new JobDbContext(CreateOptions($"Test_{Guid.NewGuid()}"));
        var service = new TenantService(context, _logger);

        await context.Jobs.AddRangeAsync(
            new JobEntity { Email = "charlie@example.com", Employer = "Company A", Title = "Job 1", Score = 50, Recommendation = "good", InternalId = Guid.NewGuid() },
            new JobEntity { Email = "alice@example.com", Employer = "Company B", Title = "Job 2", Score = 70, Recommendation = "good", InternalId = Guid.NewGuid() },
            new JobEntity { Email = "bob@example.com", Employer = "Company C", Title = "Job 3", Score = 90, Recommendation = "excellent", InternalId = Guid.NewGuid() }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetTenantEmailsAsync();

        // Assert
        Assert.Equal("alice@example.com", result[0].Email);
        Assert.Equal("bob@example.com", result[1].Email);
        Assert.Equal("charlie@example.com", result[2].Email);
    }
}
