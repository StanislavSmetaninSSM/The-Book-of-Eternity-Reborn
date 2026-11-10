using System.Diagnostics;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Publishes one genuine retained N2 pressure plan and validates its signed next turn, without creating another wound.</summary>
    [Fact]
    public Task SpiritualGuardianGroupPressure_N2PublishesAndValidatesSignedNext()
        => WithGuardianGroupRegisteredPlanAsync("guardian_group_n2_publish", async (setup, completed, plan) =>
        {
            var context = setup.Copy.Context;
            var started = Stopwatch.GetTimestamp();
            var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
            RequireGuardianJourneyIssues(setup.Name, "group_common_c4", result.Issues);
            Assert.Same(plan, result.MechanicsPlan);
            foreach (var row in plan.WoundCarrierAfterImages.Concat(plan.EffectCarrierAfterImages).Concat(plan.OwnerCompanionAfterImages))
                Assert.True(JsonNode.DeepEquals(row.Value, await context.ReadJsonAsync(row.Key)), row.Key);
            Assert.True(JsonNode.DeepEquals(plan.StateAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.StatePath)));
            Assert.True(JsonNode.DeepEquals(plan.HistoryAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath)));
            Assert.True(JsonNode.DeepEquals(plan.EffectIdentityAfterImage, await context.ReadJsonAsync(EffectIdentityState.StatePath)));
            AssertGuardianAttributionRegisteredPlan(setup, completed, plan);
            foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
                Assert.Equal(setup.Origin.Tracked[path], await context.FileSystem.ReadFileBytesAsync(path));
            await AssertGuardianGroupPublishedParentsAsync(setup, completed);
            await ExecuteGuardianLeaseStagesAsync(context, lease =>
            {
                AssertPressureNoPrivatePair(context, lease);
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                return Task.CompletedTask;
            });
            WritePressureOwnerTiming("guardian_group_common_c4", setup.Name, started);
            started = Stopwatch.GetTimestamp();
            await context.CaptureValidatedPendingSnapshotAsync(turn: 47, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5],
                requestId: "request_" + setup.Name + "_published_read_47");
            var manifest = ReadGuardianAttributionJson((await context.FileSystem.ReadFileBytesAsync(
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!);
            Assert.Equal(47, manifest["turnNumber"]!.GetValue<int>());
            foreach (var path in PressureOrdinarySignedPaths)
                Assert.Equal(await context.FileSystem.ReadFileBytesAsync(path),
                    await context.FileSystem.ReadFileBytesAsync(manifest["files"]![path]!.GetValue<string>()));
            RequireGuardianJourneyIssues(setup.Name, "group_signed47_frame", await context.Validator.ValidateGameStateAsync(
                new GameStateValidationSelection(GameStateValidationPhase.AfterlifeSpiritualConflictState)));
            WritePressureOwnerTiming("guardian_group_signed47", setup.Name, started);
        });

    /// <summary>Checks both complete original parents and their effects; only the ordinary owner-end chronology may change.</summary>
    private static async Task AssertGuardianGroupPublishedParentsAsync(GuardianAttributionJourney setup, GuardianAttributionCompleted completed)
    {
        var context = setup.Copy.Context;
        var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
        var affected = ReadPressureSingletonJourneyProfile(setup, profiles);
        Assert.Equal(2, setup.Retained.Parents.Count);
        Assert.True(JsonNode.DeepEquals(new JsonArray(setup.Retained.Parents.Select(row => row.DeepClone()).ToArray()), affected["activeWounds"]));
        var beforeIdentity = JsonNode.Parse(setup.Origin.IdentityJson)!.AsObject();
        var restoredIdentity = (await context.ReadJsonAsync(EffectIdentityState.StatePath))!.DeepClone().AsObject();
        var restoredProfiles = profiles.DeepClone().AsObject();
        var effects = ReadPressureSingletonJourneyProfile(setup, restoredProfiles)["activeEffects"]!.AsArray();
        Assert.Equal(setup.Retained.Effects.Count, effects.Count);
        foreach (var baseline in setup.Retained.Effects)
        {
            var id = baseline["effectId"]!.GetValue<string>();
            var effect = Assert.Single(effects, row => row!["effectId"]!.GetValue<string>() == id)!;
            var beforeEntry = Assert.Single(beforeIdentity["entries"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == id)!;
            var entry = Assert.Single(restoredIdentity["entries"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == id)!;
            var transitions = entry["transitions"]!.AsArray();
            Assert.Equal(beforeEntry["transitions"]!.AsArray().Count + 1, transitions.Count);
            var last = transitions[^1]!;
            Assert.Equal(("trigger", 46), (last["kind"]!.GetValue<string>(), last["turn"]!.GetValue<int>()));
            var execution = Assert.Single(completed.Resources.ResourceTriggerExecutions, row => row.EffectId == id);
            Assert.Equal(("on_owner_turn_end", "owner_turn_end"), (execution.TriggerId, execution.EventKind));
            Assert.Equal(execution.EventRef, last["eventRef"]!.GetValue<string>());
            Assert.Empty(execution.MutationKeys);
            var chronology = baseline["chronology"]!.DeepClone();
            chronology["lastTransitionTurn"] = 46;
            chronology["lastTransitionId"] = last["transitionId"]!.GetValue<string>();
            Assert.True(JsonNode.DeepEquals(chronology, effect["chronology"]));
            effect["chronology"] = baseline["chronology"]!.DeepClone();
            Assert.True(JsonNode.DeepEquals(baseline, effect));
            transitions.RemoveAt(transitions.Count - 1);
        }
        Assert.True(JsonNode.DeepEquals(beforeIdentity, restoredIdentity));
        var expectedProfiles = ReadGuardianAttributionJson(setup.Origin.Tracked[AfterlifeEntityProfileState.StatePath]!).AsObject();
        expectedProfiles["schemaVersion"] = 1;
        var other = Assert.Single(expectedProfiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray(),
            row => row!["actorType"]!.GetValue<string>() == "player_soul")!;
        other["activeEffects"] = new JsonArray(); other["activeWounds"] = new JsonArray();
        Assert.True(JsonNode.DeepEquals(expectedProfiles, restoredProfiles));
    }
}
