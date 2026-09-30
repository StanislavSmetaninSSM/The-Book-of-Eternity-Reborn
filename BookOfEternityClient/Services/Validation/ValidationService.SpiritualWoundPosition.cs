using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Combines both operation burdens before clamping the effective starting rank.
    /// </summary>
    /// <param name="canonicalRank">
    /// Canonical starting rank from minus two through two.
    /// </param>
    /// <param name="playerBurden">
    /// Nonnegative sum of applicable player components.
    /// </param>
    /// <param name="oppositionBurden">
    /// Nonnegative sum of applicable opposition components.
    /// </param>
    /// <returns>
    /// The bounded effective rank, without modifying any canonical snapshot.
    /// </returns>
    internal static int ComputeSpiritualEffectivePosition(int canonicalRank, long playerBurden, long oppositionBurden)
    {
        if (canonicalRank is < -2 or > 2 || playerBurden < 0 || oppositionBurden < 0)
            throw new ArgumentOutOfRangeException(nameof(canonicalRank), "A bounded rank and nonnegative burdens are required.");
        return (int)Math.Clamp((decimal)canonicalRank + oppositionBurden - playerBurden, -2m, 2m);
    }

    /// <summary>
    /// Selects current causal ownership or the intrinsic retained-data route without granting source authority.
    /// </summary>
    /// <param name="authority">
    /// Original or completed comparison context for this validation pass.
    /// </param>
    /// <param name="conflict">
    /// Conflict containing the exact exchange membership.
    /// </param>
    /// <param name="exchange">
    /// Exchange being validated.
    /// </param>
    /// <param name="isCurrentExchange">
    /// Whether signed-baseline comparison identified a new exchange.
    /// </param>
    /// <param name="context">
    /// Exchange diagnostic path.
    /// </param>
    /// <param name="issues">
    /// Receives malformed position evidence errors.
    /// </param>
    /// <returns>
    /// Effective starting rank, or <see langword="null"/> when no usable position evidence exists.
    /// Stale current ownership or completed-packet drift throws rather than falling back.
    /// </returns>
    private static int? ResolveSpiritualValidationPosition(AfterlifeActionCostAuthorityContext authority,
        JsonObject conflict, JsonObject exchange, bool isCurrentExchange, string context, List<ValidationIssue> issues)
    {
        if (!isCurrentExchange)
            return ReadIntrinsicSpiritualPosition(exchange, context, issues);
        var rank = authority.CompletedConflictValidation is { } completed
            ? completed.Read(conflict, exchange).EffectivePositionRank
            : SpiritualWoundSourceSession.ReadCurrentEffectivePosition(authority.WoundMechanics, conflict, exchange);
        if (!rank.HasValue && exchange["diceAudit"] is JsonObject)
            AddDiceAuditIssue(issues, context + ".before.conflictPosition",
                "A current dice audit requires a supported starting position snapshot.",
                "afterlife_conflict_exchange_missing_before_position", "supported conflictPosition snapshot value",
                exchange["before"]?["conflictPosition"]?.ToJsonString() ?? "missing");
        return rank;
    }

    /// <summary>
    /// Checks self-contained position evidence without rebinding an old exchange to present wounds or canonical position.
    /// </summary>
    /// <param name="exchange">
    /// Retained exchange; absence of dice leaves the effective position unknown.
    /// </param>
    /// <param name="context">
    /// Exchange diagnostic path.
    /// </param>
    /// <param name="issues">
    /// Receives invalid token, multiplicity, side or magnitude errors.
    /// </param>
    /// <returns>
    /// Declared intrinsic rank, zero for a dice audit without position rows, or <see langword="null"/> for absent or invalid evidence.
    /// This result cannot authorize an exchange or reconstruct its wound owner.
    /// </returns>
    private static int? ReadIntrinsicSpiritualPosition(JsonObject exchange, string context, List<ValidationIssue> issues)
    {
        if (exchange["diceAudit"] is not JsonObject dice)
            return null;
        if (!TryGetPositionRank(exchange["before"]?["conflictPosition"], out _))
        {
            AddDiceAuditIssue(issues, context + ".before.conflictPosition",
                "A retained dice audit still requires a supported canonical starting position snapshot.",
                "afterlife_conflict_exchange_missing_before_position", "supported conflictPosition snapshot value",
                exchange["before"]?["conflictPosition"]?.ToJsonString() ?? "missing");
            return null;
        }
        var rows = CollectConflictPositionModifiers(dice);
        if (rows.Count == 0)
            return 0;
        if (rows.Count != 1 || !TryGetPositionRank(JsonValue.Create(rows[0].Position), out var rank) || rank == 0)
        {
            AddDiceAuditIssue(issues, context + ".diceAudit.modifierBreakdown",
                "Retained position evidence requires one non-contested position row.",
                "afterlife_conflict_dice_invalid_position_modifier_total",
                "one supported non-contested position modifier", DescribeConflictPositionModifiers(rows));
            return null;
        }
        ValidateConflictPositionDiceModifier(dice, rank, context, issues);
        return rank;
    }

    /// <summary>
    /// Maps a bounded effective rank to the existing canonical position vocabulary.
    /// </summary>
    /// <param name="rank">
    /// Rank from minus two through two.
    /// </param>
    /// <returns>
    /// The corresponding position token; an unsupported rank throws.
    /// </returns>
    private static string SpiritualPositionToken(int rank) => rank switch
    {
        -2 => "opposition_dominant",
        -1 => "opposition_advantaged",
        0 => "contested",
        1 => "player_advantaged",
        2 => "player_dominant",
        _ => throw new ArgumentOutOfRangeException(nameof(rank))
    };

    internal sealed partial class SpiritualWoundSourceSession
    {
        /// <summary>
        /// Reads position burdens only from the exact current resource epoch and acting participant for each operation.
        /// </summary>
        /// <param name="mechanics">
        /// Current private mechanics owner, or <see langword="null"/> for the unchanged zero-burden legacy route.
        /// </param>
        /// <param name="conflict">
        /// Signed conflict identity and participant roster.
        /// </param>
        /// <param name="exchange">
        /// Current exchange containing its canonical before snapshot and exact acting operations.
        /// </param>
        /// <returns>
        /// Effective rank, or <see langword="null"/> for a missing position snapshot.
        /// Stale ownership, ambiguous matching or conflicting duplicate components throw.
        /// </returns>
        internal static int? ReadCurrentEffectivePosition(
            AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext? mechanics,
            JsonObject conflict, JsonObject exchange)
        {
            if (mechanics is not null)
            {
                if (!mechanics.IsCurrent)
                    throw new InvalidOperationException("Current owned wound mechanics are required for position.");
                _ = mechanics.ReadActionPointBefore(ExactString(conflict["conflictId"]) ?? string.Empty, mechanics.Ordinal);
            }
            if (!TryGetPositionRank(exchange["before"]?["conflictPosition"], out var canonicalRank))
                return null;
            if (mechanics is null)
                return canonicalRank;
            var realm = AfterlifeSpiritualConflictState.NormalizeAfterlifeRealmKey(ExactString(conflict["realm"]))
                ?? throw new InvalidOperationException("The exact conflict realm is required for position.");
            long player = 0, opposition = 0;
            var seen = new Dictionary<(string Effect, string Component), SpiritualWoundConflictContribution>();
            foreach (var contribution in mechanics.Contributions.Where(row => row.Profile == "spiritual_position_burden"))
            {
                var key = (contribution.EffectId, contribution.ComponentId);
                if (seen.TryGetValue(key, out var prior))
                {
                    if (prior.Actor != contribution.Actor || prior.ResolvedSide != contribution.ResolvedSide ||
                        prior.Operation != contribution.Operation || prior.Magnitude.GetRawText() != contribution.Magnitude.GetRawText())
                        throw new InvalidOperationException("A position component has ambiguous ownership or magnitude.");
                    continue;
                }
                seen.Add(key, contribution);
                var side = contribution.ResolvedSide;
                var magnitude = contribution.Magnitude.GetInt32();
                if (side is not ("player" or "opposition") || contribution.Actor.Realm != realm ||
                    magnitude is < 1 or > 2)
                    throw new InvalidOperationException("An exact actor-bound position burden of one or two steps is required.");
                if (!ConflictTokenEquals(ResolveWoundOperation(exchange, side), contribution.Operation))
                    continue;
                var actor = side == "player" ? ActorKey(conflict["playerSide"]?["leadContestant"] as JsonObject) :
                    ResolveActingOpposition(exchange, conflict);
                var parts = actor?.Split(':', 2);
                if (parts is not { Length: 2 })
                    throw new InvalidOperationException("The acting participant is ambiguous for position.");
                var kind = parts[0] switch
                {
                    "player_soul" or "player" or "soul" => "player",
                    "resident" or "shining_resident" => "resident",
                    "afterlife_actor" or "shining_faction_head" or "saref_agent" or "system_actor" or "custom_afterlife_actor" => "afterlife_actor",
                    _ => parts[0]
                };
                if (contribution.Actor.Kind != kind || contribution.Actor.TargetId != parts[1])
                    continue;
                if (side == "player") player = checked(player + magnitude);
                else opposition = checked(opposition + magnitude);
            }
            return ComputeSpiritualEffectivePosition(canonicalRank, player, opposition);
        }
    }
}
