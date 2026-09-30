using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Services;

/// <summary>
/// Carries a safe spiritual wound continuation request, without execution authority.
/// </summary>
public sealed record SpiritualWoundContinuationRequest
{
    /// <summary>
    /// Identifies the closed transport schema, currently one.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;
    /// <summary>
    /// Correlates this request with a freshly verified private continuation.
    /// </summary>
    public required string ContinuationId { get; init; }
    /// <summary>
    /// Selects decision submission or correction of a dependent draft.
    /// </summary>
    public required string Phase { get; init; }
    /// <summary>
    /// Describes the current decision, or is null during dependent correction.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required SpiritualWoundContinuationOffer? Offer { get; init; }
    /// <summary>
    /// Identifies the existing narrative field containing the final scene text.
    /// </summary>
    public required SpiritualWoundContinuationSceneSource SceneTextSource { get; init; }
    /// <summary>
    /// Lists exact client-derived dependent fields; empty for a decision request.
    /// </summary>
    public required IReadOnlyList<SpiritualWoundContinuationField> DependentDraftFields { get; init; }
}

/// <summary>
/// Carries the explicit GM response; actual admission belongs to the C2 owner.
/// </summary>
public sealed record SpiritualWoundContinuationResponse
{
    /// <summary>
    /// Identifies the closed transport schema, currently one.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;
    /// <summary>
    /// Echoes the exact current request correlation.
    /// </summary>
    public required string ContinuationId { get; init; }
    /// <summary>
    /// Contains one current decision, or no decisions for dependent correction.
    /// </summary>
    public required IReadOnlyList<JsonElement> WoundDecisions { get; init; }
}

/// <summary>
/// Exposes only the readable constraints of the current owner-derived offer.
/// </summary>
public sealed record SpiritualWoundContinuationOffer
{
    /// <summary>
    /// Identifies the current public opportunity without a private source coordinate.
    /// </summary>
    public required string OpportunityRef { get; init; }
    /// <summary>
    /// Gives the lowest new rank; it may exceed the maximum in a none-only offer.
    /// </summary>
    public int MinimumSeverityRank { get; init; }
    /// <summary>
    /// Gives the guaranteed required rank, or null for optional harm.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required int? RequiredSeverityRank { get; init; }
    /// <summary>
    /// Gives the maximum rank admitted by this source.
    /// </summary>
    public int MaximumSeverityRank { get; init; }
    /// <summary>
    /// Describes the affected target in readable terms.
    /// </summary>
    public required string Target { get; init; }
    /// <summary>
    /// Describes the cause in readable terms.
    /// </summary>
    public required string Cause { get; init; }
    /// <summary>
    /// Contains the location vocabulary allowed by the current offer.
    /// </summary>
    public required IReadOnlyList<string> AllowedLocationKinds { get; init; }
    /// <summary>
    /// Contains the decisions available at the current frontier.
    /// </summary>
    public required IReadOnlyList<string> AllowedDecisions { get; init; }
}

/// <summary>
/// Identifies one exact dependent draft field; it is not a write capability.
/// </summary>
public sealed record SpiritualWoundContinuationField
{
    /// <summary>
    /// Gives the canonical relative file path.
    /// </summary>
    public required string Path { get; init; }
    /// <summary>
    /// Gives the exact JSON pointer to a field permitted by the current owner.
    /// </summary>
    public required string JsonPointer { get; init; }
}

/// <summary>
/// Identifies the existing narrative response field, never a new text carrier.
/// </summary>
public sealed record SpiritualWoundContinuationSceneSource
{
    /// <summary>
    /// Gives the fixed narrative response path.
    /// </summary>
    public required string Path { get; init; }
    /// <summary>
    /// Gives the fixed response field name.
    /// </summary>
    public required string Field { get; init; }
}

internal static class SpiritualWoundContinuationProtocol
{
    internal const string EnvelopeName = "spiritualWoundContinuation";
    private static readonly JsonSerializerOptions ExactOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false
    };

    /// <summary>
    /// Reads the closed request before any permissive serializer can discard fields.
    /// </summary>
    /// <param name="value">
    /// Raw request envelope with exact-case, unique keys.
    /// </param>
    /// <returns>
    /// A structurally valid comparison-only request.
    /// </returns>
    internal static SpiritualWoundContinuationRequest ReadRequest(JsonElement value)
    {
        Unique(value);
        Fields(value, "schemaVersion", "continuationId", "phase", "offer", "sceneTextSource", "dependentDraftFields");
        Version(value);
        Text(value, "continuationId");
        var phase = Text(value, "phase");
        Require(phase is "decision" or "dependent_draft", "Unsupported continuation phase.");
        var scene = value.GetProperty("sceneTextSource");
        Fields(scene, "path", "field");
        Require(Text(scene, "path") == "output/narrative_response.json" && Text(scene, "field") == "response",
            "Scene text must use the existing narrative response field.");
        var dependent = value.GetProperty("dependentDraftFields");
        Require(dependent.ValueKind == JsonValueKind.Array, "dependentDraftFields must be an array.");
        var paths = new HashSet<(string, string)>();
        foreach (var field in dependent.EnumerateArray())
        {
            Fields(field, "path", "jsonPointer");
            var path = Text(field, "path");
            var pointer = Text(field, "jsonPointer");
            Require(!path.StartsWith('/') && !path.Contains('\\') && !path.Contains(':') &&
                path.Split('/').All(part => part is not ("" or "." or "..")), "Invalid relative draft path.");
            Require(pointer.StartsWith('/') && ValidPointer(pointer), "Invalid dependent JSON pointer.");
            Require(paths.Add((path, pointer)), "Duplicate dependent field.");
        }
        if (phase == "decision")
        {
            Require(dependent.GetArrayLength() == 0, "Decision requests cannot permit dependent edits.");
            ReadOffer(value.GetProperty("offer"));
        }
        else
            Require(value.GetProperty("offer").ValueKind == JsonValueKind.Null, "Dependent correction cannot offer a new decision.");
        return value.Deserialize<SpiritualWoundContinuationRequest>(ExactOptions)!;
    }

    /// <summary>
    /// Reads a closed response without granting permission to execute its decisions.
    /// </summary>
    /// <param name="value">
    /// Raw response envelope; nested duplicate keys are rejected before conversion.
    /// </param>
    /// <returns>
    /// A structurally valid response awaiting current owner comparison and composition.
    /// </returns>
    internal static SpiritualWoundContinuationResponse ReadResponse(JsonElement value)
    {
        Unique(value);
        Fields(value, "schemaVersion", "continuationId", "woundDecisions");
        Version(value);
        Text(value, "continuationId");
        var decisions = value.GetProperty("woundDecisions");
        Require(decisions.ValueKind == JsonValueKind.Array, "woundDecisions must be an array.");
        foreach (var decision in decisions.EnumerateArray())
        {
            Require(decision.ValueKind == JsonValueKind.Object, "A decision must be an object.");
            var kind = Text(decision, "decision");
            Require(kind is "none" or "materialize", "Unsupported GM decision.");
            if (kind == "none")
                Fields(decision, "opportunityRef", "decision");
            else
            {
                Fields(decision, "opportunityRef", "decision", "woundRef", "proposal");
                Text(decision, "woundRef");
                Fields(decision.GetProperty("proposal"), "classification", "display", "severity",
                    "complications", "consequenceDefinitions", "treatment", "recovery");
            }
            Text(decision, "opportunityRef");
        }
        return value.Deserialize<SpiritualWoundContinuationResponse>(ExactOptions)!;
    }

    /// <summary>
    /// Checks typed requests constructed in memory using the same closed wire rules.
    /// </summary>
    /// <param name="request">
    /// Request to validate; no original-turn authority is inferred from its contents.
    /// </param>
    /// <returns>
    /// Structural errors, or an empty list when the wire shape is valid.
    /// </returns>
    internal static IReadOnlyList<string> ValidateRequest(SpiritualWoundContinuationRequest request)
    {
        try
        {
            ReadRequest(JsonSerializer.SerializeToElement(request, ExactOptions));
            return [];
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException)
        {
            return [exception.Message];
        }
    }

    /// <summary>
    /// Compares response correlation and decision cardinality against a current request.
    /// </summary>
    /// <param name="request">
    /// Current request, or null for ordinary repair where a response is forbidden.
    /// </param>
    /// <param name="response">
    /// Explicit response; null never means a declined offer.
    /// </param>
    /// <returns>
    /// Comparison errors; an empty result still requires genuine owner admission.
    /// </returns>
    internal static IReadOnlyList<string> ValidateResponse(
        SpiritualWoundContinuationRequest? request, SpiritualWoundContinuationResponse? response)
    {
        if (request is null)
            return response is null ? [] : ["Continuation response is forbidden on ordinary repair."];
        if (response is null)
            return ["The current continuation requires an explicit response."];
        try
        {
            var current = ReadRequest(JsonSerializer.SerializeToElement(request, ExactOptions));
            var received = ReadResponse(JsonSerializer.SerializeToElement(response, ExactOptions));
            Require(current.ContinuationId == received.ContinuationId, "Continuation correlation does not match.");
            Require(received.WoundDecisions.Count == (current.Phase == "decision" ? 1 : 0),
                "The response has the wrong decision count for its phase.");
            if (current.Offer is { } offer)
            {
                var decision = received.WoundDecisions[0];
                Require(Text(decision, "opportunityRef") == offer.OpportunityRef, "Decision belongs to another offer.");
                Require(offer.AllowedDecisions.Contains(Text(decision, "decision"), StringComparer.Ordinal),
                    "Decision is not allowed by the current offer.");
            }
            return [];
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException)
        {
            return [exception.Message];
        }
    }

    private static void ReadOffer(JsonElement value)
    {
        Fields(value, "opportunityRef", "minimumSeverityRank", "requiredSeverityRank", "maximumSeverityRank",
            "target", "cause", "allowedLocationKinds", "allowedDecisions");
        Text(value, "opportunityRef");
        Text(value, "target");
        Text(value, "cause");
        var minimum = Integer(value, "minimumSeverityRank");
        var maximum = Integer(value, "maximumSeverityRank");
        Require(minimum is >= 1 and <= 5 && maximum is >= 1 and <= 4, "Invalid severity bounds.");
        var required = value.GetProperty("requiredSeverityRank");
        if (required.ValueKind != JsonValueKind.Null)
        {
            var rank = Integer(value, "requiredSeverityRank");
            Require(rank >= minimum && rank <= maximum, "Required severity is outside the offer.");
        }
        var locations = Strings(value, "allowedLocationKinds");
        Require(locations.Count > 0 && locations.All(kind => kind is "anatomical" or "systemic" or "mental" or "spiritual_axis" or "other"),
            "Unsupported location kind.");
        var decisions = Strings(value, "allowedDecisions");
        string[] expected = required.ValueKind != JsonValueKind.Null ? ["materialize"] :
            minimum <= maximum ? ["none", "materialize"] : ["none"];
        Require(decisions.SequenceEqual(expected), "Decisions do not match the offer bounds.");
    }

    private static List<string> Strings(JsonElement value, string name)
    {
        var array = value.GetProperty(name);
        Require(array.ValueKind == JsonValueKind.Array, name + " must be an array.");
        var result = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            Require(item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()), name + " requires strings.");
            Require(!result.Contains(item.GetString()!, StringComparer.Ordinal), name + " contains duplicates.");
            result.Add(item.GetString()!);
        }
        return result;
    }

    private static void Version(JsonElement value) => Require(Integer(value, "schemaVersion") == 1, "Unsupported schemaVersion.");

    private static int Integer(JsonElement value, string name)
    {
        Require(value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out _), name + " must be an integer.");
        return property.GetInt32();
    }

    private static string Text(JsonElement value, string name)
    {
        Require(value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(property.GetString()), name + " must be a nonempty string.");
        return property.GetString()!;
    }

    private static void Fields(JsonElement value, params string[] fields)
    {
        Require(value.ValueKind == JsonValueKind.Object, "Continuation objects must have a closed shape.");
        var actual = value.EnumerateObject().Select(property => property.Name).ToArray();
        Require(actual.Length == fields.Length && actual.ToHashSet(StringComparer.Ordinal).SetEquals(fields),
            "Continuation object has missing, unknown or incorrectly cased fields.");
    }

    private static void Unique(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                Require(names.Add(property.Name), "Duplicate continuation JSON key: " + property.Name);
                Unique(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) Unique(item);
    }

    private static bool ValidPointer(string pointer)
    {
        for (var index = 0; index < pointer.Length; index++)
            if (pointer[index] == '~' && (++index == pointer.Length || pointer[index] is not ('0' or '1')))
                return false;
        return true;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
