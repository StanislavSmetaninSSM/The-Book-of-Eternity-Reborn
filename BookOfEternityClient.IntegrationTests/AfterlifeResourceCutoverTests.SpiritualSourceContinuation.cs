using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Fact]
    public async Task SourceContinuation_UnchangedRetainsOwnerSourceAndDetachedImages()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(prepared.Issues);
        var owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
        var source = Assert.Single(owner.Sources);
        var before = owner.BuildInputBinding().ToJsonString();
        var fileBefore = await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath);
        var detached = owner.BuildInputBinding();
        detached["candidates"]![AfterlifeSpiritualConflictState.StatePath] = "{}";
        var detachedOriginal = JsonNode.Parse(owner.ReadOriginal(AfterlifeSpiritualConflictState.StatePath)!)!.AsObject();
        detachedOriginal["activeConflict"] = null;

        var result = await owner.ContinueAsync(lease);
        var repeated = await owner.ContinueAsync(lease);

        AssertNoConflictFrameErrors(result.Issues);
        AssertNoConflictFrameErrors(repeated.Issues);
        Assert.Same(owner, result.Session);
        Assert.Same(owner, repeated.Session);
        Assert.Same(source, Assert.Single(owner.Sources));
        Assert.True(owner.Owns(source));
        Assert.False(owner.Owns(source with { }));
        Assert.Equal(before, owner.BuildInputBinding().ToJsonString());
        Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
        Assert.Single(owner.CheckedExchanges);
        Assert.Empty(owner.PendingRequirements);
        Assert.Equal(fileBefore, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Fact]
    public async Task SourceContinuation_AppendedExchangeKeepsPriorSourceReferenceAndOriginalDice()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        ValidationService.SpiritualWoundSourceSession owner;
        ValidationService.PreparedSpiritualSource first;
        string original;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(prepared.Issues);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
            first = Assert.Single(owner.Sources);
            original = owner.ReadOriginal(AfterlifeSpiritualConflictState.StatePath)!;
        }
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        await using var continuationLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var result = await owner.ContinueAsync(continuationLease);

        AssertNoConflictFrameErrors(result.Issues);
        Assert.Same(owner, result.Session);
        Assert.Equal(2, owner.Sources.Count);
        Assert.Same(first, owner.Sources[0]);
        Assert.All(owner.Sources, source => Assert.True(owner.Owns(source)));
        Assert.Equal(new[] { "exchange_conflict_frame_42", "exchange_source_second" },
            owner.Sources.Select(source => source.ExchangeId).ToArray());
        Assert.Equal(new[] { 0, 1, 2, 3 }, owner.ClaimedDice);
        Assert.Equal(2, owner.CheckedExchanges.Count);
        Assert.Equal(original, owner.ReadOriginal(AfterlifeSpiritualConflictState.StatePath));
        var accepted = owner.BuildInputBinding().ToJsonString();
        var repeated = await owner.ContinueAsync(continuationLease);
        AssertNoConflictFrameErrors(repeated.Issues);
        Assert.Same(owner, repeated.Session);
        Assert.Equal(accepted, owner.BuildInputBinding().ToJsonString());
        Assert.Same(first, owner.Sources[0]);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, continuationLease));
    }

    [Theory]
    [InlineData("duplicate_dice")]
    [InlineData("known_side")]
    [InlineData("late_final_strain")]
    public async Task SourceContinuation_InvalidSuffixLeavesAllRetainedStateUnchanged(string mutation)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var initialCandidate = await context.FileSystem.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        ValidationService.SpiritualWoundSourceSession owner;
        ValidationService.PreparedSpiritualSource first;
        string binding;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(prepared.Issues);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
            first = Assert.Single(owner.Sources);
            binding = owner.BuildInputBinding().ToJsonString();
        }
        await WriteSourceContinuationAppendAsync(context, mutation == "duplicate_dice");
        if (mutation != "duplicate_dice")
        {
            var changed = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
            if (mutation == "known_side")
                changed["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["player"]!["before"] = 5;
            else
                changed["activeConflict"]!["oppositionSideStrain"] = "broken";
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, changed.ToJsonString());
        }
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var rejected = await owner.ContinueAsync(lease);
            Assert.Null(rejected.Session);
            Assert.Contains(rejected.Issues, issue => issue.Severity == IssueSeverity.Error);
            Assert.Equal(binding, owner.BuildInputBinding().ToJsonString());
            Assert.Same(first, Assert.Single(owner.Sources));
            Assert.True(owner.Owns(first));
            Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
            Assert.Single(owner.CheckedExchanges);
            Assert.Empty(owner.PendingRequirements);
        }
        // A valid repair can continue this very owner after a failed prospective delta.
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, initialCandidate!);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        await using var repairedLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var repaired = await owner.ContinueAsync(repairedLease);
        AssertNoConflictFrameErrors(repaired.Issues);
        Assert.Same(owner, repaired.Session);
        Assert.Same(first, owner.Sources[0]);
        Assert.Equal(new[] { 0, 1, 2, 3 }, owner.ClaimedDice);
    }

    [Theory]
    [InlineData("request_id")]
    [InlineData("request_dice")]
    [InlineData("signed_bytes")]
    public async Task SourceContinuation_ChangedOriginRevokesOwnerAndCannotBeRestoredByEqualBytes(string mutation)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        ValidationService.SpiritualWoundSourceSession owner;
        ValidationService.PreparedSpiritualSource source;
        string binding;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(prepared.Issues);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
            source = Assert.Single(owner.Sources);
            binding = owner.BuildInputBinding().ToJsonString();
        }
        var path = mutation == "signed_bytes"
            ? "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json"
            : "input/turn_request.json";
        var original = await context.FileSystem.ReadFileAsync(path);
        var changed = JsonNode.Parse(original!)!.AsObject();
        if (mutation == "request_id")
            changed["requestId"] = "another_source_request";
        else if (mutation == "request_dice")
            changed["preGeneratedDices1d20"]![0] = 14;
        else
            changed["currentRealm"] = "Mortal World";
        await context.WriteExactJsonAsync(path, changed.ToJsonString());
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var rejected = await owner.ContinueAsync(lease);
            Assert.Null(rejected.Session);
            Assert.Contains(rejected.Issues, issue => issue.Severity == IssueSeverity.Error);
            Assert.False(owner.Owns(source));
            Assert.Equal(binding, owner.BuildInputBinding().ToJsonString());
        }
        await context.WriteExactJsonAsync(path, original!);
        await using var restoredLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var restored = await owner.ContinueAsync(restoredLease);
        Assert.Null(restored.Session);
        Assert.Contains(restored.Issues, issue => issue.Code == "spiritual_source_session_revoked");
        var reacquired = await context.Validator.BeginSpiritualWoundSourceSessionAsync(restoredLease);
        AssertNoConflictFrameErrors(reacquired.Issues);
        var replacement = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(reacquired.Session);
        Assert.False(replacement.Owns(source));
        Assert.True(replacement.Owns(Assert.Single(replacement.Sources)));
        Assert.False(owner.Owns(source));
    }

    [Fact]
    public async Task SourceContinuation_TerminalProjectionReusesExactCurrentExchangeWithoutReclaimingDice()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        ValidationService.SpiritualWoundSourceSession owner;
        ValidationService.PreparedSpiritualSource source;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(prepared.Issues);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
            source = Assert.Single(owner.Sources);
        }
        await WriteSourceContinuationTerminalAsync(context, includeWitness: true);
        await using var terminalLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var result = await owner.ContinueAsync(terminalLease);
        var repeated = await owner.ContinueAsync(terminalLease);

        AssertNoConflictFrameErrors(result.Issues);
        AssertNoConflictFrameErrors(repeated.Issues);
        Assert.Same(owner, result.Session);
        Assert.Same(source, Assert.Single(owner.Sources));
        Assert.Single(owner.CheckedExchanges);
        Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
        Assert.Equal(ValidationService.SpiritualSourceRequirement.TerminalClosure,
            Assert.Single(owner.PendingRequirements).Kind);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, terminalLease));
    }

    [Fact]
    public async Task SourceContinuation_MissingTerminalAuditAddsOnlyItsWitnessAndKeepsClosurePending()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var exchange = await WriteSourceContinuationTerminalAsync(context, includeWitness: false);
        ValidationService.SpiritualWoundSourceSession owner;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(prepared.Issues);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
            Assert.Empty(owner.Sources);
            Assert.Equal(ValidationService.SpiritualSourceRequirement.TerminalExchangeAudit,
                Assert.Single(owner.PendingRequirements).Kind);
        }
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        root["recentConflicts"]![0]!["terminalExchange"] = exchange.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await using var witnessLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var result = await owner.ContinueAsync(witnessLease);

        AssertNoConflictFrameErrors(result.Issues);
        Assert.Same(owner, result.Session);
        var source = Assert.Single(owner.Sources);
        Assert.True(owner.Owns(source));
        Assert.Single(owner.CheckedExchanges);
        Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
        Assert.Equal(ValidationService.SpiritualSourceRequirement.TerminalClosure,
            Assert.Single(owner.PendingRequirements).Kind);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, witnessLease));
    }

    [Fact]
    public async Task SourceContinuation_ClosureFieldsCannotResolvePendingWithoutProducer()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationTerminalAsync(context, includeWitness: true);
        ValidationService.SpiritualWoundSourceSession owner;
        string binding;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(prepared.Issues);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
            binding = owner.BuildInputBinding().ToJsonString();
        }
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        root["recentConflicts"]![0]!["playerOutcome"] = "won";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await using var closureLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var result = await owner.ContinueAsync(closureLease);

        Assert.Null(result.Session);
        Assert.Contains(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Equal(binding, owner.BuildInputBinding().ToJsonString());
        Assert.Equal(ValidationService.SpiritualSourceRequirement.TerminalClosure,
            Assert.Single(owner.PendingRequirements).Kind);
        Assert.True(owner.Owns(Assert.Single(owner.Sources)));
    }

    [Fact]
    public async Task SourceContinuation_ModeChangeRemainsPendingWithoutHigherCapSource()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        ValidationService.SpiritualWoundSourceSession owner;
        ValidationService.PreparedSpiritualSource source;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(prepared.Issues);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
            source = Assert.Single(owner.Sources);
        }
        var root = await ReadProjectedSourceContinuationCandidateAsync(context);
        root["activeConflict"]!["dangerMode"] = "annihilation";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await using var modeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var result = await owner.ContinueAsync(modeLease);

        AssertNoConflictFrameErrors(result.Issues);
        Assert.Same(owner, result.Session);
        Assert.Same(source, Assert.Single(owner.Sources));
        Assert.Equal("hostile", source.Calculation.Input.DangerMode);
        Assert.Equal(ValidationService.SpiritualSourceRequirement.PriorEscalationDeclaration,
            Assert.Single(owner.PendingRequirements).Kind);
        Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, modeLease));
    }

    [Fact]
    public async Task SourceContinuation_DisposedLeaseCannotContinue()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(prepared.Issues);
        var owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
        await lease.DisposeAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => owner.ContinueAsync(lease));
        Assert.True(owner.Owns(Assert.Single(owner.Sources)));
    }

    [Fact]
    public async Task SourceContinuation_NewSignedRequestBeginRevokesPriorOwner()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        ValidationService.SpiritualWoundSourceSession old;
        ValidationService.PreparedSpiritualSource source;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(prepared.Issues);
            old = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
            source = Assert.Single(old.Sources);
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath,
            old.ReadOriginal(AfterlifeSpiritualConflictState.StatePath)!);
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 43, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        var request = Assert.IsType<JsonObject>(await context.ReadJsonAsync("input/turn_request.json"));
        request["requestId"] = "request_source_continuation_43";
        await context.WriteExactJsonAsync("input/turn_request.json", request.ToJsonString());
        const string manifestPath = "game_state/control/pending_turn_snapshot.json";
        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(manifestPath));
        manifest["requestId"] = "request_source_continuation_43";
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await context.WriteExactJsonAsync(manifestPath, manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(context.FileSystem);
        await using var newLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var begun = await context.Validator.BeginSpiritualWoundSourceSessionAsync(newLease);
        var stale = await old.ContinueAsync(newLease);

        AssertNoConflictFrameErrors(begun.Issues);
        var current = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(begun.Session);
        Assert.Equal("request_source_continuation_43", current.RequestId);
        Assert.Equal(43, current.TurnNumber);
        Assert.False(old.Owns(source));
        Assert.False(current.Owns(source));
        Assert.Null(stale.Session);
        Assert.Contains(stale.Issues, issue => issue.Code == "spiritual_source_session_revoked");
    }

    [Fact]
    public async Task SourceContinuation_NewTerminalExchangeUsesRetainedActivePrefix()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        ValidationService.SpiritualWoundSourceSession owner;
        ValidationService.PreparedSpiritualSource first;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(prepared.Issues);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
            first = Assert.Single(owner.Sources);
        }
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        await WriteSourceContinuationTerminalAsync(context, includeWitness: true);
        await using var terminalLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var result = await owner.ContinueAsync(terminalLease);

        AssertNoConflictFrameErrors(result.Issues);
        Assert.Same(owner, result.Session);
        Assert.Equal(2, owner.Sources.Count);
        Assert.Same(first, owner.Sources[0]);
        Assert.Equal("exchange_source_second", owner.Sources[1].ExchangeId);
        Assert.Equal(2, owner.CheckedExchanges.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, owner.ClaimedDice);
        Assert.Equal(ValidationService.SpiritualSourceRequirement.TerminalClosure,
            Assert.Single(owner.PendingRequirements).Kind);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, terminalLease));
    }

    private static async Task<JsonObject> ReadProjectedSourceContinuationCandidateAsync(
        ResourceMaterializationTestContext context)
    {
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var projected = root[AfterlifeSpiritualConflictState.ResponseField] is JsonObject update
            ? AfterlifeSpiritualConflictState.ApplyUpdate(root, update)
            : root.DeepClone().AsObject();
        projected.Remove(AfterlifeSpiritualConflictState.ResponseField);
        Assert.False(projected.ContainsKey("lastInvalidUpdate"));
        return projected;
    }

    private static async Task WriteSourceContinuationAppendAsync(
        ResourceMaterializationTestContext context, bool duplicateDice)
    {
        var root = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = root["activeConflict"]!.AsObject();
        var first = active["exchangeLog"]![0]!.AsObject();
        var second = first.DeepClone().AsObject();
        second["exchangeId"] = "exchange_source_second";
        second["before"] = first["after"]!.DeepClone();
        second["after"] = second["before"]!.DeepClone();
        second["after"]!["oppositionSideStrain"] = "fractured";
        second["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = duplicateDice ? 0 : 2;
        second["diceAudit"]!["diceUsed"]![0]!["value"] = duplicateDice ? 15 : 12;
        second["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = duplicateDice ? 1 : 3;
        second["diceAudit"]!["diceUsed"]![1]!["value"] = duplicateDice ? 5 : 8;
        second["diceAudit"]!["playerTotal"] = duplicateDice ? 15 : 12;
        second["diceAudit"]!["oppositionTotal"] = duplicateDice ? 5 : 8;
        second["diceAudit"]!["margin"] = duplicateDice ? 10 : 4;
        second["diceAudit"]!["outcomeBand"] = duplicateDice ? "decisive_player_success" : "player_success";
        foreach (var side in new[] { "player", "opposition" })
        {
            second["actionCostAudit"]![side]!["before"] = 3;
            second["actionCostAudit"]![side]!["after"] = 0;
        }
        active["exchangeLog"]!.AsArray().Add(second);
        active["oppositionSideStrain"] = "fractured";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
    }

    private static async Task<JsonObject> WriteSourceContinuationTerminalAsync(
        ResourceMaterializationTestContext context, bool includeWitness)
    {
        var root = await ReadProjectedSourceContinuationCandidateAsync(context);
        var exchange = root["activeConflict"]!["exchangeLog"]!.AsArray().Last()!.DeepClone().AsObject();
        root["activeConflict"] = null;
        var resolution = new JsonObject
        {
            ["conflictId"] = "conflict_resource_cost", ["realm"] = "Chaos Sea",
            ["dangerMode"] = "hostile", ["operationType"] = "pressure",
            ["resolutionState"] = "resolved", ["resolvedAtTurn"] = 42,
            ["diceAudit"] = exchange["diceAudit"]!.DeepClone()
        };
        if (includeWitness)
            resolution["terminalExchange"] = exchange.DeepClone();
        root["recentConflicts"]!.AsArray().Add(resolution);
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        return exchange;
    }
}
