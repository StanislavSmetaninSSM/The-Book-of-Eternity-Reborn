using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
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
        var own = Path.Combine("/tmp", "gc-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(own);
        _directGachaOutput?.WriteLine("Owned actual staging cut evidence: " + own);
        var package = Path.Combine(own, "package");
        await RunBrowserColdChildAsync("pwsh", ["-NoLogo", "-NoProfile", "-File",
            Path.Combine(TestRepoPaths.RepoRoot, "scripts/build-linux-supervisor.ps1"), "-OutputDirectory", package,
            "-IncludeHostGuardian", "-IncludeTerminalFixture"], Path.Combine(own, "native.log"), 25);
        var support = Path.Combine(Path.GetDirectoryName(typeof(GameEngineTurnLifecycleTests).Assembly.Location)!, "BookOfEternityClient.TestSupport.dll");
        await RunBrowserColdChildAsync(Path.Combine(package, "host-guardian"), ["--live-turn", Path.Combine(own, "guardian.json"), "110000",
            Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"), support, "engine-browser-consumer-profile",
            typeof(GameEngineTurnLifecycleTests).Assembly.Location, package, Path.Combine(own, "result.json"), "0", own], Path.Combine(own, "probe.log"), 115);
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
        Assert.Null(result["OperationFailure"]);
        Assert.Null(result["CleanupFailure"]);
    }

    public static async Task WriteBrowserConsumerProfileProbeAsync(string package, string output, int mode, string own)
    {
        using var factory = new GameEngineTurnLifecycleTests();
        var launch = NeutralTerminalLaunch.Create(package, own);
        var root = Directory.GetParent(launch.Scratch)!.FullName;
        var watch = Stopwatch.StartNew();
        var observations = new ConcurrentQueue<object>();
        var reads = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var preflights = 0;
        var acquisitions = 0;
        var contention = 0;
        void Observe(string label) => observations.Enqueue(new { Label = label, Seconds = watch.Elapsed.TotalSeconds });
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                AfterBrowserOriginalPreflightAsync = () => { Interlocked.Increment(ref preflights); Observe("admission-preflight-completed"); return Task.CompletedTask; },
                AfterCanonicalWriteLockOpenedAsync = () => { Interlocked.Increment(ref acquisitions); return Task.CompletedTask; },
                CanonicalWriteLockContendedAsync = () => { Interlocked.Increment(ref contention); return Task.CompletedTask; },
                AfterCanonicalReadAttemptAsync = path => { reads.AddOrUpdate(path, 1, (_, n) => n + 1); Observe("read-attempt:" + path); return Task.CompletedTask; }
            });
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
        try
        {
            await Call("StartShellAsync");
            await ((TaskCompletionSource)type.GetField("_firstStatus", flags)!.GetValue(host)!).Task.WaitAsync(TimeSpan.FromSeconds(3));
            owner = (GmSessionRunCoordinator)type.GetField("_mainRun", flags)!.GetValue(host)!;
            var binding = await factory.QueueBrowserInputAsync(files);
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
                            WriteBrowserProfileSuccess(files, originalRequest);
                            Observe("authored-success-published");
                            published.TrySetResult();
                        }
                        return Task.CompletedTask;
                    }
                });
            var soul = JsonNode.Parse(File.ReadAllText(files.ResolvePath("game_state/meta/soul_state.json")))!;
            GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(soul["sessionId"]!.GetValue<string>(), 0);
            operation = InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", binding.Action, null, null, null, true, binding);
            await Task.WhenAny(published.Task, operation).WaitAsync(TimeSpan.FromSeconds(30));
            if (!published.Task.IsCompleted)
            {
                await operation;
                throw new InvalidOperationException("Original operation returned before authored terminal publication.");
            }
            await published.Task;
            var completed = await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(40)));
            deadline = completed != operation;
            Observe(deadline ? "profile-deadline-before-stop" : "operation-settled-before-stop");
            beforeStop = new
            {
                Record = owner.Record, Preflights = preflights, Acquisitions = acquisitions, Contention = contention,
                Reads = reads.ToArray(), Phase = File.Exists(files.ResolvePath(PendingPlayerActionService.PendingPath))
                    ? JsonNode.Parse(File.ReadAllText(files.ResolvePath(PendingPlayerActionService.PendingPath)))!["status"]!.GetValue<string>() : null,
                Observations = observations.ToArray(), OperationSettled = operation.IsCompleted, Deadline = deadline
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
                Accepted = accepted, OriginalStopped = stopped, PhysicalCleanup = physical, Deadline = deadline,
                BeforeStop = beforeStop, OperationFailure = operationFailure?.ToString(), CleanupFailure = cleanupFailure?.ToString(),
                Observations = observations.ToArray(), ModelCalls = 0,
                Scope = "Actual ordinary cancelled bootstrap, NativeLineage original Bridge and real ProcessPlayerTurn success consumer; fixture authors correlated response through existing checkpoint. Not Program/relay C5. Preflight count excludes additional recovery scans."
            }));
        }
    }

    private static void WriteBrowserProfileSuccess(FileSystemManager files, TurnRequest request)
    {
        var timestamp = DateTime.UtcNow.ToString("O");
        void Write(string path, object value) => File.WriteAllText(files.ResolvePath(path), JsonSerializer.Serialize(value));
        Write("output/narrative_response.json", new { response = "Исходное письмо прочитано.", timestamp });
        Write("output/interface_updates.json", new { dialogueOptions = Array.Empty<object>(), timestamp });
        Write("output/debug_logs.json", new { timestamp, gm_thoughts_markdown = "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: нет\n- Why relevant: Системное напоминание без действий акторов.\n- Actors outside scope: нет\n- Why outside scope: Структурных изменений акторов нет.\n\n## Reasoning\n- Structured actor reasoning is not required for this actor-free explanatory turn." });
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
            timestamp, status = "success", filesModified = new[] { "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json", ProgressionScheduleService.ReportPath } });
    }
}
