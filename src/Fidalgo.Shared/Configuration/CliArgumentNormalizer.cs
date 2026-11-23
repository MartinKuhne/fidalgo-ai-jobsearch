namespace Fidalgo.Shared.Configuration;

/// <summary>
/// Normalizes bare boolean CLI flags (such as <c>--GeneratePackets</c>) into
/// <c>--Flag=true</c> before passing arguments to Microsoft's command-line configuration provider.
/// </summary>
public static class CliArgumentNormalizer
{
    /// <summary>
    /// Rewrites any argument matching <paramref name="booleanFlags"/> (case-insensitively) that is not
    /// immediately followed by a boolean literal (<c>true</c> or <c>false</c>) into <c>--Flag=true</c>.
    /// </summary>
    /// <param name="args">Original command-line arguments.</param>
    /// <param name="booleanFlags">Known boolean switch names including their leading prefix (e.g. <c>--GeneratePackets</c>).</param>
    /// <returns>A normalized argument array safe for <c>AddCommandLine</c> binding.</returns>
    public static string[] NormalizeBooleanFlags(string[] args, IReadOnlySet<string> booleanFlags)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(booleanFlags);

        if (args.Length == 0 || booleanFlags.Count == 0)
        {
            return (string[])args.Clone();
        }

        var flagLookup = new HashSet<string>(booleanFlags, StringComparer.OrdinalIgnoreCase);
        var normalized = new List<string>(args.Length);

        for (var i = 0; i < args.Length; i++)
        {
            var current = args[i];
            if (flagLookup.Contains(current))
            {
                if (i + 1 < args.Length && bool.TryParse(args[i + 1], out _))
                {
                    normalized.Add(current);
                    normalized.Add(args[i + 1]);
                    i++;
                }
                else
                {
                    normalized.Add($"{current}=true");
                }
            }
            else
            {
                normalized.Add(current);
            }
        }

        return normalized.ToArray();
    }
}
