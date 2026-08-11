using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;
/// <summary>
/// Reduces command-shaped meta/history payload files into canonical accumulated state files.
/// This keeps viewers and validation aligned on a single storage model.
/// </summary>
public partial class CanonicalStateNormalizer
{
    private static readonly string[] CanonicalAchievementCategories =
    {
        "combat",
        "exploration",
        "story",
        "social",
        "crafting",
        "meta",
        "death",
        "secret"
    };

    private static readonly string[] CanonicalAchievementRarities =
    {
        "common",
        "uncommon",
        "rare",
        "epic",
        "legendary"
    };

    private static readonly string[] CanonicalCodexCategories =
    {
        "cosmology",
        "geography",
        "history",
        "cultures",
        "creatures",
        "characters",
        "artifacts",
        "factions",
        "magic",
        "other"
    };

    public static readonly string[] CanonicalAccumulatedFiles =
    {
        "game_state/meta/soul_state.json",
        "game_state/meta/guardians.json",
        ShiningAbodeState.StatePath,
        GuardianAbodeResidentState.StatePath,
        GuardianProjectState.TrackerPath,
        GuardianPowerEventState.JournalPath,
        "game_state/meta/character_chronicle.json",
        "game_state/meta/achievements.json",
        "lore/codex_entries.json",
        "game_state/quests/regular_quests.json",
        "game_state/quests/soul_quests.json",
        "game_state/quests/quest_history.json",
        "game_state/world/rival_soul_arcs.json",
        StorageTransportMoveService.CurrentLocationPath,
        StorageTransportMoveService.VehiclesPath,
        "game_state/factions/faction_core.json",
        "game_state/npcs/npc_core.json",
        "game_state/npcs/npc_journals.json",
        NpcInteractionJournalState.StatePath,
        "game_state/inventory/items.json",
        MortalItemIdentityState.StatePath,
        "game_state/inventory/item_resources.json",
        "game_state/inventory/item_bonds.json",
        "game_state/inventory/item_text_updates.json",
        "game_state/npcs/item_journals.json",
        GuardianThoughtJournalState.StatePath,
        GuardianSocialJournalState.StatePath,
        "game_state/factions/faction_structure.json",
        "game_state/factions/faction_resources.json",
        "game_state/factions/faction_projects.json",
        "game_state/factions/faction_custom.json",
        "game_state/factions/faction_chronicles.json"
    };

    public static readonly string[] NormalizerBackupInputFiles = CanonicalAccumulatedFiles
        .Concat(new[]
        {
            "game_state/world/world_events.json",
            AfterlifeActiveThreatState.StatePath,
            ChaosSeaGuardianPoliticsState.StatePath,
            AfterlifeStoryOutlineState.StatePath,
            SarefMainStoryState.StatePath
        })
        .ToArray();

    public static readonly string[] NormalizerRollbackTrackedFiles = NormalizerBackupInputFiles
        .Concat(new[]
        {
            GuardianProjectState.JournalPath,
            "game_state/npcs/npc_inventory.json",
            "game_state/inventory/recipes.json",
            CraftRequestState.PendingRequestPath,
            NpcTradeRequestState.PendingRequestPath
        })
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private readonly FileSystemManager _fs;
    private readonly ILogger<CanonicalStateNormalizer> _logger;
    private readonly FileSystemManager.CanonicalWriteLease? _writeLease;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private const string GuardiansStatePath = "game_state/meta/guardians.json";

    private const string GuardianProjectBackupBaselineRequiredMessage =
        "Guardian project normalization requires an explicit usable pre-normalization backup baseline. " +
        "NormalizeAccumulatedStateAsync cannot reconstruct guardian project authority without readable tracker and guardians backups.";

    private const string GuardianProjectCurrentTrackerReadableRequiredMessage =
        "Guardian project normalization requires a readable current guardian_projects.json tracker surface. " +
        "The client cannot reconstruct guardian project authority from malformed or unreadable current tracker state.";

    private const string GuardianProjectCurrentGuardiansReadableRequiredMessage =
        "Guardian project normalization requires a readable current guardians.json authority surface. " +
        "The client cannot reconcile guardian project commands or guardian-side effects from malformed or unreadable current guardian state.";

    internal const string GuardianProjectCurrentSoulStateReadableRequiredMessage =
        "Guardian project normalization requires a readable current soul_state.json authority surface. " +
        "The client cannot reconcile guardian project soul-context-dependent effects from malformed or unreadable current soul state.";

    private const string RivalSoulArcCurrentStateReadableRequiredMessage =
        "Rival soul arc normalization requires a readable current rival_soul_arcs.json surface. " +
        "The client cannot reconcile lore-derived rival clue state from malformed or unreadable current rival arc data.";

    private const string RivalWorldEventsCurrentStateReadableRequiredMessage =
        "Rival soul arc normalization requires a readable current world_events.json surface. " +
        "The client cannot reconcile lore-derived rival clue state from malformed or unreadable current world event data.";

    private sealed record GuardianProjectNormalizationInputs(
        JsonObject? CurrentTrackerRoot,
        JsonObject? PreviousTrackerRoot,
        JsonObject? PreviousGuardiansRoot,
        bool RequiresReadableCurrentGuardians,
        int CurrentIncarnation,
        string? CurrentRealm);

    public CanonicalStateNormalizer(FileSystemManager fs, ILogger<CanonicalStateNormalizer> logger)
        : this(fs, logger, writeLease: null)
    {
    }

    private CanonicalStateNormalizer(
        FileSystemManager fs,
        ILogger<CanonicalStateNormalizer> logger,
        FileSystemManager.CanonicalWriteLease? writeLease)
    {
        _fs = fs;
        _logger = logger;
        _writeLease = writeLease;
    }

    internal CanonicalStateNormalizer BindTo(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        return new CanonicalStateNormalizer(_fs, _logger, writeLease);
    }

    public async Task NormalizeAccumulatedStateAsync(IReadOnlyDictionary<string, string>? backups = null)
    {
        var guardianProjectInputs = await ReadGuardianProjectNormalizationInputsAsync(backups);

        await NormalizeMortalItemsAsync(backups);
        await NormalizeGuardiansAsync(backups);
        await NormalizeGuardianAbodeResidentsAsync(backups);
        await NormalizeShiningAbodeStateAsync(backups);
        await NormalizeGuardianProjectsAsync(guardianProjectInputs);
        await NormalizeCharacterChronicleAsync(backups);
        await NormalizeAchievementsAsync(backups);
        await NormalizeCodexAsync(backups);
        await NormalizeQuestStateAsync("game_state/quests/regular_quests.json", "UpdateQuests", backups);
        await NormalizeQuestStateAsync("game_state/quests/soul_quests.json", "UpdateSoulQuests", backups);
        await NormalizeQuestHistoryAsync(backups);
        await NormalizeRivalSoulArcsAsync(backups);
        await NormalizeSoulStateAsync(backups);
        await NormalizeAfterlifeSpiritualConflictStateAsync(backups);
        await NormalizeAfterlifeEntityProfilesAsync(backups);
        await NormalizeAfterlifeActiveThreatsAsync(backups);
        await NormalizeChaosSeaGuardianPoliticsAsync(backups);
        await NormalizeAfterlifeChroniclesAsync(backups);
        await NormalizeAfterlifeGlobalFlagsAsync(backups);
        await NormalizeAfterlifeStoryOutlineAsync(backups);
        await NormalizeSarefMainStoryStateAsync(backups);
        await NormalizeFactionCoreChangesAsync(backups);
        await NormalizeFactionStructureAsync(backups);
        await NormalizeFactionResourcesAsync(backups);
        await NormalizeFactionProjectsAsync(backups);
        await NormalizeFactionCustomAsync(backups);
        await NormalizeFactionChroniclesAsync(backups);
        await NormalizeFactionCoreAsync(backups);
        await NormalizeNpcCoreChangesAsync(backups);
        await NormalizeNpcTradeCoreAsync(backups);
        await NormalizeNpcJournalsAsync(backups);
        await NormalizeNpcInteractionJournalAsync(backups);
        await NormalizeInventoryItemsAsync(backups);
        await NormalizeInventoryItemResourcesAsync(backups);
        await NormalizeInventoryItemBondsAsync(backups);
        await NormalizeInventoryItemTextsAsync(backups);
        await NormalizeItemJournalsAsync(backups);
        await NormalizeGuardianThoughtJournalAsync(backups);
        await NormalizeGuardianSocialJournalAsync(backups);
    }
}
