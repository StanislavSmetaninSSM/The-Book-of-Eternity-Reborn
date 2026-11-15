using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using BookOfEternityClient.Core;
using Xunit.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalItemMaterializationValidationTests
{
    [Trait("Category", "FullValidation")]
    public sealed class StorageBirthCapacity(ITestOutputHelper output)
    {
        [Fact]
        public Task StorageBirthCapacity_ZeroSlotsRejectsWithoutMutation() =>
            AssertCapacityRefusedAsync(capacity: 0, volume: 1, field: "capacity");

        [Fact]
        public Task StorageBirthCapacity_InsufficientVolumeRejectsWithoutMutation() =>
            AssertCapacityRefusedAsync(capacity: 1, volume: 0.5, field: "volume");

        [Fact]
        public async Task StorageBirthCapacity_ExactSlotAndVolumePublishesPermanentCarrier()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await ArrangeCapacityAsync(context, capacity: 1, volume: 1);
            var locationIssues = await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync();
            Assert.DoesNotContain(locationIssues, issue => issue.Severity == IssueSeverity.Error);

            await context.NormalizeAcceptedTurnWithValidatedCommonPlanAsync();

            var current = (await context.ReadJsonAsync(StorageTransportMoveService.CurrentLocationPath))!.AsObject();
            var locationId = current["locationId"]!.GetValue<string>();
            Assert.StartsWith("loc_", locationId, StringComparison.Ordinal);
            Assert.NotEqual(MortalLocationTestFixture.LocationInitialId, locationId);
            var storage = Assert.Single(current["locationStorages"]!.AsArray().OfType<JsonObject>());
            Assert.Equal(1, storage["capacity"]!.GetValue<int>());
            Assert.Equal(1d, storage["volume"]!.GetValue<double>());
            var item = Assert.Single(storage["contents"]!.AsArray().OfType<JsonObject>());
            var itemId = item["itemId"]!.GetValue<string>();
            Assert.NotEmpty(itemId);
            Assert.NotNull(item[MortalItemMaterializationContract.ReceiptProperty]);
            var index = (await context.ReadJsonAsync(MortalItemIdentityState.StatePath))!.AsObject();
            var entry = Assert.Single(index["entries"]!.AsArray().OfType<JsonObject>());
            Assert.Equal(itemId, entry["itemId"]!.GetValue<string>());
            Assert.Equal("location_storage", entry["currentCarrier"]!["kind"]!.GetValue<string>());
            Assert.Equal(locationId, entry["currentCarrier"]!["ownerId"]!.GetValue<string>());
            Assert.Equal(MortalItemMaterializationTestContext.SameTurnRouteStorageId,
                entry["currentCarrier"]!["containerId"]!.GetValue<string>());
            await AssertBirthHistoryAsync(context, locationId, itemId);
        }

        [Fact]
        public async Task StorageBirthCapacity_EmptyBirthFinalizesWithoutItemCreations()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await ArrangeCapacityAsync(context, 0, 0);
            var raw = (await context.ReadJsonAsync(StorageTransportMoveService.CurrentLocationPath))!.AsObject();
            raw["currentLocationData"]!["locationStorages"]![0]!["contents"] = new JsonArray();
            await context.WriteJsonAsync(StorageTransportMoveService.CurrentLocationPath, raw);
            await context.NormalizeAcceptedTurnWithValidatedCommonPlanAsync();
            var current = (await context.ReadJsonAsync(StorageTransportMoveService.CurrentLocationPath))!.AsObject();
            var evidence = await AssertBirthHistoryAsync(context, current["locationId"]!.GetValue<string>(), null);
            Assert.Empty(evidence["cargoMeasurements"]!.AsArray());
            Assert.Empty((await context.ReadJsonAsync(MortalItemIdentityState.StatePath))!["entries"]!.AsArray());
        }

        [Fact]
        public async Task StorageBirthCapacity_ExistingMoveConsumerPreservesDetachedHistory()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await ArrangeCapacityAsync(context, 1, 1);
            await context.NormalizeAcceptedTurnWithValidatedCommonPlanAsync();
            var history = await context.FileSystem.ReadFileBytesAsync(MortalLocationIdentityState.StatePath);
            var options = await StorageTransportMoveService.ReadStorageMoveContextAsync(context.FileSystem);
            Assert.True(options.Success, options.Message);
            var storage = Assert.Single(options.Storages);
            var item = Assert.Single(storage.Contents);
            var moved = await StorageTransportMoveService.MoveStorageItemAsync(context.FileSystem,
                StorageTransportMoveService.DirectionRetrieve, storage.Key, item.Key);
            Assert.True(moved.Success, moved.Message);
            Assert.Equal(history, await context.FileSystem.ReadFileBytesAsync(MortalLocationIdentityState.StatePath));
            Assert.Empty(MortalLocationIdentityState.Parse(await context.FileSystem.ReadFileAsync(MortalLocationIdentityState.StatePath)).Issues);
            var fresh = await StorageTransportMoveService.ReadStorageMoveContextAsync(context.FileSystem);
            var carried = Assert.Single(fresh.InventoryItems, candidate => candidate.Identity == item.Identity);
            var returned = await StorageTransportMoveService.MoveStorageItemAsync(context.FileSystem,
                StorageTransportMoveService.DirectionDeposit, storage.Key, carried.Key);
            Assert.True(returned.Success, returned.Message);
            Assert.Equal(history, await context.FileSystem.ReadFileBytesAsync(MortalLocationIdentityState.StatePath));
        }

        [Fact]
        public async Task StorageBirthCapacity_ReachedHistoryWriteCutRestoresAllTrackedPaths()
        {
            KnownRollbackPublicationCut? cut = null;
            await using var context = await MortalItemMaterializationTestContext.CreateAsync(new FileSystemManagerHooks
            { LocalPublicationObserver = (phase, index) => cut?.Hooks.LocalPublicationObserver?.Invoke(phase, index) });
            await ArrangeCapacityAsync(context, 1, 1);
            var rawIssues = await context.ValidateAcceptedTurnRawMaterializationAsync();
            Assert.DoesNotContain(rawIssues, i => i.Severity == IssueSeverity.Error);
            var before = await context.CaptureExactBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
            var generation = CleanupPublicationCut.ReadOptional(context.FileSystem.SessionGenerationPath);
            cut = new KnownRollbackPublicationCut(context.FileSystem.ResolvePath(MortalLocationIdentityState.StatePath));
            cut.Attach(context.FileSystem);
            var failure = await Record.ExceptionAsync(context.NormalizeAcceptedTurnAsync);
            cut.AssertRestored(before, generation, failure, output.WriteLine);
            var writeFailure = Assert.IsType<CanonicalStateWriteException>(failure);
            Assert.Equal(MortalLocationIdentityState.StatePath, writeFailure.RelativePath);
            Assert.Same(cut.Failure, writeFailure.InnerException);
        }

        [Fact]
        public async Task StorageBirthCapacity_StorageOnlySourceDriftFailsBeforePublication()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await ArrangeCapacityAsync(context, 1, 1);
            Assert.DoesNotContain(await context.ValidateAcceptedTurnRawMaterializationAsync(), i => i.Severity == IssueSeverity.Error);
            var map = (await context.ReadJsonAsync(MortalLocationMaterializationContract.WorldMapPath))!.AsObject();
            map["locations"]![0]!["name"] = "Changed after storage admission";
            await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, map);
            var before = await context.CaptureExactBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
            await Assert.ThrowsAnyAsync<InvalidDataException>(() => context.NormalizeAcceptedTurnWithIssuesAsync());
            await context.AssertExactBytesAsync(before);
        }

        private static async Task<JsonObject> AssertBirthHistoryAsync(MortalItemMaterializationTestContext context,
            string locationId, string? itemId)
        {
            var index = (await context.ReadJsonAsync(MortalLocationIdentityState.StatePath))!.AsObject();
            Assert.Empty(MortalLocationIdentityState.Parse(index).Issues);
            var entry = Assert.Single(index["locationEntries"]!.AsArray().OfType<JsonObject>(), e => e["locationId"]!.GetValue<string>() == locationId);
            var transition = Assert.Single(entry["transitions"]!.AsArray().OfType<JsonObject>(), t => t["kind"]!.GetValue<string>() == "storage_addition");
            Assert.Equal("currentLocationData.locationStorages[0]", transition["operationRef"]!.GetValue<string>());
            var after = transition["afterState"]!.AsObject();
            Assert.Null(after["contents"]); Assert.Null(after["cargoMeasurements"]);
            var evidence = after["cargoBirthEvidence"]!.AsObject();
            Assert.Equal(locationId, evidence["locationId"]!.GetValue<string>());
            Assert.Equal(MortalItemMaterializationTestContext.SameTurnRouteStorageId, evidence["storageId"]!.GetValue<string>());
            var manifest = (await context.ReadJsonAsync("game_state/control/pending_turn_snapshot.json"))!.AsObject();
            Assert.Equal(manifest["sessionId"]!.GetValue<string>(), evidence["sourceAuthority"]!["sessionId"]!.GetValue<string>());
            Assert.Equal(manifest["requestId"]!.GetValue<string>(), evidence["sourceAuthority"]!["requestId"]!.GetValue<string>());
            Assert.False(string.IsNullOrWhiteSpace(evidence["sourceAuthority"]!["snapshotToken"]!.GetValue<string>()));
            if (itemId is not null) Assert.Equal(itemId, Assert.Single(evidence["cargoMeasurements"]!.AsArray())!["itemId"]!.GetValue<string>());
            return evidence;
        }

        private static async Task AssertCapacityRefusedAsync(int capacity, double volume, string field)
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await ArrangeCapacityAsync(context, capacity, volume);
            var before = await context.CaptureExactBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);

            var issues = new List<ValidationIssue>();
            issues.AddRange(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
            issues.AddRange(await context.ValidateAcceptedTurnRawMaterializationAsync());

            await context.AssertExactBytesAsync(before);
            // Causal failure must identify the violated storage limit, not an unrelated fixture error.
            Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error &&
                issue.FilePath.Contains("locationStorages", StringComparison.Ordinal) &&
                issue.FilePath.EndsWith("." + field, StringComparison.Ordinal));
        }

        private static async Task ArrangeCapacityAsync(
            MortalItemMaterializationTestContext context, int capacity, double volume)
        {
            var item = MortalItemTestFixture.CreateRawRoot("storage_placement", "location_storage");
            item["volume"] = 1d;
            await context.ArrangeSameTurnCurrentLocationStorageRouteAsync(item);
            var raw = (await context.ReadJsonAsync(StorageTransportMoveService.CurrentLocationPath))!.AsObject();
            var storage = raw["currentLocationData"]!["locationStorages"]![0]!.AsObject();
            storage["capacity"] = capacity;
            storage["volume"] = volume;
            await context.WriteJsonAsync(StorageTransportMoveService.CurrentLocationPath, raw);
        }
    }
}
