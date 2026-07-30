using System.Text.Json;

namespace BookOfEternityClient.Core;

internal static class StrictJsonAuthority
{
    internal static T? Deserialize<T>(
        string json,
        JsonSerializerOptions options,
        string authorityName)
    {
        using var document = JsonDocument.Parse(
            json,
            CreateDocumentOptions(options));
        EnsureUniqueProperties(
            document.RootElement,
            "$",
            options.PropertyNameCaseInsensitive
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal,
            authorityName);
        return document.RootElement.Deserialize<T>(options);
    }

    internal static T? Deserialize<T>(
        ReadOnlyMemory<byte> utf8Json,
        JsonSerializerOptions options,
        string authorityName)
    {
        using var document = JsonDocument.Parse(
            utf8Json,
            CreateDocumentOptions(options));
        EnsureUniqueProperties(
            document.RootElement,
            "$",
            options.PropertyNameCaseInsensitive
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal,
            authorityName);
        return document.RootElement.Deserialize<T>(options);
    }

    private static JsonDocumentOptions CreateDocumentOptions(
        JsonSerializerOptions options) =>
        new()
        {
            AllowTrailingCommas = options.AllowTrailingCommas,
            CommentHandling = options.ReadCommentHandling ==
                              JsonCommentHandling.Skip
                ? JsonCommentHandling.Skip
                : JsonCommentHandling.Disallow
        };

    private static void EnsureUniqueProperties(
        JsonElement value,
        string path,
        StringComparer propertyComparer,
        string authorityName)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(propertyComparer);
            foreach (var property in value.EnumerateObject())
            {
                var propertyPath = $"{path}.{property.Name}";
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException(
                        $"{authorityName} contains duplicate JSON property '{propertyPath}'.");
                }

                EnsureUniqueProperties(
                    property.Value,
                    propertyPath,
                    propertyComparer,
                    authorityName);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                EnsureUniqueProperties(
                    item,
                    $"{path}[{index++}]",
                    propertyComparer,
                    authorityName);
            }
        }
    }
}
