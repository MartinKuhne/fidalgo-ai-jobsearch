using Microsoft.EntityFrameworkCore;
using Fidalgo.Shared.Storage;
using Moq;
using Fidalgo.Shared.Tools;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Shared.Tests;

public class JobInDbToolTests
{
    private readonly JobInDbTool _tool;
    private readonly Mock<JobRepository> _repository;

    public JobInDbToolTests()
    {
        var options = new DbContextOptionsBuilder<JobDbContext>()
            .UseInMemoryDatabase("JobInDbToolTestDb")
            .Options;
        var dbContext = new JobDbContext(options);
        _repository = new Mock<JobRepository>(dbContext);
        var logger = Mock.Of<ILogger<JobInDbTool>>();
        _tool = new JobInDbTool(_repository.Object, logger);
    }

    [Fact]
    public async Task ContainsAsync_ShouldReturnTrue_WhenJobExists()
    {
        _repository.Setup(r => r.ExistsAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _tool.ContainsAsync("test@example.com", "indeed.com", "JOB-123");

        Assert.True(result);
        _repository.Verify(r => r.ExistsAsync(
            "test@example.com",
            "indeed.com",
            "JOB-123",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ContainsAsync_ShouldReturnFalse_WhenJobDoesNotExist()
    {
        _repository.Setup(r => r.ExistsAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _tool.ContainsAsync("test@example.com", "indeed.com", "JOB-999");

        Assert.False(result);
    }

    [Fact]
    public async Task ContainsAsync_ShouldPassSiteUrlToRepository()
    {
        _repository.Setup(r => r.ExistsAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _tool.ContainsAsync("user@test.com", "glassdoor.com", "GDL-456");

        _repository.Verify(r => r.ExistsAsync(
            "user@test.com",
            "glassdoor.com",
            "GDL-456",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ContainsAsync_ShouldPassJobIdToRepository()
    {
        _repository.Setup(r => r.ExistsAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _tool.ContainsAsync("user@test.com", "indeed.com", "unique-job-id-xyz");

        _repository.Verify(r => r.ExistsAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            "unique-job-id-xyz",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}