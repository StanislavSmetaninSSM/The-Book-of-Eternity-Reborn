using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Confirms one wound, four original payments, corrected final control and no repeated publication on idle reentry.
    /// </summary>
    /// <param name="context">
    /// Physical session after warm or cold engine completion.
    /// </param>
    /// <param name="engine">
    /// Actual engine that accepted the original turn.
    /// </param>
    /// <param name="original">
    /// Frozen original draft with signed dice and closed first exchange.
    /// </param>
    /// <param name="observed">
    /// Real request and saved wound evidence from the responder.
    /// </param>
    /// <param name="strong">
    /// Selects the original guardian's two-point costs instead of three-point costs.
    /// </param>
    /// <returns>
    /// A task completing after canonical, cleanup and repeat-idle checks.
    /// </returns>
    private static async Task AssertBindingLifecyclePublicationAsync(ResourceMaterializationTestContext context,
        GameEngine engine, JsonObject original, SpiritualStagedObservation observed, bool strong)
    {
        var receipt = Assert.IsType<JsonObject>(await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath));
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        var source = Assert.Single(receipt["sources"]!.AsArray())!["witness"]!;
        Assert.Equal("exchange_conflict_frame_42", source["exchangeId"]!.GetValue<string>());
        var decision = Assert.Single(receipt["decisions"]!.AsArray())!;
        Assert.Equal("materialize", decision["decision"]!.GetValue<string>());
        Assert.Equal("spiritual_decision_" + observed.DecisionFingerprint![7..], decision["decisionId"]!.GetValue<string>());
        Assert.Equal(Assert.Single(observed.Phase.Requests.Values, request => request.Phase == "decision").Offer!.OpportunityRef,
            decision["opportunityRef"]!.GetValue<string>());
        var identities = WoundIdentityState.Parse(await context.FileSystem.ReadFileAsync(WoundIdentityState.StatePath), WoundIdentityState.StatePath);
        Assert.True(identities.IsValid);
        var identity = Assert.Single(identities.State!.Entries);
        Assert.Equal("player_soul", identity.OwnerId);
        Assert.Equal(decision["woundId"]!.GetValue<string>(), identity.WoundId);
        var wounds = WoundHistoryState.Parse(await context.FileSystem.ReadFileAsync(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        Assert.True(wounds.IsValid);
        var creation = Assert.Single(wounds.State!.Transitions);
        Assert.Equal("create", creation.Kind);
        Assert.Equal(identity.WoundId, creation.WoundId);
        Assert.Equal(decision["transitionId"]!.GetValue<string>(), creation.TransitionId);
        var effects = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(await context.ReadJsonAsync(EffectIdentityState.StatePath)), EffectIdentityState.StatePath);
        Assert.Empty(effects.Issues);
        var effect = Assert.Single(effects.State!.Entries);
        Assert.Equal(identity.WoundId, effect.Source["sourceId"]!.GetValue<string>());
        Assert.Equal("create", Assert.Single(effect.Transitions).Kind);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), allowMissingPristine: false);
        Assert.True(definitions.IsValid);
        var state = ResourceStateContract.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath), definitions.Catalog!, allowMissingPristine: false);
        Assert.True(state.IsValid);
        var balances = state.Ledger!.Entries.Where(row => row.Coordinate.ResourceKey == "spiritual_action_points").ToArray();
        Assert.Equal(2, balances.Length);
        Assert.Equal(1m, Assert.Single(balances, row => row.Coordinate.ResourceOwnerId == "player_soul").Current);
        var oppositionBalance = Assert.Single(balances,
            row => row.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide);
        Assert.Equal("afterlife_conflict_side_cost", oppositionBalance.Coordinate.ResourceOwnerId);
        Assert.Equal(strong ? 2m : 0m, oppositionBalance.Current);
        var history = ResourceHistoryState.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath), definitions.Catalog!, allowMissingPristine: false);
        Assert.True(history.IsValid);
        var spends = history.History!.Transitions.Where(row => row.Turn == 42 && row.Operation == ResourceTransitionOperation.Spend).ToArray();
        Assert.Equal(4, spends.Length);
        Assert.Equal(strong ? new[] { 2m, 2m, 2m, 3m } : new[] { 2m, 3m, 3m, 3m },
            spends.Select(row => row.AppliedAmount).Order().ToArray());
        var actual = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var corrected = AfterlifeResourceCutoverTests.CreateBindingLifecycleCorrectionA(original, strong);
        Assert.True(JsonNode.DeepEquals(corrected["activeConflict"]!["exchangeLog"], actual["activeConflict"]!["exchangeLog"]));
        Assert.True(JsonNode.DeepEquals(corrected["activeConflict"]!["exchangeLog"]![1]!["after"]!["controlState"], actual["activeConflict"]!["controlState"]));
        Assert.Equal(strong ? "player_dominant" : "player_advantaged", actual["activeConflict"]!["conflictPosition"]!.GetValue<string>());
        Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
        var stories = await new StoryService(context.FileSystem, NullLogger<StoryService>.Instance).ReadStoryAsync("stories/chaos_sea.jsonl");
        Assert.Contains("Чужое давление надломило волю души.", Assert.Single(stories, row => row.Turn == 42).Narrative, StringComparison.Ordinal);
        foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json", "ready/turn_error.json",
            "game_state/control/pending_turn_snapshot.json", "game_state/control/pending_turn_snapshot.authority.json",
            "game_state/control/validation_repair_request.json", "game_state/control/validation_repair_ready.json",
            SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
            Assert.False(context.FileSystem.FileExists(path), path);
        var accepted = await ReadSpiritualStagedPhysicalImagesAsync(context);
        Assert.False(Assert.IsType<bool>(await InvokePrivateTaskResultAsync(engine,
            "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync").WaitAsync(TimeSpan.FromSeconds(60))));
        foreach (var pair in accepted) Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(pair.Key));
    }
}
