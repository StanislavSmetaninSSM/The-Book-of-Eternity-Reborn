using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Uses the inserted wound's effective rank for binding prerequisites while maneuver still changes canonical position.
    /// </summary>
    /// <param name="operation">
    /// Actual second player action.
    /// </param>
    /// <param name="magnitude">
    /// One-step rank-I or two-step rank-III burden matching the guardian's actual operation.
    /// </param>
    /// <param name="canonicalShift">
    /// Whether a maneuver really improves its canonical after position.
    /// </param>
    /// <param name="expectedError">
    /// Exact required failure, or <see langword="null"/> for successful chronological admission.
    /// </param>
    /// <returns>
    /// A task completing after actual materialization and next-exchange validation preserve the original canonical files.
    /// </returns>
    [Theory]
    [InlineData("binding", 1, false, null)]
    [InlineData("force_binding", 2, false, null)]
    [InlineData("force_binding", 1, false, "afterlife_conflict_force_binding_without_strong_leverage")]
    [InlineData("maneuver", 1, true, null)]
    [InlineData("maneuver", 1, false, "afterlife_conflict_maneuver_missing_position_shift")]
    public async Task OriginalSpiritualGeneration_PositionControlsLeverageButPreservesCanonicalManeuver(
        string operation, int magnitude, bool canonicalShift, string? expectedError)
    {
        var laterDie = magnitude == 2 ? 11 : 13;
        await using var context = await CreateCompleteConflictFrameContextAsync(
            signedDice: [15, 5, laterDie, 8], seedOriginalInputs: async original =>
            {
                if (operation != "maneuver")
                {
                    var soul = Assert.IsType<JsonObject>(await original.ReadJsonAsync("game_state/meta/soul_state.json"));
                    soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]![operation] = 3;
                    await original.WriteExactJsonAsync("game_state/meta/soul_state.json", soul.ToJsonString());
                    var profiles = Assert.IsType<JsonObject>(await original.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
                    var player = profiles["profiles"]!.AsArray().Single(value => value!["actorId"]!.GetValue<string>() == "player_soul")!;
                    player["standardArts"]![operation] = 3;
                    await original.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
                }
                var root = Assert.IsType<JsonObject>(await original.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
                root["activeConflict"]!["controlState"] = new JsonObject { ["level"] = "none" };
                await original.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
            });
        await WritePositionBurdenExchangesAsync(context, playerWound: false, modifier: "exact");
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = candidate["activeConflict"]!;
        var first = active["exchangeLog"]![0]!;
        var second = active["exchangeLog"]![1]!;
        var strain = magnitude == 2 ? "overwhelmed" : "strained";
        first["before"]!["controlState"] = new JsonObject { ["level"] = "none" };
        first["after"]!["controlState"] = new JsonObject { ["level"] = "none" };
        first["after"]!["oppositionSideStrain"] = strain;
        second["before"] = first["after"]!.DeepClone();
        second["after"] = second["before"]!.DeepClone();
        second["operationType"] = operation;
        second["matchupAudit"]!["playerOperation"] = operation;
        second["matchupAudit"]!["primaryResolutionLane"] = operation;
        second["matchupAudit"]!["riskProfile"] = operation == "maneuver" ? "position_play" : "control_leverage";
        second["actionCostAudit"]!["player"]!["operationType"] = operation;
        if (operation == "maneuver")
        {
            second["matchupAudit"]!["oppositionOperation"] = "guard";
            var oppositionCost = second["actionCostAudit"]!["opposition"]!;
            oppositionCost["operationType"] = "guard";
            oppositionCost["baseCost"] = 2;
            oppositionCost["effectiveCost"] = 2;
            oppositionCost["after"] = 1;
            second["after"]!["conflictPosition"] = canonicalShift ? "player_advantaged" : "contested";
        }
        else
        {
            second["after"]!["controlState"] = new JsonObject
            {
                ["level"] = "hindered", ["controllerSide"] = "player", ["controlId"] = "position_binding_control",
                ["sourceOperation"] = operation, ["restrictedOperations"] = new JsonArray("maneuver", "binding"),
                ["summary"] = "Преимущество от раны хранителя позволяет наложить оковы."
            };
            var cost = second["actionCostAudit"]!["player"]!;
            cost["operationType"] = operation;
            cost["baseCost"] = operation == "binding" ? 4 : 5;
            cost["minCost"] = 2;
            cost["artTier"] = 3;
            cost["effectiveCost"] = 2;
            cost["after"] = 1;
        }
        var dice = second["diceAudit"]!;
        dice["diceUsed"]![0]!["value"] = laterDie;
        dice["modifierBreakdown"]!["player"]![0]!["position"] = magnitude == 2 ? "player_dominant" : "player_advantaged";
        dice["modifierBreakdown"]!["player"]![0]!["value"] = 2 * magnitude;
        dice["playerTotal"] = 15;
        dice["oppositionTotal"] = 8;
        dice["margin"] = 7;
        active["oppositionSideStrain"] = strain;
        active["conflictPosition"] = second["after"]!["conflictPosition"]!.DeepClone();
        active["controlState"] = second["after"]!["controlState"]!.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var canonical = await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath);
        var resources = await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath);
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var step = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(step.Issues);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var firstSource = Assert.Single(source.Sources);
        var admitted = await capture.AdmitWoundSourceAsync(lease, step.Step!.Interval!, firstSource);
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef,
            "spiritual_position_burden", operation == "maneuver" ? "guard" : "pressure").GetRawText())!;
        var definitions = decision["proposal"]!["consequenceDefinitions"]!.AsArray();
        definitions[0]!["definition"]!["components"]![0]!["payload"]!["magnitude"] = magnitude;
        if (magnitude == 2)
        {
            decision["proposal"]!["severity"] = "III";
            foreach (var other in new[] { "guard", "counter" })
            {
                var additional = definitions[0]!.DeepClone();
                additional["definitionRef"] = "position_" + other;
                additional["definition"]!["definitionKey"] = "position_" + other;
                additional["definition"]!["stacking"]!["stackKey"] = "position_stack_" + other;
                additional["definition"]!["components"]![0]!["payload"]!["operation"] = other;
                additional["definition"]!["components"]![0]!["payload"]!["magnitude"] = 1;
                definitions.Add(additional);
            }
        }
        var inserted = await capture.MaterializeWoundAsync(lease, admitted.Admission!,
            JsonSerializer.SerializeToElement(decision), "Чужое давление надломило волю хранителя.");
        AssertNoConflictFrameErrors(inserted.Issues);
        Assert.NotNull(inserted.Wound);
        var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
        if (expectedError is null)
        {
            AssertNoConflictFrameErrors(advanced.Issues);
            Assert.Equal(1, advanced.Step!.Interval!.Ordinal);
            Assert.Equal(new[] { 0, 1, 2, 3 }, source.ClaimedDice);
        }
        else
        {
            Assert.Null(advanced.Step);
            Assert.Contains(advanced.Issues, issue => issue.Code == expectedError);
            Assert.Equal(new[] { 0, 1 }, source.ClaimedDice);
        }
        Assert.Same(firstSource, Assert.Single(source.Sources));
        Assert.Equal(canonical, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
        Assert.Equal(resources, await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath));
    }
}
