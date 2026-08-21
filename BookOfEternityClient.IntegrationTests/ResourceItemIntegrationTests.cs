using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceItemIntegrationTests
{
    [Fact]
    public async Task Transfer_PreservesExactItemResourceCoordinateStateAndHistory()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        var itemIds = await MaterializePlayerItemsAsync(
            context,
            ResourceItem(
                MortalItemTestFixture.CreateRawRoot(),
                ("durability", 100m),
                ("charges", 3m)));
        var itemId = Assert.Single(itemIds);
        await context.WriteJsonAsync(
            NpcCoreChangesContract.NpcCorePath,
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(),
                ["NPCsInScene"] = new JsonArray(
                    new JsonObject
                    {
                        ["NPCId"] = "npc_resource_transfer",
                        ["inventory"] = new JsonArray(),
                        ["equippedItems"] = new JsonObject()
                    })
            });
        var beforeResources = await context.CaptureExactBytesAsync(ResourcePaths);

        var result = await ExecuteAsync(
            context,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Transfer,
                new[] { itemId },
                PlayerCarrier,
                new MortalItemCarrierCoordinate(
                    "npc_inventory",
                    "npc_resource_transfer",
                    null,
                    Array.Empty<string>()),
                Quantity: 1,
                Turn: 43,
                AuthorityKind: "inventory_transfer",
                AuthorityId: "transfer_resource_item_43"));

        Assert.True(result.Success, result.Message);
        await context.AssertExactBytesAsync(beforeResources);
        var state = await ReadStateAsync(context);
        Assert.Equal(2, state.Entries.Count(entry =>
            entry.Coordinate.OwnerKind == ResourceOwnerKind.Item &&
            entry.Coordinate.ResourceOwnerId == itemId));
    }

    [Fact]
    public async Task NpcCarriedItem_UsesTheSamePermanentItemResourceCommandRoute()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        var itemId = Assert.Single(await MaterializePlayerItemsAsync(
            context,
            ResourceItem(
                MortalItemTestFixture.CreateRawRoot(),
                ("charges", 3m))));
        await context.WriteJsonAsync(
            NpcCoreChangesContract.NpcCorePath,
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(),
                ["NPCsInScene"] = new JsonArray(
                    new JsonObject
                    {
                        ["NPCId"] = "npc_resource_user",
                        ["inventory"] = new JsonArray(),
                        ["equippedItems"] = new JsonObject()
                    })
            });
        var transfer = await ExecuteAsync(
            context,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Transfer,
                new[] { itemId },
                PlayerCarrier,
                new MortalItemCarrierCoordinate(
                    "npc_inventory",
                    "npc_resource_user",
                    null,
                    Array.Empty<string>()),
                Quantity: 1,
                Turn: 43,
                AuthorityKind: "inventory_transfer",
                AuthorityId: "transfer_resource_item_to_npc_43"));
        Assert.True(transfer.Success, transfer.Message);

        await ApplyItemResourceCommandsAsync(
            context,
            turn: 44,
            itemId,
            ("spend", "charges", 1m, "local_item_cost"));

        var state = await ReadStateAsync(context);
        AssertResource(state, itemId, "charges", current: 2m, maximum: 3m);
    }

    [Fact]
    public async Task Destroy_RetiresEveryLiveItemResourceInTheSameTransaction()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        var itemIds = await MaterializePlayerItemsAsync(
            context,
            ResourceItem(
                MortalItemTestFixture.CreateRawRoot(),
                ("durability", 100m),
                ("charges", 3m),
                ("ammunition", 6m)));
        var itemId = Assert.Single(itemIds);

        var result = await ExecuteAsync(
            context,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Destroy,
                new[] { itemId },
                PlayerCarrier,
                DestinationCarrier: null,
                Quantity: 1,
                Turn: 43,
                AuthorityKind: "inventory_discard",
                AuthorityId: "destroy_resource_item_43"));

        Assert.True(result.Success, result.Message);
        var definitions = await ReadDefinitionsAsync(context);
        var state = await ReadStateAsync(context, definitions);
        var history = await ReadHistoryAsync(context, definitions);
        Assert.DoesNotContain(state.Entries, entry =>
            entry.Coordinate.OwnerKind == ResourceOwnerKind.Item &&
            entry.Coordinate.ResourceOwnerId == itemId);
        var terminal = history.Transitions
            .Where(transition =>
                transition.Coordinate.OwnerKind == ResourceOwnerKind.Item &&
                transition.Coordinate.ResourceOwnerId == itemId &&
                transition.Operation == ResourceTransitionOperation.Retire)
            .OrderBy(transition => transition.Coordinate.ResourceKey, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "ammunition", "charges", "durability" },
            terminal.Select(transition => transition.Coordinate.ResourceKey));
        Assert.All(terminal, transition =>
        {
            Assert.NotNull(transition.BeforeState);
            Assert.Null(transition.AfterState);
            Assert.Equal("owner_lifecycle", transition.SourceEvidence.SourceKind);
            Assert.Equal(itemId, transition.OriginId);
            Assert.Equal(43, transition.Turn);
        });
        Assert.Empty(history.ValidateStateAgreement(state));
    }

    [Theory]
    [InlineData("split")]
    [InlineData("merge")]
    public async Task StackMutation_WithLiveResourcesAndNoDisposition_FailsWithoutWrites(
        string operation)
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        var first = ResourceItem(
            MortalItemTestFixture.CreateRawRoot(
                creationRef: "item_ref_resource_stack_a",
                materializationId: "mat_resource_stack_a"),
            ("durability", 100m),
            ("charges", 8m));
        first["count"] = 4;
        var rawItems = operation == "merge"
            ? new[]
            {
                first,
                ResourceItem(
                    MortalItemTestFixture.CreateRawRoot(
                        creationRef: "item_ref_resource_stack_b",
                        materializationId: "mat_resource_stack_b"),
                    ("durability", 100m),
                    ("charges", 8m))
            }
            : new[] { first };
        rawItems[^1]["count"] = operation == "merge" ? 2 : 4;
        var itemIds = await MaterializePlayerItemsAsync(context, rawItems);
        var before = await context.CaptureExactBytesAsync(
            ResourcePaths.Concat(new[]
            {
                InventoryEquipmentService.ItemsPath,
                MortalItemIdentityState.StatePath
            }));
        var intent = operation == "split"
            ? new MortalItemTransitionIntent(
                MortalItemTransitionKind.Split,
                new[] { itemIds[0] },
                PlayerCarrier,
                PlayerCarrier,
                Quantity: 1,
                Turn: 43,
                AuthorityKind: "inventory_split",
                AuthorityId: "split_resource_item_43")
            : new MortalItemTransitionIntent(
                MortalItemTransitionKind.Merge,
                itemIds,
                PlayerCarrier,
                PlayerCarrier,
                Quantity: 6,
                Turn: 43,
                AuthorityKind: "inventory_merge",
                AuthorityId: "merge_resource_item_43",
                SurvivorItemId: itemIds[0]);

        var result = await ExecuteAsync(context, intent);

        Assert.False(result.Success);
        Assert.Contains("resource", result.Message, StringComparison.OrdinalIgnoreCase);
        await context.AssertExactBytesAsync(before);
    }

    [Fact]
    public async Task Split_ProportionalExact_PartitionsEveryResourceWithoutRounding()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        var raw = ResourceItem(
            MortalItemTestFixture.CreateRawRoot(),
            ("durability", 100m),
            ("charges", 8m));
        raw["count"] = 4;
        var parentId = Assert.Single(await MaterializePlayerItemsAsync(context, raw));

        var result = await ExecuteAsync(
            context,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Split,
                new[] { parentId },
                PlayerCarrier,
                PlayerCarrier,
                Quantity: 1,
                Turn: 43,
                AuthorityKind: "inventory_split",
                AuthorityId: "split_resource_item_exact_43",
                ResourceDisposition: MortalItemResourceStackDisposition.ProportionalExact));

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.DerivedItemId);
        var childId = result.DerivedItemId!;
        var definitions = await ReadDefinitionsAsync(context);
        var state = await ReadStateAsync(context, definitions);
        AssertResource(state, parentId, "durability", current: 75m, maximum: 75m);
        AssertResource(state, parentId, "charges", current: 6m, maximum: 6m);
        AssertResource(state, childId, "durability", current: 25m, maximum: 25m);
        AssertResource(state, childId, "charges", current: 2m, maximum: 2m);
        var history = await ReadHistoryAsync(context, definitions);
        Assert.Empty(history.ValidateStateAgreement(state));
        Assert.Equal(2, history.Transitions.Count(transition =>
            transition.Operation == ResourceTransitionOperation.Reconfigure &&
            transition.Coordinate.ResourceOwnerId == parentId &&
            transition.Turn == 43));
        Assert.Equal(2, history.Transitions.Count(transition =>
            transition.Operation == ResourceTransitionOperation.Initialize &&
            transition.Coordinate.ResourceOwnerId == childId &&
            transition.Turn == 43));
    }

    [Fact]
    public async Task Merge_ProportionalExact_CombinesResourcesAndRetiresContributors()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        var first = ResourceItem(
            MortalItemTestFixture.CreateRawRoot(
                creationRef: "item_ref_resource_merge_a",
                materializationId: "mat_resource_merge_a"),
            ("durability", 100m),
            ("charges", 8m));
        first["count"] = 4;
        var second = ResourceItem(
            MortalItemTestFixture.CreateRawRoot(
                creationRef: "item_ref_resource_merge_b",
                materializationId: "mat_resource_merge_b"),
            ("durability", 50m),
            ("charges", 4m));
        second["count"] = 2;
        var itemIds = await MaterializePlayerItemsAsync(context, first, second);
        var beforeState = await ReadStateAsync(context);
        var survivorId = beforeState.Entries.Single(entry =>
            entry.Coordinate.ResourceKey == "durability" &&
            entry.Maximum == 100m).Coordinate.ResourceOwnerId;
        var contributorId = Assert.Single(itemIds, id => id != survivorId);

        var result = await ExecuteAsync(
            context,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Merge,
                itemIds,
                PlayerCarrier,
                PlayerCarrier,
                Quantity: 6,
                Turn: 43,
                AuthorityKind: "inventory_merge",
                AuthorityId: "merge_resource_item_exact_43",
                SurvivorItemId: survivorId,
                ResourceDisposition: MortalItemResourceStackDisposition.ProportionalExact));

        Assert.True(result.Success, result.Message);
        var definitions = await ReadDefinitionsAsync(context);
        var state = await ReadStateAsync(context, definitions);
        AssertResource(state, survivorId, "durability", current: 150m, maximum: 150m);
        AssertResource(state, survivorId, "charges", current: 12m, maximum: 12m);
        Assert.DoesNotContain(state.Entries, entry =>
            entry.Coordinate.ResourceOwnerId == contributorId);
        var history = await ReadHistoryAsync(context, definitions);
        Assert.Empty(history.ValidateStateAgreement(state));
        Assert.Equal(2, history.Transitions.Count(transition =>
            transition.Operation == ResourceTransitionOperation.Retire &&
            transition.Coordinate.ResourceOwnerId == contributorId &&
            transition.Turn == 43));
    }

    [Fact]
    public async Task TerminalConsume_RetiresResourcesAndMarksTheItemConsumed()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        var itemId = Assert.Single(await MaterializePlayerItemsAsync(
            context,
            ResourceItem(
                MortalItemTestFixture.CreateRawRoot(),
                ("charges", 1m))));

        var result = await ExecuteAsync(
            context,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Consume,
                new[] { itemId },
                PlayerCarrier,
                DestinationCarrier: null,
                Quantity: 1,
                Turn: 43,
                AuthorityKind: "inventory_consume",
                AuthorityId: "consume_resource_item_43"));

        Assert.True(result.Success, result.Message);
        var state = await ReadStateAsync(context);
        Assert.DoesNotContain(state.Entries, entry =>
            entry.Coordinate.ResourceOwnerId == itemId);
        var identity = MortalItemIdentityState.Parse(
            await context.FileSystem.ReadFileAsync(MortalItemIdentityState.StatePath));
        Assert.Empty(identity.Issues);
        Assert.Equal("consumed", identity.EntriesByItemId[itemId]["state"]!.GetValue<string>());
    }

    [Fact]
    public async Task UseRepairFireAndReload_ReduceThroughTheCommonItemResourceLedger()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        var itemId = Assert.Single(await MaterializePlayerItemsAsync(
            context,
            ResourceItem(
                MortalItemTestFixture.CreateRawRoot(),
                ("durability", 100m),
                ("charges", 3m),
                ("ammunition", 6m))));

        await ApplyItemResourceCommandsAsync(
            context,
            turn: 43,
            itemId,
            ("damage", "durability", 40m, "local_item_outcome"),
            ("spend", "charges", 1m, "local_item_cost"),
            ("spend", "ammunition", 2m, "local_item_cost"));

        var afterUse = await ReadStateAsync(context);
        AssertResource(afterUse, itemId, "durability", current: 60m, maximum: 100m);
        AssertResource(afterUse, itemId, "charges", current: 2m, maximum: 3m);
        AssertResource(afterUse, itemId, "ammunition", current: 4m, maximum: 6m);

        await ApplyItemResourceCommandsAsync(
            context,
            turn: 44,
            itemId,
            ("restore", "durability", 25m, "local_item_outcome"),
            ("gain", "ammunition", 1m, "local_item_outcome"));

        var afterRepair = await ReadStateAsync(context);
        AssertResource(afterRepair, itemId, "durability", current: 85m, maximum: 100m);
        AssertResource(afterRepair, itemId, "ammunition", current: 5m, maximum: 6m);
        var definitions = await ReadDefinitionsAsync(context);
        var history = await ReadHistoryAsync(context, definitions);
        Assert.Empty(history.ValidateStateAgreement(afterRepair));
        var turn43Operations = history.Transitions
            .Where(transition => transition.Turn == 43)
            .OrderBy(transition => transition.ExecutionSequence)
            .Select(transition => transition.Operation.ToString().ToLowerInvariant())
            .ToArray();
        Assert.Equal(new[] { "spend", "spend", "damage" }, turn43Operations);

        var turn44Operations = history.Transitions
            .Where(transition => transition.Turn == 44)
            .Select(transition => transition.Operation.ToString().ToLowerInvariant())
            .OrderBy(operation => operation, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "gain", "restore" }, turn44Operations);
    }

    [Fact]
    public async Task LocalItemSource_CannotMutateAnotherItemResource()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        var items = await MaterializePlayerItemsAsync(
            context,
            ResourceItem(
                MortalItemTestFixture.CreateRawRoot(
                    creationRef: "item_ref_resource_source_a",
                    materializationId: "mat_resource_source_a"),
                ("charges", 3m)),
            ResourceItem(
                MortalItemTestFixture.CreateRawRoot(
                    creationRef: "item_ref_resource_target_b",
                    materializationId: "mat_resource_target_b"),
                ("charges", 3m)));
        var sourceId = items[0];
        var targetId = items[1];
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.CommandPath,
            ResourceCommands(
                turn: 43,
                targetId,
                sourceId,
                ("spend", "charges", 1m, "local_item_cost")));
        var before = await context.CaptureExactBytesAsync(ResourcePaths);

        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.True(
            issues.Any(issue => issue.Code == "resource_source_target_mismatch"),
            string.Join(Environment.NewLine, issues.Select(issue =>
                $"{issue.Code}: {issue.Message} expected={issue.Expected} actual={issue.Actual}")));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem));
        await context.AssertExactBytesAsync(before);
    }

    [Fact]
    public async Task SettingDefinedGenericItemReserve_UsesTheSameMaterializationAndMutationPath()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.BuildMortalBootstrapAsync();
        await PublishGenericItemReserveDefinitionAsync(context, turn: 41);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        await context.WritePlayerUpdateAsync(ResourceItem(
            MortalItemTestFixture.CreateRawRoot(),
            ("generic_reserve", 12m)));
        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
        var resourceIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(resourceIssues, issue => issue.Severity == IssueSeverity.Error);
        await context.NormalizeAcceptedTurnAsync();
        var inventory = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            InventoryEquipmentService.ItemsPath));
        var itemId = Assert.IsType<JsonObject>(Assert.Single(inventory["items"]!.AsArray()))[
            "itemId"]!.GetValue<string>();

        await ApplyItemResourceCommandsAsync(
            context,
            turn: 43,
            itemId,
            ("spend", "generic_reserve", 5m, "local_item_cost"));

        var state = await ReadStateAsync(context);
        AssertResource(state, itemId, "generic_reserve", current: 7m, maximum: 12m);
    }

    [Fact]
    public async Task Destroy_WhenResourceHistoryWriteFails_RollsBackEveryOwnedFile()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        var itemId = Assert.Single(await MaterializePlayerItemsAsync(
            context,
            ResourceItem(
                MortalItemTestFixture.CreateRawRoot(),
                ("durability", 100m),
                ("charges", 3m))));
        var before = await context.CaptureExactBytesAsync(
            ResourcePaths.Concat(new[]
            {
                InventoryEquipmentService.ItemsPath,
                MortalItemIdentityState.StatePath
            }));
        context.InjectWriteFailureBefore(ResourceMaterializationContract.HistoryPath);

        var result = await ExecuteAsync(
            context,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Destroy,
                new[] { itemId },
                PlayerCarrier,
                DestinationCarrier: null,
                Quantity: 1,
                Turn: 43,
                AuthorityKind: "inventory_discard",
                AuthorityId: "destroy_resource_item_rollback_43"));

        Assert.False(result.Success);
        await context.AssertExactBytesAsync(before);
    }

    private static readonly string[] ResourcePaths =
    {
        ResourceMaterializationContract.DefinitionsPath,
        ResourceMaterializationContract.StatePath,
        ResourceMaterializationContract.HistoryPath
    };

    private static readonly MortalItemCarrierCoordinate PlayerCarrier = new(
        "player_inventory",
        "player",
        null,
        Array.Empty<string>());

    private static async Task<IReadOnlyList<string>> MaterializePlayerItemsAsync(
        MortalItemMaterializationTestContext context,
        params JsonObject[] items)
    {
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
        await context.WritePlayerUpdateAsync(items);
        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        var resourceIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(resourceIssues, issue => issue.Severity == IssueSeverity.Error);
        await context.NormalizeAcceptedTurnAsync();
        var inventory = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            InventoryEquipmentService.ItemsPath));
        return inventory["items"]!.AsArray()
            .OfType<JsonObject>()
            .Select(item => item["itemId"]!.GetValue<string>())
            .ToArray();
    }

    private static JsonObject ResourceItem(
        JsonObject item,
        params (string Key, decimal Maximum)[] resources)
    {
        item.Remove("durability");
        item.Remove("maxDurability");
        item["resourceMaterialization"] = new JsonObject
        {
            ["resources"] = new JsonArray(resources
                .Select(resource => (JsonNode)new JsonObject
                {
                    ["resourceKey"] = resource.Key,
                    ["maximum"] = resource.Maximum
                })
                .ToArray())
        };
        return item;
    }

    private static async Task<MortalItemTransitionResult> ExecuteAsync(
        MortalItemMaterializationTestContext context,
        MortalItemTransitionIntent intent)
    {
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        return await new MortalItemTransitionWriter(context.FileSystem).ExecuteAsync(lease, intent);
    }

    private static async Task ApplyItemResourceCommandsAsync(
        MortalItemMaterializationTestContext context,
        int turn,
        string itemId,
        params (string Operation, string ResourceKey, decimal Amount, string SourceKind)[] changes)
    {
        await context.CaptureValidatedPendingSnapshotAsync(turn);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.CommandPath,
            ResourceCommands(turn, itemId, itemId, changes));

        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.False(
            issues.Any(issue => issue.Severity == IssueSeverity.Error),
            string.Join(Environment.NewLine, issues.Select(issue =>
                $"{issue.Code}: {issue.Message} expected={issue.Expected} actual={issue.Actual}")));
        await context.NormalizeAcceptedTurnAsync();
    }

    private static JsonObject ResourceCommands(
        int turn,
        string targetItemId,
        string sourceItemId,
        params (string Operation, string ResourceKey, decimal Amount, string SourceKind)[] changes) =>
        new()
        {
            ["resourceDefinitionCreations"] = new JsonArray(),
            ["resourceCapacityChanges"] = new JsonArray(),
            ["resourceChanges"] = new JsonArray(changes
                .Select((change, index) => (JsonNode)new JsonObject
                {
                    ["operation"] = change.Operation,
                    ["target"] = new JsonObject
                    {
                        ["kind"] = "item",
                        ["targetId"] = targetItemId
                    },
                    ["resourceKey"] = change.ResourceKey,
                    ["amount"] = change.Amount,
                    ["source"] = new JsonObject
                    {
                        ["kind"] = change.SourceKind,
                        ["sourceId"] = sourceItemId
                    },
                    ["eventRef"] = $"turn_{turn}:resource:{index + 1}",
                    ["reason"] = "Exact local item resource operation"
                })
                .ToArray())
        };

    private static async Task PublishGenericItemReserveDefinitionAsync(
        MortalItemMaterializationTestContext context,
        int turn)
    {
        await context.CaptureValidatedPendingSnapshotAsync(turn);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.CommandPath,
            new JsonObject
            {
                ["resourceDefinitionCreations"] = new JsonArray(
                    new JsonObject
                    {
                        ["definitionRef"] = "generic_reserve_v1",
                        ["definition"] = new JsonObject
                        {
                            ["resourceKey"] = "generic_reserve",
                            ["definitionVersion"] = 1,
                            ["displayName"] = "Запас",
                            ["numericKind"] = "integer",
                            ["unit"] = "charge",
                            ["quantum"] = 1,
                            ["minimumPolicy"] = new JsonObject
                            {
                                ["kind"] = "definition_fixed",
                                ["value"] = 0
                            },
                            ["capacityPolicy"] = new JsonObject
                            {
                                ["kind"] = "instance_fixed"
                            },
                            ["initializationPolicy"] = new JsonObject
                            {
                                ["kind"] = "maximum"
                            },
                            ["allowedOwnerKinds"] = new JsonArray("item"),
                            ["allowedOperations"] = new JsonArray("spend", "gain"),
                            ["defaultFloorPolicy"] = "reject_below_minimum",
                            ["defaultCapPolicy"] = "clamp_to_maximum",
                            ["visibility"] = "owner_visible"
                        },
                        ["eventRef"] = $"turn_{turn}:resource:1",
                        ["reason"] = "Materialize one setting-defined item reserve"
                    }),
                ["resourceCapacityChanges"] = new JsonArray(),
                ["resourceChanges"] = new JsonArray()
            });
        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        await context.NormalizeAcceptedTurnAsync();
    }

    private static async Task<ResourceDefinitionCatalog> ReadDefinitionsAsync(
        MortalItemMaterializationTestContext context)
    {
        var parsed = ResourceDefinitionCatalog.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath),
            allowMissingPristine: false);
        Assert.True(parsed.IsValid, string.Join(Environment.NewLine, parsed.Issues));
        return parsed.Catalog!;
    }

    private static async Task<ResourceStateLedger> ReadStateAsync(
        MortalItemMaterializationTestContext context,
        ResourceDefinitionCatalog? definitions = null)
    {
        definitions ??= await ReadDefinitionsAsync(context);
        var parsed = ResourceStateContract.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions,
            allowMissingPristine: false);
        Assert.True(parsed.IsValid, string.Join(Environment.NewLine, parsed.Issues));
        return parsed.Ledger!;
    }

    private static async Task<ResourceHistoryState> ReadHistoryAsync(
        MortalItemMaterializationTestContext context,
        ResourceDefinitionCatalog definitions)
    {
        var parsed = ResourceHistoryState.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            definitions,
            allowMissingPristine: false);
        Assert.True(parsed.IsValid, string.Join(Environment.NewLine, parsed.Issues));
        return parsed.History!;
    }

    private static void AssertResource(
        ResourceStateLedger state,
        string itemId,
        string resourceKey,
        decimal current,
        decimal maximum)
    {
        var entry = Assert.Single(state.Entries, candidate =>
            candidate.Coordinate.OwnerKind == ResourceOwnerKind.Item &&
            candidate.Coordinate.ResourceOwnerId == itemId &&
            candidate.Coordinate.ResourceKey == resourceKey);
        Assert.Equal(current, entry.Current);
        Assert.Equal(maximum, entry.Maximum);
    }
}
