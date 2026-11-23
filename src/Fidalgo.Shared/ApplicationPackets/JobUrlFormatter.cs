using Fidalgo.Shared.Storage;

namespace Fidalgo.Shared.ApplicationPackets;

/// <summary>
/// Shared helper for formatting external source URLs from <see cref="JobEntity"/> metadata.
/// </summary>
public static class JobUrlFormatter
{
    /// <summary>
    /// Computes the clickable source URL for <paramref name="job"/>, or an empty string if unavailable.
    /// </summary>
    public static string FormatUrl(JobEntity job)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (string.Equals(job.SourceWebsite, "adzuna", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(job.EmployerJobId))
        {
            return $"https://www.adzuna.com/details/{job.EmployerJobId}";
        }

        if (!string.IsNullOrWhiteSpace(job.SourceWebsite) &&
            (job.SourceWebsite.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             job.SourceWebsite.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            return job.SourceWebsite;
        }

        return string.Empty;
    }
}
