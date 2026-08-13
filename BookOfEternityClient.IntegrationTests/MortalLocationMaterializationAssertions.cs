using Xunit;

namespace BookOfEternityClient.Tests;

internal static class MortalLocationMaterializationAssertions
{
    private static readonly string[] ForbiddenPlayerAuthorityTokens =
    {
        "materializationReceipt",
        "materializationId",
        "receiptId",
        "sourceAuthority",
        "sourceTurn",
        "initialId",
        "sourceInitialId",
        "targetInitialId",
        "location_identity_index",
        "locationEntries",
        "linkEntries",
        "transitionId",
        "requestId",
        "reservationId",
        "repairPacket",
        "repairTargets",
        "expectedAuthority",
        "actualEvidence",
        "targetFiles",
        "canonicalActorNames",
        "missingFields",
        "exactFieldCorrections",
        "requiredCompanionTargets",
        "safeCorrectionRules",
        "creationRef",
        "itemCreationRef",
        "itemRef",
        "sourceItemId",
        "targetItemId",
        "parentItemId",
        "containerItemId",
        "rewardItemId",
        "destinationItemId",
        "resultItemId",
        "sourceItemIds",
        "parentItemIds",
        "targetItemIds",
        "UpdateInventory",
        "NPCInventoryAdds",
        "NPCInventoryRemovals",
        "removeInventoryItems",
        "UpdateNpcTradeInventoryReceipts",
        "lootForCurrentTurn",
        "knownExits",
        "adjacencyMap",
        "image_prompt"
    };

    internal static void AssertExactBytes(
        IReadOnlyDictionary<string, string?> expected,
        IReadOnlyDictionary<string, string?> actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        Assert.Equal(
            expected.Keys.OrderBy(static value => value, StringComparer.Ordinal),
            actual.Keys.OrderBy(static value => value, StringComparer.Ordinal));
        foreach (var path in expected.Keys)
        {
            Assert.True(actual.ContainsKey(path), $"Missing rollback observation for '{path}'.");
            Assert.Equal(expected[path], actual[path]);
        }
    }

    internal static void AssertNoPlayerAuthorityPayload(
        string payload,
        params string[] additionalForbiddenTokens)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(additionalForbiddenTokens);

        foreach (var token in ForbiddenPlayerAuthorityTokens.Concat(additionalForbiddenTokens))
        {
            Assert.DoesNotContain(token, payload, StringComparison.OrdinalIgnoreCase);
        }
    }
}
