using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class SpiritualSelfTreatmentRequestCodec
{
    private const string AidKind = "spiritual_treatment_request";
    internal static bool IsAidHeader(JsonObject root) => Int(root["schemaVersion"]) is 3 or 4 &&
        Text(root["kind"]) == AidKind && Text(root["mode"]) == "aid";
    internal static bool IsReceiveAidHeader(JsonObject root) => IsAidHeader(root) && Int(root["schemaVersion"]) == 4;
    internal static bool IsActiveKind(JsonObject root) => Text(root["kind"]) is Kind or AidKind;
    internal static bool IsBoundActive(JsonObject root, SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, string realm, int ordinal) => selection.Aid is null
        ? IsBound(root, context, selection, realm, ordinal) : IsBoundAid(root, context, selection, realm, ordinal);

    internal static JsonObject ActiveSourceJson(SpiritualHealingPreparedSource source) => new()
    {
        ["healingTier"] = source.HealingTier, ["complicationModifier"] = source.ComplicationModifier,
        ["rollMode"] = source.RollMode, ["rollContributions"] = JsonSerializer.SerializeToNode(source.RollContributions)
    };

    internal static SpiritualSelfTreatmentSelection ReadActiveSelection(JsonObject root)
    {
        var attempt = root["attempt"] as JsonObject ?? throw new InvalidDataException("Active treatment attempt is absent.");
        var selection = new SpiritualSelfTreatmentSelection(Text(attempt["woundId"]) ?? "", Text(attempt["woundFingerprint"]) ?? "");
        if (IsAidHeader(root))
        {
            var witness = root["participation"] as JsonObject ?? throw new InvalidDataException("Aid witness is absent.");
            selection = selection with { Aid = new(Text(root["healer"]?["ownerId"]) ?? "", Text(root["target"]?["ownerId"]) ?? "",
                Text(witness["contentFingerprint"]) ?? "") { ReceiveAid = IsReceiveAidHeader(root),
                    PlayerConfirmed = IsReceiveAidHeader(root) && Text(root["playerConfirmation"]) == "player_target_confirmed" } };
        }
        return SpiritualSelfTreatmentSelection.Parse(selection.ToJson());
    }

    internal static JsonObject CreateActive(SpiritualSelfTreatmentStageContext context, SpiritualSelfTreatmentSelection selection,
        AfterlifeWoundTreatmentAuthority.SourceData data, IReadOnlyList<int> draws)
    {
        if ((selection.Aid is null) != (data.AidWitness is null))
            throw new InvalidDataException("Active selection/source families differ; aid cannot downgrade to self.");
        return data.AidWitness is null
            ? Create(context, selection, data.Realm, data.CycleOrdinal, data.SourceFingerprint, data.Source.RollMode, draws)
            : CreateAid(context, selection, data, draws);
    }

    internal static bool IsExactActive(JsonObject root, SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, AfterlifeWoundTreatmentAuthority.SourceData data) => data.AidWitness is null
        ? selection.Aid is null && IsExact(root, context, selection, data.Realm, data.CycleOrdinal, data.SourceFingerprint, data.Source)
        : IsExactAid(root, context, selection, data);

    private static JsonObject CreateAid(SpiritualSelfTreatmentStageContext context, SpiritualSelfTreatmentSelection selection,
        AfterlifeWoundTreatmentAuthority.SourceData data, IReadOnlyList<int> draws)
    {
        var healer = SpiritualHealingRequestProjection.OwnerJson(data.Source.Healer);
        var target = SpiritualHealingRequestProjection.OwnerJson(data.Source.Target);
        var witness = data.AidWitness ?? throw new InvalidDataException("Aid creation requires its accepted grant witness.");
        var activeSource = ActiveSourceJson(data.Source);
        if (selection.Aid is null || selection.Aid is { ReceiveAid: true, PlayerConfirmed: false })
            throw new InvalidDataException("Aid creation requires its exact family and actual player confirmation.");
        var root = new JsonObject
        {
            ["schemaVersion"] = selection.Aid.ReceiveAid ? 4 : 3, ["kind"] = AidKind, ["mode"] = "aid",
            ["sessionId"] = context.SessionId, ["requestId"] = context.RequestId, ["turn"] = context.Turn,
            ["healer"] = healer, ["target"] = target, ["participation"] = witness.DeepClone(), ["activeSource"] = activeSource,
            ["attempt"] = new JsonObject
            {
                ["attemptId"] = AidAttemptId(context, selection, data.Realm, data.CycleOrdinal, data.SourceFingerprint, healer, target, witness, activeSource),
                ["woundId"] = selection.WoundId, ["woundFingerprint"] = selection.ExpectedWoundFingerprint,
                ["realm"] = data.Realm, ["cycleOrdinal"] = data.CycleOrdinal,
                ["cycleKey"] = AfterlifeWoundTreatmentAuthority.CycleKey(data.Realm, data.CycleOrdinal),
                ["sourceFingerprint"] = data.SourceFingerprint,
                ["roll"] = new JsonObject { ["mode"] = data.Source.RollMode,
                    ["values"] = new JsonArray(draws.Select(d => (JsonNode)JsonValue.Create(d)!).ToArray()) }
            }
        };
        if (selection.Aid.ReceiveAid)
        {
            root["direction"] = "guardian_to_player";
            root["playerConfirmation"] = "player_target_confirmed";
        }
        root["requestFingerprint"] = AidFingerprint(root);
        if (!IsBoundAid(root, context, selection, data.Realm, data.CycleOrdinal))
            throw new InvalidDataException("Aid factory cannot emit an unbound request.");
        return root;
    }

    internal static bool IsBoundAid(JsonObject root, SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, string realm, int ordinal)
    {
        try
        {
            if (selection.Aid is not { } aid) return false;
            var fields = new[] { "schemaVersion", "kind", "mode", "sessionId", "requestId", "turn", "healer", "target", "participation", "activeSource", "attempt", "requestFingerprint" };
            if (!IsAidHeader(root) || Int(root["schemaVersion"]) != (aid.ReceiveAid ? 4 : 3) ||
                aid.ReceiveAid && (!aid.PlayerConfirmed || Text(root["direction"]) != "guardian_to_player" || Text(root["playerConfirmation"]) != "player_target_confirmed") ||
                realm != "chaos_sea" || ordinal < 1 ||
                !Closed(root, aid.ReceiveAid ? fields.Concat(new[] { "direction", "playerConfirmation" }).ToArray() : fields) ||
                Text(root["sessionId"]) != context.SessionId || Text(root["requestId"]) != context.RequestId || Int(root["turn"]) != context.Turn ||
                root["healer"] is not JsonObject healer || root["target"] is not JsonObject target ||
                !JsonNode.DeepEquals(healer, SpiritualHealingRequestProjection.OwnerJson(aid.Healer(realm))) ||
                !JsonNode.DeepEquals(target, SpiritualHealingRequestProjection.OwnerJson(aid.Target(realm))) ||
                root["participation"] is not JsonObject witness ||
                !Closed(witness, "record", "contentFingerprint", "grantResultFingerprint") || witness["record"] is not JsonObject record)
                return false;
            if (root["activeSource"] is not JsonObject activeSource ||
                !Closed(activeSource, "healingTier", "complicationModifier", "rollMode", "rollContributions") ||
                Int(activeSource["healingTier"]) is null or < 0 or > 5 || activeSource["complicationModifier"] is not JsonValue modifier ||
                !modifier.TryGetValue<long>(out var complicationModifier) || complicationModifier < 0 ||
                Text(activeSource["rollMode"]) is not ("normal" or "advantage" or "disadvantage") ||
                activeSource["rollContributions"] is not JsonArray)
                return false;
            SpiritualTreatmentParticipation.ValidateRecord(record);
            if (!SpiritualTreatmentParticipation.IsAvailable(record) || SpiritualTreatmentParticipation.IsProviderOffer(record) != aid.ReceiveAid ||
                Text(record["healerId"]) != aid.HealerId || Text(record["targetId"]) != aid.TargetId ||
                Text(record["woundId"]) != selection.WoundId || Text(witness["contentFingerprint"]) != aid.ParticipationFingerprint ||
                Text(witness["contentFingerprint"]) != SpiritualTreatmentParticipation.Fingerprint(record) ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(Text(witness["grantResultFingerprint"])) ||
                Int(record["eligibleCycleOrdinal"]) != ordinal || Text(record["binding"]?["sessionId"]) != context.SessionId ||
                Int(record["binding"]?["turn"]) is not { } grantTurn || grantTurn >= context.Turn ||
                root["attempt"] is not JsonObject attempt ||
                !Closed(attempt, "attemptId", "woundId", "woundFingerprint", "realm", "cycleOrdinal", "cycleKey", "sourceFingerprint", "roll") ||
                Text(attempt["woundId"]) != selection.WoundId || Text(attempt["woundFingerprint"]) != selection.ExpectedWoundFingerprint ||
                Text(attempt["realm"]) != realm || Int(attempt["cycleOrdinal"]) != ordinal ||
                Text(attempt["cycleKey"]) != AfterlifeWoundTreatmentAuthority.CycleKey(realm, ordinal) ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(Text(attempt["sourceFingerprint"])) ||
                Text(attempt["attemptId"]) != AidAttemptId(context, selection, realm, ordinal, Text(attempt["sourceFingerprint"])!, healer, target, witness, activeSource) ||
                attempt["roll"] is not JsonObject roll || !Closed(roll, "mode", "values") ||
                Text(roll["mode"]) != Text(activeSource["rollMode"]) ||
                Text(roll["mode"]) is not ("normal" or "advantage" or "disadvantage") || roll["values"] is not JsonArray values ||
                values.Count != (Text(roll["mode"]) == "normal" ? 1 : 2) || values.Any(v => Int(v) is null or < 1 or > 20) ||
                Text(root["requestFingerprint"]) != AidFingerprint(root))
                return false;
            return true;
        }
        catch (Exception failure) when (failure is JsonException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException or NullReferenceException)
        { return false; }
    }

    private static bool IsExactAid(JsonObject root, SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, AfterlifeWoundTreatmentAuthority.SourceData data)
    {
        if (!IsBoundAid(root, context, selection, data.Realm, data.CycleOrdinal)) return false;
        var draws = root["attempt"]!["roll"]!["values"]!.AsArray().Select(v => Int(v)!.Value).ToArray();
        return SpiritualHealingResolver.ResolvePrepared(data.Source, data.Source.RollMode, draws).IsValid &&
            JsonNode.DeepEquals(root, CreateAid(context, selection, data, draws));
    }

    private static string AidAttemptId(SpiritualSelfTreatmentStageContext context, SpiritualSelfTreatmentSelection selection,
        string realm, int ordinal, string sourceFingerprint, JsonObject healer, JsonObject target, JsonObject witness, JsonObject activeSource) =>
        "spiritual_aid_attempt_" + WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            AidKind, selection.Aid?.ReceiveAid == true ? "4" : "3", context.SessionId, context.RequestId, context.Turn.ToString(CultureInfo.InvariantCulture),
            selection.WoundId, selection.ExpectedWoundFingerprint, realm, ordinal.ToString(CultureInfo.InvariantCulture), sourceFingerprint,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(healer), WoundAcceptedTurnFingerprintWriter.CanonicalJson(target),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(witness), WoundAcceptedTurnFingerprintWriter.CanonicalJson(activeSource)
        })[7..];

    private static string AidFingerprint(JsonObject root)
    {
        var copy = root.DeepClone().AsObject(); copy.Remove("requestFingerprint");
        return WoundAcceptedTurnFingerprintWriter.Compute(new[] { AidKind, Int(root["schemaVersion"])!.Value.ToString(CultureInfo.InvariantCulture), WoundAcceptedTurnFingerprintWriter.CanonicalJson(copy) });
    }
}
