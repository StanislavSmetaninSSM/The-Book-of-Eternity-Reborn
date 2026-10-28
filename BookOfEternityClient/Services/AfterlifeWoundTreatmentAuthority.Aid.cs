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

    internal sealed record AidTurnStageResult(ProgressionControl? Control, SpiritualSelfTreatmentStageResult Stage);

    // Called inside GameEngine's existing pre-turn backup/rollback scope. One
    // actual lease spans the committed read BEFORE control/request writes and the
    // fresh closing checks BEFORE the first treatment draw.
    internal static async Task<AidTurnStageResult> StagePlayerAidTurnAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, ProgressionScheduleService scheduler,
        string sessionId, string requestId, int turn, SpiritualSelfTreatmentSelection selection,
        Func<int, int[]>? drawDice = null)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        files.VerifyCurrentSessionOperation(lease);
        selection = SpiritualSelfTreatmentSelection.Parse(selection.ToJson());
        if (selection.Aid is null || selection.Aid is { ReceiveAid: true, PlayerConfirmed: false } ||
            !ResourceMaterializationContract.IsExactIdentifier(sessionId) ||
            !ResourceMaterializationContract.IsExactIdentifier(requestId) || turn < 1)
            return new(null, new(false, false, "spiritual_aid_context_invalid", null));
        ProgressionControl? control = null;
        try
        {
            var grant = await ReadCommittedAidGrantAsync(files, lease, sessionId, turn, selection);
            control = await scheduler.BuildControlForNextTurnAsync(lease, activeTurnRealm: null);
            var preparedSchedule = await ReadSourceAsync(files, lease, ProgressionScheduleService.SchedulePath);
            await RequireAidReadImagesCurrentAsync(files, lease, grant.Reads, ProgressionScheduleService.SchedulePath);
            JsonObject? Root(string path)
            {
                var source = grant.Reads.Single(s => s.Path == path);
                if (source.Root is not null || !source.Image.Existed) return source.Root;
                return StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(source.Image.Bytes!),
                    new JsonSerializerOptions(), path);
            }
            if (control.AfterlifeCatchupRequired || !AfterlifeEntityProfileState.TryNormalizeEffectRealm(control.CurrentRealm, out var realm) || realm != "chaos_sea" ||
                !ProgressionScheduleService.TryBindPreparedSpiritualCycle(preparedSchedule.Root,
                    grant.Reads.Single(s => s.Path == EffectAcceptedTurnInputComposer.WorldTimePath).Image.Bytes,
                    Root(ShiningAbodeState.StatePath), control, realm, out var ordinal) ||
                grant.Witness["record"]!["eligibleCycleOrdinal"]!.GetValue<int>() != ordinal)
                throw new InvalidDataException("Aid lost its exact next prepared safe cycle.");
            var cycle = CycleKey(realm, ordinal);
            if (grant.NaturalMembers.Any(m => m.Wound.Recovery.LastTickKey == cycle ||
                    !grant.Identities.TryGetEntry(m.Wound.WoundId, out var identity) || identity.CreatedAtTurn >= turn) ||
                grant.History.Transitions.Any(t => t.WoundId == selection.WoundId && t.AttemptId is not null && t.CycleKey == cycle))
                throw new InvalidDataException("Aid would repeat a wound session or natural tick.");
            grant.Reads[grant.Reads.FindIndex(s => s.Path == ProgressionScheduleService.SchedulePath)] = preparedSchedule;
            var stageSources = SourcePaths.Concat(AidLocalityPaths).Select(p => grant.Reads.Single(s => s.Path == p)).ToArray();
            var context = new SpiritualSelfTreatmentStageContext(sessionId, requestId, turn, control);
            var data = new SourceData(grant.Wound, grant.Source, realm, ordinal, AidSourceFingerprint(context, stageSources, grant.Witness),
                grant.Carriers, grant.Effects, grant.Identities, grant.History, grant.Catalog, grant.NaturalMembers, grant.Witness.DeepClone().AsObject());
            var pending = grant.Reads.Single(s => s.Path == AcceptedMechanicsPlan.WoundCommandPath);
            // Include the preceding input/protocol read images as operation guards;
            // only the canonical 18 source paths enter the new signed source hash.
            var guards = grant.Reads.Where(s => s.Path != pending.Path).ToArray();
            var staged = await StageAdmittedActiveTreatmentAsync(files, lease, context, selection, guards, pending, data, drawDice);
            return new(control, staged);
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is (JsonException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException))
        {
            return new(control, new(false, false, "spiritual_aid_admission_invalid", null));
        }
    }

    private static string AidSourceFingerprint(SpiritualSelfTreatmentStageContext context,
        IReadOnlyList<SourceFile> sources, JsonObject witness) => WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "spiritual_aid_treatment_source", SpiritualTreatmentParticipation.IsProviderOffer(witness["record"]!.AsObject()) ? "4" : "3", context.SessionId, context.RequestId,
            context.Turn.ToString(System.Globalization.CultureInfo.InvariantCulture),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonSerializer.SerializeToNode(context.Progression)!),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(witness)
        }.Concat(SourcePaths.Concat(AidLocalityPaths).SelectMany(path =>
        {
            var source = sources.Single(s => s.Path == path);
            return new[] { path, source.Image.Fingerprint };
        })));

    internal static SourceResult PrepareAidSourceData(SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, IReadOnlyList<SourceFile> sources, JsonObject witness)
    {
        if (selection.Aid is not { } aid || aid.ReceiveAid && !aid.PlayerConfirmed) return SourceRefuse("target_invalid");
        if (SpiritualTreatmentParticipation.HasUnsettledChange(sources.Single(s => s.Path == WoundCarrierCatalog.AfterlifeProfilesPath).Root))
            return SourceRefuse("participation_change_unsupported");
        var natural = PrepareNaturalSourceData(context, sources);
        if (natural is null || natural.Realm != "chaos_sea") return SourceRefuse("cycle_invalid");
        JsonObject? Root(string path) => sources.Single(s => s.Path == path).Root;
        var matches = natural.Catalog.Occurrences.Where(o => o.WoundId == selection.WoundId).ToArray();
        if (matches is not { Length: 1 } || matches[0].Wound is not { Lifecycle: "active" } wound ||
            wound.Owner != aid.Target(natural.Realm) ||
            wound.Display.Visibility is not ("public" or "known_to_player") ||
            WoundIdentityState.ComputeSemanticFingerprint(wound) != selection.ExpectedWoundFingerprint)
            return SourceRefuse("target_invalid");
        var target = natural.Carriers.AfterlifeProfiles!["profiles"]!.AsArray().OfType<JsonObject>()
            .Single(p => WoundCarrierCollectionAuthority.AfterlifeProfileMatches(p, wound.Owner));
        var healerCoordinate = aid.Healer(natural.Realm);
        var healers = natural.Carriers.AfterlifeProfiles["profiles"]!.AsArray().OfType<JsonObject>()
            .Where(p => WoundCarrierCollectionAuthority.AfterlifeProfileMatches(p, healerCoordinate)).ToArray();
        var scope = SpiritualTreatmentParticipation.RequireCurrentGuardianScope(Root(AidGuardiansPath)
            ?? throw new InvalidDataException("Aid original locality is absent."), Root(GuardianAbodeResidentState.StatePath), aid.GuardianId);
        if (healers is not { Length: 1 } || !AfterlifeProfileVisibility.IsVisibleToPlayer(target) ||
            !AfterlifeProfileVisibility.IsVisibleToPlayer(healers[0]))
            return SourceRefuse("target_invalid");
        var participant = aid.ReceiveAid ? healers[0] : target;
        if (participant["locationId"]?.GetValue<string>() != scope.LocationId) return SourceRefuse("target_invalid");
        RequireOriginalAidWitness(witness, context, selection, participant, natural.History, wound);
        var source = SpiritualHealingResolver.PrepareSource(new(wound, healerCoordinate, healers[0], Root(SoulPath), natural.Mechanics)).Source;
        if (source is null || source.HealingTier < wound.Severity.Rank) return SourceRefuse("tier_insufficient");
        if (natural.History.Transitions.Any(t => t.WoundId == wound.WoundId && t.AttemptId is not null &&
                t.CycleKey == CycleKey(natural.Realm, natural.CycleOrdinal))) return SourceRefuse("attempt_already_accepted");
        return new(new(wound, source, natural.Realm, natural.CycleOrdinal, AidSourceFingerprint(context, sources, witness),
            natural.Carriers, natural.Effects, natural.Identities, natural.History, natural.Catalog, natural.Members,
            witness.DeepClone().AsObject()), null);
    }

    private static void RequireOriginalAidWitness(JsonObject witness, SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, JsonObject target, WoundHistoryState history, WoundMaterializationEnvelope wound)
    {
        if (witness.Count != 3 || witness["record"] is not JsonObject record ||
            String(witness["contentFingerprint"]) != SpiritualTreatmentParticipation.Fingerprint(record) ||
            String(witness["contentFingerprint"]) != selection.Aid!.ParticipationFingerprint ||
            !JsonNode.DeepEquals(target[SpiritualTreatmentParticipation.RecordProperty], record) ||
            target.ContainsKey(SpiritualTreatmentParticipation.ChangeProperty))
            throw new InvalidDataException("Aid original participation content differs from its accepted witness.");
        SpiritualTreatmentParticipation.ValidateRecord(record);
        var previous = history.Transitions.Select(t => t.TransitionResult).OfType<SpiritualHealingPersistedResult>()
            .Where(r => r.ResultFingerprint == String(witness["grantResultFingerprint"])).ToArray();
        if (previous is not { Length: 1 } || !previous[0].IsNatural)
            throw new InvalidDataException("Aid original lacks its actual committed natural grant result.");
        var final = previous[0].ReadFinalWound(wound.WoundId);
        if (previous[0].SchemaVersion != SpiritualTreatmentParticipation.ResultVersion(record) ||
            SpiritualTreatmentParticipation.IsProviderOffer(record) != selection.Aid.ReceiveAid || previous[0].ParticipationOutcomes is not JsonArray outcomes || outcomes.Count != 1 ||
            !JsonNode.DeepEquals(outcomes[0]?["record"], record) || !SpiritualTreatmentParticipation.IsAvailable(record) ||
            String(record["healerId"]) != selection.Aid.HealerId || String(record["targetId"]) != selection.Aid.TargetId ||
            String(record["woundId"]) != wound.WoundId || Integer(record["eligibleCycleOrdinal"]) != context.Progression.NextChaosSeaTurnOrdinal ||
            String(record["locationId"]) != String(target["locationId"]) ||
            String(record["binding"]?["sessionId"]) != context.SessionId || Integer(record["binding"]?["turn"]) != context.Turn - 1 ||
            final is null || WoundIdentityState.ComputeSemanticFingerprint(final) != WoundIdentityState.ComputeSemanticFingerprint(wound))
            throw new InvalidDataException("Aid original grant identity/cycle/current wound does not match its full accepted result.");
    }

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

        // Accepted Engine cleanup normally removes the old request. Its absence
        // is captured as an exact guard; the durable result remains mandatory.
        var requestRoot = await Root(LiveTurnPreparationService.TurnRequestPath);
        var published = await ProgressionScheduleService.ReadPublishedParticipationCycleDataAsync(
            sessionId, checked(turn - 1), selection, Read)
            ?? throw new InvalidDataException("Aid requires the actual immediately preceding published participation turn.");
        var control = published.Root["control"]!.Deserialize<ProgressionControl>()!;
        var realm = "chaos_sea";
        if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(control.CurrentRealm, out var acceptedRealm) || acceptedRealm != realm)
            throw new InvalidDataException("Aid requires a published Chaos participation cycle.");
        if (requestRoot is not null)
        {
            var previous = requestRoot.Deserialize<TurnRequest>(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)
                ?? throw new InvalidDataException("Aid preceding request is invalid.");
            if (previous.SessionId != sessionId || previous.TurnNumber != turn - 1 ||
                previous.RequestId != String(published.Root["binding"]?["requestId"]) ||
                !JsonNode.DeepEquals(JsonSerializer.SerializeToNode(previous.ProgressionControl), published.Root["control"]) ||
                !JsonNode.DeepEquals(previous.SpiritualHealing, published.Root["source"]?["projection"]))
                throw new InvalidDataException("A present preceding request differs from the accepted grant.");
        }
        var outcomes = published.Root["participationOutcomes"]!.AsArray();
        foreach (var carrier in published.CarrierRoots)
        {
            var index = reads.FindIndex(s => s.Path == carrier.Key);
            if (index >= 0) reads[index] = reads[index] with { Root = carrier.Value };
        }
        var record = outcomes[0]!["record"]!.AsObject();
        if (!SpiritualTreatmentParticipation.IsAvailable(record) || SpiritualTreatmentParticipation.IsProviderOffer(record) != aid.ReceiveAid ||
            record["healerId"]?.GetValue<string>() != aid.HealerId ||
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
            await Root(GuardianAbodeResidentState.StatePath), aid.GuardianId);
        if (record["locationId"]?.GetValue<string>() != scope.LocationId)
            throw new InvalidDataException("Aid participation belongs to a different Abode.");
        var profiles = published.CarrierRoots[WoundCarrierCatalog.AfterlifeProfilesPath]!;
        if (SpiritualTreatmentParticipation.HasUnsettledChange(profiles))
            throw new InvalidDataException("Active aid cannot issue a new participation change in the same turn.");
        var target = profiles["profiles"]!.AsArray().OfType<JsonObject>().Where(p =>
            p["actorType"]?.GetValue<string>() == "guardian" && SpiritualTreatmentParticipation.ProfileId(p) == aid.GuardianId).ToArray();
        var player = profiles["profiles"]!.AsArray().OfType<JsonObject>().Where(p =>
            p["actorType"]?.GetValue<string>() == "player_soul" && p["actorId"]?.GetValue<string>() == "player_soul").ToArray();
        var matches = published.Catalog.Occurrences.Where(o => o.WoundId == selection.WoundId).ToArray();
        if (target is not { Length: 1 } || player is not { Length: 1 } || matches is not { Length: 1 } ||
            !AfterlifeProfileVisibility.IsVisibleToPlayer(target[0]) || !AfterlifeProfileVisibility.IsVisibleToPlayer(player[0]) ||
            target[0]["locationId"]?.GetValue<string>() != scope.LocationId || matches[0].Wound is not { Lifecycle: "active" } wound ||
            wound.Owner != aid.Target(realm) ||
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
        var healer = aid.Healer(realm);
        var prepared = SpiritualHealingResolver.PrepareSource(new(wound, healer, aid.ReceiveAid ? target[0] : player[0], soul, effects)).Source;
        if (prepared is null || prepared.HealingTier < wound.Severity.Rank)
            throw new InvalidDataException("Aid requires the actual selected healer's sufficient current healing art.");
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
