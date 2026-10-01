using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableCoordinatedPublicationTests
{
    [Fact]
    public async Task DuplicateGuardsAfterARealWriteStillBindTheInitialExactImage()
    {
        const string beforeJson = "{ \"value\" : 1 }";
        var before = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(beforeJson)).ToArray();
        File.WriteAllBytes(_files.ResolvePath(ReplacePath), before);
        var intents = new List<string[]>();
        _observer = (phase, _) => { if (phase == TrustedLocalPublicationPhase.IntentPublished) intents.Add(ReadActiveMembers()); };

        Assert.True(await CoordinatedStateWriteHelper.TryCommitAsync(_files,
            CoordinatedStateWriteHelper.CreateGuardWrite(ReplacePath, "{\"value\":1}"),
            new CoordinatedStateWriteHelper.PlannedWrite(ReplacePath, beforeJson, NextJson, RequireCurrentBaseline: true),
            CoordinatedStateWriteHelper.CreateExactGuardWrite(ReplacePath, new(true, before))));

        Assert.Equal(new[] { _files.ResolvePath(ReplacePath) }, Assert.Single(intents));
        AssertImage(ReplacePath, Utf8WithPreamble(NextJson));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictingDuplicateGuardIsNotDiscarded(bool first)
    {
        var accepted = CoordinatedStateWriteHelper.CreateExactGuardWrite(ReplacePath, new(true, _replaceBefore));
        var rejected = CoordinatedStateWriteHelper.CreateExactGuardWrite(ReplacePath, new(true, [44]));
        var intents = 0;
        _observer = (phase, _) => { if (phase == TrustedLocalPublicationPhase.IntentPublished) intents++; };

        Assert.False(await CoordinatedStateWriteHelper.TryCommitAsync(_files,
            first ? rejected : accepted, Writes()[0], first ? accepted : rejected));

        Assert.Equal(0, intents);
        AssertImage(ReplacePath, _replaceBefore);
        AssertClean();
    }

    [Theory]
    [InlineData("replace")]
    [InlineData("delete")]
    [InlineData("recreate")]
    public async Task DuplicateRealDestinationsUseInitialBeforeAndLastImageOrder(string final)
    {
        string[]? members = null;
        byte[]? original = null;
        _observer = (phase, _) =>
        {
            if (phase != TrustedLocalPublicationPhase.IntentPublished) return;
            members = ReadActiveMembers();
            using var journal = JsonDocument.Parse(File.ReadAllBytes(Active));
            original = journal.RootElement.GetProperty("Members")[1].GetProperty("Before")
                .GetProperty("Bytes").GetBytesFromBase64();
        };
        var next = final == "delete" ? null : NextJson;

        Assert.True(await CoordinatedStateWriteHelper.TryCommitAsync(_files,
            new CoordinatedStateWriteHelper.PlannedWrite(ReplacePath, null, final == "recreate" ? null : "{\"intermediate\":true}", true,
                ExactPrevious: new(true, _replaceBefore)),
            new CoordinatedStateWriteHelper.PlannedWrite(CreatePath, null, NextJson, RequireCurrentBaseline: true),
            new CoordinatedStateWriteHelper.PlannedWrite(ReplacePath, null, next, true, ExactPrevious: new(true, _replaceBefore)),
            CoordinatedStateWriteHelper.CreateExactGuardWrite(ReplacePath, new(true, _replaceBefore))));

        Assert.Equal(new[] { _files.ResolvePath(CreatePath), _files.ResolvePath(ReplacePath) }, members);
        Assert.Equal(_replaceBefore, original);
        AssertImage(ReplacePath, next == null ? null : Utf8WithPreamble(next));
        AssertImage(CreatePath, Utf8WithPreamble(NextJson));
    }

    [Fact]
    public async Task ResolvedSeparatorAndWhitespaceAliasesPublishOneFinalDestination()
    {
        string[]? members = null;
        _observer = (phase, _) => { if (phase == TrustedLocalPublicationPhase.IntentPublished) members = ReadActiveMembers(); };
        var alias = "  " + ReplacePath.Replace('/', Path.AltDirectorySeparatorChar) + "  ";
        Assert.Equal(_files.ResolvePath(ReplacePath), _files.ResolvePath(alias));
        Assert.True(await CoordinatedStateWriteHelper.TryCommitAsync(_files,
            new CoordinatedStateWriteHelper.PlannedWrite(ReplacePath, null, "{}"),
            new CoordinatedStateWriteHelper.PlannedWrite(alias, null, NextJson)));
        Assert.Equal(new[] { _files.ResolvePath(ReplacePath) }, members);
        AssertImage(ReplacePath, Utf8WithPreamble(NextJson));
    }

    [Fact]
    public async Task DestinationCaseUsesTheActualPlatformPolicy()
    {
        const string lower = "game_state/meta/case.json";
        const string upper = "game_state/meta/Case.json";
        File.WriteAllBytes(_files.ResolvePath(lower), [1]);
        File.WriteAllBytes(_files.ResolvePath(upper), [1]);
        string[]? members = null;
        _observer = (phase, _) => { if (phase == TrustedLocalPublicationPhase.IntentPublished) members = ReadActiveMembers(); };
        Assert.True(await CoordinatedStateWriteHelper.TryCommitAsync(_files,
            new CoordinatedStateWriteHelper.PlannedWrite(lower, null, "{\"case\":1}"), new CoordinatedStateWriteHelper.PlannedWrite(upper, null, "{\"case\":2}")));
        Assert.Equal(OperatingSystem.IsWindows() ? 1 : 2, members!.Length);
        AssertImage(lower, Utf8WithPreamble(OperatingSystem.IsWindows() ? "{\"case\":2}" : "{\"case\":1}"));
        AssertImage(upper, Utf8WithPreamble("{\"case\":2}"));
    }

    [Theory]
    [InlineData("semantic")]
    [InlineData("exact")]
    [InlineData("unguarded")]
    public async Task SemanticExactAndOptionalBaselinesKeepTheirDistinctContracts(string mode)
    {
        const string value = "{ \"value\" : 1 }";
        var before = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(value)).ToArray();
        File.WriteAllBytes(_files.ResolvePath(ReplacePath), before);
        var write = new CoordinatedStateWriteHelper.PlannedWrite(ReplacePath,
            mode == "unguarded" ? "{\"different\":true}" : "{\"value\":1}", NextJson,
            RequireCurrentBaseline: mode != "unguarded",
            ExactPrevious: mode == "semantic" ? null : new(true, Utf8WithPreamble(value)));

        Assert.Equal(mode != "exact", await CoordinatedStateWriteHelper.TryCommitAsync(_files, write));
        AssertImage(ReplacePath, mode == "exact" ? before : Utf8WithPreamble(NextJson));
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("link")]
    public async Task InvalidLaterTypeFailsBeforeAnyNewIntentOrMutation(string kind)
    {
        var outside = Path.Combine(_root, "outside-sentinel");
        File.WriteAllBytes(outside, [91]);
        File.Delete(_files.ResolvePath(DeletePath));
        if (kind == "directory") Directory.CreateDirectory(_files.ResolvePath(DeletePath));
        else File.CreateSymbolicLink(_files.ResolvePath(DeletePath), outside);
        var intents = 0;
        _observer = (phase, _) => { if (phase == TrustedLocalPublicationPhase.IntentPublished) intents++; };

        await Assert.ThrowsAsync<InvalidDataException>(() => CoordinatedStateWriteHelper.TryCommitAsync(_files,
            new CoordinatedStateWriteHelper.PlannedWrite(ReplacePath, null, NextJson), new CoordinatedStateWriteHelper.PlannedWrite(DeletePath, null, NextJson)));

        Assert.Equal(0, intents);
        AssertImage(ReplacePath, _replaceBefore);
        Assert.Equal(new byte[] { 91 }, File.ReadAllBytes(outside));
        Assert.False(File.Exists(Active));
    }

    [Fact]
    public async Task ExistingUnresolvedJournalBlocksOwnedAdmissionBeforeAnyNewAttempt()
    {
        byte[] evidence = [0xFF, 0xFE, 0x37];
        Directory.CreateDirectory(Journal);
        File.WriteAllBytes(Active, evidence);
        var intents = 0;
        _observer = (phase, _) => { if (phase == TrustedLocalPublicationPhase.IntentPublished) intents++; };

        await Assert.ThrowsAsync<InvalidDataException>(() => CoordinatedStateWriteHelper.TryCommitAsync(_files, Writes()));

        Assert.Equal(0, intents);
        Assert.Equal(evidence, File.ReadAllBytes(Active));
        AssertImage(ReplacePath, _replaceBefore);
        AssertImage(CreatePath, null);
        AssertImage(DeletePath, _deleteBefore);
    }

    [Fact]
    public async Task UnknownBeforeImageAtPublicationPreflightIsNotReportedAsRestored()
    {
        byte[] unknown = [77];
        var boundaryHits = 0;
        var intents = 0;
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = path =>
                {
                    if (path == ReplacePath)
                    {
                        boundaryHits++;
                        File.WriteAllBytes(_files.ResolvePath(DeletePath), unknown);
                    }
                    return Task.CompletedTask;
                },
                LocalPublicationObserver = (phase, _) => { if (phase == TrustedLocalPublicationPhase.IntentPublished) intents++; }
            });

        await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() =>
            CoordinatedStateWriteHelper.TryCommitAsync(files, Writes()));

        Assert.Equal(1, boundaryHits);
        Assert.Equal(0, intents);
        AssertImage(ReplacePath, _replaceBefore);
        AssertImage(CreatePath, null);
        AssertImage(DeletePath, unknown);
        Assert.False(File.Exists(Active));
    }

    [Fact]
    public async Task OriginalRecorderRejectionDoesNotEnterTheCommonJournal()
    {
        var recorder = new RejectingRecorder();
        var intents = 0;
        var callbacks = 0;
        _observer = (phase, _) => { if (phase == TrustedLocalPublicationPhase.IntentPublished) intents++; };
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        lease.MutationIntentRecorder = recorder;
        Task<bool> Commit() => CoordinatedStateWriteHelper.TryCommitWithHookAsync(_files, lease,
            _ => { callbacks++; return Task.CompletedTask; }, new CoordinatedStateWriteHelper.PlannedWrite(CreatePath, null, NextJson));
        var result = await Commit();

        Assert.False(result);
        Assert.Equal(1, recorder.Intents);
        Assert.Equal(0, callbacks);
        Assert.Equal(0, intents);
        AssertImage(CreatePath, null);
        AssertClean();
    }

    [Fact]
    public async Task RecorderFreeLegacyRecoveryRetainsTheOriginalNoOpCallbackRoute()
    {
        var callbacks = 0;
        var intents = 0;
        _observer = (phase, _) => { if (phase == TrustedLocalPublicationPhase.IntentPublished) intents++; };
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Null(lease.MutationIntentRecorder);
        Assert.True(_files.UsesTrustedLocalWriter(lease, CreatePath));
        await _files.RunLegacyStorageRecoveryAsync(lease, async () =>
        {
            Assert.False(_files.UsesTrustedLocalWriter(lease, CreatePath));
            Assert.True(await CoordinatedStateWriteHelper.TryCommitWithHookAsync(_files, lease,
                _ => { callbacks++; return Task.CompletedTask; },
                new CoordinatedStateWriteHelper.PlannedWrite(CreatePath, null, null)));
        });
        Assert.True(_files.UsesTrustedLocalWriter(lease, CreatePath));
        Assert.Equal(1, callbacks);
        Assert.Equal(0, intents);
        AssertImage(CreatePath, null);
        AssertClean();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclaredSameImageMutationsStillReceiveOneDurableDecision(bool absent)
    {
        var path = absent ? CreatePath : ReplacePath;
        var next = absent ? null : NextJson;
        if (!absent) File.WriteAllBytes(_files.ResolvePath(path), Utf8WithPreamble(NextJson));
        var intents = 0;
        var commits = 0;
        _observer = (phase, _) =>
        {
            if (phase == TrustedLocalPublicationPhase.IntentPublished)
            {
                intents++;
                Assert.Equal(new[] { _files.ResolvePath(path) }, ReadActiveMembers());
            }
            if (phase == TrustedLocalPublicationPhase.Committed) commits++;
        };
        Assert.True(await CoordinatedStateWriteHelper.TryCommitAsync(_files,
            new CoordinatedStateWriteHelper.PlannedWrite(path, next, next, RequireCurrentBaseline: true)));
        Assert.Equal(1, intents);
        Assert.Equal(1, commits);
        AssertImage(path, absent ? null : Utf8WithPreamble(NextJson));
        AssertClean();
    }

    [Fact]
    public async Task OrdinaryApplyCallbackIsRejectedRatherThanSilentlyDiscarded()
    {
        var callbacks = 0;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CoordinatedStateWriteHelper.TryCommitWithHookAsync(_files,
                _ => { callbacks++; return Task.CompletedTask; }, Writes()));
        Assert.Contains("phase observation", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, callbacks);
        AssertImage(ReplacePath, _replaceBefore);
        AssertClean();
    }

    [Fact]
    public async Task MixedOrdinaryAndOriginalArtifactMembersFailBeforeMutation()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => CoordinatedStateWriteHelper.TryCommitAsync(_files,
            new CoordinatedStateWriteHelper.PlannedWrite(ReplacePath, null, NextJson), new CoordinatedStateWriteHelper.PlannedWrite(ExplorerLocalTurnRollbackArtifacts.Root + "/mixed.json", null, NextJson)));
        AssertImage(ReplacePath, _replaceBefore);
        AssertClean();
    }

    [Fact]
    public async Task ThrowingPostCommitDiagnosticCannotChangeTheCommittedHelperResult()
    {
        var logger = new ThrowingWarningLogger();
        var committedHits = 0;
        var files = new FileSystemManager(_root, logger, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, _) =>
                {
                    if (phase != TrustedLocalPublicationPhase.Committed) return;
                    committedHits++;
                    throw new InjectedFailure();
                }
            });
        bool? committed = null;
        var error = await Record.ExceptionAsync(async () => { committed = await CoordinatedStateWriteHelper.TryCommitAsync(files, Writes()); });

        Assert.Equal(1, committedHits);
        Assert.Equal(1, logger.Warnings);
        Assert.Null(error);
        Assert.True(committed);
        AssertAfterImages();
    }

    [Fact]
    public async Task ThrowingCompletedReleaseDiagnosticCannotChangeTheCompletedDisposition()
    {
        var logger = new ThrowingWarningLogger();
        var files = new FileSystemManager(_root, logger);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        lease.ExternalPublicationContext = new ThrowingReleaseContext();

        var error = await Record.ExceptionAsync(() => CoordinatedStateWriteHelper
            .ReleaseOwnedLeaseAsync(files, lease, completed: true, operationFailure: null).AsTask());

        Assert.Equal(1, logger.Warnings);
        Assert.Null(error);
        Assert.False(lease.IsActive);
    }

    private sealed class RejectingRecorder : ICanonicalMutationIntentRecorder
    {
        internal int Intents { get; private set; }
        public Task RecordMutationIntentAsync(string path, byte[]? desired) { Intents++; throw new InjectedFailure(); }
        public Task RecordMutationNonPublicationAsync(string path) => Task.CompletedTask;
        public Task RecordMutationPublicationAsync(string path, CanonicalMutationPublication publication) =>
            throw new InvalidOperationException("The rejected intent cannot publish a physical receipt.");
    }

    private sealed class ThrowingReleaseContext : IDisposable
    {
        public void Dispose() => throw new IOException("Injected committed release follow-up.");
    }

    private sealed class ThrowingWarningLogger : ILogger<FileSystemManager>
    {
        internal int Warnings { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (level != LogLevel.Warning) return;
            Warnings++;
            throw new InvalidOperationException("Injected warning sink failure.");
        }
    }
}
