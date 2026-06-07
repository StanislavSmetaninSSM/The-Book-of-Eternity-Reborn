using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

public static class ExplorerShiningAbodeCommandResultBuilder
{
    private const string SoulStatePath = "game_state/meta/soul_state.json";
    private const string GuardiansPath = "game_state/meta/guardians.json";

    private enum CommandKind
    {
        Overview,
        Politics,
        FactionFounding,
        FactionRealignment,
        FactionLeadership,
        Treasury,
        SourceOfLight
    }

    private static readonly IReadOnlyDictionary<string, CommandKind> CommandKinds =
        new Dictionary<string, CommandKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["/shining_abode"] = CommandKind.Overview,
            ["/сияющая_обитель"] = CommandKind.Overview,
            ["/shining_politics"] = CommandKind.Politics,
            ["/сияющая_политика"] = CommandKind.Politics,
            ["/shining_faction_founding"] = CommandKind.FactionFounding,
            ["/основание_сияющей_фракции"] = CommandKind.FactionFounding,
            ["/shining_faction_realignment"] = CommandKind.FactionRealignment,
            ["/перестройка_сияющей_фракции"] = CommandKind.FactionRealignment,
            ["/shining_faction_leadership"] = CommandKind.FactionLeadership,
            ["/смена_главы_сияющей_фракции"] = CommandKind.FactionLeadership,
            ["/shining_treasury"] = CommandKind.Treasury,
            ["/казначейство"] = CommandKind.Treasury,
            ["/source_of_light"] = CommandKind.SourceOfLight,
            ["/источник_света"] = CommandKind.SourceOfLight
        };

    public static bool CanBuild(string command) => CommandKinds.ContainsKey(ExplorerCommandCatalog.ExtractCommandToken(command.Trim()));

    private static string ExtractArguments(string command)
    {
        var parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? parts[1] : string.Empty;
    }

    private static string ExtractFirstArgument(string arguments) =>
        arguments
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;

    public static async Task<ExplorerCommandResult?> TryBuildAsync(
        string command,
        StateManager stateManager,
        FileSystemManager fs,
        bool includeAdvancedDiagnostics = false)
    {
        var trimmedCommand = command.Trim();
        var parsedCommand = ExplorerCommandParser.Parse(trimmedCommand);
        var normalizedCommand = parsedCommand.Success
            ? ExplorerCommandCatalog.ExtractCommandToken(parsedCommand.BuilderCommand)
            : ExplorerCommandCatalog.ExtractCommandToken(trimmedCommand);
        var commandArguments = parsedCommand.Success
            ? parsedCommand.Arguments
            : ExtractArguments(trimmedCommand);
        if (!CommandKinds.TryGetValue(normalizedCommand, out var kind))
            return null;

        await stateManager.RefreshGameStateAsync();

        return kind switch
        {
            CommandKind.Overview => await BuildOverview(normalizedCommand, fs, stateManager),
            CommandKind.Politics => await BuildPolitics(
                normalizedCommand,
                fs,
                includeAdvancedDiagnostics || stateManager.Settings.ShowGmThoughts),
            CommandKind.FactionFounding => await BuildFactionFounding(normalizedCommand, fs, stateManager),
            CommandKind.FactionRealignment => await BuildFactionRealignment(normalizedCommand, fs, stateManager, commandArguments),
            CommandKind.FactionLeadership => await BuildFactionLeadership(normalizedCommand, fs, stateManager, commandArguments),
            CommandKind.Treasury => await BuildTreasury(normalizedCommand, fs),
            CommandKind.SourceOfLight => await BuildSourceOfLight(normalizedCommand, fs),
            _ => null
        };
    }

    private static async Task<ExplorerCommandResult> BuildOverview(string command, FileSystemManager fs, StateManager stateManager)
    {
        var shining = await ReadJson(fs, ShiningAbodeState.StatePath);
        var residents = await ReadJson(fs, GuardianAbodeResidentState.StatePath);
        var soul = await ReadJson(fs, SoulStatePath);
        var core = await ReadJson(fs, ShiningCoreActionRequestState.PendingActionsRequestPath);
        var trade = await ReadJson(fs, ShiningTradeRequestState.PendingRequestsPath);
        var source = await ReadJson(fs, SourceOfLightCapstoneState.PendingRequestPath);

        var blocks = new List<UiBlock>
        {
            Panel("Сияющая Обитель",
                Grid(
                    ("Фаза", EmptyFallback(stateManager.CurrentState.CurrentRealm)),
                    ("Душа", EmptyFallback(stateManager.CurrentState.SoulName)),
                    ("Доступность", GetString(shining.Node, "availability", "не указана")),
                    ("Сияние", DescribeRadiance(shining.Node)),
                    ("Искры Света", GetNumberOrString(shining.Node, "lightSparks", "0")),
                    ("Залов", CountArray(shining.Node, "halls").ToString()),
                    ("Фракций", CountArray(shining.Node, "factions").ToString()),
                    ("Резидентов", CountArray(residents.Node, GuardianAbodeResidentState.EntriesProperty).ToString()),
                    ("Открытый черновик Врат", GetBoolText(shining.Node?["gates"]?["hasOpenDraft"])),
                    ("Ожиданий Обители", CountRequests(core).ToString()),
                    ("Ожиданий торговли", CountRequests(trade).ToString()),
                    ("Источник Света", DescribePresence(source))))
        };

        AddRawOrWarning(blocks, $"Полный JSON {ShiningAbodeState.StatePath}", shining);
        AddRawOrWarning(blocks, $"Полный JSON {GuardianAbodeResidentState.StatePath}", residents);
        AddRawOrWarning(blocks, $"Полный JSON {SoulStatePath}", soul);
        AddRawOrWarning(blocks, $"Полный JSON {ShiningCoreActionRequestState.PendingActionsRequestPath}", core);
        AddRawOrWarning(blocks, $"Полный JSON {ShiningTradeRequestState.PendingRequestsPath}", trade);
        AddRawOrWarning(blocks, $"Полный JSON {SourceOfLightCapstoneState.PendingRequestPath}", source);
        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildPolitics(
        string command,
        FileSystemManager fs,
        bool includeAdvancedDiagnostics)
    {
        var shining = await ReadJson(fs, ShiningAbodeState.StatePath);
        var residents = await ReadJson(fs, GuardianAbodeResidentState.StatePath);
        var guardians = await ReadJson(fs, GuardiansPath);
        var foundings = await ReadJson(fs, ShiningFactionRequestState.PendingFoundingsRequestPath);
        var realignments = await ReadJson(fs, ShiningFactionRequestState.PendingRealignmentsRequestPath);
        var leadership = await ReadJson(fs, ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath);

        var blocks = new List<UiBlock>
        {
            Panel("Политика Сияющей Обители",
                Grid(
                    ("Фракций", CountArray(shining.Node, "factions").ToString()),
                    ("Светозарных акторов", CountArray(shining.Node, "shiningPoliticalActors").ToString()),
                    ("Кампаний против фракций", CountArray(shining.Node, ShiningAbodeState.FactionConflictCampaignsProperty).ToString()),
                    ("Резидентов", CountArray(residents.Node, GuardianAbodeResidentState.EntriesProperty).ToString()),
                    ("Ожиданий основания", CountRequests(foundings).ToString()),
                    ("Ожиданий перехода", CountRequests(realignments).ToString()),
                    ("Ожиданий смены власти", CountRequests(leadership).ToString())))
        };

        AddFactionPoliticalMemoryBlocks(blocks, shining.Node);

        if (includeAdvancedDiagnostics)
        {
            AddRawOrWarning(blocks, $"Полный JSON {ShiningAbodeState.StatePath}", shining);
            AddRawOrWarning(blocks, $"Полный JSON {GuardianAbodeResidentState.StatePath}", residents);
            AddRawOrWarning(blocks, $"Полный JSON {GuardiansPath}", guardians);
            AddRawOrWarning(blocks, $"Полный JSON {ShiningFactionRequestState.PendingFoundingsRequestPath}", foundings);
            AddRawOrWarning(blocks, $"Полный JSON {ShiningFactionRequestState.PendingRealignmentsRequestPath}", realignments);
            AddRawOrWarning(blocks, $"Полный JSON {ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath}", leadership);
        }
        else
        {
            AddWarningIfMalformed(blocks, $"Полный JSON {ShiningAbodeState.StatePath}", shining);
            AddWarningIfMalformed(blocks, $"Полный JSON {GuardianAbodeResidentState.StatePath}", residents);
            AddWarningIfMalformed(blocks, $"Полный JSON {GuardiansPath}", guardians);
            AddWarningIfMalformed(blocks, $"Полный JSON {ShiningFactionRequestState.PendingFoundingsRequestPath}", foundings);
            AddWarningIfMalformed(blocks, $"Полный JSON {ShiningFactionRequestState.PendingRealignmentsRequestPath}", realignments);
            AddWarningIfMalformed(blocks, $"Полный JSON {ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath}", leadership);
        }

        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildFactionFounding(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var context = await ReadPoliticalPromptContext(fs, stateManager);
        if (context.Blocker != null)
            return Blocked(command, "Основание сияющей фракции недоступно", context.Blocker);

        var foundingMalformed = await ShiningFactionRequestState.IsRequestFileMalformedAsync(
            fs,
            ShiningFactionRequestState.PendingFoundingsRequestPath,
            static json => JsonSerializer.Deserialize<ShiningFactionRequestState.PendingShiningFactionFoundingRequest>(
                json,
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        if (foundingMalformed)
            return Blocked(command, "Основание сияющей фракции недоступно", "Предыдущий запрос основания ждёт проверки состояния. Повторите действие после восстановления политических ожиданий.");

        if ((await ShiningFactionRequestState.ReadFoundingRequestsAsync(fs)).Count > 0)
            return Blocked(command, "Основание сияющей фракции уже ожидает", "В Сияющей Обители уже есть ожидающий запрос основания фракции. Дождитесь ответа ГМ перед новым основанием.");

        var currentFeathers = GetSoulInkFeathers(context.SoulRoot);
        var currentSparks = GetInt(context.ShiningRoot["lightSparks"], 0);
        if (currentFeathers < ShiningFactionRequestState.FactionFoundingCostFeathers ||
            currentSparks < ShiningFactionRequestState.FactionFoundingCostLightSparks)
        {
            return Blocked(
                command,
                "Не хватает ресурсов",
                $"Для основания нужны {ShiningFactionRequestState.FactionFoundingCostFeathers} Чернильных Перьев и {ShiningFactionRequestState.FactionFoundingCostLightSparks} Искр Света. Сейчас доступно: {currentFeathers} Перьев и {currentSparks} Искр.");
        }

        var supporters = EnumeratePlayerVisibleAscendedResidents(context, allowFactionless: true)
            .Where(resident => !IsCurrentResidentHead(context.ShiningRoot, GetString(resident, "residentId", string.Empty)))
            .OrderBy(static resident => GetString(resident, "displayName", GetString(resident, "residentId", string.Empty)), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (supporters.Length < 3)
            return Blocked(command, "Недостаточно сторонников", "Для основания новой сияющей фракции нужны минимум три вознесённых обитателя, которые не являются текущими главами фракций.");

        var supporterHints = supporters
            .Select(static resident => $"{GetResidentDisplayName(resident)} ({GetString(resident, "residentId", string.Empty)})")
            .ToArray();

        var blocks = new List<UiBlock>
        {
            Panel("Основание сияющей фракции",
                Grid(
                    ("Чернильные Перья", $"{currentFeathers} доступно / нужно {ShiningFactionRequestState.FactionFoundingCostFeathers}"),
                    ("Искры Света", $"{currentSparks} доступно / нужно {ShiningFactionRequestState.FactionFoundingCostLightSparks}"),
                    ("Минимум сторонников", "3 вознесённых обитателя")),
                new UiTextBlock { Text = "Подходящие сторонники:\n- " + string.Join("\n- ", supporterHints) }),
            Message(
                UiNotificationSeverity.Info,
                "Запрос для ГМ",
                "После подтверждения ресурсы будут зарезервированы, а ГМ получит политический запрос основания.")
        };

        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiTextInputPrompt
                {
                    Id = "faction_name",
                    Prompt = "Название фракции",
                    Required = true,
                    Placeholder = "Например: Дом Зари"
                },
                new UiTextInputPrompt
                {
                    Id = "hall_name",
                    Prompt = "Название зала",
                    Required = true,
                    Placeholder = "Например: Зал Зари"
                },
                new UiLongTextInputPrompt
                {
                    Id = "charter_summary",
                    Prompt = "Краткая хартия фракции",
                    Required = true,
                    Placeholder = "Опишите идею, обет и политический смысл новой фракции.",
                    MinLines = 2,
                    MaxLines = 5
                },
                new UiLongTextInputPrompt
                {
                    Id = "hall_description",
                    Prompt = "Описание зала",
                    Required = true,
                    Placeholder = "Опишите, как выглядит зал и какую службу он несёт.",
                    MinLines = 2,
                    MaxLines = 5
                },
                new UiSelectionPrompt
                {
                    Id = "favored_archetype",
                    Prompt = "Любимый архетип проектов",
                    Required = true,
                    Options = BuildProjectArchetypeOptions()
                },
                new UiSelectionPrompt
                {
                    Id = "patron_effect_family",
                    Prompt = "Покровительская семья эффекта",
                    Required = true,
                    Options = BuildEffectFamilyOptions()
                },
                new UiSelectionPrompt
                {
                    Id = "hall_secondary_service_tag",
                    Prompt = "Дополнительная служба зала",
                    Options = BuildHallSecondaryTagOptions()
                },
                new UiTextInputPrompt
                {
                    Id = "supporting_resident_ids",
                    Prompt = "Сторонники",
                    Required = true,
                    Placeholder = $"Через запятую: {string.Join(", ", supporters.Take(3).Select(static resident => GetString(resident, "residentId", string.Empty)))}"
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_shining_politics_write",
                    Prompt = "Подтвердить основание и резерв ресурсов",
                    Required = true
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildFactionRealignment(
        string command,
        FileSystemManager fs,
        StateManager stateManager,
        string commandArguments)
    {
        var context = await ReadPoliticalPromptContext(fs, stateManager);
        if (context.Blocker != null)
            return Blocked(command, "Перестройка сияющей фракции недоступна", context.Blocker);

        var realignmentMalformed = await ShiningFactionRequestState.IsRequestFileMalformedAsync(
            fs,
            ShiningFactionRequestState.PendingRealignmentsRequestPath,
            static json => JsonSerializer.Deserialize<ShiningFactionRequestState.PendingShiningFactionRealignmentRequest>(
                json,
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        if (realignmentMalformed)
            return Blocked(command, "Перестройка временно недоступна", "Предыдущий запрос перестройки ждёт проверки состояния. Повторите действие после восстановления политических ожиданий.");

        var residents = EnumeratePlayerVisibleAscendedResidents(context, allowFactionless: false)
            .Where(static resident => string.Equals(GetString(resident, "factionRealignmentState", string.Empty), ShiningAbodeState.FactionRealignmentStateReadyToRealign, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static resident => GetResidentDisplayName(resident), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (residents.Length == 0)
            return Blocked(command, "Нет готовых к перестройке", "Сейчас нет вознесённых обитателей в состоянии готовности к фракционной перестройке.");

        var requestedResidentId = ExtractFirstArgument(commandArguments);
        JsonObject? selectedResident = null;
        if (!string.IsNullOrWhiteSpace(requestedResidentId))
        {
            selectedResident = residents.FirstOrDefault(resident =>
                string.Equals(GetString(resident, "residentId", string.Empty), requestedResidentId, StringComparison.OrdinalIgnoreCase));
            if (selectedResident == null)
                return Blocked(command, "Обитатель не готов к перестройке", "Выберите вознесённого обитателя, который сейчас готов к фракционной перестройке.");
        }
        else if (residents.Length == 1)
        {
            selectedResident = residents[0];
        }

        var visibleFactions = GetVisibleFactions(context.ShiningRoot).ToArray();
        var excludedSourceFactionId = selectedResident == null
            ? string.Empty
            : GetString(selectedResident, "shiningFactionId", string.Empty);
        var targetFactions = visibleFactions
            .Where(faction => !string.Equals(GetString(faction, "factionId", string.Empty), excludedSourceFactionId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var residentOptions = selectedResident == null ? residents : [selectedResident];

        var blocks = new List<UiBlock>
        {
            Panel("Перестройка сияющей фракции",
                new UiTextBlock { Text = "Готовые обитатели:\n- " + string.Join("\n- ", residentOptions.Select(BuildResidentPoliticalLabel)) },
                new UiTextBlock { Text = targetFactions.Length == 0
                    ? "Доступен уход в нейтралитет; других видимых целевых фракций сейчас нет."
                    : "Целевые фракции:\n- " + string.Join("\n- ", targetFactions.Select(BuildFactionPoliticalLabel)) })
        };

        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "resident_id",
                    Prompt = "Обитатель для перестройки",
                    Required = true,
                    Options = residentOptions
                        .Select(static resident => Option(
                            GetString(resident, "residentId", string.Empty),
                            BuildResidentPoliticalLabel(resident),
                            "Готов к фракционной перестройке."))
                        .ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "realignment_mode",
                    Prompt = "Режим перестройки",
                    Required = true,
                    Options =
                    [
                        Option(ShiningFactionRequestState.RealignmentModeAcceptedTransfer, "Перейти в другую фракцию", "ГМ разрешит переход к выбранной видимой фракции."),
                        Option(ShiningFactionRequestState.RealignmentModeDepartureToNeutral, "Уйти в нейтралитет", "Обитатель покинет текущую фракцию без новой цели.")
                    ]
                },
                new UiSelectionPrompt
                {
                    Id = "target_faction_id",
                    Prompt = "Целевая фракция для перехода",
                    Options = new[] { Option(string.Empty, "Не нужна для нейтралитета", "Выберите это, если обитатель уходит в нейтральное состояние.") }
                        .Concat(targetFactions
                        .Select(static faction => Option(
                            GetString(faction, "factionId", string.Empty),
                            ResolveFactionDisplayName(faction),
                            $"Сила фракции: {GetInt(faction["factionStrength"], 0)}.")))
                        .ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_shining_politics_write",
                    Prompt = "Подтвердить запрос перестройки",
                    Required = true
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildFactionLeadership(
        string command,
        FileSystemManager fs,
        StateManager stateManager,
        string commandArguments)
    {
        var context = await ReadPoliticalPromptContext(fs, stateManager);
        if (context.Blocker != null)
            return Blocked(command, "Смена главы сияющей фракции недоступна", context.Blocker);

        var leadershipMalformed = await ShiningFactionRequestState.IsRequestFileMalformedAsync(
            fs,
            ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
            static json => JsonSerializer.Deserialize<ShiningFactionRequestState.PendingShiningFactionLeadershipTransitionRequest>(
                json,
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        if (leadershipMalformed)
            return Blocked(command, "Смена главы временно недоступна", "Предыдущий запрос смены власти ждёт проверки состояния. Повторите действие после восстановления политических ожиданий.");

        var factions = GetVisibleFactions(context.ShiningRoot)
            .Where(static faction => !string.Equals(GetString(faction["leadership"], "leadershipState", string.Empty), ShiningAbodeState.LeadershipStateVacant, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (factions.Length == 0)
            return Blocked(command, "Нет фракций с действующим главой", "Сейчас нет видимых сияющих фракций с действующим главой для передачи власти.");

        var requestedFactionId = ExtractFirstArgument(commandArguments);
        JsonObject? selectedFaction = null;
        if (!string.IsNullOrWhiteSpace(requestedFactionId))
        {
            selectedFaction = factions.FirstOrDefault(faction =>
                string.Equals(GetString(faction, "factionId", string.Empty), requestedFactionId, StringComparison.OrdinalIgnoreCase));
            if (selectedFaction == null)
                return Blocked(command, "Фракция не подходит для смены главы", "Выберите видимую сияющую фракцию с действующим главой.");
        }
        else if (factions.Length == 1)
        {
            selectedFaction = factions[0];
        }

        var factionOptions = selectedFaction == null ? factions : [selectedFaction];
        var selectedFactionId = selectedFaction == null
            ? string.Empty
            : GetString(selectedFaction, "factionId", string.Empty);
        var candidates = BuildLeadershipCandidateOptions(context, selectedFactionId).ToList();
        var supporters = EnumeratePlayerVisibleAscendedResidents(context, allowFactionless: false)
            .Where(resident => string.IsNullOrWhiteSpace(selectedFactionId) ||
                               string.Equals(GetString(resident, "shiningFactionId", string.Empty), selectedFactionId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static resident => GetResidentDisplayName(resident), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var blocks = new List<UiBlock>
        {
            Panel("Смена главы сияющей фракции",
                new UiTextBlock { Text = "Фракции с действующим главой:\n- " + string.Join("\n- ", factionOptions.Select(BuildFactionLeadershipLabel)) },
                new UiTextBlock { Text = "Кандидаты и сторонники:\n- " + string.Join("\n- ", candidates.Select(static option => option.Label).Concat(supporters.Select(BuildResidentPoliticalLabel)).Distinct(StringComparer.OrdinalIgnoreCase)) })
        };

        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "faction_id",
                    Prompt = "Фракция",
                    Required = true,
                    Options = factionOptions
                        .Select(static faction => Option(
                            GetString(faction, "factionId", string.Empty),
                            BuildFactionLeadershipLabel(faction),
                            "Выберите фракцию, где меняется глава."))
                        .ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "transition_mode",
                    Prompt = "Режим смены власти",
                    Required = true,
                    Options =
                    [
                        Option(ShiningFactionRequestState.TransitionModeAbdication, "Отречение", "Действующий глава отказывается от власти без нового кандидата."),
                        Option(ShiningFactionRequestState.TransitionModePeacefulSuccession, "Мирное наследование", "Фракция признаёт нового главу при поддержке сторонников."),
                        Option(ShiningFactionRequestState.TransitionModeRevolt, "Мятеж", "Сторонники пытаются сместить оспоренного главу.")
                    ]
                },
                new UiSelectionPrompt
                {
                    Id = "candidate_head_choice",
                    Prompt = "Кандидат на главу",
                    Options = candidates
                },
                new UiTextInputPrompt
                {
                    Id = "supporting_resident_ids",
                    Prompt = "Сторонники",
                    Placeholder = $"Через запятую: {string.Join(", ", supporters.Take(2).Select(static resident => GetString(resident, "residentId", string.Empty)))}"
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_shining_politics_write",
                    Prompt = "Подтвердить запрос смены главы",
                    Required = true
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildTreasury(string command, FileSystemManager fs)
    {
        var shining = await ReadJson(fs, ShiningAbodeState.StatePath);
        var soul = await ReadJson(fs, SoulStatePath);
        var treasury = shining.Node?["treasury"];

        var blocks = new List<UiBlock>
        {
            Panel("Казначейство Сияющей Обители",
                Grid(
                    ("Режим браузера", "локальные операции доступны через форму"),
                    ("Чернильные Перья души", DescribeInkFeathers(soul.Node)),
                    ("Искры Света", GetNumberOrString(shining.Node, "lightSparks", "0")),
                    ("Вклад Перьями", GetNumberOrString(treasury, "depositedInkFeathers", "0")),
                    ("Проценты к получению", GetNumberOrString(treasury, "claimableInkFeatherInterest", "0")),
                    ("Цикл процентов", GetString(treasury, "lastInterestSettlementCycleId", "не указан")),
                    ("Цикл обмена", GetString(treasury, "exchangeCycleId", "не указан")),
                    ("Искр обменяно в цикле", GetNumberOrString(treasury, "exchangeThisCycleLightSparks", "0")))),
            Message(
                UiNotificationSeverity.Info,
                "Браузерная запись",
                "Форма использует общий протокол локальной блокировки/отката UI и блокируется при активном ходе ГМа.")
        };

        AddRawOrWarning(blocks, $"Полный JSON {ShiningAbodeState.StatePath}", shining);
        AddRawOrWarning(blocks, $"Полный JSON {SoulStatePath}", soul);
        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "treasury_operation",
                    Prompt = "Операция казначейства",
                    Required = true,
                    Options =
                    [
                        Option("deposit", "Внести Перья", "Перевести Чернильные Перья души во вклад казны."),
                        Option("withdraw", "Вывести Перья", "Вернуть Чернильные Перья из вклада душе."),
                        Option("claim_interest", "Получить проценты", "Начислить и получить проценты текущего сияющего цикла."),
                        Option("exchange", "Обменять на Искры", "Потратить Перья на Искры Света в рамках лимита цикла.")
                    ]
                },
                new UiTextInputPrompt
                {
                    Id = "treasury_amount",
                    Prompt = "Сумма",
                    Placeholder = "Для процентов можно оставить 0"
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildSourceOfLight(string command, FileSystemManager fs)
    {
        var shining = await ReadJson(fs, ShiningAbodeState.StatePath);
        var soul = await ReadJson(fs, SoulStatePath);
        var pending = await ReadJson(fs, SourceOfLightCapstoneState.PendingRequestPath);

        var status = DescribeSourceOfLightStatus(shining.Node, soul.Node, pending);
        var blocks = new List<UiBlock>
        {
            Panel("Источник Света",
                Grid(
                    ("Режим браузера", "создание ожидающего запроса доступно через форму"),
                    ("Статус", status),
                    ("Сияние", DescribeRadiance(shining.Node)),
                    ("Награда-пассив", SourceOfLightCapstoneState.PassiveId),
                    ("Награда-реликвия", SourceOfLightCapstoneState.RelicId))),
            Message(
                UiNotificationSeverity.Info,
                "Браузерная запись",
                "Форма создаёт pending_source_of_light_capstone.json только если требования полного Сияния выполнены.")
        };

        AddRawOrWarning(blocks, $"Полный JSON {ShiningAbodeState.StatePath}", shining);
        AddRawOrWarning(blocks, $"Полный JSON {SoulStatePath}", soul);
        AddRawOrWarning(blocks, $"Полный JSON {SourceOfLightCapstoneState.PendingRequestPath}", pending);
        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "source_of_light_action",
                    Prompt = "Действие Источника Света",
                    Required = true,
                    Options =
                    [
                        Option("open", "Открыть Источник", "Создать клиентский ожидающий запрос для вершинной сцены.")
                    ]
                }
            ]);
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

    private static void AddWarningIfMalformed(List<UiBlock> blocks, string title, JsonReadResult read)
    {
        if (read.Node == null && read.FileExists)
            blocks.Add(Message(UiNotificationSeverity.Warning, title, $"Файл найден, но не разобран как JSON: {read.Path}. {read.Error}"));
    }

    private static void AddFactionPoliticalMemoryBlocks(List<UiBlock> blocks, JsonNode? shiningRoot)
    {
        if (shiningRoot is not JsonObject root)
            return;

        var visibleFactions = SarefMainStoryState.GetPlayerVisibleShiningFactions(root).ToList();
        if (visibleFactions.Count == 0)
            return;

        var chronicleRows = new List<UiTableRow>();
        var influenceRows = new List<UiTableRow>();
        var resourceRows = new List<UiTableRow>();

        foreach (var faction in visibleFactions)
        {
            var factionName = ResolveFactionDisplayName(faction);

            foreach (var entry in EnumeratePlayerVisibleChronicleEntries(faction)
                         .OrderByDescending(static entry => GetInt(entry, "turnNumber"))
                         .ThenBy(static entry => GetString(entry, "entryId", string.Empty), StringComparer.OrdinalIgnoreCase))
            {
                chronicleRows.Add(new UiTableRow
                {
                    Cells =
                    [
                        factionName,
                        GetNumberOrString(entry, "turnNumber", "0"),
                        TranslateEventType(GetString(entry, "eventType", "событие")),
                        GetString(entry, "summary", "без сводки"),
                        DescribeConsequences(entry)
                    ]
                });
            }

            foreach (var zone in EnumeratePlayerVisibleInfluenceZones(faction)
                         .OrderByDescending(static zone => GetInt(zone, "updatedAtTurn"))
                         .ThenBy(static zone => GetString(zone, "zoneId", string.Empty), StringComparer.OrdinalIgnoreCase))
            {
                influenceRows.Add(new UiTableRow
                {
                    Cells =
                    [
                        factionName,
                        GetString(zone, "displayName", GetString(zone, "zoneId", "зона")),
                        $"{GetNumberOrString(zone, "influenceValue", "0")} влияния / {GetNumberOrString(zone, "controlLevel", "0")} контроля",
                        GetString(zone, "publicStatus", "не указано"),
                        GetString(zone, "summary", "без сводки")
                    ]
                });
            }

            foreach (var entry in EnumerateCurrentResourceBalances(faction))
            {
                resourceRows.Add(new UiTableRow
                {
                    Cells =
                    [
                        factionName,
                        TranslateResourceType(GetString(entry, "resourceType", "resource")),
                        GetNumberOrString(entry, "balanceAfter", "0"),
                        DescribeSignedDelta(entry["delta"]),
                        GetNumberOrString(entry, "turnNumber", "0")
                    ]
                });
            }
        }

        if (chronicleRows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Хроника фракций",
                Columns = ["Фракция", "Ход", "Событие", "Сводка", "Последствия"],
                Rows = chronicleRows
            });
        }

        if (influenceRows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Влияние фракций",
                Columns = ["Фракция", "Зона", "Влияние", "Статус", "Сводка"],
                Rows = influenceRows
            });
        }

        if (resourceRows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Ресурсы фракций",
                Columns = ["Фракция", "Ресурс", "Баланс", "Изменение", "Ход"],
                Rows = resourceRows
            });
        }
    }

    private static async Task<PoliticalPromptContext> ReadPoliticalPromptContext(
        FileSystemManager fs,
        StateManager stateManager)
    {
        var soul = await ReadJson(fs, SoulStatePath);
        var shining = await ReadJson(fs, ShiningAbodeState.StatePath);
        var residents = await ReadJson(fs, GuardianAbodeResidentState.StatePath);
        var guardians = await ReadJson(fs, GuardiansPath);

        var currentRealm = FirstNonEmpty(GetString(soul.Node, "currentRealm", string.Empty), stateManager.CurrentState.CurrentRealm);
        if (!RealmSemantics.IsShiningRealm(currentRealm))
            return PoliticalPromptContext.Blocked("Политические действия доступны только в Сияющей Обители. Сейчас душа находится в другом царстве.");

        if (shining.Node is not JsonObject shiningRoot)
            return PoliticalPromptContext.Blocked("Состояние Сияющей Обители сейчас недоступно. Повторите действие после восстановления состояния.");
        if (soul.Node is not JsonObject soulRoot)
            return PoliticalPromptContext.Blocked("Состояние души сейчас недоступно. Повторите действие после восстановления состояния.");

        var rawOwnerStateError = ShiningAbodeState.ValidateRawOwnerStateForActionableMode(shiningRoot);
        if (!string.IsNullOrWhiteSpace(rawOwnerStateError))
            return PoliticalPromptContext.Blocked("Сияющая Обитель сейчас не готова к политическим действиям. Проверьте состояние перед новым запросом.");

        if (!string.Equals(GetString(shiningRoot, "availability", string.Empty), ShiningAbodeState.AvailabilityActive, StringComparison.OrdinalIgnoreCase))
            return PoliticalPromptContext.Blocked("Политические действия доступны только в активной Сияющей Обители.");

        var packageMode = ShiningAbodeState.GetPreparedIncarnationPackageMode(shiningRoot);
        if (packageMode != ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
            return PoliticalPromptContext.Blocked("Политические действия недоступны, пока Сияющая Обитель ждёт передачу в новую жизнь.");

        var residentRoot = residents.Node as JsonObject;
        var guardiansRoot = guardians.Node as JsonObject;
        ShiningAbodeState.NormalizeStateRoot(shiningRoot, residentRoot, guardiansRoot);
        return new PoliticalPromptContext(shiningRoot, soulRoot, residentRoot, guardiansRoot, null);
    }

    private static List<UiSelectionOption> BuildProjectArchetypeOptions() =>
    [
        Option(ShiningAbodeState.ProjectArchetypeAccord, "Согласие", "Союзы, договоры и социальная ткань фракции."),
        Option(ShiningAbodeState.ProjectArchetypeRevelation, "Откровение", "Знание, разведка и раскрытие скрытого."),
        Option(ShiningAbodeState.ProjectArchetypeProvision, "Снабжение", "Ресурсы, запасы и устойчивость фракции."),
        Option(ShiningAbodeState.ProjectArchetypeRemembrance, "Память", "История, архив и восстановление следов."),
        Option(ShiningAbodeState.ProjectArchetypeRefinement, "Огранка", "Реликвии, усиление и ремесленная точность."),
        Option(ShiningAbodeState.ProjectArchetypePassage, "Путь", "Маршруты, переходы и безопасное сопровождение."),
        Option(ShiningAbodeState.ProjectArchetypeWarding, "Охрана", "Защита, выживание и удержание границ."),
        Option(ShiningAbodeState.ProjectArchetypeSubversion, "Подрыв", "Тайное влияние, маскировка и нарушение планов.")
    ];

    private static List<UiSelectionOption> BuildEffectFamilyOptions() =>
    [
        Option(ShiningAbodeState.EffectFamilySocial, "Социальная", "Главная служба зала будет связана с влиянием и согласием."),
        Option(ShiningAbodeState.EffectFamilyLore, "Знание", "Главная служба зала будет связана с лором и раскрытием."),
        Option(ShiningAbodeState.EffectFamilyResource, "Ресурсы", "Главная служба зала будет связана со снабжением."),
        Option(ShiningAbodeState.EffectFamilyMemory, "Память", "Главная служба зала будет связана с памятью."),
        Option(ShiningAbodeState.EffectFamilyDescent, "Нисхождение", "Главная служба зала будет связана с путями и переходами."),
        Option(ShiningAbodeState.EffectFamilyRoute, "Маршрут", "Главная служба зала будет связана с путями и переходами."),
        Option(ShiningAbodeState.EffectFamilyRelic, "Реликвии", "Главная служба зала будет связана с реликвиями."),
        Option(ShiningAbodeState.EffectFamilySurvival, "Выживание", "Главная служба зала будет связана с защитой и стойкостью.")
    ];

    private static List<UiSelectionOption> BuildHallSecondaryTagOptions() =>
    [
        Option(string.Empty, "Без второй службы", "Оставить только обязательную службу от покровительской семьи."),
        Option(ShiningAbodeState.HallServiceTagSocial, "Социальная служба", "Советы, переговоры и связи."),
        Option(ShiningAbodeState.HallServiceTagLore, "Служба знания", "Лор, записи и раскрытие."),
        Option(ShiningAbodeState.HallServiceTagResource, "Служба ресурсов", "Снабжение и запасы."),
        Option(ShiningAbodeState.HallServiceTagMemory, "Служба памяти", "Память, архив и восстановление."),
        Option(ShiningAbodeState.HallServiceTagDescent, "Служба пути", "Переходы, маршруты и нисхождение."),
        Option(ShiningAbodeState.HallServiceTagRelic, "Служба реликвий", "Реликвии и огранка.")
    ];

    private static IEnumerable<JsonObject> GetVisibleFactions(JsonObject shiningRoot) =>
        SarefMainStoryState.GetPlayerVisibleShiningFactions(shiningRoot)
            .Where(IsPlayerVisibleMemoryObject)
            .Where(static faction => ShiningAbodeState.IsFactionOperational(faction));

    private static IEnumerable<JsonObject> EnumeratePlayerVisibleAscendedResidents(
        PoliticalPromptContext context,
        bool allowFactionless)
    {
        var visibleFactionIds = GetVisibleFactions(context.ShiningRoot)
            .Select(static faction => GetString(faction, "factionId", string.Empty))
            .Where(static factionId => !string.IsNullOrWhiteSpace(factionId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return EnumerateAscendedResidents(context.ResidentsRoot)
            .Where(resident => ResidentFactionIsVisible(resident, visibleFactionIds, allowFactionless));
    }

    private static bool ResidentFactionIsVisible(
        JsonObject resident,
        HashSet<string> visibleFactionIds,
        bool allowFactionless)
    {
        var factionId = GetString(resident, "shiningFactionId", string.Empty);
        if (string.IsNullOrWhiteSpace(factionId))
            return allowFactionless;

        return visibleFactionIds.Contains(factionId);
    }

    private static IEnumerable<JsonObject> EnumerateVisibleRadiantActors(
        JsonObject shiningRoot,
        string selectedFactionId)
    {
        if (shiningRoot["shiningPoliticalActors"] is not JsonArray actors)
            return Enumerable.Empty<JsonObject>();

        return actors.OfType<JsonObject>()
            .Where(IsPlayerVisibleMemoryObject)
            .Where(actor => !string.IsNullOrWhiteSpace(GetString(actor, "actorId", string.Empty)))
            .Where(actor =>
            {
                var currentFactionId = GetString(actor, "currentFactionId", string.Empty);
                return string.IsNullOrWhiteSpace(selectedFactionId) ||
                       string.IsNullOrWhiteSpace(currentFactionId) ||
                       string.Equals(currentFactionId, selectedFactionId, StringComparison.OrdinalIgnoreCase);
            });
    }

    private static IEnumerable<JsonObject> EnumerateAscendedResidents(JsonObject? residentRoot)
    {
        if (residentRoot?[GuardianAbodeResidentState.EntriesProperty] is not JsonArray entries)
            return Enumerable.Empty<JsonObject>();

        return entries.OfType<JsonObject>()
            .Where(static resident => string.Equals(GetString(resident, "ascensionState", string.Empty), ShiningAbodeState.AscensionStateAscended, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCurrentResidentHead(JsonObject shiningRoot, string residentId)
    {
        if (string.IsNullOrWhiteSpace(residentId) || shiningRoot["factions"] is not JsonArray factions)
            return false;

        return factions.OfType<JsonObject>().Any(faction =>
            string.Equals(GetString(faction["leadership"], "headActorType", string.Empty), ShiningAbodeState.HeadActorTypeResident, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetString(faction["leadership"], "headActorId", string.Empty), residentId, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildResidentPoliticalLabel(JsonObject resident)
    {
        var residentId = GetString(resident, "residentId", string.Empty);
        var factionName = GetString(resident, "shiningFactionName", GetString(resident, "shiningFactionId", "без фракции"));
        var loyalty = GetNumberOrString(resident, "factionLoyaltyLevel", "0");
        var restlessness = GetNumberOrString(resident, "factionRestlessness", "0");
        return $"{GetResidentDisplayName(resident)} ({residentId}; фракция {factionName}; лояльность {loyalty}; брожение {restlessness})";
    }

    private static string BuildFactionPoliticalLabel(JsonObject faction) =>
        $"{ResolveFactionDisplayName(faction)} ({GetString(faction, "factionId", string.Empty)}; сила {GetNumberOrString(faction, "factionStrength", "0")})";

    private static string BuildFactionLeadershipLabel(JsonObject faction)
    {
        var leadership = faction["leadership"];
        var head = $"{GetString(leadership, "headActorType", string.Empty)}:{GetString(leadership, "headActorId", string.Empty)}";
        var state = GetString(leadership, "leadershipState", string.Empty);
        return $"{ResolveFactionDisplayName(faction)} ({GetString(faction, "factionId", string.Empty)}; глава {head}; состояние {state})";
    }

    private static IEnumerable<UiSelectionOption> BuildLeadershipCandidateOptions(
        PoliticalPromptContext context,
        string selectedFactionId)
    {
        yield return Option(
            $"{ShiningAbodeState.HeadActorTypePlayerSoul}:{ShiningAbodeState.HeadActorTypePlayerSoul}",
            "Душа игрока",
            "Кандидатура самой души игрока.");

        foreach (var resident in EnumeratePlayerVisibleAscendedResidents(context, allowFactionless: false)
                     .Where(resident => string.IsNullOrWhiteSpace(selectedFactionId) ||
                                        string.Equals(GetString(resident, "shiningFactionId", string.Empty), selectedFactionId, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(static resident => GetResidentDisplayName(resident), StringComparer.OrdinalIgnoreCase))
        {
            var residentId = GetString(resident, "residentId", string.Empty);
            yield return Option(
                $"{ShiningAbodeState.HeadActorTypeResident}:{residentId}",
                BuildResidentPoliticalLabel(resident),
                "Вознесённый обитатель может быть кандидатом в своей фракции.");
        }

        if (context.GuardiansRoot?["activeGuardian"] is JsonObject activeGuardian)
        {
            var guardianId = FirstNonEmpty(GetString(activeGuardian, "guardianId", string.Empty), GetString(activeGuardian, "id", string.Empty));
            if (!string.IsNullOrWhiteSpace(guardianId))
            {
                yield return Option(
                    $"{ShiningAbodeState.HeadActorTypeGuardian}:{guardianId}",
                    $"{ResolveGuardianDisplayName(activeGuardian)} ({guardianId})",
                    "Активный Хранитель как кандидат.");
            }
        }

        if (context.GuardiansRoot?["guardians"] is JsonArray guardians)
        {
            foreach (var guardian in guardians.OfType<JsonObject>())
            {
                var guardianId = FirstNonEmpty(GetString(guardian, "guardianId", string.Empty), GetString(guardian, "id", string.Empty));
                if (string.IsNullOrWhiteSpace(guardianId))
                    continue;
                yield return Option(
                    $"{ShiningAbodeState.HeadActorTypeGuardian}:{guardianId}",
                    $"{ResolveGuardianDisplayName(guardian)} ({guardianId})",
                    "Известный Хранитель как кандидат.");
            }
        }

        foreach (var actor in EnumerateVisibleRadiantActors(context.ShiningRoot, selectedFactionId))
        {
            var actorId = GetString(actor, "actorId", string.Empty);
            yield return Option(
                $"{ShiningAbodeState.HeadActorTypeRadiantActor}:{actorId}",
                $"{GetString(actor, "displayName", actorId)} ({actorId})",
                "Светозарный актор как кандидат.");
        }
    }

    private static string GetResidentDisplayName(JsonObject resident) =>
        FirstNonEmpty(
            GetString(resident, "displayName", string.Empty),
            GetString(resident, "residentName", string.Empty),
            GetString(resident, "residentId", string.Empty));

    private static string ResolveGuardianDisplayName(JsonObject guardian) =>
        FirstNonEmpty(
            GetString(guardian, "canonicalName", string.Empty),
            GetString(guardian, "guardianName", string.Empty),
            GetString(guardian, "name", string.Empty),
            GetString(guardian, "displayName", string.Empty),
            GetString(guardian, "guardianId", string.Empty),
            GetString(guardian, "id", string.Empty));

    private static int GetSoulInkFeathers(JsonObject soulRoot)
    {
        var node = soulRoot["inkFeathers"];
        return node is JsonObject feathers
            ? GetInt(feathers["current"], 0)
            : GetInt(node, 0);
    }

    private static string ResolveFactionDisplayName(JsonObject faction)
    {
        var name = GetString(faction["charter"], "factionName", string.Empty);
        return string.IsNullOrWhiteSpace(name)
            ? GetString(faction, "factionId", "фракция")
            : name;
    }

    private static IEnumerable<JsonObject> EnumeratePlayerVisibleChronicleEntries(JsonObject faction) =>
        EnumerateObjects(faction[ShiningAbodeState.FactionChronicleProperty] as JsonArray)
            .Where(IsPlayerVisibleMemoryObject);

    private static IEnumerable<JsonObject> EnumeratePlayerVisibleInfluenceZones(JsonObject faction) =>
        EnumerateObjects(faction[ShiningAbodeState.FactionInfluenceProperty] as JsonArray)
            .Where(static zone => !IsHiddenText(GetString(zone, "publicStatus", string.Empty)) && IsPlayerVisibleMemoryObject(zone));

    private static IEnumerable<JsonObject> EnumerateCurrentResourceBalances(JsonObject faction) =>
        EnumerateObjects(faction[ShiningAbodeState.FactionResourceLedgerProperty] as JsonArray)
            .Where(IsPlayerVisibleMemoryObject)
            .GroupBy(static entry => GetString(entry, "resourceType", "resource"), StringComparer.OrdinalIgnoreCase)
            .Select(static group => group
                .OrderByDescending(static entry => GetInt(entry, "turnNumber"))
                .ThenByDescending(static entry => GetString(entry, "entryId", string.Empty), StringComparer.OrdinalIgnoreCase)
                .First());

    private static IEnumerable<JsonObject> EnumerateObjects(JsonArray? array) =>
        array?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>();

    private static bool IsPlayerVisibleMemoryObject(JsonObject entry)
    {
        if (IsFalseFlag(entry["isPlayerVisible"]) || IsFalseFlag(entry["playerVisible"]))
            return false;

        var visibility = GetString(entry, "visibility", string.Empty);
        return !IsHiddenText(visibility);
    }

    private static bool IsFalseFlag(JsonNode? node) =>
        node is JsonValue value &&
        value.TryGetValue<bool>(out var flag) &&
        !flag;

    private static bool IsHiddenText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        return string.Equals(normalized, "hidden", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "gm_only", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "secret", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "private", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "internal", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "faction-internal", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeConsequences(JsonObject entry)
    {
        if (entry["consequences"] is not JsonArray consequences)
            return "нет";

        var visible = consequences
            .OfType<JsonValue>()
            .Select(static node => node.TryGetValue<string>(out var value) ? value?.Trim() ?? string.Empty : string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value) && !IsHiddenText(value))
            .ToArray();

        return visible.Length == 0 ? "нет" : string.Join("; ", visible);
    }

    private static string DescribeSignedDelta(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue))
                return intValue >= 0 ? $"+{intValue}" : intValue.ToString();
            if (value.TryGetValue<long>(out var longValue))
                return longValue >= 0 ? $"+{longValue}" : longValue.ToString();
            if (value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                return text.Trim();
        }

        return "0";
    }

    private static string TranslateResourceType(string value) => value switch
    {
        "lightSparks" or "light_sparks" => "Искры Света",
        "inkFeathers" or "ink_feathers" => "Чернильные Перья",
        _ => value
    };

    private static string TranslateEventType(string value) => value switch
    {
        "public_aid" => "публичная помощь",
        "founding" => "основание",
        "realignment" => "переход",
        "leadership" or "leadership_transition" => "смена власти",
        "resource_shift" => "ресурсный сдвиг",
        _ => value
    };

    private static ExplorerCommandResult Completed(string command, IEnumerable<UiBlock> blocks) =>
        Result(command, CommandExecutionState.Completed, blocks);

    private static ExplorerCommandResult Blocked(string command, string title, string message) =>
        Result(command, CommandExecutionState.Blocked, [Message(UiNotificationSeverity.Warning, title, message)]);

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

    private static UiSelectionOption Option(string value, string label, string description) =>
        new() { Value = value, Label = label, Description = description };

    private static UiRawJsonBlock Raw(string title, JsonNode node) =>
        new()
        {
            Title = title,
            Json = node.DeepClone()
        };

    private static string DescribeRadiance(JsonNode? root)
    {
        var radiance = root?["radiance"];
        var experience = GetNumberOrString(radiance, "experience", "0");
        var tier = GetNumberOrString(radiance, "tier", "0");
        return $"{experience} опыта / тир {tier}";
    }

    private static string DescribeInkFeathers(JsonNode? soulRoot)
    {
        var inkFeathers = soulRoot?["inkFeathers"];
        if (inkFeathers is JsonObject)
        {
            var current = GetNumberOrString(inkFeathers, "current", "0");
            var total = GetNumberOrString(inkFeathers, "total", "0");
            return $"{current} / всего {total}";
        }

        return GetNumberOrString(soulRoot, "inkFeathers", "0");
    }

    private static string DescribeSourceOfLightStatus(JsonNode? shiningRoot, JsonNode? soulRoot, JsonReadResult pending)
    {
        if (pending.Node != null)
            return "ожидает закрытия ГМ";
        if (pending.FileExists)
            return "pending-файл повреждён";
        if (shiningRoot?[SourceOfLightCapstoneState.ShiningStateProperty]?["completed"] is JsonValue completedValue &&
            completedValue.TryGetValue<bool>(out var completed) &&
            completed)
        {
            return "завершён";
        }
        if (soulRoot?["afterlifeCombatProfile"]?[SourceOfLightCapstoneState.CapstonesProperty]?[SourceOfLightCapstoneState.LightIncarnateProperty] != null)
            return "пассив уже есть у души";

        var experience = GetInt(shiningRoot?["radiance"], "experience");
        var tier = GetInt(shiningRoot?["radiance"], "tier");
        return tier >= SourceOfLightCapstoneState.RequiredRadianceTier &&
               experience >= SourceOfLightCapstoneState.RequiredRadianceExperience
            ? "требования выполнены; создание запроса доступно только через console/local-turn UX"
            : $"закрыт: нужно {SourceOfLightCapstoneState.RequiredRadianceExperience} опыта и тир {SourceOfLightCapstoneState.RequiredRadianceTier}";
    }

    private static string DescribePresence(JsonReadResult read)
    {
        if (read.Node != null)
            return "найдено";
        return read.FileExists ? "повреждено" : "отсутствует";
    }

    private static int CountRequests(JsonReadResult read)
    {
        if (read.Node is JsonArray rootArray)
            return rootArray.Count;
        if (read.Node?["requests"] is JsonArray requests)
            return requests.Count;
        return 0;
    }

    private static int CountArray(JsonNode? root, string propertyName) =>
        root?[propertyName] is JsonArray array ? array.Count : 0;

    private static string GetBoolText(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<bool>(out var result))
            return result ? "да" : "нет";
        return "не указано";
    }

    private static int GetInt(JsonNode? node, string propertyName)
    {
        if (node?[propertyName] is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue))
                return intValue;
            if (value.TryGetValue<long>(out var longValue))
                return longValue > int.MaxValue ? int.MaxValue : (int)longValue;
        }

        return 0;
    }

    private static int GetInt(JsonNode? node, int fallback)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue))
                return intValue;
            if (value.TryGetValue<long>(out var longValue))
                return longValue > int.MaxValue ? int.MaxValue : (int)longValue;
            if (value.TryGetValue<string>(out var text) && int.TryParse(text, out var parsed))
                return parsed;
        }

        return fallback;
    }

    private static string GetString(JsonNode? node, string propertyName, string fallback)
    {
        if (node?[propertyName] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
            return text.Trim();
        return fallback;
    }

    private static string GetNumberOrString(JsonNode? node, string propertyName, string fallback)
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

    private static string EmptyFallback(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "не указано" : value.Trim();

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private sealed record JsonReadResult(string Path, bool FileExists, JsonNode? Node, string Error);

    private sealed record PoliticalPromptContext(
        JsonObject ShiningRoot,
        JsonObject SoulRoot,
        JsonObject? ResidentsRoot,
        JsonObject? GuardiansRoot,
        string? Blocker)
    {
        public static PoliticalPromptContext Blocked(string blocker) =>
            new(new JsonObject(), new JsonObject(), null, null, blocker);
    }
}
