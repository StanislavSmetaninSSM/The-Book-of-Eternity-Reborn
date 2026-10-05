using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerNativeHostTests
{
    [Fact]
    public async Task ActualNeutralHost_ReadyThenOwnerClose_RetiresWithoutWorkerRelease()
    {
        Assert.True(OperatingSystem.IsLinux(), "Actual native host qualification requires Linux.");
        var evidence = Environment.GetEnvironmentVariable("BOE_NATIVE_EVIDENCE_ROOT") ?? Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "native-host");
        var output = Path.Combine(evidence, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        var build = new ProcessStartInfo("pwsh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoProfile", "-File", Path.Combine(TestRepoPaths.RepoRoot, "scripts", "build-linux-supervisor.ps1"), "-OutputDirectory", output, "-IncludeHostGuardian" }) build.ArgumentList.Add(arg);
        using (var compiler = Process.Start(build)!)
        {
            var log = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(compiler, TimeSpan.FromSeconds(40), Path.Combine(output, "build.log"));
            Assert.True(compiler.ExitCode == 0, "Native preparation failure, not behavioral RED: " + log);
        }
        var start = new ProcessStartInfo(Path.Combine(output, "host-guardian")) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        var dotnet = Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet");
        foreach (var arg in new[] { Path.Combine(output, "guardian.json"), "12000", dotnet, typeof(NativeHostScenarioDriver).Assembly.Location, "neutral-ready", output, output }) start.ArgumentList.Add(arg);
        using var guardian = Process.Start(start)!;
        var guardianLog = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(guardian, TimeSpan.FromSeconds(20), Path.Combine(output, "guardian.log"));
        Assert.True(guardian.ExitCode == 0, "Guardian failed: " + guardianLog);
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output, "guardian.json")));
        Assert.True(report.RootElement.GetProperty("echild").GetBoolean());
        Assert.Equal(0, report.RootElement.GetProperty("failures").GetInt32());
        Assert.Equal(0, report.RootElement.GetProperty("driverExitCode").GetInt32());
        Assert.Equal(0, report.RootElement.GetProperty("emergencySignals").GetInt32());
        Assert.False(report.RootElement.GetProperty("deadline").GetBoolean());
        using var scenario = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output, "scenario.json")));
        var result = scenario.RootElement;
        Assert.False(result.GetProperty("workerReleased").GetBoolean());
        Assert.True(result.GetProperty("ready").GetBoolean(), result.GetProperty("failure").ToString());
        Assert.NotEqual(result.GetProperty("hostPid").GetInt32(), result.GetProperty("supervisorPid").GetInt32());
        Assert.Equal(0, result.GetProperty("stop").GetProperty("State").GetInt32());
        Assert.True(result.GetProperty("stop").GetProperty("CleanupComplete").GetBoolean());
        Assert.False(result.GetProperty("stop").GetProperty("AuthorityRetained").GetBoolean());
    }
}
