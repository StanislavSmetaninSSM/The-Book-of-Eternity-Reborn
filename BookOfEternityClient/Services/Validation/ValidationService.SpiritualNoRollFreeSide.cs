using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>Classifies declared free-side DATA without providing original or resource authority.</summary>
    /// <param name="exchange">Detached exchange still subject to all normal source and cost validation.</param>
    /// <param name="side">Exact side whose genuinely absent cost audit is being classified.</param>
    /// <returns>Whether this explicit no-roll choice declares a known free operation for the side.</returns>
    internal static bool HasExplicitNoRollFreeSideData(JsonObject exchange, string side)
    {
        if (side is not ("player" or "opposition") ||
            !exchange.ContainsKey(SpiritualWoundNoRollMargin.Property) ||
            (exchange.ContainsKey("actionCostAudit") && exchange["actionCostAudit"] is not JsonObject) ||
            (exchange["actionCostAudit"] is JsonObject costs && costs.ContainsKey(side)))
            return false;
        try
        {
            var choice = SpiritualWoundNoRollMargin.Read(exchange, side);
            if (!choice.IsVoluntaryNoRoll || !choice.ChosenMargin.HasValue) return false;
        }
        catch (FormatException) { return false; }
        var final = side == "opposition" ? ResolveIncomingActionFinalOperation(exchange) : null;
        var operation = side == "player" ? AfterlifeSpiritualConflictState.GetNodeString(exchange["operationType"]) :
            !string.IsNullOrWhiteSpace(final) ? final : ResolveIncomingOperation(exchange);
        if (operation is null || !IsTerminalNoCostOperation(operation)) return false;
        if (exchange["matchupAudit"] is JsonObject matchup)
        {
            var declared = AfterlifeSpiritualConflictState.GetNodeString(
                matchup[side == "player" ? "playerOperation" : "oppositionOperation"]);
            if (declared is null || !ConflictTokenEquals(declared, operation)) return false;
        }
        return true;
    }

    /// <summary>Requires the existing current source/capture/resource epoch and actual zero burden.</summary>
    /// <param name="mechanics">Existing owned frontier; absence or stale ownership never proves a free side.</param>
    /// <param name="conflict">Current source-validated conflict with original membership.</param>
    /// <param name="exchange">Same validated exchange used by the resource producer.</param>
    /// <param name="side">Exact player or opposition side.</param>
    /// <returns>Whether this actual owned no-roll side may close without an invented audit or payment.</returns>
    internal static bool IsOwnedExplicitNoRollFreeSide(
        AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext? mechanics,
        JsonObject conflict, JsonObject exchange, string side) =>
        mechanics is { IsCurrent: true } && HasExplicitNoRollFreeSideData(exchange, side) &&
        SpiritualWoundSourceSession.ReadCurrentActionCostBurden(mechanics, conflict, exchange, side) == 0;
}
