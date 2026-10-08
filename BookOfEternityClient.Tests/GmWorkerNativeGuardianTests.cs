using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerNativeGuardianTests
{
    [Theory]
    [InlineData("normal", false)]
    [InlineData("driver-loss", true)]
    [InlineData("helper-loss", true)]
    [InlineData("held-gate", true)]
    public async Task IndependentGuardian_RetiresOwnedAdopteesAndLeavesForeignSentinel(string mode, bool emergency)
    {
        Assert.True(OperatingSystem.IsLinux(), "Actual Linux guardian qualification required.");
        var evidence = Environment.GetEnvironmentVariable("BOE_NATIVE_EVIDENCE_ROOT") ?? Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "native-host");
        var output = Path.Combine(evidence, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        var build = new ProcessStartInfo("pwsh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoProfile", "-File", Path.Combine(TestRepoPaths.RepoRoot, "scripts", "build-linux-supervisor.ps1"), "-OutputDirectory", output, "-IncludeHostGuardian" }) build.ArgumentList.Add(arg);
        using (var compiler = Process.Start(build)!)
        {
            var log = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(compiler, TimeSpan.FromSeconds(40), Path.Combine(output, "build.log"));
            Assert.True(compiler.ExitCode == 0, "Guardian preparation failure, not behavioral RED: " + log);
        }
        var executable = Path.Combine(output, "host-guardian");
        using var sentinel = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false, ArgumentList = { "--sentinel" } })!;
        try
        {
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { Path.Combine(output, "guardian.json"), "700", executable, "--actor", mode }) start.ArgumentList.Add(arg);
            using var guardian = Process.Start(start)!;
            var log = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(guardian, TimeSpan.FromSeconds(15), Path.Combine(output, "guardian.log"));
            Assert.True(guardian.ExitCode == 0, log);
            using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output, "guardian.json")));
            var r = report.RootElement;
            Assert.True(r.GetProperty("echild").GetBoolean());
            Assert.Equal(0, r.GetProperty("failures").GetInt32());
            Assert.Equal(mode == "held-gate" ? 137 : 0, r.GetProperty("driverExitCode").GetInt32());
            Assert.Equal(emergency, r.GetProperty("emergencySignals").GetInt32() > 0);
            Assert.Equal(mode == "held-gate", r.GetProperty("deadline").GetBoolean());
            Assert.False(sentinel.HasExited, "Guardian must never signal an unrelated sibling process.");
        }
        finally
        {
            if (!sentinel.HasExited) sentinel.Kill(); // original directly owned Process, never a group/tree signal
            await sentinel.WaitForExitAsync();
            await File.WriteAllTextAsync(Path.Combine(output, "sentinel-cleanup.json"), JsonSerializer.Serialize(new { pid = sentinel.Id, exited = sentinel.HasExited, exitCode = sentinel.ExitCode }));
        }
    }
}
