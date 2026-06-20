using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
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
            CommandKind.Status => BuildStatus(normalizedCommand, stateManager),
            CommandKind.Soul => await BuildSoul(normalizedCommand, fs),
            CommandKind.SoulRelics => await BuildSoulRelics(normalizedCommand, fs),
            CommandKind.AfterlifeArchive => await BuildAfterlifeArchive(normalizedCommand, fs),
            CommandKind.ArchiveCandidates => await BuildArchiveCandidates(normalizedCommand, fs),
            CommandKind.SoulQuests => await BuildJsonFile(normalizedCommand, fs, "Квесты души", "game_state/meta/guardians.json"),
            CommandKind.Codex => await BuildJsonFile(normalizedCommand, fs, "Кодекс", "lore/codex_entries.json", BuildCodexSummary),
            CommandKind.Achievements => await BuildJsonFile(normalizedCommand, fs, "Достижения", "game_state/meta/achievements.json", BuildAchievementsSummary),
            CommandKind.Chronicle => await BuildChronicle(normalizedCommand, fs),
            CommandKind.Story => BuildStory(normalizedCommand, fs),
            CommandKind.Behavior => await BuildJsonFile(normalizedCommand, fs, "Поведение игрока", "game_state/meta/player_behavior.json"),
            CommandKind.Lives => await BuildSoulSection(normalizedCommand, fs, "История жизней", "livesHistory"),
            CommandKind.Feathers => await BuildInkFeathers(normalizedCommand, fs, stateManager),
            CommandKind.WorldRules => await BuildJsonFile(normalizedCommand, fs, "Досье текущего мира", WorldDirectiveService.ActiveDirectivesPath),
            CommandKind.Gallery => BuildGallery(normalizedCommand, fs),
            CommandKind.Gm => await BuildGmThoughts(normalizedCommand, fs, loc),
            CommandKind.Debug => BuildDebug(normalizedCommand, fs, stateManager, loc),
            CommandKind.Mods => BuildDirectoryList(normalizedCommand, fs, "Моды", "mods"),
            CommandKind.SystemGuardians => BuildSystemGuardians(normalizedCommand, fs),
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

    private static ExplorerCommandResult BuildStatus(string command, StateManager stateManager)
    {
        var state = stateManager.CurrentState;
        return Completed(command,
            Panel("Статус",
                Grid(
                    ("Царство", EmptyFallback(state.CurrentRealm)),
                    ("Душа", EmptyFallback(state.SoulName)),
                    ("Инкарнация", state.Incarnation.ToString()),
                    ("Персонаж", EmptyFallback(state.CharacterName)),
                    ("Класс / раса", JoinNonEmpty(" / ", state.CharacterClass, state.CharacterRace)),
                    ("Локация", EmptyFallback(state.CurrentLocation)),
                    ("Время мира", EmptyFallback(state.WorldTime)),
                    ("Состояние", EmptyFallback(state.PlayerStatus.CurrentCondition)),
                    ("Здоровье", EmptyFallback(state.PlayerStatus.HealthPercentage)),
                    ("Энергия", EmptyFallback(state.PlayerStatus.EnergyPercentage)),
                    ("Равновесие", EmptyFallback(state.PlayerStatus.PoisePercentage)),
                    ("Чернильные Перья", state.InkFeathers.ToString()),
                    ("Просветление", EmptyFallback(state.EnlightenmentTier)),
                    ("Активный Хранитель", EmptyFallback(state.ActiveGuardianName)),
                    ("Сияние", $"{state.ShiningRadianceExperience} XP / тир {state.ShiningRadianceTier}"),
                    ("Искры Света", state.ShiningLightSparks.ToString()))));
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
                    ("Царство", GetString(read.Node, "currentRealm")),
                    ("Инкарнация", GetNumberOrString(read.Node, "currentIncarnation")),
                    ("Чернильные Перья", DescribeInkFeathers(read.Node)),
                    ("Просветление", DescribeNested(read.Node, "enlightenment")),
                    ("Жизней в истории", CountArray(read.Node, "livesHistory").ToString()))),
            Raw("Полный JSON game_state/meta/soul_state.json", read.Node));
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
        var blocks = new List<UiBlock>
        {
            Panel(title,
                Grid(
                    ("Записей", section is JsonArray array ? array.Count.ToString() : "0"),
                    ("Статус", section == null ? "не найдено" : "найдено")))
        };

        if (section is JsonArray { Count: > 0 } history)
        {
            var rows = history
                .OfType<JsonObject>()
                .Select((life, index) => Row(
                    FirstNonEmpty(GetNumberOrString(life, "incarnation"), GetNumberOrString(life, "incarnationNumber"), (index + 1).ToString()),
                    DescribeLifeIdentity(life),
                    DescribeLifeOutcome(life),
                    DescribeLifeRewards(life)))
                .ToList();

            if (rows.Count > 0)
            {
                blocks.Add(new UiTableBlock
                {
                    Title = title,
                    Columns = ["Инкарнация", "Кем и где", "Итог", "Награды"],
                    Rows = rows
                });
            }
        }

        return Completed(command, blocks);
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

        var rows = new List<UiTableRow>();
        rows.AddRange(context.Equipped.Select(static relic => new UiTableRow
        {
            Cells =
            [
                "Экипировано",
                SoulRelicEquipmentService.FormatSlotLabel(relic.CurrentSlot),
                string.IsNullOrWhiteSpace(relic.Name) ? relic.RelicId : relic.Name,
                EmptyFallback(relic.Rarity),
                EmptyFallback(relic.RelicId)
            ]
        }));
        rows.AddRange(context.Stored.Select(static relic => new UiTableRow
        {
            Cells =
            [
                "Хранилище",
                DescribeSoulRelicCompatibleSlots(relic),
                string.IsNullOrWhiteSpace(relic.Name) ? relic.RelicId : relic.Name,
                EmptyFallback(relic.Rarity),
                EmptyFallback(relic.RelicId)
            ]
        }));

        var blocks = new List<UiBlock>
        {
            Panel("Реликвии души",
                Grid(
                    ("Состояние", "каноническое состояние души"),
                    ("Экипировано", context.Equipped.Count.ToString()),
                    ("В хранилище", context.Stored.Count.ToString()))),
            new UiTableBlock
            {
                Title = "Реликвии души",
                Columns = ["Статус", "Слот", "Реликвия", "Редкость", "ID"],
                Rows = rows
            }
        };

        if (rows.Count == 0)
        {
            blocks.Insert(1, Message(
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
        var blocks = new List<UiBlock>
        {
            Panel("Архив души",
                Grid(
                    ("Записей", entries.Count.ToString()),
                    ("Свободно для действий", entries.Count(entry => !AfterlifeArchiveState.IsReserved(entry)).ToString()))),
            new UiTableBlock
            {
                Title = "Записи Архива",
                Columns = ["Запись", "Тип", "Редкость", "Состояние"],
                Rows = entries
                    .Select(static entry => Row(
                        FirstNonEmpty(GetString(entry, "title", string.Empty), GetString(entry, "archiveId", string.Empty), "без названия"),
                        AfterlifeArchiveState.GetEntryTypeLabel(GetString(entry, "entryType", string.Empty)),
                        FirstNonEmpty(GetString(entry, "rarity", string.Empty), "Common"),
                        DescribeArchiveEntryReservation(entry)))
                    .ToList()
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

        var blocks = new List<UiBlock>
        {
            Panel("Кандидаты в Архив",
                Grid(
                    ("Жизнь-источник", FirstNonEmpty(GetString(root, "sourceLife", string.Empty), "не указана")),
                    ("Кандидатов", candidates.Count.ToString()))),
            new UiTableBlock
            {
                Title = "Кандидаты в Архив",
                Columns = ["Кандидат", "Тип", "Редкость", "Состояние"],
                Rows = candidates
                    .Select(static candidate => Row(
                        FirstNonEmpty(GetString(candidate, "title", string.Empty), GetString(candidate, "candidateId", string.Empty), "без названия"),
                        AfterlifeArchiveState.GetEntryTypeLabel(GetString(candidate, "proposedEntryType", string.Empty)),
                        FirstNonEmpty(GetString(candidate, "rarity", string.Empty), "Common"),
                        DescribeArchiveCandidateStatus(GetString(candidate, "status", string.Empty))))
                    .ToList()
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
        var blocks = new List<UiBlock>
        {
            Panel("Хроника", Grid(
                ("character_chronicle", await DescribeJsonFile(fs, "game_state/meta/character_chronicle.json")),
                ("player_chronicle", await DescribeJsonFile(fs, "lore/chaos_sea/player_chronicle.json")),
                ("plot_outline", await DescribeJsonFile(fs, "game_state/quests/plot_outline.json"))))
        };

        await AddRawJsonIfPresent(blocks, fs, "game_state/meta/character_chronicle.json", "JSON: character_chronicle");
        await AddRawJsonIfPresent(blocks, fs, "lore/chaos_sea/player_chronicle.json", "JSON: player_chronicle");
        await AddRawJsonIfPresent(blocks, fs, "game_state/quests/plot_outline.json", "JSON: plot_outline");
        return Completed(command, blocks);
    }

    private static ExplorerCommandResult BuildStory(string command, FileSystemManager fs)
    {
        var storiesDir = fs.ResolvePath("stories");
        if (!Directory.Exists(storiesDir))
            return Completed(command, Message(UiNotificationSeverity.Info, "Рассказ", "Папка stories пока не создана."));

        var rows = Directory.GetFiles(storiesDir, "*.jsonl", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new UiTableRow
            {
                Cells =
                [
                    Path.GetFileNameWithoutExtension(path),
                    SafeCountLines(path).ToString()
                ]
            })
            .ToList();

        return Completed(command,
            new UiTableBlock
            {
                Title = "Рассказ",
                Columns = ["Рассказ", "Записей"],
                Rows = rows
            });
    }

    private static ExplorerCommandResult BuildGallery(string command, FileSystemManager fs)
    {
        var imagesDir = fs.ResolvePath("images");
        var rows = new List<UiTableRow>();
        var media = new LocalMediaService(fs);
        var imageReferences = media.EnumerateGallery().ToList();
        if (Directory.Exists(imagesDir))
        {
            foreach (var directory in Directory.GetDirectories(imagesDir).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(new UiTableRow
                {
                    Cells =
                    [
                        Path.GetFileName(directory),
                        Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length.ToString(),
                        ToRelativeGameSessionPath(fs, directory)
                    ]
                });
            }
        }

        if (rows.Count == 0)
        {
            rows.Add(new UiTableRow { Cells = ["images", "0", "game_session/images"] });
        }

        var blocks = new List<UiBlock>
        {
            new UiTableBlock
            {
                Title = "Галерея",
                Columns = ["Раздел", "Файлов", "Путь"],
                Rows = rows
            }
        };

        if (imageReferences.Count == 0)
        {
            blocks.Add(Message(UiNotificationSeverity.Info, "Изображения", "Сохранённых изображений пока нет."));
        }
        else
        {
            blocks.AddRange(imageReferences.Select(static image => new UiImageBlock
            {
                Title = image.FileName,
                Url = image.Url,
                MediaId = image.MediaId,
                RelativePath = image.RelativePath,
                AltText = image.FileName,
                ContentType = image.ContentType,
                Length = image.Length,
                ModifiedAtUtc = image.ModifiedAtUtc
            }));
        }

        return Completed(command, blocks);
    }

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
            },
            Raw("Полный JSON output/debug_logs.json", read.Node));
    }

    private static ExplorerCommandResult BuildDebug(
        string command,
        FileSystemManager fs,
        StateManager stateManager,
        LocalizationManager loc)
    {
        var files = fs.GetAllGameStateFiles();
        var rows = files
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .Select(path => new UiTableRow
            {
                Cells =
                [
                    ToRelativeGameSessionPath(fs, path),
                    SafeFileLength(path).ToString()
                ]
            })
            .ToList();

        return Completed(command,
            Panel(loc.T("debug_info"),
                Grid(
                    ("Файлов состояния", files.Length.ToString()),
                    ("Сессия", EmptyFallback(stateManager.CurrentState.SessionId)),
                    ("Язык", loc.CurrentLanguage),
                    ("BasePath", fs.BasePath))),
            new UiTableBlock
            {
                Title = "Файлы состояния",
                Columns = ["Путь", "Байт"],
                Rows = rows
            });
    }

    private static ExplorerCommandResult BuildDirectoryList(string command, FileSystemManager fs, string title, string relativePath)
    {
        var root = fs.ResolvePath(relativePath);
        var rows = new List<UiTableRow>();
        if (Directory.Exists(root))
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(root).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(new UiTableRow
                {
                    Cells =
                    [
                        Path.GetFileName(path),
                        Directory.Exists(path) ? "папка" : "файл",
                        ToRelativeGameSessionPath(fs, path)
                    ]
                });
            }
        }

        return Completed(command,
            new UiTableBlock
            {
                Title = title,
                Columns = ["Имя", "Тип", "Путь"],
                Rows = rows
            });
    }

    private static ExplorerCommandResult BuildSystemGuardians(string command, FileSystemManager fs)
    {
        var root = Path.Combine(fs.BasePath, SystemGuardianLibraryService.RootDirectoryName);
        var rows = new List<UiTableRow>();
        if (Directory.Exists(root))
        {
            foreach (var manifest in Directory.GetFiles(root, "manifest.json", SearchOption.AllDirectories)
                         .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(new UiTableRow
                {
                    Cells =
                    [
                        Path.GetFileName(Path.GetDirectoryName(manifest) ?? manifest),
                        ToRelativeBasePath(fs, manifest)
                    ]
                });
            }
        }

        return Completed(command,
            new UiTableBlock
            {
                Title = "Извечные хранители",
                Columns = ["Preset", "Manifest"],
                Rows = rows
            });
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

        var blocks = new List<UiBlock>
        {
            Panel("Крылья над Бездной",
                Grid(
                    ("Стадия раскрытия", DescribeSarefRevealStage(GetString(root, "revealStage", SarefMainStoryState.RevealStageUnknown))),
                    ("Фрагментов", CountArray(root, "sarefRevelations").ToString()),
                    ("Преимуществ", CountArray(root, "sarefAdvantages").ToString()),
                    ("Использований преимуществ", CountArray(root, "sarefAdvantageUses").ToString()),
                    ("Известных агентов", CountNestedArray(root, "factionLinks", "knownAgents").ToString()),
                    ("Финал", DescribeNested(root, "finalConfrontation")),
                    ("Клятва", DescribeNested(root, "playerOathState"))))
        };

        AddSarefStoryArrayTable(blocks, "Раскрытые фрагменты", root["sarefRevelations"] as JsonArray);
        AddSarefStoryArrayTable(blocks, "Преимущества", root["sarefAdvantages"] as JsonArray);
        AddSarefStoryArrayTable(blocks, "Использования преимуществ", root["sarefAdvantageUses"] as JsonArray);
        AddSarefFactionLinks(blocks, root["factionLinks"] as JsonObject);
        AddSarefStoryArrayTable(blocks, "Возможные исходы", root["defeatOutcomes"] as JsonArray);
        AddSarefStoryArrayTable(blocks, "Финалы", root["endings"] as JsonArray);

        return Completed(command, blocks);
    }

    private static void AddSarefFactionLinks(List<UiBlock> blocks, JsonObject? factionLinks)
    {
        if (factionLinks == null)
            return;

        var rows = new List<UiTableRow>();
        AddSarefStoryRows(rows, "Тени", factionLinks["shadowTraces"] as JsonArray);
        AddSarefStoryRows(rows, "Агенты", factionLinks["knownAgents"] as JsonArray);
        AddSarefStoryRows(rows, "Фракции", factionLinks["factions"] as JsonArray);
        if (rows.Count == 0)
            return;

        blocks.Add(new UiTableBlock
        {
            Title = "Связи Сарефа",
            Columns = ["Раздел", "Описание", "Детали"],
            Rows = rows
        });
    }

    private static void AddSarefStoryArrayTable(List<UiBlock> blocks, string title, JsonArray? array)
    {
        if (array == null || array.Count == 0)
            return;

        var rows = new List<UiTableRow>();
        AddSarefStoryRows(rows, string.Empty, array);
        if (rows.Count == 0)
            return;

        blocks.Add(new UiTableBlock
        {
            Title = title,
            Columns = ["Запись", "Описание", "Детали"],
            Rows = rows
        });
    }

    private static void AddSarefStoryRows(List<UiTableRow> rows, string prefix, JsonArray? array)
    {
        if (array == null)
            return;

        foreach (var (item, index) in array.OfType<JsonObject>().Select((item, index) => (item, index)))
        {
            var title = FirstReadableJsonValue(
                GetString(item, "name", string.Empty),
                GetString(item, "displayName", string.Empty),
                GetString(item, "title", string.Empty),
                string.IsNullOrWhiteSpace(prefix) ? $"Запись {index + 1}" : prefix);
            var summary = FirstReadableJsonValue(
                GetString(item, "summary", string.Empty),
                GetString(item, "description", string.Empty),
                GetString(item, "outcome", string.Empty),
                GetString(item, "result", string.Empty));
            var details = BuildSarefStoryDetail(item);
            if (IsUnknownReadableJsonValue(summary) && IsUnknownReadableJsonValue(details))
                continue;

            rows.Add(Row(
                string.IsNullOrWhiteSpace(prefix) ? title : JoinNonEmpty(": ", prefix, title),
                summary,
                details));
        }
    }

    private static string BuildSarefStoryDetail(JsonObject item)
    {
        var parts = new[]
            {
                FormatSarefSource(item),
                GetString(item, "revealedAtTurn", string.Empty) is { Length: > 0 } turn ? $"ход {turn}" : string.Empty,
                GetString(item, "status", string.Empty) is { Length: > 0 } status ? DescribeSarefGenericStatus(status) : string.Empty,
                GetString(item, "category", string.Empty) is { Length: > 0 } category ? TranslateSarefCategory(category) : string.Empty
            }
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        return parts.Length == 0 ? "не указано" : string.Join("; ", parts);
    }

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
            _ => EmptyFallback(category)
        };

    private static async Task<ExplorerCommandResult> BuildSarefMemoryScene(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, SarefMainStoryState.StatePath);
        if (read.Node is not JsonObject root)
        {
            var message = read.FileExists
                ? $"Файл найден, но не разобран как состояние скрытой линии: {read.Path}. {read.Error}"
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

        var blocks = new List<UiBlock>
        {
            Panel("Воспоминание",
                Grid(
                    ("Сцена", title),
                    ("Состояние", DescribeSarefMemorySceneStatus(GetString(scene, "status", string.Empty))),
                    ("Память Хранителя", GetString(scene, "guardianId")),
                    ("Квест", JoinNonEmpty(" / ", GetString(scene, "questId", string.Empty), GetNumberOrString(scene, "questOrdinal"))),
                    ("Роль внутри сцены", JoinNonEmpty(" - ", roleName, roleSummary)))),
            new UiTextBlock
            {
                Tone = UiTone.Warning,
                Text = "Это не Врата Памяти и не Наследие Памяти. Смертный инвентарь не переносится; исторический факт нельзя напрямую переписать."
            }
        };

        blocks.Add(BuildMemorySceneObjectTable("Границы сцены", scene["boundaries"] as JsonArray, "boundaryId", preferName: false));
        blocks.Add(BuildMemorySceneObjectTable("Доступные способности", scene["abilities"] as JsonArray, "abilityId", preferName: true));
        blocks.Add(BuildMemorySceneNodeTable(scene["requiredStoryNodes"] as JsonArray));
        blocks.Add(BuildMemorySceneSuccessCondition(scene["successCondition"] as JsonObject));
        blocks.Add(BuildMemorySceneClosureTarget(scene["closureTarget"] as JsonObject));
        return Completed(command, blocks);
    }

    private static UiTableBlock BuildMemorySceneObjectTable(string title, JsonArray? array, string idProperty, bool preferName)
    {
        var rows = new List<UiTableRow>();
        if (array != null)
        {
            foreach (var item in array.OfType<JsonObject>())
            {
                var name = preferName
                    ? GetString(item, "name", GetString(item, "displayName", GetString(item, idProperty)))
                    : GetString(item, "displayName", GetString(item, "name", GetString(item, idProperty)));
                rows.Add(new UiTableRow
                {
                    Cells =
                    [
                        name,
                        GetString(item, "summary", GetString(item, "description", "не указано"))
                    ]
                });
            }
        }

        if (rows.Count == 0)
            rows.Add(new UiTableRow { Cells = ["не указано", "не указано"] });

        return new UiTableBlock
        {
            Title = title,
            Columns = ["Название", "Описание"],
            Rows = rows
        };
    }

    private static UiTableBlock BuildMemorySceneNodeTable(JsonArray? nodes)
    {
        var rows = new List<UiTableRow>();
        if (nodes != null)
        {
            foreach (var node in nodes.OfType<JsonObject>())
            {
                rows.Add(new UiTableRow
                {
                    Cells =
                    [
                        DescribeSarefMemorySceneNodeStatus(GetString(node, "status", string.Empty)),
                        GetString(node, "summary", GetString(node, "title", GetString(node, "nodeId")))
                    ]
                });
            }
        }

        if (rows.Count == 0)
            rows.Add(new UiTableRow { Cells = ["не указано", "не указано"] });

        return new UiTableBlock
        {
            Title = "Обязательные сюжетные узлы",
            Columns = ["Состояние", "Узел"],
            Rows = rows
        };
    }

    private static UiPanelBlock BuildMemorySceneSuccessCondition(JsonObject? condition)
    {
        if (condition == null)
            return Panel("Условие успеха", Grid(("Описание", "не указано")));

        return Panel("Условие успеха",
            Grid(
                ("Описание", GetString(condition, "summary", GetString(condition, "conditionId"))),
                ("Состояние", GetBool(condition, "satisfied") ? "выполнено" : "ещё не выполнено")));
    }

    private static UiPanelBlock BuildMemorySceneClosureTarget(JsonObject? target)
    {
        if (target == null)
            return Panel("Что закрывает сцена", Grid(("Цель", "не указано")));

        return Panel("Что закрывает сцена",
            Grid(
                ("Хранитель", GetString(target, "guardianId")),
                ("Квест", JoinNonEmpty(" / ", GetString(target, "questId", string.Empty), GetNumberOrString(target, "questOrdinal"))),
                ("Фрагмент истины", GetString(target, "revelationId")),
                ("Преимущество", GetString(target, "advantageId"))));
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
        var blocks = new List<UiBlock>();
        if (node is JsonArray rootArray)
        {
            AddReadableJsonArrayBlock(blocks, title, "Записи", rootArray);
            return blocks;
        }

        if (node is not JsonObject root)
        {
            if (TryGetScalarString(node, out var scalar))
                blocks.Add(Panel(title, Grid(("Значение", scalar))));
            return blocks;
        }

        var scalarRows = new List<UiTableRow>();
        foreach (var property in root)
        {
            if (IsTechnicalReadableJsonProperty(property.Key))
                continue;

            if (property.Value is JsonArray array)
            {
                AddReadableJsonArrayBlock(blocks, title, TranslateReadableJsonField(property.Key), array);
                continue;
            }

            var value = DescribeReadableJsonValue(property.Value);
            if (!IsUnknownReadableJsonValue(value))
                scalarRows.Add(Row(TranslateReadableJsonField(property.Key), value));
        }

        if (scalarRows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = title + ": сведения",
                Columns = ["Раздел", "Значение"],
                Rows = scalarRows
            });
        }

        return blocks;
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
            return scalar;

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
            _ => propertyName.Replace('_', ' ')
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
