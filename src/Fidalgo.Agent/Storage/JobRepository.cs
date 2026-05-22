using Microsoft.EntityFrameworkCore;
using Fidalgo.Agent.Storage;

namespace Fidalgo.Agent.Storage;

public class JobRepository
{
    private readonly JobDbContext _context;

    public JobRepository(JobDbContext context)
    {
        _context = context;
    }

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

    public virtual async Task<List<JobEntity>> GetDiscardedAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Jobs
            .Where(j => j.Email == email && j.IsDeleted)
            .OrderByDescending(j => j.PostedDate ?? DateTime.MinValue)
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<bool> ExistsAsync(string email, string sourceWebsite, string employerJobId, CancellationToken cancellationToken = default)
    {
        return await _context.Jobs
            .AnyAsync(j => j.Email == email 
                && j.SourceWebsite.Equals(sourceWebsite, StringComparison.OrdinalIgnoreCase) 
                && j.EmployerJobId == employerJobId 
                && !j.IsDeleted, cancellationToken);
    }
}
