using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceItemOwnerTests
{
    private const string SnapshotManifestPath =
        "game_state/control/pending_turn_snapshot.json";

    [Fact]
    public async Task AcceptedTurn_NewItemUsesOnePermanentOwnerForItemSealAndResourceInitialization()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.BuildMortalBootstrapAsync();
        await context.SeedPristineResourceQuartetAsync();
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        var rawItem = ResourceItem(
            MortalItemTestFixture.CreateRawRoot(),
            ("durability", 100m),
            ("charges", 3m),
            ("ammunition", 6m));
        await context.WritePlayerUpdateAsync(rawItem);

        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/control/pending_turn_snapshot.json"));
        var snapshotToken = manifest["manifestPayloadHash"]!.GetValue<string>();
        Assert.NotNull(await AcceptedMechanicsAuthorityTestProbe.GetAllocatedItemIdAsync(
            context.FileSystem,
            "session_mortal_item_materialization",
            snapshotToken,
            MortalItemTestFixture.CreationRef));
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.GetAllocatedItemIdAsync(
            context.FileSystem,
            "session_mortal_item_materialization",
            "request_mortal_item_materialization",
            MortalItemTestFixture.CreationRef));
        var resourceIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(resourceIssues, issue => issue.Severity == IssueSeverity.Error);
        var validatedHandoff = await AcceptedMechanicsAuthorityTestProbe
            .PeekCommonAsync(context.FileSystem);
        Assert.NotNull(validatedHandoff);
        var planning = validatedHandoff.Result;
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
    public async Task RawItemValidation_RegistersOnlyTheManifestTupleUsedForValidation()
    {
        FileSystemManager? fileSystem = null;
        byte[]? replacementManifestBytes = null;
        byte[]? replacementAuthorityBytes = null;
        var armed = false;
        var manifestReads = 0;
        var swapped = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (!armed ||
                    !string.Equals(path, SnapshotManifestPath, StringComparison.OrdinalIgnoreCase) ||
                    Interlocked.Increment(ref manifestReads) != 4)
                {
                    return Task.CompletedTask;
                }

                File.WriteAllBytes(
                    fileSystem!.ResolvePath(SnapshotManifestPath),
                    replacementManifestBytes!);
                File.WriteAllBytes(
                    fileSystem.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath),
                    replacementAuthorityBytes!);
                swapped = true;
                return Task.CompletedTask;
            }
        };
        await using var context = await MortalItemMaterializationTestContext.CreateAsync(hooks);
        fileSystem = context.FileSystem;
        await context.BuildMortalBootstrapAsync();
        await context.SeedPristineResourceQuartetAsync();
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        var manifestA = Assert.IsType<JsonObject>(
            await context.ReadJsonAsync(SnapshotManifestPath));
        var tokenA = manifestA["manifestPayloadHash"]!.GetValue<string>();
        var manifestABytes = (await fileSystem.ReadFileBytesAsync(SnapshotManifestPath))!;
        var authorityABytes = (await fileSystem.ReadFileBytesAsync(
            PendingTurnSnapshotAuthority.AuthorityPath))!;

        var manifestB = manifestA.DeepClone().AsObject();
        manifestB["sourceLabel"] = "Mortal item swapped snapshot B";
        manifestB["manifestPayloadHash"] = string.Empty;
        manifestB["manifestPayloadHash"] =
            PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifestB);
        var tokenB = manifestB["manifestPayloadHash"]!.GetValue<string>();
        await context.WriteJsonAsync(SnapshotManifestPath, manifestB);
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(fileSystem);
        replacementManifestBytes = (await fileSystem.ReadFileBytesAsync(SnapshotManifestPath))!;
        replacementAuthorityBytes = (await fileSystem.ReadFileBytesAsync(
            PendingTurnSnapshotAuthority.AuthorityPath))!;
        await fileSystem.WriteFileAtomicBytesAsync(SnapshotManifestPath, manifestABytes);
        await fileSystem.WriteFileAtomicBytesAsync(
            PendingTurnSnapshotAuthority.AuthorityPath,
            authorityABytes);

        var rawItem = ResourceItem(
            MortalItemTestFixture.CreateRawRoot(),
            ("durability", 100m));
        await context.WritePlayerUpdateAsync(rawItem);
        armed = true;

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.NotNull(await AcceptedMechanicsAuthorityTestProbe.GetAllocatedItemIdAsync(
            fileSystem,
            "session_mortal_item_materialization",
            tokenA,
            MortalItemTestFixture.CreationRef));
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.GetAllocatedItemIdAsync(
            fileSystem,
            "session_mortal_item_materialization",
            tokenB,
            MortalItemTestFixture.CreationRef));
        Assert.False(swapped);
        Assert.Equal(3, manifestReads);
    }

    [Theory]
    [InlineData("cache_miss")]
    [InlineData("cache_binding_mismatch")]
    public async Task AcceptedTurnRawItemAuthorityLoss_FailsClosedAndRollsBack(
        string authorityLoss)
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.BuildMortalBootstrapAsync();
        await context.SeedPristineResourceQuartetAsync();
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        await context.WritePlayerUpdateAsync(ResourceItem(
            MortalItemTestFixture.CreateRawRoot(),
            ("durability", 100m)));
        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        var resourceIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(resourceIssues, issue => issue.Severity == IssueSeverity.Error);

        if (authorityLoss == "cache_miss")
        {
            await AcceptedMechanicsAuthorityTestProbe.InvalidateItemsAsync(
                context.FileSystem);
        }
        else if (authorityLoss == "cache_binding_mismatch")
        {
            var playerRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
                InventoryEquipmentService.ItemsPath));
            var catalog = MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
                playerRoot,
                NpcCore: null,
                NpcInventoryCommands: null,
                CurrentLocation: null,
                Vehicles: null,
                CompanionRoots: new Dictionary<string, JsonObject>(StringComparer.Ordinal),
                OffscreenLocationStorageContents: null));
            Assert.Empty(catalog.Issues);
            await AcceptedMechanicsAuthorityTestProbe.RegisterItemsAsync(
                context.FileSystem,
                "session_mortal_item_materialization",
                "snapshot_mismatch_token",
                catalog,
                Array.Empty<string>());
        }
        var before = await context.CaptureExactBytesAsync(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            context.NormalizeAcceptedTurnWithIssuesAsync);

        Assert.Contains("Mortal item accepted-turn authority", exception.Message,
            StringComparison.Ordinal);
        await context.AssertExactBytesAsync(before);
    }

    [Theory]
    [InlineData("cache_miss")]
    [InlineData("cache_binding_mismatch")]
    public async Task AcceptedTurnRawItemAuthorityLoss_FailsBeforeEarlierTransferPublication(
        string authorityLoss)
    {
        const string creationRef = "new_item_after_valid_transfer";
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.SeedPristineResourceQuartetAsync();
        await context.ArrangeNpcToPlayerTransferAsync();
        var inventory = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            InventoryEquipmentService.ItemsPath));
        var updates = Assert.IsType<JsonArray>(inventory["UpdateInventory"]);
        updates.Add(ResourceItem(
            MortalItemTestFixture.CreateRawRoot(
                authorityId: "turn_43",
                sourceTurn: MortalItemMaterializationTestContext.TransferTurn,
                creationRef: creationRef,
                materializationId: "mat_item_after_valid_transfer"),
            ("durability", 100m)));
        await context.WriteJsonAsync(InventoryEquipmentService.ItemsPath, inventory);

        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        var resourceIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(resourceIssues, issue => issue.Severity == IssueSeverity.Error);
        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            SnapshotManifestPath));
        var backups = manifest["files"]!.AsObject().ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value!.GetValue<string>(),
            StringComparer.OrdinalIgnoreCase);

        if (authorityLoss == "cache_miss")
        {
            await AcceptedMechanicsAuthorityTestProbe.InvalidateItemsAsync(
                context.FileSystem);
        }
        else
        {
            var playerRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
                InventoryEquipmentService.ItemsPath));
            var catalog = MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
                playerRoot,
                NpcCore: null,
                NpcInventoryCommands: null,
                CurrentLocation: null,
                Vehicles: null,
                CompanionRoots: new Dictionary<string, JsonObject>(StringComparer.Ordinal),
                OffscreenLocationStorageContents: null));
            Assert.Empty(catalog.Issues);
            await AcceptedMechanicsAuthorityTestProbe.RegisterItemsAsync(
                context.FileSystem,
                "session_mortal_item_materialization",
                "snapshot_mismatch_token",
                catalog,
                Array.Empty<string>());
        }

        var before = await context.CaptureExactBytesAsync(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => context.Normalizer
            .BindTo(writeLease)
            .NormalizeAccumulatedStateWithPlanAsync(backups));

        Assert.Contains(
            "Mortal item accepted-turn authority",
            exception.Message,
            StringComparison.Ordinal);
        await context.AssertExactBytesAsync(before);
    }

    /// <summary>
    /// Rejects replacement of the validated snapshot during item publication and restores every tracked before-image.
    /// </summary>
    /// <returns>
    /// A task that completes after the exact authority rejection and rollback checks.
    /// </returns>
    [Fact]
    public async Task AcceptedTurnRawItemManifestSwapAfterPreflight_FailsClosedAndRollsBack()
    {
        FileSystemManager? fileSystem = null;
        byte[]? replacementManifestBytes = null;
        byte[]? replacementAuthorityBytes = null;
        var armed = false;
        var manifestReads = 0;
        var swapped = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (!armed ||
                    !string.Equals(path, SnapshotManifestPath, StringComparison.OrdinalIgnoreCase) ||
                    Interlocked.Increment(ref manifestReads) != 2)
                {
                    return Task.CompletedTask;
                }

                File.WriteAllBytes(
                    fileSystem!.ResolvePath(SnapshotManifestPath),
                    replacementManifestBytes!);
                File.WriteAllBytes(
                    fileSystem.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath),
                    replacementAuthorityBytes!);
                swapped = true;
                return Task.CompletedTask;
            }
        };
        await using var context = await MortalItemMaterializationTestContext.CreateAsync(hooks);
        fileSystem = context.FileSystem;
        await context.BuildMortalBootstrapAsync();
        await context.SeedPristineResourceQuartetAsync();
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        await context.WritePlayerUpdateAsync(ResourceItem(
            MortalItemTestFixture.CreateRawRoot(),
            ("durability", 100m)));
        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        var resourceIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(resourceIssues, issue => issue.Severity == IssueSeverity.Error);

        var manifestA = Assert.IsType<JsonObject>(
            await context.ReadJsonAsync(SnapshotManifestPath));
        var manifestABytes = (await fileSystem.ReadFileBytesAsync(SnapshotManifestPath))!;
        var authorityABytes = (await fileSystem.ReadFileBytesAsync(
            PendingTurnSnapshotAuthority.AuthorityPath))!;
        var backups = manifestA["files"]!.AsObject().ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value!.GetValue<string>(),
            StringComparer.OrdinalIgnoreCase);
        var manifestB = manifestA.DeepClone().AsObject();
        manifestB["sourceLabel"] = "Mortal item normalization swapped snapshot B";
        manifestB["manifestPayloadHash"] = string.Empty;
        manifestB["manifestPayloadHash"] =
            PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifestB);
        await context.WriteJsonAsync(SnapshotManifestPath, manifestB);
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(fileSystem);
        replacementManifestBytes = (await fileSystem.ReadFileBytesAsync(SnapshotManifestPath))!;
        replacementAuthorityBytes = (await fileSystem.ReadFileBytesAsync(
            PendingTurnSnapshotAuthority.AuthorityPath))!;
        await fileSystem.WriteFileAtomicBytesAsync(SnapshotManifestPath, manifestABytes);
        await fileSystem.WriteFileAtomicBytesAsync(
            PendingTurnSnapshotAuthority.AuthorityPath,
            authorityABytes);
        var before = await context.CaptureExactBytesAsync(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        armed = true;

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
                fileSystem,
                context.Normalizer,
                context.Validator,
                backups));

        Assert.True(swapped);
        Assert.Equal(
            $"Accepted mechanics authority at '{SnapshotManifestPath}' changed after validation.",
            exception.Message);
        await context.AssertExactBytesAsync(before);
    }

    [Fact]
    public async Task ClientOwnedBootstrapNormalization_RejectsValidatedAcceptedPlan()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.BuildMortalBootstrapAsync();
        await context.SeedPristineResourceQuartetAsync();
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        await context.WritePlayerUpdateAsync(ResourceItem(
            MortalItemTestFixture.CreateRawRoot(),
            ("durability", 100m)));
        var issues = await context.ValidateAcceptedTurnRawMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));
        var before = await context.CaptureExactBytesAsync(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeClientOwnedBootstrapNormalizationAsync(context.Normalizer));

        await context.AssertExactBytesAsync(before);
        Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));
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

        await AcceptedMechanicsAuthorityTestProbe.RegisterItemsAsync(
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
            var itemId = await AcceptedMechanicsAuthorityTestProbe.GetAllocatedItemIdAsync(
                context.FileSystem,
                "session_item_authority",
                "request_item_authority",
                creationRef);
            Assert.NotNull(itemId);
            Assert.StartsWith("itm_", itemId, StringComparison.Ordinal);
            Assert.True(allocatedIds.Add(itemId!));
        }

        var effectSource = Assert.Single(
            await AcceptedMechanicsAuthorityTestProbe.GetItemEffectSourcesAsync(
                context.FileSystem,
                "session_item_authority",
                "request_item_authority"));
        Assert.Equal(Ref("player"), effectSource.SourceRef);
        var playerId = await AcceptedMechanicsAuthorityTestProbe.GetAllocatedItemIdAsync(
            context.FileSystem,
            "session_item_authority",
            "request_item_authority",
            Ref("player"));
        Assert.NotNull(playerId);
        Assert.Equal(allocatedIds.Single(id =>
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

    private static async Task InvokeClientOwnedBootstrapNormalizationAsync(
        CanonicalStateNormalizer normalizer)
    {
        var method = typeof(CanonicalStateNormalizer).GetMethod(
            "NormalizeClientOwnedBootstrapAccumulatedStateAsync",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        try
        {
            var invocation = method!.Invoke(normalizer, new object?[] { null });
            await Assert.IsAssignableFrom<Task>(invocation);
        }
        catch (System.Reflection.TargetInvocationException exception)
            when (exception.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(exception.InnerException)
                .Throw();
            throw;
        }
    }
}
