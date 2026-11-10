using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;

namespace BookOfEternityClient.Tests;

// Body-required Windows qualification. Discovery/build on Linux is not execution.
public sealed class GmMainMetadataWindowsTests : IDisposable
{
    private readonly string _root;
    public GmMainMetadataWindowsTests()
    {
        Assert.True(OperatingSystem.IsWindows(), "This category requires native Windows bodies; do not count a platform return as PASS.");
        _root = Path.Combine(Path.GetTempPath(), "main-metadata-native-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }
    private string Root(bool extended) => extended ? PhysicalFileAuthority.ToWindowsExtendedPath(_root) : _root;
    private static GmSessionRunRecord Record(string root, GmSessionRunDisposition state = GmSessionRunDisposition.Prepared)
    {
        var id = new GmSessionRunIdentity(root, Guid.NewGuid().ToString("N"), Guid.Empty.ToString("N"), 1,
            GmSessionRunBackend.WindowsJob, Guid.NewGuid().ToString("N"), "fixture-native-observation");
        return new(1, id, state, null);
    }
    public static IEnumerable<object[]> Faults()
    {
        foreach (var extended in new[] { false, true })
        foreach (var prior in new[] { false, true })
        foreach (var stage in new[] { MainRunIoStage.NamespaceCreated, MainRunIoStage.Staged, MainRunIoStage.FileFlushed,
                     MainRunIoStage.Renamed, MainRunIoStage.WindowsFileAcknowledged, MainRunIoStage.Readback })
            if (!prior || stage != MainRunIoStage.NamespaceCreated) yield return [extended, prior, (int)stage];
    }
    [Theory]
    [MemberData(nameof(Faults))]
    public async Task NativePublicationFaultRetainsExactPlanAndOriginalGuard(bool extended, bool prior, int selectedStage)
    {
        using var guard = await GmMainOwnerGuard.AcquireAsync(Root(extended));
        var before = Record(guard.Root);
        if (prior) new GmSessionRunPersistence(guard).Publish(null, before);
        var expectedBefore = prior ? GmSessionRunRecordCodec.Encode(before) : null;
        var after = prior ? before with { Disposition = GmSessionRunDisposition.Running } : before;
        var hits = 0;
        var observed = new List<MainRunIoStage>();
        var disk = new GmSessionRunPersistence(guard, stage => {
            observed.Add(stage);
            if ((int)stage == selectedStage && hits++ == 0) throw new IOException("native selected metadata cut");
        });
        Assert.Throws<IOException>(() => disk.Publish(expectedBefore, after));
        Assert.Equal(1, hits);
        Assert.True(disk.HasDebt);
        Assert.Throws<IOException>(() => disk.Publish(expectedBefore, after with { Disposition = GmSessionRunDisposition.Stopping }));
        await Assert.ThrowsAsync<IOException>(async () => {
            using var contender = await GmMainOwnerGuard.AcquireAsync(Root(!extended), attempts: 1);
        });
        disk.Retry();
        Assert.False(disk.HasDebt);
        Assert.Equal(GmSessionRunRecordCodec.Encode(after), GmSessionRunPersistence.Read(Root(extended)));
        Assert.Single(Directory.GetFiles(disk.DirectoryPath));
        Assert.Contains(MainRunIoStage.WindowsFileAcknowledged, observed);
        Assert.DoesNotContain(MainRunIoStage.DirectoryFlushed, observed);
        Assert.DoesNotContain(MainRunIoStage.BeforeNamespaceParentFlush, observed);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task NativeSharingRefusalRetainsBeforeAndRetriesSameReplacement(bool extended)
    {
        using var guard = await GmMainOwnerGuard.AcquireAsync(Root(extended));
        var before = Record(guard.Root);
        var disk = new GmSessionRunPersistence(guard);
        disk.Publish(null, before);
        var beforeBytes = GmSessionRunRecordCodec.Encode(before);
        var after = before with { Disposition = GmSessionRunDisposition.Running };
        using (var share = new FileStream(disk.RecordPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Throws<IOException>(() => disk.Publish(beforeBytes, after));
            Assert.True(disk.HasDebt);
            Assert.Equal(beforeBytes, File.ReadAllBytes(disk.RecordPath));
            Assert.Single(Directory.GetFiles(disk.DirectoryPath, "*.tmp"));
        }
        disk.Retry();
        Assert.False(disk.HasDebt);
        Assert.Equal(GmSessionRunRecordCodec.Encode(after), File.ReadAllBytes(disk.RecordPath));
        Assert.Single(Directory.GetFiles(disk.DirectoryPath));
    }
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task NativeForeignStageOrNamespaceRetainsDebtAndForeignEvidence(bool extended, bool foreignNamespace)
    {
        using var guard = await GmMainOwnerGuard.AcquireAsync(Root(extended));
        var hits = 0;
        var disk = new GmSessionRunPersistence(guard, stage => {
            if (stage == MainRunIoStage.Staged && ++hits == 1) throw new IOException("stage retained");
        });
        Assert.Throws<IOException>(() => disk.Publish(null, Record(guard.Root)));
        Assert.Equal(1, hits);
        var staged = Assert.Single(Directory.GetFiles(disk.DirectoryPath, "*.tmp"));
        var foreignPath = foreignNamespace ? Path.Combine(disk.DirectoryPath, "unknown.bin") : staged;
        var foreign = GmSessionRunRecordCodec.Encode(Record(guard.Root));
        File.WriteAllBytes(foreignPath, foreign);
        Assert.Throws<IOException>(() => disk.Retry());
        Assert.True(disk.HasDebt);
        Assert.Equal(foreign, File.ReadAllBytes(foreignPath));
        Assert.False(File.Exists(disk.RecordPath));
        Assert.True(File.Exists(staged));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task NativeColdPartialNamespaceCannotGuessFreshInitialization(bool extended)
    {
        string directory;
        using (var guard = await GmMainOwnerGuard.AcquireAsync(Root(extended)))
        {
            var hits = 0;
            var disk = new GmSessionRunPersistence(guard, stage => {
                if (stage == MainRunIoStage.NamespaceCreated && ++hits == 1) throw new IOException("creator interrupted");
            });
            Assert.Throws<IOException>(() => disk.Publish(null, Record(guard.Root)));
            Assert.Equal(1, hits);
            Assert.True(disk.HasDebt);
            directory = disk.DirectoryPath;
        }
        using var coldGuard = await GmMainOwnerGuard.AcquireAsync(Root(!extended));
        var cold = new GmSessionRunPersistence(coldGuard);
        Assert.Throws<FileNotFoundException>(() => GmSessionRunPersistence.Read(Root(extended)));
        Assert.Throws<FileNotFoundException>(() => cold.Publish(null, Record(coldGuard.Root)));
        Assert.False(cold.HasDebt);
        Assert.Empty(Directory.GetFileSystemEntries(directory));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task NativeUnknownAfterRenameCannotBeOverwrittenByFrozenRetry(bool extended)
    {
        using var guard = await GmMainOwnerGuard.AcquireAsync(Root(extended));
        var hits = 0;
        var disk = new GmSessionRunPersistence(guard, stage => {
            if (stage == MainRunIoStage.Renamed && ++hits == 1) throw new IOException("after rename interrupted");
        });
        Assert.Throws<IOException>(() => disk.Publish(null, Record(guard.Root)));
        Assert.Equal(1, hits);
        var unknown = GmSessionRunRecordCodec.Encode(Record(guard.Root));
        File.WriteAllBytes(disk.RecordPath, unknown);
        Assert.Throws<IOException>(() => disk.Retry());
        Assert.True(disk.HasDebt);
        Assert.Equal(unknown, File.ReadAllBytes(disk.RecordPath));
        Assert.Single(Directory.GetFiles(disk.DirectoryPath));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
