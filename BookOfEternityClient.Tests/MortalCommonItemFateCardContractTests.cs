using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalCommonItemFateCardContractTests
{
    [Theory]
    [InlineData("[]", true)]
    [InlineData("[{}]", false)]
    [InlineData("{}", false)]
    public void CommonItem_ExplicitEmptyFateCardsIsNoCapabilityButNonemptyOrMalformedIsRejected(
        string cards, bool valid)
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-common-item-fate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
            using var document = JsonDocument.Parse("{\"quality\":\"Common\",\"fateCards\":" + cards + "}");
            var issues = new List<ValidationIssue>();

            validator.ValidateItemBondAndFateCardContract(document.RootElement, "npc.inventory[0]", issues, "Common");

            if (valid)
                Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
            else
                Assert.Contains(issues, issue => issue.Code == "item_fate_cards_forbidden_for_non_rare_quality");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
