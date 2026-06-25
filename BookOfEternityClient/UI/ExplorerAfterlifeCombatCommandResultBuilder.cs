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
            CommandKind.Conflict => await BuildConflict(request, fs, includeRawDiagnostics),
            CommandKind.CombatLog => await BuildCombatLog(request, fs, includeRawDiagnostics),
            CommandKind.Help => BuildHelp(request.Command),
            CommandKind.Arts => await BuildArts(request, fs, includeRawDiagnostics),
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
        if (includeRawDiagnostics)
            return BuildProfilesDiagnosticProjection(request.Command, read, visibleProfiles, hiddenCount);

        var blocks = new List<UiBlock>
        {
            BuildAfterlifeProfilesDossier(visibleProfiles, allProfiles.Count, hiddenCount)
        };

        if (visibleProfiles.Count == 0)
        {
            blocks.Add(Message(
                "Профили не найдены",
                allProfiles.Count > 0
                    ? "Известные записи сейчас скрыты от текущей души."
                    : "Когда ГМ откроет значимые сущности посмертия, они появятся здесь."));
        }

        AddRawOrWarning(
            blocks,
            "Профили посмертия",
            read,
            includeRawDiagnostics: false);
        return Completed(request.Command, blocks, BuildProfileActions(visibleProfiles));
    }

    private static ExplorerCommandResult BuildProfilesDiagnosticProjection(
        string command,
        JsonReadResult read,
        IReadOnlyList<JsonObject> visibleProfiles,
        int hiddenCount)
    {
        var blocks = new List<UiBlock>
        {
            Panel("Профили сущностей посмертия",
                Grid(
                    ("Профилей", visibleProfiles.Count.ToString()),
                    ("Скрытых профилей не показано", hiddenCount.ToString()),
                    ("Опасных развеивателей души", CountProfilesWithDissipation(visibleProfiles).ToString()),
                    ("Особых духовных искусств", CountNestedArray(visibleProfiles, "specialArts").ToString()),
                    ("Кастомных состояний", CountNestedArray(visibleProfiles, AfterlifeEntityProfileState.CustomStatesProperty).ToString()),
                    ("Открытых карт судьбы", CountUnlockedFateCards(visibleProfiles, includeHiddenDiagnostics: true).ToString()),
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
                                DescribeFateCards(profile["fateCards"] as JsonArray, includeHiddenDiagnostics: true),
                                DescribeProfileAgency(profile),
                                DescribeDissipation(profile),
                                string.IsNullOrWhiteSpace(selector) ? "не указано" : BuildProfileDetailCommand(selector)
                            ]
                        };
                    })
                    .ToList()
            });

            var relationshipsTable = BuildProfileRelationshipsTable(visibleProfiles, includeHiddenDiagnostics: true);
            if (relationshipsTable != null)
                blocks.Add(relationshipsTable);

            var masksTable = BuildProfileMasksTable(visibleProfiles, includeHiddenDiagnostics: true);
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
                visibleProfiles.Count > 0
                    ? "Известные записи сейчас скрыты от текущей души."
                    : "Когда ГМ откроет значимые сущности посмертия, они появятся здесь."));
        }

        AddRawOrWarning(
            blocks,
            $"Полный JSON {AfterlifeEntityProfileState.StatePath}",
            read,
            includeRawDiagnostics: true);
        return Completed(command, blocks, BuildProfileActions(visibleProfiles));
    }

    private static UiEntityDossierBlock BuildAfterlifeProfilesDossier(
        IReadOnlyList<JsonObject> visibleProfiles,
        int totalProfiles,
        int hiddenProfiles)
    {
        var relationships = BuildProfileRelationshipCards(visibleProfiles).ToList();
        var masks = BuildProfileMaskCards(visibleProfiles).ToList();
        var progression = BuildProfileProgressionCards(visibleProfiles).ToList();
        var summary = visibleProfiles.Count > 0
            ? "Открытые душе профили посмертия: сущности, их намерения, связи, маски и духовная прогрессия."
            : "Сейчас нет профилей посмертия, раскрытых текущей душе.";

        var sections = new List<UiEntityDossierSection>
        {
            new()
            {
                Id = "visible-afterlife-profiles",
                Title = "Открытые сущности",
                Summary = "Карточки показывают тип сущности, область, ресурсы, цели, активность и опасность.",
                Icon = "user-round",
                Presentation = "cards",
                CollectionLabel = $"{visibleProfiles.Count} профилей",
                Collapsible = visibleProfiles.Count > 4,
                InitiallyExpanded = true,
                Cards = visibleProfiles.Select(BuildAfterlifeProfileCard).ToList()
            }
        };

        if (relationships.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "afterlife-profile-relationships",
                Title = "Отношения",
                Summary = "Связи, текущий уровень доверия или конфликта и ближайшее условие раскрытия.",
                Icon = "link",
                Presentation = "cards",
                CollectionLabel = $"{relationships.Count} связей",
                Collapsible = relationships.Count > 4,
                InitiallyExpanded = true,
                Cards = relationships
            });
        }

        if (masks.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "afterlife-profile-masks",
                Title = "Маски",
                Summary = "Активные и уже раскрытые образы сущностей без скрытых GM-деталей.",
                Icon = "venetian-mask",
                Presentation = "cards",
                CollectionLabel = $"{masks.Count} масок",
                Collapsible = masks.Count > 4,
                InitiallyExpanded = true,
                Cards = masks
            });
        }

        if (progression.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "afterlife-profile-progression",
                Title = "Стратегии развития",
                Summary = "Как сущности собираются усиливать духовные искусства и ресурсы.",
                Icon = "trending-up",
                Presentation = "cards",
                CollectionLabel = $"{progression.Count} стратегий",
                Collapsible = progression.Count > 4,
                InitiallyExpanded = true,
                Cards = progression
            });
        }

        return new UiEntityDossierBlock
        {
            EntityType = "afterlife-profiles",
            Title = "Профили сущностей посмертия",
            Subtitle = "Сущности, связи и скрытые роли посмертия",
            Summary = summary,
            Facts =
            [
                new UiEntityFact { Label = "Показано профилей", Value = visibleProfiles.Count.ToString() },
                new UiEntityFact { Label = "Скрытых профилей", Value = hiddenProfiles.ToString() },
                new UiEntityFact { Label = "Всего записей", Value = totalProfiles.ToString() },
                new UiEntityFact { Label = "Опасных развеивателей души", Value = CountProfilesWithDissipation(visibleProfiles).ToString() },
                new UiEntityFact { Label = "Открытых карт судьбы", Value = CountUnlockedFateCards(visibleProfiles, includeHiddenDiagnostics: false).ToString() },
                new UiEntityFact { Label = "Активных личных квестов", Value = CountActiveProfileQuests(visibleProfiles).ToString() }
            ],
            Sections = sections
        };
    }

    private static UiEntityCard BuildAfterlifeProfileCard(JsonObject profile)
    {
        var selector = ProfileSelector(profile);
        var name = ProfileDisplayName(profile, selector);
        var goals = profile["goals"] as JsonObject;
        var currentActivity = profile["currentActivity"] as JsonObject;
        var progression = profile["progression"] as JsonObject;
        var enlightenment = progression?["enlightenment"] as JsonObject;
        var radiance = progression?["radiance"] as JsonObject;
        var summary = FirstSafePlayerText(
            GetString(goals, "shortTermGoal", ""),
            GetString(currentActivity, "summary", ""),
            GetString(profile, "locationName", ""),
            DescribeActorType(GetString(profile, "actorType", "сущность")));

        return new UiEntityCard
        {
            Title = name,
            Subtitle = $"{DescribeActorType(GetString(profile, "actorType", "?"))} - {DescribeRealm(GetString(profile, "realm", "?"))}",
            Icon = "user-round",
            Summary = summary,
            Badges = BuildProfileBadges(profile),
            Facts =
            [
                new UiEntityFact { Label = "Тип", Value = DescribeActorType(GetString(profile, "actorType", "?")) },
                new UiEntityFact { Label = "Область", Value = DescribeRealm(GetString(profile, "realm", "?")) },
                new UiEntityFact { Label = "Место", Value = SafePlayerText(GetString(profile, "locationName", ""), "не указано") },
                new UiEntityFact { Label = "Чернильные Перья", Value = GetNumberOrString(profile["currencies"] as JsonObject, "inkFeathers", "0") },
                new UiEntityFact { Label = "Искры Света", Value = GetNumberOrString(profile["currencies"] as JsonObject, "lightSparks", "0") },
                new UiEntityFact { Label = "Просветление", Value = $"{GetNumberOrString(enlightenment, "tier", "0")} ступень, опыт {GetNumberOrString(enlightenment, "experience", "0")}" },
                new UiEntityFact { Label = "Сияние", Value = $"{GetNumberOrString(radiance, "tier", "0")} ступень, опыт {GetNumberOrString(radiance, "experience", "0")}" },
                new UiEntityFact { Label = "Обычных искусств", Value = CountObjectProperties(profile["standardArts"] as JsonObject).ToString() },
                new UiEntityFact { Label = "Особых искусств", Value = CountArray(profile, "specialArts").ToString() },
                new UiEntityFact { Label = "Открытых карт судьбы", Value = CountVisibleFateCards(profile).ToString() }
            ],
            Hints = BuildProfileCardHints(profile),
            Cards = BuildProfileNestedCards(profile),
            PrimaryAction = string.IsNullOrWhiteSpace(selector)
                ? null
                : DetailAction(
                    "afterlife-profile-detail-" + ToActionIdPart(selector),
                    "Открыть профиль",
                    BuildProfileDetailCommand(selector))
        };
    }

    private static UiEntityDossierBlock BuildAfterlifeProfileDetailDossier(JsonObject profile, string name)
    {
        var card = BuildAfterlifeProfileCard(profile);
        var questCards = BuildProfileQuestCards(profile);
        var sections = new List<UiEntityDossierSection>();
        if (questCards.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "afterlife-profile-personal-quests",
                Title = "Личные квесты",
                Summary = "Активные личные сюжетные задачи этой сущности.",
                Icon = "scroll-text",
                Presentation = "cards",
                CollectionLabel = $"{questCards.Count} квестов",
                Collapsible = questCards.Count > 4,
                InitiallyExpanded = true,
                Cards = questCards
            });
        }

        return new UiEntityDossierBlock
        {
            EntityType = "afterlife-profile-detail",
            Title = $"Профиль посмертия: {name}",
            Subtitle = card.Subtitle,
            Summary = card.Summary,
            Badges = card.Badges,
            Facts = card.Facts,
            Hints = card.Hints,
            Cards = card.Cards,
            Sections = sections
        };
    }

    private static List<UiEntityCard> BuildProfileQuestCards(JsonObject profile) =>
        (profile["personalQuests"] as JsonArray)?
            .OfType<JsonObject>()
            .Where(static quest => string.Equals(GetString(quest, "status", ""), "active", StringComparison.OrdinalIgnoreCase))
            .Select(static quest =>
            {
                var title = GetString(quest, "title", GetString(quest, "questId", "личный квест"));
                var summary = SafePlayerText(GetString(quest, "planSummary", GetString(quest, "sceneSummary", "")), "подробности квеста не указаны");
                return new UiEntityCard
                {
                    Title = title,
                    Subtitle = DescribeAfterlifeRelationshipQuestStatus(GetString(quest, "status", "active")),
                    Icon = "scroll-text",
                    Summary = summary,
                    Facts =
                    [
                        new UiEntityFact { Label = "Состояние", Value = DescribeAfterlifeRelationshipQuestStatus(GetString(quest, "status", "active")) },
                        new UiEntityFact { Label = "Кратко", Value = summary }
                    ],
                    Hints = BuildProfileQuestHints(quest)
                };
            })
            .ToList() ?? [];

    private static List<UiEntityHint> BuildProfileQuestHints(JsonObject quest)
    {
        var hints = new List<UiEntityHint>();
        AddProfileHint(hints, "Сцена", GetString(quest, "sceneSummary", ""), UiTone.Default);
        AddProfileHint(hints, "Условие успеха", GetString(quest, "successCondition", ""), UiTone.Accent);
        return hints;
    }

    private static List<UiEntityBadge> BuildProfileBadges(JsonObject profile)
    {
        var badges = new List<UiEntityBadge>();
        var tier = GetInt(profile["soulDissipationTier"]);
        if (tier > 0)
            badges.Add(new UiEntityBadge { Label = $"развеивание души {tier}", Tone = UiTone.Warning, Icon = "alert-triangle" });

        var activeQuests = CountActivePersonalQuests(profile);
        if (activeQuests > 0)
            badges.Add(new UiEntityBadge { Label = $"квестов {activeQuests}", Tone = UiTone.Accent, Icon = "scroll-text" });

        return badges;
    }

    private static List<UiEntityHint> BuildProfileCardHints(JsonObject profile)
    {
        var hints = new List<UiEntityHint>();
        var goals = profile["goals"] as JsonObject;
        AddProfileHint(hints, "Ближайшая цель", GetString(goals, "shortTermGoal", ""), UiTone.Default);
        AddProfileHint(hints, "Долгая цель", GetString(goals, "longTermGoal", ""), UiTone.Default);
        AddProfileHint(hints, "План", GetString(goals, "plan", ""), UiTone.Default);

        var currentActivity = profile["currentActivity"] as JsonObject;
        AddProfileHint(hints, "Сейчас", GetString(currentActivity, "summary", ""), UiTone.Accent);

        var activeQuests = (profile["personalQuests"] as JsonArray)?
            .OfType<JsonObject>()
            .Where(static quest => string.Equals(GetString(quest, "status", ""), "active", StringComparison.OrdinalIgnoreCase))
            .Select(static quest =>
            {
                var title = GetString(quest, "title", GetString(quest, "questId", "личный квест"));
                var plan = GetString(quest, "planSummary", "");
                return string.IsNullOrWhiteSpace(plan) ? title : $"{title}: {plan}";
            })
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray() ?? [];
        if (activeQuests.Length > 0)
            hints.Add(new UiEntityHint { Title = "Личные квесты", Text = string.Join(Environment.NewLine, activeQuests), Tone = UiTone.Accent });

        return hints;
    }

    private static void AddProfileHint(List<UiEntityHint> hints, string title, string value, UiTone tone)
    {
        var safe = SafePlayerText(value, string.Empty);
        if (!string.IsNullOrWhiteSpace(safe))
            hints.Add(new UiEntityHint { Title = title, Text = safe, Tone = tone });
    }

    private static List<UiEntityCard> BuildProfileNestedCards(JsonObject profile)
    {
        var cards = new List<UiEntityCard>();
        if (profile["standardArts"] is JsonObject standardArts && standardArts.Count > 0)
        {
            cards.Add(new UiEntityCard
            {
                Title = "Духовные искусства",
                Subtitle = "обычные действия",
                Icon = "sparkles",
                List = standardArts
                    .Select(art => $"{DescribeArt(art.Key)} - уровень {GetNumberOrString(standardArts, art.Key, "0")}")
                    .ToList()
            });
        }

        if (profile["specialArts"] is JsonArray specialArts)
        {
            var specialCards = specialArts
                .OfType<JsonObject>()
                .Select(static art => new UiEntityCard
                {
                    Title = GetString(art, "displayName", GetString(art, "artId", "Особое искусство")),
                    Subtitle = "особое искусство",
                    Icon = "sparkles",
                    Summary = SafePlayerText(GetString(art, "effectSummary", ""), "эффект не описан"),
                    Facts =
                    [
                        new UiEntityFact { Label = "Базовое действие", Value = DescribeArt(GetString(art, "baseOperation", "")) },
                        new UiEntityFact { Label = "Ступень", Value = GetNumberOrString(art, "tier", "0") },
                        new UiEntityFact { Label = "Стоимость", Value = $"{GetNumberOrString(art, "costMultiplierPercent", "100")}% от базовой" },
                        new UiEntityFact { Label = "Можно обучить душу", Value = BoolToRu(art["canTeachPlayer"]) }
                    ],
                    Hints = BuildSpecialArtHints(art)
                })
                .ToList();
            if (specialCards.Count > 0)
            {
                cards.Add(new UiEntityCard
                {
                    Title = "Особые искусства",
                    Subtitle = $"{specialCards.Count} записей",
                    Icon = "sparkles",
                    Cards = specialCards
                });
            }
        }

        var fateCards = EnumerateVisibleFateCards(profile).ToList();
        if (fateCards.Count > 0)
        {
            cards.Add(new UiEntityCard
            {
                Title = "Карты судьбы",
                Subtitle = $"{fateCards.Count} открыто",
                Icon = "scroll-text",
                Cards = fateCards.Select(static card => new UiEntityCard
                {
                    Title = GetString(card, "nameRu", GetString(card, "cardId", "Карта судьбы")),
                    Subtitle = DescribeFateCardStatus(GetString(card, "status", "locked")),
                    Icon = "scroll-text",
                    Summary = SafePlayerText(GetString(card, "storyMeaning", ""), "смысл карты не описан")
                }).ToList()
            });
        }

        if (profile[AfterlifeEntityProfileState.CustomStatesProperty] is JsonArray states)
        {
            var stateCards = states
                .OfType<JsonObject>()
                .Select(static state => new UiEntityCard
                {
                    Title = GetString(state, "stateName", GetString(state, "stateId", "Состояние")),
                    Subtitle = "состояние",
                    Icon = "gauge",
                    Facts =
                    [
                        new UiEntityFact { Label = "Текущее значение", Value = GetNumberOrString(state, "currentValue", "0") },
                        new UiEntityFact { Label = "Максимум", Value = GetNumberOrString(state, "maxValue", "0") }
                    ]
                })
                .ToList();
            if (stateCards.Count > 0)
            {
                cards.Add(new UiEntityCard
                {
                    Title = "Состояния",
                    Subtitle = $"{stateCards.Count} записей",
                    Icon = "gauge",
                    Cards = stateCards
                });
            }
        }

        return cards;
    }

    private static List<UiEntityHint> BuildSpecialArtHints(JsonObject art)
    {
        var hints = new List<UiEntityHint>();
        var combatEffect = art["combatEffect"] as JsonObject;
        AddProfileHint(hints, "Боевой эффект", GetString(combatEffect, "summary", ""), UiTone.Accent);
        AddProfileHint(hints, "Срабатывает", GetString(combatEffect, "trigger", ""), UiTone.Default);
        AddProfileHint(hints, "Выигрыш", GetString(combatEffect, "allowedPayoff", ""), UiTone.Default);
        AddProfileHint(hints, "Ограничение", GetString(combatEffect, "limit", ""), UiTone.Warning);
        return hints;
    }

    private static IEnumerable<UiEntityCard> BuildProfileRelationshipCards(IEnumerable<JsonObject> profiles)
    {
        foreach (var profile in profiles)
        {
            if (profile[AfterlifeEntityProfileState.RelationshipsProperty] is not JsonArray relationships)
                continue;

            var profileName = ProfileDisplayName(profile, ProfileSelector(profile));
            foreach (var relationship in relationships.OfType<JsonObject>())
            {
                var axis = DescribeAfterlifeRelationshipAxis(GetString(relationship, "axis", "?"));
                var target = DescribeRelationshipTargetForPlayer(relationship);
                yield return new UiEntityCard
                {
                    Title = $"{axis}: {target}",
                    Subtitle = profileName,
                    Icon = "link",
                    Summary = DescribeRelationshipProgressForPlayer(relationship),
                    Facts =
                    [
                        new UiEntityFact { Label = "Сущность", Value = profileName },
                        new UiEntityFact { Label = "Цель связи", Value = target },
                        new UiEntityFact { Label = "Текущий уровень", Value = DescribeRelationshipProgressForPlayer(relationship) },
                        new UiEntityFact { Label = "Ближайший порог", Value = DescribeNearestRelationshipThreshold(relationship) },
                        new UiEntityFact { Label = "Условие открытия", Value = DescribeRelationshipUnlockConditionForPlayer(relationship) }
                    ],
                    Hints = BuildRelationshipQuestHints(relationship)
                };
            }
        }
    }

    private static List<UiEntityHint> BuildRelationshipQuestHints(JsonObject relationship)
    {
        var hints = new List<UiEntityHint>();
        var quests = relationship[AfterlifeEntityProfileState.RelationshipGateQuestsProperty] as JsonArray;
        if (quests == null)
            return hints;

        foreach (var quest in quests.OfType<JsonObject>())
        {
            var title = GetString(quest, "title", "Испытание отношений");
            var scene = SafePlayerText(GetString(quest, "sceneSummary", ""), string.Empty);
            var success = SafePlayerText(GetString(quest, "successCondition", ""), string.Empty);
            var text = JoinNonEmpty(Environment.NewLine, scene, success);
            if (!string.IsNullOrWhiteSpace(text))
                hints.Add(new UiEntityHint { Title = title, Text = text, Tone = UiTone.Accent });
        }

        return hints;
    }

    private static IEnumerable<UiEntityCard> BuildProfileMaskCards(IEnumerable<JsonObject> profiles)
    {
        foreach (var profile in profiles)
        {
            if (profile[AfterlifeEntityProfileState.MasksProperty] is not JsonArray masks)
                continue;

            var profileName = ProfileDisplayName(profile, ProfileSelector(profile));
            var activeMaskId = GetString(profile, AfterlifeEntityProfileState.ActiveMaskIdProperty, "");
            foreach (var mask in masks.OfType<JsonObject>())
            {
                var maskId = GetString(mask, "maskId", "");
                var isActive = !string.IsNullOrWhiteSpace(maskId) &&
                               string.Equals(maskId, activeMaskId, StringComparison.OrdinalIgnoreCase);
                var isRevealed = IsAfterlifeMaskRevealed(mask);
                if (!isActive && !isRevealed)
                    continue;

                var displayName = GetString(mask, "displayName", string.IsNullOrWhiteSpace(maskId) ? "Маска" : maskId);
                var publicArchetype = SafePlayerText(GetString(mask, "publicArchetype", ""), "роль не указана");
                var visiblePersonality = SafePlayerText(GetString(mask, "visiblePersonality", ""), "поведение не описано");
                yield return new UiEntityCard
                {
                    Title = displayName,
                    Subtitle = profileName,
                    Icon = "venetian-mask",
                    Summary = visiblePersonality,
                    Facts =
                    [
                        new UiEntityFact { Label = "Сущность", Value = profileName },
                        new UiEntityFact { Label = "Статус", Value = DescribeMaskStatus(isActive, isRevealed) },
                        new UiEntityFact { Label = "Публичная роль", Value = publicArchetype },
                        new UiEntityFact { Label = "Видимое поведение", Value = visiblePersonality },
                        new UiEntityFact { Label = "Риск обмана", Value = DescribeMaskRisk(GetString(mask, "deceptionRisk", "")) }
                    ]
                };
            }
        }
    }

    private static IEnumerable<UiEntityCard> BuildProfileProgressionCards(IEnumerable<JsonObject> profiles)
    {
        foreach (var profile in profiles)
        {
            if (profile["progressionStrategy"] is not JsonObject strategy)
                continue;

            var priorities = (strategy["priorityOrder"] as JsonArray)?
                .Select(static item => DescribeArt(GetNodeString(item)))
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToList() ?? [];

            yield return new UiEntityCard
            {
                Title = ProfileDisplayName(profile, ProfileSelector(profile)),
                Subtitle = "стратегия развития",
                Icon = "trending-up",
                Summary = SafePlayerText(GetString(strategy, "summary", ""), "стратегия не описана"),
                Facts =
                [
                    new UiEntityFact { Label = "Последний цикл", Value = GetString(strategy, "lastAutoProgressionCycleKey", "нет") }
                ],
                List = priorities
            };
        }
    }

    private static IEnumerable<JsonObject> EnumerateVisibleFateCards(JsonObject profile) =>
        (profile["fateCards"] as JsonArray)?
            .OfType<JsonObject>()
            .Where(static card => IsFateCardPlayerVisible(card)) ?? [];

    private static int CountVisibleFateCards(JsonObject profile) =>
        EnumerateVisibleFateCards(profile).Count();

    private static int CountActivePersonalQuests(JsonObject profile) =>
        (profile["personalQuests"] as JsonArray)?
            .OfType<JsonObject>()
            .Count(static quest => string.Equals(GetString(quest, "status", ""), "active", StringComparison.OrdinalIgnoreCase)) ?? 0;

    private static int CountObjectProperties(JsonObject? value) => value?.Count ?? 0;

    private static string BoolToRu(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<bool>(out var result)
            ? result ? "да" : "нет"
            : "не указано";

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
            BuildAfterlifeThreatsDossier(visibleThreats, threats?.Count ?? 0, hiddenCount, activeVisible)
        };

        if (read.FileExists && read.Node == null)
        {
            blocks.Add(Message("Угрозы сейчас недоступны", "Не удалось прочитать видимые угрозы посмертия. Откройте сводку позже или попросите ГМ обновить состояние."));
            return Completed(request.Command, blocks);
        }

        if (visibleThreats.Count == 0)
        {
            blocks.Add(Message("Видимых угроз нет", "Скрытые угрозы, если они есть, не раскрываются обычному интерфейсу до сюжетного раскрытия."));
        }

        if (includeRawDiagnostics && read.Node != null)
            blocks.Add(Raw($"Полный JSON {AfterlifeActiveThreatState.StatePath}", read.Node));

        return Completed(request.Command, blocks, BuildThreatActions(visibleThreats));
    }

    private static UiEntityDossierBlock BuildAfterlifeThreatsDossier(
        IReadOnlyList<JsonObject> visibleThreats,
        int totalThreats,
        int hiddenThreats,
        int activeVisible)
    {
        var summary = visibleThreats.Count > 0
            ? "Открытые душе угрозы посмертия: кто давит на сцену, чем опасен след и куда открыть подробности."
            : "Сейчас нет угроз, раскрытых текущей душе.";

        return new UiEntityDossierBlock
        {
            EntityType = "afterlife-threats",
            Title = "Угрозы посмертия",
            Subtitle = "Видимые следы опасностей в посмертии",
            Summary = summary,
            Facts =
            [
                new UiEntityFact { Label = "Показано угроз", Value = visibleThreats.Count.ToString() },
                new UiEntityFact { Label = "Скрытых угроз", Value = hiddenThreats.ToString() },
                new UiEntityFact { Label = "Активных действий", Value = activeVisible.ToString() },
                new UiEntityFact { Label = "Всего записей", Value = totalThreats.ToString() }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "visible-afterlife-threats",
                    Title = "Видимые угрозы",
                    Summary = "Каждая карточка показывает текущий след угрозы, её давление и команду открытия подробностей.",
                    Icon = "alert-triangle",
                    Presentation = "cards",
                    CollectionLabel = $"{visibleThreats.Count} угроз",
                    Collapsible = visibleThreats.Count > 4,
                    InitiallyExpanded = true,
                    Cards = visibleThreats.Select(BuildAfterlifeThreatCard).ToList()
                }
            ]
        };
    }

    private static UiEntityCard BuildAfterlifeThreatCard(JsonObject threat)
    {
        var selector = ThreatSelector(threat);
        var name = ThreatDisplayName(threat, selector);
        var activity = threat["currentActivity"] as JsonObject;
        var summary = FirstNonEmpty(
            SafePlayerText(GetString(activity, "description", ""), string.Empty),
            SafePlayerText(GetString(activity, "narrativeSummary", ""), string.Empty),
            DescribeThreatImpact(threat["impactProfile"] as JsonObject));

        return new UiEntityCard
        {
            Title = name,
            Subtitle = DescribeRealm(GetString(threat, "realm", "?")),
            Icon = "alert-triangle",
            Summary = SafePlayerText(summary, "угроза раскрыта, но её текущий след не описан"),
            Badges =
            [
                new UiEntityBadge { Label = $"напряжённость {GetNumberOrString(threat, "intensity", "0")}", Tone = UiTone.Warning, Icon = "gauge" }
            ],
            Facts =
            [
                new UiEntityFact { Label = "Область", Value = DescribeRealm(GetString(threat, "realm", "?")) },
                new UiEntityFact { Label = "Напряжённость угрозы", Value = GetNumberOrString(threat, "intensity", "0") },
                new UiEntityFact { Label = "Архетип", Value = DescribeThreatArchetype(threat["threatArchetype"] as JsonObject) },
                new UiEntityFact { Label = "Активность", Value = DescribeThreatActivity(activity) },
                new UiEntityFact { Label = "Давление", Value = DescribeThreatImpact(threat["impactProfile"] as JsonObject) },
                new UiEntityFact { Label = "Связи", Value = DescribeThreatLinks(threat) }
            ],
            Hints = BuildThreatCardHints(threat),
            PrimaryAction = string.IsNullOrWhiteSpace(selector)
                ? null
                : DetailAction(
                    "afterlife-threat-detail-" + ToActionIdPart(selector),
                    "Открыть подробности угрозы",
                    BuildThreatDetailCommand(selector))
        };
    }

    private static UiEntityDossierBlock BuildAfterlifeThreatDetailDossier(JsonObject threat, string name)
    {
        var card = BuildAfterlifeThreatCard(threat);
        var hints = new List<UiEntityHint>(card.Hints);
        var description = SafePlayerText(GetString(threat["currentActivity"] as JsonObject, "description", ""), string.Empty);
        if (!string.IsNullOrWhiteSpace(description))
            hints.Add(new UiEntityHint { Title = "Текущий след угрозы", Text = description, Tone = UiTone.Warning });

        return new UiEntityDossierBlock
        {
            EntityType = "afterlife-threat-detail",
            Title = $"Угроза посмертия: {name}",
            Subtitle = card.Subtitle,
            Summary = card.Summary,
            Badges = card.Badges,
            Facts = card.Facts,
            Hints = hints
        };
    }

    private static List<UiEntityHint> BuildThreatCardHints(JsonObject threat)
    {
        var hints = new List<UiEntityHint>();
        var activity = threat["currentActivity"] as JsonObject;
        var narrative = SafePlayerText(GetString(activity, "narrativeSummary", ""), string.Empty);
        if (!string.IsNullOrWhiteSpace(narrative))
            hints.Add(new UiEntityHint { Title = "Как это ощущается", Text = narrative, Tone = UiTone.Default });

        var ledger = (threat["ledger"] as JsonArray)?
            .OfType<JsonObject>()
            .Select(static entry => SafePlayerText(GetString(entry, "summary", ""), string.Empty))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Take(3)
            .ToArray() ?? [];
        if (ledger.Length > 0)
            hints.Add(new UiEntityHint { Title = "Последний след", Text = string.Join(Environment.NewLine, ledger), Tone = UiTone.Accent });

        return hints;
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
            BuildAfterlifeChroniclesDossier(visibleChronicles, totalChronicles, hiddenCount)
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
            blocks.AddRange(BuildAfterlifeChronicleTimelineBlocks(visibleChronicles));
        }

        if (includeRawDiagnostics && read.Node != null)
            blocks.Add(Raw($"Полный JSON {AfterlifeChronicleState.StatePath}", read.Node));

        return Completed(request.Command, blocks, BuildChronicleActions(visibleChronicles));
    }

    private static UiEntityDossierBlock BuildAfterlifeChroniclesDossier(
        IReadOnlyList<JsonObject> chronicles,
        int totalChronicles,
        int hiddenChronicles)
    {
        var summary = chronicles.Count > 0
            ? "Хроники посмертия показывают, какие события уже стали памятью мира и какие нити остаются открыты."
            : "Пока нет хроник, видимых текущей душе.";

        return new UiEntityDossierBlock
        {
            EntityType = "afterlife-chronicles",
            Title = "Хроники посмертия",
            Subtitle = "Память сцен, решений и последствий",
            Summary = summary,
            Facts =
            [
                new UiEntityFact { Label = "Показано хроник", Value = chronicles.Count.ToString() },
                new UiEntityFact { Label = "Скрытых записей", Value = hiddenChronicles.ToString() },
                new UiEntityFact { Label = "Всего записей", Value = totalChronicles.ToString() },
                new UiEntityFact { Label = "Последний ход", Value = DescribeChronicleLatestTurn(chronicles) }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "visible-afterlife-chronicles",
                    Title = "Видимые хроники",
                    Summary = "Карточки раскрывают последнее событие, последствия и незакрытые сюжетные нити.",
                    Icon = "book-open",
                    Presentation = "cards",
                    CollectionLabel = $"{chronicles.Count} хроник",
                    Collapsible = chronicles.Count > 4,
                    InitiallyExpanded = true,
                    Cards = chronicles.Select(BuildAfterlifeChronicleCard).ToList()
                }
            ]
        };
    }

    private static UiEntityCard BuildAfterlifeChronicleCard(JsonObject chronicle)
    {
        var selector = ChronicleSelector(chronicle);
        var title = ChronicleDisplayName(chronicle, selector);
        var lastEvent = SafeChronicleText(GetString(chronicle, "lastEventsDescription", ""));
        var eventDescriptions = EnumerateChronicleTextArray(chronicle, "eventDescriptions").ToArray();
        var consequences = EnumerateChronicleTextArray(chronicle, "persistentConsequences").ToArray();
        var openThreads = EnumerateChronicleTextArray(chronicle, "openThreads").ToArray();
        var summary = FirstNonEmpty(lastEvent, eventDescriptions.FirstOrDefault() ?? string.Empty, "подробности хроники пока не описаны");

        var hints = new List<UiEntityHint>();
        AddChronicleHint(hints, "Последнее событие", lastEvent, UiTone.Default);
        AddChronicleHint(hints, "Записанные события", eventDescriptions, UiTone.Default);
        AddChronicleHint(hints, "Последствия", consequences, UiTone.Accent);
        AddChronicleHint(hints, "Открытые нити", openThreads, UiTone.Warning);

        return new UiEntityCard
        {
            Title = title,
            Subtitle = DescribeChronicleScope(chronicle),
            Icon = "book-open",
            Summary = summary,
            Facts =
            [
                new UiEntityFact { Label = "Область", Value = DescribeChronicleScope(chronicle) },
                new UiEntityFact { Label = "Последний ход", Value = GetNumberOrString(chronicle, "lastUpdatedTurn", "?") },
                new UiEntityFact { Label = "Участники", Value = DescribeChronicleParticipants(chronicle) }
            ],
            Hints = hints,
            PrimaryAction = string.IsNullOrWhiteSpace(selector)
                ? null
                : DetailAction(
                    "afterlife-chronicle-detail-" + ToActionIdPart(selector),
                    "Открыть хронику",
                    BuildChronicleDetailCommand(selector))
        };
    }

    private static IEnumerable<UiBlock> BuildAfterlifeChronicleTimelineBlocks(IReadOnlyList<JsonObject> chronicles)
    {
        var timelineCards = new List<UiEntityCard>();
        foreach (var chronicle in chronicles)
        {
            var chronicleName = ChronicleDisplayName(chronicle, ChronicleSelector(chronicle));
            foreach (var eventText in EnumerateChronicleTextArray(chronicle, "eventDescriptions"))
            {
                timelineCards.Add(new UiEntityCard
                {
                    Title = DescribeChronicleEventTurn(chronicle, eventText, preferLastUpdatedTurn: false),
                    Subtitle = chronicleName,
                    Icon = "scroll-text",
                    Summary = eventText
                });
            }

            var lastEvents = SafeChronicleText(GetString(chronicle, "lastEventsDescription", ""));
            if (!string.IsNullOrWhiteSpace(lastEvents))
            {
                timelineCards.Add(new UiEntityCard
                {
                    Title = DescribeChronicleEventTurn(chronicle, lastEvents, preferLastUpdatedTurn: true),
                    Subtitle = chronicleName,
                    Icon = "scroll-text",
                    Summary = lastEvents
                });
            }
        }

        if (timelineCards.Count == 0)
            yield break;

        yield return new UiEntityDossierBlock
        {
            EntityType = "afterlife-chronicle-timeline",
            Title = "Хронология",
            Subtitle = "Последовательность видимых событий",
            Summary = "Короткая лента событий из открытых хроник.",
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "afterlife-chronicle-timeline-events",
                    Title = "События",
                    Icon = "scroll-text",
                    Presentation = "cards",
                    CollectionLabel = $"{timelineCards.Count} событий",
                    Collapsible = timelineCards.Count > 6,
                    InitiallyExpanded = true,
                    Cards = timelineCards
                        .OrderByDescending(static card => TryParseInt(card.Title, out var turn) ? turn : 0)
                        .ThenBy(static card => card.Subtitle, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                }
            ]
        };
    }

    private static void AddChronicleHint(List<UiEntityHint> hints, string title, IReadOnlyCollection<string> values, UiTone tone)
    {
        if (values.Count == 0)
            return;

        hints.Add(new UiEntityHint { Title = title, Text = string.Join(Environment.NewLine, values), Tone = tone });
    }

    private static void AddChronicleHint(List<UiEntityHint> hints, string title, string value, UiTone tone)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        hints.Add(new UiEntityHint { Title = title, Text = value, Tone = tone });
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
            BuildAfterlifeInboxDossier(notifications, notificationNodes, unread)
        };

        if (notifications.Count == 0)
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

    private static UiEntityDossierBlock BuildAfterlifeInboxDossier(
        IReadOnlyList<AfterlifeNotificationState.NotificationEntry> notifications,
        IReadOnlyDictionary<string, JsonObject> notificationNodes,
        int unread)
    {
        return new UiEntityDossierBlock
        {
            EntityType = "afterlife-inbox",
            Title = "Уведомления загробья",
            Subtitle = "Ответы ГМ, Хранителей, Архива и Обители",
            Summary = notifications.Count > 0
                ? "Открытые уведомления загробья. Каждое можно открыть отдельно или отметить прочитанным через форму."
                : "Пока нет ответов Хранителей, Архива, резидентов или Сияющей Обители.",
            Facts =
            [
                new UiEntityFact { Label = "Всего уведомлений", Value = notifications.Count.ToString() },
                new UiEntityFact { Label = "Непрочитано", Value = unread.ToString() },
                new UiEntityFact { Label = "Отметка прочитанным", Value = "доступна через форму" }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "afterlife-inbox-notifications",
                    Title = "Ответы и уведомления",
                    Summary = "Карточки показывают источник, тип и краткое содержание уведомления.",
                    Icon = "mail",
                    Presentation = "cards",
                    CollectionLabel = $"{notifications.Count} уведомлений",
                    Collapsible = notifications.Count > 4,
                    InitiallyExpanded = true,
                    Cards = notifications.Select(notification =>
                    {
                        notificationNodes.TryGetValue(notification.NotificationId, out var raw);
                        return BuildAfterlifeInboxNotificationCard(notification, raw);
                    }).ToList()
                }
            ]
        };
    }

    private static UiEntityCard BuildAfterlifeInboxNotificationCard(
        AfterlifeNotificationState.NotificationEntry notification,
        JsonObject? raw)
    {
        var source = DescribeNotificationSource(notification);
        var title = FirstNonEmpty(source, EmptyFallback(notification.Summary, "Уведомление загробья"));
        var details = BuildInboxDetailLines(notification, raw).ToArray();

        return new UiEntityCard
        {
            Title = title,
            Subtitle = AfterlifeNotificationState.GetTypeLabel(notification.NotificationType),
            Icon = string.Equals(notification.Status, AfterlifeNotificationState.StatusUnread, StringComparison.OrdinalIgnoreCase) ? "mail" : "mail-open",
            Summary = EmptyFallback(notification.Summary),
            Facts =
            [
                new UiEntityFact { Label = "Статус", Value = DescribeNotificationStatus(notification.Status) },
                new UiEntityFact { Label = "Тип", Value = AfterlifeNotificationState.GetTypeLabel(notification.NotificationType) },
                new UiEntityFact { Label = "Источник", Value = source },
                new UiEntityFact { Label = "Ход", Value = notification.CreatedAtTurn > 0 ? notification.CreatedAtTurn.ToString() : "не указан" }
            ],
            Hints = details
                .Select(static detail => new UiEntityHint { Title = detail.Label, Text = detail.Value, Tone = UiTone.Default })
                .ToList(),
            PrimaryAction = string.IsNullOrWhiteSpace(notification.NotificationId)
                ? null
                : DetailAction(
                    "afterlife-inbox-detail-" + ToActionIdPart(notification.NotificationId),
                    "Открыть уведомление",
                    BuildInboxNotificationDetailCommand(notification.NotificationId))
        };
    }

    private static async Task<ExplorerCommandResult> BuildConflict(
        CommandRequest request,
        FileSystemManager fs,
        bool includeRawDiagnostics)
    {
        var read = await ReadJson(fs, AfterlifeSpiritualConflictState.StatePath);
        var active = read.Node?["activeConflict"] as JsonObject;
        var exchangeLog = GetVisibleExchangeLog(active);
        var detail = ParseDetailRequest(request.Arguments, "обмен", "exchange", "запись", "entry", "деталь", "detail");
        if (!string.IsNullOrWhiteSpace(detail.Selector))
        {
            return BuildSpiritualExchangeDetail(
                request.Command,
                active,
                exchangeLog,
                detail.Selector,
                "Обмен духовного конфликта",
                "/spiritual_conflict");
        }

        var blocks = new List<UiBlock>
        {
            BuildSpiritualConflictDossier(active, exchangeLog)
        };

        if (active == null)
        {
            blocks.Add(Message("Активного духовного конфликта нет", "Сейчас нет открытого духовного противостояния; когда ГМ начнёт сцену, она появится здесь."));
        }

        AddRawOrWarning(
            blocks,
            "Состояние духовного конфликта",
            SanitizeCombatConditionsForPlayer(read),
            includeRawDiagnostics);
        return Completed(request.Command, blocks, BuildConflictExchangeActions(exchangeLog));
    }

    private static UiEntityDossierBlock BuildSpiritualConflictDossier(
        JsonObject? active,
        IReadOnlyList<JsonObject> exchangeLog)
    {
        var sections = new List<UiEntityDossierSection>();
        if (active != null)
        {
            sections.Add(BuildSpiritualConflictSidesSection(active));
            var conditions = BuildSpiritualConflictConditionsSection(active["combatConditions"] as JsonArray);
            if (conditions != null)
                sections.Add(conditions);
            if (exchangeLog.Count > 0)
                sections.Add(BuildSpiritualConflictExchangesSection(exchangeLog, BuildConflictExchangeDetailCommand));
        }

        return new UiEntityDossierBlock
        {
            EntityType = "spiritual-conflict",
            Title = "Духовный конфликт",
            Subtitle = active == null ? "активного противостояния нет" : DescribeRealm(GetString(active, "realm", "?")),
            Summary = active == null
                ? "Сейчас нет открытого духовного противостояния."
                : "Текущее духовное противостояние: стороны, позиция, напряжение, условия и последние обмены.",
            Facts =
            [
                new UiEntityFact { Label = "Активный конфликт", Value = DescribeActiveConflict(active) },
                new UiEntityFact { Label = "Область", Value = DescribeRealm(GetString(active, "realm", "?")) },
                new UiEntityFact { Label = "Модель сторон", Value = DescribeSideModel(GetString(active, "sideModel", "?")) },
                new UiEntityFact { Label = "Позиция", Value = DescribeConflictPosition(GetString(active, "conflictPosition", "contested")) },
                new UiEntityFact { Label = "Напряжение души", Value = DescribeStrain(GetString(active, "playerSideStrain", "clear")) },
                new UiEntityFact { Label = "Напряжение противника", Value = DescribeStrain(GetString(active, "oppositionSideStrain", "clear")) },
                new UiEntityFact { Label = "Контроль / оковы", Value = DescribeControlState(active?["controlState"] as JsonObject) },
                new UiEntityFact { Label = "Очки действий", Value = DescribeActionEconomy(active?["actionEconomy"] as JsonObject) },
                new UiEntityFact { Label = "Обменов", Value = exchangeLog.Count.ToString() }
            ],
            Sections = sections
        };
    }

    private static UiEntityDossierSection BuildSpiritualConflictSidesSection(JsonObject active) =>
        new()
        {
            Id = "spiritual-conflict-sides",
            Title = "Стороны конфликта",
            Summary = "Кто сейчас ведёт духовное противостояние.",
            Icon = "users",
            Presentation = "cards",
            CollectionLabel = "2 стороны",
            Cards =
            [
                BuildSpiritualConflictSideCard("Сторона души", active["playerSide"] as JsonObject),
                BuildSpiritualConflictSideCard("Противостоящая сторона", active["oppositionSide"] as JsonObject)
            ]
        };

    private static UiEntityCard BuildSpiritualConflictSideCard(string title, JsonObject? side)
    {
        var lead = side?["leadContestant"] as JsonObject;
        var leadName = GetString(lead, "displayName", GetString(lead, "actorId", "не указан"));
        var participantCount = CountArray(side, "contestants");
        return new UiEntityCard
        {
            Title = title,
            Subtitle = leadName,
            Icon = "user",
            Summary = participantCount > 0
                ? $"{leadName}{Environment.NewLine}Участников: {participantCount}"
                : leadName,
            Facts =
            [
                new UiEntityFact { Label = "Ведущий", Value = leadName },
                new UiEntityFact { Label = "Участников", Value = participantCount.ToString() }
            ]
        };
    }

    private static UiEntityDossierSection? BuildSpiritualConflictConditionsSection(JsonArray? combatConditions)
    {
        var cards = BuildVisibleCombatConditionCards(combatConditions).ToList();
        if (cards.Count == 0)
            return null;

        return new UiEntityDossierSection
        {
            Id = "spiritual-conflict-conditions",
            Title = "Боевые условия",
            Summary = "Временные метки, оковы и обстоятельства, которые меняют ближайшие духовные действия.",
            Icon = "sparkles",
            Presentation = "cards",
            CollectionLabel = $"{cards.Count} условий",
            Collapsible = cards.Count > 4,
            InitiallyExpanded = true,
            Cards = cards
        };
    }

    private static IEnumerable<UiEntityCard> BuildVisibleCombatConditionCards(JsonArray? combatConditions)
    {
        if (combatConditions == null)
            yield break;

        foreach (var condition in combatConditions
                     .OfType<JsonObject>()
                     .Where(AfterlifeCombatConditionPlayerAuditSanitizer.IsVisibleToPlayer)
                     .Where(static condition => string.Equals(GetString(condition, "status", "active"), "active", StringComparison.OrdinalIgnoreCase)))
        {
            var title = GetString(condition, "displayName", GetString(condition, "name", "Без названия"));
            var summary = SafePlayerText(GetString(condition, "summary", ""), "условие активно, подробность не описана");
            yield return new UiEntityCard
            {
                Title = title,
                Subtitle = DescribeCombatConditionKind(GetString(condition, "kind", "")),
                Icon = "sparkles",
                Summary = summary,
                Facts =
                [
                    new UiEntityFact { Label = "Вид", Value = DescribeCombatConditionKind(GetString(condition, "kind", "")) },
                    new UiEntityFact { Label = "Цель", Value = DescribeCombatConditionTarget(condition) },
                    new UiEntityFact { Label = "Источник", Value = DescribeCombatConditionSource(condition["source"] as JsonObject) },
                    new UiEntityFact { Label = "Действия", Value = DescribeCombatConditionOperations(condition["affectedOperations"] as JsonArray) },
                    new UiEntityFact { Label = "Срок", Value = DescribeCombatConditionDuration(condition["duration"] as JsonObject) },
                    new UiEntityFact { Label = "Ответ", Value = DescribeCombatConditionCounterplay(condition["counterplay"] as JsonArray) }
                ],
                Hints =
                [
                    new UiEntityHint { Title = "Эффект", Text = summary, Tone = UiTone.Accent }
                ]
            };
        }
    }

    private static UiEntityDossierSection BuildSpiritualConflictExchangesSection(
        IReadOnlyList<JsonObject> exchangeLog,
        Func<string, string> detailCommandBuilder) =>
        new()
        {
            Id = "spiritual-conflict-exchanges",
            Title = "Обмены активного конфликта",
            Summary = "Последние открытые обмены духовного боя.",
            Icon = "swords",
            Presentation = "cards",
            CollectionLabel = $"{exchangeLog.Count} обменов",
            Collapsible = exchangeLog.Count > 4,
            InitiallyExpanded = true,
            Cards = exchangeLog.Select(exchange => BuildSpiritualExchangeCard(exchange, detailCommandBuilder)).ToList()
        };

    private static UiEntityCard BuildSpiritualExchangeCard(
        JsonObject exchange,
        Func<string, string> detailCommandBuilder,
        string actionIdPrefix = "spiritual-conflict-exchange-detail-",
        string actionLabel = "Открыть обмен")
    {
        var selector = SpiritualExchangeSelector(exchange);
        var title = SpiritualExchangeDisplayName(exchange, selector);
        var summary = SafePlayerText(
            FirstNonEmpty(GetString(exchange, "resultSummary", ""), GetString(exchange, "summary", ""), DescribeOutcome(GetString(exchange, "outcome", "?"))),
            DescribeOutcome(GetString(exchange, "outcome", "?")));

        return new UiEntityCard
        {
            Title = title,
            Subtitle = DescribeArt(GetString(exchange, "operationType", "?")),
            Icon = "swords",
            Summary = summary,
            Facts =
            [
                new UiEntityFact { Label = "Действие", Value = DescribeArt(GetString(exchange, "operationType", "?")) },
                new UiEntityFact { Label = "Исход", Value = DescribeOutcome(GetString(exchange, "outcome", "?")) },
                new UiEntityFact { Label = "Позиция", Value = DescribeBeforeAfter(exchange, "conflictPosition", DescribeConflictPosition) },
                new UiEntityFact { Label = "Напряжение", Value = DescribeBeforeAfter(exchange, "oppositionSideStrain", DescribeStrain) },
                new UiEntityFact { Label = "Кубики", Value = DescribeDice(exchange["diceAudit"] as JsonObject) },
                new UiEntityFact { Label = "Награда", Value = DescribeReward(exchange["rewardAudit"] as JsonObject) }
            ],
            PrimaryAction = string.IsNullOrWhiteSpace(selector)
                ? null
                : DetailAction(
                    actionIdPrefix + ToActionIdPart(selector),
                    actionLabel,
                    detailCommandBuilder(selector))
        };
    }

    private static async Task<ExplorerCommandResult> BuildCombatLog(
        CommandRequest request,
        FileSystemManager fs,
        bool includeRawDiagnostics)
    {
        var read = await ReadJson(fs, AfterlifeSpiritualConflictState.StatePath);
        var active = read.Node?["activeConflict"] as JsonObject;
        var exchangeLog = GetVisibleExchangeLog(active);
        var recent = GetVisibleRecentConflicts(read.Node);
        var detail = ParseDetailRequest(
            request.Arguments,
            "обмен",
            "exchange",
            "запись",
            "entry",
            "лог",
            "log",
            "итог",
            "result",
            "recent",
            "конфликт",
            "conflict",
            "деталь",
            "detail");
        if (!string.IsNullOrWhiteSpace(detail.Selector))
        {
            if (IsRecentConflictDetailToken(detail.Token))
                return BuildSpiritualRecentConflictDetail(request.Command, recent, detail.Selector);

            var exchange = FindSpiritualExchange(exchangeLog, detail.Selector);
            if (exchange != null || IsExchangeDetailToken(detail.Token))
            {
                return BuildSpiritualExchangeDetail(
                    request.Command,
                    active,
                    exchangeLog,
                    detail.Selector,
                    "Запись духовного боя",
                    "/spiritual_combat_log");
            }

            return BuildSpiritualRecentConflictDetail(request.Command, recent, detail.Selector);
        }

        var blocks = new List<UiBlock>
        {
            BuildSpiritualCombatLogDossier(active, exchangeLog, recent)
        };

        if (exchangeLog.Count == 0 && recent.Count == 0)
            blocks.Add(Message("Журнал пуст", "Когда ГМ проведёт обмен или завершит конфликт, здесь появятся кубики, позиция, напряжение и награды."));

        AddRawOrWarning(
            blocks,
            "Журнал духовного боя",
            SanitizeCombatConditionsForPlayer(read),
            includeRawDiagnostics);
        return Completed(request.Command, blocks, BuildCombatLogActions(exchangeLog, recent));
    }

    private static UiEntityDossierBlock BuildSpiritualCombatLogDossier(
        JsonObject? active,
        IReadOnlyList<JsonObject> exchangeLog,
        IReadOnlyList<JsonObject> recent)
    {
        var sections = new List<UiEntityDossierSection>();
        if (exchangeLog.Count > 0)
        {
            sections.Add(BuildSpiritualCombatLogExchangeSection(exchangeLog));
        }

        if (active?["combatConditions"] is JsonArray combatConditions)
        {
            var conditions = BuildSpiritualConflictConditionsSection(combatConditions);
            if (conditions != null)
            {
                sections.Add(new UiEntityDossierSection
                {
                    Id = "spiritual-combat-log-conditions",
                    Title = conditions.Title,
                    Summary = "Условия, которые действовали или продолжают действовать в текущем духовном конфликте.",
                    Icon = conditions.Icon,
                    Presentation = conditions.Presentation,
                    CollectionLabel = conditions.CollectionLabel,
                    Collapsible = conditions.Collapsible,
                    InitiallyExpanded = conditions.InitiallyExpanded,
                    Cards = conditions.Cards
                });
            }
        }

        if (recent.Count > 0)
        {
            sections.Add(BuildSpiritualCombatLogRecentSection(recent));
        }

        return new UiEntityDossierBlock
        {
            EntityType = "spiritual-combat-log",
            Title = "Журнал духовного боя",
            Subtitle = "Обмены, условия и завершённые итоги",
            Summary = "Краткая история активного духовного конфликта и последних завершённых противостояний.",
            Facts =
            [
                new UiEntityFact { Label = "Активный конфликт", Value = DescribeActiveConflict(active) },
                new UiEntityFact { Label = "Обменов активного конфликта", Value = exchangeLog.Count.ToString() },
                new UiEntityFact { Label = "Недавних завершённых конфликтов", Value = recent.Count.ToString() }
            ],
            Sections = sections
        };
    }

    private static UiEntityDossierSection BuildSpiritualCombatLogExchangeSection(IReadOnlyList<JsonObject> exchangeLog) =>
        new()
        {
            Id = "spiritual-combat-log-exchanges",
            Title = "Обмены активного конфликта",
            Summary = "Открытые записи обменов с исходом, позицией, бросками и наградами.",
            Icon = "swords",
            Presentation = "cards",
            CollectionLabel = $"{exchangeLog.Count} обменов",
            Collapsible = exchangeLog.Count > 4,
            InitiallyExpanded = true,
            Cards = exchangeLog
                .Select(exchange => BuildSpiritualExchangeCard(
                    exchange,
                    BuildCombatLogExchangeDetailCommand,
                    "spiritual-combat-log-exchange-detail-",
                    "Открыть запись боя"))
                .ToList()
        };

    private static UiEntityDossierSection BuildSpiritualCombatLogRecentSection(IReadOnlyList<JsonObject> recent) =>
        new()
        {
            Id = "spiritual-combat-log-recent",
            Title = "Недавние завершённые конфликты",
            Summary = "Итоги уже закрытых духовных конфликтов и полученные награды.",
            Icon = "scroll-text",
            Presentation = "cards",
            CollectionLabel = $"{recent.Count} итогов",
            Collapsible = recent.Count > 4,
            InitiallyExpanded = true,
            Cards = recent.Select(BuildRecentConflictCard).ToList()
        };

    private static UiEntityCard BuildRecentConflictCard(JsonObject conflict)
    {
        var selector = RecentConflictSelector(conflict);
        var title = RecentConflictDisplayName(conflict, selector);
        var summary = SafePlayerText(
            FirstNonEmpty(GetString(conflict, "resolutionSummary", ""), DescribePlayerOutcome(GetString(conflict, "playerOutcome", GetString(conflict, "outcome", "?")))),
            DescribePlayerOutcome(GetString(conflict, "playerOutcome", GetString(conflict, "outcome", "?"))));

        return new UiEntityCard
        {
            Title = title,
            Subtitle = DescribeRecentResolutionState(GetString(conflict, "resolutionState", "?")),
            Icon = "scroll-text",
            Summary = summary,
            Facts =
            [
                new UiEntityFact { Label = "Состояние", Value = DescribeRecentResolutionState(GetString(conflict, "resolutionState", "?")) },
                new UiEntityFact { Label = "Исход", Value = DescribePlayerOutcome(GetString(conflict, "playerOutcome", GetString(conflict, "outcome", "?"))) },
                new UiEntityFact { Label = "Действие", Value = DescribeArt(GetString(conflict, "operationType", "?")) },
                new UiEntityFact { Label = "Ход", Value = GetNumberOrString(conflict, "resolvedAtTurn", "?") },
                new UiEntityFact { Label = "Награда", Value = DescribeReward(conflict["rewardAudit"] as JsonObject) }
            ],
            PrimaryAction = string.IsNullOrWhiteSpace(selector)
                ? null
                : DetailAction(
                    "spiritual-combat-log-recent-detail-" + ToActionIdPart(selector),
                    "Открыть итог",
                    BuildCombatLogRecentDetailCommand(selector))
        };
    }

    private static ExplorerCommandResult BuildHelp(string command) =>
        Completed(command, BuildSpiritualCombatHelpDossier());

    private static UiEntityDossierBlock BuildSpiritualCombatHelpDossier() =>
        new()
        {
            EntityType = "spiritual-combat-help",
            Title = "Духовный бой",
            Subtitle = "Правила посмертных противостояний",
            Summary = "Духовный бой использует позицию, напряжение, контроль, оковы и очки действий вместо смертного здоровья и энергии.",
            Hints =
            [
                new UiEntityHint
                {
                    Title = "Главное отличие",
                    Text = "Духовный бой посмертия не использует здоровье, энергию и смертные боевые навыки.",
                    Tone = UiTone.Default
                },
                new UiEntityHint
                {
                    Title = "Проверки",
                    Text = "Спорные обмены используют d20, модификаторы, позицию, контроль и оковы.",
                    Tone = UiTone.Default
                },
                new UiEntityHint
                {
                    Title = "Награды",
                    Text = "Победа в проверяемом конфликте может дать Чернильные Перья в Море Хаоса или Искры Света в Сияющей Обители.",
                    Tone = UiTone.Accent
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "spiritual-combat-help-arts",
                    Title = "Духовные искусства и контрприёмы",
                    Summary = "Базовые действия духовного боя и то, как они перекрывают друг друга.",
                    Icon = "sparkles",
                    Presentation = "cards",
                    CollectionLabel = $"{AfterlifeSpiritualConflictState.SpiritualArts.Count} искусств",
                    Cards = AfterlifeSpiritualConflictState.SpiritualArts
                        .Select(static art => new UiEntityCard
                        {
                            Title = DescribeArt(art.ArtId),
                            Subtitle = "духовное искусство",
                            Icon = "sparkles",
                            Summary = art.MechanicalUse,
                            Facts =
                            [
                                new UiEntityFact { Label = "Игровой смысл", Value = art.MechanicalUse },
                                new UiEntityFact { Label = "Сильнее против", Value = DescribeStrongAgainst(art.ArtId) },
                                new UiEntityFact { Label = "Чем перекрывается", Value = DescribeCounteredBy(art.ArtId) }
                            ]
                        })
                        .ToList()
                }
            ]
        };

    private static async Task<ExplorerCommandResult> BuildArts(
        CommandRequest request,
        FileSystemManager fs,
        bool includeRawDiagnostics)
    {
        var soul = await ReadJson(fs, SoulStatePath);
        var profiles = await ReadJson(fs, AfterlifeEntityProfileState.StatePath);
        var combatProfile = soul.Node?["afterlifeCombatProfile"] as JsonObject;
        var standardArts = ResolveStandardArts(combatProfile);
        var playerProfile = FindPlayerProfile(profiles.Node);
        var learnedSpecialArts = playerProfile?["specialArts"] as JsonArray;
        var detail = ParseDetailRequest(
            request.Arguments,
            "искусство",
            "art",
            "приём",
            "standard",
            "особое",
            "special",
            "деталь",
            "detail");
        if (!string.IsNullOrWhiteSpace(detail.Selector))
        {
            if (IsSpecialArtDetailToken(detail.Token))
                return BuildSpecialArtDetail(request.Command, learnedSpecialArts, detail.Selector, soul, profiles);

            var standard = FindStandardSpiritualArt(detail.Selector);
            if (standard != null || IsStandardArtDetailToken(detail.Token))
                return BuildStandardArtDetail(request.Command, combatProfile, standardArts, detail.Selector, soul);

            return BuildSpecialArtDetail(request.Command, learnedSpecialArts, detail.Selector, soul, profiles);
        }

        var blocks = new List<UiBlock>
        {
            BuildSpiritualArtsOverviewDossier(combatProfile, learnedSpecialArts)
        };

        blocks.Add(BuildStandardSpiritualArtsDossier(standardArts));

        if (learnedSpecialArts is { Count: > 0 })
        {
            blocks.Add(BuildSpecialSpiritualArtsDossier(learnedSpecialArts));
        }
        else
        {
            blocks.Add(Message("Особые искусства не изучены", "ГМ может выдать обучение через afterlifeSpecialArtLearningReceipts, если ролевая сцена это признаёт."));
        }

        AddRawOrWarning(blocks, "Полный JSON afterlifeCombatProfile", new JsonReadResult(soul.FileExists, combatProfile, soul.Error), includeRawDiagnostics);
        AddRawOrWarning(blocks, $"Полный JSON {AfterlifeEntityProfileState.StatePath}", profiles, includeRawDiagnostics);
        return Result(
            request.Command,
            CommandExecutionState.RequiresInput,
            blocks,
            BuildSpiritualArtActions(standardArts, learnedSpecialArts),
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

    private static UiEntityDossierBlock BuildSpiritualArtsOverviewDossier(JsonObject? combatProfile, JsonArray? learnedSpecialArts) =>
        new()
        {
            EntityType = "spiritual-arts-overview",
            Title = "Духовные искусства",
            Subtitle = "Прокачка и известные приёмы души",
            Summary = "Здесь видно текущую духовную подготовку, обычные приёмы и особые искусства, которые уже открыты душе.",
            Facts =
            [
                new UiEntityFact { Label = "Просветление", Value = $"{GetNumberOrString(combatProfile, "enlightenmentTier", "0")} ступень" },
                new UiEntityFact { Label = "Сияние", Value = $"{GetNumberOrString(combatProfile, "radianceTier", "0")} ступень" },
                new UiEntityFact { Label = "Средоточие Души", Value = $"{GetNumberOrString(combatProfile, "spiritFocusTier", "0")} ступень" },
                new UiEntityFact { Label = "Особых искусств игрока", Value = (learnedSpecialArts?.Count ?? 0).ToString() }
            ],
            Hints =
            [
                new UiEntityHint
                {
                    Title = "Прокачка",
                    Text = "Прокачка доступна через форму команды: выберите цель и валюту, затем подтвердите действие.",
                    Tone = UiTone.Accent
                }
            ]
        };

    private static UiEntityDossierBlock BuildStandardSpiritualArtsDossier(JsonObject? standardArts) =>
        new()
        {
            EntityType = "spiritual-arts-standard",
            Title = "Стандартные духовные искусства",
            Summary = "Базовые приёмы духовного боя. Уровни уменьшают стоимость и расширяют тактические возможности.",
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "standard-spiritual-arts",
                    Title = "Приёмы",
                    Icon = "sparkles",
                    Presentation = "cards",
                    CollectionLabel = $"{AfterlifeSpiritualConflictState.SpiritualArts.Count} искусств",
                    Cards = AfterlifeSpiritualConflictState.SpiritualArts
                        .Select(art => new UiEntityCard
                        {
                            Title = DescribeArt(art.ArtId),
                            Subtitle = "Стандартный приём духовного боя",
                            Icon = "sparkles",
                            Summary = DescribeStandardArtUse(art.ArtId),
                            Facts =
                            [
                                new UiEntityFact { Label = "Текущий тир", Value = GetNumberOrString(standardArts, art.ArtId, "0") },
                                new UiEntityFact { Label = "Стоимость и темп", Value = DescribeArtCost(art.ArtId) },
                                new UiEntityFact { Label = "Сильно против", Value = DescribeStrongAgainst(art.ArtId) },
                                new UiEntityFact { Label = "Чем перекрывается", Value = DescribeCounteredBy(art.ArtId) }
                            ],
                            Hints =
                            [
                                new UiEntityHint
                                {
                                    Title = "Игровое применение",
                                    Text = DescribeStandardArtUse(art.ArtId),
                                    Tone = UiTone.Default
                                }
                            ]
                        })
                        .ToList()
                }
            ]
        };

    private static UiEntityDossierBlock BuildSpecialSpiritualArtsDossier(JsonArray learnedSpecialArts) =>
        new()
        {
            EntityType = "spiritual-arts-special",
            Title = "Особые духовные искусства игрока",
            Summary = "Уникальные приёмы души. Их эффект читается отдельно от стоимости и боевых ограничений.",
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "special-spiritual-arts",
                    Title = "Изученные особые искусства",
                    Icon = "sparkles",
                    Presentation = "cards",
                    CollectionLabel = $"{learnedSpecialArts.Count} искусств",
                    Cards = learnedSpecialArts.OfType<JsonObject>().Select(BuildSpecialSpiritualArtCard).ToList()
                }
            ]
        };

    private static UiEntityCard BuildSpecialSpiritualArtCard(JsonObject art)
    {
        var combatEffect = art["combatEffect"] as JsonObject;
        var effectSummary = SafePlayerText(GetString(art, "effectSummary", ""), "эффект не описан");
        var combatSummary = SafePlayerText(GetString(combatEffect, "summary", ""), string.Empty);

        var facts = new List<UiEntityFact>
        {
            new() { Label = "Основа", Value = DescribeArt(GetString(art, "baseOperation", "?")) },
            new() { Label = "Тир", Value = GetNumberOrString(art, "tier", "0") },
            new() { Label = "Стоимость", Value = DescribeSpecialArtCost(art) }
        };

        if (combatEffect != null)
        {
            AddSpecialArtCombatFact(facts, "Триггер", DescribeArt(GetString(combatEffect, "trigger", "")));
            AddSpecialArtCombatFact(facts, "Выигрыш", DescribeSpecialArtPayoff(GetString(combatEffect, "allowedPayoff", "")));
            AddSpecialArtCombatFact(facts, "Предел", DescribeSpecialArtLimit(GetString(combatEffect, "limit", "")));
        }

        var hints = new List<UiEntityHint>
        {
            new() { Title = "Эффект", Text = effectSummary, Tone = UiTone.Default }
        };
        if (!string.IsNullOrWhiteSpace(combatSummary))
            hints.Add(new UiEntityHint { Title = "В духовном бою", Text = combatSummary, Tone = UiTone.Accent });

        return new UiEntityCard
        {
            Title = DescribeSpecialArtTitle(art),
            Subtitle = DescribeArt(GetString(art, "baseOperation", "?")),
            Icon = "sparkles",
            Summary = effectSummary,
            Facts = facts,
            Hints = hints
        };
    }

    private static void AddSpecialArtCombatFact(List<UiEntityFact> facts, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "?")
            return;

        facts.Add(new UiEntityFact { Label = label, Value = value });
    }

    private static string DescribeSpecialArtPayoff(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "position" => "позиционное преимущество",
            "strain" => "изменение напряжения",
            "guard" => "защитная позиция",
            "defense" => "защитная позиция",
            "pressure" => "усиление давления",
            "counter" => "контрприём",
            "maneuver" => "манёвр",
            "binding" => "оковы",
            "break_binding" => "разрыв оков",
            _ => SafePlayerText(value, string.Empty)
        };

    private static string DescribeSpecialArtTitle(JsonObject art)
    {
        var title = GetString(art, "displayName", "");
        if (!string.IsNullOrWhiteSpace(title))
        {
            var described = DescribeArt(title);
            if (!string.Equals(described, title, StringComparison.OrdinalIgnoreCase))
                return described;

            return SafePlayerText(title, title);
        }

        return SafePlayerText(GetString(art, "artId", "Без названия"), "Без названия");
    }

    private static string DescribeSpecialArtLimit(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "once_per_exchange" => "один раз за обмен",
            "once_per_conflict" => "один раз за конфликт",
            "once_per_turn" => "один раз за ход",
            _ => SafePlayerText(value, string.Empty)
        };

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
            BuildAfterlifeProfileDetailDossier(profile, name)
        };

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
            BuildAfterlifeThreatDetailDossier(threat, name)
        };

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

    private static ExplorerCommandResult BuildSpiritualExchangeDetail(
        string command,
        JsonObject? active,
        IReadOnlyList<JsonObject> exchanges,
        string selector,
        string titlePrefix,
        string overviewCommand)
    {
        if (active == null)
            return DetailUnavailable(command, "Обмен недоступен", "не удалось открыть обмен: активный духовный конфликт уже завершён или пока не виден текущей душе.");

        var exchange = FindSpiritualExchange(exchanges, selector);
        if (exchange == null)
            return DetailUnavailable(command, "Обмен недоступен", "не удалось открыть обмен: запись уже недоступна, устарела или не видна текущей душе.");

        var name = SpiritualExchangeDisplayName(exchange, selector);
        var blocks = new List<UiBlock>
        {
            Panel($"{titlePrefix}: {name}",
                Grid(
                    ("Конфликт", SafePlayerText(FirstNonEmpty(GetString(active, "displayName", ""), GetString(active, "conflictId", "")), "активный конфликт")),
                    ("Сторона души", DescribeConflictLead(active, "playerSide")),
                    ("Противостояние", DescribeConflictLead(active, "oppositionSide")),
                    ("Действие", DescribeArt(GetString(exchange, "operationType", "?"))),
                    ("Заявка души", SafePlayerText(GetString(exchange, "playerAction", ""), "не указана")),
                    ("Давление противника", SafePlayerText(GetString(exchange, "incomingAction", ""), "не указано")),
                    ("Исход", DescribeOutcome(GetString(exchange, "outcome", "?"))),
                    ("Кубики", DescribeDice(exchange["diceAudit"] as JsonObject)),
                    ("Позиция", DescribeBeforeAfter(exchange, "conflictPosition", DescribeConflictPosition)),
                    ("Напряжение души", DescribeBeforeAfter(exchange, "playerSideStrain", DescribeStrain)),
                    ("Напряжение противника", DescribeBeforeAfter(exchange, "oppositionSideStrain", DescribeStrain)),
                    ("Ответы", DescribeExchangeCounterplay(GetString(exchange, "operationType", "?"))),
                    ("Стоимость ОД", DescribeExchangeActionPointCost(exchange)),
                    ("Награда", DescribeRewardWithReason(exchange["rewardAudit"] as JsonObject))))
        };

        var resultSummary = SafePlayerText(
            FirstNonEmpty(GetString(exchange, "resultSummary", ""), GetString(exchange, "summary", ""), GetString(exchange, "reason", "")),
            string.Empty);
        if (!string.IsNullOrWhiteSpace(resultSummary))
            blocks.Add(Message("Итог обмена", resultSummary));

        return Completed(
            command,
            blocks,
            BuildOverviewAction(
                "spiritual-combat-overview",
                titlePrefix.StartsWith("Запись", StringComparison.OrdinalIgnoreCase)
                    ? "К журналу духовного боя"
                    : "К духовному конфликту",
                overviewCommand));
    }

    private static ExplorerCommandResult BuildSpiritualRecentConflictDetail(
        string command,
        IReadOnlyList<JsonObject> recentConflicts,
        string selector)
    {
        var conflict = FindRecentConflict(recentConflicts, selector);
        if (conflict == null)
            return DetailUnavailable(command, "Итог недоступен", "не удалось открыть итог: запись уже недоступна, устарела или не видна текущей душе.");

        var name = RecentConflictDisplayName(conflict, selector);
        var blocks = new List<UiBlock>
        {
            Panel($"Итог духовного боя: {name}",
                Grid(
                    ("Состояние", DescribeRecentResolutionState(GetString(conflict, "resolutionState", "?"))),
                    ("Исход", DescribePlayerOutcome(GetString(conflict, "playerOutcome", GetString(conflict, "outcome", "?")))),
                    ("Действие", DescribeArt(GetString(conflict, "operationType", "?"))),
                    ("Ход", GetNumberOrString(conflict, "resolvedAtTurn", "?")),
                    ("Награда", DescribeRewardWithReason(conflict["rewardAudit"] as JsonObject)),
                    ("Решение", SafePlayerText(GetString(conflict, "resolutionSummary", ""), "не указано"))))
        };

        return Completed(command, blocks, BuildOverviewAction("spiritual-combat-log-overview", "К журналу духовного боя", "/spiritual_combat_log"));
    }

    private static ExplorerCommandResult BuildStandardArtDetail(
        string command,
        JsonObject? combatProfile,
        JsonObject? standardArts,
        string selector,
        JsonReadResult soul)
    {
        if (soul.FileExists && soul.Node == null)
            return DetailUnavailable(command, "Искусство недоступно", "не удалось открыть искусство: духовный профиль сейчас не читается. Откройте сводку позже или попросите ГМ обновить состояние.");

        var art = FindStandardSpiritualArt(selector);
        if (art == null)
            return DetailUnavailable(command, "Искусство недоступно", "не удалось открыть искусство: запись уже недоступна, устарела или не видна текущей душе.");

        var currentTier = GetArtTier(standardArts, art.ArtId);
        var maxUnlockedTier = GetMaxUnlockedArtTier(combatProfile);
        var availability = maxUnlockedTier >= art.MinUnlockTier
            ? currentTier > 0 ? "доступно и изучено" : "доступно для локальной прокачки"
            : $"пока закрыто до ранга, открывающего тир {art.MinUnlockTier}";
        var blocks = new List<UiBlock>
        {
            Panel($"Духовное искусство: {DescribeArt(art.ArtId)}",
                Grid(
                    ("Тир", currentTier.ToString()),
                    ("Доступность", availability),
                    ("Применение", SafePlayerText(DescribeStandardArtUse(art.ArtId), "контекст сцены")),
                    ("Стоимость и темп", DescribeArtCost(art.ArtId)),
                    ("Ранг души", DescribeCombatRanks(combatProfile)),
                    ("Граница действия", "Осмотр ничего не прокачивает; локальная прокачка остаётся через форму /spiritual_arts.")))
        };

        return Completed(command, blocks, BuildOverviewAction("spiritual-arts-overview", "К духовным искусствам", "/spiritual_arts"));
    }

    private static ExplorerCommandResult BuildSpecialArtDetail(
        string command,
        JsonArray? learnedSpecialArts,
        string selector,
        JsonReadResult soul,
        JsonReadResult profiles)
    {
        if ((soul.FileExists && soul.Node == null) || (profiles.FileExists && profiles.Node == null))
            return DetailUnavailable(command, "Искусство недоступно", "не удалось открыть искусство: духовный профиль сейчас не читается. Откройте сводку позже или попросите ГМ обновить состояние.");

        var art = FindSpecialArt(learnedSpecialArts, selector);
        if (art == null)
            return DetailUnavailable(command, "Искусство недоступно", "не удалось открыть искусство: запись уже недоступна, устарела или не видна текущей душе.");

        var name = SpecialArtDisplayName(art, selector);
        var blocks = new List<UiBlock>
        {
            Panel($"Особое духовное искусство: {name}",
                Grid(
                    ("Основа", DescribeArt(GetString(art, "baseOperation", "?"))),
                    ("Тир", GetNumberOrString(art, "tier", "0")),
                    ("Эффект", SafePlayerText(DescribeSpecialArtEffect(art), "эффект не описан")),
                    ("Стоимость", DescribeSpecialArtCost(art)),
                    ("Доступность", "доступно текущей душе"),
                    ("Применение", SafePlayerText(GetString(art, "effectSummary", ""), "по контексту духовного боя")),
                    ("Граница действия", "Осмотр ничего не прокачивает; локальная прокачка остаётся через форму /spiritual_arts.")))
        };

        return Completed(command, blocks, BuildOverviewAction("spiritual-arts-overview", "К духовным искусствам", "/spiritual_arts"));
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

    private static IEnumerable<UiAction> BuildConflictExchangeActions(IReadOnlyList<JsonObject> exchanges)
    {
        foreach (var exchange in exchanges)
        {
            var selector = SpiritualExchangeSelector(exchange);
            if (string.IsNullOrWhiteSpace(selector))
                continue;

            yield return DetailAction(
                "spiritual-conflict-exchange-detail-" + ToActionIdPart(selector),
                $"Осмотреть обмен: {SpiritualExchangeDisplayName(exchange, selector)}",
                BuildConflictExchangeDetailCommand(selector));
        }
    }

    private static IEnumerable<UiAction> BuildCombatLogActions(
        IReadOnlyList<JsonObject> exchanges,
        IReadOnlyList<JsonObject> recentConflicts)
    {
        foreach (var exchange in exchanges)
        {
            var selector = SpiritualExchangeSelector(exchange);
            if (string.IsNullOrWhiteSpace(selector))
                continue;

            yield return DetailAction(
                "spiritual-combat-log-exchange-detail-" + ToActionIdPart(selector),
                $"Разобрать запись боя: {SpiritualExchangeDisplayName(exchange, selector)}",
                BuildCombatLogExchangeDetailCommand(selector));
        }

        foreach (var conflict in recentConflicts)
        {
            var selector = RecentConflictSelector(conflict);
            if (string.IsNullOrWhiteSpace(selector))
                continue;

            yield return DetailAction(
                "spiritual-combat-log-recent-detail-" + ToActionIdPart(selector),
                $"Разобрать итог: {RecentConflictDisplayName(conflict, selector)}",
                BuildCombatLogRecentDetailCommand(selector));
        }
    }

    private static IEnumerable<UiAction> BuildSpiritualArtActions(JsonObject? standardArts, JsonArray? learnedSpecialArts)
    {
        _ = standardArts;

        foreach (var art in AfterlifeSpiritualConflictState.SpiritualArts)
        {
            yield return DetailAction(
                "spiritual-art-detail-" + ToActionIdPart(art.ArtId),
                $"Осмотреть искусство: {DescribeArt(art.ArtId)}",
                BuildStandardArtDetailCommand(art.ArtId));
        }

        if (learnedSpecialArts == null)
            yield break;

        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var art in learnedSpecialArts.OfType<JsonObject>().Where(IsSpiritualDetailObjectVisibleToPlayer))
        {
            var selector = SpecialArtSelector(art);
            if (string.IsNullOrWhiteSpace(selector) || !added.Add(selector))
                continue;

            yield return DetailAction(
                "spiritual-special-art-detail-" + ToActionIdPart(selector),
                $"Осмотреть искусство: {SpecialArtDisplayName(art, selector)}",
                BuildSpecialArtDetailCommand(selector));
        }
    }

    private static UiTableBlock BuildExchangeTable(
        IReadOnlyList<JsonObject> exchangeLog,
        Func<string, string> detailCommandBuilder) =>
        new()
        {
            Title = "Обмены активного конфликта",
            Columns = ["Обмен", "Действие", "Исход", "Позиция", "Напряжение", "Кубики", "Награда", "Подробно"],
            Rows = exchangeLog
                .Select(exchange =>
                {
                    var selector = SpiritualExchangeSelector(exchange);
                    return new UiTableRow
                    {
                        Cells =
                        [
                            SpiritualExchangeDisplayName(exchange, selector),
                            DescribeArt(GetString(exchange, "operationType", "?")),
                            DescribeOutcome(GetString(exchange, "outcome", "?")),
                            DescribeBeforeAfter(exchange, "conflictPosition", DescribeConflictPosition),
                            DescribeBeforeAfter(exchange, "oppositionSideStrain", DescribeStrain),
                            DescribeDice(exchange["diceAudit"] as JsonObject),
                            DescribeReward(exchange["rewardAudit"] as JsonObject),
                            string.IsNullOrWhiteSpace(selector) ? "не указано" : detailCommandBuilder(selector)
                        ]
                    };
                })
                .ToList()
        };

    private static UiTableBlock BuildRecentConflictTable(
        IReadOnlyList<JsonObject> recent,
        Func<string, string> detailCommandBuilder) =>
        new()
        {
            Title = "Недавние завершённые конфликты",
            Columns = ["Конфликт", "Состояние", "Итог", "Ход", "Награда", "Подробно"],
            Rows = recent
                .Select(conflict =>
                {
                    var selector = RecentConflictSelector(conflict);
                    return new UiTableRow
                    {
                        Cells =
                        [
                            RecentConflictDisplayName(conflict, selector),
                            DescribeRecentResolutionState(GetString(conflict, "resolutionState", "?")),
                            DescribePlayerOutcome(GetString(conflict, "playerOutcome", GetString(conflict, "outcome", "?"))),
                            GetNumberOrString(conflict, "resolvedAtTurn", "?"),
                            DescribeReward(conflict["rewardAudit"] as JsonObject),
                            string.IsNullOrWhiteSpace(selector) ? "не указано" : detailCommandBuilder(selector)
                        ]
                    };
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
            var nestedSideLabel = DescribeCombatConditionSide(targetSideValue);
            if (string.IsNullOrWhiteSpace(targetActorValue) || LooksLikeTechnicalIdentifier(targetActorValue))
                return nestedSideLabel;

            return $"{nestedSideLabel}: {targetActorValue}";
        }

        var rootTargetSide = GetString(condition, "targetSide", "?");
        var targetActor = GetString(condition, "targetActorRef", GetString(condition, "targetActorId", ""));
        var sideLabel = DescribeCombatConditionSide(rootTargetSide);
        return string.IsNullOrWhiteSpace(targetActor) || LooksLikeTechnicalIdentifier(targetActor)
            ? sideLabel
            : $"{sideLabel}: {targetActor}";
    }

    private static string DescribeCombatConditionSource(JsonObject? source)
    {
        if (source == null)
            return "не указан";

        var type = GetString(source, "type", GetString(source, "sourceType", ""));
        var actorType = GetString(source, "actorType", "");
        var displayName = GetString(source, "displayName", "");
        var sourceType = DescribeCombatConditionSourceType(type);
        var actorLabel = !string.IsNullOrWhiteSpace(displayName) && !LooksLikeTechnicalIdentifier(displayName)
            ? displayName
            : DescribeActorType(actorType);
        if (string.IsNullOrWhiteSpace(sourceType) && string.IsNullOrWhiteSpace(actorLabel))
            return "не указан";
        if (string.IsNullOrWhiteSpace(sourceType))
            return actorLabel;
        if (string.IsNullOrWhiteSpace(actorLabel) || actorLabel == actorType)
            return sourceType;

        return $"{sourceType}: {actorLabel}";
    }

    private static string DescribeCombatConditionDuration(JsonObject? duration)
    {
        if (duration == null)
            return "не указано";

        var parts = new List<string>();
        var type = GetString(duration, "type", "");
        if (!string.IsNullOrWhiteSpace(type))
            parts.Add(DescribeCombatConditionDurationType(type));
        if (duration.ContainsKey("remainingUses"))
            parts.Add($"осталось применений: {GetNumberOrString(duration, "remainingUses", "?")}");
        if (duration.ContainsKey("expiresAtTurn"))
            parts.Add($"до хода {GetNumberOrString(duration, "expiresAtTurn", "?")}");
        if (duration.ContainsKey("until"))
            parts.Add($"до события: {DescribeCombatConditionFreeText(GetString(duration, "until", "?"))}");
        return parts.Count == 0 ? "не указано" : string.Join(Environment.NewLine, parts);
    }

    private static string DescribeCombatConditionKind(string kind) =>
        kind.Trim().ToLowerInvariant() switch
        {
            "mark" => "метка",
            "vow" => "клятва",
            "binding" => "оковы",
            "pressure" => "давление",
            "guard" => "защита",
            "counter" => "контрприём",
            "maneuver" => "манёвр",
            "buff" => "усиление",
            "debuff" => "ослабление",
            "" => "условие",
            _ => LooksLikeTechnicalIdentifier(kind) ? "условие" : kind
        };

    private static string DescribeCombatConditionSide(string side) =>
        side.Trim().ToLowerInvariant() switch
        {
            "player" or "player_side" or "playerside" => "душа игрока",
            "opposition" or "opposition_side" or "oppositionside" => "противник",
            "both" => "обе стороны",
            "" or "?" => "не указано",
            _ => LooksLikeTechnicalIdentifier(side) ? "сторона конфликта" : side
        };

    private static string DescribeCombatConditionSourceType(string type) =>
        type.Trim().ToLowerInvariant() switch
        {
            "special_art" => "особое духовное искусство",
            "standard_art" => "духовное искусство",
            "story_link" => "сюжетная связь",
            "combat_condition" => "боевое условие",
            "guardian" => "Хранитель",
            "resident" => "резидент",
            "profile" => "профиль сущности",
            "" => string.Empty,
            _ => LooksLikeTechnicalIdentifier(type) ? "источник" : type
        };

    private static string DescribeCombatConditionDurationType(string type) =>
        type.Trim().ToLowerInvariant() switch
        {
            "next_matching_operation" => "до следующего подходящего действия",
            "scene" => "до конца сцены",
            "turns" => "несколько ходов",
            "until_removed" => "пока не снято действием",
            "instant" => "мгновенно",
            "" => "не указано",
            _ => LooksLikeTechnicalIdentifier(type) ? "по условию сцены" : type
        };

    private static string DescribeCombatConditionOperations(JsonArray? operations)
    {
        if (operations == null)
            return "нет";

        var values = operations
            .Select(GetNodeString)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => DescribeArt(value!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return values.Length == 0 ? "нет" : string.Join(Environment.NewLine, values);
    }

    private static string DescribeCombatConditionCounterplay(JsonArray? counterplay)
    {
        if (counterplay == null)
            return "нет";

        var values = counterplay
            .Select(GetNodeString)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => DescribeCombatConditionFreeText(value!))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return values.Length == 0 ? "нет" : string.Join(Environment.NewLine, values);
    }

    private static string DescribeCombatConditionFreeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value.Trim()
            .Replace("break_binding", "разрыв оков", StringComparison.OrdinalIgnoreCase)
            .Replace("recover_spiritual_power", "собрать Средоточие", StringComparison.OrdinalIgnoreCase)
            .Replace("pressure", "давление", StringComparison.OrdinalIgnoreCase)
            .Replace("counter", "контрприём", StringComparison.OrdinalIgnoreCase)
            .Replace("guard", "защита", StringComparison.OrdinalIgnoreCase)
            .Replace("maneuver", "манёвр", StringComparison.OrdinalIgnoreCase)
            .Replace("binding", "оковы", StringComparison.OrdinalIgnoreCase)
            .Replace("rollMode", "режим броска", StringComparison.OrdinalIgnoreCase);
        return SafePlayerText(text, string.Empty);
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

    private static string DescribeActiveConflict(JsonObject? active)
    {
        if (active == null)
            return "нет";

        var displayName = FirstSafePlayerText(
            GetString(active, "displayName", ""),
            GetString(active, "title", ""),
            GetString(active, "summary", ""));
        return string.IsNullOrWhiteSpace(displayName) ? "идёт" : displayName;
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
        var scopeType = GetString(chronicle, "scopeType", "");
        var scopeLabel = DescribeChronicleScopeType(scopeType);
        var scopeName = SafeChronicleText(FirstNonEmpty(
            GetString(chronicle, "scopeName", ""),
            GetString(chronicle, "locationName", ""),
            GetString(chronicle, "guardianName", ""),
            GetString(chronicle, "factionName", "")));
        if (string.IsNullOrWhiteSpace(scopeName))
        {
            var scopeId = SafeChronicleText(GetString(chronicle, "scopeId", ""));
            if (!LooksLikeTechnicalIdentifier(scopeId))
                scopeName = scopeId;
        }

        if (string.IsNullOrWhiteSpace(scopeLabel) && string.IsNullOrWhiteSpace(scopeName))
            return "не указано";
        if (string.IsNullOrWhiteSpace(scopeLabel))
            return scopeName;
        if (string.IsNullOrWhiteSpace(scopeName))
            return scopeLabel;

        return $"{scopeLabel}: {scopeName}";
    }

    private static string DescribeChronicleScopeType(string scopeType) =>
        scopeType.Trim().ToLowerInvariant() switch
        {
            "guardian_scene" => "сцена Хранителя",
            "guardian" => "Хранитель",
            "resident_scene" => "сцена резидента",
            "resident" => "резидент",
            "player_soul" => "душа игрока",
            "soul" => "душа",
            "realm" => "область",
            "chaos_sea" => "Море Хаоса",
            "shining_abode" => "Сияющая Обитель",
            "location" => "место",
            "faction" => "фракция",
            "project" => "проект",
            "conflict" => "конфликт",
            "custom" => "особая хроника",
            "" => string.Empty,
            _ => LooksLikeTechnicalIdentifier(scopeType) ? "хроника" : SafeChronicleText(scopeType)
        };

    private static bool LooksLikeTechnicalIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        return trimmed.Contains('_', StringComparison.Ordinal) ||
               trimmed.Contains(':', StringComparison.Ordinal) ||
               trimmed.Contains("Id", StringComparison.Ordinal) ||
               trimmed.Contains("ID", StringComparison.Ordinal) ||
               trimmed.Contains("id", StringComparison.Ordinal);
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

    private static string SafePlayerText(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var trimmed = value.Trim();
        return IsPlayerSafeSpiritualText(trimmed) ? trimmed : fallback;
    }

    private static bool IsPlayerSafeSpiritualText(string? value)
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
               !lower.Contains("gmonly", StringComparison.Ordinal) &&
               !lower.Contains("gmthoughts", StringComparison.Ordinal) &&
               !lower.Contains("gm thoughts", StringComparison.Ordinal) &&
               !lower.Contains("jsonexception", StringComparison.Ordinal) &&
               !lower.Contains("json поврежд", StringComparison.Ordinal) &&
               !lower.Contains("path:", StringComparison.Ordinal) &&
               !lower.Contains("linenumber", StringComparison.Ordinal) &&
               !lower.Contains("bytepositioninline", StringComparison.Ordinal) &&
               !lower.Contains("game_state/", StringComparison.Ordinal) &&
               !lower.Contains(".json", StringComparison.Ordinal) &&
               !lower.Contains("dto", StringComparison.Ordinal) &&
               !lower.Contains("api", StringComparison.Ordinal) &&
               !lower.Contains("endpoint", StringComparison.Ordinal) &&
               !lower.Contains("protocol", StringComparison.Ordinal) &&
               !lower.Contains("debug", StringComparison.Ordinal) &&
               !lower.Contains("requestid", StringComparison.Ordinal) &&
               !lower.Contains("actiontype", StringComparison.Ordinal);
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

    private static string DescribeRewardWithReason(JsonObject? audit)
    {
        var reward = DescribeReward(audit);
        if (audit == null)
            return reward;

        var reason = SafePlayerText(
            FirstNonEmpty(GetString(audit, "reason", ""), GetString(audit, "rewardReason", "")),
            string.Empty);
        return string.IsNullOrWhiteSpace(reason) ? reward : $"{reward}; {reason}";
    }

    private static string DescribeConflictLead(JsonObject? active, string sidePropertyName)
    {
        var lead = (active?[sidePropertyName] as JsonObject)?["leadContestant"] as JsonObject;
        if (lead == null)
            return "не указан";

        var displayName = SafePlayerText(GetString(lead, "displayName", ""), string.Empty);
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName;

        var actorType = GetString(lead, "actorType", "");
        if (!string.IsNullOrWhiteSpace(actorType))
            return DescribeActorType(actorType);

        return SafePlayerText(GetString(lead, "actorId", ""), "не указан");
    }

    private static string DescribeExchangeActionPointCost(JsonObject exchange)
    {
        if (exchange["actionPointCost"] is JsonObject cost)
        {
            var parts = new List<string>();
            if (TryGetInt(cost["player"], out var playerCost))
                parts.Add($"душа {playerCost} ОД");
            if (TryGetInt(cost["opposition"], out var oppositionCost))
                parts.Add($"противник {oppositionCost} ОД");
            if (TryGetInt(cost["total"], out var totalCost))
                parts.Add($"всего {totalCost} ОД");
            if (parts.Count > 0)
                return string.Join("; ", parts);
        }

        if (TryGetInt(exchange["actionPointCost"], out var plainCost))
            return $"{plainCost} ОД";
        if (TryGetInt(exchange["actionPointImpact"], out var impact))
            return $"{impact} ОД";

        return "не указана";
    }

    private static string DescribeExchangeCounterplay(string operationType) =>
        operationType.Trim().ToLowerInvariant() switch
        {
            "pressure" => $"{DescribeArt("guard")} или {DescribeArt("counter")} удерживают давление",
            "guard" => $"{DescribeArt("maneuver")} ищет обход защиты",
            "counter" => "лучший ответ - не дать явного входящего удара",
            "maneuver" => $"{DescribeArt("pressure")} или встречный манёвр спорят за позицию",
            "binding" or "force_binding" => $"{DescribeArt("break_binding")} или {DescribeArt("counter")} ломают оковы",
            "break_binding" => $"{DescribeArt("pressure")} мешает спокойно снять оковы",
            _ => "зависит от заявок сторон"
        };

    private static string DescribeRecentResolutionState(string state) =>
        state.Trim().ToLowerInvariant() switch
        {
            "resolved" or "complete" or "completed" => "решён",
            "active" => "идёт",
            "abandoned" => "оставлен",
            "cancelled" or "canceled" => "отменён",
            "repair_cancelled" => "отменён восстановлением",
            "stale" => "устарел",
            _ => string.IsNullOrWhiteSpace(state) ? "не указано" : state
        };

    private static string DescribePlayerOutcome(string outcome) =>
        outcome.Trim().ToLowerInvariant() switch
        {
            "victory" or "player_victory" or "success" => "победа",
            "defeat" or "loss" or "player_defeat" => "поражение",
            "draw" or "stalemate" => "ничья",
            "escaped" => "отступление",
            "abandoned" => "оставлено",
            "partial_success" => "частичный успех",
            _ => string.IsNullOrWhiteSpace(outcome) ? "не указано" : outcome
        };

    private static int GetArtTier(JsonObject? standardArts, string artId) =>
        standardArts == null ? 0 : GetInt(standardArts[artId]);

    private static int GetMaxUnlockedArtTier(JsonObject? combatProfile)
    {
        if (combatProfile == null)
            return 0;

        var enlightenmentRank = Math.Max(
            GetInt(combatProfile["enlightenmentTier"]),
            GetInt(combatProfile["enlightenmentRank"]));
        var radianceRank = Math.Max(
            GetInt(combatProfile["radianceTier"]),
            GetInt(combatProfile["radianceRank"]));
        var retainedRadianceRank = Math.Max(
            GetInt(combatProfile["retainedRadianceTier"]),
            GetInt(combatProfile["retainedRadianceRank"]));

        return Math.Max(
            ResolveUnlockedTierFromRanks(AfterlifeSpiritualConflictState.EnlightenmentRanks, enlightenmentRank),
            Math.Max(
                ResolveUnlockedTierFromRanks(AfterlifeSpiritualConflictState.RadianceRanks, radianceRank),
                ResolveUnlockedTierFromRanks(AfterlifeSpiritualConflictState.RadianceRanks, retainedRadianceRank)));
    }

    private static int ResolveUnlockedTierFromRanks(
        IEnumerable<AfterlifeSpiritualConflictState.RankDefinition> ranks,
        int rank) =>
        ranks
            .Where(definition => definition.Rank <= rank)
            .Select(definition => definition.UnlocksArtTier)
            .DefaultIfEmpty(0)
            .Max();

    private static string DescribeCombatRanks(JsonObject? combatProfile)
    {
        if (combatProfile == null)
            return "нет профиля";

        return $"Просветление {GetNumberOrString(combatProfile, "enlightenmentTier", "0")}; " +
               $"Сияние {GetNumberOrString(combatProfile, "radianceTier", "0")}; " +
               $"Средоточие {GetNumberOrString(combatProfile, "spiritFocusTier", "0")}; " +
               $"открытый тир {GetMaxUnlockedArtTier(combatProfile)}";
    }

    private static string DescribeStandardArtUse(string artId) =>
        artId.Trim().ToLowerInvariant() switch
        {
            "pressure" => "усиливает прямое духовное давление и ухудшает напряжение противника",
            "guard" => "держит сторону души от входящего вреда и снижает напряжение",
            "counter" => "разворачивает конкретное входящее действие в контрприём",
            "maneuver" => "меняет позицию конфликта без грубого подавления",
            "binding" => "накладывает оковы, когда душа получила рычаг",
            "force_binding" => "усиливает контроль и удерживает противника в оковах",
            "break_binding" => "ломает или ослабляет чужие духовные оковы",
            "incarnation_resistance" => "сопротивляется принудительному воплощению",
            "champion_coordination" => "помогает стороне, когда за душу действует чемпион",
            "recover_spiritual_power" => "собирает Средоточие и возвращает запас ОД",
            _ => "применяется по контексту духовного боя"
        };

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

    private static IReadOnlyList<JsonObject> GetVisibleExchangeLog(JsonObject? active)
    {
        if (active?["exchangeLog"] is not JsonArray exchangeLog)
            return [];

        return exchangeLog
            .OfType<JsonObject>()
            .Where(IsSpiritualDetailObjectVisibleToPlayer)
            .ToList();
    }

    private static IReadOnlyList<JsonObject> GetVisibleRecentConflicts(JsonNode? root)
    {
        if (root?["recentConflicts"] is not JsonArray recent)
            return [];

        return recent
            .OfType<JsonObject>()
            .Where(IsSpiritualDetailObjectVisibleToPlayer)
            .ToList();
    }

    private static bool IsSpiritualDetailObjectVisibleToPlayer(JsonObject item)
    {
        if (IsFalseFlag(item["isPlayerVisible"]) ||
            IsFalseFlag(item["playerVisible"]) ||
            IsFalseFlag(item["visibleToPlayer"]) ||
            IsFalseFlag(item["visibleForPlayer"]))
        {
            return false;
        }

        if (IsTrueFlag(item["isHidden"]) ||
            IsTrueFlag(item["hidden"]) ||
            IsTrueFlag(item["isSecret"]) ||
            IsTrueFlag(item["secret"]) ||
            IsTrueFlag(item["gmOnly"]) ||
            IsTrueFlag(item["isGmOnly"]) ||
            IsTrueFlag(item["internal"]) ||
            IsTrueFlag(item["isInternal"]))
        {
            return false;
        }

        return !IsHiddenPlayerFacingVisibility(GetString(item, "visibility", "")) &&
               !IsHiddenPlayerFacingVisibility(GetString(item, "audience", ""));
    }

    private static string SpiritualExchangeSelector(JsonObject exchange) =>
        FirstSafePlayerText(
            GetString(exchange, "exchangeId", ""),
            GetString(exchange, "eventId", ""),
            GetString(exchange, "id", ""),
            GetString(exchange, "displayName", ""));

    private static string SpiritualExchangeDisplayName(JsonObject exchange, string fallback) =>
        BuildSpiritualDisplayLabel(
            FirstNonEmpty(
                GetString(exchange, "displayName", ""),
                JoinNonEmpty(": ", DescribeArt(GetString(exchange, "operationType", "")), GetString(exchange, "resultSummary", "")),
                JoinNonEmpty(": ", DescribeArt(GetString(exchange, "operationType", "")), GetString(exchange, "summary", "")),
                DescribeArt(GetString(exchange, "operationType", ""))),
            fallback,
            "обмен");

    private static JsonObject? FindSpiritualExchange(IEnumerable<JsonObject> exchanges, string selector)
    {
        var normalized = NormalizeSelector(selector);
        if (string.IsNullOrWhiteSpace(normalized) || !IsPlayerSafeSpiritualText(normalized))
            return null;

        return exchanges.FirstOrDefault(exchange =>
            SelectorMatches(
                normalized,
                SpiritualExchangeSelector(exchange),
                GetString(exchange, "exchangeId", ""),
                GetString(exchange, "eventId", ""),
                GetString(exchange, "id", ""),
                GetString(exchange, "displayName", "")));
    }

    private static string RecentConflictSelector(JsonObject conflict) =>
        FirstSafePlayerText(
            GetString(conflict, "conflictId", ""),
            GetString(conflict, "eventId", ""),
            GetString(conflict, "id", ""),
            GetString(conflict, "displayName", ""));

    private static string RecentConflictDisplayName(JsonObject conflict, string fallback) =>
        BuildSpiritualDisplayLabel(
            FirstNonEmpty(
                GetString(conflict, "displayName", ""),
                JoinNonEmpty(": ", DescribePlayerOutcome(GetString(conflict, "playerOutcome", GetString(conflict, "outcome", ""))), GetString(conflict, "resolutionSummary", "")),
                JoinNonEmpty(": ", DescribeArt(GetString(conflict, "operationType", "")), GetString(conflict, "rewardSummary", "")),
                DescribePlayerOutcome(GetString(conflict, "playerOutcome", GetString(conflict, "outcome", "")))),
            fallback,
            "итог");

    private static string BuildSpiritualDisplayLabel(string candidate, string fallback, string genericLabel)
    {
        var safeCandidate = SafePlayerText(candidate, string.Empty);
        if (!string.IsNullOrWhiteSpace(safeCandidate) && !IsLikelySpiritualIdentifier(safeCandidate))
            return CompactSpiritualLabel(safeCandidate);

        var safeFallback = SafePlayerText(fallback, string.Empty);
        return !string.IsNullOrWhiteSpace(safeFallback) && !IsLikelySpiritualIdentifier(safeFallback)
            ? CompactSpiritualLabel(safeFallback)
            : genericLabel;
    }

    private static string CompactSpiritualLabel(string value)
    {
        var singleLine = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return singleLine.Length <= 96 ? singleLine : singleLine[..93] + "...";
    }

    private static string JoinNonEmpty(string separator, params string?[] values)
    {
        var parts = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim())
            .ToArray();
        return parts.Length == 0 ? string.Empty : string.Join(separator, parts);
    }

    private static bool IsLikelySpiritualIdentifier(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.Contains('_', StringComparison.Ordinal) &&
            !trimmed.Contains("::", StringComparison.Ordinal))
        {
            return false;
        }

        return trimmed.All(static ch =>
            ch is '_' or '-' or ':' ||
            char.IsAsciiLetterLower(ch) ||
            char.IsAsciiDigit(ch));
    }

    private static JsonObject? FindRecentConflict(IEnumerable<JsonObject> conflicts, string selector)
    {
        var normalized = NormalizeSelector(selector);
        if (string.IsNullOrWhiteSpace(normalized) || !IsPlayerSafeSpiritualText(normalized))
            return null;

        return conflicts.FirstOrDefault(conflict =>
            SelectorMatches(
                normalized,
                RecentConflictSelector(conflict),
                GetString(conflict, "conflictId", ""),
                GetString(conflict, "eventId", ""),
                GetString(conflict, "id", ""),
                GetString(conflict, "displayName", "")));
    }

    private static JsonObject? ResolveStandardArts(JsonObject? combatProfile) =>
        combatProfile?["standardArts"] as JsonObject ??
        combatProfile?["spiritualArts"] as JsonObject ??
        combatProfile?["arts"] as JsonObject;

    private static AfterlifeSpiritualConflictState.SpiritualArtDefinition? FindStandardSpiritualArt(string selector)
    {
        var normalized = NormalizeSelector(selector);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        return AfterlifeSpiritualConflictState.SpiritualArts.FirstOrDefault(art =>
            SelectorMatches(normalized, art.ArtId, art.DisplayName, DescribeArt(art.ArtId)));
    }

    private static string SpecialArtSelector(JsonObject art) =>
        FirstSafePlayerText(
            GetString(art, "artId", ""),
            GetString(art, "specialArtId", ""),
            GetString(art, "id", ""),
            GetString(art, "displayName", ""));

    private static string SpecialArtDisplayName(JsonObject art, string fallback)
    {
        var title = DescribeSpecialArtTitle(art);
        return string.IsNullOrWhiteSpace(title)
            ? SafePlayerText(fallback, "особое искусство")
            : title;
    }

    private static JsonObject? FindSpecialArt(JsonArray? learnedSpecialArts, string selector)
    {
        var normalized = NormalizeSelector(selector);
        if (string.IsNullOrWhiteSpace(normalized) || learnedSpecialArts == null || !IsPlayerSafeSpiritualText(normalized))
            return null;

        return learnedSpecialArts
            .OfType<JsonObject>()
            .Where(IsSpiritualDetailObjectVisibleToPlayer)
            .FirstOrDefault(art =>
                SelectorMatches(
                    normalized,
                    SpecialArtSelector(art),
                    GetString(art, "artId", ""),
                    GetString(art, "specialArtId", ""),
                    GetString(art, "id", ""),
                    GetString(art, "displayName", "")));
    }

    private static bool IsExchangeDetailToken(string token) =>
        IsDetailToken(token, "обмен", "exchange", "запись", "entry", "лог", "log");

    private static bool IsRecentConflictDetailToken(string token) =>
        IsDetailToken(token, "итог", "result", "recent", "конфликт", "conflict");

    private static bool IsStandardArtDetailToken(string token) =>
        IsDetailToken(token, "искусство", "art", "приём", "standard", "деталь", "detail");

    private static bool IsSpecialArtDetailToken(string token) =>
        IsDetailToken(token, "особое", "special");

    private static bool IsDetailToken(string token, params string[] candidates) =>
        !string.IsNullOrWhiteSpace(token) &&
        candidates.Any(candidate => string.Equals(token, candidate, StringComparison.OrdinalIgnoreCase));

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

    private static string BuildConflictExchangeDetailCommand(string selector) =>
        "/spiritual_conflict обмен " + FormatCommandArgument(selector);

    private static string BuildCombatLogExchangeDetailCommand(string selector) =>
        "/spiritual_combat_log обмен " + FormatCommandArgument(selector);

    private static string BuildCombatLogRecentDetailCommand(string selector) =>
        "/spiritual_combat_log итог " + FormatCommandArgument(selector);

    private static string BuildStandardArtDetailCommand(string selector) =>
        "/spiritual_arts искусство " + FormatCommandArgument(selector);

    private static string BuildSpecialArtDetailCommand(string selector) =>
        "/spiritual_arts особое " + FormatCommandArgument(selector);

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

    private static string FirstSafePlayerText(params string?[] values) =>
        values
            .Select(static value => SafePlayerText(value, string.Empty))
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

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
