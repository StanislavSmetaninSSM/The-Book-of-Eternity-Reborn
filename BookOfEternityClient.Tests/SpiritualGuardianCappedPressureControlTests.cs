using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Five after-valid capped-v5 DATA cuts; these readers cannot issue current or publication authority.</summary>
/// <param name="output">Full baseline, mutation and precise refusal diagnostics.</param>
public sealed class SpiritualGuardianCappedPressureControlTests(ITestOutputHelper output)
{
    /// <summary>Refuses one selected relation, complete parent/component or actual-source contradiction after an independent lawful IV baseline.</summary>
    /// <param name="change">One finite cut; dependent raw/source seals remain coherent.</param>
    [Theory]
    [InlineData("non-cap-equality")] [InlineData("foreign-parent")] [InlineData("foreign-component")]
    [InlineData("declaration-absent")] [InlineData("no-effect")]
    public void GuardianCapped_ResealedBoundariesRefuse(string change)
    {
        var fixture = SpiritualGuardianAttributionTestData.Create(rank: 4, contour: GuardianSingletonPressureContour.CappedBroken);
        var issues = SpiritualGuardianAttributionDataCheck.Check(fixture.Retained, fixture.Profiles, fixture.Conflict, 4);
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_control_independent_inputs", change, issues, fixture }));
        Assert.Empty(issues);
        var expected = new SpiritualWoundCalculation(new(8, 0, 0, "strained", "broken", "hostile", 4), 1, 4, 2, 20, 3, 4, 4, true, 3);
        foreach (var exchange in new[] { fixture.Ordinary, fixture.Actual })
        {
            var independent = SpiritualGuardianAttributionTestData.CreateSource(exchange, "capped_control_original_session",
                "capped_control_original_request", "capped_control_original_snapshot", "capped_control_original_conflict",
                new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint);
            Assert.False(independent.ContainsKey("strainPreimage"));
            Assert.Equal(expected, SpiritualWoundSourceWitness.ValidateAndReadStrainData(independent, out var absent)); Assert.Null(absent);
        }
        Assert.Equal(expected, SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var valid));
        var baseline = Assert.IsType<SpiritualStrainPreimageData>(valid);
        Assert.Equal(expected, baseline.OrdinaryCalculation); Assert.Equal(expected, baseline.ActualCalculation);
        Assert.Equal(5, SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual).Version);
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_control_valid_baseline", change, fixture, expected }));
        var ordinary = fixture.Ordinary.DeepClone().AsObject(); var actual = fixture.Actual.DeepClone().AsObject();
        var source = fixture.Source.DeepClone().AsObject(); var proof = source["strainPreimage"]!.AsObject();
        string cause;
        switch (change)
        {
            case "non-cap-equality":
                ordinary["after"]!["oppositionSideStrain"] = "overwhelmed";
                actual["after"]!["oppositionSideStrain"] = "overwhelmed";
                var root = fixture.Conflict.DeepClone().AsObject(); root["activeConflict"]!["exchangeLog"]![0] = ordinary.DeepClone();
                root["activeConflict"]!["oppositionSideStrain"] = "overwhelmed";
                var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + root.ToJsonString() + "\n")).ToArray();
                var fingerprint = new CanonicalBeforeImage(true, raw).Fingerprint;
                var literal = new SpiritualWoundCalculation(new(8, 0, 0, "strained", "overwhelmed", "hostile", 4), 1, 3, 1, 15, 2, 3, 4, true, 2);
                foreach (var exchange in new[] { ordinary, actual })
                {
                    var unburdened = SpiritualGuardianAttributionTestData.CreateSource(exchange, "cap_boundary_session",
                        "cap_boundary_request", "cap_boundary_snapshot", "cap_boundary_conflict", fingerprint);
                    Assert.Equal(literal, SpiritualWoundSourceWitness.ValidateAndReadStrainData(unburdened, out var noComparison)); Assert.Null(noComparison);
                }
                source = SpiritualGuardianAttributionTestData.CreateSource(actual, "cap_boundary_session", "cap_boundary_request",
                    "cap_boundary_snapshot", "cap_boundary_conflict", fingerprint);
                proof = proof.DeepClone().AsObject(); source["strainPreimage"] = proof;
                proof["originalInput"]!["contentFingerprint"] = fingerprint;
                proof["ordinaryCalculation"] = new JsonObject { ["traumaPressure"] = 15L, ["maximumSeverityRank"] = 2 };
                cause = "Increasing ordinary guardian source with nonnegative maximum required.";
                break;
            case "foreign-parent":
                var foreign = SpiritualGuardianAttributionTestData.Create("guardian_capped_foreign", 4, GuardianSingletonPressureContour.CappedBroken);
                var foreignIssues = SpiritualGuardianAttributionDataCheck.Check(foreign.Retained, foreign.Profiles, foreign.Conflict, 4);
                output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_control_foreign_parent_prerequisite", foreignIssues, foreign }));
                Assert.Empty(foreignIssues);
                Assert.Equal(expected, SpiritualWoundSourceWitness.ValidateAndReadStrainData(foreign.Source, out _));
                foreach (var field in new[] { "parentWoundJsonBase64", "effectId", "componentId" })
                    proof[field] = foreign.Source["strainPreimage"]![field]!.DeepClone();
                cause = "Matching active existing guardian III/IV parent required.";
                break;
            case "foreign-component": proof["componentId"] = "component_foreign"; cause = "Selected parent root/component/profile agreement required."; break;
            case "declaration-absent":
                Assert.True(ordinary.Remove("spiritualWoundFailedAction")); Assert.True(actual.Remove("spiritualWoundFailedAction"));
                cause = "Guardian failed-action declaration required."; break;
            case "no-effect": ordinary["outcome"] = "no_effect"; actual["outcome"] = "no_effect"; cause = "Invalid retained exchange payload."; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        source["exchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(actual);
        proof["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(ordinary);
        // Comparator bytes stay coherent even for an intentionally invalid actual outcome/declaration.
        if (change is "declaration-absent" or "no-effect")
        {
            var root = fixture.Conflict.DeepClone().AsObject(); root["activeConflict"]!["exchangeLog"]![0] = ordinary.DeepClone();
            var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + root.ToJsonString() + "\n")).ToArray();
            proof["originalInput"]!["contentFingerprint"] = new CanonicalBeforeImage(true, raw).Fingerprint;
        }
        SpiritualExistingPressureTestData.Seal(source);
        var before = source.ToJsonString(); SpiritualStrainPreimageData? escaped = baseline;
        var full = Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out escaped));
        Assert.Null(escaped); Assert.Equal(cause, full.Message);
        var direct = Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadIntrinsic(source, actual));
        Assert.Equal(cause, direct.Message); Assert.Equal(before, source.ToJsonString());
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_control_named_refusal", change,
            baselinePassed = true, owningLayer = change == "no-effect" ? "actual-source rules before intrinsic" : "v5 intrinsic",
            cause, sourceJson = before, fullError = full.ToString(), directError = direct.ToString() }));
    }
}
