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

public sealed class ExplorerWebCommandServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ExplorerWebCommandService _service;
    private readonly ValidationService _validationService;

    public ExplorerWebCommandServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-web-command-service-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
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
        Assert.Contains(result.Blocks, static block => block is UiTableBlock table && table.Columns.Contains("Описание"));
    }

    [Fact]
    public async Task ExecuteAsync_HelpInAfterlife_IncludesMemorySceneCommand()
    {
        await SeedUniversalMetaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/help"));

        var text = CollectBlockText(result.Blocks);
        Assert.Contains("/воспоминание", text, StringComparison.Ordinal);
        Assert.Contains("/воспоминание_начать", text, StringComparison.Ordinal);
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
        Assert.Empty(result.Actions);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Торек Молотобой", text, StringComparison.Ordinal);
        Assert.Contains("Впечатляющая решительность", text, StringComparison.Ordinal);
        Assert.Contains("2 записи", text, StringComparison.Ordinal);
        Assert.Contains("известные заметки", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Данные ещё не созданы", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/npc_talk", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/npc_trade", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("npc_journals.json", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pending_turn_snapshot", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/квесты", "Печать с крыльями")]
    [InlineData("/навыки", "Чувство магических потоков")]
    [InlineData("/новости_мира", "Письмо появилось ночью")]
    [InlineData("/чужие_нити", "Лунный претендент")]
    [InlineData("/погода", "08:15")]
    [InlineData("/транспорт", "Серый конь")]
    [InlineData("/эффекты", "Магический резонанс")]
    [InlineData("/бой", "Теневой посыльный")]
    [InlineData("/доступ_к_хранилищам", "Приватный письменный стол")]
    [InlineData("/взаимодействия", "странной печати")]
    public async Task ExecuteAsync_MortalReadOnlySummaries_ReadCanonicalStateKeys(string command, string expectedText)
    {
        await SeedCanonicalMortalSummaryFilesAsync();

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
        Assert.Contains(result.Actions, action =>
            action.Label.Contains("Магический резонанс", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/эффекты эффект resonance_1", StringComparison.OrdinalIgnoreCase) &&
            action.Style == UiActionStyle.Secondary &&
            action.RequiresConfirmation == false);
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
        Assert.Contains("Структурные бонусы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тип бонуса", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Восприятие", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Боевые эффекты", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Резонансный толчок", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сбивает концентрацию цели.", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Actions, action =>
            action.Label.Contains("Назад", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/эффекты", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
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
        Assert.DoesNotContain("entryId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/combat", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WorldNewsOverview_ExposesEventFlagAndProgressionDrilldownActions()
    {
        await SeedRichMortalWorldNewsFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/новости_мира"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Новости мира", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Мировые события", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Угрозы локаций", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Карманники у ворот", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Активности НПС", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Мира Ключница", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Проекты фракций", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ночные патрули", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Флаги мира", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Прогресс мира", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Беспорядки у Северных ворот", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Праздник стих после тревоги", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Дорога к Серебряному броду", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/world", text, StringComparison.OrdinalIgnoreCase);

        var eventAction = Assert.Single(result.Actions, static action => action.Id == "world-news-event-riots_at_gate");
        Assert.Equal("/новости_мира событие riots_at_gate", eventAction.Command);
        Assert.Contains("Открыть событие", eventAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Беспорядки у Северных ворот", eventAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, eventAction.Style);
        Assert.False(eventAction.RequiresConfirmation);

        var flagAction = Assert.Single(result.Actions, static action => action.Id == "world-news-flag-festival_quiet");
        Assert.Equal("/новости_мира флаг festival_quiet", flagAction.Command);
        Assert.Contains("Осмотреть флаг", flagAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Праздник стих после тревоги", flagAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, flagAction.Style);
        Assert.False(flagAction.RequiresConfirmation);

        var progressionAction = Assert.Single(result.Actions, static action => action.Id == "world-news-progression-road_silverford");
        Assert.Equal("/новости_мира прогресс road_silverford", progressionAction.Command);
        Assert.Contains("Открыть прогресс", progressionAction.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Дорога к Серебряному броду", progressionAction.Label, StringComparison.OrdinalIgnoreCase);
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
        Assert.DoesNotContain("eventId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("worldEventsLog", payload, StringComparison.OrdinalIgnoreCase);
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
        Assert.DoesNotContain("argument_at_ferry", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("interactionId", payload, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("player_mara", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Серебряный ключ", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("покрыт знаками янтарной башни", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UpdateInventory", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UpdateInventory", payload, StringComparison.OrdinalIgnoreCase);
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
        await SeedRichMortalReferenceDetailFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(expectedLabelText, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Подробно", text, StringComparison.OrdinalIgnoreCase);

        var action = Assert.Single(result.Actions, action => action.Id == expectedActionId);
        Assert.Equal(expectedDetailCommand, action.Command);
        Assert.Contains(expectedLabelText, action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, action.Style);
        Assert.False(action.RequiresConfirmation);
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
        await SeedRichMortalReferenceDetailFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);
        Assert.Contains(expectedTitle, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedDetail, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
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
              "scalingCharacteristic": "persuasion"
            }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/skills skill skill_salon_pressure"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Масштабирование", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Убеждение", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("persuasion", text, StringComparison.OrdinalIgnoreCase);
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
        var table = Assert.Single(result.Blocks.OfType<UiTableBlock>(), static block => block.Title == "Транспорт");
        var row = Assert.Single(table.Rows);
        Assert.Equal("Серый конь", row.Cells[0]);
        Assert.Contains("Ездовое животное", row.Cells[1], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Активен", row.Cells[2], StringComparison.OrdinalIgnoreCase);

        var tableText = string.Join("\n", table.Columns.Concat(table.Rows.SelectMany(static item => item.Cells)));
        Assert.DoesNotContain("mount", tableText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("active", tableText, StringComparison.OrdinalIgnoreCase);
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
        await SeedUniversalMetaFilesAsync();
        await SeedMortalFilesAsync();
        await SeedChaosSeaFilesAsync();
        await SeedShiningAbodeFilesAsync();
        await SeedAfterlifeCombatAndEntityFilesAsync();

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
        Assert.DoesNotContain("Realm", text, StringComparison.OrdinalIgnoreCase);
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
          "arcaneFocus": 7
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
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Lives_RendersLifeHistoryRowsWithoutRawJson()
    {
        await SeedUniversalMetaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/lives", AdvancedEnabled: false));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("История жизней", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Первая жизнь", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Инкарнация", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("soul_state", payload, StringComparison.OrdinalIgnoreCase);
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
        foreach (var forbidden in new[] { "JsonObject", "entries", "codexEntries", "unlockedAchievements", "trackedProgress" })
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(PlayerDefaultReadOnlyCommands))]
    public async Task ExecuteAsync_PlayerDefaultReadOnlyCommands_RenderPlayerFacingDefaultOutput(
        string commandId,
        string command,
        ExplorerCommandGroup group)
    {
        await SeedPlayerDefaultCommandAuditFilesAsync(commandId, group);

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
        await SeedPlayerDefaultCommandAuditFilesAsync(commandId, group);

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
        Assert.Contains(result.Blocks, static block => block is UiPanelBlock panel && panel.Title.Contains("Математик", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Blocks, static block => block is UiRawJsonBlock raw && raw.Title.Contains("JSON", StringComparison.OrdinalIgnoreCase));
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Результат", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("17", text, StringComparison.Ordinal);
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
        Assert.Contains(images, image => image.Title.Contains("ashen_knight", StringComparison.OrdinalIgnoreCase));
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
        await SeedUniversalMetaFilesAsync();
        await SeedMortalFilesAsync();
        await SeedChaosSeaFilesAsync();
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.NotEqual(CommandExecutionState.Blocked, result.State);
        Assert.NotEmpty(result.Blocks);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(expectedRussianLabel, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Локальный ход", text, StringComparison.OrdinalIgnoreCase);
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
                ["stat_allocation_json"] = JsonValue.Create("{ \"strength\": 2, \"wisdom\": 1 }")
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
                ["stat_allocation_json"] = JsonValue.Create("{ \"strength\": 2 }")
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
        soul["inkFeathers"] = new JsonObject { ["current"] = 200, ["total"] = 200 };
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
        Assert.Equal(75, updated["inkFeathers"]!["current"]!.GetValue<int>());
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
        soul["inkFeathers"] = new JsonObject { ["current"] = 200, ["total"] = 200 };
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
        Assert.Equal(110, updatedSoul["inkFeathers"]!["current"]!.GetValue<int>());
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
        await SeedUniversalMetaFilesAsync();

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
    public async Task ExecuteAsync_MemoryScene_ReturnsPlayerReadableDto()
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
        await SeedMortalFilesAsync();

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
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "totalWeight": 17,
          "maxWeight": 30,
          "money": 125,
          "resources": {
            "wood": 4,
            "gold": 0,
            "cloth": "2"
          },
          "equipment": {
            "head": { "name": "Железный шлем" },
            "mainHand": { "itemName": "Кривой меч" },
            "offHand": null
          },
          "items": [
            { "name": "Факел", "type": "utility", "count": 2, "durability": "100%" },
            { "itemName": "Сломанный лук", "type": "weapon", "quantity": 1, "durability": "0%" }
          ]
        }
        """);
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
        Assert.Contains(result.Blocks, static block =>
            block is UiTextBlock text &&
            text.Text == "⚖ 17 / 30" &&
            text.Tone == UiTone.Muted);
        Assert.Contains(result.Blocks, static block =>
            block is UiTextBlock text &&
            text.Text == "💰 Деньги: 125" &&
            text.Tone == UiTone.Default);

        var resources = Assert.Single(result.Blocks.OfType<UiKeyValueGridBlock>());
        Assert.Contains(resources.Items, static item => item.Key == "💎 wood" && item.Value == "4");
        Assert.Contains(resources.Items, static item => item.Key == "💎 cloth" && item.Value == "2");

        var equipmentPanel = Assert.Single(result.Blocks.OfType<UiPanelBlock>(), static panel => panel.Title == "⚔️ Экипировка");
        var equipmentGrid = Assert.IsType<UiKeyValueGridBlock>(Assert.Single(equipmentPanel.Blocks));
        Assert.Contains(equipmentGrid.Items, static item => item.Key == "🪖 Голова" && item.Value == "Железный шлем");
        Assert.Contains(equipmentGrid.Items, static item => item.Key == "⚔️ Основная рука" && item.Value == "Кривой меч");
        Assert.Contains(equipmentGrid.Items, static item => item.Key == "🛡️ Вторая рука" && item.Value == "— пусто —");

        var table = Assert.Single(result.Blocks.OfType<UiTableBlock>());
        Assert.Equal(new[] { "Название", "Тип", "Кол-во", "Прочность", "Статус" }, table.Columns);
        Assert.Contains(table.Rows, static row => row.Cells.SequenceEqual(["Факел", "utility", "2", "100%", "✓"]));
        Assert.Contains(table.Rows, static row => row.Cells.SequenceEqual(["Сломанный лук", "weapon", "1", "0%", "⚠ СЛОМАН"]));

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
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "items": [
            { "name": "Факел", "type": "utility", "count": 2 }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/inventory"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var table = Assert.Single(result.Blocks.OfType<UiTableBlock>());
        Assert.Equal("📦 Предметы (1)", table.Title);
        Assert.Contains(table.Rows, static row => row.Cells.SequenceEqual(["Факел", "utility", "2", string.Empty, "✓"]));
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
        Assert.Contains("Rare", text, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains(result.Actions, action =>
            action.Label.Contains("Назад", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/инв", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UiRawJsonBlock", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"structuredBonuses\"", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"combatEffect\"", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Books_ShowsReadableInventoryDocumentsAndSealedReasons()
    {
        await SeedBooksReadingFlowStateAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/книги"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var table = Assert.Single(result.Blocks.OfType<UiTableBlock>());
        Assert.Equal("Книжная полка", table.Title);
        Assert.Equal(["Документ", "Источник", "Доступ", "Кратко"], table.Columns);
        Assert.Contains(table.Rows, row => RowContains(row, "Письмо с площади") && RowContains(row, "Можно читать"));
        Assert.Contains(table.Rows, row => RowContains(row, "Записка с рынка") && RowContains(row, "1 запись"));
        Assert.Contains(table.Rows, row => RowContains(row, "Памятная книга") && RowContains(row, "1 запись"));
        Assert.Contains(table.Rows, row => RowContains(row, "Запечатанное письмо") && RowContains(row, "Не прочесть"));
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
        Assert.DoesNotContain("INLINE_FULL_BODY_MARKER", text, StringComparison.Ordinal);
        Assert.DoesNotContain("JOURNAL_FULL_BODY_MARKER", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Книжная полка", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Actions, action =>
            action.Label.Contains("Назад", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.Command, "/books", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
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
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "items": [
            {
              "existedId": "doc_first_1",
              "itemId": "doc_first_1",
              "name": "Первая записка",
              "type": "Документ",
              "textContent": ["FIRST_SHELF_ROW_BODY_MARKER"]
            },
            {
              "existedId": "doc_second_1",
              "itemId": "doc_second_1",
              "name": "Вторая записка",
              "type": "Документ",
              "textContent": ["SECOND_SHELF_ROW_BODY_MARKER"]
            },
            {
              "existedId": "2",
              "itemId": "2",
              "name": "Письмо с номером",
              "type": "Документ",
              "textContent": ["NUMERIC_STABLE_ID_BODY_MARKER"]
            }
          ]
        }
        """);

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
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "items": [
            {
              "existedId": "doc_inline_1",
              "itemId": "doc_inline_1",
              "name": "Письмо с площади",
              "type": "Документ",
              "group": "Документы и медиа",
              "textContent": [
                "Лира просит встретиться у фонтана до рассвета. Это длинное письмо продолжается подробностями о стороже, мокрой мостовой и тайном знаке. INLINE_FULL_BODY_MARKER"
              ]
            },
            {
              "existedId": "doc_sidecar_1",
              "itemId": "doc_sidecar_1",
              "name": "Записка с рынка",
              "type": "note",
              "group": "Документы и медиа",
              "textContent": null
            },
            {
              "existedId": "doc_journal_1",
              "itemId": "doc_journal_1",
              "name": "Памятная книга",
              "type": "Книга",
              "group": "Документы и медиа",
              "textContent": null
            },
            {
              "existedId": "doc_sealed_1",
              "itemId": "doc_sealed_1",
              "name": "Запечатанное письмо",
              "type": "Документ",
              "group": "Документы и медиа",
              "textContent": null,
              "unreadableReason": "Печать не позволяет прочесть письмо сейчас."
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/inventory/item_text_updates.json", """
        {
          "entries": [
            {
              "itemId": "doc_sidecar_1",
              "itemName": "Не это имя",
              "textContent": [
                "На обороте записки указан путь через северные ворота. Это длинная приписка с именами торговцев, часом встречи и предупреждением о дозорных. SIDECAR_FULL_BODY_MARKER"
              ]
            }
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
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "items": [
            {
              "existedId": "doc_sealed_only_1",
              "name": "Запечатанное письмо",
              "type": "Документ",
              "textContent": null,
              "unreadableReason": "Печать не позволяет прочесть письмо сейчас."
            }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/books"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Запечатанное письмо", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Печать не позволяет прочесть письмо сейчас.", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Данные ещё не созданы.", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
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
        Assert.Equal("inventory-unequip-head", unequipAction.Id);
        Assert.Equal("/снять head", unequipAction.Command);
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
    public async Task ExecuteAsync_SoulRelics_RendersStatusTableAndPlayerActions()
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
        var table = Assert.Single(result.Blocks.OfType<UiTableBlock>(), block => block.Title == "Реликвии души");
        Assert.Equal(["Статус", "Слот", "Реликвия", "Редкость", "ID"], table.Columns);
        Assert.Contains(table.Rows, row =>
            row.Cells.Contains("Хранилище") &&
            row.Cells.Contains("Клинок Памяти") &&
            row.Cells.Contains("rare"));
        Assert.Contains(table.Rows, row =>
            row.Cells.Contains("Экипировано") &&
            row.Cells.Contains("Шлем Тишины") &&
            row.Cells.Contains("legendary"));

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
        await SeedRichAfterlifeRelicArchiveDrilldownFilesAsync();

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
    [InlineData("/soul_relics реликвия relic_memory_blade", "Реликвия души: Клинок Памяти", "Память режет тьму", "Шлем Тишины")]
    [InlineData("/afterlife_archive запись archive_lore_001", "Архив души: Песнь Первого Маяка", "Полный текст маяка", "Запечатанный договор")]
    [InlineData("/archive_candidates кандидат candidate_mayak", "Кандидат в Архив: Песня маяка", "Кандидат хранит свет", "Тайный договор")]
    [InlineData("/archive_consultation хранитель guardian_azalia", "Архивная консультация: Азалия", "memory", "Недоверчивый Страж")]
    [InlineData("/archive_project_fuel проект guardian_azalia::project_forge_song", "Подпитка проекта: Песнь кузни", "lore_research", "Скрытый проект")]
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
    [InlineData("/soul_relics реликвия missing_relic")]
    [InlineData("/afterlife_archive запись missing_archive")]
    [InlineData("/archive_candidates кандидат missing_candidate")]
    [InlineData("/archive_consultation хранитель missing_guardian")]
    [InlineData("/archive_project_fuel проект guardian_azalia::missing_project")]
    public async Task ExecuteAsync_AfterlifeRelicArchiveDetails_UnknownIdsReturnPlayerFacingUnavailableText(string command)
    {
        await SeedRichAfterlifeRelicArchiveDrilldownFilesAsync();

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
        Assert.Contains(slotPrompt.Options, option => option.Value == "mainHand" && option.Label.Contains("Основная рука", StringComparison.OrdinalIgnoreCase));

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
                ["equipment_slot"] = JsonValue.Create("mainHand"),
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
        Assert.Equal("sword_1", inventory["equipment"]!["mainHand"]!.GetValue<string>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_InventoryUnequip_WritesNullAndReleasesLock()
    {
        await SeedInventoryEquipmentItemsAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/снять head",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var slotPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(started.Prompts, prompt => prompt.Id == "equipment_slot"));
        Assert.Equal("head", slotPrompt.Options.Single().Value);

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["equipment_slot"] = JsonValue.Create("head"),
                ["confirm_inventory_write"] = JsonValue.Create(true)
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        Assert.Contains("Железный шлем", CollectBlockText(completed.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("снят", CollectBlockText(completed.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(completed.Blocks, static block =>
            block is UiRawJsonBlock raw && raw.Title.Contains("JSON: результат браузерной записи", StringComparison.OrdinalIgnoreCase));

        var inventory = JsonNode.Parse((await _fs.ReadFileAsync("game_state/inventory/items.json"))!)!.AsObject();
        Assert.Null(inventory["equipment"]!["head"]);
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
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "equipment": {
            "head": "helmet_1",
            "mainHand": null,
            "offHand": null
          },
          "items": [
            { "existedId": "helmet_1", "name": "Железный шлем", "type": "helmet", "durability": "100%" }
          ]
        }
        """);

        var validation = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["item_identity"] = JsonValue.Create("sword_1"),
                ["equipment_slot"] = JsonValue.Create("mainHand"),
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
        Assert.Null(inventory["equipment"]!["mainHand"]);
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
        Assert.Null(inventory["equipment"]!["mainHand"]);
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
        var table = Assert.Single(result.Blocks.OfType<UiTableBlock>(), static block => block.Title == "Персонажи");
        Assert.Equal(new[] { "Раздел", "Состояние" }, table.Columns);
        var row = Assert.Single(table.Rows);
        Assert.Equal(["NPC", "1: Мирра"], row.Cells);
        Assert.DoesNotContain(table.Rows, static candidate => candidate.Cells.Any(static cell => cell.Contains("отсутствует", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(table.Rows.SelectMany(static candidate => candidate.Cells), static cell => cell.Contains("game_state/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("game_state/npcs", CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_NpcRichDetails_ExposesPlayerFacingDrilldownSections()
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

        var sectionTable = Assert.Single(result.Blocks.OfType<UiTableBlock>(), static block => block.Title == "Разделы НПС");
        Assert.Equal(["НПС", "Раздел", "Состояние"], sectionTable.Columns);
        Assert.Contains(sectionTable.Rows, static row =>
            row.Cells[0] == "Серафина" &&
            row.Cells[1].Contains("Дневник / мысли", StringComparison.Ordinal) &&
            row.Cells[2].Contains("2 записи", StringComparison.Ordinal));
        Assert.Contains(sectionTable.Rows, static row =>
            row.Cells[0] == "Серафина" &&
            row.Cells[1].Contains("Личные квесты", StringComparison.Ordinal) &&
            row.Cells[2].Contains("1 квест", StringComparison.Ordinal));
        Assert.Contains(sectionTable.Rows, static row =>
            row.Cells[0] == "Серафина" &&
            row.Cells[1].Contains("Активности", StringComparison.Ordinal) &&
            row.Cells[2].Contains("1 активность", StringComparison.Ordinal));
        Assert.DoesNotContain(sectionTable.Rows, static row =>
            row.Cells[1].Contains("Инвентарь", StringComparison.OrdinalIgnoreCase));

        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Серафина — Дневник / мысли", text, StringComparison.Ordinal);
        Assert.Contains("Сомневается, стоит ли доверять письму.", text, StringComparison.Ordinal);
        Assert.Contains("Сделка на рассвете", text, StringComparison.Ordinal);
        Assert.Contains("Доставить письмо в архив", text, StringComparison.Ordinal);
        Assert.Contains("Получить ключ от боковой двери", text, StringComparison.Ordinal);
        Assert.Contains("Награда", text, StringComparison.Ordinal);
        Assert.Contains("Провал", text, StringComparison.Ordinal);
        Assert.Contains("Проверяет печати у северных ворот", text, StringComparison.Ordinal);
        Assert.DoesNotContain("game_state/npcs", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".json", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/npc_talk", SerializeResult(result), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/npc_trade", SerializeResult(result), StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("Сомневается, стоит ли доверять письму.", text, StringComparison.Ordinal);
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
    public async Task ExecuteAsync_NpcRichDetails_DefaultProjectionHidesInternalFieldsAndMasks()
    {
        await SeedNpcInternalLeakDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);

        Assert.Contains("Видимый след в памяти", text, StringComparison.Ordinal);
        Assert.Contains("Клятва у печати", text, StringComparison.Ordinal);
        Assert.Contains("Получить ключ от боковой двери", text, StringComparison.Ordinal);

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

        var sectionTable = Assert.Single(result.Blocks.OfType<UiTableBlock>(), static block => block.Title == "Разделы НПС");
        Assert.Contains(sectionTable.Rows, static row =>
            row.Cells[0] == "Серафина" &&
            row.Cells[1].Contains("Навыки", StringComparison.Ordinal) &&
            row.Cells[1].Contains("инвентарь", StringComparison.OrdinalIgnoreCase));
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
        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationId": "loc_square",
          "name": "Старая площадь",
          "region": "Северный край",
          "description": "Площадь под серым небом.",
          "coordinates": { "x": 4, "y": 7, "z": 0 },
          "adjacencyMap": [
            {
              "targetLocationId": "loc_gate",
              "name": "Северные ворота",
              "direction": "север",
              "linkState": "safe",
              "targetCoordinates": { "x": 4, "y": 8, "z": 0 }
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_map.json", """
        {
          "newLocations": [
            {
              "locationId": "loc_gate",
              "locationName": "Северные ворота",
              "locationType": "gate",
              "coordinates": { "x": 4, "y": 8, "z": 0 }
            }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/map"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var mapBlock = Assert.Single(result.Blocks.OfType<UiMapBlock>());
        Assert.Equal("Mortal World", mapBlock.Map.Realm);
        Assert.Equal("loc_square", mapBlock.Map.CurrentNodeId);
        Assert.Contains(mapBlock.Map.Nodes, static node => node.IsCurrent && node.Label == "Старая площадь");
        Assert.Contains(mapBlock.Map.Links, static link => link.TargetNodeId == "loc_gate");
    }

    [Fact]
    public async Task ExecuteAsync_Map_InChaosSea_ReturnsAbodeConstellationMap()
    {
        await SeedChaosSeaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/map"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var mapBlock = Assert.Single(result.Blocks.OfType<UiMapBlock>());
        Assert.Equal("Chaos Sea", mapBlock.Map.Realm);
        Assert.Equal("abode_azalia", mapBlock.Map.CurrentNodeId);
        Assert.Contains(mapBlock.Map.Nodes, static node => node.IsCurrent && node.Label == "Сад Ночных Роз");
        Assert.Contains(mapBlock.Map.Nodes, static node => node.Details.Any(item => item.Key == "Активный Хранитель" && item.Value == "да"));
    }

    [Fact]
    public async Task ExecuteAsync_Map_InShiningAbode_ReturnsCivicAtlasMap()
    {
        await SeedShiningAbodeFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/map"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var mapBlock = Assert.Single(result.Blocks.OfType<UiMapBlock>());
        Assert.Equal("Shining Abode", mapBlock.Map.Realm);
        Assert.Contains(mapBlock.Map.Nodes, static node => node.Id == "hall_dawn" && node.Label == "Зал Рассвета");
        Assert.Contains(mapBlock.Map.Nodes, static node => node.Id == "faction_lanterns" && node.Details.Any(item => item.Key == "Лидерство"));
    }

    [Theory]
    [InlineData("/локации")]
    [InlineData("/locations")]
    public async Task ExecuteAsync_Locations_IncludesCurrentAdjacentAndWrappedWorldMapUpdates(string command)
    {
        await SeedMortalFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationId": "loc_square",
          "name": "Старая площадь",
          "description": "Площадь с тёмным фонтаном.",
          "adjacencyMap": [
            {
              "targetLocationId": "loc_gate",
              "targetLocationName": "Северные ворота",
              "direction": "север",
              "distance": "10 минут",
              "linkState": "safe"
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_map.json", """
        {
          "worldMapUpdates": {
            "newLocations": [
              {
                "locationId": "loc_tower",
                "name": "Пепельная башня",
                "locationType": "watchtower",
                "description": "Башня над дорогой."
              }
            ],
            "locationUpdates": [
              {
                "locationId": "loc_bridge",
                "name": "Сломанный мост",
                "locationType": "bridge",
                "lastEventsDescription": "Мост осел после ночного дождя."
              }
            ]
          }
        }
        """);

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
    public async Task ExecuteAsync_Locations_PreservesRootLevelUpdatesAndDeduplicatesWrappedMatches()
    {
        await SeedMortalFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/world/world_map.json", """
        {
          "newLocations": [
            {
              "locationId": "loc_gate",
              "name": "Северные ворота",
              "locationType": "gate"
            }
          ],
          "locationUpdates": [
            {
              "locationId": "loc_market",
              "name": "Старый рынок",
              "locationType": "market"
            }
          ],
          "worldMapUpdates": {
            "newLocations": [
              {
                "locationId": "loc_gate",
                "name": "Северные ворота",
                "locationType": "gate"
              },
              {
                "locationId": "loc_garden",
                "name": "Сад у стены",
                "locationType": "garden"
              }
            ],
            "locationUpdates": [
              {
                "locationId": "loc_market",
                "name": "Старый рынок",
                "locationType": "market"
              }
            ]
          }
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/locations"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Северные ворота", text, StringComparison.Ordinal);
        Assert.Contains("Сад у стены", text, StringComparison.Ordinal);
        Assert.Contains("Старый рынок", text, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(text, "Северные ворота"));
        Assert.Equal(1, CountOccurrences(text, "Старый рынок"));
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
        await SeedChaosSeaFilesAsync();

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
        await SeedRichChaosSeaGuardianAbodeDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(expectedLabelText, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Подробно", text, StringComparison.OrdinalIgnoreCase);

        var action = Assert.Single(result.Actions, action => action.Id == expectedActionId);
        Assert.Equal(expectedDetailCommand, action.Command);
        Assert.Contains("Подробно", action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedLabelText, action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, action.Style);
        Assert.False(action.RequiresConfirmation);
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
        await SeedRichChaosSeaGuardianAbodeDrilldownFilesAsync();

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
        Assert.Contains("Пороги: 4-48 Common, 49-67 Uncommon, 68-75 Rare, 76-79 Epic, 80 Legendary", text, StringComparison.Ordinal);
        Assert.Contains("Базовая редкость", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Rare", text, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains(expectedRarity, text, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("guardian_scene:guardian_mirror", text, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("known_revealed_truth_marker", text, StringComparison.Ordinal);
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
        var relationshipTable = Assert.Single(result.Blocks.OfType<UiTableBlock>(), static table => table.Title == "Отношения");
        var text = CollectBlockText([relationshipTable]);
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
        await SeedShiningAbodeFilesAsync();

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
        await SeedAfterlifeCombatAndEntityFilesAsync();

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
        Assert.Contains("Когда counter отвечает на прямое pressure", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Преимущество для ответного pressure", combinedText, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("mark", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("opposition", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("guardian_azalia", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pressure", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("remainingUses=1", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("break_binding", text, StringComparison.OrdinalIgnoreCase);
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
            { "title": "Первый знак", "content": "Тестовая запись кодекса" }
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

    private void WriteSessionImage(string relativePath)
    {
        var fullPath = _fs.ResolvePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, [137, 80, 78, 71, 13, 10, 26, 10]);
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
                }
              ],
              "currentActivity": {
                "activityName": "Проверка печатей",
                "description": "Проверяет печати у северных ворот",
                "timeSpentMinutes": 30,
                "totalTimeCostMinutes": 60
              }
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
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_serafina",
              "name": "Серафина",
              "inventory": [
                { "name": "Архивный ключ", "description": "Открывает боковую дверь." }
              ]
            }
          ]
        }
        """);
    }

    private async Task SeedMortalFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "items": [
            { "itemId": "blade_1", "itemName": "Старый клинок", "quantity": 1 }
          ]
        }
        """);

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
              "location": "Северные ворота",
              "visibility": "public",
              "status": "active",
              "category": "городские слухи",
              "description": "Толпа спорит у ворот [red]без разметки[/].",
              "summary": "Стража закрыла торговую площадь.",
              "involvedNPCs": [ "Мира Ключница" ],
              "affectedFactions": [ "Городская стража" ],
              "affectedLocations": [ "Северные ворота" ],
              "consequences": [ "торговая площадь закрыта до следующего утра" ],
              "followUp": "Капитан ждёт свидетелей."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "name": "Северные ворота",
          "activeThreats": [
            {
              "threatId": "gate_pickpockets",
              "threatName": "Карманники у ворот",
              "dangerLevel": "low",
              "description": "Несколько ловкачей пользуются давкой."
            }
          ]
        }
        """);

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
              "consequence": "Площадь открыта только для жителей."
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
              "timestamp": "день 42"
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
                  "participants": [
                    "Лианна из янтарной башни",
                    "герой"
                  ],
                  "summary": "Лианна оставила шифр под бронзовой чашей.",
                  "notes": "шифр спрятан в перчатке",
                  "outcome": "контакт сохранён",
                  "nextStep": "Можно спросить о знаке Вальмонтов.",
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
                "UpdateInventory": [
                  {
                    "itemName": "Серебряный ключ",
                    "quantity": 2,
                    "description": "покрыт знаками янтарной башни"
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

        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationId": "loc_square",
          "name": "Старая площадь",
          "region": "Северный квартал",
          "locationType": "площадь",
          "description": "Площадь с тёмным фонтаном и следами ночного письма.",
          "adjacencyMap": [
            {
              "targetLocationId": "loc_gate",
              "targetLocationName": "Северные ворота",
              "direction": "север",
              "distance": "10 минут"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_map.json", """
        {
          "newLocations": [
            {
              "locationId": "loc_archive",
              "name": "Архив Вальмонтов",
              "locationType": "архив",
              "description": "Запертый зал под старой ратушей."
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
              "remainingTurns": 3,
              "structuredBonuses": [
                {
                  "bonusType": "Characteristic",
                  "target": "Восприятие",
                  "value": -1,
                  "valueType": "Flat",
                  "modifierType": "temporary",
                  "source": "Головная боль после тяжёлых снов",
                  "summary": "Восприятие -1"
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
              "description": "В покоях найдено письмо с крыльями и полумесяцем."
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
    }

    private Task WriteShiningFactionPoliticalMemoryRawLeakFixtureAsync() =>
        _fs.WriteFileAtomicAsync("game_state/meta/shining_abode_state.json", """
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
            "standardArts": {
              "pressure": 2,
              "guard": 1,
              "counter": 1,
              "maneuver": 2,
              "binding": 1
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
              "soulDissipationTier": 0
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

    private async Task SeedInventoryEquipmentItemsAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "equipment": {
            "head": "helmet_1",
            "mainHand": null,
            "offHand": null
          },
          "items": [
            { "existedId": "sword_1", "name": "Кривой меч", "type": "weapon", "durability": "100%" },
            { "existedId": "helmet_1", "name": "Железный шлем", "type": "helmet", "durability": "100%" },
            { "existedId": "torch_1", "name": "Факел", "type": "utility", "count": 2 },
            { "existedId": "broken_bow_1", "name": "Сломанный лук", "type": "weapon", "durability": "0%" },
            { "relicId": "soul_relic_1", "name": "Реликвия души", "type": "soul_relic", "equipmentSlot": "ring1" }
          ]
        }
        """);
    }

    private async Task SeedInventoryItemDetailStateAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "equipment": {
            "hands": "runic_glove_1"
          },
          "items": [
            {
              "existedId": "runic_glove_1",
              "itemId": "runic_glove_1",
              "name": "Руническая перчатка",
              "description": "На тыльной стороне перчатки мерцает рунический контур.",
              "type": "Артефакт",
              "quality": "Rare",
              "weight": 0.3,
              "price": 450,
              "durability": "95",
              "maxDurability": "100",
              "equipmentSlot": "hands",
              "group": "Аксессуары",
              "bonuses": [
                "Чувство магических потоков +2"
              ],
              "effects": [
                { "name": "Откликается на владельца" }
              ],
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
                    "effectType": "PoiseDamage",
                    "value": 0,
                    "poiseDamage": 1,
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
              "specialProperties": [
                "Перчатка реагирует на владельца."
              ],
              "lore": "Вышита тусклым золотом."
            }
          ]
        }
        """);
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
                  "spiritVoice": "Тонкий голос просит найти серебряную нить."
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

        return violations;
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
                _validationService);
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
