namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundTreatmentItemPublicationAuthority
{
    internal IReadOnlyList<ValidationIssue> ValidateResourceProjection(
        AcceptedMechanicsResourcePlanningResult resourceResult)
    {
        ArgumentNullException.ThrowIfNull(resourceResult);
        var stateAfterImage = resourceResult.StateAfterImage;
        if (stateAfterImage is null)
            return new[] { Issue("resources", "one final resource state", "missing") };
        var transitions = resourceResult.AppliedTransitions
            .Concat(resourceResult.ReplayTransitions)
            .ToArray();
        var itemSources = _capacityTransitions
            .Select(static capacity => capacity.SourceEvidence)
            .Distinct()
            .ToHashSet();
        var owned = transitions
            .Where(transition => itemSources.Contains(transition.SourceEvidence))
            .OrderBy(static transition => transition.ExecutionSequence)
            .ToArray();
        if (owned.Length != _capacityTransitions.Length)
        {
            return new[] { Issue("capacity", $"count={_capacityTransitions.Length}",
                $"count={owned.Length}") };
        }

        var lastAfterByCoordinate = new Dictionary<
            ResourceCoordinate,
            ResourceStateSnapshot>(ResourceCoordinateComparer.Instance);
        var previousSequence = -1;
        for (var index = 0; index < _capacityTransitions.Length; index++)
        {
            var expected = _capacityTransitions[index];
            var actual = owned[index];
            ResourceStateSnapshot? expectedBefore;
            if (!lastAfterByCoordinate.TryGetValue(
                    expected.Coordinate,
                    out expectedBefore))
            {
                if (!_consumptionInput.ResourceState.TryResolveExact(
                        expected.Coordinate,
                        out var initial) ||
                    initial is null)
                {
                    return new[] { Issue("capacity", expected.EventRef,
                        "missing sealed before-state") };
                }
                expectedBefore = initial.Snapshot;
            }
            if (actual.ExecutionSequence <= previousSequence ||
                !CapacityTransitionMatches(
                    expected,
                    actual,
                    expectedBefore,
                    _consumptionInput.Turn,
                    _identitySeed))
            {
                return new[] { Issue("capacity", expected.EventRef,
                    actual.EventRef) };
            }
            previousSequence = actual.ExecutionSequence;
            lastAfterByCoordinate[expected.Coordinate] = actual.AfterState!;
        }

        foreach (var pair in lastAfterByCoordinate)
        {
            var live = stateAfterImage.Entries.Where(entry =>
                    ResourceCoordinateComparer.Instance.Equals(
                        entry.Coordinate,
                        pair.Key))
                .ToArray();
            if (live.Length != 1 || live[0].Snapshot != pair.Value)
            {
                return new[] { Issue("resources", Describe(pair.Key),
                    $"liveMatches={live.Length}") };
            }
        }
        foreach (var owner in _terminalOwners)
        {
            if (stateAfterImage.Entries.Any(entry =>
                    string.Equals(entry.Coordinate.Realm, owner.Realm,
                        StringComparison.Ordinal) &&
                    entry.Coordinate.OwnerKind == owner.OwnerKind &&
                    string.Equals(entry.Coordinate.ResourceOwnerId,
                        owner.ResourceOwnerId,
                        StringComparison.Ordinal)))
            {
                return new[] { Issue("terminalOwners", owner.ResourceOwnerId,
                    "live resource remained") };
            }
        }
        return Array.Empty<ValidationIssue>();
    }

    private static bool CapacityTransitionMatches(
        ResourceCapacityIntent expected,
        ResourceTransition actual,
        ResourceStateSnapshot expectedBefore,
        int expectedTurn,
        string identitySeed) =>
        expected.Operation == ResourceCapacityOperation.Reconfigure &&
        expected.ResolvedCapacity is { } resolved &&
        expected.CurrentDisposition == ResourceCurrentDisposition.ScaleRatioExact &&
        actual.BeforeState == expectedBefore &&
        actual.AfterState is { } after &&
        MortalWoundTreatmentResourcePublicationAuthority.CreateMutationIdentities(
            identitySeed,
            expected) is var identities &&
        string.Equals(actual.OperationId, identities.OperationId,
            StringComparison.Ordinal) &&
        string.Equals(actual.TransitionId, identities.TransitionId,
            StringComparison.Ordinal) &&
        string.Equals(actual.EventRef, expected.EventRef,
            StringComparison.Ordinal) &&
        string.Equals(actual.OriginKind, expected.OriginKind,
            StringComparison.Ordinal) &&
        string.Equals(actual.OriginId, expected.OriginId,
            StringComparison.Ordinal) &&
        ResourceCoordinateComparer.Instance.Equals(
            actual.Coordinate,
            expected.Coordinate) &&
        actual.Operation == ResourceTransitionOperation.Reconfigure &&
        actual.RequestedAmount == 0m &&
        actual.AppliedAmount == 0m &&
        actual.Outcome == ResourceTransitionOutcome.Applied &&
        actual.CapacityDisposition == ResourceCapacityDisposition.ScaleRatioExact &&
        actual.Phase == expected.Phase &&
        actual.Priority == expected.Priority &&
        actual.Turn == expectedTurn &&
        actual.SourceEvidence == expected.SourceEvidence &&
        string.Equals(actual.PolicyFingerprint,
            expected.PolicyFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(actual.ReceiptId, expected.ReceiptId,
            StringComparison.Ordinal) &&
        after.State == expectedBefore.State &&
        expectedBefore.Maximum != 0m &&
        ResourceMaterializationContract.ProductsEqualExact(
            after.Current,
            expectedBefore.Maximum,
            expectedBefore.Current,
            after.Maximum) &&
        after.Maximum == resolved.Maximum &&
        after.CapacityBinding == resolved.Binding;
}
