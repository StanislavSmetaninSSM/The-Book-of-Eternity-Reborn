using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class AfterlifeControlStateRules
{
    internal static readonly HashSet<string> SourceOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "binding",
        "force_binding",
        "force_incarnation",
        "break_binding",
        "incarnation_resistance",
        "counter",
        "guard",
        "repair"
    };

    internal readonly record struct Snapshot(int Rank, string? ControllerSide);

    internal static bool ChangedSemantically(JsonNode? before, JsonNode? after)
    {
        if (IsNoActiveSnapshot(before) && IsNoActiveSnapshot(after))
            return false;

        return !JsonNode.DeepEquals(before, after);
    }

    internal static bool AuditSnapshotMatchesPrior(JsonNode? priorControlState, JsonNode? beforeControlState)
    {
        if (IsNoActiveSnapshot(priorControlState) && IsNoActiveSnapshot(beforeControlState))
            return true;

        if (priorControlState is not JsonObject priorControl ||
            beforeControlState is not JsonObject beforeControl)
        {
            return false;
        }

        return JsonNode.DeepEquals(
            NormalizeForComparison(priorControl),
            NormalizeForComparison(beforeControl));
    }

    internal static JsonNode? NormalizeForComparison(JsonNode? controlState)
    {
        return IsNoActiveSnapshot(controlState)
            ? null
            : controlState?.DeepClone();
    }

    internal static string DescribeNode(JsonNode? node)
    {
        if (IsNoActiveSnapshot(node))
            return "missing/none";

        if (node is not JsonObject control)
            return node?.GetType().Name ?? "missing";

        var side = AfterlifeSpiritualConflictState.GetNodeString(control["controllerSide"]);
        var level = AfterlifeSpiritualConflictState.GetNodeString(control["level"]);
        var controlId = AfterlifeSpiritualConflictState.GetNodeString(control["controlId"]);
        var sourceOperation = AfterlifeSpiritualConflictState.GetNodeString(control["sourceOperation"]);
        return $"{side}:{level}:{controlId}:{sourceOperation}";
    }

    internal static bool IsNoActiveSnapshot(JsonNode? node)
    {
        if (node == null)
            return true;

        if (node is not JsonObject control)
            return false;

        var level = AfterlifeSpiritualConflictState.GetNodeString(control["level"]);
        return string.Equals(level, "none", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryGetPlayerProgression(
        JsonObject before,
        JsonObject after,
        out int beforePlayerRank,
        out int afterPlayerRank)
    {
        beforePlayerRank = TryGetSnapshot(before, out var beforeControl) &&
                           string.Equals(beforeControl.ControllerSide, "player", StringComparison.OrdinalIgnoreCase)
            ? beforeControl.Rank
            : 0;

        if (TryGetSnapshot(after, out var afterControl) &&
            string.Equals(afterControl.ControllerSide, "player", StringComparison.OrdinalIgnoreCase))
        {
            afterPlayerRank = afterControl.Rank;
            return afterPlayerRank > beforePlayerRank;
        }

        afterPlayerRank = 0;
        return false;
    }

    internal static bool HasPlayerDelta(JsonObject before, JsonObject after)
    {
        var beforeHasPlayerControl = TryGetSnapshot(before, out var beforeControl) &&
                                     beforeControl.Rank > 0 &&
                                     string.Equals(beforeControl.ControllerSide, "player", StringComparison.OrdinalIgnoreCase);
        var afterHasPlayerControl = TryGetSnapshot(after, out var afterControl) &&
                                    afterControl.Rank > 0 &&
                                    string.Equals(afterControl.ControllerSide, "player", StringComparison.OrdinalIgnoreCase);
        return (beforeHasPlayerControl || afterHasPlayerControl) &&
               ChangedSemantically(before["controlState"], after["controlState"]);
    }

    internal static bool HasControlCounterPayoff(JsonObject before, JsonObject after) =>
        HasAntiControlDelta(before, after);

    internal static bool CounterAdvancesPlayerControl(JsonObject before, JsonObject after)
    {
        if (!TryGetSnapshot(after, out var afterControl) ||
            afterControl.Rank <= 0 ||
            !string.Equals(afterControl.ControllerSide, "player", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryGetSnapshot(before, out var beforeControl) ||
            beforeControl.Rank <= 0)
        {
            return true;
        }

        if (string.Equals(beforeControl.ControllerSide, "opposition", StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(beforeControl.ControllerSide, "player", StringComparison.OrdinalIgnoreCase) &&
               afterControl.Rank > beforeControl.Rank;
    }

    internal static bool HasAntiControlDelta(JsonObject before, JsonObject after)
    {
        if (TryGetSnapshot(before, out var beforeControl) &&
            beforeControl.Rank > 0 &&
            string.Equals(beforeControl.ControllerSide, "opposition", StringComparison.OrdinalIgnoreCase))
        {
            var beforeControlNode = (JsonObject?)before["controlState"];
            if (!TryGetSnapshot(after, out var afterControl) || afterControl.Rank == 0)
                return true;

            if (string.Equals(afterControl.ControllerSide, "opposition", StringComparison.OrdinalIgnoreCase) &&
                afterControl.Rank < beforeControl.Rank)
            {
                return true;
            }

            if (string.Equals(afterControl.ControllerSide, "opposition", StringComparison.OrdinalIgnoreCase) &&
                afterControl.Rank == beforeControl.Rank &&
                beforeControlNode is not null &&
                after["controlState"] is JsonObject afterControlNode &&
                RestrictionsStrictlyReduced(beforeControlNode, afterControlNode))
            {
                return true;
            }

            if (string.Equals(afterControl.ControllerSide, "player", StringComparison.OrdinalIgnoreCase) &&
                afterControl.Rank > 0)
            {
                return true;
            }

            return false;
        }

        foreach (var field in new[] { "bindingState", "bindingId", "activeBinding", "forcedHandoff", "forceIncarnation", "forcedIncarnation" })
        {
            if (before.ContainsKey(field) && !JsonNode.DeepEquals(before[field], after[field]))
                return true;
        }

        return false;
    }

    internal static bool RestrictionsStrictlyReduced(JsonObject beforeControl, JsonObject afterControl)
    {
        var beforeRestrictions = GetRestrictionSet(beforeControl);
        var afterRestrictions = GetRestrictionSet(afterControl);
        if (beforeRestrictions.Count == 0 ||
            afterRestrictions.Count == 0 ||
            afterRestrictions.Count >= beforeRestrictions.Count)
        {
            return false;
        }

        return afterRestrictions.All(beforeRestrictions.Contains);
    }

    internal static HashSet<string> GetRestrictionSet(JsonObject control)
    {
        var restrictions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (control["restrictedOperations"] is not JsonArray restrictedOperations)
            return restrictions;

        foreach (var item in restrictedOperations)
        {
            var operation = AfterlifeSpiritualConflictState.GetNodeString(item);
            if (!string.IsNullOrWhiteSpace(operation))
                restrictions.Add(operation.Trim());
        }

        return restrictions;
    }

    internal static bool HasActiveOpposition(JsonObject root) =>
        TryGetSnapshot(root, out var control) &&
        control.Rank > 0 &&
        string.Equals(control.ControllerSide, "opposition", StringComparison.OrdinalIgnoreCase);

    internal static bool HasActive(JsonObject root) =>
        TryGetSnapshot(root, out var control) && control.Rank > 0;

    internal static bool HasActive(JsonNode? node)
    {
        if (node is not JsonObject control)
            return false;

        var level = AfterlifeSpiritualConflictState.GetNodeString(control["level"]);
        return TryGetLevelRank(level, out var rank) && rank > 0;
    }

    internal static bool HasForcedIncarnationControl(JsonObject root)
    {
        if (!TryGetSnapshot(root, out var control) ||
            control.Rank <= 0 ||
            root["controlState"] is not JsonObject controlState)
        {
            return false;
        }

        return NodeStringEquals(controlState, "force_incarnation", "sourceOperation", "operationType", "finalOperationType");
    }

    internal static bool TryGetSnapshot(JsonObject root, out Snapshot snapshot)
    {
        snapshot = default;
        if (root["controlState"] is not JsonObject control)
            return false;

        var level = AfterlifeSpiritualConflictState.GetNodeString(control["level"]);
        if (!TryGetLevelRank(level, out var rank))
            return false;

        var side = AfterlifeSpiritualConflictState.GetNodeString(control["controllerSide"]);
        snapshot = new Snapshot(rank, side);
        return true;
    }

    internal static bool TryGetLevelRank(string? level, out int rank)
    {
        rank = 0;
        if (string.IsNullOrWhiteSpace(level))
            return false;

        rank = level.Trim().ToLowerInvariant() switch
        {
            "none" => 0,
            "hindered" => 1,
            "bound" => 2,
            "locked" => 3,
            _ => 0
        };
        return AfterlifeSpiritualConflictState.ControlLevels.Contains(level);
    }

    internal static string DescribeTransition(JsonObject before, JsonObject after) =>
        $"{DescribeState(before)} -> {DescribeState(after)}";

    internal static string DescribeState(JsonObject root)
    {
        if (!TryGetSnapshot(root, out var control))
            return "missing/none";

        var side = string.IsNullOrWhiteSpace(control.ControllerSide) ? "none" : control.ControllerSide;
        return $"{side}:{control.Rank}";
    }

    private static bool NodeStringEquals(JsonObject root, string expected, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = AfterlifeSpiritualConflictState.GetNodeString(root[propertyName]);
            if (string.Equals(value, expected, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
