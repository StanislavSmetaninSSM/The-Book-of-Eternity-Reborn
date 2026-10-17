using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Current maintained relay/helper causal boundaries; synthetic content is not accepted gameplay.</summary>
public sealed class GmRelayLongSessionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("fourth-submit")]
    [InlineData("prompt-over-old-lines")]
    [InlineData("prompt-over-old-characters")]
    [InlineData("response-over-old-mib")]
    [InlineData("many-writes")]
    [InlineData("partial-apply")]
    [InlineData("small-response")]
    public async Task CurrentRelay_CompleteExactExchangeWithoutArtificialSessionCaps(string scenario)
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-relay1559-" + Guid.NewGuid().ToString("N"));
        var diagnostics = Path.Combine(TestRepoPaths.RepoRoot, "TestResults/relay1559", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); Directory.CreateDirectory(diagnostics);
        using var cleanup = new CleanupOwnedFixture(root, output.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            files.BootstrapLocalStorage(lease, null, JsonSerializer.SerializeToUtf8Bytes(new GameSettings()));
        var initialGeneration = File.ReadAllBytes(files.SessionGenerationPath);
        async Task<int> Run(string executable, string[] arguments, string name, int seconds)
        {
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            start.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(process, TimeSpan.FromSeconds(seconds), Path.Combine(diagnostics, name));
            return process.ExitCode;
        }
        Assert.Equal(0, await Run("pwsh", ["-NoLogo", "-NoProfile", "-File", Path.Combine(TestRepoPaths.RepoRoot, "scripts/build-linux-supervisor.ps1"), "-OutputDirectory", diagnostics, "-IncludeHostGuardian"], "guardian-build.log", 40));
        var guardian = Path.Combine(diagnostics, "host-guardian");
        var native = await Run(guardian, ["--relay-turn", Path.Combine(diagnostics, "guardian.json"), "90000", "/usr/bin/python3", "-B", Path.Combine(TestRepoPaths.RepoRoot, "tests/fixtures/ProductionMain/relay-long-session-checks.py"), scenario, root, diagnostics], "scenario.log", 100);
        using var owned = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(diagnostics, "guardian.json")));
        Assert.True(owned.RootElement.GetProperty("echild").GetBoolean());
        Assert.Equal(0, owned.RootElement.GetProperty("emergencySignals").GetInt32());
        Assert.Equal(0, owned.RootElement.GetProperty("failures").GetInt32());
        Assert.False(owned.RootElement.GetProperty("deadline").GetBoolean());
        Assert.Equal(initialGeneration, File.ReadAllBytes(files.SessionGenerationPath));
        output.WriteLine(JsonSerializer.Serialize(new { Scenario = scenario, Diagnostics = diagnostics, OriginalGenerationUnchanged = true, Scope = "CONTROLLED_PROTOCOL_NOT_CLIENT_ACCEPTANCE" }));
        Assert.True(native == 0, File.ReadAllText(Path.Combine(diagnostics, "scenario.log")));
    }
}
