using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalItemMaterializationValidationTests
{
    [Trait("Category", "FullValidation")]
    public sealed class StorageBirthCapacity
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
