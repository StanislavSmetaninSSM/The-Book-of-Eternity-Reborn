using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class EffectReactionContract
{
    internal const int MaximumExpansion = 64;

    internal static IReadOnlyList<ValidationIssue> ValidateDefinitionGraph(
        JsonElement definitions,
        string path)
    {
        var issues = new List<ValidationIssue>();
        if (definitions.ValueKind != JsonValueKind.Array)
            return issues;

        var parsed = new Dictionary<string, DefinitionNode>(StringComparer.Ordinal);
        var definitionIndex = 0;
        foreach (var definition in definitions.EnumerateArray())
        {
            var definitionPath = $"{path}[{definitionIndex++}]";
            if (definition.ValueKind != JsonValueKind.Object ||
                !TryReadExact(definition, "definitionKey", out var definitionKey) ||
                parsed.ContainsKey(definitionKey))
            {
                continue;
            }

            var components = ReadComponents(definition, definitionPath);
            var triggers = ReadTriggers(definition, definitionPath);
            var reactions = components.Values
                .Where(static component => string.Equals(
                    component.Profile,
                    "event_reaction",
                    StringComparison.Ordinal))
                .Select(component => ReadReaction(component, issues))
                .Where(static reaction => reaction != null)
                .Cast<ReactionNode>()
                .ToArray();
            parsed.Add(
                definitionKey,
                new DefinitionNode(
                    definitionKey,
                    definitionPath,
                    definition.Clone(),
                    ReadExactSet(definition, "allowedRealms"),
                    ReadExactSet(definition, "allowedTargetKinds"),
                    components,
                    triggers,
                    reactions));
        }

        var edges = parsed.Keys.ToDictionary(
            static key => key,
            static _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        foreach (var definition in parsed.Values)
        {
            foreach (var reaction in definition.Reactions)
            {
                ValidateOwningTrigger(definition, reaction, issues);
                ValidateComponentReferences(definition, reaction, issues);
                if (!HasBehavior(
                        reaction,
                        EffectReactionResultBehavior.ApplyDefinition))
                {
                    continue;
                }

                if (reaction.DefinitionKey == null ||
                    !parsed.TryGetValue(reaction.DefinitionKey, out var downstream))
                {
                    Add(
                        issues,
                        reaction.Path + ".definitionKey",
                        "effect_source_definition_reaction_reference_unresolved",
                        "one exact downstream definition owned by the same source",
                        reaction.DefinitionKey ?? "missing");
                    if (reaction.MaxExpansion < 2)
                    {
                        Add(
                            issues,
                            reaction.Path + ".maxExpansion",
                            "effect_source_definition_reaction_expansion_invalid",
                            "expansion bound large enough for the reaction and its downstream definition",
                            reaction.MaxExpansion.ToString());
                    }
                    continue;
                }

                edges[definition.Key].Add(downstream.Key);
                ValidateDownstreamDefinition(definition, downstream, reaction, issues);
            }
            ValidateTriggerDispatchGraph(definition, issues);
        }

        ValidateAcyclic(edges, parsed, issues);
        foreach (var definition in parsed.Values)
        {
            foreach (var reaction in definition.Reactions)
            {
                var required = 1;
                if (HasBehavior(
                        reaction,
                        EffectReactionResultBehavior.ApplyDefinition) &&
                    reaction.DefinitionKey != null &&
                    parsed.ContainsKey(reaction.DefinitionKey))
                {
                    required += CountReachableDefinitions(
                        reaction.DefinitionKey,
                        edges,
                        new HashSet<string>(StringComparer.Ordinal));
                }
                if (reaction.MaxExpansion < required)
                {
                    Add(
                        issues,
                        reaction.Path + ".maxExpansion",
                        "effect_source_definition_reaction_expansion_invalid",
                        $"source-declared expansion bound >= {required} and <= {MaximumExpansion}",
                        reaction.MaxExpansion.ToString());
                }
            }
        }

        return issues;
    }

    private static Dictionary<string, ComponentNode> ReadComponents(
        JsonElement definition,
        string definitionPath)
    {
        var result = new Dictionary<string, ComponentNode>(StringComparer.Ordinal);
        if (!definition.TryGetProperty("components", out var components) ||
            components.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        var index = 0;
        foreach (var component in components.EnumerateArray())
        {
            var componentPath = $"{definitionPath}.components[{index++}]";
            if (component.ValueKind != JsonValueKind.Object ||
                !TryReadExact(component, "componentId", out var componentId) ||
                !TryReadExact(component, "profile", out var profile) ||
                result.ContainsKey(componentId))
            {
                continue;
            }
            result.Add(
                componentId,
                new ComponentNode(
                    componentId,
                    profile,
                    componentPath,
                    component.Clone()));
        }
        return result;
    }

    private static IReadOnlyList<TriggerNode> ReadTriggers(
        JsonElement definition,
        string definitionPath)
    {
        var result = new List<TriggerNode>();
        if (!definition.TryGetProperty("triggers", out var triggers) ||
            triggers.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        var index = 0;
        foreach (var trigger in triggers.EnumerateArray())
        {
            var triggerPath = $"{definitionPath}.triggers[{index++}]";
            if (trigger.ValueKind != JsonValueKind.Object ||
                !TryReadExact(trigger, "triggerId", out var triggerId) ||
                !TryReadExact(trigger, "eventType", out var eventType) ||
                !TryReadExact(trigger, "resolutionMode", out var resolutionMode))
            {
                continue;
            }
            result.Add(new TriggerNode(
                triggerId,
                eventType,
                resolutionMode,
                triggerPath,
                ReadExactSet(trigger, "componentIds")));
        }
        return result;
    }

    private static ReactionNode? ReadReaction(
        ComponentNode component,
        List<ValidationIssue> issues)
    {
        if (!component.Element.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            !TryReadExact(payload, "eventType", out var eventType) ||
            !TryReadExact(payload, "resultKind", out var resultKind) ||
            !TryReadExact(payload, "dependency", out var dependency) ||
            !TryReadPositiveInt(payload, "maxExpansion", out var maxExpansion))
        {
            return null;
        }
        return new ReactionNode(
            component.Id,
            component.Path + ".payload",
            eventType,
            resultKind,
            dependency,
            TryReadExact(payload, "definitionKey", out var definitionKey)
                ? definitionKey
                : null,
            TryReadExact(payload, "componentId", out var targetComponentId)
                ? targetComponentId
                : null,
            TryReadExact(payload, "afterComponentId", out var afterComponentId)
                ? afterComponentId
                : null,
            payload.TryGetProperty("parameters", out var parameters) &&
            parameters.ValueKind == JsonValueKind.Object
                ? parameters.Clone()
                : null,
            maxExpansion);
    }

    private static void ValidateOwningTrigger(
        DefinitionNode definition,
        ReactionNode reaction,
        List<ValidationIssue> issues)
    {
        var triggers = definition.Triggers
            .Where(trigger => trigger.ComponentIds.Contains(reaction.ComponentId))
            .ToArray();
        if (triggers.Length != 1)
        {
            Add(
                issues,
                reaction.Path,
                "effect_source_definition_reaction_trigger_ambiguous",
                "one exact owning trigger for every event-reaction component",
                triggers.Length.ToString());
            return;
        }

        var trigger = triggers[0];
        if (!string.Equals(
                trigger.EventType,
                reaction.EventType,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                reaction.Path + ".eventType",
                "effect_source_definition_reaction_event_mismatch",
                trigger.EventType,
                reaction.EventType);
        }
        if (!EffectReactionResultCatalog.TryResolve(
                reaction.ResultKind,
                out var descriptor))
        {
            Add(
                issues,
                reaction.Path + ".resultKind",
                "effect_source_definition_reaction_result_unsupported",
                "one registered executable reaction result",
                reaction.ResultKind);
            return;
        }
        var expectedMode = descriptor.ResolutionMode;
        if (!string.Equals(
                trigger.ResolutionMode,
                expectedMode,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                trigger.Path + ".resolutionMode",
                "effect_source_definition_reaction_resolution_mismatch",
                expectedMode,
                trigger.ResolutionMode);
        }
    }

    private static void ValidateComponentReferences(
        DefinitionNode definition,
        ReactionNode reaction,
        List<ValidationIssue> issues)
    {
        if (HasBehavior(
                reaction,
                EffectReactionResultBehavior.PeriodicComponent))
        {
            if (!EffectReactionResultCatalog.TryResolve(
                    reaction.ResultKind,
                    out var descriptor) ||
                !descriptor.AllowedDependencies.Contains(reaction.Dependency))
            {
                Add(
                    issues,
                    reaction.Path + ".dependency",
                    "effect_source_definition_reaction_component_dependency_invalid",
                    "after_component for a reaction-routed resource component; select an unconditional component directly in the trigger",
                    reaction.Dependency);
            }
            if (reaction.TargetComponentId == null ||
                !definition.Components.TryGetValue(
                    reaction.TargetComponentId,
                    out var target) ||
                !EffectComponentProfiles.IsPeriodicResourceProfile(target.Profile))
            {
                Add(
                    issues,
                    reaction.Path + ".componentId",
                    "effect_source_definition_reaction_reference_unresolved",
                    "one exact periodic resource component owned by this definition",
                    reaction.TargetComponentId ?? "missing");
            }
        }

        if (!string.Equals(
                reaction.Dependency,
                "after_component",
                StringComparison.Ordinal))
        {
            return;
        }
        if (reaction.AfterComponentId == null ||
            !definition.Components.TryGetValue(
                reaction.AfterComponentId,
                out var predecessor) ||
            !EffectComponentProfiles.IsPeriodicResourceProfile(predecessor.Profile))
        {
            Add(
                issues,
                reaction.Path + ".afterComponentId",
                "effect_source_definition_reaction_reference_unresolved",
                "one exact deterministic periodic predecessor component",
                reaction.AfterComponentId ?? "missing");
            return;
        }

        var owningTriggers = definition.Triggers
            .Where(trigger => trigger.ComponentIds.Contains(reaction.ComponentId))
            .ToArray();
        if (owningTriggers.Length == 1 &&
            !owningTriggers[0].ComponentIds.Contains(reaction.AfterComponentId))
        {
            Add(
                issues,
                reaction.Path + ".afterComponentId",
                "effect_source_definition_reaction_predecessor_unselected",
                "a predecessor selected by the same exact owning trigger",
                reaction.AfterComponentId);
        }
    }

    private static void ValidateDownstreamDefinition(
        DefinitionNode source,
        DefinitionNode downstream,
        ReactionNode reaction,
        List<ValidationIssue> issues)
    {
        if (!source.Realms.IsSubsetOf(downstream.Realms) ||
            !source.TargetKinds.IsSubsetOf(downstream.TargetKinds))
        {
            Add(
                issues,
                reaction.Path + ".definitionKey",
                "effect_source_definition_reaction_target_mismatch",
                "downstream definition covering every realm and target kind of the reacting definition",
                downstream.Key);
        }

        var sourceStackKey = ReadNestedExact(
            source.Element,
            "stacking",
            "stackKey");
        var downstreamStackKey = ReadNestedExact(
            downstream.Element,
            "stacking",
            "stackKey");
        var downstreamStackPolicy = ReadNestedExact(
            downstream.Element,
            "stacking",
            "policy");
        if (sourceStackKey != null &&
            string.Equals(sourceStackKey, downstreamStackKey, StringComparison.Ordinal) &&
            !string.Equals(downstreamStackPolicy, "replace", StringComparison.Ordinal))
        {
            Add(
                issues,
                reaction.Path + ".definitionKey",
                "effect_source_definition_reaction_stack_conflict",
                "a distinct downstream stack coordinate, or an explicit replace policy on the shared coordinate",
                downstream.Key + "/" + sourceStackKey + "/" + downstreamStackPolicy);
        }

        if (reaction.Parameters is not JsonElement parameters)
            return;
        var definitionNode = JsonNode.Parse(downstream.Element.GetRawText()) as JsonObject;
        var parameterNode = JsonNode.Parse(parameters.GetRawText()) as JsonObject;
        if (definitionNode == null || parameterNode == null)
            return;
        foreach (var issue in EffectSourceAuthority.ValidateDefinitionParameters(
                     definitionNode,
                     parameterNode))
        {
            var suffix = issue.FilePath.StartsWith("parameters", StringComparison.Ordinal)
                ? issue.FilePath["parameters".Length..]
                : string.Empty;
            Add(
                issues,
                reaction.Path + ".parameters" + suffix,
                issue.Code ?? "effect_source_definition_reaction_parameter_invalid",
                issue.Expected ?? "parameters inside downstream source-owned bounds",
                issue.Actual ?? "invalid");
        }
    }

    private static void ValidateTriggerDispatchGraph(
        DefinitionNode definition,
        List<ValidationIssue> issues)
    {
        var dependencyEdges = definition.Components.Keys.ToDictionary(
            static key => key,
            static _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        foreach (var trigger in definition.Triggers)
        {
            var dispatches = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var componentId in trigger.ComponentIds)
            {
                if (definition.Components.TryGetValue(componentId, out var component) &&
                    !string.Equals(
                        component.Profile,
                        "event_reaction",
                        StringComparison.Ordinal))
                {
                    dispatches[componentId] = "direct";
                }
            }

            foreach (var reaction in definition.Reactions.Where(reaction =>
                         trigger.ComponentIds.Contains(reaction.ComponentId) &&
                         HasBehavior(
                             reaction,
                             EffectReactionResultBehavior.PeriodicComponent) &&
                         reaction.TargetComponentId != null))
            {
                if (!dispatches.TryAdd(
                        reaction.TargetComponentId!,
                        reaction.ComponentId))
                {
                    Add(
                        issues,
                        reaction.Path + ".componentId",
                        "effect_source_definition_reaction_dispatch_ambiguous",
                        "one exact direct or reaction dispatch per component in an owning trigger",
                        reaction.TargetComponentId!);
                }
                if (string.Equals(
                        reaction.Dependency,
                        "after_component",
                        StringComparison.Ordinal) &&
                    reaction.AfterComponentId != null &&
                    dependencyEdges.ContainsKey(reaction.AfterComponentId) &&
                    dependencyEdges.ContainsKey(reaction.TargetComponentId!))
                {
                    dependencyEdges[reaction.AfterComponentId]
                        .Add(reaction.TargetComponentId!);
                }
            }
        }

        var inDegree = dependencyEdges.Keys.ToDictionary(
            static key => key,
            static _ => 0,
            StringComparer.Ordinal);
        foreach (var targets in dependencyEdges.Values)
        {
            foreach (var target in targets)
                inDegree[target]++;
        }
        var ready = new Queue<string>(inDegree
            .Where(static pair => pair.Value == 0)
            .Select(static pair => pair.Key));
        var visited = 0;
        while (ready.Count != 0)
        {
            var source = ready.Dequeue();
            visited++;
            foreach (var target in dependencyEdges[source])
            {
                inDegree[target]--;
                if (inDegree[target] == 0)
                    ready.Enqueue(target);
            }
        }
        if (visited != dependencyEdges.Count)
        {
            Add(
                issues,
                definition.Path + ".components",
                "effect_source_definition_reaction_component_cycle",
                "finite acyclic after-component dependency graph",
                $"visited={visited};components={dependencyEdges.Count}");
        }
    }

    private static void ValidateAcyclic(
        IReadOnlyDictionary<string, HashSet<string>> edges,
        IReadOnlyDictionary<string, DefinitionNode> definitions,
        List<ValidationIssue> issues)
    {
        var states = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in definitions.Keys.OrderBy(static value => value, StringComparer.Ordinal))
            Visit(key, edges, definitions, states, issues);
    }

    private static void Visit(
        string key,
        IReadOnlyDictionary<string, HashSet<string>> edges,
        IReadOnlyDictionary<string, DefinitionNode> definitions,
        Dictionary<string, int> states,
        List<ValidationIssue> issues)
    {
        if (states.GetValueOrDefault(key) == 2)
            return;
        if (states.GetValueOrDefault(key) == 1)
            return;
        states[key] = 1;
        foreach (var target in edges[key].OrderBy(static value => value, StringComparer.Ordinal))
        {
            if (states.GetValueOrDefault(target) == 1)
            {
                Add(
                    issues,
                    definitions[key].Path + ".components",
                    "effect_source_definition_reaction_cycle",
                    "finite acyclic downstream effect-definition graph",
                    key + " -> " + target);
                continue;
            }
            Visit(target, edges, definitions, states, issues);
        }
        states[key] = 2;
    }

    private static int CountReachableDefinitions(
        string key,
        IReadOnlyDictionary<string, HashSet<string>> edges,
        HashSet<string> visited)
    {
        if (!visited.Add(key))
            return 0;
        var count = 1;
        foreach (var target in edges[key])
            count += CountReachableDefinitions(target, edges, visited);
        return count;
    }

    private static HashSet<string> ReadExactSet(JsonElement value, string field)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (!value.TryGetProperty(field, out var array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return result;
        }
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String &&
                item.GetString() is { Length: > 0 } text &&
                string.Equals(text, text.Trim(), StringComparison.Ordinal))
            {
                result.Add(text);
            }
        }
        return result;
    }

    private static bool TryReadExact(
        JsonElement value,
        string field,
        out string result)
    {
        result = string.Empty;
        return value.TryGetProperty(field, out var node) &&
            node.ValueKind == JsonValueKind.String &&
            node.GetString() is { Length: > 0 } text &&
            string.Equals(text, text.Trim(), StringComparison.Ordinal) &&
            (result = text).Length > 0;
    }

    private static bool TryReadPositiveInt(
        JsonElement value,
        string field,
        out int result)
    {
        result = 0;
        return value.TryGetProperty(field, out var node) &&
            node.ValueKind == JsonValueKind.Number &&
            node.TryGetInt32(out result) &&
            result is >= 1 and <= MaximumExpansion;
    }

    private static bool HasBehavior(
        ReactionNode reaction,
        EffectReactionResultBehavior behavior) =>
        EffectReactionResultCatalog.TryResolve(reaction.ResultKind, out var descriptor) &&
        descriptor.Behavior == behavior;

    private static string? ReadNestedExact(
        JsonElement root,
        string objectField,
        string valueField) =>
        root.TryGetProperty(objectField, out var nested) &&
        nested.ValueKind == JsonValueKind.Object &&
        TryReadExact(nested, valueField, out var result)
            ? result
            : null;

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Static event-reaction policy is not a closed executable graph.",
            code: code,
            section: "effect_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Use one exact same-source reaction reference, a matching execution mode, and a finite acyclic expansion bound."));

    private sealed record ComponentNode(
        string Id,
        string Profile,
        string Path,
        JsonElement Element);

    private sealed record TriggerNode(
        string Id,
        string EventType,
        string ResolutionMode,
        string Path,
        HashSet<string> ComponentIds);

    private sealed record ReactionNode(
        string ComponentId,
        string Path,
        string EventType,
        string ResultKind,
        string Dependency,
        string? DefinitionKey,
        string? TargetComponentId,
        string? AfterComponentId,
        JsonElement? Parameters,
        int MaxExpansion);

    private sealed record DefinitionNode(
        string Key,
        string Path,
        JsonElement Element,
        HashSet<string> Realms,
        HashSet<string> TargetKinds,
        Dictionary<string, ComponentNode> Components,
        IReadOnlyList<TriggerNode> Triggers,
        IReadOnlyList<ReactionNode> Reactions);
}
