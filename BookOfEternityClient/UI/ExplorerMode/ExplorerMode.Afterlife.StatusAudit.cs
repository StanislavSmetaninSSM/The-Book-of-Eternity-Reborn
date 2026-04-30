using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private sealed record AfterlifePendingContractDefinition(
        string Path,
        string Label,
        string ClosureHint,
        bool ShiningOnly = false);

    private sealed record AfterlifePendingContractAuditEntry(
        AfterlifePendingContractDefinition Definition,
        JsonObject? Payload,
        int? RequestIndex,
        bool IsMalformed,
        string? Error);

    private static readonly AfterlifePendingContractDefinition[] AfterlifePendingContractDefinitions =
    {
        new(AfterlifeArchiveActionState.ConsultationRequestPath, "Архивная консультация", "archiveActionResolutions + soul_state.afterlifeArchive.actionReceipts[]"),
        new(AfterlifeArchiveActionState.ProjectFuelRequestPath, "Подпитка проекта Архивом", "archiveActionResolutions + allowed project/log effect"),
        new(GuardianAbodeOfferingState.PendingRequestPath, "Подношение Обители", "guardianPowerEvents.reasonType=offering; ink_feathers additionally require output/ink_feather_action_result.json"),
        new(GuardianTradeRequestState.PendingRequestPath, "Торговая витрина Хранителя", "UpdateGuardians + guardians[].tradeInventory + tradeInventoryReceipts[]"),
        new(PlayerGuardianFoundationState.PendingRequestPath, "Основание собственного Хранителя", "UpdateGuardians.create + guardians/activeGuardian + playerGuardianFoundationHistory"),
        new(SystemGuardianLibraryService.AttractionRequestPath, "Притяжение извечного Хранителя", "UpdateGuardians + guardians/activeGuardian + chaosSeaNavigation or explicit client cancellation"),
        new(AfterlifeReturnGuardService.GuardPath, "Post-life return guard", "client-owned protection guard; GM must not clear or bypass it"),
        new(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, "Состав резидентов Обители", "UpdateGuardianAbodeResidents + UpdateGuardianAbodeResidentRosterReceipts"),
        new(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, "Разговор/история резидента Обители", "residentInteractionLogUpdates or resident history log + matching interaction receipts"),
        new(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, "Переход резидента между Обителями", "UpdateGuardianAbodeResidentTransferReceipts + source/target resident state"),
        new(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, "Манифестация резидента в следующей жизни", "MortalWorldProfile-only closure after bootstrap; valid files are preserved in afterlife, malformed files require repair"),
        new(ActorSocialInteractionRequestState.PendingGuardianRequestPath, "Социальный запрос к Хранителю", "guardianSocialJournalUpdates with matching requestId/guardianId/interactionType"),
        new(ShiningCoreActionRequestState.PendingActionsRequestPath, "Core-действие Сияющей Обители", "shining_abode_state.coreActionReceipts[] + exact canonical state projection", ShiningOnly: true),
        new(ShiningTradeRequestState.PendingRequestsPath, "Торговая витрина Сияющей фракции", "factions[].tradeInventory + tradeInventoryReceipts[]", ShiningOnly: true),
        new(ShiningFactionRequestState.PendingFoundingsRequestPath, "Основание сияющей фракции", "halls[]/factions[] + factionFoundingReceipts[]", ShiningOnly: true),
        new(ShiningFactionRequestState.PendingRealignmentsRequestPath, "Переход сияющего резидента", "guardian_abode_residents.json resident faction fields + factionRealignmentReceipts[]", ShiningOnly: true),
        new(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath, "Смена власти сияющей фракции", "faction.leadership + leadershipReceipts[] + leadershipHistory[]", ShiningOnly: true)
    };

    private async Task ShowAfterlifeDetailedStatusAsync()
    {
        await _stateManager.RefreshGameStateAsync();
        var soulRoot = await ReadJsonObjectForAfterlifeStatusAsync("game_state/meta/soul_state.json");
        var guardiansRoot = await ReadJsonObjectForAfterlifeStatusAsync("game_state/meta/guardians.json");
        var residentsRoot = await ReadJsonObjectForAfterlifeStatusAsync(GuardianAbodeResidentState.StatePath);
        var returnGuardRaw = await _fs.ReadFileAsync(AfterlifeReturnGuardService.GuardPath);
        var shiningContext = await LoadShiningContextAsync();
        var pendingLines = await BuildAfterlifePendingContractAuditLinesAsync(includeShining: true, includeFullPayload: true);

        var lines = new List<string>
        {
            "[bold cyan]Полный статус загробного цикла[/]",
            "",
            "[bold]Realm и душа:[/]",
            $"  • Realm: [white]{Markup.Escape(_stateManager.CurrentState.CurrentRealm)}[/]",
            $"  • Turn: [white]{_stateManager.CurrentState.TurnNumber}[/]",
            $"  • Душа: [white]{Markup.Escape(GetNodeString(soulRoot?["soulName"]) ?? _stateManager.CurrentState.CharacterName ?? "не указана")}[/]",
            $"  • Инкарнация: [white]{GetNodeInt(soulRoot?["currentIncarnation"])}[/]",
            "",
            "[bold]Ресурсы души:[/]",
            $"  • Чернильные Перья: [gold1]{CurrentInkFeathersForPreview(soulRoot)}[/]",
            $"  • Просветление: [white]{Markup.Escape(GetNodeString(soulRoot?["enlightenment"]?["currentTier"]) ?? GetNodeString(soulRoot?["enlightenmentTier"]) ?? "не указано")}[/]",
            $"  • Реликвии души: stored [white]{(soulRoot?["soulRelics"]?["stored"] as JsonArray)?.Count ?? 0}[/], equipped [white]{(soulRoot?["soulRelics"]?["equipped"] as JsonArray)?.Count ?? 0}[/]",
            $"  • Архив души: stored [white]{(soulRoot?["afterlifeArchive"]?["stored"] as JsonArray)?.Count ?? 0}[/], receipts [white]{(soulRoot?["afterlifeArchive"]?["actionReceipts"] as JsonArray)?.Count ?? 0}[/]"
        };

        AppendChaosSeaStatusLines(lines, guardiansRoot, residentsRoot, returnGuardRaw);
        AppendShiningStatusLines(lines, shiningContext);

        lines.Add("");
        lines.AddRange(pendingLines);
        lines.Add("");
        lines.Add("[bold]Куда смотреть дальше:[/]");
        lines.Add("  • /chaos_sea — Хранители, Обители, pending contracts и Chaos Sea-only действия.");
        lines.Add("  • /feathers, /afterlife_archive, /afterlife_inbox, /guardian_projects — детальные audit-панели ресурсов, архива, ответов GM и проектов.");
        lines.Add("  • /shining_abode, /shining_politics — Сияющая Обитель, Врата, trade/forge, фракции, резиденты, проекты и политические receipts.");

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 📊 Afterlife Status ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WaitForKey();
    }

    private static void AppendChaosSeaStatusLines(List<string> lines, JsonObject? guardiansRoot, JsonObject? residentsRoot, string? returnGuardRaw)
    {
        lines.Add("");
        lines.Add("[bold]Море Хаоса:[/]");
        if (guardiansRoot == null)
        {
            lines.Add("  • guardians.json не найден или повреждён.");
            return;
        }

        var activeGuardianId = GetNodeString(guardiansRoot["activeGuardian"]?["guardianId"]) ?? string.Empty;
        var currentAbodeId = GetNodeString(guardiansRoot["chaosSeaNavigation"]?["currentAbodeId"]) ?? string.Empty;
        var activeGuardian = (guardiansRoot["guardians"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(guardian => string.Equals(GetNodeString(guardian["guardianId"]), activeGuardianId, StringComparison.OrdinalIgnoreCase));
        var guardianName = activeGuardian == null ? activeGuardianId : GuardianManifestation.GetDisplayName(activeGuardian);
        var currentAbodeName = GetNodeString(activeGuardian?["abode"]?["name"]) ?? currentAbodeId;
        lines.Add($"  • Активный Хранитель: [white]{Markup.Escape(string.IsNullOrWhiteSpace(guardianName) ? "не выбран" : guardianName)}[/] [dim]({Markup.Escape(activeGuardianId)})[/]");
        lines.Add($"  • Текущая Обитель: [white]{Markup.Escape(string.IsNullOrWhiteSpace(currentAbodeName) ? "не выбрана" : currentAbodeName)}[/] [dim]({Markup.Escape(currentAbodeId)})[/]");
        lines.Add($"  • Известных Хранителей: [white]{(guardiansRoot["guardians"] as JsonArray)?.Count ?? 0}[/]");
        lines.Add($"  • Резидентов Обителей: [white]{(residentsRoot?["entries"] as JsonArray)?.Count ?? 0}[/]");
        if (activeGuardian?["gachaSystem"] is JsonObject gacha)
            lines.Add($"  • Гача активного Хранителя: [white]{Math.Max(0, GetNodeInt(gacha["chargesPerReturn"]) - GetNodeInt(gacha["chargesUsedThisReturn"]))}[/]/[white]{GetNodeInt(gacha["chargesPerReturn"])}[/] попыток за возвращение.");
        if (activeGuardian?["abodePower"] is JsonObject power)
            lines.Add($"  • Сила текущей Обители: [white]{GetNodeInt(power["currentPower"])}[/] [dim]({Markup.Escape(GetNodeString(power["tier"]) ?? "tier не указан")})[/]");

        if (activeGuardian != null)
        {
            var reputation = GuardianGachaChargeRules.ResolveGuardianReputation(activeGuardian);
            var guardState = AfterlifeReturnGuardService.Classify(returnGuardRaw, out var guard);
            var guardLabel = guardState switch
            {
                AfterlifeReturnGuardSemanticState.ActiveValid => $"protected, remainingTurns={guard?.RemainingProtectedTurns ?? 0}",
                AfterlifeReturnGuardSemanticState.BlockingInvalid => "blocking-invalid guard; fail-closed",
                _ => "not protected"
            };
            var risk = reputation <= -21 ? "[red]ENABLED[/]" : "[green]not enabled[/]";
            lines.Add($"  • Guardian-forced incarnation risk: {risk}; guardianId=[dim]{Markup.Escape(activeGuardianId)}[/], abodeId=[dim]{Markup.Escape(currentAbodeId)}[/], reputation=[white]{reputation}[/], threshold=[white]<= -21[/], returnGuard=[white]{Markup.Escape(guardLabel)}[/].");
        }
    }

    private static void AppendShiningStatusLines(List<string> lines, ShiningContext? context)
    {
        lines.Add("");
        lines.Add("[bold gold1]Сияющая Обитель:[/]");
        if (context == null)
        {
            lines.Add("  • shining_abode_state.json пока отсутствует или повреждён.");
            return;
        }

        var root = context.Root;
        lines.Add($"  • Доступность: [white]{Markup.Escape(DescribeShiningAvailability(GetNodeString(root["availability"])))}[/]");
        lines.Add($"  • Radiance: [yellow]{GetNodeInt(root["radiance"]?["experience"])} XP[/] [dim](tier {GetNodeInt(root["radiance"]?["tier"])})[/]");
        lines.Add($"  • Light Sparks: [gold1]{GetNodeInt(root["lightSparks"])}[/]");
        lines.Add($"  • Shining gacha: [white]{ShiningAbodeState.GetRemainingShiningGachaCharges(root)}[/]/[white]{GetNodeInt(root["gachaSystem"]?["chargesPerReturn"])}[/] [dim]({BuildShiningReturnCycleStatusLabel(root)})[/]");
        lines.Add($"  • Фракций: [white]{(root["factions"] as JsonArray)?.Count ?? 0}[/], залов: [white]{(root["halls"] as JsonArray)?.Count ?? 0}[/], ascended residents: [white]{CountAscendedShiningResidents(context.ResidentRoot)}[/]");
        lines.Add($"  • Receipts: coreAction={(root["coreActionReceipts"] as JsonArray)?.Count ?? 0}, founding={CountNestedReceipts(root, "factionFoundingReceipts")}, realignment={CountNestedReceipts(root, "factionRealignmentReceipts")}, leadership={CountNestedReceipts(root, "leadershipReceipts")}, trade={CountNestedReceipts(root, ShiningTradeRequestState.ReceiptsProperty)}.");
        if (root["gates"] is JsonObject gates)
        {
            lines.Add($"  • Врата: draftVersion [white]{GetNodeInt(gates["draftVersion"])}[/], open={GetNodeBool(gates["hasOpenDraft"])}, stale={GetNodeBool(gates["isStale"])}, availableCards={(gates["availableBlessingCards"] as JsonArray)?.Count ?? 0}, selected={(gates["selectedBlessingCardIds"] as JsonArray)?.Count ?? 0}, rerolls={GetNodeInt(gates["rerollsRemaining"])}.");
            AppendSelectedShiningCardStatusLines(lines, gates);
        }
        if (root["preparedIncarnationPackage"] is JsonObject package)
            lines.Add($"  • Prepared package: draftVersion [white]{GetNodeInt(package["generatedFromDraftVersion"])}[/], selectedCards={(package["selectedCards"] as JsonArray)?.Count ?? 0}, preparedAtTurn={GetNodeInt(package["preparedAtTurn"])}.");

        if (root["factions"] is JsonArray factions)
        {
            foreach (var faction in factions.OfType<JsonObject>().OrderByDescending(faction => GetNodeInt(faction["factionStrength"])))
            {
                var factionId = GetNodeString(faction["factionId"]) ?? string.Empty;
                var name = GetNodeString(faction["charter"]?["factionName"]) ?? factionId;
                var strength = GetNodeInt(faction["factionStrength"]);
                var projects = faction["projects"] as JsonArray;
                var supported = projects?.OfType<JsonObject>().Count(project => GetNodeBool(project["isSupported"])) ?? 0;
                var memberCount = CountResidentsInFaction(context.ResidentRoot, factionId);
                lines.Add($"  • {Markup.Escape(name)} [dim]({Markup.Escape(factionId)})[/]: strength={strength}, tradeTier={ShiningAbodeState.GetTradeTier(strength)}, slots={ShiningAbodeState.GetTradeStockItemCount(faction, context.ResidentRoot)}, rarity={Markup.Escape(ShiningAbodeState.GetTradeRarityCeiling(strength))}, service x{ShiningAbodeState.GetServiceMultiplier(strength):0.00}, residents={memberCount}, projects={projects?.Count ?? 0}, supported={supported}.");
            }
        }
    }

    private static int CountNestedReceipts(JsonObject root, string receiptProperty)
    {
        var total = 0;
        if (root["factions"] is JsonArray factions)
        {
            total += factions.OfType<JsonObject>()
                .Sum(faction => (faction[receiptProperty] as JsonArray)?.Count ?? 0);
        }

        total += (root[receiptProperty] as JsonArray)?.Count ?? 0;
        return total;
    }

    private static void AppendSelectedShiningCardStatusLines(List<string> lines, JsonObject gates)
    {
        var selected = (gates["selectedBlessingCardIds"] as JsonArray)?
            .OfType<JsonValue>()
            .Select(value => value.TryGetValue<string>(out var id) ? id : string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Take(8)
            .ToList() ?? new List<string>();
        if (selected.Count == 0)
            return;

        lines.Add("  • Selected blessing cards:");
        foreach (var cardId in selected)
        {
            var card = (gates["availableBlessingCards"] as JsonArray)?.OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(GetNodeString(item["cardId"]), cardId, StringComparison.OrdinalIgnoreCase));
            var label = GetNodeString(card?["displayName"]) ?? cardId;
            var rarity = GetNodeString(card?["rarity"]) ?? "unknown";
            var family = GetNodeString(card?["effectFamily"]) ?? "unknown";
            lines.Add($"    - {Markup.Escape(label)} [dim]({Markup.Escape(cardId)}; {Markup.Escape(rarity)}; {Markup.Escape(family)})[/]");
        }
    }

    private async Task<List<string>> BuildAfterlifePendingContractAuditLinesAsync(bool includeShining, bool includeFullPayload)
    {
        var entries = await ReadAfterlifePendingContractAuditEntriesAsync(includeShining);
        var lines = new List<string>
        {
            "[bold]Активные pending/control contracts:[/]"
        };

        if (entries.Count == 0)
        {
            lines.Add("  • Нет активных pending/control contracts, блокирующих обычный afterlife flow.");
            return lines;
        }

        foreach (var entry in entries)
        {
            var requestLabel = entry.RequestIndex.HasValue ? $"requests[{entry.RequestIndex.Value}]" : "root";
            lines.Add($"  • [white]{Markup.Escape(entry.Definition.Label)}[/] — [dim]{Markup.Escape(entry.Definition.Path)}[/] / {Markup.Escape(requestLabel)}");
            if (entry.IsMalformed)
            {
                lines.Add($"    malformed: [red]{Markup.Escape(entry.Error ?? "unknown parse error")}[/]");
                lines.Add($"    closure/repair: {Markup.Escape(entry.Definition.ClosureHint)}");
                continue;
            }

            if (entry.Payload != null)
            {
                var identity = BuildPendingContractIdentitySummary(entry.Payload);
                lines.Add($"    identity: {Markup.Escape(string.IsNullOrWhiteSpace(identity) ? "fields not found; inspect full payload below" : identity)}");
            }
            lines.Add($"    closure: {Markup.Escape(entry.Definition.ClosureHint)}");

            if (includeFullPayload && entry.Payload != null)
            {
                lines.Add("    full payload:");
                foreach (var payloadLine in entry.Payload.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed).Split('\n'))
                    lines.Add($"      [dim]{Markup.Escape(payloadLine.TrimEnd('\r'))}[/]");
            }
        }

        return lines;
    }

    private async Task<List<AfterlifePendingContractAuditEntry>> ReadAfterlifePendingContractAuditEntriesAsync(bool includeShining)
    {
        var result = new List<AfterlifePendingContractAuditEntry>();
        foreach (var definition in AfterlifePendingContractDefinitions)
        {
            if (definition.ShiningOnly && !includeShining)
                continue;
            if (!_fs.FileExists(definition.Path))
                continue;

            var raw = await _fs.ReadFileAsync(definition.Path);
            if (string.IsNullOrWhiteSpace(raw))
            {
                result.Add(new AfterlifePendingContractAuditEntry(definition, null, null, IsMalformed: true, Error: "empty file"));
                continue;
            }

            try
            {
                var node = JsonNode.Parse(raw);
                if (node is JsonObject root && root["requests"] is JsonArray requests)
                {
                    if (requests.Count == 0)
                    {
                        result.Add(new AfterlifePendingContractAuditEntry(definition, root, null, IsMalformed: false, Error: null));
                        continue;
                    }

                    for (var i = 0; i < requests.Count; i++)
                    {
                        result.Add(new AfterlifePendingContractAuditEntry(
                            definition,
                            requests[i] as JsonObject,
                            i,
                            IsMalformed: requests[i] is not JsonObject,
                            Error: requests[i] is JsonObject ? null : "request entry is not an object"));
                    }
                }
                else if (node is JsonObject obj)
                {
                    result.Add(new AfterlifePendingContractAuditEntry(definition, obj, null, IsMalformed: false, Error: null));
                }
                else
                {
                    result.Add(new AfterlifePendingContractAuditEntry(definition, null, null, IsMalformed: true, Error: "root is not an object"));
                }
            }
            catch (Exception ex)
            {
                result.Add(new AfterlifePendingContractAuditEntry(definition, null, null, IsMalformed: true, Error: ex.GetType().Name));
            }
        }

        return result;
    }

    private static string BuildPendingContractIdentitySummary(JsonObject payload)
    {
        var keys = new[]
        {
            "requestId", "actionType", "interactionType", "requestedMode", "requestMode", "offeringType", "reasonType",
            "guardianId", "guardianName", "abodeId", "abodeName", "residentId", "residentName",
            "sourceGuardianId", "sourceAbodeId", "targetGuardianId", "targetAbodeId", "targetProjectId",
            "factionId", "factionName", "sourceFactionId", "targetFactionId", "proposedFactionId", "proposedHallId",
            "projectId", "projectDisplayName", "relicId", "relicName", "archiveId", "archiveEntryType",
            "tradeCycleId", "returnCycleId", "createdAtTurn", "createdAtUtc",
            "costFeathers", "costLightSparks", "quotedCostFeathers", "quotedCostLightSparks", "inkFeathersOffered",
            "powerGain", "derivedTradeTier", "derivedTradeSlotCount", "derivedRarityCeiling", "derivedServiceMultiplier"
        };

        var parts = new List<string>();
        foreach (var key in keys)
        {
            var value = FormatPendingIdentityValue(payload[key]);
            if (!string.IsNullOrWhiteSpace(value))
                parts.Add($"{key}={value}");
        }

        return string.Join(", ", parts);
    }

    private static string? FormatPendingIdentityValue(JsonNode? node)
    {
        if (node == null)
            return null;

        if (node is JsonArray array)
        {
            var values = array
                .Select(FormatPendingIdentityValue)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(8)
                .ToArray();
            return values.Length == 0 ? null : $"[{string.Join(", ", values)}]";
        }

        if (node is JsonObject)
            return null;

        var value = GetNodeString(node);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private async Task<JsonObject?> ReadJsonObjectForAfterlifeStatusAsync(string path)
    {
        var raw = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonNode.Parse(raw) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

}
