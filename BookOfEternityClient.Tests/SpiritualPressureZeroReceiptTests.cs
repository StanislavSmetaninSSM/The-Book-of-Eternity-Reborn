using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks singleton v3 zero-comparison receipt DATA without granting source or publication authority.</summary>
public sealed partial class SpiritualPressureZeroReceiptTests
{
    /// <summary>Appends exactly one comparison while preserving every accepted row and inventing no decision for zero.</summary>
    [Theory]
    [InlineData("zero-only", 3)]
    [InlineData("zero-only", 4)]
    [InlineData("prior-injury", 3)]
    [InlineData("mixed-request", 3)]
    public void PressureZeroReceiptAppendsWithoutDecision(string mode, int rank)
    {
        var (before, next) = CreateLedger(mode, rank);
        var baseline = ReadLedger(before);
        var beforeBytes = before.ToJsonString(); var nextBytes = next.ToJsonString();
        var state = ReadLedger(next);
        var witness = Assert.Single(next["strainComparisons"]!.AsArray())!["witness"]!.AsObject();
        AssertZeroWitness(witness, rank);
        Assert.Equal(3, next["schemaVersion"]!.GetValue<int>());
        Assert.Equal(2, next["nextStrainComparisonOrdinal"]!.GetValue<int>());
        Assert.Empty(next["healingSources"]!.AsArray()); Assert.Empty(next["healingDecisions"]!.AsArray());
        Assert.False(state.HasCombatHealing);
        foreach (var table in new[] { "instances", "closures", "sources", "decisions" })
        {
            var old = before[table]!.AsArray(); var current = next[table]!.AsArray();
            Assert.Equal(old.Count + (mode == "mixed-request" && table is ("sources" or "decisions") ? 1 : 0), current.Count);
            for (var index = 0; index < old.Count; index++) Assert.True(JsonNode.DeepEquals(old[index], current[index]));
        }
        Assert.DoesNotContain(next["decisions"]!.AsArray(), row =>
            row!["sourceId"]!.GetValue<string>() == witness["sourceId"]!.GetValue<string>());
        if (mode == "prior-injury") Assert.Equal("materialize", next["decisions"]![0]!["decision"]!.GetValue<string>());
        if (mode == "mixed-request")
        {
            var positive = next["sources"]![1]!["witness"]!.AsObject();
            Assert.Equal(2, SpiritualWoundSourceWitness.Validate(positive).MaximumSeverityRank);
            Assert.Equal(positive["turnEvidence"]!["requestId"]!.GetValue<string>(), witness["turnEvidence"]!["requestId"]!.GetValue<string>());
            Assert.Equal("none", next["decisions"]![1]!["decision"]!.GetValue<string>());
        }
        var append = SpiritualWoundOpportunityReceiptState.PlanAppend(baseline, state);
        Assert.Equal("appended", append.Disposition);
        var appended = Assert.IsType<SpiritualWoundOpportunityReceiptState>(append.After);
        Assert.Equal(SpiritualWoundOpportunityReceiptState.SerializeCanonical(state),
            SpiritualWoundOpportunityReceiptState.SerializeCanonical(appended));
        Assert.Equal("exact_replay", SpiritualWoundOpportunityReceiptState.PlanAppend(state, ReadLedger(next)).Disposition);
        Assert.Equal(beforeBytes, before.ToJsonString()); Assert.Equal(nextBytes, next.ToJsonString());

        // Neither parsed DATA nor an append result can retain the caller's mutable witness.
        var retainedBytes = SpiritualWoundOpportunityReceiptState.SerializeCanonical(state);
        witness["maximumSeverityRank"] = 4;
        Assert.Equal(retainedBytes, SpiritualWoundOpportunityReceiptState.SerializeCanonical(state));
        Assert.Equal(retainedBytes, SpiritualWoundOpportunityReceiptState.SerializeCanonical(appended));
        Assert.Equal("exact_replay", SpiritualWoundOpportunityReceiptState.PlanAppend(state, state).Disposition);
    }

    /// <summary>Resealed witnesses remain intrinsically valid while shared receipt identity, ordering and dice joins refuse contradictions.</summary>
    [Theory]
    [InlineData("extra-row-field")]
    [InlineData("comparison-ordinal")]
    [InlineData("instance-ordinal")]
    [InlineData("foreign-instance")]
    [InlineData("duplicate-source")]
    [InlineData("duplicate-coordinate")]
    [InlineData("injury-coordinate")]
    [InlineData("original-context")]
    [InlineData("original-request")]
    [InlineData("source-ordinal")]
    [InlineData("dice-reuse")]
    public void PressureZeroReceiptRejectsCrossFamilyContradictions(string change)
    {
        var (_, root) = CreateLedger("mixed-request");
        _ = ReadLedger(root);
        var row = root["strainComparisons"]![0]!.AsObject(); var witness = row["witness"]!.AsObject();
        switch (change)
        {
            case "extra-row-field": row["unowned"] = true; break;
            case "comparison-ordinal": row["ordinal"] = 2; break;
            case "instance-ordinal": row["instanceComparisonOrdinal"] = 2; break;
            case "foreign-instance": row["instanceId"] = "foreign_instance"; break;
            case "duplicate-source":
            case "duplicate-coordinate":
                var duplicate = row.DeepClone().AsObject();
                duplicate["ordinal"] = 2; duplicate["instanceComparisonOrdinal"] = 2;
                if (change == "duplicate-coordinate")
                {
                    duplicate["witness"]!["turnEvidence"]!["requestId"] = "request_pressure_zero_duplicate";
                    duplicate["witness"]!["turnEvidence"]!["snapshotToken"] = "snapshot_pressure_zero_duplicate";
                    Seal(duplicate["witness"]!.AsObject()); AssertZeroWitness(duplicate["witness"]!.AsObject());
                }
                root["strainComparisons"]!.AsArray().Add(duplicate); root["nextStrainComparisonOrdinal"] = 3;
                break;
            case "injury-coordinate":
                ChangeExchangeId(witness, root["sources"]![1]!["witness"]!["exchangeId"]!.GetValue<string>());
                break;
            case "original-context": witness["turnEvidence"]!["originalSnapshotFingerprint"] = "sha256:" + new string('c', 64); break;
            case "original-request": witness["turnEvidence"]!["requestId"] = "request-a"; break;
            case "source-ordinal": witness["sourceOrdinal"] = 0; break;
            case "dice-reuse": SetDiceClaims(witness, 0, 1); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        Seal(witness); AssertZeroWitness(witness);
        AssertInvalidLedger(root);
    }

    /// <summary>Even individually valid replacement DATA cannot rewrite an accepted comparison or injury prefix.</summary>
    [Theory]
    [InlineData("comparison")]
    [InlineData("injury")]
    public void PressureZeroReceiptPreservesImmutablePrefixes(string family)
    {
        var (_, root) = CreateLedger("prior-injury"); var before = ReadLedger(root);
        var next = root.DeepClone().AsObject();
        var source = (family == "comparison" ? next["strainComparisons"]![0]!["witness"] : next["sources"]![0]!["witness"])!.AsObject();
        var actual = ReadActual(source); actual["summary"] = "A different retained narration.";
        source["exchangeJsonBase64"] = Encode(actual);
        if (family == "comparison")
        {
            var ordinary = ReadOrdinary(source); ordinary["summary"] = actual["summary"]!.DeepClone();
            source["strainPreimage"]!["ordinaryExchangeJsonBase64"] = Encode(ordinary); Seal(source);
        }
        else SpiritualStrainReceiptJoinTests.SealSourceAndDecision(next, 0);
        var candidate = ReadLedger(next);
        Assert.Equal("conflict", SpiritualWoundOpportunityReceiptState.PlanAppend(before, candidate).Disposition);
    }

    /// <summary>Neither table may add another row to an original request already accepted through the other table.</summary>
    [Theory]
    [InlineData("comparison-first")]
    [InlineData("injury-first")]
    public void PressureZeroReceiptCannotReopenAcceptedRequest(string acceptedFamily)
    {
        var (_, complete) = CreateLedger("mixed-request"); var next = ReadLedger(complete);
        var accepted = complete.DeepClone().AsObject();
        if (acceptedFamily == "comparison-first")
        {
            accepted["sources"]!.AsArray().RemoveAt(1); accepted["decisions"]!.AsArray().RemoveAt(1);
            accepted["nextSourceOrdinal"] = 2; accepted["nextDecisionOrdinal"] = 2;
        }
        else
        {
            accepted["schemaVersion"] = 1;
            foreach (var name in new[] { "strainComparisons", "nextStrainComparisonOrdinal", "healingSources", "healingDecisions",
                         "nextHealingSourceOrdinal", "nextHealingDecisionOrdinal" }) accepted.Remove(name);
        }
        Assert.Equal("conflict", SpiritualWoundOpportunityReceiptState.PlanAppend(ReadLedger(accepted), next).Disposition);
    }

    /// <summary>Positive v3 belongs only to the injury table, and zero v3 cannot acquire a fabricated none decision.</summary>
    [Theory]
    [InlineData("positive-comparison")]
    [InlineData("zero-injury")]
    public void PressureZeroReceiptKeepsPositiveAndZeroFamiliesSeparate(string change)
    {
        var positive = SpiritualExistingPressureTestData.CreateData();
        Assert.Equal(2, SpiritualWoundSourceWitness.Validate(positive).MaximumSeverityRank);
        _ = ReadLedger(SpiritualExistingPressureTestData.CreateLedger(positive).Root);
        var (_, zeroRoot) = CreateLedger("zero-only"); _ = ReadLedger(zeroRoot);
        if (change == "positive-comparison")
        {
            zeroRoot["strainComparisons"]![0]!["witness"] = positive.DeepClone(); AssertInvalidLedger(zeroRoot);
        }
        else
        {
            var zero = zeroRoot["strainComparisons"]![0]!["witness"]!.AsObject(); AssertZeroWitness(zero);
            var injury = SpiritualExistingPressureTestData.CreateLedger(zero).Root;
            Assert.Equal("none", injury["decisions"]![0]!["decision"]!.GetValue<string>());
            AssertInvalidLedger(injury);
        }
    }

    /// <summary>Coherent authored hostile v5-zero DATA accepts first; false actual/ordinary maxima then fail arithmetic inside its own guardian ledger.</summary>
    /// <param name="change">One declared maximum contradicted by literal actual7/max0 or ordinary2/max0.</param>
    [Theory]
    [InlineData("actual-maximum")] [InlineData("ordinary-maximum")]
    public void PressureZeroReceiptRejectsContradictoryGuardianZeroCalculations(string change)
    {
        var fixture = SpiritualGuardianZeroPressureTestData.Create(3);
        Assert.Empty(SpiritualGuardianAttributionDataCheck.Check(fixture.Retained, fixture.Profiles, fixture.Conflict));
        Assert.Equal(fixture.ActualCalculation, SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var baseline));
        Assert.Equal(fixture.OrdinaryCalculation, Assert.IsType<SpiritualStrainPreimageData>(baseline).OrdinaryCalculation);
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawOrdinaryBytes).Fingerprint, baseline!.OriginalInputFingerprint);
        var root = fixture.Retained.Receipts.DeepClone().AsObject(); _ = ReadLedger(root);
        var evidence = fixture.Source["turnEvidence"]!;
        var resolved = SpiritualWoundFirstOfferInstanceResolver.Resolve(new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(root.ToJsonString())),
            new CanonicalBeforeImage(true, fixture.RawOrdinaryBytes), "chaos_sea", evidence["sessionId"]!.GetValue<string>(),
            evidence["requestId"]!.GetValue<string>(), evidence["snapshotToken"]!.GetValue<string>(), 46);
        Assert.Empty(resolved.Issues); Assert.NotNull(resolved.ProposedRow);
        root["instances"]!.AsArray().Add(resolved.ProposedRow!.DeepClone());
        root["nextInstanceOrdinal"] = resolved.ProposedRow["ordinal"]!.GetValue<int>() + 1; _ = ReadLedger(root);
        root["schemaVersion"] = 3; root["healingSources"] = new JsonArray(); root["healingDecisions"] = new JsonArray();
        root["nextHealingSourceOrdinal"] = 1; root["nextHealingDecisionOrdinal"] = 1; root["nextStrainComparisonOrdinal"] = 2;
        root["strainComparisons"] = new JsonArray(new JsonObject { ["ordinal"] = 1, ["instanceId"] = resolved.InstanceId,
            ["instanceComparisonOrdinal"] = 1, ["witness"] = fixture.Source.DeepClone() });
        _ = ReadLedger(root);
        var source = root["strainComparisons"]![0]!["witness"]!.AsObject();
        Assert.Equal(source["conflictId"]!.GetValue<string>(), resolved.ProposedRow["displayConflictId"]!.GetValue<string>());
        if (change == "actual-maximum") source["maximumSeverityRank"] = 1;
        else source["strainPreimage"]!["ordinaryCalculation"]!["maximumSeverityRank"] = 1;
        Seal(source); var bytes = source.ToJsonString(); SpiritualStrainPreimageData? escaped = baseline;
        var error = Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out escaped));
        Assert.Equal(change == "actual-maximum" ? "Source severity differs from retained exchange calculation." :
            "Declared ordinary guardian calculation differs.", error.Message);
        Assert.Null(escaped); Assert.Equal(bytes, source.ToJsonString());
        Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadIntrinsic(source, ReadActual(source)));
        AssertInvalidLedger(root);
    }

    /// <summary>A made-up private key with or without a receipt identity cannot turn detached valid DATA into an exhausted owner.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PressureZeroReceiptRejectsForgedSourceOnlyOwner(bool hasReceiptIdentity)
    {
        var source = SpiritualPressureZeroTestData.CreateData(3).Source; AssertZeroWitness(source);
        var signed = new ValidationService.SpiritualSignedC1Origin(new CanonicalBeforeImage(false, null),
            new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"activeConflict\":{\"conflictId\":\"conflict-a\"}}")),
            source["turnEvidence"]!["originalSnapshotFingerprint"]!.GetValue<string>());
        var identity = hasReceiptIdentity ? new object() : null;
        var owner = new ValidationService.SpiritualOriginalTurnCapture.AuthenticatedC3SourceOnly(new object(), signed,
            null!, "chaos_sea", null, identity, [source]);
        var original = source.ToJsonString(); source["maximumSeverityRank"] = 4;
        var detached = Assert.Single(owner.ReadStrainComparisons()); Assert.Equal(original, detached.ToJsonString());
        detached["strainPreimage"]!["ordinaryCalculation"]!["traumaPressure"] = 999;
        Assert.Equal(original, Assert.Single(owner.ReadStrainComparisons()).ToJsonString());
        Assert.Same(identity, owner.ReceiptIdentity); Assert.False(owner.IsAuthentic);
        var result = SpiritualWoundDeclineReceiptReducer.ReduceSourceOnly(owner);
        Assert.Null(result.AfterImage);
        Assert.Equal("spiritual_c3_source_only_owner_missing", Assert.Single(result.Issues).Code);
        Assert.Equal(original, Assert.Single(owner.ReadStrainComparisons()).ToJsonString());
    }

    /// <summary>Builds fresh independent receipt DATA; positive mixed sources remain ordinary so only the last exchange carries v3.</summary>
    private static (JsonObject Before, JsonObject Next) CreateLedger(string mode, int rank = 3)
    {
        var source = SpiritualPressureZeroTestData.CreateData(rank).Source;
        var before = SpiritualStrainReceiptJoinTests.CreateLedger();
        before["sources"]!.AsArray().RemoveAt(1); before["decisions"]!.AsArray().RemoveAt(1);
        before["nextSourceOrdinal"] = 2; before["nextDecisionOrdinal"] = 2;
        if (mode == "zero-only")
        {
            before["sources"]!.AsArray().Clear(); before["decisions"]!.AsArray().Clear();
            before["nextSourceOrdinal"] = 1; before["nextDecisionOrdinal"] = 1;
        }
        var next = before.DeepClone().AsObject();
        if (mode == "mixed-request")
        {
            var positive = SpiritualExistingPressureTestData.CreateData(); Assert.True(positive.Remove("strainPreimage"));
            ChangeExchangeId(positive, "exchange_pressure_zero_positive_44");
            positive["coordinate"] = "activeConflict.exchangeLog[1].player";
            SetMixedContext(positive, 0, 0); SetDiceClaims(positive, 0, 1); Seal(positive);
            var addition = SpiritualExistingPressureTestData.CreateLedger(positive).Root;
            var injury = addition["sources"]![0]!.DeepClone().AsObject();
            injury["ordinal"] = 2; injury["instanceSourceOrdinal"] = 2;
            next["sources"]!.AsArray().Add(injury); next["nextSourceOrdinal"] = 3;
            var decision = addition["decisions"]![0]!.DeepClone().AsObject();
            decision["ordinal"] = 2; decision["decisionId"] = "decision_pressure_zero_positive_44";
            decision["opportunityRef"] = "opportunity_pressure_zero_positive_44";
            next["decisions"]!.AsArray().Add(decision); next["nextDecisionOrdinal"] = 3;
            SpiritualStrainReceiptJoinTests.SealSourceAndDecision(next, 1);
            SetMixedContext(source, 1, 1); SetDiceClaims(source, 2, 3);
            var ordinary = ReadOrdinary(source);
            var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + new JsonObject
            {
                ["schemaVersion"] = 1, ["activeConflict"] = new JsonObject
                {
                    ["exchangeLog"] = new JsonArray(new JsonObject(), ReadActual(positive), ordinary),
                    ["playerSideStrain"] = "strained"
                }
            }.ToJsonString() + "\n")).ToArray();
            source["strainPreimage"]!["originalInput"]!["contentFingerprint"] = new CanonicalBeforeImage(true, raw).Fingerprint;
            Seal(source);
        }
        next["schemaVersion"] = 3;
        next["nextHealingSourceOrdinal"] = 1; next["nextHealingDecisionOrdinal"] = 1;
        next["healingSources"] = new JsonArray(); next["healingDecisions"] = new JsonArray();
        next["nextStrainComparisonOrdinal"] = 2;
        next["strainComparisons"] = new JsonArray(new JsonObject
        {
            ["ordinal"] = 1, ["instanceId"] = next["instances"]![0]!["instanceId"]!.DeepClone(),
            ["instanceComparisonOrdinal"] = 1, ["witness"] = source.DeepClone()
        });
        return (before, next);
    }

    /// <summary>Authors one shared two-exchange request without changing any parent or actual calculation.</summary>
    private static void SetMixedContext(JsonObject source, int sourceOrdinal, int exchangeOrdinal)
    {
        source["sourceOrdinal"] = sourceOrdinal; source["exchangeOrdinal"] = exchangeOrdinal;
        var evidence = source["turnEvidence"]!;
        evidence["bounds"]!["exchangeCount"] = 2; evidence["bounds"]!["sourceSlotCount"] = 4; evidence["bounds"]!["diceCount"] = 4;
        evidence["acceptedD20Values"] = new JsonArray(4, 12, 4, 12);
    }

    /// <summary>Rebinds both payloads and their exact claims, preserving independent dice values4/12.</summary>
    private static void SetDiceClaims(JsonObject source, int playerIndex, int oppositionIndex)
    {
        source["selectedDiceIndices"] = new JsonArray(playerIndex, oppositionIndex);
        var claims = new JsonArray();
        foreach (var (index, value) in new[] { (playerIndex, 4), (oppositionIndex, 12) })
        {
            var claim = new JsonObject { ["sourceIndex"] = index, ["value"] = value,
                ["exchangeOrdinal"] = source["exchangeOrdinal"]!.DeepClone(), ["claimFingerprint"] = "" };
            claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim); claims.Add(claim);
        }
        source["turnEvidence"]!["diceClaims"] = claims;
        var actual = ReadActual(source); SetIndices(actual); source["exchangeJsonBase64"] = Encode(actual);
        if (source["strainPreimage"] is JsonObject proof)
        {
            var ordinary = ReadOrdinary(source); SetIndices(ordinary); proof["ordinaryExchangeJsonBase64"] = Encode(ordinary);
        }
        void SetIndices(JsonObject exchange)
        {
            exchange["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = playerIndex;
            exchange["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = oppositionIndex;
        }
    }

    /// <summary>Changes one intrinsic exchange identity consistently, without manufacturing its accepted membership.</summary>
    private static void ChangeExchangeId(JsonObject source, string exchangeId)
    {
        source["exchangeId"] = exchangeId;
        var actual = ReadActual(source); actual["exchangeId"] = exchangeId; source["exchangeJsonBase64"] = Encode(actual);
        if (source["strainPreimage"] is JsonObject proof)
        {
            var ordinary = ReadOrdinary(source); ordinary["exchangeId"] = exchangeId; proof["ordinaryExchangeJsonBase64"] = Encode(ordinary);
        }
        Seal(source);
    }

    /// <summary>Requires actual7/max0 and ordinary2/max0 through the real witness before any containing-ledger negative.</summary>
    private static void AssertZeroWitness(JsonObject source, int rank = 3)
    {
        var bytes = source.ToJsonString();
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained);
        Assert.Equal(SpiritualPressureZeroTestData.ActualCalculation(), calculation);
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(3, data.Version); Assert.Equal(rank, Assert.Single(data.Parents).ParentWound.Severity.Rank);
        Assert.Equal(SpiritualPressureZeroTestData.OrdinaryCalculation(), data.OrdinaryCalculation);
        Assert.Equal(bytes, source.ToJsonString());
    }

    /// <summary>Parses independent receipt DATA and verifies the parser does not edit its caller.</summary>
    private static SpiritualWoundOpportunityReceiptState ReadLedger(JsonObject root)
    {
        var bytes = root.ToJsonString(); var parsed = SpiritualWoundOpportunityReceiptState.Parse(bytes, "unit.pressureZeroReceipt");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues)); Assert.Equal(bytes, root.ToJsonString());
        return Assert.IsType<SpiritualWoundOpportunityReceiptState>(parsed.State);
    }

    /// <summary>Requires the containing parser to refuse without a state escape or input mutation.</summary>
    private static void AssertInvalidLedger(JsonObject root)
    {
        var bytes = root.ToJsonString(); var parsed = SpiritualWoundOpportunityReceiptState.Parse(bytes, "unit.pressureZeroInvalid");
        Assert.False(parsed.IsValid); Assert.Null(parsed.State);
        Assert.Equal("spiritual_wound_receipt_invalid_state", Assert.Single(parsed.Issues).Code);
        Assert.Equal(bytes, root.ToJsonString());
    }

    /// <summary>Reads a detached actual payload using the established fixture codec.</summary>
    private static JsonObject ReadActual(JsonObject source) => SpiritualExistingPressureTestData.ReadActual(source);
    /// <summary>Reads a detached ordinary payload using the established fixture codec.</summary>
    private static JsonObject ReadOrdinary(JsonObject source) => SpiritualExistingPressureTestData.ReadOrdinary(source);
    /// <summary>Encodes comparison DATA without creating accepted provenance.</summary>
    private static string Encode(JsonNode value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(value)));
    /// <summary>Reseals only lexical fixture DATA after a deliberate mutation.</summary>
    private static void Seal(JsonObject source) => SpiritualExistingPressureTestData.Seal(source);
}
