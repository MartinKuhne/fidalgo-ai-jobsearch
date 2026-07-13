using Fidalgo.Host.Services;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Fidalgo.Host.Tests;

public class JobsServiceTests
{
    private readonly JobRepository _repository;
    private readonly JobsService _service;

    public JobsServiceTests()
    {
        _repository = Substitute.For<JobRepository>((JobDbContext)null!);
        var logger = NullLogger<JobsService>.Instance;
        
        _service = new JobsService(_repository, logger);
    }

    [Fact]
    public async Task GetJobsAsync_WithEmptyEmail_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetJobsAsync(string.Empty));
    }

    [Fact]
    public async Task GetJobsAsync_WithInvalidPage_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.GetJobsAsync("test@example.com", page: 0));
    }

    [Fact]
    public async Task GetJobsAsync_WithInvalidPageSize_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.GetJobsAsync("test@example.com", pageSize: 101));
    }

    [Fact]
    public async Task GetJobsAsync_ReturnsSortedPagedResults()
    {
        // Arrange
        var email = "test@example.com";
        var jobs = new List<JobEntity>
        {
            new JobEntity { InternalId = Guid.NewGuid(), Email = email, Score = 50, PostedDate = DateTime.UtcNow.AddDays(-2), Employer = "A", Title = "Dev", Recommendation = "Yes", Description = "Desc", Pros = "P", Cons = "C", ResumeHints = "H", SourceWebsite = "W" },
            new JobEntity { InternalId = Guid.NewGuid(), Email = email, Score = 90, PostedDate = DateTime.UtcNow.AddDays(-1), Employer = "B", Title = "Dev", Recommendation = "Yes", Description = "Desc", Pros = "P", Cons = "C", ResumeHints = "H", SourceWebsite = "W" },
            new JobEntity { InternalId = Guid.NewGuid(), Email = email, Score = 50, PostedDate = DateTime.UtcNow, Employer = "C", Title = "Dev", Recommendation = "Yes", Description = "Desc", Pros = "P", Cons = "C", ResumeHints = "H", SourceWebsite = "W" }
        };

        _repository.QueryAsync(email, Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(jobs));

        // Act
        var result = await _service.GetJobsAsync(email, page: 1, pageSize: 2);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(2, result.Items.Count);
        
        // Check sorting: Highest score first, then newest date first
        Assert.Equal(90, result.Items[0].Score);
        Assert.Equal("B", result.Items[0].Company);
        
        Assert.Equal(50, result.Items[1].Score);
        Assert.Equal("C", result.Items[1].Company); // newer date than A
    }

    [Fact]
    public async Task SoftDeleteJobAsync_WithEmptyId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SoftDeleteJobAsync(Guid.Empty));
    }

    [Fact]
    public async Task SoftDeleteJobAsync_ValidId_ReturnsRepositoryResult()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.SoftDeleteAsync(id, Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        // Act
        var result = await _service.SoftDeleteJobAsync(id);

        // Assert
        Assert.True(result);
        await _repository.Received(1).SoftDeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetJobByIdAsync_WithEmptyId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetJobByIdAsync(Guid.Empty));
    }

    [Fact]
    public async Task GetJobByIdAsync_ValidId_ReturnsJob()
    {
        // Arrange
        var id = Guid.NewGuid();
        var job = new JobEntity { InternalId = id, Email = "a@b.com", Employer = "E", Title = "T", Recommendation = "R", Description = "D", Pros = "P", Cons = "C", ResumeHints = "H", Score = 100, SourceWebsite = "S" };
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<JobEntity?>(job));

        // Act
        var result = await _service.GetJobByIdAsync(id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(id, result!.InternalId);
    }
}
