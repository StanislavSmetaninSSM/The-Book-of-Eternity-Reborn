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
            ShowEmptyPanel("Духовный конфликт", "Afterlife spiritual conflict доступен только в Море Хаоса и Сияющей Обители.");
            return;
        }

        await _stateManager.RefreshGameStateAsync();
        var root = await ReadJsonObjectForAfterlifeStatusAsync(AfterlifeSpiritualConflictState.StatePath);
        var active = root?["activeConflict"] as JsonObject;

        var lines = new List<string>
        {
            "[bold cyan]Afterlife spiritual conflict[/]",
            "",
            "Это отдельная загробная система конфликтов. Она не использует Mortal combat files, HP, energy, enemiesData/alliesData или смертные боевые навыки.",
            "Конфликт начинает GM по роли: по заявке игрока или когда afterlife actor сам инициирует давление.",
            ""
        };

        if (active == null)
        {
            lines.Add("[dim]Активного духовного конфликта нет.[/]");
            lines.Add("");
            lines.Add("GM может начать конфликт только accepted-turn response surface:");
            lines.Add($"  • `{AfterlifeSpiritualConflictState.ResponseField}` with `mode=start`");
            lines.Add($"  • persisted state: `{AfterlifeSpiritualConflictState.StatePath}`");
        }
        else
        {
            var conflictId = AfterlifeSpiritualConflictState.GetNodeString(active["conflictId"]) ?? "unknown";
            lines.Add($"[bold]Активный конфликт:[/] [white]{Markup.Escape(conflictId)}[/]");
            lines.Add($"  • Realm: [white]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(active["realm"]) ?? "?")}[/]");
            lines.Add($"  • Model: [white]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(active["sideModel"]) ?? "?")}[/]");
            lines.Add($"  • Position: [white]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(active["conflictPosition"]) ?? "?")}[/]");
            lines.Add($"  • Player side strain: [white]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(active["playerSideStrain"]) ?? "?")}[/]");
            lines.Add($"  • Opposition side strain: [white]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(active["oppositionSideStrain"]) ?? "?")}[/]");
            lines.Add($"  • Resolution state: [white]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(active["resolutionState"]) ?? "?")}[/]");
            lines.Add("");
            AppendConflictSideSummary(lines, "Player side", active["playerSide"] as JsonObject);
            AppendConflictSideSummary(lines, "Opposition side", active["oppositionSide"] as JsonObject);
            lines.Add("");
            lines.Add($"  • Exchanges recorded: [white]{(active["exchangeLog"] as JsonArray)?.Count ?? 0}[/]");
        }

        lines.Add("");
        lines.Add("[bold]Команды:[/]");
        lines.Add("  • /spiritual_action — отправить действие в активном конфликте GM с явным тегом.");
        lines.Add("  • Обычная художественная заявка во время активного конфликта тоже должна резолвиться GM как действие конфликта.");
        lines.Add("  • /spiritual_arts — посмотреть ранги, art tiers и применимые действия.");

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" ⚔ Afterlife Spiritual Conflict ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        if (root != null)
            WriteJsonAuditPanel($"Full JSON {AfterlifeSpiritualConflictState.StatePath}", root, Color.Cyan1);

        WaitForKey();
    }

    private async Task ShowSpiritualArtsAsync()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Духовные искусства"))
            return;

        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Духовные искусства", "Afterlife spiritual arts доступны только в Море Хаоса и Сияющей Обители.");
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
            WriteJsonAuditPanel("Full JSON afterlifeCombatProfile", profile, Color.Cyan1);

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
            "[bold cyan]Afterlife spiritual arts[/]",
            "",
            "[bold]Current profile:[/]",
            $"  • Enlightenment rank: [white]{AfterlifeSpiritualConflictState.GetNodeInt(profile["enlightenmentRank"])}[/]",
            $"  • Radiance rank: [white]{AfterlifeSpiritualConflictState.GetNodeInt(profile["radianceRank"])}[/]",
            $"  • Retained Radiance rank: [white]{AfterlifeSpiritualConflictState.GetNodeInt(profile["retainedRadianceRank"])}[/]",
            $"  • Soul enlightenment level: [white]{AfterlifeSpiritualConflictState.GetNodeInt(enlightenment?["level"])}[/] [dim]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(enlightenment?["currentTier"]) ?? "")}[/]",
            $"  • Shining radiance: [white]{AfterlifeSpiritualConflictState.GetNodeInt(radiance?["experience"])} XP[/] / tier [white]{AfterlifeSpiritualConflictState.GetNodeInt(radiance?["tier"])}[/]",
            $"  • Max unlocked art tier: [white]{maxUnlockedTier}[/]",
            $"  • Spendable Ink Feathers: [white]{ShiningAbodeState.GetSoulSpendableInkFeathers(soulRoot)}[/]",
            $"  • Light Sparks: [gold1]{AfterlifeSpiritualConflictState.GetNodeInt(shiningRoot?["lightSparks"])}[/] [dim](usable only in ordinary Shining Abode)[/]",
            "",
            "[bold]Arts:[/]"
        };

        foreach (var quote in quotes)
        {
            var tier = AfterlifeSpiritualConflictState.GetNodeInt(artTiers?[quote.Art.ArtId]);
            var blocked = quote.BlockReason == null
                ? $"next tier {quote.NextTier}, cost {quote.InkFeatherCost} 🪶"
                : $"blocked: {quote.BlockReason}";
            var sparkCost = _stateManager.CurrentState.IsInShiningAbode ? $" / {quote.LightSparkCost} ✨" : "";
            lines.Add($"  • [white]{Markup.Escape(quote.Art.DisplayName)}[/] `[dim]{Markup.Escape(quote.Art.ArtId)}[/]`: tier [white]{tier}[/], rank gate [white]{quote.RequiredRankLabel}[/], {Markup.Escape(blocked)}{Markup.Escape(sparkCost)} — {Markup.Escape(quote.Art.MechanicalUse)}");
        }

        lines.Add("");
        lines.Add("[bold]Enlightenment rank ladder:[/]");
        foreach (var rank in AfterlifeSpiritualConflictState.EnlightenmentRanks)
            lines.Add($"  • {rank.Rank}: `{rank.RankId}` requires {rank.RequiredProgress}, unlocks art tier {rank.UnlocksArtTier}. {Markup.Escape(rank.MechanicalEffect)}");

        lines.Add("");
        lines.Add("[bold]Radiance rank ladder:[/]");
        foreach (var rank in AfterlifeSpiritualConflictState.RadianceRanks)
            lines.Add($"  • {rank.Rank}: `{rank.RankId}` requires {rank.RequiredProgress}, unlocks art tier {rank.UnlocksArtTier}. {Markup.Escape(rank.MechanicalEffect)}");

        lines.Add("");
        lines.Add("[dim]Upgrade rule: ranks gate max art tier; the client writes soul_state.afterlifeCombatProfile locally and spends the selected currency. GM does not author upgrade receipts.[/]");

        return new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" ✨ Spiritual Arts ", Justify.Center),
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

        MarkupLine($"[green]Прокачано: {Markup.Escape(quote.Art.DisplayName)} tier {quote.CurrentTier} -> {quote.NextTier}.[/]");
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
            return "Прокачка духовных искусств заблокирована: найден активный GM-turn lifecycle. " +
                   "Локальная прокачка меняет client-owned soul_state.afterlifeCombatProfile и валюту, поэтому дождитесь завершения/отмены/repair текущего хода. " +
                   $"Найдено: {string.Join(", ", activeTurnArtifacts)}.";
        }

        var conflictRead = await ReadJsonObjectForAfterlifeStatusResultAsync(AfterlifeSpiritualConflictState.StatePath);
        if (conflictRead.Error != null)
        {
            return $"Прокачка духовных искусств заблокирована: {AfterlifeSpiritualConflictState.StatePath} повреждён ({conflictRead.Error}). Сначала выполните repair.";
        }

        if (conflictRead.Root?["activeConflict"] is JsonObject)
        {
            return "Прокачка духовных искусств заблокирована: сейчас активен afterlife spiritual conflict. Завершите exchange/resolve/repair_cancel перед изменением боевого профиля.";
        }

        if (conflictRead.Root != null &&
            conflictRead.Root.TryGetPropertyValue("activeConflict", out var activeConflict) &&
            activeConflict != null)
        {
            return $"Прокачка духовных искусств заблокирована: {AfterlifeSpiritualConflictState.StatePath}.activeConflict повреждён. Сначала выполните repair.";
        }

        if (_fs.FileExists(GuardianAbodeOfferingState.PendingRequestPath))
        {
            return $"Прокачка духовных искусств заблокирована: найден unresolved cost-bearing contract {GuardianAbodeOfferingState.PendingRequestPath}. Дождитесь accepted/refused/repair closure.";
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
                return "Прокачка духовных искусств заблокирована из-за Shining cost-bearing pending contract. " + shiningBlocker;
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
                blockReason = "уже достигнут максимальный tier 5";
            else if (maxUnlockedTier < art.MinUnlockTier)
                blockReason = $"нужен ранг, открывающий art tier {art.MinUnlockTier}: {DescribeRequiredRankForArtTier(art.MinUnlockTier)}";
            else if (nextTier > maxUnlockedTier)
                blockReason = $"нужен ранг, открывающий art tier {nextTier}: {requiredRankLabel}";

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
            parts.Add($"Enlightenment {enlightenmentRank.Rank} `{enlightenmentRank.RankId}`");
        if (radianceRank != null)
            parts.Add($"Radiance {radianceRank.Rank} `{radianceRank.RankId}`");

        return parts.Count == 0 ? "not unlockable" : string.Join(" или ", parts);
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
            ? $"tier {quote.CurrentTier}->{quote.NextTier}, {quote.InkFeatherCost} 🪶"
            : $"blocked: {quote.BlockReason}";
        return $"{quote.Art.DisplayName} [{quote.Art.ArtId}] — {status}";
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
            $"  • Искусство: [white]{Markup.Escape(quote.Art.DisplayName)}[/] `[dim]{Markup.Escape(quote.Art.ArtId)}[/]`",
            $"  • Tier: [white]{quote.CurrentTier}[/] -> [white]{quote.NextTier}[/]",
            $"  • Валюта: [white]{DescribeSpiritualArtCurrency(currency)}[/]",
            $"  • Чернильные Перья: [white]{ReadSoulInkFeathers(beforeSoulRoot).Current}[/] -> [white]{ReadSoulInkFeathers(afterSoulRoot).Current}[/]",
            $"  • Искры Света: [white]{AfterlifeSpiritualConflictState.GetNodeInt(beforeShiningRoot?["lightSparks"])}[/] -> [white]{AfterlifeSpiritualConflictState.GetNodeInt(afterShiningRoot?["lightSparks"])}[/]",
            "",
            "[dim]Это client-owned операция: GM turn, receipt и report не создаются.[/]"
        };

        return new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Spiritual Art Upgrade ", Justify.Center),
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
            ShowEmptyPanel("Духовное действие", "Afterlife spiritual action доступно только в Море Хаоса и Сияющей Обители.");
            return;
        }

        var root = await ReadJsonObjectForAfterlifeStatusAsync(AfterlifeSpiritualConflictState.StatePath);
        var active = root?["activeConflict"] as JsonObject;
        if (active == null)
        {
            ShowEmptyPanel("Духовное действие", "Нет активного afterlife spiritual conflict. Конфликт должен начать GM через roleplay и accepted-turn update.");
            return;
        }

        var conflictId = AfterlifeSpiritualConflictState.GetNodeString(active["conflictId"]) ?? "unknown";
        Clear();
        MarkupLine($"[cyan]Активный конфликт:[/] [white]{Markup.Escape(conflictId)}[/]");
        MarkupLine("[dim]Опишите одно намерение: давление, защита, манёвр, контр, разрыв/наложение binding, сдача, отступление или переговоры. Команда только добавляет явный тег; обычная ролевая заявка во время активного конфликта тоже валидна.[/]");
        var action = Ask("[cyan]Действие:[/]");
        if (string.IsNullOrWhiteSpace(action))
            return;

        var gmAction =
            $"[AFTERLIFE_SPIRITUAL_ACTION: {conflictId}] {action.Trim()}\n\n" +
            "Resolve as an active afterlife spiritual conflict exchange. " +
            $"If the conflict changes, write `{AfterlifeSpiritualConflictState.ResponseField}` with mode=exchange or mode=resolve. " +
            "Do not use Mortal combat files, HP, energy, enemiesData/alliesData, NPC/world/faction Mortal surfaces, or direct currency rewards.";

        var preview = new JsonObject
        {
            ["playerActionTag"] = "AFTERLIFE_SPIRITUAL_ACTION",
            ["conflictId"] = conflictId,
            ["playerAction"] = action.Trim(),
            ["expectedResponseSurface"] = AfterlifeSpiritualConflictState.ResponseField,
            ["stateFile"] = AfterlifeSpiritualConflictState.StatePath
        };

        if (!ConfirmChaosSeaContractPreview(
                "Afterlife spiritual action preview",
                new List<string>
                {
                    "[bold]GM contract:[/]",
                    $"  • Active conflict: {Markup.Escape(conflictId)}",
                    $"  • Response surface: `{AfterlifeSpiritualConflictState.ResponseField}`",
                    $"  • State file: `{AfterlifeSpiritualConflictState.StatePath}`",
                    "  • Conflict remains side-vs-side; use playerSide/oppositionSide and side strain fields.",
                    "  • Forced incarnation by Guardian requires resolved conflict loss/surrender/concession proof.",
                    "  • Mortal combat/state files are forbidden."
                },
                preview,
                "Spiritual action audit",
                confirmChoice: "✅ Отправить действие GM"))
        {
            return;
        }

        _pendingGmAction = gmAction;
    }

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
        lines.Add($"  • {label}: [white]{Markup.Escape(displayName)}[/] [dim]({Markup.Escape(actorType)} lead, supporters={supporters})[/]");
    }
}
