using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class ShiningAbodeState
{
    public const string TreasuryProperty = "treasury";
    public const int TreasuryFeathersPerLightSpark = 25;
    public const int TreasuryMaxLightSparksExchangePerCycle = 3;
    public const int TreasuryInterestClaimCap = 5;

    public sealed record TreasuryOperationResult(
        bool Success,
        string Message,
        int InkFeathersDelta = 0,
        int LightSparksDelta = 0,
        int InterestGenerated = 0);

    public static JsonObject BuildDefaultTreasuryObject() => new()
    {
        ["depositedInkFeathers"] = 0,
        ["claimableInkFeatherInterest"] = 0,
        ["totalInterestClaimed"] = 0,
        ["lastInterestSettlementCycleId"] = string.Empty,
        ["exchangeCycleId"] = string.Empty,
        ["exchangeThisCycleLightSparks"] = 0,
        ["exchangeHistory"] = new JsonArray()
    };

    public static JsonObject EnsureTreasuryObject(JsonObject root)
    {
        if (!root.ContainsKey(TreasuryProperty))
        {
            root[TreasuryProperty] = BuildDefaultTreasuryObject();
        }

        if (root[TreasuryProperty] is not JsonObject treasury)
            throw new InvalidOperationException(ValidateTreasuryShape(root) ?? "treasury должен быть JSON object.");

        NormalizeTreasuryObject(root);
        return treasury;
    }

    public static string? ValidateTreasuryShape(JsonObject root)
    {
        if (root.ContainsKey(TreasuryProperty) &&
            root[TreasuryProperty] is not JsonObject)
        {
            return "treasury повреждён; казначейство должно быть JSON object или отсутствовать для legacy default.";
        }

        return null;
    }

    public static void NormalizeTreasuryObject(JsonObject root)
    {
        if (!root.ContainsKey(TreasuryProperty))
        {
            root[TreasuryProperty] = BuildDefaultTreasuryObject();
            return;
        }

        if (root[TreasuryProperty] is not JsonObject treasury)
            return;

        treasury["depositedInkFeathers"] = Math.Max(0, GetNodeInt(treasury["depositedInkFeathers"], 0));
        treasury["claimableInkFeatherInterest"] = Math.Max(0, GetNodeInt(treasury["claimableInkFeatherInterest"], 0));
        treasury["totalInterestClaimed"] = Math.Max(0, GetNodeInt(treasury["totalInterestClaimed"], 0));
        treasury["lastInterestSettlementCycleId"] = GetNodeString(treasury["lastInterestSettlementCycleId"]) ?? string.Empty;
        treasury["exchangeCycleId"] = GetNodeString(treasury["exchangeCycleId"]) ?? string.Empty;
        treasury["exchangeThisCycleLightSparks"] = Math.Clamp(
            GetNodeInt(treasury["exchangeThisCycleLightSparks"], 0),
            0,
            TreasuryMaxLightSparksExchangePerCycle);

        if (treasury["exchangeHistory"] is not JsonArray)
            treasury["exchangeHistory"] = new JsonArray();
    }

    public static int GetTreasuryInterestBasisPoints(int depositedInkFeathers) =>
        Math.Max(0, depositedInkFeathers) switch
        {
            >= 1000 => 150,
            >= 500 => 125,
            >= 250 => 100,
            >= 100 => 75,
            > 0 => 50,
            _ => 0
        };

    public static int ComputeTreasuryInterestForCycle(int depositedInkFeathers)
    {
        var deposited = Math.Max(0, depositedInkFeathers);
        var basisPoints = GetTreasuryInterestBasisPoints(deposited);
        var generated = (long)deposited * basisPoints / 10000;
        return (int)Math.Min(TreasuryInterestClaimCap, generated);
    }

    public static string ResolveTreasuryCycleId(JsonObject shiningRoot, JsonObject? soulRoot)
    {
        return TryResolveTreasuryCycleId(
                shiningRoot,
                soulRoot,
                synchronizeCurrentCycle: false,
                out var cycleId,
                out _)
            ? cycleId
            : GetNodeString(shiningRoot["gachaSystem"]?["currentReturnCycleId"]) ?? "shining_return_cycle_mismatch";
    }

    public static int GetTreasuryExchangeThisCycle(JsonObject treasury, string? cycleId)
    {
        if (string.IsNullOrWhiteSpace(cycleId) ||
            !string.Equals(GetNodeString(treasury["exchangeCycleId"]), cycleId, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return Math.Clamp(
            GetNodeInt(treasury["exchangeThisCycleLightSparks"], 0),
            0,
            TreasuryMaxLightSparksExchangePerCycle);
    }

    public static bool TryResolveTreasuryCycleId(
        JsonObject shiningRoot,
        JsonObject? soulRoot,
        bool synchronizeCurrentCycle,
        out string cycleId,
        out string? error)
    {
        cycleId = string.Empty;
        error = null;

        var gachaSystem = EnsureGachaSystemObject(shiningRoot);
        var currentReturnCycleId = GetNodeString(gachaSystem["currentReturnCycleId"]) ?? string.Empty;
        var hasCurrentIncarnation = soulRoot?["currentIncarnation"] != null;
        if (!hasCurrentIncarnation)
        {
            if (!string.IsNullOrWhiteSpace(currentReturnCycleId))
            {
                cycleId = currentReturnCycleId;
                return true;
            }

            error = "Казначейство требует soul_state.currentIncarnation, чтобы определить текущий Shining return cycle.";
            return false;
        }

        var expectedReturnCycleId = GetTradeCycleId(Math.Max(0, GetNodeInt(soulRoot?["currentIncarnation"], 0)));
        if (string.IsNullOrWhiteSpace(currentReturnCycleId))
        {
            cycleId = expectedReturnCycleId;
            if (synchronizeCurrentCycle)
            {
                gachaSystem["currentReturnCycleId"] = expectedReturnCycleId;
                gachaSystem["chargesUsedThisReturn"] = 0;
            }

            return true;
        }

        if (!string.Equals(currentReturnCycleId, expectedReturnCycleId, StringComparison.OrdinalIgnoreCase))
        {
            error = "Казначейство ждёт синхронизации Shining return cycle. Выйди из меню и вернись в Сияющую Обитель заново.";
            return false;
        }

        cycleId = currentReturnCycleId;
        return true;
    }

    public static int GetSoulSpendableInkFeathers(JsonObject soulRoot)
    {
        if (soulRoot["inkFeathers"] is JsonObject inkFeathers)
            return Math.Max(0, GetNodeInt(inkFeathers["current"], 0));

        return Math.Max(0, GetNodeInt(soulRoot["inkFeathers"], 0));
    }

    private static void SetSoulSpendableInkFeathers(JsonObject soulRoot, int current, int earnedDelta = 0)
    {
        var safeCurrent = Math.Max(0, current);
        var existingNode = soulRoot["inkFeathers"];
        var previousTotal = existingNode is JsonObject existingObject
            ? Math.Max(
                GetNodeInt(existingObject["current"], 0),
                GetNodeInt(existingObject["total"], GetNodeInt(existingObject["current"], 0)))
            : Math.Max(0, GetNodeInt(existingNode, 0));
        var inkFeathers = existingNode as JsonObject ?? new JsonObject();
        inkFeathers["current"] = safeCurrent;
        var nextTotal = Math.Min(int.MaxValue, (long)previousTotal + Math.Max(0, earnedDelta));
        inkFeathers["total"] = Math.Max((int)nextTotal, safeCurrent);
        soulRoot["inkFeathers"] = inkFeathers;
    }

    public static TreasuryOperationResult DepositTreasuryInkFeathers(JsonObject shiningRoot, JsonObject soulRoot, int amount)
    {
        if (amount <= 0)
            return new TreasuryOperationResult(false, "Сумма вклада должна быть положительной.");

        var spendable = GetSoulSpendableInkFeathers(soulRoot);
        if (spendable < amount)
            return new TreasuryOperationResult(false, $"Недостаточно Чернильных Перьев: доступно {spendable}, требуется {amount}.");

        var treasury = EnsureTreasuryObject(shiningRoot);
        var deposited = GetNodeInt(treasury["depositedInkFeathers"], 0);
        var nextDeposited = (long)deposited + amount;
        if (nextDeposited > int.MaxValue)
            return new TreasuryOperationResult(false, "Вклад отклонён: казна не может безопасно вместить такую сумму Чернильных Перьев.");

        SetSoulSpendableInkFeathers(soulRoot, spendable - amount);
        treasury["depositedInkFeathers"] = (int)nextDeposited;
        return new TreasuryOperationResult(true, $"Вклад принят: {amount} Чернильных Перьев.", InkFeathersDelta: -amount);
    }

    public static TreasuryOperationResult WithdrawTreasuryInkFeathers(JsonObject shiningRoot, JsonObject soulRoot, int amount)
    {
        if (amount <= 0)
            return new TreasuryOperationResult(false, "Сумма вывода должна быть положительной.");

        var treasury = EnsureTreasuryObject(shiningRoot);
        var deposited = GetNodeInt(treasury["depositedInkFeathers"], 0);
        if (deposited < amount)
            return new TreasuryOperationResult(false, $"Недостаточно Перьев на вкладе: лежит {deposited}, требуется {amount}.");

        var spendable = GetSoulSpendableInkFeathers(soulRoot);
        var nextSpendable = (long)spendable + amount;
        if (nextSpendable > int.MaxValue)
            return new TreasuryOperationResult(false, "Вывод отклонён: spendable Ink Feathers не может безопасно вместить такую сумму.");

        treasury["depositedInkFeathers"] = deposited - amount;
        SetSoulSpendableInkFeathers(soulRoot, (int)nextSpendable);
        return new TreasuryOperationResult(true, $"Выведено из казны: {amount} Чернильных Перьев.", InkFeathersDelta: amount);
    }

    public static TreasuryOperationResult SettleTreasuryInterest(JsonObject shiningRoot, JsonObject soulRoot)
    {
        var treasury = EnsureTreasuryObject(shiningRoot);
        if (!TryResolveTreasuryCycleId(shiningRoot, soulRoot, synchronizeCurrentCycle: false, out var cycleId, out var error))
            return new TreasuryOperationResult(false, error ?? "Не удалось определить Shining return cycle для казначейства.");

        if (string.Equals(GetNodeString(treasury["lastInterestSettlementCycleId"]), cycleId, StringComparison.OrdinalIgnoreCase))
            return new TreasuryOperationResult(true, "Проценты за этот цикл уже начислены.", InterestGenerated: 0);

        var deposited = GetNodeInt(treasury["depositedInkFeathers"], 0);
        var generated = ComputeTreasuryInterestForCycle(deposited);
        var nextClaimable = (long)GetNodeInt(treasury["claimableInkFeatherInterest"], 0) + generated;
        if (nextClaimable > int.MaxValue)
            return new TreasuryOperationResult(false, "Начисление процентов отклонено: казна не может безопасно вместить claimable Ink Feather interest.");

        SyncTreasuryCurrentReturnCycleIfMissing(shiningRoot, cycleId);
        treasury["claimableInkFeatherInterest"] = (int)nextClaimable;
        treasury["lastInterestSettlementCycleId"] = cycleId;
        return new TreasuryOperationResult(true, $"Начислено процентов: {generated} Чернильных Перьев.", InterestGenerated: generated);
    }

    public static TreasuryOperationResult ClaimTreasuryInterest(JsonObject shiningRoot, JsonObject soulRoot)
    {
        var treasury = EnsureTreasuryObject(shiningRoot);
        if (!TryResolveTreasuryCycleId(shiningRoot, soulRoot, synchronizeCurrentCycle: false, out var cycleId, out var error))
            return new TreasuryOperationResult(false, error ?? "Не удалось определить Shining return cycle для казначейства.");

        var alreadySettled = string.Equals(GetNodeString(treasury["lastInterestSettlementCycleId"]), cycleId, StringComparison.OrdinalIgnoreCase);
        var generated = alreadySettled
            ? 0
            : ComputeTreasuryInterestForCycle(GetNodeInt(treasury["depositedInkFeathers"], 0));
        var nextClaimable = (long)GetNodeInt(treasury["claimableInkFeatherInterest"], 0) + generated;
        if (nextClaimable > int.MaxValue)
            return new TreasuryOperationResult(false, "Получение процентов отклонено: казна не может безопасно вместить claimable Ink Feather interest.");

        var claimable = (int)nextClaimable;
        if (claimable <= 0)
        {
            if (!alreadySettled)
            {
                SyncTreasuryCurrentReturnCycleIfMissing(shiningRoot, cycleId);
                treasury["claimableInkFeatherInterest"] = 0;
                treasury["lastInterestSettlementCycleId"] = cycleId;
            }

            return new TreasuryOperationResult(true, "В казне нет процентов к получению.", InterestGenerated: generated);
        }

        var spendable = GetSoulSpendableInkFeathers(soulRoot);
        var nextSpendable = (long)spendable + claimable;
        if (nextSpendable > int.MaxValue)
            return new TreasuryOperationResult(false, "Получение процентов отклонено: spendable Ink Feathers не может безопасно вместить такую сумму.");

        var nextTotalInterestClaimed = (long)GetNodeInt(treasury["totalInterestClaimed"], 0) + claimable;
        if (nextTotalInterestClaimed > int.MaxValue)
            return new TreasuryOperationResult(false, "Получение процентов отклонено: totalInterestClaimed не может безопасно вместить такую сумму.");

        if (!alreadySettled)
        {
            SyncTreasuryCurrentReturnCycleIfMissing(shiningRoot, cycleId);
            treasury["lastInterestSettlementCycleId"] = cycleId;
        }

        treasury["claimableInkFeatherInterest"] = 0;
        treasury["totalInterestClaimed"] = (int)nextTotalInterestClaimed;
        SetSoulSpendableInkFeathers(soulRoot, (int)nextSpendable, earnedDelta: claimable);
        return new TreasuryOperationResult(true, $"Получены проценты: {claimable} Чернильных Перьев.", InkFeathersDelta: claimable, InterestGenerated: generated);
    }

    public static TreasuryOperationResult ExchangeTreasuryInkFeathersForLightSparks(JsonObject shiningRoot, JsonObject soulRoot, int lightSparks)
    {
        if (lightSparks <= 0)
            return new TreasuryOperationResult(false, "Количество Искр Света должно быть положительным.");

        if (!TryResolveTreasuryCycleId(shiningRoot, soulRoot, synchronizeCurrentCycle: false, out var cycleId, out var error))
            return new TreasuryOperationResult(false, error ?? "Не удалось определить Shining return cycle для обмена.");

        var treasury = EnsureTreasuryObject(shiningRoot);
        var exchangedThisCycle = GetTreasuryExchangeThisCycle(treasury, cycleId);
        var remainingCycleSparks = Math.Max(0, TreasuryMaxLightSparksExchangePerCycle - exchangedThisCycle);
        if (lightSparks > remainingCycleSparks)
        {
            return new TreasuryOperationResult(
                false,
                $"Лимит обмена за цикл: {TreasuryMaxLightSparksExchangePerCycle} Искры Света; уже обменяно {exchangedThisCycle}.");
        }

        var currentLightSparks = Math.Clamp(GetNodeInt(shiningRoot["lightSparks"], 0), 0, 100);
        var remainingLightSparkCapacity = Math.Max(0, 100 - currentLightSparks);
        if (lightSparks > remainingLightSparkCapacity)
            return new TreasuryOperationResult(false, $"Искры Света capped at 100: сейчас {currentLightSparks}, запрос {lightSparks}.");

        var featherCostLong = (long)lightSparks * TreasuryFeathersPerLightSpark;
        if (featherCostLong > int.MaxValue)
            return new TreasuryOperationResult(false, $"Стоимость обмена слишком велика: требуется {featherCostLong} Чернильных Перьев.");

        var featherCost = (int)featherCostLong;
        var spendable = GetSoulSpendableInkFeathers(soulRoot);
        if (spendable < featherCost)
            return new TreasuryOperationResult(false, $"Недостаточно Чернильных Перьев: доступно {spendable}, требуется {featherCost}.");

        SyncTreasuryExchangeCycle(shiningRoot, treasury, cycleId);
        SetSoulSpendableInkFeathers(soulRoot, spendable - featherCost);
        shiningRoot["lightSparks"] = currentLightSparks + lightSparks;
        treasury["exchangeThisCycleLightSparks"] = exchangedThisCycle + lightSparks;
        treasury["exchangeHistory"]!.AsArray().Add(new JsonObject
        {
            ["exchangeId"] = $"shining_treasury_exchange_{Guid.NewGuid():N}",
            ["cycleId"] = cycleId,
            ["inkFeathersSpent"] = featherCost,
            ["lightSparksReceived"] = lightSparks,
            ["rateFeathersPerSpark"] = TreasuryFeathersPerLightSpark,
            ["createdAtUtc"] = DateTime.UtcNow.ToString("o")
        });

        return new TreasuryOperationResult(
            true,
            $"Обмен завершён: {featherCost} Перьев -> {lightSparks} Искр Света.",
            InkFeathersDelta: -featherCost,
            LightSparksDelta: lightSparks);
    }

    private static void SyncTreasuryExchangeCycle(JsonObject shiningRoot, JsonObject treasury, string cycleId)
    {
        SyncTreasuryCurrentReturnCycleIfMissing(shiningRoot, cycleId);

        if (string.Equals(GetNodeString(treasury["exchangeCycleId"]), cycleId, StringComparison.OrdinalIgnoreCase))
            return;

        treasury["exchangeCycleId"] = cycleId;
        treasury["exchangeThisCycleLightSparks"] = 0;
    }

    private static void SyncTreasuryCurrentReturnCycleIfMissing(JsonObject shiningRoot, string cycleId)
    {
        if (shiningRoot["gachaSystem"] is JsonObject gachaSystem &&
            string.IsNullOrWhiteSpace(GetNodeString(gachaSystem["currentReturnCycleId"])))
        {
            gachaSystem["currentReturnCycleId"] = cycleId;
            gachaSystem["chargesUsedThisReturn"] = 0;
        }
    }
}
