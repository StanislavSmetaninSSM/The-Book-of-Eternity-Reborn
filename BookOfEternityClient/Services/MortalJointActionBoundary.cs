using System.Globalization;
using System.Text.Json;

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
    private readonly byte[]? _before, _after;
    internal MortalJointTimeProjection(MortalJointTimeKind kind, WorldTimeResolutionResult progression,
        int? elapsedMinutes, long? canonicalBeforeMinutes, long? effectCurrentTime, byte[]? before, byte[]? after)
    {
        Kind = kind; Progression = progression; ElapsedMinutes = elapsedMinutes;
        CanonicalBeforeMinutes = canonicalBeforeMinutes; EffectCurrentTime = effectCurrentTime;
        _before = before?.ToArray(); _after = after?.ToArray();
    }
    internal MortalJointTimeKind Kind { get; }
    internal WorldTimeResolutionResult Progression { get; }
    internal int? ElapsedMinutes { get; }
    internal long? CanonicalBeforeMinutes { get; }
    internal long? EffectCurrentTime { get; }
    internal byte[]? BeforeBytes => _before?.ToArray();
    internal byte[]? AfterBytes => _after?.ToArray();
}

// Pure T192a calculation seam; live issuers/M/common adapters remain gated in
// T179/T192. No production publication caller accepts any value from this class.
internal static class MortalJointActionBoundary
{
    internal static MortalJointActionCoordinates DescribeAction(MortalJointActionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (var text in new[] { source.Generation, source.SessionId, source.RequestId, source.SnapshotToken, source.Realm })
            RequireExact(text);
        if (source.Turn <= 0 || source.Incarnation < 0)
            throw Invalid("original coordinates");
        var actionId = WoundAcceptedTurnFingerprintWriter.Compute(new[] {
            "book_of_eternity.mortal_joint_action_coordinates.v1", source.Generation, source.SessionId,
            source.RequestId, source.SnapshotToken, source.Turn.ToString(CultureInfo.InvariantCulture),
            source.Realm, source.Incarnation.ToString(CultureInfo.InvariantCulture) });
        return new(source, actionId);
    }

    internal static MortalJointRequestCoordinates DescribeContinuation(MortalJointActionCoordinates action,
        string requestId, string snapshotToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        RequireExact(requestId); RequireExact(snapshotToken);
        if (DescribeAction(action.Source).ActionId != action.ActionId ||
            action.Source.RequestId == requestId || action.Source.SnapshotToken == snapshotToken)
            throw Invalid("continuation coordinates");
        return new(action.Source, action.ActionId, requestId, snapshotToken, action.Source.Turn, 1);
    }

    // Inputs are source-event values for the later genuine compositor, not raw
    // GM lifecycle commands. Consumed closures cannot silently eat independent
    // elapsed/source/scene payload; the owning compositor must separate it first.
    internal static IReadOnlyList<EffectLifecycleEvent> CalculateLifecycleEvents(MortalJointRequestCoordinates request,
        MortalJointClosureState state, IReadOnlyList<EffectLifecycleEvent> proposed)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(proposed);
        var expected = DescribeContinuation(DescribeAction(request.Source), request.RequestId, request.SnapshotToken);
        if (request != expected || state is not (MortalJointClosureState.Pending or MortalJointClosureState.Consumed))
            throw Invalid("request action boundary");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var admitted = new List<EffectLifecycleEvent>();
        foreach (var candidate in proposed)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            RequireExact(candidate.EventRef);
            if (candidate.Turn != request.GameTurn || !seen.Add(candidate.EventRef))
                throw Invalid("foreign or duplicate event");
            if (state == MortalJointClosureState.Consumed && candidate.Phase == "owner_turn_end")
            {
                if (candidate.CurrentTime.HasValue || candidate.TriggerId is not null || candidate.CurrentSceneId is not null ||
                    candidate.SceneClosed || candidate.SourceSatisfied.HasValue || candidate.ConditionSatisfied.HasValue ||
                    candidate.TargetSatisfied.HasValue || candidate.CurrentRealm is not null || candidate.CausalEventRef is not null)
                    throw Invalid("closure contains independent source payload; requires owning event decomposition");
                continue;
            }
            admitted.Add(candidate);
        }
        return admitted.AsReadOnly();
    }

    // The command discriminator must come from the later genuine accepted-source
    // owner. This value does not validate a GM reply or authorize a time writer.
    internal static MortalJointTimeProjection DescribeTime(byte[]? before, byte[]? after,
        MortalJointTimeCommand command, WorldTimeResolutionResult postPrefix)
    {
        if (command is not (MortalJointTimeCommand.None or MortalJointTimeCommand.Incremental or MortalJointTimeCommand.Absolute))
            throw Invalid("time command kind");
        var beforeMinutes = ReadEffectTime(before);
        var effectTime = ReadEffectTime(after);
        if (command == MortalJointTimeCommand.None)
            return new(MortalJointTimeKind.Unchanged, postPrefix, null, beforeMinutes, effectTime, before, after);

        var progression = ProgressionScheduleService.ResolveFrozenWorldTime(after, postPrefix.Minutes);
        if (command == MortalJointTimeCommand.Absolute)
            return new(progression.HasUnresolvedAbsoluteOverride ? MortalJointTimeKind.UnresolvedAbsolute : MortalJointTimeKind.NumericAbsolute,
                progression, null, beforeMinutes, effectTime, before, after);

        if (after is null) throw Invalid("missing incremental source");
        using var document = JsonDocument.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(after));
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("timeChange", out var delta) || !delta.TryGetInt32(out var minutes))
            throw Invalid("missing incremental command");
        return new(MortalJointTimeKind.Incremental, progression, minutes, beforeMinutes, effectTime, before, after);
    }

    private static long? ReadEffectTime(byte[]? bytes) => EffectAcceptedTurnInputComposer.ReadCanonicalWorldTime(
        bytes is null ? null : CanonicalJsonUtf8.DecodeOneOptionalBom(bytes));
    private static void RequireExact(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text != text.Trim() || text.Any(char.IsControl))
            throw Invalid("inexact coordinate");
    }
    private static InvalidDataException Invalid(string reason) => new("joint_action_calculation: " + reason);
}
