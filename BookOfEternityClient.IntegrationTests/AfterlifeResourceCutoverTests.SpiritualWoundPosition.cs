using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Requires the exact later-exchange position modifier from an actually inserted wound while preserving canonical position.
    /// </summary>
    /// <param name="playerWound">
    /// Whether the first exchange wounds the player soul instead of the guardian.
    /// </param>
    /// <param name="modifier">
    /// Exact effective modifier, missing modifier, or a forged modifier on the opposite side.
    /// </param>
    /// <param name="woundOperation">
    /// The inserted component's exact operation; guard does not apply to either later pressure action.
    /// </param>
    /// <returns>
    /// A task completing after chronological admission or precise rejection and unchanged canonical files are proved.
    /// </returns>
    [Theory]
    [InlineData(false, "exact", "pressure")]
    [InlineData(true, "exact", "pressure")]
    [InlineData(false, "missing", "pressure")]
    [InlineData(true, "missing", "pressure")]
    [InlineData(false, "forged_side", "pressure")]
    [InlineData(true, "forged_side", "pressure")]
    [InlineData(false, "missing", "guard")]
    [InlineData(true, "missing", "guard")]
    public async Task OriginalSpiritualGeneration_InsertedPositionBurdenUsesEffectivePositionOnly(
        bool playerWound, string modifier, string woundOperation)
    {
        // A five-point base margin stays player_success at both lawful effective margins three and seven.
        await using var context = await CreateCompleteConflictFrameContextAsync(
            signedDice: playerWound ? [5, 15, 13, 8] : [15, 5, 13, 8]);
        await WritePositionBurdenExchangesAsync(context, playerWound, modifier);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var canonicalBefore = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in new[]
        {
            AfterlifeEntityProfileState.StatePath, AfterlifeSpiritualConflictState.StatePath,
            ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
            WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, EffectIdentityState.StatePath,
            SpiritualWoundOpportunityReceiptState.StatePath, "input/turn_request.json",
            "game_state/control/pending_turn_snapshot.json", "game_state/control/pending_turn_snapshot.authority.json"
        })
            canonicalBefore.Add(path, await context.FileSystem.ReadFileBytesAsync(lease, path));

        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        Assert.Equal(0, first.Step!.Interval!.Ordinal);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var firstSource = Assert.Single(source.Sources);
        var closedExchange = firstSource.ExchangeJson;
        Assert.Equal(playerWound ? "player_soul:player_soul" : "guardian:guardian_frame", firstSource.AffectedActor);
        Assert.Equal(new[] { 0, 1 }, source.ClaimedDice);
        var admitted = await capture.AdmitWoundSourceAsync(lease, first.Step.Interval, firstSource);
        AssertNoConflictFrameErrors(admitted.Issues);
        var opportunity = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(opportunity.Issues);
        var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(opportunity.Opportunity!.PublicRef,
            "spiritual_position_burden", woundOperation, playerWound ? "player" : "guardian").GetRawText())!;
        // The generic fixture's position magnitude is two; rank I explicitly requires the approved one-step component.
        decision["proposal"]!["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!["payload"]!["magnitude"] = 1;
        var narration = playerWound ? "Чужое давление надломило волю души." : "Чужое давление надломило волю хранителя.";
        var inserted = await capture.MaterializeWoundAsync(lease, admitted.Admission!,
            JsonSerializer.SerializeToElement(decision), narration);
        AssertNoConflictFrameErrors(inserted.Issues);
        var wound = Assert.IsType<WoundMaterializationEnvelope>(inserted.Wound);
        Assert.Equal(1, wound.Severity.Rank);
        Assert.Equal(playerWound ? "player_soul" : "guardian_frame", wound.Owner.OwnerId);
        var root = Assert.Single(wound.Consequences.OwnedEffectSources.RootBindings);
        var component = Assert.Single(Assert.Single(wound.Consequences.OwnedEffectSources.Definitions)
            .GetProperty("components").EnumerateArray());
        Assert.Equal("spiritual_position_burden", component.GetProperty("profile").GetString());
        Assert.Equal(woundOperation, component.GetProperty("payload").GetProperty("operation").GetString());
        Assert.Equal(1, component.GetProperty("payload").GetProperty("magnitude").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.EffectId));

        var second = await capture.AdvanceNextResourceExchangeAsync(lease);
        var diagnostics = string.Join(Environment.NewLine, second.Issues.Select(issue =>
            $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}"));
        Assert.True(!second.Issues.Any(issue => issue.Code == "spiritual_wound_profile_integration_required"), diagnostics);
        var accepted = modifier == "exact" || woundOperation == "guard";
        if (accepted)
        {
            Assert.True(!second.Issues.Any(issue => issue.Severity == IssueSeverity.Error), diagnostics);
            AssertNoConflictFrameErrors(second.Issues);
            Assert.Equal(1, second.Step!.Interval!.Ordinal);
            Assert.Equal(2, source.Sources.Count);
            Assert.Equal(new[] { 0, 1, 2, 3 }, source.ClaimedDice);
            var retained = JsonNode.Parse(source.Sources[1].ExchangeJson)!;
            Assert.Equal("contested", retained["before"]!["conflictPosition"]!.GetValue<string>());
            Assert.Equal("contested", retained["after"]!["conflictPosition"]!.GetValue<string>());
            Assert.Equal(woundOperation == "guard" ? 5 : playerWound ? 3 : 7,
                retained["diceAudit"]!["margin"]!.GetValue<int>());
        }
        else
        {
            Assert.Null(second.Step);
            Assert.Contains(second.Issues, issue => issue.Code == (modifier == "missing"
                ? "afterlife_conflict_dice_missing_position_modifier"
                : "afterlife_conflict_dice_unexpected_position_modifier_side"));
            Assert.Single(source.Sources);
            Assert.Equal(new[] { 0, 1 }, source.ClaimedDice);
        }
        Assert.Same(firstSource, source.Sources[0]);
        Assert.Equal(closedExchange, firstSource.ExchangeJson);
        foreach (var pair in canonicalBefore)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>
    /// Authors two lawful pressure results with fixed canonical position and optional effective-position audit on the second exchange.
    /// </summary>
    /// <param name="context">
    /// Genuine signed conflict with the matching four original dice and six action points per side.
    /// </param>
    /// <param name="playerWound">
    /// Whether the first harmful result targets the player rather than the guardian.
    /// </param>
    /// <param name="modifier">
    /// Selects the exact later effective row, its omission, or a row on the wrong side; totals remain arithmetically consistent.
    /// </param>
    /// <returns>
    /// A task completing after both complete original exchanges and their final strain projection are written.
    /// </returns>
    private static async Task WritePositionBurdenExchangesAsync(ResourceMaterializationTestContext context,
        bool playerWound, string modifier)
    {
        await WriteCompleteConflictFrameExchangeAsync(context);
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = candidate["activeConflict"]!;
        var first = active["exchangeLog"]![0]!;
        if (playerWound)
        {
            first["outcome"] = "setback";
            first["after"]!["playerSideStrain"] = "strained";
            first["after"]!["oppositionSideStrain"] = "clear";
            var firstDice = first["diceAudit"]!;
            firstDice["diceUsed"]![0]!["value"] = 5;
            firstDice["diceUsed"]![1]!["value"] = 15;
            firstDice["playerTotal"] = 5;
            firstDice["oppositionTotal"] = 15;
            firstDice["margin"] = -10;
            firstDice["outcomeBand"] = "decisive_opposition_success";
            active["playerSideStrain"] = "strained";
            active["oppositionSideStrain"] = "clear";
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        active = candidate["activeConflict"]!;
        var second = active["exchangeLog"]![1]!;
        second["outcome"] = "success";
        second["after"]!["oppositionSideStrain"] = playerWound ? "strained" : "fractured";
        active["oppositionSideStrain"] = second["after"]!["oppositionSideStrain"]!.DeepClone();
        var dice = second["diceAudit"]!;
        dice["diceUsed"]![0]!["value"] = 13;
        var modifiers = dice["modifierBreakdown"]!;
        Assert.Empty(modifiers["player"]!.AsArray());
        Assert.Empty(modifiers["opposition"]!.AsArray());
        var expectedSide = playerWound ? "opposition" : "player";
        var actualSide = modifier == "forged_side" ? (playerWound ? "player" : "opposition") : expectedSide;
        if (modifier != "missing")
            modifiers[actualSide]!.AsArray().Add(new JsonObject
            {
                ["modifierType"] = "conflict_position", ["source"] = "conflictPosition",
                ["position"] = playerWound ? "opposition_advantaged" : "player_advantaged", ["value"] = 2
            });
        var playerTotal = 13 + (modifier != "missing" && actualSide == "player" ? 2 : 0);
        var oppositionTotal = 8 + (modifier != "missing" && actualSide == "opposition" ? 2 : 0);
        dice["playerTotal"] = playerTotal;
        dice["oppositionTotal"] = oppositionTotal;
        dice["margin"] = playerTotal - oppositionTotal;
        dice["outcomeBand"] = "player_success";
        foreach (var exchange in active["exchangeLog"]!.AsArray())
        {
            Assert.Equal("contested", exchange!["before"]!["conflictPosition"]!.GetValue<string>());
            Assert.Equal("contested", exchange["after"]!["conflictPosition"]!.GetValue<string>());
        }
        Assert.Equal("contested", active["conflictPosition"]!.GetValue<string>());
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
    }
}
