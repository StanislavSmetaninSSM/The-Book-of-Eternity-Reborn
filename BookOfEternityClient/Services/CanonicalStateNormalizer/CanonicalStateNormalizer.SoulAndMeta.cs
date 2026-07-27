using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private const string PendingTurnSnapshotManifestPath = "game_state/control/pending_turn_snapshot.json";
    private const string SoulStatePath = "game_state/meta/soul_state.json";
    private static readonly JsonSerializerOptions PendingSnapshotHashJsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal sealed record TriggerLifeEndAuthorityResolution(
        bool IsAuthorized,
        string Code,
        string Description,
        string? PreTriggerRealm,
        string? CurrentRealm);

    private sealed class PendingTurnSnapshotAuthorityManifest
    {
        public string SessionId { get; set; } = "";
        public string RequestId { get; set; } = "";
        public int TurnNumber { get; set; }
        public string RequestTimestamp { get; set; } = "";
        public string PlayerAction { get; set; } = "";
        public int[]? PreGeneratedDices1d20 { get; set; }
        public JsonObject? GachaBaseResult { get; set; }
        public ProgressionControl? ProgressionControl { get; set; }
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SnapshotFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ClientOwnedValidationHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RollbackBackups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RollbackBaselineFiles { get; set; } = new();
        public string? SourceLabel { get; set; }
        public string ManifestPayloadHash { get; set; } = "";
    }

    private sealed class PendingTurnRequestAuthorityContext
    {
        public string SessionId { get; set; } = "";
        public string RequestId { get; set; } = "";
        public int TurnNumber { get; set; }
    }

    private static JsonObject BuildNormalizedSoulStateRoot(
        JsonObject current,
        JsonObject? previous,
        int currentTurn,
        bool hasCanonicalTriggerLifeEnd,
        bool enforceStrictCanonicalRoots)
    {
        var result = CloneObject(previous ?? new JsonObject());
        MergeObject(result, current);

        if (enforceStrictCanonicalRoots)
        {
            if (GuardianPolicyContracts.TryDescribeInvalidCanonicalInkFeathersRoot(result, out var inkFeathersFailureDescription))
                throw new InvalidOperationException(inkFeathersFailureDescription);

            if (GuardianPolicyContracts.TryDescribeInvalidCanonicalSoulRelicsRoot(result, out var soulRelicsFailureDescription))
                throw new InvalidOperationException(soulRelicsFailureDescription);

            if (AfterlifeArchiveState.TryDescribeInvalidCanonicalArchiveRoot(result, out var afterlifeArchiveFailureDescription))
                throw new InvalidOperationException(afterlifeArchiveFailureDescription);
        }

        if (current.TryGetPropertyValue("metaStateUpdates", out var metaStateNode) &&
            metaStateNode != null &&
            metaStateNode is not JsonObject)
        {
            throw new InvalidOperationException(GuardianPolicyContracts.InvalidMetaStateUpdatesMessage);
        }

        if (current["metaStateUpdates"] is JsonObject updates)
            ApplyMetaStateUpdates(result, updates, hasCanonicalTriggerLifeEnd);

        if (current.TryGetPropertyValue("afterlifeArchiveUpdates", out var archiveUpdatesNode) &&
            archiveUpdatesNode is not JsonArray)
        {
            throw new InvalidOperationException(GuardianPolicyContracts.InvalidAfterlifeArchiveUpdatesMessage);
        }

        if (current["afterlifeArchiveUpdates"] is JsonArray archiveUpdates)
            AfterlifeArchiveState.ApplyUpdates(result, archiveUpdates);

        if (current.TryGetPropertyValue("archiveActionResolutions", out var archiveActionResolutionsNode) &&
            archiveActionResolutionsNode is not JsonArray)
        {
            throw new InvalidOperationException(GuardianPolicyContracts.InvalidArchiveActionResolutionsMessage);
        }

        if (current["archiveActionResolutions"] is JsonArray archiveActionResolutions)
            AfterlifeArchiveState.ApplyActionResolutions(result, archiveActionResolutions, currentTurn);

        result.Remove("metaStateUpdates");
        result.Remove("afterlifeArchiveUpdates");
        result.Remove("archiveActionResolutions");
        GuardianPolicyContracts.SanitizeSoulStateForCanonicalWrite(result);
        return result;
    }

    private static bool TryReadGuardianProjectAuthoritySoulStateRoot(
        string? soulStateJson,
        bool required,
        out JsonObject? root,
        out string? failureDescription)
    {
        root = null;
        failureDescription = null;

        if (string.IsNullOrWhiteSpace(soulStateJson))
        {
            if (!required)
                return true;

            failureDescription = GuardianProjectCurrentSoulStateReadableRequiredMessage;
            return false;
        }

        try
        {
            if (JsonNode.Parse(soulStateJson) is JsonObject parsedRoot)
            {
                root = parsedRoot;
                return true;
            }
        }
        catch
        {
        }

        if (!required)
            return true;

        failureDescription = GuardianProjectCurrentSoulStateReadableRequiredMessage;
        return false;
    }

    private static bool HasCompleteGuardianProjectCurrentIncarnation(JsonObject soulStateRoot)
    {
        return soulStateRoot["currentIncarnation"] is JsonValue value && value.TryGetValue<int>(out _);
    }

    private static bool HasCompleteGuardianProjectAuthoritySoulContext(
        JsonObject soulStateRoot,
        GuardianProjectSoulContextRequirements requirements)
    {
        if (requirements.RequiresCurrentIncarnation &&
            !HasCompleteGuardianProjectCurrentIncarnation(soulStateRoot))
        {
            return false;
        }

        if (requirements.RequiresCurrentRealm &&
            string.IsNullOrWhiteSpace(GetNodeString(soulStateRoot["currentRealm"])))
        {
            return false;
        }

        return true;
    }

    private static bool TryDescribeInvalidCurrentGuardianProjectSoulStateRoot(
        JsonObject currentRoot,
        bool hasCanonicalTriggerLifeEnd,
        out string failureDescription)
    {
        return GuardianPolicyContracts.TryDescribeInvalidPolicySensitiveReadableSoulStateRoot(
            currentRoot,
            hasCanonicalTriggerLifeEnd,
            out failureDescription);
    }

    internal static bool TryResolveGuardianProjectAuthoritySoulContext(
        string? currentSoulStateJson,
        string? preTurnSoulStateJson,
        string? currentLifeTransitionsJson,
        int currentTurn,
        GuardianProjectSoulContextRequirements requirements,
        out int currentIncarnation,
        out string? currentRealm,
        out string failureDescription)
    {
        currentIncarnation = 0;
        currentRealm = null;
        failureDescription = string.Empty;

        if (!TryReadGuardianProjectAuthoritySoulStateRoot(
                currentSoulStateJson,
                requirements.RequiresReadableCurrentSoulState,
                out var currentRoot,
                out var currentFailureDescription))
        {
            failureDescription = currentFailureDescription ?? GuardianProjectCurrentSoulStateReadableRequiredMessage;
            return false;
        }

        if (currentRoot == null)
            return true;

        TryReadGuardianProjectAuthoritySoulStateRoot(
            preTurnSoulStateJson,
            required: false,
            out var preTurnRoot,
            out _);

        var hasCanonicalTriggerLifeEnd = HasLifecycleAuthorizedTriggerLifeEnd(
            currentLifeTransitionsJson,
            preTurnRoot,
            currentRoot);

        if (requirements.RequiresReadableCurrentSoulState &&
            TryDescribeInvalidCurrentGuardianProjectSoulStateRoot(
                currentRoot,
                hasCanonicalTriggerLifeEnd,
                out var currentRootFailureDescription))
        {
            failureDescription = $"{GuardianProjectCurrentSoulStateReadableRequiredMessage} {currentRootFailureDescription}";
            return false;
        }

        JsonObject soulStateRoot;
        try
        {
            soulStateRoot = BuildNormalizedSoulStateRoot(
                currentRoot,
                preTurnRoot,
                currentTurn,
                hasCanonicalTriggerLifeEnd,
                enforceStrictCanonicalRoots: false);
        }
        catch (InvalidOperationException ex)
        {
            failureDescription = ex.Message;
            return false;
        }

        if (requirements.RequiresReadableCurrentSoulState &&
            !HasCompleteGuardianProjectAuthoritySoulContext(soulStateRoot, requirements))
        {
            failureDescription = GuardianProjectCurrentSoulStateReadableRequiredMessage;
            return false;
        }

        currentIncarnation = GetNodeInt(soulStateRoot["currentIncarnation"], 0);
        currentRealm = GetNodeString(soulStateRoot["currentRealm"]);
        return true;
    }

    private async Task<JsonObject?> BuildNormalizedSoulStateRootAsync(
        IReadOnlyDictionary<string, string>? backups,
        JsonObject? current = null,
        bool enforceStrictCanonicalRoots = false)
    {
        current ??= await ReadObjectAsync(SoulStatePath);
        if (current == null) return null;

        var previous = await ReadBackupObjectAsync(SoulStatePath, backups);
        var currentTurn = await TryReadCurrentTurnNumberAsync();
        var hasCanonicalTriggerLifeEnd = await HasLifecycleAuthorizedTriggerLifeEndAsync(previous, current);
        return BuildNormalizedSoulStateRoot(
            current,
            previous,
            currentTurn,
            hasCanonicalTriggerLifeEnd,
            enforceStrictCanonicalRoots);
    }

    private async Task<(int CurrentIncarnation, string? CurrentRealm)> ReadEffectiveGuardianProjectSoulContextAsync(
        IReadOnlyDictionary<string, string>? backups,
        GuardianProjectSoulContextRequirements requirements,
        JsonObject? currentSoulStateRoot = null)
    {
        currentSoulStateRoot ??= await ReadObjectAsync("game_state/meta/soul_state.json");
        if (requirements.RequiresReadableCurrentSoulState &&
            currentSoulStateRoot == null)
        {
            throw new InvalidOperationException(GuardianProjectCurrentSoulStateReadableRequiredMessage);
        }

        const string soulStatePath = "game_state/meta/soul_state.json";
        var previousSoulStateRoot = await ReadBackupObjectAsync(soulStatePath, backups);

        if (requirements.RequiresReadableCurrentSoulState &&
            currentSoulStateRoot != null &&
            TryDescribeInvalidCurrentGuardianProjectSoulStateRoot(
                currentSoulStateRoot,
                await HasLifecycleAuthorizedTriggerLifeEndAsync(previousSoulStateRoot, currentSoulStateRoot),
                out var currentRootFailureDescription))
        {
            throw new InvalidOperationException(
                $"{GuardianProjectCurrentSoulStateReadableRequiredMessage} {currentRootFailureDescription}");
        }

        var soulStateRoot = await BuildNormalizedSoulStateRootAsync(
            backups,
            currentSoulStateRoot,
            enforceStrictCanonicalRoots: false);
        if (requirements.RequiresReadableCurrentSoulState &&
            (soulStateRoot == null ||
             !HasCompleteGuardianProjectAuthoritySoulContext(soulStateRoot, requirements)))
        {
            throw new InvalidOperationException(GuardianProjectCurrentSoulStateReadableRequiredMessage);
        }

        return (
            GetNodeInt(soulStateRoot?["currentIncarnation"], 0),
            GetNodeString(soulStateRoot?["currentRealm"]));
    }

    private async Task NormalizeSoulStateAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/meta/soul_state.json";
        var current = await ReadObjectAsync(path);
        if (current == null) return;

        var result = await BuildNormalizedSoulStateRootAsync(
            backups,
            current,
            enforceStrictCanonicalRoots: true);
        if (result == null) return;

        await WriteIfChangedAsync(path, current, result);
    }

    private async Task<bool> HasLifecycleAuthorizedTriggerLifeEndAsync(
        JsonObject? preTurnSoulStateRoot,
        JsonObject? currentSoulStateRoot)
    {
        var lifeTransitionsJson = await ReadCanonicalFileAsync("game_state/control/life_transitions.json");
        return HasLifecycleAuthorizedTriggerLifeEnd(lifeTransitionsJson, preTurnSoulStateRoot, currentSoulStateRoot);
    }

    internal static bool TryReadCanonicalTriggerLifeEnd(
        string? lifeTransitionsJson,
        out string reason,
        out string summary)
    {
        reason = string.Empty;
        summary = string.Empty;

        if (string.IsNullOrWhiteSpace(lifeTransitionsJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(lifeTransitionsJson);
            return TryReadCanonicalTriggerLifeEnd(doc.RootElement, out reason, out summary);
        }
        catch
        {
            reason = string.Empty;
            summary = string.Empty;
            return false;
        }
    }

    internal static bool TryReadCanonicalTriggerLifeEnd(
        JsonElement lifeTransitionsRoot,
        out string reason,
        out string summary)
    {
        reason = string.Empty;
        summary = string.Empty;

        if (lifeTransitionsRoot.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var property in lifeTransitionsRoot.EnumerateObject())
        {
            if (property.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.Equals(property.Name, "reason", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(property.Name, "summary", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!lifeTransitionsRoot.TryGetProperty("reason", out var reasonNode) ||
            reasonNode.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        if (!lifeTransitionsRoot.TryGetProperty("summary", out var summaryNode) ||
            summaryNode.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        reason = (reasonNode.GetString() ?? string.Empty).Trim();
        summary = (summaryNode.GetString() ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(summary))
            return false;

        return string.Equals(reason, "Death", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(reason, "Voluntary", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryReadCanonicalTriggerLifeEnd(
        JsonObject? lifeTransitionsRoot,
        out string reason,
        out string summary)
    {
        reason = string.Empty;
        summary = string.Empty;

        if (lifeTransitionsRoot == null)
            return false;

        foreach (var property in lifeTransitionsRoot)
        {
            if (property.Key.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.Equals(property.Key, "reason", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(property.Key, "summary", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (lifeTransitionsRoot["reason"] is not JsonValue reasonNode ||
            !reasonNode.TryGetValue<string>(out var rawReason))
        {
            return false;
        }

        if (lifeTransitionsRoot["summary"] is not JsonValue summaryNode ||
            !summaryNode.TryGetValue<string>(out var rawSummary))
        {
            return false;
        }

        reason = rawReason?.Trim() ?? string.Empty;
        summary = rawSummary?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(summary))
            return false;

        return string.Equals(reason, "Death", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(reason, "Voluntary", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool HasCanonicalTriggerLifeEnd(string? lifeTransitionsJson)
        => TryReadCanonicalTriggerLifeEnd(lifeTransitionsJson, out _, out _);

    internal static bool HasCanonicalTriggerLifeEnd(JsonObject? lifeTransitionsRoot)
        => TryReadCanonicalTriggerLifeEnd(lifeTransitionsRoot, out _, out _);

    internal static bool HasCanonicalTriggerLifeEnd(JsonElement lifeTransitionsRoot)
        => TryReadCanonicalTriggerLifeEnd(lifeTransitionsRoot, out _, out _);

    internal static bool HasLifecycleAuthorizedTriggerLifeEnd(
        string? lifeTransitionsJson,
        string? preTriggerRealm)
    {
        return RealmSemantics.IsMortalRealm(preTriggerRealm) &&
               TryReadCanonicalTriggerLifeEnd(lifeTransitionsJson, out _, out _);
    }

    internal static bool HasLifecycleAuthorizedTriggerLifeEnd(
        string? lifeTransitionsJson,
        string? preTriggerRealm,
        string? currentRealm)
    {
        return ResolveLifecycleAuthorizedTriggerLifeEnd(
            lifeTransitionsJson,
            preTriggerRealm,
            currentRealm).IsAuthorized;
    }

    internal static bool HasLifecycleAuthorizedTriggerLifeEnd(
        string? lifeTransitionsJson,
        JsonObject? preTurnSoulStateRoot)
    {
        return HasLifecycleAuthorizedTriggerLifeEnd(
            lifeTransitionsJson,
            TryReadStrictCurrentRealm(preTurnSoulStateRoot));
    }

    internal static bool HasLifecycleAuthorizedTriggerLifeEnd(
        string? lifeTransitionsJson,
        JsonObject? preTurnSoulStateRoot,
        JsonObject? currentSoulStateRoot)
    {
        return HasLifecycleAuthorizedTriggerLifeEnd(
            lifeTransitionsJson,
            TryReadStrictCurrentRealm(preTurnSoulStateRoot),
            TryReadStrictCurrentRealm(currentSoulStateRoot));
    }

    internal static async Task<bool> HasLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
        FileSystemManager fs,
        string? lifeTransitionsJson,
        JsonObject? currentSoulStateRoot)
    {
        return (await ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
            fs,
            lifeTransitionsJson,
            currentSoulStateRoot)).IsAuthorized;
    }

    internal static TriggerLifeEndAuthorityResolution ResolveLifecycleAuthorizedTriggerLifeEnd(
        string? lifeTransitionsJson,
        string? preTriggerRealm,
        string? currentRealm)
    {
        if (!TryReadCanonicalTriggerLifeEnd(lifeTransitionsJson, out _, out _))
        {
            return new TriggerLifeEndAuthorityResolution(
                false,
                "noncanonical_life_transitions",
                "Canonical TriggerLifeEnd authority requires strict game_state/control/life_transitions.json.",
                preTriggerRealm,
                currentRealm);
        }

        if (string.IsNullOrWhiteSpace(preTriggerRealm))
        {
            return new TriggerLifeEndAuthorityResolution(
                false,
                "missing_preturn_realm",
                "Canonical TriggerLifeEnd authority requires readable pre-turn mortal realm authority from pending snapshot soul_state.",
                preTriggerRealm,
                currentRealm);
        }

        if (!RealmSemantics.IsMortalRealm(preTriggerRealm))
        {
            return new TriggerLifeEndAuthorityResolution(
                false,
                "preturn_afterlife_realm",
                $"Canonical TriggerLifeEnd authority requires mortal pre-turn realm, but pending snapshot soul_state.currentRealm is '{preTriggerRealm}'.",
                preTriggerRealm,
                currentRealm);
        }

        if (string.IsNullOrWhiteSpace(currentRealm))
        {
            return new TriggerLifeEndAuthorityResolution(
                false,
                "missing_current_realm",
                "Canonical TriggerLifeEnd authority requires readable current soul_state.currentRealm before runtime transition flow.",
                preTriggerRealm,
                currentRealm);
        }

        if (!RealmSemantics.IsMortalRealm(currentRealm))
        {
            return new TriggerLifeEndAuthorityResolution(
                false,
                "current_realm_already_afterlife",
                $"Canonical TriggerLifeEnd authority requires mortal current soul_state.currentRealm before runtime transition flow, but current realm is '{currentRealm}'.",
                preTriggerRealm,
                currentRealm);
        }

        return new TriggerLifeEndAuthorityResolution(
            true,
            "authorized",
            string.Empty,
            preTriggerRealm,
            currentRealm);
    }

    internal static async Task<TriggerLifeEndAuthorityResolution> ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
        FileSystemManager fs,
        string? lifeTransitionsJson,
        JsonObject? currentSoulStateRoot)
    {
        var currentRealm = currentSoulStateRoot != null
            ? TryReadStrictCurrentRealm(currentSoulStateRoot)
            : TryReadStrictCurrentRealm(await fs.ReadFileAsync(SoulStatePath));

        if (!TryReadCanonicalTriggerLifeEnd(lifeTransitionsJson, out _, out _))
        {
            return ResolveLifecycleAuthorizedTriggerLifeEnd(lifeTransitionsJson, null, currentRealm);
        }

        var manifestLookup = await TryLoadValidatedActivePendingTurnSnapshotManifestAsync(fs);
        if (!manifestLookup.IsAuthorized)
        {
            return manifestLookup with { CurrentRealm = currentRealm };
        }

        var snapshotSoulStateJson = await TryReadValidatedActivePendingSnapshotFileAsync(
            fs,
            manifestLookup,
            SoulStatePath);
        if (string.IsNullOrWhiteSpace(snapshotSoulStateJson))
        {
            return new TriggerLifeEndAuthorityResolution(
                false,
                "missing_preturn_soul_snapshot",
                "Canonical TriggerLifeEnd authority requires validated pending snapshot game_state/meta/soul_state.json.",
                null,
                currentRealm);
        }

        return ResolveLifecycleAuthorizedTriggerLifeEnd(
            lifeTransitionsJson,
            TryReadStrictCurrentRealm(snapshotSoulStateJson),
            currentRealm);
    }

    internal static async Task<TriggerLifeEndAuthorityResolution> ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
        FileSystemManager fs,
        string? lifeTransitionsJson,
        string? currentRealm)
    {
        if (!TryReadCanonicalTriggerLifeEnd(lifeTransitionsJson, out _, out _))
        {
            return ResolveLifecycleAuthorizedTriggerLifeEnd(lifeTransitionsJson, null, currentRealm);
        }

        var manifestLookup = await TryLoadValidatedActivePendingTurnSnapshotManifestAsync(fs);
        if (!manifestLookup.IsAuthorized)
        {
            return manifestLookup with { CurrentRealm = currentRealm };
        }

        var snapshotSoulStateJson = await TryReadValidatedActivePendingSnapshotFileAsync(
            fs,
            manifestLookup,
            SoulStatePath);
        if (string.IsNullOrWhiteSpace(snapshotSoulStateJson))
        {
            return new TriggerLifeEndAuthorityResolution(
                false,
                "missing_preturn_soul_snapshot",
                "Canonical TriggerLifeEnd authority requires validated pending snapshot game_state/meta/soul_state.json.",
                null,
                currentRealm);
        }

        return ResolveLifecycleAuthorizedTriggerLifeEnd(
            lifeTransitionsJson,
            TryReadStrictCurrentRealm(snapshotSoulStateJson),
            currentRealm);
    }

    internal static string? TryReadStrictCurrentRealm(string? soulStateJson)
    {
        if (string.IsNullOrWhiteSpace(soulStateJson))
            return null;

        try
        {
            return JsonNode.Parse(soulStateJson) is JsonObject soulStateRoot
                ? TryReadStrictCurrentRealm(soulStateRoot)
                : null;
        }
        catch
        {
            return null;
        }
    }

    internal static string? TryReadStrictCurrentRealm(JsonObject? currentSoulStateRoot)
    {
        if (currentSoulStateRoot?["currentRealm"] is not JsonValue realmNode ||
            !realmNode.TryGetValue<string>(out var currentRealm))
        {
            return null;
        }

        return currentRealm;
    }

    private static async Task<TriggerLifeEndAuthorityResolution> TryLoadValidatedActivePendingTurnSnapshotManifestAsync(
        FileSystemManager fs)
    {
        var manifestJson = await fs.ReadFileAsync(PendingTurnSnapshotManifestPath);
        if (string.IsNullOrWhiteSpace(manifestJson))
        {
            return new TriggerLifeEndAuthorityResolution(
                false,
                "missing_active_manifest",
                "Canonical TriggerLifeEnd authority requires active pending_turn_snapshot.json.",
                null,
                null);
        }

        PendingTurnSnapshotAuthorityManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PendingTurnSnapshotAuthorityManifest>(
                manifestJson,
                PendingSnapshotHashJsonOpts);
        }
        catch
        {
            return new TriggerLifeEndAuthorityResolution(
                false,
                "invalid_manifest",
                "Canonical TriggerLifeEnd authority requires readable pending_turn_snapshot.json.",
                null,
                null);
        }

        if (manifest == null ||
            !PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority(
                manifest,
                await fs.ReadFileAsync(PendingTurnSnapshotAuthority.AuthorityPath),
                PendingSnapshotHashJsonOpts,
                static snapshotManifest => snapshotManifest.ManifestPayloadHash,
                static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash,
                static snapshotManifest => snapshotManifest.SessionId,
                static snapshotManifest => snapshotManifest.RequestId,
                static snapshotManifest => snapshotManifest.TurnNumber,
                static snapshotManifest => snapshotManifest.Files,
                static snapshotManifest => snapshotManifest.SnapshotFileHashes,
                static snapshotManifest => snapshotManifest.ClientOwnedValidationHashes,
                static snapshotManifest => snapshotManifest.RollbackBaselineFiles,
                static snapshotManifest => snapshotManifest.SourceLabel,
                static snapshotManifest => snapshotManifest.RollbackBackups,
                relativePath => ReadRelativeFileBytesFromWorkspace(fs, relativePath),
                out _,
                out _))
        {
            return new TriggerLifeEndAuthorityResolution(
                false,
                "invalid_manifest",
                "Canonical TriggerLifeEnd authority requires untampered pending_turn_snapshot.json plus matching detached snapshot authority.",
                null,
                null);
        }

        if (!await IsCurrentPendingTurnSnapshotAsync(fs, manifest))
        {
            return new TriggerLifeEndAuthorityResolution(
                false,
                "inactive_manifest",
                "Canonical TriggerLifeEnd authority requires active pending_turn_snapshot.json that matches the current request context.",
                null,
                null);
        }

        return new TriggerLifeEndAuthorityResolution(
            true,
            "authorized",
            string.Empty,
            null,
            null);
    }

    private static async Task<string?> TryReadValidatedActivePendingSnapshotFileAsync(
        FileSystemManager fs,
        TriggerLifeEndAuthorityResolution manifestLookup,
        string relativePath)
    {
        if (!manifestLookup.IsAuthorized)
            return null;

        var manifestJson = await fs.ReadFileAsync(PendingTurnSnapshotManifestPath);
        if (string.IsNullOrWhiteSpace(manifestJson))
            return null;

        PendingTurnSnapshotAuthorityManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PendingTurnSnapshotAuthorityManifest>(
                manifestJson,
                PendingSnapshotHashJsonOpts);
        }
        catch
        {
            return null;
        }

        if (manifest == null ||
            !PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority(
                manifest,
                await fs.ReadFileAsync(PendingTurnSnapshotAuthority.AuthorityPath),
                PendingSnapshotHashJsonOpts,
                static snapshotManifest => snapshotManifest.ManifestPayloadHash,
                static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash,
                static snapshotManifest => snapshotManifest.SessionId,
                static snapshotManifest => snapshotManifest.RequestId,
                static snapshotManifest => snapshotManifest.TurnNumber,
                static snapshotManifest => snapshotManifest.Files,
                static snapshotManifest => snapshotManifest.SnapshotFileHashes,
                static snapshotManifest => snapshotManifest.ClientOwnedValidationHashes,
                static snapshotManifest => snapshotManifest.RollbackBaselineFiles,
                static snapshotManifest => snapshotManifest.SourceLabel,
                static snapshotManifest => snapshotManifest.RollbackBackups,
                relativePath => ReadRelativeFileBytesFromWorkspace(fs, relativePath),
                out var authorityPayload,
                out _) ||
            !await IsCurrentPendingTurnSnapshotAsync(fs, manifest))
        {
            return null;
        }

        if (manifest?.Files == null ||
            !manifest.Files.TryGetValue(relativePath, out var snapshotPath) ||
            string.IsNullOrWhiteSpace(snapshotPath))
        {
            return null;
        }

        if (manifest.SnapshotFileHashes == null ||
            !manifest.SnapshotFileHashes.TryGetValue(relativePath, out var expectedSnapshotHash) ||
            string.IsNullOrWhiteSpace(expectedSnapshotHash))
        {
            return null;
        }

        var snapshotContent = await fs.ReadFileBytesAsync(snapshotPath);
        if (snapshotContent == null || authorityPayload == null)
            return null;

        return string.Equals(
            PendingTurnSnapshotAuthority.ComputeSnapshotFileHash(authorityPayload, snapshotContent),
            expectedSnapshotHash,
            StringComparison.OrdinalIgnoreCase)
            ? DecodePendingSnapshotText(snapshotContent)
            : null;
    }

    private static string DecodePendingSnapshotText(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static byte[]? ReadRelativeFileBytesFromWorkspace(FileSystemManager fs, string relativePath)
    {
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(relativePath))
            return null;

        var fullPath = fs.ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            return null;

        return File.ReadAllBytes(fullPath);
    }

    private static async Task<bool> IsCurrentPendingTurnSnapshotAsync(
        FileSystemManager fs,
        PendingTurnSnapshotAuthorityManifest manifest)
    {
        const string repairRequestPath = "game_state/control/validation_repair_request.json";
        var repairContext = await ReadPendingTurnRequestContextFromFileAsync(fs, repairRequestPath);
        if (DoesPendingTurnRequestContextMatchManifest(manifest, repairContext))
            return true;

        var turnContext = await ReadPendingTurnRequestContextFromFileAsync(fs, "input/turn_request.json");
        return DoesPendingTurnRequestContextMatchManifest(manifest, turnContext);
    }

    private static async Task<PendingTurnRequestAuthorityContext?> ReadPendingTurnRequestContextFromFileAsync(
        FileSystemManager fs,
        string relativePath)
    {
        var json = await fs.ReadFileAsync(relativePath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            return new PendingTurnRequestAuthorityContext
            {
                SessionId = doc.RootElement.TryGetProperty("sessionId", out var sessionIdNode) &&
                            sessionIdNode.ValueKind == JsonValueKind.String
                    ? sessionIdNode.GetString() ?? string.Empty
                    : string.Empty,
                RequestId = doc.RootElement.TryGetProperty("requestId", out var requestIdNode) &&
                            requestIdNode.ValueKind == JsonValueKind.String
                    ? requestIdNode.GetString() ?? string.Empty
                    : string.Empty,
                TurnNumber = doc.RootElement.TryGetProperty("turnNumber", out var turnNumberNode) &&
                             turnNumberNode.ValueKind == JsonValueKind.Number &&
                             turnNumberNode.TryGetInt32(out var parsedTurnNumber)
                    ? parsedTurnNumber
                    : 0
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool DoesPendingTurnRequestContextMatchManifest(
        PendingTurnSnapshotAuthorityManifest manifest,
        PendingTurnRequestAuthorityContext? context)
    {
        if (context == null)
            return false;

        if (manifest.TurnNumber != context.TurnNumber)
            return false;

        if (!DoesPendingTurnContextIdMatch(manifest.SessionId, context.SessionId))
            return false;

        return DoesPendingTurnContextIdMatch(manifest.RequestId, context.RequestId);
    }

    private static bool DoesPendingTurnContextIdMatch(string manifestId, string contextId)
    {
        return PendingTurnSnapshotAuthority.DoesPendingTurnContextIdMatch(manifestId, contextId);
    }

    private static string ComputePendingSnapshotManifestPayloadHash(PendingTurnSnapshotAuthorityManifest manifest)
    {
        return PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(
            manifest,
            PendingSnapshotHashJsonOpts,
            static snapshotManifest => snapshotManifest.ManifestPayloadHash,
            static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash);
    }

    private static string ComputeSha256(string content)
    {
        return PendingTurnSnapshotAuthority.ComputeSha256(content);
    }


    private async Task NormalizeAchievementsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/meta/achievements.json";
        var current = await ReadObjectAsync(path);
        if (current == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        MergeObject(result, current);

        var unlocked = new List<JsonObject>();
        var tracked = new List<JsonObject>();

        CollectAchievementObjects(previous, "unlockedAchievements", unlocked);
        CollectAchievementObjects(current, "unlockedAchievements", unlocked);
        CollectAchievementObjects(previous, "trackedProgress", tracked);
        CollectAchievementObjects(current, "trackedProgress", tracked);

        if (current["achievementUnlocks"] is JsonArray unlockCommands)
        {
            foreach (var unlock in unlockCommands.OfType<JsonObject>())
                UpsertByIdentity(unlocked, unlock, "achievementId", "name");
        }

        var unlockedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var achievement in unlocked)
        {
            var key = GetNodeString(achievement["achievementId"]) ?? GetNodeString(achievement["name"]);
            if (!string.IsNullOrWhiteSpace(key))
                unlockedIds.Add(key);
        }

        tracked = tracked
            .Where(item =>
            {
                var key = GetNodeString(item["achievementId"]) ?? GetNodeString(item["name"]);
                return string.IsNullOrWhiteSpace(key) || !unlockedIds.Contains(key);
            })
            .ToList();

        result["unlockedAchievements"] = ToArray(unlocked);
        result["trackedProgress"] = ToArray(tracked);
        result["stats"] = BuildAchievementStats(unlocked);
        result.Remove("achievementUnlocks");

        await WriteIfChangedAsync(path, current, result);
    }

    private async Task NormalizeCodexAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "lore/codex_entries.json";
        var current = await ReadObjectAsync(path);
        if (current == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        MergeObject(result, current);

        var entries = new List<JsonObject>();
        CollectCodexEntries(previous, entries);
        CollectCodexEntries(current, entries);

        if (current["loreCodexUpdates"] is JsonArray updates)
            ApplyCodexUpdates(entries, updates);

        result["entries"] = ToArray(entries);
        result["totalEntries"] = entries.Count;
        result["categories"] = BuildCodexCategoryStats(entries);
        result.Remove("loreCodexUpdates");

        await WriteIfChangedAsync(path, current, result);
    }

}

