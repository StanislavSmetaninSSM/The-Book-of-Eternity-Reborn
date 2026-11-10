using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("late_changed")]
    [InlineData("late_missing")]
    [InlineData("missing_before_read")]
    [InlineData("physical_fault")]
    public async Task ObservedOriginal_GenuineBrowserRollbackRefusesDriftAbsenceAndFault(string mode)
    {
        try
        {
            var armed = false;
            var injections = 0;
            string? rollbackPath = null, selectedPath = null, fullRollback = null;
            byte[]? changed = null;
            var fault = new IOException("Actual observed rollback read fault.");
            var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    AfterCanonicalReadInitialValidationAsync = path =>
                    {
                        if (!armed) return Task.CompletedTask;
                        if (mode == "physical_fault" && path == rollbackPath)
                        { injections++; throw fault; }
                        if (mode.StartsWith("late_", StringComparison.Ordinal) && path == selectedPath && injections == 0)
                        {
                            injections++;
                            if (mode == "late_missing") File.Delete(fullRollback!);
                            else File.WriteAllBytes(fullRollback!, changed!);
                        }
                        return Task.CompletedTask;
                    }
                });
            var (_, staged) = await PrepareBrowserInputStagingAsync(withRollback: true, files: files);
            var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(staged.ManifestJson,
                LiveTurnPreparationService.ManifestJsonOptions)!;
            var row = manifest.RollbackBackups.First();
            rollbackPath = row.Value;
            fullRollback = files.ResolvePath(rollbackPath);
            selectedPath = manifest.Files["game_state/meta/soul_state.json"];
            var original = File.ReadAllBytes(fullRollback);
            changed = original.Concat(new byte[] { 0x20 }).ToArray();
            await using (var control = await files.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.True(PendingTurnSnapshotReader.ReadCurrent(files, control, ["game_state/meta/soul_state.json"]).Success);
                var observed = PendingTurnSnapshotReader.ReadObservedCurrent(files, control, ["game_state/meta/soul_state.json"]);
                Assert.True(observed.Success, JsonSerializer.Serialize(observed.Issues));
                observed.Evidence!.RequireCurrent(files, control, observed.Snapshot!);
                Assert.Contains(observed.Evidence.ExportDiagnosticData()["operations"]!.AsArray(),
                    item => item!["role"]!.GetValue<string>() == "RollbackArtifact" && item["path"]!.GetValue<string>() == rollbackPath);
            }
            var before = OriginalAdmissionTree();
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                if (mode == "missing_before_read") File.Delete(fullRollback);
                armed = true;
                PendingTurnSnapshotObservedReadResult? result = null;
                if (mode == "physical_fault")
                {
                    Assert.Same(fault, Assert.Throws<IOException>(() => result =
                        PendingTurnSnapshotReader.ReadObservedCurrent(files, lease, ["game_state/meta/soul_state.json"])));
                    Assert.Null(result);
                }
                else
                {
                    result = PendingTurnSnapshotReader.ReadObservedCurrent(files, lease, ["game_state/meta/soul_state.json"]);
                    Assert.False(result.Success);
                    Assert.Null(result.Snapshot);
                    Assert.Null(result.Evidence);
                    var issue = Assert.Single(result.Issues);
                    Assert.Equal(mode == "missing_before_read" ? "pending_turn_snapshot_reader_authority_invalid"
                        : "pending_turn_snapshot_reader_source_changed", issue.Code);
                    Assert.Equal(mode == "missing_before_read" ? PendingTurnSnapshotAuthority.AuthorityPath : row.Key, issue.FilePath);
                }
                armed = false;
                Assert.Equal(mode == "missing_before_read" ? 0 : 1, injections);
            }
            var treeKey = Path.GetRelativePath(files.BasePath, fullRollback).Replace('\\', '/');
            if (mode == "late_changed") before[treeKey] = changed;
            else if (mode != "physical_fault") Assert.True(before.Remove(treeKey));
            AssertOriginalAdmissionTree(before);
        }
        finally { CleanupObservedOriginalFixture(); }
    }

    [Theory]
    [InlineData("unchanged")]
    [InlineData("changed_restored")]
    [InlineData("unsupported")]
    public async Task ObservedOriginal_SecondaryPrivateCallbackRetainsOccurrencesAndStickyDrift(string mode)
    {
        // White-box mechanism control only. Current genuine producers emit unique
        // backup coordinates (or no backups). This in-memory secondary alias map
        // is NEVER written/resealed, and this private session NEVER issues authority.
        try
        {
            var tracing = false;
            var reads = 0;
            string? target = null;
            var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    AfterCanonicalReadInitialValidationAsync = path =>
                    { if (tracing && path == target) reads++; return Task.CompletedTask; }
                });
            var (_, staged) = await PrepareBrowserInputStagingAsync(withRollback: true, files: files);
            var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(staged.ManifestJson,
                LiveTurnPreparationService.ManifestJsonOptions)!;
            var originalRows = manifest.RollbackBackups.Take(2).ToArray();
            Assert.Equal(2, originalRows.Length);
            Assert.NotEqual(originalRows[0].Key, originalRows[1].Key);
            target = originalRows[0].Value;
            var full = files.ResolvePath(target);
            var bytes = File.ReadAllBytes(full);
            await using (var control = await files.AcquireCanonicalWriteLeaseAsync())
            {
                var genuine = PendingTurnSnapshotReader.ReadObservedCurrent(files, control, ["game_state/meta/soul_state.json"]);
                Assert.True(genuine.Success, JsonSerializer.Serialize(genuine.Issues));
                genuine.Evidence!.RequireCurrent(files, control, genuine.Snapshot!);
            }
            var before = OriginalAdmissionTree();
            // Secondary, derived coordinates only. No primary source bytes or hashes change.
            manifest.RollbackBackups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { [originalRows[0].Key] = target, [originalRows[1].Key] = target };
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                var type = typeof(PendingTurnSnapshotReader).GetNestedType("OriginalSourceReadSession", BindingFlags.NonPublic)!;
                var session = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null, args: [files, lease], culture: null)!;
                object? Invoke(string name, params object?[] args)
                {
                    try { return type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(session, args); }
                    catch (TargetInvocationException error) when (error.InnerException is not null)
                    { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
                }
                var issues = new List<ValidationIssue>();
                Assert.True((bool)Invoke("Begin", issues)!);
                tracing = true;
                Assert.Equal(bytes, Assert.IsType<byte[]>(Invoke("ReadRollbackBytesFromAuthorityCallback", manifest, target)));
                if (mode == "unsupported")
                {
                    Assert.Throws<InvalidOperationException>(() => Invoke("ReadRollbackBytesFromAuthorityCallback", manifest,
                        "game_state/unrequested-source.json"));
                    Assert.False((bool)Invoke("CloseAndRecheck", issues)!);
                    Assert.Equal("pending_turn_snapshot_reader_source_unavailable", Assert.Single(issues).Code);
                    Assert.Equal(1, reads);
                }
                else
                {
                    var second = mode == "unchanged" ? bytes : bytes.Concat(new byte[] { 0x20 }).ToArray();
                    if (mode != "unchanged") File.WriteAllBytes(full, second);
                    Assert.Equal(second, Assert.IsType<byte[]>(Invoke("ReadRollbackBytesFromAuthorityCallback", manifest, target)));
                    Assert.Equal(2, reads); // Both logical associations performed physical reads.
                    var operations = Assert.IsAssignableFrom<System.Collections.IEnumerable>(type.GetField("_operations",
                        BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session));
                    var associations = operations.Cast<object>().Select(operation =>
                    {
                        object? Field(string name) => operation.GetType().GetField(name,
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(operation);
                        return ((int)Field("Ordinal")!, (string)Field("LogicalPath")!, (string)Field("Path")!);
                    }).ToArray();
                    Assert.Equal(new[] { (0, originalRows[0].Key, target), (1, originalRows[1].Key, target) }, associations);
                    if (mode != "unchanged") File.WriteAllBytes(full, bytes);
                    Assert.Equal(bytes, File.ReadAllBytes(full)); // Source restored BEFORE closing.
                    var closed = (bool)Invoke("CloseAndRecheck", issues)!;
                    Assert.Equal(mode == "unchanged", closed);
                    if (mode == "unchanged") { Assert.Empty(issues); Assert.Equal(4, reads); }
                    else
                    {
                        var issue = Assert.Single(issues);
                        Assert.Equal("pending_turn_snapshot_reader_source_changed", issue.Code);
                        Assert.Equal(originalRows[1].Key, issue.FilePath);
                        Assert.Contains("observation 1", issue.Actual);
                        Assert.Equal(2, reads); // Sticky capture drift refused before end-state replay.
                    }
                }
                tracing = false;
                // Deliberately no call to Issue, no Snapshot/Evidence construction.
            }
            AssertOriginalAdmissionTree(before);
        }
        finally { CleanupObservedOriginalFixture(); }
    }

    private void CleanupObservedOriginalFixture()
    {
        Exception? failure = null;
        try { if (Directory.Exists(_rootPath)) Directory.Delete(_rootPath, recursive: true); }
        catch (Exception error) { failure = error; }
        Assert.NotNull(_directGachaOutput);
        _directGachaOutput!.WriteLine(JsonSerializer.Serialize(new { CleanupOwnedRoot = _rootPath,
            OwnedFixtureRemoved = !Directory.Exists(_rootPath), CleanupFailure = failure?.ToString() }));
        Assert.Null(failure);
        Assert.False(Directory.Exists(_rootPath));
    }
}
