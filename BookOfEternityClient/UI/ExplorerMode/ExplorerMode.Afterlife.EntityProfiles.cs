using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private async Task ShowAfterlifeEntityProfilesAsync()
    {
        var includeGmDiagnostics = _stateManager.Settings.ShowGmThoughts;
        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Профили сущностей посмертия", "Профили сущностей посмертия доступны только в Море Хаоса и Сияющей Обители.");
            return;
        }

        await _stateManager.RefreshGameStateAsync();
        var read = await ReadJsonObjectForAfterlifeStatusResultAsync(AfterlifeEntityProfileState.StatePath);
        if (read.Error != null)
        {
            ShowEmptyPanel(
                "Профили сущностей посмертия",
                $"{AfterlifeEntityProfileState.StatePath} повреждён ({read.Error}). Сначала выполните repair состояния.");
            if (includeGmDiagnostics && !string.IsNullOrWhiteSpace(read.RawPayload))
                WriteJsonAuditPanel($"Raw {AfterlifeEntityProfileState.StatePath}", JsonValue.Create(read.RawPayload), Color.Red);
            return;
        }

        var profiles = read.Root?[AfterlifeEntityProfileState.ProfilesProperty] as JsonArray;
        if (profiles == null || profiles.Count == 0)
        {
            ShowEmptyPanel(
                "Профили сущностей посмертия",
                "Профили сущностей посмертия пока не созданы. ГМ создаёт их через afterlifeEntityProfileUpdates для значимых Хранителей, резидентов, глав фракций и особых акторов.");
            return;
        }

        var lines = new List<string>
        {
            "[bold cyan]Профили сущностей посмертия[/]",
            "",
            "Это полные игровые профили значимых сущностей загробья: ресурсы, прогрессия, духовные искусства, цели, личные квесты, текущая активность, стратегия прокачки и опасность развеивания души.",
            ""
        };

        foreach (var profile in profiles.OfType<JsonObject>()
                     .Where(profile => includeGmDiagnostics || AfterlifeProfileVisibility.IsVisibleToPlayer(profile))
                     .OrderBy(profile => AfterlifeEntityProfileState.GetNodeString(profile["displayName"]) ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            AppendAfterlifeEntityProfile(lines, profile, includeGmDiagnostics);
            lines.Add("");
        }

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Профили сущностей посмертия ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        if (includeGmDiagnostics && read.Root != null)
            WriteJsonAuditPanel($"Полный JSON {AfterlifeEntityProfileState.StatePath}", read.Root, Color.Cyan1);

        WaitForKey();
    }

    private void AppendAfterlifeEntityProfile(List<string> lines, JsonObject profile, bool includeGmDiagnostics)
    {
        var displayName = AfterlifeEntityProfileState.GetNodeString(profile["displayName"]) ?? "Без имени";
        var actorType = AfterlifeEntityProfileState.GetNodeString(profile["actorType"]) ?? "?";
        var actorId = AfterlifeEntityProfileState.GetNodeString(profile["actorId"]) ??
                      AfterlifeEntityProfileState.GetNodeString(profile["actorRef"]) ??
                      "?";
        var realm = AfterlifeEntityProfileState.GetNodeString(profile["realm"]) ?? "?";
        var location = AfterlifeEntityProfileState.GetNodeString(profile["location"]) ??
                       AfterlifeEntityProfileState.GetNodeString(profile["locationName"]) ??
                       AfterlifeEntityProfileState.GetNodeString(profile["abodeName"]) ??
                       "место не указано";

        lines.Add($"[bold white]{Markup.Escape(displayName)}[/] [dim]({Markup.Escape(actorId)})[/]");
        lines.Add($"  • Тип: [white]{Markup.Escape(DescribeAfterlifeEntityActorType(actorType))}[/]");
        lines.Add($"  • Область: [white]{Markup.Escape(DescribeAfterlifeEntityRealm(realm))}[/]; место: [dim]{Markup.Escape(location)}[/]");

        var currencies = profile["currencies"] as JsonObject;
        lines.Add($"  • Чернильные Перья: [gold1]{AfterlifeEntityProfileState.GetNodeInt(currencies?["inkFeathers"])}[/]; Искры Света: [gold1]{AfterlifeEntityProfileState.GetNodeInt(currencies?["lightSparks"])}[/]");

        var progression = profile["progression"] as JsonObject;
        var enlightenment = progression?["enlightenment"] as JsonObject;
        var radiance = progression?["radiance"] as JsonObject;
        lines.Add($"  • Просветление: тир {AfterlifeEntityProfileState.GetNodeInt(enlightenment?["tier"])}, опыт {AfterlifeEntityProfileState.GetNodeInt(enlightenment?["experience"])}");
        lines.Add($"  • Сияние: тир {AfterlifeEntityProfileState.GetNodeInt(radiance?["tier"])}, опыт {AfterlifeEntityProfileState.GetNodeInt(radiance?["experience"])}");

        AppendAfterlifeEntityStandardArts(lines, profile["standardArts"] as JsonObject);
        AppendAfterlifeEntitySpecialArts(lines, profile["specialArts"] as JsonArray);
        AppendAfterlifeEntityCustomStates(lines, profile[AfterlifeEntityProfileState.CustomStatesProperty] as JsonArray);
        AppendAfterlifeEntityFateCards(lines, profile["fateCards"] as JsonArray, includeGmDiagnostics);
        AppendAfterlifeEntityRelationships(lines, profile[AfterlifeEntityProfileState.RelationshipsProperty] as JsonArray);
        AppendAfterlifeEntityMasks(lines, profile);
        AppendAfterlifeEntityAgency(lines, profile, includeGmDiagnostics);

        var dissipationTier = AfterlifeEntityProfileState.GetNodeInt(profile["soulDissipationTier"]);
        var stabilityCoefficient = AfterlifeEntityProfileState.ResolveSoulStabilityCoefficient(profile);
        lines.Add($"  • Развеивание души: тир {dissipationTier}; устойчивость души цели: коэффициент {stabilityCoefficient}");

        var warnings = ReadProfileStringArray(profile["warnings"] as JsonArray).ToList();
        if (dissipationTier > 0)
            warnings.Insert(0, $"ОПАСНО: эта сущность потенциально может окончательно развеять душу после победы, если её тир Развеивания выше коэффициента устойчивости цели и её мотивы это допускают. Решение не автоматическое.");
        foreach (var warning in warnings.Distinct(StringComparer.OrdinalIgnoreCase))
            lines.Add($"  • [red]{Markup.Escape(warning)}[/]");

        var strategy = profile["progressionStrategy"] as JsonObject;
        var strategySummary = AfterlifeEntityProfileState.GetNodeString(strategy?["summary"]);
        if (!string.IsNullOrWhiteSpace(strategySummary))
            lines.Add($"  • Стратегия прокачки: [dim]{Markup.Escape(strategySummary)}[/]");

        var priorities = ReadProfileStringArray(strategy?["priorityOrder"] as JsonArray).ToList();
        if (priorities.Count > 0)
            lines.Add($"  • Приоритеты: [dim]{Markup.Escape(string.Join(" → ", priorities))}[/]");

        var lastCycleKey = AfterlifeEntityProfileState.GetNodeString(strategy?["lastAutoProgressionCycleKey"]);
        var latestProgression = ReadLatestProgressionLedgerEntry(profile[AfterlifeEntityProfileState.ProgressionLedgerProperty] as JsonArray);
        if (latestProgression != null)
        {
            var cycleKey = AfterlifeEntityProfileState.GetNodeString(latestProgression["cycleKey"]) ?? lastCycleKey ?? "?";
            var summary = AfterlifeEntityProfileState.GetNodeString(latestProgression["summary"]) ?? "детали не указаны";
            lines.Add($"  • Последняя автопрокачка: [white]{Markup.Escape(cycleKey)}[/] — [dim]{Markup.Escape(summary)}[/]");
        }
        else if (!string.IsNullOrWhiteSpace(lastCycleKey))
        {
            lines.Add($"  • Последняя автопрокачка: [white]{Markup.Escape(lastCycleKey)}[/]");
        }
    }

    private static void AppendAfterlifeEntityStandardArts(List<string> lines, JsonObject? arts)
    {
        if (arts == null || arts.Count == 0)
        {
            lines.Add("  • Духовные искусства: [dim]не указаны[/]");
            return;
        }

        lines.Add("  • Духовные искусства:");
        foreach (var art in arts.OrderBy(item => DescribeAfterlifeEntityArt(item.Key), StringComparer.OrdinalIgnoreCase))
            lines.Add($"    - {Markup.Escape(DescribeAfterlifeEntityArt(art.Key))}: {AfterlifeEntityProfileState.GetNodeInt(art.Value)}");
    }

    private static void AppendAfterlifeEntitySpecialArts(List<string> lines, JsonArray? arts)
    {
        if (arts == null || arts.Count == 0)
            return;

        lines.Add("  • Особые духовные искусства:");
        foreach (var art in arts.OfType<JsonObject>())
        {
            var name = AfterlifeEntityProfileState.GetNodeString(art["displayName"]) ??
                       AfterlifeEntityProfileState.GetNodeString(art["artId"]) ??
                       "Без названия";
            var baseOperation = AfterlifeEntityProfileState.GetNodeString(art["baseOperation"]) ?? "?";
            var tier = AfterlifeEntityProfileState.GetNodeInt(art["tier"]);
            var effect = AfterlifeEntityProfileState.GetNodeString(art["effectSummary"]);
            var costMultiplier = AfterlifeEntityProfileState.GetNodeInt(art["costMultiplierPercent"]);
            var canTeach = art["canTeachPlayer"] is JsonValue canTeachValue &&
                           canTeachValue.TryGetValue<bool>(out var teachValue) &&
                           teachValue;
            lines.Add($"    - {Markup.Escape(name)}: тир {tier}, основа — {Markup.Escape(DescribeAfterlifeEntityArt(baseOperation))}");
            if (costMultiplier > 0)
                lines.Add($"      [dim]Стоимость применения: {costMultiplier}% от базовой стоимости действия.[/]");
            if (canTeach)
                lines.Add("      [green]может обучать игрока[/]");
            var trainingConditions = ReadProfileStringArray(art["trainingConditions"] as JsonArray).ToList();
            if (trainingConditions.Count > 0)
                lines.Add($"      [dim]Условия обучения: {Markup.Escape(string.Join("; ", trainingConditions))}[/]");
            var playerEffect = NormalizeAfterlifeCombatPlayerText(effect);
            if (!string.IsNullOrWhiteSpace(playerEffect))
                lines.Add($"      [dim]{Markup.Escape(playerEffect)}[/]");
            var combatEffect = FormatAfterlifeSpecialArtCombatEffect(art);
            if (!string.IsNullOrWhiteSpace(combatEffect))
                lines.Add($"      [dim]{Markup.Escape(combatEffect)}[/]");
        }
    }

    private static string? FormatAfterlifeSpecialArtCombatEffect(JsonObject art)
    {
        if (art["combatEffect"] is not JsonObject combatEffect)
            return null;

        var parts = new List<string>();
        var summary = NormalizeAfterlifeCombatPlayerText(AfterlifeEntityProfileState.GetNodeString(combatEffect["summary"]));
        var trigger = NormalizeAfterlifeCombatPlayerText(AfterlifeEntityProfileState.GetNodeString(combatEffect["trigger"]));
        var payoff = NormalizeAfterlifeCombatPlayerText(AfterlifeEntityProfileState.GetNodeString(combatEffect["allowedPayoff"]));
        var limit = NormalizeAfterlifeCombatPlayerText(AfterlifeEntityProfileState.GetNodeString(combatEffect["limit"]));

        if (!string.IsNullOrWhiteSpace(summary))
            parts.Add($"Боевой эффект: {summary}");
        if (!string.IsNullOrWhiteSpace(trigger))
            parts.Add($"срабатывает: {trigger}");
        if (!string.IsNullOrWhiteSpace(payoff))
            parts.Add($"выигрыш: {payoff}");
        if (!string.IsNullOrWhiteSpace(limit))
            parts.Add($"предел: {limit}");

        return parts.Count == 0 ? null : string.Join(". ", parts);
    }

    private static void AppendAfterlifeEntityCustomStates(List<string> lines, JsonArray? states)
    {
        if (states == null || states.Count == 0)
            return;

        lines.Add("  • Кастомные состояния:");
        foreach (var state in states.OfType<JsonObject>())
        {
            var name = AfterlifeEntityProfileState.GetNodeString(state["stateName"]) ??
                       AfterlifeEntityProfileState.GetNodeString(state["name"]) ??
                       AfterlifeEntityProfileState.GetNodeString(state["title"]) ??
                       AfterlifeEntityProfileState.GetNodeString(state["stateId"]) ??
                       "Без названия";
            var current = AfterlifeEntityProfileState.GetNodeString(state["currentValue"]) ??
                          AfterlifeEntityProfileState.GetNodeInt(state["currentValue"]).ToString();
            var max = AfterlifeEntityProfileState.GetNodeString(state["maxValue"]) ??
                      AfterlifeEntityProfileState.GetNodeInt(state["maxValue"]).ToString();
            var description = AfterlifeEntityProfileState.GetNodeString(state["description"]) ??
                              AfterlifeEntityProfileState.GetNodeString(state["summary"]);

            lines.Add($"    - {Markup.Escape(name)}: {Markup.Escape(current)}/{Markup.Escape(max)}");
            if (!string.IsNullOrWhiteSpace(description))
                lines.Add($"      [dim]{Markup.Escape(description)}[/]");
        }
    }

    private static void AppendAfterlifeEntityFateCards(
        List<string> lines,
        JsonArray? fateCards,
        bool includeGmDiagnostics)
    {
        if (fateCards == null || fateCards.Count == 0)
            return;

        var visibleCards = fateCards
            .OfType<JsonObject>()
            .Where(card => includeGmDiagnostics || IsFateCardPlayerVisible(card))
            .ToList();
        if (visibleCards.Count == 0)
            return;

        lines.Add("  • Карты судьбы:");
        foreach (var card in visibleCards)
        {
            var name = AfterlifeEntityProfileState.GetNodeString(card["nameRu"]) ??
                       AfterlifeEntityProfileState.GetNodeString(card["cardId"]) ??
                       "Без названия";
            var status = AfterlifeEntityProfileState.GetNodeString(card["status"]) ?? "locked";
            var storyMeaning = AfterlifeEntityProfileState.GetNodeString(card["storyMeaning"]);
            var secret = card["isSecret"] is JsonValue secretValue &&
                         secretValue.TryGetValue<bool>(out var secretBool) &&
                         secretBool;
            lines.Add($"    - {Markup.Escape(name)}: {Markup.Escape(DescribeFateCardStatus(status))}");
            if (!secret && !string.IsNullOrWhiteSpace(storyMeaning))
                lines.Add($"      [dim]{Markup.Escape(storyMeaning)}[/]");
            else if (secret)
                lines.Add("      [dim]Скрытые условия пока не раскрыты игроку.[/]");

            if (string.Equals(status, "unlocked", StringComparison.OrdinalIgnoreCase))
            {
                var effects = CountFateCardEffects(card);
                var appliedAtTurn = AfterlifeEntityProfileState.GetNodeInt(card["appliedAtTurn"]);
                lines.Add($"      [green]Открыта: активных эффектов {effects}, ход {appliedAtTurn}[/]");
            }
        }
    }

    private static void AppendAfterlifeEntityRelationships(List<string> lines, JsonArray? relationships)
    {
        if (relationships == null || relationships.Count == 0)
            return;

        lines.Add("  • Отношения:");
        foreach (var relationship in relationships.OfType<JsonObject>())
        {
            var axis = AfterlifeEntityProfileState.GetNodeString(relationship["axis"]) ?? "?";
            var targetActorId = AfterlifeEntityProfileState.GetNodeString(relationship["targetActorId"]) ??
                                AfterlifeEntityProfileState.GetNodeString(relationship["targetActorRef"]) ??
                                "цель не указана";
            var value = AfterlifeEntityProfileState.GetNodeInt(relationship["value"]);
            var tier = AfterlifeEntityProfileState.GetNodeString(relationship["relationshipTier"]) ?? "ступень не указана";
            lines.Add($"    - {Markup.Escape(DescribeAfterlifeRelationshipAxis(axis))} к {Markup.Escape(targetActorId)}: {value}, {Markup.Escape(tier)}");

            if (relationship["relationshipLock"] is JsonObject relationshipLock)
            {
                var lockState = AfterlifeEntityProfileState.GetNodeString(relationshipLock["lockState"]) ?? "locked";
                var reason = AfterlifeEntityProfileState.GetNodeString(relationshipLock["reason"]) ?? "причина не указана";
                var breakthroughQuestId = AfterlifeEntityProfileState.GetNodeString(relationshipLock["breakthroughQuestId"]);
                var redemptionQuestId = AfterlifeEntityProfileState.GetNodeString(relationshipLock["redemptionQuestId"]);
                lines.Add($"      [yellow]заблокировано: {Markup.Escape(DescribeAfterlifeRelationshipLock(lockState))}[/]");
                lines.Add($"      [dim]{Markup.Escape(reason)}[/]");
                if (!string.IsNullOrWhiteSpace(breakthroughQuestId))
                    lines.Add($"      [dim]Нужен прорыв: {Markup.Escape(breakthroughQuestId)}[/]");
                if (!string.IsNullOrWhiteSpace(redemptionQuestId))
                    lines.Add($"      [dim]Нужно искупление: {Markup.Escape(redemptionQuestId)}[/]");
            }

            if (relationship[AfterlifeEntityProfileState.RelationshipGateQuestsProperty] is JsonArray quests)
            {
                foreach (var quest in quests.OfType<JsonObject>())
                {
                    var status = AfterlifeEntityProfileState.GetNodeString(quest["status"]) ?? "?";
                    var title = AfterlifeEntityProfileState.GetNodeString(quest["title"]) ??
                                AfterlifeEntityProfileState.GetNodeString(quest["questId"]) ??
                                "Без названия";
                    lines.Add($"      · {Markup.Escape(title)}: {Markup.Escape(DescribeAfterlifeRelationshipQuestStatus(status))}");
                }
            }
        }
    }

    private static void AppendAfterlifeEntityMasks(List<string> lines, JsonObject profile)
    {
        var activeMaskId = AfterlifeEntityProfileState.GetNodeString(profile[AfterlifeEntityProfileState.ActiveMaskIdProperty]);
        if (string.IsNullOrWhiteSpace(activeMaskId) ||
            string.Equals(activeMaskId, AfterlifeEntityProfileState.TrueSelfMaskId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var masks = profile[AfterlifeEntityProfileState.MasksProperty] as JsonArray;
        var activeMask = masks?
            .OfType<JsonObject>()
            .FirstOrDefault(mask => string.Equals(
                AfterlifeEntityProfileState.GetNodeString(mask["maskId"]),
                activeMaskId,
                StringComparison.OrdinalIgnoreCase));
        if (activeMask == null)
            return;

        var displayName = AfterlifeEntityProfileState.GetNodeString(activeMask["displayName"]) ?? activeMaskId;
        var publicArchetype = AfterlifeEntityProfileState.GetNodeString(activeMask["publicArchetype"]);
        var visiblePersonality = AfterlifeEntityProfileState.GetNodeString(activeMask["visiblePersonality"]);
        var deceptionRisk = AfterlifeEntityProfileState.GetNodeString(activeMask["deceptionRisk"]);
        var isRevealed = activeMask["isRevealed"] is JsonValue revealedValue &&
                         revealedValue.TryGetValue<bool>(out var revealed) &&
                         revealed;

        lines.Add("  • Активная маска:");
        lines.Add($"    - {Markup.Escape(displayName)}");
        if (!string.IsNullOrWhiteSpace(publicArchetype))
            lines.Add($"      [dim]Публичная роль: {Markup.Escape(publicArchetype)}[/]");
        if (!string.IsNullOrWhiteSpace(visiblePersonality))
            lines.Add($"      [dim]Видимое поведение: {Markup.Escape(visiblePersonality)}[/]");
        if (!string.IsNullOrWhiteSpace(deceptionRisk))
            lines.Add($"      [dim]Риск обмана: {Markup.Escape(DescribeAfterlifeMaskRisk(deceptionRisk))}[/]");

        if (!isRevealed)
        {
            lines.Add("      [dim]Скрытая истина маски пока не раскрыта игроку.[/]");
            return;
        }

        var concealedTruth = AfterlifeEntityProfileState.GetNodeString(activeMask["concealedTruth"]);
        if (!string.IsNullOrWhiteSpace(concealedTruth))
            lines.Add($"      [yellow]Раскрытая истина: {Markup.Escape(concealedTruth)}[/]");

        var directives = ReadProfileStringArray(activeMask["directives"] as JsonArray).ToList();
        if (directives.Count > 0)
            lines.Add($"      [dim]Скрытые директивы: {Markup.Escape(string.Join("; ", directives))}[/]");

        var linkedThreatId = AfterlifeEntityProfileState.GetNodeString(activeMask["linkedThreatId"]);
        var linkedSarefAgentId = AfterlifeEntityProfileState.GetNodeString(activeMask["linkedSarefAgentId"]);
        if (!string.IsNullOrWhiteSpace(linkedThreatId))
            lines.Add($"      [dim]Связанная угроза: {Markup.Escape(linkedThreatId)}[/]");
        if (!string.IsNullOrWhiteSpace(linkedSarefAgentId))
            lines.Add($"      [dim]Связанный агент Сарефа: {Markup.Escape(linkedSarefAgentId)}[/]");
    }

    private static void AppendAfterlifeEntityAgency(
        List<string> lines,
        JsonObject profile,
        bool includeGmDiagnostics)
    {
        var goals = profile["goals"] as JsonObject;
        var currentActivity = profile["currentActivity"] as JsonObject;
        var personalQuests = profile["personalQuests"] as JsonArray;
        var completedActivities = profile["completedActivities"] as JsonArray;
        if (goals == null && currentActivity == null && (personalQuests == null || personalQuests.Count == 0) && (completedActivities == null || completedActivities.Count == 0))
            return;

        lines.Add("  • Цели и текущие действия:");
        if (goals != null)
        {
            var shortTerm = AfterlifeEntityProfileState.GetNodeString(goals["shortTermGoal"]);
            var longTerm = AfterlifeEntityProfileState.GetNodeString(goals["longTermGoal"]);
            var plan = AfterlifeEntityProfileState.GetNodeString(goals["plan"]);
            var thoughts = AfterlifeEntityProfileState.GetNodeString(goals["gmThoughtsSummary"]);
            if (!string.IsNullOrWhiteSpace(shortTerm))
                lines.Add($"    - Ближайшая цель: {Markup.Escape(shortTerm)}");
            if (!string.IsNullOrWhiteSpace(longTerm))
                lines.Add($"    - Дальняя цель: {Markup.Escape(longTerm)}");
            if (!string.IsNullOrWhiteSpace(plan))
                lines.Add($"    - План: [dim]{Markup.Escape(plan)}[/]");
            if (includeGmDiagnostics && !string.IsNullOrWhiteSpace(thoughts))
                lines.Add($"    - Мотивация: [dim]{Markup.Escape(thoughts)}[/]");
        }

        var activeQuests = personalQuests?
            .OfType<JsonObject>()
            .Where(quest => string.Equals(AfterlifeEntityProfileState.GetNodeString(quest["status"]), "active", StringComparison.OrdinalIgnoreCase))
            .ToList() ?? new List<JsonObject>();
        if (activeQuests.Count > 0)
        {
            lines.Add("    - Личные квесты сущности:");
            foreach (var quest in activeQuests)
            {
                var title = AfterlifeEntityProfileState.GetNodeString(quest["title"]) ??
                            AfterlifeEntityProfileState.GetNodeString(quest["questId"]) ??
                            "Без названия";
                var plan = AfterlifeEntityProfileState.GetNodeString(quest["planSummary"]);
                lines.Add($"      · {Markup.Escape(title)}");
                if (!string.IsNullOrWhiteSpace(plan))
                    lines.Add($"        [dim]{Markup.Escape(plan)}[/]");
            }
        }

        if (currentActivity != null)
        {
            var summary = AfterlifeEntityProfileState.GetNodeString(currentActivity["summary"]) ??
                          AfterlifeEntityProfileState.GetNodeString(currentActivity["activityId"]) ??
                          "не описано";
            var thoughts = AfterlifeEntityProfileState.GetNodeString(currentActivity["gmThoughtsSummary"]);
            lines.Add($"    - Сейчас делает: [white]{Markup.Escape(summary)}[/]");
            if (includeGmDiagnostics && !string.IsNullOrWhiteSpace(thoughts))
                lines.Add($"      [dim]Почему: {Markup.Escape(thoughts)}[/]");
        }

        var latestCompleted = completedActivities?.OfType<JsonObject>().LastOrDefault();
        if (latestCompleted != null)
        {
            var summary = AfterlifeEntityProfileState.GetNodeString(latestCompleted["completionSummary"]) ??
                          AfterlifeEntityProfileState.GetNodeString(latestCompleted["summary"]);
            if (!string.IsNullOrWhiteSpace(summary))
                lines.Add($"    - Последняя завершённая активность: [dim]{Markup.Escape(summary)}[/]");
        }
    }

    private static string DescribeFateCardStatus(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "hidden" => "скрыта",
            "available" => "может быть открыта",
            "unlocked" => "открыта",
            _ => "закрыта"
        };

    private static string DescribeAfterlifeRelationshipAxis(string? axis) =>
        axis?.Trim().ToLowerInvariant() switch
        {
            "trust" => "Доверие",
            "romance" => "Романтическая связь",
            "rivalry" => "Соперничество",
            "oath" => "Клятва",
            "fear" => "Страх",
            "reverence" => "Почтение",
            "debt" => "Долг",
            _ => axis ?? "?"
        };

    private static string DescribeAfterlifeRelationshipLock(string? lockState) =>
        lockState?.Trim().ToLowerInvariant() switch
        {
            "positive_locked" => "положительный порог требует личного прорыва",
            "negative_locked" => "отрицательный порог требует искупления",
            "point_of_no_return" => "точка невозврата",
            _ => lockState ?? "гейт"
        };

    private static string DescribeAfterlifeRelationshipQuestStatus(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "completed" => "завершён",
            "failed" => "провален",
            "cancelled" => "отменён",
            _ => "активен"
        };

    private static string DescribeAfterlifeMaskRisk(string? risk) =>
        risk?.Trim().ToLowerInvariant() switch
        {
            "low" => "низкий",
            "medium" => "средний",
            "high" => "высокий",
            "critical" => "критический",
            _ => risk ?? "не указан"
        };

    private static bool IsFateCardPlayerVisible(JsonObject card)
    {
        var secret = card["isSecret"] is JsonValue secretValue &&
                     secretValue.TryGetValue<bool>(out var secretBool) &&
                     secretBool;
        var status = AfterlifeEntityProfileState.GetNodeString(card["status"]);
        return !secret && !string.Equals(status, "hidden", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountFateCardEffects(JsonObject card) =>
        AfterlifeEntityProfileState.FateCardMechanicalEffectProperties.Sum(propertyName =>
            card[propertyName] is JsonArray array ? array.Count : 0);

    private static IEnumerable<string> ReadProfileStringArray(JsonArray? array)
    {
        if (array == null)
            yield break;

        foreach (var item in array)
        {
            var value = AfterlifeEntityProfileState.GetNodeString(item);
            if (!string.IsNullOrWhiteSpace(value))
                yield return value;
        }
    }

    private static JsonObject? ReadLatestProgressionLedgerEntry(JsonArray? ledger)
    {
        if (ledger == null || ledger.Count == 0)
            return null;

        return ledger.OfType<JsonObject>().LastOrDefault();
    }

    private static string DescribeAfterlifeEntityActorType(string? actorType) =>
        actorType?.Trim().ToLowerInvariant() switch
        {
            "player_soul" => "Душа игрока",
            "guardian" => "Хранитель",
            "resident" => "Резидент",
            "shining_resident" => "Резидент Сияющей Обители",
            "shining_faction_head" => "Глава фракции",
            "saref_agent" => "Агент Сарефа",
            "system_actor" => "Системная сила",
            "radiant_actor" => "Сияющий актор",
            "custom_afterlife_actor" => "Особая сущность",
            _ => actorType ?? "?"
        };

    private static string DescribeAfterlifeEntityRealm(string? realm) =>
        realm?.Trim().ToLowerInvariant() switch
        {
            "chaos sea" => "Море Хаоса",
            "море хаоса" => "Море Хаоса",
            "shining abode" => "Сияющая Обитель",
            "сияющая обитель" => "Сияющая Обитель",
            _ => realm ?? "?"
        };

    private static string DescribeAfterlifeEntityArt(string? artId) =>
        artId?.Trim().ToLowerInvariant() switch
        {
            "pressure" => "Давление",
            "counter" => "Контрприём",
            "guard" => "Защита",
            "maneuver" => "Манёвр",
            "binding" => "Оковы",
            "force_binding" => "Силовые оковы",
            "break_binding" => "Разрыв оков",
            "incarnation_resistance" => "Сопротивление воплощению",
            "champion_coordination" => "Координация чемпиона",
            "recover_spiritual_power" => "Собрать Средоточие",
            _ => artId ?? "?"
        };

    private static string? NormalizeAfterlifeCombatPlayerText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var result = value.Trim();
        foreach (var (token, replacement) in AfterlifeCombatPlayerTextTokenReplacements)
        {
            result = Regex.Replace(
                result,
                $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(token)}(?![\p{{L}}\p{{N}}_])",
                replacement,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return IsAfterlifeCombatPlayerTextSafe(result) ? result : null;
    }

    private static bool IsAfterlifeCombatPlayerTextSafe(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var lower = value.Trim().ToLowerInvariant();
        if (lower.Contains("baseoperation", StringComparison.Ordinal) ||
            lower.Contains("specialartaudit", StringComparison.Ordinal) ||
            lower.Contains("auditrequirement", StringComparison.Ordinal) ||
            lower.Contains("sourceoperation", StringComparison.Ordinal) ||
            lower.Contains("costmultiplier", StringComparison.Ordinal) ||
            lower.Contains("game_state/", StringComparison.Ordinal) ||
            lower.Contains(".json", StringComparison.Ordinal) ||
            lower.Contains("after.", StringComparison.Ordinal) ||
            lower.Contains("before.", StringComparison.Ordinal) ||
            lower.Contains("pre-turn", StringComparison.Ordinal) ||
            lower.Contains("learning receipt", StringComparison.Ordinal) ||
            lower.Contains("authority", StringComparison.Ordinal) ||
            lower.Contains("exchange payoff", StringComparison.Ordinal))
        {
            return false;
        }

        if (lower.StartsWith("use ", StringComparison.Ordinal) ||
            lower.StartsWith("add ", StringComparison.Ordinal) ||
            lower.StartsWith("set ", StringComparison.Ordinal) ||
            lower.StartsWith("write ", StringComparison.Ordinal) ||
            lower.StartsWith("record ", StringComparison.Ordinal))
        {
            return false;
        }

        if (Regex.IsMatch(value, @"\b[a-z]+(?:_[a-z0-9]+){1,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return false;

        var latinLetters = value.Count(static ch => (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z'));
        var cyrillicLetters = value.Count(static ch => ch >= '\u0400' && ch <= '\u04FF');
        return latinLetters <= 0 ||
               cyrillicLetters > 0 ||
               latinLetters <= 18;
    }

    private static readonly (string Token, string Replacement)[] AfterlifeCombatPlayerTextTokenReplacements =
    [
        ("pressure", "давление"),
        ("guard", "защита"),
        ("counter", "контрприём"),
        ("maneuver", "манёвр"),
        ("binding", "оковы"),
        ("force_binding", "силовые оковы"),
        ("break_binding", "разрыв оков"),
        ("recover_spiritual_power", "сбор Средоточия"),
        ("incarnation_resistance", "сопротивление воплощению"),
        ("champion_coordination", "координация чемпиона"),
        ("tempoAdvantage", "темповое преимущество"),
        ("strain", "напряжение"),
        ("controlState", "контроль / оковы"),
        ("incomingAction", "входящее действие"),
        ("conflictPosition", "позиция конфликта"),
        ("oppositionSideStrain", "напряжение противника"),
        ("playerSideStrain", "напряжение души")
    ];
}
