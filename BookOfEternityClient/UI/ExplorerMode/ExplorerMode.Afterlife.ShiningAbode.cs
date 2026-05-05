using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private async Task ShowShiningAbodeOverview()
    {
        while (true)
        {
            var context = await LoadShiningContextAsync();
            var coreRequests = await ShiningCoreActionRequestState.ReadRequestsAsync(_fs);
            var tradeRequests = await ShiningTradeRequestState.ReadRequestsAsync(_fs);
            var foundingRequests = await ShiningFactionRequestState.ReadFoundingRequestsAsync(_fs);
            var realignmentRequests = await ShiningFactionRequestState.ReadRealignmentRequestsAsync(_fs);
            var leadershipRequests = await ShiningFactionRequestState.ReadLeadershipTransitionRequestsAsync(_fs);
            if (context == null)
            {
                ShowEmptyPanel("Сияющая Обитель", "Состояние Сияющей Обители ещё не проявлено достаточно явно.");
                WaitForKey();
                return;
            }

            Clear();
            var overviewSignalsPanel = BuildShiningOverviewSignalsPanel(
                context.Root,
                context.ResidentRoot,
                coreRequests.Count,
                tradeRequests.Count,
                foundingRequests.Count,
                realignmentRequests.Count,
                leadershipRequests.Count,
                coreRequests.FirstOrDefault() is { } firstCoreRequest
                    ? $"{DescribeShiningCoreActionLabel(firstCoreRequest.ActionType)} / requestId={firstCoreRequest.RequestId}"
                    : null,
                tradeRequests.FirstOrDefault() is { } firstTradeRequest
                    ? $"Shining trade / requestId={firstTradeRequest.RequestId}, factionId={firstTradeRequest.FactionId}, tradeCycleId={firstTradeRequest.TradeCycleId}"
                    : null,
                foundingRequests.FirstOrDefault() is { } firstFoundingRequest
                    ? $"founding / requestId={firstFoundingRequest.RequestId}, factionId={firstFoundingRequest.ProposedFactionId}"
                    : realignmentRequests.FirstOrDefault() is { } firstRealignmentRequest
                        ? $"realignment / requestId={firstRealignmentRequest.RequestId}, residentId={firstRealignmentRequest.ResidentId}"
                        : leadershipRequests.FirstOrDefault() is { } firstLeadershipRequest
                            ? $"leadership / requestId={firstLeadershipRequest.RequestId}, factionId={firstLeadershipRequest.FactionId}"
                            : null);
            if (overviewSignalsPanel != null)
                Write(overviewSignalsPanel);
            Write(BuildShiningOverviewPanel(context.Root, context.ResidentRoot, context.GuardiansRoot));

            var choices = new List<string>();
            if (_stateManager.CurrentState.IsInShiningAbode)
            {
                choices.Add("✨ Основные действия");
                choices.Add("🚪 Врата и благословения");
                choices.Add("⚒ Торговля и кузня");
            }

            if ((context.Root["gates"] is JsonObject gates && GetNodeBool(gates["hasOpenDraft"])) ||
                context.Root["preparedIncarnationPackage"] is JsonObject)
            {
                choices.Add("🔎 Осмотреть набор и пакет");
            }

            if ((context.Root["halls"] as JsonArray)?.Count > 0 || (context.Root["shiningPoliticalActors"] as JsonArray)?.Count > 0)
                choices.Add("🗺 Осмотреть залы и светозарных акторов");

            if (context.Root["coreActionReceipts"] is JsonArray coreReceipts && coreReceipts.Count > 0)
                choices.Add("📜 Осмотреть исходы Обители");
            if (coreRequests.Count > 0)
                choices.Add("📝 Осмотреть ожидающие действия Обители");
            if ((context.Root["factions"] as JsonArray)?.Count > 0)
                choices.Add("🧾 Осмотреть торговые циклы");
            if ((context.Root["factions"] as JsonArray)?.Count > 0 || (context.ResidentRoot?["entries"] as JsonArray)?.Count > 0)
                choices.Add("🧭 Сводный аудит резидентов и проектов");

            choices.Add("🏛 Политика");
            choices.Add("← Назад");

            var choice = Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]Сияющая Обитель[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .AddChoices(choices));

            if (choice.Contains("Назад", StringComparison.Ordinal))
                return;

            if (choice.Contains("Политика", StringComparison.Ordinal))
            {
                await ShowShiningPoliticsOverview();
                continue;
            }

            if (choice.Contains("Осмотреть набор и пакет", StringComparison.OrdinalIgnoreCase))
            {
                ShowShiningGatesInspectionPanel(context);
                WaitForKey();
                continue;
            }

            if (choice.Contains("исходы Обители", StringComparison.OrdinalIgnoreCase))
            {
                ShowShiningCoreReceiptInspectionPanel(context);
                WaitForKey();
                continue;
            }

            if (choice.Contains("ожидающие действия", StringComparison.OrdinalIgnoreCase))
            {
                ShowShiningPendingCoreActionInspectionPanel(context, coreRequests);
                WaitForKey();
                continue;
            }

            if (choice.Contains("торговые циклы", StringComparison.OrdinalIgnoreCase))
            {
                await ShowShiningTradeLifecycleInspectionAsync(context);
                WaitForKey();
                continue;
            }

            if (choice.Contains("Сводный аудит", StringComparison.OrdinalIgnoreCase))
            {
                ShowShiningResidentProjectAuditPanel(context);
                WaitForKey();
                continue;
            }

            if (choice.Contains("залы", StringComparison.OrdinalIgnoreCase) ||
                choice.Contains("светозарных акторов", StringComparison.OrdinalIgnoreCase))
            {
                ShowShiningStructureInspectionPanel(context);
                WaitForKey();
                continue;
            }

            if (choice.Contains("Основные действия", StringComparison.Ordinal))
            {
                await ShowShiningCoreActionsAsync();
                continue;
            }

            if (choice.Contains("Врата", StringComparison.Ordinal))
                await ShowShiningGatesActionsAsync();
            else if (choice.Contains("кузня", StringComparison.OrdinalIgnoreCase))
                await ShowShiningTradeAndForgeAsync();
        }
    }

    private void ShowShiningResidentProjectAuditPanel(ShiningContext context)
    {
        var lines = new List<string>
        {
            "[bold yellow]Сводный аудит резидентов и проектов Сияющей Обители[/]",
            "",
            "[bold]Фракции, резиденты, проекты:[/] [dim](без перехода в отдельные карточки)[/]"
        };

        var factions = (context.Root["factions"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
        if (factions.Count == 0)
        {
            lines.Add("  • Фракции пока не материализованы.");
        }

        foreach (var faction in factions.OrderByDescending(faction => GetNodeInt(faction["factionStrength"])))
        {
            var factionId = GetNodeString(faction["factionId"]) ?? string.Empty;
            var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? factionId;
            var strength = GetNodeInt(faction["factionStrength"]);
            lines.Add("");
            lines.Add($"[bold]{Markup.Escape(factionName)}[/] [dim]({Markup.Escape(factionId)})[/]");
            lines.Add($"  • Strength: [white]{strength}[/] [dim]({Markup.Escape(ShiningAbodeState.GetFactionStrengthBand(strength))})[/], tradeTier={ShiningAbodeState.GetTradeTier(strength)}, slots={ShiningAbodeState.GetTradeStockItemCount(faction, context.ResidentRoot)}, rarity={Markup.Escape(ShiningAbodeState.GetTradeRarityCeiling(strength))}, service x{ShiningAbodeState.GetServiceMultiplier(strength):0.00}.");
            lines.Add($"  • Leadership: {Markup.Escape(BuildHeadActorLabel(GetNodeString(faction["leadership"]?["headActorType"]), GetNodeString(faction["leadership"]?["headActorId"]), context.ResidentRoot, context.GuardiansRoot, context.Root))} [dim]({Markup.Escape(DescribeShiningLeadershipState(GetNodeString(faction["leadership"]?["leadershipState"])))})[/].");

            var residents = CollectShiningFactionResidents(context.ResidentRoot, factionId);
            lines.Add($"  • Residents: [white]{residents.Count}[/]");
            foreach (var resident in residents)
            {
                var name = GetNodeString(resident["displayName"]) ?? GetNodeString(resident["residentName"]) ?? GetNodeString(resident["residentId"]) ?? "?";
                var residentRole = GetNodeString(resident["residentRole"]);
                var roleLabel = GetNodeString(resident["roleLabel"]);
                var roleText = string.IsNullOrWhiteSpace(roleLabel)
                    ? DescribeShiningResidentRole(residentRole)
                    : $"{DescribeShiningResidentRole(residentRole)} / {roleLabel}";
                lines.Add($"    - {Markup.Escape(name)} [dim]({Markup.Escape(GetNodeString(resident["residentId"]) ?? "?")})[/]: residentRole={Markup.Escape(string.IsNullOrWhiteSpace(residentRole) ? "—" : residentRole)} ({Markup.Escape(roleText)}), loyalty={GetNodeInt(resident["factionLoyaltyLevel"])}/{Markup.Escape(GetNodeString(resident["factionLoyaltyTier"]) ?? "—")}, restlessness={GetNodeInt(resident["factionRestlessness"])}, realignment={Markup.Escape(DescribeShiningFactionRealignmentState(GetNodeString(resident["factionRealignmentState"])))}.");
            }

            var projects = (faction["projects"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
            lines.Add($"  • Projects: [white]{projects.Count}[/]");
            foreach (var project in projects)
            {
                var projectName = GetNodeString(project["displayName"]) ?? GetNodeString(project["projectId"]) ?? "?";
                lines.Add($"    - {Markup.Escape(projectName)} [dim]({Markup.Escape(GetNodeString(project["projectId"]) ?? "?")})[/]: status={Markup.Escape(DescribeShiningProjectStatus(GetNodeString(project["status"])))}, supported={GetNodeBool(project["isSupported"])}, tier={GetNodeInt(project["tier"])}, strengthReward={GetNodeInt(project["strengthReward"])}, archetype={Markup.Escape(DescribeShiningProjectArchetype(GetNodeString(project["projectArchetype"])))}, effect={Markup.Escape(DescribeShiningEffectFamily(GetNodeString(project["outputEffectFamily"])))}.");
            }
        }

        var unaligned = CollectShiningFactionResidents(context.ResidentRoot, string.Empty)
            .Where(resident => string.IsNullOrWhiteSpace(GetNodeString(resident["shiningFactionId"])))
            .ToList();
        if (unaligned.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Без фракции / нейтральные:[/]");
            foreach (var resident in unaligned)
            {
                var name = GetNodeString(resident["displayName"]) ?? GetNodeString(resident["residentName"]) ?? GetNodeString(resident["residentId"]) ?? "?";
                lines.Add($"  • {Markup.Escape(name)} [dim]({Markup.Escape(GetNodeString(resident["residentId"]) ?? "?")})[/]: ascensionState={Markup.Escape(GetNodeString(resident["ascensionState"]) ?? "—")}, realignment={Markup.Escape(DescribeShiningFactionRealignmentState(GetNodeString(resident["factionRealignmentState"])))}.");
            }
        }

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🧭 Shining Residents & Projects ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WriteJsonAuditPanel("JSON shining_abode_state.factions/projects для просмотра (скрытые runtime details удалены)", CloneShiningJsonForPlayerFacingAudit(context.Root["factions"]), Color.Gold1);
        WriteJsonAuditPanel("Полный JSON guardian_abode_residents.json для Shining bindings", context.ResidentRoot, Color.Gold1);
        WriteJsonAuditPanel("JSON shining_abode_state.halls для просмотра (скрытые runtime details удалены)", CloneShiningJsonForPlayerFacingAudit(context.Root["halls"]), Color.Gold1);
        WriteJsonAuditPanel("JSON shining_abode_state.shiningPoliticalActors для просмотра (скрытые runtime details удалены)", CloneShiningJsonForPlayerFacingAudit(context.Root["shiningPoliticalActors"]), Color.Gold1);
    }

    private static List<JsonObject> CollectShiningFactionResidents(JsonObject? residentRoot, string? factionId)
    {
        if (residentRoot?["entries"] is not JsonArray entries)
            return new List<JsonObject>();

        return entries.OfType<JsonObject>()
            .Where(resident =>
            {
                var ascended = string.Equals(GetNodeString(resident["ascensionState"]), "ascended", StringComparison.OrdinalIgnoreCase) ||
                               !string.IsNullOrWhiteSpace(GetNodeString(resident["shiningFactionId"]));
                if (!ascended)
                    return false;

                if (string.IsNullOrWhiteSpace(factionId))
                    return true;

                return string.Equals(GetNodeString(resident["shiningFactionId"]), factionId, StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(resident => GetNodeString(resident["displayName"]) ?? GetNodeString(resident["residentName"]) ?? GetNodeString(resident["residentId"]))
            .ToList();
    }

    private async Task ShowShiningPoliticsOverview()
    {
        while (true)
        {
            var context = await LoadShiningContextAsync();
            var foundingRequests = await ShiningFactionRequestState.ReadFoundingRequestsAsync(_fs);
            var realignmentRequests = await ShiningFactionRequestState.ReadRealignmentRequestsAsync(_fs);
            var leadershipRequests = await ShiningFactionRequestState.ReadLeadershipTransitionRequestsAsync(_fs);
            var feathers = await ReadInkFeathersBalance();
            var lightSparks = GetNodeInt(context?.Root["lightSparks"]);
            var ascendedResidentCount = CountAscendedShiningResidents(context?.ResidentRoot);
            var foundingPreconditionsMet = feathers >= ShiningFactionRequestState.FactionFoundingCostFeathers &&
                                           lightSparks >= ShiningFactionRequestState.FactionFoundingCostLightSparks &&
                                           ascendedResidentCount >= 3;

            var lines = new List<string>
            {
                "[bold yellow]🏛 Политика Сияющей Обители[/]",
                "",
                "[bold]Политическое давление:[/]",
                $"  • Оснований фракций в ожидании: [white]{foundingRequests.Count}[/]",
                $"  • Переходов между фракциями в ожидании: [white]{realignmentRequests.Count}[/]",
                $"  • Смен власти в ожидании: [white]{leadershipRequests.Count}[/]"
            };

            lines.Add("");
            lines.Add("[bold]Доступность политических действий:[/] [dim](стоимости, блокеры и минимальные требования до выбора)[/]");
            lines.Add($"  • Основание фракции: cost {ShiningFactionRequestState.FactionFoundingCostFeathers} Чернильных Перьев / {ShiningFactionRequestState.FactionFoundingCostLightSparks} Искр Света; баланс {feathers}/{lightSparks}; минимум 3 ascended supporters из {ascendedResidentCount}; {(foundingPreconditionsMet ? "базовые условия выполнены" : "не хватает ресурсов или ascended supporters")}. Pending-модель не является глобальным mutex: запись блокируют malformed founding file, founding с тем же proposedFactionId/proposedHallId или supporters, занятые другим Shining/ordinary flow.");
            lines.Add("  • Перестройка резидента: требует ascended resident с factionRealignmentState=ready_to_realign (wavering tier сам по себе не открывает переход) и machine-readable target/source faction. Pending-модель не глобальная: блокируют foreign pending realignment для того же residentId, ordinary transfer или другой Shining flow этого резидента.");
            lines.Add("  • Смена власти: требует существующую faction, валидного incumbent и допустимого кандидата на главу. Pending-модель не глобальная: блокируют foreign pending leadership для той же factionId и supporter/candidate locks; pending других фракций сам по себе не запрещает проверку.");

            if (context?.Root["factions"] is JsonArray factions && factions.Count > 0)
            {
                lines.Add("");
                lines.Add("[bold]Текущее распределение власти:[/] [dim](показаны все фракции без сокращения)[/]");
                foreach (var faction in factions.OfType<JsonObject>())
                {
                    var factionId = GetNodeString(faction["factionId"]) ?? string.Empty;
                    var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? GetNodeString(faction["factionId"]) ?? "?";
                    var hallName = ResolveShiningHallLabel(context.Root, GetNodeString(faction["hallId"]));
                    var state = DescribeShiningLeadershipState(GetNodeString(faction["leadership"]?["leadershipState"]));
                    var headActorType = GetNodeString(faction["leadership"]?["headActorType"]) ?? "vacant";
                    var headActorId = GetNodeString(faction["leadership"]?["headActorId"]) ?? "vacant";
                    var memberCount = CountResidentsInFaction(context.ResidentRoot, factionId);
                    lines.Add($"  • {Markup.Escape(factionName)} — зал {Markup.Escape(hallName)}, глава {Markup.Escape(BuildHeadActorLabel(headActorType, headActorId, context.ResidentRoot, context.GuardiansRoot, context.Root))}, участников {memberCount}, состояние {Markup.Escape(state)}");
                }
            }

            if (foundingRequests.Count > 0)
            {
                lines.Add("");
                lines.Add("[bold]Ожидают решения:[/] основание фракций [dim](все pending-запросы)[/]");
                foreach (var request in foundingRequests)
                    lines.Add($"  • requestId={Markup.Escape(request.RequestId)}; factionId={Markup.Escape(request.ProposedFactionId)}; hallId={Markup.Escape(request.ProposedHallId)}; factionName={Markup.Escape(request.Charter.FactionName)}; hallName={Markup.Escape(request.ProposedHallName)}; supporters={request.SupportingResidentIds.Count}; quotedCost={request.QuotedCostFeathers} Чернильных Перьев/{request.QuotedCostLightSparks} Искр Света; reservedBefore={request.ReservedInkFeathersBefore}/{request.ReservedLightSparksBefore}; createdAtTurn={request.CreatedAtTurn}; createdAtUtc={Markup.Escape(request.CreatedAtUtc)}");
            }

            if (realignmentRequests.Count > 0)
            {
                lines.Add("");
                lines.Add("[bold]Ожидают решения:[/] переходы между фракциями [dim](все pending-запросы)[/]");
                foreach (var request in realignmentRequests)
                    lines.Add($"  • requestId={Markup.Escape(request.RequestId)}; residentId={Markup.Escape(request.ResidentId)}; residentName={Markup.Escape(request.ResidentName)}; sourceFactionId={Markup.Escape(request.SourceFactionId)}; targetFactionId={Markup.Escape(string.IsNullOrWhiteSpace(request.TargetFactionId) ? "neutral" : request.TargetFactionId)}; mode={Markup.Escape(DescribeShiningRealignmentMode(request.RealignmentMode))}; loyalty={request.FactionLoyaltyLevel}/{Markup.Escape(request.FactionLoyaltyTier)}; restlessness={request.FactionRestlessness}; createdAtTurn={request.CreatedAtTurn}; createdAtUtc={Markup.Escape(request.CreatedAtUtc)}");
            }

            if (leadershipRequests.Count > 0)
            {
                lines.Add("");
                lines.Add("[bold]Ожидают решения:[/] смена власти [dim](все pending-запросы)[/]");
                foreach (var request in leadershipRequests)
                    lines.Add($"  • requestId={Markup.Escape(request.RequestId)}; factionId={Markup.Escape(request.FactionId)}; factionName={Markup.Escape(request.FactionName)}; mode={Markup.Escape(DescribeShiningLeadershipMode(request.TransitionMode))}; incumbent={Markup.Escape(request.IncumbentHeadActorType)}:{Markup.Escape(request.IncumbentHeadActorId)}; candidate={Markup.Escape(request.CandidateHeadActorType)}:{Markup.Escape(request.CandidateHeadActorId)}; supportingResidentIds=[{Markup.Escape(string.Join(", ", request.SupportingResidentIds))}]; createdAtTurn={request.CreatedAtTurn}; createdAtUtc={Markup.Escape(request.CreatedAtUtc)}");
            }

            if (context?.Root["factionFoundingReceipts"] is JsonArray foundingReceipts && foundingReceipts.Count > 0)
            {
                lines.Add("");
                lines.Add("[bold]Все решения:[/] основания фракций [dim](без сокращения)[/]");
                foreach (var receipt in foundingReceipts.OfType<JsonObject>()
                             .OrderByDescending(item => GetNodeInt(item["resolvedAtTurn"])))
                    lines.Add($"  • {Markup.Escape(BuildShiningFoundingReceiptSummary(receipt))}");
            }

            if (context?.Root["factionRealignmentReceipts"] is JsonArray realignmentReceipts && realignmentReceipts.Count > 0)
            {
                lines.Add("");
                lines.Add("[bold]Все решения:[/] переходы между фракциями [dim](без сокращения)[/]");
                foreach (var receipt in realignmentReceipts.OfType<JsonObject>()
                             .OrderByDescending(item => GetNodeInt(item["resolvedAtTurn"])))
                    lines.Add($"  • {Markup.Escape(BuildShiningRealignmentReceiptSummary(receipt))}");
            }

            if (context?.Root["factions"] is JsonArray resolvedFactions)
            {
                var latestLeadershipReceipts = resolvedFactions.OfType<JsonObject>()
                    .SelectMany(faction =>
                    {
                        var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? GetNodeString(faction["factionId"]) ?? "?";
                        return (faction["leadershipReceipts"] as JsonArray)?.OfType<JsonObject>()
                            .Select(receipt => (FactionName: factionName, Receipt: receipt))
                            ?? Enumerable.Empty<(string FactionName, JsonObject Receipt)>();
                    })
                    .OrderByDescending(item => GetNodeInt(item.Receipt["resolvedAtTurn"]))
                    .ToList();
                if (latestLeadershipReceipts.Count > 0)
                {
                    lines.Add("");
                    lines.Add("[bold]Все решения:[/] смена власти [dim](без сокращения)[/]");
                    foreach (var item in latestLeadershipReceipts)
                        lines.Add($"  • {Markup.Escape(BuildShiningLeadershipReceiptSummary(item.FactionName, item.Receipt))}");
                }
            }

            Clear();
            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" 🏛 Политика Сияющей Обители ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Orange1),
                Padding = new Padding(2, 1),
                Expand = true
            });

            var choices = new List<string>();
            if (_stateManager.CurrentState.IsInShiningAbode)
            {
                choices.Add("🏗 Создать запрос на основание фракции");
                choices.Add("⇄ Создать запрос на переход между фракциями");
                choices.Add("👑 Создать запрос на смену власти");
            }

            if (context?.Root["factions"] is JsonArray inspectableFactions && inspectableFactions.Count > 0)
                choices.Add("👥 Осмотреть политическое состояние фракции");
            if (foundingRequests.Count > 0 || realignmentRequests.Count > 0 || leadershipRequests.Count > 0)
                choices.Add("📝 Осмотреть ожидающие политические запросы");
            if ((context?.Root["factionFoundingReceipts"] as JsonArray)?.Count > 0 ||
                (context?.Root["factionRealignmentReceipts"] as JsonArray)?.Count > 0 ||
                (context?.Root["factions"] as JsonArray)?.OfType<JsonObject>().Any(faction => (faction["leadershipReceipts"] as JsonArray)?.Count > 0) == true)
            {
                choices.Add("📜 Осмотреть решения фракций");
            }

            choices.Add("← Назад");
            var choice = Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]Политика Сияющей Обители[/]")
                .HighlightStyle(new Style(Color.Orange1))
                .AddChoices(choices));

            if (choice.Contains("Назад", StringComparison.Ordinal))
                return;

            if (context == null)
            {
                WaitForKey();
                continue;
            }

            if (choice.Contains("политическое состояние", StringComparison.OrdinalIgnoreCase))
            {
                ShowShiningFactionPoliticalInspectionPanel(context);
                WaitForKey();
                continue;
            }

            if (choice.Contains("ожидающие политические запросы", StringComparison.OrdinalIgnoreCase))
            {
                ShowShiningPendingPoliticalInspectionPanel(context, foundingRequests, realignmentRequests, leadershipRequests);
                WaitForKey();
                continue;
            }

            if (choice.Contains("решения фракций", StringComparison.OrdinalIgnoreCase))
            {
                ShowShiningPoliticalResolutionInspectionPanel(context);
                WaitForKey();
                continue;
            }

            if (choice.Contains("основание", StringComparison.OrdinalIgnoreCase))
                await HandleShiningFoundingRequestAsync(context);
            else if (choice.Contains("переход", StringComparison.OrdinalIgnoreCase))
                await HandleShiningRealignmentRequestAsync(context);
            else if (choice.Contains("власти", StringComparison.OrdinalIgnoreCase))
                await HandleShiningLeadershipTransitionRequestAsync(context);
        }
    }

    private static string? GetNodeString(JsonNode? node)
    {
        if (node == null)
            return null;

        try
        {
            return node.GetValue<string>();
        }
        catch
        {
            return node.ToJsonString().Trim('"');
        }
    }

    private static int GetNodeInt(JsonNode? node)
    {
        if (node == null)
            return 0;

        try
        {
            return node.GetValue<int>();
        }
        catch
        {
            return 0;
        }
    }

    private static bool GetNodeBool(JsonNode? node)
    {
        if (node == null)
            return false;

        try
        {
            return node.GetValue<bool>();
        }
        catch
        {
            return false;
        }
    }

    private Panel? BuildShiningOverviewSignalsPanel(
        JsonObject shiningRoot,
        JsonObject? residentRoot,
        int coreRequestCount,
        int tradeRequestCount,
        int foundingRequestCount,
        int realignmentRequestCount,
        int leadershipRequestCount,
        string? firstCoreRequestLabel = null,
        string? firstTradeRequestLabel = null,
        string? firstPoliticalRequestLabel = null)
    {
        var lines = new List<string>();

        if (coreRequestCount > 0 || tradeRequestCount > 0 || foundingRequestCount > 0 || realignmentRequestCount > 0 || leadershipRequestCount > 0)
        {
            lines.Add("[bold]Очереди:[/]");
            if (coreRequestCount > 0)
                lines.Add($"  • Основные действия Обители: {coreRequestCount}");
            if (tradeRequestCount > 0)
                lines.Add($"  • Торговые витрины: {tradeRequestCount}");
            if (foundingRequestCount > 0 || realignmentRequestCount > 0 || leadershipRequestCount > 0)
                lines.Add($"  • Политика: основание {foundingRequestCount}, переходы {realignmentRequestCount}, власть {leadershipRequestCount}");
        }

        var blockers = new List<string>();
        if (!string.Equals(GetNodeString(shiningRoot["availability"]), "active", StringComparison.OrdinalIgnoreCase))
            blockers.Add($"Обитель сейчас неактивна: {DescribeShiningAvailability(GetNodeString(shiningRoot["availability"]))}.");

        if (shiningRoot["preparedIncarnationPackage"] is JsonObject package)
        {
            blockers.Add($"Пакет новой жизни уже подготовлен: обычные действия остановлены, пока не начнётся следующее воплощение [{GetPreparedPackageSelectedCardIds(package).Count} карт].");
        }

        if (shiningRoot["gates"] is JsonObject gates && GetNodeBool(gates["isStale"]))
            blockers.Add($"Черновик Врат устарел: открой Врата заново [версия {GetNodeInt(gates["draftVersion"])}].");

        if (shiningRoot["factions"] is JsonArray factions && factions.Count > 0)
        {
            var dormantTradeCount = factions.OfType<JsonObject>()
                .Count(faction => ShiningAbodeState.GetTradeTier(GetNodeInt(faction["factionStrength"])) < 1);
            if (dormantTradeCount > 0)
                blockers.Add($"Торговля спит у {dormantTradeCount} из {factions.Count} фракций.");

            var blockedForgeCount = factions.OfType<JsonObject>()
                .Count(faction => !ShiningAbodeState.FactionHasSupportedProjectArchetype(faction, ShiningAbodeState.ProjectArchetypeRefinement));
            if (blockedForgeCount > 0)
                blockers.Add($"Кузня пока не раскрыта у {blockedForgeCount} из {factions.Count} фракций: нет завершённого проекта очищения.");
        }

        var nextStep = DetermineNextShiningStep(
            shiningRoot,
            coreRequestCount,
            tradeRequestCount,
            foundingRequestCount,
            realignmentRequestCount,
            leadershipRequestCount,
            firstCoreRequestLabel,
            firstTradeRequestLabel,
            firstPoliticalRequestLabel);

        lines.InsertRange(0, new[]
        {
            $"[bold]Сейчас:[/] [white]{Markup.Escape(_stateManager.CurrentState.CurrentRealm)}[/] / {Markup.Escape(DescribeShiningAvailability(GetNodeString(shiningRoot["availability"])))}",
            $"[bold green]Следующий шаг:[/] {Markup.Escape(nextStep)}",
            string.Empty
        });

        if (blockers.Count > 0)
        {
            if (lines.Count > 0)
                lines.Add("");
            lines.Add("[bold]Блокировки и сигналы:[/]");
            foreach (var blocker in blockers)
                lines.Add($"  • {Markup.Escape(blocker)}");
        }

        if (lines.Count == 0)
            return null;

        return new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" ⏳ Что важно сейчас ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Gold3),
            Padding = new Padding(1, 0),
            Expand = true
        };
    }

    private static string BuildShiningCoreReceiptSummary(JsonObject receipt, JsonObject? shiningRoot = null)
    {
        _ = shiningRoot;
        var actionType = GetNodeString(receipt["actionType"]) ?? "action";
        var status = DescribeShiningResolutionStatus(GetNodeString(receipt["status"]));
        var actionLabel = DescribeShiningCoreActionLabel(actionType);
        var factionId = GetNodeString(receipt["factionId"]) ?? GetNodeString(receipt["resolvedFactionId"]) ?? "?";
        var factionName = GetNodeString(receipt["factionName"]) ?? factionId;
        var projectId = GetNodeString(receipt["projectId"]) ?? "?";
        var projectName = GetNodeString(receipt["projectName"]) ?? projectId;
        var hallId = GetNodeString(receipt["hallId"]) ?? "?";
        var hallName = GetNodeString(receipt["hallName"]) ?? hallId;
        return actionType switch
        {
            ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction =>
                $"{actionLabel} — {status}. Зал «{hallName}», фракция «{factionName}», резидентов {(receipt["newResidentIds"] as JsonArray)?.Count ?? 0}, стартовых проектов {(receipt["seededProjectIds"] as JsonArray)?.Count ?? 0}.",
            ShiningCoreActionRequestState.ActionTypeInvestInFaction =>
                $"{actionLabel} — {status}. Усилена фракция «{factionName}».",
            ShiningCoreActionRequestState.ActionTypeCompleteProject =>
                $"{actionLabel} — {status}. Проект «{projectName}» фракции «{factionName}».",
            ShiningCoreActionRequestState.ActionTypeSupportProject =>
                $"{actionLabel} — {status}. Проект «{projectName}» фракции «{factionName}».",
            ShiningCoreActionRequestState.ActionTypeUnsupportProject =>
                $"{actionLabel} — {status}. Проект «{projectName}» фракции «{factionName}».",
            ShiningCoreActionRequestState.ActionTypeRetireProject =>
                $"{actionLabel} — {status}. Проект «{projectName}» фракции «{factionName}».",
            ShiningCoreActionRequestState.ActionTypeOpenGates =>
                $"{actionLabel} — {status}. Новый набор благословений готов [версия {GetNodeInt(receipt["generatedDraftVersion"])}].",
            ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage =>
                $"{actionLabel} — {status}. Для следующей жизни зафиксировано {((receipt["selectedCardIds"] as JsonArray)?.Count ?? 0)} карт(ы).",
            ShiningCoreActionRequestState.ActionTypePullRelicGacha =>
                $"{actionLabel} — {status}. Баннер «{factionName}», редкость {DescribeForgeRarity(GetNodeString(receipt["baseRarity"]))} -> {DescribeForgeRarity(GetNodeString(receipt["finalRarity"]))}, реликвия «{GetNodeString(receipt["relicName"]) ?? GetNodeString(receipt["relicId"]) ?? "реликвия"}».",
            ShiningCoreActionRequestState.ActionTypeForgeRelicReshape or
            ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty or
            ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand or
            ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho or
            ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity =>
                $"{actionLabel} — {status}. {(GetNodeString(receipt["relicName"]) ?? GetNodeString(receipt["relicId"]) ?? "реликвия")}{BuildForgeReceiptSuffix(receipt)}.",
            _ => $"{actionLabel} — {status}."
        };
    }

    private static string BuildShiningTradeReceiptSummary(string factionName, JsonObject faction, JsonObject receipt)
    {
        var tradeCycleId = GetNodeString(receipt["tradeCycleId"]) ?? "?";
        var itemCount = GetNodeInt(receipt["itemCount"]);
        var stableFactionName = GetNodeString(receipt["factionName"]) ?? factionName;
        var soldOutCount = TryReadIntegerNode(receipt["soldOutCount"], out var parsedSoldOutCount) ? parsedSoldOutCount : 0;
        return soldOutCount > 0
            ? $"Торговая витрина «{stableFactionName}» готова. Цикл витрины {tradeCycleId}, слотов {itemCount}, распродано {soldOutCount}/{Math.Max(itemCount, soldOutCount)}."
            : $"Торговая витрина «{stableFactionName}» готова. Цикл витрины {tradeCycleId}, слотов {itemCount}.";
    }

    private static string BuildShiningFoundingReceiptSummary(JsonObject receipt)
    {
        var requestId = GetNodeString(receipt["requestId"]) ?? "?";
        var hallId = GetNodeString(receipt["hallId"]) ?? GetNodeString(receipt["proposedHallId"]) ?? "?";
        var factionId = GetNodeString(receipt["factionId"]) ?? GetNodeString(receipt["proposedFactionId"]) ?? "?";
        var hallName = GetNodeString(receipt["hallName"]) ?? GetNodeString(receipt["hallId"]) ?? "?";
        var factionName = GetNodeString(receipt["factionName"]) ?? GetNodeString(receipt["factionId"]) ?? GetNodeString(receipt["proposedFactionId"]) ?? "?";
        var status = DescribeShiningResolutionStatus(GetNodeString(receipt["status"]));
        var supporterCount = (receipt["supportingResidentIds"] as JsonArray)?.Count ?? 0;
        return $"Основание фракции — {status}. requestId={requestId}, hallId={hallId}, factionId={factionId}; зал «{hallName}», фракция «{factionName}», сторонников {supporterCount}.";
    }

    private static string BuildShiningRealignmentReceiptSummary(JsonObject receipt)
    {
        var requestId = GetNodeString(receipt["requestId"]) ?? "?";
        var residentId = GetNodeString(receipt["residentId"]) ?? "?";
        var sourceFactionId = GetNodeString(receipt["sourceFactionId"]) ?? "none";
        var targetFactionId = GetNodeString(receipt["targetFactionId"]);
        var residentName = GetNodeString(receipt["residentName"]) ?? GetNodeString(receipt["residentId"]) ?? "?";
        var sourceFaction = string.IsNullOrWhiteSpace(GetNodeString(receipt["sourceFactionName"]))
            ? GetNodeString(receipt["sourceFactionId"]) ?? "?"
            : GetNodeString(receipt["sourceFactionName"])!;
        var targetFaction = string.IsNullOrWhiteSpace(GetNodeString(receipt["targetFactionName"]))
            ? (string.IsNullOrWhiteSpace(GetNodeString(receipt["targetFactionId"]))
                ? "нейтраль"
                : GetNodeString(receipt["targetFactionId"]) ?? "нейтраль")
            : GetNodeString(receipt["targetFactionName"])!;
        var mode = DescribeShiningRealignmentMode(GetNodeString(receipt["realignmentMode"]));
        var status = DescribeShiningResolutionStatus(GetNodeString(receipt["status"]));
        return $"Перестройка резидента — {status}. requestId={requestId}, residentId={residentId}; {residentName}: {sourceFaction} ({sourceFactionId}) -> {targetFaction} ({(string.IsNullOrWhiteSpace(targetFactionId) ? "neutral" : targetFactionId)}), режим {mode}.";
    }

    private static string BuildShiningLeadershipReceiptSummary(string factionName, JsonObject receipt)
    {
        var requestId = GetNodeString(receipt["requestId"]) ?? "?";
        var factionId = GetNodeString(receipt["factionId"]) ?? "?";
        var stableFactionName = GetNodeString(receipt["factionName"]) ?? GetNodeString(receipt["factionId"]) ?? factionName;
        var transitionMode = DescribeShiningLeadershipMode(GetNodeString(receipt["transitionMode"]));
        var status = DescribeShiningResolutionStatus(GetNodeString(receipt["status"]));
        var previousHeadActorType = GetNodeString(receipt["previousHeadActorType"]) ?? "?";
        var previousHeadActorId = GetNodeString(receipt["previousHeadActorId"]) ?? "?";
        var newHeadActorType = GetNodeString(receipt["newHeadActorType"]) ?? "?";
        var newHeadActorId = GetNodeString(receipt["newHeadActorId"]) ?? "?";
        var newHead = string.IsNullOrWhiteSpace(GetNodeString(receipt["newHeadLabel"]))
            ? BuildHeadActorLabel(GetNodeString(receipt["newHeadActorType"]), GetNodeString(receipt["newHeadActorId"]))
            : GetNodeString(receipt["newHeadLabel"])!;
        return $"Смена главы — {status}. requestId={requestId}, factionId={factionId}; {stableFactionName}, {transitionMode}, {previousHeadActorType}:{previousHeadActorId} -> {newHeadActorType}:{newHeadActorId}, новый глава: {newHead}.";
    }

    private static string BuildForgeReceiptSuffix(JsonObject receipt)
    {
        var targetFormTag = GetNodeString(receipt["targetFormTag"]);
        if (!string.IsNullOrWhiteSpace(targetFormTag))
            return $" => {DescribeForgeFormTag(targetFormTag)}";

        return TryReadIntegerNode(receipt["propertyIndex"], out var propertyIndex) && propertyIndex >= 0
            ? $" [свойство {propertyIndex + 1}]"
            : string.Empty;
    }

    private static void AppendResidentHistoryReference(List<string> lines, JsonObject? residentRoot, JsonObject receipt, int indent)
    {
        var historyEntryId = GetNodeString(receipt["residentHistoryEntryId"]);
        if (string.IsNullOrWhiteSpace(historyEntryId))
            return;

        var indentPrefix = new string(' ', indent);
        var liveHistoryEntry = FindResidentHistoryEntry(residentRoot, historyEntryId);
        if (liveHistoryEntry != null)
        {
            var title = GetNodeString(liveHistoryEntry["title"]);
            var summary = GetNodeString(liveHistoryEntry["summary"]);
            var eventType = GetNodeString(liveHistoryEntry["eventType"]);
            var revealedAtTurn = GetNodeInt(liveHistoryEntry["revealedAtTurn"]);
            if (revealedAtTurn <= 0)
                revealedAtTurn = GetNodeInt(liveHistoryEntry["turn"]);
            var revealedAtUtc = GetNodeString(liveHistoryEntry["revealedAtUtc"]) ?? GetNodeString(liveHistoryEntry["timestamp"]);
            lines.Add($"{indentPrefix}Историческая запись резидента: [white]{Markup.Escape(string.IsNullOrWhiteSpace(title) ? historyEntryId : title!)}[/]");
            if (!string.IsNullOrWhiteSpace(summary))
                lines.Add($"{indentPrefix}Содержание истории: [dim]{Markup.Escape(summary!)}[/]");
            if (!string.IsNullOrWhiteSpace(eventType))
                lines.Add($"{indentPrefix}Тип исторической записи: [dim]{Markup.Escape(HumanizeProtocolToken(eventType))}[/]");
            if (revealedAtTurn > 0)
                lines.Add($"{indentPrefix}Открыта на ходу: [dim]{revealedAtTurn}[/]");
            if (!string.IsNullOrWhiteSpace(revealedAtUtc))
                lines.Add($"{indentPrefix}Открыта в UTC: [dim]{Markup.Escape(revealedAtUtc!)}[/]");
            var tags = (liveHistoryEntry["tags"] as JsonArray)?
                .Select(tag => HumanizeProtocolToken(GetNodeString(tag)))
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .ToList() ?? new List<string>();
            if (tags.Count > 0)
                lines.Add($"{indentPrefix}Метки истории: [dim]{Markup.Escape(string.Join(", ", tags))}[/]");
            lines.Add($"{indentPrefix}Идентификатор исторической записи: [dim]{Markup.Escape(historyEntryId)}[/]");
            return;
        }

        var stableSummary = GetNodeString(receipt["residentHistorySummary"]);
        var stableTimestamp = GetNodeString(receipt["residentHistoryTimestamp"]);
        var stableEventType = GetNodeString(receipt["residentHistoryEventType"]);
        if (!string.IsNullOrWhiteSpace(stableSummary) ||
            !string.IsNullOrWhiteSpace(stableTimestamp) ||
            !string.IsNullOrWhiteSpace(stableEventType))
        {
            if (!string.IsNullOrWhiteSpace(stableSummary))
                lines.Add($"{indentPrefix}Историческая запись резидента: [dim]{Markup.Escape(stableSummary)}[/]");
            if (!string.IsNullOrWhiteSpace(stableEventType))
                lines.Add($"{indentPrefix}Тип исторической записи: [dim]{Markup.Escape(HumanizeProtocolToken(stableEventType))}[/]");
            if (!string.IsNullOrWhiteSpace(stableTimestamp))
                lines.Add($"{indentPrefix}Историческая запись UTC: [dim]{Markup.Escape(stableTimestamp)}[/]");
            lines.Add($"{indentPrefix}Идентификатор исторической записи: [dim]{Markup.Escape(historyEntryId)}[/]");
            return;
        }

        lines.Add($"{indentPrefix}Историческая запись резидента: [dim]в receipt нет замороженного фрагмента; доступен только идентификатор {Markup.Escape(historyEntryId)}[/]");
    }

    private static JsonObject? FindResidentHistoryEntry(JsonObject? residentRoot, string? historyEntryId)
    {
        if (residentRoot == null || string.IsNullOrWhiteSpace(historyEntryId))
            return null;

        if (residentRoot["historyLog"] is JsonArray historyLog)
        {
            var rootEntry = historyLog.OfType<JsonObject>()
                .FirstOrDefault(entry => string.Equals(GetNodeString(entry["entryId"]), historyEntryId, StringComparison.OrdinalIgnoreCase));
            if (rootEntry != null)
                return rootEntry;
        }

        return (residentRoot["entries"] as JsonArray)?.OfType<JsonObject>()
            .SelectMany(entry => (entry["historyLog"] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            .FirstOrDefault(entry => string.Equals(GetNodeString(entry["entryId"]), historyEntryId, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveShiningFactionLabel(JsonObject? shiningRoot, string? factionId)
    {
        if (string.IsNullOrWhiteSpace(factionId))
            return "?";

        if (shiningRoot?["factions"] is JsonArray factions)
        {
            var faction = factions.OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(GetNodeString(item["factionId"]), factionId, StringComparison.OrdinalIgnoreCase));
            if (faction != null)
                return GetNodeString(faction["charter"]?["factionName"]) ?? factionId;
        }

        return factionId;
    }

    private static string ResolveShiningHallLabel(JsonObject? shiningRoot, string? hallId)
    {
        if (string.IsNullOrWhiteSpace(hallId))
            return "?";

        if (shiningRoot?["halls"] is JsonArray halls)
        {
            var hall = halls.OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(GetNodeString(item["hallId"]), hallId, StringComparison.OrdinalIgnoreCase));
            if (hall != null)
                return GetNodeString(hall["hallName"]) ?? hallId;
        }

        return hallId;
    }

    private static string ResolveShiningProjectLabel(JsonObject? shiningRoot, string? factionId, string? projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            return "project";

        IEnumerable<JsonObject> factions = Enumerable.Empty<JsonObject>();
        if (shiningRoot?["factions"] is JsonArray factionArray)
            factions = factionArray.OfType<JsonObject>();

        if (!string.IsNullOrWhiteSpace(factionId))
            factions = factions.Where(item => string.Equals(GetNodeString(item["factionId"]), factionId, StringComparison.OrdinalIgnoreCase))
                .Concat((shiningRoot?["factions"] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>());

        foreach (var faction in factions)
        {
            var project = (faction["projects"] as JsonArray)?.OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(GetNodeString(item["projectId"]), projectId, StringComparison.OrdinalIgnoreCase));
            if (project == null)
                continue;

            var projectName = GetNodeString(project["displayName"]) ?? projectId;
            var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? GetNodeString(faction["factionId"]);
            return string.IsNullOrWhiteSpace(factionName) ? projectName : $"«{projectName}» ({factionName})";
        }

        return projectId;
    }

    private void ShowShiningStructureInspectionPanel(ShiningContext context)
    {
        var lines = new List<string>
        {
            "[bold yellow]🗺 Залы и светозарные акторы[/]"
        };

        if (context.Root["halls"] is JsonArray halls && halls.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Залы Обители:[/]");
            foreach (var hall in halls.OfType<JsonObject>())
            {
                var hallId = GetNodeString(hall["hallId"]) ?? string.Empty;
                var hallName = GetNodeString(hall["hallName"]) ?? hallId;
                var description = GetNodeString(hall["description"]) ?? string.Empty;
                var tags = (hall["serviceTags"] as JsonArray)?.OfType<JsonValue>()
                    .Where(node => node.TryGetValue<string>(out _))
                    .Select(node => DescribeShiningHallServiceTag(node.GetValue<string>()))
                    .ToList() ?? new List<string>();
                var boundFactions = (context.Root["factions"] as JsonArray)?.OfType<JsonObject>()
                    .Where(faction => string.Equals(GetNodeString(faction["hallId"]), hallId, StringComparison.OrdinalIgnoreCase))
                    .Select(faction => GetNodeString(faction["charter"]?["factionName"]) ?? GetNodeString(faction["factionId"]) ?? "?")
                    .ToList() ?? new List<string>();

                lines.Add($"  • {Markup.Escape(hallName)}");
                if (!string.IsNullOrWhiteSpace(description))
                    lines.Add($"    Описание: {Markup.Escape(description)}");
                if (tags.Count > 0)
                    lines.Add($"    Службы зала: {Markup.Escape(string.Join(", ", tags))}");
                if (boundFactions.Count > 0)
                    lines.Add($"    Связанные фракции: {Markup.Escape(string.Join(", ", boundFactions))}");
                lines.Add($"    Идентификатор зала: [dim]{Markup.Escape(hallId)}[/]");
            }
        }
        else
        {
            lines.Add("");
            lines.Add("[bold]Залы Обители:[/] пока ещё не проявлены.");
        }

        if (context.Root["shiningPoliticalActors"] is JsonArray actors && actors.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Светозарные акторы:[/]");
            foreach (var actor in actors.OfType<JsonObject>())
            {
                var actorId = GetNodeString(actor["actorId"]) ?? string.Empty;
                var displayName = GetNodeString(actor["displayName"]) ?? actorId;
                var summary = GetNodeString(actor["summary"]) ?? string.Empty;
                var originFaction = ResolveShiningFactionLabel(context.Root, GetNodeString(actor["originFactionId"]));
                var currentFaction = ResolveShiningFactionLabel(context.Root, GetNodeString(actor["currentFactionId"]));
                lines.Add($"  • {Markup.Escape(displayName)}");
                if (!string.IsNullOrWhiteSpace(summary))
                    lines.Add($"    Сводка: {Markup.Escape(summary)}");
                lines.Add($"    Политический статус: {Markup.Escape(DescribeShiningPoliticalStatus(GetNodeString(actor["politicalStatus"])))}");
                if (!string.IsNullOrWhiteSpace(originFaction) && originFaction != "?")
                    lines.Add($"    Фракция происхождения: {Markup.Escape(originFaction)}");
                if (!string.IsNullOrWhiteSpace(currentFaction) && currentFaction != "?")
                    lines.Add($"    Текущая фракция: {Markup.Escape(currentFaction)}");
                lines.Add($"    Идентификатор актора: [dim]{Markup.Escape(actorId)}[/]");
            }
        }
        else
        {
            lines.Add("");
            lines.Add("[bold]Светозарные акторы:[/] пока ещё не проявлены.");
        }

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🗺 Структура Сияющей Обители ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        if (context.Root["halls"] is JsonArray hallsAudit)
            WriteJsonAuditPanel("Полный JSON halls[]", hallsAudit, Color.Gold1);
        if (context.Root["shiningPoliticalActors"] is JsonArray actorsAudit)
            WriteJsonAuditPanel("Полный JSON shiningPoliticalActors[]", actorsAudit, Color.Orange1);
    }

    private void ShowShiningFactionPoliticalInspectionPanel(ShiningContext context)
    {
        var faction = PromptForFaction(context.Root, "Выберите фракцию для политического осмотра");
        if (faction == null)
            return;

        var factionId = GetNodeString(faction["factionId"]) ?? string.Empty;
        var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? factionId;
        var hallName = ResolveShiningHallLabel(context.Root, GetNodeString(faction["hallId"]));
        var leadershipState = DescribeShiningLeadershipState(GetNodeString(faction["leadership"]?["leadershipState"]));
        var headLabel = BuildHeadActorLabel(
            GetNodeString(faction["leadership"]?["headActorType"]),
            GetNodeString(faction["leadership"]?["headActorId"]),
            context.ResidentRoot,
            context.GuardiansRoot,
            context.Root);
        var factionStrength = GetNodeInt(faction["factionStrength"]);
        var baseStrength = GetNodeInt(faction["baseStrength"]);
        var investCountThisAscension = GetNodeInt(faction["investCountThisAscension"]);
        var countedArchetypes = (faction["projectArchetypesCountedThisAscension"] as JsonArray)?
            .Select(item => DescribeShiningProjectArchetype(GetNodeString(item)))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList() ?? new List<string>();
        var originType = DescribeShiningOriginType(GetNodeString(faction["originType"]));
        var charterSummary = GetNodeString(faction["charter"]?["summary"]);
        var favoredArchetype = DescribeShiningProjectArchetype(GetNodeString(faction["charter"]?["favoredArchetype"]));
        var patronEffectFamily = DescribeShiningEffectFamily(GetNodeString(faction["charter"]?["patronEffectFamily"]));

        var lines = new List<string>
        {
            $"[bold yellow]👥 Политическое состояние фракции «{Markup.Escape(factionName)}»[/]",
            "",
            "[bold]Сводка фракции:[/]",
            $"  • Зал: {Markup.Escape(hallName)}",
            $"  • Идентификатор фракции: [dim]{Markup.Escape(factionId)}[/]",
            $"  • Идентификатор зала: [dim]{Markup.Escape(GetNodeString(faction["hallId"]) ?? string.Empty)}[/]",
            $"  • Сила: [white]{factionStrength}[/]",
            $"  • Базовая сила: [white]{baseStrength}[/]",
            $"  • Происхождение: {Markup.Escape(originType)}",
            $"  • Любимый архетип проектов: {Markup.Escape(favoredArchetype)}",
            "  • Точный эффект любимого архетипа: снижает только цену завершения matching проекта на 5 Перьев и 5 Искр Света; награда силы остаётся строго по tier: 8/12/16.",
            $"  • Покровительствующая семья эффекта: {Markup.Escape(patronEffectFamily)}",
            $"  • Инвестиций за это Вознесение: [white]{investCountThisAscension}[/]",
            $"  • Архетипы проектов, уже учтённые за это Вознесение: {Markup.Escape(countedArchetypes.Count == 0 ? "нет" : string.Join(", ", countedArchetypes))}",
            $"  • Состояние власти: {Markup.Escape(leadershipState)}",
            $"  • Глава: {Markup.Escape(headLabel)}"
        };
        if (!string.IsNullOrWhiteSpace(charterSummary))
            lines.Add($"  • Устав: [dim]{Markup.Escape(charterSummary!)}[/]");

        var residents = (context.ResidentRoot?["entries"] as JsonArray)?.OfType<JsonObject>()
            .Where(entry =>
                string.Equals(GetNodeString(entry["ascensionState"]), ShiningAbodeState.AscensionStateAscended, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetNodeString(entry["shiningFactionId"]), factionId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => GetNodeInt(entry["factionLoyaltyLevel"]))
            .ThenBy(entry => GetNodeInt(entry["factionRestlessness"]))
            .ToList() ?? new List<JsonObject>();

        lines.Add("");
        lines.Add("[bold]Резиденты фракции:[/]");
        if (residents.Count == 0)
        {
            lines.Add("  • У этой фракции пока нет явно проявленных вознесённых резидентов.");
        }
        else
        {
            foreach (var resident in residents)
            {
                var residentId = GetNodeString(resident["residentId"]) ?? string.Empty;
                var residentName = GetNodeString(resident["displayName"]) ?? GetNodeString(resident["residentName"]) ?? residentId;
                var residentRole = DescribeShiningResidentRole(GetNodeString(resident["residentRole"]));
                var loyaltyLevel = GetNodeInt(resident["factionLoyaltyLevel"]);
                var loyaltyTier = DescribeShiningFactionLoyaltyTier(GetNodeString(resident["factionLoyaltyTier"]));
                var restlessness = GetNodeInt(resident["factionRestlessness"]);
                var realignmentState = DescribeShiningFactionRealignmentState(GetNodeString(resident["factionRealignmentState"]));
                lines.Add($"  • {Markup.Escape(residentName)}");
                lines.Add($"    Роль во фракции: {Markup.Escape(residentRole)}");
                lines.Add($"    Лояльность к фракции: [white]{loyaltyLevel}[/]/100");
                lines.Add($"    Тир лояльности: {Markup.Escape(loyaltyTier)}");
                lines.Add($"    Внутреннее брожение: [white]{restlessness}[/]/100");
                lines.Add($"    Состояние перестройки: {Markup.Escape(realignmentState)}");
                lines.Add($"    Идентификатор резидента: [dim]{Markup.Escape(residentId)}[/]");
            }
        }

        var projects = (faction["projects"] as JsonArray)?.OfType<JsonObject>()
            .OrderByDescending(project => GetNodeInt(project["tier"]))
            .ThenByDescending(project => string.Equals(GetNodeString(project["status"]), ShiningAbodeState.ProjectStatusCompleted, StringComparison.OrdinalIgnoreCase))
            .ThenBy(project => GetNodeString(project["displayName"]), StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<JsonObject>();

        lines.Add("");
        lines.Add("[bold]Проекты фракции:[/]");
        if (projects.Count == 0)
        {
            lines.Add("  • У этой фракции пока нет явно проявленных проектов.");
        }
        else
        {
            foreach (var project in projects)
            {
                var projectName = GetNodeString(project["displayName"]) ?? GetNodeString(project["projectId"]) ?? "проект";
                var projectId = GetNodeString(project["projectId"]) ?? string.Empty;
                var toneTags = (project["toneTags"] as JsonArray)?.Select(tag => GetNodeString(tag)).Where(tag => !string.IsNullOrWhiteSpace(tag)).Cast<string>().ToList() ?? new List<string>();
                var targetFactionIds = (project["targetFactionIds"] as JsonArray)?.Select(tag => ResolveShiningFactionLabel(context.Root, GetNodeString(tag))).Where(tag => !string.IsNullOrWhiteSpace(tag)).Cast<string>().ToList() ?? new List<string>();
                lines.Add($"  • {Markup.Escape(projectName)}");
                lines.Add($"    Статус: {Markup.Escape(DescribeShiningProjectStatus(GetNodeString(project["status"])))}");
                lines.Add($"    Поддержка: {(GetNodeBool(project["isSupported"]) ? "[green]поддерживается[/]" : "[dim]не поддерживается[/]")}");
                lines.Add($"    Архетип: {Markup.Escape(DescribeShiningProjectArchetype(GetNodeString(project["projectArchetype"])))}");
                lines.Add($"    Семья эффекта: {Markup.Escape(DescribeShiningEffectFamily(GetNodeString(project["outputEffectFamily"])))}");
                lines.Add($"    Уровень: [white]{GetNodeInt(project["tier"])}[/]");
                lines.Add($"    Награда силы: [white]{GetNodeInt(project["strengthReward"])}[/]");
                if (!string.IsNullOrWhiteSpace(GetNodeString(project["summary"])))
                    lines.Add($"    Сводка: [dim]{Markup.Escape(GetNodeString(project["summary"])!)}[/]");
                if (toneTags.Count > 0)
                    lines.Add($"    Тональность: [dim]{Markup.Escape(string.Join(", ", toneTags))}[/]");
                if (targetFactionIds.Count > 0)
                    lines.Add($"    Цели: [dim]{Markup.Escape(string.Join(", ", targetFactionIds))}[/]");
                if (!string.IsNullOrWhiteSpace(projectId))
                    lines.Add($"    Идентификатор проекта: [dim]{Markup.Escape(projectId)}[/]");
            }
        }

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 👥 Политическое состояние фракции ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Orange1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        WriteJsonAuditPanel("Полный JSON фракции: leadership, projects, bindings", faction, Color.Orange1);
    }

    private void ShowShiningCoreReceiptInspectionPanel(ShiningContext context)
    {
        var receipts = ShiningAbodeState.EnsureCoreActionReceiptsArray(context.Root).OfType<JsonObject>()
            .OrderByDescending(item => GetNodeInt(item["resolvedAtTurn"]))
            .ThenByDescending(item => GetNodeString(item["resolvedAtUtc"]), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lines = new List<string>
        {
            "[bold yellow]📜 Полный осмотр исходов Обители[/]"
        };

        if (receipts.Count == 0)
        {
            lines.Add("");
            lines.Add("[dim]Подтверждённых исходов действий Обители пока нет.[/]");
        }
        else
        {
            foreach (var receipt in receipts)
            {
                var actionType = GetNodeString(receipt["actionType"]) ?? string.Empty;
                var resolvedFactionId = GetNodeString(receipt["resolvedFactionId"]) ?? GetNodeString(receipt["factionId"]) ?? string.Empty;
                var hallId = GetNodeString(receipt["hallId"]) ?? string.Empty;
                var projectId = GetNodeString(receipt["projectId"]) ?? string.Empty;
                var relicName = GetNodeString(receipt["relicName"]) ?? GetNodeString(receipt["relicId"]) ?? "реликвия";

                lines.Add("");
                lines.Add($"[bold]{Markup.Escape(BuildShiningCoreReceiptSummary(receipt, context.Root))}[/]");
                AppendShiningResolutionAuditLines(lines, receipt);
                AppendShiningReceiptConsequenceLines(lines, receipt);

                switch (actionType)
                {
                    case ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction:
                        lines.Add($"  Зал: [white]{Markup.Escape(GetNodeString(receipt["hallName"]) ?? hallId)}[/] [dim]({Markup.Escape(hallId)})[/]");
                        lines.Add($"  Фракция: [white]{Markup.Escape(GetNodeString(receipt["factionName"]) ?? resolvedFactionId)}[/] [dim]({Markup.Escape(resolvedFactionId)})[/]");
                        var discoveryCharterSummary = GetNodeString(receipt["charterSummary"]);
                        if (!string.IsNullOrWhiteSpace(discoveryCharterSummary))
                            lines.Add($"  Устав фракции: [dim]{Markup.Escape(discoveryCharterSummary!)}[/]");
                        var discoveryFavoredArchetype = GetNodeString(receipt["favoredArchetype"]);
                        if (!string.IsNullOrWhiteSpace(discoveryFavoredArchetype))
                            lines.Add($"  Любимый архетип: [dim]{Markup.Escape(DescribeShiningProjectArchetype(discoveryFavoredArchetype))}[/]");
                        var discoveryPatronEffectFamily = GetNodeString(receipt["patronEffectFamily"]);
                        if (!string.IsNullOrWhiteSpace(discoveryPatronEffectFamily))
                            lines.Add($"  Покровительствующий эффект: [dim]{Markup.Escape(DescribeShiningEffectFamily(discoveryPatronEffectFamily))}[/]");
                        AppendShiningStableNamedIdList(
                            lines,
                            "Новые резиденты",
                            receipt["newResidentIds"] as JsonArray,
                            receipt["newResidentNames"] as JsonArray);
                        AppendShiningStableNamedIdList(
                            lines,
                            "Стартовые проекты",
                            receipt["seededProjectIds"] as JsonArray,
                            receipt["seededProjectNames"] as JsonArray);
                        break;
                    case ShiningCoreActionRequestState.ActionTypeInvestInFaction:
                        lines.Add($"  Усиленная фракция: [white]{Markup.Escape(GetNodeString(receipt["factionName"]) ?? (GetNodeString(receipt["factionId"]) ?? "?"))}[/] [dim]({Markup.Escape(GetNodeString(receipt["factionId"]) ?? string.Empty)})[/]");
                        break;
                    case ShiningCoreActionRequestState.ActionTypeCompleteProject:
                    case ShiningCoreActionRequestState.ActionTypeSupportProject:
                    case ShiningCoreActionRequestState.ActionTypeUnsupportProject:
                    case ShiningCoreActionRequestState.ActionTypeRetireProject:
                        var receiptFactionId = GetNodeString(receipt["factionId"]) ?? string.Empty;
                        lines.Add($"  Фракция: [white]{Markup.Escape(GetNodeString(receipt["factionName"]) ?? receiptFactionId)}[/] [dim]({Markup.Escape(receiptFactionId)})[/]");
                        lines.Add($"  Проект: [white]{Markup.Escape(GetNodeString(receipt["projectName"]) ?? projectId)}[/] [dim]({Markup.Escape(projectId)})[/]");
                        break;
                    case ShiningCoreActionRequestState.ActionTypeOpenGates:
                        lines.Add($"  Версия нового набора: [white]{GetNodeInt(receipt["generatedDraftVersion"])}[/]");
                        break;
                    case ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage:
                        lines.Add($"  Версия исходного набора: [white]{GetNodeInt(receipt["generatedDraftVersion"])}[/]");
                        var receiptSelectedCards = GetConsistentPreparedPackageReceiptCards(receipt);
                        if (receiptSelectedCards.Count > 0)
                        {
                            lines.Add("  Зафиксированные карты:");
                            foreach (var card in receiptSelectedCards)
                                lines.AddRange(BuildShiningBlessingCardInspectionLines(card, context, isSelected: true));
                        }
                        else
                        {
                            if (receipt["selectedCards"] is JsonArray)
                                lines.Add("  Зафиксированные карты: [dim]stored snapshot повреждён; показан canonical id-набор без stale card payload[/]");
                            AppendShiningNamedIdList(
                                lines,
                                "Зафиксированные карты",
                                receipt["selectedCardIds"] as JsonArray,
                                id => ResolveShiningBlessingCardLabel(context.Root, id));
                        }
                        break;
                    case ShiningCoreActionRequestState.ActionTypePullRelicGacha:
                        var gachaFactionId = GetNodeString(receipt["factionId"]) ?? string.Empty;
                        var gachaRelicId = GetNodeString(receipt["relicId"]) ?? string.Empty;
                        var stableBannerName = GetNodeString(receipt["factionName"]);
                        if (string.IsNullOrWhiteSpace(stableBannerName))
                            stableBannerName = !string.IsNullOrWhiteSpace(gachaFactionId)
                                ? ResolveShiningFactionLabel(context.Root, gachaFactionId)
                                : "фракция";
                        lines.Add($"  Баннер: [white]{Markup.Escape(stableBannerName)}[/] [dim]({Markup.Escape(gachaFactionId)})[/]");
                        if (!string.IsNullOrWhiteSpace(GetNodeString(receipt["returnCycleId"])))
                            lines.Add($"  Цикл возвращения: [dim]{Markup.Escape(GetNodeString(receipt["returnCycleId"])!)}[/]");
                        lines.Add($"  Редкость: [dim]{Markup.Escape(DescribeForgeRarity(GetNodeString(receipt["baseRarity"])))} -> {Markup.Escape(DescribeForgeRarity(GetNodeString(receipt["finalRarity"])))}[/]");
                        lines.Add($"  Реликвия: [white]{Markup.Escape(relicName)}[/] [dim]({Markup.Escape(gachaRelicId)})[/]");
                        break;
                    case ShiningCoreActionRequestState.ActionTypeForgeRelicReshape:
                    case ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty:
                    case ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand:
                    case ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho:
                    case ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity:
                        var forgeFactionId = GetNodeString(receipt["factionId"]) ?? string.Empty;
                        var forgeRelicId = GetNodeString(receipt["relicId"]) ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(forgeFactionId))
                            lines.Add($"  Фракция кузни: [dim]{Markup.Escape(forgeFactionId)}[/]");
                        lines.Add($"  Реликвия: [white]{Markup.Escape(relicName)}[/] [dim]({Markup.Escape(forgeRelicId)})[/]");
                        if (!string.IsNullOrWhiteSpace(GetNodeString(receipt["targetFormTag"])))
                            lines.Add($"  Новая форма: [dim]{Markup.Escape(DescribeForgeFormTag(GetNodeString(receipt["targetFormTag"])))}[/]");
                        if (TryReadIntegerNode(receipt["propertyIndex"], out var propertyIndex) && propertyIndex >= 0)
                            lines.Add($"  Номер свойства: [dim]{propertyIndex + 1}[/]");
                        AppendShiningForgePropertyBlock(lines, "Новое свойство", receipt["replacementProperty"]);
                        AppendShiningForgePropertyBlock(lines, "Добавленные свойства", receipt["addedProperties"]);
                        break;
                }
            }
        }

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 📜 Исходы Обители ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        if (receipts.Count > 0)
        {
            var receiptAudit = new JsonArray();
            foreach (var receipt in receipts)
                receiptAudit.Add(CloneShiningJsonForPlayerFacingAudit(receipt));
            WriteJsonAuditPanel("JSON coreActionReceipts[] для просмотра (скрытые runtime details удалены)", receiptAudit, Color.Gold1);
            WriteJsonAuditPanel(
                "JSON shining_abode_state.json после исходов для просмотра (скрытые runtime details удалены)",
                CloneShiningJsonForPlayerFacingAudit(context.Root),
                Color.Gold1);
        }
    }

    private void ShowShiningPendingCoreActionInspectionPanel(
        ShiningContext context,
        IReadOnlyList<ShiningCoreActionRequestState.PendingShiningCoreActionRequest> requests)
    {
        var lines = new List<string>
        {
            "[bold yellow]📝 Ожидающие действия Обители[/]",
            ""
        };

        if (requests.Count == 0)
        {
            lines.Add("[dim]Ожидающих действий сейчас нет.[/]");
        }
        else
        {
            foreach (var request in requests)
            {
                lines.Add($"[bold]{Markup.Escape(DescribeShiningCoreActionLabel(request.ActionType))}[/]");
                lines.Add($"  Идентификатор запроса: [dim]{Markup.Escape(request.RequestId)}[/]");

                if (!string.IsNullOrWhiteSpace(request.FactionId) || !string.IsNullOrWhiteSpace(request.FactionName))
                {
                    var factionName = string.IsNullOrWhiteSpace(request.FactionName)
                        ? ResolveShiningFactionLabel(context.Root, request.FactionId)
                        : request.FactionName;
                    lines.Add($"  Фракция: [white]{Markup.Escape(factionName)}[/] [dim]({Markup.Escape(request.FactionId)})[/]");
                }

                if (!string.IsNullOrWhiteSpace(request.ProjectId) || !string.IsNullOrWhiteSpace(request.ProjectDisplayName))
                {
                    var projectName = string.IsNullOrWhiteSpace(request.ProjectDisplayName)
                        ? ResolveShiningProjectLabel(context.Root, request.FactionId, request.ProjectId)
                        : request.ProjectDisplayName;
                    lines.Add($"  Проект: [white]{Markup.Escape(projectName)}[/] [dim]({Markup.Escape(request.ProjectId)})[/]");
                }

                if (!string.IsNullOrWhiteSpace(request.RelicId) || !string.IsNullOrWhiteSpace(request.RelicName))
                {
                    var relicName = string.IsNullOrWhiteSpace(request.RelicName) ? request.RelicId : request.RelicName;
                    lines.Add($"  Реликвия: [white]{Markup.Escape(relicName)}[/] [dim]({Markup.Escape(request.RelicId)})[/]");
                }

                if (!string.IsNullOrWhiteSpace(request.ReturnCycleId))
                    lines.Add($"  Цикл возвращения: [dim]{Markup.Escape(request.ReturnCycleId)}[/]");
                if (request.QuotedCostFeathers > 0 || request.QuotedCostLightSparks > 0)
                    lines.Add($"  Стоимость: [dim]{request.QuotedCostFeathers} Перьев / {request.QuotedCostLightSparks} Искр Света[/]");
                if (request.ProjectedGachaBonusSteps > 0)
                    lines.Add($"  Расчётный бонус гачи: [dim]+{request.ProjectedGachaBonusSteps} ступени[/]");
                if (!string.IsNullOrWhiteSpace(request.TargetFormTag))
                    lines.Add($"  Целевая форма: [dim]{Markup.Escape(DescribeForgeFormTag(request.TargetFormTag))}[/]");
                if (request.PropertyIndex >= 0)
                    lines.Add($"  Номер свойства: [dim]{request.PropertyIndex + 1}[/]");
                if (request.ReplacementProperty is JsonObject replacementProperty)
                    AppendShiningForgePropertyBlock(lines, "Замещающее свойство", replacementProperty);
                if (request.AddedProperties is JsonArray addedProperties && addedProperties.Count > 0)
                    AppendShiningForgePropertyBlock(lines, "Добавляемые свойства", addedProperties);
                if (request.SourceDraftVersion > 0)
                    lines.Add($"  Версия набора Врат: [dim]{request.SourceDraftVersion}[/]");
                if (request.SelectedCardIds.Count > 0)
                {
                    var requestSelectedCards = GetConsistentPreparedPackageRequestCards(request);
                    if (requestSelectedCards.Count > 0)
                    {
                        lines.Add("  Выбранные карты:");
                        foreach (var card in requestSelectedCards)
                            lines.AddRange(BuildShiningBlessingCardInspectionLines(card, context, isSelected: true));
                    }
                    else
                    {
                        lines.Add("  Выбранные карты:");
                        foreach (var cardId in request.SelectedCardIds)
                            lines.Add($"    [dim]• {Markup.Escape(ResolveShiningBlessingCardLabel(context.Root, cardId))} ({Markup.Escape(cardId)})[/]");
                    }
                }

                if (request.ProjectDraft is JsonObject projectDraft)
                {
                    lines.Add("  Черновик проекта:");
                    lines.Add($"    Название: [white]{Markup.Escape(GetNodeString(projectDraft["displayName"]) ?? "без названия")}[/]");
                    lines.Add($"    Сводка: [dim]{Markup.Escape(GetNodeString(projectDraft["summary"]) ?? "без сводки")}[/]");
                    lines.Add($"    Архетип: [dim]{Markup.Escape(DescribeShiningProjectArchetype(GetNodeString(projectDraft["projectArchetype"])))}[/]");
                    lines.Add($"    Семейство эффекта: [dim]{Markup.Escape(DescribeShiningEffectFamily(GetNodeString(projectDraft["outputEffectFamily"])))}[/]");
                    lines.Add($"    Уровень проекта: [dim]{GetNodeInt(projectDraft["tier"])}[/]");
                    lines.Add("    Любимый архетип: [dim]может снизить цену completion, но не меняет strengthReward; сила проекта определяется только tier 8/12/16[/]");
                    AppendShiningNamedIdList(
                        lines,
                        "Целевые фракции",
                        projectDraft["targetFactionIds"] as JsonArray,
                        id => ResolveShiningFactionLabel(context.Root, id));
                    AppendShiningStringList(lines, "Тоновые метки", projectDraft["toneTags"] as JsonArray);
                }

                lines.Add("  Полный контракт, который должен закрыть GM:");
                foreach (var previewLine in BuildShiningCoreActionRequestPreviewLines(context, request)
                             .Where(line => !string.IsNullOrWhiteSpace(line)))
                {
                    lines.Add($"    {previewLine}");
                }

                lines.Add($"  Создан на ходу: [dim]{request.CreatedAtTurn}[/]");
                if (!string.IsNullOrWhiteSpace(request.CreatedAtUtc))
                    lines.Add($"  Создан в UTC: [dim]{Markup.Escape(request.CreatedAtUtc)}[/]");
                lines.Add("");
            }
        }

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines).TrimEnd()))
        {
            Header = new PanelHeader(" 📝 Ожидающие действия Обители ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        if (requests.Count > 0)
        {
            var pendingAudit = new JsonArray();
            var receiptAudit = new JsonArray();
            var stateDeltaAudit = new JsonArray();
            foreach (var request in requests)
            {
                pendingAudit.Add(JsonSerializer.SerializeToNode(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                receiptAudit.Add(new JsonObject
                {
                    ["requestId"] = request.RequestId,
                    ["actionType"] = request.ActionType,
                    ["expectedReceipts"] = BuildShiningCoreExpectedReceiptAuditNode(context, request)
                });
                stateDeltaAudit.Add(new JsonObject
                {
                    ["requestId"] = request.RequestId,
                    ["actionType"] = request.ActionType,
                    ["expectedAcceptedStateDelta"] = BuildShiningCoreExpectedStateDeltaAuditNode(context, request)
                });
            }

            WriteJsonAuditPanel("Полный JSON pending Shining core actions", pendingAudit, Color.Gold1);
            WriteJsonAuditPanel("Ожидаемые typed coreActionReceipts[] для pending Shining core actions", receiptAudit, Color.Gold1);
            WriteJsonAuditPanel("Ожидаемые accepted-state deltas для pending Shining core actions", stateDeltaAudit, Color.Gold1);
        }
    }

    private void ShowShiningPendingPoliticalInspectionPanel(
        ShiningContext context,
        IReadOnlyList<ShiningFactionRequestState.PendingShiningFactionFoundingRequest> foundingRequests,
        IReadOnlyList<ShiningFactionRequestState.PendingShiningFactionRealignmentRequest> realignmentRequests,
        IReadOnlyList<ShiningFactionRequestState.PendingShiningFactionLeadershipTransitionRequest> leadershipRequests)
    {
        var lines = new List<string>
        {
            "[bold yellow]📝 Ожидающие политические запросы[/]"
        };

        if (foundingRequests.Count == 0 && realignmentRequests.Count == 0 && leadershipRequests.Count == 0)
        {
            lines.Add("");
            lines.Add("[dim]Ожидающих политических запросов сейчас нет.[/]");
        }
        else
        {
            if (foundingRequests.Count > 0)
            {
                lines.Add("");
                lines.Add("[bold]Основание фракций:[/]");
                foreach (var request in foundingRequests)
                {
                    lines.Add($"  • [white]{Markup.Escape(request.Charter.FactionName)}[/]");
                    lines.Add($"    Идентификатор запроса: [dim]{Markup.Escape(request.RequestId)}[/]");
                    lines.Add($"    Зал: [white]{Markup.Escape(request.ProposedHallName)}[/] [dim]({Markup.Escape(request.ProposedHallId)})[/]");
                    lines.Add($"    Фракция: [white]{Markup.Escape(request.Charter.FactionName)}[/] [dim]({Markup.Escape(request.ProposedFactionId)})[/]");
                    lines.Add($"    Описание зала: [dim]{Markup.Escape(request.ProposedHallDescription)}[/]");
                    if (request.ProposedHallServiceTags.Count > 0)
                        lines.Add($"    Службы зала: [dim]{Markup.Escape(string.Join(", ", request.ProposedHallServiceTags.Select(DescribeShiningHallServiceTag)))}[/]");
                    lines.Add($"    Устав фракции: [dim]{Markup.Escape(request.Charter.Summary)}[/]");
                    lines.Add($"    Любимый архетип: [dim]{Markup.Escape(DescribeShiningProjectArchetype(request.Charter.FavoredArchetype))}[/]");
                    lines.Add($"    Покровительствующий эффект: [dim]{Markup.Escape(DescribeShiningEffectFamily(request.Charter.PatronEffectFamily))}[/]");
                    lines.Add($"    Стоимость: [dim]{request.QuotedCostFeathers} Перьев / {request.QuotedCostLightSparks} Искр Света, уже зарезервирована при создании pending contract[/]");
                    AppendShiningStringList(
                        lines,
                        "    Сторонники",
                        new JsonArray(request.SupportingResidentIds
                            .Select(id => BuildResidentSnapshotLabelNode(context.ResidentRoot, id))
                            .ToArray()));
                    lines.Add($"    Создан на ходу: [dim]{request.CreatedAtTurn}[/]");
                    if (!string.IsNullOrWhiteSpace(request.CreatedAtUtc))
                        lines.Add($"    Создан в UTC: [dim]{Markup.Escape(request.CreatedAtUtc)}[/]");
                    lines.Add("    GM closure contract:");
                    lines.Add("      accepted: создать `halls[]`, `factions[]`, aligned supporter residents and `factionFoundingReceipts[]` with exact requestId/costs/supporters/status/time.");
                    lines.Add("      refused/withdrawn: не создавать hall/faction; закрыть только `factionFoundingReceipts[]` with canonical refusal status/reason/time.");
                }
            }

            if (realignmentRequests.Count > 0)
            {
                lines.Add("");
                lines.Add("[bold]Переходы между фракциями:[/]");
                foreach (var request in realignmentRequests)
                {
                    var targetFactionName = string.IsNullOrWhiteSpace(request.TargetFactionName) ? "нейтраль" : request.TargetFactionName;
                    lines.Add($"  • [white]{Markup.Escape(request.ResidentName)}[/]");
                    lines.Add($"    Идентификатор запроса: [dim]{Markup.Escape(request.RequestId)}[/]");
                    lines.Add($"    Резидент: [white]{Markup.Escape(request.ResidentName)}[/] [dim]({Markup.Escape(request.ResidentId)})[/]");
                    lines.Add($"    Источник: [white]{Markup.Escape(request.SourceFactionName)}[/] [dim]({Markup.Escape(request.SourceFactionId)})[/]");
                    lines.Add($"    Назначение: [white]{Markup.Escape(targetFactionName)}[/] [dim]({Markup.Escape(request.TargetFactionId)})[/]");
                    lines.Add($"    Режим перехода: [dim]{Markup.Escape(DescribeShiningRealignmentMode(request.RealignmentMode))}[/]");
                    lines.Add($"    Лояльность: [dim]{request.FactionLoyaltyLevel} ({Markup.Escape(DescribeShiningFactionLoyaltyTier(request.FactionLoyaltyTier))})[/]");
                    lines.Add($"    Внутреннее брожение: [dim]{request.FactionRestlessness}[/]");
                    lines.Add($"    Состояние перестройки: [dim]{Markup.Escape(DescribeShiningFactionRealignmentState(request.FactionRealignmentState))}[/]");
                    lines.Add($"    Создан на ходу: [dim]{request.CreatedAtTurn}[/]");
                    if (!string.IsNullOrWhiteSpace(request.CreatedAtUtc))
                        lines.Add($"    Создан в UTC: [dim]{Markup.Escape(request.CreatedAtUtc)}[/]");
                    lines.Add("    GM closure contract:");
                    lines.Add("      accepted_transfer: обновить resident `shiningFactionId/name`, loyalty/restlessness/realignment state, history entry and `factionRealignmentReceipts[]`.");
                    lines.Add("      departure_to_neutral/refused/withdrawn: не изобретать новую фракцию; закрыть canonical receipt/status/reason and only apply the allowed resident binding change.");
                }
            }

            if (leadershipRequests.Count > 0)
            {
                lines.Add("");
                lines.Add("[bold]Смена власти:[/]");
                foreach (var request in leadershipRequests)
                {
                    lines.Add($"  • [white]{Markup.Escape(request.FactionName)}[/]");
                    lines.Add($"    Идентификатор запроса: [dim]{Markup.Escape(request.RequestId)}[/]");
                    lines.Add($"    Фракция: [white]{Markup.Escape(request.FactionName)}[/] [dim]({Markup.Escape(request.FactionId)})[/]");
                    lines.Add($"    Режим перехода: [dim]{Markup.Escape(DescribeShiningLeadershipMode(request.TransitionMode))}[/]");
                    lines.Add($"    Текущий глава: [white]{Markup.Escape(BuildHeadActorLabel(request.IncumbentHeadActorType, request.IncumbentHeadActorId, context.ResidentRoot, context.GuardiansRoot, context.Root))}[/]");
                    lines.Add($"    Кандидат: [white]{Markup.Escape(BuildHeadActorLabel(request.CandidateHeadActorType, request.CandidateHeadActorId, context.ResidentRoot, context.GuardiansRoot, context.Root))}[/]");
                    AppendShiningStringList(
                        lines,
                        "    Сторонники",
                        new JsonArray(request.SupportingResidentIds
                            .Select(id => BuildResidentSnapshotLabelNode(context.ResidentRoot, id))
                            .ToArray()));
                    lines.Add($"    Создан на ходу: [dim]{request.CreatedAtTurn}[/]");
                    if (!string.IsNullOrWhiteSpace(request.CreatedAtUtc))
                        lines.Add($"    Создан в UTC: [dim]{Markup.Escape(request.CreatedAtUtc)}[/]");
                    lines.Add("    GM closure contract:");
                    lines.Add("      accepted: обновить `factions[].leadership`, `leadershipReceipts[]`, `leadershipHistory[]` and matching `shiningPoliticalActors[]` when head is radiant_actor.");
                    lines.Add("      refused/withdrawn: leadership remains unchanged except canonical receipt/history refusal marker with exact requestId/candidate/supporters.");
                }
            }
        }

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 📝 Политические запросы ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Orange1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        var pendingAudit = new JsonObject
        {
            ["foundingRequests"] = JsonSerializer.SerializeToNode(foundingRequests, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
            ["realignmentRequests"] = JsonSerializer.SerializeToNode(realignmentRequests, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
            ["leadershipRequests"] = JsonSerializer.SerializeToNode(leadershipRequests, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)
        };
        WriteJsonAuditPanel("Полный JSON pending political contracts", pendingAudit, Color.Orange1);

        var politicalReceiptAudit = new JsonObject
        {
            ["foundingRequests"] = BuildShiningPoliticalPendingReceiptAuditArray(
                ShiningFactionRequestState.PendingFoundingsRequestPath,
                foundingRequests),
            ["realignmentRequests"] = BuildShiningPoliticalPendingReceiptAuditArray(
                ShiningFactionRequestState.PendingRealignmentsRequestPath,
                realignmentRequests),
            ["leadershipRequests"] = BuildShiningPoliticalPendingReceiptAuditArray(
                ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
                leadershipRequests)
        };
        WriteJsonAuditPanel("Ожидаемые typed political receipts/history для pending contracts", politicalReceiptAudit, Color.Orange1);
    }

    private static JsonArray BuildShiningPoliticalPendingReceiptAuditArray<TRequest>(
        string pendingPath,
        IReadOnlyList<TRequest> requests)
    {
        var result = new JsonArray();
        foreach (var request in requests)
        {
            var requestAudit = JsonSerializer.SerializeToNode(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed) as JsonObject;
            result.Add(new JsonObject
            {
                ["requestId"] = GetNodeString(requestAudit?["requestId"]) ?? string.Empty,
                ["expectedReceiptsAndHistory"] = BuildShiningPoliticalExpectedReceiptAuditNode(pendingPath, requestAudit)
            });
        }

        return result;
    }

    private void ShowShiningPoliticalResolutionInspectionPanel(ShiningContext context)
    {
        var foundingReceipts = ShiningAbodeState.EnsureFactionFoundingReceiptsArray(context.Root).OfType<JsonObject>()
            .OrderByDescending(item => GetNodeInt(item["resolvedAtTurn"]))
            .ThenByDescending(item => GetNodeString(item["resolvedAtUtc"]), StringComparer.OrdinalIgnoreCase)
            .ToList();
        var realignmentReceipts = ShiningAbodeState.EnsureFactionRealignmentReceiptsArray(context.Root).OfType<JsonObject>()
            .OrderByDescending(item => GetNodeInt(item["resolvedAtTurn"]))
            .ThenByDescending(item => GetNodeString(item["resolvedAtUtc"]), StringComparer.OrdinalIgnoreCase)
            .ToList();
        var leadershipReceipts = ShiningAbodeState.EnsureFactionsArray(context.Root).OfType<JsonObject>()
            .SelectMany(faction =>
            {
                var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? GetNodeString(faction["factionId"]) ?? "?";
                return (faction["leadershipReceipts"] as JsonArray)?.OfType<JsonObject>()
                    .Select(receipt => (Faction: faction, FactionName: factionName, Receipt: receipt))
                    ?? Enumerable.Empty<(JsonObject Faction, string FactionName, JsonObject Receipt)>();
            })
            .OrderByDescending(item => GetNodeInt(item.Receipt["resolvedAtTurn"]))
            .ThenByDescending(item => GetNodeString(item.Receipt["resolvedAtUtc"]), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lines = new List<string>
        {
            "[bold yellow]📜 Полный осмотр решений фракций[/]"
        };

        if (foundingReceipts.Count == 0 && realignmentReceipts.Count == 0 && leadershipReceipts.Count == 0)
        {
            lines.Add("");
            lines.Add("[dim]Подтверждённых политических решений пока нет.[/]");
        }
        else
        {
            if (foundingReceipts.Count > 0)
            {
                lines.Add("");
                lines.Add("[bold]Основание фракций:[/]");
                foreach (var receipt in foundingReceipts)
                {
                    var factionId = GetNodeString(receipt["factionId"]) ?? GetNodeString(receipt["proposedFactionId"]) ?? string.Empty;
                    var hallId = GetNodeString(receipt["hallId"]) ?? GetNodeString(receipt["proposedHallId"]) ?? string.Empty;
                    lines.Add($"  • [white]{Markup.Escape(BuildShiningFoundingReceiptSummary(receipt))}[/]");
                    AppendShiningResolutionAuditLines(lines, receipt, 4);
                    lines.Add($"    Зал: [white]{Markup.Escape(GetNodeString(receipt["hallName"]) ?? hallId)}[/] [dim]({Markup.Escape(hallId)})[/]");
                    var hallDescription = GetNodeString(receipt["hallDescription"]);
                    if (!string.IsNullOrWhiteSpace(hallDescription))
                        lines.Add($"    Описание зала: [dim]{Markup.Escape(hallDescription!)}[/]");
                    var serviceTags = receipt["hallServiceTags"] as JsonArray;
                    if (serviceTags != null && serviceTags.Count > 0)
                        lines.Add($"    Службы зала: [dim]{Markup.Escape(string.Join(", ", serviceTags.Select(tag => DescribeShiningHallServiceTag(GetNodeString(tag))).Where(tag => !string.IsNullOrWhiteSpace(tag))))}[/]");
                    lines.Add($"    Фракция: [white]{Markup.Escape(GetNodeString(receipt["factionName"]) ?? factionId)}[/] [dim]({Markup.Escape(factionId)})[/]");
                    var charterSummary = GetNodeString(receipt["charterSummary"]);
                    if (!string.IsNullOrWhiteSpace(charterSummary))
                        lines.Add($"    Устав фракции: [dim]{Markup.Escape(charterSummary!)}[/]");
                    var favoredArchetype = GetNodeString(receipt["favoredArchetype"]);
                    if (!string.IsNullOrWhiteSpace(favoredArchetype))
                        lines.Add($"    Любимый архетип: [dim]{Markup.Escape(DescribeShiningProjectArchetype(favoredArchetype))}[/]");
                    var patronEffectFamily = GetNodeString(receipt["patronEffectFamily"]);
                    if (!string.IsNullOrWhiteSpace(patronEffectFamily))
                        lines.Add($"    Покровительствующий эффект: [dim]{Markup.Escape(DescribeShiningEffectFamily(patronEffectFamily))}[/]");
                    var quotedCostFeathers = GetNodeInt(receipt["quotedCostFeathers"]);
                    var quotedCostLightSparks = GetNodeInt(receipt["quotedCostLightSparks"]);
                    if (quotedCostFeathers > 0 || quotedCostLightSparks > 0)
                        lines.Add($"    Стоимость: [dim]{quotedCostFeathers} Перьев / {quotedCostLightSparks} Искр Света, зарезервирована pending contract[/]");
                    AppendShiningStableNamedIdList(
                        lines,
                        "    Сторонники",
                        receipt["supportingResidentIds"] as JsonArray,
                        receipt["supportingResidentLabels"] as JsonArray);
                }
            }

            if (realignmentReceipts.Count > 0)
            {
                lines.Add("");
                lines.Add("[bold]Перестройка резидентов:[/]");
                foreach (var receipt in realignmentReceipts)
                {
                    var residentId = GetNodeString(receipt["residentId"]) ?? string.Empty;
                    lines.Add($"  • [white]{Markup.Escape(BuildShiningRealignmentReceiptSummary(receipt))}[/]");
                    AppendShiningResolutionAuditLines(lines, receipt, 4);
                    lines.Add($"    Резидент: [white]{Markup.Escape(GetNodeString(receipt["residentName"]) ?? residentId)}[/] [dim]({Markup.Escape(residentId)})[/]");
                    var sourceFactionLabel = string.IsNullOrWhiteSpace(GetNodeString(receipt["sourceFactionName"]))
                        ? (GetNodeString(receipt["sourceFactionId"]) ?? "?")
                        : GetNodeString(receipt["sourceFactionName"])!;
                    var targetFactionLabel = string.IsNullOrWhiteSpace(GetNodeString(receipt["targetFactionName"]))
                        ? (string.IsNullOrWhiteSpace(GetNodeString(receipt["targetFactionId"]))
                            ? "нейтраль"
                            : GetNodeString(receipt["targetFactionId"]) ?? "нейтраль")
                        : GetNodeString(receipt["targetFactionName"])!;
                    lines.Add($"    Источник: [dim]{Markup.Escape(sourceFactionLabel)} -> {Markup.Escape(targetFactionLabel)}[/]");
                    lines.Add($"    Режим: [dim]{Markup.Escape(DescribeShiningRealignmentMode(GetNodeString(receipt["realignmentMode"])))}[/]");
                    AppendResidentHistoryReference(lines, context.ResidentRoot, receipt, 4);
                }
            }

            if (leadershipReceipts.Count > 0)
            {
                lines.Add("");
                lines.Add("[bold]Смена главы:[/]");
                foreach (var item in leadershipReceipts)
                {
                    var historyEntry = (item.Faction["leadershipHistory"] as JsonArray)?.OfType<JsonObject>()
                        .FirstOrDefault(entry => string.Equals(GetNodeString(entry["requestId"]), GetNodeString(item.Receipt["requestId"]), StringComparison.OrdinalIgnoreCase));
                    lines.Add($"  • [white]{Markup.Escape(BuildShiningLeadershipReceiptSummary(item.FactionName, item.Receipt))}[/]");
                    AppendShiningResolutionAuditLines(lines, item.Receipt, 4);
                    var stableFactionName = GetNodeString(item.Receipt["factionName"]) ?? GetNodeString(item.Receipt["factionId"]) ?? "?";
                    var stableFactionId = GetNodeString(item.Receipt["factionId"]) ?? string.Empty;
                    var previousType = GetNodeString(item.Receipt["previousHeadActorType"]) ?? string.Empty;
                    var previousId = GetNodeString(item.Receipt["previousHeadActorId"]) ?? string.Empty;
                    var newType = GetNodeString(item.Receipt["newHeadActorType"]) ?? string.Empty;
                    var newId = GetNodeString(item.Receipt["newHeadActorId"]) ?? string.Empty;
                    lines.Add($"    Фракция: [white]{Markup.Escape(stableFactionName)}[/] [dim]({Markup.Escape(stableFactionId)})[/]");
                    lines.Add($"    Предыдущий глава: [dim]{Markup.Escape(BuildStableHeadActorReceiptLabel(GetNodeString(item.Receipt["previousHeadLabel"]), previousType, previousId))} ({Markup.Escape(previousType)}:{Markup.Escape(previousId)})[/]");
                    lines.Add($"    Новый глава: [dim]{Markup.Escape(BuildStableHeadActorReceiptLabel(GetNodeString(item.Receipt["newHeadLabel"]), newType, newId))} ({Markup.Escape(newType)}:{Markup.Escape(newId)})[/]");
                    lines.Add($"    Режим перехода: [dim]{Markup.Escape(DescribeShiningLeadershipMode(GetNodeString(item.Receipt["transitionMode"])))}[/]");
                    if (historyEntry != null)
                    {
                        if (!string.IsNullOrWhiteSpace(GetNodeString(historyEntry["eventType"])))
                            lines.Add($"    Историческое событие: [dim]{Markup.Escape(DescribeShiningLeadershipHistoryEventType(GetNodeString(historyEntry["eventType"])))}[/]");
                        if (!string.IsNullOrWhiteSpace(GetNodeString(historyEntry["summary"])))
                            lines.Add($"    Историческая сводка: [dim]{Markup.Escape(GetNodeString(historyEntry["summary"])!)}[/]");
                        if (GetNodeInt(historyEntry["turnNumber"]) > 0)
                            lines.Add($"    Исторический ход: [dim]{GetNodeInt(historyEntry["turnNumber"])}[/]");
                    }
                }
            }
        }

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 📜 Решения фракций ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Orange1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        WriteJsonAuditPanel("Полный JSON political state and receipts", context.Root, Color.Orange1);
    }

    private static void AppendShiningReceiptConsequenceLines(List<string> lines, JsonObject receipt, int indent = 2)
    {
        var fields = new (string Key, string Label)[]
        {
            ("quotedCostFeathers", "Списанные/заявленные Чернильные Перья"),
            ("quotedCostLightSparks", "Списанные/заявленные Искры Света"),
            ("costInFeathers", "Стоимость в Чернильных Перьях"),
            ("costLightSparks", "Стоимость в Искрах Света"),
            ("radianceXpGained", "Полученный опыт Сияния"),
            ("factionStrengthDelta", "Изменение силы фракции"),
            ("previousFactionStrength", "Сила фракции до исхода"),
            ("newFactionStrength", "Сила фракции после исхода"),
            ("factionStrengthBefore", "Сила фракции до исхода"),
            ("factionStrengthAfter", "Сила фракции после исхода"),
            ("chargesUsedBefore", "Заряды сияющей гачи до исхода"),
            ("chargesUsedAfter", "Заряды сияющей гачи после исхода"),
            ("chargesRemaining", "Оставшиеся заряды сияющей гачи"),
            ("gatesMarkedStale", "Открытый набор Врат помечен устаревшим"),
            ("generatedDraftVersion", "Версия набора Врат"),
            ("sourceDraftVersion", "Исходная версия набора Врат"),
            ("baseRarity", "Базовая редкость"),
            ("finalRarity", "Итоговая редкость"),
            ("selectedCardIds", "Выбранные карты"),
            ("selectedCards", "Зафиксированные снимки карт"),
            ("replacementProperty", "Новое свойство перековки"),
            ("addedProperties", "Добавленные свойства перековки")
        };

        var consequenceLines = new List<string>();
        foreach (var (key, label) in fields)
        {
            if (!receipt.TryGetPropertyValue(key, out var node) || node == null)
                continue;

            consequenceLines.Add($"{new string(' ', indent)}• {label} [dim]({Markup.Escape(key)})[/]: {Markup.Escape(FormatShiningReceiptAuditValue(node))}");
        }

        if (consequenceLines.Count == 0)
            return;

        lines.Add($"{new string(' ', indent)}Ключевые последствия receipt:");
        lines.AddRange(consequenceLines);
    }

    private static string FormatShiningReceiptAuditValue(JsonNode node)
    {
        if (node is JsonArray array)
        {
            if (array.Count == 0)
                return "[]";

            return string.Join("; ", array.Select(item => item == null ? "null" : FormatShiningReceiptAuditValue(item)));
        }

        if (node is JsonObject obj)
            return FormatShiningReceiptAuditObject(obj);

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
                return text;
            if (value.TryGetValue<bool>(out var boolValue))
                return boolValue ? "да" : "нет";
            if (value.TryGetValue<int>(out var intValue))
                return intValue.ToString();
            if (value.TryGetValue<long>(out var longValue))
                return longValue.ToString();
            if (value.TryGetValue<double>(out var doubleValue))
                return doubleValue.ToString("0.###");
        }

        return node.ToJsonString();
    }

    private static string FormatShiningReceiptAuditObject(JsonObject obj)
    {
        var cardId = GetNodeString(obj["cardId"]);
        if (!string.IsNullOrWhiteSpace(cardId))
        {
            var displayName = GetNodeString(obj["displayName"]) ?? GetNodeString(obj["name"]) ?? cardId;
            var rarity = DescribeForgeRarity(GetNodeString(obj["rarity"]) ?? string.Empty);
            var effectFamily = DescribeShiningEffectFamily(GetNodeString(obj["effectFamily"]));
            var sourceType = DescribeShiningBlessingReceiptSourceType(GetNodeString(obj["sourceType"]));
            var sourceFactionId = GetNodeString(obj["sourceFactionId"]);
            var sourceActorId = GetNodeString(obj["sourceActorId"]);
            var parts = new List<string>
            {
                $"{displayName} ({cardId})",
                $"редкость {rarity}",
                $"эффект {effectFamily}"
            };

            var displaySummary = GetNodeString(obj["displaySummary"]);
            if (!string.IsNullOrWhiteSpace(displaySummary))
                parts.Add($"сводка: {displaySummary}");
            if (!string.IsNullOrWhiteSpace(sourceType))
                parts.Add($"источник {sourceType}");
            if (!string.IsNullOrWhiteSpace(sourceFactionId))
                parts.Add($"фракция {sourceFactionId}");
            if (!string.IsNullOrWhiteSpace(sourceActorId))
                parts.Add($"актор {sourceActorId}");
            foreach (var effectDetail in BuildShiningBlessingEffectDetailLines(obj))
                parts.Add($"деталь: {effectDetail}");

            return string.Join(", ", parts);
        }

        if (obj.ContainsKey("propertyId") ||
            obj.ContainsKey("stat") ||
            obj.ContainsKey("band") ||
            obj.ContainsKey("effectFamily"))
        {
            return $"{DescribeShiningForgePropertyLabel(obj)} {obj.ToJsonString()}";
        }

        return obj.ToJsonString();
    }

    private static JsonNode? CloneShiningJsonForPlayerFacingAudit(JsonNode? node)
    {
        if (node == null)
            return null;

        var clone = node.DeepClone();
        RemoveShiningBlessingRuntimePayloads(clone);
        return clone;
    }

    private static void RemoveShiningBlessingRuntimePayloads(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                AddSafeShiningBlessingEffectDetails(obj);
                obj.Remove("effectPayload");
                foreach (var child in obj.Select(pair => pair.Value).ToList())
                    RemoveShiningBlessingRuntimePayloads(child);
                break;
            case JsonArray array:
                foreach (var child in array)
                    RemoveShiningBlessingRuntimePayloads(child);
                break;
        }
    }

    private static void AddSafeShiningBlessingEffectDetails(JsonObject obj)
    {
        if (obj["effectPayload"] is not JsonObject || !obj.ContainsKey("cardId"))
            return;

        var detailLines = BuildShiningBlessingEffectDetailLines(obj);
        if (detailLines.Count == 0)
            return;

        obj["safeEffectDetails"] = new JsonArray(detailLines.Select(line => JsonValue.Create(line)).ToArray<JsonNode?>());
    }

    private static string DescribeShiningBlessingReceiptSourceType(string? sourceType) =>
        (sourceType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "head" => "глава фракции",
            "project" => "проект",
            "resident_descent" => "резидент",
            "" => string.Empty,
            _ => sourceType ?? string.Empty
        };

    private static void AppendShiningResolutionAuditLines(List<string> lines, JsonObject receipt, int indent = 2)
    {
        var indentPrefix = new string(' ', indent);
        var requestId = GetNodeString(receipt["requestId"]);
        if (!string.IsNullOrWhiteSpace(requestId))
            lines.Add($"{indentPrefix}Запрос: [dim]{Markup.Escape(requestId)}[/]");

        var status = GetNodeString(receipt["status"]);
        if (!string.IsNullOrWhiteSpace(status))
            lines.Add($"{indentPrefix}Статус: [white]{Markup.Escape(DescribeShiningResolutionStatus(status))}[/]");

        var resolvedAtTurn = GetNodeInt(receipt["resolvedAtTurn"]);
        if (resolvedAtTurn > 0)
            lines.Add($"{indentPrefix}Решено на ходу: [dim]{resolvedAtTurn}[/]");

        var resolvedAtUtc = GetNodeString(receipt["resolvedAtUtc"]);
        if (!string.IsNullOrWhiteSpace(resolvedAtUtc))
            lines.Add($"{indentPrefix}Время решения: [dim]{Markup.Escape(resolvedAtUtc)}[/]");

        var reason = GetNodeString(receipt["reason"]);
        if (!string.IsNullOrWhiteSpace(reason))
            lines.Add($"{indentPrefix}Причина решения: [dim]{Markup.Escape(DescribeShiningResolutionReason(reason))}[/]");
    }

    private static void AppendCappedSectionOverflowLine(List<string> lines, int totalCount, int cap)
    {
        if (totalCount > cap)
            lines.Add($"  • [dim]…и ещё {totalCount - cap}; полный список доступен через соответствующий пункт осмотра.[/]");
    }

    private static string FormatAfterlifeNotificationInline(AfterlifeNotificationState.NotificationEntry notification)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(notification.Summary))
            parts.Add(notification.Summary);
        if (!string.IsNullOrWhiteSpace(notification.NotificationId))
            parts.Add($"notificationId={notification.NotificationId}");
        if (!string.IsNullOrWhiteSpace(notification.RequestId))
            parts.Add($"requestId={notification.RequestId}");
        if (!string.IsNullOrWhiteSpace(notification.NotificationType))
            parts.Add($"type={notification.NotificationType}");
        return string.Join("; ", parts);
    }

    private static void AppendShiningNamedIdList(
        List<string> lines,
        string title,
        JsonArray? ids,
        Func<string?, string?> resolveLabel)
    {
        if (ids == null || ids.Count == 0)
            return;

        lines.Add($"  {title}:");
        foreach (var node in ids)
        {
            var id = GetNodeString(node);
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var label = resolveLabel(id);
            if (!string.IsNullOrWhiteSpace(label) && !string.Equals(label, id, StringComparison.OrdinalIgnoreCase))
                lines.Add($"    • {Markup.Escape(label)} [dim]({Markup.Escape(id)})[/]");
            else
                lines.Add($"    • [dim]{Markup.Escape(id)}[/]");
        }
    }

    private static void AppendShiningStableNamedIdList(
        List<string> lines,
        string title,
        JsonArray? ids,
        JsonArray? stableLabels)
    {
        if (ids == null || ids.Count == 0)
            return;

        lines.Add($"  {title}:");
        for (var index = 0; index < ids.Count; index++)
        {
            var id = GetNodeString(ids[index]);
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var stableLabel = stableLabels != null && index < stableLabels.Count
                ? GetNodeString(stableLabels[index])
                : null;
            if (!string.IsNullOrWhiteSpace(stableLabel) &&
                !string.Equals(stableLabel, id, StringComparison.OrdinalIgnoreCase))
            {
                lines.Add($"    • {Markup.Escape(stableLabel)} [dim]({Markup.Escape(id)})[/]");
            }
            else
            {
                lines.Add($"    • [dim]{Markup.Escape(id)}[/]");
            }
        }
    }

    private static void AppendShiningStringList(List<string> lines, string title, JsonArray? values)
    {
        if (values == null || values.Count == 0)
            return;

        lines.Add($"  {title}:");
        foreach (var node in values)
        {
            var value = GetNodeString(node);
            if (!string.IsNullOrWhiteSpace(value))
                lines.Add($"    • [dim]{Markup.Escape(value)}[/]");
        }
    }

    private static void AppendShiningForgePropertyBlock(List<string> lines, string title, JsonNode? payload)
    {
        switch (payload)
        {
            case JsonObject property:
                lines.Add($"  {title}:");
                AppendShiningForgePropertyLines(lines, property, "    ");
                break;
            case JsonArray properties when properties.Count > 0:
                lines.Add($"  {title}:");
                var propertyNumber = 1;
                foreach (var propertyNode in properties.OfType<JsonObject>())
                {
                    lines.Add($"    Свойство {propertyNumber}:");
                    AppendShiningForgePropertyLines(lines, propertyNode, "      ");
                    propertyNumber += 1;
                }
                break;
        }
    }

    private static void AppendShiningForgePropertyLines(List<string> lines, JsonObject property, string indent)
    {
        var propertyLabel = DescribeShiningForgePropertyLabel(property);
        lines.Add($"{indent}[white]{Markup.Escape(propertyLabel)}[/]");

        var bandLabel = DescribeShiningForgeBand(property["band"]);
        if (!string.IsNullOrWhiteSpace(bandLabel))
            lines.Add($"{indent}[dim]Диапазон: {Markup.Escape(bandLabel)}[/]");

        var description = GetNodeString(property["description"]);
        if (!string.IsNullOrWhiteSpace(description))
            lines.Add($"{indent}[dim]Описание: {Markup.Escape(description)}[/]");

        var stat = GetNodeString(property["stat"]);
        if (!string.IsNullOrWhiteSpace(stat))
            lines.Add($"{indent}[dim]Затронутая характеристика: {Markup.Escape(DescribeShiningForgeStat(stat))}[/]");

        var effectFamily = GetNodeString(property["effectFamily"]);
        if (!string.IsNullOrWhiteSpace(effectFamily))
            lines.Add($"{indent}[dim]Семейство эффекта: {Markup.Escape(DescribeShiningEffectFamily(effectFamily))}[/]");

        var propertyId = GetNodeString(property["propertyId"]);
        if (!string.IsNullOrWhiteSpace(propertyId) &&
            !string.Equals(propertyId, propertyLabel, StringComparison.OrdinalIgnoreCase))
        {
            lines.Add($"{indent}[dim]Идентификатор свойства: {Markup.Escape(propertyId)}[/]");
        }

    }

    private static string DescribeShiningForgeBand(JsonNode? bandNode)
    {
        if (bandNode == null)
            return string.Empty;

        if (bandNode is JsonValue bandValue)
        {
            if (bandValue.TryGetValue<int>(out var numericBand))
                return $"ступень {numericBand}";

            if (bandValue.TryGetValue<string>(out var stringBand))
            {
                var normalizedBand = (stringBand ?? string.Empty).Trim().ToLowerInvariant();
                return normalizedBand switch
                {
                    "common" or "uncommon" or "rare" or "epic" or "legendary" => DescribeForgeRarity(normalizedBand),
                    _ => stringBand ?? string.Empty
                };
            }
        }

        return GetNodeString(bandNode) ?? string.Empty;
    }

    private static List<string> GetPreparedPackageSelectedCardIds(JsonObject package)
    {
        if (package["selectedCardIds"] is not JsonArray selectedCardIds)
            return new List<string>();

        return selectedCardIds.OfType<JsonValue>()
            .Select(node => node.TryGetValue<string>(out var value) ? value?.Trim() ?? string.Empty : string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static List<JsonObject> GetConsistentPreparedPackageCards(JsonObject package)
    {
        if (package["selectedCards"] is not JsonArray selectedCards)
        {
            return new List<JsonObject>();
        }

        var storedIds = GetPreparedPackageSelectedCardIds(package);
        var snapshotCards = selectedCards.OfType<JsonObject>().ToList();
        var snapshotIds = snapshotCards
            .Select(card => GetNodeString(card["cardId"])?.Trim() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        return storedIds.Count > 0 &&
               storedIds.Count == snapshotIds.Count &&
               storedIds.SequenceEqual(snapshotIds, StringComparer.OrdinalIgnoreCase)
            ? snapshotCards
            : new List<JsonObject>();
    }

    private static List<JsonObject> GetConsistentPreparedPackageReceiptCards(JsonObject receipt) =>
        GetConsistentPreparedPackageCards(receipt);

    private static List<JsonObject> GetConsistentPreparedPackageRequestCards(ShiningCoreActionRequestState.PendingShiningCoreActionRequest request)
    {
        if (request.SelectedCards is not JsonArray selectedCards || request.SelectedCardIds.Count == 0)
            return new List<JsonObject>();

        var storedIds = request.SelectedCardIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToList();
        var snapshotCards = selectedCards.OfType<JsonObject>().ToList();
        var snapshotIds = snapshotCards
            .Select(card => GetNodeString(card["cardId"])?.Trim() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        return storedIds.Count > 0 &&
               storedIds.Count == snapshotIds.Count &&
               storedIds.SequenceEqual(snapshotIds, StringComparer.OrdinalIgnoreCase)
            ? snapshotCards
            : new List<JsonObject>();
    }

    private static string DescribeShiningForgePropertyLabel(JsonObject property)
    {
        var explicitName = GetNodeString(property["name"]);
        if (!string.IsNullOrWhiteSpace(explicitName))
            return explicitName!;

        var propertyId = GetNodeString(property["propertyId"]);
        if (!string.IsNullOrWhiteSpace(propertyId))
            return DescribeShiningForgeStat(propertyId);

        var stat = GetNodeString(property["stat"]);
        return string.IsNullOrWhiteSpace(stat) ? "свойство" : DescribeShiningForgeStat(stat);
    }

    private static string DescribeShiningForgeStat(string? stat) =>
        (stat ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "social" => "социальное влияние",
            "resource" => "ресурсы",
            "memory" => "память",
            "route" => "путь",
            "lore" => "знание",
            "relic" => "реликвия",
            "survival" => "выживание",
            "descent" => "нисхождение",
            _ => HumanizeProtocolToken(stat)
        };

    private static void AppendShiningJsonPayloadBlock(List<string> lines, string title, JsonNode? payload)
    {
        if (payload == null)
            return;

        var formatted = FormatShiningJsonNodeForDisplay(payload).ToList();
        if (formatted.Count == 0)
            return;

        lines.Add($"  {title}:");
        foreach (var line in formatted)
            lines.Add($"    [dim]{line}[/]");
    }

    private static JsonObject? FindShiningHallNode(JsonObject shiningRoot, string? hallId)
    {
        if (shiningRoot["halls"] is not JsonArray halls || string.IsNullOrWhiteSpace(hallId))
            return null;

        return halls.OfType<JsonObject>()
            .FirstOrDefault(hall => string.Equals(GetNodeString(hall["hallId"]), hallId, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveShiningBlessingCardLabel(JsonObject shiningRoot, string? cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return "?";

        if (shiningRoot["preparedIncarnationPackage"] is JsonObject preparedPackage)
        {
            var selected = GetConsistentPreparedPackageCards(preparedPackage)
                .FirstOrDefault(card => string.Equals(GetNodeString(card["cardId"]), cardId, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(GetNodeString(selected?["displayName"])))
                return GetNodeString(selected?["displayName"])!;
        }

        if (shiningRoot["gates"] is JsonObject gates)
        {
            foreach (var arrayName in new[] { "availableBlessingCards", "allCandidateBlessingCards" })
            {
                if (gates[arrayName] is not JsonArray cards)
                    continue;

                var card = cards.OfType<JsonObject>()
                    .FirstOrDefault(entry => string.Equals(GetNodeString(entry["cardId"]), cardId, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(GetNodeString(card?["displayName"])))
                    return GetNodeString(card?["displayName"])!;
            }
        }

        return cardId;
    }

    private static int CountResidentsInFaction(JsonObject? residentRoot, string? factionId)
    {
        if (residentRoot?["entries"] is not JsonArray entries || string.IsNullOrWhiteSpace(factionId))
            return 0;

        return entries.OfType<JsonObject>()
            .Count(entry => string.Equals(GetNodeString(entry["shiningFactionId"]), factionId, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountAscendedShiningResidents(JsonObject? residentRoot)
    {
        if (residentRoot?["entries"] is not JsonArray entries)
            return 0;

        return entries.OfType<JsonObject>()
            .Count(entry => string.Equals(GetNodeString(entry["ascensionState"]), ShiningAbodeState.AscensionStateAscended, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildStableHeadActorReceiptLabel(string? explicitLabel, string? headActorType, string? headActorId)
    {
        if (!string.IsNullOrWhiteSpace(explicitLabel))
            return explicitLabel!;

        if (string.IsNullOrWhiteSpace(headActorType) ||
            string.Equals(headActorType, "vacant", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(headActorId) ||
            string.Equals(headActorId, "vacant", StringComparison.OrdinalIgnoreCase))
        {
            return "вакантно";
        }

        return headActorType.Trim().ToLowerInvariant() switch
        {
            "resident" => $"резидент {headActorId}",
            "guardian" => $"хранитель {headActorId}",
            "player_soul" => "душа игрока",
            "radiant_actor" => $"светозарный актор {headActorId}",
            _ => $"{headActorType}:{headActorId}"
        };
    }

    private static string BuildHeadActorLabel(string? headActorType, string? headActorId, JsonObject? residentRoot = null, JsonObject? guardiansRoot = null, JsonObject? shiningRoot = null)
    {
        if (string.IsNullOrWhiteSpace(headActorType) ||
            string.Equals(headActorType, "vacant", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(headActorId) ||
            string.Equals(headActorId, "vacant", StringComparison.OrdinalIgnoreCase))
        {
            return "вакантно";
        }

        if (string.Equals(headActorType, "resident", StringComparison.OrdinalIgnoreCase))
        {
            var residentName = ResolveResidentLabel(residentRoot, headActorId);
            var label = string.IsNullOrWhiteSpace(residentName) ? headActorId : residentName;
            return $"резидент {label} [resident:{headActorId}]";
        }

        if (string.Equals(headActorType, "guardian", StringComparison.OrdinalIgnoreCase))
        {
            var guardian = ResolveGuardianObject(guardiansRoot, headActorId);
            var guardianName = GetNodeString(guardian?["canonicalName"]) ??
                               GetNodeString(guardian?["manifestation"]?["currentDisplayName"]) ??
                               headActorId;
            var guardianLabel = PlayerGuardianFoundationState.IsPlayerFoundedGuardian(guardian)
                ? "основанный хранитель"
                : "хранитель";
            var label = string.IsNullOrWhiteSpace(guardianName) ? headActorId : guardianName;
            return $"{guardianLabel} {label} [guardian:{headActorId}]";
        }

        if (string.Equals(headActorType, ShiningAbodeState.HeadActorTypePlayerSoul, StringComparison.OrdinalIgnoreCase))
            return "душа игрока [player_soul:player_soul]";

        if (string.Equals(headActorType, ShiningAbodeState.HeadActorTypeRadiantActor, StringComparison.OrdinalIgnoreCase))
        {
            var actorName = ResolveShiningPoliticalActorLabel(shiningRoot, headActorId);
            var label = string.IsNullOrWhiteSpace(actorName) ? headActorId : actorName;
            return $"светозарный актор {label} [radiant_actor:{headActorId}]";
        }

        return $"{headActorType}:{headActorId}";
    }

    private static string? ResolveShiningPoliticalActorLabel(JsonObject? shiningRoot, string? actorId)
    {
        if (shiningRoot?["shiningPoliticalActors"] is not JsonArray actors || string.IsNullOrWhiteSpace(actorId))
            return actorId;

        var actor = actors.OfType<JsonObject>()
            .FirstOrDefault(entry => string.Equals(GetNodeString(entry["actorId"]), actorId, StringComparison.OrdinalIgnoreCase));
        return GetNodeString(actor?["displayName"]) ?? actorId;
    }

    private static string BuildHeadActorBinding(string? headActorType, string? headActorId)
    {
        var normalizedType = string.IsNullOrWhiteSpace(headActorType) ? "vacant" : headActorType.Trim();
        var normalizedId = string.IsNullOrWhiteSpace(headActorId) ? "vacant" : headActorId.Trim();
        return $"{normalizedType}:{normalizedId}";
    }

    private static JsonNode BuildResidentSnapshotLabelNode(JsonObject? residentRoot, string? residentId)
    {
        var stableResidentId = residentId ?? string.Empty;
        var residentName = ResolveResidentLabel(residentRoot, stableResidentId);
        return JsonValue.Create(string.IsNullOrWhiteSpace(residentName) ? stableResidentId : $"{residentName} ({stableResidentId})");
    }

    private static string? ResolveResidentLabel(JsonObject? residentRoot, string? residentId)
    {
        if (residentRoot?["entries"] is not JsonArray entries || string.IsNullOrWhiteSpace(residentId))
            return residentId;

        var resident = entries.OfType<JsonObject>()
            .FirstOrDefault(entry => string.Equals(GetNodeString(entry["residentId"]), residentId, StringComparison.OrdinalIgnoreCase));
        return GetNodeString(resident?["residentName"]) ?? residentId;
    }

    private static JsonObject? ResolveGuardianObject(JsonObject? guardiansRoot, string? guardianId)
    {
        if (guardiansRoot == null || string.IsNullOrWhiteSpace(guardianId))
            return null;

        if (guardiansRoot["activeGuardian"] is JsonObject activeGuardian &&
            string.Equals(GetNodeString(activeGuardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase))
        {
            return activeGuardian;
        }

        if (guardiansRoot["guardians"] is not JsonArray guardians)
            return null;

        return guardians.OfType<JsonObject>()
            .FirstOrDefault(entry => string.Equals(GetNodeString(entry["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));
    }

    private static string DescribeShiningAvailability(string? availability) =>
        (availability ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "active" => "активна",
            "sealed" => "запечатана",
            "sealed_until_next_ascension" => "запечатана до следующего восхождения",
            "dormant" => "дремлет",
            _ => availability ?? "?"
        };

    private static string DescribeShiningResolutionStatus(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "accepted" => "принято",
            "ready" => "готово",
            "rejected" => "отклонено",
            "refused" => "отклонено",
            "cancelled" => "отменено",
            _ => status ?? "?"
        };

    private static string DescribeShiningResolutionReason(string? reason)
    {
        var normalized = (reason ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        return normalized switch
        {
            "founding_accepted" => "основание принято",
            "accepted_by_target_faction" => "целевая фракция приняла переход",
            "recognized_succession" => "преемственность признана",
            "package_frozen_for_next_life" => "набор новой жизни зафиксирован",
            "project_completed" => "проект завершён",
            "invested" => "вложение принято",
            "refused" => "получен отказ",
            "gates_opened" => "врата раскрыты",
            "forge_reshape_accepted" => "перековка формы принята",
            "forge_retune_accepted" => "перенастройка свойства принята",
            "shining_gacha_resolved" => "сияющий призыв завершён",
            _ when normalized.StartsWith("support_project_archived_", StringComparison.OrdinalIgnoreCase) =>
                $"архивный исход поддержки проекта №{normalized["support_project_archived_".Length..]}",
            _ when normalized.StartsWith("realignment_archive_reason_", StringComparison.OrdinalIgnoreCase) =>
                $"архивный исход перехода резидента №{normalized["realignment_archive_reason_".Length..]}",
            _ when normalized.StartsWith("founding_archive_reason_", StringComparison.OrdinalIgnoreCase) =>
                $"архивный исход основания фракции №{normalized["founding_archive_reason_".Length..]}",
            _ when normalized.StartsWith("leadership_archive_reason_", StringComparison.OrdinalIgnoreCase) =>
                $"архивный исход смены главы №{normalized["leadership_archive_reason_".Length..]}",
            _ => "каноническая причина решения зафиксирована"
        };
    }

    private static string DescribeShiningCoreActionLabel(string? actionType) =>
        (actionType ?? string.Empty).Trim() switch
        {
            ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction => "Открытие нативной фракции",
            ShiningCoreActionRequestState.ActionTypeInvestInFaction => "Инвестиция во фракцию",
            ShiningCoreActionRequestState.ActionTypeCompleteProject => "Завершение проекта",
            ShiningCoreActionRequestState.ActionTypeSupportProject => "Поддержка проекта",
            ShiningCoreActionRequestState.ActionTypeUnsupportProject => "Снятие поддержки проекта",
            ShiningCoreActionRequestState.ActionTypeRetireProject => "Отправка проекта в историю",
            ShiningCoreActionRequestState.ActionTypeOpenGates => "Открытие Врат",
            ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage => "Подготовка новой жизни",
            ShiningCoreActionRequestState.ActionTypePullRelicGacha => "Сияющая гача реликвии",
            ShiningCoreActionRequestState.ActionTypeForgeRelicReshape => "Перековка формы реликвии",
            ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty => "Перенастройка свойства реликвии",
            ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand => "Усиление свойства реликвии",
            ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho => "Стабилизация эха реликвии",
            ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity => "Возвышение редкости реликвии",
            _ => actionType ?? "действие"
        };

    private static string DescribeShiningRealignmentMode(string? mode) =>
        (mode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "accepted_transfer" => "согласованный переход",
            "refused_transfer" => "отклонённый переход",
            "departure_to_neutral" or "departure_only" => "уход в нейтраль",
            _ => mode ?? "?"
        };

    private static string DescribeShiningLeadershipMode(string? mode) =>
        (mode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "peaceful_succession" => "мирная преемственность",
            "abdication" => "отречение",
            "revolt" => "мятеж",
            _ => mode ?? "?"
        };

    private static string DescribeShiningLeadershipState(string? state) =>
        (state ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "secure" => "власть устойчива",
            "contested" => "власть оспаривается",
            "vacant" => "место главы вакантно",
            _ => state ?? "?"
        };

    private static string DescribeShiningResidentRole(string? role) =>
        (role ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "archive_support" => "архивная поддержка",
            "forge_support" => "кузнечная поддержка",
            "social_support" => "социальная поддержка",
            "resource_support" => "ресурсная поддержка",
            "descent_support" => "поддержка нисхождения",
            _ => string.IsNullOrWhiteSpace(role) ? "не определена" : role
        };

    private static string DescribeShiningFactionLoyaltyTier(string? tier) =>
        (tier ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "alienated" => "отчуждённый",
            "uncertain" => "сомневающийся",
            "attached" => "привязанный",
            "devoted" => "преданный",
            "steadfast" => "непоколебимый",
            _ => tier ?? "?"
        };

    private static string DescribeShiningFactionRealignmentState(string? state) =>
        (state ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "settled" => "устойчив",
            "wavering" => "колеблется",
            "restless" => "беспокоен",
            "considering_realignment" => "задумывается о переходе",
            "ready_to_realign" => "готов к переходу",
            _ => state ?? "?"
        };

    private static string DescribeShiningPoliticalStatus(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "head" => "глава",
            "former_head" => "бывший глава",
            "claimant" => "претендент",
            "elder" => "старейшина",
            "retired" => "удалился от дел",
            _ => status ?? "?"
        };

    private static string DescribeShiningHallServiceTag(string? tag) =>
        (tag ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "social" => "социальная поддержка",
            "lore" => "знание",
            "resource" => "ресурсы",
            "memory" => "память",
            "descent" => "нисхождение",
            "relic" => "реликты",
            _ => tag ?? "?"
        };

    private static string DescribeShiningProjectArchetype(string? archetype) =>
        (archetype ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "revelation" => "откровение",
            "accord" => "согласие",
            "provision" => "снабжение",
            "remembrance" => "память",
            "refinement" => "очищение",
            "passage" => "переход",
            "warding" => "защита",
            "subversion" => "подрыв",
            _ => archetype ?? "?"
        };

    private static string DescribeShiningProjectStatus(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "active" => "активен",
            "completed" => "завершён",
            "retired" => "отправлен в историю",
            _ => status ?? "?"
        };

    private static string DescribeShiningLeadershipHistoryEventType(string? eventType) =>
        (eventType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "abdicated" => "отречение состоялось",
            "succeeded" => "преемник признан",
            "revolted" => "мятеж завершился",
            "refused" => "переход отклонён",
            "vacated" => "место главы освобождено",
            _ => eventType ?? "?"
        };

    private static bool TryReadIntegerNode(JsonNode? node, out int value)
    {
        value = 0;
        if (node is not JsonValue jsonValue)
            return false;

        if (jsonValue.TryGetValue<int>(out value))
            return true;
        if (jsonValue.TryGetValue<long>(out var longValue) &&
            longValue is >= int.MinValue and <= int.MaxValue)
        {
            value = (int)longValue;
            return true;
        }

        return false;
    }

    private static string DescribeShiningOriginType(string? originType) =>
        (originType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "player_founded" => "основана игроком",
            "ascended_guardian" => "восходит к Хранителю",
            "native_radiant" => "рождённая в Сияющей Обители",
            _ => originType ?? "?"
        };

    private static string DescribeShiningEffectFamily(string? family) =>
        (family ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "lore" => "знание",
            "social" => "социальное влияние",
            "resource" => "ресурсы",
            "memory" => "память",
            "descent" => "нисхождение",
            "survival" => "выживание",
            "relic" => "реликт",
            "route" => "путь",
            _ => family ?? "?"
        };

    private string DetermineNextShiningStep(
        JsonObject shiningRoot,
        int coreRequestCount,
        int tradeRequestCount,
        int foundingRequestCount,
        int realignmentRequestCount,
        int leadershipRequestCount,
        string? firstCoreRequestLabel,
        string? firstTradeRequestLabel,
        string? firstPoliticalRequestLabel)
    {
        if (shiningRoot["preparedIncarnationPackage"] is JsonObject)
            return "Пакет следующей жизни уже готов: переходи к новому воплощению.";

        if (!string.Equals(GetNodeString(shiningRoot["availability"]), "active", StringComparison.OrdinalIgnoreCase))
            return "Верни Обитель в активное состояние, прежде чем продолжать обычные действия.";

        if (coreRequestCount > 0)
            return $"Сначала дождись accepted/repair для pending core action: {firstCoreRequestLabel ?? $"{coreRequestCount} request(s)"}; она блокирует новые Врата и core actions.";

        if (tradeRequestCount > 0)
            return $"Сначала проверь pending Shining trade: {firstTradeRequestLabel ?? $"{tradeRequestCount} request(s)"}; витрина ждёт canonical receipt/state.";

        if (foundingRequestCount + realignmentRequestCount + leadershipRequestCount > 0)
            return $"Сначала проверь pending Shining politics: {firstPoliticalRequestLabel ?? $"{foundingRequestCount + realignmentRequestCount + leadershipRequestCount} request(s)"}; политический контракт блокирует часть действий.";

        if (shiningRoot["gates"] is JsonObject gates)
        {
            if (GetNodeBool(gates["isStale"]))
                return "Открой Врата заново: текущий набор благословений устарел.";

            if (GetNodeBool(gates["hasOpenDraft"]))
            {
                var selectedCount = (gates["selectedBlessingCardIds"] as JsonArray)?.Count ?? 0;
                return selectedCount > 0
                    ? "Выбранные благословения уже готовы: подготовь новую жизнь."
                    : "Набор Врат уже открыт: выбери благословения для следующей жизни.";
            }
        }

        if ((shiningRoot["factions"] as JsonArray)?.Count is 0)
            return "Сначала запроси открытие нативной фракции.";

        return "Проверь фракции, проекты и Врата: Обитель готова к следующему шагу.";
    }
}
