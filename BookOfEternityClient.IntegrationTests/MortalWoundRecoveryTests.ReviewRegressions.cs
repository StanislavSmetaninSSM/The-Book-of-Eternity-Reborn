using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundRecoveryTests
{
    /// <summary>
    /// Accepts genuine stabilization after a blocked check at the same minute,
    /// then replays the new epoch's own published receipt after reopening.
    /// </summary>
    [Fact]
    public void Publication_SameMinuteStabilizationStartsFreshEpochRatherThanCorruptReplay()
    {
        using var fixture = Fixture.Create(Scenario.RequiresStabilization());
        var blocked = Assert.IsType<MortalWoundRecoveryResolution>(Required(InvokePlan(fixture), "Resolution"));
        Assert.Equal(MortalWoundRecoveryDisposition.BlockedNotStabilized, blocked.RecoveryDisposition);
        ComposeAndPublishRecovery(fixture, blocked);
        fixture.PublishSameMinuteStabilization();
        var stabilized = WoundMaterializationContract.Parse(
            ReadActiveOrTerminalRecoveryWound(fixture.FileSystem, fixture.WoundId).ToJsonString(), "stabilized").Wound!;
        Assert.Equal(blocked.TickKey, stabilized.Recovery.LastTickKey);
        Assert.Equal("stabilization", stabilized.Recovery.RecoveryAnchor!.AnchorKind);
        Assert.Equal(110, stabilized.Recovery.RecoveryAnchor.AnchorMinute);
        Assert.Equal(stabilized.LastTransition.TransitionId, stabilized.Recovery.RecoveryAnchor.AnchorTransitionId);
        var beforePlanning = fixture.CaptureCanonicalTreeBytes();
        var planned = InvokePlan(fixture);
        Assert.Equal("Resolved", Convert.ToString(Required(planned, "Disposition")));
        Assert.Null(Optional(planned, "ReplayReceipt"));
        var fresh = Assert.IsType<MortalWoundRecoveryResolution>(Required(planned, "Resolution"));
        Assert.Equal(MortalWoundRecoveryDisposition.NotDue, fresh.RecoveryDisposition);
        Assert.Equal(0, fresh.ElapsedCadences);
        Assert.Equal(120, fresh.NextRecoveryAnchorMinute);
        Assert.NotEqual(blocked.TickKey, fresh.TickKey);
        fixture.AssertCanonicalTreeBytesUnchanged(beforePlanning);
        var receipt = ComposeAndPublishRecovery(fixture, fresh);
        fixture.AssertCarrierIdentityHistoryAgreement();
        fixture.PrepareFreshContinuationTurn(47, "new_stabilization_epoch_replay", 110);
        fixture.RestartForReplay();
        var replayTree = fixture.CaptureCanonicalTreeBytes();
        AssertExactReplay(InvokePlan(fixture), receipt);
        fixture.AssertCanonicalTreeBytesUnchanged(replayTree);
    }

    /// <summary>
    /// Rejects a damaged accepted treatment continuation before reading the live clock.
    /// </summary>
    [Fact]
    public void Plan_SameMinuteTreatmentHistoryTamperPrecedesMalformedClock()
    {
        using var fixture = Fixture.Create(Scenario.RequiresStabilization());
        var blocked = Assert.IsType<MortalWoundRecoveryResolution>(Required(InvokePlan(fixture), "Resolution"));
        ComposeAndPublishRecovery(fixture, blocked);
        fixture.PublishSameMinuteStabilization();
        fixture.TamperPersistedRecoveryEvidence("history");
        fixture.CorruptLiveClock();
        var tamperedTree = fixture.CaptureCanonicalTreeBytes();
        AssertInvalidHistory(InvokePlan(fixture));
        fixture.AssertCanonicalTreeBytesUnchanged(tamperedTree);
    }

    /// <summary>
    /// Rejects a genuine planner result when its ordered outcomes cannot publish
    /// together or its retained roots exceed the resulting severity budget.
    /// </summary>
    /// <param name="axis">
    /// The impossible combined-outcome or slot-budget boundary to construct through real creation.
    /// </param>
    [Theory]
    [InlineData("combined")]
    [InlineData("slot_budget")]
    public void Compose_RejectsImpossibleRecoveryProgramWithoutPartialPublication(string axis)
    {
        var scenario = axis == "combined" ? Scenario.ConcurrentIndependentCadences() :
            Scenario.AtDueBoundary() with { StartsStabilized = false, IncludeSecondCreationRoot = true };
        using var fixture = Fixture.Create(scenario);
        fixture.AssertCarrierIdentityHistoryAgreement();
        var source = WoundMaterializationContract.Parse(
            ReadActiveOrTerminalRecoveryWound(fixture.FileSystem, fixture.WoundId).ToJsonString(), "rejectedProgramSource").Wound!;
        if (axis == "slot_budget")
        {
            Assert.Equal(2, source.Severity.Rank);
            Assert.Equal(2, source.Consequences.OwnedEffectSources.RootBindings.Count);
        }
        var resolution = Assert.IsType<MortalWoundRecoveryResolution>(Required(InvokePlan(fixture), "Resolution"));
        Assert.True(resolution.ElapsedCadences > 0);
        if (axis == "combined")
            Assert.Contains(resolution.TransitionIntents, intent => intent is MortalWoundRecoveryDeteriorationIntent);
        var before = fixture.CaptureCanonicalTreeBytes();
        var composed = MortalWoundRecoveryAcceptedPlanComposer.Compose(fixture.FileSystem, fixture.Lease,
            fixture.Binding, resolution);
        Assert.Equal(MortalWoundRecoveryAcceptedPlanCompositionDisposition.Rejected, composed.Disposition);
        Assert.NotEmpty(composed.Issues);
        Assert.Null(composed.AcceptedPlan);
        Assert.Null(composed.WoundStageBundle);
        Assert.Null(composed.Receipt);
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(fixture.FileSystem, fixture.Lease, out _, out _));
        fixture.AssertCanonicalTreeBytesUnchanged(before);
    }

    /// <summary>
    /// Publishes all four severity thresholds and terminal healing together,
    /// preserving every real intermediate effect generation and one replay receipt.
    /// </summary>
    [Fact]
    public void Publication_AllSeverityTiersRetireIntermediateGenerationsInOneAcceptedTurn()
    {
        var baseline = Scenario.MultiCadenceJump();
        var wound = baseline.Wound.DeepClone().AsObject();
        wound["severity"]!["value"] = "IV";
        wound["severity"]!["rank"] = 4;
        wound["severity"]!["maximumAtCreation"] = "IV";
        wound["consequences"]!["slotBudget"] = 4;
        wound["recovery"]!["currentStepThreshold"] = 1;
        foreach (var outcome in wound["treatment"]!["routes"]![0]!["outcomes"]!.AsArray().OfType<JsonObject>())
            outcome["result"] = new JsonArray(outcome["result"]!.AsArray().OfType<JsonObject>()
                .Where(operation => operation["kind"]!.GetValue<string>() != "reduce_severity")
                .Select(operation => (JsonNode?)operation.DeepClone()).ToArray());
        using var fixture = Fixture.Create(baseline with { Wound = wound, Minute = 145 });
        var source = WoundMaterializationContract.Parse(
            ReadActiveOrTerminalRecoveryWound(fixture.FileSystem, fixture.WoundId).ToJsonString(), "allTiersSource").Wound!;
        Assert.Equal(4, source.Severity.Rank);
        var resolution = Assert.IsType<MortalWoundRecoveryResolution>(Required(InvokePlan(fixture), "Resolution"));
        Assert.Equal(4, resolution.ElapsedCadences);
        var receipt = ComposeAndPublishRecovery(fixture, resolution);
        var history = WoundHistoryState.Parse(fixture.FileSystem.ReadFileSync(WoundHistoryState.HistoryPath),
            WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, Issues(history.Issues));
        var durable = Assert.Single(history.State!.Transitions.Select(row => row.TransitionResult)
            .OfType<MortalWoundRecoveryPersistedResult>());
        Assert.Equal(new[] { 3, 2, 1, 1, 1 }, durable.Stages.Select(stage => stage.AfterWound.Severity.Rank));
        Assert.Equal(new[] { "recover", "recover", "recover", "recover", "heal" },
            durable.Stages.Select(stage => stage.Kind));
        Assert.All(durable.Stages, stage => Assert.Equal(fixture.Binding.Turn, stage.Turn));
        Assert.Equal(resolution.TickKey, durable.Receipt.TickKey);
        Assert.Equal("healed", ReadActiveOrTerminalRecoveryWound(fixture.FileSystem, fixture.WoundId)["lifecycle"]!.GetValue<string>());
        var generations = durable.Stages.SelectMany(stage => stage.BeforeWound.Consequences.OwnedEffectSources.RootBindings)
            .Select(root => root.EffectId).Distinct(StringComparer.Ordinal).ToArray();
        Assert.Equal(4, generations.Length);
        using var identityDocument = JsonDocument.Parse(Assert.IsType<string>(
            fixture.FileSystem.ReadFileSync(EffectIdentityState.StatePath)));
        var identities = EffectIdentityState.Parse(identityDocument.RootElement, EffectIdentityState.StatePath);
        Assert.Empty(identities.Issues);
        foreach (var effectId in generations)
        {
            Assert.True(identities.State!.TryGetEntry(effectId, out var entry));
            Assert.DoesNotContain(entry.State, new[] { "active", "suspended" });
            Assert.Equal("create", entry.Transitions[0].Kind);
            Assert.Single(entry.Transitions, transition => transition.Kind == "expire" &&
                transition.Turn == fixture.Binding.Turn && transition.SourceEffectIds.Contains(effectId));
            if (effectId != generations[0])
                Assert.Equal(fixture.Binding.Turn, entry.CreatedAtTurn);
        }
        fixture.AssertCarrierIdentityHistoryAgreement();
        fixture.PrepareFreshContinuationTurn(45, "all_tiers_same_minute_replay", 145);
        fixture.RestartForReplay();
        var replayTree = fixture.CaptureCanonicalTreeBytes();
        AssertExactReplay(InvokePlan(fixture), receipt);
        fixture.AssertCanonicalTreeBytesUnchanged(replayTree);
    }

    private sealed partial class Fixture
    {
        /// <summary>
        /// Publishes real treatment at the previous recovery minute and exports
        /// the following accepted turn under the new stabilization epoch.
        /// </summary>
        internal void PublishSameMinuteStabilization()
        {
            PrepareFreshContinuationTurn(45, "stabilization_after_blocked_check", 110);
            FileSystemManager.CanonicalWriteLease? treatmentLease = Lease;
            ResolveAndPublishStabilization(FileSystem, ref treatmentLease, WoundId);
            Lease = Assert.IsType<FileSystemManager.CanonicalWriteLease>(treatmentLease);
            PrepareFreshContinuationTurn(46, "recovery_after_same_minute_stabilization", 110);
        }
    }
}
