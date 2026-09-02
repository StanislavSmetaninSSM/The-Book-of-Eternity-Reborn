using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// RED contract for the T070-B.4 pure item-consumption seam.  The production
/// planner deliberately does not exist at this stage, so every case first
/// proves its canonical input and then fails at that missing production seam.
/// </summary>
public sealed class MortalItemConsumptionPlannerTests
{
    [Fact]
    public void Plan_PartialStackPreservesIdentityReceiptAndCarrier()
    {
        var (item, index) = CreateCanonicalStack("itm_partial", count: 3);
        AssertCanonicalItemAndIdentity(item, index, "itm_partial");
        RequireConsumptionPlanner();
    }

    [Fact]
    public void Plan_FullStackConsumesIdentityClearsEquipmentAndReturnsTerminalOwner()
    {
        var (item, index) = CreateCanonicalStack("itm_terminal", count: 1);
        item["equipmentSlot"] = "MainHand";
        MortalItemTestFixture.ResealCanonical(item);
        AssertCanonicalItemAndIdentity(item, index, "itm_terminal");
        RequireConsumptionPlanner();
    }

    [Theory]
    [InlineData("container")]
    [InlineData("quest")]
    [InlineData("bond")]
    [InlineData("other")]
    public void Plan_FullStackRejectsContainerQuestBondOrOtherCompanionWithoutAfterImages(
        string companion)
    {
        var (item, index) = CreateCanonicalStack("itm_companion_" + companion, count: 1);
        ConfigureCompanionReference(item, companion);
        MortalItemTestFixture.ResealCanonical(item);
        AssertCanonicalItemAndIdentity(item, index, "itm_companion_" + companion);
        RequireConsumptionPlanner();
    }

    [Fact]
    public void Plan_RepeatedClaimsEmitSequentialTransitionsInFinalizationOrder()
    {
        var (item, index) = CreateCanonicalStack("itm_repeated", count: 3);
        AssertCanonicalItemAndIdentity(item, index, "itm_repeated");
        RequireConsumptionPlanner();
    }

    [Fact]
    public void Plan_ResourceBearingPartialScalesMaximumAndCurrentExactly()
    {
        var (item, index) = CreateCanonicalStack("itm_resource_exact", count: 4);
        item["resourceBindings"] = new JsonArray(new JsonObject
        {
            ["resourceKey"] = "charges",
            ["maximum"] = 8,
            ["current"] = 8,
            ["quantum"] = 1,
            ["instanceKind"] = "instance_fixed"
        });
        MortalItemTestFixture.ResealCanonical(item);
        AssertCanonicalItemAndIdentity(item, index, "itm_resource_exact");
        RequireConsumptionPlanner();
    }

    [Theory]
    [InlineData("inexact_quantum")]
    [InlineData("non_instance_fixed")]
    public void Plan_InexactOrNonInstanceFixedCapacityRejectsWithoutAfterImages(string axis)
    {
        var (item, index) = CreateCanonicalStack("itm_resource_" + axis, count: 3);
        item["resourceBindings"] = new JsonArray(new JsonObject
        {
            ["resourceKey"] = "charges",
            ["maximum"] = 1,
            ["current"] = 1,
            ["quantum"] = axis == "inexact_quantum" ? 1 : 0.5m,
            ["instanceKind"] = axis == "inexact_quantum" ? "instance_fixed" : "shared"
        });
        MortalItemTestFixture.ResealCanonical(item);
        AssertCanonicalItemAndIdentity(item, index, "itm_resource_" + axis);
        RequireConsumptionPlanner();
    }

    [Fact]
    public void Plan_IsWriteFreeAndDeterministicAcrossDetachedInputs()
    {
        var (firstItem, firstIndex) = CreateCanonicalStack("itm_deterministic", count: 3);
        var secondItem = firstItem.DeepClone().AsObject();
        var secondIndex = firstIndex.DeepClone().AsObject();
        AssertCanonicalItemAndIdentity(firstItem, firstIndex, "itm_deterministic");
        AssertCanonicalItemAndIdentity(secondItem, secondIndex, "itm_deterministic");
        Assert.True(JsonNode.DeepEquals(firstItem, secondItem));
        Assert.True(JsonNode.DeepEquals(firstIndex, secondIndex));
        RequireConsumptionPlanner();
    }

    [Fact]
    public void Project_SameTurnCreateAndTransferUseSnapshotDeterministicReceiptAndTransitionIds()
    {
        var (item, index) = CreateCanonicalStack("itm_same_turn", count: 1);
        AssertCanonicalItemAndIdentity(item, index, "itm_same_turn");
        RequireCanonicalProjectionPlanner();
    }

    private static (JsonObject Item, JsonObject Index) CreateCanonicalStack(
        string itemId,
        int count)
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot(itemId);
        item["count"] = count;
        MortalItemTestFixture.ResealCanonical(item);
        return (item, MortalItemTestFixture.CreateIndex(item));
    }

    private static void AssertCanonicalItemAndIdentity(
        JsonObject item,
        JsonObject index,
        string itemId)
    {
        Assert.Equal(itemId, item["itemId"]!.GetValue<string>());
        Assert.True(item["count"]!.GetValue<int>() > 0);
        var parsed = MortalItemIdentityState.Parse(index.ToJsonString());
        Assert.Empty(parsed.Issues);
        Assert.Equal("active", parsed.EntriesByItemId[itemId]["state"]!.GetValue<string>());
    }

    private static void ConfigureCompanionReference(JsonObject item, string companion)
    {
        switch (companion)
        {
            case "container":
                item["isContainer"] = true;
                item["capacity"] = 1;
                break;
            case "quest":
                item["questLinks"] = new JsonArray("quest_t070_b4");
                break;
            case "bond":
                item["ownerBondLevelCurrent"] = 1;
                item["ownerBondLevelMax"] = 1;
                break;
            case "other":
                item["fateCards"] = new JsonArray(new JsonObject { ["cardId"] = "fate_t070_b4" });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(companion), companion, null);
        }
    }

    private static void RequireConsumptionPlanner() => RequireProductionPlanner(
        "BookOfEternityClient.Services.MortalItemConsumptionPlanner",
        "T070-B.4 requires the shared pure Mortal item-consumption planner.");

    private static void RequireCanonicalProjectionPlanner() => RequireProductionPlanner(
        "BookOfEternityClient.Services.MortalItemCanonicalProjectionPlanner",
        "T070-B.4 requires the shared pure accepted-item projection planner.");

    private static void RequireProductionPlanner(string fullName, string reason) =>
        Assert.True(typeof(MortalItemIdentityState).Assembly.GetType(fullName) is not null, reason);
}
