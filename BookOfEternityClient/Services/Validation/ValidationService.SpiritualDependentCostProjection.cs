using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Computes the existing cost formula from authoritative tier and independently owned wound burden.
    /// </summary>
    /// <param name="definition">
    /// Client-owned base and minimum for the resolved operation.
    /// </param>
    /// <param name="artTier">
    /// Tier resolved from the retained pre-turn actor profile rather than the submitted cost audit.
    /// </param>
    /// <param name="specialArtAudit">
    /// Selected special-art evidence, or null for an ordinary art; callers separately validate its bindings.
    /// </param>
    /// <param name="woundBurden">
    /// Applicable current owner-derived increment, applied after any special-art multiplier.
    /// </param>
    /// <returns>
    /// Standard cost and final cost without granting source or resource execution authority.
    /// </returns>
    internal static (int Standard, long Effective) ProjectSpiritualActionCost(
        AfterlifeActionCostRules.Definition definition, int artTier, JsonObject? specialArtAudit, long woundBurden)
    {
        var standard = AfterlifeActionCostRules.ResolveStandardEffectiveCost(definition, artTier);
        long effective = standard;
        if (specialArtAudit is not null &&
            TryGetJsonNodeInt(specialArtAudit["costMultiplierPercent"], out var multiplier) && multiplier > 100)
            effective = AfterlifeActionCostRules.ComputeSpecialArtEffectiveCost(definition.MinCost, standard, multiplier);
        return (standard, checked(effective + woundBurden));
    }

    /// <summary>
    /// Projects the existing action-only recovery interval after payment, without selecting an ambiguous outcome.
    /// </summary>
    /// <param name="before">
    /// Integral resource balance before paying the action's burden.
    /// </param>
    /// <param name="cost">
    /// Nonnegative action cost that must be affordable before recovery.
    /// </param>
    /// <param name="maximum">
    /// Positive canonical resource maximum.
    /// </param>
    /// <param name="outcome">
    /// Existing side outcome; opposition uses its ordinary success rule.
    /// </param>
    /// <param name="punishingOperation">
    /// Opposing operation, or null when no such operation is known.
    /// </param>
    /// <returns>
    /// Inclusive allowed range and whether recovery is opposed, or null for invalid prerequisites.
    /// Minimum greater than maximum denotes an empty opposed interval, never a chosen outcome.
    /// </returns>
    internal static (long Minimum, long Maximum, bool Opposed)? ProjectSpiritualRecoveryAfter(
        int before, int cost, int maximum, string? outcome, string? punishingOperation)
    {
        if (cost < 0 || before < cost || maximum <= 0) return null;
        var paid = (long)before - cost;
        if (ConflictTokenEquals(punishingOperation, "pressure", "maneuver", "binding", "force_binding", "force_incarnation"))
            return (paid, Math.Min(maximum, paid + 1), true);
        var gain = ConflictTokenEquals(outcome, "success") ? 3 : ConflictTokenEquals(outcome, "partial_success") ? 2 : 0;
        var after = Math.Min(maximum, paid + gain);
        return (after, after, false);
    }

    internal sealed partial class SpiritualWoundSourceSession
    {
        /// <summary>
        /// Reuses retained profile authority for ordinary validation and diagnostic cost projection.
        /// </summary>
        /// <param name="mechanics">
        /// Actual current wound context, or null for an existing legacy source-validation path.
        /// </param>
        /// <returns>
        /// Cost authority assembled from retained pre-turn profiles and original actor snapshots.
        /// </returns>
        private AfterlifeActionCostAuthorityContext CreateSpiritualCostAuthority(
            AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext? mechanics) =>
            new(ReadAfterlifeCombatProfileArtTiers(_soul), ReadPlayerSpecialArts(_profileRoot),
                ReadSpecialArtsByOwner(_profileRoot), ReadEntityStandardArtTiers(_profileRoot),
                ReadConflictActorArtTierSnapshots(_originalConflict)) { WoundMechanics = mechanics };

        /// <summary>
        /// Derives a unique affordable ordinary cost correction from this source's actual current owners.
        /// </summary>
        /// <param name="capture">
        /// Genuine original capture; a foreign or revoked owner is rejected.
        /// </param>
        /// <param name="exchange">
        /// Detached unchanged action evidence at the diagnosed raw coordinate.
        /// </param>
        /// <param name="side">
        /// Exact player or opposition side.
        /// </param>
        /// <returns>
        /// Exact numeric audit leaves, or null for invalid evidence, ambiguity or missing prior funds.
        /// This data never substitutes for ordinary validation and actual execution of the correction.
        /// </returns>
        internal (int Cost, int Before, int After)? ProjectDependentOrdinaryCost(
            SpiritualOriginalTurnCapture capture, JsonObject exchange, string side)
        {
            if (!IsCurrentOwner || !ReferenceEquals(capture, _prefixCapture) || side is not ("player" or "opposition"))
                return null;
            var issues = new List<ValidationIssue>();
            var mechanics = capture.PrepareSourceMechanics(this, issues);
            var conflict = ReadMechanicsConflict(capture)["activeConflict"] as JsonObject;
            var operation = ResolveSpiritualCostOperation(exchange, side);
            if (mechanics is null || issues.Count != 0 || conflict is null || operation is null ||
                !AfterlifeActionCostRules.TryGetDefinition(operation, out var definition) ||
                exchange["actionCostAudit"]?[side] is not JsonObject audit) return null;
            var frontier = mechanics.ReadActionPointBefore(conflict["conflictId"]!.GetValue<string>(), mechanics.Ordinal);
            var resource = side == "player" ? frontier.Player : frontier.Opposition;
            var before = TryReadIntegralResourceValue(resource.Current);
            var maximum = TryReadIntegralResourceValue(resource.Maximum);
            if (before is null || maximum is null) return null;
            var authority = CreateSpiritualCostAuthority(mechanics);
            var special = side == "player" ? ResolvePlayerSpecialArtAudit(exchange, operation) :
                ResolveOppositionSpecialArtAudit(exchange, operation);
            var actor = side == "opposition" ? ResolveOppositionActorAuthorityKey(exchange, conflict) : null;
            ValidateSpecialArtCostBindingUniqueness(exchange, operation, side == "player",
                AfterlifeSpiritualConflictState.GetNodeString(audit["specialArtId"]), "dependent.actionCostAudit." + side, issues);
            if (side == "opposition") ValidateOppositionSpecialArtOwnerMatchesActor(special, actor, "dependent", issues);
            var tier = side == "player"
                ? ResolveAuthoritativeActionCostArtTier(operation, special, authority, "dependent", issues)
                : ResolveOppositionActionCostArtTier(exchange, conflict, operation, authority, special, actor);
            if (issues.Count != 0 || !ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(audit["operationType"]), operation) ||
                !TryGetJsonNodeInt(audit["baseCost"], out var baseCost) || baseCost != definition.BaseCost ||
                !TryGetJsonNodeInt(audit["minCost"], out var minCost) || minCost != definition.MinCost ||
                !TryGetJsonNodeInt(audit["artTier"], out var auditTier) || auditTier != tier) return null;
            var expected = ProjectSpiritualActionCost(definition, tier, special,
                ReadCurrentActionCostBurden(mechanics, conflict, exchange, side));
            if (expected.Effective < 0 || expected.Effective > int.MaxValue || before.Value < expected.Effective) return null;
            if (special is not null && TryGetJsonNodeInt(special["costMultiplierPercent"], out var multiplier) && multiplier > 100 &&
                !SpecialArtCostAuditMatches(audit, AfterlifeSpiritualConflictState.GetNodeString(special["artId"]),
                    multiplier, expected.Standard)) return null;
            var cost = (int)expected.Effective;
            if (!ConflictTokenEquals(operation, "recover_spiritual_power")) return (cost, before.Value, before.Value - cost);
            var range = ProjectSpiritualRecoveryAfter(before.Value, cost, maximum.Value,
                side == "player" ? AfterlifeSpiritualConflictState.GetNodeString(exchange["outcome"]) : "success",
                side == "player" ? ResolveMatchupOppositionOperation(exchange) :
                    AfterlifeSpiritualConflictState.GetNodeString(exchange["operationType"]));
            return range is { } allowed && allowed.Minimum == allowed.Maximum && allowed.Minimum is >= 0 and <= int.MaxValue
                ? (cost, before.Value, (int)allowed.Minimum) : null;
        }
    }
}
