using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace BookOfEternityClient.Configuration;

public static class SharedJsonOptions
{
    /// <summary>Identifies the original builtin resolver without querying serializer metadata.</summary>
    internal static DefaultJsonTypeInfoResolver PrettyCamelCaseOriginalResolver { get; } = new();

    public static JsonSerializerOptions PrettyCamelCaseUnsafeRelaxed { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = PrettyCamelCaseOriginalResolver
    };
}
