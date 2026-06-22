using BookOfEternityClient.CommandProtocol;

namespace BookOfEternityClient.WebUi;

internal static class BrowserEntityDossierPrototypeNormalizer
{
    public static ExplorerCommandResult Normalize(ExplorerCommandResult result)
    {
        if (result.Prompts.Count > 0 || result.InteractiveSession != null)
            return result;

        return new ExplorerCommandResult
        {
            Command = result.Command,
            State = result.State,
            Blocks = NormalizeBlocks(result.Blocks, convertPanels: true),
            Actions = result.Actions,
            Prompts = result.Prompts,
            Notifications = result.Notifications,
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
            Sections = dossier.Sections.Select(NormalizeSection).ToList()
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
            if (AddStructuredDetailValue(summaryParts, summary))
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
            Cards = dossier.Cards.Select(NormalizeCard).ToList()
        };
    }

    private static bool AddStructuredDetailValue(PrototypeParts parts, string value, string? cardTitle = null)
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

            AddListItemUnique(target.List, playerPart);
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

    private static IEnumerable<string> SplitStructuredDetailValue(string value) =>
        value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static part => !string.IsNullOrWhiteSpace(part) && part != "—");

    private static bool TrySplitStructuredDetailPair(string value, out string key, out string pairValue)
    {
        key = string.Empty;
        pairValue = string.Empty;
        var separator = value.IndexOf(':', StringComparison.Ordinal);
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
            Cards = card.Cards.Select(NormalizeCard).ToList()
        };
    }

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
            .Replace("range", "дистанция", StringComparison.OrdinalIgnoreCase)
            .Replace("guard", "защита", StringComparison.OrdinalIgnoreCase)
            .Replace("pressure", "давление", StringComparison.OrdinalIgnoreCase)
            .Replace("enlightenment", "просветление", StringComparison.OrdinalIgnoreCase)
            .Replace("visible", "видимый", StringComparison.OrdinalIgnoreCase)
            .Replace("hidden", "скрытый", StringComparison.OrdinalIgnoreCase)
            .Replace("rumor", "слух", StringComparison.OrdinalIgnoreCase)
            .Replace("local", "местные новости", StringComparison.OrdinalIgnoreCase)
            .Replace("melee", "ближняя", StringComparison.OrdinalIgnoreCase)
            .Replace("piercing", "колющий", StringComparison.OrdinalIgnoreCase);

        result = RemoveRawParentheticalSuffix(result);
        result = RemoveRawCommaSeparatedFragments(result);
        result = RemoveRawTrailingTokens(result);
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
