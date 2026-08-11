using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public async Task Normalize_PlayerCreation_SealsOneCanonicalItemAndIndexEntry()
    {
        await _fs.WriteFileAtomicAsync(
            "input/turn_request.json",
            new JsonObject
            {
                ["sessionId"] = "session_mortal_item_normalization",
                ["requestId"] = "request_mortal_item_normalization",
                ["turnNumber"] = 42,
                ["playerAction"] = "Получить тестовый предмет."
            }.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["UpdateInventory"] = new JsonArray(
                    MortalItemTestFixture.CreateRawRoot())
            }.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            MortalItemIdentityState.CreateEmptyRoot().ToJsonString());

        var normalizer = new CanonicalStateNormalizer(
            _fs,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync();

        var itemsRoot = JsonNode.Parse(
            (await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath))!)!.AsObject();
        Assert.False(itemsRoot.ContainsKey("UpdateInventory"));
        var item = Assert.Single(itemsRoot["items"]!.AsArray().OfType<JsonObject>());
        var itemId = item["itemId"]!.GetValue<string>();
        Assert.StartsWith("itm_", itemId, StringComparison.Ordinal);
        Assert.Equal(itemId, item["existedId"]!.GetValue<string>());
        Assert.False(item.ContainsKey("creationRef"));

        var receipt = item["materializationReceipt"]!.AsObject();
        Assert.StartsWith("mirec_", receipt["receiptId"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(itemId, receipt["itemId"]!.GetValue<string>());
        Assert.Equal(42, receipt["acceptedAtTurn"]!.GetValue<int>());

        var indexRoot = JsonNode.Parse(
            (await _fs.ReadFileAsync(MortalItemIdentityState.StatePath))!)!.AsObject();
        var entry = Assert.Single(indexRoot["entries"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(itemId, entry["itemId"]!.GetValue<string>());
        Assert.Equal(
            receipt["receiptId"]!.GetValue<string>(),
            entry["receiptId"]!.GetValue<string>());
        Assert.Equal("active", entry["state"]!.GetValue<string>());
        Assert.Equal(
            "player_inventory",
            entry["currentCarrier"]!["kind"]!.GetValue<string>());
        Assert.Equal(
            "create",
            Assert.Single(entry["transitions"]!.AsArray())!["kind"]!.GetValue<string>());

        var validator = new ValidationService(
            _fs,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ValidationService>.Instance);
        Assert.Empty(await validator.ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync());
    }

    [Fact]
    public async Task Normalize_PlayerCreations_RewritesSameTurnContainerAndEquipmentReferences()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.BuildMortalBootstrapAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        const string parentCreationRef = "new_item_parent_container";
        const string childCreationRef = "new_item_nested_child";
        var parent = MortalItemTestFixture.CreateRawRoot(
            creationRef: parentCreationRef,
            materializationId: "mat_item_parent_container");
        parent["isContainer"] = true;
        parent["capacity"] = 10;
        parent["materialization"]!["sections"]!["container"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        var child = MortalItemTestFixture.CreateRawRoot(
            creationRef: childCreationRef,
            materializationId: "mat_item_nested_child");
        child["contentsPath"] = new JsonArray(parentCreationRef);
        await context.WritePlayerUpdateAsync(parent, child);
        var rawRoot = (await context.ReadJsonAsync(
            InventoryEquipmentService.ItemsPath))!.AsObject();
        rawRoot["equipment"]!["mainHand"] = childCreationRef;
        await context.WriteJsonAsync(InventoryEquipmentService.ItemsPath, rawRoot);

        await context.NormalizeAcceptedTurnAsync();

        var canonicalRoot = (await context.ReadJsonAsync(
            InventoryEquipmentService.ItemsPath))!.AsObject();
        var items = canonicalRoot["items"]!.AsArray().OfType<JsonObject>().ToArray();
        Assert.Equal(2, items.Length);
        var parentItem = Assert.Single(items, item =>
            item["materializationReceipt"]!["creationRef"]!.GetValue<string>() == parentCreationRef);
        var childItem = Assert.Single(items, item =>
            item["materializationReceipt"]!["creationRef"]!.GetValue<string>() == childCreationRef);
        Assert.Equal(
            parentItem["itemId"]!.GetValue<string>(),
            Assert.Single(childItem["contentsPath"]!.AsArray())!.GetValue<string>());
        Assert.Equal(
            childItem["itemId"]!.GetValue<string>(),
            canonicalRoot["equipment"]!["mainHand"]!.GetValue<string>());
    }

    [Fact]
    public async Task Normalize_WriteFailure_RestoresEveryTrackedFileByteForByte()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.BuildMortalBootstrapAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WritePlayerUpdateAsync(MortalItemTestFixture.CreateRawRoot());
        var before = await context.CaptureExactBytesAsync(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        context.InjectWriteFailureBefore(MortalItemIdentityState.StatePath);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.NormalizeAcceptedTurnAsync());

        await context.AssertExactBytesAsync(before);
    }

    [Fact]
    public async Task Normalize_PostSealFailure_RestoresEveryTrackedFileByteForByte()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.BuildMortalBootstrapAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        var rawItem = MortalItemTestFixture.CreateRawRoot();
        rawItem["contentsPath"] = new JsonArray("itm_missing_parent");
        await context.WritePlayerUpdateAsync(rawItem);
        var before = await context.CaptureExactBytesAsync(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);

        var issues = await context.NormalizeAcceptedTurnWithIssuesAsync();

        Assert.Contains(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            issue.Code == "mortal_item_materialization_container_parent_missing");
        await context.AssertExactBytesAsync(before);
    }
}
