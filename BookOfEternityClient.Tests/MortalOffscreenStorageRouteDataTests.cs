using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalOffscreenStorageRouteDataTests
{
    [Fact]
    public async Task BorrowedCatalogEliminatesOnlyCatalogSourceReadsAndRetainsFreshAuthorityReads()
    {
        await using var fixture = await TinyRouteFiles.CreateAsync();
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var before = fixture.Reads.Capture();
        var baseline = await MortalItemRouteAuthorityCatalog.BuildAsync(fixture.Files, lease);
        Assert.Empty(baseline.Issues);
        var afterBaseline = fixture.Reads.Capture();
        var borrowed = await MortalItemRouteAuthorityCatalog.BuildAsync(fixture.Files, lease, suppliedCatalog: fixture.Catalog);
        Assert.Empty(borrowed.Issues);
        var afterBorrowed = fixture.Reads.Capture();
        Assert.Equal(baseline.ByCreationRef[MortalItemTestFixture.CreationRef].AuthorityId,
            borrowed.ByCreationRef[MortalItemTestFixture.CreationRef].AuthorityId);
        foreach (var path in TinyRouteFiles.CatalogOnlyPaths)
        {
            Assert.True(afterBaseline.GetValueOrDefault(path) > before.GetValueOrDefault(path), path);
            Assert.Equal(afterBaseline.GetValueOrDefault(path), afterBorrowed.GetValueOrDefault(path));
        }
        foreach (var path in TinyRouteFiles.AuthorityPaths)
            Assert.True(afterBorrowed.GetValueOrDefault(path) > afterBaseline.GetValueOrDefault(path), path);
        await fixture.Files.WriteFileAtomicAsync(lease, "input/turn_request.json", "{\"turnNumber\":43}");
        var changed = await MortalItemRouteAuthorityCatalog.BuildAsync(fixture.Files, lease, suppliedCatalog: fixture.Catalog);
        Assert.NotEmpty(changed.Issues);
        Assert.Empty(changed.ByCreationRef);
    }

    [Fact]
    public async Task BorrowedMutableInputsCannotRewriteReturnedRouteDataOrPassAnotherAdmission()
    {
        await using var fixture = await TinyRouteFiles.CreateAsync();
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var admitted = await MortalItemRouteAuthorityCatalog.BuildAsync(fixture.Files, lease, suppliedCatalog: fixture.Catalog);
        Assert.Empty(admitted.Issues);
        fixture.Catalog.ByCreationRef[MortalItemTestFixture.CreationRef][0].Item["materialization"]!["sourceAuthority"]!["authorityId"] = "turn_wrong";
        Assert.Equal("turn_42", admitted.ByCreationRef[MortalItemTestFixture.CreationRef].AuthorityId);
        var retry = await MortalItemRouteAuthorityCatalog.BuildAsync(fixture.Files, lease, suppliedCatalog: fixture.Catalog);
        Assert.Contains(retry.Issues, issue => issue.Code == "mortal_item_materialization_route_authority_mismatch");
    }

    [Fact]
    public async Task BorrowedCatalogStorageRoutePreservesNestedOccurrenceAncestry()
    {
        await using var fixture = await TinyRouteFiles.CreateAsync();
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var input = MortalOffscreenStorageBirthTests.Input();
        var storage = input.RawWorldMapUpdates!["storagesToAdd"]![0]!["storage"]!.AsObject();
        var root = storage["contents"]![0]!.AsObject();
        root["isContainer"] = true; root["capacity"] = 2;
        root["materialization"]!["sections"]!["container"] = new JsonObject { ["state"] = "populated", ["reason"] = null };
        var child = MortalItemTestFixture.CreateRawRoot("storage_placement", "location_storage",
            "loc_offscreen_birth_b:storage_offscreen_birth", creationRef: "new_nested", materializationId: "mat_nested");
        child["contentsPath"] = new JsonArray(MortalItemTestFixture.CreationRef);
        storage["contents"]!.AsArray().Add(child);
        var planning = MortalLocationAcceptedTurnPlanner.Build(input);
        Assert.True(planning.Success, string.Join(";", planning.Issues.Select(i => i.Code)));
        var plan = planning.Plan!;
        var catalog = MortalItemCarrierCatalog.Build(new(null, null, null, plan.FinalCurrentLocation, null,
            new Dictionary<string, JsonObject>(), plan.FinalStorageContents));
        Assert.Empty(catalog.Issues);
        var occurrence = Assert.Single(catalog.ByCreationRef["new_nested"]);
        Assert.Equal(new[] { MortalItemTestFixture.CreationRef }, occurrence.Carrier.ContainerPath);
        var routes = await MortalItemRouteAuthorityCatalog.BuildAsync(fixture.Files, lease,
            suppliedCatalog: catalog, storageBirthDrafts: plan.StorageBirthDrafts);
        Assert.Empty(routes.Issues);
        var mapped = routes.ByCreationRef["new_nested"];
        Assert.Equal("loc_offscreen_birth_b:storage_offscreen_birth", mapped.AuthorityId);
        Assert.Equal(occurrence.Carrier.OwnerId, mapped.Destination.OwnerId);
        Assert.Equal(occurrence.Carrier.ContainerId, mapped.Destination.ContainerId);
        Assert.Equal(occurrence.Carrier.ContainerPath, mapped.Destination.ContainerPath);
    }

    [Fact]
    public async Task ConflictingExplicitStorageSourceMappingsFailInsteadOfChoosingFirst()
    {
        await using var fixture = await TinyRouteFiles.CreateAsync();
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var storage = MortalLocationTestFixture.CreateStorageMetadata("storage", "Chest", false, new JsonArray());
        var first = new MortalLocationStorageBirthDraft("loc_b", "storage", "mltrn_one", "worldMapUpdates.storagesToAdd[0]", storage, 42,
            "worldMapUpdates.storagesToAdd[0].storage", "loc_b:storage");
        var conflicting = new MortalLocationStorageBirthDraft("loc_b", "storage", "mltrn_two", "worldMapUpdates.storagesToAdd[1]", storage, 42,
            "worldMapUpdates.storagesToAdd[1].storage", "another_source:storage");
        var result = await MortalItemRouteAuthorityCatalog.BuildAsync(fixture.Files, lease,
            suppliedCatalog: fixture.Catalog, storageBirthDrafts: new[] { first, conflicting });
        Assert.Contains(result.Issues, issue => issue.Code == "mortal_item_materialization_route_authority_conflict");
    }

    [Fact]
    public void ItemRepairAllowsExactRawItemFieldsButNotCanonicalMapOrWrapperIdentity()
    {
        const string prefix = "game_state/world/world_map.json";
        Assert.True(MortalItemRepairPacketBuilder.IsGmAuthorableTarget(prefix + ".worldMapUpdates.storagesToAdd[0].storage.contents[0].name"));
        foreach (var path in new[] { prefix, prefix + ".locations[0].name", prefix + ".locations[0].locationId",
                     prefix + ".worldMapUpdates.storagesToAdd[0].targetLocationId",
                     prefix + ".worldMapUpdates.storagesToAdd[0].storage.storageId",
                     prefix + ".worldMapUpdates.storagesToAdd[00].storage.contents[0].name",
                     prefix + ".worldMapUpdates.storagesToAdd[0].storage.contents[0].materializationReceipt.seal",
                     MortalLocationIdentityState.StatePath, MortalLocationStorageContentsState.StatePath })
            Assert.False(MortalItemRepairPacketBuilder.IsGmAuthorableTarget(path), path);
    }

    [Theory]
    [InlineData("reorder")]
    [InlineData("replacement")]
    [InlineData("deletion")]
    [InlineData("ref-only")]
    [InlineData("matId-only")]
    [InlineData("source-only")]
    public void StorageItemRepairRetryMustRetainOriginalItemIdentityAndDerivedSource(string change)
    {
        var input = MortalOffscreenStorageBirthTests.Input();
        var storage = input.RawWorldMapUpdates!["storagesToAdd"]![0]!["storage"]!.AsObject();
        storage["capacity"] = 2; storage["volume"] = 2d;
        var first = storage["contents"]![0]!.AsObject();
        first["materialization"]!["sourceAuthority"]!["authorityId"] = "wrong_source:storage";
        storage["contents"]!.AsArray().Add(MortalItemTestFixture.CreateRawRoot("storage_placement", "location_storage",
            "loc_offscreen_birth_b:storage_offscreen_birth", creationRef: "new_second", materializationId: "mat_second"));
        var planning = MortalLocationAcceptedTurnPlanner.Build(input);
        Assert.True(planning.Success, string.Join(";", planning.Issues.Select(i => i.Code)));
        var plan = planning.Plan!;
        var catalog = MortalItemCarrierCatalog.Build(new(null, null, null, plan.FinalCurrentLocation, null,
            new Dictionary<string, JsonObject>(), plan.FinalStorageContents));
        var occurrence = Assert.Single(catalog.ByCreationRef[MortalItemTestFixture.CreationRef]);
        var issue = new ValidationIssue(occurrence.JsonPath + ".materialization.sourceAuthority.authorityId", IssueSeverity.Error,
            "Repair this exact raw source field.", code: "mortal_item_materialization_route_authority_mismatch",
            actor: "mortal_item:new:" + MortalItemTestFixture.CreationRef, section: "MortalItemMaterialization",
            repairTargetFiles: new[] { MortalLocationStorageContentsState.StatePath })
        {
            MortalItemRepairContext = new("mortal_item:new:" + MortalItemTestFixture.CreationRef, "create", "storage_placement", null,
                occurrence.Carrier, "location_storage:loc_offscreen_birth_b:storage_offscreen_birth", "wrong source", Array.Empty<string>())
        };
        var issues = new List<ValidationIssue> { issue };
        ValidationService.RemapOffscreenStorageBirthDiagnostics(issues, plan, catalog);
        Assert.Equal("mortal_item:new:" + MortalItemTestFixture.CreationRef, Assert.Single(issues).MortalItemRepairContext!.Coordinate);
        var obligations = MortalLocationRepairRetryPolicy.Capture(MortalLocationRepairPacketBuilder.Build(issues));
        Assert.Single(obligations);
        var corrected = new JsonObject { ["worldMapUpdates"] = input.RawWorldMapUpdates.DeepClone() };
        var correctedItems = corrected["worldMapUpdates"]!["storagesToAdd"]![0]!["storage"]!["contents"]!.AsArray();
        correctedItems[0]!["materialization"]!["sourceAuthority"]!["authorityId"] = "loc_offscreen_birth_b:storage_offscreen_birth";
        // Correcting the reported bad source is allowed; the original erroneous value is not immutable authority.
        Assert.True(MortalLocationRepairRetryPolicy.HasExactResubmission(obligations, null, corrected));
        var changed = corrected.DeepClone().AsObject();
        var items = changed["worldMapUpdates"]!["storagesToAdd"]![0]!["storage"]!["contents"]!.AsArray();
        switch (change)
        {
            case "reorder": var one = items[0]!.DeepClone(); var two = items[1]!.DeepClone(); items[0] = two; items[1] = one; break;
            case "replacement": items[0]!["creationRef"] = "new_replacement"; items[0]!["materialization"]!["creationRef"] = "new_replacement";
                items[0]!["materialization"]!["materializationId"] = "mat_replacement"; break;
            case "deletion": items.RemoveAt(0); break;
            case "ref-only": items[0]!["creationRef"] = "new_replacement"; break;
            case "matId-only": items[0]!["materialization"]!["materializationId"] = "mat_replacement"; break;
            case "source-only": items[0]!["materialization"]!["sourceAuthority"]!["authorityId"] = "loc_other:storage"; break;
        }
        Assert.False(MortalLocationRepairRetryPolicy.HasExactResubmission(obligations, null, changed));
    }

    [Fact]
    public void EmptyAdditionCannotSilentlyDiscardNonemptyCargoMeasurements()
    {
        var input = MortalOffscreenStorageBirthTests.Input();
        var storage = input.RawWorldMapUpdates!["storagesToAdd"]![0]!["storage"]!.AsObject();
        storage["contents"] = new JsonArray();
        Assert.True(MortalLocationAcceptedTurnPlanner.Build(input).Success);
        storage["cargoMeasurements"] = new JsonArray(new JsonObject { ["creationRef"] = MortalItemTestFixture.CreationRef,
            ["count"] = 1, ["externalVolumeDm3"] = 1d, ["measuredConfiguration"] = new JsonObject
            { ["schemaVersion"] = 1, ["item"] = new JsonObject(), ["contents"] = new JsonArray() } });
        var result = MortalLocationAcceptedTurnPlanner.Build(input);
        Assert.False(result.Success);
        Assert.Contains(result.Issues, i => i.FilePath.EndsWith(".storage.cargoMeasurements", StringComparison.Ordinal));
    }

    [Fact]
    public void NewStorageSurfaceRejectsOtherwiseValidLootItemRoute()
    {
        var input = MortalOffscreenStorageBirthTests.Input();
        var item = MortalItemTestFixture.CreateRawRoot("loot_acquisition", "loot_template", "loot_template:42:0:test");
        Assert.Empty(MortalItemMaterializationContract.Validate(System.Text.Json.JsonSerializer.SerializeToElement(item),
            "fixture", MortalItemMaterializationPhase.RawPreSeal));
        input.RawWorldMapUpdates!["storagesToAdd"]![0]!["storage"]!["contents"] = new JsonArray(item);
        var result = MortalLocationAcceptedTurnPlanner.Build(input);
        Assert.False(result.Success);
        Assert.Contains(result.Issues, i => i.FilePath.Contains(".storage.contents[0].materialization.route", StringComparison.Ordinal));
    }

    private sealed class TinyRouteFiles : IAsyncDisposable
    {
        internal static readonly string[] CatalogOnlyPaths = { InventoryEquipmentService.ItemsPath,
            StorageTransportMoveService.CurrentLocationPath, MortalLocationStorageContentsState.StatePath, StorageTransportMoveService.VehiclesPath };
        internal static readonly string[] AuthorityPaths = { NpcCoreChangesContract.NpcCorePath, "game_state/npcs/npc_inventory.json",
            "game_state/quests/quest_history.json", "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
            "game_state/control/route_baseline/npc.json", "game_state/control/route_baseline/location.json",
            "game_state/control/route_baseline/craft.json", "game_state/control/route_baseline/trade.json" };
        private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-offscreen-route-data-" + Guid.NewGuid().ToString("N"));
        internal CanonicalReadActivityAccumulator Reads { get; } = new();
        internal FileSystemManager Files { get; }
        internal MortalItemCarrierCatalog Catalog { get; private set; } = null!;
        private TinyRouteFiles()
        {
            Files = new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks { PassiveCanonicalReads = Reads });
            Files.EnsureDirectoryStructure();
        }
        internal static async Task<TinyRouteFiles> CreateAsync()
        {
            var fixture = new TinyRouteFiles();
            try
            {
                var player = new JsonObject { ["items"] = new JsonArray(), ["equipment"] = new JsonObject(),
                    ["UpdateInventory"] = new JsonArray(MortalItemTestFixture.CreateRawRoot()) };
                var npc = new JsonObject { ["NPCsInScene"] = new JsonArray() };
                var commands = new JsonObject();
                var current = new JsonObject { ["locationId"] = "loc_a", ["locationStorages"] = new JsonArray() };
                var offscreen = MortalLocationStorageContentsState.CreateEmptyRoot();
                var vehicles = MortalVehicleBirthReceiptState.CreateEmptyRoot();
                var roots = new Dictionary<string, JsonNode>
                {
                    [InventoryEquipmentService.ItemsPath] = player, [NpcCoreChangesContract.NpcCorePath] = npc,
                    ["game_state/npcs/npc_inventory.json"] = commands, [StorageTransportMoveService.CurrentLocationPath] = current,
                    [MortalLocationStorageContentsState.StatePath] = offscreen, [StorageTransportMoveService.VehiclesPath] = vehicles,
                    ["game_state/quests/quest_history.json"] = new JsonObject(), ["input/turn_request.json"] = new JsonObject { ["turnNumber"] = 42 },
                    ["game_state/control/route_baseline/npc.json"] = npc.DeepClone(),
                    ["game_state/control/route_baseline/location.json"] = current.DeepClone(),
                    ["game_state/control/route_baseline/craft.json"] = new JsonObject(),
                    ["game_state/control/route_baseline/trade.json"] = new JsonObject(),
                    ["game_state/control/pending_turn_snapshot.json"] = new JsonObject { ["files"] = new JsonObject
                    {
                        [NpcCoreChangesContract.NpcCorePath] = "game_state/control/route_baseline/npc.json",
                        [StorageTransportMoveService.CurrentLocationPath] = "game_state/control/route_baseline/location.json",
                        [CraftRequestState.PendingRequestPath] = "game_state/control/route_baseline/craft.json",
                        [NpcTradeRequestState.PendingRequestPath] = "game_state/control/route_baseline/trade.json"
                    } }
                };
                foreach (var (path, root) in roots) await fixture.Files.WriteFileAtomicAsync(path, root.ToJsonString());
                fixture.Catalog = MortalItemCarrierCatalog.Build(new(player, npc, commands, current, vehicles,
                    new Dictionary<string, JsonObject>(), offscreen));
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }
        public ValueTask DisposeAsync()
        {
            if (!Path.GetFileName(_root).StartsWith("boe-offscreen-route-data-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing foreign fixture cleanup.");
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}
