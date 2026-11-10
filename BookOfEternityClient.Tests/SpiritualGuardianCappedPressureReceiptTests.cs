using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Detached positive capped-v5 schema1 receipt DATA and separate immutable-prefix/History/log joins, never live C3 authority.</summary>
/// <param name="output">Complete per-test input, parser, join and comparison observations.</param>
public sealed class SpiritualGuardianCappedPressureReceiptTests(ITestOutputHelper output)
{
    /// <summary>Preserves complete prior lifecycle/injury rows and appends one capped v5 source with an explicit none decision despite equal roots.</summary>
    /// <param name="rank">Independent complete retained guardianIII/IV parent.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void GuardianCapped_ReceiptHistoryAndLogRemainExact(int rank)
    {
        var frame = CreateBaseline(rank);
        var rootBefore = frame.Root.ToJsonString();
        var canonical = SpiritualWoundOpportunityReceiptState.SerializeCanonical(frame.State);
        Assert.Equal(1, frame.Root["schemaVersion"]!.GetValue<int>());
        Assert.False(frame.Root.ContainsKey("strainComparisons"));
        Assert.False(frame.Root.ContainsKey("healingSources"));
        Assert.Equal((2, 1, 2, 2), (frame.Root["instances"]!.AsArray().Count, frame.Root["closures"]!.AsArray().Count,
            frame.Root["sources"]!.AsArray().Count, frame.Root["decisions"]!.AsArray().Count));
        AssertHistoricalPrefixes(frame.Before, frame.Root);
        var decision = frame.Root["decisions"]![1]!;
        Assert.Equal("none", decision["decision"]!.GetValue<string>());
        foreach (var field in new[] { "selectedSeverityRank", "woundId", "transitionId" }) Assert.Null(decision[field]);
        Assert.True(JsonNode.DeepEquals(frame.Fixture.Source, frame.Root["sources"]![1]!["witness"]));
        Assert.True(JsonNode.DeepEquals(frame.Root["sources"]![1]!["witness"], decision["sourceWitness"]));
        AssertComparisonAndRaw(frame);
        var creation = Assert.Single(frame.History.ResolveWoundTransitions(frame.Comparison.ParentWound.WoundId));
        Assert.True(frame.State.CoversOriginalCreation(frame.Comparison.ParentWound.WoundId,
            Assert.IsType<string>(frame.Comparison.ParentWound.Origin.OpportunityId), creation));
        var recorded = frame.State.FindRecordedConflictSideWound(frame.InstanceId, "opposition");
        Assert.Empty(recorded.Issues); Assert.Null(recorded.Wound);
        foreach (var turn in new[] { 46, 47 })
            Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
                frame.State.MatchRetainedPressureHistory("chaos_sea", frame.Active, turn));
        Assert.Equal("exact_replay", SpiritualWoundOpportunityReceiptState.PlanAppend(frame.State, ReadReceipt(frame.Root, "exact_replay")).Disposition);
        var detached = Assert.IsType<JsonObject>(frame.State.ReadRetainedStrainOrdinaryAfter("chaos_sea", ConflictId(frame), frame.Fixture.Actual, true));
        detached["oppositionSideStrain"] = "clear";
        Assert.True(JsonNode.DeepEquals(frame.Fixture.Ordinary["after"],
            frame.State.ReadRetainedStrainOrdinaryAfter("chaos_sea", ConflictId(frame), frame.Fixture.Actual, true)));
        Assert.Equal(rootBefore, frame.Root.ToJsonString());
        // A later mutation of mutable input DATA must not change either parsed/appended immutable state.
        frame.Root["sources"]![1]!["witness"]!["maximumSeverityRank"] = 4;
        Assert.Equal(canonical, SpiritualWoundOpportunityReceiptState.SerializeCanonical(frame.State));
        Assert.Equal(canonical, SpiritualWoundOpportunityReceiptState.SerializeCanonical(frame.Appended));
        Assert.Equal("exact_replay", SpiritualWoundOpportunityReceiptState.PlanAppend(frame.State, frame.State).Disposition);
    }

    /// <summary>Reseals dependent source/decision copies after exactly one intrinsic comparison contradiction, following a fully passing baseline.</summary>
    /// <param name="change">One complete-ordinary, declared arithmetic or canonical pointer contradiction.</param>
    [Theory]
    [InlineData("ordinary-sibling")] [InlineData("ordinary-pressure")]
    [InlineData("ordinary-maximum")] [InlineData("raw-pointer")]
    public void GuardianCapped_ReceiptRejectsResealedIntrinsicContradictions(string change)
    {
        var frame = CreateBaseline(4);
        var source = CurrentSource(frame.Root); var proof = source["strainPreimage"]!.AsObject();
        string cause;
        switch (change)
        {
            case "ordinary-sibling":
                var ordinary = SpiritualExistingPressureTestData.ReadOrdinary(source);
                ordinary["summary"] = "Иное описание только обычного давления.";
                RebindOrdinary(frame, source, ordinary, ExpectedCalculation());
                cause = "Only the selected guardian after-strain may differ.";
                break;
            case "ordinary-pressure": proof["ordinaryCalculation"]!["traumaPressure"] = 21L; cause = "Declared ordinary guardian calculation differs."; break;
            case "ordinary-maximum": proof["ordinaryCalculation"]!["maximumSeverityRank"] = 4; cause = "Declared ordinary guardian calculation differs."; break;
            case "raw-pointer": proof["originalInput"]!["exchangePointer"] = "/activeConflict/exchangeLog/00"; cause = "Canonical direct-root exchange pointer required."; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        SpiritualStrainReceiptJoinTests.SealSourceAndDecision(frame.Root, 1);
        var before = frame.Root.ToJsonString(); SpiritualStrainPreimageData? leaked = null;
        var error = Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out leaked));
        Assert.Equal(cause, error.Message); Assert.Null(leaked);
        var direct = Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadIntrinsic(source, frame.Fixture.Actual));
        Assert.Equal(cause, direct.Message);
        var parsed = ParseReceipt(frame.Root, "intrinsic_contradiction_" + change);
        Assert.False(parsed.IsValid); Assert.Null(parsed.State);
        Assert.Equal("spiritual_wound_receipt_invalid_state", Assert.Single(parsed.Issues).Code);
        Assert.Equal(before, frame.Root.ToJsonString());
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_receipt_intrinsic_refusal", change,
            baselinePassed = true, cause, error = error.ToString(), directError = direct.ToString(), receipt = before }));
    }

    /// <summary>Otherwise valid DATA cannot rewrite either the accepted ordinary comparison or a complete prior decision prefix.</summary>
    /// <param name="change">One coherently resealed immutable-prefix violation.</param>
    [Theory]
    [InlineData("accepted-comparison")] [InlineData("prior-decision")]
    public void GuardianCapped_ReceiptRejectsChangedAcceptedPrefixes(string change)
    {
        var frame = CreateBaseline(4);
        var canonical = SpiritualWoundOpportunityReceiptState.SerializeCanonical(frame.State);
        var candidate = frame.Root.DeepClone().AsObject();
        if (change == "accepted-comparison")
        {
            var source = CurrentSource(candidate); var ordinary = SpiritualExistingPressureTestData.ReadOrdinary(source);
            ordinary["after"]!["oppositionSideStrain"] = "overwhelmed";
            var calculation = new SpiritualWoundCalculation(new(8, 0, 0, "strained", "overwhelmed", "hostile", 4), 1, 3, 1, 15, 2, 3, 4, true, 2);
            RebindOrdinary(frame, source, ordinary, calculation);
            SpiritualStrainReceiptJoinTests.SealSourceAndDecision(candidate, 1);
            var data = ReadSupportedSource(source, 4, calculation);
            Assert.True(JsonNode.DeepEquals(frame.Fixture.Actual, data.ReadActualExchange()));
            Assert.False(JsonNode.DeepEquals(frame.Fixture.Ordinary["after"], data.ReadOrdinaryAfter(frame.Fixture.Actual)));
        }
        else if (change == "prior-decision")
        {
            var decision = candidate["decisions"]![0]!.AsObject();
            decision["decisionId"] = "decision_guardian_capped_changed_prior";
            decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
        }
        else throw new ArgumentOutOfRangeException(nameof(change));
        var before = candidate.ToJsonString(); var changed = ReadReceipt(candidate, "valid_changed_prefix_" + change);
        Assert.True(changed.HasExactStrainHistory(frame.History));
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
            changed.MatchRetainedPressureHistory("chaos_sea", frame.Active, 47));
        var append = SpiritualWoundOpportunityReceiptState.PlanAppend(frame.State, changed);
        Assert.Equal("conflict", append.Disposition); Assert.Null(append.After);
        Assert.Equal(canonical, SpiritualWoundOpportunityReceiptState.SerializeCanonical(frame.State));
        Assert.Equal(before, candidate.ToJsonString());
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_receipt_immutable_prefix_refusal", change,
            baselinePassed = true, otherwiseValidParsedData = true, append.Disposition, beforeReceipt = canonical, candidate = before }));
    }

    /// <summary>Intrinsic/receipt success remains separate from exact unchanged parent History and complete accepted-log agreement.</summary>
    /// <param name="rank">Independent complete retained guardianIII/IV parent.</param>
    /// <param name="change">One otherwise valid complete-parent or accepted-log mutation.</param>
    [Theory]
    [InlineData(3, "parent-history")] [InlineData(4, "parent-history")]
    [InlineData(3, "accepted-log")] [InlineData(4, "accepted-log")]
    public void GuardianCapped_ReceiptParentAndAcceptedLogJoinsRefuse(int rank, string change)
    {
        var frame = CreateBaseline(rank); var historyBefore = frame.Fixture.Retained.History.ToJsonString();
        var candidate = frame.Root.DeepClone().AsObject(); var active = frame.Active.DeepClone().AsObject();
        if (change == "parent-history")
        {
            var source = CurrentSource(candidate); var parent = SpiritualExistingPressureTestData.ReadParent(source);
            var original = WoundMaterializationContract.Parse(parent.ToJsonString(), "cappedReceipt.parent");
            Assert.True(original.IsValid, JsonSerializer.Serialize(original.Issues));
            parent["display"]!["description"] = "Иное полное описание той же раны Хранителя.";
            var changed = WoundMaterializationContract.Parse(parent.ToJsonString(), "cappedReceipt.changedParent");
            Assert.True(changed.IsValid, JsonSerializer.Serialize(changed.Issues));
            Assert.NotEqual(WoundIdentityState.ComputeSemanticFingerprint(original.Wound!), WoundIdentityState.ComputeSemanticFingerprint(changed.Wound!));
            source["strainPreimage"]!["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(parent);
            SpiritualStrainReceiptJoinTests.SealSourceAndDecision(candidate, 1);
            ReadSupportedSource(source, rank);
            var state = ReadReceipt(candidate, "valid_parent_history_candidate");
            Assert.True(frame.State.HasExactStrainHistory(frame.History)); Assert.False(state.HasExactStrainHistory(frame.History));
            Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
                state.MatchRetainedPressureHistory("chaos_sea", active, 47));
        }
        else if (change == "accepted-log")
        {
            var actual = active["exchangeLog"]![0]!.AsObject();
            actual["incomingAction"]!["summary"] = "Иное вложенное описание того же входящего давления.";
            AssertOrdinaryRules(frame, actual);
            Assert.True(frame.State.HasExactStrainHistory(frame.History));
            foreach (var turn in new[] { 46, 47 })
                Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Mismatch,
                    frame.State.MatchRetainedPressureHistory("chaos_sea", active, turn));
            foreach (var allowSummaryDrift in new[] { false, true })
                Assert.Null(frame.State.ReadRetainedStrainOrdinaryAfter("chaos_sea", ConflictId(frame), actual, allowSummaryDrift));
            Assert.Equal(frame.Root.ToJsonString(), candidate.ToJsonString());
        }
        else throw new ArgumentOutOfRangeException(nameof(change));
        Assert.Equal(historyBefore, frame.Fixture.Retained.History.ToJsonString());
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_receipt_explicit_join_refusal", rank, change,
            baselinePassed = true, candidate, active, unchangedHistory = historyBefore }));
    }

    /// <summary>Well-formed raw hashes and semantic root equality cannot authenticate external raw bytes through a detached receipt parser.</summary>
    /// <param name="change">Changed external whitespace bytes or a coherently resealed opaque comparator.</param>
    [Theory]
    [InlineData("raw-bytes")] [InlineData("opaque-fingerprint")]
    public void GuardianCapped_ReceiptRawComparatorIsNotAuthority(string change)
    {
        var frame = CreateBaseline(4); var candidate = frame.Root.DeepClone().AsObject(); var source = CurrentSource(candidate);
        var raw = frame.Fixture.RawInput.ToArray();
        if (change == "raw-bytes") raw = [.. raw, (byte)' '];
        else if (change == "opaque-fingerprint")
        {
            source["strainPreimage"]!["originalInput"]!["contentFingerprint"] = "sha256:" + new string('f', 64);
            SpiritualStrainReceiptJoinTests.SealSourceAndDecision(candidate, 1);
        }
        else throw new ArgumentOutOfRangeException(nameof(change));
        var data = ReadSupportedSource(source, 4); var state = ReadReceipt(candidate, "opaque_raw_comparator_" + change);
        Assert.True(state.HasExactStrainHistory(frame.History));
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
            state.MatchRetainedPressureHistory("chaos_sea", frame.Active, 47));
        Assert.NotEqual(new CanonicalBeforeImage(true, raw).Fingerprint, data.OriginalInputFingerprint);
        Assert.True(JsonNode.DeepEquals(frame.Fixture.Conflict, SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(raw))));
        Assert.Equal(change == "raw-bytes" ? "exact_replay" : "conflict",
            SpiritualWoundOpportunityReceiptState.PlanAppend(frame.State, state).Disposition);
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_receipt_raw_data_boundary", change,
            baselinePassed = true, detachedParseValid = true, data.OriginalInputFingerprint,
            externalRawFingerprint = new CanonicalBeforeImage(true, raw).Fingerprint,
            scope = "Detached DATA cannot authenticate external original bytes or grant current/publication authority." }));
    }

    /// <summary>Builds and independently checks the complete positive premise before any corruption; no owner or signature is issued.</summary>
    /// <param name="rank">Independent complete parentIII/IV, fixed before DATA authoring.</param>
    private Frame CreateBaseline(int rank)
    {
        var fixture = SpiritualGuardianAttributionTestData.Create(rank: rank, contour: GuardianSingletonPressureContour.CappedBroken);
        var issues = SpiritualGuardianAttributionDataCheck.Check(fixture.Retained, fixture.Profiles, fixture.Conflict, rank);
        Assert.True(issues.Count == 0, JsonSerializer.Serialize(issues));
        var comparison = ReadSupportedSource(fixture.Source, rank);
        var before = fixture.Retained.Receipts.DeepClone().AsObject(); var baseline = ReadReceipt(before, "complete_prior_ledger");
        var historyRead = WoundHistoryState.Parse(fixture.Retained.History.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(historyRead.IsValid, JsonSerializer.Serialize(historyRead.Issues));
        var history = Assert.IsType<WoundHistoryState>(historyRead.State); Assert.True(baseline.HasExactStrainHistory(history));
        var evidence = fixture.Source["turnEvidence"]!;
        var resolved = SpiritualWoundFirstOfferInstanceResolver.Resolve(new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(before.ToJsonString())),
            new CanonicalBeforeImage(true, fixture.RawInput), "chaos_sea", evidence["sessionId"]!.GetValue<string>(),
            evidence["requestId"]!.GetValue<string>(), evidence["snapshotToken"]!.GetValue<string>(), 46);
        Assert.Empty(resolved.Issues); var instanceId = Assert.IsType<string>(resolved.InstanceId);
        var proposed = Assert.IsType<JsonObject>(resolved.ProposedRow);
        var skeleton = before.DeepClone().AsObject(); skeleton["instances"]!.AsArray().Add(proposed.DeepClone());
        skeleton["nextInstanceOrdinal"] = proposed["ordinal"]!.GetValue<int>() + 1;
        var skeletonState = ReadReceipt(skeleton, "schema1_instance_skeleton");
        Assert.Equal("appended", SpiritualWoundOpportunityReceiptState.PlanAppend(baseline, skeletonState).Disposition);
        var root = skeleton.DeepClone().AsObject(); var ordinal = root["nextSourceOrdinal"]!.GetValue<int>();
        Assert.Equal(ordinal, root["nextDecisionOrdinal"]!.GetValue<int>());
        root["sources"]!.AsArray().Add(new JsonObject { ["ordinal"] = ordinal, ["instanceId"] = instanceId,
            ["instanceSourceOrdinal"] = 1, ["witness"] = fixture.Source.DeepClone() });
        var key = SpiritualWoundSourceWitness.HarmEventKey(evidence["sessionId"]!.GetValue<string>(), evidence["requestId"]!.GetValue<string>(),
            evidence["snapshotToken"]!.GetValue<string>(), fixture.Source["conflictId"]!.GetValue<string>(), fixture.Source["exchangeId"]!.GetValue<string>(), "opposition");
        var decision = new JsonObject
        {
            ["ordinal"] = ordinal, ["decisionId"] = "decision_guardian_capped_data_46", ["instanceId"] = instanceId,
            ["sourceId"] = fixture.Source["sourceId"]!.DeepClone(), ["opportunityRef"] = "spiritual_wound_" + key,
            ["sourceWitness"] = fixture.Source.DeepClone(), ["binding"] = new JsonObject
            {
                ["sessionId"] = evidence["sessionId"]!.DeepClone(), ["requestId"] = evidence["requestId"]!.DeepClone(),
                ["snapshotToken"] = evidence["snapshotToken"]!.DeepClone(), ["turn"] = 46,
                ["continuationGeneration"] = 1, ["waveOrdinal"] = 0, ["sourceOrdinal"] = 0
            },
            ["decision"] = "none", ["selectedSeverityRank"] = null, ["woundId"] = null, ["transitionId"] = null,
            ["sourceFingerprint"] = fixture.Source["sourceFingerprint"]!.DeepClone(), ["decisionFingerprint"] = ""
        };
        decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
        root["decisions"]!.AsArray().Add(decision); root["nextSourceOrdinal"] = ordinal + 1; root["nextDecisionOrdinal"] = ordinal + 1;
        var state = ReadReceipt(root, "complete_capped_schema1_baseline");
        var append = SpiritualWoundOpportunityReceiptState.PlanAppend(baseline, state);
        Assert.Equal("appended", append.Disposition); var appended = Assert.IsType<SpiritualWoundOpportunityReceiptState>(append.After);
        Assert.Equal("appended", SpiritualWoundOpportunityReceiptState.PlanAppend(skeletonState, state).Disposition);
        Assert.Equal(SpiritualWoundOpportunityReceiptState.SerializeCanonical(state), SpiritualWoundOpportunityReceiptState.SerializeCanonical(appended));
        Assert.Equal("exact_replay", SpiritualWoundOpportunityReceiptState.PlanAppend(state, ReadReceipt(root, "baseline_exact_replay")).Disposition);
        Assert.True(state.HasExactStrainHistory(history)); Assert.True(state.HasRetainedPressureComparisons);
        var active = fixture.Conflict["activeConflict"]!.DeepClone().AsObject();
        active["exchangeLog"]![0] = fixture.Actual.DeepClone(); active["oppositionSideStrain"] = "broken";
        var frame = new Frame(fixture, before, root, state, appended, history, active, comparison, instanceId);
        AssertHistoricalPrefixes(before, root); AssertComparisonAndRaw(frame);
        foreach (var turn in new[] { 46, 47 })
            Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact, state.MatchRetainedPressureHistory("chaos_sea", active, turn));
        foreach (var drift in new[] { false, true })
            Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], state.ReadRetainedStrainOrdinaryAfter("chaos_sea", ConflictId(frame), fixture.Actual, drift)));
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_receipt_complete_baseline", rank,
            scope = "Detached DATA only; no genuine firstC2/none/coldC3/C4 authority or runtime qualification.",
            before, skeleton, receipt = root, history = fixture.Retained.History, fixture.Profiles,
            ordinaryRoot = fixture.Conflict, actual = fixture.Actual, rawOrdinaryBytesBase64 = Convert.ToBase64String(fixture.RawInput) }));
        return frame;
    }

    /// <summary>Checks an intrinsic complete v5 source without treating it as externally authenticated raw/current authority.</summary>
    /// <param name="source">Complete original or coherently resealed detached source DATA.</param>
    /// <param name="rank">Independent expected retained parent rank.</param>
    /// <param name="ordinary">Optional explicit lawful ordinaryIII arithmetic for the immutable-prefix control.</param>
    private static SpiritualStrainPreimageData ReadSupportedSource(JsonObject source, int rank, SpiritualWoundCalculation? ordinary = null)
    {
        var before = source.ToJsonString();
        Assert.Equal(ExpectedCalculation(), SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var comparison));
        var data = Assert.IsType<SpiritualStrainPreimageData>(comparison);
        Assert.Equal(5, data.Version); Assert.Single(data.Parents); Assert.Null(data.ParentSourceId);
        Assert.Equal((rank, "guardian", SpiritualGuardianAttributionTestData.GuardianId),
            (data.ParentWound.Severity.Rank, data.ParentWound.Owner.OwnerKind, data.ParentWound.Owner.OwnerId));
        Assert.Equal(ordinary ?? ExpectedCalculation(), data.OrdinaryCalculation); Assert.Equal(ExpectedCalculation(), data.ActualCalculation);
        Assert.Equal(before, source.ToJsonString()); return data;
    }

    /// <summary>Independently authors/validates a complete ordinary replacement and its raw comparator before resealing dependent receipt copies.</summary>
    /// <param name="frame">Already passing complete capped baseline.</param>
    /// <param name="source">Only the candidate source to update; no original accepted state is mutated.</param>
    /// <param name="ordinary">Complete independently authored replacement exchange.</param>
    /// <param name="calculation">Independent literal expected ordinary calculation.</param>
    private static void RebindOrdinary(Frame frame, JsonObject source, JsonObject ordinary, SpiritualWoundCalculation calculation)
    {
        AssertOrdinaryRules(frame, ordinary);
        var root = frame.Fixture.Conflict.DeepClone().AsObject(); root["activeConflict"]!["exchangeLog"]![0] = ordinary.DeepClone();
        root["activeConflict"]!["oppositionSideStrain"] = ordinary["after"]!["oppositionSideStrain"]!.DeepClone();
        var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + root.ToJsonString() + "\n")).ToArray();
        var fingerprint = new CanonicalBeforeImage(true, raw).Fingerprint;
        var comparisonFree = SpiritualGuardianAttributionTestData.CreateSource(ordinary, "capped_receipt_ordinary_session",
            "capped_receipt_ordinary_request", "capped_receipt_ordinary_snapshot", ConflictId(frame), fingerprint);
        Assert.False(comparisonFree.ContainsKey("strainPreimage"));
        Assert.Equal(calculation, SpiritualWoundSourceWitness.ValidateAndReadStrainData(comparisonFree, out var absent)); Assert.Null(absent);
        var proof = source["strainPreimage"]!; proof["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(ordinary);
        proof["originalInput"]!["contentFingerprint"] = fingerprint;
        proof["ordinaryCalculation"] = new JsonObject { ["traumaPressure"] = calculation.TraumaPressure, ["maximumSeverityRank"] = calculation.MaximumSeverityRank };
    }

    /// <summary>Uses unchanged real shape/action/dice/pressure rules for complete ordinary or log DATA.</summary>
    /// <param name="frame">Passing baseline supplying exact accepted dice DATA.</param>
    /// <param name="exchange">The complete unchanged or independently mutated exchange.</param>
    private static void AssertOrdinaryRules(Frame frame, JsonObject exchange)
    {
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(exchange, "cappedReceipt.ordinary", issues);
        ValidationService.ValidateSpiritualWoundSourceActionShape(exchange, "cappedReceipt.ordinary", issues);
        ValidationService.ValidateRetainedSpiritualDiceAudit(exchange, frame.Fixture.Source["turnEvidence"]!.AsObject(), issues);
        ValidationService.ValidateRetainedSpiritualExchangeRules(exchange, issues);
        Assert.True(issues.Count == 0, JsonSerializer.Serialize(issues));
    }

    /// <summary>Preserves all old source/decision/lifecycle nodes and complete source ancestry without subset selection.</summary>
    /// <param name="before">Complete retained prior receipt DATA.</param>
    /// <param name="next">Candidate after one current instance/source/none append.</param>
    private static void AssertHistoricalPrefixes(JsonObject before, JsonObject next)
    {
        foreach (var field in new[] { "instances", "closures", "sources", "decisions" })
            for (var index = 0; index < before[field]!.AsArray().Count; index++)
                Assert.True(JsonNode.DeepEquals(before[field]![index], next[field]![index]), field);
        Assert.Equal(before["closures"]!.ToJsonString(), next["closures"]!.ToJsonString());
        Assert.Equal(before["nextClosureOrdinal"]!.GetValue<int>(), next["nextClosureOrdinal"]!.GetValue<int>());
        foreach (var field in new[] { "nextInstanceOrdinal", "nextSourceOrdinal", "nextDecisionOrdinal" })
            Assert.Equal(before[field]!.GetValue<int>() + 1, next[field]!.GetValue<int>());
    }

    /// <summary>Empty selected strain differences cannot delete the complete ordinary/root/raw/pointer/parent comparison.</summary>
    /// <param name="frame">Complete already parsed capped comparison and receipt DATA.</param>
    private static void AssertComparisonAndRaw(Frame frame)
    {
        var fixture = frame.Fixture; var data = frame.Comparison;
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, fixture.Actual));
        var actualRoot = fixture.Conflict.DeepClone(); actualRoot["activeConflict"]!["exchangeLog"]![0] = fixture.Actual.DeepClone();
        actualRoot["activeConflict"]!["oppositionSideStrain"] = "broken";
        Assert.True(JsonNode.DeepEquals(fixture.Conflict, actualRoot));
        var changedStrainPaths = new List<string>();
        if (!JsonNode.DeepEquals(fixture.Ordinary["after"]!["oppositionSideStrain"], fixture.Actual["after"]!["oppositionSideStrain"]))
            changedStrainPaths.Add("/activeConflict/exchangeLog/0/after/oppositionSideStrain");
        if (!JsonNode.DeepEquals(fixture.Conflict["activeConflict"]!["oppositionSideStrain"], actualRoot["activeConflict"]!["oppositionSideStrain"]))
            changedStrainPaths.Add("/activeConflict/oppositionSideStrain");
        Assert.Empty(changedStrainPaths);
        Assert.NotNull(CurrentSource(frame.Root)["strainPreimage"]);
        Assert.True(fixture.RawInput.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        var fingerprint = new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint;
        Assert.NotEqual(fingerprint, new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(fixture.Conflict.ToJsonString())).Fingerprint);
        Assert.Equal(fingerprint, data.OriginalInputFingerprint); Assert.Equal("/activeConflict/exchangeLog/0", data.OriginalExchangePointer);
        var input = CurrentSource(frame.Root)["strainPreimage"]!["originalInput"]!;
        Assert.Equal((AfterlifeSpiritualConflictState.StatePath, true, fingerprint, "/activeConflict/exchangeLog/0"),
            (input["path"]!.GetValue<string>(), input["existed"]!.GetValue<bool>(), input["contentFingerprint"]!.GetValue<string>(), input["exchangePointer"]!.GetValue<string>()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, SpiritualExistingPressureTestData.ReadOrdinary(CurrentSource(frame.Root))));
        Assert.True(JsonNode.DeepEquals(Assert.Single(fixture.Retained.Parents), JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(data.ParentWound))));
        Assert.Equal(ExpectedCalculation(), data.OrdinaryCalculation); Assert.Equal(ExpectedCalculation(), data.ActualCalculation);
    }

    /// <summary>Strict parser result with all issues logged; no broad caught exception can turn an unexpected refusal into success.</summary>
    /// <param name="root">Complete immutable-input receipt candidate.</param>
    /// <param name="phase">Precise DATA consumer stage for durable observations.</param>
    private SpiritualWoundReceiptParseResult ParseReceipt(JsonObject root, string phase)
    {
        var before = root.ToJsonString(); var parsed = SpiritualWoundOpportunityReceiptState.Parse(before, SpiritualWoundOpportunityReceiptState.StatePath);
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_receipt_parse", stage = phase, parsed.IsValid, parsed.Issues, receiptJson = before }));
        Assert.Equal(before, root.ToJsonString()); return parsed;
    }

    /// <summary>Requires successful strict DATA parsing before a later independently named consumer is tested.</summary>
    /// <param name="root">Complete baseline or intrinsically valid mutation.</param>
    /// <param name="phase">Named successful parser prerequisite.</param>
    private SpiritualWoundOpportunityReceiptState ReadReceipt(JsonObject root, string phase)
    {
        var parsed = ParseReceipt(root, phase); Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues)); Assert.Empty(parsed.Issues);
        return Assert.IsType<SpiritualWoundOpportunityReceiptState>(parsed.State);
    }

    /// <summary>Selects the single current DATA source after the complete retained birth row.</summary>
    private static JsonObject CurrentSource(JsonObject root) => root["sources"]![1]!["witness"]!.AsObject();
    /// <summary>Reads this fixture's independently authored current display conflict identity.</summary>
    private static string ConflictId(Frame frame) => frame.Fixture.Source["conflictId"]!.GetValue<string>();
    /// <summary>Returns the independent literal capped calculation, never an oracle copied from production output.</summary>
    private static SpiritualWoundCalculation ExpectedCalculation() => new(new(8, 0, 0, "strained", "broken", "hostile", 4), 1, 4, 2, 20, 3, 4, 4, true, 3);
    /// <summary>Per-invocation detached DATA only; neither this record nor parsed states grant live source/publication authority.</summary>
    private sealed record Frame(SpiritualGuardianAttributionFixture Fixture, JsonObject Before, JsonObject Root,
        SpiritualWoundOpportunityReceiptState State, SpiritualWoundOpportunityReceiptState Appended, WoundHistoryState History,
        JsonObject Active, SpiritualStrainPreimageData Comparison, string InstanceId);
}
