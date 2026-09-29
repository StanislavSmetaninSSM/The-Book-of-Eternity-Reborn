using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Publishes a wound-adjusted terminal exchange with ordinary oldest-row pruning and binds the complete real resolution envelope.
    /// </summary>
    /// <returns>
    /// A task completing after actual common publication, ordinary validation and rejection of terminal or retained-history drift.
    /// </returns>
    [Fact]
    public Task OriginalSpiritualC4_TerminalComparisonPreservesPrunedHistoryAndRejectsDrift() =>
        VerifyCompletedPositionRootAsync(terminal: true);

    /// <summary>
    /// Publishes an active exchange whose display id also belongs to old history, selecting only the genuine planned active conflict.
    /// </summary>
    /// <returns>
    /// A task completing after actual publication and exact active/history comparison checks.
    /// </returns>
    [Fact]
    public Task OriginalSpiritualC4_ActiveComparisonAllowsOlderSameDisplayIdAndRejectsHistoryDrift() =>
        VerifyCompletedPositionRootAsync(terminal: false);

    /// <summary>
    /// Uses a genuinely published earlier position wound and closure as the baseline for one later active or terminal publication.
    /// </summary>
    /// <param name="terminal">
    /// Whether the new exchange closes through the ordinary resolution producer and prunes the oldest of twenty prior rows.
    /// </param>
    /// <returns>
    /// A task completing after exact publication and adversarial comparisons; no private execution owner is fabricated or revived.
    /// </returns>
    private static async Task VerifyCompletedPositionRootAsync(bool terminal)
    {
        var previous = await PublishPriorTerminalPositionWoundAsync();
        await using var context = await CreateCompleteConflictFrameContextAsync(
            signedDice: [15, 5], seedOriginalInputs: async original =>
            {
                await SeedOriginalIntakeBaselinesAsync(original);
                var fresh = Assert.IsType<JsonObject>(await original.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
                var started = AfterlifeSpiritualConflictState.ApplyUpdate(previous[AfterlifeSpiritualConflictState.StatePath],
                    new JsonObject { ["mode"] = "start", ["conflictState"] = fresh["activeConflict"]!.DeepClone() });
                Assert.Null(started["lastInvalidUpdate"]);
                // These signed historical siblings use the same established terminal-pruning fixture recipe.
                for (var index = 0; index < 19; index++)
                {
                    var historical = previous[AfterlifeSpiritualConflictState.StatePath]["recentConflicts"]![0]!.DeepClone();
                    historical["conflictId"] = "position_archived_" + index;
                    historical["terminalExchange"]!["exchangeId"] = "position_archived_exchange_" + index;
                    if (index == 0)
                        started["recentConflicts"]!.AsArray().Insert(0, historical);
                    else
                        started["recentConflicts"]!.AsArray().Add(historical);
                }
                foreach (var pair in previous.Where(pair => pair.Key != AfterlifeSpiritualConflictState.StatePath))
                    await original.WriteExactJsonAsync(pair.Key, pair.Value.ToJsonString());
                await original.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, started.ToJsonString());
            });
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5], sessionId: "completed_position_session",
            requestId: terminal ? "completed_position_terminal_43" : "completed_position_active_43");
        var signed = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        await WriteCompleteConflictFrameExchangeAsync(context);
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var exchange = candidate["activeConflict"]!["exchangeLog"]![0]!.AsObject();
        exchange["turnNumber"] = 43;
        exchange["exchangeId"] = "completed_position_exchange_43";
        exchange["diceAudit"]!["modifierBreakdown"]!["player"] = new JsonArray(new JsonObject
        {
            ["modifierType"] = "conflict_position", ["source"] = "conflictPosition",
            ["position"] = "player_dominant", ["value"] = 4
        });
        exchange["diceAudit"]!["playerTotal"] = 19;
        exchange["diceAudit"]!["margin"] = 14;
        if (terminal)
        {
            var resolution = new JsonObject
            {
                ["conflictId"] = "conflict_resource_cost", ["operationType"] = "pressure",
                ["resolvedAtTurn"] = 43, ["guardianId"] = "guardian_frame", ["playerOutcome"] = "won",
                ["diceAudit"] = exchange["diceAudit"]!.DeepClone(), ["terminalExchange"] = exchange.DeepClone(),
                ["summary"] = "The current signed spiritual conflict is complete."
            };
            Assert.False(resolution.ContainsKey("resolvedAtUtc"));
            candidate = AfterlifeSpiritualConflictState.ApplyUpdate(candidate,
                new JsonObject { ["mode"] = "resolve", ["resolution"] = resolution });
            Assert.Null(candidate["lastInvalidUpdate"]);
            Assert.Null(candidate["activeConflict"]);
            Assert.False(string.IsNullOrWhiteSpace(candidate["recentConflicts"]!.AsArray().Last()!["resolvedAtUtc"]!.GetValue<string>()));
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        await context.WriteExactJsonAsync(ProjectionNarrativePath,
            new JsonObject { ["response"] = "The guardian endures another spiritual exchange." }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var captured = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
            AssertNoConflictFrameErrors(captured.Issues);
            using (var warm = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture))
            {
                AssertNoConflictFrameErrors(await warm.BeginResourceExecutionAsync(lease));
                var advanced = await warm.AdvanceNextResourceExchangeAsync(lease);
                AssertNoConflictFrameErrors(advanced.Issues);
                var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(warm, "_source"));
                Assert.Equal(2, Assert.Single(source.ReadAdmittedExchangeMechanics(warm)).EffectivePositionRank);
                var committed = await warm.CommitC2FirstTransportAsync(lease, advanced.Step!.Interval!);
                AssertNoConflictFrameErrors(committed.Issues);
                Assert.Equal("committed", committed.Disposition);
            }
            var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(opened.Issues);
            using var offer = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            var completed = await offer.SubmitDecisionAsync(lease,
                JsonSerializer.SerializeToElement(new { opportunityRef = offer.Offer!.OpportunityRef, decision = "none" }), null);
            AssertNoConflictFrameErrors(completed.Issues);
            Assert.Equal("completed_unpublished", completed.Disposition);
            completed.Session?.Dispose();
        }
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        ValidationService.SpiritualOriginalTurnCapture.SpiritualCompletedConflictValidation completion;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out var authority));
            completion = authority.CompletedConflictValidation;
        }
        Assert.Throws<InvalidOperationException>(() => completion.ValidateRoot(candidate));
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues);
        Assert.NotNull(published.MechanicsPlan);
        Assert.Same(completion, published.SpiritualConflictValidation);
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var bytes = (await context.FileSystem.ReadFileBytesAsync(AfterlifeSpiritualConflictState.StatePath))!;
        Assert.Equal(20, root["recentConflicts"]!.AsArray().Count);
        if (terminal)
        {
            Assert.Null(root["activeConflict"]);
            Assert.True(JsonNode.DeepEquals(signed["recentConflicts"]![1], root["recentConflicts"]![0]));
            var resolution = root["recentConflicts"]!.AsArray().Last()!.AsObject();
            Assert.False(resolution.ContainsKey("exchangeLog"));
            Assert.Equal(resolution["conflictId"]!.GetValue<string>(),
                root["recentConflicts"]![0]!["conflictId"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(previous[AfterlifeSpiritualConflictState.StatePath]["recentConflicts"]![0],
                root["recentConflicts"]![0]));
            Assert.Throws<InvalidOperationException>(() => completion.Read(resolution, resolution["terminalExchange"]!.AsObject()));
        }
        else
        {
            Assert.Equal(root["activeConflict"]!["conflictId"]!.GetValue<string>(),
                root["recentConflicts"]![1]!["conflictId"]!.GetValue<string>());
            Assert.Equal(2, completion.Read(root["activeConflict"]!.AsObject(),
                root["activeConflict"]!["exchangeLog"]![0]!.AsObject()).EffectivePositionRank);
            Assert.Throws<InvalidOperationException>(() => completion.Read(root["recentConflicts"]![1]!.AsObject(),
                root["recentConflicts"]![1]!["terminalExchange"]!.AsObject()));
        }
        using (context.Validator.UseCompletedSpiritualConflictValidationScope(completion))
        {
            AssertNoConflictFrameErrors(await ValidatePublishedConflictPhaseAsync(context));
            foreach (var mutation in new[] { "history_remove", "current_replace_old", "history_order", "dice", "outcome", "new_active", "current_remove" })
            {
                var changed = root.DeepClone().AsObject();
                var history = changed["recentConflicts"]!.AsArray();
                if (mutation == "history_remove") history.RemoveAt(0);
                else if (mutation == "current_replace_old")
                {
                    var oldSameId = previous[AfterlifeSpiritualConflictState.StatePath]["recentConflicts"]![0]!.DeepClone();
                    if (terminal) history[history.Count - 1] = oldSameId;
                    else changed["activeConflict"] = oldSameId;
                }
                else if (mutation == "history_order")
                {
                    var first = history[0]!.DeepClone();
                    history[0] = history[1]!.DeepClone();
                    history[1] = first;
                }
                else if (mutation == "new_active") changed["activeConflict"] = new JsonObject { ["conflictId"] = "foreign_active" };
                else if (mutation == "current_remove")
                {
                    if (terminal) history.RemoveAt(history.Count - 1);
                    else changed["activeConflict"] = null;
                }
                else
                {
                    var current = terminal ? history.Last()!["terminalExchange"]! : changed["activeConflict"]!["exchangeLog"]![0]!;
                    if (mutation == "dice") current["diceAudit"]!["diceUsed"]![0]!["value"] = 14;
                    else current["outcome"] = "no_effect";
                }
                Assert.Throws<InvalidOperationException>(() => completion.ValidateRoot(changed));
                try
                {
                    await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, changed.ToJsonString());
                    Assert.Contains(await ValidatePublishedConflictPhaseAsync(context),
                        issue => issue.Code == "spiritual_completed_conflict_validation_mismatch");
                }
                finally
                {
                    await context.WriteExactBytesAsync(AfterlifeSpiritualConflictState.StatePath, bytes);
                }
            }
            AssertNoConflictFrameErrors(await ValidatePublishedConflictPhaseAsync(context));
        }
    }
}
