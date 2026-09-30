using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Verifies closed durable evidence, original receipt replay and exact-epoch interval consumption.
/// </summary>
public sealed class MortalWoundRecoveryPersistenceTests
{
    /// <summary>
    /// Checks that natural cadence consumption leaves the original schedule unchanged.
    /// </summary>
    /// <param name="minute">
    /// The signed evaluation minute.
    /// </param>
    /// <param name="consumed">
    /// The exact epoch ordinal already consumed.
    /// </param>
    /// <param name="total">
    /// The expected total elapsed ordinal.
    /// </param>
    /// <param name="delta">
    /// The expected newly unconsumed interval count.
    /// </param>
    /// <param name="next">
    /// The expected next original-schedule due minute.
    /// </param>
    [Theory]
    [InlineData(135L, 0L, 3L, 3L, 140L)]
    [InlineData(136L, 3L, 3L, 0L, 140L)]
    [InlineData(140L, 3L, 4L, 1L, 150L)]
    [InlineData(99L, 0L, 0L, 0L, 110L)]
    public void RecoveryCadences_SubtractConsumptionWithoutMovingTheEpoch(
        long minute, long consumed, long total, long delta, long next)
    {
        var schedule = MortalWoundRecoveryCadence.Evaluate(100, 10, minute, consumed, false);

        Assert.Equal(total, schedule.ElapsedTotal);
        Assert.Equal(delta, schedule.NewIntervals);
        Assert.Equal(next, schedule.NextDueMinute);
    }

    /// <summary>
    /// Keeps the earliest unconsumed natural interval due while recovery is blocked,
    /// including an epoch whose earlier intervals were already consumed.
    /// </summary>
    /// <param name="minute">
    /// The signed evaluation minute.
    /// </param>
    /// <param name="consumed">
    /// The exact epoch ordinal already consumed before blocking.
    /// </param>
    /// <param name="total">
    /// The expected total elapsed ordinal, independent of the blocked state.
    /// </param>
    /// <param name="delta">
    /// The expected unconsumed interval count retained for a later eligible evaluation.
    /// </param>
    /// <param name="next">
    /// The first unconsumed minute on the original epoch schedule.
    /// </param>
    [Theory]
    [InlineData(110L, 0L, 1L, 1L, 110L)]
    [InlineData(135L, 0L, 3L, 3L, 110L)]
    [InlineData(155L, 0L, 5L, 5L, 110L)]
    [InlineData(135L, 3L, 3L, 0L, 140L)]
    [InlineData(140L, 3L, 4L, 1L, 140L)]
    [InlineData(155L, 3L, 5L, 2L, 140L)]
    public void BlockedRecoveryCadences_RetainTheFirstUnconsumedInterval(
        long minute, long consumed, long total, long delta, long next)
    {
        var schedule = MortalWoundRecoveryCadence.Evaluate(100, 10, minute, consumed, false,
            advancePastElapsed: false);

        Assert.Equal(total, schedule.ElapsedTotal);
        Assert.Equal(delta, schedule.NewIntervals);
        Assert.Equal(next, schedule.NextDueMinute);
    }

    /// <summary>
    /// Avoids overflow from an unused advanced due minute while still rejecting
    /// a consumed ordinal that the original epoch has not reached.
    /// </summary>
    [Fact]
    public void BlockedRecoveryCadences_ValidateConsumptionWithoutCalculatingAnUnusedNextMinute()
    {
        Assert.Throws<OverflowException>(() =>
            MortalWoundRecoveryCadence.Evaluate(long.MaxValue - 20, 10, long.MaxValue, 0, false));
        var schedule = MortalWoundRecoveryCadence.Evaluate(long.MaxValue - 20, 10,
            long.MaxValue, 0, false, advancePastElapsed: false);

        Assert.Equal(2, schedule.ElapsedTotal);
        Assert.Equal(2, schedule.NewIntervals);
        Assert.Equal(long.MaxValue - 10, schedule.NextDueMinute);
        Assert.Throws<InvalidDataException>(() =>
            MortalWoundRecoveryCadence.Evaluate(100, 10, 135, 4, false, advancePastElapsed: false));
    }

    /// <summary>
    /// Reconstructs blocked source math with no natural progress or skipped due
    /// interval, using the retained ordinal rather than the current elapsed total.
    /// </summary>
    /// <param name="epochMinute">
    /// The unchanged canonical recovery anchor minute.
    /// </param>
    /// <param name="minute">
    /// The detached signed minute of the evaluation.
    /// </param>
    /// <param name="consumed">
    /// The previously consumed ordinal belonging to this exact epoch.
    /// </param>
    /// <param name="next">
    /// The expected first unconsumed due minute.
    /// </param>
    [Theory]
    [InlineData(100L, 155L, 0L, 110L)]
    [InlineData(100L, 155L, 3L, 140L)]
    [InlineData(long.MaxValue - 20, long.MaxValue, 0L, long.MaxValue - 10)]
    public void BlockedResolution_RetainsUnconsumedScheduleAndEmitsNoProgress(
        long epochMinute, long minute, long consumed, long next)
    {
        var evidence = CreateEvidence();
        var anchor = evidence.SourceWound.Recovery.RecoveryAnchor! with { AnchorMinute = epochMinute };
        var wound = evidence.SourceWound with
        {
            Recovery = evidence.SourceWound.Recovery with
            {
                Mode = "requires_stabilization", Blockers = new[] { "not_stabilized" }, RecoveryAnchor = anchor
            }
        };
        var source = MortalWoundRecoveryPersistedResult.SealSourceEvidence(
            evidence.Source with { CurrentGameMinute = minute }, evidence.Binding, wound);

        var planned = MortalWoundRecoveryPlanner.ReconstructResolution(source, evidence.Binding, wound, consumed, 0);

        Assert.Equal(MortalWoundRecoveryPlanningDisposition.Resolved, planned.Disposition);
        Assert.Empty(planned.Issues);
        var resolution = Assert.IsType<MortalWoundRecoveryResolution>(planned.Resolution);
        Assert.Equal(MortalWoundRecoveryDisposition.BlockedNotStabilized, resolution.RecoveryDisposition);
        Assert.Equal(0, resolution.ElapsedCadences);
        Assert.Equal(next, resolution.NextRecoveryAnchorMinute);
        Assert.Equal(epochMinute + 10, resolution.CadenceDueMinute);
        Assert.Empty(resolution.TransitionIntents);
        Assert.Null(resolution.DeathHandoff);
        Assert.Equal(anchor, wound.Recovery.RecoveryAnchor);
    }

    /// <summary>
    /// Checks inclusive condition cadence boundaries and full elapsed-ordinal consumption.
    /// </summary>
    /// <param name="minute">
    /// The signed evaluation minute.
    /// </param>
    /// <param name="consumed">
    /// The exact condition epoch ordinal already consumed.
    /// </param>
    /// <param name="total">
    /// The expected total elapsed ordinal.
    /// </param>
    /// <param name="delta">
    /// The expected newly unconsumed interval count.
    /// </param>
    /// <param name="next">
    /// The expected next due minute on the original condition schedule.
    /// </param>
    [Theory]
    [InlineData(109L, 0L, 0L, 0L, 110L)]
    [InlineData(110L, 0L, 1L, 1L, 120L)]
    [InlineData(135L, 0L, 3L, 3L, 140L)]
    [InlineData(136L, 3L, 3L, 0L, 140L)]
    public void DeteriorationCadences_IncludeTheGraceDeadlineAndConsumeAllElapsedIntervals(
        long minute, long consumed, long total, long delta, long next)
    {
        var schedule = MortalWoundRecoveryCadence.Evaluate(110, 10, minute, consumed, true);

        Assert.Equal(total, schedule.ElapsedTotal);
        Assert.Equal(delta, schedule.NewIntervals);
        Assert.Equal(next, schedule.NextDueMinute);
    }

    /// <summary>
    /// Checks rejection of impossible consumed ordinals and checked schedule overflow.
    /// </summary>
    [Fact]
    public void Cadences_RejectFutureConsumptionAndCheckedOverflow()
    {
        Assert.Throws<InvalidDataException>(() =>
            MortalWoundRecoveryCadence.Evaluate(100, 10, 135, 4, false));
        Assert.Throws<OverflowException>(() =>
            MortalWoundRecoveryCadence.Evaluate(long.MaxValue - 1, 10, long.MaxValue, 0, false));
    }

    /// <summary>
    /// Checks that an outer result seal cannot conceal changed source, math, receipt or stage evidence.
    /// </summary>
    /// <param name="mutation">
    /// The evidence field changed while retaining an otherwise closed result.
    /// </param>
    [Theory]
    [InlineData("version")]
    [InlineData("source_minute")]
    [InlineData("source_wound")]
    [InlineData("resolution_elapsed")]
    [InlineData("receipt")]
    [InlineData("stage_source")]
    [InlineData("consumption")]
    [InlineData("unknown_nested")]
    [InlineData("enum_number")]
    [InlineData("enum_case")]
    public void Codec_RejectsChangedEvidenceEvenWhenTheOuterResultIsResealed(string mutation)
    {
        var canonical = CreateEvidence().ToCanonicalJson();
        switch (mutation)
        {
            case "version": canonical["schemaVersion"] = 2; break;
            case "source_minute": canonical["source"]!["currentGameMinute"] = 136; break;
            case "source_wound": canonical["sourceWound"]!["recovery"]!["cadence"] = 11; break;
            case "resolution_elapsed": canonical["resolution"]!["elapsedCadences"] = 4; break;
            case "receipt": canonical["receipt"]!["tickKey"] = "wound_recovery_tick_forged"; break;
            case "stage_source": canonical["stages"]![0]!["afterWound"]!["recovery"]!["currentStepProgress"] = 99; break;
            case "consumption": canonical["recoveryConsumption"]!["consumedBefore"] = 1; break;
            case "unknown_nested": canonical["resolution"]!["callerProof"] = true; break;
            case "enum_number": canonical["resolution"]!["recoveryDisposition"] = 1; break;
            case "enum_case": canonical["resolution"]!["recoveryDisposition"] = "progressed"; break;
        }
        canonical["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(canonical);
        var issues = new List<ValidationIssue>();

        var parsed = MortalWoundRecoveryPersistedResult.Parse(
            JsonSerializer.SerializeToElement(canonical), "result", issues);

        Assert.Null(parsed);
        Assert.NotEmpty(issues);
    }

    /// <summary>
    /// Checks immutable canonical round trips and duplicate-property rejection before object materialization.
    /// </summary>
    [Fact]
    public void Codec_RoundTripsDetachedEvidenceAndRejectsNestedDuplicateProperties()
    {
        var result = CreateEvidence();
        var canonical = result.ToCanonicalJson();
        var issues = new List<ValidationIssue>();
        var parsed = MortalWoundRecoveryPersistedResult.Parse(
            JsonSerializer.SerializeToElement(canonical), "result", issues);
        Assert.NotNull(parsed);
        Assert.Empty(issues);
        Assert.Equal(result.Receipt, parsed.Receipt);
        canonical["sourceWound"]!["recovery"]!["cadence"] = 999;
        Assert.Equal(10, result.SourceWound.Recovery.Cadence);

        var duplicate = result.ToCanonicalJson().ToJsonString().Replace(
            "\"consumedBefore\":0", "\"consumedBefore\":0,\"consumedBefore\":0", StringComparison.Ordinal);
        using var document = JsonDocument.Parse(duplicate);
        Assert.Null(MortalWoundRecoveryPersistedResult.Parse(document.RootElement, "result", issues));
        Assert.Contains(issues, issue => issue.Code == "mortal_wound_recovery_duplicate_property");
    }

    /// <summary>
    /// Checks original receipt replay, exact source history and independent epoch consumption.
    /// </summary>
    [Fact]
    public void History_ReconstructsTheOriginalPrefixAndReturnsTheOriginalSameMinuteReceipt()
    {
        var result = CreateEvidence();
        var history = CreateHistory(result);
        var parsed = WoundHistoryState.Parse(history.ToJsonString(), "history");

        Assert.True(parsed.IsValid, string.Join("; ", parsed.Issues.Select(issue => issue.Code + ":" + issue.Actual)));
        Assert.True(parsed.State!.TryResolveRecoveryReplay(result.Stages[^1].AfterWound, 135,
            out var receipt, out var issues));
        Assert.Empty(issues);
        Assert.Equal(result.Receipt, receipt);
        Assert.False(parsed.State.TryResolveRecoveryReplay(result.Stages[^1].AfterWound, 136, out receipt, out issues));
        Assert.Null(receipt);
        Assert.Empty(issues);
        Assert.Equal((3L, 0L), parsed.State.GetRecoveryConsumption(result.Stages[^1].AfterWound));
        var newEpoch = result.Stages[^1].AfterWound with
        {
            Recovery = result.Stages[^1].AfterWound.Recovery with
            {
                RecoveryAnchor = new WoundRecoveryAnchor("stabilization", 100, "wound_transition_stabilize_new")
            }
        };
        Assert.Equal((0L, 0L), parsed.State.GetRecoveryConsumption(newEpoch));
    }

    /// <summary>
    /// Checks that complete durable evidence must agree with its surrounding row.
    /// </summary>
    /// <param name="field">
    /// The original outer coordinate replaced by a different valid-shaped value.
    /// </param>
    [Theory]
    [InlineData("sourceFingerprint")]
    [InlineData("eventRef")]
    [InlineData("cycleKey")]
    [InlineData("outputFingerprint")]
    public void History_RejectsOuterRowCoordinatesThatDisagreeWithTheDurableResult(string field)
    {
        var history = CreateHistory(CreateEvidence());
        history["transitions"]![1]![field] = field.EndsWith("Fingerprint", StringComparison.Ordinal)
            ? Fingerprint("changed") : "changed_coordinate";

        Assert.False(WoundHistoryState.Parse(history.ToJsonString(), "history").IsValid);
    }

    /// <summary>
    /// Checks complete prefix seals beyond the selected wound semantic chain.
    /// </summary>
    [Fact]
    public void History_RejectsAChangedOriginalPrefixEvenWhenTheWoundChainStillAgrees()
    {
        var history = CreateHistory(CreateEvidence());
        history["transitions"]![0]!["sourceFingerprint"] = Fingerprint("changed_create_source");

        var parsed = WoundHistoryState.Parse(history.ToJsonString(), "history");

        Assert.False(parsed.IsValid);
        Assert.Contains(parsed.Issues, issue => issue.Code == "wound_history_recovery_prefix_mismatch");
    }

    /// <summary>
    /// Preserves an original creation epoch across an intervening treatment while
    /// rejecting a treatment row that has no sealed evidence for allocating stabilization.
    /// </summary>
    /// <param name="epoch">
    /// The retained or substituted epoch identity in the complete recovery source.
    /// </param>
    /// <param name="valid">
    /// Whether the complete history proves that exact epoch allocation.
    /// </param>
    [Theory]
    [InlineData("creation", true)]
    [InlineData("unsealed_stabilization", false)]
    [InlineData("unknown_kind", false)]
    [InlineData("wrong_transition", false)]
    public void History_RequiresExactEpochAllocationEvidenceAfterInterveningTreatment(string epoch, bool valid)
    {
        var original = CreateEvidence().SourceWound;
        var anchor = epoch switch
        {
            "unsealed_stabilization" => new WoundRecoveryAnchor("stabilization", 100, "wound_transition_treat_001"),
            "unknown_kind" => new WoundRecoveryAnchor("invented_epoch", 100, "wound_transition_treat_001"),
            "wrong_transition" => new WoundRecoveryAnchor("creation", 100, "wound_transition_treat_001"),
            _ => original.Recovery.RecoveryAnchor!
        };
        var sourceWound = original with
        {
            Recovery = original.Recovery with { RecoveryAnchor = anchor },
            LastTransition = new WoundLastTransition("wound_transition_treat_001", 2, 42, "treat")
        };
        var prefix = CreatePrefix(original);
        var treatment = WoundContractTestData.CreateTransition("treat", 2, 2);
        treatment["transitionId"] = sourceWound.LastTransition.TransitionId;
        treatment["operationKey"] = "operation_intervening_treatment";
        treatment["beforeFingerprint"] = WoundIdentityState.ComputeSemanticFingerprint(original);
        treatment["afterFingerprint"] = WoundIdentityState.ComputeSemanticFingerprint(sourceWound);
        treatment["sourceFingerprint"] = Fingerprint("intervening_treatment_source");
        treatment["attemptId"] = null;
        treatment["outputFingerprint"] = WoundHistoryState.ComputeOutputFingerprint(
            treatment["operationKey"]!.GetValue<string>(), treatment["eventRef"]!.GetValue<string>(),
            treatment["readableSummary"]!.GetValue<string>());
        prefix["transitions"]!.AsArray().Add(treatment);
        prefix["nextOrdinal"] = 3;
        var result = CreateEvidence(sourceWound, prefix);

        var parsed = WoundHistoryState.Parse(CreateHistory(result, prefix).ToJsonString(), "history");

        Assert.Equal(valid, parsed.IsValid);
        if (valid)
            Assert.Equal((3L, 0L), parsed.State!.GetRecoveryConsumption(result.Stages[^1].AfterWound));
        else
            Assert.Contains(parsed.Issues, issue => issue.Code == "wound_history_recovery_epoch_mismatch");
    }

    /// <summary>
    /// Builds detached codec-only evidence from complete strict canonical test data.
    /// </summary>
    /// <param name="sourceWound">
    /// The complete original source, or null to use the default creation source.
    /// </param>
    /// <param name="originalPrefix">
    /// The complete source history, or null to use the default one-row creation prefix.
    /// </param>
    /// <returns>
    /// A verified original comparison result without a live registry continuation or publication authority.
    /// </returns>
    private static MortalWoundRecoveryPersistedResult CreateEvidence(
        WoundMaterializationEnvelope? sourceWound = null, JsonObject? originalPrefix = null)
    {
        var json = WoundContractTestData.CreateActiveWound();
        json["recovery"]!["mode"] = "progressive";
        json["recovery"]!["clockKind"] = "world_time.currentTimeInMinutes";
        json["recovery"]!["cadence"] = 10;
        json["recovery"]!["currentStepProgress"] = 0;
        json["recovery"]!["currentStepThreshold"] = 100;
        json["recovery"]!["blockers"] = new JsonArray();
        json["recovery"]!["deteriorationPolicy"] = null;
        json["recovery"]!["recoveryAnchor"] = new JsonObject
        {
            ["anchorKind"] = "creation", ["anchorMinute"] = 100,
            ["anchorTransitionId"] = "wound_transition_test_001"
        };
        var wound = sourceWound ?? WoundMaterializationContract.Parse(json.ToJsonString(), "wound").Wound!;
        Assert.NotNull(wound);
        var prefix = originalPrefix ?? CreatePrefix(wound);
        var events = WoundAcceptedEventAuthorityComposer.ComposeDefaultAcceptedTurn("session", "request", "snapshot", 43).Events;
        var binding = new WoundAcceptedTurnBinding("session", "request", "snapshot", "mortal_world", 43,
            events, WoundAcceptedEventSetFingerprint.Compute(events));
        var context = new MortalWoundTreatmentAuthority.Context(1, "mortal_world", "player", "player_current", "player", "player_current", "location_test");
        var source = new MortalWoundRecoverySourceEvidence("generation", "", context, "", new[] { 10 },
            "game_state/player/wounds.json.activeWounds[0]", WoundIdentityState.ComputeSemanticFingerprint(wound),
            HistoryFingerprint(prefix), prefix["nextOrdinal"]!.GetValue<int>(), Fingerprint("identity"), "", 135,
            Fingerprint("effects"), Fingerprint("items"), Fingerprint("actors"), Fingerprint("player"),
            Fingerprint("npc"), "", Fingerprint("requirements"), "");
        source = MortalWoundRecoveryPersistedResult.SealSourceEvidence(source, binding, wound);
        var resolution = MortalWoundRecoveryPlanner.ReconstructResolution(source, binding, wound, 0, 0).Resolution!;
        var after = wound with
        {
            Recovery = wound.Recovery with { CurrentStepProgress = 3, LastTickKey = resolution.TickKey },
            LastTransition = new WoundLastTransition("wound_transition_recover_001", wound.LastTransition.Ordinal + 1, 43, "recover")
        };
        var stage = new MortalWoundRecoveryPublishedStage("wound_transition_recover_001", "recover",
            source.WoundFingerprint, WoundIdentityState.ComputeSemanticFingerprint(after), false, 43,
            events[0].EventRef, "operation_recovery", wound, after);
        return MortalWoundRecoveryPersistedResult.CreateComparisonEvidence(source, binding, wound,
            resolution, new[] { stage }, 0, 0);
    }

    /// <summary>
    /// Builds a complete canonical creation prefix for the exact source wound.
    /// </summary>
    /// <param name="wound">
    /// The canonical source wound before recovery.
    /// </param>
    /// <returns>
    /// A strict one-row original history prefix.
    /// </returns>
    private static JsonObject CreatePrefix(WoundMaterializationEnvelope wound)
    {
        var row = WoundContractTestData.CreateTransition();
        row["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint(wound.WoundId);
        row["afterFingerprint"] = WoundIdentityState.ComputeSemanticFingerprint(wound);
        row["sourceFingerprint"] = Fingerprint("original_create_source");
        row["attemptId"] = null;
        row["outputFingerprint"] = WoundHistoryState.ComputeOutputFingerprint(
            row["operationKey"]!.GetValue<string>(), row["eventRef"]!.GetValue<string>(), row["readableSummary"]!.GetValue<string>());
        return WoundContractTestData.CreateHistory(row);
    }

    /// <summary>
    /// Computes the exact original complete-prefix seal used by accepted-state admission.
    /// </summary>
    /// <param name="prefix">
    /// The complete original history before the recovery evaluation.
    /// </param>
    /// <returns>
    /// The shared-writer seal of the strictly parsed canonical prefix.
    /// </returns>
    private static string HistoryFingerprint(JsonObject prefix)
    {
        var parsed = WoundHistoryState.Parse(prefix.ToJsonString(), "prefix");
        Assert.True(parsed.IsValid);
        return WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "mortal_wound_treatment_history", "1", WoundHistoryState.SerializeCanonical(parsed.State!)
        });
    }

    /// <summary>
    /// Builds complete outer history agreeing with one original durable evaluation.
    /// </summary>
    /// <param name="result">
    /// The strict detached original recovery result.
    /// </param>
    /// <param name="originalPrefix">
    /// The complete original source history, or null to use the default creation prefix.
    /// </param>
    /// <returns>
    /// The complete canonical history retaining one primary result and receipt.
    /// </returns>
    private static JsonObject CreateHistory(MortalWoundRecoveryPersistedResult result, JsonObject? originalPrefix = null)
    {
        var history = originalPrefix?.DeepClone().AsObject() ?? CreatePrefix(result.SourceWound);
        var stage = result.Stages[0];
        var row = WoundContractTestData.CreateTransition("recover", result.Source.HistoryNextOrdinal,
            stage.AfterWound.LastTransition.Ordinal);
        row["transitionId"] = stage.TransitionId;
        row["turn"] = stage.Turn;
        row["eventRef"] = stage.EventRef;
        row["operationKey"] = stage.OperationKey;
        row["beforeFingerprint"] = stage.BeforeFingerprint;
        row["afterFingerprint"] = stage.AfterFingerprint;
        row["sourceFingerprint"] = result.Resolution.AuthorityFingerprint;
        row["attemptId"] = null;
        row["cycleKey"] = stage.AfterWound.Recovery.LastTickKey;
        row["readableSummary"] = WoundAcceptedTurnPlanner.RecoveryPublicationSummary;
        row["outputFingerprint"] = WoundHistoryState.ComputeOutputFingerprint(
            stage.OperationKey, stage.EventRef, WoundAcceptedTurnPlanner.RecoveryPublicationSummary);
        row["transitionResult"] = result.ToCanonicalJson();
        history["transitions"]!.AsArray().Add(row);
        history["nextOrdinal"] = result.Source.HistoryNextOrdinal + 1;
        return history;
    }

    /// <summary>
    /// Computes a deterministic component seal for detached pure codec evidence.
    /// </summary>
    /// <param name="value">
    /// The exact component label used by the test fixture.
    /// </param>
    /// <returns>
    /// A shared canonical-writer fingerprint; this fixture grants no publication authority.
    /// </returns>
    private static string Fingerprint(string value) => WoundAcceptedTurnFingerprintWriter.Compute(new[] { value });
}
