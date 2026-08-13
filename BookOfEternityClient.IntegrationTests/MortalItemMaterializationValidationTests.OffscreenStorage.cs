using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalItemMaterializationValidationTests
{
    [Trait("Category", "FullValidation")]
    public sealed class OffscreenStorage
    {
        [Fact]
        public async Task StorageItem_CanonicalOffscreenOccurrenceMatchesIndexAndSnapshot()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.BuildMortalBootstrapAsync();
            var item = MortalItemTestFixture.CreateCanonicalRoot("itm_offscreen_canonical");
            await context.WriteJsonAsync(
                MortalLocationStorageContentsState.StatePath,
                Offscreen(
                    "loc_remote_storage",
                    "storage_chest",
                    item));
            await context.WriteJsonAsync(
                MortalItemIdentityState.StatePath,
                MortalItemTestFixture.CreateIndexForCarrier(
                    item,
                    "location_storage",
                    "loc_remote_storage",
                    "storage_chest"));
            await context.CaptureValidatedPendingSnapshotAsync();

            var issues = await context.Validator
                .ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync();

            Assert.DoesNotContain(issues, issue =>
                issue.Severity == IssueSeverity.Error);
        }

        [Theory]
        [InlineData("add")]
        [InlineData("remove")]
        [InlineData("change")]
        public async Task StorageItem_RawMutationOfClientOwnedOffscreenStateFailsClosed(
            string scenario)
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.BuildMortalBootstrapAsync();
            if (scenario is "remove" or "change")
            {
                await context.WriteJsonAsync(
                    MortalLocationStorageContentsState.StatePath,
                    MortalLocationStorageContentsState.CreateEmptyRoot());
            }
            await context.CaptureValidatedPendingSnapshotAsync();

            if (scenario == "remove")
            {
                context.FileSystem.DeleteFile(MortalLocationStorageContentsState.StatePath);
            }
            else
            {
                await context.WriteJsonAsync(
                    MortalLocationStorageContentsState.StatePath,
                    Offscreen(
                        "loc_remote_storage",
                        "storage_chest",
                        MortalItemTestFixture.CreateCanonicalRoot(
                            "itm_gm_authored_offscreen")));
            }

            var issues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Code == "mortal_location_storage_contents_gm_authored_client_field" &&
                issue.FilePath == MortalLocationStorageContentsState.StatePath);
        }

        private static JsonObject Offscreen(
            string locationId,
            string storageId,
            JsonObject item) =>
            MortalLocationStorageContentsState.BuildCanonicalRoot(
                new Dictionary<MortalLocationStorageKey, JsonArray>
                {
                    [new MortalLocationStorageKey(locationId, storageId)] =
                        new JsonArray(item.DeepClone())
                });
    }
}
