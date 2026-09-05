using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Services;

// One result authority. Canonical JSON is always a detached projection, never stored
// in a caller-visible mutable slot alongside the typed result.
[JsonConverter(typeof(WoundTransitionResultJsonConverter))]
internal abstract class WoundTransitionResult
{
    private protected WoundTransitionResult() { }
    public abstract string Kind { get; }
    internal abstract JsonObject ToCanonicalJson();
    internal virtual void WriteCanonical(Utf8JsonWriter writer) => ToCanonicalJson().WriteTo(writer);
}

[JsonConverter(typeof(WoundTransitionResultJsonConverter))]
internal sealed class WoundDiagnosisTransitionResult : WoundTransitionResult
{
    internal WoundDiagnosisTransitionResult(string diagnosisPathId, string result,
        IEnumerable<string> revealedFacts, string resultFingerprint)
    {
        DiagnosisPathId = diagnosisPathId;
        Result = result;
        RevealedFacts = revealedFacts.ToImmutableArray();
        ResultFingerprint = resultFingerprint;
    }

    public override string Kind => "diagnose";
    public string DiagnosisPathId { get; }
    public string Result { get; }
    public IReadOnlyList<string> RevealedFacts { get; }
    public string ResultFingerprint { get; }

    internal override JsonObject ToCanonicalJson() => new()
    {
        ["kind"] = Kind,
        ["diagnosisPathId"] = DiagnosisPathId,
        ["result"] = Result,
        ["revealedFacts"] = new JsonArray(RevealedFacts.Select(static fact => (JsonNode)fact).ToArray()),
        ["resultFingerprint"] = ResultFingerprint
    };
}

[JsonConverter(typeof(WoundTransitionResultJsonConverter))]
internal sealed class WoundAlternativeTreatmentTransitionResult : WoundTransitionResult
{
    internal WoundAlternativeTreatmentTransitionResult(string authoringRequestRef,
        string addedRouteId, string? addedDiagnosisPathId, string routeFingerprint,
        string? diagnosisPathFingerprint, string resultFingerprint)
    {
        AuthoringRequestRef = authoringRequestRef;
        AddedRouteId = addedRouteId;
        AddedDiagnosisPathId = addedDiagnosisPathId;
        RouteFingerprint = routeFingerprint;
        DiagnosisPathFingerprint = diagnosisPathFingerprint;
        ResultFingerprint = resultFingerprint;
    }

    public override string Kind => "author_alternative_treatment";
    public string AuthoringRequestRef { get; }
    public string AddedRouteId { get; }
    public string? AddedDiagnosisPathId { get; }
    public string RouteFingerprint { get; }
    public string? DiagnosisPathFingerprint { get; }
    public string ResultFingerprint { get; }

    internal override JsonObject ToCanonicalJson() => new()
    {
        ["kind"] = Kind,
        ["authoringRequestRef"] = AuthoringRequestRef,
        ["addedRouteId"] = AddedRouteId,
        ["addedDiagnosisPathId"] = AddedDiagnosisPathId,
        ["routeFingerprint"] = RouteFingerprint,
        ["diagnosisPathFingerprint"] = DiagnosisPathFingerprint,
        ["resultFingerprint"] = ResultFingerprint
    };
}

internal sealed class WoundTransitionResultJsonConverter : JsonConverter<WoundTransitionResult>
{
    public override bool CanConvert(Type typeToConvert) =>
        typeof(WoundTransitionResult).IsAssignableFrom(typeToConvert);

    public override WoundTransitionResult Read(ref Utf8JsonReader reader,
        Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("Restore results through the strict wound history parser.");

    public override void Write(Utf8JsonWriter writer, WoundTransitionResult value,
        JsonSerializerOptions options) => value.WriteCanonical(writer);
}
