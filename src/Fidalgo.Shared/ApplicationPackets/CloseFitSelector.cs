using Fidalgo.Shared.Storage;

namespace Fidalgo.Shared.ApplicationPackets;

/// <summary>
/// Pure helper for parsing recommendation tiers and identifying close-fit jobs eligible
/// for automatic application packet generation.
/// </summary>
public static class CloseFitSelector
{
    /// <summary>
    /// Parses a comma-separated recommendation filter, falling back to <paramref name="fallback"/>
    /// and ultimately <c>"Apply"</c> when empty.
    /// </summary>
    public static HashSet<string> ParseTiers(string? primary, string? fallback = null)
    {
        var raw = !string.IsNullOrWhiteSpace(primary) ? primary : fallback;
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = "Apply";
        }

        var tiers = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (tiers.Count == 0)
        {
            tiers.Add("Apply");
        }

        return tiers;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="job"/> is active, matches <paramref name="allowedTiers"/>,
    /// and meets or exceeds <paramref name="minScore"/>.
    /// </summary>
    public static bool IsCloseFit(JobEntity job, IReadOnlySet<string> allowedTiers, int minScore)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(allowedTiers);

        if (job.IsDeleted)
        {
            return false;
        }

        var recommendation = job.Recommendation?.Trim() ?? string.Empty;
        return allowedTiers.Contains(recommendation) && job.Score >= minScore;
    }
}
