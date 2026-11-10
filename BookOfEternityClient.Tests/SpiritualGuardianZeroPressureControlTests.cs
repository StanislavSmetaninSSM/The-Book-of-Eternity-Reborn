using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Finite after-valid guardian-zero DATA boundaries; no test issues current or publication authority.</summary>
/// <param name="output">Per-test diagnostic sink; no mutable fixture state is shared.</param>
public sealed class SpiritualGuardianZeroPressureControlTests(ITestOutputHelper output)
{
    /// <summary>Resealed semantic contradictions cannot escape through either retained reader after a lawful hostile baseline.</summary>
    /// <param name="change">One closed calculation, declaration, parent, geometry or pointer contradiction.</param>
    [Theory]
    [InlineData("actual-maximum")] [InlineData("ordinary-maximum")] [InlineData("ordinary-pressure")]
    [InlineData("declaration-absent")] [InlineData("declaration-contradictory")] [InlineData("foreign-alias")]
    [InlineData("missing-parent")] [InlineData("extra-parent-field")] [InlineData("foreign-effect")] [InlineData("foreign-component")]
    [InlineData("sibling")] [InlineData("role")] [InlineData("ordinal")] [InlineData("raw-pointer")]
    [InlineData("nonincrease")] [InlineData("no-effect")] [InlineData("ordinary-IV")]
    public void GuardianZero_ResealedContradictionsRefuse(string change)
    {
        var fixture = SpiritualGuardianZeroPressureTestData.Create(3);
        var prior = AssertLawful(fixture);
        var source = fixture.Source; var proof = source["strainPreimage"]!.AsObject();
        switch (change)
        {
            case "actual-maximum": source["maximumSeverityRank"] = 1; break;
            case "ordinary-maximum": proof["ordinaryCalculation"]!["maximumSeverityRank"] = 1; break;
            case "ordinary-pressure": proof["ordinaryCalculation"]!["traumaPressure"] = 3L; break;
            case "declaration-absent":
                fixture.Actual.Remove("spiritualWoundFailedAction"); fixture.Ordinary.Remove("spiritualWoundFailedAction"); break;
            case "declaration-contradictory": fixture.Ordinary["spiritualWoundFailedAction"]!["actorId"] = "guardian_foreign"; break;
            case "foreign-alias":
                fixture.Actual["incomingAction"]!["ownerActorId"] = "guardian_foreign";
                fixture.Ordinary["incomingAction"]!["ownerActorId"] = "guardian_foreign"; break;
            case "missing-parent": Assert.True(proof.Remove("parentWoundJsonBase64")); break;
            case "extra-parent-field": proof["parents"] = new JsonArray(fixture.Retained.Parents.Single().DeepClone()); break;
            case "foreign-effect": proof["effectId"] = "effect_foreign"; break;
            case "foreign-component": proof["componentId"] = "component_foreign"; break;
            case "sibling": fixture.Ordinary["summary"] = "Different ordinary-only summary."; break;
            case "role": proof["inputRoleRevision"] = "existing_pressure_ordinary_v1"; break;
            case "ordinal":
                source["exchangeOrdinal"] = 1; source["turnEvidence"]!["bounds"]!["exchangeCount"] = 2;
                source["turnEvidence"]!["bounds"]!["sourceSlotCount"] = 4;
                foreach (var claim in source["turnEvidence"]!["diceClaims"]!.AsArray().OfType<JsonObject>())
                { claim["exchangeOrdinal"] = 1; claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim); }
                break;
            case "raw-pointer": proof["originalInput"]!["exchangePointer"] = "/activeConflict/exchangeLog/00"; break;
            case "nonincrease":
                fixture.Actual["after"]!["oppositionSideStrain"] = "strained";
                source["destinationStrainRank"] = 1; source["extraStrainJumps"] = 0; break;
            case "no-effect":
                fixture.Actual["outcome"] = "no_effect"; fixture.Ordinary["outcome"] = "no_effect"; break;
            case "ordinary-IV": fixture.Ordinary["after"]!["oppositionSideStrain"] = "broken"; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        source["exchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Actual);
        proof["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Ordinary);
        SpiritualExistingPressureTestData.Seal(source);
        var before = source.ToJsonString(); SpiritualStrainPreimageData? escaped = prior;
        var full = Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out escaped));
        Assert.Null(escaped);
        var direct = Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadIntrinsic(source, fixture.Actual));
        Assert.Equal(full.Message, direct.Message); Assert.Equal(before, source.ToJsonString());
        if (change == "ordinary-IV")
            Assert.Equal("Increasing ordinary guardian source with nonnegative maximum required.", full.Message);
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_after_valid_wire_refusal", change,
            baselineAccepted = true, full.Message, sourceJson = before }));
    }

    /// <summary>Well-formed comparator hashes and external raw byte changes are not authenticated by detached intrinsic readers.</summary>
    /// <param name="change">Change an opaque comparator hash or independent BOM/whitespace bytes without a live owner.</param>
    [Theory]
    [InlineData("raw-hash")] [InlineData("raw-bytes")]
    public void GuardianZero_IntrinsicComparatorsDoNotAuthenticateRaw(string change)
    {
        var fixture = SpiritualGuardianZeroPressureTestData.Create(3); AssertLawful(fixture);
        var raw = fixture.RawOrdinaryBytes;
        if (change == "raw-hash") fixture.Source["strainPreimage"]!["originalInput"]!["contentFingerprint"] = "sha256:" + new string('f', 64);
        else raw = [.. raw, (byte)' '];
        SpiritualExistingPressureTestData.Seal(fixture.Source); var before = fixture.Source.ToJsonString();
        Assert.Equal(fixture.ActualCalculation, SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var data));
        var retained = Assert.IsType<SpiritualStrainPreimageData>(data);
        Assert.NotEqual(new CanonicalBeforeImage(true, raw).Fingerprint, retained.OriginalInputFingerprint);
        Assert.Equal(retained.OriginalInputFingerprint, SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual).OriginalInputFingerprint);
        Assert.Equal(before, fixture.Source.ToJsonString());
    }

    /// <summary>Exact receipt identity, request, order, immutable prefixes and positive-versus-zero tables stay closed.</summary>
    /// <param name="change">One independently named containing-table boundary after a valid guardian receipt.</param>
    [Theory]
    [InlineData("duplicate-source")] [InlineData("duplicate-coordinate")] [InlineData("instance")] [InlineData("request")]
    [InlineData("comparison-prefix")] [InlineData("injury-prefix")] [InlineData("positive-comparison")] [InlineData("zero-injury")]
    public void GuardianZero_ReceiptBoundariesRemainClosed(string change)
    {
        var fixture = SpiritualGuardianZeroPressureTestData.Create(3); AssertLawful(fixture);
        var root = BuildReceipt(fixture); var state = ReadReceipt(root); var next = root.DeepClone().AsObject();
        var rows = next["strainComparisons"]!.AsArray(); var row = rows[0]!.AsObject(); var witness = row["witness"]!.AsObject();
        switch (change)
        {
            case "duplicate-source":
            case "duplicate-coordinate":
                var duplicate = row.DeepClone().AsObject(); duplicate["ordinal"] = 2; duplicate["instanceComparisonOrdinal"] = 2;
                if (change == "duplicate-coordinate")
                {
                    var other = duplicate["witness"]!.AsObject(); other["turnEvidence"]!["requestId"] = "other_guardian_zero_request";
                    SpiritualExistingPressureTestData.Seal(other); Assert.Equal(0, SpiritualWoundSourceWitness.Validate(other).MaximumSeverityRank);
                    Assert.NotEqual(witness["sourceId"]!.GetValue<string>(), other["sourceId"]!.GetValue<string>());
                }
                rows.Add(duplicate); next["nextStrainComparisonOrdinal"] = 3; break;
            case "instance": row["instanceId"] = "foreign_instance"; break;
            case "request":
                var prior = next["sources"]![0]!["witness"]!["turnEvidence"]!;
                foreach (var field in new[] { "sessionId", "requestId", "snapshotToken" }) witness["turnEvidence"]![field] = prior[field]!.DeepClone();
                SpiritualExistingPressureTestData.Seal(witness); Assert.Equal(0, SpiritualWoundSourceWitness.Validate(witness).MaximumSeverityRank); break;
            case "comparison-prefix":
                var actual = SpiritualExistingPressureTestData.ReadActual(witness); var ordinary = SpiritualExistingPressureTestData.ReadOrdinary(witness);
                actual["summary"] = "Changed accepted comparison summary."; ordinary["summary"] = actual["summary"]!.DeepClone();
                witness["exchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(actual);
                witness["strainPreimage"]!["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(ordinary);
                SpiritualExistingPressureTestData.Seal(witness); Assert.Equal(0, SpiritualWoundSourceWitness.Validate(witness).MaximumSeverityRank); break;
            case "injury-prefix":
                var injury = next["sources"]![0]!["witness"]!.AsObject(); var birth = SpiritualExistingPressureTestData.ReadActual(injury);
                birth["summary"] = "Changed accepted historical injury summary."; injury["exchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(birth);
                SpiritualExistingPressureTestData.Seal(injury); var decision = next["decisions"]![0]!.AsObject();
                decision["sourceWitness"] = injury.DeepClone(); decision["sourceId"] = injury["sourceId"]!.DeepClone();
                decision["sourceFingerprint"] = injury["sourceFingerprint"]!.DeepClone();
                decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint"); break;
            case "positive-comparison":
                var positive = SpiritualGuardianZeroPressureTestData.CreateOrdinaryZeroPositiveActual(); AssertLawful(positive);
                Assert.Equal(1, SpiritualWoundSourceWitness.Validate(positive.Source).MaximumSeverityRank);
                next = BuildReceipt(positive); break; // The typed resolver constructs this source's own guardian instance.
            case "zero-injury":
                var fabricated = SpiritualExistingPressureTestData.CreateLedger(witness).Root;
                var injuryRow = fabricated["sources"]![0]!.DeepClone().AsObject(); var none = fabricated["decisions"]![0]!.DeepClone().AsObject();
                var ordinal = next["nextSourceOrdinal"]!.GetValue<int>(); injuryRow["ordinal"] = ordinal;
                injuryRow["instanceId"] = row["instanceId"]!.DeepClone(); injuryRow["instanceSourceOrdinal"] = 1;
                none["ordinal"] = next["nextDecisionOrdinal"]!.DeepClone(); none["instanceId"] = row["instanceId"]!.DeepClone();
                none["decisionFingerprint"] = SpiritualWoundStateJson.Hash(none, "decision", "decisionFingerprint");
                next["sources"]!.AsArray().Add(injuryRow); next["nextSourceOrdinal"] = ordinal + 1;
                next["decisions"]!.AsArray().Add(none); next["nextDecisionOrdinal"] = next["nextDecisionOrdinal"]!.GetValue<int>() + 1;
                Assert.Equal(witness["conflictId"]!.GetValue<string>(), next["instances"]!.AsArray().Single(i =>
                    i!["instanceId"]!.GetValue<string>() == row["instanceId"]!.GetValue<string>())!["displayConflictId"]!.GetValue<string>()); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        var before = next.ToJsonString(); var parsed = SpiritualWoundOpportunityReceiptState.Parse(before, SpiritualWoundOpportunityReceiptState.StatePath);
        if (change is "comparison-prefix" or "injury-prefix")
        {
            Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
            Assert.Equal("conflict", SpiritualWoundOpportunityReceiptState.PlanAppend(state, parsed.State!).Disposition);
        }
        else { Assert.False(parsed.IsValid); Assert.Null(parsed.State); }
        Assert.Equal(before, next.ToJsonString());
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_receipt_boundary", change, parsed.IsValid, parsed.Issues }));
    }

    /// <summary>The real normalizer History reader and retained actual-log matcher preserve the complete guardian creation anchor.</summary>
    /// <param name="change">Exact, missing/changed creation or parent, lawful later healing, or missing/changed actual log.</param>
    [Theory]
    [InlineData("exact")] [InlineData("missing")] [InlineData("changed")] [InlineData("parent-changed")]
    [InlineData("later-healing")] [InlineData("log-missing")] [InlineData("log-changed")]
    public async Task GuardianZero_HistoryAndSignedLogRemainExact(string change)
    {
        var fixture = SpiritualGuardianZeroPressureTestData.Create(3); AssertLawful(fixture);
        var root = BuildReceipt(fixture); _ = ReadReceipt(root);
        var history = fixture.Retained.History.DeepClone().AsObject(); var transitions = history["transitions"]!.AsArray();
        var first = transitions[0]!.AsObject(); var active = fixture.Conflict["activeConflict"]!.DeepClone().AsObject();
        active["oppositionSideStrain"] = "overwhelmed"; active["exchangeLog"]![0] = fixture.Actual.DeepClone();
        switch (change)
        {
            case "exact": break;
            case "missing": transitions.Clear(); history["nextOrdinal"] = 1; break;
            case "changed": first["afterFingerprint"] = "sha256:" + new string('d', 64); break;
            case "parent-changed":
                var witness = root["strainComparisons"]![0]!["witness"]!.AsObject(); var parent = SpiritualExistingPressureTestData.ReadParent(witness);
                parent["display"]!["description"] = "Different complete retained guardian parent description.";
                Assert.True(WoundMaterializationContract.Parse(parent.ToJsonString(), "guardianZero.changedParent").IsValid);
                witness["strainPreimage"]!["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(parent);
                SpiritualExistingPressureTestData.Seal(witness); Assert.Equal(0, SpiritualWoundSourceWitness.Validate(witness).MaximumSeverityRank); break;
            case "later-healing":
                var heal = first.DeepClone().AsObject(); heal["ordinal"] = 2; heal["woundTransitionOrdinal"] = 2; heal["turn"] = 47; heal["kind"] = "heal";
                heal["transitionId"] = "transition_guardian_zero_heal"; heal["operationKey"] = "operation_guardian_zero_heal";
                heal["eventRef"] = "event_guardian_zero_heal"; heal["terminal"] = true;
                heal["beforeFingerprint"] = first["afterFingerprint"]!.DeepClone(); heal["afterFingerprint"] = "sha256:" + new string('d', 64);
                transitions.Add(heal); history["nextOrdinal"] = 3; break;
            case "log-missing": active["exchangeLog"]!.AsArray().Clear(); break;
            case "log-changed": active["exchangeLog"]![0]!["incomingAction"]!["summary"] = "Different accepted incoming action."; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        var state = ReadReceipt(root); var parsed = WoundHistoryState.Parse(history.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
        var matches = change is not ("missing" or "changed" or "parent-changed");
        Assert.Equal(matches, state.HasExactStrainHistory(parsed.State!));
        var historyBefore = history.ToJsonString(); var receiptBefore = root.ToJsonString(); var activeBefore = active.ToJsonString(); var reads = 0;
        Task Act() => CanonicalStateNormalizer.ValidateSpiritualReceiptPublicationHistoryAsync(state, root, () =>
        { reads++; return Task.FromResult<string?>(history.ToJsonString()); });
        if (matches) await Act(); else await Assert.ThrowsAsync<InvalidDataException>(Act);
        Assert.Equal(1, reads);
        Assert.Equal(change is "log-missing" or "log-changed" ? SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Mismatch :
            SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact, state.MatchRetainedPressureHistory("chaos_sea", active, 48));
        Assert.Equal(historyBefore, history.ToJsonString()); Assert.Equal(receiptBefore, root.ToJsonString()); Assert.Equal(activeBefore, active.ToJsonString());
    }

    /// <summary>Checks complete detached ancestry and both readers before any negative mutation; original authority is not implied.</summary>
    /// <param name="fixture">Fresh independently authored hostile DATA with complete retained ancestry.</param>
    /// <returns>The complete validated comparison, useful as a nonnull precondition for out-DATA clearing checks.</returns>
    private static SpiritualStrainPreimageData AssertLawful(GuardianZeroPressureData fixture)
    {
        Assert.Empty(SpiritualGuardianAttributionDataCheck.Check(fixture.Retained, fixture.Profiles, fixture.Conflict, fixture.Rank));
        Assert.Equal(fixture.ActualCalculation, SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var data));
        var comparison = Assert.IsType<SpiritualStrainPreimageData>(data);
        Assert.Equal(fixture.OrdinaryCalculation, comparison.OrdinaryCalculation);
        Assert.Equal(5, SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual).Version);
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawOrdinaryBytes).Fingerprint, comparison.OriginalInputFingerprint);
        return comparison;
    }

    /// <summary>Builds the source's own resolved guardian schema1 skeleton before adding one comparison; caller decides final family validity.</summary>
    /// <param name="fixture">Complete zero or positive-sentinel DATA whose own original bytes drive the typed resolver.</param>
    private static JsonObject BuildReceipt(GuardianZeroPressureData fixture)
    {
        var root = fixture.Retained.Receipts.DeepClone().AsObject(); _ = ReadReceipt(root);
        var evidence = fixture.Source["turnEvidence"]!;
        var resolved = SpiritualWoundFirstOfferInstanceResolver.Resolve(new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(root.ToJsonString())),
            new CanonicalBeforeImage(true, fixture.RawOrdinaryBytes), "chaos_sea", evidence["sessionId"]!.GetValue<string>(),
            evidence["requestId"]!.GetValue<string>(), evidence["snapshotToken"]!.GetValue<string>(), 46);
        Assert.Empty(resolved.Issues); Assert.NotNull(resolved.ProposedRow);
        root["instances"]!.AsArray().Add(resolved.ProposedRow!.DeepClone()); root["nextInstanceOrdinal"] = resolved.ProposedRow["ordinal"]!.GetValue<int>() + 1;
        _ = ReadReceipt(root);
        root["schemaVersion"] = 3; root["healingSources"] = new JsonArray(); root["healingDecisions"] = new JsonArray();
        root["nextHealingSourceOrdinal"] = 1; root["nextHealingDecisionOrdinal"] = 1; root["nextStrainComparisonOrdinal"] = 2;
        root["strainComparisons"] = new JsonArray(new JsonObject { ["ordinal"] = 1, ["instanceId"] = resolved.InstanceId,
            ["instanceComparisonOrdinal"] = 1, ["witness"] = fixture.Source.DeepClone() });
        return root;
    }

    /// <summary>Reads immutable strict receipt DATA and rejects any unrelated fixture failure before a control begins.</summary>
    /// <param name="root">Complete independently authored receipt root to parse without mutation.</param>
    private static SpiritualWoundOpportunityReceiptState ReadReceipt(JsonObject root)
    {
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues)); return Assert.IsType<SpiritualWoundOpportunityReceiptState>(parsed.State);
    }
}
