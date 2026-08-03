using System.Text.RegularExpressions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerModeSourceGuardTests
{
    private static readonly string[] PrivateFactionMaterializationTokens =
    {
        "materializationId",
        "schemaVersion",
        "empty_by_design",
        "materializedAtTurn"
    };

    private static string ReadGameEngineSource()
    {
        var rootFile = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var partialDir = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine");

        var files = new List<string> { rootFile };
        if (Directory.Exists(partialDir))
            files.AddRange(Directory.GetFiles(partialDir, "*.cs", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase));

        return string.Join(Environment.NewLine + Environment.NewLine, files.Select(File.ReadAllText));
    }

    private static string ReadExplorerModeSource()
    {
        var rootFile = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "UI", "ExplorerMode.cs");
        var partialDir = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "UI", "ExplorerMode");

        var files = new List<string> { rootFile };
        if (Directory.Exists(partialDir))
            files.AddRange(Directory.GetFiles(partialDir, "*.cs", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase));

        return string.Join(Environment.NewLine + Environment.NewLine, files.Select(File.ReadAllText));
    }

    private static string ReadUiSourceFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "UI", relativePath));
    }

    private static string ExtractMethodSource(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find method signature '{signature}'.");

        var openBrace = source.IndexOf('{', start);
        Assert.True(openBrace >= 0, $"Could not find method body for '{signature}'.");

        var depth = 0;
        for (var index = openBrace; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}')
                depth--;

            if (depth == 0)
                return source[start..(index + 1)];
        }

        Assert.Fail($"Could not extract method body for '{signature}'.");
        return string.Empty;
    }

    [Fact]
    public void ExplorerMode_MustUseConsoleAdapterInsteadOfDirectAnsiConsoleCalls()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("AnsiConsole.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Console.ReadKey(true)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_MapCommand_MustUseVisualMapViewerInsteadOfLocationSelector()
    {
        var source = ReadUiSourceFile(Path.Combine("ExplorerMode", "ExplorerMode.WorldAndStatus.cs"));
        var showMap = ExtractMethodSource(source, "private async Task ShowMap()");

        Assert.Contains("LocalMapViewService.BuildCurrentRealmMapAsync(_fs)", showMap, StringComparison.Ordinal);
        Assert.Contains("new UiMapBlock", showMap, StringComparison.Ordinal);
        Assert.Contains("LocalMapViewerLauncher.WriteAndOpenAsync(_fs, map)", showMap, StringComparison.Ordinal);
        Assert.DoesNotContain("new SelectionPrompt<string>()", showMap, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowLocationDetailPanel", showMap, StringComparison.Ordinal);
        Assert.DoesNotContain("current location + adjacent + discovered", showMap, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExplorerMode_LocationsCommand_MustOwnCurrentAdjacentAndDiscoveredDetailsFlow()
    {
        var source = ReadUiSourceFile(Path.Combine("ExplorerMode", "ExplorerMode.MetaLoreAndTravel.cs"));
        var showLocations = ExtractMethodSource(source, "private async Task ShowLocations()");

        Assert.Contains("game_state/world/current_location.json", showLocations, StringComparison.Ordinal);
        Assert.Contains("game_state/world/world_map.json", showLocations, StringComparison.Ordinal);
        Assert.Contains("adjacencyMap", showLocations, StringComparison.Ordinal);
        Assert.Contains("EnumerateWorldMapLocations(mapDoc, \"newLocations\")", showLocations, StringComparison.Ordinal);
        Assert.Contains("EnumerateWorldMapLocations(mapDoc, \"locationUpdates\")", showLocations, StringComparison.Ordinal);
        Assert.Contains("worldMapUpdates", source, StringComparison.Ordinal);
        Assert.Contains("new SelectionPrompt<string>()", showLocations, StringComparison.Ordinal);
        Assert.Contains("ShowLocationDetailPanel", showLocations, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_LocationDetailPanel_MustUseSharedLocationNameFallbacks()
    {
        var source = ReadUiSourceFile(Path.Combine("ExplorerMode", "ExplorerMode.WorldAndStatus.cs"));
        var detailPanel = ExtractMethodSource(source, "private async Task ShowLocationDetailPanel(JsonElement loc, bool isCurrent)");

        Assert.Contains("GetLocationName(loc)", detailPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("GetStr(loc, \"name\", GetStr(loc, \"targetLocationId\", \"Неизвестно\"))", detailPanel, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerRollbackBackups_MustLiveOutsideClearedCurrentWorldLore()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("game_state/control/explorer_local_turn_rollback", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".explorer.rollback.{DateTime.UtcNow.Ticks}", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("BookOfEternityClient/UI/ExplorerMode.cs")]
    [InlineData("BookOfEternityClient/Core/GameEngine.cs")]
    public void DynamicJoinedMarkupBlocks_MustUseSafeMarkup(string relativePath)
    {
        var source = string.Equals(relativePath, "BookOfEternityClient/UI/ExplorerMode.cs", StringComparison.OrdinalIgnoreCase)
            ? ReadExplorerModeSource()
            : ReadGameEngineSource();

        Assert.DoesNotContain("new Markup(string.Join(\"\\n\", ", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsoleDynamicPlainText_MustUseCentralEscapingHelpersAtSpectreBoundaries()
    {
        var gameInterface = ReadUiSourceFile("GameInterface.cs");
        var consoleLayout = ReadUiSourceFile("ConsoleLayout.cs");
        var renderer = ReadUiSourceFile("ExplorerCommandResultConsoleRenderer.cs");
        var explorerMode = ReadExplorerModeSource();

        Assert.Contains("SafeMarkupText", gameInterface, StringComparison.Ordinal);
        Assert.Contains("SafePanelHeader", gameInterface, StringComparison.Ordinal);
        Assert.Contains("SafePromptChoice", gameInterface, StringComparison.Ordinal);
        Assert.Contains("GameInterface.SafePromptChoice(parts)", consoleLayout, StringComparison.Ordinal);
        Assert.Contains("GameInterface.SafeMarkupText(cell)", renderer, StringComparison.Ordinal);
        Assert.Contains(
            "table.AddRow(cells.Length == 0 ? [GameInterface.SafeMarkupText(string.Empty)] : cells);",
            renderer,
            StringComparison.Ordinal);
        Assert.Contains("GameInterface.SafeMarkupText(action.Label)", renderer, StringComparison.Ordinal);
        Assert.Contains("GameInterface.SafeMarkupText(prompt.Prompt)", renderer, StringComparison.Ordinal);
        Assert.Contains("GameInterface.SafeMarkupText(notification.Message)", renderer, StringComparison.Ordinal);

        Assert.DoesNotContain("new Markup(Markup.Escape", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("new PanelHeader($\" {Markup.Escape", renderer, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"new\s+Markup\s*\(\s*(?:\$@|@\$|\$)", renderer);
        Assert.DoesNotMatch(@"new\s+PanelHeader\s*\(\s*(?:\$@|@\$|\$)", renderer);
        Assert.DoesNotContain("new Markup(Markup.Escape", explorerMode, StringComparison.Ordinal);
        Assert.DoesNotContain("new PanelHeader($\" {Markup.Escape", explorerMode, StringComparison.Ordinal);
        Assert.DoesNotContain("new Markup($\"[dim]{message}[/]\")", explorerMode, StringComparison.Ordinal);
        Assert.DoesNotContain("new PanelHeader($\" {title} \", Justify.Center)", explorerMode, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_DynamicBracketLabels_MustEscapeLiteralSquareBrackets()
    {
        var explorerMode = ReadExplorerModeSource();

        Assert.DoesNotMatch(@"\[[^\]]+\]\[\{Markup\.Escape\(", explorerMode);
    }

    [Fact]
    public void GameInterface_MortalHudBars_MustUseFixedMetricColumnsForPercentAlignment()
    {
        var source = ReadUiSourceFile("GameInterface.cs");
        var statusBarStart = source.IndexOf("public void RenderStatusBar", StringComparison.Ordinal);
        var afterlifeStart = source.IndexOf("private void RenderAfterlifeStatus", StringComparison.Ordinal);
        Assert.True(statusBarStart >= 0, "RenderStatusBar source must be present.");
        Assert.True(afterlifeStart > statusBarStart, "RenderStatusBar block must end before RenderAfterlifeStatus.");

        var statusBarSource = source[statusBarStart..afterlifeStart];

        Assert.Contains(
            "ConsoleLayout.CreateBarMetricTable(labelWidth: 16, barWidth: 22, valueWidth: 6)",
            statusBarSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".AddColumn(new TableColumn(\"\").NoWrap())",
            statusBarSource,
            StringComparison.Ordinal);
        Assert.Equal(3, statusBarSource.Split("new Markup(string.Empty)", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void ConsoleLayout_BarMetricTable_MustKeepMetricColumnsBounded()
    {
        var source = ReadUiSourceFile("ConsoleLayout.cs");
        var metricTableSource = ExtractMethodSource(source, "public static Table CreateBarMetricTable");

        Assert.DoesNotContain(".Expand()", metricTableSource, StringComparison.Ordinal);

        var labelColumn = ".AddColumn(new TableColumn(\"\").NoWrap().Width(labelWidth))";
        var barColumn = ".AddColumn(new TableColumn(\"\").NoWrap().Width(barWidth))";
        var valueColumn = ".AddColumn(new TableColumn(\"\").RightAligned().NoWrap().Width(valueWidth))";
        var trailingColumn = ".AddColumn(new TableColumn(\"\"));";

        var labelIndex = metricTableSource.IndexOf(labelColumn, StringComparison.Ordinal);
        var barIndex = metricTableSource.IndexOf(barColumn, StringComparison.Ordinal);
        var valueIndex = metricTableSource.IndexOf(valueColumn, StringComparison.Ordinal);
        var trailingIndex = metricTableSource.IndexOf(trailingColumn, StringComparison.Ordinal);

        Assert.True(labelIndex >= 0, "Metric tables must keep a fixed label column.");
        Assert.True(barIndex > labelIndex, "Metric tables must keep a fixed bar column after the label.");
        Assert.True(valueIndex > barIndex, "Metric tables must keep a fixed, right-aligned value column after the bar.");
        Assert.True(trailingIndex > valueIndex, "Any flexible/trailing text must come after the fixed metric group.");
    }

    [Fact]
    public void GameInterface_MortalHudBars_MustKeepEmojiWidthOutOfMetricLabels()
    {
        var source = ReadUiSourceFile("GameInterface.cs");
        var statusBarStart = source.IndexOf("public void RenderStatusBar", StringComparison.Ordinal);
        var afterlifeStart = source.IndexOf("private void RenderAfterlifeStatus", StringComparison.Ordinal);
        Assert.True(statusBarStart >= 0, "RenderStatusBar source must be present.");
        Assert.True(afterlifeStart > statusBarStart, "RenderStatusBar block must end before RenderAfterlifeStatus.");

        var statusBarSource = source[statusBarStart..afterlifeStart];

        Assert.Contains("new Markup($\"[{healthColor}]Здоровье[/]\")", statusBarSource, StringComparison.Ordinal);
        Assert.Contains("new Markup($\"[{energyColor}]Энергия[/]\")", statusBarSource, StringComparison.Ordinal);
        Assert.Contains("new Markup($\"[{poiseColor}]Равновесие[/]\")", statusBarSource, StringComparison.Ordinal);

        Assert.DoesNotContain("❤️ Здоровье", statusBarSource, StringComparison.Ordinal);
        Assert.DoesNotContain("⚡ Энергия", statusBarSource, StringComparison.Ordinal);
        Assert.DoesNotContain("🛡️ Равновесие", statusBarSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_DetailedStatusMetricTables_MustUseSharedBarMetricHelper()
    {
        var source = ReadUiSourceFile(Path.Combine("ExplorerMode", "ExplorerMode.WorldAndStatus.cs"));
        var statusSource = ExtractMethodSource(source, "private async Task ShowDetailedStatus()");

        Assert.Contains(
            "var summaryTable = ConsoleLayout.CreateBarMetricTable(labelWidth: 18, barWidth: 18, valueWidth: 18);",
            statusSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "var stealthTable = ConsoleLayout.CreateBarMetricTable(labelWidth: 18, barWidth: 18, valueWidth: 18);",
            statusSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("var summaryTable = new Table()", statusSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var stealthTable = new Table()", statusSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_FactionDetail_MustUseSharedMetricTablesForAlignedColumns()
    {
        var source = ReadUiSourceFile(Path.Combine("ExplorerMode", "ExplorerMode.FactionsAndWorldNews.cs"));
        var detailPanel = ExtractMethodSource(source, "private async Task ShowFactionDetailPanel(JsonElement f, JsonDocument? projDoc");

        Assert.Contains(
            "var summaryTable = ConsoleLayout.CreateBarMetricTable(labelWidth: 24, barWidth: 18, valueWidth: 16);",
            detailPanel,
            StringComparison.Ordinal);
        Assert.Contains(
            "var powerTable = ConsoleLayout.CreateBarMetricTable(labelWidth: 18, barWidth: 10, valueWidth: 5);",
            detailPanel,
            StringComparison.Ordinal);
        Assert.Contains("new Markup(\"[cyan]Прогресс развития[/]\")", detailPanel, StringComparison.Ordinal);
        Assert.Contains("powerTable.AddRow(", detailPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("ConsoleLayout.CreateInfoTable()", detailPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("var progressTable = ConsoleLayout.CreateBarMetricTable();", detailPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("lines.Add($\"    {Markup.Escape(label)}: {PowerBar(val)}", detailPanel, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_FactionDrilldownSections_MustStayReadOnlyAndPlayerFacing()
    {
        var source = ReadUiSourceFile(Path.Combine("ExplorerMode", "ExplorerMode.FactionsAndWorldNews.cs"));

        Assert.Contains("ShowFactionDetailSectionMenu", source, StringComparison.Ordinal);
        Assert.Contains("BuildFactionDetailSections", source, StringComparison.Ordinal);
        Assert.Contains("IsFactionKnowledgeEntryVisible", source, StringComparison.Ordinal);
        Assert.Contains("GameInterface.SafePromptChoice(section.ChoiceLabel)", source, StringComparison.Ordinal);
        Assert.Contains("GameInterface.SafeMarkup(string.Join", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteJsonAuditPanel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Raw(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_fs.WriteFileAtomicAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Полный JSON", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_KnownDynamicPromptAndMarkupLineSurfaces_MustEscapePlainText()
    {
        var rootSource = ReadUiSourceFile("ExplorerMode.cs");
        var inventorySource = ReadUiSourceFile(Path.Combine("ExplorerMode", "ExplorerMode.Inventory.cs"));

        Assert.DoesNotContain(
            "MarkupLine($\"[yellow]⚠️ {parsedCommand.ErrorTitle}: {parsedCommand.ErrorMessage}[/]\");",
            rootSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("choices.Add($\"⚔ {slotLabel}: {itemName}\");", inventorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("choices.Insert(infoPrefixCount, $\"💎 {rp.Name}: {rv}\");", inventorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("choices.Add($\"📦 {sName} ({contCount} пр.) → управление\");", inventorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("choices.Add($\"📦 🔒 {sName} ({contCount} пр.)\");", inventorySource, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_DynamicSelectionPromptChoices_MustBeEscapedBeforeAddChoices()
    {
        var inventorySource = ReadUiSourceFile(Path.Combine("ExplorerMode", "ExplorerMode.Inventory.cs"));
        var consoleLayout = ReadUiSourceFile("ConsoleLayout.cs");

        Assert.Contains("PlainChoiceLabel", consoleLayout, StringComparison.Ordinal);
        Assert.Contains("GameInterface.SafePromptChoice(parts)", consoleLayout, StringComparison.Ordinal);
        Assert.Contains(".AddChoices(choices));", inventorySource, StringComparison.Ordinal);
        Assert.Contains("choices.Add(GameInterface.SafePromptChoice($\"⚔ {slotLabel}: {itemName}\"));", inventorySource, StringComparison.Ordinal);
        Assert.Contains("choices.Insert(infoPrefixCount, GameInterface.SafePromptChoice($\"💎 {rp.Name}: {rv}\"));", inventorySource, StringComparison.Ordinal);
        Assert.Contains("choices.Add(GameInterface.SafePromptChoice($\"📦 {sName} ({contCount} пр.) → управление\"));", inventorySource, StringComparison.Ordinal);
        Assert.Contains("choices.Add(GameInterface.SafePromptChoice($\"📦 🔒 {sName} ({contCount} пр.)\"));", inventorySource, StringComparison.Ordinal);
        Assert.Contains("choices.AddRange(MakeUniqueChoiceLabels(inventoryChoiceEntries));", inventorySource, StringComparison.Ordinal);
    }

    [Fact]
    public void LargeWorldDescriptionEditor_MustNotHaveDedicatedClipboardActionOrLegacyDoneSentinel()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("📋 Вставить из буфера обмена", source, StringComparison.Ordinal);
        Assert.DoesNotContain("::done", source, StringComparison.Ordinal);
        Assert.Contains("TextComposer.Read(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_MustUseSharedReputationDisplayInsteadOfLocalTierHelpers()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("private static (string label, string color) GetNpcRelationshipTier", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static (string label, string color) GetReputationLabel", source, StringComparison.Ordinal);
        Assert.Contains("ReputationDisplay.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_MustUseSharedNpcTradeAvailabilityInsteadOfLocalTradeGateHelpers()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("private static bool NpcTradeAvailableHere", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string? GetNpcTradeBlockedReason", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string? GetNpcMerchantProfile(JsonElement", source, StringComparison.Ordinal);
        Assert.Contains("NpcTradeService.EvaluateTradeAvailability", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_GuardianUi_MustUseSharedManifestationResolver()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("GuardianManifestation.GetDisplayName", source, StringComparison.Ordinal);
        Assert.Contains("GuardianManifestation.GetCanonicalName", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetStr(g, \"name\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_GuardianUi_MustMarkActiveGuardianAndUseDerivedTradeSlotCount()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("АКТИВНЫЙ", source, StringComparison.Ordinal);
        Assert.Contains("Текущий активный Хранитель", source, StringComparison.Ordinal);
        Assert.Contains("· активный", source, StringComparison.Ordinal);
        Assert.Contains("derivedState.TradeSlotCount} локальных слотов", source, StringComparison.Ordinal);
        Assert.DoesNotContain("4 локальных слота", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_GuardianProjectAndAbodePower_UiMustExposeFullGuardianJournalAndPlayerFacingVocabulary()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("Показать весь журнал Хранителя", source, StringComparison.Ordinal);
        Assert.Contains("Полный журнал Хранителя", source, StringComparison.Ordinal);
        Assert.Contains("Категория проекта", source, StringComparison.Ordinal);
        Assert.Contains("Характер проекта", source, StringComparison.Ordinal);
        Assert.Contains("Что даёт текущая сила", source, StringComparison.Ordinal);
        Assert.Contains("Предел враждебного давления", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Pressure: [orange1]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Stability: [green]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Derived-эффекты:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Hostile cap:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Temp modifiers", source, StringComparison.Ordinal);
        Assert.DoesNotContain("timestamp[..10]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("lastInteraction[..10]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("completionDate[..10]", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_ShiningUi_MustExposePackagePoliticsAndStructureInspection()
    {
        var source = ReadExplorerModeSource();
        var truncatedCoreReceiptInspectionPattern =
            "ThenByDescending(item => GetNodeString(item[\"resolvedAtUtc\"]), StringComparer.OrdinalIgnoreCase)" +
            Environment.NewLine +
            "            .Take(8)";

        Assert.Contains("Осмотреть набор и пакет", source, StringComparison.Ordinal);
        Assert.Contains("Полный осмотр Врат и пакета", source, StringComparison.Ordinal);
        Assert.Contains("Осмотреть политическое состояние фракции", source, StringComparison.Ordinal);
        Assert.Contains("Политическое состояние фракции", source, StringComparison.Ordinal);
        Assert.Contains("Осмотреть залы и светозарных акторов", source, StringComparison.Ordinal);
        Assert.Contains("Залы и светозарные акторы", source, StringComparison.Ordinal);
        Assert.Contains("Осмотреть исходы Обители", source, StringComparison.Ordinal);
        Assert.Contains("Полный осмотр исходов Обители", source, StringComparison.Ordinal);
        Assert.Contains("Осмотреть ожидающие действия Обители", source, StringComparison.Ordinal);
        Assert.Contains("Ожидающие действия Обители", source, StringComparison.Ordinal);
        Assert.Contains("Осмотреть торговые циклы", source, StringComparison.Ordinal);
        Assert.Contains("Полный осмотр торговых циклов", source, StringComparison.Ordinal);
        Assert.Contains("Осмотреть решения фракций", source, StringComparison.Ordinal);
        Assert.Contains("Полный осмотр решений фракций", source, StringComparison.Ordinal);
        Assert.Contains("Торговля и кузня", source, StringComparison.Ordinal);
        Assert.Contains("Предварительный осмотр перековки", source, StringComparison.Ordinal);
        Assert.Contains("Выберите действие кузни", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Trade и forge", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Forge action", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Stored founding/realignment/leadership contracts", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Обзор stored Сияющей Обители", source, StringComparison.Ordinal);
        Assert.DoesNotContain(truncatedCoreReceiptInspectionPattern, source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_ShiningAndFoundationUi_MustNotLeakResidualEnglishProtocolWording()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("Pending Shining core action created", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Favored archetype", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Patron effect family", source, StringComparison.Ordinal);
        Assert.DoesNotContain("candidate head", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Realignment mode", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Leadership transition mode", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Founder bonus", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Founder feature", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Foundation route", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Legacy summary", source, StringComparison.Ordinal);
        Assert.DoesNotContain("strongest visible pull", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Soulbound:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("legendary tier", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Выбранные card id", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Полный frozen payload", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Tone tags", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Archetype проекта", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Output family", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Target faction", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Технический JSON эффекта", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ruinous failure", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Tier проекта", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Subversion не может", source, StringComparison.Ordinal);
        Assert.DoesNotContain("materialize-иться", source, StringComparison.Ordinal);
        Assert.DoesNotContain("roleplay the request", source, StringComparison.Ordinal);
        Assert.DoesNotContain("curated память", source, StringComparison.Ordinal);
        Assert.DoesNotContain("materialize-ит", source, StringComparison.Ordinal);
        Assert.DoesNotContain("accepted result", source, StringComparison.Ordinal);
        Assert.DoesNotContain("rejected/cancelled", source, StringComparison.Ordinal);
        Assert.DoesNotContain("archive project fuel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("pending_archive_project_fuel_request.json", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FormatShiningJsonNodeForDisplay(card[\"effectPayload\"])", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WorldSetupAndWorldRules_UiLabels_MustNotLeakEnglishMenuText()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain(" 🌍 World Setup ", source, StringComparison.Ordinal);
        Assert.DoesNotContain(" 👁 Pending World Setup ", source, StringComparison.Ordinal);
        Assert.DoesNotContain(" 📜 World Directives ", source, StringComparison.Ordinal);
        Assert.DoesNotContain(" 📚 World Profile ", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Создать / редактировать pending setup", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Очистить pending setup", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Pending world setup", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Текущий pending setup", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Очистить world directives", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Создать / редактировать world directives", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_RivalSoulArcSignals_MustHavePlayerFacingMarker()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("🧵 Чужая нить судьбы", source, StringComparison.Ordinal);
        Assert.Contains("relatedRivalArcId", source, StringComparison.Ordinal);
        Assert.Contains("Что уже изменилось в мире", source, StringComparison.Ordinal);
        Assert.Contains("Последнее проявление", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_ShiningCoreActions_MustUsePendingRequestFlowInsteadOfDirectStateMutation()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("TryQueueNativeFactionDiscovery(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryInvestInFaction(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryCompleteProject(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TrySupportProject(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryUnsupportProject(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryRetireProject(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryOpenGates(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryPrepareIncarnationPackage(", source, StringComparison.Ordinal);
        Assert.Contains("ShiningCoreActionRequestState", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_ShiningForge_MustUsePendingCoreActionFlowInsteadOfDirectForgeMutation()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("ActionTypeForgeRelicReshape", source, StringComparison.Ordinal);
        Assert.Contains("ActionTypeForgeRelicRetuneProperty", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryApplyForgeAction(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_ShiningTrade_MustUsePendingTradeRequestFlowInsteadOfDirectInventoryMaterialization()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("ShiningTradeRequestState", source, StringComparison.Ordinal);
        Assert.Contains("ShiningTradeService.RequestInventoryAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShiningTradeRequestState.WriteRequestAsync(_fs, request)", source, StringComparison.Ordinal);
        Assert.Contains("_stateManager.CurrentState.TurnNumber + 1", source, StringComparison.Ordinal);
        Assert.Contains("BuildShiningTradePostConfirmMarkup", source, StringComparison.Ordinal);
        Assert.DoesNotContain("input/turn_request.json отсутствует", source, StringComparison.Ordinal);
        Assert.DoesNotContain("tradeInventory\"] =", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_GuardianProjects_MustExposeDedicatedJournalCommand()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("/проекты_хранителей", source, StringComparison.Ordinal);
        Assert.Contains("ShowGuardianProjects", source, StringComparison.Ordinal);
        Assert.Contains("GuardianProjectState.JournalPath", source, StringComparison.Ordinal);
        Assert.Contains("GuardianProjectState.TrackerPath", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_MustExposeGuardianCorrectionsAndScenarioCoreReview()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("/коррективы_хранителя", source, StringComparison.Ordinal);
        Assert.Contains("ShowGuardianCorrections", source, StringComparison.Ordinal);
        Assert.Contains("Просмотреть сценарное ядро", source, StringComparison.Ordinal);
        Assert.Contains("Подтвердить извлечённые факты", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_MustExposeDedicatedAbodePowerJournalCommand()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("/сила_обители", source, StringComparison.Ordinal);
        Assert.Contains("ShowAbodePower", source, StringComparison.Ordinal);
        Assert.Contains("GuardianPowerEventState.JournalPath", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_GuardianProjectsAndAbodePower_MustNotLeakResidualEnglishAuditLabels()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("Power loss", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Pressure relief", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Stability relief", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Safe pressure", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Defense rating", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Последний power event", source, StringComparison.Ordinal);
        Assert.DoesNotContain("canonical history", source, StringComparison.Ordinal);
        Assert.DoesNotContain("modifierId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Terminal state", source, StringComparison.Ordinal);
        Assert.DoesNotContain("rival-Хранителя", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Markup.Escape(modifier.ModifierType)", source, StringComparison.Ordinal);
        Assert.Contains("потеря силы", source, StringComparison.Ordinal);
        Assert.Contains("внешнее давление", source, StringComparison.Ordinal);
        Assert.Contains("Последнее изменение силы Обители", source, StringComparison.Ordinal);
        Assert.Contains("Последние изменения силы Обители", source, StringComparison.Ordinal);
        Assert.Contains("Идентификатор модификатора", source, StringComparison.Ordinal);
        Assert.Contains("Конечное состояние", source, StringComparison.Ordinal);
        Assert.Contains("\"rival_strike\" => \"Удар Хранителя-соперника\"", source, StringComparison.Ordinal);
        Assert.Contains("\"next_internal_project_starting_pressure\" => \"стартовое давление следующего внутреннего проекта\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_CompletedGuardianProjects_MustUsePlayerFacingStateLabels()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("lines.Add($\"    ✓ [white]{Markup.Escape(projectName)}[/] [dim]{Markup.Escape(finalState)}[/]{turnTag}\");", source, StringComparison.Ordinal);
        Assert.DoesNotContain("panelLines.Add($\"Конечное состояние: [white]{Markup.Escape(finalState)}[/]\");", source, StringComparison.Ordinal);
        Assert.Contains("var finalStateLabel = string.IsNullOrWhiteSpace(finalState) ? \"\" : FormatGuardianProjectStateLabel(finalState);", source, StringComparison.Ordinal);
        Assert.Contains("lines.Add($\"    ✓ [white]{Markup.Escape(projectName)}[/] [dim]{Markup.Escape(finalStateLabel)}[/]{turnTag}\");", source, StringComparison.Ordinal);
        Assert.Contains("panelLines.Add($\"Конечное состояние: [white]{Markup.Escape(FormatGuardianProjectStateLabel(finalState))}[/]\");", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_ShiningForge_ReshapeFlow_MustHumanizeFormTags()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("return formTag.Trim();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Ask(\"[cyan]Новая форма реликвии:[/]\", currentFormTag)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Ask(\"[cyan]Новая форма реликвии:[/]\", suggestion)", source, StringComparison.Ordinal);
        Assert.Contains("NormalizeForgeFormTagInput(", source, StringComparison.Ordinal);
        Assert.Contains("Ask(\"[cyan]Новая форма реликвии:[/]\", currentFormLabel)", source, StringComparison.Ordinal);
        Assert.Contains("Ask(\"[cyan]Новая форма реликвии:[/]\", DescribeForgeFormTag(suggestion))", source, StringComparison.Ordinal);
        Assert.Contains("\"glass_path\" => \"стекло пути\"", source, StringComparison.Ordinal);
        Assert.Contains("\"solar_crown\" => \"солнечный венец\"", source, StringComparison.Ordinal);
        Assert.Contains("\"lance\" => \"копьё\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_ShiningForge_RetuneAndUpliftFallbacks_MustOfferPreviewChoicesBeforeManualJson()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("Использовать базовый шаблон", source, StringComparison.Ordinal);
        Assert.Contains("Использовать подготовленный набор", source, StringComparison.Ordinal);
        Assert.Contains("BuildForgeReplacementPropertyTemplate", source, StringComparison.Ordinal);
        Assert.Contains("BuildForgeAddedPropertiesTemplate", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_ShiningForge_MustUsePlayerFacingRussianTerminology()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("Нет доступных blessing rerolls для forge", source, StringComparison.Ordinal);
        Assert.DoesNotContain("валидным JSON object", source, StringComparison.Ordinal);
        Assert.DoesNotContain("валидным JSON array", source, StringComparison.Ordinal);
        Assert.DoesNotContain("В soul_state нет доступных Soul Relics", source, StringComparison.Ordinal);
        Assert.DoesNotContain("canonical properties array", source, StringComparison.Ordinal);
        Assert.Contains("Нет доступных перебросов благословением для кузни", source, StringComparison.Ordinal);
        Assert.Contains("Настроить свойство вручную", source, StringComparison.Ordinal);
        Assert.Contains("Настроить набор вручную", source, StringComparison.Ordinal);
        Assert.Contains("реликвий души", source, StringComparison.Ordinal);
        Assert.Contains("списка свойств для перековки", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_AfterlifeInboxFallbacks_MustHumanizeArchiveAndProjectStateLabels()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("lines.Add($\"  Тип: [dim]{Markup.Escape(archiveType)}[/]\");", source, StringComparison.Ordinal);
        Assert.DoesNotContain("lines.Add($\"  Состояние: [dim]{Markup.Escape(stateLabel)}[/]\");", source, StringComparison.Ordinal);
        Assert.Contains("AfterlifeArchiveState.GetEntryTypeLabel(archiveType)", source, StringComparison.Ordinal);
        Assert.Contains("FormatGuardianProjectStateLabel(stateLabel)", source, StringComparison.Ordinal);
        Assert.Contains("FormatGuardianProjectStateLabel(notification.TargetProjectStateLabel)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_CodexRelicAndForgeDetails_MustHumanizeCanonicalCategoryAndStatLabels()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("lines.Add($\"  Категория: [white]{Markup.Escape(category)}[/]\");", source, StringComparison.Ordinal);
        Assert.DoesNotContain("lines.Add($\"  Подкатегория: [dim]{Markup.Escape(subcategory)}[/]\");", source, StringComparison.Ordinal);
        Assert.DoesNotContain("lines.Add($\"  📋 Категория: [cyan]{Markup.Escape(category)}[/]\");", source, StringComparison.Ordinal);
        Assert.DoesNotContain("lines.Add($\"{indent}[dim]Затронутая характеристика: {Markup.Escape(stat)}[/]\");", source, StringComparison.Ordinal);
        Assert.Contains("DescribeCodexCategoryLabel(category)", source, StringComparison.Ordinal);
        Assert.Contains("DescribeCodexSubcategoryLabel(subcategory)", source, StringComparison.Ordinal);
        Assert.Contains("DescribeSoulRelicCategoryLabel(category)", source, StringComparison.Ordinal);
        Assert.Contains("DescribeShiningForgeStat(stat)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_ShiningHelpAndInspectionPanels_MustUsePlayerFacingRussianTerminology()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("New Game+ reset", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Late-game", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Resolved core-action receipts", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Added properties", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Resolved political receipts", source, StringComparison.Ordinal);
        Assert.Contains("без запуска Нового Цикла", source, StringComparison.Ordinal);
        Assert.Contains("Поздний ритуал основания собственного Хранителя", source, StringComparison.Ordinal);
        Assert.Contains("Подтверждённых исходов действий Обители пока нет.", source, StringComparison.Ordinal);
        Assert.Contains("Добавленные свойства", source, StringComparison.Ordinal);
        Assert.Contains("Подтверждённых политических решений пока нет.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_MustExposeDedicatedAbodeOfferingCommand()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("/подношение_обители", source, StringComparison.Ordinal);
        Assert.Contains("ShowAbodeOffering", source, StringComparison.Ordinal);
        Assert.Contains("GuardianAbodeOfferingState.PendingRequestPath", source, StringComparison.Ordinal);
        Assert.Contains("GuardianAbodeOfferingState.ActionTag", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_MustExposeAfterlifeArchiveCommand()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("/архив_души", source, StringComparison.Ordinal);
        Assert.Contains("ShowAfterlifeArchive", source, StringComparison.Ordinal);
        Assert.Contains("afterlifeArchive", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_MustExposeArchiveCandidateCommand()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("/архив_кандидаты", source, StringComparison.Ordinal);
        Assert.Contains("ShowAfterlifeArchiveCandidates", source, StringComparison.Ordinal);
        Assert.Contains("AfterlifeArchiveCandidateService", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_ArchiveInteractions_MustUsePendingRequestFlowInsteadOfExplicitOptionsOrAffinityLogic()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("AfterlifeArchiveAffinityService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AfterlifeArchiveInteractionOptionsService", source, StringComparison.Ordinal);
        Assert.Contains("CreateRequestAsync(", source, StringComparison.Ordinal);
        Assert.Contains("_pendingGmAction = result.PendingGmAction", source, StringComparison.Ordinal);
        Assert.DoesNotContain("fit ", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_TrainingPurchasePendingGmAction_MustExitParentTrainingMenu()
    {
        var source = ReadUiSourceFile(Path.Combine("ExplorerMode", "ExplorerMode.Training.cs"));
        var mortalTraining = ExtractMethodSource(source, "private async Task ShowMortalTrainingAsync");
        var afterlifeTraining = ExtractMethodSource(source, "private async Task ShowAfterlifeTrainingAsync");

        Assert.Contains("await ShowTeacherTrainingOffersAsync(teacher);", mortalTraining, StringComparison.Ordinal);
        Assert.Contains("if (!string.IsNullOrWhiteSpace(_pendingGmAction))", mortalTraining, StringComparison.Ordinal);

        Assert.Contains("await ShowTeacherTrainingOffersAsync(teacher);", afterlifeTraining, StringComparison.Ordinal);
        Assert.Contains("if (!string.IsNullOrWhiteSpace(_pendingGmAction))", afterlifeTraining, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryFactionReaders_DoNotRenderPrivateFactionMaterializationTokens()
    {
        var mortalConsole = ReadUiSourceFile(Path.Combine(
            "ExplorerMode",
            "ExplorerMode.FactionsAndWorldNews.cs"));
        var mortalBrowserSource = ReadUiSourceFile(
            "ExplorerMortalWorldCommandResultBuilder.cs");
        var mortalBrowserFactionReader = string.Join(
            Environment.NewLine,
            ExtractMethodSource(
                mortalBrowserSource,
                "private static UiEntityDossierBlock BuildFactionReferenceOverviewCard"),
            ExtractMethodSource(
                mortalBrowserSource,
                "private static UiEntityDossierBlock BuildFactionReferenceDetailPanel"),
            ExtractMethodSource(
                mortalBrowserSource,
                "private static List<UiEntityDossierSection> BuildFactionSections"),
            ExtractMethodSource(
                mortalBrowserSource,
                "private static List<UiKeyValueItem> BuildFactionOverviewItems"));
        var shiningConsoleFiles = new[]
        {
            "ExplorerMode.Afterlife.ShiningAbode.cs",
            "ExplorerMode.Afterlife.ShiningAbode.Actions.cs",
            "ExplorerMode.Afterlife.ShiningAbode.ActionPreviews.cs",
            "ExplorerMode.Afterlife.ShiningAbode.Gates.cs",
            "ExplorerMode.Afterlife.ShiningAbode.Politics.cs",
            "ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs",
            "ExplorerMode.Afterlife.ShiningAbode.Treasury.cs"
        };
        var shiningConsoleSources = shiningConsoleFiles.ToDictionary(
            file => file,
            file => ReadUiSourceFile(Path.Combine("ExplorerMode", file)),
            StringComparer.Ordinal);
        var shiningConsole = string.Join(
            Environment.NewLine,
            shiningConsoleFiles.Select(file => shiningConsoleSources[file]));
        var shiningBrowser = ReadUiSourceFile(
            "ExplorerShiningAbodeCommandResultBuilder.cs");
        var ordinaryReaders = new[]
        {
            ("Mortal console", mortalConsole),
            ("Mortal browser", mortalBrowserFactionReader),
            ("Shining console", shiningConsole),
            ("Shining browser", shiningBrowser)
        };

        Assert.All(ordinaryReaders, reader =>
            Assert.All(PrivateFactionMaterializationTokens, token =>
                Assert.DoesNotContain(
                    token,
                    reader.Item2,
                    StringComparison.Ordinal)));

        Assert.Contains(
            "RemovePrivateFactionMaterialization(clone);",
            shiningConsole,
            StringComparison.Ordinal);
        Assert.Contains(
            "obj.Remove(FactionMaterializationContract.PropertyName);",
            shiningConsole,
            StringComparison.Ordinal);

        foreach (var source in shiningConsoleSources
                     .Where(pair => !string.Equals(
                         pair.Key,
                         "ExplorerMode.Afterlife.ShiningAbode.Treasury.cs",
                         StringComparison.Ordinal))
                     .Select(pair => pair.Value))
        {
            Assert.DoesNotMatch(
                new Regex(
                    @"WriteJsonAuditPanel\([\s\S]{0,240}?,\s*(?:context\.Root|factionAudit|faction)\s*,",
                    RegexOptions.CultureInvariant),
                source);
        }

        var treasuryAudit = ExtractMethodSource(
            shiningConsoleSources[
                "ExplorerMode.Afterlife.ShiningAbode.Treasury.cs"],
            "private static JsonObject BuildShiningTreasuryAuditNode");
        Assert.Contains(
            "ShiningAbodeState.GetSoulSpendableInkFeathers",
            treasuryAudit,
            StringComparison.Ordinal);
        Assert.Contains(
            "ShiningAbodeState.TreasuryProperty",
            treasuryAudit,
            StringComparison.Ordinal);
        Assert.Contains("[\"lightSparks\"]", treasuryAudit, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"factions\"]", treasuryAudit, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FactionMaterializationContract.PropertyName",
            treasuryAudit,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "beforeShiningRoot.DeepClone",
            treasuryAudit,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "afterShiningRoot.DeepClone",
            treasuryAudit,
            StringComparison.Ordinal);

        var factionLabelResolver = ExtractMethodSource(
            shiningConsoleSources["ExplorerMode.Afterlife.ShiningAbode.cs"],
            "private static string ResolveShiningFactionLabel");
        Assert.DoesNotContain(
            "return factionId;",
            factionLabelResolver,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_ChaosSeaTravelAction_MustExposeCanonicalNavigationContract()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("[CHAOS_SEA_TRAVEL]", source, StringComparison.Ordinal);
        Assert.Contains("BuildChaosSeaTravelAuditNode", source, StringComparison.Ordinal);
        Assert.Contains("targetAbodeId=", source, StringComparison.Ordinal);
        Assert.Contains("targetGuardianId=", source, StringComparison.Ordinal);
        Assert.Contains("previousAbodeId=", source, StringComparison.Ordinal);
        Assert.Contains("previousActiveGuardianId=", source, StringComparison.Ordinal);
        Assert.Contains("discoveredAbodes=", source, StringComparison.Ordinal);
        Assert.Contains("activeGuardian", source, StringComparison.Ordinal);
        Assert.Contains("chaosSeaNavigation.currentAbodeId", source, StringComparison.Ordinal);
        Assert.Contains("targetGuardian.abode.isDiscovered", source, StringComparison.Ordinal);
        Assert.Contains("forbiddenSurfaces", source, StringComparison.Ordinal);
        Assert.Contains("currentLocationData", source, StringComparison.Ordinal);
        Assert.Contains("worldEventsLog", source, StringComparison.Ordinal);
    }
}
