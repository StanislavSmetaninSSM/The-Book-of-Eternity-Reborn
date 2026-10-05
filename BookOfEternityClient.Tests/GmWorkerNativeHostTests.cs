using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerNativeHostTests
{
    [Theory]
    [InlineData("bootstrap-close", "bootstrap-lost")]
    [InlineData("bootstrap-wrong-ack", "bootstrap-invalid")]
    public async Task GatedBootstrapLoss_RetiresWithoutExecutingRoot(string mode, string reason)
    {
        var result = await RunScenario(mode);
        Assert.True(result.GetProperty("bound").GetBoolean(), result.GetProperty("failure").ToString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("failure").ValueKind);
        Assert.False(result.GetProperty("rootExecuted").GetBoolean());
        Assert.Equal(2, result.GetProperty("helperExitCode").GetInt32());
        var frames = result.GetProperty("frames").EnumerateArray().ToArray();
        Assert.DoesNotContain(frames, f => f.GetProperty("state").GetString() == "Started");
        Assert.Equal("Uncertain", frames[^1].GetProperty("state").GetString());
        Assert.Equal(reason, frames[^1].GetProperty("reason").GetString());
        Assert.True(frames[^1].GetProperty("cleanupComplete").GetBoolean());
        Assert.Equal("", result.GetProperty("hostStdout").GetString());
        Assert.Equal("", result.GetProperty("hostStderr").GetString());
    }

    [Fact]
    public async Task PreCanceledPreparation_RejectsBeforeAllocatingNativeOwner()
    {
        var result = await RunScenario("pre-canceled");
        Assert.True(result.GetProperty("canceled").GetBoolean(), result.GetProperty("failure").ToString());
        Assert.False(result.GetProperty("ready").GetBoolean());
        Assert.False(result.GetProperty("workerReleased").GetBoolean());
        foreach (var name in new[] { "hostPid", "supervisorPid", "stop" }) Assert.Equal(JsonValueKind.Null, result.GetProperty(name).ValueKind);
    }

    [Fact]
    public async Task HostOutput_IsDistinctAndPrivateDescriptorsDoNotSurviveExec()
    {
        var result = await RunScenario("output-audit");
        Assert.True(result.GetProperty("ready").GetBoolean(), result.GetProperty("failure").ToString());
        Assert.False(result.GetProperty("workerReleased").GetBoolean());
        Assert.Equal(0, result.GetProperty("stop").GetProperty("State").GetInt32());
        Assert.True(result.GetProperty("stop").GetProperty("CleanupComplete").GetBoolean());
        Assert.Equal("fixture-host-stdout\n", result.GetProperty("hostStdout").GetString());
        Assert.Equal("fixture-host-stderr\n", result.GetProperty("hostStderr").GetString());
        var audit = result.GetProperty("fdAudit");
        Assert.Equal(0, audit.GetProperty("extraDescriptors").GetInt32());
        Assert.True(audit.GetProperty("stdinDevNull").GetBoolean());
        Assert.True(audit.GetProperty("distinctOutputPipes").GetBoolean());
    }

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

    [Theory]
    [InlineData("foreign-control")]
    [InlineData("foreign-status")]
    public async Task NativeIdentity_RejectsForeignNamedChannelBeforeLaunch(string mode)
    {
        var result = await RunScenario(mode);
        Assert.False(result.GetProperty("ready").GetBoolean());
        Assert.False(result.GetProperty("workerReleased").GetBoolean());
        Assert.Contains("unexpected process", result.GetProperty("failure").ToString());
        Assert.Equal(0, result.GetProperty("stop").GetProperty("State").GetInt32());
        Assert.True(result.GetProperty("stop").GetProperty("CleanupComplete").GetBoolean());
        Assert.True(result.GetProperty("disposeAllowed").GetBoolean());
    }

    [Fact]
    public async Task CancellationAfterNativeBinding_RetainsOwnerThroughScopedRetirement()
    {
        var result = await RunScenario("cancel-before-ready");
        Assert.False(result.GetProperty("ready").GetBoolean());
        Assert.True(result.GetProperty("canceled").GetBoolean(), result.GetProperty("failure").ToString());
        Assert.False(result.GetProperty("workerReleased").GetBoolean());
        Assert.Equal(0, result.GetProperty("stop").GetProperty("State").GetInt32());
        Assert.True(result.GetProperty("stop").GetProperty("CleanupComplete").GetBoolean());
        Assert.True(result.GetProperty("disposeAllowed").GetBoolean());
    }

    [Theory]
    [InlineData("owner-eof")]
    [InlineData("status-loss")]
    [InlineData("exec-failure")]
    public async Task AuthorityOrExecLoss_RemainsUncertainAfterLaterObservation(string mode)
    {
        var result = await RunScenario(mode);
        Assert.Equal(mode != "exec-failure", result.GetProperty("ready").GetBoolean());
        Assert.False(result.GetProperty("workerReleased").GetBoolean());
        Assert.Equal(1, result.GetProperty("stop").GetProperty("State").GetInt32());
        Assert.Equal(1, result.GetProperty("laterStop").GetProperty("State").GetInt32());
        Assert.False(result.GetProperty("disposeAllowed").GetBoolean());
        Assert.False(result.GetProperty("pidfdClosedAfterDispose").GetBoolean());
    }

    [Fact]
    public async Task NeutralHostCapability_RejectsReleaseBeforeSendingAnyWorkerCommand()
    {
        var result = await RunScenario("release-denied");
        Assert.True(result.GetProperty("ready").GetBoolean(), result.GetProperty("failure").ToString());
        Assert.True(result.GetProperty("releaseDenied").GetBoolean());
        Assert.False(result.GetProperty("workerReleased").GetBoolean());
        Assert.Equal(0, result.GetProperty("stop").GetProperty("State").GetInt32());
        Assert.True(result.GetProperty("stop").GetProperty("CleanupComplete").GetBoolean());
    }

    [Fact]
    public async Task PublishedRelocatedPackage_StartsRealNeutralHostWithEmptyPath()
    {
        var result = await RunScenario("published-ready");
        Assert.True(result.GetProperty("ready").GetBoolean(), result.GetProperty("failure").ToString());
        Assert.False(result.GetProperty("workerReleased").GetBoolean());
        Assert.Equal(0, result.GetProperty("stop").GetProperty("State").GetInt32());
        Assert.True(result.GetProperty("stop").GetProperty("CleanupComplete").GetBoolean());
        Assert.True(result.GetProperty("disposeAllowed").GetBoolean());
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
        var driver = typeof(NativeHostScenarioDriver).Assembly.Location;
        if (mode == "published-ready")
        {
            var published = Path.Combine(output, "published");
            var relocated = Path.Combine(output, "relocated");
            var publish = new ProcessStartInfo(dotnet) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "publish", Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "BookOfEternityClient.csproj"),
                "--no-build", "--no-restore", "--disable-build-servers", "-c", "Debug", "-o", published,
                "-p:BoeNativePackageDirectory=" + output, "-p:BoeRequireNativePackage=true" }) publish.ArgumentList.Add(arg);
            using (var packager = Process.Start(publish)!)
            {
                var log = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(packager, TimeSpan.FromSeconds(45), Path.Combine(output, "publish.log"));
                Assert.True(packager.ExitCode == 0, "Publish preparation failure: " + log);
            }
            foreach (var suffix in new[] { ".dll", ".deps.json", ".runtimeconfig.json" })
                File.Copy(Path.ChangeExtension(driver, suffix), Path.Combine(published, "BookOfEternityClient.TestSupport" + suffix));
            Directory.Move(published, relocated);
            driver = Path.Combine(relocated, "BookOfEternityClient.TestSupport.dll");
            var assets = Path.Combine(relocated, "runtimes", "linux-x64", "native");
            Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(output, "boe-lineage-supervisor")), await File.ReadAllBytesAsync(Path.Combine(assets, "boe-lineage-supervisor")));
            Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(output, "package-manifest.json")), await File.ReadAllBytesAsync(Path.Combine(assets, "package-manifest.json")));
            start.Environment["PATH"] = ""; // Runtime must use only the prebuilt package and absolute host executable.
            await File.WriteAllTextAsync(Path.Combine(output, "publish-layout.json"), JsonSerializer.Serialize(new { relocated = true, defaultPackagePath = assets, emptyRuntimePath = true, runtimeCompilation = false }));
        }
        foreach (var arg in new[] { Path.Combine(output, "guardian.json"), "12000", dotnet, driver, mode, output, output }) start.ArgumentList.Add(arg);
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
