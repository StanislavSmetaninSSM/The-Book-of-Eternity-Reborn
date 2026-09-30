using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed class FateShieldReactionCandidate
{
    internal FateShieldReactionCandidate(
        string effectId,
        string triggerId,
        int createdAtTurn,
        string acceptedEffectFingerprint)
    {
        EffectId = effectId;
        TriggerId = triggerId;
        CreatedAtTurn = createdAtTurn;
        AcceptedEffectFingerprint = acceptedEffectFingerprint;
    }

    internal string EffectId { get; }
    internal string TriggerId { get; }
    internal int CreatedAtTurn { get; }
    internal string AcceptedEffectFingerprint { get; }
}

internal static class FateShieldReactionArbiter
{
    internal static IReadOnlyList<FateShieldReactionCandidate> ProjectEligibleCandidates(
        IEnumerable<EffectCarrierOccurrence> occurrences)
    {
        ArgumentNullException.ThrowIfNull(occurrences);
        return new ReadOnlyCollection<FateShieldReactionCandidate>(occurrences
            .Select(ProjectEligibleCandidate)
            .Where(static candidate => candidate is not null)
            .Select(static candidate => candidate!)
            .OrderBy(static candidate => candidate.CreatedAtTurn)
            .ThenBy(static candidate => candidate.EffectId, StringComparer.Ordinal)
            .ToArray());
    }

    internal static FateShieldReactionCandidate? SelectOldest(
        IReadOnlyList<FateShieldReactionCandidate> candidates,
        IReadOnlySet<string> excludedEffectIds)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(excludedEffectIds);
        return candidates.FirstOrDefault(candidate =>
            !excludedEffectIds.Contains(candidate.EffectId));
    }

    private static FateShieldReactionCandidate? ProjectEligibleCandidate(
        EffectCarrierOccurrence occurrence)
    {
        var effect = occurrence.Effect;
        if (!HasExact(effect, "state", "active") ||
            !HasExact(effect, "realm", "mortal_world") ||
            effect["target"] is not JsonObject target ||
            !HasExact(target, "kind", "player") ||
            !HasExact(target, "targetId", "player_current") ||
            effect["source"] is not JsonObject source ||
            !HasExact(source, "kind", EffectBuiltInSourceCatalog.FateShieldSourceKind) ||
            !HasExact(source, "sourceId", EffectBuiltInSourceCatalog.FateShieldSourceId) ||
            !HasExact(
                source,
                "definitionKey",
                EffectBuiltInSourceCatalog.FateShieldDefinitionKey) ||
            effect["lifetime"] is not JsonObject lifetime ||
            !HasExact(lifetime, "mode", "uses") ||
            !TryReadInt(lifetime["remainingUses"], out var remainingUses) ||
            remainingUses <= 0 ||
            effect["triggers"] is not JsonArray triggers ||
            effect["components"] is not JsonArray components)
        {
            return null;
        }

        var matchingTriggers = triggers.OfType<JsonObject>()
            .Where(trigger =>
                HasExact(trigger, "eventType", "owner_critical_failure") &&
                HasExact(trigger, "resolutionMode", "deterministic") &&
                trigger["consumeUses"] is JsonValue consumeNode &&
                consumeNode.TryGetValue<bool>(out var consume) && consume)
            .ToArray();
        if (matchingTriggers.Length != 1 ||
            !TryExact(matchingTriggers[0]["triggerId"], out var triggerId) ||
            matchingTriggers[0]["componentIds"] is not JsonArray componentIds ||
            componentIds.Count != 1 ||
            !TryExact(componentIds[0], out var componentId))
        {
            return null;
        }

        var matchingComponents = components.OfType<JsonObject>()
            .Where(component => HasExact(component, "componentId", componentId))
            .ToArray();
        if (matchingComponents.Length != 1 ||
            !HasExact(matchingComponents[0], "profile", "event_reaction") ||
            matchingComponents[0]["payload"] is not JsonObject payload ||
            !HasExact(payload, "eventType", "owner_critical_failure") ||
            !HasExact(payload, "resultKind", "event_outcome") ||
            !HasExact(payload, "originalOutcome", "critical_failure") ||
            !HasExact(payload, "resolvedOutcome", "failure") ||
            !HasExact(payload, "dependency", "before_current_event"))
        {
            return null;
        }

        var createdAtTurn = effect["chronology"] is JsonObject chronology &&
                            TryReadInt(chronology["createdAtTurn"], out var turn)
            ? turn
            : int.MaxValue;
        return new FateShieldReactionCandidate(
            occurrence.EffectId,
            triggerId,
            createdAtTurn,
            WoundEffectTerminalOperationPlanner.ComputeEffectFingerprint(occurrence));
    }

    private static bool HasExact(
        JsonObject value,
        string field,
        string expected) =>
        TryExact(value[field], out var actual) &&
        string.Equals(actual, expected, StringComparison.Ordinal);

    private static bool TryExact(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue scalar ||
            !scalar.TryGetValue<string>(out var parsed) ||
            parsed == null)
        {
            return false;
        }

        value = parsed;
        return value.Length != 0 &&
               string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    private static bool TryReadInt(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue scalar && scalar.TryGetValue<int>(out value);
    }
}
