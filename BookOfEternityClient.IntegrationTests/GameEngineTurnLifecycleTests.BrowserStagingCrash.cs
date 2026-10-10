using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    public async Task BrowserOriginalAdmission_ActualStagingProcessCrashRefusesBeforeRecovery(int member, bool introduceAtLock)
    {
        var own = Path.Combine("/tmp", "gc-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(own);
        _directGachaOutput?.WriteLine("Owned actual staging cut evidence: " + own);
        var package = Path.Combine(own, "package");
        await RunBrowserColdChildAsync("pwsh", ["-NoLogo", "-NoProfile", "-File",
            Path.Combine(TestRepoPaths.RepoRoot, "scripts/build-linux-supervisor.ps1"), "-OutputDirectory", package,
            "-IncludeHostGuardian"], Path.Combine(own, "native.log"), 25);
        var result = Path.Combine(own, "result.json");
        var guardian = Path.Combine(own, "guardian.json");
        var dotnet = Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet");
        var support = Path.Combine(Path.GetDirectoryName(typeof(GameEngineTurnLifecycleTests).Assembly.Location)!, "BookOfEternityClient.TestSupport.dll");
        await RunBrowserColdChildAsync(Path.Combine(package, "host-guardian"), ["--live-turn", guardian, "25000", dotnet,
            support, "engine-browser-staging-cut", typeof(GameEngineTurnLifecycleTests).Assembly.Location,
            _fs.BasePath, result, member.ToString(), introduceAtLock ? "at-lock" : "preflight"], Path.Combine(own, "probe.log"), 30);
        var g = JsonNode.Parse(File.ReadAllText(guardian))!;
        Assert.True(g["echild"]!.GetValue<bool>());
        Assert.Equal(0, g["driverExitCode"]!.GetValue<int>());
        Assert.Equal(0, g["failures"]!.GetValue<int>());
        Assert.Equal(0, g["emergencySignals"]!.GetValue<int>());
        Assert.False(g["deadline"]!.GetValue<bool>());
        var actual = JsonNode.Parse(File.ReadAllText(result))!;
        Assert.Equal(137, actual["OriginalExitCode"]!.GetValue<int>());
        Assert.True(actual["OriginalEOF"]!.GetValue<bool>());
        Assert.NotEqual(Environment.ProcessId, actual["OriginalPid"]!.GetValue<int>());
        Assert.True(actual["UncommittedOriginalJournal"]!.GetValue<bool>());
        Assert.True(actual["Blocked"]!.GetValue<bool>(), actual.ToJsonString());
        Assert.Equal(0, actual["RecoveryCallbacks"]!.GetValue<int>());
        Assert.True(actual["ExactEvidenceRetained"]!.GetValue<bool>(), actual.ToJsonString());
        Assert.Equal(introduceAtLock, actual["PhysicalHookReached"]!.GetValue<bool>());
    }

    // Runs beneath the independent guardian; this process owns and kills only
    // its freshly started publisher. No caught callback can unwind or roll back
    // the genuine original GameEngine staging transaction before the crash.
    public static async Task WriteBrowserStagingCutProbeAsync(string root, string output, int member, string mode)
    {
        var nonce = Guid.NewGuid().ToString("N");
        var ack = output + "." + nonce + ".cut.json";
        var support = Path.Combine(Path.GetDirectoryName(typeof(GameEngineTurnLifecycleTests).Assembly.Location)!, "BookOfEternityClient.TestSupport.dll");
        var info = new ProcessStartInfo(Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"))
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = TestRepoPaths.RepoRoot };
        foreach (var arg in new[] { support, "engine-browser-staging-publisher", typeof(GameEngineTurnLifecycleTests).Assembly.Location,
            root, ack, member.ToString(), nonce }) info.ArgumentList.Add(arg);
        using var original = Process.Start(info)!;
        var stdout = original.StandardOutput.ReadToEndAsync();
        var stderr = original.StandardError.ReadToEndAsync();
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            while (!File.Exists(ack))
            {
                if (original.HasExited) throw new InvalidOperationException("Original publisher exited before the actual cut: " + await stderr);
                await Task.Delay(10, deadline.Token);
            }
            var cut = JsonNode.Parse(File.ReadAllText(ack))!;
            Assert.Equal(original.Id, cut["Pid"]!.GetValue<int>());
            Assert.Equal(nonce, cut["Nonce"]!.GetValue<string>());
            Assert.Equal(member, cut["Member"]!.GetValue<int>());
            original.Kill();
            await original.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(137, original.ExitCode);
            await File.WriteAllTextAsync(output + ".original.log", await stdout + await stderr);
            var journalPath = Path.Combine(root, ".boe_runtime/trusted-local-publication-v1/active.json");
            var journalBytes = File.ReadAllBytes(journalPath);
            var journal = JsonNode.Parse(journalBytes)!;
            Assert.False(journal["Committed"]!.GetValue<bool>());
            Assert.Equal(2, journal["Members"]!.AsArray().Count);
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            var slot = JsonNode.Parse(File.ReadAllText(files.ResolvePath(PendingPlayerActionService.PendingPath)))!;
            Assert.Equal(member == 0 ? "preparing" : "staged", slot["status"]!.GetValue<string>());
            Dictionary<string, string> Snapshot() => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(path => !Path.GetRelativePath(root, path).Replace('\\', '/').StartsWith(".boe_runtime/locks/", StringComparison.Ordinal))
                .ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                    path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);
            var before = Snapshot();
            var atLock = mode == "at-lock";
            if (atLock) File.Delete(journalPath); // Restore these exact original bytes at the physical boundary below.
            var reached = false;
            var callbacks = 0;
            var cold = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks
                {
                    AfterCanonicalWriteLockOpenedAsync = () =>
                    {
                        reached = true;
                        if (atLock) File.WriteAllBytes(journalPath, journalBytes);
                        return Task.CompletedTask;
                    },
                    LocalPublicationRecoveryObserver = (_, _) => callbacks++
                });
            Exception? refusal = null;
            try { await using var lease = await cold.AcquireCanonicalWriteLeaseAsync(); }
            catch (Exception failure) when (failure is InvalidDataException or InvalidOperationException) { refusal = failure; }
            var after = Snapshot();
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
            {
                OriginalPid = original.Id, OriginalExitCode = original.ExitCode, OriginalEOF = true,
                UncommittedOriginalJournal = true, Blocked = refusal != null, ErrorType = refusal?.GetType().FullName,
                RecoveryCallbacks = callbacks, PhysicalHookReached = reached,
                ExactEvidenceRetained = before.Count == after.Count && before.All(pair => after.TryGetValue(pair.Key, out var value) && value == pair.Value),
                Before = before, After = after, Cut = cut,
                Scope = "actual GameEngine staging publication crash; guarded quiescent component; not live GM or arbitrary active-owner recovery"
            }));
        }
        finally
        {
            if (!original.HasExited) original.Kill();
            await original.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    public static async Task RunOriginalBrowserStagingPublisherAsync(string root, string ack, int member, string nonce)
    {
        using var factory = new GameEngineTurnLifecycleTests();
        FileSystemManager? files = null;
        files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) =>
                {
                    if (phase != TrustedLocalPublicationPhase.MemberPublished || index != member ||
                        !File.Exists(files!.ResolvePath("input/turn_request.json"))) return;
                    var state = JsonNode.Parse(File.ReadAllText(files.ResolvePath(PendingPlayerActionService.PendingPath)))!;
                    if (state["status"]!.GetValue<string>() is not ("preparing" or "staged")) return;
                    File.WriteAllText(ack + ".tmp", JsonSerializer.Serialize(new
                    { Pid = Environment.ProcessId, Nonce = nonce, Member = member, Phase = phase.ToString(), ActionId = state["actionId"]!.GetValue<string>() }));
                    File.Move(ack + ".tmp", ack);
                    using var blocked = new ManualResetEventSlim(false);
                    if (!blocked.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Original staging cut was not observed and killed.");
                }
            });
        await factory.PrepareBrowserInputStagingAsync(withRollback: true, files: files);
        throw new InvalidOperationException("Original staging unexpectedly returned beyond its crash gate.");
    }
}
