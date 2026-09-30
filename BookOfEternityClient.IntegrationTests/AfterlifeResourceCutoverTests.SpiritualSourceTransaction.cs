using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Fact]
    public async Task SourceTransaction_MissingSidePrepareExposesOnlyProspectiveResourceBatchesUntilCommit()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var full = await ReadSourceMissingCandidateAsync(context);
        var partial = full.DeepClone().AsObject();
        partial["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!.AsObject().Remove("opposition");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, partial.ToJsonString());
        ValidationService.SpiritualWoundSourceSession owner;
        ValidationService.SpiritualSourcePendingRequirement pending;
        string retained;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var acquired = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(acquired.Issues);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(acquired.Session);
            pending = Assert.Single(owner.PendingRequirements);
            retained = owner.BuildInputBinding().ToJsonString();
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, full.ToJsonString());
        await using var continuation = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var beforeBytes = await context.FileSystem.ReadFileBytesAsync(continuation, AfterlifeSpiritualConflictState.StatePath);
        var firstPrepare = await owner.PrepareContinuationAsync(continuation);
        AssertNoConflictFrameErrors(firstPrepare.Issues);
        var discarded = Assert.IsType<ValidationService.SpiritualWoundSourceSession.PreparedContinuation>(firstPrepare.Ticket);
        var baseline = ReadSourceTransactionResourceBaseline(owner);
        var currentBatches = owner.BuildResourceBatches(baseline.Owners, baseline.State);
        var prospectiveBatches = owner.BuildPreparedResourceBatches(continuation, discarded, baseline.Owners, baseline.State);
        Assert.True(currentBatches.IsValid, string.Join(Environment.NewLine, currentBatches.Issues));
        Assert.True(prospectiveBatches.IsValid, string.Join(Environment.NewLine, prospectiveBatches.Issues));
        Assert.Equal(new[] { "opposition" }, Assert.Single(currentBatches.Exchanges).PendingSides);
        Assert.Empty(Assert.Single(prospectiveBatches.Exchanges).PendingSides);
        Assert.Equal(retained, owner.BuildInputBinding().ToJsonString());
        Assert.Same(pending, Assert.Single(owner.PendingRequirements));
        Assert.Empty(owner.Sources);
        Assert.Empty(owner.CheckedExchanges);
        Assert.Empty(owner.ClaimedDice);
        Assert.Equal(0, owner.ContinuationRevision);

        // A rejected resource receipt would leave the first ticket uncommitted.
        // Re-prepare against the same source owner before accepting a later decision.
        var retry = await owner.PrepareContinuationAsync(continuation);
        AssertNoConflictFrameErrors(retry.Issues);
        var ticket = Assert.IsType<ValidationService.SpiritualWoundSourceSession.PreparedContinuation>(retry.Ticket);
        Assert.NotSame(discarded, ticket);
        Assert.Equal(retained, owner.BuildInputBinding().ToJsonString());
        var committed = owner.CommitPreparedContinuation(continuation, ticket);
        AssertNoConflictFrameErrors(committed.Issues);
        Assert.Same(owner, committed.Session);
        Assert.Equal(1, owner.ContinuationRevision);
        Assert.Single(owner.Sources);
        Assert.Single(owner.CheckedExchanges);
        Assert.Empty(owner.PendingRequirements);
        Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
        var accepted = owner.BuildInputBinding().ToJsonString();
        var stale = owner.CommitPreparedContinuation(continuation, discarded);
        Assert.Null(stale.Session);
        Assert.Contains(stale.Issues, issue => issue.Code == "spiritual_source_continuation_ticket_stale");
        var staleBatches = owner.BuildPreparedResourceBatches(continuation, discarded, baseline.Owners, baseline.State);
        Assert.False(staleBatches.IsValid);
        Assert.Equal(accepted, owner.BuildInputBinding().ToJsonString());
        Assert.Equal(beforeBytes, await context.FileSystem.ReadFileBytesAsync(continuation, AfterlifeSpiritualConflictState.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, continuation));
    }

    [Fact]
    public async Task SourceTransaction_CommitRetainsCheckedReferencesAndRejectsDuplicateCommit()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        ValidationService.SpiritualWoundSourceSession owner;
        ValidationService.PreparedSpiritualSource first;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var acquired = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(acquired.Issues);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(acquired.Session);
            first = Assert.Single(owner.Sources);
        }
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        await using var continuation = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var prepared = await owner.PrepareContinuationAsync(continuation);
        AssertNoConflictFrameErrors(prepared.Issues);
        var ticket = Assert.IsType<ValidationService.SpiritualWoundSourceSession.PreparedContinuation>(prepared.Ticket);
        Assert.Same(first, Assert.Single(owner.Sources));
        Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
        var committed = owner.CommitPreparedContinuation(continuation, ticket);
        AssertNoConflictFrameErrors(committed.Issues);
        Assert.Same(owner, committed.Session);
        Assert.Equal(1, owner.ContinuationRevision);
        Assert.Same(first, owner.Sources[0]);
        Assert.Equal(2, owner.Sources.Count);
        Assert.All(owner.Sources, source => Assert.True(owner.Owns(source)));
        Assert.Equal(new[] { 0, 1, 2, 3 }, owner.ClaimedDice);
        var retained = owner.BuildInputBinding().ToJsonString();
        var second = owner.Sources[1];
        var repeated = owner.CommitPreparedContinuation(continuation, ticket);
        Assert.Null(repeated.Session);
        Assert.Contains(repeated.Issues, issue => issue.Code == "spiritual_source_continuation_ticket_used");
        Assert.Equal(retained, owner.BuildInputBinding().ToJsonString());
        Assert.Same(second, owner.Sources[1]);
        Assert.Equal(1, owner.ContinuationRevision);
    }

    [Theory]
    [InlineData("known_side")]
    [InlineData("duplicate_dice")]
    [InlineData("late_final_strain")]
    public async Task SourceTransaction_InvalidSignedSuffixProducesNoTicketOrSourceMutation(string mutation)
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
        await WriteSourceContinuationAppendAsync(context, duplicateDice: mutation == "duplicate_dice");
        var candidate = await ReadSourceMissingCandidateAsync(context);
        if (mutation == "known_side") candidate["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["player"]!["before"] = 99;
        if (mutation == "late_final_strain") candidate["activeConflict"]!["oppositionSideStrain"] = "strained";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        await using var continuation = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var rejected = await owner.PrepareContinuationAsync(continuation);
        Assert.Null(rejected.Ticket);
        Assert.Contains(rejected.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(owner.IsCurrentOwner);
        Assert.Equal(retained, owner.BuildInputBinding().ToJsonString());
        Assert.Same(first, Assert.Single(owner.Sources));
        Assert.Single(owner.CheckedExchanges);
        Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
        Assert.Equal(0, owner.ContinuationRevision);
    }

    [Fact]
    public async Task SourceTransaction_ForeignOwnerCannotCommitAndDoesNotConsumeTicket()
    {
        await using var firstContext = await CreateCompleteConflictFrameContextAsync();
        await using var secondContext = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(firstContext);
        await WriteCompleteConflictFrameExchangeAsync(secondContext);
        await using var firstLease = await firstContext.FileSystem.AcquireCanonicalWriteLeaseAsync();
        await using var secondLease = await secondContext.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var firstAcquired = await firstContext.Validator.BeginLiveSpiritualWoundSourceSessionAsync(firstLease);
        var secondAcquired = await secondContext.Validator.BeginLiveSpiritualWoundSourceSessionAsync(secondLease);
        AssertNoConflictFrameErrors(firstAcquired.Issues);
        AssertNoConflictFrameErrors(secondAcquired.Issues);
        var first = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(firstAcquired.Session);
        var second = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(secondAcquired.Session);
        var prepared = await first.PrepareContinuationAsync(firstLease);
        AssertNoConflictFrameErrors(prepared.Issues);
        var ticket = Assert.IsType<ValidationService.SpiritualWoundSourceSession.PreparedContinuation>(prepared.Ticket);
        var secondBefore = second.BuildInputBinding().ToJsonString();
        var foreign = second.CommitPreparedContinuation(secondLease, ticket);
        Assert.Null(foreign.Session);
        Assert.Contains(foreign.Issues, issue => issue.Code == "spiritual_source_continuation_ticket_foreign");
        Assert.Equal(secondBefore, second.BuildInputBinding().ToJsonString());
        Assert.Equal(0, second.ContinuationRevision);
        var committed = first.CommitPreparedContinuation(firstLease, ticket);
        AssertNoConflictFrameErrors(committed.Issues);
        Assert.Same(first, committed.Session);
        Assert.Equal(1, first.ContinuationRevision);
    }

    [Fact]
    public async Task SourceTransaction_RevokedOwnerCannotCommitPreparedTicket()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var acquired = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(acquired.Issues);
        var owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(acquired.Session);
        var prepared = await owner.PrepareContinuationAsync(lease);
        AssertNoConflictFrameErrors(prepared.Issues);
        var ticket = Assert.IsType<ValidationService.SpiritualWoundSourceSession.PreparedContinuation>(prepared.Ticket);
        var replacement = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(replacement.Issues);
        Assert.NotSame(owner, replacement.Session);
        var rejected = owner.CommitPreparedContinuation(lease, ticket);
        Assert.Null(rejected.Session);
        Assert.Contains(rejected.Issues, issue => issue.Code == "spiritual_source_session_revoked");
        Assert.Equal(0, owner.ContinuationRevision);
    }

    [Fact]
    public async Task SourceTransaction_DisposedOrLaterLeaseCannotCommitPreparedTicket()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var acquired = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(acquired.Issues);
        var owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(acquired.Session);
        var prepared = await owner.PrepareContinuationAsync(lease);
        AssertNoConflictFrameErrors(prepared.Issues);
        var ticket = Assert.IsType<ValidationService.SpiritualWoundSourceSession.PreparedContinuation>(prepared.Ticket);
        var retained = owner.BuildInputBinding().ToJsonString();
        await lease.DisposeAsync();
        Assert.Throws<InvalidOperationException>(() => owner.CommitPreparedContinuation(lease, ticket));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.PrepareContinuationAsync(lease));
        Assert.Equal(retained, owner.BuildInputBinding().ToJsonString());
        Assert.Equal(0, owner.ContinuationRevision);
        await using var laterLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var rejected = owner.CommitPreparedContinuation(laterLease, ticket);
        Assert.Null(rejected.Session);
        Assert.Contains(rejected.Issues, issue => issue.Code == "spiritual_source_continuation_ticket_lease");
        Assert.Equal(retained, owner.BuildInputBinding().ToJsonString());
        var fresh = await owner.PrepareContinuationAsync(laterLease);
        AssertNoConflictFrameErrors(fresh.Issues);
        var freshTicket = Assert.IsType<ValidationService.SpiritualWoundSourceSession.PreparedContinuation>(fresh.Ticket);
        Assert.Same(owner, owner.CommitPreparedContinuation(laterLease, freshTicket).Session);
    }

    [Fact]
    public async Task SourceTransaction_OrdinaryContinueUsesSameCommitAndInvalidatesPriorTicket()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var acquired = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(acquired.Issues);
        var owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(acquired.Session);
        var first = Assert.Single(owner.Sources);
        var prepared = await owner.PrepareContinuationAsync(lease);
        AssertNoConflictFrameErrors(prepared.Issues);
        var ticket = Assert.IsType<ValidationService.SpiritualWoundSourceSession.PreparedContinuation>(prepared.Ticket);
        var unchanged = owner.BuildInputBinding().ToJsonString();
        var result = await owner.ContinueAsync(lease);
        AssertNoConflictFrameErrors(result.Issues);
        Assert.Same(owner, result.Session);
        Assert.Equal(unchanged, owner.BuildInputBinding().ToJsonString());
        Assert.Same(first, Assert.Single(owner.Sources));
        Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
        Assert.Equal(1, owner.ContinuationRevision);
        var stale = owner.CommitPreparedContinuation(lease, ticket);
        Assert.Null(stale.Session);
        Assert.Contains(stale.Issues, issue => issue.Code == "spiritual_source_continuation_ticket_stale");
        var again = await owner.ContinueAsync(lease);
        AssertNoConflictFrameErrors(again.Issues);
        Assert.Same(owner, again.Session);
        Assert.Same(first, Assert.Single(owner.Sources));
        Assert.Equal(2, owner.ContinuationRevision);
    }

    private static (ResourceOwnerAuthority Owners, ResourceStateLedger State)
        ReadSourceTransactionResourceBaseline(ValidationService.SpiritualWoundSourceSession owner)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(owner.ReadOriginal(ResourceMaterializationContract.DefinitionsPath),
            allowMissingPristine: false).Catalog!;
        var state = ResourceStateContract.ParseCanonical(owner.ReadOriginal(ResourceMaterializationContract.StatePath),
            definitions, allowMissingPristine: false).Ledger!;
        var roots = new AfterlifeResourceOwnerRoots(
            JsonNode.Parse(owner.ReadOriginal(AfterlifeEntityProfileState.StatePath)!)!.AsObject(),
            JsonNode.Parse(owner.ReadOriginal(AfterlifeSpiritualConflictState.StatePath)!)!.AsObject(),
            JsonNode.Parse(owner.ReadOriginal("game_state/meta/soul_state.json")!)!.AsObject());
        var composed = AfterlifeResourceOwnerComposer.Compose(new AfterlifeResourceOwnerCompositionInput(definitions, roots, roots));
        Assert.True(composed.IsValid, string.Join(Environment.NewLine, composed.Issues));
        return (composed.Authority!, state);
    }
}
