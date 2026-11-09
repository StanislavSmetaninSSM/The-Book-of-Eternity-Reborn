using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualGroupZeroPressureDataTests
{
    /// <summary>Shared receipt identity, original-request and immutable-prefix fences remain active for actual-zero groups.</summary>
    /// <param name="change">One invalid ledger join or valid but conflicting accepted prefix.</param>
    [Theory]
    [InlineData("duplicate-source")] [InlineData("duplicate-coordinate")]
    [InlineData("instance")] [InlineData("request")]
    [InlineData("comparison-prefix")] [InlineData("injury-prefix")]
    public void GroupZero_ReceiptBoundariesRemainClosed(string change)
    {
        var (fixture, root) = CreateSupportedGroupZeroLedger();
        var state = ReadGroupZeroReceipt(root);
        var fixtureBefore = SpiritualGroupZeroPressureData.Serialize(fixture);
        var next = root.DeepClone().AsObject(); var rows = next["strainComparisons"]!.AsArray();
        var row = rows[0]!.AsObject(); var witness = row["witness"]!.AsObject();
        switch (change)
        {
            case "duplicate-source":
            case "duplicate-coordinate":
                var duplicate = row.DeepClone().AsObject(); duplicate["ordinal"] = 2; duplicate["instanceComparisonOrdinal"] = 2;
                if (change == "duplicate-coordinate")
                {
                    var other = duplicate["witness"]!.AsObject();
                    other["turnEvidence"]!["sessionId"] = "group_zero_other_session";
                    other["turnEvidence"]!["requestId"] = "group_zero_other_request";
                    other["turnEvidence"]!["snapshotToken"] = "group_zero_other_snapshot";
                    SpiritualExistingPressureTestData.Seal(other);
                    Assert.Equal(0, SpiritualWoundSourceWitness.Validate(other).MaximumSeverityRank);
                    Assert.NotEqual(witness["sourceId"]!.GetValue<string>(), other["sourceId"]!.GetValue<string>());
                }
                rows.Add(duplicate); next["nextStrainComparisonOrdinal"] = 3; break;
            case "instance": row["instanceId"] = "foreign_instance"; break;
            case "request":
                var accepted = next["sources"]![0]!["witness"]!["turnEvidence"]!;
                foreach (var field in new[] { "sessionId", "requestId", "snapshotToken" }) witness["turnEvidence"]![field] = accepted[field]!.DeepClone();
                SpiritualExistingPressureTestData.Seal(witness);
                Assert.Equal(0, SpiritualWoundSourceWitness.Validate(witness).MaximumSeverityRank); break;
            case "comparison-prefix":
                var actual = ReadGroupZeroPayload(witness["exchangeJsonBase64"]!);
                var ordinary = ReadGroupZeroPayload(witness["strainPreimage"]!["ordinaryExchangeJsonBase64"]!);
                actual["summary"] = "Иное сохранённое описание."; ordinary["summary"] = actual["summary"]!.DeepClone();
                witness["exchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(actual);
                witness["strainPreimage"]!["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(ordinary);
                SpiritualExistingPressureTestData.Seal(witness);
                Assert.Equal(0, SpiritualWoundSourceWitness.Validate(witness).MaximumSeverityRank); break;
            case "injury-prefix":
                var injury = next["sources"]![0]!["witness"]!.AsObject();
                var exchange = ReadGroupZeroPayload(injury["exchangeJsonBase64"]!); exchange["summary"] = "Иное описание прежнего рождения раны.";
                injury["exchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(exchange); SpiritualExistingPressureTestData.Seal(injury);
                var decision = next["decisions"]![0]!.AsObject();
                decision["sourceWitness"] = injury.DeepClone(); decision["sourceId"] = injury["sourceId"]!.DeepClone();
                decision["sourceFingerprint"] = injury["sourceFingerprint"]!.DeepClone();
                decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
                Assert.True(SpiritualWoundSourceWitness.Validate(injury).MaximumSeverityRank > 0); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        var nextBefore = next.ToJsonString();
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(nextBefore, SpiritualWoundOpportunityReceiptState.StatePath);
        if (change is "comparison-prefix" or "injury-prefix")
        {
            Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
            Assert.Equal("conflict", SpiritualWoundOpportunityReceiptState.PlanAppend(state, parsed.State!).Disposition);
        }
        else { Assert.False(parsed.IsValid); Assert.Null(parsed.State); }
        Assert.Equal(nextBefore, next.ToJsonString());
        Assert.Equal(fixtureBefore, SpiritualGroupZeroPressureData.Serialize(fixture));
    }

    /// <summary>Real normalizer History reads and signed-log matching retain the final saturated parent, including lawful later healing.</summary>
    /// <param name="change">Exact final anchor, anchor loss/drift, later healing or actual-log drift.</param>
    [Theory]
    [InlineData("exact")] [InlineData("missing")] [InlineData("changed")]
    [InlineData("later-healing")] [InlineData("log-missing")] [InlineData("log-changed")]
    public async Task GroupZero_FinalHistoryAndSignedLogRemainExact(string change)
    {
        var (fixture, root) = CreateSupportedGroupZeroLedger(); var state = ReadGroupZeroReceipt(root);
        var history = fixture.Retained.History.DeepClone().AsObject(); var transitions = history["transitions"]!.AsArray();
        var final = transitions[3]!.AsObject(); Assert.Equal("wound_retained_pressure_owner_4", final["woundId"]!.GetValue<string>());
        var active = fixture.Conflict["activeConflict"]!.DeepClone().AsObject();
        active["playerSideStrain"] = "broken"; active["exchangeLog"]![0] = fixture.Actual.DeepClone();
        switch (change)
        {
            case "exact": break;
            case "missing": transitions.RemoveAt(3); history["nextOrdinal"] = 4; break;
            case "changed": final["afterFingerprint"] = "sha256:" + new string('d', 64); break;
            case "later-healing":
                var heal = final.DeepClone().AsObject();
                heal["ordinal"] = 5; heal["woundTransitionOrdinal"] = 2; heal["turn"] = 47; heal["kind"] = "heal";
                heal["transitionId"] = "transition_group_zero_final_heal"; heal["operationKey"] = "operation_group_zero_final_heal";
                heal["eventRef"] = "event_group_zero_final_heal"; heal["terminal"] = true;
                heal["beforeFingerprint"] = final["afterFingerprint"]!.DeepClone(); heal["afterFingerprint"] = "sha256:" + new string('d', 64);
                transitions.Add(heal); history["nextOrdinal"] = 6; break;
            case "log-missing": active["exchangeLog"]!.AsArray().Clear(); break;
            case "log-changed": active["exchangeLog"]![0]!["incomingAction"]!["summary"] = "Иное вложенное действие."; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        var parsed = WoundHistoryState.Parse(history.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
        var historyMatches = change is not ("missing" or "changed");
        Assert.Equal(historyMatches, state.HasExactStrainHistory(parsed.State!));
        var historyBefore = history.ToJsonString(); var rootBefore = root.ToJsonString(); var activeBefore = active.ToJsonString();
        var reads = 0;
        Task Act() => CanonicalStateNormalizer.ValidateSpiritualReceiptPublicationHistoryAsync(state, root, () =>
        { reads++; return Task.FromResult<string?>(history.ToJsonString()); });
        if (historyMatches) await Act(); else await Assert.ThrowsAsync<InvalidDataException>(Act);
        Assert.Equal(1, reads);
        Assert.Equal(change is "log-missing" or "log-changed" ? SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Mismatch :
            SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact, state.MatchRetainedPressureHistory("chaos_sea", active, 48));
        Assert.Equal(historyBefore, history.ToJsonString()); Assert.Equal(rootBefore, root.ToJsonString()); Assert.Equal(activeBefore, active.ToJsonString());
    }

    /// <summary>Builds fresh complete parser DATA from the same lawful resolver/schema1/schema3 stages, without fake C3 authority.</summary>
    private static (SpiritualGroupZeroPressureFixture Fixture, JsonObject Receipt) CreateSupportedGroupZeroLedger()
    {
        var fixture = SpiritualGroupZeroPressureData.Create(GroupZeroPressureContour.Capped);
        AssertPrerequisites(fixture, GroupZeroPressureContour.Capped);
        _ = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var data);
        AssertSupported(fixture, GroupZeroPressureContour.Capped, data!);
        var root = fixture.Retained.Receipts.DeepClone().AsObject(); _ = ReadGroupZeroReceipt(root);
        var evidence = fixture.Source["turnEvidence"]!;
        var resolved = SpiritualWoundFirstOfferInstanceResolver.Resolve(new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(root.ToJsonString())),
            new CanonicalBeforeImage(true, fixture.RawInput), "chaos_sea", evidence["sessionId"]!.GetValue<string>(),
            evidence["requestId"]!.GetValue<string>(), evidence["snapshotToken"]!.GetValue<string>(), 46);
        Assert.Empty(resolved.Issues); Assert.NotNull(resolved.ProposedRow);
        root["instances"]!.AsArray().Add(resolved.ProposedRow!.DeepClone()); root["nextInstanceOrdinal"] = resolved.ProposedRow["ordinal"]!.GetValue<int>() + 1;
        _ = ReadGroupZeroReceipt(root);
        root["schemaVersion"] = 3; root["healingSources"] = new JsonArray(); root["healingDecisions"] = new JsonArray();
        root["nextHealingSourceOrdinal"] = 1; root["nextHealingDecisionOrdinal"] = 1;
        root["nextStrainComparisonOrdinal"] = 2;
        root["strainComparisons"] = new JsonArray(new JsonObject { ["ordinal"] = 1, ["instanceId"] = resolved.InstanceId,
            ["instanceComparisonOrdinal"] = 1, ["witness"] = fixture.Source.DeepClone() });
        _ = ReadGroupZeroReceipt(root); return (fixture, root);
    }

    /// <summary>Decodes only test-owned complete payload DATA, preserving strict UTF-8/duplicate-property parsing.</summary>
    /// <param name="encoded">Canonical base64 complete exchange input.</param>
    /// <returns>Detached duplicate-checked payload.</returns>
    private static JsonObject ReadGroupZeroPayload(JsonNode encoded) => SpiritualWoundStateJson.Parse(
        SpiritualWoundStateJson.DecodeUtf8JsonText(Convert.FromBase64String(encoded.GetValue<string>())));
}
