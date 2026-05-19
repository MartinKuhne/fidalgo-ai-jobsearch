using Fidalgo.Agent.Scraping;

namespace Fidalgo.Agent.Scraping;

/// <summary>
/// Interface for website scrapers.
/// </summary>
public interface IWebsiteScraper
{
    /// <summary>
    /// The name of the website this scraper targets.
    /// </summary>
    string WebsiteName { get; }

    /// <summary>
    /// Searches the website for job postings matching the given keyword.
    /// </summary>
    /// <param name="keyword">The search keyword.</param>
    /// <param name="cancellationToken">Token for cancelling the operation.</param>
    /// <returns>List of job postings found.</returns>
    Task<List<JobPosting>> SearchAsync(string keyword, CancellationToken cancellationToken = default);
}
