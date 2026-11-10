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
        var rows = active is null ? new List<SpiritualWoundStage>()
            : BuildActiveOnly(before, attemptId!, cycleKey, active).ToList();
        var rank = rows.Count == 0 ? before.Severity.Rank : rows[^1].SeverityRank;
        var progress = rows.Count == 0 ? before.Recovery.CurrentStepProgress : rows[^1].Progress;
        if (rows.LastOrDefault()?.Kind == "heal") return rows;
        void Add(string kind, int nextRank, long nextProgress, bool preparesHeal, string? attempt)
        {
            rows.Add(Stage(before.WoundId, cycleKey, rows.Count, kind, nextRank, nextProgress, preparesHeal, attempt));
            rank = nextRank;
            progress = nextProgress;
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

    // Shared active prefix. Combat consumes this program only; the safe caller
    // above explicitly appends its target-tier natural recovery afterwards.
    internal static IReadOnlyList<SpiritualWoundStage> BuildActiveOnly(
        WoundMaterializationEnvelope before, string attemptId, string operationContext,
        SpiritualHealingCalculation active)
    {
        if (string.IsNullOrWhiteSpace(attemptId) || string.IsNullOrWhiteSpace(operationContext) ||
            !active.HasSufficientTier || active.Input.WoundSeverityRank != before.Severity.Rank ||
            SpiritualHealingOutcomeMath.Calculate(active.Input) != active)
            throw new InvalidDataException("Active healing differs from its admitted wound and art.");
        var progress = checked(before.Recovery.CurrentStepProgress + active.RecoveryPointsAdded);
        var rows = new List<SpiritualWoundStage>
        {
            Stage(before.WoundId, operationContext, 0, "treat", active.ResultingSeverityRank,
                progress, active.HealsWound, attemptId)
        };
        if (active.HealsWound)
            rows.Add(Stage(before.WoundId, operationContext, 1, "heal", 1, progress, false, null));
        return rows;
    }

    private static SpiritualWoundStage Stage(string woundId, string context, int index,
        string kind, int rank, long progress, bool preparesHeal, string? attemptId) =>
        new(woundId, kind, rank, progress, preparesHeal,
            Coordinate("spiritual_wound_operation", context, woundId, index),
            Coordinate("spiritual_wound_transition", context, woundId, index),
            Coordinate("spiritual_wound_tick", context, woundId, index), attemptId);

    internal static string Coordinate(string kind, string cycle, string wound, int index) =>
        kind + "_" + WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "book_of_eternity.spiritual_wound.stage", "1", kind, cycle, wound,
            index.ToString(CultureInfo.InvariantCulture)
        })[7..];
}
