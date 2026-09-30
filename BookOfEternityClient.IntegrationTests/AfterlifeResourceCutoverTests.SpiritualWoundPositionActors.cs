using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Keeps an actual guardian burden scoped to its exact actor when a different signed supporter performs the later action.
    /// </summary>
    /// <param name="supporterActs">
    /// Whether the unwounded signed supporter, rather than the wounded lead, performs the later pressure.
    /// </param>
    /// <returns>
    /// A task completing after exact current-owner projection and full next-exchange admission preserve canonical position.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualGeneration_PositionIgnoresNonactingWoundedParticipant(bool supporterActs)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            signedDice: [15, 5, 13, 8], seedOriginalInputs: async original =>
            {
                var profiles = Assert.IsType<JsonObject>(await original.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
                var supporter = profiles["profiles"]!.AsArray().Single(value =>
                    value!["actorId"]!.GetValue<string>() == "guardian_frame")!.DeepClone();
                supporter["actorId"] = "guardian_position_supporter";
                supporter["resourceOwnerBindings"]![0]!["resourceOwnerId"] = "guardian_position_supporter";
                profiles["profiles"]!.AsArray().Add(supporter);
                await original.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
                var root = Assert.IsType<JsonObject>(await original.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
                var actor = root["activeConflict"]!["oppositionSide"]!["leadContestant"]!.DeepClone();
                actor["actorId"] = "guardian_position_supporter";
                root["activeConflict"]!["oppositionSide"]!["supporters"]!.AsArray().Add(actor);
                await original.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
            });
        await WritePositionBurdenExchangesAsync(context, playerWound: false, supporterActs ? "missing" : "exact");
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = candidate["activeConflict"]!.AsObject();
        var second = active["exchangeLog"]![1]!.AsObject();
        second["incomingAction"] = new JsonObject
        {
            ["actorType"] = "guardian", ["actorId"] = supporterActs ? "guardian_position_supporter" : "guardian_frame",
            ["operationType"] = "pressure"
        };
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var before = await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath);
        var (capture, source) = await InsertFirstPositionActorWoundAsync(context, lease, player: false);
        using (capture)
        {
            var issues = new List<ValidationIssue>();
            var mechanics = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext>(
                capture.PrepareSourceMechanics(source, issues));
            AssertNoConflictFrameErrors(issues);
            var burden = Assert.Single(mechanics.Contributions, row => row.Profile == "spiritual_position_burden");
            Assert.Equal("guardian_frame", burden.Actor.TargetId);
            Assert.Equal("guardian", burden.Actor.Kind);
            Assert.Equal("opposition", burden.ResolvedSide);
            Assert.Equal("pressure", burden.Operation);
            Assert.Equal(supporterActs ? 0 : 1,
                ValidationService.SpiritualWoundSourceSession.ReadCurrentEffectivePosition(mechanics, active, second));
            var next = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(next.Issues);
            Assert.Equal(1, next.Step!.Interval!.Ordinal);
            Assert.Equal("guardian:guardian_frame", source.Sources[1].AffectedActor);
            var closed = source.ReadAdmittedExchangeMechanics(capture)[1];
            var closedExchange = JsonNode.Parse(closed.ExchangeJson)!;
            Assert.Equal(supporterActs ? "guardian_position_supporter" : "guardian_frame",
                closedExchange["incomingAction"]!["actorId"]!.GetValue<string>());
            Assert.Equal("guardian", closedExchange["incomingAction"]!["actorType"]!.GetValue<string>());
            Assert.Equal("pressure", closedExchange["incomingAction"]!["operationType"]!.GetValue<string>());
            Assert.Equal(supporterActs ? 0 : 1, closed.EffectivePositionRank);
            Assert.Equal(before, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
        }
    }

    /// <summary>
    /// Resolves supported player identity aliases against a genuine projected player component without accepting a foreign actor id.
    /// </summary>
    /// <returns>
    /// A task completing after alias reads and the unchanged actual exchange prove the same actor-bound rank.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualGeneration_PositionPlayerAliasesKeepExactProjectedOwner()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(signedDice: [5, 15, 13, 8]);
        await WritePositionBurdenExchangesAsync(context, playerWound: true, modifier: "exact");
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = candidate["activeConflict"]!.AsObject();
        var second = active["exchangeLog"]![1]!.AsObject();
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var (capture, source) = await InsertFirstPositionActorWoundAsync(context, lease, player: true);
        using (capture)
        {
            var issues = new List<ValidationIssue>();
            var mechanics = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext>(
                capture.PrepareSourceMechanics(source, issues));
            AssertNoConflictFrameErrors(issues);
            var burden = Assert.Single(mechanics.Contributions, row => row.Profile == "spiritual_position_burden");
            Assert.Equal("player", burden.Actor.Kind);
            Assert.Equal("player_soul", burden.Actor.TargetId);
            foreach (var alias in new[] { "player", "player_soul", "soul" })
            {
                var named = active.DeepClone().AsObject();
                named["playerSide"]!["leadContestant"]!["actorType"] = alias;
                Assert.Equal(-1, ValidationService.SpiritualWoundSourceSession.ReadCurrentEffectivePosition(mechanics, named, second));
                named["playerSide"]!["leadContestant"]!["actorId"] = "foreign_soul";
                Assert.Throws<InvalidOperationException>(() =>
                    ValidationService.SpiritualWoundSourceSession.ReadCurrentEffectivePosition(mechanics, named, second));
            }
            var next = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(next.Issues);
            Assert.Equal(-1, source.ReadAdmittedExchangeMechanics(capture)[1].EffectivePositionRank);
        }
    }

    /// <summary>
    /// Captures the actual signed original, admits its first harmful exchange and inserts one rank-I pressure position wound.
    /// </summary>
    /// <param name="context">
    /// Fixture containing the complete signed original and authored later exchanges.
    /// </param>
    /// <param name="lease">
    /// Current canonical lease held by the caller across this unpublished execution.
    /// </param>
    /// <param name="player">
    /// Whether the first source and wound target are the player rather than the guardian lead.
    /// </param>
    /// <returns>
    /// Genuine capture and source owner; the caller must dispose the capture before releasing the lease.
    /// </returns>
    private static async Task<(ValidationService.SpiritualOriginalTurnCapture Capture,
        ValidationService.SpiritualWoundSourceSession Source)> InsertFirstPositionActorWoundAsync(
        ResourceMaterializationTestContext context, FileSystemManager.CanonicalWriteLease lease, bool player)
    {
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture);
        try
        {
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
            var first = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(first.Issues);
            var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
            var admitted = await capture.AdmitWoundSourceAsync(lease, first.Step!.Interval!, Assert.Single(source.Sources));
            AssertNoConflictFrameErrors(admitted.Issues);
            var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
            AssertNoConflictFrameErrors(offered.Issues);
            var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef,
                "spiritual_position_burden", "pressure", player ? "player" : "guardian").GetRawText())!;
            decision["proposal"]!["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!["payload"]!["magnitude"] = 1;
            var inserted = await capture.MaterializeWoundAsync(lease, admitted.Admission!,
                JsonSerializer.SerializeToElement(decision), player
                    ? "Чужое давление надломило волю души." : "Чужое давление надломило волю хранителя.");
            AssertNoConflictFrameErrors(inserted.Issues);
            Assert.NotNull(inserted.Wound);
            return (capture, source);
        }
        catch
        {
            capture.Dispose();
            throw;
        }
    }
}
