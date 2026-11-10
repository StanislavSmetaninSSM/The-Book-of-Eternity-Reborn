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
    /// Publishes a genuine C2-staged automatic guarantee through cold GameEngine startup without another GM decision.
    /// The saved frontier is prepared by real owners; this test does not simulate termination of a warm engine process.
    /// </summary>
    /// <returns>
    /// A task completing after exact saved identities, one wound and notification, two receipts and four resource spends are verified.
    /// </returns>
    [Fact]
    public async Task AcceptedTurnSpiritualContinuation_AutomaticGuaranteeColdResumePublishesOnceWithoutNewDecision()
    {
        var input = new QueuedConsoleInputSource([]);
        var logger = new SpiritualLifecycleTestLogger();
        GameEngine? warmEngine = null;
        var request = new TurnRequest
        {
            SessionId = "session_engine_spiritual", RequestId = "request_engine_spiritual_42", TurnNumber = 42,
            PlayerAction = "Удержать встречное духовное давление.", Timestamp = DateTime.UtcNow.ToString("O"),
            PreGeneratedDices1d20 = [15, 5, 15, 5]
        };
        var context = await AfterlifeResourceCutoverTests.CreateSpiritualGameEngineOriginalAsync(async original =>
        {
            await AfterlifeResourceCutoverTests.SeedSpiritualAutomaticGameEngineArtAsync(original);
            var chat = Assert.IsType<JsonObject>(await original.ReadJsonAsync("game_state/history/chat_log.json"));
            chat["sessionId"] = request.SessionId;
            await original.WriteExactJsonAsync("game_state/history/chat_log.json", chat.ToJsonString());
            warmEngine = CreateGameEngine(input, fileSystem: original.FileSystem, logger: logger);
            await InvokePrivateTaskAsync(warmEngine, "RefreshRuntimeStateAsync");
            GetPrivateField<GameLoop>(warmEngine, "_gameLoop").SetSession(request.SessionId, 41);
            await InvokePrivateTaskAsync(warmEngine, "EnsureClientOwnedSystemFilesHealthyAsync");
            request.ProgressionControl = await new ProgressionScheduleService(original.FileSystem,
                NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea");
            var rollback = await InvokePrivateTaskResultAsync(warmEngine, "CreatePreTurnBackup", "automatic-spiritual-original");
            await original.WriteExactJsonAsync("input/turn_request.json", JsonSerializer.Serialize(request, SnapshotHashJsonOpts));
            await InvokePrivateTaskResultAsync(warmEngine, "CreateCanonicalBaselineSnapshotAsync",
                request, rollback, "automatic-spiritual-original");
        });
        using var owned = new OriginalFixtureCompletion(context.RootPath,
            () => context.DisposeAsync().GetAwaiter().GetResult(), text => _directGachaOutput?.WriteLine(text));
        var storageWitness = new SpiritualLifecyclePublicationWitness(context.FileSystem,
            text => _directGachaOutput?.WriteLine(text));
        await AfterlifeResourceCutoverTests.WriteSpiritualAutomaticGameEngineExchangesAsync(context);
        await WriteSpiritualAutomaticLifecycleOutputsAsync(context, request);
        var originalImages = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json",
                     "game_state/control/pending_turn_snapshot.json", "game_state/control/pending_turn_snapshot.authority.json",
                     ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                     WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, EffectIdentityState.StatePath,
                     AfterlifeEntityProfileState.StatePath, AfterlifeSpiritualConflictState.StatePath })
            originalImages.Add(path, await context.FileSystem.ReadFileBytesAsync(path));

        await InvokePrivateTaskResultAsync(warmEngine!, "CaptureCurrentSessionGenerationAsync");
        JsonObject saved;
        byte[] command;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.True(Assert.IsType<bool>(await InvokePrivateTaskResultAsync(warmEngine!,
                "BeginAcceptedSpiritualCaptureAsync", lease)), logger.Describe());
            saved = await AfterlifeResourceCutoverTests.StageSpiritualAutomaticGameEngineSubmissionAsync(context, lease);
            command = Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        }
        var savedCheckpointBytes = Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(SpiritualWoundCaptureCheckpointState.StatePath));
        var savedPendingBytes = Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(SpiritualWoundDecisionPendingState.StatePath));
        var checkpoint = saved["checkpoint"]!;
        var submission = checkpoint["pendingSubmission"]!;
        Assert.Equal(request.SessionId, checkpoint["sessionId"]!.GetValue<string>());
        Assert.Equal(request.RequestId, checkpoint["requestId"]!.GetValue<string>());
        Assert.Equal(42, checkpoint["turn"]!.GetValue<int>());
        var manifest = ParseDependentSpiritualBytes(Assert.IsType<byte[]>(originalImages["game_state/control/pending_turn_snapshot.json"]));
        Assert.Equal(manifest["manifestPayloadHash"]!.GetValue<string>(), checkpoint["snapshotToken"]!.GetValue<string>());
        Assert.Equal(1, checkpoint["committedAdvance"]!.GetValue<int>());
        Assert.Equal(1, submission["priorCommittedAdvance"]!.GetValue<int>());
        Assert.Equal("guarantee_satisfied", submission["stagedDecision"]!["decision"]!.GetValue<string>());
        Assert.Null(submission["command"]);
        var satisfiedWoundId = submission["stagedDecision"]!["satisfiedWoundId"]!.GetValue<string>();
        var automaticFingerprint = submission["stagedDecision"]!["decisionFingerprint"]!.GetValue<string>();
        var materializeFingerprint = checkpoint["advances"]![0]!["newDecisionFingerprints"]![0]!.GetValue<string>();
        var prefix = checkpoint["allocations"]!.AsArray().Concat(submission["allocations"]!.AsArray())
            .Select(row => row!.DeepClone()).ToArray();
        foreach (var pair in originalImages)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(pair.Key));
        Assert.False(context.FileSystem.FileExists(SpiritualWoundOpportunityReceiptState.StatePath));
        Assert.False(context.FileSystem.FileExists("game_state/control/validation_repair_request.json"));

        byte[]? completedCheckpoint = null;
        byte[]? completedCommand = null;
        var receiptMutations = 0;
        var observedFrontiers = new List<string>();
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = async path =>
            {
                if (path == SpiritualWoundOpportunityReceiptState.StatePath)
                    Interlocked.Increment(ref receiptMutations);
                if (path != SpiritualWoundDecisionPendingState.StatePath || completedCheckpoint is not null)
                    return;
                var physical = context.FileSystem.ResolvePath(SpiritualWoundCaptureCheckpointState.StatePath);
                if (!File.Exists(physical))
                    return;
                var bytes = await File.ReadAllBytesAsync(physical);
                var current = ParseDependentSpiritualBytes(bytes)["checkpoint"]!;
                if (observedFrontiers.Count < 8)
                    observedFrontiers.Add($"advance={current["committedAdvance"]}; submission={current["pendingSubmission"] is not null}");
                if (current["committedAdvance"]!.GetValue<int>() == 2 && current["pendingSubmission"] is null)
                {
                    completedCheckpoint = bytes;
                    completedCommand = await File.ReadAllBytesAsync(context.FileSystem.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath));
                }
            }
        };
        var coldFs = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, hooks);
        var coldInput = new QueuedConsoleInputSource([]);
        var coldEngine = CreateGameEngine(coldInput, fileSystem: coldFs, logger: logger);
        var coldLoop = GetPrivateField<GameLoop>(coldEngine, "_gameLoop");
        coldLoop.SetSession(request.SessionId, 41);
        await InvokePrivateTaskAsync(coldEngine, "RefreshRuntimeStateAsync");
        await InvokePrivateTaskAsync(coldEngine, "NormalizePendingRepairArtifactsAsync");
        foreach (var pair in originalImages)
            Assert.Equal(pair.Value, await coldFs.ReadFileBytesAsync(pair.Key));
        Assert.Equal(savedCheckpointBytes, await coldFs.ReadFileBytesAsync(SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(savedPendingBytes, await coldFs.ReadFileBytesAsync(SpiritualWoundDecisionPendingState.StatePath));
        Assert.Equal(command, await coldFs.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));

        var observation = new DependentSpiritualGmObservation();
        using (var stop = new CancellationTokenSource())
        {
            var callback = RejectUnexpectedSpiritualAutomaticRequestAsync(coldFs, coldInput, observation, stop.Token);
            var cold = await AwaitDependentSpiritualPhaseAsync(
                InvokePrivateTaskResultAsync(coldEngine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync"),
                callback, stop, coldInput, observation, logger, TimeSpan.FromSeconds(420));
            Assert.True(cold.Error is null, $"Cold guarantee recovery failed: {cold.Error}. {logger.Describe()}");
            Assert.False(Assert.IsType<bool>(cold.Result), logger.Describe());
        }
        Assert.Null(observation.LastRepair);
        Assert.True(completedCheckpoint is not null && completedCommand is not null,
            $"Automatic completion was not observed; turn={coldLoop.TurnNumber}; frontiers=[{string.Join("; ", observedFrontiers)}]. {logger.Describe()}");
        Assert.Equal(command, completedCommand);
        var completed = ParseDependentSpiritualBytes(completedCheckpoint!)["checkpoint"]!;
        Assert.Null(completed["pendingSubmission"]);
        Assert.Equal(2, completed["committedAdvance"]!.GetValue<int>());
        Assert.Equal(automaticFingerprint, completed["advances"]!.AsArray().Last()!["newDecisionFingerprints"]![0]!.GetValue<string>());
        Assert.True(completed["allocations"]!.AsArray().Count >= prefix.Length);
        for (var index = 0; index < prefix.Length; index++)
            Assert.True(JsonNode.DeepEquals(prefix[index], completed["allocations"]![index]), $"Saved allocation {index} changed.");
        foreach (var field in new[] { "sessionId", "requestId", "turn", "snapshotToken" })
            Assert.True(JsonNode.DeepEquals(checkpoint[field], completed[field]), field);
        Assert.Equal(1, receiptMutations);
        await AssertSpiritualAutomaticPublicationAsync(context, coldEngine,
            Assert.IsType<byte[]>(originalImages[AfterlifeSpiritualConflictState.StatePath]),
            satisfiedWoundId, materializeFingerprint, automaticFingerprint);
        var afterSuccess = await ReadSpiritualEntryGuardFilesAsync(context);
        using (var stop = new CancellationTokenSource())
        {
            var repeated = await AwaitDependentSpiritualPhaseAsync(
                InvokePrivateTaskResultAsync(coldEngine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync"),
                Task.CompletedTask, stop, coldInput, observation, logger, TimeSpan.FromSeconds(60));
            Assert.True(repeated.Error is null, $"Repeated idle lifecycle failed: {repeated.Error}. {logger.Describe()}");
            Assert.False(Assert.IsType<bool>(repeated.Result), logger.Describe());
        }
        await AssertSpiritualEntryGuardFilesAsync(context, afterSuccess);
        Assert.Equal(1, receiptMutations);
        storageWitness.AssertSettled("automatic-cold-and-replay-actors-settled");
    }

    /// <summary>
    /// Observes the cold automatic path without supplying any GM response and releases an unexpected wait for diagnostics.
    /// </summary>
    /// <param name="fs">
    /// Fresh canonical filesystem whose public repair request is observed through shared physical reads.
    /// </param>
    /// <param name="input">
    /// Input queue receiving Escape only when an unexpected request must release the failed test's wait.
    /// </param>
    /// <param name="observation">
    /// Bounded phase evidence retaining the unexpected request and primary observer failure.
    /// </param>
    /// <param name="cancellationToken">
    /// Token cancelled when the bounded real cold caller completes.
    /// </param>
    /// <returns>
    /// An observer task that never writes a response or grants private authority.
    /// </returns>
    private static Task RejectUnexpectedSpiritualAutomaticRequestAsync(FileSystemManager fs,
        QueuedConsoleInputSource input, DependentSpiritualGmObservation observation, CancellationToken cancellationToken) => Task.Run(async () =>
    {
        try
        {
            var path = fs.ResolvePath("game_state/control/validation_repair_request.json");
            while (!cancellationToken.IsCancellationRequested)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        using var reader = new StreamReader(stream);
                        observation.LastRepair = await reader.ReadToEndAsync(cancellationToken);
                    }
                    catch (FileNotFoundException) { continue; }
                    throw new InvalidOperationException("Saved automatic guarantee unexpectedly requested GM input.");
                }
                await Task.Delay(25, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error)
        {
            observation.Failure = error;
            input.Enqueue(Key(ConsoleKey.Escape));
        }
    });
}
