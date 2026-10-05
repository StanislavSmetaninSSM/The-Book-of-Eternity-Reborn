using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableWorkerTransactionTests : IDisposable
{
    private const string A = "game_state/world/worker-a.bin";
    private const string B = "game_state/world/worker-b.bin";
    private const string C = "game_state/world/worker-c.bin";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-worker-portable-" + Guid.NewGuid().ToString("N"));
    private readonly string _generation = Guid.NewGuid().ToString("N");
    private readonly FileSystemManager _files;
    private static readonly byte[] Before = [0xef, 0xbb, 0xbf, 0xff, 0];
    private static readonly byte[] After = [0xfe, 0, 7];
    private CanonicalWorkerApplyChange[] Changes => [new(A, Before, After), new(B, null, []), new(C, [], null)];
    private string Active => Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");

    public PortableWorkerTransactionTests()
    {
        _files = Manager();
        Directory.CreateDirectory(Path.GetDirectoryName(_files.SessionGenerationPath)!);
        File.WriteAllBytes(_files.SessionGenerationPath, Generation(_generation));
        Seed(A, Before); Seed(C, []);
        Seed("game_state/history/accepted.json", [44, 55]);
    }

    private FileSystemManager Manager(FileSystemManagerHooks? hooks = null) =>
        new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);
    private static byte[] Generation(string id) => JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = id });
    private void Seed(string path, byte[] bytes)
    {
        var full = _files.ResolvePath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); File.WriteAllBytes(full, bytes);
    }
    private void AssertState(bool after)
    {
        Assert.Equal(after ? After : Before, File.ReadAllBytes(_files.ResolvePath(A)));
        Assert.Equal(after, File.Exists(_files.ResolvePath(B)));
        if (after) Assert.Empty(File.ReadAllBytes(_files.ResolvePath(B)));
        Assert.Equal(!after, File.Exists(_files.ResolvePath(C)));
        if (!after) Assert.Empty(File.ReadAllBytes(_files.ResolvePath(C)));
        Assert.Equal(Generation(_generation), File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal(new byte[] { 44, 55 }, File.ReadAllBytes(_files.ResolvePath("game_state/history/accepted.json")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompleteSetRemainsPendingUntilExplicitDecision(bool commit)
    {
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var transaction = await _files.BeginWorkerApplyTransactionAsync(lease, Changes);
        AssertState(after: true);
        Assert.True(File.Exists(Active));
        Assert.False(File.Exists(_files.ActiveWorkerApplyTransactionJournalPath));
        Assert.Equal(After, await _files.ReadFileBytesAsync(lease, A)); // Validation observes staged canonical state.
        if (commit) _files.CommitWorkerApplyTransaction(lease, transaction);
        else Assert.Empty(await _files.RollbackWorkerApplyTransactionAsync(lease, transaction));
        AssertState(after: commit);
        Assert.False(File.Exists(Active));
    }

    [Theory]
    [InlineData("writer")]
    [InlineData("publisher")]
    [InlineData("direct")]
    [InlineData("recovery")]
    [InlineData("stream")]
    [InlineData("directory")]
    [InlineData("worker")]
    public async Task PendingDecisionRefusesNestedPublicationAndRecoveryWithoutUndo(string nested)
    {
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var transaction = await _files.BeginWorkerApplyTransactionAsync(lease, Changes);
        var evidence = File.ReadAllBytes(Active);
        var publisher = new TrustedLocalFilePublication(_files, new TrustedLocalFileScope([_root]));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(async () =>
        {
            switch (nested)
            {
                case "writer": await _files.WriteFileAtomicBytesAsync(lease, "game_state/new-parent/nested.bin", [7]); break;
                case "publisher": publisher.PublishWithOutcome(lease, TrustedLocalGeneration.Existing(_generation), [new(_files.ResolvePath(A), After, [7])]); break;
                case "direct": publisher.Publish(lease, TrustedLocalGeneration.Existing(_generation), [new(_files.ResolvePath(A), After, [7])]); break;
                case "recovery": publisher.Recover(lease); break;
                case "stream": await _files.OpenOrdinaryReadFileAsync(lease, A); break;
                case "directory": _files.DeleteDirectoryTree(lease, "game_state/world"); break;
                case "worker": await _files.BeginWorkerApplyTransactionAsync(lease, Changes); break;
            }
        });
        AssertState(after: true);
        Assert.Equal(evidence, File.ReadAllBytes(Active));
        Assert.False(Directory.Exists(_files.ResolvePath("game_state/new-parent")));
        Assert.Empty(await _files.RollbackWorkerApplyTransactionAsync(lease, transaction));
        AssertState(after: false);
    }

    [Fact]
    public async Task LateBaselineMismatchRefusesWholeSetWithoutMutation()
    {
        Seed(C, [9]);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => _files.BeginWorkerApplyTransactionAsync(lease, Changes));
        Assert.Equal(Before, File.ReadAllBytes(_files.ResolvePath(A)));
        Assert.False(File.Exists(_files.ResolvePath(B)));
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(_files.ResolvePath(C)));
        Assert.False(File.Exists(Active));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RollbackPreflightsLateUnknownMemberAndGenerationBeforeAnyRestore(bool generation)
    {
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var transaction = await _files.BeginWorkerApplyTransactionAsync(lease, Changes);
        var unknown = generation ? Generation(Guid.NewGuid().ToString("N")) : new byte[] { 99 };
        var path = generation ? _files.SessionGenerationPath : _files.ResolvePath(C);
        File.WriteAllBytes(path, unknown);
        var evidence = File.ReadAllBytes(Active);
        Assert.NotEmpty(await _files.RollbackWorkerApplyTransactionAsync(lease, transaction));
        Assert.Equal(After, File.ReadAllBytes(_files.ResolvePath(A)));
        Assert.Empty(File.ReadAllBytes(_files.ResolvePath(B)));
        Assert.Equal(unknown, File.ReadAllBytes(path));
        Assert.Equal(evidence, File.ReadAllBytes(Active));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => _files.WriteFileAtomicBytesAsync(lease, A, [8]));
        Assert.Equal(evidence, File.ReadAllBytes(Active));
    }

    [Fact]
    public async Task OldHandleCannotCommitOrRollbackNewerDecision()
    {
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var old = await _files.BeginWorkerApplyTransactionAsync(lease, Changes);
        Assert.Empty(await _files.RollbackWorkerApplyTransactionAsync(lease, old));
        var current = await _files.BeginWorkerApplyTransactionAsync(lease, Changes);
        var evidence = File.ReadAllBytes(Active);
        Assert.ThrowsAny<InvalidOperationException>(() => _files.CommitWorkerApplyTransaction(lease, old));
        Assert.NotEmpty(await _files.RollbackWorkerApplyTransactionAsync(lease, old));
        Assert.Equal(evidence, File.ReadAllBytes(Active)); AssertState(after: true);
        Assert.Empty(await _files.RollbackWorkerApplyTransactionAsync(lease, current));
    }

    [Fact]
    public async Task CommitCleanupFailurePreservesCommitForNextWriter()
    {
        var observed = false;
        var files = Manager(new FileSystemManagerHooks { LocalPublicationObserver = (phase, _) =>
        { if (phase == TrustedLocalPublicationPhase.CleanupMember) { observed = true; throw new IOException("worker cleanup fault"); } } });
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var transaction = await files.BeginWorkerApplyTransactionAsync(lease, Changes);
            files.CommitWorkerApplyTransaction(lease, transaction);
        }
        Assert.True(observed); Assert.True(File.Exists(Active)); AssertState(after: true);
        await using (var recovery = await Manager().AcquireCanonicalWriteLeaseAsync()) { }
        AssertState(after: true); Assert.False(File.Exists(Active));
    }

    [Fact]
    public async Task MissingGenerationIsNotInventedByWorkerAdmission()
    {
        File.Delete(_files.SessionGenerationPath);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => _files.BeginWorkerApplyTransactionAsync(lease, Changes));
        Assert.False(File.Exists(_files.SessionGenerationPath));
        Assert.Equal(Before, File.ReadAllBytes(_files.ResolvePath(A)));
        Assert.False(File.Exists(Active));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
