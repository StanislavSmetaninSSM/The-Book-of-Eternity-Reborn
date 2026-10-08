using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRestartAdmissionTests
{
    [Fact]
    public async Task ColdPrepared_ActualPoolRefusesBeforeRecoveryCapacityAndLaunch()
    {
        var fixture = await RestartFixture.Create();
        await fixture.Run("restart-seed-cold", 77);
        using (var cut = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "restart-cut.json"))))
        {
            Assert.Equal("MemberPublished", cut.RootElement.GetProperty("phase").GetString());
            Assert.Equal("Prepared", cut.RootElement.GetProperty("ledgerPhase").GetString());
        }
        await fixture.Run("restart-cold-run", 0);
        using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "restart-result.json")));
        var result = report.RootElement;
        Assert.True(result.GetProperty("workspaceCleaned").GetBoolean(), result.ToString());
        Assert.Equal(0, result.GetProperty("reaperEntries").GetInt32());
        Assert.Equal(0, result.GetProperty("reaperCapacity").GetInt32());
        Assert.Equal("Uncertain", result.GetProperty("ledgerKind").GetString());
        Assert.Equal("Prepared", Assert.Single(result.GetProperty("phases").EnumerateArray()).GetString());
        Assert.True(result.GetProperty("recoveryObservations").GetInt32() == 0 &&
            result.GetProperty("reservationCalls").GetInt32() == 0 &&
            result.GetProperty("boundOwners").GetInt32() == 0 &&
            result.GetProperty("releases").GetInt32() == 0 &&
            result.GetProperty("workerStarts").GetInt32() == 0 &&
            result.GetProperty("publicationCalls").GetInt32() == 0 &&
            !result.GetProperty("actualSuccess").GetBoolean() &&
            !result.GetProperty("proposalPublished").GetBoolean() &&
            result.GetProperty("preservedRoot").GetBoolean(),
            "Cold Prepared must fence the actual pool before recovery/reservation/launch. Evidence: " + result);
    }

    internal sealed class RestartFixture
    {
        internal string Output { get; } = Path.Combine(Environment.GetEnvironmentVariable("BOE_RESTART_EVIDENCE_ROOT") ??
            Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "worker-restart"), Guid.NewGuid().ToString("N"));
        private int _runs;
        internal static async Task<RestartFixture> Create()
        {
            Assert.True(OperatingSystem.IsLinux(), "Connected R2 qualification requires Linux and the independent native guardian.");
            var fixture = new RestartFixture(); Directory.CreateDirectory(fixture.Output);
            File.WriteAllText(Path.Combine(fixture.Output, "outside.txt"), "outside");
            var build = new ProcessStartInfo("pwsh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-NoProfile", "-File", Path.Combine(TestRepoPaths.RepoRoot, "scripts", "build-linux-supervisor.ps1"),
                "-OutputDirectory", fixture.Output, "-IncludeHostGuardian" }) build.ArgumentList.Add(arg);
            using (var compiler = Process.Start(build)!)
            {
                var log = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(compiler, TimeSpan.FromSeconds(40), Path.Combine(fixture.Output, "build.log"));
                Assert.True(compiler.ExitCode == 0, "Native preparation failure, not behavioral RED: " + log);
            }
            var driver = typeof(NativePoolScenarioDriver).Assembly.Location;
            var client = typeof(BookOfEternityClient.Services.GmWorkers.GmWorkerBridgePool).Assembly.Location;
            File.WriteAllText(Path.Combine(fixture.Output, "managed-provenance.json"), JsonSerializer.Serialize(new
            { driver, driverSha256 = Hash(driver), client, clientSha256 = Hash(client) }));
            return fixture;
        }
        internal async Task Run(string mode, int expectedExit, bool allowGuardianCleanup = false)
        {
            var id = (++_runs).ToString("D2");
            var start = new ProcessStartInfo(Path.Combine(Output, "host-guardian"))
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { Path.Combine(Output, id + "-guardian.json"), "30000",
                Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"),
                typeof(NativePoolScenarioDriver).Assembly.Location, mode, Output, Output }) start.ArgumentList.Add(arg);
            File.WriteAllText(Path.Combine(Output, id + "-command.json"), JsonSerializer.Serialize(new { start.FileName, arguments = start.ArgumentList.ToArray() }));
            using var guardian = Process.Start(start)!;
            var log = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(guardian, TimeSpan.FromSeconds(40), Path.Combine(Output, id + "-guardian.log"));
            Assert.True(guardian.ExitCode == 0, "Guardian preparation/cleanup failure: " + log);
            using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Output, id + "-guardian.json")));
            Assert.True(report.RootElement.GetProperty("echild").GetBoolean());
            Assert.Equal(0, report.RootElement.GetProperty("failures").GetInt32());
            if (!allowGuardianCleanup) Assert.Equal(0, report.RootElement.GetProperty("emergencySignals").GetInt32());
            Assert.False(report.RootElement.GetProperty("deadline").GetBoolean());
            Assert.Equal(expectedExit, report.RootElement.GetProperty("driverExitCode").GetInt32());
            Assert.Equal("outside", File.ReadAllText(Path.Combine(Output, "outside.txt")));
        }
        private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }
}
