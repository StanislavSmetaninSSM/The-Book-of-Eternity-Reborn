using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private const string SoulStatePath = "game_state/meta/soul_state.json";

    private enum ShiningTreasuryAction
    {
        Deposit,
        Withdraw,
        ClaimInterest,
        Exchange,
        Back
    }

    private async Task ShowShiningTreasuryAsync()
    {
        if (!EnsureActiveShiningAbodeAvailable("Казначейство Сияющей Обители"))
            return;

        while (true)
        {
            var activeTurnBlocker = TryDescribeShiningTreasuryActiveTurnBlocker();
            if (activeTurnBlocker != null)
            {
                ShowEmptyPanel("Казначейство Сияющей Обители", activeTurnBlocker);
                WaitForKey();
                return;
            }

            var pendingCostBlocker = await TryDescribeShiningTreasuryPendingCostBlockerAsync();
            if (pendingCostBlocker != null)
            {
                ShowEmptyPanel("Казначейство Сияющей Обители", pendingCostBlocker);
                WaitForKey();
                return;
            }

            var context = await LoadShiningContextAsync();
            if (context?.SoulRoot == null)
            {
                ShowEmptyPanel("Казначейство Сияющей Обители", "soul_state.json недоступен; локальные операции казначейства заблокированы.");
                WaitForKey();
                return;
            }

            if (!EnsureNoMalformedTreasuryForLocalShiningSave(context.Root))
            {
                WaitForKey();
                return;
            }

            Clear();
            Write(BuildShiningTreasuryPanel(context.Root, context.SoulRoot));

            var choice = Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]Казначейство Сияющей Обители[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .AddChoices(
                    "⬇ Внести Чернильные Перья",
                    "⬆ Вывести Чернильные Перья",
                    "✨ Получить проценты",
                    "🔁 Обменять Перья на Искры",
                    "← Назад"));

            var action = choice switch
            {
                var text when text.Contains("Внести", StringComparison.OrdinalIgnoreCase) => ShiningTreasuryAction.Deposit,
                var text when text.Contains("Вывести", StringComparison.OrdinalIgnoreCase) => ShiningTreasuryAction.Withdraw,
                var text when text.Contains("проценты", StringComparison.OrdinalIgnoreCase) => ShiningTreasuryAction.ClaimInterest,
                var text when text.Contains("Обменять", StringComparison.OrdinalIgnoreCase) => ShiningTreasuryAction.Exchange,
                _ => ShiningTreasuryAction.Back
            };

            if (action == ShiningTreasuryAction.Back)
                return;

            await HandleShiningTreasuryActionAsync(context, action);
        }
    }

    private Panel BuildShiningTreasuryPanel(JsonObject shiningRoot, JsonObject soulRoot)
    {
        var treasury = ShiningAbodeState.EnsureTreasuryObject(shiningRoot);
        var spendableFeathers = ShiningAbodeState.GetSoulSpendableInkFeathers(soulRoot);
        var deposited = GetNodeInt(treasury["depositedInkFeathers"]);
        var claimable = GetNodeInt(treasury["claimableInkFeatherInterest"]);
        var cycleId = ShiningAbodeState.ResolveTreasuryCycleId(shiningRoot, soulRoot);
        var alreadySettled = string.Equals(
            GetNodeString(treasury["lastInterestSettlementCycleId"]),
            cycleId,
            StringComparison.OrdinalIgnoreCase);
        var pendingInterest = alreadySettled ? 0 : ShiningAbodeState.ComputeTreasuryInterestForCycle(deposited);
        var rateBasisPoints = ShiningAbodeState.GetTreasuryInterestBasisPoints(deposited);
        var exchangedThisCycle = ShiningAbodeState.GetTreasuryExchangeThisCycle(treasury, cycleId);
        var exchangeRemaining = Math.Max(0, ShiningAbodeState.TreasuryMaxLightSparksExchangePerCycle - exchangedThisCycle);

        var lines = new List<string>
        {
            "[bold yellow]🏦 Казначейство Сияющей Обители[/]",
            "",
            "[bold]Локальная экономика:[/] [dim]GM turn не создаётся; казначейство не имеет receipts.[/]",
            $"  • Доступные Чернильные Перья: [white]{spendableFeathers}[/]",
            $"  • Вклад Чернильных Перьев: [white]{deposited}[/]",
            $"  • Проценты к получению: [white]{claimable}[/] [dim](ожидаемое начисление за текущий цикл: {pendingInterest})[/]",
            $"  • Ставка вклада: [white]{rateBasisPoints / 100.0:0.##}%[/] за Shining return cycle [dim](cap {ShiningAbodeState.TreasuryInterestClaimCap} 🪶/cycle)[/]",
            $"  • Искры Света: [gold1]{GetNodeInt(shiningRoot["lightSparks"])}[/]",
            $"  • Обмен: [white]{ShiningAbodeState.TreasuryFeathersPerLightSpark} 🪶 = 1 ✨[/], лимит [white]{ShiningAbodeState.TreasuryMaxLightSparksExchangePerCycle} ✨[/]/cycle, осталось [white]{exchangeRemaining}[/]",
            $"  • Текущий цикл: [dim]{Markup.Escape(cycleId)}[/]",
            "",
            "[bold]Ограничения:[/]",
            "  • Вклад принимает только Чернильные Перья.",
            "  • Искры Света нельзя сдавать под процент и нельзя менять обратно на Перья.",
            "  • Обмен дорогой и capped, чтобы не ломать прокачку Сияния."
        };

        return new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🏦 Shining Treasury ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        };
    }

    private async Task HandleShiningTreasuryActionAsync(ShiningContext context, ShiningTreasuryAction action)
    {
        if (context.SoulRoot == null)
            return;

        var amount = action switch
        {
            ShiningTreasuryAction.Deposit => PromptPositiveInteger("Сколько Чернильных Перьев внести?"),
            ShiningTreasuryAction.Withdraw => PromptPositiveInteger("Сколько Чернильных Перьев вывести?"),
            ShiningTreasuryAction.Exchange => PromptPositiveInteger("Сколько Искр Света получить?"),
            _ => 0
        };

        if (action != ShiningTreasuryAction.ClaimInterest && amount <= 0)
        {
            MarkupLine("[yellow]Операция отменена: нужна положительная integer сумма.[/]");
            WaitForKey();
            return;
        }

        var projectedShiningRoot = CloneTreasuryRoot(context.Root);
        var projectedSoulRoot = CloneTreasuryRoot(context.SoulRoot);
        var result = action switch
        {
            ShiningTreasuryAction.Deposit => ShiningAbodeState.DepositTreasuryInkFeathers(projectedShiningRoot, projectedSoulRoot, amount),
            ShiningTreasuryAction.Withdraw => ShiningAbodeState.WithdrawTreasuryInkFeathers(projectedShiningRoot, projectedSoulRoot, amount),
            ShiningTreasuryAction.ClaimInterest => ShiningAbodeState.ClaimTreasuryInterest(projectedShiningRoot, projectedSoulRoot),
            ShiningTreasuryAction.Exchange => ShiningAbodeState.ExchangeTreasuryInkFeathersForLightSparks(projectedShiningRoot, projectedSoulRoot, amount),
            _ => new ShiningAbodeState.TreasuryOperationResult(false, "Операция не выбрана.")
        };

        if (!result.Success)
        {
            ShowEmptyPanel("Казначейство Сияющей Обители", result.Message);
            WaitForKey();
            return;
        }

        ShiningAbodeState.NormalizeStateRoot(projectedShiningRoot, context.ResidentRoot, context.GuardiansRoot);
        Write(BuildShiningTreasuryPreviewPanel(action, amount, result, context.Root, context.SoulRoot, projectedShiningRoot, projectedSoulRoot));
        WriteJsonAuditPanel("JSON локальной операции казначейства", BuildShiningTreasuryAuditNode(action, amount, result, context.Root, context.SoulRoot, projectedShiningRoot, projectedSoulRoot), Color.Gold1);

        if (!Confirm("[yellow]Подтвердить локальную операцию казначейства?[/]", false))
        {
            MarkupLine("[dim]Операция отменена; состояние не изменено.[/]");
            WaitForKey();
            return;
        }

        if (!await SaveShiningTreasuryRootsAsync(projectedShiningRoot, projectedSoulRoot, context.ResidentRoot, context.GuardiansRoot))
        {
            WaitForKey();
            return;
        }

        MarkupLine($"[green]{Markup.Escape(result.Message)}[/]");
        WaitForKey();
    }

    private int PromptPositiveInteger(string title)
    {
        var raw = Ask($"[cyan]{Markup.Escape(title)}[/]", "0");
        return int.TryParse(raw, out var amount) ? amount : 0;
    }

    private Panel BuildShiningTreasuryPreviewPanel(
        ShiningTreasuryAction action,
        int amount,
        ShiningAbodeState.TreasuryOperationResult result,
        JsonObject beforeShiningRoot,
        JsonObject beforeSoulRoot,
        JsonObject afterShiningRoot,
        JsonObject afterSoulRoot)
    {
        var beforeTreasury = ShiningAbodeState.EnsureTreasuryObject(beforeShiningRoot);
        var afterTreasury = ShiningAbodeState.EnsureTreasuryObject(afterShiningRoot);
        var lines = new List<string>
        {
            "[bold yellow]Предпросмотр локальной операции казначейства[/]",
            "",
            $"  • Операция: [white]{Markup.Escape(DescribeShiningTreasuryAction(action))}[/]",
            action == ShiningTreasuryAction.ClaimInterest
                ? $"  • Начислено процентов при проверке цикла: [white]{result.InterestGenerated}[/]"
                : $"  • Заявленная сумма: [white]{amount}[/]",
            $"  • Чернильные Перья spendable: [white]{ShiningAbodeState.GetSoulSpendableInkFeathers(beforeSoulRoot)}[/] -> [white]{ShiningAbodeState.GetSoulSpendableInkFeathers(afterSoulRoot)}[/]",
            $"  • Вклад: [white]{GetNodeInt(beforeTreasury["depositedInkFeathers"])}[/] -> [white]{GetNodeInt(afterTreasury["depositedInkFeathers"])}[/]",
            $"  • Проценты к получению: [white]{GetNodeInt(beforeTreasury["claimableInkFeatherInterest"])}[/] -> [white]{GetNodeInt(afterTreasury["claimableInkFeatherInterest"])}[/]",
            $"  • Искры Света: [white]{GetNodeInt(beforeShiningRoot["lightSparks"])}[/] -> [white]{GetNodeInt(afterShiningRoot["lightSparks"])}[/]",
            "",
            "[dim]Это client-owned операция. GM не получает ход, receipt или report.[/]"
        };

        return new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Treasury Preview ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        };
    }

    private static JsonObject BuildShiningTreasuryAuditNode(
        ShiningTreasuryAction action,
        int amount,
        ShiningAbodeState.TreasuryOperationResult result,
        JsonObject beforeShiningRoot,
        JsonObject beforeSoulRoot,
        JsonObject afterShiningRoot,
        JsonObject afterSoulRoot) =>
        new()
        {
            ["sourceSurface"] = "shining_treasury_local_operation",
            ["operation"] = DescribeShiningTreasuryActionToken(action),
            ["requestedAmount"] = amount,
            ["gmTurnSent"] = false,
            ["receiptWritten"] = false,
            ["message"] = result.Message,
            ["constraints"] = new JsonObject
            {
                ["onlyInkFeathersCanBeDeposited"] = true,
                ["lightSparkDepositAllowed"] = false,
                ["reverseExchangeAllowed"] = false,
                ["feathersPerLightSpark"] = ShiningAbodeState.TreasuryFeathersPerLightSpark,
                ["maxLightSparksExchangePerCycle"] = ShiningAbodeState.TreasuryMaxLightSparksExchangePerCycle,
                ["interestClaimCapPerCycle"] = ShiningAbodeState.TreasuryInterestClaimCap
            },
            ["before"] = new JsonObject
            {
                ["soulInkFeathersCurrent"] = ShiningAbodeState.GetSoulSpendableInkFeathers(beforeSoulRoot),
                ["lightSparks"] = GetNodeInt(beforeShiningRoot["lightSparks"]),
                ["treasury"] = beforeShiningRoot[ShiningAbodeState.TreasuryProperty]?.DeepClone()
            },
            ["after"] = new JsonObject
            {
                ["soulInkFeathersCurrent"] = ShiningAbodeState.GetSoulSpendableInkFeathers(afterSoulRoot),
                ["lightSparks"] = GetNodeInt(afterShiningRoot["lightSparks"]),
                ["treasury"] = afterShiningRoot[ShiningAbodeState.TreasuryProperty]?.DeepClone()
            },
            ["affectedFiles"] = new JsonArray(SoulStatePath, ShiningAbodeState.StatePath)
        };

    private async Task<bool> SaveShiningTreasuryRootsAsync(
        JsonObject shiningRoot,
        JsonObject soulRoot,
        JsonObject? residentRoot,
        JsonObject? guardiansRoot)
    {
        var activeTurnBlocker = TryDescribeShiningTreasuryActiveTurnBlocker();
        if (activeTurnBlocker != null)
        {
            ShowEmptyPanel("Казначейство Сияющей Обители", activeTurnBlocker);
            return false;
        }

        var pendingCostBlocker = await TryDescribeShiningTreasuryPendingCostBlockerAsync();
        if (pendingCostBlocker != null)
        {
            ShowEmptyPanel("Казначейство Сияющей Обители", pendingCostBlocker);
            return false;
        }

        if (!EnsureNoMalformedLegacyPendingDiscoveryForLocalShiningSave(shiningRoot))
            return false;

        if (!EnsureNoMalformedTreasuryForLocalShiningSave(shiningRoot))
            return false;

        var previousShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        var previousSoulJson = await _fs.ReadFileAsync(SoulStatePath);
        JsonObject? previousSoulRoot = null;

        if (!string.IsNullOrWhiteSpace(previousShiningJson))
        {
            try
            {
                var liveRoot = JsonNode.Parse(previousShiningJson) as JsonObject;
                if (liveRoot == null ||
                    !EnsureNoMalformedLegacyPendingDiscoveryForLocalShiningSave(liveRoot) ||
                    !EnsureNoMalformedTreasuryForLocalShiningSave(liveRoot))
                {
                    return false;
                }
            }
            catch
            {
                MarkupLine($"[yellow]Нельзя локально сохранить {Markup.Escape(ShiningAbodeState.StatePath)}: live Shining state повреждён. Сначала выполните repair.[/]");
                return false;
            }
        }

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
                MarkupLine("[red]Казначейство не может сохранить операцию: текущий soul_state.json нечитаем. Сначала исправь состояние души.[/]");
                return false;
            }
        }

        try
        {
            ShiningAbodeState.NormalizeStateRoot(shiningRoot, residentRoot, guardiansRoot);
            await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            await WriteCanonicalSoulStateJsonAsync(soulRoot);
            await _stateManager.RefreshGameStateAsync();
            return true;
        }
        catch (Exception ex)
        {
            if (previousShiningJson == null)
                _fs.DeleteFile(ShiningAbodeState.StatePath);
            else
                await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, previousShiningJson);

            if (previousSoulJson == null)
                _fs.DeleteFile(SoulStatePath);
            else if (previousSoulRoot != null)
                await WriteCanonicalSoulStateJsonAsync(previousSoulRoot);
            else
                _fs.DeleteFile(SoulStatePath);

            MarkupLine($"[red]Не удалось сохранить казначейство; состояние восстановлено: {Markup.Escape(ex.Message)}[/]");
            return false;
        }
    }

    private string? TryDescribeShiningTreasuryActiveTurnBlocker()
    {
        return AfterlifeLocalActionGuard.TryDescribeActiveGmTurnLifecycleBlocker(
            _fs,
            "Казначейство",
            "client-owned surfaces shining_abode_state.json.treasury и soul_state.json.inkFeathers");
    }

    private bool HasAnyShiningTreasuryPendingTurnSnapshotFile() =>
        AfterlifeLocalActionGuard.HasAnyPendingTurnSnapshotFile(_fs);

    private async Task<string?> TryDescribeShiningTreasuryPendingCostBlockerAsync()
    {
        var coreBlocker = await TryDescribeShiningTreasuryCoreActionPendingCostBlockerAsync();
        if (coreBlocker != null)
            return coreBlocker;

        var foundingMalformed = await ShiningFactionRequestState.IsRequestFileMalformedAsync(
            _fs,
            ShiningFactionRequestState.PendingFoundingsRequestPath,
            static json => JsonSerializer.Deserialize<ShiningFactionRequestState.PendingShiningFactionFoundingRequest>(
                json,
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        if (foundingMalformed)
        {
            return $"Казначейство заблокировано: {ShiningFactionRequestState.PendingFoundingsRequestPath} повреждён. Исправьте или закройте pending founding contract перед локальными операциями с Перьями.";
        }

        var foundingRequests = await ShiningFactionRequestState.ReadFoundingRequestsAsync(_fs);
        var pendingFounding = foundingRequests.FirstOrDefault();
        if (pendingFounding != null)
        {
            if (pendingFounding.QuotedCostFeathers != ShiningFactionRequestState.FactionFoundingCostFeathers ||
                pendingFounding.QuotedCostLightSparks != ShiningFactionRequestState.FactionFoundingCostLightSparks)
            {
                return $"Казначейство заблокировано: pending founding request {pendingFounding.RequestId} содержит noncanonical quoted costs {pendingFounding.QuotedCostFeathers} 🪶 / {pendingFounding.QuotedCostLightSparks} ✨. Ожидается ровно {ShiningFactionRequestState.FactionFoundingCostFeathers} 🪶 / {ShiningFactionRequestState.FactionFoundingCostLightSparks} ✨; выполните repair или closure перед локальными операциями.";
            }

            return $"Казначейство заблокировано: pending founding request {pendingFounding.RequestId} уже зафиксировал стоимость {pendingFounding.QuotedCostFeathers} 🪶 / {pendingFounding.QuotedCostLightSparks} ✨. Дождитесь accepted/refused/withdrawn closure перед локальными операциями.";
        }

        var legacyDiscoveryBlocker = await TryDescribeShiningTreasuryLegacyDiscoveryBlockerAsync();
        if (legacyDiscoveryBlocker != null)
            return legacyDiscoveryBlocker;

        return null;
    }

    private async Task<string?> TryDescribeShiningTreasuryLegacyDiscoveryBlockerAsync()
    {
        var shiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        if (string.IsNullOrWhiteSpace(shiningJson))
            return null;

        JsonObject? shiningRoot;
        try
        {
            shiningRoot = JsonNode.Parse(shiningJson) as JsonObject;
        }
        catch
        {
            return null;
        }

        if (shiningRoot == null ||
            !shiningRoot.TryGetPropertyValue("pendingNativeFactionDiscovery", out var pendingDiscovery) ||
            pendingDiscovery == null)
        {
            return null;
        }

        if (pendingDiscovery is not JsonObject discovery)
        {
            return $"Казначейство заблокировано: {ShiningAbodeState.StatePath}.pendingNativeFactionDiscovery повреждён. Исправьте или закройте legacy discovery contract перед локальными операциями с Перьями.";
        }

        var requestId = GetNodeString(discovery["requestId"]) ?? "unknown";
        var costFeathers = GetNodeInt(discovery["costFeathers"]);
        var costLightSparks = GetNodeInt(discovery["costLightSparks"]);
        return $"Казначейство заблокировано: legacy pendingNativeFactionDiscovery {requestId} уже зафиксировал стоимость {costFeathers} 🪶 / {costLightSparks} ✨. Дождитесь accepted/refused/repair closure перед локальными операциями.";
    }

    private async Task<string?> TryDescribeShiningTreasuryCoreActionPendingCostBlockerAsync()
    {
        var pendingState = await ShiningCoreActionRequestState.ReadRequestsStateAsync(_fs);
        if (pendingState.IsMalformed)
            return $"Казначейство заблокировано: {ShiningCoreActionRequestState.PendingActionsRequestPath} повреждён. Исправьте или закройте pending core action contract перед локальными операциями с Перьями.";

        if (pendingState.Requests.Count == 0)
            return null;

        var rawJson = await _fs.ReadFileAsync(ShiningCoreActionRequestState.PendingActionsRequestPath);
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (!document.RootElement.TryGetProperty(ShiningCoreActionRequestState.RequestsProperty, out var requestsNode) ||
                requestsNode.ValueKind != JsonValueKind.Array)
            {
                return $"Казначейство заблокировано: {ShiningCoreActionRequestState.PendingActionsRequestPath} не содержит machine-readable requests[].";
            }

            foreach (var requestNode in requestsNode.EnumerateArray())
            {
                if (requestNode.ValueKind != JsonValueKind.Object)
                    return $"Казначейство заблокировано: {ShiningCoreActionRequestState.PendingActionsRequestPath} содержит malformed request entry.";

                var requestId = ReadJsonString(requestNode, "requestId") ?? "unknown";
                var actionType = ReadJsonString(requestNode, "actionType") ?? "";
                var hasFeathers = TryReadNonNegativeJsonInt(requestNode, "quotedCostFeathers", out var feathers);
                var hasLightSparks = TryReadNonNegativeJsonInt(requestNode, "quotedCostLightSparks", out var lightSparks);

                if ((hasFeathers && feathers > 0) || (hasLightSparks && lightSparks > 0))
                {
                    return $"Казначейство заблокировано: pending Shining core action {requestId} уже зафиксировал стоимость {feathers} 🪶 / {lightSparks} ✨. Дождитесь accepted/refused/withdrawn closure перед локальными операциями.";
                }

                if ((!hasFeathers || !hasLightSparks) && IsPotentiallyCostBearingShiningCoreAction(actionType))
                {
                    return $"Казначейство заблокировано: pending Shining core action {requestId} не имеет читаемых quotedCostFeathers/quotedCostLightSparks. Сначала исправьте pending contract.";
                }
            }
        }
        catch
        {
            return $"Казначейство заблокировано: {ShiningCoreActionRequestState.PendingActionsRequestPath} не удалось прочитать как JSON.";
        }

        return null;
    }

    private static bool IsPotentiallyCostBearingShiningCoreAction(string? actionType)
    {
        return actionType?.Trim().ToLowerInvariant() is
            ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction or
            ShiningCoreActionRequestState.ActionTypeInvestInFaction or
            ShiningCoreActionRequestState.ActionTypeCompleteProject or
            ShiningCoreActionRequestState.ActionTypePullRelicGacha or
            ShiningCoreActionRequestState.ActionTypeForgeRelicReshape or
            ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty or
            ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand or
            ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho or
            ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity;
    }

    private static string? ReadJsonString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool TryReadNonNegativeJsonInt(JsonElement root, string propertyName, out int value)
    {
        value = 0;
        return root.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out value) &&
               value >= 0;
    }

    private Task WriteCanonicalSoulStateJsonAsync(JsonObject soulRoot) =>
        _fs.WriteFileAtomicAsync(
            SoulStatePath,
            GuardianPolicyContracts.CreateCanonicalSoulStateWriteRoot(soulRoot)
                .ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

    private static JsonObject CloneTreasuryRoot(JsonObject root) =>
        JsonNode.Parse(root.ToJsonString())!.AsObject();

    private static string DescribeShiningTreasuryAction(ShiningTreasuryAction action) =>
        action switch
        {
            ShiningTreasuryAction.Deposit => "внести Чернильные Перья",
            ShiningTreasuryAction.Withdraw => "вывести Чернильные Перья",
            ShiningTreasuryAction.ClaimInterest => "получить проценты",
            ShiningTreasuryAction.Exchange => "обменять Чернильные Перья на Искры Света",
            _ => "назад"
        };

    private static string DescribeShiningTreasuryActionToken(ShiningTreasuryAction action) =>
        action switch
        {
            ShiningTreasuryAction.Deposit => "deposit_ink_feathers",
            ShiningTreasuryAction.Withdraw => "withdraw_ink_feathers",
            ShiningTreasuryAction.ClaimInterest => "claim_ink_feather_interest",
            ShiningTreasuryAction.Exchange => "exchange_ink_feathers_for_light_sparks",
            _ => "back"
        };
}
