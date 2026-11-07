using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Cheap complete-IV reader refusals after the exact valid v3/v5 baseline, never live source authority.</summary>
public sealed class SpiritualSingletonIvReaderControlsTests(ITestOutputHelper output)
{
    /// <summary>Rank IV cannot excuse missing graph members, III costs, wrong selection, actor, lifecycle or chronology.</summary>
    /// <param name="guardian">Selects the independently authored v5 guardian rather than v3 player graph.</param>
    /// <param name="change">One exact contradiction applied only after complete IV baseline admission.</param>
    [Theory]
    [InlineData(false, "missing-entry")] [InlineData(true, "missing-entry")]
    [InlineData(false, "missing-definition")] [InlineData(true, "missing-definition")]
    [InlineData(false, "III-cost")] [InlineData(true, "III-cost")]
    [InlineData(false, "selected-component")] [InlineData(true, "selected-component")]
    [InlineData(false, "foreign-owner")] [InlineData(true, "foreign-owner")]
    [InlineData(false, "terminal")] [InlineData(true, "terminal")]
    [InlineData(false, "future")] [InlineData(true, "future")]
    public void SingletonIvReaders_RejectMalformedCompleteParentControls(bool guardian, string change)
    {
        var source = guardian ? SpiritualGuardianAttributionTestData.Create(rank: 4).Source :
            SpiritualExistingPressureTestData.CreateData(4);
        AssertBaseline(source, guardian ? 5 : 3);
        var parent = SpiritualExistingPressureTestData.ReadParent(source);
        var graph = parent["consequences"]!["ownedEffectSources"]!;
        switch (change)
        {
            case "missing-entry": parent["consequences"]!["entries"]!.AsArray().RemoveAt(3); break;
            case "missing-definition": graph["definitions"]!.AsArray().RemoveAt(0); break;
            case "III-cost":
                var cost = graph["definitions"]!.AsArray().OfType<JsonObject>()
                    .SelectMany(definition => definition["components"]!.AsArray().OfType<JsonObject>())
                    .Single(component => component["profile"]!.GetValue<string>() == "spiritual_action_cost_burden" &&
                        component["payload"]!["operation"]!.GetValue<string>() == "guard");
                Assert.Equal(3, cost["payload"]!["magnitude"]!.GetValue<int>()); cost["payload"]!["magnitude"] = 2;
                break;
            case "selected-component": source["strainPreimage"]!["componentId"] = "component_foreign"; break;
            case "foreign-owner": parent["owner"]!["ownerId"] = "foreign_owner"; break;
            case "terminal": parent["lifecycle"] = "healed"; break;
            case "future":
                parent["lastTransition"]!["kind"] = "complicate"; parent["lastTransition"]!["ordinal"] = 2;
                parent["lastTransition"]!["turn"] = source["turnEvidence"]!["turn"]!.GetValue<int>() + 1;
                break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "unit.singletonIV.changedParent");
        if (change is "foreign-owner" or "future" or "selected-component")
            Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
        else if (change is "missing-entry" or "missing-definition" or "III-cost")
            Assert.False(parsed.IsValid);
        source["strainPreimage"]!["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(parent);
        SpiritualExistingPressureTestData.Seal(source);
        AssertRefused(source, guardian ? 5 : 3, change);
    }

    /// <summary>Every existing type/ID alias still names the exact declared failed guardian with a lawful IV parent.</summary>
    /// <param name="alias">One of the seven supported incoming actor aliases.</param>
    [Theory]
    [InlineData("actorType")] [InlineData("ownerActorType")]
    [InlineData("actorId")] [InlineData("actorRef")] [InlineData("ownerActorId")] [InlineData("guardianId")] [InlineData("id")]
    public void GuardianSingletonIv_RejectsEveryForeignIncomingAlias(string alias)
    {
        var fixture = SpiritualGuardianAttributionTestData.Create(rank: 4);
        AssertBaseline(fixture.Source, 5);
        foreach (var exchange in new[] { fixture.Ordinary, fixture.Actual })
            exchange["incomingAction"]![alias] = alias.EndsWith("Type", StringComparison.Ordinal) ? "resident" : "guardian_foreign";
        fixture.Source["strainPreimage"]!["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Ordinary);
        fixture.Source["exchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Actual);
        SpiritualExistingPressureTestData.Seal(fixture.Source);
        AssertRefused(fixture.Source, 5, alias);
    }

    /// <summary>Supporting existing IV does not widen the unchanged v1 newly-createdIII gate, even for complete IV DATA.</summary>
    [Fact]
    public void SingletonIvReaders_VersionOneStillRequiresNewlyCreatedRankThree()
    {
        var source = SpiritualStrainPreimageDataTests.CreateData().Source;
        _ = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var baseline);
        Assert.Equal(1, Assert.IsType<SpiritualStrainPreimageData>(baseline).Version);
        var parent = SpiritualExistingPressureTestData.ReadParent(SpiritualExistingPressureTestData.CreateData(4));
        var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "unit.singletonIV.v1Parent");
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues)); Assert.Equal(4, parsed.Wound!.Severity.Rank);
        source["strainPreimage"]!["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(parent);
        source["strainPreimage"]!["effectId"] = "effect_pressure_strain";
        SpiritualExistingPressureTestData.Seal(source);
        var before = source.ToJsonString(); SpiritualStrainPreimageData? leaked = null;
        var error = Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out leaked));
        Assert.Equal("Matching newly created rank-III parent snapshot required.", error.Message);
        Assert.Null(leaked); Assert.Equal(before, source.ToJsonString());
    }

    private static void AssertBaseline(JsonObject source, int version)
    {
        var before = source.ToJsonString();
        _ = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var data);
        var comparison = Assert.IsType<SpiritualStrainPreimageData>(data);
        Assert.Equal(version, comparison.Version); Assert.Equal(4, comparison.ParentWound.Severity.Rank);
        Assert.Equal(version, SpiritualStrainPreimageData.ReadIntrinsic(source, SpiritualExistingPressureTestData.ReadActual(source)).Version);
        Assert.Equal(before, source.ToJsonString());
    }

    private void AssertRefused(JsonObject source, int version, string change)
    {
        var before = source.ToJsonString(); SpiritualStrainPreimageData? leaked = null;
        var error = Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out leaked));
        output.WriteLine(JsonSerializer.Serialize(new { phase = "singleton_iv_reader_negative", version, change, error = error.Message }));
        Assert.Null(leaked); Assert.Equal(before, source.ToJsonString());
        Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadIntrinsic(source, SpiritualExistingPressureTestData.ReadActual(source)));
        Assert.Equal(before, source.ToJsonString());
    }
}
