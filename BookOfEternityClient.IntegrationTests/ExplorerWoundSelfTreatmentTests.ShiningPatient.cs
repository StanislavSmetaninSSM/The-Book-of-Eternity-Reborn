using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Fact] public Task ConsoleShiningResidentPatientUsesConsentCleanupAndActualHealing() => RunShiningPatientJourneyAsync("resident", false);
    [Fact] public Task BrowserShiningResidentPatientUsesConsentCleanupAndActualHealing() => RunShiningPatientJourneyAsync("resident", true);
    [Fact] public Task ConsoleRadiantHeadPatientUsesConsentCleanupAndActualHealing() => RunShiningPatientJourneyAsync("radiant_actor", false);
    [Fact] public Task BrowserRadiantHeadPatientUsesConsentCleanupAndActualHealing() => RunShiningPatientJourneyAsync("radiant_actor", true);
    [Fact] public Task ShiningResidentPatientPublicationFailureRestoresGrantWorldAndMemory() => RunShiningPatientJourneyAsync("resident", false, true);
    [Fact] public Task RadiantHeadPatientPublicationFailureRestoresGrantWorldAndMemory() => RunShiningPatientJourneyAsync("radiant_actor", false, true);

    private async Task RunShiningPatientJourneyAsync(string kind, bool browser, bool failPublication = false)
    {
        var diagnosticRoot = Path.GetFullPath(TestDiagnosticPaths.Root);
        var repo = Path.GetFullPath(TestRepoPaths.RepoRoot).TrimEnd(Path.DirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (diagnosticRoot.Equals(repo, comparison) || diagnosticRoot.StartsWith(repo + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException("Shining patient capture requires a diagnostic root outside checkout.");
        var capture = new LifecyclePassiveTimingDiagnostics("ShiningPatient-" + kind + "-" +
            (failPublication ? "rollback" : browser ? "browser" : "console"), output, gzipRecords: true);
        var timings = new OperationTimingAccumulator(); var window = timings.StartWindow(captureEvents: true);
        Exception? primary = null;
        try { await CoreAsync(); }
        catch (Exception error) { primary = error; }
        finally { await capture.CompleteAsync(primary); }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();

        async Task CoreAsync()
        {
            FileSystemManager? owned = null; var armed = false; var cuts = 0;
            Dictionary<string, byte[]?>? compensationExpected = null; Dictionary<string, byte[]?>? compensationActual = null;
            byte[]? memoryAtCut = null;
            var hooks = new FileSystemManagerHooks
            {
                LocalPublicationObserver = !failPublication ? null : (phase, _) =>
                {
                    if (!armed || phase != TrustedLocalPublicationPhase.MemberPublished ||
                        File.Exists(owned!.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath))) return;
                    armed = false; cuts++;
                    memoryAtCut = File.ReadAllBytes(owned.ResolvePath(kind == "resident"
                        ? GuardianAbodeResidentState.StatePath : WoundCarrierCatalog.AfterlifeProfilesPath));
                    throw new InvalidOperationException("Shining patient common publication cut after request consumption.");
                },
                BeforeCanonicalReadOpenAsync = path =>
                {
                    if (cuts == 1 && compensationActual is null && path == CanonicalStateNormalizer.NormalizerRollbackTrackedFiles[0])
                        compensationActual = compensationExpected!.Keys.ToDictionary(p => p,
                            p => File.Exists(owned!.ResolvePath(p)) ? File.ReadAllBytes(owned.ResolvePath(p)) : null, StringComparer.Ordinal);
                    return Task.CompletedTask;
                }
            };
            using var journey = timings.Measure("ShiningPatient.JourneyIncludingClosing");
            BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture fixture;
            using (timings.Measure("ShiningPatient.FixturePreparation"))
                fixture = await CreateShiningPatientFixtureAsync(kind, hooks, actualRoot => { capture.BindFixtureRoot(actualRoot); capture.Arm(window); });
            Exception? fixtureFailure = null;
            try { await WithFixtureAsync(); }
            catch (Exception error) { fixtureFailure = error; }
            finally
            {
                using var closing = timings.Measure("ShiningPatient.FixtureClosing");
                try { await fixture.DisposeAsync(); }
                catch (Exception error) { fixtureFailure ??= error; capture.RecordClosingFailure(error, "shining-patient-fixture-close"); }
            }
            if (fixtureFailure is not null) ExceptionDispatchInfo.Capture(fixtureFailure).Throw();

            async Task WithFixtureAsync()
            {
                var files = fixture.Files; owned = files;
                var wound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
                Assert.Equal(kind, wound.Owner.OwnerKind); Assert.Equal(ShiningPatientId, wound.Owner.OwnerId);
                JsonObject? proposal = null; JsonObject? retained = null; TurnRequest? treatmentTurn = null;
                var granted = false; var treatmentWritten = false;
                var console = new TestExplorerConsole();
                var diagnostics = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger(capture);
                var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files, inputSource: new NoPlayerInput(diagnostics),
                    logger: diagnostics, explorerConsole: console, finalizationHooks: new GameEngineSessionFinalizationHooks
                    {
                        AtCheckpointAsync = async checkpoint =>
                        {
                            if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted) return;
                            using var responseScope = timings.Measure("ShiningPatient.GmResponse");
                            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                            var turn = JsonSerializer.Deserialize<TurnRequest>((await files.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath))!,
                                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                            if (turn.TurnNumber == 43 && !granted)
                            {
                                Assert.Equal("natural", turn.SpiritualHealing!["mode"]!.GetValue<string>());
                                Assert.Equal(0, Assert.Single(turn.SpiritualHealing["cohort"]!.AsArray())!["healingTier"]!.GetValue<int>());
                                var profiles = JsonNode.Parse((await files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
                                var patient = profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p =>
                                    p["actorType"]?.GetValue<string>() == kind && p["actorId"]?.GetValue<string>() == ShiningPatientId);
                                var shining = JsonNode.Parse((await files.ReadFileAsync(lease, ShiningAbodeState.StatePath))!)!;
                                var factionId = shining["factions"]![0]!["factionId"]!.GetValue<string>();
                                proposal = new JsonObject
                                { ["schemaVersion"] = 5, ["kind"] = "shining_target_consent", ["operation"] = "grant", ["healerId"] = "player_soul",
                                    ["woundId"] = wound.WoundId, ["locality"] = SpiritualTreatmentParticipation.LocalityJson(SpiritualAidLocality.ForFaction(factionId, ShiningPatientHall)),
                                    ["conscious"] = true, ["capable"] = true, ["readableEvidence"] = "Я осознанно разрешаю игроку отдельную попытку лечения моей известной раны в следующий безопасный цикл." };
                                patient[SpiritualTreatmentParticipation.ChangeProperty] = proposal.DeepClone();
                                await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
                                granted = true;
                            }
                            else
                            {
                                Assert.Equal(44, turn.TurnNumber); Assert.False(treatmentWritten);
                                Assert.Equal("aid", turn.SpiritualHealing!["mode"]!.GetValue<string>());
                                Assert.Equal("player_to_shining_entity", turn.SpiritualHealing["direction"]!.GetValue<string>());
                                Assert.Equal(3, turn.SpiritualHealing["healingTier"]!.GetValue<int>());
                                Assert.Equal(0, turn.SpiritualHealing["targetNaturalHealingTier"]!.GetValue<int>());
                                retained = JsonNode.Parse((await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();
                                Assert.Equal(8, retained["schemaVersion"]!.GetValue<int>());
                                Assert.Equal(kind, retained["target"]!["ownerKind"]!.GetValue<string>());
                                Assert.False(retained.ContainsKey("playerConfirmation")); Assert.False(retained.ContainsKey("currencyInvariant"));
                                treatmentTurn = turn; treatmentWritten = true;
                            }
                            await WriteNativeCompletionAsync(files, lease, turn,
                                AfterlifeSpiritualNaturalRecoveryTests.AuthorNaturalGraphs(wound, turn.SpiritualHealing!["destinationGraphs"]!.AsArray()),
                                turn.TurnNumber == 43 ? "Пациент сознательно соглашается на следующую отдельную попытку; сейчас проходит только обычное восстановление."
                                    : "Душа отдельно подтверждает лечение пациента; клиент применяет реальный исход и обычный цикл мира.");
                            await WriteShiningPatientReasoningAsync(files, lease, turn, kind);
                            if (failPublication && turn.TurnNumber == 44)
                            {
                                compensationExpected = fixture.CaptureMutationRoots(new[] { ShiningAbodeState.StatePath,
                                    GuardianAbodeResidentState.StatePath, GuardianThoughtJournalState.StatePath, EffectAcceptedTurnInputComposer.WorldTimePath });
                                armed = true;
                            }
                        }
                    });
                try
                {
                    runtime.GameLoop.SetSession("session_patient_" + kind + "_" + (browser ? "browser" : "console"), 42);
                    using (timings.Measure("ShiningPatient.Refresh")) await runtime.StateManager.RefreshGameStateAsync();
                    var wallet = SpiritualTreatmentWalletInvariant.Capture(JsonNode.Parse((await files.ReadFileAsync(AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!.AsObject());
                    using (timings.Measure("ShiningPatient.ConsentTurn"))
                        await runtime.Engine.ProcessPlayerTurn("Предложить находящемуся рядом обитателю помощь с его известной духовной раной; попытку лечения пока не начинать.");
                    Assert.True(granted); Assert.Equal(43, runtime.GameLoop.TurnNumber);
                    wound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
                    Assert.Equal(3, wound.Severity.Rank); Assert.Equal(1, wound.Recovery.CurrentStepProgress);
                    await AssertNegotiatedCleanupAsync(files);
                    var grant = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single((await fixture.ReadHistoryAsync()).Transitions,
                        t => t.TransitionResult is SpiritualHealingPersistedResult).TransitionResult);
                    Assert.True(grant.IsNatural); Assert.Equal(8, grant.SchemaVersion);
                    Assert.Null(grant.ToCanonicalJson()["request"]); Assert.False(grant.ToCanonicalJson().ContainsKey("direction"));
                    Assert.Equal(5, grant.ParticipationOutcomes![0]!["record"]!["schemaVersion"]!.GetValue<int>());
                    var tracked = new[] { AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath, GuardianThoughtJournalState.StatePath,
                        GuardianAbodeResidentState.StatePath, ShiningAbodeState.StatePath, EffectAcceptedTurnInputComposer.WorldTimePath, PendingTurnStateService.PendingDiceStatePath };
                    var before = fixture.CaptureMutationRoots(tracked);
                    string? action = null; PendingPlayerActionService.Binding? queued = null;
                    using (timings.Measure("ShiningPatient.MenuAndConfirmation"))
                    {
                        var opened = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
                        var target = Assert.Single(opened.WoundCommandState!.Targets, t => t.Owner.OwnerKind == kind && t.Owner.OwnerId == ShiningPatientId);
                        var choice = Assert.Single(opened.WoundCommandState.Choices, c => c.Available && c.Selection.Aid?.ShiningPatient?.Target == target.Owner);
                        Assert.Equal(8, choice.Selection.Aid!.FamilyVersion); Assert.False(choice.Selection.Aid.PlayerConfirmed);
                        fixture.AssertMutationRootsUnchanged(before);
                        if (browser)
                        {
                            var service = CreateCommands(files);
                            var cancel = await service.ExecuteAsync(new("/лечить", Owner));
                            await service.CancelPromptSessionAsync(new(cancel.InteractiveSession!.SessionId, Owner));
                            fixture.AssertMutationRootsUnchanged(before);
                            var promptResult = await service.ExecuteAsync(new("/лечить", Owner));
                            var targets = Assert.IsType<UiSelectionPrompt>(Assert.Single(promptResult.Prompts));
                            var patientOption = Assert.Single(targets.Options, o => !o.Disabled && o.Label == PatientName(kind));
                            promptResult = await service.SubmitPromptSessionAsync(new(promptResult.InteractiveSession!.SessionId,
                                new() { ["wound_target"] = JsonValue.Create(patientOption.Value) }, Owner));
                            Assert.DoesNotContain(ShiningPatientId, JsonSerializer.Serialize(promptResult));
                            Assert.DoesNotContain(wound.WoundId, JsonSerializer.Serialize(promptResult));
                            var prompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(promptResult.Prompts, p => p.Id == "wound_selection"));
                            var option = Assert.Single(prompt.Options, o => !o.Disabled);
                            var submitted = await service.SubmitPromptSessionAsync(new(promptResult.InteractiveSession!.SessionId,
                                new() { ["wound_selection"] = JsonValue.Create(option.Value), ["confirm_wound_treatment"] = JsonValue.Create(true) }, Owner));
                            Assert.DoesNotContain(submitted.Notifications, n => n.Severity == UiNotificationSeverity.Error);
                            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                            queued = Assert.IsType<PendingPlayerActionService.State>(await PendingPlayerActionService.ReadAsync(files, lease)).Binding;
                            Assert.Equal(choice.Selection, queued.SpiritualSelfTreatment);
                        }
                        else
                        {
                            console.QueueAnySelection("Отмена");
                            Assert.Equal(string.Empty, await runtime.Explorer.TryProcessCommand("/лечить"));
                            fixture.AssertMutationRootsUnchanged(before); Assert.Empty(console.ConfirmPrompts);
                            var targets = opened.WoundCommandState.Targets.Where(t => t.Available).ToArray();
                            console.QueueAnySelection((Array.IndexOf(targets, target) + 1) + ". " + target.Label);
                            console.QueueAnySelection("1. " + choice.Name + " — " + choice.Severity);
                            console.QueueAnyConfirmResponse(true);
                            action = await runtime.Explorer.TryProcessCommand("/лечить");
                            Assert.Equal(WoundApplicationService.ShiningPatientAidActionText, action); Assert.Single(console.ConfirmPrompts);
                        }
                        fixture.AssertMutationRootsUnchanged(before);
                    }
                    using (timings.Measure("ShiningPatient.TreatmentTurn"))
                        await runtime.Engine.ProcessPlayerTurn(queued?.Action ?? action!, browserAction: queued);
                    using var assertions = timings.Measure("ShiningPatient.Assertions");
                    Assert.True(treatmentWritten); Assert.NotNull(retained);
                    if (failPublication)
                    {
                        Assert.Equal(1, cuts); Assert.NotNull(compensationExpected); Assert.NotNull(compensationActual); Assert.NotNull(memoryAtCut);
                        foreach (var pair in compensationExpected!) Assert.Equal(pair.Value, compensationActual![pair.Key]);
                        Assert.True(JsonNode.DeepEquals(retained, JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(compensationActual![AcceptedMechanicsPlan.WoundCommandPath]!))));
                        if (kind == "resident") Assert.Contains(JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(memoryAtCut!))!["thoughtJournal"]!.AsArray(),
                            e => e!["residentId"]?.GetValue<string>() == ShiningPatientId && e["turn"]!.GetValue<int>() == 44);
                        else Assert.Contains("отдельно подтверждённую", JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(memoryAtCut!))!["profiles"]!.AsArray()
                            .OfType<JsonObject>().Single(p => p["actorType"]?.GetValue<string>() == kind && p["actorId"]?.GetValue<string>() == ShiningPatientId)["gmThoughtsSummary"]!.GetValue<string>());
                        Assert.Equal(43, runtime.GameLoop.TurnNumber);
                        fixture.AssertMutationRootsUnchanged(before);
                        Assert.DoesNotContain((await fixture.ReadHistoryAsync()).Transitions, t => t.AttemptId is not null);
                        await AssertNegotiatedCleanupAsync(files); fixture.AssertResourcesUnchanged(); return;
                    }
                    Assert.Equal(44, runtime.GameLoop.TurnNumber);
                    var history = await fixture.ReadHistoryAsync();
                    var result = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(history.Transitions, t => t.AttemptId is not null).TransitionResult);
                    Assert.Equal(8, result.SchemaVersion); Assert.Equal("aid", result.Mode); Assert.True(result.AidGrantAgrees(grant));
                    Assert.True(JsonNode.DeepEquals(retained, result.ToCanonicalJson()["request"]));
                    var values = retained!["attempt"]!["roll"]!["values"]!.AsArray().Select(v => v!.GetValue<int>()).ToArray();
                    var die = retained["attempt"]!["roll"]!["mode"]!.GetValue<string>() == "disadvantage" ? values.Min() : values.Max();
                    var calculation = SpiritualHealingOutcomeMath.Calculate(new(3, wound.Severity.Rank, die, 0, 0))!;
                    var expected = SpiritualWoundRecoveryPlanner.Build(wound, 0, retained["attempt"]!["attemptId"]!.GetValue<string>(),
                        retained["attempt"]!["cycleKey"]!.GetValue<string>(), calculation);
                    Assert.Equal(expected.Select(v => (v.Kind, v.SeverityRank, v.Progress)), result.Stages.Select(v => (v.Kind, v.After.Severity.Rank, v.After.Recovery.CurrentStepProgress)));
                    var after = result.Stages[^1].After;
                    if (after.Lifecycle == "healed") Assert.Empty((await fixture.ReadWoundsAsync()).Occurrences);
                    else Assert.Equal(WoundMaterializationContract.SerializeCanonical(after), WoundMaterializationContract.SerializeCanonical(Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound));
                    Assert.Equal(2, history.Transitions.Count(t => t.TransitionResult is SpiritualHealingPersistedResult));
                    var schedule = JsonNode.Parse((await files.ReadFileAsync(ProgressionScheduleService.SchedulePath))!)!;
                    Assert.Equal(grant.ToCanonicalJson()["control"]!["nextChaosSeaTurnOrdinal"]!.GetValue<int>() + 1,
                        treatmentTurn!.ProgressionControl!.NextChaosSeaTurnOrdinal);
                    Assert.Equal(treatmentTurn.ProgressionControl.NextChaosSeaTurnOrdinal, schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
                    wallet.RequirePreserved(JsonNode.Parse((await files.ReadFileAsync(AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!.AsObject());
                    await AssertNegotiatedCleanupAsync(files);
                    var settled = fixture.CaptureMutationRoots(tracked);
                    await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                        Assert.NotNull(await ProgressionScheduleService.ReadPublishedSpiritualCycleCoreAsync(treatmentTurn.ProgressionControl,
                            treatmentTurn.SessionId, treatmentTurn.RequestId, treatmentTurn.TurnNumber, p => files.ReadFileBytesAsync(lease, p)));
                    fixture.AssertMutationRootsUnchanged(settled); fixture.AssertResourcesUnchanged();
                    if (kind == "resident")
                    {
                        var journal = JsonNode.Parse((await files.ReadFileAsync(GuardianAbodeResidentState.StatePath))!)!["thoughtJournal"]!.AsArray();
                        var own = journal.Where(e => e!["residentId"]?.GetValue<string>() == ShiningPatientId).ToArray();
                        Assert.Equal(new[] { 43, 44 }, own.Select(e => e!["turn"]!.GetValue<int>()));
                        Assert.NotNull(result.ResidentParticipationRecord);
                    }
                    else
                    {
                        Assert.Null(result.ResidentParticipationRecord);
                        var profile = JsonNode.Parse((await files.ReadFileAsync(WoundCarrierCatalog.AfterlifeProfilesPath))!)!["profiles"]!.AsArray()
                            .OfType<JsonObject>().Single(p => p["actorType"]?.GetValue<string>() == kind && p["actorId"]?.GetValue<string>() == ShiningPatientId);
                        Assert.Contains("отдельно подтверждённую", profile["gmThoughtsSummary"]!.GetValue<string>());
                    }
                    output.WriteLine($"Shining patient {kind}/{(browser ? "browser" : "console")}: genuine consent43/cleanup/confirmed44, retained die/natural8/active8, wound/effects/History/memory/one cycle and inert published replay.");
                }
                catch (Exception error) { fixtureFailure ??= error; }
                finally
                {
                    armed = false;
                    try { capture.WriteLine(diagnostics.Describe()); }
                    catch (Exception error) { fixtureFailure ??= error; capture.RecordClosingFailure(error, "shining-patient-describe-forward"); }
                    try { using (timings.Measure("ShiningPatient.AudioDisposal")) await runtime.AudioService.DisposeAsync(); }
                    catch (Exception error) { fixtureFailure ??= error; capture.RecordClosingFailure(error, "shining-patient-audio-close"); }
                }
            }
        }
    }
}
