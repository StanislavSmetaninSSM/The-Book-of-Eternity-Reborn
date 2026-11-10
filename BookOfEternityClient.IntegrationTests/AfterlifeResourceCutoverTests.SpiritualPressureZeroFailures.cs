using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Raw drift before common C4 take preserves the edited ordinary draft and performs no transaction writes.</summary>
    [Fact]
    public async Task SpiritualPressureZero_PreTakeDriftPreservesAuthoredInput()
    {
        var armed = false;
        var writes = new List<string>();
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path => { if (armed) writes.Add(path); return Task.CompletedTask; }
        };
        await WithSingletonIvJourneyFailureAsync("zero_pre_take", () =>
            WithPressureZeroPublicationAsync("zero_pre_take", 3, async setup =>
            {
                var context = setup.Copy.Context;
                var completed = await CompletePressureZeroColdC3Async(setup);
                AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
                await AssertPressureGroupRegisteredPlanAsync(setup, completed, await PeekPlanAsync(context));
                var backups = await ReadSpiritualC4BackupsAsync(context);
                ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority? oldAuthority = null;
                IReadOnlyDictionary<string, byte[]>? edited = null;
                byte[]? generation = null;
                await ExecuteGuardianLeaseStagesAsync(context, async lease =>
                {
                    Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out oldAuthority));
                    Assert.Empty(await oldAuthority.ValidateCurrentInputsAsync(context.FileSystem, lease));
                    var before = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                    generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                    await AppendPressureRawWhitespaceAsync(context, lease);
                    edited = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                    Assert.Equal(before.Keys.OrderBy(path => path, StringComparer.Ordinal), edited.Keys.OrderBy(path => path, StringComparer.Ordinal));
                    foreach (var (path, bytes) in before.Where(row => row.Key.Replace('\\', '/') != AfterlifeSpiritualConflictState.StatePath))
                        Assert.Equal(bytes, edited[path]);
                });
                var started = Stopwatch.GetTimestamp();
                armed = true;
                try
                {
                    var error = await Assert.ThrowsAsync<InvalidDataException>(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                        context.FileSystem, context.Normalizer, context.Validator, backups));
                    Assert.Contains("spiritual_c4_publication_input_changed", error.Message);
                    Assert.Contains(AfterlifeSpiritualConflictState.StatePath, error.Message);
                }
                finally { armed = false; }
                Assert.Empty(writes);
                await ExecuteGuardianLeaseStagesAsync(context, lease =>
                {
                    AssertExistingPressureInputInert(context, lease, edited!, generation!);
                    Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
                    Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority!, out _));
                    AssertPressureNoPrivatePair(context, lease);
                    AssertExistingPressureInputInert(context, lease, edited!, generation!);
                    return Task.CompletedTask;
                });
                WritePressureOwnerTiming("zero_pre_take_drift", setup.Spec.Name, started);
            }, hooks));
    }

    /// <summary>A warmed zero reduction cannot supply source-only C4 after exact physical receipt bytes change.</summary>
    [Fact]
    public Task SpiritualPressureZero_WarmCompletionRejectsReceiptDrift() =>
        WithSingletonIvJourneyFailureAsync("zero_warm_receipt_drift", () =>
            WithPressureGroupCaseAsync(new("zero_warm_receipt_drift", Parents: 1, After: "strained", Resilience: 3,
                AdvanceExpected: true), async window =>
            {
                _ = await AssertPressureZeroActualAdmissionAsync(window);
                var completed = await window.Capture.CompleteOrdinaryReductionAsync(window.Lease);
                Assert.Empty(completed.Issues);
                Assert.True(completed.Success);
                var repeated = await window.Capture.CompleteOrdinaryReductionAsync(window.Lease);
                Assert.Empty(repeated.Issues);
                Assert.Same(completed, repeated);
                var path = SpiritualWoundOpportunityReceiptState.StatePath;
                var original = await window.Copy.Context.FileSystem.ReadFileBytesAsync(window.Lease, path);
                Assert.NotNull(original);
                await window.Copy.Context.FileSystem.WriteFileAtomicBytesAsync(window.Lease, path, [.. original, (byte)' ']);
                var edited = CaptureExactSpiritualFiles(window.Copy.Context.FileSystem.GameSessionPath);
                var refused = await window.Capture.PrepareC4PublicationAsync(window.Lease);
                Assert.Null(refused.Authority);
                var issue = Assert.Single(refused.Issues);
                Assert.Equal("spiritual_original_input_changed", issue.Code);
                Assert.Equal(path, issue.FilePath);
                Assert.Equal(IssueSeverity.Error, issue.Severity);
                Assert.False(window.Capture.IsCurrentOwner);
                AssertExistingPressureInputInert(window.Copy.Context, window.Lease, edited, window.Generation);
                AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(window.Copy.Context.FileSystem, window.Lease));
                Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(window.Copy.Context.FileSystem, window.Lease, out _));
            }));

    /// <summary>Witnesses committed actual/resource/receipt writes, restores signed46 and retries exact ordinary bytes with fresh owners once.</summary>
    [Fact]
    public async Task SpiritualPressureZero_LateCutRestoresSignedOriginAndFreshRetryPublishesOnce()
    {
        var armed = false;
        var cutEnabled = true;
        var cuts = 0;
        CurrentCommittedPublicationWitness? witness = null;
        var injected = new IOException("Singleton pressure zero C4 receipt read-back failure.");
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) => { if (armed) witness!.Observe(phase, index); },
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (armed && cutEnabled && path == SpiritualWoundOpportunityReceiptState.StatePath &&
                    witness!.Has(AfterlifeSpiritualConflictState.StatePath) && witness.Has(ResourceMaterializationContract.StatePath) &&
                    witness.Has(SpiritualWoundOpportunityReceiptState.StatePath))
                {
                    witness.RequireSingleCurrent(AfterlifeSpiritualConflictState.StatePath);
                    witness.RequireSingleCurrent(ResourceMaterializationContract.StatePath);
                    witness.RequireSingleCurrent(SpiritualWoundOpportunityReceiptState.StatePath);
                    cuts++;
                    cutEnabled = false;
                    armed = false;
                    throw injected;
                }
                return Task.CompletedTask;
            }
        };
        await WithSingletonIvJourneyFailureAsync("zero_late_cut", () =>
            WithPressureZeroPublicationAsync("zero_late_cut", 3, async setup =>
            {
                var context = setup.Copy.Context;
                ValidationService.SpiritualOriginalTurnCapture? retired = null;
                string? firstCompleted = null;
                PressureZeroRetryAttemptData? firstAttempt = null;
                var first = await CompletePressureZeroColdC3Async(setup, capture => retired = capture,
                    observeCompletedColdC3: (snapshot, completed, interval, effects) =>
                    {
                        firstCompleted = snapshot;
                        firstAttempt = ReadPressureZeroRetryAttempt(completed, snapshot, interval, effects);
                    });
                AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
                await AssertPressureGroupRegisteredPlanAsync(setup, first, await PeekPlanAsync(context));
                ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority? oldAuthority = null;
                await ExecuteGuardianLeaseStagesAsync(context, async lease =>
                {
                    Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out oldAuthority));
                    Assert.Empty(await oldAuthority.ValidateCurrentInputsAsync(context.FileSystem, lease));
                });
                var backups = await ReadSpiritualC4BackupsAsync(context);
                witness = new(context.FileSystem, AfterlifeSpiritualConflictState.StatePath,
                    ResourceMaterializationContract.StatePath, SpiritualWoundOpportunityReceiptState.StatePath);
                var failedWitness = witness;
                var started = Stopwatch.GetTimestamp();
                Exception? failure;
                armed = true;
                try
                {
                    failure = await Record.ExceptionAsync(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                        context.FileSystem, context.Normalizer, context.Validator, backups));
                }
                finally { armed = false; }
                Assert.Equal(1, cuts);
                Assert.Same(injected, failure);
                failedWitness.AssertRestored("zero-signed46-late-cut", setup.Origin.Tracked.ToDictionary(
                    row => context.FileSystem.ResolvePath(row.Key), row => row.Value, StringComparer.Ordinal), failure, _storageOutput.WriteLine);
                AssertPressureGroupSignedOrigin(context, setup.Origin);
                await ExecuteGuardianLeaseStagesAsync(context, async lease =>
                {
                    Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                    Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
                    Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority!, out _));
                    Assert.False(retired!.IsCurrentOwner);
                    await Assert.ThrowsAsync<ObjectDisposedException>(async () => await retired.ReadExistingPressureOrdinaryProbeStateAsync(lease));
                    AssertPressureNoPrivatePair(context, lease);
                });
                AssertPressureGroupSignedOrigin(context, setup.Origin);
                WritePressureOwnerTiming("zero_late_cut_signed_rollback", setup.Spec.Name, started);
                await ExecuteGuardianLeaseStagesAsync(context, async lease =>
                {
                    foreach (var (path, bytes) in setup.Authored)
                    {
                        Assert.Contains(path, PressureAuthoredDraftPaths);
                        if (bytes is null) Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, path));
                        else await context.FileSystem.WriteFileAtomicBytesAsync(lease, path, bytes);
                    }
                    foreach (var (path, bytes) in setup.Authored)
                        Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
                    Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority!, out _));
                    AssertPressureNoPrivatePair(context, lease);
                });
                AssertPressureGroupOnlyReauthoredInputs(context, setup);
                started = Stopwatch.GetTimestamp();
                string? retryCompleted = null;
                PressureZeroRetryAttemptData? retryAttempt = null;
                var retry = await CompletePressureZeroColdC3Async(setup,
                    observeCompletedColdC3: (snapshot, completed, interval, effects) =>
                    {
                        retryCompleted = snapshot;
                        retryAttempt = ReadPressureZeroRetryAttempt(completed, snapshot, interval, effects);
                    });
                Assert.Equal(first.ActualRootJson, retry.ActualRootJson);
                // Preserve both complete attempts; only verified fresh allocations and their hash closure may differ.
                _storageOutput.WriteLine(JsonSerializer.Serialize(new
                {
                    phase = "zero_late_cut_retry_comparison", setup.Spec.Name,
                    first = new { first.WitnessJson, first.ReceiptJson, first.EffectPayloadFingerprint, completed = firstCompleted },
                    retry = new { retry.WitnessJson, retry.ReceiptJson, retry.EffectPayloadFingerprint, completed = retryCompleted }
                }));
                AssertPressureZeroFreshRetryEquivalent(Assert.IsType<PressureZeroRetryAttemptData>(firstAttempt),
                    Assert.IsType<PressureZeroRetryAttemptData>(retryAttempt));
                AssertPressureZeroRetryOracleRejectsMutations(firstAttempt!, retryAttempt!);
                var validator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
                AssertNoConflictFrameErrors(await validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
                var plan = await PeekPlanAsync(context);
                await AssertPressureGroupRegisteredPlanAsync(setup, retry, plan);
                ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority? retryAuthority = null;
                await ExecuteGuardianLeaseStagesAsync(context, lease =>
                {
                    Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out retryAuthority));
                    Assert.NotSame(oldAuthority, retryAuthority);
                    Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority!, out _));
                    return Task.CompletedTask;
                });
                witness = new(context.FileSystem, AfterlifeSpiritualConflictState.StatePath,
                    ResourceMaterializationContract.StatePath, SpiritualWoundOpportunityReceiptState.StatePath);
                armed = true;
                try
                {
                    var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                        context.FileSystem, context.Normalizer, validator, await ReadSpiritualC4BackupsAsync(context));
                    AssertNoConflictFrameErrors(published.Issues);
                    Assert.Same(plan, published.MechanicsPlan);
                }
                finally { armed = false; }
                Assert.Equal(1, cuts);
                witness.RequireSingleCurrent(AfterlifeSpiritualConflictState.StatePath);
                witness.RequireSingleCurrent(ResourceMaterializationContract.StatePath);
                witness.RequireSingleCurrent(SpiritualWoundOpportunityReceiptState.StatePath);
                await AssertPressureZeroPublishedAsync(setup, retry, plan);
                await ExecuteGuardianLeaseStagesAsync(context, async lease =>
                {
                    Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority!, out _));
                    Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, retryAuthority!, out _));
                    Assert.False(retired!.IsCurrentOwner);
                    await Assert.ThrowsAsync<ObjectDisposedException>(async () => await retired.ReadExistingPressureOrdinaryProbeStateAsync(lease));
                });
                WritePressureOwnerTiming("zero_fresh_retry_common_c4", setup.Spec.Name, started);
                await ReadPressureGroupSignedNextAsync(setup, validator);
            }, hooks));
    }
}
