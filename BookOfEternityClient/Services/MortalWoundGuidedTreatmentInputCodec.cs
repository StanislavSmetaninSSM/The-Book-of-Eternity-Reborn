using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

// Immutable staged input DATA. Only the actual original reader and subsequent
// accepted-world owner can authenticate this value; Kind is never an exemption.
internal static class MortalWoundGuidedTreatmentInputCodec
{
    internal const string Kind = "mortal_guided_treatment_input";
    internal static bool IsKind(JsonObject? value) => value?["kind"] is JsonValue kind && kind.TryGetValue<string>(out var text) && text == Kind;

    internal static JsonObject Create(SpiritualSelfTreatmentStageContext frame, SpiritualSelfTreatmentSelection selection,
        MortalWoundTreatmentAuthority.Context context)
    {
        var result = new JsonObject
        {
            ["kind"] = Kind, ["schemaVersion"] = 1, ["sessionId"] = frame.SessionId, ["requestId"] = frame.RequestId,
            ["turn"] = frame.Turn, ["control"] = JsonSerializer.SerializeToNode(frame.Progression),
            ["selection"] = selection.ToJson(), ["context"] = ContextJson(context)
        };
        result["inputFingerprint"] = Fingerprint(result);
        _ = Read(result);
        return result;
    }

    internal static (SpiritualSelfTreatmentStageContext Frame, SpiritualSelfTreatmentSelection Selection,
        MortalWoundTreatmentAuthority.Context Context) Read(JsonObject input)
    {
        var fields = new[] { "kind", "schemaVersion", "sessionId", "requestId", "turn", "control", "selection", "context", "inputFingerprint" };
        if (input.Count != fields.Length || fields.Any(field => !input.ContainsKey(field)) || !IsKind(input) ||
            input["schemaVersion"] is not JsonValue version || !version.TryGetValue<int>(out var schema) || schema != 1 ||
            input["turn"] is not JsonValue turnValue || !turnValue.TryGetValue<int>(out var turn) || turn < 0 ||
            !ResourceMaterializationContract.IsExactIdentifier(Text(input, "sessionId")) ||
            !ResourceMaterializationContract.IsExactIdentifier(Text(input, "requestId")) ||
            Text(input, "inputFingerprint") != Fingerprint(input) || input["control"] is not JsonObject controlJson ||
            input["context"] is not JsonObject contextJson || input["selection"] is not JsonObject selectionJson)
            throw new InvalidDataException("Invalid closed Guided physical treatment staged input.");
        var control = controlJson.Deserialize<ProgressionControl>() ?? throw new InvalidDataException("Missing Mortal control.");
        if (WoundAcceptedTurnFingerprintWriter.CanonicalJson(controlJson) != WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonSerializer.SerializeToNode(control)) ||
            control.CurrentRealm != "Mortal World") throw new InvalidDataException("Guided physical treatment requires the exact full Mortal control.");
        var selection = SpiritualSelfTreatmentSelection.Parse(selectionJson);
        var parsed = MortalWoundTreatmentAuthority.ParseContext(contextJson.ToJsonString(), "mortalTreatment.context");
        if (!parsed.IsValid || parsed.Context is null || selection.MortalTreatment is not { } selected ||
            selected.Target.OwnerKind != parsed.Context.TargetKind || selected.Target.OwnerId != parsed.Context.TargetId ||
            selected.Provider.OwnerKind != parsed.Context.ProviderKind || selected.Provider.OwnerId != parsed.Context.ProviderId ||
            selected.ExpectedContextFingerprint != ContextFingerprint(parsed.Context))
            throw new InvalidDataException("Guided physical treatment selected participants and exact context disagree.");
        return (new(Text(input, "sessionId"), Text(input, "requestId"), turn, control), selection, parsed.Context);
    }

    internal static bool IsBound(JsonObject input, SpiritualSelfTreatmentStageContext frame)
    {
        try
        {
            var parsed = Read(input);
            return parsed.Frame.SessionId == frame.SessionId && parsed.Frame.RequestId == frame.RequestId && parsed.Frame.Turn == frame.Turn &&
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(input["control"]) ==
                    WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonSerializer.SerializeToNode(frame.Progression));
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or ArgumentException or JsonException) { return false; }
    }

    internal static void RequireRequestMatches(JsonObject input, MortalWoundTreatmentAttemptRequest request)
    {
        var data = Read(input); var selected = data.Selection.MortalTreatment!; var coordinates = request.Coordinates;
        if (request.Mode != selected.Mode || coordinates.Purpose != "treatment" ||
            data.Frame.SessionId != coordinates.SessionId || data.Frame.RequestId != coordinates.RequestId || data.Frame.Turn != coordinates.Turn ||
            data.Selection.WoundId != coordinates.WoundId || data.Selection.ExpectedWoundFingerprint != coordinates.ExpectedBeforeFingerprint ||
            selected.Target != request.RouteSourceWound.Owner || selected.RouteId != coordinates.RouteId || selected.MilestoneOrdinal != request.MilestoneOrdinal ||
            selected.ExpectedRouteFingerprint != request.RequirementAuthority.RouteFingerprint ||
            data.Context.TargetKind != coordinates.TargetKind || data.Context.TargetId != coordinates.TargetId ||
            data.Context.ProviderKind != coordinates.ProviderKind || data.Context.ProviderId != coordinates.ProviderId ||
            data.Context.CurrentLocationId != coordinates.LocationId)
            throw new InvalidDataException("Retained guided treatment request differs from its original selected method, participants or frame.");
    }

    internal static void RequireMatches(JsonObject input, MortalWoundTreatmentAcceptedStateAuthority owner,
        MortalWoundTreatmentAttemptRequest request, JsonObject originalControl)
    {
        RequireRequestMatches(input, request);
        var parsed = Read(input); var binding = owner.Binding; var selected = parsed.Selection.MortalTreatment!;
        if (request.Mode != selected.Mode || request.Coordinates.Purpose != "treatment" ||
            parsed.Frame.SessionId != binding.SessionId || parsed.Frame.RequestId != binding.RequestId || parsed.Frame.Turn != binding.Turn ||
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(input["control"]) != WoundAcceptedTurnFingerprintWriter.CanonicalJson(originalControl) ||
            parsed.Selection.WoundId != request.Coordinates.WoundId || selected.Target != owner.CurrentWound.Owner ||
            parsed.Selection.ExpectedWoundFingerprint != owner.WoundFingerprint ||
            selected.RouteId != request.Coordinates.RouteId || selected.MilestoneOrdinal != request.MilestoneOrdinal ||
            selected.ExpectedRouteFingerprint != request.RequirementAuthority.RouteFingerprint ||
            ContextFingerprint(parsed.Context) != ContextFingerprint(owner.RequirementContext))
            throw new InvalidDataException("The actual signed guided treatment input differs from this accepted request, original control or participants.");
    }

    internal static string ContextFingerprint(MortalWoundTreatmentAuthority.Context context) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new[] { Kind + ".context", "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(ContextJson(context)) });
    internal static JsonObject ContextJson(MortalWoundTreatmentAuthority.Context context) => new()
    {
        ["schemaVersion"] = context.SchemaVersion, ["realm"] = context.Realm, ["targetKind"] = context.TargetKind,
        ["targetId"] = context.TargetId, ["providerKind"] = context.ProviderKind, ["providerId"] = context.ProviderId,
        ["currentLocationId"] = context.CurrentLocationId
    };
    internal static string Fingerprint(JsonObject input)
    {
        var payload = input.DeepClone().AsObject(); payload.Remove("inputFingerprint");
        return WoundAcceptedTurnFingerprintWriter.Compute(new[] { Kind, "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(payload) });
    }
    private static string Text(JsonObject value, string field) => value[field] is JsonValue node && node.TryGetValue<string>(out var text)
        ? text : throw new InvalidDataException("Invalid guided treatment input field: " + field);
}
