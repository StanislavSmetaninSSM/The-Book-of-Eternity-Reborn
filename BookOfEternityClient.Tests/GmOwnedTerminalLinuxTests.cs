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

    private static async Task RunAsync(string mode)
    {
        Assert.True(OperatingSystem.IsLinux(), "This category requires actual Linux native execution.");
        var root = TestRepoPaths.RepoRoot;
        var folder = Path.Combine(root, "TestResults/native-terminal", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var build = new ProcessStartInfo("pwsh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoProfile", "-File", Path.Combine(root, "scripts/build-linux-supervisor.ps1"),
            "-OutputDirectory", folder, "-IncludeHostGuardian", "-IncludeTerminalFixture" }) build.ArgumentList.Add(arg);
        using (var compiler = Process.Start(build)!)
        {
            var log = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(compiler, TimeSpan.FromSeconds(40), Path.Combine(folder, "build.log"));
            Assert.True(compiler.ExitCode == 0, "Preparation failure, not causal RED: " + log);
        }
        var start = new ProcessStartInfo(Path.Combine(folder, "host-guardian")) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { Path.Combine(folder, "guardian.json"), "15000",
            Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"),
            typeof(NativeHostScenarioDriver).Assembly.Location, mode, folder, folder }) start.ArgumentList.Add(arg);
        using var guardian = Process.Start(start)!;
        await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(guardian, TimeSpan.FromSeconds(20), Path.Combine(folder, "guardian.log"));
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "guardian.json")));
        Assert.True(report.RootElement.GetProperty("echild").GetBoolean());
        Assert.Equal(0, report.RootElement.GetProperty("emergencySignals").GetInt32());
        Assert.Equal(0, report.RootElement.GetProperty("failures").GetInt32());
        Assert.False(report.RootElement.GetProperty("deadline").GetBoolean());
        Assert.Equal(0, guardian.ExitCode);
        Assert.Equal(0, report.RootElement.GetProperty("driverExitCode").GetInt32());
        using var scenario = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "scenario.json")));
        if (mode == "terminal-bridge") { Assert.True(scenario.RootElement.GetProperty("ScopedRetired").GetBoolean()); return; }
        Assert.True(scenario.RootElement.GetProperty("TwoInputs").GetBoolean());
        Assert.True(scenario.RootElement.GetProperty("Resize").GetBoolean());
        Assert.True(scenario.RootElement.GetProperty("EofStillAlive").GetBoolean());
        Assert.Equal("StoppedWithinScope", scenario.RootElement.GetProperty("StopState").GetString());
    }
}
