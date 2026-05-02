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
        Assert.Contains("currentReturnCycleId=", overviewSource, StringComparison.Ordinal);
        Assert.Contains("hallId=", overviewSource, StringComparison.Ordinal);
        Assert.Contains("actorId=", overviewSource, StringComparison.Ordinal);
        Assert.Contains("currentFactionId=", overviewSource, StringComparison.Ordinal);
        Assert.Contains("factionId=", overviewSource, StringComparison.Ordinal);
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
        Assert.Contains("BuildShiningCoreExpectedReceiptAuditNode(context, request)", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("BuildShiningCoreProjectedStateFragment", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("projectedStateFragment", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("beforeFullShiningRoot", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("afterFullShiningRoot", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("beforeFullSoulRoot", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("afterFullSoulRoot", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("BuildShiningGachaAccountingAuditNode", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("expectedGachaHistoryEntry", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("oneNewRelicEvidence", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("GetNodeInt(context.Root[\"gates\"]?[\"draftVersion\"]) + 1", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("[\"refusedOrWithdrawn\"]", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("generatedDraftVersion: 0", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("quotedCostFeathers", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("quotedCostLightSparks", actionPreviewSource, StringComparison.Ordinal);
        Assert.Contains("safeEffectDetails", source, StringComparison.Ordinal);
        Assert.Contains("BuildShiningBlessingEffectDetailLines", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiningPoliticsOverview_MustMirrorImplementedEligibilityAndPendingLocks()
    {
        var source = ReadSource("ExplorerMode.Afterlife.ShiningAbode.cs");

        Assert.Contains("factionRealignmentState=ready_to_realign", source, StringComparison.Ordinal);
        Assert.Contains("wavering tier сам по себе не открывает переход", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ready_to_realign/wavering", source, StringComparison.Ordinal);
        Assert.Contains("Pending-модель не является глобальным mutex", source, StringComparison.Ordinal);
        Assert.Contains("Pending-модель не глобальная", source, StringComparison.Ordinal);
        Assert.Contains("foreign pending realignment для того же residentId", source, StringComparison.Ordinal);
        Assert.Contains("foreign pending leadership для той же factionId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("новый запрос заблокирован, пока живёт political pending request", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiningTradePreviewAndDuplicateConflict_MustExposeFullConsequences()
    {
        var tradeSource = ReadSource("ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs");
        var serviceSource = ReadServiceSource("ShiningTradeService.cs");

        Assert.Contains("Чернильные Перья: [gold1]{currentFeathers}[/] ->", tradeSource, StringComparison.Ordinal);
        Assert.Contains("expectedLocalReceipt", tradeSource, StringComparison.Ordinal);
        Assert.Contains("Ожидаемый каркас faction.tradeInventoryReceipts[]", tradeSource, StringComparison.Ordinal);
        Assert.Contains("Ожидаемый каркас faction.tradeInventory", tradeSource, StringComparison.Ordinal);
        Assert.Contains("BuildShiningTradeInventoryExpectedReceiptAuditNode", tradeSource, StringComparison.Ordinal);
        Assert.Contains("BuildShiningTradeInventoryExpectedStateAuditNode", tradeSource, StringComparison.Ordinal);
        Assert.Contains("BuildShiningTradeInventoryExpectedItemsAuditArray", tradeSource, StringComparison.Ordinal);
        Assert.Contains("priceInFeathers", tradeSource, StringComparison.Ordinal);
        Assert.Contains("relicData", tradeSource, StringComparison.Ordinal);
        Assert.Contains("slotCount", tradeSource, StringComparison.Ordinal);
        Assert.Contains("generationTradeTier", tradeSource, StringComparison.Ordinal);
        Assert.Contains("serviceMultiplierSnapshot", tradeSource, StringComparison.Ordinal);
        Assert.Contains("expectedStateFragment", tradeSource, StringComparison.Ordinal);
        Assert.Contains("affectedFiles", tradeSource, StringComparison.Ordinal);
        Assert.Contains("soldOutAfterPurchase", tradeSource, StringComparison.Ordinal);
        Assert.Contains("slotId {offer.SlotId}", tradeSource, StringComparison.Ordinal);
        Assert.Contains("relicId={Markup.Escape(item.RelicId)}", tradeSource, StringComparison.Ordinal);
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
    public void ShiningCommandChoiceLabels_MustExposeStableIds()
    {
        var gatesSource = ReadSource("ExplorerMode.Afterlife.ShiningAbode.Gates.cs");
        var politicsSource = ReadSource("ExplorerMode.Afterlife.ShiningAbode.Politics.cs");

        Assert.Contains("projectId={projectId}", gatesSource, StringComparison.Ordinal);
        Assert.Contains("residentId={residentId}", politicsSource, StringComparison.Ordinal);
        Assert.Contains("factionId={factionId}", politicsSource, StringComparison.Ordinal);
        Assert.Contains("BuildShiningRadiantActorPoliticalChoiceLabel", politicsSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiningBlessingActivationAudit_MustExposeStableIdsWithoutRawPayloads()
    {
        var serviceSource = ReadServiceSource("ShiningBlessingEffectState.cs");

        Assert.Contains("Blessing audit: effectId=", serviceSource, StringComparison.Ordinal);
        Assert.Contains("sourceCardIds=", serviceSource, StringComparison.Ordinal);
        Assert.Contains("consumptionSurface=", serviceSource, StringComparison.Ordinal);
        Assert.Contains("consumptionTarget=", serviceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("effectPayload.ToJsonString", serviceSource, StringComparison.Ordinal);
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
