using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks explicit GM choices as retained DATA, never original-turn permission.</summary>
public sealed class SpiritualWoundNoRollSourceTests
{
    /// <summary>Retains signed choices, including explicit zero, without inventing dice.</summary>
    /// <param name="margin">Actual GM choice.</param>
    /// <param name="maximum">Expected existing capped severity for this one-step standard source.</param>
    [Theory]
    [InlineData(10L, 1)]
    [InlineData(0L, 0)]
    [InlineData(-1L, 0)]
    [InlineData(long.MinValue, 0)]
    [InlineData(long.MaxValue, 1)]
    public void Validate_RetainsExplicitChoiceWithoutDice(long margin, int maximum)
    {
        var source = Source(margin, maximum);
        var original = source.ToJsonString();
        var result = SpiritualWoundSourceWitness.Validate(source);
        Assert.Equal(margin, result.Input.HarmfulMargin);
        Assert.Equal(maximum, result.MaximumSeverityRank);
        Assert.Equal(original, source.ToJsonString());
        Assert.Equal(4, source["turnEvidence"]!["acceptedD20Values"]!.AsArray().Count);
        Assert.Empty(source["turnEvidence"]!["diceClaims"]!.AsArray());
        Assert.Empty(source["selectedDiceIndices"]!.AsArray());
    }

    /// <summary>Rejects absent, mistyped or contradictory retained choices and fabricated claims.</summary>
    /// <param name="change">One invalid input mutation.</param>
    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("wrong-side")]
    [InlineData("unknown-side")]
    [InlineData("side-case")]
    [InlineData("string")]
    [InlineData("fraction")]
    [InlineData("null-map")]
    [InlineData("null-value")]
    [InlineData("overflow")]
    [InlineData("not-voluntary")]
    [InlineData("null-audit")]
    [InlineData("scalar-audit")]
    [InlineData("scalar-mismatch")]
    [InlineData("map-mismatch")]
    [InlineData("claimed-die")]
    [InlineData("selected-die")]
    public void Validate_RejectsMissingOrContradictoryChoice(string change)
    {
        // The reused ordinary witness is independently valid before changing its no-roll lane.
        SpiritualWoundSourceWitness.Validate(SpiritualWoundSourceWitnessTests.Source());
        var source = Source();
        var exchange = Exchange(source);
        switch (change)
        {
            case "missing": exchange.Remove("spiritualWoundHarmfulMargins"); break;
            case "empty": exchange["spiritualWoundHarmfulMargins"] = new JsonObject(); break;
            case "wrong-side": exchange["spiritualWoundHarmfulMargins"] = new JsonObject { ["opposition"] = 10 }; break;
            case "unknown-side": exchange["spiritualWoundHarmfulMargins"]!["other"] = 10; break;
            case "side-case": exchange["spiritualWoundHarmfulMargins"] = new JsonObject { ["Player"] = 10 }; break;
            case "string": exchange["spiritualWoundHarmfulMargins"]!["player"] = "10"; break;
            case "fraction": exchange["spiritualWoundHarmfulMargins"]!["player"] = JsonNode.Parse("10.0"); break;
            case "null-map": exchange["spiritualWoundHarmfulMargins"] = null; break;
            case "null-value": exchange["spiritualWoundHarmfulMargins"]!["player"] = null; break;
            case "overflow": exchange["spiritualWoundHarmfulMargins"]!["player"] = JsonNode.Parse("9223372036854775808"); break;
            case "not-voluntary": exchange["voluntary"] = false; break;
            case "null-audit": exchange["diceAudit"] = null; break;
            case "scalar-audit": exchange["diceAudit"] = 1; break;
            case "scalar-mismatch": source["harmfulMargin"] = 11; break;
            case "map-mismatch": exchange["spiritualWoundHarmfulMargins"]!["player"] = 11; break;
            case "claimed-die": source["turnEvidence"]!["diceClaims"] = SpiritualWoundSourceWitnessTests.Source()["turnEvidence"]!["diceClaims"]!.DeepClone(); break;
            case "selected-die": source["selectedDiceIndices"] = new JsonArray(0); break;
        }
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.Validate(source));
    }

    /// <summary>Validates the new map even when no increasing-strain side reaches source preparation.</summary>
    /// <param name="change">Map misuse outside a lawful no-roll harm source.</param>
    [Theory]
    [InlineData("rolled")]
    [InlineData("no-increase")]
    [InlineData("extra-side")]
    [InlineData("alias")]
    [InlineData("nested")]
    public void ActionShape_RejectsMapMisuseBeforeSourceFiltering(string change)
    {
        var exchange = Exchange(Source());
        if (change == "rolled")
        {
            exchange = Exchange(SpiritualWoundSourceWitnessTests.Source());
            exchange["spiritualWoundHarmfulMargins"] = new JsonObject { ["opposition"] = 10 };
        }
        else if (change == "no-increase") exchange["after"]!["playerSideStrain"] = "clear";
        else if (change == "extra-side") exchange["spiritualWoundHarmfulMargins"]!["opposition"] = 10;
        else if (change == "alias")
        {
            exchange.Remove("spiritualWoundHarmfulMargins");
            exchange["SpiritualWoundHarmfulMargins"] = new JsonObject { ["player"] = 10 };
        }
        else
        {
            var margins = exchange["spiritualWoundHarmfulMargins"]!.DeepClone();
            exchange.Remove("spiritualWoundHarmfulMargins");
            exchange["incomingAction"]!["spiritualWoundHarmfulMargins"] = margins;
        }
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateSpiritualWoundSourceActionShape(exchange, "test.exchange", issues);
        Assert.Contains(issues, issue => issue.Code == "spiritual_source_no_roll_margin_invalid");
    }

    /// <summary>Reuses the lawful negotiate/voluntary incoming-pressure source shape.</summary>
    /// <param name="margin">Explicit chosen value.</param>
    /// <param name="maximum">Existing formula's expected ceiling.</param>
    /// <returns>Detached retained DATA, without current owner or publication authority.</returns>
    internal static JsonObject Source(long margin = 10, int maximum = 1)
    {
        var source = SpiritualWoundSourceWitnessTests.Source();
        var exchange = Exchange(source);
        exchange["operationType"] = "negotiate";
        exchange["voluntary"] = true;
        exchange["outcome"] = "setback";
        exchange.Remove("diceAudit");
        exchange.Remove("matchupAudit");
        exchange["incomingAction"] = new JsonObject
        {
            ["actorType"] = "guardian", ["actorId"] = "guardian-a", ["operationType"] = "pressure"
        };
        exchange["after"]!["playerSideStrain"] = "strained";
        exchange["after"]!["oppositionSideStrain"] = "clear";
        exchange["actionCostAudit"]!.AsObject().Remove("player");
        exchange["spiritualWoundHarmfulMargins"] = new JsonObject { ["player"] = margin };
        source["coordinate"] = "afterlifeSpiritualConflictUpdate.exchange.player";
        source["affectedSide"] = "player";
        source["actingActor"] = new JsonObject { ["actorKind"] = "guardian", ["actorId"] = "guardian-a" };
        source["affectedActor"] = new JsonObject { ["actorKind"] = "player_soul", ["actorId"] = "player_soul" };
        source["appliedArtId"] = "guardian:guardian-a:pressure";
        source["harmfulMargin"] = margin;
        source["maximumSeverityRank"] = maximum;
        source["selectedDiceIndices"] = new JsonArray();
        source["turnEvidence"]!["diceClaims"] = new JsonArray();
        source["exchangeJsonBase64"] = Encode(exchange);
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(exchange, "test.exchange", issues);
        Assert.Empty(issues);
        Seal(source);
        return source;
    }

    private static JsonObject Exchange(JsonObject source) => JsonNode.Parse(
        Encoding.UTF8.GetString(Convert.FromBase64String(source["exchangeJsonBase64"]!.GetValue<string>())))!.AsObject();

    private static string Encode(JsonObject exchange) => Convert.ToBase64String(
        Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(exchange)));

    private static void Seal(JsonObject source)
    {
        var digest = SpiritualWoundStateJson.Hash(source, "source", "sourceId", "sourceFingerprint");
        source["sourceFingerprint"] = digest;
        source["sourceId"] = "spiritual_source_" + digest[7..];
    }
}
