using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Real native helper, independent mutable state and emergency guardian per test.</summary>
public sealed class LinuxFallbackSupervisorTests
{
    [Theory]
    [InlineData("S")]
    [InlineData("C")]
    public async Task BeforeStart_SealPreventsAnyChild(string command)
    {
        await using var run = await NativeRun.Create();
        await run.Ready();
        await run.Send(command);
        await run.Finish();
        run.AssertScopedStop();
        Assert.Empty(run.Events());
    }

    [Theory]
    [InlineData("doublefork")]
    [InlineData("ignore")]
    [InlineData("spawn")]
    [InlineData("root-first")]
    [InlineData("named")]
    public async Task NativeDescendants_RetireWithinDeclaredScope(string mode)
    {
        await using var run = await NativeRun.Create(mode);
        await run.Start();
        await run.WaitEvent(mode == "root-first" ? "leaf" : "prepared");
        if (mode != "root-first") await run.Send("S");
        await run.Finish();
        run.AssertScopedStop();
        Assert.Contains(run.Events(), e => e.GetProperty("kind").GetString() == (mode == "spawn" ? "spawned" : "leaf"));
        if (mode == "doublefork") Assert.Contains(run.Events(), e => e.GetProperty("kind").GetString() == "detached");
        if (mode == "named") Assert.Contains(run.Events(), e => e.GetProperty("kind").GetString() == "name-set");
        if (mode == "root-first") Assert.Equal(23, run.Records.Last().GetProperty("rootExitCode").GetInt32());
        Assert.True(run.Elapsed < TimeSpan.FromSeconds(6), "Cleanup must precede independent 7s fixture alarms.");
    }

    [Fact]
    public async Task CancellationAfterStart_StopsActualChild()
    {
        await using var run = await NativeRun.Create("ignore");
        await run.Start(); await run.WaitEvent("prepared"); await run.Send("C");
        await run.Finish(); run.AssertScopedStop();
        Assert.Equal("cancelled", run.Records.Last().GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnerChannelLoss_RetainsUncertaintyAfterActualCleanup(bool launched)
    {
        await using var run = await NativeRun.Create("ignore");
        if (launched) { await run.Start(); await run.WaitEvent("prepared"); }
        else await run.Ready();
        run.CloseOwner(); await run.Finish(); run.AssertUncertain("owner-lost");
        Assert.Equal(launched, run.Events().Length > 0);
    }

    [Fact]
    public async Task Deadline_RetainsAuthorityAndUncertaintyThroughLateCleanup()
    {
        await using var run = await NativeRun.Create("ignore", grace: 700, deadline: 60);
        await run.Start(); await run.WaitEvent("prepared"); await run.Send("S");
        var uncertain = await run.ReadUntil("Uncertain");
        Assert.False(uncertain.GetProperty("cleanupComplete").GetBoolean());
        Assert.True(uncertain.GetProperty("authorityRetained").GetBoolean());
        Assert.False(run.HasExited);
        await run.Finish(); run.AssertUncertain("stop-timeout");
        Assert.DoesNotContain(run.Records, r => r.GetProperty("state").GetString() == "StoppedWithinScope");
    }

    [Fact]
    public async Task ObservedOutsideContractWork_IsVisibleAndRemainsUncertain()
    {
        await using var run = await NativeRun.Create("tree", guardianMode: "sentinel");
        await run.Start(); await run.WaitEvent("prepared"); await run.WaitEvent("outside-sentinel");
        await run.Send("U"); await run.Finish(); run.AssertUncertain("scope-breach");
        Assert.True(run.Guard.GetProperty("sentinelAliveAfterHelper").GetBoolean());
        Assert.True(run.Guard.GetProperty("sentinelReaped").GetBoolean());
    }

    [Fact]
    public async Task ExecFailure_DoesNotClaimStartedOrScopedSuccess()
    {
        await using var run = await NativeRun.Create(missingCommand: true);
        await run.Ready(); await run.Send("L"); await run.Finish();
        run.AssertUncertain("exec-failed");
        Assert.DoesNotContain(run.Records, r => r.GetProperty("state").GetString() == "Started");
        Assert.Empty(run.Events());
    }

    [Theory]
    [InlineData("?")]
    [InlineData("LL")]
    public async Task MalformedOrRepeatedLaunch_SealsUncertain(string command)
    {
        await using var run = await NativeRun.Create();
        await run.Ready(); await run.Send(command); await run.Finish();
        run.AssertUncertain("invalid-command");
        Assert.True(run.Events().Count(e => e.GetProperty("kind").GetString() == "root") <= 1);
    }

    [Fact]
    public async Task Child_DoesNotInheritOwnerInput_AndPreservesSyntheticEnvironmentAndCwd()
    {
        await using var run = await NativeRun.Create("metadata");
        await run.Start(); await run.WaitEvent("stdin-eof"); await run.WaitEvent("metadata-ok");
        run.CloseOwner(); await run.Finish(); run.AssertUncertain("owner-lost");
    }

    [Fact]
    public async Task ClosedStatusAfterStart_RetiresOwnedChildrenWithoutBlockingOrSigpipeExit()
    {
        await using var run = await NativeRun.Create("ignore");
        await run.Start(); await run.WaitEvent("prepared"); run.CloseStatus();
        await run.Send("S"); await run.Finish();
        Assert.Equal(2, run.Guard.GetProperty("helperExitCode").GetInt32());
    }

    [Fact]
    public async Task FullStatusPipe_BeforeReadyFailsClosedWithoutAnyLaunch()
    {
        await using var run = await NativeRun.Create(guardianMode: "blocked-status");
        await run.Finish();
        Assert.Equal(2, run.Guard.GetProperty("helperExitCode").GetInt32());
        Assert.Empty(run.Events());
        Assert.Empty(run.Records);
    }

    [Fact]
    public async Task StalePidfdCannotSignalAnotherOwnedIncarnation()
    {
        await using var run = await NativeRun.Create(staleOnly: true);
        await run.FinishPrimitive();
    }

    internal sealed class NativeRun : IAsyncDisposable
    {
        private readonly Process process;
        private readonly string directory;
        private readonly string id;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Task<string> stderr;
        private bool ownerClosed, statusClosed, finished;
        public List<JsonElement> Records { get; } = new();
        public JsonElement Guard { get; private set; }
        public bool HasExited => process.HasExited;
        public TimeSpan Elapsed => clock.Elapsed;
        private NativeRun(Process process, string directory, string id)
        {
            this.process = process; this.directory = directory; this.id = id;
            stderr = process.StandardError.ReadToEndAsync();
        }
        public static async Task<NativeRun> Create(string mode = "simple", int grace = 150, int deadline = 3000, string guardianMode = "normal", bool missingCommand = false, bool staleOnly = false)
        {
            Assert.True(OperatingSystem.IsLinux(), "Actual Linux native qualification required; no OS skip.");
            var id = Guid.NewGuid().ToString("N");
            var root = TestRepoPaths.RepoRoot;
            var evidence = Environment.GetEnvironmentVariable("BOE_NATIVE_EVIDENCE_ROOT") ?? Path.Combine(root, "TestResults", "linux-fallback");
            var directory = Path.Combine(evidence, id);
            Directory.CreateDirectory(directory);
            var build = new ProcessStartInfo("pwsh") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var arg in new[] { "-NoProfile", "-File", Path.Combine(root, "scripts", "build-linux-supervisor.ps1"), "-OutputDirectory", directory, "-IncludeFixture" }) build.ArgumentList.Add(arg);
            using (var compiler = Process.Start(build)!)
            {
                var stdout = compiler.StandardOutput.ReadToEndAsync(); var errors = compiler.StandardError.ReadToEndAsync();
                await compiler.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(40));
                var log = await stdout + await errors; await File.WriteAllTextAsync(Path.Combine(directory, "build.log"), log);
                Assert.True(compiler.ExitCode == 0, "Native preparation failed (not behavioral RED): " + log);
            }
            using (var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "build-provenance.json"))))
            {
                foreach (var asset in manifest.RootElement.GetProperty("assets").EnumerateArray())
                {
                    static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
                    Assert.Equal(asset.GetProperty("sourceSha256").GetString(), Hash(Path.Combine(root, asset.GetProperty("source").GetString()!)));
                    Assert.Equal(asset.GetProperty("binarySha256").GetString(), Hash(Path.Combine(directory, asset.GetProperty("binary").GetString()!)));
                }
            }
            var fixture = Path.Combine(directory, "lineage-fixture");
            var start = new ProcessStartInfo(fixture) { WorkingDirectory = directory, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.Environment["BOE_NATIVE_TEST_MARKER"] = "synthetic-marker";
            var args = staleOnly ? new[] { "--pidfd-check" } : new[] { "--guard", directory, guardianMode, Path.Combine(directory, "boe-lineage-supervisor"), id, grace.ToString(), deadline.ToString(), missingCommand ? Path.Combine(directory, "missing-executable") : fixture, "--worker", directory, mode };
            foreach (var arg in args) start.ArgumentList.Add(arg);
            return new NativeRun(Process.Start(start)!, directory, id);
        }
        public async Task Send(string command) { await process.StandardInput.WriteAsync(command); await process.StandardInput.FlushAsync(); }
        public void CloseOwner() { if (!ownerClosed) { ownerClosed = true; process.StandardInput.Close(); } }
        public void CloseStatus() { statusClosed = true; process.StandardOutput.Close(); }
        public void RequestHelperCrash() => File.WriteAllText(Path.Combine(directory, "crash"), "owned fixture request");
        public async Task Ready()
        {
            var ready = await ReadUntil("Ready");
            Assert.False(ready.GetProperty("cleanupComplete").GetBoolean());
            Assert.True(ready.GetProperty("authorityRetained").GetBoolean());
        }
        public async Task Start() { await Ready(); await Send("L"); await ReadUntil("Started"); }
        private void Record(string line)
        {
            Assert.True(line.Length < 2048);
            using var parsed = JsonDocument.Parse(line); var record = parsed.RootElement.Clone();
            Assert.Equal(1, record.GetProperty("version").GetInt32());
            Assert.Equal(id, record.GetProperty("runId").GetString());
            Assert.Equal("native-lineage", record.GetProperty("backend").GetString());
            Assert.Equal("ordinary-same-namespace-lineage", record.GetProperty("guarantee").GetString());
            Assert.False(record.TryGetProperty("accepted", out _));
            Assert.True(Records.Count < 16);
            Records.Add(record); File.AppendAllText(Path.Combine(directory, "status.jsonl"), line + "\n");
        }
        public async Task<JsonElement> ReadUntil(string state)
        {
            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(4));
                Assert.NotNull(line); Record(line!);
                if (Records.Last().GetProperty("state").GetString() == state) return Records.Last();
            }
        }
        public JsonElement[] Events()
        {
            var path = Path.Combine(directory, "events.jsonl");
            if (!File.Exists(path)) return Array.Empty<JsonElement>();
            return File.ReadAllLines(path).Select(line => { using var j = JsonDocument.Parse(line); return j.RootElement.Clone(); }).ToArray();
        }
        public async Task WaitEvent(string kind)
        {
            var until = Stopwatch.StartNew();
            while (!Events().Any(e => e.GetProperty("kind").GetString() == kind))
            {
                Assert.True(until.Elapsed < TimeSpan.FromSeconds(3), "Missing actual native fixture event: " + kind);
                await Task.Delay(10);
            }
        }
        public async Task Finish(bool expectedEmergency = false)
        {
            if (!statusClosed)
            {
                var text = await process.StandardOutput.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(24));
                foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries)) Record(line);
            }
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(24));
            clock.Stop(); await File.WriteAllTextAsync(Path.Combine(directory, "stderr.log"), await stderr);
            using var guard = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "guardian.json")));
            Guard = guard.RootElement.Clone(); finished = true;
            Assert.Equal(expectedEmergency ? 99 : 0, process.ExitCode);
            Assert.True(Guard.GetProperty("echild").GetBoolean());
            if (expectedEmergency) Assert.True(Guard.GetProperty("emergencyLineageActions").GetInt32() > 0);
            else Assert.Equal(0, Guard.GetProperty("emergencyLineageActions").GetInt32());
            Assert.False(Guard.GetProperty("deadline").GetBoolean());
            Assert.False(Guard.GetProperty("failure").GetBoolean());
        }
        public void AssertScopedStop()
        {
            Assert.Equal(0, Guard.GetProperty("helperExitCode").GetInt32());
            Assert.Equal("StoppedWithinScope", Records.Last().GetProperty("state").GetString());
            Assert.True(Records.Last().GetProperty("cleanupComplete").GetBoolean());
            Assert.False(Records.Last().GetProperty("authorityRetained").GetBoolean());
        }
        public void AssertUncertain(string reason)
        {
            Assert.Equal(2, Guard.GetProperty("helperExitCode").GetInt32());
            Assert.Equal("Uncertain", Records.Last().GetProperty("state").GetString());
            Assert.Equal(reason, Records.Last().GetProperty("reason").GetString());
            Assert.True(Records.Last().GetProperty("cleanupComplete").GetBoolean());
            Assert.False(Records.Last().GetProperty("authorityRetained").GetBoolean());
        }
        public async Task FinishPrimitive()
        {
            var text = await process.StandardOutput.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); finished = true;
            await File.WriteAllTextAsync(Path.Combine(directory, "primitive.json"), text);
            Assert.Equal(0, process.ExitCode);
            using var result = JsonDocument.Parse(text);
            Assert.Equal(-1, result.RootElement.GetProperty("staleResult").GetInt32());
            Assert.Equal(3, result.RootElement.GetProperty("errno").GetInt32());
            Assert.True(result.RootElement.GetProperty("sentinelAlive").GetBoolean());
            Assert.True(result.RootElement.GetProperty("echild").GetBoolean());
        }
        public async Task FinishUnavailableFixture()
        {
            var text = await process.StandardOutput.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(4));
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4)); finished = true;
            await File.WriteAllTextAsync(Path.Combine(directory, "stderr.log"), await stderr);
            Assert.Equal(78, process.ExitCode);
            Assert.Empty(text);
            using var result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "guardian.json")));
            Assert.True(result.RootElement.GetProperty("unavailable").GetBoolean());
            Assert.Equal(2, result.RootElement.GetProperty("errno").GetInt32());
            Assert.False(result.RootElement.GetProperty("helperCreated").GetBoolean());
            Assert.False(result.RootElement.GetProperty("sentinelCreated").GetBoolean());
            Assert.True(result.RootElement.GetProperty("echild").GetBoolean());
            Assert.Empty(Events());
        }
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!finished) { CloseOwner(); await Finish(); }
            }
            finally { process.Dispose(); }
        }
    }
}

public sealed class LinuxFallbackBootstrapTests
{
    [Fact]
    public async Task RealRootBinding_StartStopAndActualReap()
    {
        await using var run = await LinuxFallbackSupervisorTests.NativeRun.Create();
        await run.Start(); await run.WaitEvent("prepared"); await run.Send("S"); await run.Finish();
        Assert.Equal(15, run.Records.Last().GetProperty("rootSignal").GetInt32());
        Assert.True(run.Records.Last().GetProperty("cleanupComplete").GetBoolean());
        Assert.False(run.Records.Last().GetProperty("authorityRetained").GetBoolean());
        Assert.True(run.Elapsed < TimeSpan.FromSeconds(6));
        Assert.Single(run.Events().Where(e => e.GetProperty("kind").GetString() == "root"));
    }

    [Fact]
    public async Task GuardianAfterHelperLoss_IndependentlyReapsExpiringOwnedRoot()
    {
        await using var run = await LinuxFallbackSupervisorTests.NativeRun.Create(guardianMode: "crash-helper");
        await run.Start(); await run.WaitEvent("prepared"); run.RequestHelperCrash();
        await run.Finish(expectedEmergency: true);
        Assert.Equal(137, run.Guard.GetProperty("helperExitCode").GetInt32());
        Assert.Equal(1, run.Guard.GetProperty("emergencyLineageActions").GetInt32());
        Assert.Equal(1, run.Guard.GetProperty("emergencyAlarmReaps").GetInt32());
        Assert.DoesNotContain(run.Records, r => r.GetProperty("cleanupComplete").GetBoolean());
    }
}

public sealed class LinuxFallbackProcDiscoveryTests
{
    [Fact]
    public Task FirstAdoptedChild_MustRetireBeforeIndependentFixtureExpiry() =>
        new LinuxFallbackSupervisorTests().NativeDescendants_RetireWithinDeclaredScope("tree");
}

public sealed class LinuxFallbackFixtureAdmissionTests
{
    [Fact]
    public async Task MissingRequiredProcInterface_FailsBeforeAnyFixtureChild()
    {
        // Actual open of an absent per-test path. This tests fixture admission,
        // never counts as a successful native ownership/descendant qualification.
        await using var run = await LinuxFallbackSupervisorTests.NativeRun.Create(guardianMode: "missing-proc-fixture");
        await run.FinishUnavailableFixture();
    }
}
