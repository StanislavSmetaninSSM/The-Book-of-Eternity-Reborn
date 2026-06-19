using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;

namespace BookOfEternityClient.Services.GmWorkers;

public static class GmWorkerJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
        return options;
    }
}
