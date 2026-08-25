using System.Globalization;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceOwnerTerminalScaleTests
{
    private const string Fingerprint =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void TerminalCleanup_DoublingPopulationUsesOneIndexAndKeepsCanonicalOrder()
    {
        var one = ComposeTerminalCleanup(128, reverseInput: false);
        var two = ComposeTerminalCleanup(256, reverseInput: false);
        var reversed = ComposeTerminalCleanup(128, reverseInput: true);

        Assert.Empty(one.Issues);
        Assert.Empty(two.Issues);
        Assert.Empty(reversed.Issues);
        Assert.Equal(256, one.Transitions.Count);
        Assert.Equal(512, two.Transitions.Count);
        Assert.Equal(
            one.Transitions.Select(TransitionKey),
            reversed.Transitions.Select(TransitionKey));

        Assert.Equal(256, one.Meter.OwnerTerminalStateIndexVisitCount);
        Assert.Equal(128, one.Meter.OwnerTerminalLookupCount);
        Assert.Equal(256, one.Meter.OwnerTerminalEntryVisitCount);
        Assert.Equal(512, two.Meter.OwnerTerminalStateIndexVisitCount);
        Assert.Equal(256, two.Meter.OwnerTerminalLookupCount);
        Assert.Equal(512, two.Meter.OwnerTerminalEntryVisitCount);
        Assert.True(one.Meter.OwnerTerminalCleanupWorkUnits > 0);
        Assert.True(
            two.Meter.OwnerTerminalCleanupWorkUnits <=
            one.Meter.OwnerTerminalCleanupWorkUnits * 2.5,
            $"Expected near-linear terminal cleanup work, but " +
            $"{one.Meter.OwnerTerminalCleanupWorkUnits} units became " +
            $"{two.Meter.OwnerTerminalCleanupWorkUnits}.");
    }

    private static TerminalCleanupResult ComposeTerminalCleanup(
        int ownerCount,
        bool reverseInput)
    {
        var terminalOwners = new List<ResourceOwnerKey>(ownerCount);
        var entries = new List<ResourceStateEntry>(ownerCount * 2);
        for (var index = 0; index < ownerCount; index++)
        {
            var suffix = index.ToString("D4", CultureInfo.InvariantCulture);
            var owner = new ResourceOwnerKey(
                "mortal_world",
                ResourceOwnerKind.Item,
                "terminal_scale_item_" + suffix);
            terminalOwners.Add(owner);
            entries.Add(Entry(owner, "charges", suffix));
            entries.Add(Entry(owner, "ammunition", suffix));
        }

        if (reverseInput)
        {
            terminalOwners.Reverse();
            entries.Reverse();
        }

        var authority = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            Array.Empty<ResourceOwnerExport>(),
            Array.Empty<ResourceOwnerExport>(),
            terminalOwners));
        Assert.Empty(authority.Issues);
        var issues = new List<ValidationIssue>();
        var meter = new ResourceAuthorityWorkMeter();
        var transitions = AcceptedMechanicsPlanner.ComposeOwnerCapacityTransitions(
            turn: 2,
            authority,
            new ResourceStateLedger(entries),
            Array.Empty<ResourceOwnerCapacityDraft>(),
            terminalOwners,
            issues,
            meter);
        return new TerminalCleanupResult(transitions, issues, meter);
    }

    private static ResourceStateEntry Entry(
        ResourceOwnerKey owner,
        string resourceKey,
        string suffix) =>
        new(
            new ResourceCoordinate(
                owner.Realm,
                owner.OwnerKind,
                owner.ResourceOwnerId,
                resourceKey),
            Current: 1m,
            Maximum: 1m,
            new ResourceCapacityBinding(
                ResourceCapacityKind.InstanceFixed,
                $"terminal_scale_capacity_{resourceKey}_{suffix}",
                Fingerprint),
            ResourceLifecycleState.Active,
            new ResourceChronology(
                CreatedAtTurn: 1,
                CreatedEventRef: $"turn_1:terminal_scale:{resourceKey}:{suffix}",
                LastTransitionId: $"terminal_scale_transition_{resourceKey}_{suffix}",
                LastEventRef: $"turn_1:terminal_scale:{resourceKey}:{suffix}",
                LastTransitionTurn: 1));

    private static string TransitionKey(ResourceCapacityIntent transition) =>
        transition.Coordinate.ResourceOwnerId + "/" +
        transition.Coordinate.ResourceKey;

    private sealed record TerminalCleanupResult(
        IReadOnlyList<ResourceCapacityIntent> Transitions,
        IReadOnlyList<ValidationIssue> Issues,
        ResourceAuthorityWorkMeter Meter);
}
