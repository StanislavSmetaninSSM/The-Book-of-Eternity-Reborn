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
        if (string.IsNullOrWhiteSpace(identityOrName))
            return null;

        var trimmed = identityOrName.Trim();
        return items.FirstOrDefault(item =>
            (!string.IsNullOrWhiteSpace(item.Identity) &&
             string.Equals(item.Identity, trimmed, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(item.Name) &&
             string.Equals(item.Name, trimmed, StringComparison.OrdinalIgnoreCase)));
    }

    public static IReadOnlyList<InventoryManagementItem> FindCompatibleStacks(
        InventoryManagementContext context,
        InventoryManagementItem selected)
    {
        var selectedSignature = CreateInventoryMergeSignature(selected.Data);
        return context.Items
            .Where(item => string.Equals(CreateInventoryMergeSignature(item.Data), selectedSignature, StringComparison.Ordinal))
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
        string itemIdentityOrName) =>
        await DropCoreAsync(fs, writeLease: null, itemIdentityOrName);

    internal static async Task<InventoryManagementWriteOutcome> DropAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string itemIdentityOrName) =>
        await DropCoreAsync(fs, writeLease, itemIdentityOrName);

    private static async Task<InventoryManagementWriteOutcome> DropCoreAsync(
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

        context.ItemsArray.RemoveAt(item.Index);
        ClearMatchingEquipmentReferences(context.Root, item.Identity, item.Name);

        await WriteAsync(
            fs,
            writeLease,
            context.Root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

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
        int splitQuantity) =>
        await SplitCoreAsync(fs, writeLease: null, itemIdentityOrName, splitQuantity);

    internal static async Task<InventoryManagementWriteOutcome> SplitAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string itemIdentityOrName,
        int splitQuantity) =>
        await SplitCoreAsync(fs, writeLease, itemIdentityOrName, splitQuantity);

    private static async Task<InventoryManagementWriteOutcome> SplitCoreAsync(
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

        var validation = ValidateSplit(item, splitQuantity);
        if (!validation.Success)
            return validation;

        var original = context.ItemsArray[item.Index]!.AsObject();
        var countKey = original.ContainsKey("quantity") ? "quantity" : "count";
        var currentCount = ReadStackCount(original);
        original[countKey] = currentCount - splitQuantity;

        var copy = JsonNode.Parse(original.ToJsonString())!.AsObject();
        copy[countKey] = splitQuantity;
        AssignNewInventoryIdentity(copy);
        context.ItemsArray.Add(copy);

        await WriteAsync(
            fs,
            writeLease,
            context.Root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        return InventoryManagementWriteOutcome.Completed(
            $"Стопка «{item.Name}» разделена: {currentCount - splitQuantity} и {splitQuantity}.",
            item.Identity,
            item.Name,
            splitQuantity);
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
        string itemIdentityOrName) =>
        await MergeCoreAsync(fs, writeLease: null, itemIdentityOrName);

    internal static async Task<InventoryManagementWriteOutcome> MergeAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string itemIdentityOrName) =>
        await MergeCoreAsync(fs, writeLease, itemIdentityOrName);

    private static async Task<InventoryManagementWriteOutcome> MergeCoreAsync(
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

        var validation = ValidateMerge(context, item);
        if (!validation.Success)
            return validation;

        var matchingIndices = new List<int> { item.Index };
        var selectedSignature = CreateInventoryMergeSignature(item.Data);
        for (var i = 0; i < context.ItemsArray.Count; i++)
        {
            if (i == item.Index)
                continue;

            if (CreateInventoryMergeSignature(context.ItemsArray[i]) == selectedSignature)
                matchingIndices.Add(i);
        }

        var first = context.ItemsArray[matchingIndices[0]]!.AsObject();
        var countKey = first.ContainsKey("quantity") ? "quantity" : "count";
        var totalCount = 0;
        foreach (var idx in matchingIndices)
            totalCount += ReadStackCount(context.ItemsArray[idx] as JsonObject);

        first[countKey] = totalCount;

        for (var j = matchingIndices.Count - 1; j >= 1; j--)
            context.ItemsArray.RemoveAt(matchingIndices[j]);

        await WriteAsync(
            fs,
            writeLease,
            context.Root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        return InventoryManagementWriteOutcome.Completed(
            $"Стопки «{item.Name}» объединены: {totalCount} шт.",
            item.Identity,
            item.Name,
            totalCount);
    }

    private static Task WriteAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string content) =>
        writeLease == null
            ? fs.WriteFileAtomicAsync(InventoryEquipmentService.ItemsPath, content)
            : fs.WriteFileAtomicAsync(writeLease, InventoryEquipmentService.ItemsPath, content);

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

        var total = compatible.Sum(static match => match.Count);
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

    private static void ClearMatchingEquipmentReferences(JsonObject root, string itemIdentity, string itemName)
    {
        foreach (var equipmentProperty in new[] { "equipment", "equippedItems" })
        {
            if (root[equipmentProperty] is not JsonObject equipment)
                continue;

            foreach (var prop in equipment.ToArray())
            {
                if (InventoryReferenceMatches(prop.Value, itemIdentity, itemName))
                    equipment[prop.Key] = null;
            }
        }
    }

    private static bool InventoryReferenceMatches(JsonNode? referenceNode, string itemIdentity, string itemName)
    {
        if (referenceNode == null)
            return false;

        if (referenceNode is JsonValue value && value.TryGetValue<string>(out var reference))
        {
            if (!string.IsNullOrWhiteSpace(itemIdentity) &&
                string.Equals(reference, itemIdentity, StringComparison.OrdinalIgnoreCase))
                return true;

            return !string.IsNullOrWhiteSpace(itemName) &&
                   string.Equals(reference, itemName, StringComparison.OrdinalIgnoreCase);
        }

        if (referenceNode is not JsonObject obj)
            return false;

        var identity = FirstNonEmpty(
            GetString(obj, "existedId"),
            GetString(obj, "itemId"),
            GetString(obj, "id"));
        if (!string.IsNullOrWhiteSpace(itemIdentity) &&
            string.Equals(identity, itemIdentity, StringComparison.OrdinalIgnoreCase))
            return true;

        var name = FirstNonEmpty(GetString(obj, "name"), GetString(obj, "itemName"));
        return !string.IsNullOrWhiteSpace(itemName) &&
               string.Equals(name, itemName, StringComparison.OrdinalIgnoreCase);
    }

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

    private static void AssignNewInventoryIdentity(JsonObject item)
    {
        var newId = Guid.NewGuid().ToString();
        var hadIdentityField = false;

        foreach (var key in new[] { "existedId", "itemId", "id" })
        {
            if (!item.ContainsKey(key))
                continue;

            item[key] = newId;
            hadIdentityField = true;
        }

        if (!hadIdentityField)
            item["existedId"] = newId;
    }

    private static string CreateInventoryMergeSignature(JsonNode? item)
    {
        if (item is not JsonObject obj)
            return item?.ToJsonString() ?? string.Empty;

        var clone = JsonNode.Parse(obj.ToJsonString()) as JsonObject;
        if (clone == null)
            return string.Empty;

        clone.Remove("count");
        clone.Remove("quantity");
        clone.Remove("id");
        clone.Remove("itemId");
        clone.Remove("existedId");
        clone.Remove("initialId");

        return clone.ToJsonString();
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
    int Count)
{
    public static InventoryManagementWriteOutcome Completed(
        string message,
        string itemIdentity,
        string itemName,
        int count) =>
        new(true, message, itemIdentity, itemName, count);

    public static InventoryManagementWriteOutcome Failed(string message) =>
        new(false, message, string.Empty, string.Empty, 0);
}
