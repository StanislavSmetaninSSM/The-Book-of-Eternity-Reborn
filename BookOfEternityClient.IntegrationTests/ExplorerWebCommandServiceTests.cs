using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "RegressionIntegration")]
public sealed class ExplorerWebCommandServiceTests :
    IDisposable,
    IClassFixture<ExplorerWebCommandSeedTemplateFixture>
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ExplorerWebCommandService _service;
    private readonly ValidationService _validationService;
    private readonly ExplorerWebCommandSeedTemplateFixture _seedFixture;

    public ExplorerWebCommandServiceTests(ExplorerWebCommandSeedTemplateFixture seedFixture)
    {
        _seedFixture = seedFixture;
        _rootPath = seedFixture.CreateIsolatedCaseRoot();
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        _validationService = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        _service = new ExplorerWebCommandService(_fs, _stateManager, new LocalizationManager(), _validationService);
    }

    [Fact]
    public async Task ExecuteAsync_MigratedHelp_ReturnsCompletedDto()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/help"));

        Assert.Equal("/help", result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.Title.Contains("Помощь", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
    }

    [Fact]
    public async Task ExecuteAsync_HelpInAfterlife_IncludesMemorySceneCommand()
    {
        await SeedUniversalMetaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/help"));

        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Воспоминание", text, StringComparison.Ordinal);
        Assert.Contains("Врата Памяти", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NpcWithRepositoryJournalFixtureAndNoCore_ShowsKnownJournalNotes()
    {
        var fixtureJournal = await File.ReadAllTextAsync(Path.Combine(
            TestRepoPaths.BaseSessionRoot,
            "game_state",
            "npcs",
            "npc_journals.json"));
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_journals.json", fixtureJournal);

        Assert.False(_fs.FileExists("game_state/npcs/npc_core.json"));

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(result.Actions, action =>
            action.Label.Contains("Торек Молотобой", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/npc journal npc_blacksmith_thorek", StringComparison.OrdinalIgnoreCase) &&
            action.Style == UiActionStyle.Secondary &&
            action.RequiresConfirmation == false);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Торек Молотобой", text, StringComparison.Ordinal);
        Assert.Contains("Впечатляющая решительность", text, StringComparison.Ordinal);
        Assert.Contains("2 записи", text, StringComparison.Ordinal);
        Assert.Contains("известные заметки", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "npc-journal-fallback" &&
            block.Title.Equals("Известные НПС по заметкам", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain("Данные ещё не созданы", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/npc_talk", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/npc_trade", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("npc_journals.json", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pending_turn_snapshot", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_NpcJournalFallbackDetail_ShowsFullJournalEntriesAndBackAction()
    {
        await SeedJournalOnlyNpcFilesAsync();

        var overview = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/нпс"));

        Assert.Equal(CommandExecutionState.Completed, overview.State);
        Assert.Contains(overview.Actions, action =>
            action.Label.Contains("Мартен Рош", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/нпс журнал npc_marten_roche_valmont_valet", StringComparison.OrdinalIgnoreCase) &&
            action.Style == UiActionStyle.Secondary &&
            action.RequiresConfirmation == false);

        var detail = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/нпс журнал npc_marten_roche_valmont_valet"));
        var text = CollectBlockText(detail.Blocks);
        var payload = SerializeResult(detail);

        Assert.Equal(CommandExecutionState.Completed, detail.State);
        Assert.Contains("Мартен Рош", text, StringComparison.Ordinal);
        Assert.Contains("Утренний допрос", text, StringComparison.Ordinal);
        Assert.Contains("Он признался, что ночью слышал шаги у фамильной библиотеки.", text, StringComparison.Ordinal);
        Assert.Contains("Имя свидетеля", text, StringComparison.Ordinal);
        Assert.Contains("Он отказался назвать кухонного мальчишку без защиты.", text, StringComparison.Ordinal);
        Assert.Contains("Последняя запись", text, StringComparison.Ordinal);
        Assert.Contains(detail.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "npc-journal" &&
            block.Title.Contains("Мартен Рош", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(detail.Blocks.SelectMany(EnumerateTables));
        Assert.Contains(detail.Actions, action =>
            action.Label.Contains("Назад", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/нпс", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("game_state/npcs", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("npc_journals.json", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SystemGuardiansWithEmptyLibrary_HidesLocalDirectoryPath()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/извечные_хранители"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Извечные хранители", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Папка", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_rootPath, payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("system_guardians", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SystemGuardians_RendersPresetDossiersWithoutManifestPaths()
    {
        await SeedWebSystemGuardianPresetAsync("azalia", "Азалия", "Обитель Неутолимого Пламени");

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/system_guardians"));

        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Извечные хранители", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Азалия", text, StringComparison.Ordinal);
        Assert.Contains("Тестовый системный хранитель для browser tests.", text, StringComparison.Ordinal);
        Assert.Contains("ценность 1", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Обитель Неутолимого Пламени", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тестовое досье для браузерного вывода.", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.Title.Equals("Извечные хранители", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        foreach (var forbidden in new[] { "Preset", "Manifest", "system_guardians" })
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        foreach (var forbidden in new[] { "manifest.json", ".json", _rootPath })
            Assert.DoesNotContain(forbidden, payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Mods_RendersMarkdownCardsWithoutFileNamesOrPaths()
    {
        await _fs.WriteFileAtomicAsync("mods/test_mod.md", """
        # Тонкая настройка мира

        Правило влияет на тон повествования и границы допустимой сцены.
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/mods"));

        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Моды", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тонкая настройка мира", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Правило влияет на тон повествования", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.Title.Equals("Моды", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        foreach (var forbidden in new[] { "test_mod.md", ".md", "game_session", "mods/test_mod", _rootPath })
            Assert.DoesNotContain(forbidden, payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Gm_RendersThoughtsWithoutRawDebugJson()
    {
        await _fs.WriteFileAtomicAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "ГМ заметил: игрок осторожно проверяет письмо.",
          "last_validation_payload": {
            "internalCode": "debug-only"
          }
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/gm", AdvancedEnabled: false));

        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("ГМ заметил", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("output/debug_logs.json", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internalCode", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Debug_RendersStateSummaryWithoutLocalPaths()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/core/player_status.json", "{}");
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", "{}");

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/debug", AdvancedEnabled: false));

        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Файлов состояния", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сессия", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Основное состояние", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Персонажи", text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        foreach (var forbidden in new[] { "BasePath", _rootPath, "game_state/", "output/", ".json", "Путь" })
            Assert.DoesNotContain(forbidden, payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Story_RendersReadableEntriesWithoutStoryFileNames()
    {
        await _fs.WriteFileAtomicAsync("stories/chaos_sea.jsonl", """
        {"turn":1,"timestamp":"2026-05-20T00:00:00Z","realm":"Chaos Sea","player":"Асур","narrative":"Душа проснулась на черном берегу Моря Хаоса."}
        {"turn":2,"timestamp":"2026-05-20T00:05:00Z","realm":"Chaos Sea","player":"Асур","narrative":"Хранитель протянул фонарь и попросил не смотреть в воду."}
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/story", AdvancedEnabled: false));

        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Рассказ", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Душа проснулась", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Хранитель протянул фонарь", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.Title.Equals("Рассказ", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        foreach (var forbidden in new[] { "chaos_sea", ".jsonl", "stories/", _rootPath })
            Assert.DoesNotContain(forbidden, payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Gallery_RendersImageCardsWithoutDirectoryPathTable()
    {
        WriteSessionImage("images/npcs/ashen_knight.png");
        WriteSessionImage("images/scenes/scene_001.webp");

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/gallery", AdvancedEnabled: false));

        var text = CollectBlockText(result.Blocks);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Галерея", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ashen knight", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("сцена 001", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.Title.Equals("Галерея", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain("game_session", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("images/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Путь", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, result.Blocks.OfType<UiImageBlock>().Count());
    }

    [Theory]
    [InlineData("/квесты", "Печать с крыльями")]
    [InlineData("/навыки", "Чувство магических потоков")]
    [InlineData("/новости_мира", "1 событие")]
    [InlineData("/чужие_нити", "Лунный претендент")]
    [InlineData("/погода", "08:15")]
    [InlineData("/транспорт", "Серый конь")]
    [InlineData("/эффекты", "Магический резонанс")]
    [InlineData("/бой", "Теневой посыльный")]
    [InlineData("/доступ_к_хранилищам", "Приватный письменный стол")]
    [InlineData("/взаимодействия", "странной печати")]
    public async Task ExecuteAsync_MortalReadOnlySummaries_ReadCanonicalStateKeys(string command, string expectedText)
    {
        await PrepareMortalReadOnlySummaryFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(expectedText, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Данные ещё не созданы", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_EffectsOverview_AddsEffectDetailActions()
    {
        await SeedMortalEffectsDetailStateAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/эффекты"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var dossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "effects" &&
            block.Title.Equals("Эффекты", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dossier.Sections, static section =>
            section.Title.Equals("Активные записи", StringComparison.OrdinalIgnoreCase) &&
            section.Cards.Any(card => card.Title.Contains("Магический резонанс", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(result.Blocks.SelectMany(EnumerateTables), static table =>
            table.Title.Equals("Эффекты", StringComparison.OrdinalIgnoreCase) ||
            table.Title.Equals("Подробности эффектов", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Contains(result.Actions, action =>
            action.Label.Contains("Магический резонанс", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/эффекты эффект resonance_1", StringComparison.OrdinalIgnoreCase) &&
            action.Style == UiActionStyle.Secondary &&
            action.RequiresConfirmation == false);
    }

    [Fact]
    public async Task ExecuteAsync_EffectsFallback_RendersVisibleStatusAsDossier()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/core/player_status.json", """
        {
          "currentCondition": "Лёгкое недомогание",
          "currentConditionDescription": "Ночные сны оставили тяжесть в висках.",
          "activeConditions": [
            "Головная боль после тяжёлых снов",
            "Магический резонанс"
          ]
        }
        """);
        await _stateManager.RefreshGameStateAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/эффекты"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "visible-status-effects" &&
            block.Title.Equals("Видимые состояния", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Лёгкое недомогание", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Магический резонанс", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_EffectDetail_RendersStructuredEffectDetails()
    {
        await SeedMortalEffectsDetailStateAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/эффекты эффект resonance_1"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal("/эффекты эффект resonance_1", result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Магический резонанс", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Руническая перчатка подсвечивает следы магии.", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Длительность", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("До полудня", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Источник", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Руническая перчатка", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("незначительная", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("minor", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Структурные бонусы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тип бонуса", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Восприятие", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Боевые эффекты", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Резонансный толчок", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сбивает концентрацию цели.", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Через серебряную арку", text, StringComparison.Ordinal);
        Assert.Contains("По звону хрустального колокола", text, StringComparison.Ordinal);
        Assert.Contains("По следу мерцающих рун", text, StringComparison.Ordinal);
        var effectDossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "effect" &&
            block.Title.Contains("Магический резонанс", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            effectDossier.Sections.SelectMany(static section => section.Facts),
            static fact => fact.Label.Equals("Осталось ходов", StringComparison.OrdinalIgnoreCase) &&
                           fact.Value == "1");
        Assert.Contains(effectDossier.Sections, static section =>
            section.Title.Equals("Структурные бонусы", StringComparison.OrdinalIgnoreCase) &&
            section.Cards.Any(card => card.Title.Contains("Восприятие", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(result.Blocks.SelectMany(EnumerateTables), static table =>
            table.Title.Equals("Структурные бонусы", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Actions, action =>
            action.Label.Contains("Назад", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/эффекты", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_EffectDetail_MissingTargetShowsPlayerFacingFallback()
    {
        await SeedMortalEffectsDetailStateAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/эффекты эффект missing_effect"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Такой эффект не найден.", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
    }

    [Fact]
    public async Task ExecuteAsync_CombatOverview_ExposesEnemyAllyAndLogDrilldownActions()
    {
        await SeedRichMortalCombatFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/бой"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Боевая обстановка", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Враги", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Союзники", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Боевой журнал", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Теневой посыльный", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Рина из Серебряной стражи", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Раунд 2", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/combat", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "combat-overview" &&
            block.Title.Equals("Боевая обстановка", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));

        var enemyAction = Assert.Single(result.Actions, static action => action.Id == "combat-enemy-shadow_messenger");
        Assert.Equal("/бой враг shadow_messenger", enemyAction.Command);
        Assert.Contains("Осмотреть врага", enemyAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Теневой посыльный", enemyAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, enemyAction.Style);
        Assert.False(enemyAction.RequiresConfirmation);

        var allyAction = Assert.Single(result.Actions, static action => action.Id == "combat-ally-rina_guard");
        Assert.Equal("/бой союзник rina_guard", allyAction.Command);
        Assert.Contains("Осмотреть союзника", allyAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Рина из Серебряной стражи", allyAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, allyAction.Style);
        Assert.False(allyAction.RequiresConfirmation);

        var logAction = Assert.Single(result.Actions, static action => action.Id == "combat-log-log_round_2");
        Assert.Equal("/бой журнал log_round_2", logAction.Command);
        Assert.Contains("Открыть запись боя", logAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Раунд 2", logAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, logAction.Style);
        Assert.False(logAction.RequiresConfirmation);
    }

    [Fact]
    public async Task ExecuteAsync_CombatEnemyDetail_RendersPlayerFacingDetailWithoutRawJson()
    {
        await SeedRichMortalCombatFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/бой враг shadow_messenger"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains("Враг: Теневой посыльный", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Здоровье", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("18/30", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Намерение", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("сорвать концентрацию мага", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Горит после серебряной стрелы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("урон", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "combatant" &&
            block.Title.Contains("Теневой посыльный", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain("enemyId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("targetPriority", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/combat", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_CombatAllyDetail_RendersPlayerFacingDetailWithoutRawJson()
    {
        await SeedRichMortalCombatFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/бой союзник rina_guard"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains("Союзник: Рина из Серебряной стражи", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Здоровье", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("22/28", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("защищает мага", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Боевой клич держит строй", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "combatant" &&
            block.Title.Contains("Рина из Серебряной стражи", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain("allyId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/combat", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_CombatLogDetail_RendersPlayerFacingEntryWithoutRawJson()
    {
        await SeedRichMortalCombatFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/бой журнал log_round_2"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains("Запись боя: Раунд 2", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Рина сбила посыльного с фланга", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Теневой посыльный", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("оглушён", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "combat-log-entry" &&
            block.Title.Contains("Раунд 2", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain("entryId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/combat", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WorldNewsOverview_ExposesEventFlagAndProgressionDrilldownActions()
    {
        await SeedRichMortalWorldNewsFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/новости_мира"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        var dossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "world-news" &&
            block.Title.Equals("Новости мира", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Blocks.SelectMany(EnumerateTables), static table =>
            table.Title.Equals("Новости мира", StringComparison.OrdinalIgnoreCase) ||
            table.Title.Equals("Мировые события", StringComparison.OrdinalIgnoreCase) ||
            table.Title.Equals("Флаги мира", StringComparison.OrdinalIgnoreCase) ||
            table.Title.Equals("Прогресс мира", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dossier.Sections, static section => section.Title.Equals("Сводка", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dossier.Sections, static section =>
            section.Title.Equals("Мировые события", StringComparison.OrdinalIgnoreCase) &&
            section.Cards.Any(card => card.Title.Contains("Беспорядки у Северных ворот", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("Новости мира", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Мировые события", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Угрозы локаций", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Активности НПС", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Проекты фракций", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Флаги мира", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Прогресс мира", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Беспорядки у Северных ворот", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Стража закрыла торговую площадь", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Праздник стих после тревоги", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Музыканты играют тише после ночного письма", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Дорога к Серебряному броду", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("На тракте снова появились торговцы", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Карманники у ворот", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Мира Ключница", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ночные патрули", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Полная запись", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("riots_at_gate", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("festival_quiet", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("road_silverford", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/world", text, StringComparison.OrdinalIgnoreCase);

        var eventAction = Assert.Single(result.Actions, static action => action.Id == "world-news-event-riots_at_gate");
        Assert.Equal("/новости_мира событие riots_at_gate", eventAction.Command);
        Assert.Contains("Открыть событие", eventAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Беспорядки у Северных ворот", eventAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Стража закрыла торговую площадь", eventAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, eventAction.Style);
        Assert.False(eventAction.RequiresConfirmation);

        var flagAction = Assert.Single(result.Actions, static action => action.Id == "world-news-flag-festival_quiet");
        Assert.Equal("/новости_мира флаг festival_quiet", flagAction.Command);
        Assert.Contains("Осмотреть флаг", flagAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Праздник стих после тревоги", flagAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Музыканты играют тише после ночного письма", flagAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, flagAction.Style);
        Assert.False(flagAction.RequiresConfirmation);

        var progressionAction = Assert.Single(result.Actions, static action => action.Id == "world-news-progression-road_silverford");
        Assert.Equal("/новости_мира прогресс road_silverford", progressionAction.Command);
        Assert.Contains("Открыть прогресс", progressionAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Дорога к Серебряному броду", progressionAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("На тракте снова появились торговцы", progressionAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, progressionAction.Style);
        Assert.False(progressionAction.RequiresConfirmation);
    }

    [Fact]
    public async Task ExecuteAsync_WorldNewsEventDetail_RendersPlayerFacingDetailWithoutRawJson()
    {
        await SeedRichMortalWorldNewsFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/новости_мира событие riots_at_gate"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains("Событие: Беспорядки у Северных ворот", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Северные ворота", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Городская стража", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("торговая площадь закрыта до следующего утра", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Капитан ждёт свидетелей", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Настроение жителей", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("горожане боятся новых писем", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Зацепки", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("проверить печать на письме", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ставки", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("рынок может вспыхнуть", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Свидетели всё ещё помнят серебряную печать", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Свидетель", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Старый писарь", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("видел курьера у северных ворот", text, StringComparison.OrdinalIgnoreCase);
        var dossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "world-event" &&
            block.Title.Contains("Беспорядки у Северных ворот", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dossier.Sections, static section => section.Title.Equals("Зацепки", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dossier.Sections, static section => section.Title.Equals("Ставки", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dossier.Sections.SelectMany(static section => section.Cards), static block =>
            block.Title.Contains("Старый писарь", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("eventId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("npcId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("worldEventsLog", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/world", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Source path", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("State path", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Source file", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Source url", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("world_events.json", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sourcePath", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("statePath", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sourceFile", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sourceUrl", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE_BROWSER_WORLD_NEWS_LOCATION_REPAIR", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("mortal_location_materialization_repair", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("rawCoordinate", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WorldNewsValmontEventDetail_RendersUsefulSeededDetails()
    {
        await SeedCanonicalMortalSummaryFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/новости_мира событие world_event_valmont_letter"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Contains("Событие: Письмо появилось ночью", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Печать", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("переплетённые крылья и полумесяц", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Зацепки", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сравнить печать с семейным архивом", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ставки", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("кто-то проверяет реакцию рунической перчатки", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Возможность", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("перехватить ночного посланника", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Открытые вопросы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("кто знает семейный шифр", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Что знает игрок", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("письмо появилось после полуночи", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Связанные лица", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Мариус де Вальмонт", text, StringComparison.OrdinalIgnoreCase);

        var detailDossier = Assert.Single(
            result.Blocks.SelectMany(EnumerateEntityDossiers),
            static block => block.EntityType == "world-event" &&
                block.Title.Contains("Письмо появилось ночью", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(detailDossier.Sections, static section =>
            section.Title.Equals("Зацепки", StringComparison.OrdinalIgnoreCase) &&
            section.List.Any(item => item.Contains("Сравнить печать", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(detailDossier.Sections, static section =>
            section.Title.Equals("Ставки", StringComparison.OrdinalIgnoreCase) &&
            section.Facts.Any(item => item.Label.Equals("Опасность", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(detailDossier.Sections, static section =>
            section.Title.Equals("Открытые вопросы", StringComparison.OrdinalIgnoreCase) &&
            section.List.Any(item => item.Contains("кто знает семейный шифр", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(detailDossier.Sections, static section =>
            section.Title.Equals("Связанные лица", StringComparison.OrdinalIgnoreCase) &&
            section.Cards.Any(person => person.Title.Contains("Мариус де Вальмонт", StringComparison.OrdinalIgnoreCase)));

        Assert.DoesNotContain("Метка", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Opportunity", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Open questions", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Player knowledge", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("world_event_valmont_letter", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eventId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("npcId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/world", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WorldNewsFlagDetail_RendersMajorSubsectionDetailWithoutRawJson()
    {
        await SeedRichMortalWorldNewsFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/world_news flag festival_quiet"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains("Флаг мира: Праздник стих после тревоги", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Северный квартал", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Музыканты играют тише после ночного письма", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Площадь открыта только для жителей", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Слухи", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("скрипачи ушли до заката", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("flagId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("worldStateFlags", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/world", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WorldNewsProgressionDetail_RendersPlayerFacingEntryWithoutRawJson()
    {
        await SeedRichMortalWorldNewsFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/новости_мира прогресс road_silverford"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains("Прогресс мира: Дорога к Серебряному броду", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Караваны возвращаются", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Стража разогнала засаду", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Цены на соль упали", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Следующие признаки", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("караван просит охрану", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("progressionId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/world", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_PlayerInteractionsOverview_ExposesPlayerAndRecordDrilldownActions()
    {
        await SeedRichMortalPlayerInteractionsFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/взаимодействия"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Взаимодействия игроков", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Лианна из янтарной башни", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Страж Кай", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Передача шифра", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Спор у переправы", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/misc", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "interactions" &&
            block.Title.Equals("Взаимодействия игроков", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));

        var playerAction = Assert.Single(result.Actions, static action => action.Id == "interactions-player-player_lienna");
        Assert.Equal("/взаимодействия игрок player_lienna", playerAction.Command);
        Assert.Contains("Лианна из янтарной башни", playerAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, playerAction.Style);
        Assert.False(playerAction.RequiresConfirmation);

        var recordAction = Assert.Single(result.Actions, static action => action.Id == "interactions-record-meeting_cipher");
        Assert.Equal("/взаимодействия запись meeting_cipher", recordAction.Command);
        Assert.Contains("Передача шифра", recordAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, recordAction.Style);
        Assert.False(recordAction.RequiresConfirmation);
    }

    [Fact]
    public async Task ExecuteAsync_PlayerInteractionPlayerDetail_RendersOnePlayerWithoutRawJson()
    {
        await SeedRichMortalPlayerInteractionsFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/взаимодействия игрок player_lienna"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains("Игрок: Лианна из янтарной башни", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("союзница по тайному письму", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ждёт ответа у старого фонтана", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Передача шифра", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Спор у переправы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "interaction-player" &&
            block.Title.Contains("Лианна из янтарной башни", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain("Страж Кай", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("playerId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("records", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/misc", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_PlayerInteractionRecordDetail_RendersOneRecordWithoutRawJson()
    {
        await SeedRichMortalPlayerInteractionsFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/взаимодействия запись meeting_cipher"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains("Запись взаимодействия: Передача шифра", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Лианна из янтарной башни", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Старый фонтан", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("день 42, вечер", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("шифр спрятан в перчатке", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Можно спросить о знаке Вальмонтов", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("через старый фонтан", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Лианна показала безопасный знак на серебряном ключе", text, StringComparison.Ordinal);
        AssertNoFlattenedStructuredDetails(result);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "interaction-record" &&
            block.Title.Contains("Передача шифра", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain("argument_at_ferry", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("interactionId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE_INTERACTION_RECEIPT", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_INTERACTION_CREATION_REF", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_INTERACTION_RAW_ITEM", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateInventory", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("records", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/misc", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_PlayerInteractionRecordDetail_RendersCanonicalCommandPayloadWithoutRawJson()
    {
        await SeedCanonicalCommandPlayerInteractionsFilesAsync();

        var overview = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/взаимодействия"));

        Assert.Equal(CommandExecutionState.Completed, overview.State);
        var recordAction = Assert.Single(overview.Actions, static action => action.Id == "interactions-record-player_mara-1");
        Assert.Equal("/взаимодействия запись player_mara-1", recordAction.Command);
        Assert.Contains("Запись", recordAction.Label, StringComparison.OrdinalIgnoreCase);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(recordAction.Command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains("Запись взаимодействия", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Игрок 1", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("player_mara", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Лианна показала Серебряный ключ у старого фонтана", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("покрыт знаками янтарной башни", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2", text, StringComparison.OrdinalIgnoreCase);
        AssertNoFlattenedStructuredDetails(result);
        Assert.DoesNotContain("UpdateInventory", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UpdateInventory", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE_CANONICAL_INTERACTION_RAW_ITEM", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_CANONICAL_INTERACTION_RECEIPT", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("game_state/misc", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/квесты", "quests-detail-quest_winged_seal", "/квесты квест quest_winged_seal", "Печать с крыльями")]
    [InlineData("/навыки", "skills-detail-skill_arcane_sense", "/навыки навык skill_arcane_sense", "Чувство магических потоков")]
    [InlineData("/фракции", "factions-detail-faction_city_watch", "/фракции фракция faction_city_watch", "Городская стража")]
    [InlineData("/локации", "locations-detail-loc_square", "/локации локация loc_square", "Старая площадь")]
    [InlineData("/чужие_нити", "rival-threads-detail-rival_arc_moonlit_claimant", "/чужие_нити нить rival_arc_moonlit_claimant", "Лунный претендент")]
    [InlineData("/коррективы_хранителя", "guardian-corrections-detail-correction_valmont_slot", "/коррективы_хранителя корректировка correction_valmont_slot", "Спорный слот вмешательства")]
    [InlineData("/доступ_к_хранилищам", "storage-access-detail-storage_valmont_private_desk", "/доступ_к_хранилищам хранилище storage_valmont_private_desk", "Приватный письменный стол")]
    [InlineData("/транспорт", "transport-detail-vehicle_gray_horse", "/транспорт транспорт vehicle_gray_horse", "Серый конь")]
    public async Task ExecuteAsync_MortalReferenceBrowserDetailActions_ExposeDrilldownActions(
        string command,
        string expectedActionId,
        string expectedDetailCommand,
        string expectedLabelText)
    {
        await PrepareRichMortalReferenceDetailFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(expectedLabelText, text, StringComparison.OrdinalIgnoreCase);

        var action = Assert.Single(result.Actions, action => action.Id == expectedActionId);
        Assert.Equal(expectedDetailCommand, action.Command);
        Assert.Contains(expectedLabelText, action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, action.Style);
        Assert.False(action.RequiresConfirmation);
    }

    [Theory]
    [InlineData("/квесты", "reference-bundle", "Квесты")]
    [InlineData("/навыки", "reference-bundle", "Навыки")]
    [InlineData("/фракции", "reference-bundle", "Фракции")]
    [InlineData("/чужие_нити", "reference-bundle", "Чужие нити")]
    [InlineData("/коррективы_хранителя", "reference-bundle", "Коррективы Хранителя")]
    [InlineData("/доступ_к_хранилищам", "reference-bundle", "Доступ к хранилищам")]
    public async Task ExecuteAsync_MortalReferenceOverview_RendersDossierCardsWithoutTables(
        string command,
        string expectedEntityType,
        string expectedTitle)
    {
        await PrepareRichMortalReferenceDetailFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), block =>
            block.EntityType == expectedEntityType &&
            block.Title.Equals(expectedTitle, StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        AssertNoFlattenedStructuredDetails(result);
    }

    [Fact]
    public async Task ExecuteAsync_LocationsOverview_RendersDossierCardsWithoutTables()
    {
        await SeedRichMortalReferenceDetailFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/локации"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "locations" &&
            block.Title.Equals("Локации", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Старая площадь", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Архив Вальмонтов", text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        AssertNoFlattenedStructuredDetails(result);
    }

    [Fact]
    public async Task ExecuteAsync_Locations_UsesAcceptedDiscoveryProjectionAndExactSelectors()
    {
        var current = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_browser_current",
            "Текущий двор картографа",
            "visited",
            x: 1,
            y: 1);
        var discovered = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_browser_discovered",
            "Открытая башня картографа",
            "discovered",
            x: 2,
            y: 1);
        var rumored = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_browser_rumored",
            "Переправа из осторожных слухов",
            "rumored",
            x: 3,
            y: 1);
        var hidden = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_browser_hidden",
            "НЕ ПОКАЗЫВАТЬ СКРЫТУЮ БАШНЮ",
            "hidden",
            x: 4,
            y: 1);
        var rejected = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_browser_rejected",
            "НЕ ПОКАЗЫВАТЬ ОТКЛОНЁННЫЙ ДВОР",
            "visited",
            x: 5,
            y: 1);
        rejected.Remove("materializationReceipt");
        await WriteCanonicalBrowserMortalLocationStateAsync(
            [current, discovered, rumored, hidden, rejected],
            MortalLocationTestFixture.CreateCurrentProjection(current),
            [current, discovered, rumored, hidden]);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/локации"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains("Текущий двор картографа", text, StringComparison.Ordinal);
        Assert.Contains("Открытая башня картографа", text, StringComparison.Ordinal);
        Assert.Contains("Переправа из осторожных слухов", text, StringComparison.Ordinal);
        Assert.Contains("слух", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("НЕ ПОКАЗЫВАТЬ", payload, StringComparison.Ordinal);
        Assert.Contains(result.Actions, action =>
            string.Equals(action.Command, "/локации локация loc_browser_discovered", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Actions, action =>
            action.Command.Contains("loc_browser_rumored", StringComparison.Ordinal));

        var wrongCase = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest("/локации локация LOC_BROWSER_DISCOVERED"));
        Assert.DoesNotContain(
            "Открытая башня картографа",
            CollectBlockText(wrongCase.Blocks),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WhereAmI_UsesCanonicalCurrentSchemaDifficultyProjection()
    {
        var canonical = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_browser_current_difficulty",
            "Точный брод браузера",
            "visited",
            x: 8,
            y: 3);
        await WriteCanonicalBrowserMortalLocationStateAsync(
            [canonical],
            MortalLocationTestFixture.CreateCurrentProjection(canonical),
            [canonical]);

        var result = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest("/where_am_i", AdvancedEnabled: false));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains("Точный брод браузера", text, StringComparison.Ordinal);
        Assert.Contains("Сложность (для своих)", text, StringComparison.Ordinal);
        Assert.Contains("низкая", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Рекомендуемый уровень", text, StringComparison.Ordinal);
        Assert.Contains("1", text, StringComparison.Ordinal);
        Assert.Contains("На переправе нет постоянной внутренней угрозы", text, StringComparison.Ordinal);
        Assert.Contains("Сложность (для чужих)", text, StringComparison.Ordinal);
        Assert.Contains("средняя", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("За бродом тракт становится опаснее", text, StringComparison.Ordinal);
        Assert.DoesNotContain("moderate", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("materializationReceipt", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("location_identity_index", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhereAmI_FailsClosedWhenCurrentProjectionDiffersFromMap()
    {
        var canonical = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_browser_current_mismatch",
            "Канонический двор браузера",
            "visited",
            x: 11,
            y: 4);
        var current = MortalLocationTestFixture.CreateCurrentProjection(canonical);
        current["name"] = "НЕ ПОКАЗЫВАТЬ ПОДМЕНЁННЫЙ ДВОР";
        await WriteCanonicalBrowserMortalLocationStateAsync([canonical], current, [canonical]);

        var result = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest("/where_am_i", AdvancedEnabled: false));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Местоположение неизвестно", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("НЕ ПОКАЗЫВАТЬ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Канонический двор браузера", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LocationStorages_ProjectsAcceptedItemsAndRequiresExactLocationSelector()
    {
        var canonical = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_browser_storage",
            "Двор браузерного ларя",
            "visited",
            x: 14,
            y: 5);
        canonical["locationStorages"] = new JsonArray(
            MortalLocationTestFixture.CreateStorageMetadata(
                "storage_browser_chest",
                "Ларь под навесом",
                hasFullAccess: true));
        canonical["materialization"]!["sections"]!["storageMetadata"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };
        MortalLocationTestFixture.ResealCanonicalLocation(canonical);
        var accepted = MortalItemTestFixture.CreateCanonicalRoot("itm_browser_location_storage");
        accepted["name"] = "Принятый журнал из ларя";
        MortalItemTestFixture.ResealCanonical(accepted);
        var rejected = MortalItemTestFixture.CreateRawRoot(
            creationRef: "new_item_browser_location_storage",
            materializationId: "mat_item_browser_location_storage");
        rejected["name"] = "PRIVATE_RAW_BROWSER_LOCATION_SCROLL";
        var current = MortalLocationTestFixture.CreateCurrentProjection(canonical);
        current["locationStorages"]![0]!["contents"] = new JsonArray(accepted, rejected);
        await WriteCanonicalBrowserMortalLocationStateAsync([canonical], current, [canonical]);

        var result = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest("/локации хранилища loc_browser_storage"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains("Принятый журнал из ларя", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_RAW_BROWSER_LOCATION_SCROLL", payload, StringComparison.Ordinal);

        var wrongCase = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest("/локации хранилища LOC_BROWSER_STORAGE"));
        Assert.DoesNotContain(
            "Принятый журнал из ларя",
            CollectBlockText(wrongCase.Blocks),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LocationDetail_DerivesOneWayExitFromAcceptedCanonicalLink()
    {
        var source = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_browser_link_source",
            "Площадь у башни",
            "visited",
            x: 20,
            y: 7);
        var target = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_browser_link_target",
            "Северная башня",
            "discovered",
            x: 21,
            y: 7);
        var link = MortalLocationTestFixture.CreateCanonicalLink(
            "loc_browser_link_source",
            "loc_browser_link_target");
        link["directionLabel"] = "на север";
        link["description"] = "Тропа поднимается вдоль старой стены.";
        await WriteCanonicalBrowserMortalLocationStateAsync(
            [source, target],
            MortalLocationTestFixture.CreateCurrentProjection(source),
            [source, target],
            [link]);

        var sourceResult = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest("/локации локация loc_browser_link_source"));
        var sourceText = CollectBlockText(sourceResult.Blocks);

        Assert.Equal(CommandExecutionState.Completed, sourceResult.State);
        Assert.Contains("Выходы", sourceText, StringComparison.Ordinal);
        Assert.Contains("Северная башня", sourceText, StringComparison.Ordinal);
        Assert.Contains("на север", sourceText, StringComparison.Ordinal);
        Assert.Contains("Тропа поднимается вдоль старой стены", sourceText, StringComparison.Ordinal);

        var targetResult = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest("/локации локация loc_browser_link_target"));
        var targetText = CollectBlockText(targetResult.Blocks);
        Assert.DoesNotContain("Площадь у башни", targetText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/квесты квест quest_winged_seal", "Квест: Печать с крыльями", "вернуть письмо")]
    [InlineData("/навыки навык skill_arcane_sense", "Навык: Чувство магических потоков", "Видит слабые печати")]
    [InlineData("/фракции фракция faction_city_watch", "Фракция: Городская стража", "удерживает северные ворота")]
    [InlineData("/локации локация loc_square", "Локация: Старая площадь", "тёмным фонтаном")]
    [InlineData("/чужие_нити нить rival_arc_moonlit_claimant", "Чужая нить: Лунный претендент", "руническую перчатку")]
    [InlineData("/коррективы_хранителя корректировка correction_valmont_slot", "Корректива Хранителя: Спорный слот вмешательства", "Азалия")]
    [InlineData("/доступ_к_хранилищам хранилище storage_valmont_private_desk", "Доступ к хранилищу: Приватный письменный стол", "ключ от верхнего ящика")]
    [InlineData("/транспорт транспорт vehicle_gray_horse", "Транспорт: Серый конь", "Активен")]
    public async Task ExecuteAsync_MortalReferenceDetail_RendersOneSelectedRecordWithoutRawJson(
        string command,
        string expectedTitle,
        string expectedDetail)
    {
        await PrepareRichMortalReferenceDetailFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains(expectedTitle, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedDetail, text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        AssertNoFlattenedStructuredDetails(result);
        Assert.DoesNotContain("game_state/", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_QuestDetail_LocalizesRichQuestFieldsAndHidesHiddenNotes()
    {
        await _fs.WriteFileAtomicAsync("game_state/quests/regular_quests.json", """
        {
          "quests": [
            {
              "questId": "quest_archive_escape",
              "questName": "Тайный выход из архива",
              "status": "Active",
              "questGiver": "Серафина",
              "description": "Найти безопасный проход из архива до смены караула.",
              "questSteps": [
                {
                  "stepTitle": "Проверить боковую дверь",
                  "description": "Замок скрипит, но ключ должен подойти."
                }
              ],
              "visibleClues": [
                "На полу остался след воска.",
                "Ветер тянет из-под книжного шкафа."
              ],
              "failureConditions": [
                "Караул услышит шум в архиве."
              ],
              "completionConditions": [
                "Герой выходит к старому мосту с письмом."
              ],
              "relatedNpcRefs": [
                { "npcName": "Мира Ключница", "role": "знает запасной ключ" }
              ],
              "recommendedActions": [
                "Попросить Миру отвлечь караул."
              ],
              "rewards": {
                "items": [
                  {
                    "displayName": "Ключ архивного мастера",
                    "visibleLore": "На ключе виден герб старого архива.",
                    "route": "Скрытый маршрут выдачи награды",
                    "requestId": "PRIVATE_BROWSER_QUEST_REWARD_REQUEST",
                    "removedItemId": "PRIVATE_BROWSER_QUEST_REMOVED_ITEM",
                    "destinationContainerId": "PRIVATE_BROWSER_QUEST_DESTINATION_CONTAINER",
                    "currentContentsPath": "PRIVATE_BROWSER_QUEST_CURRENT_CONTENTS_PATH",
                    "removeInventoryItems": [
                      { "itemName": "PRIVATE_BROWSER_QUEST_REMOVAL" }
                    ]
                  }
                ],
                "futureBlessing": {
                  "visibleMeaning": "Архивный мастер вспомнит о герое.",
                  "operatorPacket": {
                    "kind": "mortal_location_materialization_repair",
                    "title": "Скрытая операторская починка локации",
                    "rawCoordinate": "worldMapUpdates.newLocations[0]",
                    "targetFiles": [ "game_state/world/world_map.json" ]
                  }
                }
              },
              "detailsLog": [
                {
                  "summary": "Мира нашла след архивной печати.",
                  "materializationReceipt": "PRIVATE_BROWSER_QUEST_LOG_RECEIPT",
                  "creationRef": "PRIVATE_BROWSER_QUEST_LOG_CREATION_REF",
                  "NPCInventoryRemovals": [
                    { "itemName": "PRIVATE_BROWSER_QUEST_LOG_REMOVAL" }
                  ]
                }
              ],
              "hiddenGmNote": "Скрытая развязка: ключ у капитана."
            }
          ]
        }
        """);

        var command = "/квесты квест quest_archive_escape";
        var directResult = await BuildDirectMigratedResultAsync(command);
        var directText = CollectBlockText(directResult.Blocks);
        var directPayload = SerializeResult(directResult);
        Assert.DoesNotContain("Скрытый маршрут выдачи награды", directText, StringComparison.Ordinal);
        Assert.DoesNotContain("Скрытая операторская починка локации", directPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("mortal_location_materialization_repair", directPayload, StringComparison.Ordinal);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));

        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains("Квест: Тайный выход из архива", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Этапы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Проверить боковую дверь", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Улики", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("след воска", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Условия провала", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Условия завершения", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Связанные лица", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Мира Ключница", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Возможные действия", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ключ архивного мастера", text, StringComparison.Ordinal);
        Assert.Contains("На ключе виден герб старого архива", text, StringComparison.Ordinal);
        Assert.Contains("Архивный мастер вспомнит о герое", text, StringComparison.Ordinal);
        Assert.Contains("Мира нашла след архивной печати", text, StringComparison.Ordinal);
        Assert.DoesNotContain("деталь", text, StringComparison.OrdinalIgnoreCase);
        AssertNoFlattenedStructuredDetails(result);
        Assert.DoesNotContain("hidden", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Скрытая развязка", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Скрытый маршрут выдачи награды", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_BROWSER_QUEST_", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Скрытая операторская починка локации", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("mortal_location_materialization_repair", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("rawCoordinate", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/quests", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_QuestHistoryDetail_ProjectsAcceptedItemRewardsAndHistoricalReasons()
    {
        await _fs.WriteFileAtomicAsync("game_state/quests/quest_history.json", """
        {
          "questHistory": [
            {
              "questId": "quest_caravan_browser",
              "questName": "Караван у северной башни",
              "outcome": "completed"
            }
          ],
          "questRewards": [
            {
              "questId": "quest_caravan_browser",
              "itemsReceived": [
                {
                  "itemId": "itm_caravan_seal_browser",
                  "displayName": "Печать северного каравана"
                },
                "itm_raw_false_browser_reward",
                {
                  "itemId": "itm_first_life_ring_browser",
                  "displayName": "Перстень прошлой жизни",
                  "authorityStatus": "HistoricalOnly",
                  "reason": "Перстень остался у прежнего воплощения."
                },
                "ITM_CASE_SENSITIVE_REWARD_BROWSER",
                {
                  "itemId": "itm_missing_name_only_reward_browser",
                  "itemName": "NAME_ONLY_ACCEPTED_REWARD_MARKER"
                },
                {
                  "id": "itm_caravan_seal_browser",
                  "displayName": "FORGED_ID_FIELD_REWARD_MARKER"
                },
                {
                  "itemName": "itm_caravan_seal_browser",
                  "displayName": "FORGED_ITEM_NAME_FIELD_REWARD_MARKER"
                },
                {
                  "itemId": "itm_caravan_seal_browser",
                  "displayName": "FORGED_ACCEPTED_DISPLAY_LABEL_MARKER"
                }
              ]
            }
          ]
        }
        """);
        var accepted = CreateAcceptedUiItemFromJson(
            "itm_caravan_seal_browser",
            """{"name":"Печать северного каравана","type":"QuestItem"}""");
        var caseSensitive = CreateAcceptedUiItemFromJson(
            "itm_case_sensitive_reward_browser",
            """{"name":"CASE_SENSITIVE_ACCEPTED_REWARD_MARKER","type":"QuestItem"}""");
        var nameOnly = CreateAcceptedUiItemFromJson(
            "itm_name_only_reward_browser",
            """{"name":"NAME_ONLY_ACCEPTED_REWARD_MARKER","type":"QuestItem"}""");
        var rejected = MortalItemTestFixture.CreateRawRoot(
            creationRef: "new_itm_raw_false_browser_reward",
            materializationId: "mat_itm_raw_false_browser_reward");
        rejected["name"] = "RAW_FALSE_BROWSER_QUEST_REWARD_MARKER";
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(accepted, caseSensitive, nameOnly, rejected),
                ["UpdateInventory"] = new JsonArray(rejected.DeepClone()),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var result = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest("/квесты квест quest_caravan_browser"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains("Печать северного каравана", text, StringComparison.Ordinal);
        Assert.Contains("Перстень прошлой жизни", text, StringComparison.Ordinal);
        Assert.Contains("Перстень остался у прежнего воплощения.", text, StringComparison.Ordinal);
        Assert.Contains("Предмет из истории квеста — подробности пока не записаны", text, StringComparison.Ordinal);
        Assert.DoesNotContain("RAW_FALSE_BROWSER_QUEST_REWARD_MARKER", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("CASE_SENSITIVE_ACCEPTED_REWARD_MARKER", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("NAME_ONLY_ACCEPTED_REWARD_MARKER", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("FORGED_ID_FIELD_REWARD_MARKER", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("FORGED_ITEM_NAME_FIELD_REWARD_MARKER", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("FORGED_ACCEPTED_DISPLAY_LABEL_MARKER", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("itm_caravan_seal_browser", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("itm_raw_false_browser_reward", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("itm_first_life_ring_browser", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("materializationReceipt", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_PassiveSkillDetail_RendersStructuredBonusesWithLocalizedFields()
    {
        await _fs.WriteFileAtomicAsync("game_state/player/skills_passive.json", """
        {
          "passiveSkillChanges": [
            {
              "skillId": "skill_aristocratic_etiquette",
              "skillName": "Аристократический этикет",
              "skillDescription": "Знание придворных обращений и допустимых угроз в салонах знати.",
              "type": "Utility",
              "group": "Социальные навыки",
              "structuredBonuses": [
                {
                  "targetType": "characteristic",
                  "characteristic": "persuasion",
                  "valueType": "Flat",
                  "value": 1,
                  "source": "Аристократический этикет",
                  "summary": "Убеждение +1 в сценах с дворянами"
                }
              ],
              "playerStatBonus": "Убеждение +1 в сценах с дворянами"
            }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/навыки навык skill_aristocratic_etiquette"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Структурные бонусы", text, StringComparison.OrdinalIgnoreCase);
        var dossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "skill" &&
            block.Title.Contains("Аристократический этикет", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dossier.Sections, static section => section.Id == "bonuses" && section.Title == "Структурные бонусы");
        Assert.Contains("Тип цели", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("характеристика", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Характеристика", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Убеждение", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тип значения", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("плоский бонус", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("persuasion", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("targetType", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("???", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ActiveSkillDetail_LocalizesScalingCharacteristic()
    {
        await _fs.WriteFileAtomicAsync("game_state/player/skills_active.json", """
        {
          "activeSkillChanges": [
            {
              "skillId": "skill_salon_pressure",
              "skillName": "Салонное давление",
              "skillDescription": "Принуждает собеседника уступить, не переходя к открытому конфликту.",
              "category": "Utility",
              "level": 2,
              "scalingCharacteristic": "persuasion",
              "actionName": "Салонное давление",
              "actionDescription": "Выдавить уступку угрозой, спрятанной в любезной фразе.",
              "isActivatedEffect": true,
              "damageType": "psychic",
              "baseDamage": 0,
              "range": "conversation",
              "actionCost": "main",
              "actionPointCost": 1,
              "cooldown": 0,
              "scalesValue": true,
              "scalesDuration": false,
              "scalesChance": true,
              "duration": "1 сцена"
            }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/skills skill skill_salon_pressure"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        var dossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "skill" &&
            block.Title.Contains("Салонное давление", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dossier.Sections, static section => section.Id == "combat" && section.Title == "Боевые свойства");
        Assert.DoesNotContain(result.Blocks.SelectMany(EnumerateTables), static table =>
            table.Title.Equals("Боевые свойства", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Масштабирование", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Убеждение", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Боевые свойства", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Салонное давление", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Выдавить уступку угрозой", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Стоимость действия", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("основное действие", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Масштабирует шанс", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("persuasion", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("деталь:", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("action Name", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("action Description", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("is Activated Effect", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_FactionDetail_RendersCuratedPlayerFacingFieldsWithoutTechnicalDump()
    {
        await SeedRichMortalReferenceDetailFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/factions/faction_core.json", """
        {
          "factions": [
            {
              "factionId": "faction_city_watch",
              "name": "Городская стража",
              "description": "Стража удерживает северные ворота и ищет свидетелей.",
              "image_prompt": "A medieval guard barracks with blue banners",
              "factionColor": "#c79a3b",
              "isPlayerFaction": false,
              "developmentArchetype": "Economic",
              "level": 3,
              "reputation": 180,
              "reputationDescription": "Смотрят на героя как на полезного свидетеля.",
              "playerRank": "доверенный свидетель",
              "factionStrength": 220,
              "powerProfile": {
                "military": 42,
                "economic": 31,
                "social": 27,
                "covert": 12,
                "logistics": 18,
                "stability": 35
              },
              "metaResources": {
                "coins": {
                  "displayName": "Монеты",
                  "currentStock": 450,
                  "incomePerTurn": 25,
                  "upkeepPerTurn": 7
                }
              },
              "ranks": [
                {
                  "rankName": "Доверенный свидетель",
                  "branch": "Городская стража",
                  "benefits": [
                    "может просить патруль о помощи"
                  ]
                }
              ]
            }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/фракции фракция faction_city_watch"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Contains("Фракция: Городская стража", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Стража удерживает северные ворота", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Репутация", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Смотрят на героя как на полезного свидетеля", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сила фракции", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Монеты", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("может просить патруль о помощи", text, StringComparison.OrdinalIgnoreCase);

        foreach (var forbidden in new[]
                 {
                     "деталь:",
                     "image_prompt",
                     "factionColor",
                     "#c79a3b",
                     "isPlayerFaction",
                     "developmentArchetype",
                     "A medieval guard barracks"
                 })
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(forbidden, payload, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Transport_TranslatesCanonicalTypeAndAvailabilityInTableCells()
    {
        await SeedCanonicalMortalSummaryFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/транспорт"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "transport" &&
            block.Title.Equals("Транспорт", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Серый конь", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ездовое животное", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Активен", text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("mount", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("active", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConsoleNpcInspectionSource_UsesJournalFallbackProjection()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "UI",
            "ExplorerMode",
            "ExplorerMode.Npcs.ListAndDetails.cs"));

        Assert.Contains("NpcJournalFallbackProjection.ReadAsync(_stateManager", source, StringComparison.Ordinal);
        Assert.Contains("ShowNpcJournalFallback", source, StringComparison.Ordinal);
        Assert.Contains("BuildConsoleRows", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/help")]
    [InlineData("/status")]
    [InlineData("/inv")]
    [InlineData("/chaos_sea")]
    [InlineData("/shining_abode")]
    [InlineData("/spiritual_combat_help")]
    [InlineData("/spiritual_action")]
    public async Task ExecuteAsync_RepresentativeMigratedCommands_MatchDirectDtoBuilders(string command)
    {
        await PrepareRepresentativeMigratedCommandFilesAsync();

        var expected = await BuildDirectMigratedResultAsync(command, advancedEnabled: true);
        var actual = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command, AdvancedEnabled: true));

        Assert.True(
            JsonNode.DeepEquals(ToJsonNode(expected), ToJsonNode(WithoutInteractiveSession(actual))),
            $"Web command service diverged from the shared DTO builder for {command}.");
    }

    [Fact]
    public async Task ExecuteAsync_Status_UsesPlayerFacingRealmLabel()
    {
        await SeedUniversalMetaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/status"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Царство", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Море Хаоса", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Chaos Sea", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Realm", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Status_LocalizesWorldTimeForPlayer()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Смертный мир",
          "currentIncarnation": 2
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_time.json", """
        {
          "year": 124,
          "monthName": "Month of Beginnings",
          "dayOfMonth": 1,
          "timeOfDay": "08:15"
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/статус"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Время мира", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Месяц Начал", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("08:15", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Month of Beginnings", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Status_RendersMortalDetailsWithoutRawJson()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "soulFormDescription": "Серебристый силуэт с руническими прожилками",
          "currentRealm": "Смертный мир",
          "currentIncarnation": 2,
          "inkFeathers": { "current": 80, "total": 120 },
          "enlightenment": { "currentTier": "Ученик", "experience": 42 }
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/core/player_status.json", """
        {
          "healthPercentage": "85%",
          "energyPercentage": "60%",
          "poisePercentage": "95%",
          "currentCondition": "Лёгкое недомогание",
          "activeConditions": [
            "Головная боль после тяжёлых снов",
            "Магический резонанс"
          ],
          "money": 500
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/transformation.json", """
        {
          "playerCharacterNameChange": "Асуран де Вальмонт",
          "playerClassChange": "Аристократ-маг",
          "playerRaceChange": "Человек"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/experience.json", """
        {
          "level": 2,
          "totalExperience": 45,
          "experienceForNextLevel": 100,
          "experienceGained": 7,
          "playerEffortTrackerChange": {
            "lastUsedCharacteristic": "perception",
            "consecutivePartialSuccesses": 2
          }
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/weight_calc.json", """
        {
          "totalWeight": 17,
          "maxWeight": 30,
          "isOverloaded": false,
          "additionalEnergyExpenditure": 1
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/stealth.json", """
        {
          "isHidden": true,
          "detectionLevel": 35,
          "description": "Слуги пока не заметили движение у двери."
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/status_changes.json", """
        {
          "moneyChange": 25,
          "currentHealthChange": -10,
          "currentEnergyChange": 5,
          "currentPoiseChange": -3,
          "statsIncreased": [ "perception" ],
          "statsDecreased": [ "strength" ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/effects.json", """
        {
          "effects": [
            {
              "effectId": "runic_resonance",
              "effectName": "Магический резонанс",
              "effectType": "buff",
              "value": "+2",
              "duration": 3,
              "sourceSkill": "Руническая перчатка",
              "targetTypeDisplayName": "Восприятие",
              "effectDescription": "Перчатка отзывается на владельца и усиливает ощущение магических следов."
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/wounds.json", """
        {
          "wounds": [
            {
              "woundName": "Рассечённая ладонь",
              "severity": "light",
              "descriptionOfEffects": "Мешает уверенно держать тяжёлое оружие.",
              "healingState": {
                "currentState": "перевязана",
                "treatmentProgress": 1,
                "progressNeeded": 3
              }
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/custom_states.json", """
        {
          "states": [
            {
              "stateName": "Нервное напряжение",
              "currentValue": 40,
              "maxValue": 100,
              "description": "Сон оставил неприятный след.",
              "progressionRule": {
                "changePerTurn": "+5",
                "description": "растёт при опасности"
              }
            }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/status"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.Empty(result.Blocks.SelectMany(EnumerateKeyValueGrids));
        Assert.DoesNotContain(result.Blocks.SelectMany(EnumeratePanels), static panel =>
            panel.Title is "Статус" or "Активные состояния" or "Прогресс" or "Ресурсы и нагрузка" or "Скрытность");
        Assert.DoesNotContain(result.Blocks.SelectMany(EnumerateEntityDossiers), static dossier =>
            dossier.EntityType == "panel" &&
            dossier.Title is "Статус" or "Активные состояния" or "Прогресс" or "Ресурсы и нагрузка" or "Скрытность");
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static dossier =>
            dossier.EntityType == "status-overview" &&
            dossier.Title == "Статус");

        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Асуран де Вальмонт", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Деньги", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("500", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Вес", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("17/30", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Скрытность", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Незамечен", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Последние изменения", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Здоровье", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("-10", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Повышены", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Восприятие", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Понижены", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сила", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Активные эффекты", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Магический резонанс", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Рассечённая ладонь", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Нервное напряжение", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Status_ExposesStructuredEffectDetailActions()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Смертный мир",
          "currentIncarnation": 2
        }
        """);
        await SeedMortalEffectsDetailStateAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/статус"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Активные эффекты", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Магический резонанс", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Actions, action =>
            action.Label.Contains("Магический резонанс", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/эффекты эффект resonance_1", StringComparison.OrdinalIgnoreCase) &&
            action.Style == UiActionStyle.Secondary &&
            action.RequiresConfirmation == false);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("game_state/", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Stats_RendersCharacteristicsWithoutRawJson()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/misc/characteristics.json", """
        {
          "setCharacteristics": {
            "strength": 3,
            "perception": 5,
            "willpower": 4
          }
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/computed_characteristics.json", """
        {
          "healthMax": 120,
          "carryWeight": 18,
          "arcaneFocus": 7,
          "base": {
            "strength": 5,
            "dexterity": 7,
            "constitution": 6,
            "intelligence": 13,
            "wisdom": 10,
            "faith": 3,
            "attractiveness": 11,
            "trade": 6,
            "persuasion": 9,
            "perception": 12,
            "luck": 7,
            "speed": 6
          },
          "equipmentBonuses": {
            "magicFlowSense": 2,
            "arcaneLore": 1,
            "stealth": 1,
            "aristocraticReputation": 3
          },
          "temporaryModifiers": [
            {
              "source": "Головная боль после тяжёлых снов",
              "target": "perception",
              "value": -1,
              "expiresAt": "полдень"
            }
          ],
          "final": {
            "strength": 5,
            "dexterity": 7,
            "constitution": 6,
            "intelligence": 13,
            "wisdom": 10,
            "faith": 3,
            "attractiveness": 11,
            "trade": 6,
            "persuasion": 9,
            "perception": 11,
            "luck": 7,
            "speed": 6
          }
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/stats", AdvancedEnabled: false));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Характеристики", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сила", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Восприятие", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Воля", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Расчётные показатели", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Максимум здоровья", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Грузоподъёмность", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Магический фокус", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Базовое значение", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Бонусы снаряжения", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Временные модификаторы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Итоговое значение", text, StringComparison.OrdinalIgnoreCase);

        var statsDossier = Assert.Single(
            result.Blocks.SelectMany(EnumerateEntityDossiers),
            static block => block.EntityType == "stats" &&
                block.Title.Equals("Расчётные показатели", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Blocks.SelectMany(EnumerateTables), static table =>
            table.Title.Equals("Базовые характеристики", StringComparison.OrdinalIgnoreCase) ||
            table.Title.Equals("Расчётные показатели", StringComparison.OrdinalIgnoreCase) ||
            table.Title.Equals("Основные показатели", StringComparison.OrdinalIgnoreCase));
        var basePanel = Assert.Single(
            statsDossier.Sections,
            static section => section.Title.Equals("Базовое значение", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(basePanel.Facts, static item =>
            item.Label.Equals("Ловкость", StringComparison.OrdinalIgnoreCase) &&
            item.Value.Equals("7", StringComparison.OrdinalIgnoreCase));

        var equipmentPanel = Assert.Single(
            statsDossier.Sections,
            static section => section.Title.Equals("Бонусы снаряжения", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(equipmentPanel.Facts, static item =>
            item.Label.Equals("Чувство магических потоков", StringComparison.OrdinalIgnoreCase) &&
            item.Value.Equals("2", StringComparison.OrdinalIgnoreCase));

        var modifiersPanel = Assert.Single(
            statsDossier.Sections,
            static section => section.Title.Equals("Временные модификаторы", StringComparison.OrdinalIgnoreCase));
        var modifierEntry = Assert.Single(modifiersPanel.Cards);
        var modifierFacts = EnumerateCardFacts(modifierEntry).ToList();
        Assert.Contains(modifierFacts, static item =>
            item.Label.Equals("Источник", StringComparison.OrdinalIgnoreCase) &&
            item.Value.Contains("Головная боль после тяжёлых снов", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(modifierFacts, static item =>
            item.Label.Equals("Цель", StringComparison.OrdinalIgnoreCase) &&
            item.Value.Equals("Восприятие", StringComparison.OrdinalIgnoreCase));

        var finalPanel = Assert.Single(
            statsDossier.Sections,
            static section => section.Title.Equals("Итоговое значение", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(finalPanel.Facts, static item =>
            item.Label.Equals("Восприятие", StringComparison.OrdinalIgnoreCase) &&
            item.Value.Equals("11", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("equipment Bonuses", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("temporary Modifiers", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ловкость: 7", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Сила: 5; Ловкость", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("strength:", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dexterity:", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("source:", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("target:", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhereAmI_RendersLocationContextWithoutRawJson()
    {
        await SeedUniversalMetaFilesAsync();
        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_north_gate",
            "Северные ворота",
            "visited",
            x: 4,
            y: 8);
        location["region"] = "Купеческий квартал";
        location["locationType"] = "outdoor";
        location["biome"] = "stone_city";
        location["biomeDescription"] = "Каменная городская стена и мощёная дорога под аркой.";
        location["description"] = "Под аркой пахнет мокрым камнем и конской сбруей.";
        location["features"] = new JsonArray(
            "Тайные ходы под караульней",
            "Смотровая площадка над рынком");
        location["factionControl"] = new JsonArray(new JsonObject
        {
            ["factionId"] = "fac_merchants_guild",
            ["factionName"] = "Купеческая гильдия",
            ["controlLevel"] = 62,
            ["controlType"] = "торговые патрули"
        });
        location["activeThreats"] = new JsonArray(CreateCanonicalBrowserThreat(
            "gate_pickpockets",
            "Карманники у ворот",
            "выследить владельца рунической перчатки",
            "проверяют путников у лавок"));
        location["lastEventsDescription"] = "После ночного письма стража проверяет печати на повозках.";
        location["materialization"]!["sections"]!["factionControl"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };
        location["materialization"]!["sections"]!["activeThreats"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };
        MortalLocationTestFixture.ResealCanonicalLocation(location);
        await WriteCanonicalBrowserMortalLocationStateAsync(
            [location],
            MortalLocationTestFixture.CreateCurrentProjection(location),
            [location]);
        await _fs.WriteFileAtomicAsync("game_state/world/world_time.json", """
        {
          "year": 124,
          "monthName": "Month of Beginnings",
          "dayOfMonth": 1,
          "timeOfDay": "08:15"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/weather.json", """
        {
          "weatherChange": {
            "currentState": "утренний туман",
            "description": "Сырой туман ещё держится у ворот."
          }
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/where_am_i", AdvancedEnabled: false));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Северные ворота", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Купеческий квартал", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тайные ходы под караульней", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Купеческая гильдия", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Карманники у ворот", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("выследить владельца рунической перчатки", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Месяц Начал", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Month of Beginnings", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("08:15", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сырой туман ещё держится у ворот", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "current-location" &&
            block.Title.Contains("Северные ворота", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhereAmI_SuppressesNestedLocationRepairPackets()
    {
        await SeedUniversalMetaFilesAsync();
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        location["features"] = new JsonArray
        {
            "Старинный колодец",
            new JsonObject
            {
                ["name"] = "Каменная арка",
                ["details"] = new JsonObject
                {
                    ["kind"] = "mortal_location_materialization_repair",
                    ["title"] = "PRIVATE_BROWSER_NESTED_LOCATION_REPAIR",
                    ["rawCoordinate"] = "worldMapUpdates.newLocations[0]",
                    ["targetFiles"] = new JsonArray(MortalLocationMaterializationContract.WorldMapPath)
                }
            },
            new JsonObject
            {
                ["kind"] = "mortal_location_materialization_repair",
                ["title"] = "PRIVATE_BROWSER_DIRECT_LOCATION_REPAIR",
                ["rawCoordinate"] = "worldMapUpdates.newLocations[0]",
                ["targetFiles"] = new JsonArray(MortalLocationMaterializationContract.WorldMapPath)
            },
            CreatePrivateBrowserValidationRepairRequest(),
            CreatePrivateBrowserValidationDiagnosticReport()
        };
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            MortalLocationTestFixture.CreateCurrentProjection(location).ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationTestFixture.CreateWorldMap(location).ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationTestFixture.CreateIdentityIndex(location).ToJsonString());

        var result = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest("/where_am_i", AdvancedEnabled: false));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Старинный колодец", text, StringComparison.Ordinal);
        Assert.Contains("Каменная арка", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_BROWSER", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("mortal_location_materialization_repair", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("rawCoordinate", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gmInstructions", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fullTurnResubmissionRequired", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rollbackAvailable", payload, StringComparison.OrdinalIgnoreCase);

        var mapResult = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest("/map", AdvancedEnabled: false));
        var mapPayload = SerializeResult(mapResult);
        Assert.Equal(CommandExecutionState.Completed, mapResult.State);
        Assert.DoesNotContain("PRIVATE_BROWSER", mapPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("mortal_location_materialization_repair", mapPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("rawCoordinate", mapPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gmInstructions", mapPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rollbackAvailable", mapPayload, StringComparison.OrdinalIgnoreCase);
    }

    private async Task WriteCanonicalBrowserMortalLocationStateAsync(
        IReadOnlyCollection<JsonObject> mapLocations,
        JsonObject current,
        IReadOnlyCollection<JsonObject> indexedLocations,
        IReadOnlyCollection<JsonObject>? links = null)
    {
        var canonicalLinks = links ?? Array.Empty<JsonObject>();
        var map = MortalLocationTestFixture.CreateWorldMap(mapLocations.ToArray());
        map["links"] = new JsonArray(
            canonicalLinks.Select(static link => (JsonNode?)link.DeepClone()).ToArray());
        var index = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locationEntries"] = new JsonArray(),
            ["linkEntries"] = new JsonArray()
        };
        foreach (var location in indexedLocations)
        {
            var single = MortalLocationTestFixture.CreateIdentityIndex(location);
            index["locationEntries"]!.AsArray().Add(
                single["locationEntries"]![0]!.DeepClone());
        }
        foreach (var link in canonicalLinks)
        {
            var sourceId = link["sourceLocationId"]!.GetValue<string>();
            var source = indexedLocations.Single(location =>
                string.Equals(location["locationId"]!.GetValue<string>(), sourceId, StringComparison.Ordinal));
            var single = MortalLocationTestFixture.CreateIdentityIndex(source, link);
            index["linkEntries"]!.AsArray().Add(
                single["linkEntries"]![0]!.DeepClone());
        }

        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            map.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            current.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationIdentityState.StatePath,
            index.ToJsonString());
    }

    private static JsonObject CreateCanonicalBrowserThreat(
        string threatId,
        string name,
        string longTermGoal,
        string activityName) =>
        new()
        {
            ["threatId"] = threatId,
            ["name"] = name,
            ["threatName"] = name,
            ["description"] = "Постоянная угроза в тестовой локации.",
            ["intensity"] = 3,
            ["longTermGoal"] = longTermGoal,
            ["currentActivity"] = new JsonObject
            {
                ["activityName"] = activityName,
                ["description"] = "Угроза продолжает текущую деятельность.",
                ["totalTimeCostMinutes"] = 120,
                ["timeSpentMinutes"] = 15,
                ["currentStepNumber"] = 1,
                ["totalStepsInActivity"] = 3
            },
            ["threatArchetype"] = new JsonObject
            {
                ["motivation"] = "Domination",
                ["method"] = "Covert",
                ["customMotivation"] = null,
                ["customMethod"] = null
            },
            ["impactProfile"] = new JsonObject
            {
                ["primaryTargetType"] = "Location",
                ["primaryTargetId"] = null,
                ["primaryTargetName"] = name,
                ["primaryImpact"] = "Stability",
                ["baseImpactValue"] = 2
            }
        };

    private static JsonObject CreatePrivateBrowserValidationRepairRequest() => new()
    {
        ["sessionId"] = "session_private",
        ["requestId"] = "request_private",
        ["turnNumber"] = 42,
        ["metadataDiagnosticOnly"] = false,
        ["source"] = "private source",
        ["detectedAtUtc"] = "2026-08-12T00:00:00Z",
        ["revalidationAttempt"] = 2,
        ["fullTurnResubmissionRequired"] = true,
        ["gmInstructions"] = "PRIVATE_BROWSER_LOCATION_REPAIR_REQUEST",
        ["summaryGroups"] = new JsonArray("PRIVATE_BROWSER_SUMMARY"),
        ["harnessRepairPackets"] = new JsonArray(new JsonObject
        {
            ["kind"] = "mortal_location_materialization_repair",
            ["title"] = "PRIVATE_BROWSER_WRAPPED_PACKET",
            ["targetFiles"] = new JsonArray(MortalLocationMaterializationContract.WorldMapPath)
        }),
        ["errors"] = new JsonArray(new JsonObject
        {
            ["code"] = "mortal_location_materialization_governed_field_missing",
            ["message"] = "PRIVATE_BROWSER_VALIDATION_MESSAGE"
        })
    };

    private static JsonObject CreatePrivateBrowserValidationDiagnosticReport() => new()
    {
        ["source"] = "private source",
        ["detectedAtUtc"] = "2026-08-12T00:00:00Z",
        ["reason"] = "PRIVATE_BROWSER_LOCATION_DIAGNOSTIC_REPORT",
        ["rollbackAvailable"] = true,
        ["summaryGroups"] = new JsonArray("PRIVATE_BROWSER_DIAGNOSTIC_SUMMARY"),
        ["errors"] = new JsonArray(new JsonObject
        {
            ["code"] = "accepted_turn_invalid_snapshot_baseline",
            ["message"] = "PRIVATE_BROWSER_DIAGNOSTIC_MESSAGE"
        })
    };

    [Fact]
    public async Task ExecuteAsync_Weather_RendersDetailedTimeAndWeatherWithoutRawJson()
    {
        await SeedUniversalMetaFilesAsync();
        var weatherLocation = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_weather_gate",
            "Северные ворота",
            discoveryTier: "visited");
        weatherLocation["biome"] = "каменный город";
        MortalLocationTestFixture.ResealCanonicalLocation(weatherLocation);
        await WriteCanonicalBrowserMortalLocationStateAsync(
            [weatherLocation],
            MortalLocationTestFixture.CreateCurrentProjection(weatherLocation),
            [weatherLocation]);
        await _fs.WriteFileAtomicAsync("game_state/world/world_time.json", """
        {
          "setWorldTime": {
            "year": 124,
            "monthName": "Month of Beginnings",
            "dayOfMonth": 1,
            "timeOfDay": "08:15"
          }
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/weather.json", """
        {
          "weatherChange": {
            "currentState": "туман",
            "description": "Мокрая дымка стелется вдоль мостовой.",
            "season": "ранняя весна",
            "temperature": "+6",
            "wind": "слабый северный",
            "visibility": "низкая",
            "tendency": "WORSEN",
            "mechanicalEffects": "стрельба на дальность затруднена"
          }
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/weather", AdvancedEnabled: false));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Месяц Начал", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Month of Beginnings", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("08:15", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("каменный город", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("туман", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Мокрая дымка", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ранняя весна", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("+6", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("слабый северный", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("низкая", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ухудшение", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("стрельба на дальность затруднена", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "weather" &&
            block.Title.Equals("Время и погода", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Weather_ReceiptlessCurrentLocationDoesNotExposeRawBiomeOrName()
    {
        var canonical = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_weather_private",
            "Каноническое место",
            discoveryTier: "visited");
        await WriteCanonicalBrowserMortalLocationStateAsync(
            [canonical],
            MortalLocationTestFixture.CreateCurrentProjection(canonical),
            [canonical]);
        var rejected = MortalLocationTestFixture.CreateCurrentProjection(canonical);
        rejected.Remove("materializationReceipt");
        rejected["name"] = "PRIVATE RAW WEATHER LOCATION";
        rejected["biome"] = "PRIVATE RAW WEATHER BIOME";
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            rejected.ToJsonString());
        await _fs.WriteFileAtomicAsync("game_state/world/weather.json", """
        {
          "weatherChange": {
            "currentState": "туман",
            "description": "Погода остаётся видимой."
          }
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/weather"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.DoesNotContain("PRIVATE RAW WEATHER", payload, StringComparison.Ordinal);
        Assert.Contains("Погода остаётся видимой", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Lives_RendersLifeHistoryDossierCardsWithoutRawJson()
    {
        await SeedUniversalMetaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/lives", AdvancedEnabled: false));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("История жизней", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Первая жизнь", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Инкарнация", text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static dossier =>
            dossier.EntityType == "life-history" &&
            dossier.Sections.Any(static section => section.Cards.Count > 0));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("soul_state", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Feathers_RendersFateCurrencyDossierWithoutGenericGrid()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Смертный мир",
          "currentIncarnation": 2,
          "inkFeathers": { "current": 80, "total": 120 }
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/feathers", AdvancedEnabled: false));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.Empty(result.Blocks.SelectMany(EnumerateKeyValueGrids));
        Assert.DoesNotContain(result.Blocks.SelectMany(EnumeratePanels), static panel =>
            panel.Title.Equals("Чернильные Перья", StringComparison.OrdinalIgnoreCase));

        var dossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "ink-feathers" &&
            block.Title.Equals("Чернильные Перья", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dossier.Sections, static section => section.Title == "Баланс");
        Assert.Contains(dossier.Sections, static section => section.Title == "Действия судьбы");

        var text = CollectBlockText([dossier]);
        Assert.Contains("Сейчас", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("80", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Всего накоплено", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("120", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Открыть Судьбу", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Actions, static action =>
            action.Command.Equals("/reveal_fate", StringComparison.OrdinalIgnoreCase) &&
            action.Label.Contains("Открыть Судьбу", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("currentRealm", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Chronicle_RendersReadableSectionsWithoutRawJson()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/character_chronicle.json", """
        [
          {
            "entryId": "chronicle_valmont_rebirth_001",
            "title": "Возвращение в Вальмонт",
            "summary": "Душа Асурана вновь открыла глаза в семейной библиотеке.",
            "eventType": "rebirth",
            "turnNumber": 1
          }
        ]
        """);
        await _fs.WriteFileAtomicAsync("lore/chaos_sea/player_chronicle.json", """
        {
          "entries": [
            {
              "title": "Пепельная Искра",
              "content": "Память о первой сделке с хранителем.",
              "status": "unlocked"
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/quests/plot_outline.json", """
        {
          "mainArc": {
            "summary": "Выяснить, кто управляет рынком.",
            "nextImmediateStep": "Найти связного в трактире."
          }
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/chronicle", AdvancedEnabled: false));

        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Хроника", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Возвращение в Вальмонт", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Душа Асурана вновь открыла глаза", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Пепельная Искра", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Выяснить, кто управляет рынком", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(result.Blocks.SelectMany(EnumerateEntityDossiers), static dossier =>
            dossier.EntityType == "panel" &&
            dossier.Title == "Хроника");
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static dossier =>
            dossier.EntityType == "chronicle-overview" &&
            dossier.Title == "Хроника");
        foreach (var forbidden in new[] { "JSON:", "entryId", "eventType", "character_chronicle", "player_chronicle", "plot_outline", "game_state/", ".json" })
            Assert.DoesNotContain(forbidden, payload, StringComparison.OrdinalIgnoreCase);
        AssertNoFlattenedStructuredDetails(result);
    }

    [Theory]
    [InlineData("/codex", "Первый знак", "Тестовая запись кодекса")]
    [InlineData("/achievements", "Первое испытание", "Проверить браузерный вывод")]
    [InlineData("/behavior", "Осторожный переговорщик", "избегает насилия")]
    public async Task ExecuteAsync_GenericJsonMetaCommands_RenderReadableContentWithoutRawJson(
        string command,
        string expectedTitle,
        string expectedDetail)
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/player_behavior.json", """
        {
          "playerBehaviorAssessment": {
            "dominantPattern": "Осторожный переговорщик",
            "summary": "Игрок избегает насилия и собирает сведения перед риском."
          },
          "historyManipulationCoefficient": 1.25
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/achievements.json", """
        {
          "unlockedAchievements": [
            {
              "achievementName": "Первое испытание",
              "description": "Проверить браузерный вывод",
              "status": "unlocked"
            }
          ],
          "trackedProgress": [
            {
              "achievementName": "Следующий шаг",
              "description": "Продолжить аудит команд",
              "current": 1,
              "total": 3
            }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command, AdvancedEnabled: false));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(expectedTitle, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedDetail, text, StringComparison.OrdinalIgnoreCase);
        if (command.Equals("/behavior", StringComparison.OrdinalIgnoreCase))
            Assert.Contains("1.25", text, StringComparison.OrdinalIgnoreCase);
        foreach (var forbidden in new[] { "JsonObject", "entries", "codexEntries", "unlockedAchievements", "trackedProgress", "sourceFile", ".json" })
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/codex", "codex-summary", "Кодекс")]
    [InlineData("/achievements", "achievements-summary", "Достижения")]
    public async Task ExecuteAsync_CodexAndAchievements_RenderSummaryDossiersInsteadOfLegacyPanels(
        string command,
        string expectedEntityType,
        string expectedTitle)
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/achievements.json", """
        {
          "unlockedAchievements": [
            {
              "achievementName": "Первое испытание",
              "description": "Проверить браузерный вывод",
              "status": "unlocked"
            }
          ],
          "trackedProgress": [
            {
              "achievementName": "Следующий шаг",
              "description": "Продолжить аудит команд",
              "current": 1,
              "total": 3
            }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command, AdvancedEnabled: false));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Empty(result.Blocks.SelectMany(EnumerateKeyValueGrids));
        Assert.DoesNotContain(result.Blocks.SelectMany(EnumerateEntityDossiers), dossier =>
            dossier.EntityType == "panel" &&
            dossier.Title.Equals(expectedTitle, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), dossier =>
            dossier.EntityType == expectedEntityType &&
            dossier.Title.Equals(expectedTitle, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_WorldRules_EmptyReadableJsonUsesDossierFallbackInsteadOfLegacyPanel()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync(WorldDirectiveService.ActiveDirectivesPath, "{}");

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/world_rules", AdvancedEnabled: false));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Empty(result.Blocks.SelectMany(EnumerateKeyValueGrids));
        Assert.Empty(result.Blocks.SelectMany(EnumeratePanels));
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static dossier =>
            dossier.EntityType == "metadata-empty" &&
            dossier.Title == "Досье текущего мира");
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Досье текущего мира", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("пока нет заполненных разделов", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Behavior_RendersNestedAssessmentAsDossierFacts()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/player_behavior.json", """
        {
          "playerBehaviorAssessment": {
            "dominantPattern": "Осторожный переговорщик",
            "summary": "Игрок избегает насилия и собирает сведения перед риском.",
            "confidence": "high"
          },
          "historyManipulationCoefficient": 1.25
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/behavior", AdvancedEnabled: false));

        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        var dossiers = result.Blocks.SelectMany(EnumerateEntityDossiers).ToList();
        Assert.Contains(dossiers, static dossier =>
            dossier.Title.Equals("Поведение игрока", StringComparison.OrdinalIgnoreCase));
        var cards = dossiers.SelectMany(static dossier => dossier.Sections).SelectMany(static section => section.Cards).ToList();
        var hasAssessmentCard = cards.Any(static card =>
            card.Title.Equals("Оценка поведения", StringComparison.OrdinalIgnoreCase) &&
            card.Facts.Any(static fact =>
                fact.Label.Equals("Основной паттерн", StringComparison.OrdinalIgnoreCase) &&
                fact.Value.Equals("Осторожный переговорщик", StringComparison.Ordinal)) &&
            card.Facts.Any(static fact =>
                fact.Label.Equals("Кратко", StringComparison.OrdinalIgnoreCase) &&
                fact.Value.Contains("избегает насилия", StringComparison.OrdinalIgnoreCase)));
        Assert.True(hasAssessmentCard, payload);

        Assert.DoesNotContain("Основной паттерн: Осторожный переговорщик;", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Уверенность", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("высокая", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("playerBehaviorAssessment", text, StringComparison.OrdinalIgnoreCase);
        AssertNoFlattenedStructuredDetails(result);
    }

    [Theory]
    [MemberData(nameof(PlayerDefaultReadOnlyCommands))]
    public async Task ExecuteAsync_PlayerDefaultReadOnlyCommands_RenderPlayerFacingDefaultOutput(
        string commandId,
        string command,
        ExplorerCommandGroup group)
    {
        await PreparePlayerDefaultCommandAuditFilesAsync(commandId, group);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command, AdvancedEnabled: false));

        var violations = CollectPlayerFacingOutputViolations(result);
        Assert.True(
            violations.Count == 0,
            $"{commandId} ({command}) has non-player-facing default browser output:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Theory]
    [MemberData(nameof(PlayerDefaultMutatingCommands))]
    public async Task ExecuteAsync_PlayerDefaultMutatingCommands_RenderPlayerFacingDefaultPromptOrStatus(
        string commandId,
        string command,
        ExplorerCommandGroup group)
    {
        await PreparePlayerDefaultCommandAuditFilesAsync(commandId, group);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command, AdvancedEnabled: false));

        var violations = CollectPlayerFacingOutputViolations(result, allowRequiresInput: true);
        Assert.True(
            violations.Count == 0,
            $"{commandId} ({command}) has non-player-facing default browser prompt/status:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public async Task ExecuteAsync_RepresentativeMigratedDto_RendersThroughConsoleAdapter()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_combat_help"));
        var console = new TestExplorerConsole();

        ExplorerCommandResultConsoleRenderer.Render(console, result);

        Assert.NotEmpty(console.Rendered);
    }

    [Fact]
    public async Task ExecuteAsync_MathCommandWithExpression_ReturnsBrowserDto()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/math 2 + 3 * 5"));

        Assert.Equal("/math 2 + 3 * 5", result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.Title.Contains("Математик", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock raw && raw.Title.Contains("JSON", StringComparison.OrdinalIgnoreCase));
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Результат", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("17", text, StringComparison.Ordinal);
        Assert.DoesNotContain("JSON результата", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_MathCommandWithVariables_RendersDossierCardsWithoutLegacyTables()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/math base * tier base=10 tier=3"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(
            result.Blocks.SelectMany(EnumerateEntityDossiers),
            static dossier => dossier.EntityType == "panel");

        var dossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "math-assistant");
        Assert.Contains(dossier.Sections, static section => section.Title.Equals("Переменные", StringComparison.OrdinalIgnoreCase));

        var text = CollectBlockText([dossier]);
        Assert.Contains("Результат", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("30", text, StringComparison.Ordinal);
        Assert.Contains("base", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tier", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_MathCommandWithRounding_LocalizesRoundingMode()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/math 10 / 3 rounding=floor decimalPlaces=0"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("вниз", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("знаков: 0", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Floor", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MathCommandWithInvalidExpression_HidesInternalErrorCode()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/math 2 apples + 3"));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Формула не вычислена", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Формула не разобрана", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unexpected_token", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_GalleryWithImages_ReturnsImageBlocks()
    {
        WriteSessionImage("images/npcs/ashen_knight.png");
        WriteSessionImage("images/scenes/scene_001.webp");

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/gallery"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var images = result.Blocks.OfType<UiImageBlock>().ToList();
        Assert.Equal(2, images.Count);
        Assert.All(images, image =>
        {
            Assert.StartsWith("/api/media/", image.Url, StringComparison.Ordinal);
            Assert.StartsWith("images/", image.RelativePath, StringComparison.Ordinal);
        });
        Assert.Contains(images, image => image.Title.Contains("ashen knight", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/validate", "Валидация")]
    [InlineData("/world_setup", "Подготовка следующего мира")]
    [InlineData("/distribute", "Распределение характеристик")]
    [InlineData("/companion_directive", "Директивы компаньонов")]
    [InlineData("/faction_directive", "Директивы фракций")]
    [InlineData("/craft", "Ремесло")]
    [InlineData("/abode_offering", "Подношение Обители")]
    [InlineData("/found_guardian_mantle", "Основание собственной мантии")]
    [InlineData("/spiritual_action", "Духовное действие")]
    public async Task ExecuteAsync_LifecycleAndLocalTurnCommands_ReturnProtocolDtos(string command, string expectedRussianLabel)
    {
        await PrepareLifecycleAndLocalTurnFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.NotEqual(CommandExecutionState.Blocked, result.State);
        Assert.NotEmpty(result.Blocks);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(expectedRussianLabel, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Локальный ход", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Validate_RendersIssueDossierWithoutTechnicalPathsOrCodes()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{ invalid json");

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/validate"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static dossier =>
            dossier.EntityType == "validation-report" &&
            dossier.Title.Contains("Валидация", StringComparison.OrdinalIgnoreCase));

        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Ошибка", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Невалидный JSON", text, StringComparison.OrdinalIgnoreCase);

        var payload = SerializeResult(result);
        foreach (var forbidden in new[] { "game_state/", ".json", "invalid_json_file", "StateJson", "ProtocolViolation" })
        {
            Assert.DoesNotContain(forbidden, payload, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ExecuteAsync_LocalTurnCommandWithActiveGmTurn_ShowsPendingTurnProtocolState()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "session_web",
          "requestId": "request_web",
          "turnNumber": 12,
          "playerAction": "Тестовый ход",
          "timestamp": "2026-05-20T00:00:00Z"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", """
        {
          "sessionId": "session_web",
          "requestId": "request_web",
          "turnNumber": 12,
          "files": {}
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/world_setup"));

        Assert.Equal(CommandExecutionState.Pending, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Активный ход GM", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("input/turn_request.json", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/control/pending_turn_snapshot.json", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GM-turn protocol", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Browser-write", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rollback", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("snapshot", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Артефакты протокола", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WorldSetup_RendersReadableSummaryWithoutRawJsonOrPaths()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync(WorldDirectiveService.PendingSetupPath, """
        {
          "mode": "manual",
          "worldDirectives": {
            "worldTitle": "Королевство пепельных колоколов",
            "settingSummary": "Зимняя страна с падающими династиями и церковными интригами.",
            "startingSituation": "Душа должна родиться в доме изгнанного нотариуса.",
            "mandatoryThemes": ["память рода", "цена клятвы"],
            "forbiddenElements": ["лёгкий комедийный тон"]
          }
        }
        """);
        await _fs.WriteFileAtomicAsync(ScenarioCoreService.ManifestPath, """
        {
          "sourcePath": "game_state/control/incarnation_world_setup.json",
          "scenarioCore": {
            "summary": "Падение дома начинается с исчезновения семейной печати.",
            "playerRole": "наследник с чужими воспоминаниями",
            "mainConflict": "городские кланы спорят за право назначить регента"
          },
          "candidateAssertions": ["мир должен остаться мрачным"],
          "scenarioCoreAssertions": [
            { "assertion": "печать важна для первой арки", "status": "confirmed" }
          ],
          "openCorrectionSlots": ["уточнить первую фракцию-союзника"]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/world_setup"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static dossier =>
            dossier.EntityType == "world-setup" &&
            dossier.Title.Contains("Подготовка следующего мира", StringComparison.OrdinalIgnoreCase));

        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Королевство пепельных колоколов", text, StringComparison.Ordinal);
        Assert.Contains("Зимняя страна", text, StringComparison.Ordinal);
        Assert.Contains("Падение дома", text, StringComparison.Ordinal);
        Assert.Contains("память рода", text, StringComparison.Ordinal);

        var payload = SerializeResult(result);
        foreach (var forbidden in new[] { "game_state/", ".json", "sourcePath", "worldDirectives", "scenarioCoreAssertions", "openCorrectionSlots", "currentRealm" })
        {
            Assert.DoesNotContain(forbidden, payload, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ExecuteAsync_PromptCommand_AttachesBrowserPromptSessionAndLocalLock()
    {
        await SeedUniversalMetaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/world_setup",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.False(string.IsNullOrWhiteSpace(result.InteractiveSession.SessionId));
        Assert.Equal("/api/explorer/prompt-sessions/submit", result.InteractiveSession.SubmitEndpoint);
        Assert.Equal("/api/explorer/prompt-sessions/cancel", result.InteractiveSession.CancelEndpoint);
        Assert.True(result.InteractiveSession.RequiresLocalUiLock);
        Assert.True(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_ValidAnswers_CompletesWithoutConsoleInputAndReleasesLock()
    {
        await SeedUniversalMetaFilesAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/world_setup",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["world_setup_mode"] = JsonValue.Create("create_or_edit"),
                ["world_title"] = JsonValue.Create("Королевство пепельных колоколов"),
                ["world_directives"] = JsonValue.Create("Тёмное фэнтези, падшие династии, запрет на лёгкий тон.")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        Assert.Empty(completed.Prompts);
        Assert.Null(completed.InteractiveSession);
        var text = CollectBlockText(completed.Blocks);
        Assert.Contains("Подготовка мира записана", text, StringComparison.OrdinalIgnoreCase);
        var submittedJson = Assert.IsType<UiRawJsonBlock>(completed.Blocks.Last());
        Assert.Equal("Королевство пепельных колоколов", submittedJson.Json?["worldDirectives"]?["worldTitle"]?.GetValue<string>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_WorldSetupCreate_WritesPendingSetupAndScenarioCore()
    {
        await SeedUniversalMetaFilesAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/world_setup",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["world_setup_mode"] = JsonValue.Create("create_or_edit"),
                ["world_title"] = JsonValue.Create("Королевство пепельных колоколов"),
                ["world_directives"] = JsonValue.Create("Тёмное фэнтези, падшие династии, запрет на лёгкий тон.")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));

        var setup = JsonNode.Parse((await _fs.ReadFileAsync(WorldDirectiveService.PendingSetupPath))!)!.AsObject();
        Assert.Equal("manual", setup["mode"]!.GetValue<string>());
        Assert.Equal("Королевство пепельных колоколов", setup["worldDirectives"]!["worldTitle"]!.GetValue<string>());
        Assert.Contains("Тёмное фэнтези", setup["worldDirectives"]!["settingSummary"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(_fs.FileExists(ScenarioCoreService.ManifestPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_WorldSetupClear_DeletesPendingSetupAndScenarioCore()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync(WorldDirectiveService.PendingSetupPath, """
        {
          "mode": "manual",
          "worldDirectives": { "worldTitle": "Старый мир" }
        }
        """);
        await _fs.WriteFileAtomicAsync(ScenarioCoreService.ManifestPath, """
        {
          "sourcePath": "game_state/control/incarnation_world_setup.json",
          "candidateAssertions": [],
          "scenarioCoreAssertions": [],
          "openCorrectionSlots": []
        }
        """);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/world_setup",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["world_setup_mode"] = JsonValue.Create("clear")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        Assert.False(_fs.FileExists(WorldDirectiveService.PendingSetupPath));
        Assert.False(_fs.FileExists(ScenarioCoreService.ManifestPath));
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_Distribute_AppliesAllocationsAndReleasesLock()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/player/stat_points.json", "{ \"unspentStatPoints\": 3 }");
        await _fs.WriteFileAtomicAsync("game_state/misc/characteristics.json", """
        {
          "strength": 1,
          "wisdom": 2
        }
        """);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/distribute",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["stat_strength"] = JsonValue.Create("2"),
                ["stat_wisdom"] = JsonValue.Create("1")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var stats = JsonNode.Parse((await _fs.ReadFileAsync("game_state/misc/characteristics.json"))!)!.AsObject();
        var points = JsonNode.Parse((await _fs.ReadFileAsync("game_state/player/stat_points.json"))!)!.AsObject();
        Assert.Equal(3, stats["strength"]!.GetValue<int>());
        Assert.Equal(3, stats["wisdom"]!.GetValue<int>());
        Assert.Equal(0, points["unspentStatPoints"]!.GetValue<int>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_DistributeOverBudget_KeepsSessionOpenAndDoesNotMutate()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/player/stat_points.json", "{ \"unspentStatPoints\": 1 }");
        await _fs.WriteFileAtomicAsync("game_state/misc/characteristics.json", "{ \"strength\": 1 }");
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/distribute",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var validation = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["stat_strength"] = JsonValue.Create("2")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.RequiresInput, validation.State);
        Assert.NotNull(validation.InteractiveSession);
        Assert.Contains(validation.Notifications, static item =>
            item.Severity == UiNotificationSeverity.Error &&
            item.Message.Contains("Недостаточно", StringComparison.OrdinalIgnoreCase));
        var stats = JsonNode.Parse((await _fs.ReadFileAsync("game_state/misc/characteristics.json"))!)!.AsObject();
        var points = JsonNode.Parse((await _fs.ReadFileAsync("game_state/player/stat_points.json"))!)!.AsObject();
        Assert.Equal(1, stats["strength"]!.GetValue<int>());
        Assert.Equal(1, points["unspentStatPoints"]!.GetValue<int>());
        Assert.True(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Theory]
    [InlineData("/companion_directive", "companion-directives", "Директивы компаньонов")]
    [InlineData("/faction_directive", "faction-directives", "Директивы фракций")]
    [InlineData("/craft", "craft-requests", "Ремесло")]
    public async Task ExecuteAsync_MortalActionPromptScreens_RenderDossiersWithoutTechnicalTables(
        string command,
        string entityType,
        string expectedTitle)
    {
        await PrepareMortalActionPromptFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Contains(expectedTitle, text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), dossier =>
            dossier.EntityType == entityType &&
            dossier.Title.Contains(expectedTitle, StringComparison.OrdinalIgnoreCase));

        foreach (var forbidden in new[]
        {
            "client-authored",
            "JSON",
            "playerCompanionDirective",
            "playerStrategyDirective",
            "stat_points",
            "npc_core",
            "faction_core",
            "recipes.json",
            "game_state/"
        })
        {
            Assert.DoesNotContain(forbidden, payload, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Distribute_RendersDossierCardsAndPlayerFacingPrompts()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/player/stat_points.json", "{ \"unspentStatPoints\": 3 }");
        await _fs.WriteFileAtomicAsync("game_state/misc/characteristics.json", """
        {
          "strength": 1,
          "wisdom": 2
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/distribute"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var dossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "stat-distribution" &&
            block.Title == "Распределение характеристик");
        var text = CollectBlockText([dossier]);
        Assert.Contains("Доступно очков", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сила", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Мудрость", text, StringComparison.OrdinalIgnoreCase);
        var promptText = CollectPromptAndNotificationText(result);
        Assert.Contains(result.Prompts, static prompt => prompt.Id == "stat_strength");
        Assert.Contains(result.Prompts, static prompt => prompt.Id == "stat_wisdom");
        Assert.DoesNotContain("JSON", text + "\n" + promptText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("strength", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stat_points", SerializeResult(result), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_CompanionDirective_UpdatesNpcCore()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "npcs": [
            { "npcId": "npc_1", "name": "Мирра", "progressionType": "Companion" }
          ]
        }
        """);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/companion_directive",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["companion_id"] = JsonValue.Create("npc_1"),
                ["companion_directive"] = JsonValue.Create("Оберегай раненых и не вступай в бой первым.")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var npc = JsonNode.Parse((await _fs.ReadFileAsync("game_state/npcs/npc_core.json"))!)!.AsObject();
        Assert.Equal("Оберегай раненых и не вступай в бой первым.", npc["npcs"]![0]!["playerCompanionDirective"]!.GetValue<string>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_FactionDirective_UpdatesFactionCore()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/factions/faction_core.json", """
        {
          "factions": [
            { "factionId": "faction_1", "name": "Серые знамена", "isPlayerFaction": true }
          ]
        }
        """);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/faction_directive",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["faction_id"] = JsonValue.Create("faction_1"),
                ["faction_directive"] = JsonValue.Create("Укрепить северные заставы и искать союз с ремесленниками.")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var factions = JsonNode.Parse((await _fs.ReadFileAsync("game_state/factions/faction_core.json"))!)!.AsObject();
        Assert.Equal("Укрепить северные заставы и искать союз с ремесленниками.", factions["factions"]![0]!["playerStrategyDirective"]!.GetValue<string>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_Craft_WritesPendingCraftRequest()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/inventory/recipes.json", """
        {
          "recipes": [
            { "recipeId": "healing_salve", "recipeName": "Лечебная мазь", "craftedItemName": "Припарка" }
          ]
        }
        """);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/craft",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["recipe_id"] = JsonValue.Create("healing_salve"),
                ["craft_intent"] = JsonValue.Create("Сделать припарку из трав, не расходуя редкие реагенты.")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var request = JsonNode.Parse((await _fs.ReadFileAsync("game_state/control/pending_craft_request.json"))!)!.AsObject();
        Assert.Equal("healing_salve", request["recipeId"]!.GetValue<string>());
        Assert.Equal("Сделать припарку из трав, не расходуя редкие реагенты.", request["craftIntent"]!.GetValue<string>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_ShiningTreasuryDeposit_UpdatesTreasuryAndSoulFeathers()
    {
        await SeedShiningAbodeFilesAsync();
        _fs.DeleteFile("game_state/control/pending_shining_abode_actions.json");
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_treasury",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["treasury_operation"] = JsonValue.Create("deposit"),
                ["treasury_amount"] = JsonValue.Create(4)
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var shining = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json"))!)!.AsObject();
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        Assert.Equal(24, shining["treasury"]!["depositedInkFeathers"]!.GetValue<int>());
        Assert.Equal(20, soul["inkFeathers"]!["current"]!.GetValue<int>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_ShiningTreasuryDeposit_IgnoresNonCostCorePendingAction()
    {
        await SeedShiningAbodeFilesAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_treasury",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["treasury_operation"] = JsonValue.Create("deposit"),
                ["treasury_amount"] = JsonValue.Create(4)
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var shining = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json"))!)!.AsObject();
        Assert.Equal(24, shining["treasury"]!["depositedInkFeathers"]!.GetValue<int>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_SourceOfLight_WritesPendingCapstoneRequest()
    {
        await SeedShiningAbodeFilesAsync();
        _fs.DeleteFile("game_state/control/pending_shining_abode_actions.json");
        var shining = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json"))!)!.AsObject();
        shining["radiance"] = new JsonObject { ["experience"] = 580, ["tier"] = 4 };
        await _fs.WriteFileAtomicAsync("game_state/meta/shining_abode_state.json", shining.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/source_of_light",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["source_of_light_action"] = JsonValue.Create("open")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var request = JsonNode.Parse((await _fs.ReadFileAsync(SourceOfLightCapstoneState.PendingRequestPath))!)!.AsObject();
        Assert.StartsWith("source_of_light_capstone:", request["requestId"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(580, request["radianceExperienceAtRequest"]!.GetValue<int>());
        Assert.Equal(4, request["radianceTierAtRequest"]!.GetValue<int>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_AfterlifeInboxMarkAllRead_UpdatesNotifications()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/afterlife_inbox",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["notification_action"] = JsonValue.Create("mark_all_read")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var notifications = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeNotificationState.NotificationsPath))!)!.AsObject();
        Assert.Equal(AfterlifeNotificationState.StatusRead, notifications["notifications"]![0]!["status"]!.GetValue<string>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_SpiritualArtsUpgrade_UpdatesSoulProfile()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await _fs.WriteFileAtomicAsync(
            AfterlifeSpiritualConflictState.StatePath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeConflict"] = null,
                ["recentConflicts"] = new JsonArray()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        soul["inkFeathers"] = new JsonObject { ["current"] = 600, ["total"] = 600 };
        soul["afterlifeCombatProfile"]!["artTiers"]!["pressure"] = 0;
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/spiritual_arts",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["upgrade_target"] = JsonValue.Create("pressure"),
                ["upgrade_currency"] = JsonValue.Create("ink_feathers")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var updated = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        Assert.Equal(1, updated["afterlifeCombatProfile"]!["artTiers"]!["pressure"]!.GetValue<int>());
        Assert.Equal(100, updated["inkFeathers"]!["current"]!.GetValue<int>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_SpiritualArtsSpecialUpgrade_UpdatesEntityProfile()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await _fs.WriteFileAtomicAsync(
            AfterlifeSpiritualConflictState.StatePath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeConflict"] = null,
                ["recentConflicts"] = new JsonArray()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        soul["inkFeathers"] = new JsonObject { ["current"] = 500, ["total"] = 500 };
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var profiles = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var specialArt = profiles["profiles"]!.AsArray()[0]!["specialArts"]!.AsArray()[0]!.AsObject();
        specialArt["upgradeCost"] = new JsonObject { ["inkFeathers"] = 90, ["lightSparks"] = 0 };
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/spiritual_arts",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["upgrade_target"] = JsonValue.Create("rose_mirror_counter"),
                ["upgrade_currency"] = JsonValue.Create("ink_feathers")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var updatedSoul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var updatedProfiles = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var updatedSpecialArt = updatedProfiles["profiles"]!.AsArray()[0]!["specialArts"]!.AsArray()[0]!.AsObject();
        Assert.Equal(2, updatedSpecialArt["tier"]!.GetValue<int>());
        Assert.Equal(50, updatedSoul["inkFeathers"]!["current"]!.GetValue<int>());
        Assert.Contains(updatedProfiles["profiles"]!.AsArray()[0]!["ledger"]!.AsArray().OfType<JsonObject>(), entry =>
            string.Equals(entry["reason"]?.GetValue<string>(), "special_art_local_upgrade", StringComparison.Ordinal));
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_SpiritualAction_ReturnsGmActionPayload()
    {
        await SeedUniversalMetaFilesAsync();
        await SeedChaosSeaFilesAsync();
        await SeedAfterlifeCombatAndEntityFilesAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/spiritual_action",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["operation_type"] = JsonValue.Create("pressure"),
                ["spiritual_action_text"] = JsonValue.Create("Я давлю на трещину в клятве противника и заставляю его отступить.")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var payload = Assert.IsType<UiRawJsonBlock>(completed.Blocks.Last()).Json!.AsObject();
        Assert.Equal("AFTERLIFE_SPIRITUAL_ACTION", payload["playerActionTag"]!.GetValue<string>());
        Assert.Contains("afterlifeSpiritualConflictUpdate", payload["gmAction"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task ExecuteAsync_SarefFindWingsWithoutRoute_HidesSpoilers()
    {
        await SeedShiningAbodeFilesAsync();
        _fs.DeleteFile("game_state/control/pending_shining_abode_actions.json");
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, SarefMainStoryState.SerializeDefaultRoot());

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/сареф найти_крылья"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Null(result.InteractiveSession);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Ты пока не знаешь, что искать", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Сареф", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Крыл", text, StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists(SarefMainStoryState.PendingWingsInfiltrationPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_SarefFindWings_WritesPendingRequestAndGmPayload()
    {
        await SeedShiningAbodeFilesAsync();
        _fs.DeleteFile("game_state/control/pending_shining_abode_actions.json");
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefWingsRouteState());
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/сареф найти_крылья",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        Assert.NotNull(started.InteractiveSession);
        Assert.True(started.InteractiveSession.RequiresLocalUiLock);
        Assert.Contains(started.Prompts, prompt => prompt.Id == "saref_wings_action");

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["saref_wings_action"] = JsonValue.Create("start")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var request = JsonNode.Parse((await _fs.ReadFileAsync(SarefMainStoryState.PendingWingsInfiltrationPath))!)!.AsObject();
        Assert.Equal("safe", request["routeSafety"]!.GetValue<string>());
        Assert.Equal("safe_infiltration", request["entryMode"]!.GetValue<string>());
        Assert.Equal("sarefMainStoryUpdate", request["expectedResponseSurface"]!.GetValue<string>());
        var payload = Assert.IsType<UiRawJsonBlock>(completed.Blocks.Last()).Json!.AsObject();
        Assert.Equal("SAREF_WINGS_INFILTRATION", payload["playerActionTag"]!.GetValue<string>());
        Assert.Contains("sarefMainStoryUpdate", payload["gmAction"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task ExecuteAsync_SarefFindWings_RendersPlayerFacingDossierWithoutRawJson()
    {
        await SeedShiningAbodeFilesAsync();
        _fs.DeleteFile("game_state/control/pending_shining_abode_actions.json");
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefWingsRouteState());

        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/сареф найти_крылья"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        Assert.DoesNotContain(started.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(started.Blocks.SelectMany(EnumerateKeyValueGrids));
        Assert.Contains(started.Blocks.SelectMany(EnumerateEntityDossiers), static dossier =>
            dossier.EntityType == "saref-wings-infiltration" &&
            dossier.Title == "Поиск Крыльев Ангелов");
        var startedText = CollectBlockText(started.Blocks);
        Assert.Contains("безопасный маршрут", startedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JSON", startedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestId", startedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pending_", startedText, StringComparison.OrdinalIgnoreCase);

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["saref_wings_action"] = JsonValue.Create("start")
            }));
        Assert.Equal(CommandExecutionState.Completed, completed.State);

        var waiting = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/сареф найти_крылья"));
        Assert.Equal(CommandExecutionState.Completed, waiting.State);
        Assert.DoesNotContain(waiting.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(waiting.Blocks.SelectMany(EnumerateKeyValueGrids));
        Assert.Contains(waiting.Blocks.SelectMany(EnumerateEntityDossiers), static dossier =>
            dossier.EntityType == "saref-wings-infiltration" &&
            dossier.Title == "Поиск Крыльев Ангелов");
        var waitingText = CollectBlockText(waiting.Blocks);
        Assert.Contains("ожидает закрытия ГМ", waitingText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JSON", waitingText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestId", waitingText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pending_", waitingText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SarefUseAdvantage_RendersDossiersInsteadOfPanelAndTable()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefActionReadyState());

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/сареф преимущество"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateKeyValueGrids));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.Empty(result.Blocks.SelectMany(EnumeratePanels));
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static dossier =>
            dossier.EntityType == "saref-action-overview" &&
            dossier.Title == "Использовать преимущество");
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static dossier =>
            dossier.EntityType == "saref-advantages" &&
            dossier.Title == "Доступные преимущества" &&
            dossier.Sections.Any(static section => section.Cards.Count > 0));

        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Лунный Разрез Клятвы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Можно рассечь одну ложную печать", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("advantageId", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SarefAgenda_RendersAgendaDossierInsteadOfLegacyPanel()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefActionReadyState());

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/сареф поручение"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateKeyValueGrids));
        Assert.Empty(result.Blocks.SelectMany(EnumeratePanels));
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static dossier =>
            dossier.EntityType == "saref-agenda" &&
            dossier.Title == "Текущая повестка");
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Подчинить последнюю независимую фракцию", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("не завершено", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SarefAgenda_RendersAssignmentsDominationAndOathBreakArc()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "schemaVersion": 1,
          "revealStage": "confrontation_available",
          "guardianQuestlines": [],
          "latentTraces": [],
          "sarefRevelations": [
            { "revelationId": "rev_identity", "category": "identity", "revealedAtTurn": 50 },
            { "revelationId": "rev_method", "category": "method", "revealedAtTurn": 51 },
            { "revelationId": "rev_faction", "category": "faction", "revealedAtTurn": 52 },
            { "revelationId": "rev_path", "category": "path", "revealedAtTurn": 53 }
          ],
          "sarefAdvantages": [],
          "sarefAdvantageUses": [],
          "factionLinks": { "visibility": "revealed", "wingsFactionId": "wings_of_angels" },
          "finalConfrontation": { "status": "active", "sceneType": "saref_confrontation" },
          "postStoryAgenda": {
            "state": "oathbound_to_saref",
            "currentObjective": "Подчинить совет Серого Пепла.",
            "agendaSummary": "Сареф требует доказать полезность новой клятвы.",
            "assignments": [
              {
                "assignmentId": "assignment_gray_ash",
                "status": "active",
                "targetFactionId": "faction_gray_ash",
                "targetFactionName": "Совет Серого Пепла",
                "campaignId": "campaign_gray_ash",
                "campaignName": "Кампания раскола Серого Пепла",
                "summary": "Подорвать влияние серого совета через открытый раскол."
              }
            ],
            "dominationScene": {
              "status": "completed",
              "summary": "Совет Серого Пепла преклонил знамена перед Крыльями.",
              "resolvedAtTurn": 94
            },
            "oathBreakArc": {
              "arcId": "oathbreak_lucian",
              "state": "active",
              "route": "lucian",
              "summary": "Люциан ищет слабое место в чужой клятве.",
              "proofSummary": "Лунный разрез показывает подменённую печать."
            }
          },
          "playerOathState": { "state": "oathbound", "oathId": "saref_oath_001" },
          "defeatOutcomes": [],
          "endings": [],
          "sarefPersonalBond": null
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/сареф поручение"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.Empty(result.Blocks.SelectMany(EnumeratePanels));
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Подчинить совет Серого Пепла", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сареф требует доказать полезность новой клятвы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Подорвать влияние серого совета", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Целевая фракция", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Совет Серого Пепла", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Кампания", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Кампания раскола Серого Пепла", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Финал власти Сарефа", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Совет Серого Пепла преклонил знамена", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Арка разрыва клятвы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Лунный разрез показывает подменённую печать", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("assignments", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dominationScene", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("oathBreakArc", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("faction_gray_ash", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("campaign_gray_ash", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_SarefAdvantage_ReturnsGmPayload()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefActionReadyState());
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/сареф преимущество"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        Assert.Contains(started.Prompts, prompt => prompt.Id == "saref_advantage_id");

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["saref_advantage_id"] = JsonValue.Create("adv_lucian_oath_cut"),
                ["saref_scene_type"] = JsonValue.Create("oath_break"),
                ["saref_action_summary"] = JsonValue.Create("Разрезать одну ложную печать клятвы Сарефа.")
            }));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var payload = Assert.IsType<UiRawJsonBlock>(completed.Blocks.Last()).Json!.AsObject();
        Assert.Equal("SAREF_ADVANTAGE_USE", payload["playerActionTag"]!.GetValue<string>());
        Assert.Equal("adv_lucian_oath_cut", payload["advantageId"]!.GetValue<string>());
        Assert.Contains("sarefAdvantageUses", payload["gmAction"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_SarefConfrontationAndOathBreak_ReturnGmPayloads()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefActionReadyState());

        var confrontation = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/сареф конфронтация"));
        Assert.Equal(CommandExecutionState.RequiresInput, confrontation.State);
        var confrontationResult = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            confrontation.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["saref_route_type"] = JsonValue.Create("combat"),
                ["saref_resolution_intent"] = JsonValue.Create("defeat_saref"),
                ["saref_action_summary"] = JsonValue.Create("Вызвать Сарефа на прямой духовный бой.")
            }));

        Assert.Equal(CommandExecutionState.Completed, confrontationResult.State);
        var confrontationPayload = Assert.IsType<UiRawJsonBlock>(confrontationResult.Blocks.Last()).Json!.AsObject();
        Assert.Equal("SAREF_FINAL_CONFRONTATION", confrontationPayload["playerActionTag"]!.GetValue<string>());
        Assert.Contains("record_final_confrontation", confrontationPayload["gmAction"]!.GetValue<string>(), StringComparison.Ordinal);

        var oathBreak = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/сареф разорвать_клятву"));
        Assert.Equal(CommandExecutionState.RequiresInput, oathBreak.State);
        var oathBreakResult = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            oathBreak.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["saref_oath_break_route"] = JsonValue.Create("lucian"),
                ["saref_action_summary"] = JsonValue.Create("Использовать лунный разрез как путь разрыва клятвы.")
            }));

        Assert.Equal(CommandExecutionState.Completed, oathBreakResult.State);
        var oathBreakPayload = Assert.IsType<UiRawJsonBlock>(oathBreakResult.Blocks.Last()).Json!.AsObject();
        Assert.Equal("SAREF_OATH_BREAK", oathBreakPayload["playerActionTag"]!.GetValue<string>());
        Assert.Contains("record_oath_break", oathBreakPayload["gmAction"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_MissingRequiredAnswer_KeepsSessionOpenWithValidationError()
    {
        await SeedUniversalMetaFilesAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/world_setup",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var validation = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>(),
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.RequiresInput, validation.State);
        Assert.NotEmpty(validation.Prompts);
        Assert.NotNull(validation.InteractiveSession);
        Assert.Contains(validation.Notifications, static notification =>
            notification.Severity == UiNotificationSeverity.Error &&
            notification.Message.Contains("Режим подготовки мира", StringComparison.OrdinalIgnoreCase) &&
            !notification.Message.Contains("world_setup_mode", StringComparison.OrdinalIgnoreCase));
        Assert.True(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task CancelPromptSessionAsync_ReleasesLocalLock()
    {
        await SeedUniversalMetaFilesAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/world_setup",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var cancelled = await _service.CancelPromptSessionAsync(new ExplorerPromptSessionCancelRequest(
            started.InteractiveSession!.SessionId,
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, cancelled.State);
        Assert.Contains("отменена", CollectBlockText(cancelled.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Theory]
    [InlineData("/status")]
    [InlineData("/soul")]
    [InlineData("/codex")]
    [InlineData("/story")]
    [InlineData("/debug")]
    [InlineData("/галерея")]
    [InlineData("/saref")]
    public async Task ExecuteAsync_MigratedUniversalMetaCommands_ReturnCompletedDtos(string command)
    {
        await PrepareMigratedUniversalFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.NotEmpty(result.Blocks);
        Assert.DoesNotContain(
            result.Blocks,
            static block => block is UiMessageBlock message &&
                            message.Title.Contains("пока недоступна", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_SarefStory_RendersPlayerFacingDossierWithoutTables()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefActionReadyState());

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/saref"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Крылья над Бездной", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Лунный Разрез Клятвы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("связана клятвой", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("oathbound", text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static dossier =>
            dossier.EntityType == "saref-story" &&
            dossier.Sections.Any(static section => section.Cards.Count > 0));
    }

    [Fact]
    public async Task ExecuteAsync_Soul_RendersPlayerFacingSummaryWithoutRawJson()
    {
        await SeedUniversalMetaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/soul"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain(result.Blocks.SelectMany(EnumerateEntityDossiers), static dossier =>
            dossier.EntityType == "panel" &&
            dossier.Title == "Душа");
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static dossier =>
            dossier.EntityType == "soul-profile" &&
            dossier.Title == "Душа");
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains("Душа", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Test Soul", text, StringComparison.Ordinal);
        Assert.Contains("Море Хаоса", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Chaos Sea", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("soul_state", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Полный JSON", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_MemoryScene_ReturnsPlayerReadableDossierWithoutTables()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "schemaVersion": 1,
          "revealStage": "name_revealed",
          "guardianQuestlines": [],
          "latentTraces": [],
          "sarefRevelations": [],
          "sarefAdvantages": [],
          "sarefAdvantageUses": [],
          "memoryScene": {
            "sceneId": "memory_scene_azalia_q4",
            "title": "Ложа белых перьев",
            "status": "active",
            "layer": "Воспоминание",
            "guardianId": "azalia",
            "questId": "azalia_saref_q4",
            "questOrdinal": 4,
            "role": { "roleId": "azalia_white_lodge_witness", "displayName": "Свидетель ложи", "summary": "Роль внутри старого предательства." },
            "boundaries": [ { "summary": "Сареф уже вошёл в ложу; это нельзя отменить." } ],
            "abilities": [
              { "abilityId": "read_oath", "name": "Прочитать клятву", "summary": "Увидеть скрытую цену белых перьев." },
              { "abilityId": "hold_memory", "name": "Удержать память", "summary": "Не дать сцене рассыпаться." },
              { "abilityId": "name_traitor", "name": "Назвать предателя", "summary": "Связать образ с будущей правдой." }
            ],
            "requiredStoryNodes": [ { "status": "pending", "summary": "Увидеть предательство." } ],
            "successCondition": { "summary": "Распознать связь ложи с Крыльями Ангелов.", "satisfied": false },
            "closureTarget": { "guardianId": "azalia", "questId": "azalia_saref_q4", "questOrdinal": 4 }
          },
          "factionLinks": { "visibility": "hidden" },
          "defeatOutcomes": [],
          "endings": [],
          "playerOathState": null,
          "sarefPersonalBond": null
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/воспоминание_начать"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Воспоминание", text, StringComparison.Ordinal);
        Assert.Contains("Ложа белых перьев", text, StringComparison.Ordinal);
        Assert.Contains("Свидетель ложи", text, StringComparison.Ordinal);
        Assert.Contains("Прочитать клятву", text, StringComparison.Ordinal);
        Assert.Contains("Это не Врата Памяти", text, StringComparison.Ordinal);
        Assert.Contains("не Наследие Памяти", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Memory Gates", text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static dossier =>
            dossier.EntityType == "saref-memory-scene" &&
            dossier.Sections.Any(static section => section.Cards.Count > 0));
    }

    [Fact]
    public async Task ExecuteAsync_MemoryScene_RendersNamedClosureTargetsWithoutRawIds()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "schemaVersion": 1,
          "revealStage": "name_revealed",
          "guardianQuestlines": [],
          "latentTraces": [],
          "sarefRevelations": [],
          "sarefAdvantages": [],
          "sarefAdvantageUses": [],
          "memoryScene": {
            "sceneId": "memory_scene_azalia_q4",
            "title": "Ложа белых перьев",
            "status": "active",
            "layer": "Воспоминание",
            "guardianId": "guardian_azalia",
            "guardianName": "Азалия",
            "questId": "azalia_saref_q4",
            "questName": "Четвёртая правда Азалии",
            "questOrdinal": 4,
            "role": { "roleId": "azalia_white_lodge_witness", "displayName": "Свидетель ложи", "summary": "Роль внутри старого предательства." },
            "boundaries": [ { "summary": "Сареф уже вошёл в ложу; это нельзя отменить." } ],
            "abilities": [ { "abilityId": "read_oath", "name": "Прочитать клятву", "summary": "Увидеть скрытую цену белых перьев." } ],
            "requiredStoryNodes": [ { "status": "pending", "summary": "Увидеть предательство." } ],
            "successCondition": { "conditionId": "condition_wings_truth", "displayName": "Понять исток Крыльев", "satisfied": false },
            "closureTarget": {
              "guardianId": "guardian_azalia",
              "guardianName": "Азалия",
              "questId": "azalia_saref_q4",
              "questName": "Четвёртая правда Азалии",
              "questOrdinal": 4,
              "revelationId": "revelation_identity",
              "revelationName": "Имя Сарефа",
              "advantageId": "adv_lucian_oath_cut",
              "advantageName": "Лунный Разрез Клятвы"
            }
          },
          "factionLinks": { "visibility": "hidden" },
          "defeatOutcomes": [],
          "endings": [],
          "playerOathState": null,
          "sarefPersonalBond": null
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/воспоминание_статус"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Азалия", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Четвёртая правда Азалии", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Понять исток Крыльев", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Фрагмент истины", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Имя Сарефа", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Преимущество", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Лунный Разрез Клятвы", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("guardian_azalia", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("azalia_saref_q4", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("condition_wings_truth", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("revelation_identity", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("adv_lucian_oath_cut", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_MemorySceneSubcommand_RoutesThroughSharedParser()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "schemaVersion": 1,
          "revealStage": "name_revealed",
          "guardianQuestlines": [],
          "latentTraces": [],
          "sarefRevelations": [],
          "sarefAdvantages": [],
          "sarefAdvantageUses": [],
          "memoryScene": {
            "sceneId": "memory_scene_azalia_q4",
            "title": "Ложа белых перьев",
            "status": "active",
            "layer": "Воспоминание",
            "guardianId": "azalia",
            "questId": "azalia_saref_q4",
            "questOrdinal": 4,
            "role": { "roleId": "azalia_white_lodge_witness", "displayName": "Свидетель ложи", "summary": "Роль внутри старого предательства." },
            "boundaries": [ { "summary": "Сареф уже вошёл в ложу; это нельзя отменить." } ],
            "abilities": [
              { "abilityId": "read_oath", "name": "Прочитать клятву", "summary": "Увидеть скрытую цену белых перьев." }
            ],
            "requiredStoryNodes": [ { "status": "pending", "summary": "Увидеть предательство." } ],
            "successCondition": { "summary": "Распознать связь ложи с Крыльями Ангелов.", "satisfied": false },
            "closureTarget": { "guardianId": "azalia", "questId": "azalia_saref_q4", "questOrdinal": 4 }
          },
          "factionLinks": { "visibility": "hidden" },
          "defeatOutcomes": [],
          "endings": [],
          "playerOathState": null,
          "sarefPersonalBond": null
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/воспоминание начать"));

        Assert.Equal("/воспоминание_начать", result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Воспоминание", text, StringComparison.Ordinal);
        Assert.Contains("Ложа белых перьев", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownSubcommand_ReturnsRussianParserError()
    {
        await SeedUniversalMetaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/сареф неизвестная_ветка"));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Неизвестная подкоманда", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("неизвестная_ветка", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_RecognizedSarefWriteSubcommand_HidesSpoilersBeforeDiscovery()
    {
        await SeedUniversalMetaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/сареф найти_крылья"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Ты пока не знаешь, что искать", text, StringComparison.Ordinal);
        Assert.DoesNotContain("#592", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_MalformedArguments_ReturnsRussianParserError()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/math \"2 + 3"));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Некорректные аргументы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("кавыч", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/inv")]
    [InlineData("/npc")]
    [InlineData("/quests")]
    [InlineData("/map")]
    [InlineData("/stats")]
    [InlineData("/combat")]
    [InlineData("/weather")]
    [InlineData("/books")]
    [InlineData("/interactions")]
    public async Task ExecuteAsync_MigratedMortalReadOnlyCommands_ReturnCompletedDtos(string command)
    {
        await PrepareMigratedMortalFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.NotEmpty(result.Blocks);
        Assert.DoesNotContain(
            result.Blocks,
            static block => block is UiMessageBlock message &&
                            message.Title.Contains("пока недоступна", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_Inventory_ReturnsRichInventoryBlocksAndFriendlyTitles()
    {
        var helmet = CreateAcceptedUiItem(
            "helmet_1",
            "Железный шлем",
            item =>
            {
                item["type"] = "helmet";
                item["equipmentSlot"] = "Head";
            },
            "equipment");
        var sword = CreateAcceptedUiItem(
            "sword_1",
            "Кривой меч",
            item =>
            {
                item["type"] = "weapon";
                item["equipmentSlot"] = "MainHand";
            },
            "equipment");
        var torch = CreateAcceptedUiItem(
            "torch_1",
            "Факел",
            item =>
            {
                item["type"] = "utility";
                item["count"] = 2;
            });
        var brokenBow = CreateAcceptedUiItem(
            "bow_1",
            "Сломанный лук",
            item =>
            {
                item["type"] = "weapon";
                item["durability"] = "0%";
            });
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/items.json",
            new JsonObject
            {
                ["totalWeight"] = 17,
                ["maxWeight"] = 30,
                ["money"] = 125,
                ["resources"] = new JsonObject
                {
                    ["wood"] = 4,
                    ["gold"] = 0,
                    ["cloth"] = "2"
                },
                ["equippedItems"] = new JsonObject
                {
                    ["Head"] = "helmet_1",
                    ["MainHand"] = "sword_1",
                    ["OffHand"] = null
                },
                ["items"] = new JsonArray(helmet, sword, torch, brokenBow)
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync("game_state/inventory/item_resources.json", """
        {
          "entries": [
            { "itemId": "torch_1", "resource": "oil" }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/inventory/item_bonds.json", """
        {
          "entries": [
            { "itemId": "bow_1", "bond": "quest" }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/inventory/item_text_updates.json", """
        {
          "entries": [
            { "itemId": "note_1", "title": "Записка" }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/inv"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var inventoryDossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "inventory" &&
            block.Title.Equals("Инвентарь", StringComparison.OrdinalIgnoreCase));
        var resourceSection = Assert.Single(inventoryDossier.Sections, static section =>
            section.Title.Equals("Ресурсы", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(resourceSection.Facts, static item => item.Label == "💎 wood" && item.Value == "4");
        Assert.Contains(resourceSection.Facts, static item => item.Label == "💎 cloth" && item.Value == "2");

        var equipmentSection = Assert.Single(inventoryDossier.Sections, static section =>
            section.Title.Equals("Экипировка", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(equipmentSection.Facts, static item => item.Label == "🪖 Голова" && item.Value == "Железный шлем");
        Assert.Contains(equipmentSection.Facts, static item => item.Label == "⚔️ Основная рука" && item.Value == "Кривой меч");
        Assert.Contains(equipmentSection.Facts, static item => item.Label == "🛡️ Вторая рука" && item.Value == "— пусто");

        var itemSection = Assert.Single(inventoryDossier.Sections, static section =>
            section.Title.Equals("Предметы", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(itemSection.Cards, static card =>
            card.Title.Equals("Факел", StringComparison.OrdinalIgnoreCase) &&
            card.PrimaryAction != null &&
            !card.Summary.Contains("Прочность", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(itemSection.Cards, static card =>
            card.Title.Equals("Сломанный лук", StringComparison.OrdinalIgnoreCase) &&
            card.Badges.Any(badge => badge.Label.Contains("сломано", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(result.Blocks.SelectMany(EnumerateTables), static table =>
            table.Title.Contains("Предметы", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);

        Assert.DoesNotContain("game_state/inventory/", CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Inventory_WithoutItemsFile_ShowsEmptyInventoryMessage()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/inv"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var message = Assert.IsType<UiMessageBlock>(Assert.Single(result.Blocks));
        Assert.Equal(UiNotificationSeverity.Info, message.Severity);
        Assert.Equal("Инвентарь", message.Title);
        Assert.Equal("Инвентарь пуст или данные ещё не созданы.", message.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Inventory_WithItemsButWithoutAuxiliaryFiles_HidesMissingAuxiliaryState()
    {
        var torch = CreateAcceptedUiItem(
            "torch_without_sidecars",
            "Факел",
            item =>
            {
                item["type"] = "utility";
                item["count"] = 2;
            });
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/items.json",
            new JsonObject
            {
                ["items"] = new JsonArray(torch),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/inventory"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var inventoryDossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "inventory" &&
            block.Title.Equals("Инвентарь", StringComparison.OrdinalIgnoreCase));
        var itemSection = Assert.Single(inventoryDossier.Sections, static section =>
            section.Title.Equals("Предметы", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(itemSection.Cards, static card =>
            card.Title.Equals("Факел", StringComparison.OrdinalIgnoreCase) &&
            card.Subtitle.Contains("Полезный предмет", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Blocks.SelectMany(EnumerateTables), static table =>
            table.Title.Contains("Предметы", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("отсутствует", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/inventory/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("item_resources.json", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("item_bonds.json", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("item_text_updates.json", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Inventory_AddsItemDetailActionsForVisibleItems()
    {
        await SeedInventoryItemDetailStateAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/инв"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(result.Actions, action =>
            action.Label.Contains("Руническая перчатка", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/инв предмет runic_glove_1", StringComparison.OrdinalIgnoreCase) &&
            action.Style == UiActionStyle.Secondary &&
            action.RequiresConfirmation == false);
    }

    [Fact]
    public async Task ExecuteAsync_Inventory_ProjectsBrowserDossierIntoPrototypeSections()
    {
        await SeedInventoryItemDetailStateAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/инв"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var inventoryDossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "inventory" &&
            block.Title.Equals("Инвентарь", StringComparison.OrdinalIgnoreCase));
        var itemSection = Assert.Single(inventoryDossier.Sections, static section =>
            section.Title.Equals("Предметы", StringComparison.OrdinalIgnoreCase));
        var overviewSection = Assert.Single(inventoryDossier.Sections, static section =>
            section.Title.Equals("Сводка", StringComparison.OrdinalIgnoreCase) ||
            section.Title.Equals("Экипировка", StringComparison.OrdinalIgnoreCase));

        Assert.NotEmpty(itemSection.Cards);
        Assert.Equal("collection", itemSection.Presentation);
        Assert.All(itemSection.Cards, static card => Assert.False(string.IsNullOrWhiteSpace(card.Title)));
        Assert.Contains(itemSection.Cards, static card =>
            card.Badges.Count > 0 ||
            !string.IsNullOrWhiteSpace(card.Subtitle) ||
            !string.IsNullOrWhiteSpace(card.Summary));
        Assert.NotEmpty(overviewSection.Facts);
        Assert.Empty(itemSection.Blocks.OfType<UiEntityDossierBlock>());
    }

    [Fact]
    public async Task ExecuteAsync_InventoryItemDetail_RendersReadableItemAndSidecarDetails()
    {
        await SeedInventoryItemDetailStateAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/инв предмет runic_glove_1"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal("/инв предмет runic_glove_1", result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Руническая перчатка", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("На тыльной стороне перчатки мерцает рунический контур.", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тип", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Артефакт", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Качество", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("редк", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Бонусы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Чувство магических потоков +2", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Структурные бонусы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тип бонуса", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тип значения", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тип модификатора", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Боевые эффекты", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Рунный отклик", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сбивает концентрацию цели.", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Особые свойства", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Подсвечивает свежие следы.", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Содержимое", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SIDE_TEXT_MARKER", text, StringComparison.Ordinal);
        Assert.Contains("Связь с владельцем", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("12/100", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Записи", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JOURNAL_MARKER", text, StringComparison.Ordinal);
        Assert.Contains("Резонанс северной нити", text, StringComparison.Ordinal);
        Assert.Contains(result.Actions, action =>
            action.Label.Contains("Назад", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/инв", StringComparison.OrdinalIgnoreCase));
        var itemDossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "inventory-item" &&
            block.Title.Contains("Руническая перчатка", StringComparison.OrdinalIgnoreCase));
        var structuredBonusSection = Assert.Single(itemDossier.Sections, static section =>
            section.Title.Equals("Структурные бонусы", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(structuredBonusSection.Cards, static block =>
            block.Title.Contains("Чувство магических потоков", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"structuredBonuses\"", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"combatEffect\"", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_InventoryItemDetail_WithNumericPermanentId_PrefersExactIdentityOverOrdinal()
    {
        var first = CreateAcceptedUiItemFromJson(
            "itm_numeric_first",
            """{"name":"Первый предмет","description":"FIRST_NUMERIC_ITEM_MARKER","type":"tool"}""");
        var second = CreateAcceptedUiItemFromJson(
            "itm_numeric_second",
            """{"name":"Второй предмет","description":"SECOND_NUMERIC_ITEM_MARKER","type":"tool"}""");
        var numeric = CreateAcceptedUiItemFromJson(
            "2",
            """{"name":"Предмет с числовым ключом","description":"EXACT_NUMERIC_ITEM_MARKER","type":"tool"}""");
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(first, second, numeric),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/инв предмет 2"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Предмет с числовым ключом", text, StringComparison.Ordinal);
        Assert.Contains("EXACT_NUMERIC_ITEM_MARKER", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SECOND_NUMERIC_ITEM_MARKER", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InventoryItemDetail_UsesSemanticAllowlistAndOmitsMortalItemAuthority()
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot("itm_projection_browser");
        item["name"] = "Зубило мастера рун";
        item["description"] = "На стали виден узор, помогающий удерживать точный угол удара.";
        item["type"] = "tool";
        item["requestId"] = "PRIVATE_BROWSER_ROOT_REQUEST_ID";
        item["slotId"] = "PRIVATE_BROWSER_ROOT_SLOT_ID";
        item["tradeCycleId"] = "PRIVATE_BROWSER_ROOT_TRADE_CYCLE_ID";
        item["rewardId"] = "PRIVATE_BROWSER_ROOT_REWARD_ID";
        item["UpdateInventory"] = new JsonArray("PRIVATE_BROWSER_ROOT_UPDATE_INVENTORY");
        item["structuredBonuses"] = new JsonArray(
            new JsonObject
            {
                ["bonusType"] = "Skill",
                ["target"] = "Рунное дело",
                ["value"] = 2,
                ["valueType"] = "Flat",
                ["summary"] = "Рунное дело +2",
                ["experimentalKey"] = "BROWSER_EXPERIMENTAL_SEMANTIC",
                ["condition"] = new JsonObject
                {
                    ["trigger"] = "Когда руны совпадают",
                    ["creationRef"] = "PRIVATE_BROWSER_NESTED_CREATION_REF",
                    ["receiptSeal"] = "PRIVATE_BROWSER_NESTED_SEAL",
                    ["image_prompt"] = "PRIVATE_BROWSER_NESTED_IMAGE_PROMPT",
                    ["itemCreationRef"] = "PRIVATE_BROWSER_ITEM_CREATION_REF",
                    ["itemRef"] = "PRIVATE_BROWSER_ITEM_REF",
                    ["sourceItemId"] = "PRIVATE_BROWSER_SOURCE_ITEM_ID",
                    ["targetItemId"] = "PRIVATE_BROWSER_TARGET_ITEM_ID",
                    ["parentItemId"] = "PRIVATE_BROWSER_PARENT_ITEM_ID",
                    ["containerItemId"] = "PRIVATE_BROWSER_CONTAINER_ITEM_ID",
                    ["rewardItemId"] = "PRIVATE_BROWSER_REWARD_ITEM_ID",
                    ["destinationItemId"] = "PRIVATE_BROWSER_DESTINATION_ITEM_ID",
                    ["resultItemId"] = "PRIVATE_BROWSER_RESULT_ITEM_ID",
                    ["removedItemId"] = "PRIVATE_BROWSER_REMOVED_ITEM_ID",
                    ["destinationContainerId"] = "PRIVATE_BROWSER_DESTINATION_CONTAINER_ID",
                    ["currentContentsPath"] = "PRIVATE_BROWSER_CURRENT_CONTENTS_PATH",
                    ["itemIds"] = new JsonArray("PRIVATE_BROWSER_ITEM_IDS"),
                    ["targetItemIds"] = new JsonArray("PRIVATE_BROWSER_TARGET_ITEM_IDS"),
                    ["UpdateInventory"] = new JsonArray("PRIVATE_BROWSER_NESTED_UPDATE_INVENTORY"),
                    ["NPCInventoryAdds"] = new JsonArray("PRIVATE_BROWSER_NESTED_NPC_INVENTORY_ADDS"),
                    ["UpdateNpcTradeInventoryReceipts"] = new JsonArray("PRIVATE_BROWSER_NESTED_TRADE_RECEIPTS"),
                    ["lootForCurrentTurn"] = new JsonArray("PRIVATE_BROWSER_NESTED_LOOT"),
                    ["removeInventoryItems"] = new JsonArray("PRIVATE_BROWSER_NESTED_REMOVE_INVENTORY"),
                    ["NPCInventoryRemovals"] = new JsonArray("PRIVATE_BROWSER_NESTED_NPC_REMOVALS")
                },
                ["creationRef"] = "PRIVATE_BROWSER_CREATION_REF",
                ["receiptId"] = "PRIVATE_BROWSER_RECEIPT",
                ["seal"] = "PRIVATE_BROWSER_SEAL",
                ["lineage"] = "PRIVATE_BROWSER_LINEAGE",
                ["currentCarrier"] = "PRIVATE_BROWSER_CARRIER",
                ["carrierPath"] = "PRIVATE_BROWSER_PATH",
                ["sourceAuthority"] = "PRIVATE_BROWSER_SOURCE_AUTHORITY",
                ["sourceTurn"] = "PRIVATE_BROWSER_SOURCE_TURN",
                ["repairPacket"] = "PRIVATE_BROWSER_REPAIR",
                ["requestId"] = "PRIVATE_BROWSER_NESTED_REQUEST_ID",
                ["slotId"] = "PRIVATE_BROWSER_NESTED_SLOT_ID",
                ["tradeCycleId"] = "PRIVATE_BROWSER_NESTED_TRADE_CYCLE_ID",
                ["rewardId"] = "PRIVATE_BROWSER_NESTED_REWARD_ID"
            });
        item["ownerBondLevelCurrent"] = 12;
        item["ownerBondLevelMax"] = 80;
        item["quality"] = "Rare";
        item["rarity"] = "Rare";
        var lockedFateCard = MortalItemTestFixture.CreateItemFateCard(
            "card_runic_memory",
            "Рунная память",
            isUnlocked: false,
            unlockConditions: new JsonObject
            {
                ["ownerBondLevel"] = 35,
                ["requiredMaterials"] = new JsonArray(
                    new JsonObject
                    {
                        ["materialName"] = "Серебряная пыль",
                        ["quantity"] = 3,
                        ["receiptId"] = "PRIVATE_BROWSER_FATE_MATERIAL_RECEIPT"
                    }),
                ["plotConditionDescription"] = "Завершить гравировку в северной кузнице",
                ["conjunction"] = "OR",
                ["receiptId"] = "PRIVATE_BROWSER_FATE_CONDITION_RECEIPT"
            },
            rewards: new JsonObject
            {
                ["description"] = "Рунная память откроется после выполнения условий."
            },
            description: "Зубило помнит первый завершённый знак.",
            imagePrompt: "runic memory sigil engraved on a steel chisel");
        var unlockedFateCard = MortalItemTestFixture.CreateItemFateCard(
            "card_completed_seal_memory",
            "Память завершённой печати",
            isUnlocked: true,
            rewards: new JsonObject
            {
                ["description"] = "Открывает тайную технику рунного удара.",
                ["improvedBonuses"] = new JsonArray("Рунное дело усиливается до +3"),
                ["newCombatEffects"] = new JsonArray(
                    new JsonObject
                    {
                        ["isActivatedEffect"] = true,
                        ["actionName"] = "Удар завершённой печати",
                        ["actionCost"] = "Fast",
                        ["targetPriority"] = "enemy",
                        ["scalingCharacteristic"] = "dexterity",
                        ["effects"] = new JsonArray(
                            new JsonObject
                            {
                                ["effectType"] = "Damage",
                                ["value"] = "12%",
                                ["targetType"] = "enemy",
                                ["targetTypeDisplayName"] = "цель с печатью",
                                ["targetsCount"] = 2,
                                ["duration"] = 2,
                                ["poiseDamage"] = "6%",
                                ["effectDescription"] = "Печать выпускает рунный импульс",
                                ["currentCarrier"] = "PRIVATE_BROWSER_FATE_COMBAT_CARRIER"
                            },
                            new JsonObject
                            {
                                ["effectType"] = "DamageReduction",
                                ["value"] = "8%",
                                ["targetType"] = "self",
                                ["targetsCount"] = 1,
                                ["duration"] = 2,
                                ["damageThreshold"] = 9,
                                ["effectDescription"] = "Печатный заслон смягчает удар"
                            }),
                    },
                    new JsonObject
                    {
                        ["isActivatedEffect"] = false,
                        ["actionName"] = "Память точного знака",
                        ["targetPriority"] = "self",
                        ["scalingCharacteristic"] = "wisdom",
                        ["effects"] = new JsonArray(
                            new JsonObject
                            {
                                ["effectType"] = "Buff",
                                ["value"] = "7%",
                                ["targetType"] = "self",
                                ["targetsCount"] = 1,
                                ["duration"] = 3,
                                ["effectDescription"] = "Память печати направляет руку"
                            })
                    }),
                ["statBoostsToItemItself"] = new JsonArray("+15% к максимальной прочности"),
                ["changesDescriptionTo"] = "На зубиле проступила завершённая печать.",
                ["changesImagePromptTo"] = "steel chisel with a completed glowing rune seal",
                ["otherNarrativeChanges"] = "Архивисты узнают почерк мастера на зубиле.",
                ["repairPacket"] = "PRIVATE_BROWSER_FATE_REWARD_REPAIR"
            },
            description: "Карта откликнулась на завершённый знак.",
            imagePrompt: "completed rune seal shining on a steel chisel");
        item["fateCards"] = new JsonArray(lockedFateCard);
        item["questLinks"] = new JsonArray(
            new JsonObject
            {
                ["questName"] = "Последний знак мастера",
                ["role"] = "инструмент ритуала"
            });
        item["materialization"]!["sections"]!["mechanics"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        item["materialization"]!["sections"]!["bondsAndFateCards"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        item["materialization"]!["sections"]!["questRole"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        MortalItemTestFixture.ResealCanonical(item);
        using (var fixtureDocument = JsonDocument.Parse(item.ToJsonString()))
        {
            Assert.Empty(MortalItemMaterializationContract.Validate(
                fixtureDocument.RootElement,
                "browser projection fixture",
                MortalItemMaterializationPhase.CanonicalPostSeal));
        }
        var unaccepted = MortalItemTestFixture.CreateRawRoot(
            creationRef: "new_item_unaccepted_browser_projection",
            materializationId: "mat_item_unaccepted_browser_projection");
        unaccepted["name"] = "НЕПРИНЯТЫЙ БРАУЗЕРНЫЙ ПРЕДМЕТ";

        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(item),
                ["UpdateInventory"] = new JsonArray(unaccepted),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndex(item)
                .ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/item_resources.json",
            new JsonObject
            {
                ["entries"] = new JsonArray(
                    new JsonObject
                    {
                        ["itemId"] = "itm_projection_browser",
                        ["resource"] = "DUPLICATE_BROWSER_RESOURCE_FIRST",
                        ["maximumResource"] = 5,
                        ["resourceType"] = "заряды"
                    },
                    new JsonObject
                    {
                        ["itemId"] = "itm_projection_browser",
                        ["resource"] = "DUPLICATE_BROWSER_RESOURCE_SECOND",
                        ["maximumResource"] = 7,
                        ["resourceType"] = "заряды"
                    })
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/item_bonds.json",
            new JsonObject
            {
                ["entries"] = new JsonArray(
                    new JsonObject
                    {
                        ["itemId"] = "itm_projection_browser",
                        ["existedId"] = "itm_projection_browser_other",
                        ["ownerBondLevelCurrent"] = 99,
                        ["ownerBondLevelMax"] = 99,
                        ["lastBondChangeReason"] = "CONFLICTING_BROWSER_BOND_REASON"
                    },
                    new JsonObject
                    {
                        ["itemId"] = "ITM_PROJECTION_BROWSER",
                        ["itemName"] = "Зубило мастера рун",
                        ["ownerBondLevelCurrent"] = 12,
                        ["ownerBondLevelMax"] = 80,
                        ["fateCards"] = new JsonArray(
                            MortalItemTestFixture.CreateItemFateCard(
                                "card_wrong_case_browser",
                                "WRONG_CASE_BROWSER_FATE_CARD",
                                isUnlocked: true,
                                rewards: new JsonObject
                                {
                                    ["description"] = "WRONG_CASE_BROWSER_FATE_REWARD"
                                }))
                    },
                    new JsonObject
                    {
                        ["itemId"] = "itm_projection_browser",
                        ["itemName"] = "Зубило мастера рун",
                        ["ownerBondLevelCurrent"] = 12,
                        ["ownerBondLevelMax"] = 80,
                        ["fateCards"] = new JsonArray(
                            lockedFateCard.DeepClone(),
                            unlockedFateCard)
                    })
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/item_text_updates.json",
            new JsonObject
            {
                ["updateItemTextContents"] = new JsonArray(
                    new JsonObject
                    {
                        ["itemName"] = "Зубило мастера рун",
                        ["textContent"] = new JsonArray("RAW_NAME_BROWSER_TEXT_MARKER")
                    })
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var overview = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/инв"));
        var overviewPayload = SerializeResult(overview);
        var result = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest(
                "/инв предмет itm_projection_browser",
                AdvancedEnabled: true));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Equal(CommandExecutionState.Completed, overview.State);
        Assert.Contains("Зубило мастера рун", text, StringComparison.Ordinal);
        Assert.Contains("Рунное дело +2", text, StringComparison.Ordinal);
        Assert.Contains("BROWSER_EXPERIMENTAL_SEMANTIC", text, StringComparison.Ordinal);
        Assert.Contains("Когда руны совпадают", text, StringComparison.Ordinal);
        Assert.Contains("12/80", text, StringComparison.Ordinal);
        Assert.Contains("Рунная память", text, StringComparison.Ordinal);
        Assert.Contains("связь ≥ 35", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("3× Серебряная пыль", text, StringComparison.Ordinal);
        Assert.Contains("Завершить гравировку в северной кузнице", text, StringComparison.Ordinal);
        Assert.Contains("Память завершённой печати", text, StringComparison.Ordinal);
        Assert.Contains("Открывает тайную технику рунного удара", text, StringComparison.Ordinal);
        Assert.Contains("Рунное дело усиливается до +3", text, StringComparison.Ordinal);
        Assert.Contains("Удар завершённой печати", text, StringComparison.Ordinal);
        Assert.Contains("активируемый", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("стоимость: быстрое действие", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("приоритет цели: противник", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("масштабирование: Ловкость", text, StringComparison.Ordinal);
        Assert.Contains("Печать выпускает рунный импульс (12%)", text, StringComparison.Ordinal);
        Assert.Contains("цель: цель с печатью", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("целей: 2", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("длительность: 2", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("урон равновесию: 6%", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Печатный заслон смягчает удар (8%)", text, StringComparison.Ordinal);
        Assert.Contains("порог урона: 9", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Память точного знака", text, StringComparison.Ordinal);
        Assert.Contains("пассивный", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Память печати направляет руку (7%)", text, StringComparison.Ordinal);
        Assert.Contains("+15% к максимальной прочности", text, StringComparison.Ordinal);
        Assert.Contains("На зубиле проступила завершённая печать.", text, StringComparison.Ordinal);
        Assert.Contains("Облик предмета изменится", text, StringComparison.Ordinal);
        Assert.DoesNotContain("steel chisel with a completed glowing rune seal", text, StringComparison.Ordinal);
        Assert.Contains("Архивисты узнают почерк мастера на зубиле.", text, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(text, "Рунная память"));
        Assert.Contains("Последний знак мастера", text, StringComparison.Ordinal);
        Assert.DoesNotContain("WRONG_CASE_BROWSER_", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("CONFLICTING_BROWSER_BOND_REASON", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("DUPLICATE_BROWSER_RESOURCE_FIRST", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("DUPLICATE_BROWSER_RESOURCE_SECOND", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("RAW_NAME_BROWSER_TEXT_MARKER", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("НЕПРИНЯТЫЙ БРАУЗЕРНЫЙ ПРЕДМЕТ", overviewPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("НЕПРИНЯТЫЙ БРАУЗЕРНЫЙ ПРЕДМЕТ", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_BROWSER_", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("\"materialization\"", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"materializationReceipt\"", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"originMaterializationIds\"", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"parentItemIds\"", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/inventory/item_identity_index.json", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mortal_item_materialization_repair", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Unresolved", "Нераскрытые свойства", "Механика не раскрыта", "Руны запечатаны до ритуала распознавания.")]
    [InlineData("NarrativeOnly", "Описательные свойства", "Описание без применяемой механики", null)]
    public async Task ExecuteAsync_InventoryItemDetail_RespectsMechanicalSummaryAuthority(
        string authority,
        string expectedTitle,
        string expectedExplanation,
        string? unresolvedReason)
    {
        var suffix = authority.ToLowerInvariant();
        var itemId = $"itm_summary_{suffix}";
        var item = MortalItemTestFixture.CreateCanonicalRoot(itemId);
        item["name"] = "Перчатка со скрытыми рунами";
        item["bonuses"] = new JsonArray("Шёпот рун вокруг ладони");
        item["mechanicalSummaryAuthority"] = authority;
        item["mechanicalSummaryUnresolvedReason"] = unresolvedReason;
        item["materialization"]!["sections"]!["mechanics"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        MortalItemTestFixture.ResealCanonical(item);
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(item),
                ["equipment"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndex(item)
                .ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var result = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest($"/инв предмет {itemId}", AdvancedEnabled: true));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(expectedTitle, text, StringComparison.Ordinal);
        Assert.Contains(expectedExplanation, text, StringComparison.Ordinal);
        Assert.Contains("Шёпот рун вокруг ладони", text, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(unresolvedReason))
            Assert.Contains(unresolvedReason, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Краткое игровое описание эффектов предмета", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InventoryItemDetail_DoesNotProjectUnacceptedSameFileCandidate()
    {
        var acceptedItem = MortalItemTestFixture.CreateCanonicalRoot("itm_accepted_projection_browser");
        acceptedItem["name"] = "Принятый клинок браузерного дозорного";
        MortalItemTestFixture.ResealCanonical(acceptedItem);
        var rawItem = MortalItemTestFixture.CreateRawRoot(
            creationRef: "new_item_unaccepted_browser_detail",
            materializationId: "mat_item_unaccepted_browser_detail");
        rawItem["name"] = "UNACCEPTED_BROWSER_DETAIL_MARKER";

        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(acceptedItem, rawItem.DeepClone()),
                ["UpdateInventory"] = new JsonArray(rawItem),
                ["equipment"] = new JsonObject
                {
                    ["mainHand"] = new JsonObject
                    {
                        ["creationRef"] = "new_item_unaccepted_browser_detail",
                        ["name"] = "Принятый клинок браузерного дозорного"
                    },
                    ["offHand"] = "ITM_ACCEPTED_PROJECTION_BROWSER"
                }
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var result = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest(
                "/инв предмет 2",
                AdvancedEnabled: true));
        var overview = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/инв"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        var overviewText = CollectBlockText(overview.Blocks);
        var overviewPayload = SerializeResult(overview);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Такой предмет не найден", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Принятый клинок браузерного дозорного", overviewText, StringComparison.Ordinal);
        Assert.DoesNotContain(
            overview.Blocks.SelectMany(EnumerateEntityDossiers).SelectMany(static block => block.Sections),
            static section => section.Title.Equals("Экипировка", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            overview.Actions,
            static action => action.Label.Contains("Снять", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("UNACCEPTED_BROWSER_DETAIL_MARKER", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("UNACCEPTED_BROWSER_DETAIL_MARKER", overviewPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("new_item_unaccepted_browser_detail", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("new_item_unaccepted_browser_detail", overviewPayload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NpcDetail_ProjectsAcceptedInventoryAndIgnoresRejectedCandidates()
    {
        var accepted = MortalItemTestFixture.CreateCanonicalRoot("itm_npc_projection_browser");
        accepted["name"] = "Принятый ключ хранителя архива";
        accepted["quality"] = "Rare";
        accepted["rarity"] = "Rare";
        accepted["structuredBonuses"] = new JsonArray(
            new JsonObject
            {
                ["bonusType"] = "Skill",
                ["target"] = "perception",
                ["value"] = 2,
                ["valueType"] = "Flat",
                ["summary"] = "Архивное зрение +2"
            });
        accepted["combatEffect"] = new JsonArray(
            new JsonObject
            {
                ["isActivatedEffect"] = true,
                ["actionName"] = "Вспышка ключа",
                ["actionCost"] = "Fast",
                ["effects"] = new JsonArray(
                    new JsonObject
                    {
                        ["effectType"] = "Buff",
                        ["value"] = "5%",
                        ["targetType"] = "self",
                        ["duration"] = 2,
                        ["effectDescription"] = "Ключ помогает заметить скрытые печати."
                    })
            });
        accepted["fateCards"] = new JsonArray(
            MortalItemTestFixture.CreateItemFateCard(
                "card_archive_key_memory",
                "Память запертого хранилища",
                isUnlocked: true,
                rewards: new JsonObject
                {
                    ["description"] = "Открывает тайный проход архива."
                }));
        accepted["materialization"]!["sections"]!["mechanics"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        accepted["materialization"]!["sections"]!["bondsAndFateCards"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        MortalItemTestFixture.ResealCanonical(accepted);
        var pending = MortalItemTestFixture.CreateRawRoot(
            creationRef: "new_item_pending_npc_browser",
            materializationId: "mat_item_pending_npc_browser");
        pending["name"] = "UNACCEPTED_NPC_BROWSER_MARKER";

        await _fs.WriteFileAtomicAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(
                    new JsonObject
                    {
                        ["npcId"] = "npc_projection_browser",
                        ["name"] = "Хранитель архива",
                        ["inventory"] = new JsonArray(accepted, pending.DeepClone()),
                        ["equippedItems"] = new JsonObject
                        {
                            ["mainHand"] = "itm_npc_projection_browser",
                            ["offHand"] = "Непринятый предмет экипировки NPC"
                        }
                    })
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            "game_state/npcs/npc_inventory.json",
            new JsonObject
            {
                ["NPCInventoryAdds"] = new JsonArray(
                    new JsonObject
                    {
                        ["NPCId"] = "npc_projection_browser",
                        ["NPCName"] = "Хранитель архива",
                        ["item"] = pending
                    })
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var result = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest("/npc section npc_projection_browser mechanics"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Хранитель архива —", text, StringComparison.Ordinal);
        Assert.Contains("Принятый ключ хранителя архива", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Экипировка", text, StringComparison.Ordinal);
        Assert.Contains("Архивное зрение +2", text, StringComparison.Ordinal);
        Assert.Contains("Вспышка ключа", text, StringComparison.Ordinal);
        Assert.Contains("Память запертого хранилища", text, StringComparison.Ordinal);
        Assert.Contains("Открывает тайный проход архива", text, StringComparison.Ordinal);
        Assert.DoesNotContain("UNACCEPTED_NPC_BROWSER_MARKER", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Непринятый предмет экипировки NPC", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("new_item_pending_npc_browser", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("materializationReceipt", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_InventoryItemDetail_TranslatesRawMechanicalTypeValues()
    {
        var item = CreateAcceptedUiItemFromJson(
            "raw_weapon_1",
            """
            {
              "name": "Клинок с печатью",
              "description": "Рукоять холодна даже у огня.",
              "type": "Weapon",
              "quality": "Good",
              "rarity": "Good",
              "price": 1,
              "volume": 7.25,
              "isConsumption": true,
              "unreadableReason": "Текст закрыт соляной коркой.",
              "sealedReason": "Печать северной артели не снята.",
              "lockedReason": "Замок отвечает только владельцу.",
              "structuredBonuses": [
                {
                  "bonusType": "Skill",
                  "target": "stealth",
                  "value": 1,
                  "valueType": "Flat",
                  "modifierType": "skill",
                  "summary": "Скрытность +1",
                  "condition": {
                    "summary": "Печать откликается только в сумерках.",
                    "weatherRule": "Во время грозы бонус удваивается.",
                    "requestId": "PRIVATE_BROWSER_BONUS_CONDITION_REQUEST"
                  }
                }
              ],
              "combatEffect": [
                {
                  "actionName": "Резонансная зарубка",
                  "actionCost": "Main",
                  "targetPriority": "enemy",
                  "scalingCharacteristic": "dexterity",
                  "effects": [
                    {
                      "effectType": "Damage",
                      "value": "0%",
                      "poiseDamage": "2%",
                      "targetType": "enemy",
                      "targetsCount": 2,
                      "duration": 1,
                      "effectDescription": "Сбивает стойку противника."
                    },
                    {
                      "effectType": "DamageReduction",
                      "value": "15%",
                      "targetType": "self",
                      "targetsCount": 1,
                      "duration": 2,
                      "damageThreshold": 11,
                      "effectDescription": "Печать смягчает тяжёлый удар."
                    }
                  ]
                },
                {
                  "actionName": "Быстрая перевязь",
                  "actionCost": "Fast",
                  "effects": [
                    {
                      "effectType": "Heal",
                      "value": "1%",
                      "targetType": "self",
                      "duration": 1,
                      "effectDescription": "Собирает дыхание владельца."
                    }
                  ]
                }
              ],
              "customProperties": [
                {
                  "interactionType": "onUse",
                  "targetStateName": "печать",
                  "changeValue": "+1",
                  "description": "Печать становится заметнее.",
                  "ritualPattern": "Руна отвечает на три удара молота.",
                  "condition": {
                    "weatherRule": "В тумане отклик длится вдвое дольше.",
                    "requestId": "PRIVATE_BROWSER_CUSTOM_PROPERTY_REQUEST",
                    "ritualGuidance": {
                      "title": "Памятка кузнеца",
                      "steps": ["Ударить по наковальне трижды"]
                    },
                    "repairDebug": {
                      "kind": "mortal_item_materialization_repair",
                      "priority": "critical",
                      "title": "Служебное задание ремонта предмета",
                      "steps": ["Открыть validation_repair_request.json"],
                      "doNotDo": ["Не изменять item_identity_index.json"],
                      "expectedAuthority": "Внутреннее служебное поле ремонта: expectedAuthority",
                      "actualEvidence": "Внутреннее служебное поле ремонта: actualEvidence",
                      "targetFiles": ["Внутреннее служебное поле ремонта: targetFiles"],
                      "canonicalActorNames": ["Внутреннее служебное поле ремонта: canonicalActorNames"],
                      "missingFields": ["Внутреннее служебное поле ремонта: missingFields"],
                      "exactFieldCorrections": ["Внутреннее служебное поле ремонта: exactFieldCorrections"],
                      "requiredCompanionTargets": ["Внутреннее служебное поле ремонта: requiredCompanionTargets"],
                      "templateRefs": ["Внутреннее служебное поле ремонта: templateRefs"],
                      "expectedShape": "Внутреннее служебное поле ремонта: expectedShape",
                      "safeCorrectionRules": ["Внутреннее служебное поле ремонта: safeCorrectionRules"],
                      "transitionClass": "Внутреннее служебное поле ремонта: transitionClass",
                      "repairHint": "Внутреннее служебное поле ремонта: repairHint"
                    }
                  }
                }
              ],
              "questLinks": [
                {
                  "questName": "Путь кузнеца",
                  "role": "ключ к кузнице",
                  "stage": "после ритуала",
                  "condition": {
                    "weather": "гроза над северной кузницей",
                    "requestId": "PRIVATE_BROWSER_QUEST_LINK_REQUEST"
                  }
                }
              ]
            }
            """,
            "mechanics",
            "consumption",
            "readableOrSentient",
            "questRole");
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/items.json",
            new JsonObject
            {
                ["items"] = new JsonArray(item),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/inventory item raw_weapon_1"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        var defaultOutput = text + "\n" + payload;

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Оружие", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("хорошее", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Скрытность", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("плоский бонус", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("равновесие", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("лечение", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("противник", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("сам персонаж", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("основное действие", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("быстрое действие", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("приоритет цели: противник", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("масштабирование: Ловкость", text, StringComparison.Ordinal);
        Assert.Contains("целей: 2", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("порог урона: 11", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("при использовании", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Руна отвечает на три удара молота", text, StringComparison.Ordinal);
        Assert.Contains("В тумане отклик длится вдвое дольше", text, StringComparison.Ordinal);
        Assert.Contains("Памятка кузнеца", text, StringComparison.Ordinal);
        Assert.Contains("Ударить по наковальне трижды", text, StringComparison.Ordinal);
        Assert.Contains("Путь кузнеца", text, StringComparison.Ordinal);
        Assert.Contains("после ритуала", text, StringComparison.Ordinal);
        Assert.Contains("гроза над северной кузницей", text, StringComparison.Ordinal);
        Assert.Contains("Печать откликается только в сумерках", text, StringComparison.Ordinal);
        Assert.Contains("Во время грозы бонус удваивается", text, StringComparison.Ordinal);
        Assert.Contains("Расходуемый предмет", text, StringComparison.Ordinal);
        Assert.Contains("Текст закрыт соляной коркой.", text, StringComparison.Ordinal);
        Assert.Contains("Печать северной артели не снята.", text, StringComparison.Ordinal);
        Assert.Contains("Замок отвечает только владельцу.", text, StringComparison.Ordinal);
        var itemDossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "inventory-item" &&
            block.Title.Contains("Клинок с печатью", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            itemDossier.Sections.SelectMany(static section => section.Facts),
            static fact => fact.Label.Equals("Цена", StringComparison.OrdinalIgnoreCase) &&
                           fact.Value == "1");
        Assert.Contains(
            itemDossier.Sections.SelectMany(static section => section.Facts),
            static fact => fact.Label.Equals("Объём", StringComparison.OrdinalIgnoreCase) &&
                           fact.Value == "7.25 дм³");
        Assert.DoesNotContain("PRIVATE_BROWSER_CUSTOM_PROPERTY_REQUEST", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_BROWSER_BONUS_CONDITION_REQUEST", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_BROWSER_QUEST_LINK_REQUEST", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Внутреннее служебное поле ремонта", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Служебное задание ремонта предмета", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Открыть validation_repair_request.json", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Не изменять item_identity_index.json", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Weapon", defaultOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Good", defaultOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Main", defaultOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Fast", defaultOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Enemy", defaultOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Self", defaultOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("stealth", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Skill", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Flat", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PoiseDamage", text, StringComparison.Ordinal);
        Assert.DoesNotContain("onUse", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Books_ShowsReadableInventoryDocumentsAndSealedReasons()
    {
        await SeedBooksReadingFlowStateAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/книги"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var shelfDossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "document-shelf" &&
            block.Title.Equals("Книжная полка", StringComparison.OrdinalIgnoreCase));
        var documentSection = Assert.Single(shelfDossier.Sections, static section =>
            section.Title.Equals("Документы", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(documentSection.Cards, card => card.Title.Contains("Письмо с площади", StringComparison.OrdinalIgnoreCase) && card.Summary.Contains("Можно читать", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(documentSection.Cards, card => card.Title.Contains("Записка с рынка", StringComparison.OrdinalIgnoreCase) && card.Summary.Contains("1 запись", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(documentSection.Cards, card => card.Title.Contains("Памятная книга", StringComparison.OrdinalIgnoreCase) && card.Summary.Contains("1 запись", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(documentSection.Cards, card => card.Title.Contains("Запечатанное письмо", StringComparison.OrdinalIgnoreCase) && card.Summary.Contains("Не прочесть", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Blocks.SelectMany(EnumerateTables), static table =>
            table.Title.Equals("Книжная полка", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Письмо с площади", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Записка с рынка", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Памятная книга", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Запечатанное письмо", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INLINE_FULL_BODY_MARKER", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SIDECAR_FULL_BODY_MARKER", text, StringComparison.Ordinal);
        Assert.DoesNotContain("JOURNAL_FULL_BODY_MARKER", text, StringComparison.Ordinal);
        Assert.Contains(result.Actions, action =>
            action.Label.Contains("Записка с рынка", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/books doc_sidecar_1", StringComparison.OrdinalIgnoreCase) &&
            action.Style == UiActionStyle.Secondary &&
            action.RequiresConfirmation == false);
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("item_text_updates", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("doc_inline_1", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("doc_sidecar_1", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("doc_journal_1", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("doc_sealed_1", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RAW_REJECTED_BOOK_MARKER", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("ORPHAN_BOOK_SIDECAR_MARKER", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("WRONG_CASE_BOOK_SIDECAR_MARKER", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("RAW_COMMAND_BOOK_SIDECAR_MARKER", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("MALFORMED_BOOK_SIDECAR_MARKER", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("INLINE_FULL_BODY_MARKER", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("SIDECAR_FULL_BODY_MARKER", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("JOURNAL_FULL_BODY_MARKER", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Books_WithStableDocumentId_ShowsOnlySelectedDocumentDetail()
    {
        await SeedBooksReadingFlowStateAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/books doc_sidecar_1"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal("/books doc_sidecar_1", result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Записка с рынка", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SIDECAR_FULL_BODY_MARKER", text, StringComparison.Ordinal);
        Assert.DoesNotContain("MALFORMED_BOOK_SIDECAR_MARKER", payload, StringComparison.Ordinal);
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "document" &&
            block.Title.Contains("Записка с рынка", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("INLINE_FULL_BODY_MARKER", text, StringComparison.Ordinal);
        Assert.DoesNotContain("JOURNAL_FULL_BODY_MARKER", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Книжная полка", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Actions, action =>
            action.Label.Contains("Назад", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/books", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("doc_sidecar_1", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("item_text_updates", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Books_WithNumericStableDocumentId_PrefersSelectorOverShelfIndex()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World"
        }
        """);
        var first = CreateAcceptedUiItemFromJson(
            "doc_first_1",
            """{"name":"Первая записка","type":"Документ","textContent":["FIRST_SHELF_ROW_BODY_MARKER"]}""",
            "readableOrSentient");
        var second = CreateAcceptedUiItemFromJson(
            "doc_second_1",
            """{"name":"Вторая записка","type":"Документ","textContent":["SECOND_SHELF_ROW_BODY_MARKER"]}""",
            "readableOrSentient");
        var numeric = CreateAcceptedUiItemFromJson(
            "2",
            """{"name":"Письмо с номером","type":"Документ","textContent":["NUMERIC_STABLE_ID_BODY_MARKER"]}""",
            "readableOrSentient");
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/items.json",
            new JsonObject
            {
                ["items"] = new JsonArray(first, second, numeric),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/books 2"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal("/books 2", result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Письмо с номером", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NUMERIC_STABLE_ID_BODY_MARKER", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SECOND_SHELF_ROW_BODY_MARKER", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Книжная полка", text, StringComparison.OrdinalIgnoreCase);
    }

    private async Task SeedBooksReadingFlowStateAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World"
        }
        """);
        var inline = CreateAcceptedUiItemFromJson(
            "doc_inline_1",
            """
            {
              "name":"Письмо с площади",
              "type":"Документ",
              "group":"Документы и медиа",
              "textContent":["Лира просит встретиться у фонтана до рассвета. Это длинное письмо продолжается подробностями о стороже, мокрой мостовой и тайном знаке. INLINE_FULL_BODY_MARKER"]
            }
            """,
            "readableOrSentient");
        var sidecar = CreateAcceptedUiItemFromJson(
            "doc_sidecar_1",
            """{"name":"Записка с рынка","type":"note","group":"Документы и медиа","textContent":null,"unreadableReason":"Текст хранится в принятой записи предмета."}""",
            "readableOrSentient");
        var journal = CreateAcceptedUiItemFromJson(
            "doc_journal_1",
            """{"name":"Памятная книга","type":"Книга","group":"Документы и медиа","textContent":null,"isSentient":true}""",
            "readableOrSentient");
        var sealedDocument = CreateAcceptedUiItemFromJson(
            "doc_sealed_1",
            """{"name":"Запечатанное письмо","type":"Документ","group":"Документы и медиа","textContent":null,"unreadableReason":"Печать не позволяет прочесть письмо сейчас."}""",
            "readableOrSentient");
        var rejected = MortalItemTestFixture.CreateRawRoot(
            creationRef: "new_item_rejected_book",
            materializationId: "mat_item_rejected_book");
        rejected["name"] = "RAW_REJECTED_BOOK_MARKER";
        rejected["type"] = "Документ";
        rejected["textContent"] = new JsonArray("RAW_REJECTED_BOOK_MARKER");
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/items.json",
            new JsonObject
            {
                ["items"] = new JsonArray(inline, sidecar, journal, sealedDocument, rejected),
                ["UpdateInventory"] = new JsonArray(rejected.DeepClone()),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync("game_state/inventory/item_text_updates.json", """
        {
          "entries": [
            {
              "itemId": "doc_sidecar_1",
              "existedId": { "invalid": true },
              "itemName": "Не это имя",
              "textContent": ["MALFORMED_BOOK_SIDECAR_MARKER"]
            },
            {
              "existedId": "doc_sidecar_1",
              "itemName": "Не это имя",
              "textContent": [
                "На обороте записки указан путь через северные ворота. Это длинная приписка с именами торговцев, часом встречи и предупреждением о дозорных. SIDECAR_FULL_BODY_MARKER"
              ]
            },
            {
              "itemId": "DOC_INLINE_1",
              "itemName": "Письмо с площади",
              "textContent": ["WRONG_CASE_BOOK_SIDECAR_MARKER"]
            },
            {
              "itemId": "doc_orphan_1",
              "itemName": "ORPHAN_BOOK_SIDECAR_MARKER",
              "textContent": ["ORPHAN_BOOK_SIDECAR_MARKER"]
            }
          ],
          "updateItemTextContents": [
            { "itemId": "doc_inline_1", "textToAppend": "RAW_COMMAND_BOOK_SIDECAR_MARKER" }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/npcs/item_journals.json", """
        {
          "entries": [
            {
              "itemId": "doc_journal_1",
              "itemName": "Другое имя",
              "journalEntries": [
                {
                  "event": "Пробуждение",
                  "description": "Книга шепчет о владельце. В записи слышен скрип пера, сухой шорох страниц и обещание вернуться к началу. JOURNAL_FULL_BODY_MARKER"
                }
              ]
            }
          ]
        }
        """);
    }

    private static bool RowContains(UiTableRow row, string value) =>
        row.Cells.Any(cell => cell.Contains(value, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public async Task ExecuteAsync_Books_WithOnlySealedDocument_DoesNotShowEmptyBooksMessage()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World"
        }
        """);
        var sealedDocument = CreateAcceptedUiItemFromJson(
            "doc_sealed_only_1",
            """{"name":"Запечатанное письмо","type":"Документ","textContent":null,"unreadableReason":"Печать не позволяет прочесть письмо сейчас."}""",
            "readableOrSentient");
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/items.json",
            new JsonObject
            {
                ["items"] = new JsonArray(sealedDocument),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/books"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Запечатанное письмо", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Печать не позволяет прочесть письмо сейчас.", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Данные ещё не созданы.", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_FateActionPrompts_RenderDossierCardsInsteadOfKeyValuePanels()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World",
          "inkFeathers": {
            "current": 80,
            "total": 120
          }
        }
        """);

        var reveal = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/reveal_fate"));

        Assert.Equal(CommandExecutionState.RequiresInput, reveal.State);
        Assert.Empty(reveal.Blocks.SelectMany(EnumerateKeyValueGrids));
        Assert.Empty(reveal.Blocks.SelectMany(EnumerateTables));
        var revealDossier = Assert.Single(reveal.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "fate-reveal" &&
            block.Title == "Открыть Судьбу");
        var revealText = CollectBlockText([revealDossier]);
        Assert.Contains("8 Чернильных Перьев", revealText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("72", revealText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(reveal.Prompts, static prompt => prompt.Id == "confirm_ink_feather_fate_reveal");
        var revealSession = Assert.IsType<UiPromptSession>(reveal.InteractiveSession);
        var cancelled = await _service.CancelPromptSessionAsync(
            new ExplorerPromptSessionCancelRequest(
                revealSession.SessionId,
                revealSession.OwnerId));
        Assert.Equal(CommandExecutionState.Completed, cancelled.State);

        await _fs.WriteFileAtomicAsync(PendingTurnStateService.PendingDiceStatePath, """
        {
          "preGeneratedDices1d20": [4, 9, 16, 2, 5, 8, 11, 14, 17, 20, 1, 3, 6, 7, 10, 12, 13, 15, 18, 19],
          "gachaBaseResult": {
            "diceUsed": [11, 12, 13],
            "baseScore": 44,
            "baseRarity": "Rare",
            "formula": "client-computed gacha base (range 4-80)"
          },
          "isFateLocked": true,
          "createdAtUtc": "2026-06-02T00:00:00Z",
          "lastUpdatedUtc": "2026-06-02T00:00:00Z"
        }
        """);

        var rewrite = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/rewrite_fate"));

        Assert.Equal(CommandExecutionState.RequiresInput, rewrite.State);
        Assert.Empty(rewrite.Blocks.SelectMany(EnumerateKeyValueGrids));
        Assert.Empty(rewrite.Blocks.SelectMany(EnumerateTables));
        var rewriteDossier = Assert.Single(rewrite.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "fate-rewrite" &&
            block.Title == "Переписать Судьбу");
        var rewriteText = CollectBlockText([rewriteDossier]);
        Assert.Contains("[4, 9, 16", rewriteText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("редкая (44)", rewriteText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("20 Чернильных Перьев", rewriteText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("60", rewriteText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Rare", rewriteText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(rewrite.Prompts, static prompt => prompt.Id == "confirm_ink_feather_fate_rewrite");
    }

    [Fact]
    public async Task ExecuteAsync_Inventory_AddsEquipAndUnequipActionsForOrdinaryItems()
    {
        await SeedInventoryEquipmentItemsAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/inv"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var equipAction = Assert.Single(result.Actions, action => action.Label == "Экипировать «Кривой меч»");
        Assert.Equal("inventory-equip-sword_1", equipAction.Id);
        Assert.Equal("/экипировать sword_1", equipAction.Command);
        Assert.Equal(UiActionStyle.Secondary, equipAction.Style);
        Assert.False(equipAction.RequiresConfirmation);

        var unequipAction = Assert.Single(result.Actions, action => action.Label == "Снять «Железный шлем»");
        Assert.Equal("inventory-unequip-Head", unequipAction.Id);
        Assert.Equal("/снять Head", unequipAction.Command);
        Assert.Equal(UiActionStyle.Secondary, unequipAction.Style);
        Assert.False(unequipAction.RequiresConfirmation);

        Assert.DoesNotContain(result.Actions, action => action.Label.StartsWith("Экипировать", StringComparison.OrdinalIgnoreCase) && action.Label.Contains("Факел", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Actions, action => action.Label.StartsWith("Экипировать", StringComparison.OrdinalIgnoreCase) && action.Label.Contains("Сломанный лук", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Actions, action => action.Label.StartsWith("Экипировать", StringComparison.OrdinalIgnoreCase) && action.Label.Contains("Реликвия души", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Actions, action => action.Label.StartsWith("Снять", StringComparison.OrdinalIgnoreCase) && action.Label.Contains("Реликвия души", StringComparison.OrdinalIgnoreCase));
        Assert.All(result.Actions, action =>
        {
            Assert.DoesNotContain("/", action.Label, StringComparison.Ordinal);
            Assert.DoesNotContain("itemId", action.Label, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("API", action.Label, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DTO", action.Label, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task ExecuteAsync_SoulRelics_RendersDossierCardsAndPlayerActions()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая душа",
          "currentRealm": "Chaos Sea",
          "soulRelics": {
            "stored": [
              {
                "relicId": "relic_stored",
                "name": "Клинок Памяти",
                "rarity": "rare",
                "slot": "mainHand",
                "gameplayStatus": { "equipped": false }
              }
            ],
            "equipped": [
              {
                "relicId": "relic_equipped",
                "name": "Шлем Тишины",
                "quality": "legendary",
                "gameplayStatus": { "equipped": true, "currentSlot": "head" }
              }
            ]
          }
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/soul_relics"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Реликвии души", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Хранилище", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Клинок Памяти", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("редк", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Экипировано", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Шлем Тишины", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("легендарное", text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static dossier =>
            dossier.EntityType == "soul-relics" &&
            dossier.Sections.Any(static section => section.Cards.Count > 0));

        var equipAction = Assert.Single(result.Actions, action => action.Id == "soul-relic-equip-relic_stored");
        Assert.Equal("/soul_relic_equip relic_stored", equipAction.Command);
        Assert.Equal(UiActionStyle.Secondary, equipAction.Style);
        Assert.False(equipAction.RequiresConfirmation);

        var unequipAction = Assert.Single(result.Actions, action => action.Id == "soul-relic-unequip-head");
        Assert.Equal("/soul_relic_unequip head", unequipAction.Command);
        Assert.Equal(UiActionStyle.Secondary, unequipAction.Style);
        Assert.False(unequipAction.RequiresConfirmation);
    }

    [Theory]
    [InlineData("/soul_relics", "soul-relics", "Реликвии души", "Клинок Памяти")]
    [InlineData("/afterlife_archive", "afterlife-archive", "Архив души", "Песнь Первого Маяка")]
    [InlineData("/archive_candidates", "archive-candidates", "Кандидаты в Архив", "Песня маяка")]
    public async Task ExecuteAsync_AfterlifeRelicArchiveOverviews_RenderNamedDossiersInsteadOfGenericTables(
        string command,
        string entityType,
        string expectedTitle,
        string expectedEntry)
    {
        await SeedRichAfterlifeRelicArchiveDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), dossier =>
            dossier.EntityType == entityType &&
            dossier.Title.Contains(expectedTitle, StringComparison.OrdinalIgnoreCase) &&
            dossier.Sections.Any(static section => section.Cards.Count > 0));

        var text = CollectBlockText(result.Blocks);
        Assert.Contains(expectedEntry, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ID", text, StringComparison.Ordinal);
        AssertNoAfterlifeIssue1064TechnicalLeak(result);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeArchive_WhenCompletedLifeChronicleExists_RendersLifeMemory()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Test Soul",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "inkFeathers": { "current": 10 },
          "livesHistory": [
            {
              "incarnation": 1,
              "title": "Жизнь Асурена де Вальмона",
              "summary": "Короткая первая жизнь завершилась добровольным возвращением в Море Хаоса.",
              "turnsLived": 8,
              "endedBy": "voluntary_life_end",
              "rewards": {
                "inkFeathers": 10,
                "soulRelicIds": [ "relic_life_001_cut_registry_grain" ]
              }
            }
          ],
          "soulRelics": {
            "stored": [
              {
                "relicId": "relic_life_001_cut_registry_grain",
                "name": "Зерно разрезанной описи",
                "rarity": "common",
                "sourceLife": 1
              }
            ]
          },
          "afterlifeArchive": { "stored": [] }
        }
        """);
        await _fs.WriteFileAtomicAsync("lore/chaos_sea/player_chronicle.json", """
        {
          "entries": [
            {
              "entryId": "chronicle_life_001_evaluation",
              "incarnation": 1,
              "title": "Жизнь Асурена де Вальмона",
              "summary": "Короткая первая жизнь завершилась добровольным возвращением в Море Хаоса.",
              "turnsLived": 8,
              "endedBy": "voluntary_life_end",
              "rewards": {
                "inkFeathers": 10,
                "enlightenmentExperience": 1,
                "soulRelicIds": [ "relic_life_001_cut_registry_grain" ]
              },
              "recordedAtTurn": 9
            }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_archive"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Завершённые жизни", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Жизнь Асурена де Вальмона", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Зерно разрезанной описи", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Сохранённых записей Архива пока нет", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/archive_consultation", "archive-consultation", "Архивная консультация", "Азалия")]
    [InlineData("/archive_project_fuel", "archive-project-fuel", "Подпитка проекта Архивом", "Песнь кузни")]
    public async Task ExecuteAsync_AfterlifeArchiveActionOverviews_RenderDossiersInsteadOfLegacyPanels(
        string command,
        string entityType,
        string expectedTitle,
        string expectedText)
    {
        await SeedRichAfterlifeRelicArchiveDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        var dossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), dossier =>
            dossier.EntityType == entityType &&
            dossier.Title.Equals(expectedTitle, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dossier.Sections, static section => section.Title.Equals("Свободные записи Архива", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dossier.Sections, static section => section.Title.Contains("Хранител", StringComparison.OrdinalIgnoreCase));

        var text = CollectBlockText([dossier]);
        Assert.Contains(expectedText, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JSON", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/soul_relics", "soul-relic-detail-relic_memory_blade", "/soul_relics реликвия relic_memory_blade", "Клинок Памяти", "")]
    [InlineData("/soul_relic_equip", "soul-relic-detail-relic_memory_blade", "/soul_relics реликвия relic_memory_blade", "Клинок Памяти", "confirm_soul_relic_write")]
    [InlineData("/soul_relic_unequip", "soul-relic-detail-relic_silent_helm", "/soul_relics реликвия relic_silent_helm", "Шлем Тишины", "confirm_soul_relic_write")]
    [InlineData("/afterlife_archive", "afterlife-archive-detail-archive_lore_001", "/afterlife_archive запись archive_lore_001", "Песнь Первого Маяка", "")]
    [InlineData("/archive_candidates", "archive-candidate-detail-candidate_mayak", "/archive_candidates кандидат candidate_mayak", "Песня маяка", "")]
    [InlineData("/archive_consultation", "archive-consultation-detail-guardian_azalia", "/archive_consultation хранитель guardian_azalia", "Азалия", "confirm_archive_consultation")]
    [InlineData("/archive_project_fuel", "archive-project-fuel-detail-guardian_azalia-project_forge_song", "/archive_project_fuel проект guardian_azalia::project_forge_song", "Песнь кузни", "confirm_archive_project_fuel")]
    public async Task ExecuteAsync_AfterlifeRelicArchiveOverviews_ExposeIssue1064ReadOnlyDetailActions(
        string command,
        string expectedActionId,
        string expectedDetailCommand,
        string expectedLabelText,
        string expectedPromptId)
    {
        await PrepareRichAfterlifeRelicArchiveFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            command,
            OwnerId: "browser-issue-1064",
            OwnerLabel: "Browser issue 1064"));

        Assert.NotEqual(CommandExecutionState.Failed, result.State);
        Assert.NotEqual(CommandExecutionState.Blocked, result.State);
        var action = Assert.Single(result.Actions, candidate => candidate.Id == expectedActionId);
        Assert.Equal(expectedDetailCommand, action.Command);
        Assert.Contains("Подробно", action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedLabelText, action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, action.Style);
        Assert.False(action.RequiresConfirmation);
        Assert.DoesNotContain("/", action.Label, StringComparison.Ordinal);
        Assert.DoesNotContain("DTO", action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API", action.Label, StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(expectedPromptId))
            Assert.Contains(result.Prompts, prompt => prompt.Id == expectedPromptId);
    }

    [Theory]
    [InlineData("/soul_relics реликвия relic_memory_blade", "soul-relic-detail", "Реликвия души: Клинок Памяти", "Метки")]
    [InlineData("/afterlife_archive запись archive_lore_001", "afterlife-archive-detail", "Архив души: Песнь Первого Маяка", "Метки")]
    [InlineData("/archive_candidates кандидат candidate_mayak", "archive-candidate-detail", "Кандидат в Архив: Песня маяка", "Метки")]
    public async Task ExecuteAsync_AfterlifeRelicArchiveDetails_RenderDossiersInsteadOfLegacyPanels(
        string command,
        string expectedEntityType,
        string expectedTitle,
        string expectedSectionTitle)
    {
        await PrepareRichAfterlifeRelicArchiveFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Empty(result.Blocks.SelectMany(EnumerateKeyValueGrids));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.Empty(result.Blocks.SelectMany(EnumeratePanels));
        var dossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), dossier =>
            dossier.EntityType == expectedEntityType &&
            dossier.Title.Equals(expectedTitle, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dossier.Sections, section => section.Title.Equals("Сведения", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dossier.Sections, section => section.Title.Equals(expectedSectionTitle, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/soul_relics реликвия relic_memory_blade", "Реликвия души: Клинок Памяти", "Память режет тьму", "Шлем Тишины")]
    [InlineData("/afterlife_archive запись archive_lore_001", "Архив души: Песнь Первого Маяка", "Полный текст маяка", "Запечатанный договор")]
    [InlineData("/archive_candidates кандидат candidate_mayak", "Кандидат в Архив: Песня маяка", "Кандидат хранит свет", "Тайный договор")]
    [InlineData("/archive_consultation хранитель guardian_azalia", "Архивная консультация: Азалия", "память", "Недоверчивый Страж")]
    [InlineData("/archive_project_fuel проект guardian_azalia::project_forge_song", "Подпитка проекта: Песнь кузни", "исследование знаний", "Скрытый проект")]
    public async Task ExecuteAsync_AfterlifeRelicArchiveDetails_RenderFocusedPlayerFacingDetailWithoutRawJson(
        string command,
        string expectedTitle,
        string expectedText,
        string excludedText)
    {
        await SeedRichAfterlifeRelicArchiveDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoAfterlifeIssue1064TechnicalLeak(result);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(expectedTitle, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedText, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(excludedText, text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/archive_consultation хранитель guardian_azalia", "archive-consultation-detail", "Архивная консультация: Азалия", "Песнь Первого Маяка")]
    [InlineData("/archive_project_fuel проект guardian_azalia::project_forge_song", "archive-project-fuel-detail", "Подпитка проекта: Песнь кузни", "Песнь Первого Маяка")]
    public async Task ExecuteAsync_AfterlifeArchiveActionDetails_RenderDossiersInsteadOfLegacyPanels(
        string command,
        string expectedEntityType,
        string expectedTitle,
        string expectedArchiveEntry)
    {
        await SeedRichAfterlifeRelicArchiveDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        var dossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), dossier =>
            dossier.EntityType == expectedEntityType &&
            dossier.Title.Equals(expectedTitle, StringComparison.OrdinalIgnoreCase));

        var text = CollectBlockText([dossier]);
        Assert.Contains(expectedArchiveEntry, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JSON", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/soul_relics реликвия missing_relic")]
    [InlineData("/afterlife_archive запись missing_archive")]
    [InlineData("/archive_candidates кандидат missing_candidate")]
    [InlineData("/archive_consultation хранитель missing_guardian")]
    [InlineData("/archive_project_fuel проект guardian_azalia::missing_project")]
    public async Task ExecuteAsync_AfterlifeRelicArchiveDetails_UnknownIdsReturnPlayerFacingUnavailableText(string command)
    {
        await PrepareRichAfterlifeRelicArchiveFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoAfterlifeIssue1064TechnicalLeak(result);
        Assert.Contains("не удалось открыть", CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_InventoryEquipAction_OpensPromptSessionWithItemSlotAndConfirmation()
    {
        await SeedInventoryEquipmentItemsAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/экипировать sword_1",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.True(result.InteractiveSession.RequiresLocalUiLock);
        Assert.Equal("/api/explorer/prompt-sessions/submit", result.InteractiveSession.SubmitEndpoint);
        Assert.True(_fs.FileExists(LocalUiSessionLockService.LockPath));

        var itemPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "item_identity"));
        Assert.True(itemPrompt.Required);
        Assert.Equal("sword_1", itemPrompt.Options.Single().Value);
        Assert.Contains("Кривой меч", itemPrompt.Options.Single().Label, StringComparison.OrdinalIgnoreCase);

        var slotPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "equipment_slot"));
        Assert.True(slotPrompt.Required);
        Assert.Contains(slotPrompt.Options, option => option.Value == "MainHand" && option.Label.Contains("Основная рука", StringComparison.OrdinalIgnoreCase));

        var confirmation = Assert.IsType<UiConfirmationPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "confirm_inventory_write"));
        Assert.True(confirmation.Required);
        Assert.False(confirmation.DefaultValue);
        var blockText = CollectBlockText(result.Blocks);
        Assert.DoesNotContain("Browser-write", blockText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rollback", blockText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("snapshot", blockText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("terminal", blockText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/control", blockText, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/экипировать sword_1", "inventory-equip", "Экипировка предмета", "Кривой меч")]
    [InlineData("/снять Head", "inventory-unequip", "Снятие предмета", "Железный шлем")]
    [InlineData("/выбросить_предмет torch_1", "inventory-drop", "Выброс предмета", "Факел")]
    [InlineData("/разделить_стопку torch_1", "inventory-split", "Разделение стопки", "Факел")]
    [InlineData("/объединить_стопки torch_1", "inventory-merge", "Объединение стопок", "Факел")]
    public async Task ExecuteAsync_InventoryActionPrompts_RenderDossierCardsInsteadOfTables(
        string command,
        string expectedEntityType,
        string expectedTitle,
        string expectedItemName)
    {
        await SeedInventoryEquipmentItemsAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            command,
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);

        var dossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), block =>
            block.EntityType == expectedEntityType &&
            block.Title == expectedTitle);
        Assert.Contains(dossier.Sections, static section =>
            section.Presentation == "cards" &&
            section.Cards.Count > 0);
        Assert.Contains(expectedItemName, CollectBlockText([dossier]), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/хранилище_предметы", "storage-move", "Предметы в хранилище", "Приватный письменный стол", "Письмо с печатью")]
    [InlineData("/транспорт_предметы", "vehicle-move", "Предметы в транспорте", "Серый конь", "Седельная сумка")]
    public async Task ExecuteAsync_StorageAndVehicleMovePrompts_RenderDossierCardsInsteadOfTables(
        string command,
        string expectedEntityType,
        string expectedTitle,
        string expectedTargetName,
        string expectedStoredItemName)
    {
        await SeedStorageTransportPromptDataAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            command,
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);

        var dossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), block =>
            block.EntityType == expectedEntityType &&
            block.Title == expectedTitle);
        Assert.Contains(dossier.Sections, static section =>
            section.Presentation == "cards" &&
            section.Cards.Count > 0);
        var dossierText = CollectBlockText([dossier]);
        Assert.Contains(expectedTargetName, dossierText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedStoredItemName, dossierText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Рюкзак", dossierText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_InventoryEquip_WritesEquipmentAndReleasesLock()
    {
        await SeedInventoryEquipmentItemsAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/экипировать sword_1",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["item_identity"] = JsonValue.Create("sword_1"),
                ["equipment_slot"] = JsonValue.Create("MainHand"),
                ["confirm_inventory_write"] = JsonValue.Create(true)
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        Assert.Empty(completed.Prompts);
        Assert.Null(completed.InteractiveSession);
        Assert.Contains("Кривой меч", CollectBlockText(completed.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("экипирован", CollectBlockText(completed.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(completed.Blocks, static block =>
            block is UiRawJsonBlock raw && raw.Title.Contains("JSON: результат браузерной записи", StringComparison.OrdinalIgnoreCase));

        var inventory = JsonNode.Parse((await _fs.ReadFileAsync("game_state/inventory/items.json"))!)!.AsObject();
        Assert.Equal("sword_1", inventory["equippedItems"]!["MainHand"]!.GetValue<string>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_InventoryUnequip_WritesNullAndReleasesLock()
    {
        await SeedInventoryEquipmentItemsAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/снять Head",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var slotPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(started.Prompts, prompt => prompt.Id == "equipment_slot"));
        Assert.Equal("Head", slotPrompt.Options.Single().Value);

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["equipment_slot"] = JsonValue.Create("Head"),
                ["confirm_inventory_write"] = JsonValue.Create(true)
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        Assert.Contains("Железный шлем", CollectBlockText(completed.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("снят", CollectBlockText(completed.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(completed.Blocks, static block =>
            block is UiRawJsonBlock raw && raw.Title.Contains("JSON: результат браузерной записи", StringComparison.OrdinalIgnoreCase));

        var inventory = JsonNode.Parse((await _fs.ReadFileAsync("game_state/inventory/items.json"))!)!.AsObject();
        Assert.Null(inventory["equippedItems"]!["Head"]);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_InventoryEquip_WhenItemDisappears_KeepsSessionOpenWithPlayerFacingError()
    {
        await SeedInventoryEquipmentItemsAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/экипировать sword_1",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/items.json",
            new JsonObject
            {
                ["equippedItems"] = new JsonObject
                {
                    ["Head"] = null,
                    ["MainHand"] = null,
                    ["OffHand"] = null
                },
                ["items"] = new JsonArray()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var validation = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["item_identity"] = JsonValue.Create("sword_1"),
                ["equipment_slot"] = JsonValue.Create("MainHand"),
                ["confirm_inventory_write"] = JsonValue.Create(true)
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.RequiresInput, validation.State);
        Assert.NotNull(validation.InteractiveSession);
        var notificationText = string.Join("\n", validation.Notifications.Select(notification => notification.Message));
        Assert.Contains(validation.Notifications, notification =>
            notification.Message.Contains("Предмет не найден", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Browser-write", notificationText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rollback", notificationText, StringComparison.OrdinalIgnoreCase);

        var inventory = JsonNode.Parse((await _fs.ReadFileAsync("game_state/inventory/items.json"))!)!.AsObject();
        Assert.Null(inventory["equippedItems"]!["MainHand"]);
        Assert.True(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task ExecuteAsync_InventoryEquip_WithActiveGmTurn_BlocksPromptSession()
    {
        await SeedInventoryEquipmentItemsAsync();
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "session_web",
          "requestId": "request_web",
          "turnNumber": 12,
          "playerAction": "Тестовый ход",
          "timestamp": "2026-05-20T00:00:00Z"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", """
        {
          "sessionId": "session_web",
          "requestId": "request_web",
          "turnNumber": 12,
          "files": {}
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/экипировать sword_1"));

        Assert.Equal(CommandExecutionState.Pending, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Contains("Активный ход GM", CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
        var notificationText = string.Join("\n", result.Notifications.Select(notification => notification.Message));
        Assert.DoesNotContain("Browser-write", notificationText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GM-turn", notificationText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rollback", notificationText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_InventoryEquip_WithOtherLocalLock_BlocksPromptSession()
    {
        await SeedInventoryEquipmentItemsAsync();
        var lockService = new LocalUiSessionLockService(_fs);
        await lockService.AcquireOrRefreshAsync(
            new LocalUiSessionLockOwner("console-owner", "console", "Консоль", TimeSpan.FromMinutes(5)),
            "console inventory");

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/экипировать sword_1",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.Blocked, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Contains("Форма уже открыта", CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Локальная UI-блокировка", CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
        var inventory = JsonNode.Parse((await _fs.ReadFileAsync("game_state/inventory/items.json"))!)!.AsObject();
        Assert.Null(inventory["equippedItems"]!["MainHand"]);
    }

    [Fact]
    public async Task ExecuteAsync_NpcBundle_HidesPathsAndSkipsMissingFiles()
    {
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "npcs": [
            { "npcId": "npc_1", "name": "Мирра" }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks.OfType<UiTableBlock>(), static block => block.Title == "Персонажи");
        var overview = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "npc-collection" &&
            block.Title == "Персонажи");
        var npcSection = Assert.Single(overview.Sections, static section => section.Id == "npcs");
        Assert.Equal("collection", npcSection.Presentation);
        Assert.Contains("1 персонаж", CollectBlockText([overview]), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(npcSection.Cards, static card => card.Title == "Мирра");
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "npc-collection" &&
            block.Title == "Персонажи");
        Assert.DoesNotContain("отсутствует", CollectBlockText([overview]), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", CollectBlockText([overview]), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("game_state/npcs", CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_NpcRichDetails_ExposesPlayerFacingDossierCollection()
    {
        await SeedRichNpcDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(result.Actions, action =>
            action.Label.Contains("Серафина", StringComparison.OrdinalIgnoreCase) &&
            action.Label.Contains("Дневник / мысли", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/npc section npc_serafina journal", StringComparison.OrdinalIgnoreCase) &&
            action.Style == UiActionStyle.Secondary &&
            action.RequiresConfirmation == false);

        Assert.DoesNotContain(result.Blocks.OfType<UiTableBlock>(), static block => block.Title == "Разделы НПС");
        var npcOverview = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "npc-collection" &&
            block.Title == "Персонажи");
        var npcSection = Assert.Single(npcOverview.Sections, static section => section.Id == "npcs");
        Assert.Equal("collection", npcSection.Presentation);
        Assert.NotEmpty(npcSection.Cards);
        var serafinaCard = Assert.Single(npcSection.Cards, static card => card.Title == "Серафина");
        var npcCardText = CollectBlockText([npcOverview]);
        Assert.DoesNotContain(serafinaCard.Facts, static fact =>
            fact.Label is "Дневник / мысли" or "Личные квесты" or "Активности" or "Отношения / замки" or "Навыки");
        Assert.DoesNotContain(serafinaCard.Badges, static badge =>
            badge.Label.Contains("раздел", StringComparison.OrdinalIgnoreCase));
        var payload = SerializeResult(result);
        Assert.DoesNotContain("\"summary\":\"2 записи\"", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"summary\":\"3 квеста\"", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"summary\":\"1 активность\"", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"summary\":\"1 запись\"", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("2 записи", npcCardText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("3 квеста", npcCardText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1 активность", npcCardText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1 запись", npcCardText, StringComparison.OrdinalIgnoreCase);
        var journalCard = Assert.Single(serafinaCard.Nested, static card => card.Title == "Дневник / мысли");
        Assert.Contains(serafinaCard.Nested, static card => card.Title == "Личные квесты");
        Assert.Contains(serafinaCard.Nested, static card => card.Title == "Активности");
        var journalEntryCard = Assert.Single(journalCard.Cards, static card => card.Title == "Письмо найдено");
        Assert.DoesNotContain(';', journalEntryCard.Summary);
        Assert.Contains(journalEntryCard.Facts, static fact =>
            fact.Value == "Сомневается, стоит ли доверять письму.");
        Assert.Contains(journalEntryCard.Facts, static fact =>
            fact.Label == "Изменение отношения" &&
            fact.Value == "+1");
        Assert.DoesNotContain("Доступные разделы", npcCardText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Краткая карточка", npcCardText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Инвентарь", npcCardText, StringComparison.OrdinalIgnoreCase);

        var text = CollectBlockText(result.Blocks);
        Assert.DoesNotContain("Серафина — Дневник / мысли", text, StringComparison.Ordinal);
        Assert.Contains("Сомневается, стоит ли доверять письму.", text, StringComparison.Ordinal);
        Assert.Contains("Сделка на рассвете", text, StringComparison.Ordinal);
        Assert.Contains("Доставить письмо в архив", text, StringComparison.Ordinal);
        AssertNoGenericDetailsTables(result);
        AssertNoFlattenedStructuredDetails(result);
        Assert.DoesNotContain("game_state/npcs", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".json", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("npc_serafina", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/npc_talk", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/npc_trade", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private_mortal_materialization_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("materializationId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("materializedAtTurn", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("empty_by_design", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_NpcTalk_RendersDossierCardsInsteadOfTables()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World"
        }
        """);
        await SeedRichNpcDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc_talk npc_serafina"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);

        var dossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "npc-conversation" &&
            block.Title == "Разговор с НПС");
        var section = Assert.Single(dossier.Sections, static section => section.Id == "npc-conversation-known-npcs");
        Assert.Equal("cards", section.Presentation);
        Assert.Contains(section.Cards, static card =>
            card.Title == "Серафина" &&
            card.Facts.Any(static fact => fact.Label == "Где находится" && fact.Value == "Северные ворота") &&
            card.Facts.Any(static fact => fact.Label == "Отношение" && fact.Value == "42"));

        var text = CollectBlockText([dossier]);
        Assert.Contains("Выбран собеседник: Серафина", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Северные ворота", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("npc_serafina", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_NpcSectionDetail_ShowsOnlyRequestedDrilldownSection()
    {
        await SeedRichNpcDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc section npc_serafina journal"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal("/npc section npc_serafina journal", result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Серафина — Дневник / мысли", text, StringComparison.Ordinal);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.Contains("Сомневается, стоит ли доверять письму.", text, StringComparison.Ordinal);
        AssertNoGenericDetailsTables(result);
        AssertNoFlattenedStructuredDetails(result);
        Assert.DoesNotContain("Сделка на рассвете", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Проверяет печати у северных ворот", text, StringComparison.Ordinal);
        Assert.Contains(result.Actions, action =>
            action.Label.Contains("Назад", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/npc", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("game_state/npcs", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_NpcSectionAction_WithQuotedNpcNameSelectorOpensSection()
    {
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "npcs": [
            {
              "NPCId": null,
              "name": "Магистра Селена"
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_journals.json", """
        {
          "journals": [
            {
              "NPCName": "Магистра Селена",
              "journalEntries": [
                {
                  "event": "Проверка печати",
                  "description": "Селена сверяет знак письма с архивной книгой."
                }
              ]
            }
          ]
        }
        """);

        var overview = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/нпс"));
        var action = Assert.Single(overview.Actions, static action =>
            action.Label.Contains("Магистра Селена", StringComparison.OrdinalIgnoreCase) &&
            action.Label.Contains("Дневник", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("\"Магистра Селена\"", action.Command, StringComparison.Ordinal);

        var detail = await _service.ExecuteAsync(new ExplorerWebCommandRequest(action.Command));
        var text = CollectBlockText(detail.Blocks);

        Assert.Equal(CommandExecutionState.Completed, detail.State);
        Assert.Contains("Магистра Селена — Дневник / мысли", text, StringComparison.Ordinal);
        Assert.Empty(detail.Blocks.SelectMany(EnumerateTables));
        Assert.Contains("Селена сверяет знак письма с архивной книгой.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Такой раздел НПС не найден", text, StringComparison.Ordinal);
        AssertNoGenericDetailsTables(detail);
        AssertNoFlattenedStructuredDetails(detail);
    }

    [Fact]
    public async Task ExecuteAsync_NpcMechanicsAndMemoryDetails_LabelNonPairScalarDetails()
    {
        await SeedRichNpcDrilldownFilesAsync();

        var mechanics = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc section npc_serafina mechanics"));
        var memory = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc section npc_serafina memory"));
        var text = CollectBlockText(mechanics.Blocks.Concat(memory.Blocks));

        Assert.Equal(CommandExecutionState.Completed, mechanics.State);
        Assert.Equal(CommandExecutionState.Completed, memory.State);
        Assert.Contains("Название навыка", text, StringComparison.Ordinal);
        Assert.Contains("Арканическая диагностика", text, StringComparison.Ordinal);
        Assert.Contains("Тип навыка", text, StringComparison.Ordinal);
        Assert.Contains("основан на знаниях", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Редкость", text, StringComparison.Ordinal);
        Assert.Contains("редк", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Название карты", text, StringComparison.Ordinal);
        Assert.Contains("Холодная милость наставника", text, StringComparison.Ordinal);
        Assert.Contains("Название воспоминания", text, StringComparison.Ordinal);
        Assert.Contains("Неудачный урок резонанса", text, StringComparison.Ordinal);
        Assert.DoesNotContain("KnowledgeBased", text, StringComparison.OrdinalIgnoreCase);
        AssertNoGenericDetailsTables(mechanics);
        AssertNoGenericDetailsTables(memory);
        AssertNoFlattenedStructuredDetails(mechanics);
        AssertNoFlattenedStructuredDetails(memory);
    }

    [Fact]
    public async Task ExecuteAsync_NpcPersonalitySection_ShowsTraitsAndOfficialMasks()
    {
        await SeedRichNpcDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc section npc_serafina personality"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Серафина — Личность / маски", text, StringComparison.Ordinal);
        Assert.Contains("Образ", text, StringComparison.Ordinal);
        Assert.Contains("Строгая наставница", text, StringComparison.Ordinal);
        Assert.Contains("Черта характера", text, StringComparison.Ordinal);
        Assert.Contains("Дисциплина", text, StringComparison.Ordinal);
        Assert.Contains("Сила черты", text, StringComparison.Ordinal);
        Assert.Contains("9/10", text, StringComparison.Ordinal);
        Assert.Contains("Темперамент", text, StringComparison.Ordinal);
        Assert.Contains("Сдержанный", text, StringComparison.Ordinal);
        Assert.Contains("Мораль", text, StringComparison.Ordinal);
        Assert.Contains("Законопослушный нейтральный", text, StringComparison.Ordinal);
        Assert.Contains("Маска", text, StringComparison.Ordinal);
        Assert.Contains("Наставница академии", text, StringComparison.Ordinal);
        Assert.Contains("Активность", text, StringComparison.Ordinal);
        Assert.Contains("активна", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Триггер", text, StringComparison.Ordinal);
        Assert.Contains("Проявляется при разговоре об академии.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("mask_serafina_mentor", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("concealedTruth", text, StringComparison.OrdinalIgnoreCase);
        AssertNoGenericDetailsTables(result);
        AssertNoFlattenedStructuredDetails(result);
    }

    [Fact]
    public async Task ExecuteAsync_NpcRichDetails_ShowsFateUnlockConditionsAndDirectTradeAction()
    {
        await SeedNpcFateAndMerchantFilesAsync();

        var overview = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc"));
        var memory = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc section npc_serafina memory"));
        var overviewText = CollectBlockText(overview.Blocks);
        var memoryText = CollectBlockText(memory.Blocks);

        Assert.Equal(CommandExecutionState.Completed, overview.State);
        Assert.Equal(CommandExecutionState.Completed, memory.State);
        Assert.Contains("Условия открытия", memoryText, StringComparison.Ordinal);
        Assert.Contains("Доверить Селене полный текст загадочного письма.", memoryText, StringComparison.Ordinal);
        Assert.Contains("Требуемое отношение", memoryText, StringComparison.Ordinal);
        Assert.Contains("200", memoryText, StringComparison.Ordinal);
        Assert.Contains("Награда", memoryText, StringComparison.Ordinal);
        Assert.Contains("Открывает консультацию по опасным магическим следам.", memoryText, StringComparison.Ordinal);
        Assert.Contains("Торговец", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Можно торговать", overviewText, StringComparison.OrdinalIgnoreCase);
        var overviewDossier = Assert.Single(overview.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "npc-collection");
        var npcSection = Assert.Single(overviewDossier.Sections, static section => section.Id == "npcs");
        var merchantCard = Assert.Single(npcSection.Cards, static card => card.Title == "Ворон Рилль");
        Assert.Contains("Торговец", merchantCard.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("можно торговать", merchantCard.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(overview.Actions, action =>
            action.Label.Contains("Торговать", StringComparison.OrdinalIgnoreCase) &&
            action.Label.Contains("Ворон Рилль", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/npc_trade npc_artifact_trader_voron", StringComparison.OrdinalIgnoreCase) &&
            action.Style == UiActionStyle.Secondary &&
            action.RequiresConfirmation == false);
        AssertNoGenericDetailsTables(overview);
        AssertNoGenericDetailsTables(memory);
        AssertNoFlattenedStructuredDetails(overview);
        AssertNoFlattenedStructuredDetails(memory);
    }

    [Fact]
    public async Task ExecuteAsync_NpcPersonalQuestSection_OffersSpecificQuestDetails()
    {
        await SeedRichNpcDrilldownFilesAsync();

        var section = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc section npc_serafina personal-quests"));

        Assert.Equal(CommandExecutionState.Completed, section.State);
        Assert.Contains(section.Actions, action =>
            action.Label.Contains("Сделка на рассвете", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/npc quest npc_serafina quest_serafina_letter", StringComparison.OrdinalIgnoreCase) &&
            action.Style == UiActionStyle.Secondary &&
            action.RequiresConfirmation == false);
        Assert.Contains(section.Actions, action =>
            action.Label.Contains("Просьба без метки", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/npc quest npc_serafina просьба_без_метки", StringComparison.OrdinalIgnoreCase));

        var detail = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc quest npc_serafina quest_serafina_letter"));
        var text = CollectBlockText(detail.Blocks);
        var payload = SerializeResult(detail);

        Assert.Equal(CommandExecutionState.Completed, detail.State);
        Assert.Contains("Серафина", text, StringComparison.Ordinal);
        Assert.Contains("Сделка на рассвете", text, StringComparison.Ordinal);
        Assert.Contains("Активен", text, StringComparison.Ordinal);
        Assert.Contains("Серафина просит передать письмо без лишних свидетелей.", text, StringComparison.Ordinal);
        Assert.Contains("Доставить письмо в архив", text, StringComparison.Ordinal);
        Assert.Contains("Получить ключ от боковой двери", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Отложенный долг", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Active", text, StringComparison.Ordinal);
        Assert.DoesNotContain(detail.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("game_state/npcs", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(detail.Actions, action =>
            action.Label.Contains("Назад к личным квестам", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/npc section npc_serafina personal-quests", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_NpcRichDetails_DefaultProjectionHidesInternalFieldsAndMasks()
    {
        await SeedNpcInternalLeakDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain("reward_serafina_secret", CollectBlockText(result.Blocks), StringComparison.Ordinal);

        var questSection = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc section npc_serafina personal-quests"));
        var memorySection = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc section npc_serafina memory"));
        var text = CollectBlockText(questSection.Blocks.Concat(memorySection.Blocks));

        Assert.Contains("Видимый след в памяти", text, StringComparison.Ordinal);
        Assert.Contains("Клятва у печати", text, StringComparison.Ordinal);
        Assert.Contains("Получить ключ от боковой двери", text, StringComparison.Ordinal);
        AssertNoGenericDetailsTables(questSection);
        AssertNoGenericDetailsTables(memorySection);
        AssertNoFlattenedStructuredDetails(questSection);
        AssertNoFlattenedStructuredDetails(memorySection);

        Assert.DoesNotContain("image_prompt", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prompt-for-gm", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Dto", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reward_serafina_secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("memory_serafina_debug", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mask_serafina_false_face", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Образ доверенного архивариуса", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Скрывает связь с дозором", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Маска", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NpcMechanicsSection_LabelIncludesInventoryWhenInventoryRowsExist()
    {
        await SeedNpcMechanicsDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc"));

        Assert.DoesNotContain(result.Blocks.OfType<UiTableBlock>(), static block => block.Title == "Разделы НПС");
        var overview = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "npc-collection");
        var npcSection = Assert.Single(overview.Sections, static section => section.Id == "npcs");
        var npcCard = Assert.Single(npcSection.Cards, static card => card.Title == "Серафина");
        Assert.Contains(npcCard.Nested, static card => card.Title.Contains("инвентарь", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(npcCard.Facts, static fact =>
            fact.Label.Contains("инвентарь", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fact.Label, "Навыки", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_NpcBundle_WithoutFiles_ShowsNotCreatedMessage()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var message = Assert.IsType<UiMessageBlock>(Assert.Single(result.Blocks));
        Assert.Equal(UiNotificationSeverity.Info, message.Severity);
        Assert.Equal("Персонажи", message.Title);
        Assert.Equal("Данные ещё не созданы.", message.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Map_ReturnsInteractiveMapBlock()
    {
        await SeedMortalFilesAsync();
        var square = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_square",
            "Старая площадь",
            "visited",
            x: 4,
            y: 7);
        square["region"] = "Северный край";
        square["description"] = "Площадь под серым небом.";
        var gate = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_gate",
            "Северные ворота",
            "discovered",
            x: 4,
            y: 8);
        var link = MortalLocationTestFixture.CreateCanonicalLink("loc_square", "loc_gate");
        link["directionLabel"] = "север";
        await WriteCanonicalBrowserMortalLocationStateAsync(
            [square, gate],
            MortalLocationTestFixture.CreateCurrentProjection(square),
            [square, gate],
            [link]);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/map"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var mapBlock = Assert.Single(result.Blocks.OfType<UiMapBlock>());
        Assert.Equal("Смертный мир", mapBlock.Map.Realm);
        Assert.Equal("loc_square", mapBlock.Map.CurrentNodeId);
        Assert.Contains(mapBlock.Map.Nodes, static node => node.IsCurrent && node.Label == "Старая площадь");
        Assert.Contains(mapBlock.Map.Links, static link => link.TargetNodeId == "loc_gate");
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "map-summary" &&
            block.Title.Equals("Сводка карты", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.Empty(result.Blocks.OfType<UiRawJsonBlock>());
    }

    [Fact]
    public async Task ExecuteAsync_Map_InChaosSea_ReturnsAbodeConstellationMap()
    {
        await SeedChaosSeaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/map"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var mapBlock = Assert.Single(result.Blocks.OfType<UiMapBlock>());
        Assert.Equal("Море Хаоса", mapBlock.Map.Realm);
        Assert.Equal("abode_azalia", mapBlock.Map.CurrentNodeId);
        Assert.Contains(mapBlock.Map.Nodes, static node => node.IsCurrent && node.Label == "Сад Ночных Роз");
        Assert.Contains(mapBlock.Map.Nodes, static node => node.Details.Any(item => item.Key == "Активный Хранитель" && item.Value == "да"));
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "map-summary" &&
            block.Subtitle.Equals("Море Хаоса", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.Empty(result.Blocks.OfType<UiRawJsonBlock>());
    }

    [Fact]
    public async Task ExecuteAsync_Map_InShiningAbode_ReturnsCivicAtlasMap()
    {
        await SeedShiningAbodeFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/map"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var mapBlock = Assert.Single(result.Blocks.OfType<UiMapBlock>());
        Assert.Equal("Сияющая Обитель", mapBlock.Map.Realm);
        Assert.Contains(mapBlock.Map.Nodes, static node => node.Id == "hall_dawn" && node.Label == "Зал Рассвета");
        Assert.Contains(mapBlock.Map.Nodes, static node => node.Id == "faction_lanterns" && node.Details.Any(item => item.Key == "Лидерство"));
        Assert.Contains(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "map-summary" &&
            block.Subtitle.Equals("Сияющая Обитель", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.Empty(result.Blocks.OfType<UiRawJsonBlock>());
    }

    [Theory]
    [InlineData("/локации")]
    [InlineData("/locations")]
    public async Task ExecuteAsync_Locations_IncludesCurrentLinkedAndAcceptedCanonicalLocations(string command)
    {
        await SeedMortalFilesAsync();
        var square = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_square",
            "Старая площадь",
            "visited",
            x: 4,
            y: 7);
        square["description"] = "Площадь с тёмным фонтаном.";
        var gate = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_gate",
            "Северные ворота",
            "discovered",
            x: 4,
            y: 8);
        var tower = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_tower",
            "Пепельная башня",
            "discovered",
            x: 5,
            y: 8);
        tower["description"] = "Башня над дорогой.";
        var bridge = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_bridge",
            "Сломанный мост",
            "discovered",
            x: 3,
            y: 8);
        bridge["lastEventsDescription"] = "Мост осел после ночного дождя.";
        var link = MortalLocationTestFixture.CreateCanonicalLink("loc_square", "loc_gate");
        link["directionLabel"] = "север";
        await WriteCanonicalBrowserMortalLocationStateAsync(
            [square, gate, tower, bridge],
            MortalLocationTestFixture.CreateCurrentProjection(square),
            [square, gate, tower, bridge],
            [link]);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiMapBlock);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Старая площадь", text, StringComparison.Ordinal);
        Assert.Contains("Северные ворота", text, StringComparison.Ordinal);
        Assert.Contains("север", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Пепельная башня", text, StringComparison.Ordinal);
        Assert.Contains("Сломанный мост", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Locations_FailsClosedOnLegacyWorldMapWrappers()
    {
        await SeedMortalFilesAsync();
        var current = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_canonical_only",
            "Канонический двор",
            "visited",
            x: 2,
            y: 2);
        await WriteCanonicalBrowserMortalLocationStateAsync(
            [current],
            MortalLocationTestFixture.CreateCurrentProjection(current),
            [current]);
        var map = MortalLocationTestFixture.CreateWorldMap(current);
        map["newLocations"] = new JsonArray(new JsonObject
        {
            ["locationId"] = "loc_legacy_gate",
            ["name"] = "НЕ ПОКАЗЫВАТЬ LEGACY ВОРОТА"
        });
        map["worldMapUpdates"] = new JsonObject
        {
            ["newLocations"] = new JsonArray(new JsonObject
            {
                ["locationId"] = "loc_legacy_garden",
                ["name"] = "НЕ ПОКАЗЫВАТЬ LEGACY САД"
            }),
            ["locationUpdates"] = new JsonArray(new JsonObject
            {
                ["locationId"] = "loc_legacy_market",
                ["name"] = "НЕ ПОКАЗЫВАТЬ LEGACY РЫНОК"
            })
        };
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            map.ToJsonString());

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/locations"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Локации пока не обнаружены", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Канонический двор", text, StringComparison.Ordinal);
        Assert.DoesNotContain("НЕ ПОКАЗЫВАТЬ LEGACY", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/chaos_sea")]
    [InlineData("/guardians")]
    [InlineData("/abode_power")]
    [InlineData("/guardian_projects")]
    [InlineData("/guardian_politics")]
    [InlineData("/abodes")]
    public async Task ExecuteAsync_MigratedChaosSeaReadOnlyCommands_ReturnCompletedDtos(string command)
    {
        await PrepareMigratedChaosSeaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.NotEmpty(result.Blocks);
        Assert.DoesNotContain(
            result.Blocks,
            static block => block is UiMessageBlock message &&
                            message.Title.Contains("пока недоступна", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/guardians", "guardians-detail-guardian_azalia", "/guardians хранитель guardian_azalia", "Азалия")]
    [InlineData("/abodes", "abodes-detail-abode_azalia", "/abodes обитель abode_azalia", "Сад Ночных Роз")]
    [InlineData("/abode_power", "abode-power-detail-power_rose_offering", "/abode_power запись power_rose_offering", "Дар роз")]
    [InlineData("/guardian_projects", "guardian-projects-detail-guardian_azalia-project_rose_gate", "/guardian_projects проект guardian_azalia::project_rose_gate", "Врата роз")]
    public async Task ExecuteAsync_ChaosSeaGuardianAbodeOverviews_ExposeReadOnlyDetailActions(
        string command,
        string expectedActionId,
        string expectedDetailCommand,
        string expectedLabelText)
    {
        await PrepareRichChaosSeaGuardianFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(expectedLabelText, text, StringComparison.OrdinalIgnoreCase);

        var action = Assert.Single(result.Actions, action => action.Id == expectedActionId);
        Assert.Equal(expectedDetailCommand, action.Command);
        Assert.Contains("Подробно", action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedLabelText, action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, action.Style);
        Assert.False(action.RequiresConfirmation);
    }

    [Fact]
    public async Task ExecuteAsync_ChaosSeaGuardiansOverview_RendersDossierCardsWithoutLegacyTables()
    {
        await SeedRichChaosSeaGuardianAbodeDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/guardians"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(
            result.Blocks.SelectMany(EnumerateEntityDossiers),
            static dossier => dossier.EntityType == "panel");

        var dossier = Assert.Single(
            result.Blocks.SelectMany(EnumerateEntityDossiers),
            static candidate => candidate.EntityType == "chaos-sea-guardians");
        var guardians = Assert.Single(dossier.Sections, static section => section.Id == "chaos-sea-guardians-list");

        Assert.Equal("Хранители", dossier.Title);
        Assert.Contains("Азалия", CollectBlockText([dossier]), StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(guardians.Cards);
        Assert.All(guardians.Cards, static card => Assert.NotNull(card.PrimaryAction));
    }

    [Theory]
    [InlineData("/guardians хранитель guardian_azalia", "Хранитель: Азалия", "Покровительница перекрёстков", "Серет")]
    [InlineData("/abodes обитель abode_azalia", "Обитель: Сад Ночных Роз", "Оранжерея памяти", "Зал Серета")]
    [InlineData("/abode_power запись power_rose_offering", "Сила Обители: Дар роз", "Чернильные перья укрепили сад", "Врата Серета")]
    [InlineData("/guardian_projects проект guardian_azalia::project_rose_gate", "Проект Хранителя: Врата роз", "Закрепить проход между розами", "Серет")]
    public async Task ExecuteAsync_ChaosSeaGuardianAbodeDetails_RenderFocusedPlayerFacingDetailWithoutRawJson(
        string command,
        string expectedTitle,
        string expectedDetail,
        string excludedOtherEntity)
    {
        await PrepareRichChaosSeaGuardianFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains(expectedTitle, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedDetail, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(excludedOtherEntity, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/guardians хранитель missing_guardian")]
    [InlineData("/abodes обитель missing_abode")]
    [InlineData("/abode_power запись missing_power_entry")]
    [InlineData("/guardian_projects проект guardian_azalia::missing_project")]
    public async Task ExecuteAsync_ChaosSeaGuardianAbodeDetails_UnknownIdsReturnPlayerFacingUnavailableText(string command)
    {
        await SeedRichChaosSeaGuardianAbodeDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains("не удалось открыть", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Gacha_ReturnsDirectChaosSeaPrompt()
    {
        await SeedChaosSeaFilesAsync();
        await SeedPendingGachaBaseAsync("Rare", 72, [18, 18, 18, 18]);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/gacha",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal("/gacha", result.Command);
        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.True(result.InteractiveSession.RequiresLocalUiLock);
        var bannerPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "gacha_banner"));
        var banner = Assert.Single(bannerPrompt.Options);
        Assert.Equal("direct_chaos_sea", banner.Value);
        Assert.Contains("Прямой призыв", banner.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1-18", banner.Description, StringComparison.Ordinal);
        Assert.Contains(result.Prompts, prompt => prompt.Id == "feather_cost");
        Assert.IsType<UiConfirmationPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "confirm_gacha_pull"));
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Пороги", text, StringComparison.Ordinal);
        Assert.Contains("4-48 обычная", text, StringComparison.Ordinal);
        Assert.Contains("80 легендарная", text, StringComparison.Ordinal);
        Assert.Contains("Базовая редкость", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("редкая", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Common", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Uncommon", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Rare", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Epic", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Legendary", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Guardian-mediated", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Gacha_WhenPendingDiceMissing_CreatesAuthoritativeBaseForPromptAndSubmit()
    {
        await SeedChaosSeaFilesAsync();

        Assert.False(_fs.FileExists(PendingTurnStateService.PendingDiceStatePath));

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/gacha",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.True(_fs.FileExists(PendingTurnStateService.PendingDiceStatePath));
        var pending = JsonNode.Parse((await _fs.ReadFileAsync(PendingTurnStateService.PendingDiceStatePath))!)!.AsObject();
        var gachaBase = pending["gachaBaseResult"]!.AsObject();
        var expectedRarity = gachaBase["baseRarity"]!.GetValue<string>();
        var expectedScore = gachaBase["baseScore"]!.GetValue<int>();
        var expectedDice = gachaBase["diceUsed"]!.AsArray()
            .Select(node => node!.GetValue<int>())
            .ToArray();
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(FormatRarityForPlayerTest(expectedRarity), text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedScore.ToString(), text, StringComparison.Ordinal);
        Assert.Contains("[" + string.Join(", ", expectedDice) + "]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("не подготовлен", text, StringComparison.OrdinalIgnoreCase);

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            result.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["gacha_banner"] = JsonValue.Create("direct_chaos_sea"),
                ["feather_cost"] = JsonValue.Create(5),
                ["confirm_gacha_pull"] = JsonValue.Create(true)
            },
            OwnerId: "browser-test"));

        var payload = Assert.IsType<UiRawJsonBlock>(completed.Blocks.Last()).Json!.AsObject();
        Assert.Equal(expectedRarity, payload["gachaBaseResult"]!["baseRarity"]!.GetValue<string>());
        Assert.Equal(expectedScore, payload["gachaBaseResult"]!["baseScore"]!.GetValue<int>());
        Assert.Equal(expectedDice, payload["gachaBaseResult"]!["diceUsed"]!.AsArray()
            .Select(node => node!.GetValue<int>())
            .ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_Gacha_InShiningAbode_DoesNotOpenDirectChaosSeaPrompt()
    {
        await SeedShiningAbodeFilesAsync();
        await SeedPendingGachaBaseAsync("Rare", 72, [18, 18, 18, 18]);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/gacha",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Empty(result.Prompts);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Море Хаоса", text, StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task ExecuteAsync_Gacha_WithUnsupportedArgument_DoesNotOpenPrompt()
    {
        await SeedChaosSeaFilesAsync();
        await SeedPendingGachaBaseAsync("Rare", 72, [18, 18, 18, 18]);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/gacha guardian_pull",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Empty(result.Prompts);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("аргумент", text, StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_GachaDirectPull_QueuesGmTurnRequestWithSnapshot()
    {
        await SeedChaosSeaFilesAsync();
        await SeedPendingGachaBaseAsync("Uncommon", 55, [12, 13, 14, 16]);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/gacha",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["gacha_banner"] = JsonValue.Create("direct_chaos_sea"),
                ["feather_cost"] = JsonValue.Create(5),
                ["confirm_gacha_pull"] = JsonValue.Create(true)
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        Assert.Equal(13, soul["inkFeathers"]!["current"]!.GetValue<int>());
        var payload = Assert.IsType<UiRawJsonBlock>(completed.Blocks.Last()).Json!.AsObject();
        Assert.Equal("CHAOS_SEA_DIRECT_GACHA", payload["playerActionTag"]!.GetValue<string>());
        Assert.Equal("Uncommon", payload["gachaBaseResult"]!["baseRarity"]!.GetValue<string>());
        Assert.Contains("5 Чернильных Перьев", payload["gmAction"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("ровно одну новую Soul Relic", payload["gmAction"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(_fs.FileExists(BrowserPendingTurnInspector.TurnRequestPath));
        Assert.True(_fs.FileExists(BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath));
        Assert.True(_fs.FileExists(PendingTurnSnapshotAuthority.AuthorityPath));
        Assert.True(PendingTurnSnapshotAuthority.TryReadDetachedAuthorityPayload(
            await _fs.ReadFileAsync(PendingTurnSnapshotAuthority.AuthorityPath),
            out var authorityPayload));
        Assert.NotNull(authorityPayload);
        var pendingTurn = BrowserPendingTurnInspector.Build(_fs);
        Assert.True(pendingTurn.HasActiveGmTurn);
        Assert.Contains(
            pendingTurn.Artifacts,
            artifact => string.Equals(artifact.Path, BrowserPendingTurnInspector.TurnRequestPath, StringComparison.OrdinalIgnoreCase) &&
                        artifact.Exists);

        var request = JsonNode.Parse((await _fs.ReadFileAsync(BrowserPendingTurnInspector.TurnRequestPath))!)!.AsObject();
        Assert.Contains("[CHAOS_SEA_DIRECT_GACHA]", request["playerAction"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("5 Чернильных Перьев", request["playerAction"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("Uncommon", request["gachaBaseResult"]!["baseRarity"]!.GetValue<string>());
        Assert.Equal(55, request["gachaBaseResult"]!["baseScore"]!.GetValue<int>());
        Assert.Equal(
            Enumerable.Range(1, 20),
            request["preGeneratedDices1d20"]!.AsArray().Select(node => node!.GetValue<int>()));

        var manifest = JsonNode.Parse((await _fs.ReadFileAsync(BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath))!)!.AsObject();
        Assert.Equal(request["requestId"]!.GetValue<string>(), manifest["requestId"]!.GetValue<string>());
        Assert.Equal(request["playerAction"]!.GetValue<string>(), manifest["playerAction"]!.GetValue<string>());
        Assert.Equal("Uncommon", manifest["gachaBaseResult"]!["baseRarity"]!.GetValue<string>());
        Assert.Equal(request["requestId"]!.GetValue<string>(), authorityPayload!.RequestId);
        Assert.Equal(request["turnNumber"]!.GetValue<int>(), authorityPayload.TurnNumber);
        var files = Assert.IsType<JsonObject>(manifest["files"]);
        Assert.True(files.ContainsKey("game_state/meta/soul_state.json"));
        var snapshotSoulPath = files["game_state/meta/soul_state.json"]!.GetValue<string>();
        var snapshotSoul = JsonNode.Parse((await _fs.ReadFileAsync(snapshotSoulPath))!)!.AsObject();
        Assert.Equal(13, snapshotSoul["inkFeathers"]!["current"]!.GetValue<int>());
        var rollbackBackups = Assert.IsType<JsonObject>(manifest["rollbackBackups"]);
        Assert.True(rollbackBackups.ContainsKey("game_state/meta/soul_state.json"));
        var rollbackSoul = JsonNode.Parse((await _fs.ReadFileAsync(rollbackBackups["game_state/meta/soul_state.json"]!.GetValue<string>()))!)!.AsObject();
        Assert.Equal(18, rollbackSoul["inkFeathers"]!["current"]!.GetValue<int>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task ExecuteAsync_GuardianPolitics_HidesSecretLinks()
    {
        await SeedChaosSeaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/guardian_politics"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Азалия ищет союзников", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Скрытых записей", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Скрытая зависимость", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("system_saref_shadow", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_GuardianPolitics_DefaultProjectionOmitsGmOnlyRawState()
    {
        await SeedChaosSeaFilesAsync();
        await WriteGuardianPoliticsRawLeakFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/guardian_politics"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Contains("Азалия ищет союзников", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Публичный архивный пакт", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Скрытых записей", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hiddenRelations", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secretProjects", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internalMotivations", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("system_saref_shadow", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("system_invisible_false_guardian", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret_project_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("is_player_visible_false_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal_motivation_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_GuardianPolitics_DebugProjectionIncludesFullRawState()
    {
        await SeedChaosSeaFilesAsync();
        await WriteGuardianPoliticsRawLeakFixtureAsync();

        var advancedRequest = JsonSerializer.Deserialize<ExplorerWebCommandRequest>(
            """{"command":"/guardian_politics","advancedEnabled":true}""",
            JsonOptions)!;
        var advancedResult = await _service.ExecuteAsync(advancedRequest);

        AssertContainsGuardianPoliticsRawState(advancedResult);

        _stateManager.Settings.ShowGmThoughts = true;
        var gmThoughtsResult = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/guardian_politics"));

        AssertContainsGuardianPoliticsRawState(gmThoughtsResult);
    }

    [Fact]
    public async Task ExecuteAsync_ShiningPolitics_DefaultProjectionShowsFactionChroniclesWithoutRawMemory()
    {
        await SeedShiningAbodeFilesAsync();
        await WriteShiningFactionPoliticalMemoryRawLeakFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/shining_politics"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Contains("Хроника фракций", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Фонари Рассвета", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Открыли безопасный проход", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Влияние фракций", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Серебряный Зал", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ресурсы фракций", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Искры Света", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("19", text, StringComparison.Ordinal);
        Assert.DoesNotContain("strategicMemory", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("resourceLedger", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_chronicle_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_strategy_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_ledger_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ShiningPolitics_DebugProjectionIncludesFullFactionMemory()
    {
        await SeedShiningAbodeFilesAsync();
        await WriteShiningFactionPoliticalMemoryRawLeakFixtureAsync();

        var advancedRequest = JsonSerializer.Deserialize<ExplorerWebCommandRequest>(
            """{"command":"/shining_politics","advancedEnabled":true}""",
            JsonOptions)!;
        var advancedResult = await _service.ExecuteAsync(advancedRequest);

        AssertContainsShiningPoliticsRawState(advancedResult);

        _stateManager.Settings.ShowGmThoughts = true;
        var gmThoughtsResult = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/shining_politics"));

        AssertContainsShiningPoliticsRawState(gmThoughtsResult);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfiles_DefaultProjectionOmitsHiddenProfileRawState()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeProfilesRawLeakFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Contains("Хранитель Открытой Розы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Открытая карта клятвы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Открытая цель: защитить игрока", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_actor_motivation_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_activity_motivation_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_fate_card_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_card_story_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_concealed_truth_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_mask_directive_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_saref_agent_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private_afterlife_materialization_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("materializationId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("materializedAtTurn", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("empty_by_design", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfiles_OverviewRendersDossierCardsWithoutLegacyTables()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeProfilesRawLeakFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));

        var dossier = Assert.Single(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static candidate => candidate.EntityType == "afterlife-profiles");
        var profileSection = Assert.Single(
            dossier.Sections,
            static section => section.Id == "visible-afterlife-profiles");
        var profileCard = Assert.Single(profileSection.Cards);

        Assert.Equal("Профили сущностей посмертия", dossier.Title);
        Assert.Contains("Хранитель Открытой Розы", profileCard.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Открытая карта клятвы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Открытая цель: защитить игрока", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_actor_motivation_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/afterlife_chronicles")]
    [InlineData("/хроники_посмертия")]
    public async Task ExecuteAsync_AfterlifeChronicles_DefaultProjectionShowsPlayerSafeChronology(string command)
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeChroniclesRawLeakFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Contains("Хроники посмертия", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Зал зеркальной клятвы", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("guardian_scene:guardian_mirror", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Игрок впервые вошёл в зал отражений", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Игрок услышал зов зеркал", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Зал отражений запомнил голос игрока", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Понять, почему зеркала зовут игрока", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Хранитель Зеркал", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Игрок", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_chronicle_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret_participant_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal_scope_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("moon_visible_to_player_false_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("quiet_deal_boolean_secret_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("silent_boolean_hidden_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("closed_gm_only_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gmThoughtsSummary", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lastInvalidChronicleUpdate", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeChronicles_OverviewRendersDossierCardsWithoutLegacyTables()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeChroniclesRawLeakFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_chronicles"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));

        var dossier = Assert.Single(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static candidate => candidate.EntityType == "afterlife-chronicles");
        var chronicleSection = Assert.Single(
            dossier.Sections,
            static section => section.Id == "visible-afterlife-chronicles");
        var chronicleCard = Assert.Single(chronicleSection.Cards);

        Assert.Equal("Хроники посмертия", dossier.Title);
        Assert.Contains("Зал зеркальной клятвы", chronicleCard.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Игрок впервые вошёл в зал отражений", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Зал отражений запомнил голос игрока", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Понять, почему зеркала зовут игрока", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_chronicle_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeChronicles_MissingStateReturnsFriendlyEmptyState()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_chronicles"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Contains("Хроники пока пусты", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Когда ГМ запишет события посмертия, они появятся здесь", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeChronicles_AdvancedProjectionIncludesFullRawState()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeChroniclesRawLeakFixtureAsync();

        var advancedRequest = JsonSerializer.Deserialize<ExplorerWebCommandRequest>(
            """{"command":"/afterlife_chronicles","advancedEnabled":true}""",
            JsonOptions)!;
        var advancedResult = await _service.ExecuteAsync(advancedRequest);

        AssertContainsAfterlifeChroniclesRawState(advancedResult);

        _stateManager.Settings.ShowGmThoughts = true;
        var gmThoughtsResult = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_chronicles"));

        AssertContainsAfterlifeChroniclesRawState(gmThoughtsResult);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfiles_DefaultProjectionShowsKnownMasksWithoutHiddenInternals()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeProfilesMaskProjectionFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Маски", text, StringComparison.Ordinal);
        Assert.Contains("Хранитель Масок", text, StringComparison.Ordinal);
        Assert.Contains("Активный посланник", text, StringComparison.Ordinal);
        Assert.Contains("дипломат", text, StringComparison.Ordinal);
        Assert.Contains("улыбается и просит доверия", text, StringComparison.Ordinal);
        Assert.Contains("Раскрытая вывеска", text, StringComparison.Ordinal);
        Assert.DoesNotContain("known_revealed_truth_marker", text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("Скрытый запасной образ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden_active_truth_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_active_directive_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_active_condition_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_dormant_truth_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_dormant_directive_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_dormant_condition_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_threat_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_saref_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfiles_DefaultProjectionShowsRelationshipProgressWithoutDebugInternals()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeProfilesRelationshipGatesFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Хранитель Зеркал", text, StringComparison.Ordinal);
        Assert.Contains("Доверие", text, StringComparison.Ordinal);
        Assert.Contains("49", text, StringComparison.Ordinal);
        Assert.Contains("порог 50", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("до порога 1", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Суд зеркальной клятвы", text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("guardian_mirror_player_trust", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("quest_mirror_oath_trial", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("player_soul", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_lock_evidence_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_gate_gm_thoughts_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfiles_AdvancedProjectionShowsAllMaskDiagnostics()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeProfilesMaskProjectionFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/afterlife_profiles",
            AdvancedEnabled: true));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Маски", text, StringComparison.Ordinal);
        Assert.Contains("Активный посланник", text, StringComparison.Ordinal);
        Assert.Contains("Раскрытая вывеска", text, StringComparison.Ordinal);
        Assert.Contains("Скрытый запасной образ", text, StringComparison.Ordinal);
        Assert.Contains("hidden_active_truth_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_active_directive_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_active_condition_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_dormant_truth_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_dormant_directive_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_dormant_condition_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_threat_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_saref_marker", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfiles_AdvancedProjectionShowsRelationshipGateDiagnostics()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeProfilesRelationshipGatesFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/afterlife_profiles",
            AdvancedEnabled: true));
        var relationshipTable = Assert.Single(result.Blocks.OfType<UiTableBlock>(), static table => table.Title == "Отношения");
        var text = CollectBlockText([relationshipTable]);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("guardian_mirror_player_trust", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("player_soul", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("positive_locked", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("threshold=50", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quest_mirror_oath_trial", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_lock_evidence_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_gate_gm_thoughts_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Душа выбирает правду.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfiles_DebugProjectionIncludesFullRawState()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeProfilesRawLeakFixtureAsync();

        var advancedRequest = JsonSerializer.Deserialize<ExplorerWebCommandRequest>(
            """{"command":"/afterlife_profiles","advancedEnabled":true}""",
            JsonOptions)!;
        var advancedResult = await _service.ExecuteAsync(advancedRequest);

        AssertContainsAfterlifeProfilesRawState(advancedResult);

        _stateManager.Settings.ShowGmThoughts = true;
        var gmThoughtsResult = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));

        AssertContainsAfterlifeProfilesRawState(gmThoughtsResult);
    }

    [Theory]
    [InlineData("/shining_abode", CommandExecutionState.Completed)]
    [InlineData("/shining_politics", CommandExecutionState.Completed)]
    [InlineData("/shining_treasury", CommandExecutionState.RequiresInput)]
    [InlineData("/source_of_light", CommandExecutionState.RequiresInput)]
    public async Task ExecuteAsync_MigratedShiningAbodeCommands_ReturnDtos(string command, CommandExecutionState expectedState)
    {
        await PrepareMigratedShiningFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.Equal(expectedState, result.State);
        Assert.NotEmpty(result.Blocks);
        Assert.DoesNotContain(
            result.Blocks,
            static block => block is UiMessageBlock message &&
                            message.Title.Contains("пока недоступна", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/afterlife_profiles", "Профили сущностей посмертия", CommandExecutionState.Completed)]
    [InlineData("/afterlife_inbox", "Уведомления загробья", CommandExecutionState.RequiresInput)]
    [InlineData("/spiritual_conflict", "Духовный конфликт", CommandExecutionState.Completed)]
    [InlineData("/spiritual_combat_log", "Журнал духовного боя", CommandExecutionState.Completed)]
    [InlineData("/spiritual_combat_help", "Духовный бой", CommandExecutionState.Completed)]
    [InlineData("/spiritual_arts", "Духовные искусства", CommandExecutionState.RequiresInput)]
    public async Task ExecuteAsync_MigratedAfterlifeCombatAndEntityCommands_ReturnDtos(
        string command,
        string expectedRussianLabel,
        CommandExecutionState expectedState)
    {
        await PrepareMigratedAfterlifeFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.Equal(expectedState, result.State);
        Assert.NotEmpty(result.Blocks);
        Assert.Contains(expectedRussianLabel, CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            result.Blocks,
            static block => block is UiMessageBlock message &&
                            message.Title.Contains("пока недоступна", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/afterlife_profiles", "afterlife-profile-detail-player_soul", "/afterlife_profiles профиль player_soul", "Test Soul")]
    [InlineData("/afterlife_threats", "afterlife-threat-detail-threat_mirror_hunter", "/afterlife_threats угроза threat_mirror_hunter", "Охотник зеркального долга")]
    [InlineData("/afterlife_chronicles", "afterlife-chronicle-detail-guardian_scene_mirror", "/хроники_посмертия хроника \"Зал зеркальной клятвы\"", "Зал зеркальной клятвы")]
    [InlineData("/spiritual_conflict", "spiritual-conflict-exchange-detail-1", "/spiritual_conflict обмен 1", "Давление")]
    [InlineData("/spiritual_combat_log", "spiritual-combat-log-exchange-detail-1", "/spiritual_combat_log обмен 1", "Давление")]
    [InlineData("/spiritual_arts", "spiritual-art-detail-pressure", "/spiritual_arts искусство pressure", "Давление")]
    [InlineData("/spiritual_arts", "spiritual-special-art-detail-rose_mirror_counter", "/spiritual_arts особое rose_mirror_counter", "Зеркало Ночной Розы")]
    public async Task ExecuteAsync_ChaosSeaAfterlifeOverviews_ExposeIssue1124ReadOnlyDetailActions(
        string command,
        string expectedActionId,
        string expectedDetailCommand,
        string expectedLabelText)
    {
        await PrepareIssue1124AfterlifeFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.NotEqual(CommandExecutionState.Failed, result.State);
        Assert.NotEqual(CommandExecutionState.Blocked, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);

        var action = Assert.Single(result.Actions, candidate => candidate.Id == expectedActionId);
        Assert.Equal(expectedDetailCommand, action.Command);
        Assert.Contains(expectedLabelText, action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, action.Style);
        Assert.False(action.RequiresConfirmation);
        Assert.DoesNotContain("DTO", action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API", action.Label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeThreats_OverviewRendersDossierCardsWithoutLegacyTables()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeThreatsDrilldownFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_threats"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));

        var dossier = Assert.Single(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static candidate => candidate.EntityType == "afterlife-threats");
        var threatSection = Assert.Single(
            dossier.Sections,
            static section => section.Id == "visible-afterlife-threats");
        var threatCard = Assert.Single(threatSection.Cards);

        Assert.Equal("Угрозы посмертия", dossier.Title);
        Assert.Contains("Охотник зеркального долга", threatCard.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Долг следует за душой", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Напряжённость угрозы", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_threat_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/afterlife_profiles профиль player_soul", "Профиль посмертия: Test Soul", "Зеркало Ночной Розы", "hidden_saref_combat_effect_marker")]
    [InlineData("/afterlife_threats угроза threat_mirror_hunter", "Угроза посмертия: Охотник зеркального долга", "Долг следует за душой", "hidden_threat_marker")]
    [InlineData("/afterlife_chronicles хроника guardian_scene_mirror", "Хроника посмертия: Зал зеркальной клятвы", "Игрок впервые вошёл в зал отражений", "hidden_chronicle_marker")]
    [InlineData("/spiritual_conflict обмен exchange_1", "Обмен духовного конфликта: Давление", "Тень Хранителя", "exchange_hidden_roll_source_marker_001")]
    [InlineData("/spiritual_combat_log обмен exchange_1", "Запись духовного боя: Давление", "Тень Хранителя", "exchange_hidden_roll_source_marker_001")]
    [InlineData("/spiritual_combat_log итог conflict_done", "Итог духовного боя: победа", "Чернильные Перья", "hidden_conflict_marker")]
    [InlineData("/spiritual_arts искусство pressure", "Духовное искусство: Давление", "база 3 ОД", "hidden_saref_combat_effect_marker")]
    [InlineData("/spiritual_arts особое rose_mirror_counter", "Особое духовное искусство: Зеркало Ночной Розы", "Контрприём оставляет болезненный образ", "hidden_saref_combat_effect_marker")]
    public async Task ExecuteAsync_ChaosSeaAfterlifeDetails_RenderFocusedIssue1124DetailWithoutRawJson(
        string command,
        string expectedTitle,
        string expectedText,
        string excludedText)
    {
        await PrepareIssue1124AfterlifeFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoAfterlifeReadOnlyDetailTechnicalLeak(result);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(expectedTitle, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedText, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(excludedText, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfileDetail_RendersDossierCardsWithoutLegacyPanels()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles профиль player_soul"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoAfterlifeReadOnlyDetailTechnicalLeak(result);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static dossier => dossier.EntityType == "panel" || dossier.EntityType == "collection");

        var dossier = Assert.Single(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static candidate => candidate.EntityType == "afterlife-profile-detail");

        Assert.Equal("Профиль посмертия: Test Soul", dossier.Title);
        Assert.Contains("Зеркало Ночной Розы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Собрать Средоточие", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeThreatDetail_RendersDossierCardsWithoutLegacyPanels()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeThreatsDrilldownFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_threats угроза threat_mirror_hunter"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoAfterlifeReadOnlyDetailTechnicalLeak(result);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static dossier => dossier.EntityType == "panel" || dossier.EntityType == "collection");

        var dossier = Assert.Single(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static candidate => candidate.EntityType == "afterlife-threat-detail");

        Assert.Equal("Угроза посмертия: Охотник зеркального долга", dossier.Title);
        Assert.Contains("Долг следует за душой", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Напряжённость угрозы", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeChronicleDetail_RendersDossierCardsWithoutLegacyPanelsAndTables()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeChroniclesRawLeakFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_chronicles хроника guardian_scene_mirror"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoAfterlifeReadOnlyDetailTechnicalLeak(result);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static dossier => dossier.EntityType == "panel" || dossier.EntityType == "collection");

        var dossier = Assert.Single(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static candidate => candidate.EntityType == "afterlife-chronicle-detail");

        Assert.Equal("Хроника посмертия: Зал зеркальной клятвы", dossier.Title);
        Assert.Contains("Игрок впервые вошёл в зал отражений", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Понять, почему зеркала зовут игрока", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeInboxDetail_RendersDossierCardsWithoutLegacyPanelsAndTables()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_inbox уведомление notification_1"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoAfterlifeReadOnlyDetailTechnicalLeak(result);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static dossier => dossier.EntityType == "panel" || dossier.EntityType == "collection");

        var dossier = Assert.Single(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static candidate => candidate.EntityType == "afterlife-inbox-detail");

        Assert.Equal("Уведомление загробья", dossier.Title);
        Assert.Contains("Хранитель предлагает тёмный след", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Азалия", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SpiritualExchangeDetail_RendersDossierCardsWithoutLegacyPanels()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_conflict обмен exchange_1"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoAfterlifeReadOnlyDetailTechnicalLeak(result);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static dossier => dossier.EntityType == "panel" || dossier.EntityType == "collection");

        var dossier = Assert.Single(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static candidate => candidate.EntityType == "spiritual-exchange-detail");

        Assert.Equal("Обмен духовного конфликта: Давление", dossier.Title);
        Assert.Contains("Тень Хранителя", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Стоимость души", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SpiritualExchangeDetail_RendersActionPointCostAsSeparateFacts()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_conflict обмен exchange_1"));
        var text = CollectBlockText(result.Blocks);

        var dossier = Assert.Single(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static candidate => candidate.EntityType == "spiritual-exchange-detail");

        Assert.Contains(dossier.Facts, static fact => fact.Label == "Стоимость души" && fact.Value == "2 ОД");
        Assert.Contains(dossier.Facts, static fact => fact.Label == "Стоимость противника" && fact.Value == "1 ОД");
        Assert.Contains(dossier.Facts, static fact => fact.Label == "Стоимость всего" && fact.Value == "3 ОД");
        Assert.DoesNotContain("душа 2 ОД; противник 1 ОД", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SpiritualRecentConflictDetail_RendersDossierCardsWithoutLegacyPanels()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_combat_log итог conflict_done"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoAfterlifeReadOnlyDetailTechnicalLeak(result);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static dossier => dossier.EntityType == "panel" || dossier.EntityType == "collection");

        var dossier = Assert.Single(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static candidate => candidate.EntityType == "spiritual-recent-conflict-detail");

        Assert.Equal("Итог духовного боя: победа", dossier.Title);
        Assert.Contains("Чернильные Перья", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Давление", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/spiritual_arts искусство pressure", "spiritual-art-detail", "Духовное искусство: Давление", "база 3 ОД")]
    [InlineData("/spiritual_arts особое rose_mirror_counter", "spiritual-special-art-detail", "Особое духовное искусство: Зеркало Ночной Розы", "Контрприём оставляет болезненный образ")]
    public async Task ExecuteAsync_SpiritualArtDetails_RenderDossierCardsWithoutLegacyPanels(
        string command,
        string expectedEntityType,
        string expectedTitle,
        string expectedText)
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoAfterlifeReadOnlyDetailTechnicalLeak(result);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static dossier => dossier.EntityType == "panel" || dossier.EntityType == "collection");

        var dossier = Assert.Single(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            candidate => candidate.EntityType == expectedEntityType);

        Assert.Equal(expectedTitle, dossier.Title);
        Assert.Contains(expectedText, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_StandardSpiritualArtDetail_RendersSoulRanksAsSeparateFacts()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_arts искусство pressure"));
        var text = CollectBlockText(result.Blocks);

        var dossier = Assert.Single(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static candidate => candidate.EntityType == "spiritual-art-detail");

        Assert.Contains(dossier.Facts, static fact => fact.Label == "Просветление" && fact.Value == "3 ступень");
        Assert.Contains(dossier.Facts, static fact => fact.Label == "Сияние" && fact.Value == "1 ступень");
        Assert.Contains(dossier.Facts, static fact => fact.Label == "Средоточие Души" && fact.Value == "2 ступень");
        Assert.Contains(dossier.Facts, static fact => fact.Label == "Открытый тир");
        Assert.DoesNotContain("Просветление 3;", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/afterlife_profiles профиль missing_profile")]
    [InlineData("/afterlife_threats угроза missing_threat")]
    [InlineData("/afterlife_chronicles хроника missing_chronicle")]
    [InlineData("/spiritual_conflict обмен missing_exchange")]
    [InlineData("/spiritual_combat_log итог missing_result")]
    [InlineData("/spiritual_arts искусство missing_art")]
    public async Task ExecuteAsync_ChaosSeaAfterlifeDetails_UnknownIssue1124TargetsReturnPlayerFacingUnavailableText(string command)
    {
        await PrepareIssue1124AfterlifeFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoAfterlifeReadOnlyDetailTechnicalLeak(result);
        Assert.Contains("не удалось открыть", CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeSpecialArtSurfaces_ShowCombatEffectWithoutRawContractLeak()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var profilesResult = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));
        var artsResult = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_arts"));
        var combinedText = CollectBlockText(profilesResult.Blocks) + "\n" + CollectBlockText(artsResult.Blocks);
        var payload = SerializeResult(profilesResult) + "\n" + SerializeResult(artsResult);

        Assert.Contains("Зеркало Ночной Розы", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("контрприём превращает входящее давление в брешь", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Когда контрприём отвечает на прямое давление", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Преимущество для ответного давление", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Один раз за конфликт", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("combatEffect", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("auditRequirement", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_saref_combat_effect_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SpiritualConflict_RendersVisibleCombatConditionsAndSuppressesHiddenOnes()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeConflictStateWithCombatConditionsAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_conflict"));

        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Боевые условия", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Разогретая клятва", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("метка", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mark", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("противник", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("guardian_azalia", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("давление", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("break_binding", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Клятва подсвечена", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("ordinary_visible_roll_reason", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mark_oath_flare_001", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("visible_condition_roll_source_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("guard_tempo_window_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exchange_hidden_roll_source_marker_001", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_condition_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_condition_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_summary_legacy_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_summary_legacy_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_audit_legacy_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_audit_legacy_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("concealed_condition_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("concealed_condition_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("spoiler_condition_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("spoiler_condition_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SpiritualConflict_OverviewRendersDossierCardsWithoutLegacyTables()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeConflictStateWithCombatConditionsAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_conflict"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));

        var dossier = Assert.Single(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static candidate => candidate.EntityType == "spiritual-conflict");
        Assert.Contains(dossier.Sections, static section => section.Id == "spiritual-conflict-sides");
        var conditions = Assert.Single(dossier.Sections, static section => section.Id == "spiritual-conflict-conditions");
        Assert.Single(conditions.Cards);

        Assert.Contains("Духовный конфликт", dossier.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Разогретая клятва", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Клятва подсвечена", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Обмены активного конфликта", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mark_oath_flare_001", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_condition_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SpiritualCombatLog_OverviewRendersDossierCardsWithoutLegacyTables()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_combat_log"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));

        var dossier = Assert.Single(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static candidate => candidate.EntityType == "spiritual-combat-log");
        var exchanges = Assert.Single(dossier.Sections, static section => section.Id == "spiritual-combat-log-exchanges");
        var recent = Assert.Single(dossier.Sections, static section => section.Id == "spiritual-combat-log-recent");

        Assert.Contains("Журнал духовного боя", dossier.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Single(exchanges.Cards);
        Assert.Single(recent.Cards);
        Assert.Contains("Давление", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Чернильные Перья", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeInbox_OverviewRendersDossierCardsWithoutLegacyTablesAndKeepsPrompts()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_inbox"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Contains(result.Prompts, static prompt => prompt.Id == "notification_action");
        Assert.Contains(result.Prompts, static prompt => prompt.Id == "notification_id");
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));

        var dossier = Assert.Single(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static candidate => candidate.EntityType == "afterlife-inbox");
        var notifications = Assert.Single(dossier.Sections, static section => section.Id == "afterlife-inbox-notifications");

        Assert.Contains("Уведомления загробья", dossier.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Single(notifications.Cards);
        Assert.Contains("Хранитель предлагает тёмный след", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SpiritualCombatHelp_RendersDossierCardsWithoutLegacyTables()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_combat_help"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));

        var dossier = Assert.Single(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static candidate => candidate.EntityType == "spiritual-combat-help");
        var arts = Assert.Single(dossier.Sections, static section => section.Id == "spiritual-combat-help-arts");

        Assert.Contains("Духовный бой", dossier.Title, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(arts.Cards);
        Assert.Contains("Давление", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Чернильные Перья", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SpiritualArts_OverviewRendersDossierCardsWithoutLegacyPanels()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_arts"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static dossier => dossier.EntityType == "panel" || dossier.EntityType == "collection");

        var overview = Assert.Single(
            result.Blocks.OfType<UiEntityDossierBlock>(),
            static candidate => candidate.EntityType == "spiritual-arts-overview");

        Assert.Equal("Духовные искусства", overview.Title);
        Assert.Contains("Средоточие Души", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Зеркало Ночной Розы", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SpiritualAction_SanitizesActiveCombatConditionRawJson()
    {
        await SeedUniversalMetaFilesAsync();
        await SeedChaosSeaFilesAsync();
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeConflictStateWithCombatConditionsAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_action", AdvancedEnabled: true));

        var payload = SerializeResult(result);
        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Contains("JSON: active afterlife spiritual conflict", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ordinary_visible_roll_reason", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mark_oath_flare_001", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("visible_condition_roll_source_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("guard_tempo_window_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_condition_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_summary_legacy_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_audit_legacy_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("concealed_condition_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("spoiler_condition_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SpiritualAction_DefaultViewRendersPlayerFacingDossierWithoutTechnicalDiagnostics()
    {
        await SeedUniversalMetaFilesAsync();
        await SeedChaosSeaFilesAsync();
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeConflictStateWithCombatConditionsAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_action"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        Assert.DoesNotContain(
            result.Blocks.SelectMany(EnumeratePanels),
            static panel => panel.Title.Contains("DTO", StringComparison.OrdinalIgnoreCase) ||
                            panel.Title.Contains("protocol", StringComparison.OrdinalIgnoreCase));

        var dossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static candidate =>
            candidate.EntityType == "spiritual-action");
        Assert.Equal("Духовное действие", dossier.Title);

        var text = CollectBlockText([dossier]);
        Assert.Contains("Разогретая клятва", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тень Хранителя", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("route tag", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("response surface", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("state file", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyCommand_ReturnsFailedDto()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("   "));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        var message = Assert.IsType<UiMessageBlock>(Assert.Single(result.Blocks));
        Assert.Contains("пустая", message.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    private static string BuildSarefWingsRouteState() => """
    {
      "schemaVersion": 1,
      "revealStage": "name_revealed",
      "guardianQuestlines": [
        {
          "guardianId": "azalia",
          "questStates": [
            { "questOrdinal": 1, "status": "completed", "questId": "azalia_saref_q1" },
            { "questOrdinal": 2, "status": "completed", "questId": "azalia_saref_q2" },
            { "questOrdinal": 3, "status": "completed", "questId": "azalia_saref_q3" },
            { "questOrdinal": 4, "status": "completed", "questId": "azalia_saref_q4" }
          ]
        }
      ],
      "latentTraces": [],
      "sarefRevelations": [
        { "revelationId": "rev_identity", "category": "identity", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 50 },
        { "revelationId": "rev_method", "category": "method", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 51 },
        { "revelationId": "rev_faction", "category": "faction", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 52 },
        { "revelationId": "rev_path", "category": "path", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 53 }
      ],
      "sarefAdvantages": [],
      "sarefAdvantageUses": [],
      "factionLinks": { "visibility": "hidden" },
      "defeatOutcomes": [],
      "endings": [],
      "playerOathState": null,
      "sarefPersonalBond": null
    }
    """;

    private static string BuildSarefActionReadyState() => """
    {
      "schemaVersion": 1,
      "revealStage": "confrontation_available",
      "guardianQuestlines": [],
      "latentTraces": [],
      "sarefRevelations": [
        { "revelationId": "rev_identity", "category": "identity", "revealedAtTurn": 50 },
        { "revelationId": "rev_method", "category": "method", "revealedAtTurn": 51 },
        { "revelationId": "rev_faction", "category": "faction", "revealedAtTurn": 52 },
        { "revelationId": "rev_path", "category": "path", "revealedAtTurn": 53 }
      ],
      "sarefAdvantages": [
        {
          "advantageId": "adv_lucian_oath_cut",
          "displayName": "Лунный Разрез Клятвы",
          "state": "available",
          "applicableScenes": [ "oath_break", "saref_confrontation" ],
          "summary": "Можно рассечь одну ложную печать клятвы."
        }
      ],
      "sarefAdvantageUses": [],
      "factionLinks": { "visibility": "revealed", "wingsFactionId": "wings_of_angels" },
      "wingsInfiltration": { "status": "revealed", "requestId": "saref_wings_infiltration:80", "resolvedAtTurn": 81 },
      "finalConfrontation": { "status": "active", "sceneType": "saref_confrontation" },
      "postStoryAgenda": {
        "state": "oathbound_to_saref",
        "currentObjective": "Подчинить последнюю независимую фракцию.",
        "assignments": [],
        "dominationScene": null
      },
      "playerOathState": { "state": "oathbound", "oathId": "saref_oath_001" },
      "defeatOutcomes": [],
      "endings": [],
      "sarefPersonalBond": null
    }
    """;

    private async Task SeedUniversalMetaFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Test Soul",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 3,
          "inkFeathers": { "current": 12, "total": 34 },
          "enlightenment": { "currentTier": "Искра", "experience": 42 },
          "livesHistory": [
            { "incarnation": 1, "summary": "Первая жизнь" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("lore/codex_entries.json", """
        {
          "entries": [
            { "title": "Первый знак", "content": "Тестовая запись кодекса", "sourceFile": "lore/current_world/history.json" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "Тестовые мысли ГМ"
        }
        """);

        await _fs.WriteFileAtomicAsync("stories/chaos_sea.jsonl", """
        {"turn":1,"timestamp":"2026-05-20T00:00:00Z","realm":"Chaos Sea","player":"test","narrative":"story"}

        """);
    }

    private async Task SeedMortalActionPromptDataAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "npcs": [
            {
              "npcId": "npc_companion_1",
              "name": "Мирра",
              "progressionType": "Companion",
              "playerCompanionDirective": "Держаться рядом с раненым учеником."
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/factions/faction_core.json", """
        {
          "factions": [
            {
              "factionId": "faction_player_1",
              "name": "Серые знамена",
              "isPlayerFaction": true,
              "playerStrategyDirective": "Укрепить северные заставы."
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/inventory/recipes.json", """
        {
          "recipes": [
            {
              "recipeId": "healing_salve",
              "recipeName": "Лечебная мазь",
              "craftedItemName": "Припарка",
              "recipeRank": "ученик"
            }
          ]
        }
        """);
    }

    private async Task SeedWebSystemGuardianPresetAsync(string presetId, string displayName, string abodeName)
    {
        var presetDir = Path.Combine(
            _fs.BasePath,
            SystemGuardianLibraryService.RootDirectoryName,
            SystemGuardianLibraryService.BuiltInDirectoryName,
            presetId);
        Directory.CreateDirectory(presetDir);

        await File.WriteAllTextAsync(Path.Combine(presetDir, "manifest.json"), $$"""
        {
          "presetId": "{{presetId}}",
          "displayName": "{{displayName}}",
          "summary": "Тестовый системный хранитель для browser tests.",
          "alwaysAvailable": true,
          "category": "system_guardian",
          "identity": {
            "domain": "Social",
            "archetype": "Test Archetype",
            "tone": "Measured",
            "coreValues": ["ценность 1", "ценность 2"]
          },
          "abode": {
            "name": "{{abodeName}}",
            "theme": "тестовая обитель"
          },
          "authoring": {
            "author": "tests",
            "version": "1.0"
          }
        }
        """);

        await File.WriteAllTextAsync(
            Path.Combine(presetDir, "dossier.md"),
            $"# {displayName}\n\nТестовое досье для браузерного вывода.");
    }

    private void WriteSessionImage(string relativePath)
    {
        var fullPath = _fs.ResolvePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, [137, 80, 78, 71, 13, 10, 26, 10]);
    }

    private async Task SeedJournalOnlyNpcFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_journals.json", """
        {
          "npcJournals": [
            {
              "npcId": "npc_marten_roche_valmont_valet",
              "npcName": "Мартен Рош",
              "lastJournalNote": "Мартен знает имя свидетеля, но боится говорить без защиты.",
              "journalEntries": [
                {
                  "event": "Утренний допрос",
                  "description": "Он признался, что ночью слышал шаги у фамильной библиотеки.",
                  "relationshipChange": "+1"
                },
                {
                  "event": "Имя свидетеля",
                  "description": "Он отказался назвать кухонного мальчишку без защиты.",
                  "relationshipChange": "0"
                }
              ]
            }
          ]
        }
        """);
    }

    private async Task SeedRichNpcDrilldownFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_serafina",
              "name": "Серафина",
              "shortDescription": "Архивариус северных ворот.",
              "currentLocation": "Северные ворота",
              "relationshipLevel": 42,
              "personalQuests": [
                {
                  "questId": "quest_serafina_letter",
                  "questName": "Сделка на рассвете",
                  "status": "Active",
                  "description": "Серафина просит передать письмо без лишних свидетелей.",
                  "objectives": [
                    { "description": "Доставить письмо в архив", "status": "Active" }
                  ],
                  "rewards": "Получить ключ от боковой двери",
                  "failureConsequences": "Провал закроет путь через северные ворота"
                },
                {
                  "questId": "quest_serafina_debt",
                  "questName": "Отложенный долг",
                  "status": "Pending",
                  "description": "Серафина обещает позже попросить об услуге."
                },
                {
                  "questName": "Просьба без метки",
                  "status": "Open",
                  "description": "Серафина просит об услуге без отдельного идентификатора."
                }
              ],
              "currentActivity": {
                "activityName": "Проверка печатей",
                "description": "Проверяет печати у северных ворот",
                "timeSpentMinutes": 30,
                "totalTimeCostMinutes": 60
              },
              "worldview": "Lawful Neutral",
              "personalityArchetype": "Строгая наставница",
              "personalityTraits": [
                {
                  "traitName": "Дисциплина",
                  "description": "Требует точности в словах и ритуалах.",
                  "value": 9,
                  "valueDescription": "Ошибки в магии не любят свидетелей."
                }
              ],
              "passiveSkills": [
                {
                  "skillName": "Арканическая диагностика",
                  "description": "Читает слабый резонанс архивных печатей.",
                  "type": "KnowledgeBased",
                  "rarity": "Rare"
                }
              ],
              "fateCards": [
                {
                  "name": "Холодная милость наставника",
                  "summary": "Селена способна закрыть опасную ошибку ученика, но попросит за это трудную правду.",
                  "rarity": "Rare"
                }
              ],
              "materialization": {
                "schemaVersion": 1,
                "materializationId": "private_mortal_materialization_marker",
                "actorType": "mortal_npc",
                "actorId": "npc_serafina",
                "materializedAtTurn": 3,
                "state": "complete",
                "capabilities": {
                  "canFight": false,
                  "canTeach": false,
                  "canTrade": false,
                  "ownsItems": false
                },
                "sections": {
                  "skills": { "state": "populated" },
                  "inventory": {
                    "state": "empty_by_design",
                    "reason": "private_mortal_empty_reason_marker"
                  },
                  "fateCards": { "state": "populated" },
                  "personalQuests": { "state": "populated" },
                  "relationships": { "state": "populated" }
                }
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_relationships.json", """
        {
          "entries": [
            {
              "NPCId": "npc_serafina",
              "relationshipStatus": "осторожное доверие",
              "relationshipLevel": 42,
              "summary": "Готова помочь, если письмо не попадёт к дозору."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_personality.json", """
        {
          "NPCPersonalityTraitChanges": [
            {
              "NPCId": "npc_serafina",
              "traitName": "Дисциплина наставницы",
              "description": "Не допускает приблизительных формулировок в опасной магии.",
              "value": 9,
              "valueDescription": "Сначала точность, потом сила.",
              "temperament": "Сдержанный",
              "morality": "Lawful Neutral"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_masks.json", """
        {
          "NPCMaskAdds": [
            {
              "NPCId": "npc_serafina",
              "maskId": "mask_serafina_mentor",
              "maskName": "Наставница академии",
              "description": "Говорит строго и держит ученика на расстоянии.",
              "behavior": "Задаёт проверочные вопросы и смотрит на реакцию перчатки.",
              "isActive": true,
              "trigger": "Проявляется при разговоре об академии.",
              "mask": {
                "maskId": "mask_serafina_mentor",
                "maskName": "Наставница академии",
                "personalityArchetype": "Строгая преподавательница",
                "attitude": "требовательная забота",
                "behavioralDirectives": "Сдерживать эмоции и проверять каждое утверждение игрока."
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_skills.json", """
        {
          "entries": [
            {
              "NPCId": "npc_serafina",
              "skillName": "Проверка печатей",
              "description": "Замечает подделки на архивных печатях.",
              "type": "KnowledgeBased",
              "rarity": "Rare",
              "rank": 3
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_journals.json", """
        {
          "NPCJournals": [
            {
              "NPCId": "npc_serafina",
              "NPCName": "Серафина",
              "lastJournalNote": "Сомневается, стоит ли доверять письму.",
              "journalEntries": [
                {
                  "event": "Первый разговор",
                  "description": "Заметила осторожность игрока.",
                  "relationshipChange": "+2"
                },
                {
                  "event": "Письмо найдено",
                  "description": "Сомневается, стоит ли доверять письму.",
                  "relationshipChange": "+1"
                }
              ]
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_activities.json", """
        {
          "entries": [
            {
              "NPCId": "npc_serafina",
              "activityUpdate": {
                "activityName": "Проверка печатей",
                "description": "Проверяет печати у северных ворот",
                "activeState": "active",
                "timeSpentMinutes": 30,
                "totalTimeCostMinutes": 60
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_memory.json", """
        {
          "entries": [
            {
              "NPCId": "npc_serafina",
              "name": "Неудачный урок резонанса",
              "summary": "Однажды Селена перепутала спокойный след с опасным откликом.",
              "rarity": "Rare"
            }
          ]
        }
        """);
    }

    private async Task SeedNpcFateAndMerchantFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_serafina",
              "name": "Серафина",
              "shortDescription": "Архивариус северных ворот.",
              "fateCards": [
                {
                  "cardId": "selene_card_cold_mercy",
                  "name": "Холодная милость наставника",
                  "description": "Селена способна закрыть опасную ошибку ученика, но попросит за это трудную правду.",
                  "isUnlocked": false,
                  "unlockConditions": {
                    "requiredRelationshipLevel": 200,
                    "plotConditionDescription": "Доверить Селене полный текст загадочного письма.",
                    "conjunction": "AND"
                  },
                  "rewards": {
                    "description": "Открывает консультацию по опасным магическим следам."
                  }
                }
              ]
            },
            {
              "npcId": "npc_artifact_trader_voron",
              "name": "Ворон Рилль",
              "shortDescription": "Торговец артефактами, явившийся слишком быстро после странного письма.",
              "role": "Торговец артефактами",
              "class": "Торговец",
              "status": "В сцене, торгует",
              "tradeState": {
                "canTrade": true,
                "merchantProfile": "ArtifactsAndCurios",
                "tradeBlockedReason": ""
              },
              "tradeInventory": {
                "tradeCycleId": "world_trade_0",
                "items": [
                  {
                    "slotId": "voron_slot_001",
                    "price": 22,
                    "soldOut": false,
                    "itemData": {
                      "name": "Схема старых служебных дверей",
                      "quality": "Common",
                      "type": "Document"
                    }
                  }
                ]
              }
            }
          ]
        }
        """);
    }

    private async Task SeedNpcInternalLeakDrilldownFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_serafina",
              "name": "Серафина",
              "personalQuests": [
                {
                  "questName": "Сделка на рассвете",
                  "status": "Active",
                  "description": "Серафина просит передать письмо без лишних свидетелей.",
                  "rewards": {
                    "displayName": "Получить ключ от боковой двери",
                    "rewardId": "reward_serafina_secret",
                    "image_prompt": "image_prompt should stay hidden",
                    "prompt": "prompt-for-gm reward note",
                    "debugNotes": "debug reward trace",
                    "internalMemo": "internal reward memo",
                    "dtoType": "NpcRewardDto",
                    "apiPath": "/api/npcs/rewards"
                  }
                }
              ],
              "masks": [
                {
                  "maskId": "mask_serafina_false_face",
                  "name": "Образ доверенного архивариуса",
                  "concealedTruth": "Скрывает связь с дозором"
                }
              ],
              "customStates": [
                {
                  "name": "Клятва у печати",
                  "description": "Держит слово у северных ворот",
                  "debugNotes": "debug custom state"
                }
              ]
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_memory.json", """
        {
          "entries": [
            {
              "NPCId": "npc_serafina",
              "memoryId": "memory_serafina_debug",
              "summary": "Видимый след в памяти",
              "sourcePath": "game_state/npcs/npc_memory.json",
              "prompt": "prompt-for-gm memory note",
              "debugNotes": "debug memory trace",
              "internalMemo": "internal memory memo",
              "dtoType": "NpcMemoryDto",
              "apiPath": "/api/npcs/memory"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_masks.json", """
        {
          "entries": [
            {
              "NPCId": "npc_serafina",
              "maskId": "mask_serafina_false_face",
              "name": "Образ доверенного архивариуса",
              "concealedTruth": "Скрывает связь с дозором"
            }
          ]
        }
        """);
    }

    private async Task SeedNpcMechanicsDrilldownFilesAsync()
    {
        var key = CreateAcceptedUiItem(
            "itm_serafina_archive_key",
            "Архивный ключ",
            item => item["description"] = "Открывает боковую дверь.");
        await _fs.WriteFileAtomicAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(
                    new JsonObject
                    {
                        ["npcId"] = "npc_serafina",
                        ["name"] = "Серафина",
                        ["inventory"] = new JsonArray(key),
                        ["equippedItems"] = new JsonObject()
                    })
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task SeedMortalFilesAsync()
    {
        var blade = CreateAcceptedUiItem(
            "blade_1",
            "Старый клинок",
            item => item["count"] = 1);
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/items.json",
            new JsonObject
            {
                ["items"] = new JsonArray(blade),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "npcs": [
            { "npcId": "npc_1", "name": "Мирра", "status": "alive" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/quests/regular_quests.json", """
        {
          "activeQuests": [
            { "questId": "quest_1", "title": "Найти след", "status": "active" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationName": "Старый тракт",
          "region": "Северный край",
          "description": "Дорога под серым небом."
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_map.json", """
        {
          "newLocations": [
            { "locationId": "old_road", "locationName": "Старый тракт" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/combat/enemies.json", """
        {
          "enemies": [
            { "enemyId": "wolf_1", "name": "Волк", "status": "hostile" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_time.json", """
        { "currentTime": "ночь" }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/weather.json", """
        { "currentState": "дождь" }
        """);

        await _fs.WriteFileAtomicAsync("game_state/inventory/item_text_updates.json", """
        {
          "entries": [
            { "itemId": "letter_1", "title": "Письмо" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/misc/player_interactions.json", """
        {
          "interactions": [
            { "interactionId": "int_1", "summary": "Игроки встретились на тракте." }
          ]
        }
        """);
    }

    private async Task SeedRichMortalCombatFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/combat/enemies.json", """
        {
          "enemiesData": [
            {
              "enemyId": "shadow_messenger",
              "name": "Теневой посыльный",
              "type": "elite",
              "status": "hostile",
              "currentHealth": 18,
              "maxHealth": 30,
              "currentPoise": 4,
              "maxPoise": 10,
              "intent": "сорвать концентрацию мага",
              "targetPriority": "caster",
              "description": "Скользит между колоннами [red]без права на разметку[/].",
              "activeDebuffs": [
                {
                  "effectType": "burn",
                  "value": "-2 HP/ход",
                  "duration": 2,
                  "sourceSkill": "серебряная стрела",
                  "effectDescription": "Горит после серебряной стрелы."
                }
              ],
              "actions": [
                {
                  "actionName": "Теневой выпад",
                  "actionCost": "main",
                  "effects": [
                    {
                      "effectType": "damage",
                      "value": 7,
                      "targetType": "single_enemy",
                      "effectDescription": "Удар тенью по открытому боку."
                    }
                  ]
                }
              ]
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/combat/allies.json", """
        {
          "alliesData": [
            {
              "allyId": "rina_guard",
              "name": "Рина из Серебряной стражи",
              "role": "щит",
              "status": "wounded",
              "currentHealth": 22,
              "maxHealth": 28,
              "currentPoise": 7,
              "maxPoise": 12,
              "intent": "защищает мага",
              "description": "Держит линию у разбитой арки.",
              "activeBuffs": [
                {
                  "effectType": "inspire",
                  "value": "+1 стойкость",
                  "duration": 1,
                  "sourceSkill": "Боевой клич",
                  "effectDescription": "Боевой клич держит строй."
                }
              ],
              "actions": [
                {
                  "actionName": "Прикрыть щитом",
                  "actionCost": "fast",
                  "effects": [
                    {
                      "effectType": "guard",
                      "value": "+2 защита",
                      "targetType": "ally",
                      "effectDescription": "Закрывает союзника щитом."
                    }
                  ]
                }
              ]
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/combat/combat_log.json", """
        {
          "entries": [
            {
              "entryId": "log_round_2",
              "round": 2,
              "turn": 5,
              "participants": [
                "Теневой посыльный",
                "Рина из Серебряной стражи"
              ],
              "summary": "Рина сбила посыльного с фланга.",
              "result": "Теневой посыльный оглушён и теряет быстрое действие.",
              "consequences": [
                "Союзники получают окно для отхода."
              ]
            }
          ]
        }
        """);
    }

    private async Task SeedRichMortalWorldNewsFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "riots_at_gate",
              "title": "Беспорядки у Северных ворот",
              "timestamp": "день 42, утро",
              "locationId": "loc_north_gate",
              "visibility": "public",
              "status": "active",
              "category": "городские слухи",
              "description": "Толпа спорит у ворот [red]без разметки[/].",
              "summary": "Стража закрыла торговую площадь.",
              "involvedNPCs": [ "Мира Ключница" ],
              "affectedFactions": [ "Городская стража" ],
              "affectedLocations": [
                {
                  "locationId": "loc_north_gate",
                  "locationName": "Северные ворота",
                  "impactDescription": "У ворот усилили дозор."
                }
              ],
              "consequences": [ "торговая площадь закрыта до следующего утра" ],
              "followUp": "Капитан ждёт свидетелей.",
              "publicMood": "горожане боятся новых писем",
              "possibleLeads": [
                "проверить печать на письме",
                "найти курьера у старого рынка"
              ],
              "stakes": {
                "danger": "рынок может вспыхнуть",
                "deadline": "до полуночи",
                "visibleWitnessMemory": "Свидетели всё ещё помнят серебряную печать",
                "operatorPacket": {
                  "kind": "mortal_location_materialization_repair",
                  "title": "PRIVATE_BROWSER_WORLD_NEWS_LOCATION_REPAIR",
                  "rawCoordinate": "worldMapUpdates.newLocations[0]",
                  "targetFiles": [ "game_state/world/world_map.json" ]
                }
              },
              "witnessProfile": {
                "name": "Старый писарь",
                "testimony": "видел курьера у северных ворот",
                "npcId": "npc_old_scribe"
              },
              "sourcePath": "game_state/world/world_events.json",
              "statePath": "game_state/world",
              "sourceFile": "world_events.json",
              "sourceUrl": "https://internal.example/world_events.json"
            }
          ]
        }
        """);

        var gate = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_north_gate",
            "Северные ворота",
            "visited",
            x: 4,
            y: 8);
        var gateThreat = CreateCanonicalBrowserThreat(
            "gate_pickpockets",
            "Карманники у ворот",
            "Пользоваться давкой у северных ворот.",
            "высматривают кошельки в толпе");
        gateThreat["dangerLevel"] = "low";
        gateThreat["description"] = "Несколько ловкачей пользуются давкой.";
        gate["activeThreats"] = new JsonArray(gateThreat);
        gate["materialization"]!["sections"]!["activeThreats"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };
        MortalLocationTestFixture.ResealCanonicalLocation(gate);
        await WriteCanonicalBrowserMortalLocationStateAsync(
            [gate],
            MortalLocationTestFixture.CreateCurrentProjection(gate),
            [gate]);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_activities.json", """
        {
          "entries": [
            {
              "npcId": "npc_mira_key",
              "npcName": "Мира Ключница",
              "activityName": "Собирает свидетельства",
              "status": "active",
              "location": "Северные ворота",
              "description": "Записывает имена тех, кто видел письмо."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/factions/faction_projects.json", """
        {
          "entries": [
            {
              "factionId": "city_watch",
              "factionName": "Городская стража",
              "projectName": "Ночные патрули",
              "status": "active",
              "description": "Стража усиливает обходы у северной стены."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_flags.json", """
        {
          "worldStateFlags": [
            {
              "flagId": "festival_quiet",
              "displayName": "Праздник стих после тревоги",
              "scope": "Северный квартал",
              "status": "active",
              "value": "наблюдают стражники",
              "description": "Музыканты играют тише после ночного письма.",
              "consequence": "Площадь открыта только для жителей.",
              "rumors": [
                "скрипачи ушли до заката"
              ]
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/progression.json", """
        {
          "entries": [
            {
              "progressionId": "road_silverford",
              "trackerName": "Дорога к Серебряному броду",
              "stageName": "Караваны возвращаются",
              "status": "active",
              "description": "На тракте снова появились торговцы.",
              "changeReason": "Стража разогнала засаду.",
              "consequence": "Цены на соль упали.",
              "timestamp": "день 42",
              "nextSignals": [
                "караван просит охрану"
              ]
            }
          ]
        }
        """);
    }

    private async Task SeedRichMortalPlayerInteractionsFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/misc/player_interactions.json", """
        {
          "otherPlayersInteractions": {
            "player_lienna": {
              "playerId": "player_lienna",
              "displayName": "Лианна из янтарной башни",
              "relationship": "союзница по тайному письму",
              "context": "общий след странной печати",
              "status": "ждёт ответа у старого фонтана",
              "summary": "Лианна передаёт сведения о печати без лишней огласки.",
              "currentHooks": [
                "встретиться после заката",
                "проверить знак на перчатке"
              ],
              "records": [
                {
                  "interactionId": "meeting_cipher",
                  "title": "Передача шифра",
                  "status": "active",
                  "turn": 42,
                  "timestamp": "день 42, вечер",
                  "location": "Старый фонтан",
                  "route": "через старый фонтан",
                  "participants": [
                    "Лианна из янтарной башни",
                    "герой"
                  ],
                  "summary": "Лианна оставила шифр под бронзовой чашей.",
                  "notes": "шифр спрятан в перчатке",
                  "outcome": "контакт сохранён",
                  "nextStep": "Можно спросить о знаке Вальмонтов.",
                  "consequences": {
                    "visibleChange": "Лианна показала безопасный знак на серебряном ключе",
                    "materializationReceipt": "PRIVATE_INTERACTION_RECEIPT",
                    "nestedAuthority": {
                      "creationRef": "PRIVATE_INTERACTION_CREATION_REF",
                      "UpdateInventory": [
                        {
                          "itemName": "PRIVATE_INTERACTION_RAW_ITEM"
                        }
                      ]
                    }
                  },
                  "tags": [
                    "тайна",
                    "печать"
                  ]
                },
                {
                  "interactionId": "argument_at_ferry",
                  "title": "Спор у переправы",
                  "status": "resolved",
                  "location": "Северная переправа",
                  "summary": "Лианна отвлекла лодочника от разговора о печати."
                }
              ]
            },
            "player_kai": {
              "playerId": "player_kai",
              "displayName": "Страж Кай",
              "relationship": "осторожный наблюдатель",
              "records": [
                {
                  "interactionId": "watch_gate",
                  "title": "Дозор у ворот",
                  "summary": "Кай следит за слухами у северных ворот."
                }
              ]
            }
          }
        }
        """);
    }

    private async Task SeedCanonicalCommandPlayerInteractionsFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/misc/player_interactions.json", """
        {
          "otherPlayersInteractions": {
            "player_mara": [
              {
                "summary": "Лианна показала Серебряный ключ у старого фонтана",
                "visibleDetails": {
                  "appearance": "покрыт знаками янтарной башни",
                  "quantitySeen": 2
                },
                "UpdateInventory": [
                  {
                    "itemName": "PRIVATE_CANONICAL_INTERACTION_RAW_ITEM",
                    "materializationReceipt": "PRIVATE_CANONICAL_INTERACTION_RECEIPT"
                  }
                ]
              }
            ]
          }
        }
        """);
    }

    private async Task SeedRichMortalReferenceDetailFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/quests/regular_quests.json", """
        {
          "quests": [
            {
              "questId": "quest_winged_seal",
              "questName": "Печать с крыльями",
              "status": "Active",
              "questGiver": "Мира Ключница",
              "description": "Нужно вернуть письмо с крыльями и полумесяцем.",
              "objectives": [
                {
                  "description": "вернуть письмо в архив Вальмонтов",
                  "status": "Active"
                }
              ],
              "rewardInfo": {
                "visibleReward": "доступ к семейному архиву"
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/player/skills_active.json", """
        {
          "activeSkillChanges": [
            {
              "skillId": "skill_arcane_sense",
              "skillName": "Чувство магических потоков",
              "category": "Восприятие",
              "level": 2,
              "skillDescription": "Видит слабые печати на письмах и дверях.",
              "masteryContext": "Помогает замечать следы Вальмонтов."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/factions/faction_core.json", """
        {
          "factions": [
            {
              "factionId": "faction_city_watch",
              "name": "Городская стража",
              "reputation": 180,
              "level": "II",
              "description": "Стража удерживает северные ворота и ищет свидетелей.",
              "playerRank": "доверенный свидетель"
            }
          ]
        }
        """);

        var square = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_square",
            "Старая площадь",
            "visited",
            x: 4,
            y: 7);
        square["region"] = "Северный квартал";
        square["description"] = "Площадь с тёмным фонтаном и следами ночного письма.";
        var archive = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_archive",
            "Архив Вальмонтов",
            "discovered",
            x: 5,
            y: 7);
        archive["description"] = "Запертый зал под старой ратушей.";
        await WriteCanonicalBrowserMortalLocationStateAsync(
            [square, archive],
            MortalLocationTestFixture.CreateCurrentProjection(square),
            [square, archive]);

        await _fs.WriteFileAtomicAsync("game_state/world/rival_soul_arcs.json", """
        {
          "arcs": [
            {
              "arcId": "rival_arc_moonlit_claimant",
              "objective": "Соперник ищет ту же руническую перчатку.",
              "status": "active",
              "visibleClue": "На месте письма найден лунный воск.",
              "rivalSoul": {
                "rivalSoulId": "rival_soul_moonlit_claimant",
                "displayNameOrMoniker": "Лунный претендент"
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/guardian_corrections.json", """
        {
          "corrections": [
            {
              "correctionId": "correction_valmont_slot",
              "title": "Спорный слот вмешательства",
              "status": "contested",
              "sponsorGuardianRef": {
                "guardianId": "guardian_azalia",
                "displayName": "Азалия"
              },
              "budget": "1 мягкая правка",
              "scenarioCore": "Письмо нельзя превратить в прямую подсказку без цены."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/misc/storage_access.json", """
        {
          "grantStorageAccess": [
            {
              "storageId": "storage_valmont_private_desk",
              "storageName": "Приватный письменный стол",
              "accessLevel": "owner",
              "visibleReason": "Найден ключ от верхнего ящика.",
              "locationName": "Старая площадь"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/misc/vehicles.json", """
        {
          "vehicles": [
            {
              "vehicleId": "vehicle_gray_horse",
              "name": "Серый конь",
              "type": "mount",
              "availability": "active",
              "currentLocation": "Старая площадь",
              "capacity": "один всадник и сумка писем",
              "description": "Спокойный конь, обученный не бояться печатей."
            }
          ]
        }
        """);
    }

    private Task SeedMortalEffectsDetailStateAsync() =>
        _fs.WriteFileAtomicAsync("game_state/player/effects.json", """
        {
          "activeEffects": [
            {
              "effectId": "resonance_1",
              "name": "Магический резонанс",
              "effectDescription": "Руническая перчатка подсвечивает следы магии.",
              "duration": "До полудня",
              "source": "Руническая перчатка",
              "severity": "minor",
              "remainingTurns": 1,
              "structuredBonuses": [
                {
                  "bonusType": "Characteristic",
                  "target": "Восприятие",
                  "value": -1,
                  "valueType": "Flat",
                  "modifierType": "temporary",
                  "source": "Головная боль после тяжёлых снов",
                  "summary": "Восприятие -1",
                  "route": "Через серебряную арку"
                }
              ],
              "customProperties": [
                {
                  "interactionType": "onUse",
                  "route": "По звону хрустального колокола"
                }
              ],
              "notes": [
                {
                  "route": "По следу мерцающих рун"
                }
              ],
              "combatEffect": {
                "actionName": "Резонансный толчок",
                "isActivatedEffect": false,
                "effects": [
                  {
                    "effectType": "PoiseDamage",
                    "poiseDamage": 1,
                    "targetTypeDisplayName": "противник",
                    "effectDescription": "Сбивает концентрацию цели."
                  }
                ]
              }
            }
          ],
          "wounds": [],
          "temporaryConditions": [
            {
              "conditionId": "headache_1",
              "name": "Головная боль после тяжёлых снов",
              "effectDescription": "-1 к Восприятию до полудня.",
              "duration": "До 12:00"
            }
          ]
        }
        """);

    private async Task SeedCanonicalMortalSummaryFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/quests/regular_quests.json", """
        {
          "quests": [
            {
              "questId": "quest_valmont_letter",
              "questName": "Печать с крыльями и полумесяцем",
              "status": "Active"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/player/skills_active.json", """
        {
          "activeSkillChanges": [
            {
              "skillId": "skill_arcane_sense",
              "skillName": "Чувство магических потоков",
              "skillDescription": "Видит слабые печати."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/player/skills_passive.json", """
        {
          "passiveSkillChanges": [
            {
              "skillId": "skill_valmont_arcana",
              "skillName": "Аркановедение Вальмонтов",
              "masteryLevel": 2
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "world_event_valmont_letter",
              "title": "Письмо появилось ночью",
              "timestamp": "#[1] - 1 Month of Beginnings 124 г., 08:00",
              "description": "В покоях найдено письмо с крыльями и полумесяцем.",
              "sealDetails": "Печать: переплетённые крылья и полумесяц. Символ совпадает с намёками из семейного архива.",
              "possibleLeads": [
                "Сравнить печать с семейным архивом.",
                "Проверить, кто входил в покои после полуночи."
              ],
              "stakes": {
                "danger": "кто-то проверяет реакцию рунической перчатки",
                "deadline": "до ухода утренних слуг",
                "opportunity": "перехватить ночного посланника до смены караула"
              },
              "openQuestions": [
                "кто знает семейный шифр",
                "почему печать реагирует на перчатку"
              ],
              "playerKnowledge": "Асуран знает, что письмо появилось после полуночи и не похоже на письмо слуг.",
              "relatedPeople": [
                {
                  "name": "Мариус де Вальмонт",
                  "role": "первый свидетель ночных странностей",
                  "npcId": "npc_marius_valmont"
                }
              ]
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_flags.json", """
        {
          "worldStateFlags": [
            {
              "flagId": "flag_valmont_glove_awakened",
              "displayName": "Руническая перчатка пробудилась",
              "value": true
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/rival_soul_arcs.json", """
        {
          "arcs": [
            {
              "arcId": "rival_arc_moonlit_claimant",
              "objective": "Соперник ищет ту же руническую перчатку.",
              "rivalSoul": {
                "rivalSoulId": "rival_soul_moonlit_claimant",
                "displayNameOrMoniker": "Лунный претендент"
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_time.json", """
        {
          "year": 124,
          "monthName": "Month of Beginnings",
          "dayOfMonth": 1,
          "timeOfDay": "08:15",
          "currentTimeInMinutes": 495
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/weather.json", """
        {
          "tendency": "NO_CHANGE",
          "description": "Утренний туман рассеивается."
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/misc/vehicles.json", """
        {
          "vehicles": [
            {
              "vehicleId": "vehicle_gray_horse",
              "name": "Серый конь",
              "type": "mount",
              "availability": "active"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/player/effects.json", """
        {
          "activeEffects": [
            {
              "name": "Магический резонанс",
              "effectDescription": "Руническая перчатка подсвечивает следы магии.",
              "duration": "Пока перчатка экипирована"
            }
          ],
          "wounds": [],
          "temporaryConditions": [
            {
              "name": "Головная боль после тяжёлых снов",
              "effectDescription": "-1 к Восприятию до полудня.",
              "duration": "До 12:00"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/combat/enemies.json", """
        {
          "enemiesData": [
            {
              "enemyName": "Теневой посыльный",
              "description": "Слабый, но ловкий противник."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/combat/allies.json", """
        {
          "alliesData": [
            {
              "allyName": "Дворецкий Мариус",
              "description": "Союзник поддержки."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/combat/combat_log.json", """
        {
          "combat_log_markdown": "Последняя стычка: Ночной визитёр"
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/misc/storage_access.json", """
        {
          "grantStorageAccess": [
            {
              "storageId": "storage_valmont_private_desk",
              "storageName": "Приватный письменный стол",
              "accessLevel": "owner"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/misc/player_interactions.json", """
        {
          "otherPlayersInteractions": {
            "player_test_companion": [
              {
                "message": "Тестовый союзник оставил заметку о странной печати.",
                "visibleToPlayer": true
              }
            ]
          }
        }
        """);
    }

    private async Task SeedRichChaosSeaGuardianAbodeDrilldownFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 4,
          "inkFeathers": { "current": 18, "total": 55 },
          "enlightenment": { "currentTier": "Пепельная искра", "experience": 70 }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "activeGuardian": { "guardianId": "guardian_azalia", "guardianName": "Азалия" },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_azalia",
            "currentAbodeName": "Сад Ночных Роз",
            "knownAbodes": [
              { "abodeId": "abode_azalia", "name": "Сад Ночных Роз", "guardianId": "guardian_azalia" },
              { "abodeId": "abode_seret", "name": "Зал Серета", "guardianId": "guardian_seret" }
            ]
          },
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "domain": "Social",
              "description": "Покровительница перекрёстков и обещаний, собирающая забытые имена.",
              "relationshipData": {
                "currentReputation": 42,
                "lastInteraction": "Азалия приняла розовую печать."
              },
              "abode": {
                "abodeId": "abode_azalia",
                "name": "Сад Ночных Роз",
                "description": "Оранжерея памяти, где каждое обещание пускает корни.",
                "currentAnchor": "стеклянная теплица над Чёрной водой"
              },
              "abodePower": {
                "currentPower": 57,
                "maxPower": 100,
                "tier": "Укреплённая Обитель",
                "history": [
                  {
                    "eventId": "power_rose_offering",
                    "change": 7,
                    "reason": "Дар роз",
                    "summary": "Чернильные перья укрепили сад."
                  }
                ]
              },
              "questManagement": {
                "activeQuests": [
                  {
                    "questId": "quest_rose_key",
                    "name": "Ключ из лепестков",
                    "description": "Найти имя, спрятанное в розовой печати.",
                    "status": "active"
                  }
                ]
              },
              "loreFragments": [
                {
                  "fragmentId": "azalia_oath",
                  "title": "Клятва перекрёстка",
                  "content": "Азалия помнит дорогу к каждому невыполненному обещанию.",
                  "isUnlocked": true
                }
              ]
            },
            {
              "guardianId": "guardian_seret",
              "canonicalName": "Серет",
              "domain": "Knowledge",
              "description": "Серет хранит холодные архивы.",
              "relationshipData": { "currentReputation": -5 },
              "abode": {
                "abodeId": "abode_seret",
                "name": "Зал Серета",
                "description": "Тихий зал с зеркальными полками."
              },
              "abodePower": { "currentPower": 22, "maxPower": 100 }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardian_projects.json", """
        {
          "projects": [
            {
              "guardianId": "guardian_azalia",
              "project": {
                "projectId": "project_rose_gate",
                "projectName": "Врата роз",
                "projectType": "abode_fortification",
                "projectTier": "major",
                "projectMode": "internal",
                "activeState": "binding",
                "description": "Закрепить проход между розами и берегом Чёрной воды.",
                "startedTurn": 40,
                "estimatedCompletionTurn": 45,
                "workDone": 12,
                "totalWork": 20,
                "pressure": 3,
                "stability": 8,
                "systemEffectSummary": [
                  "Обитель лучше удерживает гостей и клятвы."
                ]
              }
            },
            {
              "guardianId": "guardian_seret",
              "project": {
                "projectId": "project_seret_gate",
                "projectName": "Врата Серета",
                "projectType": "lore_research",
                "activeState": "researching",
                "description": "Собрать холодные записи в Зале Серета."
              }
            }
          ],
          "completedProjects": [],
          "journal": [
            {
              "entryId": "journal_rose_gate_1",
              "guardianId": "guardian_azalia",
              "projectId": "project_rose_gate",
              "turn": 41,
              "title": "Розы приняли первый якорь",
              "summary": "Корни сада начали держать проход."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardian_project_journal.json", """
        {
          "entries": [
            {
              "entryId": "journal_rose_gate_1",
              "guardianId": "guardian_azalia",
              "projectId": "project_rose_gate",
              "turn": 41,
              "title": "Розы приняли первый якорь",
              "summary": "Корни сада начали держать проход.",
              "details": [ "Work: 12/20", "Stability: 8" ]
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/abode_power_journal.json", """
        {
          "entries": [
            {
              "entryId": "power_rose_offering",
              "eventId": "power_rose_offering",
              "guardianId": "guardian_azalia",
              "title": "Дар роз",
              "summary": "Чернильные перья укрепили сад и подняли решётку у входа.",
              "reasonType": "offering",
              "delta": 7,
              "turn": 41,
              "appliedAt": "2026-06-01T12:00:00Z"
            },
            {
              "entryId": "power_seret_gate",
              "eventId": "power_seret_gate",
              "guardianId": "guardian_seret",
              "title": "Врата Серета",
              "summary": "Зал Серета укрепил холодный архив.",
              "reasonType": "project_completion",
              "delta": 3,
              "turn": 42
            }
          ]
        }
        """);
    }

    private async Task SeedChaosSeaFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Test Soul",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 4,
          "inkFeathers": { "current": 18, "total": 55 },
          "enlightenment": { "currentTier": "Пепельная искра", "experience": 70 }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "activeGuardian": { "guardianId": "guardian_azalia", "guardianName": "Азалия" },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_azalia",
            "currentAbodeName": "Сад Ночных Роз",
            "knownAbodes": [
              { "abodeId": "abode_azalia", "name": "Сад Ночных Роз", "guardianId": "guardian_azalia" }
            ]
          },
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "domain": "Social",
              "relationshipData": { "currentReputation": 12 },
              "abode": { "abodeId": "abode_azalia", "name": "Сад Ночных Роз" },
              "abodePower": { "currentPower": 30, "maxPower": 100 },
              "gachaSystem": {
                "chargesPerReturn": 1,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardian_projects.json", """
        {
          "projects": [
            { "projectId": "project_1", "guardianId": "guardian_azalia", "status": "active", "title": "Садовая клятва" }
          ],
          "journal": [
            { "entryId": "entry_1", "guardianId": "guardian_azalia", "summary": "Проект начат." }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(ChaosSeaGuardianPoliticsState.StatePath, """
        {
          "schemaVersion": 1,
          "relations": [
            {
              "relationId": "azalia_seret_alliance",
              "sourceGuardianId": "guardian_azalia",
              "targetGuardianId": "guardian_seret",
              "relationType": "alliance",
              "attitudeScore": 62,
              "visibility": "known",
              "reason": "Азалия ищет союзников против охотников памяти.",
              "lastChangedTurn": 12,
              "effects": [ "training_discount" ]
            },
            {
              "relationId": "azalia_hidden_dependency",
              "sourceGuardianId": "guardian_azalia",
              "targetGuardianId": "system_saref_shadow",
              "relationType": "hidden_dependency",
              "attitudeScore": -80,
              "visibility": "hidden",
              "reason": "Скрытая зависимость не должна отображаться игроку.",
              "lastChangedTurn": 12,
              "effects": []
            }
          ],
          "projects": [],
          "influenceZones": [],
          "chronicle": []
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/abode_power_journal.json", """
        {
          "guardianPowerEvents": [
            { "eventId": "power_1", "guardianId": "guardian_azalia", "reasonType": "offering", "finalDelta": 5 }
          ]
        }
        """);
    }

    private async Task SeedPendingGachaBaseAsync(string baseRarity, int baseScore, IReadOnlyList<int> diceUsed)
    {
        var diceArray = new JsonArray();
        foreach (var die in diceUsed)
            diceArray.Add(die);

        await _fs.WriteFileAtomicAsync(
            PendingTurnStateService.PendingDiceStatePath,
            new JsonObject
            {
                ["preGeneratedDices1d20"] = new JsonArray(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20),
                ["gachaBaseResult"] = new JsonObject
                {
                    ["diceUsed"] = diceArray,
                    ["baseScore"] = baseScore,
                    ["baseRarity"] = baseRarity,
                    ["formula"] = "client-computed gacha base (range 4-80)"
                },
                ["isFateLocked"] = false,
                ["createdAtUtc"] = "2026-06-02T00:00:00Z",
                ["lastUpdatedUtc"] = "2026-06-02T00:00:00Z"
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private static string FormatRarityForPlayerTest(string rarity) =>
        rarity.Trim().ToLowerInvariant() switch
        {
            "common" => "обычная",
            "uncommon" => "необычная",
            "rare" => "редкая",
            "epic" => "эпическая",
            "legendary" => "легендарная",
            _ => rarity
        };

    private Task WriteGuardianPoliticsRawLeakFixtureAsync() =>
        _fs.WriteFileAtomicAsync(ChaosSeaGuardianPoliticsState.StatePath, """
        {
          "schemaVersion": 1,
          "relations": [
            {
              "relationId": "azalia_seret_alliance",
              "sourceGuardianId": "guardian_azalia",
              "targetGuardianId": "guardian_seret",
              "relationType": "alliance",
              "attitudeScore": 62,
              "visibility": "known",
              "reason": "Азалия ищет союзников против охотников памяти.",
              "lastChangedTurn": 12,
              "effects": [ "training_discount" ]
            },
            {
              "relationId": "azalia_hidden_dependency",
              "sourceGuardianId": "guardian_azalia",
              "targetGuardianId": "system_saref_shadow",
              "relationType": "hidden_dependency",
              "attitudeScore": -80,
              "visibility": "hidden",
              "reason": "Скрытая зависимость не должна отображаться игроку.",
              "lastChangedTurn": 12,
              "effects": []
            },
            {
              "relationId": "azalia_player_invisible_false",
              "sourceGuardianId": "guardian_azalia",
              "targetGuardianId": "system_invisible_false_guardian",
              "relationType": "alliance",
              "attitudeScore": 10,
              "visibility": "known",
              "isPlayerVisible": false,
              "reason": "is_player_visible_false_marker",
              "lastChangedTurn": 12,
              "effects": []
            }
          ],
          "projects": [
            {
              "projectId": "project_archive_pact",
              "ownerGuardianId": "guardian_azalia",
              "targetGuardianId": "guardian_seret",
              "projectType": "alliance",
              "status": "active",
              "summary": "Публичный архивный пакт укрепляет безопасные маршруты.",
              "currentProgress": 2,
              "requiredProgress": 5,
              "lastUpdatedTurn": 12,
              "visibility": "known"
            },
            {
              "projectId": "secret_project_marker",
              "ownerGuardianId": "guardian_azalia",
              "targetGuardianId": "system_saref_shadow",
              "projectType": "rivalry",
              "status": "active",
              "summary": "Секретный проект не должен попасть в обычный DTO.",
              "currentProgress": 1,
              "requiredProgress": 4,
              "lastUpdatedTurn": 12,
              "visibility": "gm_only"
            }
          ],
          "influenceZones": [],
          "chronicle": [],
          "hiddenRelations": [
            {
              "relationId": "hidden_relations_marker",
              "targetGuardianId": "system_saref_shadow"
            }
          ],
          "secretProjects": [
            {
              "projectId": "secret_project_marker"
            }
          ],
          "internalMotivations": {
            "guardian_azalia": "internal_motivation_marker"
          }
        }
        """);

    private async Task SeedShiningAbodeFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Test Soul",
          "currentRealm": "Shining Abode",
          "currentIncarnation": 5,
          "inkFeathers": { "current": 24, "total": 90 },
          "afterlifeCombatProfile": { "capstones": {} }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/shining_abode_state.json", """
        {
          "availability": "active",
          "lightSparks": 7,
          "radiance": { "experience": 260, "tier": 3 },
          "gates": { "hasOpenDraft": true },
          "treasury": {
            "depositedInkFeathers": 20,
            "claimableInkFeatherInterest": 2,
            "lastInterestSettlementCycleId": "cycle_5",
            "exchangeCycleId": "cycle_5",
            "exchangeThisCycleLightSparks": 1
          },
          "gachaSystem": {
            "chargesPerReturn": 2,
            "chargesUsedThisReturn": 1,
            "currentReturnCycleId": "cycle_5",
            "gachaHistory": []
          },
          "halls": [
            { "hallId": "hall_dawn", "hallName": "Зал Рассвета" }
          ],
          "factions": [
            {
              "factionId": "faction_lanterns",
              "hallId": "hall_dawn",
              "factionStrength": 40,
              "charter": { "factionName": "Фонари Рассвета" },
              "leadership": { "headActorType": "resident", "headActorId": "resident_1", "leadershipState": "secure" },
              "projects": [
                { "projectId": "project_light", "displayName": "Световой мост", "status": "active", "tier": 1 }
              ]
            }
          ],
          "shiningPoliticalActors": [
            { "actorId": "actor_1", "displayName": "Светозарный судья", "politicalStatus": "elder" }
          ],
          "coreActionReceipts": [
            { "receiptId": "receipt_1", "actionType": "draft_incarnation_package" }
          ],
          "sourceOfLightCapstone": { "completed": false }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardian_abode_residents.json", """
        {
          "entries": [
            {
              "residentId": "resident_1",
              "displayName": "Лиара",
              "ascensionState": "ascended",
              "shiningFactionId": "faction_lanterns",
              "factionLoyaltyLevel": 60,
              "factionLoyaltyTier": "attached"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            { "guardianId": "guardian_azalia", "canonicalName": "Азалия" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_shining_abode_actions.json", """
        {
          "requests": [
            { "requestId": "core_req_1", "actionType": "draft_incarnation_package" }
          ]
        }
        """);

        await MaterializeShiningWebFixtureAsync(materializedAtTurn: 1);
    }

    private async Task WriteShiningFactionPoliticalMemoryRawLeakFixtureAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/shining_abode_state.json", """
        {
          "availability": "active",
          "lightSparks": 7,
          "radiance": { "experience": 260, "tier": 3 },
          "gates": { "hasOpenDraft": true },
          "treasury": {
            "depositedInkFeathers": 20,
            "claimableInkFeatherInterest": 2,
            "lastInterestSettlementCycleId": "cycle_5",
            "exchangeCycleId": "cycle_5",
            "exchangeThisCycleLightSparks": 1
          },
          "gachaSystem": {
            "chargesPerReturn": 2,
            "chargesUsedThisReturn": 1,
            "currentReturnCycleId": "cycle_5",
            "gachaHistory": []
          },
          "halls": [
            { "hallId": "hall_dawn", "hallName": "Зал Рассвета" }
          ],
          "factions": [
            {
              "factionId": "faction_lanterns",
              "hallId": "hall_dawn",
              "factionStrength": 40,
              "charter": { "factionName": "Фонари Рассвета" },
              "leadership": { "headActorType": "resident", "headActorId": "resident_1", "leadershipState": "secure" },
              "chronicle": [
                {
                  "entryId": "lanterns_safe_passage_45",
                  "turnNumber": 45,
                  "eventType": "public_aid",
                  "summary": "Открыли безопасный проход для потерянных резидентов.",
                  "visibility": "visible",
                  "consequences": [ "Игрок может просить фракцию о публичной помощи." ],
                  "occurredAtUtc": "2026-05-25T12:00:00Z"
                },
                {
                  "entryId": "lanterns_hidden_oath_46",
                  "turnNumber": 46,
                  "eventType": "hidden_oath",
                  "summary": "hidden_chronicle_marker",
                  "visibility": "hidden",
                  "consequences": [ "hidden_chronicle_marker" ],
                  "occurredAtUtc": "2026-05-25T13:00:00Z"
                }
              ],
              "territorialInfluence": [
                {
                  "zoneId": "lanterns_hall_public",
                  "scopeType": "hall",
                  "scopeId": "hall_dawn",
                  "displayName": "Серебряный Зал",
                  "controlLevel": 64,
                  "influenceValue": 58,
                  "publicStatus": "известное убежище",
                  "updatedAtTurn": 46,
                  "sourceEntryId": "lanterns_safe_passage_45",
                  "summary": "Фракция публично удерживает безопасный прием резидентов."
                }
              ],
              "strategicMemory": {
                "summary": "hidden_strategy_marker",
                "lastUpdatedTurn": 46,
                "recentCampaigns": [ "hidden_strategy_marker" ],
                "losses": [ "hidden_strategy_marker" ],
                "alliances": [ "guardian_azalia" ],
                "enemies": [ "hidden_strategy_marker" ]
              },
              "resourceLedger": [
                {
                  "entryId": "lanterns_light_sparks_45",
                  "turnNumber": 45,
                  "resourceType": "lightSparks",
                  "delta": 3,
                  "balanceAfter": 19,
                  "reason": "Публичная помощь привела к пожертвованиям Искр Света.",
                  "internalNote": "hidden_ledger_marker",
                  "occurredAtUtc": "2026-05-25T12:05:00Z"
                }
              ],
              "projects": [
                { "projectId": "project_light", "displayName": "Световой мост", "status": "active", "tier": 1 }
              ]
            }
          ],
          "shiningPoliticalActors": [
            { "actorId": "actor_1", "displayName": "Светозарный судья", "politicalStatus": "elder" }
          ],
          "coreActionReceipts": [
            { "receiptId": "receipt_1", "actionType": "draft_incarnation_package" }
          ],
          "sourceOfLightCapstone": { "completed": false }
        }
        """);

        await MaterializeShiningWebFixtureAsync(materializedAtTurn: 45);
    }

    private async Task MaterializeShiningWebFixtureAsync(
        int materializedAtTurn)
    {
        var root = JsonNode.Parse(
            (await _fs.ReadFileAsync(ShiningAbodeState.StatePath))!)!
            .AsObject();
        foreach (var faction in root["factions"]!.AsArray()
                     .OfType<JsonObject>())
        {
            var factionId = faction["factionId"]!.GetValue<string>();
            faction["originType"] ??=
                ShiningAbodeState.OriginTypeNativeRadiant;
            faction["visibility"] = "revealed";
            faction["baseStrength"] ??= 37;
            faction["investCountThisAscension"] ??= 0;
            faction["projectArchetypesCountedThisAscension"] ??=
                new JsonArray();
            faction["factionLifecycle"] = new JsonObject
            {
                ["state"] = ShiningAbodeState.FactionLifecycleStateActive
            };

            var charter = faction["charter"]!.AsObject();
            charter["favoredArchetype"] ??=
                ShiningAbodeState.ProjectArchetypeAccord;
            charter["patronEffectFamily"] ??=
                ShiningAbodeState.EffectFamilySocial;
            charter["summary"] ??=
                "Тестовая сияющая фракция хранит безопасный путь.";

            foreach (var project in faction["projects"]!.AsArray()
                         .OfType<JsonObject>())
            {
                project["summary"] ??=
                    "Тестовый проект поддерживает путь фракции.";
                project["projectArchetype"] ??=
                    ShiningAbodeState.ProjectArchetypeAccord;
                project["outputEffectFamily"] ??=
                    ShiningAbodeState.EffectFamilySocial;
                project["isSupported"] ??= false;
                project["strengthReward"] ??= 8;
            }

            faction[ShiningAbodeState.FactionChronicleProperty] ??=
                new JsonArray();
            faction[ShiningAbodeState.FactionInfluenceProperty] ??=
                new JsonArray();
            faction[ShiningAbodeState.FactionResourceLedgerProperty] ??=
                new JsonArray();
            faction["tradeInventoryReceipts"] ??= new JsonArray();
            faction["leadershipReceipts"] ??= new JsonArray();
            faction["leadershipHistory"] ??= new JsonArray();

            ShiningFactionTestMaterialization.Apply(
                faction,
                materializedAtTurn,
                hasResidentAffiliations: string.Equals(
                    factionId,
                    "faction_lanterns",
                    StringComparison.Ordinal),
                canTrade: false);
        }

        await _fs.WriteFileAtomicAsync(
            ShiningAbodeState.StatePath,
            root.ToJsonString(
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task SeedAfterlifeCombatAndEntityFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Test Soul",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 6,
          "inkFeathers": { "current": 40, "total": 120 },
          "enlightenment": { "currentTier": "Пламенный знак", "experience": 160 },
          "afterlifeCombatProfile": {
            "enlightenmentTier": 3,
            "radianceTier": 1,
            "spiritFocusTier": 2,
            "artTiers": {
              "pressure": 2,
              "guard": 1,
              "counter": 1,
              "maneuver": 2,
              "binding": 1,
              "recover_spiritual_power": 1
            }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/afterlife_entity_profiles.json", """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "player_soul",
              "actorId": "player_soul",
              "displayName": "Test Soul",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 40, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "tier": 3, "experience": 160 },
                "radiance": { "tier": 1, "experience": 20 }
              },
              "standardArts": {
                "pressure": 2,
                "guard": 1,
                "counter": 1,
                "maneuver": 2,
                "binding": 1,
                "recover_spiritual_power": 1
              },
              "specialArts": [
                {
                  "artId": "rose_mirror_counter",
                  "displayName": "Зеркало Ночной Розы",
                  "baseOperation": "counter",
                  "tier": 1,
                  "effectSummary": "Контрприём оставляет болезненный образ в клятве противника.",
                  "combatEffect": {
                    "summary": "Ночной контрприём превращает входящее давление в брешь для ответа.",
                    "trigger": "Когда counter отвечает на прямое pressure или binding.",
                    "mechanicalAxis": "rollMode",
                    "allowedPayoff": "Можно дать Преимущество для ответного pressure через condition-backed rollMode source.",
                    "limit": "Один раз за конфликт, пока брешь не потрачена или не закрыта guard.",
                    "auditRequirement": "specialArtAudit.effectNote должен назвать входящее действие и источник Преимущества hidden_saref_combat_effect_marker."
                  },
                  "costMultiplierPercent": 150,
                  "canTeachPlayer": true
                }
              ],
              "customStates": [
                { "stateId": "memory_echo", "stateName": "Эхо памяти", "currentValue": 2, "maxValue": 5 }
              ],
              "soulDissipationTier": 1,
              "progressionStrategy": {
                "summary": "Сначала усилить защиту и манёвр.",
                "priorityOrder": [ "guard", "maneuver", "pressure" ],
                "lastAutoProgressionCycleKey": "cycle_6"
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/afterlife_spiritual_conflict_state.json", """
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "conflict_1",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "conflictPosition": "player_advantaged",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "resolutionState": "active",
            "controlState": {
              "controlId": "control_1",
              "controllerSide": "player",
              "level": "hindered",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Оковы держат противника у края клятвы."
            },
            "actionEconomy": {
              "player": { "current": 7, "max": 8, "source": "spirit_focus" },
              "opposition": { "current": 5, "max": 6, "source": "profile" }
            },
            "playerSide": { "leadContestant": { "actorId": "player_soul", "displayName": "Test Soul" } },
            "oppositionSide": { "leadContestant": { "actorId": "guardian_shadow", "displayName": "Тень Хранителя" } },
            "exchangeLog": [
              {
                "exchangeId": "exchange_1",
                "operationType": "pressure",
                "outcome": "success",
                "exchangeAtTurn": 6,
                "actionPointCost": { "player": 2, "opposition": 1, "total": 3 },
                "before": { "conflictPosition": "contested", "oppositionSideStrain": "clear" },
                "after": { "conflictPosition": "player_advantaged", "oppositionSideStrain": "strained" },
                "diceAudit": {
                  "rolls": [
                    { "side": "player", "value": 15 },
                    { "side": "opposition", "value": 9 }
                  ],
                  "playerTotal": 18,
                  "oppositionTotal": 11,
                  "margin": 7
                },
                "rewardAudit": {
                  "currency": "ink_feathers",
                  "finalAmount": 3,
                  "resolvedAtTurn": 6
                }
              }
            ]
          },
          "recentConflicts": [
            {
              "conflictId": "conflict_done",
              "resolutionState": "resolved",
              "operationType": "pressure",
              "playerOutcome": "victory",
              "resolvedAtTurn": 5,
              "rewardAudit": {
                "currency": "ink_feathers",
                "finalAmount": 2,
                "resolvedAtTurn": 5
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/afterlife_notifications.json", """
        {
          "notifications": [
            {
              "notificationId": "notification_1",
              "notificationType": "guardian_quest_available",
              "requestId": "quest_req_1",
              "status": "unread",
              "guardianId": "guardian_azalia",
              "guardianName": "Азалия",
              "summary": "Хранитель предлагает тёмный след из прошлой жизни.",
              "createdAtTurn": 6,
              "createdAtUtc": "2026-05-20T00:00:00Z"
            }
          ]
        }
        """);
    }

    private Task WriteAfterlifeConflictStateWithCombatConditionsAsync() =>
        _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, """
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "conflict_conditions_1",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "resolutionState": "active",
            "actionEconomy": {
              "player": { "current": 7, "max": 8, "source": "spirit_focus" },
              "opposition": { "current": 5, "max": 6, "source": "profile" }
            },
            "playerSide": { "leadContestant": { "actorId": "player_soul", "displayName": "Test Soul" } },
            "oppositionSide": { "leadContestant": { "actorId": "guardian_shadow", "displayName": "Тень Хранителя" } },
            "combatConditions": [
              {
                "conditionId": "mark_oath_flare_001",
                "displayName": "Разогретая клятва",
                "kind": "mark",
                "polarity": "buff",
                "status": "active",
                "source": {
                  "type": "special_art",
                  "actorType": "guardian",
                  "actorId": "guardian_azalia",
                  "displayName": "Азалия"
                },
                "targetSide": "opposition",
                "targetActorRef": "guardian_shadow",
                "affectedOperations": [ "pressure", "counter" ],
                "mechanicalAxis": "rollMode",
                "payoff": {
                  "effect": "advantage",
                  "level": "advantage",
                  "sourceType": "combat_condition"
                },
                "duration": {
                  "type": "next_matching_operation",
                  "remainingUses": 1
                },
                "counterplay": [ "break_binding против контекста клятвы", "выбрать действие вне pressure/counter" ],
                "visibility": "player_visible",
                "summary": "Клятва подсвечена: pressure и counter легче направить в противника.",
                "auditRequirement": "При расходовании rollMode должен сослаться на conditionId."
              },
              {
                "conditionId": "hidden_condition_marker",
                "displayName": "hidden_condition_marker",
                "kind": "vow",
                "polarity": "debuff",
                "status": "active",
                "source": {
                  "type": "story_link",
                  "actorType": "guardian",
                  "actorId": "guardian_hidden",
                  "displayName": "hidden_condition_marker"
                },
                "targetSide": "player",
                "affectedOperations": [ "guard" ],
                "mechanicalAxis": "rollMode",
                "payoff": {
                  "effect": "disadvantage",
                  "level": "disadvantage",
                  "sourceType": "combat_condition"
                },
                "duration": {
                  "type": "scene",
                  "remainingUses": 1
                },
                "counterplay": [ "hidden_condition_marker" ],
                "visibility": "gm_only",
                "summary": "hidden_summary_legacy_marker",
                "auditRequirement": "hidden_audit_legacy_marker"
              },
              {
                "conditionId": "concealed_condition_marker",
                "displayName": "concealed_condition_marker",
                "kind": "vow",
                "polarity": "debuff",
                "status": "active",
                "source": {
                  "type": "story_link",
                  "actorType": "guardian",
                  "actorId": "guardian_concealed",
                  "displayName": "concealed_condition_marker"
                },
                "targetSide": "player",
                "affectedOperations": [ "guard" ],
                "mechanicalAxis": "rollMode",
                "payoff": {
                  "effect": "disadvantage",
                  "level": "disadvantage",
                  "sourceType": "combat_condition"
                },
                "duration": {
                  "type": "scene",
                  "remainingUses": 1
                },
                "counterplay": [ "concealed_condition_marker" ],
                "visibility": "concealed",
                "summary": "concealed_condition_marker",
                "auditRequirement": "concealed_condition_marker"
              },
              {
                "conditionId": "spoiler_condition_marker",
                "displayName": "spoiler_condition_marker",
                "kind": "vow",
                "polarity": "debuff",
                "status": "active",
                "source": {
                  "type": "story_link",
                  "actorType": "guardian",
                  "actorId": "guardian_spoiler",
                  "displayName": "spoiler_condition_marker"
                },
                "targetSide": "player",
                "affectedOperations": [ "guard" ],
                "mechanicalAxis": "rollMode",
                "payoff": {
                  "effect": "disadvantage",
                  "level": "disadvantage",
                  "sourceType": "combat_condition"
                },
                "duration": {
                  "type": "scene",
                  "remainingUses": 1
                },
                "counterplay": [ "spoiler_condition_marker" ],
                "visibility": "spoiler",
                "summary": "spoiler_condition_marker",
                "auditRequirement": "spoiler_condition_marker"
              }
            ],
            "exchangeLog": [
              {
                "exchangeId": "exchange_hidden_roll_source_marker_001",
                "operationType": "pressure",
                "outcome": "success",
                "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
                "after": { "playerSideStrain": "clear", "oppositionSideStrain": "strained", "conflictPosition": "contested" },
                "diceAudit": {
                  "rollMode": {
                    "player": {
                      "effectiveMode": "normal",
                      "advantageSources": [
                        "позиционное преимущество",
                        "ordinary_visible_roll_reason",
                        "mark_oath_flare_001",
                        {
                          "sourceType": "combat_condition",
                          "conditionId": "mark_oath_flare_001",
                          "level": "advantage",
                          "summary": "visible_condition_roll_source_marker"
                        },
                        {
                          "sourceType": "guard_tempo_window",
                          "sourceId": "tempo_guard_valid_001",
                          "level": "advantage",
                          "summary": "guard_tempo_window_marker"
                        }
                      ],
                      "disadvantageSources": [
                        "hidden_condition_marker",
                        "hidden_summary_legacy_marker",
                        "hidden_audit_legacy_marker",
                        "concealed_condition_marker",
                        "spoiler_condition_marker",
                        {
                          "sourceType": "combat_condition",
                          "conditionId": "hidden_condition_marker",
                          "level": "disadvantage",
                          "summary": "hidden_condition_marker"
                        },
                        {
                          "sourceType": "combat_condition",
                          "conditionId": "concealed_condition_marker",
                          "level": "disadvantage",
                          "summary": "concealed_condition_marker"
                        },
                        {
                          "sourceType": "combat_condition",
                          "sourceId": "spoiler_condition_marker",
                          "level": "disadvantage",
                          "summary": "spoiler_condition_marker"
                        }
                      ]
                    },
                    "opposition": {
                      "effectiveMode": "normal",
                      "advantageSources": [],
                      "disadvantageSources": []
                    }
                  }
                }
              }
            ]
          },
          "recentConflicts": []
        }
        """);

    private async Task WriteAfterlifeProfilesMaskProjectionFixtureAsync()
    {
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_masked_truths",
              "displayName": "Хранитель Масок",
              "realm": "Chaos Sea",
              "locationName": "Театр известных лиц",
              "currencies": { "inkFeathers": 1, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "tier": 1, "experience": 0 },
                "radiance": { "tier": 0, "experience": 0 }
              },
              "standardArts": { "guard": 1 },
              "activeMaskId": "mask_active_envoy",
              "masks": [
                {
                  "maskId": "mask_active_envoy",
                  "displayName": "Активный посланник",
                  "publicArchetype": "дипломат",
                  "visiblePersonality": "улыбается и просит доверия",
                  "concealedTruth": "hidden_active_truth_marker",
                  "directives": [ "hidden_active_directive_marker" ],
                  "revealConditions": [ "hidden_active_condition_marker" ],
                  "deceptionRisk": "high",
                  "linkedThreatId": "hidden_threat_marker",
                  "linkedSarefAgentId": "hidden_saref_marker",
                  "isRevealed": false
                },
                {
                  "maskId": "mask_revealed_sign",
                  "displayName": "Раскрытая вывеска",
                  "publicArchetype": "бывший судья",
                  "visiblePersonality": "говорит прямее после разоблачения",
                  "concealedTruth": "known_revealed_truth_marker",
                  "directives": [ "known_revealed_directive_marker" ],
                  "revealConditions": [ "known_revealed_condition_marker" ],
                  "deceptionRisk": "medium",
                  "linkedThreatId": "known_revealed_threat_marker",
                  "linkedSarefAgentId": "known_revealed_saref_marker",
                  "isRevealed": true
                },
                {
                  "maskId": "mask_dormant_shadow",
                  "displayName": "Скрытый запасной образ",
                  "publicArchetype": "будущий свидетель",
                  "visiblePersonality": "молчит до сцены раскрытия",
                  "concealedTruth": "hidden_dormant_truth_marker",
                  "directives": [ "hidden_dormant_directive_marker" ],
                  "revealConditions": [ "hidden_dormant_condition_marker" ],
                  "deceptionRisk": "critical",
                  "linkedThreatId": "hidden_dormant_threat_marker",
                  "linkedSarefAgentId": "hidden_dormant_saref_marker",
                  "isRevealed": false
                }
              ],
              "goals": {
                "goalId": "goal_masked_truths",
                "shortTermGoal": "Держать известные лица в порядке"
              },
              "soulDissipationTier": 0
            }
          ]
        }
        """);
    }

    private async Task WriteAfterlifeProfilesRelationshipGatesFixtureAsync()
    {
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "locationName": "Зал честных отражений",
              "currencies": { "inkFeathers": 4, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "tier": 1, "experience": 12 },
                "radiance": { "tier": 0, "experience": 0 }
              },
              "standardArts": { "guard": 1 },
              "relationships": [
                {
                  "relationshipId": "guardian_mirror_player_trust",
                  "axis": "trust",
                  "targetActorType": "player_soul",
                  "targetActorId": "player_soul",
                  "value": 49,
                  "relationshipTier": "trust_breakthrough_required",
                  "relationshipLock": {
                    "lockState": "positive_locked",
                    "direction": "positive",
                    "threshold": 50,
                    "breakthroughQuestId": "quest_mirror_oath_trial",
                    "reason": "Хранитель не доверится глубже без личного испытания.",
                    "evidence": "hidden_lock_evidence_marker",
                    "updatedAtTurn": 41
                  },
                  "relationshipGateQuests": [
                    {
                      "questId": "quest_mirror_oath_trial",
                      "questType": "breakthrough",
                      "status": "active",
                      "title": "Суд зеркальной клятвы",
                      "sceneSummary": "Личное испытание доверия.",
                      "successCondition": "Душа выбирает правду.",
                      "gmThoughtsSummary": "hidden_gate_gm_thoughts_marker",
                      "updatedAtTurn": 41
                    }
                  ]
                }
              ],
              "goals": {
                "goalId": "goal_mirror_guardian",
                "shortTermGoal": "Проверить готовность души к правде"
              },
              "soulDissipationTier": 0
            }
          ]
        }
        """);
    }

    private async Task WriteAfterlifeProfilesRawLeakFixtureAsync()
    {
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_open_rose",
              "displayName": "Хранитель Открытой Розы",
              "realm": "Chaos Sea",
              "locationName": "Открытая обитель",
              "currencies": { "inkFeathers": 12, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "tier": 1, "experience": 10 },
                "radiance": { "tier": 0, "experience": 0 }
              },
              "standardArts": { "guard": 1 },
              "fateCards": [
                {
                  "cardId": "visible_oath_card",
                  "nameRu": "Открытая карта клятвы",
                  "status": "available",
                  "storyMeaning": "Игрок знает, что клятва может открыть обучение."
                },
                {
                  "cardId": "hidden_saref_card",
                  "nameRu": "Секретная карта Сарефа hidden_fate_card_marker",
                  "status": "hidden",
                  "isSecret": true,
                  "storyMeaning": "hidden_card_story_marker",
                  "unlockConditions": [ "hidden_condition_marker" ]
                }
              ],
              "activeMaskId": "mask_courteous_envoy",
              "masks": [
                {
                  "maskId": "mask_courteous_envoy",
                  "displayName": "Учтивый посредник",
                  "publicArchetype": "мягкий переговорщик",
                  "visiblePersonality": "улыбается и говорит о мире",
                  "concealedTruth": "hidden_concealed_truth_marker",
                  "directives": [ "hidden_mask_directive_marker" ],
                  "linkedSarefAgentId": "hidden_saref_agent_marker",
                  "isRevealed": false
                }
              ],
              "goals": {
                "goalId": "goal_open_guard",
                "shortTermGoal": "Открытая цель: защитить игрока",
                "longTermGoal": "Сохранить обитель",
                "plan": "Говорить только известные игроку части плана.",
                "gmThoughtsSummary": "hidden_actor_motivation_marker"
              },
              "personalQuests": [
                {
                  "questId": "quest_visible_guard",
                  "goalId": "goal_open_guard",
                  "status": "active",
                  "title": "Видимый личный квест",
                  "planSummary": "Проверить клятву без раскрытия тайных мотивов."
                }
              ],
              "currentActivity": {
                "activityId": "activity_visible_watch",
                "goalId": "goal_open_guard",
                "linkedQuestId": "quest_visible_guard",
                "summary": "Собирает видимые сведения",
                "gmThoughtsSummary": "hidden_activity_motivation_marker"
              },
              "soulDissipationTier": 0,
              "materialization": {
                "schemaVersion": 1,
                "materializationId": "private_afterlife_materialization_marker",
                "actorType": "guardian",
                "actorId": "guardian_open_rose",
                "materializedAtTurn": 8,
                "state": "complete",
                "capabilities": {
                  "canFight": true,
                  "canTeach": false,
                  "canTrade": false
                },
                "sections": {
                  "standardArts": { "state": "populated" },
                  "specialArts": {
                    "state": "empty_by_design",
                    "reason": "private_afterlife_empty_reason_marker"
                  },
                  "customStates": {
                    "state": "empty_by_design",
                    "reason": "private_afterlife_custom_state_reason_marker"
                  },
                  "fateCards": { "state": "populated" },
                  "relationships": {
                    "state": "empty_by_design",
                    "reason": "private_afterlife_relationship_reason_marker"
                  },
                  "agency": { "state": "populated" },
                  "progressionHistory": {
                    "state": "empty_by_design",
                    "reason": "private_afterlife_progression_reason_marker"
                  }
                }
              }
            }
          ]
        }
        """);
    }

    private async Task WriteAfterlifeChroniclesRawLeakFixtureAsync()
    {
        await _fs.WriteFileAtomicAsync(AfterlifeChronicleState.StatePath, """
        {
          "schemaVersion": 1,
          "lastInvalidChronicleUpdate": {
            "chronicleId": "hidden_invalid_update_marker"
          },
          "lastInvalidChronicleUpdateReason": "hidden_invalid_reason_marker",
          "chronicles": [
            {
              "chronicleId": "guardian_scene_mirror",
              "scopeType": "guardian_scene",
              "scopeId": "guardian_mirror",
              "displayName": "Зал зеркальной клятвы",
              "eventDescriptions": [
                "[Turn 4] Игрок впервые вошёл в зал отражений.",
                "hidden_chronicle_marker: GM-only archived event"
              ],
              "lastEventsDescription": "[Turn 5] Игрок услышал зов зеркал.",
              "persistentConsequences": [
                "Зал отражений запомнил голос игрока.",
                "secret_consequence_marker"
              ],
              "openThreads": [
                "Понять, почему зеркала зовут игрока.",
                "Не раскрывать игроку hidden_chronicle_marker"
              ],
              "participants": [
                { "actorId": "player_soul", "displayName": "Игрок", "actorType": "player_soul" },
                { "actorId": "guardian_mirror", "displayName": "Хранитель Зеркал", "actorType": "guardian" },
                { "actorId": "secret_participant_marker", "displayName": "secret_participant_marker", "visibility": "gm_only" },
                { "actorId": "moon_witness", "displayName": "moon_visible_to_player_false_marker", "visibleToPlayer": false },
                { "actorId": "silent_witness", "displayName": "silent_boolean_hidden_marker", "isHidden": true }
              ],
              "linkedActors": [
                { "actorId": "internal_scope_marker", "displayName": "internal_scope_marker", "isPlayerVisible": false },
                { "actorId": "closed_architect", "displayName": "closed_gm_only_marker", "gmOnly": true }
              ],
              "gmThoughtsSummary": "hidden_chronicle_marker",
              "secretPlan": "secret_chronicle_marker",
              "internalNotes": "internal_chronicle_marker",
              "_debug": "hidden_debug_marker",
              "lastUpdatedTurn": 5
            },
            {
              "chronicleId": "moon_witness_scene",
              "scopeType": "guardian_scene",
              "scopeId": "moon_witness",
              "displayName": "moon_visible_to_player_false_marker",
              "visibleToPlayer": false,
              "lastEventsDescription": "[Turn 6] moon_visible_to_player_false_marker sees a closed oath.",
              "lastUpdatedTurn": 6
            },
            {
              "chronicleId": "quiet_deal_scene",
              "scopeType": "guardian_scene",
              "scopeId": "quiet_deal",
              "displayName": "quiet_deal_boolean_secret_marker",
              "isSecret": true,
              "lastEventsDescription": "[Turn 7] quiet_deal_boolean_secret_marker stays behind the curtain.",
              "lastUpdatedTurn": 7
            }
          ]
        }
        """);
    }

    private async Task WriteAfterlifeThreatsDrilldownFixtureAsync()
    {
        await _fs.WriteFileAtomicAsync(AfterlifeActiveThreatState.StatePath, """
        {
          "schemaVersion": 1,
          "threats": [
            {
              "threatId": "threat_mirror_hunter",
              "displayName": "Охотник зеркального долга",
              "realm": "Chaos Sea",
              "scopeId": "player_soul",
              "intensity": 3,
              "visibleToPlayer": true,
              "threatArchetype": {
                "motivation": "vengeance",
                "method": "hunter"
              },
              "currentActivity": {
                "activityId": "activity_mirror_debt",
                "activityName": "Следит по отражениям",
                "activeState": "active",
                "description": "Долг следует за душой через трещины зеркал.",
                "narrativeSummary": "В отражениях появляется чужая рука."
              },
              "impactProfile": {
                "primaryTargetType": "player_soul",
                "primaryTargetName": "Test Soul",
                "primaryImpact": "strain",
                "baseImpact": 2
              },
              "ledger": [
                {
                  "summary": "Игрок увидел охотника в воде.",
                  "turn": 6
                }
              ],
              "gmThoughts": "hidden_threat_marker",
              "secretPlan": "hidden_threat_marker"
            },
            {
              "threatId": "hidden_threat_marker",
              "displayName": "hidden_threat_marker",
              "realm": "Chaos Sea",
              "scopeId": "player_soul",
              "intensity": 9,
              "visibleToPlayer": false,
              "threatArchetype": {
                "motivation": "secret",
                "method": "secret"
              },
              "currentActivity": {
                "activityId": "hidden_activity_marker",
                "activityName": "hidden_activity_marker",
                "activeState": "active",
                "description": "hidden_threat_marker"
              },
              "impactProfile": {
                "primaryTargetType": "player_soul",
                "primaryTargetName": "hidden_threat_marker",
                "primaryImpact": "hidden_threat_marker",
                "baseImpact": 9
              },
              "ledger": []
            }
          ]
        }
        """);
    }

    private static JsonObject CreateAcceptedUiItem(
        string itemId,
        string name,
        Action<JsonObject>? configure = null,
        params string[] populatedSections)
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot(itemId);
        item["name"] = name;
        item["description"] = $"Тестовый принятый предмет «{name}».";
        configure?.Invoke(item);
        foreach (var section in populatedSections)
        {
            item["materialization"]!["sections"]![section] = new JsonObject
            {
                ["state"] = "populated",
                ["reason"] = null
            };
        }

        MortalItemTestFixture.ResealCanonical(item);
        using var document = JsonDocument.Parse(item.ToJsonString());
        var issues = MortalItemMaterializationContract.Validate(
            document.RootElement,
            $"accepted UI fixture {itemId}",
            MortalItemMaterializationPhase.CanonicalPostSeal);
        if (issues.Count != 0)
            throw new InvalidOperationException(string.Join(" | ", issues.Select(issue => issue.Message)));
        return item;
    }

    private static JsonObject CreateAcceptedUiItemFromJson(
        string itemId,
        string json,
        params string[] populatedSections)
    {
        var semantic = JsonNode.Parse(json)?.AsObject() ??
                       throw new InvalidOperationException("Accepted UI fixture must be a JSON object.");
        var name = semantic["name"]?.GetValue<string>() ??
                   semantic["itemName"]?.GetValue<string>() ??
                   throw new InvalidOperationException("Accepted UI fixture requires a name.");
        return CreateAcceptedUiItem(
            itemId,
            name,
            item =>
            {
                foreach (var property in semantic)
                {
                    if (property.Key is "itemId" or "existedId" or "id" or "creationRef" or
                        "materialization" or "materializationReceipt")
                    {
                        continue;
                    }

                    item[property.Key] = property.Value?.DeepClone();
                }
            },
            populatedSections);
    }

    private async Task SeedInventoryEquipmentItemsAsync()
    {
        var sword = CreateAcceptedUiItem(
            "sword_1",
            "Кривой меч",
            item =>
            {
                item["type"] = "weapon";
                item["equipmentSlot"] = "MainHand";
            },
            "equipment");
        var helmet = CreateAcceptedUiItem(
            "helmet_1",
            "Железный шлем",
            item =>
            {
                item["type"] = "helmet";
                item["equipmentSlot"] = "Head";
            },
            "equipment");
        var torchOne = CreateAcceptedUiItem(
            "torch_1",
            "Факел",
            item =>
            {
                item["type"] = "utility";
                item["count"] = 2;
            });
        var torchTwo = CreateAcceptedUiItem(
            "torch_2",
            "Факел",
            item =>
            {
                item["type"] = "utility";
                item["count"] = 3;
            });
        var brokenBow = CreateAcceptedUiItem(
            "broken_bow_1",
            "Сломанный лук",
            item =>
            {
                item["type"] = "weapon";
                item["equipmentSlot"] = "MainHand";
                item["durability"] = "0%";
            },
            "equipment");
        var soulRelic = CreateAcceptedUiItem(
            "soul_relic_1",
            "Реликвия души",
            item =>
            {
                item["type"] = "soul_relic";
                item["relicId"] = "soul_relic_1";
                item["equipmentSlot"] = "Finger1";
            },
            "equipment");

        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/items.json",
            new JsonObject
            {
                ["equippedItems"] = new JsonObject
                {
                    ["Head"] = "helmet_1",
                    ["MainHand"] = null,
                    ["OffHand"] = null
                },
                ["items"] = new JsonArray(sword, helmet, torchOne, torchTwo, brokenBow, soulRelic)
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task SeedStorageTransportPromptDataAsync()
    {
        var sealedLetter = CreateAcceptedUiItemFromJson(
            "seal_letter_1",
            """
            { "name": "Запечатанное письмо", "type": "document", "count": 1 }
            """);
        var travelRation = CreateAcceptedUiItemFromJson(
            "travel_ration_1",
            """
            { "name": "Дорожный паёк", "type": "consumable", "count": 2 }
            """);
        var deskLetter = CreateAcceptedUiItemFromJson(
            "desk_letter_1",
            """
            { "name": "Письмо с печатью", "type": "document", "count": 1 }
            """);
        var saddlebag = CreateAcceptedUiItemFromJson(
            "saddlebag_1",
            """
            { "name": "Седельная сумка", "type": "container", "count": 1 }
            """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World"
        }
        """);
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/items.json",
            new JsonObject
            {
                ["equippedItems"] = new JsonObject(),
                ["items"] = new JsonArray(sealedLetter, travelRation)
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var storageLocation = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_valmont_room",
            "Покои виконта де Вальмонта",
            "visited");
        storageLocation["locationStorages"] = new JsonArray(
            MortalLocationTestFixture.CreateStorageMetadata(
                "storage_valmont_private_desk",
                "Приватный письменный стол",
                hasFullAccess: true,
                contents: new JsonArray(deskLetter)));
        storageLocation["materialization"]!["sections"]!["storageMetadata"] =
            new JsonObject
            {
                ["disposition"] = "populated",
                ["reason"] = null
            };
        MortalLocationTestFixture.ResealCanonicalLocation(storageLocation);
        var mapStorageLocation = storageLocation.DeepClone().AsObject();
        mapStorageLocation["locationStorages"]![0]!.AsObject().Remove("contents");
        await WriteCanonicalBrowserMortalLocationStateAsync(
            [mapStorageLocation],
            MortalLocationTestFixture.CreateCurrentProjection(storageLocation),
            [storageLocation]);
        await _fs.WriteFileAtomicAsync(
            "game_state/misc/vehicles.json",
            new JsonObject
            {
                ["vehicles"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["vehicleId"] = "vehicle_gray_horse",
                        ["name"] = "Серый конь",
                        ["inventory"] = new JsonArray(saddlebag)
                    }
                }
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task SeedInventoryItemDetailStateAsync()
    {
        var glove = CreateAcceptedUiItemFromJson(
            "runic_glove_1",
            """
            {
              "name": "Руническая перчатка",
              "description": "На тыльной стороне перчатки мерцает рунический контур.",
              "type": "Артефакт",
              "quality": "Rare",
              "rarity": "Rare",
              "weight": 0.3,
              "price": 450,
              "durability": "95%",
              "maxDurability": "100%",
              "equipmentSlot": "Hands",
              "group": "Аксессуары",
              "bonuses": ["Чувство магических потоков +2"],
              "effects": [{ "name": "Откликается на владельца" }],
              "structuredBonuses": [
                {
                  "bonusType": "Skill",
                  "target": "Чувство магических потоков",
                  "value": 2,
                  "valueType": "Flat",
                  "modifierType": "skill",
                  "source": "Руническая перчатка",
                  "summary": "Чувство магических потоков +2"
                }
              ],
              "combatEffect": {
                "actionName": "Рунный отклик",
                "isActivatedEffect": false,
                "effects": [
                  {
                    "effectType": "Damage",
                    "value": "0%",
                    "poiseDamage": "1%",
                    "targetType": "enemy",
                    "targetTypeDisplayName": "цель",
                    "effectDescription": "Сбивает концентрацию цели."
                  }
                ]
              },
              "customProperties": [
                {
                  "interactionType": "onUse",
                  "targetStateName": "магические следы",
                  "changeValue": "+1",
                  "description": "Подсвечивает свежие следы."
                }
              ],
              "specialProperties": ["Перчатка реагирует на владельца."],
              "lore": "Вышита тусклым золотом."
            }
            """,
            "mechanics",
            "equipment");
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/items.json",
            new JsonObject
            {
                ["equippedItems"] = new JsonObject { ["Hands"] = "runic_glove_1" },
                ["items"] = new JsonArray(glove)
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync("game_state/inventory/item_resources.json", """
        {
          "entries": [
            {
              "itemId": "runic_glove_1",
              "resource": 3,
              "maximumResource": 5,
              "resourceType": "заряды"
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/inventory/item_bonds.json", """
        {
          "entries": [
            {
              "itemId": "runic_glove_1",
              "ownerBondLevelCurrent": 12,
              "lastBondChangeReason": "Перчатка откликнулась на владельца.",
              "fateCards": [
                {
                  "name": "Память старого мага",
                  "description": "Открывает дополнительную подсказку при исследовании рун.",
                  "isUnlocked": true
                }
              ]
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/inventory/item_text_updates.json", """
        {
          "entries": [
            {
              "itemId": "runic_glove_1",
              "textContent": [
                "SIDE_TEXT_MARKER: внутри шва спрятана короткая инструкция."
              ]
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/npcs/item_journals.json", """
        {
          "entries": [
            {
              "itemId": "runic_glove_1",
              "journalEntries": [
                {
                  "event": "Пробуждение",
                  "description": "JOURNAL_MARKER: перчатка впервые отозвалась на владельца.",
                  "spiritVoice": "Тонкий голос просит найти серебряную нить.",
                  "magicalResonance": "Резонанс северной нити"
                }
              ]
            }
          ]
        }
        """);
    }

    private async Task SeedRichAfterlifeRelicArchiveDrilldownFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 3,
          "inkFeathers": {
            "current": 12,
            "total": 12
          },
          "soulRelics": {
            "stored": [
              {
                "relicId": "relic_memory_blade",
                "name": "Клинок Памяти",
                "rarity": "Rare",
                "slot": "mainHand",
                "compatibleSlots": ["mainHand", "offHand"],
                "description": "Память режет тьму.",
                "effectSummary": "Усиливает воспоминания о клятвах.",
                "tags": ["memory", "blade"],
                "gameplayStatus": { "equipped": false }
              }
            ],
            "equipped": [
              {
                "relicId": "relic_silent_helm",
                "name": "Шлем Тишины",
                "quality": "Legendary",
                "slot": "head",
                "description": "Шлем хранит тишину.",
                "gameplayStatus": { "equipped": true, "currentSlot": "head" }
              }
            ]
          },
          "afterlifeArchive": {
            "stored": [
              {
                "archiveId": "archive_lore_001",
                "entryType": "lore_fragment",
                "title": "Песнь Первого Маяка",
                "summary": "Фрагмент знания о первом свете.",
                "content": "Полный текст маяка держит путь через серое море.",
                "rarity": "Rare",
                "sourceLife": 3,
                "sourceKind": "codex",
                "sourceEntryId": "codex_first_lighthouse",
                "tags": ["lore", "memory"],
                "acquiredAtUtc": "2026-03-26T00:00:00Z"
              },
              {
                "archiveId": "archive_secret_002",
                "entryType": "secret_record",
                "title": "Запечатанный договор",
                "summary": "Эта запись уже занята другим действием.",
                "content": "Тайный договор не должен попасть в первый detail.",
                "rarity": "Uncommon",
                "sourceLife": 2,
                "sourceKind": "codex",
                "sourceEntryId": "codex_sealed_pact",
                "acquiredAtUtc": "2026-03-25T00:00:00Z",
                "reservation": {
                  "reservationKind": "project_fuel",
                  "requestId": "archive_fuel_existing",
                  "guardianId": "guardian_other",
                  "guardianName": "Другой Хранитель",
                  "targetProjectId": "other_project",
                  "targetProjectName": "Чужой проект",
                  "createdAtTurn": 39,
                  "createdAtUtc": "2026-03-25T00:00:00Z"
                }
              }
            ],
            "actionReceipts": []
          },
          "afterlifeArchiveUpdates": [],
          "archiveActionResolutions": []
        }
        """);

        await _fs.WriteFileAtomicAsync(AfterlifeArchiveCandidateService.ManifestPath, """
        {
          "sourceLife": 3,
          "lastExtractedAt": "2026-03-27T00:00:00Z",
          "candidates": [
            {
              "candidateId": "candidate_mayak",
              "sourceKind": "codex",
              "sourceEntryId": "codex_mayak_song",
              "sourceLife": 3,
              "proposedEntryType": "lore_fragment",
              "title": "Песня маяка",
              "summary": "Кандидат хранит свет.",
              "content": "Кандидат хранит свет, чтобы Архив мог решить его судьбу.",
              "rarity": "Rare",
              "status": "pending",
              "discoveredAt": "2026-03-27T00:00:00Z",
              "tags": ["lore", "light"]
            },
            {
              "candidateId": "candidate_secret",
              "sourceKind": "codex",
              "sourceEntryId": "codex_secret_deal",
              "sourceLife": 3,
              "proposedEntryType": "secret_record",
              "title": "Тайный договор",
              "summary": "Второй кандидат для проверки фокуса.",
              "content": "Тайный договор скрыт в стороне.",
              "rarity": "Uncommon",
              "status": "pending",
              "discoveredAt": "2026-03-27T00:00:00Z",
              "tags": ["secret"]
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "domain": "memory",
              "relationshipData": {
                "currentReputation": 80
              }
            },
            {
              "guardianId": "guardian_wary",
              "canonicalName": "Недоверчивый Страж",
              "domain": "silence",
              "relationshipData": {
                "currentReputation": 49
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_azalia",
              "project": {
                "projectId": "project_forge_song",
                "projectName": "Песнь кузни",
                "projectType": "lore_research",
                "projectTier": "minor",
                "projectMode": "supportive",
                "progress": 2,
                "target": 5,
                "status": "active"
              }
            },
            {
              "guardianId": "guardian_wary",
              "project": {
                "projectId": "project_hidden",
                "projectName": "Скрытый проект",
                "projectType": "secret"
              }
            }
          ]
        }
        """);
    }

    private static void AssertNoAfterlifeIssue1064TechnicalLeak(ExplorerCommandResult result)
    {
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var payload = SerializeResult(result);
        foreach (var forbidden in new[]
                 {
                     ".json", "game_state/", "DTO", "API", "endpoint", "debug", "exception", "UiRawJsonBlock", "pending_"
                 })
        {
            Assert.DoesNotContain(forbidden, payload, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertNoAfterlifeReadOnlyDetailTechnicalLeak(ExplorerCommandResult result)
    {
        AssertNoAfterlifeIssue1064TechnicalLeak(result);
    }

    public static IEnumerable<object[]> PlayerDefaultReadOnlyCommands()
    {
        foreach (var descriptor in ExplorerCommandCatalog.Descriptors)
        {
            var metadata = BrowserPlayerCommandMenuBuilder.GetCoverageMetadata(descriptor);
            if (descriptor.MutationMode != ExplorerCommandMutationMode.ReadOnly ||
                !string.Equals(metadata.Surface, "player-default", StringComparison.OrdinalIgnoreCase) ||
                !ExplorerCommandMigrationRegistry.IsBrowserExecutable(descriptor.BrowserStatus))
            {
                continue;
            }

            yield return [descriptor.Id, descriptor.PrimaryAlias, descriptor.Group];
        }
    }

    public static IEnumerable<object[]> PlayerDefaultMutatingCommands()
    {
        foreach (var descriptor in ExplorerCommandCatalog.Descriptors)
        {
            var metadata = BrowserPlayerCommandMenuBuilder.GetCoverageMetadata(descriptor);
            if (descriptor.MutationMode != ExplorerCommandMutationMode.LocalTurn ||
                !string.Equals(metadata.Surface, "player-default", StringComparison.OrdinalIgnoreCase) ||
                !ExplorerCommandMigrationRegistry.IsBrowserExecutable(descriptor.BrowserStatus))
            {
                continue;
            }

            yield return [descriptor.Id, descriptor.PrimaryAlias, descriptor.Group];
        }
    }

    private Task PreparePlayerDefaultCommandAuditFilesAsync(
        string commandId,
        ExplorerCommandGroup group)
    {
        var profileKey = group == ExplorerCommandGroup.MortalWorld &&
                         commandId is "inventory" or "books"
            ? $"player-default:{group}:{commandId}"
            : $"player-default:{group}";

        return _seedFixture.PrepareSeededRootAsync(
            profileKey,
            _rootPath,
            () => SeedPlayerDefaultCommandAuditFilesAsync(commandId, group));
    }

    private Task PrepareRepresentativeMigratedCommandFilesAsync()
    {
        return _seedFixture.PrepareSeededRootAsync(
            "representative-migrated-commands",
            _rootPath,
            async () =>
            {
                await SeedUniversalMetaFilesAsync();
                await SeedMortalFilesAsync();
                await SeedChaosSeaFilesAsync();
                await SeedShiningAbodeFilesAsync();
                await SeedAfterlifeCombatAndEntityFilesAsync();
            });
    }

    private Task PrepareMortalReadOnlySummaryFilesAsync()
    {
        return _seedFixture.PrepareSeededRootAsync(
            "mortal-read-only-summaries",
            _rootPath,
            SeedCanonicalMortalSummaryFilesAsync);
    }

    private Task PrepareLifecycleAndLocalTurnFilesAsync()
    {
        return _seedFixture.PrepareSeededRootAsync(
            "lifecycle-and-local-turn-commands",
            _rootPath,
            async () =>
            {
                await SeedUniversalMetaFilesAsync();
                await SeedMortalFilesAsync();
                await SeedChaosSeaFilesAsync();
                await SeedAfterlifeCombatAndEntityFilesAsync();
            });
    }

    private Task PrepareMigratedMortalFilesAsync()
    {
        return _seedFixture.PrepareSeededRootAsync(
            "migrated-mortal-commands",
            _rootPath,
            SeedMortalFilesAsync);
    }

    private Task PrepareRichMortalReferenceDetailFilesAsync()
    {
        return _seedFixture.PrepareSeededRootAsync(
            "rich-mortal-reference-details",
            _rootPath,
            SeedRichMortalReferenceDetailFilesAsync);
    }

    private Task PrepareIssue1124AfterlifeFilesAsync()
    {
        return _seedFixture.PrepareSeededRootAsync(
            "issue-1124-afterlife-details",
            _rootPath,
            async () =>
            {
                await SeedAfterlifeCombatAndEntityFilesAsync();
                await WriteAfterlifeThreatsDrilldownFixtureAsync();
                await WriteAfterlifeChroniclesRawLeakFixtureAsync();
            });
    }

    private Task PrepareMigratedAfterlifeFilesAsync()
    {
        return _seedFixture.PrepareSeededRootAsync(
            "migrated-afterlife-commands",
            _rootPath,
            SeedAfterlifeCombatAndEntityFilesAsync);
    }

    private Task PrepareRichAfterlifeRelicArchiveFilesAsync()
    {
        return _seedFixture.PrepareSeededRootAsync(
            "rich-afterlife-relic-archive",
            _rootPath,
            SeedRichAfterlifeRelicArchiveDrilldownFilesAsync);
    }

    private Task PrepareMigratedUniversalFilesAsync()
    {
        return _seedFixture.PrepareSeededRootAsync(
            "migrated-universal-commands",
            _rootPath,
            SeedUniversalMetaFilesAsync);
    }

    private Task PrepareMigratedShiningFilesAsync()
    {
        return _seedFixture.PrepareSeededRootAsync(
            "migrated-shining-commands",
            _rootPath,
            SeedShiningAbodeFilesAsync);
    }

    private Task PrepareMigratedChaosSeaFilesAsync()
    {
        return _seedFixture.PrepareSeededRootAsync(
            "migrated-chaos-sea-commands",
            _rootPath,
            SeedChaosSeaFilesAsync);
    }

    private Task PrepareRichChaosSeaGuardianFilesAsync()
    {
        return _seedFixture.PrepareSeededRootAsync(
            "rich-chaos-sea-guardian-details",
            _rootPath,
            SeedRichChaosSeaGuardianAbodeDrilldownFilesAsync);
    }

    private Task PrepareMortalActionPromptFilesAsync()
    {
        return _seedFixture.PrepareSeededRootAsync(
            "mortal-action-prompts",
            _rootPath,
            async () =>
            {
                await SeedUniversalMetaFilesAsync();
                await SeedMortalActionPromptDataAsync();
            });
    }

    private async Task SeedPlayerDefaultCommandAuditFilesAsync(string commandId, ExplorerCommandGroup group)
    {
        switch (group)
        {
            case ExplorerCommandGroup.MortalWorld:
                await SeedUniversalMetaFilesAsync();
                await SeedMortalFilesAsync();
                await SeedCanonicalMortalSummaryFilesAsync();
                await SeedRichMortalReferenceDetailFilesAsync();
                await SeedRichMortalCombatFilesAsync();
                await SeedRichMortalWorldNewsFilesAsync();
                await SeedRichMortalPlayerInteractionsFilesAsync();
                await SeedRichNpcDrilldownFilesAsync();
                if (string.Equals(commandId, "inventory", StringComparison.OrdinalIgnoreCase))
                    await SeedInventoryEquipmentItemsAsync();
                if (string.Equals(commandId, "books", StringComparison.OrdinalIgnoreCase))
                    await SeedBooksReadingFlowStateAsync();
                break;
            case ExplorerCommandGroup.ChaosSea:
                await SeedUniversalMetaFilesAsync();
                await SeedChaosSeaFilesAsync();
                await SeedRichChaosSeaGuardianAbodeDrilldownFilesAsync();
                break;
            case ExplorerCommandGroup.ShiningAbode:
                await SeedShiningAbodeFilesAsync();
                break;
            case ExplorerCommandGroup.AfterlifeCombatAndEntities:
                await SeedAfterlifeCombatAndEntityFilesAsync();
                break;
            case ExplorerCommandGroup.SarefStory:
            case ExplorerCommandGroup.UniversalMeta:
                await SeedUniversalMetaFilesAsync();
                await SeedRichAfterlifeRelicArchiveDrilldownFilesAsync();
                await SeedChaosSeaFilesAsync();
                await SeedShiningAbodeFilesAsync();
                await SeedAfterlifeCombatAndEntityFilesAsync();
                break;
            default:
                await SeedUniversalMetaFilesAsync();
                break;
        }
    }

    private static List<string> CollectPlayerFacingOutputViolations(
        ExplorerCommandResult result,
        bool allowRequiresInput = false)
    {
        var violations = new List<string>();
        var validState = result.State == CommandExecutionState.Completed ||
                         (allowRequiresInput && result.State is CommandExecutionState.RequiresInput or CommandExecutionState.Pending or CommandExecutionState.Blocked);
        if (!validState)
            violations.Add(allowRequiresInput
                ? $"state is {result.State}, expected Completed/RequiresInput/Pending/Blocked"
                : $"state is {result.State}, expected Completed");
        if (result.Blocks.Count == 0)
            violations.Add("result has no UI blocks");

        var rawBlocks = result.Blocks.OfType<UiRawJsonBlock>().Select(static block => block.Title).ToList();
        foreach (var title in rawBlocks)
            violations.Add($"default output contains raw JSON block: {title}");

        var text = CollectBlockText(result.Blocks) + "\n" + CollectPromptAndNotificationText(result);
        if (string.IsNullOrWhiteSpace(text))
            violations.Add("default output has no readable text");

        foreach (var marker in new[]
                 {
                     "Подробные сведения доступны в расширенном режиме",
                     "UiRawJsonBlock",
                     "JsonObject",
                     "JsonArray",
                     "JsonValue",
                     "game_state/",
                     ".json",
                     "image_prompt",
                     "factionColor",
                     "gm_thoughts",
                     "currentRealm",
                     "Realm",
                     "DTO",
                     "API",
                     "endpoint",
                     "exception",
                     "console-bound",
                     "route tag",
                     "pending",
                     "interactive/write",
                     "Браузерная команда",
                     "Браузерный протокол",
                     "protocol",
                     "contract",
                     "destructive",
                     "Артефакты протокола"
                 })
        {
            if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
                violations.Add($"default visible text leaks technical marker: {marker}");
        }

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(line, "деталь", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(line, "detail", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add("default visible text contains generic detail label");
                break;
            }
        }

        foreach (var violation in CollectFlattenedStructuredDetailViolations(result.Blocks))
            violations.Add("default visible text flattens structured details: " + violation);

        return violations;
    }

    private static void AssertNoFlattenedStructuredDetails(ExplorerCommandResult result)
    {
        var violations = CollectFlattenedStructuredDetailViolations(result.Blocks);
        Assert.True(
            violations.Count == 0,
            "Browser output flattens structured data into generic detail rows:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    private static void AssertNoGenericDetailsTables(ExplorerCommandResult result)
    {
        var offenders = result.Blocks
            .SelectMany(EnumerateTables)
            .Where(table => table.Columns.Any(IsGenericDetailKey))
            .Select(table => $"{table.Title}: {string.Join(", ", table.Columns)}")
            .ToList();
        Assert.True(
            offenders.Count == 0,
            "Browser output uses generic details table columns:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    private static IEnumerable<UiTableBlock> EnumerateTables(UiBlock block)
    {
        if (block is UiTableBlock table)
        {
            yield return table;
            yield break;
        }

        if (block is UiEntityDossierBlock dossier)
        {
            foreach (var section in dossier.Sections)
            foreach (var child in section.Blocks)
            foreach (var childTable in EnumerateTables(child))
                yield return childTable;
            yield break;
        }

        if (block is UiPanelBlock panel)
        {
            foreach (var child in panel.Blocks)
            foreach (var childTable in EnumerateTables(child))
                yield return childTable;
        }
    }

    private static IEnumerable<UiKeyValueGridBlock> EnumerateKeyValueGrids(UiBlock block)
    {
        if (block is UiKeyValueGridBlock grid)
        {
            yield return grid;
            yield break;
        }

        if (block is UiEntityDossierBlock dossier)
        {
            foreach (var section in dossier.Sections)
            foreach (var child in section.Blocks)
            foreach (var childGrid in EnumerateKeyValueGrids(child))
                yield return childGrid;
            yield break;
        }

        if (block is UiPanelBlock panel)
        {
            foreach (var child in panel.Blocks)
            foreach (var childGrid in EnumerateKeyValueGrids(child))
                yield return childGrid;
        }
    }

    private static IEnumerable<UiPanelBlock> EnumeratePanels(UiBlock block)
    {
        if (block is not UiPanelBlock panel)
            yield break;

        yield return panel;
        foreach (var child in panel.Blocks)
        foreach (var childPanel in EnumeratePanels(child))
            yield return childPanel;
    }

    private static IEnumerable<UiEntityDossierBlock> EnumerateEntityDossiers(UiBlock block)
    {
        if (block is UiEntityDossierBlock dossier)
        {
            yield return dossier;
            foreach (var section in dossier.Sections)
            foreach (var child in section.Blocks)
            foreach (var childDossier in EnumerateEntityDossiers(child))
                yield return childDossier;
            yield break;
        }

        if (block is not UiPanelBlock panel)
            yield break;

        foreach (var child in panel.Blocks)
        foreach (var childDossier in EnumerateEntityDossiers(child))
            yield return childDossier;
    }

    private static List<string> CollectFlattenedStructuredDetailViolations(IEnumerable<UiBlock> blocks)
    {
        var violations = new List<string>();
        foreach (var block in blocks)
            CollectFlattenedStructuredDetailViolations(block, violations, "root");
        return violations;
    }

    private static void CollectFlattenedStructuredDetailViolations(
        UiBlock block,
        List<string> violations,
        string path)
    {
        switch (block)
        {
            case UiPanelBlock panel:
                for (var i = 0; i < panel.Blocks.Count; i++)
                    CollectFlattenedStructuredDetailViolations(panel.Blocks[i], violations, $"{path}/{panel.Title}[{i}]");
                break;
            case UiEntityDossierBlock dossier:
                for (var i = 0; i < dossier.Sections.Count; i++)
                {
                    var section = dossier.Sections[i];
                    for (var j = 0; j < section.Blocks.Count; j++)
                        CollectFlattenedStructuredDetailViolations(section.Blocks[j], violations, $"{path}/{dossier.Title}/{section.Title}[{i}:{j}]");
                }

                break;
            case UiKeyValueGridBlock grid:
                foreach (var item in grid.Items)
                {
                    if (!IsGenericDetailKey(item.Key) || !LooksLikeFlattenedStructuredValue(item.Value))
                        continue;

                    violations.Add($"{path}: key '{item.Key}' contains flattened structured value '{TrimForAssertion(item.Value)}'");
                }

                break;
            case UiTableBlock table:
                for (var column = 0; column < table.Columns.Count; column++)
                {
                    if (!IsGenericDetailKey(table.Columns[column]))
                        continue;

                    for (var row = 0; row < table.Rows.Count; row++)
                    {
                        if (column >= table.Rows[row].Cells.Count ||
                            !LooksLikeFlattenedStructuredValue(table.Rows[row].Cells[column]))
                        {
                            continue;
                        }

                        violations.Add($"{path}/{table.Title}: column '{table.Columns[column]}' row {row + 1} contains flattened structured value '{TrimForAssertion(table.Rows[row].Cells[column])}'");
                    }
                }

                break;
        }
    }

    private static bool IsGenericDetailKey(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        return normalized is "подробности" or "детали" or "detail" or "details";
    }

    private static bool LooksLikeFlattenedStructuredValue(string value)
    {
        var clean = value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (string.IsNullOrWhiteSpace(clean) || clean.Contains('\n', StringComparison.Ordinal))
            return false;

        return CountOccurrences(clean, ";") >= 2 && CountOccurrences(clean, ":") >= 2;
    }

    private static string TrimForAssertion(string value)
    {
        var clean = value.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return clean.Length <= 180 ? clean : clean[..180] + "...";
    }

    private static string CollectPromptAndNotificationText(ExplorerCommandResult result)
    {
        var parts = new List<string>();
        foreach (var prompt in result.Prompts)
        {
            parts.Add(prompt.Prompt);
            switch (prompt)
            {
                case UiTextInputPrompt textInput:
                    parts.Add(textInput.Placeholder);
                    break;
                case UiSelectionPrompt selection:
                    foreach (var option in selection.Options)
                    {
                        parts.Add(option.Label);
                        parts.Add(option.Description);
                    }
                    break;
            }
        }

        foreach (var notification in result.Notifications)
        {
            parts.Add(notification.Title);
            parts.Add(notification.Message);
        }

        return string.Join("\n", parts);
    }

    private static string CollectBlockText(IEnumerable<UiBlock> blocks)
    {
        var parts = new List<string>();
        foreach (var block in blocks)
            CollectBlockText(block, parts);
        return string.Join("\n", parts);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string SerializeResult(ExplorerCommandResult result) =>
        JsonSerializer.Serialize(result, JsonOptions);

    private static void AssertContainsGuardianPoliticsRawState(ExplorerCommandResult result)
    {
        var raw = Assert.Single(result.Blocks.OfType<UiRawJsonBlock>());
        var rawText = raw.Json?.ToJsonString(JsonOptions) ?? string.Empty;
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(ChaosSeaGuardianPoliticsState.StatePath, raw.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hiddenRelations", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secretProjects", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("internalMotivations", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("system_saref_shadow", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("system_invisible_false_guardian", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secret_project_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("is_player_visible_false_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("internal_motivation_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertContainsAfterlifeProfilesRawState(ExplorerCommandResult result)
    {
        var raw = Assert.Single(result.Blocks.OfType<UiRawJsonBlock>());
        var rawText = raw.Json?.ToJsonString(JsonOptions) ?? string.Empty;
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(AfterlifeEntityProfileState.StatePath, raw.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_actor_motivation_marker", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_activity_motivation_marker", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_fate_card_marker", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_card_story_marker", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_concealed_truth_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_mask_directive_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_saref_agent_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertContainsAfterlifeChroniclesRawState(ExplorerCommandResult result)
    {
        var raw = Assert.Single(result.Blocks.OfType<UiRawJsonBlock>());
        var rawText = raw.Json?.ToJsonString(JsonOptions) ?? string.Empty;
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(AfterlifeChronicleState.StatePath, raw.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gmThoughtsSummary", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lastInvalidChronicleUpdate", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_chronicle_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secret_participant_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("internal_scope_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertContainsShiningPoliticsRawState(ExplorerCommandResult result)
    {
        var rawBlocks = result.Blocks.OfType<UiRawJsonBlock>().ToList();
        var raw = Assert.Single(rawBlocks, static block =>
            block.Title.Contains(ShiningAbodeState.StatePath, StringComparison.OrdinalIgnoreCase));
        var rawText = raw.Json?.ToJsonString(JsonOptions) ?? string.Empty;
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("strategicMemory", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resourceLedger", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_chronicle_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_strategy_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_ledger_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    private static void CollectBlockText(UiBlock block, List<string> parts)
    {
        switch (block)
        {
            case UiTextBlock text:
                parts.Add(text.Text);
                break;
            case UiPanelBlock panel:
                parts.Add(panel.Title);
                foreach (var child in panel.Blocks)
                    CollectBlockText(child, parts);
                break;
            case UiEntityDossierBlock dossier:
                parts.Add(dossier.Title);
                parts.Add(dossier.Subtitle);
                parts.Add(dossier.Summary);
                parts.AddRange(dossier.Badges.Select(static badge => badge.Label));
                CollectEntityFacts(dossier.Facts, parts);
                CollectEntityMetrics(dossier.Metrics, parts);
                CollectEntityHints(dossier.Hints, parts);
                parts.AddRange(dossier.List);
                foreach (var card in dossier.Cards)
                    CollectEntityCardText(card, parts);
                if (dossier.Media != null)
                {
                    parts.Add(dossier.Media.Title);
                    parts.Add(dossier.Media.AltText);
                }
                foreach (var section in dossier.Sections)
                {
                    parts.Add(section.Title);
                    parts.Add(section.Summary);
                    parts.Add(section.CollectionLabel);
                    CollectEntityFacts(section.Facts, parts);
                    CollectEntityMetrics(section.Metrics, parts);
                    CollectEntityHints(section.Hints, parts);
                    parts.AddRange(section.List);
                    foreach (var card in section.Cards)
                        CollectEntityCardText(card, parts);
                    foreach (var child in section.Blocks)
                        CollectBlockText(child, parts);
                }
                break;
            case UiTableBlock table:
                parts.Add(table.Title);
                parts.AddRange(table.Columns);
                parts.AddRange(table.Rows.SelectMany(static row => row.Cells));
                break;
            case UiListBlock list:
                parts.AddRange(list.Items);
                break;
            case UiKeyValueGridBlock grid:
                parts.AddRange(grid.Items.SelectMany(static item => new[] { item.Key, item.Value }));
                break;
            case UiMessageBlock message:
                parts.Add(message.Title);
                parts.Add(message.Message);
                break;
            case UiRawJsonBlock raw:
                parts.Add(raw.Title);
                break;
        }
    }

    private static void CollectEntityCardText(UiEntityCard card, List<string> parts)
    {
        parts.Add(card.Title);
        parts.Add(card.Subtitle);
        parts.Add(card.Summary);
        parts.AddRange(card.Badges.Select(static badge => badge.Label));
        CollectEntityFacts(card.Facts, parts);
        CollectEntityMetrics(card.Metrics, parts);
        CollectEntityHints(card.Hints, parts);
        parts.AddRange(card.List);
        if (card.Media != null)
        {
            parts.Add(card.Media.Title);
            parts.Add(card.Media.AltText);
        }

        foreach (var child in card.Nested)
            CollectEntityCardText(child, parts);
        foreach (var child in card.Cards)
            CollectEntityCardText(child, parts);
    }

    private static IEnumerable<UiEntityFact> EnumerateCardFacts(UiEntityCard card)
    {
        foreach (var fact in card.Facts)
            yield return fact;
        foreach (var child in card.Nested)
        foreach (var fact in EnumerateCardFacts(child))
            yield return fact;
        foreach (var child in card.Cards)
        foreach (var fact in EnumerateCardFacts(child))
            yield return fact;
    }

    private static void CollectEntityFacts(IEnumerable<UiEntityFact> facts, List<string> parts)
    {
        foreach (var fact in facts)
        {
            parts.Add(fact.Label);
            parts.Add(fact.Value);
        }
    }

    private static void CollectEntityMetrics(IEnumerable<UiEntityMetric> metrics, List<string> parts)
    {
        foreach (var metric in metrics)
        {
            parts.Add(metric.Label);
            parts.Add(metric.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            parts.Add(metric.Max.ToString(System.Globalization.CultureInfo.InvariantCulture));
            parts.Add(metric.Note);
        }
    }

    private static void CollectEntityHints(IEnumerable<UiEntityHint> hints, List<string> parts)
    {
        foreach (var hint in hints)
        {
            parts.Add(hint.Title);
            parts.Add(hint.Text);
        }
    }

    private async Task<ExplorerCommandResult> BuildDirectMigratedResultAsync(string command, bool advancedEnabled = false)
    {
        await _stateManager.RefreshGameStateAsync();
        if (string.Equals(command, "/help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "/помощь", StringComparison.OrdinalIgnoreCase))
        {
            var state = _stateManager.CurrentState;
            return ExplorerHelpCommandResultBuilder.Build(new ExplorerHelpCommandContext
            {
                Command = command,
                Title = new LocalizationManager().T("help"),
                IsChaosSea = state.IsInChaosSea,
                IsShiningAbode = state.IsInShiningAbode,
                IsPendingShiningAbodeBootstrap = state.IsInShiningAbodePendingBootstrap,
                CanReenterShiningAbode = state.CanReenterShiningAbode
            });
        }

        if (ExplorerUniversalMetaCommandResultBuilder.CanBuild(command))
        {
            var universal = await ExplorerUniversalMetaCommandResultBuilder.TryBuildAsync(
                command,
                _stateManager,
                _fs,
                new LocalizationManager());
            if (universal != null)
                return universal;
        }

        if (ExplorerMortalWorldCommandResultBuilder.CanBuild(command))
        {
            var mortal = await ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(command, _stateManager, _fs);
            if (mortal != null)
                return mortal;
        }

        if (ExplorerChaosSeaCommandResultBuilder.CanBuild(command))
        {
            var chaos = await ExplorerChaosSeaCommandResultBuilder.TryBuildAsync(command, _stateManager, _fs, advancedEnabled);
            if (chaos != null)
                return chaos;
        }

        if (ExplorerShiningAbodeCommandResultBuilder.CanBuild(command))
        {
            var shining = await ExplorerShiningAbodeCommandResultBuilder.TryBuildAsync(command, _stateManager, _fs, advancedEnabled);
            if (shining != null)
                return shining;
        }

        if (ExplorerAfterlifeCombatCommandResultBuilder.CanBuild(command))
        {
            var afterlife = await ExplorerAfterlifeCombatCommandResultBuilder.TryBuildAsync(command, _stateManager, _fs, advancedEnabled);
            if (afterlife != null)
                return afterlife;
        }

        if (ExplorerLifecycleLocalTurnCommandResultBuilder.CanBuild(command))
        {
            var lifecycle = await ExplorerLifecycleLocalTurnCommandResultBuilder.TryBuildAsync(
                command,
                _stateManager,
                _fs,
                _validationService,
                advancedEnabled);
            if (lifecycle != null)
                return lifecycle;
        }

        throw new InvalidOperationException($"No direct DTO builder for migrated command {command}.");
    }

    private static JsonNode ToJsonNode(ExplorerCommandResult result) =>
        JsonSerializer.SerializeToNode(result, JsonOptions)!;

    private static ExplorerCommandResult ApplyExpectedDefaultPlayerSurface(ExplorerCommandResult result, string command)
    {
        var descriptor = ExplorerCommandCatalog.FindByAlias(command);
        if (descriptor == null)
            return result;

        var metadata = BrowserPlayerCommandMenuBuilder.GetCoverageMetadata(descriptor);
        if (!string.Equals(metadata.Surface, "player-default", StringComparison.OrdinalIgnoreCase))
            return result;

        return new ExplorerCommandResult
        {
            Command = result.Command,
            State = result.State,
            Blocks = RemoveRawJsonBlocksForExpectation(result.Blocks),
            Actions = result.Actions,
            Prompts = result.Prompts,
            Notifications = result.Notifications,
            InteractiveSession = result.InteractiveSession
        };
    }

    private static List<UiBlock> RemoveRawJsonBlocksForExpectation(IEnumerable<UiBlock> blocks)
    {
        var filtered = new List<UiBlock>();
        foreach (var block in blocks)
        {
            switch (block)
            {
                case UiRawJsonBlock:
                    continue;
                case UiPanelBlock panel:
                    filtered.Add(new UiPanelBlock
                    {
                        Title = panel.Title,
                        Blocks = RemoveRawJsonBlocksForExpectation(panel.Blocks)
                    });
                    break;
                default:
                    filtered.Add(block);
                    break;
            }
        }

        return filtered;
    }

    private static ExplorerCommandResult WithoutInteractiveSession(ExplorerCommandResult result) => new()
    {
        Command = result.Command,
        State = result.State,
        Blocks = result.Blocks,
        Actions = result.Actions,
        Prompts = result.Prompts,
        Notifications = result.Notifications,
        InteractiveSession = null
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
