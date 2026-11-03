using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Tests free-side comparison DATA separately from current resource authority.</summary>
public sealed class SpiritualNoRollFreeSideTests
{
    /// <summary>Recognizes existing free operations without treating explicit zero as missing.</summary>
    /// <param name="operation">Existing declared free operation.</param>
    /// <param name="margin">Explicit GM choice.</param>
    [Theory]
    [InlineData("negotiate", 10L)]
    [InlineData("negotiate", 0L)]
    [InlineData("withdraw", 10L)]
    [InlineData("surrender", 10L)]
    public void Data_RecognizesOnlyExplicitKnownFreeSide(string operation, long margin)
    {
        var exchange = Exchange();
        exchange["operationType"] = operation;
        exchange[SpiritualWoundNoRollMargin.Property]!["player"] = margin;
        var before = exchange.ToJsonString();
        Assert.True(ValidationService.HasExplicitNoRollFreeSideData(exchange, "player"));
        Assert.Equal(before, exchange.ToJsonString());
        Assert.False(exchange["actionCostAudit"]!.AsObject().ContainsKey("player"));
    }

    /// <summary>Does not reinterpret missing, malformed, paid, force or explicit-null evidence as free.</summary>
    /// <param name="change">One refusal input.</param>
    [Theory]
    [InlineData("missing-map")]
    [InlineData("null-map")]
    [InlineData("empty-map")]
    [InlineData("bad-value")]
    [InlineData("overflow")]
    [InlineData("dice")]
    [InlineData("null-dice")]
    [InlineData("not-voluntary")]
    [InlineData("paid")]
    [InlineData("force")]
    [InlineData("unknown-operation")]
    [InlineData("missing-operation")]
    [InlineData("side-null-audit")]
    [InlineData("side-empty-audit")]
    [InlineData("root-null-audit")]
    [InlineData("root-scalar-audit")]
    [InlineData("null-matchup")]
    [InlineData("scalar-before")]
    [InlineData("wrong-side")]
    [InlineData("matchup-mismatch")]
    public void Data_RejectsUnprovedFreeSide(string change)
    {
        var exchange = Exchange();
        switch (change)
        {
            case "missing-map": exchange.Remove(SpiritualWoundNoRollMargin.Property); break;
            case "null-map": exchange[SpiritualWoundNoRollMargin.Property] = null; break;
            case "empty-map": exchange[SpiritualWoundNoRollMargin.Property] = new JsonObject(); break;
            case "bad-value": exchange[SpiritualWoundNoRollMargin.Property]!["player"] = "10"; break;
            case "overflow": exchange[SpiritualWoundNoRollMargin.Property]!["player"] = JsonNode.Parse("9223372036854775808"); break;
            case "dice": exchange["diceAudit"] = new JsonObject(); break;
            case "null-dice": exchange["diceAudit"] = null; break;
            case "not-voluntary": exchange["voluntary"] = false; break;
            case "paid": exchange["operationType"] = "pressure"; exchange["resolutionKind"] = "voluntary_concession"; break;
            case "force": exchange["operationType"] = "force_incarnation"; exchange["resolutionKind"] = "voluntary_concession"; break;
            case "unknown-operation": exchange["operationType"] = "unknown"; exchange["resolutionKind"] = "voluntary_concession"; break;
            case "missing-operation": exchange.Remove("operationType"); exchange["resolutionKind"] = "voluntary_concession"; break;
            case "side-null-audit": exchange["actionCostAudit"]!["player"] = null; break;
            case "side-empty-audit": exchange["actionCostAudit"]!["player"] = new JsonObject(); break;
            case "root-null-audit": exchange["actionCostAudit"] = null; break;
            case "root-scalar-audit": exchange["actionCostAudit"] = 1; break;
            case "null-matchup": exchange["matchupAudit"] = null; break;
            case "scalar-before": exchange["before"] = 1; break;
            case "matchup-mismatch": exchange["matchupAudit"] = new JsonObject { ["playerOperation"] = "pressure" }; break;
        }
        var before = exchange.ToJsonString();
        Assert.False(ValidationService.HasExplicitNoRollFreeSideData(exchange,
            change == "wrong-side" ? "other" : "player"));
        Assert.Equal(before, exchange.ToJsonString());
    }

    /// <summary>Uses existing incoming final-operation precedence and requires supplied matchup agreement.</summary>
    /// <param name="declared">Original incoming operation, possibly absent.</param>
    /// <param name="final">Actual final operation override, possibly absent.</param>
    /// <param name="matchup">Optional declared matchup operation.</param>
    /// <param name="expected">Whether the resolved side is known free comparison DATA.</param>
    [Theory]
    [InlineData("negotiate", null, null, true)]
    [InlineData("negotiate", "pressure", null, false)]
    [InlineData("pressure", "negotiate", null, true)]
    [InlineData("pressure", "negotiate", "negotiate", true)]
    [InlineData("pressure", "negotiate", "pressure", false)]
    [InlineData("pressure", null, "negotiate", false)]
    [InlineData(null, null, "negotiate", false)]
    [InlineData("withdraw", null, "withdraw", true)]
    [InlineData("force_incarnation", null, null, false)]
    [InlineData("unknown", null, null, false)]
    public void Data_PreservesIncomingFinalOperation(string? declared, string? final, string? matchup, bool expected)
    {
        var exchange = Exchange();
        exchange["operationType"] = "pressure";
        exchange["resolutionKind"] = "voluntary_concession";
        exchange["outcome"] = "success";
        exchange["after"]!["playerSideStrain"] = "clear";
        exchange["after"]!["oppositionSideStrain"] = "strained";
        exchange[SpiritualWoundNoRollMargin.Property] = new JsonObject { ["opposition"] = 10 };
        var cost = exchange["actionCostAudit"]!["opposition"]!.DeepClone();
        exchange["actionCostAudit"] = new JsonObject { ["player"] = cost };
        var incoming = new JsonObject();
        if (declared is not null) incoming["operationType"] = declared;
        if (final is not null) incoming["finalOperationType"] = final;
        exchange["incomingAction"] = incoming;
        if (matchup is not null) exchange["matchupAudit"] = new JsonObject
        { ["playerOperation"] = "pressure", ["oppositionOperation"] = matchup };
        Assert.Equal(expected, ValidationService.HasExplicitNoRollFreeSideData(exchange, "opposition"));
    }

    /// <summary>Never derives publication authority from a valid DATA classification alone.</summary>
    /// <param name="margin">Actual explicit zero or positive choice.</param>
    [Theory]
    [InlineData(0L)]
    [InlineData(10L)]
    public void UnownedContext_CannotEvaluateFreeSide(long margin)
    {
        var exchange = Exchange();
        exchange[SpiritualWoundNoRollMargin.Property]!["player"] = margin;
        Assert.True(ValidationService.HasExplicitNoRollFreeSideData(exchange, "player"));
        Assert.False(ValidationService.IsOwnedExplicitNoRollFreeSide(null, new JsonObject(), exchange, "player"));
    }

    private static JsonObject Exchange() => JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(
        SpiritualWoundNoRollSourceTests.Source()["exchangeJsonBase64"]!.GetValue<string>())))!.AsObject();
}
