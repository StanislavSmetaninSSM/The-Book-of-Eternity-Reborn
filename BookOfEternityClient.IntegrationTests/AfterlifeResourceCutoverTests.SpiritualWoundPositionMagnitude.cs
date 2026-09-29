using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Consumes an actual rank-III two-step position component only after insertion, including its exact acting side.
    /// </summary>
    /// <param name="playerWound">
    /// Whether the inserted rank-III wound belongs to the player rather than the guardian.
    /// </param>
    /// <returns>
    /// A task completing after the actual next exchange admits the four-point modifier without canonical publication.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualGeneration_RankThreePositionMagnitudeTwoUsesActualComponent(bool playerWound)
    {
        var laterDie = playerWound ? 15 : 11;
        await using var context = await CreateCompleteConflictFrameContextAsync(
            signedDice: playerWound ? [5, 15, laterDie, 8] : [15, 5, laterDie, 8]);
        await WritePositionBurdenExchangesAsync(context, playerWound, "exact");
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = candidate["activeConflict"]!;
        var first = active["exchangeLog"]![0]!;
        var second = active["exchangeLog"]![1]!;
        var affected = playerWound ? "playerSideStrain" : "oppositionSideStrain";
        first["after"]![affected] = "overwhelmed";
        second["before"]![affected] = "overwhelmed";
        second["after"]![affected] = playerWound ? "overwhelmed" : "broken";
        active[affected] = second["after"]![affected]!.DeepClone();
        var dice = second["diceAudit"]!;
        var side = playerWound ? "opposition" : "player";
        dice["modifierBreakdown"]![side]![0]!["position"] = playerWound ? "opposition_dominant" : "player_dominant";
        dice["modifierBreakdown"]![side]![0]!["value"] = 4;
        dice["diceUsed"]![0]!["value"] = laterDie;
        dice["playerTotal"] = laterDie + (playerWound ? 0 : 4);
        dice["oppositionTotal"] = playerWound ? 12 : 8;
        dice["margin"] = playerWound ? 3 : 7;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var before = new Dictionary<string, byte[]?>();
        foreach (var path in new[] { AfterlifeSpiritualConflictState.StatePath, AfterlifeEntityProfileState.StatePath,
            ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
            WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, EffectIdentityState.StatePath })
            before.Add(path, await context.FileSystem.ReadFileBytesAsync(lease, path));
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var firstStep = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(firstStep.Issues);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var firstSource = Assert.Single(source.Sources);
        var closedBytes = firstSource.ExchangeJson;
        var admitted = await capture.AdmitWoundSourceAsync(lease, firstStep.Step!.Interval!, firstSource);
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef,
            "spiritual_position_burden", "pressure", playerWound ? "player" : "guardian").GetRawText())!;
        decision["proposal"]!["severity"] = "III";
        var definitions = decision["proposal"]!["consequenceDefinitions"]!.AsArray();
        definitions[0]!["definition"]!["components"]![0]!["payload"]!["magnitude"] = 2;
        foreach (var operation in new[] { "guard", "counter" })
        {
            var additional = definitions[0]!.DeepClone();
            additional["definitionRef"] = "position_" + operation;
            additional["definition"]!["definitionKey"] = "position_" + operation;
            additional["definition"]!["stacking"]!["stackKey"] = "position_stack_" + operation;
            additional["definition"]!["components"]![0]!["payload"]!["operation"] = operation;
            additional["definition"]!["components"]![0]!["payload"]!["magnitude"] = 1;
            definitions.Add(additional);
        }
        var inserted = await capture.MaterializeWoundAsync(lease, admitted.Admission!,
            JsonSerializer.SerializeToElement(decision), playerWound
                ? "Чужое давление надломило волю души." : "Чужое давление надломило волю хранителя.");
        AssertNoConflictFrameErrors(inserted.Issues);
        var wound = Assert.IsType<WoundMaterializationEnvelope>(inserted.Wound);
        Assert.Equal(3, wound.Severity.Rank);
        Assert.Equal(3, wound.Consequences.OwnedEffectSources.Definitions.Count);
        var pressure = wound.Consequences.OwnedEffectSources.Definitions.SelectMany(definition =>
            definition.GetProperty("components").EnumerateArray()).Single(component =>
            component.GetProperty("payload").GetProperty("operation").GetString() == "pressure");
        Assert.Equal(2, pressure.GetProperty("payload").GetProperty("magnitude").GetInt32());

        var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
        Assert.True(!advanced.Issues.Any(issue => issue.Severity == IssueSeverity.Error),
            string.Join(Environment.NewLine, advanced.Issues.Select(issue => $"{issue.Code}: {issue.Expected}; {issue.Actual}")));
        Assert.Equal(1, advanced.Step!.Interval!.Ordinal);
        Assert.Equal(new[] { 0, 1, 2, 3 }, source.ClaimedDice);
        Assert.Equal(2, source.Sources.Count);
        Assert.Same(firstSource, source.Sources[0]);
        Assert.Equal(closedBytes, firstSource.ExchangeJson);
        var retained = JsonNode.Parse(source.Sources[1].ExchangeJson)!;
        Assert.Equal("contested", retained["before"]!["conflictPosition"]!.GetValue<string>());
        Assert.Equal("contested", retained["after"]!["conflictPosition"]!.GetValue<string>());
        Assert.Equal(playerWound ? 3 : 7, retained["diceAudit"]!["margin"]!.GetValue<int>());
        foreach (var pair in before)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
    }
}
