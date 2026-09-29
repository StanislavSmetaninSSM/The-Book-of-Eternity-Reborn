using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Adds the established rank-I guaranteed source art before the engine signs the original turn.
    /// </summary>
    /// <param name="context">
    /// Full engine fixture whose original player profile has not yet been snapshotted.
    /// </param>
    /// <returns>
    /// A task completing after the source owner's tier-five art and exact wound envelope are authored.
    /// </returns>
    internal static async Task SeedSpiritualAutomaticGameEngineArtAsync(ResourceMaterializationTestContext context)
    {
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        var art = SourceOwnerSpecialArt("player_soul", "player_soul");
        art["tier"] = 5;
        art["spiritualWoundEnvelope"] = new JsonObject
        {
            ["schemaVersion"] = 1, ["maximumSeverityRank"] = 1, ["guaranteedSeverityRank"] = 1
        };
        profiles["profiles"]![0]!["specialArts"]!.AsArray().Add(art);
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
    }

    /// <summary>
    /// Authors the proven two-pressure recipe whose later guarantee is satisfied by the first selected wound.
    /// </summary>
    /// <param name="context">
    /// Original engine session signed with the four dice values 15, 5, 15 and 5.
    /// </param>
    /// <returns>
    /// A task completing after both original exchanges are authored without applying resources or wounds.
    /// </returns>
    internal static async Task WriteSpiritualAutomaticGameEngineExchangesAsync(ResourceMaterializationTestContext context)
    {
        await WriteCompleteConflictFrameExchangeAsync(context);
        var conflict = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = conflict["activeConflict"]!.AsObject();
        var first = active["exchangeLog"]![0]!.AsObject();
        var second = first.DeepClone().AsObject();
        second["exchangeId"] = "exchange_guarantee_satisfied";
        second["before"] = first["after"]!.DeepClone();
        second["after"] = second["before"]!.DeepClone();
        second["after"]!["oppositionSideStrain"] = "fractured";
        second["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 2;
        second["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = 3;
        second["specialArtAudit"] = new JsonObject
        {
            ["artId"] = "art_source_owner", ["ownerActorType"] = "player_soul",
            ["ownerActorId"] = "player_soul", ["baseOperation"] = "pressure",
            ["costMultiplierPercent"] = 200, ["effectNote"] = "Искусство оставляет след в духовном узоре."
        };
        var cost = second["actionCostAudit"]!["player"]!.AsObject();
        cost["before"] = 3;
        cost["after"] = 1;
        cost["artTier"] = 5;
        cost["effectiveCost"] = 2;
        cost["specialArtId"] = "art_source_owner";
        cost["specialCostMultiplierPercent"] = 200;
        cost["standardEffectiveCost"] = 1;
        second["actionCostAudit"]!["opposition"]!["before"] = 3;
        second["actionCostAudit"]!["opposition"]!["after"] = 0;
        active["exchangeLog"]!.AsArray().Add(second);
        active["oppositionSideStrain"] = "fractured";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, conflict.ToJsonString());
    }

    /// <summary>
    /// Uses real C2 owners to commit the first wound and durably stage the later automatic satisfaction.
    /// This is a saved-state setup seam, not a simulated warm GameEngine process termination.
    /// </summary>
    /// <param name="context">
    /// Full signed engine fixture with a genuine first C2 pair and accurate two-exchange outputs.
    /// </param>
    /// <param name="lease">
    /// Active canonical lease retained while real owners compose and save their selected outcomes.
    /// </param>
    /// <returns>
    /// The physical checkpoint after a deterministic failure of the automatic outcome's final advancement write.
    /// </returns>
    internal static async Task<JsonObject> StageSpiritualAutomaticGameEngineSubmissionAsync(
        ResourceMaterializationTestContext context, FileSystemManager.CanonicalWriteLease lease)
    {
        using (var first = await PrepareC2SubmissionOwnerAsync(context, lease))
        {
            var committed = await first.CommitC2SavedTransportAsync(lease);
            AssertNoConflictFrameErrors(committed.Issues);
            Assert.Equal("committed", committed.Disposition);
        }
        var boundary = await context.Validator.ReadSpiritualWoundContinuationAsync(lease);
        Assert.Equal("automatic_continuation", boundary.Disposition);
        Assert.Null(boundary.Request);
        Assert.Empty(boundary.Issues);
        var priorCommand = Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        var saved = await InterruptC2SelectedAdvanceAsync(context, lease);
        Assert.Equal(priorCommand, await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        return saved;
    }
}
