using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private async Task ValidateReadableInventoryDocumentAuthorityAsync(List<ValidationIssue> issues)
    {
        var inventoryRoot = await ReadJsonNodeAsync("game_state/inventory/items.json");
        if (inventoryRoot == null)
            return;

        var documents = ReadableInventoryDocumentAuthority.ResolveDocumentsForValidation(
            inventoryRoot,
            await ReadJsonNodeAsync("game_state/inventory/item_text_updates.json"),
            await ReadJsonNodeAsync("game_state/npcs/item_journals.json"));

        foreach (var document in documents)
        {
            if (document.HasReadableAuthority || document.HasUnreadableReason)
                continue;

            issues.Add(new ValidationIssue(
                $"game_state/inventory/items.json.item:{document.ContextIdentity}",
                IssueSeverity.Error,
                "Документоподобный предмет в инвентаре должен иметь readable text или явную причину, почему его нельзя прочесть.",
                code: ReadableInventoryDocumentAuthority.MissingDetailAuthorityCode,
                section: "Inventory",
                expected: "textContent, item_text_updates/item_journals entry, unreadableReason, sealedReason или lockedReason",
                actual: $"«{document.Name}» не имеет readable detail authority",
                repairHint: "Если предмет можно читать, добавь textContent или matching item text/journal entry по stable itemId/existedId. Если он запечатан, закрыт или неизвестен, добавь player-facing unreadableReason/sealedReason/lockedReason."));
        }
    }

    private async Task<JsonNode?> ReadJsonNodeAsync(string path)
    {
        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
