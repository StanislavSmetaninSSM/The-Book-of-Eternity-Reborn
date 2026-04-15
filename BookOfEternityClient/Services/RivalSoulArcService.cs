using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public sealed class RivalSoulArcService
{
    public const string StatePath = "game_state/world/rival_soul_arcs.json";

    private const string CurrentLocationPath = "game_state/world/current_location.json";
    private const string WorldEventsPath = "game_state/world/world_events.json";
    private const string PlotOutlinePath = "game_state/quests/plot_outline.json";
    private const string NpcCorePath = "game_state/npcs/npc_core.json";
    private const string FactionCorePath = "game_state/factions/faction_core.json";
    private const string SoulQuestsPath = "game_state/quests/soul_quests.json";
    private const string WorldSettingPath = "lore/current_world/world_setting.json";

    private static readonly string[] PoliticalClaimKeywords =
    {
        "king", "kingdom", "court", "capital", "empire", "imper", "throne", "dynast", "noble", "power",
        "senate", "republic", "governor", "election", "parliament", "council", "state", "regime", "administration",
        "corporation", "megacorp", "board", "director", "ceo", "consortium", "syndicate", "cartel", "colony",
        "guild", "guildhall", "enclave", "warlord", "settlement", "stronghold",
        "federation", "directorate", "assembly", "planetary", "interstellar", "sector", "fleet command",
        "mayor", "minister", "party", "campaign", "oligarch", "media mogul", "city hall", "administration building",
        "duke", "count", "knight order", "holy order", "merchant guild", "craft guild",
        "hero program", "superhuman registry", "metahuman office", "cape authority",
        "detective bureau", "police chief", "district attorney", "commissioner", "precinct", "task group",
        "корол", "двор", "столиц", "импер", "трон", "династ", "виконт", "барон", "кня", "власт",
        "сенат", "республ", "губернат", "выбор", "парламент", "совет", "государ", "режим", "администр",
        "корпорац", "мегакорп", "директор", "консорци", "синдик", "картел", "колони", "гильд",
        "анклав", "вожд", "поселен", "укреплен",
        "федерац", "директорат", "ассамбле", "планетар", "межзвезд", "сектор", "флот",
        "мэр", "министр", "парт", "кампан", "олигарх", "медиамагнат", "мэрия",
        "герцог", "граф", "рыцарск", "священн орден", "купеческ", "ремеслен",
        "реестр сверхлюдей", "метачеловеческ", "геройская программа", "надзор за кейпами",
        "полицейск управл", "комиссар", "прокурат", "окружн прокурор", "детективн бюро", "оперативн группа"
    };

    private static readonly string[] IdeologicalMissionKeywords =
    {
        "sect", "cult", "faith", "doctrine", "shrine", "church", "order", "ideology", "manifesto", "prophet",
        "revolution", "rebellion", "heresy", "dogma", "creed", "awakening", "signal", "movement", "cause",
        "purity", "salvation", "mutation", "techno-cult", "machine god",
        "activist", "radicalization", "conspiracy", "whistleblower", "cult leader", "grassroots",
        "paladin", "cathedral", "totem", "spirit", "druid", "immortal", "dao", "tribulation", "elder",
        "mutant rights", "supremacist", "anomaly cult", "awakening movement", "gifted manifesto",
        "occult", "haunting", "possession", "ritual", "coven", "witchcraft", "paranormal", "unspeakable", "nightmare",
        "сект", "культ", "вера", "доктрин", "церк", "орден", "идеолог", "манифест", "пророк",
        "революц", "мятеж", "ерес", "догм", "учен", "пробужд", "сигнал", "движен", "дело",
        "чистот", "спасен", "мутац", "технокульт", "машинн бог",
        "активист", "радикал", "заговор", "разоблач", "лидер культа", "низов",
        "палад", "собор", "тотем", "дух", "друид", "бессмерт", "дао", "испытан", "старейш",
        "права мутантов", "супремас", "культ аномал", "движение пробужд", "одарен",
        "оккульт", "одержим", "ритуал", "ковен", "ведьм", "паранорм", "кошмар", "призрак", "проклятый дом"
    };

    private static readonly string[] ArtifactRaceKeywords =
    {
        "artifact", "relic", "ancient", "ruin", "seal", "tomb", "vault", "archive", "core", "shard", "key",
        "prototype", "data vault", "blacksite", "anomaly", "station", "lab", "chip", "engine", "module",
        "bunker", "reactor", "stash", "cache", "fabricator", "prewar", "old world", "salvage",
        "ai", "android", "synthetic", "drone", "quantum", "neural", "uplink", "simulation", "singularity",
        "genetic", "genome", "clone", "bioengineer", "specimen", "parasite", "hive",
        "evidence", "server", "hard drive", "research file", "biolab", "leak", "dossier", "case file",
        "dungeon", "crypt", "labyrinth", "dragon hoard", "forge", "alchemy", "runesmith", "moonwell", "grove",
        "serum", "gene sample", "power source", "containment device", "portal fragment", "anomaly core", "mutagen",
        "case file", "cold case", "evidence locker", "forensics", "crime scene", "occult file", "grimoire", "cursed film", "sealed room",
        "артеф", "релик", "древн", "руин", "печат", "гробниц", "хранилищ", "архив", "ядр", "оскол", "ключ",
        "прототип", "аномал", "станц", "лабора", "чип", "двигател", "модул",
        "бункер", "реактор", "тайник", "схрон", "фабрикат", "довоен", "старого мира", "троф",
        "ии", "андроид", "синтет", "дрон", "квант", "нейро", "симуляц", "сингуляр",
        "генет", "геном", "клон", "биоинжен", "образец", "паразит", "рой",
        "улик", "сервер", "жестк", "досье", "утечк", "материал дела",
        "подземель", "склеп", "лабиринт", "дракон", "кузн", "алхим", "рун", "лунн колод", "рощ",
        "сыворот", "генетическ образец", "источник силы", "устройств сдержив", "осколок портала", "ядро аномал", "мутаген",
        "дело", "глухар", "улики", "вещдок", "место преступ", "оккультн досье", "гримуар", "проклят", "запечатанн комната"
    };

    private static readonly string[] RivalAscensionKeywords =
    {
        "tournament", "genius", "ascend", "ascension", "cultiv", "prodigy", "champion", "chosen", "heir", "promotion",
        "elite", "legend", "rising star", "ace", "netrunner", "candidate", "breakthrough", "successor",
        "guild", "adventurer", "hero", "mage", "wizard", "sorcerer", "warlock", "spellcaster", "academy", "school", "master",
        "stalker", "survivor", "scavenger", "hunter", "pathfinder",
        "captain", "admiral", "pilot", "scientist", "researcher", "explorer", "xenolog", "commander", "officer",
        "journalist", "lawyer", "athlete", "celebrity", "influencer", "startup", "founder", "young politician",
        "paladin", "knight", "monster hunter", "young master", "forge master", "alchemist", "archmage",
        "superhero", "superhuman", "vigilante", "powered", "metahuman", "gifted", "cape", "anomaly-born", "hero academy",
        "detective", "investigator", "profiler", "inspector", "sleuth", "medium", "occult investigator", "exorcist", "survivor final girl",
        "турнир", "гений", "культив", "восхожд", "продиж", "чемпион", "избран", "наслед", "повышен",
        "элит", "легенд", "звезд", "кандидат", "прорыв", "преем",
        "гильд", "авантюр", "геро", "маг", "волшеб", "чарод", "колдун", "академ", "школ", "мастер",
        "сталкер", "выжив", "мусорщик", "охотник", "следопыт",
        "капитан", "адмирал", "пилот", "учен", "исследоват", "ксенолог", "командир", "офицер",
        "журналист", "адвокат", "спортсмен", "знаменит", "инфлюенсер", "стартап", "основател", "молодой политик",
        "палад", "рыцар", "охотник на чудовищ", "молодой мастер", "кузнец-мастер", "алхимик", "архимаг",
        "супергер", "сверхчеловек", "вигилант", "одарен", "метачеловек", "кейс", "аномальн рожден", "академия героев",
        "детектив", "следоват", "профайлер", "инспектор", "сыщик", "медиум", "оккультн следоват", "экзорцист", "выживш"
    };

    private static readonly string[] HostileHuntKeywords =
    {
        "threat", "hunt", "war", "crisis", "shadow", "invasion", "under_threat", "assassin", "bounty", "manhunt",
        "pursuit", "kill team", "task force", "black ops", "predator", "tracker", "purge", "raid", "warrant",
        "raider", "wasteland", "scarcity", "water shortage", "fuel shortage", "ambush",
        "fleet", "interceptor", "drone swarm", "security force", "containment", "quarantine",
        "police", "detective", "fbi", "intelligence", "hitman", "gang", "serial killer", "stalker", "surveillance",
        "dragon", "demon", "lich", "undead", "monster", "beast", "curse",
        "anti-mutant", "rogue hero", "containment squad", "blacksite response", "power suppressor",
        "killer", "slasher", "stalker", "haunting", "possession", "missing persons", "abduction", "cover-up", "crime scene", "copycat",
        "угроз", "охот", "войн", "криз", "тень", "нашеств", "ассас", "награда", "розыск",
        "преслед", "ликвидац", "оперативн", "карат", "следопыт", "чистк", "рейд", "ордер",
        "рейдер", "пустош", "дефицит", "дефицит воды", "дефицит топлива", "засад",
        "флот", "перехват", "дрон", "силы безопас", "карантин", "изоляц",
        "полици", "детектив", "следоват", "спецслужб", "наемник", "банд", "маньяк", "сталкер", "слежк",
        "дракон", "демон", "лич", "нежит", "чудовищ", "звер", "проклят",
        "антимутант", "сорвавш герой", "отряд сдержив", "черный объект", "подавитель сил",
        "убийц", "слэшер", "одержим", "пропавш", "похищен", "заметает следы", "место преступ", "подражат"
    };

    private readonly FileSystemManager _fs;
    private readonly ILogger<RivalSoulArcService> _logger;

    public RivalSoulArcService(FileSystemManager fs, ILogger<RivalSoulArcService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public async Task ResetForNewLifeAsync()
    {
        if (!_fs.FileExists(StatePath))
            return;

        _logger.LogInformation("Сброс life-scoped rival soul arcs для новой смертной жизни.");
        _fs.DeleteFile(StatePath);
        await Task.CompletedTask;
    }

    public async Task<string?> BuildSystemReminderFragmentAsync(string? currentRealm, int currentTurnNumber)
    {
        if (!IsMortalRealm(currentRealm))
            return null;

        var activeArcs = await ReadActiveArcsAsync();
        if (activeArcs.Count > 0)
            return BuildActiveArcSummaryReminder(activeArcs);

        var activeSoulQuestCount = await CountActivePlayerSoulQuestsAsync();
        if (await IsInitialBootstrapContextAsync())
            return BuildInitialBootstrapReminder();

        if (await ShouldOfferOpportunityReminderAsync(currentTurnNumber, activeSoulQuestCount))
            return await BuildOpportunityReminderWithSeedsAsync(activeSoulQuestCount);

        return null;
    }

    private async Task<List<ActiveArcSummary>> ReadActiveArcsAsync()
    {
        var raw = await _fs.ReadFileAsync(StatePath);
        if (string.IsNullOrWhiteSpace(raw))
            return new List<ActiveArcSummary>();

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("arcs", out var arcs) ||
                arcs.ValueKind != JsonValueKind.Array)
            {
                return new List<ActiveArcSummary>();
            }

            var result = new List<ActiveArcSummary>();
            foreach (var arc in arcs.EnumerateArray())
            {
                if (arc.ValueKind != JsonValueKind.Object)
                    continue;

                var status = GetString(arc, "status");
                if (string.Equals(status, "resolved", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var scope = GetString(arc, "scope");
                var arcType = GetString(arc, "arcType");
                var objective = GetString(arc, "objective");
                var rivalSoulName = "";
                if (arc.TryGetProperty("rivalSoul", out var rivalSoul) && rivalSoul.ValueKind == JsonValueKind.Object)
                    rivalSoulName = GetString(rivalSoul, "displayNameOrMoniker");

                if (string.IsNullOrWhiteSpace(rivalSoulName) || string.IsNullOrWhiteSpace(objective))
                    continue;

                var targetsPlayerDirectly = false;
                var canBecomeSoulQuest = false;
                if (arc.TryGetProperty("playerIntersection", out var intersection) && intersection.ValueKind == JsonValueKind.Object)
                {
                    targetsPlayerDirectly =
                        intersection.TryGetProperty("targetsPlayerDirectly", out var targetsNode) &&
                        targetsNode.ValueKind == JsonValueKind.True;
                    canBecomeSoulQuest =
                        intersection.TryGetProperty("canBecomeSoulQuest", out var soulQuestNode) &&
                        soulQuestNode.ValueKind == JsonValueKind.True;
                }

                result.Add(new ActiveArcSummary(
                    rivalSoulName,
                    scope,
                    status,
                    arcType,
                    objective,
                    targetsPlayerDirectly,
                    canBecomeSoulQuest));
            }

            return result;
        }
        catch
        {
            return new List<ActiveArcSummary>();
        }
    }

    private string BuildInitialBootstrapReminder()
    {
        return "OPTIONAL RIVAL SOUL ARC: " +
               "You may introduce up to 1 major and 1 minor parallel destiny line for OTHER souls in this mortal life if it will enrich the world. " +
               "Use UpdateRivalSoulArcs for milestone-based background pressure, not a full second-protagonist simulation. " +
               "Good fits: hostile hunt, rival ascension, political claimant, artifact race, ideological mission.";
    }

    private string BuildOpportunityReminder()
    {
        return "RIVAL ARC OPPORTUNITY: " +
               "No rival soul arc is active in this life yet. " +
               "If dramatically appropriate, consider introducing one parallel destiny line through rumors, aftermath, public achievements, faction talk, or off-screen progress by another soul sponsored by a Guardian. " +
               "Keep it lightweight and milestone-based.";
    }

    private string BuildActiveArcSummaryReminder(IReadOnlyList<ActiveArcSummary> activeArcs)
    {
        var builder = new StringBuilder();
        builder.Append("ACTIVE RIVAL SOUL ARCS:");

        foreach (var arc in activeArcs)
        {
            builder.Append(" - ");
            builder.Append(arc.RivalSoulName);
            builder.Append(" [");
            builder.Append(arc.Scope);
            builder.Append(", ");
            builder.Append(arc.Status);
            builder.Append("] — objective: ");
            builder.Append(arc.Objective);
            builder.Append(". Next natural pressure: ");
            builder.Append(GetNextNaturalPressure(arc));
            builder.Append('.');
        }

        if (activeArcs.Any(arc => arc.CanBecomeSoulQuest && (arc.TargetsPlayerDirectly || IsEscalatedStatus(arc.Status))))
        {
            builder.Append(" PLAYER COUNTERPLAY REMINDER: If a rival arc becomes personally relevant to the player, surface that through an ordinary player-facing soul quest via UpdateSoulQuests and link it with relatedRivalArcId. Do NOT turn rival_soul_arcs.json into a player quest journal.");
        }

        return builder.ToString();
    }

    private async Task<bool> IsInitialBootstrapContextAsync()
    {
        var hasWorldSetting = await HasMeaningfulFileAsync(WorldSettingPath);
        var hasCurrentLocation = await HasMeaningfulFileAsync(CurrentLocationPath);
        var hasPlotOutline = await HasMeaningfulFileAsync(PlotOutlinePath);
        var hasWorldEvents = await HasMeaningfulFileAsync(WorldEventsPath);

        return !hasWorldSetting && !hasCurrentLocation && !hasPlotOutline && !hasWorldEvents;
    }

    private async Task<bool> ShouldOfferOpportunityReminderAsync(int currentTurnNumber, int activeSoulQuestCount)
    {
        if (currentTurnNumber < 4 || currentTurnNumber % 4 != 0)
            return false;

        if (!await WorldFeelsMatureEnoughAsync())
            return false;

        if (activeSoulQuestCount >= 6)
            return false;

        return true;
    }

    private async Task<string> BuildOpportunityReminderWithSeedsAsync(int activeSoulQuestCount)
    {
        var reminder = new StringBuilder(BuildOpportunityReminder());
        var eventHook = await BuildWorldEventHookAsync(activeSoulQuestCount);
        if (!string.IsNullOrWhiteSpace(eventHook))
        {
            reminder.Append(' ');
            reminder.Append(eventHook);
        }

        var seeds = await BuildSeedSuggestionsAsync();
        if (seeds.Count > 0)
        {
            reminder.Append(" Suggested rival arc seeds: ");
            reminder.Append(string.Join(" ", seeds.Select((seed, index) => $"{index + 1}) {seed}")));
        }

        return reminder.ToString();
    }

    private async Task<string?> BuildWorldEventHookAsync(int activeSoulQuestCount)
    {
        if (activeSoulQuestCount > 0)
            return null;

        var worldEventHeadline = await TryReadLatestWorldEventHeadlineAsync();
        if (!string.IsNullOrWhiteSpace(worldEventHeadline))
        {
            return $"WORLD EVENT HOOK: Think whether the current world or faction news item \"{worldEventHeadline}\" can serve as the first visible pressure point for a rival soul arc through rumor, aftermath, faction reaction, investigation, or off-screen progress by another soul.";
        }

        if (await CountArrayItemsAsync(FactionCorePath, "factionDataChanges", "factions") > 0)
        {
            return "WORLD EVENT HOOK: No soul quests are active yet. Consider whether the current faction motion, rumor, or public development can introduce a rival soul arc through pressure, backlash, investigation, or off-screen progress by another soul.";
        }

        return null;
    }

    private async Task<bool> WorldFeelsMatureEnoughAsync()
    {
        if (await HasMeaningfulFileAsync(WorldSettingPath))
            return true;

        if (await HasMeaningfulFileAsync(PlotOutlinePath))
            return true;

        if (await CountArrayItemsAsync(WorldEventsPath, "worldEventsLog") >= 2)
            return true;

        if (await CountArrayItemsAsync(NpcCorePath, GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections) >= 1)
            return true;

        if (await CountArrayItemsAsync(FactionCorePath, "factionDataChanges", "factions") >= 1)
            return true;

        return false;
    }

    private async Task<List<string>> BuildSeedSuggestionsAsync()
    {
        var contextText = await BuildWorldContextTextAsync();
        if (string.IsNullOrWhiteSpace(contextText))
            return new List<string>();

        var lower = contextText.ToLowerInvariant();
        var worldName = await TryReadWorldNameAsync();
        var locationName = await TryReadLocationNameAsync();
        var stageName = !string.IsNullOrWhiteSpace(locationName) ? locationName : (!string.IsNullOrWhiteSpace(worldName) ? worldName : "этот мир");

        var seeds = new List<string>();
        if (ContainsAny(lower, RivalAscensionKeywords))
        {
            seeds.Add($"rival_ascension — a fast-rising rival soul becomes the talk of {stageName}; first pressure: public victory, elite invitation, faction sponsorship.");
        }

        if (ContainsAny(lower, PoliticalClaimKeywords))
        {
            seeds.Add($"political_claim — другая душа под покровительством rival Guardian quietly climbs toward power around {stageName}; first pressure: court rumor, noble endorsement, succession whisper.");
        }

        if (ContainsAny(lower, IdeologicalMissionKeywords))
        {
            seeds.Add($"ideological_mission — чужая душа spreads a rival creed through {stageName}; first pressure: converts, pamphlets, shrine growth, moral panic.");
        }

        if (ContainsAny(lower, ArtifactRaceKeywords))
        {
            seeds.Add($"artifact_race — another soul races the player toward the same relic or sealed site near {stageName}; first pressure: stolen map, excavation rumor, broken ward.");
        }

        if (ContainsAny(lower, HostileHuntKeywords))
        {
            seeds.Add($"hostile_hunt — a hostile Guardian backs another soul to track or pressure the player through {stageName}; first pressure: rumor, aftermath, bounty notice, failed ambush.");
        }

        if (seeds.Count == 0)
        {
            seeds.Add($"rival_ascension — another soul rises in prominence somewhere beyond {stageName}; first pressure: rumor, public feat, faction curiosity.");
            seeds.Add($"artifact_race — whispers spread of another seeker chasing the same hidden prize; first pressure: broken seal, missing map, rival trail.");
        }

        return seeds.Take(2).ToList();
    }

    private async Task<string> BuildWorldContextTextAsync()
    {
        var parts = new List<string>();
        foreach (var path in new[] { WorldSettingPath, PlotOutlinePath, FactionCorePath, CurrentLocationPath, WorldEventsPath })
        {
            var raw = await _fs.ReadFileAsync(path);
            if (!string.IsNullOrWhiteSpace(raw))
                parts.Add(raw);
        }

        return string.Join("\n", parts);
    }

    private async Task<string> TryReadWorldNameAsync()
    {
        var raw = await _fs.ReadFileAsync(WorldSettingPath);
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("setting", out var setting) &&
                setting.ValueKind == JsonValueKind.Object)
            {
                return GetString(setting, "name");
            }
        }
        catch
        {
            // ignored
        }

        return string.Empty;
    }

    private async Task<string> TryReadLocationNameAsync()
    {
        var raw = await _fs.ReadFileAsync(CurrentLocationPath);
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                return GetString(doc.RootElement, "name");
        }
        catch
        {
            // ignored
        }

        return string.Empty;
    }

    private async Task<string> TryReadLatestWorldEventHeadlineAsync()
    {
        var raw = await _fs.ReadFileAsync(WorldEventsPath);
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            JsonElement events;
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("worldEventsLog", out events) &&
                events.ValueKind == JsonValueKind.Array &&
                events.GetArrayLength() > 0)
            {
                return GetEventHeadline(events[events.GetArrayLength() - 1]);
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                return GetEventHeadline(doc.RootElement[doc.RootElement.GetArrayLength() - 1]);
        }
        catch
        {
            // ignored
        }

        return string.Empty;
    }

    private async Task<int> CountActivePlayerSoulQuestsAsync()
    {
        var raw = await _fs.ReadFileAsync(SoulQuestsPath);
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            JsonElement quests;
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("quests", out quests) &&
                quests.ValueKind == JsonValueKind.Array)
            {
                return CountActiveSoulQuestStatuses(quests);
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("UpdateSoulQuests", out quests) &&
                quests.ValueKind == JsonValueKind.Array)
            {
                return CountActiveSoulQuestStatuses(quests);
            }

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    private static int CountActiveSoulQuestStatuses(JsonElement quests)
    {
        var count = 0;
        foreach (var quest in quests.EnumerateArray())
        {
            if (quest.ValueKind != JsonValueKind.Object)
                continue;

            var status = GetString(quest, "status");
            if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
                count++;
        }

        return count;
    }

    private async Task<bool> HasMeaningfulFileAsync(string relativePath)
    {
        var raw = await _fs.ReadFileAsync(relativePath);
        return !string.IsNullOrWhiteSpace(raw);
    }

    private async Task<int> CountArrayItemsAsync(string relativePath, params string[] propertyNames)
    {
        var raw = await _fs.ReadFileAsync(relativePath);
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                return doc.RootElement.GetArrayLength();

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return 0;

            foreach (var propertyName in propertyNames)
            {
                if (!doc.RootElement.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.Array)
                    continue;

                return node.GetArrayLength();
            }

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    private static bool IsEscalatedStatus(string status) =>
        string.Equals(status, "rising", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "intersecting", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string source, params string[] needles) =>
        needles.Any(needle => source.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private static string GetEventHeadline(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return string.Empty;

        foreach (var key in new[] { "title", "eventTitle", "headline", "name", "summary", "description" })
        {
            var value = GetString(item, key).Trim();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static string GetNextNaturalPressure(ActiveArcSummary arc)
    {
        var status = arc.Status.ToLowerInvariant();
        return arc.ArcType.ToLowerInvariant() switch
        {
            "hostile_hunt" => status switch
            {
                "latent" => "rumor, bounty whisper, unseen scouting",
                "rising" => "rumor, aftermath, bounty notice, failed ambush",
                _ => "ambush, pursuit, public fallout"
            },
            "rival_ascension" => status switch
            {
                "latent" => "public achievement, sect rumor, tournament notice, guild buzz",
                "rising" => "tournament victory, faction interest, elite invitation, guild or academy sponsorship",
                _ => "direct rivalry, political pressure, succession crisis, champion challenge"
            },
            "political_claim" => status switch
            {
                "latent" => "court rumor, noble letter, local endorsement",
                "rising" => "alliance offer, decree, faction maneuver",
                _ => "open power struggle, purge, coup attempt"
            },
            "artifact_race" => status switch
            {
                "latent" => "excavation rumor, stolen map, broken seal",
                "rising" => "rival expedition, missing relic trail, trap aftermath",
                _ => "open race, sabotage, contested relic site"
            },
            "ideological_mission" => status switch
            {
                "latent" => "quiet converts, pamphlets, whispered doctrine",
                "rising" => "public speech, shrine growth, doctrine conflict",
                _ => "sect clash, purge, mass conversion pressure"
            },
            _ => "rumor, aftermath, off-screen progress, faction reaction"
        };
    }

    private static bool IsMortalRealm(string? realm) =>
        !string.IsNullOrWhiteSpace(realm) && !IsAfterlifeRealm(realm);

    private static bool IsAfterlifeRealm(string? realm) =>
        string.Equals(realm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);

    private static string GetString(JsonElement obj, string propName)
    {
        if (!obj.TryGetProperty(propName, out var prop) || prop.ValueKind != JsonValueKind.String)
            return string.Empty;

        return prop.GetString() ?? string.Empty;
    }

    private sealed record ActiveArcSummary(
        string RivalSoulName,
        string Scope,
        string Status,
        string ArcType,
        string Objective,
        bool TargetsPlayerDirectly,
        bool CanBecomeSoulQuest);
}
