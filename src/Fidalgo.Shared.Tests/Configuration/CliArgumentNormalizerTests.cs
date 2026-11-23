using Fidalgo.Shared.Configuration;
using Microsoft.Extensions.Configuration;

namespace Fidalgo.Shared.Tests.Configuration;

public class CliArgumentNormalizerTests
{
    private static readonly HashSet<string> BooleanFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "--GeneratePackets",
        "--ForceRegenerate",
        "--SkipExisting",
        "--QueryJobs"
    };

    [Fact]
    public void NormalizeBooleanFlags_RewritesBareFlagFollowedByAnotherFlag_AndBindsCleanly()
    {
        var args = new[] { "--GeneratePackets", "--OutputDir", @"C:\packets" };

        var normalized = CliArgumentNormalizer.NormalizeBooleanFlags(args, BooleanFlags);

        Assert.Equal(new[] { "--GeneratePackets=true", "--OutputDir", @"C:\packets" }, normalized);

        var config = new ConfigurationBuilder()
            .AddCommandLine(normalized)
            .Build();

        Assert.True(bool.Parse(config["GeneratePackets"]!));
        Assert.Equal(@"C:\packets", config["OutputDir"]);
    }

    [Fact]
    public void NormalizeBooleanFlags_RewritesBareFlagAtEndOfArgs()
    {
        var args = new[] { "--Email", "user@example.com", "--ForceRegenerate" };

        var normalized = CliArgumentNormalizer.NormalizeBooleanFlags(args, BooleanFlags);

        Assert.Equal(new[] { "--Email", "user@example.com", "--ForceRegenerate=true" }, normalized);
    }

    [Fact]
    public void NormalizeBooleanFlags_PreservesExplicitTrueAndFalseValues()
    {
        var args = new[] { "--QueryJobs", "true", "--SkipExisting", "false", "--ForceRegenerate=false" };

        var normalized = CliArgumentNormalizer.NormalizeBooleanFlags(args, BooleanFlags);

        Assert.Equal(args, normalized);

        var config = new ConfigurationBuilder()
            .AddCommandLine(normalized)
            .Build();

        Assert.True(bool.Parse(config["QueryJobs"]!));
        Assert.False(bool.Parse(config["SkipExisting"]!));
        Assert.False(bool.Parse(config["ForceRegenerate"]!));
    }

    [Fact]
    public void NormalizeBooleanFlags_MatchesFlagsCaseInsensitively()
    {
        var args = new[] { "--generatepackets", "--TargetJobId", "1234" };

        var normalized = CliArgumentNormalizer.NormalizeBooleanFlags(args, BooleanFlags);

        Assert.Equal(new[] { "--generatepackets=true", "--TargetJobId", "1234" }, normalized);
    }

    [Fact]
    public void NormalizeBooleanFlags_HandlesEmptyArgs()
    {
        var normalized = CliArgumentNormalizer.NormalizeBooleanFlags([], BooleanFlags);

        Assert.Empty(normalized);
    }
}
