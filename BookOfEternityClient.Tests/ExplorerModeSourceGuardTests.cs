using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerModeSourceGuardTests
{
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

    [Fact]
    public void ExplorerMode_MustUseConsoleAdapterInsteadOfDirectAnsiConsoleCalls()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("AnsiConsole.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Console.ReadKey(true)", source, StringComparison.Ordinal);
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
        Assert.Contains("ShiningTradeRequestState.WriteRequestAsync", source, StringComparison.Ordinal);
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
