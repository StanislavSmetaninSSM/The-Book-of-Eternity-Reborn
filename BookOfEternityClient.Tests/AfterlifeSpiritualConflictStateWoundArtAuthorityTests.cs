using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeSpiritualConflictStateWoundArtAuthorityTests
{
    public static IEnumerable<object[]> InvalidRequiredTiers()
    {
        foreach (var artId in AfterlifeSpiritualConflictState.RequiredWoundArtIds)
        foreach (var rawValue in new[] { "-1", "6", "true", "1.5" })
            yield return [artId, rawValue];
    }

    [Theory]
    [MemberData(nameof(InvalidRequiredTiers))]
    public void CurrentAuthorityRejectsOutOfRangeBooleanAndFractionalRequiredTiers(
        string artId,
        string rawValue)
    {
        var soul = BuildSoulWithDefaultProfile();
        soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]![artId] =
            JsonNode.Parse(rawValue);

        var valid = AfterlifeSpiritualConflictState.TryValidateCurrentRequiredWoundArtAuthority(
            soul,
            out var damage);

        Assert.False(valid);
        Assert.Contains($"artTiers.{artId}", damage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("spiritual_resilience", 0)]
    [InlineData("spiritual_resilience", 5)]
    [InlineData("spiritual_healing", 0)]
    [InlineData("spiritual_healing", 5)]
    public void CurrentAuthorityAcceptsRequiredTierBounds(string artId, int tier)
    {
        var soul = BuildSoulWithDefaultProfile();
        soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]![artId] = tier;

        var valid = AfterlifeSpiritualConflictState.TryValidateCurrentRequiredWoundArtAuthority(
            soul,
            out var damage);

        Assert.True(valid, damage);
    }

    [Fact]
    public void CurrentAuthorityAllowsGenuinelyAbsentWholeProfile()
    {
        var valid = AfterlifeSpiritualConflictState.TryValidateCurrentRequiredWoundArtAuthority(
            new JsonObject(),
            out var damage);

        Assert.True(valid, damage);
    }

    [Fact]
    public void CurrentAuthorityDoesNotRequireOptionalLegacyArtKeys()
    {
        var soul = BuildSoulWithDefaultProfile();
        var artTiers = Assert.IsType<JsonObject>(
            soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]);
        Assert.Equal(AfterlifeSpiritualConflictState.RequiredWoundArtIds.Count, artTiers.Count);

        var valid = AfterlifeSpiritualConflictState.TryValidateCurrentRequiredWoundArtAuthority(
            soul,
            out var damage);

        Assert.True(valid, damage);
    }

    private static JsonObject BuildSoulWithDefaultProfile() =>
        new()
        {
            [AfterlifeSpiritualConflictState.SoulStateProfileProperty] =
                AfterlifeSpiritualConflictState.CreateDefaultCombatProfile()
        };
}
