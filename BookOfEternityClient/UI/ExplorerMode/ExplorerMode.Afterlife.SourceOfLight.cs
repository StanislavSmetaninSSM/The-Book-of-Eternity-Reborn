using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private async Task ShowSourceOfLightAsync()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Источник Света"))
            return;

        await _stateManager.RefreshGameStateAsync();
        if (!_stateManager.CurrentState.IsInShiningAbode)
        {
            ShowEmptyPanel("Источник Света", "Источник Света доступен только в обычной активной Сияющей Обители.");
            WaitForKey();
            return;
        }

        var context = await LoadShiningContextAsync();
        var soulRoot = context?.SoulRoot ?? await ReadJsonObjectForAfterlifeStatusAsync(SoulStatePath);
        if (context == null || soulRoot == null)
        {
            ShowEmptyPanel("Источник Света", "Нужны читаемые game_state/meta/soul_state.json и game_state/meta/shining_abode_state.json.");
            WaitForKey();
            return;
        }

        var pending = await SourceOfLightCapstoneState.ReadRequestStateAsync(_fs);
        if (pending.IsMalformed)
        {
            ShowEmptyPanel(
                "Источник Света",
                $"{SourceOfLightCapstoneState.PendingRequestPath} повреждён: {pending.Error}. Исправьте pending-файл перед повторной попыткой.");
            WaitForKey();
            return;
        }

        if (pending.Request != null)
        {
            Write(BuildSourceOfLightPendingPanel(pending.Request));
            WaitForKey();
            return;
        }

        if (SourceOfLightCapstoneState.HasCompletedCapstone(context.Root) ||
            SourceOfLightCapstoneState.HasLightIncarnate(soulRoot) ||
            SourceOfLightCapstoneState.CountIncarnatedLightRelics(soulRoot) > 0)
        {
            Write(BuildSourceOfLightCompletedPanel(context.Root, soulRoot));
            WaitForKey();
            return;
        }

        if (!SourceOfLightCapstoneState.IsUnlockSatisfied(soulRoot, context.Root, out var unlockBlocker))
        {
            Write(BuildSourceOfLightLockedPanel(context.Root, unlockBlocker));
            WaitForKey();
            return;
        }

        var pendingBlocker = await TryDescribeSourceOfLightPendingBlockerAsync(context.Root);
        if (pendingBlocker != null)
        {
            ShowEmptyPanel("Источник Света", pendingBlocker);
            WaitForKey();
            return;
        }

        var request = SourceOfLightCapstoneState.CreateRequest(
            Math.Max(1, _stateManager.CurrentState.TurnNumber + 1),
            SourceOfLightCapstoneState.GetNodeInt(context.Root["radiance"]?["experience"]),
            SourceOfLightCapstoneState.GetNodeInt(context.Root["radiance"]?["tier"]));

        var preview = BuildSourceOfLightRequestPreview(request, context.Root, soulRoot);
        Write(BuildSourceOfLightAvailablePanel(request));
        WriteJsonAuditPanel("JSON pending_source_of_light_capstone.json", preview, Color.Gold1);

        if (!Confirm("[yellow]Открыть Источник Света и отправить pending contract GM?[/]", false))
        {
            MarkupLine("[dim]Источник Света не открыт; pending request не создан.[/]");
            WaitForKey();
            return;
        }

        await SourceOfLightCapstoneState.WriteRequestAsync(_fs, request);
        _pendingGmAction =
            $"[SOURCE_OF_LIGHT_CAPSTONE: {request.RequestId}] Душа входит в Источник Света.\n\n" +
            "Разреши это как capstone-сцену Сияющей Обители: Источник Света / Source of Light. " +
            "Опиши ролевую сцену познания причин зарождения мироздания, начала существования и смысла всего сущего. " +
            $"Закрой {SourceOfLightCapstoneState.PendingRequestPath}: сохрани ordinary active Shining invariants, " +
            $"запиши shining_abode_state.{SourceOfLightCapstoneState.ShiningStateProperty}.completed=true, " +
            $"добавь ровно один soul_state.afterlifeCombatProfile.capstones.lightIncarnate с passiveId={SourceOfLightCapstoneState.PassiveId}, " +
            $"и добавь ровно одну Soul Relic relicId={SourceOfLightCapstoneState.RelicId} через canonical soulRelics/metaStateUpdates path. " +
            "Не создавай повторную награду, не используй Mortal combat files и не смешивай closure с pending-bootstrap handoff.";

        MarkupLine("[green]Источник Света открыт: pending request создан и GM action подготовлен.[/]");
        WaitForKey();
    }

    private Panel BuildSourceOfLightAvailablePanel(SourceOfLightCapstoneState.SourceOfLightCapstoneRequest request)
    {
        var lines = new List<string>
        {
            "[bold gold1]Источник Света[/] [dim](Source of Light)[/]",
            "",
            "Полное Сияние достигнуто. Доступна секретная capstone-сцена Сияющей Обители.",
            "",
            "[bold]Награды после accepted closure:[/]",
            $"  • Воплощение Света (Light Incarnate), id={SourceOfLightCapstoneState.PassiveId}: +{SourceOfLightCapstoneState.LeadDiceBonus} к броску духовного конфликта, если игрок lead contestant; +{SourceOfLightCapstoneState.SupportDiceBonus}, если игрок supporter/champion-side contributor; ещё +{SourceOfLightCapstoneState.CoerciveOperationExtraBonus} против force_incarnation/force_binding/break_binding.",
            $"  • Воплощенный Свет (Incarnated Light), relicId={SourceOfLightCapstoneState.RelicId}: уникальная реликвия души, при экипировке в смертной жизни даёт +{SourceOfLightCapstoneState.MortalCharacteristicBonus} ко всем основным характеристикам.",
            "",
            $"[dim]Pending request: {request.RequestId}[/]"
        };

        return new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🌞 Источник Света ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        };
    }

    private static Panel BuildSourceOfLightLockedPanel(JsonObject shiningRoot, string blocker)
    {
        var lines = new List<string>
        {
            "[bold gold1]Источник Света[/]",
            "",
            "[yellow]Capstone ещё закрыт.[/]",
            $"  • Причина: {Markup.Escape(blocker)}",
            $"  • Требование: radiance.tier={SourceOfLightCapstoneState.RequiredRadianceTier}, radiance.experience>={SourceOfLightCapstoneState.RequiredRadianceExperience}.",
            $"  • Сейчас: radiance.tier={SourceOfLightCapstoneState.GetNodeInt(shiningRoot["radiance"]?["tier"])}, radiance.experience={SourceOfLightCapstoneState.GetNodeInt(shiningRoot["radiance"]?["experience"])}.",
            "",
            "[dim]Команда не создаёт pending-файл и не отправляет GM turn, пока требования не выполнены.[/]"
        };

        return new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Locked Source of Light ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(2, 1),
            Expand = true
        };
    }

    private static Panel BuildSourceOfLightPendingPanel(SourceOfLightCapstoneState.SourceOfLightCapstoneRequest request)
    {
        var lines = new List<string>
        {
            "[bold gold1]Источник Света уже ожидает closure GM.[/]",
            "",
            $"  • requestId: [white]{Markup.Escape(request.RequestId)}[/]",
            $"  • createdAtTurn: [white]{request.CreatedAtTurn}[/]",
            $"  • radiance snapshot: [white]{request.RadianceExperienceAtRequest} XP[/], tier [white]{request.RadianceTierAtRequest}[/]",
            $"  • rewardPassiveId: [white]{Markup.Escape(request.RewardPassiveId)}[/]",
            $"  • rewardRelicId: [white]{Markup.Escape(request.RewardRelicId)}[/]",
            "",
            "[dim]Не создавайте второй запрос; дождитесь accepted/refused repair path или исправьте pending-файл.[/]"
        };

        return new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Pending Source of Light ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        };
    }

    private static Panel BuildSourceOfLightCompletedPanel(JsonObject shiningRoot, JsonObject soulRoot)
    {
        var lines = new List<string>
        {
            "[bold gold1]Источник Света завершён.[/]",
            "",
            $"  • Воплощение Света: [white]{(SourceOfLightCapstoneState.HasLightIncarnate(soulRoot) ? "получено" : "не найдено в soul_state.afterlifeCombatProfile")}[/]",
            $"  • Воплощенный Свет: [white]{SourceOfLightCapstoneState.CountIncarnatedLightRelics(soulRoot)}[/] экземпляр(ов) в soulRelics.",
            $"  • Shining marker: [white]{(SourceOfLightCapstoneState.HasCompletedCapstone(shiningRoot) ? "completed" : "не найден")}[/]",
            "",
            "[dim]Награда one-per-soul; повторный запуск не создаёт новый pending request и не дублирует реликвию/пассив.[/]"
        };

        return new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Completed Source of Light ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        };
    }

    private static JsonObject BuildSourceOfLightRequestPreview(
        SourceOfLightCapstoneState.SourceOfLightCapstoneRequest request,
        JsonObject shiningRoot,
        JsonObject soulRoot) =>
        new()
        {
            ["requestId"] = request.RequestId,
            ["createdAtTurn"] = request.CreatedAtTurn,
            ["createdAtUtc"] = request.CreatedAtUtc,
            ["radianceExperienceAtRequest"] = request.RadianceExperienceAtRequest,
            ["radianceTierAtRequest"] = request.RadianceTierAtRequest,
            ["rewardPassiveId"] = request.RewardPassiveId,
            ["rewardRelicId"] = request.RewardRelicId,
            ["expectedClosure"] = new JsonObject
            {
                ["shiningState"] = SourceOfLightCapstoneState.CreateCompletedShiningMarker(request),
                ["soulCapstone"] = SourceOfLightCapstoneState.CreateLightIncarnatePassive(request),
                ["soulRelic"] = SourceOfLightCapstoneState.CreateIncarnatedLightRelic(request)
            },
            ["before"] = new JsonObject
            {
                ["radiance"] = shiningRoot["radiance"]?.DeepClone(),
                ["hasLightIncarnate"] = SourceOfLightCapstoneState.HasLightIncarnate(soulRoot),
                ["incarnatedLightRelicCount"] = SourceOfLightCapstoneState.CountIncarnatedLightRelics(soulRoot)
            }
        };

    private async Task<string?> TryDescribeSourceOfLightPendingBlockerAsync(JsonObject shiningRoot)
    {
        if (_fs.FileExists("input/turn_request.json") ||
            _fs.FileExists("game_state/control/pending_turn_snapshot.json") ||
            HasAnyShiningTreasuryPendingTurnSnapshotFile())
        {
            return "Источник Света заблокирован: найден активный GM-turn lifecycle. Дождитесь завершения, отмены или repair текущего хода.";
        }

        var coreState = await ShiningCoreActionRequestState.ReadRequestsStateAsync(_fs);
        if (coreState.IsMalformed || coreState.Requests.Count > 0)
            return $"Источник Света заблокирован: есть active/malformed {ShiningCoreActionRequestState.PendingActionsRequestPath}.";

        var tradeState = await ShiningTradeRequestState.ReadRequestsStateAsync(_fs);
        if (tradeState.IsMalformed || tradeState.Requests.Count > 0)
            return $"Источник Света заблокирован: есть active/malformed {ShiningTradeRequestState.PendingRequestsPath}.";

        var foundingMalformed = await ShiningFactionRequestState.IsRequestFileMalformedAsync(
            _fs,
            ShiningFactionRequestState.PendingFoundingsRequestPath,
            static json => JsonSerializer.Deserialize<ShiningFactionRequestState.PendingShiningFactionFoundingRequest>(json, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        if (foundingMalformed || (await ShiningFactionRequestState.ReadFoundingRequestsAsync(_fs)).Count > 0)
            return $"Источник Света заблокирован: есть active/malformed {ShiningFactionRequestState.PendingFoundingsRequestPath}.";

        var realignmentMalformed = await ShiningFactionRequestState.IsRequestFileMalformedAsync(
            _fs,
            ShiningFactionRequestState.PendingRealignmentsRequestPath,
            static json => JsonSerializer.Deserialize<ShiningFactionRequestState.PendingShiningFactionRealignmentRequest>(json, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        if (realignmentMalformed || (await ShiningFactionRequestState.ReadRealignmentRequestsAsync(_fs)).Count > 0)
            return $"Источник Света заблокирован: есть active/malformed {ShiningFactionRequestState.PendingRealignmentsRequestPath}.";

        var leadershipMalformed = await ShiningFactionRequestState.IsRequestFileMalformedAsync(
            _fs,
            ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
            static json => JsonSerializer.Deserialize<ShiningFactionRequestState.PendingShiningFactionLeadershipTransitionRequest>(json, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        if (leadershipMalformed || (await ShiningFactionRequestState.ReadLeadershipTransitionRequestsAsync(_fs)).Count > 0)
            return $"Источник Света заблокирован: есть active/malformed {ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath}.";

        if (shiningRoot.TryGetPropertyValue("pendingNativeFactionDiscovery", out var pendingDiscovery) &&
            pendingDiscovery != null)
        {
            return $"Источник Света заблокирован: есть legacy pendingNativeFactionDiscovery в {ShiningAbodeState.StatePath}.";
        }

        foreach (var path in SourceOfLightBlockingAfterlifePendingPaths)
        {
            if (_fs.FileExists(path))
                return $"Источник Света заблокирован: есть active/malformed afterlife pending/control contract {path}.";
        }

        return null;
    }

    private static readonly string[] SourceOfLightBlockingAfterlifePendingPaths =
    {
        GuardianAbodeOfferingState.PendingRequestPath,
        GuardianTradeRequestState.PendingRequestPath,
        PlayerGuardianFoundationState.PendingRequestPath,
        SystemGuardianLibraryService.AttractionRequestPath,
        AfterlifeArchiveActionState.ConsultationRequestPath,
        AfterlifeArchiveActionState.ProjectFuelRequestPath,
        GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
        GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
        GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
        ActorSocialInteractionRequestState.PendingGuardianRequestPath,
        ActorSocialInteractionRequestState.PendingNpcRequestPath,
        NpcTradeRequestState.PendingRequestPath
    };
}
