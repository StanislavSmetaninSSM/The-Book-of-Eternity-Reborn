using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record EffectReactionExecution(
    string EventRef,
    string TriggerEventRef,
    string CausalEventRef,
    int Turn,
    string EventKind,
    EffectTargetKey Target,
    string EffectId,
    string TriggerId,
    string ComponentId,
    string ResultKind,
    string Dependency,
    string? AfterComponentId,
    int MaxExpansion,
    EffectSourceAuthorityEntry? DownstreamSource,
    JsonObject? Parameters,
    EffectSourceKey? DownstreamSourceKey = null,
    int ComponentPriority = 0,
    EffectReplayIdentity? ReplacementTarget = null)
{
    internal string? ReplacementTargetEffectId => ReplacementTarget?.EffectId;
}

internal sealed record EffectReactionPlanningResult(
    IReadOnlyList<EffectReactionExecution> Executions,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success => Issues.Count == 0;
}

internal sealed record EffectReactionExpansionKey(
    string EffectId,
    string ComponentId);

internal sealed record EffectReactionExpansionUsage(
    int Count,
    int Maximum);

internal sealed record EffectExecutableComponentSelection(
    string ComponentId,
    string? AfterComponentId,
    JsonObject? IndexedComponent = null);

internal static class EffectReactionExecutor
{
    internal static EffectReactionPlanningResult PlanResourceEvent(
        EffectCarrierOccurrence occurrence,
        string triggerId,
        ResourceAppliedEvent producerEvent,
        EffectSourceAuthority sourceAuthority,
        WoundReactionLineageAuthority? woundLineageAuthority = null)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerId);
        ArgumentNullException.ThrowIfNull(producerEvent);
        ArgumentNullException.ThrowIfNull(sourceAuthority);

        var replacementTargets = EffectReplacementTargetIndex.Build(
            new[] { occurrence });

        var issues = new List<ValidationIssue>();
        if (!EffectEventTypeCatalog.IsResourceEvent(producerEvent.EventKind) ||
            producerEvent.Turn <= 0 ||
            !TryReadExactValue(producerEvent.EventRef))
        {
            Add(
                issues,
                "resourceEvent",
                "effect_reaction_event_invalid",
                "one exact emitted common-resource event",
                producerEvent.EventKind + "/" + producerEvent.EventRef);
            return new EffectReactionPlanningResult(
                Array.Empty<EffectReactionExecution>(),
                issues);
        }
        if (!HasExact(occurrence.Effect, "state", "active") ||
            !TryReadExact(occurrence.Effect["realm"], out var realm) ||
            occurrence.Effect["target"] is not JsonObject target ||
            !TryReadExact(target["kind"], out var targetKind) ||
            !TryReadExact(target["targetId"], out var targetId) ||
            occurrence.Effect["triggers"] is not JsonArray triggers ||
            occurrence.Effect["components"] is not JsonArray components)
        {
            Add(
                issues,
                occurrence.JsonPath,
                "effect_reaction_target_unresolved",
                "one exact active canonical effect occurrence",
                occurrence.EffectId);
            return new EffectReactionPlanningResult(
                Array.Empty<EffectReactionExecution>(),
                issues);
        }

        var matchingTriggers = triggers.OfType<JsonObject>()
            .Where(trigger =>
                HasExact(trigger, "triggerId", triggerId) &&
                HasExact(trigger, "eventType", producerEvent.EventKind))
            .ToArray();
        if (matchingTriggers.Length != 1 ||
            matchingTriggers[0]["componentIds"] is not JsonArray selectedIds)
        {
            Add(
                issues,
                occurrence.JsonPath + ".triggers",
                "effect_reaction_trigger_unresolved",
                "one exact trigger for the emitted resource event",
                triggerId);
            return new EffectReactionPlanningResult(
                Array.Empty<EffectReactionExecution>(),
                issues);
        }

        var acceptedEvent = new AcceptedEvent(
            producerEvent.EventRef,
            producerEvent.EventRef,
            producerEvent.Turn,
            producerEvent.EventKind,
            new EffectTargetKey(realm, targetKind, targetId),
            occurrence.EffectId,
            triggerId);
        var executions = new List<EffectReactionExecution>();
        foreach (var component in components.OfType<JsonObject>()
                     .Where(component =>
                         HasExact(component, "profile", "event_reaction") &&
                         selectedIds.OfType<JsonValue>().Any(id =>
                             id.TryGetValue<string>(out var selectedId) &&
                             HasExact(component, "componentId", selectedId)))
                     .OrderBy(component => ReadInt(component["priority"]))
                     .ThenBy(
                         component => component["componentId"]!.GetValue<string>(),
                         StringComparer.Ordinal))
        {
            if (TryBuildExecution(
                    occurrence,
                    triggerId,
                    component,
                    acceptedEvent,
                    sourceAuthority,
                    replacementTargets,
                    issues,
                    workMeter: null,
                    woundLineageAuthority: woundLineageAuthority,
                    out var execution))
            {
                executions.Add(execution);
            }
        }

        return issues.Count == 0
            ? new EffectReactionPlanningResult(executions.ToArray(), Array.Empty<ValidationIssue>())
            : new EffectReactionPlanningResult(
                Array.Empty<EffectReactionExecution>(),
                issues.ToArray());
    }

    internal static EffectReactionPlanningResult PlanResourceEvent(
        EffectCarrierOccurrence occurrence,
        string triggerId,
        JsonObject exactTrigger,
        IReadOnlyDictionary<string, JsonObject> componentsById,
        ResourceAppliedEvent producerEvent,
        EffectSourceAuthority sourceAuthority,
        EffectReplacementTargetIndex replacementTargets,
        EffectAcceptedTurnPlanner.EffectResourceRoutingWorkMeter workMeter,
        WoundReactionLineageAuthority? woundLineageAuthority = null)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerId);
        ArgumentNullException.ThrowIfNull(exactTrigger);
        ArgumentNullException.ThrowIfNull(componentsById);
        ArgumentNullException.ThrowIfNull(producerEvent);
        ArgumentNullException.ThrowIfNull(sourceAuthority);
        ArgumentNullException.ThrowIfNull(replacementTargets);
        ArgumentNullException.ThrowIfNull(workMeter);

        var issues = new List<ValidationIssue>();
        if (!EffectEventTypeCatalog.IsResourceEvent(producerEvent.EventKind) ||
            producerEvent.Turn <= 0 ||
            !TryReadExactValue(producerEvent.EventRef))
        {
            Add(
                issues,
                "resourceEvent",
                "effect_reaction_event_invalid",
                "one exact emitted common-resource event",
                producerEvent.EventKind + "/" + producerEvent.EventRef);
            return new EffectReactionPlanningResult(
                Array.Empty<EffectReactionExecution>(),
                issues);
        }
        if (!HasExact(occurrence.Effect, "state", "active") ||
            !TryReadExact(occurrence.Effect["realm"], out var realm) ||
            occurrence.Effect["target"] is not JsonObject target ||
            !TryReadExact(target["kind"], out var targetKind) ||
            !TryReadExact(target["targetId"], out var targetId) ||
            occurrence.Effect["triggers"] is not JsonArray ||
            occurrence.Effect["components"] is not JsonArray)
        {
            Add(
                issues,
                occurrence.JsonPath,
                "effect_reaction_target_unresolved",
                "one exact active canonical effect occurrence",
                occurrence.EffectId);
            return new EffectReactionPlanningResult(
                Array.Empty<EffectReactionExecution>(),
                issues);
        }
        if (!HasExact(exactTrigger, "triggerId", triggerId) ||
            !HasExact(exactTrigger, "eventType", producerEvent.EventKind) ||
            exactTrigger["componentIds"] is not JsonArray selectedIds)
        {
            Add(
                issues,
                occurrence.JsonPath + ".triggers",
                "effect_reaction_trigger_unresolved",
                "one exact trigger for the emitted resource event",
                triggerId);
            return new EffectReactionPlanningResult(
                Array.Empty<EffectReactionExecution>(),
                issues);
        }

        var selectedReactions = new Dictionary<string, JsonObject>(
            StringComparer.Ordinal);
        foreach (var idNode in selectedIds)
        {
            workMeter.RecordSelectedComponentVisit();
            if (!TryReadExact(idNode, out var componentId))
                continue;
            workMeter.RecordComponentIndexLookup();
            if (componentsById.TryGetValue(componentId, out var component) &&
                HasExact(component, "profile", "event_reaction"))
            {
                selectedReactions.TryAdd(componentId, component);
            }
        }

        var acceptedEvent = new AcceptedEvent(
            producerEvent.EventRef,
            producerEvent.EventRef,
            producerEvent.Turn,
            producerEvent.EventKind,
            new EffectTargetKey(realm, targetKind, targetId),
            occurrence.EffectId,
            triggerId);
        var executions = new List<EffectReactionExecution>();
        foreach (var component in selectedReactions.Values
                     .OrderBy(component => ReadInt(component["priority"]))
                     .ThenBy(
                         component => component["componentId"]!.GetValue<string>(),
                         StringComparer.Ordinal))
        {
            if (TryBuildExecution(
                    occurrence,
                    triggerId,
                    component,
                    acceptedEvent,
                    sourceAuthority,
                    replacementTargets,
                    issues,
                    workMeter,
                    woundLineageAuthority,
                    out var execution))
            {
                executions.Add(execution);
            }
        }

        return issues.Count == 0
            ? new EffectReactionPlanningResult(
                executions.ToArray(),
                Array.Empty<ValidationIssue>())
            : new EffectReactionPlanningResult(
                Array.Empty<EffectReactionExecution>(),
                issues.ToArray());
    }

    internal static IReadOnlyList<EffectExecutableComponentSelection>
        ResolveExecutableComponents(
        JsonObject effect,
        JsonObject trigger,
        string eventType,
        List<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(issues);

        var result = new Dictionary<string, EffectExecutableComponentSelection>(
            StringComparer.Ordinal);
        if (effect["components"] is not JsonArray components ||
            trigger["componentIds"] is not JsonArray selectedIds)
        {
            return Array.Empty<EffectExecutableComponentSelection>();
        }

        foreach (var idNode in selectedIds)
        {
            if (!TryReadExact(idNode, out var componentId))
                continue;
            var matches = components.OfType<JsonObject>()
                .Where(component => HasExact(component, "componentId", componentId))
                .ToArray();
            if (matches.Length != 1)
                continue;
            var component = matches[0];
            if (!HasExact(component, "profile", "event_reaction"))
            {
                if (!result.TryAdd(
                        componentId,
                        new EffectExecutableComponentSelection(componentId, null)))
                {
                    Add(
                        issues,
                        "effect.triggers.componentIds",
                        "effect_reaction_component_dispatch_ambiguous",
                        "one exact dispatch path per executable component",
                        componentId);
                }
                continue;
            }
            if (component["payload"] is not JsonObject payload ||
                !HasExact(payload, "eventType", eventType) ||
                !TryReadExact(payload["resultKind"], out var resultKind) ||
                !EffectReactionResultCatalog.TryResolve(resultKind, out var descriptor) ||
                descriptor.Behavior != EffectReactionResultBehavior.PeriodicComponent ||
                !TryReadExact(payload["componentId"], out var targetComponentId))
            {
                continue;
            }
            var targetMatches = components.OfType<JsonObject>()
                .Where(candidate => HasExact(
                    candidate,
                    "componentId",
                    targetComponentId))
                .ToArray();
            if (targetMatches.Length != 1 ||
                !TryReadExact(targetMatches[0]["profile"], out var targetProfile) ||
                targetProfile is not ("periodic_damage" or "periodic_restore"))
            {
                Add(
                    issues,
                    "effect.components[" + componentId + "].payload.componentId",
                    "effect_reaction_component_unresolved",
                    "one exact executable periodic component owned by this effect",
                    targetComponentId);
                continue;
            }
            var afterComponentId = HasExact(
                    payload,
                    "dependency",
                    "after_component")
                ? ReadOptionalExact(payload["afterComponentId"])
                : null;
            if (!result.TryAdd(
                    targetComponentId,
                    new EffectExecutableComponentSelection(
                        targetComponentId,
                        afterComponentId)))
            {
                Add(
                    issues,
                    "effect.components[" + componentId + "].payload.componentId",
                    "effect_reaction_component_dispatch_ambiguous",
                    "one exact dispatch path per executable component",
                    targetComponentId);
            }
        }

        return result.Values
            .OrderBy(static value => value.ComponentId, StringComparer.Ordinal)
            .ToArray();
    }

    internal static IReadOnlyList<EffectExecutableComponentSelection>
        ResolveExecutableComponents(
        JsonObject trigger,
        string eventType,
        IReadOnlyDictionary<string, JsonObject> componentsById,
        List<ValidationIssue> issues,
        EffectAcceptedTurnPlanner.EffectResourceRoutingWorkMeter workMeter)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(componentsById);
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentNullException.ThrowIfNull(workMeter);

        var result = new Dictionary<string, EffectExecutableComponentSelection>(
            StringComparer.Ordinal);
        if (trigger["componentIds"] is not JsonArray selectedIds)
            return Array.Empty<EffectExecutableComponentSelection>();

        foreach (var idNode in selectedIds)
        {
            workMeter.RecordSelectedComponentVisit();
            if (!TryReadExact(idNode, out var componentId))
                continue;
            workMeter.RecordComponentIndexLookup();
            if (!componentsById.TryGetValue(componentId, out var component))
                continue;
            if (!HasExact(component, "profile", "event_reaction"))
            {
                if (!result.TryAdd(
                        componentId,
                        new EffectExecutableComponentSelection(
                            componentId,
                            null,
                            component)))
                {
                    Add(
                        issues,
                        "effect.triggers.componentIds",
                        "effect_reaction_component_dispatch_ambiguous",
                        "one exact dispatch path per executable component",
                        componentId);
                }
                continue;
            }
            if (component["payload"] is not JsonObject payload ||
                !HasExact(payload, "eventType", eventType) ||
                !TryReadExact(payload["resultKind"], out var resultKind) ||
                !EffectReactionResultCatalog.TryResolve(resultKind, out var descriptor) ||
                descriptor.Behavior != EffectReactionResultBehavior.PeriodicComponent ||
                !TryReadExact(payload["componentId"], out var targetComponentId))
            {
                continue;
            }
            workMeter.RecordComponentIndexLookup();
            if (!componentsById.TryGetValue(
                    targetComponentId,
                    out var targetComponent) ||
                !TryReadExact(targetComponent["profile"], out var targetProfile) ||
                targetProfile is not ("periodic_damage" or "periodic_restore"))
            {
                Add(
                    issues,
                    "effect.components[" + componentId + "].payload.componentId",
                    "effect_reaction_component_unresolved",
                    "one exact executable periodic component owned by this effect",
                    targetComponentId);
                continue;
            }
            var afterComponentId = HasExact(
                    payload,
                    "dependency",
                    "after_component")
                ? ReadOptionalExact(payload["afterComponentId"])
                : null;
            if (!result.TryAdd(
                    targetComponentId,
                    new EffectExecutableComponentSelection(
                        targetComponentId,
                        afterComponentId,
                        targetComponent)))
            {
                Add(
                    issues,
                    "effect.components[" + componentId + "].payload.componentId",
                    "effect_reaction_component_dispatch_ambiguous",
                    "one exact dispatch path per executable component",
                    targetComponentId);
            }
        }

        return result.Values
            .OrderBy(static value => value.ComponentId, StringComparer.Ordinal)
            .ToArray();
    }

    internal static EffectReactionPlanningResult Plan(
        JsonObject eventInput,
        EffectSourceAuthority sourceAuthority,
        EffectCarrierCatalogInput carriers,
        WoundReactionLineageAuthority? woundLineageAuthority = null)
    {
        ArgumentNullException.ThrowIfNull(eventInput);
        ArgumentNullException.ThrowIfNull(sourceAuthority);
        ArgumentNullException.ThrowIfNull(carriers);

        var issues = new List<ValidationIssue>();
        var catalog = EffectCarrierCatalog.Build(carriers);
        issues.AddRange(catalog.Issues);
        var replacementTargets = EffectReplacementTargetIndex.Build(
            catalog.Occurrences);
        var executions = new List<EffectReactionExecution>();
        if (eventInput["lifecycleEvents"] is not JsonArray lifecycleEvents)
        {
            return new EffectReactionPlanningResult(
                Array.Empty<EffectReactionExecution>(),
                issues);
        }

        foreach (var eventNode in lifecycleEvents)
        {
            if (!TryParseEvent(eventNode, issues, out var acceptedEvent))
                continue;
            var occurrences = catalog.Occurrences
                .Where(occurrence =>
                    (acceptedEvent.EffectId == null ||
                     string.Equals(
                         occurrence.EffectId,
                         acceptedEvent.EffectId,
                         StringComparison.Ordinal)) &&
                    string.Equals(
                        occurrence.Effect["state"]?.GetValue<string>(),
                        "active",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        occurrence.Effect["realm"]?.GetValue<string>(),
                        acceptedEvent.Target.Realm,
                        StringComparison.Ordinal) &&
                    occurrence.Effect["target"] is JsonObject target &&
                    HasExact(target, "kind", acceptedEvent.Target.Kind) &&
                    HasExact(target, "targetId", acceptedEvent.Target.TargetId))
                .OrderBy(static occurrence => occurrence.EffectId, StringComparer.Ordinal)
                .ToArray();
            foreach (var occurrence in occurrences)
            {
                if (occurrence.Effect["triggers"] is not JsonArray triggers ||
                    occurrence.Effect["components"] is not JsonArray components)
                {
                    continue;
                }
                var matchingTriggers = triggers.OfType<JsonObject>()
                    .Where(trigger =>
                        (acceptedEvent.TriggerId == null ||
                         HasExact(trigger, "triggerId", acceptedEvent.TriggerId)) &&
                        HasExact(trigger, "eventType", acceptedEvent.Phase))
                    .OrderBy(trigger => ReadInt(trigger["priority"]))
                    .ThenBy(
                        trigger => trigger["triggerId"]!.GetValue<string>(),
                        StringComparer.Ordinal)
                    .ToArray();
                foreach (var trigger in matchingTriggers)
                {
                    if (!TryReadExact(trigger["triggerId"], out var triggerId) ||
                        trigger["componentIds"] is not JsonArray componentIds)
                    {
                        continue;
                    }
                    foreach (var component in components.OfType<JsonObject>()
                                 .Where(component =>
                                     componentIds.OfType<JsonValue>().Any(id =>
                                         id.TryGetValue<string>(out var selectedId) &&
                                         HasExact(component, "componentId", selectedId)) &&
                                     HasExact(component, "profile", "event_reaction"))
                                 .OrderBy(component => ReadInt(component["priority"]))
                                 .ThenBy(
                                     component => component["componentId"]!.GetValue<string>(),
                                     StringComparer.Ordinal))
                    {
                        if (!TryBuildExecution(
                                occurrence,
                                triggerId,
                                component,
                                acceptedEvent,
                                sourceAuthority,
                                replacementTargets,
                                issues,
                                workMeter: null,
                                woundLineageAuthority: woundLineageAuthority,
                                out var execution))
                        {
                            continue;
                        }
                        executions.Add(execution);
                    }
                }
            }
        }

        return issues.Count == 0
            ? new EffectReactionPlanningResult(
                executions
                    .OrderBy(static execution => execution.EventRef, StringComparer.Ordinal)
                    .ThenBy(static execution => execution.EffectId, StringComparer.Ordinal)
                    .ThenBy(static execution => execution.ComponentId, StringComparer.Ordinal)
                    .ToArray(),
                Array.Empty<ValidationIssue>())
            : new EffectReactionPlanningResult(
                Array.Empty<EffectReactionExecution>(),
                issues.ToArray());
    }

    internal static IReadOnlyDictionary<
        EffectReactionExpansionKey,
        EffectReactionExpansionUsage> CreateExpansionUsage(
        IEnumerable<EffectReactionExecution> executions)
    {
        ArgumentNullException.ThrowIfNull(executions);
        return executions
            .GroupBy(static execution => new EffectReactionExpansionKey(
                execution.EffectId,
                execution.ComponentId))
            .ToDictionary(
                static group => group.Key,
                static group => new EffectReactionExpansionUsage(
                    group.Count(),
                    group.Min(static execution => execution.MaxExpansion)));
    }

    private static bool TryBuildExecution(
        EffectCarrierOccurrence occurrence,
        string triggerId,
        JsonObject component,
        AcceptedEvent acceptedEvent,
        EffectSourceAuthority sourceAuthority,
        EffectReplacementTargetIndex replacementTargets,
        List<ValidationIssue> issues,
        EffectAcceptedTurnPlanner.EffectResourceRoutingWorkMeter? workMeter,
        WoundReactionLineageAuthority? woundLineageAuthority,
        out EffectReactionExecution execution)
    {
        execution = null!;
        if (!TryReadExact(component["componentId"], out var componentId) ||
            component["payload"] is not JsonObject payload ||
            !TryReadExact(payload["eventType"], out var eventType) ||
            !string.Equals(eventType, acceptedEvent.Phase, StringComparison.Ordinal) ||
            !TryReadExact(payload["resultKind"], out var resultKind) ||
            !EffectReactionResultCatalog.TryResolve(resultKind, out var descriptor) ||
            !TryReadExact(payload["dependency"], out var dependency) ||
            !descriptor.AllowedDependencies.Contains(dependency) ||
            !TryReadPositiveInt(payload["maxExpansion"], out var maxExpansion))
        {
            Add(
                issues,
                occurrence.JsonPath + ".components",
                "effect_reaction_policy_invalid",
                "one complete source-owned event reaction matching the accepted event",
                component.ToJsonString());
            return false;
        }

        EffectSourceAuthorityEntry? downstream = null;
        EffectSourceKey? downstreamKey = null;
        string? downstreamStackKey = null;
        string? downstreamStackPolicy = null;
        JsonObject? parameters = null;
        if (descriptor.Behavior == EffectReactionResultBehavior.ApplyDefinition)
        {
            if (occurrence.Effect["source"] is not JsonObject source ||
                !TryReadExact(source["kind"], out var sourceKind) ||
                !TryReadExact(source["sourceId"], out var sourceId) ||
                !TryReadExact(payload["definitionKey"], out var definitionKey) ||
                payload["parameters"] is not JsonObject reactionParameters)
            {
                Add(
                    issues,
                    occurrence.JsonPath + ".components[" + componentId + "].payload",
                    "effect_reaction_downstream_source_invalid",
                    "exact same-source downstream definition and closed parameters",
                    payload.ToJsonString());
                return false;
            }
            var sourceKey = new EffectSourceKey(
                acceptedEvent.Target.Realm,
                sourceKind,
                sourceId,
                definitionKey);
            IReadOnlyList<ValidationIssue> parameterIssues;
            if (string.Equals(sourceKind, "wound", StringComparison.Ordinal))
            {
                if (woundLineageAuthority is null)
                {
                    Add(
                        issues,
                        occurrence.JsonPath + ".components[" + componentId + "].payload",
                        "effect_reaction_wound_lineage_authority_missing",
                        "sealed typed wound lineage authority for every wound-owned apply_definition reaction",
                        sourceKey.ToString());
                    return false;
                }
                var woundResolution = woundLineageAuthority.ResolveApplyDefinition(
                    occurrence,
                    component,
                    acceptedEvent.Target,
                    definitionKey);
                issues.AddRange(woundResolution.Issues);
                if (!woundResolution.Success)
                    return false;
                downstream = woundResolution.Source;
                downstreamKey = woundResolution.Source!.Key;
                downstreamStackKey = woundResolution.Source.Definition["stacking"]?
                    ["stackKey"]?.GetValue<string>();
                downstreamStackPolicy = woundResolution.Source.Definition["stacking"]?
                    ["policy"]?.GetValue<string>();
                parameterIssues = sourceAuthority.ValidateCanonicalParameters(
                    woundResolution.Source,
                    reactionParameters);
            }
            else if (workMeter != null)
            {
                var routingResolution =
                    sourceAuthority.ResolveCanonicalRoutingBinding(
                        sourceKey,
                        acceptedEvent.Target.Kind,
                        workMeter);
                issues.AddRange(routingResolution.Issues);
                if (!routingResolution.Success)
                    return false;
                var routingSource = routingResolution.Source!.Value;
                parameterIssues =
                    sourceAuthority.ValidateCanonicalRoutingParameters(
                        routingSource,
                        reactionParameters);
                downstreamKey = routingSource.Key;
                downstreamStackKey = routingSource.StackKey;
                downstreamStackPolicy = routingSource.StackPolicy;
            }
            else
            {
                var resolution = sourceAuthority.ResolveCanonicalBinding(
                    sourceKey,
                    acceptedEvent.Target.Kind);
                issues.AddRange(resolution.Issues);
                if (!resolution.Success)
                    return false;
                parameterIssues = sourceAuthority.ValidateCanonicalParameters(
                    resolution.Source!,
                    reactionParameters);
                downstream = resolution.Source;
                downstreamKey = resolution.Source!.Key;
                downstreamStackKey = resolution.Source.Definition["stacking"]?
                    ["stackKey"]?.GetValue<string>();
                downstreamStackPolicy = resolution.Source.Definition["stacking"]?
                    ["policy"]?.GetValue<string>();
            }
            issues.AddRange(parameterIssues);
            if (parameterIssues.Count != 0)
                return false;
            parameters = reactionParameters.DeepClone().AsObject();
        }

        var replacementTarget = downstreamKey == null
            ? null
            : replacementTargets.ResolveExactTarget(
                acceptedEvent.Target,
                downstreamKey,
                downstreamStackKey,
                downstreamStackPolicy,
                acceptedEvent.Turn);
        var effectAuthority = EffectAcceptedTurnPlanner
            .ResolvePendingEffectAuthority(
                occurrence.Effect,
                acceptedEvent.Turn);
        execution = new EffectReactionExecution(
            CreateReactionEventRef(
                acceptedEvent.EventRef,
                occurrence.EffectId,
                effectAuthority,
                triggerId,
                componentId),
            acceptedEvent.EventRef,
            acceptedEvent.CausalEventRef ?? acceptedEvent.EventRef,
            acceptedEvent.Turn,
            acceptedEvent.Phase,
            acceptedEvent.Target,
            occurrence.EffectId,
            triggerId,
            componentId,
            resultKind,
            dependency,
            ReadOptionalExact(payload["afterComponentId"]),
            maxExpansion,
            downstream,
            parameters,
            downstreamKey,
            ReadInt(component["priority"]),
            replacementTarget);
        return true;
    }

    internal static string CreateReactionEventRef(
        string acceptedEventRef,
        string effectId,
        ResourcePendingAuthorityBinding effectAuthority,
        string triggerId,
        string componentId)
    {
        using var builder = new ResourceFingerprintBuilder(
            "effect-reaction-event-ref-v1");
        builder.Append(acceptedEventRef);
        EffectAcceptedTurnPlanner.AppendPendingEffectReplayIdentity(
            builder,
            effectId,
            effectAuthority);
        builder.Append(triggerId);
        builder.Append(componentId);
        return "effect_reaction_event_" + builder.Build()["sha256:".Length..];
    }

    private static bool TryParseEvent(
        JsonNode? node,
        List<ValidationIssue> issues,
        out AcceptedEvent acceptedEvent)
    {
        acceptedEvent = null!;
        if (node is not JsonObject value ||
            !TryReadExact(value["eventRef"], out var eventRef) ||
            !TryReadPositiveInt(value["turn"], out var turn) ||
            !TryReadExact(value["phase"], out var phase) ||
            !TryReadExact(value["realm"], out var realm) ||
            value["target"] is not JsonObject target ||
            !TryReadExact(target["kind"], out var targetKind) ||
            !TryReadExact(target["targetId"], out var targetId))
        {
            Add(
                issues,
                "eventInput.lifecycleEvents",
                "effect_reaction_event_invalid",
                "closed client-derived lifecycle event authority",
                node?.ToJsonString() ?? "null");
            return false;
        }
        acceptedEvent = new AcceptedEvent(
            eventRef,
            ReadOptionalExact(value["causalEventRef"]),
            turn,
            phase,
            new EffectTargetKey(realm, targetKind, targetId),
            ReadOptionalExact(value["effectId"]),
            ReadOptionalExact(value["triggerId"]));
        return true;
    }

    private static int ReadInt(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<int>(out var number)
            ? number
            : 0;

    private static bool TryReadExactValue(string value) =>
        !string.IsNullOrEmpty(value) &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool HasExact(JsonObject value, string field, string expected) =>
        TryReadExact(value[field], out var actual) &&
        string.Equals(actual, expected, StringComparison.Ordinal);

    private static string? ReadOptionalExact(JsonNode? node) =>
        TryReadExact(node, out var value) ? value : null;

    private static bool TryReadExact(JsonNode? node, out string result)
    {
        result = string.Empty;
        return node is JsonValue value &&
            value.TryGetValue<string>(out var text) &&
            !string.IsNullOrEmpty(text) &&
            string.Equals(text, text.Trim(), StringComparison.Ordinal) &&
            (result = text).Length > 0;
    }

    private static bool TryReadPositiveInt(JsonNode? node, out int result)
    {
        result = 0;
        return node is JsonValue value &&
            value.TryGetValue<int>(out result) &&
            result > 0;
    }

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Accepted event reaction cannot produce one exact bounded after-image.",
            code: code,
            section: "effect_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Keep event-reaction identity and mechanics client-owned; repair the source definition or sealed event evidence."));

    private sealed record AcceptedEvent(
        string EventRef,
        string? CausalEventRef,
        int Turn,
        string Phase,
        EffectTargetKey Target,
        string? EffectId,
        string? TriggerId);
}
