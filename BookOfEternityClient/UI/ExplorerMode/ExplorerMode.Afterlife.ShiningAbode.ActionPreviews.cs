using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private bool ConfirmShiningCoreActionRequestPreview(
        ShiningContext context,
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        string confirmationTitle = "Подтвердить действие Обители",
        string confirmChoice = "✅ Создать pending request",
        int relicRerollsToCommit = 0)
    {
        var lines = BuildShiningCoreActionRequestPreviewLines(context, request, relicRerollsToCommit);

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" ✨ Полный предпросмотр контракта Сияющей Обители ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        var requestAudit = JsonSerializer.SerializeToNode(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed) as JsonObject;
        WriteJsonAuditPanel("Полный JSON pending_shining_abode_actions.json.requests[0]", requestAudit, Color.Gold1);
        WriteJsonAuditPanel(
            "Ожидаемый каркас coreActionReceipts[] (скрытые runtime details удалены)",
            BuildShiningCoreExpectedReceiptAuditNode(request),
            Color.Gold1);

        var choice = Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]{Markup.Escape(confirmationTitle)}[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(confirmChoice, "← Отмена"));

        return choice.Contains("Создать", StringComparison.OrdinalIgnoreCase) ||
               choice.Contains("Подтвердить", StringComparison.OrdinalIgnoreCase);
    }

    private List<string> BuildShiningCoreActionRequestPreviewLines(
        ShiningContext context,
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        int relicRerollsToCommit = 0)
    {
        var actionType = request.ActionType?.Trim() ?? string.Empty;
        var lines = new List<string>
        {
            "[bold yellow]Перед записью pending-контракта[/]",
            "",
            $"  Действие: [white]{Markup.Escape(DescribeShiningCoreActionLabel(actionType))}[/] [dim]({Markup.Escape(actionType)})[/]",
            $"  Файл: [dim]{Markup.Escape(ShiningCoreActionRequestState.PendingActionsRequestPath)}[/]",
            $"  requestId: [dim]{Markup.Escape(request.RequestId)}[/]",
            $"  createdAtTurn: [dim]{request.CreatedAtTurn}[/]",
            $"  createdAtUtc: [dim]{Markup.Escape(request.CreatedAtUtc)}[/]",
            "",
            "[bold]Правило очереди:[/]",
            "  • Пока этот request не закрыт accepted/refused/withdrawn receipt, другой Shining core action создавать нельзя.",
            "  • GM закрывает контракт только каноническим изменением `shining_abode_state.json` и записью в `coreActionReceipts[]`.",
            "  • Выводы смертного мира, фракции смертного мира, NPC, локации и время здесь запрещены."
        };

        AppendShiningCoreRequestTargetLines(lines, context, request);
        AppendShiningCoreRequestCostLines(lines, context, request, relicRerollsToCommit);
        AppendShiningCoreRequestActionEffectLines(lines, context, request);
        AppendShiningCoreRequestClosureLines(lines, request);

        return lines;
    }

    private void AppendShiningCoreRequestTargetLines(
        List<string> lines,
        ShiningContext context,
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request)
    {
        var hasTarget = !string.IsNullOrWhiteSpace(request.FactionId) ||
                        !string.IsNullOrWhiteSpace(request.ProjectId) ||
                        !string.IsNullOrWhiteSpace(request.RelicId) ||
                        request.ProjectDraft != null ||
                        request.SelectedCardIds.Count > 0;
        if (!hasTarget)
            return;

        lines.Add("");
        lines.Add("[bold]Цели контракта:[/]");

        if (!string.IsNullOrWhiteSpace(request.FactionId) || !string.IsNullOrWhiteSpace(request.FactionName))
        {
            var factionName = string.IsNullOrWhiteSpace(request.FactionName)
                ? ResolveShiningFactionLabel(context.Root, request.FactionId)
                : request.FactionName;
            lines.Add($"  • Фракция: [white]{Markup.Escape(factionName)}[/] [dim]({Markup.Escape(request.FactionId)})[/]");
            var faction = FindShiningFactionForPreview(context.Root, request.FactionId);
            if (faction != null)
            {
                lines.Add($"    Текущая сила: [dim]{GetNodeInt(faction["factionStrength"])}[/]");
                lines.Add($"    Инвестиций за восхождение: [dim]{GetNodeInt(faction["investCountThisAscension"])}/3[/]");
                lines.Add($"    Торговый tier: [dim]{ShiningAbodeState.GetTradeTier(GetNodeInt(faction["factionStrength"]))}[/]");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.ProjectId) || !string.IsNullOrWhiteSpace(request.ProjectDisplayName))
        {
            var projectName = string.IsNullOrWhiteSpace(request.ProjectDisplayName)
                ? ResolveShiningProjectLabel(context.Root, request.FactionId, request.ProjectId)
                : request.ProjectDisplayName;
            lines.Add($"  • Проект: [white]{Markup.Escape(projectName)}[/] [dim]({Markup.Escape(request.ProjectId)})[/]");
            var project = FindShiningProjectForPreview(context.Root, request.FactionId, request.ProjectId);
            if (project != null)
            {
                lines.Add($"    Статус: [dim]{Markup.Escape(DescribeShiningProjectStatus(GetNodeString(project["status"])))}[/]");
                lines.Add($"    Поддержка сейчас: [dim]{(GetNodeBool(project["isSupported"]) ? "да" : "нет")}[/]");
            lines.Add($"    Награда силы: [dim]{GetNodeInt(project["strengthReward"])}[/]");
            }
        }

        if (request.ProjectDraft is JsonObject draft)
        {
            lines.Add("  • Черновик нового завершённого проекта:");
            lines.Add($"    Название: [white]{Markup.Escape(GetNodeString(draft["displayName"]) ?? "без названия")}[/]");
            lines.Add($"    Уровень проекта: [dim]{GetNodeInt(draft["tier"])}[/]");
            lines.Add($"    Архетип: [dim]{Markup.Escape(DescribeShiningProjectArchetype(GetNodeString(draft["projectArchetype"])))}[/]");
            lines.Add($"    Семейство эффекта: [dim]{Markup.Escape(DescribeShiningEffectFamily(GetNodeString(draft["outputEffectFamily"])))}[/]");
            lines.Add($"    Награда силы по уровню проекта: [dim]{ResolveShiningProjectStrengthRewardForPreview(GetNodeInt(draft["tier"]))}[/]");
            lines.Add("    Любимый архетип фракции меняет только quoted cost, но не strengthReward.");
            AppendShiningNamedIdList(lines, "Целевые фракции", draft["targetFactionIds"] as JsonArray, id => ResolveShiningFactionLabel(context.Root, id));
            AppendShiningStringList(lines, "Тоновые метки", draft["toneTags"] as JsonArray);
        }

        if (!string.IsNullOrWhiteSpace(request.RelicId) || !string.IsNullOrWhiteSpace(request.RelicName))
        {
            var relicName = string.IsNullOrWhiteSpace(request.RelicName) ? request.RelicId : request.RelicName;
            lines.Add($"  • Реликвия: [white]{Markup.Escape(relicName)}[/] [dim]({Markup.Escape(request.RelicId)})[/]");
            var relic = FindSoulRelicForPreview(context.SoulRoot, request.RelicId);
            if (relic != null)
            {
                lines.Add($"    Текущая редкость: [dim]{Markup.Escape(DescribeForgeRarity(GetNodeString(relic["quality"]) ?? GetNodeString(relic["rarity"]) ?? string.Empty))}[/]");
                if (!string.IsNullOrWhiteSpace(GetNodeString(relic["formTag"])))
                    lines.Add($"    Текущая форма: [dim]{Markup.Escape(DescribeForgeFormTag(GetNodeString(relic["formTag"])))}[/]");
                lines.Add($"    Свойств: [dim]{GetForgePropertyCount(relic)}[/]");
            }
        }

        if (request.SelectedCardIds.Count > 0)
        {
            lines.Add("  • Выбранные карты Врат:");
            var selectedCards = GetConsistentPreparedPackageRequestCards(request);
            if (selectedCards.Count > 0)
            {
                foreach (var card in selectedCards)
                    lines.AddRange(BuildShiningBlessingCardInspectionLines(card, context, isSelected: true).Select(line => $"    {line}"));
            }
            else
            {
                foreach (var cardId in request.SelectedCardIds)
                    lines.Add($"    • {Markup.Escape(ResolveShiningBlessingCardLabel(context.Root, cardId))} [dim]({Markup.Escape(cardId)})[/]");
            }
        }
    }

    private static void AppendShiningCoreRequestCostLines(
        List<string> lines,
        ShiningContext context,
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        int relicRerollsToCommit = 0)
    {
        var currentFeathers = CurrentInkFeathersForPreview(context.SoulRoot);
        var currentLightSparks = GetNodeInt(context.Root["lightSparks"]);
        var nextFeathers = Math.Max(0, currentFeathers - Math.Max(0, request.QuotedCostFeathers));
        var nextLightSparks = Math.Max(0, currentLightSparks - Math.Max(0, request.QuotedCostLightSparks));

        lines.Add("");
        lines.Add("[bold]Стоимость и ресурсы:[/]");
        lines.Add($"  • Чернильные Перья: [white]{currentFeathers}[/] -> [white]{nextFeathers}[/] [dim](quotedCostFeathers={request.QuotedCostFeathers})[/]");
        lines.Add($"  • Искры Света: [white]{currentLightSparks}[/] -> [white]{nextLightSparks}[/] [dim](quotedCostLightSparks={request.QuotedCostLightSparks})[/]");
        if (relicRerollsToCommit > 0)
        {
            var currentRelicRerolls = ShiningBlessingEffectState.GetPendingRelicRerolls(context.SoulRoot);
            lines.Add($"  • Перебросы реликвий от благословений: [white]{currentRelicRerolls}[/] -> [white]{Math.Max(0, currentRelicRerolls - relicRerollsToCommit)}[/] [dim](списываются только после подтверждения; отмена сохраняет право)[/]");
        }

        if (request.QuotedCostFeathers == 0 && request.QuotedCostLightSparks == 0)
            lines.Add("  • Это действие не списывает ресурсы; GM всё равно должен записать closure receipt.");
    }

    private void AppendShiningCoreRequestActionEffectLines(
        List<string> lines,
        ShiningContext context,
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request)
    {
        lines.Add("");
        lines.Add("[bold]Ожидаемый принятый исход:[/]");

        switch ((request.ActionType ?? string.Empty).Trim().ToLowerInvariant())
        {
            case ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction:
                lines.Add("  • Создать новый hall и новую `native_radiant` faction, которых не было в pre-turn state.");
                lines.Add("  • Материализовать 2..4 новых ascended residents, сразу привязанных к новой фракции.");
                lines.Add("  • Создать ровно 2 seeded completed projects внутри новой фракции.");
                lines.Add("  • Radiance XP: +20; Light Sparks и Ink Feathers списываются по quoted cost.");
                break;

            case ShiningCoreActionRequestState.ActionTypeInvestInFaction:
                AppendProjectedFactionDelta(lines, context, request);
                lines.Add("  • investCountThisAscension увеличивается на 1; лимит 3 investment за ascension.");
                lines.Add("  • Сила фракции пересчитывается canonically; если открыт draft Врат, он становится stale.");
                break;

            case ShiningCoreActionRequestState.ActionTypeCompleteProject:
                AppendProjectedFactionDelta(lines, context, request);
                lines.Add("  • Новый project должен быть materialized as completed project с canonical strengthReward по tier.");
                lines.Add("  • Если archetype ещё не считался в этом ascension, Radiance XP получает +10 и tier пересчитывается.");
                lines.Add("  • Если открыт draft Врат, он становится stale.");
                break;

            case ShiningCoreActionRequestState.ActionTypeSupportProject:
                AppendProjectedProjectSupportDelta(lines, context, request, support: true);
                lines.Add("  • Проект получает `isSupported=true`; ресурсной цены нет.");
                lines.Add("  • Если открыт draft Врат, он становится stale.");
                break;

            case ShiningCoreActionRequestState.ActionTypeUnsupportProject:
                AppendProjectedProjectSupportDelta(lines, context, request, support: false);
                lines.Add("  • Проект получает `isSupported=false`; ресурсной цены нет.");
                lines.Add("  • Если открыт draft Врат, он становится stale.");
                break;

            case ShiningCoreActionRequestState.ActionTypeRetireProject:
                AppendProjectedProjectSupportDelta(lines, context, request, support: false);
                lines.Add("  • Проект получает `status=retired` и `isSupported=false`; сила фракции пересчитывается.");
                lines.Add("  • Если открыт draft Врат, он становится stale.");
                break;

            case ShiningCoreActionRequestState.ActionTypeOpenGates:
                var radianceTier = GetNodeInt(context.Root["radiance"]?["tier"]);
                lines.Add($"  • Создать fresh gates draft: draftVersion +1, hasOpenDraft=true, isStale=false.");
                lines.Add($"  • Размер набора по Radiance tier {radianceTier}: {ShiningAbodeState.GetDraftSize(radianceTier)}; лимит выбора: {ShiningAbodeState.GetPickCap(radianceTier)}.");
                lines.Add("  • selectedBlessingCardIds очищается; shown/available/allCandidate card arrays фиксируются в state.");
                lines.Add("  • rerollsRemaining = число supported Remembrance projects.");
                break;

            case ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage:
                lines.Add($"  • Сформировать `preparedIncarnationPackage` из draftVersion {request.SourceDraftVersion} и selectedCards snapshot.");
                lines.Add("  • Очистить gates до default closed state.");
                lines.Add("  • Soul state и ресурсы не меняются; `TriggerIncarnation` НЕ пишется в этот же turn.");
                lines.Add("  • Runtime позже сам consumed/cleared package после successful Mortal bootstrap.");
                break;

            case ShiningCoreActionRequestState.ActionTypePullRelicGacha:
                lines.Add($"  • Return cycle: [dim]{Markup.Escape(request.ReturnCycleId)}[/].");
                var gachaSystem = context.Root["gachaSystem"] as JsonObject;
                var chargesUsed = GetNodeInt(gachaSystem?["chargesUsedThisReturn"]);
                var chargesPerReturn = GetNodeInt(gachaSystem?["chargesPerReturn"]);
                lines.Add($"  • chargesUsedThisReturn: {chargesUsed} -> {chargesUsed + 1} из {chargesPerReturn}; projected bonus ceiling: +{request.ProjectedGachaBonusSteps} rarity step(s).");
                if (FindShiningFactionForPreview(context.Root, request.FactionId) is JsonObject gachaFaction)
                {
                    lines.Add($"  • Bonus contributors: Radiance tier {GetNodeInt(context.Root["radiance"]?["tier"])}, factionStrength {GetNodeInt(gachaFaction["factionStrength"])}, supported projects/residents from current Shining state.");
                    lines.Add($"  • Trade/forge context for same faction: tradeTier {ShiningAbodeState.GetTradeTier(GetNodeInt(gachaFaction["factionStrength"]))}, rarity ceiling {Markup.Escape(ShiningAbodeState.GetTradeRarityCeiling(GetNodeInt(gachaFaction["factionStrength"])))}.");
                }
                lines.Add("  • GM берёт `turn_request.gachaBaseResult.baseRarity` as rarity floor, может поднять итог не выше projected bonus ceiling.");
                lines.Add("  • Soul state получает ровно одну новую Soul Relic с id/name из receipt; unrelated Soul fields не меняются.");
                lines.Add("  • gachaSystem получает currentReturnCycleId, chargesUsedThisReturn и gachaHistory entry.");
                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicReshape:
            case ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty:
            case ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand:
            case ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho:
            case ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity:
                AppendShiningForgeContractEffectLines(lines, context, request);
                break;

            default:
                lines.Add("  • Unsupported preview branch; validator will still enforce the exact request contract.");
                break;
        }
    }

    private static void AppendShiningCoreRequestClosureLines(
        List<string> lines,
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request)
    {
        lines.Add("");
        lines.Add("[bold]Контракт закрытия для GM:[/]");
        lines.Add("  • Every `coreActionReceipts[]` entry must include the full validator schema: requestId, actionType, factionId, projectId, relicId, returnCycleId, targetFormTag, quotedCostFeathers, quotedCostLightSparks, selectedCardIds[], newResidentIds[], seededProjectIds[], generatedDraftVersion, status, resolvedAtTurn, resolvedAtUtc, reason.");
        lines.Add($"  • Echo exact request values: factionId=`{Markup.Escape(request.FactionId)}`, projectId=`{Markup.Escape(request.ProjectId)}`, relicId=`{Markup.Escape(request.RelicId)}`, returnCycleId=`{Markup.Escape(request.ReturnCycleId)}`, targetFormTag=`{Markup.Escape(request.TargetFormTag)}`.");
        lines.Add($"  • Echo quoted costs exactly: quotedCostFeathers={request.QuotedCostFeathers}, quotedCostLightSparks={request.QuotedCostLightSparks}; do not recompute them in prose.");
        lines.Add("  • For accepted status, canonical state must exactly match the action helper projection.");
        lines.Add("  • For refused/withdrawn status, state remains unchanged except `coreActionReceipts[]`.");
        lines.Add("  • `pending_shining_abode_actions.json` is client-owned input; GM must not rewrite it as output.");

        if (request.ActionType.Equals(ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction, StringComparison.OrdinalIgnoreCase))
            lines.Add("  • discover_native_faction accepted receipt must fill newResidentIds[] and seededProjectIds[] with every materialized resident/project id; refused/withdrawn keeps both arrays empty.");
        if (request.ActionType.Equals(ShiningCoreActionRequestState.ActionTypeOpenGates, StringComparison.OrdinalIgnoreCase))
            lines.Add("  • open_gates accepted receipt must set generatedDraftVersion to the new positive gates.draftVersion; refused/withdrawn may keep generatedDraftVersion=0.");
        if (request.ActionType.Equals(ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage, StringComparison.OrdinalIgnoreCase))
            lines.Add("  • prepare receipt must include selectedCardIds[] and selectedCards snapshot matching the frozen package, with generatedDraftVersion equal to sourceDraftVersion.");
        if (request.ActionType.Equals(ShiningCoreActionRequestState.ActionTypePullRelicGacha, StringComparison.OrdinalIgnoreCase))
            lines.Add("  • gacha receipt must include baseRarity, finalRarity, relicId, relicName and returnCycleId.");
        if (ShiningAbodeState.IsForgeActionType(request.ActionType))
            lines.Add("  • forge receipt must echo relicId/relicName and mutation fields such as targetFormTag/propertyIndex/replacementProperty/addedProperties.");
    }

    private static JsonObject BuildShiningCoreExpectedReceiptAuditNode(ShiningCoreActionRequestState.PendingShiningCoreActionRequest request)
    {
        var selectedCardIds = new JsonArray(request.SelectedCardIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => (JsonNode?)id)
            .ToArray());
        var receipt = new JsonObject
        {
            ["requestId"] = request.RequestId,
            ["actionType"] = request.ActionType,
            ["factionId"] = request.FactionId,
            ["projectId"] = request.ProjectId,
            ["relicId"] = request.RelicId,
            ["returnCycleId"] = request.ReturnCycleId,
            ["targetFormTag"] = request.TargetFormTag,
            ["quotedCostFeathers"] = request.QuotedCostFeathers,
            ["quotedCostLightSparks"] = request.QuotedCostLightSparks,
            ["selectedCardIds"] = selectedCardIds,
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["generatedDraftVersion"] = ResolveExpectedReceiptDraftVersionForPreview(request),
            ["status"] = "accepted|refused|withdrawn",
            ["resolvedAtTurn"] = "current turn number",
            ["resolvedAtUtc"] = "ISO-8601 UTC timestamp",
            ["reason"] = "canonical human-readable closure reason"
        };

        if (request.SelectedCards is JsonArray selectedCards)
            receipt["selectedCards"] = CloneShiningJsonForPlayerFacingAudit(selectedCards);
        if (!string.IsNullOrWhiteSpace(request.RelicName))
            receipt["relicName"] = request.RelicName;
        if (request.PropertyIndex >= 0)
            receipt["propertyIndex"] = request.PropertyIndex;
        if (request.ReplacementProperty != null)
            receipt["replacementProperty"] = CloneShiningJsonForPlayerFacingAudit(request.ReplacementProperty);
        if (request.AddedProperties != null)
            receipt["addedProperties"] = CloneShiningJsonForPlayerFacingAudit(request.AddedProperties);

        return receipt;
    }

    private static int ResolveExpectedReceiptDraftVersionForPreview(ShiningCoreActionRequestState.PendingShiningCoreActionRequest request)
    {
        if (request.ActionType.Equals(ShiningCoreActionRequestState.ActionTypeOpenGates, StringComparison.OrdinalIgnoreCase))
            return Math.Max(1, request.SourceDraftVersion + 1);

        if (request.ActionType.Equals(ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage, StringComparison.OrdinalIgnoreCase))
            return Math.Max(0, request.SourceDraftVersion);

        return 0;
    }

    private void AppendProjectedFactionDelta(
        List<string> lines,
        ShiningContext context,
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request)
    {
        var beforeFaction = FindShiningFactionForPreview(context.Root, request.FactionId);
        if (!ShiningCoreActionRequestState.TryBuildProjectedShiningRootForPreview(
                request,
                context.Root,
                context.ResidentRoot,
                out var projectedRoot))
        {
            return;
        }

        var afterFaction = FindShiningFactionForPreview(projectedRoot, request.FactionId);
        if (beforeFaction == null || afterFaction == null)
            return;

        lines.Add($"  • Сила фракции: {GetNodeInt(beforeFaction["factionStrength"])} -> {GetNodeInt(afterFaction["factionStrength"])}.");
        lines.Add($"  • Light Sparks state: {GetNodeInt(context.Root["lightSparks"])} -> {GetNodeInt(projectedRoot["lightSparks"])}.");
        lines.Add($"  • Radiance XP: {GetNodeInt(context.Root["radiance"]?["experience"])} -> {GetNodeInt(projectedRoot["radiance"]?["experience"])}.");
    }

    private void AppendProjectedProjectSupportDelta(
        List<string> lines,
        ShiningContext context,
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        bool support)
    {
        var project = FindShiningProjectForPreview(context.Root, request.FactionId, request.ProjectId);
        if (project == null)
            return;

        var radianceTier = GetNodeInt(context.Root["radiance"]?["tier"]);
        var supportedBefore = ShiningAbodeState.CountSupportedProjectsAcrossState(context.Root);
        var supportedCap = ShiningAbodeState.GetSupportedProjectCap(radianceTier);
        lines.Add($"  • Project support flag: {GetNodeBool(project["isSupported"])} -> {support}.");
        lines.Add($"  • Support cap по Radiance tier {radianceTier}: {supportedBefore}/{supportedCap} сейчас.");
        lines.Add($"  • Архетип: {Markup.Escape(DescribeShiningProjectArchetype(GetNodeString(project["projectArchetype"])))}; effect family: {Markup.Escape(DescribeShiningEffectFamily(GetNodeString(project["outputEffectFamily"])))}; tier {GetNodeInt(project["tier"])}.");
        if (!string.IsNullOrWhiteSpace(GetNodeString(project["summary"])))
            lines.Add($"  • Summary: {Markup.Escape(GetNodeString(project["summary"])!)}");
        AppendShiningStringList(lines, "Тоновые метки", project["toneTags"] as JsonArray);
        AppendShiningNamedIdList(lines, "Целевые фракции", project["targetFactionIds"] as JsonArray, id => ResolveShiningFactionLabel(context.Root, id));

        if (ShiningCoreActionRequestState.TryBuildProjectedShiningRootForPreview(
                request,
                context.Root,
                context.ResidentRoot,
                out var projectedRoot) &&
            FindShiningFactionForPreview(context.Root, request.FactionId) is JsonObject beforeFaction &&
            FindShiningFactionForPreview(projectedRoot, request.FactionId) is JsonObject afterFaction)
        {
            var beforeStrength = GetNodeInt(beforeFaction["factionStrength"]);
            var afterStrength = GetNodeInt(afterFaction["factionStrength"]);
            lines.Add($"  • Faction strength/trade tier: {beforeStrength} / tier {ShiningAbodeState.GetTradeTier(beforeStrength)} -> {afterStrength} / tier {ShiningAbodeState.GetTradeTier(afterStrength)}.");
            lines.Add($"  • Trade slots/rarity/service after mutation: {ShiningAbodeState.GetTradeStockItemCount(afterFaction, context.ResidentRoot)} slots, ceiling {Markup.Escape(ShiningAbodeState.GetTradeRarityCeiling(afterStrength))}, service x{ShiningAbodeState.GetServiceMultiplier(afterStrength):0.00}.");
            lines.Add($"  • Gacha bonus after mutation: +{ShiningAbodeState.GetProjectedShiningGachaBonusSteps(projectedRoot, context.ResidentRoot, afterFaction)} rarity step(s).");
        }
    }

    private void AppendShiningForgeContractEffectLines(
        List<string> lines,
        ShiningContext context,
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request)
    {
        var relic = FindSoulRelicForPreview(context.SoulRoot, request.RelicId);
        var faction = FindShiningFactionForPreview(context.Root, request.FactionId);
        lines.Add("  • Mutates exactly one Soul Relic plus resource costs and blessing entitlement lifecycle.");
        lines.Add("  • Shining resident state must remain unchanged.");
        if (faction != null)
        {
            var strength = GetNodeInt(faction["factionStrength"]);
            var serviceMultiplier = ShiningAbodeState.GetServiceMultiplier(strength);
            lines.Add($"  • Forge pricing context: factionStrength {strength}, serviceMultiplier x{serviceMultiplier:0.00}, quotedCostFeathers={request.QuotedCostFeathers}, quotedCostLightSparks={request.QuotedCostLightSparks}.");
        }
        lines.Add("  • Pending JSON ниже содержит exact mutation payload: relicId, targetFormTag/propertyIndex/replacementProperty/addedProperties.");

        switch ((request.ActionType ?? string.Empty).Trim().ToLowerInvariant())
        {
            case ShiningCoreActionRequestState.ActionTypeForgeRelicReshape:
                lines.Add($"  • Форма реликвии: {Markup.Escape(DescribeForgeFormTag(GetNodeString(relic?["formTag"])))} → {Markup.Escape(DescribeForgeFormTag(request.TargetFormTag))}.");
                break;
            case ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty:
                if (relic != null && TryGetForgeProperty(relic, request.PropertyIndex, out var currentProperty))
                    lines.Add($"  • Выбранное свойство: {Markup.Escape(RenderForgePropertyLabel(currentProperty, request.PropertyIndex))}.");
                if (request.ReplacementProperty != null)
                    lines.Add($"  • Новая версия свойства: {Markup.Escape(RenderForgePropertyLabel(request.ReplacementProperty))}.");
                break;
            case ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand:
                if (relic != null && TryGetForgeProperty(relic, request.PropertyIndex, out var property) &&
                    TryDescribeForgeBandUpgrade(property["band"], out var currentBand, out var upgradedBand))
                {
                    lines.Add($"  • Property #{request.PropertyIndex + 1} band: {Markup.Escape(currentBand)} -> {Markup.Escape(upgradedBand)}.");
                }
                break;
            case ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho:
                lines.Add($"  • companionManifestationQualityBonus increases from {GetNodeInt(relic?["companionManifestationQualityBonus"])} by the faction service multiplier projection.");
                break;
            case ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity:
                var currentRarity = relic == null ? string.Empty : ResolveForgeRarityKey(relic);
                lines.Add($"  • rarity: {Markup.Escape(DescribeForgeRarity(currentRarity))} -> {Markup.Escape(DescribeForgeRarity(GetNextForgeRarityKey(currentRarity)))}.");
                if (request.AddedProperties is { Count: > 0 })
                    AppendShiningForgePropertyBlock(lines, "Добавленные свойства", request.AddedProperties);
                break;
        }
    }

    private static int CurrentInkFeathersForPreview(JsonObject? soulRoot)
    {
        if (soulRoot?["inkFeathers"] is JsonObject inkFeathers)
            return GetNodeInt(inkFeathers["current"]);

        return GetNodeInt(soulRoot?["inkFeathers"]);
    }

    private static int ResolveShiningProjectStrengthRewardForPreview(int tier) =>
        Math.Clamp(tier, 1, 3) switch
        {
            1 => 8,
            2 => 12,
            _ => 16
        };

    private static JsonObject? CloneJsonObjectForPreview(JsonObject? root) =>
        root == null ? null : JsonNode.Parse(root.ToJsonString()) as JsonObject;

    private static JsonObject? FindShiningFactionForPreview(JsonObject? shiningRoot, string? factionId)
    {
        if (shiningRoot?["factions"] is not JsonArray factions || string.IsNullOrWhiteSpace(factionId))
            return null;

        return factions.OfType<JsonObject>()
            .FirstOrDefault(faction => string.Equals(GetNodeString(faction["factionId"]), factionId, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject? FindShiningProjectForPreview(JsonObject? shiningRoot, string? factionId, string? projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId) || shiningRoot?["factions"] is not JsonArray factions)
            return null;

        foreach (var faction in factions.OfType<JsonObject>())
        {
            if (!string.IsNullOrWhiteSpace(factionId) &&
                !string.Equals(GetNodeString(faction["factionId"]), factionId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var project = (faction["projects"] as JsonArray)?.OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(GetNodeString(item["projectId"]), projectId, StringComparison.OrdinalIgnoreCase));
            if (project != null)
                return project;
        }

        return null;
    }

    private static JsonObject? FindSoulRelicForPreview(JsonObject? soulRoot, string? relicId)
    {
        if (soulRoot == null || string.IsNullOrWhiteSpace(relicId))
            return null;

        foreach (var containerName in new[] { "stored", "equipped" })
        {
            if (soulRoot["soulRelics"]?[containerName] is not JsonArray relics)
                continue;

            var relic = relics.OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(GetNodeString(item["relicId"]), relicId, StringComparison.OrdinalIgnoreCase));
            if (relic != null)
                return relic;
        }

        return null;
    }
}
