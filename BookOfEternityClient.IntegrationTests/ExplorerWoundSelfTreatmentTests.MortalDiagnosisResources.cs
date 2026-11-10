using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    // Fixed DATA identities for this explicitly pinned pre-existing bleeding
    // component, not values learned from the current result under assertion.
    private const string MortalDiagnosisBleedingSourceId =
        "effect_component_73d8b30fce69d6af7520c9c2c2d6d48998048aa3f4a642b2295bc9e1b86ebd78";
    private const string MortalDiagnosisBleedingSourceFingerprint =
        "sha256:0895f4a90f0adab979e70302140731aa6a3796775b4f631306c5aea6a64fe4f9";
    private const string MortalDiagnosisBleedingPolicyFingerprint =
        "sha256:ffa78a52c41c61b6dbe1c6255c0de3d47ecf3af427f4cd4bd7158880132b3799";

    private sealed record MortalDiagnosisResourceOriginal(
        string StateJson, string HistoryJson, JsonObject StateRoot, JsonObject HistoryRoot,
        JsonObject Effect, ResourceDefinitionCatalog Definitions, ResourceStateEntry Health,
        ResourceHistoryState History);

    private async Task<MortalDiagnosisResourceOriginal> CaptureMortalDiagnosisResourcesAsync(
        FileSystemManager files, WoundMaterializationEnvelope wound)
    {
        var definitionJson = Assert.IsType<string>(await files.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath));
        var stateJson = Assert.IsType<string>(await files.ReadFileAsync(ResourceMaterializationContract.StatePath));
        var historyJson = Assert.IsType<string>(await files.ReadFileAsync(ResourceMaterializationContract.HistoryPath));
        var effectsJson = Assert.IsType<string>(await files.ReadFileAsync(EffectCarrierCatalog.PlayerPath));
        try
        {
            var definitions = ResourceDefinitionCatalog.ParseCanonical(definitionJson, false);
            Assert.True(definitions.IsValid, JsonSerializer.Serialize(definitions.Issues));
            var catalog = Assert.IsType<ResourceDefinitionCatalog>(definitions.Catalog);
            Assert.True(catalog.TryResolveExact("health", out var healthDefinition));
            Assert.NotNull(healthDefinition);
            Assert.Equal(1, healthDefinition.DefinitionVersion);
            Assert.Equal("resource_definition_builtin_health_v1", healthDefinition.Materialization.DefinitionId);
            Assert.Equal("resource_definition_seal_builtin_health_v1", healthDefinition.Materialization.Seal);
            var state = ResourceStateContract.ParseCanonical(stateJson, catalog, false);
            var history = ResourceHistoryState.ParseCanonical(historyJson, catalog, false);
            Assert.True(state.IsValid, JsonSerializer.Serialize(state.Issues));
            Assert.True(history.IsValid, JsonSerializer.Serialize(history.Issues));
            var ledger = Assert.IsType<ResourceStateLedger>(state.Ledger);
            var originalHistory = Assert.IsType<ResourceHistoryState>(history.History);
            Assert.Empty(originalHistory.ValidateStateAgreement(ledger));
            var health = Assert.Single(ledger.Entries, entry => entry.Coordinate ==
                new ResourceCoordinate("mortal_world", ResourceOwnerKind.Player, "player_current", "health"));
            Assert.Equal(130m, health.Current); Assert.Equal(130m, health.Maximum);
            Assert.Equal(ResourceLifecycleState.Active, health.State);

            var effects = JsonNode.Parse(effectsJson)!.AsObject();
            var effect = Assert.IsType<JsonObject>(Assert.Single(effects["activeEffects"]!.AsArray(),
                row => row!["effectId"]!.GetValue<string>() == "effect_wound_test_bleeding"));
            Assert.Equal("mortal_world", effect["realm"]!.GetValue<string>());
            Assert.Equal("active", effect["state"]!.GetValue<string>());
            Assert.Equal("player", effect["target"]!["kind"]!.GetValue<string>());
            Assert.Equal("player_current", effect["target"]!["targetId"]!.GetValue<string>());
            Assert.Equal("wound", effect["source"]!["kind"]!.GetValue<string>());
            Assert.Equal(wound.WoundId, effect["source"]!["sourceId"]!.GetValue<string>());
            Assert.Equal("definition_wound_test_bleeding", effect["source"]!["definitionKey"]!.GetValue<string>());
            Assert.Equal("source_bound", effect["lifetime"]!["mode"]!.GetValue<string>());
            Assert.Equal(wound.WoundId, effect["lifetime"]!["targetId"]!.GetValue<string>());
            Assert.Equal(42, effect["chronology"]!["createdAtTurn"]!.GetValue<int>());
            var definition = Assert.Single(wound.Consequences.OwnedEffectSources.Definitions,
                row => row.GetProperty("definitionKey").GetString() == "definition_wound_test_bleeding");
            var definitionRoot = JsonNode.Parse(definition.GetRawText())!.AsObject();
            var expectedComponent = JsonNode.Parse("""
                {"componentId":"component_001","profile":"periodic_damage","priority":100,
                 "payload":{"resource":"health","amount":3,"damageType":"bleeding","floorPolicy":"registered_resource_floor"}}
                """)!;
            var expectedTrigger = JsonNode.Parse("""
                {"triggerId":"on_owner_turn_end","eventType":"owner_turn_end","priority":100,
                 "componentIds":["component_001"],"consumeUses":false,"resolutionMode":"deterministic"}
                """)!;
            Assert.True(JsonNode.DeepEquals(expectedComponent, Assert.Single(effect["components"]!.AsArray())));
            Assert.True(JsonNode.DeepEquals(expectedTrigger, Assert.Single(effect["triggers"]!.AsArray())));
            Assert.True(JsonNode.DeepEquals(definitionRoot["components"], effect["components"]));
            Assert.True(JsonNode.DeepEquals(definitionRoot["triggers"], effect["triggers"]));
            Assert.Contains(wound.Consequences.OwnedEffectSources.RootBindings,
                binding => binding.EffectId == "effect_wound_test_bleeding" && binding.DefinitionKey == "definition_wound_test_bleeding");
            Assert.Equal(new ResourcePendingAuthorityBinding("permanent", "effect_wound_test_bleeding"),
                EffectAcceptedTurnPlanner.ResolvePendingEffectAuthority(effect, 43));
            return new(stateJson, historyJson, JsonNode.Parse(stateJson)!.AsObject(), JsonNode.Parse(historyJson)!.AsObject(),
                effect, catalog, health, originalHistory);
        }
        catch
        {
            output.WriteLine("Mortal diagnosis original resource definitions:\n" + definitionJson);
            output.WriteLine("Mortal diagnosis original resource state:\n" + stateJson);
            output.WriteLine("Mortal diagnosis original resource History:\n" + historyJson);
            output.WriteLine("Mortal diagnosis original source effects:\n" + effectsJson);
            throw;
        }
    }

    private void AssertMortalDiagnosisOrdinaryResourceTick(MortalDiagnosisResourceOriginal original,
        string? actualStateJson, string? actualHistoryJson, int turn)
    {
        var expectedState = original.StateRoot.DeepClone().AsObject();
        var expectedHistory = original.HistoryRoot.DeepClone().AsObject();
        try
        {
            Assert.Equal(43, turn);
            Assert.IsType<string>(actualStateJson); Assert.IsType<string>(actualHistoryJson);
            var actualState = ResourceStateContract.ParseCanonical(actualStateJson, original.Definitions, false);
            var actualHistory = ResourceHistoryState.ParseCanonical(actualHistoryJson, original.Definitions, false);
            Assert.True(actualState.IsValid, JsonSerializer.Serialize(actualState.Issues));
            Assert.True(actualHistory.IsValid, JsonSerializer.Serialize(actualHistory.Issues));
            var ledger = Assert.IsType<ResourceStateLedger>(actualState.Ledger);
            var history = Assert.IsType<ResourceHistoryState>(actualHistory.History);
            Assert.Empty(history.ValidateStateAgreement(ledger));
            var historyRoot = JsonNode.Parse(actualHistoryJson)!.AsObject();
            var previousRows = original.HistoryRoot["entries"]!.AsArray();
            var currentRows = historyRoot["entries"]!.AsArray();
            Assert.Equal(previousRows.Count + 1, currentRows.Count);
            for (var index = 0; index < previousRows.Count; index++)
                Assert.True(JsonNode.DeepEquals(previousRows[index], currentRows[index]), "Original resource History row " + index + " changed.");
            expectedHistory["entries"]!.AsArray().Add(currentRows[^1]!.DeepClone());
            var tick = Assert.Single(history.Transitions.Skip(original.History.Transitions.Count));
            var eventRef = EffectAcceptedTurnPlanner.CreateLifecycleResourceTriggerEventRef(
                "turn_43:lifecycle:owner_turn_end:player_current", "effect_wound_test_bleeding",
                EffectAcceptedTurnPlanner.ResolvePendingEffectAuthority(original.Effect, turn), "on_owner_turn_end");
            Assert.Equal(eventRef, tick.EventRef); Assert.Equal(turn, tick.Turn);
            Assert.Equal(original.Health.Coordinate, tick.Coordinate);
            Assert.Equal("effect_component", tick.OriginKind); Assert.Equal(MortalDiagnosisBleedingSourceId, tick.OriginId);
            Assert.Equal(new ResourceSourceEvidence("effect_component", MortalDiagnosisBleedingSourceId,
                MortalDiagnosisBleedingSourceFingerprint), tick.SourceEvidence);
            Assert.Equal(MortalDiagnosisBleedingPolicyFingerprint, tick.PolicyFingerprint);
            Assert.Equal(ResourceMutationPhase.EffectTrigger, tick.Phase);
            Assert.Equal(200, tick.Priority); Assert.Equal(0, tick.ExecutionSequence);
            Assert.Equal(ResourceTransitionOperation.Damage, tick.Operation); Assert.Equal(ResourceTransitionOutcome.Applied, tick.Outcome);
            Assert.Equal(3m, tick.RequestedAmount); Assert.Equal(3m, tick.AppliedAmount);
            Assert.Equal(original.Health.Snapshot, tick.BeforeState);
            Assert.Equal(original.Health.Snapshot with { Current = 127m }, tick.AfterState);
            Assert.Null(tick.CapacityDisposition); Assert.Null(tick.ReceiptId);

            // One ordinary owner-turn activation, independently of the three
            // wound recovery cadences. All other coordinates/properties stay exact.
            var expectedHealth = Assert.Single(expectedState["entries"]!.AsArray(), row =>
                row!["realm"]!.GetValue<string>() == "mortal_world" && row["ownerKind"]!.GetValue<string>() == "player" &&
                row["resourceOwnerId"]!.GetValue<string>() == "player_current" && row["resourceKey"]!.GetValue<string>() == "health")!;
            expectedHealth["current"] = 127;
            expectedHealth["chronology"]!["lastTransitionId"] = tick.TransitionId;
            expectedHealth["chronology"]!["lastEventRef"] = eventRef;
            expectedHealth["chronology"]!["lastTransitionTurn"] = turn;
            Assert.True(JsonNode.DeepEquals(expectedState, JsonNode.Parse(actualStateJson)), "Complete resource state differs from the single authored bleeding tick.");
            Assert.True(JsonNode.DeepEquals(expectedHistory, historyRoot));
        }
        catch
        {
            output.WriteLine("Mortal diagnosis resource before state:\n" + original.StateJson);
            output.WriteLine("Mortal diagnosis resource expected state:\n" + expectedState.ToJsonString());
            output.WriteLine("Mortal diagnosis resource actual state:\n" + (actualStateJson ?? "<missing>"));
            output.WriteLine("Mortal diagnosis resource before History:\n" + original.HistoryJson);
            output.WriteLine("Mortal diagnosis resource expected History (original prefix plus candidate tick, whose fields are checked independently):\n" + expectedHistory.ToJsonString());
            output.WriteLine("Mortal diagnosis resource actual History:\n" + (actualHistoryJson ?? "<missing>"));
            output.WriteLine("Mortal diagnosis resource original source effect:\n" + original.Effect.ToJsonString());
            throw;
        }
    }
}
