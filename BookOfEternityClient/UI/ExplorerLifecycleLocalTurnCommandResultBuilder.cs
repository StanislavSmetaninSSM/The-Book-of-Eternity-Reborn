using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.UI;

public static class ExplorerLifecycleLocalTurnCommandResultBuilder
{
    private const string TurnRequestPath = "input/turn_request.json";
    private const string TurnCompletePath = "ready/turn_complete.json";
    private const string TurnErrorPath = "ready/turn_error.json";
    private const string PendingTurnSnapshotManifestPath = "game_state/control/pending_turn_snapshot.json";
    private const string PendingTurnSnapshotDirectory = "game_state/control/pending_turn_snapshot";
    private const string ExplorerRollbackDirectory = "game_state/control/explorer_local_turn_rollback";
    private const string SoulStatePath = "game_state/meta/soul_state.json";
    private static readonly Regex PlayerFacingStatePathRegex = new(@"(?:game_state|lore|output|input|ready|stories)/[^\s,;:)]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PlayerFacingJsonFileRegex = new(@"[A-Za-z0-9_\-]+\.json(?:\.[A-Za-z0-9_\-\[\]]+)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool CanBuild(string command) => NormalizeCommand(command) switch
    {
        "/validate" or "/валидация" or
        "/world_setup" or "/настройка_мира" or
        "/distribute" or "/распределить" or
        "/companion_directive" or "/директива_компаньону" or
        "/faction_directive" or "/директива_фракции" or
        "/npc_talk" or "/talk_npc" or "/поговорить_с_нпс" or "/разговор_с_нпс" or
        "/npc_trade" or "/торговля_нпс" or
        "/equip" or "/экипировать" or
        "/unequip" or "/снять" or
        "/inventory_drop" or "/выбросить_предмет" or
        "/inventory_split" or "/разделить_стопку" or
        "/inventory_merge" or "/объединить_стопки" or
        "/storage_move" or "/хранилище_предметы" or
        "/vehicle_move" or "/транспорт_предметы" or
        "/craft" or "/ремесло" or
        "/reveal_fate" or "/открыть_судьбу" or
        "/rewrite_fate" or "/переписать_судьбу" or
        "/gacha" or "/гача" or
        "/archive_consultation" or "/архивная_консультация" or
        "/archive_project_fuel" or "/архивная_подпитка_проекта" or
        "/abode_offering" or "/подношение_обители" or
        "/found_guardian_mantle" or "/учредить_хранителя" or
        "/guardian_trade" or "/торговля_хранителя" or
        "/guardian_social" or "/talk_guardian" or "/поговорить_с_хранителем" or "/общение_хранителя" or
        "/abode_residents" or "/обитатели_обители" or
        "/resident_interaction" or "/общение_резидента" or "/поговорить_с_резидентом" or "/история_резидента" or
        "/resident_transfer" or "/переход_резидента" or
        "/shining_trade" or "/сияющая_торговля" or
        "/spiritual_action" or "/духовное_действие" or
        "/soul_relic_equip" or "/экипировать_реликвию" or
        "/soul_relic_unequip" or "/снять_реликвию" => true,
        _ => false
    };

    public static async Task<ExplorerCommandResult?> TryBuildAsync(
        string command,
        StateManager stateManager,
        FileSystemManager fs,
        ValidationService validationService)
    {
        var normalized = NormalizeCommand(command);
        return normalized switch
        {
            "/validate" or "/валидация" => await BuildValidationAsync(command, fs, validationService),
            "/world_setup" or "/настройка_мира" => await BuildWorldSetupAsync(command, fs, stateManager),
            "/distribute" or "/распределить" => await BuildStatDistributionAsync(command, fs),
            "/companion_directive" or "/директива_компаньону" => await BuildCompanionDirectiveAsync(command, fs),
            "/faction_directive" or "/директива_фракции" => await BuildFactionDirectiveAsync(command, fs),
            "/npc_talk" or "/talk_npc" or "/поговорить_с_нпс" or "/разговор_с_нпс" => await BuildNpcTalkAsync(command, fs),
            "/npc_trade" or "/торговля_нпс" => await BuildNpcTradeAsync(command, fs, stateManager),
            "/equip" or "/экипировать" => await BuildInventoryEquipAsync(command, fs),
            "/unequip" or "/снять" => await BuildInventoryUnequipAsync(command, fs),
            "/inventory_drop" or "/выбросить_предмет" => await BuildInventoryDropAsync(command, fs),
            "/inventory_split" or "/разделить_стопку" => await BuildInventorySplitAsync(command, fs),
            "/inventory_merge" or "/объединить_стопки" => await BuildInventoryMergeAsync(command, fs),
            "/storage_move" or "/хранилище_предметы" => await BuildStorageItemMoveAsync(command, fs),
            "/vehicle_move" or "/транспорт_предметы" => await BuildVehicleItemMoveAsync(command, fs),
            "/craft" or "/ремесло" => await BuildCraftAsync(command, fs),
            "/reveal_fate" or "/открыть_судьбу" => await BuildInkFeatherRevealFateAsync(command, fs, stateManager),
            "/rewrite_fate" or "/переписать_судьбу" => await BuildInkFeatherRewriteFateAsync(command, fs, stateManager),
            "/gacha" or "/гача" => await BuildGachaAsync(command, fs, stateManager),
            "/archive_consultation" or "/архивная_консультация" => await BuildArchiveConsultationAsync(command, fs, stateManager),
            "/archive_project_fuel" or "/архивная_подпитка_проекта" => await BuildArchiveProjectFuelAsync(command, fs, stateManager),
            "/abode_offering" or "/подношение_обители" => await BuildAbodeOfferingAsync(command, fs, stateManager),
            "/found_guardian_mantle" or "/учредить_хранителя" => await BuildPlayerGuardianFoundationAsync(command, fs),
            "/guardian_trade" or "/торговля_хранителя" => await BuildGuardianTradeAsync(command, fs, stateManager),
            "/guardian_social" or "/talk_guardian" or "/поговорить_с_хранителем" or "/общение_хранителя" => await BuildGuardianSocialAsync(command, fs, stateManager),
            "/abode_residents" or "/обитатели_обители" => await BuildAbodeResidentsAsync(command, fs, stateManager),
            "/resident_interaction" or "/общение_резидента" or "/поговорить_с_резидентом" or "/история_резидента" => await BuildResidentInteractionAsync(command, fs, stateManager),
            "/resident_transfer" or "/переход_резидента" => await BuildResidentTransferAsync(command, fs, stateManager),
            "/shining_trade" or "/сияющая_торговля" => await BuildShiningTradeAsync(command, fs, stateManager),
            "/spiritual_action" or "/духовное_действие" => await BuildSpiritualActionAsync(command, fs, stateManager),
            "/soul_relic_equip" or "/экипировать_реликвию" => await BuildSoulRelicEquipAsync(command, fs),
            "/soul_relic_unequip" or "/снять_реликвию" => await BuildSoulRelicUnequipAsync(command, fs),
            _ => null
        };
    }

    private static async Task<ExplorerCommandResult> BuildValidationAsync(
        string command,
        FileSystemManager fs,
        ValidationService validationService)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var issues = await validationService.ValidateGameStateAsync();
        var blocks = new List<UiBlock> { localTurn.Panel };

        if (issues.Count == 0)
        {
            blocks.Add(Message(
                UiNotificationSeverity.Success,
                "Валидация",
                "Все проверки пройдены. Файлы состояния не содержат найденных нарушений."));
        }
        else
        {
            blocks.Add(Message(
                issues.Any(static issue => issue.Severity == IssueSeverity.Error)
                    ? UiNotificationSeverity.Error
                    : UiNotificationSeverity.Warning,
                $"Валидация: найдено проблем {issues.Count}",
                "Проверка состояния нашла нарушения, которые могут мешать игре. Ниже показаны основные разделы и первые проблемы для исправления."));

            blocks.Add(BuildValidationDossier(issues));
        }

        return Result(command, CommandExecutionState.Completed, blocks);
    }

    private static UiEntityDossierBlock BuildValidationDossier(IReadOnlyList<ValidationIssue> issues)
    {
        var errorCount = issues.Count(static issue => issue.Severity == IssueSeverity.Error);
        var warningCount = issues.Count(static issue => issue.Severity == IssueSeverity.Warning);
        var infoCount = issues.Count(static issue => issue.Severity == IssueSeverity.Info);
        var worstSeverity = errorCount > 0
            ? IssueSeverity.Error
            : warningCount > 0
                ? IssueSeverity.Warning
                : IssueSeverity.Info;

        return new UiEntityDossierBlock
        {
            EntityType = "validation-report",
            Title = "Валидация состояния",
            Subtitle = "Проверка целостности игровых данных",
            Summary = $"Найдено проблем: {issues.Count}. Сначала исправляйте ошибки, затем предупреждения.",
            Badges =
            [
                new UiEntityBadge { Label = DescribeValidationSeverity(worstSeverity), Tone = ToneForValidationSeverity(worstSeverity), Icon = "alert-triangle" }
            ],
            Facts =
            [
                new UiEntityFact { Label = "Ошибки", Value = errorCount.ToString() },
                new UiEntityFact { Label = "Предупреждения", Value = warningCount.ToString() },
                new UiEntityFact { Label = "Информация", Value = infoCount.ToString() }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "validation-summary",
                    Title = "Сводка по разделам",
                    Summary = "Где сосредоточены проблемы состояния.",
                    Icon = "list-checks",
                    CollectionLabel = "разделы",
                    Presentation = "cards",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Cards = BuildValidationSummaryCards(issues)
                },
                new UiEntityDossierSection
                {
                    Id = "validation-issues",
                    Title = "Первые проблемы",
                    Summary = issues.Count > 20
                        ? "Показаны первые 20 проблем. После их исправления запустите проверку снова."
                        : "Показаны найденные проблемы.",
                    Icon = "alert-circle",
                    CollectionLabel = "проблемы",
                    Presentation = "cards",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Cards = issues.Take(20).Select(BuildValidationIssueCard).ToList()
                }
            ]
        };
    }

    private static List<UiEntityCard> BuildValidationSummaryCards(IReadOnlyList<ValidationIssue> issues) =>
        issues
            .GroupBy(static issue => new
            {
                issue.Severity,
                issue.Category,
                Section = string.IsNullOrWhiteSpace(issue.Section) ? "General" : issue.Section
            })
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key.Severity.ToString(), StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .Select(static group => new UiEntityCard
            {
                Title = DescribeValidationSection(group.Key.Section),
                Subtitle = DescribeValidationCategory(group.Key.Category),
                Icon = "shield-alert",
                Badges =
                [
                    new UiEntityBadge
                    {
                        Label = DescribeValidationSeverity(group.Key.Severity),
                        Tone = ToneForValidationSeverity(group.Key.Severity),
                        Icon = "alert-triangle"
                    }
                ],
                Facts =
                [
                    new UiEntityFact { Label = "Количество", Value = group.Count().ToString() },
                    new UiEntityFact { Label = "Уровень", Value = DescribeValidationSeverity(group.Key.Severity) }
                ]
            })
            .ToList();

    private static UiEntityCard BuildValidationIssueCard(ValidationIssue issue)
    {
        var facts = new List<UiEntityFact>
        {
            new() { Label = "Уровень", Value = DescribeValidationSeverity(issue.Severity) },
            new() { Label = "Раздел", Value = DescribeValidationSection(issue.Section) },
            new() { Label = "Тип проверки", Value = DescribeValidationCategory(issue.Category) }
        };

        AddValidationFactIfKnown(facts, "Ожидалось", issue.Expected);
        AddValidationFactIfKnown(facts, "Получено", issue.Actual);

        var hints = new List<UiEntityHint>();
        var repairHint = SanitizeValidationPlayerText(issue.RepairHint);
        if (!string.IsNullOrWhiteSpace(repairHint))
        {
            hints.Add(new UiEntityHint
            {
                Title = "Как исправить",
                Text = repairHint,
                Tone = UiTone.Accent
            });
        }

        return new UiEntityCard
        {
            Title = $"{DescribeValidationSeverity(issue.Severity)}: {DescribeValidationSection(issue.Section)}",
            Summary = SanitizeValidationPlayerText(issue.Message),
            Icon = "alert-circle",
            Badges =
            [
                new UiEntityBadge
                {
                    Label = DescribeValidationSeverity(issue.Severity),
                    Tone = ToneForValidationSeverity(issue.Severity),
                    Icon = "alert-triangle"
                }
            ],
            Facts = facts,
            Hints = hints
        };
    }

    private static void AddValidationFactIfKnown(List<UiEntityFact> facts, string label, string? value)
    {
        var sanitized = SanitizeValidationPlayerText(value);
        if (string.IsNullOrWhiteSpace(sanitized))
            return;

        facts.Add(new UiEntityFact { Label = label, Value = sanitized });
    }

    private static string DescribeValidationSeverity(IssueSeverity severity) => severity switch
    {
        IssueSeverity.Error => "Ошибка",
        IssueSeverity.Warning => "Предупреждение",
        IssueSeverity.Info => "Информация",
        _ => "Проблема"
    };

    private static UiTone ToneForValidationSeverity(IssueSeverity severity) => severity switch
    {
        IssueSeverity.Error => UiTone.Error,
        IssueSeverity.Warning => UiTone.Warning,
        IssueSeverity.Info => UiTone.Subtle,
        _ => UiTone.Default
    };

    private static string DescribeValidationCategory(IssueCategory category) => category switch
    {
        IssueCategory.ProtocolViolation => "Нарушение игрового правила",
        IssueCategory.StateConsistency => "Согласованность состояния",
        IssueCategory.ClientOwnedSurface => "Данные клиента",
        _ => "Проверка состояния"
    };

    private static string DescribeValidationSection(string? section)
    {
        if (string.IsNullOrWhiteSpace(section))
            return "Общие данные";

        return section.Trim() switch
        {
            "General" => "Общие данные",
            "StateJson" => "Файлы состояния",
            "LoreJson" => "Лор и справочники",
            "RequiredFiles" => "Обязательные данные",
            "RequiredFields" => "Обязательные поля",
            "SoulState" => "Душа",
            "CriticalState" => "Критическое состояние",
            "WorldSetup" => "Подготовка мира",
            "ControlFiles" => "Контроль хода",
            "PendingTurnSnapshot" => "Текущий ход",
            "ProgressionSchedule" => "Расписание прогрессии",
            "SystemMods" => "Модификации",
            _ => HumanizeValidationIdentifier(section)
        };
    }

    private static string HumanizeValidationIdentifier(string value)
    {
        var normalized = value.Replace('_', ' ').Replace('-', ' ').Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return "Общие данные";

        var words = new List<string>();
        var current = new List<char>();
        foreach (var ch in normalized)
        {
            if (current.Count > 0 && char.IsUpper(ch) && !char.IsUpper(current[^1]))
            {
                words.Add(new string(current.ToArray()));
                current.Clear();
            }

            current.Add(ch);
        }

        if (current.Count > 0)
            words.Add(new string(current.ToArray()));

        return string.Join(' ', words).Trim();
    }

    private static string SanitizeValidationPlayerText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var clean = text.Trim().Replace('\\', '/');
        clean = PlayerFacingStatePathRegex.Replace(clean, "файл состояния");
        clean = PlayerFacingJsonFileRegex.Replace(clean, "файл состояния");
        clean = clean.Replace("valid JSON", "валидный JSON", StringComparison.OrdinalIgnoreCase)
            .Replace("invalid JSON", "невалидный JSON", StringComparison.OrdinalIgnoreCase)
            .Replace("canonical state file", "канонический файл состояния", StringComparison.OrdinalIgnoreCase)
            .Replace("canonical JSON", "канонический JSON", StringComparison.OrdinalIgnoreCase)
            .Replace("client-side", "на стороне клиента", StringComparison.OrdinalIgnoreCase)
            .Replace("daemon/script", "внешний процесс", StringComparison.OrdinalIgnoreCase)
            .Replace("non-JSON serialization artifacts", "посторонние данные вместо JSON", StringComparison.OrdinalIgnoreCase);
        return clean;
    }

    private static async Task<ExplorerCommandResult> BuildWorldSetupAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs);
        var pending = await ReadJson(fs, WorldDirectiveService.PendingSetupPath);
        var scenario = await ReadJson(fs, ScenarioCoreService.ManifestPath);
        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            BuildWorldSetupDossier(pending, scenario, stateManager)
        };

        var prompts = new List<UiPrompt>
        {
            new UiSelectionPrompt
            {
                Id = "world_setup_mode",
                Prompt = "Режим подготовки мира",
                Required = true,
                Options =
                [
                    Option("create_or_edit", "Создать / редактировать", "Записать или заменить подготовку следующей смертной жизни."),
                    Option("apply_profile", "Применить профиль", "Использовать профиль из папки world_profiles."),
                    Option("clear", "Очистить", "Удалить подготовку мира и сценарное ядро.")
                ]
            },
            new UiTextInputPrompt
            {
                Id = "world_title",
                Prompt = "Название мира",
                Placeholder = "Например: Королевство пепельных колоколов"
            },
            new UiLongTextInputPrompt
            {
                Id = "world_directives",
                Prompt = "Директивы мира",
                Placeholder = "Опишите жанр, запреты, обязательные темы, стартовые обстоятельства и роль персонажа.",
                MinLines = 4,
                MaxLines = 12
            }
        };

        return Result(command, localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput, blocks, prompts: prompts);
    }

    private static UiEntityDossierBlock BuildWorldSetupDossier(
        JsonReadResult pending,
        JsonReadResult scenario,
        StateManager stateManager)
    {
        var isAvailable = stateManager.CurrentState.IsInAfterlifeRealm;
        var realmName = DescribeWorldSetupRealm(stateManager.CurrentState.CurrentRealm);
        var pendingRoot = pending.Node as JsonObject;
        var scenarioRoot = scenario.Node as JsonObject;

        var sections = new List<UiEntityDossierSection>
        {
            BuildWorldDirectiveSection(pendingRoot, pending),
            BuildScenarioCoreSection(scenarioRoot, scenario)
        };

        var correctionSection = BuildScenarioCorrectionSection(scenarioRoot);
        if (correctionSection != null)
            sections.Add(correctionSection);

        return new UiEntityDossierBlock
        {
            EntityType = "world-setup",
            Title = "Подготовка следующего мира",
            Subtitle = "Настройка следующей смертной жизни",
            Summary = isAvailable
                ? "Здесь можно подготовить основу будущей смертной жизни: мир, стартовую ситуацию, тон и сценарное ядро."
                : "Подготовка следующей смертной жизни доступна в загробье. В смертной жизни для правил мира используется команда /world_rules.",
            Badges =
            [
                new UiEntityBadge
                {
                    Label = isAvailable ? "Доступно" : "Недоступно здесь",
                    Tone = isAvailable ? UiTone.Success : UiTone.Warning,
                    Icon = isAvailable ? "check-circle" : "alert-triangle"
                }
            ],
            Facts =
            [
                new UiEntityFact { Label = "Текущее место души", Value = realmName },
                new UiEntityFact { Label = "Директивы мира", Value = pending.Node == null ? "не заданы" : "подготовлены" },
                new UiEntityFact { Label = "Сценарное ядро", Value = scenario.Node == null ? "не задано" : "подготовлено" }
            ],
            Sections = sections
        };
    }

    private static UiEntityDossierSection BuildWorldDirectiveSection(JsonObject? root, JsonReadResult read)
    {
        var directives = root?["worldDirectives"] as JsonObject ?? root;
        var cards = new List<UiEntityCard>();

        if (!read.FileExists || directives == null)
        {
            cards.Add(new UiEntityCard
            {
                Title = "Директивы мира пока не заданы",
                Summary = read.FileExists && !string.IsNullOrWhiteSpace(read.Error)
                    ? "Сохранённая подготовка мира повреждена и должна быть переписана."
                    : "Можно создать новую подготовку мира через форму ниже.",
                Icon = "scroll-text"
            });
        }
        else
        {
            var title = FirstNonEmpty(
                SanitizeWorldSetupText(GetString(directives, "worldTitle")),
                SanitizeWorldSetupText(GetString(root, "worldTitle")),
                "Новая смертная жизнь");
            var summary = FirstNonEmpty(
                SanitizeWorldSetupText(GetString(directives, "settingSummary")),
                SanitizeWorldSetupText(GetString(directives, "summary")),
                "Краткое описание мира пока не указано.");

            var facts = new List<UiEntityFact>();
            AddWorldSetupFactIfKnown(facts, "Режим", DescribeWorldSetupMode(GetString(root, "mode")));
            AddWorldSetupFactIfKnown(facts, "Стартовая ситуация", GetString(directives, "startingSituation"));
            AddWorldSetupFactIfKnown(facts, "Роль персонажа", FirstNonEmpty(GetString(directives, "playerRole"), GetString(directives, "protagonistRole")));

            var nested = new List<UiEntityCard>();
            AddWorldSetupListCard(nested, "Обязательные темы", directives["mandatoryThemes"] as JsonArray);
            AddWorldSetupListCard(nested, "Запреты тона и содержания", directives["forbiddenElements"] as JsonArray);
            AddWorldSetupListCard(nested, "Важные акценты", directives["requiredElements"] as JsonArray);

            cards.Add(new UiEntityCard
            {
                Title = title,
                Summary = summary,
                Icon = "scroll-text",
                Facts = facts,
                Nested = nested
            });
        }

        return new UiEntityDossierSection
        {
            Id = "world-directives",
            Title = "Директивы мира",
            Summary = "Игровые требования к будущей смертной жизни.",
            Icon = "scroll-text",
            CollectionLabel = "директивы",
            Presentation = "cards",
            Collapsible = true,
            InitiallyExpanded = true,
            Cards = cards
        };
    }

    private static UiEntityDossierSection BuildScenarioCoreSection(JsonObject? root, JsonReadResult read)
    {
        var core = root?["scenarioCore"] as JsonObject ?? root;
        var cards = new List<UiEntityCard>();

        if (!read.FileExists || core == null)
        {
            cards.Add(new UiEntityCard
            {
                Title = "Сценарное ядро пока не задано",
                Summary = read.FileExists && !string.IsNullOrWhiteSpace(read.Error)
                    ? "Сохранённое сценарное ядро повреждено и должно быть переписано."
                    : "После подготовки мира здесь появится краткая основа будущей арки.",
                Icon = "book-open"
            });
        }
        else
        {
            var facts = new List<UiEntityFact>();
            AddWorldSetupFactIfKnown(facts, "Роль персонажа", GetString(core, "playerRole"));
            AddWorldSetupFactIfKnown(facts, "Главный конфликт", GetString(core, "mainConflict"));
            AddWorldSetupFactIfKnown(facts, "Начальная сцена", GetString(core, "openingScene"));

            cards.Add(new UiEntityCard
            {
                Title = FirstNonEmpty(SanitizeWorldSetupText(GetString(core, "title")), "Сценарное ядро"),
                Summary = FirstNonEmpty(
                    SanitizeWorldSetupText(GetString(core, "summary")),
                    "Сводка сценарного ядра пока не указана."),
                Icon = "book-open",
                Facts = facts
            });
        }

        return new UiEntityDossierSection
        {
            Id = "scenario-core",
            Title = "Сценарное ядро",
            Summary = "Сжатая основа будущего приключения.",
            Icon = "book-open",
            CollectionLabel = "ядро",
            Presentation = "cards",
            Collapsible = true,
            InitiallyExpanded = true,
            Cards = cards
        };
    }

    private static UiEntityDossierSection? BuildScenarioCorrectionSection(JsonObject? root)
    {
        if (root == null)
            return null;

        var cards = new List<UiEntityCard>();
        AddWorldSetupListCard(cards, "Кандидатные условия", root["candidateAssertions"] as JsonArray);
        AddScenarioAssertionCards(cards, root["scenarioCoreAssertions"] as JsonArray);
        AddWorldSetupListCard(cards, "Что нужно уточнить", root["openCorrectionSlots"] as JsonArray);

        if (cards.Count == 0)
            return null;

        return new UiEntityDossierSection
        {
            Id = "scenario-notes",
            Title = "Проверочные заметки",
            Summary = "Что уже зафиксировано и что ещё стоит уточнить перед стартом жизни.",
            Icon = "clipboard-check",
            CollectionLabel = "заметки",
            Presentation = "cards",
            Collapsible = true,
            InitiallyExpanded = true,
            Cards = cards
        };
    }

    private static void AddScenarioAssertionCards(List<UiEntityCard> cards, JsonArray? assertions)
    {
        if (assertions == null || assertions.Count == 0)
            return;

        var nested = new List<UiEntityCard>();
        var index = 1;
        foreach (var item in assertions)
        {
            if (item is JsonObject assertion)
            {
                nested.Add(new UiEntityCard
                {
                    Title = $"Условие {index++}",
                    Summary = SanitizeWorldSetupText(FirstNonEmpty(GetString(assertion, "assertion"), GetString(assertion, "summary"), GetString(assertion, "text"))),
                    Icon = "check-circle",
                    Facts =
                    [
                        new UiEntityFact { Label = "Статус", Value = DescribeScenarioAssertionStatus(GetString(assertion, "status")) }
                    ]
                });
                continue;
            }

            var scalar = SanitizeWorldSetupText(NodeToPlayerText(item));
            if (!string.IsNullOrWhiteSpace(scalar))
            {
                nested.Add(new UiEntityCard
                {
                    Title = $"Условие {index++}",
                    Summary = scalar,
                    Icon = "check-circle"
                });
            }
        }

        if (nested.Count == 0)
            return;

        cards.Add(new UiEntityCard
        {
            Title = "Проверенные условия ядра",
            Summary = "Условия, которые должны оставаться верными при запуске будущей жизни.",
            Icon = "clipboard-check",
            Nested = nested
        });
    }

    private static void AddWorldSetupListCard(List<UiEntityCard> cards, string title, JsonArray? values)
    {
        if (values == null || values.Count == 0)
            return;

        var items = values
            .Select(NodeToPlayerText)
            .Select(SanitizeWorldSetupText)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .ToList();
        if (items.Count == 0)
            return;

        cards.Add(new UiEntityCard
        {
            Title = title,
            Icon = "list",
            List = items
        });
    }

    private static void AddWorldSetupFactIfKnown(List<UiEntityFact> facts, string label, string? value)
    {
        var sanitized = SanitizeWorldSetupText(value);
        if (string.IsNullOrWhiteSpace(sanitized))
            return;

        facts.Add(new UiEntityFact { Label = label, Value = sanitized });
    }

    private static string NodeToPlayerText(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
                return text ?? string.Empty;
            if (value.TryGetValue<int>(out var number))
                return number.ToString();
            if (value.TryGetValue<bool>(out var boolean))
                return boolean ? "да" : "нет";
        }

        return string.Empty;
    }

    private static string DescribeWorldSetupRealm(string? realm) => realm switch
    {
        null or "" => "не указано",
        "Chaos Sea" => "Море Хаоса",
        "Shining Abode" => "Сияющая Обитель",
        "Mortal World" => "Смертный мир",
        _ => SanitizeWorldSetupText(realm)
    };

    private static string DescribeWorldSetupMode(string? mode) => mode switch
    {
        null or "" => string.Empty,
        "manual" => "ручная настройка",
        "profile" => "профиль мира",
        "create_or_edit" => "создание или редактирование",
        "apply_profile" => "профиль мира",
        "clear" => "очистка подготовки",
        _ => SanitizeWorldSetupText(mode)
    };

    private static string DescribeScenarioAssertionStatus(string? status) => status switch
    {
        null or "" => "не указан",
        "confirmed" => "подтверждено",
        "pending" => "требует проверки",
        "rejected" => "отклонено",
        _ => SanitizeWorldSetupText(status)
    };

    private static string SanitizeWorldSetupText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var clean = text.Trim().Replace('\\', '/');
        clean = PlayerFacingStatePathRegex.Replace(clean, "служебная запись");
        clean = PlayerFacingJsonFileRegex.Replace(clean, "служебная запись");
        clean = clean.Replace("scenarioCore", "сценарное ядро", StringComparison.OrdinalIgnoreCase)
            .Replace("worldDirectives", "директивы мира", StringComparison.OrdinalIgnoreCase)
            .Replace("currentRealm", "текущее место души", StringComparison.OrdinalIgnoreCase);
        return clean;
    }

    private static async Task<ExplorerCommandResult> BuildStatDistributionAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs);
        var statPoints = await ReadJson(fs, "game_state/player/stat_points.json");
        var characteristics = await ReadJson(fs, "game_state/misc/characteristics.json");
        var unspent = TryGetInt(statPoints.Node as JsonObject, "unspentStatPoints", 0);
        var characteristicCards = Characteristics.All
            .Select(stat => BuildStatDistributionCard(characteristics.Node as JsonObject, stat))
            .ToList();
        var prompts = Characteristics.All
            .Select(stat =>
            {
                var name = Characteristics.RussianNames.GetValueOrDefault(stat, stat);
                return (UiPrompt)new UiTextInputPrompt
                {
                    Id = $"stat_{stat}",
                    Prompt = $"{name}: добавить очков",
                    Placeholder = "0",
                    DefaultValue = "0",
                    Required = false
                };
            })
            .ToList();

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiEntityDossierBlock
            {
                EntityType = "stat-distribution",
                Title = "Распределение характеристик",
                Summary = unspent > 0
                    ? $"Доступно очков характеристик: {unspent}. Укажите, сколько очков добавить к нужным характеристикам."
                    : "Нераспределённых очков характеристик сейчас нет.",
                Facts =
                [
                    new UiEntityFact { Label = "Доступно очков", Value = unspent.ToString() }
                ],
                Hints =
                [
                    new UiEntityHint
                    {
                        Title = "Как распределить",
                        Text = unspent > 0
                            ? "Заполните только те характеристики, которые хотите повысить. Пустые поля и нули не тратят очки."
                            : "Очков для распределения сейчас нет, но текущие значения можно посмотреть ниже.",
                        Tone = unspent > 0 ? UiTone.Accent : UiTone.Muted
                    }
                ],
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "stat-distribution-characteristics",
                        Title = "Текущие характеристики",
                        Icon = "package",
                        Presentation = "cards",
                        CollectionLabel = $"{characteristicCards.Count} характеристик",
                        Cards = characteristicCards
                    }
                ]
            }
        };

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts: prompts);
    }

    private static UiEntityCard BuildStatDistributionCard(JsonObject? characteristics, string stat)
    {
        var name = Characteristics.RussianNames.GetValueOrDefault(stat, stat);
        var value = TryGetInt(characteristics, stat, 1);
        return new UiEntityCard
        {
            Title = name,
            Subtitle = "Характеристика",
            Summary = $"Текущее значение: {value}.",
            Icon = "package",
            Facts =
            [
                new UiEntityFact { Label = "Текущее значение", Value = value.ToString() }
            ]
        };
    }

    private static async Task<ExplorerCommandResult> BuildCompanionDirectiveAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs);
        var npcCore = await ReadJson(fs, "game_state/npcs/npc_core.json");
        var companions = CollectObjects(npcCore.Node)
            .Where(static item => string.Equals(GetString(item, "progressionType"), "Companion", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var companionCards = companions.Select(BuildCompanionDirectiveCard).ToList();

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiEntityDossierBlock
            {
                EntityType = "companion-directives",
                Title = "Директивы компаньонов",
                Summary = companions.Count == 0
                    ? "Активные компаньоны не найдены."
                    : "Здесь можно задать короткий стратегический приказ постоянному спутнику.",
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "companion-directive-list",
                        Title = "Компаньоны",
                        Icon = "users",
                        Presentation = "cards",
                        CollectionLabel = companionCards.Count == 1 ? "1 компаньон" : $"{companionCards.Count} компаньонов",
                        Cards = companionCards
                    }
                ]
            }
        };

        if (companions.Count == 0)
            return Result(command, localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.Completed, blocks);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "companion_id",
                    Prompt = "Компаньон",
                    Required = true,
                    Options = companions.Select(static item =>
                    {
                        var id = FirstNonEmpty(GetString(item, "npcId"), GetString(item, "id"));
                        var name = FirstNonEmpty(GetString(item, "name"), GetString(item, "displayName"), id);
                        var directive = FirstNonEmpty(GetString(item, "playerCompanionDirective"), "директива не задана");
                        return Option(id, name, directive);
                    }).ToList()
                },
                new UiLongTextInputPrompt
                {
                    Id = "companion_directive",
                    Prompt = "Новая директива",
                    Placeholder = "Кратко опишите, как компаньон должен действовать.",
                    MinLines = 2,
                    MaxLines = 6
                }
            ]);
    }

    private static UiEntityCard BuildCompanionDirectiveCard(JsonObject item)
    {
        var name = FirstNonEmpty(GetString(item, "name"), GetString(item, "displayName"), "Компаньон");
        var directive = FirstNonEmpty(GetString(item, "playerCompanionDirective"), "Директива пока не задана.");
        return new UiEntityCard
        {
            Title = name,
            Subtitle = "Компаньон",
            Summary = directive,
            Icon = "users",
            Facts =
            [
                new UiEntityFact { Label = "Текущая директива", Value = directive }
            ]
        };
    }

    private static async Task<ExplorerCommandResult> BuildFactionDirectiveAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs);
        var factionsRoot = await ReadJson(fs, "game_state/factions/faction_core.json");
        var factions = CollectObjects(factionsRoot.Node)
            .Where(static item => TryGetBool(item, "isPlayerFaction") || TryGetBool(item, "isPlayerMember"))
            .ToList();
        var factionCards = factions.Select(BuildFactionDirectiveCard).ToList();

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiEntityDossierBlock
            {
                EntityType = "faction-directives",
                Title = "Директивы фракций",
                Summary = factions.Count == 0
                    ? "Фракции игрока или членство во фракциях не найдены."
                    : "Здесь можно задать стратегический приказ фракции, которой игрок руководит или в которой участвует.",
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "faction-directive-list",
                        Title = "Фракции",
                        Icon = "flag",
                        Presentation = "cards",
                        CollectionLabel = factionCards.Count == 1 ? "1 фракция" : $"{factionCards.Count} фракций",
                        Cards = factionCards
                    }
                ]
            }
        };

        if (factions.Count == 0)
            return Result(command, localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.Completed, blocks);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "faction_id",
                    Prompt = "Фракция",
                    Required = true,
                    Options = factions.Select(static item =>
                    {
                        var id = FirstNonEmpty(GetString(item, "factionId"), GetString(item, "id"));
                        var name = FirstNonEmpty(GetString(item, "name"), GetString(item, "factionName"), id);
                        var role = TryGetBool(item, "isPlayerFaction") ? "лидер" : "участник";
                        return Option(id, name, role);
                    }).ToList()
                },
                new UiLongTextInputPrompt
                {
                    Id = "faction_directive",
                    Prompt = "Новая стратегическая директива",
                    Placeholder = "Опишите приоритеты фракции.",
                    MinLines = 2,
                    MaxLines = 6
                }
            ]);
    }

    private static UiEntityCard BuildFactionDirectiveCard(JsonObject item)
    {
        var name = FirstNonEmpty(GetString(item, "name"), GetString(item, "factionName"), "Фракция");
        var directive = FirstNonEmpty(GetString(item, "playerStrategyDirective"), "Директива пока не задана.");
        var role = TryGetBool(item, "isPlayerFaction") ? "лидер" : "участник";
        return new UiEntityCard
        {
            Title = name,
            Subtitle = role,
            Summary = directive,
            Icon = "flag",
            Facts =
            [
                new UiEntityFact { Label = "Роль игрока", Value = role },
                new UiEntityFact { Label = "Текущая директива", Value = directive }
            ]
        };
    }

    private static async Task<ExplorerCommandResult> BuildInventoryEquipAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var inventory = await InventoryEquipmentService.ReadContextAsync(fs);
        var requestedItem = InventoryEquipmentService.ReadFirstCommandArgument(command);
        var candidates = inventory?.Items
            .Where(static item => item.IsEquippable &&
                                  string.IsNullOrWhiteSpace(item.EquippedSlot) &&
                                  !item.IsSoulRelic &&
                                  !item.IsBroken)
            .ToList() ?? [];
        var matchedRequestedItem = InventoryEquipmentService.FindItem(candidates, requestedItem);
        if (matchedRequestedItem != null)
            candidates = [matchedRequestedItem];

        var statusText = candidates.Count == 0
            ? "В рюкзаке нет обычных предметов, которые можно экипировать."
            : "Выберите предмет, слот и подтвердите экипировку. Запись выполняется локально после проверки хода и блокировки интерфейса.";

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            BuildInventoryActionDossier(
                "inventory-equip",
                "Экипировка предмета",
                statusText,
                "inventory-equip-candidates",
                "Доступные предметы",
                candidates.Count == 1 ? "1 предмет" : $"{candidates.Count} предметов",
                candidates.Select(static item => BuildInventoryEquipmentCandidateCard(item)).ToList(),
                candidates.Count == 0 ? UiTone.Muted : UiTone.Accent)
        };

        if (candidates.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        var selected = candidates.Count == 1 ? candidates[0] : null;
        IReadOnlyList<string> slotKeys = selected is { ResolvedSlot.Length: > 0 }
            ? [selected.ResolvedSlot]
            : InventoryEquipmentService.SlotLabels.Keys.ToArray();

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "item_identity",
                    Prompt = "Предмет",
                    Required = true,
                    Options = candidates
                        .Select(static item => Option(
                            FirstNonEmpty(item.Identity, item.Name),
                            item.Name,
                            string.IsNullOrWhiteSpace(item.ResolvedSlot)
                                ? "Слот нужно выбрать вручную."
                                : $"Подходит: {InventoryEquipmentService.FormatSlotName(item.ResolvedSlot)}."))
                        .ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "equipment_slot",
                    Prompt = "Слот экипировки",
                    Required = true,
                    Options = slotKeys
                        .Select(static slot => Option(slot, InventoryEquipmentService.FormatSlotName(slot), ""))
                        .ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_inventory_write",
                    Prompt = "Подтвердить изменение экипировки",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildInventoryUnequipAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var inventory = await InventoryEquipmentService.ReadContextAsync(fs);
        var requestedSlot = InventoryEquipmentService.ReadFirstCommandArgument(command);
        var equipped = inventory?.Equipped
            .Where(static item => item.IsOrdinaryInventoryItem)
            .ToList() ?? [];
        if (InventoryEquipmentService.TryNormalizeSlot(requestedSlot, out var normalizedSlot))
        {
            var matched = equipped.FirstOrDefault(item =>
                string.Equals(item.SlotKey, normalizedSlot, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
                equipped = [matched];
        }

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            BuildInventoryActionDossier(
                "inventory-unequip",
                "Снятие предмета",
                equipped.Count == 0
                    ? "Обычные экипированные предметы не найдены."
                    : "Выберите слот и подтвердите снятие предмета в рюкзак.",
                "inventory-equipped-items",
                "Экипировано",
                equipped.Count == 1 ? "1 предмет" : $"{equipped.Count} предметов",
                equipped.Select(static item => BuildEquippedInventoryCard(item)).ToList(),
                equipped.Count == 0 ? UiTone.Muted : UiTone.Accent)
        };

        if (equipped.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "equipment_slot",
                    Prompt = "Что снять",
                    Required = true,
                    Options = equipped
                        .Select(static item => Option(item.SlotKey, $"{item.SlotLabel}: {item.ItemName}", ""))
                        .ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_inventory_write",
                    Prompt = "Подтвердить снятие предмета",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildInventoryDropAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var inventory = await InventoryManagementService.ReadContextAsync(fs);
        var requestedItem = InventoryEquipmentService.ReadFirstCommandArgument(command);
        var allItems = inventory?.Items.ToList() ?? [];
        var matchedRequestedItem = InventoryManagementService.FindItem(allItems, requestedItem);
        var candidates = new List<InventoryManagementItem>();
        if (!string.IsNullOrWhiteSpace(requestedItem))
        {
            if (matchedRequestedItem != null)
                candidates.Add(matchedRequestedItem);
        }
        else
        {
            candidates.AddRange(allItems);
        }

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            BuildInventoryActionDossier(
                "inventory-drop",
                "Выброс предмета",
                BuildInventoryDropStatusText(candidates.Count, requestedItem),
                "inventory-drop-candidates",
                "Доступные предметы",
                candidates.Count == 1 ? "1 предмет" : $"{candidates.Count} предметов",
                candidates.Select(static item => BuildInventoryManagementCard(item)).ToList(),
                candidates.Count == 0 ? UiTone.Muted : UiTone.Accent)
        };

        if (candidates.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "item_identity",
                    Prompt = "Предмет",
                    Required = true,
                    Options = candidates.Select(BuildInventoryOption).ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_inventory_drop",
                    Prompt = "Подтвердить выброс предмета",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildInventorySplitAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var inventory = await InventoryManagementService.ReadContextAsync(fs);
        var requestedItem = InventoryEquipmentService.ReadFirstCommandArgument(command);
        var allItems = inventory?.Items.ToList() ?? [];
        var matchedRequestedItem = InventoryManagementService.FindItem(allItems, requestedItem);
        var candidates = new List<InventoryManagementItem>();
        if (!string.IsNullOrWhiteSpace(requestedItem))
        {
            if (matchedRequestedItem is { Count: > 1 })
                candidates.Add(matchedRequestedItem);
        }
        else
        {
            candidates.AddRange(allItems.Where(static item => item.Count > 1));
        }

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            BuildInventoryActionDossier(
                "inventory-split",
                "Разделение стопки",
                BuildInventorySplitStatusText(candidates.Count, matchedRequestedItem, requestedItem),
                "inventory-split-candidates",
                "Стопки для разделения",
                candidates.Count == 1 ? "1 стопка" : $"{candidates.Count} стопок",
                candidates
                    .Select(static item => BuildInventoryManagementCard(
                        item,
                        "Стопка",
                        [new UiEntityFact { Label = "Можно отделить", Value = $"1-{item.Count - 1}" }]))
                    .ToList(),
                candidates.Count == 0 ? UiTone.Muted : UiTone.Accent)
        };

        if (candidates.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        var selected = candidates.Count == 1 ? candidates[0] : null;
        var quantityPrompt = selected == null
            ? "Сколько отделить"
            : $"Сколько отделить (1-{selected.Count - 1})";

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "item_identity",
                    Prompt = "Стопка",
                    Required = true,
                    Options = candidates.Select(BuildInventorySplitOption).ToList()
                },
                new UiTextInputPrompt
                {
                    Id = "split_quantity",
                    Prompt = quantityPrompt,
                    Placeholder = selected == null ? "Количество меньше выбранной стопки" : "1",
                    Required = true
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_inventory_split",
                    Prompt = "Подтвердить разделение стопки",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildInventoryMergeAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var inventory = await InventoryManagementService.ReadContextAsync(fs);
        var requestedItem = InventoryEquipmentService.ReadFirstCommandArgument(command);
        var allItems = inventory?.Items.ToList() ?? [];
        var matchedRequestedItem = InventoryManagementService.FindItem(allItems, requestedItem);
        var candidates = new List<InventoryManagementItem>();

        if (inventory != null)
        {
            if (!string.IsNullOrWhiteSpace(requestedItem))
            {
                if (matchedRequestedItem != null &&
                    InventoryManagementService.FindCompatibleStacks(inventory, matchedRequestedItem).Count > 1)
                {
                    candidates.Add(matchedRequestedItem);
                }
            }
            else
            {
                candidates.AddRange(allItems.Where(item =>
                    InventoryManagementService.FindCompatibleStacks(inventory, item).Count > 1));
            }
        }

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            BuildInventoryActionDossier(
                "inventory-merge",
                "Объединение стопок",
                BuildInventoryMergeStatusText(candidates.Count, matchedRequestedItem, requestedItem),
                "inventory-merge-candidates",
                "Совместимые стопки",
                candidates.Count == 1 ? "1 группа стопок" : $"{candidates.Count} групп стопок",
                inventory == null
                    ? []
                    : candidates.Select(item => BuildInventoryMergeCard(inventory, item)).ToList(),
                candidates.Count == 0 ? UiTone.Muted : UiTone.Accent)
        };

        if (candidates.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "item_identity",
                    Prompt = "Какие стопки объединить",
                    Required = true,
                    Options = candidates.Select(item => BuildInventoryMergeOption(inventory!, item)).ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_inventory_merge",
                    Prompt = "Подтвердить объединение стопок",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildStorageItemMoveAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var currentRealm = GetString((await ReadJson(fs, SoulStatePath)).Node as JsonObject, "currentRealm");
        if (!RealmSemantics.IsMortalRealm(currentRealm))
            return StorageTransportMortalRealmBlocker(command, localTurn.Panel);

        if (localTurn.HasActiveGmTurn)
            return Result(command, CommandExecutionState.Pending, [localTurn.Panel]);

        var context = await StorageTransportMoveService.ReadStorageMoveContextAsync(fs);
        var blocks = new List<UiBlock> { localTurn.Panel };
        if (!context.Success)
        {
            blocks.Add(Message(UiNotificationSeverity.Warning, "Хранилище недоступно", context.Message));
            return Result(command, CommandExecutionState.Completed, blocks);
        }

        var canDeposit = context.InventoryItems.Count > 0 && context.Storages.Count > 0;
        var canRetrieve = context.Storages.Any(static storage => storage.Contents.Count > 0);
        var statusText = (canDeposit, canRetrieve) switch
        {
            (true, true) => "Выберите направление, хранилище и предмет. Перемещение выполняется локально после повторной проверки состояния.",
            (true, false) => "Можно положить предмет из рюкзака в доступное хранилище. В хранилищах сейчас нет предметов для возврата.",
            (false, true) => "Можно забрать предмет из доступного хранилища в рюкзак. В рюкзаке сейчас нет предметов для вклада.",
            _ => "Сейчас нет доступных предметов для перемещения между рюкзаком и хранилищем."
        };

        blocks.Add(BuildStorageTransportMoveDossier(
            "storage-move",
            "Предметы в хранилище",
            statusText,
            "available-storages",
            "Доступные хранилища",
            context.Storages.Count == 1 ? "1 хранилище" : $"{context.Storages.Count} хранилищ",
            context.Storages.Select(static storage => BuildStorageMoveStorageCard(storage)).ToList(),
            "storage-inventory-items",
            "Рюкзак",
            context.InventoryItems.Count == 1 ? "1 предмет" : $"{context.InventoryItems.Count} предметов",
            context.InventoryItems.Select(static item => BuildStorageTransportItemCard(item, "В рюкзаке")).ToList(),
            canDeposit || canRetrieve ? UiTone.Accent : UiTone.Muted));

        if (!canDeposit && !canRetrieve)
            return Result(command, CommandExecutionState.Completed, blocks);

        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "storage_move_direction",
                    Prompt = "Что сделать",
                    Required = true,
                    Options =
                    [
                        Option("deposit", "Положить в хранилище", canDeposit ? "Из рюкзака в выбранное хранилище." : "В рюкзаке нет предметов для вклада.", disabled: !canDeposit),
                        Option("retrieve", "Забрать из хранилища", canRetrieve ? "Из выбранного хранилища в рюкзак." : "В доступных хранилищах сейчас нет предметов.", disabled: !canRetrieve)
                    ]
                },
                new UiSelectionPrompt
                {
                    Id = "storage_key",
                    Prompt = "Хранилище",
                    Required = true,
                    Options = context.Storages
                        .Select(static storage => Option(storage.Key, storage.Name, $"Предметов: {storage.ContentsCount}."))
                        .ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "inventory_item_key",
                    Prompt = "Предмет из рюкзака",
                    Required = false,
                    Options = context.InventoryItems
                        .Select(static item => Option(item.Key, item.Label, item.Description))
                        .ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "storage_item_key",
                    Prompt = "Предмет из хранилища",
                    Required = false,
                    Options = context.Storages
                        .SelectMany(static storage => storage.Contents.Select(item => Option(item.Key, item.Label, item.Description)))
                        .ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_storage_move",
                    Prompt = "Подтвердить перемещение",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildVehicleItemMoveAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var currentRealm = GetString((await ReadJson(fs, SoulStatePath)).Node as JsonObject, "currentRealm");
        if (!RealmSemantics.IsMortalRealm(currentRealm))
            return StorageTransportMortalRealmBlocker(command, localTurn.Panel);

        if (localTurn.HasActiveGmTurn)
            return Result(command, CommandExecutionState.Pending, [localTurn.Panel]);

        var context = await StorageTransportMoveService.ReadVehicleMoveContextAsync(fs);
        var blocks = new List<UiBlock> { localTurn.Panel };
        if (!context.Success)
        {
            blocks.Add(Message(UiNotificationSeverity.Warning, "Транспорт недоступен", context.Message));
            return Result(command, CommandExecutionState.Completed, blocks);
        }

        var canDeposit = context.InventoryItems.Count > 0 && context.Vehicles.Count > 0;
        var canRetrieve = context.Vehicles.Any(static vehicle => vehicle.Contents.Count > 0);
        var statusText = (canDeposit, canRetrieve) switch
        {
            (true, true) => "Выберите направление, транспорт и предмет. Перемещение выполняется локально после повторной проверки состояния.",
            (true, false) => "Можно положить предмет из рюкзака в транспорт. В транспорте сейчас нет предметов для возврата.",
            (false, true) => "Можно забрать предмет из транспорта в рюкзак. В рюкзаке сейчас нет предметов для вклада.",
            _ => "Сейчас нет доступных предметов для перемещения между рюкзаком и транспортом."
        };

        blocks.Add(BuildStorageTransportMoveDossier(
            "vehicle-move",
            "Предметы в транспорте",
            statusText,
            "available-vehicles",
            "Доступный транспорт",
            context.Vehicles.Count == 1 ? "1 транспорт" : $"{context.Vehicles.Count} транспорта",
            context.Vehicles.Select(static vehicle => BuildVehicleMoveVehicleCard(vehicle)).ToList(),
            "vehicle-inventory-items",
            "Рюкзак",
            context.InventoryItems.Count == 1 ? "1 предмет" : $"{context.InventoryItems.Count} предметов",
            context.InventoryItems.Select(static item => BuildStorageTransportItemCard(item, "В рюкзаке")).ToList(),
            canDeposit || canRetrieve ? UiTone.Accent : UiTone.Muted));

        if (!canDeposit && !canRetrieve)
            return Result(command, CommandExecutionState.Completed, blocks);

        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "vehicle_move_direction",
                    Prompt = "Что сделать",
                    Required = true,
                    Options =
                    [
                        Option("deposit", "Положить в транспорт", canDeposit ? "Из рюкзака в выбранный транспорт." : "В рюкзаке нет предметов для вклада.", disabled: !canDeposit),
                        Option("retrieve", "Забрать из транспорта", canRetrieve ? "Из выбранного транспорта в рюкзак." : "В транспорте сейчас нет предметов.", disabled: !canRetrieve)
                    ]
                },
                new UiSelectionPrompt
                {
                    Id = "vehicle_key",
                    Prompt = "Транспорт",
                    Required = true,
                    Options = context.Vehicles
                        .Select(static vehicle => Option(vehicle.Key, vehicle.Name, $"Предметов: {vehicle.ContentsCount}."))
                        .ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "inventory_item_key",
                    Prompt = "Предмет из рюкзака",
                    Required = false,
                    Options = context.InventoryItems
                        .Select(static item => Option(item.Key, item.Label, item.Description))
                        .ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "vehicle_item_key",
                    Prompt = "Предмет из транспорта",
                    Required = false,
                    Options = context.Vehicles
                        .SelectMany(static vehicle => vehicle.Contents.Select(item => Option(item.Key, item.Label, item.Description)))
                        .ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_vehicle_move",
                    Prompt = "Подтвердить перемещение",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static UiEntityDossierBlock BuildStorageTransportMoveDossier(
        string entityType,
        string title,
        string summary,
        string targetSectionId,
        string targetSectionTitle,
        string targetCollectionLabel,
        List<UiEntityCard> targetCards,
        string inventorySectionId,
        string inventorySectionTitle,
        string inventoryCollectionLabel,
        List<UiEntityCard> inventoryCards,
        UiTone summaryTone) =>
        new()
        {
            EntityType = entityType,
            Title = title,
            Summary = summary,
            Hints =
            [
                new UiEntityHint
                {
                    Title = "Как выбрать",
                    Text = summary,
                    Tone = summaryTone
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = targetSectionId,
                    Title = targetSectionTitle,
                    Icon = "archive",
                    Presentation = "cards",
                    CollectionLabel = targetCollectionLabel,
                    Cards = targetCards
                },
                new UiEntityDossierSection
                {
                    Id = inventorySectionId,
                    Title = inventorySectionTitle,
                    Summary = "Предметы, которые можно положить в выбранное место.",
                    Icon = "package",
                    Presentation = "cards",
                    CollectionLabel = inventoryCollectionLabel,
                    Cards = inventoryCards
                }
            ]
        };

    private static UiEntityCard BuildStorageMoveStorageCard(StorageMoveStorage storage) =>
        new()
        {
            Title = storage.Name,
            Subtitle = "Хранилище",
            Summary = storage.ContentsCount == 0
                ? "Хранилище доступно, но сейчас пусто."
                : $"Внутри: {FormatInventoryCount(storage.ContentsCount)}.",
            Icon = "archive",
            Facts =
            [
                new UiEntityFact { Label = "Предметов внутри", Value = FormatInventoryCount(storage.ContentsCount) },
                new UiEntityFact { Label = "Доступ", Value = "открыт" }
            ],
            Nested = storage.Contents
                .Select(static item => BuildStorageTransportItemCard(item, "Внутри хранилища"))
                .ToList()
        };

    private static UiEntityCard BuildVehicleMoveVehicleCard(VehicleMoveVehicle vehicle) =>
        new()
        {
            Title = vehicle.Name,
            Subtitle = "Транспорт",
            Summary = vehicle.ContentsCount == 0
                ? "Транспорт доступен, но сейчас пуст."
                : $"В транспорте: {FormatInventoryCount(vehicle.ContentsCount)}.",
            Icon = "package",
            Facts =
            [
                new UiEntityFact { Label = "Предметов внутри", Value = FormatInventoryCount(vehicle.ContentsCount) }
            ],
            Nested = vehicle.Contents
                .Select(static item => BuildStorageTransportItemCard(item, "В транспорте"))
                .ToList()
        };

    private static UiEntityCard BuildStorageTransportItemCard(StorageTransportItemOption item, string subtitle) =>
        new()
        {
            Title = item.Label,
            Subtitle = subtitle,
            Summary = item.Description,
            Icon = "package",
            Facts =
            [
                new UiEntityFact { Label = "Предмет", Value = item.Name },
                new UiEntityFact { Label = "Состояние", Value = item.Description }
            ]
        };

    private static ExplorerCommandResult StorageTransportMortalRealmBlocker(string command, UiPanelBlock localTurnPanel) =>
        Result(
            command,
            CommandExecutionState.Blocked,
            [
                localTurnPanel,
                Message(
                    UiNotificationSeverity.Warning,
                    "Перемещение предметов недоступно",
                    "Перемещение предметов между рюкзаком, хранилищем и транспортом доступно только в смертном мире. Сейчас действие недоступно для текущего царства.")
            ]);

    private static string BuildInventoryDropStatusText(int candidateCount, string requestedItem)
    {
        if (candidateCount == 0)
            return string.IsNullOrWhiteSpace(requestedItem)
                ? "В инвентаре нет предметов для выброса."
                : "Выбранный предмет не найден в инвентаре.";

        return "Выберите предмет и подтвердите выброс. Если предмет сейчас экипирован, его слот будет освобождён.";
    }

    private static string BuildInventorySplitStatusText(
        int candidateCount,
        InventoryManagementItem? requestedItem,
        string requestedItemText)
    {
        if (candidateCount > 0)
            return "Выберите стопку, укажите количество и подтвердите разделение.";

        if (!string.IsNullOrWhiteSpace(requestedItemText) && requestedItem != null)
            return "У выбранного предмета нет стопки, которую можно разделить.";

        return string.IsNullOrWhiteSpace(requestedItemText)
            ? "В инвентаре нет стопок, которые можно разделить."
            : "Выбранная стопка не найдена.";
    }

    private static string BuildInventoryMergeStatusText(
        int candidateCount,
        InventoryManagementItem? requestedItem,
        string requestedItemText)
    {
        if (candidateCount > 0)
            return "Выберите совместимые стопки и подтвердите объединение.";

        if (!string.IsNullOrWhiteSpace(requestedItemText) && requestedItem != null)
            return "Для выбранного предмета нет другой совместимой стопки.";

        return string.IsNullOrWhiteSpace(requestedItemText)
            ? "В инвентаре нет совместимых стопок для объединения."
            : "Выбранная стопка не найдена.";
    }

    private static UiSelectionOption BuildInventoryOption(InventoryManagementItem item) =>
        Option(
            FirstNonEmpty(item.Identity, item.Name),
            item.Name,
            $"{FormatInventoryCount(item)}. {FormatInventoryPlacement(item)}.");

    private static UiSelectionOption BuildInventorySplitOption(InventoryManagementItem item) =>
        Option(
            FirstNonEmpty(item.Identity, item.Name),
            item.Name,
            $"Сейчас {item.Count} шт.; можно отделить 1-{item.Count - 1}.");

    private static UiSelectionOption BuildInventoryMergeOption(
        InventoryManagementContext inventory,
        InventoryManagementItem item)
    {
        var compatible = InventoryManagementService.FindCompatibleStacks(inventory, item);
        return Option(
            FirstNonEmpty(item.Identity, item.Name),
            item.Name,
            $"Совместимых стопок: {compatible.Count}. После объединения будет {compatible.Sum(static match => match.Count)} шт.");
    }

    private static string FormatInventoryCount(InventoryManagementItem item) =>
        FormatInventoryCount(item.Count);

    private static string FormatInventoryCount(int count) =>
        count == 1 ? "1 шт." : $"{count} шт.";

    private static string FormatInventoryPlacement(InventoryManagementItem item) =>
        string.IsNullOrWhiteSpace(item.EquippedSlot)
            ? "В рюкзаке"
            : $"Экипирован: {InventoryEquipmentService.FormatSlotName(item.EquippedSlot)}";

    private static void AddInventoryFactIfKnown(List<UiEntityFact> facts, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        facts.Add(new UiEntityFact { Label = label, Value = value.Trim() });
    }

    private static string FormatInventoryDurability(JsonObject item)
    {
        var durability = GetString(item, "durability");
        if (string.IsNullOrWhiteSpace(durability))
            return string.Empty;

        var maxDurability = GetString(item, "maxDurability");
        return string.IsNullOrWhiteSpace(maxDurability)
            ? durability
            : $"{durability}/{maxDurability}";
    }

    private static string FormatInventoryItemTypeForPlayer(string? itemType)
    {
        if (string.IsNullOrWhiteSpace(itemType))
            return string.Empty;

        return itemType.Trim().ToLowerInvariant() switch
        {
            "weapon" => "оружие",
            "helmet" or "head" => "шлем",
            "armor" or "chest" => "доспех",
            "boots" or "feet" => "обувь",
            "gloves" or "hands" => "перчатки",
            "ring" or "ring1" or "ring2" => "кольцо",
            "amulet" or "neck" => "амулет",
            "utility" => "полезный предмет",
            "consumable" => "расходуемый предмет",
            "document" => "документ",
            "book" => "книга",
            "soul_relic" or "soulrelic" => "реликвия души",
            _ => itemType.Trim()
        };
    }

    private static async Task<ExplorerCommandResult> BuildNpcTalkAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var currentRealm = GetString((await ReadJson(fs, SoulStatePath)).Node as JsonObject, "currentRealm");
        if (!RealmSemantics.IsMortalRealm(currentRealm))
        {
            return Result(
                command,
                CommandExecutionState.Blocked,
                [
                    localTurn.Panel,
                    Message(
                        UiNotificationSeverity.Warning,
                        "Разговор с НПС недоступен",
                        "Разговор с НПС можно начать только в смертном мире. Сейчас действие недоступно для текущего царства.")
                ]);
        }

        var requestedNpcId = ReadCommandArguments(command);
        var npcRead = await ReadJson(fs, "game_state/npcs/npc_core.json");
        if (!string.IsNullOrWhiteSpace(npcRead.Error))
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(
                        UiNotificationSeverity.Warning,
                        "Разговор с НПС",
                        "Список известных персонажей временно недоступен. Повторите действие после проверки состояния.")
                ]);
        }

        var npcs = CollectNpcConversationOptions(npcRead.Node).ToList();
        if (npcs.Count == 0)
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(
                        UiNotificationSeverity.Info,
                        "Разговор с НПС",
                        "Сейчас нет известных персонажей, с которыми можно начать отдельный разговор.")
                ]);
        }

        var selected = ResolveNpcConversationOption(npcs, requestedNpcId);
        var orderedNpcs = npcs
            .OrderByDescending(npc => selected != null && string.Equals(npc.NpcId, selected.NpcId, StringComparison.OrdinalIgnoreCase))
            .ThenBy(npc => npc.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var statusText = selected == null
            ? string.IsNullOrWhiteSpace(requestedNpcId)
                ? "Выберите собеседника и кратко опишите тему разговора. ГМ разыграет сцену в следующем принятом ходе."
                : "Указанный собеседник не найден среди известных персонажей. Выберите персонажа из списка или уточните ввод."
            : $"Выбран собеседник: {selected.Name}. Можно выбрать другого персонажа из списка.";

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Разговор с НПС",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = statusText,
                        Tone = selected == null && !string.IsNullOrWhiteSpace(requestedNpcId) ? UiTone.Warning : UiTone.Accent
                    },
                    new UiTableBlock
                    {
                        Title = "Известные персонажи",
                        Columns = ["Имя", "Где находится", "Отношение"],
                        Rows = orderedNpcs
                            .Take(20)
                            .Select(static npc => Row(
                                npc.Name,
                                FirstNonEmpty(npc.Location, "-"),
                                string.IsNullOrWhiteSpace(npc.Relationship) ? "-" : npc.Relationship))
                            .ToList()
                    }
                ]
            }
        };

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "npc_id",
                    Prompt = "С кем поговорить",
                    Required = selected == null,
                    AllowCustom = true,
                    Options = orderedNpcs
                        .Select(static npc => Option(
                            npc.NpcId,
                            npc.Name,
                            FirstNonEmpty(npc.Location, "Известный персонаж")))
                        .ToList()
                },
                new UiLongTextInputPrompt
                {
                    Id = "npc_conversation_topic",
                    Prompt = "Опишите тему разговора",
                    Required = true,
                    Placeholder = "Кратко опишите, о чём спросить или что обсудить.",
                    MinLines = 2,
                    MaxLines = 6
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildNpcTradeAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var npcId = ReadCommandArguments(command);
        if (string.IsNullOrWhiteSpace(npcId))
        {
            return Result(
                command,
                localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
                [
                    localTurn.Panel,
                    Message(
                        UiNotificationSeverity.Info,
                        "Торговля с НПС",
                        "Укажите торговца командой /npc_trade <npc_id> или введите ID торговца в форме.")
                ],
                prompts:
                [
                    new UiTextInputPrompt
                    {
                        Id = "npc_id",
                        Prompt = "ID торговца",
                        Required = true,
                        Placeholder = "npcId"
                    },
                    new UiSelectionPrompt
                    {
                        Id = "npc_trade_choice",
                        Prompt = "Действие торговли",
                        Required = true,
                        Options =
                        [
                            Option("request:__selected__", "Запросить витрину", "Попросить ГМа подготовить ассортимент выбранного торговца.")
                        ]
                    },
                    TradeConfirmationPrompt()
                ]);
        }

        var service = new NpcTradeService(fs, NullLogger<NpcTradeService>.Instance);
        var currentTurn = Math.Max(1, stateManager.CurrentState.TurnNumber);
        var view = await service.EnsureTradeInventoryAsync(npcId, currentTurn, createPendingRequests: false);
        if (view == null)
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Warning, "Торговец не найден", "Такого торговца сейчас нет среди известных персонажей.")
                ]);
        }

        var sellOffers = await service.GetSellableItemsAsync(view.NpcId);
        var options = new List<UiSelectionOption>();
        if (!view.TradeBlocked && !view.InventoryReady && !view.InventoryRequestPending)
            options.Add(Option($"request:{view.NpcId}", "Запросить витрину", "Попросить ГМа подготовить ассортимент текущего цикла."));
        options.AddRange(view.Offers.Select(offer => new UiSelectionOption
        {
            Value = $"buy:{offer.SlotId}",
            Label = $"Купить: {offer.Name}",
            Description = $"{offer.Price} монет; редкость: {FormatRarityForPlayer(offer.Rarity)}",
            Disabled = offer.SoldOut || view.CurrentMoney < offer.Price
        }));
        options.AddRange(sellOffers.Select(offer => Option(
            $"sell:{offer.ItemId}",
            $"Продать: {offer.Name}",
            $"+{offer.Price} монет; редкость: {FormatRarityForPlayer(offer.Rarity)}")));
        options.AddRange(view.BuybackOffers.Select(offer => new UiSelectionOption
        {
            Value = $"buyback:{offer.BuybackEntryId}",
            Label = $"Выкупить: {offer.Name}",
            Description = $"{offer.Price} монет; ранее продано за {offer.SoldForPrice}",
            Disabled = view.CurrentMoney < offer.Price
        }));

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Торговля с НПС",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = BuildTradeStatusText(view.TradeBlocked, view.BlockReason, view.InventoryReady, view.InventoryRequestPending, view.InventoryStatusMessage, "Витрина торговца готова."),
                        Tone = view.TradeBlocked ? UiTone.Warning : view.InventoryReady ? UiTone.Accent : UiTone.Muted
                    },
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            KeyValue("Торговец", view.NpcName),
                            KeyValue("Профиль", view.MerchantProfileDisplay),
                            KeyValue("Деньги", view.CurrentMoney.ToString()),
                            KeyValue("Витрина", view.InventoryReady ? "готова" : view.InventoryRequestPending ? "ожидает ГМ" : "нужно запросить")
                        ]
                    },
                    new UiTableBlock
                    {
                        Title = "Покупка",
                        Columns = ["Товар", "Редкость", "Цена", "Статус"],
                        Rows = view.Offers
                            .Select(offer => Row(
                                offer.Name,
                                FormatRarityForPlayer(offer.Rarity, "-"),
                                offer.Price.ToString(),
                                offer.SoldOut ? "куплено" : view.CurrentMoney < offer.Price ? "не хватает денег" : "доступно"))
                            .ToList()
                    },
                    new UiTableBlock
                    {
                        Title = "Продажа из рюкзака",
                        Columns = ["Предмет", "Редкость", "Цена"],
                        Rows = sellOffers
                            .Select(offer => Row(offer.Name, FormatRarityForPlayer(offer.Rarity, "-"), offer.Price.ToString()))
                            .ToList()
                    },
                    new UiTableBlock
                    {
                        Title = "Обратный выкуп",
                        Columns = ["Предмет", "Редкость", "Цена"],
                        Rows = view.BuybackOffers
                            .Select(offer => Row(offer.Name, FormatRarityForPlayer(offer.Rarity, "-"), offer.Price.ToString()))
                            .ToList()
                    }
                ]
            }
        };

        if (options.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "npc_trade_choice",
                    Prompt = "Сделка",
                    Required = true,
                    Options = options
                },
                TradeConfirmationPrompt()
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildShiningTradeAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var commandArguments = ReadCommandArguments(command);
        var factionId = commandArguments;
        var detailArguments = string.Empty;
        if (TrySplitLeadingArgument(commandArguments, out var parsedFactionId, out var parsedDetailArguments))
        {
            factionId = parsedFactionId;
            detailArguments = parsedDetailArguments;
        }

        if (string.IsNullOrWhiteSpace(factionId))
        {
            return Result(
                command,
                localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Info, "Сияющая торговля", "Укажите фракцию командой /shining_trade <faction_id> или введите ID фракции в форме.")
                ],
                prompts:
                [
                    new UiTextInputPrompt
                    {
                        Id = "faction_id",
                        Prompt = "ID сияющей фракции",
                        Required = true,
                        Placeholder = "factionId"
                    },
                    new UiSelectionPrompt
                    {
                        Id = "shining_trade_choice",
                        Prompt = "Действие торговли",
                        Required = true,
                        Options =
                        [
                            Option("request:__selected__", "Запросить витрину", "Попросить ГМа подготовить ассортимент выбранной сияющей фракции.")
                        ]
                    },
                    TradeConfirmationPrompt()
                ]);
        }

        var view = await ShiningTradeService.ReadTradeViewAsync(fs, factionId);
        if (view == null)
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Warning, "Фракция не найдена", "Такой сияющей фракции сейчас нет в Обители.")
                ]);
        }

        if (TryReadDetailSelector(detailArguments, out var offerSelector, "товар", "item", "slot", "offer", "реликвия", "relic"))
            return BuildShiningTradeOfferDetail(command, localTurn.Panel, view, offerSelector);

        var feathers = GetSoulInkFeathers((await ReadJson(fs, SoulStatePath)).Node as JsonObject, stateManager.CurrentState.InkFeathers);
        var options = new List<UiSelectionOption>();
        if (!view.TradeBlocked && !view.InventoryReady && !view.InventoryRequestPending)
            options.Add(Option($"request:{view.FactionId}", "Запросить витрину", "Попросить ГМа подготовить ассортимент сияющей фракции."));
        options.AddRange(view.Offers.Select(offer => new UiSelectionOption
        {
            Value = $"buy:{offer.SlotId}",
            Label = $"Купить: {offer.Name}",
            Description = $"{offer.PriceInFeathers} Чернильных Перьев; редкость: {FormatRarityForPlayer(offer.Rarity)}",
            Disabled = offer.SoldOut || feathers < offer.PriceInFeathers
        }));

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Сияющая торговля",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = BuildTradeStatusText(view.TradeBlocked, view.BlockReason, view.InventoryReady, view.InventoryRequestPending, view.InventoryStatusMessage, "Витрина сияющей фракции готова."),
                        Tone = view.TradeBlocked ? UiTone.Warning : view.InventoryReady ? UiTone.Accent : UiTone.Muted
                    },
                    new UiTextBlock
                    {
                        Text = "Продажа сияющим фракциям пока не поддержана текущими правилами торговли; браузер не создаёт отдельную механику продажи.",
                        Tone = UiTone.Muted
                    },
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            KeyValue("Фракция", view.FactionName),
                            KeyValue("Чернильные Перья", feathers.ToString()),
                            KeyValue("Уровень торговли", view.TradeTier.ToString()),
                            KeyValue("Витрина", view.InventoryReady ? "готова" : view.InventoryRequestPending ? "ожидает ГМ" : "нужно запросить")
                        ]
                    },
                    new UiTableBlock
                    {
                        Title = "Покупка",
                        Columns = ["Реликвия", "Редкость", "Цена", "Статус"],
                        Rows = view.Offers
                            .Select(offer => Row(
                                offer.Name,
                                FormatRarityForPlayer(offer.Rarity, "-"),
                                offer.PriceInFeathers.ToString(),
                                offer.SoldOut ? "куплено" : feathers < offer.PriceInFeathers ? "не хватает перьев" : "доступно"))
                            .ToList()
                    }
                ]
            }
        };
        var actions = BuildShiningTradeOfferDetailActions(view).ToList();

        if (options.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks, actions: actions);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            actions: actions,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "shining_trade_choice",
                    Prompt = "Действие торговли",
                    Required = true,
                    Options = options
                },
                TradeConfirmationPrompt()
            ]);
    }

    private static ExplorerCommandResult BuildShiningTradeOfferDetail(
        string command,
        UiPanelBlock localTurnPanel,
        ShiningTradeService.ShiningTradeView view,
        string selector)
    {
        var offer = FindShiningTradeOffer(view, selector);
        if (offer == null)
            return DetailUnavailable(command, "Товар сияющей торговли недоступен");

        var relicId = GetString(offer.RelicData, "relicId");
        var propertySummaries = BuildShiningTradeRelicPropertySummaries(offer.RelicData).ToList();
        var detailBlocks = new List<UiBlock>
        {
            new UiKeyValueGridBlock
            {
                Items =
                [
                    KeyValue("Фракция", view.FactionName),
                    KeyValue("Слот", offer.SlotId),
                    KeyValue("Реликвия", FirstNonEmpty(relicId, offer.Name)),
                    KeyValue("Редкость", FormatRarityForPlayer(offer.Rarity, "-")),
                    KeyValue("Цена", $"{offer.PriceInFeathers} Чернильных Перьев"),
                    KeyValue("Статус", offer.SoldOut ? "уже куплено" : "доступно")
                ]
            },
            new UiTextBlock
            {
                Text = FirstNonEmpty(offer.Description, "Описание реликвии пока не записано.")
            }
        };

        if (propertySummaries.Count > 0)
        {
            detailBlocks.Add(new UiTextBlock
            {
                Text = "Свойства:\n- " + string.Join("\n- ", propertySummaries)
            });
        }

        return Result(
            command,
            CommandExecutionState.Completed,
            [
                localTurnPanel,
                new UiPanelBlock
                {
                    Title = $"Товар сияющей торговли: {offer.Name}",
                    Blocks = detailBlocks
                }
            ],
            actions:
            [
                new UiAction
                {
                    Id = SoulRelicEquipmentService.BuildActionId("shining-trade-back", view.FactionId),
                    Label = "← К сияющей торговле",
                    Command = "/shining_trade " + SoulRelicEquipmentService.FormatCommandArgument(view.FactionId),
                    Style = UiActionStyle.Secondary,
                    RequiresConfirmation = false
                }
            ]);
    }

    private static IEnumerable<UiAction> BuildShiningTradeOfferDetailActions(ShiningTradeService.ShiningTradeView view)
    {
        foreach (var offer in view.Offers)
        {
            if (string.IsNullOrWhiteSpace(offer.SlotId))
                continue;

            yield return DetailAction(
                "shining-trade-offer-detail",
                $"{view.FactionId}-{offer.SlotId}",
                offer.Name,
                "/shining_trade " +
                SoulRelicEquipmentService.FormatCommandArgument(view.FactionId) +
                " товар " +
                SoulRelicEquipmentService.FormatCommandArgument(offer.SlotId));
        }
    }

    private static ShiningTradeService.ShiningTradeOffer? FindShiningTradeOffer(
        ShiningTradeService.ShiningTradeView view,
        string selector)
    {
        return view.Offers.FirstOrDefault(offer =>
            string.Equals(offer.SlotId, selector, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(GetString(offer.RelicData, "relicId"), selector, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(offer.Name, selector, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> BuildShiningTradeRelicPropertySummaries(JsonObject relicData)
    {
        if (relicData["properties"] is not JsonArray properties)
            yield break;

        foreach (var property in properties.OfType<JsonObject>())
        {
            if (IsHiddenShiningTradeRelicProperty(property))
                continue;

            var name = FirstNonEmpty(
                GetString(property, "displayName"),
                GetString(property, "name"),
                GetString(property, "propertyName"),
                GetString(property, "propertyId"));
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var value = FirstNonEmpty(
                GetString(property, "summary"),
                GetString(property, "description"),
                GetString(property, "effectSummary"),
                GetString(property, "value"));
            yield return string.IsNullOrWhiteSpace(value) ? name : $"{name}: {value}";
        }
    }

    private static bool IsHiddenShiningTradeRelicProperty(JsonObject property)
    {
        if (property.TryGetPropertyValue("visibleToPlayer", out var visibleNode) &&
            visibleNode is JsonValue visibleValue &&
            visibleValue.TryGetValue<bool>(out var visibleToPlayer) &&
            !visibleToPlayer)
        {
            return true;
        }

        var visibility = GetString(property, "visibility");
        return visibility.Equals("hidden", StringComparison.OrdinalIgnoreCase) ||
               visibility.Equals("gm_only", StringComparison.OrdinalIgnoreCase) ||
               visibility.Equals("secret", StringComparison.OrdinalIgnoreCase) ||
               TryGetBool(property, "hidden") ||
               TryGetBool(property, "gmOnly") ||
               TryGetBool(property, "debugOnly");
    }

    private static async Task<ExplorerCommandResult> BuildGuardianTradeAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var guardianId = ReadCommandArguments(command);
        if (string.IsNullOrWhiteSpace(guardianId))
        {
            return Result(
                command,
                localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Info, "Торговля хранителя", "Укажите Хранителя командой /guardian_trade <guardian_id> или введите ID Хранителя в форме.")
                ],
                prompts:
                [
                    new UiTextInputPrompt
                    {
                        Id = "guardian_id",
                        Prompt = "ID хранителя",
                        Required = true,
                        Placeholder = "guardianId"
                    },
                    new UiSelectionPrompt
                    {
                        Id = "guardian_trade_choice",
                        Prompt = "Действие торговли",
                        Required = true,
                        Options =
                        [
                            Option("request:__selected__", "Запросить витрину", "Попросить ГМа подготовить ассортимент выбранного Хранителя.")
                        ]
                    },
                    TradeConfirmationPrompt()
                ]);
        }

        var service = new GuardianTradeService(fs, NullLogger<GuardianTradeService>.Instance);
        var currentTurn = Math.Max(1, stateManager.CurrentState.TurnNumber);
        var currentIncarnation = Math.Max(1, stateManager.CurrentState.Incarnation);
        var view = await service.EnsureTradeInventoryAsync(guardianId, currentIncarnation, currentTurn, createPendingRequests: false);
        if (view == null)
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Warning, "Хранитель не найден", "Такого Хранителя сейчас нет в доступной Обители.")
                ]);
        }

        var sellOffers = await service.GetSellableRelicsAsync(view.GuardianId);
        var soul = (await ReadJson(fs, SoulStatePath)).Node as JsonObject;
        var feathers = GetSoulInkFeathers(soul, stateManager.CurrentState.InkFeathers);
        var options = new List<UiSelectionOption>();
        if (!view.TradeBlocked && !view.InventoryReady && !view.InventoryRequestPending)
            options.Add(Option($"request:{view.GuardianId}", "Запросить витрину", "Попросить ГМа подготовить ассортимент Хранителя."));
        options.AddRange(view.Offers.Select(offer => new UiSelectionOption
        {
            Value = $"buy:{offer.SlotId}",
            Label = $"Купить: {offer.Name}",
            Description = $"{offer.PriceInFeathers} Чернильных Перьев; редкость: {FormatRarityForPlayer(offer.Rarity)}",
            Disabled = offer.SoldOut || feathers < offer.PriceInFeathers
        }));
        options.AddRange(sellOffers.Select(offer => Option(
            $"sell:{offer.RelicId}",
            $"Продать: {offer.Name}",
            $"+{offer.PriceInFeathers} Чернильных Перьев; редкость: {FormatRarityForPlayer(offer.Rarity)}")));
        options.AddRange(view.BuybackOffers.Select(offer => new UiSelectionOption
        {
            Value = $"buyback:{offer.BuybackEntryId}",
            Label = $"Выкупить: {offer.Name}",
            Description = $"{offer.PriceInFeathers} Чернильных Перьев; ранее продано за {offer.SoldForPrice}",
            Disabled = feathers < offer.PriceInFeathers
        }));

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Торговля хранителя",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = BuildTradeStatusText(view.TradeBlocked, view.BlockReason, view.InventoryReady, view.InventoryRequestPending, view.InventoryStatusMessage, "Витрина Хранителя готова."),
                        Tone = view.TradeBlocked ? UiTone.Warning : view.InventoryReady ? UiTone.Accent : UiTone.Muted
                    },
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            KeyValue("Хранитель", view.GuardianName),
                            KeyValue("Домен", view.DomainDisplay),
                            KeyValue("Чернильные Перья", feathers.ToString()),
                            KeyValue("Витрина", view.InventoryReady ? "готова" : view.InventoryRequestPending ? "ожидает ГМ" : "нужно запросить")
                        ]
                    },
                    new UiTableBlock
                    {
                        Title = "Покупка",
                        Columns = ["Реликвия", "Редкость", "Цена", "Статус"],
                        Rows = view.Offers
                            .Select(offer => Row(
                                offer.Name,
                                FormatRarityForPlayer(offer.Rarity, "-"),
                                offer.PriceInFeathers.ToString(),
                                offer.SoldOut ? "куплено" : feathers < offer.PriceInFeathers ? "не хватает перьев" : "доступно"))
                            .ToList()
                    },
                    new UiTableBlock
                    {
                        Title = "Продажа реликвий",
                        Columns = ["Реликвия", "Редкость", "Цена"],
                        Rows = sellOffers
                            .Select(offer => Row(offer.Name, FormatRarityForPlayer(offer.Rarity, "-"), offer.PriceInFeathers.ToString()))
                            .ToList()
                    },
                    new UiTableBlock
                    {
                        Title = "Обратный выкуп",
                        Columns = ["Реликвия", "Редкость", "Цена"],
                        Rows = view.BuybackOffers
                            .Select(offer => Row(offer.Name, FormatRarityForPlayer(offer.Rarity, "-"), offer.PriceInFeathers.ToString()))
                            .ToList()
                    }
                ]
            }
        };

        if (options.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "guardian_trade_choice",
                    Prompt = "Действие торговли",
                    Required = true,
                    Options = options
                },
                TradeConfirmationPrompt()
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildGuardianSocialAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var soulRoot = (await ReadJson(fs, SoulStatePath)).Node as JsonObject;
        var currentRealm = FirstNonEmpty(GetString(soulRoot, "currentRealm"), stateManager.CurrentState.CurrentRealm);
        if (!RealmSemantics.IsAfterlifeRealm(currentRealm))
        {
            return Result(
                command,
                CommandExecutionState.Blocked,
                [
                    localTurn.Panel,
                    Message(
                        UiNotificationSeverity.Warning,
                        "Общение с Хранителем недоступно",
                        "Разговор или просьбу о знаниях можно отправить только в посмертии. Сейчас действие недоступно для текущего царства.")
                ]);
        }

        var requestedGuardian = ReadCommandArguments(command);
        var guardiansRead = await ReadJson(fs, "game_state/meta/guardians.json");
        if (!string.IsNullOrWhiteSpace(guardiansRead.Error))
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(
                        UiNotificationSeverity.Warning,
                        "Общение с Хранителем",
                        "Список Хранителей временно недоступен. Повторите действие после проверки состояния.")
                ]);
        }

        var guardians = CollectGuardianSocialOptions(guardiansRead.Node).ToList();
        if (guardians.Count == 0)
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(
                        UiNotificationSeverity.Info,
                        "Общение с Хранителем",
                        "Сейчас нет известных Хранителей, которым можно отправить отдельный разговор или просьбу о знаниях.")
                ]);
        }

        var selected = ResolveGuardianSocialOption(guardians, requestedGuardian);
        var orderedGuardians = guardians
            .OrderByDescending(guardian => selected != null && string.Equals(guardian.GuardianId, selected.GuardianId, StringComparison.OrdinalIgnoreCase))
            .ThenBy(guardian => guardian.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var statusText = selected == null
            ? string.IsNullOrWhiteSpace(requestedGuardian)
                ? "Выберите Хранителя и тип обращения. ГМ разыграет сцену или ответ о знаниях в следующем принятом ходе."
                : "Указанный Хранитель не найден среди известных. Выберите Хранителя из списка или уточните ввод."
            : $"Выбран Хранитель: {selected.Name}. Можно выбрать другого Хранителя из списка.";

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Общение с Хранителем",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = statusText,
                        Tone = selected == null && !string.IsNullOrWhiteSpace(requestedGuardian) ? UiTone.Warning : UiTone.Accent
                    },
                    new UiTableBlock
                    {
                        Title = "Известные Хранители",
                        Columns = ["Хранитель", "Домен", "Обитель"],
                        Rows = orderedGuardians
                            .Take(20)
                            .Select(static guardian => Row(
                                guardian.Name,
                                FirstNonEmpty(guardian.Domain, "-"),
                                FirstNonEmpty(guardian.AbodeName, "-")))
                            .ToList()
                    }
                ]
            }
        };

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "guardian_id",
                    Prompt = "С каким Хранителем обратиться",
                    Required = selected == null,
                    AllowCustom = true,
                    Options = orderedGuardians
                        .Select(static guardian => Option(
                            guardian.GuardianId,
                            guardian.Name,
                            FirstNonEmpty(guardian.Domain, guardian.AbodeName, "Известный Хранитель")))
                        .ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "guardian_interaction_type",
                    Prompt = "Тип обращения",
                    Required = true,
                    Options =
                    [
                        Option(
                            ActorSocialInteractionRequestState.GuardianInteractionTypeTalk,
                            "Поговорить",
                            "Обычный разговор с Хранителем."),
                        Option(
                            ActorSocialInteractionRequestState.GuardianInteractionTypeLore,
                            "Попросить знания",
                            "Попросить Хранителя раскрыть лор или знание.")
                    ]
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildAbodeResidentsAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        if (!await IsAfterlifeRealmAsync(fs, stateManager))
            return ResidentRealmBlocker(command, localTurn.Panel, "Обитатели Обители недоступны");

        var guardiansRead = await ReadJson(fs, "game_state/meta/guardians.json");
        if (!string.IsNullOrWhiteSpace(guardiansRead.Error))
            return ResidentStateUnavailable(command, localTurn.Panel, "Обитатели Обители", "Список Хранителей временно недоступен. Повторите действие после проверки состояния.");

        var requestedGuardian = ReadCommandArguments(command);
        var abodes = CollectGuardianAbodeOptions(guardiansRead.Node).ToList();
        if (abodes.Count == 0)
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(
                        UiNotificationSeverity.Info,
                        "Обитатели Обители",
                        "Сейчас нет известных Обителей Хранителей, для которых можно запросить состав.")
                ]);
        }

        var selected = ResolveGuardianAbodeOption(abodes, requestedGuardian);
        var orderedAbodes = OrderWithSelection(abodes, selected, static abode => abode.GuardianName).ToList();
        var statusText = selected == null
            ? string.IsNullOrWhiteSpace(requestedGuardian)
                ? "Выберите Обитель. ГМ подготовит или обновит состав её обитателей в следующем принятом ходе."
                : "Указанный Хранитель или Обитель не найдены среди известных. Выберите вариант из списка."
            : $"Выбрана Обитель: {selected.AbodeName} Хранителя {selected.GuardianName}. Можно выбрать другую Обитель из списка.";

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            [
                localTurn.Panel,
                new UiPanelBlock
                {
                    Title = "Обитатели Обители",
                    Blocks =
                    [
                        new UiTextBlock
                        {
                            Text = statusText,
                            Tone = selected == null && !string.IsNullOrWhiteSpace(requestedGuardian) ? UiTone.Warning : UiTone.Accent
                        },
                        new UiTableBlock
                        {
                            Title = "Известные Обители",
                            Columns = ["Хранитель", "Обитель", "Репутация"],
                            Rows = orderedAbodes
                                .Take(20)
                                .Select(static abode => Row(
                                    abode.GuardianName,
                                    abode.AbodeName,
                                    abode.CurrentReputation.ToString()))
                                .ToList()
                        }
                    ]
                }
            ],
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "guardian_abode_id",
                    Prompt = "Для какой Обители запросить состав",
                    Required = selected == null,
                    Options = orderedAbodes
                        .Select(static abode => Option(
                            abode.CompositeId,
                            $"{abode.GuardianName} — {abode.AbodeName}",
                            $"Репутация {abode.CurrentReputation}; ГМ подготовит состав обитателей."))
                        .ToList()
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildResidentInteractionAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        if (!await IsAfterlifeRealmAsync(fs, stateManager))
            return ResidentRealmBlocker(command, localTurn.Panel, "Общение с обитателем недоступно");

        var context = await ReadResidentBrowserContextAsync(fs);
        if (!string.IsNullOrWhiteSpace(context.ErrorMessage))
            return ResidentStateUnavailable(command, localTurn.Panel, "Общение с обитателем", context.ErrorMessage);

        var residents = context.Residents.Where(static resident => resident.IsPresent).ToList();
        if (residents.Count == 0)
            return ResidentStateUnavailable(command, localTurn.Panel, "Общение с обитателем", "Сейчас нет материализованных обитателей Обители. Сначала запросите состав Обители у ГМ.");

        var requestedResident = ReadCommandArguments(command);
        var selected = ResolveResidentOption(residents, requestedResident);
        var orderedResidents = OrderWithSelection(residents, selected, static resident => resident.DisplayName).ToList();
        var statusText = selected == null
            ? string.IsNullOrWhiteSpace(requestedResident)
                ? "Выберите обитателя и тип обращения. ГМ разыграет разговор или раскрытие истории в следующем принятом ходе."
                : "Указанный обитатель не найден среди текущего состава. Выберите вариант из списка."
            : $"Выбран обитатель: {selected.DisplayName}. Можно выбрать другой вариант из списка.";

        var interactionOptions = selected == null
            ? BuildResidentInteractionOptions(orderedResidents)
            : BuildResidentInteractionOptions(selected);
        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            [
                localTurn.Panel,
                new UiPanelBlock
                {
                    Title = "Общение с обитателем Обители",
                    Blocks =
                    [
                        new UiTextBlock
                        {
                            Text = statusText,
                            Tone = selected == null && !string.IsNullOrWhiteSpace(requestedResident) ? UiTone.Warning : UiTone.Accent
                        },
                        new UiTableBlock
                        {
                            Title = "Обитатели",
                            Columns = ["Имя", "Обитель", "Состояние"],
                            Rows = orderedResidents
                                .Take(20)
                                .Select(static resident => Row(
                                    resident.DisplayName,
                                    resident.AbodeName,
                                    GuardianAbodeResidentState.GetMigrationStateLabel(resident.MigrationState)))
                                .ToList()
                        }
                    ]
                }
            ],
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "resident_id",
                    Prompt = "С каким обитателем обратиться",
                    Required = selected == null,
                    Options = orderedResidents
                        .Select(static resident => Option(
                            resident.ResidentId,
                            resident.DisplayName,
                            $"{resident.AbodeName}; {GuardianAbodeResidentState.GetResidentKindLabel(resident.ResidentKind)}."))
                        .ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "resident_interaction_type",
                    Prompt = "Тип обращения",
                    Required = true,
                    Options = interactionOptions.ToList()
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildResidentTransferAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        if (!await IsAfterlifeRealmAsync(fs, stateManager))
            return ResidentRealmBlocker(command, localTurn.Panel, "Переход обитателя недоступен");

        var context = await ReadResidentBrowserContextAsync(fs);
        if (!string.IsNullOrWhiteSpace(context.ErrorMessage))
            return ResidentStateUnavailable(command, localTurn.Panel, "Переход обитателя", context.ErrorMessage);

        var residents = context.Residents.Where(static resident => resident.IsPresent).ToList();
        if (residents.Count == 0)
            return ResidentStateUnavailable(command, localTurn.Panel, "Переход обитателя", "Сейчас нет материализованных обитателей Обители. Сначала запросите состав Обители у ГМ.");

        var requestedResident = ReadCommandArguments(command);
        var selected = ResolveResidentOption(residents, requestedResident);
        var orderedResidents = OrderWithSelection(residents, selected, static resident => resident.DisplayName).ToList();
        var transferResident = selected ?? orderedResidents.First();
        var transferChoices = selected == null
            ? BuildResidentTransferOptions(orderedResidents, context.GuardiansRoot, context.ResidentsRoot)
            : BuildResidentTransferOptions(transferResident, context.GuardiansRoot, context.ResidentsRoot);
        var statusText = selected == null
            ? string.IsNullOrWhiteSpace(requestedResident)
                ? "Выберите готового к переходу обитателя и направление. ГМ подтвердит исход в следующем принятом ходе."
                : "Указанный обитатель не найден среди текущего состава. Выберите вариант из списка."
            : $"Выбран обитатель: {selected.DisplayName}. Можно выбрать другого обитателя из списка.";

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            [
                localTurn.Panel,
                new UiPanelBlock
                {
                    Title = "Переход обитателя Обители",
                    Blocks =
                    [
                        new UiTextBlock
                        {
                            Text = statusText,
                            Tone = selected == null && !string.IsNullOrWhiteSpace(requestedResident) ? UiTone.Warning : UiTone.Accent
                        },
                        new UiTableBlock
                        {
                            Title = "Обитатели",
                            Columns = ["Имя", "Обитель", "Готовность"],
                            Rows = orderedResidents
                                .Take(20)
                                .Select(static resident => Row(
                                    resident.DisplayName,
                                    resident.AbodeName,
                                    GuardianAbodeResidentState.GetMigrationStateLabel(resident.MigrationState)))
                                .ToList()
                        }
                    ]
                }
            ],
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "resident_id",
                    Prompt = "Какой обитатель просит переход",
                    Required = selected == null,
                    Options = orderedResidents
                        .Select(static resident => Option(
                            resident.ResidentId,
                            resident.DisplayName,
                            $"{resident.AbodeName}; {GuardianAbodeResidentState.GetMigrationStateLabel(resident.MigrationState)}."))
                        .ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "resident_transfer_choice",
                    Prompt = "Куда направить переход",
                    Required = true,
                    Options = transferChoices.ToList()
                }
            ]);
    }

    private static UiConfirmationPrompt TradeConfirmationPrompt() => new()
    {
        Id = "confirm_trade_write",
        Prompt = "Подтвердить сделку",
        Required = true,
        DefaultValue = false
    };

    private static string BuildTradeStatusText(
        bool tradeBlocked,
        string? blockReason,
        bool inventoryReady,
        bool inventoryRequestPending,
        string? inventoryStatusMessage,
        string readyMessage)
    {
        if (tradeBlocked)
            return SanitizeBrowserTradeStatusText(FirstNonEmpty(blockReason, "Торговля сейчас недоступна."));
        if (inventoryReady)
            return readyMessage;
        if (!string.IsNullOrWhiteSpace(inventoryStatusMessage))
            return SanitizeBrowserTradeStatusText(inventoryStatusMessage!);
        return inventoryRequestPending
            ? "Витрина уже запрошена и ждёт ответа ГМ."
            : "Витрина ещё не подготовлена; можно отправить запрос ассортимента.";
    }

    private static string SanitizeBrowserTradeStatusText(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Торговля сейчас недоступна.";

        return ContainsBrowserTradeDiagnosticFragment(message)
            ? "Торговля временно ждёт проверки ГМ. Завершите или обновите текущий торговый запрос, затем повторите действие."
            : message;
    }

    private static bool ContainsBrowserTradeDiagnosticFragment(string value) =>
        value.Contains(".json", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("pending_", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("canonical", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("contract", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("requestId=", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("slotId", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("repair", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("cleanup", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("raw", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("debug", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("game_state/", StringComparison.OrdinalIgnoreCase);

    private static async Task<ExplorerCommandResult> BuildSoulRelicEquipAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var context = await SoulRelicEquipmentService.ReadContextAsync(fs);
        var requestedRelic = SoulRelicEquipmentService.ReadFirstCommandArgument(command);
        var stored = context?.Stored.ToList() ?? [];
        if (!string.IsNullOrWhiteSpace(requestedRelic))
        {
            var matched = stored.FirstOrDefault(relic =>
                string.Equals(relic.RelicId, requestedRelic, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(relic.Name, requestedRelic, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
                stored = [matched];
        }

        var rows = stored
            .Select(static relic => Row(
                string.IsNullOrWhiteSpace(relic.Name) ? relic.RelicId : relic.Name,
                FormatRarityForPlayer(relic.Rarity, "-"),
                string.IsNullOrWhiteSpace(relic.RelicId) ? "-" : relic.RelicId))
            .ToList();
        var slotOptions = BuildSoulRelicEquipSlotOptions(stored);

        var statusText = stored.Count == 0
            ? "В хранилище нет реликвий, доступных для экипировки."
            : "Выберите реликвию и слот. Запись выполняется локально после проверки хода и блокировки интерфейса.";

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Экипировка реликвии души",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = statusText,
                        Tone = stored.Count == 0 ? UiTone.Muted : UiTone.Accent
                    },
                    new UiTableBlock
                    {
                        Title = "Доступные реликвии",
                        Columns = ["Название", "Редкость", "ID"],
                        Rows = rows
                    }
                ]
            }
        };

        if (stored.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            actions: BuildSoulRelicDetailActions(stored),
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "soul_relic_identity",
                    Prompt = "Реликвия",
                    Required = true,
                    Options = stored
                        .Select(static relic => Option(
                            string.IsNullOrWhiteSpace(relic.RelicId) ? relic.Name : relic.RelicId,
                            string.IsNullOrWhiteSpace(relic.Name) ? relic.RelicId : relic.Name,
                            "Редкость: " + FormatRarityForPlayer(relic.Rarity)))
                        .ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "soul_relic_slot",
                    Prompt = "Слот реликвии",
                    Required = true,
                    Options = slotOptions
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_soul_relic_write",
                    Prompt = "Подтвердить экипировку реликвии",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildSoulRelicUnequipAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var context = await SoulRelicEquipmentService.ReadContextAsync(fs);
        var equipped = context?.Equipped
            .Where(static relic => !string.IsNullOrWhiteSpace(relic.CurrentSlot))
            .ToList() ?? [];

        var rows = equipped
            .Select(static relic => Row(
                string.IsNullOrWhiteSpace(relic.Name) ? relic.RelicId : relic.Name,
                SoulRelicEquipmentService.SlotLabels.TryGetValue(relic.CurrentSlot, out var slotLabel)
                    ? slotLabel
                    : (string.IsNullOrWhiteSpace(relic.CurrentSlot) ? "-" : relic.CurrentSlot)))
            .ToList();

        var statusText = equipped.Count == 0
            ? "Сейчас нет экипированных реликвий."
            : "Выберите слот и подтвердите снятие реликвии в хранилище.";

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Снятие реликвии души",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = statusText,
                        Tone = equipped.Count == 0 ? UiTone.Muted : UiTone.Accent
                    },
                    new UiTableBlock
                    {
                        Title = "Экипировано",
                        Columns = ["Реликвия", "Слот"],
                        Rows = rows
                    }
                ]
            }
        };

        if (equipped.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            actions: BuildSoulRelicDetailActions(equipped),
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "soul_relic_slot",
                    Prompt = "Какой слот освободить",
                    Required = true,
                    Options = equipped
                        .Select(relic => Option(
                            relic.CurrentSlot,
                            $"{SoulRelicEquipmentService.SlotLabels.GetValueOrDefault(relic.CurrentSlot, relic.CurrentSlot)}: {(string.IsNullOrWhiteSpace(relic.Name) ? relic.RelicId : relic.Name)}",
                            ""))
                        .ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_soul_relic_write",
                    Prompt = "Подтвердить снятие реликвии",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static List<UiSelectionOption> BuildSoulRelicEquipSlotOptions(IReadOnlyList<SoulRelicItem> stored)
    {
        var slots = new List<string>();
        foreach (var relic in stored)
        {
            foreach (var compatibleSlot in relic.CompatibleSlots)
            {
                if (!string.IsNullOrWhiteSpace(compatibleSlot) &&
                    !slots.Contains(compatibleSlot, StringComparer.OrdinalIgnoreCase))
                {
                    slots.Add(compatibleSlot);
                }
            }
        }

        if (slots.Count == 0 || stored.Count != 1)
        {
            foreach (var slot in SoulRelicEquipmentService.SlotLabels.Keys)
            {
                if (!slots.Contains(slot, StringComparer.OrdinalIgnoreCase))
                    slots.Add(slot);
            }
        }

        return slots
            .Select(static slot => Option(slot, SoulRelicEquipmentService.FormatSlotLabel(slot), ""))
            .ToList();
    }

    private static List<UiAction> BuildSoulRelicDetailActions(IEnumerable<SoulRelicItem> relics)
    {
        var actions = new List<UiAction>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relic in relics)
        {
            var identity = FirstNonEmpty(relic.RelicId, relic.Name);
            if (string.IsNullOrWhiteSpace(identity))
                continue;

            var action = DetailAction(
                "soul-relic-detail",
                identity,
                FirstNonEmpty(relic.Name, relic.RelicId, identity),
                "/soul_relics реликвия " + SoulRelicEquipmentService.FormatCommandArgument(identity));
            if (ids.Add(action.Id))
                actions.Add(action);
        }

        return actions;
    }

    private static async Task<ExplorerCommandResult> BuildCraftAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs);
        var recipes = await ReadJson(fs, "game_state/inventory/recipes.json");
        var recipeCards = CollectObjects(recipes.Node)
            .Where(static item => !string.IsNullOrWhiteSpace(FirstNonEmpty(GetString(item, "recipeName"), GetString(item, "name"))))
            .Select(BuildCraftRecipeCard)
            .ToList();

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiEntityDossierBlock
            {
                EntityType = "craft-requests",
                Title = "Ремесло",
                Summary = "Выберите известный рецепт или опишите новое ремесленное действие. ГМ разыграет результат в следующем принятом ходе.",
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "craft-known-recipes",
                        Title = "Известные рецепты",
                        Icon = "hammer",
                        Presentation = "cards",
                        CollectionLabel = recipeCards.Count == 1 ? "1 рецепт" : $"{recipeCards.Count} рецептов",
                        Cards = recipeCards
                    }
                ]
            }
        };

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiTextInputPrompt { Id = "recipe_id", Prompt = "Рецепт или название", Placeholder = "Например: Лечебная мазь" },
                new UiLongTextInputPrompt
                {
                    Id = "craft_intent",
                    Prompt = "Описание ремесленного действия",
                    Placeholder = "Что именно создать, какие материалы использовать, какие ограничения соблюсти.",
                    MinLines = 2,
                    MaxLines = 6
                }
            ]);
    }

    private static UiEntityDossierBlock BuildInventoryActionDossier(
        string entityType,
        string title,
        string summary,
        string sectionId,
        string sectionTitle,
        string collectionLabel,
        List<UiEntityCard> cards,
        UiTone summaryTone) =>
        new()
        {
            EntityType = entityType,
            Title = title,
            Summary = summary,
            Hints =
            [
                new UiEntityHint
                {
                    Title = "Что произойдёт",
                    Text = summary,
                    Tone = summaryTone
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = sectionId,
                    Title = sectionTitle,
                    Icon = "package",
                    Presentation = "cards",
                    CollectionLabel = collectionLabel,
                    Cards = cards
                }
            ]
        };

    private static UiEntityCard BuildInventoryEquipmentCandidateCard(InventoryEquipmentItem item)
    {
        var slot = string.IsNullOrWhiteSpace(item.ResolvedSlot)
            ? "нужно выбрать вручную"
            : InventoryEquipmentService.FormatSlotName(item.ResolvedSlot);
        var facts = new List<UiEntityFact>
        {
            new() { Label = "Тип", Value = FormatInventoryItemTypeForPlayer(item.Type) },
            new() { Label = "Слот", Value = slot },
            new() { Label = "Состояние", Value = item.IsBroken ? "повреждён" : "можно экипировать" }
        };

        return new UiEntityCard
        {
            Title = item.Name,
            Subtitle = "Предмет для экипировки",
            Summary = string.IsNullOrWhiteSpace(item.ResolvedSlot)
                ? "Предмет можно экипировать, но подходящий слот нужно выбрать вручную."
                : $"Предмет можно экипировать в слот: {slot}.",
            Icon = "package",
            Facts = facts
        };
    }

    private static UiEntityCard BuildEquippedInventoryCard(EquippedInventoryItem item) =>
        new()
        {
            Title = item.ItemName,
            Subtitle = item.SlotLabel,
            Summary = $"Предмет сейчас занимает слот: {item.SlotLabel}.",
            Icon = "package",
            Facts =
            [
                new UiEntityFact { Label = "Слот", Value = item.SlotLabel },
                new UiEntityFact { Label = "Действие", Value = "снять в рюкзак" }
            ]
        };

    private static UiEntityCard BuildInventoryManagementCard(
        InventoryManagementItem item,
        string subtitle = "Предмет",
        IReadOnlyList<UiEntityFact>? extraFacts = null)
    {
        var facts = new List<UiEntityFact>
        {
            new() { Label = "Количество", Value = FormatInventoryCount(item) },
            new() { Label = "Где находится", Value = FormatInventoryPlacement(item) }
        };
        AddInventoryFactIfKnown(facts, "Тип", FormatInventoryItemTypeForPlayer(GetString(item.Data, "type")));
        AddInventoryFactIfKnown(facts, "Качество", FormatRarityForPlayer(GetString(item.Data, "quality"), string.Empty));
        AddInventoryFactIfKnown(facts, "Прочность", FormatInventoryDurability(item.Data));
        if (extraFacts != null)
            facts.AddRange(extraFacts);

        return new UiEntityCard
        {
            Title = item.Name,
            Subtitle = subtitle,
            Summary = $"{FormatInventoryCount(item)}. {FormatInventoryPlacement(item)}.",
            Icon = "package",
            Facts = facts
        };
    }

    private static UiEntityCard BuildInventoryMergeCard(
        InventoryManagementContext inventory,
        InventoryManagementItem item)
    {
        var compatible = InventoryManagementService.FindCompatibleStacks(inventory, item);
        return BuildInventoryManagementCard(
            item,
            "Группа совместимых стопок",
            [
                new UiEntityFact { Label = "Совместимых стопок", Value = compatible.Count.ToString() },
                new UiEntityFact { Label = "После объединения", Value = FormatInventoryCount(compatible.Sum(static match => match.Count)) }
            ]);
    }

    private static UiEntityCard BuildCraftRecipeCard(JsonObject item)
    {
        var name = FirstNonEmpty(GetString(item, "recipeName"), GetString(item, "name"), "Рецепт");
        var result = FirstNonEmpty(GetString(item, "craftedItemName"), GetString(item, "resultItemName"), "результат не указан");
        var rank = FirstNonEmpty(GetString(item, "recipeRank"), GetString(item, "rank"), "ранг не указан");

        return new UiEntityCard
        {
            Title = name,
            Subtitle = result,
            Summary = $"Результат: {result}. Ранг: {rank}.",
            Icon = "hammer",
            Facts =
            [
                new UiEntityFact { Label = "Результат", Value = result },
                new UiEntityFact { Label = "Ранг", Value = rank }
            ]
        };
    }

    private static async Task<ExplorerCommandResult> BuildInkFeatherRevealFateAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var soul = await ReadJson(fs, SoulStatePath);
        if (soul.Node is not JsonObject soulRoot)
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Warning, "Судьба недоступна", "Состояние души сейчас недоступно. Откройте форму позже.")
                ]);
        }

        var arguments = ReadCommandArguments(command);
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            return Result(
                command,
                CommandExecutionState.Failed,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Error, "Некорректные аргументы", "Команда раскрытия судьбы не принимает аргументы. Откройте форму и подтвердите списание Перьев.")
                ]);
        }

        var currentRealm = FirstNonEmpty(GetString(soulRoot, "currentRealm"), stateManager.CurrentState.CurrentRealm);
        if (!RealmSemantics.IsMortalRealm(currentRealm))
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Warning, "Судьба недоступна", "Раскрытие судьбы доступно только во время смертной жизни.")
                ]);
        }

        var availableFeathers = Math.Max(0, GetSoulInkFeathers(soulRoot, stateManager.CurrentState.InkFeathers));
        var cost = ComputeRevealFateCost(availableFeathers);
        if (availableFeathers < cost)
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Warning, "Недостаточно Перьев", $"Для раскрытия судьбы нужно {cost} Чернильных Перьев, доступно {availableFeathers}.")
                ]);
        }

        var remaining = availableFeathers - cost;
        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Открыть Судьбу",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = $"Раскрытие покажет предстоящие кубики и базу судьбы. Стоимость: {cost} Чернильных Перьев; после списания останется {remaining}.",
                        Tone = UiTone.Accent
                    },
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            KeyValue("Доступно Перьев", availableFeathers.ToString()),
                            KeyValue("Стоимость", $"{cost} Чернильных Перьев"),
                            KeyValue("После списания", remaining.ToString())
                        ]
                    }
                ]
            }
        };

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiConfirmationPrompt
                {
                    Id = "confirm_ink_feather_fate_reveal",
                    Prompt = $"Потратить {cost} Чернильных Перьев и открыть судьбу?",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildInkFeatherRewriteFateAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var soul = await ReadJson(fs, SoulStatePath);
        if (soul.Node is not JsonObject soulRoot)
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Warning, "Переписывание недоступно", "Состояние души сейчас недоступно. Откройте форму позже.")
                ]);
        }

        var arguments = ReadCommandArguments(command);
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            return Result(
                command,
                CommandExecutionState.Failed,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Error, "Некорректные аргументы", "Команда переписывания судьбы не принимает аргументы. Откройте форму и подтвердите списание Перьев.")
                ]);
        }

        var currentRealm = FirstNonEmpty(GetString(soulRoot, "currentRealm"), stateManager.CurrentState.CurrentRealm);
        if (!RealmSemantics.IsMortalRealm(currentRealm))
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Warning, "Переписывание недоступно", "Переписывание судьбы доступно только во время смертной жизни.")
                ]);
        }

        var pendingService = new PendingTurnStateService(fs, NullLogger<PendingTurnStateService>.Instance);
        var pending = await pendingService.TryReadExistingAsync();
        if (pending == null || !pending.IsFateLocked)
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Warning, "Нет открытой судьбы", "Сначала нужно открыть судьбу. Переписывание доступно только для уже раскрытых кубиков и Гача-базы.")
                ]);
        }

        var availableFeathers = Math.Max(0, GetSoulInkFeathers(soulRoot, stateManager.CurrentState.InkFeathers));
        var cost = ComputeRewriteFateCost(availableFeathers);
        if (availableFeathers < cost)
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Warning, "Недостаточно Перьев", $"Для переписывания судьбы нужно {cost} Чернильных Перьев, доступно {availableFeathers}.")
                ]);
        }

        var remaining = availableFeathers - cost;
        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Переписать Судьбу",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = $"Переписывание заменит текущие кости судьбы и Гача-базу новым открытым набором. Стоимость: {cost} Чернильных Перьев; после списания останется {remaining}.",
                        Tone = UiTone.Warning
                    },
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            KeyValue("Текущие кубики", FormatDiceUsed(pending.PreGeneratedDices1d20)),
                            KeyValue("Гача-база", FormatGachaBaseSummary(pending.GachaBaseResult)),
                            KeyValue("Стоимость", $"{cost} Чернильных Перьев"),
                            KeyValue("После списания", remaining.ToString())
                        ]
                    }
                ]
            }
        };

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiConfirmationPrompt
                {
                    Id = "confirm_ink_feather_fate_rewrite",
                    Prompt = $"Потратить {cost} Чернильных Перьев и переписать открытую судьбу?",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildGachaAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var soul = await ReadJson(fs, SoulStatePath);
        var soulRoot = soul.Node as JsonObject;
        var arguments = ReadCommandArguments(command);
        var currentRealm = FirstNonEmpty(GetString(soulRoot, "currentRealm"), stateManager.CurrentState.CurrentRealm);
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            return Result(
                command,
                CommandExecutionState.Failed,
                [
                    localTurn.Panel,
                    Message(
                        UiNotificationSeverity.Error,
                        "Некорректные аргументы",
                        "Команда /gacha не принимает аргументы. Выберите поддерживаемый прямой призыв Моря Хаоса через браузерную форму.")
                ]);
        }

        if (!RealmSemantics.IsChaosSea(currentRealm))
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(
                        UiNotificationSeverity.Warning,
                        "Призыв недоступен",
                        $"Прямой призыв Моря Хаоса доступен только в обычном Море Хаоса. Сейчас душа находится здесь: {FormatRealmForPlayer(currentRealm)}.")
                ]);
        }

        var availableFeathers = Math.Max(0, GetSoulInkFeathers(soulRoot, stateManager.CurrentState.InkFeathers));
        var pendingTurnState = new PendingTurnStateService(fs, NullLogger<PendingTurnStateService>.Instance);
        var pendingState = await pendingTurnState.GetOrCreateAsync();
        var gachaBase = pendingState.GachaBaseResult;
        var baseRarity = FormatRarityForPlayer(FirstNonEmpty(gachaBase?.BaseRarity, "Common"));
        var baseScore = (gachaBase?.BaseScore ?? 0).ToString();
        var formula = FirstNonEmpty(gachaBase?.Formula, "client-computed gacha base (range 4-80)");
        var diceUsed = FormatDiceUsed(gachaBase?.DiceUsed);

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Призыв судьбы",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = availableFeathers > 0
                            ? "Доступен прямой нейтральный призыв из Моря Хаоса. Хранители, репутация, заряды и скидки в этом баннере не участвуют."
                            : "Чернильных Перьев сейчас нет. Прямой призыв из Моря Хаоса недоступен.",
                        Tone = availableFeathers > 0 ? UiTone.Accent : UiTone.Warning
                    },
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            KeyValue("Чернильные Перья", availableFeathers.ToString()),
                            KeyValue("Поддерживаемые баннеры", "Прямой призыв Моря Хаоса"),
                            KeyValue("Результат", "ГМ материализует ровно одну Реликвию Души после обычного хода")
                        ]
                    },
                    new UiTableBlock
                    {
                        Title = "Доступные баннеры",
                        Columns = ["Баннер", "Стоимость", "Шансы", "Разрешение"],
                        Rows =
                        [
                            Row(
                                "Прямой призыв Моря Хаоса",
                                availableFeathers > 0 ? $"1-{availableFeathers} Чернильных Перьев" : "нет доступных Перьев",
                                "Пороги: 4-48 обычная, 49-67 необычная, 68-75 редкая, 76-79 эпическая, 80 легендарная",
                                "Итоговая редкость точно равна базовой; без модификаторов Хранителей")
                        ]
                    },
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            KeyValue("Кубики базового результата", diceUsed),
                            KeyValue("Базовый счёт", baseScore),
                            KeyValue("Базовая редкость", baseRarity),
                            KeyValue("Формула", formula)
                        ]
                    }
                ]
            }
        };

        if (availableFeathers <= 0)
        {
            blocks.Add(Message(
                UiNotificationSeverity.Warning,
                "Призыв недоступен",
                "Нужно хотя бы 1 Чернильное Перо."));
            return Result(command, CommandExecutionState.Completed, blocks);
        }

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "gacha_banner",
                    Prompt = "Баннер призыва",
                    Required = true,
                    Options =
                    [
                        Option(
                            "direct_chaos_sea",
                            "Прямой призыв Моря Хаоса",
                            $"Нейтральный призыв без Хранителя. Стоимость: 1-{availableFeathers} Чернильных Перьев.")
                    ]
                },
                new UiTextInputPrompt
                {
                    Id = "feather_cost",
                    Prompt = "Сколько Чернильных Перьев потратить",
                    Required = true,
                    Placeholder = $"1-{availableFeathers}",
                    DefaultValue = "1"
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_gacha_pull",
                    Prompt = "Подтвердить списание Перьев и подготовить прямой призыв",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildArchiveConsultationAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var context = await BrowserAfterlifeArchiveActionContextReader.ReadConsultationAsync(fs, stateManager);
        if (TryReadDetailSelector(ReadCommandArguments(command), out var guardianSelector, "хранитель", "guardian", "деталь", "detail"))
            return BuildArchiveConsultationGuardianDetail(command, context, guardianSelector);

        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        if (context.IsBlocked)
            return ArchiveActionBlockedResult(command, localTurn.Panel, context);

        var blocks = BuildArchiveActionBlocks(
            localTurn.Panel,
            "Архивная консультация",
            "Выберите свободную запись Архива и дружественного Хранителя. После подтверждения запись будет зарезервирована до ответа ГМ.",
            context);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            actions: BuildArchiveConsultationDetailActions(context),
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "archive_id",
                    Prompt = "Запись Архива",
                    Required = true,
                    Options = context.Entries.Select(BuildArchiveEntryOption).ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "guardian_id",
                    Prompt = "Хранитель",
                    Required = true,
                    Options = context.Guardians
                        .Select(static guardian => Option(
                            guardian.GuardianId,
                            guardian.GuardianName,
                            $"Репутация: {guardian.Reputation}. Домен: {FirstNonEmpty(guardian.Domain, "не указан")}."))
                        .ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_archive_consultation",
                    Prompt = "Подтвердить архивную консультацию",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildArchiveProjectFuelAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var context = await BrowserAfterlifeArchiveActionContextReader.ReadProjectFuelAsync(fs, stateManager);
        if (TryReadDetailSelector(ReadCommandArguments(command), out var projectSelector, "проект", "project", "деталь", "detail"))
            return await BuildArchiveProjectFuelDetailAsync(command, fs, context, projectSelector);

        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        if (context.IsBlocked)
            return ArchiveActionUnavailableResult(command, localTurn.Panel, context);

        var blocks = BuildArchiveActionBlocks(
            localTurn.Panel,
            "Подпитка проекта Архивом",
            "Выберите свободную запись Архива и дружественного Хранителя с активным проектом. После подтверждения запись будет зарезервирована до ответа ГМ.",
            context);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            actions: BuildArchiveProjectFuelDetailActions(context),
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "archive_id",
                    Prompt = "Запись Архива",
                    Required = true,
                    Options = context.Entries.Select(BuildArchiveEntryOption).ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "guardian_id",
                    Prompt = "Хранитель и проект",
                    Required = true,
                    Options = context.Guardians
                        .Select(static guardian => Option(
                            guardian.GuardianId,
                            guardian.GuardianName,
                            $"Активный проект: {guardian.TargetProjectName}. Репутация: {guardian.Reputation}."))
                        .ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_archive_project_fuel",
                    Prompt = "Подтвердить подпитку проекта Архивом",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static ExplorerCommandResult ArchiveActionBlockedResult(
        string command,
        UiPanelBlock localTurnPanel,
        BrowserAfterlifeArchiveActionContext context) =>
        Result(
            command,
            CommandExecutionState.Blocked,
            [
                localTurnPanel,
                Message(UiNotificationSeverity.Warning, context.BlockerTitle, context.BlockerMessage)
            ]);

    private static ExplorerCommandResult ArchiveActionUnavailableResult(
        string command,
        UiPanelBlock localTurnPanel,
        BrowserAfterlifeArchiveActionContext context) =>
        Result(
            command,
            CommandExecutionState.Completed,
            [
                localTurnPanel,
                Message(UiNotificationSeverity.Warning, context.BlockerTitle, context.BlockerMessage)
            ]);

    private static List<UiBlock> BuildArchiveActionBlocks(
        UiPanelBlock localTurnPanel,
        string title,
        string statusText,
        BrowserAfterlifeArchiveActionContext context)
    {
        List<string> guardianColumns = context.Guardians.Any(static guardian => guardian.FuelAvailable)
            ? ["Хранитель", "Репутация", "Проект"]
            : ["Хранитель", "Репутация", "Домен"];

        return
        [
            localTurnPanel,
            new UiPanelBlock
            {
                Title = title,
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = statusText,
                        Tone = UiTone.Accent
                    },
                    new UiTableBlock
                    {
                        Title = "Свободные записи Архива",
                        Columns = ["Запись", "Тип", "Редкость"],
                        Rows = context.Entries
                            .Select(static entry => Row(entry.Title, AfterlifeArchiveState.GetEntryTypeLabel(entry.EntryType), FormatRarityForPlayer(entry.Rarity, "-")))
                            .ToList()
                    },
                    new UiTableBlock
                    {
                        Title = "Дружественные Хранители",
                        Columns = guardianColumns,
                        Rows = context.Guardians
                            .Select(static guardian => Row(
                                guardian.GuardianName,
                                guardian.Reputation.ToString(),
                                guardian.FuelAvailable ? guardian.TargetProjectName : FirstNonEmpty(guardian.Domain, "не указан")))
                            .ToList()
                    }
                ]
            }
        ];
    }

    private static UiSelectionOption BuildArchiveEntryOption(BrowserAfterlifeArchiveEntryChoice entry) =>
        Option(
            entry.ArchiveId,
            entry.Title,
            $"{AfterlifeArchiveState.GetEntryTypeLabel(entry.EntryType)}. Редкость: {FormatRarityForPlayer(entry.Rarity)}. {entry.Summary}");

    private static List<UiAction> BuildArchiveConsultationDetailActions(BrowserAfterlifeArchiveActionContext context)
    {
        var actions = BuildArchiveEntryDetailActions(context.Entries);
        actions.AddRange(context.Guardians.Select(static guardian =>
            DetailAction(
                "archive-consultation-detail",
                guardian.GuardianId,
                guardian.GuardianName,
                "/archive_consultation хранитель " + SoulRelicEquipmentService.FormatCommandArgument(guardian.GuardianId))));
        return actions;
    }

    private static List<UiAction> BuildArchiveProjectFuelDetailActions(BrowserAfterlifeArchiveActionContext context)
    {
        var actions = BuildArchiveEntryDetailActions(context.Entries);
        actions.AddRange(context.Guardians
            .Where(static guardian => guardian.FuelAvailable && !string.IsNullOrWhiteSpace(guardian.TargetProjectId))
            .Select(static guardian =>
                DetailAction(
                    "archive-project-fuel-detail",
                    $"{guardian.GuardianId}-{guardian.TargetProjectId}",
                    guardian.TargetProjectName,
                    "/archive_project_fuel проект " + SoulRelicEquipmentService.FormatCommandArgument($"{guardian.GuardianId}::{guardian.TargetProjectId}"))));
        return actions;
    }

    private static List<UiAction> BuildArchiveEntryDetailActions(IEnumerable<BrowserAfterlifeArchiveEntryChoice> entries) =>
        entries
            .Select(static entry =>
                DetailAction(
                    "afterlife-archive-detail",
                    entry.ArchiveId,
                    entry.Title,
                    "/afterlife_archive запись " + SoulRelicEquipmentService.FormatCommandArgument(entry.ArchiveId)))
            .ToList();

    private static ExplorerCommandResult BuildArchiveConsultationGuardianDetail(
        string command,
        BrowserAfterlifeArchiveActionContext context,
        string selector)
    {
        if (context.IsBlocked)
            return DetailUnavailable(command, "Архивная консультация");

        var guardian = ResolveArchiveGuardian(context, selector);
        if (guardian == null)
            return DetailUnavailable(command, "Архивная консультация");

        return Result(
            command,
            CommandExecutionState.Completed,
            [
                new UiPanelBlock
                {
                    Title = $"Архивная консультация: {guardian.GuardianName}",
                    Blocks =
                    [
                        new UiKeyValueGridBlock
                        {
                            Items =
                            [
                                KeyValue("Хранитель", guardian.GuardianName),
                                KeyValue("Домен", FirstNonEmpty(guardian.Domain, "не указан")),
                                KeyValue("Репутация", guardian.Reputation.ToString()),
                                KeyValue("Свободных записей Архива", context.Entries.Count.ToString())
                            ]
                        },
                        new UiTextBlock
                        {
                            Text = "Этот Хранитель доступен для архивной консультации. Выберите свободную запись Архива в форме, если хотите попросить его раскрыть смысл выбранного фрагмента.",
                            Tone = UiTone.Accent
                        },
                        new UiTableBlock
                        {
                            Title = "Свободные записи Архива",
                            Columns = ["Запись", "Тип", "Редкость"],
                            Rows = context.Entries
                                .Select(static entry => Row(entry.Title, AfterlifeArchiveState.GetEntryTypeLabel(entry.EntryType), FormatRarityForPlayer(entry.Rarity, "-")))
                                .ToList()
                        }
                    ]
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildArchiveProjectFuelDetailAsync(
        string command,
        FileSystemManager fs,
        BrowserAfterlifeArchiveActionContext context,
        string selector)
    {
        var (guardianSelector, projectSelector) = SplitArchiveProjectSelector(selector);
        if (context.IsBlocked)
        {
            if (string.IsNullOrWhiteSpace(guardianSelector) || string.IsNullOrWhiteSpace(projectSelector))
                return DetailUnavailable(command, "Подпитка проекта");

            var blockedProject = await ReadArchiveProjectDetailAsync(fs, guardianSelector, projectSelector);
            if (blockedProject == null)
                return DetailUnavailable(command, "Подпитка проекта");

            var blockedProjectName = FirstNonEmpty(GetString(blockedProject, "projectName"), projectSelector);
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    new UiPanelBlock
                    {
                        Title = $"Подпитка проекта: {blockedProjectName}",
                        Blocks =
                        [
                            new UiKeyValueGridBlock
                            {
                                Items =
                                [
                                    KeyValue("Хранитель", guardianSelector),
                                    KeyValue("Проект", blockedProjectName),
                                    KeyValue("Тип", FormatProjectTypeForPlayer(GetString(blockedProject, "projectType"))),
                                    KeyValue("Ранг", FormatProjectTierForPlayer(GetString(blockedProject, "projectTier"))),
                                    KeyValue("Режим", FormatProjectModeForPlayer(GetString(blockedProject, "projectMode")))
                                ]
                            },
                            new UiTextBlock
                            {
                                Text = context.BlockerMessage,
                                Tone = UiTone.Warning
                            }
                        ]
                    }
                ]);
        }

        var guardian = ResolveArchiveGuardian(context, guardianSelector);
        if (guardian == null ||
            !guardian.FuelAvailable ||
            string.IsNullOrWhiteSpace(guardian.TargetProjectId) ||
            (!string.IsNullOrWhiteSpace(projectSelector) &&
             !string.Equals(guardian.TargetProjectId, projectSelector, StringComparison.OrdinalIgnoreCase)))
        {
            return DetailUnavailable(command, "Подпитка проекта");
        }

        var project = await ReadArchiveProjectDetailAsync(fs, guardian.GuardianId, guardian.TargetProjectId);
        if (project == null)
            return DetailUnavailable(command, "Подпитка проекта");

        var projectName = FirstNonEmpty(GetString(project, "projectName"), guardian.TargetProjectName, guardian.TargetProjectId);
        return Result(
            command,
            CommandExecutionState.Completed,
            [
                new UiPanelBlock
                {
                    Title = $"Подпитка проекта: {projectName}",
                    Blocks =
                    [
                        new UiKeyValueGridBlock
                        {
                            Items =
                            [
                                KeyValue("Хранитель", guardian.GuardianName),
                                KeyValue("Репутация", guardian.Reputation.ToString()),
                                KeyValue("Проект", projectName),
                                KeyValue("Тип", FormatProjectTypeForPlayer(GetString(project, "projectType"))),
                                KeyValue("Ранг", FormatProjectTierForPlayer(GetString(project, "projectTier"))),
                                KeyValue("Режим", FormatProjectModeForPlayer(GetString(project, "projectMode")))
                            ]
                        },
                        new UiTextBlock
                        {
                            Text = "Свободная запись Архива может стать топливом для этого проекта через форму подпитки. Деталь только показывает цель и не создаёт запрос.",
                            Tone = UiTone.Accent
                        },
                        new UiTableBlock
                        {
                            Title = "Свободные записи Архива",
                            Columns = ["Запись", "Тип", "Редкость"],
                            Rows = context.Entries
                                .Select(static entry => Row(entry.Title, AfterlifeArchiveState.GetEntryTypeLabel(entry.EntryType), FormatRarityForPlayer(entry.Rarity, "-")))
                                .ToList()
                        }
                    ]
                }
            ]);
    }

    private static BrowserAfterlifeArchiveGuardianChoice? ResolveArchiveGuardian(
        BrowserAfterlifeArchiveActionContext context,
        string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
            return null;

        return context.Guardians.FirstOrDefault(guardian =>
                   string.Equals(guardian.GuardianId, selector, StringComparison.OrdinalIgnoreCase)) ??
               context.Guardians.FirstOrDefault(guardian =>
                   string.Equals(guardian.GuardianName, selector, StringComparison.OrdinalIgnoreCase));
    }

    private static (string GuardianId, string ProjectId) SplitArchiveProjectSelector(string selector)
    {
        var parts = selector.Split("::", 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (selector.Trim(), string.Empty);
    }

    private static async Task<JsonObject?> ReadArchiveProjectDetailAsync(
        FileSystemManager fs,
        string guardianId,
        string projectId)
    {
        var projectRoot = await ReadJson(fs, GuardianProjectState.TrackerPath);
        if (projectRoot.Node is JsonObject root)
        {
            foreach (var entry in EnumerateArchiveProjectDetailCandidates(root))
            {
                if (!string.Equals(GetString(entry, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase) ||
                    entry["project"] is not JsonObject project ||
                    !string.Equals(GetString(project, "projectId"), projectId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return project;
            }
        }

        var journalRoot = await ReadJson(fs, GuardianProjectState.JournalPath);
        if (journalRoot.Node is not JsonObject journal ||
            journal["entries"] is not JsonArray entries)
        {
            return null;
        }

        foreach (var entry in entries.OfType<JsonObject>())
        {
            if (!string.Equals(GetString(entry, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetString(entry, "projectId"), projectId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return new JsonObject
            {
                ["projectId"] = projectId,
                ["projectName"] = FirstNonEmpty(GetString(entry, "title"), projectId),
                ["projectType"] = FirstNonEmpty(GetString(entry, "eventType"), "journal"),
                ["projectTier"] = FirstNonEmpty(GetString(entry, "visibility"), "visible"),
                ["projectMode"] = "display",
                ["summary"] = GetString(entry, "summary")
            };
        }

        return null;
    }

    private static IEnumerable<JsonObject> EnumerateArchiveProjectDetailCandidates(JsonObject root)
    {
        foreach (var propertyName in new[] { "activeProjects", "projects", "completedProjects" })
        {
            if (root[propertyName] is not JsonArray projects)
                continue;

            foreach (var entry in projects.OfType<JsonObject>())
                yield return entry;
        }
    }

    private static async Task<ExplorerCommandResult> BuildAbodeOfferingAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var guardians = await ReadJson(fs, "game_state/meta/guardians.json");
        var pending = await ReadJson(fs, GuardianAbodeOfferingState.PendingRequestPath);
        var guardianRows = CollectObjects(guardians.Node)
            .Where(static item => !string.IsNullOrWhiteSpace(FirstNonEmpty(GetString(item, "guardianId"), GetString(item, "id"))))
            .Select(static item => Row(
                FirstNonEmpty(GetString(item, "canonicalName"), GetString(item, "guardianName"), GetString(item, "name"), "?"),
                FirstNonEmpty(GetString(item, "guardianId"), GetString(item, "id"), "-"),
                FirstNonEmpty(GetString(item["abode"] as JsonObject, "name"), GetString(item, "abodeName"), "-")))
            .ToList();

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Подношение Обители",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = stateManager.CurrentState.IsInAfterlifeRealm
                            ? "Выберите Хранителя, тип подношения и подтвердите намерение. ГМ разыграет результат в следующем принятом ходе."
                            : "Подношение Обители доступно только в загробье.",
                        Tone = stateManager.CurrentState.IsInAfterlifeRealm ? UiTone.Accent : UiTone.Warning
                    },
                    new UiTableBlock
                    {
                        Title = "Известные Хранители",
                        Columns = ["Хранитель", "ID", "Обитель"],
                        Rows = guardianRows
                    }
                ]
            }
        };
        AddRawOrWarning(blocks, "JSON: pending_abode_offering", pending);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiTextInputPrompt { Id = "guardian_id", Prompt = "Хранитель или Обитель", Placeholder = "Имя или идентификатор из списка" },
                new UiSelectionPrompt
                {
                    Id = "offering_type",
                    Prompt = "Тип подношения",
                    Required = true,
                    Options =
                    [
                        Option("ink_feathers", "Чернильные Перья", "Подношение валютой в пределах лимита возвращения."),
                        Option("soul_relic", "Реликвия Души", "Реликвия будет передана как подношение."),
                        Option("lore_fragment", "Фрагмент Знания", "Запись Архива будет передана как подношение."),
                        Option("secret_record", "Запись Тайны", "Запись Архива будет передана как подношение.")
                    ]
                },
                new UiTextInputPrompt { Id = "offering_value", Prompt = "Сумма или ID предмета/записи", Placeholder = "50 / relicId / archiveId" }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildPlayerGuardianFoundationAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs);
        var context = await PlayerGuardianFoundationState.ReadContextAsync(fs);
        var pending = await ReadJson(fs, PlayerGuardianFoundationState.PendingRequestPath);
        var status = context.PendingRequest != null
            ? "Ритуал уже подготовлен и ждёт хода GM."
            : context.HasCompletedFoundation
                ? "Ветка основания собственной мантии уже завершена."
                : context.CanCreateRequest
                    ? "Можно подготовить новую Хранительскую мантию."
                    : context.BlockingReason;

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Основание собственной мантии",
                Blocks =
                [
                    new UiTextBlock { Text = status, Tone = context.CanCreateRequest ? UiTone.Accent : UiTone.Warning },
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            KeyValue("Имя души", context.SoulName),
                            KeyValue("Прежний покровитель", context.PreviousGuardianName),
                            KeyValue("Завершено", context.HasCompletedFoundation ? "да" : "нет")
                        ]
                    }
                ]
            }
        };
        AddRawOrWarning(blocks, "JSON: pending_player_guardian_foundation", pending);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiTextInputPrompt { Id = "proposed_display_name", Prompt = "Имя новой мантии", Required = true },
                new UiLongTextInputPrompt { Id = "mantle_summary", Prompt = "Краткая сущность", Required = true, MinLines = 2, MaxLines = 6 },
                new UiLongTextInputPrompt { Id = "mantle_creed", Prompt = "Кредо", Required = true, MinLines = 2, MaxLines = 6 },
                new UiTextInputPrompt { Id = "appearance_motifs", Prompt = "Образные мотивы через запятую", Required = true },
                new UiSelectionPrompt
                {
                    Id = "dominant_aspect",
                    Prompt = "Доминирующий аспект",
                    Options =
                    [
                        Option("", "Без доминирующего аспекта", ""),
                        Option("memory", "Память", ""),
                        Option("forge", "Кузня", ""),
                        Option("knowledge", "Знание", ""),
                        Option("patronage", "Покровительство", ""),
                        Option("power", "Власть", ""),
                        Option("path", "Путь", "")
                    ],
                    AllowCustom = true
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildSpiritualActionAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs);
        var conflictRoot = await ReadJson(fs, AfterlifeSpiritualConflictState.StatePath);
        var active = (conflictRoot.Node as JsonObject)?["activeConflict"] as JsonObject;
        var conflictId = FirstNonEmpty(GetString(active, "conflictId"), "нет активного конфликта");

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Духовное действие",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = active == null
                            ? "Нет активного духовного конфликта. ГМ должен начать конфликт через принятый ход, либо игрок может описывать обычную прозу без явного route tag."
                            : "Опишите одно намерение в активном духовном конфликте. Браузерный DTO фиксирует route tag и форму, но не вызывает console-bound отправку хода.",
                        Tone = active == null ? UiTone.Warning : UiTone.Accent
                    },
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            KeyValue("Текущий realm", stateManager.CurrentState.CurrentRealm ?? "?"),
                            KeyValue("Активный конфликт", conflictId),
                            KeyValue("Response surface", AfterlifeSpiritualConflictState.ResponseField),
                            KeyValue("State file", AfterlifeSpiritualConflictState.StatePath)
                        ]
                    }
                ]
            }
        };
        AddRawOrWarning(blocks, "JSON: active afterlife spiritual conflict", new JsonReadResult(AfterlifeSpiritualConflictState.StatePath, conflictRoot.FileExists, AfterlifeCombatConditionPlayerAuditSanitizer.Sanitize(active), conflictRoot.Error));

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "operation_type",
                    Prompt = "Духовное искусство / действие",
                    Required = true,
                    Options =
                    [
                        Option("pressure", "Давление", "Ухудшить напряжение противника."),
                        Option("guard", "Защита", "Снизить или предотвратить входящий вред."),
                        Option("maneuver", "Манёвр", "Улучшить позицию конфликта."),
                        Option("counter", "Контрприём", "Рискованно развернуть конкретное входящее действие."),
                        Option("binding", "Оковы", "Создать или усилить контроль при наличии преимущества/подготовки."),
                        Option("break_binding", "Разрыв оков", "Ослабить или снять контроль противника."),
                        Option("recover_spiritual_power", "Восстановить духовную силу", "Восстановить ОД, рискуя попасть под активное давление."),
                        Option("negotiate", "Переговоры", "Попытаться завершить или изменить условия конфликта.")
                    ],
                    AllowCustom = true
                },
                new UiLongTextInputPrompt
                {
                    Id = "spiritual_action_text",
                    Prompt = "Художественное описание действия",
                    Required = true,
                    Placeholder = "Опишите, что именно делает душа игрока.",
                    MinLines = 3,
                    MaxLines = 10
                }
            ]);
    }

    private static LocalTurnStatus BuildLocalTurnStatus(FileSystemManager fs, bool playerFacing = false)
    {
        var entries = new List<UiTableRow>();
        AddArtifact(entries, fs, TurnRequestPath, "Запрос хода GM", playerFacing);
        AddArtifact(entries, fs, TurnCompletePath, "Готов успешный ответ", playerFacing);
        AddArtifact(entries, fs, TurnErrorPath, playerFacing ? "Готова ошибка хода" : "Готов terminal error", playerFacing);
        AddArtifact(entries, fs, PendingTurnSnapshotManifestPath, playerFacing ? "Снимок состояния хода" : "Validated pending snapshot", playerFacing);
        AddDirectoryArtifact(entries, fs, PendingTurnSnapshotDirectory, playerFacing ? "Копии файлов текущего хода" : "Копии snapshot файлов", playerFacing);
        AddDirectoryArtifact(entries, fs, ExplorerRollbackDirectory, playerFacing ? "Копии восстановления локальной записи" : "Локальные rollback backup", playerFacing);

        var hasActive = fs.FileExists(TurnRequestPath) ||
                        fs.FileExists(TurnCompletePath) ||
                        fs.FileExists(TurnErrorPath) ||
                        fs.FileExists(PendingTurnSnapshotManifestPath) ||
                        DirectoryHasContent(fs, PendingTurnSnapshotDirectory) ||
                        DirectoryHasContent(fs, ExplorerRollbackDirectory);

        var message = playerFacing
            ? hasActive
                ? "Активный ход GM или локальное восстановление обнаружены. Дождитесь завершения, ошибки или отмены текущего хода перед локальной формой."
                : "Активный ход GM не обнаружен. Можно открыть форму локального действия."
            : hasActive
                ? "Активный ход GM или локальный rollback/snapshot обнаружен. Browser-write команды должны дождаться завершения, ошибки или отмены этого протокола."
                : "Активный ход GM не обнаружен. Browser DTO может безопасно показать форму локального действия.";

        return new LocalTurnStatus(
            hasActive,
            new UiPanelBlock
            {
                Title = playerFacing ? "Локальный ход" : "Локальный ход / GM-turn protocol",
                Blocks =
                [
                    Message(hasActive ? UiNotificationSeverity.Warning : UiNotificationSeverity.Info, hasActive ? "Активный ход GM" : "Локальный ход свободен", message),
                    new UiTableBlock
                    {
                        Title = playerFacing ? "Состояние локальной записи" : "Артефакты протокола",
                        Columns = playerFacing ? ["Проверка", "Статус"] : ["Артефакт", "Путь", "Статус"],
                        Rows = entries
                    }
                ]
            });
    }

    private static void AddArtifact(List<UiTableRow> rows, FileSystemManager fs, string path, string label, bool playerFacing) =>
        rows.Add(playerFacing
            ? Row(label, fs.FileExists(path) ? "есть" : "нет")
            : Row(label, path, fs.FileExists(path) ? "есть" : "нет"));

    private static void AddDirectoryArtifact(List<UiTableRow> rows, FileSystemManager fs, string path, string label, bool playerFacing) =>
        rows.Add(playerFacing
            ? Row(label, DirectoryHasContent(fs, path) ? "есть файлы" : "нет файлов")
            : Row(label, path, DirectoryHasContent(fs, path) ? "есть файлы" : "нет файлов"));

    private static bool DirectoryHasContent(FileSystemManager fs, string path)
    {
        var fullPath = fs.ResolvePath(path);
        return Directory.Exists(fullPath) && Directory.EnumerateFileSystemEntries(fullPath, "*", SearchOption.AllDirectories).Any();
    }

    private static string FormatRealmForPlayer(string? realm)
    {
        if (string.IsNullOrWhiteSpace(realm))
            return "не определено";

        return realm.Trim().ToLowerInvariant() switch
        {
            "mortal world" => "смертный мир",
            "chaos sea" => "Море Хаоса",
            "shining abode" => "Сияющая Обитель",
            "life evaluation" => "оценка прожитой жизни",
            _ => realm.Trim()
        };
    }

    private static async Task<JsonReadResult> ReadJson(FileSystemManager fs, string path)
    {
        var raw = await fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(raw))
            return new JsonReadResult(path, fs.FileExists(path), null, string.Empty);

        try
        {
            return new JsonReadResult(path, true, JsonNode.Parse(raw), string.Empty);
        }
        catch (Exception ex)
        {
            return new JsonReadResult(path, true, null, ex.Message);
        }
    }

    private static void AddRawOrWarning(List<UiBlock> blocks, string title, JsonReadResult read)
    {
        if (!read.FileExists)
            blocks.Add(Message(UiNotificationSeverity.Info, title, $"Файл не найден: {read.Path}"));
        else if (!string.IsNullOrWhiteSpace(read.Error))
            blocks.Add(Message(UiNotificationSeverity.Error, title, $"JSON повреждён: {read.Error}"));
        else if (read.Node != null)
            blocks.Add(Raw(title, read.Node));
        else
            blocks.Add(Message(UiNotificationSeverity.Warning, title, $"Файл пуст или не содержит JSON: {read.Path}"));
    }

    private static IEnumerable<JsonObject> CollectObjects(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var item in EnumerateKnownArrays(obj))
                yield return item;

            if (obj.Count > 0)
                yield return obj;
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr.OfType<JsonObject>())
                yield return item;
        }
    }

    private static IEnumerable<JsonObject> EnumerateKnownArrays(JsonObject obj)
    {
        foreach (var prop in obj)
        {
            if (prop.Value is JsonArray arr)
            {
                foreach (var item in arr.OfType<JsonObject>())
                    yield return item;
            }
            else if (prop.Value is JsonObject child)
            {
                foreach (var item in EnumerateKnownArrays(child))
                    yield return item;
            }
        }
    }

    private static IEnumerable<NpcConversationOption> CollectNpcConversationOptions(JsonNode? node)
    {
        return CollectObjects(node)
            .Select(static obj =>
            {
                var stableId = FirstNonEmpty(GetString(obj, "npcId"), GetString(obj, "id"));
                var name = FirstNonEmpty(GetString(obj, "name"), GetString(obj, "npcName"), GetString(obj, "displayName"), stableId);
                if (string.IsNullOrWhiteSpace(stableId) || string.IsNullOrWhiteSpace(name))
                    return null;

                return new NpcConversationOption(
                    stableId,
                    name,
                    FirstNonEmpty(GetString(obj, "currentLocation"), GetString(obj, "currentLocationId"), GetString(obj, "location")),
                    FirstNonEmpty(GetString(obj, "relationshipLevel"), GetString(obj, "relationship"), GetString(obj, "attitude")));
            })
            .Where(static npc => npc != null)
            .Cast<NpcConversationOption>()
            .DistinctBy(static npc => npc.NpcId, StringComparer.OrdinalIgnoreCase);
    }

    private static NpcConversationOption? ResolveNpcConversationOption(
        IReadOnlyList<NpcConversationOption> npcs,
        string requestedNpcId)
    {
        if (string.IsNullOrWhiteSpace(requestedNpcId))
            return null;

        return npcs.FirstOrDefault(npc =>
                   string.Equals(npc.NpcId, requestedNpcId, StringComparison.OrdinalIgnoreCase)) ??
               npcs.FirstOrDefault(npc =>
                   string.Equals(npc.Name, requestedNpcId, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<GuardianSocialOption> CollectGuardianSocialOptions(JsonNode? node)
    {
        return EnumerateCanonicalGuardianObjects(node)
            .Select(static obj =>
            {
                var stableId = FirstNonEmpty(GetString(obj, "guardianId"), GetString(obj, "id"));
                if (string.IsNullOrWhiteSpace(stableId))
                    return null;

                var manifestation = obj["manifestation"] as JsonObject;
                var abode = obj["abode"] as JsonObject;
                var name = FirstNonEmpty(
                    GetString(obj, "canonicalName"),
                    GetString(obj, "guardianName"),
                    GetString(obj, "name"),
                    GetString(manifestation, "currentDisplayName"),
                    GetString(obj, "displayName"),
                    stableId);

                return new GuardianSocialOption(
                    stableId,
                    name,
                    FirstNonEmpty(GetString(obj, "domain"), GetString(obj, "domainTag")),
                    FirstNonEmpty(GetString(abode, "name"), GetString(obj, "abodeName"), GetString(abode, "abodeId")));
            })
            .Where(static guardian => guardian != null)
            .Cast<GuardianSocialOption>()
            .DistinctBy(static guardian => guardian.GuardianId, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<JsonObject> EnumerateCanonicalGuardianObjects(JsonNode? node)
    {
        if (node is JsonArray directArray)
        {
            foreach (var guardian in directArray.OfType<JsonObject>())
                yield return guardian;
            yield break;
        }

        if (node is not JsonObject root)
            yield break;

        if (root["guardians"] is JsonArray guardians)
        {
            foreach (var guardian in guardians.OfType<JsonObject>())
                yield return guardian;
        }

        if (root["activeGuardian"] is JsonObject activeGuardian)
            yield return activeGuardian;
    }

    private static GuardianSocialOption? ResolveGuardianSocialOption(
        IReadOnlyList<GuardianSocialOption> guardians,
        string requestedGuardian)
    {
        if (string.IsNullOrWhiteSpace(requestedGuardian))
            return null;

        return guardians.FirstOrDefault(guardian =>
                   string.Equals(guardian.GuardianId, requestedGuardian, StringComparison.OrdinalIgnoreCase)) ??
               guardians.FirstOrDefault(guardian =>
                   string.Equals(guardian.Name, requestedGuardian, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<bool> IsAfterlifeRealmAsync(FileSystemManager fs, StateManager stateManager)
    {
        var soulRoot = (await ReadJson(fs, SoulStatePath)).Node as JsonObject;
        var currentRealm = FirstNonEmpty(GetString(soulRoot, "currentRealm"), stateManager.CurrentState.CurrentRealm);
        return RealmSemantics.IsAfterlifeRealm(currentRealm);
    }

    private static ExplorerCommandResult ResidentRealmBlocker(string command, UiPanelBlock localTurnPanel, string title) =>
        Result(
            command,
            CommandExecutionState.Blocked,
            [
                localTurnPanel,
                Message(
                    UiNotificationSeverity.Warning,
                    title,
                    "Действия с обитателями Обители доступны только в посмертии. Сейчас действие недоступно для текущего царства.")
            ]);

    private static ExplorerCommandResult ResidentStateUnavailable(
        string command,
        UiPanelBlock localTurnPanel,
        string title,
        string message) =>
        Result(
            command,
            CommandExecutionState.Completed,
            [
                localTurnPanel,
                Message(UiNotificationSeverity.Warning, title, message)
            ]);

    private static IEnumerable<GuardianAbodeBrowserOption> CollectGuardianAbodeOptions(JsonNode? node)
    {
        return EnumerateCanonicalGuardianObjects(node)
            .Select(static guardian =>
            {
                var stableId = FirstNonEmpty(GetString(guardian, "guardianId"), GetString(guardian, "id"));
                if (string.IsNullOrWhiteSpace(stableId) || guardian["abode"] is not JsonObject abode)
                    return null;

                var abodeId = FirstNonEmpty(GetString(abode, "abodeId"), GetString(abode, "id"));
                if (string.IsNullOrWhiteSpace(abodeId))
                    return null;

                var manifestation = guardian["manifestation"] as JsonObject;
                var name = FirstNonEmpty(
                    GetString(guardian, "canonicalName"),
                    GetString(guardian, "guardianName"),
                    GetString(guardian, "name"),
                    GetString(manifestation, "currentDisplayName"),
                    GetString(guardian, "displayName"),
                    stableId);
                var relationship = guardian["relationshipData"] as JsonObject;
                return new GuardianAbodeBrowserOption(
                    stableId,
                    name,
                    FirstNonEmpty(GetString(guardian, "domain"), GetString(guardian, "domainTag")),
                    abodeId,
                    FirstNonEmpty(GetString(abode, "name"), GetString(abode, "displayName"), GetString(guardian, "abodeName"), abodeId),
                    GetNodeInt(relationship?["currentReputation"]),
                    AbodePowerRules.GetCurrentPower(guardian));
            })
            .Where(static option => option != null)
            .Cast<GuardianAbodeBrowserOption>()
            .DistinctBy(static option => option.CompositeId, StringComparer.OrdinalIgnoreCase);
    }

    private static GuardianAbodeBrowserOption? ResolveGuardianAbodeOption(
        IReadOnlyList<GuardianAbodeBrowserOption> abodes,
        string requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return null;

        return abodes.FirstOrDefault(abode =>
                   string.Equals(abode.CompositeId, requested, StringComparison.OrdinalIgnoreCase)) ??
               abodes.FirstOrDefault(abode =>
                   string.Equals(abode.GuardianId, requested, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(abode.AbodeId, requested, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(abode.GuardianName, requested, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(abode.AbodeName, requested, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<ResidentBrowserContext> ReadResidentBrowserContextAsync(FileSystemManager fs)
    {
        var guardiansRead = await ReadJson(fs, "game_state/meta/guardians.json");
        if (!string.IsNullOrWhiteSpace(guardiansRead.Error))
            return ResidentBrowserContext.Failed("Список Хранителей временно недоступен. Повторите действие после проверки состояния.");

        var guardiansRoot = guardiansRead.Node as JsonObject ?? new JsonObject();
        var abodes = CollectGuardianAbodeOptions(guardiansRoot).ToList();
        var residentRead = await ReadJson(fs, GuardianAbodeResidentState.StatePath);
        if (!string.IsNullOrWhiteSpace(residentRead.Error))
            return ResidentBrowserContext.Failed("Состав обитателей временно недоступен. Повторите действие после проверки состояния.");

        var residentsRoot = residentRead.Node as JsonObject;
        if (residentsRoot?["entries"] is not JsonArray entries)
            return new ResidentBrowserContext(guardiansRoot, residentsRoot, []);

        var powerByGuardian = GuardianAbodeResidentState.CollectGuardianAbodePowerById(guardiansRoot);
        var residents = new List<ResidentBrowserOption>();
        foreach (var entry in entries.OfType<JsonObject>())
        {
            var guardianId = GetString(entry, "guardianId");
            var abodeId = GetString(entry, "abodeId");
            if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(abodeId))
                continue;

            var currentPower = powerByGuardian.TryGetValue(guardianId, out var power) ? power : (int?)null;
            var resident = GuardianAbodeResidentState.ReadResidentEntry(entry, currentPower);
            if (string.IsNullOrWhiteSpace(resident.ResidentId))
                continue;

            var abode = abodes.FirstOrDefault(candidate =>
                string.Equals(candidate.GuardianId, resident.GuardianId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.AbodeId, resident.AbodeId, StringComparison.OrdinalIgnoreCase));
            residents.Add(new ResidentBrowserOption(
                resident.ResidentId,
                resident.DisplayName,
                resident.ResidentKind,
                resident.GuardianId,
                FirstNonEmpty(abode?.GuardianName, resident.GuardianId),
                resident.AbodeId,
                FirstNonEmpty(abode?.AbodeName, resident.AbodeId),
                resident.AvailableInteractions,
                resident.MigrationState,
                resident.IsPresent));
        }

        return new ResidentBrowserContext(
            guardiansRoot,
            residentsRoot,
            residents
                .DistinctBy(static resident => resident.ResidentId, StringComparer.OrdinalIgnoreCase)
                .OrderBy(static resident => resident.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private static ResidentBrowserOption? ResolveResidentOption(
        IReadOnlyList<ResidentBrowserOption> residents,
        string requestedResident)
    {
        if (string.IsNullOrWhiteSpace(requestedResident))
            return null;

        return residents.FirstOrDefault(resident =>
                   string.Equals(resident.ResidentId, requestedResident, StringComparison.OrdinalIgnoreCase)) ??
               residents.FirstOrDefault(resident =>
                   string.Equals(resident.DisplayName, requestedResident, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<UiSelectionOption> BuildResidentInteractionOptions(IReadOnlyList<ResidentBrowserOption> residents)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var resident in residents)
        {
            foreach (var interaction in GetResidentAllowedInteractions(resident))
                allowed.Add(interaction);
        }

        return BuildResidentInteractionOptions(allowed);
    }

    private static IReadOnlyList<UiSelectionOption> BuildResidentInteractionOptions(ResidentBrowserOption resident) =>
        BuildResidentInteractionOptions(GetResidentAllowedInteractions(resident));

    private static HashSet<string> GetResidentAllowedInteractions(ResidentBrowserOption resident)
    {
        var allowed = resident.AvailableInteractions
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (allowed.Count == 0)
        {
            allowed.Add(GuardianAbodeResidentState.InteractionTypeTalk);
            allowed.Add(GuardianAbodeResidentState.InteractionTypeHistory);
        }

        return allowed;
    }

    private static IReadOnlyList<UiSelectionOption> BuildResidentInteractionOptions(IReadOnlySet<string> allowed)
    {
        var options = new List<UiSelectionOption>();
        if (allowed.Contains(GuardianAbodeResidentState.InteractionTypeTalk))
            options.Add(Option(GuardianAbodeResidentState.InteractionTypeTalk, "Поговорить", "Обычный разговор с обитателем Обители."));
        if (allowed.Contains(GuardianAbodeResidentState.InteractionTypeHistory))
            options.Add(Option(GuardianAbodeResidentState.InteractionTypeHistory, "Раскрыть историю", "Попросить обитателя открыть прошлую историю."));
        if (options.Count == 0)
        {
            options.Add(new UiSelectionOption
            {
                Value = "unavailable",
                Label = "Нет доступного обращения",
                Description = "Этот обитатель сейчас не готов к разговору или истории.",
                Disabled = true
            });
        }

        return options;
    }

    private static IReadOnlyList<UiSelectionOption> BuildResidentTransferOptions(
        IReadOnlyList<ResidentBrowserOption> residents,
        JsonObject guardiansRoot,
        JsonObject? residentsRoot)
    {
        var optionsByValue = new Dictionary<string, UiSelectionOption>(StringComparer.Ordinal);
        foreach (var resident in residents)
        {
            foreach (var option in BuildResidentTransferOptions(resident, guardiansRoot, residentsRoot))
            {
                if (option.Disabled || optionsByValue.ContainsKey(option.Value))
                    continue;
                optionsByValue.Add(option.Value, option);
            }
        }

        if (optionsByValue.Count > 0)
            return optionsByValue.Values.ToList();

        return
        [
            new UiSelectionOption
            {
                Value = "not_ready",
                Label = "Переход сейчас недоступен",
                Description = "Сейчас среди выбранных обитателей нет готовых к переходу.",
                Disabled = true
            }
        ];
    }

    private static IReadOnlyList<UiSelectionOption> BuildResidentTransferOptions(
        ResidentBrowserOption resident,
        JsonObject guardiansRoot,
        JsonObject? residentsRoot)
    {
        if (!string.Equals(resident.MigrationState, GuardianAbodeResidentState.MigrationStateReadyToTransfer, StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new UiSelectionOption
                {
                    Value = "not_ready",
                    Label = "Переход сейчас недоступен",
                    Description = "Этот обитатель ещё не готов покинуть текущую Обитель.",
                    Disabled = true
                }
            ];
        }

        var residentEntry = GuardianAbodeResidentState.ReadResidentEntry(new JsonObject
        {
            ["residentId"] = resident.ResidentId,
            ["displayName"] = resident.DisplayName,
            ["residentKind"] = resident.ResidentKind,
            ["guardianId"] = resident.GuardianId,
            ["abodeId"] = resident.AbodeId,
            ["migrationState"] = resident.MigrationState,
            ["isPresent"] = resident.IsPresent
        });
        var sourceResident = GuardianAbodeResidentState.FindResident(residentsRoot ?? new JsonObject(), resident.ResidentId);
        if (sourceResident != null)
            residentEntry = GuardianAbodeResidentState.ReadResidentEntry(sourceResident);

        var options = GuardianAbodeResidentState.BuildTransferCompetitionCandidates(residentEntry, guardiansRoot, residentsRoot)
            .Select(static candidate => Option(
                $"target:{candidate.TargetGuardianId}::{candidate.TargetAbodeId}",
                $"{candidate.TargetGuardianName} — {candidate.TargetAbodeName}",
                $"{GuardianAbodeResidentState.GetTransferCompetitionLabelText(candidate.CompetitionLabel)} {candidate.CompetitionScore}/100. {candidate.CompetitionReason}"))
            .ToList();
        options.Add(Option("departure_only", "Уйти без новой Обители", "Разрешить уход; ГМ решит последствия и отметит исход."));
        return options;
    }

    private static IEnumerable<T> OrderWithSelection<T>(IEnumerable<T> values, T? selected, Func<T, string> labelSelector)
        where T : class =>
        values
            .OrderByDescending(value => selected != null && EqualityComparer<T>.Default.Equals(value, selected))
            .ThenBy(labelSelector, StringComparer.OrdinalIgnoreCase);

    private static string NormalizeCommand(string command)
    {
        var parts = command.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? string.Empty : parts[0].ToLowerInvariant();
    }

    private static string ReadCommandArguments(string command)
    {
        var parts = command.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length < 2 ? string.Empty : parts[1].Trim();
    }

    private static bool TrySplitLeadingArgument(string arguments, out string leadingArgument, out string remainingArguments)
    {
        var parts = arguments.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            leadingArgument = string.Empty;
            remainingArguments = string.Empty;
            return false;
        }

        leadingArgument = parts[0].Trim('"');
        remainingArguments = parts.Length == 2 ? parts[1] : string.Empty;
        return true;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string GetString(JsonObject? obj, string propertyName)
    {
        if (obj == null || !obj.TryGetPropertyValue(propertyName, out var node) || node == null)
            return string.Empty;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
                return text ?? string.Empty;
            if (value.TryGetValue<int>(out var number))
                return number.ToString();
            if (value.TryGetValue<bool>(out var boolean))
                return boolean ? "true" : "false";
        }

        return node.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
    }

    private static int TryGetInt(JsonObject? obj, string propertyName, int fallback)
    {
        if (obj == null || !obj.TryGetPropertyValue(propertyName, out var node) || node == null)
            return fallback;

        if (node is JsonValue value && value.TryGetValue<int>(out var parsed))
            return parsed;

        return fallback;
    }

    private static int GetNodeInt(JsonNode? node, int fallback = 0)
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

    private static int GetSoulInkFeathers(JsonObject? soulRoot, int fallback)
    {
        if (soulRoot == null)
            return fallback;

        if (!soulRoot.TryGetPropertyValue("inkFeathers", out var node) || node == null)
            return fallback;

        if (node is JsonObject inkFeathers)
            return TryGetInt(inkFeathers, "current", fallback);

        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var number))
                return number;
            if (value.TryGetValue<string>(out var text) && int.TryParse(text, out var parsed))
                return parsed;
        }

        return fallback;
    }

    private static string FormatDiceUsed(JsonArray? diceUsed)
    {
        if (diceUsed == null || diceUsed.Count == 0)
            return "[]";

        var values = diceUsed
            .Select(static node => node is JsonValue value
                ? value.TryGetValue<int>(out var number)
                    ? number.ToString()
                    : value.TryGetValue<string>(out var text)
                        ? text
                        : node.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)
                : node?.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed) ?? "null")
            .ToArray();
        return "[" + string.Join(", ", values) + "]";
    }

    private static string FormatDiceUsed(IReadOnlyCollection<int>? diceUsed)
    {
        if (diceUsed == null || diceUsed.Count == 0)
            return "[]";

        return "[" + string.Join(", ", diceUsed) + "]";
    }

    private static int ComputeRevealFateCost(int currentFeathers) =>
        Math.Max(5, (int)(Math.Max(0, currentFeathers) * 0.10));

    private static int ComputeRewriteFateCost(int currentFeathers) =>
        Math.Max(15, (int)(Math.Max(0, currentFeathers) * 0.25));

    private static string FormatGachaBaseSummary(GachaResult? gacha)
    {
        if (gacha == null)
            return "не определена";

        return $"{FormatRarityForPlayer(FirstNonEmpty(gacha.BaseRarity, "Common"))} ({gacha.BaseScore}); кубики {FormatDiceUsed(gacha.DiceUsed)}";
    }

    private static string FormatRarityForPlayer(string? rarity, string missing = "не указана")
    {
        if (string.IsNullOrWhiteSpace(rarity))
            return missing;

        return rarity.Trim().ToLowerInvariant() switch
        {
            "common" => "обычная",
            "uncommon" => "необычная",
            "good" => "хорошая",
            "rare" => "редкая",
            "epic" => "эпическая",
            "legendary" => "легендарная",
            "radiant" => "сияющая",
            "unique" => "уникальная",
            _ => rarity.Trim()
        };
    }

    private static string FormatProjectTypeForPlayer(string? projectType) =>
        FormatKnownProjectValue(projectType, projectType?.Trim().ToLowerInvariant() switch
        {
            "display_snapshot" => "снимок для отображения",
            "journal" => "запись журнала",
            "lore_research" => "исследование знаний",
            "relic_forging" => "ковка реликвии",
            "abode_expansion" => "расширение Обители",
            "abode_fortification" => "укрепление Обители",
            "offensive_intrigue" => "наступательная интрига",
            "counter_rival_operation" => "противодействие сопернику",
            "soul_preparation" => "подготовка души",
            _ => null
        });

    private static string FormatProjectTierForPlayer(string? projectTier) =>
        FormatKnownProjectValue(projectTier, projectTier?.Trim().ToLowerInvariant() switch
        {
            "visible" => "видимый",
            "minor" => "малый",
            "major" => "значимый",
            "grand" => "великий",
            _ => null
        });

    private static string FormatProjectModeForPlayer(string? projectMode) =>
        FormatKnownProjectValue(projectMode, projectMode?.Trim().ToLowerInvariant() switch
        {
            "display" => "просмотр",
            "internal" => "внутренний",
            "supportive" => "поддержка",
            "offensive" => "наступление",
            _ => null
        });

    private static string FormatKnownProjectValue(string? value, string? label)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "не указан";

        var trimmed = value.Trim();
        return label ?? trimmed.Replace('_', ' ');
    }

    private static bool TryGetBool(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is not JsonValue value)
            return false;

        return value.TryGetValue<bool>(out var parsed) && parsed;
    }

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

    private static UiTableRow Row(params string[] cells) => new() { Cells = cells.ToList() };

    private static UiKeyValueItem KeyValue(string key, string value) => new() { Key = key, Value = value };

    private static UiSelectionOption Option(string value, string label, string description, bool disabled = false) =>
        new() { Value = value, Label = label, Description = description, Disabled = disabled };

    private static UiAction DetailAction(string idPrefix, string identity, string label, string command) =>
        new()
        {
            Id = SoulRelicEquipmentService.BuildActionId(idPrefix, identity),
            Label = $"Подробно: «{label}»",
            Command = command,
            Style = UiActionStyle.Secondary,
            RequiresConfirmation = false
        };

    private static ExplorerCommandResult DetailUnavailable(string command, string title) =>
        Result(
            command,
            CommandExecutionState.Completed,
            [
                Message(
                    UiNotificationSeverity.Warning,
                    title,
                    "Не удалось открыть выбранную подробность: запись уже недоступна, устарела или не видна текущей душе.")
            ]);

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

    private static UiMessageBlock Message(UiNotificationSeverity severity, string title, string message) =>
        new() { Severity = severity, Title = title, Message = message };

    private static UiRawJsonBlock Raw(string title, JsonNode node) =>
        new() { Title = title, Json = node.DeepClone() };

    private sealed record NpcConversationOption(
        string NpcId,
        string Name,
        string Location,
        string Relationship);

    private sealed record GuardianSocialOption(
        string GuardianId,
        string Name,
        string Domain,
        string AbodeName);

    private sealed record GuardianAbodeBrowserOption(
        string GuardianId,
        string GuardianName,
        string Domain,
        string AbodeId,
        string AbodeName,
        int CurrentReputation,
        int CurrentAbodePower)
    {
        public string CompositeId => $"{GuardianId}::{AbodeId}";
    }

    private sealed record ResidentBrowserOption(
        string ResidentId,
        string DisplayName,
        string ResidentKind,
        string GuardianId,
        string GuardianName,
        string AbodeId,
        string AbodeName,
        IReadOnlyList<string> AvailableInteractions,
        string MigrationState,
        bool IsPresent);

    private sealed record ResidentBrowserContext(
        JsonObject GuardiansRoot,
        JsonObject? ResidentsRoot,
        IReadOnlyList<ResidentBrowserOption> Residents,
        string ErrorMessage = "")
    {
        public static ResidentBrowserContext Failed(string message) => new(new JsonObject(), null, [], message);
    }

    private sealed record JsonReadResult(string Path, bool FileExists, JsonNode? Node, string Error);

    private sealed record LocalTurnStatus(bool HasActiveGmTurn, UiPanelBlock Panel);
}
