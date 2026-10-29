using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class GuardianPolicyContracts
{
    /// <summary>Composes detached currency DATA; only the existing accepted owner may publish it.</summary>
    internal static bool TryPrepareHealingFeatherWallet(JsonObject source, int amount, out JsonObject? after)
    {
        after = null;
        if (source is null || amount <= 0 || TryDescribeInvalidCanonicalInkFeathersRoot(source, out _) ||
            source["inkFeathers"] is not JsonObject wallet ||
            !TryGetNonNegativeIntegerValue(wallet["current"], out var current) || current < amount)
            return false;
        after = wallet.DeepClone().AsObject();
        // No clamping or normalization: total/annotations and every other domain retain their exact values.
        after["current"] = current - amount;
        return true;
    }
}
