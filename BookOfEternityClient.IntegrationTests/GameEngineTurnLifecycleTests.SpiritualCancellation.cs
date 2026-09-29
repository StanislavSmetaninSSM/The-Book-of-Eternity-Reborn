using System.Text;
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
    /// Cancels a real spiritual decision wait through Escape and lets the actual caller restore the complete signed rollback baseline.
    /// </summary>
    /// <returns>
    /// A task completing after all original tracked bytes, resource balances and wound history are restored without a turn or story advance.
    /// </returns>
    [Fact]
    public async Task AcceptedTurnSpiritualContinuation_EscapeRestoresOriginalWithoutPublishing()
    {
        await RunSpiritualWaitingInterruptionAsync(replaceSession: false);
    }

    /// <summary>
    /// Replaces the session during a real spiritual decision wait and prevents the old caller from touching the new session.
    /// </summary>
    /// <returns>
    /// A task completing after generation replacement raises its actual exception and every replacement file remains exact.
    /// </returns>
    [Fact]
    public async Task AcceptedTurnSpiritualContinuation_SessionReplacementPreservesNewSession()
    {
        await RunSpiritualWaitingInterruptionAsync(replaceSession: true);
    }

    /// <summary>
    /// Interrupts the first genuine spiritual request after proving the engine released its canonical lease for the GM wait.
    /// </summary>
    /// <param name="replaceSession">
    /// Whether to clear and replace the real session; otherwise the callback sends Escape to the original caller.
    /// </param>
    /// <returns>
    /// A task completing after actual caller rollback or generation isolation preserves the corresponding physical evidence.
    /// </returns>
    private async Task RunSpiritualWaitingInterruptionAsync(bool replaceSession)
    {
        const string repairPath = "game_state/control/validation_repair_request.json";
        const string manifestPath = "game_state/control/pending_turn_snapshot.json";
        const string authorityPath = "game_state/control/pending_turn_snapshot.authority.json";
        var input = new QueuedConsoleInputSource([]);
        var logger = new SpiritualLifecycleTestLogger();
        GameEngine? engine = null;
        var request = new TurnRequest
        {
            SessionId = "session_engine_spiritual", RequestId = "request_engine_spiritual_42", TurnNumber = 42,
            PlayerAction = "Удержать встречное духовное давление.", Timestamp = DateTime.UtcNow.ToString("O"),
            PreGeneratedDices1d20 = [15, 5, 12, 8]
        };
        await using var context = await AfterlifeResourceCutoverTests.CreateSpiritualGameEngineOriginalAsync(async original =>
        {
            engine = CreateGameEngine(input, fileSystem: original.FileSystem, logger: logger);
            await InvokePrivateTaskAsync(engine, "RefreshRuntimeStateAsync");
            GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(request.SessionId, 41);
            await InvokePrivateTaskAsync(engine, "EnsureClientOwnedSystemFilesHealthyAsync");
            request.ProgressionControl = await new ProgressionScheduleService(original.FileSystem,
                NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea");
            var rollback = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "spiritual-interruption-original");
            await original.WriteExactJsonAsync("input/turn_request.json", JsonSerializer.Serialize(request, SnapshotHashJsonOpts));
            await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync",
                request, rollback, "spiritual-interruption-original");
        });
        var originalRequest = Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync("input/turn_request.json"));
        var originalManifest = Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(manifestPath));
        var originalAuthority = Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(authorityPath));
        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(manifestPath));
        var baseline = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in manifest["rollbackBaselineFiles"]!.AsArray().Select(value => value!.GetValue<string>()))
        {
            var backupPath = manifest["rollbackBackups"]![path]!.GetValue<string>();
            baseline.Add(path, Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(backupPath)));
        }
        Assert.NotEmpty(baseline);
        var originalStory = await context.FileSystem.ReadFileBytesAsync("stories/chaos_sea.jsonl");
        await AfterlifeResourceCutoverTests.WriteSpiritualGameEngineExchangeAsync(context);
        await WriteSpiritualLifecycleOutputsAsync(context, request);

        Exception? callbackFailure = null;
        string? lastRepair = null;
        string? oldGeneration = null;
        string? newGeneration = null;
        SpiritualWoundContinuationRequest? issued = null;
        byte[]? issuedCheckpoint = null;
        Dictionary<string, byte[]>? replacementFiles = null;
        var leaseReleased = false;
        using var stop = new CancellationTokenSource();
        // Start outside the engine's bound session context so the replacement uses a genuine new generation.
        var callback = Task.Run(async () =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    var physical = context.FileSystem.ResolvePath(repairPath);
                    string? raw = null;
                    try
                    {
                        if (File.Exists(physical))
                        {
                            using var stream = new FileStream(physical, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                            using var reader = new StreamReader(stream);
                            raw = await reader.ReadToEndAsync(stop.Token);
                        }
                    }
                    catch (FileNotFoundException) { }
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        await Task.Delay(25, stop.Token);
                        continue;
                    }
                    lastRepair = raw;
                    using var document = JsonDocument.Parse(raw);
                    var report = document.RootElement;
                    Assert.True(report.TryGetProperty("spiritualWoundContinuation", out var envelope), raw);
                    issued = SpiritualWoundContinuationProtocol.ReadRequest(envelope);
                    Assert.Equal("decision", issued.Phase);
                    Assert.NotNull(issued.Offer);
                    Assert.Empty(issued.DependentDraftFields);
                    Assert.Empty(SpiritualWoundContinuationProtocol.ValidateRequest(issued));
                    Assert.Equal(request.SessionId, report.GetProperty("sessionId").GetString());
                    Assert.Equal(request.RequestId, report.GetProperty("requestId").GetString());
                    Assert.Equal(42, report.GetProperty("turnNumber").GetInt32());
                    Assert.Equal(0, report.GetProperty("errors").GetArrayLength());
                    Assert.False(report.GetProperty("fullTurnResubmissionRequired").GetBoolean());
                    using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
                    {
                        deadline.CancelAfter(TimeSpan.FromSeconds(10));
                        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync(cancellationToken: deadline.Token);
                        leaseReleased = true;
                        oldGeneration = context.FileSystem.GetOrCreateSessionGeneration(lease);
                        Assert.Equal(originalRequest, await context.FileSystem.ReadFileBytesAsync(lease, "input/turn_request.json"));
                        Assert.Equal(originalManifest, await context.FileSystem.ReadFileBytesAsync(lease, manifestPath));
                        Assert.Equal(originalAuthority, await context.FileSystem.ReadFileBytesAsync(lease, authorityPath));
                        issuedCheckpoint = await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath);
                        Assert.NotNull(issuedCheckpoint);
                        var checkpoint = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease,
                            SpiritualWoundCaptureCheckpointState.StatePath))!)!["checkpoint"]!;
                        Assert.Equal(request.SessionId, checkpoint["sessionId"]!.GetValue<string>());
                        Assert.Equal(request.RequestId, checkpoint["requestId"]!.GetValue<string>());
                        Assert.Equal(42, checkpoint["turn"]!.GetValue<int>());
                        Assert.Equal(0, checkpoint["committedAdvance"]!.GetValue<int>());
                        Assert.Null(checkpoint["pendingSubmission"]);
                        Assert.Equal(manifest["manifestPayloadHash"]!.GetValue<string>(), checkpoint["snapshotToken"]!.GetValue<string>());
                        Assert.True(context.FileSystem.FileExists(lease, SpiritualWoundDecisionPendingState.StatePath));
                        Assert.False(context.FileSystem.FileExists(lease, AcceptedMechanicsPlan.WoundCommandPath));
                        Assert.False(context.FileSystem.FileExists(lease, SpiritualWoundOpportunityReceiptState.StatePath));
                    }
                    if (replaceSession)
                    {
                        await context.FileSystem.ClearGameStateAsync();
                        replacementFiles = await ReadSpiritualEntryGuardFilesAsync(context);
                        var replacementRequest = JsonSerializer.Serialize(new
                        {
                            sessionId = "replacement-spiritual-session", requestId = "replacement-spiritual-request", turnNumber = 1
                        });
                        var replacementReady = JsonSerializer.Serialize(new
                        {
                            sessionId = "replacement-spiritual-session", requestId = "replacement-spiritual-request", turnNumber = 1,
                            timestamp = DateTime.UtcNow.ToString("O"), status = "success"
                        });
                        var replacementSeeds = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["input/turn_request.json"] = replacementRequest,
                            ["ready/turn_complete.json"] = replacementReady,
                            ["game_state/meta/spiritual_replacement_sentinel.json"] = "{\"replacement\":\"preserve exact bytes\"}"
                        };
                        foreach (var seed in replacementSeeds)
                        {
                            replacementFiles[seed.Key.Replace('/', Path.DirectorySeparatorChar)] =
                                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(seed.Value);
                            await context.WriteExactJsonAsync(seed.Key, seed.Value);
                        }
                        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
                            newGeneration = context.FileSystem.GetOrCreateSessionGeneration(lease);
                    }
                    else
                    {
                        input.Enqueue(Key(ConsoleKey.Escape));
                    }
                    return;
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            catch (Exception error)
            {
                callbackFailure = error;
                input.Enqueue(Key(ConsoleKey.Escape));
            }
        });
        var operation = InvokePrivateTaskResultAsync(engine!, "WaitForGmResponse");
        object? result = null;
        Exception? failure = null;
        var cleanup = new List<Exception>();
        try { result = await operation.WaitAsync(TimeSpan.FromSeconds(240)); }
        catch (Exception error) { failure = error; }
        finally
        {
            // Replacement may have stopped the old wait before its callback has finished seeding the new files.
            try { await callback.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (Exception error) { cleanup.Add(error); }
            stop.Cancel();
            if (!callback.IsCompleted)
            {
                try { await callback.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception error) { cleanup.Add(error); }
            }
            if (!operation.IsCompleted)
            {
                input.Enqueue(Key(ConsoleKey.Escape));
                try { await operation.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception error) { cleanup.Add(error); }
            }
        }
        Assert.True(callbackFailure is null && cleanup.Count == 0,
            $"Operation: {failure}; callback: {callbackFailure}; cleanup: {string.Join("\n", cleanup)}; " +
            $"result: {result}; lease acquired during wait: {leaseReleased}; last repair: {lastRepair}. {logger.Describe()}");
        Assert.True(leaseReleased, logger.Describe());
        Assert.NotNull(issued);
        Assert.NotNull(issuedCheckpoint);
        Assert.Equal(41, GetPrivateField<GameLoop>(engine!, "_gameLoop").TurnNumber);
        if (replaceSession)
        {
            Assert.True(failure is SessionReplacedException,
                $"Expected SessionReplacedException; result={result}; error={failure}. {logger.Describe()}");
            Assert.False(string.IsNullOrWhiteSpace(oldGeneration));
            Assert.False(string.IsNullOrWhiteSpace(newGeneration));
            Assert.NotEqual(oldGeneration, newGeneration);
            Assert.NotNull(replacementFiles);
            Assert.Contains(Path.Combine("input", "turn_request.json"), replacementFiles.Keys);
            Assert.Contains(Path.Combine("ready", "turn_complete.json"), replacementFiles.Keys);
            Assert.Contains(Path.Combine("game_state", "meta", "spiritual_replacement_sentinel.json"), replacementFiles.Keys);
            await AssertSpiritualEntryGuardFilesAsync(context, replacementFiles);
        }
        else
        {
            Assert.True(failure is null, $"Escape rollback failed: {failure}. {logger.Describe()}");
            Assert.False(Assert.IsType<bool>(result));
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                var method = ResolvePrivateMethod(engine!, "EnumerateRollbackTrackedFiles", [lease]);
                var actualPaths = Assert.IsAssignableFrom<IEnumerable<string>>(
                    method.Invoke(engine, BuildPrivateInvocationArguments(method, [lease]))).ToArray();
                Assert.Equal(baseline.Keys.Order(StringComparer.Ordinal), actualPaths.Order(StringComparer.Ordinal));
                foreach (var pair in baseline)
                    Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
            }
            Assert.Equal(originalStory, await context.FileSystem.ReadFileBytesAsync("stories/chaos_sea.jsonl"));
            foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json", "ready/turn_error.json",
                         manifestPath, authorityPath, repairPath, "game_state/control/validation_repair_ready.json",
                         SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
                         AcceptedMechanicsPlan.WoundCommandPath, SpiritualWoundOpportunityReceiptState.StatePath })
                Assert.False(context.FileSystem.FileExists(path), path);
            var definitions = ResourceDefinitionCatalog.ParseCanonical(
                await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), allowMissingPristine: false);
            Assert.True(definitions.IsValid, string.Join("\n", definitions.Issues));
            var state = ResourceStateContract.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
                definitions.Catalog!, allowMissingPristine: false);
            Assert.True(state.IsValid, string.Join("\n", state.Issues));
            var balances = state.Ledger!.Entries.Where(entry => entry.Coordinate.ResourceKey == "spiritual_action_points").ToArray();
            Assert.Equal(2, balances.Length);
            Assert.All(balances, entry => Assert.Equal(6m, entry.Current));
            var wounds = WoundIdentityState.Parse(await context.FileSystem.ReadFileAsync(WoundIdentityState.StatePath), WoundIdentityState.StatePath);
            Assert.True(wounds.IsValid, string.Join("\n", wounds.Issues));
            Assert.Empty(wounds.State!.Entries);
        }
    }
}
