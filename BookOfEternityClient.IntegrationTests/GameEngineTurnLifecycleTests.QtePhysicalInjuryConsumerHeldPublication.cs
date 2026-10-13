using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    // Component controls: genuine QTE source, actual staging/reader and owner-taken common publication.
    // The two retained fresh-child changed cases separately prove the actual late-idle lifecycle.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QtePhysicalInjuryConsumer_ComponentPastPublicationDoesNotAuthorizeChangedResubmission(bool materialize)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var component = await PreparePublishedMortalCompletionProofAsync(materialize);
        var exact = await InspectMortalCompletionProofAsync(component.Snapshot);
        Assert.Equal(MortalQteDecisionPublication.CurrentPublicationState.VerifiedCompleted, exact.State);
        Assert.True(exact.MayResubmit);
        var genuineReplay = await ReadGenuineMortalCompletionReplayAsync(component.Decision, materialize);
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            await MortalQteDecisionPublication.ValidateExactReplayAsync(_fs, lease, genuineReplay);
        var draft = component.Decision.DeepClone().AsObject();
        if (materialize) draft["proposal"]!["display"]!["description"] = "Иное законное описание уже принятого пореза.";
        else { draft["decision"] = "materialize"; draft["woundRef"] = "changed_completion_cut"; draft["proposal"] = CreateMortalComponentProposal(); }
        await _fs.WriteFileAtomicAsync(MortalWoundOpportunityAdapter.DecisionPath, draft.ToJsonString());
        var before = await CaptureMortalComponentImagesAsync();
        var changed = await InspectMortalCompletionProofAsync(component.Snapshot);
        Assert.Equal(MortalQteDecisionPublication.CurrentPublicationState.VerifiedCompleted, changed.State);
        Assert.False(changed.MayResubmit);
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            Assert.IsType<InvalidDataException>(await Record.ExceptionAsync(() => MortalQteDecisionPublication.ValidateExactReplayAsync(_fs, lease, genuineReplay)));
        await AssertMortalCompletionEntryHeldAsync(component.Engine);
        foreach (var image in before) Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(image.Key));
        Assert.Equal(draft.ToJsonString(), await _fs.ReadFileAsync(MortalWoundOpportunityAdapter.DecisionPath));
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync()) Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(_fs, lease));
        await _fs.WriteFileAtomicAsync(MortalWoundOpportunityAdapter.DecisionPath, "null");
        var malformed = await InspectMortalCompletionProofAsync(component.Snapshot);
        Assert.Equal(MortalQteDecisionPublication.CurrentPublicationState.VerifiedCompleted, malformed.State);
        Assert.False(malformed.MayResubmit);
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            Assert.IsType<InvalidDataException>(await Record.ExceptionAsync(() => MortalQteDecisionPublication.ValidateExactReplayAsync(_fs, lease, genuineReplay)));
        await AssertMortalCompletionEntryHeldAsync(component.Engine);
        foreach (var image in before) Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(image.Key));
        Assert.Equal("null", await _fs.ReadFileAsync(MortalWoundOpportunityAdapter.DecisionPath));
        WritePhysicalInjuryDiagnostic("qte_completion_past_vs_current", new { materialize, exact, changed, component.Snapshot.RequestId, component.Snapshot.SnapshotToken });
    }

    [Theory]
    [InlineData("missing_marker")]
    [InlineData("null_marker")]
    [InlineData("marker_hash")]
    [InlineData("generation")]
    [InlineData("binding")]
    [InlineData("manifest")]
    [InlineData("authority")]
    [InlineData("original_quartet")]
    [InlineData("receipt")]
    [InlineData("history")]
    [InlineData("published_quartet")]
    [InlineData("published_wound")]
    [InlineData("published_effect")]
    public async Task QtePhysicalInjuryConsumer_ComponentCompletionIntegrityConflictNeverGrantsReplay(string mutation)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var component = await PreparePublishedMortalCompletionProofAsync(true);
        Assert.Equal(MortalQteDecisionPublication.CurrentPublicationState.VerifiedCompleted,
            (await InspectMortalCompletionProofAsync(component.Snapshot)).State);
        var genuineReplay = await ReadGenuineMortalCompletionReplayAsync(component.Decision, true);
        switch (mutation)
        {
            case "missing_marker": _fs.DeleteFile(MortalQteDecisionPublication.StatePath); break;
            case "null_marker": await _fs.WriteFileAtomicAsync(MortalQteDecisionPublication.StatePath, "null"); break;
            case "marker_hash":
                var marker = JsonNode.Parse(MortalArchiveText((await _fs.ReadFileBytesAsync(MortalQteDecisionPublication.StatePath))!))!.AsObject();
                marker["fingerprint"] = "invalid";
                await _fs.WriteFileAtomicAsync(MortalQteDecisionPublication.StatePath, marker.ToJsonString()); break;
            case "generation":
                await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync()) _fs.RotateSessionGeneration(lease); break;
            case "binding":
                var requestRoot = JsonNode.Parse((await _fs.ReadFileAsync("input/turn_request.json"))!)!.AsObject();
                requestRoot["requestId"] = "foreign_request";
                await _fs.WriteFileAtomicAsync("input/turn_request.json", requestRoot.ToJsonString()); break;
            case "manifest": await AppendMortalCompletionDriftAsync("game_state/control/pending_turn_snapshot.json"); break;
            case "authority": await AppendMortalCompletionDriftAsync(PendingTurnSnapshotAuthority.AuthorityPath); break;
            case "original_quartet":
                var manifest = JsonNode.Parse((await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json"))!)!.AsObject();
                var fileMap = manifest.Single(pair => pair.Key.Equals("files", StringComparison.OrdinalIgnoreCase)).Value!.AsObject();
                await AppendMortalCompletionDriftAsync(fileMap[ResourceMaterializationContract.DefinitionsPath]!.GetValue<string>()); break;
            case "receipt":
                var receiptRoot = JsonNode.Parse((await _fs.ReadFileAsync(MortalWoundOpportunityReceiptState.StatePath))!)!.AsObject();
                receiptRoot["receipts"]![0]!["sourceRequestId"] = "foreign_source_request";
                await _fs.WriteFileAtomicAsync(MortalWoundOpportunityReceiptState.StatePath, receiptRoot.ToJsonString()); break;
            case "history":
                var historyRoot = JsonNode.Parse((await _fs.ReadFileAsync(WoundHistoryState.HistoryPath))!)!.AsObject();
                historyRoot["transitions"]![0]!["turn"] = 44;
                await _fs.WriteFileAtomicAsync(WoundHistoryState.HistoryPath, historyRoot.ToJsonString()); break;
            case "published_quartet": await AppendMortalCompletionDriftAsync(ResourceMaterializationContract.StatePath); break;
            case "published_wound": await AppendMortalCompletionDriftAsync(WoundCarrierCatalog.PlayerPath); break;
            case "published_effect": await AppendMortalCompletionDriftAsync(EffectCarrierCatalog.PlayerPath); break;
            default: throw new InvalidOperationException("Unknown completion-proof mutation.");
        }
        var before = await CaptureMortalComponentImagesAsync();
        MortalQteDecisionPublication.CurrentPublicationInspection inspected;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            inspected = await MortalQteDecisionPublication.InspectCurrentAsync(_fs, lease, component.Snapshot.SessionId,
                component.Snapshot.RequestId, component.Snapshot.SnapshotToken, component.Snapshot.TurnNumber);
        Assert.Equal(MortalQteDecisionPublication.CurrentPublicationState.IntegrityConflict, inspected.State);
        Assert.False(inspected.MayResubmit);
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            Assert.IsType<InvalidDataException>(await Record.ExceptionAsync(() => MortalQteDecisionPublication.ValidateExactReplayAsync(_fs, lease, genuineReplay)));
        foreach (var image in before) Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(image.Key));
        Assert.False(_fs.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
        WritePhysicalInjuryDiagnostic("qte_completion_integrity_refusal", new { mutation, inspected, component.Snapshot.RequestId, component.Snapshot.SnapshotToken });
    }

    [Theory]
    [InlineData("resource")]
    [InlineData("effect")]
    [InlineData("wound")]
    public async Task QtePhysicalInjuryConsumer_ComponentCommittedPublicationHoldsForeignCommandsWithoutConsuming(string kind)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var component = await PreparePublishedMortalCompletionProofAsync(false);
        var genuineReplay = await ReadGenuineMortalCompletionReplayAsync(component.Decision, false);
        var path = kind switch
        { "resource" => ResourceMaterializationContract.CommandPath, "effect" => EffectAcceptedTurnPlan.CommandPath,
            _ => AcceptedMechanicsPlan.WoundCommandPath };
        if (kind == "resource") await WriteMortalReplayResourceWorkAsync();
        else await _fs.WriteFileAtomicAsync(path, "{\"foreignCommand\":true}");
        var foreign = await _fs.ReadFileBytesAsync(path);
        var before = await CaptureMortalComponentImagesAsync();
        var inspected = await InspectMortalCompletionProofAsync(component.Snapshot);
        Assert.Equal(MortalQteDecisionPublication.CurrentPublicationState.VerifiedCompleted, inspected.State);
        Assert.False(inspected.MayResubmit);
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            Assert.IsType<InvalidDataException>(await Record.ExceptionAsync(() => MortalQteDecisionPublication.ValidateExactReplayAsync(_fs, lease, genuineReplay)));
        await AssertMortalCompletionEntryHeldAsync(component.Engine);
        foreach (var image in before) Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(image.Key));
        Assert.Equal(foreign, await _fs.ReadFileBytesAsync(path));
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync()) Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(_fs, lease));
        WritePhysicalInjuryDiagnostic("qte_completion_foreign_command_held", new { kind, path, inspected });
    }

    [Fact]
    public async Task QtePhysicalInjuryConsumer_ActualUncommittedRefusalStillRestoresOriginalCanonicalBytes()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        AgentConsoleLiveInputSource? sourceInput = null;
        QteStoryRun source;
        try { source = await RunActualQteStorySourceAsync(physicalDeclaration: true, physicalMaximum: 2, retainLiveInput: input => sourceInput = input); }
        finally { sourceInput?.Dispose(); }
        Assert.Null(source.Failure);
        await AssertActualQteStoryFinalizedAsync(_fs, source.Source!, source.StoryAtHandoff!, 155m);
        await AssertActualPhysicalOccurrenceAsync(source, 2);
        var before = await CaptureMortalComponentImagesAsync();
        var resourceBefore = (await _fs.ReadFileBytesAsync(ResourceMaterializationContract.StatePath))!;
        using var run = CreateMortalArchiveOrdinaryTurn(_fs, false, false, false, cancelRepair: true);
        GetPrivateField<GameLoop>(run.Engine, "_gameLoop").SetSession(source.Source!.SessionId, 42);
        await GetPrivateField<StateManager>(run.Engine, "_stateManager").RefreshGameStateAsync();
        var dirtied = false;
        _consolePublicationObserver = (phase, _) =>
        {
            if (dirtied || phase != TrustedLocalPublicationPhase.Committed) return;
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(Path.Combine(_fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
            if (!journal.RootElement.GetProperty("Members").EnumerateArray().Any(row =>
                    row.GetProperty("Path").GetString() == _fs.ResolvePath("ready/turn_complete.json") &&
                    row.GetProperty("After").GetProperty("Exists").GetBoolean())) return;
            dirtied = true;
            Assert.False(_fs.FileExists(MortalQteDecisionPublication.StatePath));
            File.WriteAllBytes(_fs.ResolvePath(ResourceMaterializationContract.StatePath), resourceBefore.Concat(new byte[] { 10 }).ToArray());
        };
        try { run.Failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(run.Engine, "ProcessPlayerTurn", "Осмотреть ушиб.", null)); }
        finally { _consolePublicationObserver = null; }
        WritePhysicalInjuryDiagnostic("qte_completion_uncommitted_actual_rollback", new
        { dirtied, run.Accepted, Failure = run.Failure?.ToString(), ActualTurn = GetPrivateField<GameLoop>(run.Engine, "_gameLoop").TurnNumber,
            ResourceBefore = MortalArchiveHash(resourceBefore), ResourceAfter = MortalArchiveHash((await _fs.ReadFileBytesAsync(ResourceMaterializationContract.StatePath))!) });
        Assert.True(dirtied);
        Assert.Null(run.Failure);
        Assert.False(run.Accepted);
        Assert.Equal(42, GetPrivateField<GameLoop>(run.Engine, "_gameLoop").TurnNumber);
        Assert.Equal(new[] { 42 }, await MortalArchiveStoryTurnsAsync(_fs));
        Assert.Equal(resourceBefore, await _fs.ReadFileBytesAsync(ResourceMaterializationContract.StatePath));
        foreach (var image in before) Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(image.Key));
        Assert.False(_fs.FileExists(MortalWoundOpportunityReceiptState.StatePath));
        Assert.False(_fs.FileExists(MortalQteDecisionPublication.StatePath));
    }

    private async Task<(GameEngine Engine, PendingTurnSnapshotReadAuthority Snapshot, JsonObject Decision)> PreparePublishedMortalCompletionProofAsync(bool materialize)
    {
        var component = await PrepareMortalDecisionComponentAsync(materialize);
        await WriteMortalReplayResourceWorkAsync();
        Assert.Empty(await ValidateMortalDecisionComponentAsync(component.Engine));
        Assert.NotNull(await GetPrivateField<CanonicalStateNormalizer>(component.Engine, "_normalizer").NormalizeAcceptedMechanicsAsync(null));
        Assert.True(_fs.FileExists(MortalQteDecisionPublication.StatePath));
        await _fs.WriteFileAtomicAsync(MortalWoundOpportunityAdapter.DecisionPath, component.Decision.ToJsonString());
        return component;
    }

    private async Task<MortalQteDecisionPublication.CurrentPublicationInspection> InspectMortalCompletionProofAsync(PendingTurnSnapshotReadAuthority snapshot)
    {
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        return await MortalQteDecisionPublication.InspectCurrentAsync(_fs, lease, snapshot.SessionId, snapshot.RequestId, snapshot.SnapshotToken, snapshot.TurnNumber);
    }

    private async Task<MortalWoundOpportunityReceiptConsumePlanResult> ReadGenuineMortalCompletionReplayAsync(JsonObject decision, bool materialize)
    {
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        var composed = MortalWoundOpportunityAdapter.ComposeQteAcceptedResponse(_fs, lease, decision.ToJsonString(),
            materialize ? MortalComponentAcquisition : "Герой осматривает ушиб.", out var replay);
        Assert.NotNull(composed);
        Assert.True(composed!.Success, JsonSerializer.Serialize(composed.Issues));
        Assert.NotNull(replay);
        Assert.Equal("exact_replay", replay!.Disposition);
        return replay;
    }

    private async Task AssertMortalCompletionEntryHeldAsync(GameEngine engine)
    {
        SetPrivateField(engine, "_inGame", true);
        var manifest = await InvokePrivateTaskNullableResultAsync(engine, "LoadPendingTurnSnapshotManifestAsync");
        Assert.NotNull(manifest);
        var original = await InvokePrivateTaskNullableResultAsync(engine, "LoadValidatedPendingTurnSnapshotContextAsync", manifest, true);
        Assert.NotNull(original);
        var disposition = Assert.IsType<AcceptedTurnValidationDisposition>(await InvokePrivateTaskResultAsync(engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync", "component completed publication control", original, null, 43, null));
        Assert.Equal(AcceptedTurnValidationDisposition.MortalCompletedPublicationHeldBlocked, disposition);
        var preserved = Assert.IsType<bool>(typeof(GameEngine).GetMethod("PreserveAcceptedTurnForTreatmentPublicationRetry",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(engine, [disposition]));
        Assert.True(preserved);
        Assert.False(GetPrivateFieldValue<bool>(engine, "_inGame"));
    }

    private async Task AppendMortalCompletionDriftAsync(string path)
    {
        var bytes = await _fs.ReadFileBytesAsync(path);
        Assert.NotNull(bytes);
        await _fs.WriteFileAtomicBytesAsync(path, bytes!.Concat(new byte[] { 10 }).ToArray());
    }
}
