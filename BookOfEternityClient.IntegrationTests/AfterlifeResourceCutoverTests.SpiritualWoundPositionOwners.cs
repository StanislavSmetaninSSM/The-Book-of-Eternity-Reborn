using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Cancels actual player and guardian position wounds before clamping, and refuses stale or foreign mechanics reads.
    /// </summary>
    /// <returns>
    /// A task completing after both actual materializations, the next admitted exchange and ownership rejection preserve canonical state.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualGeneration_PositionCancellationUsesBothActualOwnersAndRejectsStaleRead()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(signedDice: [12, 8, 9, 8]);
        await WritePositionBurdenExchangesAsync(context, playerWound: false, modifier: "missing");
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = candidate["activeConflict"]!.AsObject();
        var first = active["exchangeLog"]![0]!;
        var second = active["exchangeLog"]![1]!.AsObject();
        foreach (var strain in new[] { "playerSideStrain", "oppositionSideStrain" })
        {
            first["after"]![strain] = "broken";
            second["before"]![strain] = "broken";
            second["after"]![strain] = "broken";
            active[strain] = "broken";
        }
        second["outcome"] = "no_effect";
        second["diceAudit"]!["diceUsed"]![0]!["value"] = 9;
        second["diceAudit"]!["playerTotal"] = 9;
        second["diceAudit"]!["oppositionTotal"] = 8;
        second["diceAudit"]!["margin"] = 1;
        second["diceAudit"]!["outcomeBand"] = "mixed_or_no_effect";
        var dice = first["diceAudit"]!;
        dice["diceUsed"]![0]!["value"] = 12;
        dice["diceUsed"]![1]!["value"] = 8;
        dice["playerTotal"] = 12;
        dice["oppositionTotal"] = 8;
        dice["margin"] = 4;
        dice["outcomeBand"] = "player_success";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var canonical = await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath);
        var profiles = await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath);
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var step = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(step.Issues);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var firstSources = source.Sources.ToArray();
        Assert.Equal(2, firstSources.Length);
        var createdOwners = new HashSet<string>(StringComparer.Ordinal);
        foreach (var woundSource in firstSources)
        {
            var admitted = await capture.AdmitWoundSourceAsync(lease, step.Step!.Interval!, woundSource);
            AssertNoConflictFrameErrors(admitted.Issues);
            var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
            AssertNoConflictFrameErrors(offered.Issues);
            var player = woundSource.AffectedActor == "player_soul:player_soul";
            var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef,
                "spiritual_position_burden", "pressure", player ? "player" : "guardian").GetRawText())!;
            decision["proposal"]!["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!["payload"]!["magnitude"] = 1;
            var inserted = await capture.MaterializeWoundAsync(lease, admitted.Admission!,
                JsonSerializer.SerializeToElement(decision), player
                    ? "Чужое давление надломило волю души." : "Чужое давление надломило волю хранителя.");
            AssertNoConflictFrameErrors(inserted.Issues);
            createdOwners.Add(Assert.IsType<WoundMaterializationEnvelope>(inserted.Wound).Owner.OwnerId);
        }
        Assert.Equal(new[] { "guardian_frame", "player_soul" }, createdOwners.OrderBy(value => value, StringComparer.Ordinal));
        var mechanicsIssues = new List<ValidationIssue>();
        var mechanics = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext>(
            capture.PrepareSourceMechanics(source, mechanicsIssues));
        AssertNoConflictFrameErrors(mechanicsIssues);
        var burdens = mechanics.Contributions.Where(value => value.Profile == "spiritual_position_burden").ToArray();
        Assert.Equal(2, burdens.Length);
        Assert.Single(burdens, value => value.ResolvedSide == "player");
        Assert.Single(burdens, value => value.ResolvedSide == "opposition");
        Assert.All(burdens, value => Assert.Equal(1, value.Magnitude.GetInt32()));
        Assert.Equal(0, ValidationService.SpiritualWoundSourceSession.ReadCurrentEffectivePosition(mechanics, active, second));
        Assert.Equal(0, ValidationService.SpiritualWoundSourceSession.ReadCurrentEffectivePosition(mechanics, active, second));
        var foreign = active.DeepClone().AsObject();
        foreign["conflictId"] = "foreign_position_conflict";
        Assert.Throws<InvalidOperationException>(() =>
            ValidationService.SpiritualWoundSourceSession.ReadCurrentEffectivePosition(mechanics, foreign, second));
        var ambiguous = second.DeepClone().AsObject();
        ambiguous["incomingAction"] = new JsonObject
        {
            ["actorType"] = "player", ["actorId"] = "guardian_frame", ["operationType"] = "pressure"
        };
        Assert.Throws<InvalidOperationException>(() =>
            ValidationService.SpiritualWoundSourceSession.ReadCurrentEffectivePosition(mechanics, active, ambiguous));

        var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(advanced.Issues);
        Assert.Equal(1, advanced.Step!.Interval!.Ordinal);
        Assert.False(mechanics.IsCurrent);
        Assert.Throws<InvalidOperationException>(() =>
            ValidationService.SpiritualWoundSourceSession.ReadCurrentEffectivePosition(mechanics, active, second));
        Assert.Equal(new[] { 0, 1, 2, 3 }, source.ClaimedDice);
        Assert.Equal(2, source.Sources.Count);
        for (var index = 0; index < firstSources.Length; index++)
            Assert.Same(firstSources[index], source.Sources[index]);
        Assert.Equal(canonical, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
        Assert.Equal(profiles, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath));
    }
}
