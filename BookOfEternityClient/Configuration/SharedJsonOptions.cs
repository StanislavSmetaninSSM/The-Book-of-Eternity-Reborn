using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Configuration;

public static class SharedJsonOptions
{
    public static JsonSerializerOptions PrettyCamelCaseUnsafeRelaxed { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
