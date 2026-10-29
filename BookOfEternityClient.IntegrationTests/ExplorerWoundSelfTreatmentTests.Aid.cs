using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Fact]
    public Task ConsoleAidUsesAnActualAcceptedGrantThenTheSameExplorerAndEngine() => RunAidJourneyAsync(browser: false);

    [Fact]
    public Task ConsoleAidSourceChangeBeforeDrawRestoresThePreparedControlAndOriginalAbsence() =>
        RunAidJourneyAsync(browser: false, refuseBeforeDraw: true);

    [Fact]
    public Task BrowserAidUsesAnActualAcceptedGrantThenTheOriginalQueuedEngineAction() => RunAidJourneyAsync(browser: true);

    [Fact]
    public Task BrowserAidStagingFaultRetainsItsPreparingQueueAndDrawWithoutASecondStage() =>
        RunAidJourneyAsync(browser: true, refuseAfterDraw: true);

    [Fact]
    public Task ConsoleGuardianHelpRequiresActualPlayerConfirmationThenHealsThePlayer() => RunAidJourneyAsync(browser: false, receiveAid: true);

    [Fact]
    public Task BrowserGuardianHelpRequiresActualPlayerConfirmationThenHealsThePlayer() => RunAidJourneyAsync(browser: true, receiveAid: true);

    [Fact]
    public Task ConsoleResidentAidUsesActualConsentCleanupAndConfirmedEngineTreatment() => RunAidJourneyAsync(browser: false, resident: true);

    [Fact]
    public Task BrowserResidentAidUsesActualConsentCleanupAndOriginalQueuedEngineTreatment() => RunAidJourneyAsync(browser: true, resident: true);

    [Fact]
    public Task ResidentAidCommonPublicationFailureRestoresAcceptedGrantMemoryAndWholeTurnState() =>
        RunAidJourneyAsync(browser: false, resident: true, failPublication: true);

    private sealed class AidSnapshotFault : InvalidOperationException { }

    private async Task RunAidJourneyAsync(bool browser, bool refuseAfterDraw = false, bool refuseBeforeDraw = false, bool receiveAid = false, bool resident = false, bool failPublication = false)
    {
        var participantId = resident ? AfterlifeSpiritualNaturalRecoveryTests.AidResidentId : AfterlifeSpiritualNaturalRecoveryTests.AidGuardianId;
        var family = resident ? 5 : receiveAid ? 4 : 3;
        FileSystemManager? ownedFiles = null;
        var injectBeforeDraw = false;
        var foreignMutationReached = 0;
        var publicationArmed = false; var publicationCuts = 0; var compensationObserved = false;
        Dictionary<string, byte[]?>? compensationExpected = null; Dictionary<string, byte[]?>? compensationActual = null;
        byte[]? memoryAtCut = null;
        var publicationFailure = new InvalidOperationException("Resident aid precommit publication failure.");
        await using var fixture = await HealingFixture.CreateAsync(rank: 3, tier: receiveAid ? 0 : 3, die: 14, stageRequest: false,
            beforeResourceBootstrap: resident ? SeedEngineResidentAidJourneyAsync : receiveAid ? SeedEngineReceivingAidJourneyAsync : SeedEngineAidJourneyAsync,
            hooks: !refuseBeforeDraw && !failPublication ? null : new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, _) =>
                {
                    if (!publicationArmed || phase != TrustedLocalPublicationPhase.MemberPublished ||
                        File.Exists(ownedFiles!.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath))) return;
                    publicationArmed = false; publicationCuts++;
                    memoryAtCut = File.ReadAllBytes(ownedFiles.ResolvePath(GuardianAbodeResidentState.StatePath));
                    throw publicationFailure;
                },
                BeforeCanonicalReadOpenAsync = path =>
                {
                    if (publicationCuts == 1 && !compensationObserved && path == CanonicalStateNormalizer.NormalizerRollbackTrackedFiles[0])
                    {
                        compensationObserved = true;
                        compensationActual = compensationExpected!.Keys.ToDictionary(p => p,
                            p => File.Exists(ownedFiles!.ResolvePath(p)) ? File.ReadAllBytes(ownedFiles.ResolvePath(p)) : null,
                            StringComparer.Ordinal);
                    }
                    if (!injectBeforeDraw || path != ProgressionScheduleService.SchedulePath) return Task.CompletedTask;
                    var schedule = JsonNode.Parse(File.ReadAllText(ownedFiles!.ResolvePath(path)))!;
                    if (schedule["pendingChaosSeaCycles"]!.GetValue<int>() != 1) return Task.CompletedTask;
                    injectBeforeDraw = false; foreignMutationReached++;
                    // Actual BuildControl has written pending1 AFTER the committed
                    // grant read. A foreign write before its fresh pre-draw close
                    // must be refused and restored by the real console backup.
                    var localityPath = AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0];
                    var locality = JsonNode.Parse(File.ReadAllText(ownedFiles.ResolvePath(localityPath)))!;
                    locality["chaosSeaNavigation"]!["currentAbodeId"] = "foreign_abode_before_draw";
                    File.WriteAllText(ownedFiles.ResolvePath(localityPath), locality.ToJsonString());
                    return Task.CompletedTask;
                }
            });
        var files = fixture.Files;
        ownedFiles = files;
        var console = new TestExplorerConsole();
        var diagnostics = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger();
        var grantWritten = false;
        var aidWritten = false;
        JsonObject? aidRequest = null;
        TurnRequest? aidTurn = null;
        WoundMaterializationEnvelope? grantWound = null;
        PendingPlayerActionService.Binding? browserBinding = null;
        var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files,
            inputSource: new NoPlayerInput(diagnostics), logger: diagnostics, explorerConsole: console,
            finalizationHooks: new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = async checkpoint =>
                {
                    if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted) return;
                    await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                    var turn = JsonSerializer.Deserialize<TurnRequest>((await files.ReadFileAsync(lease, "input/turn_request.json"))!,
                        SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                    if (turn.TurnNumber == 43 && !grantWritten)
                    {
                        Assert.Equal("natural", turn.SpiritualHealing!["mode"]!.GetValue<string>());
                        Assert.Equal(0, Assert.Single(turn.SpiritualHealing["cohort"]!.AsArray())!["healingTier"]!.GetValue<int>());
                        Assert.Null(await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
                        var profiles = JsonNode.Parse((await files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
                        profiles["profiles"]!.AsArray().OfType<JsonObject>()
                            .Single(p => SpiritualTreatmentParticipation.ProfileId(p) == participantId)
                            [SpiritualTreatmentParticipation.ChangeProperty] = resident ? AfterlifeSpiritualNaturalRecoveryTests.ResidentGrantProposal()
                                : receiveAid ? AfterlifeSpiritualNaturalRecoveryTests.GuardianOfferProposal()
                                : AfterlifeSpiritualNaturalRecoveryTests.AidGrantProposal();
                        await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
                        await WriteNativeCompletionAsync(files, lease, turn, narrative:
                            resident ? "Житель целостности осознанно разрешает душе вылечить его духовную рану в следующий безопасный цикл."
                                : receiveAid ? "Хранитель целостности добровольно предлагает бесплатную помощь сознательной душе; принять помощь может только игрок."
                                : "Хранитель целостности понимает предложение души и разрешает ей вылечить его духовную рану в следующий безопасный цикл.");
                        if (resident) await WriteResidentAidReasoningAsync(files, lease, turn, grant: true);
                        else await WriteAidGuardianReasoningAsync(files, lease, turn, grant: true, receiveAid);
                        grantWritten = true;
                    }
                    else if (turn.TurnNumber == 44 && !aidWritten)
                    {
                        Assert.NotNull(grantWound);
                        Assert.Equal("aid", turn.SpiritualHealing!["mode"]!.GetValue<string>());
                        Assert.Equal(3, turn.SpiritualHealing["healingTier"]!.GetValue<int>());
                        Assert.Equal(0, turn.SpiritualHealing["targetNaturalHealingTier"]!.GetValue<int>());
                        aidRequest = JsonNode.Parse((await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();
                        Assert.Equal("aid", aidRequest["mode"]!.GetValue<string>());
                        Assert.Equal(family, aidRequest["schemaVersion"]!.GetValue<int>());
                        Assert.Equal(receiveAid ? "guardian" : "player_soul", aidRequest["healer"]!["ownerKind"]!.GetValue<string>());
                        Assert.Equal(resident ? "resident" : receiveAid ? "player_soul" : "guardian", aidRequest["target"]!["ownerKind"]!.GetValue<string>());
                        if (resident)
                        {
                            Assert.Equal("player_to_resident", aidRequest["direction"]!.GetValue<string>());
                            Assert.Equal(participantId, aidRequest["target"]!["ownerId"]!.GetValue<string>());
                        }
                        if (receiveAid)
                        {
                            Assert.Equal("guardian_to_player", aidRequest["direction"]!.GetValue<string>());
                            Assert.Equal("player_target_confirmed", aidRequest["playerConfirmation"]!.GetValue<string>());
                        }
                        Assert.Equal(3, aidRequest["activeSource"]!["healingTier"]!.GetValue<int>());
                        Assert.Equal("normal", aidRequest["activeSource"]!["rollMode"]!.GetValue<string>());
                        Assert.Equal(0, aidRequest["activeSource"]!["complicationModifier"]!.GetValue<long>());
                        if (browser)
                        {
                            var queued = Assert.IsType<PendingPlayerActionService.State>(await PendingPlayerActionService.ReadAsync(files, lease));
                            Assert.Equal(browserBinding!.ActionId, turn.RequestId);
                            Assert.Equal(browserBinding.SpiritualSelfTreatment, queued.Binding.SpiritualSelfTreatment);
                            var staged = PendingPlayerActionService.ReadStaged(queued);
                            Assert.Equal((await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!,
                                Convert.FromBase64String(staged.SpiritualWoundRequestBytesBase64!));
                            GameEngine.ValidateDetachedBrowserBinding(staged); // Same original request3, not self downgrade.
                        }
                        else Assert.Null(await PendingPlayerActionService.ReadAsync(files, lease));
                        aidTurn = turn;
                        await WriteNativeCompletionAsync(files, lease, turn,
                            AfterlifeSpiritualNaturalRecoveryTests.AuthorNaturalGraphs(grantWound!, turn.SpiritualHealing["destinationGraphs"]!.AsArray()),
                            resident ? "Душа помогает Жителю целостности восстановить духовную рану; жизнь Обители продолжается."
                                : receiveAid ? "Хранитель целостности помогает душе восстановить выбранную духовную рану после её подтверждения; безопасная жизнь Обители продолжается."
                                : "Душа помогает Хранителю целостности восстановить духовную рану; безопасная жизнь Обители продолжается.");
                        if (resident) await WriteResidentAidReasoningAsync(files, lease, turn, grant: false);
                        else await WriteAidGuardianReasoningAsync(files, lease, turn, grant: false, receiveAid);
                        if (failPublication)
                        {
                            compensationExpected = fixture.CaptureMutationRoots(AfterlifeWoundTreatmentAuthority.AidLocalityPaths
                                .Concat(new[] { EffectAcceptedTurnInputComposer.WorldTimePath }));
                            publicationArmed = true;
                        }
                        aidWritten = true;
                    }
                }
            });
        try
        {
            runtime.GameLoop.SetSession(browser ? "session_browser_guardian_aid" : "session_console_guardian_aid", 42);
            await runtime.StateManager.RefreshGameStateAsync();
            await runtime.Engine.ProcessPlayerTurn(resident ? "Предложить Жителю целостности вылечить его духовную рану и спросить разрешение."
                : receiveAid ? "Спросить текущего Хранителя, готов ли он бесплатно помочь мне с духовной раной; я ещё не принимаю лечение."
                : "Предложить Хранителю целостности вылечить его духовную рану и спросить разрешение.");
            Assert.True(grantWritten);
            Assert.Equal(43, runtime.GameLoop.TurnNumber); // Actual acceptance precedes every aid-specific oracle.
            grantWound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
            Assert.Equal(3, grantWound.Severity.Rank);
            Assert.Equal(1, grantWound.Recovery.CurrentStepProgress);
            Assert.Null(await files.ReadFileBytesAsync(LiveTurnPreparationService.TurnRequestPath));
            Assert.Null(await files.ReadFileBytesAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
            foreach (var path in new[] { PendingTurnSnapshotAuthority.AuthorityPath, "ready/turn_complete.json", "ready/turn_error.json",
                         ProgressionScheduleService.ReportPath, AcceptedMechanicsPlan.WoundCommandPath, PendingPlayerActionService.PendingPath })
                Assert.Null(await files.ReadFileBytesAsync(path));
            var grant = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single((await fixture.ReadHistoryAsync()).Transitions,
                t => t.TransitionResult is SpiritualHealingPersistedResult).TransitionResult);
            Assert.Equal(family, grant.SchemaVersion);
            Assert.Single(grant.ParticipationOutcomes!);
            Assert.True(files.FileExists("game_state/meta/achievements.json"));
            Assert.True(files.FileExists("lore/codex_entries.json"));
            Assert.Empty(JsonNode.Parse((await files.ReadFileAsync("game_state/meta/achievements.json"))!)!["unlockedAchievements"]!.AsArray());
            Assert.Empty(JsonNode.Parse((await files.ReadFileAsync("lore/codex_entries.json"))!)!["entries"]!.AsArray());
            var before = fixture.CaptureMutationRoots(AfterlifeWoundTreatmentAuthority.AidLocalityPaths
                .Concat(new[] { EffectAcceptedTurnInputComposer.WorldTimePath, PendingTurnStateService.PendingDiceStatePath }));
            if (browser)
            {
                var service = CreateCommands(files);
                var cancelled = await service.ExecuteAsync(new("/лечить", Owner));
                Assert.Equal(CommandExecutionState.RequiresInput, cancelled.State);
                await service.CancelPromptSessionAsync(new(cancelled.InteractiveSession!.SessionId, Owner));
                fixture.AssertMutationRootsUnchanged(before);
                Assert.Null(await files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
                var opened = await service.ExecuteAsync(new("/лечить", Owner));
                var prompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(opened.Prompts, p => p.Id == "wound_selection"));
                var option = Assert.Single(prompt.Options, p => !p.Disabled);
                Assert.Contains(resident ? "Житель" : "Хранител", option.Label);
                Assert.DoesNotContain(grantWound.WoundId, JsonSerializer.Serialize(opened));
                Assert.DoesNotContain(participantId, JsonSerializer.Serialize(opened));
                var submitted = await service.SubmitPromptSessionAsync(new(opened.InteractiveSession!.SessionId,
                    new() { ["wound_selection"] = JsonValue.Create(option.Value), ["confirm_wound_treatment"] = JsonValue.Create(true) }, Owner));
                Assert.DoesNotContain(submitted.Notifications, n => n.Severity == UiNotificationSeverity.Error);
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                    browserBinding = Assert.IsType<PendingPlayerActionService.State>(await PendingPlayerActionService.ReadAsync(files, lease)).Binding;
                Assert.NotNull(browserBinding.SpiritualSelfTreatment!.Aid);
                if (receiveAid) Assert.True(browserBinding.SpiritualSelfTreatment.Aid!.PlayerConfirmed);
                fixture.AssertMutationRootsUnchanged(before);
                if (refuseAfterDraw)
                {
                    var fault = new AidSnapshotFault();
                    var reached = 0;
                    byte[]? pendingBytes = null;
                    Dictionary<string, byte[]?>? captured = null;
                    runtime.Engine.ConfigureSnapshotPublicationHooksForTesting(new GameEngineSnapshotPublicationHooks
                    {
                        AfterSnapshotFileCapturedAsync = path =>
                        {
                            if (path != AcceptedMechanicsPlan.WoundCommandPath) return Task.CompletedTask;
                            reached++;
                            pendingBytes = File.ReadAllBytes(files.ResolvePath(path));
                            captured = fixture.CaptureMutationRoots(AfterlifeWoundTreatmentAuthority.SourcePaths
                                .Concat(AfterlifeWoundTreatmentAuthority.AidLocalityPaths));
                            throw fault;
                        }
                    });
                    try
                    {
                        Assert.Same(fault, await Record.ExceptionAsync(() => runtime.Engine.ProcessPlayerTurn(browserBinding.Action, browserAction: browserBinding)));
                    }
                    finally { runtime.Engine.ConfigureSnapshotPublicationHooksForTesting(null); }
                    Assert.Equal(1, reached);
                    Assert.NotNull(captured);
                    fixture.AssertMutationRootsUnchanged(captured!);
                    Assert.Equal(pendingBytes, File.ReadAllBytes(files.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath)));
                    Assert.False(aidWritten);
                    Assert.Equal(43, runtime.GameLoop.TurnNumber);
                    var leaseRefusal = await Assert.ThrowsAnyAsync<InvalidOperationException>(async () =>
                    {
                        await using var unexpected = await files.AcquireCanonicalWriteLeaseAsync();
                    });
                    Assert.True(files.IsOriginalBrowserPhaseRefusal(leaseRefusal));
                    // Protected failure permits observation of owned fixture DATA,
                    // not a stronger owner or fresh canonical write admission.
                    var preparingBytes = File.ReadAllBytes(files.ResolvePath(PendingPlayerActionService.PendingPath));
                    var pending = PendingPlayerActionService.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(preparingBytes), browserBinding.Generation);
                    Assert.Equal("preparing", pending.Phase); // Legitimate queued→preparing, not identical queue JSON.
                    Assert.Equal(browserBinding.ActionId, pending.Binding.ActionId);
                    Assert.Equal(browserBinding.Generation, pending.Binding.Generation);
                    Assert.Equal(browserBinding.Action, pending.Binding.Action);
                    Assert.Equal(browserBinding.Source, pending.Binding.Source);
                    Assert.Equal(browserBinding.SpiritualSelfTreatment, pending.Binding.SpiritualSelfTreatment);
                    var queuedBody = JsonNode.Parse(browserBinding.Json)!.AsObject();
                    var preparingBody = JsonNode.Parse(pending.Json)!.AsObject();
                    foreach (var field in new[] { "status", "phaseProof", "phaseProofHash" })
                    { queuedBody.Remove(field); preparingBody.Remove(field); }
                    Assert.True(JsonNode.DeepEquals(queuedBody, preparingBody)); // Includes submittedAtUtc and all immutable input.
                    Assert.False(JsonNode.Parse(pending.ProofJson!)!.AsObject().ContainsKey("spiritualWoundRequestBytesBase64"));
                    // Actual second queue admission refuses BEFORE another Stage;
                    // no reentrant console call or synthetic older Main owner.
                    var secondRefusal = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => runtime.Engine.ProcessPlayerTurn(browserBinding.Action, browserAction: browserBinding));
                    Assert.True(files.IsOriginalBrowserPhaseRefusal(secondRefusal));
                    Assert.Equal(1, reached);
                    fixture.AssertMutationRootsUnchanged(captured!);
                    Assert.Equal(pendingBytes, File.ReadAllBytes(files.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath)));
                    Assert.Equal(preparingBytes, File.ReadAllBytes(files.ResolvePath(PendingPlayerActionService.PendingPath)));
                    var observedHistory = WoundHistoryState.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(
                        File.ReadAllBytes(files.ResolvePath(WoundHistoryState.HistoryPath))), WoundHistoryState.HistoryPath);
                    Assert.True(observedHistory.IsValid);
                    Assert.DoesNotContain(observedHistory.State!.Transitions, t => t.AttemptId is not null);
                    fixture.AssertResourcesUnchanged();
                    output.WriteLine("Browser aid refused after its one staged draw: preparing original/selection/body and prepared control retained; second queue admission refused, no healing/cycle accepted.");
                    return;
                }
                await runtime.Engine.ProcessPlayerTurn(browserBinding.Action, browserAction: browserBinding);
            }
            else
            {
                console.QueueAnySelection("Отмена");
                Assert.Equal(string.Empty, await runtime.Explorer.TryProcessCommand("/лечить"));
                Assert.Empty(console.ConfirmPrompts);
                fixture.AssertMutationRootsUnchanged(before);
                var offered = (await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true)).WoundCommandState!;
                var choice = Assert.Single(offered.Choices, c => c.Available);
                Assert.NotNull(choice.Selection.Aid);
                if (receiveAid) Assert.False(choice.Selection.Aid!.PlayerConfirmed);
                console.QueueAnySelection("1. " + choice.Name + " — " + choice.Severity);
                console.QueueAnyConfirmResponse(true);
                var action = await runtime.Explorer.TryProcessCommand("/лечить");
                Assert.Equal(resident ? WoundApplicationService.ResidentAidActionText : receiveAid ? WoundApplicationService.ReceiveAidActionText : WoundApplicationService.AidActionText, action);
                Assert.Single(console.ConfirmPrompts);
                fixture.AssertMutationRootsUnchanged(before);
                Assert.Null(await files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
                if (refuseBeforeDraw)
                {
                    var rollbackBaseline = fixture.CaptureMutationRoots(AfterlifeWoundTreatmentAuthority.AidLocalityPaths);
                    injectBeforeDraw = true;
                    try
                    {
                        var refused = await Assert.ThrowsAsync<InvalidDataException>(() => runtime.Engine.ProcessPlayerTurn(action!));
                        Assert.Contains("spiritual_aid_admission_invalid", refused.Message);
                    }
                    finally { injectBeforeDraw = false; }
                    Assert.Equal(1, foreignMutationReached);
                    Assert.False(aidWritten);
                    Assert.Equal(43, runtime.GameLoop.TurnNumber);
                    fixture.AssertMutationRootsUnchanged(rollbackBaseline); // Exact schedule/request absence/locality restoration.
                    Assert.Null(await files.ReadFileBytesAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
                    Assert.DoesNotContain((await fixture.ReadHistoryAsync()).Transitions, t => t.AttemptId is not null);
                    fixture.AssertResourcesUnchanged();
                    output.WriteLine("Console aid refused a foreign source after prepared-control write and before draw; actual backup restored exact schedule/source/request absence, no aid attempt or cycle accepted.");
                    return; // This hook-owned fixture makes no normal-cache qualification claim.
                }
                await runtime.Engine.ProcessPlayerTurn(action!);
                await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                Assert.Null(runtime.Explorer.ConsumePendingSpiritualSelfTreatment(action!, files.GetOrCreateSessionGeneration(lease)));
            }
            Assert.True(aidWritten);
            if (failPublication)
            {
                Assert.Equal(1, publicationCuts); Assert.True(compensationObserved);
                Assert.NotNull(compensationExpected); Assert.NotNull(compensationActual); Assert.NotNull(memoryAtCut);
                var cutMemory = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(memoryAtCut!))![GuardianAbodeResidentState.ThoughtJournalProperty]!.AsArray();
                Assert.Equal(2, cutMemory.Count);
                Assert.Contains(cutMemory.OfType<JsonObject>(), e => e["turn"]!.GetValue<int>() == 44);
                foreach (var pair in compensationExpected!) Assert.Equal(pair.Value, compensationActual![pair.Key]);
                var retained = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(compensationActual![AcceptedMechanicsPlan.WoundCommandPath]!));
                Assert.True(JsonNode.DeepEquals(aidRequest, retained)); // Full raw bytes also equal exact pre-cut capture above.
                Assert.Equal(43, runtime.GameLoop.TurnNumber);
                fixture.AssertMutationRootsUnchanged(before);
                Assert.DoesNotContain((await fixture.ReadHistoryAsync()).Transitions, t => t.AttemptId is not null);
                Assert.Null(await files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
                Assert.Null(await files.ReadFileBytesAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
                fixture.AssertResourcesUnchanged();
                output.WriteLine("Resident aid cut after real memory normalization/common request consumption: C4 restored exact staged request/die, pending schedule/report and all canonical roots; outer rollback restored grant43 including its sole memory and removed unaccepted aid44.");
                return;
            }
            Assert.Equal(44, runtime.GameLoop.TurnNumber);
            var history = await fixture.ReadHistoryAsync();
            var aid = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(history.Transitions, t => t.AttemptId is not null).TransitionResult);
            Assert.Equal("aid", aid.Mode);
            Assert.True(JsonNode.DeepEquals(aidRequest, aid.ToCanonicalJson()["request"]));
            Assert.Equal(3, aid.ToCanonicalJson()["source"]!["healingTier"]!.GetValue<int>());
            Assert.Equal(0, Assert.Single(aid.ToCanonicalJson()["source"]!["ownerTiers"]!.AsArray())!["healingTier"]!.GetValue<int>());
            var draw = aidRequest!["attempt"]!["roll"]!["values"]!.AsArray().Select(v => v!.GetValue<int>()).Max();
            var calculation = SpiritualHealingOutcomeMath.Calculate(new(3, 3, draw, 0, 0))!;
            var expected = SpiritualWoundRecoveryPlanner.Build(grantWound, 0, aidRequest["attempt"]!["attemptId"]!.GetValue<string>(),
                aidRequest["attempt"]!["cycleKey"]!.GetValue<string>(), calculation);
            Assert.Equal(expected.Select(s => (s.Kind, s.SeverityRank, s.Progress)), aid.Stages.Select(s => (s.Kind, s.After.Severity.Rank, s.After.Recovery.CurrentStepProgress)));
            var after = aid.Stages[^1].After;
            if (after.Lifecycle == "healed") Assert.Empty((await fixture.ReadWoundsAsync()).Occurrences);
            else Assert.Equal(WoundMaterializationContract.SerializeCanonical(after), WoundMaterializationContract.SerializeCanonical(Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound));
            Assert.Equal(2, history.Transitions.Count(t => t.TransitionResult is SpiritualHealingPersistedResult));
            var schedule = JsonNode.Parse((await files.ReadFileAsync(ProgressionScheduleService.SchedulePath))!)!;
            Assert.Equal(aidTurn!.ProgressionControl!.NextChaosSeaTurnOrdinal, schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
            foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath, ProgressionScheduleService.ReportPath,
                         LiveTurnPreparationService.TurnRequestPath, LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingPlayerActionService.PendingPath })
                Assert.Null(await files.ReadFileBytesAsync(path));
            fixture.AssertResourcesUnchanged();
            if (resident)
            {
                var settled = fixture.CaptureMutationRoots();
                var settledTurn = Assert.IsType<TurnRequest>(aidTurn);
                await using (var settledLease = await files.AcquireCanonicalWriteLeaseAsync())
                    Assert.NotNull(await ProgressionScheduleService.ReadPublishedSpiritualCycleCoreAsync(settledTurn.ProgressionControl!,
                        settledTurn.SessionId, settledTurn.RequestId, settledTurn.TurnNumber, p => files.ReadFileBytesAsync(settledLease, p)));
                fixture.AssertMutationRootsUnchanged(settled);
                var memories = JsonNode.Parse((await files.ReadFileAsync(GuardianAbodeResidentState.StatePath))!)![GuardianAbodeResidentState.ThoughtJournalProperty]!.AsArray();
                Assert.Equal(new[] { 43, 44 }, memories.Select(e => e!["turn"]!.GetValue<int>()));
                Assert.Equal("resident", aid.Stages[0].Before.Owner.OwnerKind);
                Assert.Equal(participantId, aid.Stages[0].Before.Owner.OwnerId);
                Assert.Equal(44, runtime.GameLoop.TurnNumber);
            }
            output.WriteLine($"{(resident ? "Resident" : "Guardian")} {(receiveAid ? "helps player" : "aid")} ({(browser ? "browser" : "console")}): actual grant43/naturalIII1; d20={draw}; final={after.Lifecycle}/{after.Severity.Rank}/{after.Recovery.CurrentStepProgress}; activeH3/targetH0, one aid cycle, one retained attempt.");
        }
        finally
        {
            injectBeforeDraw = false;
            runtime.Engine.ConfigureSnapshotPublicationHooksForTesting(null);
            output.WriteLine(diagnostics.Describe());
            await runtime.AudioService.DisposeAsync();
        }
    }

    private static async Task SeedEngineAidJourneyAsync(FileSystemManager files, JsonObject profiles)
    {
        await SeedEngineAidGuardianAsync(files, profiles);
        await SeedAidJourneyLoreAsync(files);
    }

    private static async Task SeedEngineResidentAidJourneyAsync(FileSystemManager files, JsonObject profiles)
    {
        await SeedEngineAidResidentAsync(files, profiles);
        await SeedAidJourneyLoreAsync(files);
    }

    private static async Task SeedEngineReceivingAidJourneyAsync(FileSystemManager files, JsonObject profiles)
    {
        await SeedEngineReceivingGuardianAsync(files, profiles);
        await SeedAidJourneyLoreAsync(files);
    }

    private static async Task SeedAidJourneyLoreAsync(FileSystemManager files)
    {
        // A second actual turn has accepted chat history and therefore requires
        // the standard EMPTY meta/codex baseline. Seed it before the sole resource
        // bootstrap, never between grant and aid or as invented lore/rewards.
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        await files.WriteFileAtomicAsync(lease, "game_state/meta/achievements.json", JsonSerializer.Serialize(new
        {
            unlockedAchievements = Array.Empty<object>(), trackedProgress = Array.Empty<object>(),
            stats = new
            {
                totalUnlocked = 0,
                byCategory = new { combat = 0, exploration = 0, story = 0, social = 0, crafting = 0, meta = 0, death = 0, secret = 0 },
                byRarity = new { common = 0, uncommon = 0, rare = 0, epic = 0, legendary = 0 }
            }
        }));
        await files.WriteFileAtomicAsync(lease, "lore/codex_entries.json", JsonSerializer.Serialize(new
        {
            entries = Array.Empty<object>(), totalEntries = 0,
            categories = new { cosmology = 0, geography = 0, history = 0, cultures = 0, creatures = 0, characters = 0,
                artifacts = 0, factions = 0, magic = 0, other = 0 }
        }));
    }

    internal static async Task SeedEngineAidGuardianAsync(FileSystemManager files, JsonObject profiles)
    {
        await AfterlifeSpiritualNaturalRecoveryTests.SeedAidGuardianAsync(files, profiles);
        await SeedCompleteAidGuardianLocalityAsync(files);
    }

    internal static async Task SeedEngineAidResidentAsync(FileSystemManager files, JsonObject profiles)
    {
        await AfterlifeSpiritualNaturalRecoveryTests.SeedAidResidentAsync(files, profiles);
        await SeedCompleteAidGuardianLocalityAsync(files);
    }

    internal static async Task SeedEngineReceivingGuardianAsync(FileSystemManager files, JsonObject profiles)
    {
        await AfterlifeSpiritualNaturalRecoveryTests.SeedReceivingGuardianAsync(files, profiles);
        await SeedCompleteAidGuardianLocalityAsync(files);
    }

    private static async Task SeedCompleteAidGuardianLocalityAsync(FileSystemManager files)
    {
        // Reuse the existing complete canonical Guardian factory BEFORE the one
        // resource bootstrap. Empty bootstrap return marker is the current valid
        // no-return contract, not an invented resource cycle/capacity or rebootstrap.
        var guardian = PlayerGuardianFoundationValidationTests.CreateGuardian(
            AfterlifeSpiritualNaturalRecoveryTests.AidGuardianId, "Хранитель целостности",
            AfterlifeSpiritualNaturalRecoveryTests.AidAbodeId);
        Assert.Equal(string.Empty, guardian["gachaSystem"]!["currentReturnCycleId"]!.GetValue<string>());
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        await files.WriteFileAtomicAsync(lease, AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0], new JsonObject
        {
            ["guardians"] = new JsonArray(guardian), ["activeGuardian"] = guardian.DeepClone(),
            ["chaosSeaNavigation"] = new JsonObject
            {
                ["currentGuardianId"] = AfterlifeSpiritualNaturalRecoveryTests.AidGuardianId,
                ["currentAbodeId"] = AfterlifeSpiritualNaturalRecoveryTests.AidAbodeId,
                ["discoveredAbodes"] = new JsonArray(AfterlifeSpiritualNaturalRecoveryTests.AidAbodeId)
            }
        }.ToJsonString());
    }

    private static async Task WriteResidentAidReasoningAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, TurnRequest turn, bool grant)
    {
        await AfterlifeSpiritualNaturalRecoveryTests.WriteResidentAidMemoryAsync(files, lease, turn.RequestId, turn.TurnNumber, memoryOnly: grant);
        const string name = "Житель целостности";
        await files.WriteFileAtomicAsync(lease, "output/debug_logs.json", new JsonObject
        {
            ["timestamp"] = DateTime.UtcNow.ToString("o"), ["gm_thoughts_markdown"] = string.Join("\n",
                "## NPC Scope", "- Mode: Scene-local", "- Relevant actors: " + name,
                "- Why relevant: Житель осознанно решает, разрешить ли душе помощь с его духовной раной.",
                "- Actors outside scope: Хранитель целостности", "- Why outside scope: Он не принимает нового решения в этой частной сцене.",
                "", "## Reasoning", "### " + name, "- Current location: Обитель целостности; перемещения нет.",
                "- Situation: Душа предлагает бесплатное лечение известной духовной раны.",
                "- Profile inputs: Сознательный способный житель с Исцелением0; душа владеет Исцелением3.",
                "- Thoughts: Я понимаю предложение и хочу восстановить целостность без нового долга.",
                "- Motivation: Восстановиться, сохранив право отказаться.",
                "- Constraints: Нет конфликта или оплаты; исход и цикл считает клиент.",
                "- Strategy options:", "1. Принять помощь. Benefit: восстановление. Risk: неудачный бросок.",
                "2. Отказаться. Benefit: дистанция. Risk: рана останется.", "- Chosen strategy: Добровольно принять помощь.",
                "- Rejected alternatives: Нет причины отвергать безопасную помощь.",
                grant ? "- Actions: Я явно разрешаю помощь в следующий безопасный цикл." : "- Actions: Я сохраняю согласие и принимаю помощь.",
                "- State changes: spiritualResidentThoughtJournalUpdates в output/interface_updates.json переносит first-person память через существующий residentThoughtJournalUpdates нормализатор; физический roster до допуска не меняется. Рану, эффекты, History и время публикует клиент.")
        }.ToJsonString());
    }

    private static async Task WriteAidGuardianReasoningAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, TurnRequest turn, bool grant, bool receiveAid = false)
    {
        const string name = "Хранитель целостности";
        var timestamp = DateTime.UtcNow.ToString("o");
        var journal = JsonNode.Parse((await files.ReadFileAsync(lease, GuardianThoughtJournalState.StatePath)) ?? "{\"entries\":[]}")!.AsObject();
        journal[GuardianThoughtJournalState.UpdateProperty] = new JsonArray(new JsonObject
        {
            ["entryId"] = "guardian_aid_thought_" + turn.RequestId, ["guardianId"] = AfterlifeSpiritualNaturalRecoveryTests.AidGuardianId,
            ["turn"] = turn.TurnNumber, ["timestamp"] = timestamp, ["title"] = receiveAid ? grant ? "Предложение помощи" : "Помощь душе после её подтверждения" : grant ? "Разрешение помочь" : "Принятие помощи",
            ["summary"] = receiveAid ? grant ? "Я готов бесплатно помочь сознательной душе; она сама решит, принять ли мою помощь." : "Душа подтвердила принятие помощи, и я лечу её выбранную рану без оплаты."
                : grant ? "Я понимаю предложение души и разрешаю ей вылечить мою рану в следующий безопасный цикл." : "Я сохраняю своё согласие и принимаю помощь души без оплаты.",
            ["eventType"] = "soul_assessment", ["consequence"] = "Помощь добровольна; новая привязка или обязательство не возникают.",
            ["attitude"] = "trusting", ["intent"] = "Сохранить осознанное участие."
        });
        await files.WriteFileAtomicAsync(lease, GuardianThoughtJournalState.StatePath, journal.ToJsonString());
        await files.WriteFileAtomicAsync(lease, "output/debug_logs.json", new JsonObject
        {
            ["timestamp"] = timestamp, ["gm_thoughts_markdown"] = string.Join("\n", "## NPC Scope", "- Mode: Scene-local", "- Relevant actors: " + name,
                receiveAid ? "- Why relevant: Хранитель добровольно предлагает частную помощь душе и ждёт её собственного решения." : "- Why relevant: Хранитель осознанно разрешает помощь и принимает её.", "- Actors outside scope: нет", "- Why outside scope: Другие акторы не участвуют.",
                "", "## Reasoning", "### " + name, "- Current location: Обитель целостности; перемещения нет.",
                receiveAid ? "- Situation: Душа просит частную бесплатную помощь с существующей раной; согласие выразит игрок." : "- Situation: Душа предлагает безвозмездное лечение существующей духовной раны.", receiveAid ? "- Profile inputs: Текущий сознательный Хранитель, Исцеление3; сознательная душа владеет Исцелением0." : "- Profile inputs: Текущий сознательный Хранитель, Исцеление0; душа владеет Исцелением3.",
                receiveAid ? "- Thoughts: Я могу предложить помощь, но не решаю за душу и не выдаю её согласие." : "- Thoughts: Я готов принять помощь и сохраняю право отказаться.", "- Motivation: Восстановить целостность без нового долга.", "- Constraints: Нет конфликта, оплаты или передачи контроля; клиент считает исход и цикл.",
                "- Strategy options:", receiveAid ? "1. Предложить помощь. Benefit: помочь душе. Risk: неудача лечения." : "1. Принять помощь. Benefit: восстановиться. Risk: неудача лечения.", "2. Отказаться. Benefit: сохранить дистанцию. Risk: рана останется.",
                receiveAid ? "- Chosen strategy: Предложить безвозмездную помощь, применить её только после решения игрока." : "- Chosen strategy: Принять помощь добровольно.", "- Rejected alternatives: Нет причины отвергать предложенную безопасную помощь.", receiveAid ? "- Actions: Хранитель выражает готовность; принятие помощи игроком не публикуется от имени Мастера." : "- Actions: Хранитель выражает согласие и остаётся в той же Обители.",
                "- State changes: guardianThoughtJournalUpdates в game_state/meta/guardian_thought_journal.json сохраняет first-person память; профиль содержит только предложенное participation change в grant-ходе. Раны, эффекты, History и цикл публикует клиент.")
        }.ToJsonString());
    }
}
