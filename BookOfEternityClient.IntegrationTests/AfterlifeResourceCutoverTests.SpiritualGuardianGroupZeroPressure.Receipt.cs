using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Builds the complete independent six-field v7 expected DATA once, retaining canonical owner encodings and every parent.</summary>
    /// <param name="data">Closed low-margin ordinary/actual and complete pre-sign parents.</param>
    /// <param name="raw">Exact post-sign ordinary bytes, including BOM and whitespace.</param>
    /// <returns>Detached expected proof DATA, never a live basis or source-only issuance key.</returns>
    private JsonObject CreateGuardianGroupZeroExpectedProof(GuardianGroupZeroPressureData data, byte[] raw)
    {
        var input = new JsonObject { ["path"] = AfterlifeSpiritualConflictState.StatePath, ["existed"] = true,
            ["contentFingerprint"] = new CanonicalBeforeImage(true, raw).Fingerprint, ["exchangePointer"] = "/activeConflict/exchangeLog/0" };
        var proof = SpiritualGuardianGroupPressureProofData.CreateProof(data.Retained.Parents, data.Ordinary, input, data.OrdinaryCalculation);
        proof["ordinaryExchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(data.Ordinary)));
        for (var index = 0; index < data.Retained.Parents.Count; index++)
        {
            var parsed = WoundMaterializationContract.Parse(data.Retained.Parents[index].ToJsonString(), "guardian.group.zero.expected_parent");
            RequireGuardianJourneyIssues("guardian_group_zero", "expected_parent", parsed.Issues); Assert.True(parsed.IsValid);
            proof["parents"]![index]!["parentWoundJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                WoundMaterializationContract.SerializeCanonical(parsed.Wound!)));
        }
        return proof;
    }

    /// <summary>Checks a genuine source-only schema3 append with every complete v7 parent, unchanged ancestry, independent replay and exact accepted-log joins.</summary>
    /// <param name="setup">This case's independently signed original receipt/History and exact authored ordinary DATA.</param>
    /// <param name="receipt">Actual unregistered common plan receipt candidate, never a manufactured completed owner.</param>
    /// <param name="actualRoot">Complete independently asserted actualIV conflict after-image.</param>
    /// <param name="signedSnapshotFingerprint">The real cold capture's independently verified complete signed-original fingerprint.</param>
    private void AssertGuardianGroupZeroSourceOnlyReceipt(GuardianGroupZeroPressureSetup setup, JsonObject receipt, JsonObject actualRoot, string signedSnapshotFingerprint)
    {
        var before = ReadGuardianAttributionJson(setup.OriginalReceiptBytes);
        Assert.Equal(3, receipt["schemaVersion"]!.GetValue<int>());
        foreach (var (field, node) in before)
            if (field is not ("schemaVersion" or "instances" or "nextInstanceOrdinal"))
                Assert.True(JsonNode.DeepEquals(node, receipt[field]), field);
        var oldInstances = before["instances"]!.AsArray(); var instances = receipt["instances"]!.AsArray();
        Assert.Equal(2, oldInstances.Count); Assert.Equal(3, instances.Count);
        for (var index = 0; index < oldInstances.Count; index++) Assert.True(JsonNode.DeepEquals(oldInstances[index], instances[index]));
        Assert.Equal(before["nextInstanceOrdinal"]!.GetValue<int>() + 1, receipt["nextInstanceOrdinal"]!.GetValue<int>());
        foreach (var field in new[] { "sources", "decisions", "closures" })
        {
            Assert.Equal(2, before[field]!.AsArray().Count);
            Assert.True(JsonNode.DeepEquals(before[field], receipt[field]), field);
        }
        Assert.Empty(receipt["healingSources"]!.AsArray()); Assert.Empty(receipt["healingDecisions"]!.AsArray());
        Assert.Equal((1, 1), (receipt["nextHealingSourceOrdinal"]!.GetValue<int>(), receipt["nextHealingDecisionOrdinal"]!.GetValue<int>()));
        var row = Assert.Single(receipt["strainComparisons"]!.AsArray())!;
        Assert.Equal((1, 1, 2), (row["ordinal"]!.GetValue<int>(), row["instanceComparisonOrdinal"]!.GetValue<int>(),
            receipt["nextStrainComparisonOrdinal"]!.GetValue<int>()));
        var witness = row["witness"]!.AsObject();
        Assert.Equal(setup.Data.ActualCalculation, SpiritualWoundSourceWitness.ValidateAndReadStrainData(witness, out var intrinsic));
        var comparison = Assert.IsType<SpiritualStrainPreimageData>(intrinsic);
        Assert.Equal(7, comparison.Version); Assert.Equal(2, comparison.Parents.Count);
        Assert.Equal(setup.Data.OrdinaryCalculation, comparison.OrdinaryCalculation);
        Assert.Equal(setup.Data.ActualCalculation, comparison.ActualCalculation);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(setup.ExpectedProofJson), witness["strainPreimage"]));
        for (var index = 0; index < comparison.Parents.Count; index++)
        {
            var parent = comparison.Parents[index];
            Assert.Null(parent.ParentSourceId);
            Assert.Equal(index == 0 ? 3 : 4, parent.ParentWound.Severity.Rank);
            Assert.True(JsonNode.DeepEquals(setup.Data.Retained.Parents[index],
                JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(parent.ParentWound))));
            Assert.Equal((setup.Data.Retained.Effects[index]["effectId"]!.GetValue<string>(), "component_001"),
                (parent.EffectId, parent.ComponentId));
        }
        Assert.Equal(new CanonicalBeforeImage(true, setup.RawOrdinaryBytes).Fingerprint, comparison.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", comparison.OriginalExchangePointer);
        Assert.True(JsonNode.DeepEquals(setup.Data.Actual, comparison.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(setup.Data.Ordinary["after"], comparison.ReadOrdinaryAfter(comparison.ReadActualExchange())));
        Assert.Equal(("activeConflict.exchangeLog[0]", "opposition", "pressure", 0, 0),
            (witness["coordinate"]!.GetValue<string>(), witness["affectedSide"]!.GetValue<string>(), witness["operation"]!.GetValue<string>(),
                witness["sourceOrdinal"]!.GetValue<int>(), witness["exchangeOrdinal"]!.GetValue<int>()));
        Assert.Equal("exchange_guardian_group_46", witness["exchangeId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["actorKind"] = "player_soul", ["actorId"] = "player_soul" }, witness["actingActor"]));
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["actorKind"] = "guardian", ["actorId"] = "guardian_frame" }, witness["affectedActor"]));
        Assert.Null(witness["guaranteedSeverityRank"]); Assert.Null(witness["retraumaWoundId"]);
        var evidence = witness["turnEvidence"]!;
        Assert.Equal(signedSnapshotFingerprint, evidence["originalSnapshotFingerprint"]!.GetValue<string>());
        Assert.Equal(("session_" + setup.Name, "request_" + setup.Name, 46, "chaos_sea"),
            (evidence["sessionId"]!.GetValue<string>(), evidence["requestId"]!.GetValue<string>(),
                evidence["turn"]!.GetValue<int>(), evidence["realm"]!.GetValue<string>()));
        Assert.Equal(setup.OrdinarySource["turnEvidence"]!["snapshotToken"]!.GetValue<string>(), evidence["snapshotToken"]!.GetValue<string>());
        Assert.Equal(new[] { 0, 1 }, witness["selectedDiceIndices"]!.AsArray().Select(value => value!.GetValue<int>()));
        Assert.Equal(new[] { 8, 4 }, evidence["acceptedD20Values"]!.AsArray().Select(value => value!.GetValue<int>()));
        Assert.Equal(new[] { 0, 1 }, evidence["diceClaims"]!.AsArray().Select(value => value!["sourceIndex"]!.GetValue<int>()));
        var resolved = SpiritualWoundFirstOfferInstanceResolver.Resolve(new CanonicalBeforeImage(true, setup.OriginalReceiptBytes),
            new CanonicalBeforeImage(true, setup.OriginalConflictBytes), "chaos_sea", evidence["sessionId"]!.GetValue<string>(),
            evidence["requestId"]!.GetValue<string>(), evidence["snapshotToken"]!.GetValue<string>(), 46);
        RequireGuardianJourneyIssues(setup.Name, "zero_instance_resolution", resolved.Issues); Assert.NotNull(resolved.ProposedRow);
        Assert.True(JsonNode.DeepEquals(resolved.ProposedRow, instances[^1])); Assert.Equal(resolved.InstanceId, row["instanceId"]!.GetValue<string>());
        Assert.DoesNotContain(receipt["sources"]!.AsArray(), item => item!["witness"]!["sourceId"]!.GetValue<string>() == witness["sourceId"]!.GetValue<string>());
        Assert.DoesNotContain(receipt["decisions"]!.AsArray(), item => item!["sourceId"]!.GetValue<string>() == witness["sourceId"]!.GetValue<string>());
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        var prior = SpiritualWoundOpportunityReceiptState.Parse(before.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        var history = WoundHistoryState.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(setup.OriginalHistoryBytes), WoundHistoryState.HistoryPath);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_receipt_parse_operands", setup.Name,
            before, receipt, parsed.Issues, priorIssues = prior.Issues, historyIssues = history.Issues }));
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues)); Assert.True(prior.IsValid, JsonSerializer.Serialize(prior.Issues));
        Assert.True(history.IsValid, JsonSerializer.Serialize(history.Issues));
        foreach (var parent in comparison.Parents)
        {
            var wound = parent.ParentWound;
            Assert.True(history.State!.TryResolveExactTransition(wound.LastTransition.TransitionId, out var anchor)); Assert.NotNull(anchor);
            Assert.Equal((wound.WoundId, wound.LastTransition.TransitionId, wound.LastTransition.Kind, wound.LastTransition.Ordinal, wound.LastTransition.Turn, false),
                (anchor.WoundId, anchor.TransitionId, anchor.Kind, anchor.WoundTransitionOrdinal, anchor.Turn, anchor.Terminal));
            Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(wound), anchor.AfterFingerprint);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_every_parent_history_anchor", setup.Name,
                parentJson = WoundMaterializationContract.SerializeCanonical(wound), anchor }));
        }
        var ledger = parsed.State!;
        Assert.True(ledger.HasRetainedPressureComparisons); Assert.True(ledger.HasExactStrainHistory(history.State!));
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
            ledger.MatchRetainedPressureHistory("chaos_sea", actualRoot["activeConflict"]!.AsObject(), 46));
        Assert.True(JsonNode.DeepEquals(setup.Data.Ordinary["after"], ledger.ReadRetainedStrainOrdinaryAfter("chaos_sea",
            actualRoot["activeConflict"]!["conflictId"]!.GetValue<string>(), setup.Data.Actual, allowHistoricalSummaryDrift: false)));
        var append = SpiritualWoundOpportunityReceiptState.PlanAppend(prior.State!, ledger);
        Assert.Equal("appended", append.Disposition); Assert.NotNull(append.After);
        Assert.True(JsonNode.DeepEquals(receipt, JsonNode.Parse(SpiritualWoundOpportunityReceiptState.SerializeCanonical(append.After!))));
        var independentReplay = SpiritualWoundOpportunityReceiptState.Parse(receipt.DeepClone().ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(independentReplay.IsValid, JsonSerializer.Serialize(independentReplay.Issues));
        var replay = SpiritualWoundOpportunityReceiptState.PlanAppend(append.After!, independentReplay.State!);
        Assert.Equal("exact_replay", replay.Disposition); Assert.NotNull(replay.After);
        Assert.True(JsonNode.DeepEquals(receipt, JsonNode.Parse(SpiritualWoundOpportunityReceiptState.SerializeCanonical(replay.After!))));
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_receipt_append_replay", setup.Name,
            append = append.Disposition, replay = replay.Disposition, witness, receipt }));
    }
}
