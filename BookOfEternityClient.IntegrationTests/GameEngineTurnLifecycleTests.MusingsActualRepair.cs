using System.Diagnostics;
using System.Reflection;
using System.Text;
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
    public async Task GuardianMusingsPublication_ActualRepairRevalidatesOriginalDeltaWithoutReplay()
    {
        var own = Path.Combine("/tmp", "gc-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(own);
        _directGachaOutput?.WriteLine("Owned actual staging cut evidence: " + own);
        var package = Path.Combine(own, "package");
        await RunBrowserProfileChildAsync("pwsh", ["-NoLogo", "-NoProfile", "-File",
            Path.Combine(TestRepoPaths.RepoRoot, "scripts/build-linux-supervisor.ps1"), "-OutputDirectory", package,
            "-IncludeHostGuardian", "-IncludeTerminalFixture"], Path.Combine(own, "native.log"), 25);
        var support = Path.Combine(Path.GetDirectoryName(typeof(GameEngineTurnLifecycleTests).Assembly.Location)!, "BookOfEternityClient.TestSupport.dll");
        await RunBrowserProfileChildAsync(Path.Combine(package, "host-guardian"), ["--live-turn", Path.Combine(own, "guardian.json"), "220000",
            Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"), support, "engine-musings-repair",
            typeof(GameEngineTurnLifecycleTests).Assembly.Location, package, Path.Combine(own, "result.json"), "0", own], Path.Combine(own, "probe.log"), 225);
        var guardian = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "guardian.json")))!;
        Assert.True(guardian["echild"]!.GetValue<bool>());
        Assert.Equal(0, guardian["driverExitCode"]!.GetValue<int>());
        Assert.Equal(0, guardian["failures"]!.GetValue<int>());
        Assert.Equal(0, guardian["emergencySignals"]!.GetValue<int>());
        Assert.False(guardian["deadline"]!.GetValue<bool>());
        var result = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "result.json")))!;
        Assert.True(result["OriginalStopped"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["PhysicalCleanup"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["OperationSettled"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["StopTaskSettled"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["ObserverSettled"]!.GetValue<bool>(), result.ToJsonString());
        Assert.Null(result["CleanupFailure"]);
        Assert.True(result["Accepted"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["FirstMutationInvalidatedCompletedScope"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["FreshComparisonObserved"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["RepairOnlyListedOutputs"]!.GetValue<bool>(), result.ToJsonString());
        Assert.Null(result["OperationFailure"]);
    }

    public static async Task WriteMusingsRepairProbeAsync(string package, string output, int mode, string own)
    {
        if (mode != 0) throw new ArgumentOutOfRangeException(nameof(mode));
        const string guardianPath = "game_state/meta/guardians.json";
        const string repairPath = "game_state/control/validation_repair_request.json";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using var factory = new GameEngineTurnLifecycleTests();
        var launch = NeutralTerminalLaunch.Create(package, own);
        ValidationService? validator = null;
        ValidationService.GuardianMusingsValidationScope? originalScope = null;
        object? mutationWitness = null;
        bool repaired = false, freshComparison = false, causalAuthorityRed = false, repairOnlyListedOutputs = false;
        string? originalGeneration = null;
        ValidationService.GuardianMusingsValidationScope? CurrentScope() => validator is null ? null :
            ((AsyncLocal<ValidationService.GuardianMusingsValidationScope?>)typeof(ValidationService)
                .GetField("_guardianMusingsPublication", flags)!.GetValue(validator)!).Value;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path =>
            {
                // Read inside the original mutation flow, before even the first repair deletion.
                // This callback is observational: no lease, waits, writes or manual invalidation.
                var scope = CurrentScope();
                var stack = new StackTrace().ToString();
                if (mutationWitness is null && originalScope is not null && (path is
                    "game_state/control/validation_repair_ready.json" or
                    "game_state/control/validation_repair_request.json" or
                    "game_state/control/gm_validation_repair_artifact_stall_report.json"))
                {
                    // Select the first repair-owned mutation independently of Enabled.
                    // A still-enabled/missing/replaced scope is retained and fails the oracle.
                    var completion = scope?.Completion;
                    var capture = completion?.GetType().GetField("_capture", flags)!.GetValue(completion);
                    var snapshot = capture is null ? null :
                        (PendingTurnSnapshotReadAuthority)capture.GetType().GetField("_snapshot", flags)!.GetValue(capture)!;
                    mutationWitness = new { Path = path, CompletionPresent = completion is not null,
                        Enabled = scope?.Enabled, ScopeReferenceMatches = ReferenceEquals(scope, originalScope),
                        Generation = capture?.GetType().GetField("_generation", flags)!.GetValue(capture),
                        SessionId = snapshot?.SessionId, RequestId = snapshot?.RequestId,
                        TurnNumber = snapshot?.TurnNumber, Stack = stack };
                }
                return Task.CompletedTask;
            },
            BeforeCanonicalReadOpenAsync = path =>
            {
                var scope = CurrentScope();
                if (path == guardianPath && scope?.Completion is not null && scope.Enabled &&
                    new StackTrace().ToString().Contains("ReadForComparisonAsync", StringComparison.Ordinal))
                {
                    if (!repaired) originalScope ??= scope;
                    else if (originalScope is not null && !ReferenceEquals(scope, originalScope) && !originalScope.Enabled)
                        freshComparison = true;
                }
                return Task.CompletedTask;
            }
        };
        var files = new FileSystemManager(Directory.GetParent(launch.Scratch)!.FullName,
            NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);
        files.EnsureDirectoryStructure();
        var bootstrap = factory.CreateGameEngine(new NewGameCancelInput(), settings =>
        { settings.MusicEnabled = false; settings.SoundEnabled = false; }, fileSystem: files);
        await GetPrivateField<StateManager>(bootstrap, "_stateManager").BootstrapLocalStorageAsync();
        var pendingGuardian = new SystemGuardianLibraryService(files, NullLogger<SystemGuardianLibraryService>.Instance)
            .BuildFreeformPendingGuardianCreationNode("Спокойный Хранитель берега Моря Хаоса.", "Пробная Душа");
        await InvokePrivateAsync<string>(bootstrap, "InitializeChaosSea", "Пробная Душа", "Человеческий силуэт синего света.", pendingGuardian, null);
        Assert.False(await InvokePrivateAsync<bool>(bootstrap, "WaitForGmResponse").WaitAsync(TimeSpan.FromSeconds(8)));
        var initial = JsonNode.Parse(File.ReadAllText(files.ResolvePath(guardianPath)))!.AsObject();
        var currentGuardian = initial["guardians"]![0]!;
        if (currentGuardian["musings"] is not JsonArray { Count: > 0 })
        {
            currentGuardian["musings"] = new JsonArray(new JsonObject { ["turn"] = 0, ["topic"] = "soul_assessment",
                ["mood"] = "intrigued", ["thought"] = "Я сохраню память о первой встрече с душой у берега." });
            initial["activeGuardian"]!["musings"] = currentGuardian["musings"]!.DeepClone();
            File.WriteAllText(files.ResolvePath(guardianPath), initial.ToJsonString());
        }
        var prefix = currentGuardian["musings"]!.DeepClone();
        File.WriteAllText(Path.Combine(own, "pre-turn-guardians.json"), initial.ToJsonString());
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var bridge = Assembly.LoadFrom(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityGMBridge", "bin", configuration, "net8.0", "BookOfEternityGMBridge.dll"));
        var type = bridge.GetType("BookOfEternityGMBridge.BridgeHost", true)!;
        var host = Activator.CreateInstance(type, [launch.Scratch, "musings-repair-" + Guid.NewGuid().ToString("N")])!;
        type.GetMethod("ConfigureNeutral", flags)!.Invoke(host, [launch]);
        async Task Call(string name) => await (Task)type.GetMethod(name, flags)!.Invoke(host, null)!;
        using var control = new CancellationTokenSource();
        using var observerCancellation = new CancellationTokenSource();
        var server = (Task)type.GetMethod("RunServerLoopAsync", flags)!.Invoke(host, [control.Token])!;
        var causalFailure = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var input = new QueuedConsoleInputSource([]);
        Task? operation = null, observer = null;
        Exception? operationFailure = null, cleanupFailure = null;
        GmSessionRunCoordinator? owner = null;
        TurnRequest? originalRequest = null;
        bool accepted = false, stopped = false, physical = false, firstInvalidated = false;
        int repairCount = 0;
        try
        {
            await Call("StartShellAsync");
            await ((TaskCompletionSource)type.GetField("_firstStatus", flags)!.GetValue(host)!).Task.WaitAsync(TimeSpan.FromSeconds(3));
            owner = (GmSessionRunCoordinator)type.GetField("_mainRun", flags)!.GetValue(host)!;
            var engine = factory.CreateGameEngine(input, fileSystem: files,
                configureSettings: settings => { settings.MusicEnabled = false; settings.SoundEnabled = false; },
                finalizationHooks: new GameEngineSessionFinalizationHooks
                {
                    AtCheckpointAsync = checkpoint =>
                    {
                        if (checkpoint == SessionFinalizationCheckpoint.TerminalWaitStarted)
                        {
                            originalRequest = JsonSerializer.Deserialize<TurnRequest>(File.ReadAllText(files.ResolvePath("input/turn_request.json")), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                            CaptureBrowserProfileFixture(files, own);
                            originalGeneration = JsonNode.Parse(File.ReadAllText(files.SessionGenerationPath))!["GenerationId"]!.GetValue<string>();
                            File.WriteAllBytes(Path.Combine(own, "original-generation.json"), File.ReadAllBytes(files.SessionGenerationPath));
                            WriteMusingsRepairPacket(files, originalRequest);
                        }
                        return Task.CompletedTask;
                    }
                });
            validator = GetPrivateField<ValidationService>(engine, "_validator");
            var originalLoop = GetPrivateField<GameLoop>(bootstrap, "_gameLoop");
            GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(originalLoop.SessionId, originalLoop.TurnNumber);
            observer = Task.Run(async () =>
            {
                try
                {
                    int lastAttempt = 0;
                    while (!observerCancellation.IsCancellationRequested)
                    {
                        if (File.Exists(files.ResolvePath(repairPath)))
                        {
                            var raw = File.ReadAllBytes(files.ResolvePath(repairPath));
                            // Preserve the exact first production bytes even if interpretation fails.
                            var observedPath = Path.Combine(own, "first-observed-repair-request.bin");
                            if (!File.Exists(observedPath)) File.WriteAllBytes(observedPath, raw);
                            var request = JsonNode.Parse(Encoding.UTF8.GetString(raw).TrimStart('\uFEFF'))!;
                            var attempt = request["revalidationAttempt"]!.GetValue<int>();
                            if (attempt > lastAttempt)
                            {
                                lastAttempt = attempt; repairCount++;
                                File.WriteAllBytes(Path.Combine(own, $"repair-request-{attempt}.json"), raw);
                                File.WriteAllBytes(Path.Combine(own, $"guardians-before-repair-{attempt}.json"), File.ReadAllBytes(files.ResolvePath(guardianPath)));
                                Assert.NotNull(originalRequest);
                                Assert.Equal(originalRequest.SessionId, request["sessionId"]!.GetValue<string>());
                                Assert.Equal(originalRequest.RequestId, request["requestId"]!.GetValue<string>());
                                Assert.Equal(originalRequest.TurnNumber, request["turnNumber"]!.GetValue<int>());
                                var codes = request["errors"]!.AsArray().Select(row => row!["code"]!.GetValue<string>()).ToArray();
                                if (attempt != 1)
                                {
                                    causalAuthorityRed = codes.Contains("guardian_materialized_state_outside_authority", StringComparer.Ordinal);
                                    throw new InvalidOperationException((causalAuthorityRed ? "CAUSAL AUTHORITY RED" : "UNQUALIFIED SECOND REPAIR") +
                                        ": actual second repair after original invalidation and narrative-only resubmission: " + request.ToJsonString());
                                }
                                Assert.Equal(new[] { "accepted_turn_empty_narrative_response" }, codes);
                                var packets = request["harnessRepairPackets"]!.AsArray();
                                Assert.Single(packets);
                                Assert.Equal(new[] { "output/narrative_response.json" },
                                    packets[0]!["targetFiles"]!.AsArray().Select(path => path!.GetValue<string>()).ToArray());
                                var unlistedOutputs = new[] { "output/interface_updates.json", "output/debug_logs.json" }
                                    .ToDictionary(path => path, path => File.ReadAllBytes(files.ResolvePath(path)));
                                foreach (var pair in unlistedOutputs)
                                    File.WriteAllBytes(Path.Combine(own, Path.GetFileName(pair.Key) + ".before-repair.bin"), pair.Value);
                                Assert.NotNull(mutationWitness);
                                File.WriteAllText(Path.Combine(own, "first-repair-mutation.json"), JsonSerializer.Serialize(mutationWitness));
                                var witness = JsonSerializer.SerializeToNode(mutationWitness)!;
                                Assert.Equal(originalRequest.SessionId, witness["SessionId"]!.GetValue<string>());
                                Assert.Equal(originalRequest.RequestId, witness["RequestId"]!.GetValue<string>());
                                Assert.Equal(originalRequest.TurnNumber, witness["TurnNumber"]!.GetValue<int>());
                                Assert.Equal(originalGeneration, witness["Generation"]!.GetValue<string>());
                                Assert.True(witness["ScopeReferenceMatches"]!.GetValue<bool>());
                                Assert.True(witness["CompletionPresent"]!.GetValue<bool>());
                                Assert.False(witness["Enabled"]!.GetValue<bool>());
                                firstInvalidated = true;
                                var stamp = DateTime.UtcNow.ToString("O");
                                File.WriteAllText(files.ResolvePath("output/narrative_response.json"), JsonSerializer.Serialize(new { response = "Исходное письмо прочитано.", timestamp = stamp }));
                                repaired = true;
                                File.WriteAllText(files.ResolvePath("game_state/control/validation_repair_ready.json"), JsonSerializer.Serialize(new
                                { sessionId = originalRequest.SessionId, requestId = originalRequest.RequestId, turnNumber = originalRequest.TurnNumber,
                                    updatedAtUtc = stamp, status = "success", note = "Исправлен тип текста рассказа; состояние Хранителя не изменено." }));
                                foreach (var pair in unlistedOutputs)
                                {
                                    var after = File.ReadAllBytes(files.ResolvePath(pair.Key));
                                    File.WriteAllBytes(Path.Combine(own, Path.GetFileName(pair.Key) + ".after-repair.bin"), after);
                                    Assert.Equal(pair.Value, after);
                                }
                                repairOnlyListedOutputs = true;
                            }
                        }
                        await Task.Delay(25, observerCancellation.Token);
                    }
                }
                catch (OperationCanceledException) when (observerCancellation.IsCancellationRequested) { }
                catch (Exception failure) { causalFailure.TrySetResult(failure); }
            });
            operation = InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Я читаю исходное письмо.", null);
            var completed = await Task.WhenAny(operation, causalFailure.Task, Task.Delay(TimeSpan.FromSeconds(120)));
            if (completed == causalFailure.Task) throw await causalFailure.Task;
            if (completed != operation) throw new TimeoutException("Actual repair operation exceeded protective 120s bound; no causal authority result claimed.");
            await operation;
            var published = JsonNode.Parse(File.ReadAllText(files.ResolvePath(guardianPath)))!;
            var after = published["guardians"]![0]!["musings"]!.AsArray();
            Assert.Equal(prefix.AsArray().Count + 1, after.Count);
            for (var i = 0; i < prefix.AsArray().Count; i++) Assert.True(JsonNode.DeepEquals(prefix[i], after[i]));
            Assert.True(JsonNode.DeepEquals(after, published["activeGuardian"]!["musings"]));
            Assert.False(published["UpdateGuardians"]?.AsArray().OfType<JsonObject>().Any(row => row["command"]?.GetValue<string>() == "addMusings") ?? false);
            var story = File.ReadAllLines(files.ResolvePath("stories/chaos_sea.jsonl")).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            File.WriteAllBytes(Path.Combine(own, "accepted-story.jsonl"), File.ReadAllBytes(files.ResolvePath("stories/chaos_sea.jsonl")));
            Assert.Single(story);
            var entry = JsonNode.Parse(story[0])!;
            Assert.Equal(originalRequest!.TurnNumber, entry["turn"]!.GetValue<int>());
            Assert.Equal(originalRequest.PlayerAction, entry["player"]!.GetValue<string>());
            Assert.Equal("Исходное письмо прочитано.", entry["narrative"]!.GetValue<string>());
            Assert.False(File.Exists(files.ResolvePath("input/turn_request.json")));
            Assert.Equal(1, repairCount);
            Assert.False(originalScope!.Enabled);
            accepted = true;
        }
        catch (Exception failure) { operationFailure = failure; }
        finally
        {
            // Retain the primary causal request/canonical bytes before actual cancellation/rollback.
            await File.WriteAllTextAsync(Path.Combine(own, "failure-before-cleanup.txt"), operationFailure?.ToString() ?? "none");
            await observerCancellation.CancelAsync();
            if (observer is not null) try { await observer.WaitAsync(TimeSpan.FromSeconds(3)); } catch (Exception failure) { cleanupFailure = failure; }
            if (operation is { IsCompleted: false }) input.Enqueue(Key(ConsoleKey.Escape));
            if (operation is not null) try { await operation.WaitAsync(TimeSpan.FromSeconds(35)); } catch (Exception failure) { operationFailure ??= failure; }
            var stop = Call("StopShellAsync");
            try { await stop.WaitAsync(TimeSpan.FromSeconds(15)); stopped = owner?.Record?.Disposition == GmSessionRunDisposition.Stopped; }
            catch (Exception failure) { cleanupFailure ??= failure; }
            try
            {
                if (type.GetField("_pty", flags)!.GetValue(host) is IOwnedTerminalSession terminal)
                    physical = (await terminal.StopAndObserveAsync(CancellationToken.None)).CleanupComplete;
                else physical = stopped;
            }
            catch (Exception failure) { cleanupFailure ??= failure; }
            await control.CancelAsync();
            try { await server.WaitAsync(TimeSpan.FromSeconds(5)); ((IDisposable)host).Dispose(); }
            catch (Exception failure) { cleanupFailure ??= failure; }
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { Accepted = accepted,
                FirstMutationInvalidatedCompletedScope = firstInvalidated, FreshComparisonObserved = freshComparison,
                CausalAuthorityRed = causalAuthorityRed, RepairCount = repairCount, OriginalStopped = stopped, PhysicalCleanup = physical,
                RepairOnlyListedOutputs = repairOnlyListedOutputs,
                StopTaskSettled = stop.IsCompleted, OperationSettled = operation?.IsCompleted == true, ObserverSettled = observer?.IsCompleted == true,
                OperationFailure = operationFailure?.ToString(), CleanupFailure = cleanupFailure?.ToString(),
                MutationWitness = mutationWitness, ModelCalls = 0,
                Scope = "Actual ordinary console ProcessPlayerTurn with original NativeBridge; fixture authors model data only. Not Program/relay/browser/crash qualification." }));
        }
    }
    private static void WriteMusingsRepairPacket(FileSystemManager files, TurnRequest request)
    {
        var timestamp = DateTime.UtcNow.ToString("O");
        void Write(string path, object value) => File.WriteAllText(files.ResolvePath(path), JsonSerializer.Serialize(value));
        var guardians = JsonNode.Parse(File.ReadAllText(files.ResolvePath("game_state/meta/guardians.json")))!;
        var guardian = guardians["guardians"]![0]!;
        var actor = guardian["canonicalName"]!.GetValue<string>();
        guardians["UpdateGuardians"] = JsonSerializer.SerializeToNode(new[] { new
        {
            command = "addMusings", guardianId = guardian["guardianId"]!.GetValue<string>(),
            musings = new[] { new { turn = request.TurnNumber, topic = "soul_assessment", mood = "intrigued",
                text = "Я запомню самостоятельный выбор души прочитать письмо у берега." } }
        }});
        File.WriteAllText(files.ResolvePath("game_state/meta/guardians.json"), guardians.ToJsonString());
        const string thought = "Я запомню самостоятельный выбор души прочитать письмо у берега.";
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
            "- State changes: UpdateGuardians.addMusings в game_state/meta/guardians.json: одна новая first-person запись; полный старый префикс и activeGuardian сохраняются.",
            "- Детерминированная тестовая заготовка; провайдер не вызван.");
        Write("output/narrative_response.json", new { response = 123, timestamp });
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
            timestamp, status = "success", filesModified = new[] { "game_state/meta/guardians.json", "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json", ProgressionScheduleService.ReportPath } });
    }
}
