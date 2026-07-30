using Fidalgo.Shared.UserProfile;
using Xunit;

namespace Fidalgo.Shared.Tests.UserProfile;

public class UserProfileConfigTests
{
    [Fact]
    public void GetDefaultConfigPath_EndsWithConfigJson()
    {
        var path = UserProfileConfig.GetDefaultConfigPath();

        Assert.EndsWith(Path.Combine(".config", "Fidalgo", "config.json"), path);
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
