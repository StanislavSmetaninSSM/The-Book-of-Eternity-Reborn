using System.Diagnostics;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Reaches a late actual-v4 cut, restores exact signed46 origin, then retries the same overflow bytes with fresh owners once.</summary>
    [Fact]
    public Task SpiritualPressurePair_LatePublicationCutRestoresSignedOriginAndFreshRetryPublishesOnce() =>
        AssertPressurePairLatePublicationCutAsync(new("pair_overflow_rollback", After: "overwhelmed", AdvanceExpected: true));

    /// <summary>IV+IV overflow reaches the same real late cut, exact signed46 rollback and fresh single retry through signed47.</summary>
    [Fact]
    public Task SpiritualPressurePair_FourFourLatePublicationCutRestoresSignedOriginAndFreshRetryPublishesOnce() =>
        AssertPressurePairLatePublicationCutAsync(new("pair_four_four_overflow_rollback", After: "overwhelmed",
            Ranks: new(4, 4), AdvanceExpected: true));

    /// <summary>Runs the existing full rollback oracle with rank input reaching its real pre-sign publication setup.</summary>
    private async Task AssertPressurePairLatePublicationCutAsync(PressureGroupCase spec)
    {
        var armed = false;
        var cutEnabled = true;
        var cuts = 0;
        CurrentCommittedPublicationWitness? witness = null;
        var injected = new IOException("Pair overflow C4 receipt read-back failure.");
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) => { if (armed) witness!.Observe(phase, index); },
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (armed && cutEnabled && path == SpiritualWoundOpportunityReceiptState.StatePath &&
                    witness!.Has(AfterlifeSpiritualConflictState.StatePath) &&
                    witness.Has(ResourceMaterializationContract.StatePath) &&
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
        await WithPressurePairPublicationAsync(spec, async setup =>
        {
            var ordinary = new SpiritualWoundCalculation(
                new(8, 0, 0, "clear", "overwhelmed", "hostile", 4), 0, 3, 2, 18, 3, 3, 4, true, 3);
            var context = setup.Copy.Context;
            ValidationService.SpiritualOriginalTurnCapture? retiredWarm = null;
            ValidationService.SpiritualC2PrivateSession? retiredChoice = null;
            ValidationService.SpiritualC2PrivateSession? retiredCompleted = null;
            var first = await CompletePressurePairColdC3Async(setup, context.Validator, ordinary,
                (warm, choice, completed) =>
                {
                    // Retain only disposed handles to demonstrate they cannot revive after rollback/retry.
                    retiredWarm = warm;
                    retiredChoice = choice;
                    retiredCompleted = completed;
                });
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
            var firstPlan = await PeekPlanAsync(context);
            await AssertPressurePairRegisteredPlanAsync(setup, first, firstPlan);
            ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority oldAuthority;
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out oldAuthority));
                AssertNoConflictFrameErrors(await oldAuthority.ValidateCurrentInputsAsync(context.FileSystem, lease));
            }
            var backups = await ReadSpiritualC4BackupsAsync(context);
            witness = new(context.FileSystem, AfterlifeSpiritualConflictState.StatePath,
                ResourceMaterializationContract.StatePath, SpiritualWoundOpportunityReceiptState.StatePath);
            var failedWitness = witness;
            var failureStarted = Stopwatch.GetTimestamp();
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
            foreach (var path in new[] { AfterlifeSpiritualConflictState.StatePath,
                         ResourceMaterializationContract.StatePath, SpiritualWoundOpportunityReceiptState.StatePath })
                Assert.True(failedWitness.Has(path), path);
            failedWitness.AssertRestored("pair-overflow-signed46-late-cut",
                setup.Origin.Tracked.ToDictionary(row => context.FileSystem.ResolvePath(row.Key), row => row.Value,
                    StringComparer.Ordinal), failure, _storageOutput.WriteLine);
            AssertPressurePairSignedOrigin(context, setup.Origin);
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority, out _));
                AssertPressureNoPrivatePair(context, lease);
                await AssertPressurePairRetiredOwnersAsync(retiredWarm!, retiredChoice!, retiredCompleted!, lease);
                var reopened = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
                    .OpenC2PrivateSessionAsync(lease);
                Assert.Equal("blocked", reopened.Disposition);
                Assert.Null(reopened.Session);
                Assert.Equal("spiritual_c2_private_checkpoint_required", Assert.Single(reopened.Issues).Code);
            }
            AssertPressurePairSignedOrigin(context, setup.Origin);
            WritePressureOwnerTiming("pair_overflow_late_cut_signed46_rollback", setup.Spec.Name, failureStarted);

            // Reauthor exactly the retained ordinary/output bytes. Never sign again or restore a failed pair/owner.
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                foreach (var (path, bytes) in setup.Authored)
                {
                    Assert.Contains(path, PressureAuthoredDraftPaths);
                    if (bytes is null) Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, path));
                    else await context.FileSystem.WriteFileAtomicBytesAsync(lease, path, bytes);
                }
                foreach (var (path, bytes) in setup.Authored)
                    Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority, out _));
                AssertPressureNoPrivatePair(context, lease);
            }
            AssertPressurePairOnlyReauthoredInputs(context, setup);
            var retryStarted = Stopwatch.GetTimestamp();
            var retryValidator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
            AssertNoConflictFrameErrors(await retryValidator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
            AssertNoConflictFrameErrors(await retryValidator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
            var retry = await CompletePressurePairColdC3Async(setup, retryValidator, ordinary);
            Assert.Equal(first.ActualRootJson, retry.ActualRootJson);
            AssertNoConflictFrameErrors(await retryValidator.ValidateAcceptedTurnRawResourceMaterializationAsync());
            var retryPlan = await PeekPlanAsync(context);
            await AssertPressurePairRegisteredPlanAsync(setup, retry, retryPlan);
            ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority retryAuthority;
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out retryAuthority));
                Assert.NotSame(oldAuthority, retryAuthority);
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority, out _));
                await AssertPressurePairRetiredOwnersAsync(retiredWarm!, retiredChoice!, retiredCompleted!, lease);
            }
            witness = new(context.FileSystem, AfterlifeSpiritualConflictState.StatePath,
                ResourceMaterializationContract.StatePath, SpiritualWoundOpportunityReceiptState.StatePath);
            armed = true;
            try
            {
                var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    context.FileSystem, context.Normalizer, retryValidator, await ReadSpiritualC4BackupsAsync(context));
                AssertNoConflictFrameErrors(published.Issues);
                Assert.Same(retryPlan, published.MechanicsPlan);
            }
            finally { armed = false; }
            Assert.Equal(1, cuts);
            witness.RequireSingleCurrent(AfterlifeSpiritualConflictState.StatePath);
            witness.RequireSingleCurrent(ResourceMaterializationContract.StatePath);
            witness.RequireSingleCurrent(SpiritualWoundOpportunityReceiptState.StatePath);
            await AssertPressurePairPublishedAsync(setup, ordinary, retry, retryPlan);
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, retryAuthority, out _));
                await AssertPressurePairRetiredOwnersAsync(retiredWarm!, retiredChoice!, retiredCompleted!, lease);
            }
            WritePressureOwnerTiming("pair_overflow_fresh_retry_common_c4", setup.Spec.Name, retryStarted);
            await ReadPressurePairSignedNextAsync(setup, retryValidator);
        }, hooks);
    }

    /// <summary>Compares every signed-origin byte/existence witness and exact session generation after the reached cut.</summary>
    private static void AssertPressurePairSignedOrigin(ResourceMaterializationTestContext context, PressurePairSignedOrigin origin)
    {
        var current = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
        Assert.Equal(origin.Files.Keys.OrderBy(path => path, StringComparer.Ordinal), current.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var (path, bytes) in origin.Files) Assert.True(bytes.SequenceEqual(current[path]), path);
        foreach (var (path, bytes) in origin.Tracked)
            Assert.Equal(bytes, CleanupPublicationCut.ReadOptional(context.FileSystem.ResolvePath(path)));
        Assert.Equal(origin.Generation, File.ReadAllBytes(context.FileSystem.SessionGenerationPath));
    }

    /// <summary>Proves reauthoring changed only the original ordinary/output DATA, keeping signature and snapshot files byte-exact.</summary>
    private static void AssertPressurePairOnlyReauthoredInputs(ResourceMaterializationTestContext context, PressurePairPublicationSetup setup)
    {
        var expected = setup.Origin.Files.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.Ordinal);
        foreach (var (path, bytes) in setup.Authored)
        {
            var relative = path.Replace('/', Path.DirectorySeparatorChar);
            if (bytes is null) expected.Remove(relative);
            else expected[relative] = bytes;
        }
        var current = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
        Assert.Equal(expected.Keys.OrderBy(path => path, StringComparer.Ordinal), current.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var (path, bytes) in expected) Assert.True(bytes.SequenceEqual(current[path]), path);
        Assert.Equal(setup.Origin.Generation, File.ReadAllBytes(context.FileSystem.SessionGenerationPath));
    }
}
