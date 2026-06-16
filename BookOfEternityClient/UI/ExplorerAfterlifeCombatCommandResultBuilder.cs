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

    public static bool CanBuild(string command)
    {
        var request = ParseCommandRequest(command);
        return request != null && CommandKinds.ContainsKey(request.CommandToken);
    }

    public static async Task<ExplorerCommandResult?> TryBuildAsync(
        string command,
        StateManager stateManager,
        FileSystemManager fs,
        bool includeAdvancedDiagnostics = false)
    {
        var request = ParseCommandRequest(command);
        if (request == null || !CommandKinds.TryGetValue(request.CommandToken, out var kind))
            return null;

        await stateManager.RefreshGameStateAsync();
        var includeRawDiagnostics = includeAdvancedDiagnostics || stateManager.Settings.ShowGmThoughts;

        return kind switch
        {
            CommandKind.Profiles => await BuildProfiles(request, fs, includeRawDiagnostics),
            CommandKind.Threats => await BuildThreats(request, fs, includeRawDiagnostics),
            CommandKind.Chronicles => await BuildChronicles(request, fs, includeRawDiagnostics),
            CommandKind.Inbox => await BuildInbox(request, fs),
            CommandKind.Conflict => await BuildConflict(request.Command, fs),
            CommandKind.CombatLog => await BuildCombatLog(request.Command, fs),
            CommandKind.Help => BuildHelp(request.Command),
            CommandKind.Arts => await BuildArts(request.Command, fs, includeRawDiagnostics),
            _ => null
        };
    }

    private static async Task<ExplorerCommandResult> BuildProfiles(
        CommandRequest request,
        FileSystemManager fs,
        bool includeRawDiagnostics)
    {
        var read = await ReadJson(fs, AfterlifeEntityProfileState.StatePath);
        var profiles = read.Node?[AfterlifeEntityProfileState.ProfilesProperty] as JsonArray;
        var allProfiles = profiles?.OfType<JsonObject>().ToList() ?? [];
        var visibleProfiles = includeRawDiagnostics
            ? allProfiles
            : allProfiles.Where(IsProfileVisibleToPlayer).ToList();
        var detail = ParseDetailRequest(request.Arguments, "профиль", "profile", "сущность", "entity", "деталь", "detail");
        if (!string.IsNullOrWhiteSpace(detail.Selector))
            return BuildProfileDetail(request.Command, visibleProfiles, detail.Selector);

        var hiddenCount = Math.Max(0, allProfiles.Count - visibleProfiles.Count);
        var blocks = new List<UiBlock>
        {
            Panel("Профили сущностей посмертия",
                Grid(
                    ("Профилей", visibleProfiles.Count.ToString()),
                    ("Скрытых профилей не показано", hiddenCount.ToString()),
                    ("Опасных развеивателей души", CountProfilesWithDissipation(visibleProfiles).ToString()),
                    ("Особых духовных искусств", CountNestedArray(visibleProfiles, "specialArts").ToString()),
                    ("Кастомных состояний", CountNestedArray(visibleProfiles, AfterlifeEntityProfileState.CustomStatesProperty).ToString()),
                    ("Открытых карт судьбы", CountUnlockedFateCards(visibleProfiles, includeRawDiagnostics).ToString()),
                    ("Активных личных квестов", CountActiveProfileQuests(visibleProfiles).ToString())))
        };

        if (visibleProfiles.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Сущности",
                Columns = ["Имя", "Тип", "Область", "Ресурсы", "Прогрессия", "Духовные искусства", "Особые искусства", "Карты судьбы", "Цели/активность", "Опасность", "Подробно"],
                Rows = visibleProfiles
                    .Select(profile =>
                    {
                        var selector = ProfileSelector(profile);
                        return new UiTableRow
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
                                DescribeDissipation(profile),
                                string.IsNullOrWhiteSpace(selector) ? "не указано" : BuildProfileDetailCommand(selector)
                            ]
                        };
                    })
                    .ToList()
            });

            var relationshipsTable = BuildProfileRelationshipsTable(visibleProfiles, includeRawDiagnostics);
            if (relationshipsTable != null)
                blocks.Add(relationshipsTable);

            var masksTable = BuildProfileMasksTable(visibleProfiles, includeRawDiagnostics);
            if (masksTable != null)
                blocks.Add(masksTable);

            blocks.Add(new UiTableBlock
            {
                Title = "Стратегии прокачки",
                Columns = ["Сущность", "Стратегия", "Приоритеты", "Последний цикл"],
                Rows = visibleProfiles
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
            blocks.Add(Message(
                "Профили не найдены",
                allProfiles.Count > 0
                    ? "Известные записи сейчас скрыты от текущей души."
                    : "Когда ГМ откроет значимые сущности посмертия, они появятся здесь."));
        }

        AddRawOrWarning(
            blocks,
            includeRawDiagnostics ? $"Полный JSON {AfterlifeEntityProfileState.StatePath}" : "Профили посмертия",
            read,
            includeRawDiagnostics);
        return Completed(request.Command, blocks, BuildProfileActions(visibleProfiles));
    }

    private static async Task<ExplorerCommandResult> BuildThreats(
        CommandRequest request,
        FileSystemManager fs,
        bool includeRawDiagnostics)
    {
        var read = await ReadJson(fs, AfterlifeActiveThreatState.StatePath);
        var threats = read.Node?[AfterlifeActiveThreatState.ThreatsProperty] as JsonArray;
        var visibleThreats = threats?.OfType<JsonObject>().Where(IsThreatVisible).ToList() ?? [];
        var detail = ParseDetailRequest(request.Arguments, "угроза", "threat", "деталь", "detail");
        if (!string.IsNullOrWhiteSpace(detail.Selector))
            return BuildThreatDetail(request.Command, visibleThreats, detail.Selector);

        var hiddenCount = Math.Max(0, (threats?.Count ?? 0) - visibleThreats.Count);
        var activeVisible = visibleThreats.Count(threat => threat["currentActivity"] is JsonObject);

        var blocks = new List<UiBlock>
        {
            Panel("Угрозы посмертия",
                Grid(
                    ("Всего известных системе", (threats?.Count ?? 0).ToString()),
                    ("Видимых игроку", visibleThreats.Count.ToString()),
                    ("Скрытых угроз не показано", hiddenCount.ToString()),
                    ("Активных видимых действий", activeVisible.ToString())))
        };

        if (read.FileExists && read.Node == null)
        {
            blocks.Add(Message("Угрозы сейчас недоступны", "Не удалось прочитать видимые угрозы посмертия. Откройте сводку позже или попросите ГМ обновить состояние."));
            return Completed(request.Command, blocks);
        }

        if (visibleThreats.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Видимые угрозы",
                Columns = ["Угроза", "Область", "Масштаб", "Архетип", "Активность", "Давление", "Связи", "Подробно"],
                Rows = visibleThreats
                    .Select(threat =>
                    {
                        var selector = ThreatSelector(threat);
                        return new UiTableRow
                        {
                            Cells =
                            [
                                GetString(threat, "displayName", GetString(threat, "threatId", "Без названия")),
                                DescribeRealm(GetString(threat, "realm", "?")),
                                GetNumberOrString(threat, "intensity", "0"),
                                DescribeThreatArchetype(threat["threatArchetype"] as JsonObject),
                                DescribeThreatActivity(threat["currentActivity"] as JsonObject),
                                DescribeThreatImpact(threat["impactProfile"] as JsonObject),
                                DescribeThreatLinks(threat),
                                string.IsNullOrWhiteSpace(selector) ? "не указано" : BuildThreatDetailCommand(selector)
                            ]
                        };
                    })
                    .ToList()
            });
        }
        else
        {
            blocks.Add(Message("Видимых угроз нет", "Скрытые угрозы, если они есть, не раскрываются обычному интерфейсу до сюжетного раскрытия."));
        }

        if (includeRawDiagnostics && read.Node != null)
            blocks.Add(Raw($"Полный JSON {AfterlifeActiveThreatState.StatePath}", read.Node));

        return Completed(request.Command, blocks, BuildThreatActions(visibleThreats));
    }

    private static async Task<ExplorerCommandResult> BuildChronicles(
        CommandRequest request,
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
        var detail = ParseDetailRequest(request.Arguments, "хроника", "chronicle", "событие", "event", "деталь", "detail");
        if (!string.IsNullOrWhiteSpace(detail.Selector))
            return BuildChronicleDetail(request.Command, visibleChronicles, detail.Selector);

        var totalChronicles = chronicles?.OfType<JsonObject>().Count() ?? 0;
        var hiddenCount = Math.Max(0, totalChronicles - visibleChronicles.Count);

        var blocks = new List<UiBlock>
        {
            Panel("Хроники посмертия",
                Grid(
                    ("Хроник", totalChronicles.ToString()),
                    ("Показано игроку", visibleChronicles.Count.ToString()),
                    ("Скрытых/служебных записей не показано", hiddenCount.ToString()),
                    ("Последний ход", DescribeChronicleLatestTurn(visibleChronicles))))
        };

        if (read.FileExists && read.Node == null)
        {
            blocks.Add(Message("Хроники сейчас недоступны", "Не удалось прочитать видимые хроники посмертия. Откройте сводку позже или попросите ГМ обновить состояние."));
            return Completed(request.Command, blocks);
        }

        if (visibleChronicles.Count == 0)
        {
            blocks.Add(Message(
                "Хроники пока пусты",
                read.FileExists
                    ? "Пока нет записей, видимых текущей душе."
                    : "Когда ГМ запишет события посмертия, они появятся здесь."));
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

        return Completed(request.Command, blocks, BuildChronicleActions(visibleChronicles));
    }

    private static async Task<ExplorerCommandResult> BuildInbox(CommandRequest request, FileSystemManager fs)
    {
        var read = await ReadJson(fs, AfterlifeNotificationState.NotificationsPath);
        var notifications = await AfterlifeNotificationState.ReadAsync(fs);
        var notificationNodes = BuildNotificationNodeMap(read.Node);
        var detail = ParseDetailRequest(request.Arguments, "уведомление", "notification", "notice", "деталь", "detail");
        if (!string.IsNullOrWhiteSpace(detail.Selector))
            return BuildInboxNotificationDetail(request.Command, notifications, notificationNodes, detail.Selector);

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
                Columns = ["Статус", "Тип", "Сводка", "Источник", "Ход", "Подробно"],
                Rows = notifications.Select(notification => new UiTableRow
                {
                    Cells =
                    [
                        DescribeNotificationStatus(notification.Status),
                        AfterlifeNotificationState.GetTypeLabel(notification.NotificationType),
                        EmptyFallback(notification.Summary),
                        DescribeNotificationSource(notification),
                        notification.CreatedAtTurn > 0 ? notification.CreatedAtTurn.ToString() : "?",
                        BuildInboxNotificationDetailCommand(notification.NotificationId)
                    ]
                }).ToList()
            });
        }
        else
        {
            blocks.Add(Message("Нет уведомлений", "Пока нет ответов Хранителей, Архива, резидентов или Сияющей Обители."));
        }

        return Result(
            request.Command,
            CommandExecutionState.RequiresInput,
            blocks,
            BuildInboxActions(notifications, notificationNodes),
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

            var visibleConditions = BuildVisibleCombatConditionsTable(active["combatConditions"] as JsonArray);
            if (visibleConditions != null)
                blocks.Add(visibleConditions);
        }
        else
        {
            blocks.Add(Message("Активного духовного конфликта нет", "ГМ может начать конфликт через afterlifeSpiritualConflictUpdate с mode=start."));
        }

        AddRawOrWarning(blocks, $"Полный JSON {AfterlifeSpiritualConflictState.StatePath}", SanitizeCombatConditionsForPlayer(read));
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
        if (active?["combatConditions"] is JsonArray combatConditions)
        {
            var visibleConditions = BuildVisibleCombatConditionsTable(combatConditions);
            if (visibleConditions != null)
                blocks.Add(visibleConditions);
        }
        if (recent is { Count: > 0 })
            blocks.Add(BuildRecentConflictTable(recent));
        if ((exchangeLog?.Count ?? 0) == 0 && (recent?.Count ?? 0) == 0)
            blocks.Add(Message("Журнал пуст", "Когда ГМ проведёт обмен или завершит конфликт, здесь появятся кубики, позиция, напряжение и награды."));

        AddRawOrWarning(blocks, $"Полный JSON {AfterlifeSpiritualConflictState.StatePath}", SanitizeCombatConditionsForPlayer(read));
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
                            DescribeSpecialArtEffect(art),
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

    private static ExplorerCommandResult BuildProfileDetail(
        string command,
        IReadOnlyList<JsonObject> profiles,
        string selector)
    {
        var profile = FindProfile(profiles, selector);
        if (profile == null)
            return DetailUnavailable(command, "Профиль недоступен", "не удалось открыть профиль: запись уже недоступна, устарела или не видна текущей душе.");

        var name = ProfileDisplayName(profile, selector);
        var blocks = new List<UiBlock>
        {
            Panel($"Профиль посмертия: {name}",
                Grid(
                    ("Тип", DescribeActorType(GetString(profile, "actorType", "?"))),
                    ("Область", DescribeRealm(GetString(profile, "realm", "?"))),
                    ("Ресурсы", DescribeProfileCurrencies(profile)),
                    ("Прогрессия", DescribeProfileProgression(profile)),
                    ("Духовные искусства", DescribeStandardArts(profile["standardArts"] as JsonObject)),
                    ("Особые искусства", DescribeSpecialArts(profile["specialArts"] as JsonArray)),
                    ("Карты судьбы", DescribeFateCards(profile["fateCards"] as JsonArray, includeHiddenDiagnostics: false)),
                    ("Цели/активность", DescribeProfileAgency(profile)),
                    ("Опасность", DescribeDissipation(profile))))
        };

        var activeQuests = (profile["personalQuests"] as JsonArray)?
            .OfType<JsonObject>()
            .Where(static quest => string.Equals(GetString(quest, "status", ""), "active", StringComparison.OrdinalIgnoreCase))
            .ToList() ?? [];
        if (activeQuests.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Личные квесты",
                Columns = ["Квест", "Состояние", "Кратко"],
                Rows = activeQuests.Select(static quest => new UiTableRow
                {
                    Cells =
                    [
                        GetString(quest, "title", GetString(quest, "questId", "квест")),
                        DescribeAfterlifeRelationshipQuestStatus(GetString(quest, "status", "active")),
                        GetString(quest, "planSummary", GetString(quest, "sceneSummary", "не указано"))
                    ]
                }).ToList()
            });
        }

        return Completed(command, blocks, BuildOverviewAction("afterlife-profiles-overview", "К обзору профилей", "/afterlife_profiles"));
    }

    private static ExplorerCommandResult BuildThreatDetail(
        string command,
        IReadOnlyList<JsonObject> threats,
        string selector)
    {
        var threat = FindThreat(threats, selector);
        if (threat == null)
            return DetailUnavailable(command, "Угроза недоступна", "не удалось открыть угрозу: след уже недоступен, устарел или не виден текущей душе.");

        var name = ThreatDisplayName(threat, selector);
        var blocks = new List<UiBlock>
        {
            Panel($"Угроза посмертия: {name}",
                Grid(
                    ("Область", DescribeRealm(GetString(threat, "realm", "?"))),
                    ("Масштаб", GetNumberOrString(threat, "intensity", "0")),
                    ("Архетип", DescribeThreatArchetype(threat["threatArchetype"] as JsonObject)),
                    ("Активность", DescribeThreatActivity(threat["currentActivity"] as JsonObject)),
                    ("Давление", DescribeThreatImpact(threat["impactProfile"] as JsonObject)),
                    ("Связи", DescribeThreatLinks(threat))))
        };

        var activity = threat["currentActivity"] as JsonObject;
        var description = GetString(activity, "description", "");
        if (!string.IsNullOrWhiteSpace(description))
            blocks.Add(Message("Текущий след угрозы", description));

        return Completed(command, blocks, BuildOverviewAction("afterlife-threats-overview", "К обзору угроз", "/afterlife_threats"));
    }

    private static ExplorerCommandResult BuildChronicleDetail(
        string command,
        IReadOnlyList<JsonObject> chronicles,
        string selector)
    {
        var chronicle = FindChronicle(chronicles, selector);
        if (chronicle == null)
            return DetailUnavailable(command, "Хроника недоступна", "не удалось открыть хронику: запись уже недоступна, устарела или не видна текущей душе.");

        var name = ChronicleDisplayName(chronicle, selector);
        var blocks = new List<UiBlock>
        {
            Panel($"Хроника посмертия: {name}",
                Grid(
                    ("Область", DescribeChronicleScope(chronicle)),
                    ("Последний ход", GetNumberOrString(chronicle, "lastUpdatedTurn", "?")),
                    ("Последнее событие", SafeChronicleText(GetString(chronicle, "lastEventsDescription", "нет"))),
                    ("Участники", DescribeChronicleParticipants(chronicle)),
                    ("Последствия", DescribeChronicleStringArray(chronicle, "persistentConsequences")),
                    ("Открытые нити", DescribeChronicleStringArray(chronicle, "openThreads"))))
        };

        var eventRows = EnumerateChronicleTextArray(chronicle, "eventDescriptions")
            .Select(eventText => new UiTableRow
            {
                Cells =
                [
                    DescribeChronicleEventTurn(chronicle, eventText, preferLastUpdatedTurn: false),
                    eventText
                ]
            })
            .ToList();
        if (eventRows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "События хроники",
                Columns = ["Ход", "Событие"],
                Rows = eventRows
            });
        }

        return Completed(command, blocks, BuildOverviewAction("afterlife-chronicles-overview", "К обзору хроник", "/afterlife_chronicles"));
    }

    private static ExplorerCommandResult BuildInboxNotificationDetail(
        string command,
        IReadOnlyList<AfterlifeNotificationState.NotificationEntry> notifications,
        IReadOnlyDictionary<string, JsonObject> notificationNodes,
        string selector)
    {
        var notification = notifications.FirstOrDefault(item =>
            string.Equals(item.NotificationId, selector, StringComparison.OrdinalIgnoreCase));
        if (notification == null)
            return DetailUnavailable(command, "Уведомление недоступно", "не удалось открыть уведомление: след уже исчез, устарел или пока не виден текущей душе.");

        notificationNodes.TryGetValue(notification.NotificationId, out var raw);
        var blocks = new List<UiBlock>
        {
            Panel("Уведомление загробья",
                Grid(
                    ("Статус", DescribeNotificationStatus(notification.Status)),
                    ("Тип", AfterlifeNotificationState.GetTypeLabel(notification.NotificationType)),
                    ("Источник", DescribeNotificationSource(notification)),
                    ("Ход", notification.CreatedAtTurn > 0 ? notification.CreatedAtTurn.ToString() : "?"),
                    ("Сводка", EmptyFallback(notification.Summary))))
        };

        var details = BuildInboxDetailLines(notification, raw).ToList();
        if (details.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Связанный контекст",
                Columns = ["Контекст", "Что открывает"],
                Rows = details.Select(static detail => new UiTableRow { Cells = [detail.Label, detail.Value] }).ToList()
            });
        }

        return Completed(command, blocks, BuildInboxActions([notification], notificationNodes));
    }

    private static IEnumerable<UiAction> BuildProfileActions(IEnumerable<JsonObject> profiles)
    {
        foreach (var profile in profiles)
        {
            var selector = ProfileSelector(profile);
            if (string.IsNullOrWhiteSpace(selector))
                continue;

            yield return DetailAction(
                "afterlife-profile-detail-" + ToActionIdPart(selector),
                $"Подробно: {ProfileDisplayName(profile, selector)}",
                BuildProfileDetailCommand(selector));
        }
    }

    private static IEnumerable<UiAction> BuildThreatActions(IEnumerable<JsonObject> threats)
    {
        foreach (var threat in threats)
        {
            var selector = ThreatSelector(threat);
            if (string.IsNullOrWhiteSpace(selector))
                continue;

            yield return DetailAction(
                "afterlife-threat-detail-" + ToActionIdPart(selector),
                $"Подробно: {ThreatDisplayName(threat, selector)}",
                BuildThreatDetailCommand(selector));
        }
    }

    private static IEnumerable<UiAction> BuildChronicleActions(IEnumerable<JsonObject> chronicles)
    {
        foreach (var chronicle in chronicles)
        {
            var selector = ChronicleSelector(chronicle);
            if (string.IsNullOrWhiteSpace(selector))
                continue;

            yield return DetailAction(
                "afterlife-chronicle-detail-" + ToActionIdPart(selector),
                $"Подробно: {ChronicleDisplayName(chronicle, selector)}",
                BuildChronicleDetailCommand(selector));
        }
    }

    private static IEnumerable<UiAction> BuildInboxActions(
        IReadOnlyList<AfterlifeNotificationState.NotificationEntry> notifications,
        IReadOnlyDictionary<string, JsonObject> notificationNodes)
    {
        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var notification in notifications)
        {
            notificationNodes.TryGetValue(notification.NotificationId, out var raw);
            foreach (var action in BuildInboxActions(notification, raw))
            {
                if (added.Add(action.Id))
                    yield return action;
            }
        }
    }

    private static IEnumerable<UiAction> BuildInboxActions(
        AfterlifeNotificationState.NotificationEntry notification,
        JsonObject? raw)
    {
        var notificationId = notification.NotificationId;
        if (string.IsNullOrWhiteSpace(notificationId))
            yield break;

        var notificationPart = ToActionIdPart(notificationId);
        var detailLabel = FirstNonEmpty(DescribeNotificationSource(notification), notification.Summary, "уведомление");
        yield return DetailAction(
            "afterlife-inbox-detail-" + notificationPart,
            $"Подробно: {detailLabel}",
            BuildInboxNotificationDetailCommand(notificationId));

        if (!string.IsNullOrWhiteSpace(notification.GuardianId))
        {
            yield return DetailAction(
                $"afterlife-inbox-guardian-{notificationPart}-{ToActionIdPart(notification.GuardianId)}",
                $"Открыть Хранителя: {EmptyFallback(notification.GuardianName, "Хранитель")}",
                "/guardians хранитель " + FormatCommandArgument(notification.GuardianId));
        }

        var profileActorId = FirstNonEmpty(GetString(raw, "profileActorId", ""), notification.GuardianId, notification.ResidentId);
        if (!string.IsNullOrWhiteSpace(profileActorId))
        {
            var profileLabel = FirstNonEmpty(GetString(raw, "profileName", ""), notification.GuardianName, notification.ResidentName, "профиль");
            yield return DetailAction(
                $"afterlife-inbox-profile-{notificationPart}-{ToActionIdPart(profileActorId)}",
                $"Профиль: {profileLabel}",
                BuildProfileDetailCommand(profileActorId));
        }

        var threatId = GetString(raw, "threatId", "");
        if (!string.IsNullOrWhiteSpace(threatId))
        {
            yield return DetailAction(
                $"afterlife-inbox-threat-{notificationPart}-{ToActionIdPart(threatId)}",
                $"Угроза: {FirstNonEmpty(GetString(raw, "threatName", ""), threatId)}",
                BuildThreatDetailCommand(threatId));
        }

        var chronicleId = GetString(raw, "chronicleId", "");
        if (!string.IsNullOrWhiteSpace(chronicleId))
        {
            yield return DetailAction(
                $"afterlife-inbox-chronicle-{notificationPart}-{ToActionIdPart(chronicleId)}",
                $"Хроника: {FirstNonEmpty(GetString(raw, "chronicleTitle", ""), chronicleId)}",
                BuildChronicleDetailCommand(chronicleId));
        }

        if (!string.IsNullOrWhiteSpace(notification.ArchiveId))
        {
            yield return DetailAction(
                $"afterlife-inbox-archive-{notificationPart}-{ToActionIdPart(notification.ArchiveId)}",
                $"Архив: {EmptyFallback(notification.ArchiveTitle, "запись Архива")}",
                "/afterlife_archive запись " + FormatCommandArgument(notification.ArchiveId));
        }

        if (!string.IsNullOrWhiteSpace(notification.TargetProjectId) && !string.IsNullOrWhiteSpace(notification.GuardianId))
        {
            var projectSelector = $"{notification.GuardianId}::{notification.TargetProjectId}";
            yield return DetailAction(
                $"afterlife-inbox-project-{notificationPart}-{ToActionIdPart(notification.GuardianId)}-{ToActionIdPart(notification.TargetProjectId)}",
                $"Проект: {EmptyFallback(notification.TargetProjectName, "проект Хранителя")}",
                "/guardian_projects проект " + FormatCommandArgument(projectSelector));
        }

        if (!string.IsNullOrWhiteSpace(notification.ResidentId))
        {
            yield return DetailAction(
                $"afterlife-inbox-resident-{notificationPart}-{ToActionIdPart(notification.ResidentId)}",
                $"Резидент: {EmptyFallback(notification.ResidentName, "обитатель Обители")}",
                "/resident_interaction " + FormatCommandArgument(notification.ResidentId));
        }

        if (notification.NotificationType.StartsWith("guardian_trade_", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(notification.GuardianId))
        {
            yield return DetailAction(
                $"afterlife-inbox-trade-{notificationPart}-{ToActionIdPart(notification.GuardianId)}",
                $"Торговля: {EmptyFallback(notification.GuardianName, "Хранитель")}",
                "/guardian_trade " + FormatCommandArgument(notification.GuardianId));
        }

        var shiningFactionId = GetString(raw, "factionId", "");
        if (notification.NotificationType.StartsWith("shining_trade_", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(shiningFactionId))
        {
            yield return DetailAction(
                $"afterlife-inbox-shining-trade-{notificationPart}-{ToActionIdPart(shiningFactionId)}",
                $"Сияющая торговля: {FirstNonEmpty(GetString(raw, "factionName", ""), "фракция")}",
                "/shining_trade " + FormatCommandArgument(shiningFactionId));
        }

        if (notification.NotificationType.StartsWith("shining_", StringComparison.OrdinalIgnoreCase))
        {
            yield return DetailAction(
                "afterlife-inbox-shining-politics-" + notificationPart,
                "Открыть Сияющую Обитель",
                "/shining_politics");
        }
    }

    private static IEnumerable<(string Label, string Value)> BuildInboxDetailLines(
        AfterlifeNotificationState.NotificationEntry notification,
        JsonObject? raw)
    {
        if (!string.IsNullOrWhiteSpace(notification.GuardianName))
            yield return ("Хранитель", notification.GuardianName);
        if (!string.IsNullOrWhiteSpace(notification.ResidentName))
            yield return ("Резидент", notification.ResidentName);
        if (!string.IsNullOrWhiteSpace(notification.ArchiveTitle))
            yield return ("Архив", notification.ArchiveTitle);
        if (!string.IsNullOrWhiteSpace(notification.TargetProjectName))
            yield return ("Проект", notification.TargetProjectName);
        if (!string.IsNullOrWhiteSpace(GetString(raw, "threatName", "")))
            yield return ("Угроза", GetString(raw, "threatName", ""));
        if (!string.IsNullOrWhiteSpace(GetString(raw, "chronicleTitle", "")))
            yield return ("Хроника", GetString(raw, "chronicleTitle", ""));
        if (notification.NotificationType.StartsWith("shining_", StringComparison.OrdinalIgnoreCase))
            yield return ("Сияющая Обитель", "политика и решения Обители");
    }

    private static IEnumerable<UiAction> BuildOverviewAction(string id, string label, string command)
    {
        yield return DetailAction(id, label, command);
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

    private static UiTableBlock? BuildVisibleCombatConditionsTable(JsonArray? combatConditions)
    {
        if (combatConditions == null)
            return null;

        var rows = combatConditions
            .OfType<JsonObject>()
            .Where(AfterlifeCombatConditionPlayerAuditSanitizer.IsVisibleToPlayer)
            .Where(static condition => string.Equals(GetString(condition, "status", "active"), "active", StringComparison.OrdinalIgnoreCase))
            .Select(condition => new UiTableRow
            {
                Cells =
                [
                    GetString(condition, "displayName", GetString(condition, "name", GetString(condition, "conditionId", "Без названия"))),
                    GetString(condition, "kind", "?"),
                    DescribeCombatConditionTarget(condition),
                    DescribeCombatConditionSource(condition["source"] as JsonObject),
                    DescribeStringArray(condition["affectedOperations"] as JsonArray),
                    DescribeCombatConditionDuration(condition["duration"] as JsonObject),
                    DescribeStringArray(condition["counterplay"] as JsonArray),
                    GetString(condition, "summary", "")
                ]
            })
            .ToList();

        return rows.Count == 0
            ? null
            : new UiTableBlock
            {
                Title = "Боевые условия (combatConditions)",
                Columns = ["Название", "Вид", "Цель", "Источник", "Действия", "Срок", "Ответ", "Итог"],
                Rows = rows
            };
    }

    private static string DescribeCombatConditionTarget(JsonObject condition)
    {
        if (condition["target"] is JsonObject target)
        {
            var targetSideValue = GetString(target, "side", GetString(target, "targetSide", "?"));
            var targetActorValue = GetString(target, "displayName", GetString(target, "actorId", GetString(target, "actorRef", "")));
            return string.IsNullOrWhiteSpace(targetActorValue)
                ? targetSideValue
                : $"{targetSideValue}:{targetActorValue}";
        }

        var targetSide = GetString(condition, "targetSide", "?");
        var targetActor = GetString(condition, "targetActorRef", GetString(condition, "targetActorId", ""));
        return string.IsNullOrWhiteSpace(targetActor)
            ? targetSide
            : $"{targetSide}:{targetActor}";
    }

    private static string DescribeCombatConditionSource(JsonObject? source)
    {
        if (source == null)
            return "не указан";

        var type = GetString(source, "type", GetString(source, "sourceType", ""));
        var actorId = GetString(source, "actorId", GetString(source, "sourceId", ""));
        var displayName = GetString(source, "displayName", "");
        var parts = new[] { type, actorId, displayName }
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return parts.Length == 0 ? "не указан" : string.Join(":", parts);
    }

    private static string DescribeCombatConditionDuration(JsonObject? duration)
    {
        if (duration == null)
            return "не указано";

        var parts = new List<string>();
        var type = GetString(duration, "type", "");
        if (!string.IsNullOrWhiteSpace(type))
            parts.Add(type);
        if (duration.ContainsKey("remainingUses"))
            parts.Add($"remainingUses={GetNumberOrString(duration, "remainingUses", "?")}");
        if (duration.ContainsKey("expiresAtTurn"))
            parts.Add($"expiresAtTurn={GetNumberOrString(duration, "expiresAtTurn", "?")}");
        if (duration.ContainsKey("until"))
            parts.Add($"until={GetString(duration, "until", "?")}");
        return parts.Count == 0 ? "не указано" : string.Join("; ", parts);
    }

    private static string DescribeStringArray(JsonArray? array)
    {
        if (array == null)
            return "нет";

        var values = array
            .Select(GetNodeString)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return values.Length == 0 ? "нет" : string.Join("; ", values);
    }

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
        {
            var name = GetString(art, "displayName", GetString(art, "artId", "?"));
            var tier = GetNumberOrString(art, "tier", "0");
            var effect = DescribeSpecialArtEffect(art);
            return string.IsNullOrWhiteSpace(effect)
                ? $"{name} {tier}"
                : $"{name} {tier}: {effect}";
        }));
    }

    private static string DescribeSpecialArtEffect(JsonObject art)
    {
        var summary = GetString(art, "effectSummary", "эффект не описан");
        var combatEffect = FormatSpecialArtCombatEffect(art["combatEffect"] as JsonObject);
        return string.IsNullOrWhiteSpace(combatEffect)
            ? summary
            : $"{summary}; {combatEffect}";
    }

    private static string? FormatSpecialArtCombatEffect(JsonObject? combatEffect)
    {
        if (combatEffect == null)
            return null;

        var parts = new List<string>();
        var summary = GetString(combatEffect, "summary", "");
        var trigger = GetString(combatEffect, "trigger", "");
        var payoff = GetString(combatEffect, "allowedPayoff", "");
        var limit = GetString(combatEffect, "limit", "");

        if (!string.IsNullOrWhiteSpace(summary))
            parts.Add($"Боевой эффект: {summary}");
        if (!string.IsNullOrWhiteSpace(trigger))
            parts.Add($"срабатывает: {trigger}");
        if (!string.IsNullOrWhiteSpace(payoff))
            parts.Add($"выигрыш: {payoff}");
        if (!string.IsNullOrWhiteSpace(limit))
            parts.Add($"предел: {limit}");

        return parts.Count == 0 ? null : string.Join("; ", parts);
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

    private static UiTableBlock? BuildProfileRelationshipsTable(IEnumerable<JsonObject> profiles, bool includeHiddenDiagnostics)
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

    private static UiTableBlock? BuildProfileMasksTable(IEnumerable<JsonObject> profiles, bool includeHiddenDiagnostics)
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
            Columns = ["Хроника", "Область", "Ход", "Последнее событие", "Участники", "Последствия", "Открытые нити", "Подробно"],
            Rows = chronicles.Select(chronicle =>
            {
                var selector = ChronicleSelector(chronicle);
                return new UiTableRow
                {
                    Cells =
                    [
                        SafeChronicleText(GetString(chronicle, "displayName", GetString(chronicle, "chronicleId", "Без названия"))),
                        DescribeChronicleScope(chronicle),
                        GetNumberOrString(chronicle, "lastUpdatedTurn", "?"),
                        SafeChronicleText(GetString(chronicle, "lastEventsDescription", "нет")),
                        DescribeChronicleParticipants(chronicle),
                        DescribeChronicleStringArray(chronicle, "persistentConsequences"),
                        DescribeChronicleStringArray(chronicle, "openThreads"),
                        string.IsNullOrWhiteSpace(selector) ? "не указано" : BuildChronicleDetailCommand(selector)
                    ]
                };
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

    private static bool IsHiddenPlayerFacingVisibility(string? visibility) =>
        IsChronicleHiddenVisibility(visibility) ||
        string.Equals(visibility?.Trim(), "concealed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(visibility?.Trim(), "spoiler", StringComparison.OrdinalIgnoreCase);

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

    private static int CountProfilesWithDissipation(IEnumerable<JsonObject> profiles) =>
        profiles.Count(profile => GetInt(profile["soulDissipationTier"]) > 0);

    private static int CountNestedArray(IEnumerable<JsonObject> owners, string propertyName) =>
        owners.Sum(owner => CountArray(owner, propertyName));

    private static int CountUnlockedFateCards(IEnumerable<JsonObject> profiles, bool includeHiddenDiagnostics) =>
        profiles
            .SelectMany(profile => (profile["fateCards"] as JsonArray)?.OfType<JsonObject>() ?? [])
            .Where(card => includeHiddenDiagnostics || IsFateCardPlayerVisible(card))
            .Count(card => string.Equals(GetString(card, "status", ""), "unlocked", StringComparison.OrdinalIgnoreCase));

    private static int CountActiveProfileQuests(IEnumerable<JsonObject> profiles) =>
        profiles
            .SelectMany(profile => (profile["personalQuests"] as JsonArray)?.OfType<JsonObject>() ?? [])
            .Count(quest => string.Equals(GetString(quest, "status", ""), "active", StringComparison.OrdinalIgnoreCase));

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

    private static JsonReadResult SanitizeCombatConditionsForPlayer(JsonReadResult read)
    {
        if (read.Node == null)
            return read;

        return new JsonReadResult(
            read.FileExists,
            AfterlifeCombatConditionPlayerAuditSanitizer.Sanitize(read.Node),
            read.Error);
    }

    private static bool IsFateCardPlayerVisible(JsonObject card)
    {
        var secret = card["isSecret"] is JsonValue secretValue &&
                     secretValue.TryGetValue<bool>(out var secretBool) &&
                     secretBool;
        var status = GetString(card, "status", "");
        return !secret && !string.Equals(status, "hidden", StringComparison.OrdinalIgnoreCase);
    }

    private static CommandRequest? ParseCommandRequest(string command)
    {
        var normalized = command.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var split = normalized.IndexOfAny([' ', '\t', '\r', '\n']);
        if (split < 0)
            return new CommandRequest(normalized, normalized, string.Empty);

        var token = normalized[..split].Trim();
        var arguments = normalized[split..].Trim();
        return string.IsNullOrWhiteSpace(token)
            ? null
            : new CommandRequest(normalized, token, arguments);
    }

    private static DetailRequest ParseDetailRequest(string arguments, params string[] detailTokens)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return new DetailRequest(string.Empty, string.Empty);

        var normalized = arguments.Trim();
        var split = normalized.IndexOfAny([' ', '\t', '\r', '\n']);
        var first = split < 0 ? normalized : normalized[..split].Trim();
        var rest = split < 0 ? string.Empty : normalized[split..].Trim();

        if (detailTokens.Any(token => string.Equals(token, first, StringComparison.OrdinalIgnoreCase)))
            return new DetailRequest(first, UnquoteSelector(rest));

        return new DetailRequest(string.Empty, UnquoteSelector(normalized));
    }

    private static string UnquoteSelector(string value) => value.Trim().Trim('"');

    private static bool IsProfileVisibleToPlayer(JsonObject profile)
    {
        if (IsFalseFlag(profile["isPlayerVisible"]) ||
            IsFalseFlag(profile["playerVisible"]) ||
            IsFalseFlag(profile["visibleToPlayer"]) ||
            IsFalseFlag(profile["visibleForPlayer"]))
        {
            return false;
        }

        if (IsTrueFlag(profile["isHidden"]) ||
            IsTrueFlag(profile["hidden"]) ||
            IsTrueFlag(profile["isSecret"]) ||
            IsTrueFlag(profile["secret"]) ||
            IsTrueFlag(profile["gmOnly"]) ||
            IsTrueFlag(profile["isGmOnly"]) ||
            IsTrueFlag(profile["internal"]) ||
            IsTrueFlag(profile["isInternal"]))
        {
            return false;
        }

        var visibility = GetString(profile, "visibility", "");
        return !IsHiddenPlayerFacingVisibility(visibility);
    }

    private static string ProfileSelector(JsonObject profile) =>
        FirstNonEmpty(
            GetString(profile, "actorId", ""),
            GetString(profile, "actorRef", ""),
            GetString(profile, "profileId", ""),
            GetString(profile, "id", ""),
            GetString(profile, "displayName", ""));

    private static string ProfileDisplayName(JsonObject profile, string fallback) =>
        FirstNonEmpty(GetString(profile, "displayName", ""), ProfileSelector(profile), fallback, "сущность");

    private static JsonObject? FindProfile(IEnumerable<JsonObject> profiles, string selector)
    {
        var normalized = NormalizeSelector(selector);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        return profiles.FirstOrDefault(profile =>
            SelectorMatches(
                normalized,
                ProfileSelector(profile),
                GetString(profile, "actorId", ""),
                GetString(profile, "actorRef", ""),
                GetString(profile, "profileId", ""),
                GetString(profile, "id", ""),
                GetString(profile, "displayName", "")));
    }

    private static string ThreatSelector(JsonObject threat) =>
        FirstNonEmpty(GetString(threat, "threatId", ""), GetString(threat, "id", ""), GetString(threat, "displayName", ""));

    private static string ThreatDisplayName(JsonObject threat, string fallback) =>
        FirstNonEmpty(GetString(threat, "displayName", ""), ThreatSelector(threat), fallback, "угроза");

    private static JsonObject? FindThreat(IEnumerable<JsonObject> threats, string selector)
    {
        var normalized = NormalizeSelector(selector);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        return threats.FirstOrDefault(threat =>
            SelectorMatches(
                normalized,
                ThreatSelector(threat),
                GetString(threat, "threatId", ""),
                GetString(threat, "id", ""),
                GetString(threat, "displayName", "")));
    }

    private static string ChronicleSelector(JsonObject chronicle) =>
        FirstNonEmpty(GetString(chronicle, "chronicleId", ""), GetString(chronicle, "eventId", ""), GetString(chronicle, "id", ""), GetString(chronicle, "displayName", ""));

    private static string ChronicleDisplayName(JsonObject chronicle, string fallback) =>
        FirstNonEmpty(SafeChronicleText(GetString(chronicle, "displayName", "")), ChronicleSelector(chronicle), fallback, "хроника");

    private static JsonObject? FindChronicle(IEnumerable<JsonObject> chronicles, string selector)
    {
        var normalized = NormalizeSelector(selector);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        return chronicles.FirstOrDefault(chronicle =>
            SelectorMatches(
                normalized,
                ChronicleSelector(chronicle),
                GetString(chronicle, "chronicleId", ""),
                GetString(chronicle, "eventId", ""),
                GetString(chronicle, "id", ""),
                GetString(chronicle, "displayName", "")));
    }

    private static IReadOnlyDictionary<string, JsonObject> BuildNotificationNodeMap(JsonNode? root)
    {
        if (root?["notifications"] is not JsonArray notifications)
            return new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);

        return notifications
            .OfType<JsonObject>()
            .Select(notification => (Id: GetString(notification, "notificationId", ""), Node: notification))
            .Where(static item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(static item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First().Node, StringComparer.OrdinalIgnoreCase);
    }

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
        if (notification.NotificationType.StartsWith("shining_", StringComparison.OrdinalIgnoreCase))
            return "Сияющая Обитель";
        return "не указан";
    }

    private static string BuildProfileDetailCommand(string selector) =>
        "/afterlife_profiles профиль " + FormatCommandArgument(selector);

    private static string BuildThreatDetailCommand(string selector) =>
        "/afterlife_threats угроза " + FormatCommandArgument(selector);

    private static string BuildChronicleDetailCommand(string selector) =>
        "/afterlife_chronicles хроника " + FormatCommandArgument(selector);

    private static string BuildInboxNotificationDetailCommand(string notificationId) =>
        "/afterlife_inbox уведомление " + FormatCommandArgument(notificationId);

    private static string FormatCommandArgument(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            return trimmed;
        return trimmed.Any(char.IsWhiteSpace)
            ? "\"" + trimmed.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
            : trimmed;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static bool SelectorMatches(string normalizedSelector, params string[] candidates) =>
        candidates.Any(candidate => string.Equals(normalizedSelector, NormalizeSelector(candidate), StringComparison.OrdinalIgnoreCase));

    private static string NormalizeSelector(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Trim('"');

    private static string ToActionIdPart(string value)
    {
        var chars = new List<char>(value.Length);
        var lastWasSeparator = false;
        foreach (var ch in value.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-')
            {
                chars.Add(ch);
                lastWasSeparator = false;
                continue;
            }

            if (!lastWasSeparator)
            {
                chars.Add('-');
                lastWasSeparator = true;
            }
        }

        return new string(chars.ToArray()).Trim('-');
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

        if (!includeRawDiagnostics)
        {
            blocks.Add(Message(
                read.FileExists ? title : "Данные ещё не открыты",
                read.FileExists
                    ? $"{title}: не удалось прочитать видимое состояние. Откройте сводку позже или попросите ГМ обновить состояние."
                    : $"{title}: запись ещё не открыта."));
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

    private static ExplorerCommandResult Completed(
        string command,
        IEnumerable<UiBlock> blocks,
        IEnumerable<UiAction> actions) =>
        Result(command, CommandExecutionState.Completed, blocks, actions);

    private static ExplorerCommandResult Result(
        string command,
        CommandExecutionState state,
        IEnumerable<UiBlock> blocks,
        IEnumerable<UiAction>? actions = null,
        IEnumerable<UiPrompt>? prompts = null) =>
        new()
        {
            Command = command,
            State = state,
            Blocks = blocks.ToList(),
            Actions = actions?.ToList() ?? [],
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

    private static UiAction DetailAction(string id, string label, string command) =>
        new()
        {
            Id = id,
            Label = label,
            Command = command,
            Style = UiActionStyle.Secondary,
            RequiresConfirmation = false
        };

    private static ExplorerCommandResult DetailUnavailable(string command, string title, string message) =>
        Completed(command, Message(title, message));

    private sealed record CommandRequest(string Command, string CommandToken, string Arguments);

    private sealed record DetailRequest(string Token, string Selector);

    private sealed record JsonReadResult(bool FileExists, JsonNode? Node, string? Error);
}
