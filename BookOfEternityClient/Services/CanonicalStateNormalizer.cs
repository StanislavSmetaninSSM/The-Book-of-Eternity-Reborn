using System.Text.Json;
using System.Text.Json.Nodes;
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
        "game_state/factions/faction_core.json",
        "game_state/npcs/npc_core.json",
        NpcInteractionJournalState.StatePath,
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
            "game_state/world/world_events.json"
        })
        .ToArray();

    public static readonly string[] NormalizerRollbackTrackedFiles = NormalizerBackupInputFiles
        .Concat(new[]
        {
            GuardianProjectState.JournalPath
        })
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private readonly FileSystemManager _fs;
    private readonly ILogger<CanonicalStateNormalizer> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public CanonicalStateNormalizer(FileSystemManager fs, ILogger<CanonicalStateNormalizer> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public async Task NormalizeAccumulatedStateAsync(IReadOnlyDictionary<string, string>? backups = null)
    {
        await NormalizeSoulStateAsync(backups);
        await NormalizeGuardiansAsync(backups);
        await NormalizeGuardianAbodeResidentsAsync(backups);
        await NormalizeGuardianProjectsAsync(backups);
        await NormalizeCharacterChronicleAsync(backups);
        await NormalizeAchievementsAsync(backups);
        await NormalizeCodexAsync(backups);
        await NormalizeQuestStateAsync("game_state/quests/regular_quests.json", "UpdateQuests", backups);
        await NormalizeQuestStateAsync("game_state/quests/soul_quests.json", "UpdateSoulQuests", backups);
        await NormalizeQuestHistoryAsync(backups);
        await NormalizeRivalSoulArcsAsync(backups);
        await NormalizeFactionCoreAsync(backups);
        await NormalizeNpcTradeCoreAsync(backups);
        await NormalizeNpcInteractionJournalAsync(backups);
        await NormalizeInventoryItemResourcesAsync(backups);
        await NormalizeInventoryItemBondsAsync(backups);
        await NormalizeInventoryItemTextsAsync(backups);
        await NormalizeItemJournalsAsync(backups);
        await NormalizeGuardianThoughtJournalAsync(backups);
        await NormalizeGuardianSocialJournalAsync(backups);
        await NormalizeFactionStructureAsync(backups);
        await NormalizeFactionResourcesAsync(backups);
        await NormalizeFactionProjectsAsync(backups);
        await NormalizeFactionCustomAsync(backups);
        await NormalizeFactionChroniclesAsync(backups);
    }
}
