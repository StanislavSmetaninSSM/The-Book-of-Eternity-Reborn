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

    [Fact]
    public void AcceptedCompleteProjectReceiptScaffold_IncludesGeneratedProjectId()
    {
        var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
        {
            RequestId = "core_complete_project_preview",
            ActionType = ShiningCoreActionRequestState.ActionTypeCompleteProject,
            FactionId = "faction_dawn",
            ProjectDraft = new JsonObject
            {
                ["displayName"] = "Завершённая песнь",
                ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeAccord
            }
        };

        var receipt = BuildReceiptScaffold(request, ShiningCoreActionRequestState.RequestStatusAccepted);

        Assert.Equal("generated_completed_project_id", GetString(receipt["projectId"]));
    }

    [Fact]
    public void RefusedCompleteProjectReceiptScaffold_LeavesProjectIdEmpty()
    {
        var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
        {
            RequestId = "core_complete_project_refused_preview",
            ActionType = ShiningCoreActionRequestState.ActionTypeCompleteProject,
            FactionId = "faction_dawn",
            ProjectDraft = new JsonObject
            {
                ["displayName"] = "Незавершённая песнь",
                ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeAccord
            }
        };

        var receipt = BuildReceiptScaffold(request, "refused|withdrawn");

        Assert.Equal(string.Empty, GetString(receipt["projectId"]));
    }

    [Fact]
    public void AcceptedFactionFoundingPreview_UsesCanonicalHallDescriptionAndNestedCharter()
    {
        var request = new JsonObject
        {
            ["requestId"] = "founding_preview_custom",
            ["proposedFactionId"] = "faction_custom",
            ["proposedHallId"] = "hall_custom",
            ["proposedHallName"] = "Зал Глубокого Согласия",
            ["proposedHallDescription"] = "Каноническое описание зала из pending request.",
            ["proposedHallServiceTags"] = new JsonArray("social", "memory"),
            ["supportingResidentIds"] = new JsonArray("resident_a", "resident_b", "resident_c"),
            ["quotedCostFeathers"] = ShiningFactionRequestState.FactionFoundingCostFeathers,
            ["quotedCostLightSparks"] = ShiningFactionRequestState.FactionFoundingCostLightSparks,
            ["proposedFactionName"] = "WRONG root faction name",
            ["factionSummary"] = "WRONG root summary",
            ["favoredProjectArchetype"] = "WRONG_root_archetype",
            ["patronEffectFamily"] = "WRONG_root_family",
            ["charter"] = new JsonObject
            {
                ["factionName"] = "Орден Глубокого Согласия",
                ["summary"] = "Nested charter summary must be copied exactly.",
                ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeRemembrance,
                ["patronEffectFamily"] = ShiningAbodeState.EffectFamilyLore
            }
        };

        var scaffold = BuildPoliticalExpectedReceiptAuditNode(
            ShiningFactionRequestState.PendingFoundingsRequestPath,
            request);
        var accepted = scaffold["accepted"]!.AsObject();
        var delta = accepted["acceptedStateDelta"]!.AsObject();
        var hall = delta["halls.add"]!.AsObject();
        var charter = delta["factions.add"]!["charter"]!.AsObject();

        Assert.Equal("Каноническое описание зала из pending request.", GetString(hall["description"]));
        Assert.False(hall.ContainsKey("hallDescription"));
        Assert.Equal("Орден Глубокого Согласия", GetString(charter["factionName"]));
        Assert.Equal("Nested charter summary must be copied exactly.", GetString(charter["summary"]));
        Assert.Equal(ShiningAbodeState.ProjectArchetypeRemembrance, GetString(charter["favoredArchetype"]));
        Assert.Equal(ShiningAbodeState.EffectFamilyLore, GetString(charter["patronEffectFamily"]));
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

    private static JsonObject BuildPoliticalExpectedReceiptAuditNode(string pendingPath, JsonObject request)
    {
        var method = typeof(ExplorerMode).GetMethod(
            "BuildShiningPoliticalExpectedReceiptAuditNode",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types:
            [
                typeof(string),
                typeof(JsonObject)
            ],
            modifiers: null) ?? throw new MissingMethodException(nameof(ExplorerMode), "BuildShiningPoliticalExpectedReceiptAuditNode");

        return (JsonObject)method.Invoke(null, [pendingPath, request])!;
    }

    private static string GetString(JsonNode? node) => node?.GetValue<string>() ?? string.Empty;
}
