using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualConflictDangerModeTests
{
    [Theory]
    [InlineData("training", "conflictSeed")]
    [InlineData("controlled", "conflictSeed")]
    [InlineData("hostile", "conflictSeed")]
    [InlineData("annihilation", "conflictSeed")]
    [InlineData("training", "conflictState")]
    [InlineData("controlled", "conflictState")]
    [InlineData("hostile", "conflictState")]
    [InlineData("annihilation", "conflictState")]
    [InlineData("training", "activeConflict")]
    [InlineData("controlled", "activeConflict")]
    [InlineData("hostile", "activeConflict")]
    [InlineData("annihilation", "activeConflict")]
    public void Start_PreservesEachExactDeclaration(string mode, string seedProperty)
    {
        var baseline = AfterlifeSpiritualConflictState.CreateDefaultRoot();
        var update = new JsonObject { ["mode"] = "start", [seedProperty] = Active(mode) };
        var result = ApplyWithoutMutatingInputs(baseline, update);
        Assert.False(result.ContainsKey("lastInvalidUpdate"));
        Assert.Equal(mode, result["activeConflict"]!["dangerMode"]?.GetValue<string>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"Training\"")]
    [InlineData("\"training \"")]
    [InlineData("\" hostile\"")]
    [InlineData("\"unknown\"")]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Start_RejectsMissingOrNonExactDeclaration(string? raw)
    {
        var active = Active("training");
        SetRaw(active, raw);
        var baseline = AfterlifeSpiritualConflictState.CreateDefaultRoot();
        var result = ApplyWithoutMutatingInputs(
            baseline, new JsonObject { ["mode"] = "start", ["conflictSeed"] = active });
        Assert.Equal("start_invalid_danger_mode", result["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.Null(result["activeConflict"]);
        Assert.Empty(result["recentConflicts"]!.AsArray());
    }

    [Fact]
    public void Start_RejectsAConflictingRootEcho()
    {
        var result = ApplyWithoutMutatingInputs(
            AfterlifeSpiritualConflictState.CreateDefaultRoot(),
            new JsonObject
            {
                ["mode"] = "start",
                ["dangerMode"] = "hostile",
                ["conflictSeed"] = Active("training")
            });
        Assert.Equal("start_danger_mode_mismatch", result["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.Null(result["activeConflict"]);
    }

    [Theory]
    [InlineData("patch")]
    [InlineData("no_effect")]
    [InlineData("activeConflictAfter")]
    [InlineData("conflictStateAfter")]
    public void Exchange_OmissionPreservesTheAcceptedDeclaration(string route)
    {
        var baseline = Root("controlled");
        var update = Exchange();
        if (route == "no_effect")
            update["exchange"]!["outcome"] = "no_effect";
        else if (route != "patch")
        {
            var replacement = Active("controlled");
            replacement.Remove("dangerMode");
            update[route] = replacement;
        }
        var result = ApplyWithoutMutatingInputs(baseline, update);
        Assert.False(result.ContainsKey("lastInvalidUpdate"));
        Assert.Equal("controlled", result["activeConflict"]!["dangerMode"]?.GetValue<string>());
        Assert.Single(result["activeConflict"]!["exchangeLog"]!.AsArray());
    }

    [Theory]
    [InlineData("root", "\"hostile\"")]
    [InlineData("root", "null")]
    [InlineData("exchange", "\"hostile\"")]
    [InlineData("exchange", "null")]
    [InlineData("before", "\"hostile\"")]
    [InlineData("before", "null")]
    [InlineData("after", "\"hostile\"")]
    [InlineData("after", "null")]
    [InlineData("activeConflictAfter", "\"hostile\"")]
    [InlineData("activeConflictAfter", "null")]
    [InlineData("conflictStateAfter", "\"hostile\"")]
    [InlineData("conflictStateAfter", "null")]
    public void Exchange_RejectsChangedOrNullDeclarationOnEveryCarrier(string carrier, string raw)
    {
        var baseline = Root("training");
        var update = Exchange();
        Carrier(update, carrier)["dangerMode"] = JsonNode.Parse(raw);
        var result = ApplyWithoutMutatingInputs(baseline, update);
        Assert.Equal("exchange_danger_mode_change_without_authority", result["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(baseline["activeConflict"], result["activeConflict"]));
        Assert.True(JsonNode.DeepEquals(baseline["recentConflicts"], result["recentConflicts"]));
    }

    [Theory]
    [InlineData("root")]
    [InlineData("exchange")]
    [InlineData("before")]
    [InlineData("after")]
    [InlineData("activeConflictAfter")]
    [InlineData("conflictStateAfter")]
    public void Exchange_AcceptsExactEchoOnEveryCarrier(string carrier)
    {
        var update = Exchange();
        Carrier(update, carrier)["dangerMode"] = "training";
        var result = ApplyWithoutMutatingInputs(Root("training"), update);
        Assert.False(result.ContainsKey("lastInvalidUpdate"));
        Assert.Equal("training", result["activeConflict"]!["dangerMode"]?.GetValue<string>());
    }

    [Fact]
    public void Exchange_NoEffectDoesNotHideAChangedDeclaration()
    {
        var update = Exchange();
        update["exchange"]!["outcome"] = "no_effect";
        update["exchange"]!["after"]!["dangerMode"] = "annihilation";
        var baseline = Root("training");
        var result = ApplyWithoutMutatingInputs(baseline, update);
        Assert.Equal("exchange_danger_mode_change_without_authority", result["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(baseline["activeConflict"], result["activeConflict"]));
    }

    [Theory]
    [InlineData("training", false)]
    [InlineData("controlled", false)]
    [InlineData("hostile", false)]
    [InlineData("annihilation", false)]
    [InlineData("training", true)]
    [InlineData("controlled", true)]
    [InlineData("hostile", true)]
    [InlineData("annihilation", true)]
    public void TerminalClosure_CarriesDeclarationIntoTheProof(string mode, bool repair)
    {
        var result = ApplyWithoutMutatingInputs(Root(mode), Terminal(repair));
        Assert.False(result.ContainsKey("lastInvalidUpdate"));
        Assert.Null(result["activeConflict"]);
        var proof = Assert.Single(result["recentConflicts"]!.AsArray())!;
        Assert.Equal(mode, proof["dangerMode"]?.GetValue<string>());
        Assert.Equal(repair ? "repair_cancelled" : "resolved", proof["resolutionState"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(false, false, "\"hostile\"")]
    [InlineData(false, false, "null")]
    [InlineData(false, true, "\"hostile\"")]
    [InlineData(false, true, "null")]
    [InlineData(true, false, "\"hostile\"")]
    [InlineData(true, false, "null")]
    [InlineData(true, true, "\"hostile\"")]
    [InlineData(true, true, "null")]
    public void TerminalClosure_RejectsConflictingOrNullEcho(bool repair, bool atRoot, string raw)
    {
        var baseline = Root("training");
        var update = Terminal(repair);
        (atRoot ? update : update["resolution"]!.AsObject())["dangerMode"] = JsonNode.Parse(raw);
        var result = ApplyWithoutMutatingInputs(baseline, update);
        Assert.Equal("resolve_danger_mode_change_without_authority", result["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(baseline["activeConflict"], result["activeConflict"]));
        Assert.Empty(result["recentConflicts"]!.AsArray());
    }

    [Theory]
    [InlineData("exchange")]
    [InlineData("resolve")]
    [InlineData("repair_cancel")]
    public void ExistingConflictWithoutDeclarationHasNoFallback(string lifecycle)
    {
        var baseline = Root("training");
        baseline["activeConflict"]!.AsObject().Remove("dangerMode");
        var update = lifecycle == "exchange" ? Exchange() : Terminal(lifecycle == "repair_cancel");
        var result = ApplyWithoutMutatingInputs(baseline, update);
        Assert.Equal("active_conflict_invalid_danger_mode", result["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(baseline["activeConflict"], result["activeConflict"]));
    }

    private static JsonObject Root(string mode)
    {
        var root = AfterlifeSpiritualConflictState.CreateDefaultRoot();
        root["activeConflict"] = Active(mode);
        return root;
    }

    private static JsonObject Active(string mode) => new()
    {
        ["conflictId"] = "danger_mode_conflict",
        ["dangerMode"] = mode,
        ["realm"] = "Chaos Sea",
        ["sideModel"] = "direct_duel",
        ["status"] = "active",
        ["resolutionState"] = "active",
        ["oppositionSide"] = new JsonObject
        {
            ["leadContestant"] = new JsonObject
            {
                ["actorType"] = "guardian",
                ["actorId"] = "guardian_danger"
            }
        },
        ["exchangeLog"] = new JsonArray(),
        ["combatConditions"] = new JsonArray()
    };

    private static JsonObject Exchange() => new()
    {
        ["mode"] = "exchange",
        ["exchange"] = new JsonObject
        {
            ["exchangeId"] = "danger_exchange",
            ["outcome"] = "success",
            ["before"] = new JsonObject { ["conflictPosition"] = "contested" },
            ["after"] = new JsonObject { ["conflictPosition"] = "player_advantaged" }
        }
    };

    private static JsonObject Terminal(bool repair) => new()
    {
        ["mode"] = repair ? "repair_cancel" : "resolve",
        ["resolution"] = new JsonObject
        {
            ["resolvedAtTurn"] = 7,
            ["operationType"] = "guard",
            ["guardianId"] = "guardian_danger",
            ["playerOutcome"] = "won"
        }
    };

    private static JsonObject Carrier(JsonObject update, string carrier)
    {
        if (carrier == "root")
            return update;
        if (carrier == "exchange")
            return update["exchange"]!.AsObject();
        if (carrier is "before" or "after")
            return update["exchange"]![carrier]!.AsObject();
        update[carrier] = Active("training");
        return update[carrier]!.AsObject();
    }

    private static void SetRaw(JsonObject target, string? raw)
    {
        if (raw is null)
            target.Remove("dangerMode");
        else
            target["dangerMode"] = JsonNode.Parse(raw);
    }

    private static JsonObject ApplyWithoutMutatingInputs(JsonObject baseline, JsonObject update)
    {
        var before = baseline.ToJsonString();
        var input = update.ToJsonString();
        var result = AfterlifeSpiritualConflictState.ApplyUpdate(baseline, update);
        Assert.Equal(before, baseline.ToJsonString());
        Assert.Equal(input, update.ToJsonString());
        return result;
    }
}
