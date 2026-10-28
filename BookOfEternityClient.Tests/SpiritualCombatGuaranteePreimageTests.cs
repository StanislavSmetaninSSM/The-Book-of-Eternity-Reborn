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

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("foreign_exchange")]
    [InlineData("foreign_art")]
    [InlineData("duplicate_property")]
    [InlineData("invalid_utf8")]
    [InlineData("base64_whitespace")]
    [InlineData("unknown_field")]
    public void GuaranteedWitnessRejectsResealedMissingOrForeignPreimage(string change)
    {
        var source = GuaranteedSource(out var exchangeJson, out var artJson);
        Assert.Equal(1, SpiritualWoundSourceWitness.Validate(source).MaximumSeverityRank);
        var preimage = source["guaranteePreimage"]!.AsObject();
        switch (change)
        {
            case "missing": source.Remove("guaranteePreimage"); break;
            case "null": source["guaranteePreimage"] = null; break;
            case "foreign_exchange":
                var exchange = SpiritualWoundStateJson.Parse(exchangeJson);
                exchange["exchangeId"] = "foreign-exchange";
                preimage["exchangeJsonBase64"] = Encode(exchange.ToJsonString());
                break;
            case "foreign_art":
                var art = SpiritualWoundStateJson.Parse(artJson);
                art["artId"] = "foreign-art";
                preimage["specialArtJsonBase64"] = Encode(art.ToJsonString());
                break;
            case "duplicate_property":
                preimage["specialArtJsonBase64"] = Encode("{\"artId\":\"art-source\",\"artId\":\"art-source\"}");
                break;
            case "invalid_utf8":
                preimage["exchangeJsonBase64"] = Convert.ToBase64String(new byte[] { 0xc3, 0x28 });
                break;
            case "base64_whitespace":
                preimage["exchangeJsonBase64"] = preimage["exchangeJsonBase64"]!.GetValue<string>() + "\n";
                break;
            case "unknown_field": preimage["permission"] = true; break;
        }
        Seal(source); // Intrinsic proof must reject even with a matching outer digest.
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.Validate(source));
    }

    [Fact]
    public void NonGuaranteedWitnessKeepsItsClosedForm()
    {
        var source = SpiritualWoundSourceWitnessTests.Source();
        Assert.Equal(1, SpiritualWoundSourceWitness.Validate(source).MaximumSeverityRank);
        source["guaranteePreimage"] = GuaranteedSource(out _, out _)["guaranteePreimage"]!.DeepClone();
        Seal(source);
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.Validate(source));
    }

    [Fact]
    public void ExistingGuaranteeHashRetainsItsOriginalPreparedStringDomain()
    {
        const string art = "{\"z\":1,\"a\":2}";
        const string exchange = "{\"operation\":\"pressure\",\"exchangeId\":\"exchange-a\"}";
        var claims = "sha256:" + new string('a', 64);
        var source = SpiritualWoundSourceWitness.GuaranteeSourceFingerprint("guardian:guardian_frame", "pressure", art);
        Assert.Equal("sha256:430e41cb3bfd9f2483a608adc6a43d11cb73200f76ae6b770142cf9bc14027e1", source);
        var guarantee = SpiritualWoundSourceWitness.GuaranteeAuthorityFingerprint(claims, source,
            "coordinate-a", exchange, 42, "opportunity-a_original_guarantee");
        Assert.Equal("sha256:019b7411fa9b7f90102172f9a2a09216b6f361f768a9d0ea5483e5e7e4f627b8", guarantee);
        Assert.Equal("sha256:6ede3f1d3a0d4a77ba33551e0c0b38b56177db09316a58894c47e312676c3632",
            WoundOpportunityAuthority.OriginalGuaranteeFingerprint(claims, guarantee));
        Assert.NotEqual(source, SpiritualWoundSourceWitness.GuaranteeSourceFingerprint("guardian:guardian_frame", "pressure",
            SpiritualWoundStateJson.Canonical(SpiritualWoundStateJson.Parse(art))));
        Assert.NotEqual(guarantee, SpiritualWoundSourceWitness.GuaranteeAuthorityFingerprint(claims, source,
            "coordinate-a", SpiritualWoundStateJson.Canonical(SpiritualWoundStateJson.Parse(exchange)), 42,
            "opportunity-a_original_guarantee"));
        var captured = SpiritualWoundSourceWitness.CaptureGuaranteePreimage(exchange, art);
        Assert.Equal(exchange, Decode(captured["exchangeJsonBase64"]!));
        Assert.Equal(art, Decode(captured["specialArtJsonBase64"]!));
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
