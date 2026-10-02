using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises actual save outcomes through session finalization without the full accepted-turn preparation cost.
/// </summary>
public sealed class PortableSaveBoundOutcomeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-bound-save-outcome-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Preserves the archive decision when a real bound operation encounters conflicting publication evidence while closing.
    /// </summary>
    /// <param name="committed">
    /// Corrupts the new archive after durable commit when true, or before the commit decision otherwise.
    /// </param>
    /// <returns>
    /// A task completing after the same typed save failure, exact created destination and retained evidence are verified.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealBoundSaveRetainsArchiveDecisionWhenClosingRecoveryConflicts(bool committed)
    {
        var reached = 0;
        string? destination = null;
        byte[]? retainedJournal = null;
        FileSystemManager? files = null;
        files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) =>
                {
                    if (committed ? phase != TrustedLocalPublicationPhase.Committed :
                        phase != TrustedLocalPublicationPhase.MemberPublished || index != 0) return;
                    destination = Assert.Single(Directory.GetFiles(files!.ResolvePath("saves/manual_saves"), "*.zip")
                        .Where(path => Path.GetFileName(path) != "untouched.zip"));
                    reached++;
                    retainedJournal = File.ReadAllBytes(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1/active.json"));
                    File.WriteAllBytes(destination, [83, 0, 255]);
                    throw new InvalidDataException("synthetic bound save publication conflict");
                }
            });
        var state = PortableSaveFixture.Seed(files);
        File.WriteAllBytes(files.ResolvePath("saves/manual_saves/untouched.zip"), [9, 0, 255]);
        var service = new SaveLoadService(files, state, NullLogger<SaveLoadService>.Instance);
        using var generation = JsonDocument.Parse(File.ReadAllBytes(files.SessionGenerationPath));
        var generationId = generation.RootElement.GetProperty("generationId").GetString()!;
        Exception? primary = null;

        var error = await Record.ExceptionAsync(() => SessionOperationContext.RunBoundAsync(files, generationId, async () =>
        {
            try { await service.SaveGameAsync("bound-save", "actual bound decision"); }
            catch (Exception failure) when (failure is CoordinatedStatePublicationUncertainException or CommittedSaveContinuationException)
            {
                primary = failure;
                throw;
            }
        }).WaitAsync(TimeSpan.FromSeconds(30)));

        Assert.True(reached == 1, $"Expected one actual save cut, reached {reached}. Primary: {primary}; outer: {error}");
        var expected = committed ? typeof(CommittedSaveContinuationException) : typeof(CoordinatedStatePublicationUncertainException);
        Assert.True(primary?.GetType() == expected, $"The save must establish {expected}. Primary: {primary}; outer: {error}");
        Assert.True(error?.GetType() == expected, $"Closing must preserve {expected}. Primary: {primary}; outer: {error}");
        Assert.Same(primary, error);
        Assert.IsType<InvalidDataException>(error!.Data["SessionFinalizationFailure"]);
        if (committed)
        {
            var decision = Assert.IsType<CommittedSaveContinuationException>(error).Result;
            Assert.True(decision.Committed);
            Assert.True(decision.ContinuationBlocked);
            Assert.Equal(destination, files.ResolvePath(decision.DestinationRelativePath!));
        }
        Assert.Equal(new byte[] { 83, 0, 255 }, File.ReadAllBytes(destination!));
        Assert.Equal(new byte[] { 9, 0, 255 }, File.ReadAllBytes(files.ResolvePath("saves/manual_saves/untouched.zip")));
        Assert.NotNull(retainedJournal);
        Assert.Equal(retainedJournal, File.ReadAllBytes(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1/active.json")));
    }

    /// <summary>
    /// Gives a generation replacement discovered during real closing precedence over either typed storage failure.
    /// </summary>
    /// <param name="committed">
    /// Uses a known committed save decision when true, or an uncertain publication decision otherwise.
    /// </param>
    /// <returns>
    /// A task completing after replacement fencing retains the original typed storage decision as diagnostic evidence.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosingGenerationReplacementWinsAndRetainsStorageDecision(bool committed)
    {
        FileSystemManager? files = null;
        var closing = 0;
        var fixture = CreateBoundFixture(new FileSystemManagerHooks
        {
            SessionOperationClosingAsync = () =>
            {
                closing++;
                File.WriteAllBytes(files!.SessionGenerationPath,
                    JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = "closing-replacement" }));
                return Task.CompletedTask;
            }
        });
        files = fixture.Files;
        var primary = CreateStorageFailure(committed);

        var error = await Record.ExceptionAsync(() => SessionOperationContext.RunBoundAsync(files, fixture.Generation,
            () => Task.FromException(primary)));

        Assert.Equal(1, closing);
        var replacement = Assert.IsType<SessionReplacedException>(error);
        Assert.Equal(fixture.Generation, replacement.ExpectedGeneration);
        Assert.Same(primary, replacement.Data["SessionOperationFailure"]);
        Assert.False(SessionOperationContext.TryGetExpectedGeneration(files.BasePath, out _));
        Assert.Equal("closing-replacement", ReadGeneration(files));
    }

    /// <summary>
    /// Preserves an already established replacement when an ordinary closing error would otherwise mask it.
    /// </summary>
    /// <returns>
    /// A task completing after the replacement retains its save cause and separate closing diagnostic.
    /// </returns>
    [Fact]
    public async Task AlreadyReplacedBindingSurvivesOrdinaryClosingFailure()
    {
        var closingFailure = new IOException("synthetic closing hook failure");
        var fixture = CreateBoundFixture(new FileSystemManagerHooks
        {
            SessionOperationClosingAsync = () => Task.FromException(closingFailure)
        });
        var primary = CreateStorageFailure(committed: true);

        var error = await Record.ExceptionAsync(() => SessionOperationContext.RunBoundAsync(fixture.Files, fixture.Generation, () =>
        {
            SessionOperationContext.MarkReplaced(fixture.Files.BasePath, "already-replaced", "The synthetic session was replaced.");
            return Task.FromException(primary);
        }));

        var replacement = Assert.IsType<SessionReplacedException>(error);
        Assert.Equal("already-replaced", replacement.ActualGeneration);
        Assert.Same(primary, replacement.InnerException);
        Assert.Same(closingFailure, replacement.Data["SessionFinalizationFailure"]);
        Assert.False(SessionOperationContext.TryGetExpectedGeneration(fixture.Files.BasePath, out _));
    }

    /// <summary>
    /// Leaves the existing closing-error precedence unchanged when the operation has no qualifying storage outcome.
    /// </summary>
    /// <returns>
    /// A task completing after the ordinary closing error remains the reported failure and the context is restored.
    /// </returns>
    [Fact]
    public async Task OrdinaryOperationFailureKeepsExistingClosingErrorPrecedence()
    {
        var closingFailure = new IOException("synthetic ordinary closing failure");
        var fixture = CreateBoundFixture(new FileSystemManagerHooks
        {
            SessionOperationClosingAsync = () => Task.FromException(closingFailure)
        });

        var error = await Record.ExceptionAsync(() => SessionOperationContext.RunBoundAsync(fixture.Files, fixture.Generation,
            () => Task.FromException(new InvalidOperationException("ordinary operation failure"))));

        Assert.Same(closingFailure, error);
        Assert.False(SessionOperationContext.TryGetExpectedGeneration(fixture.Files.BasePath, out _));
    }

    /// <summary>
    /// Restores the enclosing session binding after an inner storage outcome and its closing hook both fail.
    /// </summary>
    /// <returns>
    /// A task completing after the outer generation remains writable and no inner binding escapes its scope.
    /// </returns>
    [Fact]
    public async Task StorageClosingFailureRestoresEnclosingAmbientBinding()
    {
        var outer = CreateBoundFixture();
        var closingFailure = new IOException("synthetic inner closing failure");
        var inner = CreateBoundFixture(new FileSystemManagerHooks
        {
            SessionOperationClosingAsync = () => Task.FromException(closingFailure)
        }, Path.Combine(_root, "inner-context"));
        var primary = CreateStorageFailure(committed: false);

        await SessionOperationContext.RunBoundAsync(outer.Files, outer.Generation, async () =>
        {
            var error = await Record.ExceptionAsync(() => SessionOperationContext.RunBoundAsync(inner.Files, inner.Generation,
                () => Task.FromException(primary)));
            Assert.Same(primary, error);
            Assert.Same(closingFailure, error!.Data["SessionFinalizationFailure"]);
            Assert.True(SessionOperationContext.TryGetExpectedGeneration(outer.Files.BasePath, out var generation));
            Assert.Equal(outer.Generation, generation);
            Assert.False(SessionOperationContext.TryGetExpectedGeneration(inner.Files.BasePath, out _));
            await outer.Files.WriteFileAtomicAsync("game_state/world/outer-continuation.json", "{\"owner\":\"outer\"}");
        });

        Assert.Equal("{\"owner\":\"outer\"}", File.ReadAllText(outer.Files.ResolvePath("game_state/world/outer-continuation.json")));
        Assert.False(SessionOperationContext.TryGetExpectedGeneration(outer.Files.BasePath, out _));
    }

    /// <summary>
    /// Seeds an independently owned minimal canonical root for session-bound outcome controls.
    /// </summary>
    /// <param name="hooks">
    /// Optional closing hooks; null leaves the manager's ordinary closing behavior intact.
    /// </param>
    /// <param name="root">
    /// An optional child root used for nested-context coverage; null uses this test's root.
    /// </param>
    /// <returns>
    /// The manager and its exact seeded generation.
    /// </returns>
    private (FileSystemManager Files, string Generation) CreateBoundFixture(FileSystemManagerHooks? hooks = null, string? root = null)
    {
        var files = new FileSystemManager(root ?? _root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, hooks);
        PortableSaveFixture.Seed(files);
        return (files, ReadGeneration(files));
    }

    /// <summary>
    /// Reads the exact generation already established by the synthetic fixture.
    /// </summary>
    /// <param name="files">
    /// The independently owned manager whose generation is inspected.
    /// </param>
    /// <returns>
    /// The persisted generation identifier.
    /// </returns>
    private static string ReadGeneration(FileSystemManager files)
    {
        using var generation = JsonDocument.Parse(File.ReadAllBytes(files.SessionGenerationPath));
        return generation.RootElement.GetProperty("generationId").GetString()!;
    }

    /// <summary>
    /// Creates a typed storage failure for narrow closing-precedence controls.
    /// </summary>
    /// <param name="committed">
    /// Selects a confirmed committed decision when true, or an unresolved publication otherwise.
    /// </param>
    /// <returns>
    /// The storage failure whose identity and decision must survive ordinary closing errors.
    /// </returns>
    private static Exception CreateStorageFailure(bool committed) => committed
        ? new CommittedSaveContinuationException(new SaveCreationResult(SaveCreationDisposition.Committed,
            "saves/manual_saves/committed-control.zip", true, new IOException("synthetic committed follow-up"), true))
        : new CoordinatedStatePublicationUncertainException(new IOException("synthetic uncertain publication"));

    /// <summary>
    /// Removes only the independently owned synthetic root after preserving the assertion evidence.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
