using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerNativeHostTests
{
    [Fact]
    public async Task ActualNeutralHost_ReadyThenOwnerClose_RetiresWithoutWorkerRelease()
    {
        var result = await RunScenario("neutral-ready");
        Assert.False(result.GetProperty("workerReleased").GetBoolean());
        Assert.True(result.GetProperty("ready").GetBoolean(), result.GetProperty("failure").ToString());
        Assert.NotEqual(result.GetProperty("hostPid").GetInt32(), result.GetProperty("supervisorPid").GetInt32());
        Assert.Equal(0, result.GetProperty("stop").GetProperty("State").GetInt32());
        Assert.True(result.GetProperty("stop").GetProperty("CleanupComplete").GetBoolean());
        Assert.False(result.GetProperty("stop").GetProperty("AuthorityRetained").GetBoolean());
    }

    [Fact]
    public async Task ConstructorFailure_RollsBackPrivateBootstrapBeforeAnyProcessStart()
    {
        var result = await RunScenario("constructor-path");
        Assert.False(result.GetProperty("ready").GetBoolean());
        Assert.False(result.GetProperty("workerReleased").GetBoolean());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("hostPid").ValueKind);
        Assert.NotEmpty(result.GetProperty("failure").ToString());
        Assert.Equal(0, result.GetProperty("bootstrapDirectoriesAfter").GetInt32());
    }

    [Fact]
    public async Task HelperLossAndOutputEof_CannotDisposeLiveHostIdentity()
    {
        var result = await RunScenario("helper-loss-closed-output", allowGuardianEmergency: true);
        Assert.True(result.GetProperty("ready").GetBoolean(), result.GetProperty("failure").ToString());
        Assert.False(result.GetProperty("workerReleased").GetBoolean());
        Assert.Equal(1, result.GetProperty("stop").GetProperty("State").GetInt32());
        Assert.False(result.GetProperty("stop").GetProperty("CleanupComplete").GetBoolean());
        Assert.True(result.GetProperty("hostAliveBeforeDispose").GetBoolean());
        Assert.False(result.GetProperty("disposeAllowed").GetBoolean());
        Assert.False(result.GetProperty("pidfdClosedAfterDispose").GetBoolean());
    }

    private static async Task<JsonElement> RunScenario(string mode, bool allowGuardianEmergency = false)
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
        foreach (var arg in new[] { Path.Combine(output, "guardian.json"), "12000", dotnet, typeof(NativeHostScenarioDriver).Assembly.Location, mode, output, output }) start.ArgumentList.Add(arg);
        using var guardian = Process.Start(start)!;
        var guardianLog = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(guardian, TimeSpan.FromSeconds(20), Path.Combine(output, "guardian.log"));
        Assert.True(guardian.ExitCode == 0, "Guardian failed: " + guardianLog);
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output, "guardian.json")));
        Assert.True(report.RootElement.GetProperty("echild").GetBoolean());
        Assert.Equal(0, report.RootElement.GetProperty("failures").GetInt32());
        Assert.Equal(0, report.RootElement.GetProperty("driverExitCode").GetInt32());
        if (!allowGuardianEmergency) Assert.Equal(0, report.RootElement.GetProperty("emergencySignals").GetInt32());
        Assert.False(report.RootElement.GetProperty("deadline").GetBoolean());
        using var scenario = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output, "scenario.json")));
        return scenario.RootElement.Clone();
    }
}
