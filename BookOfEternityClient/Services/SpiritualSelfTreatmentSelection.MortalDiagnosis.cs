using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundDiagnosisSelection(WoundOwnerCoordinate Target, WoundOwnerCoordinate Provider,
    string DiagnosisPathId, string ExpectedDiagnosisPathFingerprint, string ExpectedContextFingerprint);

internal sealed partial record SpiritualSelfTreatmentSelection
{
    // The existing guided carrier has a historical name. This distinct closed
    // purpose grants no afterlife staging, cycle or healing permission.
    internal MortalWoundDiagnosisSelection? MortalDiagnosis { get; init; }

    private JsonObject MortalDiagnosisJson()
    {
        if (MortalDiagnosis is not { } diagnosis || Diagnosis is not null || Aid is not null || Combat is not null || Service is not null)
            throw new InvalidDataException("Mortal diagnosis cannot mix wound execution purposes.");
        var root = new JsonObject
        {
            ["schemaVersion"] = 10, ["mode"] = "mortal_diagnose", ["woundId"] = WoundId,
            ["expectedWoundFingerprint"] = ExpectedWoundFingerprint,
            ["target"] = SpiritualHealingRequestProjection.OwnerJson(diagnosis.Target),
            ["provider"] = SpiritualHealingRequestProjection.OwnerJson(diagnosis.Provider),
            ["diagnosisPathId"] = diagnosis.DiagnosisPathId,
            ["expectedDiagnosisPathFingerprint"] = diagnosis.ExpectedDiagnosisPathFingerprint,
            ["expectedContextFingerprint"] = diagnosis.ExpectedContextFingerprint
        };
        _ = ParseMortalDiagnosis(this, root);
        return root;
    }

    private static SpiritualSelfTreatmentSelection ParseMortalDiagnosis(SpiritualSelfTreatmentSelection selection, JsonObject root)
    {
        var fields = new[] { "schemaVersion", "mode", "woundId", "expectedWoundFingerprint", "target", "provider",
            "diagnosisPathId", "expectedDiagnosisPathFingerprint", "expectedContextFingerprint" };
        string Text(string key) => root[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
        if (root.Count != fields.Length || fields.Any(field => !root.ContainsKey(field)) ||
            root["schemaVersion"] is not JsonValue version || !version.TryGetValue<int>(out var schema) || schema != 10 ||
            Text("mode") != "mortal_diagnose" || !ResourceMaterializationContract.IsExactIdentifier(Text("diagnosisPathId")) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(Text("expectedDiagnosisPathFingerprint")) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(Text("expectedContextFingerprint")))
            throw new InvalidDataException("Invalid closed Mortal diagnosis selection.");
        var target = ReadMortalDiagnosisOwner(root["target"]);
        var provider = ReadMortalDiagnosisOwner(root["provider"]);
        return selection with { MortalDiagnosis = new(target, provider, Text("diagnosisPathId"),
            Text("expectedDiagnosisPathFingerprint"), Text("expectedContextFingerprint")) };
    }

    internal static WoundOwnerCoordinate ReadMortalDiagnosisOwner(JsonNode? node)
    {
        var fields = new[] { "realm", "ownerKind", "ownerId", "carrierPath" };
        if (node is not JsonObject root || root.Count != fields.Length ||
            fields.Any(field => root[field] is not JsonValue value || !value.TryGetValue<string>(out _)))
            throw new InvalidDataException("Mortal diagnosis requires an exact typed actor coordinate.");
        var owner = new WoundOwnerCoordinate(root["realm"]!.GetValue<string>(), root["ownerKind"]!.GetValue<string>(),
            root["ownerId"]!.GetValue<string>(), root["carrierPath"]!.GetValue<string>());
        if (owner.Realm != "mortal_world" || !ResourceMaterializationContract.IsExactIdentifier(owner.OwnerId) ||
            !(owner.OwnerKind == "player" && owner.OwnerId == "player_current" && owner.CarrierPath == WoundCarrierCatalog.PlayerPath ||
              owner.OwnerKind == "npc" && owner.CarrierPath == WoundCarrierCatalog.NpcPath ||
              (owner.OwnerKind is "combatant" or "combatant_member") &&
                (owner.CarrierPath == WoundCarrierCatalog.EnemiesPath || owner.CarrierPath == WoundCarrierCatalog.AlliesPath)))
            throw new InvalidDataException("Mortal diagnosis actor kind, realm and carrier differ.");
        return owner;
    }
}
