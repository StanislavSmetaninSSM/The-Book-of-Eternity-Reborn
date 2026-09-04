using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record WoundApplicationRootEffectBinding(
    string ApplicationRef,
    string EffectId);

internal sealed record WoundReactionLineageResolution(
    EffectSourceAuthorityEntry? Source,
    WoundRootOwnershipDomain? OwnershipDomain,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success => Source is not null &&
                             OwnershipDomain is not null &&
                             Issues.Count == 0;
}

internal sealed class WoundReactionLineageAuthority
{
    private const int MaximumIssues = 20;
    private readonly EffectSourceAuthority _sourceAuthority;
    private readonly Dictionary<string, LineageIdentity> _identitiesByEffectId;
    private readonly ValidationIssue[] _issues;

    private WoundReactionLineageAuthority(
        EffectSourceAuthority sourceAuthority,
        Dictionary<string, LineageIdentity> identitiesByEffectId,
        IReadOnlyList<ValidationIssue> issues)
    {
        _sourceAuthority = sourceAuthority;
        _identitiesByEffectId = identitiesByEffectId.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DetachedCopy(),
            StringComparer.Ordinal);
        _issues = issues.ToArray();
    }

    internal bool Success => _issues.Length == 0;

    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues.ToArray());

    internal static WoundReactionLineageAuthority Build(
        EffectSourceAuthority sourceAuthority,
        EffectIdentityState identities,
        EffectCarrierCatalog carriers,
        IReadOnlyList<WoundApplicationRootEffectBinding> applicationRootBindings)
    {
        ArgumentNullException.ThrowIfNull(sourceAuthority);
        ArgumentNullException.ThrowIfNull(identities);
        ArgumentNullException.ThrowIfNull(carriers);
        ArgumentNullException.ThrowIfNull(applicationRootBindings);

        var issues = new List<ValidationIssue>();
        AddRange(issues, sourceAuthority.Issues);
        AddRange(issues, carriers.Issues);
        var groups = sourceAuthority.SnapshotWoundGroupAuthorities();
        var groupKeys = groups
            .Select(static group => group.Key)
            .ToHashSet();
        var applicationEffects = ValidateApplicationRootBindings(
            groups,
            applicationRootBindings,
            issues);
        var lineage = new Dictionary<string, LineageIdentity>(StringComparer.Ordinal);
        var claimedRootEffects = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            BuildGroup(
                group,
                sourceAuthority,
                identities,
                carriers,
                applicationEffects,
                claimedRootEffects,
                lineage,
                issues);
        }

        foreach (var occurrence in carriers.Occurrences)
        {
            if (!TryReadSourceGroup(occurrence.Effect, out var group) ||
                !string.Equals(group.Kind, "wound", StringComparison.Ordinal) ||
                !groupKeys.Contains(group))
            {
                continue;
            }
            if (!lineage.ContainsKey(occurrence.EffectId))
            {
                Add(
                    issues,
                    occurrence.JsonPath,
                    "effect_reaction_wound_lineage_unreachable",
                    "Every active wound-source carrier occurrence must belong to one exact current first-create lineage.",
                    "reachable wound lineage identity",
                    occurrence.EffectId);
            }
        }

        return new WoundReactionLineageAuthority(
            sourceAuthority,
            lineage,
            issues);
    }

    internal WoundReactionLineageResolution ResolveApplyDefinition(
        EffectCarrierOccurrence producer,
        JsonObject component,
        EffectTargetKey target,
        string downstreamDefinitionKey)
    {
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(target);

        var issues = new List<ValidationIssue>();
        if (!Success)
        {
            AddRange(issues, _issues);
            return Failed(issues);
        }
        if (!_identitiesByEffectId.TryGetValue(
                producer.EffectId,
                out var producerIdentity) ||
            producerIdentity.Occurrence is null ||
            !OccurrenceEquals(producerIdentity.Occurrence, producer) ||
            producerIdentity.Group.Target != target)
        {
            Add(
                issues,
                producer.JsonPath,
                "effect_reaction_wound_lineage_producer_invalid",
                "A wound reaction producer must be the exact active carrier occurrence in one sealed current wound lineage.",
                target.ToString(),
                producer.EffectId);
            return Failed(issues);
        }

        if (!ResourceMaterializationContract.IsExactIdentifier(
                downstreamDefinitionKey) ||
            !producerIdentity.Group.Definitions.TryGetValue(
                producerIdentity.DefinitionKey,
                out var producerDefinition) ||
            !producerIdentity.Group.Definitions.TryGetValue(
                downstreamDefinitionKey,
                out _) ||
            !producerIdentity.Group.Edges.TryGetValue(
                producerIdentity.DefinitionKey,
                out var targets) ||
            !targets.Contains(downstreamDefinitionKey) ||
            !ComponentMatchesExactEdge(
                component,
                producerDefinition,
                downstreamDefinitionKey))
        {
            Add(
                issues,
                producer.JsonPath + ".components",
                "effect_reaction_wound_lineage_edge_invalid",
                "The selected component must be the exact persisted apply_definition edge of its causal wound producer.",
                producerIdentity.Group.Edges.TryGetValue(
                    producerIdentity.DefinitionKey,
                    out var expectedTargets)
                    ? string.Join(",", expectedTargets)
                    : "no edge",
                downstreamDefinitionKey);
            return Failed(issues);
        }

        if (!producerIdentity.Group.DefinitionDomains.TryGetValue(
                downstreamDefinitionKey,
                out var downstreamDomain) ||
            downstreamDomain != producerIdentity.OwnershipDomain)
        {
            Add(
                issues,
                producer.JsonPath + ".components",
                "effect_reaction_wound_lineage_cross_domain",
                "A wound reaction descendant must remain in the exact ownership domain inherited from its causal producer.",
                DescribeDomain(producerIdentity.OwnershipDomain),
                downstreamDomain is null
                    ? "unresolved"
                    : DescribeDomain(downstreamDomain));
            return Failed(issues);
        }

        var sourceKey = new EffectSourceKey(
            producerIdentity.Group.Authority.Key.Realm,
            "wound",
            producerIdentity.Group.Authority.Key.SourceId,
            downstreamDefinitionKey);
        var resolution = _sourceAuthority.ResolveCanonicalBinding(
            sourceKey,
            target.Kind);
        AddRange(issues, resolution.Issues);
        if (!resolution.Success ||
            resolution.Source is null ||
            resolution.Source.Materializable ||
            !resolution.Source.Active ||
            !string.Equals(
                resolution.Source.Key.Realm,
                target.Realm,
                StringComparison.Ordinal))
        {
            if (issues.Count == 0)
            {
                Add(
                    issues,
                    producer.JsonPath + ".components",
                    "effect_reaction_wound_lineage_source_invalid",
                    "One active non-materializable downstream definition from the same sealed wound source graph.",
                    sourceKey.ToString(),
                    resolution.Source?.Key.ToString() ?? "missing");
            }
            return Failed(issues);
        }

        return new WoundReactionLineageResolution(
            resolution.Source,
            new WoundRootOwnershipDomain(
                downstreamDomain.Kind,
                downstreamDomain.ComplicationId),
            Array.Empty<ValidationIssue>());
    }

    internal IReadOnlyList<ValidationIssue> ValidateExistingOccupantDomain(
        string effectId,
        WoundRootOwnershipDomain expectedDomain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        ArgumentNullException.ThrowIfNull(expectedDomain);
        if (!Success)
            return Issues;
        if (_identitiesByEffectId.TryGetValue(effectId, out var identity) &&
            identity.OwnershipDomain == expectedDomain)
        {
            return Array.Empty<ValidationIssue>();
        }
        return new[]
        {
            Issue(
                "effect.reactions",
                "effect_reaction_wound_lineage_cross_domain",
                "An existing wound stack occupant must belong to the exact ownership domain of the causal reaction producer before mutation.",
                DescribeDomain(expectedDomain),
                identity is null
                    ? effectId + "/unresolved"
                    : effectId + "/" + DescribeDomain(identity.OwnershipDomain))
        };
    }

    private static void BuildGroup(
        WoundSourceGroupAuthority group,
        EffectSourceAuthority sourceAuthority,
        EffectIdentityState identities,
        EffectCarrierCatalog carriers,
        IReadOnlyDictionary<string, string> applicationEffects,
        ISet<string> claimedRootEffects,
        IDictionary<string, LineageIdentity> lineage,
        List<ValidationIssue> issues)
    {
        var definitions = group.Definitions.ToDictionary(
            static definition => definition.DefinitionKey,
            static definition => definition.Definition.DeepClone().AsObject(),
            StringComparer.Ordinal);
        var edges = BuildEdges(definitions, group.Key, issues);
        var roots = ResolveRoots(
            group,
            applicationEffects,
            claimedRootEffects,
            issues);
        var definitionDomains = BuildDefinitionDomains(
            group,
            definitions,
            edges,
            roots,
            issues);
        var groupState = new LineageGroup(
            group.DetachedCopy(),
            definitions,
            edges,
            definitionDomains);
        var analyzerDefinitions = definitionDomains
            .Where(pair => definitions.ContainsKey(pair.Key))
            .ToDictionary(
                static pair => pair.Key,
                pair => new WoundEffectIdentityLineageDefinition(
                    pair.Key,
                    edges.TryGetValue(pair.Key, out var targets)
                        ? targets
                        : new HashSet<string>(StringComparer.Ordinal),
                    pair.Value),
                StringComparer.Ordinal);
        var analyzerRoots = roots
            .Select(pair => new WoundEffectIdentityLineageRoot(
                pair.Key,
                pair.Value.Row.DefinitionKey,
                pair.Value.Row.OwnershipDomain))
            .ToArray();
        var analysis = WoundEffectIdentityLineageAnalyzer.Analyze(
            group.Key,
            identities,
            analyzerRoots,
            analyzerDefinitions,
            WoundEffectLineageDiagnosticProfile.Reaction);
        AddRange(issues, analysis.Issues);

        foreach (var entry in analysis.RetiredEntries)
        {
            var definitionKey = ReadDefinitionKey(entry);
            if (definitions.TryGetValue(definitionKey, out var definition))
            {
                ValidateIdentityAuthority(
                    group,
                    entry,
                    definition,
                    carriers,
                    issues,
                    out _);
            }
        }

        foreach (var entry in analysis.CurrentEntries)
        {
            var definitionKey = ReadDefinitionKey(entry);
            if (!definitions.TryGetValue(definitionKey, out var definition) ||
                !analysis.CurrentOwnershipByEffectId.TryGetValue(
                    entry.EffectId,
                    out var ownershipDomain))
            {
                continue;
            }
            ValidateIdentityAuthority(
                group,
                entry,
                definition,
                carriers,
                issues,
                out var occurrence);
            if (!lineage.TryAdd(
                    entry.EffectId,
                    new LineageIdentity(
                        groupState,
                        entry.EffectId,
                        definitionKey,
                        ownershipDomain,
                        occurrence)))
            {
                Add(
                    issues,
                    "effect.reactions.woundLineage",
                    "effect_reaction_wound_lineage_ambiguous",
                    "An effect identity may belong to only one sealed wound source group.",
                    "one source group",
                    entry.EffectId);
            }
        }

        foreach (var definitionKey in definitions.Keys)
        {
            var key = new EffectSourceKey(
                group.Key.Realm,
                group.Key.Kind,
                group.Key.SourceId,
                definitionKey);
            var resolution = sourceAuthority.ResolveCanonicalBinding(
                key,
                group.Target.Kind);
            if (!resolution.Success || resolution.Source is null ||
                resolution.Source.Materializable || !resolution.Source.Active)
            {
                Add(
                    issues,
                    "effect.reactions.woundLineage.definitions",
                    "effect_reaction_wound_lineage_source_invalid",
                    "Every graph definition must resolve as one active non-materializable canonical source binding.",
                    key.ToString(),
                    resolution.Source?.Key.ToString() ?? "missing");
            }
        }
    }

    private static Dictionary<string, string> ValidateApplicationRootBindings(
        IReadOnlyList<WoundSourceGroupAuthority> groups,
        IReadOnlyList<WoundApplicationRootEffectBinding> bindings,
        List<ValidationIssue> issues)
    {
        var expected = groups.SelectMany(static group => group.ApplicationRootLineage)
            .Select(static row => row.ApplicationRef)
            .Where(static value => value is not null)
            .Select(static value => value!)
            .ToHashSet(StringComparer.Ordinal);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var applicationAliases = new HashSet<string>(StringComparer.Ordinal);
        var effectIds = new HashSet<string>(StringComparer.Ordinal);
        var effectAliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            if (binding is null ||
                !ResourceMaterializationContract.IsExactIdentifier(
                    binding.ApplicationRef) ||
                !ResourceMaterializationContract.IsExactIdentifier(binding.EffectId) ||
                !result.TryAdd(binding.ApplicationRef, binding.EffectId) ||
                !applicationAliases.Add(MortalLocationIdentityState.BuildConfusableKey(
                    binding.ApplicationRef)) ||
                !effectIds.Add(binding.EffectId) ||
                !effectAliases.Add(MortalLocationIdentityState.BuildConfusableKey(
                    binding.EffectId)))
            {
                Add(
                    issues,
                    "effect.reactions.woundRootBindings",
                    "effect_reaction_wound_root_binding_invalid",
                    "Unique exact/confusable applicationRef-to-effectId bindings.",
                    string.Join(",", expected.OrderBy(static value => value, StringComparer.Ordinal)),
                    binding?.ApplicationRef + "/" + binding?.EffectId);
            }
        }
        if (!expected.SetEquals(result.Keys))
        {
            Add(
                issues,
                "effect.reactions.woundRootBindings",
                "effect_reaction_wound_root_binding_invalid",
                "The exact complete set of same-turn wound root application results.",
                string.Join(",", expected.OrderBy(static value => value, StringComparer.Ordinal)),
                string.Join(",", result.Keys.OrderBy(static value => value, StringComparer.Ordinal)));
        }
        return result;
    }

    private static Dictionary<string, ResolvedRoot> ResolveRoots(
        WoundSourceGroupAuthority group,
        IReadOnlyDictionary<string, string> applicationEffects,
        ISet<string> claimedRootEffects,
        List<ValidationIssue> issues)
    {
        var result = new Dictionary<string, ResolvedRoot>(StringComparer.Ordinal);
        foreach (var row in group.ApplicationRootLineage.Concat(
                     group.ExistingRootLineage))
        {
            var effectId = row.ApplicationRef is null
                ? row.EffectId
                : applicationEffects.TryGetValue(row.ApplicationRef, out var allocated)
                    ? allocated
                    : null;
            if (!ResourceMaterializationContract.IsExactIdentifier(effectId) ||
                !claimedRootEffects.Add(effectId!) ||
                !result.TryAdd(effectId!, new ResolvedRoot(row)))
            {
                Add(
                    issues,
                    "effect.reactions.woundRootBindings",
                    "effect_reaction_wound_root_binding_invalid",
                    "Every sealed root selector must resolve to one globally unique exact effect identity.",
                    row.ApplicationRef ?? row.EffectId ?? "missing",
                    effectId ?? "missing");
            }
        }
        return result;
    }

    private static Dictionary<string, HashSet<string>> BuildEdges(
        IReadOnlyDictionary<string, JsonObject> definitions,
        EffectIdentitySourceGroup group,
        List<ValidationIssue> issues)
    {
        var result = definitions.Keys.ToDictionary(
            static key => key,
            static _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        foreach (var pair in definitions)
        {
            if (pair.Value["components"] is not JsonArray components)
                continue;
            foreach (var component in components.OfType<JsonObject>())
            {
                if (!HasExact(component, "profile", "event_reaction") ||
                    component["payload"] is not JsonObject payload ||
                    !HasExact(payload, "resultKind", "apply_definition"))
                {
                    continue;
                }
                if (!TryReadExact(payload["definitionKey"], out var target) ||
                    !definitions.ContainsKey(target) ||
                    !result[pair.Key].Add(target))
                {
                    Add(
                        issues,
                        "effect.reactions.woundLineage.definitions",
                        "effect_reaction_wound_lineage_edge_invalid",
                        "Every wound apply_definition edge must target one exact definition in the same sealed graph.",
                        DescribeGroup(group),
                        pair.Key + "/" + (target ?? "missing"));
                }
            }
        }
        return result;
    }

    private static Dictionary<string, WoundRootOwnershipDomain>
        BuildDefinitionDomains(
            WoundSourceGroupAuthority group,
            IReadOnlyDictionary<string, JsonObject> definitions,
            IReadOnlyDictionary<string, HashSet<string>> edges,
            IReadOnlyDictionary<string, ResolvedRoot> roots,
            List<ValidationIssue> issues)
    {
        var result = new Dictionary<string, WoundRootOwnershipDomain>(
            StringComparer.Ordinal);
        var ambiguous = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in roots.Values)
        {
            var queue = new Queue<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            queue.Enqueue(root.Row.DefinitionKey);
            while (queue.Count != 0)
            {
                var definitionKey = queue.Dequeue();
                if (!visited.Add(definitionKey) ||
                    !definitions.ContainsKey(definitionKey))
                {
                    continue;
                }
                if (result.TryGetValue(definitionKey, out var existing) &&
                    existing != root.Row.OwnershipDomain)
                {
                    if (ambiguous.Add(definitionKey))
                    {
                        Add(
                            issues,
                            "effect.reactions.woundLineage.definitionDomains",
                            "effect_reaction_wound_lineage_cross_domain",
                            "Every reachable wound definition must belong to one exact root ownership domain.",
                            DescribeDomain(existing),
                            DescribeDomain(root.Row.OwnershipDomain) + "/" +
                            definitionKey);
                    }
                }
                else
                {
                    result[definitionKey] = root.Row.OwnershipDomain;
                }
                if (edges.TryGetValue(definitionKey, out var targets))
                {
                    foreach (var target in targets)
                        queue.Enqueue(target);
                }
            }
        }
        foreach (var definitionKey in ambiguous)
            result.Remove(definitionKey);
        foreach (var definitionKey in definitions.Keys)
        {
            if (!result.ContainsKey(definitionKey))
            {
                Add(
                    issues,
                    "effect.reactions.woundLineage.definitionDomains",
                    "effect_reaction_wound_lineage_unreachable",
                    "Every current wound definition must be reachable from one sealed direct root.",
                    DescribeGroup(group.Key),
                    definitionKey);
            }
        }
        return result;
    }

    private static void ValidateIdentityAuthority(
        WoundSourceGroupAuthority group,
        EffectIdentityEntry identity,
        JsonObject definition,
        EffectCarrierCatalog carriers,
        List<ValidationIssue> issues,
        out EffectCarrierOccurrence? occurrence)
    {
        occurrence = null;
        var definitionKey = ReadDefinitionKey(identity);
        var expectedSource = new EffectSourceKey(
            group.Key.Realm,
            group.Key.Kind,
            group.Key.SourceId,
            definitionKey);
        if (!WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(
                group.Owner,
                group.Target,
                definition,
                out var coordinate) ||
            !WoundEffectTerminalOperationPlanner.TryCreateExpectedIdentityOwner(
                group.Target,
                coordinate,
                out var expectedOwner) ||
            definition["stacking"] is not JsonObject stacking ||
            !TryReadExact(stacking["stackKey"], out var stackKey))
        {
            Add(
                issues,
                "effect.reactions.woundLineage." + identity.EffectId,
                "effect_reaction_wound_lineage_identity_invalid",
                "Exact owner, target, carrier, and stack authority derived from the sealed wound definition.",
                expectedSource.ToString(),
                identity.EffectId);
            return;
        }
        var expectedStack = new EffectStackCoordinate(
            group.Target.Realm,
            group.Target.Kind,
            group.Target.TargetId,
            expectedSource.Kind,
            expectedSource.SourceId,
            stackKey);
        var identityAgrees = identity.Owner == expectedOwner &&
            identity.StackCoordinate == expectedStack &&
            string.Equals(identity.Realm, group.Target.Realm, StringComparison.Ordinal) &&
            HasExactTarget(identity.Target, group.Target) &&
            HasExactSource(identity.Source, expectedSource);
        if (!identityAgrees)
        {
            Add(
                issues,
                "effect.reactions.woundLineage." + identity.EffectId,
                "effect_reaction_wound_lineage_identity_invalid",
                "Every current wound identity must retain the exact owner, target, source, carrier, and stack authority.",
                expectedSource.ToString(),
                identity.Raw.ToJsonString());
            return;
        }

        if (IsActiveOrSuspended(identity))
        {
            if (!carriers.TryResolveOne(identity.EffectId, out var active) ||
                !WoundEffectTerminalOperationPlanner.OccurrenceAndIdentityAgree(
                    active,
                    identity,
                    expectedSource,
                    group.Target,
                    coordinate,
                    expectedOwner!,
                    expectedStack))
            {
                Add(
                    issues,
                    "effect.reactions.woundLineage." + identity.EffectId,
                    "effect_reaction_wound_lineage_occurrence_invalid",
                    "One exact active carrier occurrence agreeing with its current wound identity.",
                    identity.EffectId,
                    "missing or mismatched");
                return;
            }
            occurrence = active;
        }
        else if (carriers.TryResolveOne(identity.EffectId, out _))
        {
            Add(
                issues,
                "effect.reactions.woundLineage." + identity.EffectId,
                "effect_reaction_wound_lineage_occurrence_invalid",
                "No active carrier occurrence for a terminal wound identity.",
                "absent",
                identity.EffectId);
        }
    }

    private static bool ComponentMatchesExactEdge(
        JsonObject actual,
        JsonObject producerDefinition,
        string downstreamDefinitionKey)
    {
        if (!TryReadExact(actual["componentId"], out var componentId) ||
            producerDefinition["components"] is not JsonArray components)
        {
            return false;
        }
        var matches = components.OfType<JsonObject>()
            .Where(component => HasExact(component, "componentId", componentId))
            .ToArray();
        return matches.Length == 1 &&
            JsonNode.DeepEquals(matches[0], actual) &&
            HasExact(actual, "profile", "event_reaction") &&
            actual["payload"] is JsonObject payload &&
            HasExact(payload, "resultKind", "apply_definition") &&
            HasExact(payload, "definitionKey", downstreamDefinitionKey);
    }

    private static bool IsActiveOrSuspended(EffectIdentityEntry identity) =>
        identity.State is "active" or "suspended";

    private static string ReadDefinitionKey(EffectIdentityEntry identity) =>
        identity.Source["definitionKey"]?.GetValue<string>() ?? string.Empty;

    private static bool TryReadSourceGroup(
        JsonObject effect,
        out EffectIdentitySourceGroup group)
    {
        group = null!;
        if (!TryReadExact(effect["realm"], out var realm) ||
            effect["source"] is not JsonObject source ||
            !TryReadExact(source["kind"], out var kind) ||
            !TryReadExact(source["sourceId"], out var sourceId))
        {
            return false;
        }
        group = new EffectIdentitySourceGroup(realm, kind, sourceId);
        return true;
    }

    private static bool OccurrenceEquals(
        EffectCarrierOccurrence expected,
        EffectCarrierOccurrence actual) =>
        string.Equals(expected.EffectId, actual.EffectId, StringComparison.Ordinal) &&
        string.Equals(expected.FilePath, actual.FilePath, StringComparison.Ordinal) &&
        string.Equals(expected.JsonPath, actual.JsonPath, StringComparison.Ordinal) &&
        expected.Coordinate == actual.Coordinate &&
        JsonNode.DeepEquals(expected.Effect, actual.Effect);

    private static bool HasExactTarget(
        JsonObject actual,
        EffectTargetKey expected) =>
        actual.Count == 2 &&
        HasExact(actual, "kind", expected.Kind) &&
        HasExact(actual, "targetId", expected.TargetId);

    private static bool HasExactSource(
        JsonObject actual,
        EffectSourceKey expected) =>
        actual.Count == 3 &&
        HasExact(actual, "kind", expected.Kind) &&
        HasExact(actual, "sourceId", expected.SourceId) &&
        HasExact(actual, "definitionKey", expected.DefinitionKey);

    private static bool HasExact(
        JsonObject value,
        string property,
        string expected) =>
        TryReadExact(value[property], out var actual) &&
        string.Equals(actual, expected, StringComparison.Ordinal);

    private static bool TryReadExact(JsonNode? node, out string value)
    {
        value = node is JsonValue json && json.TryGetValue<string>(out var text)
            ? text
            : string.Empty;
        return ResourceMaterializationContract.IsExactIdentifier(value);
    }

    private static string DescribeGroup(EffectIdentitySourceGroup group) =>
        group.Realm + "/" + group.Kind + "/" + group.SourceId;

    private static string DescribeGroup(EffectIdentityEntry identity) =>
        identity.Realm + "/" +
        (identity.Source["kind"]?.GetValue<string>() ?? "missing") + "/" +
        (identity.Source["sourceId"]?.GetValue<string>() ?? "missing");

    private static string DescribeDomain(WoundRootOwnershipDomain domain) =>
        domain.Kind + "/" + (domain.ComplicationId ?? "none");

    private static WoundReactionLineageResolution Failed(
        IReadOnlyList<ValidationIssue> issues) =>
        new(null, null, issues.ToArray());

    private static void AddRange(
        ICollection<ValidationIssue> issues,
        IEnumerable<ValidationIssue> additions)
    {
        foreach (var issue in additions)
        {
            if (issues.Count >= MaximumIssues)
                break;
            issues.Add(issue);
        }
    }

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string message,
        string expected,
        string actual)
    {
        if (issues.Count >= MaximumIssues)
            return;
        issues.Add(Issue(path, code, message, expected, actual));
    }

    private static ValidationIssue Issue(
        string path,
        string code,
        string message,
        string expected,
        string actual) =>
        new(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint:
                "Rebuild the accepted wound source group, exact root-result map, effect carriers, and first-create identity lineage before retrying the reaction.");

    private sealed record ResolvedRoot(WoundRootLineageAuthorityRow Row);

    private sealed class LineageGroup
    {
        internal LineageGroup(
            WoundSourceGroupAuthority authority,
            IReadOnlyDictionary<string, JsonObject> definitions,
            IReadOnlyDictionary<string, HashSet<string>> edges,
            IReadOnlyDictionary<string, WoundRootOwnershipDomain> definitionDomains)
        {
            Authority = authority.DetachedCopy();
            Definitions = definitions.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.DeepClone().AsObject(),
                StringComparer.Ordinal);
            Edges = edges.ToDictionary(
                static pair => pair.Key,
                static pair => new HashSet<string>(pair.Value, StringComparer.Ordinal),
                StringComparer.Ordinal);
            DefinitionDomains = definitionDomains.ToDictionary(
                static pair => pair.Key,
                static pair => new WoundRootOwnershipDomain(
                    pair.Value.Kind,
                    pair.Value.ComplicationId),
                StringComparer.Ordinal);
        }

        internal WoundSourceGroupAuthority Authority { get; }
        internal EffectTargetKey Target => Authority.Target;
        internal IReadOnlyDictionary<string, JsonObject> Definitions { get; }
        internal IReadOnlyDictionary<string, HashSet<string>> Edges { get; }
        internal IReadOnlyDictionary<string, WoundRootOwnershipDomain>
            DefinitionDomains { get; }
    }

    private sealed record LineageIdentity(
        LineageGroup Group,
        string EffectId,
        string DefinitionKey,
        WoundRootOwnershipDomain OwnershipDomain,
        EffectCarrierOccurrence? Occurrence)
    {
        internal LineageIdentity DetachedCopy() => new(
            Group,
            EffectId,
            DefinitionKey,
            new WoundRootOwnershipDomain(
                OwnershipDomain.Kind,
                OwnershipDomain.ComplicationId),
            Occurrence is null
                ? null
                : Occurrence with
                {
                    Coordinate = Occurrence.Coordinate with { },
                    Effect = Occurrence.Effect.DeepClone().AsObject()
                });
    }
}
