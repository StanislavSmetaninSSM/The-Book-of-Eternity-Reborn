namespace BookOfEternityClient.Services;

internal static class MortalItemNpcTradeTailPolicy
{
    internal static MortalItemNpcTradeTailDisposition SelectDisposition(
        bool hasTreatmentContinuation,
        bool ownsNpcRoot) =>
        hasTreatmentContinuation && !ownsNpcRoot
            ? MortalItemNpcTradeTailDisposition.SkipUntouchedTreatmentContinuation
            : MortalItemNpcTradeTailDisposition.Apply;

    internal static bool TryPredictNpcRootOwnership(
        bool ownsNpcSkillProjection,
        IReadOnlyList<MortalWoundTreatmentResourceConsumptionIntent> consumptions,
        IReadOnlyList<MortalItemAcceptedTurnOwner> acceptedOwners,
        out bool ownsNpcRoot)
    {
        ArgumentNullException.ThrowIfNull(consumptions);
        ArgumentNullException.ThrowIfNull(acceptedOwners);
        ownsNpcRoot = ownsNpcSkillProjection;

        foreach (var consumption in consumptions)
        {
            if (!string.Equals(
                    consumption.Kind,
                    "item_quantity",
                    StringComparison.Ordinal))
            {
                continue;
            }

            var authorityKey = MortalItemIdentityRules.BuildConfusableKey(
                consumption.AuthorityRef);
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
                        consumption.AuthorityRef,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                exactMatches++;
                exactOwner = owner;
            }

            if (exactMatches != 1 || confusableMatches != 1)
                return false;

            ownsNpcRoot |= string.Equals(
                exactOwner!.FilePath,
                NpcCoreChangesContract.NpcCorePath,
                StringComparison.Ordinal);
        }

        return true;
    }
}
