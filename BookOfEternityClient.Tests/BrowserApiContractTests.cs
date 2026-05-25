using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class BrowserApiContractTests
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly string RepoRoot = TestRepoPaths.RepoRoot;
    private static readonly string FrontendRoot = Path.Combine(RepoRoot, "BookOfEternityClient.WebFrontend");
    private static readonly string ApiRoot = Path.Combine(FrontendRoot, "src", "api");
    private static readonly string FixtureRoot = Path.Combine(ApiRoot, "contract-fixtures");
    private static readonly DateTime SampleUtc = new(2026, 5, 25, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void FrontendApiContractFiles_ArePresentAndDocumentEndpointMethods()
    {
        var contractsPath = Path.Combine(ApiRoot, "contracts.ts");
        var clientPath = Path.Combine(ApiRoot, "client.ts");
        var fixtureChecksPath = Path.Combine(ApiRoot, "contract-fixture-checks.ts");

        Assert.True(File.Exists(contractsPath), $"Missing {contractsPath}");
        Assert.True(File.Exists(clientPath), $"Missing {clientPath}");
        Assert.True(File.Exists(fixtureChecksPath), $"Missing {fixtureChecksPath}");

        var contracts = File.ReadAllText(contractsPath);
        Assert.Contains("export interface BrowserMainMenuDto", contracts, StringComparison.Ordinal);
        Assert.Contains("export interface LocalWebUiSessionStatus", contracts, StringComparison.Ordinal);
        Assert.Contains("export interface BrowserGameScreenDto", contracts, StringComparison.Ordinal);
        Assert.Contains("export interface BrowserLifecycleDashboardDto", contracts, StringComparison.Ordinal);
        Assert.Contains("export interface ExplorerCommandResult", contracts, StringComparison.Ordinal);
        Assert.Contains("export interface QteWebStateDto", contracts, StringComparison.Ordinal);
        Assert.Contains("export type BrowserApiResult", contracts, StringComparison.Ordinal);
        Assert.Contains("pending-turn", contracts, StringComparison.Ordinal);
        Assert.Contains("advanced", contracts, StringComparison.OrdinalIgnoreCase);

        var client = File.ReadAllText(clientPath);
        Assert.Contains("export interface BrowserApiClient", client, StringComparison.Ordinal);
        Assert.Contains("getMainMenu", client, StringComparison.Ordinal);
        Assert.Contains("getSessionStatus", client, StringComparison.Ordinal);
        Assert.Contains("getGameScreen", client, StringComparison.Ordinal);
        Assert.Contains("getLifecycleDashboard", client, StringComparison.Ordinal);
        Assert.Contains("validateLifecycle", client, StringComparison.Ordinal);
        Assert.Contains("loadSave", client, StringComparison.Ordinal);
        Assert.Contains("executeExplorerCommand", client, StringComparison.Ordinal);
        Assert.Contains("submitPromptSession", client, StringComparison.Ordinal);
        Assert.Contains("getQteState", client, StringComparison.Ordinal);
        Assert.Contains("resolveQteAction", client, StringComparison.Ordinal);
        Assert.DoesNotContain("any", client, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ContractFixtures))]
    public void FrontendContractFixtures_MatchRepresentativeCSharpDtos(string fixtureName, object csharpDto)
    {
        var fixturePath = Path.Combine(FixtureRoot, fixtureName);
        Assert.True(File.Exists(fixturePath), $"Missing {fixturePath}");

        var expected = JsonNode.Parse(JsonSerializer.Serialize(csharpDto, WebJsonOptions));
        var actual = JsonNode.Parse(File.ReadAllText(fixturePath));

        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            $"Fixture {fixtureName} does not match the representative C# DTO.\nExpected:\n{expected!.ToJsonString(WebJsonOptions)}\nActual:\n{actual!.ToJsonString(WebJsonOptions)}");
    }

    [Fact]
    public void TypeScriptFixtureChecks_ImportEveryContractFixtureWithSatisfiesTypes()
    {
        var fixtureChecks = File.ReadAllText(Path.Combine(ApiRoot, "contract-fixture-checks.ts"));

        foreach (var fixtureName in ContractFixtures().Select(item => (string)item[0]))
            Assert.Contains($"./contract-fixtures/{fixtureName}", fixtureChecks, StringComparison.Ordinal);

        Assert.Contains("satisfies BrowserMainMenuDto", fixtureChecks, StringComparison.Ordinal);
        Assert.Contains("satisfies LocalWebUiSessionStatus", fixtureChecks, StringComparison.Ordinal);
        Assert.Contains("satisfies BrowserGameScreenDto", fixtureChecks, StringComparison.Ordinal);
        Assert.Contains("satisfies BrowserLifecycleDashboardDto", fixtureChecks, StringComparison.Ordinal);
        Assert.Contains("satisfies ExplorerCommandResult", fixtureChecks, StringComparison.Ordinal);
        Assert.Contains("satisfies QteWebStateDto", fixtureChecks, StringComparison.Ordinal);
        Assert.Contains("satisfies BrowserApiErrorPayload", fixtureChecks, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontendShell_ConsumesTypedApiContractSummaryInsteadOfHardCodedEndpointList()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));

        Assert.Contains("browserApiContractSummary", app, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/main-menu", app, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/game-screen", app, StringComparison.Ordinal);
        Assert.DoesNotContain("fetch(", app, StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> ContractFixtures()
    {
        yield return ["main-menu.json", BuildMainMenu()];
        yield return ["session-status.json", BuildSessionStatus()];
        yield return ["game-screen.json", BuildGameScreen()];
        yield return ["lifecycle-dashboard.json", BuildLifecycleDashboard()];
        yield return ["explorer-command-result.json", BuildExplorerCommandResult()];
        yield return ["qte-state.json", BuildQteState()];
        yield return ["api-error.json", BuildApiErrorPayload()];
    }

    private static BrowserMainMenuDto BuildMainMenu() =>
        new(
            SchemaVersion: 1,
            Session: new BrowserMainMenuSessionDto(
                GameSessionExists: true,
                HasReadableSoul: true,
                CanContinue: true,
                ContinueReason: "Текущую сессию можно продолжить в браузерном игровом экране.",
                SoulName: "Арион",
                CurrentRealm: "Mortal World",
                RealmLabel: "Смертный мир",
                CurrentIncarnation: 3,
                TurnNumber: 12,
                TurnLabel: "Ход 12",
                TerminalSoulDissipated: false,
                ValidationState: "clean",
                ValidationLabel: "Состояние валидно",
                PendingTurnMessage: "Активный ход ГМа не обнаружен.",
                CanStartBrowserWrite: true,
                LocalUiLocked: false,
                CheckedAtUtc: SampleUtc),
            Actions:
            [
                new BrowserMainMenuActionDto(
                    Id: "continue",
                    Label: "Продолжить",
                    Description: "Арион • Смертный мир • Ход 12",
                    Enabled: true,
                    DisabledReason: string.Empty,
                    Kind: "client-panel",
                    Command: string.Empty,
                    TargetPanel: "game-shell")
            ],
            Saves:
            [
                new BrowserSaveSlotDto(
                    SaveId: "manual:sample.json",
                    Scope: "manual",
                    ScopeLabel: "Ручное сохранение",
                    DisplayName: "Перед вратами",
                    Description: "Контрактный пример сохранения.",
                    CharacterName: "Арион",
                    TurnLabel: "Ход 12",
                    TimestampUtc: SampleUtc,
                    FileSizeBytes: 4096)
            ],
            Options: new BrowserOptionsSummaryDto(
                MusicEnabled: true,
                SoundEnabled: false,
                ConsoleFontSize: 18,
                Guidance: "Полное редактирование настроек остаётся в консольном меню до отдельной Browser Client задачи."),
            About: new BrowserAboutDto(
                Title: "Книга Вечности: Перерождение",
                Body: "Локальный браузерный клиент работает поверх того же C# runtime."),
            AdvancedShell: new BrowserAdvancedShellDto(
                Label: "Расширенный режим",
                Description: "Командная палитра и debug-инструменты скрыты от обычного главного меню.",
                InitiallyExpanded: false));

    private static LocalWebUiSessionStatus BuildSessionStatus() =>
        new(
            SchemaVersion: 1,
            Status: "ok",
            LocalOnly: true,
            BasePath: "E:/Games/The Book of Eternity Reborn/BookOfEternityClient",
            GameSessionPath: "E:/Games/The Book of Eternity Reborn/BookOfEternityClient/game_session",
            GameSessionExists: true,
            CheckedAtUtc: SampleUtc,
            CanStartBrowserWrite: true,
            PendingTurn: BuildPendingTurn(hasActive: false),
            LocalUiLock: BuildLock(exists: false));

    private static BrowserGameScreenDto BuildGameScreen() =>
        new(
            SchemaVersion: 2,
            Theme: new BrowserGameScreenThemeDto("mortal-world", "Смертный мир", "🕯", "#e1b85e"),
            Soul: new BrowserGameScreenSoulDto(
                Name: "Арион",
                Realm: "Mortal World",
                Incarnation: 3,
                InkFeathers: 5,
                EnlightenmentTier: "Искра",
                ActiveGuardianName: "Сареф"),
            Player: new BrowserGameScreenPlayerDto(
                Name: "Арион",
                Class: "Следопыт",
                Race: "Человек",
                CurrentCondition: "Собран",
                HealthPercentage: "90%",
                EnergyPercentage: "75%",
                PoisePercentage: "60%",
                ActiveConditions: ["Сосредоточенность"]),
            World: new BrowserGameScreenWorldDto(
                Location: "Пепельная дорога",
                WorldTime: "Сумерки",
                TurnNumber: 12,
                SessionId: "session-contract"),
            Narrative: new BrowserGameScreenNarrativeDto(
                Text: "Перед героем открывается тихая дорога.",
                DialogueOptions: [new BrowserGameScreenDialogueOptionDto("choice-1", "Осмотреть врата", "exploration")],
                GmThoughts: string.Empty,
                CombatLog: "Боевых событий нет.",
                ImagePrompt: "mystic road at dusk"),
            Afterlife: new BrowserGameScreenAfterlifeDto(
                ShiningRadianceExperience: 10,
                ShiningRadianceTier: 1,
                ShiningLightSparks: 2,
                ShiningHallCount: 1,
                ShiningFactionCount: 1,
                HasOpenShiningGatesDraft: false,
                IsShiningGatesDraftStale: false),
            TurnState: new BrowserGameScreenTurnStateDto(
                State: "ready",
                Title: "Можно продолжать",
                Message: "Опишите следующее действие персонажа в прозе.",
                CanStartBrowserWrite: true,
                ValidationState: "clean",
                ValidationLabel: "Состояние валидно"),
            ActionComposer: new BrowserGameScreenActionComposerDto(
                CanSubmit: true,
                Mode: "prose",
                Placeholder: "Опишите действие персонажа...",
                Guidance: "Slash-команды открываются только через расширенный режим.",
                DisabledReason: string.Empty),
            Qte: BuildQteState(),
            Flags: new BrowserGameScreenFlagsDto(
                IsInChaosSea: false,
                IsInAnyShiningAbodeState: false,
                IsInShiningAbode: false,
                IsInShiningAbodePendingBootstrap: false,
                IsInAfterlifeRealm: false,
                CanReenterShiningAbode: true));

    private static BrowserLifecycleDashboardDto BuildLifecycleDashboard() =>
        new(
            SchemaVersion: 1,
            Session: BuildSessionStatus(),
            Soul: new BrowserSoulSummaryDto(
                Name: "Арион",
                CurrentRealm: "Mortal World",
                RealmLabel: "Смертный мир",
                CurrentIncarnation: 3,
                IsReadable: true,
                ReadError: string.Empty),
            PendingTurn: BuildPendingTurn(hasActive: false),
            LocalUiLock: BuildLock(exists: false),
            CanStartBrowserWrite: true,
            Validation: BuildValidationSummary(),
            Guidance:
            [
                new BrowserLifecycleGuidanceDto(
                    Severity: "success",
                    Title: "Локальные записи из браузера доступны",
                    Message: "Активный ход ГМа и свежая чужая UI-блокировка не обнаружены.",
                    ActionLabel: "Можно запускать перенесённые браузерные формы",
                    Command: string.Empty)
            ],
            Entrypoints:
            [
                new BrowserLifecycleEntrypointDto(
                    Label: "Проверить валидацию",
                    Command: "/validate",
                    Endpoint: "/api/lifecycle/validate",
                    Enabled: true,
                    Description: "Запускает тот же ValidationService, что и консоль.")
            ]);

    private static ExplorerCommandResult BuildExplorerCommandResult() =>
        new()
        {
            Command = "/help",
            State = CommandExecutionState.Completed,
            Blocks =
            [
                new UiMessageBlock
                {
                    Severity = UiNotificationSeverity.Info,
                    Title = "Справка",
                    Message = "Контрактный пример результата команды."
                }
            ],
            Actions =
            [
                new UiAction
                {
                    Id = "open-status",
                    Label = "Открыть статус",
                    Command = "/status",
                    Style = UiActionStyle.Primary,
                    RequiresConfirmation = false,
                    Payload = null
                }
            ],
            Prompts =
            [
                new UiTextInputPrompt
                {
                    Id = "player_intent",
                    Prompt = "Что делает персонаж?",
                    Required = true,
                    DefaultValue = string.Empty,
                    Placeholder = "Опишите действие..."
                }
            ],
            Notifications =
            [
                new UiNotification
                {
                    Severity = UiNotificationSeverity.Success,
                    Title = "Команда выполнена",
                    Message = "DTO готов к отображению."
                }
            ],
            InteractiveSession = new UiPromptSession
            {
                SessionId = "prompt_contract",
                SubmitEndpoint = "/api/explorer/prompt-sessions/submit",
                CancelEndpoint = "/api/explorer/prompt-sessions/cancel",
                RequiresLocalUiLock = true,
                OwnerId = "browser:contract",
                ExpiresAtUtc = SampleUtc.AddMinutes(20)
            }
        };

    private static QteWebStateDto BuildQteState() =>
        new()
        {
            State = "NoScene",
            Offer = null,
            ActiveScene = null,
            Resolution = null,
            Completion = null,
            LastResolvedReminder = "Последняя QTE завершена.",
            LastDeclinedQteId = string.Empty,
            AvailableOperations = [],
            Notification = null,
            Error = null
        };

    private static object BuildApiErrorPayload() =>
        new
        {
            error = "Загрузка сохранения заблокирована: активный GM-turn или локальная UI-блокировка.",
            loadedSaveId = "manual:sample.json",
            menu = BuildMainMenu()
        };

    private static BrowserValidationSummaryDto BuildValidationSummary() =>
        new(
            State: "clean",
            StatusLabel: "Состояние валидно",
            IssueCount: 0,
            ErrorCount: 0,
            WarningCount: 0,
            InfoCount: 0,
            DisplayedIssueCount: 0,
            Groups: [],
            Issues: []);

    private static BrowserPendingTurnStatus BuildPendingTurn(bool hasActive) =>
        new(
            HasActiveGmTurn: hasActive,
            Artifacts:
            [
                new BrowserPendingTurnArtifactStatus("Запрос хода GM", "input/turn_request.json", hasActive, "file"),
                new BrowserPendingTurnArtifactStatus("Готов успешный ответ", "ready/turn_complete.json", false, "file")
            ],
            Message: hasActive
                ? "Обнаружен активный ход ГМа или rollback/snapshot artifact."
                : "Активный ход ГМа не обнаружен.");

    private static BrowserLocalUiLockStatus BuildLock(bool exists) =>
        exists
            ? new BrowserLocalUiLockStatus(
                Exists: true,
                IsReadable: true,
                IsStale: false,
                OwnerId: "browser:contract",
                OwnerKind: "browser",
                OwnerLabel: "Contract fixture",
                AcquiredAtUtc: SampleUtc,
                HeartbeatAtUtc: SampleUtc,
                LeaseSeconds: 120,
                LastOperation: "contract fixture")
            : new BrowserLocalUiLockStatus(
                Exists: false,
                IsReadable: false,
                IsStale: false,
                OwnerId: string.Empty,
                OwnerKind: string.Empty,
                OwnerLabel: string.Empty,
                AcquiredAtUtc: null,
                HeartbeatAtUtc: null,
                LeaseSeconds: 0,
                LastOperation: string.Empty);
}
