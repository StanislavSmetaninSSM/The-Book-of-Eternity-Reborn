using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PendingSnapshotValidationScopeTests
{
    /// <summary>
    /// An in-flight same-lease writer prevents proof from opening before or after its boundary guard.
    /// </summary>
    /// <param name="operation">
    /// Real writer route to pause, including direct publication and directory/image preparation.
    /// </param>
    /// <param name="afterBoundary">
    /// True pauses after boundary validation; false pauses immediately before it.
    /// </param>
    /// <returns>
    /// Completion after refusal before entry authentication and successful writer completion.
    /// </returns>
    [Theory]
    [InlineData("write", false)] [InlineData("write", true)]
    [InlineData("append", false)] [InlineData("append", true)]
    [InlineData("delete", false)] [InlineData("delete", true)]
    [InlineData("coordinated", false)] [InlineData("coordinated", true)]
    [InlineData("image", false)] [InlineData("image", true)]
    [InlineData("directory", false)] [InlineData("directory", true)]
    [InlineData("move", false)] [InlineData("move", true)]
    public async Task BorrowedMutation_ExistingWriterRefusesProof(string operation, bool afterBoundary)
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Pause(string path) { entered.TrySetResult(); await release.Task; }
        if (afterBoundary) fixture.AfterMutation = Pause; else fixture.BeforeMutation = Pause;
        var writer = Task.Run(() => RunBorrowedWriterAsync(fixture, lease, operation));
        var reached = false;
        try
        {
            // Observe writer failures directly rather than turning unsupported setup into a scheduling timeout.
            await Task.WhenAny(entered.Task, writer).WaitAsync(TimeSpan.FromSeconds(5));
            if (!entered.Task.IsCompleted) await writer;
            Assert.True(entered.Task.IsCompleted);
            fixture.Ordinary = fixture.Cohort = 0;
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunBorrowedAsync(lease, () =>
            {
                reached = true;
                return Task.FromResult(new List<ValidationIssue>());
            }));
            Assert.False(reached);
            Assert.Equal(0, fixture.Ordinary + fixture.Cohort);
        }
        finally
        {
            release.TrySetResult();
            await writer.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.BeforeMutation = fixture.AfterMutation = null;
        }
        Assert.True(lease.IsActive);
        await fixture.RunBorrowedAsync(lease, () => Task.FromResult(new List<ValidationIssue>()));
    }

    /// <summary>
    /// A proof already holding the original access rejects real writers before effects and permits them afterward.
    /// </summary>
    /// <param name="operation">
    /// Participating writer, pruning, recovery or profile route to exercise.
    /// </param>
    /// <returns>
    /// Completion after protected bytes remain unchanged and the same lease can write after close.
    /// </returns>
    [Theory]
    [InlineData("write")] [InlineData("append")] [InlineData("delete")]
    [InlineData("coordinated")] [InlineData("image")] [InlineData("directory")]
    [InlineData("empty-directory")] [InlineData("recovery")] [InlineData("profile")]
    public async Task BorrowedMutation_ProofRefusesWriter(string operation)
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        Directory.CreateDirectory(fixture.Files.ResolvePath("game_state/world/empty"));
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var before = File.ReadAllBytes(fixture.Files.ResolvePath("game_state/world/scope0.json"));
        await fixture.RunBorrowedAsync(lease, async () =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => RunBorrowedWriterAsync(fixture, lease, operation));
            Assert.Equal(before, File.ReadAllBytes(fixture.Files.ResolvePath("game_state/world/scope0.json")));
            Assert.True(Directory.Exists(fixture.Files.ResolvePath("game_state/world/empty")));
            return [];
        });
        await fixture.Files.WriteFileAtomicAsync(lease, "game_state/world/scope0.json", "{\"value\":2}");
    }

    /// <summary>
    /// Cancellation or a publication rollback releases the whole writer reservation for a later proof.
    /// </summary>
    /// <param name="rollback">
    /// True fails actual member publication; false cancels before the canonical boundary.
    /// </param>
    /// <returns>
    /// Completion after original bytes survive and subsequent proof and write succeed.
    /// </returns>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BorrowedMutation_FailureReleasesToken(bool rollback)
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        Exception primary = rollback ? new InvalidDataException("injected publication failure")
            : new OperationCanceledException("injected writer cancellation");
        if (rollback)
            fixture.OnPublication = (phase, _) => { if (phase == TrustedLocalPublicationPhase.MemberPublished) throw primary; };
        else fixture.BeforeMutation = _ => Task.FromException(primary);
        var before = File.ReadAllBytes(fixture.Files.ResolvePath("game_state/world/scope0.json"));
        var error = await Record.ExceptionAsync(() => RunBorrowedWriterAsync(fixture, lease, "coordinated"));
        fixture.BeforeMutation = null;
        fixture.OnPublication = null;
        Assert.Same(primary, error);
        Assert.Equal(before, File.ReadAllBytes(fixture.Files.ResolvePath("game_state/world/scope0.json")));
        await fixture.RunBorrowedAsync(lease, () => Task.FromResult(new List<ValidationIssue>()));
        await fixture.Files.WriteFileAtomicAsync(lease, "game_state/world/scope0.json", "{\"value\":3}");
    }

    /// <summary>
    /// Executes the actual supported writer selected by a mutation-ordering test.
    /// </summary>
    /// <param name="fixture">
    /// Independent physical filesystem and its existing scheduling hooks.
    /// </param>
    /// <param name="lease">
    /// Original existing lease shared with the competing validation invocation.
    /// </param>
    /// <param name="operation">
    /// Supported writer route; unknown names fail instead of becoming a no-op.
    /// </param>
    /// <returns>
    /// Completion after the real publication, pruning or recovery operation finishes.
    /// </returns>
    private static async Task RunBorrowedWriterAsync(Fixture fixture,
        FileSystemManager.CanonicalWriteLease lease, string operation)
    {
        const string path = "game_state/world/scope0.json";
        const string before = "{\"value\":1}";
        const string after = "{\"value\":2}";
        switch (operation)
        {
            case "write": await fixture.Files.WriteFileAtomicAsync(lease, path, after); break;
            case "append": await fixture.Files.AppendFileAtomicAsync(lease, path, " "); break;
            case "delete": fixture.Files.DeleteFile(lease, path); break;
            case "coordinated":
                Assert.True(await CoordinatedStateWriteHelper.TryCommitAsync(fixture.Files, lease,
                    new CoordinatedStateWriteHelper.PlannedWrite(path, before, after)));
                break;
            case "image":
                fixture.Files.RequireCommittedLocalPublication(await fixture.Files.PublishLocalImageFilesAsync(lease,
                    [new(path, TrustedLocalFileImage.FromBytes(Encoding.UTF8.GetBytes(before)),
                        TrustedLocalFileImage.FromBytes(Encoding.UTF8.GetBytes(after)))]));
                break;
            case "directory": fixture.Files.DeleteDirectoryTree(lease, "game_state/world"); break;
            case "empty-directory":
                Assert.True(fixture.Files.TryRemoveEmptyCanonicalDirectory(lease, "game_state/world/empty"));
                break;
            case "profile": await fixture.Files.PublishStandaloneDarenProfileAsync(lease, "{}"u8.ToArray()); break;
            case "move":
                var staged = Path.Combine(fixture.Files.RuntimeRootPath, "save-staging", "borrowed.bin");
                Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
                File.WriteAllText(staged, after);
                await fixture.Files.MoveRuntimeFileIntoCanonicalSessionAsync(lease, staged, "game_state/world/moved.json");
                break;
            case "recovery":
                try
                {
                    typeof(FileSystemManager).GetMethod("RecoverTrustedLocalStorage", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(fixture.Files, [lease]);
                }
                catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException!).Throw(); }
                break;
            default: throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown mutation route");
        }
    }
}
