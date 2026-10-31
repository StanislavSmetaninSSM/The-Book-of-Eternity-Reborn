using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Fact] public Task ConsoleMortalDiagnosisUsesActualSignedProcedureAndDeclaredTime() => RunMortalDiagnosisJourneyAsync(false, false);
    [Fact] public Task BrowserMortalDiagnosisUsesActualSignedProcedureAndDeclaredTime() => RunMortalDiagnosisJourneyAsync(true, false);
    [Fact] public Task MortalDiagnosisCommonPublicationCutRestoresHeldAndOriginalImages() => RunMortalDiagnosisJourneyAsync(false, true);

    private async Task RunMortalDiagnosisJourneyAsync(bool browser, bool failPublication)
    {
        var diagnosticRoot = Path.GetFullPath(TestDiagnosticPaths.Root);
        var repo = Path.GetFullPath(TestRepoPaths.RepoRoot).TrimEnd(Path.DirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (diagnosticRoot.Equals(repo, comparison) || diagnosticRoot.StartsWith(repo + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException("Mortal diagnosis capture requires a diagnostic root outside checkout.");
        var capture = new LifecyclePassiveTimingDiagnostics("MortalDiagnosis-" + (failPublication ? "rollback" : browser ? "browser" : "console"), output, gzipRecords: true);
        var timings = new OperationTimingAccumulator(); var window = timings.StartWindow(captureEvents: true);
        var root = Path.Combine(Path.GetTempPath(), "boe-mortal-diagnosis-journey-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); capture.BindFixtureRoot(root); capture.Arm(window);
        Exception? primary = null;
        var journey = timings.Measure("MortalDiagnosis.JourneyIncludingClosing");
        try { await CoreAsync(); }
        catch (Exception error) { primary = error; }
        finally
        {
            using (timings.Measure("MortalDiagnosis.FixtureClosing"))
            {
                try { Directory.Delete(root, true); }
                catch (Exception error) { primary ??= error; capture.RecordClosingFailure(error, "mortal-diagnosis-fixture-close"); }
            }
            journey?.Dispose(); await capture.CompleteAsync(primary);
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();

        async Task CoreAsync()
        {
            FileSystemManager? owned = null; var armed = false; var cuts = 0;
            Dictionary<string, byte[]?>? heldBefore = null; Dictionary<string, byte[]?>? compensationActual = null;
            Dictionary<string, byte[]?>? atCut = null;
            RepairPlayerInput? repairInput = null;
            var hooks = new FileSystemManagerHooks
            {
                LocalPublicationObserver = !failPublication ? null : (phase, _) =>
                {
                    if (!armed || phase != TrustedLocalPublicationPhase.MemberPublished || File.Exists(owned!.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath))) return;
                    armed = false; cuts++; atCut = CaptureMortalDiagnosisImages(owned!);
                    repairInput!.RequestEscape();
                    throw new InvalidOperationException("Controlled Mortal diagnosis publication cut after actual input consumption.");
                },
                BeforeCanonicalReadOpenAsync = !failPublication ? null : path =>
                {
                    if (cuts == 1 && compensationActual is null && path == CanonicalStateNormalizer.NormalizerRollbackTrackedFiles[0])
                        compensationActual = CaptureMortalDiagnosisImages(owned!);
                    return Task.CompletedTask;
                }
            };
            var files = owned = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);
            WoundMaterializationEnvelope original;
            using (timings.Measure("MortalDiagnosis.FixturePreparation"))
                original = await SeedMortalDiagnosisJourneyAsync(files);
            var coreBefore = Assert.IsType<byte[]>(await files.ReadFileBytesAsync("game_state/npcs/npc_core.json"));
            var itemIdentityBefore = Assert.IsType<string>(await files.ReadFileAsync(MortalItemIdentityState.StatePath));
            var journalsBefore = Assert.IsType<byte[]>(await files.ReadFileBytesAsync("game_state/npcs/npc_journals.json"));
            var healthBefore = Assert.IsType<string>(await files.ReadFileAsync(ResourceMaterializationContract.StatePath));
            var console = new TestExplorerConsole();
            var diagnostics = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger(capture);
            var input = repairInput = new RepairPlayerInput(diagnostics);
            TurnRequest? turn = null; MortalWoundTreatmentAttemptRequest? heldRequest = null;
            JsonObject? signedInput = null; var responded = false;
            GameEngineTestRuntime runtime;
            using (timings.Measure("MortalDiagnosis.RuntimeConstruction"))
                runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files, inputSource: input,
                    configureSettings: settings => { settings.GmBridgeAutoStart = false; settings.MusicEnabled = false;
                        settings.SoundEnabled = false; settings.AutosaveIntervalTurns = 0; settings.EnableQteEvents = false; },
                    logger: diagnostics, explorerConsole: console, finalizationHooks: new GameEngineSessionFinalizationHooks
                    {
                        AtCheckpointAsync = async checkpoint =>
                        {
                            if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted || responded) return;
                            using var response = timings.Measure("MortalDiagnosis.GmResponse");
                            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                            turn = JsonSerializer.Deserialize<TurnRequest>((await files.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath))!, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                            Assert.NotNull(turn.MortalDiagnosis); Assert.Null(turn.SpiritualHealing);
                            signedInput = JsonNode.Parse((await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();
                            Assert.True(MortalWoundDiagnosisInputCodec.IsBound(signedInput, new(turn.SessionId, turn.RequestId, turn.TurnNumber, turn.ProgressionControl!)));
                            var admission = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(files, lease,
                                new(turn.SessionId, turn.RequestId, turn.TurnNumber, turn.ProgressionControl!), requireRetained: true);
                            heldRequest = admission.Request;
                            var check = Assert.IsType<MortalWoundProcedureCheckAuthority>(heldRequest.ModeAuthority);
                            Assert.Single(check.SourceIndices); Assert.Equal(check.NaturalRoll, Assert.Single(check.SourceRolls));
                            Assert.Equal(check.NaturalRoll, turn.PreGeneratedDices1d20![Assert.Single(check.SourceIndices)]);
                            Assert.False(turn.ProgressionControl!.MustEvaluateWorldProgression);
                            Assert.False(turn.ProgressionControl.MustEvaluateFactionProgression);
                            Assert.Equal(1260, turn.ProgressionControl.CurrentWorldTimeInMinutes);
                            var retainedBefore = CaptureMortalDiagnosisImages(files);
                            var repeated = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(files, lease,
                                new(turn.SessionId, turn.RequestId, turn.TurnNumber, turn.ProgressionControl!), requireRetained: true);
                            Assert.Equal(heldRequest.RequestFingerprint, repeated.Request.RequestFingerprint);
                            AssertMortalDiagnosisImages(retainedBefore, files);
                            await WriteMortalDiagnosisJourneyResponseAsync(files, lease, turn, journalsBefore);
                            heldBefore = CaptureMortalDiagnosisImages(files); responded = true; armed = failPublication;
                        }
                    });
            Exception? bodyFailure = null;
            try
            {
                runtime.GameLoop.SetSession("session_mortal_diagnosis_" + (browser ? "browser" : failPublication ? "rollback" : "console"), 42);
                using (timings.Measure("MortalDiagnosis.Refresh")) await runtime.StateManager.RefreshGameStateAsync();
                var before = CaptureMortalDiagnosisImages(files);
                string? action = null; PendingPlayerActionService.Binding? queued = null;
                using (timings.Measure("MortalDiagnosis.ReadCancelConfirm"))
                {
                    var opened = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
                    var menu = Assert.IsType<WoundCommandState>(opened.WoundCommandState);
                    var selectedMenu = menu.IsTargetSelection ? menu.ForTarget(Assert.Single(menu.Targets, row => row.Owner.OwnerKind == "player")) : menu;
                    var choice = Assert.Single(selectedMenu.Choices, row => row.Available && row.Selection.MortalDiagnosis?.Provider.OwnerId == "field_medic_01");
                    Assert.Contains("1d20", choice.Reason); Assert.Contains("30 мин.", choice.Reason); Assert.Contains("Расход", choice.Reason);
                    if (browser)
                    {
                        var service = CreateCommands(files);
                        var cancelled = await service.ExecuteAsync(new("/лечить", Owner));
                        await service.CancelPromptSessionAsync(new(cancelled.InteractiveSession!.SessionId, Owner));
                        AssertMortalDiagnosisImages(before, files);
                        var promptResult = await service.ExecuteAsync(new("/лечить", Owner));
                        if (promptResult.Prompts.OfType<UiSelectionPrompt>().SingleOrDefault(p => p.Id == "wound_target") is { } targets)
                        {
                            var self = Assert.Single(targets.Options, option => !option.Disabled && option.Label == "Себя");
                            promptResult = await service.SubmitPromptSessionAsync(new(promptResult.InteractiveSession!.SessionId,
                                new() { ["wound_target"] = JsonValue.Create(self.Value) }, Owner));
                        }
                        var prompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(promptResult.Prompts, p => p.Id == "wound_selection"));
                        var option = Assert.Single(prompt.Options, row => !row.Disabled && row.Label.Contains(choice.Name, StringComparison.Ordinal));
                        var submitted = await service.SubmitPromptSessionAsync(new(promptResult.InteractiveSession!.SessionId,
                            new() { ["wound_selection"] = JsonValue.Create(option.Value), ["confirm_wound_treatment"] = JsonValue.Create(true) }, Owner));
                        Assert.DoesNotContain(submitted.Notifications, row => row.Severity == UiNotificationSeverity.Error);
                        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                        queued = Assert.IsType<PendingPlayerActionService.State>(await PendingPlayerActionService.ReadAsync(files, lease)).Binding;
                        Assert.Equal(choice.Selection, queued.SpiritualSelfTreatment);
                    }
                    else
                    {
                        console.QueueAnySelection("Отмена"); Assert.Equal(string.Empty, await runtime.Explorer.TryProcessCommand("/лечить"));
                        AssertMortalDiagnosisImages(before, files); Assert.Empty(console.ConfirmPrompts);
                        var sequence = new List<string>();
                        if (menu.IsTargetSelection)
                        {
                            var targets = menu.Targets.Where(row => row.Available).ToArray();
                            var self = Assert.Single(targets, row => row.Owner.OwnerKind == "player");
                            sequence.Add((Array.IndexOf(targets, self) + 1) + ". " + self.Label);
                        }
                        var choices = selectedMenu.Choices.Where(row => row.Available).ToArray();
                        sequence.Add((Array.IndexOf(choices, choice) + 1) + ". " + choice.Name + " — " + choice.Severity);
                        console.QueueAnySelection(sequence.ToArray()); console.QueueAnyConfirmResponse(true);
                        action = await runtime.Explorer.TryProcessCommand("/лечить");
                        Assert.Equal(WoundApplicationService.MortalDiagnosisActionText, action); Assert.Single(console.ConfirmPrompts);
                    }
                    AssertMortalDiagnosisImages(before, files);
                }
                using (timings.Measure("MortalDiagnosis.ProcessPlayerTurn"))
                    await runtime.Engine.ProcessPlayerTurn(queued?.Action ?? action!, browserAction: queued);
                using var assertions = timings.Measure("MortalDiagnosis.Assertions");
                Assert.True(responded); Assert.NotNull(turn); Assert.NotNull(heldRequest); Assert.NotNull(signedInput);
                if (failPublication)
                {
                    Assert.Equal(1, cuts); Assert.NotNull(atCut); Assert.NotNull(heldBefore); Assert.NotNull(compensationActual);
                    foreach (var pair in heldBefore!) Assert.Equal(pair.Value, compensationActual![pair.Key]);
                    var cutHistory = ReadMortalDiagnosisHistory(atCut![WoundHistoryState.HistoryPath]);
                    var cutResult = Assert.IsType<MortalWoundDiagnosisPersistedResult>(Assert.Single(cutHistory.Transitions, row => row.Kind == "diagnose").TransitionResult);
                    Assert.Equal(heldRequest!.RequestFingerprint, cutResult.Request.RequestFingerprint);
                    Assert.Equal(turn!.SessionId, cutResult.Binding.SessionId); Assert.Equal(turn.RequestId, cutResult.Binding.RequestId);
                    Assert.Equal(turn.TurnNumber, cutResult.Binding.Turn);
                    AssertMortalDiagnosisAgreement(atCut, cutHistory);
                    Assert.Equal(42, runtime.GameLoop.TurnNumber); AssertMortalDiagnosisImages(before, files);
                    Assert.DoesNotContain(ReadMortalDiagnosisHistory(await files.ReadFileBytesAsync(WoundHistoryState.HistoryPath)).Transitions, row => row.Kind == "diagnose");
                }
                else
                {
                    Assert.Equal(43, runtime.GameLoop.TurnNumber);
                    var images = CaptureMortalDiagnosisImages(files); var history = ReadMortalDiagnosisHistory(images[WoundHistoryState.HistoryPath]);
                    var row = Assert.Single(history.Transitions, value => value.Kind == "diagnose");
                    var result = Assert.IsType<MortalWoundDiagnosisPersistedResult>(row.TransitionResult);
                    Assert.Equal(heldRequest!.RequestFingerprint, result.Request.RequestFingerprint);
                    Assert.Equal(WoundMaterializationContract.SerializeCanonical(original), WoundMaterializationContract.SerializeCanonical(result.Original));
                    var actualCheck = Assert.IsType<MortalWoundProcedureCheckAuthority>(heldRequest.ModeAuthority);
                    Assert.Equal("normal", actualCheck.RollMode); Assert.Equal(3, actualCheck.Modifier);
                    Assert.Equal(15, actualCheck.EffectiveDifficulty); Assert.Equal(0, actualCheck.ComplicationDifficultyModifier);
                    // Independent expected band from the actual signed Engine die, never a replaced die or an extra draw.
                    var success = actualCheck.NaturalRoll == 20 || actualCheck.NaturalRoll != 1 && actualCheck.NaturalRoll + 3 >= 15;
                    Assert.Equal(success ? "success" : "failed_attempt", result.Resolution.ResultCategory);
                    var knowledge = result.KnowledgeAfter;
                    Assert.Equal(success, knowledge.Treatment.KnownRouteIds.Contains("diagnostic_hidden_route"));
                    Assert.Equal(WoundMaterializationContract.SerializeCanonical(original), WoundMaterializationContract.SerializeCanonical(
                        knowledge with { Treatment = original.Treatment, LastTransition = original.LastTransition }));
                    Assert.Equal(1290, result.ToCanonicalJson()["clock"]!["resultingMinute"]!.GetValue<long>());
                    var recovery = Assert.Single(result.Recoveries); Assert.Equal(3, recovery.Resolution.ElapsedCadences);
                    AssertMortalDiagnosisAgreement(images, history);
                    var probe = new WoundHistoryReplayProbe(row.OperationKey, row.WoundId, row.Kind, row.Turn, row.EventRef,
                        row.BeforeFingerprint, row.AfterFingerprint, row.SourceFingerprint, row.AttemptId, null, null, null, null,
                        row.OutputFingerprint, row.ReadableSummary, row.Terminal, result);
                    var replayBefore = CaptureMortalDiagnosisImages(files);
                    Assert.Equal(WoundHistoryReplayDisposition.Exact, history.ResolveReplay(probe).Disposition);
                    AssertMortalDiagnosisImages(replayBefore, files);
                    AssertMortalDiagnosisSingleDeclaredCost(coreBefore, itemIdentityBefore, result,
                        Assert.IsType<string>(await files.ReadFileAsync("game_state/npcs/npc_core.json")),
                        Assert.IsType<string>(await files.ReadFileAsync(MortalItemIdentityState.StatePath)));
                    var journals = JsonNode.Parse((await files.ReadFileAsync("game_state/npcs/npc_journals.json"))!)!.AsObject();
                    var journal = Assert.Single(journals["NPCJournals"]!.AsArray(), value => value!["npcId"]!.GetValue<string>() == "field_medic_01");
                    Assert.Single(journal!["journalEntries"]!.AsArray());
                    Assert.True(JsonNode.DeepEquals(JsonNode.Parse(healthBefore), JsonNode.Parse((await files.ReadFileAsync(ResourceMaterializationContract.StatePath))!)));
                }
                foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath, WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                    LiveTurnPreparationService.PendingTurnSnapshotManifestPath, ProgressionScheduleService.ReportPath, PendingPlayerActionService.PendingPath })
                    Assert.Null(await files.ReadFileBytesAsync(path));
                output.WriteLine("Mortal diagnosis: actual signed die, explicit check/cost/30 minutes, complete knowledge or none, ordinary recovery, exact replay and cleanup.");
            }
            catch (Exception error) { bodyFailure = error; }
            finally
            {
                armed = false;
                try { output.WriteLine(diagnostics.Describe()); }
                catch (Exception error) { bodyFailure ??= error; capture.RecordClosingFailure(error, "mortal-diagnosis-logger-close"); }
                try { using var closing = timings.Measure("MortalDiagnosis.AudioClosing"); await runtime.AudioService.DisposeAsync(); }
                catch (Exception error) { bodyFailure ??= error; capture.RecordClosingFailure(error, "mortal-diagnosis-audio-close"); }
            }
            if (bodyFailure is not null) ExceptionDispatchInfo.Capture(bodyFailure).Throw();
        }
    }

    private static void AssertMortalDiagnosisSingleDeclaredCost(byte[] originalCore, string originalIndex,
        MortalWoundDiagnosisPersistedResult result, string currentCore, string currentIndex)
    {
        var finalized = MortalWoundTreatmentResourceComposer.Finalize(result.Resolution);
        Assert.True(finalized.IsValid, JsonSerializer.Serialize(finalized.Issues));
        var consumption = Assert.Single(finalized.Finalization!.Consumptions);
        Assert.Equal("item_quantity", consumption.Kind); Assert.Equal("sterile_thread", consumption.AuthorityRef);
        Assert.Equal("npc", consumption.OwnerKind); Assert.Equal("field_medic_01", consumption.OwnerId);
        Assert.Equal(1, consumption.Quantity); Assert.Equal(result.Request.RequestFingerprint, finalized.Finalization.RequestFingerprint);
        var expectedCore = JsonNode.Parse(FileSystemManager.DecodeFileBytes(originalCore))!.AsObject();
        var provider = Assert.Single(expectedCore["NPCsInScene"]!.AsArray());
        var item = Assert.Single(provider!["inventory"]!.AsArray(), row => row!["itemId"]!.GetValue<string>() == "sterile_thread");
        Assert.Equal(2, item!["count"]!.GetValue<int>()); item["count"] = 1;
        // Exactly the declared item count changes. Identity, location, skills and
        // every other actor/item property retain their full canonical values.
        Assert.True(JsonNode.DeepEquals(expectedCore, JsonNode.Parse(currentCore)));
        var before = MortalItemIdentityState.Parse(originalIndex); var after = MortalItemIdentityState.Parse(currentIndex);
        Assert.Empty(before.Issues); Assert.Empty(after.Issues);
        var previous = before.EntriesByItemId["sterile_thread"]["transitions"]!.AsArray();
        var transitions = after.EntriesByItemId["sterile_thread"]["transitions"]!.AsArray();
        Assert.Equal(previous.Count + 1, transitions.Count);
        for (var index = 0; index < previous.Count; index++) Assert.True(JsonNode.DeepEquals(previous[index], transitions[index]));
        var spent = transitions[^1]!.AsObject(); Assert.Equal("consume", spent["kind"]!.GetValue<string>());
        Assert.Equal(result.Binding.Turn, spent["turn"]!.GetValue<int>());
        Assert.Equal(2, spent["quantityBefore"]!.GetValue<int>()); Assert.Equal(1, spent["quantityAfter"]!.GetValue<int>());
        Assert.Equal("mortal_wound_treatment", spent["authorityKind"]!.GetValue<string>());
    }

    private static Dictionary<string, byte[]?> CaptureMortalDiagnosisImages(FileSystemManager files) =>
        CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Concat(WoundAcceptedTurnSnapshotContract.RequiredPaths)
            .Append(EffectAcceptedTurnInputComposer.WorldTimePath).Distinct(StringComparer.Ordinal)
            .ToDictionary(path => path, path => File.Exists(files.ResolvePath(path)) ? File.ReadAllBytes(files.ResolvePath(path)) : null, StringComparer.Ordinal);

    private static void AssertMortalDiagnosisImages(IReadOnlyDictionary<string, byte[]?> expected, FileSystemManager files)
    { foreach (var pair in expected) Assert.Equal(pair.Value, File.Exists(files.ResolvePath(pair.Key)) ? File.ReadAllBytes(files.ResolvePath(pair.Key)) : null); }

    private static WoundHistoryState ReadMortalDiagnosisHistory(byte[]? bytes)
    {
        var parsed = WoundHistoryState.Parse(bytes is null ? null : FileSystemManager.DecodeFileBytes(bytes), WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues)); return Assert.IsType<WoundHistoryState>(parsed.State);
    }

    private static void AssertMortalDiagnosisAgreement(IReadOnlyDictionary<string, byte[]?> images, WoundHistoryState history)
    {
        JsonObject? Root(string path) => images[path] is { } bytes ? JsonNode.Parse(FileSystemManager.DecodeFileBytes(bytes))!.AsObject() : null;
        var carriers = WoundCarrierCatalog.Build(new(Root(WoundCarrierCatalog.PlayerPath), Root(WoundCarrierCatalog.NpcPath),
            Root(WoundCarrierCatalog.EnemiesPath), Root(WoundCarrierCatalog.AlliesPath), Root(WoundCarrierCatalog.AfterlifeProfilesPath)));
        var identity = WoundIdentityState.Parse(Root(WoundIdentityState.StatePath)!.ToJsonString(), WoundIdentityState.StatePath);
        Assert.Empty(carriers.Issues); Assert.True(identity.IsValid, JsonSerializer.Serialize(identity.Issues));
        Assert.Empty(history.ValidateAgreement(identity.State!, carriers));
    }
}
