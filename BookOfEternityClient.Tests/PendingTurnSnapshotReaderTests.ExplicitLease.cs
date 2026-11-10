using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PendingTurnSnapshotReaderTests
{
    [Theory]
    [InlineData("valid", null)]
    [InlineData("manifest", "pending_turn_snapshot_reader_manifest_invalid")]
    [InlineData("authority", "pending_turn_snapshot_reader_authority_invalid")]
    [InlineData("selected-original", "pending_turn_snapshot_reader_bytes_mismatch")]
    [InlineData("context", "pending_turn_snapshot_reader_context_stale")]
    public async Task ReadCurrent_UsesHeldLeaseWithoutAmbientContextForMissingSources(string source, string? code)
    {
        await _fs.WriteFileAtomicAsync(RequiredPath, "{\"schemaVersion\":1,\"occurrences\":[]}");
        await PrepareAsync();
        var snapshotPath = ReadRoot(LiveTurnPreparationService.PendingTurnSnapshotManifestPath)["files"]![RequiredPath]!.GetValue<string>();
        var original = File.ReadAllBytes(_fs.ResolvePath(snapshotPath));
        var path = source switch
        {
            "manifest" => LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            "authority" => PendingTurnSnapshotAuthority.AuthorityPath,
            "selected-original" => snapshotPath,
            "context" => LiveTurnPreparationService.TurnRequestPath,
            _ => null
        };
        if (path is not null) File.Delete(_fs.ResolvePath(path));
        var before = SnapshotTree();
        PendingTurnSnapshotReadResult result;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            Task<PendingTurnSnapshotReadResult> read;
            // Retain the real explicit lease, but deliberately remove ambient
            // execution-context evidence. A missing read cannot acquire it again.
            using (ExecutionContext.SuppressFlow())
                read = Task.Run(() => PendingTurnSnapshotReader.ReadCurrent(_fs, lease, [RequiredPath]));
            result = await read;
        }
        if (code is null)
        {
            Assert.True(result.Success, Describe(result.Issues));
            Assert.Equal(original, result.Snapshot!.ReadRequiredBytes(RequiredPath));
        }
        else
        {
            Assert.False(result.Success); Assert.Null(result.Snapshot);
            Assert.Contains(result.Issues, issue => issue.Code == code);
        }
        AssertTreeEqual(before, SnapshotTree());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadCurrent_RejectsPhysicalDriftDespitePrimedDeclaredSourceData(bool remove)
    {
        await _fs.WriteFileAtomicAsync(RequiredPath, "{\"schemaVersion\":1,\"occurrences\":[]}");
        await PrepareAsync();
        var snapshotPath = ReadRoot(LiveTurnPreparationService.PendingTurnSnapshotManifestPath)["files"]![RequiredPath]!.GetValue<string>();
        var original = File.ReadAllBytes(_fs.ResolvePath(snapshotPath));
        var before = SnapshotTree();
        var counters = new CanonicalReadActivityAccumulator();
        var timings = new OperationTimingAccumulator();
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            { PassiveCanonicalReads = counters, PassiveOperationTimings = timings });
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            files.GetOrCreateSessionGeneration(lease);
            using var window = timings.StartWindow();
            using var owner = files.BeginSourceDataOperationIfNeeded();
            Assert.NotNull(owner);
            Assert.Equal(original, files.ReadFileBytesSync(lease, snapshotPath));
            Assert.Equal(original, files.ReadFileBytesSync(lease, snapshotPath));
            Assert.Equal(1, counters.ReadCount(snapshotPath));
            var hits = window.CaptureNamedCounts("LeaseDataHit.Reuse").Completed;
            Assert.True(hits > 0, "The existing declared source service must actually be primed.");
            var physical = files.ResolvePath(snapshotPath);
            var key = Path.GetRelativePath(_root, physical).Replace('\\', '/');
            if (remove) { File.Delete(physical); before.Remove(key); }
            else
            {
                var changed = FileSystemManager.EncodeUtf8WithPreamble("{\"schemaVersion\":1,\"occurrences\":[{}]}");
                File.WriteAllBytes(physical, changed); before[key] = changed;
            }
            var result = PendingTurnSnapshotReader.ReadCurrent(files, lease, [RequiredPath]);
            Assert.False(result.Success); Assert.Null(result.Snapshot);
            Assert.Contains(result.Issues, issue => issue.Code == "pending_turn_snapshot_reader_bytes_mismatch");
            Assert.Equal(hits, window.CaptureNamedCounts("LeaseDataHit.Reuse").Completed);
            if (!remove) Assert.True(counters.ReadCount(snapshotPath) > 1);
            await Assert.ThrowsAsync<InvalidDataException>(owner!.CompleteAsync);
        }
        AssertTreeEqual(before, SnapshotTree());
    }

    [Fact]
    public async Task ReadCurrent_CannotRetireUnrelatedPendingSourceDrift()
    {
        const string unrelated = "ordinary-reader-unrelated.txt";
        await _fs.WriteFileAtomicAsync(unrelated, "original");
        await _fs.WriteFileAtomicAsync(RequiredPath, "{\"schemaVersion\":1,\"occurrences\":[]}");
        await PrepareAsync();
        var before = SnapshotTree();
        var counters = new CanonicalReadActivityAccumulator();
        var timings = new OperationTimingAccumulator();
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            { PassiveCanonicalReads = counters, PassiveOperationTimings = timings });
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            files.GetOrCreateSessionGeneration(lease);
            using var window = timings.StartWindow();
            using var owner = files.BeginSourceDataOperationIfNeeded();
            Assert.NotNull(owner);
            Assert.Equal("original", files.ReadFileSync(lease, unrelated));
            Assert.Equal("original", files.ReadFileSync(lease, unrelated));
            Assert.Equal(1, counters.ReadCount(unrelated));
            Assert.True(window.CaptureNamedCounts("LeaseDataHit.Reuse").Completed > 0);
            var changed = FileSystemManager.EncodeUtf8WithPreamble("modified");
            var physical = files.ResolvePath(unrelated);
            File.WriteAllBytes(physical, changed);
            before[Path.GetRelativePath(_root, physical).Replace('\\', '/')] = changed;
            var result = PendingTurnSnapshotReader.ReadCurrent(files, lease, [RequiredPath]);
            Assert.True(result.Success, Describe(result.Issues));
            Assert.NotNull(result.Snapshot);
            // A successful narrow reader does not discharge another consumer's
            // still-pending original DATA or authorize an effect from it.
            await Assert.ThrowsAsync<InvalidDataException>(() => owner!.VerifyAsync(lease));
        }
        AssertTreeEqual(before, SnapshotTree());
    }
}
