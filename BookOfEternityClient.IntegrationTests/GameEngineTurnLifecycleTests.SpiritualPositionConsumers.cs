using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Preserves a genuinely published causal position through cold runtime refresh, actual pre-send validation and the next signed turn.
    /// </summary>
    /// <returns>
    /// A task completing after current packet checks, cold intrinsic checks and historical snapshot checks preserve all accepted mechanics.
    /// Warm C2 setup and ordinary terminal cleanup are explicit fixture boundaries, not a simulated warm GM wait.
    /// </returns>
    [Fact]
    public async Task SpiritualPosition_PublishedCausalitySurvivesColdPreSendAndNextSignedTurn()
    {
        var fixture = await CreatePublishedSpiritualPositionConsumerFixtureAsync();
        await using var context = fixture.Context;
        var completion = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.SpiritualCompletedConflictValidation>(
            fixture.Published.SpiritualConflictValidation);
        var selection = new GameStateValidationSelection(GameStateValidationPhase.AfterlifeSpiritualConflictState);
        var accepted = new Dictionary<string, byte[]?>();
        foreach (var path in new[] { AfterlifeSpiritualConflictState.StatePath, AfterlifeEntityProfileState.StatePath,
            ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
            WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, EffectIdentityState.StatePath,
            SpiritualWoundOpportunityReceiptState.StatePath })
            accepted.Add(path, await context.FileSystem.ReadFileBytesAsync(path));
        using (context.Validator.UseCompletedSpiritualConflictValidationScope(completion))
        {
            AssertNoSpiritualPositionConsumerErrors(await context.Validator.ValidateGameStateAsync(selection));
            var changed = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
            changed["activeConflict"]!["exchangeLog"]![1]!["diceAudit"]!["modifierBreakdown"]!["player"]![0]!["value"] = 4;
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, changed.ToJsonString());
            Assert.Contains(await context.Validator.ValidateGameStateAsync(selection),
                issue => issue.Code == "spiritual_completed_conflict_validation_mismatch");
            await context.FileSystem.WriteFileAtomicBytesAsync(AfterlifeSpiritualConflictState.StatePath,
                accepted[AfterlifeSpiritualConflictState.StatePath]!);
        }
        Assert.Contains(await context.Validator.ValidateGameStateAsync(selection),
            issue => issue.Code == "afterlife_conflict_dice_unexpected_position_modifier_for_contested");

        // Genuine publication above is complete; this bounded fixture invokes ordinary accepted cleanup directly.
        await InvokePrivateTaskAsync(fixture.Engine, "CleanupAcceptedTurnTerminalArtifactsAsync");
        Assert.False(context.FileSystem.FileExists("game_state/control/pending_turn_snapshot.json"));
        Assert.False(context.FileSystem.FileExists("input/turn_request.json"));
        var coldFs = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance);
        var log = new SpiritualLifecycleTestLogger();
        var cold = CreateGameEngine(new QueuedConsoleInputSource([Key(ConsoleKey.Escape)]), fileSystem: coldFs, logger: log);
        await InvokePrivateTaskAsync(cold, "RefreshRuntimeStateAsync");
        GetPrivateField<GameLoop>(cold, "_gameLoop").SetSession("session_position_consumer", 42);
        var coldValidator = GetPrivateField<ValidationService>(cold, "_validator");
        AssertNoSpiritualPositionConsumerErrors(await coldValidator.ValidateGameStateAsync(selection));
        Assert.True(Assert.IsType<bool>(await InvokePrivateTaskResultAsync(cold,
            "ValidateCurrentGameStateOrShowErrorsAsync", "перед отправкой хода")), log.Describe());
        using (coldValidator.UseCompletedSpiritualConflictValidationScope(completion))
            Assert.Contains(await coldValidator.ValidateGameStateAsync(selection),
                issue => issue.Code == "spiritual_completed_conflict_validation_mismatch");
        await using (var lease = await coldFs.AcquireCanonicalWriteLeaseAsync())
        {
            var denied = await coldValidator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
            using var unexpected = denied.Capture;
            Assert.Null(denied.Capture);
            Assert.Contains(denied.Issues, issue => issue.Severity == IssueSeverity.Error);
        }

        var nextRequest = new TurnRequest
        {
            SessionId = "session_position_consumer", RequestId = "position_consumer_43", TurnNumber = 43,
            PlayerAction = "Продолжить после принятого духовного обмена.", Timestamp = DateTime.UtcNow.ToString("O"),
            PreGeneratedDices1d20 = [2, 19],
            ProgressionControl = await new ProgressionScheduleService(coldFs,
                NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea")
        };
        var backup = await InvokePrivateTaskResultAsync(cold, "CreatePreTurnBackup", "position-consumer-next");
        await coldFs.WriteFileAtomicBytesAsync("input/turn_request.json",
            System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(nextRequest, SnapshotHashJsonOpts)));
        await InvokePrivateTaskResultAsync(cold, "CreateCanonicalBaselineSnapshotAsync",
            nextRequest, backup, "position-consumer-next");
        AssertNoSpiritualPositionConsumerErrors(await coldValidator.ValidateGameStateAsync(selection));
        using (coldValidator.UseCompletedSpiritualConflictValidationScope(completion))
            Assert.Contains(await coldValidator.ValidateGameStateAsync(selection),
                issue => issue.Code == "spiritual_completed_conflict_validation_mismatch");
        foreach (var pair in accepted)
            Assert.Equal(pair.Value, await coldFs.ReadFileBytesAsync(pair.Key));

        // A copied old row with a new identity is current, even when it claims an earlier turn.
        var forged = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(
            accepted[AfterlifeSpiritualConflictState.StatePath]!).TrimStart('\uFEFF'))!;
        var forgedLog = forged["activeConflict"]!["exchangeLog"]!.AsArray();
        var appended = forgedLog[1]!.DeepClone();
        appended["exchangeId"] = "position_forged_current";
        appended["exchangeAtTurn"] = 42;
        forgedLog.Add(appended);
        await coldFs.WriteFileAtomicBytesAsync(AfterlifeSpiritualConflictState.StatePath,
            System.Text.Encoding.UTF8.GetBytes(forged.ToJsonString()));
        Assert.Contains(await coldValidator.ValidateGameStateAsync(selection),
            issue => issue.Code == "afterlife_conflict_dice_unexpected_position_modifier_for_contested");
        await coldFs.WriteFileAtomicBytesAsync(AfterlifeSpiritualConflictState.StatePath,
            accepted[AfterlifeSpiritualConflictState.StatePath]!);
        AssertNoSpiritualPositionConsumerErrors(await coldValidator.ValidateGameStateAsync(selection));
    }

    /// <summary>
    /// Requires pre-turn preview to withhold unconditional canonical-position totals when an actual operation-scoped wound is active.
    /// </summary>
    /// <returns>
    /// A task completing after the real accepted wound suppresses an unconditional dice preview and receives an operation-specific reminder.
    /// </returns>
    [Fact]
    public async Task SpiritualPosition_ActivePublishedBurdenPreventsUnconditionalDicePreview()
    {
        var fixture = await CreatePublishedSpiritualPositionConsumerFixtureAsync(verifyUnwoundedPreview: true);
        await using var context = fixture.Context;
        await InvokePrivateTaskAsync(fixture.Engine, "CleanupAcceptedTurnTerminalArtifactsAsync");
        var preview = await new AfterlifeSpiritualConflictTurnPreviewService(context.FileSystem)
            .BuildAsync(43, [13, 8], "Chaos Sea");
        Assert.NotNull(preview);
        Assert.Equal("contested", preview["conflictPosition"]!.GetValue<string>());
        Assert.Null(preview["dicePreview"]);
        Assert.Contains(preview["authoringReminders"]!.AsArray(), reminder =>
            reminder!.GetValue<string>().Contains("spiritual_position_burden", StringComparison.Ordinal) &&
            reminder.GetValue<string>().Contains("operation", StringComparison.OrdinalIgnoreCase));
        var original = await context.FileSystem.ReadFileBytesAsync(AfterlifeSpiritualConflictState.StatePath);
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        try
        {
            foreach (var field in new[] { "actorId", "actorType" })
            {
                var changed = root.DeepClone();
                var actor = changed["activeConflict"]!["oppositionSide"]!["leadContestant"]!;
                actor[field] = " " + actor[field]!.GetValue<string>() + " ";
                await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, changed.ToJsonString());
                var unmatched = await new AfterlifeSpiritualConflictTurnPreviewService(context.FileSystem)
                    .BuildAsync(43, [13, 8], "Chaos Sea");
                Assert.NotNull(unmatched);
                Assert.NotNull(unmatched["dicePreview"]);
            }
        }
        finally
        {
            await context.WriteExactBytesAsync(AfterlifeSpiritualConflictState.StatePath, original!);
        }
    }

    /// <summary>
    /// Creates a full GameEngine-signed original and publishes its position-adjusted exchanges through genuine C2 and common C4.
    /// </summary>
    /// <param name="verifyUnwoundedPreview">
    /// Whether to verify the ordinary canonical preview before the genuine wound is created; defaults to no extra preview read.
    /// </param>
    /// <returns>
    /// Disposable physical context, signing engine and actual detached publication result.
    /// The caller owns disposal of the context.
    /// </returns>
    private async Task<(ResourceMaterializationTestContext Context, GameEngine Engine,
        AcceptedTurnCanonicalStateRefresh.Result Published)> CreatePublishedSpiritualPositionConsumerFixtureAsync(bool verifyUnwoundedPreview = false)
    {
        GameEngine? engine = null;
        var request = new TurnRequest
        {
            SessionId = "session_position_consumer", RequestId = "position_consumer_42", TurnNumber = 42,
            PlayerAction = "Удержать встречное духовное давление.", Timestamp = DateTime.UtcNow.ToString("O"),
            PreGeneratedDices1d20 = [15, 5, 13, 8]
        };
        var context = await AfterlifeResourceCutoverTests.CreateSpiritualGameEngineOriginalAsync(async original =>
        {
            engine = CreateGameEngine(new QueuedConsoleInputSource([Key(ConsoleKey.Escape)]), fileSystem: original.FileSystem);
            await InvokePrivateTaskAsync(engine, "RefreshRuntimeStateAsync");
            GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(request.SessionId, 41);
            await InvokePrivateTaskAsync(engine, "EnsureClientOwnedSystemFilesHealthyAsync");
            request.ProgressionControl = await new ProgressionScheduleService(original.FileSystem,
                NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea");
            var rollback = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "position-consumer-original");
            await original.WriteExactJsonAsync("input/turn_request.json", JsonSerializer.Serialize(request, SnapshotHashJsonOpts));
            await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync",
                request, rollback, "position-consumer-original");
        });
        try
        {
            if (verifyUnwoundedPreview)
            {
                var baseline = await new AfterlifeSpiritualConflictTurnPreviewService(context.FileSystem)
                    .BuildAsync(42, [13, 8], "Chaos Sea");
                Assert.NotNull(baseline);
                Assert.Equal("contested", baseline["conflictPosition"]!.GetValue<string>());
                Assert.NotNull(baseline["dicePreview"]);
            }
            var published = await AfterlifeResourceCutoverTests.PublishSpiritualPositionOriginalAsync(context);
            return (context, engine!, published);
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Reports precise canonical validation failures for the bounded causal-position consumer tests.
    /// </summary>
    /// <param name="issues">
    /// Actual selected-phase validation diagnostics.
    /// </param>
    private static void AssertNoSpiritualPositionConsumerErrors(IEnumerable<ValidationIssue> issues) =>
        Assert.True(!issues.Any(issue => issue.Severity == IssueSeverity.Error),
            string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Expected}; {issue.Actual}")));
}
