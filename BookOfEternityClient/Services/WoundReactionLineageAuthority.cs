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
    private readonly Dictionary<string, HistoricalIdentity> _history;
    private readonly ValidationIssue[] _issues;

    private WoundReactionLineageAuthority(
        EffectSourceAuthority sourceAuthority,
        Dictionary<string, LineageIdentity> identitiesByEffectId,
        Dictionary<string, HistoricalIdentity> history,
        IReadOnlyList<ValidationIssue> issues)
    {
        _sourceAuthority = sourceAuthority;
        _identitiesByEffectId = identitiesByEffectId.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DetachedCopy(),
            StringComparer.Ordinal);
        _issues = issues.ToArray();
        _history = new(history, StringComparer.Ordinal);
    }

    internal bool Success => _issues.Length == 0;

    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues.ToArray());

    /// <summary>
    /// Validates current wound lineage and retains immutable definition epochs for identities retired by later generations.
    /// </summary>
    /// <param name="sourceAuthority">
    /// Current validated source groups.
    /// </param>
    /// <param name="identities">
    /// Parsed current effect identities and histories.
    /// </param>
    /// <param name="carriers">
    /// Current physical effect occurrences.
    /// </param>
    /// <param name="applicationRootBindings">
    /// Exact application-to-root bindings for the current groups.
    /// </param>
    /// <param name="previousEpoch">
    /// Successful preceding routing epoch retained by the actual owner, or <see langword="null"/> for original validation.
    /// </param>
    /// <param name="retirementHistory">
    /// Actual registered insertion proving completed wound retirements, or null for the original path.
    /// </param>
    /// <returns>
    /// Current lineage and preserved historical definitions, or issues preventing their use.
    /// </returns>
    internal static WoundReactionLineageAuthority Build(
        EffectSourceAuthority sourceAuthority,
        EffectIdentityState identities,
        EffectCarrierCatalog carriers,
        IReadOnlyList<WoundApplicationRootEffectBinding> applicationRootBindings,
        WoundReactionLineageAuthority? previousEpoch = null,
        EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion? retirementHistory = null)
    {
        ArgumentNullException.ThrowIfNull(sourceAuthority);
        ArgumentNullException.ThrowIfNull(identities);
        ArgumentNullException.ThrowIfNull(carriers);
        ArgumentNullException.ThrowIfNull(applicationRootBindings);

        var issues = new List<ValidationIssue>();
        AddRange(issues, sourceAuthority.Issues);
        AddRange(issues, carriers.Issues);
        if (previousEpoch != null)
            AddRange(issues, previousEpoch.Issues);
        var history = previousEpoch is { Success: true }
            ? new Dictionary<string, HistoricalIdentity>(previousEpoch._history, StringComparer.Ordinal)
            : new Dictionary<string, HistoricalIdentity>(StringComparer.Ordinal);
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
                history,
                retirementHistory,
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
            history,
            issues);
    }

    /// <summary>
    /// Resolves one wound-owned apply-definition reaction against the exact current producer
    /// lineage, its source-group epoch and the downstream definition sealed by that group.
    /// </summary>
    /// <param name="producer">
    /// The active carrier occurrence that released the reaction. It must equal the producer
    /// retained by the current lineage.
    /// </param>
    /// <param name="component">
    /// The persisted producer component that must define the exact outgoing apply-definition edge.
    /// </param>
    /// <param name="target">
    /// The reaction target, which must equal the target owned by the producer's wound source group.
    /// </param>
    /// <param name="downstreamDefinitionKey">
    /// The exact downstream definition key named by the released reaction.
    /// </param>
    /// <returns>
    /// The current source binding and inherited ownership domain when the producer, edge, target,
    /// source-group epoch and catalog definition agree with the sealed lineage; otherwise,
    /// validation issues.
    /// </returns>
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
                out var downstreamDefinition) ||
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
        if (!_sourceAuthority.TryResolveWoundGroup(
                producerIdentity.Group.Authority.Key,
                out var currentSourceGroup) ||
            !string.Equals(
                currentSourceGroup.GraphAuthorityFingerprint,
                producerIdentity.Group.Authority.GraphAuthorityFingerprint,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                producer.JsonPath + ".components",
                "effect_reaction_wound_lineage_source_invalid",
                "The source catalog must retain the exact source-group epoch sealed by the current producer lineage.",
                producerIdentity.Group.Authority.GraphAuthorityFingerprint,
                currentSourceGroup?.GraphAuthorityFingerprint ?? "missing");
            return Failed(issues);
        }
        var resolution = _sourceAuthority.ResolveCanonicalBinding(
            sourceKey,
            target.Kind);
        AddRange(issues, resolution.Issues);
        if (!resolution.Success ||
            resolution.Source is null ||
            resolution.Source.Materializable ||
            !resolution.Source.Active ||
            !JsonNode.DeepEquals(
                resolution.Source.Definition,
                downstreamDefinition) ||
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

    /// <summary>
    /// Validates one wound source group and retains its exact current and historical definition authority.
    /// </summary>
    /// <param name="group">
    /// Source-owned definitions and current root bindings.
    /// </param>
    /// <param name="sourceAuthority">
    /// Source catalog authorizing definitions and exports.
    /// </param>
    /// <param name="identities">
    /// Complete parsed identities from the same routing view.
    /// </param>
    /// <param name="carriers">
    /// Current effect occurrences from that routing view.
    /// </param>
    /// <param name="applicationEffects">
    /// Actual application references mapped to allocated effect identities.
    /// </param>
    /// <param name="claimedRootEffects">
    /// Tracks root identities already claimed by preceding groups.
    /// </param>
    /// <param name="lineage">
    /// Receives validated current lineage identities.
    /// </param>
    /// <param name="history">
    /// Previously validated definition and identity history, updated on successful checks.
    /// </param>
    /// <param name="retirementHistory">
    /// Registered insertion proving completed terminal images; null retains original-baseline validation.
    /// </param>
    /// <param name="issues">
    /// Receives root, definition, occurrence and history diagnostics.
    /// </param>
    private static void BuildGroup(
        WoundSourceGroupAuthority group,
        EffectSourceAuthority sourceAuthority,
        EffectIdentityState identities,
        EffectCarrierCatalog carriers,
        IReadOnlyDictionary<string, string> applicationEffects,
        ISet<string> claimedRootEffects,
        IDictionary<string, LineageIdentity> lineage,
        IDictionary<string, HistoricalIdentity> history,
        EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion? retirementHistory,
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
            WoundEffectLineageDiagnosticProfile.Reaction, retirementHistory);
        AddRange(issues, analysis.Issues);

        foreach (var entry in analysis.RetiredEntries)
        {
            var definitionKey = ReadDefinitionKey(entry);
            if (history.TryGetValue(entry.EffectId, out var prior))
            {
                if (prior.Group.Authority.Key != group.Key || !prior.Agrees(entry))
                {
                    Add(issues, "effect.reactions.woundLineage." + entry.EffectId,
                        "effect_reaction_wound_lineage_history_changed",
                        "A retired identity must preserve its validated header and prior history.",
                        "the actual preceding identity epoch", entry.EffectId);
                    continue;
                }
                var priorIssueCount = issues.Count;
                ValidateIdentityAuthority(prior.Group.Authority, entry, prior.Group.Definitions[prior.DefinitionKey],
                    carriers, issues, out _);
                if (issues.Count == priorIssueCount)
                    history[entry.EffectId] = HistoricalIdentity.Capture(prior.Group, entry);
            }
            else if (definitions.TryGetValue(definitionKey, out var definition))
            {
                var priorIssueCount = issues.Count;
                ValidateIdentityAuthority(
                    group,
                    entry,
                    definition,
                    carriers,
                    issues,
                    out _);
                if (issues.Count == priorIssueCount)
                    history[entry.EffectId] = HistoricalIdentity.Capture(groupState, entry);
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
            var currentIssueCount = issues.Count;
            ValidateIdentityAuthority(
                group,
                entry,
                definition,
                carriers,
                issues,
                out var occurrence);
            if (issues.Count == currentIssueCount)
                history[entry.EffectId] = HistoricalIdentity.Capture(groupState, entry);
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

    /// <summary>
    /// Retains one validated identity's definition epoch and compact continuity evidence without retaining an epoch chain.
    /// </summary>
    private sealed class HistoricalIdentity
    {
        /// <summary>
        /// Captures immutable fingerprints while sharing the validated group's private definition snapshot.
        /// </summary>
        /// <param name="group">
        /// Exact definition group used to validate this identity.
        /// </param>
        /// <param name="entry">
        /// Parsed identity whose current validation succeeded.
        /// </param>
        private HistoricalIdentity(LineageGroup group, EffectIdentityEntry entry)
        {
            Group = group;
            DefinitionKey = ReadDefinitionKey(entry);
            _state = entry.State;
            _transitionCount = entry.Transitions.Count;
            _headerFingerprint = HeaderFingerprint(entry.Raw);
            _transitionFingerprint = PrefixFingerprint(entry.Raw, _transitionCount);
            _fullFingerprint = Digest(entry.Raw);
        }

        /// <summary>
        /// Gets the private shared definition epoch.
        /// </summary>
        internal LineageGroup Group { get; }
        /// <summary>
        /// Gets the exact definition key within the retained epoch.
        /// </summary>
        internal string DefinitionKey { get; }
        private readonly string _state;
        private readonly int _transitionCount;
        private readonly string _headerFingerprint;
        private readonly string _transitionFingerprint;
        private readonly string _fullFingerprint;

        /// <summary>
        /// Records an already validated identity without modifying its definition snapshot.
        /// </summary>
        /// <param name="group">
        /// Group supplying the original definition for this identity.
        /// </param>
        /// <param name="entry">
        /// Successfully validated current identity image.
        /// </param>
        /// <returns>
        /// Compact private continuity evidence for the next epoch.
        /// </returns>
        internal static HistoricalIdentity Capture(LineageGroup group, EffectIdentityEntry entry) => new(group, entry);

        /// <summary>
        /// Checks a retired identity against its preceding validated image without admitting history rewrites.
        /// </summary>
        /// <param name="entry">
        /// Current parsed retired identity.
        /// </param>
        /// <returns>
        /// True for an unchanged terminal image or preserved live header and transition prefix; otherwise false.
        /// </returns>
        internal bool Agrees(EffectIdentityEntry entry) => _state is not ("active" or "suspended")
            ? _fullFingerprint == Digest(entry.Raw)
            : entry.Transitions.Count >= _transitionCount && _headerFingerprint == HeaderFingerprint(entry.Raw) &&
              _transitionFingerprint == PrefixFingerprint(entry.Raw, _transitionCount);

        /// <summary>
        /// Fingerprints all immutable identity header fields.
        /// </summary>
        /// <param name="raw">
        /// Parsed identity JSON.
        /// </param>
        /// <returns>
        /// Canonical fingerprint excluding state and transition history.
        /// </returns>
        private static string HeaderFingerprint(JsonObject raw)
        {
            var header = raw.DeepClone().AsObject();
            header.Remove("state");
            header.Remove("transitions");
            return Digest(header);
        }

        /// <summary>
        /// Fingerprints the exact ordered retained prefix of an identity history.
        /// </summary>
        /// <param name="raw">
        /// Parsed identity JSON containing its transition array.
        /// </param>
        /// <param name="count">
        /// Number of previously validated transitions.
        /// </param>
        /// <returns>
        /// Canonical prefix fingerprint.
        /// </returns>
        private static string PrefixFingerprint(JsonObject raw, int count) => Digest(
            new JsonArray(raw["transitions"]!.AsArray().Take(count).Select(node => node!.DeepClone()).ToArray()));

        /// <summary>
        /// Computes canonical private comparison evidence.
        /// </summary>
        /// <param name="node">
        /// JSON image to fingerprint.
        /// </param>
        /// <returns>
        /// A comparison fingerprint, not an independently reconstructible authority.
        /// </returns>
        private static string Digest(JsonNode node) => WoundAcceptedTurnFingerprintWriter.Compute(
            new[] { WoundAcceptedTurnFingerprintWriter.CanonicalJson(node) });
    }

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
