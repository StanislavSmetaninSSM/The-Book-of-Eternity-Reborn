using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PendingSnapshotValidationScopeTests
{
    /// <summary>
    /// Repeated real consumers under an existing QTE lease have a fixed invocation read budget.
    /// </summary>
    /// <param name="consumers">
    /// Number of alternating synchronous and asynchronous source lookups.
    /// </param>
    /// <param name="cold">
    /// Whether the original lease acquisition must first authenticate its rollback cohort.
    /// </param>
    /// <returns>
    /// Completion after checking invocation and separately measured acquisition reads.
    /// </returns>
    [Theory]
    [InlineData(1, false)] [InlineData(8, false)] [InlineData(32, false)]
    [InlineData(1, true)] [InlineData(8, true)] [InlineData(32, true)]
    public async Task BorrowedProof_ReadBudget(int consumers, bool cold)
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(cold);
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var acquisition = fixture.Ordinary + fixture.Cohort;
        fixture.Ordinary = fixture.Cohort = 0;

        var issues = await fixture.RunBorrowedAsync(lease, async () =>
        {
            for (var i = 0; i < consumers; i++)
                Assert.Equal("Usable", Status(await fixture.LookupAsync(i % 2 != 0,
                    writeLease: i % 2 != 0 ? lease : null)));
            return [new("first", IssueSeverity.Error, "one"), new("second", IssueSeverity.Error, "two")];
        });

        var invocation = fixture.Ordinary + fixture.Cohort;
        _output.WriteLine($"Borrowed N={consumers}; cold={cold}; acquisition={acquisition}; invocation={invocation}; ordinary={fixture.Ordinary}; cohort={fixture.Cohort}");
        Assert.Equal(new[] { "first", "second" }, issues.Select(issue => issue.FilePath));
        Assert.True(lease.IsActive);
        Assert.InRange(invocation, 1, 6);
        Assert.InRange(acquisition + invocation, 1, cold ? 12 : 9);
    }

    /// <summary>
    /// Durable source changes invalidate a borrowed proof even when a replacement is independently valid.
    /// </summary>
    /// <param name="change">
    /// Dependency mutation applied after the first real lookup.
    /// </param>
    /// <returns>
    /// Completion after the original proof refuses the changed source.
    /// </returns>
    [Theory]
    [InlineData("overwrite")] [InlineData("delete")] [InlineData("bom")]
    [InlineData("same-mtime")] [InlineData("manifest")] [InlineData("authority")]
    [InlineData("request")] [InlineData("session")] [InlineData("turn")]
    [InlineData("alternative")] [InlineData("generation")] [InlineData("valid-pair")]
    public async Task BorrowedProof_DependencyChange(string change)
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var reached = false;
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.RunBorrowedAsync(lease, async () =>
        {
            Assert.Equal("Usable", Status(await fixture.LookupAsync(false)));
            await fixture.ChangeAsync(change);
            reached = true;
            return [];
        }));
        Assert.True(reached);
        Assert.True(lease.IsActive);
    }

    /// <summary>
    /// Consumer DTO mutation cannot alter proof authority, and the next invocation authenticates anew.
    /// </summary>
    /// <returns>
    /// Completion after independent copies and a fresh replacement source have been observed.
    /// </returns>
    [Fact]
    public async Task BorrowedProof_CopiesAndFreshAfterOuterWrite()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await fixture.RunBorrowedAsync(lease, async () =>
        {
            var first = Manifest(await fixture.LookupAsync(false));
            first.GetType().GetProperty("SessionId")!.SetValue(first, "consumer-change");
            var second = Manifest(await fixture.LookupAsync(true, writeLease: lease));
            Assert.NotSame(first, second);
            Assert.Equal("session", second.GetType().GetProperty("SessionId")!.GetValue(second));
            return [];
        });
        await fixture.Files.WriteFileAtomicAsync(lease, "game_state/world/scope0.json", "{\"value\":2}");
        await fixture.ChangeAsync("valid-pair");
        await fixture.RunBorrowedAsync(lease, async () =>
        {
            var fresh = Manifest(await fixture.LookupAsync(true, writeLease: lease));
            Assert.Equal("replacement-session", fresh.GetType().GetProperty("SessionId")!.GetValue(fresh));
            return [];
        });
    }

    /// <summary>
    /// Missing and malformed manifests keep their original distinct reader outcomes.
    /// </summary>
    /// <param name="missing">
    /// True removes the manifest; false replaces it with malformed JSON.
    /// </param>
    /// <returns>
    /// Completion after both lookup modes report the expected status.
    /// </returns>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BorrowedProof_MissingAndUnusable(bool missing)
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var path = fixture.Files.ResolvePath("game_state/control/pending_turn_snapshot.json");
        if (missing) File.Delete(path); else File.WriteAllText(path, "{");
        await fixture.RunBorrowedAsync(lease, async () =>
        {
            Assert.Equal(missing ? "Missing" : "Unusable", Status(await fixture.LookupAsync(false)));
            Assert.Equal(missing ? "Missing" : "Unusable", Status(await fixture.LookupAsync(true, writeLease: lease)));
            return [];
        });
    }

    /// <summary>
    /// Mutable current bytes remain physical and the actual continuity rule keeps its ordered issue.
    /// </summary>
    /// <returns>
    /// Completion after comparing actual scalar and borrowed validation outcomes.
    /// </returns>
    [Fact]
    public async Task BorrowedProof_CurrentIssues()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false, flagBaseline: true);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        const GameStateValidationPhase phase = GameStateValidationPhase.AfterlifeGlobalFlagPreTurnContinuity;
        Assert.Empty(await fixture.Validator.ValidateGameStateAsync(phase));
        File.WriteAllText(fixture.Files.ResolvePath(AfterlifeGlobalFlagState.StatePath), "{\"schemaVersion\":1,\"flags\":[]}");
        var scalar = await fixture.Validator.ValidateGameStateAsync(phase);
        Assert.Equal("afterlife_global_flag_removed_without_obsolete_marker", Assert.Single(scalar).Code);
        var borrowed = await fixture.RunBorrowedAsync(lease, () => (Task<List<ValidationIssue>>)typeof(ValidationService)
            .GetMethod("ValidateGameStateCoreAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(fixture.Validator, [new GameStateValidationSelection(phase), false, null, null])!);
        Assert.Equal(JsonSerializer.Serialize(scalar), JsonSerializer.Serialize(borrowed));
        await fixture.RunBorrowedAsync(lease, async () =>
        {
            const string current = "game_state/world/scope1.json";
            var before = await fixture.Files.ReadFileAsync(current);
            File.WriteAllText(fixture.Files.ResolvePath(current), "{\"value\":2}");
            Assert.NotEqual(before, await fixture.Files.ReadFileAsync(current));
            return [];
        });
    }

    /// <summary>
    /// Final physical read failure cannot publish issues or dispose the caller's lease.
    /// </summary>
    /// <returns>
    /// Completion after preserving the actual I/O exception and allowing a fresh invocation.
    /// </returns>
    [Fact]
    public async Task BorrowedProof_FinalReadFailure()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var error = new IOException("Final source read failure");
        var final = false;
        fixture.OnRead = path => final && path.EndsWith(".rollback.json", StringComparison.Ordinal)
            ? Task.FromException(error) : Task.CompletedTask;
        var observed = await Assert.ThrowsAsync<IOException>(() => fixture.RunBorrowedAsync(lease, () =>
        {
            final = true;
            return Task.FromResult(new List<ValidationIssue>());
        }));
        Assert.Same(error, observed);
        Assert.True(lease.IsActive);
        fixture.OnRead = null;
        await fixture.RunBorrowedAsync(lease, () => Task.FromResult(new List<ValidationIssue>()));
    }

    /// <summary>
    /// An unusable entry source still binds declared rollback dependencies until final proof.
    /// </summary>
    /// <returns>
    /// Completion after a durable dependency change refuses issue publication.
    /// </returns>
    [Fact]
    public async Task BorrowedProof_UnusableSourceBindsDependencies()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await fixture.ChangeAsync("request");
        var reached = false;
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.RunBorrowedAsync(lease, async () =>
        {
            Assert.Equal("Unusable", Status(await fixture.LookupAsync(false)));
            await fixture.ChangeAsync("overwrite");
            reached = true;
            return [];
        }));
        Assert.True(reached);
    }

    /// <summary>
    /// Borrowed teardown preserves all original ownership through success, failure and cancellation.
    /// </summary>
    /// <param name="outcome">
    /// Success, primary failure or cancellation of the actual validation consumer.
    /// </param>
    /// <returns>
    /// Completion after proving post-validation writes and the next invocation remain legal.
    /// </returns>
    [Theory]
    [InlineData("success")] [InlineData("primary")] [InlineData("cancel")]
    public async Task BorrowedCustody_LeaseSurvives(string outcome)
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var admission = lease.MainAdmission;
        var registration = lease.AmbientRegistration;
        var context = new BorrowedCloseSentinel();
        lease.ExternalPublicationContext = context;
        try
        {
            Exception? primary = outcome switch
            {
                "primary" => new InvalidDataException("consumer failed"),
                "cancel" => new OperationCanceledException("consumer canceled"),
                _ => null
            };
            var error = await Record.ExceptionAsync(() => fixture.RunBorrowedAsync(lease, () => primary == null
                ? Task.FromResult(new List<ValidationIssue>()) : Task.FromException<List<ValidationIssue>>(primary)));
            Assert.Same(primary, error);
            Assert.True(lease.IsActive);
            Assert.Same(admission, lease.MainAdmission);
            Assert.Same(registration, lease.AmbientRegistration);
            Assert.True(registration!.Active);
            Assert.False(admission!.Closed);
            Assert.Same(context, lease.ExternalPublicationContext);
            Assert.Equal(0, context.Calls);
            await fixture.Files.WriteFileAtomicAsync(lease, "game_state/world/scope0.json", "{\"value\":2}");
            await fixture.RunBorrowedAsync(lease, () => Task.FromResult(new List<ValidationIssue>()));
        }
        finally { lease.ExternalPublicationContext = null; }
    }

    /// <summary>
    /// Close attempts reject before detaching resources or changing the original owner's flags.
    /// </summary>
    /// <param name="close">
    /// Actual lease, admission, ancestor, bound-close or terminal-freeze entrypoint to exercise.
    /// </param>
    /// <returns>
    /// Completion after the original owner remains usable and later closes normally.
    /// </returns>
    [Theory]
    [InlineData("lease")] [InlineData("physical")] [InlineData("ancestor")]
    [InlineData("closing")] [InlineData("freeze")]
    public async Task BorrowedCustody_CloseRefuses(string close)
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var physical = lease.MainAdmission!;
        var registration = lease.AmbientRegistration;
        await fixture.RunBorrowedAsync(lease, async () =>
        {
            if (close == "lease") await Assert.ThrowsAsync<InvalidOperationException>(() => lease.DisposeAsync().AsTask());
            else if (close == "physical") Assert.Throws<InvalidOperationException>(physical.Dispose);
            else if (close == "ancestor") Assert.Throws<InvalidOperationException>(owner.Dispose);
            else if (close == "closing") Assert.Throws<InvalidOperationException>(owner.BeginClosing);
            else Assert.Throws<InvalidOperationException>(() => FreezeAccess(owner));
            Assert.True(lease.IsActive);
            Assert.Same(physical, lease.MainAdmission);
            Assert.Same(registration, lease.AmbientRegistration);
            Assert.True(registration!.Active);
            Assert.False(owner.Closed || owner.BoundClosing || physical.Closed);
            return [];
        });
        await fixture.Files.WriteFileAtomicAsync(lease, "game_state/world/scope0.json", "{\"value\":2}");
    }

    /// <summary>
    /// A bound owner closing first prevents any new borrowed proof.
    /// </summary>
    /// <returns>
    /// Completion after refusal before the consumer runs.
    /// </returns>
    [Fact]
    public async Task BorrowedCustody_ClosingWinsBeforeReservation()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        owner.BeginClosing();
        var reached = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunBorrowedAsync(lease, () =>
        {
            reached = true;
            return Task.FromResult(new List<ValidationIssue>());
        }));
        Assert.False(reached);
    }

    /// <summary>
    /// Closing a completed nested session operation preserves its still-live outer owner for validation.
    /// </summary>
    /// <param name="borrowed">
    /// Whether validation borrows a new outer lease or opens its own read lease.
    /// </param>
    /// <returns>
    /// Completion after the actual nested session finalization and subsequent physical source lookup.
    /// </returns>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BorrowedCustody_NestedOperationCloseKeepsOuterOwner(bool borrowed)
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture.Files, () => Task.CompletedTask);
        Assert.True(owner.Acquired);
        Assert.False(owner.Closed || owner.BoundClosing);
        Func<Task<List<ValidationIssue>>> readSource = async () =>
        {
            Assert.Equal("Usable", Status(await fixture.LookupAsync(false)));
            return [];
        };
        if (borrowed)
        {
            await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
            await fixture.RunBorrowedAsync(lease, readSource);
            Assert.True(lease.IsActive);
        }
        else await fixture.RunAsync(readSource);
    }

    /// <summary>
    /// Normal competing entrypoints cannot enter a reserved validator through copied execution context.
    /// </summary>
    /// <param name="copied">
    /// True uses Task.Run with inherited context; false calls in the original flow.
    /// </param>
    /// <returns>
    /// Completion after the competing entry is refused without ending the original proof.
    /// </returns>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BorrowedCustody_CompetingEntry(bool copied)
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await fixture.RunBorrowedAsync(lease, async () =>
        {
            Task<List<ValidationIssue>> Compete() => fixture.Validator.ValidateGameStateAsync(GameStateValidationPhase.RequiredFiles);
            await Assert.ThrowsAsync<InvalidOperationException>(() => copied ? Task.Run(Compete) : Compete());
            Assert.Equal("Usable", Status(await fixture.LookupAsync(false)));
            return [];
        });
    }

    /// <summary>
    /// A retained copied context cannot read retired proof while the outer lease remains active.
    /// </summary>
    /// <returns>
    /// Completion after refusal and original lease preservation.
    /// </returns>
    [Fact]
    public async Task BorrowedCustody_RetiredContext()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<object>? copied = null;
        try
        {
            await fixture.RunBorrowedAsync(lease, () =>
            {
                copied = Task.Run(async () => { await release.Task; return await fixture.LookupAsync(true, writeLease: lease); });
                return Task.FromResult(new List<ValidationIssue>());
            });
        }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAsync<InvalidOperationException>(() => copied!);
        Assert.True(lease.IsActive);
    }

    /// <summary>
    /// An explicit foreign lease cannot create proof for another validator's filesystem.
    /// </summary>
    /// <returns>
    /// Completion after refusal without consuming either caller's lease.
    /// </returns>
    [Fact]
    public async Task BorrowedCustody_ForeignLease()
    {
        using var foreign = new Fixture();
        await foreign.PrepareAsync(false);
        await using var foreignLease = await foreign.Files.AcquireCanonicalWriteLeaseAsync();
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunBorrowedAsync(foreignLease,
            () => Task.FromResult(new List<ValidationIssue>())));
        Assert.True(foreignLease.IsActive && lease.IsActive);
    }

    /// <summary>
    /// A manager for the same path cannot adopt another manager's original lease.
    /// </summary>
    /// <returns>
    /// Completion after the foreign-manager entry is rejected before running consumers.
    /// </returns>
    [Fact]
    public async Task BorrowedCustody_SecondManager()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var otherFiles = new FileSystemManager(fixture.Files.BasePath, NullLogger<FileSystemManager>.Instance);
        var other = new ValidationService(otherFiles, NullLogger<ValidationService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunBorrowedAsync(lease,
            () => Task.FromResult(new List<ValidationIssue>()), other));
        Assert.True(lease.IsActive);
    }

    /// <summary>
    /// An explicit foreign lookup irreversibly invalidates only the active proof, preserving its outer lease.
    /// </summary>
    /// <returns>
    /// Completion after sticky refusal and a later independent successful invocation.
    /// </returns>
    [Fact]
    public async Task BorrowedCustody_ForeignLookupIsSticky()
    {
        using var foreign = new Fixture();
        await foreign.PrepareAsync(false);
        await using var foreignLease = await foreign.Files.AcquireCanonicalWriteLeaseAsync();
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var refused = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunBorrowedAsync(lease, async () =>
        {
            Assert.Equal("Usable", Status(await fixture.LookupAsync(true, writeLease: lease)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.LookupAsync(true, writeLease: foreignLease));
            refused = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.LookupAsync(true, writeLease: lease));
            return [];
        }));
        Assert.True(refused);
        Assert.True(lease.IsActive);
        await fixture.RunBorrowedAsync(lease, () => Task.FromResult(new List<ValidationIssue>()));
    }

    /// <summary>
    /// A foreign reservation cannot erase the original reservation's release registration.
    /// </summary>
    /// <returns>
    /// Completion after refusal during proof and a legitimate write and new proof after its actual release.
    /// </returns>
    [Fact]
    public async Task BorrowedCustody_ForeignReservationCannotEraseRelease()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var owner = lease.MainAdmission!;
        try
        {
            await fixture.RunBorrowedAsync(lease, async () =>
            {
                var foreign = new FileSystemManager.PendingSnapshotValidationReadScope(fixture.Files, owner, lease);
                owner.ReleaseWorldRead(foreign);
                await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Files.WriteFileAtomicAsync(
                    lease, "game_state/world/scope0.json", "{\"value\":2}"));
                return [];
            });
            await fixture.Files.WriteFileAtomicAsync(lease, "game_state/world/scope0.json", "{\"value\":2}");
            await fixture.RunBorrowedAsync(lease, () => Task.FromResult(new List<ValidationIssue>()));
        }
        finally
        {
            // Restore only this fixture's release pointer after a failed regression assertion so its owner can close.
            var access = typeof(FileSystemManager.MainAdmission).GetField("_access", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
            var retained = access.GetType().GetField("_worldRead", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(access)
                as FileSystemManager.PendingSnapshotValidationReadScope;
            if (retained != null)
            {
                typeof(FileSystemManager.MainAdmission).GetField("_worldReservedAccess", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, access);
                owner.ReleaseWorldRead(retained);
            }
        }
    }

    /// <summary>
    /// Calls the existing terminal-freeze transition without manufacturing a remote owner.
    /// </summary>
    /// <param name="owner">
    /// Actual acquired owner whose shared access is being tested.
    /// </param>
    private static void FreezeAccess(FileSystemManager.MainAdmission owner)
    {
        try
        {
            var access = typeof(FileSystemManager.MainAdmission).GetField("_access", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
            access.GetType().GetMethod("FreezeForTerminalClose", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(access, null);
        }
        catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException!).Throw(); }
    }

    /// <summary>
    /// Detects any attempt by borrowed teardown to dispose the outer publication context.
    /// </summary>
    private sealed class BorrowedCloseSentinel : IDisposable
    {
        /// <summary>
        /// Number of actual disposal calls, which must remain zero inside borrowed validation.
        /// </summary>
        internal int Calls { get; private set; }

        /// <summary>
        /// Records an unexpected disposal and supplies a secondary failure for precedence controls.
        /// </summary>
        public void Dispose()
        {
            Calls++;
            throw new IOException("Outer context must remain with its original owner");
        }
    }

    private sealed partial class Fixture
    {
        /// <summary>
        /// Optional scheduling callback before a real mutation boundary; null leaves it unchanged.
        /// </summary>
        internal Func<string, Task>? BeforeMutation;

        /// <summary>
        /// Optional scheduling callback after a real boundary validation; null leaves it unchanged.
        /// </summary>
        internal Func<string, Task>? AfterMutation;

        /// <summary>
        /// Observes actual local publication phases without manufacturing publication outcomes.
        /// </summary>
        internal Action<TrustedLocalPublicationPhase, int>? OnPublication;

        /// <summary>
        /// Observes actual retained-journal recovery phases when a test installs a callback.
        /// </summary>
        internal Action<TrustedLocalPublicationPhase, int>? OnRecovery;

        /// <summary>
        /// Invokes the same borrowed lifetime runner used by terminal QTE validation.
        /// </summary>
        /// <param name="lease">
        /// The original lease acquired by the caller and retained after the invocation.
        /// </param>
        /// <param name="body">
        /// Real read-only validation consumers whose result must pass final proof.
        /// </param>
        /// <param name="validator">
        /// Optional alternate manager's validator for ownership controls; null uses this fixture's validator.
        /// </param>
        /// <returns>
        /// The runner's ordered issues or its original unwrapped exception.
        /// </returns>
        internal async Task<List<ValidationIssue>> RunBorrowedAsync(
            FileSystemManager.CanonicalWriteLease lease, Func<Task<List<ValidationIssue>>> body,
            ValidationService? validator = null)
        {
            try
            {
                var method = typeof(ValidationService).GetMethod("RunBorrowedPendingSnapshotValidationAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                return await (Task<List<ValidationIssue>>)method.Invoke(validator ?? _validator, [lease, body])!;
            }
            catch (TargetInvocationException error)
            {
                ExceptionDispatchInfo.Capture(error.InnerException!).Throw();
                throw;
            }
        }
    }
}
