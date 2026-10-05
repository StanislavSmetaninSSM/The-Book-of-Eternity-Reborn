using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerNativePoolTests
{
    [Fact]
    public async Task ActualPool_SyntheticAnalysisPublishesAfterOwnedRetirement()
    {
        var result = await RunScenario("pool-happy");
        Assert.True(result.GetProperty("success").GetBoolean(), result.GetProperty("failure").ToString());
        Assert.Equal(1, result.GetProperty("releases").GetInt32());
        Assert.Equal(1, result.GetProperty("workerStarts").GetInt32());
        Assert.Equal(1, result.GetProperty("publicationCalls").GetInt32());
        Assert.True(result.GetProperty("workspaceCleaned").GetBoolean());
        Assert.True(result.GetProperty("canonicalContextUnchanged").GetBoolean());
        Assert.True(result.GetProperty("validatedExecution").GetBoolean());
        Assert.True(result.GetProperty("proposalBytesMatch").GetBoolean());
        Assert.True(result.GetProperty("stagingCleaned").GetBoolean());
        var checks = result.GetProperty("permitChecks").EnumerateObject().ToArray();
        Assert.Equal(14, checks.Length);
        foreach (var check in checks) Assert.True(check.Value.GetBoolean(), check.Name);
        Assert.Equal(0, result.GetProperty("reaperEntries").GetInt32());
        Assert.Equal(0, result.GetProperty("reaperCapacity").GetInt32());
        Assert.Contains("pool-worker-stdout", result.GetProperty("result").GetProperty("StandardOutput").GetString());
        Assert.Contains("pool-worker-stderr", result.GetProperty("result").GetProperty("StandardError").GetString());
        var execution = result.GetProperty("result");
        Assert.True(execution.GetProperty("OutputsSettled").GetBoolean());
        Assert.True(execution.GetProperty("StopEvidence").GetProperty("CleanupComplete").GetBoolean());
        Assert.False(execution.GetProperty("StopEvidence").GetProperty("AuthorityRetained").GetBoolean());
        Assert.Equal(execution.GetProperty("ExecutionIdentity").GetProperty("RunId").GetString(),
            execution.GetProperty("StopEvidence").GetProperty("RunId").GetString());
    }

    private static async Task<JsonElement> RunScenario(string mode, bool allowGuardianEmergency = false)
    {
        Assert.True(OperatingSystem.IsLinux(), "Actual native pool qualification requires Linux.");
        var evidence = Environment.GetEnvironmentVariable("BOE_NATIVE_EVIDENCE_ROOT") ?? Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "native-pool");
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
        foreach (var arg in new[] { Path.Combine(output, "guardian.json"), "30000",
            Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"),
            typeof(NativePoolScenarioDriver).Assembly.Location, mode, output, output }) start.ArgumentList.Add(arg);
        using var guardian = Process.Start(start)!;
        var guardianLog = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(guardian, TimeSpan.FromSeconds(40), Path.Combine(output, "guardian.log"));
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
