using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.UI;

public static partial class ExplorerUniversalMetaCommandResultBuilder
{
    private enum CommandKind
    {
        Status,
        Soul,
        SoulRelics,
        AfterlifeArchive,
        ArchiveCandidates,
        SoulQuests,
        Codex,
        Achievements,
        Chronicle,
        Story,
        Behavior,
        Lives,
        Feathers,
        WorldRules,
        Gallery,
        Gm,
        Debug,
        Mods,
        SystemGuardians,
        SarefStory,
        SarefFindWings,
        SarefUseAdvantage,
        SarefFinalConfrontation,
        SarefOathBreak,
        SarefAgenda,
        MemoryScene
    }

    private static readonly IReadOnlyDictionary<string, CommandKind> CommandKinds =
        new Dictionary<string, CommandKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["/status"] = CommandKind.Status,
            ["/статус"] = CommandKind.Status,
            ["/soul"] = CommandKind.Soul,
            ["/душа"] = CommandKind.Soul,
            ["/soul_relics"] = CommandKind.SoulRelics,
            ["/реликвии"] = CommandKind.SoulRelics,
            ["/afterlife_archive"] = CommandKind.AfterlifeArchive,
            ["/архив_души"] = CommandKind.AfterlifeArchive,
            ["/archive_candidates"] = CommandKind.ArchiveCandidates,
            ["/архив_кандидаты"] = CommandKind.ArchiveCandidates,
            ["/soul_quests"] = CommandKind.SoulQuests,
            ["/квесты_души"] = CommandKind.SoulQuests,
            ["/codex"] = CommandKind.Codex,
            ["/кодекс"] = CommandKind.Codex,
            ["/achievements"] = CommandKind.Achievements,
            ["/достижения"] = CommandKind.Achievements,
            ["/chronicle"] = CommandKind.Chronicle,
            ["/хроника"] = CommandKind.Chronicle,
            ["/story"] = CommandKind.Story,
            ["/рассказ"] = CommandKind.Story,
            ["/история"] = CommandKind.Story,
            ["/behavior"] = CommandKind.Behavior,
            ["/поведение"] = CommandKind.Behavior,
            ["/lives"] = CommandKind.Lives,
            ["/жизни"] = CommandKind.Lives,
            ["/feathers"] = CommandKind.Feathers,
            ["/перья"] = CommandKind.Feathers,
            ["/world_rules"] = CommandKind.WorldRules,
            ["/правила_мира"] = CommandKind.WorldRules,
            ["/gallery"] = CommandKind.Gallery,
            ["/галерея"] = CommandKind.Gallery,
            ["/gm"] = CommandKind.Gm,
            ["/гм"] = CommandKind.Gm,
            ["/debug"] = CommandKind.Debug,
            ["/отладка"] = CommandKind.Debug,
            ["/mods"] = CommandKind.Mods,
            ["/моды"] = CommandKind.Mods,
            ["/system_guardians"] = CommandKind.SystemGuardians,
            ["/системные_хранители"] = CommandKind.SystemGuardians,
            ["/извечные_хранители"] = CommandKind.SystemGuardians,
            ["/saref"] = CommandKind.SarefStory,
            ["/сареф"] = CommandKind.SarefStory,
            ["/saref_story"] = CommandKind.SarefStory,
            ["/история_сарефа"] = CommandKind.SarefStory,
            ["/wings_of_angels"] = CommandKind.SarefStory,
            ["/крылья_над_бездной"] = CommandKind.SarefStory,
            ["/сареф найти_крылья"] = CommandKind.SarefFindWings,
            ["/saref find_wings"] = CommandKind.SarefFindWings,
            ["/сареф преимущество"] = CommandKind.SarefUseAdvantage,
            ["/saref use_advantage"] = CommandKind.SarefUseAdvantage,
            ["/сареф конфронтация"] = CommandKind.SarefFinalConfrontation,
            ["/saref confrontation"] = CommandKind.SarefFinalConfrontation,
            ["/сареф разорвать_клятву"] = CommandKind.SarefOathBreak,
            ["/saref break_oath"] = CommandKind.SarefOathBreak,
            ["/сареф поручение"] = CommandKind.SarefAgenda,
            ["/saref agenda"] = CommandKind.SarefAgenda,
            ["/воспоминание"] = CommandKind.MemoryScene,
            ["/воспоминание_статус"] = CommandKind.MemoryScene,
            ["/воспоминание_начать"] = CommandKind.MemoryScene,
            ["/воспоминание_способности"] = CommandKind.MemoryScene
        };

    public static bool CanBuild(string command)
    {
        var normalizedCommand = command.Trim();
        var commandToken = ExplorerCommandCatalog.ExtractCommandToken(normalizedCommand);
        return CommandKinds.ContainsKey(normalizedCommand) || CommandKinds.ContainsKey(commandToken);
    }

    public static async Task<ExplorerCommandResult?> TryBuildAsync(
        string command,
        StateManager stateManager,
        FileSystemManager fs,
        LocalizationManager loc)
    {
        var normalizedCommand = command.Trim();
        var commandToken = ExplorerCommandCatalog.ExtractCommandToken(normalizedCommand);
        if (!CommandKinds.TryGetValue(normalizedCommand, out var kind) &&
            !CommandKinds.TryGetValue(commandToken, out kind))
        {
            return null;
        }

        await stateManager.RefreshGameStateAsync();

        return kind switch
        {
            CommandKind.Status => await BuildStatus(normalizedCommand, stateManager, fs),
            CommandKind.Soul => await BuildSoul(normalizedCommand, fs),
            CommandKind.SoulRelics => await BuildSoulRelics(normalizedCommand, fs),
            CommandKind.AfterlifeArchive => await BuildAfterlifeArchive(normalizedCommand, fs),
            CommandKind.ArchiveCandidates => await BuildArchiveCandidates(normalizedCommand, fs),
            CommandKind.SoulQuests => await BuildSoulQuests(normalizedCommand, fs),
            CommandKind.Codex => await BuildJsonFile(normalizedCommand, fs, "Кодекс", "lore/codex_entries.json", BuildCodexSummary),
            CommandKind.Achievements => await BuildJsonFile(normalizedCommand, fs, "Достижения", "game_state/meta/achievements.json", BuildAchievementsSummary),
            CommandKind.Chronicle => await BuildChronicle(normalizedCommand, fs),
            CommandKind.Story => await BuildStory(normalizedCommand, fs),
            CommandKind.Behavior => await BuildJsonFile(normalizedCommand, fs, "Поведение игрока", "game_state/meta/player_behavior.json"),
            CommandKind.Lives => await BuildSoulSection(normalizedCommand, fs, "История жизней", "livesHistory"),
            CommandKind.Feathers => await BuildInkFeathers(normalizedCommand, fs, stateManager),
            CommandKind.WorldRules => await BuildJsonFile(normalizedCommand, fs, "Досье текущего мира", WorldDirectiveService.ActiveDirectivesPath),
            CommandKind.Gallery => BuildGallery(normalizedCommand, fs),
            CommandKind.Gm => await BuildGmThoughts(normalizedCommand, fs, loc),
            CommandKind.Debug => BuildDebug(normalizedCommand, fs, stateManager, loc),
            CommandKind.Mods => BuildDirectoryList(normalizedCommand, fs, "Моды", "mods"),
            CommandKind.SystemGuardians => await BuildSystemGuardians(normalizedCommand, fs),
            CommandKind.SarefStory => await BuildSarefStory(normalizedCommand, fs),
            CommandKind.SarefFindWings => await BuildSarefFindWings(normalizedCommand, stateManager, fs),
            CommandKind.SarefUseAdvantage => await BuildSarefUseAdvantage(normalizedCommand, fs),
            CommandKind.SarefFinalConfrontation => await BuildSarefFinalConfrontation(normalizedCommand, fs),
            CommandKind.SarefOathBreak => await BuildSarefOathBreak(normalizedCommand, fs),
            CommandKind.SarefAgenda => await BuildSarefAgenda(normalizedCommand, fs),
            CommandKind.MemoryScene => await BuildSarefMemoryScene(normalizedCommand, fs),
            _ => null
        };
    }

    private static async Task<ExplorerCommandResult> BuildStatus(
        string command,
        StateManager stateManager,
        FileSystemManager fs)
    {
        var state = stateManager.CurrentState;
        var blocks = new List<UiBlock>
        {
            Panel("Статус",
                Grid(
                    ("Царство", ExplorerPlayerFacingLabels.Realm(state.CurrentRealm)),
                    ("Душа", EmptyFallback(state.SoulName)),
                    ("Форма души", EmptyFallback(state.SoulFormDescription)),
                    ("Инкарнация", state.Incarnation.ToString()),
                    ("Персонаж", EmptyFallback(state.CharacterName)),
                    ("Класс / раса", JoinNonEmpty(" / ", state.CharacterClass, state.CharacterRace)),
                    ("Локация", EmptyFallback(state.CurrentLocation)),
                    ("Время мира", EmptyFallback(ExplorerPlayerFacingLabels.WorldTime(state.WorldTime))),
                    ("Состояние", EmptyFallback(state.PlayerStatus.CurrentCondition)),
                    ("Здоровье", EmptyFallback(state.PlayerStatus.HealthPercentage)),
                    ("Энергия", EmptyFallback(state.PlayerStatus.EnergyPercentage)),
                    ("Равновесие", EmptyFallback(state.PlayerStatus.PoisePercentage)),
                    ("Чернильные Перья", state.InkFeathers.ToString()),
                    ("Просветление", EmptyFallback(state.EnlightenmentTier)),
                    ("Активный Хранитель", EmptyFallback(state.ActiveGuardianName)),
                    ("Сияние", $"{state.ShiningRadianceExperience} XP / тир {state.ShiningRadianceTier}"),
                    ("Искры Света", state.ShiningLightSparks.ToString())))
        };

        var actions = Enumerable.Empty<UiAction>();
        if (!state.IsInAfterlifeRealm)
            actions = await AddMortalStatusDetailBlocks(blocks, fs, state.PlayerStatus.ActiveConditions);

        return Completed(command, blocks, actions);
    }

    private static async Task<IReadOnlyList<UiAction>> AddMortalStatusDetailBlocks(
        List<UiBlock> blocks,
        FileSystemManager fs,
        IReadOnlyList<string> activeConditions)
    {
        var statusRead = await ReadJson(fs, "game_state/core/player_status.json");
        var experienceRead = await ReadJson(fs, "game_state/player/experience.json");
        var weightRead = await ReadJson(fs, "game_state/player/weight_calc.json");
        var inventoryRead = await ReadJson(fs, "game_state/inventory/items.json");
        var stealthRead = await ReadJson(fs, "game_state/player/stealth.json");
        var statusChangesRead = await ReadJson(fs, "game_state/player/status_changes.json");
        var effectsRead = await ReadJson(fs, "game_state/player/effects.json");
        var woundsRead = await ReadJson(fs, "game_state/player/wounds.json");
        var customStatesRead = await ReadJson(fs, "game_state/player/custom_states.json");

        var status = UnwrapObject(statusRead.Node, "playerStatus");
        var experience = UnwrapObject(experienceRead.Node, "experience", "playerExperience");
        var weight = UnwrapObject(weightRead.Node, "weight", "weightState", "weightCalc");
        var inventory = UnwrapObject(inventoryRead.Node, "inventory");
        var stealth = UnwrapObject(stealthRead.Node, "stealth", "stealthState");
        var statusChanges = UnwrapObject(statusChangesRead.Node, "statusChanges", "changes");

        var progressRows = BuildMortalStatusProgressRows(experience);
        if (progressRows.Count > 0)
            blocks.Add(Panel("Прогресс", new UiKeyValueGridBlock { Items = progressRows }));

        var resourceRows = BuildMortalStatusResourceRows(status, inventory, weight);
        if (resourceRows.Count > 0)
            blocks.Add(Panel("Ресурсы и нагрузка", new UiKeyValueGridBlock { Items = resourceRows }));

        if (stealth != null)
        {
            var stealthRows = BuildMortalStatusStealthRows(stealth);
            if (stealthRows.Count > 0)
                blocks.Add(Panel("Скрытность", new UiKeyValueGridBlock { Items = stealthRows }));
        }

        if (activeConditions.Count > 0)
        {
            blocks.Add(Panel("Активные состояния",
                new UiListBlock
                {
                    Items = activeConditions
                        .Where(static condition => !string.IsNullOrWhiteSpace(condition))
                        .Select(static condition => condition.Trim())
                        .ToList()
                }));
        }

        var changeRows = BuildMortalStatusChangeRows(statusChanges, experience);
        if (changeRows.Count > 0)
            AddStatusRowsDossier(
                blocks,
                "Последние изменения",
                "status-changes",
                "status-change",
                "Что изменилось за последний ход.",
                "status",
                ["Параметр", "Изменение", "Комментарий"],
                changeRows);

        AddMortalStatusEffectBlocks(blocks, effectsRead.Node);
        AddMortalStatusWoundBlocks(blocks, woundsRead.Node);
        AddMortalStatusCustomStateBlocks(blocks, customStatesRead.Node);

        return ExplorerMortalEffectDetailActions.Build("/эффекты", effectsRead.Node);
    }

    private static List<UiKeyValueItem> BuildMortalStatusProgressRows(JsonObject? experience)
    {
        var rows = new List<UiKeyValueItem>();
        if (experience == null)
            return rows;

        AddStatusRow(rows, "Уровень", GetOptionalString(experience, "level"));

        var totalExperience = GetOptionalString(experience, "totalExperience");
        var nextLevel = FirstNonEmpty(
            GetOptionalString(experience, "experienceForNextLevel"),
            GetOptionalString(experience, "nextLevelExperience"));
        if (!string.IsNullOrWhiteSpace(totalExperience) || !string.IsNullOrWhiteSpace(nextLevel))
        {
            AddStatusRow(
                rows,
                "Опыт",
                string.IsNullOrWhiteSpace(nextLevel)
                    ? totalExperience
                    : $"{EmptyFallback(totalExperience)}/{nextLevel}");
        }

        var gained = GetIntValue(experience["experienceGained"], 0);
        if (gained != 0)
            AddStatusRow(rows, "Опыт за последний ход", FormatSigned(gained));

        if (experience["playerEffortTrackerChange"] is JsonObject tracker)
        {
            var lastCharacteristic = TranslateCharacteristic(GetOptionalString(tracker, "lastUsedCharacteristic"));
            var partialSuccesses = GetOptionalString(tracker, "consecutivePartialSuccesses");
            var trackerText = JoinKnownParts(
                " / ",
                IsUnknownValue(lastCharacteristic) ? string.Empty : $"последняя: {lastCharacteristic}",
                string.IsNullOrWhiteSpace(partialSuccesses) ? string.Empty : $"частичных успехов: {partialSuccesses}/3");
            if (!IsUnknownValue(trackerText))
                AddStatusRow(rows, "Трекер усилий", trackerText);
        }

        return rows;
    }

    private static List<UiKeyValueItem> BuildMortalStatusResourceRows(
        JsonObject? status,
        JsonObject? inventory,
        JsonObject? weight)
    {
        var rows = new List<UiKeyValueItem>();

        var money = GetIntValue(status?["money"], 0);
        if (money == 0 && inventory != null)
        {
            money = GetIntValue(inventory["money"], 0);
            if (money == 0 && inventory["resources"] is JsonObject resources)
                money = FirstNonZero(
                    GetIntValue(resources["gold"], 0),
                    GetIntValue(resources["money"], 0),
                    GetIntValue(resources["coins"], 0));
        }

        if (money > 0)
            AddStatusRow(rows, "Деньги", money.ToString(CultureInfo.InvariantCulture));

        var totalWeight = GetIntValue(weight?["totalWeight"], 0);
        if (totalWeight == 0)
            totalWeight = GetIntValue(weight?["currentWeight"], 0);
        var maxWeight = FirstNonZero(
            GetIntValue(weight?["maxWeight"], 0),
            GetIntValue(weight?["maximumWeight"], 0));
        if (totalWeight == 0 && inventory != null)
            totalWeight = GetIntValue(inventory["totalWeight"], 0);
        if (maxWeight == 0 && inventory != null)
            maxWeight = GetIntValue(inventory["maxWeight"], 0);

        if (maxWeight > 0)
        {
            var overload = IsTrue(weight?["isOverloaded"]) || IsTrue(weight?["overloaded"]) ||
                           (inventory != null && IsTrue(inventory["isOverloaded"]));
            AddStatusRow(rows, "Вес", $"{totalWeight}/{maxWeight} кг{(overload ? " (перегрузка)" : string.Empty)}");
        }

        var extraEnergy = GetIntValue(weight?["additionalEnergyExpenditure"], 0);
        if (extraEnergy > 0)
            AddStatusRow(rows, "Доп. расход энергии", $"+{extraEnergy}/ход");

        return rows;
    }

    private static List<UiKeyValueItem> BuildMortalStatusStealthRows(JsonObject stealth)
    {
        var rows = new List<UiKeyValueItem>();
        var isActive = IsTrue(stealth["isActive"]) || IsTrue(stealth["isHidden"]);
        var detectionLevel = GetIntValue(stealth["detectionLevel"], -1);
        var description = FirstNonEmpty(GetOptionalString(stealth, "description"), GetOptionalString(stealth, "state"));

        if (detectionLevel >= 0)
            AddStatusRow(rows, "Состояние", $"{DescribeDetectionLevel(detectionLevel)} ({detectionLevel}%)");
        else if (isActive)
            AddStatusRow(rows, "Состояние", "Скрыт");

        AddStatusRow(rows, "Описание", description);
        return rows;
    }

    private static List<UiTableRow> BuildMortalStatusChangeRows(JsonObject? statusChanges, JsonObject? experience)
    {
        var rows = new List<UiTableRow>();
        if (statusChanges != null)
        {
            AddSignedChangeRow(rows, "Деньги", GetIntValue(statusChanges["moneyChange"], 0));
            AddSignedChangeRow(rows, "Здоровье", GetIntValue(statusChanges["currentHealthChange"], 0));
            AddSignedChangeRow(rows, "Энергия", GetIntValue(statusChanges["currentEnergyChange"], 0));
            AddSignedChangeRow(rows, "Равновесие", GetIntValue(statusChanges["currentPoiseChange"], 0));

            var statsIncreased = FormatCharacteristicList(statusChanges["statsIncreased"]);
            if (!IsUnknownValue(statsIncreased))
                rows.Add(Row("Повышены", statsIncreased, "характеристики"));

            var statsDecreased = FormatCharacteristicList(statusChanges["statsDecreased"]);
            if (!IsUnknownValue(statsDecreased))
                rows.Add(Row("Понижены", statsDecreased, "характеристики"));
        }

        var gained = GetIntValue(experience?["experienceGained"], 0);
        if (gained != 0)
            rows.Add(Row("Опыт", FormatSigned(gained), "за последний ход"));

        return rows;
    }

    private static void AddMortalStatusEffectBlocks(List<UiBlock> blocks, JsonNode? effectsRoot)
    {
        var rows = EnumerateStatusObjects(effectsRoot)
            .Select(effect =>
            {
                var name = FirstKnown(
                    GetString(effect, "effectName", string.Empty),
                    GetString(effect, "name", string.Empty),
                    TranslateEffectType(GetString(effect, "effectType", string.Empty)));
                var type = TranslateEffectType(GetString(effect, "effectType", string.Empty));
                var value = GetOptionalString(effect, "value");
                var target = FirstNonEmpty(
                    GetOptionalString(effect, "targetTypeDisplayName"),
                    TranslateCharacteristic(GetOptionalString(effect, "targetType")));
                var duration = GetOptionalString(effect, "duration");
                var source = FirstNonEmpty(GetOptionalString(effect, "sourceSkill"), GetOptionalString(effect, "source"));
                var description = FirstNonEmpty(
                    GetOptionalString(effect, "effectDescription"),
                    GetOptionalString(effect, "description"));
                return Row(
                    name,
                    JoinKnownParts(" ", type, value),
                    JoinKnownParts(" / ",
                        target,
                        string.IsNullOrWhiteSpace(duration) || duration == "0" ? string.Empty : $"{duration} ход."),
                    JoinKnownParts(" — ", source, description));
            })
            .Where(static row => row.Cells.Any(static cell => !IsUnknownValue(cell)))
            .ToList();

        if (rows.Count == 0)
            return;

        AddStatusRowsDossier(
            blocks,
            "Активные эффекты",
            "status-effects",
            "status-effect",
            "Эффекты, которые сейчас влияют на персонажа.",
            "effect",
            ["Эффект", "Что делает", "Цель / срок", "Источник и описание"],
            rows);
    }

    private static void AddMortalStatusWoundBlocks(List<UiBlock> blocks, JsonNode? woundsRoot)
    {
        var rows = EnumerateStatusObjects(woundsRoot)
            .Select(wound =>
            {
                var healing = wound["healingState"] is JsonObject healingState
                    ? JoinKnownParts(
                        " ",
                        GetOptionalString(healingState, "currentState"),
                        BuildProgressText(
                            GetOptionalString(healingState, "treatmentProgress"),
                            GetOptionalString(healingState, "progressNeeded")))
                    : string.Empty;
                return Row(
                    FirstKnown(GetString(wound, "woundName", string.Empty), GetString(wound, "name", string.Empty), "Рана"),
                    TranslateWoundSeverity(GetOptionalString(wound, "severity")),
                    FirstNonEmpty(GetOptionalString(wound, "descriptionOfEffects"), GetOptionalString(wound, "description")),
                    healing);
            })
            .Where(static row => row.Cells.Any(static cell => !IsUnknownValue(cell)))
            .ToList();

        if (rows.Count == 0)
            return;

        AddStatusRowsDossier(
            blocks,
            "Раны",
            "status-wounds",
            "status-wound",
            "Повреждения и их текущее лечение.",
            "effect",
            ["Рана", "Тяжесть", "Влияние", "Лечение"],
            rows);
    }

    private static void AddMortalStatusCustomStateBlocks(List<UiBlock> blocks, JsonNode? statesRoot)
    {
        var rows = EnumerateStatusObjects(statesRoot)
            .Select(state =>
            {
                var current = GetOptionalString(state, "currentValue");
                var max = GetOptionalString(state, "maxValue");
                var min = GetOptionalString(state, "minValue");
                var value = JoinKnownParts(
                    " ",
                    BuildProgressText(current, max),
                    string.IsNullOrWhiteSpace(min) ? string.Empty : $"мин. {min}");
                var progression = state["progressionRule"] is JsonObject rule
                    ? JoinKnownParts(" — ", GetOptionalString(rule, "changePerTurn"), GetOptionalString(rule, "description"))
                    : string.Empty;
                return Row(
                    FirstKnown(
                        GetString(state, "stateName", string.Empty),
                        GetString(state, "name", string.Empty),
                        GetString(state, "stateKey", string.Empty)),
                    FirstNonEmpty(value, GetOptionalString(state, "stateValue"), GetOptionalString(state, "value")),
                    JoinKnownParts(" — ", GetOptionalString(state, "description"), progression));
            })
            .Where(static row => row.Cells.Any(static cell => !IsUnknownValue(cell)))
            .ToList();

        if (rows.Count == 0)
            return;

        AddStatusRowsDossier(
            blocks,
            "Особые состояния",
            "status-custom-states",
            "status-custom-state",
            "Дополнительные шкалы и состояния персонажа.",
            "status",
            ["Состояние", "Значение", "Подробно"],
            rows);
    }

    private static void AddStatusRowsDossier(
        List<UiBlock> blocks,
        string title,
        string entityType,
        string itemEntityType,
        string summary,
        string icon,
        IReadOnlyList<string> columns,
        IReadOnlyList<UiTableRow> rows)
    {
        if (rows.Count == 0)
            return;

        blocks.Add(new UiEntityDossierBlock
        {
            EntityType = entityType,
            Title = title,
            Subtitle = "Статус персонажа",
            Summary = summary,
            Badges =
            [
                new UiEntityBadge
                {
                    Label = FormatStatusEntryCount(rows.Count),
                    Tone = UiTone.Accent,
                    Icon = icon
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = StableStatusId(title),
                    Title = title,
                    Icon = icon,
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = rows.Select(row => (UiBlock)BuildStatusRowCard(itemEntityType, icon, columns, row)).ToList()
                }
            ]
        });
    }

    private static UiEntityDossierBlock BuildStatusRowCard(
        string entityType,
        string icon,
        IReadOnlyList<string> columns,
        UiTableRow row)
    {
        var title = row.Cells.Count > 0 ? EmptyFallback(row.Cells[0]) : "Запись";
        var items = new List<UiKeyValueItem>();
        for (var index = 1; index < row.Cells.Count && index < columns.Count; index++)
        {
            var value = row.Cells[index];
            if (!IsUnknownValue(value))
                items.Add(new UiKeyValueItem { Key = columns[index], Value = value.Trim() });
        }

        return new UiEntityDossierBlock
        {
            EntityType = entityType,
            Title = title,
            Subtitle = columns.Count > 0 ? columns[0] : "Запись",
            Summary = FirstNonEmpty(row.Cells.Skip(1).Where(static cell => !IsUnknownValue(cell)).ToArray()),
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "details",
                    Title = "Подробности",
                    Icon = icon,
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = items.Count > 0
                        ? [new UiKeyValueGridBlock { Items = items }]
                        : [new UiTextBlock { Text = "Подробности пока не указаны.", Tone = UiTone.Muted }]
                }
            ]
        };
    }

    private static string FormatStatusEntryCount(int count)
    {
        var mod100 = count % 100;
        var mod10 = count % 10;
        var word = mod100 is >= 11 and <= 14
            ? "записей"
            : mod10 switch
            {
                1 => "запись",
                >= 2 and <= 4 => "записи",
                _ => "записей"
            };
        return $"{count} {word}";
    }

    private static string StableStatusId(string value)
    {
        var chars = value
            .Trim()
            .Select(static ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')
            .ToArray();
        var id = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(id) ? "status" : id;
    }

    private static JsonObject? UnwrapObject(JsonNode? node, params string[] wrapperProperties)
    {
        if (node is not JsonObject root)
            return null;

        foreach (var property in wrapperProperties)
        {
            if (root[property] is JsonObject wrapped)
                return wrapped;
        }

        return root;
    }

    private static IEnumerable<JsonObject> EnumerateStatusObjects(JsonNode? root)
    {
        if (root is JsonArray array)
        {
            foreach (var item in array.OfType<JsonObject>())
                yield return item;
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        var yieldedFromArrays = false;
        foreach (var property in obj)
        {
            if (property.Value is not JsonArray childArray)
                continue;

            yieldedFromArrays = true;
            foreach (var item in childArray.OfType<JsonObject>())
                yield return item;
        }

        if (!yieldedFromArrays)
            yield return obj;
    }

    private static void AddStatusRow(List<UiKeyValueItem> rows, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || IsUnknownValue(value))
            return;

        rows.Add(new UiKeyValueItem { Key = key, Value = value.Trim() });
    }

    private static void AddSignedChangeRow(List<UiTableRow> rows, string label, int value)
    {
        if (value == 0)
            return;

        rows.Add(Row(label, FormatSigned(value), "за последний ход"));
    }

    private static string JoinKnownParts(string separator, params string?[] values)
    {
        var parts = values
            .Where(static value => !IsUnknownValue(value))
            .Select(static value => value!.Trim())
            .ToArray();
        return parts.Length == 0 ? string.Empty : string.Join(separator, parts);
    }

    private static string FormatSigned(int value) =>
        value > 0
            ? "+" + value.ToString(CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);

    private static int FirstNonZero(params int[] values)
    {
        foreach (var value in values)
        {
            if (value != 0)
                return value;
        }

        return 0;
    }

    private static bool IsTrue(JsonNode? node)
    {
        if (node is not JsonValue value)
            return false;

        if (value.TryGetValue<bool>(out var boolValue))
            return boolValue;

        return value.TryGetValue<string>(out var text) &&
               bool.TryParse(text, out var parsed) &&
               parsed;
    }

    private static string DescribeDetectionLevel(int detectionLevel) =>
        detectionLevel switch
        {
            <= 25 => "Невидим",
            <= 50 => "Незамечен",
            <= 75 => "Подозрение",
            <= 99 => "Тревога",
            _ => "Обнаружен"
        };

    private static string FormatCharacteristicList(JsonNode? node)
    {
        if (node is not JsonArray array)
            return "не указано";

        var values = array
            .Select(item => TryGetScalarString(item, out var scalar) ? TranslateCharacteristic(scalar) : string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value) && !IsUnknownValue(value))
            .ToArray();

        return values.Length == 0 ? "не указано" : string.Join(", ", values);
    }

    private static string TranslateCharacteristic(string? characteristic)
    {
        if (string.IsNullOrWhiteSpace(characteristic))
            return string.Empty;

        var normalized = characteristic.Trim();
        return Characteristics.RussianNames.TryGetValue(normalized.ToLowerInvariant(), out var translated)
            ? translated
            : normalized;
    }

    private static string TranslateEffectType(string? effectType) =>
        (effectType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "buff" => "усиление",
            "debuff" => "ослабление",
            "heal" => "лечение",
            "healovertime" => "лечение со временем",
            "damage" => "урон",
            "damageovertime" => "урон со временем",
            "control" => "контроль",
            "damagereduction" => "снижение урона",
            _ => EmptyFallback(effectType)
        };

    private static string TranslateWoundSeverity(string? severity) =>
        (severity ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "light" => "лёгкая",
            "moderate" => "средняя",
            "serious" => "серьёзная",
            "critical" => "критическая",
            _ => EmptyFallback(severity)
        };

    private static string BuildProgressText(string current, string max)
    {
        if (string.IsNullOrWhiteSpace(current) && string.IsNullOrWhiteSpace(max))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(max))
            return current;

        if (string.IsNullOrWhiteSpace(current))
            return $"0/{max}";

        return $"{current}/{max}";
    }

    private static async Task<ExplorerCommandResult> BuildSoul(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, "game_state/meta/soul_state.json");
        if (read.Node == null)
            return MissingOrMalformed(command, "Душа", read);

        return Completed(command,
            Panel("Душа",
                Grid(
                    ("Имя души", GetString(read.Node, "soulName")),
                    ("Форма души", EmptyFallback(GetString(read.Node, "soulFormDescription"))),
                    ("Царство", ExplorerPlayerFacingLabels.Realm(GetString(read.Node, "currentRealm"))),
                    ("Инкарнация", GetNumberOrString(read.Node, "currentIncarnation")),
                    ("Чернильные Перья", DescribeInkFeathers(read.Node)),
                    ("Просветление", DescribeNested(read.Node, "enlightenment")),
                    ("Жизней в истории", CountArray(read.Node, "livesHistory").ToString()))));
    }

    private static async Task<ExplorerCommandResult> BuildSoulSection(
        string command,
        FileSystemManager fs,
        string title,
        string propertyName)
    {
        var read = await ReadJson(fs, "game_state/meta/soul_state.json");
        if (read.Node == null)
            return MissingOrMalformed(command, title, read);

        var section = read.Node[propertyName];
        var lifeCards = section is JsonArray { Count: > 0 } history
            ? history
                .OfType<JsonObject>()
                .Select(BuildLifeHistoryCard)
                .ToList()
            : [];
        var blocks = new List<UiBlock>
        {
            new UiEntityDossierBlock
            {
                EntityType = "life-history",
                Title = title,
                Summary = section == null
                    ? "История прошлых жизней пока не найдена."
                    : "Краткая летопись завершённых воплощений души.",
                Facts =
                [
                    new UiEntityFact { Label = "Записей", Value = lifeCards.Count.ToString() },
                    new UiEntityFact { Label = "Состояние", Value = section == null ? "не найдено" : "найдено" }
                ],
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "life-history-records",
                        Title = "Воплощения",
                        Icon = "history",
                        Presentation = "cards",
                        CollectionLabel = lifeCards.Count == 1 ? "1 жизнь" : $"{lifeCards.Count} жизней",
                        Cards = lifeCards
                    }
                ]
            }
        };

        return Completed(command, blocks);
    }

    private static UiEntityCard BuildLifeHistoryCard(JsonObject life, int index)
    {
        var incarnation = FirstNonEmpty(
            GetNumberOrString(life, "incarnation"),
            GetNumberOrString(life, "incarnationNumber"),
            (index + 1).ToString());
        var identity = DescribeLifeIdentity(life);
        var outcome = DescribeLifeOutcome(life);
        var rewards = DescribeLifeRewards(life);

        var facts = new List<UiEntityFact>
        {
            new() { Label = "Инкарнация", Value = incarnation }
        };
        AddFactIfKnown(facts, "Кем и где", identity);
        AddFactIfKnown(facts, "Итог", outcome);
        AddFactIfKnown(facts, "Награды", rewards);

        return new UiEntityCard
        {
            Title = $"Инкарнация {incarnation}",
            Subtitle = IsUnknownValue(identity) ? "Прошлая жизнь" : identity,
            Summary = IsUnknownValue(outcome) ? "Итог жизни пока не описан." : outcome,
            Icon = "history",
            Facts = facts
        };
    }

    private static string DescribeLifeIdentity(JsonObject life)
    {
        var character = FirstKnown(
            GetString(life, "characterName", string.Empty),
            GetString(life, "heroName", string.Empty),
            GetString(life, "name", string.Empty),
            GetString(life, "lifeName", string.Empty));
        var world = FirstKnown(
            GetString(life, "worldName", string.Empty),
            GetString(life, "world", string.Empty),
            GetString(life, "realm", string.Empty),
            GetString(life, "currentRealm", string.Empty));
        var role = JoinKnown(" / ",
            GetString(life, "race", string.Empty),
            GetString(life, "class", string.Empty));

        return FirstKnown(
            JoinKnown(" / ", character, world, role),
            character,
            world,
            role);
    }

    private static string DescribeLifeOutcome(JsonObject life) =>
        FirstKnown(
            GetString(life, "summary", string.Empty),
            GetString(life, "endingSummary", string.Empty),
            GetString(life, "ending", string.Empty),
            GetString(life, "finalState", string.Empty),
            GetString(life, "deathReason", string.Empty),
            GetString(life, "status", string.Empty));

    private static string DescribeLifeRewards(JsonObject life) =>
        FirstKnown(
            DescribeLifeValue(life["rewards"]),
            DescribeLifeValue(life["reward"]),
            GetString(life, "rewardInfo", string.Empty),
            GetString(life, "visibleReward", string.Empty));

    private static string DescribeLifeValue(JsonNode? node)
    {
        if (TryGetScalarString(node, out var scalar))
            return scalar;

        if (node is JsonArray array)
        {
            var values = array
                .Select(DescribeLifeValue)
                .Where(static value => !IsUnknownValue(value))
                .Take(5)
                .ToList();
            return values.Count == 0 ? "не указано" : string.Join("; ", values);
        }

        if (node is JsonObject obj)
        {
            var direct = FirstKnown(
                GetString(obj, "displayName", string.Empty),
                GetString(obj, "title", string.Empty),
                GetString(obj, "name", string.Empty),
                GetString(obj, "summary", string.Empty),
                GetString(obj, "description", string.Empty));
            if (!IsUnknownValue(direct))
                return direct;

            var parts = obj
                .Where(static property => !IsTechnicalLifeProperty(property.Key))
                .Select(static property => $"{TranslateLifeField(property.Key)}: {DescribeLifeValue(property.Value)}")
                .Where(static value => !IsUnknownValue(value) && !value.EndsWith(": не указано", StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .ToList();
            return parts.Count == 0 ? "не указано" : string.Join("; ", parts);
        }

        return "не указано";
    }

    private static bool IsTechnicalLifeProperty(string propertyName) =>
        propertyName.Equals("id", StringComparison.OrdinalIgnoreCase) ||
        propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("schemaVersion", StringComparison.OrdinalIgnoreCase) ||
        propertyName.StartsWith("_", StringComparison.OrdinalIgnoreCase);

    private static string TranslateLifeField(string propertyName) =>
        propertyName switch
        {
            "title" or "name" or "displayName" => "Название",
            "summary" => "Кратко",
            "description" => "Описание",
            "status" or "state" => "Состояние",
            "reward" or "rewards" or "rewardInfo" => "Награда",
            "world" or "worldName" or "realm" => "Мир",
            "characterName" or "heroName" => "Персонаж",
            "ending" or "endingSummary" => "Финал",
            "deathReason" => "Причина смерти",
            _ => propertyName.Replace('_', ' ')
        };

    private static string JoinKnown(string separator, params string[] values)
    {
        var parts = values
            .Where(static value => !IsUnknownValue(value))
            .Select(static value => value.Trim())
            .ToArray();
        return parts.Length == 0 ? "не указано" : string.Join(separator, parts);
    }

    private static string FirstKnown(params string[] values)
    {
        foreach (var value in values)
        {
            if (!IsUnknownValue(value))
                return value.Trim();
        }

        return "не указано";
    }

    private static bool IsUnknownValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        string.Equals(value.Trim(), "не указано", StringComparison.OrdinalIgnoreCase);

    private static async Task<ExplorerCommandResult> BuildInkFeathers(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var read = await ReadJson(fs, "game_state/meta/soul_state.json");
        if (read.Node is not JsonObject soulRoot)
        {
            return Completed(
                command,
                new UiMessageBlock
                {
                    Severity = UiNotificationSeverity.Warning,
                    Title = "Чернильные Перья",
                    Message = "Состояние души сейчас недоступно. Откройте сводку позже."
                });
        }

        var currentRealm = GetString(soulRoot, "currentRealm", stateManager.CurrentState.CurrentRealm);
        var current = Math.Max(0, GetInkFeatherCurrent(soulRoot, stateManager.CurrentState.InkFeathers));
        var total = Math.Max(current, GetInkFeatherTotal(soulRoot, current));
        var revealCost = ComputeRevealFateCost(current);
        var rewriteCost = ComputeRewriteFateCost(current);
        var pendingService = new PendingTurnStateService(fs, NullLogger<PendingTurnStateService>.Instance);
        var pending = await pendingService.TryReadExistingAsync();
        var hasLockedFate = pending?.IsFateLocked == true;
        var isMortalRealm = RealmSemantics.IsMortalRealm(currentRealm);

        var blocks = new List<UiBlock>
        {
            Panel(
                "Чернильные Перья",
                new UiTextBlock
                {
                    Text = "Чернильные Перья можно потратить на раскрытие или переписывание судьбы во время смертной жизни.",
                    Tone = UiTone.Accent
                },
                Grid(
                    ("Сейчас", current.ToString()),
                    ("Всего накоплено", total.ToString()),
                    ("Открытая судьба", hasLockedFate ? "есть" : "нет"),
                    ("Открыть Судьбу", current >= revealCost ? $"{revealCost} Чернильных Перьев" : $"нужно {revealCost}, доступно {current}"),
                    ("Переписать Судьбу", hasLockedFate
                        ? current >= rewriteCost ? $"{rewriteCost} Чернильных Перьев" : $"нужно {rewriteCost}, доступно {current}"
                        : "сначала откройте судьбу")))
        };

        if (!isMortalRealm)
        {
            blocks.Add(new UiMessageBlock
            {
                Severity = UiNotificationSeverity.Warning,
                Title = "Формы судьбы недоступны",
                Message = "Раскрытие и переписывание судьбы доступны только во время смертной жизни."
            });
        }

        var actions = new List<UiAction>();
        if (isMortalRealm && current >= revealCost)
        {
            actions.Add(new UiAction
            {
                Id = "ink-feather-reveal-fate",
                Label = $"Открыть Судьбу ({revealCost} Перьев)",
                Command = "/reveal_fate",
                Style = UiActionStyle.Primary,
                RequiresConfirmation = true
            });
        }

        if (isMortalRealm && hasLockedFate && current >= rewriteCost)
        {
            actions.Add(new UiAction
            {
                Id = "ink-feather-rewrite-fate",
                Label = $"Переписать Судьбу ({rewriteCost} Перьев)",
                Command = "/rewrite_fate",
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = true
            });
        }

        return new ExplorerCommandResult
        {
            Command = command,
            State = CommandExecutionState.Completed,
            Blocks = blocks,
            Actions = actions
        };
    }

    private static async Task<ExplorerCommandResult> BuildSoulRelics(string command, FileSystemManager fs)
    {
        var context = await SoulRelicEquipmentService.ReadContextAsync(fs);
        if (context == null)
        {
            if (TryReadDetailSelector(ReadCommandArguments(command), out _, "реликвия", "relic", "деталь", "detail"))
                return DetailUnavailable(command, "Реликвия души");

            var read = await ReadJson(fs, SoulRelicEquipmentService.SoulStatePath);
            return MissingOrMalformed(command, "Реликвии души", read);
        }

        if (TryReadDetailSelector(ReadCommandArguments(command), out var relicSelector, "реликвия", "relic", "деталь", "detail"))
            return BuildSoulRelicDetail(command, context, relicSelector);

        var equippedCards = context.Equipped
            .Select(static relic => BuildSoulRelicOverviewCard(relic, "Экипировано"))
            .ToList();
        var storedCards = context.Stored
            .Select(static relic => BuildSoulRelicOverviewCard(relic, "Хранилище"))
            .ToList();

        var blocks = new List<UiBlock>
        {
            new UiEntityDossierBlock
            {
                EntityType = "soul-relics",
                Title = "Реликвии души",
                Summary = "Постоянные реликвии души, которые можно переносить между состояниями посмертия.",
                Facts =
                [
                    new UiEntityFact { Label = "Экипировано", Value = context.Equipped.Count.ToString() },
                    new UiEntityFact { Label = "В хранилище", Value = context.Stored.Count.ToString() }
                ],
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "soul-relics-equipped",
                        Title = "Экипировано",
                        Icon = "sparkles",
                        Presentation = "cards",
                        CollectionLabel = equippedCards.Count == 1 ? "1 реликвия" : $"{equippedCards.Count} реликвий",
                        Cards = equippedCards
                    },
                    new UiEntityDossierSection
                    {
                        Id = "soul-relics-stored",
                        Title = "Хранилище",
                        Icon = "archive",
                        Presentation = "cards",
                        CollectionLabel = storedCards.Count == 1 ? "1 реликвия" : $"{storedCards.Count} реликвий",
                        Cards = storedCards
                    }
                ]
            }
        };

        if (equippedCards.Count == 0 && storedCards.Count == 0)
        {
            blocks.Add(Message(
                UiNotificationSeverity.Info,
                "Реликвии души",
                "Реликвии души пока не найдены."));
        }

        var currentRealm = GetString(context.Root, "currentRealm");
        var actions = RealmSemantics.IsChaosSea(currentRealm)
            ? BuildSoulRelicActions(context)
            : [];

        return new ExplorerCommandResult
        {
            Command = command,
            State = CommandExecutionState.Completed,
            Blocks = blocks,
            Actions = actions
        };
    }

    private static UiEntityCard BuildSoulRelicOverviewCard(SoulRelicItem relic, string status)
    {
        var title = FirstNonEmpty(relic.Name, relic.RelicId, "Реликвия души");
        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Редкость", FormatRarityForPlayer(relic.Rarity));

        if (string.Equals(status, "Экипировано", StringComparison.OrdinalIgnoreCase))
            AddFactIfKnown(facts, "Слот", SoulRelicEquipmentService.FormatSlotLabel(relic.CurrentSlot));
        else
            AddFactIfKnown(facts, "Совместимые слоты", DescribeSoulRelicCompatibleSlots(relic));

        return new UiEntityCard
        {
            Title = title,
            Subtitle = status,
            Summary = string.Equals(status, "Экипировано", StringComparison.OrdinalIgnoreCase)
                ? "Реликвия сейчас влияет на душу через выбранный слот."
                : "Реликвия хранится и может быть экипирована в подходящий слот.",
            Icon = "sparkles",
            Badges =
            [
                new UiEntityBadge
                {
                    Label = status,
                    Icon = "sparkles",
                    Tone = string.Equals(status, "Экипировано", StringComparison.OrdinalIgnoreCase) ? UiTone.Success : UiTone.Muted
                }
            ],
            Facts = facts
        };
    }

    private static ExplorerCommandResult BuildSoulRelicDetail(
        string command,
        SoulRelicEquipmentContext context,
        string selector)
    {
        var detail = FindSoulRelicDetail(context.Root, selector);
        if (detail == null)
            return DetailUnavailable(command, "Реликвия души");

        var relic = detail.Value.Relic;
        var relicId = GetString(relic, "relicId", string.Empty);
        var name = FirstNonEmpty(GetString(relic, "name", string.Empty), relicId, selector);
        var rarity = FirstNonEmpty(
            GetString(relic, "rarity", string.Empty),
            GetString(relic, "quality", string.Empty),
            "не указана");
        var slot = FirstNonEmpty(
            detail.Value.Slot,
            GetString(relic, "slot", string.Empty),
            GetString(relic["gameplayStatus"] as JsonObject, "currentSlot"),
            "не указан");
        var description = FirstNonEmpty(
            GetString(relic, "description", string.Empty),
            GetString(relic, "summary", string.Empty),
            GetString(relic, "effectSummary", string.Empty),
            "Подробное описание реликвии пока не записано.");
        var effect = FirstNonEmpty(
            GetString(relic, "effectSummary", string.Empty),
            GetString(relic, "effect", string.Empty),
            GetString(relic, "lore", string.Empty));

        var blocks = new List<UiBlock>
        {
            Panel($"Реликвия души: {name}",
                Grid(
                    ("Состояние", detail.Value.Status),
                    ("Слот", SoulRelicEquipmentService.FormatSlotLabel(slot)),
                    ("Редкость", rarity),
                    ("ID", EmptyFallback(relicId)),
                    ("Совместимые слоты", DescribeStringArray(relic["compatibleSlots"])))),
            new UiTextBlock { Text = description, Tone = UiTone.Accent }
        };

        if (!string.IsNullOrWhiteSpace(effect))
            blocks.Add(new UiTextBlock { Text = effect, Tone = UiTone.Subtle });

        var tags = DescribeStringArray(relic["tags"]);
        if (tags != "не указано")
            blocks.Add(Panel("Метки", Grid(("Метки", tags))));

        return Completed(command, blocks);
    }

    private static List<UiAction> BuildSoulRelicActions(SoulRelicEquipmentContext context)
    {
        var actions = new List<UiAction>();
        var detailActionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relic in context.Equipped.Concat(context.Stored))
        {
            var detailAction = BuildSoulRelicDetailAction(relic);
            if (detailAction != null && detailActionIds.Add(detailAction.Id))
                actions.Add(detailAction);
        }

        foreach (var relic in context.Stored)
        {
            var identity = FirstNonEmpty(relic.RelicId, relic.Name);
            if (string.IsNullOrWhiteSpace(identity))
                continue;

            actions.Add(new UiAction
            {
                Id = SoulRelicEquipmentService.BuildActionId("soul-relic-equip", identity),
                Label = $"Экипировать «{relic.Name}»",
                Command = "/soul_relic_equip " + SoulRelicEquipmentService.FormatCommandArgument(identity),
                Style = UiActionStyle.Secondary
            });
        }

        foreach (var relic in context.Equipped)
        {
            if (string.IsNullOrWhiteSpace(relic.CurrentSlot))
                continue;

            actions.Add(new UiAction
            {
                Id = SoulRelicEquipmentService.BuildActionId("soul-relic-unequip", relic.CurrentSlot),
                Label = $"Снять «{relic.Name}»",
                Command = "/soul_relic_unequip " + SoulRelicEquipmentService.FormatCommandArgument(relic.CurrentSlot),
                Style = UiActionStyle.Secondary
            });
        }

        return actions;
    }

    private static UiAction? BuildSoulRelicDetailAction(SoulRelicItem relic)
    {
        var identity = FirstNonEmpty(relic.RelicId, relic.Name);
        if (string.IsNullOrWhiteSpace(identity))
            return null;

        var label = FirstNonEmpty(relic.Name, relic.RelicId, identity);
        return new UiAction
        {
            Id = SoulRelicEquipmentService.BuildActionId("soul-relic-detail", identity),
            Label = $"Подробно: «{label}»",
            Command = "/soul_relics реликвия " + SoulRelicEquipmentService.FormatCommandArgument(identity),
            Style = UiActionStyle.Secondary,
            RequiresConfirmation = false
        };
    }

    private static string DescribeSoulRelicCompatibleSlots(SoulRelicItem relic)
    {
        if (relic.CompatibleSlots.Count == 0)
            return "-";

        return string.Join(", ", relic.CompatibleSlots.Select(SoulRelicEquipmentService.FormatSlotLabel));
    }

    private static SoulRelicJsonDetail? FindSoulRelicDetail(JsonObject root, string selector)
    {
        if (root["soulRelics"] is not JsonObject soulRelics || string.IsNullOrWhiteSpace(selector))
            return null;

        foreach (var relic in EnumerateSoulRelicJsonDetails(soulRelics))
        {
            var item = relic.Relic;
            if (string.Equals(GetString(item, "relicId", string.Empty), selector, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(GetString(item, "name", string.Empty), selector, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(relic.Slot, selector, StringComparison.OrdinalIgnoreCase))
            {
                return relic;
            }
        }

        return null;
    }

    private static IEnumerable<SoulRelicJsonDetail> EnumerateSoulRelicJsonDetails(JsonObject soulRelics)
    {
        if (soulRelics["equipped"] is JsonArray equipped)
        {
            foreach (var relic in equipped.OfType<JsonObject>())
            {
                var slot = FirstNonEmpty(
                    GetString(relic["gameplayStatus"] as JsonObject, "currentSlot"),
                    GetString(relic, "slot", string.Empty));
                yield return new SoulRelicJsonDetail(relic, "Экипировано", slot);
            }
        }

        if (soulRelics["stored"] is JsonArray stored)
        {
            foreach (var relic in stored.OfType<JsonObject>())
                yield return new SoulRelicJsonDetail(relic, "Хранилище", GetString(relic, "slot", string.Empty));
        }
    }

    private static async Task<ExplorerCommandResult> BuildAfterlifeArchive(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, "game_state/meta/soul_state.json");
        if (read.Node is not JsonObject root)
        {
            if (TryReadDetailSelector(ReadCommandArguments(command), out _, "запись", "entry", "archive", "деталь", "detail"))
                return DetailUnavailable(command, "Архив души");

            return MissingOrMalformed(command, "Архив души", read);
        }

        AfterlifeArchiveState.NormalizeShape(root);
        var stored = AfterlifeArchiveState.EnsureStoredArray(root);
        if (TryReadDetailSelector(ReadCommandArguments(command), out var archiveSelector, "запись", "entry", "archive", "деталь", "detail"))
            return BuildAfterlifeArchiveDetail(command, stored, archiveSelector);

        var entries = stored.OfType<JsonObject>().ToList();
        var cards = entries.Select(BuildAfterlifeArchiveOverviewCard).ToList();
        var blocks = new List<UiBlock>
        {
            new UiEntityDossierBlock
            {
                EntityType = "afterlife-archive",
                Title = "Архив души",
                Summary = "Сохранённые фрагменты памяти, знаний и достижений души.",
                Facts =
                [
                    new UiEntityFact { Label = "Записей", Value = entries.Count.ToString() },
                    new UiEntityFact { Label = "Свободно для действий", Value = entries.Count(entry => !AfterlifeArchiveState.IsReserved(entry)).ToString() }
                ],
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "afterlife-archive-entries",
                        Title = "Записи Архива",
                        Icon = "book-open",
                        Presentation = "cards",
                        CollectionLabel = cards.Count == 1 ? "1 запись" : $"{cards.Count} записей",
                        Cards = cards
                    }
                ]
            }
        };

        if (entries.Count == 0)
        {
            blocks.Add(Message(
                UiNotificationSeverity.Info,
                "Архив души",
                "Сохранённых записей Архива пока нет."));
        }

        return Completed(command, blocks, entries.Select(BuildAfterlifeArchiveDetailAction));
    }

    private static UiEntityCard BuildAfterlifeArchiveOverviewCard(JsonObject entry)
    {
        var title = FirstNonEmpty(GetString(entry, "title", string.Empty), GetString(entry, "archiveId", string.Empty), "Запись Архива");
        var reservation = DescribeArchiveEntryReservation(entry);
        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Тип", AfterlifeArchiveState.GetEntryTypeLabel(GetString(entry, "entryType", string.Empty)));
        AddFactIfKnown(facts, "Редкость", FormatRarityForPlayer(GetString(entry, "rarity", string.Empty)));
        AddFactIfKnown(facts, "Источник", AfterlifeArchiveState.GetSourceKindLabel(GetString(entry, "sourceKind", string.Empty)));
        AddFactIfKnown(facts, "Жизнь-источник", GetString(entry, "sourceLife", string.Empty));
        AddFactIfKnown(facts, "Состояние", reservation);

        return new UiEntityCard
        {
            Title = title,
            Subtitle = AfterlifeArchiveState.GetEntryTypeLabel(GetString(entry, "entryType", string.Empty)),
            Summary = FirstNonEmpty(GetString(entry, "summary", string.Empty), "Краткое описание записи пока не добавлено."),
            Icon = "book-open",
            Badges =
            [
                new UiEntityBadge { Label = FormatRarityForPlayer(GetString(entry, "rarity", string.Empty)), Icon = "sparkles", Tone = UiTone.Accent },
                new UiEntityBadge { Label = reservation, Icon = "archive", Tone = AfterlifeArchiveState.IsReserved(entry) ? UiTone.Warning : UiTone.Success }
            ],
            Facts = facts
        };
    }

    private static ExplorerCommandResult BuildAfterlifeArchiveDetail(string command, JsonArray stored, string selector)
    {
        var entry = AfterlifeArchiveState.FindEntry(stored, selector);
        if (entry == null)
            return DetailUnavailable(command, "Архив души");

        var archiveId = GetString(entry, "archiveId", string.Empty);
        var title = FirstNonEmpty(GetString(entry, "title", string.Empty), archiveId, selector);
        var summary = FirstNonEmpty(GetString(entry, "summary", string.Empty), "Краткое описание записи пока не добавлено.");
        var content = GetString(entry, "content", string.Empty);
        var reservation = AfterlifeArchiveState.GetReservationObject(entry);

        var blocks = new List<UiBlock>
        {
            Panel($"Архив души: {title}",
                Grid(
                    ("Тип", AfterlifeArchiveState.GetEntryTypeLabel(GetString(entry, "entryType", string.Empty))),
                    ("Редкость", FirstNonEmpty(GetString(entry, "rarity", string.Empty), "Common")),
                    ("Источник", AfterlifeArchiveState.GetSourceKindLabel(GetString(entry, "sourceKind", string.Empty))),
                    ("Жизнь-источник", FirstNonEmpty(GetString(entry, "sourceLife", string.Empty), "не указана")),
                    ("Связанный фрагмент", EmptyFallback(GetString(entry, "sourceEntryId", string.Empty))),
                    ("Состояние", DescribeArchiveEntryReservation(entry)))),
            new UiTextBlock { Text = summary, Tone = UiTone.Accent }
        };

        if (!string.IsNullOrWhiteSpace(content))
            blocks.Add(new UiTextBlock { Text = content, Tone = UiTone.Subtle });

        var tags = DescribeStringArray(entry["tags"]);
        if (tags != "не указано")
            blocks.Add(Panel("Метки", Grid(("Метки", tags))));

        if (reservation != null)
        {
            blocks.Add(Panel("Резерв",
                Grid(
                    ("Действие", AfterlifeArchiveState.GetReservationLabel(GetString(reservation, "reservationKind", string.Empty))),
                    ("Хранитель", EmptyFallback(GetString(reservation, "guardianName", string.Empty))),
                    ("Проект", EmptyFallback(GetString(reservation, "targetProjectName", string.Empty))))));
        }

        return Completed(command, blocks);
    }

    private static UiAction BuildAfterlifeArchiveDetailAction(JsonObject entry)
    {
        var archiveId = GetString(entry, "archiveId", string.Empty);
        var title = FirstNonEmpty(GetString(entry, "title", string.Empty), archiveId);
        return new UiAction
        {
            Id = SoulRelicEquipmentService.BuildActionId("afterlife-archive-detail", archiveId),
            Label = $"Подробно: «{title}»",
            Command = "/afterlife_archive запись " + SoulRelicEquipmentService.FormatCommandArgument(archiveId),
            Style = UiActionStyle.Secondary,
            RequiresConfirmation = false
        };
    }

    private static async Task<ExplorerCommandResult> BuildArchiveCandidates(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, AfterlifeArchiveCandidateService.ManifestPath);
        if (read.Node is not JsonObject root)
        {
            if (TryReadDetailSelector(ReadCommandArguments(command), out _, "кандидат", "candidate", "деталь", "detail"))
                return DetailUnavailable(command, "Кандидат в Архив");

            return MissingOrMalformed(command, "Кандидаты в Архив", read);
        }

        var candidates = root["candidates"] is JsonArray array
            ? array.OfType<JsonObject>().ToList()
            : [];

        if (TryReadDetailSelector(ReadCommandArguments(command), out var candidateSelector, "кандидат", "candidate", "деталь", "detail"))
            return BuildArchiveCandidateDetail(command, candidates, candidateSelector);

        var cards = candidates.Select(BuildArchiveCandidateOverviewCard).ToList();
        var blocks = new List<UiBlock>
        {
            new UiEntityDossierBlock
            {
                EntityType = "archive-candidates",
                Title = "Кандидаты в Архив",
                Summary = "Фрагменты прошедшей жизни, которые можно перенести в Архив души.",
                Facts =
                [
                    new UiEntityFact { Label = "Жизнь-источник", Value = FirstNonEmpty(GetString(root, "sourceLife", string.Empty), "не указана") },
                    new UiEntityFact { Label = "Кандидатов", Value = candidates.Count.ToString() }
                ],
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "archive-candidate-list",
                        Title = "Кандидаты",
                        Icon = "archive",
                        Presentation = "cards",
                        CollectionLabel = cards.Count == 1 ? "1 кандидат" : $"{cards.Count} кандидатов",
                        Cards = cards
                    }
                ]
            }
        };

        if (candidates.Count == 0)
        {
            blocks.Add(Message(
                UiNotificationSeverity.Info,
                "Кандидаты в Архив",
                "Кандидатов для Архива сейчас нет."));
        }

        return Completed(command, blocks, candidates.Select(BuildArchiveCandidateDetailAction));
    }

    private static UiEntityCard BuildArchiveCandidateOverviewCard(JsonObject candidate)
    {
        var title = FirstNonEmpty(GetString(candidate, "title", string.Empty), GetString(candidate, "candidateId", string.Empty), "Кандидат в Архив");
        var status = DescribeArchiveCandidateStatus(GetString(candidate, "status", string.Empty));
        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Тип", AfterlifeArchiveState.GetEntryTypeLabel(GetString(candidate, "proposedEntryType", string.Empty)));
        AddFactIfKnown(facts, "Редкость", FormatRarityForPlayer(GetString(candidate, "rarity", string.Empty)));
        AddFactIfKnown(facts, "Источник", AfterlifeArchiveState.GetSourceKindLabel(GetString(candidate, "sourceKind", string.Empty)));
        AddFactIfKnown(facts, "Жизнь-источник", GetString(candidate, "sourceLife", string.Empty));
        AddFactIfKnown(facts, "Состояние", status);

        return new UiEntityCard
        {
            Title = title,
            Subtitle = status,
            Summary = FirstNonEmpty(GetString(candidate, "summary", string.Empty), "Краткое описание кандидата пока не добавлено."),
            Icon = "archive",
            Badges =
            [
                new UiEntityBadge { Label = FormatRarityForPlayer(GetString(candidate, "rarity", string.Empty)), Icon = "sparkles", Tone = UiTone.Accent },
                new UiEntityBadge { Label = status, Icon = "check", Tone = string.Equals(status, "ожидает выбора", StringComparison.OrdinalIgnoreCase) ? UiTone.Warning : UiTone.Muted }
            ],
            Facts = facts
        };
    }

    private static ExplorerCommandResult BuildArchiveCandidateDetail(
        string command,
        IReadOnlyList<JsonObject> candidates,
        string selector)
    {
        var candidate = candidates.FirstOrDefault(item =>
            string.Equals(GetString(item, "candidateId", string.Empty), selector, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(GetString(item, "title", string.Empty), selector, StringComparison.OrdinalIgnoreCase));
        if (candidate == null)
            return DetailUnavailable(command, "Кандидат в Архив");

        var candidateId = GetString(candidate, "candidateId", string.Empty);
        var title = FirstNonEmpty(GetString(candidate, "title", string.Empty), candidateId, selector);
        var summary = FirstNonEmpty(GetString(candidate, "summary", string.Empty), "Краткое описание кандидата пока не добавлено.");
        var content = GetString(candidate, "content", string.Empty);
        var blocks = new List<UiBlock>
        {
            Panel($"Кандидат в Архив: {title}",
                Grid(
                    ("Тип", AfterlifeArchiveState.GetEntryTypeLabel(GetString(candidate, "proposedEntryType", string.Empty))),
                    ("Редкость", FirstNonEmpty(GetString(candidate, "rarity", string.Empty), "Common")),
                    ("Состояние", DescribeArchiveCandidateStatus(GetString(candidate, "status", string.Empty))),
                    ("Источник", AfterlifeArchiveState.GetSourceKindLabel(GetString(candidate, "sourceKind", string.Empty))),
                    ("Жизнь-источник", FirstNonEmpty(GetString(candidate, "sourceLife", string.Empty), "не указана")),
                    ("Связанный фрагмент", EmptyFallback(GetString(candidate, "sourceEntryId", string.Empty))))),
            new UiTextBlock { Text = summary, Tone = UiTone.Accent }
        };

        if (!string.IsNullOrWhiteSpace(content))
            blocks.Add(new UiTextBlock { Text = content, Tone = UiTone.Subtle });

        var tags = DescribeStringArray(candidate["tags"]);
        if (tags != "не указано")
            blocks.Add(Panel("Метки", Grid(("Метки", tags))));

        return Completed(command, blocks);
    }

    private static UiAction BuildArchiveCandidateDetailAction(JsonObject candidate)
    {
        var candidateId = GetString(candidate, "candidateId", string.Empty);
        var title = FirstNonEmpty(GetString(candidate, "title", string.Empty), candidateId);
        return new UiAction
        {
            Id = SoulRelicEquipmentService.BuildActionId("archive-candidate-detail", candidateId),
            Label = $"Подробно: «{title}»",
            Command = "/archive_candidates кандидат " + SoulRelicEquipmentService.FormatCommandArgument(candidateId),
            Style = UiActionStyle.Secondary,
            RequiresConfirmation = false
        };
    }

    private static async Task<ExplorerCommandResult> BuildChronicle(string command, FileSystemManager fs)
    {
        var files = new[]
        {
            ("Хроника персонажа", "game_state/meta/character_chronicle.json"),
            ("Хроника игрока", "lore/chaos_sea/player_chronicle.json"),
            ("Сюжетный план", "game_state/quests/plot_outline.json")
        };
        var reads = new List<(string Title, JsonReadResult Read)>();
        foreach (var (title, path) in files)
            reads.Add((title, await ReadJson(fs, path)));

        var blocks = new List<UiBlock>
        {
            Panel("Хроника", Grid(reads.Select(static read =>
                (read.Title, DescribeReadableJsonFileState(read.Read))).ToArray()))
        };

        foreach (var (title, read) in reads)
        {
            if (read.Node != null)
            {
                blocks.AddRange(BuildReadableJsonBlocks(title, read.Node));
                continue;
            }

            if (read.FileExists)
            {
                blocks.Add(Message(
                    UiNotificationSeverity.Warning,
                    title,
                    "Запись найдена, но её не удалось прочитать."));
            }
        }

        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildStory(string command, FileSystemManager fs)
    {
        var storyService = new StoryService(fs, NullLogger<StoryService>.Instance);
        var stories = storyService.GetAvailableStories();
        if (stories.Count == 0)
            return Completed(command, Message(UiNotificationSeverity.Info, "Рассказ", "Рассказ пока пуст. Сыграйте несколько ходов, и история начнёт записываться."));

        var cards = new List<UiEntityCard>();
        foreach (var story in stories)
        {
            var entries = await storyService.ReadStoryAsync(story.RelativePath, lastN: 6);
            cards.Add(BuildStoryCard(story, entries));
        }

        return Completed(command,
            new UiEntityDossierBlock
            {
                EntityType = "story",
                Title = "Рассказ",
                Summary = "Художественная летопись сыгранных ходов и переходов между главами.",
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "story-chapters",
                        Title = "Главы",
                        Icon = "book-open",
                        Presentation = "collection",
                        CollectionLabel = cards.Count == 1 ? "1 глава" : $"{cards.Count} глав",
                        Cards = cards
                    }
                ]
            });
    }

    private static UiEntityCard BuildStoryCard(StoryFileInfo story, IReadOnlyList<StoryEntry> entries)
    {
        return new UiEntityCard
        {
            Title = story.DisplayName,
            Subtitle = story.EntryCount == 1 ? "1 запись" : $"{story.EntryCount} записей",
            Summary = entries.Count == 0
                ? "Эта глава пока пуста."
                : "Последние записи главы.",
            Icon = "book-open",
            Facts =
            [
                new UiEntityFact { Label = "Записей", Value = story.EntryCount.ToString() }
            ],
            Nested = entries.Select(BuildStoryEntryCard).ToList()
        };
    }

    private static UiEntityCard BuildStoryEntryCard(StoryEntry entry)
    {
        var facts = new List<UiEntityFact>();
        if (entry.Turn >= 0)
            AddFactIfKnown(facts, "Ход", entry.Turn.ToString());
        AddFactIfKnown(facts, "Царство", ExplorerPlayerFacingLabels.Realm(entry.Realm ?? string.Empty));
        AddFactIfKnown(facts, "Действие", entry.Player);
        AddFactIfKnown(facts, "Локация", entry.Location ?? string.Empty);

        return new UiEntityCard
        {
            Title = entry.Turn < 0 ? FirstNonEmpty(entry.Player.Trim('[', ']'), "Переход") : $"Ход {entry.Turn}",
            Summary = FirstNonEmpty(entry.Narrative, "Запись без текста."),
            Icon = entry.Turn < 0 ? "sparkles" : "scroll",
            Facts = facts
        };
    }

    private static ExplorerCommandResult BuildGallery(string command, FileSystemManager fs)
    {
        var media = new LocalMediaService(fs);
        var imageReferences = media.EnumerateGallery().ToList();
        var blocks = new List<UiBlock>();

        if (imageReferences.Count == 0)
        {
            blocks.Add(Message(UiNotificationSeverity.Info, "Изображения", "Сохранённых изображений пока нет."));
        }
        else
        {
            blocks.Add(new UiEntityDossierBlock
            {
                EntityType = "gallery",
                Title = "Галерея",
                Summary = "Визуальные материалы текущей истории.",
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "gallery-images",
                        Title = "Изображения",
                        Icon = "image",
                        Presentation = "collection",
                        CollectionLabel = imageReferences.Count == 1 ? "1 изображение" : $"{imageReferences.Count} изображений",
                        Cards = imageReferences.Select(BuildGalleryImageCard).ToList()
                    }
                ]
            });
            blocks.AddRange(imageReferences.Select(static image => new UiImageBlock
            {
                Title = HumanizeFileName(Path.GetFileNameWithoutExtension(image.FileName)),
                Url = image.Url,
                MediaId = image.MediaId,
                RelativePath = image.RelativePath,
                AltText = HumanizeFileName(Path.GetFileNameWithoutExtension(image.FileName)),
                ContentType = image.ContentType,
                Length = image.Length,
                ModifiedAtUtc = image.ModifiedAtUtc
            }));
        }

        return Completed(command, blocks);
    }

    private static UiEntityCard BuildGalleryImageCard(LocalMediaReference image)
    {
        return new UiEntityCard
        {
            Title = HumanizeFileName(Path.GetFileNameWithoutExtension(image.FileName)),
            Subtitle = DescribeImageContentType(image.ContentType),
            Summary = image.Length > 0 ? $"{image.Length} байт" : "Изображение готово к просмотру.",
            Icon = "image",
            Facts =
            [
                new UiEntityFact { Label = "Формат", Value = DescribeImageContentType(image.ContentType) }
            ]
        };
    }

    private static string DescribeImageContentType(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/png" => "PNG",
            "image/jpeg" => "JPEG",
            "image/gif" => "GIF",
            "image/bmp" => "BMP",
            "image/webp" => "WebP",
            _ => "изображение"
        };

    private static async Task<ExplorerCommandResult> BuildGmThoughts(string command, FileSystemManager fs, LocalizationManager loc)
    {
        var read = await ReadJson(fs, "output/debug_logs.json");
        if (read.Node == null)
            return MissingOrMalformed(command, loc.T("gm_thoughts"), read);

        return Completed(command,
            new UiTextBlock
            {
                Tone = UiTone.Subtle,
                Text = GetString(read.Node, "gm_thoughts_markdown", "Нет данных ГМ.")
            });
    }

    private static ExplorerCommandResult BuildDebug(
        string command,
        FileSystemManager fs,
        StateManager stateManager,
        LocalizationManager loc)
    {
        var files = fs.GetAllGameStateFiles();
        var cards = files
            .GroupBy(path => DescribeDebugStateGroup(ToRelativeGameSessionPath(fs, path)), StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(static group => new UiEntityCard
            {
                Title = group.Key,
                Summary = group.Count() == 1 ? "1 файл состояния" : $"{group.Count()} файлов состояния",
                Icon = "file-text"
            })
            .ToList();

        return Completed(command,
            new UiEntityDossierBlock
            {
                EntityType = "debug-summary",
                Title = loc.T("debug_info"),
                Summary = "Краткая диагностика состояния без раскрытия локальных путей и файловых контрактов.",
                Facts =
                [
                    new UiEntityFact { Label = "Файлов состояния", Value = files.Length.ToString() },
                    new UiEntityFact { Label = "Сессия", Value = EmptyFallback(stateManager.CurrentState.SessionId) },
                    new UiEntityFact { Label = "Язык", Value = loc.CurrentLanguage }
                ],
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "debug-state-groups",
                        Title = "Разделы состояния",
                        Icon = "folder",
                        Presentation = "collection",
                        CollectionLabel = cards.Count == 1 ? "1 раздел" : $"{cards.Count} разделов",
                        Cards = cards
                    }
                ]
            });
    }

    private static string DescribeDebugStateGroup(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (normalized.StartsWith("game_state/core/", StringComparison.OrdinalIgnoreCase))
            return "Основное состояние";
        if (normalized.StartsWith("game_state/npcs/", StringComparison.OrdinalIgnoreCase))
            return "Персонажи";
        if (normalized.StartsWith("game_state/player/", StringComparison.OrdinalIgnoreCase))
            return "Герой";
        if (normalized.StartsWith("game_state/inventory/", StringComparison.OrdinalIgnoreCase))
            return "Инвентарь";
        if (normalized.StartsWith("game_state/quests/", StringComparison.OrdinalIgnoreCase))
            return "Квесты и сюжет";
        if (normalized.StartsWith("game_state/meta/", StringComparison.OrdinalIgnoreCase))
            return "Мета-состояние";
        if (normalized.StartsWith("game_state/factions/", StringComparison.OrdinalIgnoreCase))
            return "Фракции";
        if (normalized.StartsWith("game_state/locations/", StringComparison.OrdinalIgnoreCase))
            return "Локации";
        if (normalized.StartsWith("game_state/combat/", StringComparison.OrdinalIgnoreCase))
            return "Бой";
        if (normalized.StartsWith("output/", StringComparison.OrdinalIgnoreCase))
            return "Вывод ГМ";
        if (normalized.StartsWith("lore/", StringComparison.OrdinalIgnoreCase))
            return "Лор и архив";
        if (normalized.StartsWith("stories/", StringComparison.OrdinalIgnoreCase))
            return "Истории";
        return "Прочие данные";
    }

    private static ExplorerCommandResult BuildDirectoryList(string command, FileSystemManager fs, string title, string relativePath)
    {
        var root = fs.ResolvePath(relativePath);
        var cards = new List<UiEntityCard>();
        if (Directory.Exists(root))
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(root).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                var card = BuildDirectoryEntryCard(path);
                if (card != null)
                    cards.Add(card);
            }
        }

        if (cards.Count == 0)
        {
            return Completed(command,
                Message(
                    UiNotificationSeverity.Info,
                    title,
                    "Записей пока нет."));
        }

        return Completed(command,
            new UiEntityDossierBlock
            {
                EntityType = "directory-list",
                Title = title,
                Summary = "Подключённые игровые материалы и правила, которые меняют тон или ограничения партии.",
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "directory-items",
                        Title = "Записи",
                        Icon = "scroll",
                        Presentation = "collection",
                        CollectionLabel = cards.Count == 1 ? "1 запись" : $"{cards.Count} записей",
                        Cards = cards
                    }
                ]
            });
    }

    private static UiEntityCard? BuildDirectoryEntryCard(string path)
    {
        if (Directory.Exists(path))
        {
            return new UiEntityCard
            {
                Title = HumanizeFileName(Path.GetFileName(path)),
                Subtitle = "Папка",
                Summary = "Набор связанных материалов.",
                Icon = "folder"
            };
        }

        if (!File.Exists(path))
            return null;

        var fileName = Path.GetFileName(path);
        var title = HumanizeFileName(Path.GetFileNameWithoutExtension(path));
        var summary = "Описание пока не добавлено.";
        if (string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase))
        {
            var markdown = ReadMarkdownSummary(path);
            title = FirstNonEmpty(markdown.Title, title);
            summary = FirstNonEmpty(markdown.Summary, summary);
        }

        return new UiEntityCard
        {
            Title = title,
            Subtitle = "Игровой модификатор",
            Summary = summary,
            Icon = "scroll",
            Facts =
            [
                new UiEntityFact { Label = "Тип", Value = DescribePlayerFacingFileKind(fileName) }
            ]
        };
    }

    private static (string Title, string Summary) ReadMarkdownSummary(string path)
    {
        try
        {
            var lines = File.ReadAllLines(path)
                .Select(static line => line.Trim())
                .Where(static line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
            var title = lines
                .FirstOrDefault(static line => line.StartsWith("#", StringComparison.Ordinal))?
                .TrimStart('#', ' ', '\t') ?? string.Empty;
            var summary = lines.FirstOrDefault(line => !line.StartsWith("#", StringComparison.Ordinal) &&
                                                       !string.Equals(line, title, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
            return (title, summary);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    private static string DescribePlayerFacingFileKind(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase)
            ? "Текстовое правило"
            : "Материал";
    }

    private static string HumanizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "Запись";

        return fileName.Replace('_', ' ').Replace('-', ' ').Trim();
    }

    private static async Task<ExplorerCommandResult> BuildSystemGuardians(string command, FileSystemManager fs)
    {
        var service = new SystemGuardianLibraryService(fs, NullLogger<SystemGuardianLibraryService>.Instance);
        var presets = await service.GetAvailablePresetsAsync(includeDossier: true);

        if (presets.Count == 0)
        {
            return Completed(command,
                Message(
                    UiNotificationSeverity.Info,
                    "Извечные хранители",
                    "В библиотеке пока нет пресетов."));
        }

        return Completed(command,
            new UiEntityDossierBlock
            {
                EntityType = "system-guardians",
                Title = "Извечные хранители",
                Summary = "Глобальные архетипы Хранителей, которых душа может встретить или целенаправленно искать.",
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "system-guardian-presets",
                        Title = "Доступные Хранители",
                        Icon = "shield",
                        Presentation = "collection",
                        CollectionLabel = presets.Count == 1 ? "1 хранитель" : $"{presets.Count} хранителей",
                        Cards = presets.Select(BuildSystemGuardianPresetCard).ToList()
                    }
                ]
            });
    }

    private static UiEntityCard BuildSystemGuardianPresetCard(SystemGuardianLibraryService.SystemGuardianPresetDescriptor preset)
    {
        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Обитель", preset.AbodeName);
        AddFactIfKnown(facts, "Темы", preset.CoreValues.Count == 0 ? string.Empty : string.Join(", ", preset.CoreValues));
        AddFactIfKnown(facts, "Образ", preset.DefaultAppearanceDescription);

        var nested = new List<UiEntityCard>();
        var dossier = ToPlayerFacingMarkdownSnippet(preset.DossierMarkdown);
        if (!IsUnknownReadableJsonValue(dossier))
        {
            nested.Add(new UiEntityCard
            {
                Title = "Досье",
                Icon = "scroll",
                Summary = dossier
            });
        }

        return new UiEntityCard
        {
            Title = FirstNonEmpty(preset.DisplayName, preset.PresetId, "Извечный хранитель"),
            Subtitle = "Системный Хранитель",
            Summary = FirstNonEmpty(preset.Summary, "Краткое описание пока не добавлено."),
            Icon = "shield",
            Facts = facts,
            Nested = nested
        };
    }

    private static string ToPlayerFacingMarkdownSnippet(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return "не указано";

        var lines = markdown
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static line => line.TrimStart('#', ' ', '\t'))
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Take(6)
            .ToArray();

        return lines.Length == 0 ? "не указано" : string.Join(Environment.NewLine, lines);
    }

    private static async Task<ExplorerCommandResult> BuildSarefStory(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, SarefMainStoryState.StatePath);
        if (read.Node == null)
        {
            if (read.FileExists)
                return MissingOrMalformed(command, "Крылья над Бездной", read);

            return Completed(command,
                Message(
                    UiNotificationSeverity.Info,
                    "Крылья над Бездной",
                    "Ты пока не знаешь, что искать."));
        }

        if (read.Node is not JsonObject root)
        {
            return Completed(command,
                Message(
                    UiNotificationSeverity.Warning,
                    "Крылья над Бездной",
                    $"{SarefMainStoryState.StatePath} должен быть JSON object."));
        }

        if (IsSarefStoryStillUnknown(root))
        {
            return Completed(command,
                Message(
                    UiNotificationSeverity.Info,
                    "Крылья над Бездной",
                    "Ты пока не знаешь, что искать."));
        }

        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Стадия раскрытия", DescribeSarefRevealStage(GetString(root, "revealStage", SarefMainStoryState.RevealStageUnknown)));
        AddFactIfKnown(facts, "Фрагментов", CountArray(root, "sarefRevelations").ToString());
        AddFactIfKnown(facts, "Преимуществ", CountArray(root, "sarefAdvantages").ToString());
        AddFactIfKnown(facts, "Использований преимуществ", CountArray(root, "sarefAdvantageUses").ToString());
        AddFactIfKnown(facts, "Известных агентов", CountNestedArray(root, "factionLinks", "knownAgents").ToString());
        AddFactIfKnown(facts, "Финал", DescribeSarefNestedStatus(root["finalConfrontation"] as JsonObject));
        AddFactIfKnown(facts, "Клятва", DescribeSarefNestedStatus(root["playerOathState"] as JsonObject));

        var sections = new List<UiEntityDossierSection>();
        AddSectionIfPresent(sections, BuildSarefStoryArraySection("Раскрытые фрагменты", "saref-revelations", root["sarefRevelations"] as JsonArray, "Фрагмент"));
        AddSectionIfPresent(sections, BuildSarefStoryArraySection("Преимущества", "saref-advantages", root["sarefAdvantages"] as JsonArray, "Преимущество"));
        AddSectionIfPresent(sections, BuildSarefStoryArraySection("Использования преимуществ", "saref-advantage-uses", root["sarefAdvantageUses"] as JsonArray, "Использование"));
        AddSectionIfPresent(sections, BuildSarefFactionLinksSection(root["factionLinks"] as JsonObject));
        AddSectionIfPresent(sections, BuildSarefStoryArraySection("Возможные исходы", "saref-defeat-outcomes", root["defeatOutcomes"] as JsonArray, "Исход"));
        AddSectionIfPresent(sections, BuildSarefStoryArraySection("Финалы", "saref-endings", root["endings"] as JsonArray, "Финал"));

        var blocks = new List<UiBlock>
        {
            new UiEntityDossierBlock
            {
                EntityType = "saref-story",
                Title = "Крылья над Бездной",
                Summary = "Скрытая сюжетная линия Сарефа: раскрытые фрагменты, преимущества, связи и возможные исходы.",
                Facts = facts,
                Sections = sections
            }
        };

        return Completed(command, blocks);
    }

    private static string DescribeSarefNestedStatus(JsonObject? obj)
    {
        if (obj == null)
            return "не указано";

        return FirstReadableJsonValue(
            DescribeSarefGenericStatus(GetString(obj, "status", string.Empty)),
            DescribeSarefGenericStatus(GetString(obj, "state", string.Empty)),
            GetString(obj, "summary", string.Empty),
            GetString(obj, "currentObjective", string.Empty));
    }

    private static UiEntityDossierSection? BuildSarefStoryArraySection(
        string title,
        string id,
        JsonArray? array,
        string fallbackPrefix)
    {
        var cards = array?
            .OfType<JsonObject>()
            .Select((item, index) => BuildSarefStoryCard(item, fallbackPrefix, index))
            .Where(static card => !string.IsNullOrWhiteSpace(card.Title))
            .ToList() ?? [];

        if (cards.Count == 0)
            return null;

        return new UiEntityDossierSection
        {
            Id = id,
            Title = title,
            Icon = "scroll",
            Presentation = "cards",
            CollectionLabel = cards.Count == 1 ? "1 запись" : $"{cards.Count} записей",
            Cards = cards
        };
    }

    private static UiEntityCard BuildSarefStoryCard(JsonObject item, string fallbackPrefix, int index)
    {
        var category = TranslateSarefCategory(GetString(item, "category", string.Empty));
        var status = FirstReadableJsonValue(
            DescribeSarefGenericStatus(GetString(item, "status", string.Empty)),
            DescribeSarefGenericStatus(GetString(item, "state", string.Empty)));
        var title = FirstReadableJsonValue(
            GetString(item, "name", string.Empty),
            GetString(item, "displayName", string.Empty),
            GetString(item, "title", string.Empty),
            IsUnknownReadableJsonValue(category) ? string.Empty : category,
            $"{fallbackPrefix} {index + 1}");
        var summary = FirstReadableJsonValue(
            GetString(item, "summary", string.Empty),
            GetString(item, "description", string.Empty),
            GetString(item, "outcome", string.Empty),
            GetString(item, "result", string.Empty),
            "Подробности пока не записаны.");

        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Источник", FormatSarefSource(item));
        AddFactIfKnown(facts, "Ход раскрытия", GetString(item, "revealedAtTurn", string.Empty));
        AddFactIfKnown(facts, "Состояние", status);
        AddFactIfKnown(facts, "Категория", category);

        var list = BuildSarefStoryList(item);

        return new UiEntityCard
        {
            Title = title,
            Subtitle = IsUnknownReadableJsonValue(status) ? fallbackPrefix : status,
            Summary = summary,
            Icon = "scroll",
            Facts = facts,
            List = list
        };
    }

    private static List<string> BuildSarefStoryList(JsonObject item)
    {
        var list = new List<string>();
        AddSarefStringArrayItems(list, item["applicableScenes"], TranslateSarefSceneType);
        AddSarefStringArrayItems(list, item["requirements"], static value => value);
        AddSarefStringArrayItems(list, item["consequences"], static value => value);
        return list;
    }

    private static void AddSarefStringArrayItems(List<string> list, JsonNode? node, Func<string, string> map)
    {
        if (node is not JsonArray array)
            return;

        foreach (var value in array)
        {
            if (!TryGetScalarString(value, out var text) || string.IsNullOrWhiteSpace(text))
                continue;

            var mapped = map(text);
            if (!IsUnknownReadableJsonValue(mapped))
                list.Add(mapped);
        }
    }

    private static UiEntityDossierSection? BuildSarefFactionLinksSection(JsonObject? factionLinks)
    {
        if (factionLinks == null)
            return null;

        var cards = new List<UiEntityCard>();
        AddSarefFactionLinkCards(cards, "Тени", factionLinks["shadowTraces"] as JsonArray);
        AddSarefFactionLinkCards(cards, "Агенты", factionLinks["knownAgents"] as JsonArray);
        AddSarefFactionLinkCards(cards, "Фракции", factionLinks["factions"] as JsonArray);

        var wingsFaction = GetString(factionLinks, "wingsFactionId", string.Empty);
        if (!string.IsNullOrWhiteSpace(wingsFaction))
        {
            cards.Add(new UiEntityCard
            {
                Title = "Крылья Ангелов",
                Subtitle = "Фракционная связь",
                Summary = "Линия Сарефа связана с фракцией Крыльев Ангелов.",
                Icon = "users"
            });
        }

        if (cards.Count == 0)
            return null;

        return new UiEntityDossierSection
        {
            Id = "saref-faction-links",
            Title = "Связи Сарефа",
            Icon = "users",
            Presentation = "cards",
            CollectionLabel = cards.Count == 1 ? "1 связь" : $"{cards.Count} связей",
            Cards = cards
        };
    }

    private static void AddSarefFactionLinkCards(List<UiEntityCard> cards, string prefix, JsonArray? array)
    {
        if (array == null)
            return;

        cards.AddRange(array
            .OfType<JsonObject>()
            .Select((item, index) => BuildSarefStoryCard(item, prefix, index)));
    }

    private static string TranslateSarefSceneType(string sceneType) =>
        sceneType.Trim().ToLowerInvariant() switch
        {
            "oath_break" => "разрыв клятвы",
            "saref_confrontation" => "столкновение с Сарефом",
            "combat" => "бой",
            "negotiation" => "переговоры",
            _ => sceneType.Trim()
        };

    private static string FormatSarefSource(JsonObject item)
    {
        var guardian = GetString(item, "sourceGuardianId", string.Empty);
        var questOrdinal = GetNumberOrString(item, "sourceQuestOrdinal");
        if (IsUnknownReadableJsonValue(questOrdinal))
            questOrdinal = string.Empty;
        if (string.IsNullOrWhiteSpace(guardian) && string.IsNullOrWhiteSpace(questOrdinal))
            return string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(guardian))
            parts.Add("память Хранителя");
        if (!string.IsNullOrWhiteSpace(questOrdinal))
            parts.Add($"квест {questOrdinal}");
        return string.Join(", ", parts);
    }

    private static string DescribeSarefGenericStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "available" => "доступно",
            "active" => "активно",
            "used" => "использовано",
            "completed" => "завершено",
            "failed" => "провалено",
            "locked" => "закрыто",
            "hidden" => "скрыто",
            _ => EmptyFallback(status)
        };

    private static string TranslateSarefCategory(string category) =>
        category.ToLowerInvariant() switch
        {
            "identity" => "личность",
            "faction" => "фракции",
            "oath" => "клятва",
            "memory" => "память",
            "route" => "маршрут",
            "method" => "метод",
            "path" => "путь",
            _ => EmptyFallback(category)
        };

    private static async Task<ExplorerCommandResult> BuildSarefMemoryScene(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, SarefMainStoryState.StatePath);
        if (read.Node is not JsonObject root)
        {
            var message = read.FileExists
                ? "Состояние скрытой линии найдено, но его не удалось прочитать. Запустите проверку данных и исправьте запись Сарефа."
                : "Активного Воспоминания нет. Это не Врата Памяти и не Наследие Памяти: Воспоминание появляется только как особый слой 4-го квеста Хранителя в линии Сарефа.";
            return Completed(command, Message(read.FileExists ? UiNotificationSeverity.Warning : UiNotificationSeverity.Info, "Воспоминание", message));
        }

        if (root["memoryScene"] is not JsonObject scene)
        {
            return Completed(command,
                Message(
                    UiNotificationSeverity.Info,
                    "Воспоминание",
                    "Активного Воспоминания нет. Это не Врата Памяти и не Наследие Памяти: Воспоминание появляется только как особый слой 4-го квеста Хранителя в линии Сарефа."));
        }

        var title = GetString(scene, "title", GetString(scene, "sceneTitle", GetString(scene, "sceneId", "без названия")));
        var role = scene["role"] as JsonObject;
        var roleName = role == null
            ? "не указана"
            : GetString(role, "displayName", GetString(role, "roleId", "не указана"));
        var roleSummary = role == null ? string.Empty : GetString(role, "summary", string.Empty);

        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Сцена", title);
        AddFactIfKnown(facts, "Состояние", DescribeSarefMemorySceneStatus(GetString(scene, "status", string.Empty)));
        AddFactIfKnown(facts, "Память Хранителя", GetString(scene, "guardianId"));
        AddFactIfKnown(facts, "Квест", JoinNonEmpty(" / ", GetString(scene, "questId", string.Empty), GetNumberOrString(scene, "questOrdinal")));
        AddFactIfKnown(facts, "Роль внутри сцены", JoinNonEmpty(" - ", roleName, roleSummary));

        var sections = new List<UiEntityDossierSection>();
        AddSectionIfPresent(sections, BuildMemorySceneObjectSection("Границы сцены", "memory-boundaries", scene["boundaries"] as JsonArray, "boundaryId", preferName: false));
        AddSectionIfPresent(sections, BuildMemorySceneObjectSection("Доступные способности", "memory-abilities", scene["abilities"] as JsonArray, "abilityId", preferName: true));
        AddSectionIfPresent(sections, BuildMemorySceneNodeSection(scene["requiredStoryNodes"] as JsonArray));
        sections.Add(new UiEntityDossierSection
        {
            Id = "memory-success-condition",
            Title = "Условие успеха",
            Icon = "target",
            Presentation = "cards",
            Cards = [BuildMemorySceneSuccessConditionCard(scene["successCondition"] as JsonObject)]
        });
        sections.Add(new UiEntityDossierSection
        {
            Id = "memory-closure-target",
            Title = "Что закрывает сцена",
            Icon = "check",
            Presentation = "cards",
            Cards = [BuildMemorySceneClosureTargetCard(scene["closureTarget"] as JsonObject)]
        });

        var blocks = new List<UiBlock>
        {
            new UiEntityDossierBlock
            {
                EntityType = "saref-memory-scene",
                Title = "Воспоминание",
                Subtitle = title,
                Summary = "Особый слой памяти в линии Сарефа: сцена показывает прошлый факт, но не позволяет напрямую переписать историю.",
                Facts = facts,
                Sections = sections
            },
            new UiTextBlock
            {
                Tone = UiTone.Warning,
                Text = "Это не Врата Памяти и не Наследие Памяти. Смертный инвентарь не переносится; исторический факт нельзя напрямую переписать."
            }
        };

        return Completed(command, blocks);
    }

    private static void AddSectionIfPresent(List<UiEntityDossierSection> sections, UiEntityDossierSection? section)
    {
        if (section != null)
            sections.Add(section);
    }

    private static UiEntityDossierSection? BuildMemorySceneObjectSection(
        string title,
        string id,
        JsonArray? array,
        string idProperty,
        bool preferName)
    {
        var cards = array?
            .OfType<JsonObject>()
            .Select(item => BuildMemorySceneObjectCard(item, idProperty, preferName))
            .Where(static card => !string.IsNullOrWhiteSpace(card.Title))
            .ToList() ?? [];

        if (cards.Count == 0)
            return null;

        return new UiEntityDossierSection
        {
            Id = id,
            Title = title,
            Icon = "sparkles",
            Presentation = "cards",
            CollectionLabel = cards.Count == 1 ? "1 запись" : $"{cards.Count} записей",
            Cards = cards
        };
    }

    private static UiEntityCard BuildMemorySceneObjectCard(JsonObject item, string idProperty, bool preferName)
    {
        var name = preferName
            ? GetString(item, "name", GetString(item, "displayName", GetString(item, idProperty)))
            : GetString(item, "displayName", GetString(item, "name", GetString(item, idProperty)));
        var summary = FirstNonEmpty(
            GetString(item, "summary", string.Empty),
            GetString(item, "description", string.Empty),
            "Описание пока не указано.");

        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Состояние", DescribeSarefGenericStatus(GetString(item, "status", string.Empty)));
        AddFactIfKnown(facts, "Категория", TranslateSarefCategory(GetString(item, "category", string.Empty)));

        return new UiEntityCard
        {
            Title = name,
            Summary = summary,
            Icon = "sparkles",
            Facts = facts
        };
    }

    private static UiEntityDossierSection? BuildMemorySceneNodeSection(JsonArray? nodes)
    {
        var cards = nodes?
            .OfType<JsonObject>()
            .Select(BuildMemorySceneNodeCard)
            .ToList() ?? [];

        if (cards.Count == 0)
            return null;

        return new UiEntityDossierSection
        {
            Id = "memory-required-nodes",
            Title = "Обязательные сюжетные узлы",
            Icon = "history",
            Presentation = "cards",
            CollectionLabel = cards.Count == 1 ? "1 узел" : $"{cards.Count} узлов",
            Cards = cards
        };
    }

    private static UiEntityCard BuildMemorySceneNodeCard(JsonObject node)
    {
        var status = DescribeSarefMemorySceneNodeStatus(GetString(node, "status", string.Empty));
        return new UiEntityCard
        {
            Title = FirstNonEmpty(GetString(node, "summary", string.Empty), GetString(node, "title", string.Empty), "Сюжетный узел"),
            Subtitle = status,
            Summary = status,
            Icon = "history",
            Badges =
            [
                new UiEntityBadge { Label = status, Icon = "check", Tone = string.Equals(status, "завершён", StringComparison.OrdinalIgnoreCase) ? UiTone.Success : UiTone.Warning }
            ]
        };
    }

    private static UiEntityCard BuildMemorySceneSuccessConditionCard(JsonObject? condition)
    {
        if (condition == null)
        {
            return new UiEntityCard
            {
                Title = "Условие успеха",
                Summary = "Условие успеха пока не указано.",
                Icon = "target"
            };
        }

        var satisfied = GetBool(condition, "satisfied");
        return new UiEntityCard
        {
            Title = "Условие успеха",
            Summary = GetString(condition, "summary", GetString(condition, "conditionId")),
            Icon = "target",
            Badges =
            [
                new UiEntityBadge { Label = satisfied ? "выполнено" : "ещё не выполнено", Icon = "check", Tone = satisfied ? UiTone.Success : UiTone.Warning }
            ],
            Facts =
            [
                new UiEntityFact { Label = "Состояние", Value = satisfied ? "выполнено" : "ещё не выполнено" }
            ]
        };
    }

    private static UiEntityCard BuildMemorySceneClosureTargetCard(JsonObject? target)
    {
        if (target == null)
        {
            return new UiEntityCard
            {
                Title = "Что закрывает сцена",
                Summary = "Цель закрытия пока не указана.",
                Icon = "check"
            };
        }

        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Хранитель", GetString(target, "guardianId"));
        AddFactIfKnown(facts, "Квест", JoinNonEmpty(" / ", GetString(target, "questId", string.Empty), GetNumberOrString(target, "questOrdinal")));
        AddFactIfKnown(facts, "Фрагмент истины", GetString(target, "revelationId"));
        AddFactIfKnown(facts, "Преимущество", GetString(target, "advantageId"));

        return new UiEntityCard
        {
            Title = "Что закрывает сцена",
            Summary = facts.Count == 0 ? "Цель закрытия пока не указана." : "Эта сцена закрывает конкретный долг памяти.",
            Icon = "check",
            Facts = facts
        };
    }

    private static async Task<ExplorerCommandResult> BuildSoulQuests(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, "game_state/quests/soul_quests.json");
        if (read.Node is not JsonObject root)
        {
            return Completed(command,
                new UiEntityDossierBlock
                {
                    EntityType = "soul-quests",
                    Title = "Квесты души",
                    Subtitle = "Духовные поручения и долгие линии души",
                    Summary = "Активных квестов души сейчас нет.",
                    Badges =
                    [
                        new UiEntityBadge { Label = "0 квестов", Icon = "quests", Tone = UiTone.Muted }
                    ]
                });
        }

        var quests = root["quests"] as JsonArray ?? root["UpdateSoulQuests"] as JsonArray;
        var questCards = quests?
            .OfType<JsonObject>()
            .Select(quest => BuildSoulQuestCard(quest, BuildGuardianNameLookup(root, fs)))
            .Where(static card => !string.IsNullOrWhiteSpace(card.Title))
            .ToList() ?? [];

        var activeCount = quests?.OfType<JsonObject>().Count(static quest =>
            string.Equals(GetString(quest, "status", string.Empty), "active", StringComparison.OrdinalIgnoreCase)) ?? 0;

        return Completed(command,
            new UiEntityDossierBlock
            {
                EntityType = "soul-quests",
                Title = "Квесты души",
                Subtitle = "Духовные поручения и долгие линии души",
                Summary = questCards.Count == 0
                    ? "Активных квестов души сейчас нет."
                    : "Квесты, которые переживают отдельные жизни и связывают душу с Хранителями, Обителями и будущими возвращениями.",
                Badges =
                [
                    new UiEntityBadge { Label = $"{questCards.Count} квестов", Icon = "quests", Tone = UiTone.Accent },
                    new UiEntityBadge { Label = $"{activeCount} активных", Icon = "sparkles", Tone = activeCount > 0 ? UiTone.Warning : UiTone.Muted }
                ],
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "soul-quest-list",
                        Title = "Текущие квесты",
                        Icon = "quests",
                        Presentation = "cards",
                        CollectionLabel = $"{questCards.Count} записей",
                        Cards = questCards
                    }
                ]
            });
    }

    private static UiEntityCard BuildSoulQuestCard(JsonObject quest, IReadOnlyDictionary<string, string> guardianNames)
    {
        var title = FirstNonEmpty(
            GetString(quest, "title", string.Empty),
            GetString(quest, "questName", string.Empty),
            "Квест души");
        var status = DescribeSoulQuestStatus(GetString(quest, "status", string.Empty));
        var summary = FirstNonEmpty(
            GetString(quest, "description", string.Empty),
            GetString(quest, "summary", string.Empty),
            "Описание пока не записано.");

        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Хранитель", DescribeSoulQuestGuardian(GetString(quest, "guardianId", string.Empty), guardianNames));
        AddFactIfKnown(facts, "Прогресс", DescribeSoulQuestProgress(quest["progress"] as JsonObject));
        AddFactIfKnown(facts, "Связь между жизнями", GetBool(quest, "crossIncarnation") ? "сохраняется между воплощениями" : string.Empty);

        var nested = new List<UiEntityCard>();
        nested.AddRange(BuildSoulQuestObjectiveCards(quest["objectives"] as JsonArray));
        var rewards = BuildSoulQuestRewardCard(quest["rewards"] as JsonObject);
        if (rewards != null)
            nested.Add(rewards);

        return new UiEntityCard
        {
            Title = title,
            Subtitle = status,
            Icon = "quests",
            Summary = summary,
            Badges =
            [
                new UiEntityBadge { Label = status, Icon = "sparkles", Tone = DescribeSoulQuestTone(status) }
            ],
            Facts = facts,
            Nested = nested
        };
    }

    private static IEnumerable<UiEntityCard> BuildSoulQuestObjectiveCards(JsonArray? objectives)
    {
        if (objectives == null)
            yield break;

        foreach (var (objective, index) in objectives.OfType<JsonObject>().Select((objective, index) => (objective, index)))
        {
            var status = DescribeSoulQuestStatus(GetString(objective, "status", string.Empty));
            yield return new UiEntityCard
            {
                Title = FirstNonEmpty(GetString(objective, "description", string.Empty), $"Цель {index + 1}"),
                Subtitle = "Цель квеста",
                Icon = "target",
                Summary = status,
                Badges =
                [
                    new UiEntityBadge { Label = status, Icon = "check", Tone = DescribeSoulQuestTone(status) }
                ]
            };
        }
    }

    private static UiEntityCard? BuildSoulQuestRewardCard(JsonObject? rewards)
    {
        if (rewards == null)
            return null;

        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Чернильные Перья", GetNumberOrString(rewards, "inkFeathers"));
        AddFactIfKnown(facts, "Искры Света", GetNumberOrString(rewards, "lightSparks"));
        AddFactIfKnown(facts, "Опыт просветления", GetNumberOrString(rewards, "enlightenmentExperience"));

        var notes = rewards["notes"] is JsonArray notesArray
            ? notesArray.Select(DescribeReadableJsonValue).Where(static value => !IsUnknownReadableJsonValue(value)).ToList()
            : [];

        if (facts.Count == 0 && notes.Count == 0)
            return null;

        return new UiEntityCard
        {
            Title = "Награды",
            Subtitle = "Что может дать завершение",
            Icon = "reward",
            Facts = facts,
            List = notes
        };
    }

    private static IReadOnlyDictionary<string, string> BuildGuardianNameLookup(JsonObject soulQuestRoot, FileSystemManager fs)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddGuardianNamesFromNode(soulQuestRoot, names);

        var read = ReadJson(fs, "game_state/meta/guardians.json").GetAwaiter().GetResult();
        AddGuardianNamesFromNode(read.Node, names);
        return names;
    }

    private static void AddGuardianNamesFromNode(JsonNode? node, Dictionary<string, string> names)
    {
        if (node is JsonObject obj)
        {
            var id = FirstNonEmpty(
                GetString(obj, "guardianId", string.Empty),
                GetString(obj, "id", string.Empty),
                GetString(obj, "profileId", string.Empty));
            var name = FirstNonEmpty(
                GetString(obj, "currentDisplayName", string.Empty),
                GetString(obj, "displayName", string.Empty),
                GetString(obj, "name", string.Empty),
                GetString(obj, "canonicalName", string.Empty));

            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                names[id] = name;

            foreach (var child in obj.Select(static property => property.Value))
                AddGuardianNamesFromNode(child, names);
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
                AddGuardianNamesFromNode(child, names);
        }
    }

    private static string DescribeSoulQuestGuardian(string guardianId, IReadOnlyDictionary<string, string> guardianNames)
    {
        if (string.IsNullOrWhiteSpace(guardianId))
            return string.Empty;

        return guardianNames.TryGetValue(guardianId, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : "Хранитель";
    }

    private static string DescribeSoulQuestProgress(JsonObject? progress)
    {
        if (progress == null)
            return string.Empty;

        var summary = GetString(progress, "summary", string.Empty);
        var completed = GetNumberOrString(progress, "completedObjectives");
        var total = GetNumberOrString(progress, "totalObjectives");
        var numeric = !IsUnknownReadableJsonValue(completed) && !IsUnknownReadableJsonValue(total)
            ? $"{completed}/{total}"
            : string.Empty;
        return JoinNonEmpty(" - ", numeric, summary);
    }

    private static string DescribeSoulQuestStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "active" => "активен",
            "completed" => "завершён",
            "failed" => "провален",
            "available" => "доступен",
            "pending" => "ожидает",
            "locked" => "закрыт",
            _ => EmptyFallback(status)
        };

    private static UiTone DescribeSoulQuestTone(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "активен" or "доступен" => UiTone.Warning,
            "завершён" => UiTone.Success,
            "провален" => UiTone.Error,
            _ => UiTone.Muted
        };

    private static void AddFactIfKnown(List<UiEntityFact> facts, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || IsUnknownReadableJsonValue(value))
            return;

        facts.Add(new UiEntityFact { Label = label, Value = value });
    }

    private static async Task<ExplorerCommandResult> BuildJsonFile(
        string command,
        FileSystemManager fs,
        string title,
        string path,
        Func<JsonNode, UiBlock>? summaryBuilder = null)
    {
        var read = await ReadJson(fs, path);
        if (read.Node == null)
            return MissingOrMalformed(command, title, read);

        var blocks = new List<UiBlock>();
        if (summaryBuilder != null)
            blocks.Add(summaryBuilder(read.Node));

        blocks.AddRange(BuildReadableJsonBlocks(title, read.Node));
        if (blocks.Count == 0)
            blocks.Add(Panel(title, Grid(("Статус", "прочитано"))));

        return Completed(command, blocks);
    }

    private static IReadOnlyList<UiBlock> BuildReadableJsonBlocks(string title, JsonNode node)
    {
        var dossier = BuildReadableJsonDossier(title, "metadata", node, initiallyExpandedSections: 3);
        return dossier == null ? [] : [dossier];
    }

    private static UiEntityDossierBlock? BuildReadableJsonDossier(
        string title,
        string entityType,
        JsonNode? node,
        int initiallyExpandedSections = 2)
    {
        if (node is JsonArray array)
        {
            var section = BuildReadableJsonArraySection(title, "items", "Записи", array, 0);
            if (section == null)
                return null;

            return new UiEntityDossierBlock
            {
                EntityType = entityType,
                Title = title,
                Sections = [section]
            };
        }

        if (node is not JsonObject obj)
        {
            if (TryGetScalarString(node, out var scalar))
            {
                return new UiEntityDossierBlock
                {
                    EntityType = entityType,
                    Title = title,
                    Facts = [new UiEntityFact { Label = "Значение", Value = scalar }]
                };
            }

            return null;
        }

        var facts = BuildReadableJsonFacts(obj);
        var sections = BuildReadableJsonSections(obj, initiallyExpandedSections);

        if (facts.Count == 0 && sections.Count == 0)
            return null;

        return new UiEntityDossierBlock
        {
            EntityType = entityType,
            Title = title,
            Facts = facts,
            Sections = sections
        };
    }

    private static List<UiEntityFact> BuildReadableJsonFacts(JsonObject obj)
    {
        var facts = new List<UiEntityFact>();
        foreach (var property in obj)
        {
            if (IsTechnicalReadableJsonProperty(property.Key))
                continue;

            if (property.Value is JsonObject or JsonArray)
                continue;

            var value = DescribeReadableJsonValue(property.Value);
            if (!IsUnknownReadableJsonValue(value))
                facts.Add(new UiEntityFact { Label = TranslateReadableJsonField(property.Key), Value = value });
        }

        return facts;
    }

    private static List<UiEntityDossierSection> BuildReadableJsonSections(JsonObject obj, int initiallyExpandedSections)
    {
        var sections = new List<UiEntityDossierSection>();
        foreach (var property in obj)
        {
            if (IsTechnicalReadableJsonProperty(property.Key))
                continue;

            UiEntityDossierSection? section = null;
            var label = TranslateReadableJsonField(property.Key);
            if (property.Value is JsonObject nestedObject)
            {
                section = BuildReadableJsonObjectSection(label, property.Key, nestedObject, sections.Count, initiallyExpandedSections);
            }
            else if (property.Value is JsonArray nestedArray)
            {
                section = BuildReadableJsonArraySection(label, property.Key, label, nestedArray, sections.Count);
            }

            if (section != null)
                sections.Add(section);
        }

        return sections;
    }

    private static UiEntityDossierSection? BuildReadableJsonObjectSection(
        string label,
        string propertyName,
        JsonObject obj,
        int sectionIndex,
        int initiallyExpandedSections)
    {
        var dossier = BuildReadableJsonDossier(label, "metadata-section", obj, initiallyExpandedSections: 1);
        if (dossier == null)
            return null;

        return new UiEntityDossierSection
        {
            Id = "json-" + propertyName,
            Title = label,
            Summary = DescribeReadableJsonPrimaryText(obj),
            Icon = "file-text",
            Collapsible = true,
            InitiallyExpanded = sectionIndex < initiallyExpandedSections,
            Blocks = [dossier]
        };
    }

    private static UiEntityDossierSection? BuildReadableJsonArraySection(
        string ownerTitle,
        string propertyName,
        string sectionLabel,
        JsonArray array,
        int sectionIndex)
    {
        if (array.Count == 0)
            return null;

        var cards = new List<UiEntityCard>();
        var list = new List<string>();
        var index = 0;
        foreach (var item in array.Take(24))
        {
            if (item is JsonObject obj)
            {
                var card = BuildReadableJsonCard(obj, index);
                if (card != null)
                    cards.Add(card);
            }
            else
            {
                var value = DescribeReadableJsonValue(item);
                if (!IsUnknownReadableJsonValue(value))
                    list.Add(value);
            }

            index++;
        }

        if (cards.Count == 0 && list.Count == 0)
            return null;

        var omitted = array.Count > index ? $"Показано {index} из {array.Count}." : string.Empty;
        return new UiEntityDossierSection
        {
            Id = "json-" + propertyName,
            Title = sectionLabel,
            Summary = omitted,
            Icon = "list",
            CollectionLabel = array.Count == 1 ? "1 запись" : $"{array.Count} записей",
            Presentation = cards.Count > 0 ? "collection" : "list",
            Collapsible = array.Count > 6 || sectionIndex > 1,
            InitiallyExpanded = sectionIndex < 2,
            Cards = cards,
            List = list
        };
    }

    private static UiEntityCard? BuildReadableJsonCard(JsonObject obj, int index)
    {
        var title = DescribeReadableJsonTitle(obj, index);
        var summary = DescribeReadableJsonPrimaryText(obj);
        var facts = BuildReadableJsonCardFacts(obj);
        var nested = BuildReadableJsonNestedCards(obj);

        if (IsUnknownReadableJsonValue(title) &&
            IsUnknownReadableJsonValue(summary) &&
            facts.Count == 0 &&
            nested.Count == 0)
        {
            return null;
        }

        return new UiEntityCard
        {
            Title = title,
            Summary = IsUnknownReadableJsonValue(summary) ? string.Empty : summary,
            Icon = "file-text",
            Facts = facts,
            Nested = nested
        };
    }

    private static List<UiEntityFact> BuildReadableJsonCardFacts(JsonObject obj)
    {
        var facts = new List<UiEntityFact>();
        foreach (var property in obj)
        {
            if (IsTechnicalReadableJsonProperty(property.Key) ||
                IsReadableJsonTitleOrSummaryProperty(property.Key) ||
                property.Value is JsonObject or JsonArray)
            {
                continue;
            }

            var value = DescribeReadableJsonValue(property.Value);
            if (!IsUnknownReadableJsonValue(value))
                facts.Add(new UiEntityFact { Label = TranslateReadableJsonField(property.Key), Value = value });
        }

        return facts;
    }

    private static List<UiEntityCard> BuildReadableJsonNestedCards(JsonObject obj)
    {
        var nested = new List<UiEntityCard>();
        foreach (var property in obj)
        {
            if (IsTechnicalReadableJsonProperty(property.Key))
                continue;

            var label = TranslateReadableJsonField(property.Key);
            if (property.Value is JsonObject nestedObject)
            {
                var card = BuildReadableJsonCard(nestedObject, nested.Count);
                if (card != null)
                    nested.Add(WithReadableTitle(card, label));
            }
            else if (property.Value is JsonArray nestedArray)
            {
                var list = nestedArray
                    .Select(DescribeReadableJsonValue)
                    .Where(static value => !IsUnknownReadableJsonValue(value))
                    .Take(12)
                    .ToList();
                if (list.Count > 0)
                    nested.Add(new UiEntityCard { Title = label, Icon = "list", List = list });
            }
        }

        return nested;

        static UiEntityCard WithReadableTitle(UiEntityCard source, string title) => new()
        {
            Title = title,
            Subtitle = source.Subtitle,
            Summary = source.Summary,
            Icon = source.Icon,
            Badges = source.Badges,
            Media = source.Media,
            Facts = source.Facts,
            Metrics = source.Metrics,
            Hints = source.Hints,
            List = source.List,
            Nested = source.Nested,
            Cards = source.Cards,
            PrimaryAction = source.PrimaryAction
        };
    }

    private static void AddReadableJsonArrayBlock(
        List<UiBlock> blocks,
        string title,
        string sectionLabel,
        JsonArray array)
    {
        if (array.Count == 0)
            return;

        var rows = array
            .Take(12)
            .Select((item, index) => Row(
                DescribeReadableJsonTitle(item, index),
                DescribeReadableJsonPrimaryText(item),
                DescribeReadableJsonSecondaryText(item)))
            .Where(static row => row.Cells.Any(static cell => !IsUnknownReadableJsonValue(cell)))
            .ToList();

        if (rows.Count == 0)
            return;

        blocks.Add(new UiTableBlock
        {
            Title = title + ": " + sectionLabel,
            Columns = ["Запись", "Описание", "Детали"],
            Rows = rows
        });
    }

    private static string DescribeReadableJsonTitle(JsonNode? node, int index)
    {
        if (node is JsonObject obj)
        {
            return FirstReadableJsonValue(
                GetString(obj, "title", string.Empty),
                GetString(obj, "displayName", string.Empty),
                GetString(obj, "name", string.Empty),
                GetString(obj, "questName", string.Empty),
                GetString(obj, "achievementName", string.Empty),
                GetString(obj, "dominantPattern", string.Empty),
                $"Запись {index + 1}");
        }

        var value = DescribeReadableJsonValue(node);
        return IsUnknownReadableJsonValue(value) ? $"Запись {index + 1}" : value;
    }

    private static string DescribeReadableJsonPrimaryText(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            return FirstReadableJsonValue(
                GetString(obj, "summary", string.Empty),
                GetString(obj, "description", string.Empty),
                GetString(obj, "content", string.Empty),
                GetString(obj, "text", string.Empty),
                GetString(obj, "details", string.Empty),
                DescribeReadableJsonValue(obj["playerBehaviorAssessment"]));
        }

        return DescribeReadableJsonValue(node);
    }

    private static string DescribeReadableJsonSecondaryText(JsonNode? node)
    {
        if (node is not JsonObject obj)
            return "не указано";

        var parts = obj
            .Where(static property => IsReadableJsonSecondaryProperty(property.Key))
            .Select(static property => $"{TranslateReadableJsonField(property.Key)}: {DescribeReadableJsonValue(property.Value)}")
            .Where(static value => !IsUnknownReadableJsonValue(value) && !value.EndsWith(": не указано", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToArray();
        return parts.Length == 0 ? "не указано" : string.Join("; ", parts);
    }

    private static string DescribeReadableJsonValue(JsonNode? node)
    {
        if (TryGetScalarString(node, out var scalar))
            return TranslateReadableJsonScalar(scalar);

        if (node is JsonArray array)
        {
            var values = array
                .Select(DescribeReadableJsonValue)
                .Where(static value => !IsUnknownReadableJsonValue(value))
                .Take(5)
                .ToArray();
            return values.Length == 0 ? "не указано" : string.Join("; ", values);
        }

        if (node is JsonObject obj)
        {
            var heading = FirstReadableJsonValue(
                GetString(obj, "title", string.Empty),
                GetString(obj, "displayName", string.Empty),
                GetString(obj, "name", string.Empty),
                GetString(obj, "dominantPattern", string.Empty));
            var description = FirstReadableJsonValue(
                GetString(obj, "summary", string.Empty),
                GetString(obj, "description", string.Empty),
                GetString(obj, "content", string.Empty));
            if (!IsUnknownReadableJsonValue(heading) && !IsUnknownReadableJsonValue(description))
                return heading + " — " + description;

            var direct = FirstReadableJsonValue(
                heading,
                description);
            if (!IsUnknownReadableJsonValue(direct))
                return direct;

            var parts = obj
                .Where(static property => !IsTechnicalReadableJsonProperty(property.Key))
                .Select(static property => $"{TranslateReadableJsonField(property.Key)}: {DescribeReadableJsonValue(property.Value)}")
                .Where(static value => !IsUnknownReadableJsonValue(value) && !value.EndsWith(": не указано", StringComparison.OrdinalIgnoreCase))
                .Take(6)
                .ToArray();
            return parts.Length == 0 ? "не указано" : string.Join("; ", parts);
        }

        return "не указано";
    }

    private static bool IsReadableJsonSecondaryProperty(string propertyName) =>
        propertyName is "category" or "type" or "status" or "state" or "rarity" or "tags" or
            "source" or "sourceName" or "sourceKind" or "createdAt" or "updatedAt" or
            "progress" or "current" or "total";

    private static bool IsReadableJsonTitleOrSummaryProperty(string propertyName) =>
        propertyName is "title" or "displayName" or "name" or "questName" or "achievementName" or
            "summary" or "description" or "content" or "text" or "details" or "dominantPattern";

    private static bool IsTechnicalReadableJsonProperty(string propertyName) =>
        propertyName.Equals("id", StringComparison.OrdinalIgnoreCase) ||
        propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("schemaVersion", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("debugNotes", StringComparison.OrdinalIgnoreCase) ||
        propertyName.StartsWith("_", StringComparison.OrdinalIgnoreCase);

    private static string TranslateReadableJsonField(string propertyName) =>
        propertyName switch
        {
            "entries" or "codexEntries" => "Записи",
            "playerBehaviorAssessment" => "Оценка поведения",
            "dominantPattern" => "Основной паттерн",
            "historyManipulationCoefficient" => "Коэффициент вмешательства в историю",
            "unlockedAchievements" => "Открытые достижения",
            "trackedProgress" => "Прогресс достижений",
            "worldTitle" => "Название мира",
            "worldDirectives" => "Директивы мира",
            "directives" => "Директивы",
            "rules" => "Правила",
            "summary" => "Кратко",
            "description" => "Описание",
            "content" => "Текст",
            "mainArc" => "Основная арка",
            "nextImmediateStep" => "Следующий шаг",
            "eventType" => "Тип события",
            "turnNumber" => "Ход",
            "category" => "Категория",
            "type" => "Тип",
            "status" or "state" => "Состояние",
            "rarity" => "Редкость",
            "tags" => "Метки",
            "source" or "sourceName" or "sourceKind" => "Источник",
            "createdAt" => "Создано",
            "updatedAt" => "Обновлено",
            "progress" => "Прогресс",
            "current" => "Сейчас",
            "total" => "Всего",
            "confidence" => "Уверенность",
            _ => propertyName.Replace('_', ' ')
        };

    private static string TranslateReadableJsonScalar(string value) =>
        value.Trim() switch
        {
            "high" => "высокая",
            "medium" => "средняя",
            "low" => "низкая",
            "unlocked" => "открыто",
            "locked" => "закрыто",
            "pending" => "ожидает",
            "rebirth" => "перерождение",
            _ => value
        };

    private static string FirstReadableJsonValue(params string[] values)
    {
        foreach (var value in values)
        {
            if (!IsUnknownReadableJsonValue(value))
                return value.Trim();
        }

        return "не указано";
    }

    private static bool IsUnknownReadableJsonValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        string.Equals(value.Trim(), "не указано", StringComparison.OrdinalIgnoreCase);

    private static UiBlock BuildCodexSummary(JsonNode node) =>
        Panel("Кодекс", Grid(
            ("Записей", (CountArray(node, "entries") + CountArray(node, "codexEntries")).ToString())));

    private static UiBlock BuildAchievementsSummary(JsonNode node) =>
        Panel("Достижения", Grid(
            ("Открыто", CountArray(node, "unlockedAchievements").ToString()),
            ("В процессе", CountArray(node, "trackedProgress").ToString())));

    private static ExplorerCommandResult MissingOrMalformed(string command, string title, JsonReadResult read) =>
        Completed(command,
            Message(
                read.FileExists ? UiNotificationSeverity.Warning : UiNotificationSeverity.Info,
                title,
                read.FileExists
                    ? "Запись найдена, но её не удалось прочитать как JSON."
                    : "Данные пока не созданы."));

    private static async Task AddRawJsonIfPresent(List<UiBlock> blocks, FileSystemManager fs, string path, string title)
    {
        var read = await ReadJson(fs, path);
        if (read.Node != null)
            blocks.Add(Raw(title, read.Node));
    }

    private static async Task<string> DescribeJsonFile(FileSystemManager fs, string path)
    {
        var read = await ReadJson(fs, path);
        if (read.Node != null)
            return "прочитано";
        return read.FileExists ? "повреждён" : "отсутствует";
    }

    private static string DescribeReadableJsonFileState(JsonReadResult read)
    {
        if (read.Node != null)
            return "прочитано";
        return read.FileExists ? "требует починки" : "пока нет данных";
    }

    private static async Task<JsonReadResult> ReadJson(FileSystemManager fs, string path)
    {
        var raw = await fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(raw))
            return new JsonReadResult(path, FileExists: fs.FileExists(path), Node: null, Error: string.Empty);

        try
        {
            return new JsonReadResult(path, FileExists: true, Node: JsonNode.Parse(raw), Error: string.Empty);
        }
        catch (JsonException ex)
        {
            return new JsonReadResult(path, FileExists: true, Node: null, Error: ex.Message);
        }
    }

    private static ExplorerCommandResult Completed(string command, params UiBlock[] blocks) =>
        Completed(command, blocks.AsEnumerable());

    private static ExplorerCommandResult Completed(string command, IEnumerable<UiBlock> blocks) =>
        new()
        {
            Command = command,
            State = CommandExecutionState.Completed,
            Blocks = blocks.ToList()
        };

    private static ExplorerCommandResult Completed(
        string command,
        IEnumerable<UiBlock> blocks,
        IEnumerable<UiAction> actions) =>
        new()
        {
            Command = command,
            State = CommandExecutionState.Completed,
            Blocks = blocks.ToList(),
            Actions = actions.ToList()
        };

    private static ExplorerCommandResult DetailUnavailable(string command, string title) =>
        Completed(
            command,
            Message(
                UiNotificationSeverity.Warning,
                title,
                "Не удалось открыть выбранную подробность: запись уже недоступна, устарела или не видна текущей душе."));

    private static UiPanelBlock Panel(string title, params UiBlock[] blocks) =>
        new()
        {
            Title = title,
            Blocks = blocks.ToList()
        };

    private static UiKeyValueGridBlock Grid(params (string Key, string Value)[] items) =>
        new()
        {
            Items = items
                .Select(static item => new UiKeyValueItem { Key = item.Key, Value = EmptyFallback(item.Value) })
                .ToList()
        };

    private static UiTableRow Row(params string[] cells) => new() { Cells = cells.ToList() };

    private static UiMessageBlock Message(UiNotificationSeverity severity, string title, string message) =>
        new()
        {
            Severity = severity,
            Title = title,
            Message = message
        };

    private static UiRawJsonBlock Raw(string title, JsonNode node) =>
        new()
        {
            Title = title,
            Json = node.DeepClone()
        };

    private static string ReadCommandArguments(string command)
    {
        var parts = command.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length < 2 ? string.Empty : parts[1].Trim();
    }

    private static bool TryReadDetailSelector(string arguments, out string selector, params string[] keywords)
    {
        selector = string.Empty;
        var trimmed = arguments.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return false;

        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && keywords.Any(keyword => string.Equals(parts[0], keyword, StringComparison.OrdinalIgnoreCase)))
        {
            selector = parts[1].Trim().Trim('"');
            return !string.IsNullOrWhiteSpace(selector);
        }

        return false;
    }

    private static string DescribeStringArray(JsonNode? node)
    {
        if (node is not JsonArray array)
            return "не указано";

        var values = array
            .Select(static item => item switch
            {
                JsonValue value when value.TryGetValue<string>(out var text) => text,
                JsonValue value when value.TryGetValue<int>(out var number) => number.ToString(),
                _ => string.Empty
            })
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        return values.Length == 0 ? "не указано" : string.Join(", ", values);
    }

    private static string DescribeArchiveEntryReservation(JsonObject entry)
    {
        var reservation = AfterlifeArchiveState.GetReservationObject(entry);
        if (reservation == null)
            return "свободна";

        var kind = AfterlifeArchiveState.GetReservationLabel(GetString(reservation, "reservationKind", string.Empty));
        var guardian = GetString(reservation, "guardianName", string.Empty);
        var project = GetString(reservation, "targetProjectName", string.Empty);
        return JoinNonEmpty(" / ", "зарезервирована", kind, guardian, project);
    }

    private static string DescribeArchiveCandidateStatus(string status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            AfterlifeArchiveCandidateService.StatusPending => "ожидает выбора",
            AfterlifeArchiveCandidateService.StatusArchived => "перенесён в Архив",
            AfterlifeArchiveCandidateService.StatusSkipped => "пропущен",
            _ => EmptyFallback(status)
        };

    private static string FormatRarityForPlayer(string? rarity, string missing = "не указана")
    {
        if (string.IsNullOrWhiteSpace(rarity))
            return missing;

        return rarity.Trim().ToLowerInvariant() switch
        {
            "common" => "обычное",
            "uncommon" => "необычное",
            "good" => "хорошее",
            "rare" => "редкое",
            "epic" => "эпическое",
            "legendary" => "легендарное",
            "radiant" => "сияющее",
            "unique" => "уникальное",
            _ => rarity.Trim()
        };
    }

    private static string GetString(JsonNode? node, string propertyName, string fallback = "не указано") =>
        TryGetScalarString(node?[propertyName], out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;

    private static bool TryGetScalarString(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue jsonValue)
            return false;

        if (jsonValue.TryGetValue<string>(out var text))
        {
            value = text;
            return true;
        }

        if (jsonValue.TryGetValue<int>(out var intValue))
        {
            value = intValue.ToString();
            return true;
        }

        if (jsonValue.TryGetValue<long>(out var longValue))
        {
            value = longValue.ToString();
            return true;
        }

        if (jsonValue.TryGetValue<double>(out var doubleValue))
        {
            value = doubleValue.ToString("G", CultureInfo.InvariantCulture);
            return true;
        }

        if (jsonValue.TryGetValue<decimal>(out var decimalValue))
        {
            value = decimalValue.ToString("G", CultureInfo.InvariantCulture);
            return true;
        }

        if (jsonValue.TryGetValue<bool>(out var boolValue))
        {
            value = boolValue ? "true" : "false";
            return true;
        }

        return false;
    }

    private static string GetNumberOrString(JsonNode? node, string propertyName)
    {
        var value = node?[propertyName];
        return value switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<int>(out var number) => number.ToString(),
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) => EmptyFallback(text),
            _ => "не указано"
        };
    }

    private static string DescribeInkFeathers(JsonNode? node)
    {
        var feathers = node?["inkFeathers"];
        return feathers switch
        {
            JsonValue value when value.TryGetValue<int>(out var number) => number.ToString(),
            JsonObject obj => JoinNonEmpty(" / ",
                obj["current"]?.ToString() is { Length: > 0 } current ? $"сейчас {current}" : string.Empty,
                obj["total"]?.ToString() is { Length: > 0 } total ? $"всего {total}" : string.Empty),
            _ => "не указано"
        };
    }

    private static int GetInkFeatherCurrent(JsonObject soulRoot, int fallback)
    {
        var feathers = soulRoot["inkFeathers"];
        if (feathers is JsonObject obj)
            return GetIntValue(obj["current"], fallback);

        return GetIntValue(feathers, fallback);
    }

    private static int GetInkFeatherTotal(JsonObject soulRoot, int fallback)
    {
        var feathers = soulRoot["inkFeathers"];
        if (feathers is JsonObject obj)
            return GetIntValue(obj["total"], fallback);

        return fallback;
    }

    private static int GetIntValue(JsonNode? node, int fallback)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var number))
                return number;
            if (value.TryGetValue<long>(out var longValue) && longValue is >= int.MinValue and <= int.MaxValue)
                return (int)longValue;
            if (value.TryGetValue<string>(out var text) && int.TryParse(text, out var parsed))
                return parsed;
        }

        return fallback;
    }

    private static int ComputeRevealFateCost(int currentFeathers) =>
        Math.Max(5, (int)(Math.Max(0, currentFeathers) * 0.10));

    private static int ComputeRewriteFateCost(int currentFeathers) =>
        Math.Max(15, (int)(Math.Max(0, currentFeathers) * 0.25));

    private static string DescribeNested(JsonNode? node, string propertyName)
    {
        if (node?[propertyName] is not JsonObject obj)
            return GetNumberOrString(node, propertyName);

        return JoinNonEmpty(" / ",
            GetOptionalString(obj, "currentTier"),
            GetOptionalString(obj, "tier"),
            GetOptionalString(obj, "experience") is { Length: > 0 } xp ? $"{xp} XP" : string.Empty,
            GetOptionalString(obj, "progressPercent") is { Length: > 0 } pct ? $"{pct}%" : string.Empty);
    }

    private static string GetOptionalString(JsonObject obj, string propertyName)
    {
        var node = obj[propertyName];
        return node switch
        {
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            JsonValue value when value.TryGetValue<int>(out var number) => number.ToString(),
            _ => string.Empty
        };
    }

    private static bool GetBool(JsonObject obj, string propertyName) =>
        obj[propertyName] is JsonValue value &&
        value.TryGetValue<bool>(out var boolValue) &&
        boolValue;

    private static int CountArray(JsonNode? node, string propertyName) =>
        node?[propertyName] is JsonArray array ? array.Count : 0;

    private static int CountNestedArray(JsonNode? node, string objectName, string arrayName) =>
        node?[objectName] is JsonObject obj && obj[arrayName] is JsonArray array ? array.Count : 0;

    private static bool IsSarefStoryStillUnknown(JsonObject root)
    {
        var revealStage = GetString(root, "revealStage", string.Empty);
        var hasContent = CountArray(root, "sarefRevelations") > 0 ||
                         CountArray(root, "sarefAdvantages") > 0 ||
                         CountArray(root, "sarefAdvantageUses") > 0 ||
                         CountNestedArray(root, "factionLinks", "shadowTraces") > 0 ||
                         CountNestedArray(root, "factionLinks", "knownAgents") > 0;

        return !hasContent &&
               (string.IsNullOrWhiteSpace(revealStage) ||
                string.Equals(revealStage, SarefMainStoryState.RevealStageUnknown, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(revealStage, SarefMainStoryState.RevealStageShadow, StringComparison.OrdinalIgnoreCase));
    }

    private static string DescribeSarefRevealStage(string stage) =>
        stage.ToLowerInvariant() switch
        {
            "unknown" => "ты пока не знаешь, что искать",
            "shadow" => "есть тень, но нет имени",
            "name_revealed" => "имя раскрыто",
            "wings_revealed" => "Крылья Ангелов раскрыты",
            "infiltration_active" => "идёт внедрение",
            "confrontation_available" => "можно выйти к финальному столкновению",
            "completed" => "линия завершена",
            _ => EmptyFallback(stage)
        };

    private static string DescribeSarefMemorySceneStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "available" => "доступно",
            "active" => "активно",
            "blocked" => "заблокировано",
            "completed" => "завершено",
            "failed" => "провалено",
            _ => EmptyFallback(status)
        };

    private static string DescribeSarefMemorySceneNodeStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "pending" => "ожидает",
            "active" => "активно",
            "completed" => "выполнено",
            "failed" => "провалено",
            _ => EmptyFallback(status)
        };

    private static string JoinNonEmpty(string separator, params string?[] values)
    {
        var parts = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim())
            .ToArray();
        return parts.Length == 0 ? "не указано" : string.Join(separator, parts);
    }

    private static string EmptyFallback(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "не указано" : value.Trim();

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static int SafeCountLines(string path)
    {
        try
        {
            return File.ReadLines(path, Encoding.UTF8).Count(static line => !string.IsNullOrWhiteSpace(line));
        }
        catch
        {
            return 0;
        }
    }

    private static long SafeFileLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static string ToRelativeGameSessionPath(FileSystemManager fs, string fullPath)
    {
        var root = fs.GameSessionPath;
        return Path.GetRelativePath(root, fullPath).Replace('\\', '/');
    }

    private static string ToRelativeBasePath(FileSystemManager fs, string fullPath) =>
        Path.GetRelativePath(fs.BasePath, fullPath).Replace('\\', '/');

    private readonly record struct SoulRelicJsonDetail(JsonObject Relic, string Status, string Slot);

    private sealed record JsonReadResult(string Path, bool FileExists, JsonNode? Node, string Error);
}
