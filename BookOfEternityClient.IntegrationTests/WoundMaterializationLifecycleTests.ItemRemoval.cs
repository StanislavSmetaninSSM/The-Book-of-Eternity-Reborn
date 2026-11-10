using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundMaterializationLifecycleTests
{
    [Fact]
    public async Task ActiveWound_ReferencedTreatmentItemRawRemovalFailsClosed()
    {
        // Seed an agreeing retained carrier/index/history, not a synthetic source
        // occurrence. Birth publication is outside this deletion-time test.
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await WoundMaterializationValidationTests.SeedEmptyFoundationsAsync(context);
        await context.WriteExactJsonAsync("game_state/meta/soul_state.json",
            new JsonObject { ["currentRealm"] = "Mortal World" }.ToJsonString());
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        var inventory = new JsonObject
        {
            ["items"] = new JsonArray(item.DeepClone()),
            ["equipment"] = new JsonObject()
        };
        await context.WriteExactJsonAsync(InventoryEquipmentService.ItemsPath, inventory.ToJsonString());
        await context.WriteExactJsonAsync(MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndex(item).ToJsonString());
        var expectedRequirements = new JsonArray(new JsonObject
        {
            ["kind"] = "item_quantity", ["itemRef"] = MortalItemTestFixture.ItemId,
            ["quantity"] = 1, ["ownerRole"] = "target"
        });
        var ownerState = CreateOwnerState("mortal_world", "player", "player_current",
            WoundCarrierCatalog.PlayerPath, null, "referenced_item_removal");
        var retainedWound = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(ownerState.Wound))!.AsObject();
        var treatment = WoundContractTestData.CreateActiveWound()["treatment"]!.DeepClone().AsObject();
        var route = treatment["routes"]![0]!.AsObject();
        route["requirements"] = expectedRequirements.DeepClone();
        route["resolution"]!["modifierSource"] = new JsonObject { ["kind"] = "fixed_zero" };
        retainedWound["treatment"] = treatment;
        var parsed = WoundMaterializationContract.Parse(retainedWound.ToJsonString(), WoundCarrierCatalog.PlayerPath);
        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        var retained = Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
        ownerState = CreateOwnerState(retained, CreateCarrier(retained.Owner, null, retained), "referenced_item_removal");
        AssertCanonicalAgreement(ownerState);
        await context.WriteExactJsonAsync(WoundCarrierCatalog.PlayerPath, ownerState.Carriers.PlayerWounds!.ToJsonString());
        await context.WriteExactJsonAsync(WoundIdentityState.StatePath, ownerState.IdentityIndex.ToJsonString());
        await context.WriteExactJsonAsync(WoundHistoryState.HistoryPath, ownerState.History.ToJsonString());
        var canonicalIssues = await context.Validator.ValidateAcceptedTurnCanonicalWoundMaterializationAsync();
        Assert.DoesNotContain(canonicalIssues, issue => issue.Severity == IssueSeverity.Error);

        var woundRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(WoundCarrierCatalog.PlayerPath));
        var wound = Assert.IsType<JsonObject>(Assert.Single(woundRoot["activeWounds"]!.AsArray()));
        Assert.True(JsonNode.DeepEquals(expectedRequirements,
            wound["treatment"]!["routes"]![0]!["requirements"]));
        Assert.Equal("physical", wound["classification"]!["domain"]!.GetValue<string>());
        var protectedPaths = new[] { WoundCarrierCatalog.PlayerPath, WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath, MortalItemIdentityState.StatePath };
        var before = new Dictionary<string, byte[]>();
        foreach (var path in protectedPaths)
            before.Add(path, Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(path)));
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43,
            additionalTrackedPaths: WoundMaterializationValidationTests.SnapshotWoundPaths);
        var baseline = await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(baseline, issue => issue.Severity == IssueSeverity.Error);

        // A removal command does not authorize deleting canonical bytes before
        // the client-owned transition. Exercise the real signed-before-image guard.
        await context.WriteExactJsonAsync(MortalItemAcceptedTransferCatalog.PlayerRemovalPath,
            new JsonObject { ["removeInventoryItems"] = new JsonArray(new JsonObject
            {
                ["removedItemId"] = MortalItemTestFixture.ItemId,
                ["itemName"] = item["name"]?.DeepClone() ?? JsonValue.Create("Тестовый предмет"),
                ["currentContentsPath"] = null
            }) }.ToJsonString());
        inventory["items"] = new JsonArray();
        await context.WriteExactJsonAsync(InventoryEquipmentService.ItemsPath, inventory.ToJsonString());
        var issues = await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error &&
            issue.Code == "mortal_item_materialization_immutable_envelope_rewrite" &&
            issue.Actor == $"mortal_item:existing:{MortalItemTestFixture.ItemId}" &&
            issue.Actual == "item missing from raw current carriers");
        foreach (var path in protectedPaths)
            Assert.Equal(before[path], await context.FileSystem.ReadFileBytesAsync(path));
    }
}
