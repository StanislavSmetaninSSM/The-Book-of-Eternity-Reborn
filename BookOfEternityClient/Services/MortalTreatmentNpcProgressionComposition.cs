using System.Text;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>References an existing sealed treatment projection; it grants no publication or reservation authority.</summary>
internal sealed class MortalTreatmentNpcProgressionComposition
{
    private readonly object _continuation;
    private readonly object _reservation;
    internal string Fingerprint { get; }

    private MortalTreatmentNpcProgressionComposition(object continuation, object reservation, string fingerprint)
    {
        _continuation = continuation;
        _reservation = reservation;
        Fingerprint = fingerprint;
    }

    internal static MortalTreatmentNpcProgressionComposition? FromExistingProjection(object continuation, object reservation)
    {
        if (!WoundAcceptedTurnPlanner.TryReadTreatmentNpcCompositionFingerprint(continuation, reservation, out var fingerprint))
            return null;
        return new(continuation, reservation, fingerprint);
    }

    internal JsonNode? ProjectCore(JsonNode? current, JsonNode? previous,
        NpcCoreChangesContract.Authority authority, CanonicalBeforeImage trade, CanonicalBeforeImage training)
    {
        if (!WoundAcceptedTurnPlanner.TryReadTreatmentNpcCompositionFingerprint(_continuation, _reservation, out var fingerprint) ||
            !string.Equals(Fingerprint, fingerprint, StringComparison.Ordinal) ||
            !WoundAcceptedTurnPlanner.TryReadMortalProcedurePublication(_continuation, out var continuation))
            throw new InvalidDataException("Treatment NPC progression lost its exact sealed skill projection.");
        var coordinate = continuation.Resolution.Coordinates;
        var source = authority.ProgressionSource;
        if (source is null || source.SessionId != coordinate.SessionId || source.RequestId != coordinate.RequestId ||
            source.SnapshotToken != coordinate.SnapshotToken || source.TurnNumber != coordinate.Turn || source.Realm != coordinate.Realm)
            throw new InvalidDataException("Treatment NPC progression differs from its existing continuation source.");
        if (current is not JsonObject raw || previous is not JsonObject original)
            throw new InvalidDataException("Treatment NPC progression requires its exact current and original core roots.");
        var candidate = raw.DeepClone().AsObject();
        var accepted = MortalActorAcceptedTurnAuthority.Create(candidate, Decode(trade), Decode(training));
        var evaluation = NpcCoreChangesContract.EvaluateForSkillComposition(candidate, original, authority,
            ValidationService.ValidateNpcCoreFateCardsAgainstProductionContract, detectDirectMutations: true, accepted);
        if (!evaluation.CanApply)
            throw new InvalidDataException("Treatment NPC core preflight refused: " + string.Join("; ", evaluation.Issues.Select(issue => issue.Code)));
        NpcCoreChangesContract.Apply(candidate, evaluation);
        var issues = WoundAcceptedTurnPlanner.ComposeTreatmentSkillProjectionOnFinalItemBaseline(
            _continuation, _reservation, candidate, out var roots, out _, out var recomposedFingerprint);
        if (issues.Count != 0 || recomposedFingerprint != Fingerprint ||
            !roots.TryGetValue(NpcCoreChangesContract.NpcCorePath, out var complete))
            throw new InvalidDataException("Treatment NPC final skill composition refused.");
        var consequences = NpcCoreChangesContract.ValidateFinalConsequences(original, complete, authority, evaluation);
        if (consequences.Count != 0)
            throw new InvalidDataException("Treatment NPC final consequences refused: " + string.Join("; ", consequences.Select(issue => issue.Code)));
        // Return only the core intermediate. Existing treatment projection owns applying skills later.
        return candidate;
    }

    private static string? Decode(CanonicalBeforeImage image) => image.Existed && image.Bytes is { } bytes
        ? new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF') : null;
}
