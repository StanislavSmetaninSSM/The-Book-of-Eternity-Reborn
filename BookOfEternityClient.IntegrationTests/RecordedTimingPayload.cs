using System.Text.Json.Serialization;

namespace BookOfEternityClient.Tests;

/// <summary>Fresh scalar passive-row DATA in the accepted JSON member order; no authority or retained event result.</summary>
internal readonly record struct RecordedTimingPayload(
    string Boundary,
    string Operation,
    string State,
    double Milliseconds,
    long Sequence,
    int PID,
    double TotalElapsedMilliseconds,
    string Transport,
    long PassiveSequence,
    long ScopeId,
    long TimestampTicks,
    long ElapsedTicks,
    bool Late,
    long ObserverCompletedBefore,
    double ObserverGateMillisecondsBefore,
    double ObserverBuildSerializeMillisecondsBefore,
    double ObserverWriteMillisecondsBefore);

// Built-in generated schema metadata only. The first Default access stays in
// the measured recorded branch; no eager metadata/serializer warmup is added.
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(RecordedTimingPayload))]
internal partial class RecordedTimingJsonContext : JsonSerializerContext
{
}
