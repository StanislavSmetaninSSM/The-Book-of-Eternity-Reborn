using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public static class SoulRelicEquipmentService
{
    public const string SoulStatePath = "game_state/meta/soul_state.json";

    public static IReadOnlyList<string> AllowedSlots { get; } = new[]
    {
        "head",
        "body",
        "mainHand",
        "offHand",
        "soulAnchor"
    };

    public static IReadOnlyDictionary<string, string> SlotLabels { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["head"] = "Голова",
        ["body"] = "Тело",
        ["mainHand"] = "Основная рука",
        ["offHand"] = "Вспомогательная рука",
        ["soulAnchor"] = "Якорь души"
    };

    public static Task<SoulRelicEquipmentContext?> ReadContextAsync(FileSystemManager fs) =>
        ReadContextCoreAsync(fs, writeLease: null);

    internal static Task<SoulRelicEquipmentContext?> ReadContextAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        ReadContextCoreAsync(fs, writeLease);

    private static async Task<SoulRelicEquipmentContext?> ReadContextCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease)
    {
        var raw = writeLease == null
            ? await fs.ReadFileAsync(SoulStatePath)
            : await fs.ReadFileAsync(writeLease, SoulStatePath);
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

        NormalizeLegacyFlatSoulRelics(root);
        var soulRelics = root["soulRelics"] as JsonObject;
        if (soulRelics == null)
        {
            return new SoulRelicEquipmentContext(root, [], []);
        }

        var stored = ReadRelics(soulRelics["stored"] as JsonArray);
        var equipped = ReadRelics(soulRelics["equipped"] as JsonArray);
        return new SoulRelicEquipmentContext(root, stored, equipped);
    }

    public static Task<SoulRelicWriteOutcome> EquipAsync(
        FileSystemManager fs,
        string relicIdOrName,
        string slotKey) =>
        EquipCoreAsync(fs, writeLease: null, relicIdOrName, slotKey);

    internal static Task<SoulRelicWriteOutcome> EquipAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string relicIdOrName,
        string slotKey) =>
        EquipCoreAsync(fs, writeLease, relicIdOrName, slotKey);

    private static async Task<SoulRelicWriteOutcome> EquipCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string relicIdOrName,
        string slotKey)
    {
        var context = await ReadContextCoreAsync(fs, writeLease);
        if (context == null)
            return SoulRelicWriteOutcome.Failed("soul_state.json отсутствует или повреждён.");

        var outcome = ValidateEquip(context, relicIdOrName, slotKey);
        if (!outcome.Success)
            return outcome;

        var soulRelics = EnsureSoulRelicsObject(context.Root);
        var stored = EnsureArray(soulRelics, "stored");
        var equipped = EnsureArray(soulRelics, "equipped");

        var relic = FindRelicIn(stored, relicIdOrName);
        if (relic == null)
            return SoulRelicWriteOutcome.Failed("Реликвия не найдена в хранилище.");

        stored.Remove(relic);
        EnsureGameplayStatus(relic);
        relic["gameplayStatus"]!["equipped"] = true;
        relic["gameplayStatus"]!["currentSlot"] = string.IsNullOrWhiteSpace(outcome.SlotKey) ? "Default" : outcome.SlotKey;
        equipped.Add(relic);

        if (writeLease == null)
        {
            await fs.WriteFileAtomicAsync(
                SoulStatePath,
                GuardianPolicyContracts.CreatePatchedSoulStateWriteRoot(
                        context.Root,
                        GuardianPolicyContracts.SoulStatePatchConflictContext.None)
                    .ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        }
        else
        {
            await fs.WriteFileAtomicAsync(
                writeLease,
                SoulStatePath,
                GuardianPolicyContracts.CreatePatchedSoulStateWriteRoot(
                        context.Root,
                        GuardianPolicyContracts.SoulStatePatchConflictContext.None)
                    .ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        }
        return outcome;
    }

    public static Task<SoulRelicWriteOutcome> UnequipAsync(
        FileSystemManager fs,
        string slotKey) =>
        UnequipCoreAsync(fs, writeLease: null, slotKey);

    internal static Task<SoulRelicWriteOutcome> UnequipAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string slotKey) =>
        UnequipCoreAsync(fs, writeLease, slotKey);

    private static async Task<SoulRelicWriteOutcome> UnequipCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string slotKey)
    {
        var context = await ReadContextCoreAsync(fs, writeLease);
        if (context == null)
            return SoulRelicWriteOutcome.Failed("soul_state.json отсутствует или повреждён.");
        var validation = ValidateUnequip(context, slotKey);
        if (!validation.Success) return validation;

        var equipped = EnsureArray(context.Root["soulRelics"]!.AsObject(), "equipped");
        var stored = EnsureArray(context.Root["soulRelics"]!.AsObject(), "stored");
        var relicNode = FindRelicInEquippedSlot(equipped, validation.SlotKey);
        if (relicNode == null)
            return SoulRelicWriteOutcome.Failed("Реликвия не найдена в экипировке.");
        var gameplayStatus = relicNode["gameplayStatus"] as JsonObject;
        if (gameplayStatus != null)
        {
            gameplayStatus["equipped"] = false;
            gameplayStatus["currentSlot"] = string.Empty;
        }
        stored.Add(relicNode.DeepClone()!.AsObject());
        equipped.Remove(relicNode);
        if (writeLease == null)
        {
            await fs.WriteFileAtomicAsync(
                SoulStatePath,
                GuardianPolicyContracts.CreatePatchedSoulStateWriteRoot(
                        context.Root,
                        GuardianPolicyContracts.SoulStatePatchConflictContext.None)
                    .ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        }
        else
        {
            await fs.WriteFileAtomicAsync(
                writeLease,
                SoulStatePath,
                GuardianPolicyContracts.CreatePatchedSoulStateWriteRoot(
                        context.Root,
                        GuardianPolicyContracts.SoulStatePatchConflictContext.None)
                    .ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        }
        return validation;
    }

    public static async Task<SoulRelicWriteOutcome> ValidateEquipAsync(
        FileSystemManager fs,
        string relicIdOrName,
        string slotKey)
    {
        var context = await ReadContextAsync(fs);
        if (context == null)
            return SoulRelicWriteOutcome.Failed("soul_state.json отсутствует или повреждён.");
        return ValidateEquip(context, relicIdOrName, slotKey);
    }

    public static async Task<SoulRelicWriteOutcome> ValidateUnequipAsync(
        FileSystemManager fs,
        string slotKey)
    {
        var context = await ReadContextAsync(fs);
        if (context == null)
            return SoulRelicWriteOutcome.Failed("soul_state.json отсутствует или повреждён.");
        return ValidateUnequip(context, slotKey);
    }

    public static string? ResolveRelicIdentity(JsonObject? relic)
    {
        if (relic == null) return null;
        return FirstNonEmpty(
            GetString(relic, "relicId"),
            GetString(relic, "id"));
    }

    public static string? ResolveRelicName(JsonObject? relic)
    {
        if (relic == null) return null;
        return FirstNonEmpty(
            GetString(relic, "name"),
            GetString(relic, "itemName"));
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
            if (ch == '"') break;
            builder.Append(ch);
        }
        return builder.ToString().Trim();
    }

    public static string FormatCommandArgument(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return "\"\"";
        if (trimmed.All(static ch => !char.IsWhiteSpace(ch) && ch != '"' && ch != '\\'))
            return trimmed;
        return "\"" + trimmed.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    public static string BuildActionId(string prefix, string identityOrName)
    {
        var source = string.IsNullOrWhiteSpace(identityOrName) ? "relic" : identityOrName.Trim();
        var chars = source
            .Select(static ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' ? ch : '_')
            .ToArray();
        var slug = new string(chars).Trim('_');
        if (string.IsNullOrWhiteSpace(slug)) slug = "relic";
        return $"{prefix}-{slug}";
    }

    public static bool TryNormalizeSlot(string slotKey, out string normalizedSlot)
    {
        normalizedSlot = string.Empty;
        if (string.IsNullOrWhiteSpace(slotKey)) return false;
        var trimmed = slotKey.Trim();
        var direct = AllowedSlots.FirstOrDefault(key =>
            string.Equals(key, trimmed, StringComparison.OrdinalIgnoreCase));
        if (direct == null) return false;
        normalizedSlot = direct;
        return true;
    }

    internal static bool NormalizeLegacyFlatSoulRelics(JsonObject root)
    {
        if (root["soulRelics"] is not JsonArray flatRelics)
            return false;

        var equipped = new JsonArray();
        var stored = new JsonArray();

        foreach (var relicNode in flatRelics)
        {
            if (relicNode is not JsonObject relicObject)
                continue;

            var clone = relicObject.DeepClone();
            if (IsLegacyFlatRelicEquipped(clone))
                equipped.Add(clone);
            else
                stored.Add(clone);
        }

        root["soulRelics"] = new JsonObject
        {
            ["equipped"] = equipped,
            ["stored"] = stored
        };
        return true;
    }

    private static bool IsLegacyFlatRelicEquipped(JsonNode relicNode)
    {
        if (relicNode["gameplayStatus"] is JsonObject gameplayStatus &&
            gameplayStatus["equipped"] is JsonValue equippedValue &&
            equippedValue.TryGetValue<bool>(out var equipped))
        {
            return equipped;
        }
        return false;
    }

    private static SoulRelicWriteOutcome ValidateEquip(
        SoulRelicEquipmentContext context,
        string relicIdOrName,
        string slotKey)
    {
        if (string.IsNullOrWhiteSpace(relicIdOrName))
            return SoulRelicWriteOutcome.Failed("Укажите реликвию для экипировки.");

        var alreadyEquipped = FindRelicInArray(context.Equipped, relicIdOrName);
        if (alreadyEquipped != null)
            return SoulRelicWriteOutcome.Failed($"Реликвия «{alreadyEquipped.Name}» уже экипирована.");

        var storedRelic = FindRelicInArray(context.Stored, relicIdOrName);
        if (storedRelic == null)
            return SoulRelicWriteOutcome.Failed($"Реликвия «{relicIdOrName}» не найдена в хранилище.");

        if (!TryNormalizeEquipSlot(slotKey, storedRelic, out var normalizedSlot))
            return SoulRelicWriteOutcome.Failed("Выберите корректный слот для реликвии.");

        if (storedRelic.CompatibleSlots.Count > 0 &&
            !storedRelic.CompatibleSlots.Any(slot => SlotsEqual(slot, normalizedSlot)))
        {
            return SoulRelicWriteOutcome.Failed(
                $"Реликвия «{storedRelic.Name}» подходит только для слота: {FormatSlotList(storedRelic.CompatibleSlots)}.");
        }

        var occupyingRelic = context.Equipped.FirstOrDefault(item => SlotsEqual(item.CurrentSlot, normalizedSlot));
        if (occupyingRelic != null)
            return SoulRelicWriteOutcome.Failed($"Слот {FormatSlotLabel(normalizedSlot)} уже занят реликвией «{occupyingRelic.Name}».");

        return SoulRelicWriteOutcome.Completed(
            $"Реликвия «{storedRelic.Name}» экипирована в слот {FormatSlotLabel(normalizedSlot)}.",
            storedRelic.RelicId,
            storedRelic.Name,
            normalizedSlot);
    }

    private static SoulRelicWriteOutcome ValidateUnequip(
        SoulRelicEquipmentContext context,
        string slotKey)
    {
        if (string.IsNullOrWhiteSpace(slotKey))
            return SoulRelicWriteOutcome.Failed("Выберите корректный слот для снятия реликвии.");

        var normalizedSlot = NormalizeSlotReference(slotKey);
        var equippedRelic = FindRelicInEquippedBySlot(context.Equipped, normalizedSlot);
        if (equippedRelic == null)
            return SoulRelicWriteOutcome.Failed($"В слоте {FormatSlotLabel(normalizedSlot)} нет экипированной реликвии.");

        return SoulRelicWriteOutcome.Completed(
            $"Реликвия «{equippedRelic.Name}» снята и убрана в хранилище.",
            equippedRelic.RelicId,
            equippedRelic.Name,
            normalizedSlot);
    }

    private static List<SoulRelicItem> ReadRelics(JsonArray? array)
    {
        if (array == null) return new List<SoulRelicItem>();
        return array
            .OfType<JsonObject>()
            .Select(ReadRelic)
            .Where(item => !string.IsNullOrWhiteSpace(item.RelicId) || !string.IsNullOrWhiteSpace(item.Name))
            .ToList();
    }

    private static SoulRelicItem ReadRelic(JsonObject relic)
    {
        var relicId = FirstNonEmpty(GetString(relic, "relicId"), GetString(relic, "id"));
        var name = FirstNonEmpty(GetString(relic, "name"), GetString(relic, "itemName"), "Безымянная реликвия");
        var rarity = FirstNonEmpty(
            GetString(relic, "rarity"),
            GetString(relic, "quality"),
            GetString(relic, "relicRarity"));
        var gameplayStatus = relic["gameplayStatus"] as JsonObject;
        var isEquipped = false;
        var currentSlot = string.Empty;
        if (gameplayStatus != null)
        {
            if (gameplayStatus["equipped"] is JsonValue eqVal && eqVal.TryGetValue<bool>(out var eq))
                isEquipped = eq;
            if (gameplayStatus["currentSlot"] is JsonValue slotVal)
            {
                if (slotVal.TryGetValue<string>(out var slotText) && !string.IsNullOrWhiteSpace(slotText))
                    currentSlot = slotText;
            }
        }
        var compatibleSlots = ReadCompatibleSlots(relic);
        if (string.IsNullOrWhiteSpace(currentSlot) && isEquipped)
        {
            currentSlot = FirstNonEmpty(
                GetString(relic, "currentSlot"),
                compatibleSlots.FirstOrDefault() ?? string.Empty);
        }
        return new SoulRelicItem(relicId, name, rarity, isEquipped, NormalizeSlotReference(currentSlot), compatibleSlots);
    }

    private static JsonObject? FindRelicIn(JsonArray array, string relicIdOrName)
    {
        return array.OfType<JsonObject>().FirstOrDefault(node => RelicNodeMatches(node, relicIdOrName));
    }

    private static SoulRelicItem? FindRelicInArray(IEnumerable<SoulRelicItem> items, string relicIdOrName)
    {
        if (string.IsNullOrWhiteSpace(relicIdOrName)) return null;
        var trimmed = relicIdOrName.Trim();
        return items.FirstOrDefault(item =>
            (!string.IsNullOrWhiteSpace(item.RelicId) &&
             string.Equals(item.RelicId, trimmed, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(item.Name) &&
             string.Equals(item.Name, trimmed, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool RelicNodeMatches(JsonObject relic, string relicIdOrName)
    {
        if (string.IsNullOrWhiteSpace(relicIdOrName)) return false;
        var trimmed = relicIdOrName.Trim();
        var nodeId = FirstNonEmpty(GetString(relic, "relicId"), GetString(relic, "id"));
        if (!string.IsNullOrWhiteSpace(nodeId) &&
            string.Equals(nodeId, trimmed, StringComparison.OrdinalIgnoreCase))
            return true;
        var nodeName = FirstNonEmpty(GetString(relic, "name"), GetString(relic, "itemName"));
        return !string.IsNullOrWhiteSpace(nodeName) &&
               string.Equals(nodeName, trimmed, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject? FindRelicInEquippedSlot(JsonArray array, string normalizedSlot)
    {
        return array.OfType<JsonObject>().FirstOrDefault(node => SlotsEqual(ReadRelic(node).CurrentSlot, normalizedSlot));
    }

    private static SoulRelicItem? FindRelicInEquippedBySlot(IEnumerable<SoulRelicItem> items, string normalizedSlot)
    {
        return items.FirstOrDefault(item =>
            SlotsEqual(item.CurrentSlot, normalizedSlot));
    }

    private static bool TryNormalizeEquipSlot(
        string slotKey,
        SoulRelicItem relic,
        out string normalizedSlot)
    {
        normalizedSlot = string.Empty;
        if (string.IsNullOrWhiteSpace(slotKey))
            return false;

        var requested = NormalizeSlotReference(slotKey);
        if (AllowedSlots.Any(slot => SlotsEqual(slot, requested)) ||
            relic.CompatibleSlots.Any(slot => SlotsEqual(slot, requested)))
        {
            normalizedSlot = requested;
            return true;
        }

        return false;
    }

    private static IReadOnlyList<string> ReadCompatibleSlots(JsonObject relic)
    {
        var slots = new List<string>();
        AddSlotValues(slots, relic["slot"]);
        AddSlotValues(slots, relic["equipmentSlot"]);
        AddSlotValues(slots, relic["equipSlot"]);
        AddSlotValues(slots, relic["compatibleSlots"]);
        AddSlotValues(slots, relic["allowedSlots"]);

        if (relic["equipmentData"] is JsonObject equipmentData)
        {
            AddSlotValues(slots, equipmentData["slot"]);
            AddSlotValues(slots, equipmentData["equipmentSlot"]);
            AddSlotValues(slots, equipmentData["equipSlot"]);
            AddSlotValues(slots, equipmentData["compatibleSlots"]);
            AddSlotValues(slots, equipmentData["allowedSlots"]);
        }

        return slots
            .Select(NormalizeSlotReference)
            .Where(static slot => !string.IsNullOrWhiteSpace(slot))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddSlotValues(List<string> slots, JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                slots.Add(text);
            return;
        }

        if (node is not JsonArray array)
            return;

        foreach (var entry in array)
        {
            if (entry is JsonValue entryValue &&
                entryValue.TryGetValue<string>(out var text) &&
                !string.IsNullOrWhiteSpace(text))
            {
                slots.Add(text);
            }
        }
    }

    private static bool SlotsEqual(string left, string right) =>
        string.Equals(NormalizeSlotReference(left), NormalizeSlotReference(right), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeSlotReference(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot))
            return string.Empty;

        var trimmed = slot.Trim();
        var direct = AllowedSlots.FirstOrDefault(key =>
            string.Equals(key, trimmed, StringComparison.OrdinalIgnoreCase));
        return direct ?? trimmed;
    }

    public static string FormatSlotLabel(string slot) =>
        SlotLabels.TryGetValue(NormalizeSlotReference(slot), out var label)
            ? label
            : NormalizeSlotReference(slot);

    private static string FormatSlotList(IEnumerable<string> slots) =>
        string.Join(", ", slots.Select(FormatSlotLabel));

    private static JsonObject EnsureSoulRelicsObject(JsonObject root)
    {
        if (root["soulRelics"] is JsonObject existing) return existing;
        var created = new JsonObject();
        root["soulRelics"] = created;
        return created;
    }

    private static JsonArray EnsureArray(JsonObject parent, string property)
    {
        if (parent[property] is JsonArray existing) return existing;
        var created = new JsonArray();
        parent[property] = created;
        return created;
    }

    private static void EnsureGameplayStatus(JsonObject relic)
    {
        if (relic["gameplayStatus"] is JsonObject existing) return;
        relic["gameplayStatus"] = new JsonObject();
    }

    private static string GetString(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is not JsonValue value)
            return string.Empty;
        if (value.TryGetValue<string>(out var text)) return text ?? string.Empty;
        return string.Empty;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(static v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}

public sealed record SoulRelicEquipmentContext(
    JsonObject Root,
    IReadOnlyList<SoulRelicItem> Stored,
    IReadOnlyList<SoulRelicItem> Equipped);

public sealed record SoulRelicItem(
    string RelicId,
    string Name,
    string Rarity,
    bool IsEquipped,
    string CurrentSlot,
    IReadOnlyList<string> CompatibleSlots);

public sealed record SoulRelicWriteOutcome(
    bool Success,
    string Message,
    string RelicId,
    string RelicName,
    string SlotKey)
{
    public static SoulRelicWriteOutcome Completed(
        string message,
        string relicId,
        string relicName,
        string slotKey) =>
        new(true, message, relicId, relicName, slotKey);

    public static SoulRelicWriteOutcome Failed(string message) =>
        new(false, message, string.Empty, string.Empty, string.Empty);

    public JsonObject ToPayload() =>
        new()
        {
            ["relicId"] = RelicId,
            ["relicName"] = RelicName,
            ["slotKey"] = SlotKey
        };
}
