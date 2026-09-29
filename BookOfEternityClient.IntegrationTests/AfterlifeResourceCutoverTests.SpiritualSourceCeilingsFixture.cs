using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Identifies the actual rank-II acquisition sentence submitted with the second file decision.
    /// </summary>
    internal const string SourceCeilingAcquisitionNarration = "Сильное давление надломило волю хранителя.";

    /// <summary>
    /// Seeds tier-two pressure and, when requested, the player's original special art before snapshot signing.
    /// </summary>
    /// <param name="context">
    /// Genuine full engine original whose profile and soul tiers must agree.
    /// </param>
    /// <param name="special">
    /// Whether to materialize the tier-two art with a 200-percent cost multiplier.
    /// </param>
    /// <param name="capped">
    /// Whether that original special art has a rank-II ceiling; no guarantee is authored.
    /// </param>
    /// <returns>
    /// A task completing after all original tier and optional source-envelope authority is present.
    /// </returns>
    internal static async Task SeedSpiritualSourceCeilingAuthorityAsync(
        ResourceMaterializationTestContext context, bool special, bool capped)
    {
        Assert.False(capped && !special);
        await SeedSpiritualWorseningTierAuthorityAsync(context);
        if (!special) return;
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        var player = Assert.Single(profiles["profiles"]!.AsArray().OfType<JsonObject>(),
            profile => profile["actorId"]!.GetValue<string>() == "player_soul");
        var art = SourceOwnerSpecialArt("player_soul", "player_soul");
        art["tier"] = 2;
        if (capped)
            art["spiritualWoundEnvelope"] = new JsonObject
            {
                ["schemaVersion"] = 1, ["maximumSeverityRank"] = 2, ["guaranteedSeverityRank"] = null
            };
        player["specialArts"]!.AsArray().Add(art);
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
    }

    /// <summary>
    /// Authors the legal two-exchange draft, using the original special art only in the later high-pressure exchange.
    /// </summary>
    /// <param name="context">
    /// Full engine original already signed with dice 15, 5, 18, 3 and tier-two pressure.
    /// </param>
    /// <param name="special">
    /// Whether the second player action spends two points through the original special art instead of one ordinary point.
    /// </param>
    /// <returns>
    /// A task completing after the exact action, dice and chronological cost audits are written.
    /// </returns>
    internal static async Task WriteSpiritualSourceCeilingExchangesAsync(
        ResourceMaterializationTestContext context, bool special)
    {
        await WriteSpiritualWorseningExchangesAsync(context);
        if (!special) return;
        var conflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var second = conflict["activeConflict"]!["exchangeLog"]![1]!;
        second["specialArtAudit"] = new JsonObject
        {
            ["artId"] = "art_source_owner", ["ownerActorType"] = "player_soul",
            ["ownerActorId"] = "player_soul", ["baseOperation"] = "pressure",
            ["costMultiplierPercent"] = 200,
            ["effectNote"] = "Исходная Нить надлома усиливает второе духовное давление."
        };
        var cost = second["actionCostAudit"]!["player"]!;
        cost["effectiveCost"] = 2;
        cost["after"] = 3;
        cost["specialArtId"] = "art_source_owner";
        cost["specialCostMultiplierPercent"] = 200;
        cost["standardEffectiveCost"] = 1;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, conflict.ToJsonString());
    }

    /// <summary>
    /// Creates a rank-II acquisition with the two existing guard and maneuver burden roots, without a prior wound.
    /// </summary>
    /// <param name="opportunityRef">
    /// Exact reference supplied by the second actual file request.
    /// </param>
    /// <returns>
    /// A complete ordinary GM proposal with no client-generated identity or publication fields.
    /// </returns>
    internal static JsonElement CreateSpiritualSourceCeilingDecision(string opportunityRef)
    {
        var decision = JsonNode.Parse(CreateSpiritualWorseningLifecycleDecision(opportunityRef, worsening: true).GetRawText())!;
        decision["proposal"]!["display"]!["acquisitionNarration"] = SourceCeilingAcquisitionNarration;
        return JsonSerializer.SerializeToElement(decision);
    }
}
