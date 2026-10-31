using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("signed_input_only")]
    [InlineData("baseline_present")]
    [InlineData("current_input_drift")]
    [InlineData("missing_canonical_baseline")]
    [InlineData("missing_dynamic_baseline")]
    [InlineData("baseline_backup_tamper")]
    public async Task DiagnosisPublicPreparationPreservesExactDynamicRollbackRoles(string mode)
    {
        const string outputPath = "output/interface_updates.json";
        await using var fixture = await HealingFixture.CreateAsync(2, 0, 20, stageRequest: false,
            beforeResourceBootstrap: AfterlifeSpiritualNaturalRecoveryTests.SeedDiagnosisBeforeBootstrapAsync);
        var files = fixture.Files;
        var wound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        var selection = new SpiritualSelfTreatmentSelection(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound))
        { Diagnosis = new(wound.Owner, "native_diagnosis", WoundDiagnosisEvidence.ComputePathFingerprint(wound, "native_diagnosis")) };
        var control = await new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance)
            .BuildControlForNextTurnAsync();
        var request = new TurnRequest
        {
            SessionId = "session_spiritual_healing", RequestId = "request_spiritual_healing", TurnNumber = 43,
            Timestamp = DateTime.UtcNow.ToString("o"), PlayerAction = WoundApplicationService.DiagnosisActionText,
            ProgressionControl = control
        };
        var runtime = CreateGameEngineRuntime(files, configureSettings: settings =>
        { settings.GmBridgeAutoStart = false; settings.MusicEnabled = false; settings.SoundEnabled = false; });
        try
        {
            runtime.GameLoop.SetSession(request.SessionId, 42);
            if (mode != "signed_input_only") await StageAsync();
            var historyBytes = await files.ReadFileBytesAsync(WoundHistoryState.HistoryPath);
            Assert.NotNull(historyBytes);
            Assert.Null(await files.ReadFileBytesAsync(outputPath));
            if (mode == "missing_canonical_baseline") files.DeleteFile(WoundHistoryState.HistoryPath);
            var backup = await runtime.Engine.CaptureOriginalRollbackAsync("diagnosis-dynamic-role");
            if (mode is "signed_input_only" or "missing_canonical_baseline" or "missing_dynamic_baseline")
            {
                // Fault roles are authored by the real snapshot producer; no
                // manifest or detached authority is rewritten by the test.
                await runtime.Explorer.StagePendingLocalTurnRollbackSnapshotAsync(
                    AcceptedMechanicsPlan.WoundCommandPath, WoundHistoryState.HistoryPath, outputPath);
                if (mode == "missing_canonical_baseline")
                {
                    await files.WriteFileAtomicAsync(WoundHistoryState.HistoryPath, CanonicalJsonUtf8.DecodeOneOptionalBom(historyBytes!));
                    Assert.Equal(historyBytes, await files.ReadFileBytesAsync(WoundHistoryState.HistoryPath));
                    runtime.Explorer.MarkExistingPendingLocalTurnValidationSnapshotFiles(WoundHistoryState.HistoryPath);
                }
                if (mode == "signed_input_only")
                {
                    await StageAsync();
                    runtime.Explorer.MarkExistingPendingLocalTurnValidationSnapshotFiles(AcceptedMechanicsPlan.WoundCommandPath);
                }
                if (mode == "missing_dynamic_baseline")
                {
                    await files.WriteFileAtomicAsync(outputPath, "{}");
                    runtime.Explorer.MarkExistingPendingLocalTurnValidationSnapshotFiles(outputPath);
                }
                runtime.Engine.OverlayExplorerLocalRollbackSnapshot(backup,
                    runtime.Explorer.ConsumePendingLocalTurnRollbackSnapshot());
            }
            await files.WriteFileAtomicAsync(LiveTurnPreparationService.TurnRequestPath,
                JsonSerializer.Serialize(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            await runtime.Engine.CaptureOriginalValidationSnapshotAsync(request, backup, "diagnosis-dynamic-role");

            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            var manifest = await fixture.ReadManifestAsync(lease);
            Assert.Equal(mode != "signed_input_only", manifest.RollbackBaselineFiles.Contains(AcceptedMechanicsPlan.WoundCommandPath));
            Assert.Equal(mode != "missing_canonical_baseline", manifest.RollbackBaselineFiles.Contains(WoundHistoryState.HistoryPath));
            Assert.DoesNotContain(outputPath, manifest.RollbackBaselineFiles);
            Assert.Equal(mode == "missing_dynamic_baseline", manifest.Files.ContainsKey(outputPath));
            var observed = AfterlifeWoundHealingPlanner.ReadOriginalDiagnosisSource(files, lease, "chaos_sea");
            Assert.True(observed.Success, string.Join(";", observed.Issues));
            var signed = Assert.IsType<PendingTurnSnapshotReadAuthority>(observed.Snapshot);
            Assert.Equal(manifest.ManifestPayloadHash, signed.SnapshotToken);
            var original = signed.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath);
            Assert.Equal(original, await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
            Assert.True(SpiritualDiagnosisRequestCodec.IsBound(JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(original))!.AsObject(),
                new(request.SessionId, request.RequestId, request.TurnNumber, control)));
            var sourceCoverage = PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(manifest,
                m => m.Files, m => m.SnapshotFileHashes, m => m.RollbackBaselineFiles,
                AfterlifeWoundTreatmentAuthority.DiagnosisSourcePaths("chaos_sea"), out var missingSource);
            Assert.Equal(mode != "missing_canonical_baseline", sourceCoverage);
            Assert.Equal(mode == "missing_canonical_baseline" ? WoundHistoryState.HistoryPath : null, missingSource);
            await ExplorerWoundSelfTreatmentTests.WriteNativeCompletionAsync(files, lease, request);

            // Prepare the actual ordinary owners before any post-seal byte fault.
            AssertHealthy(await runtime.ValidationService.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(), "raw location setup");
            AssertHealthy(await runtime.ValidationService.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease), "raw item setup");
            if (mode == "current_input_drift")
                await files.WriteFileAtomicAsync(lease, AcceptedMechanicsPlan.WoundCommandPath, "{}");
            if (mode == "baseline_backup_tamper")
            {
                Assert.True(manifest.RollbackBackups.TryGetValue(AcceptedMechanicsPlan.WoundCommandPath, out var backupPath));
                Assert.Equal(original, await files.ReadFileBytesAsync(lease, backupPath!));
                await files.WriteFileAtomicAsync(lease, backupPath!, "{}");
            }
            var before = fixture.CaptureMutationRoots(AfterlifeWoundTreatmentAuthority.DiagnosisSourcePaths("chaos_sea")
                .Concat(WoundAcceptedTurnSnapshotContract.RequiredPaths)
                .Concat(new[] { LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath })
                .Concat(manifest.RollbackBackups.Values));
            try
            {
                var issues = await runtime.ValidationService.PrepareSpiritualSelfTreatmentPublicationAsync(lease);
                var errors = issues.Where(issue => issue.Severity == IssueSeverity.Error).ToArray();
                fixture.AssertMutationRootsUnchanged(before);
                if (mode == "current_input_drift")
                    Assert.Contains(errors, issue => issue.Code == "spiritual_diagnosis_admission_invalid" &&
                        issue.FilePath == AcceptedMechanicsPlan.WoundCommandPath &&
                        issue.Actual == "The retained spiritual treatment request changed.");
                else if (mode == "missing_canonical_baseline")
                    Assert.Contains(errors, issue => issue.Code == "spiritual_diagnosis_admission_invalid" &&
                        issue.FilePath == AcceptedMechanicsPlan.WoundCommandPath &&
                        issue.Actual == "Diagnosis requires its exact signed turn and complete rollback baseline.");
                else if (mode == "missing_dynamic_baseline")
                    Assert.Contains(errors, issue => issue.Code == "wound_materialization_snapshot_before_image_missing" && issue.FilePath == outputPath &&
                        issue.Expected == "exact signed rollback snapshot evidence for every dynamically touched wound-plan path");
                else if (mode == "baseline_backup_tamper")
                    Assert.Contains(errors, issue => issue.Code == "pending_turn_snapshot_reader_authority_invalid" &&
                        issue.FilePath == PendingTurnSnapshotAuthority.AuthorityPath);
                else
                {
                    // Intended RED: signed_input_only alone reaches the real
                    // dynamic coverage refusal despite complete source admission.
                    Assert.True(errors.Length == 0, JsonSerializer.Serialize(issues, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                    Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(files, lease, out _, out var validated));
                    var plan = Assert.IsType<AcceptedMechanicsPlan>(validated.Plan);
                    Assert.True(Assert.IsType<AfterlifeWoundHealingPlanner.Continuation>(plan.SpiritualHealingContinuation).IsDiagnosis);
                }
            }
            finally
            {
                if (AcceptedMechanicsPlanAuthority.TryPeekValidated(files, lease, out _, out var held))
                    held.Plan?.SpiritualHealingContinuation?.Close();
                AcceptedMechanicsPlanAuthority.InvalidateValidated(files, lease);
                AcceptedMechanicsPlanAuthority.InvalidateSpiritualSelfTreatment(files, lease);
            }
        }
        finally { await runtime.AudioService.DisposeAsync(); }

        async Task StageAsync()
        {
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            var staged = await AfterlifeWoundTreatmentAuthority.StageDiagnosisAsync(files, lease,
                new(request.SessionId, request.RequestId, request.TurnNumber, control), selection);
            Assert.True(staged.IsStaged, staged.IssueCode);
            request.SpiritualHealing = Assert.IsType<JsonObject>(staged.GmContext);
        }

        static void AssertHealthy(IReadOnlyList<ValidationIssue> issues, string phase) =>
            Assert.True(issues.All(issue => issue.Severity != IssueSeverity.Error),
                phase + ": " + JsonSerializer.Serialize(issues, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }
}
