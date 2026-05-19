namespace Fidalgo.Agent.Configuration;

/// <summary>
/// Constant definitions for supported job search websites.
/// </summary>
public static class SupportedWebsites
{
    public const string GovernmentJobs = "governmentjobs.com";
    public const string Google = "google";
    public const string Glassdoor = "glassdoor.com";
    public const string Monster = "monster.com";
    public const string Indeed = "indeed.com";
    public const string LinkedIn = "linkedin.com";

    private static readonly HashSet<string> _supported = new(StringComparer.OrdinalIgnoreCase)
    {
        GovernmentJobs, Google, Glassdoor, Monster, Indeed, LinkedIn
    };

    /// <summary>
    /// Checks whether the given website name is supported.
    /// </summary>
    /// <param name="website">The website name to check.</param>
    /// <returns>True if the website is supported.</returns>
    public static bool IsSupported(string website)
    {
        return _supported.Contains(website);
    }

    /// <summary>
    /// Gets all supported website names.
    /// </summary>
    public static IEnumerable<string> All => _supported;
}
