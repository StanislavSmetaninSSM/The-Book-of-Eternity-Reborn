using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeShiningPlayerFacingSourceGuardTests
{
    private static string ReadSource(string fileName)
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "UI",
            "ExplorerMode",
            fileName);
        return File.ReadAllText(path);
    }

    private static string ReadServiceSource(string fileName)
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", fileName);
        return File.ReadAllText(path);
    }

    [Fact]
    public void ShiningOverviewScreens_MustNotCapPrimaryAfterlifeLists()
    {
        var overviewSource = ReadSource("ExplorerMode.Afterlife.ShiningAbode.Actions.cs");
        var politicsSource = ReadSource("ExplorerMode.Afterlife.ShiningAbode.cs");
        var tradeSource = ReadSource("ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs");

        Assert.DoesNotContain("halls.OfType<JsonObject>().Take(3)", overviewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("actors.OfType<JsonObject>().Take(3)", overviewSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".Take(5)", overviewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var request in foundingRequests.Take(5))", politicsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var request in realignmentRequests.Take(5))", politicsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var request in leadershipRequests.Take(5))", politicsSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".Take(6)", tradeSource, StringComparison.Ordinal);
        Assert.Contains("показаны все без сокращения", overviewSource, StringComparison.Ordinal);
        Assert.Contains("все pending-запросы", politicsSource, StringComparison.Ordinal);
        Assert.Contains("Все призывы реликвий", tradeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiningInspectionScreens_MustExposeFullJsonAuditsAndPendingPreviewContract()
    {
        var source = ReadSource("ExplorerMode.Afterlife.ShiningAbode.cs");
        var actionPreviewSource = ReadSource("ExplorerMode.Afterlife.ShiningAbode.ActionPreviews.cs");

        Assert.Contains("Полный JSON halls[]", source, StringComparison.Ordinal);
        Assert.Contains("Полный JSON shiningPoliticalActors[]", source, StringComparison.Ordinal);
        Assert.Contains("JSON coreActionReceipts[] для просмотра", source, StringComparison.Ordinal);
        Assert.Contains("JSON shining_abode_state.json после исходов", source, StringComparison.Ordinal);
        Assert.Contains("Ключевые последствия receipt", source, StringComparison.Ordinal);
        Assert.Contains("Полный контракт, который должен закрыть GM", source, StringComparison.Ordinal);
        Assert.Contains("BuildShiningCoreActionRequestPreviewLines(context, request)", source, StringComparison.Ordinal);
        Assert.Contains("Доступность политических действий", source, StringComparison.Ordinal);
        Assert.Contains("ShiningFactionRequestState.FactionFoundingCostFeathers", source, StringComparison.Ordinal);
        Assert.Contains("CountAscendedShiningResidents", source, StringComparison.Ordinal);
        Assert.Contains("Ожидаемый каркас coreActionReceipts[]", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("selectedCardIds[]", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("newResidentIds[]", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("seededProjectIds[]", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("generatedDraftVersion", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("ShiningCoreActionRequestState.ActionTypeOpenGates", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("Math.Max(1, request.SourceDraftVersion + 1)", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("quotedCostFeathers", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("quotedCostLightSparks", actionPreviewSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiningTradePreviewAndDuplicateConflict_MustExposeFullConsequences()
    {
        var tradeSource = ReadSource("ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs");
        var serviceSource = ReadServiceSource("ShiningTradeService.cs");

        Assert.Contains("Чернильные Перья: [gold1]{currentFeathers}[/] ->", tradeSource, StringComparison.Ordinal);
        Assert.Contains("expectedLocalReceipt", tradeSource, StringComparison.Ordinal);
        Assert.Contains("Ожидаемый каркас faction.tradeInventoryReceipts[]", tradeSource, StringComparison.Ordinal);
        Assert.Contains("BuildShiningTradeInventoryExpectedReceiptAuditNode", tradeSource, StringComparison.Ordinal);
        Assert.Contains("expectedStateFragment", tradeSource, StringComparison.Ordinal);
        Assert.Contains("affectedFiles", tradeSource, StringComparison.Ordinal);
        Assert.Contains("soldOutAfterPurchase", tradeSource, StringComparison.Ordinal);
        Assert.Contains("Полный JSON покупки сияющей витрины: предложение, чек и фрагмент состояния", tradeSource, StringComparison.Ordinal);
        Assert.Contains("BuildDuplicatePendingTradeRequestsMessage", serviceSource, StringComparison.Ordinal);
        Assert.Contains("derivedTradeSlotCount", serviceSource, StringComparison.Ordinal);
        Assert.Contains("derivedRarityCeiling", serviceSource, StringComparison.Ordinal);
        Assert.Contains("createdAtTurn", serviceSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiningGatesRerollPreview_MustShowFullRemovedAddedAndFinalCards()
    {
        var source = ReadSource("ExplorerMode.Afterlife.ShiningAbode.Gates.cs");

        Assert.Contains("Полные карты, уходящие из selectable-набора", source, StringComparison.Ordinal);
        Assert.Contains("Полные карты, приходящие в selectable-набор", source, StringComparison.Ordinal);
        Assert.Contains("Итоговый selectable-набор после подтверждения", source, StringComparison.Ordinal);
        Assert.Contains("ReadGateAvailableCardIds(beforeGates)", source, StringComparison.Ordinal);
        Assert.Contains("ReadGateAvailableCardIds(afterGates)", source, StringComparison.Ordinal);
        Assert.Contains("BuildShiningBlessingCardInspectionLines(card, context, isSelected: false)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiningPlayerFacingPreviewLabels_MustPreferRussianGameplayWording()
    {
        var source = string.Join(
            Environment.NewLine,
            ReadSource("ExplorerMode.Afterlife.ShiningAbode.ActionPreviews.cs"),
            ReadSource("ExplorerMode.Afterlife.ShiningAbode.Gates.cs"),
            ReadSource("ExplorerMode.Afterlife.ShiningAbode.Politics.cs"),
            ReadSource("ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs"));

        Assert.Contains("Контракт закрытия для GM", source, StringComparison.Ordinal);
        Assert.Contains("Чернильные Перья", source, StringComparison.Ordinal);
        Assert.Contains("Искры Света", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GM closure contract", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Ink Feathers:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Light Sparks:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("client-local mutation", source, StringComparison.Ordinal);
    }
}
