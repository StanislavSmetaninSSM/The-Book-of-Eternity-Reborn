using System.Text.Json.Nodes;
using System.Reflection;
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
        AssertPartial(Plan(item, index, "itm_partial", 1));
    }

    [Fact]
    public void Plan_FullStackConsumesIdentityClearsEquipmentAndReturnsTerminalOwner()
    {
        var (item, index) = CreateCanonicalStack("itm_terminal", count: 1);
        item["equipmentSlot"] = "MainHand";
        MortalItemTestFixture.ResealCanonical(item);
        AssertCanonicalItemAndIdentity(item, index, "itm_terminal");
        AssertFull(Plan(item, index, "itm_terminal", 1));
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
        AssertInvalid(Plan(item, index, "itm_companion_" + companion, 1));
    }

    [Fact]
    public void Plan_RepeatedClaimsEmitSequentialTransitionsInFinalizationOrder()
    {
        var (item, index) = CreateCanonicalStack("itm_repeated", count: 3);
        AssertCanonicalItemAndIdentity(item, index, "itm_repeated");
        AssertSequential(Plan(item, index, "itm_repeated", 1, 1));
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
        AssertExactCapacity(Plan(item, index, "itm_resource_exact", 2));
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
        AssertInvalid(Plan(item, index, "itm_resource_" + axis, 1));
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
        var first = Plan(firstItem, firstIndex, "itm_deterministic", 1);
        var second = Plan(secondItem, secondIndex, "itm_deterministic", 1);
        Assert.Equal(Read(first, "Fingerprint"), Read(second, "Fingerprint"));
        Assert.Equal(Describe(first), Describe(second));
    }

    [Fact]
    public void Project_SameTurnCreateAndTransferUseSnapshotDeterministicReceiptAndTransitionIds()
    {
        var (item, index) = CreateCanonicalStack("itm_same_turn", count: 1);
        AssertCanonicalItemAndIdentity(item, index, "itm_same_turn");
        var planner = RequireExactType("BookOfEternityClient.Services.MortalItemCanonicalProjectionPlanner");
        var input = RequireExactType("BookOfEternityClient.Services.MortalItemCanonicalProjectionInput");
        var project = Assert.Single(planner.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public), method =>
            method.Name == "Project" && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == input);
        Assert.NotNull(project.ReturnType.GetProperty("ItemPhaseAfterImages"));
        Assert.NotNull(project.ReturnType.GetProperty("IdentityIndexAfterImage"));
        Assert.NotNull(project.ReturnType.GetProperty("Fingerprint"));
        AssertCanonicalItemAndIdentity(item, index, "itm_same_turn");
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

    private static object Plan(JsonObject item, JsonObject index, string itemId, params int[] quantities)
    {
        var planner = RequireExactType("BookOfEternityClient.Services.MortalItemConsumptionPlanner");
        var inputType = RequireExactType("BookOfEternityClient.Services.MortalItemConsumptionPlanningInput");
        var commandType = RequireExactType("BookOfEternityClient.Services.MortalItemConsumptionCommand");
        var resultType = RequireExactType("BookOfEternityClient.Services.MortalItemConsumptionPlanningResult");
        RequireProperties(inputType, "Turn", "BaselineFingerprint", "CarrierRoots", "IdentityState", "Commands", "Definitions", "ResourceState", "CapacitySourceEvidence", "CapacityPolicyFingerprint");
        RequireProperties(resultType, "CarrierAfterImages", "IdentityIndexAfterImage", "IdentityTransitions", "CapacityTransitions", "TerminalOwners", "Issues", "Fingerprint");
        var plan = Assert.Single(planner.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public), method => method.Name == "Plan" && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == inputType && method.ReturnType == resultType);
        var commands = Array.CreateInstance(commandType, quantities.Length);
        for (var i = 0; i < quantities.Length; i++) commands.SetValue(Activator.CreateInstance(commandType, i + 1, itemId, quantities[i], Fingerprint("claim" + i), $"mitr_consume_{i + 1:D4}", "treatment", "authority")!, i);
        var carrierRoots = new MortalItemCarrierCatalogInput(null, MortalItemTestFixture.CreateCarrier(item, "npc_inventory", "npc_test"), null, null, null, new Dictionary<string, JsonObject>());
        var identity = MortalItemIdentityState.Parse(index.ToJsonString());
        var definitions = ResourceDefinitionCatalog.ParseCanonical(null, allowMissingPristine: true).Catalog!;
        var input = Activator.CreateInstance(inputType, 42, Fingerprint("baseline"), carrierRoots, identity, commands, definitions, new ResourceStateLedger(Array.Empty<ResourceStateEntry>()), new ResourceSourceEvidence("treatment", "attempt_t070b4", Fingerprint("authority")), Fingerprint("policy"))!;
        return plan.Invoke(null, new[] { input })!;
    }

    private static Type RequireExactType(string name)
    {
        var type = typeof(MortalItemIdentityState).Assembly.GetType(name);
        Assert.True(type is not null, $"The frozen production type '{name}' is absent.");
        Assert.True(string.Equals(name, type!.FullName, StringComparison.Ordinal));
        return type;
    }
    private static string Fingerprint(string value) => "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static void RequireProperties(Type type, params string[] names) => Assert.All(names, name => Assert.NotNull(type.GetProperty(name)));
    private static object? Read(object value, string property) => value.GetType().GetProperty(property)!.GetValue(value);
    private static string Describe(object value) => string.Join("|", value.GetType().GetProperties().OrderBy(p => p.Name).Select(p => p.Name + "=" + p.GetValue(value)));
    private static void AssertPartial(object result) { Assert.Empty((System.Collections.IEnumerable)Read(result, "Issues")!); Assert.NotNull(Read(result, "IdentityIndexAfterImage")); Assert.NotEmpty((System.Collections.IEnumerable)Read(result, "IdentityTransitions")!); }
    private static void AssertFull(object result) { AssertPartial(result); Assert.NotEmpty((System.Collections.IEnumerable)Read(result, "TerminalOwners")!); }
    private static void AssertSequential(object result) { AssertPartial(result); Assert.Equal(2, ((System.Collections.IEnumerable)Read(result, "IdentityTransitions")!).Cast<object>().Count()); }
    private static void AssertExactCapacity(object result) { AssertPartial(result); Assert.NotEmpty((System.Collections.IEnumerable)Read(result, "CapacityTransitions")!); }
    private static void AssertInvalid(object result) { Assert.NotEmpty((System.Collections.IEnumerable)Read(result, "Issues")!); Assert.Null(Read(result, "IdentityIndexAfterImage")); Assert.Empty((System.Collections.IEnumerable)Read(result, "CarrierAfterImages")!); Assert.Empty((System.Collections.IEnumerable)Read(result, "IdentityTransitions")!); }
}
