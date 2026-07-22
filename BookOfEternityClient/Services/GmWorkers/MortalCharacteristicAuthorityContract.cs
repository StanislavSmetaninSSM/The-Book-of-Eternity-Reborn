using System.Text.Json;

namespace BookOfEternityClient.Services.GmWorkers;

internal static class MortalCharacteristicAuthorityContract
{
    internal const string StatePath = "game_state/misc/characteristics.json";

    internal static bool TryReadKeys(
        string? json,
        out HashSet<string> keys,
        out string error)
    {
        keys = new HashSet<string>(StringComparer.Ordinal);
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = $"Mortal characteristic authority is missing: {StatePath}.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = $"Mortal characteristic authority must be one JSON object: {StatePath}.";
                return false;
            }

            if (TryFindDuplicateProperty(document.RootElement, string.Empty, out var duplicatePath))
            {
                error = $"Mortal characteristic authority contains duplicate property {duplicatePath}: {StatePath}.";
                return false;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!property.Name.StartsWith("_", StringComparison.Ordinal) &&
                    property.Value.ValueKind is JsonValueKind.String or
                        JsonValueKind.Number or
                        JsonValueKind.True or
                        JsonValueKind.False)
                {
                    keys.Add(property.Name);
                }
            }

            if (keys.Count == 0)
            {
                error = $"Mortal characteristic authority has no setting-defined keys: {StatePath}.";
                return false;
            }

            return true;
        }
        catch (JsonException exception)
        {
            error = $"Mortal characteristic authority is malformed at {StatePath}: {exception.Message}";
            return false;
        }
    }

    private static bool TryFindDuplicateProperty(
        JsonElement value,
        string path,
        out string duplicatePath)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                var propertyPath = string.IsNullOrEmpty(path)
                    ? property.Name
                    : $"{path}.{property.Name}";
                if (!names.Add(property.Name))
                {
                    duplicatePath = propertyPath;
                    return true;
                }

                if (TryFindDuplicateProperty(property.Value, propertyPath, out duplicatePath))
                    return true;
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                if (TryFindDuplicateProperty(item, $"{path}[{index++}]", out duplicatePath))
                    return true;
            }
        }

        duplicatePath = string.Empty;
        return false;
    }
}
