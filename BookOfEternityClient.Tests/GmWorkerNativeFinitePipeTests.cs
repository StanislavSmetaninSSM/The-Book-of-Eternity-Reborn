using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Qualifies finite pipe ownership on Linux under the existing independent guardian.
/// </summary>
public sealed class GmWorkerNativeFinitePipeTests
{
    /// <summary>
    /// Checks real root exit, independent pre-exec identity and complete natural retirement.
    /// </summary>
    /// <param name="mode">
    /// The finite fixture behavior selected in the isolated driver.
    /// </param>
    /// <param name="exitCode">
    /// The expected root exit code, distinct from supervisor exit zero.
    /// </param>
    [Theory]
    [InlineData("zero", 0)]
    [InlineData("seventy-three", 73)]
    [InlineData("fast", 0)]
    [InlineData("output", 0)]
    [InlineData("natural-stop-race", 0)]
    public async Task FiniteRoot_RetiresThroughOriginalOwner(string mode, int exitCode)
    {
        var result = await RunScenarioAsync(mode);
        Assert.Null(result.GetProperty("failure").GetString());
        Assert.True(result.GetProperty("heldObserved").GetBoolean());
        Assert.True(result.GetProperty("heldBeforeExec").GetBoolean());
        Assert.True(result.GetProperty("sameOriginalOwner").GetBoolean());
        if (mode == "fast") Assert.True(result.GetProperty("heldBirth").GetUInt64() > 0);
        else Assert.True(result.GetProperty("birthMatches").GetBoolean());
        Assert.NotEqual(result.GetProperty("rootPid").GetInt32(), result.GetProperty("supervisorPid").GetInt32());
        var stop = result.GetProperty("stop");
        Assert.Equal(0, stop.GetProperty("State").GetInt32());
        Assert.Equal(exitCode, stop.GetProperty("RootExitCode").GetInt32());
        Assert.True(stop.GetProperty("CleanupComplete").GetBoolean());
        Assert.False(stop.GetProperty("AuthorityRetained").GetBoolean());
        Assert.True(result.GetProperty("disposed").GetBoolean());
        Assert.True(result.GetProperty("eof").GetBoolean());
        Assert.True(result.GetProperty("bootstrapRemoved").GetBoolean());
        if (mode == "natural-stop-race")
        {
            Assert.True(result.GetProperty("raceControlAttempted").GetBoolean());
            Assert.True(result.GetProperty("raceControlFaulted").GetBoolean());
            Assert.Equal("root-exited", stop.GetProperty("Reason").GetString());
            Assert.Null(result.GetProperty("uncertainty").GetString());
        }
        if (mode == "output")
        {
            Assert.Equal(new string('O', 131072), result.GetProperty("stdout").GetString());
            Assert.Equal(new string('E', 131072), result.GetProperty("stderr").GetString());
            Assert.True(result.GetProperty("environmentMatches").GetBoolean());
        }
    }

    /// <summary>
    /// A timed wait stops only through its original owner and observes output completion.
    /// </summary>
    [Fact]
    public async Task Timeout_StopsThroughOriginalOwner()
    {
        var result = await RunScenarioAsync("timeout");
        Assert.Equal("TimeoutException", result.GetProperty("failure").GetString());
        Assert.True(result.GetProperty("sameOriginalOwner").GetBoolean());
        Assert.Equal(0, result.GetProperty("stop").GetProperty("State").GetInt32());
        Assert.True(result.GetProperty("stop").GetProperty("CleanupComplete").GetBoolean());
        Assert.True(result.GetProperty("eof").GetBoolean());
        Assert.True(result.GetProperty("disposed").GetBoolean());
    }

    /// <summary>
    /// Partial launch and lost observation retain uncertainty even after output EOF.
    /// </summary>
    /// <param name="mode">
    /// The negative fixture action, applied only to its own retained process or observation.
    /// </param>
    [Theory]
    [InlineData("held-error")]
    [InlineData("start-failure")]
    [InlineData("exec-failure")]
    [InlineData("control-loss")]
    [InlineData("status-loss")]
    [InlineData("malformed-terminal")]
    public async Task LostAuthority_RetainsOriginalOwner(string mode)
    {
        var result = await RunScenarioAsync(mode);
        Assert.True(result.GetProperty("sameOriginalOwner").GetBoolean());
        Assert.Equal(1, result.GetProperty("stop").GetProperty("State").GetInt32());
        Assert.False(result.GetProperty("disposed").GetBoolean());
        Assert.False(result.GetProperty("bootstrapRemoved").GetBoolean());
        Assert.NotNull(result.GetProperty("uncertainty").GetString());
        Assert.True(result.GetProperty("eof").GetBoolean());
        if (mode == "start-failure")
        {
            Assert.False(result.GetProperty("heldObserved").GetBoolean());
            Assert.Equal(JsonValueKind.Null, result.GetProperty("rootPid").ValueKind);
        }
    }

    /// <summary>
    /// Runs a fixture with a previously built package and validates the guardian's own cleanup.
    /// </summary>
    /// <param name="mode">
    /// The isolated finite scenario name.
    /// </param>
    /// <returns>
    /// The driver's scalar and stream evidence; game qualification is outside this fixture.
    /// </returns>
    private static async Task<JsonElement> RunScenarioAsync(string mode)
    {
        Assert.True(OperatingSystem.IsLinux(), "Finite native qualification requires Linux.");
        var package = Environment.GetEnvironmentVariable("BOE_CRASH5_NATIVE_PACKAGE")
            ?? throw new InvalidOperationException("Prepare BOE_CRASH5_NATIVE_PACKAGE before native controls.");
        Assert.True(Path.IsPathFullyQualified(package));
        _ = GmWorkerNativePackage.Validate(package);
        var own = Path.Combine(TestDiagnosticPaths.Root, "native-finite", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(own);
        var dotnet = Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")
            ?? throw new InvalidOperationException("DOTNET_ROOT is required."), "dotnet");
        var start = new ProcessStartInfo(Path.Combine(package, "host-guardian"))
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { Path.Combine(own, "guardian.json"), "20000", dotnet,
            typeof(NativeHostScenarioDriver).Assembly.Location, "finite-pipe-" + mode, package, own })
            start.ArgumentList.Add(arg);
        using var guardian = Process.Start(start) ?? throw new InvalidOperationException("Guardian did not start.");
        var log = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(guardian, TimeSpan.FromSeconds(25), Path.Combine(own, "guardian.log"));
        Assert.True(guardian.ExitCode == 0, log);
        using var guardianReport = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(own, "guardian.json")));
        Assert.True(guardianReport.RootElement.GetProperty("echild").GetBoolean());
        Assert.Equal(0, guardianReport.RootElement.GetProperty("failures").GetInt32());
        Assert.Equal(0, guardianReport.RootElement.GetProperty("driverExitCode").GetInt32());
        Assert.Equal(0, guardianReport.RootElement.GetProperty("emergencySignals").GetInt32());
        Assert.False(guardianReport.RootElement.GetProperty("deadline").GetBoolean());
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(own, "scenario.json")));
        return report.RootElement.Clone();
    }
}
