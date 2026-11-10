using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises common-planning failure disposition with real source lifetimes and no game authority fabrication.
/// </summary>
/// <param name="output">
/// Receives complete source observations, exception evidence and nonce-owned fixture cleanup.
/// </param>
public sealed class JointRepairCommonFailureTests(ITestOutputHelper output)
{
    /// <summary>
    /// Retains a real source-drift refusal when abort encounters the same failed ambient source owner.
    /// </summary>
    [Fact]
    public async Task SourceDriftAndAbortFailureRetainBothExceptions()
    {
        using var fixture = new SourceDataOperationTests.SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var source = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(source);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        var timestamp = File.GetLastWriteTimeUtc(fixture.PresentPath);
        File.WriteAllText(fixture.PresentPath, "modified");
        File.SetLastWriteTimeUtc(fixture.PresentPath, timestamp);
        var primary = await Assert.ThrowsAsync<InvalidDataException>(() => files.VerifySourceDataAsync(lease));
        Assert.Equal("Operation source changed: present.txt", primary.Message);
        Assert.Contains("VerifyCoreAsync", primary.StackTrace);
        Assert.Throws<InvalidOperationException>(() => files.GetCurrentSemanticDataOperation());
        var abortCalls = 0;
        Exception? abortFailure = null;
        var observed = Record.Exception(() => WoundAcceptedTurnPlanner.HandleJointRepairCommonPlanFailure(primary, () =>
        {
            abortCalls++;
            try { files.GetCurrentSemanticDataOperation(); }
            catch (Exception error) { abortFailure = error; throw; }
        }));
        Assert.Equal(1, abortCalls);
        var secondary = Assert.IsType<InvalidOperationException>(abortFailure);
        Assert.Equal("The source operation is no longer available.", secondary.Message);
        Assert.Contains("RequireOpen", secondary.StackTrace);
        Assert.True(lease.IsActive);
        Assert.Equal("modified", File.ReadAllText(fixture.PresentPath));
        Assert.Throws<InvalidOperationException>(() => source.GetSemanticDataOperation());
        AssertPreservedFailures(observed, primary, secondary);
    }

    /// <summary>
    /// Does not turn completed or disposed source refusals into successful cleanup or stale DATA fallback.
    /// </summary>
    /// <param name="disposed">
    /// Whether the actual source owner is disposed rather than successfully completed before abort.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnavailableSourceAbortRetainsPrimaryAndLifecycleRefusal(bool disposed)
    {
        using var fixture = new SourceDataOperationTests.SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var source = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(source);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        if (disposed) source.Dispose();
        else await source.CompleteAsync();
        var primary = Record.Exception(() => source.GetSemanticDataOperation());
        Assert.NotNull(primary);
        Exception? abortFailure = null;
        var abortCalls = 0;
        var observed = Record.Exception(() => WoundAcceptedTurnPlanner.HandleJointRepairCommonPlanFailure(primary, () =>
        {
            abortCalls++;
            try { source.GetSemanticDataOperation(); }
            catch (Exception error) { abortFailure = error; throw; }
        }));
        Assert.Equal(1, abortCalls);
        Assert.NotNull(abortFailure);
        if (disposed)
        {
            Assert.IsType<ObjectDisposedException>(primary);
            Assert.IsType<ObjectDisposedException>(abortFailure);
            Assert.Null(files.GetCurrentSemanticDataOperation());
            Assert.False(source.HasRetainedSourceReferences);
        }
        else
        {
            Assert.IsType<InvalidOperationException>(primary);
            Assert.IsType<InvalidOperationException>(abortFailure);
            Assert.Throws<InvalidOperationException>(() => files.GetCurrentSemanticDataOperation());
        }
        Assert.Contains("RequireOpen", primary.StackTrace);
        Assert.Contains("RequireOpen", abortFailure.StackTrace);
        Assert.True(lease.IsActive);
        Assert.Equal("original", File.ReadAllText(fixture.PresentPath));
        AssertPreservedFailures(observed, primary, abortFailure);
    }

    /// <summary>
    /// Keeps the existing failed-result contract when abort succeeds or no common authority was issued.
    /// </summary>
    /// <param name="issued">
    /// Whether the caller supplied its abort dependency.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessfulOrAbsentAbortKeepsOriginalIssue(bool issued)
    {
        var primary = new InvalidDataException("original planning refusal");
        var abortCalls = 0;
        var result = WoundAcceptedTurnPlanner.HandleJointRepairCommonPlanFailure(primary,
            issued ? () => abortCalls++ : null);
        Assert.Equal(issued ? 1 : 0, abortCalls);
        Assert.False(result.Success);
        Assert.Null(result.Plan);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("wound_plan_joint_common_plan_invalid", issue.Code);
        Assert.Equal("original planning refusal", issue.Message);
        Assert.Equal("acceptedTurn.wounds", issue.FilePath);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal(IssueCategory.StateConsistency, issue.Category);
        Assert.Equal("Client", issue.Actor);
        Assert.Equal("mortal_qte_private_source", issue.Section);
        Assert.Equal("one current privately issued joint preparation", issue.Expected);
        Assert.Equal("refused", issue.Actual);
        Assert.Equal("Recompose the accepted wound turn from its sealed authority inputs.", issue.RepairHint);
        Assert.Empty(issue.RepairTargetFiles);
    }

    /// <summary>
    /// Preserves the exception types that the production enclosing handler deliberately does not catch.
    /// </summary>
    /// <param name="sessionReplaced">
    /// Whether abort reports explicit session replacement rather than an unlisted overflow failure.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OtherAbortFailuresKeepTheirOriginalIdentityAndStack(bool sessionReplaced)
    {
        Exception secondary = sessionReplaced
            ? new SessionReplacedException("replacement", "expected", "actual")
            : new OverflowException("unlisted secondary");
        var abortCalls = 0;
        void Abort()
        {
            abortCalls++;
            throw secondary;
        }
        var actual = Record.Exception(() => WoundAcceptedTurnPlanner.HandleJointRepairCommonPlanFailure(
            new InvalidDataException("original planning refusal"), Abort));
        Assert.Equal(1, abortCalls);
        Assert.Same(secondary, actual);
        Assert.Contains("Abort", actual!.StackTrace);
    }

    /// <summary>
    /// Checks the ordered original exception objects and their full diagnostic representation.
    /// </summary>
    /// <param name="observed">The failure leaving the shared production handler.</param>
    /// <param name="primary">The first thrown planning or source failure.</param>
    /// <param name="secondary">The later thrown abort refusal.</param>
    private void AssertPreservedFailures(Exception? observed, Exception primary, Exception secondary)
    {
        output.WriteLine("Primary: " + primary);
        output.WriteLine("Abort: " + secondary);
        output.WriteLine("Observed: " + observed);
        var aggregate = Assert.IsType<AggregateException>(observed);
        Assert.Collection(aggregate.InnerExceptions,
            error => Assert.Same(primary, error), error => Assert.Same(secondary, error));
        Assert.NotEmpty(primary.StackTrace!);
        Assert.NotEmpty(secondary.StackTrace!);
        Assert.Contains(primary.ToString(), aggregate.ToString());
        Assert.Contains(secondary.ToString(), aggregate.ToString());
    }
}
