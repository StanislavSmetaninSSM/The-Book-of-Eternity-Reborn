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
        Threats,
        Chronicles,
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
            ["/afterlife_threats"] = CommandKind.Threats,
            ["/угрозы_загробья"] = CommandKind.Threats,
            ["/afterlife_chronicles"] = CommandKind.Chronicles,
            ["/хроники_посмертия"] = CommandKind.Chronicles,
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

    public static bool CanBuild(string command) => CommandKinds.ContainsKey(command.Trim());

    public static async Task<ExplorerCommandResult?> TryBuildAsync(
        string command,
        StateManager stateManager,
        FileSystemManager fs,
        bool includeAdvancedDiagnostics = false)
    {
        var normalizedCommand = command.Trim();
        if (!CommandKinds.TryGetValue(normalizedCommand, out var kind))
            return null;

        await stateManager.RefreshGameStateAsync();
        var includeRawDiagnostics = includeAdvancedDiagnostics || stateManager.Settings.ShowGmThoughts;

        return kind switch
        {
            CommandKind.Profiles => await BuildProfiles(normalizedCommand, fs, includeRawDiagnostics),
            CommandKind.Threats => await BuildThreats(normalizedCommand, fs),
            CommandKind.Chronicles => await BuildChronicles(normalizedCommand, fs, includeRawDiagnostics),
            CommandKind.Inbox => await BuildInbox(normalizedCommand, fs),
            CommandKind.Conflict => await BuildConflict(normalizedCommand, fs),
            CommandKind.CombatLog => await BuildCombatLog(normalizedCommand, fs),
            CommandKind.Help => BuildHelp(normalizedCommand),
            CommandKind.Arts => await BuildArts(normalizedCommand, fs, includeRawDiagnostics),
            _ => null
        };
    }

    private static async Task<ExplorerCommandResult> BuildProfiles(
        string command,
        FileSystemManager fs,
        bool includeRawDiagnostics)
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
                    ("Кастомных состояний", CountNestedArray(profiles, AfterlifeEntityProfileState.CustomStatesProperty).ToString()),
                    ("Открытых карт судьбы", CountUnlockedFateCards(profiles, includeRawDiagnostics).ToString()),
                    ("Активных личных квестов", CountActiveProfileQuests(profiles).ToString())))
        };

        if (profiles is { Count: > 0 })
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Сущности",
                Columns = ["Имя", "Тип", "Область", "Ресурсы", "Прогрессия", "Духовные искусства", "Особые искусства", "Карты судьбы", "Цели/активность", "Опасность"],
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
                            DescribeFateCards(profile["fateCards"] as JsonArray, includeRawDiagnostics),
                            DescribeProfileAgency(profile),
                            DescribeDissipation(profile)
                        ]
                    })
                    .ToList()
            });

            var relationshipsTable = BuildProfileRelationshipsTable(profiles, includeRawDiagnostics);
            if (relationshipsTable != null)
                blocks.Add(relationshipsTable);

            var masksTable = BuildProfileMasksTable(profiles, includeRawDiagnostics);
            if (masksTable != null)
                blocks.Add(masksTable);

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

        AddRawOrWarning(blocks, $"Полный JSON {AfterlifeEntityProfileState.StatePath}", read, includeRawDiagnostics);
        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildThreats(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, AfterlifeActiveThreatState.StatePath);
        var threats = read.Node?[AfterlifeActiveThreatState.ThreatsProperty] as JsonArray;
        var visibleThreats = threats?.OfType<JsonObject>().Where(IsThreatVisible).ToList() ?? [];
        var hiddenCount = Math.Max(0, (threats?.Count ?? 0) - visibleThreats.Count);
        var activeVisible = visibleThreats.Count(threat => threat["currentActivity"] is JsonObject);

        var blocks = new List<UiBlock>
        {
            Panel("Угрозы посмертия",
                Grid(
                    ("Всего известных системе", (threats?.Count ?? 0).ToString()),
                    ("Видимых игроку", visibleThreats.Count.ToString()),
                    ("Скрытых угроз не показано", hiddenCount.ToString()),
                    ("Активных видимых действий", activeVisible.ToString()),
                    ("Источник", AfterlifeActiveThreatState.StatePath)))
        };

        if (read.FileExists && read.Node == null)
        {
            blocks.Add(Message("Файл угроз повреждён", $"Не удалось прочитать {AfterlifeActiveThreatState.StatePath}: {read.Error ?? "ошибка JSON"}"));
            return Completed(command, blocks);
        }

        if (visibleThreats.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Видимые угрозы",
                Columns = ["Угроза", "Область", "Масштаб", "Архетип", "Активность", "Давление", "Связи"],
                Rows = visibleThreats
                    .Select(threat => new UiTableRow
                    {
                        Cells =
                        [
                            GetString(threat, "displayName", GetString(threat, "threatId", "Без названия")),
                            DescribeRealm(GetString(threat, "realm", "?")),
                            GetNumberOrString(threat, "intensity", "0"),
                            DescribeThreatArchetype(threat["threatArchetype"] as JsonObject),
                            DescribeThreatActivity(threat["currentActivity"] as JsonObject),
                            DescribeThreatImpact(threat["impactProfile"] as JsonObject),
                            DescribeThreatLinks(threat)
                        ]
                    })
                    .ToList()
            });
        }
        else
        {
            blocks.Add(Message("Видимых угроз нет", "Скрытые угрозы, если они есть, не раскрываются обычному интерфейсу до сюжетного раскрытия."));
        }

        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildChronicles(
        string command,
        FileSystemManager fs,
        bool includeRawDiagnostics)
    {
        var read = await ReadJson(fs, AfterlifeChronicleState.StatePath);
        var chronicles = read.Node?[AfterlifeChronicleState.ChroniclesProperty] as JsonArray;
        var visibleChronicles = chronicles?
            .OfType<JsonObject>()
            .Where(IsChronicleVisibleToPlayer)
            .OrderByDescending(GetChronicleLastUpdatedTurn)
            .ThenBy(static chronicle => GetString(chronicle, "displayName", GetString(chronicle, "chronicleId", string.Empty)), StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
        var totalChronicles = chronicles?.OfType<JsonObject>().Count() ?? 0;
        var hiddenCount = Math.Max(0, totalChronicles - visibleChronicles.Count);

        var blocks = new List<UiBlock>
        {
            Panel("Хроники посмертия",
                Grid(
                    ("Источник", AfterlifeChronicleState.StatePath),
                    ("Хроник", totalChronicles.ToString()),
                    ("Показано игроку", visibleChronicles.Count.ToString()),
                    ("Скрытых/служебных записей не показано", hiddenCount.ToString()),
                    ("Последний ход", DescribeChronicleLatestTurn(visibleChronicles))))
        };

        if (read.FileExists && read.Node == null)
        {
            blocks.Add(Message("Файл хроник повреждён", $"Не удалось прочитать {AfterlifeChronicleState.StatePath}: {read.Error ?? "ошибка JSON"}"));
            return Completed(command, blocks);
        }

        if (visibleChronicles.Count == 0)
        {
            blocks.Add(Message(
                "Хроники пока пусты",
                read.FileExists
                    ? "В afterlife_chronicles.json пока нет записей, видимых игроку."
                    : "Файл afterlife_chronicles.json пока не создан; когда ГМ запишет события посмертия, они появятся здесь."));
        }
        else
        {
            blocks.Add(BuildChronicleSummaryTable(visibleChronicles));

            var timeline = BuildChronicleTimelineTable(visibleChronicles);
            if (timeline.Rows.Count > 0)
                blocks.Add(timeline);
        }

        if (includeRawDiagnostics && read.Node != null)
            blocks.Add(Raw($"Полный JSON {AfterlifeChronicleState.StatePath}", read.Node));

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
                    ("Режим браузера", "отметка прочитанным доступна через форму")))
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
        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "notification_action",
                    Prompt = "Действие с уведомлениями",
                    Required = true,
                    Options =
                    [
                        Option("mark_all_read", "Отметить всё", "Пометить все уведомления загробья прочитанными."),
                        Option("mark_read", "Отметить одно", "Пометить одно уведомление по notificationId.")
                    ]
                },
                new UiTextInputPrompt
                {
                    Id = "notification_id",
                    Prompt = "ID уведомления",
                    Placeholder = "Только для режима «Отметить одно»"
                }
            ]);
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

    private static async Task<ExplorerCommandResult> BuildArts(
        string command,
        FileSystemManager fs,
        bool includeRawDiagnostics)
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
                    ("Режим браузера", "локальная прокачка доступна через форму")))
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

        AddRawOrWarning(blocks, "Полный JSON afterlifeCombatProfile", new JsonReadResult(soul.FileExists, combatProfile, soul.Error), includeRawDiagnostics);
        AddRawOrWarning(blocks, $"Полный JSON {AfterlifeEntityProfileState.StatePath}", profiles, includeRawDiagnostics);
        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiTextInputPrompt
                {
                    Id = "upgrade_target",
                    Prompt = "Что прокачать",
                    Placeholder = "pressure / guard / counter / maneuver / binding / spirit_focus"
                },
                new UiSelectionPrompt
                {
                    Id = "upgrade_currency",
                    Prompt = "Валюта прокачки",
                    Required = true,
                    Options =
                    [
                        Option("ink_feathers", "Чернильные Перья", "Потратить Чернильные Перья души."),
                        Option("light_sparks", "Искры Света", "Потратить Искры Света в Сияющей Обители.")
                    ]
                }
            ]);
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

    private static string DescribeFateCards(JsonArray? fateCards, bool includeHiddenDiagnostics)
    {
        if (fateCards == null || fateCards.Count == 0)
            return "нет";

        var visibleCards = fateCards
            .OfType<JsonObject>()
            .Where(card => includeHiddenDiagnostics || IsFateCardPlayerVisible(card))
            .ToList();
        if (visibleCards.Count == 0)
            return "нет известных";

        return string.Join("; ", visibleCards.Select(card =>
        {
            var name = GetString(card, "nameRu", GetString(card, "cardId", "?"));
            var status = DescribeFateCardStatus(GetString(card, "status", "locked"));
            var effects = AfterlifeEntityProfileState.FateCardMechanicalEffectProperties.Sum(propertyName => CountArray(card, propertyName));
            return effects > 0 ? $"{name}: {status}, эффектов {effects}" : $"{name}: {status}";
        }));
    }

    private static string DescribeProfileAgency(JsonObject profile)
    {
        var goals = profile["goals"] as JsonObject;
        var currentActivity = profile["currentActivity"] as JsonObject;
        var activeQuests = (profile["personalQuests"] as JsonArray)?
            .OfType<JsonObject>()
            .Where(quest => string.Equals(GetString(quest, "status", ""), "active", StringComparison.OrdinalIgnoreCase))
            .Select(quest => GetString(quest, "title", GetString(quest, "questId", "квест")))
            .ToList() ?? [];

        var parts = new List<string>();
        var shortTerm = GetString(goals, "shortTermGoal", "");
        if (!string.IsNullOrWhiteSpace(shortTerm))
            parts.Add($"цель: {shortTerm}");
        if (activeQuests.Count > 0)
            parts.Add($"квесты: {string.Join("; ", activeQuests)}");
        var activity = GetString(currentActivity, "summary", "");
        if (!string.IsNullOrWhiteSpace(activity))
            parts.Add($"сейчас: {activity}");

        return parts.Count == 0 ? "не указаны" : string.Join(" | ", parts);
    }

    private static UiTableBlock? BuildProfileRelationshipsTable(JsonArray profiles, bool includeHiddenDiagnostics)
    {
        var rows = profiles
            .OfType<JsonObject>()
            .SelectMany(profile => BuildProfileRelationshipRows(profile, includeHiddenDiagnostics))
            .ToList();
        if (rows.Count == 0)
            return null;

        return includeHiddenDiagnostics
            ? new UiTableBlock
            {
                Title = "Отношения",
                Columns = ["Сущность", "relationshipId", "Ось", "Цель", "Значение", "Тир", "Замок", "Порог", "Квесты", "Диагностика"],
                Rows = rows
            }
            : new UiTableBlock
            {
                Title = "Отношения",
                Columns = ["Сущность", "Связь", "Цель", "Текущий уровень", "Ближайший порог", "Условие открытия"],
                Rows = rows
            };
    }

    private static IEnumerable<UiTableRow> BuildProfileRelationshipRows(JsonObject profile, bool includeHiddenDiagnostics)
    {
        if (profile[AfterlifeEntityProfileState.RelationshipsProperty] is not JsonArray relationships || relationships.Count == 0)
            yield break;

        var profileName = DescribeProfileNameForRelationshipTable(profile, includeHiddenDiagnostics);
        foreach (var relationship in relationships.OfType<JsonObject>())
        {
            if (includeHiddenDiagnostics)
            {
                yield return new UiTableRow
                {
                    Cells =
                    [
                        profileName,
                        GetString(relationship, "relationshipId", "не указан"),
                        DescribeAfterlifeRelationshipAxis(GetString(relationship, "axis", "?")),
                        DescribeRelationshipTargetForDiagnostics(relationship),
                        GetNumberOrString(relationship, "value", "0"),
                        GetString(relationship, "relationshipTier", "не указан"),
                        DescribeRelationshipLockDiagnostics(relationship["relationshipLock"] as JsonObject),
                        DescribeNearestRelationshipThreshold(relationship),
                        DescribeRelationshipQuestDiagnostics(relationship[AfterlifeEntityProfileState.RelationshipGateQuestsProperty] as JsonArray),
                        DescribeRelationshipDiagnostics(relationship)
                    ]
                };
                continue;
            }

            yield return new UiTableRow
            {
                Cells =
                [
                    profileName,
                    DescribeAfterlifeRelationshipAxis(GetString(relationship, "axis", "?")),
                    DescribeRelationshipTargetForPlayer(relationship),
                    DescribeRelationshipProgressForPlayer(relationship),
                    DescribeNearestRelationshipThreshold(relationship),
                    DescribeRelationshipUnlockConditionForPlayer(relationship)
                ]
            };
        }
    }

    private static string DescribeProfileNameForRelationshipTable(JsonObject profile, bool includeHiddenDiagnostics)
    {
        var displayName = GetString(profile, "displayName", "");
        if (!includeHiddenDiagnostics)
            return string.IsNullOrWhiteSpace(displayName)
                ? DescribeActorType(GetString(profile, "actorType", "сущность"))
                : displayName;

        var actorType = GetString(profile, "actorType", "?");
        var actorId = GetString(profile, "actorId", GetString(profile, "actorRef", "?"));
        var name = string.IsNullOrWhiteSpace(displayName) ? "Без имени" : displayName;
        return $"{name} ({actorType}:{actorId})";
    }

    private static string DescribeRelationshipTargetForPlayer(JsonObject relationship)
    {
        var displayName = GetString(relationship, "targetDisplayName", "");
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = GetString(relationship, "targetActorName", GetString(relationship, "targetName", ""));
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName;

        return DescribeActorType(GetString(relationship, "targetActorType", "сущность"));
    }

    private static string DescribeRelationshipTargetForDiagnostics(JsonObject relationship)
    {
        var actorType = GetString(relationship, "targetActorType", "?");
        var actorId = GetString(relationship, "targetActorId", GetString(relationship, "targetActorRef", "?"));
        var displayName = GetString(relationship, "targetDisplayName", GetString(relationship, "targetActorName", GetString(relationship, "targetName", "")));
        return string.IsNullOrWhiteSpace(displayName)
            ? $"{actorType}:{actorId}"
            : $"{displayName} ({actorType}:{actorId})";
    }

    private static string DescribeRelationshipProgressForPlayer(JsonObject relationship)
    {
        var value = GetNumberOrString(relationship, "value", "0");
        var tier = GetString(relationship, "relationshipTier", "");
        if (string.IsNullOrWhiteSpace(tier))
            return $"{value}/100";

        return $"{value}/100; уровень {DescribeRelationshipTierForPlayer(tier)}";
    }

    private static string DescribeRelationshipTierForPlayer(string tier)
    {
        var normalized = tier.Trim().Replace('_', ' ');
        return string.IsNullOrWhiteSpace(normalized) ? "не указан" : normalized;
    }

    private static string DescribeNearestRelationshipThreshold(JsonObject relationship)
    {
        var value = GetInt(relationship["value"]);
        var threshold = ResolveRelationshipThreshold(relationship);
        if (threshold == null)
            return "нет данных";

        var delta = threshold.Value - value;
        if (delta == 0)
            return $"порог {threshold.Value} достигнут";
        if (Math.Sign(delta) == Math.Sign(threshold.Value) || threshold.Value == 0)
            return $"порог {threshold.Value}, до порога {Math.Abs(delta)}";

        return $"порог {threshold.Value} пройден на {Math.Abs(delta)}";
    }

    private static int? ResolveRelationshipThreshold(JsonObject relationship)
    {
        if (relationship["relationshipLock"] is JsonObject relationshipLock &&
            TryGetInt(relationshipLock["threshold"], out var lockThreshold))
        {
            return lockThreshold;
        }

        var value = GetInt(relationship["value"]);
        if (value >= 50)
            return 50;
        if (value <= -50)
            return -50;

        return Math.Abs(50 - value) <= Math.Abs(value + 50) ? 50 : -50;
    }

    private static string DescribeRelationshipUnlockConditionForPlayer(JsonObject relationship)
    {
        var quest = (relationship[AfterlifeEntityProfileState.RelationshipGateQuestsProperty] as JsonArray)?
            .OfType<JsonObject>()
            .OrderBy(quest => string.Equals(GetString(quest, "status", ""), "active", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .FirstOrDefault();
        if (quest != null)
        {
            var title = GetString(quest, "title", "гейт отношений");
            var questType = DescribeRelationshipQuestTypeForPlayer(GetString(quest, "questType", ""));
            var status = DescribeAfterlifeRelationshipQuestStatus(GetString(quest, "status", "active"));
            return $"условие: {questType} \"{title}\" ({status})";
        }

        var lockState = GetString(relationship["relationshipLock"] as JsonObject, "lockState", "");
        return lockState.Trim().ToLowerInvariant() switch
        {
            "positive_locked" => "условие: личный прорыв",
            "negative_locked" => "условие: искупление",
            "point_of_no_return" => "точка невозврата зафиксирована",
            "none" or "" => "нет активного гейта",
            _ => "условие скрыто до сцены"
        };
    }

    private static string DescribeRelationshipLockDiagnostics(JsonObject? relationshipLock)
    {
        if (relationshipLock == null)
            return "нет";

        var parts = new List<string>
        {
            $"lockState={GetString(relationshipLock, "lockState", "не указан")}",
            $"direction={GetString(relationshipLock, "direction", "не указано")}"
        };

        if (TryGetInt(relationshipLock["threshold"], out var threshold))
            parts.Add($"threshold={threshold}");
        AddDiagnosticPart(parts, relationshipLock, "breakthroughQuestId");
        AddDiagnosticPart(parts, relationshipLock, "redemptionQuestId");
        AddDiagnosticPart(parts, relationshipLock, "reason");
        AddDiagnosticPart(parts, relationshipLock, "evidence");
        AddDiagnosticPart(parts, relationshipLock, "updatedAtTurn");
        AddDiagnosticPart(parts, relationshipLock, "pointOfNoReturn");
        AddDiagnosticPart(parts, relationshipLock, "proofSummary");
        if (relationshipLock["proof"] is JsonObject proof)
            parts.Add($"proof={proof.ToJsonString()}");

        return string.Join("; ", parts);
    }

    private static string DescribeRelationshipQuestDiagnostics(JsonArray? quests)
    {
        if (quests == null || quests.Count == 0)
            return "нет";

        var summaries = quests
            .OfType<JsonObject>()
            .Select(quest =>
            {
                var parts = new List<string>();
                AddDiagnosticPart(parts, quest, "questId");
                AddDiagnosticPart(parts, quest, "questType");
                AddDiagnosticPart(parts, quest, "status");
                AddDiagnosticPart(parts, quest, "title");
                AddDiagnosticPart(parts, quest, "sceneSummary");
                AddDiagnosticPart(parts, quest, "successCondition");
                AddDiagnosticPart(parts, quest, "gmThoughtsSummary");
                AddDiagnosticPart(parts, quest, "evidence");
                AddDiagnosticPart(parts, quest, "breakthroughQuestId");
                AddDiagnosticPart(parts, quest, "redemptionQuestId");
                AddDiagnosticPart(parts, quest, "updatedAtTurn");
                return parts.Count == 0 ? "пустой квест" : string.Join("; ", parts);
            });
        return string.Join(" | ", summaries);
    }

    private static string DescribeRelationshipDiagnostics(JsonObject relationship)
    {
        var parts = new List<string>();
        AddDiagnosticPart(parts, relationship, "reason");
        AddDiagnosticPart(parts, relationship, "evidence");
        AddDiagnosticPart(parts, relationship, "gmThoughtsSummary");
        AddDiagnosticPart(parts, relationship, "updatedAtTurn");
        return parts.Count == 0 ? "нет" : string.Join("; ", parts);
    }

    private static void AddDiagnosticPart(List<string> parts, JsonObject source, string propertyName)
    {
        var value = GetNodeString(source[propertyName]);
        if (!string.IsNullOrWhiteSpace(value))
            parts.Add($"{propertyName}={value}");
    }

    private static UiTableBlock? BuildProfileMasksTable(JsonArray profiles, bool includeHiddenDiagnostics)
    {
        var rows = profiles
            .OfType<JsonObject>()
            .SelectMany(profile => BuildProfileMaskRows(profile, includeHiddenDiagnostics))
            .ToList();
        if (rows.Count == 0)
            return null;

        return includeHiddenDiagnostics
            ? new UiTableBlock
            {
                Title = "Маски",
                Columns = ["Сущность", "Маска", "maskId", "Статус", "Публичная роль", "Видимое поведение", "Скрытая истина", "Директивы", "Условия раскрытия", "Связи", "Риск"],
                Rows = rows
            }
            : new UiTableBlock
            {
                Title = "Маски",
                Columns = ["Сущность", "Маска", "Статус", "Публичная роль", "Видимое поведение", "Раскрытая истина", "Риск"],
                Rows = rows
            };
    }

    private static IEnumerable<UiTableRow> BuildProfileMaskRows(JsonObject profile, bool includeHiddenDiagnostics)
    {
        if (profile[AfterlifeEntityProfileState.MasksProperty] is not JsonArray masks || masks.Count == 0)
            yield break;

        var profileName = GetString(profile, "displayName", GetString(profile, "actorId", "Без имени"));
        var activeMaskId = GetString(profile, AfterlifeEntityProfileState.ActiveMaskIdProperty, "");
        foreach (var mask in masks.OfType<JsonObject>())
        {
            var maskId = GetString(mask, "maskId", "");
            var isActive = !string.IsNullOrWhiteSpace(maskId) &&
                           string.Equals(maskId, activeMaskId, StringComparison.OrdinalIgnoreCase);
            var isRevealed = IsAfterlifeMaskRevealed(mask);
            if (!includeHiddenDiagnostics && !isActive && !isRevealed)
                continue;

            var displayName = GetString(mask, "displayName", string.IsNullOrWhiteSpace(maskId) ? "Без названия" : maskId);
            var publicArchetype = GetString(mask, "publicArchetype", "не указан");
            var visiblePersonality = GetString(mask, "visiblePersonality", "не указано");
            var risk = DescribeMaskRisk(GetString(mask, "deceptionRisk", ""));
            if (includeHiddenDiagnostics)
            {
                yield return new UiTableRow
                {
                    Cells =
                    [
                        profileName,
                        displayName,
                        string.IsNullOrWhiteSpace(maskId) ? "не указан" : maskId,
                        DescribeMaskStatus(isActive, isRevealed),
                        publicArchetype,
                        visiblePersonality,
                        GetString(mask, "concealedTruth", "не указана"),
                        JoinLiteralStringArray(mask["directives"] as JsonArray),
                        JoinLiteralStringArray(mask["revealConditions"] as JsonArray),
                        DescribeMaskLinks(mask),
                        risk
                    ]
                };
                continue;
            }

            yield return new UiTableRow
            {
                Cells =
                [
                    profileName,
                    displayName,
                    DescribeMaskStatus(isActive, isRevealed),
                    publicArchetype,
                    visiblePersonality,
                    isRevealed ? GetString(mask, "concealedTruth", "раскрыта, подробности не указаны") : "не раскрыта",
                    risk
                ]
            };
        }
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

    private static bool IsAfterlifeMaskRevealed(JsonObject mask) =>
        mask["isRevealed"] is JsonValue value && value.TryGetValue<bool>(out var revealed) && revealed;

    private static string DescribeMaskStatus(bool isActive, bool isRevealed)
    {
        var status = isRevealed ? "раскрыта" : "истина не раскрыта";
        return isActive ? $"активная; {status}" : status;
    }

    private static string DescribeMaskRisk(string risk) =>
        risk.Trim().ToLowerInvariant() switch
        {
            "low" => "низкий",
            "medium" => "средний",
            "high" => "высокий",
            "critical" => "критический",
            "" => "не указан",
            _ => risk
        };

    private static string DescribeMaskLinks(JsonObject mask)
    {
        var links = new List<string>();
        var linkedThreatId = GetString(mask, "linkedThreatId", "");
        if (!string.IsNullOrWhiteSpace(linkedThreatId))
            links.Add($"угроза {linkedThreatId}");
        var linkedSarefAgentId = GetString(mask, "linkedSarefAgentId", "");
        if (!string.IsNullOrWhiteSpace(linkedSarefAgentId))
            links.Add($"агент Сарефа {linkedSarefAgentId}");
        return links.Count == 0 ? "нет" : string.Join("; ", links);
    }

    private static bool IsThreatVisible(JsonObject threat) =>
        threat["visibleToPlayer"] is JsonValue value && value.TryGetValue<bool>(out var visible) && visible;

    private static string DescribeThreatArchetype(JsonObject? archetype)
    {
        if (archetype == null)
            return "не указан";

        var motivation = DescribeThreatMotivation(GetString(archetype, "motivation", "?"));
        var method = DescribeThreatMethod(GetString(archetype, "method", "?"));
        var summary = GetString(archetype, "summary", "");
        return string.IsNullOrWhiteSpace(summary)
            ? $"{motivation}; метод: {method}"
            : $"{motivation}; метод: {method}; {summary}";
    }

    private static string DescribeThreatActivity(JsonObject? activity)
    {
        if (activity == null)
            return "нет текущего действия";

        var summary = GetString(activity, "summary", GetString(activity, "activityId", "активность"));
        var state = GetString(activity, "activeState", "active");
        var turn = GetNumberOrString(activity, "startedAtTurn", "?");
        return $"{summary}; состояние {state}; с хода {turn}";
    }

    private static string DescribeThreatImpact(JsonObject? impact)
    {
        if (impact == null)
            return "не указано";

        var target = GetString(impact, "primaryTargetName", GetString(impact, "primaryTargetId", "?"));
        var targetType = DescribeThreatTargetType(GetString(impact, "primaryTargetType", "?"));
        var impactType = DescribeThreatImpactType(GetString(impact, "primaryImpact", "?"));
        var value = GetNumberOrString(impact, "baseImpactValue", "0");
        return $"{targetType}: {target}; давление: {impactType}; сила {value}";
    }

    private static string DescribeThreatLinks(JsonObject threat)
    {
        var links = new List<string>();
        var factionId = GetString(threat, "linkedFactionId", "");
        if (!string.IsNullOrWhiteSpace(factionId))
            links.Add($"фракция {factionId}");
        var guardianId = GetString(threat, "linkedGuardianId", "");
        if (!string.IsNullOrWhiteSpace(guardianId))
            links.Add($"Хранитель {guardianId}");
        if (threat["sarefLink"] is JsonObject sarefLink && IsSarefThreatLinkVisible(sarefLink))
            links.Add($"скрытая линия: {GetString(sarefLink, "linkType", "связь")}");
        return links.Count == 0 ? "нет" : string.Join("; ", links);
    }

    private static bool IsSarefThreatLinkVisible(JsonObject sarefLink)
    {
        var visibility = GetString(sarefLink, "visibility", "");
        return string.Equals(visibility, "visible", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "revealed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "player_known", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeThreatMotivation(string motivation) =>
        motivation.Trim().ToLowerInvariant() switch
        {
            "predation" => "охота",
            "subversion" => "подрыв",
            "domination" => "господство",
            "consumption" => "пожирание",
            "conquest" => "захват",
            "corruption" => "искажение",
            "accumulation" => "накопление",
            "execution" => "исполнение приговора",
            "preservation" => "сохранение",
            "revenge" => "месть",
            "survival" => "выживание",
            "custom" => "особый мотив",
            _ => motivation
        };

    private static string DescribeThreatMethod(string method) =>
        method.Trim().ToLowerInvariant() switch
        {
            "stalking" => "выслеживание",
            "infiltration" => "проникновение",
            "overt" => "открытое действие",
            "covert" => "скрытое действие",
            "deceptive" => "обман",
            "opportunistic" => "использование возможности",
            "systemic" => "системное давление",
            "storm" => "буря",
            "curse" => "проклятие",
            "military_pressure" => "военное давление",
            "political_plot" => "политический заговор",
            "custom" => "особый метод",
            _ => method
        };

    private static string DescribeThreatTargetType(string targetType) =>
        targetType.Trim().ToLowerInvariant() switch
        {
            "faction" => "фракция",
            "location" => "локация",
            "resource" => "ресурс",
            "guardian" => "Хранитель",
            "resident" => "резидент",
            "actor" => "сущность",
            "realm" => "область",
            "scope" => "зона",
            _ => targetType
        };

    private static string DescribeThreatImpactType(string impactType) =>
        impactType.Trim().ToLowerInvariant() switch
        {
            "military" => "военное",
            "economic" => "экономическое",
            "social" => "социальное",
            "covert" => "скрытое",
            "stability" => "стабильность",
            "environment" => "среда",
            "combat" => "бой",
            "politics" => "политика",
            "relationship" => "отношения",
            "progression" => "прогрессия",
            _ => impactType
        };

    private static UiTableBlock BuildChronicleSummaryTable(IReadOnlyList<JsonObject> chronicles) =>
        new()
        {
            Title = "Ключевые события посмертия",
            Columns = ["Хроника", "Область", "Ход", "Последнее событие", "Участники", "Последствия", "Открытые нити"],
            Rows = chronicles.Select(chronicle => new UiTableRow
            {
                Cells =
                [
                    SafeChronicleText(GetString(chronicle, "displayName", GetString(chronicle, "chronicleId", "Без названия"))),
                    DescribeChronicleScope(chronicle),
                    GetNumberOrString(chronicle, "lastUpdatedTurn", "?"),
                    SafeChronicleText(GetString(chronicle, "lastEventsDescription", "нет")),
                    DescribeChronicleParticipants(chronicle),
                    DescribeChronicleStringArray(chronicle, "persistentConsequences"),
                    DescribeChronicleStringArray(chronicle, "openThreads")
                ]
            }).ToList()
        };

    private static UiTableBlock BuildChronicleTimelineTable(IReadOnlyList<JsonObject> chronicles)
    {
        var rows = new List<UiTableRow>();
        foreach (var chronicle in chronicles)
        {
            var chronicleName = SafeChronicleText(GetString(chronicle, "displayName", GetString(chronicle, "chronicleId", "Хроника")));
            foreach (var eventText in EnumerateChronicleTextArray(chronicle, "eventDescriptions"))
            {
                rows.Add(new UiTableRow
                {
                    Cells =
                    [
                        DescribeChronicleEventTurn(chronicle, eventText, preferLastUpdatedTurn: false),
                        chronicleName,
                        eventText
                    ]
                });
            }

            var lastEvents = SafeChronicleText(GetString(chronicle, "lastEventsDescription", ""));
            if (!string.IsNullOrWhiteSpace(lastEvents))
            {
                rows.Add(new UiTableRow
                {
                    Cells =
                    [
                        DescribeChronicleEventTurn(chronicle, lastEvents, preferLastUpdatedTurn: true),
                        chronicleName,
                        lastEvents
                    ]
                });
            }
        }

        return new UiTableBlock
        {
            Title = "Хронология",
            Columns = ["Ход", "Хроника", "Событие"],
            Rows = rows
                .OrderByDescending(static row => TryParseInt(row.Cells[0], out var turn) ? turn : 0)
                .ThenBy(static row => row.Cells[1], StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static bool IsChronicleVisibleToPlayer(JsonObject chronicle)
    {
        if (!IsChronicleObjectVisibleToPlayer(chronicle))
            return false;

        return IsPlayerSafeChronicleText(GetString(chronicle, "chronicleId", "")) &&
               IsPlayerSafeChronicleText(GetString(chronicle, "displayName", "")) &&
               IsPlayerSafeChronicleText(GetString(chronicle, "scopeType", "")) &&
               IsPlayerSafeChronicleText(GetString(chronicle, "scopeId", ""));
    }

    private static bool IsChronicleObjectVisibleToPlayer(JsonObject obj)
    {
        if (IsFalseFlag(obj["isPlayerVisible"]) ||
            IsFalseFlag(obj["playerVisible"]) ||
            IsFalseFlag(obj["visibleToPlayer"]) ||
            IsFalseFlag(obj["visibleForPlayer"]))
            return false;

        if (IsTrueFlag(obj["isHidden"]) ||
            IsTrueFlag(obj["hidden"]) ||
            IsTrueFlag(obj["isSecret"]) ||
            IsTrueFlag(obj["secret"]) ||
            IsTrueFlag(obj["gmOnly"]) ||
            IsTrueFlag(obj["isGmOnly"]) ||
            IsTrueFlag(obj["internal"]) ||
            IsTrueFlag(obj["isInternal"]))
            return false;

        var visibility = GetString(obj, "visibility", "");
        if (IsChronicleHiddenVisibility(visibility))
            return false;

        var audience = GetString(obj, "audience", "");
        return !IsChronicleHiddenVisibility(audience);
    }

    private static string DescribeChronicleLatestTurn(IReadOnlyList<JsonObject> chronicles)
    {
        var latest = chronicles.Select(GetChronicleLastUpdatedTurn).DefaultIfEmpty(0).Max();
        return latest > 0 ? latest.ToString() : "нет";
    }

    private static int GetChronicleLastUpdatedTurn(JsonObject chronicle) =>
        TryGetInt(chronicle["lastUpdatedTurn"], out var turn) ? turn : 0;

    private static string DescribeChronicleScope(JsonObject chronicle)
    {
        var scopeType = SafeChronicleText(GetString(chronicle, "scopeType", ""));
        var scopeId = SafeChronicleText(GetString(chronicle, "scopeId", ""));
        if (string.IsNullOrWhiteSpace(scopeType) && string.IsNullOrWhiteSpace(scopeId))
            return "не указано";
        if (string.IsNullOrWhiteSpace(scopeType))
            return scopeId;
        if (string.IsNullOrWhiteSpace(scopeId))
            return scopeType;
        return $"{scopeType}:{scopeId}";
    }

    private static string DescribeChronicleParticipants(JsonObject chronicle)
    {
        var participants = new List<string>();
        foreach (var propertyName in new[] { "participants", "participantActors", "linkedActors", "actors" })
        {
            if (IsChronicleHiddenPropertyName(propertyName) || chronicle[propertyName] is not JsonArray array)
                continue;

            foreach (var item in array)
            {
                var participant = DescribeChronicleParticipant(item);
                if (!string.IsNullOrWhiteSpace(participant))
                    participants.Add(participant);
            }
        }

        var distinct = participants
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return distinct.Length == 0 ? "нет" : string.Join("; ", distinct);
    }

    private static string? DescribeChronicleParticipant(JsonNode? item)
    {
        if (item is JsonValue)
        {
            var text = SafeChronicleText(GetNodeString(item) ?? string.Empty);
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        if (item is not JsonObject obj || !IsChronicleObjectVisibleToPlayer(obj))
            return null;

        var name = SafeChronicleText(GetString(obj, "displayName", GetString(obj, "name", GetString(obj, "actorId", ""))));
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var actorType = SafeChronicleText(GetString(obj, "actorType", GetString(obj, "type", "")));
        return string.IsNullOrWhiteSpace(actorType)
            ? name
            : $"{name} ({DescribeActorType(actorType)})";
    }

    private static string DescribeChronicleStringArray(JsonObject chronicle, string propertyName)
    {
        if (IsChronicleHiddenPropertyName(propertyName))
            return "нет";

        var items = EnumerateChronicleTextArray(chronicle, propertyName).ToArray();
        return items.Length == 0 ? "нет" : string.Join("; ", items);
    }

    private static IEnumerable<string> EnumerateChronicleTextArray(JsonObject chronicle, string propertyName)
    {
        if (chronicle[propertyName] is not JsonArray array)
            yield break;

        foreach (var item in array)
        {
            var text = SafeChronicleText(GetNodeString(item) ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(text))
                yield return text;
        }
    }

    private static string DescribeChronicleEventTurn(JsonObject chronicle, string eventText, bool preferLastUpdatedTurn)
    {
        if (preferLastUpdatedTurn && GetChronicleLastUpdatedTurn(chronicle) > 0)
            return GetChronicleLastUpdatedTurn(chronicle).ToString();

        if (TryParseTurnFromChronicleText(eventText, out var parsed))
            return parsed.ToString();

        return GetChronicleLastUpdatedTurn(chronicle) > 0
            ? GetChronicleLastUpdatedTurn(chronicle).ToString()
            : "?";
    }

    private static bool TryParseTurnFromChronicleText(string text, out int turn)
    {
        turn = 0;
        var markerIndex = text.IndexOf("[Turn ", StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return false;

        var start = markerIndex + "[Turn ".Length;
        var end = start;
        while (end < text.Length && char.IsDigit(text[end]))
            end++;

        return end > start && int.TryParse(text[start..end], out turn);
    }

    private static string SafeChronicleText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        return IsPlayerSafeChronicleText(trimmed) ? trimmed : string.Empty;
    }

    private static bool IsPlayerSafeChronicleText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var lower = value.Trim().ToLowerInvariant();
        return !lower.Contains("hidden_", StringComparison.Ordinal) &&
               !lower.Contains("secret_", StringComparison.Ordinal) &&
               !lower.Contains("internal_", StringComparison.Ordinal) &&
               !lower.Contains("gm_only", StringComparison.Ordinal) &&
               !lower.Contains("gm-only", StringComparison.Ordinal) &&
               !lower.Contains("gm only", StringComparison.Ordinal) &&
               !lower.Contains("gmthoughts", StringComparison.Ordinal) &&
               !lower.Contains("gm thoughts", StringComparison.Ordinal) &&
               !lower.Contains("lastinvalidchronicleupdate", StringComparison.Ordinal) &&
               !lower.Contains("не раскрывать", StringComparison.Ordinal) &&
               !lower.Contains("не показывать игроку", StringComparison.Ordinal) &&
               !lower.Contains("do not reveal", StringComparison.Ordinal) &&
               !lower.Contains("don't reveal", StringComparison.Ordinal) &&
               !lower.Contains("player-facing", StringComparison.Ordinal) &&
               !lower.Contains("player facing", StringComparison.Ordinal);
    }

    private static bool IsChronicleHiddenPropertyName(string propertyName)
    {
        var normalized = propertyName.Trim();
        return normalized.StartsWith("_", StringComparison.Ordinal) ||
               normalized.StartsWith("hidden", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("secret", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("internal", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("gmThoughts", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, AfterlifeChronicleState.LastInvalidUpdateProperty, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, AfterlifeChronicleState.LastInvalidUpdateReasonProperty, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsChronicleHiddenVisibility(string? visibility)
    {
        if (string.IsNullOrWhiteSpace(visibility))
            return false;

        var normalized = visibility.Trim();
        return string.Equals(normalized, "hidden", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "gm_only", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "secret", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "private", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "internal", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFalseFlag(JsonNode? node) =>
        node is JsonValue value &&
        value.TryGetValue<bool>(out var flag) &&
        !flag;

    private static bool IsTrueFlag(JsonNode? node) =>
        node is JsonValue value &&
        value.TryGetValue<bool>(out var flag) &&
        flag;

    private static bool TryParseInt(string? value, out int result) =>
        int.TryParse(value, out result);

    private static string DescribeFateCardStatus(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "hidden" => "скрыта",
            "available" => "может быть открыта",
            "unlocked" => "открыта",
            _ => "закрыта"
        };

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

    private static string DescribeAfterlifeRelationshipAxis(string? axis) =>
        axis?.Trim().ToLowerInvariant() switch
        {
            "trust" => "Доверие",
            "romance" => "Романтическая связь",
            "rivalry" => "Соперничество",
            "oath" => "Клятва",
            "fear" => "Страх",
            "reverence" => "Почтение",
            "debt" => "Долг",
            _ => string.IsNullOrWhiteSpace(axis) ? "?" : axis
        };

    private static string DescribeRelationshipQuestTypeForPlayer(string? questType) =>
        questType?.Trim().ToLowerInvariant() switch
        {
            "breakthrough" => "прорыв",
            "redemption" => "искупление",
            _ => "гейт отношений"
        };

    private static string DescribeAfterlifeRelationshipQuestStatus(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "completed" => "завершён",
            "failed" => "провален",
            "cancelled" => "отменён",
            _ => "активен"
        };

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
            "shining_resident" => "Резидент Сияющей Обители",
            "shining_faction_head" => "Глава фракции",
            "saref_agent" => "Агент Сарефа",
            "system_actor" => "Системная сила",
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

    private static string JoinLiteralStringArray(JsonArray? array)
    {
        if (array == null || array.Count == 0)
            return "нет";

        var items = array
            .Select(GetNodeString)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        return items.Length == 0 ? "нет" : string.Join("; ", items);
    }

    private static int CountProfilesWithDissipation(JsonArray? profiles) =>
        profiles?.OfType<JsonObject>().Count(profile => GetInt(profile["soulDissipationTier"]) > 0) ?? 0;

    private static int CountNestedArray(JsonArray? owners, string propertyName) =>
        owners?.OfType<JsonObject>().Sum(owner => CountArray(owner, propertyName)) ?? 0;

    private static int CountUnlockedFateCards(JsonArray? profiles, bool includeHiddenDiagnostics) =>
        profiles?.OfType<JsonObject>()
            .SelectMany(profile => (profile["fateCards"] as JsonArray)?.OfType<JsonObject>() ?? [])
            .Where(card => includeHiddenDiagnostics || IsFateCardPlayerVisible(card))
            .Count(card => string.Equals(GetString(card, "status", ""), "unlocked", StringComparison.OrdinalIgnoreCase)) ?? 0;

    private static int CountActiveProfileQuests(JsonArray? profiles) =>
        profiles?.OfType<JsonObject>()
            .SelectMany(profile => (profile["personalQuests"] as JsonArray)?.OfType<JsonObject>() ?? [])
            .Count(quest => string.Equals(GetString(quest, "status", ""), "active", StringComparison.OrdinalIgnoreCase)) ?? 0;

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

    private static bool TryGetInt(JsonNode? node, out int result)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out result))
            return true;
        if (node is JsonValue stringValue &&
            stringValue.TryGetValue<string>(out var text) &&
            int.TryParse(text, out var parsed))
        {
            result = parsed;
            return true;
        }

        result = 0;
        return false;
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

    private static bool IsFateCardPlayerVisible(JsonObject card)
    {
        var secret = card["isSecret"] is JsonValue secretValue &&
                     secretValue.TryGetValue<bool>(out var secretBool) &&
                     secretBool;
        var status = GetString(card, "status", "");
        return !secret && !string.Equals(status, "hidden", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddRawOrWarning(
        List<UiBlock> blocks,
        string title,
        JsonReadResult read,
        bool includeRawDiagnostics = true)
    {
        if (read.Node != null)
        {
            if (includeRawDiagnostics)
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
        Result(command, CommandExecutionState.Completed, blocks);

    private static ExplorerCommandResult Result(
        string command,
        CommandExecutionState state,
        IEnumerable<UiBlock> blocks,
        IEnumerable<UiPrompt>? prompts = null) =>
        new()
        {
            Command = command,
            State = state,
            Blocks = blocks.ToList(),
            Prompts = prompts?.ToList() ?? []
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

    private static UiSelectionOption Option(string value, string label, string description) =>
        new() { Value = value, Label = label, Description = description };

    private sealed record JsonReadResult(bool FileExists, JsonNode? Node, string? Error);
}
