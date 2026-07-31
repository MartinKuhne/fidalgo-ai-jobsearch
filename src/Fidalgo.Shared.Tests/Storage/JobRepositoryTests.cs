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

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenJobMissing()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges()
    {
        // Arrange
        var job = CreateValidJob("test@example.com", "Employer 1", "EID-1");
        var id = await _sut.SaveAsync(job);

        // Act
        job.InternalId = id;
        job.Score = 95;
        job.Recommendation = "Apply";
        job.Pros = "Great match";
        await _sut.UpdateAsync(job);

        // Assert
        var reloaded = await _sut.GetByIdAsync(id);
        Assert.NotNull(reloaded);
        Assert.Equal(95, reloaded.Score);
        Assert.Equal("Apply", reloaded.Recommendation);
        Assert.Equal("Great match", reloaded.Pros);
    }

    [Fact]
    public async Task GetUntriagedAsync_ShouldReturnOnlyJobsWithZeroScore()
    {
        // Arrange
        var email = "test@example.com";
        var untriaged = CreateValidJob(email, "Employer 1", "EID-1");
        untriaged.Score = 0;
        var triaged = CreateValidJob(email, "Employer 2", "EID-2");
        triaged.Score = 80;
        await _sut.SaveAsync(untriaged);
        await _sut.SaveAsync(triaged);

        // Act
        var results = await _sut.GetUntriagedAsync(email);

        // Assert
        Assert.Single(results);
        Assert.Equal("EID-1", results[0].EmployerJobId);
    }

    [Fact]
    public async Task GetUntriagedAsync_ShouldExcludeDeletedJobs()
    {
        // Arrange
        var email = "test@example.com";
        var untriaged = CreateValidJob(email, "Employer 1", "EID-1");
        untriaged.Score = 0;
        var untriagedButDeleted = CreateValidJob(email, "Employer 2", "EID-2");
        untriagedButDeleted.Score = 0;
        var id1 = await _sut.SaveAsync(untriaged);
        var id2 = await _sut.SaveAsync(untriagedButDeleted);
        await _sut.SoftDeleteAsync(id2);

        // Act
        var results = await _sut.GetUntriagedAsync(email);

        // Assert
        Assert.Single(results);
        Assert.Equal(id1, results[0].InternalId);
    }

    [Fact]
    public async Task QueryAsync_ShouldFilterByDateRange_WhenBothBoundsProvided()
    {
        // Arrange
        var email = "test@example.com";
        var inRange = CreateValidJob(email, "E1", "J1");
        inRange.PostedDate = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc);
        var before = CreateValidJob(email, "E2", "J2");
        before.PostedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var after = CreateValidJob(email, "E3", "J3");
        after.PostedDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        await _sut.SaveAsync(inRange);
        await _sut.SaveAsync(before);
        await _sut.SaveAsync(after);

        // Act
        var results = await _sut.QueryAsync(
            email,
            dateFrom: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            dateTo: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        // Assert
        Assert.Single(results);
        Assert.Equal("J1", results[0].EmployerJobId);
    }

    [Fact]
    public async Task QueryAsync_ShouldFilterByExactEmployerJobId()
    {
        // Arrange
        var email = "test@example.com";
        await _sut.SaveAsync(CreateValidJob(email, "E1", "J1"));
        await _sut.SaveAsync(CreateValidJob(email, "E2", "J2"));

        // Act
        var results = await _sut.QueryAsync(email, employerJobId: "J1");

        // Assert
        Assert.Single(results);
        Assert.Equal("J1", results[0].EmployerJobId);
    }

    [Fact]
    public async Task QueryAsync_ShouldIncludeDeletedJobs_WhenExcludeDeletedFalse()
    {
        // Arrange
        var email = "test@example.com";
        var job = CreateValidJob(email, "E1", "J1");
        var id = await _sut.SaveAsync(job);
        await _sut.SoftDeleteAsync(id);

        // Act
        var withDeleted = await _sut.QueryAsync(email, excludeDeleted: false);
        var withoutDeleted = await _sut.QueryAsync(email, excludeDeleted: true);

        // Assert
        Assert.Single(withDeleted);
        Assert.Empty(withoutDeleted);
    }

    [Fact]
    public async Task SaveAsync_ShouldReturnExistingId_WhenDuplicateEmailAndJobId()
    {
        // Arrange
        var job1 = CreateValidJob("test@example.com", "E1", "J1");
        var id1 = await _sut.SaveAsync(job1);

        // Act
        var job2 = CreateValidJob("test@example.com", "E1", "J1");
        var id2 = await _sut.SaveAsync(job2);

        // Assert
        Assert.Equal(id1, id2);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenCaseDiffersButJobIdDiffers()
    {
        // Arrange
        var email = "test@example.com";
        var job = CreateValidJob(email, "E1", "J1");
        job.SourceWebsite = "TestSite.com";
        await _sut.SaveAsync(job);

        // Act
        var exists = await _sut.ExistsAsync(email, "TestSite.com", "J2");

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task QueryAsync_ShouldOrderByPostedDateDescending()
    {
        // Arrange
        var email = "test@example.com";
        var newest = CreateValidJob(email, "E1", "J1");
        newest.PostedDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var middle = CreateValidJob(email, "E2", "J2");
        middle.PostedDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var oldest = CreateValidJob(email, "E3", "J3");
        oldest.PostedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await _sut.SaveAsync(newest);
        await _sut.SaveAsync(middle);
        await _sut.SaveAsync(oldest);

        // Act
        var results = await _sut.QueryAsync(email);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Equal("J1", results[0].EmployerJobId);
        Assert.Equal("J2", results[1].EmployerJobId);
        Assert.Equal("J3", results[2].EmployerJobId);
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