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

        Assert.Equal("shine_relic_core_gacha_preview", GetString(receipt["relicId"]));
        Assert.Equal("Example Shining Soul Relic", GetString(receipt["relicName"]));
        Assert.Equal("copy input/turn_request.json.gachaBaseResult.baseRarity", GetString(receipt["baseRarity"]));
        Assert.Contains("+2 rarity step", GetString(receipt["finalRarity"]), StringComparison.Ordinal);
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

        Assert.Equal("hall_native_core_discovery_preview", GetString(receipt["hallId"]));
        Assert.Equal("shine_faction_native_core_discovery_preview", GetString(receipt["resolvedFactionId"]));
        Assert.Equal(2, receipt["newResidentIds"]!.AsArray().Count);
        Assert.Equal(2, receipt["seededProjectIds"]!.AsArray().Count);
        Assert.Equal("Example generated native faction charter summary", GetString(receipt["charterSummary"]));
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

        var receipt = BuildReceiptScaffold(request, ShiningCoreActionRequestState.RequestStatusRefused);

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

        Assert.Equal("shine_project_completed_core_complete_project_preview", GetString(receipt["projectId"]));
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

        var receipt = BuildReceiptScaffold(request, ShiningCoreActionRequestState.RequestStatusWithdrawn);

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

    [Theory]
    [InlineData(ShiningFactionRequestState.TransitionModePeacefulSuccession, "resident", "resident_new_head", "succeeded")]
    [InlineData(ShiningFactionRequestState.TransitionModeRevolt, "resident", "resident_rebel_head", "revolted")]
    [InlineData(ShiningFactionRequestState.TransitionModeAbdication, "guardian", "guardian_new_head", "abdicated")]
    [InlineData(ShiningFactionRequestState.TransitionModeAbdication, "", "", "vacated")]
    public void AcceptedLeadershipPreview_UsesValidatorEventTypeMapping(
        string transitionMode,
        string candidateHeadActorType,
        string candidateHeadActorId,
        string expectedEventType)
    {
        var scaffold = BuildPoliticalExpectedReceiptAuditNode(
            ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
            CreateLeadershipRequest(transitionMode, candidateHeadActorType, candidateHeadActorId));

        var history = scaffold["accepted"]!["history"]!.AsObject();

        Assert.Equal(expectedEventType, GetString(history["eventType"]));
    }

    [Fact]
    public void WithdrawnLeadershipPreview_OmitsUnsupportedHistoryEventType()
    {
        var scaffold = BuildPoliticalExpectedReceiptAuditNode(
            ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
            CreateLeadershipRequest(
                ShiningFactionRequestState.TransitionModePeacefulSuccession,
                "resident",
                "resident_new_head"));

        var withdrawn = scaffold["withdrawn"]!.AsObject();
        var history = withdrawn["history"]!.AsArray();

        Assert.Empty(history);
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

    private static JsonObject CreateLeadershipRequest(
        string transitionMode,
        string candidateHeadActorType,
        string candidateHeadActorId) => new()
    {
        ["requestId"] = "leadership_preview",
        ["factionId"] = "faction_dawn",
        ["transitionMode"] = transitionMode,
        ["incumbentHeadActorType"] = "resident",
        ["incumbentHeadActorId"] = "resident_old_head",
        ["candidateHeadActorType"] = candidateHeadActorType,
        ["candidateHeadActorId"] = candidateHeadActorId,
        ["supportingResidentIds"] = new JsonArray("resident_support_a", "resident_support_b", "resident_support_c"),
        ["createdAtTurn"] = 20,
        ["createdAtUtc"] = "2026-05-02T00:00:00Z"
    };

    private static string GetString(JsonNode? node) => node?.GetValue<string>() ?? string.Empty;
}
