using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualGroupZeroPressureDataTests
{
    /// <summary>Every complete cardinality keeps literal zero arithmetic, including mixed ranks and the separate training cap.</summary>
    /// <param name="contour">Closed near-cap, capped hostile or training geometry.</param>
    /// <param name="parents">Independently expected complete N2/N3/N4 count.</param>
    /// <param name="mixed">Authors the fixed complete III/IV sequence before sealing.</param>
    [Theory]
    [InlineData(1, 2, false)] [InlineData(1, 3, false)] [InlineData(1, 4, false)]
    [InlineData(2, 2, false)] [InlineData(2, 3, false)] [InlineData(2, 4, false)]
    [InlineData(2, 4, true)] [InlineData(3, 4, false)]
    public void GroupZero_FreshCompleteGeometry(int contour, int parents, bool mixed)
    {
        var kind = SpiritualGroupZeroPressureData.Contour(contour);
        int[]? ranks = mixed ? [3, 4, 3, 4] : null;
        var fixture = SpiritualGroupZeroPressureData.Create(kind, parents, ranks);
        AssertPrerequisites(fixture, kind, parents, ranks);
        Assert.Equal(SpiritualGroupZeroPressureData.Expected(kind).Actual,
            SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var data));
        AssertSupported(fixture, kind, data!, parents);
        AssertSupported(fixture, kind, SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual), parents);
        Assert.Equal(ranks ?? Enumerable.Repeat(3, parents).ToArray(), data!.Parents.Select(parent => parent.ParentWound.Severity.Rank));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpiritualGroupZeroPressureData.Contour(99));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpiritualGroupZeroPressureData.Create(kind, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpiritualGroupZeroPressureData.Create(GroupZeroPressureContour.Low, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpiritualGroupZeroPressureData.Create(kind, parents, [3]));
    }

    /// <summary>Thirteen full/direct refusals preserve out-DATA clearing; nonincrease tests only the defensive intrinsic guard because full sources require increasing strain.</summary>
    /// <param name="change">One bounded contradiction after a valid complete baseline.</param>
    [Theory]
    [InlineData("duplicate")] [InlineData("order")] [InlineData("count")]
    [InlineData("owner")] [InlineData("effect")] [InlineData("component")]
    [InlineData("sibling")] [InlineData("no-effect")] [InlineData("nonincrease")]
    [InlineData("ordinary-maximum")] [InlineData("actual-maximum")]
    [InlineData("role")] [InlineData("ordinal")] [InlineData("raw-pointer")]
    public void GroupZero_ResealedContradictionsRefuse(string change)
    {
        var count = change == "count" ? 3 : 4;
        var fixture = SpiritualGroupZeroPressureData.Create(GroupZeroPressureContour.Capped, count);
        AssertPrerequisites(fixture, GroupZeroPressureContour.Capped, count);
        _ = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var prior);
        Assert.NotNull(prior);
        var proof = fixture.Source["strainPreimage"]!.AsObject(); var rows = proof["parents"]!.AsArray();
        switch (change)
        {
            case "duplicate": rows[^1] = rows[0]!.DeepClone(); break;
            case "order":
                var reverse = rows.Reverse().Select(row => row!.DeepClone()).ToArray(); rows.Clear();
                foreach (var row in reverse) rows.Add(row); break;
            case "count": rows.RemoveAt(2); break; // v6 requires at least three; unlike lawful N4->N3.
            case "owner":
                var parent = fixture.Retained.Parents[^1].DeepClone().AsObject();
                parent["owner"]!["ownerId"] = "foreign_soul";
                Assert.True(WoundMaterializationContract.Parse(parent.ToJsonString(), "groupZero.foreignParent").IsValid);
                rows[^1]!["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(parent); break;
            case "effect": rows[^1]!["effectId"] = "effect_foreign"; break;
            case "component": rows[^1]!["componentId"] = "component_foreign"; break;
            case "sibling": fixture.Ordinary["summary"] = "Иное описание только обычного обмена."; break;
            case "no-effect":
                fixture.Actual["outcome"] = "no_effect"; fixture.Ordinary["outcome"] = "no_effect";
                fixture.Source["exchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Actual); break;
            case "nonincrease":
                fixture.Actual["before"]!["playerSideStrain"] = "broken";
                fixture.Ordinary["before"]!["playerSideStrain"] = "broken";
                var calculation = new SpiritualWoundCalculation(new(8, 0, 4, "broken", "broken", "hostile", 4),
                    4, 4, 0, 6, 0, 4, 4, false, 0);
                Assert.Equal(calculation, SpiritualWoundOpportunityMath.Calculate(calculation.Input));
                var replacement = SpiritualGroupZeroPressureData.CreateStandaloneSource(fixture.Actual, calculation, fixture.RawInput, "group_zero_data");
                replacement["strainPreimage"] = proof.DeepClone();
                replacement["strainPreimage"]!["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Ordinary);
                SpiritualExistingPressureTestData.Seal(replacement);
                var intrinsicFixture = fixture with { Source = replacement };
                var intrinsicBefore = SpiritualGroupZeroPressureData.Serialize(intrinsicFixture);
                // This guard runs before ordinary arithmetic/geometry. No nonincreasing full-source success is claimed.
                var refusal = Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadPressureIntrinsic(replacement, fixture.Actual, calculation));
                Assert.Equal("Only increasing non-guaranteed standard pressure/setback comparison with a nonnegative maximum is supported.", refusal.Message);
                Assert.Equal(intrinsicBefore, SpiritualGroupZeroPressureData.Serialize(intrinsicFixture));
                return;
            case "ordinary-maximum": proof["ordinaryCalculation"]!["maximumSeverityRank"] = 1; break;
            case "actual-maximum": fixture.Source["maximumSeverityRank"] = 1; break;
            case "role": proof["inputRoleRevision"] = "existing_pressure_ordinary_pair_v1"; break;
            case "ordinal": fixture.Source["sourceOrdinal"] = 1; break;
            case "raw-pointer": proof["originalInput"]!["exchangePointer"] = "/activeConflict/exchangeLog/00"; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        proof["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Ordinary);
        SpiritualExistingPressureTestData.Seal(fixture.Source);
        var before = SpiritualGroupZeroPressureData.Serialize(fixture);
        var retained = prior;
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out retained));
        Assert.Null(retained);
        Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual));
        Assert.Equal(before, SpiritualGroupZeroPressureData.Serialize(fixture));
    }

    /// <summary>Intrinsic DATA cannot prove the complete current membership or authenticity of otherwise well-formed raw bytes.</summary>
    /// <param name="change">A lawful saturated subset or a well-formed unbound comparator hash.</param>
    [Theory]
    [InlineData("subset")] [InlineData("raw-hash")]
    public void GroupZero_IntrinsicDataDoesNotProveMembership(string change)
    {
        var fixture = SpiritualGroupZeroPressureData.Create(GroupZeroPressureContour.Capped);
        AssertPrerequisites(fixture, GroupZeroPressureContour.Capped);
        _ = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var baseline); Assert.Equal(4, baseline!.Parents.Count);
        var proof = fixture.Source["strainPreimage"]!;
        if (change == "subset") proof["parents"]!.AsArray().RemoveAt(3);
        else proof["originalInput"]!["contentFingerprint"] = "sha256:" + new string('f', 64);
        SpiritualExistingPressureTestData.Seal(fixture.Source);
        var before = SpiritualGroupZeroPressureData.Serialize(fixture);
        Assert.Equal(SpiritualGroupZeroPressureData.Expected(GroupZeroPressureContour.Capped).Actual,
            SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var data));
        Assert.Equal(change == "subset" ? 3 : 4, data!.Parents.Count);
        Assert.Equal(4, fixture.Retained.Parents.Count);
        Assert.Equal(data.Parents.Count, SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual).Parents.Count);
        if (change == "raw-hash")
            Assert.NotEqual(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint, data.OriginalInputFingerprint);
        Assert.Equal(before, SpiritualGroupZeroPressureData.Serialize(fixture));
    }
}
