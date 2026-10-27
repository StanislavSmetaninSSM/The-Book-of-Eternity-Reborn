using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

/// <summary>Executable self-healing belongs to the actual publication lease.</summary>
public sealed class AfterlifeWoundHealingPublicationOwnerTests
{
    [Fact]
    public async Task DeferredRoutingLeavesNoExecutablePlanAndPublicationUsesItsOwnLease()
    {
        await using var fixture = await HealingFixture.CreateAsync(2, 2, 1);
        var validator = new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance);
        FileSystemManager.CanonicalWriteLease originalLease;
        Dictionary<string, byte[]?> before;
        AcceptedMechanicsPlan publication;
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
            publication = Assert.IsType<AcceptedMechanicsPlan>(await new CanonicalStateNormalizer(fixture.Files,
                NullLogger<CanonicalStateNormalizer>.Instance).BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
            Assert.Same(ready.Plan, publication);
        }
        var wound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        Assert.Equal(2, wound.Severity.Rank);
        Assert.Equal(3, wound.Recovery.CurrentStepProgress);
        await AssertVisibleOutputAsync(fixture, publication, healed: false);
        await fixture.AssertOneCycleAndRetainedAttemptAsync();
        fixture.AssertResourcesUnchanged();
    }

    [Fact]
    public async Task MissingOrReplacedSourceCannotFallBackToAnEarlierLeasePlan()
    {
        string? faultPath = null;
        var uncertainty = new CoordinatedStatePublicationUncertainException(new IOException("owned manifest read fault"));
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (faultPath == path)
                {
                    faultPath = null;
                    return Task.FromException(uncertainty);
                }
                return Task.CompletedTask;
            }
        };
        await using var fixture = await HealingFixture.CreateAsync(1, 1, 1, hooks: hooks);
        var validator = new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance);
        AcceptedMechanicsPlan earlier;
        byte[] original;
        byte[] manifest;
        AcceptedMechanicsPlan publication;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            await fixture.WriteReportAsync(lease);
            await fixture.Files.WriteFileAtomicAsync(lease, "output/narrative_response.json",
                JsonSerializer.Serialize(new { response = "Духовная рана восстанавливается.", timestamp = DateTime.UtcNow.ToString("o") }));
            AssertNoErrors(await validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease));
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(fixture.Files, lease, out _, out var ready));
            earlier = ready.Plan!;
            original = (await fixture.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!;
            manifest = (await fixture.Files.ReadFileBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!;
        }

        await using (var current = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            fixture.Files.DeleteFile(current, LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
            fixture.Files.DeleteFile(current, AcceptedMechanicsPlan.WoundCommandPath);
            var missingProof = fixture.CaptureMutationRoots(new[] { LiveTurnPreparationService.PendingTurnSnapshotManifestPath });
            var proofIssues = await validator.PrepareSpiritualSelfTreatmentPublicationAsync(current);
            Assert.Contains(proofIssues, issue => issue.Code == "spiritual_self_treatment_admission_invalid" &&
                issue.Actual == "Spiritual treatment requires its signed original manifest.");
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(fixture.Files, current));
            fixture.AssertMutationRootsUnchanged(missingProof);

            await fixture.Files.WriteFileAtomicBytesAsync(current, AcceptedMechanicsPlan.WoundCommandPath, original);
            var missingManifest = fixture.CaptureMutationRoots(new[] { LiveTurnPreparationService.PendingTurnSnapshotManifestPath });
            var manifestIssues = await validator.PrepareSpiritualSelfTreatmentPublicationAsync(current);
            Assert.Contains(manifestIssues, issue => issue.Code == "spiritual_self_treatment_admission_invalid" &&
                issue.Actual == "Spiritual treatment requires its signed original manifest.");
            fixture.AssertMutationRootsUnchanged(missingManifest);

            await fixture.Files.WriteFileAtomicBytesAsync(current, LiveTurnPreparationService.PendingTurnSnapshotManifestPath, manifest);
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
            faultPath = LiveTurnPreparationService.PendingTurnSnapshotManifestPath;
            var propagated = await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() =>
                validator.PrepareSpiritualSelfTreatmentPublicationAsync(current));
            Assert.Same(uncertainty, propagated);
            Assert.Null(faultPath);
            fixture.AssertMutationRootsUnchanged(restored);
            AssertNoErrors(await validator.PrepareSpiritualSelfTreatmentPublicationAsync(current));
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(fixture.Files, current, out _, out var ready));
            Assert.NotSame(earlier, ready.Plan);
            Assert.NotSame(earlier.SpiritualHealingContinuation, ready.Plan!.SpiritualHealingContinuation);
            ready.Plan.SpiritualHealingContinuation!.RequireCurrent(fixture.Files, current);
            fixture.AssertMutationRootsUnchanged(restored);
            publication = Assert.IsType<AcceptedMechanicsPlan>(await new CanonicalStateNormalizer(fixture.Files,
                NullLogger<CanonicalStateNormalizer>.Instance).BindTo(current).NormalizeAcceptedMechanicsAsync(backups: null));
            Assert.Same(ready.Plan, publication);
        }
        Assert.Empty((await fixture.ReadWoundsAsync()).Occurrences);
        await AssertVisibleOutputAsync(fixture, publication, healed: true);
        var accepted = fixture.CaptureMutationRoots(new[] { "output/narrative_response.json", "output/interface_updates.json" });
        faultPath = "output/narrative_response.json";
        Assert.Same(uncertainty, await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() =>
            GameEngine.BindPublishedAcceptedWoundOutputAsync(fixture.Files, publication)));
        Assert.Null(faultPath);
        fixture.AssertMutationRootsUnchanged(accepted);
        await fixture.AssertOneCycleAndRetainedAttemptAsync();
        fixture.AssertResourcesUnchanged();
    }

    private static async Task AssertVisibleOutputAsync(HealingFixture fixture, AcceptedMechanicsPlan publication, bool healed)
    {
        Assert.True(publication.SpiritualHealingContinuation!.IsClosed);
        var bundle = publication.WoundStageBundle!;
        var mutation = Assert.Single(bundle.FinalPlan.CarrierContributions.SelectMany(c => c.Mutations));
        var unchanged = fixture.CaptureMutationRoots();
        var rejected = WoundPlayerNotification.ComposeAcceptedSpiritualHealing(bundle,
            new[] { new WoundCarrierMutation(mutation.Operation, mutation.WoundId, mutation.BeforeWound, mutation.BeforeWound) });
        Assert.False(rejected.Success);
        Assert.Empty(rejected.Notifications);
        fixture.AssertMutationRootsUnchanged(unchanged);
        var output = await GameEngine.BindPublishedAcceptedWoundOutputAsync(fixture.Files, publication);
        Assert.True(output.Success, string.Join("\n", output.Issues.Select(issue => issue.Code + ": " + issue.Actual)));
        var notification = Assert.Single(output.Notifications);
        Assert.Contains(healed ? "Исцелена духовная рана" : "восстановление 3/4", notification.Text.PlainText);
        Assert.Equal("/раны", notification.DetailCommand);
        Assert.DoesNotContain("wound_spiritual_session", notification.Text.PlainText);
        Assert.DoesNotContain("request_spiritual_healing", notification.Text.PlainText);
        Assert.DoesNotContain(publication.SpiritualHealingContinuation!.Input.Binding.SnapshotToken, notification.Text.PlainText);
    }

    private static void AssertNoErrors(IReadOnlyList<ValidationIssue> issues) =>
        Assert.True(!issues.Any(issue => issue.Severity == IssueSeverity.Error),
            string.Join("\n", issues.Select(issue => issue.Code + ": " + issue.Actual)));
}
