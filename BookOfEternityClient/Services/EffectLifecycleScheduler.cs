using System.Globalization;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record EffectStackApplicationInput(
    IReadOnlyList<JsonObject> ExistingEffects,
    JsonObject Definition,
    JsonArray BoundComponents,
    JsonObject InitialLifetime,
    string EventRef,
    IReadOnlySet<string> ProcessedEventRefs);

internal sealed record EffectStackApplicationResult(
    string Outcome,
    bool CreatesNewIdentity,
    bool TerminatesExisting,
    string? ExistingEffectId,
    JsonObject? UpdatedExistingEffect,
    JsonObject NewEffectStacking,
    JsonArray NewEffectComponents,
    JsonObject NewEffectLifetime,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success => Issues.Count == 0;
}

internal sealed record EffectLifecycleEvent(
    string EventRef,
    int Turn,
    string? Phase = null,
    string? TriggerId = null,
    long? CurrentTime = null,
    string? CurrentSceneId = null,
    bool SceneClosed = false,
    bool? SourceSatisfied = null,
    bool? ConditionSatisfied = null,
    string? CurrentRealm = null,
    string? CausalEventRef = null,
    bool? TargetSatisfied = null);

internal sealed record EffectLifetimeReductionInput(
    JsonObject Effect,
    EffectLifecycleEvent Event,
    IReadOnlySet<string> ProcessedEventRefs);

internal sealed record EffectLifetimeReductionResult(
    string Outcome,
    JsonObject? UpdatedEffect,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success => Issues.Count == 0;
}

internal static class EffectLifecycleScheduler
{
    private const int ForbiddenNumericSentinel = 999;

    internal static EffectStackApplicationResult ResolveApplication(
        EffectStackApplicationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.ExistingEffects);
        ArgumentNullException.ThrowIfNull(input.Definition);
        ArgumentNullException.ThrowIfNull(input.BoundComponents);
        ArgumentNullException.ThrowIfNull(input.InitialLifetime);
        ArgumentNullException.ThrowIfNull(input.ProcessedEventRefs);

        var issues = new List<ValidationIssue>();
        ValidateEvent(input.EventRef, input.ProcessedEventRefs, issues);
        var stacking = input.Definition["stacking"] as JsonObject;
        if (stacking == null ||
            !TryExact(stacking["stackKey"], out var stackKey) ||
            !TryExact(stacking["policy"], out var policy) ||
            !TryPositiveInt(stacking["maxStacks"], out var maxStacks) ||
            !TryExact(stacking["atMaximum"], out var atMaximum))
        {
            Add(
                issues,
                "source.stacking",
                "effect_lifecycle_stack_policy_invalid",
                "complete source-owned stackKey/policy/maxStacks/atMaximum policy",
                Describe(stacking));
            return FailedStack(input, issues);
        }

        if (policy is not ("independent" or "stack" or "refresh" or "replace" or "merge"))
        {
            Add(
                issues,
                "source.stacking.policy",
                "effect_lifecycle_stack_policy_invalid",
                "independent, stack, refresh, replace, or merge",
                policy);
        }
        if (atMaximum is not ("no_change" or "refresh" or "component_response"))
        {
            Add(
                issues,
                "source.stacking.atMaximum",
                "effect_lifecycle_stack_policy_invalid",
                "no_change, refresh, or component_response",
                atMaximum);
        }

        var newStacking = stacking.DeepClone().AsObject();
        newStacking.Remove("atMaximum");
        newStacking["currentStacks"] = 1;
        if (string.Equals(policy, "independent", StringComparison.Ordinal))
            newStacking["maxStacks"] = 1;
        var newComponents = input.BoundComponents.DeepClone().AsArray();
        var newLifetime = input.InitialLifetime.DeepClone().AsObject();
        var existing = input.ExistingEffects
            .Select(static effect => effect.DeepClone().AsObject())
            .OrderBy(static effect => ReadExact(effect["effectId"]), StringComparer.Ordinal)
            .ToArray();

        foreach (var effect in existing)
            ValidateExistingStack(
                effect,
                stacking,
                policy,
                stackKey,
                maxStacks,
                issues);
        if (!string.Equals(policy, "independent", StringComparison.Ordinal) && existing.Length > 1)
        {
            Add(
                issues,
                "activeEffects",
                "effect_lifecycle_stack_coordinate_ambiguous",
                "at most one active identity for a non-independent stack coordinate",
                existing.Length.ToString());
        }
        if (issues.Count > 0)
            return FailedStack(input, issues, newStacking, newComponents, newLifetime);

        if (existing.Length == 0)
            return StackResult("create", true, false, null, null, newStacking, newComponents, newLifetime);

        if (string.Equals(policy, "independent", StringComparison.Ordinal))
        {
            if (existing.Length < maxStacks)
                return StackResult("create", true, false, null, null, newStacking, newComponents, newLifetime);
            if (string.Equals(atMaximum, "no_change", StringComparison.Ordinal))
            {
                var retained = existing[0];
                return StackResult(
                    "no_change",
                    false,
                    false,
                    ReadExact(retained["effectId"]),
                    retained,
                    newStacking,
                    newComponents,
                    newLifetime);
            }
            Add(
                issues,
                "source.stacking.atMaximum",
                "effect_lifecycle_independent_maximum_policy_unsupported",
                "no_change for a bounded independent instance set",
                atMaximum);
            return FailedStack(input, issues, newStacking, newComponents, newLifetime);
        }

        var current = existing[0];
        var existingEffectId = ReadExact(current["effectId"]);
        switch (policy)
        {
            case "stack":
                return ResolveStack(
                    input,
                    current,
                    existingEffectId,
                    maxStacks,
                    atMaximum,
                    newStacking,
                    newComponents,
                    newLifetime,
                    issues);
            case "refresh":
                return ResolveRefresh(
                    input,
                    current,
                    existingEffectId,
                    newStacking,
                    newComponents,
                    newLifetime,
                    issues);
            case "replace":
                return StackResult(
                    "replace",
                    true,
                    true,
                    existingEffectId,
                    null,
                    newStacking,
                    newComponents,
                    newLifetime);
            case "merge":
                return ResolveMerge(
                    current,
                    existingEffectId,
                    maxStacks,
                    atMaximum,
                    stacking,
                    newStacking,
                    newComponents,
                    newLifetime,
                    issues);
            default:
                return FailedStack(input, issues, newStacking, newComponents, newLifetime);
        }
    }

    internal static EffectLifetimeReductionResult AdvanceLifetime(
        EffectLifetimeReductionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Effect);
        ArgumentNullException.ThrowIfNull(input.Event);
        ArgumentNullException.ThrowIfNull(input.ProcessedEventRefs);

        var issues = new List<ValidationIssue>();
        ValidateEvent(input.Event.EventRef, input.ProcessedEventRefs, issues);
        if (input.Event.Turn <= 0)
        {
            Add(
                issues,
                "event.turn",
                "effect_lifecycle_event_invalid",
                "positive accepted turn",
                input.Event.Turn.ToString());
        }
        var effect = input.Effect.DeepClone().AsObject();
        if (effect["lifetime"] is not JsonObject lifetime ||
            !TryExact(lifetime["mode"], out var mode))
        {
            Add(
                issues,
                "effect.lifetime",
                "effect_lifecycle_mode_invalid",
                "one complete registered lifetime mode",
                Describe(effect["lifetime"]));
            return new EffectLifetimeReductionResult("invalid", null, issues);
        }
        if (mode is not ("turns" or "uses" or "until_time" or "scene" or
            "source_bound" or "condition_bound" or "permanent" or "manual"))
        {
            Add(
                issues,
                "effect.lifetime.mode",
                "effect_lifecycle_mode_invalid",
                "turns, uses, until_time, scene, source_bound, condition_bound, permanent, or manual",
                mode);
        }
        if (issues.Count > 0)
            return new EffectLifetimeReductionResult("invalid", null, issues);

        if (input.Event.TargetSatisfied == false)
            return LifetimeResult("expire", null);

        if (TryExact(input.Event.CurrentRealm, out var currentRealm) &&
            TryExact(effect["realm"], out var effectRealm) &&
            !string.Equals(currentRealm, effectRealm, StringComparison.Ordinal))
        {
            var realmLossPolicy = effect["removal"]?["onSourceLoss"]?.GetValue<string>();
            return realmLossPolicy switch
            {
                "expire" => LifetimeResult("expire", null),
                "suspend" => LifetimeResult("suspend", SetState(effect, "suspended")),
                _ => LifetimeFailure(
                    "effect.removal.onSourceLoss",
                    "effect_lifecycle_realm_transition_unauthorized",
                    "source-declared suspend or expire policy for realm exit",
                    realmLossPolicy ?? "missing")
            };
        }

        return mode switch
        {
            "turns" => AdvanceTurns(effect, lifetime, input.Event),
            "uses" => AdvanceUses(effect, lifetime, input.Event),
            "until_time" => AdvanceUntilTime(effect, lifetime, input.Event),
            "scene" => AdvanceScene(effect, lifetime, input.Event),
            "source_bound" => AdvanceBound(
                effect,
                input.Event.SourceSatisfied,
                ReadExact(lifetime["onSourceLoss"]),
                "source"),
            "condition_bound" => AdvanceBound(
                effect,
                input.Event.ConditionSatisfied,
                ReadExact(lifetime["onConditionLoss"]),
                "condition"),
            "permanent" => LifetimeResult("no_change", effect),
            "manual" => AdvanceManual(effect, lifetime),
            _ => LifetimeFailure(
                "effect.lifetime.mode",
                "effect_lifecycle_mode_invalid",
                "registered lifetime mode",
                mode)
        };
    }

    private static EffectStackApplicationResult ResolveStack(
        EffectStackApplicationInput input,
        JsonObject current,
        string? existingEffectId,
        int maxStacks,
        string atMaximum,
        JsonObject newStacking,
        JsonArray newComponents,
        JsonObject newLifetime,
        List<ValidationIssue> issues)
    {
        if (!TryPositiveInt(current["stacking"]?["currentStacks"], out var currentStacks))
        {
            Add(
                issues,
                "activeEffect.stacking.currentStacks",
                "effect_lifecycle_stack_state_invalid",
                "positive currentStacks",
                Describe(current["stacking"]?["currentStacks"]));
            return FailedStack(input, issues, newStacking, newComponents, newLifetime);
        }

        if (currentStacks < maxStacks)
        {
            current["stacking"]!["currentStacks"] = currentStacks + 1;
            return StackResult(
                "stack",
                false,
                false,
                existingEffectId,
                current,
                newStacking,
                newComponents,
                newLifetime);
        }

        if (string.Equals(atMaximum, "no_change", StringComparison.Ordinal))
        {
            return StackResult(
                "no_change",
                false,
                false,
                existingEffectId,
                current,
                newStacking,
                newComponents,
                newLifetime);
        }
        if (string.Equals(atMaximum, "refresh", StringComparison.Ordinal))
        {
            current["lifetime"] = newLifetime.DeepClone();
            return StackResult(
                "refresh",
                false,
                false,
                existingEffectId,
                current,
                newStacking,
                newComponents,
                newLifetime);
        }

        Add(
            issues,
            "source.stacking.atMaximum",
            "effect_lifecycle_component_response_deferred",
            "no_change or refresh until registered component-response scheduling is enabled",
            atMaximum);
        return FailedStack(input, issues, newStacking, newComponents, newLifetime);
    }

    private static EffectStackApplicationResult ResolveRefresh(
        EffectStackApplicationInput input,
        JsonObject current,
        string? existingEffectId,
        JsonObject newStacking,
        JsonArray newComponents,
        JsonObject newLifetime,
        List<ValidationIssue> issues)
    {
        var refreshMode = ReadExact(input.Definition["stacking"]?["refreshMode"]);
        if (string.Equals(refreshMode, "reset", StringComparison.Ordinal))
        {
            current["lifetime"] = newLifetime.DeepClone();
        }
        else if (string.Equals(refreshMode, "extend", StringComparison.Ordinal))
        {
            if (!TryExtendLifetime(
                    current["lifetime"] as JsonObject,
                    input.Definition["lifetime"] as JsonObject,
                    newLifetime,
                    out var extended,
                    issues))
            {
                return FailedStack(input, issues, newStacking, newComponents, newLifetime);
            }
            current["lifetime"] = extended;
        }
        else
        {
            Add(
                issues,
                "source.stacking.refreshMode",
                "effect_lifecycle_refresh_mode_invalid",
                "reset or extend",
                refreshMode ?? "missing");
            return FailedStack(input, issues, newStacking, newComponents, newLifetime);
        }

        return StackResult(
            "refresh",
            false,
            false,
            existingEffectId,
            current,
            newStacking,
            newComponents,
            newLifetime);
    }

    private static EffectStackApplicationResult ResolveMerge(
        JsonObject current,
        string? existingEffectId,
        int maxStacks,
        string atMaximum,
        JsonObject sourceStacking,
        JsonObject newStacking,
        JsonArray newComponents,
        JsonObject newLifetime,
        List<ValidationIssue> issues)
    {
        if (!TryPositiveInt(current["stacking"]?["currentStacks"], out var currentStacks))
        {
            Add(
                issues,
                "activeEffect.stacking.currentStacks",
                "effect_lifecycle_stack_state_invalid",
                "positive currentStacks",
                Describe(current["stacking"]?["currentStacks"]));
            return new EffectStackApplicationResult(
                "invalid", false, false, existingEffectId, null,
                newStacking, newComponents, newLifetime, issues);
        }
        if (currentStacks >= maxStacks)
        {
            if (string.Equals(atMaximum, "no_change", StringComparison.Ordinal))
            {
                return StackResult(
                    "no_change", false, false, existingEffectId, current,
                    newStacking, newComponents, newLifetime);
            }
            Add(
                issues,
                "source.stacking.atMaximum",
                "effect_lifecycle_merge_maximum_policy_unsupported",
                "no_change until bounded maximum merge responses are registered",
                atMaximum);
            return new EffectStackApplicationResult(
                "invalid", false, false, existingEffectId, null,
                newStacking, newComponents, newLifetime, issues);
        }

        var mergeRule = ReadExact(sourceStacking["mergeRule"]);
        if (mergeRule is not ("sum" or "minimum" or "maximum"))
        {
            Add(
                issues,
                "source.stacking.mergeRule",
                "effect_lifecycle_merge_rule_unsupported",
                "registered sum, minimum, or maximum reducer",
                mergeRule ?? "missing");
            return new EffectStackApplicationResult(
                "invalid", false, false, existingEffectId, null,
                newStacking, newComponents, newLifetime, issues);
        }
        if (current["components"] is not JsonArray existingComponents ||
            !TryMergeComponents(existingComponents, newComponents, mergeRule, issues, out var merged))
        {
            return new EffectStackApplicationResult(
                "invalid", false, false, existingEffectId, null,
                newStacking, newComponents, newLifetime, issues);
        }

        current["components"] = merged;
        current["stacking"]!["currentStacks"] = currentStacks + 1;
        return StackResult(
            "merge", false, false, existingEffectId, current,
            newStacking, newComponents, newLifetime);
    }

    private static bool TryMergeComponents(
        JsonArray existing,
        JsonArray incoming,
        string rule,
        List<ValidationIssue> issues,
        out JsonArray merged)
    {
        merged = existing.DeepClone().AsArray();
        if (existing.Count != incoming.Count)
        {
            Add(
                issues,
                "activeEffect.components",
                "effect_lifecycle_merge_component_mismatch",
                "identical component identity/profile set",
                $"{existing.Count}/{incoming.Count}");
            return false;
        }

        var incomingById = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var node in incoming)
        {
            if (node is not JsonObject component ||
                !TryExact(component["componentId"], out var id) ||
                !incomingById.TryAdd(id, component))
            {
                Add(
                    issues,
                    "source.components",
                    "effect_lifecycle_merge_component_mismatch",
                    "unique incoming component identities",
                    Describe(node));
                return false;
            }
        }

        for (var index = 0; index < merged.Count; index++)
        {
            if (merged[index] is not JsonObject current ||
                !TryExact(current["componentId"], out var id) ||
                !incomingById.TryGetValue(id, out var next) ||
                !JsonNode.DeepEquals(current["profile"], next["profile"]) ||
                !JsonNode.DeepEquals(current["priority"], next["priority"]) ||
                current["payload"] is not JsonObject currentPayload ||
                next["payload"] is not JsonObject nextPayload ||
                !TryMergePayload(currentPayload, nextPayload, rule, issues, out var payload))
            {
                Add(
                    issues,
                    $"activeEffect.components[{index}]",
                    "effect_lifecycle_merge_component_mismatch",
                    "identical component identity/profile/payload shape",
                    Describe(merged[index]));
                return false;
            }
            current["payload"] = payload;
        }
        return true;
    }

    private static bool TryMergePayload(
        JsonObject current,
        JsonObject incoming,
        string rule,
        List<ValidationIssue> issues,
        out JsonObject merged)
    {
        merged = current.DeepClone().AsObject();
        if (!current.Select(static pair => pair.Key).ToHashSet(StringComparer.Ordinal)
                .SetEquals(incoming.Select(static pair => pair.Key)))
        {
            return false;
        }
        foreach (var (key, currentNode) in current)
        {
            var incomingNode = incoming[key];
            if (TryFiniteDouble(currentNode, out var left) &&
                TryFiniteDouble(incomingNode, out var right))
            {
                var value = rule switch
                {
                    "sum" => left + right,
                    "minimum" => Math.Min(left, right),
                    "maximum" => Math.Max(left, right),
                    _ => double.NaN
                };
                if (!double.IsFinite(value))
                {
                    Add(
                        issues,
                        "activeEffect.components.payload." + key,
                        "effect_lifecycle_merge_overflow",
                        "finite deterministic merge result",
                        $"{left}/{right}");
                    return false;
                }
                merged[key] = value;
            }
            else if (!JsonNode.DeepEquals(currentNode, incomingNode))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryExtendLifetime(
        JsonObject? current,
        JsonObject? sourcePolicy,
        JsonObject initial,
        out JsonObject extended,
        List<ValidationIssue> issues)
    {
        extended = current?.DeepClone().AsObject() ?? new JsonObject();
        var mode = ReadExact(current?["mode"]);
        if (!string.Equals(mode, ReadExact(initial["mode"]), StringComparison.Ordinal))
        {
            Add(
                issues,
                "activeEffect.lifetime.mode",
                "effect_lifecycle_refresh_lifetime_mismatch",
                "same source-owned lifetime mode",
                mode ?? "missing");
            return false;
        }
        try
        {
            switch (mode)
            {
                case "turns" when TryPositiveInt(current!["remainingTurns"], out var turns) &&
                    TryPositiveInt(initial["remainingTurns"], out var initialTurns):
                    extended["remainingTurns"] = checked(turns + initialTurns);
                    return true;
                case "uses" when TryPositiveInt(current!["remainingUses"], out var uses) &&
                    TryPositiveInt(initial["remainingUses"], out var initialUses):
                    extended["remainingUses"] = checked(uses + initialUses);
                    return true;
                case "until_time" when TryNonNegativeLong(current!["deadline"], out var deadline) &&
                    TryPositiveInt(sourcePolicy?["duration"], out var duration):
                    extended["deadline"] = checked(deadline + duration);
                    return true;
            }
        }
        catch (OverflowException)
        {
            Add(
                issues,
                "activeEffect.lifetime",
                "effect_lifecycle_refresh_overflow",
                "bounded finite lifetime extension",
                Describe(current));
            return false;
        }

        Add(
            issues,
            "activeEffect.lifetime",
            "effect_lifecycle_refresh_mode_unsupported",
            "turns, uses, or until_time lifetime for extend refresh",
            mode ?? "missing");
        return false;
    }

    private static EffectLifetimeReductionResult AdvanceTurns(
        JsonObject effect,
        JsonObject lifetime,
        EffectLifecycleEvent lifecycleEvent)
    {
        if (!TryPositiveInt(lifetime["remainingTurns"], out var remaining))
        {
            return LifetimeFailure(
                "effect.lifetime.remainingTurns",
                "effect_lifecycle_state_invalid",
                "positive remainingTurns",
                Describe(lifetime["remainingTurns"]));
        }
        if (remaining == ForbiddenNumericSentinel)
        {
            return LifetimeFailure(
                "effect.lifetime.remainingTurns",
                "effect_lifecycle_numeric_sentinel_forbidden",
                "source-derived positive count rather than 999 sentinel",
                remaining.ToString());
        }
        if (!TryExact(lifetime["advancePhase"], out var phase))
        {
            return LifetimeFailure(
                "effect.lifetime.advancePhase",
                "effect_lifecycle_state_invalid",
                "exact registered advance phase",
                Describe(lifetime["advancePhase"]));
        }
        if (!string.Equals(phase, lifecycleEvent.Phase, StringComparison.Ordinal))
            return LifetimeResult("no_change", effect);
        if (remaining == 1)
            return LifetimeResult("expire", null);
        lifetime["remainingTurns"] = remaining - 1;
        return LifetimeResult("advance", effect);
    }

    private static EffectLifetimeReductionResult AdvanceUses(
        JsonObject effect,
        JsonObject lifetime,
        EffectLifecycleEvent lifecycleEvent)
    {
        if (!TryPositiveInt(lifetime["remainingUses"], out var remaining))
        {
            return LifetimeFailure(
                "effect.lifetime.remainingUses",
                "effect_lifecycle_state_invalid",
                "positive remainingUses",
                Describe(lifetime["remainingUses"]));
        }
        if (remaining == ForbiddenNumericSentinel)
        {
            return LifetimeFailure(
                "effect.lifetime.remainingUses",
                "effect_lifecycle_numeric_sentinel_forbidden",
                "source-derived positive count rather than 999 sentinel",
                remaining.ToString());
        }
        if (lifetime["consumingTriggerIds"] is not JsonArray triggerIds)
        {
            return LifetimeFailure(
                "effect.lifetime.consumingTriggerIds",
                "effect_lifecycle_state_invalid",
                "non-empty exact trigger identity array",
                Describe(lifetime["consumingTriggerIds"]));
        }
        if (lifecycleEvent.TriggerId is not { } exactTriggerId)
            return LifetimeResult("no_change", effect);
        var consumes = triggerIds
            .OfType<JsonValue>()
            .Any(node => node.TryGetValue<string>(out var id) &&
                string.Equals(id, exactTriggerId, StringComparison.Ordinal));
        if (!consumes)
            return LifetimeResult("no_change", effect);

        if (effect["triggers"] is not JsonArray triggers)
        {
            return LifetimeFailure(
                "effect.triggers",
                "effect_lifecycle_trigger_catalog_invalid",
                "canonical trigger array containing the exact consuming trigger",
                Describe(effect["triggers"]));
        }

        var matchingTriggers = triggers
            .OfType<JsonObject>()
            .Where(trigger => TryExact(trigger["triggerId"], out var id) &&
                string.Equals(id, exactTriggerId, StringComparison.Ordinal))
            .ToArray();
        if (matchingTriggers.Length != 1 ||
            !TryExact(matchingTriggers[0]["eventType"], out var eventType) ||
            matchingTriggers[0]["consumeUses"] is not JsonValue consumeUsesNode ||
            !consumeUsesNode.TryGetValue<bool>(out var consumeUses) ||
            !consumeUses)
        {
            return LifetimeFailure(
                "effect.triggers",
                "effect_lifecycle_consuming_trigger_invalid",
                "one exact consumeUses trigger matching the lifetime trigger identity",
                exactTriggerId);
        }
        if (!string.Equals(eventType, lifecycleEvent.Phase, StringComparison.Ordinal))
            return LifetimeResult("no_change", effect);

        if (remaining == 1)
            return LifetimeResult("expire", null);
        lifetime["remainingUses"] = remaining - 1;
        return LifetimeResult("advance", effect);
    }

    private static EffectLifetimeReductionResult AdvanceUntilTime(
        JsonObject effect,
        JsonObject lifetime,
        EffectLifecycleEvent lifecycleEvent)
    {
        if (!TryNonNegativeLong(lifetime["deadline"], out var deadline))
        {
            return LifetimeFailure(
                "effect.lifetime.deadline",
                "effect_lifecycle_state_invalid",
                "non-negative canonical world-time deadline",
                Describe(lifetime["deadline"]));
        }
        if (!lifecycleEvent.CurrentTime.HasValue)
            return LifetimeResult("no_change", effect);
        return lifecycleEvent.CurrentTime.Value >= deadline
            ? LifetimeResult("expire", null)
            : LifetimeResult("no_change", effect);
    }

    private static EffectLifetimeReductionResult AdvanceScene(
        JsonObject effect,
        JsonObject lifetime,
        EffectLifecycleEvent lifecycleEvent)
    {
        if (!TryExact(lifetime["sceneId"], out var sceneId) ||
            !TryExact(lifetime["onSceneExit"], out var policy) ||
            policy is not ("expire" or "suspend"))
        {
            return LifetimeFailure(
                "effect.lifetime",
                "effect_lifecycle_state_invalid",
                "exact sceneId and suspend/expire exit policy",
                lifetime.ToJsonString());
        }
        if (!lifecycleEvent.SceneClosed &&
            string.Equals(sceneId, lifecycleEvent.CurrentSceneId, StringComparison.Ordinal))
        {
            return IsSuspended(effect)
                ? LifetimeResult("resume", SetState(effect, "active"))
                : LifetimeResult("no_change", effect);
        }
        if (lifecycleEvent.CurrentSceneId == null && !lifecycleEvent.SceneClosed)
            return LifetimeResult("no_change", effect);
        return policy == "expire"
            ? LifetimeResult("expire", null)
            : LifetimeResult("suspend", SetState(effect, "suspended"));
    }

    private static EffectLifetimeReductionResult AdvanceBound(
        JsonObject effect,
        bool? satisfied,
        string? lossPolicy,
        string boundary)
    {
        if (!satisfied.HasValue)
            return LifetimeResult("no_change", effect);
        if (satisfied.Value)
        {
            return IsSuspended(effect)
                ? LifetimeResult("resume", SetState(effect, "active"))
                : LifetimeResult("no_change", effect);
        }
        return lossPolicy switch
        {
            "expire" => LifetimeResult("expire", null),
            "suspend" => LifetimeResult("suspend", SetState(effect, "suspended")),
            _ => LifetimeFailure(
                $"effect.lifetime.on{boundary}Loss",
                "effect_lifecycle_state_invalid",
                "source-declared suspend or expire loss policy",
                lossPolicy ?? "missing")
        };
    }

    private static EffectLifetimeReductionResult AdvanceManual(
        JsonObject effect,
        JsonObject lifetime)
    {
        if (lifetime["authorities"] is JsonArray { Count: > 0 })
            return LifetimeResult("no_change", effect);
        return LifetimeFailure(
            "effect.lifetime.authorities",
            "effect_lifecycle_state_invalid",
            "non-empty registered manual authority array",
            Describe(lifetime["authorities"]));
    }

    private static void ValidateExistingStack(
        JsonObject effect,
        JsonObject sourceStacking,
        string policy,
        string stackKey,
        int sourceMaximum,
        List<ValidationIssue> issues)
    {
        var existing = effect["stacking"] as JsonObject;
        var actualPolicy = ReadExact(existing?["policy"]);
        var actualKey = ReadExact(existing?["stackKey"]);
        var actualMaximum = TryPositiveInt(existing?["maxStacks"], out var maximum)
            ? maximum
            : 0;
        var expectedMaximum = policy == "independent" ? 1 : sourceMaximum;
        var replacesPriorPolicy = string.Equals(
            policy,
            "replace",
            StringComparison.Ordinal);
        var actualPolicyIsClosed = actualPolicy is
            "independent" or "stack" or "refresh" or "replace" or "merge";
        var policyMatches = replacesPriorPolicy
            ? actualPolicyIsClosed
            : string.Equals(actualPolicy, policy, StringComparison.Ordinal);
        var maximumMatches = replacesPriorPolicy
            ? actualMaximum > 0
            : actualMaximum == expectedMaximum;
        var reducerDetailsMatch = replacesPriorPolicy ||
            (JsonNode.DeepEquals(
                 existing?["refreshMode"],
                 sourceStacking["refreshMode"]) &&
             JsonNode.DeepEquals(
                 existing?["mergeRule"],
                 sourceStacking["mergeRule"]));
        if (!policyMatches ||
            !string.Equals(actualKey, stackKey, StringComparison.Ordinal) ||
            !maximumMatches ||
            !reducerDetailsMatch)
        {
            Add(
                issues,
                "activeEffect.stacking",
                "effect_lifecycle_stack_policy_conflict",
                $"source policy/key/maximum {policy}/{stackKey}/{expectedMaximum}",
                Describe(existing));
        }
    }

    private static void ValidateEvent(
        string eventRef,
        IReadOnlySet<string> processed,
        List<ValidationIssue> issues)
    {
        if (!TryExact(eventRef))
        {
            Add(
                issues,
                "eventRef",
                "effect_lifecycle_event_invalid",
                "exact accepted event reference",
                eventRef);
        }
        else if (processed.Contains(eventRef))
        {
            Add(
                issues,
                "eventRef",
                "effect_lifecycle_event_replay",
                "unprocessed accepted event reference",
                eventRef);
        }
    }

    private static EffectStackApplicationResult FailedStack(
        EffectStackApplicationInput input,
        List<ValidationIssue> issues,
        JsonObject? stacking = null,
        JsonArray? components = null,
        JsonObject? lifetime = null) =>
        new(
            "invalid",
            false,
            false,
            null,
            null,
            stacking ?? new JsonObject(),
            components ?? input.BoundComponents.DeepClone().AsArray(),
            lifetime ?? input.InitialLifetime.DeepClone().AsObject(),
            issues.ToArray());

    private static EffectStackApplicationResult StackResult(
        string outcome,
        bool createsNew,
        bool terminatesExisting,
        string? existingEffectId,
        JsonObject? updatedExisting,
        JsonObject newStacking,
        JsonArray newComponents,
        JsonObject newLifetime) =>
        new(
            outcome,
            createsNew,
            terminatesExisting,
            existingEffectId,
            updatedExisting?.DeepClone().AsObject(),
            newStacking.DeepClone().AsObject(),
            newComponents.DeepClone().AsArray(),
            newLifetime.DeepClone().AsObject(),
            Array.Empty<ValidationIssue>());

    private static EffectLifetimeReductionResult LifetimeResult(
        string outcome,
        JsonObject? effect) =>
        new(outcome, effect?.DeepClone().AsObject(), Array.Empty<ValidationIssue>());

    private static EffectLifetimeReductionResult LifetimeFailure(
        string path,
        string code,
        string expected,
        string actual)
    {
        var issues = new List<ValidationIssue>();
        Add(issues, path, code, expected, actual);
        return new EffectLifetimeReductionResult("invalid", null, issues);
    }

    private static JsonObject SetState(JsonObject effect, string state)
    {
        effect["state"] = state;
        return effect;
    }

    private static bool IsSuspended(JsonObject effect) =>
        string.Equals(ReadExact(effect["state"]), "suspended", StringComparison.Ordinal);

    private static bool TryFiniteDouble(JsonNode? node, out double value)
    {
        value = 0;
        return node is JsonValue &&
            double.TryParse(
                node.ToJsonString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value) &&
            double.IsFinite(value);
    }

    private static bool TryPositiveInt(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue json && json.TryGetValue<int>(out value) && value > 0;
    }

    private static bool TryNonNegativeLong(JsonNode? node, out long value)
    {
        value = -1;
        return node is JsonValue json && json.TryGetValue<long>(out value) && value >= 0;
    }

    private static bool TryExact(JsonNode? node, out string value)
    {
        value = node is JsonValue json && json.TryGetValue<string>(out var text)
            ? text
            : string.Empty;
        return TryExact(value);
    }

    private static string? ReadExact(JsonNode? node) =>
        TryExact(node, out var value) ? value : null;

    private static bool TryExact(string? value) =>
        value is { Length: > 0 } &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static string Describe(JsonNode? node) => node?.ToJsonString() ?? "missing";

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Accepted effect lifecycle cannot produce one deterministic after-image.",
            code: code,
            section: "effect_lifecycle",
            expected: expected,
            actual: actual,
            repairHint: "Restore the validated effect state and use one exact source-authorized lifecycle event; do not submit stack counts, remaining lifetime, or terminal identity."));
}
