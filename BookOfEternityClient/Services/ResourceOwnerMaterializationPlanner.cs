using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record ResourceOwnerMaterializationCandidate(
    string Path,
    ResourceOwnerKey OwnerKey,
    string OwnerRef,
    JsonObject Envelope,
    IReadOnlyList<string> RequiredResourceKeys);

internal static class ResourceOwnerMaterializationPlanner
{
    internal static IReadOnlyList<ResourceOwnerCapacityDraft> ComposeCapacityDrafts(
        ResourceDefinitionCatalog definitions,
        IReadOnlyList<ResourceOwnerExport> sameTurnExports,
        IReadOnlyList<ResourceOwnerMaterializationCandidate> candidates,
        List<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(sameTurnExports);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(issues);

        var result = new List<ResourceOwnerCapacityDraft>();
        foreach (var candidate in candidates.OrderBy(
                     static value => ResourceDefinitionCatalog.GetOwnerKindToken(
                         value.OwnerKey.OwnerKind),
                     StringComparer.Ordinal).ThenBy(
                     static value => value.OwnerKey.ResourceOwnerId,
                     StringComparer.Ordinal))
        {
            var exports = sameTurnExports.Where(value =>
                    value.Key == candidate.OwnerKey &&
                    string.Equals(value.OwnerRef, candidate.OwnerRef, StringComparison.Ordinal))
                .ToArray();
            if (exports.Length != 1)
            {
                Add(
                    issues,
                    candidate.Path,
                    "resource_owner_materialization_authority_ambiguous",
                    "one exact validated same-turn owner export",
                    candidate.OwnerRef);
                continue;
            }

            var export = exports[0];
            var envelope = candidate.Envelope;
            if (envelope.Count != 1 || envelope["resources"] is not JsonArray resources)
            {
                Add(
                    issues,
                    candidate.Path + ".resourceMaterialization",
                    "resource_owner_materialization_shape_invalid",
                    "closed object containing only a non-empty resources array",
                    envelope.ToJsonString());
                continue;
            }
            if (resources.Count == 0 ||
                resources.Count > ResourceMaterializationContract.MaxCapacityTransitionsPerTurn)
            {
                Add(
                    issues,
                    candidate.Path + ".resourceMaterialization.resources",
                    "resource_owner_materialization_cardinality_invalid",
                    $"1..{ResourceMaterializationContract.MaxCapacityTransitionsPerTurn} resource entries",
                    resources.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                continue;
            }

            var exactKeys = new HashSet<string>(StringComparer.Ordinal);
            var aliases = new HashSet<string>(StringComparer.Ordinal);
            var candidateDrafts = new List<ResourceOwnerCapacityDraft>();
            for (var index = 0; index < resources.Count; index++)
            {
                var path = candidate.Path +
                           $".resourceMaterialization.resources[{index}]";
                if (resources[index] is not JsonObject resource ||
                    resource.Count != 2 ||
                    !resource.ContainsKey("resourceKey") ||
                    !resource.ContainsKey("maximum"))
                {
                    Add(
                        issues,
                        path,
                        "resource_owner_materialization_entry_invalid",
                        "closed resourceKey/maximum object",
                        Describe(resources[index]));
                    continue;
                }
                if (!TryReadExact(resource["resourceKey"], out var resourceKey) ||
                    !exactKeys.Add(resourceKey) ||
                    !aliases.Add(ResourceMaterializationContract.BuildConfusableKey(resourceKey)))
                {
                    Add(
                        issues,
                        path + ".resourceKey",
                        "resource_owner_materialization_resource_ambiguous",
                        "one exact/confusable-unique resourceKey",
                        Describe(resource["resourceKey"]));
                    continue;
                }
                if (!TryReadDecimal(resource["maximum"], out var maximum))
                {
                    Add(
                        issues,
                        path + ".maximum",
                        "resource_owner_materialization_maximum_invalid",
                        "one exact finite decimal maximum",
                        Describe(resource["maximum"]));
                    continue;
                }
                if (!definitions.TryResolveExact(resourceKey, out var definition) ||
                    definition == null ||
                    !definition.AllowedOwnerKinds.Contains(candidate.OwnerKey.OwnerKind) ||
                    !CanMaterializeCapacity(definition, candidate.OwnerKey.OwnerKind))
                {
                    Add(
                        issues,
                        path + ".resourceKey",
                        "resource_owner_materialization_definition_forbidden",
                        "sealed materializable capacity definition allowed for this owner kind",
                        resourceKey);
                    continue;
                }

                var coordinate = new ResourceCoordinate(
                    candidate.OwnerKey.Realm,
                    candidate.OwnerKey.OwnerKind,
                    candidate.OwnerKey.ResourceOwnerId,
                    resourceKey);
                var formulaOwner = new ResourceFormulaOwner(
                    coordinate.Realm,
                    coordinate.OwnerKind,
                    coordinate.ResourceOwnerId);
                ResourceCapacityInput capacityInput;
                string? instanceAuthorityKey;
                if (definition.CapacityPolicy.Kind == ResourceCapacityKind.InstanceFixed)
                {
                    using var authorityFingerprint = new ResourceFingerprintBuilder(
                        "resource-owner-instance-capacity-v1");
                    authorityFingerprint.Append(export.AuthorityFingerprint);
                    authorityFingerprint.Append(candidate.OwnerRef);
                    ResourceStateContract.AppendCoordinate(authorityFingerprint, coordinate);
                    authorityFingerprint.Append(maximum);
                    capacityInput = new InstanceFixedCapacityInput(
                        formulaOwner,
                        maximum,
                        authorityFingerprint.Build());
                    instanceAuthorityKey = candidate.OwnerRef;
                }
                else
                {
                    capacityInput = new RegisteredFormulaCapacityInput(
                        new MaterializedOwnerCapacityFormulaInput(
                            formulaOwner,
                            export.AuthorityFingerprint,
                            maximum));
                    instanceAuthorityKey = null;
                }

                var resolved = ResolvedResourceCapacity.Resolve(
                    definition,
                    coordinate,
                    capacityInput,
                    instanceAuthorityKey,
                    includeInitialization: true);
                if (!resolved.IsValid || resolved.Capacity == null)
                {
                    issues.AddRange(resolved.Issues);
                    continue;
                }

                using var fingerprint = new ResourceFingerprintBuilder(
                    "resource-owner-materialization-v1");
                fingerprint.Append(export.AuthorityFingerprint);
                fingerprint.Append(candidate.OwnerRef);
                ResourceStateContract.AppendCoordinate(fingerprint, coordinate);
                fingerprint.Append(maximum);
                fingerprint.Append(resolved.Capacity.Binding.AuthorityFingerprint);
                candidateDrafts.Add(new ResourceOwnerCapacityDraft(
                    coordinate,
                    maximum,
                    resolved,
                    new ResourceSourceEvidence(
                        "owner_materialization",
                        candidate.OwnerRef,
                        fingerprint.Build())));
            }

            foreach (var required in candidate.RequiredResourceKeys)
            {
                if (!exactKeys.Contains(required))
                {
                    Add(
                        issues,
                        candidate.Path + ".resourceMaterialization.resources",
                        "resource_owner_materialization_required_resource_missing",
                        "all required resources for the new owner kind",
                        required);
                }
            }
            if (issues.Count == 0)
                result.AddRange(candidateDrafts);
        }
        return result;
    }

    private static bool CanMaterializeCapacity(
        ResourceDefinition definition,
        ResourceOwnerKind ownerKind) =>
        definition.CapacityPolicy.Kind == ResourceCapacityKind.InstanceFixed ||
        definition.CapacityPolicy is
        {
            Kind: ResourceCapacityKind.RegisteredFormula,
            FormulaKey: not null
        } && ResourceCapacityFormulaCatalog.SupportsEveryOwnerKind(
            definition.CapacityPolicy.FormulaKey,
            new[] { ownerKind });

    private static bool TryReadExact(JsonNode? node, out string value)
    {
        value = node is JsonValue jsonValue &&
                jsonValue.TryGetValue<string>(out var text)
            ? text
            : string.Empty;
        return ResourceMaterializationContract.IsExactIdentifier(value);
    }

    private static bool TryReadDecimal(JsonNode? node, out decimal value)
    {
        value = default;
        if (node == null)
            return false;
        try
        {
            using var document = JsonDocument.Parse(node.ToJsonString());
            return document.RootElement.ValueKind == JsonValueKind.Number &&
                   document.RootElement.TryGetDecimal(out value);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Describe(JsonNode? node) =>
        node?.ToJsonString() ?? "missing";

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Resource owner materialization violates exact accepted-turn authority.",
            code: code,
            section: "resource_owner_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Use one exact validated owner identity and one closed resourceMaterialization envelope backed by sealed resource definitions."));
}
