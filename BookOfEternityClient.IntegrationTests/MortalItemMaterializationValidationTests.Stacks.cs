using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalItemMaterializationValidationTests
{
    [Trait("Category", "FullValidation")]
    public sealed class Stacks
    {
        [Fact]
        public async Task CanonicalActiveCount_MustEqualLastIndexedQuantity()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.ArrangeEmptyMortalTurnAsync();
            var item = MortalItemTestFixture.CreateCanonicalRoot();
            var index = MortalItemTestFixture.CreateIndex(item);
            item["count"] = 2;
            await context.WriteCanonicalPlayerItemAsync(item, index);

            var issues = await context.Validator
                .ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync();

            var mismatch = Assert.Single(issues, issue =>
                issue.Code == "mortal_item_materialization_index_quantity_mismatch");
            Assert.Equal($"mortal_item:existing:{MortalItemTestFixture.ItemId}", mismatch.Actor);
            Assert.Equal(
                new[] { InventoryEquipmentService.ItemsPath },
                mismatch.RepairTargetFiles);
        }
    }
}
