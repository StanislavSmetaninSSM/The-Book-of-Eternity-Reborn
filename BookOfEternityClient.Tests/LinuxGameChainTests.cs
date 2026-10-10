using System.Diagnostics;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class LinuxGameChainTests(ITestOutputHelper output)
{
    [Fact]
    public Task Console_ActualRelayTurnColdRestartContinuesExactlyOnce() => RunAsync("console");

    [Fact]
    public Task Console_AddMusingsActualOriginalTurnColdRestartPreservesPrefixAndMirror() => RunAsync("console-musings");

    [Fact]
    public Task Browser_ActualRelayPlayerActionColdRestartContinuesExactlyOnce() => RunAsync("browser-relay");

    [Fact]
    public Task Browser_StagedIdleCrash_BoundedDurationDiagnostic() => RunAsync("browser-relay", durationDiagnostic: true);

    private async Task RunAsync(string mode, bool durationDiagnostic = false)
    {
        Assert.True(OperatingSystem.IsLinux(), "This scenario requires real Linux execution; Windows is not qualified.");
        var repo = TestRepoPaths.RepoRoot;
        var own = Path.Combine("/tmp", "gc-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(own);
        output.WriteLine("Owned chain evidence: " + own);
        var package = Path.Combine(own, "package");
        var ship = Path.Combine(own, "ship");
        async Task Run(string executable, string[] arguments, string logName, int seconds)
        {
            var info = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = repo
            };
            info.Environment.Remove("BOE_TEST_C5_DURATION_DIAGNOSTIC");
            if (durationDiagnostic && logName == "chain.log")
                info.Environment["BOE_TEST_C5_DURATION_DIAGNOSTIC"] = "1";
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            using var process = Process.Start(info)!;
            var text = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(
                process, TimeSpan.FromSeconds(seconds), Path.Combine(own, logName));
            Assert.True(process.ExitCode == 0, "Chain/preparation failure; evidence " + own + Environment.NewLine + text);
        }
        await Run("pwsh", ["-NoLogo", "-NoProfile", "-File", Path.Combine(repo, "scripts/build-linux-supervisor.ps1"),
            "-OutputDirectory", package, "-IncludeHostGuardian"], "native-preparation.log", 20);
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        foreach (var project in new[] { "BookOfEternityClient", "BookOfEternityGMBridge" })
            await Run("dotnet", ["publish", Path.Combine(repo, project, project + ".csproj"), "--no-build", "--no-restore",
                "-c", configuration, "-o", Path.Combine(ship, project), "-p:BoeNativePackageDirectory=" + package,
                "-p:BoeRequireNativePackage=true"], "publish-" + project + ".log", 20);
        var python = mode == "browser-relay" ? Environment.GetEnvironmentVariable("BOE_GAME_CHAIN_BROWSER_PYTHON") : "/usr/bin/python3";
        Assert.False(string.IsNullOrWhiteSpace(python), "Browser relay chain requires existing Python with Playwright.");
        await Run(Path.Combine(package, "host-guardian"), ["--live-turn", Path.Combine(own, "guardian.json"), durationDiagnostic ? "930000" : "300000",
            python!, Path.Combine(repo, "tests/fixtures/LinuxGameChains/console_chain.py"), repo, own, ship, mode],
            "chain.log", durationDiagnostic ? 935 : 310);
        using var result = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(own, "result.json")));
        Assert.True(result.RootElement.GetProperty("PASS").GetBoolean(), result.RootElement.ToString());
        Assert.Equal(0, result.RootElement.GetProperty("ModelCalls").GetInt32());
        Assert.True(result.RootElement.GetProperty("ClientColdRestart").GetBoolean());
        Assert.True(result.RootElement.TryGetProperty("WholeChainColdRestart", out _));
        Assert.Equal(3, result.RootElement.GetProperty("AcceptedTurns").GetArrayLength());
        if (mode == "browser-relay")
        {
            if (durationDiagnostic)
            {
                Assert.True(result.RootElement.GetProperty("DiagnosticOnly").GetBoolean());
                Assert.Equal(3, result.RootElement.GetProperty("TurnDurations").GetArrayLength());
                Assert.All(result.RootElement.GetProperty("TurnDurations").EnumerateArray(), turn =>
                {
                    Assert.True(turn.TryGetProperty("HelperCompleteObservedAtSeconds", out _));
                    Assert.True(turn.TryGetProperty("AcceptanceObservedAtSeconds", out _));
                    Assert.True(turn.GetProperty("HelperToAcceptanceSeconds").GetDouble() <= 180);
                });
            }
            var cuts = result.RootElement.GetProperty("InterruptedColdCuts").EnumerateArray().ToArray();
            Assert.Equal(new[] { "queued", "staged" }, cuts.Select(cut => cut.GetProperty("Phase").GetString()));
            Assert.All(cuts, cut =>
            {
                Assert.NotEqual(cut.GetProperty("OriginalPid").GetInt32(), cut.GetProperty("ColdPid").GetInt32());
                Assert.Equal(-9, cut.GetProperty("OriginalExitCode").GetInt32());
                Assert.True(cut.GetProperty("OriginalEOF").GetBoolean());
                Assert.True(cut.GetProperty("OriginalIdentityConsumedOnce").GetBoolean());
                if (cut.GetProperty("Phase").GetString() == "staged")
                {
                    var witness = cut.GetProperty("IdleCloseWitness");
                    Assert.True(witness.GetProperty("topLevelStagingAwaitReturned").GetBoolean());
                    Assert.True(witness.GetProperty("outsideParticipation").GetBoolean());
                    Assert.True(witness.GetProperty("observed").GetBoolean());
                    Assert.True(witness.GetProperty("remote").GetBoolean());
                    Assert.Equal(0, witness.GetProperty("close").GetProperty("outcome").GetInt32());
                    Assert.False(witness.GetProperty("close").GetProperty("closingFailed").GetBoolean());
                    Assert.Equal(cut.GetProperty("OriginalPid").GetInt32(), witness.GetProperty("pid").GetInt32());
                    Assert.Equal(cut.GetProperty("ActionId").GetString(), witness.GetProperty("requestId").GetString());
                    Assert.Equal(cut.GetProperty("Generation").GetString(), witness.GetProperty("generation").GetString());
                }
            });
        }
        using var guardian = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(own, "guardian.json")));
        Assert.True(guardian.RootElement.GetProperty("echild").GetBoolean());
        Assert.Equal(0, guardian.RootElement.GetProperty("emergencySignals").GetInt32());
        Assert.Equal(0, guardian.RootElement.GetProperty("failures").GetInt32());
        Assert.False(guardian.RootElement.GetProperty("deadline").GetBoolean());
    }
}
