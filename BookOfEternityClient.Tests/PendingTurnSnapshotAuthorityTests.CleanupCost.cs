using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PendingTurnSnapshotAuthorityTests
{
    /// <summary>
    /// Measures the same real validator and exact rollback bytes through isolated reader implementations.
    /// </summary>
    /// <param name="count">
    /// The number of independently guarded rollback members prepared before measurement.
    /// </param>
    /// <param name="size">
    /// The byte length of each snapshot and rollback image.
    /// </param>
    [Theory]
    [InlineData(1, 256)]
    [InlineData(17, 256)]
    [InlineData(17, 16384)]
    public async Task CleanupCost_RealValidatorSeparatesComputationAndReaders(int count, int size)
    {
        var setup = Stopwatch.StartNew();
        var manifest = await CreateCleanupCostManifestAsync(count, size);
        var envelope = CreateDetachedAuthorityJson(manifest);
        var memory = manifest.RollbackBackups.Values.ToDictionary(path => path,
            path => ReadRelativeFileBytes(path)!, StringComparer.Ordinal);
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        var generation = _fs.GetOrCreateSessionGeneration(lease);
        _cleanupCostOutput.WriteLine(JsonSerializer.Serialize(new { Boundary="cleanup_component_v1", Operation="CleanupComponent.PrepareFixtureAsync", Members=count, BytesPerMember=size, Milliseconds=setup.Elapsed.TotalMilliseconds }));

        void Measure(string mode, Func<string, byte[]?> reader)
        {
            var calls = 0; long bytes = 0; long readTicks = 0;
            byte[]? Read(string path)
            {
                calls++;
                var started = Stopwatch.GetTimestamp();
                try { var value = reader(path); bytes += value?.LongLength ?? 0; return value; }
                finally { readTicks += Stopwatch.GetTimestamp() - started; }
            }
            var clock = Stopwatch.StartNew();
            var valid = ValidateCleanupCost(manifest, envelope, Read, out var payload, out var failure);
            clock.Stop();
            _cleanupCostOutput.WriteLine(JsonSerializer.Serialize(new { Boundary="cleanup_component_v1", Operation="CleanupComponent.DestructiveAuthority", Reader=mode, Members=count, BytesPerMember=size, Milliseconds=clock.Elapsed.TotalMilliseconds, CallbackCount=calls, CallbackMilliseconds=readTicks*1000.0/Stopwatch.Frequency, ReturnedBytes=bytes, Valid=valid, Failure=failure, MainOwner=(mode=="main-leased"), Scope="Exact rollback hashing only; fixture setup excluded, warm files, not whole joint cleanup" }));
            Assert.True(valid, failure); Assert.NotNull(payload);
            Assert.Equal(count, payload.RollbackBackups.Count);
            Assert.Equal("authorized", failure);
            Assert.Equal(count, calls); Assert.Equal((long)count * size, bytes);
        }

        await Task.Run(() =>
        {
            Measure("memory", path => memory[path]);
            Measure("direct", ReadRelativeFileBytes);
            Measure("leased", path => ReadCleanupCostBytes(lease, path));
        });
        await SessionOperationContext.RunBoundAsync(_fs, generation, lease, () => Task.Run(() =>
        {
            Measure("main-leased", path => ReadCleanupCostBytes(lease, path));
            return 0;
        }));
    }

    /// <summary>
    /// Rejects actual changed or missing rollback bytes and corrupted integrity before granting a payload.
    /// </summary>
    /// <param name="fault">
    /// The physical changed/missing member or corrupt envelope to retain through refusal.
    /// </param>
    [Theory]
    [InlineData("changed")]
    [InlineData("missing")]
    [InlineData("envelope")]
    public async Task CleanupCost_RealValidatorRefusesChangedSource(string fault)
    {
        var manifest = await CreateCleanupCostManifestAsync(3, 256);
        var envelope = CreateDetachedAuthorityJson(manifest);
        var target = manifest.RollbackBackups.Values.First();
        if (fault == "changed") File.WriteAllBytes(_fs.ResolvePath(target), new byte[256]);
        if (fault == "missing") File.Delete(_fs.ResolvePath(target));
        if (fault == "envelope")
        {
            var root = JsonNode.Parse(envelope)!.AsObject(); root["payloadSha256"] = new string('0', 64); envelope = root.ToJsonString();
        }
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        var calls = 0;
        var result = await Task.Run(() =>
        {
            byte[]? Read(string path) { calls++; return ReadCleanupCostBytes(lease, path); }
            var valid = ValidateCleanupCost(manifest, envelope, Read, out var payload, out var failure);
            return (valid, payload, failure);
        });
        _cleanupCostOutput.WriteLine(JsonSerializer.Serialize(new { Boundary="cleanup_component_v1", Operation="CleanupComponent.DestructiveRefusal", Fault=fault, Valid=result.valid, Failure=result.failure, CallbackCount=calls }));
        Assert.False(result.valid); Assert.Null(result.payload);
        Assert.Equal(fault == "envelope" ? "invalid_detached_authority" : fault == "missing" ? "rollback_backup_unreadable" : "detached_authority_mismatch", result.failure);
        if (fault == "envelope") Assert.Equal(0, calls);
    }

    /// <summary>
    /// Prepares detached signed data and physical bytes without a joint gameplay owner or deletion operation.
    /// </summary>
    /// <param name="count">
    /// The positive rollback-member count.
    /// </param>
    /// <param name="size">
    /// The positive exact byte length of each image.
    /// </param>
    /// <returns>
    /// A manifest consumed by the real portable authority builder and validator.
    /// </returns>
    private async Task<TestManifest> CreateCleanupCostManifestAsync(int count, int size)
    {
        var manifest = await CreateManifestWithSnapshotAndRollbackAsync(31);
        manifest.Files.Clear(); manifest.SnapshotFileHashes.Clear(); manifest.RollbackBackups.Clear(); manifest.RollbackBaselineFiles.Clear();
        var bytes = Enumerable.Repeat((byte)'x', size).ToArray();
        for (var index = 0; index < count; index++)
        {
            var logical = index == 0 ? "game_state/meta/soul_state.json" :
                "game_state/core/cleanup_cost_" + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture) + ".json";
            var snapshot = "game_state/control/pending_turn_snapshot/" + logical;
            var rollback = logical + (index == 0 ? ".rollback.test-31" : ".rollback.cleanup-cost");
            foreach (var path in new[] { snapshot, rollback })
            {
                var full = _fs.ResolvePath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); File.WriteAllBytes(full, bytes);
            }
            manifest.Files.Add(logical, snapshot); manifest.SnapshotFileHashes.Add(logical, ComputeSha256(bytes));
            manifest.RollbackBackups.Add(logical, rollback); manifest.RollbackBaselineFiles.Add(logical);
        }
        manifest.SourceLabel = "cleanup-component-cost";
        manifest.ManifestPayloadHash = ComputeManifestPayloadHash(manifest);
        return manifest;
    }

    /// <summary>
    /// Runs the production cleanup reader's synchronous bridge on the calling measurement worker.
    /// </summary>
    /// <param name="lease">
    /// The actual current lease, optionally bound to participating main admission.
    /// </param>
    /// <param name="path">
    /// The rollback member's relative path.
    /// </param>
    /// <returns>
    /// Fresh leased bytes, or null if the actual file is absent.
    /// </returns>
    private byte[]? ReadCleanupCostBytes(FileSystemManager.CanonicalWriteLease lease, string path) =>
        _fs.ReadFileBytesAsync(lease, path).GetAwaiter().GetResult();

    /// <summary>
    /// Invokes exactly the generic destructive validator used by production cleanup with a substituted reader.
    /// </summary>
    /// <param name="manifest">
    /// The detached manifest with matching exact-byte hashes.
    /// </param>
    /// <param name="envelope">
    /// The portable integrity envelope, including deliberately corrupted refusal inputs.
    /// </param>
    /// <param name="reader">
    /// The actual content source for rollback hashing; no snapshot content read is implied.
    /// </param>
    /// <param name="payload">
    /// The validated detached payload on success, or null on the tested refusals.
    /// </param>
    /// <param name="failure">
    /// The validator's original classification, including authorized on success.
    /// </param>
    /// <returns>
    /// True only if the existing destructive validator accepts the envelope and rollback bytes.
    /// </returns>
    private static bool ValidateCleanupCost(TestManifest manifest, string envelope, Func<string, byte[]?> reader,
        out PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload? payload, out string failure) =>
        PendingTurnSnapshotAuthority.TryValidateManifestForDestructiveAuthority(manifest, envelope, ManifestJsonOpts,
            static m => m.ManifestPayloadHash, static (m, h) => m.ManifestPayloadHash = h,
            static m => m.SessionId, static m => m.RequestId, static m => m.TurnNumber,
            static m => m.Files, static m => m.SnapshotFileHashes, static m => m.ClientOwnedValidationHashes,
            static m => m.RollbackBaselineFiles, static m => m.SourceLabel, static m => m.RollbackBackups,
            reader, out payload, out failure);
}
