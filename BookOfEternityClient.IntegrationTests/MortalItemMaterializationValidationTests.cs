using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalItemMaterializationValidationTests
{
    [Fact]
    public async Task RawCompletePlayerCreation_PassesBeforeSeal()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        await context.WritePlayerUpdateAsync(MortalItemTestFixture.CreateRawRoot());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task RawCompletePlayerCreation_NormalizesToValidSealedState()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        await context.WritePlayerUpdateAsync(MortalItemTestFixture.CreateRawRoot());

        await context.NormalizeAcceptedTurnAsync();

        var itemsRoot = (await context.ReadJsonAsync(
            InventoryEquipmentService.ItemsPath))!.AsObject();
        var item = Assert.Single(itemsRoot["items"]!.AsArray().OfType<JsonObject>());
        Assert.False(itemsRoot.ContainsKey("UpdateInventory"));
        Assert.Equal(
            item["itemId"]!.GetValue<string>(),
            item["existedId"]!.GetValue<string>());
        Assert.NotNull(item["materializationReceipt"]);
        Assert.DoesNotContain(
            await context.Validator.ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task RawPlayerCreation_WithWrongAcceptedTurn_IsRejected()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync(turn: 42);
        await context.WritePlayerUpdateAsync(
            MortalItemTestFixture.CreateRawRoot(sourceTurn: 41));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_materialization_route_authority_mismatch" &&
            issue.Expected == "42");
    }

    [Fact]
    public async Task RawCreationWithGmAuthoredPermanentIdentity_IsRejected()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        var raw = MortalItemTestFixture.CreateRawRoot();
        raw["itemId"] = "itm_forged";
        raw["materializationReceipt"] = new JsonObject
        {
            ["receiptId"] = "mirec_forged"
        };
        await context.WritePlayerUpdateAsync(raw);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_materialization_gm_authored_client_field");
    }

    [Fact]
    public async Task RawIdentityIndexMutation_IsRejectedAsClientOwned()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        await context.WritePlayerUpdateAsync(MortalItemTestFixture.CreateRawRoot());
        var forgedCanonical = MortalItemTestFixture.CreateCanonicalRoot("itm_forged_index");
        await context.WriteJsonAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndex(forgedCanonical));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_materialization_gm_authored_client_field" &&
            issue.FilePath == MortalItemIdentityState.StatePath);
    }

    [Fact]
    public async Task CanonicalCompletePlayerCreation_WithMatchingIndex_Passes()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        await context.WriteCanonicalPlayerItemAsync(
            item,
            MortalItemTestFixture.CreateIndex(item));

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task CanonicalReceiptlessItem_IsRejectedWithoutPromotion()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        await context.WriteCanonicalPlayerItemAsync(
            MortalItemTestFixture.CreateReceiptlessNegative());

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_materialization_receiptless_current_item");
    }

    [Fact]
    public async Task CanonicalCompleteItemWithoutIndexEntry_IsRejected()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        await context.WriteCanonicalPlayerItemAsync(item);

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_materialization_missing_index_entry");
        var bounded = Assert.Single(issues, issue =>
            issue.Code == "mortal_item_materialization_missing_index_entry");
        Assert.Equal($"mortal_item:existing:{MortalItemTestFixture.ItemId}", bounded.Actor);
        Assert.Equal("MortalItemMaterialization", bounded.Section);
        Assert.Equal(
            new[] { InventoryEquipmentService.ItemsPath },
            bounded.RepairTargetFiles);
    }

    [Fact]
    public async Task CanonicalHistoricalEnvelopeRewrite_IsRejected()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.BuildMortalBootstrapAsync();
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        var index = MortalItemTestFixture.CreateIndex(item);
        await context.WriteCanonicalPlayerItemAsync(item, index);
        await context.CaptureValidatedPendingSnapshotAsync();
        item["materialization"]!["sourceTurn"] = 43;
        await context.WriteCanonicalPlayerItemAsync(item, index);

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_materialization_immutable_envelope_rewrite");
    }

    [Fact]
    public async Task CanonicalHistoricalReceiptRewrite_IsRejected()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.BuildMortalBootstrapAsync();
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        var index = MortalItemTestFixture.CreateIndex(item);
        await context.WriteCanonicalPlayerItemAsync(item, index);
        await context.CaptureValidatedPendingSnapshotAsync();
        item["materializationReceipt"]!["acceptedAtTurn"] = 43;
        await context.WriteCanonicalPlayerItemAsync(item, index);

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_materialization_immutable_receipt_rewrite");
    }

    [Fact]
    public async Task CanonicalLeaseAwareEntryPoint_UsesTheBoundCurrentState()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        await context.WriteCanonicalPlayerItemAsync(
            item,
            MortalItemTestFixture.CreateIndex(item));

        await using var writeLease =
            await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync(writeLease);

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task CanonicalDuplicateCarrierOccurrence_IsRejected()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        await context.WriteCanonicalPlayerItemAsync(
            item,
            MortalItemTestFixture.CreateIndex(item));
        await context.WriteCanonicalNpcItemAsync("npc_duplicate", item);

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_materialization_duplicate_item_id");
    }

    [Fact]
    public async Task CanonicalContentsPath_RequiresExactContainerParent()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        var parent = MortalItemTestFixture.CreateCanonicalRoot("itm_parent_not_container");
        var child = MortalItemTestFixture.CreateCanonicalRoot("itm_nested_child");
        child["contentsPath"] = new JsonArray("itm_parent_not_container");
        var parentIndex = MortalItemTestFixture.CreateIndex(parent);
        var childIndex = MortalItemTestFixture.CreateIndexForCarrier(
            child,
            "player_inventory",
            "player",
            containerPath: new JsonArray("itm_parent_not_container"));
        var index = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray(
                parentIndex["entries"]![0]!.DeepClone(),
                childIndex["entries"]![0]!.DeepClone())
        };
        await context.WriteCanonicalPlayerItemsAsync(index, parent, child);

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_materialization_container_parent_invalid" &&
            issue.Actor == "mortal_item:existing:itm_nested_child");
    }

    [Fact]
    public async Task PlayerStatePhase_RejectsReceiptlessCanonicalItem()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.WriteCanonicalPlayerItemAsync(
            MortalItemTestFixture.CreateReceiptlessNegative());

        var issues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.PlayerStateFiles);

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_materialization_receiptless_current_item");
    }

    [Fact]
    public async Task NpcStatePhase_RejectsReceiptlessCanonicalInventoryItem()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.WriteCanonicalNpcItemAsync(
            "npc_receiptless",
            MortalItemTestFixture.CreateReceiptlessNegative());

        var issues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.NpcStateFiles);

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_materialization_receiptless_current_item");
    }

    [Fact]
    public async Task EmptyMortalBootstrap_PassesRawAndCanonicalItemValidation()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();

        var rawIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        var canonicalIssues = await context.Validator
            .ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync();

        Assert.DoesNotContain(rawIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(canonicalIssues, issue => issue.Severity == IssueSeverity.Error);
    }
}
