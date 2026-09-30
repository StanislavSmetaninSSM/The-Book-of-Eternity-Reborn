using System.Globalization;

namespace BookOfEternityClient.Services;

internal sealed record MortalItemOwnedResourceConsumptionPlanningResult(
    ResourceStateLedger? StateAfterImage,
    IReadOnlyList<ResourceCapacityIntent> CapacityTransitions,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => StateAfterImage != null && Issues.Count == 0;
}

/// <summary>
/// Plans strict proportional reconfiguration for the live resources owned by
/// one partially consumed Mortal item. This helper is write-free and does not
/// inspect or append resource history.
/// </summary>
internal static class MortalItemOwnedResourceConsumptionPlanner
{
    internal static MortalItemOwnedResourceConsumptionPlanningResult Plan(
        int turn,
        MortalItemConsumptionCommand command,
        int sourceCount,
        int remainingCount,
        ResourceDefinitionCatalog definitions,
        ResourceStateLedger state,
        ResourceSourceEvidence sourceEvidence,
        string policyFingerprint)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sourceEvidence);

        var issues = new List<ValidationIssue>();
        if (turn < 1 || sourceCount <= 0 || remainingCount <= 0 ||
            remainingCount >= sourceCount ||
            !ResourceMaterializationContract.IsExactIdentifier(sourceEvidence.SourceKind) ||
            !ResourceMaterializationContract.IsExactIdentifier(sourceEvidence.SourceId) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(
                sourceEvidence.AuthorityFingerprint) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(policyFingerprint))
        {
            issues.Add(Issue(
                command.ItemId,
                "mortal_item_consumption_capacity_authority_invalid",
                "one exact partial-consumption resource authority",
                $"turn={turn}; before={sourceCount}; after={remainingCount}"));
            return Invalid(issues);
        }

        var entries = state.Entries
            .Where(entry =>
                string.Equals(entry.Coordinate.Realm, "mortal_world", StringComparison.Ordinal) &&
                entry.Coordinate.OwnerKind == ResourceOwnerKind.Item &&
                string.Equals(
                    entry.Coordinate.ResourceOwnerId,
                    command.ItemId,
                    StringComparison.Ordinal))
            .OrderBy(static entry => entry.Coordinate.ResourceKey, StringComparer.Ordinal)
            .ToArray();
        if (entries.Length == 0)
        {
            return new MortalItemOwnedResourceConsumptionPlanningResult(
                new ResourceStateLedger(state.Entries),
                Array.Empty<ResourceCapacityIntent>(),
                Array.Empty<ValidationIssue>());
        }

        var replacements = new Dictionary<ResourceCoordinate, ResourceStateEntry>(
            ResourceCoordinateComparer.Instance);
        var capacities = new List<ResourceCapacityIntent>(entries.Length);
        foreach (var entry in entries)
        {
            if (!definitions.TryResolveExact(entry.Coordinate.ResourceKey, out var definition) ||
                definition == null ||
                definition.CapacityPolicy.Kind != ResourceCapacityKind.InstanceFixed ||
                entry.CapacityBinding.Kind != ResourceCapacityKind.InstanceFixed)
            {
                issues.Add(Issue(
                    command.ItemId,
                    "mortal_item_consumption_capacity_not_instance_fixed",
                    "one live instance_fixed item-owned coordinate",
                    entry.Coordinate.ResourceKey));
                continue;
            }

            if (!TryScaleExact(
                    entry.Maximum,
                    remainingCount,
                    sourceCount,
                    definition.Quantum,
                    definition.MinimumPolicy.Value,
                    out var newMaximum) ||
                !TryScaleExact(
                    entry.Current,
                    remainingCount,
                    sourceCount,
                    definition.Quantum,
                    definition.MinimumPolicy.Value,
                    out var newCurrent) ||
                newMaximum <= definition.MinimumPolicy.Value ||
                newCurrent < definition.MinimumPolicy.Value ||
                newCurrent > newMaximum)
            {
                issues.Add(Issue(
                    command.ItemId,
                    "mortal_item_consumption_capacity_ratio_inexact",
                    "checked exact quantum-aligned proportional capacity and current",
                    $"{entry.Coordinate.ResourceKey}:{entry.Current}/{entry.Maximum}*" +
                    $"{remainingCount}/{sourceCount}"));
                continue;
            }

            var resolved = ResolvedResourceCapacity.Resolve(
                definition,
                entry.Coordinate,
                new InstanceFixedCapacityInput(
                    new ResourceFormulaOwner(
                        entry.Coordinate.Realm,
                        entry.Coordinate.OwnerKind,
                        entry.Coordinate.ResourceOwnerId),
                    newMaximum,
                    policyFingerprint),
                entry.CapacityBinding.AuthorityKey,
                includeInitialization: false);
            if (!resolved.IsValid || resolved.Capacity == null)
            {
                issues.AddRange(resolved.Issues);
                continue;
            }

            var capacity = resolved.Capacity.AsReconfiguration();
            capacities.Add(new ResourceCapacityIntent(
                $"turn_{turn}:mortal_item_consumption:" +
                command.FinalizationOrdinal.ToString("D4", CultureInfo.InvariantCulture) +
                $":{entry.Coordinate.ResourceKey}",
                sourceEvidence.SourceKind,
                sourceEvidence.SourceId,
                entry.Coordinate,
                ResourceCapacityOperation.Reconfigure,
                capacity,
                ResourceCurrentDisposition.ScaleRatioExact,
                ResourceMutationPhase.RegisteredSystemOutcome,
                Priority: 70,
                sourceEvidence,
                policyFingerprint,
                ReceiptId: null));
            replacements.Add(
                entry.Coordinate,
                entry with
                {
                    Current = newCurrent,
                    Maximum = newMaximum,
                    CapacityBinding = capacity.Binding
                });
        }

        if (issues.Count > 0)
            return Invalid(issues);

        var afterEntries = state.Entries
            .Select(entry => replacements.GetValueOrDefault(entry.Coordinate, entry))
            .ToArray();
        return new MortalItemOwnedResourceConsumptionPlanningResult(
            new ResourceStateLedger(afterEntries),
            capacities.ToArray(),
            Array.Empty<ValidationIssue>());
    }

    private static bool TryScaleExact(
        decimal value,
        int remainingCount,
        int sourceCount,
        decimal quantum,
        decimal minimum,
        out decimal scaled)
    {
        scaled = 0m;
        try
        {
            var product = checked(value * remainingCount);
            scaled = checked(product / sourceCount);
            return checked(scaled * sourceCount) == product &&
                   ResourceMaterializationContract.IsQuantumAligned(
                       scaled,
                       minimum,
                       quantum);
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static MortalItemOwnedResourceConsumptionPlanningResult Invalid(
        IEnumerable<ValidationIssue> issues) => new(
        null,
        Array.Empty<ResourceCapacityIntent>(),
        issues.ToArray());

    private static ValidationIssue Issue(
        string itemId,
        string code,
        string expected,
        string actual) => new(
        ResourceMaterializationContract.StatePath,
        IssueSeverity.Error,
        "Mortal item-owned resource consumption planning failed.",
        code: code,
        actor: "mortal_item:" + itemId,
        section: "MortalItemMaterialization",
        expected: expected,
        actual: actual,
        repairHint: "Reject the complete item consumption plan and rebuild its sealed authority.",
        repairTargetFiles: new[] { ResourceMaterializationContract.StatePath });
}
