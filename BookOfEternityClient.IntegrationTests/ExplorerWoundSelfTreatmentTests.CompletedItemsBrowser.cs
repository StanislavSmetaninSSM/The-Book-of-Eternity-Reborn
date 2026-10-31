using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Fact]
    public Task BrowserCompletedItemComparisonUsesActualFrameAndWorldProof() => RunCompletedItemsBrowserAsync("exact");

    [Fact]
    public Task BrowserCompletedItemComparisonRefusesGenuineForeignOriginalAndOverride() => RunCompletedItemsBrowserAsync("foreign");

    [Fact]
    public Task BrowserCompletedItemComparisonRefusesDriftBetweenParseAndWorld() => RunCompletedItemsBrowserAsync("between_parse_and_world");

    [Fact]
    public Task BrowserCompletedItemComparisonWithholdsResultWhenFinalProofFails() => RunCompletedItemsBrowserAsync("after_comparison");

    private async Task RunCompletedItemsBrowserAsync(string fault)
    {
        var diagnosticRoot = Path.GetFullPath(TestDiagnosticPaths.Root);
        var repo = Path.GetFullPath(TestRepoPaths.RepoRoot).TrimEnd(Path.DirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (diagnosticRoot.Equals(repo, comparison) || diagnosticRoot.StartsWith(repo + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException("Browser component capture requires a diagnostic root outside checkout.");
        var capture = new LifecyclePassiveTimingDiagnostics("MortalDiagnosis-browser-component-" + fault, output, gzipRecords: true);
        var timings = new OperationTimingAccumulator();
        var window = timings.StartWindow(captureEvents: true);
        var root = Path.Combine(Path.GetTempPath(), "boe-completed-item-browser-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); capture.BindFixtureRoot(root); capture.Arm(window);
        Exception? primary = null;
        var journey = timings.Measure("MortalDiagnosisBrowserComponent.IncludingClosing");
        try { await RunCompletedItemsBrowserCoreAsync(fault, root, timings, capture); }
        catch (Exception error) { primary = error; }
        finally
        {
            using (timings.Measure("MortalDiagnosisBrowserComponent.FixtureClosing"))
            {
                try { Directory.Delete(root, true); }
                catch (Exception error) { primary ??= error; capture.RecordClosingFailure(error, "completed-item-browser-fixture-close"); }
            }
            journey?.Dispose();
            await capture.CompleteAsync(primary);
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
    }

    private async Task RunCompletedItemsBrowserCoreAsync(string fault, string root,
        OperationTimingAccumulator timings, LifecyclePassiveTimingDiagnostics capture)
    {
        FileSystemManager files = null!;
        ValidationService.CompletedTreatmentItemValidationScope? scope = null;
        byte[]? publishedNpc = null;
        var armed = false; var cuts = 0; var checkpoints = 0;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = fault is "between_parse_and_world" or "after_comparison" ? path =>
            {
                if (!armed || path != NpcCoreChangesContract.NpcCorePath || scope is null || cuts != 0) return Task.CompletedTask;
                var cut = fault == "between_parse_and_world" ? scope.Observed && !scope.Invalidated :
                    fault == "after_comparison" && scope.Invalidated;
                if (cut)
                {
                    cuts++;
                    File.WriteAllBytes(files!.ResolvePath(path), publishedNpc!.Append((byte)' ').ToArray());
                }
                return Task.CompletedTask;
            } : null
        };
        var stop = new OperationCanceledException("Controlled stop after genuine browser post-validator proof, before GM wait.");
        GameEngineTestRuntime runtime = null!;
        Exception? bodyFailure = null;
        try
        {
            files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                BookOfEternityClient.Core.PhysicalLoadTransactionOperations.Instance, hooks);
            using (timings.Measure("MortalDiagnosisBrowserComponent.FixturePreparation"))
                await SeedMortalDiagnosisJourneyAsync(files);
            using (timings.Measure("MortalDiagnosisBrowserComponent.RuntimeConstruction"))
            runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files, configureSettings: settings =>
            {
                settings.GmBridgeAutoStart = false; settings.MusicEnabled = false; settings.SoundEnabled = false;
                settings.AutosaveIntervalTurns = 0; settings.EnableQteEvents = false;
            }, logger: new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger(capture),
            finalizationHooks: new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = async checkpoint =>
                {
                    if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted) return;
                    Assert.Equal(1, ++checkpoints);
                    using var component = timings.Measure("MortalDiagnosisBrowserComponent.PreparedComponent");
                    TurnRequest turn;
                    GameEngine.PendingTurnSnapshotManifest manifest;
                    PendingPlayerActionService.Staged staged;
                    var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
                    using (timings.Measure("MortalDiagnosisBrowserComponent.ReadStaged"))
                    await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                    {
                        var requestJson = Assert.IsType<string>(await files.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath));
                        var manifestJson = Assert.IsType<string>(await files.ReadFileAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
                        turn = JsonSerializer.Deserialize<TurnRequest>(requestJson, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                        manifest = JsonSerializer.Deserialize<GameEngine.PendingTurnSnapshotManifest>(manifestJson, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                        Assert.NotNull(manifest.BrowserOriginalMainCondition);
                        var pending = Assert.IsType<PendingPlayerActionService.State>(await PendingPlayerActionService.ReadAsync(files, lease));
                        Assert.Equal("staged", pending.Phase);
                        staged = PendingPlayerActionService.ReadStaged(pending);
                        Assert.Equal(requestJson, staged.RequestJson); Assert.Equal(manifestJson, staged.ManifestJson);
                        Assert.Equal(turn.RequestId, staged.Binding.ActionId);
                        Assert.Equal(manifest.ManifestPayloadHash, GameEngine.ValidateDetachedBrowserBinding(staged).ManifestPayloadHash);
                    }
                    // The actual wait callback is an idle inspection, not terminal processing.
                    // Reuse the admitted component pattern under the same live owner and exact real staged DATA.
                    using var processing = files.BeginBrowserOriginalOperation(staged.Binding, staged);
                    using (timings.Measure("MortalDiagnosisBrowserComponent.EnterProcessing"))
                        files.BeginBrowserOriginalProcessing(staged);
                    using (timings.Measure("MortalDiagnosisBrowserComponent.PrepareCommon"))
                    await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                    {
                        var pending = Assert.IsType<PendingPlayerActionService.State>(await PendingPlayerActionService.ReadAsync(files, lease));
                        Assert.Equal("staged", pending.Phase); Assert.Equal(staged.Json, pending.Json);
                        var admitted = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(files, lease,
                            new(turn.SessionId, turn.RequestId, turn.TurnNumber, turn.ProgressionControl!), requireRetained: true);
                        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(lease));
                        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease));
                        var common = await WoundAcceptedTurnPlanner.ComposeMortalDiagnosisPublicationAsync(files, lease,
                            new GameResponse(), admitted.Owner, admitted.Request);
                        Assert.True(common.IsValid, JsonSerializer.Serialize(common.Issues));
                    }
                    AcceptedTurnCanonicalStateRefresh.Result published;
                    using (timings.Measure("MortalDiagnosisBrowserComponent.CommonPublication"))
                    published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(files,
                        new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance), validator,
                        new Dictionary<string, string>(StringComparer.Ordinal));
                    Assert.DoesNotContain(published.Issues, issue => issue.Severity == IssueSeverity.Error);
                    var tx = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction>(published.TreatmentResourcePublicationTransaction);
                    await using var cleanup = (IAsyncDisposable)tx;
                    MortalWoundTreatmentPublicationOperationResult advanced;
                    using (timings.Measure("MortalDiagnosisBrowserComponent.ProgressionAgreement"))
                    advanced = await tx.AdvancePublishedAgreementWithProgressionAsync(files,
                        new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance), turn.ProgressionControl!);
                    Assert.True(advanced.IsValid, JsonSerializer.Serialize(advanced.Issues));
                    MortalWoundTreatmentPublicationOperationResult finalized;
                    using (timings.Measure("MortalDiagnosisBrowserComponent.Complete"))
                        finalized = await tx.CompleteAsync(files);
                    Assert.True(finalized.IsValid, JsonSerializer.Serialize(finalized.Issues));
                    Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.Finalized, finalized.Outcome);
                    var completed = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction.CompletedItemValidation>(tx.ReadCompletedItemValidation());
                    var exactPublishedNpc = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(NpcCoreChangesContract.NpcCorePath));
                    publishedNpc = exactPublishedNpc;
                    var before = CaptureMortalDiagnosisImages(files);
                    using var original = validator.UsePrevalidatedPendingTurnSnapshotScope(manifest);
                    if (fault == "foreign")
                    {
                        MortalWoundTreatmentResolverTests.DiagnosisOwnershipFixture foreign;
                        using (timings.Measure("MortalDiagnosisBrowserComponent.ForeignPreparation"))
                            foreign = await MortalWoundTreatmentResolverTests.DiagnosisOwnershipFixture.CreateAsync(20,
                                signedInput: true, duration: 0);
                        await using var foreignClosing = foreign;
                        var foreignManifest = JsonSerializer.Deserialize<GameEngine.PendingTurnSnapshotManifest>(Assert.IsType<string>(
                            await foreign.Files.ReadFileAsync(foreign.Lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath)),
                            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                        var foreignRead = PendingTurnSnapshotReader.ReadCurrent(foreign.Files, foreign.Lease,
                            new[] { EffectAcceptedTurnInputComposer.WorldTimePath });
                        Assert.True(foreignRead.Success, JsonSerializer.Serialize(foreignRead.Issues));
                        var foreignOriginal = Assert.IsType<PendingTurnSnapshotReadAuthority>(foreignRead.Snapshot);
                        Assert.NotEqual(manifest.ManifestPayloadHash, foreignOriginal.SnapshotToken);
                        await foreign.ReleaseLeaseAsync();
                        var actualRead = Assert.IsType<FileSystemManager.WorldValidationReadScope>(files.BeginWorldValidationRead());
                        using (timings.Measure("MortalDiagnosisBrowserComponent.ForeignFrameProof"))
                        await using (actualRead)
                        {
                            await actualRead.OpenAsync();
                            Assert.True(actualRead.MatchesOriginalFrame(manifest.SessionId, manifest.RequestId, manifest.TurnNumber, manifest.ManifestPayloadHash));
                            Assert.False(actualRead.MatchesOriginalFrame(foreignOriginal.SessionId, foreignOriginal.RequestId,
                                foreignOriginal.TurnNumber, foreignOriginal.SnapshotToken));
                            Assert.Equal(publishedNpc, await files.ReadFileBytesAsync(actualRead.Lease!, NpcCoreChangesContract.NpcCorePath));
                            actualRead.RequireFullEndProof();
                        }
                        using (validator.UsePrevalidatedPendingTurnSnapshotScope(foreignManifest))
                        {
                            List<ValidationIssue> foreignRaw;
                            using (timings.Measure("MortalDiagnosisBrowserComponent.ForeignRawPostValidation"))
                                foreignRaw = await validator.ValidatePostMaterializedGameStateAsync();
                            Assert.Single(foreignRaw, MortalWoundTreatmentResolverTests.IsCompletedItemInventoryIssue);
                            using var foreignScope = validator.UseCompletedTreatmentItemValidationScope(completed);
                            List<ValidationIssue> foreignActual;
                            using (timings.Measure("MortalDiagnosisBrowserComponent.ForeignScopedPostValidation"))
                                foreignActual = await validator.ValidatePostMaterializedGameStateAsync();
                            Assert.True(foreignScope.Observed);
                            Assert.True(foreignScope.Invalidated);
                            Assert.Equal(JsonSerializer.Serialize(foreignRaw), JsonSerializer.Serialize(foreignActual));
                            Assert.Equal(publishedNpc, await files.ReadFileBytesAsync(NpcCoreChangesContract.NpcCorePath));
                        }
                        AssertMortalDiagnosisImages(before, files);
                        throw stop;
                    }
                    List<ValidationIssue> raw;
                    using (timings.Measure("MortalDiagnosisBrowserComponent.RawPostValidation"))
                        raw = await validator.ValidatePostMaterializedGameStateAsync();
                    var exact = Assert.Single(raw, MortalWoundTreatmentResolverTests.IsCompletedItemInventoryIssue);
                    if (fault == "exact")
                    {
                        // Without World selection the actual guard remains, even inside a real processing component.
                        using (timings.Measure("MortalDiagnosisBrowserComponent.NoWorldValidation"))
                        using (validator.UseCompletedTreatmentItemValidationScope(completed))
                            Assert.Contains(await validator.ValidateGameStateAsync(new GameStateValidationSelection(GameStateValidationPhase.NpcStateFiles)),
                                MortalWoundTreatmentResolverTests.IsCompletedItemInventoryIssue);
                    }
                    using (scope = validator.UseCompletedTreatmentItemValidationScope(completed))
                    {
                        List<ValidationIssue>? actual = null;
                        armed = true;
                        Exception? failure;
                        try
                        {
                            using (timings.Measure("MortalDiagnosisBrowserComponent.ScopedPostValidation"))
                                failure = await Record.ExceptionAsync(async () => actual = await validator.ValidatePostMaterializedGameStateAsync());
                        }
                        finally { armed = false; File.WriteAllBytes(files.ResolvePath(NpcCoreChangesContract.NpcCorePath), exactPublishedNpc); }
                        if (fault == "after_comparison")
                        {
                            Assert.Equal(1, cuts);
                            Assert.True(scope.Invalidated);
                            Assert.Null(actual);
                            Assert.IsAssignableFrom<IOException>(failure);
                        }
                        else
                        {
                            Assert.Null(failure);
                            Assert.NotNull(actual);
                            if (fault == "exact")
                            {
                                Assert.Equal(0, cuts);
                                Assert.Equal(JsonSerializer.Serialize(raw.Where(issue => !ReferenceEquals(issue, exact))), JsonSerializer.Serialize(actual));
                            }
                            else
                            {
                                Assert.Equal(1, cuts);
                                Assert.Contains(actual!, MortalWoundTreatmentResolverTests.IsCompletedItemInventoryIssue);
                            }
                        }
                    }
                    AssertMortalDiagnosisImages(before, files);
                    throw stop;
                }
            });
            runtime.GameLoop.SetSession("session_completed_item_browser", 42);
            using (timings.Measure("MortalDiagnosisBrowserComponent.Refresh"))
                await runtime.StateManager.RefreshGameStateAsync();
            await SessionOperationContext.RunParticipatingCurrentSessionAsync(files, async () =>
            {
                var opened = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
                var menu = Assert.IsType<WoundCommandState>(opened.WoundCommandState);
                if (menu.IsTargetSelection) menu = menu.ForTarget(Assert.Single(menu.Targets, target => target.Owner.OwnerKind == "player"));
                var choice = Assert.Single(menu.Choices, item => item.Available && item.Selection.MortalDiagnosis?.Provider.OwnerId == "field_medic_01");
                PendingPlayerActionService.Binding binding;
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                {
                    var queued = PendingPlayerActionService.PrepareQueued(files, lease, WoundApplicationService.MortalDiagnosisActionText,
                        "browser-wound-action", DateTime.UtcNow.ToString("O"), choice.Selection).ToJsonString();
                    await files.WriteFileAtomicAsync(lease, PendingPlayerActionService.PendingPath, queued);
                    binding = PendingPlayerActionService.Parse(queued, files.GetOrCreateSessionGeneration(lease)).Binding;
                }
                using (timings.Measure("MortalDiagnosisBrowserComponent.EngineStageAndComponent"))
                    Assert.Same(stop, await Record.ExceptionAsync(() => runtime.Engine.ProcessPlayerTurn(binding.Action, browserAction: binding)));
                Assert.Equal(1, checkpoints);
            });
        }
        catch (Exception error) { bodyFailure = error; throw; }
        finally
        {
            using (timings.Measure("MortalDiagnosisBrowserComponent.AudioClosing"))
            {
                try { if (runtime is not null) await runtime.AudioService.DisposeAsync(); }
                catch (Exception error)
                {
                    capture.RecordClosingFailure(error, "completed-item-browser-audio-close");
                    if (bodyFailure is null) throw;
                    try { bodyFailure.Data["AudioClosingFailure"] = error; } catch { }
                }
            }
        }
    }
}
