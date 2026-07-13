using Fidalgo.Host.Services;
using Fidalgo.Shared.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fidalgo.Host.Tests;

public class TenantServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly JobDbContext _context;
    private readonly TenantService _service;

    public TenantServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<JobDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new JobDbContext(options);
        _context.Database.EnsureCreated();

        var logger = NullLogger<TenantService>.Instance;
        _service = new TenantService(_context, logger);
    }

    [Fact]
    public async Task GetTenantEmailsAsync_ReturnsAlphabeticallyOrderedTenantsAndJobCounts()
    {
        // Arrange
        _context.Jobs.AddRange(
            new JobEntity { InternalId = Guid.NewGuid(), Email = "zebra@example.com", Employer = "Z", Title = "T", Recommendation = "R", Description = "D", Pros = "P", Cons = "C", ResumeHints = "H", Score = 100, SourceWebsite = "S" },
            new JobEntity { InternalId = Guid.NewGuid(), Email = "apple@example.com", Employer = "A", Title = "T", Recommendation = "R", Description = "D", Pros = "P", Cons = "C", ResumeHints = "H", Score = 100, SourceWebsite = "S" },
            new JobEntity { InternalId = Guid.NewGuid(), Email = "apple@example.com", Employer = "A2", Title = "T", Recommendation = "R", Description = "D", Pros = "P", Cons = "C", ResumeHints = "H", Score = 100, SourceWebsite = "S" },
            new JobEntity { InternalId = Guid.NewGuid(), Email = "banana@example.com", Employer = "B", Title = "T", Recommendation = "R", Description = "D", Pros = "P", Cons = "C", ResumeHints = "H", Score = 100, SourceWebsite = "S" },
            // Deleted job - should be ignored
            new JobEntity { InternalId = Guid.NewGuid(), Email = "banana@example.com", Employer = "B2", Title = "T", Recommendation = "R", Description = "D", Pros = "P", Cons = "C", ResumeHints = "H", Score = 100, SourceWebsite = "S", IsDeleted = true }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTenantEmailsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        
        // Check order and counts
        Assert.Equal("apple@example.com", result[0].Email);
        Assert.Equal(2, result[0].JobCount);
        
        Assert.Equal("banana@example.com", result[1].Email);
        Assert.Equal(1, result[1].JobCount);
        
        Assert.Equal("zebra@example.com", result[2].Email);
        Assert.Equal(1, result[2].JobCount);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
