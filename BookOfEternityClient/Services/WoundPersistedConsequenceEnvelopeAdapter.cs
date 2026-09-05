using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed record WoundPersistedConsequenceDefinition(
    string DefinitionRef,
    string AuthorPath,
    JsonElement Definition);

internal sealed record WoundPersistedConsequenceRoot(
    string EffectRef,
    string DefinitionRef,
    string AuthorPath,
    IReadOnlyList<WoundEffectSlotAgreement>? ExpectedSlots,
    IReadOnlyList<string>? ExpectedSlotPaths = null);

internal sealed record WoundPersistedConsequenceEnvelopeValidationResult(
    ImmutableArray<WoundDetachedMortalEnvelopeSlot> DerivedSlots,
    ImmutableArray<ValidationIssue> Issues)
{
    internal bool IsValid => Issues.IsEmpty;
}

/// <summary>
/// Reconstructs the severity-aware detached component envelope from a complete
/// persisted/direct-root graph. This is shared by authored treatment graph
/// validation and lower-severity projection so their component-power verdicts
/// cannot drift.
/// </summary>
internal static class WoundPersistedConsequenceEnvelopeAdapter
{
    internal static WoundPersistedConsequenceEnvelopeValidationResult ValidateDetached(
        int severityRank,
        string authorPath,
        IReadOnlyList<WoundPersistedConsequenceDefinition> definitions,
        IReadOnlyList<WoundPersistedConsequenceRoot> roots,
        bool requireExactGlobalSlotAgreement,
        int? persistedSlotsUsed = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorPath);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(roots);

        var graph = BuildGraph(definitions);
        var issues = new List<ValidationIssue>();
        var exactRefs = new HashSet<string>(StringComparer.Ordinal);
        var confusableRefs = new HashSet<string>(StringComparer.Ordinal);
        var exactKeys = new HashSet<string>(StringComparer.Ordinal);
        var confusableKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            var key = ReadString(definition.Definition, "definitionKey");
            if (!ResourceMaterializationContract.IsExactIdentifier(definition.DefinitionRef) ||
                !exactRefs.Add(definition.DefinitionRef) ||
                !confusableRefs.Add(Confusable(definition.DefinitionRef)))
            {
                AddGraphIssue(
                    issues,
                    definition.AuthorPath,
                    "one exact/confusable-unique definition reference",
                    definition.DefinitionRef);
            }
            if (!ResourceMaterializationContract.IsExactIdentifier(key) ||
                !exactKeys.Add(key) ||
                !confusableKeys.Add(Confusable(key)))
            {
                AddGraphIssue(
                    issues,
                    definition.AuthorPath + ".definitionKey",
                    "one exact/confusable-unique definitionKey",
                    key.Length == 0 ? "missing or invalid" : key);
            }
        }

        var directKeys = new HashSet<string>(StringComparer.Ordinal);
        var exactEffectRefs = new HashSet<string>(StringComparer.Ordinal);
        var confusableEffectRefs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            if (!ResourceMaterializationContract.IsExactIdentifier(root.EffectRef) ||
                !exactEffectRefs.Add(root.EffectRef) ||
                !confusableEffectRefs.Add(Confusable(root.EffectRef)))
            {
                AddBindingIssue(
                    issues,
                    root.AuthorPath,
                    "one exact/confusable-unique direct-root effect reference",
                    root.EffectRef);
            }
            if (!graph.ByRef.TryGetValue(root.DefinitionRef, out var definition))
            {
                AddGraphIssue(
                    issues,
                    root.AuthorPath + ".definitionRef",
                    "definitionRef resolving to one definition in this graph",
                    root.DefinitionRef);
                continue;
            }
            if (!directKeys.Add(definition.DefinitionKey))
            {
                AddBindingIssue(
                    issues,
                    root.AuthorPath + ".definitionRef",
                    "one direct root per exact definitionKey",
                    definition.DefinitionKey);
            }
        }

        if (issues.Count != 0)
            return Invalid(issues);

        return ValidateResolvedGraph(
            severityRank,
            authorPath,
            graph,
            roots,
            requireExactGlobalSlotAgreement,
            persistedSlotsUsed);
    }

    /// <summary>
    /// Runs shared catalog traversal and slot reciprocity for a graph whose
    /// owning contract already diagnosed identifiers and root bindings.
    /// Unresolved roots are skipped, but resolvable roots remain independently
    /// eligible for catalog and slot diagnostics.
    /// </summary>
    internal static WoundPersistedConsequenceEnvelopeValidationResult ValidatePrevalidatedDetached(
        int severityRank,
        string authorPath,
        IReadOnlyList<WoundPersistedConsequenceDefinition> definitions,
        IReadOnlyList<WoundPersistedConsequenceRoot> roots,
        bool requireExactGlobalSlotAgreement,
        int? persistedSlotsUsed = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorPath);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(roots);

        return ValidateResolvedGraph(
            severityRank,
            authorPath,
            BuildGraph(definitions),
            roots,
            requireExactGlobalSlotAgreement,
            persistedSlotsUsed);
    }

    private static WoundPersistedConsequenceEnvelopeValidationResult ValidateResolvedGraph(
        int severityRank,
        string authorPath,
        DefinitionGraph graph,
        IReadOnlyList<WoundPersistedConsequenceRoot> roots,
        bool requireExactGlobalSlotAgreement,
        int? persistedSlotsUsed)
    {
        var issues = new List<ValidationIssue>();
        var resolvedRoots = roots
            .Select(root => graph.ByRef.TryGetValue(root.DefinitionRef, out var definition)
                ? new ResolvedRoot(root, definition)
                : null)
            .Where(static root => root is not null)
            .Select(static root => root!)
            .ToArray();
        var directKeys = resolvedRoots
            .Select(static root => root.Definition.DefinitionKey)
            .ToHashSet(StringComparer.Ordinal);

        var effects = new List<WoundDetachedMortalEffectRef>(resolvedRoots.Length);
        foreach (var resolved in resolvedRoots)
        {
            var root = resolved.Root;
            var definition = resolved.Definition;
            var expansions = new List<WoundDetachedMortalReactionExpansionRef>();
            foreach (var edge in definition.Edges)
            {
                IReadOnlyList<JsonElement> childComponents = Array.Empty<JsonElement>();
                var childPath = edge.AuthorPath;
                if (!directKeys.Contains(edge.TargetDefinitionKey) &&
                    graph.ByKey.TryGetValue(edge.TargetDefinitionKey, out var child))
                {
                    childComponents = child.Components;
                    childPath = child.AuthorPath;
                }
                expansions.Add(new WoundDetachedMortalReactionExpansionRef(
                    edge.ComponentId,
                    childPath,
                    childComponents,
                    edge.Parameters));
            }
            effects.Add(new WoundDetachedMortalEffectRef(
                root.EffectRef,
                definition.AuthorPath,
                definition.Components,
                expansions));
        }

        var validation = WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
            new WoundDetachedMortalEnvelopeRequest(
                severityRank,
                authorPath,
                effects));
        issues.AddRange(validation.Issues);

        var resolvedRootValues = resolvedRoots
            .Select(static resolved => resolved.Root)
            .ToArray();
        if (requireExactGlobalSlotAgreement)
        {
            var expectedSlotCount = resolvedRootValues.Sum(
                static root => root.ExpectedSlots?.Count ?? 0);
            if (Math.Max(validation.Slots.Length, expectedSlotCount) > severityRank)
            {
                AddSlotIssue(
                    issues,
                    authorPath,
                    $"at most {severityRank} aggregate consequence slots at severity {severityRank}",
                    Math.Max(validation.Slots.Length, expectedSlotCount)
                        .ToString(CultureInfo.InvariantCulture));
            }
        }

        if (persistedSlotsUsed is not null && persistedSlotsUsed.Value != validation.Slots.Length)
        {
            AddSlotIssue(
                issues,
                authorPath + ".slotsUsed",
                "exact number of severity-derived consequence slots",
                persistedSlotsUsed.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (requireExactGlobalSlotAgreement)
            ValidateGlobalSlotAgreement(
                validation.Slots,
                resolvedRootValues,
                authorPath,
                issues);
        else
            ValidatePrevalidatedPerRootProfileAgreement(
                resolvedRoots,
                directKeys,
                graph,
                authorPath,
                severityRank,
                issues);

        return new WoundPersistedConsequenceEnvelopeValidationResult(
            validation.Slots,
            issues.ToImmutableArray());
    }

    private static DefinitionGraph BuildGraph(
        IReadOnlyList<WoundPersistedConsequenceDefinition> definitions)
    {
        var byRef = new Dictionary<string, DefinitionNode>(StringComparer.Ordinal);
        var byKey = new Dictionary<string, DefinitionNode>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            var key = ReadString(definition.Definition, "definitionKey");
            var node = BuildDefinitionNode(definition, key);
            if (definition.DefinitionRef.Length > 0 && !byRef.ContainsKey(definition.DefinitionRef))
                byRef.Add(definition.DefinitionRef, node);
            if (key.Length > 0 && !byKey.ContainsKey(key))
                byKey.Add(key, node);
        }
        return new DefinitionGraph(byRef, byKey);
    }

    private static void ValidateGlobalSlotAgreement(
        ImmutableArray<WoundDetachedMortalEnvelopeSlot> derived,
        IReadOnlyList<WoundPersistedConsequenceRoot> roots,
        string authorPath,
        List<ValidationIssue> issues)
    {
        var expected = roots
            .SelectMany(static root => (root.ExpectedSlots ?? Array.Empty<WoundEffectSlotAgreement>())
                .Select(slot => (root.EffectRef, Slot: slot)))
            .OrderBy(static row => row.Slot.Slot)
            .ToArray();
        if (expected.Length != derived.Length)
        {
            AddSlotIssue(
                issues,
                authorPath + ".entries",
                "one persisted consequence entry for every derived slot",
                expected.Length.ToString(CultureInfo.InvariantCulture));
            return;
        }

        for (var index = 0; index < derived.Length; index++)
        {
            var actual = expected[index];
            var calculated = derived[index];
            if (actual.Slot.Slot == calculated.Slot &&
                string.Equals(actual.EffectRef, calculated.EffectRef, StringComparison.Ordinal) &&
                string.Equals(
                    actual.Slot.ProfileKey,
                    calculated.ProfileKey,
                    StringComparison.Ordinal))
            {
                continue;
            }
            AddSlotIssue(
                issues,
                authorPath + $".entries[{index}]",
                "exact derived slot/effect/profile tuple",
                $"{actual.Slot.Slot}:{actual.EffectRef}:{actual.Slot.ProfileKey}");
        }
    }

    private static void ValidatePrevalidatedPerRootProfileAgreement(
        IReadOnlyList<ResolvedRoot> roots,
        IReadOnlySet<string> directKeys,
        DefinitionGraph graph,
        string authorPath,
        int severityRank,
        List<ValidationIssue> issues)
    {
        var derivedSlotCount = 0;
        foreach (var resolved in roots)
        {
            var root = resolved.Root;
            if (root.ExpectedSlots is null)
                continue;
            var remaining = DirectMechanicalProfiles(resolved.Definition);
            foreach (var edge in resolved.Definition.Edges)
            {
                if (!directKeys.Contains(edge.TargetDefinitionKey) &&
                    graph.ByKey.TryGetValue(edge.TargetDefinitionKey, out var child))
                {
                    remaining.AddRange(DirectMechanicalProfiles(child));
                }
            }
            derivedSlotCount += remaining.Count;
            for (var index = 0; index < root.ExpectedSlots.Count; index++)
            {
                var expected = root.ExpectedSlots[index];
                var match = remaining.FindIndex(profile => string.Equals(
                    profile,
                    expected.ProfileKey,
                    StringComparison.Ordinal));
                if (match >= 0)
                {
                    remaining.RemoveAt(match);
                    continue;
                }
                var path = root.ExpectedSlotPaths is not null &&
                           index < root.ExpectedSlotPaths.Count
                    ? root.ExpectedSlotPaths[index]
                    : root.AuthorPath + $".slots[{index}].profileKey";
                AddSlotIssue(
                    issues,
                    path,
                    "profile matching one derived mechanical slot of this root",
                    expected.ProfileKey);
            }
            if (remaining.Count != 0)
            {
                AddSlotIssue(
                    issues,
                    root.AuthorPath + ".slots",
                    "one reciprocal slot for every derived mechanical component",
                    string.Join(",", remaining));
            }
        }

        var maximum = Math.Min(WoundMaterializationContract.MaxConsequences, severityRank);
        if (derivedSlotCount > maximum)
        {
            AddSlotIssue(
                issues,
                authorPath,
                $"at most {maximum} aggregate consequence slots at severity {severityRank}",
                derivedSlotCount.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static List<string> DirectMechanicalProfiles(DefinitionNode definition)
    {
        var profiles = new List<string>();
        foreach (var component in definition.Components)
        {
            var profile = ReadString(component, "profile");
            if (string.Equals(profile, "wound_consequence", StringComparison.Ordinal))
                continue;
            profiles.Add(profile);
        }
        return profiles;
    }

    private static DefinitionNode BuildDefinitionNode(
        WoundPersistedConsequenceDefinition definition,
        string key)
    {
        var components = ImmutableArray.CreateBuilder<JsonElement>();
        var edges = ImmutableArray.CreateBuilder<ApplyEdge>();
        if (definition.Definition.ValueKind == JsonValueKind.Object &&
            definition.Definition.TryGetProperty("components", out var rawComponents) &&
            rawComponents.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var component in rawComponents.EnumerateArray())
            {
                components.Add(component.Clone());
                var componentPath = $"{definition.AuthorPath}.components[{index++}]";
                if (!string.Equals(
                        ReadString(component, "profile"),
                        "event_reaction",
                        StringComparison.Ordinal) ||
                    !component.TryGetProperty("payload", out var payload) ||
                    payload.ValueKind != JsonValueKind.Object ||
                    !string.Equals(
                        ReadString(payload, "resultKind"),
                        "apply_definition",
                        StringComparison.Ordinal))
                {
                    continue;
                }
                edges.Add(new ApplyEdge(
                    ReadString(payload, "definitionKey"),
                    ReadString(component, "componentId"),
                    componentPath + ".payload",
                    payload.TryGetProperty("parameters", out var parameters)
                        ? parameters.Clone()
                        : default));
            }
        }
        return new DefinitionNode(
            key,
            definition.AuthorPath,
            components.ToImmutable(),
            edges.ToImmutable());
    }

    private static string ReadString(JsonElement value, string field) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(field, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static string Confusable(string value) =>
        ResourceMaterializationContract.BuildConfusableKey(value);

    private static WoundPersistedConsequenceEnvelopeValidationResult Invalid(
        IEnumerable<ValidationIssue> issues) => new(
        ImmutableArray<WoundDetachedMortalEnvelopeSlot>.Empty,
        issues.ToImmutableArray());

    private static void AddGraphIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string expected,
        string actual) => Add(
        issues,
        path,
        "wound_materialization_owned_source_graph_invalid",
        expected,
        actual);

    private static void AddBindingIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string expected,
        string actual) => Add(
        issues,
        path,
        "wound_materialization_effect_binding_invalid",
        expected,
        actual);

    private static void AddSlotIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string expected,
        string actual) => Add(
        issues,
        path,
        "wound_materialization_consequence_slot_invalid",
        expected,
        actual);

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) => issues.Add(new ValidationIssue(
        path,
        IssueSeverity.Error,
        "The persisted wound consequence envelope is invalid.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual,
        repairHint: "Rebuild the complete wound-owned consequence graph from canonical authority."));

    private sealed record DefinitionNode(
        string DefinitionKey,
        string AuthorPath,
        ImmutableArray<JsonElement> Components,
        ImmutableArray<ApplyEdge> Edges);

    private sealed record DefinitionGraph(
        IReadOnlyDictionary<string, DefinitionNode> ByRef,
        IReadOnlyDictionary<string, DefinitionNode> ByKey);

    private sealed record ResolvedRoot(
        WoundPersistedConsequenceRoot Root,
        DefinitionNode Definition);

    private sealed record ApplyEdge(
        string TargetDefinitionKey,
        string ComponentId,
        string AuthorPath,
        JsonElement Parameters);
}
