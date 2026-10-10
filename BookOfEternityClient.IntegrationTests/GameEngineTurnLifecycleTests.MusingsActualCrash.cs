using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
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
    public Task GuardianMusingsPublication_ActualCaptureCrashBlocksColdConsoleWithoutReplay() => RunActualMusingsCrashAsync(0);

    [Fact]
    public Task GuardianMusingsPublication_ActualNormalizedCrashBlocksColdConsoleWithoutReplay() => RunActualMusingsCrashAsync(1);

    [Fact]
    public Task GuardianMusingsPublication_ActualAcceptedCleanupCrashBlocksColdConsoleWithoutReplay() => RunActualMusingsCrashAsync(2);

    private async Task RunActualMusingsCrashAsync(int cut)
    {
        var own = Path.Combine("/tmp", "gc-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(own);
        _directGachaOutput?.WriteLine("Owned actual staging cut evidence: " + own);
        var package = Path.Combine(own, "package");
        await RunBrowserProfileChildAsync("pwsh", ["-NoLogo", "-NoProfile", "-File",
            Path.Combine(TestRepoPaths.RepoRoot, "scripts/build-linux-supervisor.ps1"), "-OutputDirectory", package,
            "-IncludeHostGuardian", "-IncludeTerminalFixture"], Path.Combine(own, "native.log"), 25);
        var support = Path.Combine(Path.GetDirectoryName(typeof(GameEngineTurnLifecycleTests).Assembly.Location)!, "BookOfEternityClient.TestSupport.dll");
        await RunBrowserProfileChildAsync(Path.Combine(package, "host-guardian"), ["--live-turn", Path.Combine(own, "guardian.json"), "240000",
            Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"), support, "engine-musings-cut",
            typeof(GameEngineTurnLifecycleTests).Assembly.Location, package, Path.Combine(own, "result.json"), cut.ToString(), own],
            Path.Combine(own, "probe.log"), 245);
        var guardian = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "guardian.json")))!;
        Assert.True(guardian["echild"]!.GetValue<bool>());
        Assert.Equal(0, guardian["driverExitCode"]!.GetValue<int>());
        Assert.Equal(0, guardian["failures"]!.GetValue<int>());
        Assert.Equal(0, guardian["emergencySignals"]!.GetValue<int>());
        Assert.False(guardian["deadline"]!.GetValue<bool>());
        var result = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "result.json")))!;
        Assert.True(result["ChildrenSettled"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["StopTaskSettled"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["ServerSettled"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["DisposeTaskSettled"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["PhysicalCleanup"]!.GetValue<bool>(), result.ToJsonString());
        Assert.Null(result["CleanupFailure"]);
        Assert.True(result["Success"]!.GetValue<bool>(), result.ToJsonString());
        Assert.Equal(137, result["OriginalExitCode"]!.GetValue<int>());
        Assert.True(result["OriginalEOF"]!.GetValue<bool>());
        Assert.True(result["OriginalPinUnresolved"]!.GetValue<bool>());
        Assert.True(result["LogicalUncertainRetained"]!.GetValue<bool>());
        Assert.True(result["ExactEvidenceRetained"]!.GetValue<bool>());
        Assert.True(result["ExactCutEvidenceRetained"]!.GetValue<bool>());
        Assert.True(result["ColdNativeAdmissionRefused"]!.GetValue<bool>());
        Assert.Null(result["Failure"]);
    }

    // This parent owns the real Bridge. Killing its separate GameEngine child
    // leaves the original terminal, coordinator and unresolved pin alive for cold classification.
    public static async Task WriteMusingsCutProbeAsync(string package, string output, int cut, string own)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using var factory = new GameEngineTurnLifecycleTests();
        var launch = NeutralTerminalLaunch.Create(package, own);
        var files = new FileSystemManager(Directory.GetParent(launch.Scratch)!.FullName, NullLogger<FileSystemManager>.Instance);
        files.EnsureDirectoryStructure();
        var bootstrap = factory.CreateGameEngine(new NewGameCancelInput(), settings =>
        { settings.MusicEnabled = false; settings.SoundEnabled = false; }, fileSystem: files);
        await GetPrivateField<StateManager>(bootstrap, "_stateManager").BootstrapLocalStorageAsync();
        var pending = new SystemGuardianLibraryService(files, NullLogger<SystemGuardianLibraryService>.Instance)
            .BuildFreeformPendingGuardianCreationNode("Спокойный Хранитель берега Моря Хаоса.", "Пробная Душа");
        await InvokePrivateAsync<string>(bootstrap, "InitializeChaosSea", "Пробная Душа", "Человеческий силуэт синего света.", pending, null);
        Assert.False(await InvokePrivateAsync<bool>(bootstrap, "WaitForGmResponse").WaitAsync(TimeSpan.FromSeconds(8)));
        var initial = JsonNode.Parse(File.ReadAllText(files.ResolvePath("game_state/meta/guardians.json")))!;
        if (initial["guardians"]![0]!["musings"] is not JsonArray { Count: > 0 })
        {
            initial["guardians"]![0]!["musings"] = new JsonArray(new JsonObject { ["turn"] = 0, ["topic"] = "soul_assessment",
                ["mood"] = "intrigued", ["thought"] = "Я сохраню память о первой встрече с душой у берега." });
            initial["activeGuardian"]!["musings"] = initial["guardians"]![0]!["musings"]!.DeepClone();
            File.WriteAllText(files.ResolvePath("game_state/meta/guardians.json"), initial.ToJsonString());
        }
        File.WriteAllText(Path.Combine(own, "pre-turn-guardians.json"), initial.ToJsonString());
        var loop = GetPrivateField<GameLoop>(bootstrap, "_gameLoop");
        var nonce = Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(own, "cut-control.json"), JsonSerializer.Serialize(new
        { Nonce = nonce, ParentPid = Environment.ProcessId, SessionId = loop.SessionId, TurnNumber = loop.TurnNumber, Cut = cut, Root = files.BasePath }));
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var bridge = Assembly.LoadFrom(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityGMBridge", "bin", configuration, "net8.0", "BookOfEternityGMBridge.dll"));
        var type = bridge.GetType("BookOfEternityGMBridge.BridgeHost", true)!;
        var host = Activator.CreateInstance(type, [launch.Scratch, "musings-cut-" + nonce])!;
        type.GetMethod("ConfigureNeutral", flags)!.Invoke(host, [launch]);
        async Task Call(string name) => await (Task)type.GetMethod(name, flags)!.Invoke(host, null)!;
        using var control = new CancellationTokenSource();
        var server = (Task)type.GetMethod("RunServerLoopAsync", flags)!.Invoke(host, [control.Token])!;
        GmSessionRunCoordinator? owner = null;
        IOwnedTerminalSession? terminal = null;
        Process? original = null, cold = null;
        Task<string>? originalOut = null, originalError = null, coldOut = null, coldError = null;
        Task? stop = null, dispose = null;
        Exception? failure = null, cleanupFailure = null;
        string? logicalStopFailure = null;
        MainOperationClose? identity = null;
        bool success = false, originalEof = false, unresolved = false, exact = false, exactCut = false, coldRefused = false, physical = false;
        JsonNode? cutEvidence = null, coldEvidence = null;
        Dictionary<string, string>? before = null, after = null;
        try
        {
            await Call("StartShellAsync");
            await ((TaskCompletionSource)type.GetField("_firstStatus", flags)!.GetValue(host)!).Task.WaitAsync(TimeSpan.FromSeconds(3));
            owner = (GmSessionRunCoordinator)type.GetField("_mainRun", flags)!.GetValue(host)!;
            terminal = (IOwnedTerminalSession)type.GetField("_pty", flags)!.GetValue(host)!;
            original = StartMusingsCutChild("engine-musings-original-cut", files.BasePath, Path.Combine(own, "original-cut.json"), cut, own);
            originalOut = original.StandardOutput.ReadToEndAsync(); originalError = original.StandardError.ReadToEndAsync();
            using var cutDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            while (!File.Exists(Path.Combine(own, "original-cut.json")))
            {
                if (File.Exists(Path.Combine(own, "fixture-preparation-failure.txt")))
                    throw new InvalidOperationException("PREPARATION: " + File.ReadAllText(Path.Combine(own, "fixture-preparation-failure.txt")));
                if (original.HasExited) throw new InvalidOperationException("PREPARATION: original engine exited before intended cut: " + await originalError);
                await Task.Delay(10, cutDeadline.Token);
            }
            cutEvidence = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "original-cut.json")))!;
            Assert.Equal(original.Id, cutEvidence["Pid"]!.GetValue<int>());
            Assert.Equal(nonce, cutEvidence["Nonce"]!.GetValue<string>());
            Assert.Equal(cut, cutEvidence["Cut"]!.GetValue<int>());
            Assert.True(cutEvidence["QualifiedBoundary"]!.GetValue<bool>());
            Assert.Equal(loop.SessionId, cutEvidence["SessionId"]!.GetValue<string>());
            Assert.Equal(loop.TurnNumber + 1, cutEvidence["TurnNumber"]!.GetValue<int>());
            Assert.Equal(owner.Identity.GenerationId, cutEvidence["Generation"]!.GetValue<string>());
            identity = cutEvidence["PinIdentity"]!.Deserialize<MainOperationClose>()!;
            var active = owner.QueryRemoteOperation(identity);
            Assert.Equal(MainOperationState.Active, active.State);
            Assert.False(terminal.RootExited.IsCompleted);
            File.WriteAllText(Path.Combine(own, "pin-before-kill.json"), JsonSerializer.Serialize(active));
            original.Kill();
            await original.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await Task.WhenAll(originalOut, originalError).WaitAsync(TimeSpan.FromSeconds(5));
            originalEof = true;
            Assert.Equal(137, original.ExitCode);
            File.WriteAllText(Path.Combine(own, "original.log"), await originalOut + await originalError);
            using var lostDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (owner.QueryRemoteOperation(identity).State != MainOperationState.Unresolved)
                await Task.Delay(10, lostDeadline.Token);
            unresolved = owner.IsUncertain && owner.QueryRemoteOperation(identity).State == MainOperationState.Unresolved;
            Assert.True(unresolved);
            Assert.False(terminal.RootExited.IsCompleted);
            File.WriteAllText(Path.Combine(own, "pin-after-eof.json"), JsonSerializer.Serialize(owner.QueryRemoteOperation(identity)));
            // Ownership metadata may change after EOF. Freeze the canonical inventory
            // now, separately from the original cut-time ACK and before cold execution.
            before = MusingsCutInventory(files);
            var atCut = cutEvidence["Inventory"]!.Deserialize<Dictionary<string, string>>()!;
            exactCut = atCut.Count == before.Count && atCut.All(pair => before.TryGetValue(pair.Key, out var value) && pair.Value == value);
            Assert.True(exactCut);
            RetainMusingsCutFiles(files, own, "pre-cold");
            cold = StartMusingsCutChild("engine-musings-cold", files.BasePath, Path.Combine(own, "cold.json"), cut, own);
            coldOut = cold.StandardOutput.ReadToEndAsync(); coldError = cold.StandardError.ReadToEndAsync();
            await cold.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            await Task.WhenAll(coldOut, coldError).WaitAsync(TimeSpan.FromSeconds(5));
            File.WriteAllText(Path.Combine(own, "cold.log"), await coldOut + await coldError);
            Assert.Equal(0, cold.ExitCode);
            coldEvidence = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "cold.json")))!;
            Assert.Equal(cold.Id, coldEvidence["Pid"]!.GetValue<int>());
            Assert.True(coldEvidence["NativeAdmissionRefused"]!.GetValue<bool>(), coldEvidence.ToJsonString());
            Assert.True(coldEvidence["OperationStarted"]!.GetValue<bool>());
            Assert.Equal(0, coldEvidence["CanonicalOpens"]!.GetValue<int>());
            Assert.Equal(0, coldEvidence["RecoveryCallbacks"]!.GetValue<int>());
            Assert.Equal(0, coldEvidence["Mutations"]!.GetValue<int>());
            coldRefused = true;
            after = MusingsCutInventory(files);
            exact = before.Count == after.Count && before.All(pair => after.TryGetValue(pair.Key, out var value) && pair.Value == value);
            Assert.True(exact);
            Assert.Equal(MainOperationState.Unresolved, owner.QueryRemoteOperation(identity).State);
            Assert.False(terminal.RootExited.IsCompleted);
            success = true;
        }
        catch (Exception primary) { failure = primary; }
        finally
        {
            File.WriteAllText(Path.Combine(own, "failure-before-cleanup.txt"), failure?.ToString() ?? "none");
            foreach (var child in new[] { original, cold })
            {
                if (child is null) continue;
                try { if (!child.HasExited) child.Kill(); await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception cleanup) { cleanupFailure ??= cleanup; }
            }
            foreach (var io in new[] { originalOut, originalError, coldOut, coldError })
                if (io is not null) try { await io.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception cleanup) { cleanupFailure ??= cleanup; }
            stop = Call("StopShellAsync");
            try { await stop.WaitAsync(TimeSpan.FromSeconds(15)); }
            catch (Exception logical)
            {
                // An unresolved original pin must not be retired by physical stop.
                if (stop.IsCompleted && owner?.IsUncertain == true) logicalStopFailure = logical.ToString();
                else cleanupFailure ??= logical;
            }
            if (terminal is not null)
                try { physical = (await terminal.StopAndObserveAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).CleanupComplete; }
                catch (Exception cleanup) { cleanupFailure ??= cleanup; }
            await control.CancelAsync();
            try { await server.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception cleanup) { cleanupFailure ??= cleanup; }
            dispose = Task.Run(() => ((IDisposable)host).Dispose());
            try { await dispose.WaitAsync(TimeSpan.FromSeconds(20)); } catch (Exception cleanup) { cleanupFailure ??= cleanup; }
            var logicalRetained = owner?.IsUncertain == true && owner.RetainsAuthority && identity is not null &&
                owner.QueryRemoteOperation(identity).State == MainOperationState.Unresolved;
            File.WriteAllText(output, JsonSerializer.Serialize(new
            {
                Success = success, Cut = cut, Failure = failure?.ToString(), CleanupFailure = cleanupFailure?.ToString(),
                OriginalPid = original?.Id, OriginalExitCode = original?.HasExited == true ? original.ExitCode : (int?)null,
                OriginalEOF = originalEof, OriginalPinUnresolved = unresolved, LogicalUncertainRetained = logicalRetained,
                LogicalStopFailure = logicalStopFailure, PhysicalCleanup = physical,
                ChildrenSettled = (original is null || original.HasExited) && (cold is null || cold.HasExited) &&
                    new[] { originalOut, originalError, coldOut, coldError }.All(task => task is null || task.IsCompleted),
                StopTaskSettled = stop.IsCompleted, ServerSettled = server.IsCompleted, DisposeTaskSettled = dispose.IsCompleted,
                ColdNativeAdmissionRefused = coldRefused, ExactEvidenceRetained = exact, ExactCutEvidenceRetained = exactCut,
                BeforeCold = before, AfterCold = after,
                CutEvidence = cutEvidence, ColdEvidence = coldEvidence, OriginalRecordAfterStop = owner?.Record, ModelCalls = 0,
                Scope = "Actual ordinary console engine process SIGKILL with original NativeBridge alive; same original pin Active -> Unresolved, cold ordinary native-admission refusal. No automatic resume, Program/relay/browser/Windows/live-model or whole T070 acceptance."
            }));
            original?.Dispose(); cold?.Dispose();
        }
    }

    public static async Task RunOriginalMusingsCutAsync(string root, string ack, int cut, string own)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        using var factory = new GameEngineTurnLifecycleTests();
        var control = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "cut-control.json")))!;
        Assert.Equal(cut, control["Cut"]!.GetValue<int>());
        Task? operation = null;
        ValidationService? validator = null;
        ValidationService.GuardianMusingsPublicationCapture.GuardianMusingsCompletedValidation? observedCompletion = null;
        FileSystemManager? files = null;
        bool packetWritten = false, gateClaimed = false;
        int gateObservations = 0;
        TurnRequest? request = null;
        Dictionary<string, byte[]>? originalRollbackBytes = null;
        MainOperationClose DescribeOriginalPin()
        {
            var admissions = (AsyncLocal<FileSystemManager.MainAdmission?>)typeof(FileSystemManager).GetField("MainAdmissions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            var frame = admissions.Value;
            while (frame is not null && !frame.OwnsRemote) frame = frame.Parent;
            Assert.NotNull(frame);
            Assert.False(frame.CloseObserved);
            Assert.Null(frame.TerminalClose);
            return frame.DescribeClose(MainOperationOutcome.Completed, false)!;
        }
        async Task HoldBoundaryAsync(string path, object capture, FileSystemManager.CanonicalWriteLease? lease, string stateType, string? stack = null)
        {
            Assert.NotNull(request);
            var snapshot = (PendingTurnSnapshotReadAuthority)capture.GetType().GetField("_snapshot", flags)!.GetValue(capture)!;
            var generation = (string)capture.GetType().GetField("_generation", flags)!.GetValue(capture)!;
            Assert.Equal(request.SessionId, snapshot.SessionId); Assert.Equal(request.RequestId, snapshot.RequestId);
            Assert.Equal(request.TurnNumber, snapshot.TurnNumber);
            Assert.Equal(generation, JsonNode.Parse(File.ReadAllText(files!.SessionGenerationPath))!["GenerationId"]!.GetValue<string>());
            if (lease is not null) { Assert.True(lease.IsActive); Assert.Same(files, lease.Owner); }
            var before = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "pre-turn-guardians.json")))!;
            var currentBytes = File.ReadAllBytes(files.ResolvePath("game_state/meta/guardians.json"));
            var current = JsonNode.Parse(Encoding.UTF8.GetString(currentBytes).TrimStart('\uFEFF'))!;
            var old = before["guardians"]![0]!["musings"]!.AsArray();
            var actual = current["guardians"]![0]!["musings"]!.AsArray();
            Assert.Equal(old.Count + (cut == 0 ? 0 : 1), actual.Count);
            for (var i = 0; i < old.Count; i++) Assert.True(JsonNode.DeepEquals(old[i], actual[i]));
            Assert.True(JsonNode.DeepEquals(actual, current["activeGuardian"]!["musings"]));
            if (cut == 0) Assert.Equal(File.ReadAllBytes(Path.Combine(own, "original-packet-guardians.bin")), currentBytes);
            else
            {
                Assert.False(current["UpdateGuardians"]?.AsArray().OfType<JsonObject>().Any(row => row["command"]?.GetValue<string>() == "addMusings") ?? false);
                Assert.Equal(request.TurnNumber, actual[^1]!["turn"]!.GetValue<int>());
                Assert.Equal("Я запомню самостоятельный выбор души прочитать письмо у берега.", actual[^1]!["thought"]!.GetValue<string>());
            }
            var storyPath = files.ResolvePath("stories/chaos_sea.jsonl");
            var story = File.Exists(storyPath) ? File.ReadAllLines(storyPath).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray() : [];
            Assert.Equal(cut == 2 ? 1 : 0, story.Length);
            if (cut == 2)
            {
                Assert.NotNull(observedCompletion);
                Assert.Equal((string)observedCompletion.GetType().GetField("_publishedHash", flags)!.GetValue(observedCompletion)!,
                    Convert.ToHexString(SHA256.HashData(currentBytes)), ignoreCase: true);
                var entry = JsonNode.Parse(story[0])!;
                Assert.Equal(request.TurnNumber, entry["turn"]!.GetValue<int>());
                Assert.Equal(request.PlayerAction, entry["player"]!.GetValue<string>());
                Assert.Equal("Исходное письмо прочитано.", entry["narrative"]!.GetValue<string>());
                Assert.True(File.Exists(files.ResolvePath(path)));
                Assert.NotNull(originalRollbackBytes);
                foreach (var pair in originalRollbackBytes)
                {
                    Assert.True(File.Exists(files.ResolvePath(pair.Key)), "Original rollback path disappeared before first cleanup mutation: " + pair.Key);
                    Assert.Equal(pair.Value, File.ReadAllBytes(files.ResolvePath(pair.Key)));
                }
            }
            RetainMusingsCutFiles(files, own, "at-cut");
            File.WriteAllText(ack + ".tmp", JsonSerializer.Serialize(new
            {
                Pid = Environment.ProcessId, Nonce = control["Nonce"]!.GetValue<string>(), Cut = cut,
                Path = path, StateType = stateType, StackDiagnosticOnly = stack, QualifiedBoundary = true,
                CaptureReturned = cut == 0, NormalizationReturned = cut == 1,
                OriginalCompletedProofObserved = cut == 2, BeforeFirstCleanupMutation = cut == 2,
                HeldOriginalPublicationLease = lease?.IsActive == true, SessionId = snapshot.SessionId,
                RequestId = snapshot.RequestId, TurnNumber = snapshot.TurnNumber, Generation = generation,
                PinIdentity = DescribeOriginalPin(), PinIdentityOnlyNotObservedClose = true,
                GuardianBytes = currentBytes.Length, GuardianSHA256 = Convert.ToHexString(SHA256.HashData(currentBytes)),
                StoryEntries = story.Length, Inventory = MusingsCutInventory(files)
            }));
            File.Move(ack + ".tmp", ack);
            await Task.Delay(TimeSpan.FromSeconds(180));
            throw new TimeoutException("PREPARATION: original intended cut was not SIGKILLed while held.");
        }
        Task ObserveReadAsync(string path)
        {
            if (validator is not null)
            {
                var scope = ((AsyncLocal<ValidationService.GuardianMusingsValidationScope?>)typeof(ValidationService)
                    .GetField("_guardianMusingsPublication", flags)!.GetValue(validator)!).Value;
                if (path == "game_state/meta/guardians.json" && scope is { Enabled: true, Completion: not null })
                    observedCompletion = scope.Completion; // Retain only the actual earlier immutable object.
            }
            if (!packetWritten || cut == 2 || gateClaimed || path != "game_state/meta/guardians.json") return Task.CompletedTask;
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var observation = Interlocked.Increment(ref gateObservations);
            var hookStack = new StackTrace().ToString();
            _ = Task.Run(async () =>
            {
                try
                {
                    // Observe this exact gate in the original task's actual await
                    // chain before reading state or releasing an irrelevant read.
                    var registration = Stopwatch.StartNew();
                    (List<object> States, bool GateReachable, object Diagnostic) actualChain;
                    do
                    {
                        actualChain = operation is null ? ([], false, new { OperationAssigned = false }) : ReadActualMusingsStateMachines(operation, gate.Task);
                        if (!actualChain.GateReachable) await Task.Delay(1);
                    } while (!actualChain.GateReachable && registration.Elapsed < TimeSpan.FromSeconds(2));
                    var states = actualChain.States;
                    var normal = states.SingleOrDefault(state => state.GetType().FullName!.Contains("AcceptedTurnCanonicalStateRefresh+<NormalizeAndValidateWithPlanAsync>", StringComparison.Ordinal));
                    var capture = normal?.GetType().GetFields(flags).Where(field => field.FieldType == typeof(ValidationService.GuardianMusingsPublicationCapture))
                        .Select(field => field.GetValue(normal)).SingleOrDefault(value => value is not null);
                    var lease = FindActualMusingsPublicationLease(normal);
                    var complete = states.SingleOrDefault(state => state.GetType().FullName!.Contains("GuardianMusingsPublicationCapture+<CompleteAsync>", StringComparison.Ordinal));
                    var beforeImages = states.Any(state => state.GetType().FullName!.Contains("AcceptedTurnCanonicalStateRefresh+<CaptureBeforeImagesAsync>", StringComparison.Ordinal));
                    var intended = capture is not null && lease is not null && (cut == 0 ? beforeImages && complete is null : complete is not null);
                    File.WriteAllText(Path.Combine(own, $"read-gate-{observation:D3}.json"), JsonSerializer.Serialize(new
                    {
                        Path = path, HookStack = hookStack, Cut = cut, actualChain.GateReachable, Intended = intended,
                        CaptureType = capture?.GetType().FullName, LeaseActive = lease?.IsActive == true,
                        SameFileSystem = lease is not null && ReferenceEquals(lease.Owner, files), BeforeImages = beforeImages,
                        CompleteState = complete?.GetType().FullName, RegistrationMilliseconds = registration.Elapsed.TotalMilliseconds,
                        actualChain.Diagnostic
                    }));
                    if (!actualChain.GateReachable) throw new InvalidOperationException("PREPARATION: exact read gate registration was not observed in the original engine await chain.");
                    if (!intended) { gate.TrySetResult(); return; }
                    if (cut == 0) Assert.DoesNotContain(states, state => state.GetType().FullName!.Contains("<NormalizeAccumulatedState", StringComparison.Ordinal));
                    else Assert.Same(capture, complete!.GetType().GetField("<>4__this", flags | BindingFlags.Public)!.GetValue(complete));
                    gateClaimed = true;
                    await HoldBoundaryAsync(path, capture!, lease, normal!.GetType().FullName! + "/" + (cut == 0 ? "CaptureBeforeImagesAsync" : "CompleteAsync"));
                }
                catch (Exception preparation)
                {
                    gateClaimed = true;
                    File.WriteAllText(Path.Combine(own, "fixture-preparation-failure.txt"), preparation.ToString());
                    // Preserve the boundary; the parent notices this preparation
                    // failure and kills only its child before any rollback/mutation.
                    await Task.Delay(TimeSpan.FromSeconds(180));
                    gate.TrySetException(preparation);
                }
            });
            return gate.Task;
        }
        async Task ObserveMutationAsync(string path)
        {
            if (cut != 2 || gateClaimed || !packetWritten || originalRollbackBytes?.ContainsKey(path) != true) return;
            // Latch the FIRST exact original backup mutation independently of
            // completion/story validity; a later deletion cannot stand in for it.
            gateClaimed = true;
            try
            {
                Assert.NotNull(observedCompletion);
                var capture = observedCompletion.GetType().GetField("_capture", flags)!.GetValue(observedCompletion)!;
                await HoldBoundaryAsync(path, capture, null, "first exact original rollback mutation after ordinary story",
                    new StackTrace().ToString());
            }
            catch (Exception preparation)
            {
                File.WriteAllText(Path.Combine(own, "fixture-preparation-failure.txt"), preparation.ToString());
                await Task.Delay(TimeSpan.FromSeconds(180));
                throw;
            }
        }
        files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalReadOpenAsync = ObserveReadAsync,
                BeforeCanonicalMutationAsync = ObserveMutationAsync
            });
        var engine = factory.CreateGameEngine(new QueuedConsoleInputSource([]), fileSystem: files, configureSettings: settings =>
        { settings.MusicEnabled = false; settings.SoundEnabled = false; }, finalizationHooks: new GameEngineSessionFinalizationHooks
        {
            AtCheckpointAsync = checkpoint =>
            {
                if (checkpoint == SessionFinalizationCheckpoint.TerminalWaitStarted)
                {
                    request = JsonSerializer.Deserialize<TurnRequest>(File.ReadAllText(files.ResolvePath("input/turn_request.json")), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                    CaptureBrowserProfileFixture(files, own);
                    var manifest = JsonNode.Parse(File.ReadAllText(files.ResolvePath("game_state/control/pending_turn_snapshot.json")))!;
                    originalRollbackBytes = manifest["rollbackBackups"]!.AsObject().ToDictionary(
                        pair => pair.Value!.GetValue<string>(), pair => File.ReadAllBytes(files.ResolvePath(pair.Value!.GetValue<string>())));
                    WriteMusingsRepairPacket(files, request, invalidNarrative: false);
                    File.WriteAllBytes(Path.Combine(own, "original-packet-guardians.bin"), File.ReadAllBytes(files.ResolvePath("game_state/meta/guardians.json")));
                    packetWritten = true;
                }
                return Task.CompletedTask;
            }
        });
        validator = GetPrivateField<ValidationService>(engine, "_validator");
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(control["SessionId"]!.GetValue<string>(), control["TurnNumber"]!.GetValue<int>());
        operation = InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Я читаю исходное письмо.", null);
        await operation;
        throw new InvalidOperationException("PREPARATION: original engine returned without reaching its intended crash gate.");
    }

    public static async Task WriteMusingsColdProbeAsync(string root, string output, int cut, string own)
    {
        using var factory = new GameEngineTurnLifecycleTests(); // Its independent temporary root is unrelated to retained state.
        int opens = 0, callbacks = 0, mutations = 0;
        bool operationStarted = false;
        Exception? refusal = null;
        // Instrument the complete retained-root constructor/factory/setup interval.
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                AfterCanonicalWriteLockOpenedAsync = () => { opens++; return Task.CompletedTask; },
                LocalPublicationRecoveryObserver = (_, _) => callbacks++,
                BeforeCanonicalMutationAsync = _ => { mutations++; return Task.CompletedTask; }
            });
        try
        {
            var engine = factory.CreateGameEngine(new QueuedConsoleInputSource([]), fileSystem: files, configureSettings: settings =>
            { settings.MusicEnabled = false; settings.SoundEnabled = false; });
            var control = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "cut-control.json")))!;
            GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(control["SessionId"]!.GetValue<string>(), control["TurnNumber"]!.GetValue<int>());
            operationStarted = true;
            await InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Этот новый ход должен быть заблокирован.", null).WaitAsync(TimeSpan.FromSeconds(12));
        }
        catch (Exception failure) { refusal = failure; }
        var phase = refusal?.Data["ParticipatingAdmissionPhase"]?.ToString();
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            Pid = Environment.ProcessId, Cut = cut, OperationStarted = operationStarted,
            NativeAdmissionRefused = operationStarted && refusal is not null && phase is "original-connection" or "read-record" &&
                refusal.ToString().Contains("GmMainOperationClient.OpenAsync", StringComparison.Ordinal),
            AdmissionPhase = phase, Failure = refusal?.ToString(), CanonicalOpens = opens, RecoveryCallbacks = callbacks, Mutations = mutations,
            Scope = "Cold ordinary ProcessPlayerTurn, no bootstrap/Ensure/menu/normalization; all retained-root setup instrumented."
        }));
    }

    private static Process StartMusingsCutChild(string role, string root, string output, int cut, string own)
    {
        var support = Path.Combine(Path.GetDirectoryName(typeof(GameEngineTurnLifecycleTests).Assembly.Location)!, "BookOfEternityClient.TestSupport.dll");
        var start = new ProcessStartInfo(Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"))
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = TestRepoPaths.RepoRoot };
        foreach (var arg in new[] { support, role, typeof(GameEngineTurnLifecycleTests).Assembly.Location, root, output, cut.ToString(), own }) start.ArgumentList.Add(arg);
        return Process.Start(start)!;
    }

    private static FileSystemManager.CanonicalWriteLease? FindActualMusingsPublicationLease(object? state)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var owners = new HashSet<FileSystemManager.CanonicalWriteLease>(ReferenceEqualityComparer.Instance);
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        void Inspect(object? value)
        {
            if (value is FileSystemManager.CanonicalWriteLease { IsActive: true } lease) { owners.Add(lease); return; }
            if (value is null || !seen.Add(value)) return;
            var type = value.GetType();
            if (!type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false) ||
                !type.Name.Contains("DisplayClass", StringComparison.Ordinal)) return;
            foreach (var field in type.GetFields(flags)) Inspect(field.GetValue(value));
        }
        if (state is not null) foreach (var field in state.GetType().GetFields(flags)) Inspect(field.GetValue(state));
        return owners.SingleOrDefault();
    }

    private static (List<object> States, bool GateReachable, object Diagnostic) ReadActualMusingsStateMachines(Task operation, Task gate)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const int maximumNodes = 256;
        var ids = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        int Id(object value) { if (!ids.TryGetValue(value, out var id)) ids.Add(value, id = ids.Count + 1); return id; }
        var nodes = new List<object>();
        var edges = new List<object>();
        var recorded = new HashSet<object>(ReferenceEqualityComparer.Instance);
        object? Record(object value)
        {
            var state = value is Task task ? task.GetType().GetField("StateMachine", flags)?.GetValue(task) : null;
            if (recorded.Add(value)) nodes.Add(new
            {
                Id = Id(value), Type = value.GetType().FullName, State = state?.GetType().FullName,
                TaskId = (value as Task)?.Id, Completed = (value as Task)?.IsCompleted,
                Fields = state?.GetType().GetFields(flags).Select(field => new
                { field.Name, Type = field.FieldType.FullName, ActualType = field.GetValue(state)?.GetType().FullName }).ToArray()
            });
            return state;
        }
        var queue = new Queue<Task>(); queue.Enqueue(operation);
        var seen = new HashSet<Task>(ReferenceEqualityComparer.Instance);
        var states = new List<object>();
        bool forwardReachable = false;
        void Forward(Task from, Task to, string kind)
        { edges.Add(new { From = Id(from), To = Id(to), Kind = kind, Direction = "forward-await" }); queue.Enqueue(to); }
        while (queue.TryDequeue(out var task) && seen.Count < maximumNodes)
        {
            if (task.IsCompleted || !seen.Add(task)) continue;
            if (ReferenceEquals(task, gate)) forwardReachable = true;
            var state = Record(task);
            if (state is null) continue;
            states.Add(state);
            foreach (var field in state.GetType().GetFields(flags))
            {
                var value = field.GetValue(state);
                if (value is Task child) Forward(task, child, field.Name);
                else if (value is not null && field.Name.StartsWith("<>u__", StringComparison.Ordinal))
                    foreach (var awaited in value.GetType().GetFields(flags))
                        if (awaited.GetValue(value) is Task pending) Forward(task, pending, field.Name + "/" + awaited.Name);
            }
        }
        // Runtime wrappers do not all expose an async state machine. Follow only
        // real registered continuation links from this gate to the SAME original
        // operation, never arbitrary service/FS captures or matching method names.
        var reverse = new Queue<object>(); reverse.Enqueue(gate);
        var reverseSeen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var reverseStates = new List<object>();
        var parent = new Dictionary<object, (object From, string Kind)>(ReferenceEqualityComparer.Instance);
        bool reverseReachable = false;
        void Reverse(object from, object to, string kind)
        {
            edges.Add(new { From = Id(from), To = Id(to), Kind = kind, Direction = "reverse-continuation" });
            parent.TryAdd(to, (from, kind)); reverse.Enqueue(to);
        }
        while (reverse.TryDequeue(out var value) && reverseSeen.Count < maximumNodes)
        {
            if (!reverseSeen.Add(value)) continue;
            var state = Record(value);
            if (state is not null) reverseStates.Add(state);
            if (ReferenceEquals(value, operation)) { reverseReachable = true; break; }
            if (value is Task task)
            {
                var continuation = typeof(Task).GetField("m_continuationObject", flags)!.GetValue(task);
                if (continuation is not null) Reverse(value, continuation, "m_continuationObject");
            }
            else if (value is Delegate action)
            {
                foreach (var item in action.GetInvocationList()) if (item.Target is not null) Reverse(value, item.Target, "delegate-target");
            }
            else if (value is System.Collections.IEnumerable list)
            {
                foreach (var item in list) if (item is not null) Reverse(value, item, "continuation-list-item");
            }
            else if (value.GetType().Namespace is { } ns &&
                (ns.StartsWith("System.Threading.Tasks", StringComparison.Ordinal) || ns == "System.Runtime.CompilerServices"))
            {
                for (var type = value.GetType(); type is not null; type = type.BaseType)
                    foreach (var field in type.GetFields(flags | BindingFlags.DeclaredOnly))
                        if (field.GetValue(value) is { } next && (next is Task or Delegate || field.Name.Contains("continuation", StringComparison.OrdinalIgnoreCase)))
                            Reverse(value, next, field.Name);
            }
        }
        var connecting = new List<object>();
        if (reverseReachable)
        {
            object current = operation;
            while (!ReferenceEquals(current, gate) && parent.TryGetValue(current, out var edge))
            { connecting.Add(new { From = Id(edge.From), To = Id(current), edge.Kind }); current = edge.From; }
            connecting.Reverse();
            states.AddRange(reverseStates);
        }
        var diagnostic = new { Operation = Id(operation), Gate = Id(gate), ForwardReachable = forwardReachable,
            ReverseReachable = reverseReachable, ConnectingReversePath = connecting, Nodes = nodes, Edges = edges,
            ForwardTruncated = queue.Count != 0, ReverseTruncated = reverse.Count != 0 && !reverseReachable };
        return (states.Distinct(ReferenceEqualityComparer.Instance).ToList(), forwardReachable || reverseReachable, diagnostic);
    }

    private static Dictionary<string, string> MusingsCutInventory(FileSystemManager files)
    {
        // Two fixed diagnostic endpoints are owned by the live Bridge/daemon and
        // are not canonical gameplay or evidence. No other session path is excluded.
        var result = Directory.EnumerateFiles(files.GameSessionPath, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(files.GameSessionPath, path).Replace('\\', '/') is not
                ("game_state/control/gm_bridge_status.json" or "game_state/control/gm_daemon_status.json"))
            .ToDictionary(path => Path.GetRelativePath(files.GameSessionPath, path).Replace('\\', '/'),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);
        result.Add("original-runtime-generation", Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(files.SessionGenerationPath))));
        return result;
    }

    private static void RetainMusingsCutFiles(FileSystemManager files, string own, string label)
    {
        var folder = Path.Combine(own, label); Directory.CreateDirectory(folder);
        var paths = new[] { "game_state/meta/guardians.json", "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
            PendingTurnSnapshotAuthority.AuthorityPath, PendingTurnStateService.PendingDiceStatePath, "stories/chaos_sea.jsonl" };
        foreach (var path in paths)
        {
            var full = files.ResolvePath(path);
            if (!File.Exists(full)) continue;
            var copy = Path.Combine(folder, path); Directory.CreateDirectory(Path.GetDirectoryName(copy)!); File.WriteAllBytes(copy, File.ReadAllBytes(full));
        }
        File.WriteAllBytes(Path.Combine(folder, "original-generation.json"), File.ReadAllBytes(files.SessionGenerationPath));
        File.WriteAllText(Path.Combine(folder, "inventory.json"), JsonSerializer.Serialize(MusingsCutInventory(files)));
    }
}
