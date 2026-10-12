using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task QtePhysicalInjuryProducer_ActualSelectedDeclarationBoundsOccurrence(int maximum)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var run = await RunActualQteStorySourceAsync(physicalDeclaration: true, physicalMaximum: maximum,
            observeQteAction: () => Assert.False(_fs.FileExists(MortalWoundOccurrenceState.StatePath)));
        Assert.Null(run.Failure);
        Assert.True(run.ActionObserved);
        await AssertActualQteStoryFinalizedAsync(_fs, run.Source!, run.StoryAtHandoff!, 155m);
        var occurrence = await AssertActualPhysicalOccurrenceAsync(run, maximum);
        Assert.Equal(4, occurrence.HardMaximumSeverityRank);
        Assert.Null(occurrence.MinimumSeverityRank);
        Assert.Null(occurrence.GuaranteedTrigger);
        Assert.Null(occurrence.WorseningTarget);
        Assert.False(_fs.FileExists("input/turn_request.json"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QtePhysicalInjuryProducer_ActualResourceOnlyOrUnselectedDeclarationCreatesNoOccurrence(bool unselected)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var run = await RunActualQteStorySourceAsync(physicalDeclaration: unselected, physicalOnlyUnselected: unselected,
            observeQteAction: () => Assert.False(_fs.FileExists(MortalWoundOccurrenceState.StatePath)));
        Assert.Null(run.Failure);
        Assert.True(run.ActionObserved);
        await AssertActualQteStoryFinalizedAsync(_fs, run.Source!, run.StoryAtHandoff!, 155m);
        Assert.False(_fs.FileExists(MortalWoundOccurrenceState.StatePath));
        WritePhysicalInjuryDiagnostic("qte_physical_no_selected_declaration", new
        { unselected, run.Source!.RequestId, run.SnapshotToken, Health = 155 });
    }

    [Fact]
    public async Task QtePhysicalInjuryProducer_ActualAwaitingReceiptPublishesOneCompleteFinalBatch()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var run = await RunActualQteStorySourceAsync(bounded: true, physicalDeclaration: true,
            observeQteAction: () => Assert.False(_fs.FileExists(MortalWoundOccurrenceState.StatePath)));
        Assert.Null(run.Failure);
        Assert.True(run.ActionObserved);
        var qte = GetPrivateField<QteSceneService>(run.Engine!, "_qteSceneService");
        Assert.Equal("awaiting_receipt", (await qte.ReadRuntimeStateAsync()).ActiveScene!.EffectResolutionState);
        Assert.False(_fs.FileExists(MortalWoundOccurrenceState.StatePath));
        var originals = await CaptureQteStoryOriginalArtifactsAsync();
        await AssertPhysicalInjuryProtectedBytesAsync(run.ProtectedBefore);
        Assert.True(await InvokePrivateAsync<bool>(run.Engine!, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync"));
        Assert.False(_fs.FileExists(MortalWoundOccurrenceState.StatePath));
        Assert.Equal(run.StoryAtHandoff, await _fs.ReadFileBytesAsync(QteStoryPath));
        foreach (var image in originals) Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(image.Key));
        await WriteActualPhysicalBoundedReceiptAsync();
        var resumed = CreateGameEngine(new QueuedConsoleInputSource(), settings =>
        { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; });
        GetPrivateField<GameLoop>(resumed, "_gameLoop").SetSession(run.Source!.SessionId, PhysicalInjurySourceTurn);
        await GetPrivateField<StateManager>(resumed, "_stateManager").RefreshGameStateAsync();
        Assert.True(await InvokePrivateAsync<bool>(resumed, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync"));
        await AssertActualQteStoryFinalizedAsync(_fs, run.Source, run.StoryAtHandoff!, 153m);
        var occurrence = await AssertActualPhysicalOccurrenceAsync(run, 2);
        Assert.Equal(1, occurrence.ProducerCandidateCount);
        Assert.True(occurrence.AcceptedEvents.Count > 2, "Reaction and effect siblings were dropped.");
        Assert.Contains(occurrence.AcceptedEvents, row => row.Kind == "effect_activation");
        Assert.Contains(occurrence.AcceptedEvents, row => row.Kind.StartsWith("effect_terminal_", StringComparison.Ordinal));
        Assert.True(occurrence.AcceptedEvents.Count(row => row.Kind == "resource_mutation") >= 2);
        AssertQteStoryOriginalArtifactsRemoved(originals);
    }

    [Fact]
    public async Task QtePhysicalInjuryProducer_PublicationFailureRestoresSourceThenRetryPublishesOnce()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        IDisposable? retainedInput = null;
        try
        {
            Dictionary<string, byte[]?>? selectedBefore = null;
            var injected = false;
            var run = await RunActualQteStorySourceAsync(physicalDeclaration: true, observeQteAction: () =>
            {
                selectedBefore = QteDeferredEffectContinuation.SealedRootPaths.Concat(QtePhysicalInjurySource.WoundRootPaths)
                    .Distinct(StringComparer.Ordinal).ToDictionary(path => path,
                        path => _fs.FileExists(path) ? File.ReadAllBytes(_fs.ResolvePath(path)) : null, StringComparer.Ordinal);
                _consolePublicationObserver = (phase, index) =>
                {
                    if (injected || phase != TrustedLocalPublicationPhase.MemberPublished) return;
                    var journalPath = Path.Combine(_fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
                    using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(journalPath));
                    if (journal.RootElement.GetProperty("Members")[index].GetProperty("Path").GetString()
                        != _fs.ResolvePath(MortalWoundOccurrenceState.StatePath)) return;
                    Assert.False(journal.RootElement.GetProperty("Committed").GetBoolean());
                    injected = true;
                    throw new InvalidOperationException("Controlled physical occurrence publication failure.");
                };
            }, retainLiveInput: input => retainedInput = input);
            _consolePublicationObserver = null;
            Assert.True(injected);
            Assert.NotNull(run.Failure);
            Assert.NotNull(selectedBefore);
            foreach (var image in selectedBefore!) Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(image.Key));
            Assert.False(_fs.FileExists(MortalWoundOccurrenceState.StatePath));
            Assert.Equal(run.StoryAtHandoff, await _fs.ReadFileBytesAsync(QteStoryPath));
            Assert.True(await InvokePrivateAsync<bool>(run.Engine!, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync"));
            await AssertActualQteStoryFinalizedAsync(_fs, run.Source!, run.StoryAtHandoff!, 155m);
            var first = await AssertActualPhysicalOccurrenceAsync(run, 2);
            var occurrenceBytes = await _fs.ReadFileBytesAsync(MortalWoundOccurrenceState.StatePath);
            var historyBytes = await _fs.ReadFileBytesAsync(ResourceMaterializationContract.HistoryPath);
            _ = await InvokePrivateAsync<bool>(run.Engine!, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
            Assert.Equal(occurrenceBytes, await _fs.ReadFileBytesAsync(MortalWoundOccurrenceState.StatePath));
            Assert.Equal(historyBytes, await _fs.ReadFileBytesAsync(ResourceMaterializationContract.HistoryPath));
            WritePhysicalInjuryDiagnostic("qte_physical_actual_failure_retry", new
            { run.Source!.RequestId, run.SnapshotToken, first.OccurrenceId, Roots = selectedBefore.Keys });
        }
        finally
        {
            _consolePublicationObserver = null;
            retainedInput?.Dispose();
        }
    }

    private async Task<MortalWoundOccurrence> AssertActualPhysicalOccurrenceAsync(QteStoryRun run, int maximum)
    {
        var raw = await _fs.ReadFileAsync(MortalWoundOccurrenceState.StatePath);
        var parsed = MortalWoundOccurrenceState.Parse(raw, MortalWoundOccurrenceState.StatePath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
        var occurrence = Assert.Single(parsed.State!.Occurrences);
        Assert.Equal(run.Source!.SessionId, occurrence.SourceSessionId);
        Assert.Equal(run.Source.RequestId, occurrence.SourceRequestId);
        Assert.Equal(run.SnapshotToken, occurrence.SourceSnapshotToken);
        Assert.Equal(run.Source.TurnNumber, occurrence.SourceTurn);
        Assert.Equal("qte", occurrence.AdapterKind);
        Assert.Equal("physical", occurrence.Domain);
        Assert.Equal("mortal_qte_terminal_health_damage_v1", occurrence.ProfileKey);
        Assert.Equal(maximum, occurrence.Outcome.MaximumSeverityRank);
        Assert.Equal(FormattableString.Invariant($"turn_{run.Source.TurnNumber}:qte_terminal:1:resource:1"),
            occurrence.AcceptedEvents[occurrence.AcceptedEventOrdinal].EventRef);
        Assert.Equal(0, occurrence.ProducerCandidateOrdinal);
        Assert.Equal(1, occurrence.ProducerCandidateCount);
        WritePhysicalInjuryDiagnostic("qte_physical_actual_occurrence", new
        { run.Source.RequestId, run.SnapshotToken, maximum, Occurrence = raw });
        return occurrence;
    }

    private async Task WriteActualPhysicalBoundedReceiptAsync()
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await _fs.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), false).Catalog!;
        var pending = ResourcePendingResolutionState.ParseCanonical(await _fs.ReadFileAsync(ResourcePendingResolutionState.PendingPath), definitions, false);
        Assert.True(pending.IsValid, JsonSerializer.Serialize(pending.Issues));
        var requestId = Assert.Single(pending.State!.Requests).RequestId;
        var request = JsonNode.Parse((await _fs.ReadFileAsync(QteDeferredEffectContinuation.RequestPath))!)!.AsObject();
        var receipts = new JsonObject { ["schemaVersion"] = 1, ["effectResolutionReceipts"] = new JsonArray(new JsonObject
        { ["requestId"] = requestId, ["resultKind"] = "resource_delta", ["amount"] = 2, ["reason"] = "Крошка усиливает удар." }) };
        foreach (var field in new[] { "requestKind", "sessionId", "sessionGeneration", "continuationId", "requestId", "waveId", "waveOrdinal", "acceptedSourceTurn", "qteId", "selectedTerminalFingerprint", "pendingStateFingerprint", "fullTurnFingerprint", "semanticTurnFingerprint" })
            receipts[field] = request[field]!.DeepClone();
        await _fs.WriteFileAtomicAsync(QteDeferredEffectContinuation.ReceiptPath, receipts.ToJsonString());
        var bytes = (await _fs.ReadFileBytesAsync(QteDeferredEffectContinuation.ReceiptPath))!;
        var ready = new JsonObject { ["schemaVersion"] = 1, ["receiptsFingerprint"] = "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(),
            ["timestamp"] = DateTime.UtcNow.ToString("o"), ["status"] = "success" };
        foreach (var field in new[] { "requestKind", "sessionId", "sessionGeneration", "continuationId", "requestId", "waveId", "waveOrdinal", "acceptedSourceTurn", "qteId", "pendingStateFingerprint" })
            ready[field] = request[field]!.DeepClone();
        await _fs.WriteFileAtomicAsync(QteDeferredEffectContinuation.ReadyPath, ready.ToJsonString());
    }
}
