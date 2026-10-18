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
    public Task GuaranteedTreatment_LiveSeverityRepairCompletesJointOnceWithZeroCycleProgression() =>
        RunJointCompletionControlAsync(nonzeroProgression: false);

    [Fact]
    public Task GuaranteedTreatment_LiveSeverityRepairConsumesMatchingNonzeroReportExactlyOnce() =>
        RunJointCompletionControlAsync(nonzeroProgression: true);

    private async Task RunJointCompletionControlAsync(bool nonzeroProgression, bool pauseAppliedForCold = false, bool pauseUnappliedForCold = false,
        string? refusalScenario = null, bool newPhysicalRootForCold = false, string? activeTailScenario = null,
        bool useLiveCaller = false, bool recoverRetainedStoryRow = false, bool normalizeNarrativeBeforeCheckpoint = false,
        Action<AcceptedMechanicsPlan?>? observeSettledCommonPlan = null,
        bool postOriginalAutomaticSubmission = false, Action<GameEngine>? attachActualOwnerProbe = null,
        JointCaptureCutProbe? currentCaptureProbe = null,
        JointInterfaceLifecycleProbe? interfaceLifecycleProbe = null)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        IDisposable? sourceInput = null;
        QteStoryRun source;
        try
        {
            source = await RunActualQteStorySourceAsync(physicalDeclaration: true, physicalMaximum: 2,
                retainLiveInput: input => sourceInput = input,
                configureBeforePreparation: ConfigureJointCompletionWorldBeforeOriginalAsync);
        }
        finally { sourceInput?.Dispose(); }
        Assert.Null(source.Failure);
        Assert.True(source.ActionObserved);
        Assert.NotNull(source.Source);
        await AssertGenuinePendingQteSourceAsync(source); // Actual effect-free source10->5, not the old160->155 world.
        var occurrence = await AssertActualPhysicalOccurrenceAsync(source, 2);
        var priorLifecycleTail = await AssertJointPriorLifecycleInactiveBaselineAsync(source);
        var jointOriginalTail = activeTailScenario is null ? priorLifecycleTail :
            await AuthorJointActiveTailBeforeOriginalAsync(activeTailScenario, priorLifecycleTail);
        // Detached expectations only: the real owner must consume the reminder
        // INSIDE Stage43, after backup. Never pre-consume or recapture expected bytes.
        var (jointCurrentTail, priorQteReminder) = BuildJointNaturalStage43CurrentExpectation(jointOriginalTail);

        if (nonzeroProgression)
        {
            await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            Assert.Null(await _fs.ReadFileBytesAsync(lease, "input/turn_request.json"));
            Assert.Null(await _fs.ReadFileBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
            var clock = ParseJsonObjectBytes(Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease, "game_state/world/world_time.json")));
            var schedule = StrictJsonAuthority.Deserialize<ProgressionScheduleState>(
                CanonicalJsonUtf8.DecodeOneOptionalBom(Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease, ProgressionScheduleService.SchedulePath))),
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "actual pre-original43 progression schedule");
            Assert.NotNull(schedule);
            // Public current world time is a fixture input BEFORE Stage43 signs
            // its original. Actual BuildControl derives the nonzero obligation.
            clock["currentTimeInMinutes"] = checked(clock["currentTimeInMinutes"]!.GetValue<int>() + schedule!.WorldCycleMinutes);
            await _fs.WriteFileAtomicAsync(lease, "game_state/world/world_time.json", clock.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        }

        var input = new QueuedConsoleInputSource([]);
        var logger = new SpiritualLifecycleTestLogger();
        var entered = false;
        var liveWaitEntered = false;
        var jointOriginalAuthored = false;
        var appliedPauseReached = false;
        var unappliedPauseReached = false;
        var contextResult = MortalWoundTreatmentAuthority.ParseContext(new JsonObject
        {
            ["schemaVersion"] = 1, ["realm"] = "mortal_world", ["targetKind"] = "player",
            ["targetId"] = "player_current", ["providerKind"] = "npc", ["providerId"] = "field_medic_01",
            ["currentLocationId"] = "loc_field_clinic_001"
        }.ToJsonString(), "treatmentContext");
        Assert.True(contextResult.IsValid, DescribeValidationIssues(contextResult.Issues));
        var context = Assert.IsType<MortalWoundTreatmentAuthority.Context>(contextResult.Context);
        var scene = HeldTreatmentPipelineContext.FinalSceneText + " " + MortalComponentAcquisition +
            (normalizeNarrativeBeforeCheckpoint ? "\nРассказ продолжается." : string.Empty);
        MortalWoundTreatmentAttemptRequest request = null!;
        MortalWoundTreatmentResourceFinalization finalization = null!;
        WoundResponseInputCompositionResult composition = null!;
        PendingTurnSnapshotReadAuthority original = null!;
        byte[] signedOriginalSchedule = null!;
        JsonObject decision = null!;
        string generation = null!;
        Dictionary<string, byte[]?> before = new(StringComparer.Ordinal);
        var authoredPublicOutputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        HotJointSubmissionFixture? hotSubmission = null;
        byte[]? correctedNarrativeForBinding = null;
        byte[]? authoredProgressionReport = null;
        JsonObject heldCommand = new();

        GameEngine engine = null!;
        engine = CreateGameEngine(input, settings => { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; },
            finalizationHooks: new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = checkpoint =>
                {
                    if (checkpoint == SessionFinalizationCheckpoint.LateTerminalAndIdleOperationBound) entered = true;
                    if (useLiveCaller && checkpoint == SessionFinalizationCheckpoint.TerminalWaitStarted && !jointOriginalAuthored)
                    {
                        entered = true; liveWaitEntered = true;
                        return PrepareJointOriginalAsync();
                    }
                    if (observeSettledCommonPlan is not null &&
                        checkpoint == SessionFinalizationCheckpoint.MortalJointCommonReadbackSettledBeforeProgression)
                    {
                        // Read-only observation of the actual retained owner after its
                        // lease closes. No capability construction or owner invocation.
                        var refresh = GetPrivateFieldValue<object?>(engine, "_mortalJointRepairCanonicalRefresh");
                        observeSettledCommonPlan(refresh?.GetType().GetProperty("Plan")?.GetValue(refresh)
                            as AcceptedMechanicsPlan);
                    }
                    if (pauseUnappliedForCold && checkpoint == SessionFinalizationCheckpoint.MortalJointCommonReadbackSettledBeforeProgression)
                    {
                        unappliedPauseReached = true;
                        throw new InvalidDataException("joint known-common-unapplied test pause");
                    }
                    if (pauseAppliedForCold && checkpoint == SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization)
                    {
                        appliedPauseReached = true;
                        throw new OperationCanceledException("joint applied-before-story test pause");
                    }
                    return Task.CompletedTask;
                }
            }, logger: logger);
        attachActualOwnerProbe?.Invoke(engine);
        currentCaptureProbe?.Attach(engine);
        interfaceLifecycleProbe?.Attach(engine);
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(source.Source!.SessionId, 42);
        interfaceLifecycleProbe?.ConfigureBeforeOriginal();
        await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
        if (!useLiveCaller)
            await SessionOperationContext.RunParticipatingCurrentSessionAsync(_fs, async () =>
            {
                Assert.NotNull(await InvokePrivateTaskNullableResultAsync(engine, "StagePlayerTurnAsync", "Осмотреть ушиб.", null, null));
                return true;
            });

        if (!useLiveCaller) await PrepareJointOriginalAsync();

        async Task PrepareJointOriginalAsync()
        {
            await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            {
                generation = Assert.IsType<string>(_fs.ReadLocalGenerationSnapshot(lease).Binding.Id);
                original = MortalQteWoundOpportunityProjection.ReadSnapshot(_fs, lease);
                // The progression basis is the SIGNED original, not the later
                // current schedule that ordinary GM preparation may have updated.
                // The QTE-only reader above does not select schedule coverage.
                var progressionRead = PendingTurnSnapshotReader.ReadCurrent(_fs, lease,
                    new PendingTurnSnapshotPathSelection(["game_state/meta/soul_state.json", ProgressionScheduleService.SchedulePath], []));
                WritePhysicalInjuryDiagnostic("fr063_joint_original_progression_reader_before_owner", new
                {
                    progressionRead.Success, Issues = progressionRead.Issues.Select(issue => issue.Code).ToArray(),
                    Expected = new { original.Realm, original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber },
                    Actual = progressionRead.Snapshot is { } observed
                        ? new { observed.Realm, observed.SessionId, observed.RequestId, observed.SnapshotToken, observed.TurnNumber } : null
                });
                Assert.True(progressionRead.Success, DescribeValidationIssues(progressionRead.Issues));
                var progressionOriginal = Assert.IsType<PendingTurnSnapshotReadAuthority>(progressionRead.Snapshot);
                Assert.Equal(original.Realm, progressionOriginal.Realm);
                Assert.Equal(original.SessionId, progressionOriginal.SessionId); Assert.Equal(original.RequestId, progressionOriginal.RequestId);
                Assert.Equal(original.SnapshotToken, progressionOriginal.SnapshotToken); Assert.Equal(original.TurnNumber, progressionOriginal.TurnNumber);
                signedOriginalSchedule = progressionOriginal.ReadRequiredBytes(ProgressionScheduleService.SchedulePath).ToArray();
                WritePhysicalInjuryDiagnostic("fr063_joint_signed_original_schedule_before_owner", new
                { original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber, ScheduleBytes = signedOriginalSchedule });
                foreach (var image in jointCurrentTail)
                    AssertJointNaturalStage43Image("current", image.Key, image.Value,
                        await _fs.ReadFileBytesAsync(lease, image.Key));
                await AssertJointNaturalStage43OriginalAndRollbackAsync(lease, original, jointOriginalTail, priorQteReminder);
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
                    "operation_fr063_joint_completion", HeldTreatmentPipelineContext.RouteId,
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

            await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.Equal(generation, _fs.ReadLocalGenerationSnapshot(lease).Binding.Id);
                var confirmed = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(_fs, lease, request, finalization);
                WriteConfirmedClaimInspection("joint_completion_before_actual_entry", confirmed);
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
                await WriteLiveRepairOrdinaryOutputsAsync(lease, original, decision,
                    normalizeNarrativeBeforeCheckpoint ? scene.Replace("\n", "`n", StringComparison.Ordinal) : scene);
                if (postOriginalAutomaticSubmission)
                {
                    Assert.True(useLiveCaller);
                    Assert.Null(activeTailScenario);
                    hotSubmission = await AuthorHotJointSubmissionAfterOriginalAsync(lease, original);
                    foreach (var submitted in hotSubmission.AuthoredPublicInputs)
                        authoredPublicOutputs.Add(submitted.Key, submitted.Value.ToArray());
                }
                if (activeTailScenario == "new_qte_offer")
                {
                    // Stage43 signed this independent input, then correctly cleared
                    // transient output. The GM now submits the same public offer.
                    var offerBytes = Assert.IsType<byte[]>(jointOriginalTail[QteSceneService.QteOfferPath]);
                    Assert.Null(await _fs.ReadFileBytesAsync(lease, QteSceneService.QteOfferPath));
                    await _fs.WriteFileAtomicBytesAsync(lease, QteSceneService.QteOfferPath, offerBytes);
                    Assert.Equal(offerBytes, await _fs.ReadFileBytesAsync(lease, QteSceneService.QteOfferPath));
                    var ready = ParseJsonObjectBytes(Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease, "ready/turn_complete.json")));
                    var modified = ready["filesModified"]!.AsArray();
                    Assert.DoesNotContain(modified, path => path!.GetValue<string>() == QteSceneService.QteOfferPath);
                    modified.Add(QteSceneService.QteOfferPath);
                    // Honest initial Ready remains last; all correlation fields
                    // and the original response timestamp are retained.
                    await _fs.WriteFileAtomicAsync(lease, "ready/turn_complete.json",
                        ready.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                    authoredPublicOutputs.Add(QteSceneService.QteOfferPath, offerBytes.ToArray());
                }
                if (nonzeroProgression) await WriteJointCompletionMatchingReportAsync(lease, original);
                // Freeze actual authored43 public bytes before the real repair restores
                // original baseline. This is a GM response, never private restore authority.
                foreach (var path in WoundAcceptedTurnSnapshotContract.OutputPaths)
                    authoredPublicOutputs.Add(path,
                        Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease, path)).ToArray());
                if (nonzeroProgression)
                    authoredProgressionReport = Assert.IsType<byte[]>(
                        await _fs.ReadFileBytesAsync(lease, ProgressionScheduleService.ReportPath)).ToArray();
                before = await CaptureConfirmedClaimReadImagesAsync(_fs, lease);
                if (activeTailScenario is not null || postOriginalAutomaticSubmission)
                    Assert.False(before.ContainsKey("game_state/control/gm_trajectory_ledger.jsonl"));
                var actualRequest = StrictJsonAuthority.Deserialize<TurnRequest>(
                    CanonicalJsonUtf8.DecodeOneOptionalBom(before["input/turn_request.json"]!),
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "actual joint original43 request");
                Assert.NotNull(actualRequest);
                Assert.NotNull(actualRequest!.ProgressionControl);
                Assert.Equal(nonzeroProgression ? 1 : 0, actualRequest.ProgressionControl!.WorldCyclesAlreadyPendingBeforeTurn);
                Assert.Equal(0, actualRequest.ProgressionControl.FactionCyclesAlreadyPendingBeforeTurn);
                if (activeTailScenario is null && !postOriginalAutomaticSubmission)
                    await GameEngine.ValidateCurrentMortalJointInactiveTailAsync(_fs, lease, original.TurnNumber);
            }
            WritePhysicalInjuryDiagnostic("fr063_joint_completion_actual_prerequisites", new
            {
                Source = new { source.Source.SessionId, source.Source.RequestId, source.SnapshotToken, source.Source.TurnNumber },
                Original = new { original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber, original.Realm },
                nonzeroProgression, generation, occurrence.OpportunityRef, Scene = scene, Draft = decision,
                FullRequest = WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request), Command = heldCommand,
                ConfirmedHeldBeforeEntry = true, EnergyBefore = 2, HealthBefore = 5, NoPrecomposedCommonPlan = true,
                Before = MortalArchiveDigests(before), Ready = MortalArchiveText(before["ready/turn_complete.json"]!)
            });
            if (interfaceLifecycleProbe is { UsesGenuineLegacyProducer: true })
                await interfaceLifecycleProbe.InstallGenuineLegacyWaveAsync(engine, original);
            jointOriginalAuthored = true;
        }

        const string repairPath = "game_state/control/validation_repair_request.json";
        const string diagnosticPath = "game_state/control/validation_diagnostic_failure_report.json";
        var publications = new List<object>();
        var repairReplies = 0;
        var reportDeletions = 0;
        string? baselineReportRemovalTransaction = null;
        string? progressionReportConsumptionTransaction = null;
        var reportRemovalHistory = new List<object>();
        var milestones = new List<(string Path, string Raw, Dictionary<string, byte[]?> Images)>();
        Exception? observerFailure = null;
        _consolePublicationObserver = (phase, index) =>
        {
            ObserveActualRepair(phase, index);
            currentCaptureProbe?.ObservePublication(phase, index);
            interfaceLifecycleProbe?.ObservePublication(phase, index);
        };
        Exception? failure;
        var handled = false;
        var liveCallerReturned = false;
        try
        {
            failure = await Record.ExceptionAsync(async () =>
            {
                if (useLiveCaller)
                {
                    await InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Осмотреть ушиб.", null);
                    liveCallerReturned = true;
                }
                else handled = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
            });
        }
        finally { _consolePublicationObserver = null; }
        WriteJointInMemoryOwnerSnapshot(engine);
        var after = await CaptureMortalArchiveImagesAsync(_fs, before.Keys.Where(path => path != "$sessionGeneration"));
        WritePhysicalInjuryDiagnostic("fr063_joint_completion_actual_owner_outcome", new
        {
            nonzeroProgression, ActualCaller = useLiveCaller ? "ProcessPlayerTurn" : "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync",
            entered, HandledByActualLateCaller = useLiveCaller ? (bool?)null : handled,
            liveCallerReturned, liveWaitEntered, jointOriginalAuthored,
            Failure = failure?.ToString(), ObserverFailure = observerFailure?.ToString(), EngineDiagnostics = logger.Describe(),
            ActualTurn = GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber, InputKeyAvailable = input.KeyAvailable,
            Publications = publications, ReportRemovalHistory = reportRemovalHistory,
            Milestones = milestones.Select(m => new { m.Path, Raw = m.Raw,
                Images = m.Images.ToDictionary(pair => pair.Key, pair => pair.Value is null ? null : Convert.ToBase64String(pair.Value)) }),
            After = MortalArchiveDigests(after)
        });
        if (postOriginalAutomaticSubmission)
        {
            Assert.True(liveCallerReturned);
            await AssertHotJointPostOriginalCustodyAsync(engine, original, request, finalization,
                before, signedOriginalSchedule, Assert.IsType<byte[]>(jointOriginalTail[QteSceneService.QteRuntimePath]),
                heldCommand, milestones, repairReplies, entered, handled, failure, observerFailure,
                Assert.IsType<HotJointSubmissionFixture>(hotSubmission));
            return;
        }
        if (activeTailScenario is not null)
        {
            await AssertJointActiveTailRefusalAsync(activeTailScenario, engine, original, request, finalization,
                before, signedOriginalSchedule, Assert.IsType<byte[]>(jointOriginalTail[QteSceneService.QteRuntimePath]),
                heldCommand, milestones, repairReplies, entered, handled, failure, observerFailure);
            return;
        }
        if (interfaceLifecycleProbe is { ExpectsInitialClose: true })
        {
            Assert.Null(observerFailure); Assert.True(entered); Assert.True(jointOriginalAuthored);
            Assert.True(useLiveCaller); Assert.True(liveWaitEntered);
            interfaceLifecycleProbe.AssertInitialCloseHeld(engine, failure);
            return; // explicit owning-close case, not the ordinary repair-dispatch/completion branch
        }
        Assert.Null(observerFailure);
        Assert.Null(failure);
        Assert.True(entered);
        Assert.True(jointOriginalAuthored);
        Assert.Equal(useLiveCaller, liveWaitEntered);
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
        Assert.Equal(signedOriginalSchedule, dispatch.Images[ProgressionScheduleService.SchedulePath]);
        Assert.Equal(1, repairReplies);
        if (currentCaptureProbe is { StopRequested: true })
        {
            currentCaptureProbe.AssertStopped(engine);
            return; // New deliberate held-cut branch only; default completion oracles remain below.
        }
        if (interfaceLifecycleProbe is { ExpectsHeld: true })
        {
            interfaceLifecycleProbe.AssertHeld(engine, logger.Describe());
            return; // genuine prerequisites above; selected ErrorPrepared cannot enter ordinary completion
        }
        if (normalizeNarrativeBeforeCheckpoint)
        {
            var correctedNarrative = Assert.IsType<byte[]>(correctedNarrativeForBinding);
            var markers = milestones.Where(row => row.Path == MortalQteDecisionPublication.StatePath).ToArray();
            Assert.NotEmpty(markers);
            foreach (var marker in markers)
            {
                var binding = ParseJsonObjectBytes(Encoding.UTF8.GetBytes(marker.Raw))["outputBinding"]!;
                Assert.Equal(correctedNarrative, Convert.FromBase64String(binding["narrativeBase64"]!.GetValue<string>()));
                Assert.Equal(MortalArchiveHash(correctedNarrative), binding["narrativeHash"]!.GetValue<string>());
            }
        }
        if (pauseAppliedForCold || pauseUnappliedForCold)
        {
            Assert.True(pauseUnappliedForCold ? unappliedPauseReached : appliedPauseReached);
            Assert.True(handled); // Real owning held outcome retains completion evidence.
            if (pauseAppliedForCold)
            {
                // Verify genuine report consumption and frozen schedule arithmetic
                // BEFORE returning to a fresh process. Unapplied must remain beforephase.
                await AssertJointProgressionReportOutcomeAsync(nonzeroProgression, original, signedOriginalSchedule, before, milestones);
                Assert.Equal(nonzeroProgression ? 1 : 0, reportDeletions);
                Assert.Equal(nonzeroProgression, baselineReportRemovalTransaction is not null);
                Assert.Equal(nonzeroProgression, progressionReportConsumptionTransaction is not null);
            }
            await RunJointAppliedBeforeStoryColdAsync(engine, original, request.RequestFingerprint, before, scene,
                expectedPhase: pauseUnappliedForCold ? "unapplied" : "applied", refusalScenario: refusalScenario,
                newPhysicalRoot: newPhysicalRootForCold, recoverRetainedStoryRow: recoverRetainedStoryRow);
            interfaceLifecycleProbe?.ObserveCompletionBeforeCleanup();
            return;
        }
        await AssertJointCompletionPhysicalOutcomeAsync(engine, original, request, occurrence, before, scene, milestones);
        await AssertJointProgressionReportOutcomeAsync(nonzeroProgression, original, signedOriginalSchedule, before, milestones);
        Assert.Equal(nonzeroProgression ? 1 : 0, reportDeletions);
        Assert.Equal(nonzeroProgression, baselineReportRemovalTransaction is not null);
        Assert.Equal(nonzeroProgression, progressionReportConsumptionTransaction is not null);
        if (useLiveCaller) Assert.True(liveCallerReturned);
        else Assert.False(handled); // Actual caller completion follows the strict full-union oracles.
        Dictionary<string, byte[]?> complete;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            complete = await CaptureConfirmedClaimReadImagesAsync(_fs, lease);
        var repeat = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
        Dictionary<string, byte[]?> repeated;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            repeated = await CaptureConfirmedClaimReadImagesAsync(_fs, lease);
            await AssertConfirmedClaimReadImagesAsync(_fs, lease, complete);
        }
        WritePhysicalInjuryDiagnostic("fr063_joint_completion_inert_repeat", new
        { repeat, Turn = GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber,
            Before = MortalArchiveDigests(complete), After = MortalArchiveDigests(repeated), FullBytesEqual = true });
        Assert.False(repeat);
        Assert.Equal(43, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
        interfaceLifecycleProbe?.ObserveCompletionBeforeCleanup();
        if (interfaceLifecycleProbe is not null) await interfaceLifecycleProbe.ObserveNewOrdinaryBeforeCleanupAsync(engine);

        void ObserveActualRepair(TrustedLocalPublicationPhase phase, int index)
        {
            if (!jointOriginalAuthored || phase != TrustedLocalPublicationPhase.Committed || observerFailure is not null) return;
            try
            {
                using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(Path.Combine(_fs.RuntimeRootPath,
                    "trusted-local-publication-v1", "active.json")));
                foreach (var member in journal.RootElement.GetProperty("Members").EnumerateArray().Where(row =>
                             row.GetProperty("Path").GetString() == _fs.ResolvePath(repairPath) ||
                             row.GetProperty("Path").GetString() == _fs.ResolvePath(diagnosticPath) ||
                             row.GetProperty("Path").GetString() == _fs.ResolvePath(MortalQteDecisionPublication.StatePath) ||
                             row.GetProperty("Path").GetString() == _fs.ResolvePath(ProgressionScheduleService.ReportPath)))
                {
                    Assert.Equal(1, journal.RootElement.GetProperty("Format").GetInt32());
                    Assert.True(journal.RootElement.GetProperty("Committed").GetBoolean());
                    var path = member.GetProperty("Path").GetString()!;
                    var image = member.GetProperty("After");
                    var exists = image.GetProperty("Exists").GetBoolean();
                    var declared = image.GetProperty("Bytes").ValueKind == JsonValueKind.Null ? null : image.GetProperty("Bytes").GetBytesFromBase64();
                    publications.Add(new { Path = path, Exists = exists, DeclaredBytesLength = declared?.LongLength,
                        Sha256 = image.GetProperty("Sha256").GetString(), TransactionId = journal.RootElement.GetProperty("TransactionId").GetString() });
                    if (!exists)
                    {
                        Assert.Null(declared); Assert.Equal(JsonValueKind.Null, image.GetProperty("Sha256").ValueKind);
                        if (path == _fs.ResolvePath(ProgressionScheduleService.ReportPath) && member.GetProperty("Before").GetProperty("Exists").GetBoolean())
                        {
                            Assert.Equal(before[ProgressionScheduleService.ReportPath], member.GetProperty("Before").GetProperty("Bytes").GetBytesFromBase64());
                            ObserveReportRemoval(journal.RootElement, member);
                        }
                        continue;
                    }
                    var bytes = File.ReadAllBytes(path);
                    Assert.Equal(declared, bytes);
                    Assert.Equal(declared!.LongLength, bytes.LongLength);
                    Assert.Equal(image.GetProperty("Sha256").GetString(), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
                    // Physical evidence only inside the publisher callback: no new lease/registry authority or pending writer.
                    var images = before.Keys.Where(key => key != "$sessionGeneration").ToDictionary(key => key,
                        key => File.Exists(_fs.ResolvePath(key)) ? File.ReadAllBytes(_fs.ResolvePath(key)) : null, StringComparer.Ordinal);
                    images[WoundAcceptedTurnSnapshotContract.PendingResolutionPath] = File.Exists(_fs.ResolvePath(WoundAcceptedTurnSnapshotContract.PendingResolutionPath))
                        ? File.ReadAllBytes(_fs.ResolvePath(WoundAcceptedTurnSnapshotContract.PendingResolutionPath)) : null;
                    foreach (var progressionPath in new[] { ProgressionScheduleService.SchedulePath, ProgressionScheduleService.ReportPath })
                        images[progressionPath] = File.Exists(_fs.ResolvePath(progressionPath)) ? File.ReadAllBytes(_fs.ResolvePath(progressionPath)) : null;
                    var relative = path == _fs.ResolvePath(repairPath) ? repairPath :
                        path == _fs.ResolvePath(MortalQteDecisionPublication.StatePath) ? MortalQteDecisionPublication.StatePath :
                        path == _fs.ResolvePath(ProgressionScheduleService.ReportPath) ? ProgressionScheduleService.ReportPath : diagnosticPath;
                    milestones.Add((relative, Encoding.UTF8.GetString(bytes), images));
                    if (relative == repairPath)
                    {
                        using var actual = JsonDocument.Parse(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
                        var root = actual.RootElement;
                        Assert.True(root.GetProperty("fullTurnResubmissionRequired").GetBoolean());
                        Assert.Contains(root.GetProperty("errors").EnumerateArray(), error =>
                            error.GetProperty("code").GetString() == "wound_severity_above_opportunity");
                        var packet = Assert.Single(root.GetProperty("harnessRepairPackets").EnumerateArray()
                            .Where(row => row.TryGetProperty("candidateKind", out _)));
                        Assert.Equal("construct_wound", packet.GetProperty("candidateKind").GetString());
                        Assert.Equal(original.SnapshotToken, packet.GetProperty("snapshotToken").GetString());
                        Assert.Equal(0, repairReplies++); // Unexpected extra repair is a failure, never automatic retry.
                        var authoredDecision = ParseJsonObjectBytes(authoredPublicOutputs[MortalWoundOpportunityAdapter.DecisionPath]);
                        Assert.True(JsonNode.DeepEquals(decision, authoredDecision));
                        var corrected = authoredDecision.DeepClone().AsObject();
                        corrected["proposal"]!["severity"] = "II";
                        var resubmittedPublicOutputs = authoredPublicOutputs.ToDictionary(
                            pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
                        if (normalizeNarrativeBeforeCheckpoint)
                        {
                            var initialPublicScene = ParseJsonObjectBytes(authoredPublicOutputs["output/narrative_response.json"]);
                            Assert.Equal(scene.Replace("\n", "`n", StringComparison.Ordinal), initialPublicScene["response"]!.GetValue<string>());
                            var checkpoint = ParseJsonObjectBytes(Assert.IsType<byte[]>(
                                images[WoundAcceptedTurnSnapshotContract.PendingResolutionPath]))["jointRepairCheckpoint"]!;
                            var capturedBytes = Convert.FromBase64String(checkpoint["narrativeBase64"]!.GetValue<string>());
                            Assert.Equal(MortalArchiveHash(capturedBytes), checkpoint["narrativeHash"]!.GetValue<string>());
                            Assert.False(AcceptedTurnOutputProjector.ProjectNarrative(CanonicalJsonUtf8.DecodeOneOptionalBom(capturedBytes)).Changed);
                            // The genuine FIRST checkpoint is an oracle only. Public reply
                            // fields come from the initial GM input and the known scene.
                            var expectedFirstScene = initialPublicScene.DeepClone().AsObject();
                            expectedFirstScene["response"] = scene;
                            Assert.True(JsonNode.DeepEquals(expectedFirstScene, ParseJsonObjectBytes(capturedBytes)));
                            var reply = expectedFirstScene.DeepClone().AsObject();
                            var firstTimestamp = initialPublicScene["timestamp"]!.GetValue<string>();
                            reply["timestamp"] = DateTimeOffset.Parse(firstTimestamp, System.Globalization.CultureInfo.InvariantCulture)
                                .AddSeconds(1).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                            Assert.NotEqual(firstTimestamp, reply["timestamp"]!.GetValue<string>());
                            correctedNarrativeForBinding = Encoding.UTF8.GetBytes(reply.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                            resubmittedPublicOutputs["output/narrative_response.json"] = correctedNarrativeForBinding;
                        }
                        interfaceLifecycleProbe?.ConfigureCorrectedOutputs(resubmittedPublicOutputs);
                        // Offline MAIN-GM resubmits the COMPLETE actual43 public turn
                        // after real baseline restore. The normalized positive additionally
                        // exercises the permitted pre-seal timestamp-only scene correction.
                        // Never copy canonical/private command, pending, source or seal roots.
                        foreach (var publicOutput in resubmittedPublicOutputs)
                        {
                            if (publicOutput.Key == MortalWoundOpportunityAdapter.DecisionPath) continue;
                            File.WriteAllBytes(_fs.ResolvePath(publicOutput.Key), publicOutput.Value);
                        }
                        File.WriteAllText(_fs.ResolvePath(MortalWoundOpportunityAdapter.DecisionPath),
                            corrected.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed), new UTF8Encoding(false));
                        if (nonzeroProgression)
                            File.WriteAllBytes(_fs.ResolvePath(ProgressionScheduleService.ReportPath),
                                Assert.IsType<byte[]>(authoredProgressionReport));
                        foreach (var publicOutput in resubmittedPublicOutputs)
                        {
                            if (publicOutput.Key != MortalWoundOpportunityAdapter.DecisionPath)
                                Assert.Equal(publicOutput.Value, File.ReadAllBytes(_fs.ResolvePath(publicOutput.Key)));
                        }
                        if (nonzeroProgression)
                            Assert.Equal(authoredProgressionReport, File.ReadAllBytes(_fs.ResolvePath(ProgressionScheduleService.ReportPath)));
                        var actualCorrected = ParseJsonObjectBytes(File.ReadAllBytes(_fs.ResolvePath(MortalWoundOpportunityAdapter.DecisionPath)));
                        Assert.True(JsonNode.DeepEquals(corrected, actualCorrected));
                        var severityReverted = actualCorrected.DeepClone().AsObject();
                        severityReverted["proposal"]!["severity"] = "III";
                        Assert.True(JsonNode.DeepEquals(authoredDecision, severityReverted));
                        Assert.Equal(scene, ParseJsonObjectBytes(resubmittedPublicOutputs["output/narrative_response.json"])["response"]!.GetValue<string>());
                        interfaceLifecycleProbe?.ObserveCorrectedSubmissionReadback();
                        // Correlated repair Ready is LAST, after complete public readback.
                        File.WriteAllText(_fs.ResolvePath("game_state/control/validation_repair_ready.json"),
                            JsonSerializer.Serialize(new { sessionId = original.SessionId, requestId = original.RequestId,
                                turnNumber = original.TurnNumber, updatedAtUtc = DateTime.UtcNow.ToString("O"),
                                note = "Полный исходный ход повторно представлен; только разрешённая тяжесть исправлена на II." }), new UTF8Encoding(false));
                    }
                }
            }
            catch (Exception error) { observerFailure = error; input.Enqueue(Key(ConsoleKey.Escape)); }
        }

        void ObserveReportRemoval(JsonElement journal, JsonElement reportMember)
        {
            Assert.True(nonzeroProgression);
            var transactionId = Assert.IsType<string>(journal.GetProperty("TransactionId").GetString());
            Assert.NotEmpty(transactionId);
            var members = journal.GetProperty("Members").EnumerateArray().ToArray();
            var paths = members.Select(member => member.GetProperty("Path").GetString()!).ToArray();
            var isProgressionPhase = paths.ToHashSet(StringComparer.Ordinal).SetEquals(new[]
            {
                _fs.ResolvePath(ProgressionScheduleService.SchedulePath),
                _fs.ResolvePath(ProgressionScheduleService.ReportPath),
                _fs.ResolvePath(MortalQteDecisionPublication.StatePath)
            });
            // Keep both real transactions. A pre-dispatch baseline removal is
            // distinct from consuming the resubmitted report in the exact phase.
            reportRemovalHistory.Add(new { TransactionId = transactionId,
                Role = isProgressionPhase ? "progression_consumption" : "original_baseline_restore",
                Members = members.Select(member => member.Clone()).ToArray() });
            Assert.False(File.Exists(_fs.ResolvePath(ProgressionScheduleService.ReportPath)));
            if (!isProgressionPhase)
            {
                Assert.Null(baselineReportRemovalTransaction);
                Assert.Null(progressionReportConsumptionTransaction);
                Assert.Equal(0, repairReplies);
                Assert.DoesNotContain(milestones, milestone => milestone.Path == repairPath ||
                    milestone.Path == MortalQteDecisionPublication.StatePath);
                Assert.False(File.Exists(_fs.ResolvePath(MortalWoundOpportunityAdapter.DecisionPath)));
                Assert.True(JsonNode.DeepEquals(heldCommand, ParseJsonObjectBytes(
                    File.ReadAllBytes(_fs.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath)))));
                baselineReportRemovalTransaction = transactionId;
                return;
            }

            Assert.Equal(3, members.Length);
            Assert.NotNull(baselineReportRemovalTransaction);
            Assert.NotEqual(baselineReportRemovalTransaction, transactionId);
            Assert.Null(progressionReportConsumptionTransaction);
            Assert.Equal(1, repairReplies);
            var marker = Assert.Single(members, member => member.GetProperty("Path").GetString() ==
                _fs.ResolvePath(MortalQteDecisionPublication.StatePath));
            var markerBeforeBytes = marker.GetProperty("Before").GetProperty("Bytes").GetBytesFromBase64();
            var markerAfterBytes = marker.GetProperty("After").GetProperty("Bytes").GetBytesFromBase64();
            Assert.Equal(Encoding.UTF8.GetBytes(Assert.Single(milestones,
                milestone => milestone.Path == MortalQteDecisionPublication.StatePath).Raw), markerBeforeBytes);
            var markerBefore = ParseJsonObjectBytes(markerBeforeBytes);
            var markerAfter = ParseJsonObjectBytes(markerAfterBytes);
            Assert.Equal(2, markerBefore["schemaVersion"]!.GetValue<int>());
            Assert.Equal(original.SessionId, markerBefore["sessionId"]!.GetValue<string>());
            Assert.Equal(original.RequestId, markerBefore["requestId"]!.GetValue<string>());
            Assert.Equal(original.SnapshotToken, markerBefore["snapshotToken"]!.GetValue<string>());
            Assert.Equal(original.TurnNumber, markerBefore["turn"]!.GetValue<int>());
            Assert.Equal("unapplied", markerBefore["progression"]!["phase"]!.GetValue<string>());
            Assert.Equal("applied", markerAfter["progression"]!["phase"]!.GetValue<string>());
            Assert.NotEmpty(markerBefore["fingerprint"]!.GetValue<string>());
            Assert.NotEmpty(markerAfter["fingerprint"]!.GetValue<string>());
            markerBefore["progression"]!["phase"] = "applied";
            markerBefore.Remove("fingerprint");
            markerAfter.Remove("fingerprint");
            Assert.True(JsonNode.DeepEquals(markerBefore, markerAfter));
            var schedule = Assert.Single(members, member => member.GetProperty("Path").GetString() ==
                _fs.ResolvePath(ProgressionScheduleService.SchedulePath));
            Assert.Equal(Convert.FromBase64String(markerBefore["progression"]!["scheduleBefore"]!["bytes"]!.GetValue<string>()),
                schedule.GetProperty("Before").GetProperty("Bytes").GetBytesFromBase64());
            var scheduleAfter = schedule.GetProperty("After").GetProperty("Bytes").GetBytesFromBase64();
            Assert.Equal(Convert.FromBase64String(markerBefore["progression"]!["scheduleAfter"]!["bytes"]!.GetValue<string>()), scheduleAfter);
            Assert.Equal(scheduleAfter, File.ReadAllBytes(_fs.ResolvePath(ProgressionScheduleService.SchedulePath)));
            Assert.Equal(markerAfterBytes, File.ReadAllBytes(_fs.ResolvePath(MortalQteDecisionPublication.StatePath)));
            Assert.Equal(Convert.FromBase64String(markerBefore["progression"]!["reportBefore"]!["bytes"]!.GetValue<string>()),
                reportMember.GetProperty("Before").GetProperty("Bytes").GetBytesFromBase64());
            Assert.False(markerBefore["progression"]!["reportAfter"]!["exists"]!.GetValue<bool>());
            Assert.Equal(0, reportDeletions++);
            progressionReportConsumptionTransaction = transactionId;
        }
    }


    private void WriteJointInMemoryOwnerSnapshot(GameEngine engine)
    {
        // Diagnostic reflection over EXISTING retained state only. Never serialize
        // the checkpoint, plan, frame, receipt or capability, and never call an owner.
        var checkpointPresent = GetPrivateFieldValue<object?>(engine, "_mortalJointRepairCheckpoint") is not null;
        var correctionReady = GetPrivateFieldValue<bool>(engine, "_mortalJointRepairCorrectionReady");
        var refresh = GetPrivateFieldValue<object?>(engine, "_mortalJointRepairCanonicalRefresh");
        var dispositionProperty = refresh?.GetType().GetProperty("Disposition");
        var issuesProperty = refresh?.GetType().GetProperty("Issues");
        var disposition = dispositionProperty?.GetValue(refresh)?.ToString();
        var issues = issuesProperty?.GetValue(refresh) as IReadOnlyList<ValidationIssue>;
        const int maximumIssues = 32;
        object BoundedField(string? value, int limit) => new
        {
            Value = value is null || value.Length <= limit ? value : value[..limit],
            OriginalLength = value?.Length, Truncated = value is not null && value.Length > limit,
            FullUtf8Sha256 = value is null ? null : Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value)))
        };
        // Only public diagnostic strings enter this complete projection/digest;
        // the underlying result and original full owner-outcome emission stay intact.
        var fullIssueProjection = issues?.Select(issue => new { issue.Code, issue.FilePath, issue.Message }).ToArray();
        WritePhysicalInjuryDiagnostic("fr063_joint_in_memory_owner_snapshot_before_filesystem", new
        {
            CheckpointPresent = checkpointPresent, CorrectionReady = correctionReady,
            CanonicalRefreshPresent = refresh is not null,
            CanonicalDispositionPropertyPresent = dispositionProperty is not null,
            CanonicalDisposition = disposition,
            IssuePropertyPresent = issuesProperty is not null, IssueCollectionPresent = issues is not null,
            IssueCount = issues?.Count, MaximumReportedIssues = maximumIssues,
            ReportedIssueCount = issues is null ? (int?)null : Math.Min(issues.Count, maximumIssues),
            IssueRowsTruncated = issues is not null && issues.Count > maximumIssues,
            CompletePublicIssueProjectionSha256 = fullIssueProjection is null ? null : Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(fullIssueProjection))),
            Issues = issues?.Take(maximumIssues).Select((issue, index) => new
            {
                Index = index, Code = BoundedField(issue.Code, 256),
                FilePath = BoundedField(issue.FilePath, 512), Message = BoundedField(issue.Message, 1024)
            }).ToArray(),
            Scope = "Existing in-memory diagnostics only; no authority issued or filesystem admission performed"
        });
    }

    private static (Dictionary<string, byte[]?> Current, string Reminder)
        BuildJointNaturalStage43CurrentExpectation(IReadOnlyDictionary<string, byte[]?> originalImages)
    {
        Assert.Equal(GameEngine.MortalJointInactiveTailPaths.Count, originalImages.Count);
        var current = originalImages.ToDictionary(row => row.Key, row => row.Value?.ToArray(), StringComparer.Ordinal);
        // Ordinary Stage43 clears transient public outputs AFTER signing original.
        current[QteSceneService.QteOfferPath] = null;
        var before = Assert.IsType<byte[]>(originalImages[QteSceneService.QteRuntimePath]);
        // The same typed load/serializer/BOM encoder used by the actual QTE owner.
        // Roundtrip first prevents an unrepresented field from silently disappearing.
        var runtime = StrictJsonAuthority.Deserialize<QteSceneService.QteRuntimeState>(
            CanonicalJsonUtf8.DecodeOneOptionalBom(before), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed,
            "genuine prior42 QTE runtime expectation");
        Assert.NotNull(runtime);
        Assert.Equal(before, FileSystemManager.EncodeUtf8WithPreamble(
            JsonSerializer.Serialize(runtime, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)));
        var reminder = Assert.IsType<string>(runtime!.LastResolvedQteSummaryPendingReminder);
        Assert.False(string.IsNullOrWhiteSpace(reminder));
        runtime.LastResolvedQteSummaryPendingReminder = null;
        current[QteSceneService.QteRuntimePath] = FileSystemManager.EncodeUtf8WithPreamble(
            JsonSerializer.Serialize(runtime, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        Assert.False(before.SequenceEqual(current[QteSceneService.QteRuntimePath]!));
        return (current, reminder);
    }

    private async Task AssertJointNaturalStage43OriginalAndRollbackAsync(FileSystemManager.CanonicalWriteLease lease,
        PendingTurnSnapshotReadAuthority projectedOriginal, IReadOnlyDictionary<string, byte[]?> expectedOriginal,
        string priorQteReminder)
    {
        // Coverage authenticates present original bytes and the signed rollback map.
        // Missing tail paths have structural inventory absence below, not signed
        // presence-set authority. No current image becomes an expectation.
        var read = PendingTurnSnapshotReader.ReadCurrent(_fs, lease,
            new PendingTurnSnapshotPathSelection(
                ["game_state/meta/soul_state.json"], GameEngine.MortalJointInactiveTailPaths));
        Assert.True(read.Success, DescribeValidationIssues(read.Issues));
        var original = Assert.IsType<PendingTurnSnapshotReadAuthority>(read.Snapshot);
        Assert.Equal(projectedOriginal.SessionId, original.SessionId);
        Assert.Equal(projectedOriginal.RequestId, original.RequestId);
        Assert.Equal(projectedOriginal.SnapshotToken, original.SnapshotToken);
        var manifest = StrictJsonAuthority.Deserialize<LiveTurnPendingSnapshotManifest>(
            CanonicalJsonUtf8.DecodeOneOptionalBom(Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease,
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath))),
            LiveTurnPreparationService.ManifestHashJsonOptions, "actual validated natural Stage43 manifest");
        Assert.NotNull(manifest);
        Assert.Equal(original.SnapshotToken, manifest!.ManifestPayloadHash);
        Assert.Equal(original.SessionId, manifest.SessionId);
        Assert.Equal(original.RequestId, manifest.RequestId);
        Assert.Equal(original.TurnNumber, manifest.TurnNumber);
        foreach (var image in expectedOriginal)
        {
            if (image.Value is null)
            {
                var covered = original.CoveredLogicalPaths.Contains(image.Key, StringComparer.Ordinal);
                var inFiles = manifest.Files.Keys.Any(path => string.Equals(path, image.Key, StringComparison.OrdinalIgnoreCase));
                var inHashes = manifest.SnapshotFileHashes.Keys.Any(path => string.Equals(path, image.Key, StringComparison.OrdinalIgnoreCase));
                var inBackups = manifest.RollbackBackups.Keys.Any(path => string.Equals(path, image.Key, StringComparison.OrdinalIgnoreCase));
                var inBaseline = manifest.RollbackBaselineFiles.Any(path => string.Equals(path, image.Key, StringComparison.OrdinalIgnoreCase));
                WritePhysicalInjuryDiagnostic("fr063_joint_original_inventory_structural_absence_before_assert", new
                {
                    Path = image.Key, OriginalCovered = covered, InManifestFiles = inFiles,
                    InSnapshotFileHashes = inHashes, InRollbackBackups = inBackups, InRollbackBaselineFiles = inBaseline,
                    Scope = "validated signed-inventory/rollback structural absence; no signed presence authority"
                });
                AssertJointNaturalStage43Image("original-inventory-structural", image.Key, null,
                    covered ? original.ReadRequiredBytes(image.Key) : null);
                Assert.False(covered);
                Assert.False(inFiles);
                Assert.False(inHashes);
                Assert.False(inBackups);
                Assert.False(inBaseline);
                continue;
            }
            AssertJointNaturalStage43Image("original", image.Key, image.Value, original.ReadRequiredBytes(image.Key));
            Assert.Contains(image.Key, manifest.RollbackBaselineFiles);
            Assert.True(manifest.RollbackBackups.TryGetValue(image.Key, out var backupPath), image.Key);
            AssertJointNaturalStage43Image("rollback", image.Key, image.Value,
                await _fs.ReadFileBytesAsync(lease, backupPath!));
        }
        var request = StrictJsonAuthority.Deserialize<TurnRequest>(CanonicalJsonUtf8.DecodeOneOptionalBom(
            Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease, "input/turn_request.json"))),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "actual natural Stage43 request reminder");
        Assert.NotNull(request);
        Assert.Equal(original.SessionId, request!.SessionId);
        Assert.Equal(original.RequestId, request.RequestId);
        Assert.Equal(original.TurnNumber, request.TurnNumber);
        Assert.NotNull(request.SystemReminder);
        Assert.Contains("QTE SUMMARY FROM PREVIOUS LOCAL SCENE: " + priorQteReminder, request.SystemReminder);
        Assert.Equal(1, request.SystemReminder!.Split(priorQteReminder, StringSplitOptions.None).Length - 1);
    }

    private void AssertJointNaturalStage43Image(string scope, string path, byte[]? expected, byte[]? actual)
    {
        WritePhysicalInjuryDiagnostic("fr063_joint_natural_stage43_image_before_assert", new
        {
            Scope = scope, Path = path, ExpectedPresent = expected is not null, ActualPresent = actual is not null,
            ExpectedLength = expected?.LongLength, ActualLength = actual?.LongLength,
            ExpectedSha256 = expected is null ? null : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(expected)),
            ActualSha256 = actual is null ? null : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(actual))
        });
        Assert.Equal(expected, actual);
    }

    private async Task ConfigureJointCompletionWorldBeforeOriginalAsync(FileSystemManager files)
    {
        await ConfigureGenuinePendingTreatmentWorldAsync(files);
        // These are labelled canonical bootstrap prerequisites BEFORE the genuine
        // producer mints original42. No original, request, claim or seal exists yet.
        // The real accepted turn42 progression owner must produce this file.
        // A fixture-authored processed-level acknowledgment is not its evidence.
        Assert.Null(await files.ReadFileBytesAsync("game_state/player/stat_points.json"));
        var worldTime = ParseJsonObjectBytes(Assert.IsType<byte[]>(await files.ReadFileBytesAsync("game_state/world/world_time.json")));
        var current = worldTime["currentTimeInMinutes"]!.GetValue<int>();
        await files.WriteFileAtomicAsync(ProgressionScheduleService.SchedulePath,
            JsonSerializer.Serialize(new ProgressionScheduleState { CurrentRealm = "Mortal World",
                CurrentWorldTimeInMinutes = current, LastWorldSimulationTimeInMinutes = current,
                LastFactionSimulationTimeInMinutes = current, HasAuthoritativeWorldTimeBaseline = true },
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task<Dictionary<string, byte[]?>> AssertJointPriorLifecycleInactiveBaselineAsync(QteStoryRun source)
    {
        Assert.NotNull(source.Engine);
        Assert.Equal(42, GetPrivateField<GameLoop>(source.Engine!, "_gameLoop").TurnNumber);
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        Assert.Null(await _fs.ReadFileBytesAsync(lease, "input/turn_request.json"));
        Assert.Null(await _fs.ReadFileBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
        var images = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in GameEngine.MortalJointInactiveTailPaths)
            images.Add(path, await _fs.ReadFileBytesAsync(lease, path));
        // Read-only production predicate checks all actual level/stat/QTE paths.
        // This is before Stage43; it creates no acknowledgment or authority.
        await GameEngine.ValidateCurrentMortalJointInactiveTailAsync(_fs, lease, 43);
        var experience = ParseJsonObjectBytes(Assert.IsType<byte[]>(images["game_state/player/experience.json"]));
        Assert.Equal(1, experience["level"]!.GetValue<int>());
        var points = ParseJsonObjectBytes(Assert.IsType<byte[]>(images["game_state/player/stat_points.json"]));
        Assert.Equal(1, points["levelUpStatPointsAwardedThroughLevel"]!.GetValue<int>());
        Assert.Equal(0, points["unspentStatPoints"]?.GetValue<int>() ?? 0);
        var computedBytes = Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease, "game_state/player/computed_characteristics.json"));
        Assert.Equal(1, ParseJsonObjectBytes(computedBytes)["playerLevel"]!.GetValue<int>());
        Assert.Null(images[QteSceneService.QteOfferPath]);
        var runtime = ParseJsonObjectBytes(Assert.IsType<byte[]>(images[QteSceneService.QteRuntimePath]));
        Assert.Null(runtime["pendingOffer"]);
        Assert.Null(runtime["activeScene"]);
        var priorQte = ParseJsonObjectBytes(Assert.IsType<byte[]>(images[QteDeferredEffectContinuation.StatePath]));
        Assert.Equal("terminal", priorQte["state"]!.GetValue<string>());
        Assert.Equal(source.Source!.SessionId, priorQte["clientStoryAppend"]!["source"]!["sessionId"]!.GetValue<string>());
        Assert.Equal(source.Source.RequestId, priorQte["clientStoryAppend"]!["source"]!["requestId"]!.GetValue<string>());
        Assert.Equal(42, priorQte["clientStoryAppend"]!["source"]!["turnNumber"]!.GetValue<int>());
        if (images["game_state/player/status_changes.json"] is { } changesBytes &&
            ParseJsonObjectBytes(changesBytes)["statsIncreased"] is { } stats)
            Assert.Empty(Assert.IsType<JsonArray>(stats));
        foreach (var image in images)
            Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(lease, image.Key));
        WritePhysicalInjuryDiagnostic("fr063_joint_actual_prior42_inactive_tail", new
        {
            source.Source.SessionId, source.Source.RequestId, PriorTurn = 42, BeforeJointOriginalTurn = 43,
            ProcessedLevel = 1, UnspentStatPoints = 0, ActualLifecycleImages = images,
            ComputedCharacteristics = computedBytes, FixtureAuthoredAcknowledgment = false
        });
        return images;
    }

    private async Task AssertJointCompletionPhysicalOutcomeAsync(GameEngine engine, PendingTurnSnapshotReadAuthority original,
        MortalWoundTreatmentAttemptRequest request, MortalWoundOccurrence occurrence, IReadOnlyDictionary<string, byte[]?> before,
        string scene, IReadOnlyList<(string Path, string Raw, Dictionary<string, byte[]?> Images)> milestones)
    {
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        Assert.Equal(before["$sessionGeneration"], _fs.ReadLocalGenerationSnapshot(lease).Bytes);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await _fs.ReadFileAsync(lease, ResourceMaterializationContract.DefinitionsPath), false);
        Assert.True(definitions.IsValid, DescribeValidationIssues(definitions.Issues));
        var resources = ResourceStateContract.ParseCanonical(await _fs.ReadFileAsync(lease, ResourceMaterializationContract.StatePath), definitions.Catalog!, false);
        var resourceHistory = ResourceHistoryState.ParseCanonical(await _fs.ReadFileAsync(lease, ResourceMaterializationContract.HistoryPath), definitions.Catalog!, false);
        Assert.True(resources.IsValid, DescribeValidationIssues(resources.Issues));
        Assert.True(resourceHistory.IsValid, DescribeValidationIssues(resourceHistory.Issues));
        Assert.Empty(resourceHistory.History!.ValidateStateAgreement(resources.Ledger!));
        var spend = Assert.Single(resourceHistory.History.Transitions, row => row.Turn == 43 &&
            row.Coordinate.ResourceKey == "energy" && row.Operation == ResourceTransitionOperation.Spend);
        Assert.Equal(ResourceMutationPhase.RegisteredSystemOutcome, spend.Phase);
        Assert.Equal(2m, spend.RequestedAmount); Assert.Equal(2m, spend.AppliedAmount);
        Assert.Equal(2m, spend.BeforeState!.Current); Assert.Equal(0m, spend.AfterState!.Current);
        Assert.Equal(0m, Assert.Single(resources.Ledger!.Entries, row => row.Coordinate.ResourceKey == "energy").Current);
        var damage = Assert.Single(resourceHistory.History.Transitions, row => row.Turn == 43 && row.Coordinate.ResourceKey == "health");
        Assert.Equal(ResourceMutationPhase.EffectTrigger, damage.Phase);
        Assert.Equal(ResourceTransitionOperation.Damage, damage.Operation);
        Assert.Equal("effect_component", damage.SourceEvidence.SourceKind);
        Assert.Equal(3m, damage.AppliedAmount); Assert.Equal(5m, damage.BeforeState!.Current); Assert.Equal(2m, damage.AfterState!.Current);
        Assert.Equal(2m, Assert.Single(resources.Ledger.Entries, row => row.Coordinate.ResourceKey == "health").Current);
        Assert.Equal(2, resourceHistory.History.Transitions.Count(row => row.Turn == 43));
        var prior = WoundHistoryState.Parse(MortalArchiveText(before[WoundHistoryState.HistoryPath]!), WoundHistoryState.HistoryPath);
        var history = WoundHistoryState.Parse(await _fs.ReadFileAsync(lease, WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        Assert.True(prior.IsValid, DescribeValidationIssues(prior.Issues)); Assert.True(history.IsValid, DescribeValidationIssues(history.Issues));
        foreach (var previous in prior.State!.Transitions)
            Assert.Equal(JsonSerializer.Serialize(previous, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
                JsonSerializer.Serialize(Assert.Single(history.State!.Transitions, current => current.Ordinal == previous.Ordinal),
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var union = history.State!.Transitions.Skip(prior.State.Transitions.Count).ToArray();
        Assert.Equal(2, union.Length);
        Assert.Equal("create", union[0].Kind); Assert.Equal(prior.State.NextOrdinal, union[0].Ordinal);
        Assert.Equal("treat", union[1].Kind); Assert.Equal(prior.State.NextOrdinal + 1, union[1].Ordinal);
        Assert.All(union, row => Assert.Equal(43, row.Turn));
        Assert.Equal(request.Coordinates.WoundId, union[1].WoundId); Assert.NotEqual(union[0].WoundId, union[1].WoundId);
        var result = Assert.IsType<MortalWoundTreatmentPersistedResult>(union[1].TreatmentResult);
        Assert.Equal(request.RequestFingerprint, result.Request.RequestFingerprint);
        Assert.True(JsonNode.DeepEquals(WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request),
            WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(result.Request)));
        var wounds = ParseJsonObjectBytes(Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease, WoundCarrierCatalog.PlayerPath)))["activeWounds"]!.AsArray();
        Assert.Equal(2, wounds.Count);
        var treated = Assert.Single(wounds.OfType<JsonObject>(), wound => wound["woundId"]!.GetValue<string>() == request.Coordinates.WoundId);
        Assert.Equal("stabilized", treated["care"]!["state"]!.GetValue<string>());
        var acquired = Assert.Single(wounds.OfType<JsonObject>(), wound => wound["woundId"]!.GetValue<string>() == union[0].WoundId);
        var severity = Assert.IsType<JsonObject>(acquired["severity"]);
        Assert.Equal(new[] { "lastChangeEventRef", "maximumAtCreation", "rank", "value" },
            severity.Select(field => field.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal("II", severity["value"]!.GetValue<string>());
        Assert.Equal(2, severity["rank"]!.GetValue<int>());
        Assert.Equal("II", severity["maximumAtCreation"]!.GetValue<string>());
        Assert.Equal(union[0].EventRef, severity["lastChangeEventRef"]!.GetValue<string>());
        var receipts = MortalWoundOpportunityReceiptState.Parse(await _fs.ReadFileAsync(lease, MortalWoundOpportunityReceiptState.StatePath), MortalWoundOpportunityReceiptState.StatePath);
        Assert.True(receipts.IsValid, DescribeValidationIssues(receipts.Issues));
        var receipt = Assert.Single(receipts.State!.Receipts);
        Assert.Equal(original.SessionId, receipt.SessionId); Assert.Equal(original.RequestId, receipt.RequestId);
        Assert.Equal(original.SnapshotToken, receipt.SnapshotToken); Assert.Equal(43, receipt.Turn); Assert.Equal("materialize", receipt.Decision);
        Assert.Equal(occurrence.OccurrenceId, receipt.OpportunityId);
        var pending = MortalWoundOccurrenceState.Parse(await _fs.ReadFileAsync(lease, MortalWoundOccurrenceState.StatePath), MortalWoundOccurrenceState.StatePath);
        Assert.True(pending.IsValid, DescribeValidationIssues(pending.Issues)); Assert.Empty(pending.State!.Occurrences);
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        Assert.Empty(await validator.ValidateAcceptedTurnCanonicalWoundMaterializationAsync(lease));
        Assert.Empty(await validator.ValidateAcceptedTurnCanonicalEffectMaterializationAsync(lease));
        Assert.Empty(await validator.ValidateAcceptedTurnCanonicalResourceMaterializationAsync(lease));
        var marker = ParseJsonObjectBytes(Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease, MortalQteDecisionPublication.StatePath)));
        Assert.Equal(2, marker["schemaVersion"]!.GetValue<int>()); Assert.Equal("applied", marker["progression"]!["phase"]!.GetValue<string>());
        Assert.Equal(original.SessionId, marker["sessionId"]!.GetValue<string>()); Assert.Equal(original.RequestId, marker["requestId"]!.GetValue<string>());
        Assert.Equal(original.SnapshotToken, marker["snapshotToken"]!.GetValue<string>());
        var phaseRows = milestones.Where(m => m.Path == MortalQteDecisionPublication.StatePath).Select(m => ParseJsonObjectBytes(Encoding.UTF8.GetBytes(m.Raw))).ToArray();
        Assert.Contains(phaseRows, row => row["progression"]?["phase"]?.GetValue<string>() == "unapplied");
        Assert.Single(phaseRows, row => row["progression"]?["phase"]?.GetValue<string>() == "applied");
        var actualStory = Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease, QteStoryPath));
        var originalStory = Assert.IsType<byte[]>(before[QteStoryPath]);
        Assert.True(actualStory.AsSpan().StartsWith(originalStory));
        var suffix = Encoding.UTF8.GetString(actualStory.AsSpan(originalStory.Length)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var row = StrictJsonAuthority.Deserialize<StoryEntry>(Assert.Single(suffix), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "actual joint story suffix");
        Assert.NotNull(row); Assert.Equal(43, row!.Turn); Assert.Equal("Осмотреть ушиб.", row.Player); Assert.Equal(scene, row.Narrative);
        Assert.Equal(43, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
        Assert.True((bool)typeof(GameEngine).GetField("_inGame", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(engine)!);
        Assert.Equal(scene, GetPrivateField<GameResponse>(engine, "_lastResponse").Response);
        foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath, WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                     MortalWoundOpportunityAdapter.DecisionPath, "input/turn_request.json", "ready/turn_complete.json",
                     "game_state/control/validation_repair_request.json", "game_state/control/validation_repair_ready.json",
                     LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath })
            Assert.Null(await _fs.ReadFileBytesAsync(lease, path));
        var manifest = ParseJsonObjectBytes(Assert.IsType<byte[]>(before[LiveTurnPreparationService.PendingTurnSnapshotManifestPath]));
        var ownedSnapshots = Assert.IsType<JsonObject>(manifest["files"]).Select(pair => pair.Value!.GetValue<string>());
        var ownedBackups = Assert.IsType<JsonObject>(manifest["rollbackBackups"]).Select(pair => pair.Value!.GetValue<string>());
        foreach (var path in ownedSnapshots.Concat(ownedBackups).Distinct(StringComparer.Ordinal))
            Assert.Null(await _fs.ReadFileBytesAsync(lease, path));
        Assert.Null(await _fs.ReadFileBytesAsync(lease, ProgressionScheduleService.ReportPath));
        WritePhysicalInjuryDiagnostic("fr063_joint_complete_union", new
        { original.SessionId, original.RequestId, original.SnapshotToken, OriginalTurn = original.TurnNumber,
            RequestFingerprint = request.RequestFingerprint, receipt, Create = union[0], Treat = union[1], spend, damage,
            Marker = marker, ExactStorySuffix = row, FullOwnerCanonicalValidation = true,
            ActualTurn = GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber });
    }

    private async Task WriteJointCompletionMatchingReportAsync(FileSystemManager.CanonicalWriteLease lease,
        PendingTurnSnapshotReadAuthority original)
    {
        var request = StrictJsonAuthority.Deserialize<TurnRequest>(CanonicalJsonUtf8.DecodeOneOptionalBom(
            Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease, "input/turn_request.json"))),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "real nonzero original43 request");
        Assert.NotNull(request); Assert.NotNull(request!.ProgressionControl);
        Assert.Equal(original.SessionId, request.SessionId); Assert.Equal(original.RequestId, request.RequestId);
        Assert.Equal(original.TurnNumber, request.TurnNumber);
        var control = request.ProgressionControl!;
        Assert.Equal(1, control.WorldCyclesAlreadyPendingBeforeTurn);
        Assert.Equal(0, control.FactionCyclesAlreadyPendingBeforeTurn);
        Assert.True(control.MustEvaluateWorldProgression);
        var nextLastWorld = checked(control.LastWorldSimulationTimeInMinutes + control.WorldCycleMinutes);
        Assert.Equal(control.CurrentWorldTimeInMinutes, nextLastWorld);
        // Offline GM report: actual original correlation and specified arithmetic,
        // not a fabricated progression control, marker or accepted capability.
        await _fs.WriteFileAtomicAsync(lease, ProgressionScheduleService.ReportPath, JsonSerializer.Serialize(new
        {
            progressionProcessingReport = new { sessionId = original.SessionId, requestId = original.RequestId,
                turnNumber = original.TurnNumber, worldCyclesProcessed = 1, factionCyclesProcessed = 0,
                newLastWorldSimulationTimeInMinutes = nextLastWorld }
        }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var ready = ParseJsonObjectBytes(Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease, "ready/turn_complete.json")));
        var modified = Assert.IsType<JsonArray>(ready["filesModified"]);
        Assert.DoesNotContain(modified, path => path?.GetValue<string>() == ProgressionScheduleService.ReportPath);
        modified.Add(ProgressionScheduleService.ReportPath);
        await _fs.WriteFileAtomicAsync(lease, "ready/turn_complete.json", ready.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task AssertJointProgressionReportOutcomeAsync(bool nonzeroProgression, PendingTurnSnapshotReadAuthority original,
        byte[] signedOriginalSchedule, IReadOnlyDictionary<string, byte[]?> before, IReadOnlyList<(string Path, string Raw, Dictionary<string, byte[]?> Images)> milestones)
    {
        var request = StrictJsonAuthority.Deserialize<TurnRequest>(CanonicalJsonUtf8.DecodeOneOptionalBom(before["input/turn_request.json"]!),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "actual progression source request");
        Assert.NotNull(request); Assert.NotNull(request!.ProgressionControl);
        var control = request.ProgressionControl!;
        Assert.Equal(nonzeroProgression ? 1 : 0, control.WorldCyclesAlreadyPendingBeforeTurn);
        var phases = milestones.Where(m => m.Path == MortalQteDecisionPublication.StatePath)
            .Select(m => (Marker: ParseJsonObjectBytes(Encoding.UTF8.GetBytes(m.Raw)), m.Images)).ToArray();
        var unapplied = Assert.Single(phases, row => row.Marker["progression"]!["phase"]!.GetValue<string>() == "unapplied");
        var applied = Assert.Single(phases, row => row.Marker["progression"]!["phase"]!.GetValue<string>() == "applied");
        var initial = Assert.IsType<JsonObject>(unapplied.Marker["progression"]);
        var final = Assert.IsType<JsonObject>(applied.Marker["progression"]);
        Assert.True(JsonNode.DeepEquals(initial["sourceGuards"], final["sourceGuards"]));
        Assert.True(JsonNode.DeepEquals(initial["scheduleBefore"], final["scheduleBefore"]));
        Assert.True(JsonNode.DeepEquals(initial["reportBefore"], final["reportBefore"]));
        Assert.True(JsonNode.DeepEquals(initial["scheduleAfter"], final["scheduleAfter"]));
        Assert.True(JsonNode.DeepEquals(initial["reportAfter"], final["reportAfter"]));
        Assert.Equal(initial["mutationFingerprint"]!.GetValue<string>(), final["mutationFingerprint"]!.GetValue<string>());
        var preRepairCurrentSchedule = Assert.IsType<byte[]>(before[ProgressionScheduleService.SchedulePath]);
        var scheduleBefore = signedOriginalSchedule;
        Assert.Equal(scheduleBefore, unapplied.Images[ProgressionScheduleService.SchedulePath]);
        Assert.Equal(scheduleBefore, Convert.FromBase64String(final["scheduleBefore"]!["bytes"]!.GetValue<string>()));
        var expectedAfter = Convert.FromBase64String(final["scheduleAfter"]!["bytes"]!.GetValue<string>());
        Assert.Equal(expectedAfter, applied.Images[ProgressionScheduleService.SchedulePath]);
        var expectedSchedule = ParseJsonObjectBytes(scheduleBefore);
        expectedSchedule["currentWorldTimeInMinutes"] = control.CurrentWorldTimeInMinutes;
        expectedSchedule["lastWorldSimulationTimeInMinutes"] = checked(control.LastWorldSimulationTimeInMinutes +
            control.WorldCyclesAlreadyPendingBeforeTurn * control.WorldCycleMinutes);
        expectedSchedule["lastFactionSimulationTimeInMinutes"] = control.LastFactionSimulationTimeInMinutes;
        expectedSchedule["pendingWorldCycles"] = 0; expectedSchedule["pendingFactionCycles"] = 0;
        expectedSchedule["lastUpdatedUtc"] = final["preparedAtUtc"]!.GetValue<string>();
        Assert.True(JsonNode.DeepEquals(expectedSchedule, ParseJsonObjectBytes(expectedAfter)),
            "Actual schedule afterimage disagrees with independent original-control arithmetic/preserved fields.");
        var reportBefore = before.TryGetValue(ProgressionScheduleService.ReportPath, out var report) ? report : null;
        if (nonzeroProgression)
        {
            Assert.NotNull(reportBefore);
            Assert.Equal(reportBefore, unapplied.Images[ProgressionScheduleService.ReportPath]);
            Assert.Equal(reportBefore, Convert.FromBase64String(final["reportBefore"]!["bytes"]!.GetValue<string>()));
            var body = ParseJsonObjectBytes(reportBefore!)["progressionProcessingReport"]!;
            Assert.Equal(original.SessionId, body["sessionId"]!.GetValue<string>());
            Assert.Equal(original.RequestId, body["requestId"]!.GetValue<string>());
            Assert.Equal(original.TurnNumber, body["turnNumber"]!.GetValue<int>());
            Assert.Equal(1, body["worldCyclesProcessed"]!.GetValue<int>());
        }
        else Assert.Null(reportBefore);
        Assert.False(final["reportAfter"]!["exists"]!.GetValue<bool>());
        Assert.Null(applied.Images[ProgressionScheduleService.ReportPath]);
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        Assert.Equal(expectedAfter, await _fs.ReadFileBytesAsync(lease, ProgressionScheduleService.SchedulePath));
        Assert.Null(await _fs.ReadFileBytesAsync(lease, ProgressionScheduleService.ReportPath));
        WritePhysicalInjuryDiagnostic("fr063_joint_progression_phase_report_arithmetic", new
        { nonzeroProgression, original.SessionId, original.RequestId, OriginalTurn = original.TurnNumber,
            ActualOriginalControl = control, Unapplied = initial, Applied = final,
            SignedOriginalScheduleBefore = Convert.ToBase64String(scheduleBefore),
            PreRepairCurrentSchedule = Convert.ToBase64String(preRepairCurrentSchedule), ReportBefore = reportBefore is null ? null : Convert.ToBase64String(reportBefore),
            ExactScheduleAfter = Convert.ToBase64String(expectedAfter), ReportAfterAbsent = true });
    }
}
