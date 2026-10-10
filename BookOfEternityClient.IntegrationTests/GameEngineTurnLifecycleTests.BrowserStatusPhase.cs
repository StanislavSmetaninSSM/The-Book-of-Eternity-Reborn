using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    public async Task BrowserOriginalAdmission_StatusPreparingRefusalDefersWithoutLosingOriginalOwner()
    {
        var own = Path.Combine("/tmp", "gc-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(own);
        _directGachaOutput?.WriteLine("Owned actual staging cut evidence: " + own);
        var package = Path.Combine(own, "package");
        await RunBrowserColdChildAsync("pwsh", ["-NoLogo", "-NoProfile", "-File",
            Path.Combine(TestRepoPaths.RepoRoot, "scripts/build-linux-supervisor.ps1"), "-OutputDirectory", package,
            "-IncludeHostGuardian", "-IncludeTerminalFixture"], Path.Combine(own, "native.log"), 25);
        var support = Path.Combine(Path.GetDirectoryName(typeof(GameEngineTurnLifecycleTests).Assembly.Location)!, "BookOfEternityClient.TestSupport.dll");
        await RunBrowserColdChildAsync(Path.Combine(package, "host-guardian"), ["--live-turn", Path.Combine(own, "guardian.json"), "25000",
            Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"), support, "engine-browser-status-phase",
            typeof(GameEngineTurnLifecycleTests).Assembly.Location, package, Path.Combine(own, "result.json"), "0", own], Path.Combine(own, "probe.log"), 30);
        var guardian = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "guardian.json")))!;
        Assert.True(guardian["echild"]!.GetValue<bool>());
        Assert.Equal(0, guardian["driverExitCode"]!.GetValue<int>());
        Assert.Equal(0, guardian["failures"]!.GetValue<int>());
        Assert.Equal(0, guardian["emergencySignals"]!.GetValue<int>());
        Assert.False(guardian["deadline"]!.GetValue<bool>());
        var result = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "result.json")))!;
        Assert.True(result["Deferred"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["EvidenceRetained"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["RepublishedAfterStage"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["OriginalStopped"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["PhysicalCleanup"]!.GetValue<bool>(), result.ToJsonString());
    }

    public static async Task WriteBrowserStatusPhaseProbeAsync(string package, string output, int mode, string own)
    {
        using var factory = new GameEngineTurnLifecycleTests();
        var launch = NeutralTerminalLaunch.Create(package, own);
        var root = Directory.GetParent(launch.Scratch)!.FullName;
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        files.EnsureDirectoryStructure();
        await files.WriteFileAtomicAsync("game_state/meta/soul_state.json",
            "{\"soulName\":\"Проверочная душа\",\"sessionId\":\"browser-recovery-session\",\"currentRealm\":\"Mortal World\",\"currentIncarnation\":1}");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var type = Assembly.LoadFrom(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityGMBridge/bin", configuration, "net8.0/BookOfEternityGMBridge.dll"))
            .GetType("BookOfEternityGMBridge.BridgeHost", true)!;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var host = Activator.CreateInstance(type, [launch.Scratch, "c5-status-" + Guid.NewGuid().ToString("N")])!;
        type.GetMethod("ConfigureNeutral", flags)!.Invoke(host, [launch]);
        async Task Call(string name) => await (Task)type.GetMethod(name, flags)!.Invoke(host, null)!;
        using var control = new CancellationTokenSource();
        var server = (Task)type.GetMethod("RunServerLoopAsync", flags)!.Invoke(host, [control.Token])!;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        var deferred = false;
        var retained = false;
        var republished = false;
        var stopped = false;
        var physical = false;
        Exception? publisherFailure = null;
        Exception? cleanupFailure = null;
        GmSessionRunCoordinator? owner = null;
        try
        {
            await Call("StartShellAsync");
            await ((TaskCompletionSource)type.GetField("_firstStatus", flags)!.GetValue(host)!).Task.WaitAsync(TimeSpan.FromSeconds(3));
            owner = (GmSessionRunCoordinator)type.GetField("_mainRun", flags)!.GetValue(host)!;
            var binding = await factory.QueueBrowserInputAsync(files);
            var engine = factory.CreateGameEngine(new QueuedConsoleInputSource([]), fileSystem: files);
            await SessionOperationContext.RunParticipatingExpectedSessionAsync(files, binding.Generation, async () =>
            {
                using var original = files.BeginBrowserOriginalOperation(binding);
                await InvokePrivateTaskAsync(engine, "ClaimBrowserPreparationAsync", binding);
                var pendingPath = files.ResolvePath(PendingPlayerActionService.PendingPath);
                var pending = File.ReadAllBytes(pendingPath);
                await File.WriteAllBytesAsync(Path.Combine(own, "actual-preparing.json"), pending);
                type.GetField("BeforeStatusPublication", flags)!.SetValue(host, (Func<Task>)(async () =>
                {
                    Interlocked.Increment(ref attempts);
                    entered.TrySetResult();
                    await release.Task;
                }));
                type.GetMethod("WriteStatusFile", flags)!.Invoke(host, null);
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
                var publisher = (Task)type.GetField("_statusPublisher", flags)!.GetValue(host)!;
                var beforeStatus = File.ReadAllBytes(files.ResolvePath("game_state/control/gm_bridge_status.json"));
                release.TrySetResult();
                var pinField = typeof(GmSessionRunCoordinator).GetField("_pins", flags)!;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                while (!publisher.IsCompleted && (int)pinField.GetValue(owner)! != 1 && watch.Elapsed < TimeSpan.FromSeconds(3))
                    await Task.Delay(10);
                if (publisher.IsFaulted) publisherFailure = publisher.Exception!.GetBaseException();
                deferred = !publisher.IsCompleted && !owner.IsUncertain && (int)pinField.GetValue(owner)! == 1;
                retained = File.ReadAllBytes(pendingPath).SequenceEqual(pending) &&
                    File.ReadAllBytes(files.ResolvePath("game_state/control/gm_bridge_status.json")).SequenceEqual(beforeStatus) &&
                    !File.Exists(files.ResolvePath("input/turn_request.json"));
                if (!deferred) return false;
                var request = new TurnRequest
                {
                    SessionId = "browser-recovery-session", RequestId = binding.ActionId, TurnNumber = 1,
                    PlayerAction = binding.Action, Timestamp = DateTime.UtcNow.ToString("O"), PreGeneratedDices1d20 = [3, 17],
                    ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" }
                };
                await InvokePrivateTaskAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, null, "обработки хода", binding);
                await InvokePrivateAsync<PendingPlayerActionService.Staged>(engine, "PublishBrowserStagingAsync", binding,
                    JsonSerializer.Serialize(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                type.GetMethod("WriteStatusFile", flags)!.Invoke(host, null);
                watch.Restart();
                while (!publisher.IsCompleted && (attempts < 2 || (int)pinField.GetValue(owner)! != 1) && watch.Elapsed < TimeSpan.FromSeconds(3))
                    await Task.Delay(10);
                republished = !publisher.IsCompleted && !owner.IsUncertain && attempts >= 2 &&
                    !File.ReadAllBytes(files.ResolvePath("game_state/control/gm_bridge_status.json")).SequenceEqual(beforeStatus);
                return true;
            });
        }
        catch (Exception failure) { publisherFailure ??= failure; }
        finally
        {
            release.TrySetResult();
            // Fixture cleanup only: withhold original browser markers during original
            // native Stop, then restore exact bytes. This does not qualify game recovery.
            var paths = new[] { PendingPlayerActionService.PendingPath, "game_state/control/pending_turn_snapshot.json" };
            var saved = paths.Where(p => File.Exists(files.ResolvePath(p))).ToDictionary(p => p, p => File.ReadAllBytes(files.ResolvePath(p)));
            foreach (var path in saved.Keys) File.Delete(files.ResolvePath(path));
            try
            {
                await Call("StopShellAsync");
                stopped = owner?.Record?.Disposition == GmSessionRunDisposition.Stopped;
            }
            catch (Exception failure) { cleanupFailure = failure; }
            finally { foreach (var pair in saved) File.WriteAllBytes(files.ResolvePath(pair.Key), pair.Value); }
            if (type.GetField("_pty", flags)!.GetValue(host) is IOwnedTerminalSession terminal)
                physical = (await terminal.StopAndObserveAsync(CancellationToken.None)).CleanupComplete;
            else physical = stopped;
            await control.CancelAsync();
            await server.WaitAsync(TimeSpan.FromSeconds(5));
            ((IDisposable)host).Dispose();
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
            {
                Deferred = deferred, EvidenceRetained = retained, RepublishedAfterStage = republished, OriginalStopped = stopped,
                PhysicalCleanup = physical, OriginalUncertain = owner?.IsUncertain, PublisherFailure = publisherFailure?.ToString(),
                CleanupFailure = cleanupFailure?.ToString(), Attempts = attempts,
                Scope = "Actual GameEngine preparing claim and original NativeLineage Bridge status publisher; controlled pause, no Program/relay/model"
            }));
        }
    }
}
