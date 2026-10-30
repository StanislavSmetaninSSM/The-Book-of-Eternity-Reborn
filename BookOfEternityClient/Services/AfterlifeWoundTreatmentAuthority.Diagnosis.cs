using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeWoundTreatmentAuthority
{
    internal sealed record DiagnosisCandidate(string Realm, SpiritualDiagnosisAdmission Admission,
        WoundCarrierCatalogInput Carriers, EffectCarrierCatalog Effects, WoundIdentityState Identities,
        WoundHistoryState History, WoundCarrierCatalog Catalog, IReadOnlyList<SpiritualWoundRecoveryMember> Members,
        EffectMechanicsSnapshot Mechanics);
    internal sealed record DiagnosisSourceData(DiagnosisCandidate Candidate, NaturalSourceData Natural,
        SpiritualSelfTreatmentStageContext Context, JsonObject Request, SpiritualDiagnosisProgram Program,
        string SourceFingerprint, IReadOnlyList<DiagnosisSourceStamp> OriginalSources);
    internal sealed record DiagnosisSourceStamp(string Path, string Fingerprint);
    internal sealed record DiagnosisTurnStageResult(ProgressionControl? Control, SpiritualSelfTreatmentStageResult Stage);

    internal static IReadOnlyList<string> DiagnosisSourcePaths(string realm) => Array.AsReadOnly(SourcePaths.Concat(
        realm == "chaos_sea" ? AidLocalityPaths : new[] { GuardianAbodeResidentState.StatePath })
        .Distinct(StringComparer.Ordinal).ToArray());

    internal static DiagnosisCandidate PrepareDiagnosisCandidate(SpiritualSelfTreatmentSelection selection,
        IReadOnlyList<SourceFile> sources, int turn)
    {
        _ = selection.ToJson();
        var chosen = selection.Diagnosis ?? throw new InvalidDataException("Diagnosis requires its distinct selection purpose.");
        JsonObject? Root(string path) => sources.Single(s => s.Path == path).Root;
        var soul = Root(SoulPath) ?? throw new InvalidDataException("Diagnosis requires its actual current soul source.");
        if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(String(soul["currentRealm"]), out var realm) ||
            realm != chosen.Target.Realm || realm is not ("chaos_sea" or "shining_abode") ||
            Root(AfterlifeSpiritualConflictState.StatePath)?["activeConflict"] is not null ||
            realm == "shining_abode" && ShiningAbodeState.GetPreparedIncarnationPackageMode(Root(ShiningAbodeState.StatePath)) !=
                ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
            throw new InvalidDataException("Diagnosis requires its exact current safe spiritual realm.");
        var carriers = new WoundCarrierCatalogInput(Root(WoundCarrierCatalog.PlayerPath), Root(WoundCarrierCatalog.NpcPath),
            Root(WoundCarrierCatalog.EnemiesPath), Root(WoundCarrierCatalog.AlliesPath), Root(WoundCarrierCatalog.AfterlifeProfilesPath));
        var catalog = WoundCarrierCatalog.Build(carriers);
        var identities = WoundIdentityState.Parse(Root(WoundIdentityState.StatePath)?.ToJsonString(), WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(Root(WoundHistoryState.HistoryPath)?.ToJsonString(), WoundHistoryState.HistoryPath);
        if (catalog.Issues.Count != 0 || !identities.IsValid || !history.IsValid ||
            history.State!.ValidateAgreement(identities.State!, catalog).Count != 0)
            throw new InvalidDataException("Diagnosis requires complete current wound/identity/History agreement.");
        var effects = EffectCarrierCatalog.Build(new(Root(EffectCarrierCatalog.PlayerPath), Root(EffectCarrierCatalog.NpcPath),
            carriers.EnemyCombatants, carriers.AllyCombatants, carriers.AfterlifeProfiles, Root(EffectCarrierCatalog.SpiritualConflictPath)));
        var mechanics = EffectMechanicsSnapshot.Build(effects, Root(EffectIdentityState.StatePath), null, out var effectIdentities);
        var members = SpiritualHealingRequestProjection.ReadNaturalMembers(catalog, carriers.AfterlifeProfiles, soul, realm);
        var woundSources = EffectAcceptedTurnInputComposer.BuildCanonicalWoundSourceAuthority(catalog);
        if (members.Count == 0 || !mechanics.IsAccepted || effectIdentities is null || woundSources.Issues.Count != 0 ||
            members.Any(m => !identities.State!.TryGetEntry(m.Wound.WoundId, out var identity) || identity.CreatedAtTurn >= turn ||
                !ValidateCurrentWoundEffects(m.Wound, catalog, effects, effectIdentities, woundSources)))
            throw new InvalidDataException("Diagnosis requires every original cohort member and its actual effect lineage.");
        var player = (carriers.AfterlifeProfiles?["profiles"] as JsonArray)?.OfType<JsonObject>().Where(p =>
            String(p["actorType"]) == "player_soul" && SpiritualTreatmentParticipation.ProfileId(p) == "player_soul").ToArray();
        if (player is not { Length: 1 } || !AfterlifeEntityProfileState.TryResolveEffectTarget(player[0], out var playerTarget) ||
            playerTarget.Realm != realm || playerTarget.Kind != "player" || playerTarget.TargetId != "player_soul" ||
            !AfterlifeProfileVisibility.IsVisibleToPlayer(player[0]))
            throw new InvalidDataException("Diagnosis requires its exact visible current player profile.");
        IReadOnlyList<SpiritualShiningPatientAdmission> patients = realm == "shining_abode" ? SpiritualTreatmentParticipation.ReadShiningPatientAdmissions(
            Root(ShiningAbodeState.StatePath), carriers.AfterlifeProfiles!, Root(GuardianAbodeResidentState.StatePath)) : [];
        var targets = WoundTargetSelectionService.Build(catalog, carriers.AfterlifeProfiles, realm, true,
            Root(AfterlifeSpiritualConflictState.StatePath), patients,
            realm == "chaos_sea" ? Root(AidGuardiansPath) : null, Root(GuardianAbodeResidentState.StatePath));
        var target = targets.SingleOrDefault(t => t.Owner == chosen.Target && t.Available)
            ?? throw new InvalidDataException("Diagnosis target is no longer exactly visible and reachable in its current locality.");
        var wound = catalog.Occurrences.Select(o => o.Wound).SingleOrDefault(w => w.WoundId == selection.WoundId && w.Owner == chosen.Target)
            ?? throw new InvalidDataException("Diagnosis wound is absent from its exact target.");
        if (WoundIdentityState.ComputeSemanticFingerprint(wound) != selection.ExpectedWoundFingerprint)
            throw new InvalidDataException("The selected diagnosis wound changed.");
        var admission = new SpiritualDiagnosisSourceData(catalog, carriers.AfterlifeProfiles, soul, realm, true, false)
            .Admit(wound, chosen.DiagnosisPathId, target).Data
            ?? throw new InvalidDataException("Every current diagnosis requirement/check/path must be proved.");
        if (admission.Selection != selection) throw new InvalidDataException("The retained diagnosis path changed.");
        return new(realm, admission, carriers, effects, identities.State!, history.State!, catalog, members, mechanics);
    }

    internal static DiagnosisSourceData BindPreparedDiagnosisSource(SpiritualSelfTreatmentStageContext context,
        DiagnosisCandidate candidate, IReadOnlyList<SourceFile> sources, JsonObject? retained = null)
    {
        context = context with { Progression = JsonSerializer.Deserialize<ProgressionControl>(JsonSerializer.Serialize(context.Progression))! };
        JsonObject? Root(string path) => sources.Single(s => s.Path == path).Root;
        if (!ProgressionScheduleService.TryBindPreparedSpiritualCycle(Root(ProgressionScheduleService.SchedulePath),
                sources.Single(s => s.Path == EffectAcceptedTurnInputComposer.WorldTimePath).Image.Bytes,
                Root(ShiningAbodeState.StatePath), context.Progression, candidate.Realm, out var ordinal) ||
            candidate.Members.Any(m => m.Wound.Recovery.LastTickKey == CycleKey(candidate.Realm, ordinal)))
            throw new InvalidDataException("Diagnosis requires its exact unconsumed single safe cycle.");
        var stamps = sources.Select(s => new DiagnosisSourceStamp(s.Path, s.Image.Fingerprint)).ToArray();
        var fingerprint = ComputeDiagnosisSourceFingerprint(context, stamps);
        var natural = new NaturalSourceData(candidate.Realm, ordinal, fingerprint, candidate.Carriers,
            candidate.Effects, candidate.Identities, candidate.History, candidate.Catalog, candidate.Members, candidate.Mechanics);
        var request = retained?.DeepClone().AsObject() ?? SpiritualDiagnosisRequestCodec.Create(context, candidate.Admission.Selection, candidate.Admission);
        var program = SpiritualDiagnosisProgram.Build(context, request, candidate.Admission, candidate.Members);
        return new(candidate, natural, context, request, program, fingerprint, stamps);
    }

    internal static string ComputeDiagnosisSourceFingerprint(SpiritualSelfTreatmentStageContext context,
        IReadOnlyList<DiagnosisSourceStamp> sources) => WoundAcceptedTurnFingerprintWriter.Compute(new[]
        { "spiritual_diagnosis.original_source", "1", context.SessionId, context.RequestId,
            context.Turn.ToString(System.Globalization.CultureInfo.InvariantCulture),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonSerializer.SerializeToNode(context.Progression)) }
            .Concat(sources.SelectMany(s => new[] { s.Path, s.Fingerprint })));

    internal static async Task<SpiritualSelfTreatmentStageResult> StageDiagnosisAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, SpiritualSelfTreatmentStageContext context, SpiritualSelfTreatmentSelection selection)
    {
        files.EnsureCanonicalWriteLeaseActive(lease); files.VerifyCurrentSessionOperation(lease);
        context = context with { Progression = JsonSerializer.Deserialize<ProgressionControl>(JsonSerializer.Serialize(context.Progression))! };
        try
        {
            var sources = await ReadDiagnosisSourcesAsync(files, lease, selection);
            var candidate = PrepareDiagnosisCandidate(selection, sources, context.Turn);
            var pending = await ReadSourceAsync(files, lease, AcceptedMechanicsPlan.WoundCommandPath);
            return await StageBoundDiagnosisAsync(files, lease, sources, pending,
                BindPreparedDiagnosisSource(context, candidate, sources, pending.Root));
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is JsonException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
        { return new(false, false, "spiritual_diagnosis_admission_invalid", null); }
    }

    internal static async Task<DiagnosisTurnStageResult> StagePlayerDiagnosisTurnAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, ProgressionScheduleService scheduler, string sessionId,
        string requestId, int turn, SpiritualSelfTreatmentSelection selection)
    {
        files.EnsureCanonicalWriteLeaseActive(lease); files.VerifyCurrentSessionOperation(lease);
        ProgressionControl? control = null;
        try
        {
            var sources = await ReadDiagnosisSourcesAsync(files, lease, selection);
            var candidate = PrepareDiagnosisCandidate(selection, sources, turn); // Before scheduler writes.
            var pending = await ReadSourceAsync(files, lease, AcceptedMechanicsPlan.WoundCommandPath);
            if (pending.Image.Existed)
                control = pending.Root?["control"]?.Deserialize<ProgressionControl>()
                    ?? throw new InvalidDataException("Diagnosis retry lost its original control.");
            else
            {
                control = await scheduler.BuildControlForNextTurnAsync(lease, activeTurnRealm: null);
                var prepared = await ReadSourceAsync(files, lease, ProgressionScheduleService.SchedulePath);
                await RequireAidReadImagesCurrentAsync(files, lease, sources.Append(pending), ProgressionScheduleService.SchedulePath);
                sources[sources.FindIndex(s => s.Path == prepared.Path)] = prepared;
            }
            var context = new SpiritualSelfTreatmentStageContext(sessionId, requestId, turn, control);
            return new(control, await StageBoundDiagnosisAsync(files, lease, sources, pending,
                BindPreparedDiagnosisSource(context, candidate, sources, pending.Root)));
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is JsonException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
        { return new(control, new(false, false, "spiritual_diagnosis_admission_invalid", null)); }
    }

    private static async Task<List<SourceFile>> ReadDiagnosisSourcesAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, SpiritualSelfTreatmentSelection selection)
    {
        var chosen = selection.Diagnosis ?? throw new InvalidDataException("Missing diagnosis purpose.");
        var sources = new List<SourceFile>();
        foreach (var path in DiagnosisSourcePaths(chosen.Target.Realm)) sources.Add(await ReadSourceAsync(files, lease, path));
        return sources;
    }

    private static async Task<SpiritualSelfTreatmentStageResult> StageBoundDiagnosisAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, IReadOnlyList<SourceFile> sources, SourceFile pending, DiagnosisSourceData data)
    {
        if (pending.Image.Existed && (pending.Root is null || !JsonNode.DeepEquals(pending.Root, data.Request)))
            return new(false, false, "pending_conflict", null);
        var json = data.Request.ToJsonString();
        using var fresh = FileSystemManager.SuppressValidationReuse();
        var writes = sources.Select(s => CoordinatedStateWriteHelper.CreateExactGuardWrite(s.Path, s.Image)).ToList();
        writes.Add(pending.Image.Existed ? CoordinatedStateWriteHelper.CreateExactGuardWrite(pending.Path, pending.Image)
            : new(pending.Path, null, json, RequireCurrentBaseline: true, ExactPrevious: pending.Image));
        if (!await CoordinatedStateWriteHelper.TryCommitAsync(files, lease, writes.ToArray()))
            return new(false, false, "source_changed", null);
        try
        {
            var stored = await files.ReadFileBytesAsync(lease, pending.Path);
            if (stored is null || CanonicalJsonUtf8.DecodeOneOptionalBom(stored) != json)
                throw new InvalidDataException("Diagnosis request readback differs.");
        }
        catch (Exception failure) when (failure is IOException or InvalidDataException or UnauthorizedAccessException)
        { throw new CoordinatedStatePublicationUncertainException(failure); }
        return new(true, pending.Image.Existed, null, data.Request["requestFingerprint"]!.GetValue<string>())
        { GmContext = data.Program.Projection };
    }
}
