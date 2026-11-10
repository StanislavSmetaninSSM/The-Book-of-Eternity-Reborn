using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Closed retained diagnosis DATA. Parsing it grants no signed/current authority.</summary>
internal static class SpiritualDiagnosisRequestCodec
{
    private const string Kind = "spiritual_diagnosis_request";

    internal static bool IsKind(JsonObject root) => root["kind"] is JsonValue kind &&
        kind.TryGetValue<string>(out var value) && value == Kind;

    internal static bool IsBound(JsonObject root, SpiritualSelfTreatmentStageContext context)
    {
        _ = ReadSelection(root);
        return Text(root, "sessionId") == context.SessionId && Text(root, "requestId") == context.RequestId &&
            Number(root, "turn") == context.Turn && WoundAcceptedTurnFingerprintWriter.CanonicalJson(root["control"]) ==
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonSerializer.SerializeToNode(context.Progression));
    }

    internal static JsonObject Create(SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, SpiritualDiagnosisAdmission data)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(selection); ArgumentNullException.ThrowIfNull(data);
        SpiritualDiagnosisRequirementBindings.Validate(data);
        if (selection.Diagnosis is not { } diagnosis || selection != data.Selection ||
            !ResourceMaterializationContract.IsExactIdentifier(context.SessionId) || !ResourceMaterializationContract.IsExactIdentifier(context.RequestId) ||
            context.Turn < 1 || context.Progression is null ||
            !AfterlifeEntityProfileState.TryNormalizeEffectRealm(context.Progression.CurrentRealm, out var realm) ||
            realm != diagnosis.Target.Realm || context.Progression.NextChaosSeaTurnOrdinal < 1 ||
            data.Wound.Owner != diagnosis.Target || data.Wound.WoundId != selection.WoundId ||
            WoundIdentityState.ComputeSemanticFingerprint(data.Wound) != selection.ExpectedWoundFingerprint ||
            data.PathFingerprint != diagnosis.ExpectedDiagnosisPathFingerprint || data.HealingTier is < 0 or > 5)
            throw new InvalidDataException("Diagnosis request does not match its exact admitted DATA and turn.");
        _ = selection.ToJson();
        var ordinal = context.Progression.NextChaosSeaTurnOrdinal;
        var root = new JsonObject
        {
            ["schemaVersion"] = 1, ["kind"] = Kind, ["sessionId"] = context.SessionId, ["requestId"] = context.RequestId, ["turn"] = context.Turn,
            ["control"] = JsonSerializer.SerializeToNode(context.Progression),
            ["diagnosis"] = new JsonObject
            {
                ["attemptId"] = AttemptId(context, selection, ordinal), ["woundId"] = selection.WoundId,
                ["woundFingerprint"] = selection.ExpectedWoundFingerprint, ["target"] = SpiritualHealingRequestProjection.OwnerJson(diagnosis.Target),
                ["diagnosisPathId"] = diagnosis.DiagnosisPathId, ["pathFingerprint"] = data.PathFingerprint,
                ["requirementFingerprint"] = data.RequirementFingerprint, ["sourceFingerprint"] = data.SourceFingerprint,
                ["requirementBindingFingerprint"] = data.RequirementBindingFingerprint,
                ["healingTier"] = data.HealingTier, ["cycleOrdinal"] = ordinal,
                ["cycleKey"] = AfterlifeWoundTreatmentAuthority.CycleKey(realm, ordinal)
            }
        };
        root["requestFingerprint"] = Fingerprint(root);
        _ = ReadSelection(root);
        return root;
    }

    internal static SpiritualSelfTreatmentSelection ReadSelection(JsonObject root)
    {
        if (!Closed(root, "schemaVersion", "kind", "sessionId", "requestId", "turn", "control", "diagnosis", "requestFingerprint") ||
            Number(root, "schemaVersion") != 1 || Text(root, "kind") != Kind || root["diagnosis"] is not JsonObject diagnosis ||
            !Closed(diagnosis, "attemptId", "woundId", "woundFingerprint", "target", "diagnosisPathId", "pathFingerprint",
                "requirementFingerprint", "sourceFingerprint", "requirementBindingFingerprint", "healingTier", "cycleOrdinal", "cycleKey") ||
            !ResourceMaterializationContract.IsExactIdentifier(Text(root, "sessionId")) ||
            !ResourceMaterializationContract.IsExactIdentifier(Text(root, "requestId")) || Number(root, "turn") < 1 ||
            Text(root, "requestFingerprint") != Fingerprint(root))
            throw new InvalidDataException("Invalid closed spiritual diagnosis request.");
        if (root["control"] is not JsonObject controlNode)
            throw new InvalidDataException("Diagnosis request requires its complete admitted control.");
        BookOfEternityClient.Models.ProgressionControl control;
        try { control = controlNode.Deserialize<BookOfEternityClient.Models.ProgressionControl>()
            ?? throw new InvalidDataException("Diagnosis request control is absent."); }
        catch (JsonException failure) { throw new InvalidDataException("Diagnosis request control is malformed.", failure); }
        if (WoundAcceptedTurnFingerprintWriter.CanonicalJson(controlNode) !=
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonSerializer.SerializeToNode(control)))
            throw new InvalidDataException("Diagnosis request control must have its complete exact closed shape.");
        var selected = SpiritualSelfTreatmentSelection.Parse(new JsonObject
        {
            ["schemaVersion"] = 9, ["mode"] = "diagnose", ["woundId"] = Text(diagnosis, "woundId"),
            ["expectedWoundFingerprint"] = Text(diagnosis, "woundFingerprint"), ["target"] = diagnosis["target"]?.DeepClone(),
            ["diagnosisPathId"] = Text(diagnosis, "diagnosisPathId"), ["expectedDiagnosisPathFingerprint"] = Text(diagnosis, "pathFingerprint")
        });
        var ordinal = Number(diagnosis, "cycleOrdinal");
        var realm = selected.Diagnosis!.Target.Realm;
        var context = new SpiritualSelfTreatmentStageContext(Text(root, "sessionId"), Text(root, "requestId"), Number(root, "turn"),
            control);
        if (ordinal < 1 || Number(diagnosis, "healingTier") is < 0 or > 5 ||
            control.NextChaosSeaTurnOrdinal != ordinal ||
            !AfterlifeEntityProfileState.TryNormalizeEffectRealm(control.CurrentRealm, out var controlRealm) || controlRealm != realm ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(Text(diagnosis, "requirementFingerprint")) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(Text(diagnosis, "requirementBindingFingerprint")) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(Text(diagnosis, "sourceFingerprint")) ||
            Text(diagnosis, "cycleKey") != AfterlifeWoundTreatmentAuthority.CycleKey(realm, ordinal) ||
            Text(diagnosis, "attemptId") != AttemptId(context, selected, ordinal))
            throw new InvalidDataException("Diagnosis request program/binding differs.");
        return selected;
    }

    internal static bool IsExact(JsonObject root, SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, SpiritualDiagnosisAdmission data)
    {
        try { return JsonNode.DeepEquals(root, Create(context, selection, data)); }
        catch (Exception failure) when (failure is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException) { return false; }
    }

    private static string AttemptId(SpiritualSelfTreatmentStageContext context, SpiritualSelfTreatmentSelection selection, int ordinal) =>
        "spiritual_diagnosis_attempt_" + WoundAcceptedTurnFingerprintWriter.Compute(new[] { Kind, "1", context.SessionId, context.RequestId,
            context.Turn.ToString(CultureInfo.InvariantCulture), ordinal.ToString(CultureInfo.InvariantCulture),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(selection.ToJson()) })[7..];
    private static string Fingerprint(JsonObject root)
    {
        var copy = root.DeepClone().AsObject(); copy.Remove("requestFingerprint");
        return WoundAcceptedTurnFingerprintWriter.Compute(new[] { Kind, "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(copy) });
    }
    private static bool Closed(JsonObject root, params string[] fields) => root.Count == fields.Length && fields.All(root.ContainsKey);
    private static string Text(JsonObject root, string field) => root[field] is JsonValue value && value.TryGetValue<string>(out var text)
        ? text : throw new InvalidDataException("Diagnosis request text field is invalid: " + field);
    private static int Number(JsonObject root, string field) => root[field] is JsonValue value && value.TryGetValue<int>(out var number)
        ? number : throw new InvalidDataException("Diagnosis request integer field is invalid: " + field);
}
