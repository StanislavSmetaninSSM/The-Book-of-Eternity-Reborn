using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.Services;

/// <summary>Coherent read-only wound status; never issues command or healing authority.</summary>
internal static class WoundPlayerReadService
{
    private static readonly JsonSerializerOptions StrictOptions = new();

    internal static async Task<WoundPlayerProjectionResult> LoadAsync(FileSystemManager fs)
    {
        ArgumentNullException.ThrowIfNull(fs);
        try
        {
            await using var lease = await fs.AcquireCanonicalWriteLeaseAsync(CanonicalWritePurpose.PublicationReadQuiescence);
            return await LoadAsync(fs, lease);
        }
        catch (Exception failure) when (failure is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return WoundPlayerProjection.Unavailable();
        }
    }

    /// <summary>Reads under a caller-owned lease; never reacquires or disposes it.</summary>
    internal static async Task<WoundPlayerProjectionResult> LoadAsync(FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease lease)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(lease);
        fs.EnsureCanonicalWriteLeaseActive(lease);
        fs.VerifyCurrentSessionOperation(lease);
        try
        {
            var carriers = new WoundCarrierCatalogInput(
                await Root(fs, lease, WoundCarrierCatalog.PlayerPath),
                await Root(fs, lease, WoundCarrierCatalog.NpcPath),
                await Root(fs, lease, WoundCarrierCatalog.EnemiesPath),
                await Root(fs, lease, WoundCarrierCatalog.AlliesPath),
                await Root(fs, lease, WoundCarrierCatalog.AfterlifeProfilesPath));
            var identities = WoundIdentityState.Parse(await RequiredText(fs, lease, WoundIdentityState.StatePath), WoundIdentityState.StatePath);
            var history = WoundHistoryState.Parse(await RequiredText(fs, lease, WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
            if (!identities.IsValid || !history.IsValid) return WoundPlayerProjection.Unavailable();
            var soul = (await Root(fs, lease, "game_state/meta/soul_state.json"))!;
            var currentRealm = ExactString(soul["currentRealm"]);
            string realm;
            if (AfterlifeEntityProfileState.TryNormalizeEffectRealm(currentRealm, out var afterlifeRealm))
                realm = afterlifeRealm;
            else if (RealmSemantics.IsMortalRealm(currentRealm)) realm = "mortal_world";
            else return WoundPlayerProjection.Unavailable();

            var selfVisible = true;
            if (realm != "mortal_world")
            {
                var matches = (carriers.AfterlifeProfiles!["profiles"] as JsonArray)?.OfType<JsonObject>()
                    .Where(p => ExactString(p["actorType"]) == "player_soul" && ExactString(p["actorId"]) == "player_soul").ToArray();
                if (matches is null || matches.Length != 1 ||
                    !AfterlifeEntityProfileState.TryResolveEffectTarget(matches[0], out var target) ||
                    target.Realm != realm || target.Kind != "player" || target.TargetId != "player_soul")
                    return WoundPlayerProjection.Unavailable();
                selfVisible = AfterlifeProfileVisibility.IsVisibleToPlayer(matches[0]);
            }
            // A persistent old afterlife profile is intentionally retained during Mortal life.
            var resources = ResourceDefinitionCatalog.ParseCanonical(
                await RequiredText(fs, lease, ResourceMaterializationContract.DefinitionsPath), allowMissingPristine: false);
            if (!resources.IsValid || resources.Catalog is null) return WoundPlayerProjection.Unavailable();
            var names = KnownItems(await Root(fs, lease, "game_state/inventory/items.json", required: false));
            foreach (var path in new[] { EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath,
                EffectCarrierCatalog.SpiritualConflictPath, EffectIdentityState.StatePath,
                "game_state/player/skills_active.json", "game_state/player/skills_passive.json", "game_state/npcs/npc_core.json" })
                _ = await Root(fs, lease, path, required: false);
            var effects = await EffectMechanicsSnapshot.LoadAsync(fs, lease);
            return WoundPlayerProjection.Build(new(carriers, identities.State!, history.State!, effects,
                realm, resources.Catalog, names, selfVisible));
        }
        catch (Exception failure) when (failure is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            // Detailed authority errors belong in diagnostics, never a player/NPC privacy channel.
            return WoundPlayerProjection.Unavailable();
        }
    }

    private static async Task<string> RequiredText(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease, string path)
    {
        var bytes = await fs.ReadFileBytesAsync(lease, path);
        return bytes is null ? throw new InvalidDataException("Required wound display authority is absent.") :
            CanonicalJsonUtf8.DecodeOneOptionalBom(bytes);
    }

    private static async Task<JsonObject?> Root(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
        string path, bool required = true)
    {
        var bytes = await fs.ReadFileBytesAsync(lease, path);
        if (bytes is null)
        {
            if (required) throw new InvalidDataException("Required wound display carrier is absent.");
            return null;
        }
        return StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), StrictOptions,
            "Wound display input") ?? throw new InvalidDataException("Wound display input must be an object.");
    }

    private static IReadOnlyDictionary<string, string> KnownItems(JsonObject? inventory)
    {
        if (inventory?["items"] is not JsonArray items) return new Dictionary<string, string>();
        return items.OfType<JsonObject>()
            .GroupBy(item => ExactString(item["itemId"]), StringComparer.Ordinal)
            .Where(group => group.Key is not null && group.Count() == 1)
            .Select(group => group.Single())
            .Where(item => (item["contentsPath"] is null || item["contentsPath"] is JsonArray { Count: 0 }) &&
                (item["visibility"] is null || ExactString(item["visibility"]) is "public" or "known_to_player" or "owner_visible") &&
                MortalItemMaterializationContract.TryReadAcceptedIdentity(item, out _))
            .Select(item => (Id: ExactString(item["itemId"])!, Name: ExactString(item["name"])))
            .Where(pair => pair.Name is not null)
            .ToDictionary(pair => pair.Id, pair => pair.Name!, StringComparer.Ordinal);
    }

    private static string? ExactString(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) &&
        !string.IsNullOrWhiteSpace(text) && text == text.Trim() ? text : null;
}
