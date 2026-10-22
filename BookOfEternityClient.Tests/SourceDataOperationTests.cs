using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises whole-operation source reuse and effect fences on independent ordinary files.
/// </summary>
/// <param name="output">
/// Receives every actual source/timing observation and nonce-owned cleanup result.
/// </param>
public sealed class SourceDataOperationTests(ITestOutputHelper output)
{
    /// <summary>
    /// Reuses detached bytes throughout one owner and performs only one fresh final source read.
    /// </summary>
    /// <param name="synchronous">
    /// Whether the original synchronous rather than asynchronous reader supplies the source.
    /// </param>
    /// <returns>
    /// Completion after real physical read reduction and unchanged source bytes.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EightRequestsUseOneInitialAndOneFinalSource(bool synchronous)
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        fixture.Recording = true;
        for (var i = 0; i < 8; i++)
        {
            var bytes = synchronous ? files.ReadFileBytesSync(lease, "present.txt")
                : await files.ReadFileBytesAsync(lease, "present.txt");
            Assert.Equal("original", Encoding.UTF8.GetString(bytes!));
            bytes![0] = 0;
        }
        await operation.CompleteAsync();
        output.WriteLine(JsonSerializer.Serialize(new { synchronous, fixture.PresentSources }));
        Assert.Equal(2, fixture.PresentSources);
        Assert.Equal("original", File.ReadAllText(fixture.PresentPath));
    }

    /// <summary>
    /// Shares sources between independent nested read plans instead of closing one read set per plan.
    /// </summary>
    /// <param name="bound">
    /// Whether the plans run through the actual participating session context.
    /// </param>
    /// <returns>
    /// Completion after two full nested plans share one first and one final source.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TwoNestedPlansShareTheOwningSourceLifetime(bool bound)
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var generation = files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        fixture.Recording = true;
        async Task<bool> Plans()
        {
            for (var plan = 0; plan < 2; plan++)
                Assert.True(await files.ReadStableRecoveryDataAsync(lease, async () =>
                {
                    for (var read = 0; read < 8; read++) Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
                    return true;
                }));
            return true;
        }
        if (bound) Assert.True(await SessionOperationContext.RunBoundAsync(files, generation, lease, Plans));
        else Assert.True(await Plans());
        await operation.CompleteAsync();
        output.WriteLine(JsonSerializer.Serialize(new { bound, fixture.PresentSources }));
        Assert.Equal(2, fixture.PresentSources);
    }

    /// <summary>
    /// Refuses raw bytes, absence, namespace and generation changes before successful owner completion.
    /// </summary>
    /// <param name="change">
    /// The external untracked change introduced after actual source observations.
    /// </param>
    /// <returns>
    /// Completion after no owner success escapes the fresh source fence.
    /// </returns>
    [Theory]
    [InlineData("same-time-bytes")]
    [InlineData("appearance")]
    [InlineData("namespace")]
    [InlineData("generation")]
    public async Task RawDriftCannotEscapeSuccessfulCompletion(string change)
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        var timestamp = File.GetLastWriteTimeUtc(fixture.PresentPath);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        Assert.Null(await files.ReadFileAsync(lease, "missing.txt"));
        switch (change)
        {
            case "same-time-bytes": File.WriteAllText(fixture.PresentPath, "modified"); File.SetLastWriteTimeUtc(fixture.PresentPath, timestamp); break;
            case "appearance": File.WriteAllText(Path.Combine(fixture.Root, "game_session", "missing.txt"), "new"); break;
            case "namespace": File.Delete(fixture.PresentPath); Directory.CreateDirectory(fixture.PresentPath); break;
            case "generation": File.AppendAllText(files.SessionGenerationPath, "\n"); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        await Assert.ThrowsAsync<InvalidDataException>(operation.CompleteAsync);
        Assert.False(File.Exists(Path.Combine(fixture.Root, "game_session", "effect.txt")));
    }

    /// <summary>
    /// Blocks a real writer before effects when its cached sources have not passed a fresh fence.
    /// </summary>
    /// <returns>
    /// Completion after the actual public writer refuses and creates no target file.
    /// </returns>
    [Fact]
    public async Task ActualWriterRefusesUnverifiedSourceData()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => files.WriteFileAtomicAsync(lease, "effect.txt", "forbidden"));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "game_session", "effect.txt")));
        Assert.Equal("original", File.ReadAllText(fixture.PresentPath));
    }

    /// <summary>
    /// Allows an explicitly verified real writer and observes its new bytes in the next DATA segment.
    /// </summary>
    /// <returns>
    /// Completion after old bytes cannot be returned after the supported mutation.
    /// </returns>
    [Fact]
    public async Task VerifiedRealWriterRetiresEarlierData()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        using (operation)
        {
            Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
            await operation.VerifyAsync(lease);
            await files.WriteFileAtomicAsync(lease, "present.txt", "changed");
            Assert.Equal("changed", await files.ReadFileAsync(lease, "present.txt"));
            await operation.CompleteAsync();
        }
        Assert.Equal(0, operation.CaptureState().Entries);
        Assert.True(operation.CaptureState().Closed);
    }

    /// <summary>
    /// Refuses a captured child after owner disposal even while the child's original physical lease is still live.
    /// </summary>
    /// <returns>
    /// Completion after child settlement and closed empty general storage.
    /// </returns>
    [Fact]
    public async Task CapturedChildCannotUseClosedOperation()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var child = Task.Run(async () => { await release.Task; return await files.ReadFileAsync(lease, "present.txt"); });
        operation.Dispose();
        release.TrySetResult();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => { await child; });
        Assert.Equal(0, operation.CaptureState().Entries);
        Assert.True(operation.CaptureState().Closed);
    }

    /// <summary>
    /// Keeps source reads fresh while an unrelated live sibling borrows the original access.
    /// </summary>
    /// <returns>
    /// Completion after eight sibling-conflicted physical reads and later first/final reuse.
    /// </returns>
    [Fact]
    public async Task ExistingSiblingKeepsSourcesFreshUntilItCloses()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sibling = Task.Run(async () =>
        {
            await using var frame = files.BeginParticipatingMainAdmission();
            try { await frame.AcquireAsync(); ready.TrySetResult(); await release.Task; }
            catch (Exception error) { ready.TrySetException(error); throw; }
        });
        fixture.Recording = true;
        try
        {
            await ready.Task;
            for (var i = 0; i < 8; i++) Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
            Assert.Equal(8, fixture.PresentSources);
            Assert.Equal(0, operation.CaptureState().Entries);
        }
        finally { release.TrySetResult(); await sibling; }
        for (var i = 0; i < 8; i++) Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        await operation.CompleteAsync();
        Assert.Equal(10, fixture.PresentSources);
    }

    /// <summary>
    /// Prevents a newly admitted descendant from consuming another entry's verified receipt.
    /// </summary>
    /// <returns>
    /// Completion after an actual writer refuses before creating its target.
    /// </returns>
    [Fact]
    public async Task AnotherEntryCannotConsumeVerifiedReceipt()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        await operation.VerifyAsync(lease);
        await using var entry = files.BeginParticipatingMainAdmission();
        await entry.AcquireAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => files.WriteFileAtomicAsync(lease, "effect.txt", "forbidden"));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "game_session", "effect.txt")));
    }

    /// <summary>
    /// Keeps successful physical no-evidence recovery from retiring unchanged file DATA.
    /// </summary>
    /// <returns>
    /// Completion after one original and one final physical source with no mutation transitions.
    /// </returns>
    [Fact]
    public async Task NoEvidenceRecoveryReusesAlreadyReadData()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        var before = files.CanonicalRootAuthorityIdentity.ValidationEpoch.CaptureActivity();
        fixture.Recording = true;
        for (var i = 0; i < 8; i++)
        {
            files.ResolveBackupPublicationRecovery(lease);
            Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        }
        await operation.CompleteAsync();
        Assert.Equal(2, fixture.PresentSources);
        Assert.Equal(before, files.CanonicalRootAuthorityIdentity.ValidationEpoch.CaptureActivity());
    }

    /// <summary>
    /// Runs actual committed-journal cleanup under independent fresh recovery authority.
    /// </summary>
    /// <returns>
    /// Completion after real cleanup retires cached DATA and removes its original journal.
    /// </returns>
    [Fact]
    public async Task ActualJournalCleanupRetiresDataAndSettlesIndependently()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        fixture.OnPublication = (phase, _) =>
        {
            if (phase == TrustedLocalPublicationPhase.Committed) throw new IOException("retain actual committed cleanup debt");
        };
        File.WriteAllText(files.ResolvePath("debt.json"), "{}");
        Assert.True(await CoordinatedStateWriteHelper.TryCommitAsync(files, lease,
            new CoordinatedStateWriteHelper.PlannedWrite("debt.json", "{}", "{\"n\":1}")));
        fixture.OnPublication = null;
        var journal = Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
        Assert.True(File.Exists(journal));
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        fixture.Recording = true;
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        var before = files.CanonicalRootAuthorityIdentity.ValidationEpoch.CaptureActivity();
        files.ResolveBackupPublicationRecovery(lease);
        Assert.False(File.Exists(journal));
        Assert.True(files.CanonicalRootAuthorityIdentity.ValidationEpoch.CaptureActivity().Begins > before.Begins);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        await operation.CompleteAsync();
        Assert.Equal(3, fixture.PresentSources);
    }

    /// <summary>
    /// Releases the physical lock even when a raw edit makes lease-close verification refuse.
    /// </summary>
    /// <returns>
    /// Completion after the failed close releases its owned resources and another manager acquires the lock.
    /// </returns>
    [Fact]
    public async Task FailedCloseFenceReleasesOriginalPhysicalResources()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        File.WriteAllText(fixture.PresentPath, "modified");
        await Assert.ThrowsAsync<InvalidDataException>(async () => await lease.DisposeAsync());
        Assert.False(lease.IsActive);
        var other = new FileSystemManager(fixture.Root, NullLogger<FileSystemManager>.Instance);
        await using var later = await other.AcquireCanonicalWriteLeaseAsync();
        Assert.True(later.IsActive);
    }

    /// <summary>
    /// Invalidates a one-use effect receipt when another source observation occurs.
    /// </summary>
    /// <returns>
    /// Completion after the real writer refuses until the new observation receives its own fence.
    /// </returns>
    [Fact]
    public async Task SourceReadAfterVerificationRequiresAnotherFence()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        await operation.VerifyAsync(lease);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => files.WriteFileAtomicAsync(lease, "effect.txt", "forbidden"));
        await operation.VerifyAsync(lease);
        await files.WriteFileAtomicAsync(lease, "present.txt", "changed");
        Assert.Equal("changed", await files.ReadFileAsync(lease, "present.txt"));
        await operation.CompleteAsync();
    }

    /// <summary>
    /// Retires DATA throughout an independently authorized callback and its awaited descendants.
    /// </summary>
    /// <returns>
    /// Completion after fresh callback observations and a newly established post-callback source.
    /// </returns>
    [Fact]
    public async Task FreshCallbackCannotReviveEarlierData()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        fixture.Recording = true;
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        using (files.ForceFreshRecoveryValidation())
        {
            File.WriteAllText(fixture.PresentPath, "callback");
            Assert.Equal("callback", await files.ReadFileAsync(lease, "present.txt"));
            await Task.Yield();
            Assert.Equal("callback", await files.ReadFileAsync(lease, "present.txt"));
        }
        Assert.Equal("callback", await files.ReadFileAsync(lease, "present.txt"));
        Assert.Equal("callback", await files.ReadFileAsync(lease, "present.txt"));
        await operation.CompleteAsync();
        Assert.Equal(5, fixture.PresentSources);
    }

    /// <summary>
    /// Retires source DATA even when the original write has equal bytes or fails before publication.
    /// </summary>
    /// <param name="invalidPath">
    /// Whether the attempted writer fails on its original invalid path rather than publishing equal bytes.
    /// </param>
    /// <returns>
    /// Completion after a later segment reads the physical source once and freshly verifies it once.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameBytesAndFailedWriterRetireData(bool invalidPath)
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        await operation.VerifyAsync(lease);
        if (invalidPath)
            Assert.NotNull(await Record.ExceptionAsync(() => files.WriteFileAtomicAsync(lease, "../outside.txt", "forbidden")));
        else await files.WriteFileAtomicAsync(lease, "present.txt", "original");
        fixture.Recording = true;
        for (var i = 0; i < 8; i++) Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        await operation.CompleteAsync();
        Assert.Equal(2, fixture.PresentSources);
    }

    /// <summary>
    /// Rechecks the exact verified root version atomically when a mutation token is registered.
    /// </summary>
    [Fact]
    public void RootEpochRejectsAnInterveningMutationAtRegistration()
    {
        var epoch = new CanonicalValidationEpoch();
        Assert.True(epoch.TryCapture(out var verified));
        var intervening = new object();
        epoch.Begin(intervening);
        epoch.End(intervening);
        Assert.Throws<InvalidOperationException>(() => epoch.Begin(new object(), verified));
        Assert.Equal((1L, 1L, 0), epoch.CaptureActivity());
    }

    /// <summary>
    /// Admits a replacement physical lease freshly and never revives a prior lease's source receipt.
    /// </summary>
    /// <returns>
    /// Completion after observing changed physical bytes and requiring a new pre-write fence.
    /// </returns>
    [Fact]
    public async Task ReplacementLeaseRequiresNewDataAndVerification()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        await using (var first = await files.AcquireCanonicalWriteLeaseAsync())
        {
            files.GetOrCreateSessionGeneration(first);
            Assert.Equal("original", await files.ReadFileAsync(first, "present.txt"));
            await operation.VerifyAsync(first);
        }
        File.WriteAllText(fixture.PresentPath, "modified");
        await using var second = await files.AcquireCanonicalWriteLeaseAsync();
        Assert.Equal("modified", await files.ReadFileAsync(second, "present.txt"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => files.WriteFileAtomicAsync(second, "effect.txt", "forbidden"));
        await operation.VerifyAsync(second);
        await files.WriteFileAtomicAsync(second, "present.txt", "final");
        Assert.Equal("final", await files.ReadFileAsync(second, "present.txt"));
        await operation.CompleteAsync();
    }

    /// <summary>
    /// Invalidates an adapter's receipt without depending on Windows acquisition recovery invalidation.
    /// </summary>
    /// <returns>
    /// Completion after a retired and readmitted adapter cannot authorize a writer from its old receipt.
    /// </returns>
    [Fact]
    public async Task RetiredAdapterCannotLendItsReceiptToReadmission()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        await operation.VerifyAsync(lease);
        var epoch = files.CanonicalRootAuthorityIdentity.ValidationEpoch.CaptureActivity();
        // Exercise the real adapter-retirement transition while retaining the real physical lock.
        // This is a component boundary test; the separate replacement-lease case acquires both real leases.
        await operation.BeforeLeaseCloseAsync(lease);
        var stamp = File.GetLastWriteTimeUtc(fixture.PresentPath);
        File.WriteAllText(fixture.PresentPath, "modified");
        File.SetLastWriteTimeUtc(fixture.PresentPath, stamp);
        operation.Observe(lease);
        await Assert.ThrowsAsync<InvalidOperationException>(() => files.WriteFileAtomicAsync(lease, "effect.txt", "forbidden"));
        Assert.Equal(epoch, files.CanonicalRootAuthorityIdentity.ValidationEpoch.CaptureActivity());
        Assert.False(File.Exists(files.ResolvePath("effect.txt")));
    }

    /// <summary>
    /// Lets independent fresh recovery bypass a failed DATA owner without reviving it afterward.
    /// </summary>
    /// <returns>
    /// Completion after original helper, nested owner, fence and writer routes run freshly during recovery only.
    /// </returns>
    [Fact]
    public async Task FailedOwnerDoesNotBlockIndependentlyFreshRecovery()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        File.WriteAllText(fixture.PresentPath, "modified");
        await Assert.ThrowsAsync<InvalidDataException>(operation.CompleteAsync);
        using (files.ForceFreshRecoveryValidation())
        {
            Assert.Null(files.BeginSourceDataOperationIfNeeded());
            Assert.Equal("modified", await files.ReadStableRecoveryDataAsync(lease,
                () => files.ReadFileAsync(lease, "present.txt")));
            await files.VerifySourceDataAsync(lease);
            await operation.VerifyAsync(lease);
            await files.WriteFileAtomicAsync(lease, "present.txt", "recovered");
            Assert.Equal("recovered", await files.ReadFileAsync(lease, "present.txt"));
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => files.ReadFileAsync(lease, "present.txt"));
        Assert.Equal(0, operation.CaptureState().Entries);
        Assert.Equal("recovered", File.ReadAllText(fixture.PresentPath));
    }

    /// <summary>
    /// Excludes passive diagnostic descendants from the functional owner and its disposal lifetime.
    /// </summary>
    /// <returns>
    /// Completion after the functional cache closes empty and a captured passive read remains an original fresh read.
    /// </returns>
    [Fact]
    public async Task PassiveDescendantNeverJoinsFunctionalData()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        File.WriteAllText(files.ResolvePath("passive.txt"), "passive");
        var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<string?>? passive = null;
        fixture.OnRead = path =>
        {
            if (path == "present.txt") passive ??= Task.Run(async () =>
            { await release.Task; return await files.ReadFileAsync(lease, "passive.txt"); });
        };
        try
        {
            for (var i = 0; i < 8; i++) Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
            Assert.Equal(1, operation.CaptureState().Entries);
            await operation.CompleteAsync();
        }
        finally { operation.Dispose(); release.TrySetResult(); }
        Assert.NotNull(passive);
        Assert.Equal("passive", await passive);
        Assert.Equal(new ValidationOperationState(0, 0, true), operation.CaptureState());
    }

    /// <summary>
    /// Refuses cached effect approval once its actual participating entry begins closing.
    /// </summary>
    /// <returns>
    /// Completion after both source success and actual write are refused without creating a target.
    /// </returns>
    [Fact]
    public async Task ClosingEntryCannotApproveCachedEffects()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        await using var entry = files.BeginParticipatingMainAdmission();
        await entry.AcquireAsync();
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        entry.BeginClosing();
        Assert.NotNull(await Record.ExceptionAsync(() => operation.VerifyAsync(lease)));
        Assert.NotNull(await Record.ExceptionAsync(() => files.WriteFileAtomicAsync(lease, "effect.txt", "forbidden")));
        Assert.False(File.Exists(files.ResolvePath("effect.txt")));
    }

    /// <summary>
    /// Refuses overlapping DATA consumers and settles both tasks before releasing their physical lease.
    /// </summary>
    /// <returns>
    /// Completion after the actual source coordinator rejects overlap and retains no reusable result.
    /// </returns>
    [Fact]
    public async Task OverlappingSourcesCannotReturnMixedData()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        operation.SourceReadBarrierForTests = release.Task;
        var first = files.ReadFileAsync(lease, "present.txt");
        try
        {
            Assert.False(first.IsCompleted);
            Assert.True(operation.CaptureState().Dependencies > 0);
            await Assert.ThrowsAsync<InvalidOperationException>(() => files.ReadFileAsync(lease, "present.txt"));
        }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await first);
        Assert.Equal(0, operation.CaptureState().Entries);
    }

    /// <summary>
    /// Closes the general cache while an actual asynchronous source is pending and rejects its late result.
    /// </summary>
    /// <returns>
    /// Completion after the captured source settles and the disposed service remains empty.
    /// </returns>
    [Fact]
    public async Task DisposalDuringSourceRejectsLateResult()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        operation.SourceReadBarrierForTests = release.Task;
        var first = files.ReadFileAsync(lease, "present.txt");
        try { Assert.False(first.IsCompleted); operation.Dispose(); }
        finally { operation.Dispose(); release.TrySetResult(); }
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await first);
        Assert.Equal(new ValidationOperationState(0, 0, true), operation.CaptureState());
    }

    /// <summary>
    /// Uses the actual runtime advancement seam while avoiding expensive game-engine construction.
    /// </summary>
    /// <param name="drift">
    /// Whether untracked bytes change after the source was cached and before advancement.
    /// </param>
    /// <returns>
    /// Completion after exactly one valid advance or refusal with the original turn intact.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualRuntimeAdvanceRequiresFreshData(bool drift)
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        var loop = new GameLoop();
        loop.SetSession("fixture", 42);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        if (drift) File.WriteAllText(fixture.PresentPath, "modified");
        if (drift)
            await Assert.ThrowsAsync<InvalidDataException>(() => GameEngine.AdvanceVerifiedMortalJointRuntimeAsync(files, lease, loop, 43));
        else
        {
            await GameEngine.AdvanceVerifiedMortalJointRuntimeAsync(files, lease, loop, 43);
            await operation.CompleteAsync();
        }
        Assert.Equal(drift ? 42 : 43, loop.TurnNumber);
    }

    /// <summary>
    /// Owns tiny real files, actual source counters and complete passive timing output.
    /// </summary>
    private sealed class SourceOperationFixture : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly ConcurrentQueue<(string Name, string Boundary, double Milliseconds)> _rows = new();
        private int _presentSources;

        /// <summary>
        /// Creates one nonce-qualified directory with an ordinary present file and a passive receiver.
        /// </summary>
        /// <param name="output">
        /// Receiver for every timing and final exact cleanup observation.
        /// </param>
        internal SourceOperationFixture(ITestOutputHelper output)
        {
            _output = output;
            Root = Path.Combine(Path.GetTempPath(), "boe-source-operation-" + _nonce);
            Directory.CreateDirectory(Path.Combine(Root, "game_session"));
            File.WriteAllText(Path.Combine(Root, ".fixture-owner"), _nonce);
            File.WriteAllText(PresentPath, "original");
            Files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    CanonicalReadActivityObserver = path =>
                    {
                        if (Recording && path == "present.txt") Interlocked.Increment(ref _presentSources);
                        OnRead?.Invoke(path);
                    },
                    OperationTimingObserver = (name, boundary, elapsed) => _rows.Enqueue((name, boundary, elapsed.TotalMilliseconds)),
                    LocalPublicationObserver = (phase, ordinal) => OnPublication?.Invoke(phase, ordinal)
                });
        }

        /// <summary>
        /// Gets the exclusively owned physical root.
        /// </summary>
        internal string Root { get; }

        /// <summary>
        /// Gets the actual ordinary source file name.
        /// </summary>
        internal string PresentPath => Path.Combine(Root, "game_session", "present.txt");

        /// <summary>
        /// Gets the production manager issuing every real lease and source read.
        /// </summary>
        internal FileSystemManager Files { get; }

        /// <summary>
        /// Gets or sets the actual publication hook used to retain known committed cleanup debt.
        /// </summary>
        internal Action<TrustedLocalPublicationPhase, int>? OnPublication { get; set; }

        /// <summary>
        /// Gets or sets the passive physical-read receiver used by diagnostic descendant controls.
        /// </summary>
        internal Action<string>? OnRead { get; set; }

        /// <summary>
        /// Gets or sets whether actual opened present-file sources contribute to the counter.
        /// </summary>
        internal bool Recording { get; set; }

        /// <summary>
        /// Gets the number of actual present-file read activity events while recording.
        /// </summary>
        internal int PresentSources => Volatile.Read(ref _presentSources);

        /// <summary>
        /// Deletes only the verified owned root and emits all observations without a record quota.
        /// </summary>
        public void Dispose()
        {
            try
            {
                Assert.Equal("boe-source-operation-" + _nonce, Path.GetFileName(Root));
                Assert.Equal(_nonce, File.ReadAllText(Path.Combine(Root, ".fixture-owner")));
                new TrustedLocalFileScope([Path.GetTempPath()]).DeleteOwnedTree(Root);
                Assert.False(Directory.Exists(Root));
            }
            finally
            {
                foreach (var row in _rows) _output.WriteLine(JsonSerializer.Serialize(new { row.Name, row.Boundary, row.Milliseconds }));
                _output.WriteLine(JsonSerializer.Serialize(new { Root, Removed = !Directory.Exists(Root) }));
            }
        }
    }
}
