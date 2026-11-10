using System.Diagnostics;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>One reached late N2 cut restores the complete signed origin and retires publication authority; no retry-equivalence claim.</summary>
    [Fact]
    public Task SpiritualGuardianGroupPressure_N2LateCutRestoresSignedOrigin()
    {
        var armed = false; var cuts = 0;
        CurrentCommittedPublicationWitness? witness = null;
        var injected = new IOException("Retained N2 committed receipt read-back failure.");
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) => { if (armed) witness!.Observe(phase, index); },
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (armed && path == SpiritualWoundOpportunityReceiptState.StatePath &&
                    witness!.Has(AfterlifeSpiritualConflictState.StatePath) && witness.Has(ResourceMaterializationContract.StatePath) &&
                    witness.Has(SpiritualWoundOpportunityReceiptState.StatePath))
                {
                    witness.RequireSingleCurrent(AfterlifeSpiritualConflictState.StatePath);
                    witness.RequireSingleCurrent(ResourceMaterializationContract.StatePath);
                    witness.RequireSingleCurrent(SpiritualWoundOpportunityReceiptState.StatePath);
                    cuts++; armed = false; throw injected;
                }
                return Task.CompletedTask;
            }
        };
        return WithGuardianGroupRegisteredPlanAsync("guardian_group_n2_late_cut", async (setup, completed, plan) =>
        {
            var context = setup.Copy.Context;
            AssertGuardianAttributionRegisteredPlan(setup, completed, plan);
            ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority oldAuthority = null!;
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out oldAuthority));
                Assert.Same(plan, oldAuthority.Plan);
                Assert.Empty(await oldAuthority.ValidateCurrentInputsAsync(context.FileSystem, lease));
            });
            var backups = await ReadSpiritualC4BackupsAsync(context);
            witness = new(context.FileSystem, AfterlifeSpiritualConflictState.StatePath,
                ResourceMaterializationContract.StatePath, SpiritualWoundOpportunityReceiptState.StatePath);
            var epoch = context.FileSystem.CanonicalRootAuthorityIdentity.ValidationEpoch;
            Assert.True(epoch.TryCapture(out var beforeEpoch));
            var started = Stopwatch.GetTimestamp();
            Exception? failure;
            armed = true;
            try
            {
                failure = await Record.ExceptionAsync(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    context.FileSystem, context.Normalizer, context.Validator, backups));
            }
            finally { armed = false; }
            Assert.Equal(1, cuts); Assert.Same(injected, failure);
            witness.AssertRestored(setup.Name, setup.Origin.Tracked.ToDictionary(
                row => context.FileSystem.ResolvePath(row.Key), row => row.Value, StringComparer.Ordinal), failure, _storageOutput.WriteLine);
            AssertPressureGroupSignedOrigin(context, setup.Origin);
            Assert.True(epoch.TryCapture(out var afterEpoch)); Assert.NotSame(beforeEpoch, afterEpoch);
            Assert.Equal(0, epoch.CaptureActivity().Active);
            await ExecuteGuardianLeaseStagesAsync(context, lease =>
            {
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority, out _));
                AssertPressureNoPrivatePair(context, lease);
                return Task.CompletedTask;
            });
            WritePressureOwnerTiming("guardian_group_n2_late_cut_restore", setup.Name, started);
        }, hooks);
    }
}
