using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Reads the next source frontier from this capture's actual resource progression.
        /// </summary>
        /// <param name="source">
        /// Exact source session retained by this capture.
        /// </param>
        /// <param name="limit">
        /// Number of current exchanges permitted through the next resource boundary; zero on rejection.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when this current capture can advance; otherwise <see langword="false"/>.
        /// </returns>
        internal bool TryReadNextSourceFrontier(SpiritualWoundSourceSession source, out int limit)
        {
            limit = 0;
            if (_resources is not { Result: null } ||
                _originalPrefix == null || !OwnsOriginalPrefix(source, _originalPrefix) ||
                _effects is { HasPendingWoundIntegration: true } ||
                _lastResourceStep is { PendingResource: not null } or { PendingExchange: not null })
                return false;
            limit = checked(_nextResourceOrdinal + 1);
            return true;
        }
    }

    internal sealed partial class SpiritualWoundSourceSession
    {
        /// <summary>
        /// Compares a proposed cold correction with the complete original accepted exchange inventory.
        /// The ordinary source continuation still validates every exchange and permitted field.
        /// </summary>
        /// <param name="originalInputs">
        /// Immutable original draft A retained by the capture.
        /// </param>
        /// <param name="candidateInputs">
        /// Detached proposed layer with the same registered path inventory.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only for the same active conflict, exact exchange count,
        /// and unchanged recent-conflict inventory.
        /// </returns>
        internal bool MatchesColdOriginalExchangeInventory(
            SpiritualOriginalDraftInputs originalInputs, SpiritualOriginalDraftInputs candidateInputs)
        {
            ArgumentNullException.ThrowIfNull(originalInputs);
            ArgumentNullException.ThrowIfNull(candidateInputs);
            if (!originalInputs.MatchesIdentity(SessionId, RequestId, SnapshotToken, TurnNumber) ||
                !candidateInputs.MatchesIdentity(SessionId, RequestId, SnapshotToken, TurnNumber))
                return false;
            JsonObject Project(SpiritualOriginalDraftInputs inputs)
            {
                var raw = SpiritualWoundStateJson.Parse(
                    inputs.ReadText(AfterlifeSpiritualConflictState.StatePath) ?? "{}");
                var projected = raw[AfterlifeSpiritualConflictState.ResponseField] is JsonObject update
                    ? AfterlifeSpiritualConflictState.ApplyUpdate(_originalConflict, update, _projectionClock)
                    : raw.DeepClone().AsObject();
                projected.Remove(AfterlifeSpiritualConflictState.ResponseField);
                if (projected.ContainsKey("lastInvalidUpdate"))
                    throw new InvalidOperationException("Invalid dependent conflict update.");
                return projected;
            }
            var originalRoot = Project(originalInputs);
            var candidateRoot = Project(candidateInputs);
            if (TryReadSingleTerminal(_originalConflict, originalRoot, out _))
                return JsonNode.DeepEquals(originalRoot, candidateRoot);
            var original = originalRoot["activeConflict"] as JsonObject;
            var candidate = candidateRoot["activeConflict"] as JsonObject;
            return original != null && candidate != null &&
                JsonNode.DeepEquals(originalRoot["recentConflicts"], candidateRoot["recentConflicts"]) &&
                ExactString(original["conflictId"]) == ExactString(candidate["conflictId"]) &&
                original["exchangeLog"] is JsonArray initialLog &&
                candidate["exchangeLog"] is JsonArray proposedLog &&
                initialLog.Count == proposedLog.Count;
        }

        /// <summary>
        /// Bounds the private execution view without changing signed originals or retained candidate input.
        /// </summary>
        /// <param name="projected">
        /// Detached candidate projection whose unexecuted active suffix may be removed.
        /// </param>
        /// <returns>
        /// The execution view through the capture-owned exchange limit; legacy and other lifecycle contours are unchanged.
        /// </returns>
        private JsonObject ProjectAcceptedExchangeFrontier(JsonObject projected)
        {
            if (_acceptedExchangeLimit is not { } limit ||
                _originalConflict["activeConflict"] is not JsonObject original ||
                projected["activeConflict"] is not JsonObject active ||
                ExactString(original["conflictId"]) != ExactString(active["conflictId"]))
                return projected;
            var historical = original["exchangeLog"] as JsonArray ?? new JsonArray();
            if (active["exchangeLog"] is not JsonArray log || log.Count < historical.Count)
                throw new InvalidOperationException("complete original exchange prefix required");
            for (var index = 0; index < historical.Count; index++)
                if (!JsonNode.DeepEquals(historical[index], log[index]))
                    throw new InvalidOperationException("original exchange prefix cannot change");
            var count = checked(historical.Count + limit);
            if (log.Count <= count)
                return projected;
            JsonObject state = original;
            JsonNode? control = original["controlState"];
            for (var index = historical.Count; index < count; index++)
            {
                var exchange = log[index] as JsonObject ??
                    throw new InvalidOperationException("source exchange must be an object");
                state = exchange["after"] as JsonObject ??
                    throw new InvalidOperationException("source exchange after state required");
                control = ResolveNextPriorControlState(control, exchange);
            }
            while (log.Count > count)
                log.RemoveAt(log.Count - 1);
            foreach (var field in new[] { "playerSideStrain", "oppositionSideStrain", "conflictPosition", "tempoAdvantage" })
            {
                if (state.ContainsKey(field))
                    active[field] = state[field]?.DeepClone();
                else
                    active.Remove(field);
            }
            if (control != null)
                active["controlState"] = control.DeepClone();
            else
                active.Remove("controlState");
            return projected;
        }

        /// <summary>
        /// Verifies that a freshly prepared candidate contains no source work beyond the
        /// exchanges already closed by the owning original capture.
        /// </summary>
        /// <param name="retainedOwner">
        /// Current source owner whose checked coordinate stream must remain unchanged.
        /// </param>
        /// <param name="closedExchangeCount">
        /// Number of current exchanges closed by the owning original capture.
        /// </param>
        /// <returns>
        /// Validation issues describing unfinished source work, or an empty collection when
        /// the fresh candidate is exhausted at the retained frontier.
        /// </returns>
        private IReadOnlyList<ValidationIssue> ValidateCompletionFrontier(
            SpiritualWoundSourceSession retainedOwner, int closedExchangeCount)
        {
            if ((_acceptedExchangeLimit is { } limit && limit != closedExchangeCount) ||
                _checkedExchanges.Count != retainedOwner._checkedExchanges.Count ||
                !_checkedExchanges.SequenceEqual(retainedOwner._checkedExchanges, StringComparer.Ordinal))
            {
                return CompletionFailure("spiritual_source_exchange_incomplete",
                    "the fresh checked source stream must equal the capture-owned closed exchange frontier");
            }
            if (_pending.Any(value => value.Kind != SpiritualSourceRequirement.TerminalClosure ||
                    TerminalPreparation == null || closedExchangeCount != 1) || _missingActionCostAudit != null)
            {
                return CompletionFailure("spiritual_source_requirement_unresolved",
                    "the fresh source candidate must have no pending requirement or incomplete action-cost audit");
            }

            var complete = ProjectFullCandidateConflict();
            if (_originalConflict["activeConflict"] is JsonObject original &&
                complete["activeConflict"] is JsonObject active &&
                ExactString(original["conflictId"]) == ExactString(active["conflictId"]))
            {
                var historical = original["exchangeLog"] as JsonArray ?? new JsonArray();
                if (active["exchangeLog"] is not JsonArray log ||
                    log.Count != checked(historical.Count + closedExchangeCount))
                {
                    return CompletionFailure("spiritual_source_exchange_incomplete",
                        "the complete fresh candidate must end at the capture-owned closed exchange frontier");
                }
            }
            return Array.Empty<ValidationIssue>();
        }

        /// <summary>
        /// Creates one source-path diagnostic for a rejected completion frontier.
        /// </summary>
        /// <param name="code">
        /// Stable diagnostic code identifying the failed completion invariant.
        /// </param>
        /// <param name="expected">
        /// Description of the source evidence required for completion.
        /// </param>
        /// <returns>
        /// A single validation issue associated with the spiritual conflict state.
        /// </returns>
        private static IReadOnlyList<ValidationIssue> CompletionFailure(string code, string expected) =>
            new[] { SourceIssue(AfterlifeSpiritualConflictState.StatePath, code, expected) };

        /// <summary>
        /// Checks retained future action coordinates before projecting away unexecuted rows.
        /// </summary>
        /// <param name="candidate">
        /// Fresh complete candidate inputs read under the canonical lease.
        /// </param>
        private void ValidateRetainedCandidateCoordinates(Dictionary<string, string?> candidate)
        {
            if (_acceptedExchangeLimit == null)
                return;
            JsonObject Read(Dictionary<string, string?> values)
            {
                var raw = JsonNode.Parse(values[AfterlifeSpiritualConflictState.StatePath] ?? "{}")!.AsObject();
                return raw[AfterlifeSpiritualConflictState.ResponseField] is JsonObject update
                    ? AfterlifeSpiritualConflictState.ApplyUpdate(_originalConflict, update, _projectionClock)
                    : raw;
            }
            var oldActive = Read(_candidate)["activeConflict"] as JsonObject;
            var active = Read(candidate)["activeConflict"] as JsonObject;
            if (oldActive == null || active == null ||
                ExactString(oldActive["conflictId"]) != ExactString(active["conflictId"]))
                return; // Terminal/start contours retain their existing validation.
            if (oldActive["exchangeLog"] is not JsonArray oldLog || active["exchangeLog"] is not JsonArray log ||
                log.Count < oldLog.Count)
                throw new InvalidOperationException("retained candidate exchange coordinates required");
            for (var index = 0; index < oldLog.Count; index++)
            {
                if (ExactString(oldLog[index]?["exchangeId"]) != ExactString(log[index]?["exchangeId"]))
                    throw new InvalidOperationException("frozen future exchange coordinates cannot change");
                var acceptedCount = (_candidateConflict["activeConflict"]?["exchangeLog"] as JsonArray)?.Count ?? 0;
                if (index >= acceptedCount)
                    ValidateFutureExchangeEvidence(oldLog[index]!.AsObject(), log[index]!.AsObject());
            }
        }

        /// <summary>
        /// Freezes unexecuted action evidence while permitting dependent arithmetic and a newly required closed critical audit.
        /// Existing critical narration and unrelated evidence remain unchanged.
        /// </summary>
        /// <param name="previous">
        /// Previously observed future exchange, including original action and dice coordinates.
        /// </param>
        /// <param name="candidate">
        /// Proposed replacement at the same future coordinate; acceptance still requires full exchange validation.
        /// </param>
        private static void ValidateFutureExchangeEvidence(JsonObject previous, JsonObject candidate)
        {
            RequireSameExcept(previous, candidate, "before", "after", "outcome", "diceAudit", "actionCostAudit");
            foreach (var field in new[] { "before", "after" })
                CompareObjects(previous[field], candidate[field],
                    "playerSideStrain", "oppositionSideStrain", "conflictPosition", "controlState", "tempoAdvantage");
            CompareObjects(previous["diceAudit"], candidate["diceAudit"],
                "playerTotal", "oppositionTotal", "margin", "outcomeBand", "modifierBreakdown", "rollMode", "diceUsed", "criticalResult");
            var oldCritical = previous["diceAudit"]?["criticalResult"];
            var newCritical = candidate["diceAudit"]?["criticalResult"];
            if (oldCritical is null && newCritical is JsonObject addedCritical)
            {
                // The owner-derived correction policy limits when this scaffold may be added.
                // Full source validation still proves the rolls and outcome bands.
                string[] fields = ["playerNaturalRoll", "oppositionNaturalRoll", "marginOutcomeBand",
                    "normalizedOutcomeBand", "scaleLimit", "narrativeConstraint"];
                if (addedCritical.Count != fields.Length || fields.Any(field => !addedCritical.ContainsKey(field)) ||
                    new[] { "scaleLimit", "narrativeConstraint" }.Any(field =>
                        addedCritical[field] is not JsonValue text || !text.TryGetValue<string>(out var value) ||
                        string.IsNullOrWhiteSpace(value)))
                    throw new InvalidOperationException("new future critical audit requires its closed authored scaffold");
            }
            else
            {
                // Existing narration and unknown siblings remain exact, including after a
                // previously corrected exchange becomes part of the retained source prefix.
                CompareObjects(oldCritical, newCritical, "playerNaturalRoll", "oppositionNaturalRoll",
                    "marginOutcomeBand", "normalizedOutcomeBand");
            }
            var oldDice = previous["diceAudit"]?["diceUsed"] as JsonArray;
            var dice = candidate["diceAudit"]?["diceUsed"] as JsonArray;
            if (oldDice == null || dice == null || oldDice.Count != dice.Count)
            {
                if (!JsonNode.DeepEquals(previous["diceAudit"]?["diceUsed"], candidate["diceAudit"]?["diceUsed"]))
                    throw new InvalidOperationException("original future dice claims required");
            }
            else
                for (var index = 0; index < oldDice.Count; index++)
                    CompareObjects(oldDice[index], dice[index], "selection");
            var oldCosts = previous["actionCostAudit"] as JsonObject;
            var newCosts = candidate["actionCostAudit"] as JsonObject;
            var addsRoot = !previous.ContainsKey("actionCostAudit") && newCosts is { Count: > 0 } &&
                newCosts.All(pair => pair.Key is "player" or "opposition");
            var removesRoot = !candidate.ContainsKey("actionCostAudit") && oldCosts is { Count: > 0 } &&
                oldCosts.All(pair => pair.Key is "player" or "opposition");
            if (addsRoot || removesRoot)
            {
                // Each added or removed side must independently qualify below.
            }
            else
                CompareObjects(previous["actionCostAudit"], candidate["actionCostAudit"], "player", "opposition");
            foreach (var side in new[] { "player", "opposition" })
            {
                if (oldCosts?.ContainsKey(side) != true && newCosts?.ContainsKey(side) == true &&
                    ConflictTokenEquals(ResolveSpiritualCostOperation(previous, side), "force_incarnation") &&
                    IsPrescribedForceCostAudit(newCosts[side]))
                    continue;
                if (oldCosts?.ContainsKey(side) == true && newCosts?.ContainsKey(side) != true &&
                    ConflictTokenEquals(ResolveSpiritualCostOperation(previous, side), "force_incarnation") &&
                    IsPrescribedForceCostAudit(oldCosts[side]))
                    continue; // Only current owner reconstruction can prove that payment is no longer required.
                CompareObjects(oldCosts?[side], newCosts?[side], "effectiveCost", "before", "after");
            }

            static void CompareObjects(JsonNode? before, JsonNode? after, params string[] mutable)
            {
                if (before is JsonObject oldObject && after is JsonObject newObject)
                    RequireSameExcept(oldObject, newObject, mutable);
                else if (!JsonNode.DeepEquals(before, after))
                    throw new InvalidOperationException("future evidence structure cannot change");
            }
        }
    }
}
