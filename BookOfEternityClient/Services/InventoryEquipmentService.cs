using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public static class InventoryEquipmentService
{
    public const string ItemsPath = "game_state/inventory/items.json";

    public static readonly IReadOnlyDictionary<string, string> SlotLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["head"] = "🪖 Голова",
            ["body"] = "🛡️ Тело",
            ["hands"] = "🧤 Руки",
            ["feet"] = "👢 Ноги",
            ["mainHand"] = "⚔️ Основная рука",
            ["offHand"] = "🛡️ Вторая рука",
            ["neck"] = "📿 Шея",
            ["ring1"] = "💍 Кольцо 1",
            ["ring2"] = "💍 Кольцо 2"
        };

    public static async Task<InventoryEquipmentContext?> ReadContextAsync(FileSystemManager fs) =>
        await ReadContextCoreAsync(fs, writeLease: null);

    internal static async Task<InventoryEquipmentContext?> ReadContextAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        await ReadContextCoreAsync(fs, writeLease);

    private static async Task<InventoryEquipmentContext?> ReadContextCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease)
    {
        var raw = writeLease == null
            ? await fs.ReadFileAsync(ItemsPath)
            : await fs.ReadFileAsync(writeLease, ItemsPath);
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

        var items = ReadItems(root);
        var equipped = ReadEquipped(root, items);
        var equippedSlotsByItem = equipped
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.ItemIdentity))
            .GroupBy(static entry => entry.ItemIdentity, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First().SlotKey, StringComparer.OrdinalIgnoreCase);
        var equippedNames = equipped
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.ItemName))
            .GroupBy(static entry => entry.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First().SlotKey, StringComparer.OrdinalIgnoreCase);

        var enrichedItems = items
            .Select(item =>
            {
                var equippedSlot = string.Empty;
                if (!string.IsNullOrWhiteSpace(item.Identity))
                    equippedSlotsByItem.TryGetValue(item.Identity, out equippedSlot);
                if (string.IsNullOrWhiteSpace(equippedSlot) && !string.IsNullOrWhiteSpace(item.Name))
                    equippedNames.TryGetValue(item.Name, out equippedSlot);
                return item with { EquippedSlot = equippedSlot ?? string.Empty };
            })
            .ToArray();

        equipped = ReadEquipped(root, enrichedItems);
        return new InventoryEquipmentContext(root, enrichedItems, equipped);
    }

    public static async Task<InventoryEquipmentWriteOutcome> EquipAsync(
        FileSystemManager fs,
        string itemIdentityOrName,
        string slotKey) =>
        await EquipCoreAsync(fs, writeLease: null, itemIdentityOrName, slotKey);

    internal static async Task<InventoryEquipmentWriteOutcome> EquipAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string itemIdentityOrName,
        string slotKey) =>
        await EquipCoreAsync(fs, writeLease, itemIdentityOrName, slotKey);

    private static async Task<InventoryEquipmentWriteOutcome> EquipCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string itemIdentityOrName,
        string slotKey)
    {
        var context = await ReadContextCoreAsync(fs, writeLease);
        if (context == null)
            return InventoryEquipmentWriteOutcome.Failed("Инвентарь пуст или повреждён.");

        var outcome = ValidateEquip(context, itemIdentityOrName, slotKey);
        if (!outcome.Success)
            return outcome;

        var equipment = context.Root["equipment"] as JsonObject;
        if (equipment == null)
        {
            equipment = new JsonObject();
            context.Root["equipment"] = equipment;
        }

        var item = FindItem(context.Items, itemIdentityOrName)!;
        var reference = !string.IsNullOrWhiteSpace(item.Identity) ? item.Identity : item.Name;
        equipment[outcome.SlotKey] = reference;
        await WriteAsync(
            fs,
            writeLease,
            context.Root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        return outcome;
    }

    public static async Task<InventoryEquipmentWriteOutcome> UnequipAsync(
        FileSystemManager fs,
        string slotKey) =>
        await UnequipCoreAsync(fs, writeLease: null, slotKey);

    internal static async Task<InventoryEquipmentWriteOutcome> UnequipAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string slotKey) =>
        await UnequipCoreAsync(fs, writeLease, slotKey);

    private static async Task<InventoryEquipmentWriteOutcome> UnequipCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string slotKey)
    {
        var context = await ReadContextCoreAsync(fs, writeLease);
        if (context == null)
            return InventoryEquipmentWriteOutcome.Failed("Инвентарь пуст или повреждён.");

        var outcome = ValidateUnequip(context, slotKey);
        if (!outcome.Success)
            return outcome;

        if (context.Root["equipment"] is not JsonObject equipment)
            return InventoryEquipmentWriteOutcome.Failed("Экипировка не найдена.");

        equipment[outcome.SlotKey] = null;
        await WriteAsync(
            fs,
            writeLease,
            context.Root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        return outcome;
    }

    public static async Task<InventoryEquipmentWriteOutcome> ValidateEquipAsync(
        FileSystemManager fs,
        string itemIdentityOrName,
        string slotKey) =>
        await ValidateEquipCoreAsync(fs, writeLease: null, itemIdentityOrName, slotKey);

    internal static async Task<InventoryEquipmentWriteOutcome> ValidateEquipAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string itemIdentityOrName,
        string slotKey) =>
        await ValidateEquipCoreAsync(fs, writeLease, itemIdentityOrName, slotKey);

    private static async Task<InventoryEquipmentWriteOutcome> ValidateEquipCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string itemIdentityOrName,
        string slotKey)
    {
        var context = await ReadContextCoreAsync(fs, writeLease);
        if (context == null)
            return InventoryEquipmentWriteOutcome.Failed("Инвентарь пуст или повреждён.");

        return ValidateEquip(context, itemIdentityOrName, slotKey);
    }

    public static async Task<InventoryEquipmentWriteOutcome> ValidateUnequipAsync(
        FileSystemManager fs,
        string slotKey) =>
        await ValidateUnequipCoreAsync(fs, writeLease: null, slotKey);

    internal static async Task<InventoryEquipmentWriteOutcome> ValidateUnequipAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string slotKey) =>
        await ValidateUnequipCoreAsync(fs, writeLease, slotKey);

    private static async Task<InventoryEquipmentWriteOutcome> ValidateUnequipCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string slotKey)
    {
        var context = await ReadContextCoreAsync(fs, writeLease);
        if (context == null)
            return InventoryEquipmentWriteOutcome.Failed("Инвентарь пуст или повреждён.");

        return ValidateUnequip(context, slotKey);
    }

    private static Task WriteAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string content) =>
        writeLease == null
            ? fs.WriteFileAtomicAsync(ItemsPath, content)
            : fs.WriteFileAtomicAsync(writeLease, ItemsPath, content);

    public static InventoryEquipmentItem? FindItem(
        IEnumerable<InventoryEquipmentItem> items,
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

    public static string FormatSlotName(string slotKey) =>
        SlotLabels.TryGetValue(slotKey, out var label) ? label : slotKey;

    public static bool TryNormalizeSlot(string slotKey, out string normalizedSlot)
    {
        normalizedSlot = string.Empty;
        if (string.IsNullOrWhiteSpace(slotKey))
            return false;

        var direct = SlotLabels.Keys.FirstOrDefault(key =>
            string.Equals(key, slotKey.Trim(), StringComparison.OrdinalIgnoreCase));
        if (direct == null)
            return false;

        normalizedSlot = direct;
        return true;
    }

    public static string? ResolveEquipSlot(string itemSlot, string itemType)
    {
        _ = itemType;
        return string.IsNullOrWhiteSpace(itemSlot)
            ? null
            : SlotLabels.Keys.FirstOrDefault(key =>
                string.Equals(key, itemSlot.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static string ReadFirstCommandArgument(string command)
    {
        var parts = command.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return string.Empty;

        var remainder = parts[1].Trim();
        if (remainder.Length == 0)
            return string.Empty;

        if (remainder[0] != '"')
            return remainder.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

        var builder = new StringBuilder();
        var escaping = false;
        for (var i = 1; i < remainder.Length; i++)
        {
            var ch = remainder[i];
            if (escaping)
            {
                builder.Append(ch);
                escaping = false;
                continue;
            }

            if (ch == '\\')
            {
                escaping = true;
                continue;
            }

            if (ch == '"')
                break;

            builder.Append(ch);
        }

        return builder.ToString().Trim();
    }

    public static string FormatCommandArgument(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            return "\"\"";

        if (trimmed.All(static ch => !char.IsWhiteSpace(ch) && ch != '"' && ch != '\\'))
            return trimmed;

        return "\"" + trimmed.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    public static string BuildActionId(string prefix, string identityOrName)
    {
        var source = string.IsNullOrWhiteSpace(identityOrName) ? "item" : identityOrName.Trim();
        var chars = source
            .Select(static ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' ? ch : '_')
            .ToArray();
        var slug = new string(chars).Trim('_');
        if (string.IsNullOrWhiteSpace(slug))
            slug = "item";
        return $"{prefix}-{slug}";
    }

    private static InventoryEquipmentWriteOutcome ValidateEquip(
        InventoryEquipmentContext context,
        string itemIdentityOrName,
        string slotKey)
    {
        var item = FindItem(context.Items, itemIdentityOrName);
        if (item == null)
            return InventoryEquipmentWriteOutcome.Failed("Предмет не найден в инвентаре.");

        if (item.IsSoulRelic)
            return InventoryEquipmentWriteOutcome.Failed("Реликвии души экипируются только через отдельный посмертный поток.");

        if (item.IsBroken)
            return InventoryEquipmentWriteOutcome.Failed($"«{item.Name}» сломан и не может быть экипирован.");

        if (!item.IsEquippable)
            return InventoryEquipmentWriteOutcome.Failed($"«{item.Name}» нельзя экипировать как обычный предмет.");

        if (!string.IsNullOrWhiteSpace(item.EquippedSlot))
            return InventoryEquipmentWriteOutcome.Failed($"«{item.Name}» уже экипирован: {FormatSlotName(item.EquippedSlot)}.");

        if (!TryNormalizeSlot(slotKey, out var normalizedSlot))
            return InventoryEquipmentWriteOutcome.Failed("Выберите корректный слот экипировки.");

        if (!string.IsNullOrWhiteSpace(item.ResolvedSlot) &&
            !string.Equals(item.ResolvedSlot, normalizedSlot, StringComparison.OrdinalIgnoreCase))
        {
            return InventoryEquipmentWriteOutcome.Failed(
                $"«{item.Name}» подходит только для слота: {FormatSlotName(item.ResolvedSlot)}.");
        }

        var slotLabel = FormatSlotName(normalizedSlot);
        return InventoryEquipmentWriteOutcome.Completed(
            $"«{item.Name}» экипирован: {slotLabel}.",
            item.Identity,
            item.Name,
            normalizedSlot,
            slotLabel);
    }

    private static InventoryEquipmentWriteOutcome ValidateUnequip(
        InventoryEquipmentContext context,
        string slotKey)
    {
        if (!TryNormalizeSlot(slotKey, out var normalizedSlot))
            return InventoryEquipmentWriteOutcome.Failed("Выберите корректный слот экипировки.");

        var equipped = context.Equipped.FirstOrDefault(entry =>
            string.Equals(entry.SlotKey, normalizedSlot, StringComparison.OrdinalIgnoreCase));
        if (equipped == null)
            return InventoryEquipmentWriteOutcome.Failed("В выбранном слоте нет экипированного предмета.");

        if (!equipped.IsOrdinaryInventoryItem)
            return InventoryEquipmentWriteOutcome.Failed("Этот слот не относится к обычному инвентарю персонажа.");

        return InventoryEquipmentWriteOutcome.Completed(
            $"«{equipped.ItemName}» снят и убран в рюкзак.",
            equipped.ItemIdentity,
            equipped.ItemName,
            normalizedSlot,
            equipped.SlotLabel);
    }

    private static InventoryEquipmentItem[] ReadItems(JsonObject root)
    {
        var array = GetPlayerInventoryArrayNode(root);
        if (array == null)
            return [];

        return array
            .OfType<JsonObject>()
            .Select(ReadItem)
            .Where(static item => !string.IsNullOrWhiteSpace(item.Name))
            .ToArray();
    }

    private static InventoryEquipmentItem ReadItem(JsonObject item)
    {
        var identity = FirstNonEmpty(
            GetString(item, "existedId"),
            GetString(item, "itemId"),
            GetString(item, "id"));
        var name = FirstNonEmpty(GetString(item, "name"), GetString(item, "itemName"), "???");
        var type = GetString(item, "type");
        var itemSlot = GetString(item, "equipmentSlot");
        var resolvedSlot = ResolveEquipSlot(itemSlot, type) ?? string.Empty;
        var isBroken = ReadBool(item, "isBroken") || IsZeroPercent(GetString(item, "durability"));
        var isSoulRelic = IsSoulRelic(item);
        var isEquippable = !isSoulRelic &&
                            !isBroken &&
                            !string.IsNullOrWhiteSpace(resolvedSlot);

        return new InventoryEquipmentItem(
            Identity: identity,
            Name: name,
            Type: type,
            ItemSlot: itemSlot,
            ResolvedSlot: resolvedSlot,
            IsBroken: isBroken,
            IsSoulRelic: isSoulRelic,
            IsEquippable: isEquippable,
            EquippedSlot: string.Empty);
    }

    private static List<EquippedInventoryItem> ReadEquipped(
        JsonObject root,
        IReadOnlyList<InventoryEquipmentItem> items)
    {
        var result = new List<EquippedInventoryItem>();
        var equipment = root["equipment"] as JsonObject ?? root["equippedItems"] as JsonObject;
        if (equipment == null)
            return result;

        foreach (var prop in equipment)
        {
            if (prop.Value == null || prop.Value.GetValueKind() == JsonValueKind.Null)
                continue;

            var referenceIdentity = ReadEquipmentReferenceIdentity(prop.Value);
            var referenceName = ReadEquipmentReferenceName(prop.Value);
            var matched = FindMatchingItem(items, referenceIdentity, referenceName);
            var itemName = matched?.Name ??
                           FirstNonEmpty(referenceName, referenceIdentity, "???");
            var itemIdentity = matched?.Identity ?? referenceIdentity;
            result.Add(new EquippedInventoryItem(
                SlotKey: prop.Key,
                SlotLabel: FormatSlotName(prop.Key),
                ItemIdentity: itemIdentity,
                ItemName: itemName,
                IsOrdinaryInventoryItem: matched == null || !matched.IsSoulRelic));
        }

        return result;
    }

    private static InventoryEquipmentItem? FindMatchingItem(
        IEnumerable<InventoryEquipmentItem> items,
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

        if (slotData is not JsonObject obj)
            return string.Empty;

        return FirstNonEmpty(GetString(obj, "name"), GetString(obj, "itemName"));
    }

    private static JsonArray? GetPlayerInventoryArrayNode(JsonObject root)
    {
        if (root["items"] is JsonArray items)
            return items;

        if (root["UpdateInventory"] is JsonArray updateInventory)
            return updateInventory;

        return null;
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

    private static bool ReadBool(JsonObject obj, string propertyName) =>
        obj[propertyName] is JsonValue value &&
        value.TryGetValue<bool>(out var parsed) &&
        parsed;

    private static bool IsZeroPercent(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return int.TryParse(value.Replace("%", string.Empty, StringComparison.Ordinal).Trim(), out var parsed) &&
               parsed == 0;
    }

    private static bool IsSoulRelic(JsonObject item)
        => !string.IsNullOrWhiteSpace(GetString(item, "relicId"));

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed record InventoryEquipmentContext(
    JsonObject Root,
    IReadOnlyList<InventoryEquipmentItem> Items,
    IReadOnlyList<EquippedInventoryItem> Equipped);

public sealed record InventoryEquipmentItem(
    string Identity,
    string Name,
    string Type,
    string ItemSlot,
    string ResolvedSlot,
    bool IsBroken,
    bool IsSoulRelic,
    bool IsEquippable,
    string EquippedSlot);

public sealed record EquippedInventoryItem(
    string SlotKey,
    string SlotLabel,
    string ItemIdentity,
    string ItemName,
    bool IsOrdinaryInventoryItem);

public sealed record InventoryEquipmentWriteOutcome(
    bool Success,
    string Message,
    string ItemIdentity,
    string ItemName,
    string SlotKey,
    string SlotLabel)
{
    public static InventoryEquipmentWriteOutcome Completed(
        string message,
        string itemIdentity,
        string itemName,
        string slotKey,
        string slotLabel) =>
        new(true, message, itemIdentity, itemName, slotKey, slotLabel);

    public static InventoryEquipmentWriteOutcome Failed(string message) =>
        new(false, message, string.Empty, string.Empty, string.Empty, string.Empty);

    public JsonObject ToPayload() =>
        new()
        {
            ["itemIdentity"] = ItemIdentity,
            ["itemName"] = ItemName,
            ["slotKey"] = SlotKey,
            ["slotLabel"] = SlotLabel
        };
}
