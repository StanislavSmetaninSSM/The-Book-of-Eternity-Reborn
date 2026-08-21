using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceItemOwnerTests
{
    [Fact]
    public async Task AcceptedTurn_NewItemUsesOnePermanentOwnerForItemSealAndResourceInitialization()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.BuildMortalBootstrapAsync();
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        await context.WriteJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            JsonNode.Parse(bootstrap.Definitions!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.StatePath,
            JsonNode.Parse(bootstrap.State!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            JsonNode.Parse(bootstrap.History!.ToCanonicalJson())!);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        var rawItem = ResourceItem(
            MortalItemTestFixture.CreateRawRoot(),
            ("durability", 100m),
            ("charges", 3m),
            ("ammunition", 6m));
        await context.WritePlayerUpdateAsync(rawItem);

        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        var resourceIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(resourceIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            out _,
            out var planning));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(planning.Plan);
        var itemOwner = Assert.Single(
            plan.OwnerAuthority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Item);
        Assert.True(itemOwner.SameTurn);
        Assert.Equal(MortalItemTestFixture.CreationRef, itemOwner.SameTurnRef);

        var plannedDefinitions = ResourceDefinitionCatalog.ParseCanonical(
            plan.DefinitionAfterImage.ToJsonString(),
            allowMissingPristine: false);
        Assert.True(
            plannedDefinitions.IsValid,
            string.Join(Environment.NewLine, plannedDefinitions.Issues));
        var plannedState = ResourceStateContract.ParseCanonical(
            plan.StateAfterImage.ToJsonString(),
            plannedDefinitions.Catalog!,
            allowMissingPristine: false);
        Assert.True(plannedState.IsValid, string.Join(Environment.NewLine, plannedState.Issues));
        Assert.Equal(
            new[] { "ammunition", "charges", "durability" },
            plannedState.Ledger!.Entries
                .Where(entry => entry.Coordinate.OwnerKind == ResourceOwnerKind.Item)
                .OrderBy(entry => entry.Coordinate.ResourceKey, StringComparer.Ordinal)
                .Select(entry => entry.Coordinate.ResourceKey));
        Assert.All(
            plannedState.Ledger.Entries.Where(entry =>
                entry.Coordinate.OwnerKind == ResourceOwnerKind.Item),
            entry => Assert.Equal(itemOwner.Key.ResourceOwnerId, entry.Coordinate.ResourceOwnerId));

        await context.NormalizeAcceptedTurnAsync();

        var inventory = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            InventoryEquipmentService.ItemsPath));
        var canonicalItem = Assert.IsType<JsonObject>(
            Assert.Single(inventory["items"]!.AsArray()));
        Assert.Equal(itemOwner.Key.ResourceOwnerId, canonicalItem["itemId"]!.GetValue<string>());
        Assert.Equal(itemOwner.Key.ResourceOwnerId, canonicalItem["existedId"]!.GetValue<string>());
        Assert.False(canonicalItem.ContainsKey("creationRef"));
        Assert.False(canonicalItem.ContainsKey("resourceMaterialization"));
        Assert.False(canonicalItem.ContainsKey("durability"));
        Assert.False(canonicalItem.ContainsKey("maxDurability"));
    }

    [Fact]
    public void ItemContract_AcceptsResourceMaterializationWithoutLegacyDurabilityFields()
    {
        var item = ResourceItem(
            MortalItemTestFixture.CreateRawRoot(),
            ("durability", 100m),
            ("charges", 3m),
            ("ammunition", 6m));

        using var document = JsonDocument.Parse(item.ToJsonString());
        var issues = MortalItemMaterializationContract.Validate(
            document.RootElement,
            "game_state/inventory/items.json.UpdateInventory[0]",
            MortalItemMaterializationPhase.RawPreSeal);

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void ItemContract_RejectsLegacyDurabilityAuthority()
    {
        var item = MortalItemTestFixture.CreateRawRoot();
        item["durability"] = "100%";
        item["maxDurability"] = "100%";
        item["resourceMaterialization"] = Materialization(("durability", 100m));

        using var document = JsonDocument.Parse(item.ToJsonString());
        var issues = MortalItemMaterializationContract.Validate(
            document.RootElement,
            "game_state/inventory/items.json.UpdateInventory[0]",
            MortalItemMaterializationPhase.RawPreSeal);

        Assert.Contains(issues, issue =>
            issue.Code == "resource_owner_legacy_value_forbidden" &&
            issue.FilePath.EndsWith(".durability", StringComparison.Ordinal));
        Assert.Contains(issues, issue =>
            issue.Code == "resource_owner_legacy_value_forbidden" &&
            issue.FilePath.EndsWith(".maxDurability", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidatedItemAuthority_AllocatesEveryCarrierButExportsOnlyEligibleEffectSource()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var player = ResourceItem(NewItem("player"), ("durability", 100m));
        var npc = ResourceItem(NewItem("npc"), ("charges", 4m));
        var currentStorage = ResourceItem(NewItem("current_storage"), ("ammunition", 12m));
        var offscreenStorage = ResourceItem(NewItem("offscreen_storage"), ("durability", 80m));
        var vehicle = ResourceItem(NewItem("vehicle"), ("charges", 2m));
        var offscreen = MortalLocationStorageContentsState.BuildCanonicalRoot(
            new Dictionary<MortalLocationStorageKey, JsonArray>
            {
                [new MortalLocationStorageKey("loc_remote", "storage_remote")] =
                    new JsonArray(offscreenStorage)
            });
        var catalog = MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            new JsonObject { ["UpdateInventory"] = Items(player) },
            new JsonObject
            {
                ["NPCsInScene"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["NPCId"] = "npc_owner",
                        ["inventory"] = Items(npc)
                    }
                }
            },
            null,
            new JsonObject
            {
                ["locationId"] = "loc_current",
                ["locationStorages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["storageId"] = "storage_current",
                        ["contents"] = Items(currentStorage)
                    }
                }
            },
            new JsonObject
            {
                ["vehicles"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["vehicleId"] = "vehicle_owner",
                        ["inventory"] = Items(vehicle)
                    }
                }
            },
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            offscreen));
        Assert.Empty(catalog.Issues);

        MortalItemAcceptedTurnAuthority.RegisterValidatedItems(
            context.FileSystem,
            "session_item_authority",
            "request_item_authority",
            catalog,
            Array.Empty<string>());

        var allocatedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var creationRef in new[]
                 {
                     Ref("player"),
                     Ref("npc"),
                     Ref("current_storage"),
                     Ref("offscreen_storage"),
                     Ref("vehicle")
                 })
        {
            Assert.True(MortalItemAcceptedTurnAuthority.TryGetAllocatedItemId(
                context.FileSystem,
                "session_item_authority",
                "request_item_authority",
                creationRef,
                out var itemId));
            Assert.StartsWith("itm_", itemId, StringComparison.Ordinal);
            Assert.True(allocatedIds.Add(itemId));
        }

        var effectSource = Assert.Single(
            MortalItemAcceptedTurnAuthority.GetValidatedEffectSources(
                context.FileSystem,
                "session_item_authority",
                "request_item_authority"));
        Assert.Equal(Ref("player"), effectSource.SourceRef);
        Assert.Equal(allocatedIds.Single(id =>
            MortalItemAcceptedTurnAuthority.TryGetAllocatedItemId(
                context.FileSystem,
                "session_item_authority",
                "request_item_authority",
                Ref("player"),
                out var playerId) &&
            string.Equals(id, playerId, StringComparison.Ordinal)), effectSource.SourceId);
    }

    private static JsonObject ResourceItem(
        JsonObject item,
        params (string Key, decimal Maximum)[] resources)
    {
        item.Remove("durability");
        item.Remove("maxDurability");
        item["resourceMaterialization"] = Materialization(resources);
        return item;
    }

    private static JsonObject Materialization(
        params (string Key, decimal Maximum)[] resources) =>
        new()
        {
            ["resources"] = new JsonArray(resources
                .Select(resource => (JsonNode)new JsonObject
                {
                    ["resourceKey"] = resource.Key,
                    ["maximum"] = resource.Maximum
                })
                .ToArray())
        };

    private static JsonObject NewItem(string suffix) =>
        MortalItemTestFixture.CreateRawRoot(
            creationRef: Ref(suffix),
            materializationId: $"mat_resource_{suffix}");

    private static string Ref(string suffix) => $"item_ref_resource_{suffix}";

    private static JsonArray Items(params JsonObject[] items) =>
        new(items.Select(item => (JsonNode?)item.DeepClone()).ToArray());
}
