using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

public static partial class ExplorerUniversalMetaCommandResultBuilder
{
    private static async Task<ExplorerCommandResult> BuildSarefFindWings(
        string command,
        StateManager stateManager,
        FileSystemManager fs)
    {
        var story = await ReadSarefStoryOrHidden(command, fs);
        if (story.HiddenOrIssue != null)
            return story.HiddenOrIssue;

        if (!stateManager.CurrentState.IsInShiningAbode)
        {
            return Completed(command,
                Message(
                    UiNotificationSeverity.Warning,
                    "Поиск Крыльев Ангелов",
                    "Поиск Крыльев доступен только в обычной активной Сияющей Обители. В Море Хаоса можно собирать фрагменты, но нельзя начать внедрение."));
        }

        var shiningRead = await ReadJson(fs, ShiningAbodeState.StatePath);
        if (shiningRead.Node is not JsonObject shiningRoot)
            return MissingOrMalformed(command, "Сияющая Обитель", shiningRead);

        var pending = await SarefMainStoryState.ReadWingsInfiltrationRequestStateAsync(fs);
        if (pending.IsMalformed)
        {
            return Completed(command,
                Message(
                    UiNotificationSeverity.Warning,
                    "Поиск Крыльев Ангелов",
                    $"{SarefMainStoryState.PendingWingsInfiltrationPath} повреждён: {pending.Error}. Исправьте ожидающий файл перед повторной попыткой."));
        }

        if (pending.Request != null)
        {
            return Completed(command,
                BuildSarefWingsInfiltrationDossier(pending.Request, "ожидает закрытия ГМ"));
        }

        var blocker = await SourceOfLightCapstoneState.TryDescribeBlockingPendingContractAsync(fs, shiningRoot);
        if (blocker != null)
        {
            return Completed(command,
                Message(
                    UiNotificationSeverity.Warning,
                    "Поиск Крыльев Ангелов",
                    $"Поиск Крыльев заблокирован: есть {blocker}."));
        }

        var request = SarefMainStoryState.BuildWingsInfiltrationRequest(
            story.Root,
            Math.Max(1, stateManager.CurrentState.TurnNumber + 1));
        if (request == null)
            return HiddenSarefResult(command);

        return RequiresInput(command,
            [
                BuildSarefWingsInfiltrationDossier(request, "готов к началу")
            ],
            [
                new UiSelectionPrompt
                {
                    Id = "saref_wings_action",
                    Prompt = "Начать поиск Крыльев Ангелов?",
                    Required = true,
                    Options =
                    [
                        Option("start", "Начать поиск", "Создать ожидающий запрос и подготовить действие для ГМа.")
                    ]
                }
            ]);
    }

    private static UiEntityDossierBlock BuildSarefWingsInfiltrationDossier(JsonObject request, string state)
    {
        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Состояние", state);
        AddFactIfKnown(facts, "Маршрут", DescribeSarefWingsRouteSafety(GetString(request, "routeSafety", string.Empty)));
        AddFactIfKnown(facts, "Режим входа", DescribeSarefWingsEntryMode(GetString(request, "entryMode", string.Empty)));
        AddFactIfKnown(facts, "Фрагментов маршрута", CountArray(request, "routeFragments").ToString());
        AddFactIfKnown(facts, "Запасных фрагментов", CountArray(request, "substituteFragments").ToString());
        AddFactIfKnown(facts, "Доступных преимуществ", CountArray(request, "availableAdvantages").ToString());

        var sections = new List<UiEntityDossierSection>
        {
            new()
            {
                Id = "saref-wings-infiltration-overview",
                Title = "Сведения",
                Icon = "route",
                Presentation = "facts",
                Facts = facts
            }
        };

        AddSarefWingsObjectArraySection(sections, request, "routeFragments", "Фрагменты маршрута", "map");
        AddSarefWingsObjectArraySection(sections, request, "substituteFragments", "Запасные фрагменты", "layers");
        AddSarefWingsObjectArraySection(sections, request, "availableAdvantages", "Доступные преимущества", "sparkles");
        AddSarefWingsStringArraySection(sections, request, "disadvantages", "Риски маршрута", "alert-triangle");

        return new UiEntityDossierBlock
        {
            EntityType = "saref-wings-infiltration",
            Title = "Поиск Крыльев Ангелов",
            Subtitle = state,
            Summary = "Маршрут внедрения в Крылья Ангелов готовится как сюжетное действие для ГМа.",
            Badges =
            [
                new UiEntityBadge
                {
                    Label = DescribeSarefWingsRouteSafety(GetString(request, "routeSafety", string.Empty)),
                    Icon = "route",
                    Tone = UiTone.Accent
                }
            ],
            Sections = sections
        };
    }

    private static void AddSarefWingsObjectArraySection(
        List<UiEntityDossierSection> sections,
        JsonObject request,
        string propertyName,
        string title,
        string icon)
    {
        if (request[propertyName] is not JsonArray array || array.Count == 0)
            return;

        var cards = array
            .OfType<JsonObject>()
            .Select((item, index) => BuildSarefWingsCard(item, index, icon))
            .ToList();
        if (cards.Count == 0)
            return;

        sections.Add(new UiEntityDossierSection
        {
            Id = "saref-wings-" + propertyName,
            Title = title,
            Icon = icon,
            Presentation = "cards",
            CollectionLabel = cards.Count == 1 ? "1 запись" : $"{cards.Count} записей",
            Cards = cards
        });
    }

    private static UiEntityCard BuildSarefWingsCard(JsonObject item, int index, string icon)
    {
        var category = GetString(item, "category", string.Empty);
        var displayName = FirstNonEmpty(
            GetString(item, "displayName", string.Empty),
            GetString(item, "name", string.Empty),
            DescribeSarefFragmentCategory(category),
            $"Запись {index + 1}");
        var summary = FirstNonEmpty(
            GetString(item, "summary", string.Empty),
            GetString(item, "description", string.Empty),
            "Подробности пока не записаны.");
        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Категория", DescribeSarefFragmentCategory(category));
        AddFactIfKnown(facts, "Состояние", DescribeSarefAdvantageState(GetString(item, "state", string.Empty)));

        return new UiEntityCard
        {
            Title = displayName,
            Summary = summary,
            Icon = icon,
            Facts = facts
        };
    }

    private static void AddSarefWingsStringArraySection(
        List<UiEntityDossierSection> sections,
        JsonObject request,
        string propertyName,
        string title,
        string icon)
    {
        if (request[propertyName] is not JsonArray array || array.Count == 0)
            return;

        var values = array
            .Select(static item => item?.GetValue<string>() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        if (values.Count == 0)
            return;

        sections.Add(new UiEntityDossierSection
        {
            Id = "saref-wings-" + propertyName,
            Title = title,
            Icon = icon,
            Presentation = "list",
            CollectionLabel = values.Count == 1 ? "1 запись" : $"{values.Count} записей",
            List = values
        });
    }

    private static async Task<ExplorerCommandResult> BuildSarefUseAdvantage(string command, FileSystemManager fs)
    {
        var story = await ReadSarefStoryOrHidden(command, fs);
        if (story.HiddenOrIssue != null)
            return story.HiddenOrIssue;

        var advantages = EnumerateUsableSarefAdvantages(story.Root).ToList();
        if (advantages.Count == 0)
        {
            return Completed(command,
                Message(
                    UiNotificationSeverity.Info,
                    "Преимущество Сарефа",
                    "Нет доступных преимуществ для текущей линии."));
        }

        return RequiresInput(command,
            [
                BuildSarefActionOverview(story.Root, "Использовать преимущество"),
                BuildSarefAdvantageDossier(advantages)
            ],
            [
                new UiSelectionPrompt
                {
                    Id = "saref_advantage_id",
                    Prompt = "Какое преимущество использовать?",
                    Required = true,
                    Options = advantages
                        .Select(advantage => Option(
                            GetString(advantage, "advantageId"),
                            GetString(advantage, "displayName", GetString(advantage, "advantageId")),
                            GetString(advantage, "summary", "Без описания.")))
                        .ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "saref_scene_type",
                    Prompt = "Тип сцены, где применяется преимущество",
                    Required = true,
                    Options =
                    [
                        Option(SarefMainStoryState.SceneWingsInfiltration, "Внедрение в Крылья", "Поиск, вход или проверка Крыльев Ангелов."),
                        Option(SarefMainStoryState.SceneSarefConfrontation, "Конфронтация", "Прямое столкновение с Сарефом."),
                        Option(SarefMainStoryState.SceneOathBreak, "Разрыв клятвы", "Юридический, духовный или личный разрыв клятвы."),
                        Option(SarefMainStoryState.SceneMemoryAttack, "Атака на память", "Стирание, подавление или подмена памяти."),
                        Option(SarefMainStoryState.SceneFactionConflict, "Конфликт фракций", "Политическая или силовая сцена против фракций.")
                    ]
                },
                new UiLongTextInputPrompt
                {
                    Id = "saref_action_summary",
                    Prompt = "Как именно игрок применяет преимущество?",
                    Required = true,
                    MinLines = 3,
                    MaxLines = 8,
                    Placeholder = "Опишите художественное действие и цель преимущества."
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildSarefFinalConfrontation(string command, FileSystemManager fs)
    {
        var story = await ReadSarefStoryOrHidden(command, fs);
        if (story.HiddenOrIssue != null)
            return story.HiddenOrIssue;

        if (SarefMainStoryState.StageRank(GetString(story.Root, "revealStage", string.Empty)) < SarefMainStoryState.StageRank(SarefMainStoryState.RevealStageWingsRevealed))
        {
            return Completed(command,
                Message(UiNotificationSeverity.Info, "Развязка с Сарефом", "Эта ветвь ещё не открыта."));
        }

        return RequiresInput(command,
            [BuildSarefActionOverview(story.Root, "Развязка с Сарефом")],
            [
                new UiSelectionPrompt
                {
                    Id = "saref_route_type",
                    Prompt = "Маршрут развязки",
                    Required = true,
                    Options =
                    [
                        Option(SarefMainStoryState.FinalRouteCombat, "Бой", "Прямое духовное столкновение."),
                        Option(SarefMainStoryState.FinalRoutePolitical, "Политика", "Разоблачение, коалиция или удар по власти."),
                        Option(SarefMainStoryState.FinalRouteOathLaw, "Закон клятв", "Развязка через договоры и клятвенные условия."),
                        Option(SarefMainStoryState.FinalRouteMetaphysical, "Метафизика", "Удар по чужемирной природе Сарефа."),
                        Option(SarefMainStoryState.FinalRouteHybrid, "Смешанный путь", "Несколько подготовленных путей сразу."),
                        Option(SarefMainStoryState.FinalRouteDeal, "Сделка", "Принять союз и клятву Крыльев Ангелов.")
                    ]
                },
                new UiSelectionPrompt
                {
                    Id = "saref_resolution_intent",
                    Prompt = "Намерение игрока",
                    Required = true,
                    Options =
                    [
                        Option("defeat_saref", "Победить Сарефа", "ГМ должен закрыть сцену победой/поражением по контракту."),
                        Option("accept_deal", "Принять сделку", "ГМ должен оформить deal route, клятву и награды."),
                        Option("force_scene", "Открыть сцену", "Игрок инициирует прямую сцену, исход решает ГМ.")
                    ]
                },
                new UiLongTextInputPrompt
                {
                    Id = "saref_action_summary",
                    Prompt = "Что делает игрок в сцене?",
                    Required = true,
                    MinLines = 3,
                    MaxLines = 10,
                    Placeholder = "Опишите вызов, аргументы, бой или сделку."
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildSarefOathBreak(string command, FileSystemManager fs)
    {
        var story = await ReadSarefStoryOrHidden(command, fs);
        if (story.HiddenOrIssue != null)
            return story.HiddenOrIssue;

        if (!HasSarefOath(story.Root))
        {
            return Completed(command,
                Message(UiNotificationSeverity.Info, "Разрыв клятвы", "У игрока нет активной клятвы Сарефа, которую можно разрывать через эту форму."));
        }

        return RequiresInput(command,
            [BuildSarefActionOverview(story.Root, "Разрыв клятвы")],
            [
                new UiSelectionPrompt
                {
                    Id = "saref_oath_break_route",
                    Prompt = "Путь разрыва клятвы",
                    Required = true,
                    Options =
                    [
                        Option(SarefMainStoryState.OathBreakRouteSeret, "Серет", "Юридическое вскрытие клятвы."),
                        Option(SarefMainStoryState.OathBreakRouteLucian, "Люциан", "Разрез ложного света и печати."),
                        Option(SarefMainStoryState.OathBreakRouteIlarion, "Иларион", "Якорь памяти и доказательство подмены."),
                        Option(SarefMainStoryState.OathBreakRouteVeyra, "Вейра", "Роль, маска и выход из навязанной идентичности."),
                        Option(SarefMainStoryState.OathBreakRouteDeepStoryEvidence, "Глубокое доказательство", "Собранная правда линии Сарефа.")
                    ]
                },
                new UiLongTextInputPrompt
                {
                    Id = "saref_action_summary",
                    Prompt = "Как игрок пытается разорвать клятву?",
                    Required = true,
                    MinLines = 3,
                    MaxLines = 10,
                    Placeholder = "Опишите доказательство, цену и выбранный путь."
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildSarefAgenda(string command, FileSystemManager fs)
    {
        var story = await ReadSarefStoryOrHidden(command, fs);
        if (story.HiddenOrIssue != null)
            return story.HiddenOrIssue;

        if (story.Root["postStoryAgenda"] is not JsonObject agenda ||
            !string.Equals(GetString(agenda, "state", string.Empty), SarefMainStoryState.PostStoryStateOathbound, StringComparison.OrdinalIgnoreCase))
        {
            return Completed(command,
                Message(UiNotificationSeverity.Info, "Поручения Сарефа", "Поручения доступны только после deal-ending, когда игрок связан клятвой с Сарефом."));
        }

        return RequiresInput(command,
            [
                BuildSarefActionOverview(story.Root, "Поручение Сарефа"),
                BuildSarefAgendaDossier(agenda)
            ],
            [
                new UiSelectionPrompt
                {
                    Id = "saref_agenda_action",
                    Prompt = "Что продвинуть?",
                    Required = true,
                    Options =
                    [
                        Option("assignment_update", "Поручение", "Продвинуть конкретное поручение Сарефа."),
                        Option("faction_campaign", "Кампания фракций", "Связать ход с factionConflictCampaigns[]."),
                        Option("domination_scene", "Финальная сцена доминирования", "Зафиксировать момент, когда противников Сарефа больше нет.")
                    ]
                },
                new UiLongTextInputPrompt
                {
                    Id = "saref_action_summary",
                    Prompt = "Что именно делает игрок?",
                    Required = true,
                    MinLines = 3,
                    MaxLines = 10,
                    Placeholder = "Опишите поручение, целевую фракцию, доказательство или итог сцены."
                }
            ]);
    }

    private static UiEntityDossierBlock BuildSarefAgendaDossier(JsonObject agenda)
    {
        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Состояние", DescribeSarefPostStoryState(GetString(agenda, "state", string.Empty)));
        AddFactIfKnown(facts, "Цель", GetString(agenda, "currentObjective"));
        AddFactIfKnown(facts, "Пояснение", GetString(agenda, "agendaSummary"));
        AddFactIfKnown(facts, "Поручений", CountArray(agenda, "assignments").ToString());
        AddFactIfKnown(facts, "Доминирование", agenda["dominationScene"] is JsonObject ? "есть сцена" : "не завершено");

        var sections = new List<UiEntityDossierSection>
        {
            new()
            {
                Id = "saref-agenda-current",
                Title = "Повестка",
                Icon = "list-check",
                Presentation = "facts",
                Facts = facts
            }
        };

        AddSectionIfPresent(sections, BuildSarefAgendaAssignmentsSection(agenda["assignments"] as JsonArray));
        AddSectionIfPresent(sections, BuildSarefDominationSceneSection(agenda["dominationScene"] as JsonObject));
        AddSectionIfPresent(sections, BuildSarefOathBreakArcSection(agenda["oathBreakArc"] as JsonObject));

        return new UiEntityDossierBlock
        {
            EntityType = "saref-agenda",
            Title = "Текущая повестка",
            Summary = FirstNonEmpty(
                GetString(agenda, "agendaSummary", string.Empty),
                GetString(agenda, "currentObjective", string.Empty),
                "Текущая цель Сарефа пока не записана."),
            Badges =
            [
                new UiEntityBadge
                {
                    Label = agenda["dominationScene"] is JsonObject ? "финал записан" : "финал не завершён",
                    Icon = "list-check",
                    Tone = agenda["dominationScene"] is JsonObject ? UiTone.Success : UiTone.Warning
                }
            ],
            Sections = sections
        };
    }

    private static UiEntityDossierSection? BuildSarefAgendaAssignmentsSection(JsonArray? assignments)
    {
        var cards = assignments?
            .OfType<JsonObject>()
            .Select(BuildSarefAgendaAssignmentCard)
            .Where(static card => !string.IsNullOrWhiteSpace(card.Title))
            .ToList() ?? [];

        if (cards.Count == 0)
            return null;

        return new UiEntityDossierSection
        {
            Id = "saref-agenda-assignments",
            Title = "Поручения Сарефа",
            Icon = "list-check",
            Presentation = "cards",
            CollectionLabel = cards.Count == 1 ? "1 поручение" : $"{cards.Count} поручений",
            Cards = cards
        };
    }

    private static UiEntityCard BuildSarefAgendaAssignmentCard(JsonObject assignment)
    {
        var status = DescribeSarefAssignmentStatus(GetString(assignment, "status", string.Empty));
        var summary = FirstNonEmpty(
            GetString(assignment, "summary", string.Empty),
            GetString(assignment, "objective", string.Empty),
            "Подробности поручения пока не записаны.");

        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Состояние", status);
        AddFactIfKnown(facts, "Целевая фракция", FirstNonEmpty(
            GetString(assignment, "targetFactionName", string.Empty),
            GetString(assignment, "targetFactionDisplayName", string.Empty),
            GetString(assignment, "targetFactionId", string.Empty)));
        AddFactIfKnown(facts, "Кампания", FirstNonEmpty(
            GetString(assignment, "campaignName", string.Empty),
            GetString(assignment, "campaignDisplayName", string.Empty),
            GetString(assignment, "campaignId", string.Empty)));
        AddFactIfKnown(facts, "Ход выдачи", GetNumberOrString(assignment, "assignedAtTurn"));
        AddFactIfKnown(facts, "Ход закрытия", GetNumberOrString(assignment, "resolvedAtTurn"));

        return new UiEntityCard
        {
            Title = FirstNonEmpty(
                GetString(assignment, "displayName", string.Empty),
                GetString(assignment, "name", string.Empty),
                GetString(assignment, "objective", string.Empty),
                GetString(assignment, "assignmentId", string.Empty),
                "Поручение Сарефа"),
            Subtitle = status,
            Summary = summary,
            Icon = "list-check",
            Facts = facts
        };
    }

    private static UiEntityDossierSection? BuildSarefDominationSceneSection(JsonObject? dominationScene)
    {
        if (dominationScene == null)
            return null;

        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Состояние", DescribeSarefAssignmentStatus(GetString(dominationScene, "status", string.Empty)));
        AddFactIfKnown(facts, "Ход", GetNumberOrString(dominationScene, "resolvedAtTurn"));
        AddFactIfKnown(facts, "Источник", GetString(dominationScene, "source"));

        return new UiEntityDossierSection
        {
            Id = "saref-agenda-domination",
            Title = "Финал власти Сарефа",
            Icon = "crown",
            Presentation = "cards",
            Cards =
            [
                new UiEntityCard
                {
                    Title = "Финал власти Сарефа",
                    Summary = FirstNonEmpty(
                        GetString(dominationScene, "summary", string.Empty),
                        "Финальная сцена власти записана, но описание пока отсутствует."),
                    Icon = "crown",
                    Facts = facts
                }
            ]
        };
    }

    private static UiEntityDossierSection? BuildSarefOathBreakArcSection(JsonObject? arc)
    {
        if (arc == null)
            return null;

        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Состояние", DescribeSarefOathBreakState(GetString(arc, "state", string.Empty)));
        AddFactIfKnown(facts, "Путь", DescribeSarefOathBreakRoute(GetString(arc, "route", string.Empty)));
        AddFactIfKnown(facts, "Доказательство", GetString(arc, "proofSummary"));

        return new UiEntityDossierSection
        {
            Id = "saref-agenda-oath-break",
            Title = "Арка разрыва клятвы",
            Icon = "unlink",
            Presentation = "cards",
            Cards =
            [
                new UiEntityCard
                {
                    Title = FirstNonEmpty(GetString(arc, "displayName", string.Empty), GetString(arc, "arcId", string.Empty), "Арка разрыва клятвы"),
                    Summary = FirstNonEmpty(GetString(arc, "summary", string.Empty), "Арка разрыва клятвы пока без описания."),
                    Icon = "unlink",
                    Facts = facts
                }
            ]
        };
    }

    private static ExplorerCommandResult RequiresInput(string command, IEnumerable<UiBlock> blocks, IEnumerable<UiPrompt> prompts) =>
        new()
        {
            Command = command,
            State = CommandExecutionState.RequiresInput,
            Blocks = blocks.ToList(),
            Prompts = prompts.ToList()
        };

    private static async Task<(JsonObject Root, ExplorerCommandResult? HiddenOrIssue)> ReadSarefStoryOrHidden(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, SarefMainStoryState.StatePath);
        if (read.Node == null)
            return (new JsonObject(), HiddenSarefResult(command));
        if (read.Node is not JsonObject root)
            return (new JsonObject(), HiddenSarefResult(command));
        if (IsSarefStoryStillUnknown(root))
            return (root, HiddenSarefResult(command));

        return (root, null);
    }

    private static ExplorerCommandResult HiddenSarefResult(string command) =>
        Completed(command,
            Message(
                UiNotificationSeverity.Info,
                "Скрытая нить",
                "Ты пока не знаешь, что искать."));

    private static UiEntityDossierBlock BuildSarefActionOverview(JsonObject root, string title)
    {
        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Стадия", DescribeSarefRevealStage(GetString(root, "revealStage", string.Empty)));
        AddFactIfKnown(facts, "Фрагментов", CountArray(root, "sarefRevelations").ToString());
        AddFactIfKnown(facts, "Преимуществ", CountArray(root, "sarefAdvantages").ToString());
        AddFactIfKnown(facts, "Клятва", root["playerOathState"] is JsonObject oath ? DescribeSarefOathState(GetString(oath, "state", string.Empty)) : "нет");

        return new UiEntityDossierBlock
        {
            EntityType = "saref-action-overview",
            Title = title,
            Summary = "Сводка текущего положения линии Сарефа перед выбором действия.",
            Badges =
            [
                new UiEntityBadge { Label = DescribeSarefRevealStage(GetString(root, "revealStage", string.Empty)), Icon = "sparkles", Tone = UiTone.Accent }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "saref-action-overview",
                    Title = "Сведения",
                    Icon = "sparkles",
                    Presentation = "facts",
                    Facts = facts
                }
            ]
        };
    }

    private static UiEntityDossierBlock BuildSarefAdvantageDossier(IEnumerable<JsonObject> advantages)
    {
        var cards = advantages
            .Select(BuildSarefAdvantageCard)
            .ToList();

        return new UiEntityDossierBlock
        {
            EntityType = "saref-advantages",
            Title = "Доступные преимущества",
            Summary = cards.Count == 1
                ? "Можно применить одно подготовленное преимущество."
                : $"Можно применить {cards.Count} подготовленных преимуществ.",
            Badges =
            [
                new UiEntityBadge { Label = cards.Count == 1 ? "1 преимущество" : $"{cards.Count} преимуществ", Icon = "sparkles", Tone = UiTone.Accent }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "saref-advantages",
                    Title = "Преимущества",
                    Icon = "sparkles",
                    Presentation = "cards",
                    CollectionLabel = cards.Count == 1 ? "1 преимущество" : $"{cards.Count} преимуществ",
                    Cards = cards
                }
            ]
        };
    }

    private static UiEntityCard BuildSarefAdvantageCard(JsonObject advantage)
    {
        var facts = new List<UiEntityFact>();
        AddFactIfKnown(facts, "Состояние", DescribeSarefAdvantageState(GetString(advantage, "state", string.Empty)));
        AddFactIfKnown(facts, "Сцены", DescribeSarefScenes(advantage["applicableScenes"] as JsonArray));

        return new UiEntityCard
        {
            Title = FirstNonEmpty(
                GetString(advantage, "displayName", string.Empty),
                GetString(advantage, "name", string.Empty),
                "Преимущество"),
            Summary = FirstNonEmpty(GetString(advantage, "summary", string.Empty), "Описание преимущества пока не записано."),
            Icon = "sparkles",
            Facts = facts
        };
    }

    private static IEnumerable<JsonObject> EnumerateUsableSarefAdvantages(JsonObject root)
    {
        if (root["sarefAdvantages"] is not JsonArray advantages)
            yield break;

        foreach (var advantage in advantages.OfType<JsonObject>())
        {
            var state = GetString(advantage, "state", string.Empty);
            if (string.Equals(state, SarefMainStoryState.AdvantageStateAvailable, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(state, SarefMainStoryState.AdvantageStatePassive, StringComparison.OrdinalIgnoreCase))
            {
                yield return advantage;
            }
        }
    }

    private static bool HasSarefOath(JsonObject root)
    {
        if (root["playerOathState"] is JsonObject oath)
        {
            var state = GetString(oath, "state", string.Empty);
            if (string.Equals(state, "oathbound", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(state, "strained", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return root["postStoryAgenda"] is JsonObject agenda &&
               string.Equals(GetString(agenda, "state", string.Empty), SarefMainStoryState.PostStoryStateOathbound, StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeSarefPostStoryState(string state) =>
        state.ToLowerInvariant() switch
        {
            SarefMainStoryState.PostStoryStateOathbound => "связана послесюжетной клятвой",
            "active" => "активно",
            "completed" => "завершено",
            _ => string.IsNullOrWhiteSpace(state) ? "не указано" : state
        };

    private static string DescribeSarefAssignmentStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "active" => "активно",
            "completed" => "завершено",
            "failed" => "провалено",
            "cancelled" => "отменено",
            "blocked" => "заблокировано",
            _ => string.IsNullOrWhiteSpace(status) ? "не указано" : status
        };

    private static string DescribeSarefOathBreakState(string state) =>
        state.ToLowerInvariant() switch
        {
            "active" => "активно",
            "completed" => "завершено",
            "failed" => "провалено",
            "blocked" => "заблокировано",
            _ => string.IsNullOrWhiteSpace(state) ? "не указано" : state
        };

    private static string DescribeSarefOathBreakRoute(string route) =>
        route.ToLowerInvariant() switch
        {
            SarefMainStoryState.OathBreakRouteSeret => "Серет",
            SarefMainStoryState.OathBreakRouteLucian => "Люциан",
            SarefMainStoryState.OathBreakRouteIlarion => "Иларион",
            SarefMainStoryState.OathBreakRouteVeyra => "Вейра",
            SarefMainStoryState.OathBreakRouteDeepStoryEvidence => "глубокое доказательство",
            _ => string.IsNullOrWhiteSpace(route) ? "не указано" : route
        };

    private static string DescribeSarefWingsRouteSafety(string routeSafety) =>
        routeSafety.ToLowerInvariant() switch
        {
            SarefMainStoryState.WingsRouteSafetySafe => "безопасный маршрут",
            SarefMainStoryState.WingsRouteSafetyRisky => "рискованный маршрут",
            SarefMainStoryState.WingsRouteSafetyDesperate => "отчаянный маршрут",
            _ => string.IsNullOrWhiteSpace(routeSafety) ? "не указано" : routeSafety
        };

    private static string DescribeSarefWingsEntryMode(string entryMode) =>
        entryMode.ToLowerInvariant() switch
        {
            "safe_infiltration" => "осторожное внедрение",
            "risky_infiltration" => "рискованное внедрение",
            "desperate_infiltration" => "отчаянное внедрение",
            _ => string.IsNullOrWhiteSpace(entryMode) ? "не указано" : entryMode
        };

    private static string DescribeSarefFragmentCategory(string category) =>
        category.ToLowerInvariant() switch
        {
            SarefMainStoryState.CategoryIdentity => "личность",
            SarefMainStoryState.CategoryMethod => "метод",
            SarefMainStoryState.CategoryFaction => "фракция",
            SarefMainStoryState.CategoryPath => "путь",
            _ => string.IsNullOrWhiteSpace(category) ? "не указано" : category
        };

    private static string DescribeSarefAdvantageState(string state) =>
        state.ToLowerInvariant() switch
        {
            SarefMainStoryState.AdvantageStateAvailable => "доступно",
            SarefMainStoryState.AdvantageStatePassive => "пассивно",
            SarefMainStoryState.AdvantageStateSpent => "использовано",
            _ => string.IsNullOrWhiteSpace(state) ? string.Empty : state
        };

    private static string DescribeSarefOathState(string state) =>
        state.ToLowerInvariant() switch
        {
            "oathbound" or "oathbound_to_saref" => "связана клятвой",
            "strained" => "клятва напряжена",
            "broken" => "клятва разорвана",
            _ => string.IsNullOrWhiteSpace(state) ? "нет" : state
        };

    private static string DescribeSarefScenes(JsonArray? scenes)
    {
        if (scenes == null || scenes.Count == 0)
            return string.Empty;

        var labels = scenes
            .Select(static scene => TryGetScalarString(scene, out var value) ? DescribeSarefScene(value) : string.Empty)
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return labels.Count == 0 ? string.Empty : string.Join(", ", labels);
    }

    private static string DescribeSarefScene(string scene) =>
        scene.ToLowerInvariant() switch
        {
            SarefMainStoryState.SceneWingsInfiltration => "внедрение в Крылья",
            SarefMainStoryState.SceneSarefConfrontation => "конфронтация с Сарефом",
            SarefMainStoryState.SceneOathBreak => "разрыв клятвы",
            SarefMainStoryState.SceneMemoryAttack => "атака на память",
            SarefMainStoryState.SceneFactionConflict => "конфликт фракций",
            _ => scene
        };

    private static UiSelectionOption Option(string value, string label, string description) =>
        new()
        {
            Value = value,
            Label = label,
            Description = description
        };
}
