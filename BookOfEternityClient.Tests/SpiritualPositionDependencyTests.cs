using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks detached arithmetic comparisons only; genuine rank ownership and saved selection remain integration responsibilities.
/// </summary>
public sealed class SpiritualPositionDependencyTests
{
    /// <summary>
    /// Inserts, replaces or removes the recognized position row while preserving independent modifiers in their exact order.
    /// </summary>
    /// <param name="oldRank">
    /// Zero for no original row, or one for an existing player advantage.
    /// </param>
    /// <param name="newRank">
    /// Supplied comparison rank; this pure fixture does not mint live mechanics authority.
    /// </param>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, -1)]
    [InlineData(0, 2)]
    [InlineData(0, -2)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 2)]
    public void ExactGroup_ProjectsPositionAndPreservesIndependentRows(int oldRank, int newRank)
    {
        var audit = Audit();
        if (oldRank == 1)
        {
            audit["modifierBreakdown"]!["player"]!.AsArray().Insert(1, Position("player_advantaged", 2));
            audit["playerTotal"] = 15;
            audit["margin"] = 7;
        }
        var correction = Assert.IsType<ValidationService.SpiritualPositionDraftCorrection>(
            ValidationService.ProjectPositionDraftCorrection(audit, newRank, "/activeConflict/exchangeLog/1/diceAudit", [13, 8]));
        Assert.True(correction.Allows(audit));
        var projected = audit.DeepClone().AsObject();
        Assert.True(correction.TryApply(projected));
        Assert.True(correction.Allows(projected));
        Assert.Equal(13 + Math.Max(newRank, 0) * 2, projected["playerTotal"]!.GetValue<int>());
        Assert.Equal(8 + Math.Max(-newRank, 0) * 2, projected["oppositionTotal"]!.GetValue<int>());
        Assert.Equal(5 + newRank * 2, projected["margin"]!.GetValue<int>());
        Assert.Equal(newRank == 2 ? "decisive_player_success" : newRank == -2 ? "mixed_or_no_effect" : "player_success",
            projected["outcomeBand"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(audit["diceUsed"], projected["diceUsed"]));
        var independent = projected["modifierBreakdown"]!["player"]!.AsArray()
            .Where(row => row!["modifierType"]!.GetValue<string>() == "situational").ToArray();
        Assert.Equal(2, independent.Length);
        Assert.Equal("first", independent[0]!["source"]!.GetValue<string>());
        Assert.Equal("second", independent[1]!["source"]!.GetValue<string>());
        Assert.Equal(1, independent[0]!["value"]!.GetValue<int>());
        Assert.Equal(-1, independent[1]!["value"]!.GetValue<int>());
        var positions = new[] { "player", "opposition" }.SelectMany(side =>
            projected["modifierBreakdown"]![side]!.AsArray().Where(row =>
                row!["modifierType"]!.GetValue<string>() == "conflict_position").Select(row => (side, row))).ToArray();
        if (newRank == 0) Assert.Empty(positions);
        else
        {
            var row = Assert.Single(positions);
            Assert.Equal(newRank > 0 ? "player" : "opposition", row.side);
            Assert.Equal(Math.Abs(newRank) * 2, row.row!["value"]!.GetValue<int>());
        }
    }

    /// <summary>
    /// Rejects incoherent partial correction and independent evidence changes even when public array pointers exist.
    /// </summary>
    /// <param name="mutation">
    /// The forbidden candidate edit after a lawful complete projection.
    /// </param>
    [Theory]
    [InlineData("partial")]
    [InlineData("independent_value")]
    [InlineData("independent_order")]
    [InlineData("duplicate_position")]
    [InlineData("dice")]
    [InlineData("roll_mode")]
    [InlineData("total")]
    [InlineData("critical")]
    public void ExactGroup_RejectsUnprovedChanges(string mutation)
    {
        var original = Audit();
        var correction = Assert.IsType<ValidationService.SpiritualPositionDraftCorrection>(
            ValidationService.ProjectPositionDraftCorrection(original, 1, "/activeConflict/exchangeLog/1/diceAudit", [13, 8]));
        var candidate = original.DeepClone().AsObject();
        Assert.True(correction.TryApply(candidate));
        var rows = candidate["modifierBreakdown"]!["player"]!.AsArray();
        switch (mutation)
        {
            case "partial": candidate["margin"] = 5; break;
            case "independent_value": rows[0]!["value"] = 0; break;
            case "independent_order":
                var first = rows[0]!.DeepClone();
                rows[0] = rows[1]!.DeepClone();
                rows[1] = first;
                break;
            case "duplicate_position": rows.Add(Position("player_advantaged", 2)); break;
            case "dice": candidate["diceUsed"]![0]!["value"] = 14; break;
            case "roll_mode": candidate["rollMode"] = new JsonObject(); break;
            case "total": candidate["playerTotal"] = 16; break;
            case "critical": candidate["criticalResult"] = new JsonObject(); break;
        }
        Assert.False(correction.Allows(candidate));
    }

    /// <summary>
    /// Uses the ordinary roll-selection rules and does not promote a discarded natural twenty into a critical result.
    /// </summary>
    [Fact]
    public void Projection_UsesSelectedDieAndPreservesRollMode()
    {
        var original = Audit();
        original["diceUsed"]![0]!["selection"] = "selected";
        original["diceUsed"]!.AsArray().Add(new JsonObject
        {
            ["side"] = "player", ["sourceIndex"] = 2, ["sides"] = 20, ["value"] = 20, ["selection"] = "discarded"
        });
        original["rollMode"] = new JsonObject { ["player"] = new JsonObject
        {
            ["effectiveMode"] = "disadvantage", ["advantageSources"] = new JsonArray(),
            ["disadvantageSources"] = new JsonArray("existing adverse situation")
        } };
        var correction = Assert.IsType<ValidationService.SpiritualPositionDraftCorrection>(
            ValidationService.ProjectPositionDraftCorrection(original, 1, "/activeConflict/exchangeLog/1/diceAudit", [13, 8, 20]));
        var candidate = original.DeepClone().AsObject();
        Assert.True(correction.TryApply(candidate));
        Assert.Equal(15, candidate["playerTotal"]!.GetValue<int>());
        Assert.Equal(7, candidate["margin"]!.GetValue<int>());
        Assert.Equal("player_success", candidate["outcomeBand"]!.GetValue<string>());
        Assert.Null(candidate["criticalResult"]);
        Assert.True(JsonNode.DeepEquals(original["rollMode"], candidate["rollMode"]));
        Assert.True(JsonNode.DeepEquals(original["diceUsed"], candidate["diceUsed"]));
    }

    /// <summary>
    /// Exposes sorted raw fields while keeping closed rows, unrelated siblings and whole modifier groups immutable.
    /// </summary>
    [Fact]
    public void RawPolicy_RequiresDerivedProjectionAndPreservesClosedPrefix()
    {
        const string pointer = "/activeConflict/exchangeLog/1/diceAudit";
        var audit = Audit();
        var baseline = new JsonObject { ["activeConflict"] = new JsonObject
        {
            ["exchangeLog"] = new JsonArray(new JsonObject { ["exchangeId"] = "closed" },
                new JsonObject { ["exchangeId"] = "next", ["diceAudit"] = audit })
        } };
        var issues = new[] { new ValidationIssue("activeConflict.exchangeLog[1].diceAudit.modifierBreakdown.player",
            IssueSeverity.Error, "fixture", code: "afterlife_conflict_dice_missing_position_modifier") };
        Assert.Null(SpiritualWoundDependentDraftPolicy.Create(new JsonObject(), baseline, issues, null));
        var correction = Assert.IsType<ValidationService.SpiritualPositionDraftCorrection>(
            ValidationService.ProjectPositionDraftCorrection(audit, 1, pointer, [13, 8]));
        var policy = Assert.IsType<SpiritualWoundDependentDraftPolicy>(
            SpiritualWoundDependentDraftPolicy.Create(new JsonObject(), baseline, issues, null, [correction]));
        Assert.Equal(new[] { pointer + "/margin", pointer + "/modifierBreakdown", pointer + "/playerTotal" },
            policy.Fields.Select(field => field.JsonPointer));
        Assert.True(policy.Allows(baseline));
        var candidate = baseline.DeepClone().AsObject();
        Assert.True(policy.TryApplyPositionCorrections(candidate));
        Assert.True(policy.Allows(candidate));
        candidate["activeConflict"]!["exchangeLog"]![0]!["exchangeId"] = "replaced";
        Assert.False(policy.Allows(candidate));
        candidate["activeConflict"]!["exchangeLog"]![0]!["exchangeId"] = "closed";
        candidate["unrelated"] = true;
        Assert.False(policy.Allows(candidate));
    }

    /// <summary>
    /// Requires prescribed critical scalars and explicit GM constraints without inventing narrative during diagnostic walking.
    /// </summary>
    [Fact]
    public void NaturalCritical_RequiresBoundedNewScaffoldAndStopsAutomaticDiagnosticProjection()
    {
        var original = Audit();
        original["diceUsed"]![0]!["value"] = 20;
        original["diceUsed"]![1]!["value"] = 18;
        original["modifierBreakdown"]!["player"]![0]!["value"] = 3;
        original["playerTotal"] = 22;
        original["oppositionTotal"] = 18;
        original["margin"] = 4;
        original["outcomeBand"] = "player_success";
        var correction = Assert.IsType<ValidationService.SpiritualPositionDraftCorrection>(
            ValidationService.ProjectPositionDraftCorrection(original, -2, "/activeConflict/exchangeLog/1/diceAudit", [20, 18]));
        var candidate = original.DeepClone().AsObject();
        Assert.False(correction.TryApply(candidate));
        Assert.True(JsonNode.DeepEquals(original, candidate));
        candidate["modifierBreakdown"]!["opposition"]!.AsArray().Add(Position("opposition_dominant", 4));
        candidate["oppositionTotal"] = 22;
        candidate["margin"] = 0;
        candidate["criticalResult"] = new JsonObject
        {
            ["playerNaturalRoll"] = 20, ["oppositionNaturalRoll"] = 18,
            ["marginOutcomeBand"] = "mixed_or_no_effect", ["normalizedOutcomeBand"] = "player_success",
            ["scaleLimit"] = "The current pressure exchange only.", ["narrativeConstraint"] = "No additional unrelated outcome."
        };
        Assert.True(correction.Allows(candidate));
        candidate["criticalResult"]!["normalizedOutcomeBand"] = "opposition_success";
        Assert.False(correction.Allows(candidate));
        candidate["criticalResult"]!["normalizedOutcomeBand"] = "player_success";
        candidate["criticalResult"]!["unrelated"] = true;
        Assert.False(correction.Allows(candidate));
    }

    /// <summary>
    /// Builds a valid two-die audit with two independently cancelling player modifiers.
    /// </summary>
    /// <returns>
    /// A detached audit whose original margin five is player_success.
    /// </returns>
    private static JsonObject Audit() => JsonNode.Parse("""
        {"formulaVersion":"afterlife_spiritual_conflict_v1",
         "diceSource":"input/turn_request.json.preGeneratedDices1d20",
         "diceUsed":[{"side":"player","sourceIndex":0,"sides":20,"value":13},
                     {"side":"opposition","sourceIndex":1,"sides":20,"value":8}],
         "modifierBreakdown":{"player":[{"modifierType":"situational","source":"first","value":1},
                                           {"modifierType":"situational","source":"second","value":-1}],"opposition":[]},
         "playerTotal":13,"oppositionTotal":8,"margin":5,"outcomeBand":"player_success"}
        """)!.AsObject();

    /// <summary>
    /// Builds one existing canonical position modifier row for arithmetic fixtures.
    /// </summary>
    /// <param name="position">
    /// Existing position token.
    /// </param>
    /// <param name="value">
    /// Positive modifier magnitude.
    /// </param>
    /// <returns>
    /// A detached modifier carrying no gameplay authority.
    /// </returns>
    private static JsonObject Position(string position, int value) => new()
    {
        ["modifierType"] = "conflict_position", ["source"] = "conflictPosition", ["position"] = position, ["value"] = value
    };
}
