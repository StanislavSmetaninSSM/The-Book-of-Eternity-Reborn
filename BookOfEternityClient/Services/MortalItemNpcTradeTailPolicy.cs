using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class MortalItemNpcTradeTailPolicy
{
    internal static MortalItemNpcTradeTailDisposition SelectDisposition(
        bool hasTreatmentContinuation,
        bool ownsNpcRoot) =>
        hasTreatmentContinuation && !ownsNpcRoot
            ? MortalItemNpcTradeTailDisposition.SkipUntouchedTreatmentContinuation
            : MortalItemNpcTradeTailDisposition.Apply;

    internal static MortalItemNpcTradeTailDisposition SelectRuntimeDisposition(
        bool hasTreatmentContinuation,
        MortalItemNpcTradeTailDisposition? authenticatedDisposition) =>
        authenticatedDisposition ?? SelectDisposition(
            hasTreatmentContinuation,
            ownsNpcRoot: false);

    internal static bool TryPredictNpcRootOwnership(
        bool ownsNpcSkillProjection,
        IReadOnlyList<MortalWoundTreatmentResourceConsumptionIntent> consumptions,
        IReadOnlyList<MortalItemAcceptedTurnOwner> acceptedOwners,
        IReadOnlyDictionary<string, JsonNode?> projectedItemRoots,
        out bool ownsNpcRoot)
    {
        ArgumentNullException.ThrowIfNull(consumptions);
        ArgumentNullException.ThrowIfNull(acceptedOwners);
        ArgumentNullException.ThrowIfNull(projectedItemRoots);
        ownsNpcRoot = ownsNpcSkillProjection;
        var catalog = MortalItemCanonicalProjectionPlanner.BuildCatalog(
            projectedItemRoots,
            includeNpcCommands: true);
        if (catalog.Issues.Count != 0)
            return false;

        foreach (var consumption in consumptions)
        {
            if (!string.Equals(
                    consumption.Kind,
                    "item_quantity",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryResolveCanonicalItemId(
                    consumption.AuthorityRef,
                    acceptedOwners,
                    out var canonicalItemId))
            {
                return false;
            }
            var authorityKey = MortalItemIdentityRules.BuildConfusableKey(
                canonicalItemId);
            MortalItemCarrierOccurrence? exactOccurrence = null;
            var exactMatches = 0;
            var confusableMatches = 0;
            foreach (var occurrence in catalog.Occurrences)
            {
                if (occurrence.ItemId is not { } itemId ||
                    !string.Equals(
                        MortalItemIdentityRules.BuildConfusableKey(itemId),
                        authorityKey,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                confusableMatches++;
                if (!string.Equals(
                        itemId,
                        canonicalItemId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                exactMatches++;
                exactOccurrence = occurrence;
            }

            if (exactMatches != 1 || confusableMatches != 1)
                return false;

            ownsNpcRoot |= string.Equals(
                exactOccurrence!.FilePath,
                NpcCoreChangesContract.NpcCorePath,
                StringComparison.Ordinal);
        }

        return true;
    }

    private static bool TryResolveCanonicalItemId(
        string authorityRef,
        IReadOnlyList<MortalItemAcceptedTurnOwner> acceptedOwners,
        out string canonicalItemId)
    {
        canonicalItemId = string.Empty;
        var authorityKey = MortalItemIdentityRules.BuildConfusableKey(authorityRef);
        MortalItemAcceptedTurnOwner? exactOwner = null;
        var exactMatches = 0;
        var confusableMatches = 0;
        foreach (var owner in acceptedOwners)
        {
            var acceptedReference = owner.SameTurn
                ? owner.ItemRef
                : owner.ItemId;
            if (acceptedReference is null ||
                !string.Equals(
                    MortalItemIdentityRules.BuildConfusableKey(acceptedReference),
                    authorityKey,
                    StringComparison.Ordinal))
            {
                continue;
            }

            confusableMatches++;
            if (!string.Equals(
                    acceptedReference,
                    authorityRef,
                    StringComparison.Ordinal))
            {
                continue;
            }

            exactMatches++;
            exactOwner = owner;
        }

        if (exactMatches != 1 || confusableMatches != 1 || exactOwner is null)
            return false;
        canonicalItemId = exactOwner.ItemId;
        return true;
    }
}
