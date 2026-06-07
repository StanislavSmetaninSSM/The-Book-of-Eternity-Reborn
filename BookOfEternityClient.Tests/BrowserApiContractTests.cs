using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Models.GameState;
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
    private static readonly string[] Issue804CommandAliases =
    [
        "/inv",
        "/npc",
        "/quests",
        "/map",
        "/where_am_i",
        "/factions",
        "/skills",
        "/stats",
        "/distribute",
        "/effects",
        "/combat",
        "/weather",
        "/books",
        "/locations",
        "/transport",
        "/world_news",
        "/soul",
        "/soul_relics",
        "/gacha",
        "/spiritual_arts",
        "/craft",
        "/storage_access"
    ];

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
        Assert.Contains("export interface BrowserAudioSettingsDto", contracts, StringComparison.Ordinal);
        Assert.Contains("export interface BrowserAudioSettingsUpdateRequest", contracts, StringComparison.Ordinal);
        Assert.Contains("export interface BrowserClientSettingsDto", contracts, StringComparison.Ordinal);
        Assert.Contains("export interface BrowserClientSettingsUpdateRequest", contracts, StringComparison.Ordinal);
        Assert.Contains("export interface BrowserCommandCoverageDto", contracts, StringComparison.Ordinal);
        Assert.Contains("export interface BrowserCommandCoverageEntryDto", contracts, StringComparison.Ordinal);
        Assert.Contains("export type BrowserApiResult", contracts, StringComparison.Ordinal);
        Assert.Contains("pending-turn", contracts, StringComparison.Ordinal);
        Assert.Contains("advanced", contracts, StringComparison.OrdinalIgnoreCase);

        var client = File.ReadAllText(clientPath);
        Assert.Contains("export interface BrowserApiClient", client, StringComparison.Ordinal);
        Assert.Contains("getMainMenu", client, StringComparison.Ordinal);
        Assert.Contains("getSessionStatus", client, StringComparison.Ordinal);
        Assert.Contains("getGameScreen", client, StringComparison.Ordinal);
        Assert.Contains("getLifecycleDashboard", client, StringComparison.Ordinal);
        Assert.Contains("getCommandCoverage", client, StringComparison.Ordinal);
        Assert.Contains("validateLifecycle", client, StringComparison.Ordinal);
        Assert.Contains("loadSave", client, StringComparison.Ordinal);
        Assert.Contains("executeExplorerCommand", client, StringComparison.Ordinal);
        Assert.Contains("submitPromptSession", client, StringComparison.Ordinal);
        Assert.Contains("getQteState", client, StringComparison.Ordinal);
        Assert.Contains("resolveQteAction", client, StringComparison.Ordinal);
        Assert.Contains("getAudioSettings", client, StringComparison.Ordinal);
        Assert.Contains("updateAudioSettings", client, StringComparison.Ordinal);
        Assert.Contains("getClientSettings", client, StringComparison.Ordinal);
        Assert.Contains("updateClientSettings", client, StringComparison.Ordinal);
        Assert.Contains("audio-settings", client, StringComparison.Ordinal);
        Assert.Contains("audio-settings-update", client, StringComparison.Ordinal);
        Assert.Contains("client-settings", client, StringComparison.Ordinal);
        Assert.Contains("client-settings-update", client, StringComparison.Ordinal);
        Assert.Contains("command-coverage", client, StringComparison.Ordinal);
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
        Assert.Contains("satisfies BrowserAudioSettingsDto", fixtureChecks, StringComparison.Ordinal);
        Assert.Contains("satisfies BrowserClientSettingsDto", fixtureChecks, StringComparison.Ordinal);
        Assert.Contains("satisfies BrowserCommandCoverageDto", fixtureChecks, StringComparison.Ordinal);
        Assert.Contains("satisfies BrowserApiErrorPayload", fixtureChecks, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontendClientSettingsContracts_ExposeUiScaleAccessibilityFields()
    {
        var contracts = File.ReadAllText(Path.Combine(ApiRoot, "contracts.ts"));
        var fixture = File.ReadAllText(Path.Combine(FixtureRoot, "client-settings.json"));

        Assert.Contains("uiScalePercent: number;", contracts, StringComparison.Ordinal);
        Assert.Contains("browserUiScalePercent?: number | null;", contracts, StringComparison.Ordinal);
        Assert.Contains("\"uiScalePercent\": 100", fixture, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontendClientSettingsUi_ConsumesBrowserUiScaleSignals()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var settingsView = File.ReadAllText(Path.Combine(FrontendRoot, "src", "components", "SettingsView.tsx"));

        Assert.Contains("--browser-ui-scale", app, StringComparison.Ordinal);
        Assert.Contains("uiScalePercent", settingsView, StringComparison.Ordinal);
        Assert.Contains("browserUiScalePercent", settingsView, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontendShell_ConsumesTypedApiContractSummaryInsteadOfHardCodedEndpointList()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var client = File.ReadAllText(Path.Combine(ApiRoot, "client.ts"));
        var diagnostics = File.ReadAllText(Path.Combine(FrontendRoot, "src", "components", "AdvancedDiagnostics.tsx"));

        Assert.Contains("browserApiContractSummary", client, StringComparison.Ordinal);
        Assert.Contains("browserApiContractSummary.endpointDocs", diagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/main-menu", app, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/game-screen", app, StringComparison.Ordinal);
        Assert.DoesNotContain("fetch(", app, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontendShell_RendersLifecyclePhaseMachineWithoutDefaultRawValidationDetails()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var sceneView = File.ReadAllText(Path.Combine(FrontendRoot, "src", "components", "SceneView.tsx"));
        var turnStatePanel = File.ReadAllText(Path.Combine(FrontendRoot, "src", "components", "TurnStatePanel.tsx"));
        var diagnostics = File.ReadAllText(Path.Combine(FrontendRoot, "src", "components", "AdvancedDiagnostics.tsx"));

        Assert.Contains("import { TurnStatePanel } from './TurnStatePanel';", sceneView, StringComparison.Ordinal);
        Assert.Contains("<TurnStatePanel turnState={game.turnState} />", sceneView, StringComparison.Ordinal);
        Assert.Contains("turnState.phase", turnStatePanel, StringComparison.Ordinal);
        Assert.Contains("turnState.playerGuidance", turnStatePanel, StringComparison.Ordinal);
        Assert.Contains("turnState.recommendedActions", turnStatePanel, StringComparison.Ordinal);
        Assert.Contains("turnState.knownPhases", turnStatePanel, StringComparison.Ordinal);
        Assert.Contains("Жизненный цикл хода", turnStatePanel, StringComparison.Ordinal);

        var defaultSources = string.Join('\n', app, sceneView, turnStatePanel);
        Assert.DoesNotContain("validation.issues.map", defaultSources, StringComparison.Ordinal);
        Assert.DoesNotContain("issue.filePath", defaultSources, StringComparison.Ordinal);

        Assert.Contains("function AdvancedDiagnosticsPanel", diagnostics, StringComparison.Ordinal);
        Assert.Contains("lifecycle.validation.groups", diagnostics, StringComparison.Ordinal);
        Assert.Contains("validation.issues.map", diagnostics, StringComparison.Ordinal);
        Assert.Contains("issue.filePath", diagnostics, StringComparison.Ordinal);
        Assert.Contains("issue.repairHint", diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserGameScreenContract_IncludesPlayerCommandActionMenu()
    {
        var screen = BuildGameScreen();
        var json = JsonSerializer.Serialize(screen, WebJsonOptions);

        Assert.NotNull(screen.ActionMenu);
        Assert.Contains("\"actionMenu\"", json, StringComparison.Ordinal);
        Assert.Contains("\"sections\"", json, StringComparison.Ordinal);
        Assert.Contains("\"realmAvailability\"", json, StringComparison.Ordinal);
        Assert.Contains("\"mutationWarning\"", json, StringComparison.Ordinal);
        Assert.Contains("\"formPrompt\"", json, StringComparison.Ordinal);
        Assert.True(
            json.IndexOf("\"actionMenu\"", StringComparison.Ordinal) < json.IndexOf("\"flags\"", StringComparison.Ordinal),
            "BrowserGameScreenDto must serialize actionMenu before flags so frontend fixtures evolve predictably.");

        Assert.Equal(1, screen.ActionMenu.SchemaVersion);
        Assert.Contains(screen.ActionMenu.Sections, section =>
            section.Id == "soul" &&
            section.Label == "Персонаж / Душа" &&
            section.PlayerDefault);
        Assert.Contains(screen.ActionMenu.Sections, section =>
            section.Id == "advanced" &&
            section.Label == "Расширенный режим" &&
            !section.PlayerDefault);
        Assert.Equal("idle", screen.TurnState.Phase);
        Assert.Equal("Можно готовить ход", screen.TurnState.PhaseLabel);
        Assert.Equal("success", screen.TurnState.Severity);
        Assert.Contains(screen.TurnState.RecommendedActions, action => action.Id == "compose-action" && action.Surface == "player-default");
        Assert.Contains(screen.TurnState.KnownPhases, phase => phase.Id == "accepted");
        Assert.Contains(screen.TurnState.KnownPhases, phase => phase.Id == "cancelled");

        var soulAction = FindAction(screen.ActionMenu, "soul");
        Assert.Equal("soul", soulAction.SectionId);
        Assert.Equal("Душа", soulAction.Label);
        Assert.True(soulAction.Enabled);
        Assert.Equal("Открыть раздел", soulAction.FormLabel);
        Assert.Equal("/soul", soulAction.AdvancedCommand);
    }

    [Fact]
    public void BrowserGameScreenContract_IncludesPlayerFacingMediaMapAndGalleryData()
    {
        var screen = BuildGameScreen();
        var json = JsonSerializer.Serialize(screen, WebJsonOptions);

        Assert.NotNull(screen.Media);
        Assert.Equal("mystic road at dusk", screen.Media.SceneImagePrompt);
        var item = Assert.Single(screen.Media.Gallery);
        Assert.Equal("scene-road.png", item.FileName);
        Assert.StartsWith("/api/media/", item.Url, StringComparison.Ordinal);
        Assert.DoesNotContain("E:/", item.Url, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_session", item.Url, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("relativePath", json, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(screen.Media.Map);
        Assert.Equal("Карта смертного мира", screen.Media.Map.Title);
        Assert.Contains(screen.Media.Map.Layers, layer => layer.Id == "world" && layer.IsDefault);
        Assert.Contains(screen.Media.Map.ZLevels, level => level.Z == 0);
        Assert.Contains(screen.Media.Map.Nodes, node => node.IsCurrent);
        var narrativeIndex = json.IndexOf("\"narrative\"", StringComparison.Ordinal);
        var mediaIndex = json.IndexOf("\"media\"", StringComparison.Ordinal);
        var afterlifeIndex = json.IndexOf("\"afterlife\"", StringComparison.Ordinal);
        Assert.True(narrativeIndex >= 0, "BrowserGameScreenDto must serialize narrative for the game screen.");
        Assert.True(mediaIndex >= 0, "BrowserGameScreenDto must serialize media for the game screen.");
        Assert.True(afterlifeIndex >= 0, "BrowserGameScreenDto must serialize afterlife for the game screen.");
        Assert.True(
            narrativeIndex < mediaIndex && mediaIndex < afterlifeIndex,
            "BrowserGameScreenDto must serialize media between narrative and afterlife so frontend fixtures evolve predictably.");
    }

    [Fact]
    public void EveryBrowserExecutableCommand_HasPlayerFacingActionMetadata()
    {
        var menu = BrowserPlayerCommandMenuBuilder.Build(
            BuildRepresentativeState(isChaosSea: false, isShiningAbode: false, isAfterlife: false),
            BuildLifecycleDashboard(),
            BuildQteState());

        var requiredSectionIds = new[]
        {
            "soul", "world", "quests", "map", "factions", "guardians", "afterlife", "combat", "archive", "settings", "advanced"
        };
        foreach (var sectionId in requiredSectionIds)
            Assert.Contains(menu.Sections, section => section.Id == sectionId);

        var actions = menu.Sections.SelectMany(static section => section.Actions).ToArray();
        var actionIds = actions.Select(static action => action.Id).ToArray();
        var executableDescriptors = ExplorerCommandCatalog.Descriptors
            .Where(static descriptor => ExplorerCommandMigrationRegistry.IsBrowserExecutable(descriptor.BrowserStatus))
            .ToArray();

        Assert.Equal(executableDescriptors.Length, actions.Length);
        Assert.Equal(actionIds.Length, actionIds.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var descriptor in executableDescriptors)
        {
            var action = Assert.Single(actions, action => string.Equals(action.Id, descriptor.Id, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(descriptor.MutationMode == ExplorerCommandMutationMode.LocalTurn ? "guided-form" : "none", action.FormMode);
        }

        Assert.All(actions, action =>
        {
            Assert.False(string.IsNullOrWhiteSpace(action.Label));
            Assert.True(ContainsCyrillic(action.Label), $"Action {action.Id} label must be Russian-facing: {action.Label}");
            Assert.False(string.IsNullOrWhiteSpace(action.Description));
            Assert.False(string.IsNullOrWhiteSpace(action.RealmAvailability));
            Assert.False(string.IsNullOrWhiteSpace(action.MutationWarning));
            Assert.False(string.IsNullOrWhiteSpace(action.FormLabel));
            Assert.False(string.IsNullOrWhiteSpace(action.FormPrompt));
            Assert.False(string.IsNullOrWhiteSpace(action.AdvancedCommand));
            Assert.StartsWith("/", action.AdvancedCommand, StringComparison.Ordinal);
            Assert.DoesNotContain('/', action.Label);
            Assert.DoesNotContain("endpoint", action.Label, StringComparison.OrdinalIgnoreCase);
            if (!action.Enabled)
                Assert.False(string.IsNullOrWhiteSpace(action.DisabledReason));
        });
    }

    [Fact]
    public void PlayerDefaultActionMenuCopy_AvoidsTechnicalEnglishJargon()
    {
        var menus = new[]
        {
            BrowserPlayerCommandMenuBuilder.Build(
                BuildRepresentativeState(isChaosSea: false, isShiningAbode: false, isAfterlife: false),
                BuildLifecycleDashboard(),
                BuildQteState()),
            BrowserPlayerCommandMenuBuilder.Build(
                BuildRepresentativeState(isChaosSea: true, isShiningAbode: false, isAfterlife: true),
                BuildLifecycleDashboard(),
                BuildQteState()),
            BrowserPlayerCommandMenuBuilder.Build(
                BuildRepresentativeState(isChaosSea: false, isShiningAbode: true, isAfterlife: true),
                BuildLifecycleDashboard(),
                BuildQteState()),
            BrowserPlayerCommandMenuBuilder.Build(
                BuildRepresentativeState(isChaosSea: false, isShiningAbode: false, isAfterlife: false),
                BuildLifecycleDashboard(),
                BuildQteState("Active"))
        };
        var forbiddenTerms = new[]
        {
            "C#", "DTO", "React", "TypeScript", "endpoint", "runtime", "slash",
            "guided", "pending-turn", "browser write", "local-write", "lifecycle", "QTE", "NPC"
        };

        foreach (var (value, source) in menus.SelectMany(CollectPlayerDefaultMenuText))
        {
            foreach (var forbiddenTerm in forbiddenTerms)
            {
                Assert.False(
                    value.Contains(forbiddenTerm, StringComparison.OrdinalIgnoreCase),
                    $"Player-default action menu text must not expose `{forbiddenTerm}` in {source}: {value}");
            }
        }
    }

    [Fact]
    public void PlayerCommandActionMenu_SeparatesAdvancedAndContextualRealmActions()
    {
        var mortalMenu = BrowserPlayerCommandMenuBuilder.Build(
            BuildRepresentativeState(isChaosSea: false, isShiningAbode: false, isAfterlife: false),
            BuildLifecycleDashboard(),
            BuildQteState());
        var chaosMenu = BrowserPlayerCommandMenuBuilder.Build(
            BuildRepresentativeState(isChaosSea: true, isShiningAbode: false, isAfterlife: true),
            BuildLifecycleDashboard(),
            BuildQteState());
        var shiningMenu = BrowserPlayerCommandMenuBuilder.Build(
            BuildRepresentativeState(isChaosSea: false, isShiningAbode: true, isAfterlife: true),
            BuildLifecycleDashboard(),
            BuildQteState());
        var qteMenu = BrowserPlayerCommandMenuBuilder.Build(
            BuildRepresentativeState(isChaosSea: false, isShiningAbode: false, isAfterlife: false),
            BuildLifecycleDashboard(),
            BuildQteState("Active"));

        Assert.Contains(mortalMenu.Sections, section => section.Id == "world" && section.Label == "Мир" && section.PlayerDefault);
        Assert.Contains(mortalMenu.Sections, section => section.Id == "advanced" && section.Label == "Расширенный режим" && !section.PlayerDefault);

        foreach (var advancedId in new[] { "debug", "gm", "validate", "mods", "system_guardians", "math", "help" })
        {
            var action = FindAction(mortalMenu, advancedId);
            Assert.Equal("advanced", action.SectionId);
            Assert.False(action.PlayerDefault);
        }

        Assert.True(FindAction(mortalMenu, "inventory").Enabled);
        Assert.False(FindAction(chaosMenu, "inventory").Enabled);
        Assert.False(FindAction(mortalMenu, "chaos_sea").Enabled);
        Assert.True(FindAction(chaosMenu, "chaos_sea").Enabled);
        Assert.False(FindAction(mortalMenu, "source_of_light").Enabled);
        Assert.True(FindAction(shiningMenu, "source_of_light").Enabled);
        Assert.True(FindAction(chaosMenu, "afterlife_profiles").Enabled);
        Assert.Contains("активный духовный конфликт", FindAction(shiningMenu, "spiritual_action").DisabledReason, StringComparison.OrdinalIgnoreCase);
        Assert.False(FindAction(qteMenu, "craft").Enabled);
        Assert.Contains("быстрая сцена", FindAction(qteMenu, "craft").DisabledReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "BrowserWebUiParity")]
    public void BrowserCommandCoverageContract_ListsEveryCommandAndUxDecision()
    {
        var coverage = BrowserCommandCoverageService.Build();

        Assert.Equal(2, coverage.SchemaVersion);
        Assert.Equal(ExplorerCommandCatalog.Descriptors.Count, coverage.Commands.Count);
        Assert.Equal(ExplorerCommandCatalog.Descriptors.SelectMany(static descriptor => descriptor.Aliases).Count(), coverage.Summary.AliasCount);
        Assert.Equal(ExplorerCommandCatalog.Descriptors.SelectMany(static descriptor => descriptor.SubcommandDescriptors).Count(), coverage.Summary.SubcommandCount);
        Assert.Equal(
            ExplorerCommandCatalog.Descriptors.Count(static descriptor => ExplorerCommandMigrationRegistry.IsBrowserExecutable(descriptor.BrowserStatus)),
            coverage.Summary.BrowserExecutableCount);

        Assert.All(coverage.Commands, command =>
        {
            Assert.False(string.IsNullOrWhiteSpace(command.Id));
            Assert.NotEmpty(command.Aliases);
            Assert.False(string.IsNullOrWhiteSpace(command.Group));
            Assert.False(string.IsNullOrWhiteSpace(command.MutationMode));
            Assert.False(string.IsNullOrWhiteSpace(command.BrowserStatus));
            Assert.False(string.IsNullOrWhiteSpace(command.HandlerKind));
            Assert.False(string.IsNullOrWhiteSpace(command.UxDecision));
            Assert.False(string.IsNullOrWhiteSpace(command.Surface));
            Assert.False(string.IsNullOrWhiteSpace(command.FormMode));
            Assert.False(string.IsNullOrWhiteSpace(command.PrimaryActionLabel));
            Assert.StartsWith("/", command.PrimaryCommand, StringComparison.Ordinal);
            Assert.All(command.Subcommands, subcommand =>
            {
                Assert.False(string.IsNullOrWhiteSpace(subcommand.Id));
                Assert.NotEmpty(subcommand.Aliases);
                Assert.StartsWith("/", subcommand.CanonicalCommand, StringComparison.Ordinal);
                Assert.False(string.IsNullOrWhiteSpace(subcommand.Group));
                Assert.False(string.IsNullOrWhiteSpace(subcommand.MutationMode));
                Assert.False(string.IsNullOrWhiteSpace(subcommand.BrowserStatus));
                Assert.False(string.IsNullOrWhiteSpace(subcommand.HandlerKind));
                Assert.False(string.IsNullOrWhiteSpace(subcommand.UxDecision));
                Assert.False(string.IsNullOrWhiteSpace(subcommand.Surface));
                Assert.False(string.IsNullOrWhiteSpace(subcommand.FormMode));
                Assert.False(string.IsNullOrWhiteSpace(subcommand.PrimaryActionLabel));
                Assert.StartsWith("/", subcommand.PrimaryCommand, StringComparison.Ordinal);
                Assert.Equal(subcommand.CanonicalCommand, subcommand.PrimaryCommand);
            });
        });

        var saref = Assert.Single(coverage.Commands, command => command.Id == "saref_story");
        Assert.Equal("player-default", saref.Surface);
        Assert.Equal("contextual-button", saref.UxDecision);
        Assert.Contains(saref.Subcommands, subcommand =>
            subcommand.Id == "find_wings" &&
            subcommand.MutationMode == nameof(ExplorerCommandMutationMode.LocalTurn) &&
            subcommand.BrowserStatus == nameof(ExplorerCommandMigrationStatus.MutatingParity) &&
            subcommand.FormMode == "guided-form" &&
            subcommand.PrimaryCommand == subcommand.CanonicalCommand &&
            subcommand.UxDecision == "guided-form");

        foreach (var advancedId in new[] { "debug", "gm", "validate", "mods", "system_guardians", "math", "help" })
            Assert.Contains(coverage.Commands, command => command.Id == advancedId && command.Surface == "advanced-only" && command.UxDecision == "advanced-diagnostics");

        Assert.DoesNotContain(coverage.Commands, command => command.Surface == "player-default" && command.Id is "debug" or "gm" or "validate" or "math" or "help");
        Assert.All(
            coverage.Commands.Where(static command => command.Surface == "player-default" && ExplorerCommandMigrationRegistry.IsBrowserExecutable(Enum.Parse<ExplorerCommandMigrationStatus>(command.BrowserStatus))),
            command => Assert.NotEqual("advanced-diagnostics", command.UxDecision));
    }

    [Fact]
    [Trait("Category", "BrowserWebUiParity")]
    public void BrowserCommandCoverageContract_AuditsIssue804CommandsWithEvidence()
    {
        var root = JsonNode.Parse(JsonSerializer.Serialize(BrowserCommandCoverageService.Build(), WebJsonOptions))!.AsObject();
        var commands = root["commands"]!.AsArray();

        foreach (var alias in Issue804CommandAliases)
        {
            var command = Assert.Single(commands, node => JsonArrayContains(node!["aliases"]!.AsArray(), alias))!.AsObject();
            AssertRequiredAuditFields(command, $"command {alias}");
        }

        var gacha = Assert.Single(commands, node => JsonArrayContains(node!["aliases"]!.AsArray(), "/gacha"))!.AsObject();
        Assert.Equal("covered", RequiredString(gacha, "auditStatus", "command /gacha"));
        Assert.DoesNotContain("#803", RequiredString(gacha, "followUpIssue", "command /gacha"), StringComparison.Ordinal);
        Assert.Equal("No tracked browser parity gap for the audited command scope.", RequiredString(gacha, "gapSummary", "command /gacha"));
        Assert.Contains("prompt flow", RequiredString(gacha, "parityNotes", "command /gacha"), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", RequiredString(gacha, "gapSummary", "command /gacha"), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".json", RequiredString(gacha, "gapSummary", "command /gacha"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "BrowserWebUiParity")]
    public void BrowserCommandCoverageContract_AuditMetadataIsCompleteForEveryCommandAndSubcommand()
    {
        var root = JsonNode.Parse(JsonSerializer.Serialize(BrowserCommandCoverageService.Build(), WebJsonOptions))!.AsObject();
        var commands = root["commands"]!.AsArray();
        var validStatuses = new HashSet<string>(StringComparer.Ordinal)
        {
            "covered",
            "tracked-follow-up",
            "advanced-only",
            "blocked"
        };

        foreach (var commandNode in commands)
        {
            var command = commandNode!.AsObject();
            var commandContext = $"command {RequiredString(command, "id", "command")}";
            Assert.Contains(RequiredString(command, "auditStatus", commandContext), validStatuses);
            AssertRequiredAuditFields(command, commandContext);

            foreach (var subcommandNode in command["subcommands"]!.AsArray())
            {
                var subcommand = subcommandNode!.AsObject();
                var subcommandContext = $"{commandContext} subcommand {RequiredString(subcommand, "id", commandContext)}";
                Assert.Contains(RequiredString(subcommand, "auditStatus", subcommandContext), validStatuses);
                AssertRequiredAuditFields(subcommand, subcommandContext);
                Assert.False(string.IsNullOrWhiteSpace(RequiredString(subcommand, "group", subcommandContext)));
                Assert.False(string.IsNullOrWhiteSpace(RequiredString(subcommand, "mutationMode", subcommandContext)));
                Assert.False(string.IsNullOrWhiteSpace(RequiredString(subcommand, "handlerKind", subcommandContext)));
                Assert.False(string.IsNullOrWhiteSpace(RequiredString(subcommand, "surface", subcommandContext)));
                Assert.False(string.IsNullOrWhiteSpace(RequiredString(subcommand, "formMode", subcommandContext)));
                Assert.False(string.IsNullOrWhiteSpace(RequiredString(subcommand, "primaryActionLabel", subcommandContext)));
                Assert.StartsWith("/", RequiredString(subcommand, "primaryCommand", subcommandContext), StringComparison.Ordinal);
            }
        }
    }

    public static IEnumerable<object[]> ContractFixtures()
    {
        yield return ["main-menu.json", BuildMainMenu()];
        yield return ["session-status.json", BuildSessionStatus()];
        yield return ["game-screen.json", BuildGameScreen()];
        yield return ["lifecycle-dashboard.json", BuildLifecycleDashboard()];
        yield return ["explorer-command-result.json", BuildExplorerCommandResult()];
        yield return ["qte-state.json", BuildQteState()];
        yield return ["audio-settings.json", BuildAudioSettings()];
        yield return ["client-settings.json", BuildClientSettings()];
        yield return ["command-coverage.json", BuildCommandCoverage()];
        yield return ["api-error.json", BuildApiErrorPayload()];
    }

    private static BrowserCommandCoverageDto BuildCommandCoverage() =>
        BrowserCommandCoverageService.Build();

    private static BrowserClientSettingsDto BuildClientSettings() =>
        new(
            SchemaVersion: 1,
            Language: new BrowserSettingsChoiceGroupDto(
                Value: "ru",
                Label: "Русский",
                Choices:
                [
                    new BrowserSettingsChoiceDto("ru", "Русский", "Основной язык текущих игровых подсказок."),
                    new BrowserSettingsChoiceDto("en", "English", "English client labels where supported.")
                ]),
            Difficulty: new BrowserSettingsChoiceGroupDto(
                Value: "normal",
                Label: "Обычная",
                Choices:
                [
                    new BrowserSettingsChoiceDto("normal", "Обычная", "Базовый уровень сложности."),
                    new BrowserSettingsChoiceDto("hard", "Сложно", "Более опасные проверки и конфликты."),
                    new BrowserSettingsChoiceDto("impossible", "Невозможно", "Предельная сложность для рискованного прохождения.")
                ]),
            ShowGmThoughts: false,
            Audio: new BrowserClientAudioSettingsDto(
                MusicEnabled: true,
                MusicVolume: 65,
                SoundEnabled: true,
                SoundVolume: 75),
            Accessibility: new BrowserClientAccessibilitySettingsDto(
                FontScalePercent: 100,
                UiScalePercent: 100,
                ReducedMotion: false,
                ContrastFriendly: false),
            Locality: new BrowserClientLocalityDto(
                LocalhostOnly: true,
                SessionLabel: "Текущая глава книги",
                GameSessionExists: true,
                GmBridgeEnabled: true,
                GmBridgeLabel: "Локальный мост ГМа включён",
                SafetySummary: "Книга открыта только на этом устройстве и хранит настройки вместе с вашим прохождением."));

    private static BrowserAudioSettingsDto BuildAudioSettings() =>
        new(
            SchemaVersion: 1,
            MusicEnabled: true,
            MusicVolume: 65,
            SoundEnabled: true,
            SoundVolume: 75,
            AutoplayGuidance: "Звук запускается после вашего нажатия: нажмите «Включить музыку», чтобы разрешить музыку и звуковые подсказки для этой вкладки.",
            MissingAssetsMessage: string.Empty,
            Playlists:
            [
                new BrowserAudioPlaylistDto(
                    Id: "main-menu",
                    Label: "Главное меню",
                    Usage: "Тихая тема книги до входа в активную сцену.",
                    Available: true,
                    Tracks:
                    [
                        new BrowserAudioAssetDto(
                            Id: "music:main-menu:Main Theme.mp3",
                            Label: "Main Theme",
                            Url: "/api/audio/assets/music%3Amain-menu%3AMain%20Theme.mp3",
                            ContentType: "audio/mpeg")
                    ]),
                new BrowserAudioPlaylistDto(
                    Id: "in-game",
                    Label: "Игра",
                    Usage: "Фоновая музыка для текущей сцены и переходов между мирами.",
                    Available: false,
                    Tracks: [])
            ],
            Cues:
            [
                new BrowserAudioCueDto(
                    Id: "turn-ready",
                    Label: "Ответ ГМа готов",
                    Usage: "Уведомление, что ход принят или готов к чтению.",
                    Available: true,
                    Asset: new BrowserAudioAssetDto(
                        Id: "cue:turn-ready:sound-notification.wav",
                        Label: "sound-notification",
                        Url: "/api/audio/assets/cue%3Aturn-ready%3Asound-notification.wav",
                        ContentType: "audio/wav"))
            ]);

    private static BrowserMainMenuDto BuildMainMenu() =>
        new(
            SchemaVersion: 1,
            Session: new BrowserMainMenuSessionDto(
                GameSessionExists: true,
                HasReadableSoul: true,
                CanContinue: true,
                ContinueReason: "Текущую главу можно продолжить.",
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
                Guidance: "Настройки книги, звука и доступности открываются в отдельном разделе."),
            About: new BrowserAboutDto(
                Title: "Книга Вечности: Перерождение",
                Body: "Книга Вечности: Перерождение открывает текущую главу, сохранения и настройки в одном локальном окне."),
            AdvancedShell: new BrowserAdvancedShellDto(
                Label: "Расширенный режим",
                Description: "Служебные сведения и перенесённые команды скрыты от обычного главного меню.",
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
                CombatLog: "Боевых событий нет.",
                ImagePrompt: "mystic road at dusk"),
            Media: BuildMedia(),
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
                ValidationLabel: "Состояние валидно",
                Phase: "idle",
                PhaseLabel: "Можно готовить ход",
                Severity: "success",
                PlayerGuidance: "Игра не ждёт ГМа; можно подготовить следующий художественный ход.",
                RecommendedActions:
                [
                    new BrowserGameScreenTurnActionDto(
                        Id: "compose-action",
                        Label: "Подготовить действие",
                        Description: "Заполните основной художественный ввод и подтвердите действие, когда запись хода будет подключена.",
                        Surface: "player-default",
                        Enabled: true,
                        DisabledReason: string.Empty)
                ],
                KnownPhases: BrowserGameScreenTurnStateDto.KnownPhaseCatalog),
            ActionComposer: new BrowserGameScreenActionComposerDto(
                CanSubmit: true,
                Mode: "prose",
                Placeholder: "Опишите действие персонажа...",
                Guidance: "Slash-команды открываются только через расширенный режим.",
                DisabledReason: string.Empty),
            Qte: BuildQteState(),
            ActionMenu: BrowserPlayerCommandMenuBuilder.Build(
                BuildRepresentativeState(isChaosSea: false, isShiningAbode: false, isAfterlife: false),
                BuildLifecycleDashboard(),
                BuildQteState()),
            Flags: new BrowserGameScreenFlagsDto(
                IsInChaosSea: false,
                IsInAnyShiningAbodeState: false,
                IsInShiningAbode: false,
                IsInShiningAbodePendingBootstrap: false,
                IsInAfterlifeRealm: false,
                CanReenterShiningAbode: true));

    private static BrowserGameScreenMediaDto BuildMedia() =>
        new(
            SchemaVersion: 1,
            SceneImagePrompt: "mystic road at dusk",
            Gallery:
            [
                new BrowserGameScreenMediaItemDto(
                    MediaId: "images-scenes-scene-road",
                    Url: "/api/media/images-scenes-scene-road",
                    FileName: "scene-road.png",
                    ContentType: "image/png",
                    Length: 2048,
                    ModifiedAtUtc: SampleUtc)
            ],
            Map: new MapViewDto
            {
                Realm = "Mortal World",
                Title = "Карта смертного мира",
                CurrentNodeId = "ash-road",
                Layers = [new MapLayerDto { Id = "world", Label = "Мир", IsDefault = true }],
                ZLevels = [new MapZLevelDto { Z = 0, Label = "земля" }],
                Nodes =
                [
                    new MapNodeDto
                    {
                        Id = "ash-road",
                        Label = "Пепельная дорога",
                        Type = "current",
                        X = 0,
                        Y = 0,
                        Z = 0,
                        Layer = "world",
                        IsCurrent = true,
                        OwnerFactionId = "",
                        OwnerFactionName = "",
                        Influence = new Dictionary<string, int>(),
                        Details = [new MapDetailItemDto { Key = "Время", Value = "Сумерки" }]
                    }
                ],
                Links = [],
                Regions = []
            });

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

    private static QteWebStateDto BuildQteState(string state = "NoScene") =>
        new()
        {
            State = state,
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

    private static BrowserPlayerCommandActionDto FindAction(BrowserPlayerCommandMenuDto menu, string id) =>
        menu.Sections
            .SelectMany(static section => section.Actions)
            .Single(action => string.Equals(action.Id, id, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<(string Value, string Source)> CollectPlayerDefaultMenuText(BrowserPlayerCommandMenuDto menu)
    {
        foreach (var section in menu.Sections.Where(static section => section.PlayerDefault))
        {
            yield return (section.Label, $"section {section.Id} label");
            yield return (section.Description, $"section {section.Id} description");

            foreach (var action in section.Actions.Where(static action => action.PlayerDefault))
            {
                yield return (action.Label, $"action {action.Id} label");
                yield return (action.Description, $"action {action.Id} description");
                yield return (action.RealmAvailability, $"action {action.Id} realm availability");
                yield return (action.MutationWarning, $"action {action.Id} mutation warning");
                yield return (action.FormLabel, $"action {action.Id} form label");
                yield return (action.FormPrompt, $"action {action.Id} form prompt");
                if (!string.IsNullOrWhiteSpace(action.DisabledReason))
                    yield return (action.DisabledReason, $"action {action.Id} disabled reason");
            }
        }
    }

    private static void AssertRequiredAuditFields(JsonObject node, string context)
    {
        foreach (var field in new[]
                 {
                     "auditStatus",
                     "sampleDataStatus",
                     "browserEvidence",
                     "consoleEvidence",
                     "parityNotes",
                     "readabilityNotes",
                     "gapSummary"
                 })
        {
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(node, field, context)));
        }
    }

    private static bool JsonArrayContains(JsonArray values, string expected) =>
        values.Any(value => string.Equals(value?.GetValue<string>(), expected, StringComparison.OrdinalIgnoreCase));

    private static string RequiredString(JsonObject node, string field, string context)
    {
        Assert.True(node.TryGetPropertyValue(field, out var value), $"{context} is missing {field}.");
        Assert.NotNull(value);
        return value!.GetValue<string>();
    }

    private static AggregatedGameState BuildRepresentativeState(bool isChaosSea, bool isShiningAbode, bool isAfterlife) =>
        new()
        {
            SoulName = "Арион",
            CharacterName = "Арион",
            CharacterClass = "Следопыт",
            CharacterRace = "Человек",
            CurrentRealm = isShiningAbode
                ? "Shining Abode"
                : isChaosSea || isAfterlife
                    ? "Chaos Sea"
                    : "Mortal World",
            CurrentLocation = isShiningAbode ? "Зал Света" : isChaosSea ? "Море Хаоса" : "Пепельная дорога",
            WorldTime = "Сумерки",
            SessionId = "contract",
            ShiningAbodeAvailability = isChaosSea ? "active" : string.Empty,
            PlayerStatus = new PlayerStatusState
            {
                CurrentCondition = "Собран",
                HealthPercentage = "90%",
                EnergyPercentage = "75%",
                PoisePercentage = "60%",
                ActiveConditions = ["Сосредоточенность"]
            }
        };

    private static bool ContainsCyrillic(string value) =>
        value.Any(character => character is >= '\u0400' and <= '\u04FF');

    private static object BuildApiErrorPayload() =>
        new
        {
            error = "Загрузка сохранения сейчас недоступна: книга занята текущим ходом.",
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
