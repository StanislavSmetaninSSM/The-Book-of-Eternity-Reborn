using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class ResourceOwnerComposition
{
    internal static ResourceOwnerCompositionResult Combine(
        params ResourceOwnerCompositionResult[] compositions)
    {
        ArgumentNullException.ThrowIfNull(compositions);
        var issues = compositions
            .SelectMany(static result => result.Issues)
            .ToList();
        if (issues.Count != 0 || compositions.Any(static result => result.Authority == null))
            return Invalid(issues);

        var authority = ResourceOwnerAuthority.Combine(
            compositions.Select(static result => result.Authority!).ToArray());
        issues.AddRange(authority.Issues);

        var afterImages = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var pair in compositions.SelectMany(
                     static result => result.OwnerCompanionAfterImages))
        {
            if (!afterImages.TryAdd(pair.Key, pair.Value.DeepClone().AsObject()))
            {
                Add(
                    issues,
                    pair.Key,
                    "resource_owner_companion_write_conflict",
                    "one owner composer for each companion root",
                    "multiple prepared after-images");
            }
        }

        var combatIdentities = compositions
            .Where(static result => result.CombatantIdentities != null)
            .Select(static result => result.CombatantIdentities)
            .ToArray();
        if (combatIdentities.Length > 1)
        {
            Add(
                issues,
                "ownerComposition.combatantIdentities",
                "resource_owner_combat_identity_composition_conflict",
                "at most one common combat identity plan",
                combatIdentities.Length.ToString());
        }

        if (issues.Count != 0)
            return Invalid(issues);

        return new ResourceOwnerCompositionResult(
            authority,
            new ReadOnlyDictionary<string, JsonObject>(afterImages),
            Array.AsReadOnly(compositions
                .SelectMany(static result => result.OwnerTransitions)
                .Select(static value => value.Clone())
                .ToArray()),
            combatIdentities.SingleOrDefault(),
            Array.AsReadOnly(compositions
                .SelectMany(static result => result.CapacityDrafts)
                .ToArray()),
            Array.AsReadOnly(compositions
                .SelectMany(static result => result.TerminalOwners)
                .Distinct()
                .ToArray()),
            Array.Empty<ValidationIssue>());
    }

    private static ResourceOwnerCompositionResult Invalid(
        IReadOnlyList<ValidationIssue> issues) =>
        new(
            null,
            new ReadOnlyDictionary<string, JsonObject>(
                new Dictionary<string, JsonObject>(StringComparer.Ordinal)),
            Array.Empty<AcceptedMechanicsOwnerTransition>(),
            null,
            Array.Empty<ResourceOwnerCapacityDraft>(),
            Array.Empty<ResourceOwnerKey>(),
            issues.ToArray());

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Common resource owner composition is ambiguous.",
            code,
            "ResourceMaterialization",
            expected,
            actual));
}
