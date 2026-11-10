using System.Runtime.ExceptionServices;
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

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Fact]
    public async Task MortalDiagnosisEngineOriginalKeepsPreparedScheduleSeparateFromExactRollback()
    {
        var diagnosticRoot = Path.GetFullPath(TestDiagnosticPaths.Root);
        var repo = Path.GetFullPath(TestRepoPaths.RepoRoot).TrimEnd(Path.DirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (diagnosticRoot.Equals(repo, comparison) || diagnosticRoot.StartsWith(repo + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException("Mortal diagnosis capture requires a diagnostic root outside checkout.");
        var capture = new LifecyclePassiveTimingDiagnostics("MortalDiagnosis-prepared-original", output, gzipRecords: true);
        var timings = new OperationTimingAccumulator();
        var window = timings.StartWindow(captureEvents: true);
        var root = Path.Combine(Path.GetTempPath(), "boe-mortal-diagnosis-original-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        capture.BindFixtureRoot(root);
        capture.Arm(window);
        Exception? primary = null;
        var journey = timings.Measure("MortalDiagnosisOriginal.IncludingClosing");
        try { await CoreAsync(); }
        catch (Exception error) { primary = error; }
        finally
        {
            using (timings.Measure("MortalDiagnosisOriginal.FixtureClosing"))
            {
                try { Directory.Delete(root, true); }
                catch (Exception error) { primary ??= error; capture.RecordClosingFailure(error, "mortal-diagnosis-original-close"); }
            }
            journey?.Dispose();
            await capture.CompleteAsync(primary);
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();

        async Task CoreAsync()
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            using (timings.Measure("MortalDiagnosisOriginal.FixturePreparation"))
                await SeedMortalDiagnosisJourneyAsync(files);
            var console = new TestExplorerConsole();
            var logger = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger(capture);
            var stop = new OperationCanceledException("Controlled stop after genuine Mortal diagnosis original, before GM wait.");
            GameEngine.RollbackSnapshot? rollback = null;
            Dictionary<string, byte[]?>? original = null;
            string? generation = null;
            var checkpoints = 0;
            GameEngineTestRuntime runtime = null!;
            runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files,
                configureSettings: settings =>
                {
                    settings.GmBridgeAutoStart = false; settings.MusicEnabled = false;
                    settings.SoundEnabled = false; settings.AutosaveIntervalTurns = 0; settings.EnableQteEvents = false;
                },
                logger: logger, explorerConsole: console,
                finalizationHooks: new GameEngineSessionFinalizationHooks
                {
                    AtCheckpointAsync = async checkpoint =>
                    {
                        if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted) return;
                        Assert.Equal(1, ++checkpoints);
                        using var inspection = timings.Measure("MortalDiagnosisOriginal.PreparedOriginalAndDriftGuards");
                        SpiritualSelfTreatmentStageContext? retryFrame = null;
                        MortalWoundTreatmentAttemptRequest? checkpointRequest = null;
                        string? signedSchedulePath = null;
                        byte[]? signedScheduleBytes = null;
                        byte[]? outerScheduleBytes = null;
                        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                        {
                        generation = files.GetOrCreateSessionGeneration(lease);
                        var turn = Assert.IsType<TurnRequest>(JsonSerializer.Deserialize<TurnRequest>(
                            Assert.IsType<string>(await files.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath)),
                            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                        Assert.NotNull(turn.MortalDiagnosis);
                        var manifest = Assert.IsType<LiveTurnPendingSnapshotManifest>(JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
                            Assert.IsType<string>(await files.ReadFileAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath)),
                            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                        var authorityText = Assert.IsType<string>(await files.ReadFileAsync(lease, PendingTurnSnapshotAuthority.AuthorityPath));
                        Assert.True(PendingTurnSnapshotAuthority.TryValidateManifestAgainstAuthority(manifest, authorityText,
                            LiveTurnPreparationService.ManifestHashJsonOptions,
                            value => value.ManifestPayloadHash, (value, hash) => value.ManifestPayloadHash = hash,
                            value => value.SessionId, value => value.RequestId, value => value.TurnNumber,
                            value => value.Files, value => value.SnapshotFileHashes, value => value.ClientOwnedValidationHashes,
                            value => value.RollbackBaselineFiles, value => value.SourceLabel, value => value.RollbackBackups,
                            path => files.ReadFileBytesSync(lease, path), out var payload, out var failureCode,
                            requireRollbackArtifactPaths: true), failureCode);
                        Assert.NotNull(payload);
                        Assert.Equal(turn.SessionId, payload.SessionId);
                        Assert.Equal(turn.RequestId, payload.RequestId);
                        Assert.Equal(turn.TurnNumber, payload.TurnNumber);
                        Assert.Equal(PendingTurnSnapshotAuthority.ExactSnapshotHashMode, payload.SnapshotHashMode);
                        Assert.Equal(PendingTurnSnapshotAuthority.ExactRollbackHashMode, payload.RollbackHashMode);

                        var schedulePath = ProgressionScheduleService.SchedulePath;
                        var clockPath = EffectAcceptedTurnInputComposer.WorldTimePath;
                        var preparedSchedule = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease, schedulePath));
                        var preparedClock = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease, clockPath));
                        var signedSchedule = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease, payload.Files[schedulePath]));
                        var rollbackSchedule = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease, payload.RollbackBackups[schedulePath]));
                        Assert.Equal(preparedSchedule, signedSchedule);
                        Assert.Equal(PendingTurnSnapshotAuthority.ComputeSha256(preparedSchedule), payload.SnapshotFileHashes[schedulePath], ignoreCase: true);
                        Assert.NotNull(original);
                        Assert.Equal(original[schedulePath], rollbackSchedule);
                        Assert.False(preparedSchedule.AsSpan().SequenceEqual(rollbackSchedule));
                        Assert.Equal(PendingTurnSnapshotAuthority.ComputeSha256(rollbackSchedule), payload.RollbackBackupHashes[schedulePath], ignoreCase: true);
                        Assert.Contains(schedulePath, payload.RollbackBaselineFiles);
                        Assert.Equal(original[clockPath], preparedClock);
                        Assert.Equal(preparedClock, await files.ReadFileBytesAsync(lease, payload.Files[clockPath]));

                        // Copy only actual authenticated producer DATA, including its
                        // technical markers. The restore owner still verifies every
                        // backup hash/byte and the actual session generation.
                        rollback = new GameEngine.RollbackSnapshot
                        {
                            TechnicalArtifacts = payload.Files.Keys.Where(path => ConsoleLocalTurnRollbackArtifacts.IsMarker(path) &&
                                payload.SnapshotFileHashes.ContainsKey(path)).ToHashSet(StringComparer.OrdinalIgnoreCase),
                            BackupFiles = payload.RollbackBackups.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                            BackupHashes = payload.RollbackBackupHashes.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                            BackupHashesAreExactBytes = payload.RollbackHashMode == PendingTurnSnapshotAuthority.ExactRollbackHashMode,
                            BaselineFiles = new HashSet<string>(payload.RollbackBaselineFiles, StringComparer.OrdinalIgnoreCase)
                        };
                        foreach (var pair in rollback.BackupFiles)
                        {
                            var bytes = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease, pair.Value));
                            Assert.Equal(PendingTurnSnapshotAuthority.ComputeSha256(bytes), rollback.BackupHashes[pair.Key], ignoreCase: true);
                        }
                        var frame = retryFrame = new SpiritualSelfTreatmentStageContext(turn.SessionId, turn.RequestId, turn.TurnNumber, turn.ProgressionControl!);
                        var admission = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(files, lease, frame, requireRetained: true);
                        checkpointRequest = admission.Request;
                        signedSchedulePath = payload.Files[schedulePath];
                        signedScheduleBytes = signedSchedule;
                        outerScheduleBytes = rollbackSchedule;
                        Assert.Equal(rollbackSchedule, admission.Owner.ReadDiagnosisOriginalScheduleBeforeImage().Bytes);
                        var retainedCheck = Assert.IsType<MortalWoundProcedureCheckAuthority>(admission.Request.ModeAuthority);
                        var outcome = MortalWoundDiagnosisOutcome.Resolve(admission.Owner,
                            new(admission.Owner.History, Array.Empty<ValidationIssue>()), admission.Request);
                        var working = MortalWoundDiagnosisWorkingProjection.Build(admission.Owner, outcome);
                        var unchanged = CaptureMortalDiagnosisImages(files);
                        var scheduler = new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance);
                        var calculation = await scheduler.PrepareDiagnosisOutcomeAsync(lease, working);
                        Assert.Equal(1260, working.OriginalMinute);
                        Assert.Equal(1290, working.ResultingMinute);
                        Assert.Equal(preparedSchedule, calculation.ScheduleBefore.Bytes);
                        var after = JsonNode.Parse(FileSystemManager.DecodeFileBytes(calculation.ScheduleAfter.Bytes!))!.AsObject();
                        Assert.Equal(1290, after["currentWorldTimeInMinutes"]!.GetValue<int>());
                        Assert.Equal(1290, working.ClockAfter["currentTimeInMinutes"]!.GetValue<long>());
                        AssertMortalDiagnosisImages(unchanged, files);
                        foreach (var path in new[] { schedulePath, clockPath })
                        {
                            var currentBytes = path == schedulePath ? preparedSchedule : preparedClock;
                            var changed = JsonNode.Parse(FileSystemManager.DecodeFileBytes(currentBytes))!.AsObject();
                            if (path == schedulePath)
                                changed["lastUpdatedUtc"] = DateTimeOffset.Parse(changed["lastUpdatedUtc"]!.GetValue<string>()).AddSeconds(1).ToString("o");
                            else changed["currentTimeInMinutes"] = 1261;
                            try
                            {
                                await files.WriteFileAtomicAsync(lease, path, changed.ToJsonString());
                                var refusal = await Assert.ThrowsAsync<InvalidDataException>(() => scheduler.PrepareDiagnosisOutcomeAsync(lease, working));
                                Assert.Equal("A competing schedule or timeChange/setWorldTime cannot replace diagnosis original time.", refusal.Message);
                            }
                            finally { await files.WriteFileAtomicBytesAsync(lease, path, currentBytes); }
                            AssertMortalDiagnosisImages(unchanged, files);
                        }
                        var retained = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(files, lease, frame, requireRetained: true);
                        Assert.Equal(admission.Request.RequestFingerprint, retained.Request.RequestFingerprint);
                        var sameCheck = Assert.IsType<MortalWoundProcedureCheckAuthority>(retained.Request.ModeAuthority);
                        Assert.Equal(retainedCheck.SourceIndices.ToArray(), sameCheck.SourceIndices.ToArray());
                        Assert.Equal(retainedCheck.SourceRolls.ToArray(), sameCheck.SourceRolls.ToArray());
                        AssertMortalDiagnosisImages(unchanged, files);
                        Assert.Equal(rollbackSchedule, await files.ReadFileBytesAsync(lease, payload.RollbackBackups[schedulePath]));
                        Assert.Equal(signedSchedule, await files.ReadFileBytesAsync(lease, payload.Files[schedulePath]));
                        }
                        // Close the inspection lease, retaining the actual live
                        // ProcessPlayerTurn owner until restore/resume settles.
                        Assert.NotNull(rollback); Assert.NotNull(generation); Assert.NotNull(original);
                        Assert.NotNull(retryFrame); Assert.NotNull(checkpointRequest);
                        Assert.NotNull(signedSchedulePath); Assert.NotNull(signedScheduleBytes); Assert.NotNull(outerScheduleBytes);
                        using (timings.Measure("MortalDiagnosisOriginal.ExactOuterRestore"))
                            await runtime.Engine.RestoreOriginalForRepairAsync(rollback, generation);
                        AssertMortalDiagnosisImages(original, files);
                        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                        {
                            Assert.True(AcceptedTurnAuthorityRegistry.HasMortalDiagnosisOuterRollbackCheckpoint(files, lease));
                            // Each fault changes actual owned evidence, then restores
                            // its independently captured bytes. No envelope is forged.
                            foreach (var fault in new[]
                            {
                                (Path: ProgressionScheduleService.SchedulePath, Bytes: outerScheduleBytes),
                                (Path: signedSchedulePath, Bytes: signedScheduleBytes),
                                (Path: rollback.BackupFiles[ProgressionScheduleService.SchedulePath], Bytes: outerScheduleBytes)
                            })
                            {
                                var changed = JsonNode.Parse(FileSystemManager.DecodeFileBytes(fault.Bytes))!.AsObject();
                                changed["lastUpdatedUtc"] = DateTimeOffset.Parse(changed["lastUpdatedUtc"]!.GetValue<string>()).AddSeconds(2).ToString("o");
                                try
                                {
                                    await files.WriteFileAtomicAsync(lease, fault.Path, changed.ToJsonString());
                                    await Assert.ThrowsAsync<InvalidDataException>(() => AcceptedTurnAuthorityRegistry.ResumeMortalDiagnosisAfterOuterRollbackAsync(files, lease, retryFrame));
                                    Assert.True(AcceptedTurnAuthorityRegistry.HasMortalDiagnosisOuterRollbackCheckpoint(files, lease));
                                    Assert.Null(await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
                                    Assert.Null(await files.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
                                }
                                finally { await files.WriteFileAtomicBytesAsync(lease, fault.Path, fault.Bytes); }
                                AssertMortalDiagnosisImages(original, files);
                            }
                            await AcceptedTurnAuthorityRegistry.ResumeMortalDiagnosisAfterOuterRollbackAsync(files, lease, retryFrame);
                            Assert.False(AcceptedTurnAuthorityRegistry.HasMortalDiagnosisOuterRollbackCheckpoint(files, lease));
                            Assert.Equal(signedScheduleBytes, await files.ReadFileBytesAsync(lease, ProgressionScheduleService.SchedulePath));
                            var resumed = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(files, lease, retryFrame, requireRetained: true);
                            Assert.Equal(checkpointRequest.RequestFingerprint, resumed.Request.RequestFingerprint);
                            Assert.Equal(checkpointRequest.Coordinates.CoordinatesFingerprint, resumed.Request.Coordinates.CoordinatesFingerprint);
                            Assert.Equal(checkpointRequest.ResourceAuthority.AuthorityFingerprint, resumed.Request.ResourceAuthority.AuthorityFingerprint);
                            var originalCheck = Assert.IsType<MortalWoundProcedureCheckAuthority>(checkpointRequest.ModeAuthority);
                            var resumedCheck = Assert.IsType<MortalWoundProcedureCheckAuthority>(resumed.Request.ModeAuthority);
                            Assert.Equal(originalCheck.AuthorityFingerprint, resumedCheck.AuthorityFingerprint);
                            Assert.Equal(originalCheck.SourceIndices.ToArray(), resumedCheck.SourceIndices.ToArray());
                            Assert.Equal(originalCheck.SourceRolls.ToArray(), resumedCheck.SourceRolls.ToArray());
                            Assert.Equal(originalCheck.PreparedCriticalReaction?.PreparedReactionFingerprint, resumedCheck.PreparedCriticalReaction?.PreparedReactionFingerprint);
                            var resumedOutcome = MortalWoundDiagnosisOutcome.Resolve(resumed.Owner,
                                new(resumed.Owner.History, Array.Empty<ValidationIssue>()), resumed.Request);
                            var resumedWorking = MortalWoundDiagnosisWorkingProjection.Build(resumed.Owner, resumedOutcome);
                            var resumedBefore = CaptureMortalDiagnosisImages(files);
                            var calculation = await new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance)
                                .PrepareDiagnosisOutcomeAsync(lease, resumedWorking);
                            Assert.Equal(signedScheduleBytes, calculation.ScheduleBefore.Bytes);
                            Assert.Equal(1260, resumedWorking.OriginalMinute); Assert.Equal(1290, resumedWorking.ResultingMinute);
                            AssertMortalDiagnosisImages(resumedBefore, files);
                        }
                        await runtime.Engine.RestoreOriginalForRepairAsync(rollback, generation);
                        AssertMortalDiagnosisImages(original, files);
                        throw stop;
                    }
                });
            try
            {
                runtime.GameLoop.SetSession("session_mortal_diagnosis_prepared_original", 42);
                using (timings.Measure("MortalDiagnosisOriginal.Refresh"))
                    await runtime.StateManager.RefreshGameStateAsync();
                original = CaptureMortalDiagnosisImages(files);
                var opened = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
                var menu = Assert.IsType<WoundCommandState>(opened.WoundCommandState);
                var self = menu.IsTargetSelection ? Assert.Single(menu.Targets, row => row.Owner.OwnerKind == "player") : null;
                var selectedMenu = self is null ? menu : menu.ForTarget(self);
                var choice = Assert.Single(selectedMenu.Choices, row => row.Available && row.Selection.MortalDiagnosis?.Provider.OwnerId == "field_medic_01");
                var sequence = new List<string>();
                if (self is not null)
                    sequence.Add((Array.IndexOf(menu.Targets.Where(row => row.Available).ToArray(), self) + 1) + ". " + self.Label);
                sequence.Add((Array.IndexOf(selectedMenu.Choices.Where(row => row.Available).ToArray(), choice) + 1) + ". " + choice.Name + " — " + choice.Severity);
                console.QueueAnySelection(sequence.ToArray());
                console.QueueAnyConfirmResponse(true);
                var action = Assert.IsType<string>(await runtime.Explorer.TryProcessCommand("/лечить"));
                Assert.Equal(WoundApplicationService.MortalDiagnosisActionText, action);
                Assert.Single(console.ConfirmPrompts);
                AssertMortalDiagnosisImages(original, files);
                using (timings.Measure("MortalDiagnosisOriginal.EngineStage"))
                    Assert.Same(stop, await Record.ExceptionAsync(() => runtime.Engine.ProcessPlayerTurn(action)));
                Assert.Equal(1, checkpoints);
                Assert.NotNull(rollback);
                Assert.NotNull(generation);
                // Exact restoration completed inside the live Engine operation,
                // after each inspection lease closed and before controlled stop.
                AssertMortalDiagnosisImages(original, files);
                Assert.Equal(42, runtime.GameLoop.TurnNumber);
            }
            finally
            {
                output.WriteLine(logger.Describe());
                using var closing = timings.Measure("MortalDiagnosisOriginal.AudioClosing");
                await runtime.AudioService.DisposeAsync();
            }
        }
    }
}
