using Fidalgo.Agent.Scraping;
using Microsoft.EntityFrameworkCore;

namespace Fidalgo.Agent.Storage;

/// <summary>
/// Service for querying job postings.
/// </summary>
public class JobQueryService
{
    private readonly JobDbContext _context;

    /// <summary>
    /// Creates a new instance of the JobQueryService.
    /// </summary>
    /// <param name="context">The database context.</param>
    public JobQueryService(JobDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets all non-discarded job postings.
    /// </summary>
    /// <param name="cancellationToken">Token for cancelling the operation.</param>
    /// <returns>List of all job postings.</returns>
    public async Task<List<JobPosting>> GetAllJobsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.JobPostings
            .Where(j => !j.IsDiscarded)
            .OrderByDescending(j => j.ScrapedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets job postings within a date range.
    /// </summary>
    /// <param name="startDate">The start date (inclusive).</param>
    /// <param name="endDate">The end date (inclusive).</param>
    /// <param name="cancellationToken">Token for cancelling the operation.</param>
    /// <returns>List of job postings in the date range.</returns>
    public async Task<List<JobPosting>> GetJobsByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _context.JobPostings
            .Where(j => j.ScrapedAt >= startDate && j.ScrapedAt <= endDate && !j.IsDiscarded)
            .OrderByDescending(j => j.ScrapedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets job postings filtered by website.
    /// </summary>
    /// <param name="website">The website name.</param>
    /// <param name="cancellationToken">Token for cancelling the operation.</param>
    /// <returns>List of job postings from the specified website.</returns>
    public async Task<List<JobPosting>> GetJobsByWebsiteAsync(string website, CancellationToken cancellationToken = default)
    {
        return await _context.JobPostings
            .Where(j => j.SourceWebsite == website && !j.IsDiscarded)
            .OrderByDescending(j => j.ScrapedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets job postings that matched a specific keyword.
    /// </summary>
    /// <param name="keyword">The keyword to search for.</param>
    /// <param name="cancellationToken">Token for cancelling the operation.</param>
    /// <returns>List of job postings that matched the keyword.</returns>
    public async Task<List<JobPosting>> GetJobsByKeywordAsync(string keyword, CancellationToken cancellationToken = default)
    {
        return await _context.JobPostings
            .Where(j => j.MatchedKeywords.Contains(keyword) && !j.IsDiscarded)
            .OrderByDescending(j => j.ScrapedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets jobs scraped since a specific date.
    /// </summary>
    /// <param name="since">The cutoff date.</param>
    /// <param name="cancellationToken">Token for cancelling the operation.</param>
    /// <returns>List of new jobs since the specified date.</returns>
    public async Task<List<JobPosting>> GetNewJobsSinceAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        return await _context.JobPostings
            .Where(j => j.ScrapedAt >= since && !j.IsDiscarded)
            .OrderByDescending(j => j.ScrapedAt)
            .ToListAsync(cancellationToken);
    }
}
