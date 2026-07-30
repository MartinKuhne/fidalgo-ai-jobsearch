using Microsoft.Extensions.Configuration;

namespace Fidalgo.Shared.UserProfile;

/// <summary>
/// Helpers for the user's persistent configuration file at
/// <c>%APPDATA%\.config\Fidalgo\config.json</c>. The file holds the user's
/// email and a list of job search keywords so the same data does not have
/// to be passed on every command-line invocation. CLI flags override any
/// value loaded from the file.
/// </summary>
public static class UserProfileConfig
{
    /// <summary>File name of the user configuration document.</summary>
    public const string ConfigFileName = "config.json";

    /// <summary>Returns the absolute path of the user configuration file in the default profile directory.</summary>
    public static string GetDefaultConfigPath() =>
        Path.Combine(UserProfilePaths.GetDefaultBaseDir(), ConfigFileName);

    /// <summary>
    /// Adds the user configuration file as an optional, reload-on-change
    /// JSON source on the supplied configuration builder. Missing file is
    /// not an error. Command-line arguments added later will still win.
    /// </summary>
    public static IConfigurationBuilder AddUserProfileConfig(this IConfigurationBuilder builder)
    {
        return builder.AddJsonFile(GetDefaultConfigPath(), optional: true, reloadOnChange: true);
    }

    /// <summary>
    /// Resolves the effective search-keywords string. The command-line
    /// value wins when present. Otherwise the configured
    /// <paramref name="jobSearchKeywords"/> list (if any) is joined with
    /// spaces into a single string suitable for the existing search path.
    /// </summary>
    /// <param name="keywords">The --Keywords CLI value, or null when the user did not pass the flag.</param>
    /// <param name="jobSearchKeywords">The list loaded from the user config file, or null when absent.</param>
    /// <returns>The effective keywords string, or null when neither source provided a value.</returns>
    public static string? ResolveKeywords(string? keywords, string[]? jobSearchKeywords)
    {
        if (!string.IsNullOrWhiteSpace(keywords))
        {
            return keywords;
        }

        if (jobSearchKeywords is { Length: > 0 })
        {
            return string.Join(" ", jobSearchKeywords);
        }

        return null;
    }
}
