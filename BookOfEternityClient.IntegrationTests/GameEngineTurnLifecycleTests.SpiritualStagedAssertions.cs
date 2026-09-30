using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Verifies the actual staged caller publishes one player wound, six spends and one story, then remains idle without replay.
    /// </summary>
    /// <param name="context">
    /// Physical session after normal or cold completion.
    /// </param>
    /// <param name="engine">
    /// Actual successful caller holding turn 42 and its player response.
    /// </param>
    /// <param name="original">
    /// Frozen original three-exchange draft from before any GM response.
    /// </param>
    /// <param name="observed">
    /// Real initial/dependent requests and immutable saved decision evidence.
    /// </param>
    /// <returns>
    /// A task completing after canonical publication, output, receipt and repeat-idle preservation checks.
    /// </returns>
    private static async Task AssertSpiritualStagedPublicationAsync(ResourceMaterializationTestContext context,
        GameEngine engine, JsonObject original, SpiritualStagedObservation observed)
    {
        var receipt = Assert.IsType<JsonObject>(await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath));
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        var source = Assert.Single(receipt["sources"]!.AsArray())!["witness"]!;
        Assert.Equal("exchange_conflict_frame_42", source["exchangeId"]!.GetValue<string>());
        Assert.Equal("player", source["affectedSide"]!.GetValue<string>());
        var decision = Assert.Single(receipt["decisions"]!.AsArray())!;
        Assert.Equal("materialize", decision["decision"]!.GetValue<string>());
        Assert.Equal("spiritual_decision_" + observed.DecisionFingerprint![7..], decision["decisionId"]!.GetValue<string>());
        var initial = Assert.Single(observed.Phase.Requests.Values, request => request.Phase == "decision");
        Assert.Equal(initial.Offer!.OpportunityRef, decision["opportunityRef"]!.GetValue<string>());
        var identities = WoundIdentityState.Parse(await context.FileSystem.ReadFileAsync(WoundIdentityState.StatePath), WoundIdentityState.StatePath);
        Assert.True(identities.IsValid);
        var identity = Assert.Single(identities.State!.Entries);
        Assert.Equal("player_soul", identity.OwnerKind);
        Assert.Equal("player_soul", identity.OwnerId);
        Assert.Equal(decision["woundId"]!.GetValue<string>(), identity.WoundId);
        Assert.Equal(42, identity.CreatedAtTurn);
        var wounds = WoundHistoryState.Parse(await context.FileSystem.ReadFileAsync(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        Assert.True(wounds.IsValid);
        var creation = Assert.Single(wounds.State!.Transitions);
        Assert.Equal("create", creation.Kind);
        Assert.Equal(identity.WoundId, creation.WoundId);
        Assert.Equal(decision["transitionId"]!.GetValue<string>(), creation.TransitionId);
        var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!["profiles"]!.AsArray();
        var player = Assert.Single(profiles.OfType<JsonObject>(), row => row["actorId"]!.GetValue<string>() == "player_soul");
        var carrier = Assert.Single(player["activeWounds"]!.AsArray())!;
        Assert.Equal(identity.WoundId, carrier["woundId"]!.GetValue<string>());
        var parsedWound = WoundMaterializationContract.Parse(carrier.ToJsonString(), AfterlifeEntityProfileState.StatePath + ".profiles[0].activeWounds[0]");
        Assert.True(parsedWound.IsValid);
        Assert.Equal(1, parsedWound.Wound!.Severity.Rank);
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
        Assert.All(balances, row => Assert.Equal(3m, row.Current));
        var history = ResourceHistoryState.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath), definitions.Catalog!, allowMissingPristine: false);
        Assert.True(history.IsValid);
        var spends = history.History!.Transitions.Where(row => row.Turn == 42 && row.Operation == ResourceTransitionOperation.Spend).ToArray();
        Assert.Equal(6, spends.Length);
        Assert.All(spends, row => Assert.Equal(1m, row.AppliedAmount));
        foreach (var after in new[] { 5m, 4m, 3m })
            Assert.Equal(2, spends.Count(row => row.BeforeState!.Current == after + 1 && row.AfterState!.Current == after));
        var expected = AfterlifeResourceCutoverTests.CreateSpiritualStagedCorrectionB(AfterlifeResourceCutoverTests.CreateSpiritualStagedCorrectionA(original));
        var actual = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        Assert.True(JsonNode.DeepEquals(expected["activeConflict"]!["exchangeLog"], actual["activeConflict"]!["exchangeLog"]));
        Assert.Equal("contested", actual["activeConflict"]!["conflictPosition"]!.GetValue<string>());
        Assert.Equal("strained", actual["activeConflict"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("clear", actual["activeConflict"]!["oppositionSideStrain"]!.GetValue<string>());
        Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
        var response = GetPrivateField<GameResponse>(engine, "_lastResponse");
        Assert.Contains("Чужое давление надломило волю души.", response.Response, StringComparison.Ordinal);
        Assert.Contains("Надлом воли", Assert.Single(Assert.IsType<string[]>(response.WoundNotifications)), StringComparison.Ordinal);
        var reread = Assert.IsType<GameResponse>(await InvokePrivateTaskResultAsync(engine, "BuildGameResponseFromFiles"));
        Assert.True(reread.WoundNotifications is null or { Length: 0 });
        var stories = await new StoryService(context.FileSystem, NullLogger<StoryService>.Instance).ReadStoryAsync("stories/chaos_sea.jsonl");
        var story = Assert.Single(stories, row => row.Turn == 42);
        Assert.Contains("Чужое давление надломило волю души.", story.Narrative, StringComparison.Ordinal);
        Assert.Equal("Удержать встречное духовное давление.", story.Player);
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
