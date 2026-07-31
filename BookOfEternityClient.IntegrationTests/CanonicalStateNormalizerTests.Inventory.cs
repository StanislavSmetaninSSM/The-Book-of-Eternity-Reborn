using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_StripsPlayerFacingItemJournalTurnAnchors()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "items": [
            {
              "itemId": "letter_black_wax",
              "name": "Письмо под черным воском",
              "journalEntries": [
                "#[4]. Письмо с неизвестной печатью найдено на столе в спальне наследницы.",
                "#[4] - Фамильная руническая перчатка Дома Вирент теплеет рядом с письмом."
              ]
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/npcs/item_journals.json", """
        {
          "entries": [
            {
              "itemId": "letter_black_wax",
              "itemName": "Письмо под черным воском",
              "journalEntries": [
                "#[4]. На полях письма проступил след старой руны."
              ]
            }
          ]
        }
        """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var itemsRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/inventory/items.json"))!)!.AsObject();
        var item = Assert.Single(itemsRoot["items"]!.AsArray().OfType<JsonObject>());
        var entries = item["journalEntries"]!.AsArray();
        Assert.Equal("Письмо с неизвестной печатью найдено на столе в спальне наследницы.", entries[0]!.GetValue<string>());
        Assert.Equal("Фамильная руническая перчатка Дома Вирент теплеет рядом с письмом.", entries[1]!.GetValue<string>());

        var journalRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/npcs/item_journals.json"))!)!.AsObject();
        var sidecar = Assert.Single(journalRoot["entries"]!.AsArray().OfType<JsonObject>());
        Assert.Equal("На полях письма проступил след старой руны.", sidecar["journalEntries"]!.AsArray()[0]!.GetValue<string>());

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "item_journal_entry_turn_anchor_player_facing", StringComparison.OrdinalIgnoreCase));
    }
}
