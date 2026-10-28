using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Exact active selection DATA; display names and indexes never identify a wound.</summary>
internal sealed record SpiritualAidSelection(string HealerId, string TargetId, string ParticipationFingerprint);

internal sealed record SpiritualSelfTreatmentSelection(string WoundId, string ExpectedWoundFingerprint)
{
    internal SpiritualAidSelection? Aid { get; init; }
    internal JsonObject ToJson() => Aid is null
        ? new() { ["woundId"] = WoundId, ["expectedWoundFingerprint"] = ExpectedWoundFingerprint }
        : new()
        {
            ["schemaVersion"] = 3, ["mode"] = "aid", ["woundId"] = WoundId,
            ["expectedWoundFingerprint"] = ExpectedWoundFingerprint, ["healerId"] = Aid.HealerId,
            ["targetId"] = Aid.TargetId, ["participationFingerprint"] = Aid.ParticipationFingerprint
        };
    internal static SpiritualSelfTreatmentSelection Parse(JsonNode? node)
    {
        if (node is not JsonObject root ||
            root["woundId"] is not JsonValue id || !id.TryGetValue<string>(out var woundId) ||
            root["expectedWoundFingerprint"] is not JsonValue fp || !fp.TryGetValue<string>(out var fingerprint) ||
            !ResourceMaterializationContract.IsExactIdentifier(woundId) || !ResourceMaterializationContract.IsAuthorityFingerprint(fingerprint))
            throw new InvalidDataException("Invalid exact spiritual wound selection.");
        var selection = new SpiritualSelfTreatmentSelection(woundId!, fingerprint!);
        if (root.Count == 2) return selection; // Still-produced exact self form.
        if (root.Count != 7 || root["schemaVersion"] is not JsonValue version || !version.TryGetValue<int>(out var schema) || schema != 3 ||
            root["mode"] is not JsonValue mode || !mode.TryGetValue<string>(out var treatmentMode) || treatmentMode != "aid" ||
            root["healerId"] is not JsonValue healer || !healer.TryGetValue<string>(out var healerId) || healerId != "player_soul" ||
            root["targetId"] is not JsonValue target || !target.TryGetValue<string>(out var targetId) ||
            !ResourceMaterializationContract.IsExactIdentifier(targetId) || root["participationFingerprint"] is not JsonValue participation ||
            !participation.TryGetValue<string>(out var participationFingerprint) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(participationFingerprint))
            throw new InvalidDataException("Invalid exact spiritual aid selection.");
        return selection with { Aid = new("player_soul", targetId!, participationFingerprint!) };
    }
}

/// <summary>Client staging coordinates and the existing scheduler's ordinary turn control.</summary>
internal sealed record SpiritualSelfTreatmentStageContext(
    string SessionId, string RequestId, int Turn, ProgressionControl Progression);

/// <summary>Staging status only; it grants no wound, cycle or accepted-publication authority.</summary>
internal sealed record SpiritualSelfTreatmentStageResult(
    bool IsStaged, bool AlreadyStaged, string? IssueCode, string? RequestFingerprint)
{
    // Detached GM reference DATA, derived before snapshot without a snapshot token.
    internal JsonObject? GmContext { get; init; }
}

/// <summary>Owns exact source admission for spiritual treatment requests.</summary>
internal static partial class AfterlifeWoundTreatmentAuthority
{
    /// <summary>Stages one retained self-treatment request before ordinary signed snapshot capture.</summary>
    /// <param name="files">The current canonical filesystem.</param>
    /// <param name="lease">Its actual active canonical write lease.</param>
    /// <param name="context">The client turn and scheduler coordinates.</param>
    /// <param name="selection">The exact current wound selected by the player.</param>
    /// <param name="drawDice">Optional entropy dependency; null uses the existing secure GameLoop generator.</param>
    /// <returns>A durable pending request or a refusal with no treatment/publication authority.</returns>
    internal static async Task<SpiritualSelfTreatmentStageResult> StageSelfTreatmentAsync(
        FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease,
        SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection,
        Func<int, int[]>? drawDice = null)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(selection);
        files.EnsureCanonicalWriteLeaseActive(lease);
        files.VerifyCurrentSessionOperation(lease);
        if (!ResourceMaterializationContract.IsExactIdentifier(context.SessionId) ||
            !ResourceMaterializationContract.IsExactIdentifier(context.RequestId) || context.Turn < 1 ||
            context.Progression is null || selection.Aid is not null || !ResourceMaterializationContract.IsExactIdentifier(selection.WoundId))
            return Refuse("context_invalid");

        // Detach mutable caller DATA before the first await. Neither this object nor
        // the entropy dependency is retained by the staged source.
        var control = JsonSerializer.Deserialize<ProgressionControl>(JsonSerializer.Serialize(context.Progression))!;
        try
        {
            var pending = await ReadSourceAsync(files, lease, AcceptedMechanicsPlan.WoundCommandPath);
            if (pending.Image.Existed && (pending.Root is not { } existing ||
                String(existing["sessionId"]) != context.SessionId ||
                String(existing["requestId"]) != context.RequestId ||
                Integer(existing["turn"]) != context.Turn))
                return Refuse("pending_conflict");

            // These are operation-owned source DATA, not another cache. Each unique
            // path is read and parsed once, including authoritative absence. Wound,
            // History, art and effect consumers share the exact same profile root.
            var sources = new List<SourceFile>();
            foreach (var path in SourcePaths)
                sources.Add(await ReadSourceAsync(files, lease, path));
            var admitted = PrepareSourceData(context with { Progression = control }, selection, sources);
            if (admitted.Data is not { } data)
                return Refuse(admitted.IssueCode!);
            return await StageAdmittedActiveTreatmentAsync(files, lease, context, selection, sources, pending, data, drawDice);
        }
        catch (Exception failure) when (failure is JsonException or InvalidDataException)
        {
            return Refuse("source_invalid");
        }
    }

    // One retained-draw/write implementation for both currently closed active
    // families. Caller admission owns the actual lease and fresh source images.
    private static async Task<SpiritualSelfTreatmentStageResult> StageAdmittedActiveTreatmentAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, IReadOnlyList<SourceFile> sources, SourceFile pending,
        SourceData data, Func<int, int[]>? drawDice)
    {
        var source = data.Source;

        var existingRoot = pending.Root;
        if (pending.Image.Existed)
        {
            if (existingRoot is null || !SpiritualSelfTreatmentRequestCodec.IsExactActive(existingRoot, context, selection, data))
                return Refuse("pending_conflict");
            using var freshRetry = FileSystemManager.SuppressValidationReuse();
            if (!await CoordinatedStateWriteHelper.TryCommitAsync(files, lease,
                    sources.Append(pending).Select(s => CoordinatedStateWriteHelper.CreateExactGuardWrite(s.Path, s.Image)).ToArray()))
                return Refuse("source_changed");
            return new(true, true, null, String(existingRoot["requestFingerprint"]))
                { GmContext = SpiritualHealingRequestProjection.Create(data, existingRoot) };
        }

        var draws = (drawDice ?? GameLoop.GenerateSecureRandomDice)(source.RollMode == "normal" ? 1 : 2);
        var resolution = SpiritualHealingResolver.ResolvePrepared(source, source.RollMode, draws);
        if (!resolution.IsValid)
            return Refuse("draw_invalid");
        var request = SpiritualSelfTreatmentRequestCodec.CreateActive(context, selection, data, resolution.Resolution!.SourceRolls);
        var gmContext = SpiritualHealingRequestProjection.Create(data, request, resolution.Resolution);
        var json = request.ToJsonString();
        using var fresh = FileSystemManager.SuppressValidationReuse();
        var writes = sources.Select(s => CoordinatedStateWriteHelper.CreateExactGuardWrite(s.Path, s.Image))
            .Append(new CoordinatedStateWriteHelper.PlannedWrite(pending.Path, null, json,
                RequireCurrentBaseline: true, ExactPrevious: pending.Image)).ToArray();
        if (!await CoordinatedStateWriteHelper.TryCommitAsync(files, lease, writes))
            return Refuse("source_changed");
        try
        {
            var stored = await files.ReadFileBytesAsync(lease, pending.Path);
            if (stored is null || CanonicalJsonUtf8.DecodeOneOptionalBom(stored) != json)
                throw new InvalidDataException("Treatment request readback differs after publication.");
        }
        catch (Exception failure) when (failure is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            throw new CoordinatedStatePublicationUncertainException(failure);
        }
        return new(true, false, null, String(request["requestFingerprint"]))
            { GmContext = gmContext };
    }

    internal sealed record SourceData(WoundMaterializationEnvelope Wound,
        SpiritualHealingPreparedSource Source, string Realm, int CycleOrdinal,
        string SourceFingerprint, WoundCarrierCatalogInput Carriers, EffectCarrierCatalog Effects,
        WoundIdentityState Identities, WoundHistoryState History, WoundCarrierCatalog Catalog,
        IReadOnlyList<SpiritualWoundRecoveryMember> NaturalMembers, JsonObject? AidWitness = null);
    internal sealed record SourceResult(SourceData? Data, string? IssueCode);
    private static SourceResult SourceRefuse(string suffix) => new(null, suffix);

    internal static SourceResult PrepareSourceData(SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, IReadOnlyList<SourceFile> sources)
    {
        if (selection.Aid is not null) return SourceRefuse("target_invalid");
        var control = context.Progression;
        JsonObject? Root(string path) => sources.Single(s => s.Path == path).Root;
        var soul = Root(SoulPath);
        if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(String(soul?["currentRealm"]), out var realm))
            return SourceRefuse("source_invalid");
        if (!ProgressionScheduleService.TryBindPreparedSpiritualCycle(
                Root(ProgressionScheduleService.SchedulePath),
                sources.Single(s => s.Path == EffectAcceptedTurnInputComposer.WorldTimePath).Image.Bytes,
                Root(ShiningAbodeState.StatePath), control, realm, out var ordinal))
            return SourceRefuse("cycle_invalid");
        if (Root(AfterlifeSpiritualConflictState.StatePath) is { } conflict &&
            conflict["activeConflict"] is not null)
            return SourceRefuse("conflict_active");

        var carriers = new WoundCarrierCatalogInput(Root(WoundCarrierCatalog.PlayerPath),
            Root(WoundCarrierCatalog.NpcPath), Root(WoundCarrierCatalog.EnemiesPath),
            Root(WoundCarrierCatalog.AlliesPath), Root(WoundCarrierCatalog.AfterlifeProfilesPath));
        var catalog = WoundCarrierCatalog.Build(carriers);
        var identities = WoundIdentityState.Parse(Root(WoundIdentityState.StatePath)?.ToJsonString(), WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(Root(WoundHistoryState.HistoryPath)?.ToJsonString(), WoundHistoryState.HistoryPath);
        if (catalog.Issues.Count != 0 || !identities.IsValid || !history.IsValid ||
            history.State!.ValidateAgreement(identities.State!, catalog).Count != 0)
            return SourceRefuse("source_invalid");
        var matches = catalog.Occurrences.Where(o => o.WoundId == selection.WoundId).ToArray();
        if (matches.Length != 1 || matches[0].Wound is not { Lifecycle: "active" } wound ||
            WoundIdentityState.ComputeSemanticFingerprint(wound) != selection.ExpectedWoundFingerprint)
            return SourceRefuse("wound_changed");
        if (wound.Owner != new WoundOwnerCoordinate(realm, "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath) ||
            wound.Display.Visibility is not ("public" or "known_to_player"))
            return SourceRefuse("target_invalid");
        var profiles = (carriers.AfterlifeProfiles?["profiles"] as JsonArray)?.OfType<JsonObject>()
            .Where(p => WoundCarrierCollectionAuthority.AfterlifeProfileMatches(p, wound.Owner)).ToArray();
        if (profiles is not { Length: 1 } || !AfterlifeProfileVisibility.IsVisibleToPlayer(profiles[0]))
            return SourceRefuse("target_invalid");
        var effectCatalog = EffectCarrierCatalog.Build(new(Root(EffectCarrierCatalog.PlayerPath),
            Root(EffectCarrierCatalog.NpcPath), carriers.EnemyCombatants, carriers.AllyCombatants,
            carriers.AfterlifeProfiles, Root(EffectCarrierCatalog.SpiritualConflictPath)));
        var effects = EffectMechanicsSnapshot.Build(effectCatalog, Root(EffectIdentityState.StatePath),
            skillScopeAuthority: null, out var effectIdentities);
        if (!effects.IsAccepted || effectIdentities is null ||
            !ValidateCurrentWoundEffects(wound, catalog, effectCatalog, effectIdentities))
            return SourceRefuse("source_invalid");
        var prepared = SpiritualHealingResolver.PrepareSource(new(wound, wound.Owner, profiles[0], soul, effects));
        if (prepared.Source is not { } source)
            return SourceRefuse("source_invalid");
        if (source.HealingTier < source.WoundSeverityRank)
            return SourceRefuse("tier_insufficient");
        var cycleKey = CycleKey(realm, ordinal);
        if (history.State.Transitions.Any(t => t.WoundId == wound.WoundId && t.AttemptId is not null && t.CycleKey == cycleKey))
            return SourceRefuse("attempt_already_accepted");
        var sourceFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "spiritual_self_treatment_source", "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                JsonSerializer.SerializeToNode(control)!)
        }.Concat(sources.SelectMany(s => new[] { s.Path, s.Image.Fingerprint })));

        var members = SpiritualHealingRequestProjection.ReadNaturalMembers(catalog, carriers.AfterlifeProfiles, soul, realm);
        if (members.Any(m => !identities.State!.TryGetEntry(m.Wound.WoundId, out var identity) || identity.CreatedAtTurn >= context.Turn))
            return SourceRefuse("source_invalid");
        return new(new(wound, source, realm, ordinal, sourceFingerprint, carriers, effectCatalog, identities.State!, history.State!, catalog, members), null);
    }

    private static bool ValidateCurrentWoundEffects(WoundMaterializationEnvelope wound,
        WoundCarrierCatalog wounds, EffectCarrierCatalog effects, EffectIdentityState identities,
        EffectSourceAuthority? sourceAuthority = null)
    {
        // Reuse the strict lineage classifier: terminal roots remain in identity
        // history and need no live carrier; missing roots are not terminal roots.
        var lineage = WoundEffectLineagePlanner.Plan(wound, identities);
        if (!lineage.Success) return false;
        var sources = sourceAuthority ?? EffectAcceptedTurnInputComposer.BuildCanonicalWoundSourceAuthority(wounds);
        if (sources.Issues.Count != 0) return false;
        var issues = new List<ValidationIssue>();
        foreach (var effectId in lineage.ActiveOrSuspendedEffectIds)
        {
            if (!effects.TryResolveOne(effectId, out var occurrence)) return false;
            var source = occurrence.Effect["source"]!;
            var binding = sources.ResolveCanonicalBinding(new(wound.Owner.Realm, "wound", wound.WoundId,
                String(source["definitionKey"])!), String(occurrence.Effect["target"]!["kind"])!);
            if (!binding.Success) return false;
            ValidationService.ValidateCanonicalSourceDefinitionAgreement(occurrence, binding.Source!, sources, issues);
        }
        return issues.Count == 0;
    }

    internal static string CycleKey(string realm, int ordinal) =>
        "spiritual_safe_cycle:" + realm + ":" + ordinal.ToString(CultureInfo.InvariantCulture);

    private const string SoulPath = "game_state/meta/soul_state.json";
    internal static IReadOnlyList<string> SourcePaths { get; } = Array.AsReadOnly(new[]
    {
        SoulPath, WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath,
        WoundCarrierCatalog.EnemiesPath, WoundCarrierCatalog.AlliesPath, WoundCarrierCatalog.AfterlifeProfilesPath,
        WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
        EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath, EffectIdentityState.StatePath,
        AfterlifeSpiritualConflictState.StatePath, ProgressionScheduleService.SchedulePath,
        ProgressionScheduleService.ReportPath, EffectAcceptedTurnInputComposer.WorldTimePath, ShiningAbodeState.StatePath
    });

    internal sealed record SourceFile(string Path, CanonicalBeforeImage Image, JsonObject? Root);

    private static async Task<SourceFile> ReadSourceAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, string path)
    {
        var bytes = await files.ReadFileBytesAsync(lease, path);
        if (bytes is null) return new(path, new(false, null), null);
        var text = CanonicalJsonUtf8.DecodeOneOptionalBom(bytes);
        if (JsonNode.Parse(text) is not JsonObject root)
            throw new InvalidDataException("Spiritual treatment source requires an unambiguous object.");
        // Force every lazy JsonObject dictionary once: duplicate keys throw,
        // including in branches not selected by a later domain consumer.
        try { MaterializeProperties(root); }
        catch (ArgumentException failure) { throw new InvalidDataException("Duplicate treatment source property.", failure); }
        return new(path, new(true, bytes), root);
    }

    internal static void MaterializeProperties(JsonNode? node)
    {
        if (node is JsonObject obj)
            foreach (var pair in obj) MaterializeProperties(pair.Value);
        else if (node is JsonArray array)
            foreach (var child in array) MaterializeProperties(child);
    }

    private static string? String(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    private static int? Integer(JsonNode? node) => node is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;
    private static SpiritualSelfTreatmentStageResult Refuse(string suffix) =>
        new(false, false, "spiritual_self_treatment_" + suffix, null);
}
