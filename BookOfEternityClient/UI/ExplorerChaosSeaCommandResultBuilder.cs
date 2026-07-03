using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

public static class ExplorerChaosSeaCommandResultBuilder
{
    private const string SoulStatePath = "game_state/meta/soul_state.json";
    private const string GuardiansPath = "game_state/meta/guardians.json";
    private const string AbodePowerJournalPath = "game_state/meta/abode_power_journal.json";

    private enum CommandKind
    {
        Overview,
        Guardians,
        AbodePower,
        GuardianProjects,
        GuardianPolitics,
        Abodes
    }

    private static readonly IReadOnlyDictionary<string, CommandKind> CommandKinds =
        new Dictionary<string, CommandKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["/chaos_sea"] = CommandKind.Overview,
            ["/море_хаоса"] = CommandKind.Overview,
            ["/guardians"] = CommandKind.Guardians,
            ["/хранители"] = CommandKind.Guardians,
            ["/abode_power"] = CommandKind.AbodePower,
            ["/сила_обители"] = CommandKind.AbodePower,
            ["/guardian_projects"] = CommandKind.GuardianProjects,
            ["/проекты_хранителей"] = CommandKind.GuardianProjects,
            ["/guardian_politics"] = CommandKind.GuardianPolitics,
            ["/политика_хранителей"] = CommandKind.GuardianPolitics,
            ["/abodes"] = CommandKind.Abodes,
            ["/обители"] = CommandKind.Abodes
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

        return kind switch
        {
            CommandKind.Overview => await BuildOverview(request.Command, fs, stateManager),
            CommandKind.Guardians => await BuildGuardians(request, fs, includeAdvancedDiagnostics),
            CommandKind.AbodePower => await BuildAbodePower(request, fs, includeAdvancedDiagnostics),
            CommandKind.GuardianProjects => await BuildGuardianProjects(request, fs, includeAdvancedDiagnostics),
            CommandKind.GuardianPolitics => await BuildGuardianPolitics(
                request.Command,
                fs,
                includeAdvancedDiagnostics || stateManager.Settings.ShowGmThoughts),
            CommandKind.Abodes => await BuildAbodes(request, fs, includeAdvancedDiagnostics),
            _ => null
        };
    }

    private static async Task<ExplorerCommandResult> BuildOverview(string command, FileSystemManager fs, StateManager stateManager)
    {
        var soul = await ReadJson(fs, SoulStatePath);
        var guardians = await ReadJson(fs, GuardiansPath);
        var offering = await ReadJson(fs, GuardianAbodeOfferingState.PendingRequestPath);
        var foundation = await ReadJson(fs, PlayerGuardianFoundationState.PendingRequestPath);

        var blocks = new List<UiBlock>
        {
            Panel("Море Хаоса",
                Grid(
                    ("Фаза", EmptyFallback(stateManager.CurrentState.CurrentRealm)),
                    ("Душа", EmptyFallback(stateManager.CurrentState.SoulName)),
                    ("Чернильные Перья", stateManager.CurrentState.InkFeathers.ToString()),
                    ("Просветление", EmptyFallback(stateManager.CurrentState.EnlightenmentTier)),
                    ("Активный Хранитель", DescribeActiveGuardian(guardians.Node)),
                    ("Текущая Обитель", DescribeCurrentAbode(guardians.Node)),
                    ("Ожидает подношение", DescribePresence(offering)),
                    ("Ожидает основание мантии", DescribePresence(foundation))))
        };

        AddRawOrWarning(blocks, "JSON: soul_state", soul);
        AddRawOrWarning(blocks, "JSON: guardians", guardians);
        AddRawOrWarning(blocks, "JSON: pending_abode_offering", offering);
        AddRawOrWarning(blocks, "JSON: pending_player_guardian_foundation", foundation);
        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildGuardians(
        CommandRequest request,
        FileSystemManager fs,
        bool includeAdvancedDiagnostics)
    {
        var read = await ReadJson(fs, GuardiansPath);
        if (read.Node == null)
            return DetailUnavailable(request.Command, "Хранители", "не удалось открыть сведения о Хранителях.");

        var detail = ParseDetailRequest(request.Arguments, "хранитель", "guardian");
        var guardians = EnumerateGuardians(read.Node).ToList();
        if (!string.IsNullOrWhiteSpace(detail.Selector))
            return BuildGuardianDetail(request.Command, guardians, detail.Selector);

        var blocks = new List<UiBlock>
        {
            BuildGuardiansOverviewDossier(read.Node, guardians)
        };

        if (includeAdvancedDiagnostics)
            AddRawOrWarning(blocks, $"Полный JSON {GuardiansPath}", read);

        return Completed(request.Command, blocks, BuildGuardianActions(guardians));
    }

    private static UiEntityDossierBlock BuildGuardiansOverviewDossier(JsonNode? root, IReadOnlyList<JsonObject> guardians) =>
        new()
        {
            EntityType = "chaos-sea-guardians",
            Title = "Хранители",
            Subtitle = "Море Хаоса",
            Summary = "Известные душе Хранители, их сферы влияния, Обители и текущее отношение.",
            Facts =
            [
                new UiEntityFact { Label = "Хранителей", Value = guardians.Count.ToString() },
                new UiEntityFact { Label = "Активный Хранитель", Value = DescribeActiveGuardian(root) },
                new UiEntityFact { Label = "Текущая Обитель", Value = DescribeCurrentAbode(root) },
                new UiEntityFact { Label = "Ожидаемое создание Хранителя", Value = DescribeNodePresence(root?["pendingGuardianCreation"]) }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "chaos-sea-guardians-list",
                    Title = "Известные Хранители",
                    Summary = guardians.Count == 0
                        ? "Душа пока не знает ни одного Хранителя."
                        : "Карточки Хранителей открывают подробное досье без повторного ввода команды.",
                    Icon = "shield",
                    Presentation = "cards",
                    CollectionLabel = guardians.Count == 1 ? "1 Хранитель" : $"{guardians.Count} Хранителей",
                    Collapsible = guardians.Count > 4,
                    InitiallyExpanded = true,
                    Cards = guardians.Select(BuildGuardianOverviewCard).ToList()
                }
            ]
        };

    private static UiEntityCard BuildGuardianOverviewCard(JsonObject guardian)
    {
        var selector = GuardianSelector(guardian);
        var name = GuardianName(guardian);
        var domain = EmptyFallback(GetString(guardian, "domain", "sphere", "mantleName"));
        var abode = AbodeName(guardian["abode"] as JsonObject);

        return new UiEntityCard
        {
            Title = name,
            Subtitle = domain,
            Summary = string.IsNullOrWhiteSpace(GetString(guardian, "description", "summary"))
                ? "Хранитель Моря Хаоса."
                : GetString(guardian, "description", "summary"),
            Icon = "shield",
            Facts =
            [
                new UiEntityFact { Label = "Сфера", Value = domain },
                new UiEntityFact { Label = "Обитель", Value = abode },
                new UiEntityFact { Label = "Отношение", Value = DescribeReputation(guardian) }
            ],
            PrimaryAction = string.IsNullOrWhiteSpace(selector)
                ? null
                : DetailAction(
                    "guardians-detail-" + ToActionIdPart(selector),
                    $"Подробно: {name}",
                    BuildGuardianDetailCommand(selector))
        };
    }

    private static async Task<ExplorerCommandResult> BuildAbodePower(
        CommandRequest request,
        FileSystemManager fs,
        bool includeAdvancedDiagnostics)
    {
        var guardians = await ReadJson(fs, GuardiansPath);
        var journal = await ReadJson(fs, AbodePowerJournalPath);
        var entries = EnumerateAbodePowerEntries(guardians.Node, journal.Node).ToList();

        var detail = ParseDetailRequest(request.Arguments, "запись", "entry", "event", "событие");
        if (!string.IsNullOrWhiteSpace(detail.Selector))
            return BuildAbodePowerDetail(request.Command, entries, detail.Selector);

        var blocks = new List<UiBlock>
        {
            Panel("Сила Обители",
                Grid(
                    ("Хранителей с данными", CountGuardiansWithObject(guardians.Node, "abodePower").ToString()),
                    ("Событий силы", entries.Count.ToString()),
                    ("Журнал", DescribePresence(journal))))
        };

        if (entries.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Журнал силы Обители",
                Columns = ["Запись", "Хранитель", "Изменение", "Ход", "Подробно"],
                Rows = entries.Select(static entry => new UiTableRow
                {
                    Cells =
                    [
                        entry.Title,
                        EmptyFallback(entry.GuardianId),
                        FormatSigned(entry.Delta),
                        EmptyFallback(entry.Turn),
                        BuildAbodePowerDetailCommand(entry.Selector)
                    ]
                }).ToList()
            });
        }

        if (includeAdvancedDiagnostics)
        {
            AddRawOrWarning(blocks, $"Полный JSON {GuardiansPath}", guardians);
            AddRawOrWarning(blocks, $"Полный JSON {AbodePowerJournalPath}", journal);
        }

        return Completed(request.Command, blocks, BuildAbodePowerActions(entries));
    }

    private static async Task<ExplorerCommandResult> BuildAbodes(
        CommandRequest request,
        FileSystemManager fs,
        bool includeAdvancedDiagnostics)
    {
        var read = await ReadJson(fs, GuardiansPath);
        if (read.Node == null)
            return DetailUnavailable(request.Command, "Обители", "не удалось открыть сведения об Обителях.");

        var detail = ParseDetailRequest(request.Arguments, "обитель", "abode");
        var abodes = EnumerateAbodes(read.Node).ToList();
        if (!string.IsNullOrWhiteSpace(detail.Selector))
            return BuildAbodeDetail(request.Command, abodes, detail.Selector);

        var blocks = new List<UiBlock>
        {
            Panel("Обители Моря Хаоса",
                Grid(
                    ("Текущая Обитель", DescribeCurrentAbode(read.Node)),
                    ("Известных Обителей", CountKnownAbodes(read.Node).ToString()),
                    ("Хранителей с Обителью", CountGuardiansWithObject(read.Node, "abode").ToString())))
        };

        if (abodes.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Обзор Обителей",
                Columns = ["Обитель", "Хранитель", "Якорь", "Подробно"],
                Rows = abodes.Select(static abode => new UiTableRow
                {
                    Cells =
                    [
                        abode.Name,
                        EmptyFallback(abode.GuardianName),
                        EmptyFallback(abode.Anchor),
                        BuildAbodeDetailCommand(abode.Selector)
                    ]
                }).ToList()
            });
        }

        if (includeAdvancedDiagnostics)
            AddRawOrWarning(blocks, $"Полный JSON {GuardiansPath}", read);

        return Completed(request.Command, blocks, BuildAbodeActions(abodes));
    }

    private static async Task<ExplorerCommandResult> BuildGuardianProjects(
        CommandRequest request,
        FileSystemManager fs,
        bool includeAdvancedDiagnostics)
    {
        var tracker = await ReadJson(fs, GuardianProjectState.TrackerPath);
        var guardians = await ReadJson(fs, GuardiansPath);
        var journal = await ReadJson(fs, GuardianProjectState.JournalPath);
        var projects = EnumerateGuardianProjects(tracker.Node, guardians.Node, journal.Node).ToList();

        var detail = ParseDetailRequest(request.Arguments, "проект", "project");
        if (!string.IsNullOrWhiteSpace(detail.Selector))
            return BuildGuardianProjectDetail(request.Command, projects, detail.Selector);

        var blocks = new List<UiBlock>
        {
            Panel("Проекты Хранителей",
                Grid(
                    ("Проектов", projects.Count.ToString()),
                    ("Хранителей", CountArray(guardians.Node, "guardians").ToString()),
                    ("Журнал", DescribePresence(journal))))
        };

        if (projects.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Обзор проектов Хранителей",
                Columns = ["Проект", "Хранитель", "Статус", "Прогресс", "Подробно"],
                Rows = projects.Select(static project => new UiTableRow
                {
                    Cells =
                    [
                        project.Title,
                        EmptyFallback(project.GuardianName),
                        EmptyFallback(project.State),
                        FormatProgress(project.WorkDone, project.TotalWork),
                        BuildGuardianProjectDetailCommand(project.Selector)
                    ]
                }).ToList()
            });
        }

        if (includeAdvancedDiagnostics)
        {
            AddRawOrWarning(blocks, $"Полный JSON {GuardianProjectState.TrackerPath}", tracker);
            AddRawOrWarning(blocks, $"Полный JSON {GuardiansPath}", guardians);
            AddRawOrWarning(blocks, $"Полный JSON {GuardianProjectState.JournalPath}", journal);
        }

        return Completed(request.Command, blocks, BuildGuardianProjectActions(projects));
    }

    private static async Task<ExplorerCommandResult> BuildGuardianPolitics(
        string command,
        FileSystemManager fs,
        bool includeAdvancedDiagnostics)
    {
        var politics = await ReadJson(fs, ChaosSeaGuardianPoliticsState.StatePath);
        if (politics.Node == null)
        {
            var message = politics.FileExists
                ? "Состояние политики Хранителей сейчас не читается. Откройте раздел позже, когда Хранители прояснят свои связи и решения."
                : "Пока нет открытых политических записей Хранителей. Политика появится здесь после сцен, проектов или решений, которые раскроют связи между Хранителями.";
            return Completed(command,
                [
                    Message(
                    politics.FileExists ? UiNotificationSeverity.Warning : UiNotificationSeverity.Info,
                    "Политика Хранителей",
                    message)
                ],
                BuildOverviewAction("guardian-politics-to-guardians", "К Хранителям", "/хранители"));
        }

        var guardians = await ReadJson(fs, GuardiansPath);
        var labels = BuildGuardianPoliticsLabels(guardians.Node);
        var root = politics.Node;
        var relations = VisibleObjects(root[ChaosSeaGuardianPoliticsState.RelationsProperty] as JsonArray).ToList();
        var projects = VisibleObjects(root[ChaosSeaGuardianPoliticsState.ProjectsProperty] as JsonArray).ToList();
        var zones = VisibleObjects(root[ChaosSeaGuardianPoliticsState.InfluenceZonesProperty] as JsonArray).ToList();
        var chronicle = VisibleObjects(root[ChaosSeaGuardianPoliticsState.ChronicleProperty] as JsonArray).ToList();
        var hiddenCount =
            HiddenCount(root[ChaosSeaGuardianPoliticsState.RelationsProperty] as JsonArray) +
            HiddenCount(root[ChaosSeaGuardianPoliticsState.ProjectsProperty] as JsonArray) +
            HiddenCount(root[ChaosSeaGuardianPoliticsState.InfluenceZonesProperty] as JsonArray) +
            HiddenCount(root[ChaosSeaGuardianPoliticsState.ChronicleProperty] as JsonArray);

        var blocks = new List<UiBlock>
        {
            Panel("Политика Хранителей",
                Grid(
                    ("Видимых связей", relations.Count.ToString()),
                    ("Видимых проектов", projects.Count.ToString()),
                    ("Видимых зон влияния", zones.Count.ToString()),
                    ("Видимых записей хроники", chronicle.Count.ToString()),
                    ("Скрытых записей", hiddenCount.ToString())))
        };

        if (relations.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Связи Хранителей",
                Columns = ["Источник", "Цель", "Тип", "Отношение", "Причина"],
                Rows = relations.Select(relation => new UiTableRow
                {
                    Cells =
                    [
                        ResolveGuardianLabel(GetString(relation, "sourceGuardianId"), labels),
                        ResolveGuardianLabel(GetString(relation, "targetGuardianId"), labels),
                        TranslateRelationType(GetString(relation, "relationType")),
                        GetNumberOrString(relation, "attitudeScore", "0"),
                        GetString(relation, "reason")
                    ]
                }).ToList()
            });
        }

        if (projects.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Политические проекты",
                Columns = ["Проект", "Владелец", "Цель", "Статус", "Прогресс", "Сводка"],
                Rows = projects.Select(project => new UiTableRow
                {
                    Cells =
                    [
                        FirstNonEmpty(GetString(project, "title", "displayName", "name"), "Политический проект"),
                        ResolveGuardianLabel(GetString(project, "ownerGuardianId"), labels),
                        ResolveGuardianLabel(GetString(project, "targetGuardianId"), labels),
                        TranslateProjectStatus(GetString(project, "status")),
                        $"{GetNumberOrString(project, "currentProgress", "0")}/{GetNumberOrString(project, "requiredProgress", "0")}",
                        GetString(project, "summary")
                    ]
                }).ToList()
            });
        }

        if (zones.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Зоны влияния",
                Columns = ["Зона", "Хранитель", "Область", "Влияние", "Контроль"],
                Rows = zones.Select(zone => new UiTableRow
                {
                    Cells =
                    [
                        GetString(zone, "displayName", "zoneId"),
                        ResolveGuardianLabel(GetString(zone, "guardianId"), labels),
                        ResolveGuardianPoliticsScope(zone, labels),
                        GetNumberOrString(zone, "influenceValue", "0"),
                        GetNumberOrString(zone, "controlLevel", "0")
                    ]
                }).ToList()
            });
        }

        if (chronicle.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Хроника политики",
                Columns = ["Ход", "Событие", "Сводка"],
                Rows = chronicle.Select(static entry => new UiTableRow
                {
                    Cells =
                    [
                        GetNumberOrString(entry, "turnNumber", "0"),
                        GetString(entry, "eventType"),
                        GetString(entry, "summary")
                    ]
                }).ToList()
            });
        }

        if (blocks.Count == 1)
            blocks.Add(Message(UiNotificationSeverity.Info, "Нет видимых политических событий", "Скрытые связи не показываются игроку до раскрытия через сцену, хронику или прямое свидетельство."));

        if (includeAdvancedDiagnostics)
            blocks.Add(Raw($"Полный JSON {ChaosSeaGuardianPoliticsState.StatePath}", root));

        return Completed(command, blocks);
    }

    private static ExplorerCommandResult BuildGuardianDetail(
        string command,
        IReadOnlyList<JsonObject> guardians,
        string selector)
    {
        var guardian = FindGuardian(guardians, selector);
        if (guardian == null)
            return DetailUnavailable(command, "Хранитель недоступен", $"не удалось открыть Хранителя «{selector}». Запись не найдена или ещё не раскрыта душой.");

        var relationship = guardian["relationshipData"];
        var abode = guardian["abode"] as JsonObject;
        var description = GetString(guardian, "description", "summary");
        var blocks = new List<UiBlock>
        {
            Panel($"Хранитель: {GuardianName(guardian)}",
                Grid(
                    ("Сфера", EmptyFallback(GetString(guardian, "domain", "sphere", "mantleName"))),
                    ("Обитель", AbodeName(abode)),
                    ("Репутация", DescribeReputation(guardian)),
                    ("Последняя встреча", EmptyFallback(GetString(relationship, "lastInteraction"))),
                    ("Сила Обители", DescribeAbodePower(guardian["abodePower"]))))
        };

        if (!string.IsNullOrWhiteSpace(description))
            blocks.Add(Message(UiNotificationSeverity.Info, "Образ Хранителя", description));

        AddGuardianQuestBlocks(blocks, guardian);
        AddGuardianLoreBlocks(blocks, guardian);
        return Completed(command, blocks, BuildOverviewAction("guardians-overview", "К обзору Хранителей", "/guardians"));
    }

    private static ExplorerCommandResult BuildAbodeDetail(
        string command,
        IReadOnlyList<AbodeSnapshot> abodes,
        string selector)
    {
        var abode = FindAbode(abodes, selector);
        if (abode == null)
            return DetailUnavailable(command, "Обитель недоступна", $"не удалось открыть Обитель «{selector}». Запись не найдена или ещё не раскрыта душой.");

        var blocks = new List<UiBlock>
        {
            Panel($"Обитель: {abode.Name}",
                Grid(
                    ("Хранитель", EmptyFallback(abode.GuardianName)),
                    ("Якорь", EmptyFallback(abode.Anchor)),
                    ("Сила", EmptyFallback(abode.Power))))
        };

        if (!string.IsNullOrWhiteSpace(abode.Description))
            blocks.Add(Message(UiNotificationSeverity.Info, "Облик Обители", abode.Description));

        return Completed(command, blocks, BuildOverviewAction("abodes-overview", "К обзору Обителей", "/abodes"));
    }

    private static ExplorerCommandResult BuildAbodePowerDetail(
        string command,
        IReadOnlyList<AbodePowerEntrySnapshot> entries,
        string selector)
    {
        var entry = FindAbodePowerEntry(entries, selector);
        if (entry == null)
            return DetailUnavailable(command, "Запись силы недоступна", $"не удалось открыть запись силы Обители «{selector}». Запись не найдена или ещё не раскрыта душой.");

        var blocks = new List<UiBlock>
        {
            Panel($"Сила Обители: {entry.Title}",
                Grid(
                    ("Хранитель", EmptyFallback(entry.GuardianId)),
                    ("Изменение", FormatSigned(entry.Delta)),
                    ("Причина", EmptyFallback(entry.Reason)),
                    ("Ход", EmptyFallback(entry.Turn))))
        };

        if (!string.IsNullOrWhiteSpace(entry.Summary))
            blocks.Add(Message(UiNotificationSeverity.Info, "След силы", entry.Summary));

        return Completed(command, blocks, BuildOverviewAction("abode-power-overview", "К обзору силы Обители", "/abode_power"));
    }

    private static ExplorerCommandResult BuildGuardianProjectDetail(
        string command,
        IReadOnlyList<GuardianProjectSnapshot> projects,
        string selector)
    {
        var project = FindGuardianProject(projects, selector);
        if (project == null)
            return DetailUnavailable(command, "Проект недоступен", $"не удалось открыть проект Хранителя «{selector}». Запись не найдена или ещё не раскрыта душой.");

        var blocks = new List<UiBlock>
        {
            Panel($"Проект Хранителя: {project.Title}",
                Grid(
                    ("Хранитель", EmptyFallback(project.GuardianName)),
                    ("Тип", EmptyFallback(project.Type)),
                    ("Ранг", EmptyFallback(project.Tier)),
                    ("Режим", EmptyFallback(project.Mode)),
                    ("Состояние", EmptyFallback(project.State)),
                    ("Прогресс", FormatProgress(project.WorkDone, project.TotalWork)),
                    ("Давление", EmptyFallback(project.Pressure)),
                    ("Устойчивость", EmptyFallback(project.Stability))))
        };

        if (!string.IsNullOrWhiteSpace(project.Description))
            blocks.Add(Message(UiNotificationSeverity.Info, "Замысел проекта", project.Description));

        if (project.Effects.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Следствия проекта",
                Columns = ["Эффект"],
                Rows = project.Effects.Select(static effect => new UiTableRow { Cells = [effect] }).ToList()
            });
        }

        if (project.Journal.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Журнал проекта",
                Columns = ["Ход", "Запись", "Сводка"],
                Rows = project.Journal.Select(static entry => new UiTableRow
                {
                    Cells = [EmptyFallback(entry.Turn), entry.Title, entry.Summary]
                }).ToList()
            });
        }

        return Completed(command, blocks, BuildOverviewAction("guardian-projects-overview", "К обзору проектов", "/guardian_projects"));
    }

    private static IEnumerable<UiAction> BuildGuardianActions(IEnumerable<JsonObject> guardians)
    {
        foreach (var guardian in guardians)
        {
            var selector = GuardianSelector(guardian);
            if (string.IsNullOrWhiteSpace(selector))
                continue;

            yield return DetailAction(
                "guardians-detail-" + ToActionIdPart(selector),
                $"Подробно: {GuardianName(guardian)}",
                BuildGuardianDetailCommand(selector));
        }
    }

    private static IEnumerable<UiAction> BuildAbodeActions(IEnumerable<AbodeSnapshot> abodes)
    {
        foreach (var abode in abodes)
        {
            yield return DetailAction(
                "abodes-detail-" + ToActionIdPart(abode.Selector),
                $"Подробно: {abode.Name}",
                BuildAbodeDetailCommand(abode.Selector));
        }
    }

    private static IEnumerable<UiAction> BuildAbodePowerActions(IEnumerable<AbodePowerEntrySnapshot> entries)
    {
        foreach (var entry in entries)
        {
            yield return DetailAction(
                "abode-power-detail-" + ToActionIdPart(entry.Selector),
                $"Подробно: {entry.Title}",
                BuildAbodePowerDetailCommand(entry.Selector));
        }
    }

    private static IEnumerable<UiAction> BuildGuardianProjectActions(IEnumerable<GuardianProjectSnapshot> projects)
    {
        foreach (var project in projects)
        {
            yield return DetailAction(
                "guardian-projects-detail-" + ToActionIdPart(project.Selector),
                $"Подробно: {project.Title}",
                BuildGuardianProjectDetailCommand(project.Selector));
        }
    }

    private static IEnumerable<UiAction> BuildOverviewAction(string id, string label, string command)
    {
        yield return DetailAction(id, label, command);
    }

    private static UiAction DetailAction(string id, string label, string command) =>
        new()
        {
            Id = id,
            Label = label,
            Command = command,
            Style = UiActionStyle.Secondary,
            RequiresConfirmation = false
        };

    private static string BuildGuardianDetailCommand(string selector) => $"/guardians хранитель {selector}";

    private static string BuildAbodeDetailCommand(string selector) => $"/abodes обитель {selector}";

    private static string BuildAbodePowerDetailCommand(string selector) => $"/abode_power запись {selector}";

    private static string BuildGuardianProjectDetailCommand(string selector) => $"/guardian_projects проект {selector}";

    private static IEnumerable<JsonObject> EnumerateGuardians(JsonNode? root)
    {
        if (root is JsonArray rootArray)
        {
            foreach (var item in rootArray.OfType<JsonObject>())
                yield return item;
            yield break;
        }

        if (root?["guardians"] is not JsonArray guardians)
            yield break;

        foreach (var guardian in guardians.OfType<JsonObject>())
            yield return guardian;
    }

    private static JsonObject? FindGuardian(IEnumerable<JsonObject> guardians, string selector)
    {
        var normalized = NormalizeSelector(selector);
        return guardians.FirstOrDefault(guardian =>
            SelectorMatches(normalized, GuardianSelector(guardian), GuardianName(guardian), GetString(guardian, "canonicalName", "name", "guardianName")));
    }

    private static IEnumerable<AbodeSnapshot> EnumerateAbodes(JsonNode? root)
    {
        var guardians = EnumerateGuardians(root).ToList();
        var snapshots = new Dictionary<string, AbodeSnapshot>(StringComparer.OrdinalIgnoreCase);

        foreach (var guardian in guardians)
        {
            if (guardian["abode"] is not JsonObject abode)
                continue;

            var selector = AbodeSelector(abode);
            if (string.IsNullOrWhiteSpace(selector))
                continue;

            snapshots[selector] = new AbodeSnapshot(
                selector,
                AbodeName(abode),
                GuardianSelector(guardian),
                GuardianName(guardian),
                GetString(abode, "description", "summary"),
                GetString(abode, "currentAnchor", "anchor", "location"),
                DescribeAbodePower(guardian["abodePower"]));
        }

        if (root?["chaosSeaNavigation"]?["knownAbodes"] is JsonArray knownAbodes)
        {
            foreach (var known in knownAbodes.OfType<JsonObject>())
            {
                var selector = AbodeSelector(known);
                if (string.IsNullOrWhiteSpace(selector) || snapshots.ContainsKey(selector))
                    continue;

                var guardianId = GetString(known, "guardianId");
                var guardian = FindGuardian(guardians, guardianId);
                snapshots[selector] = new AbodeSnapshot(
                    selector,
                    AbodeName(known),
                    guardianId,
                    guardian == null ? guardianId : GuardianName(guardian),
                    GetString(known, "description", "summary"),
                    GetString(known, "currentAnchor", "anchor", "location"),
                    guardian == null ? string.Empty : DescribeAbodePower(guardian["abodePower"]));
            }
        }

        return snapshots.Values;
    }

    private static AbodeSnapshot? FindAbode(IEnumerable<AbodeSnapshot> abodes, string selector)
    {
        var normalized = NormalizeSelector(selector);
        return abodes.FirstOrDefault(abode =>
            SelectorMatches(normalized, abode.Selector, abode.Name, abode.GuardianId, abode.GuardianName));
    }

    private static IEnumerable<AbodePowerEntrySnapshot> EnumerateAbodePowerEntries(JsonNode? guardiansRoot, JsonNode? journalRoot)
    {
        var snapshots = new Dictionary<string, AbodePowerEntrySnapshot>(StringComparer.OrdinalIgnoreCase);

        AddPowerEntriesFromArray(journalRoot?["entries"] as JsonArray, snapshots);
        AddPowerEntriesFromArray(journalRoot?["guardianPowerEvents"] as JsonArray, snapshots);

        foreach (var guardian in EnumerateGuardians(guardiansRoot))
        {
            if (guardian["abodePower"]?["history"] is not JsonArray history)
                continue;

            foreach (var historyEntry in history.OfType<JsonObject>())
            {
                var selector = PowerEntrySelector(historyEntry);
                if (string.IsNullOrWhiteSpace(selector) || snapshots.ContainsKey(selector))
                    continue;

                snapshots[selector] = BuildPowerEntrySnapshot(historyEntry, GuardianSelector(guardian));
            }
        }

        return snapshots.Values;
    }

    private static void AddPowerEntriesFromArray(JsonArray? entries, Dictionary<string, AbodePowerEntrySnapshot> snapshots)
    {
        if (entries == null)
            return;

        foreach (var entry in entries.OfType<JsonObject>())
        {
            var selector = PowerEntrySelector(entry);
            if (string.IsNullOrWhiteSpace(selector) || snapshots.ContainsKey(selector))
                continue;

            snapshots[selector] = BuildPowerEntrySnapshot(entry, GetString(entry, "guardianId", "ownerGuardianId"));
        }
    }

    private static AbodePowerEntrySnapshot BuildPowerEntrySnapshot(JsonObject entry, string guardianId)
    {
        var selector = PowerEntrySelector(entry);
        var title = GetString(entry, "title", "reason", "eventType", "eventId", "entryId");
        return new AbodePowerEntrySnapshot(
            selector,
            EmptyFallback(title),
            guardianId,
            GetString(entry, "summary", "description"),
            GetString(entry, "reason", "reasonType", "source"),
            FirstNumberOrString(entry, "delta", "change", "powerDelta"),
            FirstNumberOrString(entry, "turn", "turnNumber"));
    }

    private static AbodePowerEntrySnapshot? FindAbodePowerEntry(IEnumerable<AbodePowerEntrySnapshot> entries, string selector)
    {
        var normalized = NormalizeSelector(selector);
        return entries.FirstOrDefault(entry =>
            SelectorMatches(normalized, entry.Selector, entry.Title));
    }

    private static IEnumerable<GuardianProjectSnapshot> EnumerateGuardianProjects(
        JsonNode? trackerRoot,
        JsonNode? guardiansRoot,
        JsonNode? journalRoot)
    {
        var guardianNames = EnumerateGuardians(guardiansRoot)
            .Select(static guardian => new { Id = GuardianSelector(guardian), Name = GuardianName(guardian) })
            .Where(static item => !string.IsNullOrWhiteSpace(item.Id))
            .ToDictionary(static item => item.Id, static item => item.Name, StringComparer.OrdinalIgnoreCase);
        var journal = EnumerateProjectJournalEntries(trackerRoot, journalRoot).ToList();
        var emittedSelectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in ProjectArrays(trackerRoot).SelectMany(static array => array.OfType<JsonObject>()))
        {
            var project = item["project"] as JsonObject ?? item;
            var guardianId = GetString(item, "guardianId", "ownerGuardianId");
            if (string.IsNullOrWhiteSpace(guardianId))
                guardianId = GetString(project, "guardianId", "ownerGuardianId");

            var projectId = GetString(project, "projectId", "id");
            if (string.IsNullOrWhiteSpace(projectId))
                continue;

            var selector = string.IsNullOrWhiteSpace(guardianId) ? projectId : $"{guardianId}::{projectId}";
            if (!emittedSelectors.Add(selector))
                continue;

            var relatedJournal = journal
                .Where(entry =>
                    (string.IsNullOrWhiteSpace(guardianId) || string.Equals(entry.GuardianId, guardianId, StringComparison.OrdinalIgnoreCase)) &&
                    string.Equals(entry.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            yield return new GuardianProjectSnapshot(
                selector,
                guardianId,
                guardianNames.TryGetValue(guardianId, out var guardianName) ? guardianName : guardianId,
                projectId,
                EmptyFallback(GetString(project, "projectName", "name", "title", "displayName", "projectId")),
                GetString(project, "projectType", "type"),
                GetString(project, "projectTier", "tier"),
                GetString(project, "projectMode", "mode"),
                GetString(project, "activeState", "status", "state"),
                GetString(project, "description", "summary"),
                FirstNumberOrString(project, "workDone", "currentProgress", "progress"),
                FirstNumberOrString(project, "totalWork", "requiredProgress", "targetProgress"),
                FirstNumberOrString(project, "pressure"),
                FirstNumberOrString(project, "stability"),
                ReadStringArray(project["systemEffectSummary"]),
                relatedJournal);
        }

        foreach (var entry in journal)
        {
            if (string.IsNullOrWhiteSpace(entry.ProjectId))
                continue;

            var selector = string.IsNullOrWhiteSpace(entry.GuardianId)
                ? entry.ProjectId
                : $"{entry.GuardianId}::{entry.ProjectId}";
            if (!emittedSelectors.Add(selector))
                continue;

            var relatedJournal = journal
                .Where(other =>
                    string.Equals(other.ProjectId, entry.ProjectId, StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(entry.GuardianId) ||
                     string.Equals(other.GuardianId, entry.GuardianId, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            yield return new GuardianProjectSnapshot(
                selector,
                entry.GuardianId,
                guardianNames.TryGetValue(entry.GuardianId, out var guardianName) ? guardianName : entry.GuardianId,
                entry.ProjectId,
                EmptyFallback(entry.Title),
                "journal",
                EmptyFallback(entry.Visibility),
                EmptyFallback(entry.EventType),
                "journaled",
                EmptyFallback(entry.Summary),
                "",
                "",
                "",
                "",
                [],
                relatedJournal);
        }
    }

    private static IEnumerable<JsonArray> ProjectArrays(JsonNode? trackerRoot)
    {
        if (trackerRoot is JsonArray rootArray)
            yield return rootArray;
        if (trackerRoot?["projects"] is JsonArray projects)
            yield return projects;
        if (trackerRoot?["activeProjects"] is JsonArray activeProjects)
            yield return activeProjects;
        if (trackerRoot?["completedProjects"] is JsonArray completedProjects)
            yield return completedProjects;
    }

    private static IEnumerable<GuardianProjectJournalEntry> EnumerateProjectJournalEntries(JsonNode? trackerRoot, JsonNode? journalRoot)
    {
        foreach (var entries in new[] { trackerRoot?["journal"] as JsonArray, journalRoot?["entries"] as JsonArray, journalRoot?["journal"] as JsonArray })
        {
            if (entries == null)
                continue;

            foreach (var entry in entries.OfType<JsonObject>())
            {
                yield return new GuardianProjectJournalEntry(
                    GetString(entry, "guardianId", "ownerGuardianId"),
                    GetString(entry, "projectId"),
                    FirstNumberOrString(entry, "turn", "turnNumber"),
                    GetString(entry, "eventType"),
                    GetString(entry, "visibility"),
                    EmptyFallback(GetString(entry, "title", "entryId")),
                    EmptyFallback(GetString(entry, "summary", "description")));
            }
        }
    }

    private static GuardianProjectSnapshot? FindGuardianProject(IEnumerable<GuardianProjectSnapshot> projects, string selector)
    {
        var normalized = NormalizeSelector(selector);
        return projects.FirstOrDefault(project =>
            SelectorMatches(normalized, project.Selector, project.ProjectId, project.Title));
    }

    private static void AddGuardianQuestBlocks(List<UiBlock> blocks, JsonObject guardian)
    {
        if (guardian["questManagement"]?["activeQuests"] is not JsonArray quests)
            return;

        var rows = quests.OfType<JsonObject>()
            .Select(static quest => new UiTableRow
            {
                Cells =
                [
                    EmptyFallback(GetString(quest, "name", "title", "questId")),
                    EmptyFallback(GetString(quest, "status")),
                    EmptyFallback(GetString(quest, "description", "summary"))
                ]
            })
            .ToList();
        if (rows.Count == 0)
            return;

        blocks.Add(new UiTableBlock
        {
            Title = "Поручения Хранителя",
            Columns = ["Поручение", "Статус", "Суть"],
            Rows = rows
        });
    }

    private static void AddGuardianLoreBlocks(List<UiBlock> blocks, JsonObject guardian)
    {
        if (guardian["loreFragments"] is not JsonArray loreFragments)
            return;

        var rows = loreFragments.OfType<JsonObject>()
            .Where(static fragment => IsVisibleLore(fragment))
            .Select(static fragment => new UiTableRow
            {
                Cells =
                [
                    EmptyFallback(GetString(fragment, "title", "fragmentId")),
                    EmptyFallback(GetString(fragment, "content", "summary"))
                ]
            })
            .ToList();
        if (rows.Count == 0)
            return;

        blocks.Add(new UiTableBlock
        {
            Title = "Открытые предания",
            Columns = ["Фрагмент", "Текст"],
            Rows = rows
        });
    }

    private static bool IsVisibleLore(JsonObject fragment)
    {
        if (fragment["isUnlocked"] is JsonValue unlocked && unlocked.TryGetValue<bool>(out var isUnlocked))
            return isUnlocked;
        if (fragment["isPlayerVisible"] is JsonValue visible && visible.TryGetValue<bool>(out var isVisible))
            return isVisible;
        return true;
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
            return new DetailRequest(first, rest);

        return new DetailRequest(string.Empty, normalized);
    }

    private static string GuardianSelector(JsonObject guardian) =>
        GetString(guardian, "guardianId", "id", "key", "canonicalName", "guardianName", "name");

    private static string GuardianName(JsonObject guardian) =>
        EmptyFallback(GetString(guardian, "canonicalName", "guardianName", "name", "displayName", "guardianId"));

    private static string AbodeSelector(JsonObject? abode) =>
        GetString(abode, "abodeId", "id", "key", "name");

    private static string AbodeName(JsonObject? abode) =>
        EmptyFallback(GetString(abode, "name", "abodeName", "displayName", "abodeId"));

    private static GuardianPoliticsLabels BuildGuardianPoliticsLabels(JsonNode? guardiansRoot)
    {
        var guardianLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var abodeLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var guardian in EnumerateGuardians(guardiansRoot))
        {
            var guardianId = GuardianSelector(guardian);
            var guardianName = GuardianName(guardian);
            if (!string.IsNullOrWhiteSpace(guardianId) && !string.IsNullOrWhiteSpace(guardianName))
                guardianLabels[guardianId] = guardianName;

            if (guardian["abode"] is not JsonObject abode)
                continue;

            var abodeId = AbodeSelector(abode);
            var abodeName = AbodeName(abode);
            if (!string.IsNullOrWhiteSpace(abodeId) && !string.IsNullOrWhiteSpace(abodeName))
                abodeLabels[abodeId] = abodeName;
        }

        return new GuardianPoliticsLabels(guardianLabels, abodeLabels);
    }

    private static string ResolveGuardianLabel(string guardianId, GuardianPoliticsLabels labels)
    {
        if (string.IsNullOrWhiteSpace(guardianId))
            return "не указано";

        if (labels.Guardians.TryGetValue(guardianId, out var label) && !string.IsNullOrWhiteSpace(label))
            return label;

        return IsLikelyTechnicalIdentifier(guardianId) ? "Хранитель" : EmptyFallback(guardianId);
    }

    private static string ResolveGuardianPoliticsScope(JsonObject zone, GuardianPoliticsLabels labels)
    {
        var displayName = GetString(zone, "displayName", "zoneName", "name");
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName;

        var scopeType = GetString(zone, "scopeType", "type");
        var scopeId = GetString(zone, "scopeId", "abodeId", "locationId", "id");
        if (scopeType.Equals("abode", StringComparison.OrdinalIgnoreCase) &&
            labels.Abodes.TryGetValue(scopeId, out var abodeName) &&
            !string.IsNullOrWhiteSpace(abodeName))
        {
            return abodeName;
        }

        var typeLabel = DescribeGuardianPoliticsScopeType(scopeType);
        if (IsLikelyTechnicalIdentifier(scopeId))
            return typeLabel;

        return JoinNonEmpty(": ", typeLabel, scopeId);
    }

    private static string DescribeGuardianPoliticsScopeType(string scopeType) =>
        scopeType.Trim().ToLowerInvariant() switch
        {
            "" => "область влияния",
            "abode" => "Обитель",
            "route" => "маршрут",
            "guardian" => "Хранитель",
            "region" => "область",
            _ => scopeType.Replace('_', ' ')
        };

    private static bool IsLikelyTechnicalIdentifier(string value)
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

    private static string PowerEntrySelector(JsonObject entry) =>
        GetString(entry, "entryId", "eventId", "id", "title", "reason");

    private static string DescribeReputation(JsonObject guardian)
    {
        var reputation = FirstNumberOrString(guardian["relationshipData"], "currentReputation", "reputation");
        return string.IsNullOrWhiteSpace(reputation) ? "не указано" : reputation;
    }

    private static string DescribeAbodePower(JsonNode? abodePower)
    {
        if (abodePower is not JsonObject power)
            return "не указано";

        var current = FirstNumberOrString(power, "currentPower", "current");
        var max = FirstNumberOrString(power, "maxPower", "max");
        var tier = GetString(power, "tier", "level");
        var value = string.IsNullOrWhiteSpace(current) && string.IsNullOrWhiteSpace(max)
            ? string.Empty
            : $"{EmptyFallback(current)} / {EmptyFallback(max)}";
        if (!string.IsNullOrWhiteSpace(tier))
            value = string.IsNullOrWhiteSpace(value) ? tier : $"{value}, {tier}";
        return EmptyFallback(value);
    }

    private static string FirstNumberOrString(JsonNode? node, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = GetNumberOrString(node, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonNode? node)
    {
        if (node is JsonArray array)
            return array
                .Select(static item => TryGetScalarString(item, out var value) ? value : string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToList();

        if (TryGetScalarString(node, out var text) && !string.IsNullOrWhiteSpace(text))
            return [text];

        return [];
    }

    private static bool SelectorMatches(string normalizedSelector, params string[] candidates) =>
        candidates.Any(candidate => string.Equals(normalizedSelector, NormalizeSelector(candidate), StringComparison.OrdinalIgnoreCase));

    private static string NormalizeSelector(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string FormatSigned(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "не указано";
        return value.StartsWith("-", StringComparison.Ordinal) || value.StartsWith("+", StringComparison.Ordinal)
            ? value
            : "+" + value;
    }

    private static string FormatProgress(string workDone, string totalWork)
    {
        if (!string.IsNullOrWhiteSpace(workDone) && !string.IsNullOrWhiteSpace(totalWork))
            return $"{workDone}/{totalWork}";
        if (!string.IsNullOrWhiteSpace(workDone))
            return workDone;
        return "не указано";
    }

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

    private static ExplorerCommandResult DetailUnavailable(string command, string title, string message) =>
        Completed(command, Message(UiNotificationSeverity.Info, title, message));

    private static async Task<ExplorerCommandResult> BuildBundle(string command, FileSystemManager fs, string title, IReadOnlyList<SummarySpec> specs)
    {
        var grouped = specs.GroupBy(static spec => spec.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        var reads = new Dictionary<string, JsonReadResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in grouped)
            reads[group.Key] = await ReadJson(fs, group.Key);

        var rows = new List<UiTableRow>();
        foreach (var spec in specs)
        {
            var read = reads[spec.Path];
            rows.Add(new UiTableRow
            {
                Cells =
                [
                    spec.Label,
                    spec.Path,
                    DescribeSpec(read, spec.PropertyName)
                ]
            });
        }

        var blocks = new List<UiBlock>
        {
            new UiTableBlock
            {
                Title = title,
                Columns = ["Раздел", "Файл", "Состояние"],
                Rows = rows
            }
        };

        foreach (var (path, read) in reads)
            AddRawOrWarning(blocks, $"Полный JSON {path}", read);

        return Completed(command, blocks);
    }

    private static string DescribeSpec(JsonReadResult read, string propertyName)
    {
        if (read.Node == null)
            return read.FileExists ? "повреждён" : "отсутствует";

        if (read.Node is JsonArray rootArray)
            return rootArray.Count.ToString();

        var node = read.Node is JsonObject obj ? obj[propertyName] : null;
        return node switch
        {
            JsonArray array => array.Count.ToString(),
            JsonObject nested => $"{nested.Count} полей",
            JsonValue value when TryGetScalarString(value, out var text) => EmptyFallback(text),
            _ => "не найдено"
        };
    }

    private static void AddRawOrWarning(List<UiBlock> blocks, string title, JsonReadResult read)
    {
        if (read.Node != null)
        {
            blocks.Add(Raw(title, read.Node));
            return;
        }

        if (read.FileExists)
            blocks.Add(Message(UiNotificationSeverity.Warning, title, $"Файл найден, но не разобран как JSON: {read.Path}. {read.Error}"));
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

    private static ExplorerCommandResult MissingOrMalformed(string command, string title, JsonReadResult read) =>
        Completed(command, Message(
            read.FileExists ? UiNotificationSeverity.Warning : UiNotificationSeverity.Info,
            title,
            read.FileExists
                ? $"Файл найден, но не разобран как JSON: {read.Path}. {read.Error}"
                : $"Файл отсутствует: {read.Path}."));

    private static ExplorerCommandResult Completed(string command, params UiBlock[] blocks) =>
        Completed(command, (IEnumerable<UiBlock>)blocks);

    private static ExplorerCommandResult Completed(string command, IEnumerable<UiBlock> blocks, IEnumerable<UiAction>? actions = null) =>
        new()
        {
            Command = command,
            State = CommandExecutionState.Completed,
            Blocks = blocks.ToList(),
            Actions = actions?.ToList() ?? []
        };

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

    private static string DescribeActiveGuardian(JsonNode? root)
    {
        var active = root?["activeGuardian"];
        var name = GetString(active, "guardianName", "name", "canonicalName");
        var id = GetString(active, "guardianId");
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id))
            return $"{name} ({id})";
        if (!string.IsNullOrWhiteSpace(name))
            return name;
        if (!string.IsNullOrWhiteSpace(id))
            return id;
        return "не указан";
    }

    private static string DescribeCurrentAbode(JsonNode? root)
    {
        var navigation = root?["chaosSeaNavigation"];
        var name = GetString(navigation, "currentAbodeName", "abodeName", "name");
        var id = GetString(navigation, "currentAbodeId", "abodeId");
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id))
            return $"{name} ({id})";
        if (!string.IsNullOrWhiteSpace(name))
            return name;
        if (!string.IsNullOrWhiteSpace(id))
            return id;
        return "не указана";
    }

    private static string DescribeInkFeathers(JsonNode? soulRoot)
    {
        var inkFeathers = soulRoot?["inkFeathers"];
        if (inkFeathers is JsonObject)
        {
            var current = GetNumberOrString(inkFeathers, "current");
            var total = GetNumberOrString(inkFeathers, "total");
            if (!string.IsNullOrWhiteSpace(current) || !string.IsNullOrWhiteSpace(total))
                return $"{EmptyFallback(current)} / всего {EmptyFallback(total)}";
        }

        return GetNumberOrString(soulRoot, "inkFeathers", "0");
    }

    private static string DescribePresence(JsonReadResult read)
    {
        if (read.Node != null)
            return "найдено";
        return read.FileExists ? "повреждено" : "отсутствует";
    }

    private static string DescribeNodePresence(JsonNode? node) =>
        node == null ? "отсутствует" : "найдено";

    private static int CountArray(JsonNode? root, string propertyName) =>
        root?[propertyName] is JsonArray array ? array.Count : 0;

    private static int CountKnownAbodes(JsonNode? root)
    {
        var navigation = root?["chaosSeaNavigation"];
        if (navigation?["knownAbodes"] is JsonArray knownAbodes)
            return knownAbodes.Count;
        if (navigation?["visitedAbodes"] is JsonArray visitedAbodes)
            return visitedAbodes.Count;
        return 0;
    }

    private static int CountGuardiansWithObject(JsonNode? root, string propertyName)
    {
        if (root?["guardians"] is not JsonArray guardians)
            return 0;

        return guardians
            .OfType<JsonObject>()
            .Count(guardian => guardian[propertyName] is JsonObject);
    }

    private static int CountGuardianNestedArray(JsonNode? root, string objectPropertyName, string arrayPropertyName)
    {
        if (root?["guardians"] is not JsonArray guardians)
            return 0;

        return guardians
            .OfType<JsonObject>()
            .Select(guardian => guardian[objectPropertyName]?[arrayPropertyName] as JsonArray)
            .Where(static array => array != null)
            .Sum(static array => array!.Count);
    }

    private static IEnumerable<JsonObject> VisibleObjects(JsonArray? array)
    {
        if (array == null)
            yield break;

        foreach (var item in array.OfType<JsonObject>())
        {
            if (!IsHidden(item))
                yield return item;
        }
    }

    private static int HiddenCount(JsonArray? array) =>
        array?.OfType<JsonObject>().Count(IsHidden) ?? 0;

    private static bool IsHidden(JsonObject item)
    {
        var visibility = GetString(item, "visibility");
        if (string.Equals(visibility, "hidden", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(visibility, "gm_only", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return item["isPlayerVisible"] is JsonValue playerVisible &&
               playerVisible.TryGetValue<bool>(out var isPlayerVisible) &&
               !isPlayerVisible;
    }

    private static string TranslateRelationType(string relationType) =>
        relationType.ToLowerInvariant() switch
        {
            "alliance" => "союз",
            "rivalry" => "соперничество",
            "debt" => "долг",
            "fear" => "страх",
            "patronage" => "покровительство",
            "memory_oath" => "клятва памяти",
            "trade" => "обмен",
            "hostility" => "вражда",
            "hidden_dependency" => "скрытая зависимость",
            _ => EmptyFallback(relationType)
        };

    private static string TranslateProjectStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "active" => "активен",
            "blocked" => "заблокирован",
            "completed" => "завершён",
            "failed" => "провален",
            "abandoned" => "оставлен",
            _ => EmptyFallback(status)
        };

    private static string GetString(JsonNode? node, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (node?[propertyName] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                return text.Trim();
        }

        return string.Empty;
    }

    private static string GetNumberOrString(JsonNode? node, string propertyName, string fallback = "")
    {
        if (node?[propertyName] is not JsonValue value)
            return fallback;

        if (value.TryGetValue<int>(out var intValue))
            return intValue.ToString();
        if (value.TryGetValue<long>(out var longValue))
            return longValue.ToString();
        if (value.TryGetValue<string>(out var text))
            return text.Trim();
        return fallback;
    }

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

        if (jsonValue.TryGetValue<bool>(out var boolValue))
        {
            value = boolValue ? "true" : "false";
            return true;
        }

        return false;
    }

    private static string EmptyFallback(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "не указано" : value.Trim();

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string JoinNonEmpty(string separator, params string?[] values)
    {
        var parts = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim())
            .ToArray();
        return parts.Length == 0 ? "не указано" : string.Join(separator, parts);
    }

    private sealed record CommandRequest(string Command, string CommandToken, string Arguments);

    private readonly record struct DetailRequest(string Token, string Selector);

    private sealed record GuardianPoliticsLabels(
        IReadOnlyDictionary<string, string> Guardians,
        IReadOnlyDictionary<string, string> Abodes);

    private sealed record AbodeSnapshot(
        string Selector,
        string Name,
        string GuardianId,
        string GuardianName,
        string Description,
        string Anchor,
        string Power);

    private sealed record AbodePowerEntrySnapshot(
        string Selector,
        string Title,
        string GuardianId,
        string Summary,
        string Reason,
        string Delta,
        string Turn);

    private sealed record GuardianProjectSnapshot(
        string Selector,
        string GuardianId,
        string GuardianName,
        string ProjectId,
        string Title,
        string Type,
        string Tier,
        string Mode,
        string State,
        string Description,
        string WorkDone,
        string TotalWork,
        string Pressure,
        string Stability,
        IReadOnlyList<string> Effects,
        IReadOnlyList<GuardianProjectJournalEntry> Journal);

    private sealed record GuardianProjectJournalEntry(
        string GuardianId,
        string ProjectId,
        string Turn,
        string EventType,
        string Visibility,
        string Title,
        string Summary);

    private sealed record SummarySpec(string Path, string PropertyName, string Label);

    private sealed record JsonReadResult(string Path, bool FileExists, JsonNode? Node, string Error);
}
