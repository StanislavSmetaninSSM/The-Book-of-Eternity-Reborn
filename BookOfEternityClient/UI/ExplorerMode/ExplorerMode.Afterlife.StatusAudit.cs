using System.Text;
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
        string? Error,
        string? RawPayload = null);

    private sealed record AfterlifeStatusJsonReadResult(
        string Path,
        JsonObject? Root,
        string? RawPayload,
        string? Error);

    private static readonly AfterlifePendingContractDefinition[] AfterlifePendingContractDefinitions =
    {
        new(AfterlifeArchiveActionState.ConsultationRequestPath, "Архивная консультация", "archiveActionResolutions + soul_state.afterlifeArchive.actionReceipts[]"),
        new(AfterlifeArchiveActionState.ProjectFuelRequestPath, "Подпитка проекта Архивом", "archiveActionResolutions + разрешённый эффект проекта/журнала"),
        new(GuardianAbodeOfferingState.PendingRequestPath, "Подношение Обители", "guardianPowerEvents.reasonType=offering; для ink_feathers дополнительно нужен output/ink_feather_action_result.json"),
        new(GuardianTradeRequestState.PendingRequestPath, "Торговая витрина Хранителя", "UpdateGuardians + guardians[].tradeInventory + tradeInventoryReceipts[]"),
        new(PlayerGuardianFoundationState.PendingRequestPath, "Основание собственного Хранителя", "UpdateGuardians.create + guardians/activeGuardian + playerGuardianFoundationHistory"),
        new(SystemGuardianLibraryService.AttractionRequestPath, "Притяжение извечного Хранителя", "Только Море Хаоса: UpdateGuardians + guardians/activeGuardian + chaosSeaNavigation; вне Моря Хаоса сохранять как только ремонт в неверной области или чистить явной клиентской отменой"),
        new(AfterlifeReturnGuardService.GuardPath, "Защита возвращения после жизни", "клиентская защита; ГМ не должен очищать или обходить её"),
        new(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, "Состав резидентов Обители", "UpdateGuardianAbodeResidents + UpdateGuardianAbodeResidentRosterReceipts"),
        new(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, "Разговор/история резидента Обители", "residentInteractionLogUpdates или журнал истории резидента + совпадающие квитанции взаимодействия"),
        new(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, "Переход резидента между Обителями", "UpdateGuardianAbodeResidentTransferReceipts + состояние исходного/целевого резидента"),
        new(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, "Манифестация резидента в следующей жизни", "закрывается только через MortalWorldProfile после bootstrap; валидные файлы сохраняются в посмертии, повреждённые требуют ремонта"),
        new(ActorSocialInteractionRequestState.PendingGuardianRequestPath, "Социальный запрос к Хранителю", "guardianSocialJournalUpdates со совпадающими requestId/guardianId/interactionType"),
        new(ActorSocialInteractionRequestState.PendingNpcRequestPath, "Социальный запрос NPC из смертного мира", "только ремонт в неверной области посмертия; сохранить полные данные и не закрывать через посмертие"),
        new(NpcTradeRequestState.PendingRequestPath, "Торговый запрос NPC из смертного мира", "только ремонт в неверной области посмертия; сохранить полные данные и не создавать посмертные торговые квитанции"),
        new(CraftRequestState.PendingRequestPath, "Ремесленный запрос из смертного мира", "только ремонт в неверной области посмертия; сохранить intent/recipeId и не закрывать через посмертие"),
        new(TrainingRequestState.PendingRequestPath, "Витрина обучения", "GM обновляет trainingShowcase/mentorTrainingShowcase с точным sourceActorSnapshotHash; покупку и квитанции пишет клиент"),
        new(ShiningCoreActionRequestState.PendingActionsRequestPath, "Основное действие Сияющей Обители", "shining_abode_state.coreActionReceipts[] + точная каноническая проекция состояния", ShiningOnly: true),
        new(ShiningTradeRequestState.PendingRequestsPath, "Торговая витрина Сияющей фракции", "factions[].tradeInventory + tradeInventoryReceipts[]", ShiningOnly: true),
        new(ShiningFactionRequestState.PendingFoundingsRequestPath, "Основание сияющей фракции", "halls[]/factions[] + factionFoundingReceipts[]", ShiningOnly: true),
        new(ShiningFactionRequestState.PendingRealignmentsRequestPath, "Переход сияющего резидента", "guardian_abode_residents.json resident faction fields + factionRealignmentReceipts[]", ShiningOnly: true),
        new(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath, "Смена власти сияющей фракции", "faction.leadership + leadershipReceipts[] + leadershipHistory[]", ShiningOnly: true),
        new(SourceOfLightCapstoneState.PendingRequestPath, "Источник Света", "sourceOfLightCapstone + afterlifeCombatProfile.capstones.lightIncarnate + soulRelics.stored[]", ShiningOnly: true),
        new(SarefMainStoryState.PendingWingsInfiltrationPath, "Поиск Крыльев Ангелов", "sarefMainStoryUpdate.mode=reveal_wings/refuse_wings/block_wings + wingsInfiltration.status", ShiningOnly: true)
    };

    private async Task ShowAfterlifeDetailedStatusAsync(bool includeAuditPayloads)
    {
        await _stateManager.RefreshGameStateAsync();
        var soulStateRead = await ReadJsonObjectForAfterlifeStatusResultAsync("game_state/meta/soul_state.json");
        var guardiansStateRead = await ReadJsonObjectForAfterlifeStatusResultAsync("game_state/meta/guardians.json");
        var residentsStateRead = await ReadJsonObjectForAfterlifeStatusResultAsync(GuardianAbodeResidentState.StatePath);
        var shiningStateRead = await ReadJsonObjectForAfterlifeStatusResultAsync(ShiningAbodeState.StatePath);
        var spiritualConflictRead = await ReadJsonObjectForAfterlifeStatusResultAsync(AfterlifeSpiritualConflictState.StatePath);
        var entityProfilesRead = await ReadJsonObjectForAfterlifeStatusResultAsync(AfterlifeEntityProfileState.StatePath);
        var globalFlagsRead = await ReadJsonObjectForAfterlifeStatusResultAsync(AfterlifeGlobalFlagState.StatePath);
        var soulRoot = soulStateRead.Root;
        var guardiansRoot = guardiansStateRead.Root;
        var residentsRoot = residentsStateRead.Root;
        var returnGuardRaw = await _fs.ReadFileAsync(AfterlifeReturnGuardService.GuardPath);
        var shiningContext = await LoadShiningContextAsync();
        var pendingLines = await BuildAfterlifePendingContractAuditLinesAsync(includeShining: true, includeFullPayload: includeAuditPayloads);

        var lines = new List<string>
        {
            includeAuditPayloads
                ? "[bold cyan]Аудит посмертия[/]"
                : "[bold cyan]Статус посмертия[/]",
            "",
            "[bold]Область и душа:[/]",
            $"  • Область: [white]{Markup.Escape(FormatAfterlifeRealmForPlayer(_stateManager.CurrentState.CurrentRealm))}[/]",
            $"  • Ход: [white]{_stateManager.CurrentState.TurnNumber}[/]",
            $"  • Душа: [white]{Markup.Escape(GetNodeString(soulRoot?["soulName"]) ?? _stateManager.CurrentState.CharacterName ?? "не указана")}[/]",
            $"  • Форма души: [white]{Markup.Escape(GetNodeString(soulRoot?["soulFormDescription"]) ?? "не описана")}[/]",
            $"  • Инкарнация: [white]{GetNodeInt(soulRoot?["currentIncarnation"])}[/]",
            "",
            "[bold]Ресурсы души:[/]",
            $"  • Чернильные Перья: [gold1]{CurrentInkFeathersForPreview(soulRoot)}[/]",
            $"  • Просветление: [white]{Markup.Escape(GetNodeString(soulRoot?["enlightenment"]?["currentTier"]) ?? GetNodeString(soulRoot?["enlightenmentTier"]) ?? "не указано")}[/]",
            $"  • Реликвии души: в хранилище [white]{(soulRoot?["soulRelics"]?["stored"] as JsonArray)?.Count ?? 0}[/], экипировано [white]{(soulRoot?["soulRelics"]?["equipped"] as JsonArray)?.Count ?? 0}[/]",
            $"  • Архив души: в хранилище [white]{(soulRoot?["afterlifeArchive"]?["stored"] as JsonArray)?.Count ?? 0}[/], записей о действиях [white]{(soulRoot?["afterlifeArchive"]?["actionReceipts"] as JsonArray)?.Count ?? 0}[/]"
        };

        AppendNextLifePayloadStatusLines(lines, soulRoot, includeAuditPayloads);
        AppendChaosSeaStatusLines(lines, guardiansRoot, residentsRoot, returnGuardRaw);
        AppendShiningStatusLines(lines, shiningContext);
        AppendAfterlifeSpiritualConflictStatusLines(lines, spiritualConflictRead.Root);
        AppendAfterlifeGlobalFlagStatusLines(lines, globalFlagsRead.Root);
        AppendMalformedAfterlifeStateStatusLines(
            lines,
            new[] { soulStateRead, guardiansStateRead, residentsStateRead, shiningStateRead, spiritualConflictRead, entityProfilesRead, globalFlagsRead },
            shiningContext?.ReadIssues,
            includeAuditPayloads);
        await AppendAfterlifeProgressionStatusLinesAsync(lines);

        lines.Add("");
        lines.AddRange(pendingLines);
        lines.Add("");
        lines.Add("[bold]Куда смотреть дальше:[/]");
        if (includeAuditPayloads)
        {
            lines.Add("  • /chaos_sea — Хранители, Обители, ожидающие/контрольные контракты и действия Моря Хаоса.");
            lines.Add("  • /feathers, /afterlife_archive, /afterlife_inbox, /guardian_projects, /guardian_politics — детальные аудит-панели ресурсов, архива, ответов ГМ, проектов и политики Хранителей.");
            lines.Add("  • /shining_abode, /shining_politics — Сияющая Обитель, Врата, торговля/ковка, фракции, резиденты, проекты и политические квитанции.");
            lines.Add("  • Обычная игроковая сводка без служебных данных: /статус.");
        }
        else
        {
            lines.Add("  • /море_хаоса — Хранители, Обители, ожидающие решения и действия Моря Хаоса.");
            lines.Add("  • /перья, /архив_души, /уведомления_загробья, /проекты_хранителей, /политика_хранителей — ресурсы души, Архив, ответы ГМ, проекты и политика Хранителей.");
            lines.Add("  • /сияющая_обитель, /сияющая_политика — Сияющая Обитель, Врата, торговля, ковка, фракции, резиденты и политические решения.");
        }

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 📊 Статус посмертия ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        if (includeAuditPayloads)
        {
            WriteJsonAuditPanel("Полный JSON game_state/meta/soul_state.json", soulRoot, Color.Cyan1);
            WriteJsonAuditPanel("Полный JSON game_state/meta/guardians.json", guardiansRoot, Color.Cyan1);
            WriteJsonAuditPanel("Полный JSON game_state/meta/guardian_abode_residents.json", residentsRoot, Color.Cyan1);
            WriteMalformedAfterlifeStateAuditPanels(
                new[] { soulStateRead, guardiansStateRead, residentsStateRead, shiningStateRead, spiritualConflictRead, entityProfilesRead, globalFlagsRead },
                shiningContext?.ReadIssues);
            if (shiningContext != null)
            {
                WriteJsonAuditPanel("Полный JSON game_state/meta/shining_abode_state.json", CloneShiningJsonForPlayerFacingAudit(shiningContext.Root), Color.Gold1);
                WriteJsonAuditPanel("Полный JSON привязок резидентов Сияющей Обители", shiningContext.ResidentRoot, Color.Gold1);
                WriteJsonAuditPanel("Полный JSON Врат Сияющей Обители", CloneShiningJsonForPlayerFacingAudit(shiningContext.Root["gates"]), Color.Gold1);
                WriteJsonAuditPanel("Полный JSON preparedIncarnationPackage", CloneShiningJsonForPlayerFacingAudit(shiningContext.Root["preparedIncarnationPackage"]), Color.Gold1);
                WriteJsonAuditPanel("Полный JSON coreActionReceipts Сияющей Обители", CloneShiningJsonForPlayerFacingAudit(shiningContext.Root["coreActionReceipts"]), Color.Gold1);
                WriteJsonAuditPanel("Полный JSON gachaSystem Сияющей Обители", CloneShiningJsonForPlayerFacingAudit(shiningContext.Root["gachaSystem"]), Color.Gold1);
                WriteJsonAuditPanel("Полный JSON Сокровищницы Сияющей Обители", CloneShiningJsonForPlayerFacingAudit(shiningContext.Root["treasury"]), Color.Gold1);
            }
            WriteJsonAuditPanel($"Полный JSON {AfterlifeSpiritualConflictState.StatePath}", BuildPlayerFacingCombatConditionAudit(spiritualConflictRead.Root), Color.Cyan1);
            WriteJsonAuditPanel($"Полный JSON {AfterlifeEntityProfileState.StatePath}", entityProfilesRead.Root, Color.Cyan1);
            WriteJsonAuditPanel($"Полный JSON {AfterlifeGlobalFlagState.StatePath}", BuildPlayerFacingAfterlifeGlobalFlagsAudit(globalFlagsRead.Root), Color.Cyan1);
            await WriteAfterlifeProgressionAuditPanelsAsync();
        }
        WaitForKey();
    }

    private static bool IsAfterlifeStatusAuditRequested(string commandRemainder)
    {
        var normalized = commandRemainder.Trim();
        return string.Equals(normalized, "audit", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "аудит", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendAfterlifeSpiritualConflictStatusLines(List<string> lines, JsonObject? conflictRoot)
    {
        lines.Add("");
        lines.Add("[bold]Духовный конфликт посмертия:[/]");
        var active = conflictRoot?["activeConflict"] as JsonObject;
        if (active == null)
        {
            lines.Add("  • Активного духовного конфликта нет.");
            return;
        }

        lines.Add($"  • Формат столкновения: [white]{Markup.Escape(FormatSideModelLabel(AfterlifeSpiritualConflictState.GetNodeString(active["sideModel"])))}[/].");
        lines.Add($"  • Позиция: [white]{Markup.Escape(FormatConflictPositionLabel(AfterlifeSpiritualConflictState.GetNodeString(active["conflictPosition"])))}[/].");
        lines.Add($"  • Напряжение сторон: игрок — [white]{Markup.Escape(FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(active["playerSideStrain"])))}[/], противник — [white]{Markup.Escape(FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(active["oppositionSideStrain"])))}[/].");
        lines.Add($"  • Состояние: [white]{Markup.Escape(FormatResolutionStateLabel(AfterlifeSpiritualConflictState.GetNodeString(active["resolutionState"])))}[/].");
        lines.Add($"  • Записано обменов действиями: [white]{(active["exchangeLog"] as JsonArray)?.Count ?? 0}[/].");
    }

    private static void AppendAfterlifeGlobalFlagStatusLines(List<string> lines, JsonObject? flagsRoot)
    {
        lines.Add("");
        lines.Add("[bold]Глобальные факты посмертия:[/]");
        if (flagsRoot?[AfterlifeGlobalFlagState.FlagsProperty] is not JsonArray flags)
        {
            lines.Add("  • Файл глобальных флагов не найден или пуст.");
            return;
        }

        var visibleFlags = flags.OfType<JsonObject>()
            .Where(flag => string.Equals(AfterlifeGlobalFlagState.GetNodeString(flag["visibility"]), "visible", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var hiddenCount = flags.Count - visibleFlags.Count;
        lines.Add($"  • Видимых глобальных фактов: [white]{visibleFlags.Count}[/]; скрытых: [white]{hiddenCount}[/].");
        foreach (var flag in visibleFlags.Take(5))
        {
            var category = HumanizeAfterlifeStatusToken(AfterlifeGlobalFlagState.GetNodeString(flag["category"]));
            var state = HumanizeAfterlifeStatusToken(AfterlifeGlobalFlagState.GetNodeString(flag["state"]));
            var reason = AfterlifeGlobalFlagState.GetNodeString(flag["reason"]) ?? "без описания";
            lines.Add($"  • [white]{Markup.Escape(reason)}[/] [dim]({Markup.Escape(category)}, {Markup.Escape(state)})[/]");
        }
    }

    private static JsonObject? BuildPlayerFacingAfterlifeGlobalFlagsAudit(JsonObject? flagsRoot)
    {
        if (flagsRoot == null)
            return null;

        var clone = flagsRoot.DeepClone().AsObject();
        if (clone[AfterlifeGlobalFlagState.FlagsProperty] is not JsonArray flags)
            return clone;

        var visibleFlags = new JsonArray();
        foreach (var flag in flags.OfType<JsonObject>())
        {
            if (string.Equals(AfterlifeGlobalFlagState.GetNodeString(flag["visibility"]), "visible", StringComparison.OrdinalIgnoreCase))
                visibleFlags.Add(flag.DeepClone());
        }

        clone[AfterlifeGlobalFlagState.FlagsProperty] = visibleFlags;
        clone["hiddenFlagCount"] = flags.Count - visibleFlags.Count;
        return clone;
    }

    private static void AppendMalformedAfterlifeStateStatusLines(
        List<string> lines,
        IEnumerable<AfterlifeStatusJsonReadResult> stateReads,
        IReadOnlyList<ShiningContextReadIssue>? shiningReadIssues,
        bool includeAuditPayloads)
    {
        var issues = stateReads
            .Where(result => !string.IsNullOrWhiteSpace(result.Error))
            .Select(result => (Path: result.Path, Error: result.Error!))
            .ToList();

        if (shiningReadIssues != null)
        {
            issues.AddRange(shiningReadIssues
                .Where(issue => !string.IsNullOrWhiteSpace(issue.Error))
                .Select(issue => (issue.Path, issue.Error)));
        }

        if (issues.Count == 0)
            return;

        lines.Add("");
        lines.Add("[bold red]Повреждённые файлы состояния посмертия:[/]");
        foreach (var issue in issues)
        {
            lines.Add($"  • {Markup.Escape(issue.Path)}: [red]{Markup.Escape(issue.Error)}[/]");
            lines.Add(includeAuditPayloads
                ? "    Полные сырые данные выведены ниже отдельной аудит-панелью только для ремонта; ГМ/клиент не должен молча нормализовать или перезаписывать этот файл."
                : "    Для ремонта откройте /status audit или /статус аудит; обычная сводка не показывает сырые повреждённые данные.");
        }
    }

    private void WriteMalformedAfterlifeStateAuditPanels(
        IEnumerable<AfterlifeStatusJsonReadResult> stateReads,
        IReadOnlyList<ShiningContextReadIssue>? shiningReadIssues)
    {
        foreach (var read in stateReads.Where(result => !string.IsNullOrWhiteSpace(result.Error)))
        {
            WriteRawAuditPanel($"Сырые данные повреждённого {read.Path}", read.RawPayload, Color.Red);
        }

        if (shiningReadIssues == null)
            return;

        foreach (var issue in shiningReadIssues.Where(issue => !string.IsNullOrWhiteSpace(issue.Error)))
        {
            WriteRawAuditPanel($"Сырые данные повреждённого {issue.Path}", issue.RawPayload, Color.Red);
        }
    }

    private void WriteRawAuditPanel(string title, string? rawPayload, Color? borderColor = null)
    {
        if (rawPayload == null)
            return;

        Write(new Panel(new Text(rawPayload))
        {
            Header = GameInterface.SafePanelHeader(title),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor ?? Color.Grey),
            Padding = new Padding(1, 1),
            Expand = true
        });
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
        lines.Add($"  • Активный Хранитель: [white]{Markup.Escape(string.IsNullOrWhiteSpace(guardianName) ? "не выбран" : guardianName)}[/]");
        lines.Add($"  • Текущая Обитель: [white]{Markup.Escape(string.IsNullOrWhiteSpace(currentAbodeName) ? "не выбрана" : currentAbodeName)}[/]");
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
                AfterlifeReturnGuardSemanticState.ActiveValid => $"защищено, remainingTurns={guard?.RemainingProtectedTurns ?? 0}",
                AfterlifeReturnGuardSemanticState.BlockingInvalid => "блокирующая повреждённая защита; остановиться до ремонта",
                _ => "не защищено"
            };
            var risk = reputation <= -21 ? "[red]доступно[/]" : "[green]недоступно[/]";
            lines.Add($"  • Риск принудительного воплощения Хранителем: {risk}; репутация=[white]{reputation}[/], порог=[white]<= -21[/], защита возвращения=[white]{Markup.Escape(guardLabel)}[/].");
        }
    }

    private async Task AppendAfterlifeProgressionStatusLinesAsync(List<string> lines)
    {
        lines.Add("");
        lines.Add("[bold]Живое развитие посмертия:[/]");

        var scheduleRoot = await ReadJsonObjectForAfterlifeStatusAsync(ProgressionScheduleService.SchedulePath);
        if (scheduleRoot == null)
        {
            lines.Add("  • План живого развития: не найден или повреждён.");
        }
        else
        {
            AppendProgressionObjectSummaryLines(lines, scheduleRoot, "План живого развития");
        }

        var turnRoot = await ReadJsonObjectForAfterlifeStatusAsync("input/turn_request.json");
        if (turnRoot?["progressionControl"] is JsonObject control)
        {
            AppendProgressionObjectSummaryLines(lines, control, "Управление развитием текущего хода");
            if (control["afterlifeCatchupContours"] is JsonArray contours)
                lines.Add($"  • Контуры догоняющей симуляции: [white]{Markup.Escape(FormatPendingIdentityValue(contours) ?? "[]")}[/]");
        }
        else
        {
            lines.Add("  • Управление развитием текущего хода: активных указаний нет.");
        }

        var reportRoot = await ReadJsonObjectForAfterlifeStatusAsync(ProgressionScheduleService.ReportPath);
        if (reportRoot == null)
        {
            lines.Add("  • Последний отчёт развития: нет текущего отчёта.");
        }
        else
        {
            var reportForSummary = UnwrapProgressionReportForStatus(reportRoot, out _);
            AppendProgressionObjectSummaryLines(lines, reportForSummary, "Последний отчёт развития");
        }
    }

    private static JsonObject UnwrapProgressionReportForStatus(JsonObject root, out string label)
    {
        if (root["progressionProcessingReport"] is JsonObject report)
        {
            label = "progression_report.progressionProcessingReport";
            return report;
        }

        label = "progression_report";
        return root;
    }

    private async Task WriteAfterlifeProgressionAuditPanelsAsync()
    {
        var scheduleRoot = await ReadJsonObjectForAfterlifeStatusAsync(ProgressionScheduleService.SchedulePath);
        WriteJsonAuditPanel("Полный JSON progression_schedule.json", scheduleRoot, Color.Cyan1);

        var turnRoot = await ReadJsonObjectForAfterlifeStatusAsync("input/turn_request.json");
        if (turnRoot?["progressionControl"] is JsonObject progressionControl)
            WriteJsonAuditPanel("Полный JSON input/turn_request.json.progressionControl", progressionControl, Color.Cyan1);

        var reportRoot = await ReadJsonObjectForAfterlifeStatusAsync(ProgressionScheduleService.ReportPath);
        if (reportRoot != null)
        {
            WriteJsonAuditPanel("Полный JSON progression_report.json", reportRoot, Color.Cyan1);
            var unwrappedReport = UnwrapProgressionReportForStatus(reportRoot, out _);
            if (!ReferenceEquals(unwrappedReport, reportRoot))
                WriteJsonAuditPanel("Полный JSON progression_report.progressionProcessingReport", unwrappedReport, Color.Cyan1);
        }
    }

    private static void AppendProgressionObjectSummaryLines(List<string> lines, JsonObject root, string label)
    {
        var fields = new[]
        {
            "currentRealm",
            "currentWorldTimeInMinutes",
            "lastWorldSimulationTimeInMinutes",
            "lastFactionSimulationTimeInMinutes",
            "worldCyclesAlreadyPendingBeforeTurn",
            "factionCyclesAlreadyPendingBeforeTurn",
            "currentChaosSeaTurnOrdinal",
            "nextChaosSeaTurnOrdinal",
            "lastChaosSeaSimulationOrdinal",
            "lastGuardianProjectCycleOrdinal",
            "nextGuardianProjectCycleOrdinal",
            "lastResidentAgencyCycleOrdinal",
            "nextResidentAgencyCycleOrdinal",
            "lastShiningAbodeCycleOrdinal",
            "nextShiningAbodeCycleOrdinal",
            "lastShiningFactionCycleOrdinal",
            "nextShiningFactionCycleOrdinal",
            "lastShiningTradeCycleOrdinal",
            "nextShiningTradeCycleOrdinal",
            "chaosSeaCyclesExpectedThisTurn",
            "guardianProjectCyclesExpectedThisTurn",
            "residentAgencyCyclesExpectedThisTurn",
            "shiningAbodeCyclesExpectedThisTurn",
            "shiningFactionCyclesExpectedThisTurn",
            "shiningTradeCyclesExpectedThisTurn",
            "mustEvaluateChaosSeaProgression",
            "mustEvaluateGuardianProjectProgression",
            "mustEvaluateResidentAgencyProgression",
            "mustEvaluateShiningAbodeProgression",
            "mustEvaluateShiningFactionProgression",
            "mustEvaluateShiningTradeProgression",
            "afterlifeCatchupRequired",
            "afterlifeCatchupElapsedCycles",
            "afterlifeCatchupPressureTier",
            "afterlifeCatchupSummaryEventsRequired",
            "sessionId",
            "requestId",
            "turnNumber",
            "chaosSeaCyclesProcessed",
            "guardianProjectCyclesProcessed",
            "residentAgencyCyclesProcessed",
            "shiningAbodeCyclesProcessed",
            "shiningFactionCyclesProcessed",
            "shiningTradeCyclesProcessed",
            "newLastChaosSeaSimulationOrdinal",
            "newLastGuardianProjectCycleOrdinal",
            "newLastResidentAgencyCycleOrdinal",
            "newLastShiningAbodeCycleOrdinal",
            "newLastShiningFactionCycleOrdinal",
            "newLastShiningTradeCycleOrdinal",
            "afterlifeCatchupProcessed",
            "afterlifeCatchupSummaryEventsProcessed"
        };

        var parts = fields
            .Where(field => !IsProgressionFieldInternalOnly(field))
            .Select(field => (Label: FormatProgressionFieldLabel(field), Value: FormatProgressionFieldValue(field, root[field])))
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{item.Label}: {item.Value}")
            .ToList();

        lines.Add(parts.Count == 0
            ? $"  • {label}: кратких сведений нет; подробности доступны в аудит-режиме."
            : $"  • {label}: [dim]{Markup.Escape(string.Join("; ", parts))}[/]");
    }

    private static bool IsProgressionFieldInternalOnly(string field) =>
        field is "sessionId" or "requestId";

    private static string FormatProgressionFieldLabel(string field) =>
        field switch
        {
            "currentRealm" => "Область",
            "currentWorldTimeInMinutes" => "Время мира, мин",
            "lastWorldSimulationTimeInMinutes" => "Последняя симуляция мира, мин",
            "lastFactionSimulationTimeInMinutes" => "Последняя симуляция фракций, мин",
            "worldCyclesAlreadyPendingBeforeTurn" => "Ожидающих циклов мира до хода",
            "factionCyclesAlreadyPendingBeforeTurn" => "Ожидающих циклов фракций до хода",
            "currentChaosSeaTurnOrdinal" => "Текущий цикл Моря Хаоса",
            "nextChaosSeaTurnOrdinal" => "Следующий цикл Моря Хаоса",
            "lastChaosSeaSimulationOrdinal" => "Последняя симуляция Моря Хаоса",
            "lastGuardianProjectCycleOrdinal" => "Последний цикл проектов Хранителей",
            "nextGuardianProjectCycleOrdinal" => "Следующий цикл проектов Хранителей",
            "lastResidentAgencyCycleOrdinal" => "Последний цикл резидентов",
            "nextResidentAgencyCycleOrdinal" => "Следующий цикл резидентов",
            "lastShiningAbodeCycleOrdinal" => "Последний цикл Сияющей Обители",
            "nextShiningAbodeCycleOrdinal" => "Следующий цикл Сияющей Обители",
            "lastShiningFactionCycleOrdinal" => "Последний цикл сияющих фракций",
            "nextShiningFactionCycleOrdinal" => "Следующий цикл сияющих фракций",
            "lastShiningTradeCycleOrdinal" => "Последний торговый цикл Сияющей Обители",
            "nextShiningTradeCycleOrdinal" => "Следующий торговый цикл Сияющей Обители",
            "chaosSeaCyclesExpectedThisTurn" => "Циклов Моря Хаоса на этом ходу",
            "guardianProjectCyclesExpectedThisTurn" => "Циклов проектов Хранителей на этом ходу",
            "residentAgencyCyclesExpectedThisTurn" => "Циклов резидентов на этом ходу",
            "shiningAbodeCyclesExpectedThisTurn" => "Циклов Сияющей Обители на этом ходу",
            "shiningFactionCyclesExpectedThisTurn" => "Циклов сияющих фракций на этом ходу",
            "shiningTradeCyclesExpectedThisTurn" => "Торговых циклов Сияющей Обители на этом ходу",
            "mustEvaluateChaosSeaProgression" => "Нужно обновить Море Хаоса",
            "mustEvaluateGuardianProjectProgression" => "Нужно обновить проекты Хранителей",
            "mustEvaluateResidentAgencyProgression" => "Нужно обновить резидентов",
            "mustEvaluateShiningAbodeProgression" => "Нужно обновить Сияющую Обитель",
            "mustEvaluateShiningFactionProgression" => "Нужно обновить сияющие фракции",
            "mustEvaluateShiningTradeProgression" => "Нужно обновить торговлю Сияющей Обители",
            "afterlifeCatchupRequired" => "Нужна догоняющая симуляция",
            "afterlifeCatchupElapsedCycles" => "Пропущенных циклов",
            "afterlifeCatchupPressureTier" => "Давление догоняющей симуляции",
            "afterlifeCatchupSummaryEventsRequired" => "Нужно кратких событий",
            "turnNumber" => "Ход",
            "chaosSeaCyclesProcessed" => "Обработано циклов Моря Хаоса",
            "guardianProjectCyclesProcessed" => "Обработано циклов проектов Хранителей",
            "residentAgencyCyclesProcessed" => "Обработано циклов резидентов",
            "shiningAbodeCyclesProcessed" => "Обработано циклов Сияющей Обители",
            "shiningFactionCyclesProcessed" => "Обработано циклов сияющих фракций",
            "shiningTradeCyclesProcessed" => "Обработано торговых циклов Сияющей Обители",
            "newLastChaosSeaSimulationOrdinal" => "Новая отметка Моря Хаоса",
            "newLastGuardianProjectCycleOrdinal" => "Новая отметка проектов Хранителей",
            "newLastResidentAgencyCycleOrdinal" => "Новая отметка резидентов",
            "newLastShiningAbodeCycleOrdinal" => "Новая отметка Сияющей Обители",
            "newLastShiningFactionCycleOrdinal" => "Новая отметка сияющих фракций",
            "newLastShiningTradeCycleOrdinal" => "Новая отметка торговли Сияющей Обители",
            "afterlifeCatchupProcessed" => "Догоняющая симуляция обработана",
            "afterlifeCatchupSummaryEventsProcessed" => "Создано кратких событий",
            _ => HumanizeAfterlifeStatusToken(field)
        };

    private static string? FormatProgressionFieldValue(string field, JsonNode? value)
    {
        var formatted = FormatPendingIdentityValue(value);
        if (string.IsNullOrWhiteSpace(formatted))
            return null;

        return field switch
        {
            "currentRealm" => FormatAfterlifeRealmForPlayer(formatted),
            "mustEvaluateChaosSeaProgression" or
            "mustEvaluateGuardianProjectProgression" or
            "mustEvaluateResidentAgencyProgression" or
            "mustEvaluateShiningAbodeProgression" or
            "mustEvaluateShiningFactionProgression" or
            "mustEvaluateShiningTradeProgression" or
            "afterlifeCatchupRequired" or
            "afterlifeCatchupProcessed" => FormatBooleanWord(formatted),
            _ => formatted
        };
    }

    private static string FormatBooleanWord(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "true" => "да",
            "false" => "нет",
            _ => value
        };

    private static string FormatAfterlifeRealmForPlayer(string? realm) =>
        realm switch
        {
            "Chaos Sea" => "Море Хаоса",
            "Shining Abode" => "Сияющая Обитель",
            "Mortal World" => "Смертный мир",
            "Mortal Realm" => "Смертный мир",
            null or "" => "не указана",
            _ => HumanizeAfterlifeStatusToken(realm)
        };

    private static string HumanizeAfterlifeStatusToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return "не указано";

        var raw = token.Trim().Replace('_', ' ');
        var builder = new StringBuilder(raw.Length + 8);
        for (var i = 0; i < raw.Length; i++)
        {
            var ch = raw[i];
            if (i > 0 && char.IsUpper(ch) && char.IsLetter(raw[i - 1]) && !char.IsWhiteSpace(raw[i - 1]))
                builder.Append(' ');
            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static void AppendShiningStatusLines(List<string> lines, ShiningContext? context)
    {
        lines.Add("");
        lines.Add("[bold gold1]Сияющая Обитель:[/]");
        if (context == null)
        {
            lines.Add("  • Сияющая Обитель ещё не открыта для этой души.");
            return;
        }

        var root = context.Root;
        lines.Add($"  • Доступность: [white]{Markup.Escape(DescribeShiningAvailability(GetNodeString(root["availability"])))}[/]");
        lines.Add($"  • Сияние (radiance): [yellow]{GetNodeInt(root["radiance"]?["experience"])} XP[/] [dim](тир {GetNodeInt(root["radiance"]?["tier"])})[/]");
        var sourceStatus = root[SourceOfLightCapstoneState.ShiningStateProperty] is JsonObject sourceMarker &&
                           GetNodeBool(sourceMarker["completed"])
            ? "завершён: Воплощение Света + Воплощенный Свет"
            : GetNodeInt(root["radiance"]?["tier"]) >= SourceOfLightCapstoneState.RequiredRadianceTier &&
              GetNodeInt(root["radiance"]?["experience"]) >= SourceOfLightCapstoneState.RequiredRadianceExperience
                ? "доступен: /источник_света"
                : $"закрыт: нужно radiance.tier={SourceOfLightCapstoneState.RequiredRadianceTier}, experience>={SourceOfLightCapstoneState.RequiredRadianceExperience}";
        lines.Add($"  • Источник Света (Source of Light): [white]{Markup.Escape(sourceStatus)}[/].");
        lines.Add($"  • Искры Света (lightSparks): [gold1]{GetNodeInt(root["lightSparks"])}[/]");
        if (root["treasury"] is JsonObject treasury)
        {
            lines.Add($"  • Сокровищница (treasury): вклад [white]{GetNodeInt(treasury["depositedInkFeathers"])} 🪶[/], доступный процент [white]{GetNodeInt(treasury["claimableInkFeatherInterest"])} 🪶[/], обменено в цикле [white]{GetNodeInt(treasury["exchangeThisCycleLightSparks"])}[/]/[white]{ShiningAbodeState.TreasuryMaxLightSparksExchangePerCycle}[/] ✨.");
        }
        lines.Add($"  • Заряды гачи Сияющей Обители (gachaSystem): [white]{ShiningAbodeState.GetRemainingShiningGachaCharges(root)}[/]/[white]{GetNodeInt(root["gachaSystem"]?["chargesPerReturn"])}[/] [dim]({BuildShiningReturnCycleStatusLabel(root)})[/]");
        lines.Add($"  • Фракций: [white]{(root["factions"] as JsonArray)?.Count ?? 0}[/], залов: [white]{(root["halls"] as JsonArray)?.Count ?? 0}[/], вознесённых резидентов: [white]{CountAscendedShiningResidents(context.ResidentRoot)}[/]");
        lines.Add($"  • Журналы квитанций: coreAction={(root["coreActionReceipts"] as JsonArray)?.Count ?? 0}, founding={CountNestedReceipts(root, "factionFoundingReceipts")}, realignment={CountNestedReceipts(root, "factionRealignmentReceipts")}, leadership={CountNestedReceipts(root, "leadershipReceipts")}, trade={CountNestedReceipts(root, ShiningTradeRequestState.ReceiptsProperty)}.");
        var legacyPendingDiscoveryIssue = ShiningAbodeState.ValidateLegacyPendingNativeFactionDiscoveryShape(root);
        if (!string.IsNullOrWhiteSpace(legacyPendingDiscoveryIssue))
        {
            lines.Add($"  • Блокер ремонта: [red]{Markup.Escape(legacyPendingDiscoveryIssue)}[/]");
            lines.Add($"    path: [dim]{Markup.Escape(ShiningAbodeState.StatePath)}.pendingNativeFactionDiscovery[/]");
        }

        if (root["gates"] is JsonObject gates)
        {
            lines.Add($"  • Врата: draftVersion [white]{GetNodeInt(gates["draftVersion"])}[/], открытый черновик={GetNodeBool(gates["hasOpenDraft"])}, устарел={GetNodeBool(gates["isStale"])}, доступно карт={(gates["availableBlessingCards"] as JsonArray)?.Count ?? 0}, выбрано={(gates["selectedBlessingCardIds"] as JsonArray)?.Count ?? 0}, перебросы={GetNodeInt(gates["rerollsRemaining"])}.");
            AppendSelectedShiningCardStatusLines(lines, gates, context);
        }
        if (root["preparedIncarnationPackage"] is JsonObject package)
        {
            lines.Add($"  • Подготовленный пакет (preparedIncarnationPackage): draftVersion [white]{GetNodeInt(package["generatedFromDraftVersion"])}[/], выбрано карт={(package["selectedCards"] as JsonArray)?.Count ?? 0}, preparedAtTurn={GetNodeInt(package["preparedAtTurn"])}.");
            AppendPreparedPackageShiningCardStatusLines(lines, root, package);
        }

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
                var lifecycle = DescribeShiningFactionLifecycle(GetNodeString(faction["factionLifecycle"]?["state"]));
                lines.Add($"  • {Markup.Escape(name)} [dim]({Markup.Escape(factionId)})[/]: жизненный цикл={Markup.Escape(lifecycle)}, сила={strength}, торговый тир={ShiningAbodeState.GetTradeTier(strength)}, слоты={ShiningAbodeState.GetTradeStockItemCount(faction, context.ResidentRoot)}, редкость={Markup.Escape(ShiningAbodeState.GetTradeRarityCeiling(strength))}, множитель услуг x{ShiningAbodeState.GetServiceMultiplier(strength):0.00}, резиденты={memberCount}, проекты={projects?.Count ?? 0}, поддержано={supported}.");
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

    private static void AppendNextLifePayloadStatusLines(List<string> lines, JsonObject? soulRoot, bool includeAuditPayloads)
    {
        if (soulRoot == null)
            return;

        var hasLegacy = soulRoot["pendingMemoryLegacy"] is JsonObject;
        var hasImprint = soulRoot["soulImprint"] is JsonObject;
        if (!hasLegacy && !hasImprint)
            return;

        lines.Add("");
        lines.Add("[bold]Данные следующей жизни:[/]");
        lines.Add(includeAuditPayloads
            ? "  • Эти данные будут применяться в следующей смертной жизни; ниже показаны ремонтные идентификаторы и снимки."
            : "  • Эти данные будут применяться в следующей смертной жизни; состояние души можно проверить через /душа.");

        if (soulRoot["pendingMemoryLegacy"] is JsonObject legacy)
        {
            if (!includeAuditPayloads)
            {
                var title = GetNodeString(legacy["skillName"])
                            ?? GetNodeString(legacy["sourceLifeHint"])
                            ?? GetNodeString(legacy["legacyType"])
                            ?? "эхо памяти";
                var bonus = FormatNextLifeMemoryLegacyBonus(legacy);
                var suffix = string.IsNullOrWhiteSpace(bonus) ? string.Empty : $"; бонус: {bonus}";
                lines.Add($"  • Память прошлой жизни: [white]{Markup.Escape(title)}[/]{Markup.Escape(suffix)}.");
            }
            else
            {
                lines.Add("  • pendingMemoryLegacy:");
                AddNextLifePayloadField(lines, legacy, "legacyId");
                AddNextLifePayloadField(lines, legacy, "legacyType");
                AddNextLifePayloadField(lines, legacy, "grantSource");
                AddNextLifePayloadField(lines, legacy, "applicationState");
                AddNextLifePayloadField(lines, legacy, "sourceLifeHint");
                AddNextLifePayloadField(lines, legacy, "characteristic");
                AddNextLifePayloadField(lines, legacy, "skillName");
                AddNextLifePayloadField(lines, legacy, "playerStatBonus");
                AddNextLifePayloadField(lines, legacy, "bonus");
                if (legacy["bonus"] is JsonObject bonus)
                    lines.Add($"    bonus: [dim]{Markup.Escape(bonus.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed))}[/]");
                if (legacy["grantSnapshot"] is JsonObject snapshot)
                    lines.Add($"    grantSnapshot: [dim]{Markup.Escape(snapshot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed))}[/]");
            }
        }

        if (soulRoot["soulImprint"] is JsonObject imprint)
        {
            if (!includeAuditPayloads)
            {
                var companionName = GetNodeString(imprint["companionName"]) ?? GetNodeString(imprint["NPCName"]) ?? "будущий спутник";
                var summary = GetNodeString(imprint["summary"]) ?? "связь сохранена для следующей жизни";
                lines.Add($"  • Отпечаток спутника: [white]{Markup.Escape(companionName)}[/] — {Markup.Escape(summary)}");
            }
            else
            {
                lines.Add("  • soulImprint:");
                foreach (var key in new[]
                {
                    "imprintId", "sourceCompanionId", "companionId", "NPCId", "sourceNpcId", "companionName", "NPCName",
                    "summary", "futureCompanionPrompt", "grantSource", "applicationState"
                })
                {
                    AddNextLifePayloadField(lines, imprint, key);
                }

                foreach (var key in new[] { "coreTraits", "personalityMarkers", "relationshipMarkers", "appearanceMotifs", "sourceProvenance" })
                {
                    if (imprint[key] is JsonArray or JsonObject)
                        lines.Add($"    {key}: [dim]{Markup.Escape(imprint[key]!.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed))}[/]");
                }
            }
        }
    }

    private static void AddNextLifePayloadField(List<string> lines, JsonObject root, string key)
    {
        var value = FormatPendingIdentityValue(root[key]);
        if (!string.IsNullOrWhiteSpace(value))
            lines.Add($"    {key}: [white]{Markup.Escape(value)}[/]");
    }

    private static string FormatNextLifeMemoryLegacyBonus(JsonObject legacy)
    {
        var characteristic = GetNodeString(legacy["characteristic"]);
        var numericBonus = GetNodeInt(legacy["bonus"]);
        if (!string.IsNullOrWhiteSpace(characteristic) && numericBonus != 0)
            return $"{StructuredBonusDisplay.FormatCharacteristicName(characteristic)} {numericBonus:+#;-#;0}";

        return FormatPendingIdentityValue(legacy["bonus"] ?? legacy["playerStatBonus"]) ?? string.Empty;
    }

    private static void AppendSelectedShiningCardStatusLines(List<string> lines, JsonObject gates, ShiningContext context)
    {
        var selected = (gates["selectedBlessingCardIds"] as JsonArray)?
            .OfType<JsonValue>()
            .Select(value => value.TryGetValue<string>(out var id) ? id : string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList() ?? new List<string>();
        if (selected.Count == 0)
            return;

        lines.Add("  • Выбранные карты благословений (все выбранные карты; безопасное описание эффектов, без скрытых runtime-ключей):");
        foreach (var cardId in selected)
        {
            var card = FindShiningBlessingCardForAudit(context.Root, cardId);
            if (card == null)
            {
                lines.Add($"    - {Markup.Escape(cardId)} [dim](карта не найдена в открытых или подготовленных вариантах; проверьте /сияющая_обитель)[/]");
                continue;
            }

            lines.Add($"    - {Markup.Escape(FormatShiningReceiptAuditObject(card))}");
            foreach (var effectLine in BuildShiningBlessingEffectDetailLines(card))
                lines.Add($"      эффект: [dim]{Markup.Escape(effectLine)}[/]");
        }
    }

    private static JsonObject? FindShiningBlessingCardForAudit(JsonObject shiningRoot, string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return null;

        if (shiningRoot["gates"] is JsonObject gates)
        {
            foreach (var arrayName in new[] { "availableBlessingCards", "allCandidateBlessingCards" })
            {
                var card = (gates[arrayName] as JsonArray)?.OfType<JsonObject>()
                    .FirstOrDefault(item => string.Equals(GetNodeString(item["cardId"]), cardId, StringComparison.OrdinalIgnoreCase));
                if (card != null)
                    return card;
            }
        }

        if (shiningRoot["preparedIncarnationPackage"] is JsonObject package)
        {
            return GetConsistentPreparedPackageCards(package)
                .FirstOrDefault(item => string.Equals(GetNodeString(item["cardId"]), cardId, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static void AppendPreparedPackageShiningCardStatusLines(List<string> lines, JsonObject shiningRoot, JsonObject package)
    {
        var selected = GetPreparedPackageSelectedCardIds(package);
        if (selected.Count == 0)
            return;

        lines.Add("  • Выбранные карты подготовленного пакета (заморожены; безопасное описание эффектов, без скрытых runtime-ключей):");
        foreach (var cardId in selected)
        {
            var card = FindShiningBlessingCardForAudit(shiningRoot, cardId);
            if (card == null)
            {
                lines.Add($"    - {Markup.Escape(cardId)} [dim](сохранённые данные карты недоступны; проверьте /сияющая_обитель)[/]");
                continue;
            }

            lines.Add($"    - {Markup.Escape(FormatShiningReceiptAuditObject(card))}");
            foreach (var effectLine in BuildShiningBlessingEffectDetailLines(card))
                lines.Add($"      эффект: [dim]{Markup.Escape(effectLine)}[/]");
        }
    }

    private async Task<List<string>> BuildAfterlifePendingContractAuditLinesAsync(bool includeShining, bool includeFullPayload)
    {
        var entries = await ReadAfterlifePendingContractAuditEntriesAsync(includeShining);
        var lines = new List<string>
        {
            includeFullPayload
                ? "[bold]Активные ожидающие/контрольные контракты:[/]"
                : "[bold]Ожидающие решения посмертия:[/]"
        };

        if (entries.Count == 0)
        {
            lines.Add("  • Нет активных ожидающих/контрольных контрактов, блокирующих обычный посмертный ход.");
            return lines;
        }

        foreach (var entry in entries)
        {
            var isWrongRealmShiningContract =
                entry.Definition.ShiningOnly &&
                _stateManager.CurrentState.IsInChaosSea;
            var isWrongRealmMortalContract =
                !entry.Definition.ShiningOnly &&
                IsMortalWorldPendingContract(entry.Definition.Path);
            var requestLabel = entry.RequestIndex.HasValue ? $"requests[{entry.RequestIndex.Value}]" : "root";
            lines.Add(includeFullPayload
                ? $"  • [white]{Markup.Escape(entry.Definition.Label)}[/] — [dim]{Markup.Escape(entry.Definition.Path)}[/] / {Markup.Escape(requestLabel)}"
                : $"  • [white]{Markup.Escape(entry.Definition.Label)}[/]");
            if (isWrongRealmShiningContract)
            {
                lines.Add("    область: [yellow]только ремонт в неверной области: Море Хаоса[/]; сохранить данные и не закрывать сияющими квитанциями/состоянием, пока область не станет Сияющей Обителью.");
            }
            else if (isWrongRealmMortalContract && !includeFullPayload)
            {
                lines.Add("    область: [yellow]только ремонт в неверной области посмертия[/]; сохранить данные и не закрывать посмертными квитанциями.");
            }
            if (entry.IsMalformed)
            {
                lines.Add(includeFullPayload
                    ? $"    повреждение: [red]{Markup.Escape(entry.Error ?? "unknown parse error")}[/]"
                    : "    состояние: [red]данные повреждены; требуется ремонт, прежде чем продолжать это ожидание[/]");
                if (includeFullPayload)
                    lines.Add($"    закрытие/ремонт: {Markup.Escape(isWrongRealmShiningContract ? "только ремонт повреждённого файла; не проводить сияющее закрытие из Моря Хаоса" : entry.Definition.ClosureHint)}");
                if (includeFullPayload && !string.IsNullOrWhiteSpace(entry.RawPayload))
                {
                    lines.Add("    сырые повреждённые данные:");
                    AppendIndentedRawPayloadLines(lines, entry.RawPayload, "      ");
                }
                continue;
            }

            if (entry.Payload != null)
            {
                var identity = includeFullPayload
                    ? BuildPendingContractIdentitySummary(entry.Payload)
                    : BuildPendingContractPlayerSummary(entry.Payload);
                if (!string.IsNullOrWhiteSpace(identity))
                    lines.Add(includeFullPayload
                        ? $"    идентификаторы: {Markup.Escape(identity)}"
                        : $"    кратко: {Markup.Escape(identity)}");
                else if (includeFullPayload)
                {
                    lines.Add("    идентификаторы: поля не найдены; проверьте полные данные ниже");
                }
            }
            if (includeFullPayload)
            {
                lines.Add(isWrongRealmShiningContract
                    ? "    закрытие: [yellow]недоступно в Море Хаоса[/]; сияющий ожидающий контракт показан только для полного аудита/ремонта."
                    : $"    закрытие: {Markup.Escape(entry.Definition.ClosureHint)}");
            }
            else
            {
                lines.Add(isWrongRealmShiningContract
                    ? "    состояние: [yellow]ожидает Сияющей Обители или ремонта[/]"
                    : isWrongRealmMortalContract
                        ? "    состояние: [yellow]ожидает ремонта в неверной области[/]"
                    : "    состояние: ожидает ответа ГМ; подробности появятся в уведомлениях посмертия.");
            }

            if (includeFullPayload && entry.Payload != null)
            {
                lines.Add("    полные данные:");
                foreach (var payloadLine in entry.Payload.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed).Split('\n'))
                    lines.Add($"      [dim]{Markup.Escape(payloadLine.TrimEnd('\r'))}[/]");
            }
        }

        return lines;
    }

    private static bool IsMortalWorldPendingContract(string path) =>
        string.Equals(path, ActorSocialInteractionRequestState.PendingNpcRequestPath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, NpcTradeRequestState.PendingRequestPath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, CraftRequestState.PendingRequestPath, StringComparison.OrdinalIgnoreCase);

    private static string BuildPendingContractPlayerSummary(JsonObject payload)
    {
        var parts = new List<string>();
        AddPendingPlayerField(parts, "Хранитель", payload["guardianName"]);
        AddPendingPlayerField(parts, "Обитель", payload["abodeName"]);
        AddPendingPlayerField(parts, "NPC", payload["npcName"] ?? payload["NPCName"]);
        AddPendingPlayerField(parts, "Резидент", payload["residentName"]);
        AddPendingPlayerField(parts, "Фракция", payload["factionName"]);
        AddPendingPlayerField(parts, "Проект", payload["projectDisplayName"]);
        AddPendingPlayerField(parts, "Реликвия", payload["relicName"]);
        AddPendingPlayerField(parts, "Запись Архива", payload["archiveEntryType"]);
        AddPendingPlayerField(parts, "Тип", payload["interactionType"] ?? payload["requestMode"] ?? payload["offeringType"] ?? payload["actionType"]);
        AddPendingPlayerField(parts, "Ход", payload["createdAtTurn"]);
        return string.Join("; ", parts);
    }

    private static void AddPendingPlayerField(List<string> parts, string label, JsonNode? node)
    {
        var value = FormatPendingIdentityValue(node);
        if (!string.IsNullOrWhiteSpace(value))
            parts.Add($"{label}: {value}");
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
                result.Add(new AfterlifePendingContractAuditEntry(definition, null, null, IsMalformed: true, Error: "пустой файл", RawPayload: raw));
                continue;
            }

            try
            {
                var node = JsonNode.Parse(raw);
                if (node is JsonObject root && root["requests"] is JsonArray requests)
                {
                    if (requests.Count == 0)
                    {
                        continue;
                    }

                    for (var i = 0; i < requests.Count; i++)
                    {
                        result.Add(new AfterlifePendingContractAuditEntry(
                            definition,
                            requests[i] as JsonObject,
                            i,
                            IsMalformed: requests[i] is not JsonObject,
                            Error: requests[i] is JsonObject ? null : "запись request не является объектом",
                            RawPayload: requests[i]?.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed) ?? raw));
                    }
                }
                else if (node is JsonObject obj)
                {
                    result.Add(new AfterlifePendingContractAuditEntry(definition, obj, null, IsMalformed: false, Error: null, RawPayload: raw));
                }
                else
                {
                    result.Add(new AfterlifePendingContractAuditEntry(definition, null, null, IsMalformed: true, Error: "корень JSON не является объектом", RawPayload: raw));
                }
            }
            catch (Exception ex)
            {
                result.Add(new AfterlifePendingContractAuditEntry(definition, null, null, IsMalformed: true, Error: ex.GetType().Name, RawPayload: raw));
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
            var allValues = array
                .Select(FormatPendingIdentityValue)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            var values = allValues.Take(8).ToList();
            if (allValues.Length > values.Count)
                values.Add($"+{allValues.Length - values.Count} more");
            return values.Count == 0 ? null : $"[{string.Join(", ", values)}]";
        }

        if (node is JsonObject)
            return null;

        var value = GetNodeString(node);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private async Task<JsonObject?> ReadJsonObjectForAfterlifeStatusAsync(string path)
        => (await ReadJsonObjectForAfterlifeStatusResultAsync(path)).Root;

    private async Task<AfterlifeStatusJsonReadResult> ReadJsonObjectForAfterlifeStatusResultAsync(string path)
    {
        var raw = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(raw))
            return new AfterlifeStatusJsonReadResult(path, null, raw, null);

        try
        {
            var root = JsonNode.Parse(raw) as JsonObject;
            return root == null
                ? new AfterlifeStatusJsonReadResult(path, null, raw, "корень JSON не является объектом")
                : new AfterlifeStatusJsonReadResult(path, root, raw, null);
        }
        catch (Exception ex)
        {
            return new AfterlifeStatusJsonReadResult(path, null, raw, ex.GetType().Name);
        }
    }

    private static void AppendIndentedRawPayloadLines(List<string> lines, string rawPayload, string prefix)
    {
        foreach (var payloadLine in rawPayload.Replace("\r\n", "\n").Split('\n'))
            lines.Add($"{prefix}[dim]{Markup.Escape(payloadLine.TrimEnd('\r'))}[/]");
    }

}
