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
    internal static GmSessionRunObservation Valid(GmSessionRunRecord record) => new(GmSessionRunObservationKind.Valid, record);
}

/// <summary>Bounded record codec contract; this RED baseline is not runtime-integrated or qualified.</summary>
internal static class GmSessionRunRecordCodec
{
    internal const int MaximumBytes = 64 * 1024;
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };
    internal static byte[] Encode(GmSessionRunRecord record) => JsonSerializer.SerializeToUtf8Bytes(record, Options);
    internal static GmSessionRunRecord Decode(ReadOnlyMemory<byte> bytes)
    {
        try { return JsonSerializer.Deserialize<GmSessionRunRecord>(bytes.Span, Options) ?? throw new JsonException(); }
        catch (JsonException) { throw new InvalidDataException("Persistent GM run record is invalid."); }
    }
    internal static GmSessionRunObservation Observe(ReadOnlyMemory<byte> bytes)
    {
        try { return GmSessionRunObservation.Valid(Decode(bytes)); }
        catch (InvalidDataException) { return GmSessionRunObservation.Unreadable; }
    }
}
