using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmOwnedTerminalLinuxTests
{
    [Fact]
    public async Task OwnedRoot_TwoUnicodeInputsResizeCanonicalEof_ExactScopedCleanup()
    { await RunAsync("terminal-own-root"); }

    [Fact]
    public async Task ActualBridge_PipeDispatchConsumesOriginalTerminalView() { await RunAsync("terminal-bridge"); }

    [Fact]
    public async Task ActualProgram_ForegroundConsoleInputsResizeEofAndScopedStop() { await RunAsync("terminal-foreground"); }

    [Theory]
    [InlineData("terminal-descendants")]
    [InlineData("terminal-root-first")]
    [InlineData("terminal-uncertain")]
    [InlineData("terminal-retirement")]
    [InlineData("terminal-late-fault")]
    [InlineData("terminal-gated-fds")]
    [InlineData("terminal-authority-loss")]
    [InlineData("terminal-partial-start")]
    [InlineData("terminal-root-exit-admission")]
    public async Task OriginalOwner_DescendantsRootExitOrUncertain(string mode) { await RunAsync(mode); }

    internal static async Task RunAsync(string mode,Action<string>? observeOwnFolder=null)
    {
        Assert.True(OperatingSystem.IsLinux(), "This category requires actual Linux native execution.");
        var root = TestRepoPaths.RepoRoot;
        var race = mode is "terminal-main-prepared-stop-race" or "terminal-main-loss-prepared" or "terminal-main-loss-active";
        var soak = mode == "terminal-main-consumer-pidfd-soak";
        var consumer = mode is "terminal-main-consumer-fifo" or "terminal-main-consumer-load-fifo" or "terminal-main-prepared-stop-race";
        var folder = Path.Combine(TestDiagnosticPaths.Root, "native-terminal", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        observeOwnFolder?.Invoke(folder);
        var build = new ProcessStartInfo("pwsh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoProfile", "-File", Path.Combine(root, "scripts/build-linux-supervisor.ps1"),
            "-OutputDirectory", folder, "-IncludeHostGuardian", "-IncludeTerminalFixture" }) build.ArgumentList.Add(arg);
        if (consumer || soak || race) { build.ArgumentList.Add("-TerminalFixtureLifetimeSeconds"); build.ArgumentList.Add(soak ? "180" : race ? "45" : "25"); }
        using (var compiler = Process.Start(build)!)
        {
            var log = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(compiler, TimeSpan.FromSeconds(40), Path.Combine(folder, "build.log"));
            Assert.True(compiler.ExitCode == 0, "Preparation failure, not causal RED: " + log);
        }
        var foreground=mode=="terminal-foreground";
        var start = new ProcessStartInfo(foreground?"python3":Path.Combine(folder, "host-guardian")) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        var configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var arguments=foreground ? new[] {Path.Combine(root,"tests/fixtures/LinuxTerminal/foreground.py"),root,folder,
            Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!,"dotnet"),Path.Combine(root,"BookOfEternityGMBridge/bin",configuration,"net8.0/BookOfEternityGMBridge.dll")} : new[] { Path.Combine(folder, "guardian.json"), soak ? "170000" : race ? "50000" : consumer ? "30000" : "15000",
            Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"),
            typeof(NativeHostScenarioDriver).Assembly.Location, mode, folder, folder };
        // Explicit guardian budget selector only: the soak contains no game or GM.
        if (soak || race) start.ArgumentList.Add("--live-turn");
        foreach (var arg in arguments) start.ArgumentList.Add(arg);
        using var guardian = Process.Start(start)!;
        await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(guardian, TimeSpan.FromSeconds(soak ? 175 : race ? 55 : consumer ? 35 : 20), Path.Combine(folder, "guardian.log"));
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "guardian.json")));
        Assert.True(report.RootElement.GetProperty("echild").GetBoolean());
        Assert.Equal(0, report.RootElement.GetProperty("emergencySignals").GetInt32());
        Assert.Equal(0, report.RootElement.GetProperty("failures").GetInt32());
        Assert.False(report.RootElement.GetProperty("deadline").GetBoolean());
        Assert.Equal(0, guardian.ExitCode);
        Assert.Equal(0, report.RootElement.GetProperty("driverExitCode").GetInt32());
        using var scenario = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "scenario.json")));
        if(mode.StartsWith("terminal-inherited-control-", StringComparison.Ordinal)) { Assert.True(scenario.RootElement.GetProperty("Success").GetBoolean()); return; }
        if(foreground) {
            foreach(var proof in new[]{"Success","TwoActualConsoleInputsOneSession","ActualConsoleResize","CanonicalEofRootAlive","ActualScopedStop","OuterInputModeRestored"})Assert.True(scenario.RootElement.GetProperty(proof).GetBoolean());return;
        }
        if(mode=="terminal-environment" || mode.StartsWith("terminal-main-",StringComparison.Ordinal) || mode.StartsWith("terminal-systemd-",StringComparison.Ordinal)){Assert.True(scenario.RootElement.GetProperty("Success").GetBoolean());return;}
        if (mode == "terminal-retirement") { Assert.True(scenario.RootElement.GetProperty("OwnerHeldBeforeEof").GetBoolean()); return; }
        if (mode == "terminal-late-fault") { Assert.True(scenario.RootElement.GetProperty("LateFaultUncertain").GetBoolean()); return; }
        if (mode == "terminal-gated-fds") Assert.True(scenario.RootElement.GetProperty("HeldRootHasNoHelperChannels").GetBoolean());
        if(mode=="terminal-partial-start") { Assert.True(scenario.RootElement.GetProperty("PartialExceptionOriginal").GetBoolean());Assert.True(scenario.RootElement.GetProperty("PartialOwnerRetained").GetBoolean());return; }
        if(mode=="terminal-root-exit-admission") { Assert.True(scenario.RootElement.GetProperty("RootExitAdmissionClosed").GetBoolean());Assert.True(scenario.RootElement.GetProperty("ScopedRetired").GetBoolean());return; }
        if (mode == "terminal-authority-loss") { Assert.True(scenario.RootElement.GetProperty("LiveAuthorityLossBlocked").GetBoolean()); return; }
        if (mode == "terminal-uncertain") { Assert.True(scenario.RootElement.GetProperty("UncertainRetained").GetBoolean()); return; }
        if (mode is "terminal-bridge" or "terminal-fence") {
            foreach(var proof in new[]{"ScopedRetired","TwoDispatchesOneSession","DraftPreserved","CancelledViaPipe","TakeoverViaPipe","ActualResizeViaPipe","ScopedStopViaPipe"}) Assert.True(scenario.RootElement.GetProperty(proof).GetBoolean()); return;
        }
        Assert.True(scenario.RootElement.GetProperty("TwoInputs").GetBoolean());
        Assert.True(scenario.RootElement.GetProperty("Resize").GetBoolean());
        Assert.True(scenario.RootElement.GetProperty("EofStillAlive").GetBoolean());
        if(mode is "terminal-descendants" or "terminal-root-first")Assert.True(scenario.RootElement.GetProperty("UnrelatedOwnSentinelSurvived").GetBoolean());
        Assert.Equal("StoppedWithinScope", scenario.RootElement.GetProperty("StopState").GetString());
    }
}
