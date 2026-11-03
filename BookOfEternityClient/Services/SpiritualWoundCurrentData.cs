namespace BookOfEternityClient.Services;

/// <summary>
/// Reads intrinsic active spiritual wound DATA with complete carrier, identity and History agreement.
/// The caller authenticates its snapshots and current effect/source availability; this reader grants no authority.
/// </summary>
internal static class SpiritualWoundCurrentData
{
    /// <summary>Shares the existing re-trauma query without duplicating its graph and History checks in strain consumers.</summary>
    internal static WoundMaterializationEnvelope ReadActive(WoundCarrierCatalogInput carriers,
        string? identityJson, string? historyJson, string realm, string woundId, string actor)
    {
        var catalog = WoundCarrierCatalog.Build(carriers);
        if (catalog.Issues.Count != 0 || !catalog.TryResolveOne(woundId, out var occurrence) ||
            occurrence.Wound.Lifecycle != "active" ||
            occurrence.Coordinate.Realm != realm)
            throw new InvalidOperationException("exact active re-trauma wound in the authoritative view");
        var separator = actor.IndexOf(':');
        if (separator <= 0 || separator == actor.Length - 1)
            throw new InvalidOperationException("re-trauma target owner mismatch");
        var ownerId = actor[(separator + 1)..];
        var kind = actor[..separator] switch
        {
            "player_soul" => "player_soul",
            "guardian" => "guardian",
            "resident" or "shining_resident" => "resident",
            "radiant_actor" => "radiant_actor",
            "shining_faction_head" or "saref_agent" or "system_actor" or
                "custom_afterlife_actor" => "afterlife_actor",
            _ => null
        };
        if (occurrence.Coordinate.OwnerId != ownerId ||
            occurrence.Coordinate.OwnerKind != kind ||
            occurrence.Coordinate.CarrierPath != WoundCarrierCatalog.AfterlifeProfilesPath)
            throw new InvalidOperationException("re-trauma target owner mismatch");
        var identity = WoundIdentityState.Parse(identityJson,
            WoundIdentityState.StatePath);
        if (!identity.IsValid || !identity.State!.TryGetEntry(woundId, out var entry) ||
            entry.Status != "active" || entry.OwnerId != occurrence.Coordinate.OwnerId ||
            entry.OwnerKind != occurrence.Coordinate.OwnerKind ||
            entry.CarrierPath != occurrence.Coordinate.CarrierPath || entry.Realm != realm ||
            entry.SemanticFingerprint != WoundIdentityState.ComputeSemanticFingerprint(occurrence.Wound))
            throw new InvalidOperationException("original re-trauma identity/carrier agreement");
        var history = WoundHistoryState.Parse(historyJson,
            WoundHistoryState.HistoryPath);
        if (!history.IsValid ||
            history.State!.ValidateAgreement(identity.State!, catalog).Count != 0)
            throw new InvalidOperationException("original re-trauma history/identity/carrier agreement");
        return occurrence.Wound;
    }
}
