using System.Globalization;
using System.Text.RegularExpressions;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "RegressionIntegration")]
public sealed partial class BrowserCommandPresentationAuditTests : IClassFixture<BrowserCommandPresentationAuditFixture>
{
    private readonly BrowserCommandPresentationAuditFixture _fixture;

    public BrowserCommandPresentationAuditTests(BrowserCommandPresentationAuditFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MemberData(nameof(MortalEntityCommandInvocations))]
    public async Task MortalEntityCommands_DoNotFlattenStructuredDataIntoPlayerFacingText(string command)
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            command);

        AssertNoPresentationAntiPatterns(command, result);
    }

    [Theory]
    [MemberData(nameof(MortalEntityDetailCommandInvocations))]
    public async Task MortalEntityDetailCommands_DoNotFlattenStructuredDataIntoPlayerFacingText(string command)
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            command);

        AssertNoPresentationAntiPatterns(command, result);
    }

    [Theory]
    [MemberData(nameof(ChaosSeaEntityCommandInvocations))]
    public async Task ChaosSeaEntityCommands_DoNotFlattenStructuredDataIntoPlayerFacingText(string command)
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "chaos_sea_command_display_fixture.zip",
            command);

        AssertNoPresentationAntiPatterns(command, result);
    }

    [Theory]
    [MemberData(nameof(ShiningAbodeEntityCommandInvocations))]
    public async Task ShiningAbodeEntityCommands_DoNotFlattenStructuredDataIntoPlayerFacingText(string command)
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "shining_abode_command_display_fixture.zip",
            command);

        AssertNoPresentationAntiPatterns(command, result);
    }

    public static IEnumerable<object[]> MortalEntityCommandInvocations()
    {
        yield return ["/статус"];
        yield return ["/инв"];
        yield return ["/нпс"];
        yield return ["/квесты"];
        yield return ["/карта"];
        yield return ["/где_я"];
        yield return ["/фракции"];
        yield return ["/навыки"];
        yield return ["/статы"];
        yield return ["/новости_мира"];
        yield return ["/чужие_нити"];
        yield return ["/коррективы_хранителя"];
        yield return ["/локации"];
        yield return ["/транспорт"];
        yield return ["/эффекты"];
        yield return ["/бой"];
        yield return ["/погода"];
        yield return ["/книги"];
        yield return ["/доступ_к_хранилищам"];
        yield return ["/взаимодействия"];
    }

    public static IEnumerable<object[]> MortalEntityDetailCommandInvocations()
    {
        yield return ["/инв предмет item_dark_travel_cloak"];
        yield return ["/нпс персонаж npc_valmont_steward_marius"];
        yield return ["/нпс раздел npc_valmont_steward_marius journal"];
        yield return ["/нпс раздел npc_valmont_steward_marius personal-quests"];
        yield return ["/нпс раздел npc_valmont_steward_marius activities"];
        yield return ["/нпс раздел npc_valmont_steward_marius relationships"];
        yield return ["/нпс раздел npc_valmont_steward_marius personality"];
        yield return ["/нпс раздел npc_valmont_steward_marius mechanics"];
        yield return ["/нпс раздел npc_valmont_steward_marius memory"];
        yield return ["/нпс квест npc_valmont_steward_marius quest_marius_missing_key"];
        yield return ["/квесты квест quest_valmont_letter"];
        yield return ["/фракции фракция faction_merchant_guild"];
        yield return ["/навыки навык skill_quick_lunge"];
        yield return ["/новости_мира событие world_event_valmont_letter"];
        yield return ["/локации локация loc_valmont_bedroom_initial"];
        yield return ["/локации хранилища loc_valmont_bedroom_initial"];
        yield return ["/транспорт транспорт transport_valmont_carriage"];
        yield return ["/эффекты эффект лёгкое_недомогание"];
        yield return ["/бой враг 1"];
        yield return ["/бой журнал 1"];
        yield return ["/доступ_к_хранилищам хранилище storage_valmont_private_desk"];
        yield return ["/взаимодействия запись player_test_companion-1"];
    }

    [Fact]
    public void MortalReadOnlyIssue1268Commands_AreCoveredByBrowserPresentationAudit()
    {
        var auditedIds = MortalEntityCommandInvocations()
            .Select(static invocation => (string)invocation[0])
            .Select(static command => ExplorerCommandParser.Parse(command).Descriptor?.Id)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] requiredIds =
        [
            "inventory",
            "npcs",
            "quests",
            "map",
            "where_am_i",
            "factions",
            "skills",
            "stats",
            "world_news",
            "rival_threads",
            "guardian_corrections",
            "locations",
            "transport",
            "effects",
            "combat",
            "weather",
            "books",
            "storage_access",
            "interactions"
        ];

        var missing = requiredIds
            .Where(id => !auditedIds.Contains(id))
            .ToList();

        Assert.True(
            missing.Count == 0,
            "Issue #1268 mortal read-only commands missing from browser presentation audit: " +
            string.Join(", ", missing));
    }

    [Fact]
    public void MortalNpcIssue1258SectionDrilldowns_AreCoveredByBrowserPresentationAudit()
    {
        var auditedCommands = MortalEntityDetailCommandInvocations()
            .Select(static invocation => (string)invocation[0])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] requiredCommands =
        [
            "/нпс персонаж npc_valmont_steward_marius",
            "/нпс раздел npc_valmont_steward_marius journal",
            "/нпс раздел npc_valmont_steward_marius personal-quests",
            "/нпс раздел npc_valmont_steward_marius activities",
            "/нпс раздел npc_valmont_steward_marius relationships",
            "/нпс раздел npc_valmont_steward_marius personality",
            "/нпс раздел npc_valmont_steward_marius mechanics",
            "/нпс раздел npc_valmont_steward_marius memory",
            "/нпс квест npc_valmont_steward_marius quest_marius_missing_key"
        ];

        var missing = requiredCommands
            .Where(command => !auditedCommands.Contains(command))
            .ToList();

        Assert.True(
            missing.Count == 0,
            "Issue #1258 NPC drilldowns missing from browser presentation audit: " +
            string.Join(", ", missing));
    }

    public static IEnumerable<object[]> ChaosSeaEntityCommandInvocations()
    {
        yield return ["/статус"];
        yield return ["/душа"];
        yield return ["/реликвии"];
        yield return ["/хранители"];
        yield return ["/хранители хранитель guardian_azalia"];
        yield return ["/обители"];
        yield return ["/обители обитель abode_azalia"];
        yield return ["/сила_обители"];
        yield return ["/сила_обители запись power_azalia_archive_oath_001"];
        yield return ["/проекты_хранителей"];
        yield return ["/проекты_хранителей проект guardian_azalia::project_archive_lighthouse"];
        yield return ["/профили_загробья"];
        yield return ["/профили_загробья профиль player_soul"];
        yield return ["/угрозы_загробья"];
        yield return ["/хроники_посмертия"];
        yield return ["/духовный_конфликт"];
        yield return ["/журнал_духовного_боя"];
        yield return ["/духовные_искусства"];
    }

    public static IEnumerable<object[]> ShiningAbodeEntityCommandInvocations()
    {
        yield return ["/статус"];
        yield return ["/душа"];
        yield return ["/реликвии"];
        yield return ["/сияющая_обитель"];
        yield return ["/сияющая_обитель врата card_social"];
        yield return ["/сияющая_обитель проект faction_lanterns::project_dawn"];
        yield return ["/сияющая_политика"];
        yield return ["/сияющая_политика фракция faction_lanterns"];
        yield return ["/профили_загробья"];
        yield return ["/профили_загробья профиль player_soul"];
        yield return ["/угрозы_загробья"];
        yield return ["/хроники_посмертия"];
        yield return ["/духовный_конфликт"];
        yield return ["/журнал_духовного_боя"];
        yield return ["/духовные_искусства"];
    }

    [Theory]
    [MemberData(nameof(CommonProtocolLocalizationCases))]
    public async Task CommandSurfaces_LocalizeCommonProtocolValues(
        string saveFileName,
        string command,
        string rawValue,
        string expectedPlayerText)
    {
        var result = await ExecuteFromLoadedSaveAsync(saveFileName, command);
        var text = CollectResultText(result);

        Assert.DoesNotContain(rawValue, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedPlayerText, text, StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<object[]> CommonProtocolLocalizationCases()
    {
        yield return ["mortal_world_command_display_fixture.zip", "/душа", "Mortal World", "Смертный мир"];
        yield return ["chaos_sea_command_display_fixture.zip", "/душа", "Chaos Sea", "Море Хаоса"];
        yield return ["shining_abode_command_display_fixture.zip", "/душа", "Shining Abode", "Сияющая Обитель"];
        yield return ["mortal_world_command_display_fixture.zip", "/инв предмет item_dark_travel_cloak", "Chest", "Грудь"];
        yield return ["shining_abode_command_display_fixture.zip", "/реликвии реликвия relic_lantern_memory", "rare", "редкое"];
        yield return ["mortal_world_command_display_fixture.zip", "/достижения", "exploration", "исследование"];
        yield return ["mortal_world_command_display_fixture.zip", "/бой", "Humanoid", "гуманоид"];
        yield return ["mortal_world_command_display_fixture.zip", "/карта", "outdoor", "открытая местность"];
        yield return ["mortal_world_command_display_fixture.zip", "/карта", "Mortal World", "Смертный мир"];
        yield return ["mortal_world_command_display_fixture.zip", "/правила_мира", "requiredElements", "Обязательные элементы"];
        yield return ["chaos_sea_command_display_fixture.zip", "/квесты_души", "currentDisplayName", "Квесты души"];
        yield return ["chaos_sea_command_display_fixture.zip", "/духовные_искусства", "Counter", "Контрприём"];
        yield return ["chaos_sea_command_display_fixture.zip", "/духовные_искусства", "defense", "защита"];
        yield return ["shining_abode_command_display_fixture.zip", "/карта", "shining abode district", "район Сияющей Обители"];
        yield return ["shining_abode_command_display_fixture.zip", "/карта", "Shining Abode", "Сияющая Обитель"];
        yield return ["shining_abode_command_display_fixture.zip", "/карта", "resident_senna", "резидент"];
        yield return ["mortal_world_command_display_fixture.zip", "/квесты_души", "защитаian", "Квесты души"];
        yield return ["mortal_world_command_display_fixture.zip", "/квесты_души", "activeGuardian", "Квесты души"];
        yield return ["mortal_world_command_display_fixture.zip", "/квесты_души", "abodePower", "Квесты души"];
        yield return ["mortal_world_command_display_fixture.zip", "/взаимодействия", "player_test_companion", "Игрок 1"];
        yield return ["chaos_sea_command_display_fixture.zip", "/карта", "abode_azalia", "Шелковый Архив"];
        yield return ["chaos_sea_command_display_fixture.zip", "/карта", "Chaos Sea", "Море Хаоса"];
        yield return ["chaos_sea_command_display_fixture.zip", "/политика_хранителей", "abode_azalia", "Шелковый Архив"];
        yield return ["chaos_sea_command_display_fixture.zip", "/журнал_духовного_боя", "exchange_chaos_hunter_001", "Защита"];
        yield return ["chaos_sea_command_display_fixture.zip", "/журнал_духовного_боя", "recent_conflict_hunter_pack_044", "После зеркальной защиты"];
        yield return ["shining_abode_command_display_fixture.zip", "/журнал_духовного_боя", "recent_shining_oath_cell_001", "победа"];
        yield return ["shining_abode_command_display_fixture.zip", "/карта", "; Скрытый Дом", "Скрытый Дом"];
        yield return ["shining_abode_command_display_fixture.zip", "/сияющая_политика", "rumor_credit", "Кредит слухов"];
        yield return ["shining_abode_command_display_fixture.zip", "/сияющая_политика хроника chronicle_hidden", "hidden_house_suspected", "Скрытом Доме"];
        yield return ["shining_abode_command_display_fixture.zip", "/сияющая_политика хроника chronicle_hidden", "rumor", "слух"];
        yield return ["mortal_world_command_display_fixture.zip", "/world_setup", "world_profiles", "Подготовка следующего мира"];
        yield return ["mortal_world_command_display_fixture.zip", "/companion_directive", "playerCompanionDirective", "директив"];
        yield return ["mortal_world_command_display_fixture.zip", "/spiritual_arts", "afterlifeSpecialArtLearningReceipts", "обучения духовным искусствам"];
        yield return ["chaos_sea_command_display_fixture.zip", "/spiritual_action", "afterlifeSpiritualConflictUpdate", "духовного конфликта"];
        yield return ["chaos_sea_command_display_fixture.zip", "/abode_offering", "guardian_azalia", "Азалия"];
        yield return ["shining_abode_command_display_fixture.zip", "/soul_relic_equip", "relic_silver_votive_thread", "Серебряная обетная нить"];
        yield return ["mortal_world_command_display_fixture.zip", "/объединить_стопки", "; после объединения:", "Объединение стопок"];
        yield return ["chaos_sea_command_display_fixture.zip", "/archive_consultation", "; редкость:", "Редкость"];
    }

    [Theory]
    [InlineData("/погода")]
    [InlineData("/статус")]
    [InlineData("/где_я")]
    public async Task MortalTimeSurfaces_PreserveClockFormatting(string command)
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            command);
        var text = CollectResultText(result);

        Assert.Contains("08:15", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("08: 15", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(TimeWithWhitespaceAfterColonPattern(), text);
    }

    [Fact]
    public async Task MortalLocationOverviewCardsSplitPlaceFactsFromNarrative()
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            "/локации");
        var text = CollectResultText(result);

        Assert.Contains("Покои виконта де Вальмонта", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Регион", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тип", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Роскошные покои", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("помещение Роскошные покои", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("помещение здание Длинный коридор", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Month of Beginnings", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MortalTransportOverviewCardsSplitStateLocationAndCapacity()
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            "/транспорт");
        var text = CollectResultText(result);

        Assert.Contains("Карета дома Вальмонт", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Состояние", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Местоположение", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Вместимость", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Активен (оседлан/управляется) Покои виконта", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MortalCombatOverviewSummariesExplainHealthAndPoiseValues()
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            "/бой");
        var text = CollectResultText(result);

        Assert.Contains("Теневой посыльный", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Здоровье", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Стойкость", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(AdjacentCombatPercentagesPattern(), text);
    }

    [Fact]
    public async Task MortalCombatLogOverviewUsesPlayerFacingRussianText()
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            "/бой");
        var text = CollectResultText(result);

        Assert.Contains("Последняя стычка: Ночной визитёр", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("проверка Восприятия", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("против сложности 12", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("против защиты 13", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("###", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("**", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vs DC", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vs AC", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("- Атака", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Итог: не указано", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MortalCombatOverviewOmitsEmptyCombatantStateFacts()
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            "/бой");
        var text = CollectResultText(result);

        Assert.Contains("Теневой посыльный", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Дворецкий Мариус", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Состояние: не указано", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Намерение: не указано", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/где_я")]
    [InlineData("/погода")]
    [InlineData("/фракции")]
    [InlineData("/чужие_нити")]
    public async Task MortalReadOnlyCommandsLocalizeCommonProtocolValues(string command)
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            command);
        var text = CollectResultText(result);

        Assert.DoesNotContain("Month of Beginnings", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rising", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(GluedRivalStatusSummaryPattern(), text);
    }

    [Fact]
    public async Task MortalNpcOverviewCardsExposeDirectProfileActions()
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            "/нпс");

        var npcCards = EnumerateEntityCards(result.Blocks)
            .Where(static card =>
                string.Equals(card.Icon, "npc", StringComparison.OrdinalIgnoreCase) ||
                card.Subtitle.Contains("Персонаж", StringComparison.OrdinalIgnoreCase))
            .Where(static card => !string.Equals(card.Title, "Персонажи", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(npcCards);
        Assert.Contains(npcCards, static card =>
        {
            var action = GetCardPrimaryAction(card);
            return action != null &&
                   action.Label.Contains("Открыть", StringComparison.OrdinalIgnoreCase) &&
                   action.Command.Contains("/нпс", StringComparison.OrdinalIgnoreCase) &&
                   action.Command.Contains("персонаж", StringComparison.OrdinalIgnoreCase);
        });

        Assert.DoesNotContain(npcCards, static card =>
            ContainsUiInstructionCopy(card.Summary) ||
            card.Summary.Contains("игровые свойства", StringComparison.OrdinalIgnoreCase) ||
            card.Summary.Contains("раскрыты ниже", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MortalNpcRelationshipCardsLabelNumericRelationshipValues()
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            "/нпс");

        var relationshipCards = EnumerateEntityCards(result.Blocks)
            .Where(static card =>
                card.Title.Contains("Отношение", StringComparison.OrdinalIgnoreCase) ||
                card.Title.Contains("Замок отношения", StringComparison.OrdinalIgnoreCase) ||
                card.Subtitle.Contains("Отношения", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(relationshipCards);
        Assert.Contains(
            relationshipCards,
            static card => card.Facts.Any(static fact =>
                string.Equals(fact.Label, "Уровень отношения", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(fact.Value, out _)));
        Assert.Contains(
            relationshipCards,
            static card => card.Facts.Any(static fact =>
                string.Equals(fact.Label, "Уровень отношения", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(fact.Value, "-80", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(
            relationshipCards,
            static card => card.Facts.Any(static fact =>
                string.Equals(fact.Label, "Порог отношения", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(fact.Value, "-50", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(
            relationshipCards,
            static card => int.TryParse(card.Summary, out _) ||
                           card.Facts.Any(static fact =>
                               string.Equals(fact.Label, "Подробности", StringComparison.OrdinalIgnoreCase) &&
                               int.TryParse(fact.Value, out _)));
        Assert.DoesNotContain(
            relationshipCards,
            static card => card.Title.Contains("Замок отношения", StringComparison.OrdinalIgnoreCase) &&
                           string.IsNullOrWhiteSpace(card.Summary) &&
                           card.Facts.Count == 0 &&
                           card.Metrics.Count == 0 &&
                           card.List.Count == 0 &&
                           card.Cards.Count == 0 &&
                           card.Nested.Count == 0);
        Assert.DoesNotContain(
            relationshipCards,
            static card => card.Facts.Count == 1 &&
                           string.Equals(card.Facts[0].Label, "Кто", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MortalNpcCardsDoNotRepeatSummaryAsGenericDetails()
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            "/нпс");

        var npcCards = EnumerateEntityCards(result.Blocks)
            .Where(static card => !string.IsNullOrWhiteSpace(card.Summary))
            .ToList();

        Assert.NotEmpty(npcCards);
        Assert.DoesNotContain(
            npcCards,
            static card => card.Facts.Any(fact =>
                IsGenericDetailsColumn(fact.Label) &&
                string.Equals(fact.Value, card.Summary, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task MortalQuestOverviewUsesPlotOutlineEntriesInsteadOfContainer()
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            "/квесты");

        Assert.Contains(
            result.Actions,
            static action => action.Label.Contains("Письмо на прикроватном столике", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            result.Actions,
            static action => action.Label.Contains("Безопасный выезд через гильдию", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            result.Actions,
            static action => action.Label.Contains("Сюжетных записей 3", StringComparison.OrdinalIgnoreCase));

        var cards = EnumerateEntityCards(result.Blocks).ToList();
        Assert.Contains(
            cards,
            static card => string.Equals(card.Title, "Письмо на прикроватном столике", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            cards,
            static card => string.Equals(card.Title, "Безопасный выезд через гильдию", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            cards,
            static card => card.Title.Contains("Сюжетных записей 3", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            cards,
            static card => card.Summary.Contains("Available", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MortalInventoryOverviewCardsExposeUsefulItemDataAndDirectOpenActions()
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            "/инв");

        var itemCards = EnumerateEntityCards(result.Blocks)
            .Where(static card =>
                string.Equals(card.Icon, "inventory", StringComparison.OrdinalIgnoreCase) ||
                card.Subtitle.Contains("предмет", StringComparison.OrdinalIgnoreCase) ||
                card.Subtitle.Contains("одеж", StringComparison.OrdinalIgnoreCase) ||
                card.Subtitle.Contains("артефакт", StringComparison.OrdinalIgnoreCase) ||
                card.Subtitle.Contains("документ", StringComparison.OrdinalIgnoreCase))
            .Where(static card => !string.Equals(card.Title, "Инвентарь", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(itemCards);
        Assert.Contains(itemCards, static card => GetCardPrimaryAction(card) != null);
        Assert.Contains(itemCards, static card =>
            HasFact(card, "Тип") &&
            HasFact(card, "Количество") &&
            (HasFact(card, "Группа") || HasFact(card, "Слот") || HasFact(card, "Цена")) &&
            !IsDurabilityOnlySummary(card.Summary));
    }

    [Fact]
    public async Task MortalInteractionPlayerCardsUseRecordSummaryInsteadOfGenericPlaceholder()
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            "/взаимодействия");

        var playerCards = EnumerateEntityCards(result.Blocks)
            .Where(static card => string.Equals(card.Title, "Игрок 1", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(playerCards);
        Assert.Contains(
            playerCards,
            static card => card.Summary.Contains("Помогает проверить взаимодействия с другими участниками сцены", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            playerCards,
            static card => string.Equals(card.Summary, "Есть видимые взаимодействия.", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(card.Summary, "Видимая запись игрока.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MortalBooksOverviewDoesNotRepeatCountsOrUnreadableReasons()
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            "/книги");
        var text = CollectResultText(result);

        Assert.Contains("Можно читать. 3 записи. Господин де Вальмонт", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Не прочесть. Это не текстовый документ", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("3 записи. 3 записи:", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1 запись. 1 запись:", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(". .", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Это не текстовый документ, а латунная печать для подтверждения права говорить от имени караванного мастера. . Это не текстовый документ",
            text,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/книги")]
    [InlineData("/эффекты")]
    [InlineData("/бой")]
    [InlineData("/новости_мира")]
    [InlineData("/взаимодействия")]
    public async Task MortalReadOnlySummariesAvoidUiInstructionCopy(string command)
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            command);
        var text = CollectResultText(result);

        Assert.DoesNotContain("Выберите", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Полные данные", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Полные сведения", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Подробности открываются", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("отдельным действием", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/квесты")]
    [InlineData("/фракции")]
    [InlineData("/навыки")]
    [InlineData("/чужие_нити")]
    [InlineData("/коррективы_хранителя")]
    [InlineData("/доступ_к_хранилищам")]
    public async Task MortalReferenceBundlesUseCommandSpecificSummaries(string command)
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            command);
        var text = CollectResultText(result);

        Assert.DoesNotContain("Что уже отмечено в книге.", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Известные записи этого раздела.", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MortalCurrentLocationRendersCanonicalDifficultyProfilesInLocalizedFacts()
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            "/где_я");
        var text = CollectResultText(result);

        Assert.Contains("Сложность (для своих)", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сложность (для чужих)", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Рекомендуемый уровень (для своих)", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Рекомендуемый уровень (для чужих)", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("бой: 30 окружение", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MortalFactionCardsExposeRanksChroniclesAndResourcesBeyondPowerProfile()
    {
        var overview = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            "/фракции");

        var factionCards = EnumerateEntityCards(overview.Blocks)
            .Where(static card =>
                card.Title.Contains("гильд", StringComparison.OrdinalIgnoreCase) ||
                card.Subtitle.Contains("Фракц", StringComparison.OrdinalIgnoreCase) ||
                card.Badges.Any(static badge => badge.Label.Contains("Фракц", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.NotEmpty(factionCards);
        Assert.Contains(factionCards, static card =>
            CardContainsText(card, "Почётный должник") ||
            CardContainsText(card, "Караванная служба"));
        Assert.Contains(factionCards, static card =>
            CardContainsText(card, "Кредит доверия") ||
            CardContainsText(card, "Караванные припасы"));
        Assert.Contains(factionCards, static card =>
            CardContainsText(card, "Долг за спасение каравана") ||
            CardContainsText(card, "право просить о помощи"));

        var guildAction = overview.Actions.FirstOrDefault(action =>
            action.Command.Contains("/фракции", StringComparison.OrdinalIgnoreCase) &&
            action.Label.Contains("Купеческая гильдия", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(guildAction);

        var detail = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            guildAction.Command);
        var detailText = CollectResultText(detail);
        Assert.Contains("Ранги", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Караванная служба", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Кредит доверия", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Долг за спасение каравана", detailText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MortalFactionCardsAvoidDuplicateSelfCardsAndEmptyResourceFields()
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            "/фракции");
        var text = CollectResultText(result);

        Assert.DoesNotContain("Содержание за ход: не указано", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("нужно репутации: 10 Спасти", text, StringComparison.OrdinalIgnoreCase);

        var factionCards = EnumerateEntityCards(result.Blocks)
            .Where(static card =>
                card.Title.Contains("гильд", StringComparison.OrdinalIgnoreCase) ||
                card.Subtitle.Contains("Фракц", StringComparison.OrdinalIgnoreCase) ||
                card.Badges.Any(static badge => badge.Label.Contains("Фракц", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.NotEmpty(factionCards);
        Assert.DoesNotContain(
            factionCards,
            static card => card.Cards.Any(child =>
                string.Equals(child.Title, card.Title, StringComparison.OrdinalIgnoreCase) &&
                child.Subtitle.Contains("Фракц", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task MortalLocationCardsUsePlaceDataWithoutUiInstructionNoise()
    {
        var overview = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            "/локации");

        var locationCards = EnumerateEntityCards(overview.Blocks)
            .Where(static card =>
                card.Subtitle.Contains("Текущ", StringComparison.OrdinalIgnoreCase) ||
                card.Subtitle.Contains("Откры", StringComparison.OrdinalIgnoreCase) ||
                card.Subtitle.Contains("Рядом", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(locationCards);
        Assert.DoesNotContain(locationCards, static card => ContainsUiInstructionCopy(card.Summary));
        Assert.Contains(locationCards, static card =>
            CardContainsText(card, "Роскошные покои") ||
            CardContainsText(card, "Главный коридор") ||
            CardContainsText(card, "Поместье Вальмонт"));

        var detailAction = overview.Actions.FirstOrDefault(action =>
            action.Command.Contains("/локации", StringComparison.OrdinalIgnoreCase) &&
            action.Label.Contains("Покои виконта", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(detailAction);

        var detail = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            detailAction.Command);
        var detailText = CollectResultText(detail);
        Assert.Contains("Роскошные покои", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Вернуться к обзору можно командой", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Подробности доступны", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("adjacencyMap", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("locationStorages", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coordinates", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" indoor", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("помещение", detailText, StringComparison.OrdinalIgnoreCase);

        var storagesAction = detail.Actions.FirstOrDefault(action =>
            action.Command.Contains("/локации", StringComparison.OrdinalIgnoreCase) &&
            action.Command.Contains("хранилищ", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(storagesAction);

        var storages = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            storagesAction.Command);
        var storagesText = CollectResultText(storages);
        Assert.Contains("Приватный письменный стол", storagesText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Старый библиотечный ключ", storagesText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("locationStorages", storagesText, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ExplorerCommandResult> ExecuteFromLoadedSaveAsync(string saveFileName, string command)
    {
        return await _fixture.ExecuteBrowserCommandAsync(saveFileName, command);
    }

    private static void AssertNoPresentationAntiPatterns(string command, ExplorerCommandResult result)
    {
        var violations = new List<string>();
        foreach (var block in result.Blocks)
            CollectPresentationViolations(block, violations, "root");

        Assert.True(
            violations.Count == 0,
            $"{command} browser DTO violates the entity dossier presentation contract:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    private static void CollectPresentationViolations(UiBlock block, List<string> violations, string path)
    {
        switch (block)
        {
            case UiEntityDossierBlock dossier:
                CollectTextViolations(dossier.Title, violations, $"{path}/{dossier.Title}.title");
                CollectTextViolations(dossier.Subtitle, violations, $"{path}/{dossier.Title}.subtitle");
                CollectTextViolations(dossier.Summary, violations, $"{path}/{dossier.Title}.summary");
                foreach (var fact in dossier.Facts)
                    CollectTextViolations(fact.Value, violations, $"{path}/{dossier.Title}.fact[{fact.Label}]");
                foreach (var hint in dossier.Hints)
                    CollectTextViolations(hint.Text, violations, $"{path}/{dossier.Title}.hint[{hint.Title}]");
                foreach (var item in dossier.List)
                    CollectTextViolations(item, violations, $"{path}/{dossier.Title}.list");
                foreach (var card in dossier.Cards)
                    CollectPresentationViolations(card, violations, $"{path}/{dossier.Title}/card[{card.Title}]");
                foreach (var section in dossier.Sections)
                    CollectPresentationViolations(section, violations, $"{path}/{dossier.Title}/section[{section.Title}]");
                break;

            case UiPanelBlock panel:
                foreach (var child in panel.Blocks)
                    CollectPresentationViolations(child, violations, $"{path}/{panel.Title}");
                break;

            case UiKeyValueGridBlock grid:
                foreach (var item in grid.Items)
                    CollectTextViolations(item.Value, violations, $"{path}/grid[{item.Key}]");
                break;

            case UiTableBlock table:
                violations.Add($"{path}/{table.Title}: entity command exposes a raw table instead of dossier cards ({string.Join(", ", table.Columns)})");
                foreach (var row in table.Rows)
                foreach (var cell in row.Cells)
                    CollectTextViolations(cell, violations, $"{path}/{table.Title}.cell");
                break;

            case UiListBlock list:
                foreach (var item in list.Items)
                    CollectTextViolations(item, violations, $"{path}/list");
                break;

            case UiTextBlock text:
                CollectTextViolations(text.Text, violations, $"{path}/text");
                break;
        }
    }

    private static IEnumerable<UiEntityCard> EnumerateEntityCards(IEnumerable<UiBlock> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case UiEntityDossierBlock dossier:
                    foreach (var card in EnumerateEntityCards(dossier))
                        yield return card;
                    break;

                case UiPanelBlock panel:
                    foreach (var card in EnumerateEntityCards(panel.Blocks))
                        yield return card;
                    break;
            }
        }
    }

    private static IEnumerable<UiEntityCard> EnumerateEntityCards(UiEntityDossierBlock dossier)
    {
        foreach (var card in dossier.Cards)
        foreach (var child in EnumerateEntityCards(card))
            yield return child;

        foreach (var section in dossier.Sections)
        {
            foreach (var card in section.Cards)
            foreach (var child in EnumerateEntityCards(card))
                yield return child;

            foreach (var card in EnumerateEntityCards(section.Blocks))
                yield return card;
        }
    }

    private static IEnumerable<UiEntityCard> EnumerateEntityCards(UiEntityCard card)
    {
        yield return card;
        foreach (var child in card.Nested)
        foreach (var nested in EnumerateEntityCards(child))
            yield return nested;
        foreach (var child in card.Cards)
        foreach (var nested in EnumerateEntityCards(child))
            yield return nested;
    }

    private static UiAction? GetCardPrimaryAction(UiEntityCard card)
    {
        var property = typeof(UiEntityCard).GetProperty("PrimaryAction");
        return property?.GetValue(card) as UiAction;
    }

    private static bool ContainsUiInstructionCopy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Contains("выберите", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("доступны в карточке", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("раскрывается", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("раскрыты", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("ниже", StringComparison.OrdinalIgnoreCase);
    }

    private static bool CardContainsText(UiEntityCard card, string expected) =>
        CardPlainText(card).Contains(expected, StringComparison.OrdinalIgnoreCase);

    private static string CardPlainText(UiEntityCard card) =>
        string.Join(
            Environment.NewLine,
            EnumerateCardText(card));

    private static IEnumerable<string> EnumerateCardText(UiEntityCard card)
    {
        yield return card.Title;
        yield return card.Subtitle;
        yield return card.Summary;
        foreach (var badge in card.Badges)
            yield return badge.Label;
        foreach (var fact in card.Facts)
        {
            yield return fact.Label;
            yield return fact.Value;
        }
        foreach (var metric in card.Metrics)
        {
            yield return metric.Label;
            yield return metric.Value.ToString(CultureInfo.InvariantCulture);
            yield return metric.Note;
        }
        foreach (var hint in card.Hints)
        {
            yield return hint.Title;
            yield return hint.Text;
        }
        foreach (var item in card.List)
            yield return item;
        foreach (var child in card.Nested)
        foreach (var value in EnumerateCardText(child))
            yield return value;
        foreach (var child in card.Cards)
        foreach (var value in EnumerateCardText(child))
            yield return value;
    }

    private static string CollectResultText(ExplorerCommandResult result)
    {
        var values = new List<string>();
        foreach (var block in result.Blocks)
            CollectBlockText(block, values);
        foreach (var action in result.Actions)
            values.Add(action.Label);
        return string.Join(Environment.NewLine, values);
    }

    private static void CollectBlockText(UiBlock block, List<string> values)
    {
        switch (block)
        {
            case UiTextBlock text:
                values.Add(text.Text);
                break;
            case UiMessageBlock message:
                values.Add(message.Title);
                values.Add(message.Message);
                break;
            case UiPanelBlock panel:
                values.Add(panel.Title);
                foreach (var child in panel.Blocks)
                    CollectBlockText(child, values);
                break;
            case UiKeyValueGridBlock grid:
                foreach (var item in grid.Items)
                {
                    values.Add(item.Key);
                    values.Add(item.Value);
                }
                break;
            case UiListBlock list:
                values.AddRange(list.Items);
                break;
            case UiTableBlock table:
                values.AddRange(table.Columns);
                foreach (var row in table.Rows)
                foreach (var cell in row.Cells)
                    values.Add(cell);
                break;
            case UiEntityDossierBlock dossier:
                values.Add(dossier.Title);
                values.Add(dossier.Subtitle);
                values.Add(dossier.Summary);
                foreach (var badge in dossier.Badges)
                    values.Add(badge.Label);
                foreach (var fact in dossier.Facts)
                {
                    values.Add(fact.Label);
                    values.Add(fact.Value);
                    values.Add($"{fact.Label}: {fact.Value}");
                }
                foreach (var metric in dossier.Metrics)
                {
                    values.Add(metric.Label);
                    values.Add(metric.Note);
                }
                foreach (var hint in dossier.Hints)
                {
                    values.Add(hint.Title);
                    values.Add(hint.Text);
                }
                values.AddRange(dossier.List);
                foreach (var card in dossier.Cards)
                    CollectCardText(card, values);
                foreach (var section in dossier.Sections)
                {
                    values.Add(section.Title);
                    values.Add(section.Summary);
                    foreach (var fact in section.Facts)
                    {
                        values.Add(fact.Label);
                        values.Add(fact.Value);
                        values.Add($"{fact.Label}: {fact.Value}");
                    }
                    foreach (var card in section.Cards)
                        CollectCardText(card, values);
                    foreach (var child in section.Blocks)
                        CollectBlockText(child, values);
                }
                break;
            case UiMapBlock map:
                values.Add(map.Title);
                values.Add(map.Map.Title);
                values.Add(map.Map.Realm);
                foreach (var node in map.Map.Nodes)
                {
                    values.Add(node.Label);
                    values.Add(node.Type);
                    foreach (var detail in node.Details)
                    {
                        values.Add(detail.Key);
                        values.Add(detail.Value);
                    }
                }
                break;
        }
    }

    private static void CollectCardText(UiEntityCard card, List<string> values)
    {
        values.Add(card.Title);
        values.Add(card.Subtitle);
        values.Add(card.Summary);
        foreach (var badge in card.Badges)
            values.Add(badge.Label);
        foreach (var fact in card.Facts)
        {
            values.Add(fact.Label);
            values.Add(fact.Value);
            values.Add($"{fact.Label}: {fact.Value}");
        }
        foreach (var metric in card.Metrics)
        {
            values.Add(metric.Label);
            values.Add(metric.Note);
        }
        foreach (var hint in card.Hints)
        {
            values.Add(hint.Title);
            values.Add(hint.Text);
        }
        values.AddRange(card.List);
        foreach (var nested in card.Nested)
            CollectCardText(nested, values);
        foreach (var child in card.Cards)
            CollectCardText(child, values);
    }

    private static bool HasFact(UiEntityCard card, string label) =>
        card.Facts.Any(fact => string.Equals(fact.Label, label, StringComparison.OrdinalIgnoreCase));

    private static bool IsDurabilityOnlySummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return false;

        return summary.Contains("Прочность", StringComparison.OrdinalIgnoreCase) &&
               summary.Contains("состояние", StringComparison.OrdinalIgnoreCase);
    }

    private static void CollectPresentationViolations(UiEntityDossierSection section, List<string> violations, string path)
    {
        CollectTextViolations(section.Summary, violations, $"{path}.summary");
        foreach (var fact in section.Facts)
            CollectTextViolations(fact.Value, violations, $"{path}.fact[{fact.Label}]");
        foreach (var hint in section.Hints)
            CollectTextViolations(hint.Text, violations, $"{path}.hint[{hint.Title}]");
        foreach (var item in section.List)
            CollectTextViolations(item, violations, $"{path}.list");
        foreach (var card in section.Cards)
            CollectPresentationViolations(card, violations, $"{path}/card[{card.Title}]");
        foreach (var block in section.Blocks)
            CollectPresentationViolations(block, violations, $"{path}/block");
    }

    private static void CollectPresentationViolations(UiEntityCard card, List<string> violations, string path)
    {
        CollectTextViolations(card.Title, violations, $"{path}.title");
        CollectTextViolations(card.Subtitle, violations, $"{path}.subtitle");
        CollectTextViolations(card.Summary, violations, $"{path}.summary");
        foreach (var fact in card.Facts)
            CollectTextViolations(fact.Value, violations, $"{path}.fact[{fact.Label}]");
        foreach (var hint in card.Hints)
            CollectTextViolations(hint.Text, violations, $"{path}.hint[{hint.Title}]");
        foreach (var item in card.List)
            CollectTextViolations(item, violations, $"{path}.list");
        foreach (var child in card.Nested)
            CollectPresentationViolations(child, violations, $"{path}/nested[{child.Title}]");
        foreach (var child in card.Cards)
            CollectPresentationViolations(child, violations, $"{path}/card[{child.Title}]");
    }

    private static void CollectTextViolations(string? value, List<string> violations, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (FlattenedStructuredTextPattern().IsMatch(value))
            violations.Add($"{path}: flattened structured fields in one string: {TrimForAssertion(value)}");

        if (RawProtocolTokenPattern().IsMatch(value))
            violations.Add($"{path}: raw protocol token leaks into player-facing text: {TrimForAssertion(value)}");
    }

    private static bool IsGenericDetailsColumn(string column)
    {
        var normalized = column.Trim().ToLowerInvariant();
        return normalized is "подробно" or "подробности" or "детали" or "detail" or "details";
    }

    private static string TrimForAssertion(string value)
    {
        var normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 180 ? normalized : normalized[..180] + "...";
    }

    [GeneratedRegex(@";\s*[\p{L}\p{N}_/\- ]{2,40}:\s*\S", RegexOptions.CultureInvariant)]
    private static partial Regex FlattenedStructuredTextPattern();

    [GeneratedRegex(@"\b(?:DTO|Ui[A-Z]\w+|game_state/|pending_|debug|internal|[a-z]+[A-Z][a-zA-Z]+|[a-z]+_[a-z0-9_]+)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RawProtocolTokenPattern();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])\d+%/100%\s+\d+%/100%(?![\p{L}\p{N}])", RegexOptions.CultureInvariant)]
    private static partial Regex AdjacentCombatPercentagesPattern();

    [GeneratedRegex(@"\b\d{1,2}:\s+\d{2}\b", RegexOptions.CultureInvariant)]
    private static partial Regex TimeWithWhitespaceAfterColonPattern();

    [GeneratedRegex(@"нарастает\s+Неизвестный", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GluedRivalStatusSummaryPattern();
}
