using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class SpiritualSelfTreatmentRequestCodec
{
    private const string AidKind = "spiritual_treatment_request";
    internal static string? AidDirection(int version) => version switch
    { 4 => "guardian_to_player", 5 => "player_to_resident", 6 => "resident_to_player", 8 => "player_to_shining_entity", _ => null };
    internal static bool IsAidHeader(JsonObject root) => Int(root["schemaVersion"]) is 3 or 4 or 5 or 6 or 8 &&
        Text(root["kind"]) == AidKind && Text(root["mode"]) == "aid";
    internal static bool IsReceiveAidHeader(JsonObject root) => IsAidHeader(root) && Int(root["schemaVersion"]) is 4 or 6;
    internal static bool IsActiveKind(JsonObject root) => Text(root["kind"]) is Kind or AidKind;
    internal static bool IsBoundActive(JsonObject root, SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, string realm, int ordinal) => selection.Service is not null
        ? IsBoundService(root, context, selection, realm, ordinal) : selection.Aid is null
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
        if (IsServiceHeader(root))
            selection = selection with { Service = new(AfterlifeHealingServiceContract.ParseQuote(root["quote"]),
                Text(root["playerConfirmation"]) == "player_service_confirmed") };
        else if (IsAidHeader(root))
        {
            var witness = root["participation"] as JsonObject ?? throw new InvalidDataException("Aid witness is absent.");
            var record = witness["record"] as JsonObject ?? throw new InvalidDataException("Aid witness record is absent.");
            SpiritualTreatmentParticipation.ValidateRecord(record);
            selection = selection with { Aid = SpiritualTreatmentParticipation.Selection(record) with
            { PlayerConfirmed = IsReceiveAidHeader(root) && Text(root["playerConfirmation"]) == "player_target_confirmed" } };
        }
        return SpiritualSelfTreatmentSelection.Parse(selection.ToJson());
    }

    internal static JsonObject CreateActive(SpiritualSelfTreatmentStageContext context, SpiritualSelfTreatmentSelection selection,
        AfterlifeWoundTreatmentAuthority.SourceData data, IReadOnlyList<int> draws)
    {
        if (selection.Service is not null || data.ServiceQuote is not null || data.ServicePayment is not null)
            return CreateService(context, selection, data, draws);
        if ((selection.Aid is null) != (data.AidWitness is null))
            throw new InvalidDataException("Active selection/source families differ; aid cannot downgrade to self.");
        return data.AidWitness is null
            ? Create(context, selection, data.Realm, data.CycleOrdinal, data.SourceFingerprint, data.Source.RollMode, draws)
            : CreateAid(context, selection, data, draws);
    }

    internal static bool IsExactActive(JsonObject root, SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, AfterlifeWoundTreatmentAuthority.SourceData data) =>
        selection.Service is not null || data.ServiceQuote is not null || data.ServicePayment is not null
            ? IsExactService(root, context, selection, data)
            : data.AidWitness is null
                ? selection.Aid is null && IsExact(root, context, selection, data.Realm, data.CycleOrdinal, data.SourceFingerprint, data.Source)
                : IsExactAid(root, context, selection, data);

    private static JsonObject CreateAid(SpiritualSelfTreatmentStageContext context, SpiritualSelfTreatmentSelection selection,
        AfterlifeWoundTreatmentAuthority.SourceData data, IReadOnlyList<int> draws)
    {
        var healer = SpiritualHealingRequestProjection.OwnerJson(data.Source.Healer);
        var target = SpiritualHealingRequestProjection.OwnerJson(data.Source.Target);
        var witness = data.AidWitness ?? throw new InvalidDataException("Aid creation requires its accepted grant witness.");
        var activeSource = ActiveSourceJson(data.Source);
        var currencyInvariant = selection.Aid?.Compensation is not null
            ? (data.NegotiatedWalletInvariant ?? throw new InvalidDataException("Negotiated treatment has no original wallet DATA.")).ToJson() : null;
        if (selection.Aid is null || selection.Aid is { ReceiveAid: true, PlayerConfirmed: false })
            throw new InvalidDataException("Aid creation requires its exact family and actual player confirmation.");
        var root = new JsonObject
        {
            ["schemaVersion"] = selection.Aid.FamilyVersion, ["kind"] = AidKind, ["mode"] = "aid",
            ["sessionId"] = context.SessionId, ["requestId"] = context.RequestId, ["turn"] = context.Turn,
            ["healer"] = healer, ["target"] = target, ["participation"] = witness.DeepClone(), ["activeSource"] = activeSource,
            ["attempt"] = new JsonObject
            {
                ["attemptId"] = AidAttemptId(context, selection, data.Realm, data.CycleOrdinal, data.SourceFingerprint, healer, target, witness, activeSource, currencyInvariant),
                ["woundId"] = selection.WoundId, ["woundFingerprint"] = selection.ExpectedWoundFingerprint,
                ["realm"] = data.Realm, ["cycleOrdinal"] = data.CycleOrdinal,
                ["cycleKey"] = AfterlifeWoundTreatmentAuthority.CycleKey(data.Realm, data.CycleOrdinal),
                ["sourceFingerprint"] = data.SourceFingerprint,
                ["roll"] = new JsonObject { ["mode"] = data.Source.RollMode,
                    ["values"] = new JsonArray(draws.Select(d => (JsonNode)JsonValue.Create(d)!).ToArray()) }
            }
        };
        if (AidDirection(selection.Aid.FamilyVersion) is { } direction) root["direction"] = direction;
        if (selection.Aid.ReceiveAid) root["playerConfirmation"] = "player_target_confirmed";
        if (currencyInvariant is not null) root["currencyInvariant"] = currencyInvariant;
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
            if (aid.Compensation is not null)
            {
                _ = SpiritualTreatmentWalletInvariant.Parse(root["currencyInvariant"]);
                fields = fields.Append("currencyInvariant").ToArray();
            }
            if (!IsAidHeader(root) || Int(root["schemaVersion"]) != aid.FamilyVersion ||
                aid.ReceiveAid && (!aid.PlayerConfirmed || Text(root["direction"]) != AidDirection(aid.FamilyVersion) || Text(root["playerConfirmation"]) != "player_target_confirmed") ||
                (aid.Resident is not null || aid.ShiningPatient is not null) && !aid.ReceiveAid && Text(root["direction"]) != AidDirection(aid.FamilyVersion) ||
                realm is not ("chaos_sea" or "shining_abode") || !aid.ReceiveAid && aid.ShiningPatient is null && realm != "chaos_sea" || ordinal < 1 ||
                !Closed(root, aid.ReceiveAid ? fields.Concat(new[] { "direction", "playerConfirmation" }).ToArray()
                    : aid.Resident is not null || aid.ShiningPatient is not null ? fields.Append("direction").ToArray() : fields) ||
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
            if (!SpiritualTreatmentParticipation.IsAvailable(record) || !SpiritualTreatmentParticipation.MatchesSelection(record, aid) ||
                Text(record["woundId"]) != selection.WoundId || Text(witness["contentFingerprint"]) != aid.ParticipationFingerprint ||
                Text(witness["contentFingerprint"]) != SpiritualTreatmentParticipation.Fingerprint(record) ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(Text(witness["grantResultFingerprint"])) ||
                Int(record["eligibleCycleOrdinal"]) != ordinal || Text(record["binding"]?["sessionId"]) != context.SessionId ||
                Int(record["binding"]?["turn"]) is not { } grantTurn || grantTurn >= context.Turn ||
                (aid.Resident is not null || aid.ShiningPatient is not null) && grantTurn != context.Turn - 1 ||
                root["attempt"] is not JsonObject attempt ||
                !Closed(attempt, "attemptId", "woundId", "woundFingerprint", "realm", "cycleOrdinal", "cycleKey", "sourceFingerprint", "roll") ||
                Text(attempt["woundId"]) != selection.WoundId || Text(attempt["woundFingerprint"]) != selection.ExpectedWoundFingerprint ||
                Text(attempt["realm"]) != realm || Int(attempt["cycleOrdinal"]) != ordinal ||
                Text(attempt["cycleKey"]) != AfterlifeWoundTreatmentAuthority.CycleKey(realm, ordinal) ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(Text(attempt["sourceFingerprint"])) ||
                Text(attempt["attemptId"]) != AidAttemptId(context, selection, realm, ordinal, Text(attempt["sourceFingerprint"])!, healer, target, witness, activeSource, root["currencyInvariant"] as JsonObject) ||
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
        string realm, int ordinal, string sourceFingerprint, JsonObject healer, JsonObject target, JsonObject witness, JsonObject activeSource,
        JsonObject? currencyInvariant = null)
    {
        var fields = new[]
        {
            AidKind, (selection.Aid?.FamilyVersion ?? 3).ToString(CultureInfo.InvariantCulture), context.SessionId, context.RequestId, context.Turn.ToString(CultureInfo.InvariantCulture),
            selection.WoundId, selection.ExpectedWoundFingerprint, realm, ordinal.ToString(CultureInfo.InvariantCulture), sourceFingerprint,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(healer), WoundAcceptedTurnFingerprintWriter.CanonicalJson(target),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(witness), WoundAcceptedTurnFingerprintWriter.CanonicalJson(activeSource)
        };
        if (currencyInvariant is not null) fields = fields.Append(WoundAcceptedTurnFingerprintWriter.CanonicalJson(currencyInvariant)).ToArray();
        return "spiritual_aid_attempt_" + WoundAcceptedTurnFingerprintWriter.Compute(fields)[7..];
    }

    private static string AidFingerprint(JsonObject root)
    {
        var copy = root.DeepClone().AsObject(); copy.Remove("requestFingerprint");
        return WoundAcceptedTurnFingerprintWriter.Compute(new[] { AidKind, Int(root["schemaVersion"])!.Value.ToString(CultureInfo.InvariantCulture), WoundAcceptedTurnFingerprintWriter.CanonicalJson(copy) });
    }
}
