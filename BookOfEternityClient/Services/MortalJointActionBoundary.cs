namespace BookOfEternityClient.Services;

// Detached calculation inputs/results only. Neither a hash nor these values prove
// physical original/G, a consumed phase, accepted source, or publication authority.
internal sealed record MortalJointActionSource(string Generation, string SessionId, string RequestId,
    string SnapshotToken, int Turn, string Realm, int Incarnation);
internal sealed record MortalJointActionCoordinates(MortalJointActionSource Source, string ActionId);
internal sealed record MortalJointRequestCoordinates(MortalJointActionSource Source, string ActionId,
    string RequestId, string SnapshotToken, int GameTurn, int SegmentOrdinal);
internal enum MortalJointClosureState { Pending, Consumed }
internal enum MortalJointTimeCommand { None, Incremental, Absolute }
internal enum MortalJointTimeKind { Unchanged, Incremental, NumericAbsolute, UnresolvedAbsolute }

internal sealed class MortalJointTimeProjection
{
    internal MortalJointTimeKind Kind => throw new NotImplementedException();
    internal WorldTimeResolutionResult Progression => throw new NotImplementedException();
    internal int? ElapsedMinutes => throw new NotImplementedException();
    internal long? CanonicalBeforeMinutes => throw new NotImplementedException();
    internal long? EffectCurrentTime => throw new NotImplementedException();
    internal byte[]? BeforeBytes => throw new NotImplementedException();
    internal byte[]? AfterBytes => throw new NotImplementedException();
}

// T192a calculation seam; live issuers/M/common adapters remain gated in T179/T192.
internal static class MortalJointActionBoundary
{
    internal static MortalJointActionCoordinates DescribeAction(MortalJointActionSource source) =>
        throw new NotImplementedException("T192a runtime RED: action calculation is missing.");
    internal static MortalJointRequestCoordinates DescribeContinuation(MortalJointActionCoordinates action,
        string requestId, string snapshotToken) =>
        throw new NotImplementedException("T192a runtime RED: continuation calculation is missing.");
    internal static IReadOnlyList<EffectLifecycleEvent> CalculateLifecycleEvents(MortalJointRequestCoordinates request,
        MortalJointClosureState state, IReadOnlyList<EffectLifecycleEvent> proposed) =>
        throw new NotImplementedException("T192a runtime RED: event admission calculation is missing.");
    internal static MortalJointTimeProjection DescribeTime(byte[]? before, byte[]? after,
        MortalJointTimeCommand command, WorldTimeResolutionResult postPrefix) =>
        throw new NotImplementedException("T192a runtime RED: time calculation is missing.");
}
