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
        new(AfterlifeArchiveActionState.ProjectFuelRequestPath, "Подпитка проекта Архивом", "archiveActionResolutions + allowed project/log effect"),
        new(GuardianAbodeOfferingState.PendingRequestPath, "Подношение Обители", "guardianPowerEvents.reasonType=offering; ink_feathers additionally require output/ink_feather_action_result.json"),
        new(GuardianTradeRequestState.PendingRequestPath, "Торговая витрина Хранителя", "UpdateGuardians + guardians[].tradeInventory + tradeInventoryReceipts[]"),
        new(PlayerGuardianFoundationState.PendingRequestPath, "Основание собственного Хранителя", "UpdateGuardians.create + guardians/activeGuardian + playerGuardianFoundationHistory"),
        new(SystemGuardianLibraryService.AttractionRequestPath, "Притяжение извечного Хранителя", "Chaos Sea-only: UpdateGuardians + guardians/activeGuardian + chaosSeaNavigation; outside Chaos Sea preserve as wrong-realm repair-only or clear by explicit client cancellation"),
        new(AfterlifeReturnGuardService.GuardPath, "Post-life return guard", "client-owned protection guard; GM must not clear or bypass it"),
        new(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, "Состав резидентов Обители", "UpdateGuardianAbodeResidents + UpdateGuardianAbodeResidentRosterReceipts"),
        new(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, "Разговор/история резидента Обители", "residentInteractionLogUpdates or resident history log + matching interaction receipts"),
        new(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, "Переход резидента между Обителями", "UpdateGuardianAbodeResidentTransferReceipts + source/target resident state"),
        new(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, "Манифестация резидента в следующей жизни", "MortalWorldProfile-only closure after bootstrap; valid files are preserved in afterlife, malformed files require repair"),
        new(ActorSocialInteractionRequestState.PendingGuardianRequestPath, "Социальный запрос к Хранителю", "guardianSocialJournalUpdates with matching requestId/guardianId/interactionType"),
        new(ActorSocialInteractionRequestState.PendingNpcRequestPath, "NPC social request из смертного мира", "wrong-realm repair-only in afterlife; preserve full payload and do not close through afterlife"),
        new(NpcTradeRequestState.PendingRequestPath, "NPC trade request из смертного мира", "wrong-realm repair-only in afterlife; preserve full payload and do not create afterlife trade receipts"),
        new(ShiningCoreActionRequestState.PendingActionsRequestPath, "Core-действие Сияющей Обители", "shining_abode_state.coreActionReceipts[] + exact canonical state projection", ShiningOnly: true),
        new(ShiningTradeRequestState.PendingRequestsPath, "Торговая витрина Сияющей фракции", "factions[].tradeInventory + tradeInventoryReceipts[]", ShiningOnly: true),
        new(ShiningFactionRequestState.PendingFoundingsRequestPath, "Основание сияющей фракции", "halls[]/factions[] + factionFoundingReceipts[]", ShiningOnly: true),
        new(ShiningFactionRequestState.PendingRealignmentsRequestPath, "Переход сияющего резидента", "guardian_abode_residents.json resident faction fields + factionRealignmentReceipts[]", ShiningOnly: true),
        new(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath, "Смена власти сияющей фракции", "faction.leadership + leadershipReceipts[] + leadershipHistory[]", ShiningOnly: true),
        new(SourceOfLightCapstoneState.PendingRequestPath, "Источник Света", "sourceOfLightCapstone + afterlifeCombatProfile.capstones.lightIncarnate + soulRelics.stored[]", ShiningOnly: true)
    };

    private async Task ShowAfterlifeDetailedStatusAsync()
    {
        await _stateManager.RefreshGameStateAsync();
        var soulStateRead = await ReadJsonObjectForAfterlifeStatusResultAsync("game_state/meta/soul_state.json");
        var guardiansStateRead = await ReadJsonObjectForAfterlifeStatusResultAsync("game_state/meta/guardians.json");
        var residentsStateRead = await ReadJsonObjectForAfterlifeStatusResultAsync(GuardianAbodeResidentState.StatePath);
        var shiningStateRead = await ReadJsonObjectForAfterlifeStatusResultAsync(ShiningAbodeState.StatePath);
        var spiritualConflictRead = await ReadJsonObjectForAfterlifeStatusResultAsync(AfterlifeSpiritualConflictState.StatePath);
        var entityProfilesRead = await ReadJsonObjectForAfterlifeStatusResultAsync(AfterlifeEntityProfileState.StatePath);
        var soulRoot = soulStateRead.Root;
        var guardiansRoot = guardiansStateRead.Root;
        var residentsRoot = residentsStateRead.Root;
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

        AppendNextLifePayloadStatusLines(lines, soulRoot);
        AppendChaosSeaStatusLines(lines, guardiansRoot, residentsRoot, returnGuardRaw);
        AppendShiningStatusLines(lines, shiningContext);
        AppendAfterlifeSpiritualConflictStatusLines(lines, spiritualConflictRead.Root);
        AppendMalformedAfterlifeStateStatusLines(
            lines,
            new[] { soulStateRead, guardiansStateRead, residentsStateRead, shiningStateRead, spiritualConflictRead, entityProfilesRead },
            shiningContext?.ReadIssues);
        await AppendAfterlifeProgressionStatusLinesAsync(lines);

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
        WriteJsonAuditPanel("Полный JSON game_state/meta/soul_state.json", soulRoot, Color.Cyan1);
        WriteJsonAuditPanel("Полный JSON game_state/meta/guardians.json", guardiansRoot, Color.Cyan1);
        WriteJsonAuditPanel("Полный JSON game_state/meta/guardian_abode_residents.json", residentsRoot, Color.Cyan1);
        WriteMalformedAfterlifeStateAuditPanels(
            new[] { soulStateRead, guardiansStateRead, residentsStateRead, shiningStateRead, spiritualConflictRead, entityProfilesRead },
            shiningContext?.ReadIssues);
        if (shiningContext != null)
        {
            WriteJsonAuditPanel("Полный JSON game_state/meta/shining_abode_state.json", CloneShiningJsonForPlayerFacingAudit(shiningContext.Root), Color.Gold1);
            WriteJsonAuditPanel("Полный JSON Shining resident bindings", shiningContext.ResidentRoot, Color.Gold1);
            WriteJsonAuditPanel("Полный JSON Shining gates", CloneShiningJsonForPlayerFacingAudit(shiningContext.Root["gates"]), Color.Gold1);
            WriteJsonAuditPanel("Полный JSON preparedIncarnationPackage", CloneShiningJsonForPlayerFacingAudit(shiningContext.Root["preparedIncarnationPackage"]), Color.Gold1);
            WriteJsonAuditPanel("Полный JSON Shining coreActionReceipts", CloneShiningJsonForPlayerFacingAudit(shiningContext.Root["coreActionReceipts"]), Color.Gold1);
            WriteJsonAuditPanel("Полный JSON Shining gachaSystem", CloneShiningJsonForPlayerFacingAudit(shiningContext.Root["gachaSystem"]), Color.Gold1);
            WriteJsonAuditPanel("Полный JSON Shining treasury", CloneShiningJsonForPlayerFacingAudit(shiningContext.Root["treasury"]), Color.Gold1);
        }
        WriteJsonAuditPanel($"Полный JSON {AfterlifeSpiritualConflictState.StatePath}", spiritualConflictRead.Root, Color.Cyan1);
        WriteJsonAuditPanel($"Полный JSON {AfterlifeEntityProfileState.StatePath}", entityProfilesRead.Root, Color.Cyan1);
        await WriteAfterlifeProgressionAuditPanelsAsync();
        WaitForKey();
    }

    private static void AppendAfterlifeSpiritualConflictStatusLines(List<string> lines, JsonObject? conflictRoot)
    {
        lines.Add("");
        lines.Add("[bold]Духовный конфликт посмертия[/] [dim](afterlife spiritual conflict)[/]:");
        var active = conflictRoot?["activeConflict"] as JsonObject;
        if (active == null)
        {
            lines.Add("  • Активного духовного конфликта нет.");
            return;
        }

        lines.Add($"  • ID конфликта (conflictId): [white]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(active["conflictId"]) ?? "unknown")}[/]");
        lines.Add($"  • Модель/позиция (sideModel/conflictPosition): [white]{Markup.Escape(FormatSideModelLabel(AfterlifeSpiritualConflictState.GetNodeString(active["sideModel"])))}[/] / [white]{Markup.Escape(FormatConflictPositionLabel(AfterlifeSpiritualConflictState.GetNodeString(active["conflictPosition"])))}[/]");
        lines.Add($"  • Напряжение сторон (side strain): игрок=[white]{Markup.Escape(FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(active["playerSideStrain"])))}[/], противник=[white]{Markup.Escape(FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(active["oppositionSideStrain"])))}[/]");
        lines.Add($"  • Состояние завершения (resolutionState): [white]{Markup.Escape(FormatResolutionStateLabel(AfterlifeSpiritualConflictState.GetNodeString(active["resolutionState"])))}[/]");
        lines.Add($"  • Обмены действиями (exchangeLog): [white]{(active["exchangeLog"] as JsonArray)?.Count ?? 0}[/]");
    }

    private static void AppendMalformedAfterlifeStateStatusLines(
        List<string> lines,
        IEnumerable<AfterlifeStatusJsonReadResult> stateReads,
        IReadOnlyList<ShiningContextReadIssue>? shiningReadIssues)
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
        lines.Add("[bold red]Malformed afterlife state files:[/]");
        foreach (var issue in issues)
        {
            lines.Add($"  • {Markup.Escape(issue.Path)}: [red]{Markup.Escape(issue.Error)}[/]");
            lines.Add("    Полный raw payload выведен ниже отдельной repair-only audit-панелью; GM/client не должен молча нормализовать или перезаписывать этот файл.");
        }
    }

    private void WriteMalformedAfterlifeStateAuditPanels(
        IEnumerable<AfterlifeStatusJsonReadResult> stateReads,
        IReadOnlyList<ShiningContextReadIssue>? shiningReadIssues)
    {
        foreach (var read in stateReads.Where(result => !string.IsNullOrWhiteSpace(result.Error)))
        {
            WriteRawAuditPanel($"Raw malformed {read.Path}", read.RawPayload, Color.Red);
        }

        if (shiningReadIssues == null)
            return;

        foreach (var issue in shiningReadIssues.Where(issue => !string.IsNullOrWhiteSpace(issue.Error)))
        {
            WriteRawAuditPanel($"Raw malformed {issue.Path}", issue.RawPayload, Color.Red);
        }
    }

    private void WriteRawAuditPanel(string title, string? rawPayload, Color? borderColor = null)
    {
        if (rawPayload == null)
            return;

        Write(new Panel(new Text(rawPayload))
        {
            Header = new PanelHeader($" {Markup.Escape(title)} ", Justify.Center),
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

    private async Task AppendAfterlifeProgressionStatusLinesAsync(List<string> lines)
    {
        lines.Add("");
        lines.Add("[bold]Afterlife living-world progression:[/]");

        var scheduleRoot = await ReadJsonObjectForAfterlifeStatusAsync(ProgressionScheduleService.SchedulePath);
        if (scheduleRoot == null)
        {
            lines.Add($"  • {ProgressionScheduleService.SchedulePath}: не найден или повреждён.");
        }
        else
        {
            AppendProgressionObjectSummaryLines(lines, scheduleRoot, "schedule");
        }

        var turnRoot = await ReadJsonObjectForAfterlifeStatusAsync("input/turn_request.json");
        if (turnRoot?["progressionControl"] is JsonObject control)
        {
            AppendProgressionObjectSummaryLines(lines, control, "current progressionControl");
            if (control["afterlifeCatchupContours"] is JsonArray contours)
                lines.Add($"  • catch-up contours: [white]{Markup.Escape(FormatPendingIdentityValue(contours) ?? "[]")}[/]");
        }
        else
        {
            lines.Add("  • current progressionControl: нет активного input/turn_request.json с progressionControl.");
        }

        var reportRoot = await ReadJsonObjectForAfterlifeStatusAsync(ProgressionScheduleService.ReportPath);
        if (reportRoot == null)
        {
            lines.Add($"  • {ProgressionScheduleService.ReportPath}: нет текущего отчёта.");
        }
        else
        {
            var reportForSummary = UnwrapProgressionReportForStatus(reportRoot, out var reportLabel);
            AppendProgressionObjectSummaryLines(lines, reportForSummary, reportLabel);
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
            .Select(field => (Field: field, Value: FormatPendingIdentityValue(root[field])))
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{item.Field}={item.Value}")
            .ToList();

        lines.Add(parts.Count == 0
            ? $"  • {label}: no compact fields; inspect JSON/state file."
            : $"  • {label}: [dim]{Markup.Escape(string.Join("; ", parts))}[/]");
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
        var sourceStatus = root[SourceOfLightCapstoneState.ShiningStateProperty] is JsonObject sourceMarker &&
                           GetNodeBool(sourceMarker["completed"])
            ? "completed: Воплощение Света + Воплощенный Свет"
            : GetNodeInt(root["radiance"]?["tier"]) >= SourceOfLightCapstoneState.RequiredRadianceTier &&
              GetNodeInt(root["radiance"]?["experience"]) >= SourceOfLightCapstoneState.RequiredRadianceExperience
                ? "available: /источник_света"
                : $"locked: нужно radiance.tier={SourceOfLightCapstoneState.RequiredRadianceTier}, experience>={SourceOfLightCapstoneState.RequiredRadianceExperience}";
        lines.Add($"  • Источник Света (Source of Light): [white]{Markup.Escape(sourceStatus)}[/].");
        lines.Add($"  • Light Sparks: [gold1]{GetNodeInt(root["lightSparks"])}[/]");
        if (root["treasury"] is JsonObject treasury)
        {
            lines.Add($"  • Treasury: deposit [white]{GetNodeInt(treasury["depositedInkFeathers"])} 🪶[/], claimable interest [white]{GetNodeInt(treasury["claimableInkFeatherInterest"])} 🪶[/], exchanged this cycle [white]{GetNodeInt(treasury["exchangeThisCycleLightSparks"])}[/]/[white]{ShiningAbodeState.TreasuryMaxLightSparksExchangePerCycle}[/] ✨.");
        }
        lines.Add($"  • Shining gacha: [white]{ShiningAbodeState.GetRemainingShiningGachaCharges(root)}[/]/[white]{GetNodeInt(root["gachaSystem"]?["chargesPerReturn"])}[/] [dim]({BuildShiningReturnCycleStatusLabel(root)})[/]");
        lines.Add($"  • Фракций: [white]{(root["factions"] as JsonArray)?.Count ?? 0}[/], залов: [white]{(root["halls"] as JsonArray)?.Count ?? 0}[/], ascended residents: [white]{CountAscendedShiningResidents(context.ResidentRoot)}[/]");
        lines.Add($"  • Receipts: coreAction={(root["coreActionReceipts"] as JsonArray)?.Count ?? 0}, founding={CountNestedReceipts(root, "factionFoundingReceipts")}, realignment={CountNestedReceipts(root, "factionRealignmentReceipts")}, leadership={CountNestedReceipts(root, "leadershipReceipts")}, trade={CountNestedReceipts(root, ShiningTradeRequestState.ReceiptsProperty)}.");
        var legacyPendingDiscoveryIssue = ShiningAbodeState.ValidateLegacyPendingNativeFactionDiscoveryShape(root);
        if (!string.IsNullOrWhiteSpace(legacyPendingDiscoveryIssue))
        {
            lines.Add($"  • Repair-only blocker: [red]{Markup.Escape(legacyPendingDiscoveryIssue)}[/]");
            lines.Add($"    path: [dim]{Markup.Escape(ShiningAbodeState.StatePath)}.pendingNativeFactionDiscovery[/]");
        }

        if (root["gates"] is JsonObject gates)
        {
            lines.Add($"  • Врата: draftVersion [white]{GetNodeInt(gates["draftVersion"])}[/], open={GetNodeBool(gates["hasOpenDraft"])}, stale={GetNodeBool(gates["isStale"])}, availableCards={(gates["availableBlessingCards"] as JsonArray)?.Count ?? 0}, selected={(gates["selectedBlessingCardIds"] as JsonArray)?.Count ?? 0}, rerolls={GetNodeInt(gates["rerollsRemaining"])}.");
            AppendSelectedShiningCardStatusLines(lines, gates, context);
        }
        if (root["preparedIncarnationPackage"] is JsonObject package)
        {
            lines.Add($"  • Prepared package: draftVersion [white]{GetNodeInt(package["generatedFromDraftVersion"])}[/], selectedCards={(package["selectedCards"] as JsonArray)?.Count ?? 0}, preparedAtTurn={GetNodeInt(package["preparedAtTurn"])}.");
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

    private static void AppendNextLifePayloadStatusLines(List<string> lines, JsonObject? soulRoot)
    {
        if (soulRoot == null)
            return;

        var hasLegacy = soulRoot["pendingMemoryLegacy"] is JsonObject;
        var hasImprint = soulRoot["soulImprint"] is JsonObject;
        if (!hasLegacy && !hasImprint)
            return;

        lines.Add("");
        lines.Add("[bold]Next-life payloads:[/]");
        lines.Add("  • Эти данные будут применяться в следующей смертной жизни; полный просмотр доступен через /soul.");

        if (soulRoot["pendingMemoryLegacy"] is JsonObject legacy)
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

        if (soulRoot["soulImprint"] is JsonObject imprint)
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

    private static void AddNextLifePayloadField(List<string> lines, JsonObject root, string key)
    {
        var value = FormatPendingIdentityValue(root[key]);
        if (!string.IsNullOrWhiteSpace(value))
            lines.Add($"    {key}: [white]{Markup.Escape(value)}[/]");
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

        lines.Add("  • Selected blessing cards (all selected; safe effect details, no hidden runtime payload keys):");
        foreach (var cardId in selected)
        {
            var card = FindShiningBlessingCardForAudit(context.Root, cardId);
            if (card == null)
            {
                lines.Add($"    - {Markup.Escape(cardId)} [dim](card not found in available/candidate/package snapshots; inspect /shining_abode)[/]");
                continue;
            }

            lines.Add($"    - {Markup.Escape(FormatShiningReceiptAuditObject(card))}");
            foreach (var effectLine in BuildShiningBlessingEffectDetailLines(card))
                lines.Add($"      effect: [dim]{Markup.Escape(effectLine)}[/]");
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

        lines.Add("  • Prepared package selected cards (frozen; safe effect details, no hidden runtime payload keys):");
        foreach (var cardId in selected)
        {
            var card = FindShiningBlessingCardForAudit(shiningRoot, cardId);
            if (card == null)
            {
                lines.Add($"    - {Markup.Escape(cardId)} [dim](frozen card snapshot unavailable; inspect /shining_abode)[/]");
                continue;
            }

            lines.Add($"    - {Markup.Escape(FormatShiningReceiptAuditObject(card))}");
            foreach (var effectLine in BuildShiningBlessingEffectDetailLines(card))
                lines.Add($"      effect: [dim]{Markup.Escape(effectLine)}[/]");
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
            var isWrongRealmShiningContract =
                entry.Definition.ShiningOnly &&
                _stateManager.CurrentState.IsInChaosSea;
            var requestLabel = entry.RequestIndex.HasValue ? $"requests[{entry.RequestIndex.Value}]" : "root";
            lines.Add($"  • [white]{Markup.Escape(entry.Definition.Label)}[/] — [dim]{Markup.Escape(entry.Definition.Path)}[/] / {Markup.Escape(requestLabel)}");
            if (isWrongRealmShiningContract)
            {
                lines.Add("    realm: [yellow]wrong-realm repair-only in Chaos Sea[/]; preserve the payload and do not close it with Shining receipts/state until the realm is Shining Abode.");
            }
            if (entry.IsMalformed)
            {
                lines.Add($"    malformed: [red]{Markup.Escape(entry.Error ?? "unknown parse error")}[/]");
                lines.Add($"    closure/repair: {Markup.Escape(isWrongRealmShiningContract ? "repair malformed file only; do not process Shining closure from Chaos Sea" : entry.Definition.ClosureHint)}");
                if (includeFullPayload && !string.IsNullOrWhiteSpace(entry.RawPayload))
                {
                    lines.Add("    raw malformed payload:");
                    AppendIndentedRawPayloadLines(lines, entry.RawPayload, "      ");
                }
                continue;
            }

            if (entry.Payload != null)
            {
                var identity = BuildPendingContractIdentitySummary(entry.Payload);
                lines.Add($"    identity: {Markup.Escape(string.IsNullOrWhiteSpace(identity) ? "fields not found; inspect full payload below" : identity)}");
            }
            lines.Add(isWrongRealmShiningContract
                ? "    closure: [yellow]none in Chaos Sea[/]; Shining pending contract is displayed for full audit/repair only."
                : $"    closure: {Markup.Escape(entry.Definition.ClosureHint)}");

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
                result.Add(new AfterlifePendingContractAuditEntry(definition, null, null, IsMalformed: true, Error: "empty file", RawPayload: raw));
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
                            Error: requests[i] is JsonObject ? null : "request entry is not an object",
                            RawPayload: requests[i]?.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed) ?? raw));
                    }
                }
                else if (node is JsonObject obj)
                {
                    result.Add(new AfterlifePendingContractAuditEntry(definition, obj, null, IsMalformed: false, Error: null, RawPayload: raw));
                }
                else
                {
                    result.Add(new AfterlifePendingContractAuditEntry(definition, null, null, IsMalformed: true, Error: "root is not an object", RawPayload: raw));
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
                ? new AfterlifeStatusJsonReadResult(path, null, raw, "root is not an object")
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
