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

    public static async Task<SoulRelicEquipmentContext?> ReadContextAsync(FileSystemManager fs)
    {
        var raw = await fs.ReadFileAsync(SoulStatePath);
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

    public static async Task<SoulRelicWriteOutcome> EquipAsync(
        FileSystemManager fs,
        string relicIdOrName,
        string slotKey)
    {
        var context = await ReadContextAsync(fs);
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

        await fs.WriteFileAtomicAsync(
            SoulStatePath,
            context.Root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        return outcome;
    }

    public static async Task<SoulRelicWriteOutcome> UnequipAsync(
        FileSystemManager fs,
        string relicIdOrName)
    {
        var context = await ReadContextAsync(fs);
        if (context == null)
            return SoulRelicWriteOutcome.Failed("soul_state.json отсутствует или повреждён.");

        var outcome = ValidateUnequip(context, relicIdOrName);
        if (!outcome.Success)
            return outcome;

        var soulRelics = EnsureSoulRelicsObject(context.Root);
        var stored = EnsureArray(soulRelics, "stored");
        var equipped = EnsureArray(soulRelics, "equipped");

        var relic = FindRelicIn(equipped, relicIdOrName);
        if (relic == null)
            return SoulRelicWriteOutcome.Failed("Реликвия не найдена в экипировке.");

        equipped.Remove(relic);
        EnsureGameplayStatus(relic);
        relic["gameplayStatus"]!["equipped"] = false;
        relic["gameplayStatus"]!["currentSlot"] = string.Empty;
        stored.Add(relic);

        await fs.WriteFileAtomicAsync(
            SoulStatePath,
            context.Root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        return outcome;
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
        string relicIdOrName)
    {
        var context = await ReadContextAsync(fs);
        if (context == null)
            return SoulRelicWriteOutcome.Failed("soul_state.json отсутствует или повреждён.");
        return ValidateUnequip(context, relicIdOrName);
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

        if (!TryNormalizeSlot(slotKey, out var normalizedSlot))
            return SoulRelicWriteOutcome.Failed("Выберите корректный слот для реликвии.");

        var alreadyEquipped = FindRelicInArray(context.Equipped, relicIdOrName);
        if (alreadyEquipped != null)
            return SoulRelicWriteOutcome.Failed($"Реликвия «{alreadyEquipped.Name}» уже экипирована.");

        var storedRelic = FindRelicInArray(context.Stored, relicIdOrName);
        if (storedRelic == null)
            return SoulRelicWriteOutcome.Failed($"Реликвия «{relicIdOrName}» не найдена в хранилище.");

        return SoulRelicWriteOutcome.Completed(
            $"Реликвия «{storedRelic.Name}» экипирована в слот {normalizedSlot}.",
            storedRelic.RelicId,
            storedRelic.Name,
            normalizedSlot);
    }

    private static SoulRelicWriteOutcome ValidateUnequip(
        SoulRelicEquipmentContext context,
        string relicIdOrName)
    {
        if (string.IsNullOrWhiteSpace(relicIdOrName))
            return SoulRelicWriteOutcome.Failed("Укажите реликвию для снятия.");

        var equippedRelic = FindRelicInArray(context.Equipped, relicIdOrName);
        if (equippedRelic == null)
            return SoulRelicWriteOutcome.Failed($"Реликвия «{relicIdOrName}» не экипирована.");

        return SoulRelicWriteOutcome.Completed(
            $"Реликвия «{equippedRelic.Name}» снята и убрана в хранилище.",
            equippedRelic.RelicId,
            equippedRelic.Name,
            equippedRelic.CurrentSlot);
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
        var rarity = GetString(relic, "rarity");
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
        return new SoulRelicItem(relicId, name, rarity, isEquipped, currentSlot);
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
    string CurrentSlot);

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
