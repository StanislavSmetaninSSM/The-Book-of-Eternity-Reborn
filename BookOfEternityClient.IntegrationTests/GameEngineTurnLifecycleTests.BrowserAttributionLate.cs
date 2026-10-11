using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task BrowserAdmissionDiagnostics_LateTerminalBranchCapturesOnceAndPreservesOriginalFailure()
    {
        var (_, staged) = await PrepareBrowserInputStagingAsync(withRollback: true);
        var original = new IOException("original late-terminal checkpoint failure");
        var reached = 0;
        var engine = CreateGameEngine(finalizationHooks: new GameEngineSessionFinalizationHooks
        {
            AtCheckpointAsync = checkpoint => checkpoint == SessionFinalizationCheckpoint.LateTerminalAndIdleOperationBound
                ? FailOriginal() : Task.CompletedTask
        });
        Task FailOriginal() { reached++; return Task.FromException(original); }
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession("browser-recovery-session", 0);
        var type = typeof(FileSystemManager).Assembly.GetType("BookOfEternityClient.Core.BrowserAdmissionDiagnosticCollector", true)!;
        var collector = type.GetMethod("CreateForTests", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { (Func<long>)System.Diagnostics.Stopwatch.GetTimestamp, 4096 })!;
        typeof(FileSystemManager).GetField("_browserAdmissionDiagnostic", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(_fs, collector);
        JsonNode Snapshot() => JsonNode.Parse((string)type.GetMethod("SnapshotJson", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(collector, null)!)!;

        // The actual staged idle return must not create a terminal window.
        Assert.True(await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync"));
        Assert.Equal(0, reached);
        Assert.Empty(Snapshot()["Windows"]!.AsArray());
        var request = JsonSerializer.Deserialize<TurnRequest>(staged.RequestJson, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", JsonSerializer.Serialize(new
        {
            sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
            timestamp = DateTime.UtcNow.ToString("O"), status = "success", filesModified = Array.Empty<string>()
        }));
        var failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(engine,
            "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync"));
        Assert.Same(original, failure);
        Assert.Equal(1, reached);
        var result = Snapshot();
        _directGachaOutput?.WriteLine("actual-late-branch-profile=" + result.ToJsonString());
        var window = Assert.Single(result["Windows"]!.AsArray())!;
        Assert.Equal(staged.Binding.ActionId, window["ActionId"]!.GetValue<string>());
        Assert.Equal(staged.Binding.Generation, window["Generation"]!.GetValue<string>());
        Assert.Equal("unbound", window["RequestId"]!.GetValue<string>()); // Original snapshot load is not reached.
        Assert.NotNull(window["OwnerIdentity"]);
        Assert.Equal("failed", window["Outcome"]!.GetValue<string>());
        Assert.Null(window["AcceptedTicks"]);
        Assert.NotNull(window["ClosedTicks"]);
        var markers = window["Markers"]!.AsObject();
        Assert.True(markers.ContainsKey("late-terminal-observed"));
        Assert.False(markers.ContainsKey("terminal-wait-returned"));
        Assert.True(markers.ContainsKey("processing-owner"));
        Assert.True(markers.ContainsKey("finalizer-failed"));
        Assert.True(result["Complete"]!.GetValue<bool>()); // Complete failure capture, never accepted success.
    }
}
