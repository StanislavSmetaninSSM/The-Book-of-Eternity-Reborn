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
    [InlineData("missing_signed_input")]
    [InlineData("tampered_signed_input")]
    [InlineData("current_input_drift")]
    [InlineData("missing_canonical_baseline")]
    public async Task PreparedDiagnosisDistinguishesSignedInputFromCanonicalRollbackBaseline(string mode)
    {
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
            // Refusal controls keep a baseline-present request so the known role
            // bug cannot mask their independent missing/tampered/current-input guards.
            if (mode != "signed_input_only") await StageAsync();
            var history = await files.ReadFileBytesAsync(WoundHistoryState.HistoryPath);
            Assert.NotNull(history);
            if (mode == "missing_canonical_baseline") files.DeleteFile(WoundHistoryState.HistoryPath);
            var backup = await runtime.Engine.CaptureOriginalRollbackAsync("diagnosis-role");
            if (mode is "signed_input_only" or "missing_canonical_baseline")
            {
                // Reuse the actual Explorer overlay for signed preparation input
                // and for the deliberately missing canonical-source baseline fault.
                await runtime.Explorer.StagePendingLocalTurnRollbackSnapshotAsync(
                    AcceptedMechanicsPlan.WoundCommandPath, WoundHistoryState.HistoryPath);
                if (mode == "missing_canonical_baseline")
                {
                    await files.WriteFileAtomicAsync(WoundHistoryState.HistoryPath, CanonicalJsonUtf8.DecodeOneOptionalBom(history!));
                    Assert.Equal(history, await files.ReadFileBytesAsync(WoundHistoryState.HistoryPath));
                }
                if (mode == "signed_input_only")
                {
                    await StageAsync();
                    runtime.Explorer.MarkExistingPendingLocalTurnValidationSnapshotFiles(AcceptedMechanicsPlan.WoundCommandPath);
                }
                if (mode == "missing_canonical_baseline")
                    runtime.Explorer.MarkExistingPendingLocalTurnValidationSnapshotFiles(WoundHistoryState.HistoryPath);
                runtime.Engine.OverlayExplorerLocalRollbackSnapshot(backup,
                    runtime.Explorer.ConsumePendingLocalTurnRollbackSnapshot());
            }
            await files.WriteFileAtomicAsync(LiveTurnPreparationService.TurnRequestPath,
                JsonSerializer.Serialize(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            await runtime.Engine.CaptureOriginalValidationSnapshotAsync(request, backup, "diagnosis-role");

            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            var manifest = await fixture.ReadManifestAsync(lease);
            Assert.True(manifest.Files.ContainsKey(AcceptedMechanicsPlan.WoundCommandPath));
            Assert.Equal(mode != "signed_input_only", manifest.RollbackBaselineFiles.Contains(AcceptedMechanicsPlan.WoundCommandPath));
            Assert.Equal(mode != "missing_canonical_baseline", manifest.RollbackBaselineFiles.Contains(WoundHistoryState.HistoryPath));
            {
                var observed = AfterlifeWoundHealingPlanner.ReadOriginalDiagnosisSource(files, lease, "chaos_sea");
                Assert.True(observed.Success, string.Join(";", observed.Issues));
                var signed = Assert.IsType<PendingTurnSnapshotReadAuthority>(observed.Snapshot);
                Assert.Equal(request.SessionId, signed.SessionId);
                Assert.Equal(request.RequestId, signed.RequestId);
                Assert.Equal(request.TurnNumber, signed.TurnNumber);
                Assert.Equal(manifest.ManifestPayloadHash, signed.SnapshotToken);
                var original = signed.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath);
                Assert.Equal(original, await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
                Assert.Equal(manifest.SnapshotFileHashes[AcceptedMechanicsPlan.WoundCommandPath],
                    PendingTurnSnapshotAuthority.ComputeSha256(original), ignoreCase: true);
                var exact = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(original))!.AsObject();
                Assert.True(SpiritualDiagnosisRequestCodec.IsBound(exact,
                    new(request.SessionId, request.RequestId, request.TurnNumber, control)));
                var sourceCoverage = PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(manifest,
                    m => m.Files, m => m.SnapshotFileHashes, m => m.RollbackBaselineFiles,
                    AfterlifeWoundTreatmentAuthority.DiagnosisSourcePaths("chaos_sea"), out var missing);
                Assert.Equal(mode != "missing_canonical_baseline", sourceCoverage);
                Assert.Equal(mode == "missing_canonical_baseline" ? WoundHistoryState.HistoryPath : null, missing);
            }
            if (mode == "missing_signed_input")
                files.DeleteFile(lease, manifest.Files[AcceptedMechanicsPlan.WoundCommandPath]);
            if (mode == "tampered_signed_input")
                await files.WriteFileAtomicAsync(lease, manifest.Files[AcceptedMechanicsPlan.WoundCommandPath], "{}");
            if (mode == "current_input_drift")
                await files.WriteFileAtomicAsync(lease, AcceptedMechanicsPlan.WoundCommandPath, "{}");
            await ExplorerWoundSelfTreatmentTests.WriteNativeCompletionAsync(files, lease, request);
            var before = fixture.CaptureMutationRoots(AfterlifeWoundTreatmentAuthority.DiagnosisSourcePaths("chaos_sea")
                .Concat(new[] { AcceptedMechanicsPlan.WoundCommandPath, LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                    PendingTurnSnapshotAuthority.AuthorityPath, "output/interface_updates.json" }));
            AfterlifeWoundHealingPlanner.Continuation? owner = null;
            var failure = await Record.ExceptionAsync(async () =>
            {
                owner = await AfterlifeWoundHealingPlanner.Continuation.AdmitDiagnosisAsync(files, lease, manifest);
            });
            try
            {
                fixture.AssertMutationRootsUnchanged(before);
                if (mode is "signed_input_only" or "baseline_present")
                {
                    Assert.Null(failure); // Intended RED only for the authentic signed-input-only role.
                    Assert.NotNull(owner);
                    Assert.True(owner.IsDiagnosis);
                    Assert.Equal(selection, SpiritualDiagnosisRequestCodec.ReadSelection(Assert.IsType<JsonObject>(owner.Request)));
                }
                else
                {
                    Assert.IsType<InvalidDataException>(failure);
                    Assert.Null(owner);
                }
            }
            finally { owner?.Close(); }
        }
        finally { await runtime.AudioService.DisposeAsync(); }

        async Task StageAsync()
        {
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            var staged = await AfterlifeWoundTreatmentAuthority.StageDiagnosisAsync(files, lease,
                new(request.SessionId, request.RequestId, request.TurnNumber, control), selection);
            Assert.True(staged.IsStaged, staged.IssueCode);
            request.SpiritualHealing = Assert.IsType<JsonObject>(staged.GmContext);
            Assert.Equal("diagnose", request.SpiritualHealing["mode"]!.GetValue<string>());
        }
    }
}
