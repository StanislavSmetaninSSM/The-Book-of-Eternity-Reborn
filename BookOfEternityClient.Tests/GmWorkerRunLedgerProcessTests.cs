using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRunLedgerProcessTests
{
    [Fact]
    public async Task FiniteGuardian_RealInitializationThenFreshProcessObservation()
    {
        using var fixture = await ProcessFixture.Create();
        await fixture.Write("initialize", null, 0);
        var observed = await fixture.Observe();
        Assert.Equal("Quiescent", observed.GetProperty("kind").GetString());
        Assert.Equal(1, observed.GetProperty("Sequence").GetInt64());
        Assert.False(observed.GetProperty("runtimeBaseCreated").GetBoolean());
    }

    internal sealed class ProcessFixture : IDisposable
    {
        internal string Output { get; } = Path.Combine(Environment.GetEnvironmentVariable("BOE_LEDGER_EVIDENCE_ROOT") ??
            Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "worker-ledger-process"), Guid.NewGuid().ToString("N"));
        internal WorkerLedgerTarget Target { get; }
        private int _runs;
        private ProcessFixture() { Target = new(Path.Combine(Output, "root")); Directory.CreateDirectory(Target.RootPath); File.WriteAllText(Path.Combine(Output, "outside.txt"), "outside"); }
        internal static async Task<ProcessFixture> Create()
        {
            Assert.True(OperatingSystem.IsLinux(), "This category requires the actual Linux metadata adapter.");
            var fixture = new ProcessFixture();
            var source = Path.Combine(TestRepoPaths.RepoRoot, "tests", "fixtures", "LinuxHost", "host-guardian.c");
            var flags = new[] { "-std=c11", "-O2", "-g", "-Wall", "-Wextra", "-Werror", "-D_FORTIFY_SOURCE=2", "-fstack-protector-strong", "-Wl,-z,relro,-z,now" };
            var compiler = new ProcessStartInfo("cc") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in flags.Concat(new[] { source, "-o", Path.Combine(fixture.Output, "host-guardian") })) compiler.ArgumentList.Add(arg);
            using (var process = Process.Start(compiler)!)
            {
                var log = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(process, TimeSpan.FromSeconds(15), Path.Combine(fixture.Output, "build.log"));
                Assert.True(process.ExitCode == 0, "Guardian preparation failure, not behavioral RED: " + log);
            }
            var versionInfo = new ProcessStartInfo("cc") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true }; versionInfo.ArgumentList.Add("--version");
            using var version = Process.Start(versionInfo)!;
            var compilerVersion = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(version, TimeSpan.FromSeconds(5), Path.Combine(fixture.Output, "compiler.log"));
            Assert.Equal(0, version.ExitCode);
            File.WriteAllText(Path.Combine(fixture.Output, "provenance.json"), JsonSerializer.Serialize(new
            { source, sourceSha256 = Hash(source), binarySha256 = Hash(Path.Combine(fixture.Output, "host-guardian")), compilerVersion, flags,
                driver = typeof(WorkerRunLedgerScenarioDriver).Assembly.Location, driverSha256 = Hash(typeof(WorkerRunLedgerScenarioDriver).Assembly.Location),
                ledgerAssemblySha256 = Hash(typeof(GmWorkerRunLedger).Assembly.Location) }));
            return fixture;
        }
        internal async Task Run(string mode, int expectedExit)
        {
            var id = (++_runs).ToString("D2");
            var start = new ProcessStartInfo(Path.Combine(Output, "host-guardian")) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { Path.Combine(Output, id + "-guardian.json"), "7000", Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"),
                typeof(WorkerRunLedgerScenarioDriver).Assembly.Location, mode, Target.RootPath, Output }) start.ArgumentList.Add(arg);
            File.WriteAllText(Path.Combine(Output, id + "-command.json"), JsonSerializer.Serialize(new { start.FileName, arguments = start.ArgumentList.ToArray() }));
            using var guardian = Process.Start(start)!;
            var log = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(guardian, TimeSpan.FromSeconds(12), Path.Combine(Output, id + "-guardian.log"));
            Assert.True(guardian.ExitCode == 0, "Guardian preparation/cleanup failure: " + log);
            using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Output, id + "-guardian.json")));
            Assert.True(report.RootElement.GetProperty("echild").GetBoolean());
            Assert.Equal(0, report.RootElement.GetProperty("failures").GetInt32());
            Assert.Equal(0, report.RootElement.GetProperty("emergencySignals").GetInt32());
            Assert.False(report.RootElement.GetProperty("deadline").GetBoolean());
            Assert.Equal(expectedExit, report.RootElement.GetProperty("driverExitCode").GetInt32());
            Assert.Equal("outside", File.ReadAllText(Path.Combine(Output, "outside.txt")));
        }
        internal async Task Write(string operation, string? cut, int exit)
        { File.WriteAllText(Path.Combine(Output, "request.json"), JsonSerializer.Serialize(new { operation, cut })); await Run("ledger-write", exit); }
        internal async Task<JsonElement> Observe()
        {
            var before = Snapshot(); await Run("ledger-observe", 0); Assert.Equal(before, Snapshot());
            using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Output, "observation.json"))); return json.RootElement.Clone();
        }
        private string[] Snapshot() => Directory.EnumerateFiles(Target.RootPath, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).Select(path => Path.GetRelativePath(Target.RootPath, path) + ":" + Hash(path)).ToArray();
        internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        public void Dispose() { /* Keep synthetic evidence after every independently reaped fixture. */ }
    }
}
