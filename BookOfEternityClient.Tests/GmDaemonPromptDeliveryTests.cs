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

    [Fact]
    public async Task ProvenBusy_RetriesIdenticalFrozenOperation() => await Run("typed-retry", result =>
    {
        var commands = result.GetProperty("commands").EnumerateArray().ToArray();
        Assert.Equal(2, commands.Length);
        Assert.Equal(commands[0].GetRawText(), commands[1].GetRawText());
        Assert.Equal("sent", result.GetProperty("status").GetString());
    });

    [Fact]
    public async Task PendingReplacedDuringRemoteCall_CannotReturnSent() => await Run("callback-replaced", result =>
    {
        Assert.Equal("bridge-unknown-outcome", result.GetProperty("status").GetString());
        Assert.True(result.GetProperty("pending").GetBoolean());
        Assert.True(result.GetProperty("paused").GetBoolean());
        Assert.Equal(0, result.GetProperty("readyFiles").GetInt32());
    });

    [Fact]
    public async Task CallerSnapshotReplacedBeforeAllocation_CannotSendOldMessage() => await Run("turn-packet-replaced", result =>
    {
        Assert.Empty(result.GetProperty("commands").EnumerateArray());
        Assert.Equal(0, result.GetProperty("readyFiles").GetInt32());
        Assert.True(result.GetProperty("pending").GetBoolean());
        Assert.Empty(result.GetProperty("errors").EnumerateArray());
    });

    [Fact]
    public async Task FirstAutostart_CapturesOnlyActualStartedBinding() => await Run("autostart-binding", result =>
    {
        var command = Assert.Single(result.GetProperty("commands").EnumerateArray());
        Assert.Equal("started-binding", command.GetProperty("inputBindingId").GetString());
    });

    [Theory]
    [InlineData("turn")]
    [InlineData("qte")]
    [InlineData("repair")]
    [InlineData("terminal")]
    public async Task RealConsumers_AmbiguityPreservesAuthorityAndCreatesNoTerminal(string kind) => await Run("consumer-" + kind, result =>
    {
        var commands = result.GetProperty("commands").EnumerateArray().ToArray();
        Assert.Single(commands);
        Assert.Equal(kind == "terminal" ? "terminal-repair" : kind == "qte" ? "qte-effect" : kind,
            commands[0].GetProperty("operationKind").GetString());
        Assert.True(result.GetProperty("pending").GetBoolean());
        Assert.True(result.GetProperty("paused").GetBoolean());
        Assert.Equal(0, result.GetProperty("readyFiles").GetInt32());
        Assert.Equal(0, result.GetProperty("errorCount").GetInt32());
        Assert.Empty(result.GetProperty("errors").EnumerateArray());
        Assert.False(result.GetProperty("processing").GetBoolean());
    });

    [Fact]
    public async Task QteIdle_QueriesOriginalWithoutRepaste() => await Run("qte-idle", result =>
    {
        var commands = result.GetProperty("commands").EnumerateArray().ToArray();
        Assert.Equal(new[] { "dispatchPrompt", "promptStatus" }, commands.Select(c => c.GetProperty("command").GetString()));
        Assert.Equal(commands[0].GetProperty("operationId").GetString(), commands[1].GetProperty("operationId").GetString());
        Assert.True(result.GetProperty("pending").GetBoolean());
        Assert.Equal(0, result.GetProperty("readyFiles").GetInt32());
    });

    [Fact]
    public async Task LauncherLostResponse_OnlyQueriesSameOperation() => await Run("launcher-lost-response", result =>
    {
        Assert.Equal(new[] { "dispatchPrompt", "promptStatus" }, result.GetProperty("commands").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal("submission-observed", result.GetProperty("disposition").GetString());
    });

    [Fact]
    public async Task RealDaemonLauncherRpc_RetainsOneOperationAndOneSubmit()
    {
        await using var host = new GmBridgePromptOperationTests.PromptHostFixture();
        host.Input.Written = bytes => host.Observe(bytes == "<submit>" ? "WORKING" : "CONTROLLED CLI\n› connected prompt");
        await host.Rpc(new { command = "status" }); // Real current pipe/binding status file consumed by launcher.
        await Run("connected-pipe", result =>
        {
            Assert.Equal("sent", result.GetProperty("first").GetString());
            Assert.Equal("sent", result.GetProperty("second").GetString());
            Assert.Equal(result.GetProperty("firstId").GetString(), result.GetProperty("secondId").GetString());
        }, host.Root);
        Assert.Equal("<paste>connected prompt</paste><submit>", System.Text.Encoding.UTF8.GetString(host.Input.Bytes));
    }

    internal static async Task Run(string scenario, Action<JsonElement> assert, string sessionRoot = "")
    {
        var root = GmBridgePromptOperationTests.PromptHostFixture.Repo;
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(root, "tests/fixtures/GmPromptDelivery/daemon-functions.ps1"), "-RepoRoot", root, "-Scenario", scenario }) start.ArgumentList.Add(arg);
        var owned = Path.Combine(Path.GetTempPath(), "boe-prompt-ps-owner-" + Guid.NewGuid().ToString("N"));
        start.ArgumentList.Add("-OwnedRoot"); start.ArgumentList.Add(owned);
        start.ArgumentList.Add("-SessionRoot"); start.ArgumentList.Add(sessionRoot);
        using var process = Process.Start(start) ?? throw new IOException("Inert PowerShell fixture did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
            if (Directory.Exists(owned)) Directory.Delete(owned, true);
            Assert.False(Directory.Exists(owned));
        }
        Assert.Equal(0, process.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(await error), await error);
        using var json = JsonDocument.Parse((await output).Trim());
        assert(json.RootElement);
    }
}
