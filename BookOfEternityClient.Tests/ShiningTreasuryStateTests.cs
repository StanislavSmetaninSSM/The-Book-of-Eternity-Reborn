using System.Linq;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningTreasuryStateTests
{
    [Fact]
    public void TreasuryDepositWithdrawAndExchangePreserveProgressionCaps()
    {
        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot["lightSparks"] = 10;
        shiningRoot["gachaSystem"]!["currentReturnCycleId"] = "shining_return_7";
        var soulRoot = CreateSoulRoot(currentFeathers: 200, totalFeathers: 240);

        var deposit = ShiningAbodeState.DepositTreasuryInkFeathers(shiningRoot, soulRoot, 100);
        Assert.True(deposit.Success, deposit.Message);
        Assert.Equal(100, ShiningAbodeState.GetSoulSpendableInkFeathers(soulRoot));
        Assert.Equal(100, (int)shiningRoot[ShiningAbodeState.TreasuryProperty]!["depositedInkFeathers"]!);
        Assert.Equal(240, (int)soulRoot["inkFeathers"]!["total"]!);

        var withdraw = ShiningAbodeState.WithdrawTreasuryInkFeathers(shiningRoot, soulRoot, 40);
        Assert.True(withdraw.Success, withdraw.Message);
        Assert.Equal(140, ShiningAbodeState.GetSoulSpendableInkFeathers(soulRoot));
        Assert.Equal(60, (int)shiningRoot[ShiningAbodeState.TreasuryProperty]!["depositedInkFeathers"]!);

        var exchange = ShiningAbodeState.ExchangeTreasuryInkFeathersForLightSparks(shiningRoot, soulRoot, 2);
        Assert.True(exchange.Success, exchange.Message);
        Assert.Equal(90, ShiningAbodeState.GetSoulSpendableInkFeathers(soulRoot));
        Assert.Equal(12, (int)shiningRoot["lightSparks"]!);
        Assert.Equal(2, (int)shiningRoot[ShiningAbodeState.TreasuryProperty]!["exchangeThisCycleLightSparks"]!);

        var overCap = ShiningAbodeState.ExchangeTreasuryInkFeathersForLightSparks(shiningRoot, soulRoot, 2);
        Assert.False(overCap.Success);
        Assert.Equal(90, ShiningAbodeState.GetSoulSpendableInkFeathers(soulRoot));
        Assert.Equal(12, (int)shiningRoot["lightSparks"]!);

        var history = Assert.IsType<JsonArray>(shiningRoot[ShiningAbodeState.TreasuryProperty]!["exchangeHistory"]);
        var entry = Assert.IsType<JsonObject>(Assert.Single(history));
        Assert.Equal(50, (int)entry["inkFeathersSpent"]!);
        Assert.Equal(2, (int)entry["lightSparksReceived"]!);
        Assert.Equal(ShiningAbodeState.TreasuryFeathersPerLightSpark, (int)entry["rateFeathersPerSpark"]!);
    }

    [Fact]
    public void TreasuryDepositRejectsOverflowWithoutMutatingState()
    {
        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        var soulRoot = CreateSoulRoot(currentFeathers: 10, totalFeathers: 10);
        var treasury = ShiningAbodeState.EnsureTreasuryObject(shiningRoot);
        treasury["depositedInkFeathers"] = int.MaxValue - 1;

        var deposit = ShiningAbodeState.DepositTreasuryInkFeathers(shiningRoot, soulRoot, 2);

        Assert.False(deposit.Success);
        Assert.Equal(10, ShiningAbodeState.GetSoulSpendableInkFeathers(soulRoot));
        Assert.Equal(10, (int)soulRoot["inkFeathers"]!["total"]!);
        Assert.Equal(int.MaxValue - 1, (int)treasury["depositedInkFeathers"]!);
    }

    [Fact]
    public void TreasuryWithdrawRejectsOverflowWithoutMutatingState()
    {
        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        var soulRoot = CreateSoulRoot(currentFeathers: int.MaxValue - 1, totalFeathers: int.MaxValue - 1);
        var treasury = ShiningAbodeState.EnsureTreasuryObject(shiningRoot);
        treasury["depositedInkFeathers"] = 10;
        var beforeShiningJson = shiningRoot.ToJsonString();
        var beforeSoulJson = soulRoot.ToJsonString();

        var withdraw = ShiningAbodeState.WithdrawTreasuryInkFeathers(shiningRoot, soulRoot, 2);

        Assert.False(withdraw.Success);
        Assert.Equal(beforeShiningJson, shiningRoot.ToJsonString());
        Assert.Equal(beforeSoulJson, soulRoot.ToJsonString());
        Assert.Equal(int.MaxValue - 1, ShiningAbodeState.GetSoulSpendableInkFeathers(soulRoot));
        Assert.Equal(10, (int)treasury["depositedInkFeathers"]!);
    }

    [Fact]
    public void TreasuryMalformedStateIsPreservedForRepair()
    {
        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot[ShiningAbodeState.TreasuryProperty] = "malformed_treasury";

        ShiningAbodeState.NormalizeStateRoot(shiningRoot, residentRoot: null, guardiansRoot: null);

        Assert.Equal("malformed_treasury", (string)shiningRoot[ShiningAbodeState.TreasuryProperty]!);
        var issue = ShiningAbodeState.ValidateTreasuryShape(shiningRoot);
        Assert.NotNull(issue);
        Assert.Contains("treasury", issue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JSON object", issue, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<InvalidOperationException>(() => ShiningAbodeState.EnsureTreasuryObject(shiningRoot));
    }

    [Fact]
    public void TreasuryExchangeThisCycleIgnoresStaleCycleCounters()
    {
        var treasury = ShiningAbodeState.BuildDefaultTreasuryObject();
        treasury["exchangeCycleId"] = "shining_return_8";
        treasury["exchangeThisCycleLightSparks"] = ShiningAbodeState.TreasuryMaxLightSparksExchangePerCycle;

        var staleCycleCount = ShiningAbodeState.GetTreasuryExchangeThisCycle(treasury, "shining_return_9");
        var currentCycleCount = ShiningAbodeState.GetTreasuryExchangeThisCycle(treasury, "shining_return_8");

        Assert.Equal(0, staleCycleCount);
        Assert.Equal(ShiningAbodeState.TreasuryMaxLightSparksExchangePerCycle, currentCycleCount);
    }

    [Fact]
    public void TreasuryInterestIsCappedAndOncePerReturnCycle()
    {
        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot["gachaSystem"]!["currentReturnCycleId"] = "shining_return_3";
        var soulRoot = CreateSoulRoot(currentFeathers: 0, totalFeathers: 0, currentIncarnation: 3);

        var treasury = ShiningAbodeState.EnsureTreasuryObject(shiningRoot);
        treasury["depositedInkFeathers"] = 1000;

        var firstClaim = ShiningAbodeState.ClaimTreasuryInterest(shiningRoot, soulRoot);
        Assert.True(firstClaim.Success, firstClaim.Message);
        Assert.Equal(ShiningAbodeState.TreasuryInterestClaimCap, ShiningAbodeState.GetSoulSpendableInkFeathers(soulRoot));
        Assert.Equal(ShiningAbodeState.TreasuryInterestClaimCap, (int)soulRoot["inkFeathers"]!["total"]!);
        Assert.Equal("shining_return_3", (string)treasury["lastInterestSettlementCycleId"]!);

        var secondClaim = ShiningAbodeState.ClaimTreasuryInterest(shiningRoot, soulRoot);
        Assert.True(secondClaim.Success, secondClaim.Message);
        Assert.Equal(ShiningAbodeState.TreasuryInterestClaimCap, ShiningAbodeState.GetSoulSpendableInkFeathers(soulRoot));

        shiningRoot["gachaSystem"]!["currentReturnCycleId"] = "shining_return_4";
        soulRoot["currentIncarnation"] = 4;
        var nextCycleClaim = ShiningAbodeState.ClaimTreasuryInterest(shiningRoot, soulRoot);
        Assert.True(nextCycleClaim.Success, nextCycleClaim.Message);
        Assert.Equal(ShiningAbodeState.TreasuryInterestClaimCap * 2, ShiningAbodeState.GetSoulSpendableInkFeathers(soulRoot));
        Assert.Equal(ShiningAbodeState.TreasuryInterestClaimCap * 2, (int)soulRoot["inkFeathers"]!["total"]!);
    }

    [Fact]
    public void TreasuryInterestComputationCapsLargeDepositsWithoutOverflow()
    {
        var generated = ShiningAbodeState.ComputeTreasuryInterestForCycle(14_400_000);

        Assert.Equal(ShiningAbodeState.TreasuryInterestClaimCap, generated);
    }

    [Fact]
    public void TreasuryInterestClaimCapsLargeDepositsWithoutOverflow()
    {
        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot["gachaSystem"]!["currentReturnCycleId"] = "shining_return_11";
        var soulRoot = CreateSoulRoot(currentFeathers: 0, totalFeathers: 0, currentIncarnation: 11);
        var treasury = ShiningAbodeState.EnsureTreasuryObject(shiningRoot);
        treasury["depositedInkFeathers"] = 14_400_000;

        var claim = ShiningAbodeState.ClaimTreasuryInterest(shiningRoot, soulRoot);

        Assert.True(claim.Success, claim.Message);
        Assert.Equal(ShiningAbodeState.TreasuryInterestClaimCap, claim.InterestGenerated);
        Assert.Equal(ShiningAbodeState.TreasuryInterestClaimCap, ShiningAbodeState.GetSoulSpendableInkFeathers(soulRoot));
        Assert.Equal(ShiningAbodeState.TreasuryInterestClaimCap, (int)soulRoot["inkFeathers"]!["total"]!);
        Assert.Equal(0, (int)treasury["claimableInkFeatherInterest"]!);
        Assert.Equal("shining_return_11", (string)treasury["lastInterestSettlementCycleId"]!);
    }

    [Fact]
    public void TreasuryInterestClaimRejectsSpendableOverflowWithoutMutatingState()
    {
        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot["gachaSystem"]!["currentReturnCycleId"] = "shining_return_14";
        var soulRoot = CreateSoulRoot(currentFeathers: int.MaxValue - 1, totalFeathers: int.MaxValue - 1, currentIncarnation: 14);
        var treasury = ShiningAbodeState.EnsureTreasuryObject(shiningRoot);
        treasury["claimableInkFeatherInterest"] = 2;
        treasury["totalInterestClaimed"] = 3;
        treasury["lastInterestSettlementCycleId"] = "shining_return_14";
        var beforeShiningJson = shiningRoot.ToJsonString();
        var beforeSoulJson = soulRoot.ToJsonString();

        var claim = ShiningAbodeState.ClaimTreasuryInterest(shiningRoot, soulRoot);

        Assert.False(claim.Success);
        Assert.Equal(beforeShiningJson, shiningRoot.ToJsonString());
        Assert.Equal(beforeSoulJson, soulRoot.ToJsonString());
    }

    [Fact]
    public void TreasuryInterestClaimRejectsTotalInterestClaimedOverflowWithoutMutatingState()
    {
        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot["gachaSystem"]!["currentReturnCycleId"] = "shining_return_15";
        var soulRoot = CreateSoulRoot(currentFeathers: 10, totalFeathers: 10, currentIncarnation: 15);
        var treasury = ShiningAbodeState.EnsureTreasuryObject(shiningRoot);
        treasury["claimableInkFeatherInterest"] = 2;
        treasury["totalInterestClaimed"] = int.MaxValue - 1;
        treasury["lastInterestSettlementCycleId"] = "shining_return_15";
        var beforeShiningJson = shiningRoot.ToJsonString();
        var beforeSoulJson = soulRoot.ToJsonString();

        var claim = ShiningAbodeState.ClaimTreasuryInterest(shiningRoot, soulRoot);

        Assert.False(claim.Success);
        Assert.Equal(beforeShiningJson, shiningRoot.ToJsonString());
        Assert.Equal(beforeSoulJson, soulRoot.ToJsonString());
    }

    [Fact]
    public void TreasuryInterestSettlementRejectsClaimableOverflowWithoutMarkingCycle()
    {
        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot["gachaSystem"]!["currentReturnCycleId"] = "shining_return_16";
        var soulRoot = CreateSoulRoot(currentFeathers: 0, totalFeathers: 0, currentIncarnation: 16);
        var treasury = ShiningAbodeState.EnsureTreasuryObject(shiningRoot);
        treasury["depositedInkFeathers"] = 1000;
        treasury["claimableInkFeatherInterest"] = int.MaxValue;
        treasury["lastInterestSettlementCycleId"] = string.Empty;
        var beforeShiningJson = shiningRoot.ToJsonString();
        var beforeSoulJson = soulRoot.ToJsonString();

        var claim = ShiningAbodeState.ClaimTreasuryInterest(shiningRoot, soulRoot);

        Assert.False(claim.Success);
        Assert.Equal(beforeShiningJson, shiningRoot.ToJsonString());
        Assert.Equal(beforeSoulJson, soulRoot.ToJsonString());
    }

    [Fact]
    public void TreasuryInterestClaimUsesPreClaimTotalWhenTotalIsMissing()
    {
        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot["gachaSystem"]!["currentReturnCycleId"] = "shining_return_12";
        var soulRoot = CreateSoulRootWithoutTotal(currentFeathers: 0, currentIncarnation: 12);
        var treasury = ShiningAbodeState.EnsureTreasuryObject(shiningRoot);
        treasury["depositedInkFeathers"] = 1000;

        var claim = ShiningAbodeState.ClaimTreasuryInterest(shiningRoot, soulRoot);

        Assert.True(claim.Success, claim.Message);
        var inkFeathers = Assert.IsType<JsonObject>(soulRoot["inkFeathers"]);
        Assert.Equal(ShiningAbodeState.TreasuryInterestClaimCap, (int)inkFeathers["current"]!);
        Assert.Equal(ShiningAbodeState.TreasuryInterestClaimCap, (int)inkFeathers["total"]!);
    }

    [Fact]
    public void TreasuryInterestClaimUsesLegacyScalarAsPreClaimTotal()
    {
        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot["gachaSystem"]!["currentReturnCycleId"] = "shining_return_13";
        var soulRoot = CreateSoulRoot(currentFeathers: 0, totalFeathers: 0, currentIncarnation: 13);
        soulRoot["inkFeathers"] = 0;
        var treasury = ShiningAbodeState.EnsureTreasuryObject(shiningRoot);
        treasury["depositedInkFeathers"] = 1000;

        var claim = ShiningAbodeState.ClaimTreasuryInterest(shiningRoot, soulRoot);

        Assert.True(claim.Success, claim.Message);
        var inkFeathers = Assert.IsType<JsonObject>(soulRoot["inkFeathers"]);
        Assert.Equal(ShiningAbodeState.TreasuryInterestClaimCap, (int)inkFeathers["current"]!);
        Assert.Equal(ShiningAbodeState.TreasuryInterestClaimCap, (int)inkFeathers["total"]!);
    }

    [Fact]
    public void TreasuryOperationsUseRealReturnCycleWhenGachaCycleIsUnsynced()
    {
        var expectedCycleId = ShiningAbodeState.GetTradeCycleId(9);
        var interestRoot = ShiningAbodeState.CreateDefaultState();
        var interestSoulRoot = CreateSoulRoot(currentFeathers: 0, totalFeathers: 0, currentIncarnation: 9);
        var treasury = ShiningAbodeState.EnsureTreasuryObject(interestRoot);
        treasury["depositedInkFeathers"] = 1000;

        var firstClaim = ShiningAbodeState.ClaimTreasuryInterest(interestRoot, interestSoulRoot);
        Assert.True(firstClaim.Success, firstClaim.Message);
        Assert.Equal(expectedCycleId, (string)interestRoot["gachaSystem"]!["currentReturnCycleId"]!);
        Assert.Equal(expectedCycleId, (string)treasury["lastInterestSettlementCycleId"]!);
        Assert.DoesNotContain("unsynced", (string)treasury["lastInterestSettlementCycleId"]!, StringComparison.OrdinalIgnoreCase);

        var syncChanged = ShiningAbodeState.SyncShiningReturnCycle(interestRoot, 9, out var cycleChanged);
        Assert.False(syncChanged);
        Assert.False(cycleChanged);
        var secondClaim = ShiningAbodeState.ClaimTreasuryInterest(interestRoot, interestSoulRoot);
        Assert.True(secondClaim.Success, secondClaim.Message);
        Assert.Equal(ShiningAbodeState.TreasuryInterestClaimCap, ShiningAbodeState.GetSoulSpendableInkFeathers(interestSoulRoot));

        var exchangeRoot = ShiningAbodeState.CreateDefaultState();
        exchangeRoot["lightSparks"] = 10;
        var exchangeSoulRoot = CreateSoulRoot(currentFeathers: 200, totalFeathers: 200, currentIncarnation: 9);
        var exchange = ShiningAbodeState.ExchangeTreasuryInkFeathersForLightSparks(exchangeRoot, exchangeSoulRoot, ShiningAbodeState.TreasuryMaxLightSparksExchangePerCycle);
        Assert.True(exchange.Success, exchange.Message);
        var exchangeTreasury = ShiningAbodeState.EnsureTreasuryObject(exchangeRoot);
        Assert.Equal(expectedCycleId, (string)exchangeRoot["gachaSystem"]!["currentReturnCycleId"]!);
        Assert.Equal(expectedCycleId, (string)exchangeTreasury["exchangeCycleId"]!);
        Assert.DoesNotContain("unsynced", (string)exchangeTreasury["exchangeCycleId"]!, StringComparison.OrdinalIgnoreCase);

        ShiningAbodeState.SyncShiningReturnCycle(exchangeRoot, 9, out _);
        var overCap = ShiningAbodeState.ExchangeTreasuryInkFeathersForLightSparks(exchangeRoot, exchangeSoulRoot, 1);
        Assert.False(overCap.Success);
        Assert.Equal(200 - ShiningAbodeState.TreasuryFeathersPerLightSpark * ShiningAbodeState.TreasuryMaxLightSparksExchangePerCycle, ShiningAbodeState.GetSoulSpendableInkFeathers(exchangeSoulRoot));
    }

    [Fact]
    public void TreasuryExchangeRejectsExtremeInputWithoutMutatingState()
    {
        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot["lightSparks"] = 1;
        shiningRoot["gachaSystem"]!["currentReturnCycleId"] = "shining_return_7";
        var soulRoot = CreateSoulRoot(currentFeathers: 1000, totalFeathers: 1000);
        var treasury = ShiningAbodeState.EnsureTreasuryObject(shiningRoot);
        treasury["exchangeCycleId"] = "shining_return_7";
        treasury["exchangeThisCycleLightSparks"] = 1;

        var result = ShiningAbodeState.ExchangeTreasuryInkFeathersForLightSparks(shiningRoot, soulRoot, int.MaxValue);

        Assert.False(result.Success);
        Assert.Equal(1, (int)shiningRoot["lightSparks"]!);
        Assert.Equal(1, (int)treasury["exchangeThisCycleLightSparks"]!);
        Assert.Equal(1000, ShiningAbodeState.GetSoulSpendableInkFeathers(soulRoot));
        Assert.Empty(Assert.IsType<JsonArray>(treasury["exchangeHistory"]));
    }

    [Fact]
    public void TreasuryExchangeRejectsInsufficientFeathersWithoutSyncingUnsyncedCycle()
    {
        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot["lightSparks"] = 10;
        var gachaSystem = Assert.IsType<JsonObject>(shiningRoot["gachaSystem"]);
        gachaSystem["currentReturnCycleId"] = string.Empty;
        gachaSystem["chargesUsedThisReturn"] = 2;
        var soulRoot = CreateSoulRoot(currentFeathers: 1, totalFeathers: 1, currentIncarnation: 9);
        var treasury = ShiningAbodeState.EnsureTreasuryObject(shiningRoot);

        var result = ShiningAbodeState.ExchangeTreasuryInkFeathersForLightSparks(shiningRoot, soulRoot, 1);

        Assert.False(result.Success);
        Assert.Equal(string.Empty, (string)gachaSystem["currentReturnCycleId"]!);
        Assert.Equal(2, (int)gachaSystem["chargesUsedThisReturn"]!);
        Assert.Equal(string.Empty, (string)treasury["exchangeCycleId"]!);
        Assert.Equal(0, (int)treasury["exchangeThisCycleLightSparks"]!);
        Assert.Equal(10, (int)shiningRoot["lightSparks"]!);
        Assert.Equal(1, ShiningAbodeState.GetSoulSpendableInkFeathers(soulRoot));
        Assert.Empty(Assert.IsType<JsonArray>(treasury["exchangeHistory"]));
    }

    [Fact]
    public void TreasuryDoesNotExposeLightSparkDepositApi()
    {
        var methodNames = typeof(ShiningAbodeState)
            .GetMethods()
            .Select(method => method.Name)
            .ToArray();

        Assert.DoesNotContain(methodNames, name => name.Contains("DepositTreasuryLightSparks", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, name => name.Contains("WithdrawTreasuryLightSparks", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, name => name.Contains("ExchangeTreasuryLightSparksForInkFeathers", StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject CreateSoulRoot(int currentFeathers, int totalFeathers, int currentIncarnation = 7) =>
        new()
        {
            ["currentRealm"] = "Shining Abode",
            ["currentIncarnation"] = currentIncarnation,
            ["inkFeathers"] = new JsonObject
            {
                ["current"] = currentFeathers,
                ["total"] = totalFeathers
            }
        };

    private static JsonObject CreateSoulRootWithoutTotal(int currentFeathers, int currentIncarnation = 7) =>
        new()
        {
            ["currentRealm"] = "Shining Abode",
            ["currentIncarnation"] = currentIncarnation,
            ["inkFeathers"] = new JsonObject
            {
                ["current"] = currentFeathers
            }
        };
}
