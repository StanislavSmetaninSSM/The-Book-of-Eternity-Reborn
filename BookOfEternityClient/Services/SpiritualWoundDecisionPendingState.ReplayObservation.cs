using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed partial class SpiritualWoundDecisionPendingState
{
    /// <summary>Compares the already validated retained packet cursor with an exact owner-retained offer source.</summary>
    /// <param name="binding">The capture's retained accepted event binding, not a newly composed witness.</param>
    /// <param name="closedExchangeCount">The actual closed resource/source frontier.</param>
    /// <param name="sourceCount">The complete retained source prefix length.</param>
    /// <param name="sourceOrdinal">The selected source's position in that prefix.</param>
    /// <param name="exchangeOrdinal">The selected exact closed interval's ordinal.</param>
    /// <param name="source">The actual retained source whose immutable facts are observed.</param>
    /// <returns>True only for the same retained origin, cursor and source facts; no parsing or authority is performed.</returns>
    internal bool MatchesOfferReplaySource(WoundAcceptedTurnBinding binding, int closedExchangeCount,
        int sourceCount, int sourceOrdinal, int exchangeOrdinal, ValidationService.PreparedSpiritualSource source)
    {
        if (_root["pending"] is not JsonObject packet || packet["cursor"] is not JsonObject cursor ||
            packet["sources"] is not JsonArray sources || sources.Count != sourceCount ||
            sourceOrdinal < 0 || sourceOrdinal >= sources.Count || sources[sourceOrdinal] is not JsonObject row ||
            packet["sessionId"]?.GetValue<string>() != binding.SessionId ||
            packet["requestId"]?.GetValue<string>() != binding.RequestId ||
            packet["snapshotToken"]?.GetValue<string>() != binding.SnapshotToken ||
            packet["realm"]?.GetValue<string>() != binding.Realm || packet["turn"]?.GetValue<int>() != binding.Turn ||
            cursor["exchangeOrdinal"]?.GetValue<int>() != closedExchangeCount ||
            cursor["nextSourceOrdinal"]?.GetValue<int>() != sourceOrdinal ||
            row["turnEvidence"] is not JsonObject evidence)
            return false;
        var calculation = source.Calculation;
        return row["sourceOrdinal"]?.GetValue<int>() == sourceOrdinal &&
            row["exchangeOrdinal"]?.GetValue<int>() == exchangeOrdinal &&
            row["conflictId"]?.GetValue<string>() == source.ConflictId &&
            row["exchangeId"]?.GetValue<string>() == source.ExchangeId &&
            row["coordinate"]?.GetValue<string>() == source.Coordinate &&
            row["affectedSide"]?.GetValue<string>() == source.AffectedSide &&
            MatchesActor(row["actingActor"], source.ActingActor) &&
            MatchesActor(row["affectedActor"], source.AffectedActor) &&
            row["operation"]?.GetValue<string>() == source.Operation &&
            row["appliedArtKind"]?.GetValue<string>() == (source.OriginalSpecialArtJson == null ? "standard" : "special") &&
            row["appliedArtTier"]?.GetValue<int>() == calculation.Input.AppliedArtTier &&
            row["targetResilienceTier"]?.GetValue<int>() == calculation.Input.TargetResilienceTier &&
            row["priorStrainRank"]?.GetValue<int>() == calculation.PreviousStrainRank &&
            row["destinationStrainRank"]?.GetValue<int>() == calculation.NewStrainRank &&
            row["extraStrainJumps"]?.GetValue<int>() == calculation.ExtraJumpSteps &&
            row["harmfulMargin"]?.GetValue<long>() == calculation.Input.HarmfulMargin &&
            row["maximumSeverityRank"]?.GetValue<int>() == calculation.MaximumSeverityRank &&
            row["guaranteedSeverityRank"]?.GetValue<int>() == source.GuaranteedSeverityRank &&
            row["dangerMode"]?.GetValue<string>() == calculation.Input.DangerMode &&
            row["retraumaWoundId"]?.GetValue<string>() == source.RetraumaWoundId &&
            evidence["sessionId"]?.GetValue<string>() == binding.SessionId &&
            evidence["requestId"]?.GetValue<string>() == binding.RequestId &&
            evidence["snapshotToken"]?.GetValue<string>() == binding.SnapshotToken &&
            evidence["realm"]?.GetValue<string>() == binding.Realm && evidence["turn"]?.GetValue<int>() == binding.Turn;
    }

    /// <summary>Compares retained actor coordinates without parsing or reconstructing source JSON.</summary>
    private static bool MatchesActor(JsonNode? node, string coordinate)
    {
        var separator = coordinate.IndexOf(':');
        return separator > 0 && node is JsonObject actor &&
            actor["actorKind"]?.GetValue<string>() == coordinate[..separator] &&
            actor["actorId"]?.GetValue<string>() == coordinate[(separator + 1)..];
    }
}
