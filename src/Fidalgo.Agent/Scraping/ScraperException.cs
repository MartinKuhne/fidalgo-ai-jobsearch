namespace Fidalgo.Agent.Scraping;

/// <summary>
/// Exception thrown when a website scraper encounters an error.
/// </summary>
public class ScraperException : Exception
{
    /// <summary>
    /// The name of the website that caused the error.
    /// </summary>
    public string WebsiteName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScraperException"/> class.
    /// </summary>
    /// <param name="websiteName">The name of the website.</param>
    /// <param name="message">The error message.</param>
    public ScraperException(string websiteName, string message)
        : base(message)
    {
        WebsiteName = websiteName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScraperException"/> class.
    /// </summary>
    /// <param name="websiteName">The name of the website.</param>
    /// <param name="message">The error message.</param>
    /// <param name="inner">The inner exception.</param>
    public ScraperException(string websiteName, string message, Exception? inner)
        : base(message, inner)
    {
        WebsiteName = websiteName;
    }
}
