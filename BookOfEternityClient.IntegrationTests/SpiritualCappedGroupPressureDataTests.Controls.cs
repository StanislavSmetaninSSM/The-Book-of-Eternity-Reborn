using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualCappedGroupPressureDataTests
{
    /// <summary>Fresh full ordinary roots prove all increasing before0..3 capped geometries at N2/N3/N4, never post-seal reclassification.</summary>
    /// <param name="parents">Complete retained list size; no parent is dropped after scalar saturation.</param>
    /// <param name="previous">Independent clear through overwhelmed preimage.</param>
    /// <param name="pressure">Literal independent ordinary and actual trauma pressure.</param>
    /// <param name="maximum">Literal independent positive ordinary and actual ceiling.</param>
    [Theory]
    [InlineData(2, 0, 23L, 4)] [InlineData(2, 1, 20L, 3)] [InlineData(2, 2, 17L, 2)] [InlineData(2, 3, 14L, 2)]
    [InlineData(3, 0, 23L, 4)] [InlineData(3, 1, 20L, 3)] [InlineData(3, 2, 17L, 2)] [InlineData(3, 3, 14L, 2)]
    [InlineData(4, 0, 23L, 4)] [InlineData(4, 1, 20L, 3)] [InlineData(4, 2, 17L, 2)] [InlineData(4, 3, 14L, 2)]
    public void CappedGroupPressure_FreshFullRootGeometry(int parents, int previous, long pressure, int maximum)
    {
        var fixture = SpiritualCappedGroupPressureData.Create(parents, previous);
        var data = ReadSupportedCappedGroup(fixture, previous);
        Assert.Equal((previous, 4, 3 - previous, pressure, maximum, true),
            (data.ActualCalculation.PreviousStrainRank, data.ActualCalculation.NewStrainRank,
                data.ActualCalculation.ExtraJumpSteps, data.ActualCalculation.TraumaPressure,
                data.ActualCalculation.MaximumSeverityRank, data.ActualCalculation.HasIncreasingStrain));
        Assert.Equal(data.ActualCalculation, data.OrdinaryCalculation);
    }

    /// <summary>Complete ordered III/IV graphs remain individually required even though all selected pressure contributions saturate at the same IV.</summary>
    /// <param name="ranks">Independent complete rank sequence authored before every provenance and raw comparison field.</param>
    [Theory]
    [InlineData("3,4")] [InlineData("4,3")] [InlineData("3,4,3")]
    [InlineData("4,3,4,3")] [InlineData("4,4,4,4")]
    public void CappedGroupPressure_MixedCompleteRanks(string ranks)
    {
        var expected = ranks.Split(',').Select(int.Parse).ToArray();
        var fixture = SpiritualCappedGroupPressureData.Create(expected.Length, ranks: expected);
        var data = ReadSupportedCappedGroup(fixture, ranks: expected);
        Assert.Equal(expected, data.Parents.Select(parent => parent.ParentWound.Severity.Rank));
        Assert.Equal(SpiritualCappedGroupPressureData.Calculation, data.ActualCalculation);
    }

    /// <summary>A previously accepted saturated complete N4 comparison cannot hide malformed, foreign, future or contradictory final-parent DATA.</summary>
    /// <param name="change">One finite final-parent wire or complete-graph contradiction after a full lawful positive baseline.</param>
    [Theory]
    [InlineData("base64")] [InlineData("utf8")] [InlineData("duplicate-json")]
    [InlineData("owner")] [InlineData("realm")] [InlineData("future")]
    [InlineData("effect")] [InlineData("component")] [InlineData("operation")]
    [InlineData("magnitude")] [InlineData("max-stacks")] [InlineData("profile-entry")]
    public void CappedGroupPressure_FinalParentContradictionsRefuse(string change)
    {
        var fixture = SpiritualCappedGroupPressureData.Create(4);
        var previous = ReadSupportedCappedGroup(fixture);
        var row = CappedProof(fixture.Source)["parents"]![3]!;
        var parent = fixture.Retained.Parents[3].DeepClone().AsObject();
        var definition = parent["consequences"]!["ownedEffectSources"]!["definitions"]![0]!.AsObject();
        switch (change)
        {
            case "base64": row["parentWoundJsonBase64"] = "!not-base64!"; break;
            case "utf8": row["parentWoundJsonBase64"] = Convert.ToBase64String(new byte[] { 0x7b, 0x22, 0xc3, 0x28, 0x22, 0x3a, 0x31, 0x7d }); break;
            case "duplicate-json": row["parentWoundJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"nested\":[{\"key\":1,\"key\":2}]}")); break;
            case "owner": parent["owner"]!["ownerId"] = "foreign_soul"; break;
            case "realm": parent["owner"]!["realm"] = "shining_abode"; definition["allowedRealms"] = new JsonArray("shining_abode"); break;
            case "future": parent["lastTransition"]!["turn"] = 47; parent["lastTransition"]!["kind"] = "complicate"; parent["lastTransition"]!["ordinal"] = 2; break;
            case "effect": row["effectId"] = "effect_foreign"; break;
            case "component": row["componentId"] = "component_foreign"; break;
            case "operation": definition["components"]![0]!["payload"]!["operation"] = "maneuver"; break;
            case "magnitude": definition["components"]![0]!["payload"]!["magnitude"] = 2; break;
            case "max-stacks": definition["stacking"]!["maxStacks"] = 2; break;
            case "profile-entry": parent["consequences"]!["entries"]!.AsArray().RemoveAt(0); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        if (change is not ("base64" or "utf8" or "duplicate-json"))
        {
            if (change is "owner" or "realm" or "future" or "effect" or "component" or "operation") AssertCappedParentShape(parent);
            row["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(parent);
        }
        AssertCappedGroupRejected(fixture, previous);
    }

    /// <summary>Version-specific cardinality, role, complete canonical order and global root identities stay closed after valid capped success.</summary>
    /// <param name="change">One finite count/role/list/root contradiction; a lawful N3 subset is separately accepted as DATA only.</param>
    [Theory]
    [InlineData("v4-count1")] [InlineData("v4-count3")] [InlineData("v6-count2")]
    [InlineData("v4-role")] [InlineData("v6-role")] [InlineData("duplicate")]
    [InlineData("reversed")] [InlineData("shared-root")] [InlineData("scalar-count")]
    public void CappedGroupPressure_ClosedListAndWireRefusals(string change)
    {
        var count = change is "v4-count1" or "v4-role" ? 2 : change == "v4-count3" ? 3 : 4;
        var fixture = SpiritualCappedGroupPressureData.Create(count);
        var previous = ReadSupportedCappedGroup(fixture);
        var proof = CappedProof(fixture.Source); var rows = proof["parents"]!.AsArray();
        switch (change)
        {
            case "v4-count1": rows.RemoveAt(1); break;
            case "v4-count3": proof["version"] = 4; proof["inputRoleRevision"] = "existing_pressure_ordinary_pair_v1"; break;
            case "v6-count2": while (rows.Count > 2) rows.RemoveAt(rows.Count - 1); break;
            case "v4-role": proof["inputRoleRevision"] = "existing_pressure_ordinary_group_v1"; break;
            case "v6-role": proof["inputRoleRevision"] = "existing_pressure_ordinary_pair_v1"; break;
            case "duplicate": rows[^1] = rows[0]!.DeepClone(); break;
            case "reversed":
                var reversed = rows.Reverse().Select(row => row!.DeepClone()).ToArray(); rows.Clear();
                foreach (var row in reversed) rows.Add(row); break;
            case "shared-root":
                var parent = fixture.Retained.Parents[^1];
                var oldRoot = rows[^1]!["effectId"]!.GetValue<string>(); var alias = rows[0]!["effectId"]!.GetValue<string>();
                var changed = SpiritualWoundStateJson.Parse(parent.ToJsonString().Replace(oldRoot, alias, StringComparison.Ordinal));
                AssertCappedParentShape(changed);
                rows[^1]!["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(changed); rows[^1]!["effectId"] = alias;
                break;
            case "scalar-count": proof["contributionCount"] = count; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        AssertCappedGroupRejected(fixture, previous);
    }

    /// <summary>Equality never removes full ordinary sibling, calculation, action or raw-comparator-shape requirements.</summary>
    /// <param name="change">One resealed contradiction after an accepted capped N4 baseline; modified payloads are never relabelled lawful full inputs.</param>
    [Theory]
    [InlineData("summary")] [InlineData("opposition-strain")] [InlineData("player-cost")] [InlineData("opposition-cost")]
    [InlineData("dice")] [InlineData("ordinary-trauma")] [InlineData("ordinary-maximum")]
    [InlineData("actual-margin")] [InlineData("actual-maximum")] [InlineData("no-effect")]
    [InlineData("raw-path")] [InlineData("raw-absent")] [InlineData("raw-hash")] [InlineData("raw-pointer")]
    public void CappedGroupPressure_ExchangeAndComparatorContradictionsRefuse(string change)
    {
        var fixture = SpiritualCappedGroupPressureData.Create(4);
        var previous = ReadSupportedCappedGroup(fixture);
        var proof = CappedProof(fixture.Source);
        switch (change)
        {
            case "summary": fixture.Ordinary["summary"] = "Changed complete ordinary sibling."; break;
            case "opposition-strain": fixture.Ordinary["after"]!["oppositionSideStrain"] = "strained"; break;
            case "player-cost": fixture.Ordinary["actionCostAudit"]!["player"]!["max"] = 11; break;
            case "opposition-cost": fixture.Ordinary["actionCostAudit"]!["opposition"]!["max"] = 10; break;
            case "dice": fixture.Ordinary["diceAudit"]!["diceUsed"]![0]!["value"] = 5; break;
            case "ordinary-trauma": proof["ordinaryCalculation"]!["traumaPressure"] = 15L; break;
            case "ordinary-maximum": proof["ordinaryCalculation"]!["maximumSeverityRank"] = 3; break;
            case "actual-margin": fixture.Source["harmfulMargin"] = 9; break;
            case "actual-maximum": fixture.Source["maximumSeverityRank"] = 3; break;
            case "no-effect":
                fixture.Ordinary["outcome"] = "no_effect"; fixture.Actual["outcome"] = "no_effect";
                fixture.Source["exchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Actual); break;
            case "raw-path": proof["originalInput"]!["path"] = "game_state/meta/foreign.json"; break;
            case "raw-absent": proof["originalInput"]!["existed"] = false; break;
            case "raw-hash": proof["originalInput"]!["contentFingerprint"] = "not-a-fingerprint"; break;
            case "raw-pointer": proof["originalInput"]!["exchangePointer"] = "/activeConflict/exchangeLog/00"; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        proof["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Ordinary);
        AssertCappedGroupRejected(fixture, previous);
    }

    /// <summary>Consistent full-root training sources remain lawful standalone DATA but never become positive capped group comparisons.</summary>
    /// <param name="parents">Exact-two v4 and N3/N4 v6 preserve the actual-positive ceiling fence.</param>
    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void CappedGroupPressure_ActualZeroMaximumStillRefuses(int parents)
    {
        var positive = SpiritualCappedGroupPressureData.Create(parents);
        var previous = ReadSupportedCappedGroup(positive);
        var zero = SpiritualCappedGroupPressureData.Create(parents, training: true);
        AssertPrerequisites(zero, parents, training: true);
        Assert.Equal(0, SpiritualCappedGroupPressureData.Expected(3, training: true).MaximumSeverityRank);
        AssertCappedGroupRejected(zero, previous,
            "Only increasing non-guaranteed standard pressure/setback comparison, with a positive multi-parent maximum, is supported.");
    }

    /// <summary>A lawful saturated subset carries exactly three complete parents as intrinsic DATA and cannot prove the actual current N4 set.</summary>
    [Fact]
    public void CappedGroupPressure_LawfulSubsetRemainsDataOnly()
    {
        var fixture = SpiritualCappedGroupPressureData.Create(4); _ = ReadSupportedCappedGroup(fixture);
        CappedProof(fixture.Source)["parents"]!.AsArray().RemoveAt(3); SpiritualExistingPressureTestData.Seal(fixture.Source);
        var before = SpiritualCappedGroupPressureData.Serialize(fixture);
        Assert.Equal(SpiritualCappedGroupPressureData.Calculation,
            SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var subset));
        Assert.Equal(3, subset!.Parents.Count); Assert.Equal(4, fixture.Retained.Parents.Count);
        Assert.Equal(3, SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual).Parents.Count);
        Assert.Equal(before, SpiritualCappedGroupPressureData.Serialize(fixture));
        CappedProof(fixture.Source)["contributionCount"] = 4;
        AssertCappedGroupRejected(fixture, subset);
    }

    /// <summary>A well-formed comparator hash is retained exactly as DATA; only the real owner can authenticate original byte membership.</summary>
    /// <param name="parents">Both version families preserve this non-authority boundary despite capped equality.</param>
    [Theory]
    [InlineData(2)] [InlineData(4)]
    public void CappedGroupPressure_WellFormedFingerprintRemainsDataOnly(int parents)
    {
        var fixture = SpiritualCappedGroupPressureData.Create(parents); _ = ReadSupportedCappedGroup(fixture);
        var original = new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint;
        CappedProof(fixture.Source)["originalInput"]!["contentFingerprint"] = "sha256:" + new string('f', 64);
        SpiritualExistingPressureTestData.Seal(fixture.Source);
        var before = SpiritualCappedGroupPressureData.Serialize(fixture);
        Assert.Equal(SpiritualCappedGroupPressureData.Calculation,
            SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var changed));
        Assert.NotEqual(original, changed!.OriginalInputFingerprint);
        Assert.Equal(original, fixture.Source["turnEvidence"]!["originalSnapshotFingerprint"]!.GetValue<string>());
        Assert.Equal(changed.OriginalInputFingerprint, SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual).OriginalInputFingerprint);
        Assert.Equal(before, SpiritualCappedGroupPressureData.Serialize(fixture));
    }

    /// <summary>Proves each control's own complete lawful baseline before mutation, retaining all-parent DATA without exposing authority.</summary>
    private static SpiritualStrainPreimageData ReadSupportedCappedGroup(SpiritualCappedGroupPressureFixture fixture,
        int previous = 3, IReadOnlyList<int>? ranks = null)
    {
        var count = fixture.Retained.Parents.Count;
        AssertPrerequisites(fixture, count, previous, ranks);
        var before = SpiritualCappedGroupPressureData.Serialize(fixture);
        Assert.Equal(SpiritualCappedGroupPressureData.Expected(previous),
            SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var retained));
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        AssertSupported(fixture, count, data, previous);
        AssertSupported(fixture, count, SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual), previous);
        Assert.Equal(before, SpiritualCappedGroupPressureData.Serialize(fixture)); return data;
    }

    /// <summary>Failed full and direct reads cannot leak previously accepted DATA or mutate any supplied graph, ledger, raw byte or source.</summary>
    private static void AssertCappedGroupRejected(SpiritualCappedGroupPressureFixture fixture, SpiritualStrainPreimageData previous,
        string? expectedMessage = null)
    {
        SpiritualExistingPressureTestData.Seal(fixture.Source);
        var before = SpiritualCappedGroupPressureData.Serialize(fixture);
        SpiritualStrainPreimageData? retained = previous;
        var full = Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out retained));
        Assert.Null(retained);
        var direct = Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual));
        if (expectedMessage is not null) { Assert.Equal(expectedMessage, full.Message); Assert.Equal(expectedMessage, direct.Message); }
        Assert.Equal(before, SpiritualCappedGroupPressureData.Serialize(fixture));
    }

    /// <summary>Selects caller-owned wire DATA, never the reader's immutable retained comparison.</summary>
    private static JsonObject CappedProof(JsonObject source) => source["strainPreimage"]!.AsObject();

    /// <summary>Isolates contradictory cross-parent references from malformed individual graph shapes.</summary>
    private static void AssertCappedParentShape(JsonObject parent)
    {
        var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "cappedGroup.changedParent");
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
    }
}
