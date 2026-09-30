using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Verifies control presence and raw-carrier boundaries independently of expensive original-turn execution.
/// </summary>
public sealed class SpiritualBindingCorrectionBoundaryTests
{
    /// <summary>
    /// Keeps absent, null and complete before-control values distinct in failed-result and terminal comparisons.
    /// </summary>
    /// <param name="shape">
    /// The original raw before-control shape: absent, explicit null or a complete none object.
    /// </param>
    [Theory]
    [InlineData("absent")]
    [InlineData("null")]
    [InlineData("none")]
    public void FailedResultAndFinalEcho_PreserveExactControlPresence(string shape)
    {
        var before = new JsonObject();
        if (shape != "absent") before["controlState"] = shape == "null" ? null :
            new JsonObject { ["level"] = "none", ["controllerSide"] = null };
        var success = new JsonObject { ["controlState"] = new JsonObject { ["level"] = "hindered" } };
        var original = new JsonObject { ["outcome"] = "success", ["before"] = before, ["after"] = success };
        var group = new ValidationService.SpiritualBindingDraftCorrection("/activeConflict/exchangeLog/1", original);
        var corrected = original.DeepClone().AsObject();
        corrected["outcome"] = "blocked";
        corrected["after"] = before.DeepClone();
        Assert.True(group.Allows(corrected));
        var wrong = corrected.DeepClone().AsObject();
        if (shape == "absent") wrong["after"]!["controlState"] = null;
        else wrong["after"]!.AsObject().Remove("controlState");
        Assert.False(group.Allows(wrong));
        var root = new JsonObject { ["activeConflict"] = success.DeepClone() };
        var terminal = new SpiritualBindingTerminalControlCorrection("/activeConflict/controlState", success, before);
        Assert.True(terminal.TryApply(root));
        Assert.Equal(before.ContainsKey("controlState"), root["activeConflict"]!.AsObject().ContainsKey("controlState"));
        Assert.True(JsonNode.DeepEquals(before["controlState"], root["activeConflict"]!["controlState"]));
        Assert.True(terminal.Allows(root, correctedOnly: true));
        if (shape == "absent") root["activeConflict"]!["controlState"] = null;
        else root["activeConflict"]!.AsObject().Remove("controlState");
        Assert.False(terminal.Allows(root, correctedOnly: true));
    }

    /// <summary>
    /// Selects the effective replacement alias and rejects explicit-exchange control or malformed wrapper fallback.
    /// </summary>
    [Fact]
    public void FinalEchoCarrier_RespectsReplacementPrecedenceAndExplicitPresence()
    {
        var fallback = new JsonObject { ["controlState"] = new JsonObject { ["level"] = "none" } };
        var preferred = new JsonObject { ["controlState"] = new JsonObject { ["level"] = "hindered" } };
        var exchange = new JsonObject { ["after"] = new JsonObject() };
        var update = new JsonObject { ["mode"] = "exchange", ["exchange"] = exchange,
            ["activeConflictAfter"] = preferred, ["conflictStateAfter"] = fallback };
        var raw = new JsonObject { [AfterlifeSpiritualConflictState.ResponseField] = update,
            ["activeConflict"] = new JsonObject { ["controlState"] = new JsonObject { ["level"] = "none" } } };
        Assert.Equal("/afterlifeSpiritualConflictUpdate/activeConflictAfter/controlState",
            AfterlifeSpiritualConflictState.ResolveRawFinalControl(raw)!.Value.Pointer);
        var policy = new SpiritualBindingTerminalControlCorrection(
            "/afterlifeSpiritualConflictUpdate/activeConflictAfter/controlState", preferred, fallback);
        var ignoredBefore = fallback.DeepClone();
        Assert.True(policy.TryApply(raw));
        Assert.True(JsonNode.DeepEquals(ignoredBefore, fallback));
        update.Remove("activeConflictAfter");
        Assert.False(policy.Allows(raw));
        Assert.Equal("/afterlifeSpiritualConflictUpdate/conflictStateAfter/controlState",
            AfterlifeSpiritualConflictState.ResolveRawFinalControl(raw)!.Value.Pointer);
        exchange["after"]!["controlState"] = null;
        Assert.Null(AfterlifeSpiritualConflictState.ResolveRawFinalControl(raw));
        raw[AfterlifeSpiritualConflictState.ResponseField] = null;
        Assert.Null(AfterlifeSpiritualConflictState.ResolveRawFinalControl(raw));
    }
}
