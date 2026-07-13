using Microsoft.EntityFrameworkCore;

namespace Fidalgo.Shared.Storage;

/// <summary>
/// Repository pattern over JobDbContext providing CRUD and query operations for job entities.
/// Handles deduplication by email and employer job ID, soft deletes, and multi-field filtering.
/// Virtual methods enable mocking in unit tests.
/// </summary>
public class JobRepository
{
    private readonly JobDbContext _context;

    /// <summary>Initializes a new instance of the JobRepository.</summary>
    /// <param name="context">The EF Core DbContext for database access.</param>
    public JobRepository(JobDbContext context)
    {
        _context = context;
    }

    /// <summary>Saves a job entity, returning existing ID if a duplicate is found.</summary>
    /// <param name="job">The job entity to save.</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>The internal ID of the saved or existing job.</returns>
    public virtual async Task<Guid> SaveAsync(JobEntity job, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Jobs
            .FirstOrDefaultAsync(j => j.Email == job.Email && j.EmployerJobId == job.EmployerJobId, cancellationToken);
        
        if (existing != null)
        {
            return existing.InternalId;
        }

        job.InternalId = Guid.NewGuid();
        await _context.Jobs.AddAsync(job, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return job.InternalId;
    }

    /// <summary>Updates an existing job entity.</summary>
    /// <param name="job">The job entity with updated values.</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    public virtual async Task UpdateAsync(JobEntity job, CancellationToken cancellationToken = default)
    {
        _context.Jobs.Update(job);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Queries jobs by email with optional filters for date range, employer, and source.</summary>
    /// <param name="email">Tenant email to filter by (required).</param>
    /// <param name="dateFrom">Optional start date filter.</param>
    /// <param name="dateTo">Optional end date filter.</param>
    /// <param name="employer">Optional employer name substring filter.</param>
    /// <param name="employerJobId">Optional exact employer job ID filter.</param>
    /// <param name="sourceWebsite">Optional source website substring filter.</param>
    /// <param name="excludeDeleted">Whether to exclude soft-deleted jobs (default: true).</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>List of matching job entities ordered by posted date descending.</returns>
    public virtual async Task<List<JobEntity>> QueryAsync(
        string email,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        string? employer = null,
        string? employerJobId = null,
        string? sourceWebsite = null,
        bool excludeDeleted = true,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Jobs.Where(j => j.Email == email);

        if (dateFrom.HasValue)
        {
            query = query.Where(j => j.PostedDate >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(j => j.PostedDate <= dateTo.Value);
        }

        if (!string.IsNullOrEmpty(employer))
        {
            query = query.Where(j => j.Employer.Contains(employer));
        }

        if (!string.IsNullOrEmpty(employerJobId))
        {
            query = query.Where(j => j.EmployerJobId == employerJobId);
        }

        if (!string.IsNullOrEmpty(sourceWebsite))
        {
            query = query.Where(j => j.SourceWebsite.Contains(sourceWebsite));
        }

        if (excludeDeleted)
        {
            query = query.Where(j => !j.IsDeleted);
        }

        return await query.OrderByDescending(j => j.PostedDate ?? DateTime.MinValue).ToListAsync(cancellationToken);
    }

    /// <summary>Soft-deletes a job by setting its IsDeleted flag.</summary>
    /// <param name="internalId">The job's internal database ID.</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>True if the job was found and deleted; false if not found.</returns>
    public virtual async Task<bool> SoftDeleteAsync(Guid internalId, CancellationToken cancellationToken = default)
    {
        var job = await _context.Jobs.FindAsync(internalId);
        if (job == null)
        {
            return false;
        }

        job.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Returns all soft-deleted jobs for a given email.</summary>
    /// <param name="email">Tenant email to filter by.</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>List of discarded job entities ordered by posted date descending.</returns>
    public virtual async Task<List<JobEntity>> GetDiscardedAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Jobs
            .Where(j => j.Email == email && j.IsDeleted)
            .OrderByDescending(j => j.PostedDate ?? DateTime.MinValue)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Checks if a job with the given employer ID exists for the tenant.</summary>
    /// <param name="email">Tenant email.</param>
    /// <param name="sourceWebsite">Source website URL.</param>
    /// <param name="employerJobId">Employer's job identifier.</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>True if a non-deleted matching job exists.</returns>
    public virtual async Task<bool> ExistsAsync(string email, string sourceWebsite, string employerJobId, CancellationToken cancellationToken = default)
    {
        return await _context.Jobs
            .AnyAsync(j => j.Email == email 
                && j.SourceWebsite.ToLower() == sourceWebsite.ToLower() 
                && j.EmployerJobId == employerJobId 
                && !j.IsDeleted, cancellationToken);
    }

    public virtual async Task<JobEntity?> GetByIdAsync(Guid internalId, CancellationToken cancellationToken = default)
    {
        return await _context.Jobs.FirstOrDefaultAsync(j => j.InternalId == internalId, cancellationToken);
    }

    /// <summary>Returns jobs that have not been triaged (Score == 0).</summary>
    /// <param name="email">Tenant email to filter by.</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>List of untriaged job entities.</returns>
    public virtual async Task<List<JobEntity>> GetUntriagedAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Jobs
            .Where(j => j.Email == email && j.Score == 0 && !j.IsDeleted)
            .OrderByDescending(j => j.PostedDate ?? DateTime.MinValue)
            .ToListAsync(cancellationToken);
    }
}