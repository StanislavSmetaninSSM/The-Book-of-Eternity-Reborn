using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>Independent singleton v3 zero-ceiling DATA; retained hashes never confer provenance or owner authority.</summary>
internal static class SpiritualPressureZeroTestData
{
    /// <summary>Retains independent pre-embedding expectations and exact ordinary input bytes for one fresh fixture.</summary>
    internal sealed record Fixture(JsonObject Source, JsonObject Ordinary, JsonObject Actual,
        JsonObject Parent, byte[] RawInput);

    /// <summary>Builds a complete III/IV graph and a lawful zero comparison without using production arithmetic for expectations.</summary>
    /// <param name="parentRank">The existing singleton III/IV rank domain.</param>
    /// <param name="trainingCap">Keeps the existing beforeI/ordinaryII/actualIII geometry, capped to zero by training.</param>
    internal static Fixture CreateData(int parentRank, bool trainingCap = false)
    {
        var source = SpiritualExistingPressureTestData.CreateData(parentRank);
        var parent = SpiritualExistingPressureTestData.ReadParent(source);
        var ordinary = SpiritualExistingPressureTestData.ReadOrdinary(source);
        if (!trainingCap)
        {
            ordinary["before"]!["playerSideStrain"] = "clear";
            ordinary["after"]!["playerSideStrain"] = "strained";
        }
        var actual = ordinary.DeepClone().AsObject();
        actual["after"]!["playerSideStrain"] = trainingCap ? "overwhelmed" : "fractured";
        source["priorStrainRank"] = trainingCap ? 1 : 0;
        source["destinationStrainRank"] = trainingCap ? 3 : 2;
        source["extraStrainJumps"] = 1;
        source["targetResilienceTier"] = trainingCap ? 0 : 3;
        source["dangerMode"] = trainingCap ? "training" : "hostile";
        source["maximumSeverityRank"] = 0;
        source["exchangeJsonBase64"] = Encode(actual);
        var rawInput = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + new JsonObject
        {
            ["schemaVersion"] = 1,
            ["activeConflict"] = new JsonObject
            {
                ["exchangeLog"] = new JsonArray(new JsonObject(), new JsonObject(), ordinary.DeepClone()),
                ["playerSideStrain"] = trainingCap ? "fractured" : "strained"
            }
        }.ToJsonString() + "\n")).ToArray();
        var proof = source["strainPreimage"]!.AsObject();
        proof["ordinaryExchangeJsonBase64"] = Encode(ordinary);
        proof["originalInput"]!["contentFingerprint"] = new CanonicalBeforeImage(true, rawInput).Fingerprint;
        proof["ordinaryCalculation"]!["traumaPressure"] = trainingCap ? 10L : 2L;
        proof["ordinaryCalculation"]!["maximumSeverityRank"] = 0;
        SpiritualExistingPressureTestData.Seal(source);
        return new(source, ordinary, actual, parent, rawInput);
    }

    /// <summary>The literal resilience-three ordinary result, independent of the production calculator.</summary>
    internal static SpiritualWoundCalculation OrdinaryCalculation() =>
        new(new(8, 0, 3, "clear", "strained", "hostile", 4), 0, 1, 0, 2, 0, 1, 4, true, 0);

    /// <summary>The literal resilience-three actual result, independent of the production calculator.</summary>
    internal static SpiritualWoundCalculation ActualCalculation() =>
        new(new(8, 0, 3, "clear", "fractured", "hostile", 4), 0, 2, 1, 7, 0, 2, 4, true, 0);

    /// <summary>Encodes an independent complete object without acquiring authority.</summary>
    private static string Encode(JsonObject value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value.ToJsonString()));
}
