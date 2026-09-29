using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualWoundSourceSession
    {
        /// <summary>
        /// Checks applicable actor-bound wound burdens from the actual resource routing epoch before ordinary exchange validation.
        /// </summary>
        /// <param name="conflict">
        /// Signed conflict establishing actor membership.
        /// </param>
        /// <param name="exchange">
        /// Next source exchange whose declared operation and audit are checked.
        /// </param>
        /// <param name="coordinate">
        /// Exact source coordinate used for diagnostics.
        /// </param>
        /// <param name="previous">
        /// Accepted prior frontier used to authenticate the authored tempo snapshot.
        /// </param>
        /// <param name="issues">
        /// Receives stale-context, missing-burden or unfinished-consumer issues.
        /// </param>
        private void ValidateCurrentWoundContributions(JsonObject conflict, JsonObject exchange,
            JsonObject previous, string coordinate, List<ValidationIssue> issues)
        {
            if (_mechanicsContext == null)
                return; // Strict legacy source path has no installed live generation.
            if (!_mechanicsContext.IsCurrent)
            {
                issues.Add(SourceIssue(coordinate, "spiritual_wound_mechanics_stale", "same owned resource routing epoch"));
                return;
            }
            foreach (var contribution in _mechanicsContext.Contributions)
            {
                var side = contribution.ResolvedSide;
                var operation = ResolveWoundOperation(exchange, side);
                var actor = side == "player" ? ActorKey(conflict["playerSide"]?["leadContestant"] as JsonObject) :
                    ResolveActingOpposition(exchange, conflict);
                if (!ConflictTokenEquals(operation, contribution.Operation) || actor == null)
                    continue;
                var actorParts = actor.Split(':', 2);
                if (actorParts.Length != 2 || actorParts[1] != contribution.Actor.TargetId)
                    continue;
                if (contribution.Profile == "spiritual_action_cost_burden" &&
                    (OperationHasActionCost(operation) || ConflictTokenEquals(operation, "force_incarnation")))
                    continue; // The ordinary cost formula consumes the same owned context.
                if (contribution.Profile == "spiritual_art_restriction" &&
                    contribution.Magnitude.ValueKind == System.Text.Json.JsonValueKind.String &&
                    contribution.Magnitude.GetString() == "forbid")
                {
                    issues.Add(SourceIssue(coordinate, "spiritual_wound_art_forbidden",
                        "an available art for the acting participant under the current wound generation"));
                    continue;
                }
                if (contribution.Profile == "spiritual_tempo_burden")
                {
                    if (!SameTempoWindow(previous["tempoAdvantage"], exchange["before"]?["tempoAdvantage"]))
                        issues.Add(SourceIssue(coordinate + ".before.tempoAdvantage",
                            "spiritual_wound_tempo_frontier_mismatch", "the accepted prior tempo snapshot"));
                    if (IsNewTempoGain(previous["tempoAdvantage"], exchange["after"]?["tempoAdvantage"], side))
                        issues.Add(SourceIssue(coordinate + ".after.tempoAdvantage",
                            "spiritual_wound_tempo_gain_forbidden", "no new tempo gain for the wounded actor's operation"));
                    continue;
                }
                if (contribution.Profile == "spiritual_position_burden")
                    continue; // The position consumer validates the exact current actor and operation.
                if (contribution.Profile != "spiritual_roll_hindrance")
                {
                    issues.Add(SourceIssue(coordinate, "spiritual_wound_profile_integration_required",
                        "current-generation consumer for " + contribution.Profile));
                    continue;
                }
                if (exchange["diceAudit"] != null && exchange["diceAudit"] is not JsonObject)
                {
                    issues.Add(SourceIssue(coordinate + ".diceAudit", "spiritual_wound_dice_audit_invalid",
                        "an audit object when optional dice evidence is supplied"));
                    continue;
                }
                if (exchange["diceAudit"] is not JsonObject &&
                    !ExchangeDiceAuditRequired(exchange, ExactString(exchange["outcome"])))
                    continue;
                var valid = true;
                var mode = exchange["diceAudit"]?["rollMode"]?[side] as JsonObject;
                var level = mode == null ? 0 : ReadRollModeSources(mode, "disadvantageSources",
                    coordinate + ".diceAudit.rollMode." + side + ".disadvantageSources", false, issues, ref valid);
                if (!valid || level < 1)
                    issues.Add(SourceIssue(coordinate + ".diceAudit.rollMode." + side,
                        "spiritual_wound_roll_hindrance_missing", "the applicable owned wound's disadvantage before cancellation"));
            }
        }

        /// <summary>
        /// Determines whether an available window grants new tempo to the affected side.
        /// </summary>
        /// <param name="before">
        /// Accepted prior window, or null when none existed.
        /// </param>
        /// <param name="after">
        /// Proposed resulting window, or null when no window remains.
        /// </param>
        /// <param name="side">
        /// Side affected by the owned contribution.
        /// </param>
        /// <returns>
        /// True for a new, restored, transferred or strengthened available window; otherwise false.
        /// </returns>
        private static bool IsNewTempoGain(JsonNode? before, JsonNode? after, string side)
        {
            if (after is not JsonObject next || !ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(next["status"]), "available") ||
                !ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(next["ownerSide"]), side))
                return false;
            if (before is not JsonObject prior || !ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(prior["status"]), "available") ||
                !ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(prior["ownerSide"]), side))
                return true;
            return !string.Equals(ReadTempoIdentity(prior), ReadTempoIdentity(next), StringComparison.OrdinalIgnoreCase) ||
                   !string.Equals(AfterlifeSpiritualConflictState.GetNodeString(prior["sourceExchangeId"]),
                       AfterlifeSpiritualConflictState.GetNodeString(next["sourceExchangeId"]), StringComparison.OrdinalIgnoreCase) ||
                   (!ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(prior["level"]), "great_advantage") &&
                    ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(next["level"]), "great_advantage"));
        }

        /// <summary>
        /// Compares the mechanical identity of tempo snapshots without comparing narrative text.
        /// </summary>
        /// <param name="accepted">
        /// Trusted prior window, or null when absent.
        /// </param>
        /// <param name="declared">
        /// Authored before window, or null when absent.
        /// </param>
        /// <returns>
        /// True when the mechanical fields match under ordinary tempo normalization; otherwise false.
        /// </returns>
        private static bool SameTempoWindow(JsonNode? accepted, JsonNode? declared)
        {
            if (accepted is not JsonObject prior || declared is not JsonObject current)
                return accepted == null && declared == null;
            foreach (var field in new[] { "status", "ownerSide", "level", "sourceOperation", "sourceExchangeId" })
                if (!string.Equals(AfterlifeSpiritualConflictState.GetNodeString(prior[field]),
                    AfterlifeSpiritualConflictState.GetNodeString(current[field]), StringComparison.OrdinalIgnoreCase))
                    return false;
            return string.Equals(ReadTempoIdentity(prior), ReadTempoIdentity(current), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reads the ordinary tempo identity, applying the source alias after string normalization.
        /// </summary>
        /// <param name="tempo">
        /// Tempo object whose primary identity may be absent or blank.
        /// </param>
        /// <returns>
        /// The normalized primary identity or source alias, or null when neither is valid.
        /// </returns>
        private static string? ReadTempoIdentity(JsonObject tempo) =>
            AfterlifeSpiritualConflictState.GetNodeString(tempo["advantageId"]) ??
            AfterlifeSpiritualConflictState.GetNodeString(tempo["sourceId"]);

        /// <summary>
        /// Reads a tempo denial only from the current owned mechanics context.
        /// </summary>
        /// <param name="mechanics">
        /// Current resource-owned context, or null for legacy validation.
        /// </param>
        /// <param name="conflict">
        /// Signed conflict actor membership.
        /// </param>
        /// <param name="exchange">
        /// Exchange whose player operation is being validated.
        /// </param>
        /// <returns>
        /// True when an applicable owned wound denies the player's tempo gain; otherwise false.
        /// </returns>
        internal static bool HasCurrentPlayerTempoBurden(
            AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext? mechanics,
            JsonObject conflict, JsonObject exchange)
        {
            if (mechanics == null)
                return false;
            if (!mechanics.IsCurrent)
                throw new InvalidOperationException("Current owned wound mechanics are required for tempo denial.");
            var actor = ActorKey(conflict["playerSide"]?["leadContestant"] as JsonObject)?.Split(':', 2);
            return actor is { Length: 2 } && mechanics.Contributions.Any(contribution =>
                contribution.Profile == "spiritual_tempo_burden" && contribution.ResolvedSide == "player" &&
                contribution.Actor.TargetId == actor[1] &&
                ConflictTokenEquals(ResolveWoundOperation(exchange, "player"), contribution.Operation));
        }

        /// <summary>
        /// Sums applicable wound cost increments from the exact live mechanics context.
        /// </summary>
        /// <param name="mechanics">
        /// Owned current context; null preserves the ordinary legacy cost formula.
        /// </param>
        /// <param name="conflict">
        /// Validated conflict membership identifying the acting soul or opposition.
        /// </param>
        /// <param name="exchange">
        /// Current exchange providing the actual operation and optional opposition actor.
        /// </param>
        /// <param name="side">
        /// Exact player or opposition side whose action cost is being checked.
        /// </param>
        /// <returns>
        /// The checked total increment, or zero when no applicable wound contributes.
        /// </returns>
        internal static long ReadCurrentActionCostBurden(
            AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext? mechanics,
            JsonObject conflict, JsonObject exchange, string side)
        {
            if (mechanics == null)
                return 0;
            if (!mechanics.IsCurrent)
                throw new InvalidOperationException("Current owned wound mechanics are required for action costs.");
            var operation = side == "player" ? AfterlifeSpiritualConflictState.GetNodeString(exchange["operationType"]) :
                ResolveOppositionOperationForActionCost(exchange);
            var actor = side == "player" ? ActorKey(conflict["playerSide"]?["leadContestant"] as JsonObject) :
                ResolveActingOpposition(exchange, conflict);
            var actorParts = actor?.Split(':', 2);
            if (actorParts is not { Length: 2 })
                return 0;
            long burden = 0;
            foreach (var contribution in mechanics.Contributions)
                if (contribution.Profile == "spiritual_action_cost_burden" &&
                    contribution.ResolvedSide == side && ConflictTokenEquals(operation, contribution.Operation) &&
                    contribution.Actor.TargetId == actorParts[1])
                    burden = checked(burden + contribution.Magnitude.GetInt32());
            return burden;
        }

        /// <summary>
        /// Reads the declared operation without limiting wound applicability to the ordinary cost table.
        /// </summary>
        /// <param name="exchange">
        /// Current exchange whose action and matchup remain subject to ordinary validation.
        /// </param>
        /// <param name="side">
        /// Exact player or opposition side.
        /// </param>
        /// <returns>
        /// The authored operation, preserving its token spelling, or null when no operation is declared.
        /// </returns>
        private static string? ResolveWoundOperation(JsonObject exchange, string side) =>
            side == "player" ? AfterlifeSpiritualConflictState.GetNodeString(exchange["operationType"]) :
            AfterlifeSpiritualConflictState.GetNodeString(exchange["matchupAudit"]?["oppositionOperation"]) ??
            ResolveIncomingActionFinalOperation(exchange) ?? AfterlifeSpiritualConflictState.GetNodeString(exchange["incomingAction"]?["operationType"]);
    }
}
