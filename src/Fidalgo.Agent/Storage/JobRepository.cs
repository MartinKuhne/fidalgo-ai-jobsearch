using Fidalgo.Agent.Scraping;
using Microsoft.EntityFrameworkCore;

namespace Fidalgo.Agent.Storage;

/// <summary>
/// Repository for job posting persistence and retrieval.
/// </summary>
public class JobRepository
{
    private readonly JobDbContext _context;

    /// <summary>
    /// Creates a new instance of the JobRepository.
    /// </summary>
    /// <param name="context">The database context.</param>
    public JobRepository(JobDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Adds a single job posting, skipping duplicates.
    /// </summary>
    /// <param name="job">The job posting to add.</param>
    /// <returns>True if the job was added, false if it was a duplicate.</returns>
    public async Task<bool> AddJobAsync(JobPosting job)
    {
        var exists = await _context.JobPostings.AnyAsync(j => j.SourceUrl == job.SourceUrl);
        if (exists)
            return false;

        await _context.JobPostings.AddAsync(job);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Adds multiple job postings, skipping duplicates.
    /// </summary>
    /// <param name="jobs">The job postings to add.</param>
    /// <returns>The number of jobs actually added.</returns>
    public async Task<int> AddJobsAsync(IEnumerable<JobPosting> jobs)
    {
        var newJobs = new List<JobPosting>();
        var existingUrls = await _context.JobPostings.Select(j => j.SourceUrl).ToListAsync();

        foreach (var job in jobs)
        {
            if (!existingUrls.Contains(job.SourceUrl))
            {
                newJobs.Add(job);
            }
        }

        if (newJobs.Any())
        {
            await _context.JobPostings.AddRangeAsync(newJobs);
            await _context.SaveChangesAsync();
        }

        return newJobs.Count;
    }

    /// <summary>
    /// Gets all job postings within a date range.
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
    /// Gets job postings filtered by keyword match.
    /// </summary>
    /// <param name="keyword">The keyword to search for in matched keywords JSON.</param>
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
    /// Adds or updates job postings.
    /// </summary>
    /// <param name="jobs">The job postings to upsert.</param>
    /// <returns>The number of jobs added.</returns>
    public async Task<int> UpsertJobsAsync(IEnumerable<JobPosting> jobs)
    {
        var added = 0;
        foreach (var job in jobs)
        {
            if (await AddJobAsync(job))
                added++;
        }
        return added;
    }
}
