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
                BuildSarefAdvantageTable(advantages)
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
                Panel("Текущая повестка",
                    Grid(
                        ("Цель", GetString(agenda, "currentObjective")),
                        ("Поручений", CountArray(agenda, "assignments").ToString()),
                        ("Доминирование", agenda["dominationScene"] is JsonObject ? "есть сцена" : "не завершено")))
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

    private static UiPanelBlock BuildSarefActionOverview(JsonObject root, string title) =>
        Panel(title,
            Grid(
                ("Стадия", DescribeSarefRevealStage(GetString(root, "revealStage", string.Empty))),
                ("Фрагментов", CountArray(root, "sarefRevelations").ToString()),
                ("Преимуществ", CountArray(root, "sarefAdvantages").ToString()),
                ("Клятва", root["playerOathState"] is JsonObject oath ? GetString(oath, "state") : "нет")));

    private static UiTableBlock BuildSarefAdvantageTable(IEnumerable<JsonObject> advantages) =>
        new()
        {
            Title = "Доступные преимущества",
            Columns = ["ID", "Название", "Описание"],
            Rows = advantages
                .Select(advantage => new UiTableRow
                {
                    Cells =
                    [
                        GetString(advantage, "advantageId"),
                        GetString(advantage, "displayName", GetString(advantage, "advantageId")),
                        GetString(advantage, "summary", "Без описания.")
                    ]
                })
                .ToList()
        };

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

    private static UiSelectionOption Option(string value, string label, string description) =>
        new()
        {
            Value = value,
            Label = label,
            Description = description
        };
}
