using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

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
    public async Task MortalDiagnosisRequiresActualSignedInputRoleAndCompleteCanonicalRollback(string mode)
    {
        TurnRequest? request = null;
        await using var fixture = await MortalWoundTreatmentResolverTests.DiagnosisOwnershipFixture.CreateAsync(
            20, signedInput: true, duration: 0, originalProducer: async (files, actualRequest) =>
            {
                request = actualRequest;
                var input = await files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath);
                Assert.NotNull(input);
                if (mode == "signed_input_only") files.DeleteFile(AcceptedMechanicsPlan.WoundCommandPath);
                var history = await files.ReadFileBytesAsync(WoundHistoryState.HistoryPath);
                Assert.NotNull(history);
                if (mode == "missing_canonical_baseline") files.DeleteFile(WoundHistoryState.HistoryPath);
                var runtime = CreateGameEngineRuntime(files, configureSettings: settings =>
                { settings.GmBridgeAutoStart = false; settings.MusicEnabled = false; settings.SoundEnabled = false; });
                try
                {
                    runtime.GameLoop.SetSession(actualRequest.SessionId, actualRequest.TurnNumber - 1);
                    var backup = await runtime.Engine.CaptureOriginalRollbackAsync("mortal-diagnosis-role");
                    if (mode is "signed_input_only" or "missing_canonical_baseline")
                    {
                        await runtime.Explorer.StagePendingLocalTurnRollbackSnapshotAsync(
                            AcceptedMechanicsPlan.WoundCommandPath, WoundHistoryState.HistoryPath);
                        if (mode == "signed_input_only") await files.WriteFileAtomicBytesAsync(AcceptedMechanicsPlan.WoundCommandPath, input);
                        else await files.WriteFileAtomicBytesAsync(WoundHistoryState.HistoryPath, history);
                        runtime.Explorer.MarkExistingPendingLocalTurnValidationSnapshotFiles(mode == "signed_input_only"
                            ? AcceptedMechanicsPlan.WoundCommandPath : WoundHistoryState.HistoryPath);
                        runtime.Engine.OverlayExplorerLocalRollbackSnapshot(backup, runtime.Explorer.ConsumePendingLocalTurnRollbackSnapshot());
                    }
                    await files.WriteFileAtomicAsync(LiveTurnPreparationService.TurnRequestPath,
                        JsonSerializer.Serialize(actualRequest, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                    await runtime.Engine.CaptureOriginalValidationSnapshotAsync(actualRequest, backup, "mortal-diagnosis-role");
                }
                finally { await runtime.AudioService.DisposeAsync(); }
            });
        Assert.NotNull(request);
        var fs = fixture.Files; var lease = fixture.Lease;
        var manifestJson = Assert.IsType<string>(await fs.ReadFileAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
        var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(manifestJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Contains(AcceptedMechanicsPlan.WoundCommandPath, manifest.Files.Keys);
        Assert.Equal(mode != "signed_input_only", manifest.RollbackBaselineFiles.Contains(AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Equal(mode != "missing_canonical_baseline", manifest.RollbackBaselineFiles.Contains(WoundHistoryState.HistoryPath));
        var originalInput = await fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        Assert.NotNull(originalInput);
        Assert.Equal(PendingTurnSnapshotAuthority.ComputeSha256(originalInput), manifest.SnapshotFileHashes[AcceptedMechanicsPlan.WoundCommandPath], ignoreCase: true);
        var signedPath = manifest.Files[AcceptedMechanicsPlan.WoundCommandPath];
        var signedBytes = await fs.ReadFileBytesAsync(lease, signedPath);
        Assert.Equal(originalInput, signedBytes);
        if (mode == "missing_signed_input") fs.DeleteFile(lease, signedPath);
        if (mode == "tampered_signed_input") await fs.WriteFileAtomicAsync(lease, signedPath, "{}");
        if (mode == "current_input_drift") await fs.WriteFileAtomicAsync(lease, AcceptedMechanicsPlan.WoundCommandPath, "{}");
        var unchanged = fixture.CaptureJson();
        if (mode is "signed_input_only" or "baseline_present")
        {
            // A post-GM intake may only reuse the attempt produced before GM.
            // Missing retention must refuse before taking a new dice/resource claim.
            await Assert.ThrowsAsync<InvalidDataException>(() => WoundApplicationService.ResolveMortalDiagnosisInputAsync(fs, lease,
                new(request.SessionId, request.RequestId, request.TurnNumber, request.ProgressionControl!), requireRetained: true));
            fixture.AssertJson(unchanged);
        }
        WoundApplicationService.MortalDiagnosisAdmission? admission = null;
        var failure = await Record.ExceptionAsync(async () => admission = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(fs, lease,
            new(request.SessionId, request.RequestId, request.TurnNumber, request.ProgressionControl!)));
        if (mode is "signed_input_only" or "baseline_present")
        {
            Assert.Null(failure);
            Assert.NotNull(admission);
            Assert.False(admission.Owner.ReadDiagnosisOriginalPending().Existed);
            Assert.Null(admission.Owner.ReadDiagnosisOriginalPending().Bytes);
            var retained = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(fs, lease,
                new(request.SessionId, request.RequestId, request.TurnNumber, request.ProgressionControl!), requireRetained: true);
            Assert.Equal(admission.Request.RequestFingerprint, retained.Request.RequestFingerprint);
            Assert.Equal(Assert.IsType<MortalWoundProcedureCheckAuthority>(admission.Request.ModeAuthority).SourceIndices,
                Assert.IsType<MortalWoundProcedureCheckAuthority>(retained.Request.ModeAuthority).SourceIndices);
            admission = retained;
            var computedHash = PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(manifest, LiveTurnPreparationService.ManifestHashJsonOptions,
                value => value.ManifestPayloadHash, (value, hash) => value.ManifestPayloadHash = hash);
            Assert.Equal(mode == "signed_input_only", admission.Owner.HasCurrentDiagnosisPreparedCommandRole(fs, lease,
                admission.Request, request.SessionId, request.RequestId, request.TurnNumber, manifest.ManifestPayloadHash, computedHash));
            Assert.Equal(new[] { 0 }, Assert.IsType<MortalWoundProcedureCheckAuthority>(admission.Request.ModeAuthority).SourceIndices);
            var pending = fs.ResolvePath(WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
            var after = fixture.CaptureJson();
            Assert.Equal(unchanged.Keys.Where(path => path != pending).Order(StringComparer.Ordinal), after.Keys.Where(path => path != pending).Order(StringComparer.Ordinal));
            foreach (var pair in unchanged.Where(pair => pair.Key != pending)) Assert.Equal(pair.Value, after[pair.Key]);
        }
        else
        {
            Assert.IsType<InvalidDataException>(failure);
            Assert.Null(admission);
            fixture.AssertJson(unchanged);
            Assert.Null(await fs.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
        }
    }
}
