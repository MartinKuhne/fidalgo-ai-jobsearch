using AngleSharp;
using AngleSharp.Dom;
using Fidalgo.Agent.Scraping;

namespace Fidalgo.Agent.Scraping.Scrapers;

/// <summary>
/// Scraper for governmentjobs.com job postings.
/// </summary>
public class GovernmentJobsScraper : IWebsiteScraper
{
    private readonly HttpClient _httpClient;
    private readonly IBrowsingContext _context;

    /// <summary>
    /// Creates a new instance with the specified HTTP client.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    public GovernmentJobsScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _context = BrowsingContext.New(AngleSharp.Configuration.Default);
        WebsiteName = "governmentjobs.com";
    }

    /// <inheritdoc />
    public string WebsiteName { get; }

    /// <inheritdoc />
    public async Task<List<JobPosting>> SearchAsync(string keyword, CancellationToken cancellationToken = default)
    {
        var searchUrl = $"https://www.governmentjobs.com/jobsearch/results?q={Uri.EscapeDataString(keyword)}";
        var html = await FetchHtmlAsync(searchUrl, cancellationToken);

        if (string.IsNullOrEmpty(html))
            return new List<JobPosting>();

        return ParseJobPostings(html);
    }

    private async Task<string> FetchHtmlAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new ScraperException(WebsiteName, $"Failed to fetch {url}: {ex.Message}", ex);
        }
    }

    private List<JobPosting> ParseJobPostings(string html)
    {
        var doc = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);

        var jobs = new List<JobPosting>();

        // GovernmentJobs uses a list-based structure for job results
        var jobElements = doc.QuerySelectorAll(".jobListing, .job-listing, .job-result");

        foreach (var element in jobElements.Take(20))
        {
            var title = element.QuerySelector("a, .job-title, .title")?.TextContent?.Trim() ?? string.Empty;
            var company = element.QuerySelector(".company, .employer, .org")?.TextContent?.Trim() ?? string.Empty;
            var link = element.QuerySelector("a")?.GetAttribute("href") ?? string.Empty;

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(company))
                continue;

            if (!link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var baseUrl = "https://www.governmentjobs.com" + link;
                link = baseUrl;
            }

            jobs.Add(new JobPosting
            {
                SourceUrl = link,
                Title = title,
                Company = company,
                Description = element.QuerySelector(".description, .summary, p")?.TextContent?.Trim() ?? string.Empty,
                SourceWebsite = WebsiteName
            });
        }

        return jobs;
    }
}
