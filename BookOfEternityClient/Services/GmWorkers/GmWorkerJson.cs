using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;

namespace BookOfEternityClient.Services.GmWorkers;

public static class GmWorkerJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, Options);

    /// <summary>
    /// Reads a worker contract, strictly validating any wound continuation before conversion.
    /// </summary>
    /// <param name="json">
    /// Worker JSON; malformed continuation envelopes are rejected without discarding fields.
    /// </param>
    /// <typeparam name="T">
    /// Worker contract type to read.
    /// </typeparam>
    /// <returns>
    /// The deserialized contract, or null when the JSON root is null.
    /// </returns>
    public static T? Deserialize<T>(string json)
    {
        if (typeof(T) == typeof(WorkerTaskPacket) || typeof(T) == typeof(WorkerProposal))
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                var envelopes = document.RootElement.EnumerateObject().Where(property =>
                    property.Name.Equals(SpiritualWoundContinuationProtocol.EnvelopeName,
                        StringComparison.OrdinalIgnoreCase)).ToArray();
                if (envelopes.Length > 1 || envelopes.Any(property =>
                    property.Name != SpiritualWoundContinuationProtocol.EnvelopeName))
                    throw new InvalidDataException("Duplicate or incorrectly cased continuation envelope.");
                if (envelopes.Length == 1)
                {
                    if (typeof(T) == typeof(WorkerTaskPacket))
                        SpiritualWoundContinuationProtocol.ReadRequest(envelopes[0].Value);
                    else
                        SpiritualWoundContinuationProtocol.ReadResponse(envelopes[0].Value);
                }
            }
        }
        return JsonSerializer.Deserialize<T>(json, Options);
    }

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
