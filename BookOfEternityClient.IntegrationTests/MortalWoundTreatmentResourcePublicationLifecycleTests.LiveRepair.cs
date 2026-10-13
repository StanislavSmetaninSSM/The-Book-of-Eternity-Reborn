using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task GuaranteedTreatment_LiveSeverityRepairPersistsSameConfirmedRequestBeforeDispatch()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        IDisposable? sourceInput = null;
        QteStoryRun source;
        try
        {
            source = await RunActualQteStorySourceAsync(physicalDeclaration: true, physicalMaximum: 2,
                retainLiveInput: input => sourceInput = input,
                configureBeforePreparation: ConfigureGenuinePendingTreatmentWorldAsync);
        }
        finally { sourceInput?.Dispose(); }
        Assert.Null(source.Failure);
        Assert.True(source.ActionObserved);
        Assert.NotNull(source.Source);
        await AssertGenuinePendingQteSourceAsync(source); // Actual effect-free source10->5, not the old160->155 world.
        var occurrence = await AssertActualPhysicalOccurrenceAsync(source, 2);

        var input = new QueuedConsoleInputSource([Key(ConsoleKey.Escape)]);
        var logger = new SpiritualLifecycleTestLogger();
        var entered = false;
        var engine = CreateGameEngine(input, settings => { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; },
            finalizationHooks: new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = checkpoint =>
                {
                    if (checkpoint == SessionFinalizationCheckpoint.LateTerminalAndIdleOperationBound) entered = true;
                    return Task.CompletedTask;
                }
            }, logger: logger);
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(source.Source!.SessionId, 42);
        await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(_fs, async () =>
        {
            Assert.NotNull(await InvokePrivateTaskNullableResultAsync(engine, "StagePlayerTurnAsync", "Осмотреть ушиб.", null, null));
            return true;
        });

        var contextResult = MortalWoundTreatmentAuthority.ParseContext(new JsonObject
        {
            ["schemaVersion"] = 1, ["realm"] = "mortal_world", ["targetKind"] = "player",
            ["targetId"] = "player_current", ["providerKind"] = "npc", ["providerId"] = "field_medic_01",
            ["currentLocationId"] = "loc_field_clinic_001"
        }.ToJsonString(), "treatmentContext");
        Assert.True(contextResult.IsValid, DescribeValidationIssues(contextResult.Issues));
        var context = Assert.IsType<MortalWoundTreatmentAuthority.Context>(contextResult.Context);
        var scene = HeldTreatmentPipelineContext.FinalSceneText + " " + MortalComponentAcquisition;
        MortalWoundTreatmentAttemptRequest request;
        MortalWoundTreatmentResourceFinalization finalization;
        WoundResponseInputCompositionResult composition;
        PendingTurnSnapshotReadAuthority original;
        JsonObject decision;
        string generation;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            generation = Assert.IsType<string>(_fs.ReadLocalGenerationSnapshot(lease).Binding.Id);
            original = MortalQteWoundOpportunityProjection.ReadSnapshot(_fs, lease);
            Assert.Equal(43, original.TurnNumber);
            Assert.Equal(source.Source.SessionId, original.SessionId);
            Assert.NotEqual(source.Source.RequestId, original.RequestId);
            Assert.NotEqual(source.SnapshotToken, original.SnapshotToken);
            Assert.Equal("mortal_world", original.Realm);
            var offer = Assert.Single(MortalQteWoundOpportunityProjection.Project(original));
            Assert.Equal(occurrence.OpportunityRef, offer["opportunityRef"]!.GetValue<string>());
            Assert.Equal("II", offer["maximumSeverity"]!.GetValue<string>());
            var exported = MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(_fs, lease, context, HeldTreatmentPipelineContext.WoundId);
            Assert.True(exported.IsValid, DescribeValidationIssues(exported.Issues));
            var accepted = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(exported.Authority);
            AssertPendingTreatmentTuple(original, accepted.Binding);
            var history = ReadCurrentTreatmentHistory(_fs);
            var wound = ReadCurrentTreatmentWound(_fs);
            var prepared = MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(accepted, history, wound,
                "operation_fr063_live_repair", HeldTreatmentPipelineContext.RouteId,
                Assert.Single(accepted.Binding.AcceptedEvents).EventRef);
            Assert.True(prepared.IsValid, DescribeValidationIssues(prepared.Issues));
            request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(prepared.Request);
            Assert.Equal(2m, Assert.Single(request.ResourceAuthority.Claims).Quantity);
            var resolved = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(request, history, wound, accepted);
            Assert.Equal("Resolved", resolved.Disposition);
            Assert.Empty(resolved.Issues);
            var resolution = Assert.IsType<MortalWoundTreatmentResolution>(resolved.Resolution);
            var finalized = MortalWoundTreatmentResourceComposer.Finalize(resolution);
            Assert.True(finalized.IsValid, DescribeValidationIssues(finalized.Issues));
            finalization = Assert.IsType<MortalWoundTreatmentResourceFinalization>(finalized.Finalization);
            var command = WoundResponseInputComposer.ComposeMortalWoundTreatmentCommandRoot(accepted.Binding, resolution, scene);
            var parsed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(command));
            Assert.True(parsed.Success, DescribeValidationIssues(parsed.Issues));
            composition = WoundResponseInputComposer.RecomposeCommandRoot(accepted.Binding, parsed, Array.Empty<WoundOpportunityDecisionReceipt>());
            Assert.True(composition.Success, DescribeValidationIssues(composition.Issues));
            decision = new JsonObject { ["opportunityRef"] = occurrence.OpportunityRef, ["decision"] = "materialize",
                ["woundRef"] = "live_pending_impact", ["proposal"] = CreateMortalComponentProposal() };
            decision["proposal"]!["severity"] = "III"; // Only this public severity leaf contradicts the genuine maximumII.
        }
        await new StateDistributor(_fs, NullLogger<StateDistributor>.Instance)
            .DistributeAsync(new GameResponse { Response = scene }, composition);

        Dictionary<string, byte[]?> before;
        JsonObject heldCommand;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.Equal(generation, _fs.ReadLocalGenerationSnapshot(lease).Binding.Id);
            var confirmed = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(_fs, lease, request, finalization);
            WriteConfirmedClaimInspection("live_repair_before_actual_entry", confirmed);
            Assert.True(confirmed.IsValid, DescribeValidationIssues(confirmed.Issues));
            Assert.Equal(MortalWoundTreatmentResourceReservationState.ConfirmedHeld, confirmed.State);
            Assert.Equal(request.RequestFingerprint, confirmed.RequestFingerprint);
            Assert.Null(confirmed.GenerationFingerprint); // This read is diagnostic, not publication authority.
            var definitions = ResourceDefinitionCatalog.ParseCanonical(await _fs.ReadFileAsync(lease, ResourceMaterializationContract.DefinitionsPath), false);
            Assert.True(definitions.IsValid, DescribeValidationIssues(definitions.Issues));
            var resources = ResourceStateContract.ParseCanonical(await _fs.ReadFileAsync(lease, ResourceMaterializationContract.StatePath), definitions.Catalog!, false);
            Assert.True(resources.IsValid, DescribeValidationIssues(resources.Issues));
            Assert.Equal(2m, Assert.Single(resources.Ledger!.Entries, entry => entry.Coordinate.ResourceKey == "energy").Current);
            Assert.Equal(5m, Assert.Single(resources.Ledger.Entries, entry => entry.Coordinate.ResourceKey == "health").Current);
            Assert.Single(ReadCurrentTreatmentHistory(_fs).State!.Transitions);
            Assert.Equal("untreated", ReadCurrentTreatmentWound(_fs).Care.State);
            heldCommand = ParseJsonObjectBytes(Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath)));
            var parsed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(heldCommand));
            Assert.True(parsed.Success, DescribeValidationIssues(parsed.Issues));
            Assert.Empty(parsed.Commands);
            var treatment = Assert.Single(parsed.TreatmentCommands);
            Assert.Equal(request.RequestFingerprint, treatment.RequestFingerprint);
            Assert.True(JsonNode.DeepEquals(WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request), treatment.Request));
            Assert.Equal(scene, treatment.FinalSceneText);
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(_fs, lease));
            await WriteLiveRepairOrdinaryOutputsAsync(lease, original, decision, scene);
            before = await CaptureConfirmedClaimReadImagesAsync(_fs, lease);
        }
        WritePhysicalInjuryDiagnostic("fr063_live_repair_actual_prerequisites", new
        {
            Source = new { source.Source.SessionId, source.Source.RequestId, source.SnapshotToken, source.Source.TurnNumber },
            Original = new { original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber, original.Realm },
            generation, occurrence.OpportunityRef, Scene = scene, Draft = decision,
            FullRequest = WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request), Command = heldCommand,
            ConfirmedHeldBeforeEntry = true, EnergyBefore = 2, HealthBefore = 5, NoPrecomposedCommonPlan = true,
            Before = MortalArchiveDigests(before), Ready = MortalArchiveText(before["ready/turn_complete.json"]!)
        });

        const string repairPath = "game_state/control/validation_repair_request.json";
        const string diagnosticPath = "game_state/control/validation_diagnostic_failure_report.json";
        var publications = new List<object>();
        var milestones = new List<(string Path, string Raw, Dictionary<string, byte[]?> Images)>();
        Exception? observerFailure = null;
        _consolePublicationObserver = ObserveActualRepair;
        Exception? failure;
        var handled = false;
        try
        {
            failure = await Record.ExceptionAsync(async () =>
                handled = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync"));
        }
        finally { _consolePublicationObserver = null; }
        var after = await CaptureMortalArchiveImagesAsync(_fs, before.Keys.Where(path => path != "$sessionGeneration"));
        WritePhysicalInjuryDiagnostic("fr063_live_repair_actual_owner_outcome", new
        {
            ActualCaller = "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync", entered, handled,
            Failure = failure?.ToString(), ObserverFailure = observerFailure?.ToString(), EngineDiagnostics = logger.Describe(),
            ActualTurn = GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber, InputKeyAvailable = input.KeyAvailable,
            Publications = publications, Milestones = milestones.Select(m => new { m.Path, Raw = m.Raw,
                Images = m.Images.ToDictionary(pair => pair.Key, pair => pair.Value is null ? null : Convert.ToBase64String(pair.Value)) }),
            After = MortalArchiveDigests(after)
        });
        Assert.Null(observerFailure);
        Assert.Null(failure);
        Assert.True(entered);
        // First owning-contract oracle, after genuine source/frame/held-request and actual caller evidence.
        Assert.True(milestones.Any(m => m.Path == repairPath),
            "Actual ordinary joint repair owner did not dispatch the genuine severity repair wave while preserving the full held request.");
        var dispatch = Assert.Single(milestones.Where(m => m.Path == repairPath));
        using var repair = JsonDocument.Parse(dispatch.Raw.TrimStart('\uFEFF'));
        Assert.Equal(original.SessionId, repair.RootElement.GetProperty("sessionId").GetString());
        Assert.Equal(original.RequestId, repair.RootElement.GetProperty("requestId").GetString());
        Assert.Equal(43, repair.RootElement.GetProperty("turnNumber").GetInt32());
        Assert.True(repair.RootElement.GetProperty("fullTurnResubmissionRequired").GetBoolean());
        Assert.Contains(repair.RootElement.GetProperty("errors").EnumerateArray(), row =>
            row.GetProperty("code").GetString() == "wound_severity_above_opportunity");
        var packet = Assert.Single(repair.RootElement.GetProperty("harnessRepairPackets").EnumerateArray()
            .Where(row => row.TryGetProperty("candidateKind", out _)));
        Assert.Equal("construct_wound", packet.GetProperty("candidateKind").GetString());
        Assert.Equal(original.SnapshotToken, packet.GetProperty("snapshotToken").GetString());
        var commandAtDispatch = ParseJsonObjectBytes(Assert.IsType<byte[]>(dispatch.Images[AcceptedMechanicsPlan.WoundCommandPath]));
        Assert.True(JsonNode.DeepEquals(heldCommand, commandAtDispatch));
        var pending = ParseJsonObjectBytes(Assert.IsType<byte[]>(dispatch.Images[WoundAcceptedTurnSnapshotContract.PendingResolutionPath]));
        Assert.True(WoundRepairPacketBuilder.IsValidPersistedRepairWave(JsonSerializer.SerializeToElement(pending["repairPackets"]),
            JsonSerializer.SerializeToElement(pending["repairReceipts"]), original.SessionId, original.RequestId, original.SnapshotToken));
        var submitted = Assert.IsType<JsonObject>(Assert.Single(pending["submittedTreatmentRequests"]!.AsArray()));
        Assert.True(JsonNode.DeepEquals(WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request), submitted["request"]));
        Assert.Equal(request.RequestFingerprint, submitted["requestFingerprint"]!.GetValue<string>());
        Assert.IsType<JsonObject>(pending["jointRepairCheckpoint"]);
        var historyAtDispatch = WoundHistoryState.Parse(MortalArchiveText(dispatch.Images[WoundHistoryState.HistoryPath]!), WoundHistoryState.HistoryPath);
        Assert.True(historyAtDispatch.IsValid, DescribeValidationIssues(historyAtDispatch.Issues));
        var catalog = MortalWoundTreatmentPersistedRequestCatalog.Parse(JsonSerializer.SerializeToElement(commandAtDispatch),
            JsonSerializer.SerializeToElement(pending), historyAtDispatch);
        Assert.True(catalog.IsValid, DescribeValidationIssues(catalog.Issues));
        Assert.Single(catalog.Requests); Assert.Single(catalog.HeldRequests); Assert.Empty(catalog.FinalizedRequests);
        foreach (var path in new[] { "input/turn_request.json", LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                     PendingTurnSnapshotAuthority.AuthorityPath, ResourceMaterializationContract.DefinitionsPath,
                     ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                     WoundCarrierCatalog.PlayerPath, WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
                     MortalWoundOccurrenceState.StatePath, MortalWoundOpportunityReceiptState.StatePath })
            Assert.Equal(before[path], dispatch.Images[path]);
        Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);

        void ObserveActualRepair(TrustedLocalPublicationPhase phase, int index)
        {
            if (phase != TrustedLocalPublicationPhase.Committed || observerFailure is not null) return;
            try
            {
                using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(Path.Combine(_fs.RuntimeRootPath,
                    "trusted-local-publication-v1", "active.json")));
                foreach (var member in journal.RootElement.GetProperty("Members").EnumerateArray().Where(row =>
                             row.GetProperty("Path").GetString() == _fs.ResolvePath(repairPath) ||
                             row.GetProperty("Path").GetString() == _fs.ResolvePath(diagnosticPath)))
                {
                    Assert.Equal(1, journal.RootElement.GetProperty("Format").GetInt32());
                    Assert.True(journal.RootElement.GetProperty("Committed").GetBoolean());
                    var path = member.GetProperty("Path").GetString()!;
                    var image = member.GetProperty("After");
                    var exists = image.GetProperty("Exists").GetBoolean();
                    var declared = image.GetProperty("Bytes").ValueKind == JsonValueKind.Null ? null : image.GetProperty("Bytes").GetBytesFromBase64();
                    publications.Add(new { Path = path, Exists = exists, DeclaredBytesLength = declared?.LongLength,
                        Sha256 = image.GetProperty("Sha256").GetString(), TransactionId = journal.RootElement.GetProperty("TransactionId").GetString() });
                    if (!exists) { Assert.Null(declared); Assert.Equal(JsonValueKind.Null, image.GetProperty("Sha256").ValueKind); continue; }
                    var bytes = File.ReadAllBytes(path);
                    Assert.Equal(declared, bytes);
                    Assert.Equal(declared!.LongLength, bytes.LongLength);
                    Assert.Equal(image.GetProperty("Sha256").GetString(), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
                    // Physical evidence only inside the publisher callback: no new lease/registry authority or pending writer.
                    var images = before.Keys.Where(key => key != "$sessionGeneration").ToDictionary(key => key,
                        key => File.Exists(_fs.ResolvePath(key)) ? File.ReadAllBytes(_fs.ResolvePath(key)) : null, StringComparer.Ordinal);
                    images[WoundAcceptedTurnSnapshotContract.PendingResolutionPath] = File.Exists(_fs.ResolvePath(WoundAcceptedTurnSnapshotContract.PendingResolutionPath))
                        ? File.ReadAllBytes(_fs.ResolvePath(WoundAcceptedTurnSnapshotContract.PendingResolutionPath)) : null;
                    milestones.Add((path == _fs.ResolvePath(repairPath) ? repairPath : diagnosticPath, Encoding.UTF8.GetString(bytes), images));
                }
            }
            catch (Exception error) { observerFailure = error; input.Enqueue(Key(ConsoleKey.Escape)); }
        }
    }

    private async Task WriteLiveRepairOrdinaryOutputsAsync(FileSystemManager.CanonicalWriteLease lease,
        PendingTurnSnapshotReadAuthority original, JsonObject decision, string scene)
    {
        var timestamp = DateTime.UtcNow.ToString("O");
        await _fs.WriteFileAtomicAsync(lease, MortalWoundOpportunityAdapter.DecisionPath, decision.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(lease, "output/narrative_response.json", JsonSerializer.Serialize(new { response = scene, timestamp }));
        await _fs.WriteFileAtomicAsync(lease, "output/interface_updates.json", JsonSerializer.Serialize(new { dialogueOptions = Array.Empty<object>(), timestamp }));
        await _fs.WriteFileAtomicAsync(lease, "output/debug_logs.json", JsonSerializer.Serialize(new
        {
            timestamp, gm_thoughts_markdown = "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: нет\n" +
                "- Why relevant: Уже запечатанное клиентом лечение и описание собственного пореза; нового решения NPC нет.\n" +
                "- Actors outside scope: Field medic\n- Why outside scope: Нет нового взаимодействия, реплик или изменений NPC.\n\n" +
                "## Reasoning\n- The client owns the held treatment. The single physical QTE draft supplies its complete acquisition narration."
        }));
        string[] modified = ["output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json", MortalWoundOpportunityAdapter.DecisionPath];
        await _fs.WriteFileAtomicAsync(lease, "ready/turn_complete.json", JsonSerializer.Serialize(new
        { sessionId = original.SessionId, requestId = original.RequestId, turnNumber = original.TurnNumber,
            status = "success", timestamp, filesModified = modified }));
        foreach (var path in modified) Assert.NotNull(await _fs.ReadFileBytesAsync(lease, path));
    }
}
