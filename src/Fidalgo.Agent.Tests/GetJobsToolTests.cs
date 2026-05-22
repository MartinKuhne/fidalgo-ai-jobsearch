using Microsoft.EntityFrameworkCore;
using Fidalgo.Agent.Storage;
using Moq;
using Fidalgo.Agent.Tools;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Agent.Tests;

public class GetJobsToolTests
{
    private readonly GetJobsTool _tool;
    private readonly Mock<JobRepository> _repository;

    public GetJobsToolTests()
    {
        var options = new DbContextOptionsBuilder<JobDbContext>()
            .UseInMemoryDatabase("GetJobsToolTestDb")
            .Options;
        var dbContext = new JobDbContext(options);
        _repository = new Mock<JobRepository>(dbContext);
        var logger = Mock.Of<ILogger<GetJobsTool>>();
        _tool = new GetJobsTool(_repository.Object, logger);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnAllJobs_WhenNoFilters()
    {
        var jobs = new List<JobEntity>
        {
            new() { Employer = "Acme", EmployerJobId = "1" },
            new() { Employer = "Globex", EmployerJobId = "2" }
        };
        _repository.Setup(r => r.QueryAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        var result = await _tool.GetAsync("test@example.com");

        Assert.Equal(2, result.Count);
        _repository.Verify(r => r.QueryAsync(
            "test@example.com",
            null, null, null, null, null,
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_ShouldFilterByEmployer()
    {
        var jobs = new List<JobEntity>
        {
            new() { Employer = "Acme", EmployerJobId = "1" }
        };
        _repository.Setup(r => r.QueryAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        await _tool.GetAsync("test@example.com", null, null, "Acme", null, null);

        _repository.Verify(r => r.QueryAsync(
            "test@example.com",
            null, null, "Acme", null, null,
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_ShouldFilterByEmployerJobId()
    {
        var jobs = new List<JobEntity> { new() };
        _repository.Setup(r => r.QueryAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        await _tool.GetAsync("test@example.com", null, null, null, "JOB-123", null);

        _repository.Verify(r => r.QueryAsync(
            "test@example.com",
            null, null, null, "JOB-123", null,
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_ShouldFilterBySourceWebsite()
    {
        var jobs = new List<JobEntity> { new() };
        _repository.Setup(r => r.QueryAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        await _tool.GetAsync("test@example.com", null, null, null, null, "indeed.com");

        _repository.Verify(r => r.QueryAsync(
            "test@example.com",
            null, null, null, null, "indeed.com",
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_ShouldFilterByDateRange()
    {
        var jobs = new List<JobEntity> { new() };
        var dateFrom = new DateTime(2024, 1, 1);
        var dateTo = new DateTime(2024, 12, 31);
        _repository.Setup(r => r.QueryAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        await _tool.GetAsync("test@example.com", dateFrom, dateTo);

        _repository.Verify(r => r.QueryAsync(
            "test@example.com",
            dateFrom, dateTo, null, null, null,
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnEmptyList_WhenNoJobsFound()
    {
        _repository.Setup(r => r.QueryAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobEntity>());

        var result = await _tool.GetAsync("test@example.com");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAsync_ShouldExcludeDeletedJobsByDefault()
    {
        _repository.Setup(r => r.QueryAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobEntity>());

        await _tool.GetAsync("test@example.com");

        _repository.Verify(r => r.QueryAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
