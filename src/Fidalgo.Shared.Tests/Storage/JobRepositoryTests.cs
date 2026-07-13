using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fidalgo.Shared.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidalgo.Shared.Tests.Storage;

public class JobRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly JobDbContext _context;
    private readonly JobRepository _sut;

    public JobRepositoryTests()
    {
        // Use SQLite in-memory database for testing
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<JobDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new JobDbContext(options);
        _context.Database.EnsureCreated();

        _sut = new JobRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task SaveAsync_ShouldInsertNewJob()
    {
        // Arrange
        var job = CreateValidJob("test@example.com", "Employer 1", "EID-1");

        // Act
        var id = await _sut.SaveAsync(job);

        // Assert
        var dbJob = await _context.Jobs.FindAsync(id);
        Assert.NotNull(dbJob);
        Assert.Equal("Employer 1", dbJob.Employer);
    }

    [Fact]
    public async Task SaveAsync_ShouldReturnExistingId_WhenDuplicateJob()
    {
        // Arrange
        var job1 = CreateValidJob("test@example.com", "Employer 1", "EID-1");
        var id1 = await _sut.SaveAsync(job1);

        var job2 = CreateValidJob("test@example.com", "Employer 2", "EID-1");

        // Act
        var id2 = await _sut.SaveAsync(job2);

        // Assert
        Assert.Equal(id1, id2);
        var allJobs = await _context.Jobs.ToListAsync();
        Assert.Single(allJobs);
    }

    [Fact]
    public async Task QueryAsync_ShouldFilterByEmail()
    {
        // Arrange
        await _sut.SaveAsync(CreateValidJob("test1@example.com", "Employer 1", "EID-1"));
        await _sut.SaveAsync(CreateValidJob("test2@example.com", "Employer 2", "EID-2"));

        // Act
        var results = await _sut.QueryAsync("test1@example.com");

        // Assert
        Assert.Single(results);
        Assert.Equal("test1@example.com", results[0].Email);
    }

    [Fact]
    public async Task QueryAsync_ShouldFilterByDateRange()
    {
        // Arrange
        var job1 = CreateValidJob("test@example.com", "Employer 1", "EID-1");
        job1.PostedDate = new DateTime(2025, 1, 10);
        await _sut.SaveAsync(job1);

        var job2 = CreateValidJob("test@example.com", "Employer 2", "EID-2");
        job2.PostedDate = new DateTime(2025, 1, 20);
        await _sut.SaveAsync(job2);

        // Act
        var results = await _sut.QueryAsync("test@example.com", 
            dateFrom: new DateTime(2025, 1, 15), 
            dateTo: new DateTime(2025, 1, 25));

        // Assert
        Assert.Single(results);
        Assert.Equal("EID-2", results[0].EmployerJobId);
    }
    
    [Fact]
    public async Task QueryAsync_ShouldFilterByEmployerAndSource()
    {
        // Arrange
        var job1 = CreateValidJob("test@example.com", "Alpha Corp", "EID-1");
        job1.SourceWebsite = "indeed.com";
        await _sut.SaveAsync(job1);

        var job2 = CreateValidJob("test@example.com", "Beta Inc", "EID-2");
        job2.SourceWebsite = "linkedin.com";
        await _sut.SaveAsync(job2);

        // Act
        var results = await _sut.QueryAsync("test@example.com", employer: "Alpha", sourceWebsite: "indeed");

        // Assert
        Assert.Single(results);
        Assert.Equal("EID-1", results[0].EmployerJobId);
    }

    [Fact]
    public async Task SoftDeleteAsync_ShouldSetIsDeletedToTrue()
    {
        // Arrange
        var job = CreateValidJob("test@example.com", "Employer 1", "EID-1");
        var id = await _sut.SaveAsync(job);

        // Act
        var result = await _sut.SoftDeleteAsync(id);

        // Assert
        Assert.True(result);
        var dbJob = await _context.Jobs.FindAsync(id);
        Assert.NotNull(dbJob);
        Assert.True(dbJob.IsDeleted);
    }

    [Fact]
    public async Task GetDiscardedAsync_ShouldReturnOnlyDeletedJobs()
    {
        // Arrange
        var email = "test@example.com";
        var job1 = CreateValidJob(email, "Employer 1", "EID-1");
        var job2 = CreateValidJob(email, "Employer 2", "EID-2");
        
        var id1 = await _sut.SaveAsync(job1);
        await _sut.SaveAsync(job2);
        
        await _sut.SoftDeleteAsync(id1);

        // Act
        var results = await _sut.GetDiscardedAsync(email);

        // Assert
        Assert.Single(results);
        Assert.Equal(id1, results[0].InternalId);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueIfJobExists()
    {
        // Arrange
        var email = "test@example.com";
        var job = CreateValidJob(email, "Employer 1", "EID-1");
        job.SourceWebsite = "testsite.com";
        await _sut.SaveAsync(job);

        // Act
        var exists = await _sut.ExistsAsync(email, "TestSite.com", "EID-1");
        var notExists = await _sut.ExistsAsync(email, "TestSite.com", "EID-2");

        // Assert
        Assert.True(exists);
        Assert.False(notExists);
    }
    
    [Fact]
    public async Task GetByIdAsync_ShouldReturnJob()
    {
        // Arrange
        var job = CreateValidJob("test@example.com", "Employer 1", "EID-1");
        var id = await _sut.SaveAsync(job);

        // Act
        var result = await _sut.GetByIdAsync(id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(id, result.InternalId);
    }

    private JobEntity CreateValidJob(string email, string employer, string employerJobId)
    {
        return new JobEntity
        {
            Email = email,
            Employer = employer,
            EmployerJobId = employerJobId,
            Description = "Test Description",
            Pros = "Test Pros",
            Cons = "Test Cons",
            ResumeHints = "Test Hints",
            Score = 50,
            Recommendation = "Maybe",
            SourceWebsite = "indeed.com",
            PostedDate = DateTime.UtcNow
        };
    }
}