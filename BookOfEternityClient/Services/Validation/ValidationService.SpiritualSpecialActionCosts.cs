using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Resolves the effective operation used for ordinary and conditional wound costs.
    /// </summary>
    /// <param name="exchange">
    /// Exchange retaining the original operation and opposition final-operation precedence.
    /// </param>
    /// <param name="side">
    /// Exact player or opposition side.
    /// </param>
    /// <returns>
    /// The declared effective operation, or <see langword="null"/> when no supported opposition operation resolves.
    /// </returns>
    internal static string? ResolveSpiritualCostOperation(JsonObject exchange, string side) =>
        side == "player" ? AfterlifeSpiritualConflictState.GetNodeString(exchange["operationType"]) :
            ResolveOppositionOperationForActionCost(exchange);

    /// <summary>
    /// Proves that a force action has no wound payment in an authenticated live frontier.
    /// </summary>
    /// <param name="mechanics">
    /// Actual resource-owned frontier; <see langword="null"/> does not prove a free live action.
    /// </param>
    /// <param name="conflict">
    /// Conflict with validated actor membership.
    /// </param>
    /// <param name="exchange">
    /// Unchanged action evidence for this frontier.
    /// </param>
    /// <param name="side">
    /// Exact player or opposition side.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only for a force action with zero applicable burden in the current owned context;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool IsOwnedUnburdenedForceAction(
        AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext? mechanics,
        JsonObject conflict, JsonObject exchange, string side) =>
        mechanics is { IsCurrent: true } &&
        ConflictTokenEquals(ResolveSpiritualCostOperation(exchange, side), "force_incarnation") &&
        SpiritualWoundSourceSession.ReadCurrentActionCostBurden(mechanics, conflict, exchange, side) == 0;

    /// <summary>
    /// Checks the closed force-payment evidence shape without treating it as proof of a wound burden.
    /// </summary>
    /// <param name="node">
    /// Proposed audit; <see langword="null"/>, extra fields and noninteger arithmetic are invalid.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for the prescribed seven fields and zero base, minimum and art tier;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool IsPrescribedForceCostAudit(JsonNode? node)
    {
        if (node is not JsonObject audit || audit.Count != 7 ||
            !ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(audit["operationType"]), "force_incarnation"))
            return false;
        foreach (var field in new[] { "baseCost", "minCost", "artTier", "effectiveCost", "before", "after" })
            if (!TryGetJsonNodeInt(audit[field], out var value) ||
                (field is "baseCost" or "minCost" or "artTier") && value != 0)
                return false;
        return true;
    }

    /// <summary>
    /// Identifies a supplied conditional force audit for ordinary chronological balance validation.
    /// </summary>
    /// <param name="exchange">
    /// Exchange whose audit has separately undergone owned cost validation.
    /// </param>
    /// <param name="side">
    /// Exact player or opposition side.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when this force action supplies a side audit; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool HasConditionalForceCostAudit(JsonObject exchange, string side) =>
        ConflictTokenEquals(ResolveSpiritualCostOperation(exchange, side), "force_incarnation") &&
        HasActionCostAuditSide(exchange, side);

    /// <summary>
    /// Validates only the applicable wound payment for a force action, preserving its free unburdened path.
    /// </summary>
    /// <param name="exchange">
    /// Current exchange containing optional conditional payment evidence.
    /// </param>
    /// <param name="conflict">
    /// Validated conflict identifying the acting owner.
    /// </param>
    /// <param name="side">
    /// Exact player or opposition side.
    /// </param>
    /// <param name="context">
    /// Exchange diagnostic path.
    /// </param>
    /// <param name="authority">
    /// Original authority with the actual current wound mechanics when available.
    /// </param>
    /// <param name="issues">
    /// Destination for malformed, missing, incorrect or unaffordable payment diagnostics.
    /// </param>
    private static void ValidateForceWoundCostAudit(JsonObject exchange, JsonObject conflict,
        string side, string context, AfterlifeActionCostAuthorityContext authority, List<ValidationIssue> issues)
    {
        var burden = ReadSpiritualValidationCostBurden(
            authority, conflict, exchange, side);
        var path = context + ".actionCostAudit." + side;
        var prefix = side == "player" ? "afterlife_conflict_" : "afterlife_conflict_opposition_";
        if (burden == 0)
        {
            if (HasActionCostAuditSide(exchange, side))
            {
                var obsolete = authority.WoundMechanics is { IsCurrent: true } &&
                    IsPrescribedForceCostAudit(exchange["actionCostAudit"]![side]);
                AddActionCostIssue(issues, path, "Без применимого штрафа принудительное воплощение не имеет платы.",
                    obsolete ? "afterlife_conflict_wound_force_cost_audit_obsolete" : prefix + "action_cost_audit_unexpected",
                    "no audit for an unburdened force action",
                    exchange["actionCostAudit"]!.ToJsonString());
            }
            return;
        }
        var root = exchange["actionCostAudit"] as JsonObject;
        if (!exchange.ContainsKey("actionCostAudit") || root != null && !root.ContainsKey(side))
        {
            AddActionCostIssue(issues, path, "Для штрафа раны при принудительном воплощении нужен расчёт оплаты.",
                "afterlife_conflict_wound_force_cost_audit_missing", "prescribed force audit for the owned positive wound burden", "missing");
            return;
        }
        if (!IsPrescribedForceCostAudit(root?[side]))
        {
            AddActionCostIssue(issues, path, "Расчёт принудительного воплощения содержит только семь предписанных полей с нулевой базовой платой.",
                prefix + "action_cost_audit_invalid", "seven-field force audit with baseCost=minCost=artTier=0",
                root?[side]?.ToJsonString() ?? "malformed or null audit");
            return;
        }
        var audit = root![side]!.AsObject();
        TryGetJsonNodeInt(audit["effectiveCost"], out var cost);
        TryGetJsonNodeInt(audit["before"], out var before);
        TryGetJsonNodeInt(audit["after"], out var after);
        if (cost != burden)
            AddActionCostIssue(issues, path + ".effectiveCost", "Оплата должна равняться сумме применимых штрафов раны.",
                prefix + "action_cost_mismatch", $"baseCost=0, minCost=0, effectiveCost={burden}",
                $"baseCost=0, minCost=0, effectiveCost={cost}");
        if (before < burden)
            AddActionCostIssue(issues, path + ".before", "Для оплаты штрафа раны недостаточно духовных ОД.",
                prefix + "action_points_insufficient", $"before >= effectiveCost ({burden})", before.ToString());
        if (after < 0 || after != (long)before - cost)
            AddActionCostIssue(issues, path + ".after", "После действия остаётся ресурс до действия за вычетом штрафа.",
                prefix + "action_cost_delta_mismatch", ((long)before - cost).ToString(), after.ToString());
    }
}
