using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Theory]
    [InlineData(false, true, false, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, true, false, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, true, true, true, false)]
    [InlineData(true, false, true, false, true)]
    [InlineData(true, true, false, false, false, "status")]
    [InlineData(true, true, true, false, false, "ownerSide")]
    [InlineData(true, true, false, false, false, "operation")]
    [InlineData(true, false, false, false, false, "retained")]
    [InlineData(true, true, false, false, false, "upgraded")]
    [InlineData(true, false, false, false, false, "fallback_retained")]
    [InlineData(true, true, false, false, false, "fallback_replaced")]
    public async Task OriginalSpiritualGeneration_InsertedTempoBurdenDeniesGain(
        bool materialize, bool grantTempo, bool playerWound, bool forgeBefore, bool noOtherDelta,
        string? paddedField = null)
        => await AssertInsertedPlayerOrOppositionBurdenAsync(materialize, grantTempo, playerWound,
            forgeBefore, noOtherDelta, paddedField, playerCost: null);

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task OriginalSpiritualGeneration_PaddedPlayerOperationCannotHideCostBurden(int cost)
        => await AssertInsertedPlayerOrOppositionBurdenAsync(true, false, true,
            false, true, null, cost);

    [Theory]
    [InlineData(false, false, "guard")]
    [InlineData(false, true, "guard")]
    [InlineData(true, false, "guard")]
    [InlineData(true, true, "guard")]
    [InlineData(true, false, "counter")]
    [InlineData(true, true, "counter")]
    [InlineData(true, false, "guard", true)]
    [InlineData(true, true, "guard", true)]
    public async Task OriginalSpiritualGeneration_RankFourWoundForbidsMatchingArt(
        bool materialize, bool playerWound, string art, bool noEffect = false)
        => await AssertInsertedPlayerOrOppositionBurdenAsync(materialize, !noEffect, playerWound,
            false, noEffect, null, null, art);

    /// <summary>
    /// Executes an actual wound insertion followed by the affected actor's next audited operation.
    /// </summary>
    /// <param name="materialize">
    /// Whether to insert the offered wound instead of declining it.
    /// </param>
    /// <param name="grantTempo">
    /// Whether the next operation proposes an available tempo window.
    /// </param>
    /// <param name="playerWound">
    /// Whether the first exchange wounds the player rather than the guardian.
    /// </param>
    /// <param name="forgeBefore">
    /// Whether the next before snapshot invents the proposed after window.
    /// </param>
    /// <param name="noOtherDelta">
    /// Whether the next exchange preserves its snapshots and declares no effect.
    /// </param>
    /// <param name="paddedField">
    /// Optional normalization or retained-window regression variant.
    /// </param>
    /// <param name="playerCost">
    /// Optional declared player cost, selecting the cost profile and a padded player operation.
    /// </param>
    /// <param name="forbiddenArt">
    /// Optional forbidden art, selecting a real rank-IV wound with four independent art restrictions.
    /// </param>
    /// <returns>
    /// A task that completes after checking acceptance and unpublished canonical state.
    /// </returns>
    private static async Task AssertInsertedPlayerOrOppositionBurdenAsync(
        bool materialize, bool grantTempo, bool playerWound, bool forgeBefore, bool noOtherDelta,
        string? paddedField, int? playerCost, string? forbiddenArt = null)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: playerWound ? [5, 15, 12, 8] : [15, 5, 4, 12]);
        await WriteCompleteConflictFrameExchangeAsync(context);
        if (playerWound)
        {
            var firstCandidate = await ReadProjectedSourceContinuationCandidateAsync(context);
            var firstActive = firstCandidate["activeConflict"]!;
            var firstExchange = firstActive["exchangeLog"]![0]!;
            firstExchange["outcome"] = "setback";
            firstExchange["after"]!["playerSideStrain"] = "strained";
            firstExchange["after"]!["oppositionSideStrain"] = "clear";
            firstExchange["diceAudit"]!["diceUsed"]![0]!["value"] = 5;
            firstExchange["diceAudit"]!["diceUsed"]![1]!["value"] = 15;
            firstExchange["diceAudit"]!["playerTotal"] = 5;
            firstExchange["diceAudit"]!["oppositionTotal"] = 15;
            firstExchange["diceAudit"]!["margin"] = -10;
            firstExchange["diceAudit"]!["outcomeBand"] = "decisive_opposition_success";
            firstActive["playerSideStrain"] = "strained";
            firstActive["oppositionSideStrain"] = "clear";
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, firstCandidate.ToJsonString());
        }
        if (forbiddenArt != null)
        {
            var severeCandidate = await ReadProjectedSourceContinuationCandidateAsync(context);
            var severeActive = severeCandidate["activeConflict"]!;
            var strainKey = playerWound ? "playerSideStrain" : "oppositionSideStrain";
            severeActive["exchangeLog"]![0]!["after"]![strainKey] = "broken";
            severeActive[strainKey] = "broken";
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, severeCandidate.ToJsonString());
        }
        if (paddedField is "retained" or "upgraded" or "fallback_retained" or "fallback_replaced")
        {
            var priorCandidate = await ReadProjectedSourceContinuationCandidateAsync(context);
            var priorActive = priorCandidate["activeConflict"]!;
            var priorTempo = new JsonObject
            {
                ["advantageId"] = "tempo_second", ["status"] = "available",
                ["level"] = "advantage", ["ownerSide"] = "opposition", ["sourceOperation"] = "guard",
                ["sourceExchangeId"] = "exchange_source_second", ["summary"] = "Прежнее темповое окно."
            };
            if (paddedField.StartsWith("fallback_", StringComparison.Ordinal))
            {
                priorTempo["advantageId"] = "";
                priorTempo["sourceId"] = "prior_window";
            }
            priorActive["exchangeLog"]![0]!["after"]!["tempoAdvantage"] = priorTempo;
            priorActive["tempoAdvantage"] = priorTempo.DeepClone();
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, priorCandidate.ToJsonString());
        }
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = candidate["activeConflict"]!.AsObject();
        var exchange = active["exchangeLog"]![1]!.AsObject();
        exchange["outcome"] = playerWound ? "success" : grantTempo ? "setback" : "no_effect";
        exchange["after"] = exchange["before"]!.DeepClone();
        exchange["matchupAudit"]!["oppositionOperation"] = playerWound ? "pressure" : "guard";
        if (playerWound)
        {
            exchange["operationType"] = "guard";
            exchange["matchupAudit"]!["playerOperation"] = "guard";
            exchange["matchupAudit"]!["primaryResolutionLane"] = "guard";
            exchange["matchupAudit"]!["riskProfile"] = "safe_defense";
            exchange["incomingAction"] = new JsonObject
            {
                ["operationType"] = "pressure", ["actorType"] = "guardian",
                ["actorId"] = "guardian_frame", ["summary"] = "Хранитель продолжает духовное давление."
            };
            exchange["after"]!["playerSideStrain"] = forbiddenArt == null ? "clear" : "overwhelmed";
            active["playerSideStrain"] = forbiddenArt == null ? "clear" : "overwhelmed";
        }
        var cost = exchange["actionCostAudit"]![playerWound ? "player" : "opposition"]!;
        cost["operationType"] = "guard";
        cost["baseCost"] = 2;
        cost["effectiveCost"] = 2;
        cost["after"] = 1;
        var dice = exchange["diceAudit"]!;
        dice["diceUsed"]![0]!["value"] = playerWound ? 12 : 4;
        dice["diceUsed"]![1]!["value"] = playerWound ? 8 : 12;
        dice["playerTotal"] = playerWound ? 12 : 4;
        dice["oppositionTotal"] = playerWound ? 8 : 12;
        dice["margin"] = playerWound ? 4 : -8;
        dice["outcomeBand"] = playerWound ? "player_success" : "decisive_opposition_success";
        active["oppositionSideStrain"] = exchange["after"]!["oppositionSideStrain"]!.DeepClone();
        if (grantTempo)
        {
            var tempo = new JsonObject
            {
                ["advantageId"] = "tempo_second", ["status"] = "available",
                ["level"] = "advantage", ["ownerSide"] = playerWound ? "player" : "opposition", ["sourceOperation"] = "guard",
                ["sourceExchangeId"] = "exchange_source_second", ["summary"] = "Защита даёт темп."
            };
            if (paddedField == "operation")
                exchange["matchupAudit"]!["oppositionOperation"] = " guard ";
            else if (paddedField == "upgraded")
                tempo["level"] = "great_advantage";
            else if (paddedField == "fallback_replaced")
            {
                tempo["advantageId"] = "";
                tempo["sourceId"] = "replacement_window";
            }
            else if (paddedField != null)
                tempo[paddedField] = " " + tempo[paddedField]!.GetValue<string>() + " ";
            exchange["after"]!["tempoAdvantage"] = tempo;
            active["tempoAdvantage"] = tempo.DeepClone();
            if (forgeBefore)
                exchange["before"]!["tempoAdvantage"] = tempo.DeepClone();
        }
        if (noOtherDelta)
        {
            exchange["outcome"] = "no_effect";
            exchange["after"] = exchange["before"]!.DeepClone();
            active["playerSideStrain"] = exchange["before"]!["playerSideStrain"]!.DeepClone();
        }
        if (paddedField == "retained")
        {
            exchange["before"]!["tempoAdvantage"]!["summary"] = "То же окно, другое описание.";
            exchange["after"]!["tempoAdvantage"]!["summary"] = "То же окно, другое описание.";
            active["tempoAdvantage"] = exchange["after"]!["tempoAdvantage"]!.DeepClone();
        }
        if (playerCost.HasValue)
        {
            exchange["operationType"] = " guard ";
            exchange["actionCostAudit"]!["player"]!["effectiveCost"] = playerCost.Value;
            exchange["actionCostAudit"]!["player"]!["after"] = 3 - playerCost.Value;
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var canonicalBefore = await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath);
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var firstSource = Assert.Single(source.Sources);
        var admitted = await capture.AdmitWoundSourceAsync(lease, first.Step!.Interval!, firstSource);
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decision = materialize ? OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef,
            forbiddenArt != null ? "spiritual_art_restriction" :
                playerCost.HasValue ? "spiritual_action_cost_burden" : "spiritual_tempo_burden",
            forbiddenArt ?? "guard", playerWound ? "player" : "guardian") :
            System.Text.Json.JsonSerializer.SerializeToElement(new { opportunityRef = offered.Opportunity!.PublicRef, decision = "none" });
        if (materialize && forbiddenArt != null)
        {
            var severeDecision = JsonNode.Parse(decision.GetRawText())!;
            var proposal = severeDecision["proposal"]!;
            proposal["severity"] = "IV";
            var definitions = proposal["consequenceDefinitions"]!.AsArray();
            foreach (var art in new[] { "binding", "break_binding", "maneuver" })
            {
                var additional = definitions[0]!.DeepClone();
                additional["definitionRef"] = "forbid_" + art;
                additional["definition"]!["definitionKey"] = "forbid_" + art;
                additional["definition"]!["stacking"]!["stackKey"] = "stack_forbid_" + art;
                additional["definition"]!["components"]![0]!["payload"]!["operation"] = art;
                definitions.Add(additional);
            }
            decision = System.Text.Json.JsonSerializer.SerializeToElement(severeDecision);
        }
        var wound = await capture.MaterializeWoundAsync(lease, admitted.Admission!, decision,
            materialize ? playerWound ? "Чужое давление надломило волю души." : "Чужое давление надломило волю хранителя." : null);
        AssertNoConflictFrameErrors(wound.Issues);
        Assert.Equal(materialize, wound.Wound != null);
        var second = await capture.AdvanceNextResourceExchangeAsync(lease);
        if (materialize && forbiddenArt == "guard")
        {
            Assert.Null(second.Step);
            Assert.Contains(second.Issues, issue => issue.Code == "spiritual_wound_art_forbidden");
            Assert.Equal(new[] { 0, 1 }, source.ClaimedDice);
        }
        else if (playerCost == 2)
        {
            Assert.Null(second.Step);
            Assert.Contains(second.Issues, issue => issue.Code == "afterlife_conflict_action_cost_mismatch");
            Assert.Equal(new[] { 0, 1 }, source.ClaimedDice);
        }
        else if (materialize && grantTempo && forbiddenArt == null)
        {
            Assert.Null(second.Step);
            Assert.Contains(second.Issues, issue => issue.Code == "spiritual_wound_tempo_gain_forbidden");
            if (forgeBefore)
                Assert.Contains(second.Issues, issue => issue.Code == "spiritual_wound_tempo_frontier_mismatch");
            Assert.Equal(new[] { 0, 1 }, source.ClaimedDice);
        }
        else
        {
            AssertNoConflictFrameErrors(second.Issues);
            Assert.Equal(1, second.Step!.Interval!.Ordinal);
            Assert.Equal(new[] { 0, 1, 2, 3 }, source.ClaimedDice);
        }
        Assert.Same(firstSource, Assert.Single(source.Sources));
        Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }
}
