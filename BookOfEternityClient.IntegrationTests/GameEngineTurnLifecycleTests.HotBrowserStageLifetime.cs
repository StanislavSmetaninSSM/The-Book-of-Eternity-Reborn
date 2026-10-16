using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HotSubmission_BrowserAcknowledgesOnlyActualStagingOwnerReturn(bool failClosing)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        GameEngine? engine = null;
        FileSystemManager? files = null;
        var cuts = 0; var terminalWaits = 0;
        JsonObject? acknowledged = null;
        var captured = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var closeFailure = new IOException("Actual browser Stage returned; its owning finalization failed.");
        files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                SessionOperationClosingAsync = () =>
                {
                    if (!failClosing || engine is null || cuts != 0) return Task.CompletedTask;
                    var diagnostic = InvokePrivateValue<JsonObject>(engine, "ExportHotOrdinarySubmissionDiagnostic");
                    if (diagnostic["binding"]?["stageDisposition"]?.GetValue<string>() != "AwaitingStagingOwnerReturn")
                        return Task.CompletedTask;
                    Assert.True(diagnostic["binding"]!["actualStageCompleted"]!.GetValue<bool>());
                    foreach (var path in new[] { PendingPlayerActionService.PendingPath, "input/turn_request.json",
                        LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath })
                        captured.Add(path, File.ReadAllBytes(files!.ResolvePath(path)));
                    cuts++;
                    throw closeFailure;
                }
            });
        CopyDirectory(TestRepoPaths.BaseSessionRoot, files.GameSessionPath);
        var binding = await QueueBrowserInputAsync(files);
        engine = CreateGameEngine(new QueuedConsoleInputSource([Key(ConsoleKey.Enter)]),
            settings => { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; },
            finalizationHooks: new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = async checkpoint =>
                {
                    if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted) return;
                    terminalWaits++;
                    acknowledged = InvokePrivateValue<JsonObject>(engine!, "ExportHotOrdinarySubmissionDiagnostic");
                    var data = acknowledged["binding"]!;
                    Assert.Equal("Staged", data["stageDisposition"]!.GetValue<string>());
                    Assert.True(data["actualStageCompleted"]!.GetValue<bool>());
                    var request = JsonSerializer.Deserialize<BookOfEternityClient.Models.TurnRequest>(
                        File.ReadAllText(files.ResolvePath("input/turn_request.json")), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                    Assert.Equal(binding.Generation, data["generation"]!.GetValue<string>());
                    Assert.Equal(request.SessionId, data["sessionId"]!.GetValue<string>());
                    Assert.Equal(request.RequestId, data["requestId"]!.GetValue<string>());
                    Assert.Equal(request.TurnNumber, data["turn"]!.GetValue<int>());
                    await files.WriteFileAtomicAsync("ready/turn_error.json", JsonSerializer.Serialize(new
                    {
                        sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
                        timestamp = DateTime.UtcNow.ToString("O"), status = "error", error = "Controlled correlated browser terminal error."
                    }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                }
            }, fileSystem: files);
        var failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(engine, "ProcessPlayerTurn",
            binding.Action, null, null, null, true, binding));
        var final = InvokePrivateValue<JsonObject>(engine, "ExportHotOrdinarySubmissionDiagnostic");
        if (failClosing)
        {
            Assert.NotNull(failure); Assert.Equal(1, cuts); Assert.Equal(0, terminalWaits); Assert.Null(acknowledged);
            Assert.Contains(closeFailure.Message, failure!.ToString());
            Assert.Equal("Ended", final["binding"]!["stageDisposition"]!.GetValue<string>());
            Assert.Equal("Unavailable", final["custody"]!["status"]!.GetValue<string>());
            Assert.Equal(4, captured.Count);
            foreach (var (path, bytes) in captured) Assert.Equal(bytes, File.ReadAllBytes(files.ResolvePath(path)));
            Assert.Equal("staged", JsonNode.Parse(File.ReadAllText(files.ResolvePath(PendingPlayerActionService.PendingPath)))!["status"]!.GetValue<string>());
        }
        else
        {
            Assert.Null(failure); Assert.Equal(0, cuts); Assert.Equal(1, terminalWaits); Assert.NotNull(acknowledged);
            Assert.False(File.Exists(files.ResolvePath(PendingPlayerActionService.PendingPath)));
        }
        foreach (var key in new[] { "hasS", "hasB", "hasM3" }) Assert.False(final[key]!.GetValue<bool>());
        WritePhysicalInjuryDiagnostic("fr063_hot_browser_actual_stage_lifetime", new
            { failClosing, cuts, terminalWaits, acknowledged, final, failure = failure?.ToString(), capturedPaths = captured.Keys });
    }
}
