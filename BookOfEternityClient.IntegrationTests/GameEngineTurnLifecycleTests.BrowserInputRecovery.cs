using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using System.Security.Cryptography;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(false, "recoverable")]
    [InlineData(false, "dual")]
    [InlineData(true, "synthetic")]
    [InlineData(true, "dual")]
    public async Task BrowserInput_OriginalTerminalProvenanceRefusesBeforeMutatingWarmOrLateResolver(bool late, string signalKind)
    {
        Dictionary<string, byte[]>? original = null;
        async Task WriteSignalsAsync(TurnRequest request)
        {
            var timestamp = DateTime.UtcNow.ToString("O");
            if (signalKind == "recoverable")
            {
                await _fs.WriteFileAtomicAsync("output/narrative_response.json", JsonSerializer.Serialize(new { response = "Fresh recoverable original output", timestamp }));
                await _fs.WriteFileAtomicAsync("output/debug_logs.json", JsonSerializer.Serialize(new { gm_thoughts_markdown = "Fresh recoverable original debug", timestamp }));
            }
            await _fs.WriteFileAtomicAsync("ready/turn_error.json", JsonSerializer.Serialize(new
            {
                sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber, timestamp,
                status = "error", error = "Original retained signal",
                harnessSource = signalKind == "dual" ? null : signalKind == "recoverable" ? "gm_output_without_terminal_signal" : "gm_runtime_unavailable"
            }));
            if (signalKind == "dual")
                await _fs.WriteFileAtomicAsync("ready/turn_complete.json", JsonSerializer.Serialize(new
                {
                    sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
                    timestamp, status = "success", filesModified = new[] { "output/narrative_response.json" }
                }));
            original = BrowserRecoveryTree();
            original.Remove(Path.GetRelativePath(_fs.BasePath, _fs.ResolvePath(PendingPlayerActionService.PendingPath)).Replace('\\', '/'));
        }
        if (late)
        {
            var (engine, staged) = await PrepareBrowserInputStagingAsync(withRollback: true);
            await WriteSignalsAsync(JsonSerializer.Deserialize<TurnRequest>(staged.RequestJson, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!);
            await InvokePrivateTaskAsync(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
        }
        else
        {
            CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
            var binding = await QueueBrowserInputAsync();
            var engine = CreateGameEngine(new QueuedConsoleInputSource([Key(ConsoleKey.Enter)]), finalizationHooks: new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = async checkpoint =>
                {
                    if (checkpoint == SessionFinalizationCheckpoint.TerminalWaitStarted) await WriteSignalsAsync(await WaitForTurnRequestAsync());
                }
            });
            await InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", binding.Action, null, null, null, true, binding);
        }
        Assert.NotNull(original);
        var after = BrowserRecoveryTree();
        var pendingKey = Path.GetRelativePath(_fs.BasePath, _fs.ResolvePath(PendingPlayerActionService.PendingPath)).Replace('\\', '/');
        Assert.Equal("terminalProcessing", JsonNode.Parse((await _fs.ReadFileAsync(PendingPlayerActionService.PendingPath))!)!["status"]!.GetValue<string>());
        after.Remove(pendingKey);
        Assert.Equal(original.Keys.Order(), after.Keys.Order());
        foreach (var entry in original) Assert.Equal(entry.Value, after[entry.Key]);
    }

    [Theory]
    [InlineData("state")]
    [InlineData("history")]
    [InlineData("generation")]
    [InlineData("repair-work")]
    [InlineData("original-artifact")]
    public async Task BrowserInput_DamagedSettledReceiptRefusesAndPreservesEvidence(string damage)
    {
        await PrepareSettledBrowserRecordAsync();
        switch (damage)
        {
            case "state": await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{\"changed\":true}"); break;
            case "history": await _fs.WriteFileAtomicAsync("stories/competing.jsonl", "{\"late\":true}\n"); break;
            case "generation":
                var root = JsonNode.Parse((await _fs.ReadFileAsync(PendingPlayerActionService.PendingPath))!)!;
                root["sessionGeneration"] = Guid.NewGuid().ToString("N");
                await _fs.WriteFileAtomicAsync(PendingPlayerActionService.PendingPath, root.ToJsonString()); break;
            case "repair-work": await _fs.WriteFileAtomicAsync("game_state/control/validation_repair_request.json", "{\"remaining\":true}"); break;
            case "original-artifact": await _fs.WriteFileAtomicAsync("input/turn_request.json", "{\"late\":true}"); break;
        }
        var before = BrowserRecoveryTree();
        await Assert.ThrowsAsync<InvalidDataException>(() => InvokePrivateTaskAsync(CreateGameEngine(), "ClassifyBrowserRecoveryAsync"));
        AssertBrowserRecoveryTree(before);
    }

    [Fact]
    public async Task BrowserInput_FailedOriginalRollbackRetainsSnapshotAndBlocksContinuation()
    {
        var (engine, staged) = await PrepareBrowserInputStagingAsync(withRollback: true);
        await InvokePrivateTaskAsync(engine, "ClaimBrowserTerminalAsync", staged);
        var originalManifest = await InvokePrivateTaskResultAsync(engine, "LoadPendingTurnSnapshotManifestAsync");
        var snapshot = await InvokePrivateTaskResultAsync(engine, "GetValidatedRollbackSnapshotAsync", originalManifest);
        var original = await _fs.ReadFileAsync(PendingTurnSnapshotAuthority.AuthorityPath);
        // Corrupt an original retained backup: the actual rollback must refuse before any restore.
        var manifest = JsonNode.Parse(staged.ManifestJson)!;
        var backup = manifest["rollbackBackups"]!.AsObject().First().Value!.GetValue<string>();
        await _fs.WriteFileAtomicAsync(backup, "damaged original rollback evidence");
        Assert.False(await InvokePrivateAsync<bool>(engine, "RollbackRejectedAcceptedTurnAsync", snapshot, ""));
        Assert.Equal(original, await _fs.ReadFileAsync(PendingTurnSnapshotAuthority.AuthorityPath));
        Assert.True(_fs.FileExists(backup));
        var before = BrowserRecoveryTree();
        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokePrivateTaskAsync(CreateGameEngine(), "ClassifyBrowserRecoveryAsync"));
        AssertBrowserRecoveryTree(before);
    }

    [Theory]
    [InlineData("queued")]
    [InlineData("staged")]
    [InlineData("terminalProcessing")]
    [InlineData("accepted")]
    [InlineData("settled")]
    [InlineData("stale-generation")]
    public async Task BrowserInput_SeparateProcessColdClassificationPreservesOrDequeuesOnlyOriginalSlot(string phase)
    {
        if (phase == "queued") await QueueBrowserInputAsync();
        else if (phase == "accepted") await PrepareAcceptedBrowserRecordAsync();
        else if (phase == "settled") await PrepareSettledBrowserRecordAsync();
        else
        {
            var (engine, staged) = await PrepareBrowserInputStagingAsync();
            if (phase == "terminalProcessing") await InvokePrivateTaskAsync(engine, "ClaimBrowserTerminalAsync", staged);
            if (phase == "stale-generation")
            {
                var root = JsonNode.Parse((await _fs.ReadFileAsync(PendingPlayerActionService.PendingPath))!)!;
                root["sessionGeneration"] = Guid.NewGuid().ToString("N");
                await _fs.WriteFileAtomicAsync(PendingPlayerActionService.PendingPath, root.ToJsonString());
            }
        }
        var before = BrowserRecoveryTree();
        var own = Path.Combine("/tmp", "gc-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(own);
        _directGachaOutput?.WriteLine("Owned browser cold evidence: " + own);
        var package = Path.Combine(own, "package");
        var repo = TestRepoPaths.RepoRoot;
        await RunBrowserColdChildAsync("pwsh", ["-NoLogo", "-NoProfile", "-File", Path.Combine(repo, "scripts/build-linux-supervisor.ps1"),
            "-OutputDirectory", package, "-IncludeHostGuardian"], Path.Combine(own, "native.log"), 25);
        var probePath = Path.Combine(own, "cold.json");
        var guardianPath = Path.Combine(own, "guardian.json");
        var dotnet = Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? throw new InvalidOperationException("DOTNET_ROOT required"), "dotnet");
        // Use the freshly copied support binary plus its runtime config from the selected build.
        var support = Path.Combine(Path.GetDirectoryName(typeof(GameEngineTurnLifecycleTests).Assembly.Location)!, "BookOfEternityClient.TestSupport.dll");
        await RunBrowserColdChildAsync(Path.Combine(package, "host-guardian"), ["--live-turn", guardianPath, "15000", dotnet,
            support, "engine-browser-recovery-cold", typeof(GameEngineTurnLifecycleTests).Assembly.Location, _fs.BasePath, probePath],
            Path.Combine(own, "cold.log"), 20);
        using var guardian = JsonDocument.Parse(File.ReadAllBytes(guardianPath));
        Assert.True(guardian.RootElement.GetProperty("echild").GetBoolean());
        Assert.Equal(0, guardian.RootElement.GetProperty("driverExitCode").GetInt32());
        Assert.Equal(0, guardian.RootElement.GetProperty("failures").GetInt32());
        Assert.Equal(0, guardian.RootElement.GetProperty("emergencySignals").GetInt32());
        Assert.False(guardian.RootElement.GetProperty("deadline").GetBoolean());
        using var probe = JsonDocument.Parse(File.ReadAllBytes(probePath));
        Assert.NotEqual(Environment.ProcessId, probe.RootElement.GetProperty("ProcessId").GetInt32());
        Assert.Equal(phase is "terminalProcessing" or "stale-generation", probe.RootElement.GetProperty("Blocked").GetBoolean());
        Assert.Equal(phase is "queued" or "staged" ? phase : null, probe.RootElement.GetProperty("ClassifiedPhase").GetString());
        Assert.Equal(phase == "terminalProcessing" ? typeof(InvalidOperationException).FullName :
            phase == "stale-generation" ? typeof(InvalidDataException).FullName : null, probe.RootElement.GetProperty("ErrorType").GetString());
        if (phase is "accepted" or "settled")
            Assert.True(before.Remove(Path.GetRelativePath(_fs.BasePath, _fs.ResolvePath(PendingPlayerActionService.PendingPath)).Replace('\\', '/')));
        AssertBrowserRecoveryTree(before);
    }

    // Called only in an independently owned process by NativeHostScenarioDriver.
    // This qualifies actual GameEngine recovery classification, not Program/relay gameplay.
    public static async Task WriteBrowserRecoveryColdProbeAsync(string root, string output)
    {
        using var factory = new GameEngineTurnLifecycleTests();
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        Dictionary<string, string> Snapshot() => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path).StartsWith(".boe_runtime", StringComparison.Ordinal))
            .ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(), StringComparer.Ordinal);
        var before = Snapshot();
        Exception? refusal = null;
        PendingPlayerActionService.State? classified = null;
        try { classified = await InvokePrivateAsync<PendingPlayerActionService.State?>(factory.CreateGameEngine(fileSystem: files), "ClassifyBrowserRecoveryAsync"); }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException) { refusal = exception; }
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
        {
            ProcessId = Environment.ProcessId, Blocked = refusal != null, ErrorType = refusal?.GetType().FullName,
            ClassifiedPhase = classified?.Phase,
            Before = before, After = Snapshot(), Scope = "actual GameEngine classification factory; no gameplay/relay/model"
        }));
    }

    private static async Task RunBrowserColdChildAsync(string executable, string[] args, string log, int seconds)
    {
        var info = new ProcessStartInfo(executable)
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = TestRepoPaths.RepoRoot };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(seconds));
            await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception failure)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                await File.WriteAllTextAsync(log, failure + Environment.NewLine +
                    (stdout.IsCompletedSuccessfully ? stdout.Result : "stdout EOF unobserved") + Environment.NewLine +
                    (stderr.IsCompletedSuccessfully ? stderr.Result : "stderr EOF unobserved"));
            }
            throw;
        }
        var text = await stdout + await stderr;
        await File.WriteAllTextAsync(log, text);
        Assert.True(process.ExitCode == 0, log + Environment.NewLine + text);
    }

    private async Task PrepareSettledBrowserRecordAsync()
    {
        var (engine, staged) = await PrepareBrowserInputStagingAsync(withRollback: true);
        staged = await InvokePrivateAsync<PendingPlayerActionService.Staged>(engine, "ClaimBrowserTerminalAsync", staged);
        var originalManifest = await InvokePrivateTaskResultAsync(engine, "LoadPendingTurnSnapshotManifestAsync");
        var snapshot = await InvokePrivateTaskResultAsync(engine, "GetValidatedRollbackSnapshotAsync", originalManifest);
        await InvokePrivateTaskAsync(engine, "RestorePreTurnBackup", snapshot);
        InvokePrivate(engine, "CleanupBackup", snapshot);
        _fs.DeleteFile("input/turn_request.json");
        await InvokePrivateTaskAsync(engine, "CleanupPendingTurnSnapshotAsync");
        var request = JsonSerializer.Deserialize<TurnRequest>(staged.RequestJson, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
        var signal = JsonSerializer.Serialize(new { sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
            timestamp = DateTime.UtcNow.ToString("O"), status = "error", error = "Original controlled terminal error." });
        await InvokePrivateTaskAsync(engine, "FinishRestoredBrowserActionAsync", staged, "originalTerminalErrorRestored", signal);
    }
    [Fact]
    public async Task BrowserInput_ConflictBeforePublicationPreservesCompetingRequestAndOriginalSlot()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        var binding = await QueueBrowserInputAsync();
        const string other = "{\"requestId\":\"competing-at-staging-publication\"}";
        var injected = false;
        _consoleMutationObserver = path =>
        {
            if (injected || path != PendingTurnSnapshotAuthority.AuthorityPath) return;
            injected = true;
            File.WriteAllText(_fs.ResolvePath("input/turn_request.json"), other);
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokePrivateTaskAsync(CreateGameEngine(),
            "ProcessPlayerTurn", binding.Action, null, null, null, true, binding));
        Assert.True(injected);
        Assert.Equal(other, await _fs.ReadFileAsync("input/turn_request.json"));
        Assert.Equal("preparing", JsonNode.Parse((await _fs.ReadFileAsync(PendingPlayerActionService.PendingPath))!)!["status"]!.GetValue<string>());
        Assert.True(_fs.FileExists(PendingTurnSnapshotAuthority.AuthorityPath));
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("error")]
    [InlineData("gm_terminal_wait_timeout")]
    [InlineData("gm_runtime_unavailable")]
    public async Task BrowserInput_TerminalDispositionSettlesOrBlocksBeforeContinuation(string disposition)
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        var binding = await QueueBrowserInputAsync();
        var input = new QueuedConsoleInputSource([Key(ConsoleKey.Enter)]);
        GameEngine? engine = null;
        engine = CreateGameEngine(input, finalizationHooks: new GameEngineSessionFinalizationHooks
        {
            AtCheckpointAsync = async checkpoint =>
            {
                if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted) return;
                if (disposition == "cancel") { input.Enqueue(Key(ConsoleKey.Escape)); return; }
                if (disposition != "error")
                {
                    Assert.True(await InvokePrivateAsync<bool>(engine!, "TryWriteHarnessTerminalErrorAsync", disposition, "Controlled synthetic GM diagnostic."));
                    return;
                }
                var request = await WaitForTurnRequestAsync();
                await _fs.WriteFileAtomicAsync("ready/turn_error.json", JsonSerializer.Serialize(new
                {
                    sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
                    timestamp = DateTime.UtcNow.ToString("O"), status = "error", error = "Controlled original GM terminal error."
                }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            }
        });
        await InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", binding.Action, null, null, null, true, binding);
        if (disposition != "error")
        {
            // Local waiter completion proves no external worker stop or late-output fence.
            Assert.NotNull(await _fs.ReadFileAsync(PendingPlayerActionService.PendingPath));
            Assert.Equal("terminalProcessing", JsonNode.Parse((await _fs.ReadFileAsync(PendingPlayerActionService.PendingPath))!)!["status"]!.GetValue<string>());
            var before = BrowserRecoveryTree();
            await Assert.ThrowsAsync<InvalidOperationException>(() => InvokePrivateTaskAsync(CreateGameEngine(), "ClassifyBrowserRecoveryAsync"));
            AssertBrowserRecoveryTree(before);
        }
        else Assert.False(_fs.FileExists(PendingPlayerActionService.PendingPath));
        if (disposition is "cancel" or "error")
        {
            Assert.False(_fs.FileExists("input/turn_request.json"));
            Assert.False(_fs.FileExists(PendingTurnSnapshotAuthority.AuthorityPath));
        }
        else
        {
            Assert.True(_fs.FileExists("input/turn_request.json"));
            Assert.True(_fs.FileExists("ready/turn_error.json"));
            Assert.True(_fs.FileExists(PendingTurnSnapshotAuthority.AuthorityPath));
        }
        Assert.Equal(0, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
    }

    [Fact]
    public async Task BrowserInput_ProcessRefusesCompetingRequestBeforeMutatingValidation()
    {
        var binding = await QueueBrowserInputAsync();
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "{\"requestId\":\"competing-before-validation\"}");
        var before = BrowserRecoveryTree();
        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokePrivateTaskAsync(CreateGameEngine(),
            "ProcessPlayerTurn", binding.Action, null, null, null, true, binding));
        AssertBrowserRecoveryTree(before);
    }

    [Fact]
    public async Task BrowserInput_CompetingRequestAfterSelectionCannotBeOverwrittenByClaim()
    {
        var binding = await QueueBrowserInputAsync();
        var pending = File.ReadAllBytes(_fs.ResolvePath(PendingPlayerActionService.PendingPath));
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "{\"requestId\":\"other-original-request\"}");
        var competing = File.ReadAllBytes(_fs.ResolvePath("input/turn_request.json"));
        var engine = CreateGameEngine();
        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokePrivateTaskAsync(engine, "ClaimBrowserPreparationAsync", binding));
        Assert.Equal(pending, File.ReadAllBytes(_fs.ResolvePath(PendingPlayerActionService.PendingPath)));
        Assert.Equal(competing, File.ReadAllBytes(_fs.ResolvePath("input/turn_request.json")));
    }

    [Fact]
    public async Task BrowserInput_PreparingMenuRefusesBeforeHealthMutationAndPreservesEvidence()
    {
        var binding = await QueueBrowserInputAsync();
        var engine = CreateGameEngine();
        await InvokePrivateTaskAsync(engine, "ClaimBrowserPreparationAsync", binding);
        var before = BrowserRecoveryTree();
        Assert.False(await InvokePrivateAsync<bool>(engine, "HasCurrentSessionCoreAsync"));
        AssertBrowserRecoveryTree(before);
    }

    [Fact]
    public async Task BrowserInput_StagedMenuDescriptionDoesNotInitializeOrRepairCanonicalState()
    {
        var (engine, _) = await PrepareBrowserInputStagingAsync();
        var before = BrowserRecoveryTree();
        await InvokePrivateTaskAsync(engine, "BuildContinueDescriptionAsync");
        AssertBrowserRecoveryTree(before);
    }

    [Fact]
    public async Task BrowserInput_ProcessingReentryRefusesBeforeReplayOrEvidenceCleanup()
    {
        var (engine, staged) = await PrepareBrowserInputStagingAsync();
        await InvokePrivateTaskAsync(engine, "ClaimBrowserTerminalAsync", staged);
        var before = BrowserRecoveryTree();
        var restarted = CreateGameEngine();
        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokePrivateTaskAsync(restarted, "ClassifyBrowserRecoveryAsync"));
        AssertBrowserRecoveryTree(before);
    }

    [Fact]
    public async Task BrowserInput_AcceptedRecordReentryOnlyDequeuesOriginalSlotAndKeepsHistory()
    {
        await PrepareAcceptedBrowserRecordAsync();
        var before = BrowserRecoveryTree();
        await InvokePrivateTaskAsync(CreateGameEngine(), "ClassifyBrowserRecoveryAsync");
        Assert.False(File.Exists(_fs.ResolvePath(PendingPlayerActionService.PendingPath)));
        Assert.True(before.Remove(Path.GetRelativePath(_fs.BasePath,
            _fs.ResolvePath(PendingPlayerActionService.PendingPath)).Replace('\\', '/')));
        AssertBrowserRecoveryTree(before);
        await InvokePrivateTaskAsync(CreateGameEngine(), "ClassifyBrowserRecoveryAsync");
        AssertBrowserRecoveryTree(before);
    }

    [Theory]
    [InlineData("history")]
    [InlineData("proof")]
    [InlineData("new-request")]
    [InlineData("generation")]
    public async Task BrowserInput_DamagedAcceptedRecordRefusesAndPreservesAllRemainingEvidence(string damage)
    {
        await PrepareAcceptedBrowserRecordAsync();
        var pendingPath = _fs.ResolvePath(PendingPlayerActionService.PendingPath);
        var pending = JsonNode.Parse(File.ReadAllText(pendingPath))!.AsObject();
        if (damage == "new-request")
            await _fs.WriteFileAtomicAsync("input/turn_request.json", "{\"requestId\":\"newer-original-request\"}");
        else if (damage == "history")
        {
            var proof = JsonNode.Parse(pending["phaseProof"]!.GetValue<string>())!.AsObject();
            var path = proof["story"]!["path"]!.GetValue<string>();
            File.AppendAllText(_fs.ResolvePath(path), proof["story"]!["rowJson"]!.GetValue<string>() + "\n");
        }
        else
        {
            pending[damage == "proof" ? "phaseProofHash" : "sessionGeneration"] = "damaged-original-binding";
            await _fs.WriteFileAtomicAsync(PendingPlayerActionService.PendingPath, pending.ToJsonString());
        }
        var before = BrowserRecoveryTree();
        await Assert.ThrowsAsync<InvalidDataException>(() => InvokePrivateTaskAsync(CreateGameEngine(), "ClassifyBrowserRecoveryAsync"));
        AssertBrowserRecoveryTree(before);
    }

    private Task<PendingPlayerActionService.Binding> QueueBrowserInputAsync() =>
        SessionOperationContext.RunParticipatingCurrentSessionAsync(_fs, async () =>
        {
            await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            var root = PendingPlayerActionService.PrepareQueued(_fs, lease, "Я читаю исходное письмо.", "browser-composer", DateTime.UtcNow.ToString("O"));
            var json = root.ToJsonString();
            await _fs.WriteFileAtomicAsync(lease, PendingPlayerActionService.PendingPath, json);
            return PendingPlayerActionService.Parse(json, _fs.GetOrCreateSessionGeneration(lease)).Binding;
        });

    private async Task<(GameEngine Engine, PendingPlayerActionService.Staged Staged)> PrepareBrowserInputStagingAsync(bool withRollback = false)
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json",
            "{\"soulName\":\"Проверочная душа\",\"sessionId\":\"browser-recovery-session\",\"currentRealm\":\"Mortal World\",\"currentIncarnation\":1}");
        var binding = await QueueBrowserInputAsync();
        var engine = CreateGameEngine(new QueuedConsoleInputSource([Key(ConsoleKey.Enter)]));
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession("browser-recovery-session", 0);
        await InvokePrivateTaskAsync(engine, "ClaimBrowserPreparationAsync", binding);
        var request = new TurnRequest
        {
            SessionId = "browser-recovery-session", RequestId = binding.ActionId, TurnNumber = 1,
            PlayerAction = binding.Action, Timestamp = DateTime.UtcNow.ToString("O"), PreGeneratedDices1d20 = [3, 17]
        };
        var rollback = withRollback ? await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", binding.ActionId) : null;
        await InvokePrivateTaskAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, rollback, "обработки хода", binding);
        var staged = await InvokePrivateAsync<PendingPlayerActionService.Staged>(engine, "PublishBrowserStagingAsync", binding,
            JsonSerializer.Serialize(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        return (engine, staged);
    }

    private async Task PrepareAcceptedBrowserRecordAsync()
    {
        var (engine, staged) = await PrepareBrowserInputStagingAsync();
        staged = await InvokePrivateAsync<PendingPlayerActionService.Staged>(engine, "ClaimBrowserTerminalAsync", staged);
        var story = await InvokePrivateAsync<PendingPlayerActionService.StoryProof>(engine, "AppendBrowserStoryAsync", staged,
            new GameResponse { Response = "Исходное письмо прочитано." }, "Берег", null);
        // Component fixture establishes completed original artifact inventory explicitly;
        // it does not qualify gameplay's terminal branch, which has its own actual C5 chain.
        _fs.DeleteFile("input/turn_request.json");
        await InvokePrivateTaskAsync(engine, "CleanupPendingTurnSnapshotAsync");
        await InvokePrivateTaskAsync(engine, "FinishAcceptedBrowserActionAsync", staged, story);
    }

    private Dictionary<string, byte[]> BrowserRecoveryTree() => Directory.EnumerateFiles(_fs.BasePath, "*", SearchOption.AllDirectories)
        .Where(path => !Path.GetRelativePath(_fs.BasePath, path).StartsWith(".boe_runtime", StringComparison.Ordinal))
        .ToDictionary(path => Path.GetRelativePath(_fs.BasePath, path).Replace('\\', '/'), File.ReadAllBytes, StringComparer.Ordinal);

    private void AssertBrowserRecoveryTree(Dictionary<string, byte[]> before)
    {
        var after = BrowserRecoveryTree();
        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
    }
}
