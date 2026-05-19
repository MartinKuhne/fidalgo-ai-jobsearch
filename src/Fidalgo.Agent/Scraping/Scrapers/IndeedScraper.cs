using AngleSharp;
using AngleSharp.Dom;
using Fidalgo.Agent.Scraping;

namespace Fidalgo.Agent.Scraping.Scrapers;

/// <summary>
/// Scraper for indeed.com job postings.
/// </summary>
public class IndeedScraper : IWebsiteScraper
{
    private readonly HttpClient _httpClient;
    private readonly IBrowsingContext _context;

    /// <summary>
    /// Creates a new instance with the specified HTTP client.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    public IndeedScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _context = BrowsingContext.New(AngleSharp.Configuration.Default);
        WebsiteName = "indeed.com";
    }

    /// <inheritdoc />
    public string WebsiteName { get; }

    /// <inheritdoc />
    public async Task<List<JobPosting>> SearchAsync(string keyword, CancellationToken cancellationToken = default)
    {
        var searchUrl = $"https://www.indeed.com/jobs?q={Uri.EscapeDataString(keyword)}";
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

        // Indeed uses specific selectors for job listings
        var jobElements = doc.QuerySelectorAll(".jobsearch-Result, [data-jk], .slider_item, .job_seen_bordered");

        foreach (var element in jobElements.Take(20))
        {
            var title = element.QuerySelector(".jobTitle, a, h2")?.TextContent?.Trim() ?? string.Empty;
            var company = element.QuerySelector(".companyName, .company, .si-principal")?.TextContent?.Trim() ?? string.Empty;
            var link = element.QuerySelector("a")?.GetAttribute("href") ?? string.Empty;

            if (string.IsNullOrEmpty(title))
                continue;

            if (link.StartsWith("/"))
            {
                link = "https://www.indeed.com" + link;
            }

            jobs.Add(new JobPosting
            {
                SourceUrl = link,
                Title = title,
                Company = company,
                Description = string.Empty,
                SourceWebsite = WebsiteName
            });
        }

        return jobs;
    }
}
