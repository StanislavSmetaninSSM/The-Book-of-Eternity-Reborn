using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;

namespace BookOfEternityClient.UI;

public static class StructuredBonusDisplay
{
    public static string FieldLabel(string fieldName) =>
        fieldName switch
        {
            "bonusId" => "ID бонуса",
            "bonusType" => "Тип бонуса",
            "type" => "Тип",
            "targetType" => "Тип цели",
            "targetTypeDisplayName" => "Название цели",
            "target" => "Цель",
            "skill" => "Навык",
            "resource" => "Ресурс",
            "characteristic" => "Характеристика",
            "scalingCharacteristic" => "Масштабирование",
            "stat" => "Показатель",
            "effect" => "Эффект",
            "valueType" => "Тип значения",
            "modifierType" => "Тип модификатора",
            "value" => "Значение",
            "application" => "Применение",
            "condition" => "Условие",
            "source" => "Источник",
            "sourceId" => "ID источника",
            "summary" => "Кратко",
            "description" => "Описание",
            "stackingRule" => "Правило сложения",
            "duration" => "Длительность",
            "group" => "Группа",
            "isActive" => "Активен",
            "interactionType" => "Тип взаимодействия",
            "targetStateName" => "Целевое состояние",
            "changeValue" => "Изменение",
            "scalesValue" => "Масштабирует значение",
            "scalesDuration" => "Масштабирует длительность",
            "scalesChance" => "Масштабирует шанс",
            _ => HumanizeFieldName(fieldName)
        };

    public static string FormatValue(JsonNode? node, string? fieldName = null)
    {
        if (TryGetScalarString(node, out var scalar))
            return FormatScalar(scalar, fieldName);

        if (node is JsonArray array)
        {
            return string.Join("; ", array
                .Select(item => FormatValue(item, fieldName))
                .Where(static value => !string.IsNullOrWhiteSpace(value)));
        }

        if (node is JsonObject obj)
        {
            return string.Join("; ", obj
                .Select(property => $"{FieldLabel(property.Key)}: {FormatValue(property.Value, property.Key)}")
                .Where(static value => !string.IsNullOrWhiteSpace(value)));
        }

        return string.Empty;
    }

    public static string FormatValue(JsonElement value, string? fieldName = null) =>
        value.ValueKind switch
        {
            JsonValueKind.String => FormatScalar(value.GetString() ?? string.Empty, fieldName),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "да",
            JsonValueKind.False => "нет",
            JsonValueKind.Null => "null",
            JsonValueKind.Undefined => "undefined",
            JsonValueKind.Array => string.Join("; ", value.EnumerateArray()
                .Select(item => FormatValue(item, fieldName))
                .Where(static item => !string.IsNullOrWhiteSpace(item))),
            JsonValueKind.Object => string.Join("; ", value.EnumerateObject()
                .Select(property => $"{FieldLabel(property.Name)}: {FormatValue(property.Value, property.Name)}")
                .Where(static item => !string.IsNullOrWhiteSpace(item))),
            _ => value.ToString() ?? string.Empty
        };

    public static string FormatScalar(string value, string? fieldName = null)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return string.Empty;

        if (ShouldLocalizeCharacteristic(fieldName) && TryLocalizeCharacteristic(trimmed, out var characteristic))
            return characteristic;

        if (string.Equals(fieldName, "target", StringComparison.OrdinalIgnoreCase) &&
            TryLocalizeCharacteristic(trimmed, out characteristic))
        {
            return characteristic;
        }

        return trimmed switch
        {
            "Skill" => "Навык",
            "Characteristic" => "Характеристика",
            "Resource" => "Ресурс",
            "Flat" => "плоский бонус",
            "Fixed" => "фиксированный бонус",
            "Percent" => "процент",
            "Multiplier" => "множитель",
            "skill" => "навык",
            "characteristic" => "характеристика",
            "resource" => "ресурс",
            "temporary" => "временный",
            "permanent" => "постоянный",
            "onUse" => "при использовании",
            "onEquip" => "при экипировке",
            "onConsume" => "при употреблении",
            "PoiseDamage" => "урон равновесию",
            "Damage" => "урон",
            "Heal" => "лечение",
            "main" => "основное действие",
            "fast" => "быстрое действие",
            "free" => "свободное действие",
            "true" => "да",
            "false" => "нет",
            _ => trimmed
        };
    }

    public static string FormatCharacteristicName(string value) =>
        TryLocalizeCharacteristic(value, out var characteristic) ? characteristic : value;

    private static bool ShouldLocalizeCharacteristic(string? fieldName) =>
        string.Equals(fieldName, "characteristic", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fieldName, "scalingCharacteristic", StringComparison.OrdinalIgnoreCase);

    private static bool TryLocalizeCharacteristic(string value, out string translated)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (Characteristics.RussianNames.TryGetValue(normalized, out translated!))
            return true;

        translated = string.Empty;
        return false;
    }

    private static bool TryGetScalarString(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<string>(out var stringValue))
            {
                value = stringValue;
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

            if (jsonValue.TryGetValue<double>(out var doubleValue))
            {
                value = doubleValue.ToString("G");
                return true;
            }

            if (jsonValue.TryGetValue<bool>(out var boolValue))
            {
                value = boolValue ? "true" : "false";
                return true;
            }
        }

        return false;
    }

    private static string HumanizeFieldName(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            return "Поле";

        var spaced = string.Concat(fieldName.Select((ch, index) =>
            index > 0 && char.IsUpper(ch) ? " " + ch : ch.ToString()));
        return spaced.Replace('_', ' ').Trim();
    }
}
