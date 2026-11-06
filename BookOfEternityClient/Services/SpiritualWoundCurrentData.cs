namespace BookOfEternityClient.Services;

/// <summary>
/// Reads intrinsic active spiritual wound DATA with complete carrier, identity and History agreement.
/// The caller authenticates its snapshots and current effect/source availability; this reader grants no authority.
/// </summary>
internal static class SpiritualWoundCurrentData
{
    /// <summary>Observes real source attempts for one caller-owned DATA operation; no counter denotes physical IO or authority.</summary>
    internal sealed class ReadDiagnostics
    {
        /// <summary>Gets actual carrier catalog source entries, including a refused build.</summary>
        internal int CatalogBuildAttempts { get; private set; }
        /// <summary>Gets actual complete identity parser entries, including a refused parse.</summary>
        internal int IdentityParseAttempts { get; private set; }
        /// <summary>Gets actual complete History parser entries, including a refused parse.</summary>
        internal int HistoryParseAttempts { get; private set; }
        /// <summary>Gets actual full History/identity/carrier agreement computations.</summary>
        internal int HistoryAgreementAttempts { get; private set; }
        /// <summary>Gets exact requested parent lookup attempts, including a missing target.</summary>
        internal int ParentResolutionAttempts { get; private set; }

        /// <summary>Records entry into the actual carrier catalog builder.</summary>
        internal void RecordCatalogBuild() => CatalogBuildAttempts++;
        /// <summary>Records entry into the actual global identity parser.</summary>
        internal void RecordIdentityParse() => IdentityParseAttempts++;
        /// <summary>Records entry into the actual immutable History parser.</summary>
        internal void RecordHistoryParse() => HistoryParseAttempts++;
        /// <summary>Records entry into the actual global History/identity/carrier agreement validator.</summary>
        internal void RecordHistoryAgreement() => HistoryAgreementAttempts++;
        /// <summary>Records an exact catalog parent resolution, separately from source construction.</summary>
        internal void RecordParentResolution() => ParentResolutionAttempts++;
    }

    /// <summary>Resolves requested parents from one complete validated DATA basis without returning a partial result.</summary>
    /// <param name="carriers">One caller-authenticated intrinsic snapshot, without a permission grant.</param>
    /// <param name="identityJson">The complete identity DATA for that basis.</param>
    /// <param name="historyJson">The complete History DATA for that basis.</param>
    /// <param name="realm">Exact requested spiritual realm.</param>
    /// <param name="woundIds">Exact requested order, including duplicates; an empty list skips validation. This DATA reader does not select a live group.</param>
    /// <param name="actor">Exact actor coordinate required of every returned parent.</param>
    /// <param name="diagnostics">Optional caller-owned observational counters, never shared or used as validation input.</param>
    /// <returns>Complete detached envelopes in requested order, or a refusal without any partial result.</returns>
    internal static IReadOnlyList<WoundMaterializationEnvelope> ReadActiveBatch(WoundCarrierCatalogInput carriers,
        string? identityJson, string? historyJson, string realm, IReadOnlyList<string> woundIds, string actor,
        ReadDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(woundIds);
        var result = new WoundMaterializationEnvelope[woundIds.Count];
        if (result.Length == 0)
            return Array.AsReadOnly(result);
        var view = BuildValidatedView(carriers, identityJson, historyJson, diagnostics);
        for (var index = 0; index < woundIds.Count; index++)
            result[index] = ResolveActive(view, realm, woundIds[index], actor, diagnostics);
        return Array.AsReadOnly(result);
    }

    /// <summary>Shares the existing re-trauma query without duplicating its graph and History checks in strain consumers.</summary>
    internal static WoundMaterializationEnvelope ReadActive(WoundCarrierCatalogInput carriers,
        string? identityJson, string? historyJson, string realm, string woundId, string actor,
        ReadDiagnostics? diagnostics = null) => ResolveActive(
            BuildValidatedView(carriers, identityJson, historyJson, diagnostics), realm, woundId, actor, diagnostics);

    /// <summary>Retains detached catalog/identity DATA only after complete global History agreement for this synchronous call.</summary>
    private sealed record ValidatedView(WoundCarrierCatalog Catalog, WoundIdentityState Identity);

    /// <summary>Validates all supplied DATA before member resolution, including malformed unrequested identities and graphs.</summary>
    private static ValidatedView BuildValidatedView(WoundCarrierCatalogInput carriers,
        string? identityJson, string? historyJson, ReadDiagnostics? diagnostics)
    {
        diagnostics?.RecordCatalogBuild();
        var catalog = WoundCarrierCatalog.Build(carriers);
        if (catalog.Issues.Count != 0)
            throw new InvalidOperationException("exact active re-trauma wound in the authoritative view");
        diagnostics?.RecordIdentityParse();
        var identity = WoundIdentityState.Parse(identityJson, WoundIdentityState.StatePath);
        if (!identity.IsValid)
            throw new InvalidOperationException("original re-trauma identity/carrier agreement");
        diagnostics?.RecordHistoryParse();
        var history = WoundHistoryState.Parse(historyJson, WoundHistoryState.HistoryPath);
        if (!history.IsValid)
            throw new InvalidOperationException("original re-trauma history/identity/carrier agreement");
        diagnostics?.RecordHistoryAgreement();
        if (history.State!.ValidateAgreement(identity.State!, catalog).Count != 0)
            throw new InvalidOperationException("original re-trauma history/identity/carrier agreement");
        return new(catalog, identity.State!);
    }

    /// <summary>Checks every requested member's exact active realm, actor, carrier and identity without treating DATA as authority.</summary>
    private static WoundMaterializationEnvelope ResolveActive(ValidatedView view,
        string realm, string woundId, string actor, ReadDiagnostics? diagnostics)
    {
        diagnostics?.RecordParentResolution();
        if (!view.Catalog.TryResolveOne(woundId, out var occurrence) ||
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
        // Global History agreement already checked this detached immutable envelope's
        // fingerprint against identity and latest History. The private synchronous
        // view exposes no mutation boundary, so a second canonical hash adds no proof.
        if (!view.Identity.TryGetEntry(woundId, out var entry) ||
            entry.Status != "active" || entry.OwnerId != occurrence.Coordinate.OwnerId ||
            entry.OwnerKind != occurrence.Coordinate.OwnerKind ||
            entry.CarrierPath != occurrence.Coordinate.CarrierPath || entry.Realm != realm)
            throw new InvalidOperationException("original re-trauma identity/carrier agreement");
        return occurrence.Wound;
    }
}
