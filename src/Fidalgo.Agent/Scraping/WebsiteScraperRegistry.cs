using Fidalgo.Agent.Configuration;

namespace Fidalgo.Agent.Scraping;

/// <summary>
/// Registry for website scrapers, providing lookup by website name.
/// </summary>
public class WebsiteScraperRegistry
{
    private readonly Dictionary<string, IWebsiteScraper> _scrapers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    /// <summary>
    /// Registers a scraper for a specific website.
    /// </summary>
    /// <param name="websiteName">The website name to register for.</param>
    /// <param name="scraper">The scraper implementation.</param>
    /// <exception cref="InvalidOperationException">Thrown when the website is already registered.</exception>
    public void Register(string websiteName, IWebsiteScraper scraper)
    {
        lock (_lock)
        {
            if (_scrapers.ContainsKey(websiteName))
            {
                throw new InvalidOperationException($"Scraper already registered for website: {websiteName}");
            }
            _scrapers[websiteName] = scraper;
        }
    }

    /// <summary>
    /// Gets a scraper by website name.
    /// </summary>
    /// <param name="websiteName">The website name.</param>
    /// <returns>The scraper, or null if not found.</returns>
    public IWebsiteScraper? Get(string websiteName)
    {
        lock (_lock)
        {
            _scrapers.TryGetValue(websiteName, out var scraper);
            return scraper;
        }
    }

    /// <summary>
    /// Gets all registered scrapers.
    /// </summary>
    public IEnumerable<IWebsiteScraper> GetAll()
    {
        lock (_lock)
        {
            return _scrapers.Values.ToList();
        }
    }

    /// <summary>
    /// Registers all built-in scrapers.
    /// </summary>
    public void RegisterAll(Func<IWebsiteScraper> createGovernmentJobs, Func<IWebsiteScraper> createGoogle,
        Func<IWebsiteScraper> createGlassdoor, Func<IWebsiteScraper> createMonster,
        Func<IWebsiteScraper> createIndeed, Func<IWebsiteScraper> createLinkedIn)
    {
        Register("governmentjobs.com", createGovernmentJobs());
        Register("google", createGoogle());
        Register("glassdoor.com", createGlassdoor());
        Register("monster.com", createMonster());
        Register("indeed.com", createIndeed());
        Register("linkedin.com", createLinkedIn());
    }
}
