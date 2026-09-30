using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Records only closed rows when legacy capture validates the full suffix up front.
    /// </summary>
    [Fact]
    public async Task ClosedExchangeEvidence_LegacyCaptureAllowsPrevalidatedSuffix()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));

        AssertNoConflictFrameErrors((await capture.AdvanceNextResourceExchangeAsync(lease)).Issues);
        var first = Assert.Single(capture.ReadClosedExchangeEvidence(lease));
        Assert.Equal("exchange_conflict_frame_42", first.ExchangeId);
        Assert.Equal(new[] { 0, 1 }, first.DiceClaims.Select(claim => claim.SourceIndex));

        AssertNoConflictFrameErrors((await capture.AdvanceNextResourceExchangeAsync(lease)).Issues);
        var entries = capture.ReadClosedExchangeEvidence(lease);
        Assert.Equal(2, entries.Count);
        Assert.Equal("exchange_source_second", entries[1].ExchangeId);
        Assert.Equal(new[] { 2, 3 }, entries[1].DiceClaims.Select(claim => claim.SourceIndex));
    }

    /// <summary>
    /// Preserves signed dice from a closed exchange that creates no harmful source.
    /// </summary>
    [Fact]
    public async Task ClosedExchangeEvidence_RetainsSourceFreeDice()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var conflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var response = conflict[AfterlifeSpiritualConflictState.ResponseField]!;
        response["exchange"]!["outcome"] = "no_effect";
        response["exchange"]!["after"]!["oppositionSideStrain"] = "clear";
        response.AsObject().Remove("activeConflictAfter");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, conflict.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        AssertNoConflictFrameErrors((await capture.AdvanceNextResourceExchangeAsync(lease)).Issues);

        var entry = Assert.Single(capture.ReadClosedExchangeEvidence(lease));
        Assert.Empty(entry.Sources);
        Assert.Equal(new[] { 0, 1 }, entry.DiceClaims.Select(claim => claim.SourceIndex));
    }

    /// <summary>
    /// Preserves a source-free die prefix when a later exchange first creates an eligible source.
    /// </summary>
    [Fact]
    public async Task ClosedExchangeEvidence_SourceFreePrefixPrecedesEligibleSource()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var conflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var response = conflict[AfterlifeSpiritualConflictState.ResponseField]!;
        response["exchange"]!["outcome"] = "no_effect";
        response["exchange"]!["after"]!["oppositionSideStrain"] = "clear";
        response.AsObject().Remove("activeConflictAfter");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, conflict.ToJsonString());
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var continued = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        continued["activeConflict"]!["exchangeLog"]![1]!["outcome"] = "success";
        continued["activeConflict"]!["exchangeLog"]![1]!["after"]!["oppositionSideStrain"] = "strained";
        continued["activeConflict"]!["oppositionSideStrain"] = "strained";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, continued.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        AssertNoConflictFrameErrors((await capture.AdvanceNextResourceExchangeAsync(lease)).Issues);
        Assert.Empty(Assert.Single(capture.ReadClosedExchangeEvidence(lease)).Sources);
        AssertNoConflictFrameErrors((await capture.AdvanceNextResourceExchangeAsync(lease)).Issues);

        var entries = capture.ReadClosedExchangeEvidence(lease);
        Assert.Equal(2, entries.Count);
        Assert.Empty(entries[0].Sources);
        Assert.Equal(new[] { 0, 1 }, entries[0].DiceClaims.Select(claim => claim.SourceIndex));
        Assert.Single(entries[1].Sources);
        Assert.Equal(new[] { 2, 3 }, entries[1].DiceClaims.Select(claim => claim.SourceIndex));
    }

    /// <summary>
    /// Keeps die and source evidence even when training caps a harmful exchange at zero severity.
    /// </summary>
    [Fact]
    public async Task ClosedExchangeEvidence_RetainsZeroCeilingTrainingSource()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        var conflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        conflict["activeConflict"]!["dangerMode"] = "training";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, conflict.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        AssertNoConflictFrameErrors((await capture.AdvanceNextResourceExchangeAsync(lease)).Issues);

        var entry = Assert.Single(capture.ReadClosedExchangeEvidence(lease));
        Assert.Equal(0, Assert.Single(entry.Sources).Calculation.MaximumSeverityRank);
        Assert.Equal(new[] { 0, 1 }, entry.DiceClaims.Select(claim => claim.SourceIndex));
    }

    /// <summary>
    /// Retains the first closed row across a later source continuation and rejects a reused die.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosedExchangeEvidence_PreservesPrefixAcrossContinuation(bool duplicateDice)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        AssertNoConflictFrameErrors((await capture.AdvanceNextResourceExchangeAsync(lease)).Issues);
        var first = Assert.Single(capture.ReadClosedExchangeEvidence(lease));
        Assert.Equal(new[] { 0, 1 }, first.DiceClaims.Select(claim => claim.SourceIndex));

        var second = await capture.AdvanceNextResourceExchangeAsync(lease);
        if (duplicateDice)
        {
            Assert.Null(second.Step);
            Assert.NotEmpty(second.Issues);
            Assert.Equal(first.ExchangeId, Assert.Single(capture.ReadClosedExchangeEvidence(lease)).ExchangeId);
        }
        else
        {
            AssertNoConflictFrameErrors(second.Issues);
            var evidence = capture.ReadClosedExchangeEvidence(lease);
            Assert.Equal(2, evidence.Count);
            Assert.Equal(first.ExchangeId, evidence[0].ExchangeId);
            Assert.Equal(1, evidence[1].Ordinal);
            Assert.Single(evidence[1].Sources);
            Assert.Equal(new[] { 2, 3 }, evidence[1].DiceClaims.Select(claim => claim.SourceIndex));
            Assert.NotSame(first.Sources[0], evidence[0].Sources[0]);
        }
    }

    /// <summary>
    /// Retains detached source, die and effect evidence for the exact closed original exchange.
    /// </summary>
    [Fact]
    public async Task ClosedExchangeEvidence_RecordsOneOwnedExchangeAndRejectsDisposedCapture()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        Assert.Empty(capture.ReadClosedExchangeEvidence(lease));
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(advanced.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);

        var entry = Assert.Single(capture.ReadClosedExchangeEvidence(lease));
        Assert.Equal(0, entry.Ordinal);
        Assert.Equal(interval.ConflictId, entry.ConflictId);
        Assert.Equal(interval.ExchangeId, entry.ExchangeId);
        Assert.Single(entry.Sources);
        Assert.Equal(new[] { 0, 1 }, entry.DiceClaims.Select(claim => claim.SourceIndex));
        Assert.Equal(new[] { 15, 5 }, entry.DiceClaims.Select(claim => claim.Value));
        Assert.Equal(interval.EffectBefore.Fingerprint, entry.EffectBeforeFingerprint);
        Assert.Equal(interval.EffectAfter.Fingerprint, entry.EffectAfterFingerprint);
        Assert.Throws<NotSupportedException>(() =>
        {
            ((IList<ValidationService.PreparedSpiritualSource>)entry.Sources)[0] = entry.Sources[0];
        });
        Assert.Throws<NotSupportedException>(() =>
        {
            ((IList<ValidationService.SpiritualClosedD20Claim>)entry.DiceClaims)[0] = entry.DiceClaims[0];
        });
        capture.Dispose();
        Assert.Throws<ObjectDisposedException>(() => capture.ReadClosedExchangeEvidence(lease));
    }
}
