using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Routes a signed spiritual exchange through the accepted-turn loop, preserving ordinary costs and one publication.
    /// </summary>
    /// <param name="training">
    /// Whether the source has a zero wound ceiling and must complete without a GM continuation request.
    /// </param>
    /// <returns>
    /// A task completing after the real loop publishes the signed exchange and its appropriate spiritual receipt.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptedTurnSpiritualContinuation_UsesOriginalSignedTurnAndSinglePublication(bool training)
    {
        await RunAcceptedSpiritualContinuationAsync(training, worker: false, materialize: false);
    }

    /// <summary>
    /// Recovers the committed decision when its derived pending replacement fails, without asking the GM twice or rolling back the turn.
    /// </summary>
    /// <returns>
    /// A task completing after one injected pending-write failure still yields one accepted canonical publication.
    /// </returns>
    [Fact]
    public async Task AcceptedTurnSpiritualContinuation_PendingWriteFailureRecoversCommittedDecisionOnce()
    {
        await RunAcceptedSpiritualContinuationAsync(training: false, worker: false, materialize: false,
            failPendingWriteAfterAdvance: true);
    }

    /// <summary>
    /// Preserves an accepted choice and its terminal signal during repeated pending failures, then completes the actual cold lifecycle.
    /// </summary>
    /// <returns>
    /// A task completing after gameplay stops, startup preserves the held repair, and late-terminal handling publishes once without another GM response.
    /// </returns>
    [Fact]
    public async Task AcceptedTurnSpiritualContinuation_PersistentPendingFailurePreservesChoiceForColdResume()
    {
        await RunAcceptedSpiritualContinuationAsync(training: false, worker: false, materialize: false,
            failPendingWriteAfterAdvance: true, persistentPendingFailure: true);
    }

    /// <summary>
    /// Runs the shared real original-turn fixture through its selected GM transport without creating private decision authority.
    /// </summary>
    /// <param name="training">
    /// Whether the signed source has zero wound ceiling and requires no GM response.
    /// </param>
    /// <param name="worker">
    /// Whether the actual configured worker bridge supplies the response; otherwise the file callback does so.
    /// </param>
    /// <param name="materialize">
    /// Whether to materialize the offered wound with exact acquisition narration.
    /// </param>
    /// <param name="failPendingWriteAfterAdvance">
    /// Whether to fail one derived pending write after the physical checkpoint confirms the first committed advance.
    /// </param>
    /// <param name="persistentPendingFailure">
    /// Whether the failure remains active until gameplay stops with the original terminal signal preserved and a cold lifecycle resumes the saved choice.
    /// </param>
    /// <returns>
    /// A task completing after the original turn is accepted and the resulting publication is verified.
    /// </returns>
    private async Task RunAcceptedSpiritualContinuationAsync(bool training, bool worker, bool materialize,
        bool failPendingWriteAfterAdvance = false, bool persistentPendingFailure = false)
    {
        const string repairPath = "game_state/control/validation_repair_request.json";
        const string readyPath = "game_state/control/validation_repair_ready.json";
        const string manifestPath = "game_state/control/pending_turn_snapshot.json";
        const string authorityPath = "game_state/control/pending_turn_snapshot.authority.json";
        var input = new QueuedConsoleInputSource([]);
        var engineLog = new SpiritualLifecycleTestLogger();
        GameEngine? engine = null;
        object? rollback = null;
        ResourceMaterializationTestContext? faultContext = null;
        byte[]? faultCheckpoint = null;
        byte[]? faultCommand = null;
        var pendingWriteFailures = 0;
        var pendingWriteFaultEnabled = 1;
        var hooks = failPendingWriteAfterAdvance ? new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path =>
            {
                if (faultContext is null || path != SpiritualWoundDecisionPendingState.StatePath ||
                    Volatile.Read(ref pendingWriteFaultEnabled) == 0 ||
                    (!persistentPendingFailure && Volatile.Read(ref pendingWriteFailures) != 0))
                    return Task.CompletedTask;
                var checkpointPath = faultContext.FileSystem.ResolvePath(SpiritualWoundCaptureCheckpointState.StatePath);
                if (!File.Exists(checkpointPath))
                    return Task.CompletedTask;
                var checkpoint = JsonNode.Parse(File.ReadAllText(checkpointPath))?["checkpoint"];
                if (checkpoint?["committedAdvance"]?.GetValue<int>() != 1)
                    return Task.CompletedTask;
                Interlocked.Increment(ref pendingWriteFailures);
                faultCheckpoint ??= File.ReadAllBytes(checkpointPath);
                faultCommand ??= File.ReadAllBytes(faultContext.FileSystem.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath));
                return Task.FromException(new IOException("Injected pending projection failure after committed spiritual advance."));
            }
        } : null;
        var request = new TurnRequest
        {
            SessionId = "session_engine_spiritual",
            RequestId = "request_engine_spiritual_42",
            TurnNumber = 42,
            PlayerAction = "Удержать встречное духовное давление.",
            Timestamp = DateTime.UtcNow.ToString("O"),
            PreGeneratedDices1d20 = [15, 5, 12, 8]
        };
        await using var context = await AfterlifeResourceCutoverTests.CreateSpiritualGameEngineOriginalAsync(
            async original =>
            {
                engine = CreateGameEngine(input, configureSettings: settings =>
                {
                    if (worker)
                        settings.GmWorkerBridgeProfiles.Add(CreateSpiritualLifecycleWorkerProfile(original, materialize));
                }, fileSystem: original.FileSystem, logger: engineLog);
                await InvokePrivateTaskAsync(engine, "RefreshRuntimeStateAsync");
                // Initialize the real client-owned manifest before signing and capturing physical images.
                await InvokePrivateTaskAsync(engine, "EnsureClientOwnedSystemFilesHealthyAsync");
                request.ProgressionControl = await new ProgressionScheduleService(original.FileSystem,
                    NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea");
                rollback = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "engine-spiritual-original");
                await original.WriteExactJsonAsync("input/turn_request.json", JsonSerializer.Serialize(request, SnapshotHashJsonOpts));
                await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync",
                    request, rollback, "engine-spiritual-original");
            }, training, hooks);
        faultContext = context;
        Assert.NotNull(engine);
        Assert.NotNull(rollback);
        await AfterlifeResourceCutoverTests.WriteSpiritualGameEngineExchangeAsync(context);
        await WriteSpiritualLifecycleOutputsAsync(context, request);
        var originalTerminalSignal = await context.FileSystem.ReadFileBytesAsync("ready/turn_complete.json");
        Assert.NotNull(originalTerminalSignal);
        var resolution = await InvokePrivateTaskResultAsync(engine!, "ResolveActivePendingTurnSnapshotContextAsync");
        Assert.Equal("Usable", resolution.GetType().GetProperty("Status")!.GetValue(resolution)!.ToString());
        var snapshotContext = resolution.GetType().GetProperty("Context")!.GetValue(resolution);
        Assert.NotNull(snapshotContext);
        var originalRequest = await context.FileSystem.ReadFileBytesAsync("input/turn_request.json");
        var originalManifest = await context.FileSystem.ReadFileBytesAsync(manifestPath);
        var originalAuthority = await context.FileSystem.ReadFileBytesAsync(authorityPath);
        var originalResources = await context.FileSystem.ReadFileBytesAsync(ResourceMaterializationContract.StatePath);
        var originalDraft = await context.FileSystem.ReadFileBytesAsync(AfterlifeSpiritualConflictState.StatePath);
        var heldCanonicalImages = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        if (persistentPendingFailure)
            foreach (var path in Directory.EnumerateFiles(context.FileSystem.ResolvePath("game_state"), "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(context.FileSystem.GameSessionPath, path).Replace('\\', '/');
                if (!relative.StartsWith("game_state/control/", StringComparison.Ordinal))
                    heldCanonicalImages.Add(relative, await File.ReadAllBytesAsync(path));
            }
        Assert.False(context.FileSystem.FileExists(SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.False(context.FileSystem.FileExists(SpiritualWoundDecisionPendingState.StatePath));
        var manifest = JsonNode.Parse((await context.FileSystem.ReadFileAsync(manifestPath))!)!;
        Assert.Equal(request.SessionId, manifest["sessionId"]!.GetValue<string>());
        Assert.Equal(request.RequestId, manifest["requestId"]!.GetValue<string>());
        Assert.Equal(42, manifest["turnNumber"]!.GetValue<int>());
        Assert.Equal(new[] { 15, 5, 12, 8 }, manifest["preGeneratedDices1d20"]!.AsArray().Select(value => value!.GetValue<int>()));
        Assert.Equal("Chaos Sea", manifest["progressionControl"]!["currentRealm"]!.GetValue<string>());

        var responseCount = 0;
        Exception? callbackFailure = null;
        string? lastRepair = null;
        using var stop = new CancellationTokenSource();
        var physicalRepairPath = context.FileSystem.ResolvePath(repairPath);
        var callback = Task.Run(async () =>
        {
            string? handled = null;
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    string? raw = null;
                    if (File.Exists(physicalRepairPath))
                    {
                        try
                        {
                            using var repairStream = new FileStream(physicalRepairPath, FileMode.Open,
                                FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                            using var repairReader = new StreamReader(repairStream);
                            raw = await repairReader.ReadToEndAsync(stop.Token);
                        }
                        catch (FileNotFoundException) { }
                    }
                    if (!string.IsNullOrWhiteSpace(raw) && raw != handled)
                    {
                        handled = raw;
                        lastRepair = raw;
                        using var repairDocument = JsonDocument.Parse(raw);
                        var repair = repairDocument.RootElement;
                        Assert.False(training, "A training exchange must not request a wound decision or ordinary repair: " + raw);
                        Assert.Equal(0, responseCount);
                        Assert.True(repair.TryGetProperty("spiritualWoundContinuation", out var envelope),
                            "Expected a genuine continuation instead of ordinary validation repair: " + raw);
                        var continuation = SpiritualWoundContinuationProtocol.ReadRequest(envelope);
                        Assert.Equal("decision", continuation.Phase);
                        Assert.Empty(continuation.DependentDraftFields);
                        Assert.Equal(0, repair.GetProperty("errors").GetArrayLength());
                        Assert.False(repair.GetProperty("fullTurnResubmissionRequired").GetBoolean());
                        Assert.Equal(request.SessionId, repair.GetProperty("sessionId").GetString());
                        Assert.Equal(request.RequestId, repair.GetProperty("requestId").GetString());
                        Assert.Equal(42, repair.GetProperty("turnNumber").GetInt32());
                        if (worker)
                            continue;
                        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
                        {
                            var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
                            var current = await fresh.ReadSpiritualWoundContinuationAsync(lease);
                            Assert.Equal("decision", current.Disposition);
                            Assert.Empty(current.Issues);
                            Assert.Equal(JsonSerializer.Serialize(current.Request), JsonSerializer.Serialize(continuation));
                            Assert.True(context.FileSystem.FileExists(lease, SpiritualWoundCaptureCheckpointState.StatePath));
                            Assert.True(context.FileSystem.FileExists(lease, SpiritualWoundDecisionPendingState.StatePath));
                            Assert.Equal(originalRequest, await context.FileSystem.ReadFileBytesAsync(lease, "input/turn_request.json"));
                            Assert.Equal(originalManifest, await context.FileSystem.ReadFileBytesAsync(lease, manifestPath));
                            Assert.Equal(originalAuthority, await context.FileSystem.ReadFileBytesAsync(lease, authorityPath));
                            Assert.Equal(originalResources, await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath));
                            Assert.Equal(originalDraft, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
                            Assert.False(context.FileSystem.FileExists(lease, AcceptedMechanicsPlan.WoundCommandPath));
                        }
                        if (materialize)
                            await context.WriteExactJsonAsync("output/narrative_response.json", JsonSerializer.Serialize(new
                            {
                                response = "Чужое давление надломило волю хранителя.",
                                timestamp = DateTime.UtcNow.ToString("O")
                            }));
                        var decision = materialize
                            ? AfterlifeResourceCutoverTests.CreateSpiritualLifecycleMaterializeDecision(continuation.Offer!.OpportunityRef)
                            : JsonSerializer.SerializeToElement(new { opportunityRef = continuation.Offer!.OpportunityRef, decision = "none" });
                        await context.WriteExactJsonAsync(readyPath, JsonSerializer.Serialize(new
                        {
                            sessionId = request.SessionId,
                            requestId = request.RequestId,
                            turnNumber = 42,
                            timestamp = DateTime.UtcNow.ToString("O"),
                            status = "success",
                            spiritualWoundContinuation = new
                            {
                                schemaVersion = 1,
                                continuationId = continuation.ContinuationId,
                                woundDecisions = new[] { decision }
                            }
                        }));
                        responseCount++;
                    }
                    await Task.Delay(25, stop.Token);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            catch (Exception error)
            {
                callbackFailure = error;
                input.Enqueue(Key(ConsoleKey.Escape));
            }
        });
        var validation = InvokePrivateTaskResultAsync(engine!, "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "ответа GM", snapshotContext, rollback, 42, request.ProgressionControl);
        object? disposition = null;
        Exception? validationFailure = null;
        var cleanupFailures = new List<Exception>();
        try
        {
            disposition = await validation.WaitAsync(TimeSpan.FromSeconds(240));
        }
        catch (Exception error)
        {
            validationFailure = error;
        }
        finally
        {
            stop.Cancel();
            input.Enqueue(Key(ConsoleKey.Escape));
            try { await callback.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception error) { cleanupFailures.Add(error); }
            if (!validation.IsCompleted)
            {
                try { await validation.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception error) { cleanupFailures.Add(error); }
            }
        }
        Assert.True(validationFailure is null && cleanupFailures.Count == 0,
            $"Validation failure: {validationFailure}; cleanup failures: {string.Join("\n", cleanupFailures)}; " +
            $"disposition: {disposition}; pending faults: {pendingWriteFailures}; GM responses: {responseCount}; " +
            $"callback failure: {callbackFailure}; last repair: {lastRepair ?? "<none>"}. {engineLog.Describe()}");
        Assert.True(callbackFailure is null, callbackFailure + "\nLast repair: " + lastRepair + "\n" + engineLog.Describe());
        if (failPendingWriteAfterAdvance)
        {
            Assert.True(pendingWriteFailures > 0,
                $"Pending fault was not reached. Disposition: {disposition}; GM responses: {responseCount}; " +
                $"last repair: {lastRepair ?? "<none>"}. {engineLog.Describe()}");
            if (!persistentPendingFailure)
                Assert.Equal(1, pendingWriteFailures);
            Assert.NotNull(faultCheckpoint);
            using var stream = new MemoryStream(faultCheckpoint);
            using var reader = new StreamReader(stream);
            var committed = JsonNode.Parse(await reader.ReadToEndAsync())!["checkpoint"]!;
            Assert.Equal(request.SessionId, committed["sessionId"]!.GetValue<string>());
            Assert.Equal(request.RequestId, committed["requestId"]!.GetValue<string>());
            Assert.Equal(request.TurnNumber, committed["turn"]!.GetValue<int>());
            Assert.Equal(manifest["manifestPayloadHash"]!.GetValue<string>(), committed["snapshotToken"]!.GetValue<string>());
            Assert.Equal(1, committed["committedAdvance"]!.GetValue<int>());
        }
        if (persistentPendingFailure)
        {
            Assert.True(disposition!.ToString() == "SpiritualContinuationHeldBlocked",
                $"Expected retryable spiritual hold, received {disposition}. {engineLog.Describe()}");
            Assert.True(pendingWriteFailures >= 2, "The owner must attempt actual pending repair before returning a retryable hold.");
            Assert.Equal(1, responseCount);
            Assert.Equal(faultCheckpoint, await context.FileSystem.ReadFileBytesAsync(SpiritualWoundCaptureCheckpointState.StatePath));
            Assert.Equal(faultCommand, await context.FileSystem.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
            var heldPending = await context.FileSystem.ReadFileBytesAsync(SpiritualWoundDecisionPendingState.StatePath);
            Assert.NotNull(heldPending);
            var heldRepairRequest = await context.FileSystem.ReadFileBytesAsync(repairPath);
            var heldRepairReady = await context.FileSystem.ReadFileBytesAsync(readyPath);
            Assert.NotNull(heldRepairRequest);
            Assert.NotNull(heldRepairReady);
            SetPrivateField(engine!, "_inGame", true);
            Assert.True(InvokePrivateValue<bool>(engine!, "PreserveAcceptedTurnForTreatmentPublicationRetry", disposition));
            Assert.Equal(originalTerminalSignal, await context.FileSystem.ReadFileBytesAsync("ready/turn_complete.json"));
            Assert.False(GetPrivateFieldValue<bool>(engine!, "_inGame"));
            Assert.Equal(faultCheckpoint, await context.FileSystem.ReadFileBytesAsync(SpiritualWoundCaptureCheckpointState.StatePath));
            Assert.Equal(faultCommand, await context.FileSystem.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
            Assert.Equal(heldPending, await context.FileSystem.ReadFileBytesAsync(SpiritualWoundDecisionPendingState.StatePath));
            Assert.Equal(originalRequest, await context.FileSystem.ReadFileBytesAsync("input/turn_request.json"));
            Assert.Equal(originalManifest, await context.FileSystem.ReadFileBytesAsync(manifestPath));
            Assert.Equal(originalAuthority, await context.FileSystem.ReadFileBytesAsync(authorityPath));
            var heldPaths = Directory.EnumerateFiles(context.FileSystem.ResolvePath("game_state"), "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(context.FileSystem.GameSessionPath, path).Replace('\\', '/'))
                .Where(path => !path.StartsWith("game_state/control/", StringComparison.Ordinal)).Order(StringComparer.Ordinal);
            var expectedHeldImages = new Dictionary<string, byte[]>(heldCanonicalImages, StringComparer.Ordinal)
            {
                // This private command is intentionally retained with the saved choice outside control/.
                [AcceptedMechanicsPlan.WoundCommandPath] = Assert.IsType<byte[]>(faultCommand)
            };
            Assert.Equal(expectedHeldImages.Keys.Order(StringComparer.Ordinal), heldPaths);
            foreach (var pair in expectedHeldImages)
            {
                var heldBytes = await context.FileSystem.ReadFileBytesAsync(pair.Key);
                Assert.True(heldBytes is not null && pair.Value.AsSpan().SequenceEqual(heldBytes),
                    $"Held canonical image changed: {pair.Key}; original length {pair.Value.Length}, held length {heldBytes?.Length}.");
            }
            Assert.False(context.FileSystem.FileExists(SpiritualWoundOpportunityReceiptState.StatePath));

            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                var ordinaryItems = MortalItemAcceptedTurnAuthority.HasValidatedItems(context.FileSystem, lease);
                var commonPlan = AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease);
                var treatmentVacant = AcceptedTurnAuthorityRegistry.CanBeginSpiritualOriginalIntake(
                    context.FileSystem, lease, expectedFactory: null, checkPlansAndItems: false);
                var allOwnersVacant = AcceptedTurnAuthorityRegistry.CanBeginSpiritualOriginalIntake(
                    context.FileSystem, lease, new MortalItemIdentityFactory(), checkPlansAndItems: true);
                Assert.True(allOwnersVacant,
                    $"Warm hold retained item claim: ordinaryItems={ordinaryItems}; commonPlan={commonPlan}; " +
                    $"treatmentVacant={treatmentVacant}; allOwnersVacant={allOwnersVacant}.");
            }

            Interlocked.Exchange(ref pendingWriteFaultEnabled, 0);
            var coldInput = new QueuedConsoleInputSource([]);
            var coldFileSystem = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance);
            Assert.Equal(context.FileSystem.GameSessionPath, coldFileSystem.GameSessionPath);
            var coldEngine = CreateGameEngine(coldInput, fileSystem: coldFileSystem, logger: engineLog);
            var coldGameLoop = GetPrivateField<GameLoop>(coldEngine, "_gameLoop");
            coldGameLoop.SetSession(request.SessionId, 41);
            await InvokePrivateTaskAsync(coldEngine, "RefreshRuntimeStateAsync");
            await InvokePrivateTaskAsync(coldEngine, "NormalizePendingRepairArtifactsAsync");
            Assert.Equal(originalTerminalSignal, await context.FileSystem.ReadFileBytesAsync("ready/turn_complete.json"));
            Assert.Equal(heldRepairRequest, await context.FileSystem.ReadFileBytesAsync(repairPath));
            Assert.Equal(heldRepairReady, await context.FileSystem.ReadFileBytesAsync(readyPath));
            var coldLifecycle = InvokePrivateTaskResultAsync(coldEngine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
            Exception? coldFailure = null;
            Exception? coldCleanupFailure = null;
            object? coldResult = null;
            try
            {
                coldResult = await coldLifecycle.WaitAsync(TimeSpan.FromSeconds(240));
            }
            catch (Exception error)
            {
                coldFailure = error;
            }
            finally
            {
                if (!coldLifecycle.IsCompleted)
                {
                    coldInput.Enqueue(Key(ConsoleKey.Escape));
                    try { await coldLifecycle.WaitAsync(TimeSpan.FromSeconds(5)); }
                    catch (Exception error) { coldCleanupFailure = error; }
                }
            }
            Assert.True(coldFailure is null && coldCleanupFailure is null,
                $"Cold lifecycle failure: {coldFailure}; cleanup failure: {coldCleanupFailure}; " +
                $"held disposition: {disposition}; pending faults: {pendingWriteFailures}; GM responses: {responseCount}; " +
                $"last repair: {lastRepair ?? "<none>"}. {engineLog.Describe()}");
            // A normal accepted late turn returns false so the caller can continue to player input.
            Assert.False(Assert.IsType<bool>(coldResult), engineLog.Describe());
            Assert.Equal(42, coldGameLoop.TurnNumber);
            Assert.Contains("встречное давление", GetPrivateField<GameResponse>(coldEngine, "_lastResponse").Response, StringComparison.Ordinal);
            foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json", "ready/turn_error.json", manifestPath, authorityPath })
                Assert.False(context.FileSystem.FileExists(path), path);
        }
        else
        {
            var actualDisposition = Assert.IsType<AcceptedTurnValidationDisposition>(disposition);
            Assert.True(actualDisposition == AcceptedTurnValidationDisposition.Accepted,
                $"Expected Accepted, received {actualDisposition}. GM responses: {responseCount}. {engineLog.Describe()}");
            Assert.Equal(originalRequest, await context.FileSystem.ReadFileBytesAsync("input/turn_request.json"));
            Assert.Equal(originalManifest, await context.FileSystem.ReadFileBytesAsync(manifestPath));
            Assert.Equal(originalAuthority, await context.FileSystem.ReadFileBytesAsync(authorityPath));
        }
        Assert.Equal(training || worker ? 0 : 1, responseCount);
        if (worker)
            await AssertSpiritualLifecycleWorkerCompletedAsync(context, request, materialize);
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath, repairPath, readyPath })
            Assert.False(context.FileSystem.FileExists(path), path);

        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), allowMissingPristine: false);
        Assert.True(definitions.IsValid, string.Join(Environment.NewLine, definitions.Issues));
        var state = ResourceStateContract.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions.Catalog!, allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        var balances = state.Ledger!.Entries.Where(entry => entry.Coordinate.ResourceKey == "spiritual_action_points").ToArray();
        Assert.Equal(2, balances.Length);
        Assert.All(balances, entry => Assert.Equal(3m, entry.Current));
        var history = ResourceHistoryState.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            definitions.Catalog!, allowMissingPristine: false);
        Assert.True(history.IsValid, string.Join(Environment.NewLine, history.Issues));
        var spends = history.History!.Transitions.Where(transition => transition.Turn == 42 &&
            transition.OriginId == "exchange_conflict_frame_42" && transition.Operation == ResourceTransitionOperation.Spend).ToArray();
        Assert.Equal(2, spends.Length);
        Assert.All(spends, transition =>
        {
            Assert.Equal(3m, transition.AppliedAmount);
            Assert.Equal(6m, transition.BeforeState!.Current);
            Assert.Equal(3m, transition.AfterState!.Current);
        });
        var receipt = Assert.IsType<JsonObject>(await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath));
        Assert.Single(receipt["instances"]!.AsArray());
        if (training)
        {
            Assert.Empty(receipt["sources"]!.AsArray());
            Assert.Empty(receipt["decisions"]!.AsArray());
        }
        else
        {
            Assert.Single(receipt["sources"]!.AsArray());
            var decisionReceipt = Assert.Single(receipt["decisions"]!.AsArray())!;
            Assert.Equal(materialize ? "materialize" : "none", decisionReceipt["decision"]!.GetValue<string>());
            if (materialize)
                await AssertSpiritualLifecycleMaterializedWoundAsync(context, decisionReceipt);
        }
        var conflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        Assert.False(conflict.ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
        var exchange = Assert.Single(conflict["activeConflict"]!["exchangeLog"]!.AsArray())!;
        Assert.Equal("exchange_conflict_frame_42", exchange["exchangeId"]!.GetValue<string>());
        Assert.Equal(new[] { 15, 5 }, exchange["diceAudit"]!["diceUsed"]!.AsArray().Select(die => die!["value"]!.GetValue<int>()));
        Assert.Equal("pressure", exchange["matchupAudit"]!["playerOperation"]!.GetValue<string>());
        Assert.Equal("pressure", exchange["matchupAudit"]!["oppositionOperation"]!.GetValue<string>());
    }

    /// <summary>
    /// Retains bounded information and warning diagnostics from the actual engine so fixture failures reveal their production phase.
    /// </summary>
    private sealed class SpiritualLifecycleTestLogger : ILogger<GameEngine>
    {
        private readonly ConcurrentQueue<string> _messages = new();

        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;
            var message = formatter(state, exception) + (exception is null ? string.Empty : "\n" + exception);
            _messages.Enqueue(message.Length <= 2000 ? message : message[..2000]);
            while (_messages.Count > 24)
                _messages.TryDequeue(out _);
        }

        /// <summary>
        /// Formats retained phase and failure messages without reading or dumping session files.
        /// </summary>
        /// <returns>
        /// At most twenty-four messages, each bounded to two thousand characters.
        /// </returns>
        internal string Describe() => "Engine diagnostics:\n" + string.Join("\n", _messages);
    }

    /// <summary>
    /// Supplies ordinary player-facing files and the original accepted-turn signal without encoding wound decisions in narrative.
    /// </summary>
    /// <param name="context">
    /// Signed spiritual fixture receiving the original GM outputs.
    /// </param>
    /// <param name="request">
    /// Exact original request whose metadata correlates the initial turn-complete signal.
    /// </param>
    /// <returns>
    /// A task completing after the three ordinary outputs and original Ready signal are written.
    /// </returns>
    private static async Task WriteSpiritualLifecycleOutputsAsync(ResourceMaterializationTestContext context, TurnRequest request)
    {
        var timestamp = DateTime.UtcNow.ToString("O");
        await context.WriteExactJsonAsync("output/narrative_response.json", JsonSerializer.Serialize(new
        {
            response = "Душа удерживает встречное давление, и воля хранителя на миг колеблется.", timestamp
        }));
        await context.WriteExactJsonAsync("output/interface_updates.json", JsonSerializer.Serialize(new
        {
            dialogueOptions = new[] { new { text = "Продолжить духовный обмен.", category = "neutral" } }, timestamp
        }));
        await context.WriteExactJsonAsync("output/debug_logs.json", JsonSerializer.Serialize(new
        {
            timestamp,
            gm_thoughts_markdown = string.Join("\n",
                "## NPC Scope",
                "- Mode: Scene-local",
                "- Relevant actors: guardian_frame",
                "- Why relevant: Хранитель участвует в духовном обмене; его запас духовных действий изменяется.",
                "- Actors outside scope: нет",
                "- Why outside scope: Другие самостоятельные акторы в этом обмене не участвуют.",
                "",
                "## Reasoning",
                "### guardian_frame",
                "- Current location: Море Хаоса; перемещения нет.",
                "- Situation: Хранитель противостоит давлению player_soul в текущем духовном обмене.",
                "- Profile inputs: guardian_frame — guardian из afterlife_entity_profiles; базовые искусства имеют ранг 0, особые искусства отсутствуют.",
                "- Thoughts: Я удержу встречное давление, сохраняя внимание на противнике.",
                "- Motivation: Сохранить собственную устойчивость в духовном противостоянии.",
                "- Constraints: Исходное действие pressure и плата 3 уже определены; новые силы и независимые действия не добавляются.",
                "- Strategy options:",
                "1. Продолжить встречное давление. Benefit: сохранить инициативу в обмене. Risk: усилить собственное напряжение.",
                "2. Перейти к защите. Benefit: сосредоточиться на устойчивости. Risk: уступить инициативу противнику.",
                "- Chosen strategy: Продолжить исходное pressure без изменения уже выбранного действия.",
                "- Rejected alternatives: Защита не выбрана; она изменила бы исходное действие этого обмена.",
                "- Actions: Выполняет pressure; исходные значения кубиков 15 и 5 и плата 3 сохранены.",
                $"- State changes: {AfterlifeEntityProfileState.StatePath} и {AfterlifeSpiritualConflictState.StatePath} отражают результат обмена; spiritual_action_points guardian_frame уменьшаются с 6 до 3 в {ResourceMaterializationContract.StatePath}. Личные цели, отношения и записи памяти не изменяются.")
        }));
        var control = Assert.IsType<ProgressionControl>(request.ProgressionControl);
        Assert.False(control.AfterlifeCatchupRequired, "This first owning fixture requires an ordinary bounded afterlife cycle, not a catch-up turn.");
        await context.WriteExactJsonAsync(ProgressionScheduleService.ReportPath, JsonSerializer.Serialize(new
        {
            progressionProcessingReport = new
            {
                sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
                worldCyclesProcessed = 0, factionCyclesProcessed = 0,
                chaosSeaCyclesProcessed = control.ChaosSeaCyclesExpectedThisTurn,
                guardianProjectCyclesProcessed = control.GuardianProjectCyclesExpectedThisTurn,
                residentAgencyCyclesProcessed = control.ResidentAgencyCyclesExpectedThisTurn,
                shiningAbodeCyclesProcessed = control.ShiningAbodeCyclesExpectedThisTurn,
                shiningFactionCyclesProcessed = control.ShiningFactionCyclesExpectedThisTurn,
                shiningTradeCyclesProcessed = control.ShiningTradeCyclesExpectedThisTurn,
                newLastChaosSeaSimulationOrdinal = control.NextChaosSeaTurnOrdinal,
                newLastGuardianProjectCycleOrdinal = control.NextGuardianProjectCycleOrdinal,
                newLastResidentAgencyCycleOrdinal = control.NextResidentAgencyCycleOrdinal,
                newLastShiningAbodeCycleOrdinal = control.NextShiningAbodeCycleOrdinal,
                newLastShiningFactionCycleOrdinal = control.NextShiningFactionCycleOrdinal,
                newLastShiningTradeCycleOrdinal = control.NextShiningTradeCycleOrdinal,
                afterlifeCatchupProcessed = false, afterlifeCatchupSummaryEventsProcessed = 0
            }
        }));
        await context.WriteExactJsonAsync("ready/turn_complete.json", JsonSerializer.Serialize(new
        {
            sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
            timestamp, status = "success", filesModified = new[]
            {
                AfterlifeSpiritualConflictState.StatePath, "output/narrative_response.json",
                "output/interface_updates.json", "output/debug_logs.json", ProgressionScheduleService.ReportPath
            }
        }));
    }
}
