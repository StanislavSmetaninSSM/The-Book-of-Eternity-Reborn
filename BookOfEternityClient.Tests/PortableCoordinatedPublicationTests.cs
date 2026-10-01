using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableCoordinatedPublicationTests : IDisposable
{
    private const string ReplacePath = "game_state/meta/coordinated_replace.json";
    private const string CreatePath = "game_state/meta/coordinated_create.json";
    private const string DeletePath = "game_state/meta/coordinated_delete.json";
    private const string NextJson = "{\"value\":\"accepted\"}";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-coordinated-publication-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _files;
    private readonly byte[] _replaceBefore = [0xEF, 0xBB, 0xBF, 0xFF, 0];
    private readonly byte[] _deleteBefore = [];
    private Action<TrustedLocalPublicationPhase, int>? _observer;
    private string Journal => Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1");
    private string Active => Path.Combine(Journal, "active.json");

    public PortableCoordinatedPublicationTests()
    {
        _files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) => _observer?.Invoke(phase, index)
            });
        _files.EnsureDirectoryStructure();
        Directory.CreateDirectory(Path.GetDirectoryName(_files.SessionGenerationPath)!);
        File.WriteAllBytes(_files.SessionGenerationPath,
            JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1, GenerationId = Guid.NewGuid().ToString("N") }));
        File.WriteAllBytes(_files.ResolvePath(ReplacePath), _replaceBefore);
        File.WriteAllBytes(_files.ResolvePath(DeletePath), _deleteBefore);
    }

    [Fact]
    public async Task ActualHelperPublishesCompleteMemberSetUnderOneDecision()
    {
        var intents = new List<string[]>();
        var commits = 0;
        _observer = (phase, _) =>
        {
            if (phase == TrustedLocalPublicationPhase.IntentPublished) intents.Add(ReadActiveMembers());
            if (phase == TrustedLocalPublicationPhase.Committed) commits++;
        };

        Assert.True(await CoordinatedStateWriteHelper.TryCommitAsync(_files, Writes()));

        Assert.Equal(ExpectedMembers(), Assert.Single(intents));
        Assert.Equal(1, commits);
        AssertAfterImages();
        AssertClean();
    }

    [Fact]
    public async Task ActualHelperReachedSecondMemberFailureRestoresWholeExactBeforeSet()
    {
        var cutHits = 0;
        _observer = (phase, index) =>
        {
            if (phase != TrustedLocalPublicationPhase.MemberPublished || index != 1) return;
            cutHits++;
            throw new InjectedFailure();
        };

        var committed = await CoordinatedStateWriteHelper.TryCommitAsync(_files, Writes());

        Assert.Equal(1, cutHits);
        Assert.False(committed);
        AssertImage(ReplacePath, _replaceBefore);
        AssertImage(CreatePath, null);
        AssertImage(DeletePath, _deleteBefore);
        AssertClean();
    }

    [Fact]
    public async Task ActualHelperUnknownLaterMemberPreservesWholeUnresolvedSet()
    {
        byte[] unknown = [0xFE, 0xFF, 0x41];
        var cutHits = 0;
        _observer = (phase, index) =>
        {
            if (phase != TrustedLocalPublicationPhase.MemberPublished ||
                ReadActiveMembers()[index] != _files.ResolvePath(CreatePath)) return;
            cutHits++;
            File.WriteAllBytes(_files.ResolvePath(DeletePath), unknown);
            throw new InjectedFailure();
        };

        var error = await Record.ExceptionAsync(
            () => CoordinatedStateWriteHelper.TryCommitAsync(_files, Writes()));

        Assert.Equal(1, cutHits);
        Assert.NotNull(error);
        AssertImage(ReplacePath, Utf8WithPreamble(NextJson));
        AssertImage(CreatePath, Utf8WithPreamble(NextJson));
        AssertImage(DeletePath, unknown);
        Assert.Equal(ExpectedMembers(), ReadActiveMembers());
        var evidence = File.ReadAllBytes(Active);
        _observer = null;
        var restarted = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await using var unexpected = await restarted.AcquireCanonicalWriteLeaseAsync();
        });
        Assert.Equal(evidence, File.ReadAllBytes(Active));
        AssertImage(ReplacePath, Utf8WithPreamble(NextJson));
        AssertImage(CreatePath, Utf8WithPreamble(NextJson));
        AssertImage(DeletePath, unknown);
    }

    [Fact]
    public async Task ActualHelperCommittedCleanupFailureKeepsCompleteCommittedResult()
    {
        var intents = 0;
        var commits = 0;
        var cleanupHits = 0;
        _observer = (phase, index) =>
        {
            if (phase == TrustedLocalPublicationPhase.IntentPublished) intents++;
            if (phase == TrustedLocalPublicationPhase.Committed) commits++;
            if (phase != TrustedLocalPublicationPhase.CleanupMember || index != 0) return;
            cleanupHits++;
            throw new InjectedFailure();
        };

        Assert.True(await CoordinatedStateWriteHelper.TryCommitAsync(_files, Writes()));

        Assert.Equal(1, intents);
        Assert.Equal(1, commits);
        Assert.Equal(1, cleanupHits);
        AssertAfterImages();
        Assert.Equal(ExpectedMembers(), ReadActiveMembers());
        using (var journal = JsonDocument.Parse(File.ReadAllBytes(Active)))
            Assert.True(journal.RootElement.GetProperty("Committed").GetBoolean());
        _observer = null;
        var restarted = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        await using (var lease = await restarted.AcquireCanonicalWriteLeaseAsync())
            AssertAfterImages();
        AssertClean();
    }

    private CoordinatedStateWriteHelper.PlannedWrite[] Writes() =>
    [
        new(ReplacePath, null, NextJson, true, ExactPrevious: new CanonicalBeforeImage(true, _replaceBefore)),
        new(CreatePath, null, NextJson, true, ExactPrevious: new CanonicalBeforeImage(false, null)),
        new(DeletePath, null, null, true, ExactPrevious: new CanonicalBeforeImage(true, _deleteBefore))
    ];

    private string[] ExpectedMembers() => [.. new[] { ReplacePath, CreatePath, DeletePath }.Select(_files.ResolvePath)];

    private string[] ReadActiveMembers()
    {
        using var journal = JsonDocument.Parse(File.ReadAllBytes(Active));
        return journal.RootElement.GetProperty("Members").EnumerateArray()
            .Select(member => member.GetProperty("Path").GetString()!).ToArray();
    }

    private void AssertAfterImages()
    {
        AssertImage(ReplacePath, Utf8WithPreamble(NextJson));
        AssertImage(CreatePath, Utf8WithPreamble(NextJson));
        AssertImage(DeletePath, null);
    }

    private void AssertImage(string relative, byte[]? expected)
    {
        var path = _files.ResolvePath(relative);
        Assert.Equal(expected != null, File.Exists(path));
        if (expected != null) Assert.Equal(expected, File.ReadAllBytes(path));
    }

    private void AssertClean()
    {
        Assert.False(File.Exists(Active));
        Assert.Empty(Directory.EnumerateFiles(_root, ".boe-local-*", SearchOption.AllDirectories));
        if (Directory.Exists(Journal)) Assert.Empty(Directory.EnumerateFileSystemEntries(Journal));
    }

    private static byte[] Utf8WithPreamble(string text) => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)];
    private sealed class InjectedFailure : Exception { }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
