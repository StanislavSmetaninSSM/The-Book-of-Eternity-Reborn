using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task BrowserOriginalAdmission_ActualSuccessConsumerProfilesBeforeOriginalStop()
    {
        await RunBrowserConsumerProfileAsync(mode: 0);
    }

    [Fact]
    public async Task BrowserOriginalAdmission_BoundedDurationDiagnostic()
    {
        await RunBrowserConsumerProfileAsync(mode: 1);
    }

    private async Task RunBrowserConsumerProfileAsync(int mode)
    {
        var own = Path.Combine("/tmp", "gc-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(own);
        _directGachaOutput?.WriteLine("Owned actual staging cut evidence: " + own);
        var package = Path.Combine(own, "package");
        await RunBrowserProfileChildAsync("pwsh", ["-NoLogo", "-NoProfile", "-File",
            Path.Combine(TestRepoPaths.RepoRoot, "scripts/build-linux-supervisor.ps1"), "-OutputDirectory", package,
            "-IncludeHostGuardian", "-IncludeTerminalFixture"], Path.Combine(own, "native.log"), 25);
        var support = Path.Combine(Path.GetDirectoryName(typeof(GameEngineTurnLifecycleTests).Assembly.Location)!, "BookOfEternityClient.TestSupport.dll");
        await RunBrowserProfileChildAsync(Path.Combine(package, "host-guardian"), ["--live-turn", Path.Combine(own, "guardian.json"), (mode == 1 ? "260000" : "110000"),
            Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"), support, "engine-browser-consumer-profile",
            typeof(GameEngineTurnLifecycleTests).Assembly.Location, package, Path.Combine(own, "result.json"), mode.ToString(), own], Path.Combine(own, "probe.log"), mode == 1 ? 265 : 115);
        var guardian = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "guardian.json")))!;
        Assert.True(guardian["echild"]!.GetValue<bool>());
        Assert.Equal(0, guardian["driverExitCode"]!.GetValue<int>());
        Assert.Equal(0, guardian["failures"]!.GetValue<int>());
        Assert.Equal(0, guardian["emergencySignals"]!.GetValue<int>());
        Assert.False(guardian["deadline"]!.GetValue<bool>());
        var result = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "result.json")))!;
        Assert.True(result["Accepted"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["OriginalStopped"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["PhysicalCleanup"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["OperationSettled"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["StopTaskSettled"]!.GetValue<bool>(), result.ToJsonString());
        Assert.Null(result["OperationFailure"]);
        Assert.Null(result["CleanupFailure"]);
    }

    public static async Task WriteBrowserConsumerProfileProbeAsync(string package, string output, int mode, string own)
    {
        if (mode is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(mode));
        var consumerSafetySeconds = mode == 1 ? 180 : 40;
        var resolvedRepoRoot = Path.GetFullPath(TestRepoPaths.RepoRoot);
        object DescribeAssembly(Assembly assembly)
        {
            var location = Path.GetFullPath(assembly.Location);
            Assert.StartsWith(resolvedRepoRoot + Path.DirectorySeparatorChar, location);
            return new { Name = assembly.GetName().Name, Location = location,
                Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(location))) };
        }
        var coreAssembly = DescribeAssembly(typeof(GameEngine).Assembly);
        var testAssembly = DescribeAssembly(typeof(GameEngineTurnLifecycleTests).Assembly);
        var supportAssembly = DescribeAssembly(Assembly.GetEntryAssembly()!);
        using var factory = new GameEngineTurnLifecycleTests();
        var launch = NeutralTerminalLaunch.Create(package, own);
        var root = Directory.GetParent(launch.Scratch)!.FullName;
        var watch = Stopwatch.StartNew();
        var observations = new ConcurrentQueue<object>();
        var reads = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var timings = new ConcurrentDictionary<string, (int Count, long Ticks)>(StringComparer.Ordinal);
        object[] CaptureTimings() => timings.OrderBy(pair => pair.Key).Select(pair => (object)new
        {
            Stage = pair.Key, pair.Value.Count, Seconds = TimeSpan.FromTicks(pair.Value.Ticks).TotalSeconds
        }).ToArray();
        var preflights = 0;
        var acquisitions = 0;
        var contention = 0;
        void Observe(string label) => observations.Enqueue(new { Label = label, Seconds = watch.Elapsed.TotalSeconds });
        var hooks = new FileSystemManagerHooks
        {
            AfterCanonicalWriteLockOpenedAsync = () => { Interlocked.Increment(ref acquisitions); return Task.CompletedTask; },
            CanonicalWriteLockContendedAsync = () => { Interlocked.Increment(ref contention); return Task.CompletedTask; },
            AfterCanonicalReadAttemptAsync = path => { reads.AddOrUpdate(path, 1, (_, n) => n + 1); Observe("read-attempt:" + path); return Task.CompletedTask; }
        };
        // The identical diagnostic fixture can be built against the historical
        // runtime that predates these read-only observers. Missing instrumentation
        // is reported; it never substitutes an admission or authority decision.
        bool AttachObserver(string name, object callback)
        {
            var property = typeof(FileSystemManagerHooks).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (property == null)
            {
                if (mode == 0) throw new InvalidOperationException("Required profile observer is missing: " + name);
                return false;
            }
            property.SetValue(hooks, callback);
            return true;
        }
        var timingSupported = AttachObserver("BrowserOriginalAdmissionTimingObserver", (Action<string, TimeSpan>)((stage, elapsed) =>
            timings.AddOrUpdate(stage, (1, elapsed.Ticks), (_, previous) => (previous.Count + 1, previous.Ticks + elapsed.Ticks))));
        var preflightSupported = AttachObserver("AfterBrowserOriginalPreflightAsync", (Func<Task>)(() =>
        {
            Interlocked.Increment(ref preflights); Observe("admission-preflight-completed"); return Task.CompletedTask;
        }));
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, hooks);
        files.EnsureDirectoryStructure();
        var bootstrap = factory.CreateGameEngine(new NewGameCancelInput(), settings =>
        {
            settings.MusicEnabled = false; settings.SoundEnabled = false;
        }, fileSystem: files);
        await GetPrivateField<StateManager>(bootstrap, "_stateManager").BootstrapLocalStorageAsync();
        var guardian = new SystemGuardianLibraryService(files, NullLogger<SystemGuardianLibraryService>.Instance)
            .BuildFreeformPendingGuardianCreationNode("Спокойный Хранитель берега Моря Хаоса.", "Пробная Душа");
        await InvokePrivateAsync<string>(bootstrap, "InitializeChaosSea", "Пробная Душа", "Человеческий силуэт синего света.", guardian, null);
        Assert.False(await InvokePrivateAsync<bool>(bootstrap, "WaitForGmResponse").WaitAsync(TimeSpan.FromSeconds(8)));
        Observe("ordinary-bootstrap-cancelled");

        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var type = Assembly.LoadFrom(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityGMBridge/bin", configuration, "net8.0/BookOfEternityGMBridge.dll"))
            .GetType("BookOfEternityGMBridge.BridgeHost", true)!;
        var bridgeAssembly = DescribeAssembly(type.Assembly);
        var provenance = new { ResolvedRepoRoot = resolvedRepoRoot, Core = coreAssembly, Bridge = bridgeAssembly,
            Tests = testAssembly, Support = supportAssembly };
        await File.WriteAllTextAsync(Path.Combine(own, "runtime-provenance.json"), JsonSerializer.Serialize(provenance));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var host = Activator.CreateInstance(type, [launch.Scratch, "c5-profile-" + Guid.NewGuid().ToString("N")])!;
        type.GetMethod("ConfigureNeutral", flags)!.Invoke(host, [launch]);
        async Task Call(string name) => await (Task)type.GetMethod(name, flags)!.Invoke(host, null)!;
        using var control = new CancellationTokenSource();
        var server = (Task)type.GetMethod("RunServerLoopAsync", flags)!.Invoke(host, [control.Token])!;
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? operation = null;
        Exception? operationFailure = null;
        Exception? cleanupFailure = null;
        bool accepted = false, stopped = false, physical = false, deadline = false;
        GmSessionRunCoordinator? owner = null;
        TurnRequest? originalRequest = null;
        object? beforeStop = null;
        object[]? timingsAtPublication = null;
        double? publicationSeconds = null, consumerDurationSeconds = null;
        object? fixture = null;
        try
        {
            await Call("StartShellAsync");
            await ((TaskCompletionSource)type.GetField("_firstStatus", flags)!.GetValue(host)!).Task.WaitAsync(TimeSpan.FromSeconds(3));
            owner = (GmSessionRunCoordinator)type.GetField("_mainRun", flags)!.GetValue(host)!;
            var binding = await QueueBrowserProfileInputAsync(files);
            var engine = factory.CreateGameEngine(new QueuedConsoleInputSource([]), fileSystem: files,
                configureSettings: settings => { settings.MusicEnabled = false; settings.SoundEnabled = false; },
                finalizationHooks: new GameEngineSessionFinalizationHooks
                {
                    AtCheckpointAsync = checkpoint =>
                    {
                        Observe("engine:" + checkpoint);
                        if (checkpoint == SessionFinalizationCheckpoint.TerminalWaitStarted)
                        {
                            originalRequest = JsonSerializer.Deserialize<TurnRequest>(File.ReadAllText(files.ResolvePath("input/turn_request.json")), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                            File.WriteAllText(Path.Combine(own, "original-request.json"), JsonSerializer.Serialize(originalRequest));
                            if (mode == 1) fixture = CaptureBrowserProfileFixture(files, own);
                            WriteBrowserProfileSuccess(files, originalRequest);
                            Observe("authored-success-published");
                            publicationSeconds = watch.Elapsed.TotalSeconds;
                            timingsAtPublication = CaptureTimings();
                            published.TrySetResult();
                        }
                        return Task.CompletedTask;
                    }
                });
            var originalLoop = GetPrivateField<GameLoop>(bootstrap, "_gameLoop");
            GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(originalLoop.SessionId, originalLoop.TurnNumber);
            operation = InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", binding.Action, null, null, null, true, binding);
            await Task.WhenAny(published.Task, operation).WaitAsync(TimeSpan.FromSeconds(30));
            if (!published.Task.IsCompleted)
            {
                await operation;
                throw new InvalidOperationException("Original operation returned before authored terminal publication.");
            }
            await published.Task;
            var completed = await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(consumerSafetySeconds)));
            consumerDurationSeconds = watch.Elapsed.TotalSeconds - publicationSeconds;
            deadline = completed != operation;
            Observe(deadline ? "profile-deadline-before-stop" : "operation-settled-before-stop");
            beforeStop = new
            {
                DiagnosticOnly = mode == 1, ConsumerSafetySeconds = consumerSafetySeconds, ConsumerDurationSeconds = consumerDurationSeconds,
                Legacy40SecondsExceeded = consumerDurationSeconds > 40, TimingSupported = timingSupported, PreflightSupported = preflightSupported, Provenance = provenance, Fixture = fixture,
                Record = owner.Record, Preflights = preflightSupported ? (int?)preflights : null, Acquisitions = acquisitions, Contention = contention,
                Reads = reads.ToArray(), Phase = File.Exists(files.ResolvePath(PendingPlayerActionService.PendingPath))
                    ? JsonNode.Parse(File.ReadAllText(files.ResolvePath(PendingPlayerActionService.PendingPath)))!["status"]!.GetValue<string>() : null,
                Observations = observations.ToArray(), OperationSettled = operation.IsCompleted, Deadline = deadline,
                AdmissionTimings = CaptureTimings(), AdmissionTimingsAtPublication = timingsAtPublication
            };
            await File.WriteAllTextAsync(Path.Combine(own, "profile-before-stop.json"), JsonSerializer.Serialize(beforeStop));
            if (!deadline)
            {
                await operation;
                var path = files.ResolvePath("stories/chaos_sea.jsonl");
                var rows = File.ReadAllLines(path).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => JsonNode.Parse(line)!).ToArray();
                accepted = rows.Length == 1 && rows[0]["requestId"]!.GetValue<string>() == originalRequest!.RequestId &&
                    !File.Exists(files.ResolvePath(PendingPlayerActionService.PendingPath)) && !File.Exists(files.ResolvePath("input/turn_request.json"));
                await File.WriteAllBytesAsync(Path.Combine(own, "accepted-story.jsonl"), File.ReadAllBytes(path));
            }
        }
        catch (Exception failure) { operationFailure = failure; }
        finally
        {
            Observe("original-stop-requested");
            var stop = Call("StopShellAsync");
            if (operation != null)
                try { await operation.WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (Exception failure) { operationFailure ??= failure; }
            try { await stop.WaitAsync(TimeSpan.FromSeconds(10)); stopped = owner?.Record?.Disposition == GmSessionRunDisposition.Stopped; }
            catch (Exception failure) { cleanupFailure = failure; }
            if (type.GetField("_pty", flags)!.GetValue(host) is IOwnedTerminalSession terminal)
                physical = (await terminal.StopAndObserveAsync(CancellationToken.None)).CleanupComplete;
            else physical = stopped;
            await control.CancelAsync();
            try { await server.WaitAsync(TimeSpan.FromSeconds(5)); ((IDisposable)host).Dispose(); }
            catch (Exception failure) { cleanupFailure ??= failure; }
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
            {
                DiagnosticOnly = mode == 1, ConsumerSafetySeconds = consumerSafetySeconds, ConsumerDurationSeconds = consumerDurationSeconds,
                Legacy40SecondsExceeded = consumerDurationSeconds > 40, TimingSupported = timingSupported, PreflightSupported = preflightSupported, Provenance = provenance, Fixture = fixture,
                Accepted = accepted, OriginalStopped = stopped, PhysicalCleanup = physical, Deadline = deadline,
                OperationSettled = operation?.IsCompleted == true, StopTaskSettled = stop.IsCompleted,
                BeforeStop = beforeStop, OperationFailure = operationFailure?.ToString(), CleanupFailure = cleanupFailure?.ToString(),
                Observations = observations.ToArray(), ModelCalls = 0,
                AdmissionTimings = CaptureTimings(),
                Scope = "Actual ordinary cancelled bootstrap, NativeLineage original Bridge and real ProcessPlayerTurn success consumer; fixture authors correlated response through existing checkpoint. Not Program/relay C5. Preflight count excludes additional recovery scans."
            }));
        }
    }

    private static async Task RunBrowserProfileChildAsync(string executable, string[] args, string log, int seconds)
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

    private static Task<PendingPlayerActionService.Binding> QueueBrowserProfileInputAsync(FileSystemManager files) =>
        SessionOperationContext.RunParticipatingCurrentSessionAsync(files, async () =>
        {
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            var root = PendingPlayerActionService.PrepareQueued(files, lease, "Я читаю исходное письмо.", "browser-composer", DateTime.UtcNow.ToString("O"));
            var json = root.ToJsonString();
            await files.WriteFileAtomicAsync(lease, PendingPlayerActionService.PendingPath, json);
            return PendingPlayerActionService.Parse(json, files.GetOrCreateSessionGeneration(lease)).Binding;
        });

    private static object CaptureBrowserProfileFixture(FileSystemManager files, string own)
    {
        object Describe(string relative)
        {
            var bytes = File.ReadAllBytes(files.ResolvePath(relative));
            return new { Path = relative, Bytes = bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
        }
        var manifestPath = "game_state/control/pending_turn_snapshot.json";
        var authorityPath = PendingTurnSnapshotAuthority.AuthorityPath;
        var manifest = JsonNode.Parse(File.ReadAllText(files.ResolvePath(manifestPath)))!;
        var snapshots = manifest["files"]!.AsObject().OrderBy(pair => pair.Key)
            .Select(pair => Describe(pair.Value!.GetValue<string>())).ToArray();
        var rollback = manifest["rollbackBackups"]!.AsObject().OrderBy(pair => pair.Key)
            .Select(pair => Describe(pair.Value!.GetValue<string>())).ToArray();
        foreach (var (relative, name) in new[] { ("input/turn_request.json", "initial-request.json"),
            (manifestPath, "initial-manifest.json"), (authorityPath, "initial-authority.json") })
            File.WriteAllBytes(Path.Combine(own, name), File.ReadAllBytes(files.ResolvePath(relative)));
        return new { Request = Describe("input/turn_request.json"), Manifest = Describe(manifestPath), Authority = Describe(authorityPath),
            SnapshotCount = snapshots.Length, RollbackCount = rollback.Length, Snapshots = snapshots, Rollback = rollback };
    }

    private static void WriteBrowserProfileSuccess(FileSystemManager files, TurnRequest request)
    {
        var timestamp = DateTime.UtcNow.ToString("O");
        void Write(string path, object value) => File.WriteAllText(files.ResolvePath(path), JsonSerializer.Serialize(value));
        var guardians = JsonNode.Parse(File.ReadAllText(files.ResolvePath("game_state/meta/guardians.json")))!;
        var guardian = guardians["guardians"]![0]!;
        var actor = guardian["canonicalName"]!.GetValue<string>();
        const string thought = "Я запомню самостоятельный выбор души прочитать письмо у берега.";
        const string journalPath = "game_state/meta/guardian_thought_journal.json";
        var journal = File.Exists(files.ResolvePath(journalPath))
            ? JsonNode.Parse(File.ReadAllText(files.ResolvePath(journalPath)))!.AsObject() : new JsonObject { ["entries"] = new JsonArray() };
        journal["guardianThoughtJournalUpdates"] = JsonSerializer.SerializeToNode(new[] { new
        {
            entryId = "profile_thought_" + request.RequestId, guardianId = guardian["guardianId"]!.GetValue<string>(),
            turn = request.TurnNumber, timestamp, title = "Наблюдение у берега", summary = thought,
            eventType = "soul_assessment", consequence = "Душа сохраняет самостоятельность.", attitude = "intrigued", intent = "Остаться рядом без вмешательства."
        }});
        File.WriteAllText(files.ResolvePath(journalPath), journal.ToJsonString());
        var reasoning = string.Join("\n", "## NPC Scope", "- Mode: Scene-local", "- Relevant actors: " + actor,
            "- Why relevant: Хранитель наблюдает за выбором души и сохраняет свою реакцию.", "- Actors outside scope: нет",
            "- Why outside scope: Самостоятельные акторы не участвуют.", "", "## Reasoning", "### " + actor,
            "- Current location: Море Хаоса; перемещения нет.", "- Situation: Душа читает письмо у берега, Хранитель наблюдает.",
            "- Profile inputs: Существующий свободный Хранитель сопровождает душу; искусства не применяются.",
            "- Motivation: Дать душе пространство для самостоятельного решения.", "- Constraints: Без новых сил, ресурсов, предметов или ран.",
            "- Thoughts: " + thought, "- Strategy options:",
            "1. Наблюдать. Benefit: сохранить самостоятельность. Risk: душа не попросит помощи.",
            "2. Вмешаться. Benefit: дать совет. Risk: навязать направление.", "- Chosen strategy: Наблюдать.",
            "- Rejected alternatives: Душа не просила совета.", "- Actions: Хранитель наблюдает и запоминает выбор души.",
            "- State changes: guardianThoughtJournalUpdates в game_state/meta/guardian_thought_journal.json: одна новая first-person запись, предыдущие entries сохраняются.",
            "- Детерминированная тестовая заготовка; провайдер не вызван.");
        Write("output/narrative_response.json", new { response = "Исходное письмо прочитано.", timestamp });
        Write("output/interface_updates.json", new { dialogueOptions = Array.Empty<object>(), timestamp });
        Write("output/debug_logs.json", new { timestamp, gm_thoughts_markdown = reasoning });
        var c = request.ProgressionControl!;
        Write(ProgressionScheduleService.ReportPath, new { progressionProcessingReport = new
        {
            sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
            worldCyclesProcessed = 0, factionCyclesProcessed = 0,
            newLastWorldSimulationTimeInMinutes = c.LastWorldSimulationTimeInMinutes,
            newLastFactionSimulationTimeInMinutes = c.LastFactionSimulationTimeInMinutes,
            chaosSeaCyclesProcessed = c.ChaosSeaCyclesExpectedThisTurn, guardianProjectCyclesProcessed = c.GuardianProjectCyclesExpectedThisTurn,
            residentAgencyCyclesProcessed = c.ResidentAgencyCyclesExpectedThisTurn, shiningAbodeCyclesProcessed = c.ShiningAbodeCyclesExpectedThisTurn,
            shiningFactionCyclesProcessed = c.ShiningFactionCyclesExpectedThisTurn, shiningTradeCyclesProcessed = c.ShiningTradeCyclesExpectedThisTurn,
            newLastChaosSeaSimulationOrdinal = c.NextChaosSeaTurnOrdinal, newLastGuardianProjectCycleOrdinal = c.NextGuardianProjectCycleOrdinal,
            newLastResidentAgencyCycleOrdinal = c.NextResidentAgencyCycleOrdinal, newLastShiningAbodeCycleOrdinal = c.NextShiningAbodeCycleOrdinal,
            newLastShiningFactionCycleOrdinal = c.NextShiningFactionCycleOrdinal, newLastShiningTradeCycleOrdinal = c.NextShiningTradeCycleOrdinal,
            afterlifeCatchupProcessed = false, afterlifeCatchupSummaryEventsProcessed = 0
        }});
        Write("ready/turn_complete.json", new { sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
            timestamp, status = "success", filesModified = new[] { journalPath, "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json", ProgressionScheduleService.ReportPath } });
    }
}
