using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningCoreActionPreviewScaffoldTests
{
    [Fact]
    public void AcceptedGachaReceiptScaffold_IncludesGeneratedOutcomeFields()
    {
        var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
        {
            RequestId = "core_gacha_preview",
            ActionType = ShiningCoreActionRequestState.ActionTypePullRelicGacha,
            FactionId = "faction_dawn",
            ReturnCycleId = "shining_return_7",
            ProjectedGachaBonusSteps = 2,
            QuotedCostFeathers = 20,
            QuotedCostLightSparks = 0
        };

        var receipt = BuildReceiptScaffold(request, ShiningCoreActionRequestState.RequestStatusAccepted);

        Assert.Equal("generated_shining_relic_id", GetString(receipt["relicId"]));
        Assert.Equal("generated Shining Soul Relic name", GetString(receipt["relicName"]));
        Assert.Contains("gachaBaseResult.baseRarity", GetString(receipt["baseRarity"]), StringComparison.Ordinal);
        Assert.Contains("<= 2", GetString(receipt["finalRarity"]), StringComparison.Ordinal);
        Assert.Equal("shining_return_7", GetString(receipt["returnCycleId"]));
    }

    [Fact]
    public void AcceptedNativeDiscoveryReceiptScaffold_IncludesGeneratedStateIds()
    {
        var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
        {
            RequestId = "core_discovery_preview",
            ActionType = ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction,
            QuotedCostFeathers = 15,
            QuotedCostLightSparks = 5
        };

        var receipt = BuildReceiptScaffold(request, ShiningCoreActionRequestState.RequestStatusAccepted);

        Assert.Equal("generated_native_hall_id", GetString(receipt["hallId"]));
        Assert.Equal("generated_native_faction_id", GetString(receipt["resolvedFactionId"]));
        Assert.Equal(2, receipt["newResidentIds"]!.AsArray().Count);
        Assert.Equal(2, receipt["seededProjectIds"]!.AsArray().Count);
        Assert.Equal("generated native faction charter summary", GetString(receipt["charterSummary"]));
    }

    [Fact]
    public void RefusedGeneratedOutcomeReceiptScaffold_LeavesGeneratedFieldsEmpty()
    {
        var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
        {
            RequestId = "core_gacha_refused_preview",
            ActionType = ShiningCoreActionRequestState.ActionTypePullRelicGacha,
            FactionId = "faction_dawn",
            ReturnCycleId = "shining_return_7"
        };

        var receipt = BuildReceiptScaffold(request, "refused|withdrawn");

        Assert.Equal(string.Empty, GetString(receipt["relicId"]));
        Assert.Null(receipt["baseRarity"]);
        Assert.Null(receipt["finalRarity"]);
    }

    [Fact]
    public void AcceptedForgeReshapeReceiptScaffold_EchoesExactTargetFormTag()
    {
        var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
        {
            RequestId = "core_forge_reshape_preview",
            ActionType = ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
            RelicId = "relic_routeglass",
            RelicName = "Стекло Пути",
            TargetFormTag = "solar_crown"
        };

        var receipt = BuildReceiptScaffold(request, ShiningCoreActionRequestState.RequestStatusAccepted);

        Assert.Equal("solar_crown", GetString(receipt["targetFormTag"]));
    }

    private static JsonObject BuildReceiptScaffold(
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        string status)
    {
        var method = typeof(ExplorerMode).GetMethod(
            "BuildShiningCoreExpectedReceiptAuditNode",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types:
            [
                typeof(ShiningCoreActionRequestState.PendingShiningCoreActionRequest),
                typeof(string),
                typeof(int)
            ],
            modifiers: null) ?? throw new MissingMethodException(nameof(ExplorerMode), "BuildShiningCoreExpectedReceiptAuditNode");

        return (JsonObject)method.Invoke(null, [request, status, 0])!;
    }

    private static string GetString(JsonNode? node) => node?.GetValue<string>() ?? string.Empty;
}
