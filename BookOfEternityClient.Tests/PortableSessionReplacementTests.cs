using System.Text;
using BookOfEternityClient.Core;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableSessionReplacementTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-session-replacement-" + Guid.NewGuid().ToString("N"));
    private readonly string _generation = Guid.NewGuid().ToString("N");
    private readonly FileSystemManager _files;
    private readonly byte[] _generationBytes;

    public PortableSessionReplacementTests()
    {
        Directory.CreateDirectory(_root);
        _files = Manager();
        var encoding = new UnicodeEncoding(false, true);
        _generationBytes = encoding.GetPreamble().Concat(encoding.GetBytes(
            " { \"schemaVersion\" : 1, \"generationId\" : \"" + _generation + "\", \"extension\" : true }\n")).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(_files.SessionGenerationPath)!);
        File.WriteAllBytes(_files.SessionGenerationPath, _generationBytes);
    }

    private FileSystemManager Manager(FileSystemManagerHooks? hooks = null) =>
        new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);
    private string PathFor(string relative) => Path.Combine(_files.GameSessionPath, relative.Replace('/', Path.DirectorySeparatorChar));
    private void Seed(string relative, byte[] bytes)
    {
        var path = PathFor(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }
    private Dictionary<string, byte[]> SeedSelected() => new()
    {
        ["input/nested/request.json"] = [0xEF, 0xBB, 0xBF, 0x7B, 0x7D],
        ["game_state/core/state.bin"] = [0xFF, 0, 0xFE],
        ["output/nested/answer.json"] = [],
        ["ready/complete.json"] = [4],
        ["lore/current_world/note.txt"] = [5],
        ["stories/nested/story.txt"] = [6],
        ["worker_tasks/task/packet.bin"] = [7],
        ["worker_proposals/proposal/content.bin"] = [8],
        ["game_state/control/gm_worker_latest_validation_repair_task.json"] = [9],
        ["game_state/control/validation_repair_ready.json"] = [10]
    };
    private void SeedAll(IReadOnlyDictionary<string, byte[]> images)
    {
        foreach (var image in images) Seed(image.Key, image.Value);
    }
    private void AssertImages(IReadOnlyDictionary<string, byte[]> images, bool present)
    {
        foreach (var image in images)
        {
            Assert.Equal(present, File.Exists(PathFor(image.Key)));
            if (present) Assert.Equal(image.Value, File.ReadAllBytes(PathFor(image.Key)));
        }
    }
    private async Task<string?> ReadGeneration()
    {
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        return _files.ReadLocalGenerationSnapshot(lease).Binding.Id;
    }

    [Fact]
    public async Task Clear_DeletesCompleteSelectedSetAndPreservesExcludedBytes()
    {
        var selected = SeedSelected(); SeedAll(selected);
        var preserved = new Dictionary<string, byte[]>
        {
            ["config.json"] = [0xFF, 0],
            ["input/nested/keep.txt"] = [],
            ["output/keep.bin"] = [1],
            ["ready/keep.txt"] = [2],
            ["game_state/control/gm_bridge_status.json"] = [3],
            ["game_state/control/gm_cli_window_binding.json"] = [4],
            ["game_state/control/gm_context_pack/nested/context.bin"] = [5],
            ["saves/manual_saves/keep.bin"] = [6],
            ["images/keep.bin"] = [7],
            ["mods/keep.bin"] = [8]
        };
        SeedAll(preserved);
        Seed("game_state/control/gm_context_pack_old/selected.bin", [9]);
        await _files.ClearGameStateAsync();
        AssertImages(selected, present: false);
        AssertImages(preserved, present: true);
        Assert.False(File.Exists(PathFor("game_state/control/gm_context_pack_old/selected.bin")));
        Assert.NotEqual(_generation, await ReadGeneration());
        Assert.True(Directory.Exists(PathFor("worker_proposals/proposal")));
    }

    [Theory]
    [InlineData((int)TrustedLocalPublicationPhase.IntentStaged)]
    [InlineData((int)TrustedLocalPublicationPhase.IntentPublished)]
    [InlineData((int)TrustedLocalPublicationPhase.MemberStaged)]
    [InlineData((int)TrustedLocalPublicationPhase.MemberPublished)]
    [InlineData((int)TrustedLocalPublicationPhase.CommitStaged)]
    [InlineData((int)TrustedLocalPublicationPhase.Committed)]
    [InlineData((int)TrustedLocalPublicationPhase.CleanupMember)]
    [InlineData((int)TrustedLocalPublicationPhase.CleanupComplete)]
    public async Task Clear_PublicationCutKeepsWholeOldSetOrWholeCommittedSet(int cutValue)
    {
        var selected = SeedSelected(); SeedAll(selected);
        var cut = (TrustedLocalPublicationPhase)cutValue;
        var reached = false;
        var files = Manager(new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, _) =>
            {
                if (phase != cut) return;
                reached = true;
                throw new CutFailure();
            }
        });
        var failure = await Record.ExceptionAsync(() => files.ClearGameStateAsync());
        Assert.True(reached, "Clear did not use the common publication decision.");
        var committed = cut is TrustedLocalPublicationPhase.Committed or
            TrustedLocalPublicationPhase.CleanupMember or TrustedLocalPublicationPhase.CleanupComplete;
        if (committed) Assert.Null(failure);
        else Assert.IsType<CutFailure>(failure);
        // A new lease invokes actual FileSystemManager admission/recovery.
        var actualGeneration = await ReadGeneration();
        AssertImages(selected, present: !committed);
        if (committed) Assert.NotEqual(_generation, actualGeneration);
        else
        {
            Assert.Equal(_generation, actualGeneration);
            Assert.Equal(_generationBytes, File.ReadAllBytes(_files.SessionGenerationPath));
        }
        Assert.False(File.Exists(Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
    }

    [Fact]
    public async Task Clear_InvalidRequiredDirectoryRejectsBeforeGenerationOrSelectedMutation()
    {
        var selected = SeedSelected(); SeedAll(selected);
        Seed("saves/autosaves", [42]);
        await Assert.ThrowsAsync<InvalidDataException>(() => _files.ClearGameStateAsync());
        AssertImages(selected, present: true);
        Assert.Equal(_generationBytes, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(PathFor("saves/autosaves")));
    }

    [Fact]
    public async Task Clear_InvalidLockLinkRetainsOutsideBytesAndAllSelectedMembers()
    {
        var selected = SeedSelected(); SeedAll(selected);
        var outside = Path.Combine(_root, "outside-lock.bin"); File.WriteAllBytes(outside, [42]);
        var link = PathFor("game_state/control/local_ui_session_lock.json");
        File.CreateSymbolicLink(link, outside);
        await Assert.ThrowsAsync<InvalidDataException>(() => _files.ClearGameStateAsync());
        AssertImages(selected, present: true);
        Assert.Equal(_generationBytes, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.NotNull(new FileInfo(link).LinkTarget);
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(outside));
    }

    [Fact]
    public async Task Clear_OpaqueContextPackLinkIsPreservedWithoutFollowingIt()
    {
        var selected = SeedSelected(); SeedAll(selected);
        var outside = Path.Combine(_root, "outside-pack"); Directory.CreateDirectory(outside);
        var external = Path.Combine(outside, "keep.bin"); File.WriteAllBytes(external, [42]);
        var excluded = PathFor("game_state/control/gm_context_pack");
        Directory.CreateSymbolicLink(excluded, outside);
        await _files.ClearGameStateAsync();
        AssertImages(selected, present: false);
        Assert.NotNull(new DirectoryInfo(excluded).LinkTarget);
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(external));
    }

    [Fact]
    public async Task Clear_ManifestlessLegacyEvidenceRemainsBlockedAndIntact()
    {
        var selected = SeedSelected(); SeedAll(selected);
        const string evidence = "game_state/control/explorer_local_turn_rollback/unknown/bytes";
        Seed(evidence, [42]);
        await Assert.ThrowsAsync<InvalidDataException>(() => _files.ClearGameStateAsync());
        AssertImages(selected, present: true);
        Assert.Equal(_generationBytes, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(PathFor(evidence)));
    }

    [Fact]
    public void PendingInspector_IgnoresEmptySnapshotDescendantsButCountsZeroByteFiles()
    {
        var child = PathFor(BrowserPendingTurnInspector.PendingTurnSnapshotDirectory + "/game_state/core");
        Directory.CreateDirectory(child);
        Assert.False(BrowserPendingTurnInspector.Build(_files).HasActiveGmTurn);
        File.WriteAllBytes(Path.Combine(child, "empty.json"), []);
        Assert.True(BrowserPendingTurnInspector.Build(_files).HasActiveGmTurn);
    }

    [Fact]
    public async Task Rotate_WorkerCleanupAndGenerationRollbackTogether()
    {
        var workers = new Dictionary<string, byte[]>
        {
            ["worker_tasks/task/task.json"] = [1],
            ["worker_proposals/proposal/proposal.json"] = [2],
            ["game_state/control/gm_worker_latest_validation_repair_task.json"] = [3],
            ["game_state/control/validation_repair_ready.json"] = []
        };
        SeedAll(workers); Seed("game_state/core/keep.bin", [42]);
        var reached = false;
        var files = Manager(new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, _) =>
            {
                if (phase != TrustedLocalPublicationPhase.MemberPublished) return;
                reached = true; throw new CutFailure();
            }
        });
        await using (var lifecycle = await files.AcquireSessionLifecycleLeaseAsync())
        await using (var replacement = await files.AcquireSessionReplacementWriteLeaseAsync(lifecycle))
        {
            var failure = Record.Exception(() => files.RotateSessionGeneration(replacement));
            Assert.True(reached, "Rotation did not journal generation with worker cleanup.");
            Assert.IsType<CutFailure>(failure);
        }
        AssertImages(workers, present: true);
        Assert.Equal(_generationBytes, File.ReadAllBytes(_files.SessionGenerationPath));
        await using (var lifecycle = await _files.AcquireSessionLifecycleLeaseAsync())
        await using (var replacement = await _files.AcquireSessionReplacementWriteLeaseAsync(lifecycle))
            Assert.NotEqual(_generation, _files.RotateSessionGeneration(replacement));
        AssertImages(workers, present: false);
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(PathFor("game_state/core/keep.bin")));
    }

    private sealed class CutFailure : Exception { }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
