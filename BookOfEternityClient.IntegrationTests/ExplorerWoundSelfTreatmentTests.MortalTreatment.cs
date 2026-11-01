using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Theory]
    [InlineData("procedure")]
    [InlineData("guaranteed")]
    public Task ConsolePhysicalTreatmentUsesTheActualConfirmedModeAndSettlement(string mode) => RunMortalTreatmentJourneyAsync(mode, false, false);
    [Fact]
    public Task BrowserPhysicalProcedureUsesTheActualConfirmedModeAndSettlement() => RunMortalTreatmentJourneyAsync("procedure", true, false);
    [Fact]
    public Task BrowserPhysicalGuaranteedUsesTheActualConfirmedModeAndSettlement() => RunMortalTreatmentJourneyAsync("guaranteed", true, false);
    [Fact] public Task ConsolePhysicalCourseSeparatelyConfirmsEveryMilestoneAfterAcceptedWaits() => RunMortalTreatmentJourneyAsync("course", false, false);
    [Fact] public Task BrowserPhysicalCourseSeparatelyConfirmsEveryMilestoneAfterAcceptedWaits() => RunMortalTreatmentJourneyAsync("course", true, false);
    [Theory]
    [InlineData("procedure")]
    [InlineData("course")]
    [InlineData("guaranteed")]
    public Task PhysicalTreatmentPublicationCutRestoresHeldAndWholeOriginalImages(string mode) => RunMortalTreatmentJourneyAsync(mode, false, true);

    private async Task RunMortalTreatmentJourneyAsync(string mode, bool browser, bool failPublication)
    {
        var diagnosticRoot = Path.GetFullPath(TestDiagnosticPaths.Root);
        var repo = Path.GetFullPath(TestRepoPaths.RepoRoot).TrimEnd(Path.DirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (diagnosticRoot.Equals(repo, comparison) || diagnosticRoot.StartsWith(repo + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException("Physical treatment capture requires the existing external diagnostic root.");
        var capture = new LifecyclePassiveTimingDiagnostics("MortalTreatment-" + mode + "-" +
            (failPublication ? "rollback" : browser ? "browser" : "console"), output, gzipRecords: true);
        var timings = new OperationTimingAccumulator(); var window = timings.StartWindow(captureEvents: true);
        var root = Path.Combine(Path.GetTempPath(), "boe-mortal-physical-journey-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); capture.BindFixtureRoot(root); capture.Arm(window);
        Exception? primary = null; var journey = timings.Measure("MortalTreatment.JourneyIncludingClosing");
        try { await CoreAsync(); }
        catch (Exception error) { primary = error; }
        finally
        {
            using (timings.Measure("MortalTreatment.FixtureClosing"))
            {
                try { Directory.Delete(root, true); }
                catch (Exception error) { primary ??= error; capture.RecordClosingFailure(error, "mortal-treatment-fixture-close"); }
            }
            journey?.Dispose(); await capture.CompleteAsync(primary);
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();

        async Task CoreAsync()
        {
            FileSystemManager? owned = null; RepairPlayerInput? repairInput = null;
            var armed = false; var cuts = 0; var responded = false; int? waitMinutes = null;
            Dictionary<string, byte[]?>? heldBefore = null, compensation = null, atCut = null;
            var hooks = new FileSystemManagerHooks
            {
                PassiveOperationTimings = browser ? timings : null,
                LocalPublicationObserver = !failPublication ? null : (phase, _) =>
                {
                    if (!armed || phase != TrustedLocalPublicationPhase.MemberPublished ||
                        File.Exists(owned!.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath))) return;
                    armed = false; cuts++; atCut = CaptureMortalDiagnosisImages(owned!); repairInput!.RequestEscape();
                    throw new InvalidOperationException("Controlled physical treatment publication cut after actual selection consumption.");
                },
                BeforeCanonicalReadOpenAsync = !failPublication ? null : path =>
                {
                    if (cuts == 1 && compensation is null && path == CanonicalStateNormalizer.NormalizerRollbackTrackedFiles[0])
                        compensation = CaptureMortalDiagnosisImages(owned!);
                    return Task.CompletedTask;
                }
            };
            var files = owned = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);
            WoundMaterializationEnvelope initial;
            using (timings.Measure("MortalTreatment.FixturePreparation"))
                initial = await SeedMortalWoundJourneyAsync(files, (current, prepare) =>
                    MortalWoundTreatmentResolverTests.SeedGuidedPhysicalInitialWorld(current, mode, prepare));
            var console = new TestExplorerConsole(); var logger = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger(capture);
            var input = repairInput = new RepairPlayerInput(logger);
            TurnRequest? actualTurn = null; MortalWoundTreatmentAttemptRequest? held = null;
            var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files, inputSource: input,
                configureSettings: settings => { settings.GmBridgeAutoStart = false; settings.MusicEnabled = false;
                    settings.SoundEnabled = false; settings.AutosaveIntervalTurns = 0; settings.EnableQteEvents = false; },
                logger: logger, explorerConsole: console, finalizationHooks: new GameEngineSessionFinalizationHooks
                {
                    AtCheckpointAsync = async checkpoint =>
                    {
                        if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted || responded) return;
                        using var response = timings.Measure("MortalTreatment.GmResponse");
                        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                        actualTurn = JsonSerializer.Deserialize<TurnRequest>(Assert.IsType<string>(await files.ReadFileAsync(lease,
                            LiveTurnPreparationService.TurnRequestPath)), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                        Assert.Null(actualTurn.MortalDiagnosis); Assert.Null(actualTurn.SpiritualHealing);
                        if (waitMinutes is null)
                        {
                            Assert.NotNull(actualTurn.MortalTreatment);
                            var source = JsonNode.Parse(Assert.IsType<string>(await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath)))!.AsObject();
                            var frame = new SpiritualSelfTreatmentStageContext(actualTurn.SessionId, actualTurn.RequestId, actualTurn.TurnNumber, actualTurn.ProgressionControl!);
                            Assert.True(MortalWoundGuidedTreatmentInputCodec.IsBound(source, frame));
                            var admission = await WoundApplicationService.ResolveMortalTreatmentInputAsync(files, lease, frame, requireRetained: true);
                            held = admission.Request; Assert.Equal(mode, held.Mode); Assert.Equal("treatment", held.Coordinates.Purpose);
                            if (held.ModeAuthority is MortalWoundProcedureCheckAuthority check)
                            {
                                Assert.Single(check.SourceIndices); Assert.Equal(check.NaturalRoll, Assert.Single(check.SourceRolls));
                                Assert.Equal(check.NaturalRoll, actualTurn.PreGeneratedDices1d20[Assert.Single(check.SourceIndices)]);
                            }
                            var retained = CaptureMortalDiagnosisImages(files);
                            var repeated = await WoundApplicationService.ResolveMortalTreatmentInputAsync(files, lease, frame, requireRetained: true);
                            Assert.Equal(WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(held)!.ToJsonString(),
                                WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(repeated.Request)!.ToJsonString());
                            AssertMortalDiagnosisImages(retained, files);
                        }
                        else { Assert.Null(actualTurn.MortalTreatment); Assert.Null(await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath)); }
                        await WriteMortalTreatmentJourneyResponseAsync(files, lease, actualTurn, waitMinutes);
                        heldBefore = CaptureMortalDiagnosisImages(files); responded = true; armed = failPublication && waitMinutes is null;
                    }
                });
            Exception? failure = null;
            try
            {
                runtime.GameLoop.SetSession("session_physical_" + mode + "_" + (browser ? "browser" : failPublication ? "rollback" : "console"), 42);
                await runtime.StateManager.RefreshGameStateAsync();
                var milestone = 0; string? activeCourse = null;
                while (true)
                {
                    responded = false; actualTurn = null; held = null; waitMinutes = null;
                    var originalImages = CaptureMortalDiagnosisImages(files);
                    var beforeItems = await CapturePhysicalItemCountsAsync(files);
                    var nextAction = await SelectPhysicalJourneyActionAsync(files, runtime, console, mode, browser, originalImages);
                    var beforeTurn = runtime.GameLoop.TurnNumber;
                    using (timings.Measure("MortalTreatment.ProcessConfirmedAttempt"))
                        await runtime.Engine.ProcessPlayerTurn(nextAction.Binding?.Action ?? nextAction.Action!, browserAction: nextAction.Binding);
                    Assert.True(responded); Assert.NotNull(actualTurn); Assert.NotNull(held);
                    if (failPublication)
                    {
                        Assert.Equal(1, cuts); Assert.NotNull(atCut); Assert.NotNull(heldBefore); Assert.NotNull(compensation);
                        foreach (var image in heldBefore!) Assert.True(image.Value is { } value ? compensation![image.Key] is { } restored &&
                            value.AsSpan().SequenceEqual(restored) : compensation![image.Key] is null, image.Key);
                        var cutHistory = ReadMortalDiagnosisHistory(atCut![WoundHistoryState.HistoryPath]);
                        Assert.Equal(held!.RequestFingerprint, Assert.Single(cutHistory.Transitions, entry => entry.Kind == "treat").TreatmentResult!.Request.RequestFingerprint);
                        AssertMortalDiagnosisAgreement(atCut, cutHistory);
                        if (mode == "guaranteed")
                        {
                            var terminal = Assert.Single(cutHistory.Transitions, entry => entry.Kind == "heal");
                            Assert.True(terminal.Terminal);
                            Assert.Equal(held.RequestFingerprint, cutHistory.Transitions.Single(entry => entry.Kind == "treat").SourceFingerprint);
                        }
                        Assert.Equal(beforeTurn, runtime.GameLoop.TurnNumber); AssertMortalDiagnosisImages(originalImages, files);
                        break;
                    }
                    Assert.Equal(beforeTurn + 1, runtime.GameLoop.TurnNumber);
                    var images = CaptureMortalDiagnosisImages(files);
                    var history = ReadMortalDiagnosisHistory(images[WoundHistoryState.HistoryPath]);
                    AssertMortalDiagnosisAgreement(images, history);
                    var row = Assert.Single(history.Transitions, entry => entry.Kind == "treat" && entry.Turn == actualTurn!.TurnNumber);
                    var result = Assert.IsType<MortalWoundTreatmentPersistedResult>(row.TransitionResult);
                    Assert.True(result.HasGuidedPublicationWitness); Assert.Equal(held!.RequestFingerprint, result.Request.RequestFingerprint);
                    var current = result.ReconstructGuidedPublication();
                    var replay = new WoundHistoryParseResult(history, []).ProbeTreatmentAttempt(result.Request.Coordinates.OperationKey,
                        result.Request.Coordinates.AttemptId, result.Request.RequestFingerprint);
                    Assert.Empty(replay.Issues);
                    Assert.Equal("ExactReplay", replay.Status);
                    Assert.Equal(mode, result.Receipt.Mode);
                    await AssertPhysicalDeclaredItemsExactlyOnceAsync(files, beforeItems, result, current.Finalization);
                    AssertPhysicalCleanup(files);
                    if (current.Preparation.PreparesTerminalHeal)
                    {
                        var healed = Assert.Single(history.Transitions, entry => entry.Kind == "heal" && entry.Turn == actualTurn.TurnNumber);
                        Assert.Equal(row.AfterFingerprint, healed.BeforeFingerprint);
                        Assert.Equal(row.Ordinal + 1, healed.Ordinal);
                        Assert.True(healed.Terminal);
                        Assert.DoesNotContain(JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(images[WoundCarrierCatalog.PlayerPath]!))!
                            ["activeWounds"]!.AsArray().OfType<JsonObject>(), wound => wound["woundId"]!.GetValue<string>() == initial.WoundId);
                    }
                    if (mode != "course") break;
                    milestone++;
                    Assert.Equal(milestone, result.Receipt.CourseMilestoneOrdinal);
                    activeCourse ??= result.Receipt.CourseId;
                    Assert.Equal(activeCourse, result.Receipt.CourseId);
                    if (result.Receipt.CourseDisposition == "completed") { Assert.Equal(3, milestone); break; }
                    Assert.Equal("active", result.Receipt.CourseDisposition);
                    var opened = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
                    var menu = Assert.IsType<WoundCommandState>(opened.WoundCommandState);
                    if (menu.IsTargetSelection) menu = menu.ForTarget(Assert.Single(menu.Targets, target => target.Owner.OwnerKind == "player"));
                    var tooEarly = Assert.Single(menu.Choices, choice => choice.Selection.MortalTreatment?.Mode == "course" &&
                        choice.Selection.MortalTreatment.Provider.OwnerId == "field_medic_01");
                    Assert.False(tooEarly.Available); Assert.Contains("Ещё рано", tooEarly.Reason);
                    await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                    {
                        var context = Assert.IsType<WoundPlayerReadContext>(await WoundPlayerReadService.LoadContextAsync(files, lease, retainMenuInputs: true));
                        var wound = Assert.Single(context.Catalog.Occurrences, occurrence => occurrence.WoundId == initial.WoundId).Wound;
                        var typed = MortalWoundTreatmentContract.ParseProjection(wound.Treatment, "courseJourney", wound.Owner.Realm,
                            wound.Owner.OwnerKind, wound.Severity.Rank, wound.Complications, wound.Recovery.DeteriorationPolicy);
                        Assert.True(typed.IsValid, JsonSerializer.Serialize(typed.Issues));
                        var course = Assert.IsType<MortalWoundCourseRouteDefinition>(Assert.Single(typed.Treatment!.Routes));
                        var minute = JsonNode.Parse(Assert.IsType<string>(await files.ReadFileAsync(lease, EffectAcceptedTurnInputComposer.WorldTimePath)))!["currentTimeInMinutes"]!.GetValue<long>();
                        var next = MortalWoundCourseModeAuthority.DescribeCurrentWindow(wound, context.Input.History, course, minute);
                        Assert.Equal("TooEarly", next.Status); Assert.Equal(milestone + 1, next.Ordinal);
                        waitMinutes = checked((int)(next.Due - minute)); Assert.True(waitMinutes > 0);
                    }
                    var waitState = CaptureMortalDiagnosisImages(files);
                    var woundBeforeWait = waitState[WoundCarrierCatalog.PlayerPath];
                    responded = false; var waitTurn = runtime.GameLoop.TurnNumber;
                    using (timings.Measure("MortalTreatment.OrdinaryAcceptedWait"))
                        await runtime.Engine.ProcessPlayerTurn("Дождаться следующего согласованного этапа лечения, спокойно отдыхая в клинике.");
                    Assert.True(responded); Assert.Equal(waitTurn + 1, runtime.GameLoop.TurnNumber);
                    Assert.Equal(woundBeforeWait, await files.ReadFileBytesAsync(WoundCarrierCatalog.PlayerPath));
                    AssertPhysicalCleanup(files);
                    // A newly opened menu and a separate confirmation reconstruct
                    // the next milestone; no clock or canonical file is patched.
                    await runtime.StateManager.RefreshGameStateAsync();
                }
            }
            catch (Exception error) { failure = error; }
            finally
            {
                armed = false;
                try { output.WriteLine(logger.Describe()); }
                catch (Exception error) { failure ??= error; capture.RecordClosingFailure(error, "physical-logger-close"); }
                try { using var close = timings.Measure("MortalTreatment.AudioClosing"); await runtime.AudioService.DisposeAsync(); }
                catch (Exception error) { failure ??= error; capture.RecordClosingFailure(error, "physical-audio-close"); }
            }
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static void AssertPhysicalCleanup(FileSystemManager files)
    {
        foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath, WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                     LiveTurnPreparationService.PendingTurnSnapshotManifestPath, ProgressionScheduleService.ReportPath, PendingPlayerActionService.PendingPath })
            Assert.False(File.Exists(files.ResolvePath(path)), path);
    }
}
