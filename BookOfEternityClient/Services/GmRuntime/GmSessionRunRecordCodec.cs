using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Services.GmRuntime;

internal enum GmSessionRunObservationKind { Unspecified, Missing, Valid, Unreadable }

/// <summary>An explicit observation of the main slot; Missing is supplied only by a trusted absence read.</summary>
internal sealed class GmSessionRunObservation
{
    private GmSessionRunObservation(GmSessionRunObservationKind kind, GmSessionRunRecord? record) { Kind = kind; Record = record; }
    internal GmSessionRunObservationKind Kind { get; }
    internal GmSessionRunRecord? Record { get; }
    internal static GmSessionRunObservation Missing { get; } = new(GmSessionRunObservationKind.Missing, null);
    internal static GmSessionRunObservation Unreadable { get; } = new(GmSessionRunObservationKind.Unreadable, null);

    internal static GmSessionRunObservation FromRecord(GmSessionRunRecord? record)
    {
        try { GmSessionRunValidation.Validate(record); return new(GmSessionRunObservationKind.Valid, record); }
        catch (InvalidDataException) { return Unreadable; }
    }
}

/// <summary>Strict bounded schema codec, without filesystem writes, path grants or authenticated process ownership.</summary>
internal static class GmSessionRunRecordCodec
{
    internal const int MaximumBytes = 64 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };

    internal static byte[] Encode(GmSessionRunRecord record)
    {
        GmSessionRunValidation.Validate(record);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, Options);
        if (bytes.Length > MaximumBytes) throw GmSessionRunValidation.Invalid();
        return bytes;
    }

    /// <summary>Validates bytes only. Cold callers must apply InterpretCold before any admission decision.</summary>
    internal static GmSessionRunRecord Decode(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length is 0 or > MaximumBytes) throw GmSessionRunValidation.Invalid();
        try
        {
            // Decode before JSON parsing so invalid UTF-8 cannot be silently replaced in a string token.
            var json = StrictUtf8.GetString(bytes.Span);
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            var root = Object(document.RootElement, "SchemaVersion", "Identity", "Disposition", "StopEvidence");
            var stopElement = root.GetProperty("StopEvidence");
            GmSessionRunStopEvidence? stop = null;
            if (stopElement.ValueKind != JsonValueKind.Null)
            {
                var item = Object(stopElement, "Identity", "Kind", "ObservedBootId");
                stop = new(ReadIdentity(item.GetProperty("Identity")), ReadEnum<GmSessionRunStopKind>(item.GetProperty("Kind")),
                    ReadString(item.GetProperty("ObservedBootId")));
            }
            var record = new GmSessionRunRecord(root.GetProperty("SchemaVersion").GetInt32(),
                ReadIdentity(root.GetProperty("Identity")), ReadEnum<GmSessionRunDisposition>(root.GetProperty("Disposition")), stop);
            GmSessionRunValidation.Validate(record);
            return record;
        }
        catch (Exception failure) when (failure is JsonException or DecoderFallbackException or InvalidOperationException or FormatException or OverflowException)
        {
            // Do not retain parser/property/payload text, even in an inner exception.
            throw GmSessionRunValidation.Invalid();
        }
    }

    internal static GmSessionRunObservation Observe(ReadOnlyMemory<byte> bytes)
    {
        try { return GmSessionRunObservation.FromRecord(Decode(bytes)); }
        catch (InvalidDataException) { return GmSessionRunObservation.Unreadable; }
    }

    private static GmSessionRunIdentity ReadIdentity(JsonElement element)
    {
        var item = Object(element, "RootKey", "RunId", "GenerationId", "Epoch", "Backend", "HostInstanceId", "BootId");
        return new(ReadString(item.GetProperty("RootKey")), ReadString(item.GetProperty("RunId")),
            ReadString(item.GetProperty("GenerationId")), item.GetProperty("Epoch").GetInt64(),
            ReadEnum<GmSessionRunBackend>(item.GetProperty("Backend")), ReadString(item.GetProperty("HostInstanceId")),
            ReadString(item.GetProperty("BootId")));
    }

    private static JsonElement Object(JsonElement element, params string[] properties)
    {
        if (element.ValueKind != JsonValueKind.Object) throw GmSessionRunValidation.Invalid();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!properties.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name))
                throw GmSessionRunValidation.Invalid();
        if (seen.Count != properties.Length) throw GmSessionRunValidation.Invalid();
        return element;
    }

    private static string ReadString(JsonElement element) =>
        element.ValueKind == JsonValueKind.String ? element.GetString()! : throw GmSessionRunValidation.Invalid();

    private static T ReadEnum<T>(JsonElement element) where T : struct, Enum
    {
        var text = ReadString(element);
        if (!Enum.TryParse<T>(text, out var value) || Enum.GetName(value) != text) throw GmSessionRunValidation.Invalid();
        return value;
    }
}
