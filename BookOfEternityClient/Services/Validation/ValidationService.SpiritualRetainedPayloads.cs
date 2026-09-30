using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Checks all retained exchange rules that do not depend on original roster, wound or resource authority.
    /// </summary>
    /// <param name="exchange">
    /// Detached exchange whose basic shape and dice arithmetic have already been checked.
    /// </param>
    /// <param name="issues">
    /// Mutable diagnostic destination.
    /// </param>
    internal static void ValidateRetainedSpiritualExchangeRules(JsonObject exchange, List<ValidationIssue> issues)
    {
        const string context = "retainedSource.exchange";
        var before = exchange["before"] as JsonObject ?? throw new FormatException("Before snapshot required.");
        var after = exchange["after"] as JsonObject ?? throw new FormatException("After snapshot required.");
        foreach (var (snapshot, name) in new[] { (before, "before"), (after, "after") })
        {
            ValidateControlStateShape(snapshot["controlState"], context + "." + name + ".controlState", issues, required: false);
            ValidateTempoAdvantageShape(snapshot["tempoAdvantage"], context + "." + name + ".tempoAdvantage", issues);
        }
        if (AfterlifeControlStateRules.HasActive(before) || AfterlifeControlStateRules.HasActive(after))
            SpiritualWoundStateJson.Require(before.ContainsKey("controlState") && after.ContainsKey("controlState"),
                "Active retained control requires both snapshots.");
        var effectivePosition = ReadIntrinsicSpiritualPosition(exchange, context, issues);
        ValidateSpiritualArtOperationRules(exchange, before, after,
            AfterlifeSpiritualConflictState.GetNodeString(exchange["operationType"]),
            AfterlifeSpiritualConflictState.GetNodeString(exchange["outcome"]), context, issues,
            isCurrentExchange: true, requiresCurrentMatchupAudit: true, effectivePosition: effectivePosition);
        var dice = exchange["diceAudit"] as JsonObject ?? throw new FormatException("Dice audit required.");
        var lightModifier = SourceOfLightCapstoneState.SumLightIncarnatePlayerModifiers(dice);
        if (lightModifier != 0)
            SpiritualWoundStateJson.Require(ResolveLightIncarnateAuditTurn(exchange, dice) is > 0 &&
                lightModifier == ResolveLightIncarnateExpectedDiceBonus(exchange), "Retained Light Incarnate declaration differs.");
        ValidateRetainedSpiritualCosts(exchange, issues);
    }

    /// <summary>
    /// Checks both sides' cost shapes, registered constants, special bindings and intrinsic resource deltas.
    /// Original tiers, wound burdens, resource baselines and recovery maxima are validated during reconstruction.
    /// </summary>
    /// <param name="exchange">
    /// Retained exchange with operation and cost evidence.
    /// </param>
    /// <param name="issues">
    /// Mutable diagnostic destination.
    /// </param>
    private static void ValidateRetainedSpiritualCosts(JsonObject exchange, List<ValidationIssue> issues)
    {
        SpiritualWoundStateJson.Require(!exchange.ContainsKey("actionCostAudit") || exchange["actionCostAudit"] is JsonObject,
            "A supplied action-cost audit must be an object.");
        foreach (var side in new[] { "player", "opposition" })
        {
            var operation = side == "player" ? AfterlifeSpiritualConflictState.GetNodeString(exchange["operationType"]) :
                ResolveOppositionOperationForActionCost(exchange);
            var costRoot = exchange["actionCostAudit"] as JsonObject;
            if (ConflictTokenEquals(operation, "force_incarnation"))
            {
                if (costRoot?.ContainsKey(side) != true)
                    continue; // Actual burden necessity is checked against the reconstructed owner.
                SpiritualWoundStateJson.Require(IsPrescribedForceCostAudit(costRoot[side]),
                    "Force payment requires its prescribed zero-base audit.");
                var force = costRoot[side]!;
                var payment = SpiritualWoundStateJson.Integer(force["effectiveCost"], 1);
                var initial = SpiritualWoundStateJson.Integer(force["before"]);
                var final = SpiritualWoundStateJson.Integer(force["after"]);
                SpiritualWoundStateJson.Require(initial >= payment && final == initial - payment,
                    "Force payment affordability or delta differs.");
                continue;
            }
            if (!AfterlifeActionCostRules.TryGetDefinition(operation, out var definition))
            {
                SpiritualWoundStateJson.Require(costRoot?.ContainsKey(side) != true, "No-cost operation has a cost audit.");
                continue;
            }
            var audit = costRoot?[side] as JsonObject ?? throw new FormatException("Cost audit required.");
            SpiritualWoundStateJson.Require(ConflictTokenEqualsSingle(
                SpiritualWoundStateJson.Text(audit["operationType"]), operation), "Cost operation differs.");
            SpiritualWoundStateJson.Require(SpiritualWoundStateJson.Integer(audit["baseCost"]) == definition.BaseCost &&
                SpiritualWoundStateJson.Integer(audit["minCost"]) == definition.MinCost, "Registered costs differ.");
            var tier = (int)SpiritualWoundStateJson.Integer(audit["artTier"], 0, 5);
            var effective = SpiritualWoundStateJson.Integer(audit["effectiveCost"]);
            var before = SpiritualWoundStateJson.Integer(audit["before"]);
            var after = SpiritualWoundStateJson.Integer(audit["after"]);
            var player = side == "player";
            ValidateSpecialArtCostBindingUniqueness(exchange, operation!, player,
                AfterlifeSpiritualConflictState.GetNodeString(audit["specialArtId"]), "retainedSource.exchange", issues);
            var special = player ? ResolvePlayerSpecialArtAudit(exchange, operation!) : ResolveOppositionSpecialArtAudit(exchange, operation!);
            var standardCost = AfterlifeActionCostRules.ResolveStandardEffectiveCost(definition, tier);
            var minimumEffectiveCost = standardCost;
            if (special is not null)
            {
                var multiplier = (int)SpiritualWoundStateJson.Integer(special["costMultiplierPercent"], 101);
                SpiritualWoundStateJson.Require(SpecialArtCostAuditMatches(audit,
                    SpiritualWoundStateJson.Text(special["artId"]),
                    multiplier, standardCost),
                    "Special-art cost binding differs.");
                minimumEffectiveCost = AfterlifeActionCostRules.ComputeSpecialArtEffectiveCost(definition.MinCost, standardCost, multiplier);
            }
            SpiritualWoundStateJson.Require(effective >= minimumEffectiveCost, "Cost falls below the pre-burden minimum.");
            if (!ConflictTokenEquals(operation, "recover_spiritual_power"))
                SpiritualWoundStateJson.Require(before >= effective && after == before - effective,
                    "Ordinary cost affordability or delta differs.");
            else
            {
                var punishing = player ? ResolveMatchupOppositionOperation(exchange) :
                    AfterlifeSpiritualConflictState.GetNodeString(exchange["operationType"]);
                var punished = ConflictTokenEquals(punishing, "pressure", "maneuver", "binding", "force_binding", "force_incarnation");
                var outcome = player ? AfterlifeSpiritualConflictState.GetNodeString(exchange["outcome"]) : "success";
                var maximumDelta = punished ? 1 : ConflictTokenEquals(outcome, "success") ? 3 :
                    ConflictTokenEquals(outcome, "partial_success") ? 2 : 0;
                SpiritualWoundStateJson.Require(before >= effective,
                    "Recovery cannot pay its burden from the later gain.");
                var recovered = after - (before - effective);
                SpiritualWoundStateJson.Require(recovered >= 0 && recovered <= maximumDelta,
                    "Recovery delta exceeds its retained operation bounds.");
            }
        }
    }

    /// <summary>
    /// Checks retained dice against the recorded pool and the declared difficulty's registered arithmetic.
    /// Original difficulty and combat-condition provenance remain source-owner reconstruction checks.
    /// </summary>
    /// <param name="exchange">
    /// Retained exchange containing its dice audit.
    /// </param>
    /// <param name="evidence">
    /// Structurally validated turn context with the retained D20 pool.
    /// </param>
    /// <param name="issues">
    /// Mutable diagnostic destination.
    /// </param>
    internal static void ValidateRetainedSpiritualDiceAudit(
        JsonObject exchange, JsonObject evidence, List<ValidationIssue> issues)
    {
        var audit = exchange["diceAudit"] as JsonObject ?? throw new FormatException("Retained dice audit required.");
        AfterlifeDifficultyDefinition? difficulty = null;
        if (audit.ContainsKey("difficultyAudit"))
        {
            var declaration = audit["difficultyAudit"] as JsonObject ?? throw new FormatException("Difficulty object required.");
            var name = SpiritualWoundStateJson.Text(declaration["difficulty"]);
            SpiritualWoundStateJson.Require(AfterlifeSpiritualConflictState.DifficultyDefinitions.ContainsKey(name),
                "Unknown declared difficulty.");
            difficulty = ResolveAfterlifeDifficultyDefinition(name);
        }
        var pool = evidence["acceptedD20Values"]!.AsArray()
            .Select(value => (int)SpiritualWoundStateJson.Integer(value, 1, 20)).ToArray();
        var comparison = new AfterlifeConflictDiceContext(pool, Difficulty: difficulty);
        SpiritualWoundStateJson.Require(ValidateAfterlifeConflictDiceAudit(audit,
            "retainedSource.exchange.diceAudit", issues, comparison, requireConditionReferences: false),
            "Invalid retained dice arithmetic.");
        // Live source admission also checks widened arithmetic. Keep that stricter
        // protection when reading retained values instead of allowing Int32 wraparound.
        foreach (var side in new[] { "player", "opposition" })
        {
            var rolls = audit["diceUsed"]!.AsArray().OfType<JsonObject>().Where(row => side == "player"
                ? ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(row["side"]), "player", "playerSide", "soul")
                : ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(row["side"]), "opposition", "oppositionSide", "guardian"))
                .ToArray();
            var selected = rolls.Length == 1 ? rolls[0] : rolls.Single(row =>
                ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(row["selection"]), "selected"));
            var total = SpiritualWoundStateJson.Integer(selected["value"], 1, 20);
            foreach (var modifier in audit["modifierBreakdown"]![side]!.AsArray())
                total = checked(total + SpiritualWoundStateJson.Integer(modifier!["value"], int.MinValue, int.MaxValue));
            SpiritualWoundStateJson.Require(total == SpiritualWoundStateJson.Integer(audit[side + "Total"], int.MinValue, int.MaxValue),
                "Widened retained dice total differs.");
        }
    }

    /// <summary>
    /// Joins the applied side's selected audit to its retained original art without claiming profile membership.
    /// </summary>
    /// <param name="exchange">
    /// Retained exchange with audit payloads.
    /// </param>
    /// <param name="affectedSide">
    /// Exact affected side; the acting side is its opposite.
    /// </param>
    /// <param name="operation">
    /// Registered acting operation.
    /// </param>
    /// <param name="art">
    /// Parsed original special art, or <see langword="null"/> for a standard source.
    /// </param>
    /// <param name="issues">
    /// Mutable diagnostic destination.
    /// </param>
    internal static void ValidateRetainedSpiritualArtBinding(
        JsonObject exchange, string affectedSide, string operation, JsonObject? art, List<ValidationIssue> issues)
    {
        var audits = ReadSpecialArtAuditsForValidation(exchange, "retainedSource.exchange", issues).ToArray();
        foreach (var audit in audits)
            ValidateSpecialArtAuditShape(exchange, AfterlifeSpiritualConflictState.GetNodeString(exchange["operationType"]),
                "retainedSource.exchange", issues, audit);
        var playerActs = affectedSide == "opposition";
        var matching = audits.Where(audit => SpecialArtAuditOwnerIsPlayer(audit) == playerActs &&
            ConflictTokenEqualsSingle(AfterlifeSpiritualConflictState.GetNodeString(audit["baseOperation"]), operation)).ToArray();
        SpiritualWoundStateJson.Require(matching.Length == (art is null ? 0 : 1), "Applied special-art audit count differs.");
        if (art is null) return;
        var selected = matching[0];
        SpiritualWoundStateJson.Require(SpiritualWoundStateJson.Text(selected["artId"]) ==
            SpiritualWoundStateJson.Text(art["artId"]) && string.Equals(
                BuildActorAuthorityKey(SpiritualWoundStateJson.Text(selected["ownerActorType"]),
                    SpiritualWoundStateJson.Text(selected["ownerActorId"])),
                BuildActorAuthorityKey(SpiritualWoundStateJson.Text(art["ownerActorType"]),
                    SpiritualWoundStateJson.Text(art["ownerActorId"])), StringComparison.OrdinalIgnoreCase),
            "Applied special-art identity differs.");
        SpiritualWoundStateJson.Require(string.Equals(SpiritualWoundStateJson.Text(selected["baseOperation"]),
            SpiritualWoundStateJson.Text(art["baseOperation"]), StringComparison.OrdinalIgnoreCase) &&
            SpiritualWoundStateJson.Integer(selected["costMultiplierPercent"], 101) ==
                SpiritualWoundStateJson.Integer(art["costMultiplierPercent"], 101) &&
            selected["effectNote"] is JsonValue note && note.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text),
            "Applied special-art operation, cost or effect note differs.");
    }

    /// <summary>
    /// Checks retained original special-art data through the owning profile-art contract.
    /// The supplied owner and realm are comparison data, not proof of original profile membership.
    /// </summary>
    /// <param name="art">
    /// Detached original art from a duplicate-checked payload; it is not mutated.
    /// </param>
    /// <param name="actorKind">
    /// Nonempty retained acting actor type to compare with the art owner.
    /// </param>
    /// <param name="actorId">
    /// Nonempty retained acting actor identifier to compare with the art owner.
    /// </param>
    /// <param name="realm">
    /// Nonempty retained realm used by the owning active-effect definition checks.
    /// </param>
    /// <returns>
    /// Read-only contract diagnostics, empty when the retained payload satisfies original-art rules.
    /// </returns>
    internal static IReadOnlyList<ValidationIssue> ValidateRetainedSpiritualSpecialArt(
        JsonObject art, string actorKind, string actorId, string realm)
    {
        ArgumentNullException.ThrowIfNull(art);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(realm);
        var projection = new JsonObject { ["specialArts"] = new JsonArray(art.DeepClone()) };
        using var document = JsonDocument.Parse(projection.ToJsonString());
        var issues = new List<ValidationIssue>();
        ValidateAfterlifeProfileSpecialArts(document.RootElement, "retainedSource", actorKind, actorId,
            realm, issues, requireCurrentSpecialArtCombatEffect: false);
        return issues.AsReadOnly();
    }
}
