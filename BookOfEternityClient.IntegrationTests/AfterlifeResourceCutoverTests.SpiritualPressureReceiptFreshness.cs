using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>A warm detached comparison cannot outlive changed or deleted physical receipt bytes.</summary>
    /// <param name="delete">Deletes the receipt instead of appending valid JSON whitespace.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SpiritualPressureGroup_WarmReceiptDriftRevokesComparison(bool delete) =>
        AssertWarmPressureReceiptDriftAsync(delete, advance: false);

    /// <summary>Warm comparison DATA cannot authorize an actual interval after physical receipt drift.</summary>
    /// <param name="delete">Deletes the receipt instead of appending valid JSON whitespace.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SpiritualPressureGroup_WarmReceiptDriftRejectsActual(bool delete) =>
        AssertWarmPressureReceiptDriftAsync(delete, advance: true);

    /// <summary>Exercises real signed owners and restores deliberate drift before the baseline fixture can mask the causal oracle.</summary>
    /// <param name="delete">Whether the independently authored mutation is deletion.</param>
    /// <param name="advance">Whether the caller requests actual admission rather than detached comparison.</param>
    /// <returns>Exact live freshness refusal and permanent revocation, without canonical publication.</returns>
    private Task AssertWarmPressureReceiptDriftAsync(bool delete, bool advance) =>
        WithPressureGroupCaseAsync(new("receipt_" + (advance ? "actual_" : "comparison_") +
            (delete ? "delete" : "bytes"), AdvanceExpected: advance), async window =>
        {
            var capture = window.Capture;
            var files = window.Copy.Context.FileSystem;
            AssertPressureGroupComparison(window, await capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease));
            var coldCounts = capture.ReadExistingStrainReuseCounts();
            var coldSourceCounts = capture.ReadExistingPressureGroupComputationCounts();
            AssertPressureGroupComparison(window, await capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease));
            var warmCounts = capture.ReadExistingStrainReuseCounts();
            Assert.Equal(coldCounts.OrdinaryEvaluations, warmCounts.OrdinaryEvaluations);
            Assert.Equal(coldCounts.ActualEvaluations, warmCounts.ActualEvaluations);
            Assert.Equal(coldCounts.SignedParentReads, warmCounts.SignedParentReads);
            Assert.Equal(coldSourceCounts, capture.ReadExistingPressureGroupComputationCounts());
            Assert.True(warmCounts.FreshInputChecks > coldCounts.FreshInputChecks);
            Assert.True(warmCounts.FreshDependencyChecks > coldCounts.FreshDependencyChecks);
            await AssertPressureGroupWindowInertAsync(window);
            var path = SpiritualWoundOpportunityReceiptState.StatePath;
            var original = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(window.Lease, path));
            Assert.NotEmpty(original);
            var changed = original.Concat(new byte[] { 13, 10, 32 }).ToArray();
            Assert.False(original.AsSpan().SequenceEqual(changed));
            ValidationService.SpiritualOrdinaryPressureComparisonResult? comparison = null;
            AcceptedMechanicsPlanner.ResourceContinuationResult? actual = null;
            Exception? operationError = null;
            bool currentAfterOperation = true;
            var afterCounts = warmCounts;
            var afterSourceCounts = coldSourceCounts;
            try
            {
                if (delete) files.DeleteFile(window.Lease, path);
                else await files.WriteFileAtomicBytesAsync(window.Lease, path, changed);
                Assert.Equal(delete ? null : changed, await files.ReadFileBytesAsync(window.Lease, path));
                var driftedFiles = CaptureExactSpiritualFiles(files.GameSessionPath);
                operationError = await Record.ExceptionAsync(async () =>
                {
                    if (advance) actual = await capture.AdvanceNextResourceExchangeAsync(window.Lease);
                    else comparison = await capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease);
                });
                currentAfterOperation = capture.IsCurrentOwner;
                afterCounts = capture.ReadExistingStrainReuseCounts();
                afterSourceCounts = capture.ReadExistingPressureGroupComputationCounts();
                AssertExistingPressureInputInert(window.Copy.Context, window.Lease, driftedFiles, window.Generation);
            }
            finally
            {
                // Restore exact bytes even on the expected baseline FAIL; a masking fixture is the separate target.
                await files.WriteFileAtomicBytesAsync(window.Lease, path, original);
            }
            Assert.Equal(original, await files.ReadFileBytesAsync(window.Lease, path));
            AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new
            {
                phase = "RECON1_live_receipt_result", delete, advance, path,
                originalFingerprint = new CanonicalBeforeImage(true, original).Fingerprint,
                mutationFingerprint = new CanonicalBeforeImage(!delete, delete ? null : changed).Fingerprint,
                operationError = operationError?.ToString(), currentAfterOperation,
                comparisonPresent = comparison is not null, comparisonIssues = comparison?.Issues,
                comparisonPrefixBlocked = comparison?.PrefixBlocked,
                comparisonData = comparison?.Comparison is not { } data ? null : new
                {
                    data.OrdinaryExchangeJson, data.OrdinaryCalculation, data.ExpectedTransitions,
                    data.RawInputFingerprint, data.ExchangePointer
                },
                actualPresent = actual is not null, actualIssues = actual?.Issues,
                stepPresent = actual?.Step is not null,
                actualInterval = actual?.Step?.Interval is not { } interval ? null : new
                {
                    interval.ConflictId, interval.ExchangeId, interval.Ordinal,
                    interval.AppliedRange, interval.ReplayRange, interval.EventRange,
                    interval.AppliedTransitions, interval.ReplayTransitions, interval.Events,
                    effectBeforeFingerprint = interval.EffectBefore.Fingerprint,
                    effectAfterFingerprint = interval.EffectAfter.Fingerprint,
                    effectBeforeBoundaries = interval.EffectBefore.Boundaries.Count,
                    effectAfterBoundaries = interval.EffectAfter.Boundaries.Count,
                    effectBeforeMutations = interval.EffectBefore.ResourceMutations.Count,
                    effectAfterMutations = interval.EffectAfter.ResourceMutations.Count
                },
                pendingExchange = actual?.Step?.PendingExchange is not null,
                pendingResource = actual?.Step?.PendingResource is not null,
                checkpoint = actual?.Step?.Checkpoint is not null,
                originalPrefix = actual?.Step?.OriginalPrefix is not null,
                finishedResult = actual?.Step?.Result is not null,
                warmCounts = new { warmCounts.Queries, warmCounts.SignedParentReads, warmCounts.OrdinaryEvaluations,
                    warmCounts.ActualEvaluations, warmCounts.FreshInputChecks, warmCounts.FreshDependencyChecks },
                afterCounts = new { afterCounts.Queries, afterCounts.SignedParentReads, afterCounts.OrdinaryEvaluations,
                    afterCounts.ActualEvaluations, afterCounts.FreshInputChecks, afterCounts.FreshDependencyChecks },
                warmSourceCounts = coldSourceCounts, afterSourceCounts
            }));
            Assert.Null(operationError);
            var issues = advance ? Assert.IsType<AcceptedMechanicsPlanner.ResourceContinuationResult>(actual).Issues
                : Assert.IsType<ValidationService.SpiritualOrdinaryPressureComparisonResult>(comparison).Issues;
            Assert.True(!currentAfterOperation && issues.Count == 1 &&
                issues[0].Code == "spiritual_original_input_changed" && issues[0].FilePath == path,
                advance ? "RECON1_RECEIPT_ACTUAL_REFUSED: live receipt drift must revoke before actual admission."
                    : "RECON1_RECEIPT_COMPARISON_REFUSED: live receipt drift must revoke before returning warm DATA.");
            var issue = Assert.Single(issues);
            Assert.Equal(IssueSeverity.Error, issue.Severity);
            if (advance) Assert.Null(actual!.Step);
            else { Assert.Null(comparison!.Comparison); Assert.False(comparison.PrefixBlocked); }
            Assert.Equal(warmCounts.Queries, afterCounts.Queries);
            Assert.Equal(warmCounts.OrdinaryEvaluations, afterCounts.OrdinaryEvaluations);
            Assert.Equal(warmCounts.ActualEvaluations, afterCounts.ActualEvaluations);
            Assert.Equal(warmCounts.SignedParentReads, afterCounts.SignedParentReads);
            Assert.Equal(coldSourceCounts, afterSourceCounts);
            Assert.False(capture.IsCurrentOwner);
            await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => capture.CheckRetainedInputsAsync(window.Lease));
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(files, window.Lease));
            AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
            AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        });
}
