using System.Collections.Frozen;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Frozen pre-reaction authority for the exact effect identity that an incoming
/// replace-policy definition would retire at its stack coordinate.
/// </summary>
internal sealed class EffectReplacementTargetIndex
{
    private readonly FrozenDictionary<EffectStackCoordinate, TargetBucket>
        _effectIdsByCoordinate;

    private EffectReplacementTargetIndex(
        Dictionary<EffectStackCoordinate, TargetBucket> effectIdsByCoordinate)
    {
        _effectIdsByCoordinate = effectIdsByCoordinate.ToFrozenDictionary();
    }

    internal static EffectReplacementTargetIndex Build(
        IEnumerable<EffectCarrierOccurrence> occurrences) =>
        Build(occurrences, out _);

    internal static EffectReplacementTargetIndex Build(
        IEnumerable<EffectCarrierOccurrence> occurrences,
        out int occurrenceVisitCount)
    {
        ArgumentNullException.ThrowIfNull(occurrences);
        occurrenceVisitCount = 0;
        var indexed = new Dictionary<EffectStackCoordinate, TargetBucket>();
        foreach (var occurrence in occurrences)
        {
            occurrenceVisitCount++;
            if (!TryReadExact(occurrence.Effect["realm"], out var realm) ||
                occurrence.Effect["target"] is not JsonObject target ||
                !TryReadExact(target["kind"], out var targetKind) ||
                !TryReadExact(target["targetId"], out var targetId) ||
                occurrence.Effect["source"] is not JsonObject source ||
                !TryReadExact(source["kind"], out var sourceKind) ||
                !TryReadExact(source["sourceId"], out var sourceId) ||
                occurrence.Effect["stacking"] is not JsonObject stacking ||
                !TryReadExact(stacking["stackKey"], out var stackKey))
            {
                continue;
            }

            var coordinate = new EffectStackCoordinate(
                realm,
                targetKind,
                targetId,
                sourceKind,
                sourceId,
                stackKey);
            if (!indexed.TryGetValue(coordinate, out var bucket))
            {
                indexed.Add(
                    coordinate,
                    CreateTargetBucket(occurrence));
                continue;
            }
            var duplicate = CreateTargetBucket(occurrence);
            if (!string.Equals(
                    bucket.EffectId,
                    duplicate.EffectId,
                    StringComparison.Ordinal) ||
                bucket.CreatedAtTurn != duplicate.CreatedAtTurn ||
                !string.Equals(
                    bucket.CreatedEventRef,
                    duplicate.CreatedEventRef,
                    StringComparison.Ordinal))
            {
                indexed[coordinate] = bucket with { Ambiguous = true };
            }
        }
        return new EffectReplacementTargetIndex(indexed);
    }

    internal EffectReplayIdentity? ResolveExactTarget(
        EffectTargetKey target,
        EffectSourceKey downstreamSource,
        string? downstreamStackKey,
        string? downstreamStackPolicy,
        int acceptedTurn)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(downstreamSource);
        if (!string.Equals(
                downstreamStackPolicy,
                "replace",
                StringComparison.Ordinal) ||
            !IsExact(downstreamStackKey) ||
            !string.Equals(
                target.Realm,
                downstreamSource.Realm,
                StringComparison.Ordinal))
        {
            return null;
        }

        var coordinate = new EffectStackCoordinate(
            target.Realm,
            target.Kind,
            target.TargetId,
            downstreamSource.Kind,
            downstreamSource.SourceId,
            downstreamStackKey!);
        if (!_effectIdsByCoordinate.TryGetValue(coordinate, out var bucket) ||
            bucket.Ambiguous)
        {
            return null;
        }

        var authority = bucket.CreatedAtTurn == acceptedTurn &&
                        bucket.CreatedEventRef != null
            ? new ResourcePendingAuthorityBinding(
                "accepted_application",
                bucket.CreatedEventRef)
            : new ResourcePendingAuthorityBinding(
                "permanent",
                bucket.EffectId);
        return new EffectReplayIdentity(bucket.EffectId, authority);
    }

    private readonly record struct TargetBucket(
        string EffectId,
        int? CreatedAtTurn,
        string? CreatedEventRef,
        bool Ambiguous);

    private static TargetBucket CreateTargetBucket(
        EffectCarrierOccurrence occurrence)
    {
        int? createdAtTurn = null;
        string? createdEventRef = null;
        if (occurrence.Effect["chronology"] is JsonObject chronology)
        {
            if (chronology["createdAtTurn"] is JsonValue createdAtTurnNode &&
                createdAtTurnNode.TryGetValue<int>(out var parsedTurn))
            {
                createdAtTurn = parsedTurn;
            }
            if (TryReadExact(
                    chronology["createdEventRef"],
                    out var parsedEventRef))
            {
                createdEventRef = parsedEventRef;
            }
        }
        return new TargetBucket(
            occurrence.EffectId,
            createdAtTurn,
            createdEventRef,
            Ambiguous: false);
    }

    private static bool TryReadExact(JsonNode? node, out string result)
    {
        result = string.Empty;
        return node is JsonValue value &&
               value.TryGetValue<string>(out var text) &&
               IsExact(text) &&
               (result = text).Length > 0;
    }

    private static bool IsExact(string? value) =>
        !string.IsNullOrEmpty(value) &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
