using Microsoft.EntityFrameworkCore;
using Fidalgo.Agent.Storage;
using Moq;
using Fidalgo.Agent.Tools;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Agent.Tests;

public class SaveJobToolTests
{
    private readonly SaveJobTool _tool;
    private readonly Mock<JobRepository> _repository;

    public SaveJobToolTests()
    {
        var options = new DbContextOptionsBuilder<JobDbContext>()
            .UseInMemoryDatabase("SaveJobToolTestDb")
            .Options;
        var dbContext = new JobDbContext(options);
        _repository = new Mock<JobRepository>(dbContext);
        var logger = Mock.Of<ILogger<SaveJobTool>>();
        _tool = new SaveJobTool(_repository.Object, logger);
    }

    [Fact]
    public async Task SaveAsync_ShouldSaveJob_WithValidData()
    {
        var expectedId = Guid.NewGuid();
        _repository.Setup(r => r.SaveAsync(It.IsAny<JobEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedId);

        var jobId = await _tool.SaveAsync(
            "test@example.com",
            "Acme Corp",
            "Software Engineer",
            "JOB-123",
            DateTime.UtcNow,
            80000,
            120000,
            "Software engineer position",
            "Good benefits",
            "No commute",
            "Matches resume well",
            85,
            "Apply",
            "indeed.com");

        Assert.Equal(expectedId, jobId);
        _repository.Verify(r => r.SaveAsync(
            It.Is<JobEntity>(j =>
                j.Email == "test@example.com"
                && j.Employer == "Acme Corp"
                && j.Title == "Software Engineer"
                && j.EmployerJobId == "JOB-123"
                && j.Score == 85
                && j.Recommendation == "Apply"
                && j.SourceWebsite == "indeed.com"
                && j.SalaryRangeLow == 80000
                && j.SalaryRangeHigh == 120000
                && !j.IsDeleted),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_ShouldSaveJob_WithNullOptionalFields()
    {
        var expectedId = Guid.NewGuid();
        _repository.Setup(r => r.SaveAsync(It.IsAny<JobEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedId);

        var jobId = await _tool.SaveAsync(
            "test@example.com",
            "Acme Corp",
            null,
            null,
            null,
            null,
            null,
            "Description",
            "Pros",
            "Cons",
            "Hints",
            50,
            "Maybe",
            "indeed.com");

        Assert.Equal(expectedId, jobId);
        _repository.Verify(r => r.SaveAsync(
            It.Is<JobEntity>(j =>
                j.Title == null
                && j.EmployerJobId == null
                && j.PostedDate == null
                && j.SalaryRangeLow == null
                && j.SalaryRangeHigh == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_ShouldThrow_ForScoreBelowZero()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _tool.SaveAsync(
                "test@example.com",
                "Acme Corp",
                null,
                "JOB-1",
                null, null, null,
                "Desc", "Pros", "Cons", "Hints",
                -1, "Apply", "indeed.com");
        });

        Assert.Equal("score", exception.ParamName);
        _repository.Verify(r => r.SaveAsync(It.IsAny<JobEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_ShouldThrow_ForScoreAboveHundred()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _tool.SaveAsync(
                "test@example.com",
                "Acme Corp",
                null,
                "JOB-1",
                null, null, null,
                "Desc", "Pros", "Cons", "Hints",
                101, "Apply", "indeed.com");
        });

        Assert.Equal("score", exception.ParamName);
    }

    [Fact]
    public async Task SaveAsync_ShouldThrow_ForEmptyRecommendation()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _tool.SaveAsync(
                "test@example.com",
                "Acme Corp",
                null,
                "JOB-1",
                null, null, null,
                "Desc", "Pros", "Cons", "Hints",
                50, "", "indeed.com");
        });

        Assert.Equal("recommendation", exception.ParamName);
    }

    [Fact]
    public async Task SaveAsync_ShouldThrow_ForInvalidRecommendation()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _tool.SaveAsync(
                "test@example.com",
                "Acme Corp",
                null,
                "JOB-1",
                null, null, null,
                "Desc", "Pros", "Cons", "Hints",
                50, "Ignore", "indeed.com");
        });

        Assert.Equal("recommendation", exception.ParamName);
    }

    [Theory]
    [InlineData("Apply")]
    [InlineData("Maybe")]
    [InlineData("Do not apply")]
    public async Task SaveAsync_ShouldAcceptAllValidRecommendations(string recommendation)
    {
        var expectedId = Guid.NewGuid();
        _repository.Setup(r => r.SaveAsync(It.IsAny<JobEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedId);

        var jobId = await _tool.SaveAsync(
            "test@example.com",
            "Acme Corp",
            null,
            "JOB-1",
            null, null, null,
            "Desc", "Pros", "Cons", "Hints",
            50, recommendation, "indeed.com");

        Assert.Equal(expectedId, jobId);
    }
}
