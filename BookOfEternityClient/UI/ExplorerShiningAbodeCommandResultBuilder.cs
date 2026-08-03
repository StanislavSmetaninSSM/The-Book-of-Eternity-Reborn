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
        NativeFactionDiscovery,
        FactionInvestment,
        ProjectSupport,
        ProjectUnsupport,
        ProjectRetirement,
        GatesOpen,
        GatesSelect,
        GatesDeselect,
        GatesReroll,
        IncarnationPrepare,
        RelicForge,
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
            ["/shining_native_faction_discovery"] = CommandKind.NativeFactionDiscovery,
            ["/открытие_нативной_фракции"] = CommandKind.NativeFactionDiscovery,
            ["/shining_faction_investment"] = CommandKind.FactionInvestment,
            ["/инвестиция_в_сияющую_фракцию"] = CommandKind.FactionInvestment,
            ["/shining_project_support"] = CommandKind.ProjectSupport,
            ["/поддержать_сияющий_проект"] = CommandKind.ProjectSupport,
            ["/shining_project_unsupport"] = CommandKind.ProjectUnsupport,
            ["/снять_поддержку_сияющего_проекта"] = CommandKind.ProjectUnsupport,
            ["/shining_project_retirement"] = CommandKind.ProjectRetirement,
            ["/отправить_сияющий_проект_в_историю"] = CommandKind.ProjectRetirement,
            ["/shining_gates_open"] = CommandKind.GatesOpen,
            ["/открыть_врата_инкарнации"] = CommandKind.GatesOpen,
            ["/shining_gates_select"] = CommandKind.GatesSelect,
            ["/выбрать_благословение"] = CommandKind.GatesSelect,
            ["/shining_gates_deselect"] = CommandKind.GatesDeselect,
            ["/снять_благословение"] = CommandKind.GatesDeselect,
            ["/shining_gates_reroll"] = CommandKind.GatesReroll,
            ["/обновить_врата"] = CommandKind.GatesReroll,
            ["/shining_incarnation_prepare"] = CommandKind.IncarnationPrepare,
            ["/подготовить_новую_жизнь"] = CommandKind.IncarnationPrepare,
            ["/shining_relic_forge"] = CommandKind.RelicForge,
            ["/сияющая_ковка"] = CommandKind.RelicForge,
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
            CommandKind.Overview => await BuildOverview(normalizedCommand, commandArguments, fs, stateManager, includeAdvancedDiagnostics || stateManager.Settings.ShowGmThoughts),
            CommandKind.Politics => await BuildPolitics(
                normalizedCommand,
                commandArguments,
                fs,
                includeAdvancedDiagnostics || stateManager.Settings.ShowGmThoughts),
            CommandKind.FactionFounding => await BuildFactionFounding(normalizedCommand, fs, stateManager),
            CommandKind.FactionRealignment => await BuildFactionRealignment(normalizedCommand, fs, stateManager, commandArguments),
            CommandKind.FactionLeadership => await BuildFactionLeadership(normalizedCommand, fs, stateManager, commandArguments),
            CommandKind.NativeFactionDiscovery => await BuildNativeFactionDiscovery(normalizedCommand, fs, stateManager),
            CommandKind.FactionInvestment => await BuildFactionInvestment(normalizedCommand, fs, stateManager, commandArguments),
            CommandKind.ProjectSupport => await BuildProjectSupportMutation(normalizedCommand, fs, stateManager, support: true),
            CommandKind.ProjectUnsupport => await BuildProjectSupportMutation(normalizedCommand, fs, stateManager, support: false),
            CommandKind.ProjectRetirement => await BuildProjectRetirement(normalizedCommand, fs, stateManager),
            CommandKind.GatesOpen => await BuildGatesOpen(normalizedCommand, fs, stateManager),
            CommandKind.GatesSelect => await BuildGatesBlessingSelection(normalizedCommand, fs, stateManager, commandArguments, select: true),
            CommandKind.GatesDeselect => await BuildGatesBlessingSelection(normalizedCommand, fs, stateManager, commandArguments, select: false),
            CommandKind.GatesReroll => await BuildGatesReroll(normalizedCommand, fs, stateManager),
            CommandKind.IncarnationPrepare => await BuildIncarnationPrepare(normalizedCommand, fs, stateManager),
            CommandKind.RelicForge => await BuildRelicForge(normalizedCommand, fs, stateManager),
            CommandKind.Treasury => await BuildTreasury(
                normalizedCommand,
                fs,
                includeAdvancedDiagnostics || stateManager.Settings.ShowGmThoughts),
            CommandKind.SourceOfLight => await BuildSourceOfLight(
                normalizedCommand,
                fs,
                includeAdvancedDiagnostics || stateManager.Settings.ShowGmThoughts),
            _ => null
        };
    }

    private static async Task<ExplorerCommandResult> BuildOverview(
        string command,
        string commandArguments,
        FileSystemManager fs,
        StateManager stateManager,
        bool includeAdvancedDiagnostics)
    {
        var shining = await ReadJson(fs, ShiningAbodeState.StatePath);
        var residents = await ReadJson(fs, GuardianAbodeResidentState.StatePath);
        var soul = await ReadJson(fs, SoulStatePath);
        var core = await ReadJson(fs, ShiningCoreActionRequestState.PendingActionsRequestPath);
        var trade = await ReadJson(fs, ShiningTradeRequestState.PendingRequestsPath);
        var source = await ReadJson(fs, SourceOfLightCapstoneState.PendingRequestPath);

        if (TryReadDetailSelector(commandArguments, out var gateSelector, "врата", "gate", "card", "карта", "благословение"))
            return BuildGatesCardDetail(command, shining.Node, gateSelector);
        if (TryReadDetailSelector(commandArguments, out var projectSelector, "проект", "project"))
            return BuildShiningProjectDetail(command, shining.Node, projectSelector);
        if (TryReadDetailSelector(commandArguments, out var pendingSelector, "ожидание", "pending", "request"))
            return BuildPendingCoreActionDetail(command, core.Node, pendingSelector);
        if (TryReadDetailSelector(commandArguments, out var receiptSelector, "исход", "receipt", "result", "итог"))
            return BuildCoreActionReceiptDetail(command, shining.Node, receiptSelector);

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
                    ("Фракций", SarefMainStoryState.GetPlayerVisibleShiningFactions(shining.Node as JsonObject).Count().ToString()),
                    ("Резидентов", CountArray(residents.Node, GuardianAbodeResidentState.EntriesProperty).ToString()),
                    ("Открытый черновик Врат", GetBoolText(shining.Node?["gates"]?["hasOpenDraft"])),
                    ("Ожиданий Обители", CountRequests(core).ToString()),
                    ("Ожиданий торговли", CountRequests(trade).ToString()),
                    ("Источник Света", DescribePresence(source))))
        };

        var actions = BuildShiningAbodeOverviewActions(shining.Node, core.Node).ToList();

        if (includeAdvancedDiagnostics)
        {
            AddRawOrWarning(blocks, $"Полный JSON {ShiningAbodeState.StatePath}", shining);
            AddRawOrWarning(blocks, $"Полный JSON {GuardianAbodeResidentState.StatePath}", residents);
            AddRawOrWarning(blocks, $"Полный JSON {SoulStatePath}", soul);
            AddRawOrWarning(blocks, $"Полный JSON {ShiningCoreActionRequestState.PendingActionsRequestPath}", core);
            AddRawOrWarning(blocks, $"Полный JSON {ShiningTradeRequestState.PendingRequestsPath}", trade);
            AddRawOrWarning(blocks, $"Полный JSON {SourceOfLightCapstoneState.PendingRequestPath}", source);
        }
        else
        {
            AddWarningIfMalformed(blocks, $"Полный JSON {ShiningAbodeState.StatePath}", shining);
            AddWarningIfMalformed(blocks, $"Полный JSON {GuardianAbodeResidentState.StatePath}", residents);
            AddWarningIfMalformed(blocks, $"Полный JSON {SoulStatePath}", soul);
            AddWarningIfMalformed(blocks, $"Полный JSON {ShiningCoreActionRequestState.PendingActionsRequestPath}", core);
            AddWarningIfMalformed(blocks, $"Полный JSON {ShiningTradeRequestState.PendingRequestsPath}", trade);
            AddWarningIfMalformed(blocks, $"Полный JSON {SourceOfLightCapstoneState.PendingRequestPath}", source);
        }

        return Completed(command, blocks, actions);
    }

    private static async Task<ExplorerCommandResult> BuildPolitics(
        string command,
        string commandArguments,
        FileSystemManager fs,
        bool includeAdvancedDiagnostics)
    {
        var shining = await ReadJson(fs, ShiningAbodeState.StatePath);
        var residents = await ReadJson(fs, GuardianAbodeResidentState.StatePath);
        var guardians = await ReadJson(fs, GuardiansPath);
        var foundings = await ReadJson(fs, ShiningFactionRequestState.PendingFoundingsRequestPath);
        var realignments = await ReadJson(fs, ShiningFactionRequestState.PendingRealignmentsRequestPath);
        var leadership = await ReadJson(fs, ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath);

        if (TryReadDetailSelector(commandArguments, out var factionSelector, "фракция", "faction"))
            return BuildShiningFactionDetail(command, shining.Node, factionSelector);
        if (TryReadDetailSelector(commandArguments, out var chronicleSelector, "хроника", "chronicle", "entry"))
            return BuildShiningChronicleDetail(command, shining.Node, chronicleSelector);
        if (TryReadDetailSelector(commandArguments, out var resourceSelector, "ресурс", "resource", "ledger"))
            return BuildShiningResourceDetail(command, shining.Node, resourceSelector);
        if (TryReadDetailSelector(commandArguments, out var politicalPendingSelector, "ожидание", "pending", "request"))
            return BuildPoliticalPendingDetail(command, foundings.Node, realignments.Node, leadership.Node, politicalPendingSelector);
        if (TryReadDetailSelector(commandArguments, out var resolutionSelector, "решение", "resolution", "receipt", "итог"))
            return BuildPoliticalResolutionDetail(command, shining.Node, resolutionSelector);

        var blocks = new List<UiBlock>
        {
            Panel("Политика Сияющей Обители",
                Grid(
                    ("Фракций", SarefMainStoryState.GetPlayerVisibleShiningFactions(shining.Node as JsonObject).Count().ToString()),
                    ("Светозарных акторов", CountArray(shining.Node, "shiningPoliticalActors").ToString()),
                    ("Кампаний против фракций", CountArray(shining.Node, ShiningAbodeState.FactionConflictCampaignsProperty).ToString()),
                    ("Резидентов", CountArray(residents.Node, GuardianAbodeResidentState.EntriesProperty).ToString()),
                    ("Ожиданий основания", CountRequests(foundings).ToString()),
                    ("Ожиданий перехода", CountRequests(realignments).ToString()),
                    ("Ожиданий смены власти", CountRequests(leadership).ToString())))
        };

        AddFactionPoliticalMemoryBlocks(blocks, shining.Node);
        var actions = BuildShiningPoliticsActions(shining.Node, foundings.Node, realignments.Node, leadership.Node).ToList();

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

        return Completed(command, blocks, actions);
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

    private static async Task<ExplorerCommandResult> BuildNativeFactionDiscovery(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var context = await ReadCoreActionPromptContext(fs, stateManager);
        if (context.Blocker != null)
            return Blocked(command, "Открытие нативной фракции недоступно", context.Blocker);

        if (context.ShiningRoot["pendingNativeFactionDiscovery"] is JsonObject)
            return Blocked(command, "Открытие нативной фракции уже ожидает", "Открытие новой нативной фракции уже ожидает решения ГМ.");

        var cost = ShiningAbodeState.GetNativeDiscoveryCost();
        var radianceTier = GetInt(context.ShiningRoot["radiance"], "tier");
        if (radianceTier < 1)
            return Blocked(command, "Сияния пока недостаточно", "Открытие нативной фракции доступно с первого тира Сияния.");

        var currentFeathers = GetSoulInkFeathers(context.SoulRoot);
        var currentSparks = GetInt(context.ShiningRoot["lightSparks"], 0);
        if (currentFeathers < cost.Feathers || currentSparks < cost.LightSparks)
        {
            return Blocked(
                command,
                "Не хватает ресурсов",
                $"Для открытия нужны {cost.Feathers} Чернильных Перьев и {cost.LightSparks} Искр Света. Сейчас доступно: {currentFeathers} Перьев и {currentSparks} Искр.");
        }

        var blocks = new List<UiBlock>
        {
            Panel("Открытие нативной фракции",
                Grid(
                    ("Сияние", $"тир {radianceTier}"),
                    ("Чернильные Перья", $"{currentFeathers} доступно / нужно {cost.Feathers}"),
                    ("Искры Света", $"{currentSparks} доступно / нужно {cost.LightSparks}"))),
            Message(
                UiNotificationSeverity.Info,
                "Запрос для ГМ",
                "После подтверждения браузер отправит просьбу об открытии нативной фракции через существующее ожидание Сияющей Обители.")
        };

        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiConfirmationPrompt
                {
                    Id = "confirm_shining_core_action_write",
                    Prompt = "Подтвердить открытие нативной фракции и стоимость",
                    Required = true
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildFactionInvestment(
        string command,
        FileSystemManager fs,
        StateManager stateManager,
        string commandArguments)
    {
        var context = await ReadCoreActionPromptContext(fs, stateManager);
        if (context.Blocker != null)
            return Blocked(command, "Инвестиция в сияющую фракцию недоступна", context.Blocker);

        var cost = ShiningAbodeState.GetFactionInvestmentCost();
        var currentFeathers = GetSoulInkFeathers(context.SoulRoot);
        var currentSparks = GetInt(context.ShiningRoot["lightSparks"], 0);
        if (currentFeathers < cost.Feathers || currentSparks < cost.LightSparks)
        {
            return Blocked(
                command,
                "Не хватает ресурсов",
                $"Для инвестиции нужны {cost.Feathers} Чернильных Перьев и {cost.LightSparks} Искр Света. Сейчас доступно: {currentFeathers} Перьев и {currentSparks} Искр.");
        }

        var factions = GetVisibleFactions(context.ShiningRoot)
            .Where(static faction => Math.Clamp(GetInt(faction["investCountThisAscension"], 0), 0, 3) < 3)
            .OrderBy(static faction => ResolveFactionDisplayName(faction), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (factions.Length == 0)
            return Blocked(command, "Нет доступных фракций", "Сейчас нет видимых сияющих фракций, куда можно вложиться в этом восхождении.");

        var requestedFactionId = ExtractFirstArgument(commandArguments);
        if (!string.IsNullOrWhiteSpace(requestedFactionId))
        {
            factions = factions
                .Where(faction => string.Equals(GetString(faction, "factionId", string.Empty), requestedFactionId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (factions.Length == 0)
                return Blocked(command, "Фракция недоступна", "Выберите видимую сияющую фракцию, в которую сейчас можно вложиться.");
        }

        var blocks = new List<UiBlock>
        {
            Panel("Инвестиция в сияющую фракцию",
                Grid(
                    ("Чернильные Перья", $"{currentFeathers} доступно / нужно {cost.Feathers}"),
                    ("Искры Света", $"{currentSparks} доступно / нужно {cost.LightSparks}")),
                new UiTextBlock { Text = "Доступные фракции:\n- " + string.Join("\n- ", factions.Select(BuildFactionInvestmentLabel)) }),
            Message(
                UiNotificationSeverity.Info,
                "Запрос для ГМ",
                "После подтверждения браузер отправит инвестицию через существующее ожидание Сияющей Обители.")
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
                    Prompt = "Фракция для инвестиции",
                    Required = true,
                    Options = factions
                        .Select(static faction => Option(
                            GetString(faction, "factionId", string.Empty),
                            BuildFactionInvestmentLabel(faction),
                            "Выберите видимую фракцию, которая ещё принимает инвестиции."))
                        .ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_shining_core_action_write",
                    Prompt = "Подтвердить инвестицию и стоимость",
                    Required = true
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildProjectSupportMutation(
        string command,
        FileSystemManager fs,
        StateManager stateManager,
        bool support)
    {
        var context = await ReadCoreActionPromptContext(fs, stateManager);
        var title = support ? "Поддержка сияющего проекта недоступна" : "Снятие поддержки сияющего проекта недоступно";
        if (context.Blocker != null)
            return Blocked(command, title, context.Blocker);

        if (support &&
            ShiningAbodeState.CountSupportedProjectsAcrossState(context.ShiningRoot) >=
            ShiningAbodeState.GetSupportedProjectCap(GetInt(context.ShiningRoot["radiance"], "tier")))
        {
            return Blocked(command, "Лимит поддержки достигнут", "Сейчас достигнут лимит поддерживаемых проектов Сияющей Обители.");
        }

        var projects = EnumerateVisibleProjectOptions(
                context.ShiningRoot,
                support
                    ? static project => IsCompletedProject(project) && !GetBool(project["isSupported"])
                    : static project => IsCompletedProject(project) && GetBool(project["isSupported"]))
            .ToArray();
        if (projects.Length == 0)
        {
            return Blocked(
                command,
                support ? "Нет проектов для поддержки" : "Нет поддерживаемых проектов",
                support
                    ? "Сейчас нет видимых завершённых проектов без поддержки."
                    : "Сейчас нет видимых проектов, с которых можно снять поддержку.");
        }

        var blocks = new List<UiBlock>
        {
            Panel(
                support ? "Поддержка сияющего проекта" : "Снятие поддержки сияющего проекта",
                new UiTextBlock { Text = "Доступные проекты:\n- " + string.Join("\n- ", projects.Select(static option => option.Label)) }),
            Message(
                UiNotificationSeverity.Info,
                "Запрос для ГМ",
                support
                    ? "После подтверждения браузер отправит просьбу поддержать выбранный проект."
                    : "После подтверждения браузер отправит просьбу снять поддержку с выбранного проекта.")
        };

        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            actions: projects
                .Select(static option => BuildProjectDetailAction(option.Value, ExtractProjectLabel(option.Label)))
                .OfType<UiAction>(),
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "project_choice",
                    Prompt = support ? "Проект для поддержки" : "Проект для снятия поддержки",
                    Required = true,
                    Options = projects
                        .Select(static option => Option(option.Value, option.Label, option.Description))
                        .ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_shining_core_action_write",
                    Prompt = support ? "Подтвердить поддержку проекта" : "Подтвердить снятие поддержки",
                    Required = true
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildProjectRetirement(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var context = await ReadCoreActionPromptContext(fs, stateManager);
        if (context.Blocker != null)
            return Blocked(command, "Отправка сияющего проекта в историю недоступна", context.Blocker);

        var projects = EnumerateVisibleProjectOptions(context.ShiningRoot, static project => IsCompletedProject(project))
            .ToArray();
        if (projects.Length == 0)
            return Blocked(command, "Нет проектов для истории", "Сейчас нет видимых завершённых проектов, которые можно отправить в историю.");

        var blocks = new List<UiBlock>
        {
            Panel(
                "Отправка сияющего проекта в историю",
                new UiTextBlock { Text = "Доступные проекты:\n- " + string.Join("\n- ", projects.Select(static option => option.Label)) }),
            Message(
                UiNotificationSeverity.Info,
                "Запрос для ГМ",
                "После подтверждения браузер отправит просьбу вывести выбранный проект из активного вклада фракции.")
        };

        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "project_choice",
                    Prompt = "Проект для истории",
                    Required = true,
                    Options = projects
                        .Select(static option => Option(option.Value, option.Label, option.Description))
                        .ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_shining_core_action_write",
                    Prompt = "Подтвердить отправку проекта в историю",
                    Required = true
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildGatesOpen(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var context = await ReadCoreActionPromptContext(fs, stateManager);
        if (context.Blocker != null)
            return Blocked(command, "Открытие Врат недоступно", context.Blocker);

        var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
        {
            ActionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
            CreatedAtTurn = Math.Max(1, stateManager.CurrentState.TurnNumber + 1)
        };
        var validation = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(fs, request);
        if (!string.IsNullOrWhiteSpace(validation))
            return Blocked(command, "Открытие Врат недоступно", SanitizeShiningGatesValidationMessage(validation));

        var blocks = new List<UiBlock>
        {
            Panel(
                "Открытие Врат инкарнации",
                Grid(
                    ("Сияние", DescribeRadiance(context.ShiningRoot)),
                    ("Текущий набор", DescribeGatesDraftState(context.ShiningRoot["gates"] as JsonObject))),
                new UiTextBlock { Text = "После подтверждения ГМ получит просьбу открыть Врата и показать благословения для будущей жизни." })
        };

        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiConfirmationPrompt
                {
                    Id = "confirm_shining_core_action_write",
                    Prompt = "Подтвердить открытие Врат",
                    Required = true
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildGatesBlessingSelection(
        string command,
        FileSystemManager fs,
        StateManager stateManager,
        string commandArguments,
        bool select)
    {
        var context = await ReadCoreActionPromptContext(fs, stateManager);
        if (context.Blocker != null)
            return Blocked(command, select ? "Выбор благословения недоступен" : "Снятие благословения недоступно", context.Blocker);
        if (!TryGetOpenFreshGatesForBrowser(context.ShiningRoot, out var gates, out var blocker))
            return Blocked(command, select ? "Выбор благословения недоступен" : "Снятие благословения недоступно", blocker);

        var requestedCardId = ExtractFirstArgument(commandArguments);
        var selectedIds = ReadGatesStringArray(gates, "selectedBlessingCardIds")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var availableCards = (gates["availableBlessingCards"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
        var optionCards = availableCards
            .Where(card =>
            {
                var cardId = GetString(card, "cardId", string.Empty);
                if (string.IsNullOrWhiteSpace(cardId))
                    return false;
                if (!string.IsNullOrWhiteSpace(requestedCardId) && !string.Equals(cardId, requestedCardId, StringComparison.OrdinalIgnoreCase))
                    return false;
                return select || selectedIds.Contains(cardId);
            })
            .ToArray();

        if (optionCards.Length == 0)
        {
            return Blocked(
                command,
                select ? "Нет доступного благословения" : "Нет выбранного благословения",
                select
                    ? "В текущем наборе Врат нет подходящих благословений для выбора."
                    : "Сейчас в Вратах нет выбранных благословений, которые можно снять.");
        }

        var blocks = new List<UiBlock>
        {
            Panel(
                select ? "Выбор благословения Врат" : "Снятие благословения Врат",
                Grid(
                    ("Набор Врат", DescribeGatesDraftState(gates)),
                    ("Выбрано", selectedIds.Count == 0 ? "пока ничего" : string.Join(", ", selectedIds.Select(id => ResolveGatesCardLabel(gates, id))))),
                new UiTextBlock { Text = "Доступные благословения:\n- " + string.Join("\n- ", availableCards.Select(card => BuildBlessingCardLabel(card, selectedIds))) })
        };

        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            actions: optionCards
                .Select(static card => BuildGateDetailAction(card))
                .OfType<UiAction>(),
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "blessing_card_id",
                    Prompt = select ? "Благословение для выбора" : "Благословение для снятия",
                    Required = true,
                    Options = optionCards
                        .Select(card => Option(
                            GetString(card, "cardId", string.Empty),
                            BuildBlessingCardPromptLabel(card, selectedIds),
                            BuildBlessingCardDescription(card)))
                        .ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_shining_gates_local_write",
                    Prompt = select ? "Подтвердить выбор благословения" : "Подтвердить снятие благословения",
                    Required = true
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildGatesReroll(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var context = await ReadCoreActionPromptContext(fs, stateManager);
        if (context.Blocker != null)
            return Blocked(command, "Обновление Врат недоступно", context.Blocker);
        if (!TryGetOpenFreshGatesForBrowser(context.ShiningRoot, out var gates, out var blocker))
            return Blocked(command, "Обновление Врат недоступно", blocker);

        var projectedRoot = JsonNode.Parse(context.ShiningRoot.ToJsonString())!.AsObject();
        if (!ShiningAbodeState.TryRerollGatesDraft(projectedRoot, out var error))
            return Blocked(command, "Обновление Врат недоступно", SanitizeShiningGatesValidationMessage(error));

        var projectedGates = projectedRoot["gates"] as JsonObject;
        var blocks = new List<UiBlock>
        {
            Panel(
                "Обновление Врат",
                Grid(
                    ("Осталось обновлений", $"{GetInt(gates["rerollsRemaining"], 0)} -> {GetInt(projectedGates?["rerollsRemaining"], 0)}"),
                    ("Текущие благословения", FormatGatesCardNames(gates)),
                    ("После обновления", FormatGatesCardNames(projectedGates))),
                new UiTextBlock { Text = "Выбранные благословения сохраняются, а Врата заменят часть невыбранного набора." })
        };

        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiConfirmationPrompt
                {
                    Id = "confirm_shining_gates_local_write",
                    Prompt = "Подтвердить обновление Врат",
                    Required = true
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildIncarnationPrepare(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var context = await ReadCoreActionPromptContext(fs, stateManager);
        if (context.Blocker != null)
            return Blocked(command, "Подготовка новой жизни недоступна", context.Blocker);
        if (!TryGetOpenFreshGatesForBrowser(context.ShiningRoot, out var gates, out var blocker))
            return Blocked(command, "Подготовка новой жизни недоступна", blocker);

        var selectedIds = ReadGatesStringArray(gates, "selectedBlessingCardIds");
        if (selectedIds.Count == 0)
            return Blocked(command, "Нет выбранных благословений", "Перед подготовкой новой жизни выберите хотя бы одно благословение Врат.");

        var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
        {
            ActionType = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
            SourceDraftVersion = GetInt(gates["draftVersion"], 0),
            SelectedCardIds = selectedIds,
            SelectedCards = BuildSelectedGatesCardSnapshot(gates, selectedIds),
            CreatedAtTurn = Math.Max(1, stateManager.CurrentState.TurnNumber + 1)
        };
        var validation = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(fs, request);
        if (!string.IsNullOrWhiteSpace(validation))
            return Blocked(command, "Подготовка новой жизни недоступна", SanitizeShiningGatesValidationMessage(validation));

        var blocks = new List<UiBlock>
        {
            Panel(
                "Подготовка новой жизни",
                Grid(
                    ("Набор Врат", DescribeGatesDraftState(gates)),
                    ("Выбранные благословения", string.Join(", ", selectedIds.Select(id => ResolveGatesCardLabel(gates, id))))),
                new UiTextBlock { Text = "После подтверждения ГМ получит просьбу закрепить выбранные благословения для следующей жизни." })
        };

        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiConfirmationPrompt
                {
                    Id = "confirm_shining_core_action_write",
                    Prompt = "Подтвердить подготовку новой жизни",
                    Required = true
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildRelicForge(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var context = await ReadCoreActionPromptContext(fs, stateManager);
        if (context.Blocker != null)
            return Blocked(command, "Ковка реликвий недоступна", context.Blocker);

        var currentFeathers = GetSoulInkFeathers(context.SoulRoot);
        var currentSparks = GetInt(context.ShiningRoot["lightSparks"], 0);
        var radianceTier = GetInt(context.ShiningRoot["radiance"], "tier");
        var factions = GetVisibleFactions(context.ShiningRoot)
            .Where(static faction => ShiningAbodeState.FactionHasSupportedProjectArchetype(faction, ShiningAbodeState.ProjectArchetypeRefinement))
            .OrderBy(static faction => ResolveFactionDisplayName(faction), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (factions.Length == 0)
            return Blocked(command, "Нет фракции-кузни", "Сейчас нет видимой действующей сияющей фракции с поддержанным проектом огранки реликвий.");

        var relics = EnumerateSoulRelics(context.SoulRoot)
            .OrderBy(static relic => relic.RelicName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (relics.Length == 0)
            return Blocked(command, "Нет реликвий души", "У души сейчас нет реликвий, которые можно передать на сияющую ковку.");

        var actionOptions = BuildForgeActionOptions(context.ShiningRoot, context.SoulRoot, context.ResidentsRoot, factions, relics).ToList();
        if (actionOptions.All(static option => option.Disabled))
            return Blocked(command, "Ковка пока недоступна", "Текущего сияния или ресурсов не хватает ни на один вид ковки реликвий.");

        var propertyOptions = BuildForgePropertyChoiceOptions(relics).ToList();
        var replacementOptions = BuildForgeReplacementPropertyOptions(context.SoulRoot).ToList();
        var addedPropertyOptions = BuildForgeAddedPropertyOptions(context.SoulRoot).ToList();
        var rerolls = ShiningBlessingEffectState.GetPendingRelicRerolls(context.SoulRoot);

        var blocks = new List<UiBlock>
        {
            Panel(
                "Ковка реликвий Сияющей Обители",
                Grid(
                    ("Сияние", $"тир {radianceTier}"),
                    ("Чернильные Перья", currentFeathers.ToString()),
                    ("Искры Света", currentSparks.ToString()),
                    ("Перебросы благословения", rerolls.ToString())),
                new UiTextBlock { Text = "Фракции-кузницы:\n- " + string.Join("\n- ", factions.Select(BuildForgeFactionLabel)) },
                new UiTextBlock { Text = "Реликвии души:\n- " + string.Join("\n- ", relics.Select(BuildSoulRelicForgeLabel)) }),
            Message(
                UiNotificationSeverity.Info,
                "Запрос для ГМ",
                "После подтверждения ГМ получит просьбу о перековке реликвии через Сияющую Обитель. Браузер не меняет реликвию напрямую.")
        };

        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            actions: BuildForgeRelicDetailActions(relics).ToList(),
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "faction_id",
                    Prompt = "Фракция-кузница",
                    Required = true,
                    Options = factions
                        .Select(static faction => Option(
                            GetString(faction, "factionId", string.Empty),
                            ResolveFactionDisplayName(faction),
                            "Эта фракция поддерживает огранку реликвий."))
                        .ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "forge_action_type",
                    Prompt = "Действие ковки",
                    Required = true,
                    Options = actionOptions
                },
                new UiSelectionPrompt
                {
                    Id = "relic_id",
                    Prompt = "Реликвия души",
                    Required = true,
                    Options = relics
                        .Select(static relic => Option(
                            relic.RelicId,
                            relic.RelicName,
                            $"{DescribeForgeRarity(ResolveForgeRarityKey(relic.Relic))}; форма {DescribeForgeFormTag(GetString(relic.Relic, "formTag", string.Empty))}; {DescribeSoulRelicCollection(relic.Collection)}."))
                        .ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "target_form_tag",
                    Prompt = "Новая форма для перековки",
                    AllowCustom = true,
                    Options = BuildForgeFormTagOptions(relics).ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "property_choice",
                    Prompt = "Свойство реликвии",
                    Options = propertyOptions
                },
                new UiSelectionPrompt
                {
                    Id = "replacement_property_choice",
                    Prompt = "Новое свойство",
                    Options = replacementOptions
                },
                new UiSelectionPrompt
                {
                    Id = "added_properties_choice",
                    Prompt = "Дополнительное свойство для новой редкости",
                    AllowCustom = true,
                    Options = addedPropertyOptions
                },
                new UiSelectionPrompt
                {
                    Id = "relic_rerolls_to_commit",
                    Prompt = "Перебросы благословения",
                    Options = BuildForgeRerollOptions(rerolls).ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_shining_relic_forge_write",
                    Prompt = "Подтвердить запрос ковки",
                    Required = true
                }
            ]);
    }

    private static IEnumerable<UiAction> BuildForgeRelicDetailActions(
        IEnumerable<(string RelicId, string RelicName, string Collection, JsonObject Relic)> relics)
    {
        foreach (var relic in relics)
        {
            if (string.IsNullOrWhiteSpace(relic.RelicId))
                continue;

            yield return DetailAction(
                "shining-forge-relic-detail",
                relic.RelicId,
                relic.RelicName,
                "/soul_relics реликвия " + SoulRelicEquipmentService.FormatCommandArgument(relic.RelicId));
        }
    }

    private static async Task<ExplorerCommandResult> BuildTreasury(
        string command,
        FileSystemManager fs,
        bool includeAdvancedDiagnostics)
    {
        var shining = await ReadJson(fs, ShiningAbodeState.StatePath);
        var soul = await ReadJson(fs, SoulStatePath);
        var treasury = shining.Node?["treasury"];

        var blocks = new List<UiBlock>
        {
            Panel("Казначейство Сияющей Обители",
                Grid(
                    ("Доступное действие", "операции казначейства доступны через форму"),
                    ("Чернильные Перья души", DescribeInkFeathers(soul.Node)),
                    ("Искры Света", GetNumberOrString(shining.Node, "lightSparks", "0")),
                    ("Вклад Перьями", GetNumberOrString(treasury, "depositedInkFeathers", "0")),
                    ("Проценты к получению", GetNumberOrString(treasury, "claimableInkFeatherInterest", "0")),
                    ("Цикл процентов", GetString(treasury, "lastInterestSettlementCycleId", "не указан")),
                    ("Цикл обмена", GetString(treasury, "exchangeCycleId", "не указан")),
                    ("Искр обменяно в цикле", GetNumberOrString(treasury, "exchangeThisCycleLightSparks", "0")))),
            Message(
                UiNotificationSeverity.Info,
                "Запись казначейства",
                "Выберите операцию и сумму; при активном ходе ГМа запись дождётся завершения текущего ответа.")
        };

        if (includeAdvancedDiagnostics)
        {
            AddRawOrWarning(blocks, $"Полный JSON {ShiningAbodeState.StatePath}", shining);
            AddRawOrWarning(blocks, $"Полный JSON {SoulStatePath}", soul);
        }
        else
        {
            AddPlayerSafeMalformedWarning(blocks, "Казначейство требует проверки", shining);
            AddPlayerSafeMalformedWarning(blocks, "Сведения души требуют проверки", soul);
        }

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

    private static async Task<ExplorerCommandResult> BuildSourceOfLight(
        string command,
        FileSystemManager fs,
        bool includeAdvancedDiagnostics)
    {
        var shining = await ReadJson(fs, ShiningAbodeState.StatePath);
        var soul = await ReadJson(fs, SoulStatePath);
        var pending = await ReadJson(fs, SourceOfLightCapstoneState.PendingRequestPath);

        var status = DescribeSourceOfLightStatus(shining.Node, soul.Node, pending);
        var blocks = new List<UiBlock>
        {
            Panel("Источник Света",
                Grid(
                    ("Доступное действие", "просьба открыть Источник доступна через форму"),
                    ("Статус", status),
                    ("Сияние", DescribeRadiance(shining.Node)),
                    ("Дар души", "Воплощение Света"),
                    ("Реликвия души", "Воплощенный Свет"))),
            Message(
                UiNotificationSeverity.Info,
                "Сцена Источника",
                "Если требования полного Сияния выполнены, подтверждение попросит ГМ разыграть сцену Источника.")
        };

        if (includeAdvancedDiagnostics)
        {
            AddRawOrWarning(blocks, $"Полный JSON {ShiningAbodeState.StatePath}", shining);
            AddRawOrWarning(blocks, $"Полный JSON {SoulStatePath}", soul);
            AddRawOrWarning(blocks, $"Полный JSON {SourceOfLightCapstoneState.PendingRequestPath}", pending);
        }
        else
        {
            AddPlayerSafeMalformedWarning(blocks, "Состояние Обители требует проверки", shining);
            AddPlayerSafeMalformedWarning(blocks, "Сведения души требуют проверки", soul);
            AddPlayerSafeMalformedWarning(blocks, "Ожидание Источника требует проверки", pending);
        }

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
                        Option("open", "Открыть Источник", "Попросить ГМ разыграть вершинную сцену полного Сияния.")
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

    private static void AddPlayerSafeMalformedWarning(List<UiBlock> blocks, string title, JsonReadResult read)
    {
        if (read.Node == null && read.FileExists)
        {
            blocks.Add(Message(
                UiNotificationSeverity.Warning,
                title,
                "Часть сведений повреждена или недоступна. Попросите ГМ проверить состояние перед действием."));
        }
    }

    private static IEnumerable<UiAction> BuildShiningAbodeOverviewActions(JsonNode? shiningRoot, JsonNode? pendingCoreRoot)
    {
        if (shiningRoot is JsonObject root)
        {
            foreach (var card in EnumerateVisibleGateCards(root["gates"] as JsonObject))
            {
                var action = BuildGateDetailAction(card);
                if (action != null)
                    yield return action;
            }

            foreach (var faction in GetVisibleFactions(root))
            {
                var factionAction = BuildFactionDetailAction(faction);
                if (factionAction != null)
                    yield return factionAction;

                var factionId = GetString(faction, "factionId", string.Empty);
                if (string.IsNullOrWhiteSpace(factionId) || faction["projects"] is not JsonArray projects)
                    continue;

                foreach (var project in projects.OfType<JsonObject>().Where(IsPlayerVisibleMemoryObject))
                {
                    var projectAction = BuildProjectDetailAction(factionId, project);
                    if (projectAction != null)
                        yield return projectAction;
                }
            }

            foreach (var receipt in EnumerateObjects(root["coreActionReceipts"] as JsonArray))
            {
                var action = BuildCoreReceiptDetailAction(receipt);
                if (action != null)
                    yield return action;
            }
        }

        foreach (var request in EnumerateRequestObjects(pendingCoreRoot))
        {
            var action = BuildPendingCoreDetailAction(request);
            if (action != null)
                yield return action;
        }
    }

    private static IEnumerable<UiAction> BuildShiningPoliticsActions(
        JsonNode? shiningRoot,
        JsonNode? pendingFoundingsRoot,
        JsonNode? pendingRealignmentsRoot,
        JsonNode? pendingLeadershipRoot)
    {
        if (shiningRoot is JsonObject root)
        {
            foreach (var faction in GetVisibleFactions(root))
            {
                var factionAction = BuildFactionDetailAction(faction);
                if (factionAction != null)
                    yield return factionAction;

                foreach (var entry in EnumeratePlayerVisibleChronicleEntries(faction))
                {
                    var action = BuildChronicleDetailAction(entry);
                    if (action != null)
                        yield return action;
                }

                foreach (var entry in EnumerateCurrentResourceBalances(faction))
                {
                    var action = BuildResourceDetailAction(entry);
                    if (action != null)
                        yield return action;
                }
            }

            foreach (var receipt in EnumeratePoliticalResolutionReceipts(root))
            {
                var action = BuildPoliticalResolutionAction(receipt);
                if (action != null)
                    yield return action;
            }
        }

        foreach (var request in EnumerateRequestObjects(pendingFoundingsRoot)
                     .Concat(EnumerateRequestObjects(pendingRealignmentsRoot))
                     .Concat(EnumerateRequestObjects(pendingLeadershipRoot)))
        {
            var action = BuildPoliticalPendingAction(request);
            if (action != null)
                yield return action;
        }
    }

    private static ExplorerCommandResult BuildGatesCardDetail(string command, JsonNode? shiningRoot, string selector)
    {
        var gates = shiningRoot?["gates"] as JsonObject;
        var card = FindGatesCard(gates, selector);
        if (card == null || !IsPlayerVisibleMemoryObject(card))
            return UnavailableDetail(command, "Благословение Врат недоступно");

        var cardId = GetString(card, "cardId", selector);
        var selected = ReadGatesStringArray(gates, "selectedBlessingCardIds")
            .Contains(cardId, StringComparer.OrdinalIgnoreCase);
        var name = GetString(card, "displayName", cardId);
        var blocks = new List<UiBlock>
        {
            Panel(
                $"Благословение Врат: {name}",
                Grid(
                    ("Семья эффекта", DescribeEffectFamily(GetString(card, "effectFamily", string.Empty))),
                    ("Редкость", DescribeRarity(GetString(card, "rarity", string.Empty))),
                    ("Состояние", selected ? "выбрано" : "доступно")),
                new UiTextBlock { Text = GetString(card, "displaySummary", "Сводка благословения пока не записана.") })
        };

        return Completed(command, blocks);
    }

    private static ExplorerCommandResult BuildShiningProjectDetail(string command, JsonNode? shiningRoot, string selector)
    {
        if (shiningRoot is not JsonObject root ||
            !TryFindVisibleProject(root, selector, out var faction, out var project))
        {
            return UnavailableDetail(command, "Проект Сияющей Обители недоступен");
        }

        var name = ResolveProjectDisplayName(project);
        var blocks = new List<UiBlock>
        {
            Panel(
                $"Проект Сияющей Обители: {name}",
                Grid(
                    ("Фракция", ResolveFactionDisplayName(faction)),
                    ("Состояние", DescribeProjectStatus(project)),
                    ("Поддержка", DescribeProjectSupport(project)),
                    ("Тир", GetNumberOrString(project, "tier", "0"))),
                new UiTextBlock { Text = GetString(project, "summary", "Сводка проекта пока не записана.") })
        };

        return Completed(command, blocks);
    }

    private static ExplorerCommandResult BuildPendingCoreActionDetail(string command, JsonNode? pendingRoot, string selector)
    {
        var request = FindRequestObject(pendingRoot, selector);
        if (request == null)
            return UnavailableDetail(command, "Ожидающее действие Обители недоступно");

        var label = DescribeCoreActionType(GetString(request, "actionType", string.Empty));
        var blocks = new List<UiBlock>
        {
            Panel(
                $"Ожидающее действие Обители: {label}",
                Grid(
                    ("Создано", FormatCreatedTurn(request)),
                    ("Состояние", "ожидает решения ГМ")),
                new UiTextBlock { Text = GetString(request, "summary", "Сводка ожидания пока не записана.") })
        };

        return Completed(command, blocks);
    }

    private static ExplorerCommandResult BuildCoreActionReceiptDetail(string command, JsonNode? shiningRoot, string selector)
    {
        var receipt = FindReceiptObject(shiningRoot?["coreActionReceipts"], selector);
        if (receipt == null)
            return UnavailableDetail(command, "Итог действия Обители недоступен");

        var summary = GetString(receipt, "summary", string.Empty);
        var title = FirstNonEmpty(ExtractLeadingTitle(summary), DescribeCoreActionType(GetString(receipt, "actionType", string.Empty)), "действие завершено");
        var blocks = new List<UiBlock>
        {
            Panel(
                $"Итог действия Обители: {title}",
                Grid(
                    ("Статус", DescribeResolutionStatus(GetString(receipt, "status", string.Empty))),
                    ("Ход", GetNumberOrString(receipt, "resolvedAtTurn", "не указан"))),
                new UiTextBlock { Text = string.IsNullOrWhiteSpace(summary) ? "Сводка итога пока не записана." : summary })
        };

        return Completed(command, blocks);
    }

    private static ExplorerCommandResult BuildShiningFactionDetail(string command, JsonNode? shiningRoot, string selector)
    {
        if (shiningRoot is not JsonObject root)
            return UnavailableDetail(command, "Фракция Сияющей Обители недоступна");

        var faction = GetVisibleFactions(root).FirstOrDefault(item => MatchesAnyIdentity(item, selector, "factionId"));
        if (faction == null)
            return UnavailableDetail(command, "Фракция Сияющей Обители недоступна");

        var name = ResolveFactionDisplayName(faction);
        var blocks = new List<UiBlock>
        {
            Panel(
                $"Фракция Сияющей Обители: {name}",
                Grid(
                    ("Сила", GetNumberOrString(faction, "factionStrength", "0")),
                    ("Состояние", DescribeFactionLifecycle(faction)),
                    ("Глава", DescribeFactionLeader(faction))),
                new UiTextBlock { Text = GetString(faction["charter"], "summary", "Хартия фракции пока не записана.") })
        };

        return Completed(command, blocks);
    }

    private static ExplorerCommandResult BuildShiningChronicleDetail(string command, JsonNode? shiningRoot, string selector)
    {
        var match = FindVisibleChronicleEntry(shiningRoot, selector);
        if (match.Entry == null)
            return UnavailableDetail(command, "Хроника фракции недоступна");

        var title = FirstNonEmpty(GetString(match.Entry, "displayName", string.Empty), TranslateEventType(GetString(match.Entry, "eventType", "событие")));
        var blocks = new List<UiBlock>
        {
            Panel(
                $"Хроника фракции: {title}",
                Grid(
                    ("Фракция", ResolveFactionDisplayName(match.Faction!)),
                    ("Ход", GetNumberOrString(match.Entry, "turnNumber", "0")),
                    ("Событие", TranslateEventType(GetString(match.Entry, "eventType", "событие"))),
                    ("Последствия", DescribeConsequences(match.Entry))),
                new UiTextBlock { Text = GetString(match.Entry, "summary", "Сводка хроники пока не записана.") })
        };

        return Completed(command, blocks);
    }

    private static ExplorerCommandResult BuildShiningResourceDetail(string command, JsonNode? shiningRoot, string selector)
    {
        var match = FindVisibleResourceEntry(shiningRoot, selector);
        if (match.Entry == null)
            return UnavailableDetail(command, "Ресурс фракции недоступен");

        var resourceName = TranslateResourceType(GetString(match.Entry, "resourceType", "resource"));
        var blocks = new List<UiBlock>
        {
            Panel(
                $"Ресурс фракции: {resourceName}",
                Grid(
                    ("Фракция", ResolveFactionDisplayName(match.Faction!)),
                    ("Баланс", GetNumberOrString(match.Entry, "balanceAfter", "0")),
                    ("Изменение", DescribeSignedDelta(match.Entry["delta"])),
                    ("Ход", GetNumberOrString(match.Entry, "turnNumber", "0"))),
                new UiTextBlock { Text = GetString(match.Entry, "reason", "Причина изменения пока не записана.") })
        };

        return Completed(command, blocks);
    }

    private static ExplorerCommandResult BuildPoliticalPendingDetail(
        string command,
        JsonNode? foundingsRoot,
        JsonNode? realignmentsRoot,
        JsonNode? leadershipRoot,
        string selector)
    {
        var request = FindRequestObject(foundingsRoot, selector) ??
                      FindRequestObject(realignmentsRoot, selector) ??
                      FindRequestObject(leadershipRoot, selector);
        if (request == null)
            return UnavailableDetail(command, "Ожидающее решение фракций недоступно");

        var name = ResolvePoliticalRequestDisplayName(request);
        var blocks = new List<UiBlock>
        {
            Panel(
                $"Ожидающее решение фракций: {name}",
                Grid(
                    ("Создано", FormatCreatedTurn(request)),
                    ("Состояние", "ожидает решения ГМ")),
                new UiTextBlock { Text = GetString(request, "summary", "Сводка ожидания пока не записана.") })
        };

        return Completed(command, blocks);
    }

    private static ExplorerCommandResult BuildPoliticalResolutionDetail(string command, JsonNode? shiningRoot, string selector)
    {
        var receipt = FindPoliticalResolutionReceipt(shiningRoot, selector);
        if (receipt == null)
            return UnavailableDetail(command, "Решение фракций недоступно");

        var name = ResolvePoliticalReceiptDisplayName(receipt);
        var blocks = new List<UiBlock>
        {
            Panel(
                $"Решение фракций: {name}",
                Grid(
                    ("Статус", DescribeResolutionStatus(GetString(receipt, "status", string.Empty))),
                    ("Ход", GetNumberOrString(receipt, "resolvedAtTurn", "не указан"))),
                new UiTextBlock { Text = GetString(receipt, "summary", "Сводка решения пока не записана.") })
        };

        return Completed(command, blocks);
    }

    private static ExplorerCommandResult UnavailableDetail(string command, string title) =>
        Completed(command, [Message(UiNotificationSeverity.Info, title, "Не удалось открыть выбранную запись: она скрыта, устарела или уже исчезла из видимой памяти Обители.")]);

    private static IEnumerable<JsonObject> EnumerateVisibleGateCards(JsonObject? gates)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var propertyName in new[] { "availableBlessingCards", "allCandidateBlessingCards" })
        {
            if (gates?[propertyName] is not JsonArray cards)
                continue;

            foreach (var card in cards.OfType<JsonObject>().Where(IsPlayerVisibleMemoryObject))
            {
                var cardId = GetString(card, "cardId", string.Empty);
                if (string.IsNullOrWhiteSpace(cardId) || !seen.Add(cardId))
                    continue;
                yield return card;
            }
        }
    }

    private static IEnumerable<JsonObject> EnumerateRequestObjects(JsonNode? root)
    {
        if (root is JsonArray array)
            return array.OfType<JsonObject>();
        return root?["requests"] is JsonArray requests
            ? requests.OfType<JsonObject>()
            : Enumerable.Empty<JsonObject>();
    }

    private static IEnumerable<JsonObject> EnumeratePoliticalResolutionReceipts(JsonObject root)
    {
        foreach (var propertyName in new[] { "factionFoundingReceipts", "factionRealignmentReceipts", "politicalResolutionReceipts" })
        {
            foreach (var receipt in EnumerateObjects(root[propertyName] as JsonArray))
                yield return receipt;
        }

        foreach (var faction in SarefMainStoryState
                     .GetPlayerVisibleShiningFactions(root)
                     .Where(IsPlayerVisibleMemoryObject))
        {
            foreach (var receipt in EnumerateObjects(faction["leadershipReceipts"] as JsonArray))
                yield return receipt;
            foreach (var receipt in EnumerateObjects(faction["politicalResolutionReceipts"] as JsonArray))
                yield return receipt;
        }
    }

    private static UiAction? BuildGateDetailAction(JsonObject card)
    {
        var cardId = GetString(card, "cardId", string.Empty);
        if (string.IsNullOrWhiteSpace(cardId))
            return null;
        return DetailAction("shining-gate-detail", cardId, GetString(card, "displayName", cardId), $"/shining_abode врата {cardId}");
    }

    private static UiAction? BuildFactionDetailAction(JsonObject faction)
    {
        var factionId = GetString(faction, "factionId", string.Empty);
        if (string.IsNullOrWhiteSpace(factionId))
            return null;
        return DetailAction("shining-faction-detail", factionId, ResolveFactionDisplayName(faction), $"/shining_politics фракция {factionId}");
    }

    private static UiAction? BuildProjectDetailAction(string value, string label)
    {
        if (!TrySplitProjectSelector(value, out var factionId, out var projectId))
            return null;
        return DetailAction(
            "shining-project-detail",
            $"{factionId}-{projectId}",
            label,
            $"/shining_abode проект {factionId}::{projectId}");
    }

    private static UiAction? BuildProjectDetailAction(string factionId, JsonObject project)
    {
        var projectId = GetString(project, "projectId", string.Empty);
        return string.IsNullOrWhiteSpace(factionId) || string.IsNullOrWhiteSpace(projectId)
            ? null
            : BuildProjectDetailAction($"{factionId}|{projectId}", ResolveProjectDisplayName(project));
    }

    private static UiAction? BuildPendingCoreDetailAction(JsonObject request)
    {
        var identity = FirstNonEmpty(GetString(request, "requestId", string.Empty), GetString(request, "id", string.Empty));
        if (string.IsNullOrWhiteSpace(identity))
            return null;
        return DetailAction("shining-pending-core-detail", identity, DescribeCoreActionType(GetString(request, "actionType", string.Empty)), $"/shining_abode ожидание {identity}");
    }

    private static UiAction? BuildCoreReceiptDetailAction(JsonObject receipt)
    {
        var identity = FirstNonEmpty(GetString(receipt, "receiptId", string.Empty), GetString(receipt, "id", string.Empty));
        if (string.IsNullOrWhiteSpace(identity))
            return null;
        var label = FirstNonEmpty(ExtractLeadingTitle(GetString(receipt, "summary", string.Empty)), DescribeCoreActionType(GetString(receipt, "actionType", string.Empty)), "действие завершено");
        return DetailAction("shining-core-receipt-detail", identity, label, $"/shining_abode исход {identity}");
    }

    private static UiAction? BuildChronicleDetailAction(JsonObject entry)
    {
        var identity = GetString(entry, "entryId", string.Empty);
        if (string.IsNullOrWhiteSpace(identity))
            return null;
        var label = FirstNonEmpty(GetString(entry, "displayName", string.Empty), GetString(entry, "summary", identity));
        return DetailAction("shining-chronicle-detail", identity, label, $"/shining_politics хроника {identity}");
    }

    private static UiAction? BuildResourceDetailAction(JsonObject entry)
    {
        var identity = GetString(entry, "entryId", string.Empty);
        if (string.IsNullOrWhiteSpace(identity))
            return null;
        var label = TranslateResourceType(GetString(entry, "resourceType", "resource"));
        return DetailAction("shining-resource-detail", identity, label, $"/shining_politics ресурс {identity}");
    }

    private static UiAction? BuildPoliticalPendingAction(JsonObject request)
    {
        var identity = FirstNonEmpty(GetString(request, "requestId", string.Empty), GetString(request, "id", string.Empty));
        if (string.IsNullOrWhiteSpace(identity))
            return null;
        return DetailAction("shining-political-pending-detail", identity, ResolvePoliticalRequestDisplayName(request), $"/shining_politics ожидание {identity}");
    }

    private static UiAction? BuildPoliticalResolutionAction(JsonObject receipt)
    {
        var identity = FirstNonEmpty(GetString(receipt, "receiptId", string.Empty), GetString(receipt, "id", string.Empty), GetString(receipt, "requestId", string.Empty));
        if (string.IsNullOrWhiteSpace(identity))
            return null;
        return DetailAction("shining-political-resolution-detail", identity, ResolvePoliticalReceiptDisplayName(receipt), $"/shining_politics решение {identity}");
    }

    private static UiAction DetailAction(string idPrefix, string identity, string label, string command) =>
        new()
        {
            Id = $"{idPrefix}-{ToActionIdPart(identity)}",
            Label = $"Подробно: «{label}».",
            Command = command,
            Style = UiActionStyle.Secondary,
            RequiresConfirmation = false
        };

    private static bool TryReadDetailSelector(string arguments, out string selector, params string[] tokens)
    {
        selector = string.Empty;
        var parts = arguments
            .Split([' ', '\t', '\r', '\n'], 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;

        if (!tokens.Any(token => string.Equals(parts[0], token, StringComparison.OrdinalIgnoreCase)))
            return false;

        selector = parts[1].Trim();
        return !string.IsNullOrWhiteSpace(selector);
    }

    private static bool TryFindVisibleProject(JsonObject root, string selector, out JsonObject faction, out JsonObject project)
    {
        faction = new JsonObject();
        project = new JsonObject();
        var hasComposite = TrySplitProjectSelector(selector, out var selectedFactionId, out var selectedProjectId);
        var selectedProjectOnly = hasComposite ? selectedProjectId : selector;

        foreach (var candidateFaction in GetVisibleFactions(root))
        {
            var factionId = GetString(candidateFaction, "factionId", string.Empty);
            if (hasComposite && !string.Equals(factionId, selectedFactionId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (candidateFaction["projects"] is not JsonArray projects)
                continue;

            foreach (var candidateProject in projects.OfType<JsonObject>().Where(IsPlayerVisibleMemoryObject))
            {
                if (!MatchesAnyIdentity(candidateProject, selectedProjectOnly, "projectId", "id", "displayName", "projectName"))
                    continue;

                faction = candidateFaction;
                project = candidateProject;
                return true;
            }
        }

        return false;
    }

    private static (JsonObject? Faction, JsonObject? Entry) FindVisibleChronicleEntry(JsonNode? shiningRoot, string selector)
    {
        if (shiningRoot is not JsonObject root)
            return (null, null);

        foreach (var faction in GetVisibleFactions(root))
        {
            foreach (var entry in EnumeratePlayerVisibleChronicleEntries(faction))
            {
                if (MatchesAnyIdentity(entry, selector, "entryId", "id", "displayName"))
                    return (faction, entry);
            }
        }

        return (null, null);
    }

    private static (JsonObject? Faction, JsonObject? Entry) FindVisibleResourceEntry(JsonNode? shiningRoot, string selector)
    {
        if (shiningRoot is not JsonObject root)
            return (null, null);

        foreach (var faction in GetVisibleFactions(root))
        {
            foreach (var entry in EnumerateCurrentResourceBalances(faction))
            {
                if (MatchesAnyIdentity(entry, selector, "entryId", "id", "resourceType"))
                    return (faction, entry);
            }
        }

        return (null, null);
    }

    private static JsonObject? FindRequestObject(JsonNode? root, string selector) =>
        EnumerateRequestObjects(root).FirstOrDefault(request =>
            MatchesAnyIdentity(request, selector, "requestId", "id", "proposedFactionId", "factionId", "proposedFactionName"));

    private static JsonObject? FindReceiptObject(JsonNode? root, string selector) =>
        (root as JsonArray)?.OfType<JsonObject>().FirstOrDefault(receipt =>
            MatchesAnyIdentity(receipt, selector, "receiptId", "id", "requestId"));

    private static JsonObject? FindPoliticalResolutionReceipt(JsonNode? shiningRoot, string selector) =>
        shiningRoot is JsonObject root
            ? EnumeratePoliticalResolutionReceipts(root).FirstOrDefault(receipt =>
                MatchesAnyIdentity(receipt, selector, "receiptId", "id", "requestId", "factionId", "factionName"))
            : null;

    private static bool MatchesAnyIdentity(JsonObject node, string selector, params string[] propertyNames)
    {
        if (string.IsNullOrWhiteSpace(selector))
            return false;

        foreach (var propertyName in propertyNames)
        {
            var value = GetString(node, propertyName, string.Empty);
            if (string.Equals(value, selector, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool TrySplitProjectSelector(string selector, out string factionId, out string projectId)
    {
        factionId = string.Empty;
        projectId = string.Empty;
        var parts = selector.Split(["::", "|"], 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;

        factionId = parts[0];
        projectId = parts[1];
        return !string.IsNullOrWhiteSpace(factionId) && !string.IsNullOrWhiteSpace(projectId);
    }

    private static string ExtractProjectLabel(string optionLabel)
    {
        var markerIndex = optionLabel.IndexOf(" - ", StringComparison.Ordinal);
        return markerIndex >= 0 && markerIndex + 3 < optionLabel.Length
            ? optionLabel[(markerIndex + 3)..]
            : optionLabel;
    }

    private static string ResolvePoliticalRequestDisplayName(JsonObject request) =>
        FirstNonEmpty(
            GetString(request, "proposedFactionName", string.Empty),
            GetString(request, "factionName", string.Empty),
            GetString(request, "targetFactionName", string.Empty),
            GetString(request, "proposedHallName", string.Empty),
            GetString(request, "requestTitle", string.Empty),
            "решение фракций");

    private static string ResolvePoliticalReceiptDisplayName(JsonObject receipt) =>
        FirstNonEmpty(
            GetString(receipt, "factionName", string.Empty),
            GetString(receipt, "proposedFactionName", string.Empty),
            GetString(receipt, "targetFactionName", string.Empty),
            GetString(receipt, "displayName", string.Empty),
            "решение фракций");

    private static string FormatCreatedTurn(JsonObject node)
    {
        var turn = GetNumberOrString(node, "createdAtTurn", string.Empty);
        return string.IsNullOrWhiteSpace(turn) ? "ход не указан" : $"создано на ходу {turn}";
    }

    private static string DescribeCoreActionType(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            ShiningCoreActionRequestState.ActionTypeOpenGates => "Открытие Врат",
            ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage => "Подготовка новой жизни",
            ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction => "Открытие нативной фракции",
            ShiningCoreActionRequestState.ActionTypeInvestInFaction => "Инвестиция во фракцию",
            ShiningCoreActionRequestState.ActionTypeSupportProject => "Поддержка проекта",
            ShiningCoreActionRequestState.ActionTypeUnsupportProject => "Снятие поддержки проекта",
            ShiningCoreActionRequestState.ActionTypeRetireProject => "Проект уходит в историю",
            ShiningCoreActionRequestState.ActionTypeForgeRelicReshape => "Ковка реликвии",
            ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty => "Настройка реликвии",
            ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand => "Усиление реликвии",
            ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho => "Стабилизация эха",
            ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity => "Возвышение реликвии",
            _ => string.IsNullOrWhiteSpace(value) ? "Действие Обители" : HumanizeProtocolValue(value)
        };

    private static string DescribeFactionLifecycle(JsonObject faction)
    {
        var state = ShiningAbodeState.GetFactionLifecycleState(faction);
        return state switch
        {
            ShiningAbodeState.FactionLifecycleStateActive => "действует",
            ShiningAbodeState.FactionLifecycleStateBroken => "сломлена",
            ShiningAbodeState.FactionLifecycleStateDissolved => "распущена",
            _ => state
        };
    }

    private static string DescribeFactionLeader(JsonObject faction)
    {
        var leadership = faction["leadership"];
        var displayName = GetString(leadership, "headDisplayName", string.Empty);
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName;

        var type = GetString(leadership, "headActorType", string.Empty);
        var identity = GetString(leadership, "headActorId", string.Empty);
        return string.IsNullOrWhiteSpace(type) && string.IsNullOrWhiteSpace(identity)
            ? "не указан"
            : $"{type}:{identity}";
    }

    private static string DescribeResolutionStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "accepted" or "approved" or "complete" or "completed" => "принято",
            "rejected" or "declined" => "отклонено",
            "cancelled" or "canceled" => "отменено",
            _ => string.IsNullOrWhiteSpace(status) ? "не указан" : status
        };

    private static string ExtractLeadingTitle(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return string.Empty;

        var trimmed = summary.Trim();
        foreach (var separator in new[] { " и ", ". ", ";", ":" })
        {
            var index = trimmed.IndexOf(separator, StringComparison.Ordinal);
            if (index > 0)
                return trimmed[..index].Trim().TrimEnd('.');
        }

        return trimmed.TrimEnd('.');
    }

    private static string ToActionIdPart(string value)
    {
        var chars = value
            .Trim()
            .Select(static ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '-')
            .ToArray();
        var normalized = new string(chars).Trim('-');
        while (normalized.Contains("--", StringComparison.Ordinal))
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(normalized) ? "detail" : normalized;
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

    private static async Task<ActionPromptContext> ReadCoreActionPromptContext(
        FileSystemManager fs,
        StateManager stateManager)
    {
        var soul = await ReadJson(fs, SoulStatePath);
        var shining = await ReadJson(fs, ShiningAbodeState.StatePath);
        var residents = await ReadJson(fs, GuardianAbodeResidentState.StatePath);
        var guardians = await ReadJson(fs, GuardiansPath);

        var currentRealm = FirstNonEmpty(GetString(soul.Node, "currentRealm", string.Empty), stateManager.CurrentState.CurrentRealm);
        if (!RealmSemantics.IsShiningRealm(currentRealm))
            return ActionPromptContext.Blocked("Действия Сияющей Обители доступны только в Сияющей Обители. Сейчас душа находится в другом царстве.");

        if (shining.Node is not JsonObject shiningRoot)
            return ActionPromptContext.Blocked("Состояние Сияющей Обители сейчас недоступно. Повторите действие после восстановления состояния.");
        if (soul.Node is not JsonObject soulRoot)
            return ActionPromptContext.Blocked("Состояние души сейчас недоступно. Повторите действие после восстановления состояния.");

        var rawOwnerStateError = ShiningAbodeState.ValidateRawOwnerStateForActionableMode(shiningRoot);
        if (!string.IsNullOrWhiteSpace(rawOwnerStateError))
            return ActionPromptContext.Blocked("Сияющая Обитель сейчас не готова к действиям. Проверьте состояние перед новым запросом.");

        if (!string.Equals(GetString(shiningRoot, "availability", string.Empty), ShiningAbodeState.AvailabilityActive, StringComparison.OrdinalIgnoreCase))
            return ActionPromptContext.Blocked("Действия доступны только в активной Сияющей Обители.");

        var packageMode = ShiningAbodeState.GetPreparedIncarnationPackageMode(shiningRoot);
        if (packageMode != ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
            return ActionPromptContext.Blocked("Действия недоступны, пока Сияющая Обитель ждёт передачу в новую жизнь.");

        var pending = await ShiningCoreActionRequestState.ReadRequestsStateAsync(fs);
        if (pending.IsMalformed)
            return ActionPromptContext.Blocked("Ожидающее действие Сияющей Обители требует проверки состояния. Повторите после восстановления ожиданий.");
        if (pending.Requests.Count > 0)
            return ActionPromptContext.Blocked("Другое действие Сияющей Обители уже ожидает решения ГМ. Дождитесь результата перед новым запросом.");

        var residentRoot = residents.Node as JsonObject;
        var guardiansRoot = guardians.Node as JsonObject;
        ShiningAbodeState.NormalizeStateRoot(shiningRoot, residentRoot, guardiansRoot);
        return new ActionPromptContext(shiningRoot, soulRoot, residentRoot, guardiansRoot, null);
    }

    private static bool TryGetOpenFreshGatesForBrowser(JsonObject shiningRoot, out JsonObject gates, out string blocker)
    {
        gates = shiningRoot["gates"] as JsonObject ?? new JsonObject();
        if (!GetBool(gates["hasOpenDraft"]))
        {
            blocker = "Сначала откройте Врата инкарнации.";
            return false;
        }

        if (GetBool(gates["isStale"]))
        {
            blocker = "Текущий набор Врат устарел. Откройте Врата заново.";
            return false;
        }

        blocker = string.Empty;
        return true;
    }

    private static string DescribeGatesDraftState(JsonObject? gates)
    {
        if (gates == null || !GetBool(gates["hasOpenDraft"]))
            return "закрыт";

        var freshness = GetBool(gates["isStale"]) ? "устарел" : "свежий";
        var selected = ReadGatesStringArray(gates, "selectedBlessingCardIds").Count;
        return $"открыт, {freshness}; выбрано {selected}";
    }

    private static List<string> ReadGatesStringArray(JsonObject? gates, string propertyName) =>
        (gates?[propertyName] as JsonArray)?.OfType<JsonValue>()
        .Where(static node => node.TryGetValue<string>(out _))
        .Select(static node => node.GetValue<string>().Trim())
        .Where(static value => !string.IsNullOrWhiteSpace(value))
        .ToList() ?? [];

    private static string FormatGatesCardNames(JsonObject? gates)
    {
        var names = (gates?["availableBlessingCards"] as JsonArray)?.OfType<JsonObject>()
            .Select(static card => GetString(card, "displayName", GetString(card, "cardId", "благословение")))
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToArray() ?? [];

        return names.Length == 0 ? "нет доступных благословений" : string.Join(", ", names);
    }

    private static JsonArray BuildSelectedGatesCardSnapshot(JsonObject gates, IReadOnlyList<string> selectedIds)
    {
        var snapshot = new JsonArray();
        foreach (var selectedId in selectedIds)
        {
            var card = FindGatesCard(gates, selectedId);
            if (card != null)
                snapshot.Add(card.DeepClone());
        }

        return snapshot;
    }

    private static JsonObject? FindGatesCard(JsonObject? gates, string cardId)
    {
        if (gates == null || string.IsNullOrWhiteSpace(cardId))
            return null;

        foreach (var propertyName in new[] { "availableBlessingCards", "allCandidateBlessingCards" })
        {
            if (gates[propertyName] is not JsonArray cards)
                continue;

            var card = cards.OfType<JsonObject>().FirstOrDefault(item =>
                string.Equals(GetString(item, "cardId", string.Empty), cardId, StringComparison.OrdinalIgnoreCase));
            if (card != null)
                return card;
        }

        return null;
    }

    private static string ResolveGatesCardLabel(JsonObject gates, string cardId)
    {
        var card = FindGatesCard(gates, cardId);
        return card == null
            ? "благословение больше не видно"
            : GetString(card, "displayName", cardId);
    }

    private static string BuildBlessingCardLabel(JsonObject card, ISet<string> selectedIds)
    {
        var name = GetString(card, "displayName", GetString(card, "cardId", "Благословение"));
        var marker = selectedIds.Contains(GetString(card, "cardId", string.Empty)) ? "выбрано" : "доступно";
        return $"{name} - {DescribeEffectFamily(GetString(card, "effectFamily", string.Empty))}, {DescribeRarity(GetString(card, "rarity", string.Empty))}, {marker}";
    }

    private static string BuildBlessingCardPromptLabel(JsonObject card, ISet<string> selectedIds)
    {
        var name = GetString(card, "displayName", GetString(card, "cardId", "Благословение"));
        return selectedIds.Contains(GetString(card, "cardId", string.Empty))
            ? $"{name} (выбрано)"
            : name;
    }

    private static string BuildBlessingCardDescription(JsonObject card)
    {
        var summary = GetString(card, "displaySummary", string.Empty);
        var parts = new[]
            {
                DescribeEffectFamily(GetString(card, "effectFamily", string.Empty)),
                DescribeRarity(GetString(card, "rarity", string.Empty)),
                summary
            }
            .Where(static part => !string.IsNullOrWhiteSpace(part));
        return string.Join("; ", parts);
    }

    private static string DescribeEffectFamily(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            ShiningAbodeState.EffectFamilyLore => "знание",
            ShiningAbodeState.EffectFamilySocial => "связи",
            ShiningAbodeState.EffectFamilyResource => "ресурсы",
            ShiningAbodeState.EffectFamilyMemory => "память",
            ShiningAbodeState.EffectFamilyDescent => "нисхождение",
            ShiningAbodeState.EffectFamilySurvival => "стойкость",
            ShiningAbodeState.EffectFamilyRelic => "реликвии",
            ShiningAbodeState.EffectFamilyRoute => "путь",
            _ => "сияние"
        };

    private static string DescribeRarity(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            ShiningAbodeState.RarityCommon => "простое",
            ShiningAbodeState.RarityUncommon => "необычное",
            ShiningAbodeState.RarityRare => "редкое",
            ShiningAbodeState.RarityEpic => "эпическое",
            ShiningAbodeState.RarityLegendary => "легендарное",
            ShiningAbodeState.RarityRadiant => "сияющее",
            _ => "особое"
        };

    private static string SanitizeShiningGatesValidationMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Врата сейчас не прошли проверку состояния. Повторите действие после восстановления Обители.";

        if (message.Contains("currentRealm", StringComparison.OrdinalIgnoreCase))
            return "Действия Врат доступны только в Сияющей Обители.";
        if (message.Contains("preparedIncarnationPackage", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("frozen", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("handoff", StringComparison.OrdinalIgnoreCase))
        {
            return "Сияющая Обитель уже ждёт передачу в новую жизнь.";
        }

        var sanitized = message
            .Replace("draft", "набор Врат", StringComparison.OrdinalIgnoreCase)
            .Replace("reroll", "обновление", StringComparison.OrdinalIgnoreCase)
            .Replace("replacement", "новое благословение", StringComparison.OrdinalIgnoreCase);

        return sanitized.Contains("open_gates", StringComparison.OrdinalIgnoreCase) ||
               sanitized.Contains("prepare_incarnation_package", StringComparison.OrdinalIgnoreCase) ||
               sanitized.Contains("selectedCardIds", StringComparison.OrdinalIgnoreCase) ||
               sanitized.Contains("sourceDraftVersion", StringComparison.OrdinalIgnoreCase) ||
               sanitized.Contains("selectedCards", StringComparison.OrdinalIgnoreCase) ||
               sanitized.Contains("pending_", StringComparison.OrdinalIgnoreCase) ||
               sanitized.Contains(".json", StringComparison.OrdinalIgnoreCase)
            ? "Врата сейчас не прошли проверку состояния. Повторите действие после восстановления Обители."
            : sanitized;
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

    private static IEnumerable<UiSelectionOption> BuildForgeActionOptions(
        JsonObject shiningRoot,
        JsonObject soulRoot,
        JsonObject? residentRoot,
        IReadOnlyList<JsonObject> factions,
        IReadOnlyList<(string RelicId, string RelicName, string Collection, JsonObject Relic)> relics)
    {
        foreach (var action in new[]
                 {
                     (
                         Type: ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
                         Label: "Перековать форму реликвии",
                         Summary: "сменить форму без прямого изменения сущности реликвии"
                     ),
                     (
                         Type: ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty,
                         Label: "Перенастроить свойство реликвии",
                         Summary: "заменить выбранное свойство равноценным вариантом"
                     ),
                     (
                         Type: ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand,
                         Label: "Усилить ступень свойства",
                         Summary: "поднять выбранное свойство на следующий допустимый шаг"
                     ),
                     (
                         Type: ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho,
                         Label: "Стабилизировать эхо реликвии",
                         Summary: "закрепить проявление спутника в подходящей реликвии"
                     ),
                     (
                         Type: ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity,
                         Label: "Возвысить редкость реликвии",
                         Summary: "поднять реликвию на следующую ступень редкости"
                     )
                 })
        {
            var requiredTier = ShiningAbodeState.GetForgeRequiredRadianceTier(action.Type);
            var hasCurrentQuote = TryFindBestForgeActionQuote(
                shiningRoot,
                soulRoot,
                residentRoot,
                factions,
                relics,
                action.Type,
                useAbundantResources: false,
                out var cost);
            var hasPreviewQuote = hasCurrentQuote ||
                                  TryFindBestForgeActionQuote(
                                      shiningRoot,
                                      soulRoot,
                                      residentRoot,
                                      factions,
                                      relics,
                                      action.Type,
                                      useAbundantResources: true,
                                      out cost);
            var costText = hasPreviewQuote
                ? $"минимальная цена {cost.Feathers} Перьев и {cost.LightSparks} Искр"
                : "нет подходящей видимой реликвии или фракции";

            yield return Option(
                action.Type,
                action.Label,
                $"{action.Summary}; нужно сияние {requiredTier}, {costText}.",
                disabled: !hasCurrentQuote);
        }
    }

    private static bool TryFindBestForgeActionQuote(
        JsonObject shiningRoot,
        JsonObject soulRoot,
        JsonObject? residentRoot,
        IEnumerable<JsonObject> factions,
        IEnumerable<(string RelicId, string RelicName, string Collection, JsonObject Relic)> relics,
        string actionType,
        bool useAbundantResources,
        out ShiningAbodeState.ResourceCost bestCost)
    {
        bestCost = default;
        var found = false;
        var quoteShiningRoot = shiningRoot;
        var quoteSoulRoot = soulRoot;
        if (useAbundantResources)
        {
            quoteShiningRoot = shiningRoot.DeepClone().AsObject();
            quoteSoulRoot = soulRoot.DeepClone().AsObject();
            quoteShiningRoot["lightSparks"] = 1_000_000;
            if (quoteSoulRoot["inkFeathers"] is JsonObject inkFeathers)
                inkFeathers["current"] = 1_000_000;
            else
                quoteSoulRoot["inkFeathers"] = 1_000_000;
        }

        foreach (var faction in factions)
        {
            var factionId = GetString(faction, "factionId", string.Empty);
            if (string.IsNullOrWhiteSpace(factionId))
                continue;

            foreach (var relic in relics)
            {
                foreach (var sample in BuildForgeQuoteSamples(actionType, soulRoot, relic))
                {
                    if (!ShiningAbodeState.TryQuoteForgeAction(
                            quoteShiningRoot,
                            quoteSoulRoot,
                            residentRoot,
                            actionType,
                            factionId,
                            relic.RelicId,
                            sample.TargetFormTag,
                            sample.PropertyIndex,
                            sample.ReplacementProperty,
                            sample.AddedProperties,
                            out var cost,
                            out _))
                    {
                        continue;
                    }

                    if (!found || IsLowerForgeCost(cost, bestCost))
                        bestCost = cost;
                    found = true;
                }
            }
        }

        return found;
    }

    private static IEnumerable<ForgeQuoteSample> BuildForgeQuoteSamples(
        string actionType,
        JsonObject soulRoot,
        (string RelicId, string RelicName, string Collection, JsonObject Relic) relic)
    {
        switch (actionType)
        {
            case ShiningCoreActionRequestState.ActionTypeForgeRelicReshape:
                var currentFormTag = GetString(relic.Relic, "formTag", string.Empty);
                if (!string.IsNullOrWhiteSpace(currentFormTag))
                    yield return new ForgeQuoteSample(ResolveAlternateForgeFormTag(currentFormTag), -1, null, null);
                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty:
                if (relic.Relic["properties"] is not JsonArray retuneProperties)
                    break;

                var replacementProperties = EnumerateDistinctForgeProperties(soulRoot).ToArray();
                for (var index = 0; index < retuneProperties.Count; index++)
                {
                    foreach (var replacementProperty in replacementProperties)
                    {
                        yield return new ForgeQuoteSample(
                            string.Empty,
                            index,
                            replacementProperty.DeepClone().AsObject(),
                            null);
                    }
                }

                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand:
                if (relic.Relic["properties"] is not JsonArray strengthenProperties)
                    break;

                for (var index = 0; index < strengthenProperties.Count; index++)
                    yield return new ForgeQuoteSample(string.Empty, index, null, null);
                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho:
                yield return new ForgeQuoteSample(string.Empty, -1, null, null);
                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity:
                yield return new ForgeQuoteSample(string.Empty, -1, null, BuildForgeAddedPropertiesQuoteSample(soulRoot));
                break;
        }
    }

    private static JsonArray BuildForgeAddedPropertiesQuoteSample(JsonObject soulRoot)
    {
        var properties = new JsonArray();
        foreach (var property in EnumerateDistinctForgeProperties(soulRoot))
            properties.Add(property.DeepClone());
        return properties;
    }

    private static string ResolveAlternateForgeFormTag(string currentFormTag)
    {
        foreach (var candidate in new[] { "lance", "ring", "blade", "mirror", "lantern", "chalice", "companion_echo" })
        {
            if (!string.Equals(candidate, currentFormTag, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return "relic";
    }

    private static bool IsLowerForgeCost(ShiningAbodeState.ResourceCost candidate, ShiningAbodeState.ResourceCost current) =>
        candidate.Feathers + candidate.LightSparks < current.Feathers + current.LightSparks ||
        candidate.Feathers + candidate.LightSparks == current.Feathers + current.LightSparks &&
        (candidate.Feathers < current.Feathers ||
         candidate.Feathers == current.Feathers && candidate.LightSparks < current.LightSparks);

    private static IEnumerable<UiSelectionOption> BuildForgeFormTagOptions(
        IEnumerable<(string RelicId, string RelicName, string Collection, JsonObject Relic)> relics)
    {
        var known = relics
            .Select(static relic => GetString(relic.Relic, "formTag", string.Empty))
            .Concat(["lance", "ring", "blade", "mirror", "lantern", "chalice", "companion_echo"])
            .Where(static formTag => !string.IsNullOrWhiteSpace(formTag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static formTag => DescribeForgeFormTag(formTag), StringComparer.OrdinalIgnoreCase);

        foreach (var formTag in known)
            yield return Option(formTag, DescribeForgeFormTag(formTag), "Можно выбрать предложенную форму или вписать свою.");
    }

    private static IEnumerable<UiSelectionOption> BuildForgePropertyChoiceOptions(
        IEnumerable<(string RelicId, string RelicName, string Collection, JsonObject Relic)> relics)
    {
        foreach (var relic in relics)
        {
            if (relic.Relic["properties"] is not JsonArray properties)
                continue;

            for (var index = 0; index < properties.Count; index++)
            {
                if (properties[index] is not JsonObject property)
                    continue;

                yield return Option(
                    $"{relic.RelicId}|{index}",
                    $"{relic.RelicName} - {RenderForgePropertyLabel(property, index)}",
                    "Выберите это свойство для перенастройки или усиления.");
            }
        }
    }

    private static IEnumerable<UiSelectionOption> BuildForgeReplacementPropertyOptions(JsonObject soulRoot) =>
        EnumerateDistinctForgeProperties(soulRoot)
            .Select(static property => Option(
                property.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
                RenderForgePropertyLabel(property),
                "Предложенный вариант для перенастройки свойства."));

    private static IEnumerable<UiSelectionOption> BuildForgeAddedPropertyOptions(JsonObject soulRoot) =>
        EnumerateDistinctForgeProperties(soulRoot)
            .Select(static property =>
            {
                var value = new JsonArray(property.DeepClone());
                return Option(
                    value.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
                    RenderForgePropertyLabel(property),
                    "Предложенное дополнительное свойство для новой редкости.");
            });

    private static IEnumerable<JsonObject> EnumerateDistinctForgeProperties(JsonObject soulRoot) =>
        EnumerateSoulRelics(soulRoot)
            .SelectMany(static relic => (relic.Relic["properties"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            .GroupBy(BuildForgePropertySuggestionKey, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First().DeepClone().AsObject())
            .OrderBy(static property => BuildForgePropertySuggestionKey(property), StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<UiSelectionOption> BuildForgeRerollOptions(int rerolls)
    {
        yield return Option("0", "Без переброса", "Не тратить благословение на этот запрос.");
        for (var index = 1; index <= Math.Max(0, rerolls); index++)
        {
            yield return Option(
                index.ToString(),
                index == 1 ? "Потратить 1 переброс" : $"Потратить {index} переброса",
                "Переброс будет списан только вместе с созданием запроса ковки.");
        }
    }

    private static string BuildForgeFactionLabel(JsonObject faction) =>
        $"{ResolveFactionDisplayName(faction)} - сила {GetInt(faction["factionStrength"], 0)}, поддержана огранка реликвий";

    private static string BuildSoulRelicForgeLabel((string RelicId, string RelicName, string Collection, JsonObject Relic) relic)
    {
        var formTag = GetString(relic.Relic, "formTag", string.Empty);
        var rarity = ResolveForgeRarityKey(relic.Relic);
        return $"{relic.RelicName} - {DescribeForgeRarity(rarity)}, {DescribeSoulRelicCollection(relic.Collection)}, форма {DescribeForgeFormTag(formTag)}, свойств {GetForgePropertyCount(relic.Relic)}";
    }

    private static string RenderForgePropertyLabel(JsonObject property, int? propertyIndex = null)
    {
        var propertyName = FirstNonEmpty(
            GetString(property, "name", string.Empty),
            DescribeShiningForgeStat(GetString(property, "stat", string.Empty)),
            HumanizeProtocolValue(GetString(property, "propertyId", string.Empty)),
            "свойство");
        var prefix = propertyIndex.HasValue ? $"Свойство {propertyIndex.Value + 1}: " : string.Empty;
        return $"{prefix}{propertyName} (ступень: {DescribeForgeBand(property["band"])})";
    }

    private static string BuildForgePropertySuggestionKey(JsonObject property)
    {
        var propertyId = GetString(property, "propertyId", string.Empty);
        var name = GetString(property, "name", string.Empty);
        var stat = GetString(property, "stat", string.Empty);
        var band = GetStringValue(property["band"]);
        return $"{propertyId}|{name}|{stat}|{band}";
    }

    private static string DescribeShiningForgeStat(string? stat) =>
        (stat ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            ShiningAbodeState.EffectFamilyLore => "Знание",
            ShiningAbodeState.EffectFamilySocial => "Связи",
            ShiningAbodeState.EffectFamilyResource => "Ресурсы",
            ShiningAbodeState.EffectFamilyMemory => "Память",
            ShiningAbodeState.EffectFamilyDescent => "Нисхождение",
            ShiningAbodeState.EffectFamilySurvival => "Стойкость",
            ShiningAbodeState.EffectFamilyRelic => "Реликвия",
            ShiningAbodeState.EffectFamilyRoute => "Путь",
            _ => string.Empty
        };

    private static string DescribeSoulRelicCollection(string collection) =>
        (collection ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "equipped" => "экипировано",
            "stored" => "хранилище",
            _ => string.IsNullOrWhiteSpace(collection) ? "неизвестно" : collection
        };

    private static string DescribeForgeFormTag(string? formTag)
    {
        if (string.IsNullOrWhiteSpace(formTag))
            return "неопределённая форма";

        var normalized = formTag.Trim();
        return normalized.ToLowerInvariant() switch
        {
            "glass_path" => "стекло пути",
            "solar_crown" => "солнечный венец",
            "lance" => "копьё",
            _ => HumanizeForgeFormTag(normalized)
        };
    }

    private static string HumanizeForgeFormTag(string formTag)
    {
        var words = formTag
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static word => word.ToLowerInvariant() switch
            {
                "blade" => "клинок",
                "spear" => "копьё",
                "lance" => "копьё",
                "ring" => "кольцо",
                "seed" => "зерно",
                "companion" => "спутник",
                "echo" => "эхо",
                "lantern" => "фонарь",
                "mirror" => "зеркало",
                "chalice" => "чаша",
                "sigil" => "печать",
                "memory" => "память",
                "dawn" => "рассвет",
                "radiant" => "сияющий",
                "route" => "путь",
                "glass" => "стекло",
                "path" => "путь",
                "solar" => "солнечный",
                "crown" => "венец",
                _ => word
            })
            .ToArray();

        return words.Length == 0 ? "неопределённая форма" : string.Join(' ', words);
    }

    private static string ResolveForgeRarityKey(JsonObject relic)
    {
        var rarity = GetString(relic, "quality", string.Empty);
        if (string.IsNullOrWhiteSpace(rarity))
            rarity = GetString(relic, "rarity", string.Empty);

        return rarity.Trim().ToLowerInvariant();
    }

    private static string DescribeForgeRarity(string rarityKey) =>
        (rarityKey ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            ShiningAbodeState.RarityCommon => "обычная",
            ShiningAbodeState.RarityUncommon => "необычная",
            ShiningAbodeState.RarityRare => "редкая",
            ShiningAbodeState.RarityEpic => "эпическая",
            ShiningAbodeState.RarityLegendary => "легендарная",
            ShiningAbodeState.RarityRadiant => "сияющая",
            _ => string.IsNullOrWhiteSpace(rarityKey) ? "неизвестная" : rarityKey
        };

    private static string DescribeForgeBand(JsonNode? bandNode)
    {
        if (bandNode is JsonValue value)
        {
            if (value.TryGetValue<int>(out var numericBand))
                return $"ступень {numericBand}";

            if (value.TryGetValue<string>(out var stringBand))
            {
                var normalized = stringBand.Trim().ToLowerInvariant();
                return int.TryParse(normalized, out var parsedBand)
                    ? $"ступень {parsedBand}"
                    : DescribeForgeRarity(normalized);
            }
        }

        return "неизвестна";
    }

    private static int GetForgePropertyCount(JsonObject relic) => (relic["properties"] as JsonArray)?.Count ?? 0;

    private static IEnumerable<(string RelicId, string RelicName, string Collection, JsonObject Relic)> EnumerateSoulRelics(JsonObject soulRoot)
    {
        if (soulRoot["soulRelics"] is JsonObject soulRelicsObject)
        {
            foreach (var collectionName in new[] { "equipped", "stored" })
            {
                if (soulRelicsObject[collectionName] is not JsonArray collection)
                    continue;

                foreach (var relic in collection.OfType<JsonObject>())
                {
                    var relicId = FirstNonEmpty(GetString(relic, "relicId", string.Empty), GetString(relic, "id", string.Empty));
                    if (string.IsNullOrWhiteSpace(relicId))
                        continue;

                    var relicName = FirstNonEmpty(GetString(relic, "name", string.Empty), GetString(relic, "displayName", string.Empty), relicId);
                    yield return (relicId, relicName, collectionName, relic);
                }
            }
        }
        else if (soulRoot["soulRelics"] is JsonArray flatCollection)
        {
            foreach (var relic in flatCollection.OfType<JsonObject>())
            {
                var relicId = FirstNonEmpty(GetString(relic, "relicId", string.Empty), GetString(relic, "id", string.Empty));
                if (string.IsNullOrWhiteSpace(relicId))
                    continue;

                var relicName = FirstNonEmpty(GetString(relic, "name", string.Empty), GetString(relic, "displayName", string.Empty), relicId);
                yield return (relicId, relicName, "stored", relic);
            }
        }
    }

    private static string HumanizeProtocolValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return string.Join(
            ' ',
            value
                .Trim()
                .Replace('-', '_')
                .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(static part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]));
    }

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
        if (string.IsNullOrWhiteSpace(residentId))
            return false;

        return SarefMainStoryState.GetPlayerVisibleShiningFactions(shiningRoot).Any(faction =>
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

    private static string BuildFactionInvestmentLabel(JsonObject faction)
    {
        var current = Math.Clamp(GetInt(faction["investCountThisAscension"], 0), 0, 3);
        return $"{ResolveFactionDisplayName(faction)} (инвестиций {current}/3; сила {GetNumberOrString(faction, "factionStrength", "0")})";
    }

    private static IEnumerable<ShiningProjectPromptOption> EnumerateVisibleProjectOptions(
        JsonObject shiningRoot,
        Func<JsonObject, bool> predicate)
    {
        foreach (var faction in GetVisibleFactions(shiningRoot).OrderBy(static faction => ResolveFactionDisplayName(faction), StringComparer.OrdinalIgnoreCase))
        {
            var factionId = GetString(faction, "factionId", string.Empty);
            if (string.IsNullOrWhiteSpace(factionId) || faction["projects"] is not JsonArray projects)
                continue;

            foreach (var project in projects
                         .OfType<JsonObject>()
                         .Where(IsPlayerVisibleMemoryObject)
                         .Where(predicate)
                         .OrderBy(static project => ResolveProjectDisplayName(project), StringComparer.OrdinalIgnoreCase))
            {
                var projectId = GetString(project, "projectId", string.Empty);
                if (string.IsNullOrWhiteSpace(projectId))
                    continue;

                var projectName = ResolveProjectDisplayName(project);
                var factionName = ResolveFactionDisplayName(faction);
                yield return new ShiningProjectPromptOption(
                    $"{factionId}|{projectId}",
                    $"{factionName} - {projectName}",
                    $"{DescribeProjectSupport(project)}; {DescribeProjectStatus(project)}.");
            }
        }
    }

    private static bool IsCompletedProject(JsonObject project) =>
        string.Equals(GetString(project, "status", string.Empty), ShiningAbodeState.ProjectStatusCompleted, StringComparison.OrdinalIgnoreCase);

    private static string ResolveProjectDisplayName(JsonObject project) =>
        FirstNonEmpty(
            GetString(project, "displayName", string.Empty),
            GetString(project, "projectName", string.Empty),
            GetString(project, "projectId", "проект"));

    private static string DescribeProjectStatus(JsonObject project) =>
        GetString(project, "status", string.Empty) switch
        {
            var status when string.Equals(status, ShiningAbodeState.ProjectStatusCompleted, StringComparison.OrdinalIgnoreCase) => "завершён",
            var status when string.Equals(status, ShiningAbodeState.ProjectStatusRetired, StringComparison.OrdinalIgnoreCase) => "в истории",
            "" => "статус не указан",
            _ => "ещё не завершён"
        };

    private static string DescribeProjectSupport(JsonObject project) =>
        GetBool(project["isSupported"]) ? "поддерживается" : "без поддержки";

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
            .Select(DescribePoliticalConsequence)
            .ToArray();

        return visible.Length == 0 ? "нет" : string.Join("\n", visible);
    }

    private static string DescribePoliticalConsequence(string value) =>
        value.Trim() switch
        {
            "hidden_house_suspected" => "Появилось подозрение о Скрытом Доме.",
            "dawn_public_accord" => "Закреплено публичное рассветное соглашение.",
            var text => HumanizeProtocolValue(text)
        };

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
        "rumor_credit" or "rumorCredit" => "Кредит слухов",
        _ => value
    };

    private static string TranslateEventType(string value) => value switch
    {
        "public_aid" => "публичная помощь",
        "founding" => "основание",
        "realignment" => "переход",
        "leadership" or "leadership_transition" => "смена власти",
        "resource_shift" => "ресурсный сдвиг",
        "rumor" => "слух",
        _ => value
    };

    private static ExplorerCommandResult Completed(
        string command,
        IEnumerable<UiBlock> blocks,
        IEnumerable<UiAction>? actions = null) =>
        Result(command, CommandExecutionState.Completed, blocks, actions: actions);

    private static ExplorerCommandResult Blocked(string command, string title, string message) =>
        Result(command, CommandExecutionState.Blocked, [Message(UiNotificationSeverity.Warning, title, message)]);

    private static ExplorerCommandResult Result(
        string command,
        CommandExecutionState state,
        IEnumerable<UiBlock> blocks,
        IEnumerable<UiPrompt>? prompts = null,
        IEnumerable<UiAction>? actions = null) =>
        new()
        {
            Command = command,
            State = state,
            Blocks = blocks.ToList(),
            Actions = actions?.ToList() ?? [],
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

    private static UiSelectionOption Option(string value, string label, string description, bool disabled = false) =>
        new() { Value = value, Label = label, Description = description, Disabled = disabled };

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
            return "ожидание Источника повреждено; нужна проверка ГМ";
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
            ? "требования выполнены; сцену можно открыть через форму"
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

    private static bool GetBool(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var flag))
                return flag;
            if (value.TryGetValue<string>(out var text) && bool.TryParse(text, out flag))
                return flag;
        }

        return false;
    }

    private static string GetString(JsonNode? node, string propertyName, string fallback)
    {
        if (node?[propertyName] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
            return text.Trim();
        return fallback;
    }

    private static string GetStringValue(JsonNode? node, string fallback = "")
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                return text.Trim();
            if (value.TryGetValue<int>(out var intValue))
                return intValue.ToString();
            if (value.TryGetValue<long>(out var longValue))
                return longValue.ToString();
        }

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

    private sealed record ActionPromptContext(
        JsonObject ShiningRoot,
        JsonObject SoulRoot,
        JsonObject? ResidentsRoot,
        JsonObject? GuardiansRoot,
        string? Blocker)
    {
        public static ActionPromptContext Blocked(string blocker) =>
            new(new JsonObject(), new JsonObject(), null, null, blocker);
    }

    private sealed record ShiningProjectPromptOption(
        string Value,
        string Label,
        string Description);

    private sealed record ForgeQuoteSample(
        string TargetFormTag,
        int PropertyIndex,
        JsonObject? ReplacementProperty,
        JsonArray? AddedProperties);
}
