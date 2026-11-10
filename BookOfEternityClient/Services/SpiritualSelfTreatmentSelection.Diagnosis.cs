using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record SpiritualDiagnosisSelection(WoundOwnerCoordinate Target, string DiagnosisPathId, string ExpectedDiagnosisPathFingerprint);

internal sealed partial record SpiritualSelfTreatmentSelection
{
    internal SpiritualDiagnosisSelection? Diagnosis { get; init; }

    internal void RequireTreatmentPurpose()
    {
        if (Diagnosis is not null || MortalDiagnosis is not null || MortalTreatment is not null) throw new InvalidDataException("This purpose requires its own execution boundary.");
    }

    private JsonObject DiagnosisJson()
    {
        if (Diagnosis is not { } diagnosis || MortalDiagnosis is not null || MortalTreatment is not null || Aid is not null || Combat is not null || Service is not null)
            throw new InvalidDataException("Diagnosis cannot mix with treatment families.");
        var root = new JsonObject { ["schemaVersion"] = 9, ["mode"] = "diagnose", ["woundId"] = WoundId,
            ["expectedWoundFingerprint"] = ExpectedWoundFingerprint, ["target"] = SpiritualHealingRequestProjection.OwnerJson(diagnosis.Target),
            ["diagnosisPathId"] = diagnosis.DiagnosisPathId, ["expectedDiagnosisPathFingerprint"] = diagnosis.ExpectedDiagnosisPathFingerprint };
        _ = ParseDiagnosis(this, root);
        return root;
    }

    private static SpiritualSelfTreatmentSelection ParseDiagnosis(SpiritualSelfTreatmentSelection selection, JsonObject root)
    {
        var fields = new[] { "schemaVersion", "mode", "woundId", "expectedWoundFingerprint", "target", "diagnosisPathId", "expectedDiagnosisPathFingerprint" };
        if (root.Count != fields.Length || fields.Any(field => !root.ContainsKey(field)) ||
            root["mode"] is not JsonValue mode || !mode.TryGetValue<string>(out var modeName) || modeName != "diagnose" ||
            root["target"] is not JsonObject target || target.Count != 4 ||
            new[] { "realm", "ownerKind", "ownerId", "carrierPath" }.Any(field => target[field] is not JsonValue value || !value.TryGetValue<string>(out _)) ||
            root["diagnosisPathId"] is not JsonValue pathNode || !pathNode.TryGetValue<string>(out var pathId) ||
            !ResourceMaterializationContract.IsExactIdentifier(pathId) ||
            root["expectedDiagnosisPathFingerprint"] is not JsonValue fingerprintNode || !fingerprintNode.TryGetValue<string>(out var fingerprint) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(fingerprint))
            throw new InvalidDataException("Invalid closed spiritual diagnosis selection.");
        var owner = new WoundOwnerCoordinate(target["realm"]!.GetValue<string>(), target["ownerKind"]!.GetValue<string>(),
            target["ownerId"]!.GetValue<string>(), target["carrierPath"]!.GetValue<string>());
        if (owner.Realm is not ("chaos_sea" or "shining_abode") || owner.CarrierPath != WoundCarrierCatalog.AfterlifeProfilesPath ||
            owner.OwnerKind is not ("player_soul" or "guardian" or "resident" or "radiant_actor" or "afterlife_actor") ||
            !ResourceMaterializationContract.IsExactIdentifier(owner.OwnerId) || owner.OwnerKind == "player_soul" && owner.OwnerId != "player_soul")
            throw new InvalidDataException("Diagnosis target is not an exact persistent spiritual owner.");
        return selection with { Diagnosis = new(owner, root["diagnosisPathId"]!.GetValue<string>(), root["expectedDiagnosisPathFingerprint"]!.GetValue<string>()) };
    }
}
