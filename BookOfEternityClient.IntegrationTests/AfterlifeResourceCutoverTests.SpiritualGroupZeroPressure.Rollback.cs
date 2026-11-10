using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>One N4 capped source-only late committed-write failure restores signed46, then fresh same-byte owners publish once through signed47.</summary>
    [Fact]
    public async Task SpiritualGroupZero_LateCutRestoresSignedOriginAndFreshRetryPublishesOnce()
    {
        var armed = false; var cutEnabled = true; var cuts = 0;
        CurrentCommittedPublicationWitness? witness = null;
        var injected = new IOException("Complete N4 pressure zero C4 receipt read-back failure.");
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
                    cuts++; cutEnabled = false; armed = false; throw injected;
                }
                return Task.CompletedTask;
            }
        };
        var spec = GroupZeroCase("group_zero_n4_late_retry", GroupZeroPressureContour.Capped);
        await WithPairLowFailureAsync(spec.Name, () => WithPressureGroupPublicationAsync(spec, async setup =>
        {
            var context = setup.Copy.Context;
            ValidationService.SpiritualOriginalTurnCapture? retiredCapture = null;
            GroupZeroRetryAttemptData? firstAttempt = null;
            var first = await CompleteGroupZeroSourceOnlyC3Async(setup, registerPublication: true,
                retainDisposed: capture => retiredCapture = capture, observeCompletedColdC3: attempt =>
                {
                    firstAttempt = attempt;
                    _storageOutput.WriteLine(JsonSerializer.Serialize(new
                    {
                        phase = "group_zero_n4_first_attempt_verified_before_late_hook", setup.Spec.Name,
                        freshIds = attempt.Allocations.Length, checkedBindings = attempt.Bindings.Count, attempt
                    }));
                });
            Assert.NotNull(retiredCapture); Assert.False(retiredCapture!.IsCurrentOwner);
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
            await AssertPressureGroupRegisteredPlanAsync(setup, first, await PeekPlanAsync(context));
            ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority? oldAuthority = null;
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out oldAuthority));
                Assert.Empty(await oldAuthority!.ValidateCurrentInputsAsync(context.FileSystem, lease));
            });
            var backups = await ReadSpiritualC4BackupsAsync(context);
            witness = new(context.FileSystem, AfterlifeSpiritualConflictState.StatePath,
                ResourceMaterializationContract.StatePath, SpiritualWoundOpportunityReceiptState.StatePath);
            var failedWitness = witness;
            var started = Stopwatch.GetTimestamp(); Exception? failure;
            armed = true;
            try
            {
                failure = await Record.ExceptionAsync(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    context.FileSystem, context.Normalizer, context.Validator, backups));
            }
            finally { armed = false; }
            Assert.Equal(1, cuts); Assert.Same(injected, failure);
            failedWitness.AssertRestored("group-zero-N4-signed46-late-cut", setup.Origin.Tracked.ToDictionary(
                row => context.FileSystem.ResolvePath(row.Key), row => row.Value, StringComparer.Ordinal), failure, _storageOutput.WriteLine);
            AssertPressureGroupSignedOrigin(context, setup.Origin);
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority!, out _));
                Assert.False(retiredCapture.IsCurrentOwner);
                await Assert.ThrowsAsync<ObjectDisposedException>(async () => await retiredCapture.ReadExistingPressureOrdinaryProbeStateAsync(lease));
                AssertPressureNoPrivatePair(context, lease);
            });
            AssertPressureGroupSignedOrigin(context, setup.Origin);
            WritePressureOwnerTiming("group_zero_n4_late_cut_signed46_rollback", setup.Spec.Name, started);
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                foreach (var (path, bytes) in setup.Authored)
                {
                    Assert.Contains(path, PressureAuthoredDraftPaths);
                    if (bytes is null) Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, path));
                    else await context.FileSystem.WriteFileAtomicBytesAsync(lease, path, bytes);
                }
                foreach (var (path, bytes) in setup.Authored) Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority!, out _));
                AssertPressureNoPrivatePair(context, lease);
            });
            // This whole-tree assertion includes exact original signature/snapshot bytes and generation; no resigning.
            AssertPressureGroupOnlyReauthoredInputs(context, setup);
            started = Stopwatch.GetTimestamp(); GroupZeroRetryAttemptData? retryAttempt = null;
            var retry = await CompleteGroupZeroSourceOnlyC3Async(setup, registerPublication: true,
                observeCompletedColdC3: attempt =>
                {
                    retryAttempt = attempt;
                    _storageOutput.WriteLine(JsonSerializer.Serialize(new
                    {
                        phase = "group_zero_n4_fresh_retry_verified_before_publication", setup.Spec.Name,
                        freshIds = attempt.Allocations.Length, checkedBindings = attempt.Bindings.Count, attempt
                    }));
                });
            Assert.Equal(first.ActualRootJson, retry.ActualRootJson);
            var checkedFirst = Assert.IsType<GroupZeroRetryAttemptData>(firstAttempt);
            var checkedRetry = Assert.IsType<GroupZeroRetryAttemptData>(retryAttempt);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new
            {
                phase = "group_zero_n4_late_retry_complete_envelopes", setup.Spec.Name,
                freshIdsPerAttempt = 8, checkedBindingsPerAttempt = 57, independentlyDerivedBindings = 11,
                firstAllocations = checkedFirst.Allocations, retryAllocations = checkedRetry.Allocations,
                firstBindings = checkedFirst.Bindings, retryBindings = checkedRetry.Bindings
            }));
            AssertGroupZeroFreshRetryEquivalent(checkedFirst, checkedRetry);
            AssertGroupZeroRetryOracleRejectsMutations(checkedFirst, checkedRetry);
            var validator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
            AssertNoConflictFrameErrors(await validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
            var plan = await PeekPlanAsync(context);
            await AssertPressureGroupRegisteredPlanAsync(setup, retry, plan);
            ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority? retryAuthority = null;
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out retryAuthority));
                Assert.NotSame(oldAuthority, retryAuthority);
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority!, out _));
                Assert.False(retiredCapture.IsCurrentOwner);
                await Assert.ThrowsAsync<ObjectDisposedException>(async () => await retiredCapture.ReadExistingPressureOrdinaryProbeStateAsync(lease));
            });
            witness = new(context.FileSystem, AfterlifeSpiritualConflictState.StatePath,
                ResourceMaterializationContract.StatePath, SpiritualWoundOpportunityReceiptState.StatePath);
            armed = true;
            try { await PublishGroupZeroAndReadNextAsync(setup, retry); }
            finally { armed = false; }
            Assert.Equal(1, cuts);
            witness.RequireSingleCurrent(AfterlifeSpiritualConflictState.StatePath);
            witness.RequireSingleCurrent(ResourceMaterializationContract.StatePath);
            witness.RequireSingleCurrent(SpiritualWoundOpportunityReceiptState.StatePath);
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority!, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, retryAuthority!, out _));
                Assert.False(retiredCapture.IsCurrentOwner);
                await Assert.ThrowsAsync<ObjectDisposedException>(async () => await retiredCapture.ReadExistingPressureOrdinaryProbeStateAsync(lease));
                AssertPressureNoPrivatePair(context, lease);
            });
            Assert.Null(CleanupPublicationCut.ReadOptional(witness.JournalPath));
            WritePressureOwnerTiming("group_zero_n4_fresh_retry_common_c4_signed47", setup.Spec.Name, started);
        }, hooks));
    }
}
