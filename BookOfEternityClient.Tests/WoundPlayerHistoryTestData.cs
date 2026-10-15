using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Strict detached codec evidence, not a live recovery/publication authority.</summary>
internal static class WoundPlayerHistoryTestData
{
    internal static WoundPlayerProjectionInput TerminalInput(bool hidden = false)
    {
        var node = WoundContractTestData.CreateActiveWound("wound_healed_ui");
        node["display"]!["name"] = "Известная зажившая рана";
        node["display"]!["visibility"] = hidden ? "hidden" : "known_to_player";
        node["treatment"]!["routes"]![0]!["outcomes"]![0]!["result"]![1] = new JsonObject
        { ["kind"] = "add_recovery", ["points"] = 1 };
        node["severity"]!["value"] = "I"; node["severity"]!["rank"] = 1; node["severity"]!["maximumAtCreation"] = "I";
        node["consequences"] = new JsonObject
        {
            ["slotBudget"] = 1, ["slotsUsed"] = 0, ["entries"] = new JsonArray(),
            ["ownedEffectSources"] = new JsonObject { ["definitions"] = new JsonArray(), ["rootBindings"] = new JsonArray() }
        };
        node["recovery"]!["mode"] = "progressive";
        node["recovery"]!["clockKind"] = "world_time.currentTimeInMinutes";
        node["recovery"]!["cadence"] = 10;
        node["recovery"]!["currentStepThreshold"] = 3;
        node["recovery"]!["blockers"] = new JsonArray();
        node["lastTransition"]!["transitionId"] = "create_wound_healed_ui";
        node["recovery"]!["recoveryAnchor"] = new JsonObject
        { ["anchorKind"] = "creation", ["anchorMinute"] = 100, ["anchorTransitionId"] = "create_wound_healed_ui" };
        var start = WoundPlayerProjectionTestData.Input(node);
        var wound = Assert.Single(WoundCarrierCatalog.Build(start.Carriers).Occurrences).Wound;
        var prefix = WoundHistoryState.SerializeCanonical(start.History);
        var events = WoundAcceptedEventAuthorityComposer.ComposeDefaultAcceptedTurn("session_ui", "request_ui", "snapshot_ui", 43).Events;
        var binding = new WoundAcceptedTurnBinding("session_ui", "request_ui", "snapshot_ui", "mortal_world", 43,
            events, WoundAcceptedEventSetFingerprint.Compute(events));
        var context = new MortalWoundTreatmentAuthority.Context(1, "mortal_world", "player", "player_current", "player", "player_current", "location_ui");
        var source = new MortalWoundRecoverySourceEvidence("generation_ui", "", context, "", [10],
            "game_state/player/wounds.json.activeWounds[0]", WoundIdentityState.ComputeSemanticFingerprint(wound),
            WoundAcceptedTurnFingerprintWriter.Compute(["mortal_wound_treatment_history", "1", prefix]), start.History.NextOrdinal,
            Fingerprint("identity"), "", 135, Fingerprint("effects"), Fingerprint("items"), Fingerprint("actors"),
            Fingerprint("player"), Fingerprint("npc"), "", Fingerprint("requirements"), "");
        source = MortalWoundRecoveryPersistedResult.SealSourceEvidence(source, binding, wound);
        var planned = MortalWoundRecoveryPlanner.ReconstructResolution(source, binding, wound, 0, 0);
        var resolution = Assert.IsType<MortalWoundRecoveryResolution>(planned.Resolution);
        var program = MortalWoundRecoveryProgressProgram.Create(1, 0, 3, resolution.ElapsedCadences, true);
        Assert.Equal(2, program.Length);
        var stages = new List<MortalWoundRecoveryPublishedStage>();
        var before = wound;
        for (var index = 0; index < program.Length; index++)
        {
            var scalar = program[index];
            var kind = scalar.FullHeal ? "heal" : "recover";
            var transition = "ui_" + kind;
            var after = before with
            {
                Lifecycle = scalar.FullHeal ? "healed" : "active",
                Care = scalar.FullHeal ? before.Care with { State = "healed", ActiveCourseId = null } : before.Care,
                Recovery = before.Recovery with { CurrentStepProgress = scalar.Progress,
                    LastTickKey = EffectAcceptedTurnPlanner.GetMortalRecoveryStageTick(resolution.TickKey, index, 0, scalar.FullHeal) },
                LastTransition = new(transition, index + 2, 43, kind)
            };
            stages.Add(new(transition, kind, WoundIdentityState.ComputeSemanticFingerprint(before),
                WoundIdentityState.ComputeSemanticFingerprint(after), scalar.FullHeal, 43, events[0].EventRef,
                "operation_ui_" + kind, before, after));
            before = after;
        }
        var result = MortalWoundRecoveryPersistedResult.CreateComparisonEvidence(source, binding, wound, resolution, stages, 0, 0);
        var root = JsonNode.Parse(prefix)!.AsObject();
        foreach (var stage in result.Stages)
        {
            var row = WoundContractTestData.CreateTransition(stage.Kind, root["nextOrdinal"]!.GetValue<int>(), stage.AfterWound.LastTransition.Ordinal,
                terminal: stage.Terminal);
            row["woundId"] = wound.WoundId; row["transitionId"] = stage.TransitionId; row["turn"] = stage.Turn;
            row["eventRef"] = stage.EventRef; row["operationKey"] = stage.OperationKey;
            row["beforeFingerprint"] = stage.BeforeFingerprint; row["afterFingerprint"] = stage.AfterFingerprint;
            row["sourceFingerprint"] = result.Resolution.AuthorityFingerprint; row["attemptId"] = null;
            row["cycleKey"] = stage.AfterWound.Recovery.LastTickKey;
            row["readableSummary"] = WoundAcceptedTurnPlanner.RecoveryPublicationSummary;
            row["outputFingerprint"] = WoundHistoryState.ComputeOutputFingerprint(stage.OperationKey, stage.EventRef, WoundAcceptedTurnPlanner.RecoveryPublicationSummary);
            if (stage == result.Stages[0]) row["transitionResult"] = result.ToCanonicalJson();
            root["transitions"]!.AsArray().Add(row); root["nextOrdinal"] = root["nextOrdinal"]!.GetValue<int>() + 1;
        }
        var history = WoundHistoryState.Parse(root.ToJsonString(), "test.history");
        Assert.True(history.IsValid, string.Join(";", history.Issues));
        var final = result.Stages[^1].AfterWound;
        var identity = WoundIdentityState.Parse(WoundContractTestData.CreateIdentityIndex(WoundContractTestData.CreateIdentityEntry(
            woundId: wound.WoundId, status: "healed", lastTransitionOrdinal: final.LastTransition.Ordinal,
            terminalTransitionId: final.LastTransition.TransitionId, semanticFingerprint: WoundIdentityState.ComputeSemanticFingerprint(final))).ToJsonString(), "test.identity");
        Assert.True(identity.IsValid);
        var empty = WoundPlayerProjectionTestData.Input();
        Assert.Empty(history.State!.ValidateAgreement(identity.State!, WoundCarrierCatalog.Build(empty.Carriers)));
        return empty with { History = history.State!, Identities = identity.State! };
    }

    private static string Fingerprint(string label) => WoundAcceptedTurnFingerprintWriter.Compute([label]);
}
