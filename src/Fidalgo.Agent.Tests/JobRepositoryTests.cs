using Microsoft.EntityFrameworkCore;
using Fidalgo.Agent.Storage;

namespace Fidalgo.Agent.Tests;

public class JobRepositoryTests
{
    private readonly JobDbContext _context;
    private readonly JobRepository _repository;

    public JobRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<JobDbContext>()
            .UseInMemoryDatabase($"FidalgoTestDb_{Guid.NewGuid()}")
            .Options;
        _context = new JobDbContext(options);
        _repository = new JobRepository(_context);
    }

    [Fact]
    public async Task SaveAsync_ShouldSaveNewJob()
    {
        var job = new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        };

        var jobId = await _repository.SaveAsync(job);

        Assert.NotEqual(Guid.Empty, jobId);
        var saved = await _context.Jobs.FindAsync(jobId);
        Assert.NotNull(saved);
        Assert.Equal("Acme Corp", saved.Employer);
    }

    [Fact]
    public async Task SaveAsync_ShouldReturnExistingJobId_WhenDuplicate()
    {
        var job1 = new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        };

        var id1 = await _repository.SaveAsync(job1);

        var job2 = new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Different description",
            Pros = "Different pros",
            Cons = "Different cons",
            ResumeHints = "Different hints",
            Score = 90,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        };

        var id2 = await _repository.SaveAsync(job2);

        Assert.Equal(id1, id2);
        var count = await _context.Jobs.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SaveAsync_ShouldSetIsDeletedToFalse()
    {
        var job = new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-002",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        };

        var jobId = await _repository.SaveAsync(job);
        var saved = await _context.Jobs.FindAsync(jobId);

        Assert.NotNull(saved);
        Assert.False(saved.IsDeleted);
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnAllJobsForEmail()
    {
        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com",
            PostedDate = new DateTime(2024, 6, 1)
        });

        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "Globex",
            EmployerJobId = "JOB-002",
            Description = "Data scientist",
            Pros = "Interesting work",
            Cons = "Lower pay",
            ResumeHints = "Partial match",
            Score = 60,
            Recommendation = "Maybe",
            SourceWebsite = "indeed.com",
            PostedDate = new DateTime(2024, 7, 1)
        });

        var jobs = await _repository.QueryAsync("test@example.com");

        Assert.Equal(2, jobs.Count);
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnEmpty_WhenNoJobsForEmail()
    {
        var jobs = await _repository.QueryAsync("nobody@example.com");

        Assert.Empty(jobs);
    }

    [Fact]
    public async Task QueryAsync_ShouldFilterByEmployer()
    {
        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        });

        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "Globex",
            EmployerJobId = "JOB-002",
            Description = "Data scientist",
            Pros = "Interesting work",
            Cons = "Lower pay",
            ResumeHints = "Partial match",
            Score = 60,
            Recommendation = "Maybe",
            SourceWebsite = "indeed.com"
        });

        var jobs = await _repository.QueryAsync("test@example.com", null, null, "Acme Corp");

        Assert.Single(jobs);
        Assert.Equal("Acme Corp", jobs[0].Employer);
    }

    [Fact]
    public async Task QueryAsync_ShouldFilterByEmployerJobId()
    {
        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        });

        var jobs = await _repository.QueryAsync("test@example.com", null, null, null, "JOB-001");

        Assert.Single(jobs);
        Assert.Equal("JOB-001", jobs[0].EmployerJobId);
    }

    [Fact]
    public async Task QueryAsync_ShouldFilterBySourceWebsite()
    {
        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        });

        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "Globex",
            EmployerJobId = "JOB-002",
            Description = "Data scientist",
            Pros = "Interesting work",
            Cons = "Lower pay",
            ResumeHints = "Partial match",
            Score = 60,
            Recommendation = "Maybe",
            SourceWebsite = "glassdoor.com"
        });

        var jobs = await _repository.QueryAsync("test@example.com", null, null, null, null, "indeed.com");

        Assert.Single(jobs);
        Assert.Equal("indeed.com", jobs[0].SourceWebsite);
    }

    [Fact]
    public async Task QueryAsync_ShouldExcludeDeletedJobsByDefault()
    {
        var job1 = new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        };

        await _repository.SaveAsync(job1);
        await _repository.SoftDeleteAsync(job1.InternalId);

        var jobs = await _repository.QueryAsync("test@example.com");

        Assert.Empty(jobs);
    }

    [Fact]
    public async Task QueryAsync_ShouldIncludeDeletedJobs_WhenExcludeDeletedIsFalse()
    {
        var job1 = new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        };

        await _repository.SaveAsync(job1);
        await _repository.SoftDeleteAsync(job1.InternalId);

        var jobs = await _context.Jobs.ToListAsync();

        Assert.Single(jobs);
        Assert.True(jobs[0].IsDeleted);
    }

    [Fact]
    public async Task QueryAsync_ShouldSortByPostedDateDescending()
    {
        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "Old Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com",
            PostedDate = new DateTime(2024, 1, 1)
        });

        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "New Corp",
            EmployerJobId = "JOB-002",
            Description = "Data scientist",
            Pros = "Interesting work",
            Cons = "Lower pay",
            ResumeHints = "Partial match",
            Score = 60,
            Recommendation = "Maybe",
            SourceWebsite = "indeed.com",
            PostedDate = new DateTime(2024, 12, 1)
        });

        var jobs = await _repository.QueryAsync("test@example.com");

        Assert.Equal("New Corp", jobs[0].Employer);
        Assert.Equal("Old Corp", jobs[1].Employer);
    }

    [Fact]
    public async Task QueryAsync_ShouldFilterByDateFrom()
    {
        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "Recent Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com",
            PostedDate = new DateTime(2024, 6, 1)
        });

        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "Old Corp",
            EmployerJobId = "JOB-002",
            Description = "Data scientist",
            Pros = "Interesting work",
            Cons = "Lower pay",
            ResumeHints = "Partial match",
            Score = 60,
            Recommendation = "Maybe",
            SourceWebsite = "indeed.com",
            PostedDate = new DateTime(2024, 1, 1)
        });

        var jobs = await _repository.QueryAsync("test@example.com", new DateTime(2024, 4, 1));

        Assert.Single(jobs);
        Assert.Equal("Recent Corp", jobs[0].Employer);
    }

    [Fact]
    public async Task QueryAsync_ShouldFilterByDateTo()
    {
        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "Old Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com",
            PostedDate = new DateTime(2024, 1, 1)
        });

        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "Recent Corp",
            EmployerJobId = "JOB-002",
            Description = "Data scientist",
            Pros = "Interesting work",
            Cons = "Lower pay",
            ResumeHints = "Partial match",
            Score = 60,
            Recommendation = "Maybe",
            SourceWebsite = "indeed.com",
            PostedDate = new DateTime(2024, 12, 1)
        });

        var jobs = await _repository.QueryAsync("test@example.com", null, new DateTime(2024, 6, 1));

        Assert.Single(jobs);
        Assert.Equal("Old Corp", jobs[0].Employer);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenJobExists()
    {
        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        });

        var exists = await _repository.ExistsAsync("test@example.com", "indeed.com", "JOB-001");

        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenJobDoesNotExist()
    {
        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        });

        var exists = await _repository.ExistsAsync("test@example.com", "indeed.com", "JOB-999");

        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenDifferentWebsite()
    {
        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        });

        var exists = await _repository.ExistsAsync("test@example.com", "glassdoor.com", "JOB-001");

        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenDifferentEmail()
    {
        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        });

        var exists = await _repository.ExistsAsync("other@example.com", "indeed.com", "JOB-001");

        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenJobIsDeleted()
    {
        var job = new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        };

        await _repository.SaveAsync(job);
        await _repository.SoftDeleteAsync(job.InternalId);

        var exists = await _repository.ExistsAsync("test@example.com", "indeed.com", "JOB-001");

        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsAsync_ShouldBeCaseInsensitiveForWebsite()
    {
        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        });

        var exists = await _repository.ExistsAsync("test@example.com", "Indeed.COM", "JOB-001");

        Assert.True(exists);
    }

    [Fact]
    public async Task SoftDeleteAsync_ShouldMarkJobAsDeleted()
    {
        var job = new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        };

        var jobId = await _repository.SaveAsync(job);
        var success = await _repository.SoftDeleteAsync(jobId);

        Assert.True(success);
        var saved = await _context.Jobs.FindAsync(jobId);
        Assert.NotNull(saved);
        Assert.True(saved.IsDeleted);
    }

    [Fact]
    public async Task SoftDeleteAsync_ShouldReturnFalse_WhenJobNotFound()
    {
        var success = await _repository.SoftDeleteAsync(Guid.NewGuid());

        Assert.False(success);
    }

    [Fact]
    public async Task GetDiscardedAsync_ShouldReturnDeletedJobs()
    {
        var job1 = new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        };

        var job2 = new JobEntity
        {
            Email = "test@example.com",
            Employer = "Globex",
            EmployerJobId = "JOB-002",
            Description = "Data scientist",
            Pros = "Interesting work",
            Cons = "Lower pay",
            ResumeHints = "Partial match",
            Score = 60,
            Recommendation = "Maybe",
            SourceWebsite = "indeed.com"
        };

        await _repository.SaveAsync(job1);
        await _repository.SaveAsync(job2);
        await _repository.SoftDeleteAsync(job1.InternalId);

        var discarded = await _repository.GetDiscardedAsync("test@example.com");

        Assert.Single(discarded);
        Assert.Equal("Acme Corp", discarded[0].Employer);
    }

    [Fact]
    public async Task GetDiscardedAsync_ShouldReturnEmpty_WhenNoDeletedJobs()
    {
        await _repository.SaveAsync(new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        });

        var discarded = await _repository.GetDiscardedAsync("test@example.com");

        Assert.Empty(discarded);
    }

    [Fact]
    public async Task SaveAsync_ShouldGenerateUniqueInternalId()
    {
        var job1 = new JobEntity
        {
            Email = "test@example.com",
            Employer = "Acme Corp",
            EmployerJobId = "JOB-001",
            Description = "Software engineer",
            Pros = "Good pay",
            Cons = "Long commute",
            ResumeHints = "Matches well",
            Score = 80,
            Recommendation = "Apply",
            SourceWebsite = "indeed.com"
        };

        var job2 = new JobEntity
        {
            Email = "test@example.com",
            Employer = "Globex",
            EmployerJobId = "JOB-002",
            Description = "Data scientist",
            Pros = "Interesting work",
            Cons = "Lower pay",
            ResumeHints = "Partial match",
            Score = 60,
            Recommendation = "Maybe",
            SourceWebsite = "indeed.com"
        };

        var id1 = await _repository.SaveAsync(job1);
        var id2 = await _repository.SaveAsync(job2);

        Assert.NotEqual(Guid.Empty, id1);
        Assert.NotEqual(Guid.Empty, id2);
        Assert.NotEqual(id1, id2);
    }
}
