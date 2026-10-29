using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class SpiritualSelfTreatmentRequestCodec
{
    internal static bool IsServiceHeader(JsonObject root) => Int(root["schemaVersion"]) == 7 &&
        Text(root["kind"]) == AidKind && Text(root["mode"]) == "service";

    internal static string ServiceAttemptId(SpiritualSelfTreatmentStageContext context,
        AfterlifeHealingServiceQuote quote, string realm, int ordinal, string sourceFingerprint) =>
        "spiritual_service_attempt_" + WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            AidKind, "7", context.SessionId, context.RequestId, context.Turn.ToString(CultureInfo.InvariantCulture),
            realm, ordinal.ToString(CultureInfo.InvariantCulture), quote.Fingerprint, sourceFingerprint
        })[7..];

    private static JsonObject CreateService(SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, AfterlifeWoundTreatmentAuthority.SourceData data, IReadOnlyList<int> draws)
    {
        if (selection is not { Service.PlayerConfirmed: true, Aid: null, Combat: null } || data.AidWitness is not null ||
            data.ServiceQuote is not { } quote || quote != selection.Service.Quote || data.ServicePayment is not { } payment)
            throw new InvalidDataException("Service creation requires the exact confirmed quote and prepared payment DATA.");
        var root = new JsonObject
        {
            ["schemaVersion"] = 7, ["kind"] = AidKind, ["mode"] = "service",
            ["sessionId"] = context.SessionId, ["requestId"] = context.RequestId, ["turn"] = context.Turn,
            ["healer"] = SpiritualHealingRequestProjection.OwnerJson(quote.Provider),
            ["target"] = SpiritualHealingRequestProjection.OwnerJson(quote.Patient),
            ["quote"] = quote.ToJson(), ["payment"] = payment.ToJson(),
            ["activeSource"] = ActiveSourceJson(data.Source), ["playerConfirmation"] = "player_service_confirmed",
            ["attempt"] = new JsonObject
            {
                ["attemptId"] = ServiceAttemptId(context, quote, data.Realm, data.CycleOrdinal, data.SourceFingerprint),
                ["woundId"] = selection.WoundId, ["woundFingerprint"] = selection.ExpectedWoundFingerprint,
                ["realm"] = data.Realm, ["cycleOrdinal"] = data.CycleOrdinal,
                ["cycleKey"] = AfterlifeWoundTreatmentAuthority.CycleKey(data.Realm, data.CycleOrdinal),
                ["sourceFingerprint"] = data.SourceFingerprint,
                ["roll"] = new JsonObject { ["mode"] = data.Source.RollMode,
                    ["values"] = new JsonArray(draws.Select(d => (JsonNode)JsonValue.Create(d)!).ToArray()) }
            }
        };
        root["requestFingerprint"] = ServiceFingerprint(root);
        if (!IsBoundService(root, context, selection, data.Realm, data.CycleOrdinal))
            throw new InvalidDataException("Service factory cannot emit an unbound request.");
        return root;
    }

    internal static bool IsBoundService(JsonObject root, SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, string realm, int ordinal)
    {
        try
        {
            if (!IsServiceHeader(root) || selection is not { Service.PlayerConfirmed: true, Aid: null, Combat: null } ||
                !Closed(root, "schemaVersion", "kind", "mode", "sessionId", "requestId", "turn", "healer", "target",
                    "quote", "payment", "activeSource", "playerConfirmation", "attempt", "requestFingerprint") ||
                realm != "chaos_sea" || ordinal < 1 || Text(root["sessionId"]) != context.SessionId ||
                Text(root["requestId"]) != context.RequestId || Int(root["turn"]) != context.Turn ||
                Text(root["playerConfirmation"]) != "player_service_confirmed") return false;
            var quote = AfterlifeHealingServiceContract.ParseQuote(root["quote"]);
            if (quote != selection.Service.Quote || quote.WoundId != selection.WoundId ||
                quote.WoundFingerprint != selection.ExpectedWoundFingerprint ||
                !JsonNode.DeepEquals(root["healer"], SpiritualHealingRequestProjection.OwnerJson(quote.Provider)) ||
                !JsonNode.DeepEquals(root["target"], SpiritualHealingRequestProjection.OwnerJson(quote.Patient)) ||
                root["attempt"] is not JsonObject attempt ||
                !Closed(attempt, "attemptId", "woundId", "woundFingerprint", "realm", "cycleOrdinal", "cycleKey", "sourceFingerprint", "roll") ||
                Text(attempt["woundId"]) != quote.WoundId || Text(attempt["woundFingerprint"]) != quote.WoundFingerprint ||
                Text(attempt["realm"]) != realm || Int(attempt["cycleOrdinal"]) != ordinal ||
                Text(attempt["cycleKey"]) != AfterlifeWoundTreatmentAuthority.CycleKey(realm, ordinal) ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(Text(attempt["sourceFingerprint"])) ||
                Text(attempt["attemptId"]) != ServiceAttemptId(context, quote, realm, ordinal, Text(attempt["sourceFingerprint"])!)) return false;
            _ = AfterlifeHealingServicePlanner.ParsePayment(root["payment"], quote, Text(attempt["attemptId"])!);
            var source = ReadServiceActiveSource(root["activeSource"], quote);
            if (attempt["roll"] is not JsonObject roll || !Closed(roll, "mode", "values") ||
                Text(roll["mode"]) != source.RollMode || roll["values"] is not JsonArray values ||
                values.Count != (source.RollMode == "normal" ? 1 : 2) || values.Any(v => Int(v) is null or < 1 or > 20) ||
                !SpiritualHealingResolver.ResolvePrepared(source, source.RollMode, values.Select(v => Int(v)!.Value).ToArray()).IsValid ||
                Text(root["requestFingerprint"]) != ServiceFingerprint(root)) return false;
            return true;
        }
        catch (Exception failure) when (failure is JsonException or InvalidDataException or InvalidOperationException or
            ArgumentException or OverflowException or NullReferenceException) { return false; }
    }

    internal static SpiritualHealingPreparedSource ReadServiceActiveSource(JsonNode? node, AfterlifeHealingServiceQuote quote)
    {
        if (node is not JsonObject root || !Closed(root, "healingTier", "complicationModifier", "rollMode", "rollContributions") ||
            Int(root["healingTier"]) != quote.HealingTier || root["complicationModifier"] is not JsonValue modifier ||
            !modifier.TryGetValue<long>(out var complicationModifier) || complicationModifier < 0 ||
            Text(root["rollMode"]) is not ("normal" or "advantage" or "disadvantage") ||
            root["rollContributions"] is not JsonArray contributions || contributions.Count > EffectRollContributionResolver.MaximumCapturedRows)
            throw new InvalidDataException("Invalid closed service check source.");
        var source = new SpiritualHealingPreparedSource(quote.WoundId, quote.Patient, quote.Provider, quote.SeverityRank,
            quote.HealingTier, complicationModifier, Text(root["rollMode"])!,
            contributions.Deserialize<EffectRollContributionEvidence[]>() ?? throw new InvalidDataException("Service roll evidence is absent."));
        if (!JsonNode.DeepEquals(ActiveSourceJson(source), root) ||
            AfterlifeHealingServiceContract.HealingCheckFingerprint(source) != quote.CheckFingerprint)
            throw new InvalidDataException("Service check differs from its sealed quote.");
        return source;
    }

    private static bool IsExactService(JsonObject root, SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, AfterlifeWoundTreatmentAuthority.SourceData data) =>
        IsBoundService(root, context, selection, data.Realm, data.CycleOrdinal) && data.ServiceQuote is not null &&
        data.ServicePayment is not null && data.AidWitness is null &&
        JsonNode.DeepEquals(root, CreateService(context, selection, data,
            root["attempt"]!["roll"]!["values"]!.AsArray().Select(v => Int(v)!.Value).ToArray()));

    private static string ServiceFingerprint(JsonObject root)
    {
        var copy = root.DeepClone().AsObject(); copy.Remove("requestFingerprint");
        return WoundAcceptedTurnFingerprintWriter.Compute(new[] { AidKind, "7", WoundAcceptedTurnFingerprintWriter.CanonicalJson(copy) });
    }
}
