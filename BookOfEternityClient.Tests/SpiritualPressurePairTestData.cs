using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>Authors detached intrinsic pair DATA only; no member issues source, History or publication authority.</summary>
internal static class SpiritualPressurePairTestData
{
    /// <summary>Builds a fresh turn46 pair source with independently fixed ordinary13/II or18/III and actual23/IV.</summary>
    /// <param name="ordinaryStrain">The supported independently authored fractured or overwhelmed ordinary result.</param>
    /// <param name="ranks">Complete parent severities authored before the private witness is sealed, in exact wound-ID order.</param>
    /// <returns>A complete mutable source owned only by this invocation.</returns>
    internal static JsonObject CreateSource(string ordinaryStrain = "fractured", SpiritualPressureParentRanks? ranks = null)
    {
        var (ordinaryPressure, ordinaryMaximum) = ordinaryStrain switch
        {
            "fractured" => (13L, 2),
            "overwhelmed" => (18L, 3),
            _ => throw new ArgumentOutOfRangeException(nameof(ordinaryStrain))
        };
        var ordinary = SpiritualRetainedPressureGroupTestData.CreateExchange(
            "exchange_pressure_pair_data_46", 46, "clear", ordinaryStrain);
        var actual = ordinary.DeepClone().AsObject();
        actual["after"]!["playerSideStrain"] = "broken";
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
        var pair = SpiritualPressureGroupViewTestData.CreatePair(ranks: ranks);
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
                ["conflictId"] = "conflict-a", ["playerSideStrain"] = ordinaryStrain,
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
                ["traumaPressure"] = ordinaryPressure, ["maximumSeverityRank"] = ordinaryMaximum
            }
        };
        Seal(source);
        return source;
    }

    /// <summary>Encodes an independently authored complete object in canonical strict UTF8/base64 form.</summary>
    internal static string Encode(JsonObject value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(value)));

    /// <summary>Updates only lexical fixture digests; a matching hash grants no accepted provenance.</summary>
    internal static void Seal(JsonObject source) => SpiritualExistingPressureTestData.Seal(source);
}
