using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Requires supported v4 comparison DATA, without claiming signed provenance or live pair authority.</summary>
public sealed partial class SpiritualPressurePairPreimageTests
{
    /// <summary>Retains the independently authored ordinary II boundary and actual IV calculation.</summary>
    [Fact]
    public void PairPressureDataRetainsBoundaryOrdinaryAndActualCalculations() =>
        AssertPairPressureData(new(new(8, 0, 0, "clear", "fractured", "hostile", 4),
            0, 2, 1, 13, 2, 2, 4, true, 2));

    /// <summary>Retains ordinary III while clamping its two-parent sum5 to actual IV.</summary>
    [Fact]
    public void PairPressureDataRetainsClampedOrdinaryAndActualCalculations() =>
        AssertPairPressureData(new(new(8, 0, 0, "clear", "overwhelmed", "hostile", 4),
            0, 3, 2, 18, 3, 3, 4, true, 3));

    /// <summary>Proves existing-code intrinsic prerequisites before the desired-success v4 read.</summary>
    private static void AssertPairPressureData(SpiritualWoundCalculation expectedOrdinary)
    {
        var ordinary = SpiritualRetainedPressureGroupTestData.CreateExchange(
            "exchange_pressure_pair_data_46", 46, "clear", expectedOrdinary.Input.NewStrain!);
        var actual = ordinary.DeepClone().AsObject();
        actual["after"]!["playerSideStrain"] = "broken";
        var expectedActual = new SpiritualWoundCalculation(new(8, 0, 0, "clear", "broken", "hostile", 4),
            0, 4, 3, 23, 4, 4, 4, true, 4);
        var source = CreatePairSource(ordinary, actual, expectedOrdinary);
        var sourceBefore = source.ToJsonString();

        // Both complete exchanges must pass the existing witness without invoking any strain codec.
        var actualOnly = source.DeepClone().AsObject();
        Assert.True(actualOnly.Remove("strainPreimage"));
        SpiritualExistingPressureTestData.Seal(actualOnly);
        AssertWitnessCalculation(actualOnly, expectedActual);
        var ordinaryOnly = actualOnly.DeepClone().AsObject();
        ordinaryOnly["exchangeJsonBase64"] = Encode(ordinary);
        ordinaryOnly["destinationStrainRank"] = expectedOrdinary.NewStrainRank;
        ordinaryOnly["extraStrainJumps"] = expectedOrdinary.ExtraJumpSteps;
        ordinaryOnly["maximumSeverityRank"] = expectedOrdinary.MaximumSeverityRank;
        SpiritualExistingPressureTestData.Seal(ordinaryOnly);
        AssertWitnessCalculation(ordinaryOnly, expectedOrdinary);

        var rows = source["strainPreimage"]!["parents"]!.AsArray();
        Assert.Equal(2, rows.Count);
        var expectedIds = new[] { SpiritualPressureGroupViewTestData.FirstWoundId,
            SpiritualPressureGroupViewTestData.SecondWoundId };
        Assert.Equal(expectedIds.OrderBy(id => id, StringComparer.Ordinal), expectedIds);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index]!.AsObject();
            var parent = SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(
                SpiritualWoundStateJson.DecodeJsonObjectBytes(row["parentWoundJsonBase64"])));
            var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "unit.pairPressure.parent");
            Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
            var wound = parsed.Wound!;
            Assert.Equal(expectedIds[index], wound.WoundId);
            Assert.Equal((3, "active", "spiritual"),
                (wound.Severity.Rank, wound.Lifecycle, wound.Classification.Domain));
            Assert.Equal(new WoundOwnerCoordinate("chaos_sea", "player_soul", "player_soul",
                WoundCarrierCatalog.AfterlifeProfilesPath), wound.Owner);
            Assert.True(wound.LastTransition.Turn <= 46);
            var root = Assert.Single(wound.Consequences.OwnedEffectSources.RootBindings);
            Assert.Equal(index == 0 ? "effect_pressure_view_A" : "effect_pressure_view_B", root.EffectId);
            Assert.Equal(root.EffectId, row["effectId"]!.GetValue<string>());
            Assert.Equal("component_001", row["componentId"]!.GetValue<string>());
        }

        // The whole relation is independently authored, not produced by a future v4 writer.
        var restored = actual.DeepClone().AsObject();
        restored["after"]!["playerSideStrain"] = expectedOrdinary.Input.NewStrain;
        Assert.True(JsonNode.DeepEquals(ordinary, restored));
        Assert.Equal("clear", ordinary["before"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("broken", actual["after"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal(4, Math.Min(4, expectedOrdinary.NewStrainRank + rows.Count));

        // On unchanged source the first unsupported-v4 refusal is codec support RED, not cap/parent semantic coverage.
        var calculated = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained);
        Assert.Equal(expectedActual, calculated);
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(4, data.Version);
        Assert.Equal(expectedOrdinary, data.OrdinaryCalculation);
        Assert.Same(calculated, data.ActualCalculation);
        Assert.Equal("/activeConflict/exchangeLog/0", data.OriginalExchangePointer);
        Assert.Equal(source["strainPreimage"]!["originalInput"]!["contentFingerprint"]!.GetValue<string>(),
            data.OriginalInputFingerprint);
        Assert.True(JsonNode.DeepEquals(ordinary["after"], data.ReadOrdinaryAfter(actual)));
        Assert.True(JsonNode.DeepEquals(actual, data.ReadActualExchange()));
        var direct = SpiritualStrainPreimageData.ReadIntrinsic(source, actual);
        Assert.Equal(expectedOrdinary, direct.OrdinaryCalculation);
        Assert.Equal(expectedActual, direct.ActualCalculation);
        data.ReadOrdinaryAfter(actual)["playerSideStrain"] = "clear";
        data.ReadActualExchange()["summary"] = "Caller-mutated copy.";
        Assert.True(JsonNode.DeepEquals(ordinary["after"], data.ReadOrdinaryAfter(actual)));
        Assert.True(JsonNode.DeepEquals(actual, data.ReadActualExchange()));
        Assert.Equal(sourceBefore, source.ToJsonString());
    }

    /// <summary>Authors closed v4 wire from independent existing immutable-shape DATA, never a live producer.</summary>
    private static JsonObject CreatePairSource(JsonObject ordinary, JsonObject actual,
        SpiritualWoundCalculation expectedOrdinary)
    {
        var source = SpiritualExistingPressureTestData.CreateData();
        source["coordinate"] = "activeConflict.exchangeLog[0].player";
        source["exchangeId"] = "exchange_pressure_pair_data_46";
        source["actingActor"]!["actorId"] = "guardian_frame";
        source["appliedArtId"] = "guardian:guardian_frame:pressure";
        source["priorStrainRank"] = 0; source["destinationStrainRank"] = 4;
        source["extraStrainJumps"] = 3; source["harmfulMargin"] = 8; source["maximumSeverityRank"] = 4;
        source["exchangeJsonBase64"] = Encode(actual);
        source["turnEvidence"]!["turn"] = 46;
        source["turnEvidence"]!["requestId"] = "request-pressure-pair-data46";
        source["turnEvidence"]!["snapshotToken"] = "snapshot-pressure-pair-data46";
        var pair = SpiritualPressureGroupViewTestData.CreatePair();
        var parents = new JsonArray();
        foreach (var woundId in pair.WoundIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            var parent = pair.Parent(woundId);
            parents.Add(new JsonObject
            {
                ["parentWoundJsonBase64"] = Encode(parent),
                ["effectId"] = parent["consequences"]!["ownedEffectSources"]!["rootBindings"]![0]!["effectId"]!.DeepClone(),
                ["componentId"] = "component_001"
            });
        }
        var original = new JsonObject
        {
            ["schemaVersion"] = 1, ["activeConflict"] = new JsonObject
            {
                ["conflictId"] = "conflict-a", ["playerSideStrain"] = expectedOrdinary.Input.NewStrain,
                ["exchangeLog"] = new JsonArray(ordinary.DeepClone())
            }
        };
        var originalBytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(" \n" + original.ToJsonString() + "\n")).ToArray();
        source["strainPreimage"] = new JsonObject
        {
            ["version"] = 4, ["inputRoleRevision"] = "existing_pressure_ordinary_pair_v1",
            ["ordinaryExchangeJsonBase64"] = Encode(ordinary),
            ["originalInput"] = new JsonObject
            {
                ["path"] = AfterlifeSpiritualConflictState.StatePath, ["existed"] = true,
                ["contentFingerprint"] = new CanonicalBeforeImage(true, originalBytes).Fingerprint,
                ["exchangePointer"] = "/activeConflict/exchangeLog/0"
            },
            ["parents"] = parents,
            ["ordinaryCalculation"] = new JsonObject
            {
                ["traumaPressure"] = expectedOrdinary.TraumaPressure,
                ["maximumSeverityRank"] = expectedOrdinary.MaximumSeverityRank
            }
        };
        SpiritualExistingPressureTestData.Seal(source);
        return source;
    }

    /// <summary>Uses the real existing witness and proves it leaves its independent input unchanged.</summary>
    private static void AssertWitnessCalculation(JsonObject source, SpiritualWoundCalculation expected)
    {
        Assert.False(source.ContainsKey("strainPreimage"));
        var before = source.ToJsonString();
        Assert.Equal(expected, SpiritualWoundSourceWitness.Validate(source));
        Assert.Equal(before, source.ToJsonString());
    }

    private static string Encode(JsonNode value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(value)));
}
