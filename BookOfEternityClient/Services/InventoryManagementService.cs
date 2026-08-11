using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public static class InventoryManagementService
{
    public static async Task<InventoryManagementContext?> ReadContextAsync(FileSystemManager fs) =>
        await ReadContextCoreAsync(fs, writeLease: null);

    internal static async Task<InventoryManagementContext?> ReadContextAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        await ReadContextCoreAsync(fs, writeLease);

    private static async Task<InventoryManagementContext?> ReadContextCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease)
    {
        var raw = writeLease == null
            ? await fs.ReadFileAsync(InventoryEquipmentService.ItemsPath)
            : await fs.ReadFileAsync(writeLease, InventoryEquipmentService.ItemsPath);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(raw) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }

        if (root == null)
            return null;

        var itemsArray = GetPlayerInventoryArrayNode(root);
        if (itemsArray == null)
            return new InventoryManagementContext(root, new JsonArray(), []);

        var items = new List<InventoryManagementItem>();
        for (var index = 0; index < itemsArray.Count; index++)
        {
            if (itemsArray[index] is not JsonObject item)
                continue;

            items.Add(ReadItem(index, item));
        }

        var equippedSlots = ReadEquippedSlots(root, items);
        var enriched = items
            .Select(item =>
            {
                var equippedSlot = string.Empty;
                if (!string.IsNullOrWhiteSpace(item.Identity))
                    equippedSlots.TryGetValue(item.Identity, out equippedSlot);
                if (string.IsNullOrWhiteSpace(equippedSlot) && !string.IsNullOrWhiteSpace(item.Name))
                    equippedSlots.TryGetValue(item.Name, out equippedSlot);
                return item with { EquippedSlot = equippedSlot ?? string.Empty };
            })
            .ToArray();

        return new InventoryManagementContext(root, itemsArray, enriched);
    }

    public static InventoryManagementItem? FindItem(
        IEnumerable<InventoryManagementItem> items,
        string identityOrName)
    {
        if (string.IsNullOrWhiteSpace(identityOrName) ||
            !string.Equals(identityOrName, identityOrName.Trim(), StringComparison.Ordinal))
            return null;

        return items.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(item.Identity) &&
            string.Equals(item.Identity, identityOrName, StringComparison.Ordinal));
    }

    public static IReadOnlyList<InventoryManagementItem> FindCompatibleStacks(
        InventoryManagementContext context,
        InventoryManagementItem selected)
    {
        return context.Items
            .Where(item => MortalItemTransitionWriter.StackSemanticsEqual(selected.Data, item.Data))
            .ToArray();
    }

    public static async Task<InventoryManagementWriteOutcome> ValidateDropAsync(
        FileSystemManager fs,
        string itemIdentityOrName) =>
        await ValidateDropCoreAsync(fs, writeLease: null, itemIdentityOrName);

    internal static async Task<InventoryManagementWriteOutcome> ValidateDropAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string itemIdentityOrName) =>
        await ValidateDropCoreAsync(fs, writeLease, itemIdentityOrName);

    private static async Task<InventoryManagementWriteOutcome> ValidateDropCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string itemIdentityOrName)
    {
        var context = await ReadContextCoreAsync(fs, writeLease);
        if (context == null)
            return InventoryManagementWriteOutcome.Failed("Инвентарь пуст или повреждён.");

        var item = FindItem(context.Items, itemIdentityOrName);
        if (item == null)
            return InventoryManagementWriteOutcome.Failed("Предмет не найден в инвентаре.");

        var equippedText = string.IsNullOrWhiteSpace(item.EquippedSlot)
            ? string.Empty
            : $" Слот {InventoryEquipmentService.FormatSlotName(item.EquippedSlot)} будет освобождён.";
        return InventoryManagementWriteOutcome.Completed(
            $"«{item.Name}» будет выброшен.{equippedText}",
            item.Identity,
            item.Name,
            item.Count);
    }

    public static async Task<InventoryManagementWriteOutcome> DropAsync(
        FileSystemManager fs,
        string itemIdentityOrName)
    {
        await using var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
        return await DropCoreAsync(fs, writeLease, itemIdentityOrName);
    }

    internal static async Task<InventoryManagementWriteOutcome> DropAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string itemIdentityOrName) =>
        await DropCoreAsync(fs, writeLease, itemIdentityOrName);

    private static async Task<InventoryManagementWriteOutcome> DropCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string itemIdentityOrName)
    {
        var context = await ReadContextCoreAsync(fs, writeLease);
        if (context == null)
            return InventoryManagementWriteOutcome.Failed("Инвентарь пуст или повреждён.");

        var item = FindItem(context.Items, itemIdentityOrName);
        if (item == null)
            return InventoryManagementWriteOutcome.Failed("Предмет не найден в инвентаре.");

        if (string.IsNullOrWhiteSpace(item.Identity))
            return InventoryManagementWriteOutcome.Failed("Предмет не имеет точного permanent itemId.");
        var turn = await ResolveLocalTransitionTurnAsync(fs, writeLease);
        var transition = await new MortalItemTransitionWriter(fs).ExecuteAsync(
            writeLease,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Destroy,
                new[] { item.Identity },
                PlayerCarrier(item.Data),
                DestinationCarrier: null,
                Quantity: item.Count,
                Turn: turn,
                AuthorityKind: "inventory_discard",
                AuthorityId: $"inventory_discard:{turn}:{item.Identity}"));
        if (!transition.Success)
            return InventoryManagementWriteOutcome.Failed(transition.Message);

        return InventoryManagementWriteOutcome.Completed(
            $"«{item.Name}» выброшен.",
            item.Identity,
            item.Name,
            item.Count);
    }

    public static async Task<InventoryManagementWriteOutcome> ValidateSplitAsync(
        FileSystemManager fs,
        string itemIdentityOrName,
        int splitQuantity) =>
        await ValidateSplitCoreAsync(fs, writeLease: null, itemIdentityOrName, splitQuantity);

    internal static async Task<InventoryManagementWriteOutcome> ValidateSplitAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string itemIdentityOrName,
        int splitQuantity) =>
        await ValidateSplitCoreAsync(fs, writeLease, itemIdentityOrName, splitQuantity);

    private static async Task<InventoryManagementWriteOutcome> ValidateSplitCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string itemIdentityOrName,
        int splitQuantity)
    {
        var context = await ReadContextCoreAsync(fs, writeLease);
        if (context == null)
            return InventoryManagementWriteOutcome.Failed("Инвентарь пуст или повреждён.");

        var item = FindItem(context.Items, itemIdentityOrName);
        if (item == null)
            return InventoryManagementWriteOutcome.Failed("Предмет не найден в инвентаре.");

        return ValidateSplit(item, splitQuantity);
    }

    public static async Task<InventoryManagementWriteOutcome> SplitAsync(
        FileSystemManager fs,
        string itemIdentityOrName,
        int splitQuantity)
    {
        await using var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
        return await SplitCoreAsync(fs, writeLease, itemIdentityOrName, splitQuantity);
    }

    internal static async Task<InventoryManagementWriteOutcome> SplitAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string itemIdentityOrName,
        int splitQuantity) =>
        await SplitCoreAsync(fs, writeLease, itemIdentityOrName, splitQuantity);

    private static async Task<InventoryManagementWriteOutcome> SplitCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string itemIdentityOrName,
        int splitQuantity)
    {
        var context = await ReadContextCoreAsync(fs, writeLease);
        if (context == null)
            return InventoryManagementWriteOutcome.Failed("Инвентарь пуст или повреждён.");

        var item = FindItem(context.Items, itemIdentityOrName);
        if (item == null)
            return InventoryManagementWriteOutcome.Failed("Предмет не найден в инвентаре.");

        var validation = ValidateSplit(item, splitQuantity);
        if (!validation.Success)
            return validation;

        if (string.IsNullOrWhiteSpace(item.Identity))
            return InventoryManagementWriteOutcome.Failed("Стопка не имеет точного permanent itemId.");
        var turn = await ResolveLocalTransitionTurnAsync(fs, writeLease);
        var carrier = PlayerCarrier(item.Data);
        var transition = await new MortalItemTransitionWriter(fs).ExecuteAsync(
            writeLease,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Split,
                new[] { item.Identity },
                carrier,
                carrier,
                Quantity: splitQuantity,
                Turn: turn,
                AuthorityKind: "inventory_split",
                AuthorityId: $"inventory_split:{turn}:{item.Identity}"));
        if (!transition.Success)
            return InventoryManagementWriteOutcome.Failed(transition.Message);

        return InventoryManagementWriteOutcome.Completed(
            $"Стопка «{item.Name}» разделена: {item.Count - splitQuantity} и {splitQuantity}.",
            item.Identity,
            item.Name,
            splitQuantity,
            transition.DerivedItemId);
    }

    public static async Task<InventoryManagementWriteOutcome> ValidateMergeAsync(
        FileSystemManager fs,
        string itemIdentityOrName) =>
        await ValidateMergeCoreAsync(fs, writeLease: null, itemIdentityOrName);

    internal static async Task<InventoryManagementWriteOutcome> ValidateMergeAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string itemIdentityOrName) =>
        await ValidateMergeCoreAsync(fs, writeLease, itemIdentityOrName);

    private static async Task<InventoryManagementWriteOutcome> ValidateMergeCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string itemIdentityOrName)
    {
        var context = await ReadContextCoreAsync(fs, writeLease);
        if (context == null)
            return InventoryManagementWriteOutcome.Failed("Инвентарь пуст или повреждён.");

        var item = FindItem(context.Items, itemIdentityOrName);
        if (item == null)
            return InventoryManagementWriteOutcome.Failed("Предмет не найден в инвентаре.");

        return ValidateMerge(context, item);
    }

    public static async Task<InventoryManagementWriteOutcome> MergeAsync(
        FileSystemManager fs,
        string itemIdentityOrName)
    {
        await using var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
        return await MergeCoreAsync(fs, writeLease, itemIdentityOrName);
    }

    internal static async Task<InventoryManagementWriteOutcome> MergeAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string itemIdentityOrName) =>
        await MergeCoreAsync(fs, writeLease, itemIdentityOrName);

    private static async Task<InventoryManagementWriteOutcome> MergeCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string itemIdentityOrName)
    {
        var context = await ReadContextCoreAsync(fs, writeLease);
        if (context == null)
            return InventoryManagementWriteOutcome.Failed("Инвентарь пуст или повреждён.");

        var item = FindItem(context.Items, itemIdentityOrName);
        if (item == null)
            return InventoryManagementWriteOutcome.Failed("Предмет не найден в инвентаре.");

        var validation = ValidateMerge(context, item);
        if (!validation.Success)
            return validation;

        if (string.IsNullOrWhiteSpace(item.Identity))
            return InventoryManagementWriteOutcome.Failed("Стопка не имеет точного permanent itemId.");
        var compatible = FindCompatibleStacks(context, item);
        if (compatible.Any(candidate => string.IsNullOrWhiteSpace(candidate.Identity)))
            return InventoryManagementWriteOutcome.Failed("Совместимая стопка не имеет точного permanent itemId.");
        var totalLong = compatible.Sum(candidate => (long)candidate.Count);
        if (totalLong > int.MaxValue)
            return InventoryManagementWriteOutcome.Failed("Итоговое количество стопки слишком велико.");
        var totalCount = (int)totalLong;
        var turn = await ResolveLocalTransitionTurnAsync(fs, writeLease);
        var carrier = PlayerCarrier(item.Data);
        var transition = await new MortalItemTransitionWriter(fs).ExecuteAsync(
            writeLease,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Merge,
                compatible.Select(candidate => candidate.Identity).ToArray(),
                carrier,
                carrier,
                Quantity: totalCount,
                Turn: turn,
                AuthorityKind: "inventory_merge",
                AuthorityId: $"inventory_merge:{turn}:{item.Identity}",
                SurvivorItemId: item.Identity));
        if (!transition.Success)
            return InventoryManagementWriteOutcome.Failed(transition.Message);

        return InventoryManagementWriteOutcome.Completed(
            $"Стопки «{item.Name}» объединены: {totalCount} шт.",
            item.Identity,
            item.Name,
            totalCount);
    }

    private static async Task<int> ResolveLocalTransitionTurnAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var latestTurn = 1;
        var indexJson = await fs.ReadFileAsync(writeLease, MortalItemIdentityState.StatePath);
        var index = MortalItemIdentityState.Parse(indexJson);
        foreach (var entry in index.EntriesByItemId.Values)
        {
            if (entry["transitions"] is not JsonArray transitions)
                continue;
            foreach (var transition in transitions.OfType<JsonObject>())
            {
                if (TryReadInt(transition["turn"], out var transitionTurn))
                    latestTurn = Math.Max(latestTurn, transitionTurn);
            }
        }

        var storiesRoot = fs.ResolvePath("stories");
        if (!Directory.Exists(storiesRoot))
            return latestTurn;
        foreach (var path in Directory.EnumerateFiles(storiesRoot, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var relativePath = Path.GetRelativePath(fs.GameSessionPath, path).Replace('\\', '/');
                var json = await fs.ReadFileAsync(writeLease, relativePath);
                if (string.IsNullOrWhiteSpace(json))
                    continue;
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("turnNumber", out var turnNode) &&
                    turnNode.ValueKind == JsonValueKind.Number &&
                    turnNode.TryGetInt32(out var storyTurn))
                {
                    latestTurn = Math.Max(latestTurn, storyTurn);
                }
            }
            catch (Exception exception) when (
                exception is IOException or JsonException or UnauthorizedAccessException)
            {
                // An unrelated partial story file cannot make a local inventory write move backward.
            }
        }
        return latestTurn;
    }

    private static MortalItemCarrierCoordinate PlayerCarrier(JsonObject item) =>
        new(
            "player_inventory",
            "player",
            null,
            ReadExactContentsPath(item));

    private static IReadOnlyList<string> ReadExactContentsPath(JsonObject item)
    {
        if (item["contentsPath"] == null)
            return Array.Empty<string>();
        if (item["contentsPath"] is not JsonArray path)
            return new[] { string.Empty };

        var result = new List<string>(path.Count);
        foreach (var node in path)
        {
            if (node is JsonValue value &&
                value.TryGetValue<string>(out var identity) &&
                !string.IsNullOrWhiteSpace(identity) &&
                string.Equals(identity, identity.Trim(), StringComparison.Ordinal))
            {
                result.Add(identity);
            }
            else
            {
                result.Add(string.Empty);
            }
        }
        return result;
    }

    private static InventoryManagementWriteOutcome ValidateSplit(InventoryManagementItem item, int splitQuantity)
    {
        if (item.Count <= 1)
            return InventoryManagementWriteOutcome.Failed("У этой стопки недостаточно предметов для разделения.");

        if (splitQuantity < 1 || splitQuantity >= item.Count)
            return InventoryManagementWriteOutcome.Failed($"Введите количество от 1 до {item.Count - 1}.");

        return InventoryManagementWriteOutcome.Completed(
            $"Из «{item.Name}» будет отделено {splitQuantity} из {item.Count}.",
            item.Identity,
            item.Name,
            splitQuantity);
    }

    private static InventoryManagementWriteOutcome ValidateMerge(
        InventoryManagementContext context,
        InventoryManagementItem item)
    {
        var compatible = FindCompatibleStacks(context, item);
        if (compatible.Count < 2)
            return InventoryManagementWriteOutcome.Failed("Нет другой совместимой стопки для объединения.");

        var totalLong = compatible.Sum(static match => (long)match.Count);
        if (totalLong > int.MaxValue)
            return InventoryManagementWriteOutcome.Failed("Итоговое количество стопки слишком велико.");
        var total = (int)totalLong;
        return InventoryManagementWriteOutcome.Completed(
            $"Будет объединено стопок: {compatible.Count}. Итоговое количество: {total}.",
            item.Identity,
            item.Name,
            total);
    }

    private static InventoryManagementItem ReadItem(int index, JsonObject item)
    {
        var identity = FirstNonEmpty(
            GetString(item, "existedId"),
            GetString(item, "itemId"),
            GetString(item, "id"));
        var name = FirstNonEmpty(GetString(item, "name"), GetString(item, "itemName"), "???");
        var countField = item.ContainsKey("quantity") ? "quantity" : "count";
        var count = ReadStackCount(item);

        return new InventoryManagementItem(
            Index: index,
            Identity: identity,
            Name: name,
            Count: count,
            CountField: countField,
            EquippedSlot: string.Empty,
            Data: item);
    }

    private static Dictionary<string, string> ReadEquippedSlots(
        JsonObject root,
        IReadOnlyList<InventoryManagementItem> items)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var equipmentProperty in new[] { "equipment", "equippedItems" })
        {
            if (root[equipmentProperty] is not JsonObject equipment)
                continue;

            foreach (var prop in equipment)
            {
                if (prop.Value == null || prop.Value.GetValueKind() == JsonValueKind.Null)
                    continue;

                var referenceIdentity = ReadEquipmentReferenceIdentity(prop.Value);
                var referenceName = ReadEquipmentReferenceName(prop.Value);
                var matched = FindMatchingItem(items, referenceIdentity, referenceName);
                if (matched == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(matched.Identity))
                    result[matched.Identity] = prop.Key;
                if (!string.IsNullOrWhiteSpace(matched.Name))
                    result[matched.Name] = prop.Key;
            }
        }

        return result;
    }

    private static InventoryManagementItem? FindMatchingItem(
        IEnumerable<InventoryManagementItem> items,
        string itemIdentity,
        string itemName) =>
        items.FirstOrDefault(item =>
            (!string.IsNullOrWhiteSpace(itemIdentity) &&
             string.Equals(item.Identity, itemIdentity, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(itemName) &&
             string.Equals(item.Name, itemName, StringComparison.OrdinalIgnoreCase)));

    private static string ReadEquipmentReferenceIdentity(JsonNode? slotData)
    {
        if (TryGetScalarString(slotData, out var scalar))
            return scalar;

        if (slotData is not JsonObject obj)
            return string.Empty;

        return FirstNonEmpty(
            GetString(obj, "existedId"),
            GetString(obj, "itemId"),
            GetString(obj, "id"));
    }

    private static string ReadEquipmentReferenceName(JsonNode? slotData)
    {
        if (TryGetScalarString(slotData, out var scalar))
            return scalar;

        return slotData is JsonObject obj
            ? FirstNonEmpty(GetString(obj, "name"), GetString(obj, "itemName"))
            : string.Empty;
    }

    private static JsonArray? GetPlayerInventoryArrayNode(JsonObject root)
    {
        if (root["items"] is JsonArray items)
            return items;

        if (root["UpdateInventory"] is JsonArray updateInventory)
            return updateInventory;

        return null;
    }

    private static int ReadStackCount(JsonObject? item)
    {
        if (item == null)
            return 1;

        if (TryReadInt(item["count"], out var count) || TryReadInt(item["quantity"], out count))
            return Math.Max(1, count);

        return 1;
    }

    private static bool TryReadInt(JsonNode? node, out int value)
    {
        value = 0;
        if (node is not JsonValue jsonValue)
            return false;

        if (jsonValue.TryGetValue<int>(out value))
            return true;

        if (jsonValue.TryGetValue<long>(out var longValue))
        {
            value = (int)longValue;
            return true;
        }

        return jsonValue.TryGetValue<string>(out var text) && int.TryParse(text, out value);
    }

    private static string GetString(JsonObject obj, string propertyName) =>
        TryGetScalarString(obj[propertyName], out var value) ? value : string.Empty;

    private static bool TryGetScalarString(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue jsonValue)
            return false;

        if (jsonValue.TryGetValue<string>(out var text))
        {
            value = text ?? string.Empty;
            return true;
        }

        if (jsonValue.TryGetValue<int>(out var intValue))
        {
            value = intValue.ToString();
            return true;
        }

        if (jsonValue.TryGetValue<long>(out var longValue))
        {
            value = longValue.ToString();
            return true;
        }

        if (jsonValue.TryGetValue<bool>(out var boolValue))
        {
            value = boolValue ? "true" : "false";
            return true;
        }

        return false;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed record InventoryManagementContext(
    JsonObject Root,
    JsonArray ItemsArray,
    IReadOnlyList<InventoryManagementItem> Items);

public sealed record InventoryManagementItem(
    int Index,
    string Identity,
    string Name,
    int Count,
    string CountField,
    string EquippedSlot,
    JsonObject Data);

public sealed record InventoryManagementWriteOutcome(
    bool Success,
    string Message,
    string ItemIdentity,
    string ItemName,
    int Count,
    string? DerivedItemIdentity)
{
    public static InventoryManagementWriteOutcome Completed(
        string message,
        string itemIdentity,
        string itemName,
        int count,
        string? derivedItemIdentity = null) =>
        new(true, message, itemIdentity, itemName, count, derivedItemIdentity);

    public static InventoryManagementWriteOutcome Failed(string message) =>
        new(false, message, string.Empty, string.Empty, 0, null);
}
