using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeWoundTreatmentAuthority
{
    // Whole-operation parsed DATA, not a request, mutation authority or independent cache.
    internal sealed record NaturalSourceData(string Realm, int CycleOrdinal, string SourceFingerprint,
        WoundCarrierCatalogInput Carriers, EffectCarrierCatalog Effects, WoundIdentityState Identities,
        WoundHistoryState History, WoundCarrierCatalog Catalog, IReadOnlyList<SpiritualWoundRecoveryMember> Members);

    internal static async Task<JsonObject?> ProjectNaturalRecoveryAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, SpiritualSelfTreatmentStageContext context)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        files.VerifyCurrentSessionOperation(lease);
        context = context with { Progression = JsonSerializer.Deserialize<BookOfEternityClient.Models.ProgressionControl>(
            JsonSerializer.Serialize(context.Progression))! };
        if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(context.Progression.CurrentRealm, out var realm) ||
            realm is not ("chaos_sea" or "shining_abode") || context.Progression.AfterlifeCatchupRequired)
            return null;
        var profiles = await ReadSourceAsync(files, lease, WoundCarrierCatalog.AfterlifeProfilesPath);
        if (profiles.Root is null) return null;
        if (profiles.Root["profiles"] is not JsonArray rows)
            throw new InvalidDataException("Natural recovery requires a canonical profile array.");
        var candidate = rows.OfType<JsonObject>().Any(p =>
            AfterlifeEntityProfileState.TryNormalizeEffectRealm(String(p["realm"]), out var profileRealm) && profileRealm == realm &&
            p["activeWounds"] is not null && (p["activeWounds"] is not JsonArray wounds || wounds.Count != 0));
        if (!candidate) return null; // No selected family: avoid reading/parsing the other fifteen roots.
        var sources = new List<SourceFile>();
        foreach (var path in SourcePaths) sources.Add(path == profiles.Path ? profiles : await ReadSourceAsync(files, lease, path));
        var data = PrepareNaturalSourceData(context, sources);
        return data is null ? null : SpiritualHealingRequestProjection.CreateNatural(data);
    }

    internal static NaturalSourceData? PrepareNaturalSourceData(SpiritualSelfTreatmentStageContext context,
        IReadOnlyList<SourceFile> sources)
    {
        var control = context.Progression;
        if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(control.CurrentRealm, out var realm) ||
            realm is not ("chaos_sea" or "shining_abode") ||
            control.AfterlifeCatchupRequired)
            return null; // Independent unsupported modes keep their existing owners.
        JsonObject? Root(string path) => sources.Single(s => s.Path == path).Root;
        var soul = Root(SoulPath);
        if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(String(soul?["currentRealm"]), out var soulRealm) || soulRealm != realm)
            throw new InvalidDataException("Natural recovery control differs from its original soul realm.");
        if (Root(AfterlifeSpiritualConflictState.StatePath)?["activeConflict"] is not null ||
            realm == "shining_abode" && ShiningAbodeState.GetPreparedIncarnationPackageMode(Root(ShiningAbodeState.StatePath)) !=
                ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
            return null;
        var carriers = new WoundCarrierCatalogInput(Root(WoundCarrierCatalog.PlayerPath), Root(WoundCarrierCatalog.NpcPath),
            Root(WoundCarrierCatalog.EnemiesPath), Root(WoundCarrierCatalog.AlliesPath), Root(WoundCarrierCatalog.AfterlifeProfilesPath));
        var catalog = WoundCarrierCatalog.Build(carriers);
        if (catalog.Issues.Count != 0)
            throw new InvalidDataException("Natural recovery requires a canonical original wound catalog.");
        var members = SpiritualHealingRequestProjection.ReadNaturalMembers(catalog, carriers.AfterlifeProfiles, soul, realm);
        if (members.Count == 0) return null;
        var identities = WoundIdentityState.Parse(Root(WoundIdentityState.StatePath)?.ToJsonString(), WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(Root(WoundHistoryState.HistoryPath)?.ToJsonString(), WoundHistoryState.HistoryPath);
        if (catalog.Issues.Count != 0 || !identities.IsValid || !history.IsValid ||
            history.State!.ValidateAgreement(identities.State!, catalog).Count != 0)
            throw new InvalidDataException("Natural recovery requires canonical original wound/identity/History agreement.");
        if (!ProgressionScheduleService.TryBindPreparedSpiritualCycle(Root(ProgressionScheduleService.SchedulePath),
                sources.Single(s => s.Path == EffectAcceptedTurnInputComposer.WorldTimePath).Image.Bytes,
                Root(ShiningAbodeState.StatePath), control, realm, out var ordinal))
            throw new InvalidDataException("Natural recovery requires its exact single prepared safe cycle.");
        var cycle = CycleKey(realm, ordinal);
        if (members.Any(m => m.Wound.Recovery.LastTickKey == cycle))
            throw new InvalidDataException("The original wound cohort already consumed this natural cycle.");
        var effectCatalog = EffectCarrierCatalog.Build(new(Root(EffectCarrierCatalog.PlayerPath), Root(EffectCarrierCatalog.NpcPath),
            carriers.EnemyCombatants, carriers.AllyCombatants, carriers.AfterlifeProfiles, Root(EffectCarrierCatalog.SpiritualConflictPath)));
        var effects = EffectMechanicsSnapshot.Build(effectCatalog, Root(EffectIdentityState.StatePath), null, out var effectIdentities);
        var woundSources = EffectAcceptedTurnInputComposer.BuildCanonicalWoundSourceAuthority(catalog);
        if (!effects.IsAccepted || effectIdentities is null || woundSources.Issues.Count != 0 || members.Any(m =>
                !ValidateCurrentWoundEffects(m.Wound, catalog, effectCatalog, effectIdentities, woundSources)))
            throw new InvalidDataException("Natural recovery requires every original wound's actual effect lineage and source.");
        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "spiritual_natural_recovery_source", "1", context.SessionId, context.RequestId,
            context.Turn.ToString(System.Globalization.CultureInfo.InvariantCulture),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonSerializer.SerializeToNode(control)!)
        }.Concat(sources.SelectMany(s => new[] { s.Path, s.Image.Fingerprint })));
        return new(realm, ordinal, fingerprint, carriers, effectCatalog, identities.State!, history.State!, catalog, members);
    }
}
