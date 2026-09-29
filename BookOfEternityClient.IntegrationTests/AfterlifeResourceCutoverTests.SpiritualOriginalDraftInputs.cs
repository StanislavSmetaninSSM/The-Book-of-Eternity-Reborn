using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Retains signed and live distributed siblings exactly while original capture remains write-free.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCapture_RetainsUntouchedDistributedDraftWithoutWritingFiles()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var lore = new byte[] { 0, 255, 10, 13 };
        await context.WriteExactBytesAsync("lore/independent.bin", lore);
        await context.WriteExactBytesAsync("world_profiles/unused.bin", new byte[] { 42 });
        await context.WriteExactBytesAsync("output/unread.bin", Array.Empty<byte>());
        await context.WriteExactBytesAsync("game_state/factions/unused.bin", new byte[] { 91 });
        await context.WriteExactBytesAsync("game_state/control/pending_unused_proposal.json", new byte[] { 12 });
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var signed = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease,
            [ValidationService.SpiritualWoundSourceSession.SoulPath]);
        Assert.True(signed.Success);
        Assert.Contains(ResourceMaterializationTestContext.DefinitionsPath,
            signed.Snapshot!.DeclaredOriginalLogicalPaths);
        Assert.DoesNotContain(ResourceMaterializationTestContext.DefinitionsPath,
            signed.Snapshot.CoveredLogicalPaths);
        var beforePaths = context.FileSystem.EnumerateFiles(lease, "*")
            .OrderBy(static path => path, StringComparer.Ordinal).ToArray();
        var before = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in beforePaths)
            before.Add(path, (await context.FileSystem.ReadFileBytesAsync(lease, path))!);

        var captured = await InvokeOriginalCaptureAsync(context.Validator,
            "CaptureSpiritualOriginalTurnAsync", lease);
        AssertNoConflictFrameErrors(OriginalCaptureIssues(captured));
        var capture = OriginalCaptureProperty(captured, "Capture")!;
        var draft = Assert.IsType<SpiritualOriginalDraftInputs>(OriginalCaptureField(capture, "_draftInputs"));
        Assert.Equal(lore, draft.ReadImage("lore/independent.bin").Bytes);
        Assert.Equal(new byte[] { 42 }, draft.ReadImage("world_profiles/unused.bin").Bytes);
        Assert.Empty(draft.ReadImage("output/unread.bin").Bytes!);
        Assert.Equal(new byte[] { 91 }, draft.ReadImage("game_state/factions/unused.bin").Bytes);
        Assert.Equal(new byte[] { 12 },
            draft.ReadImage("game_state/control/pending_unused_proposal.json").Bytes);
        Assert.False(draft.ReadImage("game_state/meta/guardians.json").Existed);
        Assert.Contains(ResourceMaterializationTestContext.DefinitionsPath, draft.PathInventory);
        Assert.DoesNotContain("game_state/control/pending_turn_snapshot.json", draft.PathInventory);
        Assert.DoesNotContain("game_state/control/pending_turn_snapshot.authority.json", draft.PathInventory);
        Assert.Throws<KeyNotFoundException>(() => draft.ReadImage("lore/not-retained.bin"));

        var afterPaths = context.FileSystem.EnumerateFiles(lease, "*")
            .OrderBy(static path => path, StringComparer.Ordinal).ToArray();
        Assert.Equal(beforePaths, afterPaths);
        foreach (var path in afterPaths)
            Assert.Equal(before[path], await context.FileSystem.ReadFileBytesAsync(lease, path));
    }

    /// <summary>
    /// Keeps excluded physical manifest freshness separate from candidate draft reads.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCapture_ExcludedPhysicalManifestStillGuardsFreshness()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await InvokeOriginalCaptureAsync(context.Validator,
            "CaptureSpiritualOriginalTurnAsync", lease);
        AssertNoConflictFrameErrors(OriginalCaptureIssues(captured));
        var capture = OriginalCaptureProperty(captured, "Capture")!;
        var draft = Assert.IsType<SpiritualOriginalDraftInputs>(OriginalCaptureField(capture, "_draftInputs"));
        Assert.DoesNotContain("game_state/control/pending_turn_snapshot.json", draft.PathInventory);
        var manifestPath = LiveTurnPreparationService.PendingTurnSnapshotManifestPath;
        var original = (await context.FileSystem.ReadFileBytesAsync(lease, manifestPath))!;
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, manifestPath,
            original.Concat(new byte[] { 10 }).ToArray());
        var checkedInputs = await InvokeOriginalCaptureAsync(capture, "CheckRetainedInputsAsync", lease);
        Assert.Contains(Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(checkedInputs),
            issue => issue.Code == "spiritual_original_input_changed" && issue.FilePath == manifestPath);
    }

    /// <summary>
    /// Rejects a request identity that no longer agrees with the signed original snapshot.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCapture_RejectsStaleOriginalRequestBeforeDraftBinding()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var requestPath = LiveTurnPreparationService.TurnRequestPath;
        var request = JsonNode.Parse((await context.FileSystem.ReadFileAsync(requestPath))!)!.AsObject();
        request["requestId"] = "stale-original-request";
        await context.WriteExactJsonAsync(requestPath, request.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await InvokeOriginalCaptureAsync(context.Validator,
            "CaptureSpiritualOriginalTurnAsync", lease);
        Assert.Null(OriginalCaptureProperty(captured, "Capture"));
        Assert.Contains(OriginalCaptureIssues(captured),
            issue => issue.Code == "pending_turn_snapshot_reader_context_stale");
    }

    /// <summary>
    /// Keeps Explorer rollback manifests and markers outside the detached draft payload.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCapture_ExcludesExplorerRollbackTransactionFiles()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var rollbackRoot = ExplorerLocalTurnRollbackArtifacts.Root + "/browser_write/probe";
        var manifestPath = rollbackRoot + "/browser_write_manifest.json";
        var markerPath = rollbackRoot + "/browser_write_committed.marker";
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, manifestPath, new byte[] { 1, 2 });
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, markerPath, new byte[] { 3 });
        var beforeManifest = await context.FileSystem.ReadFileBytesAsync(lease, manifestPath);
        var beforeMarker = await context.FileSystem.ReadFileBytesAsync(lease, markerPath);
        var captured = await InvokeOriginalCaptureAsync(context.Validator,
            "CaptureSpiritualOriginalTurnAsync", lease);
        AssertNoConflictFrameErrors(OriginalCaptureIssues(captured));
        var capture = OriginalCaptureProperty(captured, "Capture")!;
        var draft = Assert.IsType<SpiritualOriginalDraftInputs>(OriginalCaptureField(capture, "_draftInputs"));
        Assert.DoesNotContain(draft.PathInventory, path =>
            path.StartsWith(ExplorerLocalTurnRollbackArtifacts.Root + "/", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(beforeManifest, await context.FileSystem.ReadFileBytesAsync(lease, manifestPath));
        Assert.Equal(beforeMarker, await context.FileSystem.ReadFileBytesAsync(lease, markerPath));
    }

    /// <summary>
    /// Rejects noncanonical draft-root casing before a live dynamic sibling can be dropped.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCapture_RejectsCaseAliasedDraftRootWithoutWritingFiles()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var sessionPath = context.FileSystem.GameSessionPath;
        var canonicalRoot = Path.Combine(sessionPath, "lore");
        var temporaryRoot = Path.Combine(sessionPath, "lore_case_stage");
        var aliasedRoot = Path.Combine(sessionPath, "Lore");
        if (Directory.Exists(canonicalRoot))
        {
            Directory.Move(canonicalRoot, temporaryRoot);
            Directory.Move(temporaryRoot, aliasedRoot);
        }
        else
        {
            Directory.CreateDirectory(aliasedRoot);
        }
        var aliasPath = Path.Combine(aliasedRoot, "unread.bin");
        await File.WriteAllBytesAsync(aliasPath, new byte[] { 0, 255 });
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var before = await File.ReadAllBytesAsync(aliasPath);
        var captured = await InvokeOriginalCaptureAsync(context.Validator,
            "CaptureSpiritualOriginalTurnAsync", lease);
        Assert.Null(OriginalCaptureProperty(captured, "Capture"));
        Assert.Contains(OriginalCaptureIssues(captured),
            issue => issue.Code == "spiritual_original_input_path_alias");
        Assert.Equal(before, await File.ReadAllBytesAsync(aliasPath));
    }
}
