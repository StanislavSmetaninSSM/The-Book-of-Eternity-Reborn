using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmDaemonPromptDeliveryTests
{
    [Fact]
    public async Task ActualRetry_TransportAmbiguityDoesNotRepeat() =>
        await Run("transport-ambiguity", result =>
        {
            Assert.Equal(1, result.GetProperty("calls").GetInt32());
            Assert.Equal("bridge-unknown-outcome", result.GetProperty("status").GetString());
        });

    [Fact]
    public async Task DormantAllowNotReady_UsesOneImmutableOperation() =>
        await Run("allow-not-ready", result =>
        {
            var commands = result.GetProperty("commands").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.Equal(new[] { "dispatch-operation" }, commands);
        });

    internal static async Task Run(string scenario, Action<JsonElement> assert)
    {
        var root = GmBridgePromptOperationTests.PromptHostFixture.Repo;
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(root, "tests/fixtures/GmPromptDelivery/daemon-functions.ps1"), "-RepoRoot", root, "-Scenario", scenario }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Inert PowerShell fixture did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        }
        Assert.Equal(0, process.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(await error), await error);
        using var json = JsonDocument.Parse((await output).Trim());
        assert(json.RootElement);
    }
}
