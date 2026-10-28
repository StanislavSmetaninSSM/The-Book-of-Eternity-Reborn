using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeWoundTreatmentAuthority
{
    private const string AidGuardiansPath = "game_state/meta/guardians.json";
    internal static IReadOnlyList<string> AidLocalityPaths { get; } = Array.AsReadOnly(new[]
        { AidGuardiansPath, GuardianAbodeResidentState.StatePath });

    // Entirely operation-local DATA. The caller keeps the actual lease and closes
    // the read images before control, before entropy and on request publication.
    private sealed record AidGrantData(JsonObject Witness, List<SourceFile> Reads,
        WoundMaterializationEnvelope Wound, SpiritualHealingPreparedSource Source,
        WoundCarrierCatalogInput Carriers, EffectCarrierCatalog Effects,
        WoundIdentityState Identities, WoundHistoryState History, WoundCarrierCatalog Catalog,
        IReadOnlyList<SpiritualWoundRecoveryMember> NaturalMembers);

    private static async Task<AidGrantData> ReadCommittedAidGrantAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, string sessionId, int turn,
        SpiritualSelfTreatmentSelection selection)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        files.VerifyCurrentSessionOperation(lease);
        selection = SpiritualSelfTreatmentSelection.Parse(selection.ToJson());
        var aid = selection.Aid ?? throw new InvalidDataException("Aid requires its explicit selection variant.");
        var reads = new List<SourceFile>();
        async Task<byte[]?> Read(string path)
        {
            var found = reads.SingleOrDefault(s => s.Path == path);
            if (found is not null) return found.Image.Bytes;
            var bytes = await files.ReadFileBytesAsync(lease, path);
            reads.Add(new(path, new(bytes is not null, bytes), null));
            return bytes;
        }
        async Task<JsonObject?> Root(string path)
        {
            var bytes = await Read(path);
            if (bytes is null) return null;
            var index = reads.FindIndex(s => s.Path == path);
            if (reads[index].Root is { } existing) return existing;
            var parsed = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes),
                new JsonSerializerOptions(), path) ?? throw new InvalidDataException("Aid source must be an object.");
            reads[index] = reads[index] with { Root = parsed };
            return parsed;
        }

        var requestRoot = await Root(LiveTurnPreparationService.TurnRequestPath)
            ?? throw new InvalidDataException("Aid lost its actual preceding accepted turn context.");
        var previous = requestRoot.Deserialize<TurnRequest>(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)
            ?? throw new InvalidDataException("Aid preceding turn is invalid.");
        if (previous.SessionId != sessionId || previous.TurnNumber != checked(turn - 1) || previous.ProgressionControl is not { } control ||
            !AfterlifeEntityProfileState.TryNormalizeEffectRealm(control.CurrentRealm, out var realm) || realm != "chaos_sea")
            throw new InvalidDataException("Aid grant must belong to the actual immediately preceding accepted Chaos turn.");
        var published = await ProgressionScheduleService.ReadPublishedSpiritualCycleDataAsync(control,
            previous.SessionId, previous.RequestId, previous.TurnNumber, Read)
            ?? throw new InvalidDataException("Aid requires an actually published participation turn, not a raw stamp.");
        if (!published.Result.IsNatural || published.Root["schemaVersion"]?.GetValue<int>() != 3 ||
            published.Root["participationOutcomes"] is not JsonArray outcomes || outcomes.Count != 1 ||
            !JsonNode.DeepEquals(previous.SpiritualHealing, published.Root["source"]?["projection"]))
            throw new InvalidDataException("The preceding accepted turn did not issue a matching participation outcome.");
        foreach (var carrier in published.CarrierRoots)
        {
            var index = reads.FindIndex(s => s.Path == carrier.Key);
            if (index >= 0) reads[index] = reads[index] with { Root = carrier.Value };
        }
        var record = outcomes[0]!["record"]!.AsObject();
        if (record["status"]?.GetValue<string>() != "granted" || record["healerId"]?.GetValue<string>() != aid.HealerId ||
            record["targetId"]?.GetValue<string>() != aid.TargetId || record["woundId"]?.GetValue<string>() != selection.WoundId ||
            SpiritualTreatmentParticipation.Fingerprint(record) != aid.ParticipationFingerprint ||
            record["acceptedCycleOrdinal"]!.GetValue<int>() != control.NextChaosSeaTurnOrdinal ||
            record["eligibleCycleOrdinal"]!.GetValue<int>() != checked(control.NextChaosSeaTurnOrdinal + 1))
            throw new InvalidDataException("Aid participation is revoked, expired or bound to another healer/target.");
        var soul = await Root(SoulPath) ?? throw new InvalidDataException("Aid requires its current player soul.");
        if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(String(soul["currentRealm"]), out var currentRealm) || currentRealm != realm ||
            (await Root(AfterlifeSpiritualConflictState.StatePath))?["activeConflict"] is not null)
            throw new InvalidDataException("Aid requires the same current safe Chaos realm without an active conflict.");
        var guardians = await Root(AidGuardiansPath) ?? throw new InvalidDataException("Aid requires current Guardian locality.");
        var scope = SpiritualTreatmentParticipation.RequireCurrentGuardianScope(guardians,
            await Root(GuardianAbodeResidentState.StatePath), aid.TargetId);
        if (record["locationId"]?.GetValue<string>() != scope.LocationId)
            throw new InvalidDataException("Aid participation belongs to a different Abode.");
        var profiles = published.CarrierRoots[WoundCarrierCatalog.AfterlifeProfilesPath]!;
        var target = profiles["profiles"]!.AsArray().OfType<JsonObject>().Where(p =>
            p["actorType"]?.GetValue<string>() == "guardian" && SpiritualTreatmentParticipation.ProfileId(p) == aid.TargetId).ToArray();
        var player = profiles["profiles"]!.AsArray().OfType<JsonObject>().Where(p =>
            p["actorType"]?.GetValue<string>() == "player_soul" && p["actorId"]?.GetValue<string>() == "player_soul").ToArray();
        var matches = published.Catalog.Occurrences.Where(o => o.WoundId == selection.WoundId).ToArray();
        if (target is not { Length: 1 } || player is not { Length: 1 } || matches is not { Length: 1 } ||
            !AfterlifeProfileVisibility.IsVisibleToPlayer(target[0]) || !AfterlifeProfileVisibility.IsVisibleToPlayer(player[0]) ||
            target[0]["locationId"]?.GetValue<string>() != scope.LocationId || matches[0].Wound is not { Lifecycle: "active" } wound ||
            wound.Owner != new WoundOwnerCoordinate(realm, "guardian", aid.TargetId, WoundCarrierCatalog.AfterlifeProfilesPath) ||
            wound.Display.Visibility is not ("public" or "known_to_player") ||
            WoundIdentityState.ComputeSemanticFingerprint(wound) != selection.ExpectedWoundFingerprint)
            throw new InvalidDataException("Aid target/wound is no longer known, visible, local and current.");
        var effectCatalog = EffectCarrierCatalog.Build(new(await Root(EffectCarrierCatalog.PlayerPath),
            await Root(EffectCarrierCatalog.NpcPath), published.CarrierRoots[WoundCarrierCatalog.EnemiesPath],
            published.CarrierRoots[WoundCarrierCatalog.AlliesPath], profiles, await Root(EffectCarrierCatalog.SpiritualConflictPath)));
        var effects = EffectMechanicsSnapshot.Build(effectCatalog, await Root(EffectIdentityState.StatePath), null, out var effectIdentities);
        var sourceAuthority = EffectAcceptedTurnInputComposer.BuildCanonicalWoundSourceAuthority(published.Catalog);
        var members = SpiritualHealingRequestProjection.ReadNaturalMembers(published.Catalog, profiles, soul, realm);
        if (!effects.IsAccepted || effectIdentities is null || sourceAuthority.Issues.Count != 0 || members.Any(m =>
                !ValidateCurrentWoundEffects(m.Wound, published.Catalog, effectCatalog, effectIdentities, sourceAuthority)))
            throw new InvalidDataException("Aid lost its complete current wound/effect lineage.");
        var healer = new WoundOwnerCoordinate(realm, "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath);
        var prepared = SpiritualHealingResolver.PrepareSource(new(wound, healer, player[0], soul, effects)).Source;
        if (prepared is null || prepared.HealingTier < wound.Severity.Rank)
            throw new InvalidDataException("Aid requires the actual player's sufficient current healing art.");
        foreach (var path in SourcePaths.Concat(AidLocalityPaths))
            await Read(path); // Complete source/absence identity; roots are decoded only when used.
        await RequireAidReadImagesCurrentAsync(files, lease, reads);
        var witness = new JsonObject { ["record"] = record.DeepClone(),
            ["contentFingerprint"] = aid.ParticipationFingerprint,
            ["grantResultFingerprint"] = published.Root["resultFingerprint"]!.DeepClone() };
        return new(witness, reads, wound, prepared,
            new(published.CarrierRoots[WoundCarrierCatalog.PlayerPath], published.CarrierRoots[WoundCarrierCatalog.NpcPath],
                published.CarrierRoots[WoundCarrierCatalog.EnemiesPath], published.CarrierRoots[WoundCarrierCatalog.AlliesPath], profiles),
            effectCatalog, published.Identities, published.History, published.Catalog, members);
    }

    private static async Task RequireAidReadImagesCurrentAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, IEnumerable<SourceFile> reads, string? exceptPath = null)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        files.VerifyCurrentSessionOperation(lease);
        using var fresh = FileSystemManager.SuppressValidationReuse();
        foreach (var source in reads)
        {
            if (source.Path == exceptPath) continue;
            var bytes = await files.ReadFileBytesAsync(lease, source.Path);
            if (new CanonicalBeforeImage(bytes is not null, bytes).Fingerprint != source.Image.Fingerprint)
                throw new InvalidDataException("Aid source changed during its owned staging operation: " + source.Path);
        }
    }
}
