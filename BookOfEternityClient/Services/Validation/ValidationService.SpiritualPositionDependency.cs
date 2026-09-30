using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Recognizes only existing position diagnostics at one exact exchange modifier coordinate.
    /// Recognition permits a genuine owner probe; it does not supply a position value or correction authority.
    /// </summary>
    /// <param name="issue">
    /// Ordinary source-validation diagnostic to classify.
    /// </param>
    /// <returns>
    /// The affected projected exchange index, or <see langword="null"/> for any other diagnostic.
    /// </returns>
    internal static int? PositionDependencyIndex(ValidationIssue issue)
    {
        if (issue.Code is not ("afterlife_conflict_dice_missing_position_modifier" or
            "afterlife_conflict_dice_unexpected_position_modifier_for_contested" or
            "afterlife_conflict_dice_unexpected_position_modifier_side" or
            "afterlife_conflict_dice_unexpected_position_modifier" or
            "afterlife_conflict_dice_invalid_position_modifier_total")) return null;
        var match = Regex.Match(issue.FilePath,
            @"^activeConflict\.exchangeLog\[(\d+)\]\.diceAudit\.modifierBreakdown(?:\.(player|opposition))?$",
            RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None,
            CultureInfo.InvariantCulture, out var index) ? index : null;
    }

    /// <summary>
    /// Retains detached exact arithmetic comparisons without an execution owner or publication capability.
    /// </summary>
    internal sealed class SpiritualPositionDraftCorrection
    {
        private readonly JsonObject _before;
        private readonly JsonObject _after;
        private readonly bool _requiresCriticalNarration;

        /// <summary>
        /// Freezes a derived audit and the exact changed public coordinates.
        /// </summary>
        /// <param name="pointer">
        /// Raw carrier pointer to the affected dice audit.
        /// </param>
        /// <param name="before">
        /// Original arithmetic group whose unchanged form remains admissible for further diagnostics.
        /// </param>
        /// <param name="after">
        /// Prescribed arithmetic group; new critical narration is deliberately absent.
        /// </param>
        /// <param name="requiresCriticalNarration">
        /// Whether a newly required critical scaffold still needs two GM-authored nonempty narrative constraints.
        /// </param>
        internal SpiritualPositionDraftCorrection(string pointer, JsonObject before, JsonObject after,
            bool requiresCriticalNarration)
        {
            Pointer = pointer;
            _before = before.DeepClone().AsObject();
            _after = after.DeepClone().AsObject();
            _requiresCriticalNarration = requiresCriticalNarration;
            Fields = before.Select(pair => pair.Key).Union(after.Select(pair => pair.Key), StringComparer.Ordinal)
                .Where(key => !JsonNode.DeepEquals(before[key], after[key]))
                .Select(key => pointer + "/" + key).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        /// <summary>
        /// Gets the exact raw audit pointer, resolved from the original carrier.
        /// </summary>
        internal string Pointer { get; }

        /// <summary>
        /// Gets public correction fields; internal whole-group comparison remains stricter than these containers.
        /// </summary>
        internal IReadOnlyList<string> Fields { get; }

        /// <summary>
        /// Gets the uniquely derived ordinary dice band for prerequisite comparisons, without fabricating critical narration.
        /// </summary>
        internal string CorrectedOutcomeBand => _after["outcomeBand"]!.GetValue<string>();

        /// <summary>
        /// Requires either the unchanged audit or its complete prescribed correction, preserving independent rows and order.
        /// </summary>
        /// <param name="candidate">
        /// Actual raw audit, or <see langword="null"/> for a missing or malformed container.
        /// </param>
        /// <returns>
        /// <see langword="true"/> for one complete allowed group; partial arithmetic edits return <see langword="false"/>.
        /// </returns>
        internal bool Allows(JsonNode? candidate)
        {
            if (JsonNode.DeepEquals(_before, candidate)) return true;
            if (candidate is not JsonObject audit) return false;
            if (!_requiresCriticalNarration) return JsonNode.DeepEquals(_after, audit);
            var comparison = audit.DeepClone().AsObject();
            if (comparison["criticalResult"] is not JsonObject critical) return false;
            foreach (var field in new[] { "scaleLimit", "narrativeConstraint" })
            {
                if (critical[field] is not JsonValue text || !text.TryGetValue<string>(out var value) ||
                    string.IsNullOrWhiteSpace(value)) return false;
                critical.Remove(field);
            }
            return JsonNode.DeepEquals(_after, comparison);
        }

        /// <summary>
        /// Applies unique arithmetic to a disposable diagnostic audit without fabricating required narrative text.
        /// </summary>
        /// <param name="audit">
        /// Detached current audit, already limited to the unchanged or prescribed group.
        /// </param>
        /// <returns>
        /// <see langword="true"/> after exact projection; <see langword="false"/> when narration or valid evidence is missing.
        /// </returns>
        internal bool TryApply(JsonObject audit)
        {
            if (_requiresCriticalNarration || !Allows(audit)) return false;
            audit.Clear();
            foreach (var pair in _after) audit[pair.Key] = pair.Value?.DeepClone();
            return true;
        }
    }

    /// <summary>
    /// Projects a comparison-only audit without difficulty settings; no source authority is created.
    /// </summary>
    /// <param name="audit">
    /// Frozen valid baseline dice audit.
    /// </param>
    /// <param name="rank">
    /// Supplied bounded effective rank for pure arithmetic comparison.
    /// </param>
    /// <param name="pointer">
    /// Exact raw audit pointer.
    /// </param>
    /// <param name="signedDice">
    /// Original D20 values against which immutable selections are checked.
    /// </param>
    /// <returns>
    /// Detached correction, or <see langword="null"/> when the input cannot be projected exactly.
    /// </returns>
    internal static SpiritualPositionDraftCorrection? ProjectPositionDraftCorrection(JsonObject audit,
        int rank, string pointer, int[] signedDice) =>
        ProjectPositionDraftCorrection(audit, rank, pointer, signedDice, null);

    /// <summary>
    /// Projects only existing position rows and ordinary dice arithmetic from a supplied causal rank.
    /// This pure comparison helper does not prove the rank's ownership; live callers must authenticate their current mechanics.
    /// </summary>
    /// <param name="audit">
    /// Frozen raw dice audit with valid original dice selection and independently consistent arithmetic.
    /// </param>
    /// <param name="rank">
    /// Effective rank already computed from the genuine current frontier, from minus two through two.
    /// </param>
    /// <param name="pointer">
    /// Raw carrier pointer to this audit.
    /// </param>
    /// <param name="signedDice">
    /// Exact original signed D20 pool; absence does not authorize a projection.
    /// </param>
    /// <param name="difficulty">
    /// Original readable difficulty definition, or <see langword="null"/> when the original has no difficulty settings.
    /// </param>
    /// <returns>
    /// Detached exact comparison data, or <see langword="null"/> for malformed or ambiguous baseline evidence.
    /// </returns>
    private static SpiritualPositionDraftCorrection? ProjectPositionDraftCorrection(JsonObject audit,
        int rank, string pointer, int[] signedDice, AfterlifeDifficultyDefinition? difficulty)
    {
        if (rank is < -2 or > 2 || signedDice.Length == 0) return null;
        var issues = new List<ValidationIssue>();
        var diceContext = new AfterlifeConflictDiceContext(signedDice, Difficulty: difficulty);
        if (!ValidateAfterlifeConflictDiceAudit(audit, "dependent.diceAudit", issues, diceContext,
                requireConditionReferences: false) || issues.Count != 0 || CollectConflictPositionModifiers(audit).Count > 1)
            return null;
        var after = audit.DeepClone().AsObject();
        if (after["modifierBreakdown"] is not JsonObject modifiers ||
            modifiers["player"] is not JsonArray || modifiers["opposition"] is not JsonArray) return null;
        var targetSide = rank > 0 ? "player" : "opposition";
        foreach (var side in new[] { "player", "opposition" })
        {
            var rows = modifiers[side]!.AsArray();
            var insertion = rows.Count;
            for (var index = rows.Count - 1; index >= 0; index--)
            {
                var probe = new JsonObject { ["modifierBreakdown"] = new JsonObject
                { [side] = new JsonArray(rows[index]?.DeepClone()) } };
                if (CollectConflictPositionModifiers(probe).Count == 0) continue;
                insertion = index;
                rows.RemoveAt(index);
            }
            if (rank != 0 && side == targetSide)
                rows.Insert(insertion, new JsonObject
                {
                    ["modifierType"] = "conflict_position", ["source"] = "conflictPosition",
                    ["position"] = SpiritualPositionToken(rank), ["value"] = Math.Abs(rank) * 2
                });
        }
        var valid = true;
        var selected = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var side in new[] { "player", "opposition" })
        {
            var rolls = new List<DiceRollEntry>();
            var used = audit["diceUsed"]!.AsArray();
            for (var index = 0; index < used.Count; index++)
            {
                var row = used[index]!;
                var actualSide = AfterlifeSpiritualConflictState.GetNodeString(row["side"]);
                if (!(side == "player" ? ConflictTokenEquals(actualSide, "player", "playerSide", "soul") :
                        ConflictTokenEquals(actualSide, "opposition", "oppositionSide", "guardian"))) continue;
                rolls.Add(new(index, row["sourceIndex"]!.GetValue<int>(), row["value"]!.GetValue<int>(),
                    AfterlifeSpiritualConflictState.GetNodeString(row["selection"])));
            }
            var die = ValidateDiceRollSelection(audit, side, rolls, "dependent.diceAudit", issues, ref valid);
            if (!valid || die is null) return null;
            selected[side] = die.Value;
            after[side + "Total"] = checked(die.Value + SumDiceAuditModifiers(after, side,
                "dependent.diceAudit.modifierBreakdown." + side, issues, ref valid));
        }
        if (!valid || issues.Count != 0) return null;
        var margin = checked(after["playerTotal"]!.GetValue<int>() - after["oppositionTotal"]!.GetValue<int>());
        var marginBand = ExpectedAfterlifeConflictOutcomeBand(margin);
        var band = ExpectedAfterlifeConflictOutcomeBand(margin, selected["player"], selected["opposition"]);
        after["margin"] = margin;
        after["outcomeBand"] = band;
        var needsNarration = after["criticalResult"] is null && marginBand != band;
        if (after["criticalResult"] is JsonObject || needsNarration)
        {
            if (after["criticalResult"] is not JsonObject) after["criticalResult"] = new JsonObject();
            var critical = after["criticalResult"]!;
            critical["playerNaturalRoll"] = selected["player"];
            critical["oppositionNaturalRoll"] = selected["opposition"];
            critical["marginOutcomeBand"] = marginBand;
            critical["normalizedOutcomeBand"] = band;
        }
        return new(pointer, audit, after, needsNarration);
    }

    internal sealed partial class SpiritualWoundSourceSession
    {
        /// <summary>
        /// Projects arithmetic with the exact current source's original dice and difficulty settings.
        /// </summary>
        /// <param name="capture">
        /// Current capture that owns this source prefix.
        /// </param>
        /// <param name="audit">
        /// Frozen raw audit from the current frontier.
        /// </param>
        /// <param name="rank">
        /// Effective rank read from that frontier's genuine mechanics.
        /// </param>
        /// <param name="pointer">
        /// Actual raw-carrier audit coordinate.
        /// </param>
        /// <returns>
        /// Detached correction or <see langword="null"/> for invalid arithmetic; foreign or revoked ownership throws.
        /// </returns>
        internal SpiritualPositionDraftCorrection? ProjectDependentPositionCorrection(SpiritualOriginalTurnCapture capture,
            JsonObject audit, int rank, string pointer)
        {
            _ = ReadMechanicsConflict(capture);
            return ProjectPositionDraftCorrection(audit, rank, pointer, ReadOriginalAcceptedD20Values(), ReadOriginalDifficulty());
        }
    }

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Adds position permissions only for the exact next original exchange under current causal ownership.
        /// </summary>
        /// <param name="original">
        /// Signed original conflict root used for raw carrier precedence and historical-prefix length.
        /// </param>
        /// <param name="baseline">
        /// Frozen complete raw draft whose future rows cannot grant their own permission.
        /// </param>
        /// <param name="issues">
        /// Genuine diagnostics at the current execution frontier.
        /// </param>
        /// <param name="frontier">
        /// Separately derived current resource balances for existing cost permissions.
        /// </param>
        /// <param name="mechanics">
        /// Current owner-authenticated mechanics context, or <see langword="null"/> when there are no position errors.
        /// </param>
        /// <returns>
        /// A comparison policy, or <see langword="null"/> when any coordinate or causal rank is unavailable.
        /// </returns>
        private SpiritualWoundDependentDraftPolicy? CreateC2DependentPolicy(JsonObject original, JsonObject baseline,
            IReadOnlyList<ValidationIssue> issues, AfterlifeConflictActionPointProjection? frontier,
            AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext? mechanics)
        {
            var corrections = new List<SpiritualPositionDraftCorrection>();
            var bindings = new List<SpiritualBindingDraftCorrection>();
            foreach (var index in issues.Select(issue => PositionDependencyIndex(issue) ?? BindingDependencyIndex(issue)).Where(index => index.HasValue)
                         .Select(index => index!.Value).Distinct())
            {
                if (mechanics is null || !mechanics.IsCurrent ||
                    original["activeConflict"] is not JsonObject conflict ||
                    conflict["exchangeLog"] is not JsonArray prior ||
                    index != prior.Count + _closedExchangeEvidence.Count ||
                    !_source.TryReadInitialActiveExchangeInventory(out var conflictId, out var ids) ||
                    _closedExchangeEvidence.Count >= ids.Length ||
                    AfterlifeSpiritualConflictState.ResolveRawExchange(original, baseline, index) is not { } raw ||
                    raw.Exchange["exchangeId"]?.GetValue<string>() != ids[_closedExchangeEvidence.Count] ||
                    conflict["conflictId"]?.GetValue<string>() != conflictId ||
                    raw.Exchange["diceAudit"] is not JsonObject audit) return null;
                var rank = SpiritualWoundSourceSession.ReadCurrentEffectivePosition(mechanics, conflict, raw.Exchange);
                if (rank is null || _source.ProjectDependentPositionCorrection(this, audit, rank.Value,
                        raw.Pointer + "/diceAudit") is not { } correction) return null;
                corrections.Add(correction);
                if (CreateBindingCorrection(raw.Exchange, raw.Pointer, rank.Value, correction) is { } binding)
                    bindings.Add(binding);
            }
            return SpiritualWoundDependentDraftPolicy.Create(original, baseline, issues, frontier, corrections, bindings);
        }
    }
}
