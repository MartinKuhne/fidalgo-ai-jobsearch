using Microsoft.EntityFrameworkCore;

namespace Fidalgo.Agent.Storage;

/// <summary>
/// Detects duplicate job postings by checking existing database entries.
/// </summary>
public class DuplicateDetector
{
    private readonly JobDbContext _context;

    /// <summary>
    /// Creates a new instance of the DuplicateDetector.
    /// </summary>
    /// <param name="context">The database context.</param>
    public DuplicateDetector(JobDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets URLs that already exist in the database.
    /// </summary>
    /// <param name="urls">The URLs to check.</param>
    /// <param name="cancellationToken">Token for cancelling the operation.</param>
    /// <returns>Set of URLs that already exist in the database.</returns>
    public async Task<HashSet<string>> GetDuplicateUrlsAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default)
    {
        var urlList = urls.ToList();
        if (!urlList.Any())
            return new HashSet<string>();

        var existingUrls = await _context.JobPostings
            .Where(j => urlList.Contains(j.SourceUrl))
            .Select(j => j.SourceUrl)
            .ToListAsync(cancellationToken);

        return new HashSet<string>(existingUrls);
    }
}
