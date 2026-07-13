using Fidalgo.Ingest.Tools;
using Fidalgo.Shared.Storage;
using Fidalgo.Shared.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Net.Http;

namespace Fidalgo.Ingest.Tests.Tools;

public class SaveJobToolTests
{
    private readonly JobRepository _repository;
    private readonly MarkdownFetchTool _markdownFetchTool;
    private readonly ILogger<SaveJobTool> _logger;
    private readonly SaveJobTool _sut;
    private readonly JobDbContext _dbContext;

    public SaveJobToolTests()
    {
        var options = new DbContextOptionsBuilder<JobDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        _dbContext = new JobDbContext(options);
        _repository = new JobRepository(_dbContext);
        
        var httpClient = new HttpClient();
        _markdownFetchTool = Substitute.For<MarkdownFetchTool>(httpClient, Substitute.For<ILogger<MarkdownFetchTool>>());
        _logger = Substitute.For<ILogger<SaveJobTool>>();
        
        _sut = new SaveJobTool(_repository, _markdownFetchTool, _logger);
    }

    [Fact]
    public async Task SaveAsync_WithValidJob_SavesToDatabase()
    {
        // Arrange
        string email = "test@example.com";
        string employer = "Test Employer";
        string title = "Software Engineer";
        string employerJobId = "job123";
        DateTime postedDate = DateTime.UtcNow;
        decimal salaryLow = 100000;
        decimal salaryHigh = 120000;
        string jobUrl = "https://example.com/job";
        string sourceWebsite = "example.com";

        _markdownFetchTool.FetchAsync(jobUrl, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("Fetched description from markdown"));

        // Act
        var result = await _sut.SaveAsync(email, employer, title, employerJobId, postedDate, salaryLow, salaryHigh, jobUrl, sourceWebsite);

        // Assert
        Assert.NotEqual(Guid.Empty, result);

        var savedJob = await _dbContext.Jobs.FirstOrDefaultAsync(j => j.InternalId == result);
        Assert.NotNull(savedJob);
        Assert.Equal(email, savedJob.Email);
        Assert.Equal(employer, savedJob.Employer);
        Assert.Equal(title, savedJob.Title);
        Assert.Equal("Fetched description from markdown", savedJob.Description);
        Assert.Equal(0, savedJob.Score);
        Assert.Equal("Maybe", savedJob.Recommendation);
    }

    [Fact]
    public async Task SaveAsync_WithEmptyUrl_SavesWithEmptyDescription()
    {
        // Arrange
        string email = "test@example.com";
        string employer = "Test Employer";
        string sourceWebsite = "example.com";

        // Act
        var result = await _sut.SaveAsync(email, employer, null, null, null, null, null, "", sourceWebsite);

        // Assert
        var savedJob = await _dbContext.Jobs.FirstOrDefaultAsync(j => j.InternalId == result);
        Assert.NotNull(savedJob);
        Assert.Equal(string.Empty, savedJob.Description);
    }
}
