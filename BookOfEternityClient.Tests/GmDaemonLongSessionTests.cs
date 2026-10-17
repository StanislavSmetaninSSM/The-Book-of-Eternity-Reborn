using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class GmDaemonLongSessionTests(ITestOutputHelper output)
{
    [Fact]
    public async Task CurrentDaemonAllocator_AdvancingSourcesContinueBeyond256SettledDispatches()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-daemon-relay1559-" + Guid.NewGuid().ToString("N"));
        var diagnostics = Path.Combine(TestRepoPaths.RepoRoot, "TestResults/relay1559", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); Directory.CreateDirectory(diagnostics);
        using var cleanup = new GmRelayLongSessionTests.OwnedRoot(root, output.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            files.BootstrapLocalStorage(lease, null, JsonSerializer.SerializeToUtf8Bytes(new GameSettings()));
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", Path.Combine(TestRepoPaths.RepoRoot, "tests/fixtures/GmPromptDelivery/long-session-retention.ps1"), "-RepoRoot", TestRepoPaths.RepoRoot, "-OwnedRoot", root }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(process, TimeSpan.FromSeconds(25), Path.Combine(diagnostics, "daemon.log"));
        var log = File.ReadAllText(Path.Combine(diagnostics, "daemon.log"));
        Assert.True(process.ExitCode == 0, log);
        // ObserveBuild preserves stdout/stderr. The final JSON line is the actual
        // function result; a log/PowerShell prerequisite error never counts as RED.
        var line = log.Split('\n').Last(value => value.TrimStart().StartsWith("{\"completed\"", StringComparison.Ordinal));
        using var result = JsonDocument.Parse(line);
        output.WriteLine(result.RootElement.GetRawText());
        Assert.Equal(272, result.RootElement.GetProperty("completed").GetInt32());
        Assert.True(result.RootElement.GetProperty("retainedOperations").GetInt32() <= 256);
    }
    [Fact]
    public async Task DaemonRestart_SameCurrentSourceAndBindingRetainsOriginalOperationWithoutRedispatch()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-daemon-restart1559-" + Guid.NewGuid().ToString("N"));
        var diagnostics = Path.Combine(TestRepoPaths.RepoRoot, "TestResults/relay1559", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); Directory.CreateDirectory(diagnostics);
        using var cleanup = new GmRelayLongSessionTests.OwnedRoot(root, output.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            files.BootstrapLocalStorage(lease, null, JsonSerializer.SerializeToUtf8Bytes(new GameSettings()));
        async Task<JsonElement> Execute(string phase)
        {
            var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", Path.Combine(TestRepoPaths.RepoRoot, "tests/fixtures/GmPromptDelivery/long-session-retention.ps1"), "-RepoRoot", TestRepoPaths.RepoRoot, "-OwnedRoot", root, "-Scenario", phase }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var log = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(process, TimeSpan.FromSeconds(15), Path.Combine(diagnostics, phase + ".log"));
            Assert.True(process.ExitCode == 0, log);
            using var result = JsonDocument.Parse(log.Split('\n').Last(value => value.StartsWith("{\"payload\"", StringComparison.Ordinal)));
            return result.RootElement.Clone();
        }
        var original = await Execute("restart-first");
        Assert.Equal("submission-observed", original.GetProperty("delivery").GetProperty("disposition").GetString());
        var originalPending = File.ReadAllBytes(files.ResolvePath("input/turn_request.json"));
        var replacement = await Execute("restart-second");
        Assert.Equal(originalPending, File.ReadAllBytes(files.ResolvePath("input/turn_request.json")));
        var calls = File.ReadAllLines(Path.Combine(root, "launcher-calls.jsonl")).Select(line => JsonDocument.Parse(line)).ToArray();
        try
        {
            output.WriteLine(JsonSerializer.Serialize(new { Original = original, Replacement = replacement, Calls = calls.Select(x => x.RootElement.Clone()).ToArray(), Diagnostics = diagnostics }));
            Assert.Equal(original.GetProperty("payload").GetProperty("operationId").GetString(), replacement.GetProperty("payload").GetProperty("operationId").GetString());
            Assert.Single(calls.Where(call => call.RootElement.GetProperty("command").GetString() == "dispatchPrompt"));
        }
        finally { foreach (var call in calls) call.Dispose(); }
    }

}
