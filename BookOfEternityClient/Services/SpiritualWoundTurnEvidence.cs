using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

/// <summary>
/// Validates retained turn and dice comparison data without authenticating its original snapshot.
/// Owning root parsers reject duplicate JSON properties before invoking this component.
/// </summary>
internal static class SpiritualWoundTurnEvidence
{
    /// <summary>
    /// Checks the closed durable context and the claims belonging to one source exchange.
    /// </summary>
    /// <param name="evidence">
    /// Required detached context object; validation does not mutate it.
    /// </param>
    /// <param name="exchangeOrdinal">
    /// Zero-based source exchange position within the original admitted draft.
    /// </param>
    internal static void Validate(JsonObject evidence, int exchangeOrdinal)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        Closed(evidence, "sessionId requestId snapshotToken turn realm originalSnapshotFingerprint bounds acceptedD20Values diceClaims");
        Text(evidence["sessionId"]);
        Text(evidence["requestId"]);
        Text(evidence["snapshotToken"]);
        Integer(evidence["turn"], 1);
        Require(Text(evidence["realm"]) is "chaos_sea" or "shining_abode", "Invalid spiritual realm.");
        Fingerprint(evidence["originalSnapshotFingerprint"]);
        var bounds = evidence["bounds"] as JsonObject ?? throw new FormatException("Bounds object required.");
        ValidateBounds(bounds);
        var exchangeCount = (int)Integer(bounds["exchangeCount"]);
        Require(exchangeOrdinal >= 0 && exchangeOrdinal < exchangeCount, "Source exchange outside original bounds.");
        var pool = evidence["acceptedD20Values"] as JsonArray ?? throw new FormatException("Original dice array required.");
        Require(pool.Count == Integer(bounds["diceCount"]), "Original dice count differs.");
        foreach (var value in pool) Integer(value, 1, 20);
        var claims = evidence["diceClaims"] as JsonArray ?? throw new FormatException("Dice claims array required.");
        ValidateClaims(claims, pool, exchangeCount, exchangeOrdinal);
    }

    /// <summary>
    /// Checks closed count bounds and the exact two-side source slot relationship.
    /// </summary>
    /// <param name="bounds">
    /// Original-authority count claims; origin is verified separately during reconstruction.
    /// </param>
    internal static void ValidateBounds(JsonObject bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        Closed(bounds, "exchangeCount sourceSlotCount diceCount imagePathCount");
        var exchanges = (int)Integer(bounds["exchangeCount"]);
        Require(Integer(bounds["sourceSlotCount"]) == checked(2 * exchanges), "Source slots differ from exchange bounds.");
        Integer(bounds["diceCount"]);
        Integer(bounds["imagePathCount"]);
    }

    /// <summary>
    /// Checks exact values, digests, unique pool indices and causal claim ordering.
    /// </summary>
    /// <param name="claims">
    /// Closed claim rows, ordered by exchange and then pool index.
    /// </param>
    /// <param name="pool">
    /// Original ordered D20 values; the caller validates its count and every value.
    /// </param>
    /// <param name="exchangeCount">
    /// Nonnegative admitted exchange count.
    /// </param>
    /// <param name="exchangeOrdinal">
    /// Exact exchange for source context, or <see langword="null"/> for a complete packet claim collection.
    /// </param>
    internal static void ValidateClaims(JsonArray claims, JsonArray pool, int exchangeCount, int? exchangeOrdinal)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(pool);
        Require(exchangeCount >= 0 && claims.Count <= pool.Count, "Claim count outside original bounds.");
        var indices = new HashSet<int>();
        var previousExchange = -1;
        var previousIndex = -1;
        foreach (var node in claims)
        {
            var claim = node as JsonObject ?? throw new FormatException("Claim object required.");
            Closed(claim, "sourceIndex value exchangeOrdinal claimFingerprint");
            var index = (int)Integer(claim["sourceIndex"], 0, pool.Count - 1);
            var exchange = (int)Integer(claim["exchangeOrdinal"], 0, exchangeCount - 1);
            Require(exchangeOrdinal is null || exchange == exchangeOrdinal, "Claim belongs to another exchange.");
            Require(indices.Add(index), "An original die cannot be claimed twice.");
            Require(exchange > previousExchange || exchange == previousExchange && index > previousIndex,
                "Claims are not in original exchange and pool order.");
            Require(Integer(claim["value"], 1, 20) == Integer(pool[index], 1, 20), "Claim differs from original die.");
            Require(Fingerprint(claim["claimFingerprint"]) == ComputeClaimFingerprint(claim), "Claim digest differs.");
            previousExchange = exchange;
            previousIndex = index;
        }
    }

    /// <summary>
    /// Computes the comparison digest for a retained die claim without granting spend authority.
    /// </summary>
    /// <param name="claim">
    /// Claim payload; the derived fingerprint itself is excluded.
    /// </param>
    /// <returns>
    /// Versioned domain-separated SHA-256 fingerprint.
    /// </returns>
    internal static string ComputeClaimFingerprint(JsonObject claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return Hash(claim, "die_claim", "claimFingerprint");
    }
}
