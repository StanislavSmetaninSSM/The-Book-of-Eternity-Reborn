using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class MortalWoundTreatmentContract
{
    private const string ProposalWoundMarkerId = "wound_proposal_local_marker";

    private static JsonElement PrepareDetachedDefinitionForCommonValidation(
        DetachedEffectDefinitionCandidate candidate,
        bool bindProposalWoundMarkers,
        List<ValidationIssue> issues)
    {
        if (!bindProposalWoundMarkers)
            return candidate.Definition.Clone();

        var clone = JsonNode.Parse(candidate.Definition.GetRawText())!.AsObject();
        if (clone["components"] is not JsonArray components)
            return candidate.Definition.Clone();

        for (var index = 0; index < components.Count; index++)
        {
            if (components[index] is not JsonObject component ||
                component["profile"] is not JsonValue profileNode ||
                !profileNode.TryGetValue<string>(out var profile) ||
                !string.Equals(profile, "wound_consequence", StringComparison.Ordinal) ||
                component["payload"] is not JsonObject payload)
            {
                continue;
            }

            var markerPath = $"{candidate.DefinitionPath}.components[{index}].payload.woundId";
            if (payload.ContainsKey("woundId"))
            {
                AddInvalid(
                    issues,
                    markerPath,
                    "no GM-authored woundId; the client binds the selected wound",
                    payload["woundId"]?.ToJsonString() ?? "null");
                continue;
            }
            payload["woundId"] = ProposalWoundMarkerId;
        }

        return JsonSerializer.SerializeToElement(clone);
    }

    private static void ValidateDetachedComplicationGraph(
        IReadOnlyList<DetachedEffectDefinitionCandidate> definitions,
        IReadOnlyList<DetachedEffectRootCandidate> roots,
        string collectionPath,
        string ownerTargetKind,
        int severityRank,
        List<ValidationIssue> issues)
    {
        if (definitions.Count == 0)
            return;
        if (roots.Count == 0)
        {
            AddInvalid(
                issues,
                collectionPath,
                "at least one direct root for every effectful complication graph",
                "definitions without roots");
            return;
        }

        var graph = BuildDetachedGraph(definitions);
        var rootKeys = ResolveRootKeys(graph.ByRef, roots, allowRepeatedDefinition: false, issues);
        ValidateComplicationDefinitionEnvelope(
            graph.Definitions,
            graph.ByKey,
            rootKeys,
            ownerTargetKind,
            severityRank,
            issues);
        ValidateDetachedComplicationStaticEnvelope(
            graph,
            roots,
            collectionPath,
            severityRank,
            issues);
        ValidateAllDefinitionsReachable(
            graph.Definitions,
            graph.ByKey,
            rootKeys,
            requireExactSingleInboundForUnbound: true,
            issues);
    }

    private static void ValidateDetachedLegacyGraph(
        IReadOnlyList<DetachedEffectDefinitionCandidate> definitions,
        IReadOnlyList<DetachedEffectRootCandidate> roots,
        string collectionPath,
        string ownerTargetKind,
        List<ValidationIssue> issues)
    {
        if (definitions.Count == 0)
            return;
        if (roots.Count == 0)
        {
            AddInvalid(
                issues,
                collectionPath,
                "at least one application resolving inside the mechanical legacy draft",
                "definitions without applications");
            return;
        }

        var graph = BuildDetachedGraph(definitions);
        ValidateDerivedLegacyBindings(graph.Definitions, ownerTargetKind, issues);
        var rootKeys = ResolveRootKeys(graph.ByRef, roots, allowRepeatedDefinition: true, issues);
        ValidateAllDefinitionsReachable(
            graph.Definitions,
            graph.ByKey,
            rootKeys,
            requireExactSingleInboundForUnbound: false,
            issues);
    }

    private static void ValidateDerivedLegacyBindings(
        IReadOnlyList<DetachedGraphDefinition> definitions,
        string ownerTargetKind,
        List<ValidationIssue> issues)
    {
        foreach (var definition in definitions)
        {
            var value = definition.Candidate.Definition;
            var path = definition.Candidate.DefinitionPath;
            if (!GraphArrayContains(value, "allowedTargetKinds", ownerTargetKind))
            {
                AddInvalid(
                    issues,
                    path + ".allowedTargetKinds",
                    "array containing the wound owner's exact derived effect target kind",
                    ownerTargetKind);
            }

            if (!value.TryGetProperty("lifetime", out var lifetime) ||
                lifetime.ValueKind != JsonValueKind.Object ||
                !string.Equals(
                    GraphString(lifetime, "mode"),
                    "source_bound",
                    StringComparison.Ordinal))
            {
                continue;
            }

            var activePredicate = GraphString(lifetime, "activePredicate");
            if (EffectSourcePredicateCatalog.IsRegistered(activePredicate) &&
                !EffectSourcePredicateCatalog.IsAllowedForSource(
                    "wound_legacy",
                    activePredicate))
            {
                AddInvalid(
                    issues,
                    path + ".lifetime.activePredicate",
                    "registered active predicate compatible with the derived wound_legacy source",
                    activePredicate);
            }
        }
    }

    private static DetachedGraph BuildDetachedGraph(
        IReadOnlyList<DetachedEffectDefinitionCandidate> candidates)
    {
        var definitions = new List<DetachedGraphDefinition>(candidates.Count);
        var byRef = new Dictionary<string, DetachedGraphDefinition>(StringComparer.Ordinal);
        var byKey = new Dictionary<string, DetachedGraphDefinition>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            var key = GraphString(candidate.Definition, "definitionKey");
            var profiles = new List<DetachedGraphProfile>();
            var edges = new List<DetachedGraphEdge>();
            if (candidate.Definition.TryGetProperty("components", out var components) &&
                components.ValueKind == JsonValueKind.Array)
            {
                var componentIndex = 0;
                foreach (var component in components.EnumerateArray())
                {
                    var componentPath =
                        $"{candidate.DefinitionPath}.components[{componentIndex++}]";
                    var profile = GraphString(component, "profile");
                    profiles.Add(new DetachedGraphProfile(profile, componentPath, component));
                    if (!string.Equals(profile, "event_reaction", StringComparison.Ordinal) ||
                        !component.TryGetProperty("payload", out var payload) ||
                        payload.ValueKind != JsonValueKind.Object ||
                        !string.Equals(
                            GraphString(payload, "resultKind"),
                            "apply_definition",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int? maximumExpansion = null;
                    if (payload.TryGetProperty("maxExpansion", out var rawMaximum) &&
                        rawMaximum.ValueKind == JsonValueKind.Number &&
                        rawMaximum.TryGetInt32(out var parsedMaximum))
                    {
                        maximumExpansion = parsedMaximum;
                    }
                    edges.Add(new DetachedGraphEdge(
                        GraphString(payload, "definitionKey"),
                        GraphString(component, "componentId"),
                        componentPath + ".payload",
                        maximumExpansion,
                        payload.TryGetProperty("parameters", out var parameters)
                            ? parameters.Clone()
                            : default));
                }
            }
            var definition = new DetachedGraphDefinition(
                candidate,
                key,
                profiles,
                edges);
            definitions.Add(definition);
            if (candidate.DefinitionRef.Length > 0 && !byRef.ContainsKey(candidate.DefinitionRef))
                byRef.Add(candidate.DefinitionRef, definition);
            if (key.Length > 0 && !byKey.ContainsKey(key))
                byKey.Add(key, definition);
        }
        return new DetachedGraph(definitions, byRef, byKey);
    }

    private static HashSet<string> ResolveRootKeys(
        IReadOnlyDictionary<string, DetachedGraphDefinition> definitionsByRef,
        IReadOnlyList<DetachedEffectRootCandidate> roots,
        bool allowRepeatedDefinition,
        List<ValidationIssue> issues)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            if (!definitionsByRef.TryGetValue(root.DefinitionRef, out var definition))
            {
                AddInvalid(
                    issues,
                    root.RootPath,
                    "root/application definitionRef resolving inside the same draft",
                    root.DefinitionRef);
                continue;
            }
            if (!allowRepeatedDefinition && !keys.Add(definition.DefinitionKey))
            {
                AddInvalid(
                    issues,
                    root.RootPath,
                    "one direct root per exact definition in a wound-owned graph",
                    definition.DefinitionKey);
                continue;
            }
            keys.Add(definition.DefinitionKey);
        }
        return keys;
    }

    private static void ValidateComplicationDefinitionEnvelope(
        IReadOnlyList<DetachedGraphDefinition> definitions,
        IReadOnlyDictionary<string, DetachedGraphDefinition> definitionsByKey,
        IReadOnlySet<string> rootKeys,
        string ownerTargetKind,
        int severityRank,
        List<ValidationIssue> issues)
    {
        var markerCount = 0;
        var exactStackKeys = new HashSet<string>(StringComparer.Ordinal);
        var confusableStackKeys = new HashSet<string>(StringComparer.Ordinal);
        var edges = definitions.SelectMany(static definition => definition.Edges).ToArray();
        for (var edgeIndex = 1; edgeIndex < edges.Length; edgeIndex++)
        {
            AddInvalid(
                issues,
                edges[edgeIndex].Path + ".definitionKey",
                "at most one wound-owned apply_definition edge",
                edges[edgeIndex].TargetDefinitionKey);
        }

        foreach (var definition in definitions)
        {
            ValidateWoundOwnedDefinitionPolicy(
                definition,
                ownerTargetKind,
                exactStackKeys,
                confusableStackKeys,
                issues);
            if (rootKeys.Contains(definition.DefinitionKey) &&
                (!definition.Candidate.Definition.TryGetProperty(
                     "parameterBounds",
                     out var parameterBounds) ||
                 parameterBounds.ValueKind != JsonValueKind.Object ||
                 parameterBounds.EnumerateObject().Any()))
            {
                AddInvalid(
                    issues,
                    definition.Candidate.DefinitionPath + ".parameterBounds",
                    "exact empty parameterBounds for an implicitly parameterless root",
                    definition.Candidate.Definition.TryGetProperty(
                        "parameterBounds",
                        out var actualBounds)
                        ? actualBounds.GetRawText()
                        : "missing");
            }

            foreach (var profile in definition.Profiles)
            {
                var allowed = WoundConsequenceEnvelopeCatalog.MortalMechanicalProfiles.Contains(
                                  profile.Profile) ||
                              WoundConsequenceEnvelopeCatalog.MortalZeroSlotProfiles.Contains(
                                  profile.Profile);
                if (!allowed)
                {
                    AddInvalid(
                        issues,
                        profile.Path + ".profile",
                        "registered Mortal wound-consequence profile",
                        profile.Profile);
                }
                if (!string.Equals(
                        profile.Profile,
                        "wound_consequence",
                        StringComparison.Ordinal))
                {
                    continue;
                }
                markerCount++;
                if (markerCount > 1)
                {
                    AddInvalid(
                        issues,
                        profile.Path + ".profile",
                        "at most one wound_consequence marker in the complete graph",
                        markerCount.ToString(CultureInfo.InvariantCulture));
                }
            }

            foreach (var edge in definition.Edges)
            {
                if (edge.MaximumExpansion != 2)
                {
                    AddInvalid(
                        issues,
                        edge.Path + ".maxExpansion",
                        "exact integer 2 for a wound-owned reaction and leaf",
                        edge.MaximumExpansion?.ToString(CultureInfo.InvariantCulture) ?? "missing");
                }
                if (severityRank < 3)
                {
                    AddInvalid(
                        issues,
                        edge.Path + ".definitionKey",
                        "wound-owned apply_definition only at severity III or IV",
                        severityRank.ToString(CultureInfo.InvariantCulture));
                }
                if (rootKeys.Contains(edge.TargetDefinitionKey) &&
                    definitionsByKey.TryGetValue(edge.TargetDefinitionKey, out var target))
                {
                    var targetPolicy = GraphNestedString(
                        target.Candidate.Definition,
                        "stacking",
                        "policy");
                    if (!string.Equals(targetPolicy, "replace", StringComparison.Ordinal))
                    {
                        AddInvalid(
                            issues,
                            target.Candidate.DefinitionPath + ".stacking.policy",
                            "replace for a root-bound wound reaction target",
                            targetPolicy.Length == 0 ? "missing" : targetPolicy);
                    }
                    if (!GraphIsEmptyObject(edge.Parameters))
                    {
                        AddInvalid(
                            issues,
                            edge.Path + ".parameters",
                            "exact empty parameters for a root-bound wound reaction target",
                            edge.Parameters.ValueKind == JsonValueKind.Undefined
                                ? "missing"
                                : edge.Parameters.GetRawText());
                    }
                }
            }
        }
    }

    private static void ValidateDetachedComplicationStaticEnvelope(
        DetachedGraph graph,
        IReadOnlyList<DetachedEffectRootCandidate> roots,
        string collectionPath,
        int severityRank,
        List<ValidationIssue> issues)
    {
        var definitions = graph.Definitions
            .Select(static definition => new WoundPersistedConsequenceDefinition(
                definition.Candidate.DefinitionRef,
                definition.Candidate.DefinitionPath,
                definition.Candidate.Definition.Clone()))
            .ToArray();
        var persistedRoots = roots.Select(root =>
            new WoundPersistedConsequenceRoot(
                root.DefinitionRef,
                root.DefinitionRef,
                root.RootPath,
                root.Slots?.Select((slot, index) => new WoundEffectSlotAgreement(
                    index + 1,
                    slot.ProfileKey,
                    string.Empty)).ToArray(),
                root.Slots?.Select(static slot => slot.Path).ToArray()))
            .ToArray();
        var validation = WoundPersistedConsequenceEnvelopeAdapter.ValidateDetached(
            severityRank,
            collectionPath,
            definitions,
            persistedRoots,
            requireExactGlobalSlotAgreement: false);
        foreach (var issue in validation.Issues)
        {
            AddInvalid(
                issues,
                issue.FilePath,
                issue.Expected ?? "valid detached Mortal wound component envelope",
                issue.Actual ?? issue.Code ?? "invalid");
        }
    }

    private static void ValidateWoundOwnedDefinitionPolicy(
        DetachedGraphDefinition definition,
        string ownerTargetKind,
        HashSet<string> exactStackKeys,
        HashSet<string> confusableStackKeys,
        List<ValidationIssue> issues)
    {
        var element = definition.Candidate.Definition;
        var path = definition.Candidate.DefinitionPath;
        if (element.TryGetProperty("stacking", out var stacking) &&
            stacking.ValueKind == JsonValueKind.Object)
        {
            var stackKey = GraphString(stacking, "stackKey");
            if (stackKey.Length > 0 &&
                (!exactStackKeys.Add(stackKey) ||
                 !confusableStackKeys.Add(ExactIdentifierConfusableKey.Build(stackKey))))
            {
                AddInvalid(
                    issues,
                    path + ".stacking.stackKey",
                    "exact and Unicode-confusable unique wound-owned stackKey",
                    stackKey);
            }

            if (!stacking.TryGetProperty("maxStacks", out var maxStacks) ||
                maxStacks.ValueKind != JsonValueKind.Number ||
                !maxStacks.TryGetInt32(out var maximum) ||
                maximum != 1)
            {
                AddInvalid(
                    issues,
                    path + ".stacking.maxStacks",
                    "exact integer 1 for a wound-owned definition",
                    stacking.TryGetProperty("maxStacks", out var actualMaximum)
                        ? actualMaximum.GetRawText()
                        : "missing");
            }

            var policy = GraphString(stacking, "policy");
            var atMaximum = GraphString(stacking, "atMaximum");
            if (string.Equals(policy, "independent", StringComparison.Ordinal) &&
                !string.Equals(atMaximum, "no_change", StringComparison.Ordinal))
            {
                AddInvalid(
                    issues,
                    path + ".stacking.atMaximum",
                    "no_change for an independent wound-owned definition",
                    atMaximum.Length == 0 ? "missing" : atMaximum);
            }
            ValidateIrrelevantStackingField(
                stacking,
                path,
                policy,
                expectedPolicy: "refresh",
                field: "refreshMode",
                issues);
            ValidateIrrelevantStackingField(
                stacking,
                path,
                policy,
                expectedPolicy: "merge",
                field: "mergeRule",
                issues);
        }

        if (!GraphArrayContains(element, "allowedTargetKinds", ownerTargetKind))
        {
            AddInvalid(
                issues,
                path + ".allowedTargetKinds",
                "array containing the wound owner's exact effect target kind",
                ownerTargetKind);
        }

        if (element.TryGetProperty("lifetime", out var lifetime) &&
            lifetime.ValueKind == JsonValueKind.Object &&
            string.Equals(GraphString(lifetime, "mode"), "source_bound", StringComparison.Ordinal))
        {
            var activePredicate = GraphString(lifetime, "activePredicate");
            if (EffectSourcePredicateCatalog.IsRegistered(activePredicate) &&
                !EffectSourcePredicateCatalog.IsAllowedForSource("wound", activePredicate))
            {
                AddInvalid(
                    issues,
                    path + ".lifetime.activePredicate",
                    "registered active predicate compatible with the exact wound source kind",
                    activePredicate);
            }
        }
    }

    private static void ValidateIrrelevantStackingField(
        JsonElement stacking,
        string definitionPath,
        string actualPolicy,
        string expectedPolicy,
        string field,
        List<ValidationIssue> issues)
    {
        if (string.Equals(actualPolicy, expectedPolicy, StringComparison.Ordinal) ||
            !stacking.TryGetProperty(field, out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return;
        }
        AddInvalid(
            issues,
            definitionPath + ".stacking." + field,
            $"{field} omitted unless policy is {expectedPolicy}",
            value.GetRawText());
    }

    private static void ValidateAllDefinitionsReachable(
        IReadOnlyList<DetachedGraphDefinition> definitions,
        IReadOnlyDictionary<string, DetachedGraphDefinition> definitionsByKey,
        IReadOnlySet<string> rootKeys,
        bool requireExactSingleInboundForUnbound,
        List<ValidationIssue> issues)
    {
        var reachable = new HashSet<string>(rootKeys, StringComparer.Ordinal);
        var pending = new Queue<string>(rootKeys);
        var inbound = definitionsByKey.Keys.ToDictionary(
            static key => key,
            static _ => 0,
            StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            foreach (var edge in definition.Edges)
            {
                if (inbound.ContainsKey(edge.TargetDefinitionKey))
                    inbound[edge.TargetDefinitionKey]++;
                if (requireExactSingleInboundForUnbound &&
                    definitionsByKey.TryGetValue(edge.TargetDefinitionKey, out var target) &&
                    target.Edges.Count != 0)
                {
                    AddInvalid(
                        issues,
                        target.Edges[0].Path + ".definitionKey",
                        "no nested wound-owned apply_definition edge",
                        target.Edges[0].TargetDefinitionKey);
                }
            }
        }

        while (pending.Count != 0)
        {
            var key = pending.Dequeue();
            if (!definitionsByKey.TryGetValue(key, out var definition))
                continue;
            foreach (var edge in definition.Edges)
            {
                if (reachable.Add(edge.TargetDefinitionKey))
                    pending.Enqueue(edge.TargetDefinitionKey);
            }
        }

        foreach (var definition in definitions)
        {
            if (!reachable.Contains(definition.DefinitionKey))
            {
                AddInvalid(
                    issues,
                    definition.Candidate.DefinitionPath,
                    "definition reachable from at least one same-draft root/application",
                    definition.DefinitionKey);
                continue;
            }
            if (requireExactSingleInboundForUnbound &&
                !rootKeys.Contains(definition.DefinitionKey) &&
                inbound.GetValueOrDefault(definition.DefinitionKey) != 1)
            {
                AddInvalid(
                    issues,
                    definition.Candidate.DefinitionPath,
                    "unbound wound definition reachable exactly once from a direct root",
                    inbound.GetValueOrDefault(definition.DefinitionKey)
                        .ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    private static string GraphString(JsonElement value, string field) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(field, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static string GraphNestedString(
        JsonElement value,
        string objectField,
        string stringField) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(objectField, out var nested) &&
        nested.ValueKind == JsonValueKind.Object
            ? GraphString(nested, stringField)
            : string.Empty;

    private static bool GraphArrayContains(
        JsonElement value,
        string field,
        string expected) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(field, out var array) &&
        array.ValueKind == JsonValueKind.Array &&
        array.EnumerateArray().Any(item =>
            item.ValueKind == JsonValueKind.String &&
            string.Equals(item.GetString(), expected, StringComparison.Ordinal));

    private static bool GraphIsEmptyObject(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object &&
        !value.EnumerateObject().Any();

    private sealed record DetachedGraph(
        IReadOnlyList<DetachedGraphDefinition> Definitions,
        IReadOnlyDictionary<string, DetachedGraphDefinition> ByRef,
        IReadOnlyDictionary<string, DetachedGraphDefinition> ByKey);

    private sealed record DetachedGraphDefinition(
        DetachedEffectDefinitionCandidate Candidate,
        string DefinitionKey,
        IReadOnlyList<DetachedGraphProfile> Profiles,
        IReadOnlyList<DetachedGraphEdge> Edges);

    private sealed record DetachedGraphProfile(
        string Profile,
        string Path,
        JsonElement Component);

    private sealed record DetachedGraphEdge(
        string TargetDefinitionKey,
        string ComponentId,
        string Path,
        int? MaximumExpansion,
        JsonElement Parameters);
}
