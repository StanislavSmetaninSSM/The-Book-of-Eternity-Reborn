using System.Diagnostics;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Keeps a genuine original-prefix receipt wait private until Advance returns that exact owned wait,
    /// without repeating prefix work or admitting any exchange before its real receipt resolves.
    /// This source-first common sequencing fixture does not qualify pressure comparison or publication.
    /// </summary>
    [Fact]
    public async Task SpiritualPressurePrefixPreparation_RetainsExactOwnedWaitUntilReceiptResolution()
    {
        var preparationStarted = Stopwatch.GetTimestamp();
        var context = await CreateCompleteConflictFrameContextAsync();
        try
        {
            await SeedOriginalContinuationBoundedEffectAsync(context);
            await WriteCompleteConflictFrameExchangeAsync(context);
            WritePressureOwnerTiming("prefix_wait_fixture", "prefix", preparationStarted);
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
            var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
            var recorded = await MeasurePressureOwnerPhaseAsync("prefix_wait_intake", "prefix",
                () => context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease));
            AssertNoConflictFrameErrors(recorded.Issues);
            using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));

            var firstPrefix = await MeasurePressureOwnerPhaseAsync("prefix_wait_prepare", "first",
                () => capture.PrepareOriginalResourcePrefixAsync(lease));
            AssertNoConflictFrameErrors(firstPrefix.Issues);
            Assert.False(firstPrefix.Ready);
            Assert.True(firstPrefix.PrefixBlocked);
            Assert.Empty(capture.ReadClosedExchangeEvidence(lease));
            var allocationCursor = capture.ReadAllocationCursor(lease);
            var allocationJournal = capture.ReadAllocationJournal(lease).ToJsonString();
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await capture.ReadExistingPressureOrdinaryProbeStateAsync(lease));

            var repeatedPrefix = await capture.PrepareOriginalResourcePrefixAsync(lease);
            AssertNoConflictFrameErrors(repeatedPrefix.Issues);
            Assert.False(repeatedPrefix.Ready);
            Assert.True(repeatedPrefix.PrefixBlocked);
            var comparison = await capture.ReadExistingPressureOrdinaryComparisonAsync(lease);
            AssertNoConflictFrameErrors(comparison.Issues);
            Assert.True(comparison.PrefixBlocked);
            Assert.Null(comparison.Comparison);
            Assert.Empty(capture.ReadClosedExchangeEvidence(lease));
            Assert.Equal(allocationCursor, capture.ReadAllocationCursor(lease));
            Assert.Equal(allocationJournal, capture.ReadAllocationJournal(lease).ToJsonString());

            var firstAdvance = await MeasurePressureOwnerPhaseAsync("prefix_wait_advance", "first",
                () => capture.AdvanceNextResourceExchangeAsync(lease));
            AssertNoConflictFrameErrors(firstAdvance.Issues);
            Assert.NotNull(firstAdvance.Step);
            Assert.Null(firstAdvance.Step.Interval);
            Assert.Null(firstAdvance.Step.OriginalPrefix);
            Assert.Null(firstAdvance.Step.PendingExchange);
            var wait = Assert.IsType<AcceptedMechanicsPlanner.PendingSpiritualResourceExchange>(firstAdvance.Step.PendingResource);
            var packet = capture.ReadPendingResourceRequest(lease, wait);
            var repeatedAdvance = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(repeatedAdvance.Issues);
            Assert.Same(wait, repeatedAdvance.Step?.PendingResource);
            Assert.Null(repeatedAdvance.Step!.Interval);
            Assert.Null(repeatedAdvance.Step.OriginalPrefix);
            Assert.Equal(packet.ToJsonString(), capture.ReadPendingResourceRequest(lease, wait).ToJsonString());

            var stillBlocked = await capture.PrepareOriginalResourcePrefixAsync(lease);
            AssertNoConflictFrameErrors(stillBlocked.Issues);
            Assert.False(stillBlocked.Ready);
            Assert.True(stillBlocked.PrefixBlocked);
            var stillNoComparison = await capture.ReadExistingPressureOrdinaryComparisonAsync(lease);
            AssertNoConflictFrameErrors(stillNoComparison.Issues);
            Assert.True(stillNoComparison.PrefixBlocked);
            Assert.Null(stillNoComparison.Comparison);
            var thirdAdvance = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(thirdAdvance.Issues);
            Assert.Same(wait, thirdAdvance.Step?.PendingResource);
            Assert.Empty(capture.ReadClosedExchangeEvidence(lease));
            Assert.Equal(allocationCursor, capture.ReadAllocationCursor(lease));
            Assert.Equal(allocationJournal, capture.ReadAllocationJournal(lease).ToJsonString());
            var blockedCounts = capture.ReadExistingStrainReuseCounts();
            Assert.Equal(0, blockedCounts.OrdinaryEvaluations);
            Assert.Equal(0, blockedCounts.ActualEvaluations);
            Assert.Equal(0, blockedCounts.SignedParentReads);
            Assert.True(capture.IsCurrentOwner);
            AssertExistingPressureInputInert(context, lease, files, generation);

            var request = Assert.Single(packet["requests"]!.AsArray())!;
            var receipt = new JsonArray(new JsonObject
            {
                ["requestId"] = request["requestId"]!.GetValue<string>(),
                ["resultKind"] = "resource_delta", ["amount"] = 1,
                ["reason"] = "Resolve the actual bounded original-prefix effect."
            });
            var resumed = await MeasurePressureOwnerPhaseAsync("prefix_wait_resume", "valid_receipt",
                () => capture.ResumePendingResourceAsync(lease, wait, receipt));
            AssertNoConflictFrameErrors(resumed.Issues);
            Assert.NotNull(resumed.Step?.OriginalPrefix);
            Assert.Null(resumed.Step!.Interval);
            Assert.Null(resumed.Step.PendingResource);
            Assert.Null(resumed.Step.PendingExchange);
            Assert.Empty(capture.ReadClosedExchangeEvidence(lease));
            var closedPrefix = await capture.PrepareOriginalResourcePrefixAsync(lease);
            AssertNoConflictFrameErrors(closedPrefix.Issues);
            Assert.True(closedPrefix.Ready);
            Assert.False(closedPrefix.PrefixBlocked);
            var frontier = await ReadPressureOwnerSnapshotAsync(capture, lease, "prefix_after_real_resume");
            Assert.Equal(0, frontier.NextOrdinal);
            var source = JsonNode.Parse(frontier.SourceStateJson)!;
            foreach (var key in new[] { "sources", "checkedExchanges", "claimedDice", "pendingRequirements" })
                Assert.Empty(source[key]!.AsArray());
            AssertExistingPressureInputInert(context, lease, files, generation);

            var actual = await MeasurePressureOwnerPhaseAsync("prefix_wait_actual_exchange", "after_real_resume",
                () => capture.AdvanceNextResourceExchangeAsync(lease));
            AssertNoConflictFrameErrors(actual.Issues);
            var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(actual.Step?.Interval);
            var closed = Assert.Single(capture.ReadClosedExchangeEvidence(lease));
            Assert.Equal(0, closed.Ordinal);
            Assert.Single(closed.Sources);
            Assert.Equal(new[] { 0, 1 }, closed.DiceClaims.Select(claim => claim.SourceIndex));
            Assert.Equal(new[] { 15, 5 }, closed.DiceClaims.Select(claim => claim.Value));
            Assert.Equal(interval.ExchangeId, closed.ExchangeId);
            Assert.Equal(0, capture.ReadExistingStrainReuseCounts().OrdinaryEvaluations);
            AssertExistingPressureInputInert(context, lease, files, generation);
            foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                         SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
                Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, path));
        }
        finally
        {
            var cleanupStarted = Stopwatch.GetTimestamp();
            await context.DisposeAsync();
            WritePressureOwnerTiming("prefix_wait_owned_cleanup", context.RootPath, cleanupStarted);
            Assert.False(Directory.Exists(context.RootPath));
        }
    }
}
