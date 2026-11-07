using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Checks detached v5 receipt, parent History and retained-log DATA through their separate typed consumers, never a live owner.</summary>
public sealed class SpiritualGuardianAttributionReceiptTests(ITestOutputHelper output)
{
    /// <summary>A positive schema1 receipt retains one v5 declaration, its exact guardian parent anchor and the complete accepted actual exchange.</summary>
    /// <param name="rank">Independent expected rank of the complete III or IV guardian parent.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void GuardianReceipt_RetainsDeclarationParentHistoryAndOrdinary(int rank)
    {
        var (fixture, root, state, history, active) = CreateBaseline(rank);
        Assert.Equal(1, root["schemaVersion"]!.GetValue<int>());
        Assert.Single(root["sources"]!.AsArray());
        Assert.Equal("none", Assert.Single(root["decisions"]!.AsArray())!["decision"]!.GetValue<string>());
        Assert.True(state.HasExactStrainHistory(history));
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
            state.MatchRetainedPressureHistory("chaos_sea", active, 47));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"],
            state.ReadRetainedStrainOrdinaryAfter("chaos_sea", ConflictId(fixture), fixture.Actual, true)));
    }

    /// <summary>Resealed source/decision copies cannot hide a stripped or contradictory declaration inside a retained v5 receipt.</summary>
    /// <param name="change">One declaration-only payload mutation, with the raw ordinary comparator and both lexical digests kept coherent.</param>
    /// <param name="cause">The exact intrinsic declaration or complete-payload comparison guard reached before the containing parser refusal.</param>
    [Theory]
    [InlineData("actual-absent", "Guardian failed-action declaration required.")]
    [InlineData("ordinary-absent", "Only the selected guardian after-strain may differ.")]
    [InlineData("both-absent", "Guardian failed-action declaration required.")]
    [InlineData("both-foreign-actor", "Incoming guardian identity alias differs.")]
    public void GuardianReceipt_RejectsRetainedDeclarationMutation(string change, string cause)
    {
        var (fixture, root, _, _, _) = CreateBaseline();
        var baseline = root.ToJsonString();
        var source = root["sources"]![0]!["witness"]!.AsObject();
        var ordinary = SpiritualExistingPressureTestData.ReadOrdinary(source);
        var actual = SpiritualExistingPressureTestData.ReadActual(source);
        switch (change)
        {
            case "actual-absent": Assert.True(actual.Remove("spiritualWoundFailedAction")); break;
            case "ordinary-absent": Assert.True(ordinary.Remove("spiritualWoundFailedAction")); break;
            case "both-absent":
                Assert.True(actual.Remove("spiritualWoundFailedAction"));
                Assert.True(ordinary.Remove("spiritualWoundFailedAction"));
                break;
            case "both-foreign-actor":
                actual["spiritualWoundFailedAction"]!["actorId"] = "guardian_foreign";
                ordinary["spiritualWoundFailedAction"]!["actorId"] = "guardian_foreign";
                break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        source["exchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(actual);
        source["strainPreimage"]!["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(ordinary);
        var original = fixture.Conflict.DeepClone().AsObject();
        original["activeConflict"]!["exchangeLog"]![0] = ordinary.DeepClone();
        var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + original.ToJsonString() + "\n")).ToArray();
        source["strainPreimage"]!["originalInput"]!["contentFingerprint"] = new CanonicalBeforeImage(true, raw).Fingerprint;
        SpiritualStrainReceiptJoinTests.SealSourceAndDecision(root, 0);
        var before = root.ToJsonString();
        var actualBefore = actual.ToJsonString();
        var actionIssues = new List<ValidationIssue>();
        ValidationService.ValidateSpiritualWoundSourceActionShape(actual, "retainedSource.exchange", actionIssues);
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_receipt_actual_action_shape", change, issues = IssuesJson(actionIssues) }));
        if (change == "both-foreign-actor")
        {
            var issue = Assert.Single(actionIssues);
            Assert.Equal(IssueSeverity.Error, issue.Severity);
            Assert.Equal("retainedSource.exchange.spiritualWoundFailedAction", issue.FilePath);
            Assert.Equal("spiritual_wound_failed_action_invalid", issue.Code);
            Assert.Equal(cause, issue.Actual);
        }
        else Assert.Empty(actionIssues);
        Assert.Equal(actualBefore, actual.ToJsonString());
        SpiritualStrainPreimageData? leaked = null;
        var error = Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out leaked));
        Assert.Equal(change == "both-foreign-actor" ? "Invalid retained exchange payload." : cause, error.Message);
        Assert.Null(leaked);
        Assert.Equal(before, root.ToJsonString());
        var result = ParseReceipt(root, "guardian_receipt_" + change);
        Assert.False(result.IsValid); Assert.Null(result.State);
        Assert.Equal("spiritual_wound_receipt_invalid_state", Assert.Single(result.Issues).Code);
        Assert.Equal(before, root.ToJsonString());

        // Restore only the changed payload/comparator fields. Byte-identical accepted DATA rules out an unrelated join defect.
        source["exchangeJsonBase64"] = fixture.Source["exchangeJsonBase64"]!.DeepClone();
        source["strainPreimage"]!["ordinaryExchangeJsonBase64"] = fixture.Source["strainPreimage"]!["ordinaryExchangeJsonBase64"]!.DeepClone();
        source["strainPreimage"]!["originalInput"]!["contentFingerprint"] = fixture.Source["strainPreimage"]!["originalInput"]!["contentFingerprint"]!.DeepClone();
        SpiritualStrainReceiptJoinTests.SealSourceAndDecision(root, 0);
        Assert.Equal(baseline, root.ToJsonString());
    }

    /// <summary>A lawfully changed complete parent passes intrinsic and receipt parsing but fails the separate exact unchanged History after-image join.</summary>
    /// <param name="rank">Independent expected rank of the complete III or IV guardian parent.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void GuardianReceipt_RejectsChangedParentAtExplicitHistoryJoin(int rank)
    {
        var (fixture, root, baselineState, history, active) = CreateBaseline(rank);
        var historyBefore = fixture.Retained.History.ToJsonString();
        var source = root["sources"]![0]!["witness"]!.AsObject();
        var parent = SpiritualExistingPressureTestData.ReadParent(source);
        var baselineParent = WoundMaterializationContract.Parse(parent.ToJsonString(), "unit.guardianReceipt.parent");
        Assert.True(baselineParent.IsValid, IssuesJson(baselineParent.Issues));
        parent["display"]!["description"] = "Иное полное описание той же духовной раны Хранителя.";
        var changedParent = WoundMaterializationContract.Parse(parent.ToJsonString(), "unit.guardianReceipt.changedParent");
        Assert.True(changedParent.IsValid, IssuesJson(changedParent.Issues));
        Assert.NotEqual(WoundIdentityState.ComputeSemanticFingerprint(baselineParent.Wound!),
            WoundIdentityState.ComputeSemanticFingerprint(changedParent.Wound!));
        source["strainPreimage"]!["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(parent);
        SpiritualStrainReceiptJoinTests.SealSourceAndDecision(root, 0);
        var before = root.ToJsonString();
        AssertSupportedSource(source);
        var changedState = ReadReceipt(root, "guardian_receipt_changed_parent");
        Assert.True(baselineState.HasExactStrainHistory(history));
        Assert.False(changedState.HasExactStrainHistory(history));
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
            changedState.MatchRetainedPressureHistory("chaos_sea", active, 47));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"],
            changedState.ReadRetainedStrainOrdinaryAfter("chaos_sea", ConflictId(fixture), fixture.Actual, false)));
        Assert.Equal(before, root.ToJsonString());
        Assert.Equal(historyBefore, fixture.Retained.History.ToJsonString());
    }

    /// <summary>An otherwise unchanged accepted log cannot strip the declaration, even with historical readable-summary drift allowed.</summary>
    /// <param name="rank">Independent expected rank of the complete III or IV guardian parent.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void GuardianReceipt_RejectsStrippedDeclarationAtExplicitLogJoin(int rank)
    {
        var (fixture, root, _, history, active) = CreateBaseline(rank);
        var receiptBefore = root.ToJsonString();
        var historyBefore = fixture.Retained.History.ToJsonString();
        var changedActual = active["exchangeLog"]![0]!.AsObject();
        Assert.True(changedActual.Remove("spiritualWoundFailedAction"));
        var activeBefore = active.ToJsonString();
        var state = ReadReceipt(root, "guardian_receipt_unchanged_after_log_mutation");
        Assert.True(state.HasExactStrainHistory(history));
        foreach (var currentTurn in new[] { 46, 47 })
            Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Mismatch,
                state.MatchRetainedPressureHistory("chaos_sea", active, currentTurn));
        foreach (var allowSummaryDrift in new[] { false, true })
            Assert.Null(state.ReadRetainedStrainOrdinaryAfter("chaos_sea", ConflictId(fixture), changedActual, allowSummaryDrift));
        Assert.Equal(receiptBefore, root.ToJsonString());
        Assert.Equal(historyBefore, fixture.Retained.History.ToJsonString());
        Assert.Equal(activeBefore, active.ToJsonString());
    }

    private (SpiritualGuardianAttributionFixture Fixture, JsonObject Root, SpiritualWoundOpportunityReceiptState State,
        WoundHistoryState History, JsonObject Active) CreateBaseline(int rank = 3)
    {
        var fixture = SpiritualGuardianAttributionTestData.Create(rank: rank);
        var fixtureIssues = SpiritualGuardianAttributionDataCheck.Check(fixture.Retained, fixture.Profiles, fixture.Conflict, rank);
        Assert.True(fixtureIssues.Count == 0, IssuesJson(fixtureIssues));
        AssertSupportedSource(fixture.Source);
        var root = SpiritualExistingPressureTestData.CreateLedger(fixture.Source).Root;
        // The existing cheap declined-ledger shape has player-fixture start coordinates. Author this guardian instance
        // from its actual source before hashing, then update its two references and decision digest. No owner is issued.
        var instance = root["instances"]![0]!.AsObject();
        var evidence = fixture.Source["turnEvidence"]!;
        instance["displayConflictId"] = fixture.Source["conflictId"]!.DeepClone();
        instance["realm"] = evidence["realm"]!.DeepClone();
        instance["startSessionId"] = evidence["sessionId"]!.DeepClone();
        instance["startRequestId"] = evidence["requestId"]!.DeepClone();
        instance["startSnapshotToken"] = evidence["snapshotToken"]!.DeepClone();
        instance["startTurn"] = evidence["turn"]!.DeepClone();
        instance["baselineConflictFingerprint"] = fixture.Source["conflictBeforeFingerprint"]!.DeepClone();
        var hash = SpiritualWoundConflictInstanceState.ComputeRowFingerprint(instance, "instance");
        instance["instanceId"] = "spiritual_instance_" + hash[7..]; instance["instanceFingerprint"] = hash;
        root["sources"]![0]!["instanceId"] = instance["instanceId"]!.DeepClone();
        root["decisions"]![0]!["instanceId"] = instance["instanceId"]!.DeepClone();
        SpiritualStrainReceiptJoinTests.SealSourceAndDecision(root, 0);
        var before = root.ToJsonString();
        var state = ReadReceipt(root, "guardian_receipt_baseline");
        var historyJson = fixture.Retained.History.ToJsonString();
        var parsedHistory = WoundHistoryState.Parse(historyJson, WoundHistoryState.HistoryPath);
        Assert.True(parsedHistory.IsValid, IssuesJson(parsedHistory.Issues));
        var history = Assert.IsType<WoundHistoryState>(parsedHistory.State);
        Assert.True(state.HasRetainedPressureComparisons);
        Assert.True(state.HasExactStrainHistory(history));
        var active = fixture.Conflict["activeConflict"]!.DeepClone().AsObject();
        active["exchangeLog"]![0] = fixture.Actual.DeepClone();
        active["oppositionSideStrain"] = fixture.Actual["after"]!["oppositionSideStrain"]!.DeepClone();
        foreach (var currentTurn in new[] { 46, 47 })
            Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
                state.MatchRetainedPressureHistory("chaos_sea", active, currentTurn));
        foreach (var allowSummaryDrift in new[] { false, true })
            Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"],
                state.ReadRetainedStrainOrdinaryAfter("chaos_sea", ConflictId(fixture), fixture.Actual, allowSummaryDrift)));
        Assert.Equal(before, root.ToJsonString());
        Assert.Equal(historyJson, fixture.Retained.History.ToJsonString());
        return (fixture, root, state, history, active);
    }

    private static void AssertSupportedSource(JsonObject source)
    {
        var before = source.ToJsonString();
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained);
        Assert.Equal((15L, 2), (calculation.TraumaPressure, calculation.MaximumSeverityRank));
        var comparison = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(5, comparison.Version); Assert.Single(comparison.Parents);
        Assert.Equal("guardian", comparison.ParentWound.Owner.OwnerKind);
        Assert.Equal(before, source.ToJsonString());
    }

    private SpiritualWoundOpportunityReceiptState ReadReceipt(JsonObject root, string phase)
    {
        var result = ParseReceipt(root, phase);
        Assert.True(result.IsValid, IssuesJson(result.Issues));
        Assert.Empty(result.Issues);
        return Assert.IsType<SpiritualWoundOpportunityReceiptState>(result.State);
    }

    private SpiritualWoundReceiptParseResult ParseReceipt(JsonObject root, string phase)
    {
        var before = root.ToJsonString();
        var result = SpiritualWoundOpportunityReceiptState.Parse(before, SpiritualWoundOpportunityReceiptState.StatePath);
        output.WriteLine(JsonSerializer.Serialize(new { phase, result.IsValid, issues = IssuesJson(result.Issues), receiptJson = before }));
        Assert.Equal(before, root.ToJsonString());
        return result;
    }

    private static string ConflictId(SpiritualGuardianAttributionFixture fixture) => fixture.Source["conflictId"]!.GetValue<string>();

    private static string IssuesJson(IReadOnlyList<ValidationIssue> issues) => JsonSerializer.Serialize(issues.Select(issue => new
    {
        issue.FilePath, issue.Severity, issue.Message, issue.Category, issue.Code, issue.Actor,
        issue.Section, issue.Expected, issue.Actual, issue.RepairHint, issue.RepairTargetFiles
    }));
}
