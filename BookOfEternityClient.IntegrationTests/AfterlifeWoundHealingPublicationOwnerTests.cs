using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

/// <summary>Executable self-healing belongs to the actual publication lease.</summary>
public sealed class AfterlifeWoundHealingPublicationOwnerTests
{
    [Fact]
    public async Task DeferredRoutingLeavesNoExecutablePlanAndPublicationUsesItsOwnLease()
    {
        await using var fixture = await HealingFixture.CreateAsync(1, 1, 1);
        var validator = new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance);
        FileSystemManager.CanonicalWriteLease originalLease;
        Dictionary<string, byte[]?> before;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            originalLease = lease;
            await fixture.WriteReportAsync(lease);
            before = fixture.CaptureMutationRoots();
            AssertNoErrors(await validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease,
                deferSpiritualSelfTreatmentPublication: true));
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(fixture.Files, lease));
            Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(fixture.Files, lease, out _));
            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(fixture.Files, lease, out _));
            fixture.AssertMutationRootsUnchanged(before);
        }

        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            AssertNoErrors(await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease));
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(fixture.Files, lease, out _, out var ready));
            var owner = Assert.IsType<AfterlifeWoundHealingPlanner.Continuation>(ready.Plan!.SpiritualHealingContinuation);
            owner.RequireCurrent(fixture.Files, lease);
            Assert.Throws<InvalidDataException>(() => owner.RequireCurrent(fixture.Files, originalLease));
            fixture.AssertMutationRootsUnchanged(before);
            var published = await new CanonicalStateNormalizer(fixture.Files, NullLogger<CanonicalStateNormalizer>.Instance)
                .BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null);
            Assert.Same(ready.Plan, published);
        }
        Assert.Empty((await fixture.ReadWoundsAsync()).Occurrences);
        await fixture.AssertOneCycleAndRetainedAttemptAsync();
        fixture.AssertResourcesUnchanged();
    }

    [Fact]
    public async Task MissingOrReplacedSourceCannotFallBackToAnEarlierLeasePlan()
    {
        await using var fixture = await HealingFixture.CreateAsync(1, 1, 1);
        var validator = new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance);
        AcceptedMechanicsPlan earlier;
        byte[] original;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            await fixture.WriteReportAsync(lease);
            AssertNoErrors(await validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease));
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(fixture.Files, lease, out _, out var ready));
            earlier = ready.Plan!;
            original = (await fixture.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!;
        }

        await using (var current = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            fixture.Files.DeleteFile(current, AcceptedMechanicsPlan.WoundCommandPath);
            var missing = fixture.CaptureMutationRoots();
            var missingIssues = await validator.PrepareSpiritualSelfTreatmentPublicationAsync(current);
            Assert.Contains(missingIssues, issue => issue.Code == "spiritual_self_treatment_replay_conflict");
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(fixture.Files, current));
            fixture.AssertMutationRootsUnchanged(missing);

            await fixture.Files.WriteFileAtomicAsync(current, AcceptedMechanicsPlan.WoundCommandPath,
                " " + CanonicalJsonUtf8.DecodeOneOptionalBom(original));
            var replaced = fixture.CaptureMutationRoots();
            var replacedIssues = await validator.PrepareSpiritualSelfTreatmentPublicationAsync(current);
            Assert.Contains(replacedIssues, issue => issue.Code == "spiritual_self_treatment_admission_invalid" &&
                issue.Actual == "The retained spiritual treatment request changed.");
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(fixture.Files, current));
            fixture.AssertMutationRootsUnchanged(replaced);
            Assert.Throws<InvalidDataException>(() => earlier.SpiritualHealingContinuation!.RequireCurrent(fixture.Files, current));

            await fixture.Files.WriteFileAtomicBytesAsync(current, AcceptedMechanicsPlan.WoundCommandPath, original);
            var restored = fixture.CaptureMutationRoots();
            AssertNoErrors(await validator.PrepareSpiritualSelfTreatmentPublicationAsync(current));
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(fixture.Files, current, out _, out var ready));
            Assert.NotSame(earlier, ready.Plan);
            Assert.NotSame(earlier.SpiritualHealingContinuation, ready.Plan!.SpiritualHealingContinuation);
            ready.Plan.SpiritualHealingContinuation!.RequireCurrent(fixture.Files, current);
            fixture.AssertMutationRootsUnchanged(restored);
            var published = await new CanonicalStateNormalizer(fixture.Files, NullLogger<CanonicalStateNormalizer>.Instance)
                .BindTo(current).NormalizeAcceptedMechanicsAsync(backups: null);
            Assert.Same(ready.Plan, published);
        }
        Assert.Empty((await fixture.ReadWoundsAsync()).Occurrences);
        await fixture.AssertOneCycleAndRetainedAttemptAsync();
        fixture.AssertResourcesUnchanged();
    }

    private static void AssertNoErrors(IReadOnlyList<ValidationIssue> issues) =>
        Assert.True(!issues.Any(issue => issue.Severity == IssueSeverity.Error),
            string.Join("\n", issues.Select(issue => issue.Code + ": " + issue.Actual)));
}
