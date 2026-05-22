using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;
using Fidalgo.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Fidalgo.Web.Tests.Unit;

public class JobsServiceTests
{
    private readonly ILogger<JobsService> _logger = Substitute.For<ILogger<JobsService>>();

    private DbContextOptions<JobDbContext> CreateOptions(string databaseName)
    {
        return new DbContextOptionsBuilder<JobDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    [Fact]
    public async Task GetJobsAsync_ReturnsJobsOrderedByScoreDescending()
    {
        // Arrange
        using var context = new JobDbContext(CreateOptions($"Test_{Guid.NewGuid()}"));
        var repository = new JobRepository(context);
        var service = new JobsService(repository, _logger);

        var email = "test@example.com";
        await context.Jobs.AddRangeAsync(
            new JobEntity { Email = email, Employer = "Company A", Title = "Job 1", Score = 50, Recommendation = "good", InternalId = Guid.NewGuid() },
            new JobEntity { Email = email, Employer = "Company B", Title = "Job 2", Score = 90, Recommendation = "excellent", InternalId = Guid.NewGuid() },
            new JobEntity { Email = email, Employer = "Company C", Title = "Job 3", Score = 70, Recommendation = "good", InternalId = Guid.NewGuid() }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetJobsAsync(email);

        // Assert
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(90, result.Items[0].Score);
        Assert.Equal(70, result.Items[1].Score);
        Assert.Equal(50, result.Items[2].Score);
    }

    [Fact]
    public async Task GetJobsAsync_ApplyDateFilterCorrectly()
    {
        // Arrange
        using var context = new JobDbContext(CreateOptions($"Test_{Guid.NewGuid()}"));
        var repository = new JobRepository(context);
        var service = new JobsService(repository, _logger);

        var email = "test@example.com";
        var dateFrom = new DateTime(2024, 6, 1);
        await context.Jobs.AddRangeAsync(
            new JobEntity { Email = email, Employer = "Company A", Title = "Old Job", Score = 50, Recommendation = "good", PostedDate = new DateTime(2024, 1, 1), InternalId = Guid.NewGuid() },
            new JobEntity { Email = email, Employer = "Company B", Title = "New Job", Score = 90, Recommendation = "excellent", PostedDate = new DateTime(2024, 7, 1), InternalId = Guid.NewGuid() }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetJobsAsync(email, dateFrom: dateFrom);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("New Job", result.Items[0].Title);
    }

    [Fact]
    public async Task GetJobsAsync_ExcludesDeletedJobs()
    {
        // Arrange
        using var context = new JobDbContext(CreateOptions($"Test_{Guid.NewGuid()}"));
        var repository = new JobRepository(context);
        var service = new JobsService(repository, _logger);

        var email = "test@example.com";
        await context.Jobs.AddRangeAsync(
            new JobEntity { Email = email, Employer = "Company A", Title = "Active Job", Score = 50, Recommendation = "good", IsDeleted = false, InternalId = Guid.NewGuid() },
            new JobEntity { Email = email, Employer = "Company B", Title = "Deleted Job", Score = 90, Recommendation = "excellent", IsDeleted = true, InternalId = Guid.NewGuid() }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetJobsAsync(email);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("Active Job", result.Items[0].Title);
    }

    [Fact]
    public async Task GetJobsAsync_ReturnsPaginatedResults()
    {
        // Arrange
        using var context = new JobDbContext(CreateOptions($"Test_{Guid.NewGuid()}"));
        var repository = new JobRepository(context);
        var service = new JobsService(repository, _logger);

        var email = "test@example.com";
        var jobs = new List<JobEntity>();
        for (var i = 0; i < 10; i++)
        {
            jobs.Add(new JobEntity { Email = email, Employer = $"Company {i}", Title = $"Job {i}", Score = i, Recommendation = "good", InternalId = Guid.NewGuid() });
        }
        await context.Jobs.AddRangeAsync(jobs);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetJobsAsync(email, page: 1, pageSize: 5);

        // Assert
        Assert.Equal(5, result.Items.Count);
        Assert.Equal(10, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(1, result.Page);
    }

    [Fact]
    public async Task SoftDeleteJobAsync_MarksJobAsDeleted()
    {
        // Arrange
        using var context = new JobDbContext(CreateOptions($"Test_{Guid.NewGuid()}"));
        var repository = new JobRepository(context);
        var service = new JobsService(repository, _logger);

        var jobId = Guid.NewGuid();
        var email = "test@example.com";
        await context.Jobs.AddAsync(new JobEntity { Email = email, Employer = "Company A", Title = "Job", Score = 50, Recommendation = "good", InternalId = jobId });
        await context.SaveChangesAsync();

        // Act
        var result = await service.SoftDeleteJobAsync(jobId);

        // Assert
        Assert.True(result);
        var job = await context.Jobs.FindAsync(jobId);
        Assert.True(job!.IsDeleted);
    }

    [Fact]
    public async Task SoftDeleteJobAsync_ReturnsFalseForNonExistentJob()
    {
        // Arrange
        using var context = new JobDbContext(CreateOptions($"Test_{Guid.NewGuid()}"));
        var repository = new JobRepository(context);
        var service = new JobsService(repository, _logger);

        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await service.SoftDeleteJobAsync(nonExistentId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetJobByIdAsync_ReturnsJobByInternalId()
    {
        // Arrange
        using var context = new JobDbContext(CreateOptions($"Test_{Guid.NewGuid()}"));
        var repository = new JobRepository(context);
        var service = new JobsService(repository, _logger);

        var jobId = Guid.NewGuid();
        var email = "test@example.com";
        await context.Jobs.AddAsync(new JobEntity { Email = email, Employer = "Company A", Title = "Job", Score = 50, Recommendation = "good", InternalId = jobId });
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetJobByIdAsync(jobId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(jobId, result!.InternalId);
        Assert.Equal("Company A", result.Employer);
    }

    [Fact]
    public async Task GetJobByIdAsync_ReturnsNullForNonExistentJob()
    {
        // Arrange
        using var context = new JobDbContext(CreateOptions($"Test_{Guid.NewGuid()}"));
        var repository = new JobRepository(context);
        var service = new JobsService(repository, _logger);

        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await service.GetJobByIdAsync(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetJobsAsync_ThrowsArgumentExceptionForEmptyEmail()
    {
        // Arrange
        using var context = new JobDbContext(CreateOptions($"Test_{Guid.NewGuid()}"));
        var repository = new JobRepository(context);
        var service = new JobsService(repository, _logger);

        var emptyEmail = string.Empty;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () => await service.GetJobsAsync(emptyEmail));
    }

    [Fact]
    public async Task GetJobsAsync_ThrowsArgumentOutOfRangeExceptionForInvalidPage()
    {
        // Arrange
        using var context = new JobDbContext(CreateOptions($"Test_{Guid.NewGuid()}"));
        var repository = new JobRepository(context);
        var service = new JobsService(repository, _logger);

        var email = "test@example.com";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await service.GetJobsAsync(email, page: 0));
    }

    [Fact]
    public async Task GetJobsAsync_ThrowsArgumentOutOfRangeExceptionForInvalidPageSize()
    {
        // Arrange
        using var context = new JobDbContext(CreateOptions($"Test_{Guid.NewGuid()}"));
        var repository = new JobRepository(context);
        var service = new JobsService(repository, _logger);

        var email = "test@example.com";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await service.GetJobsAsync(email, pageSize: 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await service.GetJobsAsync(email, pageSize: 101));
    }

    [Fact]
    public async Task GetJobsAsync_ReturnsEmptyListWhenNoJobsExist()
    {
        // Arrange
        using var context = new JobDbContext(CreateOptions($"Test_{Guid.NewGuid()}"));
        var repository = new JobRepository(context);
        var service = new JobsService(repository, _logger);

        var email = "nonexistent@example.com";

        // Act
        var result = await service.GetJobsAsync(email);

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalItems);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetJobsAsync_SortsByScoreThenPostedDate()
    {
        // Arrange
        using var context = new JobDbContext(CreateOptions($"Test_{Guid.NewGuid()}"));
        var repository = new JobRepository(context);
        var service = new JobsService(repository, _logger);

        var email = "test@example.com";
        await context.Jobs.AddRangeAsync(
            new JobEntity { Email = email, Employer = "Company A", Title = "Job 1", Score = 80, Recommendation = "good", PostedDate = new DateTime(2024, 1, 1), InternalId = Guid.NewGuid() },
            new JobEntity { Email = email, Employer = "Company B", Title = "Job 2", Score = 80, Recommendation = "good", PostedDate = new DateTime(2024, 7, 1), InternalId = Guid.NewGuid() },
            new JobEntity { Email = email, Employer = "Company C", Title = "Job 3", Score = 90, Recommendation = "excellent", PostedDate = new DateTime(2024, 3, 1), InternalId = Guid.NewGuid() }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetJobsAsync(email);

        // Assert
        Assert.Equal(90, result.Items[0].Score);
        Assert.Equal(80, result.Items[1].Score);
        Assert.Equal(80, result.Items[2].Score);
        Assert.Equal(new DateTime(2024, 7, 1), result.Items[1].PostedDate);
        Assert.Equal(new DateTime(2024, 1, 1), result.Items[2].PostedDate);
    }
}
