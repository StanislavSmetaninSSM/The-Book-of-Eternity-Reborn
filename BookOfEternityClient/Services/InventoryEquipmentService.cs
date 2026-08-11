using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public static class InventoryEquipmentService
{
    public const string ItemsPath = "game_state/inventory/items.json";

    private static readonly IReadOnlyList<string> UniversalAccessorySlots =
        ["Accessory1", "Accessory2", "Accessory3", "Accessory4"];

    public static readonly IReadOnlyDictionary<string, string> SlotLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Head"] = "🪖 Голова",
            ["Chest"] = "🛡️ Грудь",
            ["Legs"] = "🦵 Ноги",
            ["Feet"] = "👢 Ступни",
            ["Hands"] = "🧤 Кисти",
            ["Wrists"] = "⌚ Запястья",
            ["Neck"] = "📿 Шея",
            ["Waist"] = "🪢 Пояс",
            ["Back"] = "🎒 Спина",
            ["Finger1"] = "💍 Палец 1",
            ["Finger2"] = "💍 Палец 2",
            ["MainHand"] = "⚔️ Основная рука",
            ["OffHand"] = "🛡️ Вторая рука",
            ["Underwear_Top"] = "👕 Нижнее бельё (верх)",
            ["Underwear_Bottom"] = "🩳 Нижнее бельё (низ)",
            ["Accessory1"] = "🔹 Аксессуар 1",
            ["Accessory2"] = "🔹 Аксессуар 2",
            ["Accessory3"] = "🔹 Аксессуар 3",
            ["Accessory4"] = "🔹 Аксессуар 4"
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
        if (!MortalItemEquipmentAuthority.TryRead(
                root,
                root["items"] as JsonArray,
                ItemsPath,
                out var equipmentState,
                out _))
        {
            return null;
        }
        var equipped = ReadEquipped(equipmentState, items);
        var equippedSlotsByItem = equipped
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.ItemIdentity))
            .GroupBy(static entry => entry.ItemIdentity, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First().SlotKey, StringComparer.Ordinal);
        var occupiedSlots = ReadOccupiedSlots(equipmentState);

        var enrichedItems = items
            .Select(item =>
            {
                var equippedSlot = string.Empty;
                if (!string.IsNullOrWhiteSpace(item.Identity))
                    equippedSlotsByItem.TryGetValue(item.Identity, out equippedSlot);
                var resolvedSlots = item.IsAccessory && string.IsNullOrWhiteSpace(equippedSlot)
                    ? item.ResolvedSlots
                        .Where(slot => !occupiedSlots.Contains(slot))
                        .ToArray()
                    : item.ResolvedSlots;
                var isEquippable = !item.IsSoulRelic &&
                                    !item.IsBroken &&
                                    item.IsCarriedByPlayer &&
                                    resolvedSlots.Count > 0 &&
                                    (!item.RequiresTwoHands ||
                                     resolvedSlots.Count == 2 &&
                                     resolvedSlots.Contains("MainHand", StringComparer.Ordinal) &&
                                     resolvedSlots.Contains("OffHand", StringComparer.Ordinal));
                return item with
                {
                    ResolvedSlot = resolvedSlots.FirstOrDefault() ?? string.Empty,
                    ResolvedSlots = resolvedSlots,
                    IsEquippable = isEquippable,
                    EquippedSlot = equippedSlot ?? string.Empty
                };
            })
            .ToArray();

        equipped = ReadEquipped(equipmentState, enrichedItems);
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

        if (context.Root["equippedItems"] is not JsonObject equipment)
        {
            equipment = new JsonObject();
            context.Root["equippedItems"] = equipment;
        }

        var item = FindItem(context.Items, itemIdentityOrName)!;
        foreach (var targetSlot in outcome.AffectedSlots)
            equipment[FindStoredSlotKey(equipment, targetSlot) ?? targetSlot] = item.Identity;
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

        if (context.Root["equippedItems"] is not JsonObject equipment)
            return InventoryEquipmentWriteOutcome.Failed("Экипировка не найдена.");

        foreach (var affectedSlot in outcome.AffectedSlots)
            equipment[affectedSlot] = null;
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

        if (!string.Equals(identityOrName, identityOrName.Trim(), StringComparison.Ordinal))
            return null;

        return items.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(item.Identity) &&
            string.Equals(item.Identity, identityOrName, StringComparison.Ordinal));
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

    public static IReadOnlyList<string> ReadCanonicalSlots(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var scalar))
            return NormalizeSlots([scalar]);

        if (node is not JsonArray array)
            return [];

        return NormalizeSlots(array
            .OfType<JsonValue>()
            .Select(static candidate => candidate.TryGetValue<string>(out var slot) ? slot : null));
    }

    public static IReadOnlyList<string> ReadCanonicalSlots(JsonElement owner, string propertyName)
    {
        if (!owner.TryGetProperty(propertyName, out var node))
            return [];

        if (node.ValueKind == JsonValueKind.String)
            return NormalizeSlots([node.GetString()]);

        if (node.ValueKind != JsonValueKind.Array)
            return [];

        return NormalizeSlots(node.EnumerateArray()
            .Select(static candidate => candidate.ValueKind == JsonValueKind.String ? candidate.GetString() : null));
    }

    public static string FormatSlotNames(IEnumerable<string> slots) =>
        string.Join(", ", slots.Select(FormatSlotName));

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

        if (!item.IsCarriedByPlayer)
            return InventoryEquipmentWriteOutcome.Failed($"«{item.Name}» находится вне рюкзака и не может быть экипирован.");

        if (item.IsAccessory && item.ResolvedSlots.Count == 0)
            return InventoryEquipmentWriteOutcome.Failed("Все универсальные слоты аксессуаров уже заняты.");

        if (!item.IsEquippable)
            return InventoryEquipmentWriteOutcome.Failed($"«{item.Name}» нельзя экипировать как обычный предмет.");

        if (!string.IsNullOrWhiteSpace(item.EquippedSlot))
            return InventoryEquipmentWriteOutcome.Failed($"«{item.Name}» уже экипирован: {FormatSlotName(item.EquippedSlot)}.");

        if (!TryNormalizeSlot(slotKey, out var normalizedSlot))
            return InventoryEquipmentWriteOutcome.Failed("Выберите корректный слот экипировки.");

        if (!item.ResolvedSlots.Contains(normalizedSlot, StringComparer.Ordinal))
        {
            return InventoryEquipmentWriteOutcome.Failed(
                $"«{item.Name}» подходит только для слотов: {FormatSlotNames(item.ResolvedSlots)}.");
        }

        var affectedSlots = item.RequiresTwoHands
            ? item.ResolvedSlots
            : [normalizedSlot];
        var slotLabel = item.RequiresTwoHands
            ? FormatSlotNames(affectedSlots)
            : FormatSlotName(normalizedSlot);
        return InventoryEquipmentWriteOutcome.Completed(
            item.RequiresTwoHands
                ? $"«{item.Name}» экипирован и занимает обе руки: {slotLabel}."
                : $"«{item.Name}» экипирован: {slotLabel}.",
            item.Identity,
            item.Name,
            normalizedSlot,
            slotLabel,
            affectedSlots);
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

        var affectedSlots = context.Equipped
            .Where(entry => string.Equals(entry.ItemIdentity, equipped.ItemIdentity, StringComparison.Ordinal))
            .Select(static entry => entry.SlotKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return InventoryEquipmentWriteOutcome.Completed(
            $"«{equipped.ItemName}» снят и убран в рюкзак.",
            equipped.ItemIdentity,
            equipped.ItemName,
            equipped.SlotKey,
            equipped.SlotLabel,
            affectedSlots);
    }

    private static InventoryEquipmentItem[] ReadItems(JsonObject root)
    {
        var array = GetPlayerInventoryArrayNode(root);
        if (array == null)
            return [];

        return array
            .OfType<JsonObject>()
            .Select(static item =>
                MortalItemMaterializationContract.TryReadAcceptedIdentity(item, out var identity)
                    ? ReadItem(item, identity)
                    : null)
            .OfType<InventoryEquipmentItem>()
            .Where(static item => !string.IsNullOrWhiteSpace(item.Name))
            .ToArray();
    }

    private static InventoryEquipmentItem ReadItem(JsonObject item, string identity)
    {
        var name = FirstNonEmpty(GetString(item, "name"), GetString(item, "itemName"), "???");
        var type = GetString(item, "type");
        var equipmentSlots = ReadCanonicalSlots(item["equipmentSlot"]);
        var accessoryForSlots = ReadCanonicalSlots(item["accessoryForSlot"]);
        var isAccessory = accessoryForSlots.Count > 0;
        var resolvedSlots = isAccessory ? UniversalAccessorySlots : equipmentSlots;
        var resolvedSlot = resolvedSlots.FirstOrDefault() ?? string.Empty;
        var requiresTwoHands = ReadBool(item, "requiresTwoHands");
        var hasValidTwoHandProfile = !requiresTwoHands ||
                                     equipmentSlots.Count == 2 &&
                                     equipmentSlots.Contains("MainHand", StringComparer.Ordinal) &&
                                     equipmentSlots.Contains("OffHand", StringComparer.Ordinal);
        var isBroken = ReadBool(item, "isBroken") || IsZeroPercent(GetString(item, "durability"));
        var isSoulRelic = IsSoulRelic(item);
        var isCarriedByPlayer = MortalItemLocalActionPolicy.IsCarriedByPlayer(item);
        var isEquippable = !isSoulRelic &&
                            !isBroken &&
                            isCarriedByPlayer &&
                            resolvedSlots.Count > 0 &&
                            hasValidTwoHandProfile;

        return new InventoryEquipmentItem(
            Identity: identity,
            Name: name,
            Type: type,
            ItemSlot: resolvedSlot,
            ResolvedSlot: resolvedSlot,
            ResolvedSlots: resolvedSlots,
            AccessoryForSlots: accessoryForSlots,
            IsAccessory: isAccessory,
            RequiresTwoHands: requiresTwoHands,
            IsBroken: isBroken,
            IsSoulRelic: isSoulRelic,
            IsCarriedByPlayer: isCarriedByPlayer,
            IsEquippable: isEquippable,
            EquippedSlot: string.Empty);
    }

    private static List<EquippedInventoryItem> ReadEquipped(
        MortalItemEquipmentSnapshot equipmentState,
        IReadOnlyList<InventoryEquipmentItem> items)
    {
        var result = new List<EquippedInventoryItem>();
        foreach (var slot in equipmentState.Slots)
        {
            if (slot.ItemId == null)
                continue;

            var matched = items.FirstOrDefault(item =>
                string.Equals(item.Identity, slot.ItemId, StringComparison.Ordinal));
            if (matched == null)
                continue;

            result.Add(new EquippedInventoryItem(
                SlotKey: slot.StoredSlot,
                SlotLabel: FormatSlotName(slot.CanonicalSlot),
                ItemIdentity: matched.Identity,
                ItemName: matched.Name,
                IsOrdinaryInventoryItem: !matched.IsSoulRelic));
        }

        return result;
    }

    private static JsonArray? GetPlayerInventoryArrayNode(JsonObject root)
    {
        if (root["items"] is JsonArray items)
            return items;

        return null;
    }

    private static HashSet<string> ReadOccupiedSlots(MortalItemEquipmentSnapshot equipmentState)
    {
        return equipmentState.Slots
            .Where(static slot => slot.ItemId != null)
            .Select(static slot => slot.CanonicalSlot)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string? FindStoredSlotKey(JsonObject equipment, string canonicalSlot) =>
        equipment
            .Select(static property => property.Key)
            .SingleOrDefault(slot =>
                TryNormalizeSlot(slot, out var normalized) &&
                string.Equals(normalized, canonicalSlot, StringComparison.Ordinal));

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

    private static IReadOnlyList<string> NormalizeSlots(IEnumerable<string?> candidates)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (candidate == null || !TryNormalizeSlot(candidate, out var normalized) || !seen.Add(normalized))
                continue;

            result.Add(normalized);
        }

        return result;
    }

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
    IReadOnlyList<string> ResolvedSlots,
    IReadOnlyList<string> AccessoryForSlots,
    bool IsAccessory,
    bool RequiresTwoHands,
    bool IsBroken,
    bool IsSoulRelic,
    bool IsCarriedByPlayer,
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
    string SlotLabel,
    IReadOnlyList<string> AffectedSlots)
{
    public static InventoryEquipmentWriteOutcome Completed(
        string message,
        string itemIdentity,
        string itemName,
        string slotKey,
        string slotLabel,
        IReadOnlyList<string> affectedSlots) =>
        new(true, message, itemIdentity, itemName, slotKey, slotLabel, affectedSlots);

    public static InventoryEquipmentWriteOutcome Failed(string message) =>
        new(false, message, string.Empty, string.Empty, string.Empty, string.Empty, []);

    public JsonObject ToPayload() =>
        new()
        {
            ["itemIdentity"] = ItemIdentity,
            ["itemName"] = ItemName,
            ["slotKey"] = SlotKey,
            ["slotLabel"] = SlotLabel
        };
}
