using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Fact] public Task ConsoleDiagnosisConfirmsKnowledgeAndOneOrdinaryCycle() => RunDiagnosisJourneyAsync(false);
    [Fact] public Task BrowserDiagnosisConfirmsKnowledgeAndOneOrdinaryCycle() => RunDiagnosisJourneyAsync(true);
    [Fact] public Task DiagnosisCommonPublicationFailureRestoresWholeTurn() => RunDiagnosisJourneyAsync(false, true);

    private async Task RunDiagnosisJourneyAsync(bool browser, bool failPublication = false)
    {
        var diagnosticRoot = Path.GetFullPath(TestDiagnosticPaths.Root);
        var repo = Path.GetFullPath(TestRepoPaths.RepoRoot).TrimEnd(Path.DirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (diagnosticRoot.Equals(repo, comparison) || diagnosticRoot.StartsWith(repo + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException("Diagnosis capture requires a diagnostic root outside checkout.");
        var capture = new LifecyclePassiveTimingDiagnostics("SpiritualDiagnosis-" + (failPublication ? "rollback" : browser ? "browser" : "console"), output, gzipRecords: true);
        var timings = new OperationTimingAccumulator(); var window = timings.StartWindow(captureEvents: true);
        Exception? primary = null; IDisposable? journey = null; IDisposable? preparation = null;
        try { await CoreAsync(); }
        catch (Exception error) { primary = error; }
        finally { preparation?.Dispose(); journey?.Dispose(); await capture.CompleteAsync(primary); }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();

        async Task CoreAsync()
        {
            FileSystemManager? owned = null; var armed = false; var cuts = 0;
            Dictionary<string, byte[]?>? compensationExpected = null; Dictionary<string, byte[]?>? compensationActual = null;
            byte[]? historyAtCut = null;
            var hooks = new FileSystemManagerHooks
            {
                LocalPublicationObserver = !failPublication ? null : (phase, _) =>
                {
                    if (!armed || phase != TrustedLocalPublicationPhase.MemberPublished || File.Exists(owned!.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath))) return;
                    armed = false; cuts++; historyAtCut = File.ReadAllBytes(owned.ResolvePath(WoundHistoryState.HistoryPath));
                    throw new InvalidOperationException("Diagnosis common publication cut after exact request consumption.");
                },
                BeforeCanonicalReadOpenAsync = !failPublication ? null : path =>
                {
                    if (cuts == 1 && compensationActual is null && path == CanonicalStateNormalizer.NormalizerRollbackTrackedFiles[0])
                        compensationActual = compensationExpected!.Keys.ToDictionary(p => p,
                            p => File.Exists(owned!.ResolvePath(p)) ? File.ReadAllBytes(owned.ResolvePath(p)) : null, StringComparer.Ordinal);
                    return Task.CompletedTask;
                }
            };
            void Allocated(string root)
            {
                capture.BindFixtureRoot(root); capture.Arm(window);
                journey = timings.Measure("SpiritualDiagnosis.JourneyIncludingClosing");
                preparation = timings.Measure("SpiritualDiagnosis.FixturePreparation");
            }
            HealingFixture fixture;
            try
            {
                fixture = await HealingFixture.CreateAsync(2, 0, 20, stageRequest: false, hooks: hooks,
                    realm: browser ? "Shining Abode" : "Chaos Sea", allocatedRootForDiagnostics: Allocated,
                    beforeResourceBootstrap: AfterlifeSpiritualNaturalRecoveryTests.SeedDiagnosisBeforeBootstrapAsync);
            }
            finally { preparation?.Dispose(); preparation = null; }
            Exception? fixtureFailure = null;
            try { await WithFixtureAsync(); }
            catch (Exception error) { fixtureFailure = error; }
            finally
            {
                using var closing = timings.Measure("SpiritualDiagnosis.FixtureClosing");
                try { await fixture.DisposeAsync(); }
                catch (Exception error) { fixtureFailure ??= error; capture.RecordClosingFailure(error, "diagnosis-fixture-close"); }
            }
            if (fixtureFailure is not null) ExceptionDispatchInfo.Capture(fixtureFailure).Throw();

            async Task WithFixtureAsync()
            {
                var files = fixture.Files; owned = files;
                var original = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
                var console = new TestExplorerConsole();
                var diagnostics = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger(capture);
                JsonObject? retained = null; TurnRequest? turn = null; var responded = false;
                using var construction = timings.Measure("SpiritualDiagnosis.RuntimeConstruction");
                var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files, inputSource: new NoPlayerInput(diagnostics),
                    logger: diagnostics, explorerConsole: console, finalizationHooks: new GameEngineSessionFinalizationHooks
                    {
                        AtCheckpointAsync = async checkpoint =>
                        {
                            if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted || responded) return;
                            using var response = timings.Measure("SpiritualDiagnosis.GmResponse");
                            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                            turn = JsonSerializer.Deserialize<TurnRequest>((await files.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath))!, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                            Assert.Equal("diagnose", turn.SpiritualHealing!["mode"]!.GetValue<string>());
                            Assert.Null(turn.SpiritualHealing["roll"]); Assert.Null(turn.SpiritualHealing["payment"]);
                            retained = JsonNode.Parse((await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();
                            Assert.True(SpiritualDiagnosisRequestCodec.IsKind(retained));
                            Assert.Equal(0, retained["diagnosis"]!["healingTier"]!.GetValue<int>());
                            Assert.False(retained.ContainsKey("attempt")); Assert.False(retained.ContainsKey("payment"));
                            var repeatedBefore = fixture.CaptureMutationRoots();
                            var repeated = await AfterlifeWoundTreatmentAuthority.StageDiagnosisAsync(files, lease,
                                new(turn.SessionId, turn.RequestId, turn.TurnNumber, turn.ProgressionControl!), SpiritualDiagnosisRequestCodec.ReadSelection(retained));
                            Assert.True(repeated.IsStaged); Assert.True(repeated.AlreadyStaged);
                            fixture.AssertMutationRootsUnchanged(repeatedBefore);
                            await WriteNativeCompletionAsync(files, lease, turn,
                                narrative: "Я осматриваю духовную рану по выбранному способу. Сведения раскрывает клиент; мир продолжает один обычный безопасный цикл без платы и броска осмотра.");
                            compensationExpected = fixture.CaptureMutationRoots(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Concat(
                                new[] { AcceptedMechanicsPlan.WoundCommandPath, ProgressionScheduleService.ReportPath }));
                            responded = true; armed = failPublication;
                        }
                    });
                construction.Dispose();
                Exception? bodyFailure = null;
                try
                {
                    runtime.GameLoop.SetSession("session_diagnosis_" + (browser ? "browser" : "console"), 42);
                    using (timings.Measure("SpiritualDiagnosis.Refresh")) await runtime.StateManager.RefreshGameStateAsync();
                    if (failPublication)
                        await new ProgressionScheduleService(files, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProgressionScheduleService>.Instance).EnsureInitializedAsync();
                    var before = fixture.CaptureMutationRoots(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
                    string? action = null; PendingPlayerActionService.Binding? queued = null;
                    using (timings.Measure("SpiritualDiagnosis.ReadCancelConfirm"))
                    {
                        var opened = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
                        var state = Assert.IsType<WoundCommandState>(opened.WoundCommandState);
                        var choice = Assert.Single(state.Choices, c => c.Available && c.Selection.Diagnosis is not null);
                        if (browser)
                        {
                            var service = CreateCommands(files);
                            var cancelled = await service.ExecuteAsync(new("/лечить", Owner));
                            await service.CancelPromptSessionAsync(new(cancelled.InteractiveSession!.SessionId, Owner));
                            fixture.AssertMutationRootsUnchanged(before);
                            var promptResult = await service.ExecuteAsync(new("/лечить", Owner));
                            if (promptResult.Prompts.OfType<UiSelectionPrompt>().SingleOrDefault(p => p.Id == "wound_target") is { } targetPrompt)
                            {
                                var self = Assert.Single(targetPrompt.Options, o => !o.Disabled && o.Label == "Себя");
                                promptResult = await service.SubmitPromptSessionAsync(new(promptResult.InteractiveSession!.SessionId,
                                    new() { ["wound_target"] = JsonValue.Create(self.Value) }, Owner));
                            }
                            var prompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(promptResult.Prompts, p => p.Id == "wound_selection"));
                            var option = Assert.Single(prompt.Options, o => !o.Disabled && o.Label.Contains(choice.Name, StringComparison.Ordinal));
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
                            var selections = new List<string>();
                            if (state.IsTargetSelection)
                            {
                                var targets = state.Targets.Where(t => t.Available).ToArray();
                                var self = Assert.Single(targets, t => t.Owner.OwnerKind == "player_soul");
                                selections.Add((Array.IndexOf(targets, self) + 1) + ". " + self.Label);
                            }
                            var choices = state.Choices.Where(c => c.Available).ToArray();
                            selections.Add((Array.IndexOf(choices, choice) + 1) + ". " + choice.Name + " — " + choice.Severity);
                            console.QueueAnySelection(selections.ToArray()); console.QueueAnyConfirmResponse(true);
                            action = await runtime.Explorer.TryProcessCommand("/лечить");
                            Assert.Equal(WoundApplicationService.DiagnosisActionText, action); Assert.Single(console.ConfirmPrompts);
                        }
                        fixture.AssertMutationRootsUnchanged(before);
                    }
                    using (timings.Measure("SpiritualDiagnosis.ProcessPlayerTurn"))
                        await runtime.Engine.ProcessPlayerTurn(queued?.Action ?? action!, browserAction: queued);
                    using var assertions = timings.Measure("SpiritualDiagnosis.Assertions");
                    Assert.True(responded); Assert.NotNull(turn); Assert.NotNull(retained);
                    if (failPublication)
                    {
                        Assert.Equal(1, cuts); Assert.NotNull(compensationExpected); Assert.NotNull(compensationActual); Assert.NotNull(historyAtCut);
                        foreach (var pair in compensationExpected!) Assert.Equal(pair.Value, compensationActual![pair.Key]);
                        Assert.True(WoundHistoryState.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(historyAtCut!), WoundHistoryState.HistoryPath).IsValid);
                        Assert.Equal(42, runtime.GameLoop.TurnNumber); fixture.AssertMutationRootsUnchanged(before);
                        Assert.DoesNotContain((await fixture.ReadHistoryAsync()).Transitions, t => t.Kind == "diagnose");
                    }
                    else
                    {
                        Assert.Equal(43, runtime.GameLoop.TurnNumber);
                        var history = await fixture.ReadHistoryAsync();
                        var result = Assert.IsType<SpiritualDiagnosisPersistedResult>(Assert.Single(history.Transitions, t => t.Kind == "diagnose").TransitionResult);
                        Assert.True(JsonNode.DeepEquals(retained, result.Request));
                        Assert.Equal(WoundMaterializationContract.SerializeCanonical(original), WoundMaterializationContract.SerializeCanonical(result.Original));
                        Assert.Equal(original.Recovery, result.KnowledgeAfter.Recovery);
                        var after = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
                        Assert.Equal(2, after.Severity.Rank); Assert.Equal(1, after.Recovery.CurrentStepProgress);
                        Assert.Equal("known_to_player", Assert.Single(after.Complications).Visibility);
                        Assert.DoesNotContain(history.Transitions, t => t.Kind == "treat");
                        var replayBefore = fixture.CaptureMutationRoots();
                        Assert.True(await ProgressionScheduleService.IsPublishedSpiritualCycleCoreAsync(turn!.ProgressionControl!,
                            turn.SessionId, turn.RequestId, turn.TurnNumber, path => files.ReadFileBytesAsync(path),
                            result.Binding["snapshotToken"]!.GetValue<string>(), retained));
                        fixture.AssertMutationRootsUnchanged(replayBefore);
                    }
                    foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath, ProgressionScheduleService.ReportPath,
                        LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingPlayerActionService.PendingPath })
                        Assert.Null(await files.ReadFileBytesAsync(path));
                    fixture.AssertResourcesUnchanged();
                    output.WriteLine("Diagnosis: separate confirmation, no diagnostic draw/fee, knowledge first, one ordinary natural cycle; complete cleanup.");
                }
                catch (Exception error) { bodyFailure = error; }
                finally
                {
                    armed = false;
                    try { output.WriteLine(diagnostics.Describe()); }
                    catch (Exception error) { bodyFailure ??= error; capture.RecordClosingFailure(error, "diagnosis-describe"); }
                    try { using var audio = timings.Measure("SpiritualDiagnosis.AudioClosing"); await runtime.AudioService.DisposeAsync(); }
                    catch (Exception error) { bodyFailure ??= error; capture.RecordClosingFailure(error, "diagnosis-audio-close"); }
                }
                if (bodyFailure is not null) ExceptionDispatchInfo.Capture(bodyFailure).Throw();
            }
        }
    }
}
