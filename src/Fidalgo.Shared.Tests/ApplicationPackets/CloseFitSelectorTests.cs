using Fidalgo.Shared.ApplicationPackets;
using Fidalgo.Shared.Storage;

namespace Fidalgo.Shared.Tests.ApplicationPackets;

public class CloseFitSelectorTests
{
    [Theory]
    [InlineData(null, null, new[] { "Apply" })]
    [InlineData("", "Apply, Maybe", new[] { "Apply", "Maybe" })]
    [InlineData("Apply, Maybe", "Do not apply", new[] { "Apply", "Maybe" })]
    public void ParseTiers_ResolvesExpectedTiers(string? primary, string? fallback, string[] expected)
    {
        var tiers = CloseFitSelector.ParseTiers(primary, fallback);

        Assert.Equal(expected.Length, tiers.Count);
        foreach (var item in expected)
        {
            Assert.Contains(item, tiers);
        }
    }

    [Theory]
    [InlineData("Apply", 85, false, 70, true)]
    [InlineData("apply", 70, false, 70, true)]
    [InlineData("Apply", 69, false, 70, false)]
    [InlineData("Maybe", 95, false, 70, false)]
    [InlineData("Apply", 95, true, 70, false)]
    public void IsCloseFit_AppliesTierScoreAndDeleteFilters(
        string recommendation,
        int score,
        bool isDeleted,
        int minScore,
        bool expected)
    {
        var job = new JobEntity
        {
            Recommendation = recommendation,
            Score = score,
            IsDeleted = isDeleted
        };
        var tiers = CloseFitSelector.ParseTiers("Apply");

        Assert.Equal(expected, CloseFitSelector.IsCloseFit(job, tiers, minScore));
    }
}
