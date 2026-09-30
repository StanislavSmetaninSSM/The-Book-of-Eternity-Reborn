using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Keeps archived special-art payload checks on the production art contract without file authority.
/// </summary>
public sealed class SpiritualWoundRetainedArtTests
{
    /// <summary>
    /// Validates a retained original art without mutating it or requiring current profile files.
    /// </summary>
    [Fact]
    public void Validate_AcceptsOriginalArtWithoutMutatingPayload()
    {
        var art = Art();
        var before = art.ToJsonString();
        Assert.Empty(Validate(art));
        Assert.Equal(before, art.ToJsonString());
    }

    /// <summary>
    /// Preserves original teachable arts without applying the stricter current-authoring combat-effect rule.
    /// </summary>
    [Fact]
    public void Validate_PreservesOriginalTeachableArtPolicy()
    {
        var art = Art();
        art["canTeachPlayer"] = true;
        art["trainingConditions"] = new JsonArray("Согласие хранителя");
        Assert.Empty(Validate(art));
    }

    /// <summary>
    /// Reuses owner, operation, tier, cost, training and wound-envelope validation.
    /// </summary>
    /// <param name="mutation">
    /// Invalid original-art property under the existing production contract.
    /// </param>
    [Theory]
    [InlineData("owner")]
    [InlineData("operation")]
    [InlineData("tier")]
    [InlineData("cost")]
    [InlineData("upgrade")]
    [InlineData("training")]
    [InlineData("envelope")]
    public void Validate_RejectsOriginalArtContractViolation(string mutation)
    {
        var art = Art();
        switch (mutation)
        {
            case "owner": art["ownerActorId"] = "foreign-owner"; break;
            case "operation": art["baseOperation"] = "unknown-operation"; break;
            case "tier": art["tier"] = 6; break;
            case "cost": art["costMultiplierPercent"] = 100; break;
            case "upgrade": art["upgradeCost"] = new JsonObject(); break;
            case "training": art["canTeachPlayer"] = true; break;
            case "envelope": art["spiritualWoundEnvelope"] = null; break;
        }
        Assert.Contains(Validate(art), issue => issue.Severity == IssueSeverity.Error);
    }

    /// <summary>
    /// Builds comparison data using the original special-art profile schema.
    /// </summary>
    /// <returns>
    /// Detached art with no source admission authority.
    /// </returns>
    private static JsonObject Art() => new()
    {
        ["artId"] = "art-retained", ["displayName"] = "Нить надлома",
        ["effectSummary"] = "Духовное давление.", ["ownerActorType"] = "guardian",
        ["ownerActorId"] = "guardian-a", ["baseOperation"] = "pressure", ["tier"] = 2,
        ["costMultiplierPercent"] = 200, ["upgradeCost"] = new JsonObject { ["inkFeathers"] = 1 },
        ["canTeachPlayer"] = false, ["trainingConditions"] = new JsonArray()
    };

    /// <summary>
    /// Calls the internal retained-payload seam without constructing a validator or filesystem.
    /// </summary>
    /// <param name="art">
    /// Original art payload under test.
    /// </param>
    /// <returns>
    /// Owning production contract diagnostics.
    /// </returns>
    private static IReadOnlyList<ValidationIssue> Validate(JsonObject art)
    {
        var method = typeof(ValidationService).GetMethod("ValidateRetainedSpiritualSpecialArt",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (IReadOnlyList<ValidationIssue>)method.Invoke(null,
            new object[] { art, "guardian", "guardian-a", "chaos_sea" })!;
    }
}
