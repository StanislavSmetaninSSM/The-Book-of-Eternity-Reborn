using System.Globalization;

namespace BookOfEternityClient.Services;

// Signed start-of-cycle DATA shared by projection, preparation and strict replay.
internal sealed record SpiritualWoundRecoveryMember(WoundMaterializationEnvelope Wound, int HealingTier);

// A bounded scalar program, never a grant to mutate a wound or advance a cycle.
internal sealed record SpiritualWoundStage(
    string WoundId, string Kind, int SeverityRank, long Progress, bool PreparesHeal,
    string OperationKey, string TransitionId, string TickKey, string? AttemptId);

internal static class SpiritualWoundRecoveryPlanner
{
    internal static IReadOnlyList<SpiritualWoundStage> Build(
        WoundMaterializationEnvelope before, int tier, string? attemptId, string cycleKey,
        SpiritualHealingCalculation? active)
    {
        if (tier is < 0 or > 5)
            throw new InvalidDataException("Natural recovery requires the target's bounded admitted art tier.");
        var rows = new List<SpiritualWoundStage>();
        var rank = before.Severity.Rank;
        var progress = before.Recovery.CurrentStepProgress;
        void Add(string kind, int nextRank, long nextProgress, bool preparesHeal, string? attempt)
        {
            var index = rows.Count;
            var key = Coordinate("spiritual_wound_operation", cycleKey, before.WoundId, index);
            rows.Add(new(before.WoundId, kind, nextRank, nextProgress, preparesHeal,
                key, Coordinate("spiritual_wound_transition", cycleKey, before.WoundId, index),
                Coordinate("spiritual_wound_tick", cycleKey, before.WoundId, index), attempt));
            rank = nextRank;
            progress = nextProgress;
        }

        if (active is not null)
        {
            if (string.IsNullOrWhiteSpace(attemptId) || !active.HasSufficientTier ||
                active.Input.WoundSeverityRank != rank || SpiritualHealingOutcomeMath.Calculate(active.Input) != active)
                throw new InvalidDataException("Active healing differs from its admitted wound and art.");
            Add("treat", active.ResultingSeverityRank, checked(progress + active.RecoveryPointsAdded),
                active.HealsWound, attemptId);
            if (active.HealsWound)
            {
                Add("heal", 1, progress, false, null);
                return rows;
            }
        }

        var calculation = SpiritualWoundRecoveryMath.Calculate(new(tier, rank, progress))
            ?? throw new InvalidDataException("Spiritual recovery is outside its bounded arithmetic domain.");
        var remaining = calculation.AvailablePoints;
        while (remaining >= 2L * rank)
        {
            remaining -= 2L * rank;
            if (rank == 1)
            {
                Add("recover", 1, 0, true, null);
                rows[^1] = rows[^1] with { TickKey = cycleKey };
                Add("heal", 1, 0, false, null);
                rows[^1] = rows[^1] with { TickKey = cycleKey };
                return rows;
            }
            Add("recover", rank - 1, remaining, false, null);
        }
        if (rows.Count == 0 || rows[^1].Kind != "recover")
            Add("recover", rank, remaining, false, null);
        rows[^1] = rows[^1] with { TickKey = cycleKey };
        if (rank != calculation.ResultingSeverityRank || progress != calculation.RemainingProgress || calculation.HealsWound)
            throw new InvalidDataException("The ordered spiritual recovery program lost carried progress.");
        return rows;
    }

    internal static string Coordinate(string kind, string cycle, string wound, int index) =>
        kind + "_" + WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "book_of_eternity.spiritual_wound.stage", "1", kind, cycle, wound,
            index.ToString(CultureInfo.InvariantCulture)
        })[7..];
}
