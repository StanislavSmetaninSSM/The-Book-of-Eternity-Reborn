using System.Reflection;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    // Compiles after the source-missing companion but before transaction APIs.
    // OLD's immediate continuation commits the source while resource acceptance
    // is still undecided, so the unchanged retained-state assertion fails.
    [Fact]
    public async Task SourceTransaction_OldDiscardablePreparationDoesNotCommitBeforeResourceAcceptance()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        ValidationService.SpiritualWoundSourceSession owner;
        ValidationService.PreparedSpiritualSource first;
        string retained;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var acquired = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(acquired.Issues);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(acquired.Session);
            first = Assert.Single(owner.Sources);
            retained = owner.BuildInputBinding().ToJsonString();
        }
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        await using var continuation = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var prepare = owner.GetType().GetMethod("PrepareContinuationAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        if (prepare is null)
            AssertNoConflictFrameErrors((await owner.ContinueAsync(continuation)).Issues);
        else
        {
            var task = (Task)prepare.Invoke(owner, new object[] { continuation })!;
            await task;
            var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
            var issues = Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(
                result.GetType().GetProperty("Issues")!.GetValue(result));
            AssertNoConflictFrameErrors(issues);
            Assert.NotNull(result.GetType().GetProperty("Ticket")!.GetValue(result));
        }

        // No resource decision has been accepted; discard the prospective result.
        Assert.Equal(retained, owner.BuildInputBinding().ToJsonString());
        Assert.Same(first, Assert.Single(owner.Sources));
        Assert.Single(owner.CheckedExchanges);
        Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, continuation));
    }
}
