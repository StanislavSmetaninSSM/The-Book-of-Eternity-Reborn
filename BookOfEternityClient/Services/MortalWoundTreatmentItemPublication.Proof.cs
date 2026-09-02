using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundTreatmentItemPublicationAuthority
{
    internal IReadOnlyList<ValidationIssue> ValidateCandidate(
        AcceptedMechanicsPlan candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (!HasValidSeal())
            return new[] { Issue("authority", "sealed genuine item authority", "changed") };
        if (!MortalWoundTreatmentItemOwnerSuccessor.IsExactSuccessor(
                _baselineOwnerAuthority,
                _terminalOwners,
                candidate.OwnerAuthority))
        {
            return new[] { Issue("owners", FinalOwnerAuthority.Fingerprint,
                candidate.OwnerAuthority.Fingerprint) };
        }
        var candidateAfterImages = candidate.OwnerCompanionAfterImages;
        foreach (var pair in _publicationAfterImages)
        {
            if (!candidateAfterImages.TryGetValue(pair.Key, out var actual) ||
                !JsonNode.DeepEquals(pair.Value, actual) ||
                !candidate.TouchedPaths.Contains(pair.Key, StringComparer.Ordinal))
            {
                return new[] { Issue(pair.Key, "exact baseline-to-skill-to-item root",
                    "missing or changed") };
            }
        }
        return Array.Empty<ValidationIssue>();
    }

    internal bool MatchesNormalizationSnapshot(
        MortalItemAcceptedTurnNormalizationSnapshot snapshot,
        ResourceOwnerAuthority ownerAuthority)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(ownerAuthority);
        var finalOwnerInput = FinalOwnerAuthority.ExportInput();
        return HasValidSeal() &&
               snapshot.MatchesFinalPublicationBaseline(_snapshot) &&
               snapshot.MatchesAcceptedOwnerAuthority(_baselineOwnerAuthority) &&
               _terminalOwners.All(owner =>
                   _baselineOwnerAuthority.Entries.ContainsKey(owner) &&
                   !FinalOwnerAuthority.Entries.ContainsKey(owner) &&
                   finalOwnerInput.HistoricalOwners.Contains(owner)) &&
               MortalWoundTreatmentItemOwnerSuccessor.IsExactSuccessor(
                   _baselineOwnerAuthority,
                   _terminalOwners,
                   FinalOwnerAuthority) &&
               string.Equals(
                   ownerAuthority.Fingerprint,
                   FinalOwnerAuthority.Fingerprint,
                   StringComparison.Ordinal) &&
               MortalWoundTreatmentItemOwnerSuccessor.IsExactSuccessor(
                   _baselineOwnerAuthority,
                   _terminalOwners,
                   ownerAuthority);
    }

    internal bool ProvesNpcRootTransition(
        JsonObject skillAfterImage,
        JsonObject finalRoot) =>
        TryRecompose(
            out var recomposedSkillAfterImages,
            out var recomposedFinalRoots,
            out _) &&
        WoundAcceptedTurnPlanner.TryReadTreatmentSkillProjection(
            _continuationAuthority,
            PublicationReservationAuthority,
            out _,
            out var sealedSkillAfterImages,
            out var sealedSkillFingerprint) &&
        string.Equals(
            sealedSkillFingerprint,
            SkillProjectionFingerprint,
            StringComparison.Ordinal) &&
        sealedSkillAfterImages.TryGetValue(
            NpcCoreChangesContract.NpcCorePath,
            out var sealedSuppliedSkillRoot) &&
        recomposedSkillAfterImages.ContainsKey(
            NpcCoreChangesContract.NpcCorePath) &&
        recomposedFinalRoots.TryGetValue(
            NpcCoreChangesContract.NpcCorePath,
            out var expectedFinal) &&
        JsonNode.DeepEquals(sealedSuppliedSkillRoot, skillAfterImage) &&
        JsonNode.DeepEquals(expectedFinal, finalRoot);

    internal bool ProvesNormalizedEffectSourceTransition(
        string beforeFingerprint,
        string normalizedFingerprint) =>
        HasValidSeal() &&
        TryRecomposeEffectSourceSuccessor(
            out var recomposedBeforeFingerprint,
            out var recomposedNormalizedFingerprint) &&
        string.Equals(
            beforeFingerprint,
            recomposedBeforeFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            normalizedFingerprint,
            recomposedNormalizedFingerprint,
            StringComparison.Ordinal);
}
