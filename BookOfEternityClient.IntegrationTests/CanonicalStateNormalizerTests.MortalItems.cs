using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_RawCreationWithoutValidatedBindingFailsClosedWithoutMutation()
    {
        await SeedRawPlayerItemCreationAsync();
        var inventoryBefore = await _fs.ReadFileBytesAsync(
            InventoryEquipmentService.ItemsPath);
        var identityBefore = await _fs.ReadFileBytesAsync(
            MortalItemIdentityState.StatePath);
        var normalizer = new CanonicalStateNormalizer(
            _fs,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CanonicalStateNormalizer>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            normalizer.NormalizeAccumulatedStateAsync());

        Assert.Contains(
            "validated common-plan binding",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Equal(
            inventoryBefore,
            await _fs.ReadFileBytesAsync(InventoryEquipmentService.ItemsPath));
        Assert.Equal(
            identityBefore,
            await _fs.ReadFileBytesAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_RawCreationAndLocationCommand_FailsBeforeEveryWrite()
    {
        await SeedRawPlayerItemCreationAsync();
        var emptyMap = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locations"] = new JsonArray(),
            ["links"] = new JsonArray()
        };
        var emptyLocationIndex = MortalLocationIdentityState.CreateEmptyRoot();
        const string mapBackupPath = "test_backups/no_binding/world_map.json";
        const string indexBackupPath =
            "test_backups/no_binding/location_identity_index.json";
        await _fs.WriteFileAtomicAsync(mapBackupPath, emptyMap.ToJsonString());
        await _fs.WriteFileAtomicAsync(indexBackupPath, emptyLocationIndex.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            emptyMap.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationIdentityState.StatePath,
            emptyLocationIndex.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject
            {
                ["currentLocationData"] = MortalLocationTestFixture.CreateRawLocation(
                    "current_scene_creation")
            }.ToJsonString());
        var before = await CaptureNormalizerRollbackBytesAsync();
        var normalizer = new CanonicalStateNormalizer(
            _fs,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CanonicalStateNormalizer>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            normalizer.NormalizeAccumulatedStateAsync(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [MortalLocationMaterializationContract.WorldMapPath] = mapBackupPath,
                    [MortalLocationIdentityState.StatePath] = indexBackupPath
                }));

        Assert.Contains(
            "validated common-plan binding",
            exception.Message,
            StringComparison.Ordinal);
        await AssertNormalizerRollbackBytesAsync(before);
    }

    [Fact]
    public async Task NormalizeClientOwnedBootstrapAccumulatedStateAsync_PlayerCreation_SealsOneCanonicalItemAndIndexEntry()
    {
        await SeedRawPlayerItemCreationAsync();
        var normalizer = new CanonicalStateNormalizer(
            _fs,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CanonicalStateNormalizer>.Instance);

        await InvokeClientOwnedBootstrapNormalizationAsync(normalizer);

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

    [Theory]
    [InlineData("game_state/control/pending_turn_snapshot.json")]
    [InlineData("game_state/control/pending_turn_snapshot.authority.json")]
    [InlineData("game_state/resources/resource_commands.json")]
    [InlineData("game_state/effects/effect_commands.json")]
    public async Task NormalizeClientOwnedBootstrapAccumulatedStateAsync_RejectsAcceptedAuthoritySurface(
        string authorityPath)
    {
        await SeedRawPlayerItemCreationAsync();
        await _fs.WriteFileAtomicAsync(authorityPath, "{}");
        var inventoryBefore = await _fs.ReadFileBytesAsync(
            InventoryEquipmentService.ItemsPath);
        var identityBefore = await _fs.ReadFileBytesAsync(
            MortalItemIdentityState.StatePath);
        var normalizer = new CanonicalStateNormalizer(
            _fs,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CanonicalStateNormalizer>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeClientOwnedBootstrapNormalizationAsync(normalizer));

        Assert.Equal(
            inventoryBefore,
            await _fs.ReadFileBytesAsync(InventoryEquipmentService.ItemsPath));
        Assert.Equal(
            identityBefore,
            await _fs.ReadFileBytesAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    public async Task NormalizeClientOwnedBootstrapAccumulatedStateAsync_RejectsNonItemGmMaterializationSurfaceWithoutMutation()
    {
        await SeedRawPlayerItemCreationAsync();
        const string npcPath = "game_state/npcs/npc_core.json";
        await _fs.WriteFileAtomicAsync(
            npcPath,
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(new JsonObject
                {
                    ["initialId"] = "npc_ref_bootstrap_forbidden_materialization",
                    ["resourceMaterialization"] = new JsonObject
                    {
                        ["resources"] = new JsonArray(new JsonObject
                        {
                            ["resourceKey"] = "health",
                            ["maximum"] = 60
                        })
                    }
                })
            }.ToJsonString());
        var before = await CaptureNormalizerRollbackBytesAsync();
        var normalizer = new CanonicalStateNormalizer(
            _fs,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CanonicalStateNormalizer>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeClientOwnedBootstrapNormalizationAsync(normalizer));

        Assert.Contains(
            "GM-authored materialization surfaces",
            exception.Message,
            StringComparison.Ordinal);
        await AssertNormalizerRollbackBytesAsync(before);
    }

    [Fact]
    public async Task NormalizeClientOwnedBootstrapAccumulatedStateAsync_RejectsBareNonItemMaterializationEnvelopeWithoutMutation()
    {
        await SeedRawPlayerItemCreationAsync();
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject
            {
                ["currentLocationData"] = new JsonObject
                {
                    ["name"] = "Запретная bootstrap-локация",
                    [MortalLocationMaterializationContract.EnvelopeProperty] = new JsonObject
                    {
                        ["materializationId"] = "mat_location_bootstrap_forbidden"
                    }
                }
            }.ToJsonString());
        var before = await CaptureNormalizerRollbackBytesAsync();
        var normalizer = new CanonicalStateNormalizer(
            _fs,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CanonicalStateNormalizer>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeClientOwnedBootstrapNormalizationAsync(normalizer));

        Assert.Contains(
            "GM-authored materialization surfaces",
            exception.Message,
            StringComparison.Ordinal);
        await AssertNormalizerRollbackBytesAsync(before);
    }

    [Fact]
    public async Task NormalizeClientOwnedBootstrapAccumulatedStateAsync_RejectsForbiddenSurfaceNestedInsideAllowedItemEnvelopeWithoutMutation()
    {
        await SeedRawPlayerItemCreationAsync();
        var inventory = JsonNode.Parse(
            (await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath))!)!.AsObject();
        var rawItem = Assert.IsType<JsonObject>(
            Assert.Single(inventory["UpdateInventory"]!.AsArray()));
        rawItem[MortalItemMaterializationContract.EnvelopeProperty]!["nestedAuthority"] =
            new JsonObject
            {
                ["ownerMaterialization"] = new JsonObject
                {
                    ["ownerRef"] = "forbidden_nested_owner"
                }
            };
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            inventory.ToJsonString());
        var before = await CaptureNormalizerRollbackBytesAsync();
        var normalizer = new CanonicalStateNormalizer(
            _fs,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CanonicalStateNormalizer>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeClientOwnedBootstrapNormalizationAsync(normalizer));

        Assert.Contains(
            "GM-authored materialization surfaces",
            exception.Message,
            StringComparison.Ordinal);
        await AssertNormalizerRollbackBytesAsync(before);
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
        rawRoot["equippedItems"]!["MainHand"] = childCreationRef;
        await context.WriteJsonAsync(InventoryEquipmentService.ItemsPath, rawRoot);

        var rawIssues = await context.ValidateAcceptedTurnRawMaterializationAsync();
        Assert.DoesNotContain(
            rawIssues,
            issue => issue.Severity == IssueSeverity.Error);
        Assert.True(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));

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
            canonicalRoot["equippedItems"]!["MainHand"]!.GetValue<string>());
    }

    [Fact]
    public async Task Normalize_WriteFailure_RestoresEveryTrackedFileByteForByte()
    {
        var armed = false;
        var injected = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationBoundaryAsync = path =>
            {
                if (armed &&
                    !injected &&
                    string.Equals(
                        path,
                        MortalItemIdentityState.StatePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    injected = true;
                    throw new InvalidOperationException(
                        "Injected Mortal item identity publication failure.");
                }

                return Task.CompletedTask;
            }
        };
        await using var context = await MortalItemMaterializationTestContext.CreateAsync(hooks);
        await context.BuildMortalBootstrapAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WritePlayerUpdateAsync(MortalItemTestFixture.CreateRawRoot());
        var rawIssues = await context.ValidateAcceptedTurnRawMaterializationAsync();
        Assert.DoesNotContain(
            rawIssues,
            issue => issue.Severity == IssueSeverity.Error);
        Assert.True(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));
        var before = await context.CaptureExactBytesAsync(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        armed = true;

        var exception = await Assert.ThrowsAsync<CanonicalStateWriteException>(
            () => context.NormalizeAcceptedTurnAsync());
        Assert.True(injected);
        Assert.Equal(MortalItemIdentityState.StatePath, exception.RelativePath);
        Assert.IsType<InvalidOperationException>(exception.InnerException);

        await context.AssertExactBytesAsync(before);
    }

    [Fact]
    public async Task RawValidation_UnresolvedContainerReferenceFailsBeforeSealWithoutMutation()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.BuildMortalBootstrapAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        var rawItem = MortalItemTestFixture.CreateRawRoot();
        rawItem["contentsPath"] = new JsonArray("itm_missing_parent");
        await context.WritePlayerUpdateAsync(rawItem);
        var before = await context.CaptureExactBytesAsync(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);

        var issues = await context.ValidateAcceptedTurnRawMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            issue.Code == "mortal_item_materialization_orphan_companion");
        Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));
        await context.AssertExactBytesAsync(before);
    }

    private async Task SeedRawPlayerItemCreationAsync()
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
    }

    private async Task<IReadOnlyDictionary<string, byte[]?>>
        CaptureNormalizerRollbackBytesAsync()
    {
        var result = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in CanonicalStateNormalizer.NormalizerRollbackTrackedFiles)
            result[path] = await _fs.ReadFileBytesAsync(path);
        return result;
    }

    private async Task AssertNormalizerRollbackBytesAsync(
        IReadOnlyDictionary<string, byte[]?> expected)
    {
        foreach (var (path, expectedBytes) in expected)
        {
            var actualBytes = await _fs.ReadFileBytesAsync(path);
            if (expectedBytes == null)
            {
                Assert.Null(actualBytes);
                continue;
            }

            Assert.NotNull(actualBytes);
            Assert.True(
                expectedBytes.AsSpan().SequenceEqual(actualBytes),
                $"Canonical normalization changed exact bytes for '{path}'.");
        }
    }

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
