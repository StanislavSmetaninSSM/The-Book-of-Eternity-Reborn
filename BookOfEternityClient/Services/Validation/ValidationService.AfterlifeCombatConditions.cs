using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private static readonly HashSet<string> AfterlifeCombatConditionKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "mark",
        "ward",
        "burden",
        "opening",
        "vow"
    };

    private static readonly HashSet<string> AfterlifeCombatConditionMechanicalAxes = new(StringComparer.OrdinalIgnoreCase)
    {
        "rollMode",
        "conflictPosition",
        "controlState",
        "playerSideStrain",
        "oppositionSideStrain",
        "tempoAdvantage",
        "counterPayoff",
        "actionCostAudit",
        "actionCostAudit.player",
        "actionCostAudit.opposition",
        "specialArtAudit.effectNote",
        "specialArtAudits.effectNote"
    };

    private static readonly HashSet<string> AfterlifeCombatConditionStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "active",
        "consumed",
        "expired",
        "cleared",
        "blocked"
    };

    private static readonly HashSet<string> AfterlifeCombatConditionAntiControlOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "break_binding",
        "incarnation_resistance",
        "counter",
        "guard"
    };

    private static readonly HashSet<string> AfterlifeCombatConditionControlPayoffs = new(StringComparer.OrdinalIgnoreCase)
    {
        "soften_control",
        "narrow_restrictions",
        "clear_control",
        "reverse_control"
    };

    private static readonly string[] AfterlifeCombatConditionFiniteDurationFields =
    [
        "remainingUses",
        "expiresAtTurn",
        "expiresAtExchangeId",
        "expiresAfterExchangeId",
        "expiresAfterTurns",
        "expiresAtScene",
        "sceneId"
    ];

    private static readonly string[] AfterlifeCombatConditionSourceIdentityFields =
    [
        "sourceType",
        "type",
        "sourceId",
        "id",
        "actorType",
        "actorId",
        "actorRef",
        "artId",
        "sourceOperation",
        "sourceExchangeId"
    ];

    private static HashSet<string> ValidateCombatConditions(
        JsonNode? node,
        string context,
        List<ValidationIssue> issues)
    {
        var conditionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (node == null)
            return conditionIds;

        if (node is not JsonArray conditions)
        {
            AddCombatConditionIssue(
                issues,
                context,
                "combatConditions должен быть array.",
                "afterlife_combat_condition_invalid_collection",
                "array",
                node.ToJsonString());
            return conditionIds;
        }

        for (var index = 0; index < conditions.Count; index++)
        {
            if (conditions[index] is not JsonObject condition)
            {
                AddCombatConditionIssue(
                    issues,
                    $"{context}[{index}]",
                    "combatConditions[] item должен быть object.",
                    "afterlife_combat_condition_invalid_item",
                    "condition object",
                    conditions[index]?.ToJsonString() ?? "null");
                continue;
            }

            ValidateCombatCondition(condition, $"{context}[{index}]", issues, conditionIds);
        }

        return conditionIds;
    }

    private static void ValidateCombatCondition(
        JsonObject condition,
        string context,
        List<ValidationIssue> issues,
        HashSet<string> conditionIds)
    {
        var conditionId = RequireCombatConditionString(condition, context, issues, "conditionId");
        RequireCombatConditionString(condition, context, issues, "displayName", "name");
        var kind = RequireCombatConditionString(condition, context, issues, "kind");
        if (!string.IsNullOrWhiteSpace(kind) && !AfterlifeCombatConditionKinds.Contains(kind))
        {
            AddCombatConditionIssue(
                issues,
                $"{context}.kind",
                "combatConditions.kind должен быть mark, ward, burden, opening или vow.",
                "afterlife_combat_condition_invalid_kind",
                "mark/ward/burden/opening/vow",
                kind);
        }

        var status = AfterlifeSpiritualConflictState.GetNodeString(condition["status"]) ?? "active";
        if (!AfterlifeCombatConditionStatuses.Contains(status))
        {
            AddCombatConditionIssue(
                issues,
                $"{context}.status",
                "combatConditions.status должен быть active, consumed, expired, cleared или blocked.",
                "afterlife_combat_condition_invalid_status",
                "active/consumed/expired/cleared/blocked",
                status);
        }

        if (!string.IsNullOrWhiteSpace(conditionId) && ConflictTokenEquals(status, "active"))
            conditionIds.Add(conditionId);

        var source = RequireCombatConditionObject(condition, context, issues, "source");
        ValidateCombatConditionSource(source, $"{context}.source", issues);
        ValidateCombatConditionTarget(condition, context, issues);
        var mechanicalAxes = RequireCombatConditionMechanicalAxes(condition, context, issues);
        foreach (var mechanicalAxis in mechanicalAxes)
        {
            if (!AfterlifeCombatConditionMechanicalAxes.Contains(mechanicalAxis))
            {
                AddCombatConditionIssue(
                    issues,
                    $"{context}.mechanicalAxis",
                    "combatConditions.mechanicalAxis должен ссылаться только на существующие legal afterlife combat axes.",
                    "afterlife_combat_condition_invalid_mechanical_axis",
                    string.Join("/", AfterlifeCombatConditionMechanicalAxes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                    mechanicalAxis);
            }
        }

        var affectedOperations = RequireCombatConditionStringArray(condition, context, issues, "affectedOperations");
        var payoff = RequireCombatConditionObject(condition, context, issues, "payoff");
        var duration = RequireCombatConditionObject(condition, context, issues, "duration");
        RequireCombatConditionString(condition, context, issues, "summary");
        ValidateCombatConditionCounterplay(condition["counterplay"], $"{context}.counterplay", issues);
        ValidateCombatConditionDuration(duration, status, $"{context}.duration", issues);
        ValidateCombatConditionAffectedOperations(affectedOperations, $"{context}.affectedOperations", issues);
        ValidateCombatConditionPayoff(mechanicalAxes, affectedOperations, payoff, context, issues);
    }

    private static void ValidateCombatConditionSource(
        JsonObject? source,
        string context,
        List<ValidationIssue> issues)
    {
        if (source == null)
            return;

        if (AfterlifeCombatConditionSourceIdentityFields.Any(field =>
                !string.IsNullOrWhiteSpace(AfterlifeSpiritualConflictState.GetNodeString(source[field]))))
        {
            return;
        }

        AddCombatConditionIssue(
            issues,
            context,
            "combatConditions.source должен содержать источник или стабильную identity.",
            "afterlife_combat_condition_missing_source_identity",
            string.Join("/", AfterlifeCombatConditionSourceIdentityFields),
            source.ToJsonString());
    }

    private static void ValidateCombatConditionCounterplay(
        JsonNode? node,
        string context,
        List<ValidationIssue> issues)
    {
        if (node == null)
        {
            AddCombatConditionIssue(
                issues,
                context,
                "combatConditions active entry должен иметь counterplay.",
                "afterlife_combat_condition_missing_required_field",
                "non-empty counterplay array",
                "missing");
            return;
        }

        if (node is not JsonArray counterplay ||
            !counterplay.Any(item => !string.IsNullOrWhiteSpace(AfterlifeSpiritualConflictState.GetNodeString(item))))
        {
            AddCombatConditionIssue(
                issues,
                context,
                "combatConditions.counterplay должен содержать хотя бы один player/GM-readable способ ответа.",
                "afterlife_combat_condition_missing_counterplay",
                "non-empty counterplay array",
                node.ToJsonString());
        }
    }

    private static void ValidateCombatConditionDuration(
        JsonObject? duration,
        string status,
        string context,
        List<ValidationIssue> issues)
    {
        if (duration == null)
            return;

        var type = AfterlifeSpiritualConflictState.GetNodeString(duration["type"]);
        if (ConflictTokenEquals(type, "indefinite", "permanent", "passive"))
        {
            AddCombatConditionIssue(
                issues,
                $"{context}.type",
                "combatConditions.duration не может быть indefinite/passive/permanent.",
                "afterlife_combat_condition_indefinite_duration",
                "finite duration, remainingUses, expiresAtTurn, or scene-bound duration",
                type ?? "missing");
        }

        var hasRemainingUses = duration.ContainsKey("remainingUses");
        var remainingUses = hasRemainingUses
            ? AfterlifeSpiritualConflictState.GetNodeInt(duration["remainingUses"])
            : 1;
        if (ConflictTokenEquals(status, "active") &&
            !AfterlifeCombatConditionFiniteDurationFields.Any(duration.ContainsKey))
        {
            AddCombatConditionIssue(
                issues,
                context,
                "active combatCondition должен иметь finite duration/uses.",
                "afterlife_combat_condition_missing_finite_duration",
                string.Join("/", AfterlifeCombatConditionFiniteDurationFields),
                duration.ToJsonString());
        }

        if (ConflictTokenEquals(status, "active") && hasRemainingUses && remainingUses <= 0)
        {
            AddCombatConditionIssue(
                issues,
                $"{context}.remainingUses",
                "active combatCondition не может иметь spent remainingUses.",
                "afterlife_combat_condition_active_duration_spent",
                "remainingUses > 0 or status consumed/expired/cleared",
                remainingUses.ToString());
        }
    }

    private static void ValidateCombatConditionAffectedOperations(
        IReadOnlyCollection<string> affectedOperations,
        string context,
        List<ValidationIssue> issues)
    {
        var index = 0;
        foreach (var operation in affectedOperations)
        {
            if (AfterlifeSpiritualConflictState.OperationTypes.Contains(operation))
            {
                index++;
                continue;
            }

            AddCombatConditionIssue(
                issues,
                $"{context}[{index}]",
                "combatConditions.affectedOperations должен содержать только legal afterlife operations.",
                "afterlife_combat_condition_invalid_affected_operation",
                string.Join("/", AfterlifeSpiritualConflictState.OperationTypes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                operation);
            index++;
        }
    }

    private static void ValidateCombatConditionPayoff(
        IReadOnlyCollection<string> mechanicalAxes,
        IReadOnlyCollection<string> affectedOperations,
        JsonObject? payoff,
        string context,
        List<ValidationIssue> issues)
    {
        if (payoff == null || mechanicalAxes.Count == 0)
            return;

        if (!string.Equals(
                AfterlifeSpiritualConflictState.GetNodeString(payoff["sourceType"]),
                "combat_condition",
                StringComparison.OrdinalIgnoreCase))
        {
            AddCombatConditionIssue(
                issues,
                $"{context}.payoff.sourceType",
                "combatConditions.payoff.sourceType должен быть combat_condition.",
                "afterlife_combat_condition_invalid_payoff_source",
                "combat_condition",
                AfterlifeSpiritualConflictState.GetNodeString(payoff["sourceType"]) ?? "missing");
        }

        var effect = AfterlifeSpiritualConflictState.GetNodeString(payoff["effect"]);
        if (string.IsNullOrWhiteSpace(effect))
        {
            AddCombatConditionIssue(
                issues,
                $"{context}.payoff",
                "combatConditions.payoff должен иметь meaningful effect.",
                "afterlife_combat_condition_missing_payoff_effect",
                "non-empty effect",
                payoff.ToJsonString());
        }

        if (!mechanicalAxes.Any(axis => ConflictTokenEquals(axis, "controlState")))
            return;

        var effectIsLegal = !string.IsNullOrWhiteSpace(effect) &&
                            AfterlifeCombatConditionControlPayoffs.Contains(effect);
        var operationsAreLegal = affectedOperations.Count > 0 &&
                                 affectedOperations.All(AfterlifeCombatConditionAntiControlOperations.Contains);
        if (!effectIsLegal || !operationsAreLegal)
        {
            AddCombatConditionIssue(
                issues,
                $"{context}.payoff",
                "combatConditions не может создавать или дублировать controlState; controlState axis allowed only for legal anti-control softening/narrowing/clearing.",
                "afterlife_combat_condition_illegal_control_payoff",
                "effect=soften_control|narrow_restrictions|clear_control|reverse_control with break_binding/incarnation_resistance/counter/guard operations",
                payoff.ToJsonString());
        }
    }

    private static string? RequireCombatConditionString(
        JsonObject condition,
        string context,
        List<ValidationIssue> issues,
        string propertyName,
        params string[] aliases)
    {
        var propertyNames = new[] { propertyName }.Concat(aliases).ToArray();
        var value = propertyNames
            .Select(name => AfterlifeSpiritualConflictState.GetNodeString(condition[name]))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (string.IsNullOrWhiteSpace(value))
        {
            AddCombatConditionIssue(
                issues,
                $"{context}.{propertyName}",
                $"combatConditions entry должен иметь {propertyName}.",
                "afterlife_combat_condition_missing_required_field",
                aliases.Length == 0 ? "non-empty string" : $"non-empty string ({string.Join(" or ", propertyNames)})",
                propertyNames
                    .Select(name => condition[name]?.ToJsonString())
                    .FirstOrDefault(actual => !string.IsNullOrWhiteSpace(actual)) ?? "missing");
        }

        return value;
    }

    private static void ValidateCombatConditionTarget(
        JsonObject condition,
        string context,
        List<ValidationIssue> issues)
    {
        if (condition["target"] is JsonObject target)
        {
            var side = AfterlifeSpiritualConflictState.GetNodeString(target["side"]) ??
                       AfterlifeSpiritualConflictState.GetNodeString(target["targetSide"]);
            if (!string.IsNullOrWhiteSpace(side))
                return;

            AddCombatConditionIssue(
                issues,
                $"{context}.target.side",
                "combatConditions.target должен иметь side.",
                "afterlife_combat_condition_missing_required_field",
                "non-empty target.side or targetSide",
                target.ToJsonString());
            return;
        }

        RequireCombatConditionString(condition, context, issues, "targetSide");
    }

    private static IReadOnlyCollection<string> RequireCombatConditionMechanicalAxes(
        JsonObject condition,
        string context,
        List<ValidationIssue> issues)
    {
        if (condition["mechanicalAxes"] is JsonArray axisArray)
            return ParseCombatConditionStringArray(axisArray, $"{context}.mechanicalAxes", "mechanicalAxes", issues);

        var mechanicalAxis = AfterlifeSpiritualConflictState.GetNodeString(condition["mechanicalAxis"]);
        if (!string.IsNullOrWhiteSpace(mechanicalAxis))
            return new[] { mechanicalAxis };

        AddCombatConditionIssue(
            issues,
            $"{context}.mechanicalAxis",
            "combatConditions entry должен иметь mechanicalAxis.",
            "afterlife_combat_condition_missing_required_field",
            "non-empty string or mechanicalAxes array",
            condition["mechanicalAxis"]?.ToJsonString() ??
            condition["mechanicalAxes"]?.ToJsonString() ??
            "missing");
        return Array.Empty<string>();
    }

    private static JsonObject? RequireCombatConditionObject(
        JsonObject condition,
        string context,
        List<ValidationIssue> issues,
        string propertyName)
    {
        if (condition[propertyName] is JsonObject obj)
            return obj;

        AddCombatConditionIssue(
            issues,
            $"{context}.{propertyName}",
            $"combatConditions entry должен иметь object {propertyName}.",
            "afterlife_combat_condition_missing_required_field",
            "object",
            condition[propertyName]?.ToJsonString() ?? "missing");
        return null;
    }

    private static IReadOnlyCollection<string> RequireCombatConditionStringArray(
        JsonObject condition,
        string context,
        List<ValidationIssue> issues,
        string propertyName)
    {
        if (condition[propertyName] is not JsonArray array)
        {
            AddCombatConditionIssue(
                issues,
                $"{context}.{propertyName}",
                $"combatConditions entry должен иметь array {propertyName}.",
                "afterlife_combat_condition_missing_required_field",
                "non-empty string array",
                condition[propertyName]?.ToJsonString() ?? "missing");
            return Array.Empty<string>();
        }

        return ParseCombatConditionStringArray(array, $"{context}.{propertyName}", propertyName, issues);
    }

    private static IReadOnlyCollection<string> ParseCombatConditionStringArray(
        JsonArray array,
        string path,
        string propertyName,
        List<ValidationIssue> issues)
    {
        var values = array
            .Select(AfterlifeSpiritualConflictState.GetNodeString)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
        if (values.Length == 0)
        {
            AddCombatConditionIssue(
                issues,
                path,
                $"combatConditions.{propertyName} должен содержать хотя бы одно значение.",
                "afterlife_combat_condition_missing_required_field",
                "non-empty string array",
                array.ToJsonString());
        }

        return values;
    }

    private static void ValidateCombatConditionRollModeSources(
        JsonObject audit,
        string context,
        List<ValidationIssue>? issues,
        IReadOnlySet<string>? conditionIds)
    {
        if (issues == null ||
            audit["rollMode"] is not JsonObject rollMode)
            return;

        foreach (var sideProperty in rollMode)
        {
            if (sideProperty.Value is not JsonObject sideMode)
                continue;

            ValidateCombatConditionRollModeSourceArray(
                sideMode["advantageSources"],
                $"{context}.rollMode.{sideProperty.Key}.advantageSources",
                issues,
                conditionIds);
            ValidateCombatConditionRollModeSourceArray(
                sideMode["disadvantageSources"],
                $"{context}.rollMode.{sideProperty.Key}.disadvantageSources",
                issues,
                conditionIds);
        }
    }

    private static IReadOnlySet<string>? MergeCombatConditionIds(
        IReadOnlySet<string>? first,
        IReadOnlySet<string>? second)
    {
        if (first is not { Count: > 0 })
            return second;
        if (second is not { Count: > 0 })
            return first;

        var merged = new HashSet<string>(first, StringComparer.OrdinalIgnoreCase);
        merged.UnionWith(second);
        return merged;
    }

    private static void ValidateCombatConditionRollModeSourceArray(
        JsonNode? node,
        string context,
        List<ValidationIssue> issues,
        IReadOnlySet<string>? conditionIds)
    {
        if (node is not JsonArray sources)
            return;

        for (var index = 0; index < sources.Count; index++)
        {
            if (sources[index] is not JsonObject source)
                continue;

            var sourceType = AfterlifeSpiritualConflictState.GetNodeString(source["sourceType"]) ??
                             AfterlifeSpiritualConflictState.GetNodeString(source["type"]);
            var conditionId = AfterlifeSpiritualConflictState.GetNodeString(source["conditionId"]);
            var isConditionBackedSource =
                ConflictTokenEquals(sourceType, "combat_condition") ||
                !string.IsNullOrWhiteSpace(conditionId);
            if (!isConditionBackedSource)
            {
                continue;
            }

            conditionId ??= AfterlifeSpiritualConflictState.GetNodeString(source["sourceId"]) ??
                            AfterlifeSpiritualConflictState.GetNodeString(source["id"]);
            if (string.IsNullOrWhiteSpace(conditionId) ||
                conditionIds == null ||
                !conditionIds.Contains(conditionId))
            {
                AddCombatConditionIssue(
                    issues,
                    $"{context}[{index}]",
                    "condition-backed rollMode source должен ссылаться на existing combatConditions.conditionId.",
                    "afterlife_combat_condition_roll_source_missing_active_condition",
                    "conditionId/sourceId/id matching combatConditions[].conditionId",
                    source.ToJsonString());
            }
        }
    }

    private static void AddCombatConditionIssue(
        List<ValidationIssue> issues,
        string path,
        string message,
        string code,
        string expected,
        string actual)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: "AfterlifeSpiritualConflict",
            expected: expected,
            actual: actual));
    }
}
