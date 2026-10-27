using System.Globalization;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Closed pre-snapshot source DATA. It is neither an accepted command nor publication authority.</summary>
internal static class SpiritualSelfTreatmentRequestCodec
{
    private const string Kind = "spiritual_self_treatment_request";

    internal static JsonObject Create(SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, string realm, int ordinal,
        string sourceFingerprint, string mode, IReadOnlyList<int> draws)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = 2, ["kind"] = Kind,
            ["sessionId"] = context.SessionId, ["requestId"] = context.RequestId, ["turn"] = context.Turn,
            ["attempt"] = new JsonObject
            {
                ["attemptId"] = AttemptId(context, selection, realm, ordinal),
                ["woundId"] = selection.WoundId, ["woundFingerprint"] = selection.ExpectedWoundFingerprint,
                ["realm"] = realm, ["cycleOrdinal"] = ordinal,
                ["cycleKey"] = AfterlifeWoundTreatmentAuthority.CycleKey(realm, ordinal),
                ["sourceFingerprint"] = sourceFingerprint,
                ["roll"] = new JsonObject
                {
                    ["mode"] = mode,
                    ["values"] = new JsonArray(draws.Select(d => (JsonNode)JsonValue.Create(d)!).ToArray())
                }
            }
        };
        root["requestFingerprint"] = Fingerprint(root);
        return root;
    }

    internal static bool IsExact(JsonObject root, SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, string realm, int ordinal,
        string sourceFingerprint, SpiritualHealingPreparedSource source)
    {
        if (!Closed(root, "schemaVersion", "kind", "sessionId", "requestId", "turn", "attempt", "requestFingerprint") ||
            Int(root["schemaVersion"]) != 2 || Text(root["kind"]) != Kind ||
            Text(root["sessionId"]) != context.SessionId || Text(root["requestId"]) != context.RequestId ||
            Int(root["turn"]) != context.Turn || root["attempt"] is not JsonObject attempt ||
            !Closed(attempt, "attemptId", "woundId", "woundFingerprint", "realm", "cycleOrdinal", "cycleKey", "sourceFingerprint", "roll") ||
            Text(attempt["attemptId"]) != AttemptId(context, selection, realm, ordinal) ||
            Text(attempt["woundId"]) != selection.WoundId || Text(attempt["woundFingerprint"]) != selection.ExpectedWoundFingerprint ||
            Text(attempt["realm"]) != realm || Int(attempt["cycleOrdinal"]) != ordinal ||
            Text(attempt["cycleKey"]) != AfterlifeWoundTreatmentAuthority.CycleKey(realm, ordinal) ||
            Text(attempt["sourceFingerprint"]) != sourceFingerprint || attempt["roll"] is not JsonObject roll ||
            !Closed(roll, "mode", "values") || Text(roll["mode"]) != source.RollMode || roll["values"] is not JsonArray values ||
            values.Count != (source.RollMode == "normal" ? 1 : 2) || values.Any(v => Int(v) is null or < 1 or > 20) ||
            Text(root["requestFingerprint"]) != Fingerprint(root))
            return false;
        return SpiritualHealingResolver.ResolvePrepared(source, source.RollMode,
            values.Select(v => Int(v)!.Value).ToArray()).IsValid;
    }

    private static string AttemptId(SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, string realm, int ordinal) =>
        "spiritual_self_attempt_" + WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            Kind, "2", context.SessionId, context.RequestId, context.Turn.ToString(CultureInfo.InvariantCulture),
            selection.WoundId, selection.ExpectedWoundFingerprint, realm, ordinal.ToString(CultureInfo.InvariantCulture)
        })[7..];

    private static string Fingerprint(JsonObject root)
    {
        var copy = root.DeepClone().AsObject();
        copy.Remove("requestFingerprint");
        return WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            Kind, "2", WoundAcceptedTurnFingerprintWriter.CanonicalJson(copy)
        });
    }

    private static bool Closed(JsonObject root, params string[] fields) =>
        root.Count == fields.Length && fields.All(root.ContainsKey);
    private static string? Text(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    private static int? Int(JsonNode? node) => node is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;
}
