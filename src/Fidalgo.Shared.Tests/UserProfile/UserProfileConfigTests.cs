using Fidalgo.Shared.UserProfile;
using Xunit;

namespace Fidalgo.Shared.Tests.UserProfile;

public class UserProfileConfigTests
{
    [Fact]
    public void GetDefaultConfigPath_EndsWithConfigJson()
    {
        var prevPath = Environment.GetEnvironmentVariable("FIDALGO_PROFILE_CONFIG_PATH");
        var prevDir = Environment.GetEnvironmentVariable("FIDALGO_CONFIG_DIR");
        Environment.SetEnvironmentVariable("FIDALGO_PROFILE_CONFIG_PATH", null);
        Environment.SetEnvironmentVariable("FIDALGO_CONFIG_DIR", null);
        try
        {
            var path = UserProfileConfig.GetDefaultConfigPath();

            Assert.EndsWith(Path.Combine(".config", "Fidalgo", "config.json"), path);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FIDALGO_PROFILE_CONFIG_PATH", prevPath);
            Environment.SetEnvironmentVariable("FIDALGO_CONFIG_DIR", prevDir);
        }
    }

    [Fact]
    public void GetDefaultConfigPath_HonorsEnvVarOverrides()
    {
        var prevPath = Environment.GetEnvironmentVariable("FIDALGO_PROFILE_CONFIG_PATH");
        var prevDir = Environment.GetEnvironmentVariable("FIDALGO_CONFIG_DIR");
        try
        {
            Environment.SetEnvironmentVariable("FIDALGO_PROFILE_CONFIG_PATH", null);
            Environment.SetEnvironmentVariable("FIDALGO_CONFIG_DIR", "/data/config");
            Assert.Equal(Path.Combine("/data/config", "config.json"), UserProfileConfig.GetDefaultConfigPath());

            Environment.SetEnvironmentVariable("FIDALGO_PROFILE_CONFIG_PATH", "/custom/my-config.json");
            Assert.Equal("/custom/my-config.json", UserProfileConfig.GetDefaultConfigPath());
        }
        finally
        {
            Environment.SetEnvironmentVariable("FIDALGO_PROFILE_CONFIG_PATH", prevPath);
            Environment.SetEnvironmentVariable("FIDALGO_CONFIG_DIR", prevDir);
        }
    }

    [Fact]
    public void ResolveKeywords_ReturnsCliValue_WhenCliProvided()
    {
        var result = UserProfileConfig.ResolveKeywords("from-cli", new[] { "from-config" });

        Assert.Equal("from-cli", result);
    }

    [Fact]
    public void ResolveKeywords_IgnoresConfig_WhenCliProvided()
    {
        var result = UserProfileConfig.ResolveKeywords("only-this", new[] { "ignored-1", "ignored-2" });

        Assert.Equal("only-this", result);
    }

    [Fact]
    public void ResolveKeywords_JoinsConfigList_WhenCliMissing()
    {
        var result = UserProfileConfig.ResolveKeywords(null, new[] { "software architect", "principal engineer" });

        Assert.Equal("software architect principal engineer", result);
    }

    [Fact]
    public void ResolveKeywords_ReturnsNull_WhenBothEmpty()
    {
        var result = UserProfileConfig.ResolveKeywords(null, null);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveKeywords_ReturnsNull_WhenCliEmptyAndConfigEmpty()
    {
        var result = UserProfileConfig.ResolveKeywords(string.Empty, Array.Empty<string>());

        Assert.Null(result);
    }

    [Fact]
    public void ResolveKeywords_TreatsWhitespaceCliAsMissing()
    {
        var result = UserProfileConfig.ResolveKeywords("   ", new[] { "a", "b" });

        Assert.Equal("a b", result);
    }

    [Fact]
    public void ResolveKeywords_HandlesSingleItemConfigList()
    {
        var result = UserProfileConfig.ResolveKeywords(null, new[] { "only" });

        Assert.Equal("only", result);
    }
}
