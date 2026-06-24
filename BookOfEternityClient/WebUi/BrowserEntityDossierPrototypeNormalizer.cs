using System.Text.RegularExpressions;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.WebUi;

internal static class BrowserEntityDossierPrototypeNormalizer
{
    private static readonly IReadOnlyDictionary<string, string> ProtocolValueLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Mortal World"] = "Смертный мир",
            ["Mortal Realm"] = "Смертный мир",
            ["Chaos Sea"] = "Море Хаоса",
            ["Shining Abode"] = "Сияющая Обитель",
            ["Month of Beginnings"] = "Месяц Начал",
            ["Common"] = "обычное",
            ["Uncommon"] = "необычное",
            ["Rare"] = "редкое",
            ["Epic"] = "эпическое",
            ["Legendary"] = "легендарное",
            ["Unique"] = "уникальное",
            ["Trash"] = "хлам",
            ["Finger1"] = "Кольцо 1",
            ["Finger2"] = "Кольцо 2",
            ["MainHand"] = "Основная рука",
            ["OffHand"] = "Вторая рука",
            ["Hands"] = "Руки",
            ["Head"] = "Голова",
            ["Chest"] = "Тело",
            ["Back"] = "Спина",
            ["Feet"] = "Ноги",
            ["Accessory"] = "Аксессуар",
            ["outdoor"] = "открытая местность",
            ["indoor"] = "помещение",
            ["exploration"] = "исследование",
            ["combat"] = "бой",
            ["social"] = "социальное",
            ["environment"] = "окружение",
            ["artifacts"] = "артефакты",
            ["factions"] = "фракции",
            ["scene"] = "сцена",
            ["journaled"] = "занесено в журнал",
            ["journal"] = "журнал",
            ["alliance"] = "союз",
            ["defense"] = "защита",
            ["guard"] = "защита",
            ["pressure"] = "давление",
            ["enlightenment"] = "просветление",
            ["visible"] = "видимый",
            ["hidden"] = "скрытый",
            ["rumor"] = "слух",
            ["local"] = "местные новости",
            ["melee"] = "ближняя",
            ["piercing"] = "колющий",
            ["position"] = "позиция",
            ["mark"] = "метка",
            ["won"] = "победа",
            ["rising"] = "нарастает",
            ["Counter"] = "контрприём",
            ["KnowledgeBased"] = "основан на знаниях",
            ["Memory"] = "память",
            ["Humanoid"] = "гуманоид",
            ["Rival"] = "соперник",
            ["hardRules"] = "Жёсткие правила",
            ["character_chronicle"] = "Хроника персонажа",
            ["canonicalName"] = "Каноническое имя",
            ["requiredElements"] = "Обязательные элементы",
            ["forbiddenElements"] = "Запрещённые элементы",
            ["specialMechanics"] = "Особые механики",
            ["continuityNotes"] = "Заметки непрерывности",
            ["playerAmendments"] = "Правки игрока",
            ["settingSummary"] = "Кратко о мире",
            ["sourceProfileName"] = "Исходный профиль",
            ["lastUpdated"] = "Последнее обновление",
            ["totalUnlocked"] = "Открыто всего",
            ["byCategory"] = "По категориям",
            ["byRarity"] = "По редкости",
            ["totalEntries"] = "Всего записей",
            ["player_chronicle"] = "Хроника игрока",
            ["plot_outline"] = "Сюжетный план",
            ["nameVariants"] = "Варианты имени",
            ["default"] = "по умолчанию",
            ["feminine"] = "женский вариант",
            ["masculine"] = "мужской вариант",
            ["neutral"] = "нейтральный вариант",
            ["manifestation"] = "Проявление",
            ["currentDisplayName"] = "Текущее имя",
            ["formFlexibility"] = "Гибкость формы",
            ["currentPresentationStyle"] = "Текущий образ",
            ["currentPronouns"] = "Местоимения",
            ["appearanceDescription"] = "Описание облика",
            ["personalityProfile"] = "Профиль личности",
            ["speechPattern"] = "Манера речи",
            ["coreValues"] = "Главные ценности",
            ["domain"] = "Домен",
            ["abode"] = "Обитель",
            ["chaosSeaNavigation"] = "Навигация Моря Хаоса",
            ["currentAbodeName"] = "Текущая Обитель",
            ["currentRoute"] = "Текущий путь",
            ["knownAbodes"] = "Известные Обители",
            ["secret"] = "секретное",
            ["focus"] = "фокус",
            ["world_profiles"] = "профилей миров",
            ["world_rules"] = "правила мира",
            ["playerCompanionDirective"] = "директива компаньона",
            ["afterlifeSpecialArtLearningReceipts"] = "записи обучения духовным искусствам",
            ["afterlifeSpiritualConflictUpdate"] = "обновление духовного конфликта",
            ["notificationId"] = "метка уведомления",
            ["return_to_chaos_sea"] = "возвращение в Море Хаоса",
            ["client-owned"] = "локальный",
            ["client-authored"] = "локально созданный"
        };

    public static ExplorerCommandResult Normalize(ExplorerCommandResult result)
    {
        return new ExplorerCommandResult
        {
            Command = result.Command,
            State = result.State,
            Blocks = NormalizeBlocks(result.Blocks, convertPanels: true),
            Actions = CloneActions(result.Actions),
            Prompts = ClonePrompts(result.Prompts),
            Notifications = CloneNotifications(result.Notifications),
            InteractiveSession = result.InteractiveSession
        };
    }

    private static List<UiBlock> NormalizeBlocks(IEnumerable<UiBlock> blocks, bool convertPanels)
    {
        var normalized = new List<UiBlock>();
        foreach (var block in blocks)
            normalized.Add(NormalizeBlock(block, convertPanels));
        return normalized;
    }

    private static UiBlock NormalizeBlock(UiBlock block, bool convertPanels)
    {
        return block switch
        {
            UiEntityDossierBlock dossier => NormalizeDossier(dossier),
            UiPanelBlock panel when convertPanels => PanelToDossier(panel),
            UiPanelBlock panel => NormalizePanel(panel, convertPanels),
            UiTableBlock table => TableToDossier(table),
            UiTextBlock text => new UiTextBlock
            {
                Text = SanitizePlayerFacingValue(text.Text),
                Tone = text.Tone
            },
            UiMessageBlock message => new UiMessageBlock
            {
                Severity = message.Severity,
                Title = SanitizePlayerFacingValue(message.Title),
                Message = SanitizePlayerFacingValue(message.Message)
            },
            UiKeyValueGridBlock grid => new UiKeyValueGridBlock
            {
                Items = grid.Items
                    .Select(static item => new UiKeyValueItem
                    {
                        Key = SanitizePlayerFacingValue(item.Key),
                        Value = SanitizePlayerFacingValue(item.Value)
                    })
                    .Where(static item => !IsRawProtocolOnly(item.Value))
                    .ToList()
            },
            UiListBlock list => new UiListBlock
            {
                Ordered = list.Ordered,
                Items = CloneList(list.Items)
            },
            _ => block
        };
    }

    private static UiPanelBlock NormalizePanel(UiPanelBlock panel, bool convertPanels)
    {
        return new UiPanelBlock
        {
            Title = SanitizePlayerFacingValue(panel.Title),
            Blocks = NormalizeBlocks(panel.Blocks, convertPanels)
        };
    }

    private static UiEntityDossierBlock NormalizeDossier(UiEntityDossierBlock dossier)
    {
        return new UiEntityDossierBlock
        {
            EntityType = dossier.EntityType,
            Title = SanitizePlayerFacingValue(dossier.Title),
            Subtitle = SanitizePlayerFacingValue(dossier.Subtitle),
            Summary = SanitizePlayerFacingValue(dossier.Summary),
            Badges = CloneBadges(dossier.Badges),
            Media = dossier.Media,
            Facts = CloneFacts(dossier.Facts),
            Metrics = CloneMetrics(dossier.Metrics),
            Hints = CloneHints(dossier.Hints),
            List = CloneList(dossier.List),
            Cards = dossier.Cards.Select(NormalizeCard).ToList(),
            Sections = dossier.Sections.Select(NormalizeSection).ToList(),
            PrimaryAction = CloneAction(dossier.PrimaryAction)
        };
    }

    private static UiEntityDossierSection NormalizeSection(UiEntityDossierSection section)
    {
        var parts = new PrototypeParts
        {
            Facts = CloneFacts(section.Facts),
            Metrics = CloneMetrics(section.Metrics),
            Hints = CloneHints(section.Hints),
            List = CloneList(section.List),
            Cards = section.Cards.Select(NormalizeCard).ToList()
        };

        foreach (var block in section.Blocks)
            CollectBlock(block, parts);

        return new UiEntityDossierSection
        {
            Id = section.Id,
            Title = SanitizePlayerFacingValue(section.Title),
            Summary = SanitizePlayerFacingValue(section.Summary),
            Icon = section.Icon,
            CollectionLabel = string.IsNullOrWhiteSpace(section.CollectionLabel)
                ? BuildCollectionLabel(parts.Cards.Count)
                : section.CollectionLabel,
            Presentation = section.Presentation,
            Collapsible = section.Collapsible || parts.Cards.Count > 8,
            InitiallyExpanded = section.InitiallyExpanded,
            Facts = parts.Facts,
            Metrics = parts.Metrics,
            Hints = parts.Hints,
            List = parts.List,
            Cards = parts.Cards,
            Blocks = parts.FallbackBlocks
        };
    }

    private static void CollectBlock(UiBlock block, PrototypeParts parts)
    {
        switch (block)
        {
            case UiEntityDossierBlock dossier:
                parts.Cards.Add(DossierToCard(NormalizeDossier(dossier)));
                break;

            case UiKeyValueGridBlock grid:
                foreach (var item in grid.Items)
                    AddKeyValue(parts, item.Key, item.Value);
                break;

            case UiListBlock list:
                parts.List.AddRange(list.Items.Where(static item => !string.IsNullOrWhiteSpace(item)));
                break;

            case UiTextBlock text when !string.IsNullOrWhiteSpace(text.Text):
                parts.Hints.Add(new UiEntityHint
                {
                    Title = "Заметка",
                    Text = text.Text,
                    Tone = text.Tone
                });
                break;

            case UiMessageBlock message:
                parts.Hints.Add(new UiEntityHint
                {
                    Title = string.IsNullOrWhiteSpace(message.Title) ? "Сообщение" : message.Title,
                    Text = message.Message,
                    Tone = SeverityToTone(message.Severity)
                });
                break;

            case UiPanelBlock panel:
                parts.Cards.Add(PanelToCard(panel));
                break;

            case UiImageBlock image:
                parts.Cards.Add(ImageToCard(image));
                break;

            case UiTableBlock table:
                parts.Cards.Add(TableToCard(table));
                break;

            case UiMapBlock:
                parts.FallbackBlocks.Add(NormalizeBlock(block, convertPanels: true));
                break;
        }
    }

    private static void AddKeyValue(PrototypeParts parts, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        var label = SanitizePlayerFacingValue(key);
        var sanitizedValue = SanitizePlayerFacingValue(value);

        if (IsRawProtocolOnly(sanitizedValue))
            return;

        if (AddStructuredDetailValue(parts, sanitizedValue, IsGenericDetailKey(label) ? null : label))
            return;

        if (TryParseMetric(label, sanitizedValue, out var metric))
        {
            parts.Metrics.Add(metric);
            return;
        }

        parts.Facts.Add(new UiEntityFact
        {
            Label = label,
            Value = string.IsNullOrWhiteSpace(sanitizedValue) ? "—" : sanitizedValue
        });
    }

    private static UiEntityCard DossierToCard(UiEntityDossierBlock dossier)
    {
        var summary = SanitizePlayerFacingValue(dossier.Summary);
        var facts = CloneFacts(dossier.Facts);
        var list = CloneList(dossier.List);
        var sections = dossier.Sections;
        var mergeGenericInfoSections = IsNpcSectionEntry(dossier);

        if (mergeGenericInfoSections && LooksLikeStructuredDetailValue(summary))
        {
            var summaryParts = new PrototypeParts();
            if (AddStructuredDetailValue(summaryParts, summary, contextTitle: dossier.Subtitle, rowTitle: dossier.Title))
            {
                summary = string.Empty;
                MergePartsIntoCardCollections(summaryParts, facts, list);
            }
        }

        if (mergeGenericInfoSections)
        {
            foreach (var section in sections.Where(IsGenericInfoSection))
            {
                MergeFacts(facts, section.Facts);
                MergeList(list, CloneList(section.List));
            }
        }

        return new UiEntityCard
        {
            Title = SanitizePlayerFacingValue(dossier.Title),
            Subtitle = SanitizePlayerFacingValue(dossier.Subtitle),
            Summary = summary,
            Icon = string.IsNullOrWhiteSpace(dossier.EntityType) ? "default" : dossier.EntityType,
            Badges = CloneBadges(dossier.Badges),
            Media = dossier.Media,
            Facts = facts,
            Metrics = CloneMetrics(dossier.Metrics),
            Hints = CloneHints(dossier.Hints),
            List = list,
            Nested = sections
                .Where(section => !mergeGenericInfoSections || !IsGenericInfoSection(section))
                .Select(SectionToNestedCard)
                .Where(static card => card != null)
                .Cast<UiEntityCard>()
                .ToList(),
            Cards = dossier.Cards.Select(NormalizeCard).ToList(),
            PrimaryAction = CloneAction(dossier.PrimaryAction)
        };
    }

    private static bool AddStructuredDetailValue(
        PrototypeParts parts,
        string value,
        string? cardTitle = null,
        string? contextTitle = null,
        string? rowTitle = null)
    {
        if (!LooksLikeStructuredDetailValue(value))
            return false;

        var detailParts = SplitStructuredDetailValue(value).ToList();
        if (detailParts.Count == 0)
            return false;

        var target = parts;
        UiEntityCard? nestedCard = null;
        if (!string.IsNullOrWhiteSpace(cardTitle))
        {
            target = new PrototypeParts();
            nestedCard = new UiEntityCard
            {
                Title = cardTitle,
                Icon = "archive",
                Facts = target.Facts,
                Metrics = target.Metrics,
                Hints = target.Hints,
                List = target.List,
                Cards = target.Cards
            };
        }

        var added = false;
        var scalarIndex = 0;
        foreach (var part in detailParts)
        {
            var playerPart = SanitizePlayerFacingValue(part);
            if (IsRawProtocolOnly(playerPart))
                continue;

            if (TrySplitStructuredDetailPair(part, out var key, out var pairValue))
            {
                var label = SanitizePlayerFacingValue(key);
                var playerValue = SanitizePlayerFacingValue(pairValue);
                if (IsRawProtocolOnly(playerValue))
                    continue;

                AddFactUnique(target.Facts, new UiEntityFact { Label = label, Value = playerValue });
                added = true;
                continue;
            }

            var scalarLabel = InferStructuredDetailScalarLabel(contextTitle, rowTitle ?? cardTitle, scalarIndex, playerPart);
            if (string.IsNullOrWhiteSpace(scalarLabel))
            {
                AddListItemUnique(target.List, playerPart);
            }
            else
            {
                AddFactUnique(target.Facts, new UiEntityFact { Label = scalarLabel, Value = playerPart });
            }

            scalarIndex++;
            added = true;
        }

        if (!added)
            return false;

        if (nestedCard != null)
        {
            if (!HasAnyContent(nestedCard))
                return false;

            parts.Cards.Add(nestedCard);
        }

        return true;
    }

    private static string InferStructuredDetailScalarLabel(string? contextTitle, string? rowTitle, int scalarIndex, string value)
    {
        if (IsSkillContext(contextTitle, rowTitle))
        {
            if (IsSkillTypeValue(value))
                return "Тип навыка";
            if (IsRarityValue(value))
                return "Редкость";
            if (IsIntegerText(value))
                return "Ранг";

            return scalarIndex switch
            {
                0 => "Название навыка",
                1 => "Описание",
                _ => "Свойство навыка"
            };
        }

        if (IsFateCardContext(contextTitle, rowTitle))
        {
            if (IsRarityValue(value))
                return "Редкость";

            return scalarIndex switch
            {
                0 => "Название карты",
                1 => "Описание",
                _ => "Свойство карты"
            };
        }

        if (IsMemoryContext(contextTitle, rowTitle))
        {
            if (IsRarityValue(value))
                return "Редкость";

            return scalarIndex switch
            {
                0 => "Название воспоминания",
                1 => "Описание",
                _ => "Свойство воспоминания"
            };
        }

        if (IsStateContext(contextTitle, rowTitle))
        {
            if (IsRarityValue(value))
                return "Редкость";

            return scalarIndex switch
            {
                0 => "Название состояния",
                1 => "Описание",
                _ => "Свойство состояния"
            };
        }

        return string.Empty;
    }

    private static bool IsSkillContext(string? contextTitle, string? rowTitle)
    {
        var context = ((contextTitle ?? string.Empty) + " " + (rowTitle ?? string.Empty)).ToLowerInvariant();
        return context.Contains("навык", StringComparison.Ordinal) ||
               context.Contains("skill", StringComparison.Ordinal);
    }

    private static bool IsFateCardContext(string? contextTitle, string? rowTitle)
    {
        var context = ((contextTitle ?? string.Empty) + " " + (rowTitle ?? string.Empty)).ToLowerInvariant();
        return context.Contains("карта судьбы", StringComparison.Ordinal) ||
               context.Contains("fate", StringComparison.Ordinal);
    }

    private static bool IsMemoryContext(string? contextTitle, string? rowTitle)
    {
        var context = ((contextTitle ?? string.Empty) + " " + (rowTitle ?? string.Empty)).ToLowerInvariant();
        return context.Contains("воспомин", StringComparison.Ordinal) ||
               context.Contains("memory", StringComparison.Ordinal);
    }

    private static bool IsStateContext(string? contextTitle, string? rowTitle)
    {
        var context = ((contextTitle ?? string.Empty) + " " + (rowTitle ?? string.Empty)).ToLowerInvariant();
        return context.Contains("особое состояние", StringComparison.Ordinal) ||
               context.Contains("state", StringComparison.Ordinal);
    }

    private static bool IsSkillTypeValue(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "основан на знаниях" or "утилитарный" or "боевой" or "социальный";
    }

    private static bool IsRarityValue(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "обычный" or "обычное" or "необычный" or "необычное" or "редкий" or "редкое" or
            "эпический" or "эпическое" or "легендарный" or "легендарное" or "уникальный" or "уникальное";
    }

    private static bool IsIntegerText(string value) =>
        int.TryParse(value.Trim(), out _);

    private static IEnumerable<string> SplitStructuredDetailValue(string value) =>
        value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static part => !string.IsNullOrWhiteSpace(part) && part != "—");

    private static bool TrySplitStructuredDetailPair(string value, out string key, out string pairValue)
    {
        key = string.Empty;
        pairValue = string.Empty;
        var separator = FindStructuredDetailSeparator(value);
        if (separator <= 0 || separator >= value.Length - 1)
            return false;

        var candidateKey = value[..separator].Trim();
        var candidateValue = value[(separator + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(candidateKey) || string.IsNullOrWhiteSpace(candidateValue))
            return false;

        if (candidateKey.All(static ch => char.IsDigit(ch)))
            return false;

        if (candidateKey.Contains(',', StringComparison.Ordinal))
            return false;

        key = candidateKey;
        pairValue = candidateValue;
        return true;
    }

    private static int FindStructuredDetailSeparator(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != ':')
                continue;

            var previousIsDigit = index > 0 && char.IsDigit(value[index - 1]);
            var nextIsDigit = index + 1 < value.Length && char.IsDigit(value[index + 1]);
            if (previousIsDigit && nextIsDigit)
                continue;

            return index;
        }

        return -1;
    }

    private static bool LooksLikeStructuredDetailValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (value.Contains(';', StringComparison.Ordinal))
            return true;

        return TrySplitStructuredDetailPair(value, out _, out _);
    }

    private static bool IsGenericDetailKey(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        return normalized is "подробности" or "сведения" or "детали" or "detail" or "details";
    }

    private static bool IsNpcSectionEntry(UiEntityDossierBlock dossier) =>
        string.Equals(dossier.EntityType, "npc-section-entry", StringComparison.OrdinalIgnoreCase);

    private static bool IsGenericInfoSection(UiEntityDossierSection section)
    {
        var normalized = section.Title.Trim().ToLowerInvariant();
        return normalized is "сведения" or "подробности" or "детали" or "details";
    }

    private static void MergePartsIntoCardCollections(
        PrototypeParts parts,
        List<UiEntityFact> facts,
        List<string> list)
    {
        MergeFacts(facts, parts.Facts);
        MergeList(list, parts.List);
    }

    private static void MergeFacts(List<UiEntityFact> target, IEnumerable<UiEntityFact> facts)
    {
        foreach (var fact in facts)
            AddFactUnique(target, fact);
    }

    private static void MergeList(List<string> target, IEnumerable<string> items)
    {
        foreach (var item in items)
            AddListItemUnique(target, item);
    }

    private static void AddFactUnique(List<UiEntityFact> target, UiEntityFact fact)
    {
        if (string.IsNullOrWhiteSpace(fact.Label) || string.IsNullOrWhiteSpace(fact.Value))
            return;

        if (target.Any(existing =>
            string.Equals(existing.Label, fact.Label, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Value, fact.Value, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        target.Add(new UiEntityFact { Label = fact.Label, Value = fact.Value });
    }

    private static void AddListItemUnique(List<string> target, string item)
    {
        if (string.IsNullOrWhiteSpace(item))
            return;

        if (target.Any(existing => string.Equals(existing, item, StringComparison.OrdinalIgnoreCase)))
            return;

        target.Add(item);
    }

    private static UiEntityCard? SectionToNestedCard(UiEntityDossierSection section)
    {
        if (string.IsNullOrWhiteSpace(section.Title) &&
            section.Facts.Count == 0 &&
            section.Metrics.Count == 0 &&
            section.Hints.Count == 0 &&
            section.List.Count == 0 &&
            section.Cards.Count == 0)
        {
            return null;
        }

        return new UiEntityCard
        {
            Title = string.IsNullOrWhiteSpace(section.Title) ? "Раздел" : section.Title,
            Summary = SanitizePlayerFacingValue(section.Summary),
            Icon = string.IsNullOrWhiteSpace(section.Icon) ? "section" : section.Icon,
            Facts = CloneFacts(section.Facts),
            Metrics = CloneMetrics(section.Metrics),
            Hints = CloneHints(section.Hints),
            List = CloneList(section.List),
            Cards = section.Cards.Select(NormalizeCard).ToList()
        };
    }

    private static UiEntityDossierBlock PanelToDossier(UiPanelBlock panel)
    {
        var card = PanelToCard(panel);
        return new UiEntityDossierBlock
        {
            EntityType = "panel",
            Title = card.Title,
            Subtitle = card.Subtitle,
            Summary = card.Summary,
            Badges = CloneBadges(card.Badges),
            Media = card.Media,
            Facts = CloneFacts(card.Facts),
            Metrics = CloneMetrics(card.Metrics),
            Hints = CloneHints(card.Hints),
            List = CloneList(card.List),
            Cards = card.Cards,
            Sections = card.Nested.Count == 0
                ? []
                : [
                    new UiEntityDossierSection
                    {
                        Id = Slug(card.Title) + "-details",
                        Title = "Подробности",
                        Icon = "archive",
                        Presentation = "cards",
                        Collapsible = card.Nested.Count > 4,
                        InitiallyExpanded = true,
                        Cards = card.Nested
                    }
                ]
        };
    }

    private static UiEntityDossierBlock TableToDossier(UiTableBlock table)
    {
        return new UiEntityDossierBlock
        {
            EntityType = "collection",
            Title = string.IsNullOrWhiteSpace(table.Title) ? "Список" : SanitizePlayerFacingValue(table.Title),
            Sections =
            [
                TableToSection(table)
            ]
        };
    }

    private static UiEntityCard TableToCard(UiTableBlock table)
    {
        var section = TableToSection(table);
        return new UiEntityCard
        {
            Title = string.IsNullOrWhiteSpace(table.Title) ? "Список" : SanitizePlayerFacingValue(table.Title),
            Icon = "archive",
            Cards = section.Cards
        };
    }

    private static UiEntityDossierSection TableToSection(UiTableBlock table)
    {
        var cards = new List<UiEntityCard>();
        foreach (var row in table.Rows)
        {
            var facts = new List<UiEntityFact>();
            var list = new List<string>();
            var nested = new List<UiEntityCard>();
            for (var index = 0; index < row.Cells.Count && index < table.Columns.Count; index++)
            {
                var column = table.Columns[index];
                var value = row.Cells[index];
                if (ShouldHideTableColumn(column))
                    continue;

                var label = SanitizePlayerFacingValue(column);
                var sanitizedValue = SanitizePlayerFacingValue(value);
                if (IsRawProtocolOnly(sanitizedValue))
                    continue;

                var parts = new PrototypeParts();
                if (AddStructuredDetailValue(parts, sanitizedValue, IsGenericDetailKey(label) ? null : label))
                {
                    MergeFacts(facts, parts.Facts);
                    MergeList(list, parts.List);
                    nested.AddRange(parts.Cards);
                    continue;
                }

                AddFactUnique(facts, new UiEntityFact { Label = label, Value = sanitizedValue });
            }

            var title = BuildTableRowTitle(table, row, facts, list);
            var card = new UiEntityCard
            {
                Title = title,
                Icon = "archive",
                Facts = facts,
                List = list,
                Nested = nested
            };

            if (HasAnyContent(card))
                cards.Add(card);
        }

        return new UiEntityDossierSection
        {
            Id = Slug(table.Title),
            Title = string.IsNullOrWhiteSpace(table.Title) ? "Список" : SanitizePlayerFacingValue(table.Title),
            Icon = "archive",
            Presentation = cards.Count > 8 ? "collection" : "cards",
            CollectionLabel = BuildCollectionLabel(cards.Count),
            Collapsible = cards.Count > 8,
            InitiallyExpanded = true,
            Cards = cards
        };
    }

    private static string BuildTableRowTitle(
        UiTableBlock table,
        UiTableRow row,
        IReadOnlyCollection<UiEntityFact> facts,
        IReadOnlyCollection<string> list)
    {
        foreach (var fact in facts)
        {
            if (!IsRawProtocolOnly(fact.Value))
                return fact.Value;
        }

        foreach (var item in list)
        {
            if (!IsRawProtocolOnly(item))
                return item;
        }

        foreach (var cell in row.Cells)
        {
            var value = SanitizePlayerFacingValue(cell);
            if (!IsRawProtocolOnly(value))
                return value;
        }

        return string.IsNullOrWhiteSpace(table.Title) ? "Запись" : SanitizePlayerFacingValue(table.Title);
    }

    private static bool ShouldHideTableColumn(string column)
    {
        var normalized = column.Trim().ToLowerInvariant();
        return normalized is "id" or "ид" or "метка" or "команда" or "command";
    }

    private static UiEntityCard PanelToCard(UiPanelBlock panel)
    {
        var parts = new PrototypeParts();
        foreach (var child in panel.Blocks)
            CollectBlock(child, parts);

        return new UiEntityCard
        {
            Title = string.IsNullOrWhiteSpace(panel.Title) ? "Раздел" : panel.Title,
            Icon = "archive",
            Facts = parts.Facts,
            Metrics = parts.Metrics,
            Hints = parts.Hints,
            List = parts.List,
            Cards = parts.Cards,
            Nested = parts.FallbackBlocks
                .Select(static block => new UiEntityCard
                {
                    Title = block switch
                    {
                        UiTableBlock table => string.IsNullOrWhiteSpace(table.Title) ? "Таблица" : table.Title,
                        UiMapBlock map => string.IsNullOrWhiteSpace(map.Title) ? "Карта" : map.Title,
                        _ => "Подробности"
                    },
                    Icon = "archive"
                })
                .ToList()
        };
    }

    private static UiEntityCard ImageToCard(UiImageBlock image)
    {
        return new UiEntityCard
        {
            Title = string.IsNullOrWhiteSpace(image.Title) ? "Изображение" : image.Title,
            Subtitle = "образ",
            Icon = "memory",
            Media = new UiEntityMedia
            {
                Title = image.Title,
                Url = image.Url,
                MediaId = image.MediaId,
                RelativePath = image.RelativePath,
                AltText = image.AltText,
                ContentType = image.ContentType,
                Length = image.Length,
                ModifiedAtUtc = image.ModifiedAtUtc
            }
        };
    }

    private static UiEntityCard NormalizeCard(UiEntityCard card)
    {
        return new UiEntityCard
        {
            Title = SanitizePlayerFacingValue(card.Title),
            Subtitle = SanitizePlayerFacingValue(card.Subtitle),
            Summary = SanitizePlayerFacingValue(card.Summary),
            Icon = card.Icon,
            Badges = CloneBadges(card.Badges),
            Media = card.Media,
            Facts = CloneFacts(card.Facts),
            Metrics = CloneMetrics(card.Metrics),
            Hints = CloneHints(card.Hints),
            List = CloneList(card.List),
            Nested = card.Nested.Select(NormalizeCard).ToList(),
            Cards = card.Cards.Select(NormalizeCard).ToList(),
            PrimaryAction = CloneAction(card.PrimaryAction)
        };
    }

    private static UiAction? CloneAction(UiAction? action)
    {
        if (action == null)
            return null;

        return new UiAction
        {
            Id = action.Id,
            Label = SanitizePlayerFacingValue(action.Label),
            Command = action.Command,
            Style = action.Style,
            RequiresConfirmation = action.RequiresConfirmation,
            Payload = action.Payload?.DeepClone()
        };
    }

    private static List<UiAction> CloneActions(IEnumerable<UiAction> actions) =>
        actions
            .Select(CloneAction)
            .Where(static action => action != null)
            .Cast<UiAction>()
            .ToList();

    private static List<UiPrompt> ClonePrompts(IEnumerable<UiPrompt> prompts) =>
        prompts
            .Select(ClonePrompt)
            .ToList();

    private static UiPrompt ClonePrompt(UiPrompt prompt) =>
        prompt switch
        {
            UiSelectionPrompt selection => new UiSelectionPrompt
            {
                Id = selection.Id,
                Prompt = SanitizePlayerFacingValue(selection.Prompt),
                Required = selection.Required,
                Options = selection.Options.Select(CloneSelectionOption).ToList(),
                AllowCustom = selection.AllowCustom
            },
            UiConfirmationPrompt confirmation => new UiConfirmationPrompt
            {
                Id = confirmation.Id,
                Prompt = SanitizePlayerFacingValue(confirmation.Prompt),
                Required = confirmation.Required,
                DefaultValue = confirmation.DefaultValue
            },
            UiTextInputPrompt text => new UiTextInputPrompt
            {
                Id = text.Id,
                Prompt = SanitizePlayerFacingValue(text.Prompt),
                Required = text.Required,
                DefaultValue = text.DefaultValue,
                Placeholder = SanitizePlayerFacingValue(text.Placeholder)
            },
            UiLongTextInputPrompt text => new UiLongTextInputPrompt
            {
                Id = text.Id,
                Prompt = SanitizePlayerFacingValue(text.Prompt),
                Required = text.Required,
                DefaultValue = text.DefaultValue,
                Placeholder = SanitizePlayerFacingValue(text.Placeholder),
                MinLines = text.MinLines,
                MaxLines = text.MaxLines
            },
            _ => prompt
        };

    private static UiSelectionOption CloneSelectionOption(UiSelectionOption option) =>
        new()
        {
            Value = option.Value,
            Label = SanitizePlayerFacingValue(option.Label),
            Description = SanitizePlayerFacingValue(option.Description),
            Disabled = option.Disabled
        };

    private static List<UiNotification> CloneNotifications(IEnumerable<UiNotification> notifications) =>
        notifications
            .Select(static notification => new UiNotification
            {
                Severity = notification.Severity,
                Title = SanitizePlayerFacingValue(notification.Title),
                Message = SanitizePlayerFacingValue(notification.Message)
            })
            .ToList();

    private static bool HasAnyContent(UiEntityCard card) =>
        !string.IsNullOrWhiteSpace(card.Title) ||
        !string.IsNullOrWhiteSpace(card.Subtitle) ||
        !string.IsNullOrWhiteSpace(card.Summary) ||
        card.Badges.Count > 0 ||
        card.Media != null ||
        card.Facts.Count > 0 ||
        card.Metrics.Count > 0 ||
        card.Hints.Count > 0 ||
        card.List.Count > 0 ||
        card.Nested.Count > 0 ||
        card.Cards.Count > 0;

    private static string SanitizePlayerFacingValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var result = value.Trim();
        result = TranslateKnownProtocolTokens(result);
        result = StructuredBonusDisplay.FormatScalar(result);
        result = result
            .Replace("display_snapshot", "снимок для отображения", StringComparison.OrdinalIgnoreCase)
            .Replace("last_known_snapshot", "последний известный снимок", StringComparison.OrdinalIgnoreCase)
            .Replace("guardian_forced", "принуждение хранителя", StringComparison.OrdinalIgnoreCase)
            .Replace("actionPointCost", "стоимость в очках действия", StringComparison.OrdinalIgnoreCase)
            .Replace("baseDamage", "базовый урон", StringComparison.OrdinalIgnoreCase)
            .Replace("damageType", "тип урона", StringComparison.OrdinalIgnoreCase)
            .Replace("actionDescription", "описание действия", StringComparison.OrdinalIgnoreCase)
            .Replace("actionName", "название действия", StringComparison.OrdinalIgnoreCase)
            .Replace("cooldown", "перезарядка", StringComparison.OrdinalIgnoreCase)
            .Replace("remainingUses", "осталось применений", StringComparison.OrdinalIgnoreCase)
            .Replace("break_binding", "разрыв связи", StringComparison.OrdinalIgnoreCase)
            .Replace("range", "дистанция", StringComparison.OrdinalIgnoreCase);

        result = TranslateKnownProtocolTokens(result);
        result = RemoveEnglishSlashCommandHints(result);
        result = RemoveRawParentheticalSuffix(result);
        result = RemoveRawCommaSeparatedFragments(result);
        result = RemoveRawInlineIdentifiers(result);
        result = RemoveRawTrailingTokens(result);
        return result;
    }

    private static string RemoveEnglishSlashCommandHints(string value)
    {
        if (!HasCyrillic(value))
            return value;

        var result = Regex.Replace(
            value,
            @"\s*/[a-z_]+(?:\s*<[^>]+>)?",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return CollapseWhitespaceAroundPunctuation(result);
    }

    private static string TranslateKnownProtocolTokens(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var result = value;
        foreach (var (source, label) in ProtocolValueLabels.OrderByDescending(static pair => pair.Key.Length))
        {
            result = Regex.Replace(
                result,
                $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(source)}(?![\p{{L}}\p{{N}}_])",
                label,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return result;
    }

    private static string RemoveRawParentheticalSuffix(string value)
    {
        var result = value;
        while (result.EndsWith(")", StringComparison.Ordinal))
        {
            var open = result.LastIndexOf(" (", StringComparison.Ordinal);
            if (open < 0)
                break;

            var inner = result[(open + 2)..^1];
            if (!IsRawProtocolOnly(inner))
                break;

            result = result[..open].TrimEnd();
        }

        return result;
    }

    private static string RemoveRawCommaSeparatedFragments(string value)
    {
        if (!value.Contains(',', StringComparison.Ordinal) || !HasCyrillic(value))
            return value;

        var parts = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static part => !IsRawProtocolOnly(part))
            .ToList();

        return parts.Count == 0 ? value : string.Join(", ", parts);
    }

    private static string RemoveRawInlineIdentifiers(string value)
    {
        if (!HasCyrillic(value))
            return value;

        var result = Regex.Replace(
            value,
            @"(?<![\p{L}\p{N}_])(?:[a-z]+_[a-z0-9_]{2,}|[a-z]+[A-Z][a-zA-Z]{2,})(?![\p{L}\p{N}_])",
            string.Empty,
            RegexOptions.CultureInvariant);

        return CollapseWhitespaceAroundPunctuation(result);
    }

    private static string RemoveRawTrailingTokens(string value)
    {
        if (!HasCyrillic(value))
            return value;

        var result = value.Trim();
        while (true)
        {
            var separator = result.LastIndexOf(' ');
            if (separator < 0)
                return result;

            var lastToken = result[(separator + 1)..];
            if (!IsRawProtocolOnly(lastToken))
                return result;

            result = result[..separator].TrimEnd();
        }
    }

    private static string CollapseWhitespaceAroundPunctuation(string value)
    {
        var result = value;
        result = Regex.Replace(result, @"\s{2,}", " ", RegexOptions.CultureInvariant);
        result = Regex.Replace(result, @"\(\s*[;,:]\s*", "(", RegexOptions.CultureInvariant);
        result = Regex.Replace(result, @"\(\s+", "(", RegexOptions.CultureInvariant);
        result = Regex.Replace(result, @"\s+\)", ")", RegexOptions.CultureInvariant);
        result = Regex.Replace(result, @"\(\s*\)", string.Empty, RegexOptions.CultureInvariant);
        result = Regex.Replace(result, @"\s+([,.;:])", "$1", RegexOptions.CultureInvariant);
        var punctuated = result;
        result = Regex.Replace(
            punctuated,
            @"([,.;:])(?=\S)",
            match =>
            {
                var index = match.Index;
                if (index > 0 &&
                    index + 1 < punctuated.Length &&
                    char.IsDigit(punctuated[index - 1]) &&
                    char.IsDigit(punctuated[index + 1]))
                {
                    return match.Value;
                }

                return match.Value + " ";
            },
            RegexOptions.CultureInvariant);
        result = Regex.Replace(result, @"\s{2,}", " ", RegexOptions.CultureInvariant);
        return result.Trim();
    }

    private static bool IsRawProtocolOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var trimmed = value.Trim();
        if (trimmed == "—")
            return true;

        if (trimmed.StartsWith("/", StringComparison.Ordinal))
            return true;

        if (trimmed.Contains("::", StringComparison.Ordinal))
            return true;

        if (!HasCyrillic(trimmed) &&
            trimmed.Contains(':', StringComparison.Ordinal) &&
            (trimmed.Contains('_', StringComparison.Ordinal) || trimmed.Contains('-', StringComparison.Ordinal)))
        {
            return true;
        }

        if (trimmed.Contains("game_state/", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("pending_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (HasCyrillic(trimmed))
            return false;

        if (Regex.IsMatch(trimmed, @"^[+-]?\d+(?:[.,]\d+)?%?$", RegexOptions.CultureInvariant))
            return false;

        if (Regex.IsMatch(trimmed, @"^\d+\s*[-–—]\s*\d+$", RegexOptions.CultureInvariant))
            return false;

        if (trimmed.All(static ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.'))
            return trimmed.Contains('_', StringComparison.Ordinal) || trimmed.Contains('-', StringComparison.Ordinal);

        return false;
    }

    private static bool HasCyrillic(string value) =>
        value.Any(static ch => ch is >= '\u0400' and <= '\u04FF');

    private static string Slug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "section";

        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(static ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        var slug = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "section" : slug;
    }

    private static bool TryParseMetric(string key, string value, out UiEntityMetric metric)
    {
        metric = new UiEntityMetric();
        var trimmed = value.Trim();
        if (trimmed.EndsWith('%') &&
            double.TryParse(trimmed[..^1].Trim(), out var percent))
        {
            metric = new UiEntityMetric
            {
                Label = key,
                Value = Math.Clamp(percent, 0, 100),
                Max = 100,
                Tone = MetricTone(key, percent)
            };
            return true;
        }

        var slashIndex = trimmed.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex > 0 &&
            double.TryParse(trimmed[..slashIndex].Trim(), out var valuePart) &&
            double.TryParse(trimmed[(slashIndex + 1)..].Trim(), out var maxPart) &&
            maxPart > 0)
        {
            metric = new UiEntityMetric
            {
                Label = key,
                Value = valuePart,
                Max = maxPart,
                Tone = MetricTone(key, valuePart / maxPart * 100),
                Note = trimmed
            };
            return true;
        }

        return false;
    }

    private static UiTone MetricTone(string key, double percent)
    {
        if (percent <= 33)
            return UiTone.Error;
        if (percent <= 66)
            return UiTone.Warning;

        var normalized = key.ToLowerInvariant();
        if (normalized.Contains("здоров", StringComparison.Ordinal) ||
            normalized.Contains("health", StringComparison.Ordinal))
        {
            return UiTone.Success;
        }

        return UiTone.Accent;
    }

    private static UiTone SeverityToTone(UiNotificationSeverity severity) =>
        severity switch
        {
            UiNotificationSeverity.Success => UiTone.Success,
            UiNotificationSeverity.Warning => UiTone.Warning,
            UiNotificationSeverity.Error => UiTone.Error,
            _ => UiTone.Default
        };

    private static string BuildCollectionLabel(int cardCount) =>
        cardCount > 0
            ? $"{FormatRussianCount(cardCount, "объект", "объекта", "объектов")} в разделе"
            : string.Empty;

    private static string FormatRussianCount(int count, string singular, string paucal, string plural)
    {
        var abs = Math.Abs(count);
        var lastTwo = abs % 100;
        var last = abs % 10;
        var word = lastTwo is >= 11 and <= 14
            ? plural
            : last switch
            {
                1 => singular,
                2 or 3 or 4 => paucal,
                _ => plural
            };
        return $"{count} {word}";
    }

    private static List<UiEntityBadge> CloneBadges(IEnumerable<UiEntityBadge> badges) =>
        badges
            .Select(static badge => new UiEntityBadge
            {
                Label = SanitizePlayerFacingValue(badge.Label),
                Tone = badge.Tone,
                Icon = badge.Icon
            })
            .ToList();

    private static List<UiEntityFact> CloneFacts(IEnumerable<UiEntityFact> facts) =>
        facts
            .Select(static fact => new UiEntityFact
            {
                Label = SanitizePlayerFacingValue(fact.Label),
                Value = SanitizePlayerFacingValue(fact.Value)
            })
            .Where(static fact => !IsRawProtocolOnly(fact.Value))
            .ToList();

    private static List<UiEntityMetric> CloneMetrics(IEnumerable<UiEntityMetric> metrics) =>
        metrics
            .Select(static metric => new UiEntityMetric
            {
                Label = SanitizePlayerFacingValue(metric.Label),
                Value = metric.Value,
                Max = metric.Max,
                Tone = metric.Tone,
                Note = SanitizePlayerFacingValue(metric.Note)
            })
            .ToList();

    private static List<UiEntityHint> CloneHints(IEnumerable<UiEntityHint> hints) =>
        hints
            .Select(static hint => new UiEntityHint
            {
                Title = SanitizePlayerFacingValue(hint.Title),
                Text = SanitizePlayerFacingValue(hint.Text),
                Tone = hint.Tone
            })
            .Where(static hint => !IsRawProtocolOnly(hint.Text))
            .ToList();

    private static List<string> CloneList(IEnumerable<string> items) =>
        items
            .Select(static item => SanitizePlayerFacingValue(item))
            .Where(static item => !IsRawProtocolOnly(item))
            .ToList();

    private sealed class PrototypeParts
    {
        public List<UiEntityFact> Facts { get; init; } = [];
        public List<UiEntityMetric> Metrics { get; init; } = [];
        public List<UiEntityHint> Hints { get; init; } = [];
        public List<string> List { get; init; } = [];
        public List<UiEntityCard> Cards { get; init; } = [];
        public List<UiBlock> FallbackBlocks { get; init; } = [];
    }
}
