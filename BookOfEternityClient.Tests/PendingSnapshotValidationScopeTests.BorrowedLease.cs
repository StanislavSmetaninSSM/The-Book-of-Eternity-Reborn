using System.Reflection;
using System.Runtime.ExceptionServices;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
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

    private sealed partial class Fixture
    {
        /// <summary>
        /// Invokes the same borrowed lifetime runner used by terminal QTE validation.
        /// </summary>
        /// <param name="lease">
        /// The original lease acquired by the caller and retained after the invocation.
        /// </param>
        /// <param name="body">
        /// Real read-only validation consumers whose result must pass final proof.
        /// </param>
        /// <returns>
        /// The runner's ordered issues or its original unwrapped exception.
        /// </returns>
        internal async Task<List<ValidationIssue>> RunBorrowedAsync(
            FileSystemManager.CanonicalWriteLease lease, Func<Task<List<ValidationIssue>>> body)
        {
            try
            {
                var method = typeof(ValidationService).GetMethod("RunBorrowedPendingSnapshotValidationAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                return await (Task<List<ValidationIssue>>)method.Invoke(_validator, [lease, body])!;
            }
            catch (TargetInvocationException error)
            {
                ExceptionDispatchInfo.Capture(error.InnerException!).Throw();
                throw;
            }
        }
    }
}
