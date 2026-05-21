using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

public static class ExplorerAfterlifeCombatCommandResultBuilder
{
    private const string SoulStatePath = "game_state/meta/soul_state.json";

    private enum CommandKind
    {
        Profiles,
        Inbox,
        Conflict,
        CombatLog,
        Help,
        Arts
    }

    private static readonly IReadOnlyDictionary<string, CommandKind> CommandKinds =
        new Dictionary<string, CommandKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["/afterlife_profiles"] = CommandKind.Profiles,
            ["/профили_загробья"] = CommandKind.Profiles,
            ["/afterlife_inbox"] = CommandKind.Inbox,
            ["/уведомления_загробья"] = CommandKind.Inbox,
            ["/spiritual_conflict"] = CommandKind.Conflict,
            ["/духовный_конфликт"] = CommandKind.Conflict,
            ["/spiritual_combat_log"] = CommandKind.CombatLog,
            ["/журнал_духовного_боя"] = CommandKind.CombatLog,
            ["/spiritual_combat_help"] = CommandKind.Help,
            ["/духовный_бой"] = CommandKind.Help,
            ["/spiritual_arts"] = CommandKind.Arts,
            ["/духовные_искусства"] = CommandKind.Arts
        };

    public static async Task<ExplorerCommandResult?> TryBuildAsync(
        string command,
        StateManager stateManager,
        FileSystemManager fs)
    {
        var normalizedCommand = command.Trim();
        if (!CommandKinds.TryGetValue(normalizedCommand, out var kind))
            return null;

        await stateManager.RefreshGameStateAsync();

        return kind switch
        {
            CommandKind.Profiles => await BuildProfiles(normalizedCommand, fs),
            CommandKind.Inbox => await BuildInbox(normalizedCommand, fs),
            CommandKind.Conflict => await BuildConflict(normalizedCommand, fs),
            CommandKind.CombatLog => await BuildCombatLog(normalizedCommand, fs),
            CommandKind.Help => BuildHelp(normalizedCommand),
            CommandKind.Arts => await BuildArts(normalizedCommand, fs),
            _ => null
        };
    }

    private static async Task<ExplorerCommandResult> BuildProfiles(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, AfterlifeEntityProfileState.StatePath);
        var profiles = read.Node?[AfterlifeEntityProfileState.ProfilesProperty] as JsonArray;
        var blocks = new List<UiBlock>
        {
            Panel("Профили сущностей посмертия",
                Grid(
                    ("Профилей", (profiles?.Count ?? 0).ToString()),
                    ("Опасных развеивателей души", CountProfilesWithDissipation(profiles).ToString()),
                    ("Особых духовных искусств", CountNestedArray(profiles, "specialArts").ToString()),
                    ("Кастомных состояний", CountNestedArray(profiles, AfterlifeEntityProfileState.CustomStatesProperty).ToString())))
        };

        if (profiles is { Count: > 0 })
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Сущности",
                Columns = ["Имя", "Тип", "Область", "Ресурсы", "Прогрессия", "Духовные искусства", "Особые искусства", "Опасность"],
                Rows = profiles.OfType<JsonObject>()
                    .Select(profile => new UiTableRow
                    {
                        Cells =
                        [
                            GetString(profile, "displayName", "Без имени"),
                            DescribeActorType(GetString(profile, "actorType", "?")),
                            DescribeRealm(GetString(profile, "realm", "?")),
                            DescribeProfileCurrencies(profile),
                            DescribeProfileProgression(profile),
                            DescribeStandardArts(profile["standardArts"] as JsonObject),
                            DescribeSpecialArts(profile["specialArts"] as JsonArray),
                            DescribeDissipation(profile)
                        ]
                    })
                    .ToList()
            });

            blocks.Add(new UiTableBlock
            {
                Title = "Стратегии прокачки",
                Columns = ["Сущность", "Стратегия", "Приоритеты", "Последний цикл"],
                Rows = profiles.OfType<JsonObject>()
                    .Select(profile =>
                    {
                        var strategy = profile["progressionStrategy"] as JsonObject;
                        return new UiTableRow
                        {
                            Cells =
                            [
                                GetString(profile, "displayName", "Без имени"),
                                GetString(strategy, "summary", "не указана"),
                                JoinStringArray(strategy?["priorityOrder"] as JsonArray),
                                GetString(strategy, "lastAutoProgressionCycleKey", "нет")
                            ]
                        };
                    })
                    .ToList()
            });
        }
        else
        {
            blocks.Add(Message("Профили не найдены", "ГМ создаёт профили значимых сущностей через afterlifeEntityProfileUpdates."));
        }

        AddRawOrWarning(blocks, $"Полный JSON {AfterlifeEntityProfileState.StatePath}", read);
        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildInbox(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, AfterlifeNotificationState.NotificationsPath);
        var notifications = await AfterlifeNotificationState.ReadAsync(fs);
        var unread = notifications.Count(static item => string.Equals(item.Status, AfterlifeNotificationState.StatusUnread, StringComparison.OrdinalIgnoreCase));

        var blocks = new List<UiBlock>
        {
            Panel("Уведомления загробья",
                Grid(
                    ("Всего", notifications.Count.ToString()),
                    ("Непрочитано", unread.ToString()),
                    ("Режим браузера", "только просмотр; отметка прочитанным остаётся консольным действием до #574")))
        };

        if (notifications.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Ответы ГМ и уведомления",
                Columns = ["Статус", "Тип", "Сводка", "Источник", "Ход"],
                Rows = notifications.Select(notification => new UiTableRow
                {
                    Cells =
                    [
                        DescribeNotificationStatus(notification.Status),
                        AfterlifeNotificationState.GetTypeLabel(notification.NotificationType),
                        EmptyFallback(notification.Summary),
                        DescribeNotificationSource(notification),
                        notification.CreatedAtTurn > 0 ? notification.CreatedAtTurn.ToString() : "?"
                    ]
                }).ToList()
            });
        }
        else
        {
            blocks.Add(Message("Нет уведомлений", "Пока нет ответов Хранителей, Архива, резидентов или Сияющей Обители."));
        }

        AddRawOrWarning(blocks, $"Полный JSON {AfterlifeNotificationState.NotificationsPath}", read);
        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildConflict(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, AfterlifeSpiritualConflictState.StatePath);
        var active = read.Node?["activeConflict"] as JsonObject;
        var blocks = new List<UiBlock>
        {
            Panel("Духовный конфликт",
                Grid(
                    ("Активный конфликт", active == null ? "нет" : GetString(active, "conflictId", "unknown")),
                    ("Область", DescribeRealm(GetString(active, "realm", "?"))),
                    ("Модель сторон", DescribeSideModel(GetString(active, "sideModel", "?"))),
                    ("Позиция", DescribeConflictPosition(GetString(active, "conflictPosition", "contested"))),
                    ("Напряжение игрока", DescribeStrain(GetString(active, "playerSideStrain", "clear"))),
                    ("Напряжение противника", DescribeStrain(GetString(active, "oppositionSideStrain", "clear"))),
                    ("Контроль/оковы", DescribeControlState(active?["controlState"] as JsonObject)),
                    ("ОД", DescribeActionEconomy(active?["actionEconomy"] as JsonObject)),
                    ("Обменов", CountArray(active, "exchangeLog").ToString())))
        };

        if (active != null)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Стороны конфликта",
                Columns = ["Сторона", "Ведущий", "Участников"],
                Rows =
                [
                    SideRow("Сторона игрока", active["playerSide"] as JsonObject),
                    SideRow("Противостоящая сторона", active["oppositionSide"] as JsonObject)
                ]
            });
        }
        else
        {
            blocks.Add(Message("Активного духовного конфликта нет", "ГМ может начать конфликт через afterlifeSpiritualConflictUpdate с mode=start."));
        }

        AddRawOrWarning(blocks, $"Полный JSON {AfterlifeSpiritualConflictState.StatePath}", read);
        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildCombatLog(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, AfterlifeSpiritualConflictState.StatePath);
        var active = read.Node?["activeConflict"] as JsonObject;
        var exchangeLog = active?["exchangeLog"] as JsonArray;
        var recent = read.Node?["recentConflicts"] as JsonArray;
        var blocks = new List<UiBlock>
        {
            Panel("Журнал духовного боя",
                Grid(
                    ("Активный конфликт", active == null ? "нет" : GetString(active, "conflictId", "unknown")),
                    ("Обменов активного конфликта", (exchangeLog?.Count ?? 0).ToString()),
                    ("Недавних завершённых конфликтов", (recent?.Count ?? 0).ToString()),
                    ("Источник", AfterlifeSpiritualConflictState.StatePath)))
        };

        if (exchangeLog is { Count: > 0 })
            blocks.Add(BuildExchangeTable(exchangeLog));
        if (recent is { Count: > 0 })
            blocks.Add(BuildRecentConflictTable(recent));
        if ((exchangeLog?.Count ?? 0) == 0 && (recent?.Count ?? 0) == 0)
            blocks.Add(Message("Журнал пуст", "Когда ГМ проведёт обмен или завершит конфликт, здесь появятся кубики, позиция, напряжение и награды."));

        AddRawOrWarning(blocks, $"Полный JSON {AfterlifeSpiritualConflictState.StatePath}", read);
        return Completed(command, blocks);
    }

    private static ExplorerCommandResult BuildHelp(string command) =>
        Completed(command,
            Panel("Духовный бой",
                new UiListBlock
                {
                    Items =
                    [
                        "Духовный бой посмертия не использует здоровье, энергию и смертные боевые навыки.",
                        "Спорные обмены используют d20, модификаторы, позицию, контроль/оковы и аудит кубиков.",
                        "Давление ухудшает напряжение противника; защита снижает входящий вред; контрприём разворачивает конкретное входящее действие.",
                        "Манёвр меняет позицию и будущие бонусы; оковы создают контроль; разрыв оков снимает или ослабляет контроль.",
                        "Собрать Средоточие восстанавливает ОД, но опасно против давления, манёвра, оков и принудительного воплощения.",
                        "Критическая 20 и критическая 1 симметричны и ограничиваются масштабом сцены.",
                        "Победа в проверяемом конфликте может дать Чернильные Перья в Море Хаоса или Искры Света в Сияющей Обители."
                    ]
                }),
            new UiTableBlock
            {
                Title = "Духовные искусства и контрприёмы",
                Columns = ["Искусство", "Игровой смысл", "Сильнее против", "Его контрит"],
                Rows = AfterlifeSpiritualConflictState.SpiritualArts
                    .Select(art => new UiTableRow
                    {
                        Cells = [art.DisplayName, art.MechanicalUse, DescribeStrongAgainst(art.ArtId), DescribeCounteredBy(art.ArtId)]
                    })
                    .ToList()
            });

    private static async Task<ExplorerCommandResult> BuildArts(string command, FileSystemManager fs)
    {
        var soul = await ReadJson(fs, SoulStatePath);
        var profiles = await ReadJson(fs, AfterlifeEntityProfileState.StatePath);
        var combatProfile = soul.Node?["afterlifeCombatProfile"] as JsonObject;
        var standardArts = combatProfile?["standardArts"] as JsonObject;
        var playerProfile = FindPlayerProfile(profiles.Node);
        var learnedSpecialArts = playerProfile?["specialArts"] as JsonArray;

        var blocks = new List<UiBlock>
        {
            Panel("Духовные искусства",
                Grid(
                    ("Просветление", GetNumberOrString(combatProfile, "enlightenmentTier", "0")),
                    ("Сияние", GetNumberOrString(combatProfile, "radianceTier", "0")),
                    ("Средоточие Души", GetNumberOrString(combatProfile, "spiritFocusTier", "0")),
                    ("Особых искусств игрока", (learnedSpecialArts?.Count ?? 0).ToString()),
                    ("Режим браузера", "только просмотр; локальная прокачка ждёт #574")))
        };

        blocks.Add(new UiTableBlock
        {
            Title = "Стандартные духовные искусства",
            Columns = ["Искусство", "Тир", "Назначение", "Стоимость/темп"],
            Rows = AfterlifeSpiritualConflictState.SpiritualArts
                .Select(art => new UiTableRow
                {
                    Cells =
                    [
                        art.DisplayName,
                        GetNumberOrString(standardArts, art.ArtId, "0"),
                        art.MechanicalUse,
                        DescribeArtCost(art.ArtId)
                    ]
                })
                .ToList()
        });

        if (learnedSpecialArts is { Count: > 0 })
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Особые духовные искусства игрока",
                Columns = ["Название", "Основа", "Тир", "Эффект", "Стоимость"],
                Rows = learnedSpecialArts.OfType<JsonObject>()
                    .Select(art => new UiTableRow
                    {
                        Cells =
                        [
                            GetString(art, "displayName", GetString(art, "artId", "Без названия")),
                            DescribeArt(GetString(art, "baseOperation", "?")),
                            GetNumberOrString(art, "tier", "0"),
                            GetString(art, "effectSummary", "эффект не описан"),
                            DescribeSpecialArtCost(art)
                        ]
                    })
                    .ToList()
            });
        }
        else
        {
            blocks.Add(Message("Особые искусства не изучены", "ГМ может выдать обучение через afterlifeSpecialArtLearningReceipts, если ролевая сцена это признаёт."));
        }

        AddRawOrWarning(blocks, "Полный JSON afterlifeCombatProfile", new JsonReadResult(soul.FileExists, combatProfile, soul.Error));
        AddRawOrWarning(blocks, $"Полный JSON {AfterlifeEntityProfileState.StatePath}", profiles);
        return Completed(command, blocks);
    }

    private static UiTableBlock BuildExchangeTable(JsonArray exchangeLog) =>
        new()
        {
            Title = "Обмены активного конфликта",
            Columns = ["Обмен", "Действие", "Исход", "Позиция", "Напряжение", "Кубики", "Награда"],
            Rows = exchangeLog.OfType<JsonObject>()
                .Select(exchange => new UiTableRow
                {
                    Cells =
                    [
                        GetString(exchange, "exchangeId", "?"),
                        DescribeArt(GetString(exchange, "operationType", "?")),
                        DescribeOutcome(GetString(exchange, "outcome", "?")),
                        DescribeBeforeAfter(exchange, "conflictPosition", DescribeConflictPosition),
                        DescribeBeforeAfter(exchange, "oppositionSideStrain", DescribeStrain),
                        DescribeDice(exchange["diceAudit"] as JsonObject),
                        DescribeReward(exchange["rewardAudit"] as JsonObject)
                    ]
                })
                .ToList()
        };

    private static UiTableBlock BuildRecentConflictTable(JsonArray recent) =>
        new()
        {
            Title = "Недавние завершённые конфликты",
            Columns = ["Конфликт", "Состояние", "Итог", "Ход", "Награда"],
            Rows = recent.OfType<JsonObject>()
                .Select(conflict => new UiTableRow
                {
                    Cells =
                    [
                        GetString(conflict, "conflictId", "?"),
                        GetString(conflict, "resolutionState", "?"),
                        GetString(conflict, "playerOutcome", GetString(conflict, "outcome", "?")),
                        GetNumberOrString(conflict, "resolvedAtTurn", "?"),
                        DescribeReward(conflict["rewardAudit"] as JsonObject)
                    ]
                })
                .ToList()
        };

    private static UiTableRow SideRow(string label, JsonObject? side)
    {
        var lead = side?["leadContestant"] as JsonObject;
        return new UiTableRow
        {
            Cells =
            [
                label,
                GetString(lead, "displayName", GetString(lead, "actorId", "?")),
                CountArray(side, "contestants").ToString()
            ]
        };
    }

    private static JsonObject? FindPlayerProfile(JsonNode? root)
    {
        if (root?[AfterlifeEntityProfileState.ProfilesProperty] is not JsonArray profiles)
            return null;

        return profiles.OfType<JsonObject>().FirstOrDefault(profile =>
            string.Equals(GetString(profile, "actorType", ""), "player_soul", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(GetString(profile, "actorId", ""), "player_soul", StringComparison.OrdinalIgnoreCase));
    }

    private static string DescribeProfileCurrencies(JsonObject profile)
    {
        var currencies = profile["currencies"] as JsonObject;
        return $"Перья {GetNumberOrString(currencies, "inkFeathers", "0")}; Искры {GetNumberOrString(currencies, "lightSparks", "0")}";
    }

    private static string DescribeProfileProgression(JsonObject profile)
    {
        var progression = profile["progression"] as JsonObject;
        var enlightenment = progression?["enlightenment"] as JsonObject;
        var radiance = progression?["radiance"] as JsonObject;
        return $"Просветление {GetNumberOrString(enlightenment, "tier", "0")}/{GetNumberOrString(enlightenment, "experience", "0")}; " +
               $"Сияние {GetNumberOrString(radiance, "tier", "0")}/{GetNumberOrString(radiance, "experience", "0")}";
    }

    private static string DescribeStandardArts(JsonObject? arts)
    {
        if (arts == null || arts.Count == 0)
            return "не указаны";

        return string.Join("; ", arts.Select(item => $"{DescribeArt(item.Key)} {GetNumberOrString(arts, item.Key, "0")}"));
    }

    private static string DescribeSpecialArts(JsonArray? arts)
    {
        if (arts == null || arts.Count == 0)
            return "нет";

        return string.Join("; ", arts.OfType<JsonObject>().Select(art =>
            $"{GetString(art, "displayName", GetString(art, "artId", "?"))} {GetNumberOrString(art, "tier", "0")}"));
    }

    private static string DescribeDissipation(JsonObject profile)
    {
        var tier = GetInt(profile?["soulDissipationTier"]);
        var coefficient = GetInt(profile?["soulStabilityCoefficient"]);
        if (coefficient <= 0)
            coefficient = GetInt(profile?["progression"]?["soulStabilityCoefficient"]);

        return tier > 0
            ? $"ОПАСНО: развеивание души {tier}; устойчивость {Math.Max(0, coefficient)}"
            : "не умеет развеивать душу";
    }

    private static string DescribeNotificationStatus(string status) =>
        string.Equals(status, AfterlifeNotificationState.StatusUnread, StringComparison.OrdinalIgnoreCase)
            ? "непрочитано"
            : "прочитано";

    private static string DescribeNotificationSource(AfterlifeNotificationState.NotificationEntry notification)
    {
        if (!string.IsNullOrWhiteSpace(notification.GuardianName))
            return notification.GuardianName;
        if (!string.IsNullOrWhiteSpace(notification.ResidentName))
            return notification.ResidentName;
        if (!string.IsNullOrWhiteSpace(notification.ArchiveTitle))
            return notification.ArchiveTitle;
        if (!string.IsNullOrWhiteSpace(notification.TargetProjectName))
            return notification.TargetProjectName;
        return EmptyFallback(notification.RequestId);
    }

    private static string DescribeControlState(JsonObject? control)
    {
        if (control == null)
            return "нет активного контроля";

        var level = DescribeControlLevel(GetString(control, "level", "none"));
        if (string.Equals(level, "нет контроля", StringComparison.OrdinalIgnoreCase))
            return level;

        var side = DescribeSide(GetString(control, "controllerSide", "?"));
        var restricted = JoinStringArray(control["restrictedOperations"] as JsonArray);
        var summary = GetString(control, "summary", "");
        return string.IsNullOrWhiteSpace(summary)
            ? $"{side}: {level}; ограничено: {restricted}"
            : $"{side}: {level}; ограничено: {restricted}; {summary}";
    }

    private static string DescribeActionEconomy(JsonObject? economy)
    {
        if (economy == null)
            return "нет данных";

        return $"игрок {DescribeActionPoints(economy["player"] as JsonObject)}; противник {DescribeActionPoints(economy["opposition"] as JsonObject)}";
    }

    private static string DescribeActionPoints(JsonObject? side)
    {
        if (side == null)
            return "?";

        return $"{GetNumberOrString(side, "current", "0")}/{GetNumberOrString(side, "max", "0")} ОД";
    }

    private static string DescribeBeforeAfter(JsonObject exchange, string propertyName, Func<string, string> formatter)
    {
        var before = exchange["before"] as JsonObject;
        var after = exchange["after"] as JsonObject;
        return $"{formatter(GetString(before, propertyName, "?"))} -> {formatter(GetString(after, propertyName, "?"))}";
    }

    private static string DescribeDice(JsonObject? audit)
    {
        if (audit == null)
            return "нет";

        var player = GetNumberOrString(audit, "playerTotal", "?");
        var opposition = GetNumberOrString(audit, "oppositionTotal", "?");
        var margin = GetNumberOrString(audit, "margin", "?");
        var rollText = "";
        if (audit["rolls"] is JsonArray rolls)
        {
            var values = rolls.OfType<JsonObject>()
                .Select(roll => $"{DescribeSide(GetString(roll, "side", "?"))} d20={GetNumberOrString(roll, "value", "?")}");
            rollText = "; " + string.Join(", ", values);
        }

        return $"итог {player}:{opposition}, разница {margin}{rollText}";
    }

    private static string DescribeReward(JsonObject? audit)
    {
        if (audit == null)
            return "нет";

        return $"{DescribeCurrency(GetString(audit, "currency", "?"))}: {GetNumberOrString(audit, "finalAmount", "0")}";
    }

    private static string DescribeArtCost(string artId) =>
        artId.Trim().ToLowerInvariant() switch
        {
            "pressure" => "база 3 ОД, ухудшает напряжение",
            "guard" => "база 2 ОД, снижает входящий вред",
            "counter" => "база 4 ОД, требует входящее действие",
            "maneuver" => "база 3 ОД, меняет позицию",
            "binding" => "база 4 ОД, создаёт контроль",
            "force_binding" => "база 5 ОД, сильный контроль",
            "break_binding" => "база 3 ОД, снимает контроль",
            "incarnation_resistance" => "база 3 ОД, против принудительного воплощения",
            "champion_coordination" => "база 2 ОД, поддержка чемпиона",
            "recover_spiritual_power" => "восстанавливает ОД",
            _ => "стоимость зависит от аудита"
        };

    private static string DescribeSpecialArtCost(JsonObject art)
    {
        var multiplier = GetInt(art["costMultiplierPercent"]);
        return multiplier > 0 ? $"{multiplier}% базовой стоимости" : "дороже базового действия";
    }

    private static string DescribeStrongAgainst(string artId) =>
        artId.Trim().ToLowerInvariant() switch
        {
            "pressure" => "манёвр, восстановление ОД",
            "guard" => "давление, входящий вред",
            "counter" => "конкретное входящее действие",
            "maneuver" => "защита, пассивность",
            "binding" => "противник под рычагом",
            "force_binding" => "противник под сильным рычагом",
            "break_binding" => "оковы и контроль",
            "incarnation_resistance" => "принудительное воплощение",
            "champion_coordination" => "бой через союзника",
            "recover_spiritual_power" => "защита, ожидание",
            _ => "контекст сцены"
        };

    private static string DescribeCounteredBy(string artId) =>
        artId.Trim().ToLowerInvariant() switch
        {
            "pressure" => "защита, контрприём",
            "guard" => "манёвр, восстановление ОД",
            "counter" => "ожидание, отсутствие входящего действия",
            "maneuver" => "давление, встречный манёвр, контроль",
            "binding" => "разрыв оков, контрприём",
            "force_binding" => "разрыв оков, сопротивление воплощению",
            "break_binding" => "давление, повторное усиление контроля",
            "incarnation_resistance" => "давление, силовое принуждение",
            "champion_coordination" => "давление на чемпиона",
            "recover_spiritual_power" => "давление, манёвр, оковы",
            _ => "контекст сцены"
        };

    private static string DescribeArt(string? artId) =>
        (artId ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "pressure" => "Давление",
            "counter" => "Контрприём",
            "guard" => "Защита",
            "maneuver" => "Манёвр",
            "binding" => "Оковы",
            "force_binding" => "Силовые оковы",
            "break_binding" => "Разрыв оков",
            "incarnation_resistance" => "Сопротивление воплощению",
            "champion_coordination" => "Координация чемпиона",
            "recover_spiritual_power" => "Собрать Средоточие",
            _ => string.IsNullOrWhiteSpace(artId) ? "?" : artId
        };

    private static string DescribeActorType(string actorType) =>
        actorType.Trim().ToLowerInvariant() switch
        {
            "player_soul" => "Душа игрока",
            "guardian" => "Хранитель",
            "resident" => "Резидент",
            "shining_faction_head" => "Глава фракции",
            "radiant_actor" => "Сияющий актор",
            "custom_afterlife_actor" => "Особая сущность",
            _ => actorType
        };

    private static string DescribeRealm(string realm) =>
        realm.Trim().ToLowerInvariant() switch
        {
            "chaos sea" => "Море Хаоса",
            "море хаоса" => "Море Хаоса",
            "shining abode" => "Сияющая Обитель",
            "сияющая обитель" => "Сияющая Обитель",
            _ => realm
        };

    private static string DescribeSideModel(string sideModel) =>
        sideModel.Trim().ToLowerInvariant() switch
        {
            "direct_duel" => "прямой поединок",
            "champion_duel" => "поединок чемпионов",
            "assisted_duel" => "поединок с поддержкой",
            _ => sideModel
        };

    private static string DescribeConflictPosition(string position) =>
        position.Trim().ToLowerInvariant() switch
        {
            "opposition_dominant" => "доминирование противника",
            "opposition_advantaged" => "преимущество противника",
            "contested" => "спорная позиция",
            "player_advantaged" => "преимущество игрока",
            "player_dominant" => "доминирование игрока",
            _ => position
        };

    private static string DescribeStrain(string strain) =>
        strain.Trim().ToLowerInvariant() switch
        {
            "clear" => "нет напряжения",
            "strained" => "напряжён",
            "cracking" => "трещит",
            "broken" => "сломлен",
            _ => strain
        };

    private static string DescribeControlLevel(string level) =>
        level.Trim().ToLowerInvariant() switch
        {
            "none" => "нет контроля",
            "hindered" => "стеснён",
            "bound" => "скован",
            "locked" => "запечатан",
            _ => level
        };

    private static string DescribeSide(string side) =>
        side.Trim().ToLowerInvariant() switch
        {
            "player" or "player_side" or "playerside" or "soul" => "игрок",
            "opposition" or "opposition_side" or "oppositionside" or "guardian" => "противник",
            _ => side
        };

    private static string DescribeOutcome(string outcome) =>
        outcome.Trim().ToLowerInvariant() switch
        {
            "success" => "успех",
            "partial_success" => "частичный успех",
            "blocked" => "заблокировано",
            "countered" => "контрировано",
            "setback" => "неудача с осложнением",
            "no_effect" => "без эффекта",
            _ => outcome
        };

    private static string DescribeCurrency(string currency) =>
        currency.Trim().ToLowerInvariant() switch
        {
            "ink_feathers" or "inkfeathers" => "Чернильные Перья",
            "light_sparks" or "lightsparks" => "Искры Света",
            _ => currency
        };

    private static string JoinStringArray(JsonArray? array)
    {
        if (array == null || array.Count == 0)
            return "нет";

        var items = array
            .Select(GetNodeString)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(item => DescribeArt(item!))
            .ToArray();
        return items.Length == 0 ? "нет" : string.Join(", ", items);
    }

    private static int CountProfilesWithDissipation(JsonArray? profiles) =>
        profiles?.OfType<JsonObject>().Count(profile => GetInt(profile["soulDissipationTier"]) > 0) ?? 0;

    private static int CountNestedArray(JsonArray? owners, string propertyName) =>
        owners?.OfType<JsonObject>().Sum(owner => CountArray(owner, propertyName)) ?? 0;

    private static int CountArray(JsonNode? node, string propertyName) =>
        node is JsonObject obj && obj[propertyName] is JsonArray array ? array.Count : 0;

    private static int GetInt(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var intValue))
            return intValue;
        if (node is JsonValue stringValue && stringValue.TryGetValue<string>(out var text) && int.TryParse(text, out var parsed))
            return parsed;
        return 0;
    }

    private static string GetString(JsonNode? node, string propertyName, string fallback)
    {
        if (node is not JsonObject obj)
            return fallback;

        return GetNodeString(obj[propertyName]) ?? fallback;
    }

    private static string GetNumberOrString(JsonNode? node, string propertyName, string fallback)
    {
        if (node is not JsonObject obj)
            return fallback;

        var value = obj[propertyName];
        return GetNodeString(value) ?? (GetInt(value) != 0 ? GetInt(value).ToString() : fallback);
    }

    private static string? GetNodeString(JsonNode? node)
    {
        if (node is null)
            return null;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
                return string.IsNullOrWhiteSpace(text) ? null : text;
            if (value.TryGetValue<int>(out var intValue))
                return intValue.ToString();
            if (value.TryGetValue<bool>(out var boolValue))
                return boolValue ? "true" : "false";
        }

        return null;
    }

    private static string EmptyFallback(string? value, string fallback = "не указано") =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static async Task<JsonReadResult> ReadJson(FileSystemManager fs, string path)
    {
        if (!fs.FileExists(path))
            return new JsonReadResult(false, null, null);

        try
        {
            var raw = await fs.ReadFileAsync(path) ?? "";
            return new JsonReadResult(true, JsonNode.Parse(raw), null);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new JsonReadResult(true, null, ex.Message);
        }
    }

    private static void AddRawOrWarning(List<UiBlock> blocks, string title, JsonReadResult read)
    {
        if (read.Node != null)
        {
            blocks.Add(Raw(title, read.Node));
            return;
        }

        blocks.Add(Message(
            read.FileExists ? "JSON повреждён" : "Файл отсутствует",
            read.FileExists
                ? $"{title}: {read.Error ?? "не удалось прочитать JSON"}"
                : $"{title}: файл ещё не создан."));
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

    private static UiPanelBlock Panel(string title, params UiBlock[] blocks) =>
        Panel(title, blocks.AsEnumerable());

    private static UiPanelBlock Panel(string title, IEnumerable<UiBlock> blocks) =>
        new()
        {
            Title = title,
            Blocks = blocks.ToList()
        };

    private static UiKeyValueGridBlock Grid(params (string Key, string Value)[] items) =>
        new()
        {
            Items = items
                .Select(item => new UiKeyValueItem { Key = item.Key, Value = item.Value })
                .ToList()
        };

    private static UiMessageBlock Message(string title, string message) =>
        new()
        {
            Severity = UiNotificationSeverity.Info,
            Title = title,
            Message = message
        };

    private static UiRawJsonBlock Raw(string title, JsonNode node) =>
        new()
        {
            Title = title,
            Json = node.DeepClone()
        };

    private sealed record JsonReadResult(bool FileExists, JsonNode? Node, string? Error);
}
