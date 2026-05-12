using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private const int SpiritualArtMaxTier = 5;

    private enum SpiritualArtCurrency
    {
        InkFeathers,
        LightSparks
    }

    private sealed record SpiritualArtUpgradeQuote(
        AfterlifeSpiritualConflictState.SpiritualArtDefinition Art,
        int CurrentTier,
        int NextTier,
        int MaxUnlockedTier,
        int InkFeatherCost,
        int LightSparkCost,
        string RequiredRankLabel,
        string? BlockReason);

    private async Task ShowSpiritualConflictAsync()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Духовный конфликт"))
            return;

        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Духовный конфликт", "Духовный конфликт посмертия (afterlife spiritual conflict) доступен только в Море Хаоса и Сияющей Обители.");
            return;
        }

        await _stateManager.RefreshGameStateAsync();
        var root = await ReadJsonObjectForAfterlifeStatusAsync(AfterlifeSpiritualConflictState.StatePath);
        var active = root?["activeConflict"] as JsonObject;

        var lines = new List<string>
        {
            "[bold cyan]Духовный конфликт посмертия[/] [dim](afterlife spiritual conflict)[/]",
            "",
            "Это отдельная загробная система конфликтов. Она не использует файлы смертного боя (Mortal combat files), HP, energy, enemiesData/alliesData или смертные боевые навыки.",
            "Конфликт начинает GM по роли: по заявке игрока или когда актор посмертия (afterlife actor) сам инициирует давление.",
            ""
        };

        if (active == null)
        {
            lines.Add("[dim]Активного духовного конфликта нет.[/]");
            lines.Add("");
            lines.Add("GM может начать конфликт только через поверхность ответа принятого хода (accepted-turn response surface):");
            lines.Add($"  • `{AfterlifeSpiritualConflictState.ResponseField}` с `mode=start`");
            lines.Add($"  • сохраняемое состояние (persisted state): `{AfterlifeSpiritualConflictState.StatePath}`");
        }
        else
        {
            var conflictId = AfterlifeSpiritualConflictState.GetNodeString(active["conflictId"]) ?? "unknown";
            lines.Add($"[bold]Активный конфликт:[/] [white]{Markup.Escape(conflictId)}[/]");
            lines.Add($"  • Область (realm): [white]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(active["realm"]) ?? "?")}[/]");
            lines.Add($"  • Модель сторон (sideModel): [white]{Markup.Escape(FormatSideModelLabel(AfterlifeSpiritualConflictState.GetNodeString(active["sideModel"])))}[/]");
            lines.Add($"  • Позиция конфликта (conflictPosition): [white]{Markup.Escape(FormatConflictPositionLabel(AfterlifeSpiritualConflictState.GetNodeString(active["conflictPosition"])))}[/]");
            lines.Add($"  • Напряжение стороны игрока (playerSideStrain): [white]{Markup.Escape(FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(active["playerSideStrain"])))}[/]");
            lines.Add($"  • Напряжение противостоящей стороны (oppositionSideStrain): [white]{Markup.Escape(FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(active["oppositionSideStrain"])))}[/]");
            lines.Add($"  • Состояние завершения (resolutionState): [white]{Markup.Escape(FormatResolutionStateLabel(AfterlifeSpiritualConflictState.GetNodeString(active["resolutionState"])))}[/]");
            lines.Add("");
            AppendConflictSideSummary(lines, "Сторона игрока (playerSide)", active["playerSide"] as JsonObject);
            AppendConflictSideSummary(lines, "Противостоящая сторона (oppositionSide)", active["oppositionSide"] as JsonObject);
            lines.Add("");
            lines.Add($"  • Записано обменов действиями (exchangeLog): [white]{(active["exchangeLog"] as JsonArray)?.Count ?? 0}[/]");
        }

        lines.Add("");
        lines.Add("[bold]Команды:[/]");
        lines.Add("  • /spiritual_action — отправить действие в активном духовном конфликте с явным тегом для GM.");
        lines.Add("  • Обычная художественная заявка во время активного конфликта тоже должна резолвиться GM как действие конфликта.");
        lines.Add("  • /spiritual_arts — посмотреть ранги, уровни искусств (art tiers) и применимые действия.");

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" ⚔ Духовный конфликт посмертия ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        if (root != null)
            WriteJsonAuditPanel($"Полный JSON {AfterlifeSpiritualConflictState.StatePath}", root, Color.Cyan1);

        WaitForKey();
    }

    private async Task ShowSpiritualArtsAsync()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Духовные искусства"))
            return;

        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Духовные искусства", "Духовные искусства посмертия (Spiritual Arts) доступны только в Море Хаоса и Сияющей Обители.");
            return;
        }

        while (true)
        {
            await _stateManager.RefreshGameStateAsync();
            var soulRoot = await ReadJsonObjectForAfterlifeStatusAsync("game_state/meta/soul_state.json");
            if (soulRoot == null)
            {
                ShowEmptyPanel("Духовные искусства", "game_state/meta/soul_state.json недоступен; прокачка духовных искусств заблокирована.");
                WaitForKey();
                return;
            }

            var shiningRoot = await ReadJsonObjectForAfterlifeStatusAsync(ShiningAbodeState.StatePath);
            var profile = BuildSyncedAfterlifeCombatProfile(soulRoot, shiningRoot);
            var quotes = BuildSpiritualArtUpgradeQuotes(profile);

            Clear();
            Write(BuildSpiritualArtsPanel(soulRoot, shiningRoot, profile, quotes));
            WriteJsonAuditPanel("Полный JSON afterlifeCombatProfile", profile, Color.Cyan1);

            var choice = Prompt(new SelectionPrompt<string>()
                .Title("[bold cyan]Действие духовных искусств[/]")
                .HighlightStyle(new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1))
                .AddChoices(
                    "⬆ Прокачать духовное искусство",
                    "← Назад"));

            if (!choice.Contains("Прокачать", StringComparison.OrdinalIgnoreCase))
                return;

            await HandleSpiritualArtUpgradeAsync(soulRoot, shiningRoot, quotes);
        }
    }

    private Panel BuildSpiritualArtsPanel(
        JsonObject soulRoot,
        JsonObject? shiningRoot,
        JsonObject profile,
        IReadOnlyList<SpiritualArtUpgradeQuote> quotes)
    {
        var enlightenment = soulRoot["enlightenment"] as JsonObject;
        var radiance = shiningRoot?["radiance"] as JsonObject;
        var artTiers = profile["artTiers"] as JsonObject;
        var maxUnlockedTier = quotes.Count == 0 ? 0 : quotes.Max(quote => quote.MaxUnlockedTier);

        var lines = new List<string>
        {
            "[bold cyan]Духовные искусства посмертия[/] [dim](Spiritual Arts)[/]",
            "",
            "[bold]Текущий боевой профиль:[/]",
            $"  • Ранг Просветления (Enlightenment rank): [white]{AfterlifeSpiritualConflictState.GetNodeInt(profile["enlightenmentRank"])}[/]",
            $"  • Ранг Сияния (Radiance rank): [white]{AfterlifeSpiritualConflictState.GetNodeInt(profile["radianceRank"])}[/]",
            $"  • Сохранённый ранг Сияния (Retained Radiance rank): [white]{AfterlifeSpiritualConflictState.GetNodeInt(profile["retainedRadianceRank"])}[/]",
            $"  • Уровень Просветления души: [white]{AfterlifeSpiritualConflictState.GetNodeInt(enlightenment?["level"])}[/] [dim]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(enlightenment?["currentTier"]) ?? "")}[/]",
            $"  • Сияние Сияющей Обители (Shining radiance): [white]{AfterlifeSpiritualConflictState.GetNodeInt(radiance?["experience"])} XP[/] / tier [white]{AfterlifeSpiritualConflictState.GetNodeInt(radiance?["tier"])}[/]",
            $"  • Максимальный открытый уровень искусства (art tier): [white]{maxUnlockedTier}[/]",
            $"  • Доступные Чернильные Перья (Ink Feathers): [white]{ShiningAbodeState.GetSoulSpendableInkFeathers(soulRoot)}[/]",
            $"  • Искры Света (Light Sparks): [gold1]{AfterlifeSpiritualConflictState.GetNodeInt(shiningRoot?["lightSparks"])}[/] [dim](тратятся только в обычной активной Сияющей Обители)[/]",
            "",
            "[bold]Искусства:[/]"
        };

        foreach (var quote in quotes)
        {
            var tier = AfterlifeSpiritualConflictState.GetNodeInt(artTiers?[quote.Art.ArtId]);
            var blocked = quote.BlockReason == null
                ? $"следующий уровень {quote.NextTier}, цена {quote.InkFeatherCost} 🪶"
                : $"заблокировано: {quote.BlockReason}";
            var sparkCost = _stateManager.CurrentState.IsInShiningAbode ? $" / {quote.LightSparkCost} ✨" : "";
            lines.Add($"  • [white]{Markup.Escape(FormatSpiritualArtLabel(quote.Art))}[/]: уровень (tier) [white]{tier}[/], порог ранга (rank gate) [white]{quote.RequiredRankLabel}[/], {Markup.Escape(blocked)}{Markup.Escape(sparkCost)} — {Markup.Escape(FormatSpiritualArtUse(quote.Art))}");
        }

        lines.Add("");
        lines.Add("[bold]Лестница рангов Просветления (Enlightenment ranks):[/]");
        foreach (var rank in AfterlifeSpiritualConflictState.EnlightenmentRanks)
            lines.Add($"  • {rank.Rank}: {Markup.Escape(FormatRankIdLabel(rank.RankId))}, требует {rank.RequiredProgress}, открывает уровень искусства (art tier) {rank.UnlocksArtTier}. {Markup.Escape(FormatRankMechanicalEffect(rank.MechanicalEffect))}");

        lines.Add("");
        lines.Add("[bold]Лестница рангов Сияния (Radiance ranks):[/]");
        foreach (var rank in AfterlifeSpiritualConflictState.RadianceRanks)
            lines.Add($"  • {rank.Rank}: {Markup.Escape(FormatRankIdLabel(rank.RankId))}, требует {rank.RequiredProgress}, открывает уровень искусства (art tier) {rank.UnlocksArtTier}. {Markup.Escape(FormatRankMechanicalEffect(rank.MechanicalEffect))}");

        lines.Add("");
        lines.Add("[dim]Правило прокачки: ранги ограничивают максимальный уровень искусства (art tier); клиент локально пишет soul_state.afterlifeCombatProfile и тратит выбранную валюту. GM не пишет receipt/report прокачки.[/]");

        return new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" ✨ Духовные искусства ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        };
    }

    private async Task HandleSpiritualArtUpgradeAsync(
        JsonObject soulRoot,
        JsonObject? shiningRoot,
        IReadOnlyList<SpiritualArtUpgradeQuote> quotes)
    {
        var blocker = await TryDescribeSpiritualArtUpgradeBlockerAsync();
        if (blocker != null)
        {
            ShowEmptyPanel("Прокачка духовных искусств", blocker);
            WaitForKey();
            return;
        }

        var choicesByLabel = quotes.ToDictionary(
            quote => BuildSpiritualArtUpgradeChoiceLabel(quote),
            quote => quote,
            StringComparer.Ordinal);
        choicesByLabel["← Назад"] = null!;

        var selected = Prompt(new SelectionPrompt<string>()
            .Title("[bold cyan]Выберите духовное искусство для прокачки[/]")
            .HighlightStyle(new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1))
            .PageSize(12)
            .AddChoices(choicesByLabel.Keys));

        if (!choicesByLabel.TryGetValue(selected, out var quote) || quote == null)
            return;

        if (quote.BlockReason != null)
        {
            ShowEmptyPanel("Прокачка духовных искусств", quote.BlockReason);
            WaitForKey();
            return;
        }

        var currency = PromptSpiritualArtCurrency(quote, soulRoot, shiningRoot);
        if (currency == null)
            return;

        var beforeSoulRoot = soulRoot.DeepClone().AsObject();
        var beforeShiningRoot = shiningRoot?.DeepClone()?.AsObject();
        var projectedSoulRoot = soulRoot.DeepClone().AsObject();
        var projectedShiningRoot = shiningRoot?.DeepClone()?.AsObject();
        var result = ApplySpiritualArtUpgrade(projectedSoulRoot, projectedShiningRoot, quote, currency.Value);
        if (!result.Success)
        {
            ShowEmptyPanel("Прокачка духовных искусств", result.Message);
            WaitForKey();
            return;
        }

        Write(BuildSpiritualArtUpgradePreviewPanel(beforeSoulRoot, beforeShiningRoot, projectedSoulRoot, projectedShiningRoot, quote, currency.Value));
        WriteJsonAuditPanel("JSON локальной прокачки духовного искусства", BuildSpiritualArtUpgradeAuditNode(beforeSoulRoot, beforeShiningRoot, projectedSoulRoot, projectedShiningRoot, quote, currency.Value), Color.Cyan1);

        if (!Confirm("[yellow]Подтвердить локальную прокачку духовного искусства?[/]", false))
        {
            MarkupLine("[dim]Прокачка отменена; состояние не изменено.[/]");
            WaitForKey();
            return;
        }

        if (!await SaveSpiritualArtUpgradeRootsAsync(projectedSoulRoot, projectedShiningRoot, currency.Value))
        {
            WaitForKey();
            return;
        }

        MarkupLine($"[green]Прокачано: {Markup.Escape(FormatSpiritualArtLabel(quote.Art))}, уровень (tier) {quote.CurrentTier} -> {quote.NextTier}.[/]");
        WaitForKey();
    }

    private async Task<string?> TryDescribeSpiritualArtUpgradeBlockerAsync()
    {
        var activeTurnArtifacts = new List<string>();
        if (_fs.FileExists("input/turn_request.json"))
            activeTurnArtifacts.Add("input/turn_request.json");
        if (_fs.FileExists("game_state/control/pending_turn_snapshot.json"))
            activeTurnArtifacts.Add("game_state/control/pending_turn_snapshot.json");
        if (HasAnyShiningTreasuryPendingTurnSnapshotFile())
            activeTurnArtifacts.Add("game_state/control/pending_turn_snapshot");
        if (activeTurnArtifacts.Count > 0)
        {
            return "Прокачка духовных искусств заблокирована: найден активный GM-turn lifecycle (жизненный цикл хода GM). " +
                   "Локальная прокачка меняет принадлежащий клиенту (client-owned) soul_state.afterlifeCombatProfile и валюту, поэтому дождитесь завершения, отмены или repair текущего хода. " +
                   $"Найдено: {string.Join(", ", activeTurnArtifacts)}.";
        }

        var conflictRead = await ReadJsonObjectForAfterlifeStatusResultAsync(AfterlifeSpiritualConflictState.StatePath);
        if (conflictRead.Error != null)
        {
            return $"Прокачка духовных искусств заблокирована: {AfterlifeSpiritualConflictState.StatePath} повреждён ({conflictRead.Error}). Сначала выполните repair (ремонт состояния).";
        }

        if (conflictRead.Root?["activeConflict"] is JsonObject)
        {
            return "Прокачка духовных искусств заблокирована: сейчас активен духовный конфликт посмертия (afterlife spiritual conflict). Завершите exchange (обмен действиями), resolve (разрешение) или repair_cancel (ремонтную отмену) перед изменением боевого профиля.";
        }

        if (conflictRead.Root != null &&
            conflictRead.Root.TryGetPropertyValue("activeConflict", out var activeConflict) &&
            activeConflict != null)
        {
            return $"Прокачка духовных искусств заблокирована: {AfterlifeSpiritualConflictState.StatePath}.activeConflict повреждён. Сначала выполните repair (ремонт состояния).";
        }

        if (_fs.FileExists(GuardianAbodeOfferingState.PendingRequestPath))
        {
            return $"Прокачка духовных искусств заблокирована: найден незакрытый контракт с зарезервированной ценой (cost-bearing contract) {GuardianAbodeOfferingState.PendingRequestPath}. Дождитесь закрытия со status=accepted | refused или repair.";
        }

        foreach (var archivePath in new[] { AfterlifeArchiveActionState.ConsultationRequestPath, AfterlifeArchiveActionState.ProjectFuelRequestPath })
        {
            if (_fs.FileExists(archivePath))
                return $"Прокачка духовных искусств заблокирована: найден незакрытый контракт Архива с зарезервированной ценой {archivePath}. Дождитесь закрытия со status=accepted | rejected | cancelled или repair.";
        }

        if (_stateManager.CurrentState.IsInShiningAbode)
        {
            var shiningBlocker = await TryDescribeShiningTreasuryPendingCostBlockerAsync();
            if (shiningBlocker != null)
                return "Прокачка духовных искусств заблокирована из-за незакрытого контракта Сияющей Обители с зарезервированной ценой (Shining cost-bearing pending contract). " + shiningBlocker;
        }

        return null;
    }

    private SpiritualArtCurrency? PromptSpiritualArtCurrency(
        SpiritualArtUpgradeQuote quote,
        JsonObject soulRoot,
        JsonObject? shiningRoot)
    {
        var choices = new List<string>
        {
            $"Чернильные Перья — {quote.InkFeatherCost} 🪶",
        };

        if (_stateManager.CurrentState.IsInShiningAbode && shiningRoot != null)
            choices.Add($"Искры Света — {quote.LightSparkCost} ✨");

        choices.Add("← Назад");

        var selected = Prompt(new SelectionPrompt<string>()
            .Title("[bold cyan]Выберите валюту прокачки[/]")
            .HighlightStyle(new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1))
            .AddChoices(choices));

        if (selected.Contains("Назад", StringComparison.OrdinalIgnoreCase))
            return null;

        return selected.Contains("Искры", StringComparison.OrdinalIgnoreCase)
            ? SpiritualArtCurrency.LightSparks
            : SpiritualArtCurrency.InkFeathers;
    }

    private static (bool Success, string Message) ApplySpiritualArtUpgrade(
        JsonObject soulRoot,
        JsonObject? shiningRoot,
        SpiritualArtUpgradeQuote quote,
        SpiritualArtCurrency currency)
    {
        if (quote.BlockReason != null)
            return (false, quote.BlockReason);

        var profile = BuildSyncedAfterlifeCombatProfile(soulRoot, shiningRoot);
        var artTiers = profile["artTiers"] as JsonObject ?? new JsonObject();
        artTiers[quote.Art.ArtId] = quote.NextTier;
        profile["artTiers"] = artTiers;
        soulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty] = profile;

        if (currency == SpiritualArtCurrency.InkFeathers)
        {
            if (!TrySpendSoulInkFeathers(soulRoot, quote.InkFeatherCost, out var reason))
                return (false, reason);
        }
        else
        {
            if (shiningRoot == null)
                return (false, "Искры Света доступны для прокачки только в Сияющей Обители.");

            var current = AfterlifeSpiritualConflictState.GetNodeInt(shiningRoot["lightSparks"]);
            if (current < quote.LightSparkCost)
                return (false, $"Недостаточно Искр Света: нужно {quote.LightSparkCost}, доступно {current}.");

            shiningRoot["lightSparks"] = current - quote.LightSparkCost;
        }

        return (true, "ok");
    }

    private async Task<bool> SaveSpiritualArtUpgradeRootsAsync(
        JsonObject soulRoot,
        JsonObject? shiningRoot,
        SpiritualArtCurrency currency)
    {
        var blocker = await TryDescribeSpiritualArtUpgradeBlockerAsync();
        if (blocker != null)
        {
            ShowEmptyPanel("Прокачка духовных искусств", blocker);
            return false;
        }

        var previousSoulJson = await _fs.ReadFileAsync(SoulStatePath);
        var previousShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        JsonObject? previousSoulRoot = null;

        if (!string.IsNullOrWhiteSpace(previousSoulJson))
        {
            try
            {
                previousSoulRoot = JsonNode.Parse(previousSoulJson) as JsonObject;
            }
            catch
            {
                previousSoulRoot = null;
            }

            if (previousSoulRoot == null)
            {
                MarkupLine("[red]Прокачка духовных искусств не может сохранить операцию: текущий soul_state.json нечитаем. Сначала исправь состояние души.[/]");
                return false;
            }
        }

        try
        {
            await WriteCanonicalSoulStateJsonAsync(soulRoot);
            if (currency == SpiritualArtCurrency.LightSparks && shiningRoot != null)
                await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

            await _stateManager.RefreshGameStateAsync();
            return true;
        }
        catch (Exception ex)
        {
            if (previousSoulJson == null)
                _fs.DeleteFile(SoulStatePath);
            else if (previousSoulRoot != null)
                await WriteCanonicalSoulStateJsonAsync(previousSoulRoot);
            else
                _fs.DeleteFile(SoulStatePath);

            if (currency == SpiritualArtCurrency.LightSparks)
            {
                if (previousShiningJson != null)
                    await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, previousShiningJson);
                else
                    _fs.DeleteFile(ShiningAbodeState.StatePath);
            }

            MarkupLine($"[red]Не удалось сохранить прокачку духовного искусства; состояние восстановлено: {Markup.Escape(ex.Message)}[/]");
            return false;
        }
    }

    private static JsonObject BuildSyncedAfterlifeCombatProfile(JsonObject soulRoot, JsonObject? shiningRoot)
    {
        var profile = soulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty]?.DeepClone() as JsonObject
                      ?? AfterlifeSpiritualConflictState.CreateDefaultCombatProfile();

        if (profile["schemaVersion"] is not JsonValue)
            profile["schemaVersion"] = 1;
        if (profile["artTiers"] is not JsonObject)
            profile["artTiers"] = new JsonObject();

        var enlightenmentRank = ResolveEnlightenmentRank(soulRoot);
        var radianceRank = ResolveRadianceRank(shiningRoot);
        var previousRetained = AfterlifeSpiritualConflictState.GetNodeInt(profile["retainedRadianceRank"]);

        profile["enlightenmentRank"] = enlightenmentRank;
        profile["radianceRank"] = radianceRank;
        profile["retainedRadianceRank"] = shiningRoot != null
            ? Math.Max(previousRetained, radianceRank)
            : previousRetained;
        if (!profile.ContainsKey("lastRecoveryTurn"))
            profile["lastRecoveryTurn"] = 0;

        return profile;
    }

    private static IReadOnlyList<SpiritualArtUpgradeQuote> BuildSpiritualArtUpgradeQuotes(JsonObject profile)
    {
        var maxUnlockedTier = ResolveMaxUnlockedSpiritualArtTier(profile);
        var artTiers = profile["artTiers"] as JsonObject;
        var result = new List<SpiritualArtUpgradeQuote>();
        foreach (var art in AfterlifeSpiritualConflictState.SpiritualArts)
        {
            var currentTier = Math.Clamp(AfterlifeSpiritualConflictState.GetNodeInt(artTiers?[art.ArtId]), 0, SpiritualArtMaxTier);
            var nextTier = Math.Min(SpiritualArtMaxTier, currentTier + 1);
            var requiredRankLabel = DescribeRequiredRankForArtTier(Math.Max(art.MinUnlockTier, nextTier));
            string? blockReason = null;
            if (currentTier >= SpiritualArtMaxTier)
                blockReason = "уже достигнут максимальный уровень искусства (tier 5)";
            else if (maxUnlockedTier < art.MinUnlockTier)
                blockReason = $"нужен ранг, открывающий уровень искусства (art tier) {art.MinUnlockTier}: {DescribeRequiredRankForArtTier(art.MinUnlockTier)}";
            else if (nextTier > maxUnlockedTier)
                blockReason = $"нужен ранг, открывающий уровень искусства (art tier) {nextTier}: {requiredRankLabel}";

            result.Add(new SpiritualArtUpgradeQuote(
                art,
                currentTier,
                nextTier,
                maxUnlockedTier,
                ComputeSpiritualArtInkFeatherCost(art, nextTier),
                ComputeSpiritualArtLightSparkCost(art, nextTier),
                requiredRankLabel,
                blockReason));
        }

        return result;
    }

    private static int ResolveMaxUnlockedSpiritualArtTier(JsonObject profile)
    {
        var enlightenmentRank = AfterlifeSpiritualConflictState.GetNodeInt(profile["enlightenmentRank"]);
        var radianceRank = AfterlifeSpiritualConflictState.GetNodeInt(profile["radianceRank"]);
        var retainedRadianceRank = AfterlifeSpiritualConflictState.GetNodeInt(profile["retainedRadianceRank"]);
        return Math.Clamp(
            Math.Max(
                ResolveUnlockedTierFromRanks(AfterlifeSpiritualConflictState.EnlightenmentRanks, enlightenmentRank),
                Math.Max(
                    ResolveUnlockedTierFromRanks(AfterlifeSpiritualConflictState.RadianceRanks, radianceRank),
                    ResolveUnlockedTierFromRanks(AfterlifeSpiritualConflictState.RadianceRanks, retainedRadianceRank))),
            0,
            SpiritualArtMaxTier);
    }

    private static int ResolveUnlockedTierFromRanks(
        IReadOnlyList<AfterlifeSpiritualConflictState.RankDefinition> ranks,
        int rank)
    {
        return ranks
            .Where(definition => definition.Rank <= rank)
            .Select(definition => definition.UnlocksArtTier)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static int ResolveEnlightenmentRank(JsonObject soulRoot)
    {
        var directProgress = AfterlifeSpiritualConflictState.GetNodeInt(soulRoot["enlightenment"]);
        var enlightenment = soulRoot["enlightenment"] as JsonObject;
        var soulProgression = soulRoot["soulProgression"] as JsonObject;
        var progress = Math.Max(
            Math.Max(directProgress, AfterlifeSpiritualConflictState.GetNodeInt(enlightenment?["experience"])),
            Math.Max(
                AfterlifeSpiritualConflictState.GetNodeInt(soulProgression?["totalExperience"]),
                AfterlifeSpiritualConflictState.GetNodeInt(soulProgression?["progressPercent"])));
        var tier = Math.Max(
            AfterlifeSpiritualConflictState.GetNodeInt(enlightenment?["level"]),
            AfterlifeSpiritualConflictState.GetNodeInt(soulProgression?["tier"]));
        return Math.Clamp(
            Math.Max(tier, ResolveRankFromProgress(AfterlifeSpiritualConflictState.EnlightenmentRanks, progress)),
            0,
            AfterlifeSpiritualConflictState.EnlightenmentRanks.Max(rank => rank.Rank));
    }

    private static int ResolveRadianceRank(JsonObject? shiningRoot)
    {
        var radiance = shiningRoot?["radiance"] as JsonObject;
        var progress = AfterlifeSpiritualConflictState.GetNodeInt(radiance?["experience"]);
        var tier = AfterlifeSpiritualConflictState.GetNodeInt(radiance?["tier"]);
        return Math.Clamp(
            Math.Max(tier, ResolveRankFromProgress(AfterlifeSpiritualConflictState.RadianceRanks, progress)),
            0,
            AfterlifeSpiritualConflictState.RadianceRanks.Max(rank => rank.Rank));
    }

    private static int ResolveRankFromProgress(
        IReadOnlyList<AfterlifeSpiritualConflictState.RankDefinition> ranks,
        int progress)
    {
        return ranks
            .Where(rank => progress >= rank.RequiredProgress)
            .Select(rank => rank.Rank)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static string DescribeRequiredRankForArtTier(int tier)
    {
        var enlightenmentRank = AfterlifeSpiritualConflictState.EnlightenmentRanks
            .FirstOrDefault(rank => rank.UnlocksArtTier >= tier);
        var radianceRank = AfterlifeSpiritualConflictState.RadianceRanks
            .FirstOrDefault(rank => rank.UnlocksArtTier >= tier);

        var parts = new List<string>();
        if (enlightenmentRank != null)
            parts.Add($"Просветление (Enlightenment) {enlightenmentRank.Rank} `{enlightenmentRank.RankId}`");
        if (radianceRank != null)
            parts.Add($"Сияние (Radiance) {radianceRank.Rank} `{radianceRank.RankId}`");

        return parts.Count == 0 ? "не открывается текущими шкалами" : string.Join(" или ", parts);
    }

    private static int ComputeSpiritualArtInkFeatherCost(
        AfterlifeSpiritualConflictState.SpiritualArtDefinition art,
        int nextTier) =>
        checked(50 + nextTier * 50 + art.MinUnlockTier * 25);

    private static int ComputeSpiritualArtLightSparkCost(
        AfterlifeSpiritualConflictState.SpiritualArtDefinition art,
        int nextTier) =>
        checked(4 + nextTier * 3 + art.MinUnlockTier);

    private static string BuildSpiritualArtUpgradeChoiceLabel(SpiritualArtUpgradeQuote quote)
    {
        var status = quote.BlockReason == null
            ? $"уровень {quote.CurrentTier}->{quote.NextTier}, {quote.InkFeatherCost} 🪶"
            : $"заблокировано: {quote.BlockReason}";
        return $"{FormatSpiritualArtLabel(quote.Art)} — {status}";
    }

    private static (int Current, int Total) ReadSoulInkFeathers(JsonObject soulRoot)
    {
        var node = soulRoot["inkFeathers"];
        if (node is JsonObject obj)
        {
            var current = Math.Max(0, AfterlifeSpiritualConflictState.GetNodeInt(obj["current"]));
            var total = Math.Max(current, AfterlifeSpiritualConflictState.GetNodeInt(obj["total"], current));
            return (current, total);
        }

        var value = Math.Max(0, AfterlifeSpiritualConflictState.GetNodeInt(node));
        return (value, value);
    }

    private static bool TrySpendSoulInkFeathers(JsonObject soulRoot, int cost, out string reason)
    {
        reason = "";
        if (cost <= 0)
        {
            reason = "Стоимость должна быть положительной.";
            return false;
        }

        var (current, total) = ReadSoulInkFeathers(soulRoot);
        if (current < cost)
        {
            reason = $"Недостаточно Чернильных Перьев: нужно {cost}, доступно {current}.";
            return false;
        }

        soulRoot["inkFeathers"] = new JsonObject
        {
            ["current"] = current - cost,
            ["total"] = Math.Max(total, current)
        };
        return true;
    }

    private static Panel BuildSpiritualArtUpgradePreviewPanel(
        JsonObject beforeSoulRoot,
        JsonObject? beforeShiningRoot,
        JsonObject afterSoulRoot,
        JsonObject? afterShiningRoot,
        SpiritualArtUpgradeQuote quote,
        SpiritualArtCurrency currency)
    {
        var lines = new List<string>
        {
            "[bold cyan]Предпросмотр локальной прокачки духовного искусства[/]",
            "",
            $"  • Искусство: [white]{Markup.Escape(FormatSpiritualArtLabel(quote.Art))}[/]",
            $"  • Уровень (tier): [white]{quote.CurrentTier}[/] -> [white]{quote.NextTier}[/]",
            $"  • Валюта: [white]{DescribeSpiritualArtCurrency(currency)}[/]",
            $"  • Чернильные Перья: [white]{ReadSoulInkFeathers(beforeSoulRoot).Current}[/] -> [white]{ReadSoulInkFeathers(afterSoulRoot).Current}[/]",
            $"  • Искры Света: [white]{AfterlifeSpiritualConflictState.GetNodeInt(beforeShiningRoot?["lightSparks"])}[/] -> [white]{AfterlifeSpiritualConflictState.GetNodeInt(afterShiningRoot?["lightSparks"])}[/]",
            "",
            "[dim]Это client-owned операция: GM turn, receipt и report не создаются.[/]"
        };

        return new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Прокачка духовного искусства ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(currency == SpiritualArtCurrency.LightSparks ? Color.Gold1 : Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        };
    }

    private static JsonObject BuildSpiritualArtUpgradeAuditNode(
        JsonObject beforeSoulRoot,
        JsonObject? beforeShiningRoot,
        JsonObject afterSoulRoot,
        JsonObject? afterShiningRoot,
        SpiritualArtUpgradeQuote quote,
        SpiritualArtCurrency currency) =>
        new()
        {
            ["sourceSurface"] = "spiritual_arts_local_upgrade",
            ["gmTurnSent"] = false,
            ["receiptWritten"] = false,
            ["artId"] = quote.Art.ArtId,
            ["displayName"] = quote.Art.DisplayName,
            ["tierBefore"] = quote.CurrentTier,
            ["tierAfter"] = quote.NextTier,
            ["currency"] = DescribeSpiritualArtCurrencyToken(currency),
            ["cost"] = currency == SpiritualArtCurrency.LightSparks ? quote.LightSparkCost : quote.InkFeatherCost,
            ["before"] = new JsonObject
            {
                ["soulInkFeathersCurrent"] = ReadSoulInkFeathers(beforeSoulRoot).Current,
                ["lightSparks"] = AfterlifeSpiritualConflictState.GetNodeInt(beforeShiningRoot?["lightSparks"]),
                ["afterlifeCombatProfile"] = beforeSoulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty]?.DeepClone()
            },
            ["after"] = new JsonObject
            {
                ["soulInkFeathersCurrent"] = ReadSoulInkFeathers(afterSoulRoot).Current,
                ["lightSparks"] = AfterlifeSpiritualConflictState.GetNodeInt(afterShiningRoot?["lightSparks"]),
                ["afterlifeCombatProfile"] = afterSoulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty]?.DeepClone()
            },
            ["affectedFiles"] = currency == SpiritualArtCurrency.LightSparks
                ? new JsonArray(SoulStatePath, ShiningAbodeState.StatePath)
                : new JsonArray(SoulStatePath)
        };

    private static string DescribeSpiritualArtCurrency(SpiritualArtCurrency currency) =>
        currency == SpiritualArtCurrency.LightSparks ? "Искры Света" : "Чернильные Перья";

    private static string DescribeSpiritualArtCurrencyToken(SpiritualArtCurrency currency) =>
        currency == SpiritualArtCurrency.LightSparks ? "light_sparks" : "ink_feathers";

    private async Task ShowSpiritualActionAsync()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Действие в духовном конфликте"))
            return;

        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Духовное действие", "Духовное действие посмертия (afterlife spiritual action) доступно только в Море Хаоса и Сияющей Обители.");
            return;
        }

        var root = await ReadJsonObjectForAfterlifeStatusAsync(AfterlifeSpiritualConflictState.StatePath);
        var active = root?["activeConflict"] as JsonObject;
        if (active == null)
        {
            ShowEmptyPanel("Духовное действие", "Нет активного духовного конфликта посмертия (afterlife spiritual conflict). Конфликт должен начать GM через roleplay (отыгрыш) и accepted-turn update (обновление принятого хода).");
            return;
        }

        var conflictId = AfterlifeSpiritualConflictState.GetNodeString(active["conflictId"]) ?? "unknown";
        Clear();
        MarkupLine($"[cyan]Активный конфликт:[/] [white]{Markup.Escape(conflictId)}[/]");
        MarkupLine("[dim]Опишите одно намерение: давление (pressure), защита (guard), манёвр (maneuver), контрприём (counter), разрыв/наложение духовных оков (break_binding/binding), сдача, отступление или переговоры. Команда только добавляет явный тег; обычная ролевая заявка во время активного конфликта тоже валидна.[/]");
        var action = Ask("[cyan]Действие:[/]");
        if (string.IsNullOrWhiteSpace(action))
            return;

        var gmAction =
            $"[AFTERLIFE_SPIRITUAL_ACTION: {conflictId}] {action.Trim()}\n\n" +
            "Разреши это как обмен действиями активного духовного конфликта посмертия (active afterlife spiritual conflict exchange). " +
            $"Если конфликт меняется, запиши `{AfterlifeSpiritualConflictState.ResponseField}` с `mode=exchange` или `mode=resolve`. " +
            "Не используй Mortal combat files, HP, energy, enemiesData/alliesData, NPC/world/faction Mortal surfaces или прямые награды валютой.";

        var preview = new JsonObject
        {
            ["playerActionTag"] = "AFTERLIFE_SPIRITUAL_ACTION",
            ["conflictId"] = conflictId,
            ["playerAction"] = action.Trim(),
            ["expectedResponseSurface"] = AfterlifeSpiritualConflictState.ResponseField,
            ["stateFile"] = AfterlifeSpiritualConflictState.StatePath
        };

        if (!ConfirmChaosSeaContractPreview(
                "Предпросмотр духовного действия посмертия",
                new List<string>
                {
                    "[bold]Контракт GM:[/]",
                    $"  • Активный конфликт (active conflict): {Markup.Escape(conflictId)}",
                    $"  • Поверхность ответа (response surface): `{AfterlifeSpiritualConflictState.ResponseField}`",
                    $"  • Файл состояния (state file): `{AfterlifeSpiritualConflictState.StatePath}`",
                    "  • Конфликт остаётся side-vs-side: используй playerSide/oppositionSide и поля напряжения сторон (side strain).",
                    "  • Принудительное воплощение Хранителем (forced incarnation by Guardian) требует proof проигрыша/сдачи/уступки в resolve.",
                    "  • Файлы смертного боя и состояния Mortal combat/state files запрещены."
                },
                preview,
                "Аудит духовного действия",
                confirmChoice: "✅ Отправить действие GM"))
        {
            return;
        }

        _pendingGmAction = gmAction;
    }

    private static string FormatSideModelLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "direct_duel" => "прямой поединок (direct_duel)",
            "assisted_duel" => "поединок с поддержкой (assisted_duel)",
            "champion_duel" => "поединок чемпиона/союзника (champion_duel)",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatConflictPositionLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "opposition_dominant" => "противник доминирует (opposition_dominant)",
            "opposition_advantaged" => "преимущество противника (opposition_advantaged)",
            "contested" => "спорная позиция (contested)",
            "player_advantaged" => "преимущество игрока (player_advantaged)",
            "player_dominant" => "игрок доминирует (player_dominant)",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatSideStrainLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "clear" => "устойчиво (clear)",
            "strained" => "напряжено (strained)",
            "fractured" => "надломлено (fractured)",
            "overwhelmed" => "подавлено (overwhelmed)",
            "broken" => "сломлено (broken)",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatResolutionStateLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "active" => "активен (active)",
            "concession_pending" => "уступка ожидает закрытия (concession_pending)",
            "surrender_pending" => "сдача ожидает закрытия (surrender_pending)",
            "retreat_pending" => "отступление ожидает закрытия (retreat_pending)",
            "ready_to_resolve" => "готов к завершению (ready_to_resolve)",
            "resolved" => "завершён (resolved)",
            "repair_cancelled" => "отменён repair-путём (repair_cancelled)",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatActorTypeLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "player" => "игрок (player)",
            "guardian" => "Хранитель (guardian)",
            "resident" => "резидент Обители (resident)",
            "radiant_actor" => "светозарный актор (radiant_actor)",
            "custom_afterlife_actor" => "особый актор посмертия (custom_afterlife_actor)",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatSpiritualArtLabel(AfterlifeSpiritualConflictState.SpiritualArtDefinition art) =>
        NormalizeKey(art.ArtId) switch
        {
            "pressure" => "Давление [pressure; Pressure]",
            "counter" => "Контрприём [counter; Counter]",
            "guard" => "Защита [guard; Guard]",
            "maneuver" => "Манёвр [maneuver; Maneuver]",
            "break_binding" => "Разрыв оков [break_binding; Break Binding]",
            "binding" => "Наложение оков [binding; Binding]",
            "incarnation_resistance" => "Сопротивление воплощению [incarnation_resistance; Incarnation Resistance]",
            "champion_coordination" => "Координация чемпиона [champion_coordination; Champion Coordination]",
            _ => $"{art.DisplayName} [{art.ArtId}]"
        };

    private static string FormatSpiritualArtUse(AfterlifeSpiritualConflictState.SpiritualArtDefinition art) =>
        NormalizeKey(art.ArtId) switch
        {
            "pressure" => "усиливает прямое духовное давление на ведущего противника",
            "counter" => "усиливает отражение и разворот заявленного действия противника",
            "guard" => "снижает входящее напряжение или последствия для своей стороны",
            "maneuver" => "улучшает позиционный сдвиг без грубого подавления",
            "break_binding" => "помогает сопротивляться оковам и принудительным handoff/воплощениям",
            "binding" => "помогает наложить ограничивающие духовные оковы после получения преимущества",
            "incarnation_resistance" => "усиливает сопротивление принудительному воплощению от Хранителя",
            "champion_coordination" => "усиливает поддержку, когда ведущим бойцом выступает союзник/чемпион",
            _ => art.MechanicalUse
        };

    private static string FormatRankIdLabel(string? rankId) =>
        NormalizeKey(rankId) switch
        {
            "dormant" => "дремлющий (dormant)",
            "stirring" => "пробуждающийся (stirring)",
            "focused" => "собранный (focused)",
            "tempered" => "закалённый (tempered)",
            "lucid" => "ясный (lucid)",
            "illuminated" => "просветлённый (illuminated)",
            "unlit" => "не зажжён (unlit)",
            "spark" => "искра (spark)",
            "gleam" => "проблеск (gleam)",
            "ray" => "луч (ray)",
            "halo" => "ореол (halo)",
            "suncrest" => "солнечный гребень (suncrest)",
            "aurora" => "аврора (aurora)",
            "dawn_throne" => "трон рассвета (dawn_throne)",
            "stellar_mantle" => "звёздная мантия (stellar_mantle)",
            "radiant_sovereign" => "сияющий владыка (radiant_sovereign)",
            "" => "?",
            _ => rankId ?? "?"
        };

    private static string FormatRankMechanicalEffect(string? effect) =>
        effect switch
        {
            "Baseline afterlife conflict participation." => "Базовое участие в духовных конфликтах посмертия.",
            "Unlocks tier-1 spiritual art upgrades." => "Открывает прокачку духовных искусств до уровня (tier) 1.",
            "Improves strain recovery after ordinary Chaos Sea conflicts." => "Улучшает восстановление напряжения (strain) после обычных конфликтов Моря Хаоса.",
            "Unlocks tier-2 spiritual art upgrades." => "Открывает прокачку духовных искусств до уровня (tier) 2.",
            "Improves resistance against ordinary Guardian pressure." => "Улучшает сопротивление обычному давлению Хранителя.",
            "Unlocks tier-3 spiritual art upgrades and ascension-ready conflict scale." => "Открывает прокачку духовных искусств до уровня (tier) 3 и масштаб конфликтов перед восхождением.",
            "No persistent Radiant combat advantage." => "Нет постоянного боевого преимущества Сияния.",
            "Radiance begins to count as retained combat authority after Shining return." => "Сияние начинает учитываться как сохранённый боевой авторитет после возвращения из Обители.",
            "Unlocks tier-1 Radiant art upgrades." => "Открывает Сияющие духовные искусства до уровня (tier) 1.",
            "Unlocks tier-2 Radiant art upgrades." => "Открывает Сияющие духовные искусства до уровня (tier) 2.",
            "Improves side support when a Shining ally is the lead contestant." => "Улучшает поддержку стороны, когда ведущим бойцом является союзник из Сияющей Обители.",
            "Unlocks tier-3 Radiant art upgrades." => "Открывает Сияющие духовные искусства до уровня (tier) 3.",
            "Retained Radiance strongly influences Chaos Sea conflicts after return." => "Сохранённое Сияние заметно влияет на конфликты Моря Хаоса после возвращения.",
            "Unlocks tier-4 Radiant art upgrades." => "Открывает Сияющие духовные искусства до уровня (tier) 4.",
            "High-rank Abode actors recognize the soul as a major spiritual combatant." => "Высокоранговые акторы Обители распознают душу как значимого духовного бойца.",
            "Unlocks tier-5 Radiant art upgrades and top-end afterlife conflict authority." => "Открывает Сияющие духовные искусства до уровня (tier) 5 и верхний предел авторитета в конфликтах посмертия.",
            _ => effect ?? ""
        };

    private static string NormalizeKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();

    private static void AppendConflictSideSummary(List<string> lines, string label, JsonObject? side)
    {
        if (side == null)
        {
            lines.Add($"  • {label}: [red]missing[/]");
            return;
        }

        var lead = side["leadContestant"] as JsonObject;
        var displayName = AfterlifeSpiritualConflictState.GetNodeString(lead?["displayName"]) ??
                          AfterlifeSpiritualConflictState.GetNodeString(lead?["actorId"]) ??
                          "unknown";
        var actorType = AfterlifeSpiritualConflictState.GetNodeString(lead?["actorType"]) ?? "?";
        var supporters = (side["supporters"] as JsonArray)?.Count ?? 0;
        lines.Add($"  • {label}: [white]{Markup.Escape(displayName)}[/] [dim]({Markup.Escape(FormatActorTypeLabel(actorType))}; ведущий, поддержка={supporters})[/]");
    }
}
