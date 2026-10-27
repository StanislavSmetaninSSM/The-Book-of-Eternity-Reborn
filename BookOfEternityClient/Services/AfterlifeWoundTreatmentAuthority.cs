using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Exact self selection; display names and visible indexes never identify a wound.</summary>
internal sealed record SpiritualSelfTreatmentSelection(string WoundId, string ExpectedWoundFingerprint);

/// <summary>Client staging coordinates and the existing scheduler's ordinary turn control.</summary>
internal sealed record SpiritualSelfTreatmentStageContext(
    string SessionId, string RequestId, int Turn, ProgressionControl Progression);

/// <summary>Staging status only; it grants no wound, cycle or accepted-publication authority.</summary>
internal sealed record SpiritualSelfTreatmentStageResult(
    bool IsStaged, bool AlreadyStaged, string? IssueCode, string? RequestFingerprint);

/// <summary>Owns exact source admission for spiritual treatment requests.</summary>
internal static class AfterlifeWoundTreatmentAuthority
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
            context.Progression is null || !ResourceMaterializationContract.IsExactIdentifier(selection.WoundId))
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
            JsonObject? Root(string path) => sources.Single(s => s.Path == path).Root;
            var soul = Root(SoulPath);
            if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(String(soul?["currentRealm"]), out var realm))
                return Refuse("source_invalid");
            if (!ProgressionScheduleService.TryBindPreparedSpiritualCycle(
                    Root(ProgressionScheduleService.SchedulePath), control, realm, out var ordinal))
                return Refuse("cycle_invalid");
            if (Root(AfterlifeSpiritualConflictState.StatePath) is { } conflict &&
                conflict["activeConflict"] is not null)
                return Refuse("conflict_active");

            var carriers = new WoundCarrierCatalogInput(Root(WoundCarrierCatalog.PlayerPath),
                Root(WoundCarrierCatalog.NpcPath), Root(WoundCarrierCatalog.EnemiesPath),
                Root(WoundCarrierCatalog.AlliesPath), Root(WoundCarrierCatalog.AfterlifeProfilesPath));
            var catalog = WoundCarrierCatalog.Build(carriers);
            var identities = WoundIdentityState.Parse(Root(WoundIdentityState.StatePath)?.ToJsonString(), WoundIdentityState.StatePath);
            var history = WoundHistoryState.Parse(Root(WoundHistoryState.HistoryPath)?.ToJsonString(), WoundHistoryState.HistoryPath);
            if (catalog.Issues.Count != 0 || !identities.IsValid || !history.IsValid ||
                history.State!.ValidateAgreement(identities.State!, catalog).Count != 0)
                return Refuse("source_invalid");
            var matches = catalog.Occurrences.Where(o => o.WoundId == selection.WoundId).ToArray();
            if (matches.Length != 1 || matches[0].Wound is not { Lifecycle: "active" } wound ||
                WoundIdentityState.ComputeSemanticFingerprint(wound) != selection.ExpectedWoundFingerprint)
                return Refuse("wound_changed");
            if (wound.Owner != new WoundOwnerCoordinate(realm, "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath) ||
                wound.Display.Visibility is not ("public" or "known_to_player"))
                return Refuse("target_invalid");
            var profiles = (carriers.AfterlifeProfiles?["profiles"] as JsonArray)?.OfType<JsonObject>()
                .Where(p => WoundCarrierCollectionAuthority.AfterlifeProfileMatches(p, wound.Owner)).ToArray();
            if (profiles is not { Length: 1 } || !AfterlifeProfileVisibility.IsVisibleToPlayer(profiles[0]))
                return Refuse("target_invalid");
            var effects = EffectMechanicsSnapshot.Build(new(new(Root(EffectCarrierCatalog.PlayerPath),
                Root(EffectCarrierCatalog.NpcPath), carriers.EnemyCombatants, carriers.AllyCombatants,
                carriers.AfterlifeProfiles, Root(EffectCarrierCatalog.SpiritualConflictPath)), Root(EffectIdentityState.StatePath)));
            var prepared = SpiritualHealingResolver.PrepareSource(new(wound, wound.Owner, profiles[0], soul, effects));
            if (prepared.Source is not { } source)
                return Refuse("source_invalid");
            if (source.HealingTier < source.WoundSeverityRank)
                return Refuse("tier_insufficient");
            var cycleKey = CycleKey(realm, ordinal);
            if (history.State.Transitions.Any(t => t.WoundId == wound.WoundId && t.AttemptId is not null && t.CycleKey == cycleKey))
                return Refuse("attempt_already_accepted");
            var sourceFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            {
                "spiritual_self_treatment_source", "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                    JsonSerializer.SerializeToNode(control)!)
            }.Concat(sources.SelectMany(s => new[] { s.Path, s.Image.Fingerprint })));

            var existingRoot = pending.Root;
            if (pending.Image.Existed)
            {
                if (existingRoot is null || !SpiritualSelfTreatmentRequestCodec.IsExact(existingRoot, context,
                        selection, realm, ordinal, sourceFingerprint, source))
                    return Refuse("pending_conflict");
                using var freshRetry = FileSystemManager.SuppressValidationReuse();
                if (!await CoordinatedStateWriteHelper.TryCommitAsync(files, lease,
                        sources.Append(pending).Select(s => CoordinatedStateWriteHelper.CreateExactGuardWrite(s.Path, s.Image)).ToArray()))
                    return Refuse("source_changed");
                return new(true, true, null, String(existingRoot["requestFingerprint"]));
            }

            var draws = (drawDice ?? GameLoop.GenerateSecureRandomDice)(source.RollMode == "normal" ? 1 : 2);
            var resolution = SpiritualHealingResolver.ResolvePrepared(source, source.RollMode, draws);
            if (!resolution.IsValid)
                return Refuse("draw_invalid");
            var request = SpiritualSelfTreatmentRequestCodec.Create(context, selection, realm, ordinal,
                sourceFingerprint, source.RollMode, resolution.Resolution!.SourceRolls);
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
            return new(true, false, null, String(request["requestFingerprint"]));
        }
        catch (Exception failure) when (failure is JsonException or InvalidDataException)
        {
            return Refuse("source_invalid");
        }
    }

    internal static string CycleKey(string realm, int ordinal) =>
        "spiritual_safe_cycle:" + realm + ":" + ordinal.ToString(CultureInfo.InvariantCulture);

    private const string SoulPath = "game_state/meta/soul_state.json";
    private static readonly string[] SourcePaths =
    [
        SoulPath, WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath,
        WoundCarrierCatalog.EnemiesPath, WoundCarrierCatalog.AlliesPath, WoundCarrierCatalog.AfterlifeProfilesPath,
        WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
        EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath, EffectIdentityState.StatePath,
        AfterlifeSpiritualConflictState.StatePath, ProgressionScheduleService.SchedulePath,
        ProgressionScheduleService.ReportPath
    ];

    private sealed record SourceFile(string Path, CanonicalBeforeImage Image, JsonObject? Root);

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

    private static void MaterializeProperties(JsonNode? node)
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
