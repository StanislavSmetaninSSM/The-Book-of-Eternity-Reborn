using System.Collections;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Eight separate desired successes catch missing v7 dispatch, lost final parents, equality evidence and max0 support.</summary>
/// <param name="output">Complete input and actual reader observations, including failures before unconditional success assertions.</param>
public sealed class SpiritualGuardianGroupPressureIntrinsicTests(ITestOutputHelper output)
{
    /// <summary>Catches unsupported full-witness v7 or a singleton-only correction for the mixed N2 group.</summary>
    [Fact]
    public void GuardianGroup_N2MixedFullWitnessAccepts() => AssertReader(GuardianGroupPressureCase.MixedPair,
        direct: false, [3, 4], PositiveOrdinary(), PositiveActual());

    /// <summary>Independently catches a direct reader bypass/refusal even if full-witness reading fails first.</summary>
    [Fact]
    public void GuardianGroup_N2MixedDirectIntrinsicAccepts() => AssertReader(GuardianGroupPressureCase.MixedPair,
        direct: true, [3, 4], PositiveOrdinary(), PositiveActual());

    /// <summary>Catches lost final parent after saturation in the complete full-witness group.</summary>
    [Fact]
    public void GuardianGroup_N3MixedFullWitnessAccepts() => AssertReader(GuardianGroupPressureCase.MixedTriple,
        direct: false, [3, 4, 3], PositiveOrdinary(), PositiveActual());

    /// <summary>Catches direct-reader truncation of the third parent despite the already broken scalar.</summary>
    [Fact]
    public void GuardianGroup_N3MixedDirectIntrinsicAccepts() => AssertReader(GuardianGroupPressureCase.MixedTriple,
        direct: true, [3, 4, 3], PositiveOrdinary(), PositiveActual());

    /// <summary>Catches dropping the full comparison when ordinaryIV and actualIV are equal.</summary>
    [Fact]
    public void GuardianGroup_N2EqualCapFullWitnessAccepts() => AssertReader(GuardianGroupPressureCase.EqualCapPair,
        direct: false, [3, 4], PositiveActual(), PositiveActual());

    /// <summary>Catches the independent direct-reader equality gate discarding ordinary/raw/group evidence.</summary>
    [Fact]
    public void GuardianGroup_N2EqualCapDirectIntrinsicAccepts() => AssertReader(GuardianGroupPressureCase.EqualCapPair,
        direct: true, [3, 4], PositiveActual(), PositiveActual());

    /// <summary>Catches an invented positive-only v7 witness restriction on the accepted lawful margin4/resilience5 row.</summary>
    [Fact]
    public void GuardianGroup_N2LowMarginZeroFullWitnessAccepts() => AssertReader(GuardianGroupPressureCase.LowMarginZeroPair,
        direct: false, [3, 4], ZeroOrdinary(), ZeroActual());

    /// <summary>Catches the same nonnegative-maximum contract independently through direct intrinsic reading.</summary>
    [Fact]
    public void GuardianGroup_N2LowMarginZeroDirectIntrinsicAccepts() => AssertReader(GuardianGroupPressureCase.LowMarginZeroPair,
        direct: true, [3, 4], ZeroOrdinary(), ZeroActual());

    /// <summary>Runs one existing typed API, logs its actual result, then unconditionally requires complete desired success.</summary>
    private void AssertReader(GuardianGroupPressureCase contour, bool direct, int[] ranks,
        SpiritualWoundCalculation expectedOrdinary, SpiritualWoundCalculation expectedActual)
    {
        var fixture = SpiritualGuardianGroupPressureTestData.Create(contour);
        var fixtureBefore = JsonSerializer.Serialize(fixture);
        var source = SpiritualGuardianGroupPressureProofData.CreateSource(fixture, expectedOrdinary);
        var sourceBefore = source.ToJsonString();
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_intrinsic_inputs", contour, direct,
            expectedOrdinary, expectedActual, source, fixture,
            prerequisites = "unchanged Task1 records accepted 8/8; no live authority supplied" }));
        SpiritualStrainPreimageData? retained = null;
        SpiritualWoundCalculation? calculation = null;
        var error = Record.Exception(() =>
        {
            if (direct) retained = SpiritualStrainPreimageData.ReadIntrinsic(source, fixture.Actual);
            else calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out retained);
        });
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_intrinsic_desired_success", contour, direct,
            error = error?.ToString(), exceptionDataCount = error?.Data.Count,
            exceptionData = error?.Data.Cast<DictionaryEntry>().Select(entry => new
                { key = entry.Key?.ToString(), value = entry.Value?.ToString() }).ToArray(),
            comparisonReturned = retained is not null, calculation,
            causalTarget = "existing supported-version boundary; observed exception/stack, not a claimed RED until execution" }));
        Assert.Equal(sourceBefore, source.ToJsonString());
        Assert.Equal(fixtureBefore, JsonSerializer.Serialize(fixture));
        Assert.Null(error); // Missing-feature refusal must fail this leaf, never count as its success.
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        if (!direct) { Assert.Equal(expectedActual, calculation); Assert.Same(calculation, data.ActualCalculation); }
        Assert.Equal(7, data.Version);
        Assert.Equal(expectedOrdinary, data.OrdinaryCalculation);
        Assert.Equal(expectedActual, data.ActualCalculation);
        Assert.Equal(ranks, data.Parents.Select(row => row.ParentWound.Severity.Rank));
        Assert.Equal(ranks.Length, data.Parents.Count);
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        var proof = source["strainPreimage"]!.AsObject();
        Assert.Equal(new[] { "inputRoleRevision", "ordinaryCalculation", "ordinaryExchangeJsonBase64", "originalInput", "parents", "version" },
            proof.Select(row => row.Key).Order(StringComparer.Ordinal));
        Assert.Equal(SpiritualGuardianGroupPressureProofData.Role, proof["inputRoleRevision"]!.GetValue<string>());
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes(fixture.Ordinary.ToJsonString())),
            proof["ordinaryExchangeJsonBase64"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(fixture.OriginalInput, proof["originalInput"]));
        Assert.Equal(new[] { "contentFingerprint", "exchangePointer", "existed", "path" },
            proof["originalInput"]!.AsObject().Select(row => row.Key).Order(StringComparer.Ordinal));
        Assert.Equal(AfterlifeSpiritualConflictState.StatePath, proof["originalInput"]!["path"]!.GetValue<string>());
        Assert.True(proof["originalInput"]!["existed"]!.GetValue<bool>());
        Assert.Equal(new[] { "maximumSeverityRank", "traumaPressure" }, proof["ordinaryCalculation"]!.AsObject().Select(row => row.Key).Order(StringComparer.Ordinal));
        Assert.Equal((expectedOrdinary.TraumaPressure, expectedOrdinary.MaximumSeverityRank),
            (proof["ordinaryCalculation"]!["traumaPressure"]!.GetValue<long>(), proof["ordinaryCalculation"]!["maximumSeverityRank"]!.GetValue<int>()));
        var raw = Convert.FromBase64String(fixture.RawOrdinaryBase64);
        Assert.True(raw.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.Equal(" \n" + fixture.OrdinaryRoot.ToJsonString() + "\n", SpiritualWoundStateJson.DecodeUtf8JsonText(raw));
        Assert.True(JsonNode.DeepEquals(fixture.OrdinaryRoot, SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(raw))));
        Assert.Equal(new CanonicalBeforeImage(true, raw).Fingerprint, data.OriginalInputFingerprint);
        Assert.Equal(fixture.OriginalInput["contentFingerprint"]!.GetValue<string>(), data.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", data.OriginalExchangePointer);
        for (var index = 0; index < ranks.Length; index++)
        {
            var expected = fixture.Retained.Parents[index];
            var parent = data.Parents[index];
            var row = proof["parents"]![index]!.AsObject();
            Assert.Equal(new[] { "componentId", "effectId", "parentWoundJsonBase64" }, row.Select(field => field.Key).Order(StringComparer.Ordinal));
            Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes(expected.ToJsonString())), row["parentWoundJsonBase64"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(parent.ParentWound))));
            Assert.Equal(new WoundOwnerCoordinate("chaos_sea", "guardian", "guardian_frame", WoundCarrierCatalog.AfterlifeProfilesPath), parent.ParentWound.Owner);
            Assert.Equal(("active", "spiritual", 40 + 2 * index),
                (parent.ParentWound.Lifecycle, parent.ParentWound.Classification.Domain, parent.ParentWound.Origin.CreatedAtTurn));
            Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(parent.ParentWound), parent.ParentSemanticFingerprint);
            Assert.Null(parent.ParentSourceId);
            Assert.Equal(expected["consequences"]!["ownedEffectSources"]!["rootBindings"]![0]!["effectId"]!.GetValue<string>(), parent.EffectId);
            Assert.Equal(parent.EffectId, row["effectId"]!.GetValue<string>());
            Assert.Equal("component_001", parent.ComponentId);
            Assert.Equal(parent.ComponentId, row["componentId"]!.GetValue<string>());
        }
        data.ReadActualExchange()["after"]!["oppositionSideStrain"] = "clear";
        data.ReadOrdinaryAfter(fixture.Actual)["oppositionSideStrain"] = "clear";
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        Assert.Equal(sourceBefore, source.ToJsonString());
        Assert.Equal(fixtureBefore, JsonSerializer.Serialize(fixture));
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_intrinsic_complete", contour, direct,
            parents = data.Parents.Count, data.OrdinaryCalculation, data.ActualCalculation }));
    }

    /// <summary>Independent margin8/art0/resilience0 ordinaryII10/maxI literal.</summary>
    private static SpiritualWoundCalculation PositiveOrdinary() =>
        new(new(8, 0, 0, "strained", "fractured", "hostile", 4), 1, 2, 0, 10, 1, 2, 4, true, 1);
    /// <summary>Independent margin8/art0/resilience0 actualIV20/maxIII literal, also the equal-cap ordinary.</summary>
    private static SpiritualWoundCalculation PositiveActual() =>
        new(new(8, 0, 0, "strained", "broken", "hostile", 4), 1, 4, 2, 20, 3, 4, 4, true, 3);
    /// <summary>Independent margin4/art0/resilience5 ordinaryII−4/max0 literal.</summary>
    private static SpiritualWoundCalculation ZeroOrdinary() =>
        new(new(4, 0, 5, "strained", "fractured", "hostile", 4), 1, 2, 0, -4, 0, 2, 4, true, 0);
    /// <summary>Independent margin4/art0/resilience5 actualIV6/max0 literal.</summary>
    private static SpiritualWoundCalculation ZeroActual() =>
        new(new(4, 0, 5, "strained", "broken", "hostile", 4), 1, 4, 2, 6, 0, 4, 4, true, 0);
}
