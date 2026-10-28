using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks retained guarantee DATA without issuing an original-source capability.</summary>
public sealed class SpiritualCombatGuaranteePreimageTests
{
    [Fact]
    public void GuaranteedWitnessRetainsBothNoncanonicalPreparedPreimages()
    {
        var source = GuaranteedSource(out var exchangeJson, out var artJson);
        Assert.NotEqual(SpiritualWoundStateJson.Canonical(SpiritualWoundStateJson.Parse(exchangeJson)), exchangeJson);
        Assert.NotEqual(SpiritualWoundStateJson.Canonical(SpiritualWoundStateJson.Parse(artJson)), artJson);
        Assert.Equal(exchangeJson, Decode(source["guaranteePreimage"]!["exchangeJsonBase64"]!));
        Assert.Equal(artJson, Decode(source["guaranteePreimage"]!["specialArtJsonBase64"]!));
        Assert.Equal(1, SpiritualWoundSourceWitness.Validate(source).MaximumSeverityRank);
    }

    private static JsonObject GuaranteedSource(out string exchangeJson, out string artJson)
    {
        var source = SpiritualWoundSourceWitnessTests.Source();
        var exchange = SpiritualWoundStateJson.Parse(Decode(source["exchangeJsonBase64"]!));
        var art = SpiritualWoundStateJson.Parse("""
            {"artId":"art-source","displayName":"Нить надлома","effectSummary":"Духовное давление.",
             "ownerActorType":"player_soul","ownerActorId":"player_soul","baseOperation":"pressure","tier":0,
             "costMultiplierPercent":200,"upgradeCost":{"inkFeathers":1},"canTeachPlayer":false,"trainingConditions":[],
             "spiritualWoundEnvelope":{"schemaVersion":1,"maximumSeverityRank":1,"guaranteedSeverityRank":1}}
            """);
        exchange["specialArtAudit"] = SpiritualWoundStateJson.Parse("""
            {"artId":"art-source","ownerActorType":"player_soul","ownerActorId":"player_soul",
             "baseOperation":"pressure","costMultiplierPercent":200,"effectNote":"Надлом духовного узора."}
            """);
        var cost = exchange["actionCostAudit"]!["player"]!;
        cost["specialArtId"] = "art-source";
        cost["specialCostMultiplierPercent"] = 200;
        cost["standardEffectiveCost"] = 3;
        cost["effectiveCost"] = 6;
        cost["after"] = 0;
        exchangeJson = exchange.ToJsonString();
        artJson = art.ToJsonString();
        source["appliedArtKind"] = "special";
        source["appliedArtId"] = "art-source";
        source["guaranteedSeverityRank"] = 1;
        source["exchangeJsonBase64"] = Encode(SpiritualWoundStateJson.Canonical(exchange));
        source["specialArtJsonBase64"] = Encode(SpiritualWoundStateJson.Canonical(art));
        source["guaranteePreimage"] = new JsonObject
        {
            ["exchangeJsonBase64"] = Encode(exchangeJson),
            ["specialArtJsonBase64"] = Encode(artJson)
        };
        Seal(source);
        return source;
    }

    private static string Encode(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    private static string Decode(JsonNode node) => Encoding.UTF8.GetString(Convert.FromBase64String(node.GetValue<string>()));

    private static void Seal(JsonObject source)
    {
        var fingerprint = SpiritualWoundStateJson.Hash(source, "source", "sourceId", "sourceFingerprint");
        source["sourceFingerprint"] = fingerprint;
        source["sourceId"] = "spiritual_source_" + fingerprint[7..];
    }
}
