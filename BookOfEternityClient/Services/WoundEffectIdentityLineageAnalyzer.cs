using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal enum WoundEffectLineageDiagnosticProfile
{
    AcceptedMechanics,
    Reaction
}

internal sealed record WoundEffectIdentityLineageRoot(
    string EffectId,
    string DefinitionKey,
    WoundRootOwnershipDomain OwnershipDomain);

internal sealed record WoundEffectIdentityLineageDefinition(
    string DefinitionKey,
    IReadOnlySet<string> ApplyDefinitionTargets,
    WoundRootOwnershipDomain OwnershipDomain);

internal sealed class WoundEffectIdentityLineageAnalysis
{
    private readonly EffectIdentityEntry[] _currentEntries;
    private readonly EffectIdentityEntry[] _retiredEntries;
    private readonly IReadOnlyDictionary<string, WoundRootOwnershipDomain>
        _currentOwnershipByEffectId;
    private readonly IReadOnlyDictionary<string, string> _currentOriginRootByEffectId;
    private readonly ValidationIssue[] _issues;

    internal WoundEffectIdentityLineageAnalysis(
        IReadOnlyList<EffectIdentityEntry> currentEntries,
        IReadOnlyList<EffectIdentityEntry> retiredEntries,
        IReadOnlyDictionary<string, WoundRootOwnershipDomain>
            currentOwnershipByEffectId,
        IReadOnlyDictionary<string, string> currentOriginRootByEffectId,
        int sourceGroupIdentityCount,
        int visitedCurrentIdentityCount,
        IReadOnlyList<ValidationIssue> issues)
    {
        _currentEntries = currentEntries.Select(CloneEntry).ToArray();
        _retiredEntries = retiredEntries.Select(CloneEntry).ToArray();
        _currentOwnershipByEffectId = currentOwnershipByEffectId.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value with { },
            StringComparer.Ordinal);
        _currentOriginRootByEffectId = currentOriginRootByEffectId.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.Ordinal);
        SourceGroupIdentityCount = sourceGroupIdentityCount;
        VisitedCurrentIdentityCount = visitedCurrentIdentityCount;
        _issues = issues.ToArray();
    }

    internal bool Success => _issues.Length == 0;

    internal IReadOnlyList<EffectIdentityEntry> CurrentEntries =>
        Array.AsReadOnly(_currentEntries.Select(CloneEntry).ToArray());

    internal IReadOnlyList<EffectIdentityEntry> RetiredEntries =>
        Array.AsReadOnly(_retiredEntries.Select(CloneEntry).ToArray());

    internal IReadOnlyDictionary<string, WoundRootOwnershipDomain>
        CurrentOwnershipByEffectId => new ReadOnlyDictionary<
            string,
            WoundRootOwnershipDomain>(_currentOwnershipByEffectId.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value with { },
                StringComparer.Ordinal));

    internal IReadOnlyDictionary<string, string> CurrentOriginRootByEffectId =>
        new ReadOnlyDictionary<string, string>(
            _currentOriginRootByEffectId.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value,
                StringComparer.Ordinal));

    internal int SourceGroupIdentityCount { get; }
    internal int VisitedCurrentIdentityCount { get; }

    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues.ToArray());

    private static EffectIdentityEntry CloneEntry(EffectIdentityEntry value) =>
        new(
            value.EffectId,
            value.State,
            value.Realm,
            value.Owner with { },
            value.Target.DeepClone().AsObject(),
            value.Source.DeepClone().AsObject(),
            value.StackCoordinate with { },
            value.CreatedAtTurn,
            value.Transitions.Select(static transition =>
                new EffectIdentityTransition(
                    transition.TransitionId,
                    transition.Kind,
                    transition.Turn,
                    transition.EventRef,
                    transition.SourceEffectIds.ToArray(),
                    transition.ResultEffectIds.ToArray(),
                    transition.ReceiptId,
                    transition.Raw.DeepClone().AsObject())).ToArray(),
            value.Raw.DeepClone().AsObject());
}

internal static class WoundEffectIdentityLineageAnalyzer
{
    private const int MaximumIssues = 20;

    internal static WoundEffectIdentityLineageAnalysis Analyze(
        EffectIdentitySourceGroup sourceGroup,
        EffectIdentityState identities,
        IReadOnlyList<WoundEffectIdentityLineageRoot> currentRoots,
        IReadOnlyDictionary<string, WoundEffectIdentityLineageDefinition>
            currentDefinitions,
        WoundEffectLineageDiagnosticProfile diagnosticProfile)
    {
        ArgumentNullException.ThrowIfNull(sourceGroup);
        ArgumentNullException.ThrowIfNull(identities);
        ArgumentNullException.ThrowIfNull(currentRoots);
        ArgumentNullException.ThrowIfNull(currentDefinitions);

        var issues = new List<ValidationIssue>();
        var members = identities.ResolveSourceGroup(sourceGroup).ToArray();
        var membersById = members.ToDictionary(
            static entry => entry.EffectId,
            StringComparer.Ordinal);
        var rootsById = new Dictionary<
            string,
            WoundEffectIdentityLineageRoot>(StringComparer.Ordinal);
        foreach (var root in currentRoots)
        {
            if (root is null ||
                !ResourceMaterializationContract.IsExactIdentifier(root.EffectId) ||
                !ResourceMaterializationContract.IsExactIdentifier(
                    root.DefinitionKey) ||
                !rootsById.TryAdd(root.EffectId, root))
            {
                Add(
                    issues,
                    diagnosticProfile,
                    "root_binding_invalid",
                    root?.EffectId ?? "missing",
                    "Every current wound root selector must resolve to one unique exact effect identity.",
                    "unique current root",
                    root?.EffectId ?? "missing");
            }
        }

        ValidateCurrentMembership(
            members,
            currentDefinitions,
            diagnosticProfile,
            issues);
        ValidateCurrentGraphAcyclicity(
            members,
            currentDefinitions,
            diagnosticProfile,
            issues);

        var retired = new HashSet<string>(StringComparer.Ordinal);
        var current = new Dictionary<string, CurrentVisit>(StringComparer.Ordinal);
        var generationSuccessorByPredecessor = new Dictionary<string, string>(
            StringComparer.Ordinal);

        foreach (var root in rootsById.Values.OrderBy(
                     static value => value.EffectId,
                     StringComparer.Ordinal))
        {
            if (!membersById.TryGetValue(root.EffectId, out var currentRoot) ||
                !string.Equals(
                    DefinitionKey(currentRoot),
                    root.DefinitionKey,
                    StringComparison.Ordinal) ||
                !currentDefinitions.ContainsKey(root.DefinitionKey))
            {
                Add(
                    issues,
                    diagnosticProfile,
                    "root_binding_invalid",
                    root.EffectId,
                    "Every current wound root binding must resolve to its exact source-group identity and definition.",
                    root.DefinitionKey,
                    membersById.TryGetValue(root.EffectId, out var found)
                        ? DefinitionKey(found)
                        : "missing");
                continue;
            }

            var chain = new HashSet<string>(StringComparer.Ordinal);
            var successor = currentRoot;
            while (true)
            {
                if (!chain.Add(successor.EffectId))
                {
                    Add(
                        issues,
                        diagnosticProfile,
                        "cycle",
                        successor.EffectId,
                        "A retained severity-generation chain must be finite and acyclic.",
                        "acyclic generation chain",
                        successor.EffectId);
                    break;
                }

                if (!TryReadCreate(successor, out var create))
                {
                    AddCreateInvalid(
                        issues,
                        diagnosticProfile,
                        successor,
                        "one first create transition");
                    break;
                }
                if (create.SourceEffectIds.Count == 0)
                    break;
                if (create.SourceEffectIds.Count != 1 ||
                    !membersById.TryGetValue(
                        create.SourceEffectIds[0],
                        out var predecessor) ||
                    !IsGenerationPredecessor(successor, predecessor, sourceGroup))
                {
                    Add(
                        issues,
                        diagnosticProfile,
                        "generation_invalid",
                        successor.EffectId,
                        "A retained root generation must name one exact same-source, same-definition, same-target, same-carrier predecessor.",
                        DefinitionKey(successor),
                        string.Join(",", create.SourceEffectIds));
                    break;
                }
                if (!generationSuccessorByPredecessor.TryAdd(
                        predecessor.EffectId,
                        successor.EffectId) &&
                    !string.Equals(
                        generationSuccessorByPredecessor[predecessor.EffectId],
                        successor.EffectId,
                        StringComparison.Ordinal))
                {
                    AddGenerationFork(
                        issues,
                        diagnosticProfile,
                        predecessor.EffectId);
                }
                if (IsActiveOrSuspended(predecessor))
                {
                    Add(
                        issues,
                        diagnosticProfile,
                        "retired_active",
                        predecessor.EffectId,
                        "Every predecessor generation and its reaction descendants must be terminal.",
                        "terminal retired generation",
                        predecessor.State);
                }
                retired.Add(predecessor.EffectId);
                successor = predecessor;
            }

            ValidateGenerationForks(
                sourceGroup,
                identities,
                chain,
                generationSuccessorByPredecessor,
                diagnosticProfile,
                issues);
            TraverseRetiredGenerationReactions(
                sourceGroup,
                identities,
                currentDefinitions,
                root.OwnershipDomain,
                chain.Where(effectId => !string.Equals(
                    effectId,
                    currentRoot.EffectId,
                    StringComparison.Ordinal)),
                retired,
                diagnosticProfile,
                issues);
            TraverseCurrentGeneration(
                sourceGroup,
                identities,
                currentDefinitions,
                root,
                currentRoot,
                current,
                diagnosticProfile,
                issues);
        }

        foreach (var member in members.Where(entry =>
                     currentDefinitions.ContainsKey(DefinitionKey(entry))))
        {
            if (current.ContainsKey(member.EffectId) ||
                retired.Contains(member.EffectId))
            {
                continue;
            }
            Add(
                issues,
                diagnosticProfile,
                IsActiveOrSuspended(member)
                    ? "active_unreachable"
                    : "current_unreachable",
                member.EffectId,
                "Every identity whose definition remains current must belong to exactly one current or authenticated retired generation.",
                "reachable current or retired generation",
                member.EffectId);
        }

        var currentEntries = current.Values
            .Select(static visit => visit.Entry)
            .OrderBy(static entry => entry.EffectId, StringComparer.Ordinal)
            .ToArray();
        var retiredEntries = retired
            .Where(membersById.ContainsKey)
            .Select(effectId => membersById[effectId])
            .OrderBy(static entry => entry.EffectId, StringComparer.Ordinal)
            .ToArray();
        var ownership = current.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.OwnershipDomain,
            StringComparer.Ordinal);
        var origins = current.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.OriginRootEffectId,
            StringComparer.Ordinal);
        return new WoundEffectIdentityLineageAnalysis(
            currentEntries,
            retiredEntries,
            ownership,
            origins,
            members.Length,
            currentEntries.Length,
            issues);
    }

    private static void TraverseCurrentGeneration(
        EffectIdentitySourceGroup sourceGroup,
        EffectIdentityState identities,
        IReadOnlyDictionary<string, WoundEffectIdentityLineageDefinition>
            definitions,
        WoundEffectIdentityLineageRoot root,
        EffectIdentityEntry currentRoot,
        IDictionary<string, CurrentVisit> current,
        WoundEffectLineageDiagnosticProfile profile,
        List<ValidationIssue> issues)
    {
        var queue = new Queue<EffectIdentityEntry>();
        queue.Enqueue(currentRoot);
        while (queue.Count != 0)
        {
            var entry = queue.Dequeue();
            if (current.TryGetValue(entry.EffectId, out var prior))
            {
                if (!string.Equals(
                        prior.OriginRootEffectId,
                        root.EffectId,
                        StringComparison.Ordinal) ||
                    prior.OwnershipDomain != root.OwnershipDomain)
                {
                    Add(
                        issues,
                        profile,
                        "ambiguous",
                        entry.EffectId,
                        "Each current wound identity must belong to exactly one root generation and ownership domain.",
                        prior.OriginRootEffectId,
                        root.EffectId);
                }
                continue;
            }
            current.Add(
                entry.EffectId,
                new CurrentVisit(entry, root.EffectId, root.OwnershipDomain));

            if (!definitions.TryGetValue(
                    DefinitionKey(entry),
                    out var definition) ||
                definition.OwnershipDomain != root.OwnershipDomain)
            {
                Add(
                    issues,
                    profile,
                    "cross_domain",
                    entry.EffectId,
                    "Every current identity must resolve in the exact inherited wound ownership domain.",
                    Describe(root.OwnershipDomain),
                    DefinitionKey(entry));
                continue;
            }

            foreach (var child in identities.ResolveFirstCreateChildren(
                         entry.EffectId))
            {
                if (!IsSourceGroup(child, sourceGroup))
                {
                    Add(
                        issues,
                        profile,
                        "foreign_child",
                        child.EffectId,
                        "A reaction descendant must retain the exact parent wound source group.",
                        Describe(sourceGroup),
                        Describe(child));
                    continue;
                }
                var childDefinition = DefinitionKey(child);
                if (string.Equals(
                        childDefinition,
                        definition.DefinitionKey,
                        StringComparison.Ordinal))
                {
                    AddGenerationFork(issues, profile, entry.EffectId);
                    continue;
                }
                if (!definitions.TryGetValue(childDefinition, out var childFact))
                {
                    if (IsActiveOrSuspended(child))
                    {
                        Add(
                            issues,
                            profile,
                            "definition_missing",
                            child.EffectId,
                            "Every active wound reaction descendant must resolve in the current source graph.",
                            string.Join(",", definition.ApplyDefinitionTargets),
                            childDefinition);
                    }
                    continue;
                }
                if (!definition.ApplyDefinitionTargets.Contains(childDefinition) ||
                    childFact.OwnershipDomain != root.OwnershipDomain ||
                    !TryReadCreate(child, out var create) ||
                    create.SourceEffectIds.Count != 1 ||
                    !string.Equals(
                        create.SourceEffectIds[0],
                        entry.EffectId,
                        StringComparison.Ordinal))
                {
                    Add(
                        issues,
                        profile,
                        childFact.OwnershipDomain != root.OwnershipDomain
                            ? "cross_domain"
                            : "edge_invalid",
                        child.EffectId,
                        "A reaction descendant must be the exact persisted apply_definition target of its sole causal parent.",
                        string.Join(",", definition.ApplyDefinitionTargets),
                        childDefinition);
                    continue;
                }
                queue.Enqueue(child);
            }
        }
    }

    private static void TraverseRetiredGenerationReactions(
        EffectIdentitySourceGroup sourceGroup,
        EffectIdentityState identities,
        IReadOnlyDictionary<string, WoundEffectIdentityLineageDefinition>
            definitions,
        WoundRootOwnershipDomain ownershipDomain,
        IEnumerable<string> retiredRootIds,
        ISet<string> retired,
        WoundEffectLineageDiagnosticProfile profile,
        List<ValidationIssue> issues)
    {
        var queue = new Queue<EffectIdentityEntry>();
        foreach (var rootId in retiredRootIds.Distinct(StringComparer.Ordinal))
        {
            if (identities.TryGetEntry(rootId, out var entry))
                queue.Enqueue(entry);
        }
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (queue.Count != 0)
        {
            var parent = queue.Dequeue();
            if (!visited.Add(parent.EffectId) ||
                !definitions.TryGetValue(DefinitionKey(parent), out var parentFact))
            {
                continue;
            }
            foreach (var child in identities.ResolveFirstCreateChildren(
                         parent.EffectId))
            {
                var childDefinition = DefinitionKey(child);
                if (!IsSourceGroup(child, sourceGroup))
                {
                    Add(
                        issues,
                        profile,
                        "foreign_child",
                        child.EffectId,
                        "A retired reaction descendant must retain the exact parent wound source group.",
                        Describe(sourceGroup),
                        Describe(child));
                    continue;
                }
                if (string.Equals(
                        childDefinition,
                        parentFact.DefinitionKey,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                if (!definitions.TryGetValue(childDefinition, out var childFact))
                {
                    if (IsActiveOrSuspended(child))
                    {
                        Add(
                            issues,
                            profile,
                            "definition_missing",
                            child.EffectId,
                            "Historical identities for removed definitions must remain terminal.",
                            "terminal removed-definition history",
                            child.State);
                    }
                    continue;
                }
                if (!parentFact.ApplyDefinitionTargets.Contains(childDefinition) ||
                    childFact.OwnershipDomain != ownershipDomain ||
                    !TryReadCreate(child, out var create) ||
                    create.SourceEffectIds.Count != 1 ||
                    !string.Equals(
                        create.SourceEffectIds[0],
                        parent.EffectId,
                        StringComparison.Ordinal))
                {
                    Add(
                        issues,
                        profile,
                        "edge_invalid",
                        child.EffectId,
                        "A retired generation reaction child must retain its exact persisted apply_definition edge.",
                        string.Join(",", parentFact.ApplyDefinitionTargets),
                        childDefinition);
                    continue;
                }
                retired.Add(child.EffectId);
                if (IsActiveOrSuspended(child))
                {
                    Add(
                        issues,
                        profile,
                        "retired_active",
                        child.EffectId,
                        "Every predecessor generation and its reaction descendants must be terminal.",
                        "terminal retired reaction",
                        child.State);
                }
                queue.Enqueue(child);
            }
        }
    }

    private static void ValidateGenerationForks(
        EffectIdentitySourceGroup sourceGroup,
        EffectIdentityState identities,
        IReadOnlySet<string> generationChain,
        IReadOnlyDictionary<string, string> expectedSuccessors,
        WoundEffectLineageDiagnosticProfile profile,
        List<ValidationIssue> issues)
    {
        foreach (var predecessorId in generationChain)
        {
            if (!identities.TryGetEntry(predecessorId, out var predecessor))
                continue;
            var sameCoordinateChildren = identities.ResolveFirstCreateChildren(
                    predecessorId)
                .Where(child => IsGenerationPredecessor(child, predecessor, sourceGroup))
                .Select(static child => child.EffectId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (sameCoordinateChildren.Length > 1 ||
                expectedSuccessors.TryGetValue(predecessorId, out var expected) &&
                (sameCoordinateChildren.Length != 1 ||
                 !string.Equals(
                     sameCoordinateChildren[0],
                     expected,
                     StringComparison.Ordinal)))
            {
                AddGenerationFork(issues, profile, predecessorId);
            }
        }
    }

    private static void ValidateCurrentMembership(
        IReadOnlyList<EffectIdentityEntry> members,
        IReadOnlyDictionary<string, WoundEffectIdentityLineageDefinition>
            definitions,
        WoundEffectLineageDiagnosticProfile profile,
        List<ValidationIssue> issues)
    {
        var active = members.Where(IsActiveOrSuspended).ToArray();
        if (active.Length > WoundMaterializationContract.MaxOwnedEffectDefinitions)
        {
            Add(
                issues,
                profile,
                "active_bound_exceeded",
                "woundLineage",
                "At most one simultaneous source member per bounded wound definition.",
                WoundMaterializationContract.MaxOwnedEffectDefinitions.ToString(),
                active.Length.ToString());
        }
        foreach (var entry in active)
        {
            if (!definitions.ContainsKey(DefinitionKey(entry)))
            {
                Add(
                    issues,
                    profile,
                    "definition_missing",
                    entry.EffectId,
                    "Every active or suspended wound identity must resolve in the current source graph.",
                    "current definitionKey",
                    DefinitionKey(entry));
            }
        }
        foreach (var duplicate in active
                     .Where(entry => definitions.ContainsKey(DefinitionKey(entry)))
                     .GroupBy(DefinitionKey, StringComparer.Ordinal)
                     .Where(static group => group.Count() > 1))
        {
            Add(
                issues,
                profile,
                "active_duplicate",
                duplicate.Key,
                "At most one active or suspended identity may occupy each current definition coordinate.",
                "one identity",
                string.Join(",", duplicate.Select(static value => value.EffectId)));
        }
    }

    private static void ValidateCurrentGraphAcyclicity(
        IReadOnlyList<EffectIdentityEntry> members,
        IReadOnlyDictionary<string, WoundEffectIdentityLineageDefinition>
            definitions,
        WoundEffectLineageDiagnosticProfile profile,
        List<ValidationIssue> issues)
    {
        var current = members
            .Where(entry => definitions.ContainsKey(DefinitionKey(entry)))
            .ToDictionary(static entry => entry.EffectId, StringComparer.Ordinal);
        var indegree = current.Keys.ToDictionary(
            static value => value,
            static _ => 0,
            StringComparer.Ordinal);
        var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var entry in current.Values)
        {
            if (!TryReadCreate(entry, out var create))
            {
                AddCreateInvalid(issues, profile, entry, "one first create transition");
                continue;
            }
            foreach (var parent in create.SourceEffectIds)
            {
                if (!current.ContainsKey(parent))
                    continue;
                if (!children.TryGetValue(parent, out var values))
                {
                    values = new List<string>();
                    children.Add(parent, values);
                }
                values.Add(entry.EffectId);
                indegree[entry.EffectId]++;
            }
        }
        var ready = new Queue<string>(indegree
            .Where(static pair => pair.Value == 0)
            .Select(static pair => pair.Key)
            .OrderBy(static value => value, StringComparer.Ordinal));
        var processed = 0;
        while (ready.Count != 0)
        {
            var effectId = ready.Dequeue();
            processed++;
            if (!children.TryGetValue(effectId, out var descendants))
                continue;
            foreach (var child in descendants)
            {
                if (--indegree[child] == 0)
                    ready.Enqueue(child);
            }
        }
        if (processed != current.Count)
        {
            Add(
                issues,
                profile,
                "cycle",
                "woundLineage",
                "First-create wound generation/reaction lineage must be acyclic.",
                "acyclic first-create lineage",
                string.Join(",", indegree.Where(static pair => pair.Value > 0)
                    .Select(static pair => pair.Key)
                    .OrderBy(static value => value, StringComparer.Ordinal)));
        }
    }

    private static bool IsGenerationPredecessor(
        EffectIdentityEntry successor,
        EffectIdentityEntry predecessor,
        EffectIdentitySourceGroup group) =>
        IsSourceGroup(predecessor, group) &&
        string.Equals(
            DefinitionKey(successor),
            DefinitionKey(predecessor),
            StringComparison.Ordinal) &&
        string.Equals(successor.Realm, predecessor.Realm, StringComparison.Ordinal) &&
        successor.Owner == predecessor.Owner &&
        successor.StackCoordinate == predecessor.StackCoordinate &&
        JsonNode.DeepEquals(successor.Target, predecessor.Target) &&
        JsonNode.DeepEquals(successor.Source, predecessor.Source);

    private static bool TryReadCreate(
        EffectIdentityEntry entry,
        out EffectIdentityTransition create)
    {
        create = null!;
        var creates = entry.Transitions.Where(static transition => string.Equals(
                transition.Kind,
                "create",
                StringComparison.Ordinal))
            .ToArray();
        if (creates.Length != 1 ||
            entry.Transitions.Count == 0 ||
            !string.Equals(
                entry.Transitions[0].TransitionId,
                creates[0].TransitionId,
                StringComparison.Ordinal) ||
            creates[0].SourceEffectIds.Count > 1 ||
            creates[0].ResultEffectIds.Count != 1 ||
            !string.Equals(
                creates[0].ResultEffectIds[0],
                entry.EffectId,
                StringComparison.Ordinal) ||
            creates[0].ReceiptId is not null)
        {
            return false;
        }
        create = creates[0];
        return true;
    }

    private static bool IsActiveOrSuspended(EffectIdentityEntry entry) =>
        entry.State is "active" or "suspended";

    private static string DefinitionKey(EffectIdentityEntry entry) =>
        entry.Source["definitionKey"]?.GetValue<string>() ?? string.Empty;

    private static bool IsSourceGroup(
        EffectIdentityEntry entry,
        EffectIdentitySourceGroup group) =>
        string.Equals(entry.Realm, group.Realm, StringComparison.Ordinal) &&
        string.Equals(
            entry.Source["kind"]?.GetValue<string>(),
            group.Kind,
            StringComparison.Ordinal) &&
        string.Equals(
            entry.Source["sourceId"]?.GetValue<string>(),
            group.SourceId,
            StringComparison.Ordinal);

    private static void AddCreateInvalid(
        ICollection<ValidationIssue> issues,
        WoundEffectLineageDiagnosticProfile profile,
        EffectIdentityEntry entry,
        string expected) => Add(
        issues,
        profile,
        "create_invalid",
        entry.EffectId,
        "Ownership lineage requires one first create transition with zero or one exact causal parent, self result, and no receipt.",
        expected,
        entry.Transitions.Count == 0
            ? "missing"
            : string.Join(",", entry.Transitions[0].SourceEffectIds));

    private static void AddGenerationFork(
        ICollection<ValidationIssue> issues,
        WoundEffectLineageDiagnosticProfile profile,
        string predecessorEffectId) => Add(
        issues,
        profile,
        "generation_invalid",
        predecessorEffectId,
        "A severity-generation predecessor may have only one exact same-coordinate successor.",
        "one successor",
        "fork or mismatched successor");

    private static void Add(
        ICollection<ValidationIssue> issues,
        WoundEffectLineageDiagnosticProfile profile,
        string kind,
        string identity,
        string message,
        string expected,
        string actual)
    {
        if (issues.Count >= MaximumIssues)
            return;
        var reaction = profile == WoundEffectLineageDiagnosticProfile.Reaction;
        var code = reaction
            ? kind switch
            {
                "root_binding_invalid" => "effect_reaction_wound_root_binding_invalid",
                "foreign_child" => "effect_reaction_wound_lineage_foreign",
                "definition_missing" => "effect_reaction_wound_lineage_edge_invalid",
                "edge_invalid" => "effect_reaction_wound_lineage_edge_invalid",
                "cross_domain" => "effect_reaction_wound_lineage_cross_domain",
                "ambiguous" => "effect_reaction_wound_lineage_ambiguous",
                "active_bound_exceeded" => "effect_reaction_wound_lineage_active_bound_exceeded",
                "active_duplicate" => "effect_reaction_wound_lineage_active_duplicate",
                "cycle" => "effect_reaction_wound_lineage_cycle",
                "generation_invalid" => "effect_reaction_wound_lineage_generation_invalid",
                "retired_active" => "effect_reaction_wound_lineage_retired_active",
                "create_invalid" => "effect_reaction_wound_lineage_create_invalid",
                _ => "effect_reaction_wound_lineage_unreachable"
            }
            : kind switch
            {
                "root_binding_invalid" => "accepted_mechanics_wound_lineage_root_missing",
                "foreign_child" => "accepted_mechanics_wound_lineage_foreign_child",
                "definition_missing" => "accepted_mechanics_wound_lineage_definition_missing",
                "edge_invalid" => "accepted_mechanics_wound_lineage_edge_invalid",
                "cross_domain" => "accepted_mechanics_wound_lineage_cross_domain",
                "ambiguous" => "accepted_mechanics_wound_lineage_ambiguous",
                "active_bound_exceeded" => "accepted_mechanics_wound_lineage_active_bound_exceeded",
                "active_duplicate" => "accepted_mechanics_wound_lineage_active_duplicate",
                "cycle" => "accepted_mechanics_wound_lineage_cycle",
                "generation_invalid" => "accepted_mechanics_wound_lineage_generation_invalid",
                "retired_active" => "accepted_mechanics_wound_lineage_retired_active",
                "active_unreachable" => "accepted_mechanics_wound_lineage_active_unreachable",
                "current_unreachable" => "accepted_mechanics_wound_lineage_current_unreachable",
                _ => "accepted_mechanics_wound_lineage_create_invalid"
            };
        issues.Add(new ValidationIssue(
            (reaction
                ? "effect.reactions.woundLineage."
                : "acceptedMechanics.woundEffectLineage.") + identity,
            IssueSeverity.Error,
            message,
            code: code,
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint:
                "Restore the exact sealed origin, generation, and reaction lineage before retrying."));
    }

    private static string Describe(EffectIdentitySourceGroup group) =>
        group.Realm + "/" + group.Kind + "/" + group.SourceId;

    private static string Describe(EffectIdentityEntry entry) =>
        entry.Realm + "/" +
        (entry.Source["kind"]?.GetValue<string>() ?? "missing") + "/" +
        (entry.Source["sourceId"]?.GetValue<string>() ?? "missing");

    private static string Describe(WoundRootOwnershipDomain domain) =>
        domain.Kind + "/" + (domain.ComplicationId ?? "none");

    private sealed record CurrentVisit(
        EffectIdentityEntry Entry,
        string OriginRootEffectId,
        WoundRootOwnershipDomain OwnershipDomain);
}
