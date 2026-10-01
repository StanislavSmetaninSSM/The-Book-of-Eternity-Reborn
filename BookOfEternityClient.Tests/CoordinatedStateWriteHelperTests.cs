using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class CoordinatedStateWriteHelperTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private Action<TrustedLocalPublicationPhase, int>? _observer;
    private Action? _onLockContention;

    public CoordinatedStateWriteHelperTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"boe-coordinated-write-{Guid.NewGuid():N}");
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) => _observer?.Invoke(phase, index),
                CanonicalWriteLockContendedAsync = () => { _onLockContention?.Invoke(); return Task.CompletedTask; }
            });
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task TryCommitAsync_ConcurrentChangeAfterFirstWrite_IsNotOverwrittenByRollback()
    {
        const string firstPath = "game_state/meta/coordinated_first.json";
        const string blockedPath = "game_state/meta/coordinated_blocked.json";
        const string previousJson = "{\"value\":\"before\"}";
        const string nextJson = "{\"value\":\"client-next\"}";
        const string concurrentJson = "{\"value\":\"gm-concurrent\"}";
        await _fs.WriteFileAtomicAsync(firstPath, previousJson);
        var concurrentWriteObserved = false;
        _observer = (phase, index) =>
        {
            if (phase != TrustedLocalPublicationPhase.MemberPublished || index != 0) return;
            concurrentWriteObserved = true;
            File.WriteAllText(_fs.ResolvePath(firstPath), concurrentJson,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Directory.CreateDirectory(_fs.ResolvePath(blockedPath));
        };

        var exception = await Record.ExceptionAsync(() => CoordinatedStateWriteHelper.TryCommitAsync(
            _fs,
            new CoordinatedStateWriteHelper.PlannedWrite(firstPath, previousJson, nextJson, true),
            new CoordinatedStateWriteHelper.PlannedWrite(blockedPath, null, "{}", true)));

        Assert.True(concurrentWriteObserved, "The first published member cut was not reached.");
        Assert.IsType<CoordinatedStatePublicationUncertainException>(exception);
        // Ordinary reads must refuse this unresolved journal; inspect the owned
        // fixture bytes directly rather than implicitly attempting recovery.
        Assert.Equal(concurrentJson, File.ReadAllText(_fs.ResolvePath(firstPath)));
    }

    [Fact]
    public async Task TryCommitAsync_HoldsCanonicalLeaseAcrossBaselineChecksAndEveryWrite()
    {
        const string firstPath = "game_state/meta/coordinated_linear_first.json";
        const string secondPath = "game_state/meta/coordinated_linear_second.json";
        const string previousJson = "{\"value\":\"before\"}";
        const string nextJson = "{\"value\":\"client-next\"}";
        const string concurrentJson = "{\"value\":\"concurrent-after\"}";
        await _fs.WriteFileAtomicAsync(firstPath, previousJson);
        await _fs.WriteFileAtomicAsync(secondPath, previousJson);

        Task? concurrentWrite = null;
        var contended = false;
        var memberObserved = false;
        _onLockContention = () => contended = true;
        _observer = (phase, index) =>
        {
            if (phase != TrustedLocalPublicationPhase.MemberPublished || index != 0 || concurrentWrite != null) return;
            memberObserved = true;
            concurrentWrite = _fs.WriteFileAtomicAsync(firstPath, concurrentJson);
            Assert.True(contended, "The competing writer did not reach the held canonical lock.");
            Assert.False(concurrentWrite.IsCompleted);
        };
        bool committed;
        try
        {
            committed = await CoordinatedStateWriteHelper.TryCommitAsync(_fs,
                new CoordinatedStateWriteHelper.PlannedWrite(firstPath, previousJson, nextJson, true),
                new CoordinatedStateWriteHelper.PlannedWrite(secondPath, previousJson, nextJson, true));
        }
        finally
        {
            _observer = null;
            if (concurrentWrite != null) await concurrentWrite.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.True(committed);
        Assert.True(memberObserved);
        Assert.True(contended);
        Assert.NotNull(concurrentWrite);
        Assert.Equal(concurrentJson, await _fs.ReadFileAsync(firstPath));
        Assert.Equal(nextJson, await _fs.ReadFileAsync(secondPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath)) Directory.Delete(_rootPath, recursive: true);
    }
}
