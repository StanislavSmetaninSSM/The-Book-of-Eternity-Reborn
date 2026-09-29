using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Fact]
    public async Task SourceMissingAudit_DefaultBeginRemainsStrict()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var root = await ReadSourceMissingCandidateAsync(context);
        root["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!.AsObject().Remove("opposition");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var rejected = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        Assert.Null(rejected.Session);
        Assert.Contains(rejected.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        var live = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(live.Issues);
        var owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(live.Session);
        Assert.True(owner.IsCurrentOwner);
        Assert.Empty(owner.Sources);
        Assert.Single(owner.PendingRequirements);
    }

    [Fact]
    public async Task SourceMissingAudit_DefaultOwnerContinueRemainsStrict()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        ValidationService.SpiritualWoundSourceSession owner;
        ValidationService.PreparedSpiritualSource first;
        string retained;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(prepared.Issues);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
            first = Assert.Single(owner.Sources);
            retained = owner.BuildInputBinding().ToJsonString();
        }
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var partial = await ReadSourceMissingCandidateAsync(context);
        partial["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!.AsObject().Remove("opposition");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, partial.ToJsonString());
        await using var continuation = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var rejected = await owner.ContinueAsync(continuation);
        Assert.Null(rejected.Session);
        Assert.Contains(rejected.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Equal(retained, owner.BuildInputBinding().ToJsonString());
        Assert.Same(first, Assert.Single(owner.Sources));
        Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
        Assert.Empty(owner.PendingRequirements);
    }

    [Theory]
    [InlineData("player")]
    [InlineData("opposition")]
    [InlineData("both")]
    [InlineData("container")]
    public async Task SourceMissingAudit_FullCompletionClaimsSameOwnerOnce(string missing)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var full = await ReadSourceMissingCandidateAsync(context);
        var partial = full.DeepClone().AsObject();
        var exchange = partial["activeConflict"]!["exchangeLog"]![0]!.AsObject();
        if (missing == "container") exchange.Remove("actionCostAudit");
        else foreach (var side in missing == "both" ? new[] { "player", "opposition" } : new[] { missing })
            exchange["actionCostAudit"]!.AsObject().Remove(side);
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, partial.ToJsonString());
        ValidationService.SpiritualWoundSourceSession owner;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var prepared = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(prepared.Issues);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
            Assert.True(owner.IsCurrentOwner);
            Assert.Empty(owner.Sources);
            Assert.Empty(owner.CheckedExchanges);
            Assert.Empty(owner.ClaimedDice);
            var pending = Assert.Single(owner.PendingRequirements);
            Assert.Equal(ValidationService.SpiritualSourceRequirement.MissingActionCostAudit, pending.Kind);
            Assert.Equal(exchange.ToJsonString(), pending.CandidateJson);
            var view = Assert.IsType<JsonObject>(owner.GetMissingActionCostAuditCandidate());
            view["exchangeId"] = "forged_detached_view";
            Assert.Equal(exchange.ToJsonString(), owner.GetMissingActionCostAuditCandidate()!.ToJsonString());
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, full.ToJsonString());
        await using var continuation = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await owner.ContinueAsync(continuation);
        AssertNoConflictFrameErrors(result.Issues);
        Assert.Same(owner, result.Session);
        var source = Assert.Single(owner.Sources);
        Assert.True(owner.Owns(source));
        Assert.Empty(owner.PendingRequirements);
        Assert.Null(owner.GetMissingActionCostAuditCandidate());
        Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
        Assert.Single(owner.CheckedExchanges);
        var binding = owner.BuildInputBinding().ToJsonString();
        var repeated = await owner.ContinueAsync(continuation);
        AssertNoConflictFrameErrors(repeated.Issues);
        Assert.Same(owner, repeated.Session);
        Assert.Same(source, Assert.Single(owner.Sources));
        Assert.Equal(binding, owner.BuildInputBinding().ToJsonString());
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, continuation));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceMissingAudit_CheckedPrefixRetainsReferencesAndDice(bool appendToHeldOwner)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        ValidationService.SpiritualWoundSourceSession? owner = null;
        ValidationService.PreparedSpiritualSource? first = null;
        if (appendToHeldOwner)
        {
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var prepared = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(prepared.Issues);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
            first = Assert.Single(owner.Sources);
        }
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var full = await ReadSourceMissingCandidateAsync(context);
        var partial = full.DeepClone().AsObject();
        partial["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!.AsObject().Remove("opposition");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, partial.ToJsonString());
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var prepared = owner is null
                ? await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(lease) : await owner.ContinueAsync(lease);
            AssertNoConflictFrameErrors(prepared.Issues);
            if (owner is not null) Assert.Same(owner, prepared.Session);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
            if (first is not null) Assert.Same(first, Assert.Single(owner.Sources));
            first = Assert.Single(owner.Sources);
            Assert.Single(owner.CheckedExchanges);
            Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
            Assert.Equal(ValidationService.SpiritualSourceRequirement.MissingActionCostAudit,
                Assert.Single(owner.PendingRequirements).Kind);
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, full.ToJsonString());
        await using var resumeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await owner.ContinueAsync(resumeLease);
        AssertNoConflictFrameErrors(result.Issues);
        Assert.Same(owner, result.Session);
        Assert.Same(first, owner.Sources[0]);
        Assert.Equal(2, owner.Sources.Count);
        Assert.Equal(2, owner.CheckedExchanges.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, owner.ClaimedDice);
        Assert.Empty(owner.PendingRequirements);
    }

    [Theory]
    [InlineData("known_side")]
    [InlineData("extra_suffix")]
    [InlineData("forged_zero")]
    [InlineData("known_dice")]
    [InlineData("null_side")]
    [InlineData("changed_source_target")]
    public async Task SourceMissingAudit_InvalidCompletionPreservesExactWaitAndCanRepair(string mutation)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var full = await ReadSourceMissingCandidateAsync(context);
        var partial = full.DeepClone().AsObject();
        partial["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!.AsObject().Remove("opposition");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, partial.ToJsonString());
        ValidationService.SpiritualWoundSourceSession owner;
        string retained;
        ValidationService.SpiritualSourcePendingRequirement pending;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var prepared = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(prepared.Issues);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
            retained = owner.BuildInputBinding().ToJsonString();
            pending = Assert.Single(owner.PendingRequirements);
        }
        var invalid = full.DeepClone().AsObject();
        var exchange = invalid["activeConflict"]!["exchangeLog"]![0]!.AsObject();
        switch (mutation)
        {
            case "known_side": exchange["actionCostAudit"]!["player"]!["before"] = 99; break;
            case "extra_suffix": invalid["activeConflict"]!["exchangeLog"]!.AsArray().Add(exchange.DeepClone()); break;
            case "forged_zero":
                exchange["actionCostAudit"]!["opposition"]!["effectiveCost"] = 0;
                exchange["actionCostAudit"]!["opposition"]!["after"] = 6;
                break;
            case "known_dice": exchange["diceAudit"]!["margin"] = 9; break;
            case "null_side": exchange["actionCostAudit"]!["opposition"] = null; break;
            case "changed_source_target": exchange["spiritualWoundTarget"] = new JsonObject
                { ["actorType"] = "guardian", ["actorId"] = "foreign" }; break;
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, invalid.ToJsonString());
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var rejected = await owner.ContinueAsync(lease);
            Assert.Null(rejected.Session);
            Assert.Contains(rejected.Issues, issue => issue.Severity == IssueSeverity.Error);
            Assert.True(owner.IsCurrentOwner);
            Assert.Equal(retained, owner.BuildInputBinding().ToJsonString());
            Assert.Same(pending, Assert.Single(owner.PendingRequirements));
            Assert.Empty(owner.Sources);
            Assert.Empty(owner.CheckedExchanges);
            Assert.Empty(owner.ClaimedDice);
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, full.ToJsonString());
        await using var repairedLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var repaired = await owner.ContinueAsync(repairedLease);
        AssertNoConflictFrameErrors(repaired.Issues);
        Assert.Same(owner, repaired.Session);
        Assert.Single(owner.Sources);
        Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
    }

    [Theory]
    [InlineData("null_side")]
    [InlineData("scalar_side")]
    [InlineData("empty_side")]
    [InlineData("null_container")]
    [InlineData("bad_known_cost")]
    [InlineData("bad_known_sequence")]
    [InlineData("bad_source")]
    [InlineData("bad_dice")]
    [InlineData("nonleaf")]
    public async Task SourceMissingAudit_MalformedOrOtherFailureCannotAcquirePendingOwner(string mutation)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var root = await ReadSourceMissingCandidateAsync(context);
        var exchange = root["activeConflict"]!["exchangeLog"]![0]!.AsObject();
        exchange["actionCostAudit"]!.AsObject().Remove("opposition");
        switch (mutation)
        {
            case "null_side": exchange["actionCostAudit"]!["opposition"] = null; break;
            case "scalar_side": exchange["actionCostAudit"]!["opposition"] = "missing"; break;
            case "empty_side": exchange["actionCostAudit"]!["opposition"] = new JsonObject(); break;
            case "null_container": exchange["actionCostAudit"] = null; break;
            case "bad_known_cost": exchange["actionCostAudit"]!["player"]!["effectiveCost"] = 0; break;
            case "bad_known_sequence":
                exchange["actionCostAudit"]!["player"]!["before"] = 8;
                exchange["actionCostAudit"]!["player"]!["after"] = 5;
                break;
            case "bad_source": exchange["spiritualWoundTarget"] = new JsonObject
                { ["actorType"] = "guardian", ["actorId"] = "foreign" }; break;
            case "bad_dice": exchange["diceAudit"]!["diceUsed"]![0]!["value"] = 16; break;
            case "nonleaf": root["activeConflict"]!["exchangeLog"]!.AsArray().Add(exchange.DeepClone()); break;
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(lease);
        Assert.Null(result.Session);
        Assert.Contains(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Fact]
    public async Task SourceMissingAudit_BothSidesMayFillInSeparateOwnedStepsWithoutEarlySource()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var full = await ReadSourceMissingCandidateAsync(context);
        var partial = full.DeepClone().AsObject();
        partial["activeConflict"]!["exchangeLog"]![0]!.AsObject().Remove("actionCostAudit");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, partial.ToJsonString());
        ValidationService.SpiritualWoundSourceSession owner;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var prepared = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(prepared.Issues);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
        }
        partial["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"] = new JsonObject
        { ["player"] = full["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["player"]!.DeepClone() };
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, partial.ToJsonString());
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var result = await owner.ContinueAsync(lease);
            AssertNoConflictFrameErrors(result.Issues);
            Assert.Same(owner, result.Session);
            Assert.Empty(owner.Sources);
            Assert.Empty(owner.CheckedExchanges);
            Assert.Empty(owner.ClaimedDice);
            Assert.Single(owner.PendingRequirements);
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, full.ToJsonString());
        await using var completedLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var completed = await owner.ContinueAsync(completedLease);
        AssertNoConflictFrameErrors(completed.Issues);
        Assert.Same(owner, completed.Session);
        Assert.Single(owner.Sources);
        Assert.Empty(owner.PendingRequirements);
        Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
    }

    [Theory]
    [InlineData("duplicate_dice")]
    [InlineData("final_strain")]
    public async Task SourceMissingAudit_InvalidAppendedPartialPreservesCheckedPrefix(string mutation)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        ValidationService.SpiritualWoundSourceSession owner;
        ValidationService.PreparedSpiritualSource first;
        string retained;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var prepared = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(prepared.Issues);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
            first = Assert.Single(owner.Sources);
            retained = owner.BuildInputBinding().ToJsonString();
        }
        await WriteSourceContinuationAppendAsync(context, duplicateDice: mutation == "duplicate_dice");
        var partial = await ReadSourceMissingCandidateAsync(context);
        partial["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!.AsObject().Remove("opposition");
        if (mutation == "final_strain") partial["activeConflict"]!["oppositionSideStrain"] = "strained";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, partial.ToJsonString());
        await using var continuation = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var rejected = await owner.ContinueAsync(continuation);
        Assert.Null(rejected.Session);
        Assert.Contains(rejected.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(owner.IsCurrentOwner);
        Assert.Equal(retained, owner.BuildInputBinding().ToJsonString());
        Assert.Same(first, Assert.Single(owner.Sources));
        Assert.True(owner.Owns(first));
        Assert.Single(owner.CheckedExchanges);
        Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
        Assert.Empty(owner.PendingRequirements);
    }

    [Fact]
    public async Task SourceMissingAudit_ReplacedEmptyOwnerCannotExportOrContinue()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var root = await ReadSourceMissingCandidateAsync(context);
        root["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!.AsObject().Remove("opposition");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var first = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var old = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(first.Session);
        var second = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(second.Issues);
        Assert.NotSame(old, second.Session);
        Assert.False(old.IsCurrentOwner);
        Assert.Throws<InvalidOperationException>(() => old.GetMissingActionCostAuditCandidate());
        var rejected = await old.ContinueAsync(lease);
        Assert.Null(rejected.Session);
        Assert.Contains(rejected.Issues, issue => issue.Code == "spiritual_source_session_revoked");
    }
}
