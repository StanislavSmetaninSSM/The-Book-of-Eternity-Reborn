using System.Text;
using System.Text.Json;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task QtePhysicalInjuryConsumer_ActualNextOrdinaryRequestProjectsOneSafeOpportunity()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var sourceRun = await RunActualQteStorySourceAsync(physicalDeclaration: true, physicalMaximum: 2);
        Assert.Null(sourceRun.Failure);
        Assert.True(sourceRun.ActionObserved);
        await AssertActualQteStoryFinalizedAsync(_fs, sourceRun.Source!, sourceRun.StoryAtHandoff!, 155m);
        var occurrence = await AssertActualPhysicalOccurrenceAsync(sourceRun, 2);
        Assert.Equal(PhysicalInjurySourceTurn, GetPrivateField<GameLoop>(sourceRun.Engine!, "_gameLoop").TurnNumber);
        foreach (var path in new[] { "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
                     PendingTurnSnapshotAuthority.AuthorityPath, QteSceneService.QteOfferPath, "ready/turn_complete.json", "ready/turn_error.json" })
            Assert.False(_fs.FileExists(path), "Source QTE did not finish original cleanup: " + path);
        var pendingBefore = (await _fs.ReadFileBytesAsync(MortalWoundOccurrenceState.StatePath))!;
        var resourceBefore = (await _fs.ReadFileBytesAsync(ResourceMaterializationContract.StatePath))!;
        var historyBefore = (await _fs.ReadFileBytesAsync(ResourceMaterializationContract.HistoryPath))!;
        var storyBefore = (await _fs.ReadFileBytesAsync(QteStoryPath))!;
        var pause = new OperationCanceledException("Controlled stop after genuine N+1 request/snapshot capture, before the wait worker.");
        var reached = false;
        byte[]? requestBytes = null;
        byte[]? pendingAtCapture = null;
        byte[]? resourceAtCapture = null;
        byte[]? historyAtCapture = null;
        PendingTurnSnapshotReadResult? nextRead = null;
        using var live = new AgentConsoleLiveInputSource(new AgentConsoleStateStore(), TimeSpan.FromSeconds(10));
        var nextEngine = CreateGameEngine(live, settings =>
        {
            InertRealmSettings(settings);
            settings.AutosaveIntervalTurns = 0;
        }, finalizationHooks: new GameEngineSessionFinalizationHooks { AtCheckpointAsync = CaptureNextSource });
        GetPrivateField<GameLoop>(nextEngine, "_gameLoop").SetSession(sourceRun.Source!.SessionId, PhysicalInjurySourceTurn);
        await GetPrivateField<StateManager>(nextEngine, "_stateManager").RefreshGameStateAsync();
        var failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(nextEngine,
            "ProcessPlayerTurn", "Осмотреть ушиб после обвала.", null));
        Assert.True(reached, "N+1 ordinary staging did not reach the official capture hook; this is a prerequisite blocker.");
        Assert.Same(pause, failure);
        Assert.NotNull(requestBytes);
        Assert.NotNull(nextRead);
        Assert.True(nextRead!.Success, "N+1 official snapshot prerequisite blocked: " + JsonSerializer.Serialize(nextRead.Issues));
        var next = nextRead.Snapshot!;
        Assert.Equal(sourceRun.Source.SessionId, next.SessionId);
        Assert.Equal(PhysicalInjurySourceTurn + 1, next.TurnNumber);
        Assert.NotEqual(sourceRun.Source.RequestId, next.RequestId);
        Assert.NotEqual(sourceRun.SnapshotToken, next.SnapshotToken);
        Assert.Equal("mortal_world", next.Realm);
        Assert.Equal(pendingBefore, pendingAtCapture);
        Assert.Equal(pendingBefore, next.ReadRequiredBytes(MortalWoundOccurrenceState.StatePath));
        Assert.Equal(resourceBefore, resourceAtCapture);
        Assert.Equal(resourceBefore, next.ReadRequiredBytes(ResourceMaterializationContract.StatePath));
        Assert.Equal(historyBefore, historyAtCapture);
        Assert.Equal(historyBefore, next.ReadRequiredBytes(ResourceMaterializationContract.HistoryPath));
        Assert.Equal(storyBefore, await _fs.ReadFileBytesAsync(QteStoryPath));
        var signedPending = MortalWoundOccurrenceState.Parse(
            Encoding.UTF8.GetString(next.ReadRequiredBytes(MortalWoundOccurrenceState.StatePath)).TrimStart('\uFEFF'),
            MortalWoundOccurrenceState.StatePath);
        Assert.True(signedPending.IsValid, JsonSerializer.Serialize(signedPending.Issues));
        var retained = Assert.Single(signedPending.State!.Occurrences);
        Assert.Equal(occurrence.OccurrenceId, retained.OccurrenceId);
        Assert.Equal(occurrence.OccurrenceFingerprint, retained.OccurrenceFingerprint);
        Assert.Equal(occurrence.SourceRequestId, retained.SourceRequestId);
        Assert.Equal(occurrence.SourceSnapshotToken, retained.SourceSnapshotToken);
        Assert.Equal(PhysicalInjurySourceTurn, retained.SourceTurn);
        using var request = JsonDocument.Parse(requestBytes!);
        var raw = request.RootElement;
        Assert.Equal(next.SessionId, raw.GetProperty("sessionId").GetString());
        Assert.Equal(next.RequestId, raw.GetProperty("requestId").GetString());
        Assert.Equal(next.TurnNumber, raw.GetProperty("turnNumber").GetInt32());
        WritePhysicalInjuryDiagnostic("qte_physical_consumer_actual_next_source", new
        {
            Source = new { sourceRun.Source.SessionId, sourceRun.Source.RequestId, sourceRun.SnapshotToken, sourceRun.Source.TurnNumber },
            Next = new { next.SessionId, next.RequestId, next.SnapshotToken, next.TurnNumber, next.Realm },
            SourceHealth = 155, PendingOccurrence = Encoding.UTF8.GetString(pendingBefore),
            ActualRequest = Encoding.UTF8.GetString(requestBytes!), ControlledPause = ReferenceEquals(pause, failure)
        });

        // The first missing consumer contract is asserted only after real source and N+1 authority are proved.
        Assert.True(raw.TryGetProperty("woundOpportunities", out var offers) && offers.ValueKind == JsonValueKind.Array,
            "Actual N+1 ordinary request did not expose the finalized QTE physical wound opportunity.");
        var offer = Assert.Single(offers.EnumerateArray());
        Assert.Equal(new[] { "domain", "maximumSeverity", "opportunityRef", "safeContext" },
            offer.EnumerateObject().Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(occurrence.OpportunityRef, offer.GetProperty("opportunityRef").GetString());
        Assert.Equal("physical", offer.GetProperty("domain").GetString());
        Assert.Equal("II", offer.GetProperty("maximumSeverity").GetString());
        var context = offer.GetProperty("safeContext");
        Assert.Equal(new[] { "allowedLocationKinds", "cause", "target" },
            context.EnumerateObject().Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(occurrence.SafeContext.Target, context.GetProperty("target").GetString());
        Assert.Equal(occurrence.SafeContext.Cause, context.GetProperty("cause").GetString());
        Assert.Equal(occurrence.SafeContext.AllowedLocationKinds,
            context.GetProperty("allowedLocationKinds").EnumerateArray().Select(kind => kind.GetString()));

        async Task CaptureNextSource(SessionFinalizationCheckpoint checkpoint)
        {
            if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted) return;
            reached = true;
            await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            {
                requestBytes = await _fs.ReadFileBytesAsync(lease, "input/turn_request.json");
                nextRead = PendingTurnSnapshotReader.ReadCurrent(_fs, lease,
                    [MortalWoundOccurrenceState.StatePath, ResourceMaterializationContract.DefinitionsPath,
                     ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                     CanonicalResourceOwnerAuthorityComposer.AuthorityPath]);
                pendingAtCapture = await _fs.ReadFileBytesAsync(lease, MortalWoundOccurrenceState.StatePath);
                resourceAtCapture = await _fs.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath);
                historyAtCapture = await _fs.ReadFileBytesAsync(lease, ResourceMaterializationContract.HistoryPath);
            }
            WritePhysicalInjuryDiagnostic("qte_physical_consumer_actual_capture_hook", new
            { ReaderSuccess = nextRead.Success, nextRead.Issues, ActualRequest = requestBytes is null ? null : Encoding.UTF8.GetString(requestBytes) });
            throw pause;
        }
    }
}
