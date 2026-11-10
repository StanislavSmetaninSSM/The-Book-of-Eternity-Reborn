using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PendingTurnSnapshotPresenceIntegrationTests
{
    [Theory]
    [InlineData("live", false)]
    [InlineData("live", true)]
    [InlineData("engine", false)]
    [InlineData("engine", true)]
    public async Task OriginalPendingViewUsesBothRealProducersExactImagesCopiesAndFreshSource(string producer, bool present)
    {
        var reads = new CanonicalReadActivityAccumulator();
        await using var context = await ResourceMaterializationTestContext.CreateAsync(new FileSystemManagerHooks { PassiveCanonicalReads = reads });
        var files = context.FileSystem;
        await context.WriteExactJsonAsync(SoulPath, "{\"currentRealm\":\"Mortal World\"}");
        const string pending = WoundAcceptedTurnSnapshotContract.PendingResolutionPath;
        var original = Encoding.UTF8.GetBytes("{\"snapshotFixture\":\"original pending DATA, not an owner\"}");
        if (present) await context.WriteExactBytesAsync(pending, original);
        if (producer == "live") await PrepareLiveAsync(context);
        else
        {
            var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files, configureSettings: settings =>
            { settings.GmBridgeAutoStart = false; settings.MusicEnabled = false; settings.SoundEnabled = false; });
            try
            {
                runtime.GameLoop.SetSession("pending-observation-engine", 41);
                var backup = await runtime.Engine.CaptureOriginalRollbackAsync("pending-observation-engine");
                var turn = new TurnRequest
                {
                    SessionId = "pending-observation-engine", RequestId = "pending-observation-engine-request", TurnNumber = 42,
                    PlayerAction = "Observe original pending DATA only.", Timestamp = DateTime.UtcNow.ToString("O"),
                    PreGeneratedDices1d20 = [7], ProgressionControl = new() { CurrentRealm = "Mortal World" }
                };
                await files.WriteFileAtomicAsync(LiveTurnPreparationService.TurnRequestPath,
                    JsonSerializer.Serialize(turn, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                await runtime.Engine.CaptureOriginalValidationSnapshotAsync(turn, backup, "pending-observation-engine");
            }
            finally { await runtime.AudioService.DisposeAsync(); }
        }
        var manifest = await ReadManifestAsync(context);
        Assert.Equal(present, manifest["originalPathPresenceV1"]![pending]!.GetValue<bool>());
        var manifestToken = manifest["manifestPayloadHash"]!.GetValue<string>();
        var originalArtifact = present ? manifest["files"]![pending]!.GetValue<string>() : null;
        // Deliberately later canonical DATA cannot replace the original. No
        // diagnosis owner, pending request or accepted receipt is manufactured.
        await context.WriteExactJsonAsync(pending, "{\"snapshotFixture\":\"later current DATA\"}");
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var result = PendingTurnSnapshotReader.ReadObservedCurrent(files, lease,
            new PendingTurnSnapshotPathSelection([SoulPath], [pending]));
        Assert.True(result.Success, JsonSerializer.Serialize(result.Issues));
        var evidence = Assert.IsType<PendingTurnSnapshotReader.OriginalReadEvidence>(result.Evidence);
        var issued = Assert.IsType<PendingTurnSnapshotReadAuthority>(result.Snapshot);
        Assert.Equal(manifestToken, issued.SnapshotToken);
        var counterPaths = new[] { LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath,
            originalArtifact ?? pending };
        var before = counterPaths.Select(reads.ReadCount).ToArray();
        Assert.True(before[0] > 0); Assert.True(before[1] > 0);
        var image = Assert.IsType<CanonicalBeforeImage>(evidence.ReadPendingResolutionOriginal());
        Assert.Equal(present, image.Existed); Assert.Equal(present ? original : null, image.Bytes);
        if (image.Bytes is { } escaped) escaped[0] ^= 1;
        var copied = Assert.IsType<CanonicalBeforeImage>(evidence.ReadPendingResolutionOriginal());
        Assert.Equal(present ? original : null, copied.Bytes);
        Assert.Equal(image.Fingerprint, copied.Fingerprint);
        Assert.Equal(before, counterPaths.Select(reads.ReadCount).ToArray()); // Factual DATA exports open no source.
        evidence.RequireCurrent(files, lease, issued);
        if (present)
        {
            // Existing exact selected coverage proves presence even in the prior
            // supported map. This signed-DATA fixture grants no gameplay owner.
            Assert.True(manifest["originalPathPresenceV1"]!.AsObject().Remove(pending));
            await ResignUnderLeaseAsync(context, lease, manifest);
            var legacy = PendingTurnSnapshotReader.ReadObservedCurrent(files, lease,
                new PendingTurnSnapshotPathSelection([SoulPath], [pending]));
            Assert.True(legacy.Success, JsonSerializer.Serialize(legacy.Issues));
            var legacyEvidence = Assert.IsType<PendingTurnSnapshotReader.OriginalReadEvidence>(legacy.Evidence);
            var legacyIssued = Assert.IsType<PendingTurnSnapshotReadAuthority>(legacy.Snapshot);
            var legacyImage = Assert.IsType<CanonicalBeforeImage>(legacyEvidence.ReadPendingResolutionOriginal());
            Assert.True(legacyImage.Existed); Assert.Equal(original, legacyImage.Bytes);
            Assert.Contains("pending_turn_snapshot_reader_source_changed", Assert.Throws<InvalidDataException>(
                () => evidence.RequireCurrent(files, lease, issued)).Message);
            await files.WriteFileAtomicBytesAsync(lease, originalArtifact!, original.Concat(new byte[] { 32 }).ToArray());
            Assert.Contains("pending_turn_snapshot_reader_source_changed", Assert.Throws<InvalidDataException>(
                () => legacyEvidence.RequireCurrent(files, lease, legacyIssued)).Message);
            // Restore only this detached reader fixture's artifact so the next
            // independently resealed negative isolates presence, not byte drift.
            await files.WriteFileAtomicBytesAsync(lease, originalArtifact!, original);
            manifest["originalPathPresenceV1"]![pending] = false;
            await ResignUnderLeaseAsync(context, lease, manifest);
            var contradiction = PendingTurnSnapshotReader.ReadObservedCurrent(files, lease,
                new PendingTurnSnapshotPathSelection([SoulPath], [pending]));
            Assert.False(contradiction.Success); Assert.Null(contradiction.Evidence);
            Assert.Contains(contradiction.Issues, issue => issue.Code == "pending_turn_snapshot_reader_presence_invalid" && issue.FilePath == pending);
        }
        else
        {
            var rawManifest = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
            await files.WriteFileAtomicBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath, rawManifest.Concat(new byte[] { 32 }).ToArray());
            Assert.Contains("pending_turn_snapshot_reader_source_changed", Assert.Throws<InvalidDataException>(
                () => evidence.RequireCurrent(files, lease, issued)).Message);
            if (producer == "live") manifest["files"]!.AsObject()[pending] = LiveTurnPreparationService.PendingTurnSnapshotDirectory + "/" + pending;
            else manifest["snapshotFileHashes"]!.AsObject()[pending] = new string('D', 64);
            await ResignUnderLeaseAsync(context, lease, manifest);
            var asymmetric = PendingTurnSnapshotReader.ReadObservedCurrent(files, lease,
                new PendingTurnSnapshotPathSelection([SoulPath], [pending]));
            Assert.False(asymmetric.Success); Assert.Null(asymmetric.Evidence);
            Assert.Contains(asymmetric.Issues, issue => issue.Code == "pending_turn_snapshot_reader_coverage_case_mismatch" && issue.FilePath == pending);
        }
    }

    private static async Task ResignUnderLeaseAsync(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease lease, JsonObject manifest)
    {
        manifest["manifestPayloadHash"] = string.Empty;
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await context.FileSystem.WriteFileAtomicAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath, manifest.ToJsonString());
        // Same existing component signed-DATA writer; this is not a gameplay
        // authority or an accepted publication/repair receipt.
        await PendingTurnSnapshotTestAuthority.WriteAuthorityForManifestAsync(context.FileSystem, manifest, lease);
    }
}
